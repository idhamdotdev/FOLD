Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class DisplayChanger {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int ChangeDisplaySettingsEx(
        string lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DISPLAY_DEVICE {
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DEVMODE {
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
    }

    public const int DM_PELSWIDTH = 0x00080000;
    public const int DM_PELSHEIGHT = 0x00100000;
    public const int DM_DISPLAYFREQUENCY = 0x00400000;
    public const int CDS_UPDATEREGISTRY = 0x01;

    public static void ChangeRes(string deviceName, int w, int h) {
        DEVMODE mode = new DEVMODE();
        mode.dmSize = (ushort)Marshal.SizeOf(mode);
        
        bool found = false;
        int modeNum = 0;
        while (EnumDisplaySettings(deviceName, modeNum++, ref mode)) {
            if (mode.dmPelsWidth == w && mode.dmPelsHeight == h) {
                found = true;
                break;
            }
        }
        
        if (!found) {
            Console.WriteLine("Mode " + w + "x" + h + " not found!");
            return;
        }

        mode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
        mode.dmPelsWidth = (uint)w;
        mode.dmPelsHeight = (uint)h;
        mode.dmDisplayFrequency = 60; // 60Hz

        int res = ChangeDisplaySettingsEx(deviceName, ref mode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        Console.WriteLine("ChangeDisplaySettingsEx result: " + res);
    }
}
"@
[DisplayChanger]::ChangeRes("\\.\DISPLAY55", 2560, 1600)
