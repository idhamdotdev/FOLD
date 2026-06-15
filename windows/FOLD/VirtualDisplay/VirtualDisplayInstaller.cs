using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FOLD.VirtualDisplay;

/// <summary>
/// Downloads and installs the Virtual Display Driver using the official setup executable.
/// The driver is cached in the app's local data folder so it only needs to be downloaded once.
/// After installation, the display is auto-extended and the virtual monitor is auto-detected.
/// </summary>
public static class VirtualDisplayInstaller
{
    private const string DOWNLOAD_URL =
        "https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/download/25.5.2/Virtual.Display.Driver-v25.05.03-setup-x64.exe";

    /// <summary>
    /// Local cache directory inside the FOLD app data folder.
    /// </summary>
    private static string CacheDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FOLD", "drivers");

    private static string CachedSetupPath =>
        Path.Combine(CacheDir, "vdd_setup.exe");

    /// <summary>
    /// Checks if the driver setup is already cached locally.
    /// </summary>
    public static bool IsSetupCached =>
        File.Exists(CachedSetupPath) && new FileInfo(CachedSetupPath).Length > 500_000; // sanity check > 500KB

    /// <summary>
    /// Full install flow: download (if needed) → install → configure → extend → detect.
    /// Shows a progress dialog and handles everything in one click.
    /// </summary>
    public static async void InstallAsync(MainWindow window)
    {
        // ── Progress dialog ─────────────────────────────────────────────
        var progress = new Form
        {
            Text            = "FOLD — Installing Virtual Display Driver",
            Width           = 520,
            Height          = 180,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterScreen,
            MaximizeBox     = false,
            MinimizeBox     = false,
            ControlBox      = false,
            TopMost         = true,
            BackColor       = System.Drawing.Color.FromArgb(17, 27, 54)
        };

        var lbl = new Label
        {
            Text      = "Preparing...",
            AutoSize  = false,
            Dock      = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font      = new System.Drawing.Font("Segoe UI", 11),
            ForeColor = System.Drawing.Color.White
        };

        var progressBar = new ProgressBar
        {
            Dock   = DockStyle.Bottom,
            Height = 8,
            Style  = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };

        progress.Controls.Add(lbl);
        progress.Controls.Add(progressBar);
        progress.Show();
        progress.Refresh();

        try
        {
            // ── Step 1: Extract (or use cache) ──────────────────────────
            if (!IsSetupCached)
            {
                lbl.Text = "📦  Extracting Virtual Display Driver installer...";
                progress.Refresh();

                await Task.Run(() =>
                {
                    try
                    {
                        Directory.CreateDirectory(CacheDir);
                        var asm = System.Reflection.Assembly.GetExecutingAssembly();
                        using var stream = asm.GetManifestResourceStream("FOLD.Resources.vdd_setup.exe");
                        if (stream == null)
                        {
                            throw new Exception("Embedded vdd_setup.exe resource not found.");
                        }

                        var tempPath = CachedSetupPath + ".tmp";
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            stream.CopyTo(fs);
                        }

                        if (File.Exists(CachedSetupPath))
                            File.Delete(CachedSetupPath);
                        File.Move(tempPath, CachedSetupPath);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to extract driver setup: {ex.Message}", ex);
                    }
                });
            }
            else
            {
                lbl.Text = "✓  Driver installer already cached.";
                progress.Refresh();
                await Task.Delay(500);
            }

            // ── Step 2: Install (silent, elevated) ───────────────────────
            lbl.Text = "⚙  Installing driver (approve the admin prompt)...";
            progress.Refresh();

            var success = await Task.Run(() => RunSetup(CachedSetupPath));
            if (!success)
            {
                progress.Close();
                ShowError(
                    "The driver installer failed or was cancelled.\n\n" +
                    "Please try again, or run the installer manually from:\n" +
                    CachedSetupPath);
                return;
            }

            // ── Step 3: Write VDD config ─────────────────────────────────
            lbl.Text = "📝  Writing display configuration...";
            progress.Refresh();
            await Task.Run(WriteVddConfig);

            // ── Step 4: Auto-extend the display ──────────────────────────
            lbl.Text = "🖥  Extending display...";
            progress.Refresh();

            // Enable the device node in case it starts disabled (Code 22)
            await Task.Run(() =>
            {
                try
                {
                    using var pEnable = Process.Start(new ProcessStartInfo
                    {
                        FileName    = "pnputil.exe",
                        Arguments   = "/enable-device \"ROOT\\DISPLAY\\0000\"",
                        UseShellExecute = true,
                        Verb        = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    pEnable?.WaitForExit(5000);
                }
                catch { }
            });

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "DisplaySwitch.exe",
                    Arguments       = "/extend",
                    UseShellExecute = true,
                    WindowStyle     = ProcessWindowStyle.Hidden
                });
            }
            catch { }
            await Task.Delay(2000); // Give Windows time to enumerate the new display

