namespace VideoRandomizer.Tests;

/// <summary>Throwaway directory that cleans itself up at the end of a test.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "VideoRandomizerTests", System.IO.Path.GetRandomFileName());

    public TempDir() => Directory.CreateDirectory(Path);

    /// <summary>Creates an empty file; <paramref name="relativePath"/> may include subfolders.</summary>
    public string Touch(string relativePath)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, []);
        return full;
    }

    public string Write(string relativePath, string contents)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; the OS will reclaim the temp folder.
        }
    }
}
