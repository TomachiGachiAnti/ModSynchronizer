using System.Diagnostics;
using System.Text.Json;
using ModSynchronizer.Core.Models;

namespace ModSynchronizer.Core.Services;

public sealed class JavaRuntimeResolver
{
    public string ResolveJavaExecutable()
    {
        return ResolveJavaRuntime().JavaExecutablePath;
    }

    public JavaRuntimeInfo ResolveJavaRuntime()
    {
        var candidates = GetBundledJavaCandidates().ToList();
        var javaExecutablePath = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(javaExecutablePath))
        {
            return BuildRuntimeInfo(javaExecutablePath);
        }

        var fromPath = TryResolveFromPath();
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return BuildRuntimeInfo(fromPath);
        }

        throw new InvalidOperationException("Java 実行環境が見つかりませんでした。");
    }

    private static string? TryResolveFromPath()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "java",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetBundledJavaCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            yield break;
        }

        foreach (var runtimeRoot in GetRuntimeRoots(localAppData))
        {
            foreach (var path in BuildRuntimeCandidates(runtimeRoot))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> GetRuntimeRoots(string localAppData)
    {
        yield return Path.Combine(
            localAppData,
            "Packages",
            "Microsoft.4297127D64EC6_8wekyb3d8bbwe",
            "LocalCache",
            "Local",
            "runtime");

        yield return Path.Combine(localAppData, "MinecraftInstaller", "runtime");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Minecraft Launcher", "runtime");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Minecraft Launcher", "runtime");

        var productLibraryDir = TryReadProductLibraryDir();
        if (!string.IsNullOrWhiteSpace(productLibraryDir))
        {
            yield return Path.Combine(productLibraryDir, "runtime");
            yield return productLibraryDir;
        }
    }

    private static string? TryReadProductLibraryDir()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                return null;
            }

            var launcherSettingsPath = Path.Combine(appData, ".minecraft", "launcher_settings.json");
            if (!File.Exists(launcherSettingsPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(launcherSettingsPath));
            if (!document.RootElement.TryGetProperty("productLibraryDir", out var property))
            {
                return null;
            }

            return property.GetString();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> BuildRuntimeCandidates(string runtimeRoot)
    {
        yield return Path.Combine(runtimeRoot, "java-runtime-delta", "windows-x64", "java-runtime-delta", "bin", "java.exe");
        yield return Path.Combine(runtimeRoot, "java-runtime-delta", "windows-x64", "java-runtime-delta", "bin", "javaw.exe");
        yield return Path.Combine(runtimeRoot, "jre-legacy", "windows-x64", "jre-legacy", "bin", "java.exe");
        yield return Path.Combine(runtimeRoot, "jre-legacy", "windows-x64", "jre-legacy", "bin", "javaw.exe");
    }

    private static JavaRuntimeInfo BuildRuntimeInfo(string javaExecutablePath)
    {
        var javaDirectory = Path.GetDirectoryName(javaExecutablePath);
        if (string.IsNullOrWhiteSpace(javaDirectory))
        {
            throw new InvalidOperationException("Java 実行環境のフォルダが取得できませんでした。");
        }

        var javaFileName = Path.GetFileName(javaExecutablePath);
        string javaPath;
        string javawPath;
        if (string.Equals(javaFileName, "javaw.exe", StringComparison.OrdinalIgnoreCase))
        {
            javawPath = javaExecutablePath;
            javaPath = Path.Combine(javaDirectory, "java.exe");
        }
        else
        {
            javaPath = javaExecutablePath;
            javawPath = Path.Combine(javaDirectory, "javaw.exe");
        }

        if (!File.Exists(javaPath))
        {
            throw new InvalidOperationException("java.exe が見つかりませんでした。");
        }

        if (!File.Exists(javawPath))
        {
            throw new InvalidOperationException("javaw.exe が見つかりませんでした。");
        }

        return new JavaRuntimeInfo
        {
            JavaExecutablePath = javaPath,
            JavaWindowExecutablePath = javawPath
        };
    }
}
