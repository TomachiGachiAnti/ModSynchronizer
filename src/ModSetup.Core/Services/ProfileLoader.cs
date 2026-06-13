using System.Text.Json;
using ModSetup.Core.Models;

namespace ModSetup.Core.Services;

public sealed class ProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ProfileConfig LoadFromFile(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("構成ファイルのパスが指定されていません。", nameof(profilePath));
        }

        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException("構成ファイルが見つかりません。", profilePath);
        }

        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<ProfileConfig>(json, JsonOptions);

        if (profile is null)
        {
            throw new InvalidOperationException("構成ファイルを読み込めませんでした。");
        }

        Validate(profile);
        return profile;
    }

    public void Validate(ProfileConfig profile)
    {
        if (profile.FormatVersion <= 0)
        {
            throw new InvalidOperationException("format_version が不正です。");
        }

        if (string.IsNullOrWhiteSpace(profile.ConfigId))
        {
            throw new InvalidOperationException("config_id が未設定です。");
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            throw new InvalidOperationException("display_name が未設定です。");
        }

        if (string.IsNullOrWhiteSpace(profile.MinecraftVersion))
        {
            throw new InvalidOperationException("minecraft_version が未設定です。");
        }

        if (string.IsNullOrWhiteSpace(profile.Loader.Type))
        {
            throw new InvalidOperationException("loader.type が未設定です。");
        }

        if (string.IsNullOrWhiteSpace(profile.GameDirectory.Name))
        {
            throw new InvalidOperationException("game_directory.name が未設定です。");
        }
    }
}
