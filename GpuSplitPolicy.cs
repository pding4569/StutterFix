#if DEV || AUTOTEST
using System;
using System.Collections.Generic;

namespace StutterFix
{
    [Flags]
    internal enum GpuSplitGroup { None = 0, Image = 1, Text = 2, Particle = 4, Tiles = 8, Planets = 16, Other = 32, All = 63 }

    // Unity-free decisions and restoration ledger, also compiled into the offline fixture.
    internal static class GpuSplitPolicy
    {
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
