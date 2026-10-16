using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    internal static unsafe class NativeMotion
    {
        internal const int Capacity=65536, Minimum=512;
        internal static bool Enabled, Failed;
        private static float[] start,change,position,duration,output;
        private static AlignedArena arena;
        private static IntPtr startPtr,changePtr,positionPtr,durationPtr,outputPtr;
        internal static bool ArenaRequested;
        internal static bool PinRequested;
        internal static bool ArenaActive=>arena!=null;
        internal static bool NativeArenaActive=>arena!=null&&arena.NativeAllocated;
        internal static bool ArenaPinned=>arena!=null&&PinRequested&&PinnedArenaOwner.Owns(arena.Pointer);
        internal static int ArenaAlignment=>arena==null?-1:(int)((startPtr.ToInt64()|changePtr.ToInt64()|positionPtr.ToInt64()|durationPtr.ToInt64()|outputPtr.ToInt64())&63);
        internal static Span<float> Start=>arena!=null?new Span<float>((void*)startPtr,Capacity):start;
        internal static Span<float> Change=>arena!=null?new Span<float>((void*)changePtr,Capacity):change;
        internal static Span<float> Position=>arena!=null?new Span<float>((void*)positionPtr,Capacity):position;
        internal static Span<float> Duration=>arena!=null?new Span<float>((void*)durationPtr,Capacity):duration;
        internal static Span<float> Output=>arena!=null?new Span<float>((void*)outputPtr,Capacity):output;
        internal static long Batches,Values,Checks,Mismatch;
        internal static bool Compatible;
        internal static int Rounding;
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        private static extern int sf_motion_linear(float* start,float* change,float* position,float* duration,float* output,int count,int rounding);
        internal static void Configure(bool enabled,bool useArena=false,bool pinArena=false)
        {
            bool wantedPin=enabled&&useArena&&pinArena;
            if(enabled==Enabled&&(!enabled||(ArenaRequested==useArena&&PinRequested==wantedPin)))return;
            Reset();
            if(!enabled)return;
            try
            {
                ArenaRequested=useArena;
                PinRequested=wantedPin;
                if(useArena)
                {
                    try{arena=CreateArena();ResetArena();}
                    catch{if(arena!=null)arena.Dispose();arena=null;}
                }
                if(arena==null)
                {start=new float[Capacity];change=new float[Capacity];position=new float[Capacity];duration=new float[Capacity];output=new float[Capacity];}
                Enabled=true;
            }
            catch{Reset();}
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)]private static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern IntPtr sf_alloc_aligned64_v1(UIntPtr bytes);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern void sf_free_aligned64_v1(IntPtr pointer);
        private static AlignedArena CreateArena()
        {
            try
            {
                if(PinRequested)
                {
                    var pointer=PinnedArenaOwner.Allocate(Capacity*5*sizeof(float));
                    if(pointer!=IntPtr.Zero)
                        return new AlignedArena(Capacity*5*sizeof(float),bytes=>pointer,PinnedArenaOwner.Release);
                }
                // ApplyConfig runs before image-module installation on a fresh start.
                // The optional native arena depends on the DLL being initialized first.
                if(!SfNative.Ready&&Main.Entry!=null)
                    SfNative.Init(System.IO.Path.Combine(Main.Entry.Path,"sfnative.dll"));
                if(SfNative.Available)
                {
                    var module=GetModuleHandleW("sfnative.dll");
                    if(module!=IntPtr.Zero&&GetProcAddress(module,"sf_alloc_aligned64_v1")!=IntPtr.Zero&&GetProcAddress(module,"sf_free_aligned64_v1")!=IntPtr.Zero)
                        return new AlignedArena(Capacity*5*sizeof(float),bytes=>sf_alloc_aligned64_v1(new UIntPtr((uint)bytes)),sf_free_aligned64_v1);
                }
            }
            catch{}
            return new AlignedArena(Capacity*5*sizeof(float));
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
            bool pinCleanup=PinRequested;
            Enabled=Failed=Compatible=ArenaRequested=false;Rounding=0;start=change=position=duration=output=null;
            if(arena!=null)arena.Dispose();arena=null;
            if(pinCleanup)PinnedArenaOwner.RetryCleanup();PinRequested=false;
            startPtr=changePtr=positionPtr=durationPtr=outputPtr=IntPtr.Zero;
            Batches=Values=Checks=Mismatch=0;
        }
        internal static void ResetArena()
        {
            if(arena==null)return;
            arena.Reset();int bytes=Capacity*sizeof(float);
            startPtr=arena.Allocate(bytes);changePtr=arena.Allocate(bytes);positionPtr=arena.Allocate(bytes);
            durationPtr=arena.Allocate(bytes);outputPtr=arena.Allocate(bytes);
            if(outputPtr==IntPtr.Zero)throw new InvalidOperationException("Motion arena capacity mismatch");
        }
    }
}
