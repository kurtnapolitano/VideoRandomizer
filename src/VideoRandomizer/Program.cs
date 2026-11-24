namespace VideoRandomizer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        StartupLog.Begin();

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

        StartupLog.Write("config loaded");

        Application.Run(new PlayerForm(config));

        // A wedged VLC decoder thread should never keep the process alive after ESC.
        Environment.Exit(0);
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "VideoRandomizer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
