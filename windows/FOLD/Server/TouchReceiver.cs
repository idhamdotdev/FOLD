using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FOLD.Input;
using FOLD.Models;
using FOLD.VirtualDisplay;

namespace FOLD.Server;

/// <summary>
/// HTTP server listening on port 8766 for touch events POSTed by the Android app.
/// Each request body is a JSON TouchEvent that is injected as mouse input via SendInput.
/// </summary>
public sealed class TouchReceiver : IDisposable
{
    private readonly int _port;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public H264Server? Server { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TouchReceiver(int port) => _port = port;

    public void Start()
    {
        _cts      = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_port}/");
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); }  catch { /* ignore */ }
        try { _listener?.Close(); } catch { /* ignore */ }
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener!.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleTouch(ctx), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
            catch { break; }
        }
    }

    private async Task HandleTouch(HttpListenerContext ctx)
    {
        // CORS — preflight support
        ctx.Response.Headers["Access-Control-Allow-Origin"]  = "*";
        ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
        ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

        if (ctx.Request.HttpMethod == "OPTIONS")
        {
            ctx.Response.StatusCode = 204;
            ctx.Response.Close();
            return;
        }

        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);

            if (ctx.Request.RawUrl?.Contains("/config") == true)
            {
                var cfg = JsonSerializer.Deserialize<DisplayConfigRequest>(body, JsonOpts);
                if (cfg != null)
                {
                    // Auto-detect/activate virtual display on client connect
                    if (Server?.ServerApp != null)
                    {
                        Server.ServerApp.AutoDetectVirtualMonitor();
                    }

                    IntPtr activeMonitor = TouchInjector.SelectedMonitorHandle;
                    if (activeMonitor == IntPtr.Zero && Server?.ServerApp != null)
                    {
                        activeMonitor = Server.ServerApp.SelectedMonitor;
                    }

                    if (activeMonitor != IntPtr.Zero)
                    {
                        int useW = Server?.ServerApp != null && Server.ServerApp.ForceWidth > 0 ? Server.ServerApp.ForceWidth : cfg.Width;
                        int useH = Server?.ServerApp != null && Server.ServerApp.ForceHeight > 0 ? Server.ServerApp.ForceHeight : cfg.Height;
                        int useFps = Server?.ServerApp != null && Server.ServerApp.ForceWidth > 0 ? Server.ServerApp.ForceFps : cfg.RefreshRate;

                        // Run on main thread context or threadpool, ChangeDisplaySettingsEx is thread-safe
                        VirtualDisplayManager.SetMonitorResolution(
                            activeMonitor,
                            useW,
                            useH,
                            useFps
                        );

                        if (Server?.ServerApp != null)
                        {
                            Server.ServerApp.RestartCaptureSession(useW, useH);
                        }

                    if (Server != null)
                    {
                        Server.TargetFps = useFps;
                        if (useW >= 3800)
                        {
                            Server.BitrateMbps = 45; // Max 4K quality over USB/WiFi
                        }
                        else if (useW >= 2500)
                        {
                            Server.BitrateMbps = 24; // Ultra-crisp 2.5K
                        }
                        else if (useW >= 1900)
                        {
                            Server.BitrateMbps = 16; // Crisp 1080p
                        }
                        else
                        {
                            Server.BitrateMbps = 10;
                        }
                    }
                }
            }
            }
            else
            {
                var evt = JsonSerializer.Deserialize<TouchEvent>(body, JsonOpts);
                if (evt != null)
                    TouchInjector.InjectTouch(evt);
            }

            ctx.Response.StatusCode = 200;
        }
        catch (JsonException)
        {
            ctx.Response.StatusCode = 400;
        }
        catch (Exception)
        {
            ctx.Response.StatusCode = 500;
        }
        finally
        {
            try { ctx.Response.Close(); } catch { /* ignore */ }
        }
    }

    public void Dispose() => Stop();
}

public sealed class DisplayConfigRequest
{
    [JsonPropertyName("Width")]
    public int Width { get; set; }

    [JsonPropertyName("Height")]
    public int Height { get; set; }

    [JsonPropertyName("RefreshRate")]
    public int RefreshRate { get; set; }
}
