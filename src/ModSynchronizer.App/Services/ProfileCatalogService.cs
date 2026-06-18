using ModSynchronizer.Core.Models;
using ModSynchronizer.Core.Services;
using System.Reflection;

namespace ModSynchronizer.App.Services;

public sealed class ProfileCatalogService
{
    private const string SetupExecutableSuffix = "-Setup";
    private const string DefaultProfilesBaseUrl = "https://raw.githubusercontent.com/TomachiGachiAnti/ModSynchronizer/main/profiles";
    private const string ProfilesBaseUrlEnvironmentVariableName = "MODSYNCHRONIZER_PROFILES_BASE_URL";
    private const string LegacyProfilesBaseUrlEnvironmentVariableName = "MODSETUP_PROFILES_BASE_URL";
    private const string CacheRootDirectoryName = "ModSynchronizer";
    private const string LegacyCacheRootDirectoryName = "ModSetup";
    private const string CacheSubDirectoryName = "profiles-cache";

    private readonly HttpClient _httpClient;
    private readonly ProfileLoader _profileLoader;
    public string? PreferredProfileName { get; }
    public bool HasFixedProfile => !string.IsNullOrWhiteSpace(PreferredProfileName);

    public ProfileCatalogService(HttpClient httpClient, ProfileLoader profileLoader, string? preferredProfileName = null)
    {
        _httpClient = httpClient;
        _profileLoader = profileLoader;
        PreferredProfileName = ResolvePreferredProfileName(preferredProfileName);
    }

