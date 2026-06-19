using ModSynchronizer.Core.Models;
using System.Text.Json;

namespace ModSynchronizer.Core.Services;

public sealed class JavaProxyInstallerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly PathResolver _pathResolver;
    private readonly JavaRuntimeResolver _javaRuntimeResolver;

    public JavaProxyInstallerService(PathResolver pathResolver, JavaRuntimeResolver javaRuntimeResolver)
    {
        _pathResolver = pathResolver;
        _javaRuntimeResolver = javaRuntimeResolver;
    }

    public string EnsureProxy(ProfileConfig profile, string modSynchronizerPath)
    {
        if (string.IsNullOrWhiteSpace(modSynchronizerPath) || !File.Exists(modSynchronizerPath))
        {
            throw new InvalidOperationException("常設 ModSynchronizer ランタイムが見つかりません。");
        }

        var runtimeInfo = _javaRuntimeResolver.ResolveJavaRuntime();
        var proxyRootDirectory = _pathResolver.GetJavaProxyProfileDirectory(profile);
        var proxyBinDirectory = _pathResolver.GetJavaProxyBinDirectory(profile);
        Directory.CreateDirectory(proxyBinDirectory);

        var javawProxyPath = Path.Combine(proxyBinDirectory, "javaw.exe");
        var javaProxyPath = Path.Combine(proxyBinDirectory, "java.exe");

        File.Copy(modSynchronizerPath, javawProxyPath, true);
        File.Copy(modSynchronizerPath, javaProxyPath, true);

        var config = new JavaProxyConfig
        {
            ProfileName = profile.ConfigId,
            ModSynchronizerPath = modSynchronizerPath,
            RealJavaPath = runtimeInfo.JavaExecutablePath,
            RealJavawPath = runtimeInfo.JavaWindowExecutablePath
        };

        var configPath = Path.Combine(proxyRootDirectory, "proxy-config.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOptions));

        return javawProxyPath;
    }
}

public sealed class JavaProxyConfig
{
    public string ProfileName { get; set; } = "";
    public string ModSynchronizerPath { get; set; } = "";
    public string RealJavaPath { get; set; } = "";
    public string RealJavawPath { get; set; } = "";
}
