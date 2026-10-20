using System;
using System.Collections.Generic;

namespace StutterFix
{
    // No Unity dependencies: the same interval/cursor code is exercised offline.
    internal sealed class FloorRangeLedger
    {
        internal const int Width = 64, MaxDepth = 32;
        internal struct Entry { internal int Start, End; internal float Opacity; }
        internal sealed class Chunk
        {
            internal readonly List<Entry> Entries = new List<Entry>(4);
            internal readonly int[] Cursor = new int[Width];
            internal int Remaining;
            internal bool Blocked;
        }
        internal readonly Chunk[] Chunks;
        internal readonly int Count;
        internal FloorRangeLedger(int count)
        {
            Count = count; Chunks = new Chunk[(count + Width - 1) / Width];
            for (int i = 0; i < Chunks.Length; i++) Chunks[i] = new Chunk();
        }
        internal bool Append(int chunk, Entry e)
        {
            var c = Chunks[chunk];
            if (c.Blocked || c.Entries.Count >= MaxDepth) return false;
            c.Entries.Add(e);
            c.Remaining = Math.Min(Width, Count - chunk * Width);
            return true;
        }
        internal void Drain(int index, Action<int, float> apply)
        {
            var c = Chunks[index / Width]; int slot = index % Width;
            if (c.Cursor[slot] == c.Entries.Count) return;
            // Advance only after a successful apply. An exception never loses work.
            while (c.Cursor[slot] < c.Entries.Count)
            {
                var e = c.Entries[c.Cursor[slot]];
                if (index >= e.Start && index <= e.End) apply(index, e.Opacity);
                c.Cursor[slot]++;
            }
            if (--c.Remaining == 0)
            {
                c.Entries.Clear(); Array.Clear(c.Cursor, 0, c.Cursor.Length);
            }
        }
    }
}
