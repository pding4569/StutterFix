using System;

namespace StutterFix
{
    internal static class MoveDeltaPolicy
    {
        internal const float Epsilon = 1e-6f;
        // Compare the actual last written position, never a cached requested
        // position: repeated sub-epsilon movement must not accumulate drift.
        internal static bool Within(float ax,float ay,float az,float bx,float by,float bz)
        {
            return Math.Abs(ax-bx)<Epsilon && Math.Abs(ay-by)<Epsilon && Math.Abs(az-bz)<Epsilon;
        }
    }
}
