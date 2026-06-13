namespace ModSetup.Core.Models;

public sealed class ServerSetupConfig
{
    public string ServerJarUrl { get; set; } = "";
    public string ServerJarSha1 { get; set; } = "";
    public string ConfigBundlePath { get; set; } = "";
}
