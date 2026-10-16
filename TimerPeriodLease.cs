using System;
namespace StutterFix
{
    // Matched WinMM requests only; no process-global NT override.
    internal sealed class TimerPeriodLease
    {
        private readonly uint period;
        private readonly Func<uint,uint> begin,end;
        internal bool Owned { get; private set; }
        internal TimerPeriodLease(uint period,Func<uint,uint> begin,Func<uint,uint> end)
        {
            if(period==0||begin==null||end==null)throw new ArgumentException("Timer period/delegates required");
            this.period=period;this.begin=begin;this.end=end;
        }
        internal bool Apply(bool requested)
        {
            if(Owned==requested)return true;
            try
            {
                if((requested?begin:end)(period)!=0)return false;
                Owned=requested;return true;
            }
            catch{return false;}
        }
    }
}
