using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
/// TCP-based lightweight HTTP server listening on port 8766 for touch events POSTed by the Android app.
/// This replaces HttpListener to run without administrator privileges.
/// </summary>
public sealed class TouchReceiver : IDisposable
{
    private readonly int _port;
    private TcpListener? _listener;
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
        _listener = new TcpListener(IPAddress.Any, _port);
        try
        {
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }
        catch { }
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ignore */ }
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleClient(client), ct);
            }
            catch (Exception) when (ct.IsCancellationRequested) { break; }
            catch { break; }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                
                string? requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                string method = parts[0];
                string path = parts[1];

                int contentLength = 0;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
                    }
                }

                // Handle CORS preflight
                if (method == "OPTIONS")
                {
                    string corsResponse = "HTTP/1.1 204 No Content\r\n" +
                                          "Access-Control-Allow-Origin: *\r\n" +
                                          "Access-Control-Allow-Methods: POST, OPTIONS\r\n" +
                                          "Access-Control-Allow-Headers: Content-Type\r\n" +
                                          "Connection: close\r\n\r\n";
                    var corsBytes = Encoding.UTF8.GetBytes(corsResponse);
                    await stream.WriteAsync(corsBytes, 0, corsBytes.Length).ConfigureAwait(false);
                    return;
                }

                // Read request body
                char[] bodyBuffer = new char[contentLength];
                int read = 0;
                while (read < contentLength)
                {
                    int n = await reader.ReadAsync(bodyBuffer, read, contentLength - read).ConfigureAwait(false);
                    if (n <= 0) break;
                    read += n;
                }
                string body = new string(bodyBuffer);

                if (path.Contains("/config", StringComparison.OrdinalIgnoreCase))
                {
                    var cfg = JsonSerializer.Deserialize<DisplayConfigRequest>(body, JsonOpts);
                    if (cfg != null)
                    {
                        if (Server?.ServerApp != null)
                        {
                            Server.ServerApp.RegisterDeviceResolution(cfg.Width, cfg.Height, cfg.RefreshRate);
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

                            VirtualDisplayManager.SetMonitorResolution(activeMonitor, useW, useH, useFps);

                            if (Server?.ServerApp != null)
                            {
                                Server.ServerApp.RestartCaptureSession(useW, useH);
                            }

                            if (Server != null)
                            {
                                Server.TargetFps = useFps;
                                if (useW >= 3800) Server.BitrateMbps = 45;
                                else if (useW >= 2500) Server.BitrateMbps = 24;
                                else if (useW >= 1900) Server.BitrateMbps = 16;
                                else Server.BitrateMbps = 10;
                            }
                        }
                    }
                }
                else
                {
                    var evt = JsonSerializer.Deserialize<TouchEvent>(body, JsonOpts);
                    if (evt != null)
                    {
                        TouchInjector.InjectTouch(evt);
                    }
                }

                string successResponse = "HTTP/1.1 200 OK\r\n" +
                                         "Access-Control-Allow-Origin: *\r\n" +
                                         "Access-Control-Allow-Methods: POST, OPTIONS\r\n" +
                                         "Access-Control-Allow-Headers: Content-Type\r\n" +
                                         "Content-Length: 0\r\n" +
                                         "Connection: close\r\n\r\n";
                var resBytes = Encoding.UTF8.GetBytes(successResponse);
                await stream.WriteAsync(resBytes, 0, resBytes.Length).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    using var stream = client.GetStream();
                    string errResponse = "HTTP/1.1 500 Internal Server Error\r\n" +
                                         "Access-Control-Allow-Origin: *\r\n" +
                                         "Connection: close\r\n\r\n";
                    var errBytes = Encoding.UTF8.GetBytes(errResponse);
                    await stream.WriteAsync(errBytes, 0, errBytes.Length).ConfigureAwait(false);
                }
                catch {}
            }
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
