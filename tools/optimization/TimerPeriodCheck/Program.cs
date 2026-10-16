using System;
using System.Runtime.InteropServices;
using StutterFix;
int checks=0,beginCalls=0,endCalls=0;uint failBegin=0,failEnd=0;bool throwBegin=false,throwEnd=false;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
var lease=new TimerPeriodLease(1,p=>{beginCalls++;Check(p==1,"begin period");if(throwBegin)throw new DllNotFoundException();return failBegin;},p=>{endCalls++;Check(p==1,"end period");if(throwEnd)throw new EntryPointNotFoundException();return failEnd;});
Check(lease.Apply(false)&&!lease.Owned&&beginCalls==0&&endCalls==0,"default off makes no request");
failBegin=97;Check(!lease.Apply(true)&&!lease.Owned,"failed begin does not claim ownership");
Check(lease.Apply(false)&&endCalls==0,"failed begin has no matching release");
failBegin=0;throwBegin=true;Check(!lease.Apply(true)&&!lease.Owned,"missing DLL preserves prior state");
throwBegin=false;Check(lease.Apply(true)&&lease.Owned,"successful begin owned");
int before=beginCalls;Check(lease.Apply(true)&&beginCalls==before,"repeated enabled state adds no request");
failEnd=97;Check(!lease.Apply(false)&&lease.Owned,"failed release retains ownership for retry");
failEnd=0;throwEnd=true;Check(!lease.Apply(false)&&lease.Owned,"missing end API retains ownership");
throwEnd=false;Check(lease.Apply(false)&&!lease.Owned,"release retry succeeds");
before=endCalls;Check(lease.Apply(false)&&endCalls==before,"release idempotent");
string actual="unsupported";
if(OperatingSystem.IsWindows())
{
    var real=new TimerPeriodLease(1,Native.timeBeginPeriod,Native.timeEndPeriod);
    try
    {
        if(real.Apply(true)){Check(real.Owned,"Windows ownership after success");Check(real.Apply(false)&&!real.Owned,"Windows matched release");actual="begin/end success";}
        else actual="request unavailable; fallback";
    }
    finally{real.Apply(false);}
}
Console.WriteLine($"Timer period protocol conditions {checks}, errors 0; isolated process {actual}; NT0.5ms/game latency unverified");
internal static class Native
{
    [DllImport("winmm.dll")]internal static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")]internal static extern uint timeEndPeriod(uint period);
}
