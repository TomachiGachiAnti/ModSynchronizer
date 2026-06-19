using ModSynchronizer.App.Forms;
using ModSynchronizer.App.Services;
using ModSynchronizer.Core.Models;
using System.Text;

namespace ModSynchronizer.App;

internal static class Program
{
    private const int SelfUpdateScheduledExitCode = 20;

    [STAThread]
    private static int Main(string[] args)
    {
        var javaProxyRuntimeService = new JavaProxyRuntimeService();
        if (javaProxyRuntimeService.IsProxyInvocation())
        {
            ApplicationConfiguration.Initialize();
            return javaProxyRuntimeService.Run(args);
        }

        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch
        {
            return 1;
        }

        if (options.IsGuiMode)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }

        return RunCommandLineModeAsync(options).GetAwaiter().GetResult();
    }

    private static async Task<int> RunCommandLineModeAsync(CommandLineOptions options)
    {
        if (!string.Equals(options.Mode, "sync-only", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Mode, "setup-and-launch", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        using var runtimeServices = AppRuntimeServicesFactory.Create(options.ProfileName);

        string profileName;
        try
        {
            profileName = options.ProfileName ?? runtimeServices.ProfileCatalogService.GetRequiredProfileName();
        }
        catch
        {
            return 2;
        }

        ProfileCatalogEntry profileEntry;
        try
        {
            profileEntry = await runtimeServices.ProfileCatalogService.LoadProfileByNameAsync(profileName, CancellationToken.None);
        }
        catch
        {
            return 2;
        }

        SetupResult result;
        try
        {
            var profile = runtimeServices.SetupRunner.LoadProfile(profileEntry.Path);
            if (string.Equals(options.Mode, "sync-only", StringComparison.OrdinalIgnoreCase))
            {
                var selfUpdateResult = await runtimeServices.SelfUpdateService.CheckAndApplyAsync(
                    profile,
                    CancellationToken.None,
                    relaunchAfterUpdate: false);
                if (selfUpdateResult.UpdateScheduled)
                {
                    return SelfUpdateScheduledExitCode;
                }
            }

            var progress = new Progress<SetupProgress>(WriteProgressToConsole);
            result = await runtimeServices.SetupRunner.RunAsync(
                profileEntry.Path,
                progress: progress,
                cancellationToken: CancellationToken.None,
                options: new SetupRunOptions
                {
                    EnsureLauncherProfile = !string.Equals(options.Mode, "sync-only", StringComparison.OrdinalIgnoreCase),
                    LaunchOfficialLauncher = string.Equals(options.Mode, "setup-and-launch", StringComparison.OrdinalIgnoreCase)
                });
        }
        catch
        {
            return 1;
        }

        WriteSyncFailuresToError(result);
        return result.HasSyncFailures ? 5 : 0;
    }

    private static void WriteSyncFailuresToError(SetupResult result)
    {
        foreach (var mod in result.Mods.Failed)
        {
            Console.Error.WriteLine($"FAILED MOD\t{mod}");
        }

        foreach (var file in result.Files.Failed)
        {
            Console.Error.WriteLine($"FAILED FILE\t{file}");
        }

        Console.Error.Flush();
    }

    private static void WriteProgressToConsole(SetupProgress progress)
    {
        var sanitizedMessage = SanitizeProgressMessage(progress.Message);
        Console.Out.WriteLine($"PROGRESS\t{progress.Current}\t{progress.Total}\t{sanitizedMessage}");
        Console.Out.Flush();
    }

    private static string SanitizeProgressMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "同期しています";
        }

        var builder = new StringBuilder(message.Length);
        foreach (var character in message)
        {
            if (character == '\r' || character == '\n' || character == '\t')
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
