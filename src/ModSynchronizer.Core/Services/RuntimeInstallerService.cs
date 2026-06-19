namespace ModSynchronizer.Core.Services;

public sealed class RuntimeInstallerService
{
    private readonly PathResolver _pathResolver;

    public RuntimeInstallerService(PathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public string EnsureInstalledRuntime()
    {
        var currentExecutablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutablePath) || !File.Exists(currentExecutablePath))
        {
            throw new InvalidOperationException("ModSynchronizer の実行ファイルパスが取得できません。");
        }

        var currentBaseDirectory = AppContext.BaseDirectory;
        var runtimeDirectory = _pathResolver.GetInstalledRuntimeDirectory();
        Directory.CreateDirectory(runtimeDirectory);

        var runtimeExecutablePath = _pathResolver.GetInstalledRuntimeExecutablePath();
        File.Copy(currentExecutablePath, runtimeExecutablePath, true);

        CopyOptionalDirectory(currentBaseDirectory, runtimeDirectory, "profiles");
        CopyOptionalDirectory(currentBaseDirectory, runtimeDirectory, "assets");

        return runtimeExecutablePath;
    }

    private static void CopyOptionalDirectory(string sourceRoot, string destinationRoot, string directoryName)
    {
        var sourceDirectory = Path.Combine(sourceRoot, directoryName);
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        var destinationDirectory = Path.Combine(destinationRoot, directoryName);
        CopyDirectory(sourceDirectory, destinationDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourceFilePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);
            var destinationFileDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationFileDirectory))
            {
                Directory.CreateDirectory(destinationFileDirectory);
            }

            File.Copy(sourceFilePath, destinationFilePath, true);
        }
    }
}
