namespace ModSynchronizer.Core.Models;

public sealed class SetupResult
{
    public EnvironmentCheckResult Environment { get; set; } = new();
    public PreparationResult Preparation { get; set; } = new();
    public SyncItemResult Mods { get; } = new();
    public SyncItemResult Files { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool OfficialLauncherLaunchSucceeded { get; set; }
    public bool HasSyncFailures => Mods.Failed.Count > 0 || Files.Failed.Count > 0;
}
