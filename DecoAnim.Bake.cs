#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        private static readonly long[] bakeTicks = new long[5];
        private static long bakeCalls, bakeHidden, bakeDeferred;
        internal static void BeginBakeMeasure() { Array.Clear(bakeTicks,0,5); bakeCalls = bakeHidden = bakeDeferred = 0; }
        internal static void BakeReport(double frames)
        {
            string[] names = { "위치", "회전", "크기+시차배율", "색", "숨김" };
            var parts = new string[5];
            for (int i = 0; i < 5; i++) parts[i] = names[i]+"ms="+(bakeTicks[i]*1000.0/Stopwatch.Frequency/frames).ToString("F6",System.Globalization.CultureInfo.InvariantCulture);
            Main.Entry.Logger.Log("[굽기채택 쓰기] "+string.Join(" ",parts)+" 호출="+bakeCalls+" 숨김="+bakeHidden+" Transform미룸="+bakeDeferred+"; 64호출당1 표본추정, 목록/평가 별도");
        }
        // Apply final alpha before choosing the hidden Transform path. Reuse InvisibleSkip's
        // shader/mask/hitbox rules and lazy flush; never write forceRenderingOff independently.
        internal static void BakeWrite(scrDecoration d, float[] v, int o, int mask)
        {
            bool sample = (++bakeCalls & 63) == 0;
            long start = sample ? Stopwatch.GetTimestamp() : 0;
            if ((mask & (15<<7)) != 0)
            {
                var color = new Color(v[o+7],v[o+8],v[o+9],v[o+10]); var old = colRef(d);
                if (old.r != color.r || old.g != color.g || old.b != color.b || old.a != color.a)
                    if (!ColorFast.Enabled || !ColorFast.TryColor(d,color)) setCol(d,color);
            }
            if ((mask & (1<<11)) != 0 && opaRef(d) != v[o+11])
                if (!ColorFast.Enabled || !ColorFast.TryOpacity(d,v[o+11])) setOpa(d,v[o+11]);
            if (sample) { long now = Stopwatch.GetTimestamp(); bakeTicks[3] += (now-start)*64; start = now; }
            var visual = d as scrVisualDecoration;
            if (visual != null) InvisibleSkip.After(visual);
            bool hidden = InvisibleSkip.LazyCan(d);
            if (hidden) bakeHidden++;
            if (sample) { long now = Stopwatch.GetTimestamp(); bakeTicks[4] += (now-start)*64; start = now; }
            var p = pivotPosRef(d); var off = pivotOffRef(d); var po = parOffRef(d);
            if (hidden)
            {
                if ((mask & 1) != 0) p.x = v[o];
                if ((mask & 2) != 0) p.y = v[o+1];
                if ((mask & 4) != 0) off.x = v[o+2];
                if ((mask & 8) != 0) off.y = v[o+3];
                if ((mask & (1<<14)) != 0) po.x = v[o+14];
                if ((mask & (1<<15)) != 0) po.y = v[o+15];
                if ((mask & (15 | (3<<14) | 16 | 32 | 64)) != 0)
                { parOffRef(d) = po; InvisibleSkip.LazyStore(d,p,off); bakeDeferred++; }
            }
            else
            {
                if ((mask & 1) != 0 && p.x != v[o]) setPosX(d,v[o],off);
                if ((mask & 2) != 0 && p.y != v[o+1]) setPosY(d,v[o+1],off);
                if ((mask & (1<<14)) != 0 && po.x != v[o+14]) setParOffX(d,v[o+14]);
                if ((mask & (1<<15)) != 0 && po.y != v[o+15]) setParOffY(d,v[o+15]);
                if ((mask & 4) != 0 && off.x != v[o+2]) setPivX(d,v[o+2]);
                if ((mask & 8) != 0 && off.y != v[o+3]) setPivY(d,v[o+3]);
            }
            if (sample) { long now = Stopwatch.GetTimestamp(); bakeTicks[0] += (now-start)*64; start = now; }
            if ((mask & 16) != 0 && rotRef(d) != v[o+4])
            { if (hidden || MoveApply.DeferTransform(d)) rotRef(d) = v[o+4]; else setRot(d,v[o+4]); }
            if (sample) { long now = Stopwatch.GetTimestamp(); bakeTicks[1] += (now-start)*64; start = now; }
            var scale = scaleRef(d); var next = scale;
            if ((mask & 32) != 0) next.x = v[o+5];
            if ((mask & 64) != 0) next.y = v[o+6];
            if (scale.x != next.x || scale.y != next.y)
            { if (hidden || MoveApply.DeferTransform(d)) scaleRef(d) = next; else setScale(d,next); }
            var par = parallaxRef(d).multiplier;
            if ((mask & (1<<12)) != 0) par.x = v[o+12];
            if ((mask & (1<<13)) != 0) par.y = v[o+13];
            if ((mask & (3<<12)) != 0) parallaxRef(d).multiplier = par;
            if (sample) bakeTicks[2] += (Stopwatch.GetTimestamp()-start)*64;
        }
    }
}
#endif
