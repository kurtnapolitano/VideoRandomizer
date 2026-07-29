namespace VideoRandomizer;

public enum BumperKind
{
    Image,
    Video,
}

/// <summary>A bumper file plus how it should be presented.</summary>
public readonly record struct Bumper(string Path, BumperKind Kind);

/// <summary>
/// Reads the video and bumper folders off disk and puts the results in a random order.
/// </summary>
public static class MediaLibrary
{
    /// <summary>
    /// Everything in the bumper folder, tagged as image or video. The two kinds share one pool,
    /// so a bumper is chosen from all of them with equal probability.
    /// </summary>
    public static List<Bumper> ScanBumpers(
        string folder,
        IReadOnlyCollection<string> imageExtensions,
        IReadOnlyCollection<string> videoExtensions,
        bool searchSubfolders)
    {
        var bumpers = new List<Bumper>();

        foreach (string path in Scan(folder, imageExtensions, searchSubfolders))
        {
            bumpers.Add(new Bumper(path, BumperKind.Image));
        }

        foreach (string path in Scan(folder, videoExtensions, searchSubfolders))
        {
            bumpers.Add(new Bumper(path, BumperKind.Video));
        }

        return bumpers;
    }

    /// <summary>
    /// Every file under <paramref name="folder"/> whose extension is in
    /// <paramref name="extensions"/>. Never throws: an unreadable folder yields an empty list,
    /// because a missing network share should not take the player down mid-loop.
    /// </summary>
    public static List<string> Scan(string folder, IReadOnlyCollection<string> extensions, bool searchSubfolders)
    {
        if (string.IsNullOrWhiteSpace(folder) || extensions.Count == 0 || !Directory.Exists(folder))
        {
            return [];
        }

        var wanted = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var search = searchSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            return Directory
                .EnumerateFiles(folder, "*", search)
                .Where(f => wanted.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Fisher-Yates, in place.</summary>
    public static void Shuffle<T>(IList<T> items, Random rng)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Shuffles <paramref name="paths"/>, reshuffling if the first entry is the file that just
    /// played, so the same video never runs twice back to back across a cycle boundary.
    /// </summary>
    public static void ShuffleAvoidingRepeat(IList<string> paths, string? lastPlayed, Random rng)
    {
        Shuffle(paths, rng);

        // With 0 or 1 files there is no alternative to offer, so a repeat is unavoidable.
        if (lastPlayed is null || paths.Count < 2)
        {
            return;
        }

        const int maxAttempts = 32;
        for (int attempt = 0; attempt < maxAttempts && IsSameFile(paths[0], lastPlayed); attempt++)
        {
            Shuffle(paths, rng);
        }

        // Ludicrously unlikely, but a bounded loop needs a deterministic way out: if the shuffles
        // kept landing on the repeat, just move it out of the first slot.
        if (IsSameFile(paths[0], lastPlayed))
        {
            int swapWith = rng.Next(1, paths.Count);
            (paths[0], paths[swapWith]) = (paths[swapWith], paths[0]);
        }
    }

    private static bool IsSameFile(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
