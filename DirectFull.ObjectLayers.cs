using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Reversible per-object journal. A move can fail partway through: ownership
    // is marked BEFORE the first setter, so the same restore path covers it.
    internal sealed class DirectFullObjectLayers<T> where T : class
    {
        readonly Func<T, bool> alive;
        readonly Func<T, int> read;
        readonly Action<T, int> write;
        readonly List<int> original = new List<int>();
        internal readonly List<T> Objects = new List<T>();
        internal int Mask { get; private set; }
        internal bool Moved { get; private set; }
        internal DirectFullObjectLayers(Func<T, bool> alive, Func<T, int> read, Action<T, int> write)
        { this.alive = alive; this.read = read; this.write = write; }
        internal void Capture(T obj)
        {
            if (Moved) throw new InvalidOperationException("cannot replace original layers during a move");
            if (!alive(obj)) return;
            int layer = read(obj); Objects.Add(obj); original.Add(layer); Mask |= 1 << layer;
        }
        internal void Move(int layer)
        {
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));
            Moved = true;
            foreach (var obj in Objects) if (alive(obj)) write(obj, layer);
        }
        internal void Restore()
        {
            if (!Moved) return;
            Exception first = null;
            for (int i = 0; i < Objects.Count; i++)
                try { if (alive(Objects[i])) write(Objects[i], original[i]); }
                catch (Exception e) { if (first == null) first = e; }
            if (first != null) throw first;
            Moved = false;
        }
        internal static void MoveAll(List<DirectFullObjectLayers<T>> leases, int layer)
        {
            Exception first = null;
            foreach (var lease in leases)
                try { lease.Move(layer); }
                catch (Exception e) { if (first == null) first = e; }
            if (first != null) throw first;
        }
    }
    internal static class DirectFullOrderPath
    {
        // Common group prefixes stay contiguous. At the first differing group
        // compare its outer key against the other group/renderer, then descend.
        internal static int Compare(int[] a, SceneRenderOrder.Key[] ak, SceneRenderOrder.Key leafA,
            int[] b, SceneRenderOrder.Key[] bk, SceneRenderOrder.Key leafB)
        {
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                if (i < a.Length && i < b.Length && a[i] == b[i]) continue;
                return (i < a.Length ? ak[i] : leafA).CompareTo(i < b.Length ? bk[i] : leafB);
            }
            return 0;
        }
    }
}
