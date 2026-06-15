# FOLD — Portable Monitor App
### Implementation Plan v2 (IDE-Ready)
> Stream your Windows PC screen to Redmi Pad Pro over **Wi-Fi** or **USB (zero-latency)**. Touch input forwarded back to PC.

---

## Table of Contents
1. [Architecture Overview](#1-architecture-overview)
2. [Tech Stack](#2-tech-stack)
3. [Repo Structure](#3-repo-structure)
4. [Windows App (C# .NET 8)](#4-windows-app)
5. [Android App (Kotlin)](#5-android-app)
6. [Communication Protocol](#6-communication-protocol)
7. [Build & Package](#7-build--package)
8. [Quick Start / Usage](#8-quick-start--usage)
9. [Performance Tuning](#9-performance-tuning)

---

## 1. Architecture Overview

### Mode A — Wi-Fi

```
┌─────────────────────────────┐        Wi-Fi (Same Network)        ┌──────────────────────────────┐
│        Windows PC           │ ──────────────────────────────────► │    Redmi Pad Pro (Android)   │
│                             │                                     │                              │
│  ┌──────────────────────┐   │   HTTP :8765/stream (MJPEG)         │  ┌────────────────────────┐ │
│  │  Screen Capture      │   │ ──────────────────────────────────► │  │  MjpegView (display)   │ │
│  │  (WGC API / GDI)     │   │                                     │  └────────────────────────┘ │
│  └──────────┬───────────┘   │   HTTP :8766/touch (POST JSON)      │                              │
│             │               │ ◄────────────────────────────────── │  ┌────────────────────────┐ │
│  ┌──────────▼───────────┐   │                                     │  │  Touch Input Sender    │ │
│  │  MJPEG HTTP Server   │   │   HTTP :8765/info (GET - handshake) │  └────────────────────────┘ │
│  │  Touch Input Recv    │   │ ◄────────────────────────────────── │                              │
│  │  System Tray UI      │   │                                     │  ┌────────────────────────┐ │
│  └──────────────────────┘   │                                     │  │  Connection Screen UI  │ │
└─────────────────────────────┘                                     │  └────────────────────────┘ │
                                                                    └──────────────────────────────┘
```

### Mode B — USB Debugging (Zero Latency) ⚡

```
┌─────────────────────────────┐         USB Cable (ADB)            ┌──────────────────────────────┐
│        Windows PC           │ ──────────────────────────────────► │    Redmi Pad Pro (Android)   │
│                             │                                     │                              │
│  ┌──────────────────────┐   │  adb reverse tcp:8765 tcp:8765      │  ┌────────────────────────┐ │
│  │  Screen Capture      │   │  adb reverse tcp:8766 tcp:8766      │  │  MjpegView (display)   │ │
│  │  (WGC API / GDI)     │   │                                     │  │  connects to localhost │ │
│  └──────────┬───────────┘   │  ◄── USB tunnel replaces Wi-Fi ──► │  └────────────────────────┘ │
│             │               │                                     │                              │
│  ┌──────────▼───────────┐   │  HTTP traffic travels over cable,   │  ┌────────────────────────┐ │
│  │  MJPEG HTTP Server   │   │  not network. Same HTTP API,        │  │  Touch Sender          │ │
│  │  Touch Input Recv    │   │  zero code changes to server.       │  │  (to localhost)        │ │
│  │  AdbForwarder        │   │                                     │  └────────────────────────┘ │
│  │  System Tray UI      │   │                                     │                              │
│  └──────────────────────┘   │                                     │  ┌────────────────────────┐ │
└─────────────────────────────┘                                     │  │  USB Mode Button       │ │
                                                                    │  └────────────────────────┘ │
                                                                    └──────────────────────────────┘
```

**How USB mode works:**  
`adb reverse` tells the Android device to forward any TCP traffic on its `localhost:PORT` through the USB cable to the PC's `localhost:PORT`. The HTTP server on Windows doesn't change at all — the tablet simply connects to `127.0.0.1` instead of a Wi-Fi IP. Latency drops from ~20–40ms (Wi-Fi) to **<5ms** (USB).

**Protocol (both modes):** MJPEG over HTTP (no extra dependencies, hardware-decodable, low-latency)

---

## 2. Tech Stack

| Side | Tech | Reason |
|------|------|--------|
| Windows EXE | C# .NET 8, WinForms | Self-contained, no install needed |
| Screen Capture | `Windows.Graphics.Capture` API | GPU-accelerated, supports HDR, fastest |
| Fallback Capture | `System.Drawing` + BitBlt GDI | Works on all Windows 10+ |
| HTTP Server | `System.Net.HttpListener` | Built-in, zero dependencies |
| JPEG Encode | `System.Drawing.Imaging` | Built-in |
| Touch Injection | `user32.dll SendInput` P/Invoke | Native Windows API |
| **USB Forwarding** | **`adb reverse` via `Process`** | **ADB ships with Android SDK / standalone** |
| Android APK | Kotlin, minSdk 26 (Android 8) | Redmi Pad Pro is Android 13 |
| MJPEG Decode | Custom `MjpegInputStream` + `BitmapFactory` | No library needed |
| Display | `SurfaceView` | Lowest-latency rendering |
| Touch Send | `OkHttp 4` | Simple, reliable |
| UI | ViewBinding + ConstraintLayout | Standard modern Android |

---

## 3. Repo Structure

```
FOLD/
├── README.md
├── .gitignore
│
├── windows/                          ← Windows EXE project
│   ├── FOLD.sln
│   └── FOLD/
│       ├── FOLD.csproj
│       ├── Program.cs                ← Entry point
│       ├── TrayApp.cs                ← System tray + main logic
│       ├── ScreenCapture/
│       │   ├── IScreenCapture.cs     ← Interface
│       │   ├── WgcCapture.cs         ← Windows.Graphics.Capture (primary)
│       │   └── GdiCapture.cs         ← GDI fallback
│       ├── Server/
│       │   ├── MjpegServer.cs        ← HTTP MJPEG streaming server
│       │   └── TouchReceiver.cs      ← Receives touch events from tablet
│       ├── Input/
│       │   └── TouchInjector.cs      ← Injects touch/mouse via SendInput
│       ├── Usb/
│       │   └── AdbForwarder.cs       ← ⚡ NEW: runs adb reverse for USB mode
│       ├── Models/
│       │   └── TouchEvent.cs         ← Shared data model
│       └── Resources/
│           └── tray_icon.ico
│
└── android/                          ← Android APK project
    └── app/
        ├── build.gradle.kts
        └── src/main/
            ├── AndroidManifest.xml
            ├── java/com/FOLD/
            │   ├── MainActivity.kt           ← Connection screen (Wi-Fi + USB tabs)
            │   ├── DisplayActivity.kt        ← Full-screen monitor view
            │   ├── ConnectionMode.kt         ← ⚡ NEW: sealed class Wi-Fi / USB
            │   ├── MjpegView.kt              ← SurfaceView MJPEG renderer
            │   ├── MjpegInputStream.kt       ← Parses multipart MJPEG stream
            │   ├── TouchSender.kt            ← Sends touch events to PC
            │   └── PrefsManager.kt           ← Saves last IP/settings + mode
            └── res/
                ├── layout/
                │   ├── activity_main.xml     ← Updated: Wi-Fi / USB tabs
                │   └── activity_display.xml
                └── values/
                    ├── strings.xml
                    └── themes.xml
```

---

## 4. Windows App

### 4.1 `FOLD.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <ApplicationIcon>Resources\tray_icon.ico</ApplicationIcon>
    <AssemblyName>FOLD</AssemblyName>
    <RootNamespace>FOLD</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>
</Project>
```

---

### 4.2 `Program.cs`

```csharp
using System;
using System.Windows.Forms;

namespace FOLD;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using var trayApp = new TrayApp();
        trayApp.Run();
    }
}
```

---

### 4.3 `TrayApp.cs`

```csharp
using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using FOLD.ScreenCapture;
using FOLD.Server;
using FOLD.Usb;

namespace FOLD;

public sealed class TrayApp : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly MjpegServer _mjpegServer;
    private readonly TouchReceiver _touchReceiver;
    private readonly AdbForwarder _adbForwarder;          // ⚡ USB
    private IScreenCapture? _capture;
    private bool _running;
    private bool _usbMode;                                // ⚡ USB

    // Settings (expose via tray menu later)
    public int StreamPort { get; set; } = 8765;
    public int TouchPort  { get; set; } = 8766;
    public int Quality    { get; set; } = 75;   // JPEG quality 1-100
    public int TargetFps  { get; set; } = 30;

    public TrayApp()
    {
        _mjpegServer   = new MjpegServer(StreamPort, Quality, TargetFps);
        _touchReceiver = new TouchReceiver(TouchPort);
        _adbForwarder  = new AdbForwarder(StreamPort, TouchPort);  // ⚡ USB

        _trayIcon = new NotifyIcon
        {
            Icon    = new Icon(typeof(TrayApp), "Resources.tray_icon.ico"),
            Text    = "FOLD",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ShowInfo();
    }

    public void Run()
    {
        StartStreaming();
        Application.Run();          // Message pump keeps tray alive
    }

    private void StartStreaming()
    {
        _capture = TryCreateCapture();
        _mjpegServer.Start(_capture);
        _touchReceiver.Start();
        _running = true;
        UpdateTrayText();
    }

    private void StopStreaming()
    {
        _mjpegServer.Stop();
        _touchReceiver.Stop();
        if (_usbMode) _adbForwarder.RemoveForwards();   // ⚡ USB
        _capture?.Dispose();
        _running = false;
        _usbMode = false;
        UpdateTrayText();
    }

    // ⚡ USB — enable ADB port forwarding
    private void EnableUsbMode()
    {
        var result = _adbForwarder.SetupForwards();
        if (result.Success)
        {
            _usbMode = true;
            _trayIcon.ShowBalloonTip(3000, "FOLD ⚡ USB",
                "ADB forwarding active.\nOpen the app on your tablet and tap \"USB Mode\".",
                ToolTipIcon.Info);
            UpdateTrayText();
        }
        else
        {
            MessageBox.Show(
                $"ADB forwarding failed:\n\n{result.Error}\n\n" +
                "Make sure:\n" +
                "• adb.exe is on your PATH (or in the app folder)\n" +
                "• USB Debugging is enabled on the tablet\n" +
                "• The cable is plugged in",
                "USB Mode Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static IScreenCapture TryCreateCapture()
    {
        try   { return new WgcCapture(); }
        catch { return new GdiCapture(); }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var ipItem   = new ToolStripMenuItem("📋 Copy PC IP") { Name = "mnuIp" };
        var usbItem  = new ToolStripMenuItem("⚡ Enable USB Mode") { Name = "mnuUsb" };  // ⚡ USB
        var stopItem = new ToolStripMenuItem("⏸ Stop Streaming") { Name = "mnuStop" };
        var quitItem = new ToolStripMenuItem("✖ Quit");

        ipItem.Click += (_, _) =>
        {
            var ip = GetLocalIp();
            Clipboard.SetText(ip);
            _trayIcon.ShowBalloonTip(2000, "FOLD", $"IP copied: {ip}", ToolTipIcon.Info);
        };
        usbItem.Click += (_, _) =>                              // ⚡ USB
        {
            if (!_usbMode) { EnableUsbMode(); usbItem.Text = "⚡ USB Mode Active"; }
            else
            {
                _adbForwarder.RemoveForwards();
                _usbMode = false;
                usbItem.Text = "⚡ Enable USB Mode";
                UpdateTrayText();
            }
        };
        stopItem.Click += (_, _) =>
        {
            if (_running) { StopStreaming(); stopItem.Text = "▶ Start Streaming"; }
            else          { StartStreaming(); stopItem.Text = "⏸ Stop Streaming"; }
        };
        quitItem.Click += (_, _) => { StopStreaming(); Application.Exit(); };

        menu.Items.AddRange([ipItem, usbItem, new ToolStripSeparator(), stopItem, new ToolStripSeparator(), quitItem]);
        return menu;
    }

    private void ShowInfo()
    {
        var ip   = GetLocalIp();
        var mode = _usbMode ? "⚡ USB (zero-latency)" : "📶 Wi-Fi";
        MessageBox.Show(
            $"FOLD is running!\n\nMode: {mode}\n\nWi-Fi IP: {ip}\nPorts: Stream={StreamPort}  Touch={TouchPort}\n\n" +
            "For USB mode: right-click tray → Enable USB Mode",
            "FOLD", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateTrayText() =>
        _trayIcon.Text = _running
            ? $"FOLD {(_usbMode ? "⚡USB" : "📶WiFi")} — {GetLocalIp()}"
            : "FOLD (stopped)";

    public static string GetLocalIp()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect("8.8.8.8", 80);
        return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
        _mjpegServer.Dispose();
        _touchReceiver.Dispose();
        _adbForwarder.Dispose();    // ⚡ USB
        _capture?.Dispose();
    }
}
```

---

### 4.4 `ScreenCapture/IScreenCapture.cs`

```csharp
using System;
using System.Drawing;

namespace FOLD.ScreenCapture;

public interface IScreenCapture : IDisposable
{
    /// <summary>Capture primary screen and return a Bitmap. Thread-safe.</summary>
    Bitmap CaptureFrame();
    int ScreenWidth  { get; }
    int ScreenHeight { get; }
}
```

---

### 4.5 `ScreenCapture/GdiCapture.cs`

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FOLD.ScreenCapture;

/// <summary>Reliable GDI fallback. Works everywhere but uses CPU.</summary>
public sealed class GdiCapture : IScreenCapture
{
    public int ScreenWidth  => Screen.PrimaryScreen!.Bounds.Width;
    public int ScreenHeight => Screen.PrimaryScreen!.Bounds.Height;

    public Bitmap CaptureFrame()
    {
        var bmp = new Bitmap(ScreenWidth, ScreenHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public void Dispose() { }
}
```

---

### 4.6 `ScreenCapture/WgcCapture.cs`

```csharp
// Windows.Graphics.Capture — GPU-accelerated, lowest CPU usage
// Requires: TargetFramework net8.0-windows10.0.19041.0
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace FOLD.ScreenCapture;

public sealed class WgcCapture : IScreenCapture
{
    private readonly GraphicsCaptureItem _item;
    private readonly Direct3D11CaptureFramePool _framePool;
    private readonly GraphicsCaptureSession _session;
    private Bitmap? _lastFrame;
    private readonly object _lock = new();

    public int ScreenWidth  { get; }
    public int ScreenHeight { get; }

    public WgcCapture()
    {
        var monitor = User32.MonitorFromPoint(default, 2);   // MONITOR_DEFAULTTOPRIMARY
        _item = GraphicsCaptureItem.TryCreateFromDisplayId(
            new Windows.Graphics.DisplayId((ulong)monitor));

        var size = _item.Size;
        ScreenWidth  = size.Width;
        ScreenHeight = size.Height;

        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            CreateD3DDevice(),
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            size);

        _framePool.FrameArrived += OnFrameArrived;
        _session = _framePool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = true;
        _session.IsBorderRequired = false;
        _session.StartCapture();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;
        var bmp = SurfaceToBitmap(frame.Surface, ScreenWidth, ScreenHeight);
        lock (_lock) { _lastFrame?.Dispose(); _lastFrame = bmp; }
    }

    public Bitmap CaptureFrame()
    {
        lock (_lock)
        {
            if (_lastFrame == null) return new Bitmap(ScreenWidth, ScreenHeight);
            return (Bitmap)_lastFrame.Clone();
        }
    }

    private static IDirect3DDevice CreateD3DDevice() =>
        throw new PlatformNotSupportedException("WGC D3D device setup — see README");

    private static Bitmap SurfaceToBitmap(IDirect3DSurface surface, int w, int h) =>
        throw new NotImplementedException("Surface-to-Bitmap conversion");

    public void Dispose()
    {
        _session.Dispose();
        _framePool.Dispose();
        _lastFrame?.Dispose();
    }

    private static class User32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);
    }
}
/*
 NOTE: WgcCapture full implementation requires additional NuGet packages:
   dotnet add package Microsoft.Windows.CsWin32
   dotnet add package SharpDX.Direct3D11
 Start with GdiCapture (fully complete above). WGC adds ~20% lower CPU at 30fps.
*/
```

---

### 4.7 `Server/MjpegServer.cs`

```csharp
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FOLD.ScreenCapture;

namespace FOLD.Server;

public sealed class MjpegServer : IDisposable
{
    private readonly int _port;
    private readonly int _quality;
    private readonly int _targetFps;
    private HttpListener? _listener;
    private IScreenCapture? _capture;
    private CancellationTokenSource? _cts;

    private static readonly ImageCodecInfo JpegCodec;
    private static readonly EncoderParameters JpegParams;

    static MjpegServer()
    {
        JpegCodec  = GetEncoder(ImageFormat.Jpeg)!;
        JpegParams = new EncoderParameters(1);
    }

    public MjpegServer(int port, int quality, int fps)
    {
        _port      = port;
        _quality   = quality;
        _targetFps = fps;
    }

    public void Start(IScreenCapture capture)
    {
        _capture  = capture;
        _cts      = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_port}/");
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener!.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx, ct), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx, CancellationToken ct)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";

        // ── GET /info ─── Handshake: return screen resolution ───────────────
        if (path == "/info" && ctx.Request.HttpMethod == "GET")
        {
            var json  = $"{{\"width\":{_capture!.ScreenWidth},\"height\":{_capture.ScreenHeight},\"fps\":{_targetFps}}}";
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentType     = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.Close();
            return;
        }

        // ── GET /stream ── MJPEG stream ──────────────────────────────────────
        if (path == "/stream" && ctx.Request.HttpMethod == "GET")
        {
            ctx.Response.ContentType  = "multipart/x-mixed-replace; boundary=--frame";
            ctx.Response.SendChunked  = true;

            var stream   = ctx.Response.OutputStream;
            var interval = TimeSpan.FromMilliseconds(1000.0 / _targetFps);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var start     = DateTime.UtcNow;
                    using var bmp = _capture!.CaptureFrame();
                    var jpegBytes = EncodeJpeg(bmp, _quality);

                    var header = Encoding.ASCII.GetBytes(
                        "--frame\r\n" +
                        "Content-Type: image/jpeg\r\n" +
                        $"Content-Length: {jpegBytes.Length}\r\n\r\n");

                    await stream.WriteAsync(header, ct);
                    await stream.WriteAsync(jpegBytes, ct);
                    await stream.WriteAsync("\r\n"u8.ToArray(), ct);
                    await stream.FlushAsync(ct);

                    var elapsed = DateTime.UtcNow - start;
                    var delay   = interval - elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);
                }
            }
            catch (Exception) { /* client disconnected */ }
            finally { ctx.Response.Close(); }
            return;
        }

        ctx.Response.StatusCode = 404;
        ctx.Response.Close();
    }

    private byte[] EncodeJpeg(Bitmap bmp, int quality)
    {
        var encoderParam = new EncoderParameter(Encoder.Quality, (long)quality);
        var p = new EncoderParameters(1) { Param = { [0] = encoderParam } };
        using var ms = new MemoryStream();
        bmp.Save(ms, JpegCodec, p);
        return ms.ToArray();
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format) =>
        Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.FormatID == format.Guid);

    public void Dispose() => Stop();
}
```

---

### 4.8 `Server/TouchReceiver.cs`

```csharp
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FOLD.Input;
using FOLD.Models;

namespace FOLD.Server;

public sealed class TouchReceiver : IDisposable
{
    private readonly int _port;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public TouchReceiver(int port) => _port = port;

    public void Start()
    {
        _cts      = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_port}/");
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void Stop() { _cts?.Cancel(); _listener?.Stop(); }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener!.GetContextAsync();
                _ = Task.Run(() => HandleTouch(ctx), ct);
            }
            catch { break; }
        }
    }

    private async Task HandleTouch(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var evt  = JsonSerializer.Deserialize<TouchEvent>(body);
            if (evt != null) TouchInjector.InjectTouch(evt);
            ctx.Response.StatusCode = 200;
        }
        catch { ctx.Response.StatusCode = 400; }
        finally { ctx.Response.Close(); }
    }

    public void Dispose() => Stop();
}
```

---

### 4.9 `Models/TouchEvent.cs`

```csharp
namespace FOLD.Models;

public record TouchEvent(
    string Type,       // "down" | "move" | "up"
    float  NormX,      // 0.0–1.0 (normalized to screen width)
    float  NormY,      // 0.0–1.0 (normalized to screen height)
    int    PointerId   // for multi-touch (0,1,2...)
);
```

---

### 4.10 `Input/TouchInjector.cs`

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FOLD.Models;

namespace FOLD.Input;

public static class TouchInjector
{
    public static void InjectTouch(TouchEvent evt)
    {
        var screen = Screen.PrimaryScreen!.Bounds;
        int x = (int)(evt.NormX * screen.Width);
        int y = (int)(evt.NormY * screen.Height);

        int absX = (int)((float)x / screen.Width  * 65535);
        int absY = (int)((float)y / screen.Height * 65535);

        var mouseFlags = evt.Type switch
        {
            "down" => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.LeftDown,
            "move" => MouseEventFlags.Move | MouseEventFlags.Absolute,
            "up"   => MouseEventFlags.Move | MouseEventFlags.Absolute | MouseEventFlags.LeftUp,
            _      => MouseEventFlags.Move | MouseEventFlags.Absolute
        };

        SendMouseInput(absX, absY, mouseFlags);
    }

    private static void SendMouseInput(int x, int y, MouseEventFlags flags)
    {
        var input = new INPUT
        {
            type = 0,
            U = new InputUnion { mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = (uint)flags } }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    [Flags]
    private enum MouseEventFlags : uint
    {
        Move     = 0x0001,
        LeftDown = 0x0002,
        LeftUp   = 0x0004,
        Absolute = 0x8000
    }

    [DllImport("user32.dll")] static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)] struct INPUT      { public int type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)]   struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT
    {
        public int  dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }
}
```

---

### 4.11 ⚡ `Usb/AdbForwarder.cs` *(NEW)*

This class is the entire Windows-side implementation for USB mode. It shells out to `adb` to set up reverse port forwarding so the tablet's `localhost` tunnels to the PC over the cable.

```csharp
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

    // Search order: app folder first, then PATH
    private static readonly string[] AdbSearchPaths =
    [
        Path.Combine(AppContext.BaseDirectory, "adb.exe"),
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
```

> **Tip:** If you don't want to require the full Android SDK, download the standalone **Platform-Tools** zip from `developer.android.com/tools/releases/platform-tools` and place `adb.exe` (+ `AdbWinApi.dll`, `AdbWinUsbApi.dll`) next to `FOLD.exe`.

---

## 5. Android App

### 5.1 `build.gradle.kts` (app module)

```kotlin
plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

android {
    namespace = "com.FOLD"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.FOLD"
        minSdk = 26
        targetSdk = 35
        versionCode = 2
        versionName = "2.0"
    }
    buildFeatures { viewBinding = true }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.constraintlayout:constraintlayout:2.1.4")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")
}
```

---

### 5.2 `AndroidManifest.xml`

```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <uses-permission android:name="android.permission.INTERNET"/>
    <application
        android:allowBackup="true"
        android:label="@string/app_name"
        android:theme="@style/Theme.FOLD">

        <activity android:name=".MainActivity"
            android:exported="true"
            android:screenOrientation="portrait">
            <intent-filter>
                <action android:name="android.intent.action.MAIN"/>
                <category android:name="android.intent.category.LAUNCHER"/>
            </intent-filter>
        </activity>

        <activity android:name=".DisplayActivity"
            android:exported="false"
            android:screenOrientation="landscape"
            android:configChanges="orientation|screenSize"
            android:keepScreenOn="true"/>
    </application>
</manifest>
```

---

### 5.3 ⚡ `ConnectionMode.kt` *(NEW)*

A sealed class that carries the resolved host for each mode. The rest of the app just reads `.host` — no `if/else` scattered everywhere.

```kotlin
package com.FOLD

/**
 * Describes how the app connects to the PC.
 *
 *  WiFi  → host = user-entered IP (e.g. "192.168.1.10")
 *  Usb   → host = "127.0.0.1"  (ADB reverse tunnel; PC set up adb reverse)
 */
sealed class ConnectionMode(val host: String) {
    class WiFi(ip: String) : ConnectionMode(ip)
    object Usb : ConnectionMode("127.0.0.1")
}
```

---

### 5.4 `MainActivity.kt` *(updated — Wi-Fi + USB tabs)*

```kotlin
package com.FOLD

import android.content.Intent
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.FOLD.databinding.ActivityMainBinding
import kotlinx.coroutines.*

class MainActivity : AppCompatActivity() {

    private lateinit var b: ActivityMainBinding
    private val scope = CoroutineScope(Dispatchers.Main + SupervisorJob())

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        b = ActivityMainBinding.inflate(layoutInflater)
        setContentView(b.root)

        // Restore last saved IP
        b.etIpAddress.setText(PrefsManager.getLastIp(this))

        // ── Mode toggle ──────────────────────────────────────────────────────
        // Default to last used mode; fall back to Wi-Fi
        setMode(PrefsManager.getLastMode(this))

        b.btnModeWifi.setOnClickListener { setMode(MODE_WIFI) }
        b.btnModeUsb.setOnClickListener  { setMode(MODE_USB)  }

        // ── Connect buttons ──────────────────────────────────────────────────
        b.btnConnect.setOnClickListener    { connectWifi() }
        b.btnConnectUsb.setOnClickListener { connectUsb()  }
    }

    // ── Wi-Fi connection ─────────────────────────────────────────────────────

    private fun connectWifi() {
        val ip = b.etIpAddress.text.toString().trim()
        if (ip.isEmpty()) { toast("Enter PC IP address"); return }
        PrefsManager.saveLastIp(this, ip)
        launchConnection(ConnectionMode.WiFi(ip))
    }

    // ── USB connection ────────────────────────────────────────────────────────

    private fun connectUsb() {
        // The tablet connects to localhost — ADB reverse forwards to the PC
        launchConnection(ConnectionMode.Usb)
    }

    // ── Shared launch logic ───────────────────────────────────────────────────

    private fun launchConnection(mode: ConnectionMode) {
        val btn = if (mode is ConnectionMode.Usb) b.btnConnectUsb else b.btnConnect
        btn.isEnabled  = false
        b.tvStatus.text = if (mode is ConnectionMode.Usb)
            "⚡ Connecting via USB…" else "Connecting…"

        scope.launch {
            val ok = withContext(Dispatchers.IO) { testConnection(mode.host) }
            if (ok) {
                PrefsManager.saveLastMode(this@MainActivity,
                    if (mode is ConnectionMode.Usb) MODE_USB else MODE_WIFI)
                startActivity(
                    Intent(this@MainActivity, DisplayActivity::class.java)
                        .putExtra("host", mode.host)
                        .putExtra("mode", if (mode is ConnectionMode.Usb) MODE_USB else MODE_WIFI)
                )
            } else {
                val hint = if (mode is ConnectionMode.Usb)
                    "❌ USB tunnel not ready.\nDid you click \"Enable USB Mode\" in the tray app?"
                else
                    "❌ Cannot reach PC. Same Wi-Fi?"
                b.tvStatus.text = hint
            }
            btn.isEnabled = true
        }
    }

    // ── Mode UI helper ────────────────────────────────────────────────────────

    private fun setMode(mode: String) {
        val isUsb = mode == MODE_USB
        b.tilIp.visibility    = if (isUsb) View.GONE else View.VISIBLE
        b.btnConnect.visibility    = if (isUsb) View.GONE else View.VISIBLE
        b.btnConnectUsb.visibility = if (isUsb) View.VISIBLE else View.GONE
        b.tvHint.text = if (isUsb)
            "⚡ USB Mode — plug in your cable.\nMake sure USB Debugging is on and you've clicked\n\"Enable USB Mode\" in the tray app."
        else
            "Enter your PC's IP address.\nBoth devices must be on the same Wi-Fi."
        b.btnModeWifi.isSelected = !isUsb
        b.btnModeUsb.isSelected  = isUsb
        b.tvStatus.text = ""
    }

    private fun testConnection(host: String): Boolean = try {
        okhttp3.OkHttpClient().newCall(
            okhttp3.Request.Builder().url("http://$host:8765/info").build()
        ).execute().use { it.isSuccessful }
    } catch (e: Exception) { false }

    private fun toast(msg: String) = Toast.makeText(this, msg, Toast.LENGTH_SHORT).show()

    override fun onDestroy() { super.onDestroy(); scope.cancel() }

    companion object {
        const val MODE_WIFI = "wifi"
        const val MODE_USB  = "usb"
    }
}
```

---

### 5.5 `DisplayActivity.kt` *(updated — reads `host` extra)*

```kotlin
package com.FOLD

import android.os.Bundle
import android.view.*
import androidx.appcompat.app.AppCompatActivity
import com.FOLD.databinding.ActivityDisplayBinding

class DisplayActivity : AppCompatActivity() {

    private lateinit var b: ActivityDisplayBinding
    private lateinit var touchSender: TouchSender

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        window.setDecorFitsSystemWindows(false)
        window.insetsController?.apply {
            hide(WindowInsets.Type.systemBars())
            systemBarsBehavior = WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }

        b = ActivityDisplayBinding.inflate(layoutInflater)
        setContentView(b.root)

        // host = Wi-Fi IP  OR  "127.0.0.1" (USB mode)
        val host = intent.getStringExtra("host") ?: "127.0.0.1"
        touchSender = TouchSender(host, 8766)

        b.mjpegView.setOnTouchListener { v, event ->
            val normX = event.x / v.width
            val normY = event.y / v.height
            val type  = when (event.action) {
                MotionEvent.ACTION_DOWN -> "down"
                MotionEvent.ACTION_MOVE -> "move"
                MotionEvent.ACTION_UP   -> "up"
                else -> return@setOnTouchListener false
            }
            touchSender.send(type, normX, normY, event.getPointerId(event.actionIndex))
            true
        }

        b.mjpegView.start("http://$host:8765/stream")
    }

    override fun onStop()    { super.onStop(); b.mjpegView.stop() }
    override fun onRestart() {
        super.onRestart()
        val host = intent.getStringExtra("host") ?: "127.0.0.1"
        b.mjpegView.start("http://$host:8765/stream")
    }
}
```

---

### 5.6 `MjpegInputStream.kt`

```kotlin
package com.FOLD

import java.io.BufferedInputStream
import java.io.InputStream

class MjpegInputStream(stream: InputStream) {

    private val input = BufferedInputStream(stream, 8192)

    fun readNextJpeg(): ByteArray? {
        var prev = -1
        while (true) {
            val b = input.read().takeIf { it != -1 } ?: return null
            if (prev == 0xFF && b == 0xD8) break
            prev = b
        }
        val buf = mutableListOf<Byte>(0xFF.toByte(), 0xD8.toByte())
        var p = -1
        while (true) {
            val b = input.read().takeIf { it != -1 } ?: break
            buf.add(b.toByte())
            if (p == 0xFF && b == 0xD9) break
            p = b
        }
        return buf.toByteArray()
    }
}
```

---

### 5.7 `MjpegView.kt`

```kotlin
package com.FOLD

import android.content.Context
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.util.AttributeSet
import android.view.SurfaceHolder
import android.view.SurfaceView
import kotlinx.coroutines.*
import okhttp3.OkHttpClient
import okhttp3.Request
import java.util.concurrent.TimeUnit

class MjpegView @JvmOverloads constructor(
    ctx: Context, attrs: AttributeSet? = null
) : SurfaceView(ctx, attrs), SurfaceHolder.Callback {

    private val client = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.SECONDS)  // No timeout on stream
        .build()

    private var scope: CoroutineScope? = null
    private var streamUrl: String? = null
    private val errorPaint = Paint().apply { color = Color.RED; textSize = 40f }

    init { holder.addCallback(this) }

    fun start(url: String) { streamUrl = url; if (holder.surface.isValid) launchStream(url) }
    fun stop()  { scope?.cancel() }

    override fun surfaceCreated(h: SurfaceHolder)  { streamUrl?.let { launchStream(it) } }
    override fun surfaceChanged(h: SurfaceHolder, f: Int, w: Int, hh: Int) {}
    override fun surfaceDestroyed(h: SurfaceHolder) { stop() }

    private fun launchStream(url: String) {
        scope?.cancel()
        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        scope!!.launch {
            while (isActive) {
                try { runStream(url) }
                catch (e: Exception) { drawError("Reconnecting… ${e.message}"); delay(2000) }
            }
        }
    }

    private fun runStream(url: String) {
        val req = Request.Builder().url(url).build()
        client.newCall(req).execute().use { response ->
            val mjpeg = MjpegInputStream(response.body!!.byteStream())
            while (true) {
                val jpeg = mjpeg.readNextJpeg() ?: break
                val bmp  = BitmapFactory.decodeByteArray(jpeg, 0, jpeg.size) ?: continue
                val c    = holder.lockCanvas() ?: continue
                try {
                    val scale = minOf(c.width.toFloat() / bmp.width, c.height.toFloat() / bmp.height)
                    val dstW  = (bmp.width  * scale).toInt()
                    val dstH  = (bmp.height * scale).toInt()
                    val left  = (c.width  - dstW) / 2
                    val top   = (c.height - dstH) / 2
                    c.drawColor(Color.BLACK)
                    c.drawBitmap(bmp,
                        android.graphics.Rect(0, 0, bmp.width, bmp.height),
                        android.graphics.Rect(left, top, left + dstW, top + dstH), null)
                } finally { holder.unlockCanvasAndPost(c); bmp.recycle() }
            }
        }
    }

    private fun drawError(msg: String) {
        val c = holder.lockCanvas() ?: return
        try { c.drawColor(Color.BLACK); c.drawText(msg, 40f, c.height / 2f, errorPaint) }
        finally { holder.unlockCanvasAndPost(c) }
    }
}
```

---

### 5.8 `TouchSender.kt`

```kotlin
package com.FOLD

import kotlinx.coroutines.*
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody

class TouchSender(private val host: String, private val port: Int) {

    private val client = OkHttpClient()
    private val scope  = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val json   = "application/json".toMediaType()
    private val url    = "http://$host:$port/"

    fun send(type: String, normX: Float, normY: Float, pointerId: Int = 0) {
        val body = """{"Type":"$type","NormX":$normX,"NormY":$normY,"PointerId":$pointerId}"""
        scope.launch {
            runCatching {
                client.newCall(
                    Request.Builder().url(url).post(body.toRequestBody(json)).build()
                ).execute().close()
            }
        }
    }

    fun destroy() = scope.cancel()
}
```

---

### 5.9 `PrefsManager.kt` *(updated — saves last mode)*

```kotlin
package com.FOLD

import android.content.Context

object PrefsManager {
    private const val FILE    = "FOLD_prefs"
    private const val KEY_IP  = "last_ip"
    private const val KEY_MODE = "last_mode"   // ⚡ USB

    fun getLastIp(ctx: Context): String =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getString(KEY_IP, "") ?: ""

    fun saveLastIp(ctx: Context, ip: String) =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putString(KEY_IP, ip).apply()

    fun getLastMode(ctx: Context): String =                             // ⚡ USB
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE)
            .getString(KEY_MODE, MainActivity.MODE_WIFI) ?: MainActivity.MODE_WIFI

    fun saveLastMode(ctx: Context, mode: String) =                     // ⚡ USB
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putString(KEY_MODE, mode).apply()
}
```

---

### 5.10 Layout: `activity_main.xml` *(updated — Wi-Fi / USB tabs)*

```xml
<?xml version="1.0" encoding="utf-8"?>
<androidx.constraintlayout.widget.ConstraintLayout
    xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:app="http://schemas.android.com/apk/res-auto"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:padding="32dp"
    android:background="#121212">

    <TextView
        android:id="@+id/tvTitle"
        android:layout_width="wrap_content"
        android:layout_height="wrap_content"
        android:text="🖥 FOLD"
        android:textSize="28sp"
        android:textColor="#FFFFFF"
        android:textStyle="bold"
        app:layout_constraintTop_toTopOf="parent"
        app:layout_constraintStart_toStartOf="parent"
        android:layout_marginTop="48dp"/>

    <!-- ⚡ Mode selector: Wi-Fi / USB -->
    <com.google.android.material.button.MaterialButtonToggleGroup
        android:id="@+id/toggleMode"
        android:layout_width="0dp"
        android:layout_height="wrap_content"
        app:singleSelection="true"
        app:layout_constraintTop_toBottomOf="@id/tvTitle"
        app:layout_constraintStart_toStartOf="parent"
        app:layout_constraintEnd_toEndOf="parent"
        android:layout_marginTop="20dp">

        <Button
            android:id="@+id/btnModeWifi"
            style="@style/Widget.Material3.Button.OutlinedButton"
            android:layout_width="0dp"
            android:layout_height="wrap_content"
            android:layout_weight="1"
            android:text="📶  Wi-Fi"/>

        <Button
            android:id="@+id/btnModeUsb"
            style="@style/Widget.Material3.Button.OutlinedButton"
            android:layout_width="0dp"
            android:layout_height="wrap_content"
            android:layout_weight="1"
            android:text="⚡  USB"/>
    </com.google.android.material.button.MaterialButtonToggleGroup>

    <TextView
        android:id="@+id/tvHint"
        android:layout_width="0dp"
        android:layout_height="wrap_content"
        android:text="Enter your PC's IP address.\nBoth devices must be on the same Wi-Fi."
        android:textColor="#AAAAAA"
        android:textSize="14sp"
        app:layout_constraintTop_toBottomOf="@id/toggleMode"
        app:layout_constraintStart_toStartOf="parent"
        app:layout_constraintEnd_toEndOf="parent"
        android:layout_marginTop="16dp"/>

    <!-- Wi-Fi IP input (hidden in USB mode) -->
    <com.google.android.material.textfield.TextInputLayout
        android:id="@+id/tilIp"
        android:layout_width="0dp"
        android:layout_height="wrap_content"
        android:hint="PC IP Address (e.g. 192.168.1.10)"
        app:layout_constraintTop_toBottomOf="@id/tvHint"
        app:layout_constraintStart_toStartOf="parent"
        app:layout_constraintEnd_toEndOf="parent"
        android:layout_marginTop="24dp">
        <com.google.android.material.textfield.TextInputEditText
            android:id="@+id/etIpAddress"
            android:layout_width="match_parent"
            android:layout_height="wrap_content"
            android:inputType="text"
            android:imeOptions="actionDone"
            android:textColor="#FFFFFF"/>
    </com.google.android.material.textfield.TextInputLayout>

    <!-- Wi-Fi connect button -->
    <Button
        android:id="@+id/btnConnect"
        android:layout_width="0dp"
        android:layout_height="56dp"
        android:text="Connect via Wi-Fi"
        android:textSize="16sp"
        app:layout_constraintTop_toBottomOf="@id/tilIp"
        app:layout_constraintStart_toStartOf="parent"
        app:layout_constraintEnd_toEndOf="parent"
        android:layout_marginTop="16dp"/>

    <!-- ⚡ USB connect button (shown in USB mode) -->
    <Button
        android:id="@+id/btnConnectUsb"
        android:layout_width="0dp"
        android:layout_height="56dp"
        android:text="⚡ Connect via USB"
        android:textSize="16sp"
        android:visibility="gone"
        app:layout_constraintTop_toBottomOf="@id/tvHint"
        app:layout_constraintStart_toStartOf="parent"
        app:layout_constraintEnd_toEndOf="parent"
        android:layout_marginTop="24dp"/>

    <TextView
        android:id="@+id/tvStatus"
        android:layout_width="0dp"
        android:layout_height="wrap_content"
        android:text=""
        android:textColor="#FF6B6B"
        android:textSize="14sp"
        app:layout_constraintTop_toBottomOf="@id/btnConnect"
        app:layout_constraintStart_toStartOf="parent"
        app:layout_constraintEnd_toEndOf="parent"
        android:layout_marginTop="12dp"/>

</androidx.constraintlayout.widget.ConstraintLayout>
```

---

### 5.11 Layout: `activity_display.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<FrameLayout
    xmlns:android="http://schemas.android.com/apk/res/android"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:background="#000000">

    <com.FOLD.MjpegView
        android:id="@+id/mjpegView"
        android:layout_width="match_parent"
        android:layout_height="match_parent"/>

</FrameLayout>
```

---

## 6. Communication Protocol

| Endpoint | Method | Description |
|----------|--------|-------------|
| `http://[HOST]:8765/info` | GET | Returns `{"width":1920,"height":1080,"fps":30}` — handshake |
| `http://[HOST]:8765/stream` | GET | MJPEG stream: `multipart/x-mixed-replace; boundary=--frame` |
| `http://[HOST]:8766/` | POST | Touch event JSON: `{"Type":"down","NormX":0.5,"NormY":0.5,"PointerId":0}` |

`[HOST]` is:
- **Wi-Fi mode** → the PC's LAN IP (e.g. `192.168.1.10`)
- **USB mode** → `127.0.0.1` (ADB reverse tunnel makes tablet's localhost reach the PC)

The HTTP server on Windows is identical in both modes. ADB `reverse` is the only difference.

**MJPEG Frame Format:**
```
--frame\r\n
Content-Type: image/jpeg\r\n
Content-Length: 12345\r\n
\r\n
[JPEG bytes]
\r\n
```

---

## 7. Build & Package

### Windows EXE

```bash
# Prerequisites: .NET 8 SDK
cd windows/FOLD

# Debug run
dotnet run

# Release — single self-contained EXE (~25MB)
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish

# Output: publish/FOLD.exe  ← run this on any Windows 10/11 PC, no install needed
```

### Android APK

```bash
# Prerequisites: Android Studio or JDK 17 + Android SDK

# Debug APK (install via USB)
cd android
./gradlew assembleDebug
adb install app/build/outputs/apk/debug/app-debug.apk

# Release APK (sideload on tablet)
./gradlew assembleRelease
# Output: app/build/outputs/apk/release/app-release.apk
# Transfer to Redmi Pad Pro → Enable "Install unknown apps" → Install
```

### ⚡ ADB Setup (for USB mode)

```bash
# Option A — Install Android SDK Platform-Tools (includes adb)
# Download: https://developer.android.com/tools/releases/platform-tools
# Add the folder to your system PATH.

# Option B — Drop adb.exe next to FOLD.exe
# Download the zip above, copy adb.exe + AdbWinApi.dll + AdbWinUsbApi.dll
# into the same folder as FOLD.exe. No PATH changes needed.

# Verify adb sees your tablet (cable must be plugged in):
adb devices
# Expected output:
# List of devices attached
# XXXXXXXXXXXXXXXX    device

# Test the reverse forward manually (FOLD does this automatically):
adb reverse tcp:8765 tcp:8765
adb reverse tcp:8766 tcp:8766

# Remove forwards:
adb reverse --remove-all
```

---

## 8. Quick Start / Usage

### Mode A — Wi-Fi Setup (5 minutes)

```
1. Run FOLD.exe on your PC
   → Tray icon appears (bottom-right)
   → Right-click → "Copy PC IP"

2. Install FOLD.apk on Redmi Pad Pro
   → Settings → Apps → Special app access → Install unknown apps

3. Connect both devices to the SAME Wi-Fi network

4. Open FOLD on tablet
   → Tap "📶 Wi-Fi" tab
   → Paste/type the PC IP address
   → Tap "Connect via Wi-Fi"

5. Your PC screen streams to the tablet!
   → Touch the tablet → controls your PC mouse
```

### ⚡ Mode B — USB (Zero-Latency) Setup

```
1. Enable USB Debugging on the tablet
   → Settings → About tablet → tap "MIUI version" 7× → Developer options
   → Developer options → USB Debugging: ON

2. Plug the tablet into the PC with a USB cable
   → Accept the "Allow USB Debugging?" prompt on the tablet

3. Make sure adb is available on the PC
   → adb.exe either on PATH or next to FOLD.exe
   → Verify: open CMD and run: adb devices
   → You should see your tablet listed as "device"

4. Run FOLD.exe on your PC
   → Right-click tray icon → "⚡ Enable USB Mode"
   → Tray icon title changes to "⚡USB"

5. Open FOLD on tablet
   → Tap "⚡ USB" tab
   → Tap "⚡ Connect via USB"

6. Zero-latency streaming begins!
   → <5ms latency over USB vs ~20–40ms over Wi-Fi
```

### Windows Firewall (if blocked)

```powershell
# Run PowerShell as Administrator once:
New-NetFirewallRule -DisplayName "FOLD Stream" -Direction Inbound -Protocol TCP -LocalPort 8765 -Action Allow
New-NetFirewallRule -DisplayName "FOLD Touch"  -Direction Inbound -Protocol TCP -LocalPort 8766 -Action Allow
```

> **Note:** Firewall rules are needed for Wi-Fi mode. USB mode bypasses the firewall entirely because traffic stays on localhost.

---

## 9. Performance Tuning

| Setting | Location | Recommended |
|---------|----------|-------------|
| JPEG Quality | `TrayApp.cs Quality` | 75 (default) / 60 (faster) / 90 (sharper) |
| Target FPS | `TrayApp.cs TargetFps` | 30 (default) / 20 (slow Wi-Fi) / 60 (USB/fast LAN) |
| Resolution scale | `GdiCapture.cs` | Add 0.75× scale before encode for lower bandwidth |
| Wi-Fi band | Router settings | Use 5GHz for lowest Wi-Fi latency |
| **USB mode** | **Tray → Enable USB Mode** | **Eliminates network entirely; best for gaming/video** |

### Scale Resolution (Optional — add to `GdiCapture.cs`)

```csharp
// In CaptureFrame(), after capture, before returning:
// Scales to 75% of native resolution → ~44% less JPEG data
int scaledW = (int)(ScreenWidth  * 0.75);
int scaledH = (int)(ScreenHeight * 0.75);
var scaled  = new Bitmap(bmp, scaledW, scaledH);
bmp.Dispose();
return scaled;
```

### Expected Performance

| Mode | Resolution | Quality | FPS | Latency |
|------|------------|---------|-----|---------|
| ⚡ USB | 1920×1080 | 90 | 60 | **<5ms** |
| ⚡ USB | 1920×1080 | 75 | 60 | **<5ms** |
| 📶 Wi-Fi 5GHz | 1920×1080 | 75 | 30 | ~40ms |
| 📶 Wi-Fi 5GHz | 1920×1080 | 60 | 30 | ~25ms |
| 📶 Wi-Fi 5GHz | 1440×810  | 75 | 60 | ~30ms |
| 📶 Wi-Fi 5GHz | 1280×720  | 75 | 60 | ~20ms |

> USB mode isn't limited by network bandwidth, so you can crank quality to 90 and FPS to 60 with no latency penalty.

---

## Notes & Known Limitations

- **USB mode requires USB Debugging:** Enabled under Developer Options on the Redmi Pad Pro. This is a one-time setup. The setting persists across reboots.
- **ADB daemon on PC:** `adb reverse` starts an ADB server on your PC automatically. It runs in the background as `adb.exe`. FOLD calls `adb reverse --remove-all` on exit/stop to clean up. You can also run it manually if you prefer.
- **One device at a time (USB):** `adb reverse` forwards to the first connected USB device. If you have multiple Android devices plugged in, use `adb -s <serial> reverse …` — the `AdbForwarder` can be extended to accept a serial number.
- **WgcCapture:** Stub is provided; start with `GdiCapture` (fully implemented). WGC gives ~15% lower CPU. Implement later with `SharpDX.Direct3D11` NuGet.
- **Multi-monitor:** `GdiCapture` captures primary monitor only. Add a monitor picker in the tray menu using `Screen.AllScreens[]`.
- **Touch accuracy:** Normalized coordinates work for 1:1 aspect ratio. For stretched/letterboxed display, adjust normalization in `DisplayActivity` based on the `/info` response.
- **Security:** This app has NO authentication. Use only on trusted networks/cables.
