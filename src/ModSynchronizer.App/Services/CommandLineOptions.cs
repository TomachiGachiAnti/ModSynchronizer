namespace ModSynchronizer.App.Services;

internal sealed class CommandLineOptions
{
    public string? Mode { get; private init; }
    public string? ProfileName { get; private init; }

    public bool IsGuiMode => string.IsNullOrWhiteSpace(Mode);

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        string? mode = null;
        string? profileName = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (TryReadOption(argument, "--mode", out var inlineMode))
            {
                mode = ReadOptionValue(args, ref index, inlineMode, "--mode");
                continue;
            }

            if (TryReadOption(argument, "--profile", out var inlineProfileName))
            {
                profileName = ReadOptionValue(args, ref index, inlineProfileName, "--profile");
                continue;
            }

            throw new InvalidOperationException($"未対応の引数です: {argument}");
        }

        return new CommandLineOptions
        {
            Mode = mode,
            ProfileName = profileName
        };
    }

    private static bool TryReadOption(string argument, string optionName, out string? inlineValue)
    {
        inlineValue = null;
        if (string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = optionName + "=";
        if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        inlineValue = argument[prefix.Length..];
        return true;
    }

    private static string ReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string? inlineValue,
        string optionName)
    {
        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            return inlineValue;
        }

        if (index + 1 >= args.Count)
        {
            throw new InvalidOperationException($"{optionName} の値が不足しています。");
        }

        index++;
        var value = args[index];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{optionName} の値が不足しています。");
        }

        return value;
    }
}
