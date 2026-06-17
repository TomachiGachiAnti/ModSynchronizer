using ModSynchronizer.Core.Models;

namespace ModSynchronizer.Core.Services;

public sealed class MinecraftEnvironmentService
{
    private readonly PathResolver _pathResolver;

    public MinecraftEnvironmentService(PathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public EnvironmentCheckResult Check(ProfileConfig profile)
    {
        var minecraftRoot = _pathResolver.GetMinecraftRoot();
        var result = new EnvironmentCheckResult
        {
            MinecraftRoot = minecraftRoot,
            RequiredVersionDirectory = Path.Combine(minecraftRoot, "versions", profile.MinecraftVersion),
            RequiredLoaderVersionId = ResolveLoaderVersionId(profile)
        };

        result.MinecraftInstalled = Directory.Exists(minecraftRoot);
        result.MinecraftVersionInstalled = Directory.Exists(result.RequiredVersionDirectory);

        if (!result.MinecraftInstalled)
        {
            return result;
        }

        var versionsRoot = Path.Combine(minecraftRoot, "versions");
        var requiredLoaderDirectory = Path.Combine(versionsRoot, result.RequiredLoaderVersionId);
        result.LoaderInstalled = Directory.Exists(requiredLoaderDirectory);

        return result;
    }

    private static string ResolveLoaderVersionId(ProfileConfig profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Loader.VersionId))
        {
            return profile.Loader.VersionId;
        }

        if (!string.IsNullOrWhiteSpace(profile.Launcher.VersionId))
        {
            return profile.Launcher.VersionId;
        }

        throw new InvalidOperationException("loader.version_id または launcher.version_id が未設定です。");
    }
}
