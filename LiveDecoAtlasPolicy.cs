using System;
using System.Collections.Generic;
using System.Linq;

namespace StutterFix
{
    // No Unity dependency: integer placement, true edge extrusion and one UV rounding.
    internal static class LiveDecoAtlasPolicy
    {
        internal const int Pad = 2;
        internal const long ByteLimit = 512L * 1024 * 1024;
        internal readonly struct Source
        {
            internal readonly int Width, Height, Group;
            internal Source(int width, int height, int group) { Width = width; Height = height; Group = group; }
        }
        internal readonly struct Slot
        {
            internal readonly int Page, X, Y;
            internal Slot(int page, int x, int y) { Page = page; X = x; Y = y; }
        }
        internal sealed class Layout
        {
            internal readonly List<Shelf> Pages = new List<Shelf>();
            internal readonly List<int> Groups = new List<int>();
            internal Slot[] Slots;
            internal long Bytes;
        }
        sealed class Choice
        {
            internal long Bytes;
            internal int Pages;
            internal Choice Previous;
            internal Layout Layout;
        }
        // Initial-only planning. Enumerate uniform-size shelf layouts per
        // sampler; among those choose the fewest pages within the TOTAL budget
        // (lower bytes break ties). This is not an optimal rectangle packer.
        // Return the actual shelves/coordinates: admission order cannot repack.
        internal static Layout Plan(Source[] sources, int maxSize, long budget = ByteLimit)
        {
            if (maxSize < 8 || budget < 0) throw new ArgumentException("atlas limits");
            var frontier = new List<Choice> { new Choice() };
            foreach (var group in Enumerable.Range(0, sources.Length).GroupBy(i => sources[i].Group).OrderBy(g => g.Key))
            {
                int[] indices = group.OrderByDescending(i => sources[i].Height).ThenByDescending(i => sources[i].Width).ThenBy(i => i).ToArray();
                var options = new List<Layout>();
                int first = 8;
                while (first < 2048 && first <= maxSize / 2) first *= 2;
                for (int size = first; size <= maxSize && (long)size * size * 4 <= budget; size *= 2)
                {
                    var option = new Layout { Slots = new Slot[sources.Length] };
                    bool fits = true;
                    foreach (int i in indices)
                    {
                        var s = sources[i];
                        if (s.Width < 1 || s.Height < 1) throw new ArgumentException("texture dimensions");
                        if (Align(checked(s.Width + 4)) > size || Align(checked(s.Height + 4)) > size) { fits = false; break; }
                        int page = 0, x = 0, y = 0;
                        for (; page < option.Pages.Count; page++) if (option.Pages[page].Place(s.Width, s.Height, out x, out y)) break;
                        if (page == option.Pages.Count)
                        {
                            option.Pages.Add(new Shelf(size)); option.Groups.Add(group.Key);
                            option.Bytes += (long)size * size * 4;
                            if (option.Bytes > budget) { fits = false; break; }
                            if (!option.Pages[page].Place(s.Width, s.Height, out x, out y)) throw new InvalidOperationException("atlas-placement");
                        }
                        option.Slots[i] = new Slot(page, x, y);
                    }
                    if (fits) options.Add(option);
                    if (size > maxSize / 2) break;
                }
                var next = new SortedDictionary<long, Choice>();
                foreach (var previous in frontier) foreach (var option in options)
                {
                    long bytes = previous.Bytes + option.Bytes;
                    int pages = previous.Pages + option.Pages.Count;
                    if (bytes <= budget && (!next.TryGetValue(bytes, out var old) || pages < old.Pages))
                        next[bytes] = new Choice { Bytes = bytes, Pages = pages, Previous = previous, Layout = option };
                }
                // A state using more bytes for no fewer pages cannot help a later group.
                frontier.Clear(); int bestPages = int.MaxValue;
                foreach (var state in next.Values) if (state.Pages < bestPages) { frontier.Add(state); bestPages = state.Pages; }
                if (frontier.Count == 0) throw new InvalidOperationException("atlas-vram-budget-512MiB");
            }
            var best = frontier.OrderBy(c => c.Pages).ThenBy(c => c.Bytes).First();
            var result = new Layout { Slots = new Slot[sources.Length], Bytes = best.Bytes };
            var selected = new Stack<Layout>();
            for (var c = best; c.Layout != null; c = c.Previous) selected.Push(c.Layout);
            while (selected.Count != 0)
            {
                var part = selected.Pop(); int offset = result.Pages.Count;
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i].Group == part.Groups[0])
                    { var slot = part.Slots[i]; result.Slots[i] = new Slot(slot.Page + offset, slot.X, slot.Y); }
                result.Pages.AddRange(part.Pages); result.Groups.AddRange(part.Groups);
            }
            return result;
        }
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
