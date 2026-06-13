namespace ModSetup.Core.Models;

public sealed class ModEntry
{
    public string Id { get; set; } = "";
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string BundlePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Required { get; set; } = true;
    public bool Deprecated { get; set; }
    public List<string> Tags { get; set; } = new();
}
