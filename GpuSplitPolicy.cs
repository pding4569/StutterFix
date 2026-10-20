#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StutterFix
{
    [Flags]
    internal enum GpuSplitGroup { None = 0, Image = 1, Text = 2, Particle = 4, Tiles = 8, Planets = 16, Other = 32, All = 63 }

    // Unity-free decisions and restoration ledger, also compiled into the offline fixture.
    internal static class GpuSplitPolicy
    {
        internal struct ProjectionArea
        {
            bool front, behind;
            double x0, y0, x1, y1;
            internal void Include(double x, double y, double z, double near)
            {
                if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) { behind = front = true; return; }
                if (z < near) { behind = true; return; }
                if (!front) { x0 = x1 = x; y0 = y1 = y; front = true; }
                else { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            }
            // A bound crossing the near plane must never be called tiny.
            internal double Percent => behind ? front ? double.PositiveInfinity : 0 : front ? (x1 - x0) * (y1 - y0) * 100 : 0;
        }
        internal static void PixelDiff(int length, Func<int, uint> a, Func<int, uint> b, out int max, out int different)
        {
            max = 0; different = 0;
            for (int i = 0; i < length; i++)
            {
                uint p = a(i), q = b(i);
                if (p != q) different++;
                for (int channel = 0; channel < 32; channel += 8)
                    max = Math.Max(max, Math.Abs((int)((p >> channel) & 255) - (int)((q >> channel) & 255)));
            }
        }
        internal sealed class Selection
        {
            internal GpuSplitGroup Groups;
            internal string[] Filters = Array.Empty<string>();
            internal double Percent = 1;
            internal bool Live;
            internal string LiveFilter = "all";
            // Alpha is a diagnostic hypothesis, never a shader-output guarantee.
            internal double AlphaThreshold = 0;
            internal bool MatchesLive(bool enabledActive, bool forced, bool onscreen, double alpha, double area)
            {
                if (!enabledActive || forced) return false;
                bool invisible = !onscreen || alpha <= AlphaThreshold;
                bool tiny = !double.IsNaN(area) && area < Percent;
                return LiveFilter == "all" || (LiveFilter == "visible" ? onscreen && alpha > AlphaThreshold :
                    LiveFilter == "invisible" ? invisible : LiveFilter == "tiny" ? tiny : invisible || tiny);
            }
            internal bool Matches(bool onscreen, double areaPercent, string blend, bool transparent)
            {
                foreach (var f in Filters)
                {
                    bool ok = f == "onscreen" ? onscreen : f == "offscreen" ? !onscreen :
                        f == "big" ? areaPercent > Percent : f == "small" ? areaPercent <= Percent :
                        f == "transparent" ? transparent : f == blend;
                    if (!ok) return false;
                }
                return true;
            }
        }

        // Image filters use ':' and ','; '+' continues to join whole groups.
        internal static Selection ParseSelection(string selector)
        {
            if (selector == "live" || selector.StartsWith("live:", StringComparison.Ordinal))
            {
                var live = new Selection { Groups = GpuSplitGroup.Image, Live = true, Percent = 0.01 };
                live.LiveFilter = selector == "live" ? "all" : selector.Substring(5);
                if (live.LiveFilter != "all" && live.LiveFilter != "visible" && live.LiveFilter != "invisible" &&
                    live.LiveFilter != "tiny" && live.LiveFilter != "invisible+tiny")
                    throw new ArgumentException("live[:visible|invisible|tiny|invisible+tiny]; alpha<=0, tiny<0.01 screen percent");
                return live;
            }
            int colon = selector.IndexOf(':');
            if (colon < 0) return new Selection { Groups = Parse(selector) };
            if (selector.Substring(0, colon) != "deco-image") throw new ArgumentException("filters require deco-image:<filter,...>; big=N/small=N in screen percent");
            var result = new Selection { Groups = GpuSplitGroup.Image };
            var filters = new List<string>();
            bool threshold = false;
            foreach (string token in selector.Substring(colon + 1).Split(','))
            {
                string f = token; int eq = token.IndexOf('=');
                if (eq >= 0)
                {
                    f = token.Substring(0, eq);
                    if ((f != "big" && f != "small") || threshold ||
                        !double.TryParse(token.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out result.Percent) ||
                        double.IsNaN(result.Percent) || double.IsInfinity(result.Percent) || result.Percent < 0)
                        throw new ArgumentException("one finite nonnegative big=N/small=N threshold required");
                    threshold = true;
                }
                if (f != "onscreen" && f != "offscreen" && f != "big" && f != "small" && f != "additive" && f != "alpha" && f != "transparent")
                    throw new ArgumentException("unknown image filter: " + token);
                if (filters.Contains(f)) throw new ArgumentException("duplicate image filter: " + f);
                filters.Add(f);
            }
            foreach (var pair in new[] { new[] { "onscreen", "offscreen" }, new[] { "big", "small" }, new[] { "alpha", "additive" } })
                if (filters.Contains(pair[0]) && filters.Contains(pair[1])) throw new ArgumentException("contradictory image filters");
            result.Filters = filters.ToArray(); return result;
        }

        internal static GpuSplitGroup Classify(bool scene, bool mainMask, bool ui, bool decoration,
            bool sprite, bool text, bool particle, bool floor, bool planet)
        {
            if (!scene || !mainMask || ui) return GpuSplitGroup.None;
            if (decoration)
            {
                if (particle) return GpuSplitGroup.Particle;
                if (text) return GpuSplitGroup.Text;
                if (sprite) return GpuSplitGroup.Image;
            }
            if (floor) return GpuSplitGroup.Tiles;
            if (planet) return GpuSplitGroup.Planets;
            return GpuSplitGroup.Other;
        }

        internal static GpuSplitGroup Parse(string selector)
        {
            if (selector == "none") return GpuSplitGroup.None;
            if (selector == "all") return GpuSplitGroup.All;
            GpuSplitGroup result = GpuSplitGroup.None;
            foreach (var part in selector.Split('+'))
                switch (part)
                {
                    case "deco-image": result |= GpuSplitGroup.Image; break;
                    case "deco-text": result |= GpuSplitGroup.Text; break;
                    case "deco-particle": result |= GpuSplitGroup.Particle; break;
                    case "tiles": result |= GpuSplitGroup.Tiles; break;
                    case "planets": result |= GpuSplitGroup.Planets; break;
                    case "other": result |= GpuSplitGroup.Other; break;
                    default: throw new ArgumentException("gpusplit hide <deco-image|deco-text|deco-particle|tiles|planets|other|all|none>; combine with +; refresh|state|off");
                }
            return result;
        }
    }

    internal sealed class GpuSplitLedger<T> where T : class
    {
        struct Saved { internal T Item; internal bool Off; }
        readonly List<Saved> saved = new List<Saved>();
        readonly HashSet<T> known = new HashSet<T>();
        readonly Func<T, bool> alive, read;
        readonly Action<T, bool> write;
        internal int Count => saved.Count;
        internal GpuSplitLedger(Func<T, bool> alive, Func<T, bool> read, Action<T, bool> write)
        { this.alive = alive; this.read = read; this.write = write; }
        internal void Hide(T item)
        {
            if (!alive(item)) return;
            if (known.Add(item)) saved.Add(new Saved { Item = item, Off = read(item) });
            write(item, true);
        }
        internal int Enforce()
        {
            int changed = 0;
            foreach (var s in saved)
                if (alive(s.Item) && !read(s.Item)) { write(s.Item, true); changed++; }
            return changed;
        }
        internal void Restore()
        {
            foreach (var s in saved) if (alive(s.Item)) write(s.Item, s.Off);
            saved.Clear(); known.Clear();
        }
    }
}
#endif
