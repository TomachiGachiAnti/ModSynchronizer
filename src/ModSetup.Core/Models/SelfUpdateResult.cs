namespace ModSetup.Core.Models;

public sealed class SelfUpdateResult
{
    public bool Checked { get; set; }
    public bool UpdateAvailable { get; set; }
    public bool UpdateScheduled { get; set; }
    public string CurrentVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string DownloadPath { get; set; } = "";
}