    public async Task<IReadOnlyList<ProfileCatalogEntry>> LoadAvailableProfilesAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(PreferredProfileName))
        {
            var remoteEntry = await LoadRemoteProfileAsync(PreferredProfileName, cancellationToken);
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

    public string GetRequiredProfileName()
    {
        if (!string.IsNullOrWhiteSpace(PreferredProfileName))
        {
            return PreferredProfileName;
        }

        throw new InvalidOperationException("プロファイル名が確定できません。");
    }

    public Task<ProfileCatalogEntry> LoadProfileByNameAsync(string profileName, CancellationToken cancellationToken)
    {
        return LoadRemoteProfileAsync(profileName, cancellationToken);
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
            var solutionPath = Path.Combine(current.FullName, "ModSynchronizer.sln");
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
        var localOverridePath = TryResolveLocalOverridePath(profileName);
        if (!string.IsNullOrWhiteSpace(localOverridePath))
        {
            var localProfile = _profileLoader.LoadFromFile(localOverridePath);
            return new ProfileCatalogEntry(profileName, localProfile.DisplayName, localOverridePath, false);
        }

        var cachePath = GetCachePath(profileName);
        var remoteUrl = BuildProfileUrl(profileName);
        string? warningMessage = null;

        try
        {
            using var response = await _httpClient.GetAsync(remoteUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            if (!File.Exists(cachePath))
            {
                throw new InvalidOperationException($"GitHub 上の構成ファイルを取得できませんでした: {remoteUrl}");
            }

            warningMessage = $"GitHub から最新の構成ファイルを取得できなかったため、キャッシュを使用します。{Environment.NewLine}{ex.Message}";
        }

        var profile = _profileLoader.LoadFromFile(cachePath);
        return new ProfileCatalogEntry(profileName, profile.DisplayName, cachePath, true, warningMessage);
    }

    private static string BuildProfileUrl(string profileName)
    {
        var baseUrl = GetProfilesBaseSource();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultProfilesBaseUrl;
        }

        return $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(profileName)}.json";
    }

    private static string GetProfilesBaseSource()
    {
        var baseSource = Environment.GetEnvironmentVariable(ProfilesBaseUrlEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(baseSource))
        {
            return baseSource;
        }

        baseSource = Environment.GetEnvironmentVariable(LegacyProfilesBaseUrlEnvironmentVariableName);
        return string.IsNullOrWhiteSpace(baseSource) ? DefaultProfilesBaseUrl : baseSource;
    }

    private static string? TryResolveLocalOverridePath(string profileName)
    {
        var localProfilesDirectory = ResolveLocalProfilesDirectory();
        if (!string.IsNullOrWhiteSpace(localProfilesDirectory))
        {
            var localProfilePath = Path.Combine(localProfilesDirectory, $"{profileName}.json");
            if (File.Exists(localProfilePath))
            {
                return localProfilePath;
            }
        }

        var baseSource = GetProfilesBaseSource();
        if (string.IsNullOrWhiteSpace(baseSource))
        {
            return null;
        }

        if (Uri.TryCreate(baseSource, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            baseSource = uri.LocalPath;
        }

        if (!Path.IsPathRooted(baseSource))
        {
            return null;
        }

        if (Directory.Exists(baseSource))
        {
            var profilePath = Path.Combine(baseSource, $"{profileName}.json");
            return File.Exists(profilePath) ? profilePath : null;
        }

        if (File.Exists(baseSource) &&
            string.Equals(Path.GetFileNameWithoutExtension(baseSource), profileName, StringComparison.OrdinalIgnoreCase))
        {
            return baseSource;
        }

        return null;
    }

    private static string GetCachePath(string profileName)
    {
        return Path.Combine(GetCurrentCacheDirectoryPath(), $"{profileName}.json");
    }

    public CacheCleanupResult ClearLegacyProfileCache()
    {
        var deletedDirectories = new List<string>();
        var missingDirectories = new List<string>();

        DeleteCacheDirectory(GetVeryLegacyCacheDirectoryPath(), deletedDirectories, missingDirectories);
        DeleteCacheDirectory(GetLegacyCacheDirectoryPath(), deletedDirectories, missingDirectories);

        return new CacheCleanupResult(deletedDirectories, missingDirectories);
    }

    public static string GetCurrentCacheDirectoryPath()
    {
        var tempPath = Path.GetTempPath();
        if (string.IsNullOrWhiteSpace(tempPath))
        {
            throw new InvalidOperationException("TEMP フォルダが取得できません。");
        }

        return Path.Combine(tempPath, CacheRootDirectoryName, CacheSubDirectoryName);
    }

    public static string GetLegacyCacheDirectoryPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LocalApplicationData フォルダが取得できません。");
        }

        return Path.Combine(localAppData, CacheRootDirectoryName, CacheSubDirectoryName);
    }

    public static string GetVeryLegacyCacheDirectoryPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LocalApplicationData フォルダが取得できません。");
        }

        return Path.Combine(localAppData, LegacyCacheRootDirectoryName, CacheSubDirectoryName);
    }

    private static void DeleteCacheDirectory(
        string directoryPath,
        ICollection<string> deletedDirectories,
        ICollection<string> missingDirectories)
    {
        if (!Directory.Exists(directoryPath))
        {
            missingDirectories.Add(directoryPath);
            return;
        }

        Directory.Delete(directoryPath, true);
        deletedDirectories.Add(directoryPath);
    }

    private static string? ResolvePreferredProfileName(string? preferredProfileName)
    {
        if (!string.IsNullOrWhiteSpace(preferredProfileName))
        {
            return preferredProfileName;
        }

        var embeddedProfileName = GetEmbeddedProfileName();
        if (!string.IsNullOrWhiteSpace(embeddedProfileName))
        {
            return embeddedProfileName;
        }

        return GetProfileNameFromExecutableName();
    }

    private static string? GetEmbeddedProfileName()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static item => string.Equals(item.Key, "ModSynchronizerProfileName", StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(attribute?.Value) ? null : attribute.Value;
    }

    private static string? GetProfileNameFromExecutableName()
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

public sealed record ProfileCatalogEntry(
    string ProfileName,
    string DisplayName,
    string Path,
    bool IsRemote,
    string? WarningMessage = null);

public sealed record CacheCleanupResult(
    IReadOnlyList<string> DeletedDirectories,
    IReadOnlyList<string> MissingDirectories);
