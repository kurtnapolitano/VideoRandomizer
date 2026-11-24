using System.Diagnostics;

namespace VideoRandomizer;

/// <summary>
/// Timestamps each step of startup so a slow launch on a machine we cannot reach can be diagnosed
/// from the file it leaves behind. Never throws: logging must not be able to stop the player.
/// </summary>
internal static class StartupLog
{
    private const string FileName = "startup.log";
    private const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new();
    private static DateTime _processStart = DateTime.Now;
    private static string? _path;

    public static void Begin()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            _processStart = process.StartTime;
        }
        catch (Exception)
        {
            // Without the real start time, elapsed times count from here instead.
        }

        // Beside the exe is where someone on site will look; fall back if that is read-only.
        string fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoRandomizer");

        _path = TryPrepare(AppContext.BaseDirectory) ?? TryPrepare(fallback);

        var sinceBoot = TimeSpan.FromMilliseconds(Environment.TickCount64);

        Write($"--- launch from {AppContext.BaseDirectory} ({sinceBoot.TotalMinutes:0.0} minutes after boot)");
        Write("program code running");
    }

    /// <summary>Appends a line prefixed with the clock time and seconds since the process started.</summary>
    public static void Write(string message)
    {
        if (_path is null)
        {
            return;
        }

        var now = DateTime.Now;
        string line = $"{now:yyyy-MM-dd HH:mm:ss.fff}  +{(now - _processStart).TotalSeconds,7:0.00}s  " +
                      $"{message}{Environment.NewLine}";

        try
        {
            lock (Gate)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch (Exception)
        {
            // A full or locked disk costs us the log line, nothing more.
        }
    }

    private static string? TryPrepare(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, FileName);

            // Start over once the file gets big; a kiosk can run unattended for years.
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
            {
                File.Delete(path);
            }

            File.AppendAllText(path, "");
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
