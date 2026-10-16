using System;
namespace StutterFix
{
    // Software delivery -> polling only. No USB polling or hardware-switch estimate.
    internal static class FrameQueueDelayPolicy
    {
        internal static bool TryCorrect(long receivedQpc,long polledQpc,long frequency,long polledUtc,
            bool alreadyTimestamped,bool focused,bool playing,out long correctedUtc,out double delayMs)
        {
            correctedUtc=polledUtc;delayMs=0;
            if(alreadyTimestamped||!focused||!playing||frequency<=0||receivedQpc<=0||polledQpc<receivedQpc)return false;
            long elapsed=polledQpc-receivedQpc;
            // Stale/ambiguous events are rejected, never clamped into a plausible hit.
            if(elapsed>frequency/4)return false;
            long ticks=(long)(elapsed*(10000000.0/frequency));
            if(ticks<0||ticks>polledUtc)return false;
            correctedUtc=polledUtc-ticks;delayMs=elapsed*1000.0/frequency;return true;
        }
    }
}
