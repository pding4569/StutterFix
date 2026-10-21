#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace StutterFix
{
    // Pure RGBA8 comparison, shared by the offline fixture and the live capture tool.
    internal static class FrameCompareMetrics
    {
        internal sealed class Mask
        {
            internal double X, Y, Width, Height;
            internal bool Percent;
            internal static Mask Parse(string token)
            {
                bool pct = token.StartsWith("maskpct=", StringComparison.Ordinal);
                if (!pct && !token.StartsWith("mask=", StringComparison.Ordinal)) throw new ArgumentException("expected mask=x,y,w,h or maskpct=cx,cy,w,h (fractions 0..1)");
                var values = token.Substring(pct ? 8 : 5).Split(',');
                if (values.Length != 4) throw new ArgumentException("mask requires four values");
                var v = values.Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                if (v.Any(d => double.IsNaN(d) || double.IsInfinity(d)) || v[2] <= 0 || v[3] <= 0 ||
                    (pct && v.Any(d => d < 0 || d > 1))) throw new ArgumentException("invalid mask extent/ratio");
                return new Mask { X = v[0], Y = v[1], Width = v[2], Height = v[3], Percent = pct };
            }
            internal bool Contains(int x, int y, int width, int height)
            {
                double left = Percent ? (X - Width / 2) * width : X;
                double bottom = Percent ? (Y - Height / 2) * height : Y;
                return x + 0.5 >= left && x + 0.5 < left + Width * (Percent ? width : 1) &&
                    y + 0.5 >= bottom && y + 0.5 < bottom + Height * (Percent ? height : 1);
            }
        }
        internal sealed class Result
        {
            internal readonly int[] Max = new int[4];
            internal long Changed;
            internal long InsideChanged, InsidePixels, OutsidePixels;
            internal double Ratio, Psnr;
            internal string Regions;
        }
        internal static Result Compare(byte[] a, byte[] b, int width, int height, int threshold, Mask[] masks = null)
        {
            if (width <= 0 || height <= 0 || threshold < 0 || threshold > 255 ||
                a == null || b == null || a.Length != checked(width * height * 4) || b.Length != a.Length)
                throw new ArgumentException("RGBA8 dimensions/threshold mismatch");
            var r = new Result();
            int cols = (width + 31) / 32, rows = (height + 31) / 32;
            var tiles = new long[cols * rows]; double squared = 0;
            for (int p = 0; p < a.Length / 4; p++)
            {
                bool inside = false;
                if (masks != null) for (int i = 0; i < masks.Length; i++)
                    if (masks[i].Contains(p % width, p / width, width, height)) { inside = true; break; }
                if (inside) r.InsidePixels++; else r.OutsidePixels++;
                bool changed = false;
                for (int c = 0; c < 4; c++)
                {
                    int d = Math.Abs(a[p * 4 + c] - b[p * 4 + c]);
                    changed |= d > threshold;
                    if (inside) continue;
                    r.Max[c] = Math.Max(r.Max[c], d); squared += (double)d * d;
                    tiles[(p / width / 32) * cols + p % width / 32] += d;
                }
                if (changed) { if (inside) r.InsideChanged++; else r.Changed++; }
            }
            r.Ratio = r.OutsidePixels == 0 ? double.NaN : r.Changed / (double)r.OutsidePixels;
            r.Psnr = r.OutsidePixels == 0 ? double.NaN : squared == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255 * r.OutsidePixels * 4 / squared);
            r.Regions = string.Join(";", Enumerable.Range(0, tiles.Length).Where(i => tiles[i] > 0)
                .OrderByDescending(i => tiles[i]).ThenBy(i => i).Take(5).Select(i =>
                    $"({i % cols * 32},{i / cols * 32},{Math.Min(32, width - i % cols * 32)},{Math.Min(32, height - i / cols * 32)}):{tiles[i]}"));
            return r;
        }
    }
}
#endif
