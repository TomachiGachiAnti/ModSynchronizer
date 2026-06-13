namespace ModSetup.Core.Models;

public sealed class ServerEntry
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool AcceptTextures { get; set; } = true;
}
