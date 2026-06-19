using ModSynchronizer.Core.Models;

namespace ModSynchronizer.Core.Services;

public sealed class PathResolver
{
    private const string ModdedMinecraftRootDirectoryName = ".modded-minecraft";

    public string GetAppBaseDirectory()
    {
        return AppContext.BaseDirectory;
    }

    public string GetMinecraftRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("AppData フォルダが取得できません。");
        }

        return Path.Combine(appData, ".minecraft");
    }

    public string GetGameDirectory(ProfileConfig profile)
    {
        return Path.Combine(GetModdedMinecraftRoot(), profile.GameDirectory.Name);
    }

    public string GetModsDirectory(ProfileConfig profile)
    {
        return Path.Combine(GetGameDirectory(profile), "mods");
    }

    public string GetManagedFilePath(ProfileConfig profile, string relativePath)
    {
        return Path.Combine(GetGameDirectory(profile), relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public string GetJavaProxyProfileDirectory(ProfileConfig profile)
    {
        return Path.Combine(GetModdedMinecraftRoot(), "java-proxy", profile.ConfigId);
    }

    public string GetJavaProxyBinDirectory(ProfileConfig profile)
    {
        return Path.Combine(GetJavaProxyProfileDirectory(profile), "bin");
    }

    public string GetToolsRootDirectory()
    {
        return Path.Combine(GetModdedMinecraftRoot(), "tools");
    }

    public string GetInstalledRuntimeDirectory()
    {
        return Path.Combine(GetToolsRootDirectory(), "ModSynchronizer");
    }

    public string GetInstalledRuntimeExecutablePath()
    {
        return Path.Combine(GetInstalledRuntimeDirectory(), "ModSynchronizer.exe");
    }

    public string GetModdedMinecraftRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("AppData フォルダが取得できません。");
        }

        return Path.Combine(appData, ModdedMinecraftRootDirectoryName);
    }

    public string ResolveBundlePath(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            return "";
        }

        var appRelativePath = Path.GetFullPath(Path.Combine(GetAppBaseDirectory(), bundlePath));
        if (File.Exists(appRelativePath) || Directory.Exists(appRelativePath))
        {
            return appRelativePath;
        }

        var bundleRoot = Environment.GetEnvironmentVariable("MODSETUP_BUNDLE_ROOT");
        if (!string.IsNullOrWhiteSpace(bundleRoot))
        {
            return Path.GetFullPath(Path.Combine(bundleRoot, bundlePath));
        }

        return appRelativePath;
    }
}
