using System;

namespace StutterFix
{
    internal static class RenderMergeLayoutPolicy
    {
        // Only known auxiliary renderers of an admitted sprite can be omitted.
        // Unknown/CPU-owned renderers remain barriers even while inactive.
        internal static bool OmitAuxiliary(bool admittedOwner, bool cpuFallback, bool live)
        {
            if (!admittedOwner || cpuFallback) return false;
            if (live) throw new InvalidOperationException("rendermerge admitted auxiliary became live");
            return true;
        }

        // Fixed mesh topology: hidden quads copy source vertex zero four times.
        // Updating this separate StructuredBuffer never edits CPU mesh data or
        // invalidates a vertex buffer captured by a pre-recorded command buffer.
        internal static void MapQuad(uint[] map, int destination, int sourceItem, bool drawn)
        {
            if (map == null || destination < 0 || destination > map.Length - 4 || sourceItem < 0)
                throw new ArgumentOutOfRangeException();
            uint first = checked((uint)sourceItem * 4);
            for (uint k = 0; k < 4; k++) map[destination + k] = first + (drawn ? k : 0);
        }

        internal const int ResourceFileLimit = 1048576;
        internal static bool ValidResourceObjects(int count, long remaining)
            => count > 0 && remaining <= ResourceFileLimit && 4L * count + 17 == remaining;
    }
}
