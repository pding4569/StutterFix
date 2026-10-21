using System;

namespace StutterFix
{
    // Observed transparent back-to-front rule. Renderer ID is a deterministic diagnostic
    // fallback ONLY: Unity's same-material tie rule is not established by this model.
    internal static class SceneRenderOrder
    {
        internal readonly struct Key : IComparable<Key>
        {
            internal readonly int Layer, Order, Queue, Material, Renderer, Slot;
            internal readonly float Distance;
            internal Key(int layer, int order, int queue, float distance, int material, int renderer, int slot = 0)
            {
                if (float.IsNaN(distance) || float.IsInfinity(distance)) throw new ArgumentException("nonfinite distance");
                Layer = layer; Order = order; Queue = queue; Distance = distance;
                Material = material; Renderer = renderer; Slot = slot;
            }
            internal bool SameBucket(Key b) => Layer == b.Layer && Order == b.Order && Queue == b.Queue && Distance == b.Distance;
            internal bool SameMaterialTie(Key b) => SameBucket(b) && Material == b.Material;
            public int CompareTo(Key b)
            {
                int c = Layer.CompareTo(b.Layer); if (c != 0) return c;
                c = Order.CompareTo(b.Order); if (c != 0) return c;
                c = Queue.CompareTo(b.Queue); if (c != 0) return c;
                c = b.Distance.CompareTo(Distance); if (c != 0) return c;
                c = Material.CompareTo(b.Material); if (c != 0) return c;
                c = Renderer.CompareTo(b.Renderer); return c != 0 ? c : Slot.CompareTo(b.Slot);
            }
        }
    }
}
