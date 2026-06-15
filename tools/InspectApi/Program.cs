using System;
using System.Reflection;

var asm = Assembly.LoadFrom(@"C:\Users\idham\.nuget\packages\sdcb.ffmpeg\7.0.0\lib\net6.0\Sdcb.FFmpeg.dll");
foreach (var t in asm.GetTypes())
{
    if (t.Name == "PixelConverter")
    {
        Console.WriteLine($"=== {t.FullName} Methods ===");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name + " " + p.Name))})");
        break;
    }
}
