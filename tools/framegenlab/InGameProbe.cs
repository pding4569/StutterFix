using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Experimental source-copy build only. Gameplay updates run on every Unity/output tick.
    internal static class FrameGenInGameProbe
    {
        [StructLayout(LayoutKind.Sequential)] private struct Packet
        {
            public IntPtr scene, black, white;
            public float x,y,size,angle;
            public int real,frame,mode,measure;
            public double seconds;
            public int flip,linear,capture;
            public double song;
        }
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Unicode)] private static extern int sf_fg_setup(IntPtr chain,string path);
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr sf_fg_event_ptr();
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] private static extern int sf_fg_status();
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] private static extern int sf_fg_error();
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] private static extern int sf_fg_stopped();
        private static readonly AccessTools.FieldRef<scrCamera,Camera> Cam=AccessTools.FieldRefAccess<scrCamera,Camera>("camobj");
        private static readonly AccessTools.FieldRef<scrCamera,Camera> BG=AccessTools.FieldRefAccess<scrCamera,Camera>("BGcam");
        private static readonly AccessTools.FieldRef<scrCamera,Camera> Static=AccessTools.FieldRefAccess<scrCamera,Camera>("Bgcamstatic");
        private static readonly AccessTools.FieldRef<scrCamera,RenderTexture> RT=AccessTools.FieldRefAccess<scrCamera,RenderTexture>("camRT");
        private static readonly AccessTools.FieldRef<scrCamera,Camera> Overlay=AccessTools.FieldRefAccess<scrCamera,Camera>("Overlaycam");
        private static Runner runner;
        private static IntPtr callback;
        private static CommandBuffer command;
        private static Camera overlayCamera,worldCamera;
        private static RenderTexture worldRT;
        private static IntPtr worldPtr;
        private static IntPtr[] packets;
        private static int packetIndex, parity, oldMode=-1;
        private static bool finished, wasPlaying, real, borrowed, failed;
        private static double start;
        private static Camera main, planetCamera;
        private static Camera[] cameras;
        private static bool[] cameraEnabled;
        private static Renderer[] planets;
        private static bool[] forceFlags;
        private static int[] layers;
        private static GameObject[] planetObjects;
        private static int captures;
        private static RenderTexture sceneRT, blackRT, whiteRT;
        private static IntPtr scenePtr,blackPtr,whitePtr;
        private static readonly WaitForEndOfFrame End=new WaitForEndOfFrame();
        private static string directory;
        internal static void Install()
        {
            if(runner!=null) return;
            finished=failed=wasPlaying=false; oldMode=-1; parity=packetIndex=captures=0;
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11) { Log("D3D11 required; feature disabled"); return; }
            directory=Path.Combine(Main.Entry.Path,"framegen-ingame"); Directory.CreateDirectory(directory);
            if(Marshal.SizeOf<Packet>()!=88) throw new InvalidOperationException("FrameGen packet layout mismatch");
            if(sf_fg_setup(IntPtr.Zero,Path.Combine(directory,"presents.csv"))==0) { Log("previous shutdown pending; feature disabled"); return; } callback=sf_fg_event_ptr();
            packets=new IntPtr[512]; for(int i=0;i<packets.Length;i++) packets[i]=Marshal.AllocHGlobal(Marshal.SizeOf<Packet>());
            command=new CommandBuffer { name="FrameGen experiment" }; Issue(0,IntPtr.Zero);
            var go=new GameObject("StutterFix.FrameGenExperiment") { hideFlags=HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go); runner=go.AddComponent<Runner>(); runner.StartCoroutine(Loop());
            Log("experiment default OFF; setting FrameGenExperiment=0/2/4; F8 cycles; input/logic every output tick; native Present sync 0");
        }
        internal static void DrawGUI()
        {
            GUILayout.Label("프레임 생성 실험 (연구 빌드 전용)");
            GUILayout.BeginHorizontal(); foreach(int n in new[]{0,2,4}) if(GUILayout.Button(n==0?"끔":n+"배")) Main.Config.FrameGenExperiment=n; GUILayout.EndHorizontal();
        }
        private static void Late()
        {
            if(finished || failed) return;
            if(Input.GetKeyDown(KeyCode.F8)) Main.Config.FrameGenExperiment=Main.Config.FrameGenExperiment==0?2:Main.Config.FrameGenExperiment==2?4:0;
            bool playing=Hitch.Playing;
            if(playing && !wasPlaying) { start=Time.realtimeSinceStartupAsDouble; parity=0; }
            wasPlaying=playing;
            if(!playing) { Restore(); return; }
            if(sf_fg_status()<0) { failed=true; Restore(); Log("native failure 0x"+sf_fg_error().ToString("X8")+"; reverting to normal cameras"); return; }
            if(sf_fg_status()==0) { real=true; oldMode=0; return; }
            int mode=Main.Config.FrameGenExperiment;
            if(mode!=0 && mode!=2 && mode!=4) mode=0;
            if(mode!=oldMode) { Restore(); if(mode==0 && oldMode>0) Log("state restore differences="+RestoreDifferences()); parity=0; oldMode=mode; Log("mode="+mode); }
            real=mode==0 || parity++%mode==0;
            if(mode==0) return;
            var sc=scrCamera.instance; var controller=scrController.instance;
            if(sc==null || controller==null) { real=true; return; }
            main=Cam(sc); var rt=RT(sc);
            if(main==null || rt==null || controller.planetRed==null || controller.planetBlue==null) { real=true; return; }
            if(sceneRT!=rt)
            {
                Restore(); FreeTextures(); sceneRT=rt; scenePtr=rt.GetNativeTexturePtr();
                blackRT=NewRT(rt.width,rt.height,true); whiteRT=NewRT(rt.width,rt.height,true);
                blackPtr=blackRT.GetNativeTexturePtr(); whitePtr=whiteRT.GetNativeTexturePtr();
                cameras=new[]{Static(sc),BG(sc),main}; cameraEnabled=new bool[3];
                var list=new List<Renderer>(); list.AddRange(controller.planetRed.GetComponentsInChildren<Renderer>(true)); list.AddRange(controller.planetBlue.GetComponentsInChildren<Renderer>(true));
                planets=list.ToArray(); forceFlags=new bool[planets.Length];
                var objects=new List<GameObject>(); foreach(var r in planets) if(!objects.Contains(r.gameObject)) objects.Add(r.gameObject);
                planetObjects=objects.ToArray(); layers=new int[planetObjects.Length];
                foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(r.gameObject.layer==31 && !objects.Contains(r.gameObject)) { failed=true; Log("layer 31 occupied; feature disabled"); return; }
                var go=new GameObject("FrameGen.PlanetCamera") { hideFlags=HideFlags.HideAndDontSave }; planetCamera=go.AddComponent<Camera>(); planetCamera.enabled=false;
                overlayCamera=Overlay(sc);
                if(overlayCamera==null) { failed=true; Log("overlay camera unavailable; feature disabled"); return; }
                worldRT=NewRT(rt.width,rt.height); worldPtr=worldRT.GetNativeTexturePtr();
                var worldGo=new GameObject("FrameGen.WorldCamera") { hideFlags=HideFlags.HideAndDontSave }; worldCamera=worldGo.AddComponent<Camera>(); worldCamera.enabled=false;
                Log("camRT="+rt.width+"x"+rt.height+" renderers="+planets.Length+" colorSpace="+QualitySettings.activeColorSpace+" texturePtrs cached");
                real=true; parity=1;
            }
            for(int i=0;i<cameras.Length;i++) if(cameras[i]!=null) { cameraEnabled[i]=cameras[i].enabled; if(!real) cameras[i].enabled=false; }
            for(int i=0;i<planets.Length;i++) if(planets[i]!=null) { forceFlags[i]=planets[i].forceRenderingOff; if(real) planets[i].forceRenderingOff=true; }
            borrowed=true;
        }
        private static RenderTexture NewRT(int w,int h,bool hdr=false)
        {
            var r=new RenderTexture(w,h,24,hdr?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) { hideFlags=HideFlags.HideAndDontSave };
            r.Create(); return r;
        }
        private static IEnumerator Loop()
        {
            while(!finished)
            {
                yield return End;
                if(finished) break;
                Packet p=new Packet { real=real?1:0, frame=Time.frameCount,mode=wasPlaying && !failed?Math.Max(0,oldMode):0,
                    measure=Hitch.Playing && scrConductor.instance!=null && scrConductor.instance.songposition_minusi>=5?1:0,seconds=Time.realtimeSinceStartupAsDouble,
                    song=scrConductor.instance!=null?scrConductor.instance.songposition_minusi:-1,
                    flip=Main.Config.FrameGenFlipY?1:0,linear=QualitySettings.activeColorSpace==ColorSpace.Linear?1:0 };
                if(p.mode!=0 && sceneRT!=null && main!=null && borrowed)
                {
                    if(Main.Config.FrameGenCapture && Time.realtimeSinceStartupAsDouble-start>8 && ((captures==0 && real) || (captures==1 && !real))) p.capture=++captures;
                    p.scene=worldPtr; p.black=blackPtr; p.white=whitePtr;
                    if(real) { worldCamera.CopyFrom(overlayCamera); worldCamera.enabled=false; worldCamera.transform.SetPositionAndRotation(overlayCamera.transform.position,overlayCamera.transform.rotation); worldCamera.targetTexture=worldRT; worldCamera.Render(); }
                    var pos=main.transform.position; p.x=pos.x; p.y=pos.y; p.size=main.orthographicSize; p.angle=main.transform.eulerAngles.z*Mathf.Deg2Rad;
                    IntPtr ptr=Write(p); Issue(2,ptr);
                    try
                    {
                        planetCamera.CopyFrom(main); planetCamera.enabled=false; planetCamera.transform.SetPositionAndRotation(main.transform.position,main.transform.rotation);
                        planetCamera.cullingMask=1<<31; planetCamera.clearFlags=CameraClearFlags.SolidColor; planetCamera.allowHDR=true; planetCamera.allowMSAA=false;
                        for(int i=0;i<planetObjects.Length;i++) if(planetObjects[i]!=null) { layers[i]=planetObjects[i].layer; planetObjects[i].layer=31; }
                        for(int i=0;i<planets.Length;i++) if(planets[i]!=null) planets[i].forceRenderingOff=forceFlags[i];
                        planetCamera.targetTexture=blackRT; planetCamera.backgroundColor=Color.black; planetCamera.Render();
                        planetCamera.targetTexture=whiteRT; planetCamera.backgroundColor=Color.white; planetCamera.Render();
                    }
                    finally
                    {
                        for(int i=0;i<planetObjects.Length;i++) if(planetObjects[i]!=null) planetObjects[i].layer=layers[i];
                        Restore();
                    }
                    Issue(1,ptr);
                }
                else { Restore(); p.real=1; p.mode=0; Issue(1,Write(p)); }
            }
        }
        private static unsafe IntPtr Write(Packet p) { var ptr=packets[packetIndex++%packets.Length]; *(Packet*)ptr=p; return ptr; }
        private static void Issue(int id,IntPtr ptr) { command.Clear(); command.IssuePluginEventAndData(callback,id,ptr); Graphics.ExecuteCommandBuffer(command); }
        private static void Restore()
        {
            if(!borrowed) return;
            if(cameras!=null) for(int i=0;i<cameras.Length;i++) if(cameras[i]!=null && !cameras[i].enabled) cameras[i].enabled=cameraEnabled[i];
            if(planets!=null) for(int i=0;i<planets.Length;i++) if(planets[i]!=null) planets[i].forceRenderingOff=forceFlags[i];
            borrowed=false;
        }
        private static void FreeTextures()
        {
            if(worldCamera!=null) { UnityEngine.Object.Destroy(worldCamera.gameObject); worldCamera=null; }
            if(worldRT!=null) { worldRT.Release(); UnityEngine.Object.Destroy(worldRT); worldRT=null; worldPtr=IntPtr.Zero; }
            overlayCamera=null;
            if(planetCamera!=null) { UnityEngine.Object.Destroy(planetCamera.gameObject); planetCamera=null; }
            foreach(var rt in new[]{blackRT,whiteRT}) if(rt!=null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            sceneRT=blackRT=whiteRT=null; scenePtr=blackPtr=whitePtr=IntPtr.Zero;
        }
        private static int RestoreDifferences()
        {
            int n=0;
            if(cameras!=null) for(int i=0;i<cameras.Length;i++) if(cameras[i]!=null && cameras[i].enabled!=cameraEnabled[i]) n++;
            if(planets!=null) for(int i=0;i<planets.Length;i++) if(planets[i]!=null && planets[i].forceRenderingOff!=forceFlags[i]) n++;
            if(planetObjects!=null) for(int i=0;i<planetObjects.Length;i++) if(planetObjects[i]!=null && planetObjects[i].layer!=layers[i]) n++;
            return n;
        }
        internal static void Finish()
        {
            if(finished || callback==IntPtr.Zero) return;
            finished=true; Restore(); Issue(4,IntPtr.Zero); GL.Flush();
            Log("state restore differences="+RestoreDifferences());
            Log("native CSV requested; status="+sf_fg_status());
        }
        internal static void Uninstall()
        {
            if(callback==IntPtr.Zero) return;
            Finish(); if(runner!=null) { runner.StopAllCoroutines(); UnityEngine.Object.Destroy(runner.gameObject); runner=null; }
            Issue(3,IntPtr.Zero); GL.Flush();
            var end=System.Diagnostics.Stopwatch.StartNew(); while(sf_fg_stopped()==0 && end.ElapsedMilliseconds<300) System.Threading.Thread.Sleep(1);
            if(sf_fg_stopped()!=0 && packets!=null) foreach(var p in packets) Marshal.FreeHGlobal(p);
            else Log("shutdown pending; retain packet memory to avoid an in-flight use-after-free");
            packets=null; callback=IntPtr.Zero; command.Release(); command=null; FreeTextures(); planets=null; cameras=null; main=null;
        }
        private static void Log(string s) { Main.Entry.Logger.Log("[프레임생성 게임] "+s); }
        [DefaultExecutionOrder(32000)] private sealed class Runner:MonoBehaviour { private void LateUpdate() { Late(); } }
    }
}
