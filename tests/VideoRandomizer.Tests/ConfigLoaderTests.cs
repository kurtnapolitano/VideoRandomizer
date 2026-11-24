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
              "bumperImageExtensions": [ ".png" ],
              "bumperVideoExtensions": [ ".mp4" ],
              "bumperDurationMs": 3500,
              "searchSubfolders": true,
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal(videos, config.VideoFolder);
        Assert.Equal([".mp4", ".mkv"], config.VideoExtensions);
        Assert.Equal([".png"], config.BumperImageExtensions);
        Assert.Equal([".mp4"], config.BumperVideoExtensions);
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
              "bumperImageExtensions": [],
              "bumperVideoExtensions": [],
              "bumperDurationMs": 1000
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal([expected], config.VideoExtensions);
    }

    [Fact]
    public void Load_VideoOnlyBumpersAreEnabled()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": {{Json(dir.Path)}},
              "bumperImageExtensions": [],
              "bumperVideoExtensions": [ ".mp4" ],
              "bumperDurationMs": 0
            }
            """);

        Assert.True(ConfigLoader.Load(path).BumpersEnabled);
    }

    [Fact]
    public void Load_AcceptsLegacyBumperExtensionsAsImages()
    {
        // Config files written before bumper videos existed must keep working.
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": {{Json(dir.Path)}},
              "bumperExtensions": [ ".png", ".jpg" ],
              "bumperDurationMs": 1000
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal([".png", ".jpg"], config.BumperImageExtensions);
        Assert.Empty(config.BumperVideoExtensions);
        Assert.True(config.BumpersEnabled);
    }

    [Fact]
    public void Load_NewImageExtensionsWinOverLegacyName()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": {{Json(dir.Path)}},
              "bumperImageExtensions": [ ".png" ],
              "bumperExtensions": [ ".bmp" ],
              "bumperDurationMs": 1000
            }
            """);

        Assert.Equal([".png"], ConfigLoader.Load(path).BumperImageExtensions);
    }

    [Fact]
    public void Load_ExtensionInBothBumperListsIsReported()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(dir.Path)}},
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": {{Json(dir.Path)}},
              "bumperImageExtensions": [ ".gif" ],
              "bumperVideoExtensions": [ ".GIF" ],
              "bumperDurationMs": 1000
            }
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains(".gif", ex.Message);
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
              "bumperImageExtensions": [ ".png" ],
              "bumperVideoExtensions": [ ".mp4" ],
              "bumperDurationMs": 1000
            }
            """);

        Assert.False(ConfigLoader.Load(path).BumpersEnabled);
    }

    [Fact]
    public void Load_UnreachableFoldersDoNotFailTheLoad()
    {
        // A network drive may still be connecting after a reboot, so the player waits for it.
        using var dir = new TempDir();
        string videos = Path.Combine(dir.Path, "not-yet-videos");
        string bumpers = Path.Combine(dir.Path, "not-yet-bumpers");
        string path = ConfigFor(dir, $$"""
            {
              "videoFolder": {{Json(videos)}},
              "videoExtensions": [ ".mp4" ],
              "bumperFolder": {{Json(bumpers)}},
              "bumperImageExtensions": [ ".png" ]
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal([videos, bumpers], ConfigLoader.MissingFolders(config));

        Directory.CreateDirectory(videos);
        Directory.CreateDirectory(bumpers);

        Assert.Empty(ConfigLoader.MissingFolders(config));
    }

    [Fact]
    public void MissingFolders_IgnoresABlankBumperFolder()
    {
        using var dir = new TempDir();
        var config = new AppConfig { VideoFolder = dir.Path, BumperFolder = "" };

        Assert.Empty(ConfigLoader.MissingFolders(config));
    }

    [Fact]
    public void Load_BlankVideoFolderIsReported()
    {
        using var dir = new TempDir();
        string path = ConfigFor(dir, """
            { "videoFolder": "", "videoExtensions": [ ".mp4" ] }
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
              "bumperImageExtensions": [],
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
