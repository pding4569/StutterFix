using System;
using System.Runtime.InteropServices;
namespace StutterFix
{
    internal static class ThreadScheduling
    {
        [DllImport("kernel32.dll")]private static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")]private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll",SetLastError=true)]private static extern int GetThreadPriority(IntPtr thread);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetThreadPriority(IntPtr thread,int priority);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetProcessAffinityMask(IntPtr process,out UIntPtr processMask,out UIntPtr systemMask);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern UIntPtr SetThreadAffinityMask(IntPtr thread,UIntPtr mask);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetThreadGroupAffinity(IntPtr thread,out GroupAffinity affinity);
        [DllImport("kernel32.dll")]private static extern ushort GetActiveProcessorGroupCount();
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetSystemCpuSetInformation(IntPtr data,uint length,out uint required,IntPtr process,uint flags);
        [DllImport("avrt.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr AvSetMmThreadCharacteristicsW(string task,ref uint index);
        [DllImport("avrt.dll",SetLastError=true)]private static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);
        [StructLayout(LayoutKind.Sequential)]private struct GroupAffinity{public UIntPtr Mask;public ushort Group,R0,R1,R2;}
        private static int mode,lastKey=-1,oldPriority;
        private static bool cores,avoidZero,priorityOwned;
        private static uint owner;
        private static IntPtr mmcss;
        private static ulong oldMask,ownMask;
        private static float nextRestoreAttempt;
        internal static string Status="꺼짐";
        internal static void Configure(int value,bool performanceCores,bool avoidLogicalZero=false)
        {mode=value>=0&&value<=2?value:0;cores=performanceCores;avoidZero=avoidLogicalZero;}
        internal static void Tick()
        {
            int key=Hitch.Playing?mode+(cores?4:0)+(avoidZero?8:0):0;
            if(key==lastKey)return;
            if(UnityEngine.Time.unscaledTime<nextRestoreAttempt)return;
            try
            {
                if(!Restore()){nextRestoreAttempt=UnityEngine.Time.unscaledTime+1f;return;}
                nextRestoreAttempt=0;
                lastKey=key;if(key==0){Status="꺼짐";return;}
                owner=GetCurrentThreadId();var thread=GetCurrentThread();
                Status="main="+owner;
                if(mode==1)
                {
                    oldPriority=GetThreadPriority(thread);
                    if(oldPriority==int.MaxValue||!SetThreadPriority(thread,15))throw new InvalidOperationException("Thread priority unavailable");
                    priorityOwned=true;Status+=" priority=15";
                }
                else if(mode==2)
                {
                    uint index=0;mmcss=AvSetMmThreadCharacteristicsW("Pro Audio",ref index);
                    if(mmcss==IntPtr.Zero)throw new InvalidOperationException("MMCSS Pro Audio unavailable");
                    Status+=" mmcss=True";
                }
                if(cores||avoidZero)ApplyCores(thread);
            }
            catch(Exception ex)
            {
                bool restored=Restore();lastKey=restored?key:-1;
                nextRestoreAttempt=restored?0:UnityEngine.Time.unscaledTime+1f;
                Status="기존 예약 유지: "+ex.Message;
            }
        }
        private static void ApplyCores(IntPtr thread)
        {
            if(GetActiveProcessorGroupCount()!=1){Status+=" pcores=unsupported-groups";return;}
            GroupAffinity group;UIntPtr allowed,system;
            if(!GetThreadGroupAffinity(thread,out group)||group.Group!=0||!GetProcessAffinityMask(GetCurrentProcess(),out allowed,out system))throw new InvalidOperationException("Thread affinity unavailable");
            ulong eligible=allowed.ToUInt64()&group.Mask.ToUInt64();
            if(!cores)
            {
                bool ignored;SetOwnedMask(thread,CpuSetPolicy.SchedulingMask(null,eligible,false,avoidZero,out ignored));
                return;
            }
            uint required;GetSystemCpuSetInformation(IntPtr.Zero,0,out required,GetCurrentProcess(),0);
            if(required==0||required>1024*1024)throw new InvalidOperationException("CPU topology unavailable");
            var memory=Marshal.AllocHGlobal((int)required);
            try
            {
                uint actual;if(!GetSystemCpuSetInformation(memory,required,out actual,GetCurrentProcess(),0)||actual>required)throw new InvalidOperationException("CPU topology changed");
                var bytes=new byte[actual];Marshal.Copy(memory,bytes,0,bytes.Length);
                bool hybrid;ulong mask=CpuSetPolicy.SchedulingMask(bytes,eligible,cores,avoidZero,out hybrid);
                if(mask==0){Status+=hybrid?" pcores=no-eligible-core":" pcores=homogeneous-or-unknown";return;}
                SetOwnedMask(thread,mask);
            }
            finally{Marshal.FreeHGlobal(memory);}
        }
        private static void SetOwnedMask(IntPtr thread,ulong mask)
        {
            if(mask==0){Status+=" affinity=no-eligible-core";return;}
            var prior=SetThreadAffinityMask(thread,new UIntPtr(mask));
            if(prior==UIntPtr.Zero)throw new InvalidOperationException("CPU affinity rejected");
            oldMask=prior.ToUInt64();ownMask=mask;Status+=" affinity-mask="+mask.ToString("X");
        }
        private static bool Restore()
        {
            if(owner==0)return true;
            try
            {
                if(GetCurrentThreadId()!=owner){Status="원래 스레드의 복구 대기";return false;}
                var thread=GetCurrentThread();
                if(ownMask!=0)
                {
                    GroupAffinity group;
                    if(!GetThreadGroupAffinity(thread,out group))return false;
                    if(group.Group==0&&group.Mask.ToUInt64()==ownMask&&SetThreadAffinityMask(thread,new UIntPtr(oldMask))==UIntPtr.Zero)return false;
                    ownMask=oldMask=0;
                }
                if(priorityOwned)
                {
                    if(GetThreadPriority(thread)==15&&!SetThreadPriority(thread,oldPriority))return false;
                    priorityOwned=false;
                }
                if(mmcss!=IntPtr.Zero){if(!AvRevertMmThreadCharacteristics(mmcss))return false;mmcss=IntPtr.Zero;}
                owner=0;return true;
            }
            catch{Status="스레드 예약 복구 확인 안 됨";return false;}
        }
        internal static void Shutdown(){mode=0;cores=avoidZero=false;if(Restore()){lastKey=-1;Status="꺼짐";}}
#if DEV || AUTOTEST
        internal static string Fixture()
        {
            if(Hitch.Playing)throw new InvalidOperationException("Thread fixture is menu-only");
            // Apply to an owned short-lived worker, never the game main/render thread.
            string result=null;var done=new System.Threading.ManualResetEvent(false);
            var worker=new System.Threading.Thread(()=>
            {
                try
                {
                    var thread=GetCurrentThread();int before=GetThreadPriority(thread);
                    if(before==int.MaxValue||!SetThreadPriority(thread,15))throw new InvalidOperationException("Priority test unavailable");
                    bool priority=GetThreadPriority(thread)==15;
                    if(!SetThreadPriority(thread,before)||GetThreadPriority(thread)!=before)throw new InvalidOperationException("Priority test restore failed");
                    uint index=0;var token=AvSetMmThreadCharacteristicsW("Pro Audio",ref index);
                    bool registered=token!=IntPtr.Zero;
                    if(registered&&!AvRevertMmThreadCharacteristics(token))throw new InvalidOperationException("MMCSS test restore failed");
                    result="priority_apply_restore="+priority+" mmcss_apply_restore="+registered+" worker_only=True";
                }
                catch(Exception ex){result="unavailable="+ex.Message;}
                finally{done.Set();}
            });
            worker.IsBackground=true;worker.Start();
            if(!done.WaitOne(3000))return "worker completion pending";
            done.Dispose();return result;
        }
#endif
    }
}
