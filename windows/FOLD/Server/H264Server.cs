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
                client.SendBufferSize = 262144;
                client.SendTimeout = 500;
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
            int currentFps = TargetFps;
            int currentBitrate = BitrateMbps;

            TimeSpan timeSpan = TimeSpan.FromMilliseconds(1000.0 / (double)currentFps);
            int screenWidth = _capture.ScreenWidth;
            int screenHeight = _capture.ScreenHeight;
            int num = screenWidth;
            int num2 = screenHeight;
            if (screenWidth > 1280 || screenHeight > 720)
            {
                double num3 = Math.Min(1280.0 / (double)screenWidth, 720.0 / (double)screenHeight);
                num = (int)((double)screenWidth * num3) & -2;
                num2 = (int)((double)screenHeight * num3) & -2;
            }
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
            codecContext.Level = 40;
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
            using PixelConverter pixelConverter = new PixelConverter(screenWidth, screenHeight, AVPixelFormat.Bgra, num, num2, AVPixelFormat.Yuv420p);
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
                    using Bitmap bitmap = _capture.CaptureFrame();
                    if (bitmap.Width != screenWidth || bitmap.Height != screenHeight)
                    {
                        num4++;
                        continue;
                    }
                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, screenWidth, screenHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        pixelConverter.Convert(new byte_ptrArray8 { [0] = bitmapData.Scan0 }, new int_array8 { [0] = bitmapData.Stride }, screenHeight, frame.Data, frame.Linesize);
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }
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
                            SendTypedPacket(stream, 1, array3);
                            SendTypedPacket(stream, 2, array4);
                            stream.Flush();
                            flag = true;
                            Log($"Config sent - SPS={array3.Length}B  PPS={array4.Length}B");
                            array2 = StripConfigNals(array2);
                        }
                        Log($"Frame {num4}: {array2.Length}B");
                        SendTypedPacket(stream, 0, array2);
                        stream.Flush();
                        packet.Unref();
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
