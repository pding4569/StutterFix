using System;

namespace StutterFix
{
    // Pure projected geometry/order policy; exercised without starting Unity.
    internal static class OccludePolicy
    {
        internal static bool Before(int layer, int order, int coverLayer, int coverOrder)
            => layer < coverLayer || layer == coverLayer && order < coverOrder;
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        internal static bool Covers(float[] xy, int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            float winding = 0;
            for (int i = 0; i < 4; i++)
            {
                int a = i*2, b = ((i+1)&3)*2, c = ((i+2)&3)*2;
                float turn = (xy[b]-xy[a])*(xy[c+1]-xy[b+1]) - (xy[b+1]-xy[a+1])*(xy[c]-xy[b]);
                if (!Finite(turn) || turn == 0 || winding != 0 && (turn > 0) != (winding > 0)) return false;
                winding = turn;
            }
            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner&1) == 0 ? -1f/width : 1+1f/width;
                float y = (corner&2) == 0 ? -1f/height : 1+1f/height;
                for (int i = 0; i < 4; i++)
                {
                    int a = i*2, b = ((i+1)&3)*2;
                    float cross = (xy[b]-xy[a])*(y-xy[a+1]) - (xy[b+1]-xy[a+1])*(x-xy[a]);
                    if (!Finite(cross) || cross == 0 || (cross > 0) != (winding > 0)) return false;
                }
            }
            return true;
        }
    }
}
