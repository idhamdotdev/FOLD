using System;
using System.Diagnostics;
using System.IO;

namespace FOLD.Usb;

/// <summary>
/// Manages ADB reverse port forwarding for zero-latency USB mode.
///
/// What it does:
///   adb reverse tcp:8765 tcp:8765   →  tablet:localhost:8765 → PC:8765
///   adb reverse tcp:8766 tcp:8766   →  tablet:localhost:8766 → PC:8766
///
/// The Android app then connects to "127.0.0.1" instead of the PC's Wi-Fi IP.
/// The HTTP server on Windows requires no changes at all.
/// </summary>
public sealed class AdbForwarder : IDisposable
{
    private readonly int _streamPort;
    private readonly int _touchPort;

    // Search order: app folder first, relative tools directories, known SDK paths, then PATH
    private static readonly string[] AdbSearchPaths =
    [
        Path.Combine(AppContext.BaseDirectory, "adb.exe"),
        Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe"),
        Path.Combine(AppContext.BaseDirectory, "..", "tools", "adb.exe"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "tools", "adb.exe"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tools", "adb.exe"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "adb.exe"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "adb.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"),
        "adb"    // falls back to PATH
    ];

    public AdbForwarder(int streamPort, int touchPort)
    {
        _streamPort = streamPort;
        _touchPort  = touchPort;
    }

    public record AdbResult(bool Success, string Output, string Error);

    /// <summary>
    /// Runs `adb reverse` for both ports.
    /// Returns success/failure + stdout/stderr for user-facing error messages.
    /// </summary>
    public AdbResult SetupForwards()
    {
        // 1. Verify a device is connected
        var devResult = RunAdb("devices");
        if (!devResult.Success)
            return devResult with { Error = $"adb not found or not on PATH.\n\n{devResult.Error}" };

        if (!devResult.Output.Contains("device", StringComparison.OrdinalIgnoreCase) ||
            devResult.Output.Trim() == "List of devices attached")
            return new AdbResult(false, devResult.Output, "No Android device detected. Is USB Debugging enabled?");

        // 2. Forward stream port
        var r1 = RunAdb($"reverse tcp:{_streamPort} tcp:{_streamPort}");
        if (!r1.Success) return r1;

        // 3. Forward touch port
        var r2 = RunAdb($"reverse tcp:{_touchPort} tcp:{_touchPort}");
        return r2;
    }

    /// <summary>Removes all reverse forwards — call on stop or quit.</summary>
    public void RemoveForwards()
    {
        RunAdb("reverse --remove-all");
    }

    private static AdbResult RunAdb(string arguments)
    {
        var adbExe = FindAdb();
        if (adbExe is null)
            return new AdbResult(false, "", "adb.exe not found. Install Android SDK Platform-Tools or place adb.exe next to FOLD.exe");

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = adbExe,
                    Arguments              = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };
            proc.Start();
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(10_000);   // 10-second timeout

            bool ok = proc.ExitCode == 0 || stdout.Contains("5037");
            return new AdbResult(ok, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new AdbResult(false, "", ex.Message);
        }
    }

    private static string? FindAdb()
    {
        foreach (var path in AdbSearchPaths)
        {
            if (File.Exists(path))   return path;
            // Try PATH resolution for bare "adb"
            if (!path.Contains('\\') && !path.Contains('/'))
            {
                var inPath = FindInPath(path);
                if (inPath != null) return inPath;
            }
        }
        return null;
    }

    private static string? FindInPath(string exe)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir.Trim(), exe);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    public void Dispose() => RemoveForwards();
}
