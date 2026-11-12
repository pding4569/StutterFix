#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace StutterFix
{
    // Sources retain their identity, position accumulation and completion order.
    // Only pure easing evaluation is shared. Registry work occurs at allocation;
    // steady-state hits compare one exact time bit pattern, without hashing.
    internal sealed class ExactEaseCohorts
    {
        internal readonly struct Lease
        {
            internal readonly int Slot;internal readonly ulong Token;
            internal Lease(int slot,ulong token){Slot=slot;Token=token;}
            internal static Lease Invalid=>new Lease(-1,0);
        }
        readonly struct Key:IEquatable<Key>
        {
            internal readonly int Start,Initial,Duration,Ease,Over,Period;
            internal Key(int start,int initial,int duration,int ease,int over,int period)
            {Start=start;Initial=initial;Duration=duration;Ease=ease;Over=over;Period=period;}
            public bool Equals(Key x)=>Start==x.Start&&Initial==x.Initial&&Duration==x.Duration&&Ease==x.Ease&&Over==x.Over&&Period==x.Period;
            public override bool Equals(object x)=>x is Key k&&Equals(k);
            public override int GetHashCode()=>unchecked(((((Start*397^Initial)*397^Duration)*397^Ease)*397^Over)*397^Period);
        }
        struct Entry
        {
            internal Key Key;internal ulong Token;internal int References,RegistryFrame,Time;
            internal float Duration,Over,Period,Value;internal bool Cached;
        }
        readonly Entry[] entries;readonly int[] free;readonly Dictionary<Key,Lease> registry;
        readonly ExactEaseMemo.Evaluator evaluate;
        int freeCount,registryFrame;bool hasRegistryFrame;ulong serial;
        internal long Hits,Misses,Acquires,Releases,InvalidReads,InvalidReleases,CapacityFallbacks;
        internal int LiveGroups,PeakGroups;
        internal ExactEaseCohorts(ExactEaseMemo.Evaluator evaluator,int capacity)
        {
            if(capacity<=0||capacity>8192)throw new ArgumentOutOfRangeException(nameof(capacity));
            evaluate=evaluator??throw new ArgumentNullException(nameof(evaluator));
            entries=new Entry[capacity];free=new int[capacity];registry=new Dictionary<Key,Lease>(capacity);Reset();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] static unsafe int Bits(float v)=>*(int*)&v;
        internal Lease Acquire(int allocationFrame,int startFrame,float initial,int ease,float duration,float over,float period)
        {
            if(!hasRegistryFrame||registryFrame!=allocationFrame){registry.Clear();registryFrame=allocationFrame;hasRegistryFrame=true;}
            var key=new Key(startFrame,Bits(initial),Bits(duration),ease,Bits(over),Bits(period));
            if(registry.TryGetValue(key,out var found))
            {
                ref Entry same=ref entries[found.Slot];same.References++;Acquires++;return found;
            }
            if(freeCount==0){CapacityFallbacks++;return Lease.Invalid;}
            int slot=free[--freeCount];if(++serial==0)serial++;
            var lease=new Lease(slot,serial);
            entries[slot]=new Entry{Key=key,Token=serial,References=1,RegistryFrame=allocationFrame,Duration=duration,Over=over,Period=period};
            registry.Add(key,lease);LiveGroups++;PeakGroups=Math.Max(PeakGroups,LiveGroups);Acquires++;return lease;
        }
        internal bool TryEvaluate(Lease lease,float time,out float value)
        {
            value=0;
            if((uint)lease.Slot>=(uint)entries.Length){InvalidReads++;return false;}
            ref Entry e=ref entries[lease.Slot];
            if(e.References<=0||e.Token!=lease.Token){InvalidReads++;return false;}
            int bits=Bits(time);
            if(e.Cached&&e.Time==bits){Hits++;value=e.Value;return true;}
            value=evaluate(e.Key.Ease,time,e.Duration,e.Over,e.Period);
            e.Value=value;e.Time=bits;e.Cached=true;Misses++;return true;
        }
        internal bool Release(Lease lease)
        {
            if((uint)lease.Slot>=(uint)entries.Length){InvalidReleases++;return false;}
            ref Entry e=ref entries[lease.Slot];
            if(e.References<=0||e.Token!=lease.Token){InvalidReleases++;return false;}
            Releases++;if(--e.References>0)return true;
            if(hasRegistryFrame&&e.RegistryFrame==registryFrame&&registry.TryGetValue(e.Key,out var current)&&current.Token==lease.Token)
                registry.Remove(e.Key);
            e.Cached=false;free[freeCount++]=lease.Slot;LiveGroups--;return true;
        }
        internal void Reset()
        {
            registry.Clear();Array.Clear(entries,0,entries.Length);freeCount=entries.Length;
            for(int i=0;i<free.Length;i++)free[i]=free.Length-1-i;
            hasRegistryFrame=false;LiveGroups=PeakGroups=0;
            Hits=Misses=Acquires=Releases=InvalidReads=InvalidReleases=CapacityFallbacks=0;
            // Serial deliberately survives reset, so a stale source lease can
            // never alias the next allocation of the same slot.
        }
    }
}
#endif
