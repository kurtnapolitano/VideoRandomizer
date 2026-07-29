using Xunit;

namespace VideoRandomizer.Tests;

public class MediaLibraryTests
{
    [Fact]
    public void Scan_ReturnsOnlyConfiguredExtensions()
    {
        using var dir = new TempDir();
        dir.Touch("a.mp4");
        dir.Touch("b.mkv");
        dir.Touch("c.txt");
        dir.Touch("d.jpg");

        var found = MediaLibrary.Scan(dir.Path, [".mp4", ".mkv"], searchSubfolders: false);

        Assert.Equal(["a.mp4", "b.mkv"], found.Select(System.IO.Path.GetFileName).Order());
    }

    [Fact]
    public void Scan_IsCaseInsensitiveOnExtensions()
    {
        using var dir = new TempDir();
        dir.Touch("LOUD.MP4");
        dir.Touch("quiet.Mp4");

        var found = MediaLibrary.Scan(dir.Path, [".mp4"], searchSubfolders: false);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Scan_HonoursSearchSubfolders()
    {
        using var dir = new TempDir();
        dir.Touch("top.mp4");
        dir.Touch("nested/deep.mp4");

        Assert.Single(MediaLibrary.Scan(dir.Path, [".mp4"], searchSubfolders: false));
        Assert.Equal(2, MediaLibrary.Scan(dir.Path, [".mp4"], searchSubfolders: true).Count);
    }

    [Fact]
    public void Scan_MissingFolderReturnsEmpty()
    {
        var found = MediaLibrary.Scan(@"Z:\definitely\not\here", [".mp4"], searchSubfolders: false);

        Assert.Empty(found);
    }

    [Fact]
    public void ScanBumpers_TagsImagesAndVideosSeparately()
    {
        using var dir = new TempDir();
        dir.Touch("still.png");
        dir.Touch("clip.mp4");
        dir.Touch("notes.txt");

        var bumpers = MediaLibrary.ScanBumpers(dir.Path, [".png"], [".mp4"], searchSubfolders: false);

        Assert.Equal(2, bumpers.Count);
        Assert.Contains(bumpers, b => b.Kind == BumperKind.Image && b.Path.EndsWith("still.png"));
        Assert.Contains(bumpers, b => b.Kind == BumperKind.Video && b.Path.EndsWith("clip.mp4"));
    }

    [Fact]
    public void ScanBumpers_ImagesOnlyWhenNoVideoExtensions()
    {
        using var dir = new TempDir();
        dir.Touch("still.png");
        dir.Touch("clip.mp4");

        var bumpers = MediaLibrary.ScanBumpers(dir.Path, [".png"], [], searchSubfolders: false);

        Assert.Equal(BumperKind.Image, Assert.Single(bumpers).Kind);
    }

    [Fact]
    public void ScanBumpers_VideosOnlyWhenNoImageExtensions()
    {
        // This is the "bumperDurationMs: 0" case: stills are dropped, clips still play.
        using var dir = new TempDir();
        dir.Touch("still.png");
        dir.Touch("clip.mp4");

        var bumpers = MediaLibrary.ScanBumpers(dir.Path, [], [".mp4"], searchSubfolders: false);

        Assert.Equal(BumperKind.Video, Assert.Single(bumpers).Kind);
    }

    [Fact]
    public void ScanBumpers_EmptyFolderYieldsNoBumpers()
    {
        using var dir = new TempDir();

        Assert.Empty(MediaLibrary.ScanBumpers(dir.Path, [".png"], [".mp4"], searchSubfolders: false));
    }

    [Fact]
    public void Shuffle_KeepsEveryItem()
    {
        var items = Enumerable.Range(0, 200).Select(i => $"v{i}.mp4").ToList();
        var original = items.ToList();

        MediaLibrary.Shuffle(items, new Random(1234));

        Assert.Equal(original.Order(), items.Order());
    }

    /// <summary>
    /// The core requirement: crossing a cycle boundary must never replay the same file twice.
    /// </summary>
    [Fact]
    public void ShuffleAvoidingRepeat_NeverLeadsWithTheLastPlayedFile()
    {
        var rng = new Random(7);

        // Three files makes an accidental repeat likely (1 in 3 per pass), so 5000 passes would
        // catch a broken guard many times over.
        for (int i = 0; i < 5000; i++)
        {
            var items = new List<string> { "a.mp4", "b.mp4", "c.mp4" };
            MediaLibrary.ShuffleAvoidingRepeat(items, "b.mp4", rng);

            Assert.NotEqual("b.mp4", items[0]);
            Assert.Equal(3, items.Count);
            Assert.Equal(["a.mp4", "b.mp4", "c.mp4"], items.Order());
        }
    }

    [Fact]
    public void ShuffleAvoidingRepeat_ComparesPathsCaseInsensitively()
    {
        var rng = new Random(11);

        for (int i = 0; i < 500; i++)
        {
            var items = new List<string> { @"C:\v\A.mp4", @"C:\v\B.mp4" };
            MediaLibrary.ShuffleAvoidingRepeat(items, @"c:\v\a.MP4", rng);

            Assert.Equal(@"C:\v\B.mp4", items[0]);
        }
    }

    [Fact]
    public void ShuffleAvoidingRepeat_SingleFileStillPlays()
    {
        // One video in the folder means a repeat is unavoidable; it must not hang or drop it.
        var items = new List<string> { "only.mp4" };

        MediaLibrary.ShuffleAvoidingRepeat(items, "only.mp4", new Random(3));

        Assert.Equal(["only.mp4"], items);
    }

    [Fact]
    public void ShuffleAvoidingRepeat_HandlesEmptyList()
    {
        var items = new List<string>();

        MediaLibrary.ShuffleAvoidingRepeat(items, "gone.mp4", new Random(3));

        Assert.Empty(items);
    }

    [Fact]
    public void ShuffleAvoidingRepeat_FirstRunHasNoConstraint()
    {
        var items = new List<string> { "a.mp4", "b.mp4" };

        MediaLibrary.ShuffleAvoidingRepeat(items, lastPlayed: null, new Random(3));

        Assert.Equal(2, items.Count);
    }
}
