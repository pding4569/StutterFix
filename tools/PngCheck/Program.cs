// 큰 이미지 줄이기 검증: 실제 PNG 를 (1) libdeflate 로 전부 풀고 sf_unfilter_downscale, (2) sf_png_shrink(흘려 풀기) 로 각각 줄여 바이트 비교한다.
//   dotnet run -c Release -- <긴 변> <최대 파일 수> "<폴더>;<폴더>..." [time|inflate]   (실행 폴더에 native/sfnative.dll, native/libdeflate.dll 을 둔다)
//   time: 흘려 풀기만 재기, inflate: 압축 풀기만 재기(sf_png_inflate_only)
using System; using System.IO; using System.Linq; using System.Diagnostics; using System.Runtime.InteropServices; using System.Threading; using System.Threading.Tasks; using System.Collections.Concurrent;
unsafe class P {
  [DllImport("sfnative.dll")] static extern int sf_unfilter_downscale(byte* raw, int w, int h, int bpp, byte* d, int nw, int nh);
  [DllImport("sfnative.dll")] static extern int sf_png_shrink(byte* z, int zlen, int w, int h, int bpp, byte* d, int nw, int nh);
  [DllImport("sfnative.dll")] static extern int sf_png_inflate_only(byte* z, int zlen, nint total);
  [DllImport("libdeflate.dll")] static extern IntPtr libdeflate_alloc_decompressor();
  [DllImport("libdeflate.dll")] static extern int libdeflate_zlib_decompress(IntPtr d, byte* i, nint il, byte* o, nint ol, out nint actual);
  static int BE(byte[] d, int p) => (d[p] << 24) | (d[p+1] << 16) | (d[p+2] << 8) | d[p+3];
  static void Main(string[] a) {
    int side = int.Parse(a[0]); int maxFiles = int.Parse(a[1]); bool timeOnly = a.Length > 3 && (a[3] == "time" || a[3] == "inflate"); bool inflOnly = a.Length > 3 && a[3] == "inflate";
    var files = a[2].Split(';').SelectMany(dir => Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories)).Take(maxFiles).ToArray();
    long same = 0, diff = 0, refFail = 0, streamFail = 0, skipped = 0; double tRef = 0, tStream = 0; long px = 0; string firstDiff = null;
    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 5 }, () => libdeflate_alloc_decompressor(), (f, st, dec) => {
      byte[] d; try { d = File.ReadAllBytes(f); } catch { return dec; }
      if (d.Length < 45 || d[1] != 0x50) { Interlocked.Increment(ref skipped); return dec; }
      int pos = 8, w = 0, h = 0, bd = 0, ct = 0, il = 0; bool trns = false; var idat = new MemoryStream();
      try { while (pos + 8 <= d.Length) { int len = BE(d, pos), type = BE(d, pos + 4); if (len < 0 || pos + 12L + len > d.Length) break;
          if (type == 0x49484452) { w = BE(d, pos + 8); h = BE(d, pos + 12); bd = d[pos + 16]; ct = d[pos + 17]; il = d[pos + 20]; }
          else if (type == 0x74524E53) trns = true; else if (type == 0x49444154) idat.Write(d, pos + 8, len); else if (type == 0x49454E44) break;
          pos += 12 + len; } } catch { }
      int bpp = ct == 6 ? 4 : (ct == 2 && !trns) ? 3 : 0;
      if (bd != 8 || il != 0 || bpp == 0 || Math.Max(w, h) <= side || idat.Length < 3) { Interlocked.Increment(ref skipped); return dec; }
      double fct = (double)(float)((float)side / Math.Max(w, h)); int nw = Math.Max(1, (int)Math.Round(w * fct)), nh = Math.Max(1, (int)Math.Round(h * fct));
      var z = idat.ToArray(); long rawN = (long)h * (1 + (long)w * bpp);
      var o1 = new byte[(long)nw * nh * bpp]; var o2 = new byte[o1.Length];
      bool okRef = false;
      if (!timeOnly) {
        var sw = Stopwatch.StartNew();
        IntPtr raw = Marshal.AllocHGlobal((IntPtr)rawN);
        try { fixed (byte* zp = z) fixed (byte* op = o1) { nint act; int r = libdeflate_zlib_decompress(dec, zp, z.Length, (byte*)raw, (nint)rawN, out act);
            okRef = r == 0 && sf_unfilter_downscale((byte*)raw, w, h, bpp, op, nw, nh) != 0; } } finally { Marshal.FreeHGlobal(raw); }
        lock (files) tRef += sw.Elapsed.TotalSeconds;
      }
      var sw2 = Stopwatch.StartNew(); bool okS;
      fixed (byte* zp = z) fixed (byte* op = o2) okS = inflOnly ? sf_png_inflate_only(zp, z.Length, (nint)rawN) != 0 : sf_png_shrink(zp, z.Length, w, h, bpp, op, nw, nh) != 0;
      lock (files) { tStream += sw2.Elapsed.TotalSeconds; px += (long)w * h; }
      if (timeOnly) { if (!okS) Interlocked.Increment(ref streamFail); else Interlocked.Increment(ref same); return dec; }
      if (!okRef) { Interlocked.Increment(ref refFail); if (okS) Console.WriteLine("ref fail, stream ok: " + f); return dec; }
      if (!okS) { Interlocked.Increment(ref streamFail); Console.WriteLine("STREAM FAIL " + f); return dec; }
      if (o1.AsSpan().SequenceEqual(o2)) Interlocked.Increment(ref same); else { Interlocked.Increment(ref diff); lock (files) firstDiff ??= f; }
      return dec; }, dec => { });
    Console.WriteLine($"files {files.Length}: same {same}, different {diff}, ref failed {refFail}, stream failed {streamFail}, skipped {skipped} | pixels {px/1e9:F1}G | thread time: full decode {tRef:F1}s, stream {tStream:F1}s" + (firstDiff != null ? " | first diff " + firstDiff : ""));
  }
}
