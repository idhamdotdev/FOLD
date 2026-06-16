Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class Display {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE lpDevMode);

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
    
    public static void ShowAll() {
        DISPLAY_DEVICE dev = new DISPLAY_DEVICE();
        dev.cb = Marshal.SizeOf(dev);
        uint id = 0;
        while (EnumDisplayDevices(null, id++, ref dev, 0)) {
            if ((dev.StateFlags & 1) != 0) { // Active
                Console.WriteLine("Display Name: " + dev.DeviceName + " (" + dev.DeviceString + ") ID: " + dev.DeviceID);
                DEVMODE mode = new DEVMODE();
                mode.dmSize = (ushort)Marshal.SizeOf(mode);
                int modeNum = 0;
                System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
                while (EnumDisplaySettings(dev.DeviceName, modeNum++, ref mode)) {
                    string res = mode.dmPelsWidth + "x" + mode.dmPelsHeight + "@" + mode.dmDisplayFrequency + "Hz";
                    if (!seen.Contains(res)) {
                        seen.Add(res);
                        Console.WriteLine("  - " + res);
                    }
                }
            }
        }
    }
}
"@
[Display]::ShowAll()
