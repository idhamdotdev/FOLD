using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FOLD.ScreenCapture;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Utils;

namespace FOLD.Server;

public sealed class H264Server : IDisposable
{
    public readonly int Port;
    public int TargetFps;
    public int BitrateMbps;
    public TrayApp? ServerApp { get; set; }

    private TcpListener? _listener;
    private IScreenCapture? _capture;
    private CancellationTokenSource? _cts;
    private TcpClient? _activeClient;
    private readonly object _clientLock = new object();

    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "h264_server.log");

    public H264Server(int port, int targetFps = 60, int bitrateMbps = 8)
    {
        Port = port;
        TargetFps = targetFps;
        BitrateMbps = bitrateMbps;
    }

    public void Start(IScreenCapture capture)
    {
        _capture = capture;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, Port);
        try
        {
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }
        catch { }
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
        Log($"H264Server listening on port {Port}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(ct);
                client.NoDelay = true;
                client.SendBufferSize = 1048576;
                client.SendTimeout = 1000;
                lock (_clientLock)
                {
                    if (_activeClient != null)
                    {
                        Log("Disconnecting existing client to accept new connection...");
                        try { _activeClient.Close(); } catch { }
                    }
                    _activeClient = client;
                }
                Log("Client connected");
                Task.Run(delegate
                {
                    ServeClient(client, ct);
                }, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex2)
            {
                Log("AcceptLoop error - " + ex2.Message);
                break;
            }
        }
    }

    private void ServeClient(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            int screenWidth = _capture.ScreenWidth;
            int screenHeight = _capture.ScreenHeight;
            int num = screenWidth & -2;
            int num2 = screenHeight & -2;

            // Compute bitrate and fps from the actual capture resolution
            int currentFps = TargetFps > 0 ? TargetFps : 60;
            int currentBitrate;
            if (num >= 3800) currentBitrate = 45;
            else if (num >= 2500) currentBitrate = 24;
            else if (num >= 1900) currentBitrate = 16;
            else currentBitrate = 12;
            // Allow TouchReceiver override if it already set a higher value
            if (BitrateMbps > currentBitrate) currentBitrate = BitrateMbps;

            TimeSpan timeSpan = TimeSpan.FromMilliseconds(1000.0 / (double)currentFps);
            Log($"Encoding {screenWidth}x{screenHeight} -> {num}x{num2} @ {currentFps} fps @ {currentBitrate} Mbps");
            Codec? codec = null;
            string text = "libx264";
            string[] array = new string[2] { "h264_nvenc", "libx264" };
            foreach (string text2 in array)
            {
                try
                {
                    codec = Codec.FindEncoderByName(text2);
                    text = text2;
                    Log("Encoder: " + text2);
                }
                catch
                {
                    continue;
                }
                break;
            }
            Codec valueOrDefault = codec.GetValueOrDefault();
            if (!codec.HasValue)
            {
                valueOrDefault = Codec.FindEncoderById(AVCodecID.H264);
                codec = valueOrDefault;
            }
            try
            {
                Log("Supported formats for " + text + ":");
                foreach (var fmt in codec.Value.PixelFormats)
                {
                    Log("  - " + fmt.ToString());
                }
            }
            catch (Exception ex)
            {
                Log("Could not print formats: " + ex.Message);
            }
            using CodecContext codecContext = new CodecContext(codec);
            codecContext.Width = num;
            codecContext.Height = num2;
            codecContext.TimeBase = new AVRational
            {
                Num = 1,
                Den = currentFps
            };
            codecContext.Framerate = new AVRational
            {
                Num = currentFps,
                Den = 1
            };
            codecContext.PixelFormat = AVPixelFormat.Yuv420p;
            codecContext.BitRate = (long)currentBitrate * 1000000L;
            codecContext.GopSize = currentFps;
            codecContext.MaxBFrames = 0;
            codecContext.Profile = 66;
            codecContext.Level = 51;
            using MediaDictionary mediaDictionary = new MediaDictionary();
            if (text == "h264_nvenc")
            {
                mediaDictionary["preset"] = "p1";
                mediaDictionary["tune"] = "ull";
                mediaDictionary["rc"] = "cbr";
                mediaDictionary["forced-idr"] = "1";
                mediaDictionary["zerolatency"] = "1";
            }
            else
            {
                mediaDictionary["preset"] = "ultrafast";
                mediaDictionary["tune"] = "zerolatency";
                mediaDictionary["forced_idr"] = "1";
                mediaDictionary["vbv-maxrate"] = $"{currentBitrate * 2}000";
                mediaDictionary["vbv-bufsize"] = $"{currentBitrate}000";
            }
            codecContext.Open(codec, mediaDictionary);
            // Initialize CPU buffer for capturing raw pixels
            IntPtr cpuBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(screenWidth * screenHeight * 4 + 65536);
            
            // Initialize parallel PixelConverters
            int numThreads = (screenHeight % 4 == 0 && (screenHeight / 4) % 2 == 0) ? 4 : 1;
            PixelConverter[] converters = new PixelConverter[numThreads];
            int sliceHeight = screenHeight / numThreads;
            for (int i = 0; i < numThreads; i++)
            {
                converters[i] = new PixelConverter(screenWidth, sliceHeight, AVPixelFormat.Bgra, num, sliceHeight, AVPixelFormat.Yuv420p);
            }

            try
            {
                using Frame frame = new Frame();
                frame.Width = num;
                frame.Height = num2;
                frame.Format = 0;
                frame.EnsureBuffer();
                bool flag = false;
                long num4 = 0L;
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        DateTime utcNow = DateTime.UtcNow;
                        frame.MakeWritable();
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        
                        int targetFrameTimeMs = 1000 / currentFps;
                        bool hasNewFrame = _capture.CaptureToBuffer(cpuBuffer, out int rowPitch, targetFrameTimeMs);
                        long msCapture = sw.ElapsedMilliseconds;

                        if (!hasNewFrame && num4 == 0)
                        {
                            continue;
                        }

                        long msConvert = 0;
                        if (hasNewFrame)
                        {
                            sw.Restart();
                            // Parallel color space conversion
                            Parallel.For(0, numThreads, i =>
                            {
                                int startY = i * sliceHeight;
                                IntPtr srcSlicePtr = new IntPtr(cpuBuffer.ToInt64() + startY * rowPitch);
                                IntPtr dstY = new IntPtr(frame.Data[0].ToInt64() + startY * frame.Linesize[0]);
                                IntPtr dstU = new IntPtr(frame.Data[1].ToInt64() + (startY / 2) * frame.Linesize[1]);
                                IntPtr dstV = new IntPtr(frame.Data[2].ToInt64() + (startY / 2) * frame.Linesize[2]);
                                
                                converters[i].Convert(
                                    new byte_ptrArray8 { [0] = srcSlicePtr },
                                    new int_array8 { [0] = rowPitch },
                                    sliceHeight,
                                    new byte_ptrArray8 { [0] = dstY, [1] = dstU, [2] = dstV },
                                    frame.Linesize
                                );
                            });
                            msConvert = sw.ElapsedMilliseconds;
                        }

                        sw.Restart();
                        frame.Pts = num4++;
                        codecContext.SendFrame(frame);
                        using Packet packet = new Packet();
                        while (true)
                        {
                            CodecResult codecResult = codecContext.ReceivePacket(packet);
                            if (codecResult == CodecResult.Again || codecResult == CodecResult.EOF)
                            {
                                break;
                            }
                            byte[] array2 = packet.Data.ToArray();
                            if (!flag)
                            {
                                var (array3, array4) = ExtractSpsAndPps(array2);
                                if (array3 == null || array4 == null)
                                {
                                    Log($"Waiting for IDR frame with SPS/PPS (pkt {num4})...");
                                    packet.Unref();
                                    continue;
                                }

                                // Send resolution info packet (type 3)
                                byte[] resolutionBytes = new byte[8];
                                resolutionBytes[0] = (byte)((num >> 24) & 0xFF);
                                resolutionBytes[1] = (byte)((num >> 16) & 0xFF);
                                resolutionBytes[2] = (byte)((num >> 8) & 0xFF);
                                resolutionBytes[3] = (byte)(num & 0xFF);
                                resolutionBytes[4] = (byte)((num2 >> 24) & 0xFF);
                                resolutionBytes[5] = (byte)((num2 >> 16) & 0xFF);
                                resolutionBytes[6] = (byte)((num2 >> 8) & 0xFF);
                                resolutionBytes[7] = (byte)(num2 & 0xFF);

                                SendTypedPacket(stream, 3, resolutionBytes);
                                SendTypedPacket(stream, 1, array3);
                                SendTypedPacket(stream, 2, array4);
                                stream.Flush();
                                flag = true;
                                Log($"Config sent - Res={num}x{num2} SPS={array3.Length}B  PPS={array4.Length}B");
                                array2 = StripConfigNals(array2);
                            }
                            SendTypedPacket(stream, 0, array2);
                            stream.Flush();
                            packet.Unref();
                        }
                        long msEncode = sw.ElapsedMilliseconds;
                        if (num4 <= 5 || num4 % 60 == 0)
                        {
                            Log($"[Profile] Frame {num4}: Capture={msCapture}ms, Convert={msConvert}ms, Encode={msEncode}ms, Size={frame.Width}x{frame.Height}");
                        }
                        TimeSpan timeSpan2 = timeSpan - (DateTime.UtcNow - utcNow);
                        if (timeSpan2 > TimeSpan.Zero)
                        {
                            Thread.Sleep(timeSpan2);
                        }
                    }
                }
                catch (IOException)
                {
                    Log("Client disconnected.");
                }
                catch (Exception ex2) when (!(ex2 is OperationCanceledException))
                {
                    Log($"CRASH: {ex2.GetType().Name}: {ex2.Message}\n{ex2.StackTrace}");
                }
            }
            finally
            {
                // Free Parallel PixelConverters
                foreach (var conv in converters)
                {
                    conv?.Dispose();
                }
                // Free CPU buffer
                System.Runtime.InteropServices.Marshal.FreeHGlobal(cpuBuffer);
                lock (_clientLock)
                {
                    if (_activeClient == client)
                    {
                        _activeClient = null;
                    }
                }
            }
        }
    }

    private static (byte[]? sps, byte[]? pps) ExtractSpsAndPps(byte[] annexB)
    {
        byte[] item = null;
        byte[] item2 = null;
        List<(int, int)> list = new List<(int, int)>();
        for (int i = 0; i <= annexB.Length - 3; i++)
        {
            if (annexB[i] == 0 && annexB[i + 1] == 0)
            {
                if (i + 3 < annexB.Length && annexB[i + 2] == 0 && annexB[i + 3] == 1)
                {
                    list.Add((i, 4));
                    i += 3;
                }
                else if (annexB[i + 2] == 1)
                {
                    list.Add((i, 3));
                    i += 2;
                }
            }
        }
        for (int j = 0; j < list.Count; j++)
        {
            (int, int) tuple = list[j];
            int item3 = tuple.Item1;
            int item4 = tuple.Item2;
            int num = ((j + 1 < list.Count) ? list[j + 1].Item1 : annexB.Length);
            int num2 = item3 + item4;
            if (num2 < num)
            {
                int num3 = annexB[num2] & 0x1F;
                byte[] array = new byte[num - item3];
                Array.Copy(annexB, item3, array, 0, array.Length);
                if (num3 == 7)
                {
                    item = array;
                }
                if (num3 == 8)
                {
                    item2 = array;
                }
            }
        }
        return (sps: item, pps: item2);
    }

    private static byte[] StripConfigNals(byte[] annexB)
    {
        List<(int, int)> list = new List<(int, int)>();
        for (int i = 0; i <= annexB.Length - 3; i++)
        {
            if (annexB[i] == 0 && annexB[i + 1] == 0)
            {
                if (i + 3 < annexB.Length && annexB[i + 2] == 0 && annexB[i + 3] == 1)
                {
                    list.Add((i, 4));
                    i += 3;
                }
                else if (annexB[i + 2] == 1)
                {
                    list.Add((i, 3));
                    i += 2;
                }
            }
        }
        using MemoryStream memoryStream = new MemoryStream();
        for (int j = 0; j < list.Count; j++)
        {
            (int, int) tuple = list[j];
            int item = tuple.Item1;
            int item2 = tuple.Item2;
            int num = item + item2;
            int num2 = ((j + 1 < list.Count) ? list[j + 1].Item1 : annexB.Length);
            if (num < num2)
            {
                int num3 = annexB[num] & 0x1F;
                if (num3 != 7 && num3 != 8)
                {
                    memoryStream.Write(annexB, item, num2 - item);
                }
            }
        }
        byte[] array = memoryStream.ToArray();
        return (array.Length != 0) ? array : annexB;
    }

    private static void SendTypedPacket(NetworkStream net, byte type, byte[] data)
    {
        Span<byte> span = stackalloc byte[5];
        span[0] = type;
        span[1] = (byte)(data.Length >> 24);
        span[2] = (byte)(data.Length >> 16);
        span[3] = (byte)(data.Length >> 8);
        span[4] = (byte)data.Length;
        net.Write(span);
        net.Write(data);
    }

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }
        catch { }
    }
}
