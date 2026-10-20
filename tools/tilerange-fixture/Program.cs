using System;
using System.Collections.Generic;
using System.Diagnostics;
using StutterFix;

class Program
{
    static long assertions, applies;
    static void Check(bool condition, string reason) { assertions++; if (!condition) throw new Exception(reason); }
    static int Bits(float v) => BitConverter.SingleToInt32Bits(v);
    static bool Approximately(float a, float b) => Math.Abs(b-a) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon*8);
    static void Write(float[] values, bool[] flags, int i, float value)
    { if (!Approximately(values[i], value)) { values[i] = value; flags[i] = true; } }
    static void Case(int count, int seed)
    {
        var random = new Random(seed); var ledger = new FloorRangeLedger(count);
        var actual = new float[count]; var reference = new float[count]; var flags = new bool[count]; var referenceFlags = new bool[count];
        for (int i=0;i<count;i++) actual[i]=reference[i]=(float)(random.NextDouble()*2-1);
        Action<int,float> apply = (i,v) => { applies++; Write(actual,flags,i,v); };
        float[] special = { 0, -0f, 1, -1, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon, 0.99999994f };
        for (int e=0;e<220;e++)
        {
            int start=random.Next(count), end=random.Next(count); if(start>end) (start,end)=(end,start);
            float v=e%4==0 ? special[random.Next(special.Length)] : (float)random.NextDouble();
            for (int i=start;i<=end;i++) Write(reference,referenceFlags,i,v);
            for (int ch=start/64;ch<=end/64;ch++)
            {
                var entry = new FloorRangeLedger.Entry { Start=start, End=end, Opacity=v };
                if (!ledger.Append(ch,entry))
                    for(int i=Math.Max(start,ch*64);i<=Math.Min(end,ch*64+63);i++) { ledger.Drain(i,apply); Write(actual,flags,i,v); }
            }
            // Random reads/writes, visibility transitions, repeated reads, alias fences.
            for(int k=0;k<7;k++)
            {
                int i=random.Next(count); ledger.Drain(i,apply); ledger.Drain(i,apply);
                Check(Bits(actual[i])==Bits(reference[i]) && flags[i]==referenceFlags[i],"interleaved read");
                if(k==0) { actual[i]=reference[i]=special[random.Next(special.Length)]; flags[i]=referenceFlags[i]=false; }
                if(k==1 && e%17==0) ledger.Chunks[i/64].Blocked=true;
            }
        }
        for(int i=count-1;i>=0;i--) { ledger.Drain(i,apply); Check(Bits(actual[i])==Bits(reference[i]) && flags[i]==referenceFlags[i],"final flush"); }
        foreach(var c in ledger.Chunks) Check(c.Entries.Count==0 && c.Remaining==0,"history released");
    }
    static void Main()
    {
        foreach(int n in new[]{1,63,64,65,127,128,129,1000,8171,8933,90000}) Case(n,n);
        // Fault injection: failed callbacks cannot advance their cursor or lose work.
        var ledger=new FloorRangeLedger(65);
        Check(ledger.Append(0,new FloorRangeLedger.Entry{Start=0,End=64,Opacity=1}),"append");
        bool fault=true; int calls=0;
        Action<int,float> apply=(i,v)=>{calls++;if(fault){fault=false;throw new Exception("injected");}};
        try {ledger.Drain(0,apply);}catch(Exception ex)when(ex.Message=="injected"){}
        Check(ledger.Chunks[0].Cursor[0]==0,"fault cursor"); ledger.Drain(0,apply); Check(calls==2,"fault retry");
        for(int i=1;i<64;i++) ledger.Drain(i,apply);
        for(int i=0;i<32;i++) Check(ledger.Append(0,new FloorRangeLedger.Entry{Start=0,End=63,Opacity=i}),"bounded append");
        Check(!ledger.Append(0,new FloorRangeLedger.Entry()),"depth fallback");
        var bench=new FloorRangeLedger(90000); var watch=Stopwatch.StartNew();
        for(int ch=0;ch<bench.Chunks.Length;ch++) Check(bench.Append(ch,new FloorRangeLedger.Entry{Start=0,End=89999,Opacity=1}),"bench append");
        watch.Stop();
        Console.WriteLine($"PASS assertions={assertions} applies={applies}; 90000 tiles => {bench.Chunks.Length} chunk appends, {watch.Elapsed.TotalMilliseconds:F3}ms (.NET8, not Unity/Mono/game performance)");
    }
}
