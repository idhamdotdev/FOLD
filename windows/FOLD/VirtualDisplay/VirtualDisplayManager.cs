using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FOLD.VirtualDisplay;

/// <summary>
/// Enumerates all attached monitors (real + virtual) and provides
/// helpers to install / uninstall the virtual display driver.
/// </summary>
public static class VirtualDisplayManager
{
    // ── Monitor enumeration ────────────────────────────────────────────────

    public record MonitorInfo(IntPtr Handle, int Index, int X, int Y, int Width, int Height, bool IsPrimary, bool IsVirtual)
    {
        public string Label
        {
            get
            {
                if (IsVirtual)
                    return $"Virtual Display — {Width}×{Height} (Extended)";
                if (IsPrimary)
                    return $"Monitor {Index + 1} — {Width}×{Height} (Primary)";
                return $"Monitor {Index + 1} — {Width}×{Height}";
            }
        }
    }

    public static bool IsMonitorVirtual(string szDevice)
    {
        try
        {
            var dev = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            uint i = 0;
            while (NativeMethods.EnumDisplayDevices(null, i++, ref dev, 0))
            {
                if (string.Equals(dev.DeviceName, szDevice, StringComparison.OrdinalIgnoreCase))
                {
                    if (dev.DeviceID != null && (
                        dev.DeviceID.Contains("MttVDD", StringComparison.OrdinalIgnoreCase) ||
                        dev.DeviceID.Contains("MTT1337", StringComparison.OrdinalIgnoreCase) ||
                        dev.DeviceID.Contains("ROOT\\DISPLAY", StringComparison.OrdinalIgnoreCase) ||
                        dev.DeviceString.Contains("Virtual Display Driver", StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        try
        {
            var dev = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            if (NativeMethods.EnumDisplayDevices(szDevice, 0, ref dev, 0))
            {
                if (dev.DeviceID != null && (
                    dev.DeviceID.Contains("MttVDD", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceID.Contains("MTT1337", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceString.Contains("Virtual Display Driver", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    public static List<MonitorInfo> GetAllMonitors()
    {
        var list  = new List<MonitorInfo>();
        int index = 0;

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT rect, IntPtr dwData) =>
            {
                var info = new NativeMethods.MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    bool isPrimary = (info.dwFlags & 0x1) != 0;
                    bool isVirtual = IsMonitorVirtual(info.szDevice);

                    var  rc        = info.rcMonitor;
                    list.Add(new MonitorInfo(hMonitor, index++,
                        rc.left, rc.top,
                        rc.right  - rc.left,
                        rc.bottom - rc.top,
                        isPrimary,
                        isVirtual));
                }
                return true;
            }, IntPtr.Zero);

        return list;
    }

    public static bool IsVirtualDriverInstalled()
    {
        try
        {
            var dev = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            uint i = 0;
            while (NativeMethods.EnumDisplayDevices(null, i++, ref dev, 0))
            {
                if (dev.DeviceID != null && (
                    dev.DeviceID.Contains("MttVDD", StringComparison.OrdinalIgnoreCase) || 
                    dev.DeviceID.Contains("MTT1337", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceID.Contains("ROOT\\DISPLAY", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceString.Contains("Virtual Display Driver", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    public static string GetVirtualDisplayDeviceInstanceId()
    {
        try
        {
            var dev = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            uint i = 0;
            while (NativeMethods.EnumDisplayDevices(null, i++, ref dev, 0))
            {
                if (dev.DeviceID != null && (
                    dev.DeviceID.Contains("MttVDD", StringComparison.OrdinalIgnoreCase) || 
                    dev.DeviceID.Contains("MTT1337", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceID.Contains("ROOT\\DISPLAY", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceID.Contains("LCI\\IDDCX", StringComparison.OrdinalIgnoreCase) ||
                    dev.DeviceString.Contains("Virtual Display Driver", StringComparison.OrdinalIgnoreCase)))
                {
                    return dev.DeviceID;
                }
            }
        }
        catch { }
        return "ROOT\\DISPLAY\\0000"; // Fallback
    }

    public static IntPtr GetVirtualDisplayMonitorHandle()
    {
        IntPtr foundHandle = IntPtr.Zero;
        try
        {
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT rect, IntPtr dwData) =>
                {
                    var info = new NativeMethods.MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                    if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                    {
                        if (IsMonitorVirtual(info.szDevice))
                        {
                            foundHandle = hMonitor;
                            return false; // stop enum
                        }
                    }
                    return true;
                }, IntPtr.Zero);
        }
        catch { }
        return foundHandle;
    }

    /// <summary>
    /// Dynamically sets the resolution and refresh rate of a given monitor handle.
    /// </summary>
    public static bool SetMonitorResolution(IntPtr hMonitor, int width, int height, int refreshRate)
    {
        try
        {
            var info = new NativeMethods.MONITORINFOEX();
            info.cbSize = (uint)Marshal.SizeOf(info);
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
                return false;

            string deviceName = info.szDevice;

            NativeMethods.DEVMODE bestDevMode = default;
            bool found = false;

            NativeMethods.DEVMODE tempDevMode = new NativeMethods.DEVMODE();
            tempDevMode.dmSize = (ushort)Marshal.SizeOf(tempDevMode);
            
            int modeNum = 0;
            while (NativeMethods.EnumDisplaySettings(deviceName, modeNum, ref tempDevMode))
            {
                if (tempDevMode.dmPelsWidth == width && tempDevMode.dmPelsHeight == height)
                {
                    if (!found || Math.Abs((int)tempDevMode.dmDisplayFrequency - refreshRate) < Math.Abs((int)bestDevMode.dmDisplayFrequency - refreshRate))
                    {
                        bestDevMode = tempDevMode;
                        found = true;
                    }
                }
                modeNum++;
            }

            if (!found)
            {
                // Fallback: match closest resolution
                modeNum = 0;
                while (NativeMethods.EnumDisplaySettings(deviceName, modeNum, ref tempDevMode))
                {
                    if (!found || 
                        (Math.Abs((int)tempDevMode.dmPelsWidth - width) + Math.Abs((int)tempDevMode.dmPelsHeight - height) <
                         Math.Abs((int)bestDevMode.dmPelsWidth - width) + Math.Abs((int)bestDevMode.dmPelsHeight - height)))
                    {
                        bestDevMode = tempDevMode;
                        found = true;
                    }
                    modeNum++;
                }
            }

            if (found)
            {
                int currentX = info.rcMonitor.left;
                int targetX = currentX;
                int targetY = 0; // Force vertical alignment to Y=0 to keep monitors touching

                if (currentX < 0)
                {
                    targetX = -width;
                }
                else if (currentX > 0)
                {
                    var primary = System.Windows.Forms.Screen.PrimaryScreen;
                    if (primary != null)
                    {
                        targetX = primary.Bounds.Width;
                    }
                }

                bestDevMode.dmFields = NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY | NativeMethods.DM_POSITION;
                bestDevMode.dmPelsWidth = (uint)width;
                bestDevMode.dmPelsHeight = (uint)height;
                bestDevMode.dmDisplayFrequency = (uint)refreshRate;
                bestDevMode.dmPositionX = targetX;
                bestDevMode.dmPositionY = targetY;

                int result = NativeMethods.ChangeDisplaySettingsEx(deviceName, ref bestDevMode, IntPtr.Zero, NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_RESET, IntPtr.Zero);
                return result == NativeMethods.DISP_CHANGE_SUCCESSFUL;
            }
        }
        catch { }
        return false;
    }

    // ── Driver installer ───────────────────────────────────────────────────

    /// <summary>
    /// Launches the PowerShell installer script as Administrator.
    /// Returns immediately; the PS window handles progress/errors.
    /// </summary>
    public static void LaunchInstaller()
    {
        // Locate the script relative to the executable
        var exeDir     = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(exeDir, "install_virtual_display.ps1");

        // If not found beside the .exe, try the repo tools folder
        if (!File.Exists(scriptPath))
        {
            var repoTools = Path.GetFullPath(
                Path.Combine(exeDir, @"..\..\..\..\..\..\tools\install_virtual_display.ps1"));
            if (File.Exists(repoTools)) scriptPath = repoTools;
        }

        if (!File.Exists(scriptPath))
        {
            System.Windows.Forms.MessageBox.Show(
                "Cannot find install_virtual_display.ps1.\n\n" +
                "Please run it manually from the 'tools' folder.",
                "FOLD", System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
            Verb            = "runas",      // elevation prompt
            UseShellExecute = true
        });
    }

    // ── Native helpers ─────────────────────────────────────────────────────

    private static class NativeMethods
    {
        public delegate bool MonitorEnumProc(
            IntPtr hMonitor, IntPtr hdcMonitor,
            ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr lprcClip,
            MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(
            IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmNup;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        public const int DM_PELSWIDTH = 0x00080000;
        public const int DM_PELSHEIGHT = 0x00100000;
        public const int DM_DISPLAYFREQUENCY = 0x00400000;
        public const int DM_POSITION = 0x00000020;

        public const int CDS_UPDATEREGISTRY = 0x01;
        public const int CDS_RESET = 0x40000000;
        public const int DISP_CHANGE_SUCCESSFUL = 0;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ChangeDisplaySettingsEx(
            string? lpszDeviceName,
            ref DEVMODE lpDevMode,
            IntPtr hwnd,
            uint dwflags,
            IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplaySettings(
            string? lpszDeviceName,
            int iModeNum,
            ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevices(
            string? lpDevice,
            uint iDevNum,
            ref DISPLAY_DEVICE lpDisplayDevice,
            uint dwFlags);
    }
}
