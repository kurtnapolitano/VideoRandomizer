using System.Security.Cryptography;
using System.Text;

namespace VideoRandomizer;

/// <summary>
/// Decides when VLC's plugin cache (plugins.dat) has to be rebuilt. Without it VLC opens every
/// plugin DLL each time it starts, which is very slow on the first launch after a reboot.
///
/// The cache is built on the machine that runs the app because VLC matches it against each
/// plugin's size and modified time, and copying by thumb drive can change the latter.
/// </summary>
public sealed class PluginCache(string pluginFolder)
{
    private string CacheFile => Path.Combine(pluginFolder, "plugins.dat");

    /// <summary>Records which set of plugin files the cache was built from.</summary>
    private string StampFile => Path.Combine(pluginFolder, "plugins.stamp");

    /// <summary>True when there is no cache, or the plugins have changed since it was built.</summary>
    public bool NeedsRebuild()
    {
        try
        {
            if (!Directory.Exists(pluginFolder))
            {
                return false;
            }

            return !File.Exists(CacheFile)
                   || !File.Exists(StampFile)
                   || File.ReadAllText(StampFile) != Fingerprint();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false; // The cache is only a speed-up; never let it block startup.
        }
    }

    /// <summary>
    /// Call after VLC has been asked to rebuild. Returns false if no cache could be written,
    /// typically because the app folder is read-only.
    /// </summary>
    public bool MarkBuilt()
    {
        try
        {
            if (!File.Exists(CacheFile))
            {
                return false;
            }

            File.WriteAllText(StampFile, Fingerprint());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string Fingerprint()
    {
        var files = new DirectoryInfo(pluginFolder)
            .EnumerateFiles("*.dll", SearchOption.AllDirectories)
            .Select(f => $"{Path.GetRelativePath(pluginFolder, f.FullName)}|{f.Length}|{f.LastWriteTimeUtc.Ticks}")
            .Order(StringComparer.OrdinalIgnoreCase);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', files))));
    }
}