            // ── Step 5: Auto-detect the virtual monitor ──────────────────
            lbl.Text = "🔍  Detecting virtual monitor...";
            progress.Refresh();

            // Retry detection up to 5 times (the display adapter may need a moment)
            bool detected = false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                window.App.AutoDetectVirtualMonitor();
                if (window.App.SelectedMonitor != IntPtr.Zero)
                {
                    detected = true;
                    break;
                }
                await Task.Delay(1000);
            }

            progress.Close();

            if (detected)
            {
                // Auto-start streaming if not already running
                if (!window.App.IsRunning)
                {
                    window.App.ToggleStreaming(window);
                }

                MessageBox.Show(
                    "✅ Virtual Display Driver installed successfully!\n\n" +
                    "The virtual monitor has been detected and FOLD is now streaming.\n" +
                    "Connect your Android device via WiFi or USB to start using it.",
                    "FOLD — Setup Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    "✅ Virtual Display Driver installed successfully!\n\n" +
                    "However, the virtual monitor was not detected automatically.\n\n" +
                    "Please try:\n" +
                    "  1. Press  Win + P  and choose  'Extend'\n" +
                    "  2. Open Windows Settings → Display → Extend these displays\n" +
                    "  3. Click START in FOLD\n\n" +
                    "If issues persist, try restarting your PC.",
                    "FOLD — Almost There",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            window.RefreshStatus();
        }
        catch (Exception ex)
        {
            try { progress.Close(); } catch { }
            ShowError($"Installation failed:\n\n{ex.Message}\n\n{ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Uninstalls the virtual display driver using the same setup.exe /uninstall flag.
    /// </summary>
    public static async Task<bool> UninstallAsync()
    {
        if (!IsSetupCached) return false;

        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName        = CachedSetupPath,
                    Arguments       = "/VERYSILENT /NORESTART /uninstall",
                    UseShellExecute = true,
                    Verb            = "runas"
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit(60_000);
                return proc.ExitCode == 0;
            }
            catch { return false; }
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs the VDD setup.exe with silent flags and elevated privileges.
    /// </summary>
    private static bool RunSetup(string exePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName        = exePath,
            Arguments       = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES",
            UseShellExecute = true,
            Verb            = "runas"
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(120_000); // 2-minute timeout
            // Exit code 0 = success, 3010 = success but reboot recommended
            return process.ExitCode == 0 || process.ExitCode == 3010;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the VDD configuration file that controls the available resolutions
    /// and the number of virtual monitors to create.
    /// </summary>
    private static void WriteVddConfig()
    {
        // VirtualDrivers/Virtual-Display-Driver uses C:\IddSampleDriver\option.txt
        var dir = @"C:\IddSampleDriver";
        try
        {
            Directory.CreateDirectory(dir);

            // Format: first line = number of monitors
            // Subsequent lines = width, height, refreshRate
            var config =
                "1\n" +
                "1920, 1080, 60\n" +
                "2560, 1440, 60\n" +
                "2560, 1600, 60\n" +
                "3840, 2160, 60\n" +
                "1280, 720, 60\n" +
                "1366, 768, 60\n";

            File.WriteAllText(Path.Combine(dir, "option.txt"), config);
        }
        catch { }
    }

    private static void ShowError(string msg) =>
        MessageBox.Show(msg, "FOLD — Installation Error",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
}
