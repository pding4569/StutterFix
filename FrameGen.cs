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
    // No runtime type initialization, camera hooks, native setup or buffers while OFF.
    internal static class FrameGen
    {
        private static bool active,research,failed,quitting;
        internal static bool RuntimeInitialized;
        internal static int CounterEpoch;
        internal static bool Active => active;
        internal static string Status = "";
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_framegen_installed();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern ulong sf_framegen_sources();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern ulong sf_framegen_generated();
        internal static string Describe() => "active="+active+" native_installed="+(SfNative.FrameGenReady?sf_framegen_installed():0)+
            " sources="+(SfNative.FrameGenReady?sf_framegen_sources():0)+" generated="+(SfNative.FrameGenReady?sf_framegen_generated():0)+" runtime_initialized="+RuntimeInitialized+" status="+Status;
        // Read existing atomic counters only when the monitor refreshes its text.
        // Never install an observation hook to count the default-OFF path.
        internal static bool TryOutputCount(out ulong count)
        {
            count=0;
            if(!active || !Hitch.Playing || !SfNative.FrameGenReady) return false;
            count=sf_framegen_sources()+sf_framegen_generated();
            return true;
        }
        internal static void Tick()
        {
            if(quitting) return;
            bool requested=Main.Config.FrameGenOutside>=2 && Main.Config.FrameGenOutside<=8;
            if(!requested && !research) { if(active) Shutdown(); failed=false; Status=""; return; }
            if(!research && HalfRender.Enabled) { if(active) Shutdown(); return; }
            if(failed) return;
            try {
                if(!active) {
                    ImagePrefetch.LoadSfNativeOnly();
                    if(!SfNative.FrameGenReady) throw new InvalidOperationException("게임을 다시 켜야 새 네이티브 DLL을 사용할 수 있습니다.");
                    FrameGenRuntime.Install(); active=true; CounterEpoch++;
                }
                if(FrameGenRuntime.Failed) throw new InvalidOperationException("실험이 중단됐습니다. 끈 뒤 다시 켜 주세요.");
            } catch(Exception ex) {
                failed=true; Status=ex.Message;
                try { FrameGenRuntime.Uninstall(); } catch { }
                active=false;
                Main.Entry.Logger.Log("[프레임 늘리기] "+Status);
            }
        }
        internal static void Shutdown()
        {
            research=false;
            if(!active) return;
            active=false;
            FrameGenRuntime.Uninstall();
        }
        internal static void Quit() { quitting=true; Shutdown(); }
        internal static void InstallResearch() { research=true; failed=false; Tick(); }
        internal static void FinishResearch() { quitting=true; if(active) FrameGenRuntime.Finish(); }
    }
    // Experimental image interpolation between two completed filtered scenes.
    // Planet envelopes and UI use the nearer true frame; uncertain blocks do too.
    // Unity's camera scheduling is untouched; no draw-function interception.
    internal static class FrameGenRuntime
    {
        // Prevent beforefieldinit. JitWarm also excludes this type because Mono's
        // GetFunctionPointer can still invoke its initializer without running Install.
        static FrameGenRuntime() { FrameGen.RuntimeInitialized=true; }
        [StructLayout(LayoutKind.Sequential)] private struct Pose { public float x,y,size,angle,rx,ry,rs,ra,bx,by,bs,ba; }
        [StructLayout(LayoutKind.Sequential)] private struct Packet {
            public IntPtr world,unused1,unused2;
            public Vector4 baseCamera;
            public Pose pose;
            public double song;
            public int frame,mode,measure,flip,linear,capture;
            public Vector4 pulse;
        }
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] private static extern int sf_framegen_setup(string directory,int diagnostics);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr sf_framegen_event_ptr();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_framegen_status();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_framegen_error();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_framegen_stopped();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] private static extern int sf_framegen_block_version();
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
#if FRAMEGEN_RESEARCH
        internal const bool Diagnostics=true;
        private static bool Narrow => Main.Config.FrameGenNarrow;
        private static bool FlipY => Main.Config.FrameGenFlipY;
        private static bool Capture => Main.Config.FrameGenCapture;
        private static bool Clip => Main.Config.FrameGenClip;
        private static bool CameraBlend => Main.Config.FrameGenCameraBlend;
        private static bool BlockFlow => Main.Config.FrameGenBlockFlow;
        private static int BlockVariant => Main.Config.FrameGenBlockVariant;
        private static bool CostSplit => Main.Config.FrameGenCostSplit;
        private static bool ScenePair => Main.Config.FrameGenScenePair;
        private static bool pairGeometrySaved;
        private static double lastPairSong;
        private static readonly AccessTools.FieldRef<scrCamera,MeshRenderer> QuadMesh=AccessTools.FieldRefAccess<scrCamera,MeshRenderer>("camQuadMesh");
