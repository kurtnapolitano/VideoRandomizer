using Xunit;

namespace VideoRandomizer.Tests;

public class PluginCacheTests
{
    [Fact]
    public void NeedsRebuild_UntilACacheHasBeenBuilt()
    {
        using var dir = new TempDir();
        dir.Write("a_plugin.dll", "one");
        var cache = new PluginCache(dir.Path);

        Assert.True(cache.NeedsRebuild());

        dir.Write("plugins.dat", "cache");
        Assert.True(cache.MarkBuilt());

        Assert.False(cache.NeedsRebuild());
    }

    [Fact]
    public void NeedsRebuild_WhenAPluginTimestampChanges()
    {
        // What re-deploying by thumb drive can do, even with identical file contents.
        using var dir = new TempDir();
        string plugin = dir.Write("a_plugin.dll", "one");
        dir.Write("plugins.dat", "cache");
        var cache = new PluginCache(dir.Path);
        cache.MarkBuilt();

        File.SetLastWriteTimeUtc(plugin, File.GetLastWriteTimeUtc(plugin).AddSeconds(2));

        Assert.True(cache.NeedsRebuild());
    }

    [Fact]
    public void MarkBuilt_FailsWhenVlcWroteNoCache()
    {
        using var dir = new TempDir();
        dir.Write("a_plugin.dll", "one");
        var cache = new PluginCache(dir.Path);

        Assert.False(cache.MarkBuilt());
        Assert.True(cache.NeedsRebuild());
    }

    [Fact]
    public void NeedsRebuild_IsFalseWithoutAPluginFolder()
    {
        using var dir = new TempDir();

        Assert.False(new PluginCache(Path.Combine(dir.Path, "absent")).NeedsRebuild());
    }
}
