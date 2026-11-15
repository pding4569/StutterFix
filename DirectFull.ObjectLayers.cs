using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Nodes belong to entries, so remove/reinsert never allocates a tree node.
    // Priorities are a deterministic permutation of IDs; comparison keys must
    // remain immutable while a node is linked.
    internal sealed class DirectFullOrderTree<T>
    {
        internal sealed class Node
        {
            internal T Value; internal Node Left, Right; internal bool Linked;
            internal uint Priority;
            internal Node(T value, uint id) { Value = value; id ^= id >> 16; id *= 0x7feb352d; id ^= id >> 15; id *= 0x846ca68b; Priority = id ^ (id >> 16); }
        }
        readonly Comparison<T> compare;
        Node root;
        internal DirectFullOrderTree(Comparison<T> compare) { this.compare = compare; }
        static Node Merge(Node a, Node b)
        {
            if (a == null) return b; if (b == null) return a;
            if (a.Priority < b.Priority) { a.Right = Merge(a.Right, b); return a; }
            b.Left = Merge(a, b.Left); return b;
        }
        Node Insert(Node at, Node n)
        {
            if (at == null) return n;
            int c = compare(n.Value, at.Value);
            if (c == 0) throw new InvalidOperationException("duplicate direct order key");
            if (c < 0) { at.Left = Insert(at.Left, n); if (at.Left.Priority < at.Priority) { var top = at.Left; at.Left = top.Right; top.Right = at; return top; } }
            else { at.Right = Insert(at.Right, n); if (at.Right.Priority < at.Priority) { var top = at.Right; at.Right = top.Left; top.Left = at; return top; } }
            return at;
        }
        Node Erase(Node at, Node n)
        {
            if (at == null) throw new InvalidOperationException("missing direct order node");
            if (ReferenceEquals(at, n)) return Merge(at.Left, at.Right);
            if (compare(n.Value, at.Value) < 0) at.Left = Erase(at.Left, n); else at.Right = Erase(at.Right, n);
            return at;
        }
        internal void Add(Node n) { if (n.Linked) throw new InvalidOperationException("linked direct order node"); n.Left = n.Right = null; root = Insert(root, n); n.Linked = true; }
        internal void Remove(Node n) { if (!n.Linked) return; root = Erase(root, n); n.Linked = false; n.Left = n.Right = null; }
        static void Append(Node n, List<T> target) { if (n == null) return; Append(n.Left, target); target.Add(n.Value); Append(n.Right, target); }
        internal void CopyTo(List<T> target) { target.Clear(); Append(root, target); }
        internal void Clear() { root = null; }
    }

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
