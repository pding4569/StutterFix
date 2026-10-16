using System;
using System.Runtime.InteropServices;
using System.Threading;
using StutterFix;

int checks=0;
void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
IntPtr dll=NativeLibrary.Load(System.IO.Path.GetFullPath(args[0]));
var begin=Marshal.GetDelegateForFunctionPointer<Begin>(NativeLibrary.GetExport(dll,"sf_render_thread_capture_begin_v1"));
var take=Marshal.GetDelegateForFunctionPointer<Take>(NativeLibrary.GetExport(dll,"sf_render_thread_capture_take_v1"));
var cancel=Marshal.GetDelegateForFunctionPointer<Cancel>(NativeLibrary.GetExport(dll,"sf_render_thread_capture_cancel_v1"));
var eventPtr=Marshal.GetDelegateForFunctionPointer<Pointer>(NativeLibrary.GetExport(dll,"sf_render_thread_capture_event_ptr_v1"));
var callback=Marshal.GetDelegateForFunctionPointer<Callback>(eventPtr());
IntPtr owned=IntPtr.Zero;RenderThreadLease lease=null;
using var ready=new AutoResetEvent(false);using var go=new AutoResetEvent(false);using var done=new AutoResetEvent(false);
int command=0,generation=0,workerPriority=0;ulong workerMask=0;uint workerTid=0;Exception workerError=null;
var worker=new Thread(()=>
{
    workerTid=Native.GetCurrentThreadId();ready.Set();
    try
    {
        while(true)
        {
            go.WaitOne();if(command==9)break;
            if(command==1)callback(generation);
            if(command==2){Native.GetThreadGroupAffinity(Native.GetCurrentThread(),out var a);workerMask=a.Mask.ToUInt64();workerPriority=Native.GetThreadPriority(Native.GetCurrentThread());}
            if(command==3)Native.SetThreadPriority(Native.GetCurrentThread(),-1);
            done.Set();
        }
    }
    catch(Exception ex){workerError=ex;done.Set();}
});worker.IsBackground=true;worker.Start();
void Call(int c){command=c;go.Set();if(!done.WaitOne(3000))throw new Exception("owned worker timeout");if(workerError!=null)throw workerError;}
try
{
    Check(ready.WaitOne(3000),"owned worker started");
    Call(2);int priorityBefore=workerPriority;ulong maskBefore=workerMask;
    int first=begin();Check(first>0&&take(first)==IntPtr.Zero,"no handle before render event");
    cancel(first);generation=first;Call(1);Check(take(first)==IntPtr.Zero,"cancelled queued event ignored");
    int old=begin();int current=begin();generation=old;Call(1);Check(take(current)==IntPtr.Zero,"older generation cannot capture");
    cancel(old);generation=current;Call(1);owned=take(current);
    Check(owned!=IntPtr.Zero&&Native.GetThreadId(owned)==workerTid,"actual worker object captured");
    Check(take(current)==IntPtr.Zero,"single transfer of handle");
    Call(1);Check(take(current)==IntPtr.Zero,"duplicate event does not leak another handle");
    lease=new RenderThreadLease(owned);owned=IntPtr.Zero;
    ulong selected=maskBefore&~1UL;
    Check(lease.Apply(true,selected),"priority and subset affinity apply from other thread");
    Call(2);Check(workerPriority==15&&(selected==0||workerMask==selected),"worker observes applied values");
    Check(lease.Restore()&&!lease.Owned,"restore and close from main thread");
    Call(2);Check(workerPriority==priorityBefore&&workerMask==maskBefore,"worker observes exact originals");
    Check(lease.Restore(),"repeated restore idempotent");
    generation=begin();Call(1);owned=take(generation);lease=new RenderThreadLease(owned);owned=IntPtr.Zero;
    Check(lease.Apply(true,0),"second priority lease");Call(3);Check(lease.Restore(),"foreign priority restore path");
    Call(2);Check(workerPriority==-1,"foreign priority preserved");
    var repair=Native.OpenThread(0x20,false,workerTid);
    try{Check(repair!=IntPtr.Zero&&Native.SetThreadPriority(repair,priorityBefore),"restore fixture's foreign replacement");}
    finally{if(repair!=IntPtr.Zero)Native.CloseHandle(repair);}
    generation=begin();Call(1);owned=take(generation);lease=new RenderThreadLease(owned);owned=IntPtr.Zero;
    Check(!lease.Apply(true,ulong.MaxValue),"reject an affinity outside thread/process eligibility");
    Check(lease.Restore(),"partial priority application rolled back after mask rejection");
    Call(2);Check(workerPriority==priorityBefore&&workerMask==maskBefore,"partial failure leaves originals");
    if((selected&(selected-1))!=0)
    {
        generation=begin();Call(1);owned=take(generation);lease=new RenderThreadLease(owned);owned=IntPtr.Zero;
        Check(lease.Apply(false,selected),"affinity-only lease");
        ulong foreign=selected&(~selected+1);
        var observer=Native.OpenThread(0x60,false,workerTid);
        try
        {
            Check(observer!=IntPtr.Zero&&Native.SetThreadAffinityMask(observer,new UIntPtr(foreign))!=UIntPtr.Zero,"fixture installs foreign affinity");
            Check(lease.Restore(),"affinity foreign restore path");Call(2);Check(workerMask==foreign,"foreign affinity preserved");
            Check(Native.SetThreadAffinityMask(observer,new UIntPtr(maskBefore))!=UIntPtr.Zero,"fixture repairs its own affinity");
        }
        finally{if(observer!=IntPtr.Zero)Native.CloseHandle(observer);}
    }
    // Cancellation closes the unconsumed native handle. Check bounded handle usage
    // across many stale/current event sequences (DLL load/JIT happen before count).
    Native.GetProcessHandleCount(Native.GetCurrentProcess(),out uint before);
    for(int i=0;i<512;i++){generation=begin();Call(1);cancel(generation);Check(take(generation)==IntPtr.Zero,"cancel closes captured handle");}
    Native.GetProcessHandleCount(Native.GetCurrentProcess(),out uint after);
    Check(after<=before,"no per-generation kernel handle growth");
    generation=begin();Call(1);owned=take(generation);lease=new RenderThreadLease(owned);owned=IntPtr.Zero;
    command=9;go.Set();Check(worker.Join(3000),"owned worker exits");Check(lease.Restore()&&!lease.Owned,"exited thread handle safely released");
    Console.WriteLine($"Render capture/lease conditions {checks}, errors 0; main/render game scheduling changed 0; MMCSS render implemented 0");
}
finally
{
    cancel(generation);lease?.Restore();if(owned!=IntPtr.Zero)Native.CloseHandle(owned);
    if(worker.IsAlive){command=9;go.Set();worker.Join(3000);}
    NativeLibrary.Free(dll);
}
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Begin();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Take(int id);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Cancel(int id);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Pointer();
[UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate void Callback(int id);
static class Native
{
    [StructLayout(LayoutKind.Sequential)]internal struct Affinity{internal UIntPtr Mask;internal ushort Group,R0,R1,R2;}
    [DllImport("kernel32.dll")]internal static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")]internal static extern uint GetThreadId(IntPtr thread);
    [DllImport("kernel32.dll")]internal static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")]internal static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")]internal static extern int GetThreadPriority(IntPtr h);
    [DllImport("kernel32.dll")]internal static extern bool SetThreadPriority(IntPtr h,int p);
    [DllImport("kernel32.dll")]internal static extern bool GetThreadGroupAffinity(IntPtr h,out Affinity a);
    [DllImport("kernel32.dll")]internal static extern UIntPtr SetThreadAffinityMask(IntPtr h,UIntPtr mask);
    [DllImport("kernel32.dll")]internal static extern IntPtr OpenThread(uint access,bool inherit,uint id);
    [DllImport("kernel32.dll")]internal static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")]internal static extern bool GetProcessHandleCount(IntPtr h,out uint count);
}
