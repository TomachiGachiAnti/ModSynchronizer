namespace ModSetup.App.Services;

internal sealed class GithubLatestRelease
{
    public string TagName { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public List<GithubReleaseAsset> Assets { get; set; } = new();
}
