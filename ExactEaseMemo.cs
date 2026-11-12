#if DEV || AUTOTEST
using System;
using System.Runtime.CompilerServices;

namespace StutterFix
{
    // Research-only, main-thread cache. Input bit patterns and frame must all
    // match. Clock positions, completion, identities and source order are untouched.
    internal sealed class ExactEaseMemo
    {
        internal delegate float Evaluator(int ease,float time,float duration,float over,float period);
        struct Entry
        {
            internal uint Frame;
            internal int Ease,Time,Duration,Over,Period;
            internal float Value;
        }
        readonly Entry[] entries=new Entry[256];
        readonly Evaluator evaluate;
        uint frame;
        internal long Hits,Misses;
        internal ExactEaseMemo(Evaluator evaluator){evaluate=evaluator??throw new ArgumentNullException(nameof(evaluator));}
        internal void BeginFrame()
        {
            if(++frame==0){Array.Clear(entries,0,entries.Length);frame=1;}
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe int Bits(float value)=>*(int*)&value;
        internal float Evaluate(int ease,float time,float duration,float over,float period)
        {
            int t=Bits(time),d=Bits(duration),o=Bits(over),p=Bits(period);
            uint hash=unchecked((uint)t*16777619u^(uint)d*2246822519u^(uint)o*3266489917u^(uint)p*668265263u^(uint)ease);
            hash^=hash>>16;
            ref Entry e=ref entries[hash&255];
            if(frame!=0 && e.Frame==frame && e.Ease==ease && e.Time==t && e.Duration==d && e.Over==o && e.Period==p)
            {Hits++;return e.Value;}
            // Publish only after a successful evaluation; exceptions retain the
            // original fallback behaviour and cannot publish an uncomputed value.
            float value=evaluate(ease,time,duration,over,period);
            e=new Entry{Frame=frame,Ease=ease,Time=t,Duration=d,Over=o,Period=p,Value=value};
            Misses++;return value;
        }
    }
}
#endif
