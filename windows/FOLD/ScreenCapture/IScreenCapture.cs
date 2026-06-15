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
}
