using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using WinRT;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace FOLD.ScreenCapture;

public sealed class WgcCapture : IScreenCapture, IDisposable
{
    private static class User32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    private readonly GraphicsCaptureItem _item;
    private readonly Direct3D11CaptureFramePool _framePool;
    private readonly GraphicsCaptureSession _session;
    private Bitmap? _lastFrame;
    private readonly object _lock = new object();
    private SharpDX.Direct3D11.Device _d3dDevice;

    public int ScreenWidth { get; }
    public int ScreenHeight { get; }

    public WgcCapture(IntPtr monitorHandle = default)
    {
        _item = CreateItemForMonitor((monitorHandle == IntPtr.Zero) ? User32.MonitorFromPoint(default(System.Drawing.Point), 2u) : monitorHandle);
        if (_item == null)
        {
            throw new PlatformNotSupportedException("WGC: Cannot create capture item for primary monitor.");
        }
        SizeInt32 size = _item.Size;
        ScreenWidth = size.Width;
        ScreenHeight = size.Height;
        _d3dDevice = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        IDirect3DDevice device = CreateWinRTDevice(_d3dDevice);
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
        _framePool.FrameArrived += OnFrameArrived;
        _session = _framePool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = true;
        _session.StartCapture();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using Direct3D11CaptureFrame direct3D11CaptureFrame = sender.TryGetNextFrame();
        if (direct3D11CaptureFrame == null)
        {
            return;
        }
        Bitmap lastFrame = SurfaceToBitmap(direct3D11CaptureFrame.Surface, ScreenWidth, ScreenHeight);
        lock (_lock)
        {
            _lastFrame?.Dispose();
            _lastFrame = lastFrame;
        }
    }

    public Bitmap CaptureFrame()
    {
        int num = 0;
        while (_lastFrame == null && num < 2000)
        {
            Thread.Sleep(5);
            num += 5;
        }
        lock (_lock)
        {
            if (_lastFrame == null)
            {
                return new Bitmap(ScreenWidth, ScreenHeight, PixelFormat.Format32bppArgb);
            }
            return (Bitmap)_lastFrame.Clone();
        }
    }

    private Bitmap SurfaceToBitmap(IDirect3DSurface surface, int w, int h)
    {
        IDirect3DDxgiInterfaceAccess direct3DDxgiInterfaceAccess = (IDirect3DDxgiInterfaceAccess)surface;
        Guid iid = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        IntPtr nativePtr = direct3DDxgiInterfaceAccess.GetInterface(ref iid);
        using Texture2D source = new Texture2D(nativePtr);
        Texture2DDescription description = new Texture2DDescription
        {
            Width = w,
            Height = h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            OptionFlags = ResourceOptionFlags.None
        };
        using Texture2D texture2D = new Texture2D(_d3dDevice, description);
        _d3dDevice.ImmediateContext.CopyResource(source, texture2D);
        DataBox dataBox = _d3dDevice.ImmediateContext.MapSubresource(texture2D, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
        try
        {
            Bitmap bitmap = new Bitmap(w, h, dataBox.RowPitch, PixelFormat.Format32bppArgb, dataBox.DataPointer);
            return (Bitmap)bitmap.Clone();
        }
        finally
        {
            _d3dDevice.ImmediateContext.UnmapSubresource(texture2D, 0);
        }
    }

    private static IDirect3DDevice CreateWinRTDevice(SharpDX.Direct3D11.Device d3dDevice)
    {
        using SharpDX.DXGI.Device device = d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
        CreateDirect3D11DeviceFromDXGIDevice(device.NativePointer, out var graphicsDevice);
        IDirect3DDevice result = (IDirect3DDevice)Marshal.GetObjectForIUnknown(graphicsDevice);
        Marshal.Release(graphicsDevice);
        return result;
    }

    private static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor)
    {
        IGraphicsCaptureItemInterop graphicsCaptureItemInterop = CastExtensions.As<IGraphicsCaptureItemInterop>(ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem"));
        Guid iid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        IntPtr thisPtr = graphicsCaptureItemInterop.CreateForMonitor(monitor, ref iid);
        return GraphicsCaptureItem.FromAbi(thisPtr);
    }

    public void Dispose()
    {
        _session.Dispose();
        _framePool.Dispose();
        _d3dDevice.Dispose();
        lock (_lock)
        {
            _lastFrame?.Dispose();
        }
    }

    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}
