namespace ModSetup.Core.Models;

public sealed class LoaderConfig
{
    public string Type { get; set; } = "";
    public string Version { get; set; } = "";
    public string InstallerUrl { get; set; } = "";
    public string VersionId { get; set; } = "";
}
