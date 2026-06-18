using ModSynchronizer.Core.Services;

namespace ModSynchronizer.App.Services;

internal sealed class AppRuntimeServices : IDisposable
{
    public AppRuntimeServices(
        HttpClient httpClient,
        ProfileCatalogService profileCatalogService,
        SetupRunner setupRunner,
        SelfUpdateService selfUpdateService)
    {
        HttpClient = httpClient;
        ProfileCatalogService = profileCatalogService;
        SetupRunner = setupRunner;
        SelfUpdateService = selfUpdateService;
    }

    public HttpClient HttpClient { get; }
    public ProfileCatalogService ProfileCatalogService { get; }
    public SetupRunner SetupRunner { get; }
    public SelfUpdateService SelfUpdateService { get; }

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}

internal static class AppRuntimeServicesFactory
{
    public static AppRuntimeServices Create(string? preferredProfileName = null)
    {
        var httpClient = new HttpClient();
        var pathResolver = new PathResolver();
        var javaRuntimeResolver = new JavaRuntimeResolver();
        var downloadService = new DownloadService(httpClient);
        var hashService = new HashService();
        var profileLoader = new ProfileLoader();
        var minecraftEnvironmentService = new MinecraftEnvironmentService(pathResolver);
        var loaderPreparationService = new LoaderPreparationService(downloadService, javaRuntimeResolver, hashService);
        var syncService = new SyncService(pathResolver, downloadService, hashService);
        var javaProxyInstallerService = new JavaProxyInstallerService(pathResolver, javaRuntimeResolver);

        return new AppRuntimeServices(
            httpClient,
            new ProfileCatalogService(httpClient, profileLoader, preferredProfileName),
            new SetupRunner(
                profileLoader,
                pathResolver,
                minecraftEnvironmentService,
                loaderPreparationService,
                syncService,
                new LauncherService(),
                javaProxyInstallerService),
            new SelfUpdateService(httpClient, downloadService, hashService));
    }
}
