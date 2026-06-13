namespace ModSetup.Core.Models;

public sealed class SyncItemResult
{
    public List<string> Downloaded { get; } = new();
    public List<string> Updated { get; } = new();
    public List<string> Deleted { get; } = new();
    public List<string> Skipped { get; } = new();
    public List<string> Failed { get; } = new();
}
