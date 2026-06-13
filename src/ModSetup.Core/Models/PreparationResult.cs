namespace ModSetup.Core.Models;

public sealed class PreparationResult
{
    public bool RequiresManualLoaderInstall { get; set; }
    public string LoaderInstallerPath { get; set; } = "";
    public string LoaderInstallerUrl { get; set; } = "";
    public bool LoaderInstallAttempted { get; set; }
    public bool LoaderInstallSucceeded { get; set; }
}