#else
        internal const bool Diagnostics=false;
        private const bool Narrow=true,FlipY=true,Capture=false,Clip=false,CameraBlend=true,ScenePair=false,BlockFlow=true;
        private const int BlockVariant=3;
#endif
        private static Renderer[] redRenderers,blueRenderers;
        internal static bool Failed => failed;
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
        private static void BeginFrame() { if(!Narrow && !finished && callback!=IntPtr.Zero) Issue(5,IntPtr.Zero); }
        private static void PreRender(Camera c) {
            if(!Narrow || finished || failed || c.targetTexture!=null || finalBeginFrame==Time.frameCount) return;
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
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11) throw new InvalidOperationException("D3D11에서 사용할 수 있습니다.");
            if(Marshal.SizeOf<Packet>()!=136) throw new InvalidOperationException("Outside packet ABI");
            if(BlockFlow) {
                try { if(sf_framegen_block_version()!=1) throw new EntryPointNotFoundException(); }
                catch(EntryPointNotFoundException) { throw new InvalidOperationException("블록 보간 DLL을 사용하려면 게임을 다시 켜 주세요."); }
            }
            string directory=Diagnostics?Path.Combine(Main.Entry.Path,"framegen-outside"):string.Empty; if(Diagnostics) Directory.CreateDirectory(directory);
            if(sf_framegen_setup(directory,Diagnostics?1:0)==0) throw new InvalidOperationException("지난 해제가 끝나지 않았습니다. 게임을 다시 켜 주세요.");
            callback=sf_framegen_event_ptr(); command=new CommandBuffer {name="FrameGen included snapshot"};
            packets=new IntPtr[512]; for(int i=0;i<packets.Length;i++) packets[i]=Marshal.AllocHGlobal(136);
            finished=failed=false; oldMode=-1; packetIndex=missingScenes=0; renderedFrame=-1; trackedCamera=null;
#if FRAMEGEN_RESEARCH
            pairGeometrySaved=false;
            lastPairSong=double.NaN;
