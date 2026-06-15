using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FOLD.Models;

namespace FOLD.Input;

/// <summary>
/// Translates normalized tablet touch events into Windows mouse input via SendInput.
/// PointerID 0 → left button actions.
/// Multi-touch (PointerID > 0) → mouse move only (no additional buttons mapped yet).
/// </summary>
public static class TouchInjector
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public static IntPtr SelectedMonitorHandle { get; set; } = IntPtr.Zero;

    public static void InjectTouch(TouchEvent evt)
    {
        // 1. Get all monitors
        var monitors = FOLD.VirtualDisplay.VirtualDisplayManager.GetAllMonitors();
        
        // 2. Find the selected monitor or default to the primary one
        var target = monitors.Find(m => m.Handle == SelectedMonitorHandle);
        if (target == null)
        {
            // Fallback to primary monitor
            target = monitors.Find(m => m.IsPrimary) ?? (monitors.Count > 0 ? monitors[0] : null);
        }

        int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        if (target == null)
        {
            // Absolute fallback: use PrimaryScreen Bounds (retains original single-monitor logic)
            int absX = (int)(evt.NormX * 65535);
            int absY = (int)(evt.NormY * 65535);
            absX = Math.Clamp(absX, 0, 65535);
            absY = Math.Clamp(absY, 0, 65535);
            
            bool isPrimary = evt.PointerId == 0;
            var mouseFlags = evt.Type switch
            {
                "down" when isPrimary => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.LeftDown,
                "move"                => MouseEventFlags.Move | MouseEventFlags.Absolute,
                "up"   when isPrimary => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.LeftUp,
                _                     => MouseEventFlags.Move | MouseEventFlags.Absolute
            };

            try
            {
                string fallbackLog = $"[{DateTime.Now:HH:mm:ss.fff}] FALLBACK: Type={evt.Type}, NormX={evt.NormX:F4}, NormY={evt.NormY:F4}, AbsX={absX}, AbsY={absY}, Flags={mouseFlags}\n";
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "touch_debug.txt"), fallbackLog);
            }
            catch { }

            SendMouseInput(absX, absY, mouseFlags);
            return;
        }

        // 3. Map normalized coordinate (0.0 to 1.0) on the target monitor to virtual desktop pixels
        double pixelX = target.X + (evt.NormX * target.Width);
        double pixelY = target.Y + (evt.NormY * target.Height);

        // 4. Map virtual desktop pixels to absolute SendInput coordinate space (0 to 65535)
        int absVirtualX = (int)(((pixelX - virtualLeft) / (double)virtualWidth) * 65535.0);
        int absVirtualY = (int)(((pixelY - virtualTop) / (double)virtualHeight) * 65535.0);

        absVirtualX = Math.Clamp(absVirtualX, 0, 65535);
        absVirtualY = Math.Clamp(absVirtualY, 0, 65535);

        bool isPrimaryBtn = evt.PointerId == 0;

        // Note: we must include MouseEventFlags.VirtualDesk (0x4000) so Windows maps coordinates to the entire virtual desktop!
        var flags = evt.Type switch
        {
            "down" when isPrimaryBtn => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.VirtualDesk | MouseEventFlags.LeftDown,
            "move"                   => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.VirtualDesk,
            "up"   when isPrimaryBtn => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.VirtualDesk | MouseEventFlags.LeftUp,
            _                        => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.VirtualDesk
        };

        try
        {
            string debugLine = $"[{DateTime.Now:HH:mm:ss.fff}] Touch: Type={evt.Type}, NormX={evt.NormX:F4}, NormY={evt.NormY:F4}, SelHandle={SelectedMonitorHandle}, Target={target.Label}\n" +
                               $"  TargetBounds: X={target.X}, Y={target.Y}, W={target.Width}, H={target.Height}\n" +
                               $"  VirtualScreen: L={virtualLeft}, T={virtualTop}, W={virtualWidth}, H={virtualHeight}\n" +
                               $"  Calculated Abs: X={absVirtualX}, Y={absVirtualY}, Flags={flags}\n";
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "touch_debug.txt"), debugLine);
        }
        catch { }

        SendMouseInput(absVirtualX, absVirtualY, flags);
    }

    // ── Win32 plumbing ────────────────────────────────────────────────────────

    private static void SendMouseInput(int x, int y, MouseEventFlags flags)
    {
        var input = new INPUT
        {
            type = InputType.Mouse,
            U    = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx         = x,
                    dy         = y,
                    mouseData  = 0,
                    dwFlags    = (uint)flags,
                    time       = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        NativeMethods.SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    [Flags]
    private enum MouseEventFlags : uint
    {
        Move        = 0x0001,
        LeftDown    = 0x0002,
        LeftUp      = 0x0004,
        VirtualDesk = 0x4000,
        Absolute    = 0x8000
    }

    private enum InputType : int
    {
        Mouse    = 0,
        Keyboard = 1,
        Hardware = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public InputType  type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT    mi;
        [FieldOffset(0)] public KEYBDINPUT    ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int    dx, dy;
        public uint   mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint   dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint  uMsg;
        public ushort wParamL, wParamH;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);
    }
}
