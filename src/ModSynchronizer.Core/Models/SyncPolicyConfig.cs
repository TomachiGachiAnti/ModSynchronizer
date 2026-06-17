namespace ModSynchronizer.Core.Models;

public sealed class SyncPolicyConfig
{
    public bool RemoveManagedFilesNotInManifest { get; set; }
    public bool RemoveDeprecatedFiles { get; set; }
}
