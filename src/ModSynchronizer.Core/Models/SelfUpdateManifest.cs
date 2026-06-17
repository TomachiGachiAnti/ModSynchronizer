namespace ModSynchronizer.Core.Models;

public sealed class SelfUpdateManifest
{
    public string Version { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
