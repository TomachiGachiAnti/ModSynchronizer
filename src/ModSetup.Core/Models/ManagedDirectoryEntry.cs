namespace ModSetup.Core.Models;

public sealed class ManagedDirectoryEntry
{
    public string Path { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string BundlePath { get; set; } = "";
    public bool Required { get; set; } = true;
    public string Category { get; set; } = "other";
}
