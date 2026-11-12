using System;
using System.Text.Json;
using StutterFix;
class Program
{
    static void Require(bool v,string message){if(!v)throw new Exception(message);}
    static float Oracle(int e,float t,float d,float o,float p)=>e==0?t:(float)(Math.Sin((double)t/d+o)*p+e);
    static int Bits(float v)=>BitConverter.SingleToInt32Bits(v);
    static void Main()
    {
        var pool=new ExactEaseCohorts(Oracle,256);var leases=new ExactEaseCohorts.Lease[2048];
        for(int i=0;i<leases.Length;i++)leases[i]=pool.Acquire(1,1,0,i%64%3,1+i%64*.01f,.7f,.3f);
        Require(pool.LiveGroups==64,"sources not grouped");
        long comparisons=0;
        for(int frame=0;frame<200;frame++)for(int i=0;i<leases.Length;i++)
        {
            float time=frame*.0037f;Require(pool.TryEvaluate(leases[i],time,out var actual),"valid lease rejected");
            Require(Bits(actual)==Bits(Oracle(i%64%3,time,1+i%64*.01f,.7f,.3f)),"input-output mismatch");comparisons++;
        }
        long hits=pool.Hits,misses=pool.Misses;
        // Completing/reusing one source must not finish the other members.
        Require(pool.Release(leases[0]),"first release");Require(pool.TryEvaluate(leases[64],.74f,out _),"sibling lost");
        for(int i=1;i<leases.Length;i++)Require(pool.Release(leases[i]),"release failed");Require(pool.LiveGroups==0,"retained groups");
        Require(!pool.TryEvaluate(leases[0],.74f,out _),"stale read accepted");Require(!pool.Release(leases[0]),"double release accepted");
        pool.Reset();var first=pool.Acquire(2,1,0,1,1,.7f,.3f);pool.Reset();var second=pool.Acquire(2,1,0,1,1,.7f,.3f);
        Require(first.Slot==second.Slot&&first.Token!=second.Token,"reset lease alias");Require(!pool.TryEvaluate(first,1,out _),"stale reset read accepted");
        var future=pool.Acquire(3,1,0,1,1,.7f,.3f);Require(future.Slot!=second.Slot,"allocation-frame boundaries merged");
        Require(pool.TryEvaluate(second,.5f,out _)&&pool.TryEvaluate(future,.5f,out _),"registry clear invalidated live cohort");
        pool.Release(second);pool.Release(future);pool.Reset();
        var positive=pool.Acquire(1,1,0,0,1,.7f,.3f);var negative=pool.Acquire(1,1,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),0,1,.7f,.3f);
        Require(positive.Slot!=negative.Slot,"initial signed zero merged");
        Require(pool.TryEvaluate(positive,0,out var zero)&&pool.TryEvaluate(positive,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),out var minus)&&Bits(zero)!=Bits(minus),"time signed zero aliased");
        var tiny=new ExactEaseCohorts(Oracle,1);tiny.Acquire(1,1,0,1,1,.7f,.3f);var overflow=tiny.Acquire(1,1,0,2,1,.7f,.3f);Require(overflow.Slot<0&&tiny.CapacityFallbacks==1,"capacity fallback failed");
        // Sources can diverge in position without sharing a wrong value.
        pool.Reset();var a=pool.Acquire(10,8,0,2,1,.7f,.3f);var b=pool.Acquire(10,8,0,2,1,.7f,.3f);
        for(int i=0;i<2048;i++)
        {
            float t=(i%7)*.137f;var lease=(i&1)==0?a:b;
            Require(pool.TryEvaluate(lease,t,out var value)&&Bits(value)==Bits(Oracle(2,t,1,.7f,.3f)),"divergent source position alias");comparisons++;
        }
        // Every immutable input and start boundary must distinguish groups.
        var variants=new[]{pool.Acquire(10,9,0,2,1,.7f,.3f),pool.Acquire(10,8,.01f,2,1,.7f,.3f),pool.Acquire(10,8,0,3,1,.7f,.3f),
            pool.Acquire(10,8,0,2,1.1f,.7f,.3f),pool.Acquire(10,8,0,2,1,.71f,.3f),pool.Acquire(10,8,0,2,1,.7f,.31f)};
        foreach(var v in variants)Require(v.Slot!=a.Slot,"immutable parameter key omitted");
        pool.Reset();var older=pool.Acquire(11,8,0,2,1,.7f,.3f);var newer=pool.Acquire(12,8,0,2,1,.7f,.3f);
        pool.Release(older);var newest=pool.Acquire(12,8,0,2,1,.7f,.3f);
        Require(newest.Slot==newer.Slot&&newest.Token==newer.Token,"old release removed current registry entry");
        bool throws=true;var fault=new ExactEaseCohorts((e,t,d,o,p)=>{if(throws&&t==.25f)throw new InvalidOperationException();return t;},2);
        var f=fault.Acquire(1,1,0,1,1,0,0);Require(fault.TryEvaluate(f,0,out _),"fault warmup failed");
        try{fault.TryEvaluate(f,.25f,out _);throw new Exception("evaluator exception swallowed");}catch(InvalidOperationException){}
        throws=false;Require(fault.TryEvaluate(f,.25f,out var recovered)&&recovered==.25f&&fault.Misses==2,"failed evaluation published cache entry");
        // Allocation paths are warmed too. No per-source allocations after setup.
        pool.Reset();for(int i=0;i<2000;i++){var l=pool.Acquire(i,i,0,1,1,.7f,.3f);pool.TryEvaluate(l,.5f,out _);pool.Release(l);}
        long begin=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++){var l=pool.Acquire(i,i,0,1,1,.7f,.3f);pool.TryEvaluate(l,.5f,out _);pool.TryEvaluate(l,.5f,out _);pool.Release(l);}
        long allocated=GC.GetAllocatedBytesForCurrentThread()-begin;Require(allocated==0,"steady cohort allocation");Require(pool.LiveGroups==0,"final retained groups");
        Console.WriteLine(JsonSerializer.Serialize(new{passed=true,comparisons,hits,misses,allocation_bytes=allocated,sibling_continuity=true,stale_read_rejected=true,double_release_rejected=true,reset_epoch_changed=true,capacity_fallback=true,divergent_positions=true,all_parameter_keys=true,old_registry_release=true,exception_not_cached=true,groups_after_release=pool.LiveGroups,scope="cohort cache mechanics; actual DOTween/Mono/game/FPS UNMEASURED"}));
    }
}
