var asm = System.Reflection.Assembly.LoadFrom(@"C:\Users\idham\.nuget\packages\sdcb.ffmpeg\7.0.0\lib\net6.0\Sdcb.FFmpeg.dll");
foreach (var t in asm.GetTypes())
{
    if (t.Name == "CodecContext")
    {
        Console.WriteLine("=== Properties ===");
        foreach (var p in t.GetProperties()) Console.WriteLine(p.Name);
        Console.WriteLine("=== Methods ===");
        foreach (var m in t.GetMethods()) if (m.Name.ToLower().Contains("extra") || m.Name.ToLower().Contains("ptr") || m.Name.ToLower().Contains("native") || m.Name.ToLower().Contains("handle")) Console.WriteLine(m.Name);
        break;
    }
}
