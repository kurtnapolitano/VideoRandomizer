using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoRandomizer;

/// <summary>
/// Everything the player reads out of config.json.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Folder that holds the video files to play.</summary>
    public string VideoFolder { get; set; } = "";

    /// <summary>File extensions treated as playable video, e.g. ".mp4".</summary>
    public List<string> VideoExtensions { get; set; } = [];

    /// <summary>Folder that holds the bumper images. Leave blank to disable bumpers.</summary>
    public string BumperFolder { get; set; } = "";

    /// <summary>File extensions treated as bumper images, e.g. ".jpg".</summary>
    public List<string> BumperExtensions { get; set; } = [];

    /// <summary>How long a bumper image stays on screen, in milliseconds.</summary>
    public int BumperDurationMs { get; set; } = 5000;

    /// <summary>When true, subfolders of the video and bumper folders are scanned too.</summary>
    public bool SearchSubfolders { get; set; }

    /// <summary>Mute video audio. Handy for storefront displays.</summary>
    public bool MuteAudio { get; set; }

    [JsonIgnore]
    public bool BumpersEnabled => !string.IsNullOrWhiteSpace(BumperFolder) && BumperExtensions.Count > 0;
}

public sealed class ConfigException(string message) : Exception(message);

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>config.json sitting next to the exe.</summary>
    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConfigException(
                $"Configuration file not found:\n{path}\n\n" +
                "Create it next to VideoRandomizer.exe, or pass a path as the first command line argument.");
        }

        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options);
        }
        catch (JsonException ex)
        {
            throw new ConfigException(
                $"{path} is not valid JSON.\n\n{ex.Message}\n\n" +
                "Tip: in JSON, Windows paths need forward slashes (C:/Videos) or doubled " +
                "backslashes (C:\\\\Videos).");
        }

        if (config is null)
        {
            throw new ConfigException($"{path} is empty.");
        }

        Normalize(config);
        Validate(config, path);
        return config;
    }

    /// <summary>
    /// Accept the extension spellings people actually type: "mp4", ".MP4", "*.mp4".
    /// </summary>
    private static void Normalize(AppConfig config)
    {
        config.VideoFolder = config.VideoFolder.Trim();
        config.BumperFolder = config.BumperFolder.Trim();
        config.VideoExtensions = NormalizeExtensions(config.VideoExtensions);
        config.BumperExtensions = NormalizeExtensions(config.BumperExtensions);
    }

    private static List<string> NormalizeExtensions(IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return [];
        }

        return extensions
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().TrimStart('*').ToLowerInvariant())
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Where(e => e.Length > 1)
            .Distinct()
            .ToList();
    }

    private static void Validate(AppConfig config, string path)
    {
        var errors = new List<string>();

        if (config.VideoFolder.Length == 0)
        {
            errors.Add("\"videoFolder\" is required.");
        }
        else if (!Directory.Exists(config.VideoFolder))
        {
            errors.Add($"\"videoFolder\" does not exist: {config.VideoFolder}");
        }

        if (config.VideoExtensions.Count == 0)
        {
            errors.Add("\"videoExtensions\" must list at least one extension, e.g. [ \".mp4\", \".mkv\" ].");
        }

        // A blank bumper folder is a legitimate way to turn bumpers off; a wrong one is not.
        if (config.BumperFolder.Length > 0 && !Directory.Exists(config.BumperFolder))
        {
            errors.Add($"\"bumperFolder\" does not exist: {config.BumperFolder}");
        }

        if (config.BumperDurationMs < 0)
        {
            errors.Add("\"bumperDurationMs\" cannot be negative.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigException($"Problems in {path}:\n\n  " + string.Join("\n  ", errors));
        }
    }
}
