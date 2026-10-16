using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace StutterFix
{
    internal static class RenderThreadScheduling
    {
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern int sf_render_thread_capture_begin_v1();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern IntPtr sf_render_thread_capture_event_ptr_v1();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern IntPtr sf_render_thread_capture_take_v1(int generation);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern void sf_render_thread_capture_cancel_v1(int generation);
        [DllImport("kernel32.dll")]private static extern uint GetThreadId(IntPtr handle);
        [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
        private static bool enabled,cores,avoidZero;
        private static int mode,lastKey=-1,pending;
        private static float nextPoll,expires,retry;
        private static RenderThreadLease lease;
#if DEV || AUTOTEST
        private static string beforeFixture="",afterFixture="";
#endif
        internal static string Status="꺼짐";
        internal static void Configure(bool requested,int schedulingMode,bool performanceCores,bool excludeZero)
        {
            if(enabled!=requested||mode!=schedulingMode||cores!=performanceCores||avoidZero!=excludeZero)lastKey=-1;
            enabled=requested;mode=schedulingMode;cores=performanceCores;avoidZero=excludeZero;
        }
        internal static void Tick()
        {
            if(!enabled&&lease==null&&pending==0)return;
            int key=enabled&&Hitch.Playing?((mode==1?1:0)+(cores?4:0)+(avoidZero?8:0)):0;
            if(key==lastKey&&pending==0)return;
            float now=Time.unscaledTime;
            if(now<retry)return;
            try
            {
                if(key!=lastKey)
                {
                    if(!Release()){retry=now+1;Status="렌더 스레드 복구 재시도 대기";return;}
                    lastKey=key;
                    if(key==0){Status=enabled&&mode==2?"오디오 예약은 메인 스레드만 적용":"꺼짐";return;}
                    IntPtr callback=sf_render_thread_capture_event_ptr_v1();
                    if(callback==IntPtr.Zero)throw new InvalidOperationException("Native callback unavailable");
                    pending=sf_render_thread_capture_begin_v1();
                    if(pending<=0)throw new InvalidOperationException("Native capture unavailable");
                    GL.IssuePluginEvent(callback,pending); // native code only; no managed delegate/data lifetime
                    expires=now+3;nextPoll=now+.05f;Status="렌더 스레드 연결 대기";return;
                }
                if(pending==0||now<nextPoll)return;
                nextPoll=now+.05f;
                IntPtr thread=sf_render_thread_capture_take_v1(pending);
                if(thread==IntPtr.Zero)
                {
                    if(now>=expires)throw new InvalidOperationException("Render callback timeout");
                    return;
                }
                lease=new RenderThreadLease(thread); // transfer before any later API can fail
#if DEV || AUTOTEST
                beforeFixture=lease.SnapshotForFixture();afterFixture="";
#endif
                uint tid=GetThreadId(thread);
                if(tid==0||tid==GetCurrentThreadId())throw new InvalidOperationException("Separate render thread unavailable");
                sf_render_thread_capture_cancel_v1(pending);pending=0;
                string detail="";ulong mask=cores||avoidZero?ThreadScheduling.SelectMask(thread,cores,avoidZero,out detail):0;
                if(!lease.Apply((key&1)!=0,mask))throw new InvalidOperationException("Render scheduling rejected");
                Status="render="+tid+" priority="+((key&1)!=0?"15":"unchanged")+" mask="+mask.ToString("X")+" "+detail;
            }
            catch(Exception ex)
            {
                bool restored=Release();lastKey=restored?key:-1;retry=restored?0:now+1;
                Status="기존 렌더 예약 유지: "+ex.Message;
            }
        }
        private static bool Release()
        {
            try
            {
                if(pending!=0){sf_render_thread_capture_cancel_v1(pending);pending=0;}
                if(lease!=null)
                {
                    if(!lease.Restore())return false;
#if DEV || AUTOTEST
                    afterFixture=lease.LastAfterForFixture;
#endif
                    lease=null;
                }
                return true;
            }
            catch{return false;}
        }
        internal static void Shutdown()
        {enabled=false;if(Release()){lastKey=-1;Status="꺼짐";}else Status="렌더 스레드 복구 확인 안 됨";}
#if DEV || AUTOTEST
        internal static string FixtureState=>Status+" owned="+(lease!=null&&lease.Owned)+" pending="+pending+" requested="+enabled+" before=["+beforeFixture+"] current=["+(lease==null?"closed":lease.SnapshotForFixture())+"] restored=["+afterFixture+"]";
#endif
    }
}
