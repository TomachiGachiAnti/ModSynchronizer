namespace ModSynchronizer.Core.Models;

public sealed class SelfUpdateConfig
{
    public bool Enabled { get; set; }
    public string ManifestUrl { get; set; } = "";
    public string GithubReleasesApiUrl { get; set; } = "";
    public string AssetName { get; set; } = "";
}
