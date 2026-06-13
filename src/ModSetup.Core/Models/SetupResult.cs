namespace ModSetup.Core.Models;

public sealed class SetupResult
{
    public EnvironmentCheckResult Environment { get; set; } = new();
    public PreparationResult Preparation { get; set; } = new();
    public SyncItemResult Mods { get; } = new();
    public SyncItemResult Files { get; } = new();
    public List<string> Warnings { get; } = new();
}
