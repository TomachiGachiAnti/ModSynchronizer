using System.Diagnostics;
using System.Text.Json;
using ModSynchronizer.App.Forms;
using ModSynchronizer.Core.Services;

namespace ModSynchronizer.App.Services;

internal sealed class JavaProxyRuntimeService
{
    private const int SelfUpdateScheduledExitCode = 20;

    public bool IsProxyInvocation()
    {
        var configPath = TryGetProxyConfigPath();
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return false;
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        var executableName = Path.GetFileName(processPath);
        return string.Equals(executableName, "javaw.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executableName, "java.exe", StringComparison.OrdinalIgnoreCase);
    }

    public int Run(IReadOnlyList<string> args)
    {
        try
        {
            var config = LoadConfig();
            WriteLog(config.ProfileName, "proxy start");
            WriteLog(config.ProfileName, $"current_dir={Environment.CurrentDirectory}");
            WriteLog(config.ProfileName, $"args={string.Join(" | ", args)}");

            using var mutex = new Mutex(false, BuildMutexName(config.ProfileName));
            mutex.WaitOne();
            WriteLog(config.ProfileName, "sync lock acquired");

            try
            {
                var syncExitCode = RunSynchronizerWithRetry(config);
                WriteLog(config.ProfileName, $"sync exit={syncExitCode}");
                if (syncExitCode != 0)
                {
                    return syncExitCode;
                }
            }
            finally
            {
                mutex.ReleaseMutex();
                WriteLog(config.ProfileName, "sync lock released");
            }

            var exitCode = RunRealJava(config, args);
            WriteLog(config.ProfileName, $"real java exit={exitCode}");
            return exitCode;
        }
        catch (Exception ex)
        {
            WriteLog(null, $"proxy exception={ex}");
            return 1;
        }
    }

    private static string BuildMutexName(string profileName)
    {
        return $"Global\\ModSynchronizer.Sync.{profileName.Replace('\\', '_').Replace('/', '_').Replace(':', '_')}";
    }

    private static string? TryGetProxyConfigPath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var processDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(processDirectory))
        {
            return null;
        }

        var parentDirectory = Directory.GetParent(processDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return null;
        }

        return Path.Combine(parentDirectory, "proxy-config.json");
    }

    private static JavaProxyConfig LoadConfig()
    {
        var configPath = TryGetProxyConfigPath();
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            throw new InvalidOperationException("proxy-config.json が見つかりません。");
        }

        var config = JsonSerializer.Deserialize<JavaProxyConfig>(File.ReadAllText(configPath));
        if (config is null
            || string.IsNullOrWhiteSpace(config.ProfileName)
            || string.IsNullOrWhiteSpace(config.ModSynchronizerPath)
            || string.IsNullOrWhiteSpace(config.RealJavaPath)
            || string.IsNullOrWhiteSpace(config.RealJavawPath))
        {
            throw new InvalidOperationException("proxy-config.json の内容が不正です。");
        }

        return config;
    }

    private static int RunSynchronizer(JavaProxyConfig config)
    {
        if (!File.Exists(config.ModSynchronizerPath))
        {
            WriteLog(config.ProfileName, $"mod synchronizer not found={config.ModSynchronizerPath}");
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = config.ModSynchronizerPath,
            Arguments = $"--mode sync-only --profile \"{config.ProfileName}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            WriteLog(config.ProfileName, "failed to start mod synchronizer");
            return 1;
        }

        ProxySyncProgressForm? progressForm = null;
        DataReceivedEventHandler? outputHandler = null;
        DataReceivedEventHandler? errorHandler = null;

        if (ShouldShowProgressWindow())
        {
            progressForm = new ProxySyncProgressForm(process, config.ProfileName);
            outputHandler = (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    HandleSynchronizerOutput(config.ProfileName, eventArgs.Data, progressForm);
                }
            };
            errorHandler = (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    WriteLog(config.ProfileName, $"sync stderr={eventArgs.Data}");
                }
            };
            process.OutputDataReceived += outputHandler;
            process.ErrorDataReceived += errorHandler;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (progressForm is not null)
        {
            using (progressForm)
            {
                Application.Run(progressForm);
            }
        }
        else
        {
            process.WaitForExit();
        }

        if (outputHandler is not null)
        {
            process.OutputDataReceived -= outputHandler;
        }

        if (errorHandler is not null)
        {
            process.ErrorDataReceived -= errorHandler;
        }

        return process.ExitCode;
    }

    private static int RunSynchronizerWithRetry(JavaProxyConfig config)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exitCode = RunSynchronizer(config);
            if (exitCode != SelfUpdateScheduledExitCode)
            {
                return exitCode;
            }

            WriteLog(config.ProfileName, "self update scheduled; waiting for runtime replacement");
            WaitForRuntimeReplacement(config.ModSynchronizerPath);
        }

        return 1;
    }

    private static int RunRealJava(JavaProxyConfig config, IReadOnlyList<string> args)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return 1;
        }

        var executableName = Path.GetFileName(processPath);
        var realJavaPath = string.Equals(executableName, "javaw.exe", StringComparison.OrdinalIgnoreCase)
            ? config.RealJavawPath
            : config.RealJavaPath;
        WriteLog(config.ProfileName, $"selected real java={realJavaPath}");
        if (!File.Exists(realJavaPath))
        {
            WriteLog(config.ProfileName, $"real java not found={realJavaPath}");
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = realJavaPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            WriteLog(config.ProfileName, "failed to start real java");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static void WriteLog(string? profileName, string message)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                return;
            }

            var safeProfileName = string.IsNullOrWhiteSpace(profileName) ? "unknown" : profileName;
            var logDirectory = Path.Combine(appData, ".modded-minecraft", "java-proxy", safeProfileName);
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "proxy.log");
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            File.AppendAllLines(logPath, [line]);
        }
        catch
        {
        }
    }

    private static void WaitForRuntimeReplacement(string runtimePath)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (!File.Exists(runtimePath))
            {
                Thread.Sleep(250);
                continue;
            }

            try
            {
                using var stream = File.Open(runtimePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length > 0)
                {
                    Thread.Sleep(500);
                    return;
                }
            }
            catch
            {
            }

            Thread.Sleep(250);
        }
    }

    private static void HandleSynchronizerOutput(string profileName, string line, ProxySyncProgressForm progressForm)
    {
        WriteLog(profileName, $"sync stdout={line}");

        const string prefix = "PROGRESS\t";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var parts = line.Split('\t');
        if (parts.Length < 4)
        {
            return;
        }

        if (!int.TryParse(parts[1], out var current))
        {
            current = 0;
        }

        if (!int.TryParse(parts[2], out var total))
        {
            total = 0;
        }

        progressForm.UpdateProgress(parts[3], current, total);
    }

    private static bool ShouldShowProgressWindow()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        var executableName = Path.GetFileName(processPath);
        return string.Equals(executableName, "javaw.exe", StringComparison.OrdinalIgnoreCase);
    }
}
