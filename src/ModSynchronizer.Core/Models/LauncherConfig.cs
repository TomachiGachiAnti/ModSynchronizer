namespace ModSynchronizer.Core.Models;

public sealed class LauncherConfig
{
    public bool CreateProfile { get; set; }
    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string Icon { get; set; } = "Furnace";
    public string JavaArgs { get; set; } = "";
    public string JavaDir { get; set; } = "";
}
