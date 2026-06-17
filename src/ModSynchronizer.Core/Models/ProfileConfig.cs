namespace ModSynchronizer.Core.Models;

public sealed class ProfileConfig
{
    public int FormatVersion { get; set; }
    public string ConfigId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string MinecraftVersion { get; set; } = "";
    public SelfUpdateConfig SelfUpdate { get; set; } = new();
    public LoaderConfig Loader { get; set; } = new();
    public GameDirectoryConfig GameDirectory { get; set; } = new();
    public LauncherConfig Launcher { get; set; } = new();
    public SyncPolicyConfig Sync { get; set; } = new();
    public ServerSetupConfig ServerSetup { get; set; } = new();
    public List<ModEntry> Mods { get; set; } = new();
    public List<ManagedFileEntry> Files { get; set; } = new();
    public List<ManagedDirectoryEntry> Directories { get; set; } = new();
    public List<ServerEntry> Servers { get; set; } = new();
}
