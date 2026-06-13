using ModSetup.Core.Models;
using ModSetup.Core.Services;

namespace ModSetup.App.Services;

public sealed class ProfileCatalogService
{
    private const string SetupExecutableSuffix = "-Setup";
    private const string DefaultProfilesBaseUrl = "https://raw.githubusercontent.com/TomachiGachiAnti/ModSynchronizer/main/profiles";

    private readonly HttpClient _httpClient;
    private readonly ProfileLoader _profileLoader;

    public ProfileCatalogService(HttpClient httpClient, ProfileLoader profileLoader)
    {
        _httpClient = httpClient;
        _profileLoader = profileLoader;
    }

    public async Task<IReadOnlyList<ProfileCatalogEntry>> LoadAvailableProfilesAsync(CancellationToken cancellationToken)
    {
        var preferredProfileName = GetPreferredProfileName();
        if (!string.IsNullOrWhiteSpace(preferredProfileName))
        {
            var remoteEntry = await LoadRemoteProfileAsync(preferredProfileName, cancellationToken);
            return [remoteEntry];
        }

        return LoadLocalProfiles();
    }

    public async Task<ProfileCatalogEntry> RefreshAsync(ProfileCatalogEntry entry, CancellationToken cancellationToken)
    {
        if (!entry.IsRemote)
        {
            return entry;
        }

        return await LoadRemoteProfileAsync(entry.ProfileName, cancellationToken);
    }

    private IReadOnlyList<ProfileCatalogEntry> LoadLocalProfiles()
    {
        var profilesDirectory = ResolveLocalProfilesDirectory();
        if (string.IsNullOrWhiteSpace(profilesDirectory) || !Directory.Exists(profilesDirectory))
        {
            return [];
        }

        var entries = new List<ProfileCatalogEntry>();
        foreach (var path in Directory.GetFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = _profileLoader.LoadFromFile(path);
                entries.Add(new ProfileCatalogEntry(
                    Path.GetFileNameWithoutExtension(path),
                    profile.DisplayName,
                    path,
                    false));
            }
            catch
            {
                entries.Add(new ProfileCatalogEntry(
                    Path.GetFileNameWithoutExtension(path),
                    $"読み込み失敗: {Path.GetFileName(path)}",
                    path,
                    false));
            }
        }

        return entries;
    }

    private static string? ResolveLocalProfilesDirectory()
    {
        var directPath = Path.Combine(AppContext.BaseDirectory, "profiles");
        if (Directory.Exists(directPath))
        {
            return directPath;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "ModSetup.sln");
            var profilesPath = Path.Combine(current.FullName, "profiles");
            if (File.Exists(solutionPath) && Directory.Exists(profilesPath))
            {
                return profilesPath;
            }

            current = current.Parent;
        }

        return null;
    }

    private async Task<ProfileCatalogEntry> LoadRemoteProfileAsync(string profileName, CancellationToken cancellationToken)
    {
        var cachePath = GetCachePath(profileName);
        var remoteUrl = BuildProfileUrl(profileName);

        try
        {
            using var response = await _httpClient.GetAsync(remoteUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, json, cancellationToken);
        }
        catch
        {
            if (!File.Exists(cachePath))
            {
                throw new InvalidOperationException($"GitHub 上の構成ファイルを取得できませんでした: {remoteUrl}");
            }
        }

        var profile = _profileLoader.LoadFromFile(cachePath);
        return new ProfileCatalogEntry(profileName, profile.DisplayName, cachePath, true);
    }

    private static string BuildProfileUrl(string profileName)
    {
        var baseUrl = Environment.GetEnvironmentVariable("MODSETUP_PROFILES_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultProfilesBaseUrl;
        }

        return $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(profileName)}.json";
    }

    private static string GetCachePath(string profileName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LocalApplicationData フォルダが取得できません。");
        }

        return Path.Combine(localAppData, "ModSetup", "profiles-cache", $"{profileName}.json");
    }

    private static string? GetPreferredProfileName()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var executableName = Path.GetFileNameWithoutExtension(processPath);
        if (!executableName.EndsWith(SetupExecutableSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return executableName[..^SetupExecutableSuffix.Length];
    }
}

public sealed record ProfileCatalogEntry(string ProfileName, string DisplayName, string Path, bool IsRemote);
