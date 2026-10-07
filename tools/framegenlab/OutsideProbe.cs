using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.LowLevel;
namespace StutterFix
{
    // Chapter 14 only. The entire filtered scene, including both planets, is reprojected.
    // Unity's camera scheduling is untouched; no draw-function interception.
    internal static class FrameGenOutsideProbe
    {
        [StructLayout(LayoutKind.Sequential)] private struct Pose { public float x,y,size,angle,rx,ry,rs,ra,bx,by,bs,ba; }
        [StructLayout(LayoutKind.Sequential)] private struct Packet {
            public IntPtr world,unused1,unused2;
            public Vector4 baseCamera;
            public Pose pose;
            public double song;
            public int frame,mode,measure,flip,linear,capture;
            public Vector4 pulse;
        }
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] private static extern int sf_outside_setup(string directory);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr sf_outside_event_ptr();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_outside_status();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_outside_error();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_outside_stopped();
        private static readonly AccessTools.FieldRef<scrCamera,Camera> Cam=AccessTools.FieldRefAccess<scrCamera,Camera>("camobj");
        private static readonly AccessTools.FieldRef<scrCamera,Camera> Overlay=AccessTools.FieldRefAccess<scrCamera,Camera>("Overlaycam");
        private static readonly AccessTools.FieldRef<scrCamera,RenderTexture> RT=AccessTools.FieldRefAccess<scrCamera,RenderTexture>("camRT");
        private static readonly AccessTools.FieldRef<scrCamera,Vector2> Shake=AccessTools.FieldRefAccess<scrCamera,Vector2>("shake");
        private static readonly AccessTools.FieldRef<scrCamera,float> Zoom=AccessTools.FieldRefAccess<scrCamera,float>("zoomSize");
        private static readonly AccessTools.FieldRef<scrCamera,float> UserSize=AccessTools.FieldRefAccess<scrCamera,float>("userSizeMultiplier");
        private static readonly AccessTools.FieldRef<scrCamera,float> PulseFrom=AccessTools.FieldRefAccess<scrCamera,float>("fromsize");
        private static readonly AccessTools.FieldRef<scrCamera,float> PulseTo=AccessTools.FieldRefAccess<scrCamera,float>("tosize");
        private static readonly AccessTools.FieldRef<scrCamera,float> PulseTime=AccessTools.FieldRefAccess<scrCamera,float>("pulsetimer");
        private static readonly AccessTools.FieldRef<scrCamera,float> PulseDuration=AccessTools.FieldRefAccess<scrCamera,float>("sizeTweenTime");
        private static Runner runner;
        private static CommandBuffer command;
        private static IntPtr callback;
        private static IntPtr[] packets;
        private static int packetIndex,oldMode=-1;
        private static bool finished,failed;
        private static Camera worldCamera,trackedCamera;
        private static RenderTexture scene,world;
        private static IntPtr texture;
        private static int renderedFrame=-1,missingScenes;
        private static int finalBeginFrame=-1;
        private static Vector4 sceneCamera,sceneBase,scenePulse;
        private static readonly WaitForEndOfFrame End=new WaitForEndOfFrame();
        private struct FrameStartMarker { }
        private static void BeginFrame() { if(!Main.Config.FrameGenNarrow && !finished && callback!=IntPtr.Zero) Issue(5,IntPtr.Zero); }
        private static void PreRender(Camera c) {
            if(!Main.Config.FrameGenNarrow || finished || failed || c.targetTexture!=null || finalBeginFrame==Time.frameCount) return;
            finalBeginFrame=Time.frameCount; Issue(5,IntPtr.Zero);
        }
        private static PlayerLoopSystem InsertFrameStart(PlayerLoopSystem loop,ref int found) {
            if(loop.subSystemList==null) return loop;
            var children=new List<PlayerLoopSystem>();
            foreach(var child in loop.subSystemList) {
                if(child.type==typeof(UnityEngine.PlayerLoop.PostLateUpdate.PlayerSendFrameStarted)) {
                    children.Add(new PlayerLoopSystem {type=typeof(FrameStartMarker),updateDelegate=BeginFrame}); found++;
                }
                children.Add(InsertFrameStart(child,ref found));
            }
            loop.subSystemList=children.ToArray(); return loop;
        }
        private static PlayerLoopSystem RemoveFrameStart(PlayerLoopSystem loop) {
            if(loop.subSystemList==null) return loop;
            var children=new List<PlayerLoopSystem>();
            foreach(var child in loop.subSystemList) if(child.type!=typeof(FrameStartMarker)) children.Add(RemoveFrameStart(child));
            loop.subSystemList=children.ToArray(); return loop;
        }
        internal static void Install() {
            if(runner!=null) return;
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11) { Log("D3D11 required; feature disabled"); return; }
            if(Marshal.SizeOf<Packet>()!=136) throw new InvalidOperationException("Outside packet ABI");
            string directory=Path.Combine(Main.Entry.Path,"framegen-outside"); Directory.CreateDirectory(directory);
            if(sf_outside_setup(directory)==0) { Log("previous shutdown pending; feature disabled"); return; }
            callback=sf_outside_event_ptr(); command=new CommandBuffer {name="FrameGen included snapshot"};
            packets=new IntPtr[512]; for(int i=0;i<packets.Length;i++) packets[i]=Marshal.AllocHGlobal(136);
            finished=failed=false; oldMode=-1; packetIndex=missingScenes=0; renderedFrame=-1; trackedCamera=null;
            finalBeginFrame=-1;
            sceneCamera=sceneBase=scenePulse=Vector4.zero;
            Camera.onPostRender+=PostRender;
            Camera.onPreRender+=PreRender;
            Issue(0,IntPtr.Zero);
            var loop=PlayerLoop.GetCurrentPlayerLoop(); int found=0; loop=InsertFrameStart(loop,ref found);
            if(found!=1) throw new InvalidOperationException("PlayerSendFrameStarted boundary unavailable; stop the experiment");
            PlayerLoop.SetPlayerLoop(loop);
            var go=new GameObject("StutterFix.FrameGenIncluded") {hideFlags=HideFlags.HideAndDontSave}; UnityEngine.Object.DontDestroyOnLoad(go); runner=go.AddComponent<Runner>(); runner.StartCoroutine(Loop());
            Log("default OFF; FrameGenOutside=0/2/4; planets included; original swapchain only; no draw hooks; plugin begin -> original Present -> unlock");
        }
        internal static void DrawGUI() {
            GUILayout.Label("공 포함 프레임 생성 (연구 빌드 전용)");
            GUILayout.BeginHorizontal(); foreach(int n in new[]{0,2,4}) if(GUILayout.Button(n==0?"끔":n+"배")) Main.Config.FrameGenOutside=n; GUILayout.EndHorizontal();
            GUILayout.Label("F8: 끔 / 2배 / 4배. 공은 진짜 프레임에서만 움직입니다.");
        }
        private static void Late() {
            if(finished || failed) return;
            if(Hitch.Playing && scrCamera.instance!=null) trackedCamera=Cam(scrCamera.instance);
            if(Input.GetKeyDown(KeyCode.F8)) Main.Config.FrameGenOutside=Main.Config.FrameGenOutside==0?2:Main.Config.FrameGenOutside==2?4:0;
            if(sf_outside_status()<0) { failed=true; Log("native failure="+sf_outside_error()+"; experiment stopped"); return; }
            int mode=Hitch.Playing && sf_outside_status()==1?Main.Config.FrameGenOutside:0;
            if(mode!=2 && mode!=4) mode=0;
            if(mode!=oldMode) { oldMode=mode; Log("mode="+mode); }
            if(mode==0) return;
            var sc=scrCamera.instance; if(sc==null) return;
            var rt=RT(sc); var overlay=Overlay(sc); if(rt==null || overlay==null) return;
            if(scene==rt) return;
            FreeTextures(); scene=rt;
            world=new RenderTexture(rt.width,rt.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) {hideFlags=HideFlags.HideAndDontSave}; world.Create(); texture=world.GetNativeTexturePtr();
            worldCamera=new GameObject("FrameGen.IncludedWorld") {hideFlags=HideFlags.HideAndDontSave}.AddComponent<Camera>(); worldCamera.enabled=false;
            Log("scene="+rt.width+"x"+rt.height+" planets, trails, glow and game filters retained in the scene");
        }
        private static IEnumerator Loop() {
            while(!finished) {
                yield return End; if(finished) break;
                var sc=scrCamera.instance; var controller=scrController.instance; double song=scrConductor.instance!=null?scrConductor.instance.songposition_minusi:-1;
                if(Hitch.Playing && song>=5 && trackedCamera!=null && renderedFrame!=Time.frameCount) missingScenes++;
                Packet p=new Packet {unused1=Main.Config.FrameGenNarrow?new IntPtr(1):IntPtr.Zero,frame=Time.frameCount,song=song,measure=Hitch.Playing?1:0,mode=Hitch.Playing && !failed?Math.Max(0,oldMode):0,flip=Main.Config.FrameGenFlipY?1:0,linear=QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,capture=Main.Config.FrameGenCapture?1:Main.Config.FrameGenClip?2:0};
                p.unused2=renderedFrame==Time.frameCount?new IntPtr(1):IntPtr.Zero;
                if(sc!=null && controller!=null && controller.planetRed!=null && controller.planetBlue!=null) {
                    var camera=Cam(sc); if(camera!=null) {
                        var r=controller.planetRed.transform; var b=controller.planetBlue.transform;
                        p.pose=new Pose {x=sceneCamera.x,y=sceneCamera.y,size=sceneCamera.z,angle=sceneCamera.w,rx=r.position.x,ry=r.position.y,rs=r.lossyScale.x,ra=r.eulerAngles.z*Mathf.Deg2Rad,bx=b.position.x,by=b.position.y,bs=b.lossyScale.x,ba=b.eulerAngles.z*Mathf.Deg2Rad};
                        p.baseCamera=sceneBase; p.pulse=scenePulse;
                        if(renderedFrame!=Time.frameCount) p.pulse.w=0; // The stored image belongs to the last camera that actually rendered.
                    }
                }
                if(p.mode!=0 && sceneCamera.z>.00001f && scene!=null && sc!=null && Overlay(sc)!=null) {
                    var overlay=Overlay(sc); worldCamera.CopyFrom(overlay); worldCamera.enabled=false;
                    worldCamera.transform.SetPositionAndRotation(overlay.transform.position,overlay.transform.rotation); worldCamera.targetTexture=world; worldCamera.Render();
                    p.world=texture;
                } else p.mode=0;
                Issue(1,Write(p));
            }
        }
        private static unsafe IntPtr Write(Packet p) { var ptr=packets[packetIndex++%packets.Length]; *(Packet*)ptr=p; return ptr; }
        private static void PostRender(Camera c) {
            if(c!=trackedCamera) return;
            renderedFrame=Time.frameCount; var sc=scrCamera.instance; if(sc==null) return;
            var position=c.transform.position; var shake=Shake(sc); var worldShake=new Vector3(shake.x,shake.y,0);
            if(sc.transform.parent!=null) worldShake=sc.transform.parent.TransformVector(worldShake);
            sceneCamera=new Vector4(position.x,position.y,c.orthographicSize,c.transform.eulerAngles.z*Mathf.Deg2Rad);
            sceneBase=new Vector4(position.x-worldShake.x,position.y-worldShake.y,Zoom(sc)*UserSize(sc),sceneCamera.w);
            scenePulse=new Vector4(PulseFrom(sc),PulseTo(sc),PulseTime(sc),PulseDuration(sc));
        }
        private static void Issue(int id,IntPtr data) { command.Clear(); command.IssuePluginEventAndData(callback,id,data); Graphics.ExecuteCommandBuffer(command); }
        private static void FreeTextures() {
            if(worldCamera!=null) UnityEngine.Object.Destroy(worldCamera.gameObject); worldCamera=null;
            if(world!=null) { world.Release(); UnityEngine.Object.Destroy(world); } world=null; scene=null; texture=IntPtr.Zero;
        }
        internal static void Finish() {
            if(finished || callback==IntPtr.Zero) return; finished=true; Camera.onPostRender-=PostRender; Camera.onPreRender-=PreRender;
            PlayerLoop.SetPlayerLoop(RemoveFrameStart(PlayerLoop.GetCurrentPlayerLoop()));
            Issue(4,IntPtr.Zero); GL.Flush(); Log("finish; missing real scenes="+missingScenes+" native status="+sf_outside_status());
        }
        internal static void Uninstall() {
            if(callback==IntPtr.Zero) return; Finish();
            if(runner!=null) { runner.StopAllCoroutines(); UnityEngine.Object.Destroy(runner.gameObject); runner=null; }
            Issue(3,IntPtr.Zero); GL.Flush(); var wait=System.Diagnostics.Stopwatch.StartNew(); while(sf_outside_stopped()==0 && wait.ElapsedMilliseconds<300) System.Threading.Thread.Sleep(1);
            if(sf_outside_stopped()!=0) { foreach(var p in packets) Marshal.FreeHGlobal(p); FreeTextures(); }
            else Log("shutdown pending; retaining packets and RTs to avoid in-flight use-after-free");
            packets=null; callback=IntPtr.Zero; command.Release(); command=null;
        }
        private static void Log(string message) { Main.Entry.Logger.Log("[프레임생성 바깥] "+message); }
        [DefaultExecutionOrder(32000)] private sealed class Runner:MonoBehaviour { private void LateUpdate(){Late();} }
    }
}
