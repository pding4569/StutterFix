using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    internal static unsafe class NativeMotion
    {
        internal const int Capacity=65536, Minimum=512;
        internal static bool Enabled, Failed;
        internal static float[] Start,Change,Position,Duration,Output;
        internal static long Batches,Values,Checks,Mismatch;
        internal static bool Compatible;
        internal static int Rounding;
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        private static extern int sf_motion_linear(float* start,float* change,float* position,float* duration,float* output,int count,int rounding);
        internal static void Configure(bool enabled)
        {
            if(enabled==Enabled)return;
            Reset();
            if(!enabled)return;
            try
            {
                Start=new float[Capacity];Change=new float[Capacity];Position=new float[Capacity];Duration=new float[Capacity];Output=new float[Capacity];
                Enabled=true;
            }
            catch{Reset();}
        }
        internal static bool Run(int count)
        {
            if(!Enabled||Failed||!Compatible||count<Minimum||count>Capacity||!SfNative.Available)return false;
            try
            {
                if(!Invoke(count,Rounding)){Failed=true;return false;}
                Batches++;Values+=count;return true;
            }
            catch{Failed=true;return false;}
        }
        private static bool Invoke(int count,int rounding)
        {
            fixed(float* a=Start,b=Change,c=Position,d=Duration,o=Output)
                return sf_motion_linear(a,b,c,d,o,count,rounding)!=0;
        }
        // Call before gathering animation rows: calibration owns scratch rows.
        internal static bool EnsureCompatible()
        {
            if(!Enabled||Failed||!SfNative.Available||!ZeroTween.CanEase)return false;
            if(Compatible)return true;
            try
            {
                const int n=1024;
                for(int i=0;i<n;i++)
                {
                    Start[i]=(i%997-500)*.37f;Change[i]=(i%499-250)*1.11f;
                    Duration[i]=(i%199+1)*.019f;Position[i]=Duration[i]*(i%101)/100f;
                }
                for(int mode=0;mode<=1;mode++)
                {
                    if(!Invoke(n,mode))break;
                    bool same=true;
                    for(int i=0;i<n;i++)
                    {
                        float e=ZeroTween.Eval(DG.Tweening.Ease.Linear,Position[i],Duration[i],0,0);
                        float expected=Start[i]+Change[i]*e;
                        if(InstantMove.Bits(expected)!=InstantMove.Bits(Output[i])){same=false;break;}
                    }
                    if(same)
                    {
                        Compatible=true;Rounding=mode;
                        Main.Entry.Logger.Log("[네이티브 장식 계산] 반올림 모드 "+mode+", 엔진 대조 "+n+"개 다름 0");
                        return true;
                    }
                }
            }
            catch{}
            Failed=true;
            Main.Entry.Logger.Log("[네이티브 장식 계산] 엔진 대조 실패: 기존 계산 사용");
            return false;
        }
        internal static void Reset()
        {
            Enabled=Failed=Compatible=false;Rounding=0;Start=Change=Position=Duration=Output=null;
            Batches=Values=Checks=Mismatch=0;
        }
    }
}
