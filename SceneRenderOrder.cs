using System;

namespace StutterFix
{
    // Observed transparent back-to-front rule. Renderer ID is a deterministic diagnostic
    // fallback ONLY: Unity's same-material tie rule is not established by this model.
    internal static class SceneRenderOrder
    {
        // 동점의 셰이더 순위. 게임 안 측정: 같은 종류 셰이더끼리는 셰이더 ID 오름차순(20/20)이지만, 화면 복사(GrabPass)가 있는
        // 여러 패스 셰이더는 ID 가 더 작아도 일반 셰이더보다 뒤에 그려진다(Sprites/Tile 770 대 Blend Modes Grab 344: Grab 이 나중 12/12).
        internal static int ShaderRank(UnityEngine.Material m)
        {
            if (!m || !m.shader) return 0;
            return m.shader.GetInstanceID() + (m.passCount > 1 ? 0x40000000 : 0);
        }
        internal readonly struct Key : IComparable<Key>
        {
            internal readonly int Layer, Order, Queue, Material, Renderer, Slot, Shader;
            internal readonly float Distance;
            // 같은 (층,순서,큐,거리)에서 유니티는 셰이더 인스턴스 ID 오름차순, 그다음 재질 인스턴스 ID 오름차순으로 그린다(게임 안 측정 20/20).
            // Shader 0 = 모름(옛 호출): 그때는 재질만으로 비교한다.
            internal Key(int layer, int order, int queue, float distance, int material, int renderer, int slot = 0, int shader = 0)
            {
                Shader = shader;
                if (float.IsNaN(distance) || float.IsInfinity(distance)) throw new ArgumentException("nonfinite distance");
                Layer = layer; Order = order; Queue = queue; Distance = distance;
                Material = material; Renderer = renderer; Slot = slot;
            }
            internal bool SameBucket(Key b) => Layer == b.Layer && Order == b.Order && Queue == b.Queue && Distance == b.Distance;
            internal bool SameMaterialTie(Key b) => SameBucket(b) && Shader == b.Shader && Material == b.Material;
            // 동점 순위: 셰이더 ID 다음 재질 ID(부호 있는 값을 그 순서 그대로 64비트로)
            internal long Rank => ((long)Shader << 32) + ((long)Material - int.MinValue);
            public int CompareTo(Key b)
            {
                int c = Layer.CompareTo(b.Layer); if (c != 0) return c;
                c = Order.CompareTo(b.Order); if (c != 0) return c;
                c = Queue.CompareTo(b.Queue); if (c != 0) return c;
                c = b.Distance.CompareTo(Distance); if (c != 0) return c;
                c = Shader.CompareTo(b.Shader); if (c != 0) return c;
                c = Material.CompareTo(b.Material); if (c != 0) return c;
                c = Renderer.CompareTo(b.Renderer); return c != 0 ? c : Slot.CompareTo(b.Slot);
            }
        }
    }
}
