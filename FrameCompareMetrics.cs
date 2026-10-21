#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;

namespace StutterFix
{
    // Pure RGBA8 comparison, shared by the offline fixture and the live capture tool.
    internal static class FrameCompareMetrics
    {
        internal sealed class Result
        {
            internal readonly int[] Max = new int[4];
            internal long Changed;
            internal double Ratio, Psnr;
            internal string Regions;
        }
        internal static Result Compare(byte[] a, byte[] b, int width, int height, int threshold)
        {
            if (width <= 0 || height <= 0 || threshold < 0 || threshold > 255 ||
                a == null || b == null || a.Length != checked(width * height * 4) || b.Length != a.Length)
                throw new ArgumentException("RGBA8 dimensions/threshold mismatch");
            var r = new Result();
            int cols = (width + 31) / 32, rows = (height + 31) / 32;
            var tiles = new long[cols * rows]; double squared = 0;
            for (int p = 0; p < a.Length / 4; p++)
            {
                bool changed = false;
                for (int c = 0; c < 4; c++)
                {
                    int d = Math.Abs(a[p * 4 + c] - b[p * 4 + c]);
                    r.Max[c] = Math.Max(r.Max[c], d); squared += (double)d * d;
                    changed |= d > threshold;
                    tiles[(p / width / 32) * cols + p % width / 32] += d;
                }
                if (changed) r.Changed++;
            }
            r.Ratio = r.Changed / (double)(width * height);
            r.Psnr = squared == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255 * a.Length / squared);
            r.Regions = string.Join(";", Enumerable.Range(0, tiles.Length).Where(i => tiles[i] > 0)
                .OrderByDescending(i => tiles[i]).ThenBy(i => i).Take(5).Select(i =>
                    $"({i % cols * 32},{i / cols * 32},{Math.Min(32, width - i % cols * 32)},{Math.Min(32, height - i / cols * 32)}):{tiles[i]}"));
            return r;
        }
    }
}
#endif
