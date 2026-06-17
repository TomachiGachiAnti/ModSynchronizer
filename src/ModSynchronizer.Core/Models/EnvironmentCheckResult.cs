namespace ModSynchronizer.Core.Models;

public sealed class EnvironmentCheckResult
{
    public bool MinecraftInstalled { get; set; }
    public bool MinecraftVersionInstalled { get; set; }
    public bool LoaderInstalled { get; set; }
    public string MinecraftRoot { get; set; } = "";
    public string RequiredVersionDirectory { get; set; } = "";
    public string RequiredLoaderVersionId { get; set; } = "";
}
