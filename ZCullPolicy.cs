#if DEV || AUTOTEST || ZCULL_CHECK
using System;
namespace StutterFix
{
    internal static class ZCullPolicy
    {
        // Same-material ties have no established native order. Never use the ID fallback as proof.
        internal static bool[] Ambiguous(SceneRenderOrder.Key[] sorted)
        {
            var result = new bool[sorted.Length];
            for (int i = 1; i < sorted.Length; i++)
                if (sorted[i].SameMaterialTie(sorted[i - 1])) result[i] = result[i - 1] = true;
            return result;
        }
        internal static float Depth(int index, int count, bool counting)
            => (count - index - (counting ? .25f : 0f)) / (count + 1f);
        // CPU gate only. Texture alpha is evaluated by the actual GPU shader, not a fixture surrogate.
        internal static bool MayOcclude(float colorAlpha, bool normalBlend, bool ambiguous)
            => normalBlend && !ambiguous && colorAlpha == 1f;
    }
}
#endif
