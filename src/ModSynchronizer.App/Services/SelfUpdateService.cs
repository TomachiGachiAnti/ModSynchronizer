using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ModSynchronizer.Core.Models;
using ModSynchronizer.Core.Services;

namespace ModSynchronizer.App.Services;

public sealed class SelfUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly DownloadService _downloadService;
    private readonly HashService _hashService;

    public SelfUpdateService(HttpClient httpClient, DownloadService downloadService, HashService hashService)
    {
        _httpClient = httpClient;
        _downloadService = downloadService;
        _hashService = hashService;
    }

    public async Task<SelfUpdateResult> CheckAndApplyAsync(
        ProfileConfig profile,
        CancellationToken cancellationToken,
        bool relaunchAfterUpdate = true,
        string? relaunchArgumentsOverride = null)
    {
        var result = new SelfUpdateResult
        {
            CurrentVersion = GetCurrentVersion().ToString()
        };

        if (!profile.SelfUpdate.Enabled)
        {
            return result;
        }

        result.Checked = true;

        try
        {
            if (!string.IsNullOrWhiteSpace(profile.SelfUpdate.GithubReleasesApiUrl))
            {
                return await CheckGithubReleaseAndApplyAsync(profile, result, cancellationToken, relaunchAfterUpdate, relaunchArgumentsOverride);
            }

            if (!string.IsNullOrWhiteSpace(profile.SelfUpdate.ManifestUrl))
            {
                return await CheckManifestAndApplyAsync(profile, result, cancellationToken, relaunchAfterUpdate, relaunchArgumentsOverride);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.WarningMessage = $"自己更新を確認できなかったため、現在の版で続行します。{Environment.NewLine}{ex.Message}";
        }

        return result;
    }

    private async Task<SelfUpdateResult> CheckManifestAndApplyAsync(
        ProfileConfig profile,
        SelfUpdateResult result,
        CancellationToken cancellationToken,
        bool relaunchAfterUpdate,
        string? relaunchArgumentsOverride)
    {
        var manifestUri = new Uri(profile.SelfUpdate.ManifestUrl, UriKind.Absolute);
        using var response = await _httpClient.GetAsync(manifestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<SelfUpdateManifest>(stream, JsonOptions, cancellationToken);
        if (manifest is null)
        {
            throw new InvalidOperationException("自己更新マニフェストを読み込めませんでした。");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("自己更新マニフェストに version がありません。");
        }

        if (string.IsNullOrWhiteSpace(manifest.Url))
        {
            throw new InvalidOperationException("自己更新マニフェストに url がありません。");
        }

        if (!Version.TryParse(manifest.Version, out var latestVersion))
        {
            throw new InvalidOperationException($"自己更新マニフェストの version が不正です: {manifest.Version}");
        }

        var currentVersion = GetCurrentVersion();
        result.LatestVersion = latestVersion.ToString();

        if (latestVersion <= currentVersion)
        {
            return result;
        }

        result.UpdateAvailable = true;

        var downloadUri = ResolveDownloadUri(manifestUri, manifest.Url);
        result.DownloadUrl = downloadUri.AbsoluteUri;

        var downloadPath = await DownloadUpdateAsync(profile, latestVersion, downloadUri.AbsoluteUri, cancellationToken);

        if (!_hashService.VerifySha256(downloadPath, manifest.Sha256))
        {
            throw new InvalidOperationException("自己更新ファイルの SHA-256 検証に失敗しました。");
        }

        result.DownloadPath = downloadPath;

        ScheduleReplacement(GetRequiredProcessPath(), downloadPath, relaunchAfterUpdate, relaunchArgumentsOverride);
        result.UpdateScheduled = true;
        return result;
    }

    private async Task<SelfUpdateResult> CheckGithubReleaseAndApplyAsync(
        ProfileConfig profile,
        SelfUpdateResult result,
        CancellationToken cancellationToken,
        bool relaunchAfterUpdate,
        string? relaunchArgumentsOverride)
    {
        var apiUri = new Uri(profile.SelfUpdate.GithubReleasesApiUrl, UriKind.Absolute);
        using var response = await _httpClient.GetAsync(apiUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GithubLatestRelease>(stream, JsonOptions, cancellationToken);
        if (release is null)
        {
            throw new InvalidOperationException("GitHub Releases の応答を読み込めませんでした。");
        }

        var latestVersionText = NormalizeVersionText(release.TagName);
        if (string.IsNullOrWhiteSpace(latestVersionText))
        {
            throw new InvalidOperationException("GitHub Releases の tag_name が取得できませんでした。");
        }

        if (!Version.TryParse(latestVersionText, out var latestVersion))
        {
            throw new InvalidOperationException($"GitHub Releases の tag_name が不正です: {release.TagName}");
        }

        var currentVersion = GetCurrentVersion();
        result.LatestVersion = latestVersion.ToString();
        if (latestVersion <= currentVersion)
        {
            return result;
        }

        var asset = SelectGithubAsset(profile, release);
        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException("GitHub Releases から対象 exe アセットを見つけられませんでした。");
        }

        result.UpdateAvailable = true;
        result.DownloadUrl = asset.BrowserDownloadUrl;
        var downloadPath = await DownloadUpdateAsync(profile, latestVersion, asset.BrowserDownloadUrl, cancellationToken);
        result.DownloadPath = downloadPath;
        ScheduleReplacement(GetRequiredProcessPath(), downloadPath, relaunchAfterUpdate, relaunchArgumentsOverride);
        result.UpdateScheduled = true;
        return result;
    }

    private static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var numericPart = informationalVersion.Split('+', 2)[0];
            if (Version.TryParse(numericPart, out var parsedInformationalVersion))
            {
                return parsedInformationalVersion;
            }
        }

        return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private static Uri ResolveDownloadUri(Uri manifestUri, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        return new Uri(manifestUri, url);
    }

    private async Task<string> DownloadUpdateAsync(
        ProfileConfig profile,
        Version latestVersion,
        string downloadUrl,
        CancellationToken cancellationToken)
    {
        var processPath = GetRequiredProcessPath();
        var extension = Path.GetExtension(processPath);
        var downloadFileName = $"{Path.GetFileNameWithoutExtension(processPath)}-{latestVersion}{extension}";
        var downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            "ModSynchronizer",
            "self-update",
            profile.ConfigId,
            latestVersion.ToString());
        var downloadPath = Path.Combine(downloadDirectory, downloadFileName);

        await _downloadService.DownloadFileAsync(downloadUrl, downloadPath, cancellationToken);
        return downloadPath;
    }

    private static string GetRequiredProcessPath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("現在の実行ファイルパスを取得できませんでした。");
        }

        return processPath;
    }

    private static GithubReleaseAsset? SelectGithubAsset(ProfileConfig profile, GithubLatestRelease release)
    {
        var configuredAssetName = profile.SelfUpdate.AssetName;
        if (!string.IsNullOrWhiteSpace(configuredAssetName))
        {
            var configuredMatch = release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, configuredAssetName, StringComparison.OrdinalIgnoreCase));
            if (configuredMatch is not null)
            {
                return configuredMatch;
            }
        }

        var currentExecutableName = Path.GetFileName(GetRequiredProcessPath());
        var sameNameAsset = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, currentExecutableName, StringComparison.OrdinalIgnoreCase));
        if (sameNameAsset is not null)
        {
            return sameNameAsset;
        }

        return release.Assets.FirstOrDefault(asset =>
            asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    private static void ScheduleReplacement(
        string currentExecutablePath,
        string downloadedExecutablePath,
        bool relaunchAfterUpdate,
        string? relaunchArgumentsOverride)
    {
        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            "ModSynchronizer",
            "self-update",
            Guid.NewGuid().ToString("N"),
            "apply-update.ps1");

        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrWhiteSpace(scriptDirectory))
        {
            Directory.CreateDirectory(scriptDirectory);
        }

        File.WriteAllText(scriptPath, BuildScript(), new UTF8Encoding(false));

        var arguments = relaunchArgumentsOverride ?? string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(QuotePowerShellArgument));
        var process = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments =
                $"-NoProfile -ExecutionPolicy Bypass -File {QuotePowerShellArgument(scriptPath)} " +
                $"-TargetProcessId {process.Id} " +
                $"-SourcePath {QuotePowerShellArgument(downloadedExecutablePath)} " +
                $"-DestinationPath {QuotePowerShellArgument(currentExecutablePath)} " +
                $"-RelaunchPath {QuotePowerShellArgument(currentExecutablePath)} " +
                $"-OriginalArguments {QuotePowerShellArgument(arguments)} " +
                $"-RelaunchAfterUpdate {(relaunchAfterUpdate ? "$true" : "$false")}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(startInfo);
    }

    private static string BuildScript()
    {
        return """
param(
    [int]$TargetProcessId,
    [string]$SourcePath,
    [string]$DestinationPath,
    [string]$RelaunchPath,
    [string]$OriginalArguments,
    [bool]$RelaunchAfterUpdate
)

$ErrorActionPreference = "Stop"

for ($i = 0; $i -lt 240; $i++) {
    if (-not (Get-Process -Id $TargetProcessId -ErrorAction SilentlyContinue)) {
        break
    }

    Start-Sleep -Milliseconds 500
}

Start-Sleep -Milliseconds 500

if (Test-Path -LiteralPath $DestinationPath) {
    Remove-Item -LiteralPath $DestinationPath -Force
}

Move-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force

if ($RelaunchAfterUpdate) {
    if ([string]::IsNullOrWhiteSpace($OriginalArguments)) {
        Start-Process -FilePath $RelaunchPath
    }
    else {
        Start-Process -FilePath $RelaunchPath -ArgumentList $OriginalArguments
    }
}

Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
""";
    }

    private static string QuotePowerShellArgument(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private static string NormalizeVersionText(string versionText)
    {
        var normalized = versionText.Trim();
        if (normalized.StartsWith("v.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }
        else
        {
            normalized = normalized.TrimStart('v', 'V');
        }

        return normalized.Trim();
    }
}
