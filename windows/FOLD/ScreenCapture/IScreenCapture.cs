using System;
using System.Drawing;

namespace FOLD.ScreenCapture;

/// <summary>Abstraction over screen capture backends (GDI, WGC).</summary>
public interface IScreenCapture : IDisposable
{
    /// <summary>Capture primary screen and return a Bitmap. Thread-safe.</summary>
    Bitmap CaptureFrame();

    int ScreenWidth  { get; }
    int ScreenHeight { get; }

    /// <summary>Capture primary screen and copy the raw BGRA pixels directly to the CPU buffer. Thread-safe.</summary>
    bool CaptureToBuffer(IntPtr dstBuffer, out int rowPitch, int timeoutMs);
}
