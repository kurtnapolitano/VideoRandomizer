using LibVLCSharp.Shared;

namespace VideoRandomizer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        string configPath = args.Length > 0 ? args[0] : ConfigLoader.DefaultPath;

        AppConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
        }
        catch (ConfigException ex)
        {
            ShowError(ex.Message);
            return;
        }

        try
        {
            Core.Initialize();
        }
        catch (Exception ex)
        {
            ShowError($"Could not start the VLC playback engine.{Environment.NewLine}" +
                      $"{Environment.NewLine}{ex.Message}");
            return;
        }

        Application.Run(new PlayerForm(config));

        // A wedged VLC decoder thread should never keep the process alive after ESC.
        Environment.Exit(0);
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "VideoRandomizer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