#endif
            finalBeginFrame=-1;
            sceneCamera=sceneBase=scenePulse=Vector4.zero;
            Camera.onPostRender+=PostRender;
            Camera.onPreRender+=PreRender;
            Issue(0,IntPtr.Zero);
            if(Diagnostics) { var loop=PlayerLoop.GetCurrentPlayerLoop(); int found=0; loop=InsertFrameStart(loop,ref found);
                if(found!=1) throw new InvalidOperationException("PlayerSendFrameStarted boundary unavailable; stop the experiment");
                PlayerLoop.SetPlayerLoop(loop); }
            var go=new GameObject("StutterFix.FrameGenIncluded") {hideFlags=HideFlags.HideAndDontSave}; UnityEngine.Object.DontDestroyOnLoad(go); runner=go.AddComponent<Runner>(); runner.StartCoroutine(Loop());
            Log("기본 꺼짐; 공 포함; 원래 스왑체인; 배율 2..8");
        }
        private static void Late() {
            if(finished || failed) return;
            if(Hitch.Playing && scrCamera.instance!=null) trackedCamera=Cam(scrCamera.instance);
            if(sf_framegen_status()<0) { failed=true; Log("native failure="+sf_framegen_error()+"; experiment stopped"); return; }
            int mode=Hitch.Playing && sf_framegen_status()==1?Main.Config.FrameGenOutside:0;
#if FRAMEGEN_RESEARCH
            if(!(CostSplit && mode==1) && (mode<2 || mode>8)) mode=0;
#else
            if(mode<2 || mode>8) mode=0;
#endif
            // HalfRender already reprojects alternating scenes and retains an older
            // camera pose. Treating those Unity frames as new source scenes doubles
            // camera velocity at the next completed render. Suspend this experiment.
            if(HalfRender.Enabled) mode=0;
            if(mode!=oldMode) { oldMode=mode; Log("mode="+mode); }
            if(mode==0) return;
            var sc=scrCamera.instance; if(sc==null) return;
            var rt=RT(sc); var overlay=Overlay(sc); if(rt==null || overlay==null) return;
            int width=Screen.width,height=Screen.height;
            if(scene==rt && world!=null && world.width==width && world.height==height) return;
            FreeTextures(); scene=rt;
            // FSR/sharpening have already produced the final screen-sized quad.
            // Downsampling it to camRT size changes pixels and mislabels scene edges
            // as UI. Capture the same pixel grid as the game's final output instead.
            world=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) {hideFlags=HideFlags.HideAndDontSave}; world.Create(); texture=world.GetNativeTexturePtr();
            worldCamera=new GameObject("FrameGen.IncludedWorld") {hideFlags=HideFlags.HideAndDontSave}.AddComponent<Camera>(); worldCamera.enabled=false;
            if(BlockFlow && scrController.instance!=null) {
                redRenderers=scrController.instance.planetRed.GetComponentsInChildren<Renderer>(true);
                blueRenderers=scrController.instance.planetBlue.GetComponentsInChildren<Renderer>(true);
            }
            Log("scene="+rt.width+"x"+rt.height+" snapshot="+width+"x"+height+" planets, trails, glow and game filters retained in the scene");
        }
        private static IEnumerator Loop() {
            while(!finished) {
                yield return End; if(finished) break;
                try { EndFrame(); }
                catch(Exception ex) { failed=true; Log("중단: "+ex.Message); yield break; }
            }
        }
        private static void EndFrame() {
                var sc=scrCamera.instance; var controller=scrController.instance; double song=scrConductor.instance!=null?scrConductor.instance.songposition_minusi:-1;
#if FRAMEGEN_RESEARCH
                if(ScenePair) { if(song<lastPairSong-1) pairGeometrySaved=false; lastPairSong=song; }
#endif
                if(Diagnostics && Hitch.Playing && song>=5 && trackedCamera!=null && renderedFrame!=Time.frameCount) missingScenes++;
                Packet p=new Packet {unused1=Narrow?new IntPtr(1):IntPtr.Zero,frame=Time.frameCount,song=song,measure=Diagnostics && Hitch.Playing?1:0,mode=Hitch.Playing && !failed?Math.Max(0,oldMode):0,flip=FlipY?1:0,linear=QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,capture=Capture?1:Clip?2:0};
#if FRAMEGEN_RESEARCH
                if(Main.Config.FrameGenImageGate && CameraBlend) p.capture|=32;
                if(CostSplit) p.capture|=1024; // Equal diagnostic work in OFF/copy-only/2x/4x cost comparison.
#endif
                if(BlockFlow) p.capture|=64|4|((BlockVariant&7)<<7);
                if(CameraBlend) p.capture|=4; // Shared delayed timeline, one-source visual delay.
                if(ScenePair) p.capture|=8; // Separate early visual test; never a performance sample.
                p.unused2=renderedFrame==Time.frameCount?new IntPtr(1):IntPtr.Zero;
                if(sc!=null && controller!=null && controller.planetRed!=null && controller.planetBlue!=null) {
                    var camera=Cam(sc); if(camera!=null) {
                        var r=controller.planetRed.transform; var b=controller.planetBlue.transform;
                        p.pose=new Pose {x=sceneCamera.x,y=sceneCamera.y,size=sceneCamera.z,angle=sceneCamera.w,rx=r.position.x,ry=r.position.y,rs=r.lossyScale.x,ra=r.eulerAngles.z*Mathf.Deg2Rad,bx=b.position.x,by=b.position.y,bs=b.lossyScale.x,ba=b.eulerAngles.z*Mathf.Deg2Rad};
                        p.baseCamera=sceneBase; p.pulse=scenePulse;
                        if(renderedFrame!=Time.frameCount) p.pulse.w=0; // The stored image belongs to the last camera that actually rendered.
                        // Bit64 reuses base/pulse payload for two true-frame planet envelopes.
                        // Actual camera/planet poses remain intact; counterfactual prediction is disabled.
                        if(BlockFlow) {p.baseCamera=PlanetEnvelope(camera,redRenderers);p.pulse=PlanetEnvelope(camera,blueRenderers);}
                    }
                }
                if(p.mode!=0 && sceneCamera.z>.00001f && scene!=null && sc!=null && Overlay(sc)!=null) {
                    var overlay=Overlay(sc); worldCamera.CopyFrom(overlay); worldCamera.enabled=false;
                    worldCamera.transform.SetPositionAndRotation(overlay.transform.position,overlay.transform.rotation); worldCamera.targetTexture=world;
#if FRAMEGEN_RESEARCH
                    if(Main.Config.FrameGenScreenBorder && FrameGenScreenBorder.TryGet(out var border)) {
                        if((border.ToInt64()&1)!=0) throw new InvalidOperationException("unaligned screen mask pointer");
                        p.unused2=new IntPtr(border.ToInt64()|p.unused2.ToInt64()); p.capture|=16;
                    }
#endif
                    worldCamera.Render();
#if FRAMEGEN_RESEARCH
                    if(ScenePair && !pairGeometrySaved && song>=20 && song<23) { pairGeometrySaved=true; SavePairGeometry(sc,song); }
#endif
                    p.world=texture;
                } else p.mode=0;
                Issue(1,Write(p));
        }
        private static unsafe IntPtr Write(Packet p) { var ptr=packets[packetIndex++%packets.Length]; *(Packet*)ptr=p; return ptr; }
        private static Vector4 PlanetEnvelope(Camera camera,Renderer[] renderers) {
            if(renderers==null || renderers.Length==0) return new Vector4(0,0,1,1); // Unknown coverage: keep true image.
            var box=new Vector4(2,2,-1,-1);bool any=false;
            foreach(var renderer in renderers) {
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                var b=renderer.bounds;
                for(int corner=0;corner<8;corner++) {
                    var point=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                    if(point.z<0) continue;
                    box.x=Mathf.Min(box.x,point.x);box.y=Mathf.Min(box.y,1-point.y);box.z=Mathf.Max(box.z,point.x);box.w=Mathf.Max(box.w,1-point.y);any=true;
                }
            }
            if(!any) return Vector4.zero;
            float x=32f/Screen.width,y=32f/Screen.height;
            return new Vector4(Mathf.Clamp01(box.x-x),Mathf.Clamp01(box.y-y),Mathf.Clamp01(box.z+x),Mathf.Clamp01(box.w+y));
        }
