using ModSynchronizer.Core.Models;

namespace ModSynchronizer.Core.Services;

public sealed class SetupRunner
{
    private readonly ProfileLoader _profileLoader;
    private readonly PathResolver _pathResolver;
    private readonly MinecraftEnvironmentService _minecraftEnvironmentService;
    private readonly LoaderPreparationService _loaderPreparationService;
    private readonly SyncService _syncService;
    private readonly LauncherService _launcherService;
    private readonly JavaProxyInstallerService _javaProxyInstallerService;

    public SetupRunner(
        ProfileLoader profileLoader,
        PathResolver pathResolver,
        MinecraftEnvironmentService minecraftEnvironmentService,
        LoaderPreparationService loaderPreparationService,
        SyncService syncService,
        LauncherService launcherService,
        JavaProxyInstallerService javaProxyInstallerService)
    {
        _profileLoader = profileLoader;
        _pathResolver = pathResolver;
        _minecraftEnvironmentService = minecraftEnvironmentService;
        _loaderPreparationService = loaderPreparationService;
        _syncService = syncService;
        _launcherService = launcherService;
        _javaProxyInstallerService = javaProxyInstallerService;
    }

    public ProfileConfig LoadProfile(string profilePath)
    {
        return _profileLoader.LoadFromFile(profilePath);
    }

    public async Task<SetupResult> RunAsync(
        string profilePath,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken,
        SetupRunOptions? options = null)
    {
        options ??= new SetupRunOptions();
        var profile = _profileLoader.LoadFromFile(profilePath);
        var result = new SetupResult
        {
            Environment = _minecraftEnvironmentService.Check(profile)
        };

        if (!result.Environment.MinecraftInstalled)
        {
            throw new InvalidOperationException("Minecraft のインストールが見つかりませんでした。先に公式ランチャーで Minecraft を起動してください。");
        }

        result.Preparation = await _loaderPreparationService.PrepareLoaderAsync(profile, result.Environment, cancellationToken);
        result.Environment = _minecraftEnvironmentService.Check(profile);

        if (!result.Environment.LoaderInstalled)
        {
            if (!string.IsNullOrWhiteSpace(result.Preparation.LoaderInstallerPath))
            {
                throw new InvalidOperationException($"対象ローダーが未導入です。インストーラーを取得しましたが導入は完了していません: {result.Preparation.LoaderInstallerPath}");
            }

            throw new InvalidOperationException("対象ローダーが未導入です。ローダー導入処理がまだ完了していません。");
        }

        var syncResult = await _syncService.SyncAsync(profile, progress, cancellationToken);
        result.Mods.Downloaded.AddRange(syncResult.Mods.Downloaded);
        result.Mods.Updated.AddRange(syncResult.Mods.Updated);
        result.Mods.Deleted.AddRange(syncResult.Mods.Deleted);
        result.Mods.Skipped.AddRange(syncResult.Mods.Skipped);
        result.Mods.Failed.AddRange(syncResult.Mods.Failed);
        result.Files.Downloaded.AddRange(syncResult.Files.Downloaded);
        result.Files.Updated.AddRange(syncResult.Files.Updated);
        result.Files.Deleted.AddRange(syncResult.Files.Deleted);
        result.Files.Skipped.AddRange(syncResult.Files.Skipped);
        result.Files.Failed.AddRange(syncResult.Files.Failed);
        result.Warnings.AddRange(syncResult.Warnings);
        var gameDirectory = _pathResolver.GetGameDirectory(profile);

        string? javaProxyDirectory = null;
        if (options.EnsureLauncherProfile)
        {
            javaProxyDirectory = _javaProxyInstallerService.EnsureProxy(profile);
            _launcherService.EnsureProfile(profile, gameDirectory, javaProxyDirectory);
        }

        if (options.LaunchOfficialLauncher)
        {
            result.OfficialLauncherLaunchSucceeded = _launcherService.TryLaunchOfficialLauncher();
        }

        return result;
    }
}
