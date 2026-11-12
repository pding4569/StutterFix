using System;
using System.Reflection;
using System.Text.Json;
using StutterFix;

class Program
{
    static long comparisons,calls;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static int Bits(float v)=>BitConverter.SingleToInt32Bits(v);
    static float Oracle(int e,float t,float d,float o,float p)
    {calls++;return e%3==0?t:e%3==1?(float)(Math.Sin((double)t/d+o)*p):(float)((double)t*t/d+o-p);}
    static void Check(ExactEaseMemo memo,int e,float t,float d,float o,float p)
    {float expected=Oracle(e,t,d,o,p),actual=memo.Evaluate(e,t,d,o,p);Require(Bits(expected)==Bits(actual),"bit mismatch");comparisons++;}
    static void Main()
    {
        var memo=new ExactEaseMemo(Oracle);var random=new Random(871313);
        float[] special={0f,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),float.Epsilon,-float.Epsilon,1f,-1f,float.MaxValue,float.MinValue,float.PositiveInfinity,float.NegativeInfinity,
            BitConverter.Int32BitsToSingle(0x7fc00001),BitConverter.Int32BitsToSingle(0x7fc00002)};
        foreach(float t in special)foreach(float d in special)foreach(float o in special)foreach(float p in special)
        {memo.BeginFrame();Check(memo,0,t,d,o,p);Check(memo,0,t,d,o,p);}
        for(int frame=0;frame<200;frame++)
        {
            memo.BeginFrame();
            for(int j=0;j<512;j++)
            {
                int e=random.Next(54);float t=(float)random.NextDouble()*8,d=.01f+(float)random.NextDouble()*4,o=(float)random.NextDouble()*2,p=(float)random.NextDouble();
                Check(memo,e,t,d,o,p);Check(memo,e,t,d,o,p);Check(memo,e+1,t,d,o,p);Check(memo,e,t,d,o,p);
            }
        }
        // Every key component, collisions, per-frame invalidation and failed
        // evaluation are tested independently of the table's hashing details.
        int invoked=0;var exact=new ExactEaseMemo((e,t,d,o,p)=>{invoked++;return t;});exact.BeginFrame();
        exact.Evaluate(1,0,1,1,1);exact.Evaluate(1,0,1,1,1);Require(invoked==1,"no memo hit");
        exact.Evaluate(2,0,1,1,1);exact.Evaluate(1,-1,1,1,1);exact.Evaluate(1,0,2,1,1);exact.Evaluate(1,0,1,2,1);exact.Evaluate(1,0,1,1,2);Require(invoked==6,"key field omitted");
        exact.BeginFrame();exact.Evaluate(1,0,1,1,1);Require(invoked==7,"stale generation");
        exact.Evaluate(1,special[1],1,1,1);Require(invoked==8,"signed zero aliased");
        bool throwing=true;var fault=new ExactEaseMemo((e,t,d,o,p)=>{if(throwing)throw new InvalidOperationException();return t;});fault.BeginFrame();
        try{fault.Evaluate(1,7,1,1,1);throw new Exception("exception swallowed");}catch(InvalidOperationException){}
        throwing=false;Require(fault.Evaluate(1,7,1,1,1)==7 && fault.Hits==0,"failed entry published");
        // Warm and measure only cache calls; test harness and oracle allocation
        // are outside this range. This is .NET 8 allocation evidence, not Mono FPS.
        exact.BeginFrame();for(int i=0;i<10000;i++)exact.Evaluate(1,0,1,1,1);
        long begin=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100000;i++)exact.Evaluate(1,0,1,1,1);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-begin;Require(allocated==0,"cache allocated");
        Console.WriteLine(JsonSerializer.Serialize(new{passed=true,comparisons,hits=memo.Hits,misses=memo.Misses,allocation_bytes=allocated,exception_propagated=true,signed_zero_distinct=true,generation_invalidated=true,scope="cache mechanics; actual DOTween/game pixels/FPS UNMEASURED"}));
    }
}