#if FRAMEGEN_RESEARCH
        // One explicit visual-only read, never used by ordinary measurements/builds.
        private static void SavePairGeometry(scrCamera sc,double song) {
            using(var f=new StreamWriter(Path.Combine(Main.Entry.Path,"framegen-outside","pair-geometry.txt"))) {
                f.WriteLine(FormattableString.Invariant($"frame={Time.frameCount} song={song:R} screen={Screen.width}x{Screen.height}"));
                var cameras=new[]{Cam(sc),Overlay(sc),worldCamera}; var labels=new[]{"scene","overlay","snapshot"};
                for(int i=0;i<cameras.Length;i++) {
                    var c=cameras[i]; if(c==null) continue; var r=c.rect; var p=c.pixelRect;
                    f.WriteLine(FormattableString.Invariant($"{labels[i]} rect={r.x:R},{r.y:R},{r.width:R},{r.height:R} pixelRect={p.x:R},{p.y:R},{p.width:R},{p.height:R} size={c.orthographicSize:R} aspect={c.aspect:R}"));
                    foreach(var behaviour in c.GetComponents<MonoBehaviour>()) if(behaviour.enabled)
                        f.WriteLine(labels[i]+" enabled_component="+behaviour.GetType().Name);
                }
                var quad=QuadMesh(sc); var mesh=quad!=null?quad.GetComponent<MeshFilter>():null;
                if(mesh==null || mesh.sharedMesh==null) return;
                var material=quad.sharedMaterial;
                if(material!=null) { var scale=material.mainTextureScale; var offset=material.mainTextureOffset;
                    f.WriteLine(FormattableString.Invariant($"quad_shader={material.shader.name} uv_scale={scale.x:R},{scale.y:R} uv_offset={offset.x:R},{offset.y:R}")); }
                foreach(var vertex in mesh.sharedMesh.vertices) {
                    var point=Overlay(sc).WorldToViewportPoint(quad.transform.TransformPoint(vertex));
                    f.WriteLine(FormattableString.Invariant($"quad_viewport={point.x:R},{point.y:R},{point.z:R}"));
                }
            }
        }
