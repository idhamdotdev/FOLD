using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using FOLD.Input;
using FOLD.ScreenCapture;
using FOLD.Server;
using FOLD.Usb;
using FOLD.VirtualDisplay;

namespace FOLD;

public sealed class TrayApp : IDisposable
{
    private readonly NotifyIcon    _trayIcon;
    private readonly H264Server    _h264Server;
    private readonly TouchReceiver _touchReceiver;
    private readonly AdbForwarder  _adbForwarder;
    private IScreenCapture?        _capture;

    private bool   _running;
    private bool   _usbMode;
    private IntPtr _selectedMonitor      = IntPtr.Zero;
    private string _selectedMonitorLabel = "Primary Monitor";

    private int _forceWidth  = 0;
    private int _forceHeight = 0;
    private int _forceFps    = 60;
    private int _selectedResIndex = 0;
    public readonly List<ResolutionOption> ResolutionOptions = new();

    // The main window — created once, shown/hidden.
    private readonly MainWindow _window;

    // ── Public properties (read by MainWindow) ─────────────────────────
    public bool   IsRunning     => _running;
    public bool   IsUsbMode     => _usbMode;
    public IntPtr SelectedMonitor => _selectedMonitor;

    public int ForceWidth  => _forceWidth;
    public int ForceHeight => _forceHeight;
    public int ForceFps    => _forceFps;
    public int SelectedResIndex => _selectedResIndex;

    public int StreamPort { get; set; } = 8765;
    public int TouchPort  { get; set; } = 8766;

    public TrayApp()
    {
        ResolutionOptions.Add(new ResolutionOption("Auto (Match Device Display)", 0, 0, 60));
        ResolutionOptions.Add(new ResolutionOption("1080p Full HD (1920x1080 @ 60 FPS)", 1920, 1080, 60));

        _h264Server    = new H264Server(StreamPort, targetFps: 60, bitrateMbps: 24) { ServerApp = this };
        _touchReceiver = new TouchReceiver(TouchPort) { Server = _h264Server };
        _adbForwarder  = new AdbForwarder(StreamPort, TouchPort);

        _trayIcon = new NotifyIcon
        {
            Icon             = LoadIcon(),
            Text             = "FOLD",
            Visible          = true,
            ContextMenuStrip = BuildTrayMenu(),
        };

        // Show initial status dot (red = not yet streaming)
        UpdateTray();

        // Double-click tray icon → show window
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        // Create main window
        _window = new MainWindow(this);

        // Auto-detect virtual display after main window is ready
        AutoDetectVirtualMonitor();
    }

    public void Run()
    {
        StartStreaming();

        // Show the main window on the first idle tick after the message pump starts.
        // This is more reliable than calling Show() before Application.Run().
        void OnFirstIdle(object? s, EventArgs e)
        {
            Application.Idle -= OnFirstIdle;   // fire only once
            ShowWindow();
        }
        Application.Idle += OnFirstIdle;

        // ApplicationContext with no owner form keeps the process alive
        // even when all windows are hidden (tray-only mode).
        Application.Run(new ApplicationContext());
    }

    // ── Streaming lifecycle ────────────────────────────────────────────

