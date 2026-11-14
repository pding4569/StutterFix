using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Pure ordering policy shared by runtime and the offline regression fixture.
    internal static class DirectProxyPolicy
    {
        internal readonly struct Row
        {
            internal readonly SceneRenderOrder.Key Key;
            internal readonly int Slot, Texture, Layer;
            internal readonly uint Flags;
            internal Row(SceneRenderOrder.Key key, int slot, int texture, uint flags, int layer)
            { Key = key; Slot = slot; Texture = texture; Flags = flags; Layer = layer; }
            internal bool Fits(Row b) => Slot >= 0 && b.Slot >= 0 && Texture == b.Texture && Flags == b.Flags && Layer == b.Layer
                && Key.Layer == b.Key.Layer && Key.Order == b.Key.Order && Key.Queue == b.Key.Queue
                // GrabPass must capture again for EACH image, even in an otherwise homogeneous span.
                && (Flags >> 8) == 0;
        }
        internal readonly struct Span
        {
            internal readonly int Start, Count;
            internal Span(int start, int count) { Start = start; Count = count; }
        }
        // An entire ambiguous (layer/order/queue/distance) bucket stays in Unity.
        // A homogeneous, wholly-owned bucket needs only one proxy and retains original index order.
        internal static int Plan(List<Row> sorted, List<Span> spans, List<int> returned)
        {
            spans.Clear(); returned.Clear(); int ties = 0, open = -1, length = 0;
            for (int a = 0; a < sorted.Count;)
            {
                int b = a + 1;
                while (b < sorted.Count && sorted[a].Key.SameBucket(sorted[b].Key)) b++;
                bool safe = true;
                if (b - a > 1)
                {
                    ties++;
                    for (int k = a; k < b; k++) if (sorted[k].Slot < 0 || !sorted[a].Fits(sorted[k])) { safe = false; break; }
                    // Renderer instance ID is only our diagnostic fallback, not an observed Unity rule.
                    for (int k = a + 1; safe && k < b; k++) if (sorted[k - 1].Key.SameMaterialTie(sorted[k].Key)) safe = false;
                }
                for (int k = a; k < b; k++)
                {
                    var row = sorted[k];
                    if (!safe || row.Slot < 0)
                    {
                        if (open >= 0) { spans.Add(new Span(open, length)); open = -1; length = 0; }
                        if (row.Slot >= 0) returned.Add(row.Slot);
                        continue;
                    }
                    if (open >= 0 && sorted[k - 1].Fits(row)) length++;
                    else
                    {
                        if (open >= 0) spans.Add(new Span(open, length));
                        open = k; length = 1;
                    }
                }
                a = b;
            }
            if (open >= 0) spans.Add(new Span(open, length));
            return ties;
        }
    }
}