#endif
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
            redRenderers=blueRenderers=null;
            if(worldCamera!=null) UnityEngine.Object.Destroy(worldCamera.gameObject); worldCamera=null;
            if(world!=null) { world.Release(); UnityEngine.Object.Destroy(world); } world=null; scene=null; texture=IntPtr.Zero;
        }
        internal static void Finish() {
            if(finished || callback==IntPtr.Zero) return; finished=true; Camera.onPostRender-=PostRender; Camera.onPreRender-=PreRender;
            if(Diagnostics) PlayerLoop.SetPlayerLoop(RemoveFrameStart(PlayerLoop.GetCurrentPlayerLoop()));
            Issue(4,IntPtr.Zero); GL.Flush(); if(Diagnostics) Log("finish; missing real scenes="+missingScenes+" native status="+sf_framegen_status());
        }
        internal static void Uninstall() {
            if(callback==IntPtr.Zero) return; Finish();
            if(runner!=null) { runner.StopAllCoroutines(); UnityEngine.Object.Destroy(runner.gameObject); runner=null; }
            Issue(3,IntPtr.Zero); GL.Flush(); var wait=System.Diagnostics.Stopwatch.StartNew(); while(sf_framegen_stopped()==0 && wait.ElapsedMilliseconds<300) System.Threading.Thread.Sleep(1);
            if(sf_framegen_stopped()!=0) { foreach(var p in packets) Marshal.FreeHGlobal(p); FreeTextures();
#if FRAMEGEN_RESEARCH
                FrameGenScreenBorder.Release();
#endif
            }
            else Log("shutdown pending; retaining packets and RTs to avoid in-flight use-after-free");
            trackedCamera=null; renderedFrame=finalBeginFrame=-1;
            packets=null; callback=IntPtr.Zero; command.Release(); command=null;
        }
        private static void Log(string message) { Main.Entry.Logger.Log("[프레임생성 바깥] "+message); }
        [DefaultExecutionOrder(32000)] private sealed class Runner:MonoBehaviour { private void LateUpdate(){try { Late(); } catch(Exception ex) { failed=true; Log("중단: "+ex.Message); }} private void OnApplicationQuit(){FrameGen.Quit();} }
    }
}