    internal void StartStreaming()
    {
        try
        {
            // Auto detect virtual monitor before starting capture!
            AutoDetectVirtualMonitor();

            if (_selectedMonitor == IntPtr.Zero)
            {
                // No virtual display found — fall back to primary monitor mirroring
                // so the app still works (user can install the driver later)
                var primary = Screen.PrimaryScreen;
                if (primary != null)
                {
                    // Find the HMONITOR handle for the primary screen
                    var monitors = VirtualDisplayManager.GetAllMonitors();
                    var primaryMon = monitors.Find(m => m.IsPrimary);
                    if (primaryMon != null)
                    {
                        _selectedMonitor = primaryMon.Handle;
                        _selectedMonitorLabel = "Primary (Mirroring)";
                    }
                }

                if (_selectedMonitor == IntPtr.Zero)
                {
                    _trayIcon.ShowBalloonTip(4000, "FOLD by @idham.dev",
                        "No display found. Go to Advanced → Install Virtual Display Driver.",
                        ToolTipIcon.Warning);
                    return;
                }

                // Notify the user they're mirroring, not extending
                if (!VirtualDisplayManager.IsVirtualDriverInstalled())
                {
                    _trayIcon.ShowBalloonTip(5000, "FOLD by @idham.dev",
                        "Mirroring primary display. For extended display, go to Advanced → Install Virtual Display Driver.",
                        ToolTipIcon.Info);
                }
            }

            // Register active monitor with input injector for correct multi-monitor touch mapping
            TouchInjector.SelectedMonitorHandle = _selectedMonitor;

            // Apply resolution setting on the virtual display BEFORE starting capture!
            if (_selectedMonitor != IntPtr.Zero && _forceWidth > 0)
            {
                VirtualDisplayManager.SetMonitorResolution(_selectedMonitor, _forceWidth, _forceHeight, _forceFps);
            }

            try
            {
                _capture = new WgcCapture(_selectedMonitor);
            }
            catch
            {
                _capture = new GdiCapture(_selectedMonitor);
            }

            _h264Server.Start(_capture);
            _touchReceiver.Start();
            _running = true;
            UpdateTray();
            if (_window != null) _window.RefreshStatus();
        }
        catch (Exception ex)
        {
            StopStreaming();
            _trayIcon.ShowBalloonTip(4000, "FOLD by @idham.dev — Start Failed",
                "Run as Administrator or check if ports are in use.\n" + ex.Message,
                System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    internal void StopStreaming()
    {
        _h264Server.Stop();
        _touchReceiver.Stop();
        if (_usbMode) _adbForwarder.RemoveForwards();
        _capture?.Dispose();
        _capture = null;
        _running = false;
        _usbMode = false;
        UpdateTray();
        _window.RefreshStatus();
    }

    public void RestartCaptureSession(int targetW, int targetH)
    {
        if (!_running) return;

        if (_capture == null || _capture.ScreenWidth != targetW || _capture.ScreenHeight != targetH)
        {
            try
            {
                _capture?.Dispose();
                _h264Server.Stop();

                // Give Windows a brief moment to stabilize the display adapter layout
                System.Threading.Thread.Sleep(300);

                try
                {
                    _capture = new WgcCapture(_selectedMonitor);
                }
                catch
                {
                    _capture = new GdiCapture(_selectedMonitor);
                }

                _h264Server.Start(_capture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restart capture session: {ex.Message}");
            }
        }
    }

    /// <summary>Called by MainWindow's Start/Stop button.</summary>
    public void ToggleStreaming(MainWindow _)
    {
        if (_running) StopStreaming();
        else          StartStreaming();
    }

    // ── Monitor switching ──────────────────────────────────────────────

    public void SwitchMonitor(IntPtr handle, string label)
    {
        if (handle == _selectedMonitor) return;

        bool wasRunning = _running;
        bool wasUsb     = _usbMode;

        if (wasRunning) StopStreaming();
        _selectedMonitor      = handle;
        _selectedMonitorLabel = label;

        FOLD.Input.TouchInjector.SelectedMonitorHandle = handle;

        if (wasRunning) StartStreaming();
        if (wasUsb) EnableUsbMode();

        _trayIcon.ShowBalloonTip(2000, "FOLD by @idham.dev", $"Now streaming: {label}", ToolTipIcon.None);
        if (_window != null) _window.RefreshStatus();
    }

    public void AutoDetectVirtualMonitor()
    {
        var vHandle = VirtualDisplayManager.GetVirtualDisplayMonitorHandle();
        if (vHandle != IntPtr.Zero)
        {
            if (vHandle != _selectedMonitor)
            {
                SwitchMonitor(vHandle, "Virtual Display");
            }
            return;
        }

        // Driver is installed but virtual monitor is not visible.
        // Try extending the display (no UAC prompt needed for DisplaySwitch).
        if (VirtualDisplayManager.IsVirtualDriverInstalled())
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "DisplaySwitch.exe",
                    Arguments = "/extend",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(3000);
                System.Threading.Thread.Sleep(1500);
            }
            catch { }

            vHandle = VirtualDisplayManager.GetVirtualDisplayMonitorHandle();
            if (vHandle != IntPtr.Zero)
            {
                if (vHandle != _selectedMonitor)
                {
                    SwitchMonitor(vHandle, "Virtual Display");
                }
                return;
            }

            // Last resort: try enabling the device (this may trigger UAC)
            try
            {
                string devId = VirtualDisplayManager.GetVirtualDisplayDeviceInstanceId();
                using var pEnable = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/enable-device \"{devId}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });
                pEnable?.WaitForExit(3000);
            }
            catch { }

            try
            {
                using var p2 = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "DisplaySwitch.exe",
                    Arguments = "/extend",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });
                p2?.WaitForExit(3000);
                System.Threading.Thread.Sleep(1500);
            }
            catch { }

            vHandle = VirtualDisplayManager.GetVirtualDisplayMonitorHandle();
            if (vHandle != IntPtr.Zero && vHandle != _selectedMonitor)
            {
                SwitchMonitor(vHandle, "Virtual Display");
                return;
            }
        }

        // If we get here, no virtual display was found
        // Don't reset _selectedMonitor if it's already pointing to a real monitor
        if (_selectedMonitor == IntPtr.Zero)
        {
            _selectedMonitorLabel = "None";
        }
    }


