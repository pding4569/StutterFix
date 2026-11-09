using System;
using System.Runtime.InteropServices;
using StutterFix;

// CPU-only fixture: actual production scan, no Unity process or native engine calls.
namespace UnityEngine
{
    internal enum TextureFormat { RGB24=3, RGBA32=4, ARGB32=5, DXT5=12 }
    internal sealed class Texture2D
    {
        static int next;
        readonly int id = ++next;
        public int width, height;
        public TextureFormat format;
        public uint updateCount;
        public int GetInstanceID() => id;
        public static implicit operator bool(Texture2D t) => t != null;
        public static bool operator !(Texture2D t) => t == null;
    }
}
static class Program
{
    static int checks;
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("check " + checks); }
    static void Main()
    {
        var random = new Random(11010);
        int cases = 0;
        foreach (int format in new[] { 3,4,5,12 })
        foreach (int w in new[] { 1,7,31,63,64,255,257 })
        foreach (int h in new[] { 1,9,65 })
        foreach (bool solid in new[] { true,false })
        {
            int stride = format == 3 ? 3 : 4;
            var px = new byte[w*h*stride]; random.NextBytes(px);
            int alpha = format == 4 ? 3 : 0;
            if (format == 4 || format == 5)
                for (int i=0; i<w*h; i++) px[i*4+alpha] = solid || random.Next(5)!=0 ? (byte)255 : (byte)random.Next(255);
            var ptr = Marshal.AllocHGlobal(px.Length);
            try
            {
                Marshal.Copy(px,0,ptr,px.Length);
                ShapeCellsCpu.Compute(ptr,w,h,format,out var info);
                if (format == 12) { Check(info == null); continue; }
                Check(info != null);
                int grid = ShapeCellsCpu.GridFor(w,h);
                var expected = new ulong[grid*grid/64]; Array.Fill(expected,ulong.MaxValue);
                bool all = true;
                // Independent oracle: visit each cell's exact ceil-divided texel rectangle.
                if (format != 3) for (int cy=0; cy<grid; cy++) for (int cx=0; cx<grid; cx++)
                {
                    bool cellOpaque = true;
                    for (int y=(cy*h+grid-1)/grid; y<((cy+1)*h+grid-1)/grid; y++)
                        for (int x=(cx*w+grid-1)/grid; x<((cx+1)*w+grid-1)/grid; x++)
                            if (px[(y*w+x)*4+alpha] != 255) cellOpaque = false;
                    if (!cellOpaque) { int bit=cy*grid+cx; expected[bit>>6] &= ~(1UL << (bit&63)); all=false; }
                }
                Check(info.All == all);
                for (int i=0; i<expected.Length; i++) Check(expected[i] == info.Bits[i]);
                var tex = new UnityEngine.Texture2D { width=w,height=h,format=(UnityEngine.TextureFormat)format };
                ShapeCellsCpu.RegisterOpaque(tex,info); tex.updateCount++; // game's initial Apply
                Check(ShapeCellsCpu.TryGetOpaque(tex,out _,out bool registeredAll) && registeredAll == all);
                tex.updateCount++; Check(!ShapeCellsCpu.TryGetOpaque(tex,out _,out _));
                tex.format=UnityEngine.TextureFormat.DXT5; Check(!ShapeCellsCpu.TryGetOpaque(tex,out _,out _));
                cases++;
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        ShapeCellsCpu.Compute(IntPtr.Zero,64,64,4,out var empty); Check(empty == null);
        Console.WriteLine($"opacity fixture cases={cases} checks={checks} PASS (CPU only)");
        int before = checks;
        Check(OccludePolicy.Covers(new[] { -.1f,-.1f,1.1f,-.1f,1.1f,1.1f,-.1f,1.1f },3440,1440));
        Check(OccludePolicy.Covers(new[] { -.1f,1.1f,1.1f,1.1f,1.1f,-.1f,-.1f,-.1f },3440,1440));
        Check(!OccludePolicy.Covers(new[] { 0f,0f,1f,0f,1f,1f,0f,1f },3440,1440)); // no 1px halo
        Check(!OccludePolicy.Covers(new[] { .5f,-.3f,1.3f,.5f,.5f,1.3f,-.3f,.5f },3440,1440)); // AABB is insufficient
        Check(OccludePolicy.Covers(new[] { .5f,-.6f,1.6f,.5f,.5f,1.6f,-.6f,.5f },3440,1440));
        Check(!OccludePolicy.Covers(new[] { -.1f,-.1f,1.1f,1.1f,1.1f,-.1f,-.1f,1.1f },3440,1440)); // self crossing
        Check(!OccludePolicy.Covers(new[] { float.NaN,0f,1f,0f,1f,1f,0f,1f },3440,1440));
        Check(!OccludePolicy.Covers(new float[8],3440,1440));
        Check(!OccludePolicy.Covers(new float[8],0,0));
        Check(OccludePolicy.Before(-1,100,0,-100));
        Check(OccludePolicy.Before(0,-2,0,-1));
        Check(!OccludePolicy.Before(0,-1,0,-1));
        Check(!OccludePolicy.Before(0,1,0,-1));
        Check(!OccludePolicy.Before(1,-100,0,100));
        Console.WriteLine($"coverage/order fixture checks={checks-before} PASS (CPU only)");
    }
}
