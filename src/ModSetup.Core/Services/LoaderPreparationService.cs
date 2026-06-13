using ModSetup.Core.Models;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace ModSetup.Core.Services;

public sealed class LoaderPreparationService
{
    private const string TemporaryJavaApiUrl = "https://api.adoptium.net/v3/assets/latest/21/hotspot?architecture=x64&image_type=jre&os=windows&heap_size=normal&vendor=eclipse";

    private readonly DownloadService _downloadService;
    private readonly JavaRuntimeResolver _javaRuntimeResolver;
    private readonly HashService _hashService;

    public LoaderPreparationService(
        DownloadService downloadService,
        JavaRuntimeResolver javaRuntimeResolver,
        HashService hashService)
    {
        _downloadService = downloadService;
        _javaRuntimeResolver = javaRuntimeResolver;
        _hashService = hashService;
    }

    public async Task<PreparationResult> PrepareLoaderAsync(
        ProfileConfig profile,
        EnvironmentCheckResult environment,
        CancellationToken cancellationToken)
    {
        var result = new PreparationResult();

        if (environment.LoaderInstalled)
        {
            return result;
        }

        if (string.IsNullOrWhiteSpace(profile.Loader.InstallerUrl))
        {
            result.RequiresManualLoaderInstall = true;
            return result;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ModSetup");
        Directory.CreateDirectory(tempDirectory);

        var installerFileName = BuildInstallerFileName(profile);
        var installerPath = Path.Combine(tempDirectory, installerFileName);

        await _downloadService.DownloadFileAsync(profile.Loader.InstallerUrl, installerPath, cancellationToken);

        result.LoaderInstallAttempted = true;
        result.LoaderInstallerUrl = profile.Loader.InstallerUrl;
        result.LoaderInstallerPath = installerPath;
        string? temporaryJavaRoot = null;

        try
        {
            var resolvedJava = await ResolveJavaExecutableWithFallbackAsync(cancellationToken);
            var javaExecutable = resolvedJava.JavaExecutable;
            temporaryJavaRoot = resolvedJava.TemporaryJavaRoot;
            result.LoaderInstallSucceeded = await TryInstallClientAsync(javaExecutable, installerPath, environment.MinecraftRoot, cancellationToken);
        }
        finally
        {
            CleanupTemporaryJavaRuntime(temporaryJavaRoot);
        }

        result.RequiresManualLoaderInstall = !result.LoaderInstallSucceeded;
        return result;
    }

    private static async Task<bool> TryInstallClientAsync(
        string javaExecutable,
        string installerPath,
        string minecraftRoot,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = javaExecutable,
            Arguments = $"-jar \"{installerPath}\" --install-client \"{minecraftRoot}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = minecraftRoot
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("NeoForge インストーラーを起動できませんでした。");
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            return true;
        }

        var details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        if (string.IsNullOrWhiteSpace(details))
        {
            throw new InvalidOperationException($"NeoForge の導入に失敗しました。手動でインストーラーを実行してください: {installerPath}");
        }

        throw new InvalidOperationException(
            $"NeoForge の導入に失敗しました。手動でインストーラーを実行してください: {installerPath}{Environment.NewLine}{details.Trim()}");
    }

    private static string BuildInstallerFileName(ProfileConfig profile)
    {
        var loaderType = string.IsNullOrWhiteSpace(profile.Loader.Type) ? "loader" : profile.Loader.Type;
        var version = string.IsNullOrWhiteSpace(profile.Loader.Version) ? "unknown" : profile.Loader.Version;
        return $"{loaderType}-{version}-installer.jar";
    }

    private async Task<(string JavaExecutable, string? TemporaryJavaRoot)> ResolveJavaExecutableWithFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (_javaRuntimeResolver.ResolveJavaExecutable(), null);
        }
        catch (InvalidOperationException)
        {
            var temporaryJavaRoot = await DownloadTemporaryJavaRuntimeAsync(cancellationToken);
            var javaExecutable = Directory
                .GetFiles(temporaryJavaRoot, "java.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(javaExecutable))
            {
                throw new InvalidOperationException("一時 Java 実行環境の展開後も java.exe が見つかりませんでした。");
            }

            return (javaExecutable, temporaryJavaRoot);
        }
    }

    private async Task<string> DownloadTemporaryJavaRuntimeAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(TemporaryJavaApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("一時 Java 実行環境の取得情報が空でした。");
        }

        var binary = root[0].GetProperty("binary");
        var package = binary.GetProperty("package");
        var packageUrl = package.GetProperty("link").GetString();
        var packageChecksum = package.GetProperty("checksum").GetString();
        var packageName = package.GetProperty("name").GetString();

        if (string.IsNullOrWhiteSpace(packageUrl) || string.IsNullOrWhiteSpace(packageName))
        {
            throw new InvalidOperationException("一時 Java 実行環境のダウンロード URL が取得できませんでした。");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "ModSetup", "temp-java", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var zipPath = Path.Combine(tempRoot, packageName);
        await _downloadService.DownloadFileAsync(packageUrl, zipPath, cancellationToken);

        if (!_hashService.VerifySha256(zipPath, packageChecksum ?? ""))
        {
            throw new InvalidOperationException("一時 Java 実行環境の SHA-256 検証に失敗しました。");
        }

        var extractDirectory = Path.Combine(tempRoot, "extracted");
        ZipFile.ExtractToDirectory(zipPath, extractDirectory);
        return extractDirectory;
    }

    private static void CleanupTemporaryJavaRuntime(string? temporaryJavaRoot)
    {
        if (string.IsNullOrWhiteSpace(temporaryJavaRoot))
        {
            return;
        }

        var rootDirectory = Directory.GetParent(temporaryJavaRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(rootDirectory, true);
        }
        catch
        {
        }
    }
}