    // ── USB mode ───────────────────────────────────────────────────────

    internal void EnableUsbMode()
    {
        var result = _adbForwarder.SetupForwards();
        if (result.Success)
        {
            _usbMode = true;
            _trayIcon.ShowBalloonTip(3000, "FOLD by @idham.dev",
                "ADB forwarding active.\nOpen the app on your tablet and tap \"USB Mode\".",
                ToolTipIcon.None);
            UpdateTray();
            _window.RefreshStatus();
        }
        else
        {
            MessageBox.Show(
                $"ADB forwarding failed:\n\n{result.Error}\n\n" +
                "Make sure:\n" +
                "• adb.exe is on your PATH\n" +
                "• USB Debugging is enabled on the tablet\n" +
                "• The cable is plugged in",
                "USB Mode Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Called by MainWindow's USB button.</summary>
    public void ToggleUsbMode(MainWindow _)
    {
        if (_usbMode)
        {
            _adbForwarder.RemoveForwards();
            _usbMode = false;
            UpdateTray();
            _window.RefreshStatus();
        }
        else
        {
            EnableUsbMode();
        }
    }

    // ── Virtual display ────────────────────────────────────────────────

    public void InstallVirtualDisplay()
    {
        if (VirtualDisplayManager.IsVirtualDriverInstalled())
        {
            var answer = MessageBox.Show(
                "Virtual Display Driver is already installed.\n\n" +
                "Would you like to reinstall it?",
                "FOLD — Virtual Display",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
        }
        else
        {
            var answer = MessageBox.Show(
                "This will download and install the Virtual Display Driver.\n" +
                "Windows will ask for Administrator permission.\n\n" +
                "Everything will be set up automatically — no manual steps needed!\n\n" +
                "Continue?",
                "FOLD — Install Virtual Display Driver",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;
        }

        VirtualDisplayInstaller.InstallAsync(_window);
    }

    // ── Tray menu ──────────────────────────────────────────────────────

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("🖥  Open FOLD");
        openItem.Click += (_, _) => ShowWindow();

        var ipItem = new ToolStripMenuItem("📋 Copy PC IP");
        ipItem.Click += (_, _) =>
        {
            var ip = GetLocalIp();
            Clipboard.SetText(ip);
            _trayIcon.ShowBalloonTip(2000, "FOLD by @idham.dev", $"IP copied: {ip}", ToolTipIcon.None);
        };

        var quitItem = new ToolStripMenuItem("✖ Quit");
        quitItem.Click += (_, _) => Shutdown();

        menu.Items.AddRange(new ToolStripItem[] { openItem, ipItem, new ToolStripSeparator(), quitItem });
        return menu;
    }

    // ── Shutdown ───────────────────────────────────────────────────────

    /// <summary>Cleanly stops all services, hides the tray icon, and exits the application.</summary>
    public void Shutdown()
    {
        StopStreaming();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    // ── Window show/restore ────────────────────────────────────────────

    public void ShowWindow()
    {
        _window.Show();
        _window.WindowState = FormWindowState.Normal;
        _window.BringToFront();
        _window.Activate();
        _window.RefreshStatus();
    }

    public void ShowBalloon(string message)
    {
        _trayIcon.ShowBalloonTip(2000, "FOLD by @idham.dev", message, ToolTipIcon.None);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private Icon? _lastDynIcon;

    private void UpdateTray()
    {
        var mon = _selectedMonitorLabel.Length > 10
            ? _selectedMonitorLabel[..10] + "…"
            : _selectedMonitorLabel;
        _trayIcon.Text = _running
            ? $"FOLD by @idham.dev {(_usbMode ? "⚡USB" : "📶WiFi")} [{mon}] — {GetLocalIp()}"
            : "FOLD by @idham.dev (stopped)";

        // Swap icon to show status dot
        _lastDynIcon?.Dispose();
        _lastDynIcon = BuildStatusIcon(_running);
        _trayIcon.Icon = _lastDynIcon;
    }

    /// <summary>Renders the base icon with a small green or red dot on the top-right corner.</summary>
    private static Icon BuildStatusIcon(bool online)
    {
        const int sz = 32;
        using var bmp = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Draw base icon
        using var baseIcon = LoadIcon();
        using var baseBmp  = baseIcon.ToBitmap();
        g.DrawImage(baseBmp, 0, 0, sz, sz);

        // Dot position: top-right corner
        int dotSz  = 11;
        int dotX   = sz - dotSz - 1;
        int dotY   = 1;

        // Shadow ring
        using var shadow = new System.Drawing.SolidBrush(Color.FromArgb(120, 0, 0, 0));
        g.FillEllipse(shadow, dotX - 1, dotY - 1, dotSz + 2, dotSz + 2);

        // Dot fill
        Color dotColor = online ? Color.FromArgb(0, 220, 130) : Color.FromArgb(230, 50, 50);
        using var dotBr = new System.Drawing.SolidBrush(dotColor);
        g.FillEllipse(dotBr, dotX, dotY, dotSz, dotSz);

        // Dot highlight (small white gloss top-left)
        using var gloss = new System.Drawing.SolidBrush(Color.FromArgb(120, 255, 255, 255));
        g.FillEllipse(gloss, dotX + 2, dotY + 1, dotSz / 2, dotSz / 2);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public static string GetLocalIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }

    public static Icon LoadIcon()
    {
        var stream = typeof(TrayApp).Assembly
            .GetManifestResourceStream("FOLD.Resources.tray_icon.ico");
        if (stream != null) return new Icon(stream);
        return GenerateFallbackIcon();
    }

    private static Icon GenerateFallbackIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(99, 102, 241));
        g.DrawString("P", new Font("Arial", 7, FontStyle.Bold), Brushes.White, 2, 1);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void SetMonitor(IntPtr handle, string label)
    {
        _selectedMonitor = handle;
        _selectedMonitorLabel = label;
        FOLD.Input.TouchInjector.SelectedMonitorHandle = handle;
    }

    public void SetResolutionIndex(int index)
    {
        lock (ResolutionOptions)
        {
            if (index >= 0 && index < ResolutionOptions.Count)
            {
                _selectedResIndex = index;
                var opt = ResolutionOptions[index];
                _forceWidth = opt.Width;
                _forceHeight = opt.Height;
                _forceFps = opt.Fps;
            }
        }
    }

    public void RegisterDeviceResolution(int width, int height, int fps)
    {
        if (width <= 0 || height <= 0) return;

        bool exists = false;
        lock (ResolutionOptions)
        {
            foreach (var opt in ResolutionOptions)
            {
                if (opt.Width == width && opt.Height == height)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                string label;
                if (width == 2560 && height == 1600)
                    label = "2.5K Quad HD (2560x1600 @ 60 FPS)";
                else if (width == 3840 && height == 2160)
                    label = "4K Ultra HD (3840x2160 @ 60 FPS)";
                else
                    label = $"Device Display ({width}x{height} @ {fps} FPS)";

                ResolutionOptions.Add(new ResolutionOption(label, width, height, fps));

                if (_window != null && !_window.IsDisposed)
                {
                    try
                    {
                        _window.BeginInvoke(new Action(() =>
                        {
                            _window.UpdateResolutionDropdown();
                        }));
                    }
                    catch { }
                }
            }
        }
    }

    public void UpdateResolutionSelection(int index)
    {
        SetResolutionIndex(index);

        // If streaming is running, let's restart the stream to apply the new resolution!
        if (_running)
        {
            bool wasUsb = _usbMode;
            StopStreaming();
            StartStreaming();
            if (wasUsb) EnableUsbMode();
        }
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _h264Server.Dispose();
        _touchReceiver.Dispose();
        _adbForwarder.Dispose();
        _capture?.Dispose();
        _window.Dispose();
    }
}

public class ResolutionOption
{
    public string Label { get; }
    public int Width { get; }
    public int Height { get; }
    public int Fps { get; }

    public ResolutionOption(string label, int width, int height, int fps)
    {
        Label = label;
        Width = width;
        Height = height;
        Fps = fps;
    }

    public override string ToString() => Label;
}
