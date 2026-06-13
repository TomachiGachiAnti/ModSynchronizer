using ModSetup.Core.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModSetup.Core.Services;

public sealed class LauncherService
{
    private const string StoreLauncherAppId = "Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void EnsureProfile(ProfileConfig profile, string gameDirectory)
    {
        if (!profile.Launcher.CreateProfile)
        {
            return;
        }

        var launcherProfilesPath = GetLauncherProfilesPath();
        if (!File.Exists(launcherProfilesPath))
        {
            throw new FileNotFoundException("launcher_profiles.json が見つかりません。", launcherProfilesPath);
        }

        var rootNode = JsonNode.Parse(File.ReadAllText(launcherProfilesPath))?.AsObject();
        if (rootNode is null)
        {
            throw new InvalidOperationException("launcher_profiles.json を読み込めませんでした。");
        }

        var profilesNode = rootNode["profiles"] as JsonObject;
        if (profilesNode is null)
        {
            profilesNode = new JsonObject();
            rootNode["profiles"] = profilesNode;
        }

        var profileId = string.IsNullOrWhiteSpace(profile.Launcher.ProfileId)
            ? profile.ConfigId
            : profile.Launcher.ProfileId;
        var now = DateTimeOffset.UtcNow.ToString("O");

        var launcherProfile = profilesNode[profileId] as JsonObject ?? new JsonObject();
        launcherProfile["created"] ??= now;
        launcherProfile["icon"] = string.IsNullOrWhiteSpace(profile.Launcher.Icon) ? "Furnace" : profile.Launcher.Icon;
        launcherProfile["lastUsed"] = now;
        launcherProfile["lastVersionId"] = ResolveVersionId(profile);
        launcherProfile["name"] = profile.Launcher.ProfileName;
        launcherProfile["type"] = "custom";
        launcherProfile["gameDir"] = gameDirectory;

        if (!string.IsNullOrWhiteSpace(profile.Launcher.JavaArgs))
        {
            launcherProfile["javaArgs"] = profile.Launcher.JavaArgs;
        }

        if (!string.IsNullOrWhiteSpace(profile.Launcher.JavaDir))
        {
            launcherProfile["javaDir"] = profile.Launcher.JavaDir;
        }

        profilesNode[profileId] = launcherProfile;
        File.WriteAllText(launcherProfilesPath, rootNode.ToJsonString(JsonOptions));
    }

    public bool CanLaunchOfficialLauncher(out string launcherPath)
    {
        foreach (var candidate in GetLauncherPathCandidates())
        {
            if (File.Exists(candidate))
            {
                launcherPath = candidate;
                return true;
            }
        }

        launcherPath = StoreLauncherAppId;
        return true;
    }

    public void LaunchOfficialLauncher()
    {
        if (!CanLaunchOfficialLauncher(out var launcherPath))
        {
            throw new FileNotFoundException("公式 Minecraft ランチャーが見つかりません。", launcherPath);
        }

        ProcessStartInfo startInfo;
        if (string.Equals(launcherPath, StoreLauncherAppId, StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{StoreLauncherAppId}",
                UseShellExecute = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                UseShellExecute = true
            };
        }

        Process.Start(startInfo);
    }

    private static IEnumerable<string> GetLauncherPathCandidates()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Minecraft Launcher",
            "MinecraftLauncher.exe");

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Minecraft Launcher",
            "MinecraftLauncher.exe");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            yield break;
        }

        yield return Path.Combine(localAppData, "Programs", "Minecraft Launcher", "MinecraftLauncher.exe");
    }

    public string GetLauncherProfilesPath()
    {
        var minecraftRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft");
        return Path.Combine(minecraftRoot, "launcher_profiles.json");
    }

    private static string ResolveVersionId(ProfileConfig profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Launcher.VersionId))
        {
            return profile.Launcher.VersionId;
        }

        throw new InvalidOperationException("launcher.version_id が未設定です。");
    }
}
