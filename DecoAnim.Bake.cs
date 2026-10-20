#if DEV || AUTOTEST
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        // Same delegates and late/color deferral as Step. No Rec, proxy or shadow tween.
        internal static void BakeWrite(scrDecoration d, float[] v, int o, int mask)
        {
            var p = pivotPosRef(d); var off = pivotOffRef(d);
            if ((mask & 1) != 0 && p.x != v[o]) setPosX(d, v[o], off);
            if ((mask & 2) != 0 && p.y != v[o+1]) setPosY(d, v[o+1], off);
            var po = parOffRef(d);
            if ((mask & (1<<14)) != 0 && po.x != v[o+14]) setParOffX(d, v[o+14]);
            if ((mask & (1<<15)) != 0 && po.y != v[o+15]) setParOffY(d, v[o+15]);
            if ((mask & 4) != 0 && off.x != v[o+2]) setPivX(d, v[o+2]);
            if ((mask & 8) != 0 && off.y != v[o+3]) setPivY(d, v[o+3]);
            if ((mask & 16) != 0 && rotRef(d) != v[o+4])
            { if (MoveApply.DeferTransform(d)) rotRef(d) = v[o+4]; else setRot(d, v[o+4]); }
            var scale = scaleRef(d); var next = scale;
            if ((mask & 32) != 0) next.x = v[o+5];
            if ((mask & 64) != 0) next.y = v[o+6];
            if (scale.x != next.x || scale.y != next.y)
            { if (MoveApply.DeferTransform(d)) scaleRef(d) = next; else setScale(d, next); }
            if ((mask & (15<<7)) != 0)
            {
                var color = new Color(v[o+7], v[o+8], v[o+9], v[o+10]); var old = colRef(d);
                if (old.r != color.r || old.g != color.g || old.b != color.b || old.a != color.a)
                    if (!ColorFast.Enabled || !ColorFast.TryColor(d, color)) setCol(d, color);
            }
            if ((mask & (1<<11)) != 0 && opaRef(d) != v[o+11])
                if (!ColorFast.Enabled || !ColorFast.TryOpacity(d, v[o+11])) setOpa(d, v[o+11]);
            var par = parallaxRef(d).multiplier;
            if ((mask & (1<<12)) != 0) par.x = v[o+12];
            if ((mask & (1<<13)) != 0) par.y = v[o+13];
            if ((mask & (3<<12)) != 0) parallaxRef(d).multiplier = par;
        }
    }
}
#endif
