using Xunit;

namespace VideoRandomizer.Tests;

public class ConfigLoaderTests
{
    private static string ConfigFor(TempDir dir, string body) => dir.Write("config.json", body);

    [Fact]
    public void Load_ReadsAllSettings()
    {
        using var dir = new TempDir();
        string videos = Path.Combine(dir.Path, "videos");
        string bumpers = Path.Combine(dir.Path, "bumpers");
        Directory.CreateDirectory(videos);
        Directory.CreateDirectory(bumpers);

        string path = ConfigFor(dir, $$"""
            {
              // comments and trailing commas are tolerated
              "videoFolder": {{Json(videos)}},
              "videoExtensions": [ ".mp4", ".mkv" ],
              "bumperFolder": {{Json(bumpers)}},
              "bumperExtensions": [ ".png" ],
              "bumperDurationMs": 3500,
              "searchSubfolders": true,
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal(videos, config.VideoFolder);
        Assert.Equal([".mp4", ".mkv"], config.VideoExtensions);
        Assert.Equal(3500, config.BumperDurationMs);
        Assert.True(config.SearchSubfolders);
        Assert.True(config.BumpersEnabled);
    }

    [Theory]
    [InlineData("mp4", ".mp4")]
    [InlineData(".MP4", ".mp4")]
    [InlineData("*.mp4", ".mp4")]
    [InlineData("  .Mp4  ", ".mp4")]
    public void Load_NormalizesExtensionSpellings(string written, string expected)
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [ {{Json(written)}} ],
              "bumperFolder": "",
              "bumperExtensions": [],
              "bumperDurationMs": 1000
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal([expected], config.VideoExtensions);
    }

    [Fact]
    public void Load_BlankBumperFolderDisablesBumpers()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": "",
              "bumperExtensions": [ ".png" ],
              "bumperDurationMs": 1000
            }
            """);

        Assert.False(ConfigLoader.Load(path).BumpersEnabled);
    }

    [Fact]
    public void Load_MissingVideoFolderIsReported()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, """
            {
              "videoFolder": "Z:/nope",
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": "",
              "bumperExtensions": [],
              "bumperDurationMs": 1000
            }
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("videoFolder", ex.Message);
    }

    [Fact]
    public void Load_NoExtensionsIsReported()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [],
              "bumperFolder": "",
              "bumperExtensions": [],
              "bumperDurationMs": 1000
            }
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("videoExtensions", ex.Message);
    }

    [Fact]
    public void Load_MalformedJsonGivesAPathHint()
    {
        using var dir = new TempDir();
        // The classic mistake: a single backslash in a JSON string.
        string path = ConfigFor(dir, """
            { "videoFolder": "C:\Videos" }
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("valid JSON", ex.Message);
        Assert.Contains("forward slashes", ex.Message);
    }

    [Fact]
    public void Load_MissingFileIsReported()
    {
        using var dir = new TempDir();

        var ex = Assert.Throws<ConfigException>(
            () => ConfigLoader.Load(Path.Combine(dir.Path, "absent.json")));

        Assert.Contains("not found", ex.Message);
    }

    private static string Json(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
