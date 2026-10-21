using System;

namespace StutterFix
{
    // No Unity dependency: integer placement, true edge extrusion and one UV rounding.
    internal static class LiveDecoAtlasPolicy
    {
        internal const int Pad = 2;
        internal static int Align(int x) => checked((x + 3) & ~3);
        internal sealed class Shelf
        {
            internal readonly int Size;
            int x, y, height;
            internal Shelf(int size) { if (size < 8 || (size & 3) != 0) throw new ArgumentException("atlas size"); Size = size; }
            internal bool Place(int width, int h, out int left, out int bottom)
            {
                if (width < 1 || h < 1) throw new ArgumentException("texture dimensions");
                int w = Align(checked(width + 2 * Pad)), rows = Align(checked(h + 2 * Pad));
                left = bottom = 0;
                if (w > Size || rows > Size) return false;
                int nx = x, ny = y, nh = height;
                if (nx + w > Size) { nx = 0; ny += nh; nh = 0; }
                if (ny + rows > Size) return false;
                left = nx + Pad; bottom = ny + Pad;
                x = nx + w; y = ny; height = Math.Max(nh, rows); return true;
            }
        }
        internal static float UV(int origin, int extent, int atlasSize, float uv)
        {
            double min = (double)origin / atlasSize, max = (double)(origin + extent) / atlasSize;
            return (float)(min + (double)uv * (max - min));
        }
        internal static T[] Extrude<T>(T[] pixels, int width, int height)
        {
            if (width < 1 || height < 1 || pixels.Length != checked(width * height)) throw new ArgumentException("pixels");
            int w = Align(checked(width + 2 * Pad)), h = Align(checked(height + 2 * Pad));
            var result = new T[checked(w * h)];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                result[y * w + x] = pixels[Math.Max(0, Math.Min(height - 1, y - Pad)) * width + Math.Max(0, Math.Min(width - 1, x - Pad))];
            return result;
        }
        internal static string Verdict(int pixels, bool restored, bool pixelsValid, bool fpsValid, double off, double on)
        {
            if (!restored || !pixelsValid || !fpsValid || off <= 0 || on <= 0 || double.IsNaN(off + on) || double.IsInfinity(off + on)) return "ERROR";
            if (pixels != 0) return "PIXEL_FAIL";
            return on >= off * 1.15 ? "PASS" : "NO_GAIN";
        }
    }
}
