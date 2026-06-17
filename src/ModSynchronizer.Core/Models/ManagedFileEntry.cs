namespace ModSynchronizer.Core.Models;

public sealed class ManagedFileEntry
{
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string BundlePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Required { get; set; } = true;
    public string Category { get; set; } = "other";
}
