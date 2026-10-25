using System;
using System.Collections.Generic;
using StutterFix;

int checks=0;
void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
var q=new GpuDecoImageWakeQueue();var work=new List<int>();
q.Reserve(4476);Check(q.Pending.Count==0&&q.Watching.Count==0,"preallocation does not enqueue images");
for(int i=0;i<4476;i++)q.Defer(i);
q.Snapshot(work);Check(work.Count==4476,"initial deferred image check");
foreach(int item in work)Check(!q.ShouldFlush(item,0,false),"static alpha stays hidden");
q.Snapshot(work);Check(work.Count==0&&q.Pending.Count==4476,"static pending 4476, steady-state checks zero");
Check(q.AlphaDirty(17),"alpha source start wakes pending image");
q.Snapshot(work);Check(work.Count==1&&work[0]==17,"dirty event checks only one image");
Check(!q.ShouldFlush(17,0,true)&&q.Watching.Contains(17),"zero start retains animated watcher");
Check(!q.ShouldFlush(17,-.3f,true)&&q.Watching.Contains(17),"negative easing overshoot retains watcher");
Check(q.ShouldFlush(17,.001f,true),"next-frame positive alpha flushes without another dirty event");
q.Remove(17);q.Snapshot(work);Check(work.Count==0&&q.Pending.Count==4475,"flushed image removed");
Check(!q.AlphaDirty(17),"later dirty events do not wake flushed images");
q.AlphaDirty(18);q.AlphaDirty(18);q.Snapshot(work);Check(work.Count==1,"two alpha channels deduplicate");
Check(!q.ShouldFlush(18,0,true),"animation still at zero");
Check(!q.ShouldFlush(18,0,false),"completed zero animation sleeps");
q.Snapshot(work);Check(work.Count==0&&q.Pending.Contains(18),"pending remains asleep after animation completion");
q.AlphaDirty(18);Check(q.ShouldFlush(18,1,false),"static positive assignment wakes and flushes");
q.Remove(18);q.Defer(19);q.Defer(19);q.Snapshot(work);Check(work.Count==1,"repeated image overwrite remains one watcher");
q.Remove(19);Check(!q.ShouldFlush(19,1,true),"external flush invalidates snapshot safely");
q.Defer(20);Check(q.ShouldFlush(20,(-.2f)*(-.3f),false),"rendered product sign semantics preserved");
q.Clear();q.Snapshot(work);Check(q.Pending.Count==0&&work.Count==0,"map cleanup has no retained IDs");
Console.WriteLine("imageown_wake_checks="+checks+" failed=0 static_pending=4476 steady_state_checks=0 physical_pixels=UNMEASURED");
