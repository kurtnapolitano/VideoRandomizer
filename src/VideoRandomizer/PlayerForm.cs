using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace VideoRandomizer;

/// <summary>
/// Borderless full screen window that runs the bumper/video loop until ESC is pressed.
/// </summary>
public sealed class PlayerForm : Form, IMessageFilter
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkEscape = 0x1B;

    /// <summary>How long to wait before rescanning when the video folder is empty.</summary>
    private static readonly TimeSpan EmptyFolderRetry = TimeSpan.FromSeconds(10);

    private readonly AppConfig _config;
    private readonly Random _rng = new();
    private readonly CancellationTokenSource _cts = new();

    private readonly VideoView _videoView;
    private readonly PictureBox _bumperView;
    private readonly Label _statusLabel;

    private LibVLC? _libVlc;
    private MediaPlayer? _player;

    // The stream behind the current bumper has to stay open for the lifetime of the Image,
    // otherwise GDI+ faults when it draws (and animated GIFs stop animating).
    private Image? _bumperImage;
    private Stream? _bumperStream;

    private bool _shuttingDown;

    public PlayerForm(AppConfig config)
    {
        _config = config;

        Text = "VideoRandomizer";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        BackColor = Color.Black;
        KeyPreview = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        _videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
        };

        _bumperView = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.Zoom,
            Visible = false,
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(FontFamily.GenericSansSerif, 16f),
            Visible = false,
        };

        // The video surface stays at the back for the whole session; bumpers and status text
        // are layered over it. Hiding the VideoView risks VLC losing its render target.
        Controls.Add(_statusLabel);
        Controls.Add(_bumperView);
        Controls.Add(_videoView);
        _videoView.SendToBack();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        Application.AddMessageFilter(this);
        Cursor.Hide();
        KeepDisplayAwake();

        _libVlc = new LibVLC();
        _player = new MediaPlayer(_libVlc)
        {
            // Let the form own the keyboard and mouse so ESC always reaches us.
            EnableKeyInput = false,
            EnableMouseInput = false,
            Mute = _config.MuteAudio,
        };
        _videoView.MediaPlayer = _player;

        ShowStatus("Starting…");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        _ = RunAsync(_cts.Token);
    }

    /// <summary>
    /// The playback loop: shuffle, then bumper/video/bumper/video… and rescan the folder at the
    /// end of every pass so newly added files get picked up.
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        string? lastPlayed = null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var videos = MediaLibrary.Scan(
                    _config.VideoFolder, _config.VideoExtensions, _config.SearchSubfolders);

                var bumpers = _config.BumpersEnabled
                    ? MediaLibrary.Scan(
                        _config.BumperFolder, _config.BumperExtensions, _config.SearchSubfolders)
                    : [];

                if (videos.Count == 0)
                {
                    ShowStatus(
                        $"No video files found in{Environment.NewLine}{_config.VideoFolder}" +
                        $"{Environment.NewLine}{Environment.NewLine}Looking for: " +
                        $"{string.Join("  ", _config.VideoExtensions)}" +
                        $"{Environment.NewLine}{Environment.NewLine}Checking again every " +
                        $"{EmptyFolderRetry.TotalSeconds:0} seconds.  Press ESC to exit.");

                    await Task.Delay(EmptyFolderRetry, ct);
                    continue;
                }

                MediaLibrary.ShuffleAvoidingRepeat(videos, lastPlayed, _rng);

                foreach (var video in videos)
                {
                    ct.ThrowIfCancellationRequested();
                    await ShowBumperAsync(bumpers, ct);

                    ct.ThrowIfCancellationRequested();
                    await PlayVideoAsync(video, ct);

                    lastPlayed = video;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ESC. Nothing to clean up here; OnFormClosed handles teardown.
        }
        catch (Exception ex)
        {
            ShowStatus($"Playback stopped after an unexpected error:{Environment.NewLine}" +
                       $"{Environment.NewLine}{ex.Message}{Environment.NewLine}" +
                       $"{Environment.NewLine}Press ESC to exit.");
        }
    }

    /// <summary>Shows one randomly chosen bumper for the configured duration.</summary>
    private async Task ShowBumperAsync(IReadOnlyList<string> bumpers, CancellationToken ct)
    {
        if (bumpers.Count == 0 || _config.BumperDurationMs <= 0)
        {
            return;
        }

        string path = bumpers[_rng.Next(bumpers.Count)];

        Image image;
        Stream stream;
        try
        {
            (image, stream) = await Task.Run(() => LoadImage(path), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Corrupt or locked image: skip the bumper rather than stalling the loop.
            return;
        }

        SetBumper(image, stream);
        ShowBumperView();

        await Task.Delay(_config.BumperDurationMs, ct);
    }

    /// <summary>Plays one file and returns when it finishes, errors out, or ESC is pressed.</summary>
    private async Task PlayVideoAsync(string path, CancellationToken ct)
    {
        if (_player is null || _libVlc is null)
        {
            return;
        }

        // RunContinuationsAsynchronously keeps the resumption off VLC's event thread, where
        // calling back into the player would deadlock.
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFinished(object? sender, EventArgs e) => finished.TrySetResult();

        _player.EndReached += OnFinished;
        _player.EncounteredError += OnFinished;

        Media? media = null;
        try
        {
            media = new Media(_libVlc, path, FromType.FromPath);
            ShowVideoView();

            if (!_player.Play(media))
            {
                return; // Could not start this file; the loop moves on to the next one.
            }

            await finished.Task.WaitAsync(ct);
        }
        finally
        {
            _player.EndReached -= OnFinished;
            _player.EncounteredError -= OnFinished;
            StopPlayback();
            media?.Dispose();
        }
    }

    private void StopPlayback()
    {
        // After EndReached the player has already stopped, so this only does real work on the
        // ESC path.
        try
        {
            if (_player is { IsPlaying: true })
            {
                _player.Stop();
            }
        }
        catch (Exception)
        {
            // A player torn down underneath us is not worth reporting during shutdown.
        }
    }

    private static (Image Image, Stream Stream) LoadImage(string path)
    {
        // Read into memory so the player never holds a lock on a file someone wants to replace.
        var stream = new MemoryStream(File.ReadAllBytes(path));
        try
        {
            return (Image.FromStream(stream), stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private void SetBumper(Image image, Stream stream)
    {
        var oldImage = _bumperImage;
        var oldStream = _bumperStream;

        _bumperImage = image;
        _bumperStream = stream;
        _bumperView.Image = image;

        oldImage?.Dispose();
        oldStream?.Dispose();
    }

    private void ShowVideoView()
    {
        _statusLabel.Visible = false;
        _bumperView.Visible = false;
    }

    private void ShowBumperView()
    {
        _statusLabel.Visible = false;
        _bumperView.Visible = true;
        _bumperView.BringToFront();
    }

    private void ShowStatus(string text)
    {
        _bumperView.Visible = false;
        _statusLabel.Text = text;
        _statusLabel.Visible = true;
        _statusLabel.BringToFront();
    }

    /// <summary>
    /// Catches ESC even when the native VLC child window has focus, which is where a plain
    /// KeyDown handler would miss it.
    /// </summary>
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is WmKeyDown or WmSysKeyDown && (int)m.WParam == VkEscape)
        {
            BeginExit();
            return true;
        }

        return false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            BeginExit();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BeginExit()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        _cts.Cancel();
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _shuttingDown = true;
        _cts.Cancel();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(this);
        Cursor.Show();
        AllowDisplaySleep();
        ReleaseVlc();

        _bumperView.Image = null;
        _bumperImage?.Dispose();
        _bumperStream?.Dispose();
        _cts.Dispose();

        base.OnFormClosed(e);
    }

    private void ReleaseVlc()
    {
        var player = _player;
        var libVlc = _libVlc;
        _player = null;
        _libVlc = null;

        if (player is null && libVlc is null)
        {
            return;
        }

        _videoView.MediaPlayer = null;

        // VLC teardown occasionally blocks on a stuck decoder thread. Give it a moment, then
        // let the process exit regardless (Program.Main forces the exit).
        Task.Run(() =>
        {
            try
            {
                player?.Dispose();
                libVlc?.Dispose();
            }
            catch (Exception)
            {
                // Shutdown path; nothing useful to do with a failure here.
            }
        }).Wait(TimeSpan.FromSeconds(3));
    }

    private static void KeepDisplayAwake() =>
        SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);

    private static void AllowDisplaySleep() => SetThreadExecutionState(EsContinuous);

    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);
}
