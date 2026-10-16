using System;
using StutterFix;
int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
var original=new WorkingSetBounds{Minimum=204800,Maximum=1413120,Flags=0};WorkingSetBounds plan;
Check(PageLockLease.Plan(original,1310720,1UL<<30,out plan)&&plan.Minimum==1581056&&plan.Maximum==1646592&&plan.Flags==1,"bounded working set plan");
Check(!PageLockLease.Plan(original,1310720,128UL<<20,out plan),"low RAM refuses locking");
Check(!PageLockLease.Plan(original,5*1024*1024,1UL<<30,out plan),"lock budget limited to4MiB");
var hard=original;hard.Flags=4;Check(!PageLockLease.Plan(hard,1310720,1UL<<30,out plan),"does not increase foreign hard maximum");
var api=new Fake{Current=original};var lease=PageLockLease.Create(api,1310720);
Check(lease.Pointer!=IntPtr.Zero&&api.Locked&&api.Current.Flags==1,"owned pages allocated and locked");
lease.Dispose();Check(lease.Released&&!api.Locked&&api.Current.Same(original)&&api.Frees==1,"exact working set restoration");
lease.Dispose();Check(api.Frees==1,"double free avoided");
foreach(string fault in new[]{"query","quota","allocation","lock"})
{
 api=new Fake{Current=original,Fault=fault};lease=PageLockLease.Create(api,1310720);
 Check(lease.Pointer==IntPtr.Zero&&lease.Released&&api.Current.Same(original),"safe partial failure "+fault);
}
api=new Fake{Current=original};lease=PageLockLease.Create(api,1310720);api.Current.Minimum+=4096;var foreign=api.Current;lease.Dispose();
Check(lease.Released&&api.Current.Same(foreign),"foreign quota change preserved");
api=new Fake{Current=original};lease=PageLockLease.Create(api,1310720);api.Fault="free";lease.Dispose();Check(!lease.Released&&lease.Pointer!=IntPtr.Zero,"failed free retains cleanup ownership");
api.Fault="";lease.Dispose();Check(lease.Released&&api.Current.Same(original),"failed free can retry");
api=new Fake{Current=original};lease=PageLockLease.Create(api,1310720);api.Fault="query";lease.Dispose();Check(!lease.Released&&lease.Pointer==IntPtr.Zero,"failed quota query retains cleanup ownership");
api.Fault="";lease.Dispose();Check(lease.Released&&api.Current.Same(original),"quota cleanup can retry");
Console.WriteLine($"Private page lock conditions {n}, errors 0; Windows actual locking and game impact separately required");
sealed class Fake:IPageLockApi
{
 internal WorkingSetBounds Current;internal string Fault="";internal bool Locked;internal int Frees;
 public ulong AvailableBytes=>1UL<<30;
 public bool GetWorkingSet(out WorkingSetBounds b){b=Current;return Fault!="query";}
 public bool SetWorkingSet(WorkingSetBounds b){if(Fault=="quota")return false;Current=b;return true;}
 public IntPtr Allocate(int bytes)=>Fault=="allocation"?IntPtr.Zero:new IntPtr(4096);
 public bool Free(IntPtr p){Frees++;return Fault!="free";}
 public bool Lock(IntPtr p,int bytes){if(Fault=="lock")return false;Locked=true;return true;}
 public bool Unlock(IntPtr p,int bytes){Locked=false;return true;}
}
