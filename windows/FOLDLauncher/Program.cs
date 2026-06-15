using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;

namespace FOLDLauncher;

public static class Program
{
    private static readonly string TargetDir = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FOLD", "app");
    
    private static readonly string ExePath = Path.Combine(TargetDir, "FOLD.exe");
    private static readonly string VersionFile = Path.Combine(TargetDir, "launcher_version.txt");

    public static int Main(string[] args)
    {
        // Get the current assembly's last write time as a simple version token
        string currentVersion = GetAssemblyBuildToken();

        try
        {
            if (ShouldExtract(currentVersion))
            {
                ExtractApp();
                File.WriteAllText(VersionFile, currentVersion);
            }
        }
        catch (Exception ex)
        {
            // Fallback: if extraction fails (e.g. app running), try to run whatever is there,
            // or show a dialog box
            Console.WriteLine($"Launcher Warning: Extraction failed. {ex.Message}");
        }

        if (!File.Exists(ExePath))
        {
            ShowError($"FOLD executable not found at:\n{ExePath}\n\nPlease try running the launcher again.");
            return 1;
        }

        return RunApp(args);
    }

    private static string GetAssemblyBuildToken()
    {
        try
        {
            var path = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // Fallback if location is empty (AOT/SingleFile)
                return "1.0.0";
            }
            return File.GetLastWriteTimeUtc(path).Ticks.ToString();
        }
        catch
        {
            return "1.0.0_fallback";
        }
    }

    private static bool ShouldExtract(string currentVersion)
    {
        if (!Directory.Exists(TargetDir) || !File.Exists(ExePath) || !File.Exists(VersionFile))
        {
            return true;
        }

        try
        {
            string installedVersion = File.ReadAllText(VersionFile).Trim();
            return installedVersion != currentVersion;
        }
        catch
        {
            return true;
        }
    }

    private static void ExtractApp()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("FOLDLauncher.FOLD_portable.zip");
        if (stream == null)
        {
            throw new Exception("Embedded FOLD_portable.zip resource not found inside launcher.");
        }

        // Clean target directory safely
        if (Directory.Exists(TargetDir))
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Directory.Delete(TargetDir, true);
                    break;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }
        }

        Directory.CreateDirectory(TargetDir);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        
        // Extract all entries, resolving the "FOLD/" root directory from the zip
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories

            // The zip entries start with "FOLD/"
            string relPath = entry.FullName;
            if (relPath.StartsWith("FOLD/", StringComparison.OrdinalIgnoreCase))
            {
                relPath = relPath.Substring(5);
            }
            else if (relPath.StartsWith("FOLD\\", StringComparison.OrdinalIgnoreCase))
            {
                relPath = relPath.Substring(5);
            }

            string destPath = Path.Combine(TargetDir, relPath);
            string? destDir = Path.GetDirectoryName(destPath);
            if (destDir != null)
            {
                Directory.CreateDirectory(destDir);
            }

            entry.ExtractToFile(destPath, true);
        }
    }

    private static int RunApp(string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExePath,
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            CreateNoWindow = false
        };

        try
        {
            using var proc = Process.Start(startInfo);
            if (proc == null) return 1;
            
            // Wait for exit to return the same exit code
            proc.WaitForExit();
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to start FOLD app:\n{ex.Message}");
            return 1;
        }
    }

    private static void ShowError(string message)
    {
        // Simple console error fallback, or we can use a basic MsgBox if needed
        Console.WriteLine(message);
        try
        {
            // Keep console open if run directly
            if (Environment.UserInteractive)
            {
                Thread.Sleep(3000);
            }
        }
        catch {}
    }
}
