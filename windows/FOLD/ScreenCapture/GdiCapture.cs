using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FOLD.VirtualDisplay;

namespace FOLD.ScreenCapture;

/// <summary>
/// Reliable GDI fallback — works on every Windows 10+ machine.
/// Uses CPU BitBlt. Sufficient for 30 fps at 1080p on any modern CPU.
/// Optional: scale the result before returning to reduce bandwidth.
/// </summary>
public sealed class GdiCapture : IScreenCapture
{
    private readonly float _scale;
    private readonly int _x, _y;
    private readonly int _width, _height;

    /// <param name="scale">Optional downscale factor, e.g. 0.75 reduces bandwidth by ~44%.</param>
    public GdiCapture(IntPtr monitorHandle = default, float scale = 1.0f)
    {
        _scale = scale;

        if (monitorHandle == IntPtr.Zero)
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            _x = bounds.X;
            _y = bounds.Y;
            _width = bounds.Width;
            _height = bounds.Height;
        }
        else
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            _x = bounds.X; _y = bounds.Y; _width = bounds.Width; _height = bounds.Height;
            
            // Find matching screen bounds
            var monitors = VirtualDisplayManager.GetAllMonitors();
            foreach (var m in monitors)
            {
                if (m.Handle == monitorHandle)
                {
                    _x = m.X;
                    _y = m.Y;
                    _width = m.Width;
                    _height = m.Height;
                    break;
                }
            }
        }
    }

    public int ScreenWidth  => _width;
    public int ScreenHeight => _height;

    public Bitmap CaptureFrame()
    {
        // Full-resolution capture
        var bmp = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(_x, _y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            DrawCursor(g, _x, _y);
        }

        // Optional downscale
        if (_scale >= 1.0f) return bmp;

        int dstW = (int)(_width * _scale);
        int dstH = (int)(_height * _scale);
        var scaled = new Bitmap(bmp, dstW, dstH);
        bmp.Dispose();
        return scaled;
    }

    private static void DrawCursor(Graphics g, int originX, int originY)
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci)) return;
        if (ci.flags != CURSOR_SHOWING) return;

        var cursor = new Cursor(ci.hCursor);
        int cx = ci.ptScreenPos.X - originX - cursor.HotSpot.X;
        int cy = ci.ptScreenPos.Y - originY - cursor.HotSpot.Y;
        cursor.Draw(g, new Rectangle(cx, cy, cursor.Size.Width, cursor.Size.Height));
    }

    public void Dispose() { }

    // ── Native cursor APIs ────────────────────────────────────────────────────
    private const int CURSOR_SHOWING = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int    cbSize;
        public int    flags;
        public IntPtr hCursor;
        public POINT  ptScreenPos;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);
}

