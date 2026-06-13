namespace ModSetup.Core.Models;

public sealed class SetupProgress
{
    public string Message { get; init; } = "";
    public int Current { get; init; }
    public int Total { get; init; }
}
