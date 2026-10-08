using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // The research harness uses the same runtime as the Player build.
    internal static class FrameGenOutsideProbe
    {
        internal static void Install() { FrameGen.InstallResearch(); if(Main.Config.FrameGenFilterPair) FrameGenScreenFilterProbe.Install(); if(Main.Config.FrameGenScreenBorder) FrameGenScreenBorder.Install(); }
        internal static void Finish() { FrameGen.FinishResearch(); FrameGenScreenBorder.Detach(); try { FrameGenScreenFilterProbe.Finish(); } finally { FrameGenScreenFilterProbe.Uninstall(); } }
        internal static void Uninstall() { FrameGen.Shutdown(); FrameGenScreenBorder.Detach(); FrameGenScreenFilterProbe.Uninstall(); }
        internal static void DrawGUI() { UnityEngine.GUILayout.Label("프레임 늘리기 (실험): StutterFix 설정 → 그래픽"); }
    }

    // Research candidate: move only the confirmed binary border to final composition.
    // Original effect runs first (fields/time unchanged); its destination is then
    // replaced by its input. All subsequent game filters still run exactly once.
    internal static class FrameGenScreenBorder
    {
        private static readonly AccessTools.FieldRef<CameraFilterPack_TV_WideScreenHV,Material> Mat=AccessTools.FieldRefAccess<CameraFilterPack_TV_WideScreenHV,Material>("SCMaterial");
        private static readonly AccessTools.FieldRef<CameraFilterPack_TV_WideScreenHV,float> StretchX=AccessTools.FieldRefAccess<CameraFilterPack_TV_WideScreenHV,float>("StretchX");
        private static readonly AccessTools.FieldRef<CameraFilterPack_TV_WideScreenHV,float> StretchY=AccessTools.FieldRefAccess<CameraFilterPack_TV_WideScreenHV,float>("StretchY");
        private static Harmony harmony;
        private static RenderTexture mask;
        private static IntPtr maskPointer;
        private static CameraFilterPack_TV_WideScreenHV owner;
        private static int capturedFrame=-1;
        private static float lastSize=float.NaN;
        private static string failure;
        internal static void Install() {
            if(!Main.Config.FrameGenCameraBlend) throw new InvalidOperationException("screen-border candidate requires delayed real-frame composition");
            capturedFrame=-1; lastSize=float.NaN; failure=null; owner=null;
            harmony=new Harmony("StutterFix.FrameGen.ScreenBorder");
            harmony.Patch(AccessTools.Method(typeof(CameraFilterPack_TV_WideScreenHV),"OnRenderImage"),
                postfix:new HarmonyMethod(typeof(FrameGenScreenBorder),nameof(Postfix)));
        }
        private static void Postfix(CameraFilterPack_TV_WideScreenHV __instance,RenderTexture __0,RenderTexture __1) {
            if(!Hitch.Playing || !FrameGen.Active || Main.Config.FrameGenOutside<2 || failure!=null) return;
            try {
                if(owner==null) owner=__instance;
                if(owner!=__instance) throw new InvalidOperationException("multiple WideScreenHV cameras");
                if(__instance.Smooth!=0 || StretchX(__instance)!=1 || StretchY(__instance)!=1) throw new InvalidOperationException("unverified WideScreenHV smooth/stretch");
                if(__1==null || __0.width!=Screen.width || __0.height!=Screen.height) throw new InvalidOperationException("screen-border candidate requires full-size filter input/output");
                if(mask==null) {
                    mask=new RenderTexture(__0.width,__0.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) {hideFlags=HideFlags.HideAndDontSave}; mask.Create(); maskPointer=mask.GetNativeTexturePtr();
                    Main.Entry.Logger.Log("[프레임생성 테두리] retain all other filters; move binary WideScreenHV border to final composition");
                }
                if(mask.width!=__0.width || mask.height!=__0.height) throw new InvalidOperationException("screen-border source resized; stop");
                if(lastSize!=__instance.Size) {
                    var material=Mat(__instance); var original=material.mainTexture;
                    try { Graphics.Blit(Texture2D.whiteTexture,mask,material); } finally { material.mainTexture=original; }
                    lastSize=__instance.Size;
                }
                Graphics.Blit(__0,__1); // Keep the unmasked image for the remaining game filters.
                capturedFrame=Time.frameCount;
            } catch(Exception ex) { failure=ex.Message; Main.Entry.Logger.Log("[프레임생성 테두리] 중단: "+failure); }
        }
        internal static bool TryGet(out IntPtr border) {
            border=IntPtr.Zero;
            if(failure!=null) throw new InvalidOperationException(failure);
            if(owner==null || !owner.isActiveAndEnabled) return false;
            if(capturedFrame!=Time.frameCount) throw new InvalidOperationException("WideScreenHV source was not captured this frame");
            border=maskPointer; return true;
        }
        internal static void Detach() { harmony?.UnpatchAll("StutterFix.FrameGen.ScreenBorder"); harmony=null; }
        // Called by FrameGenRuntime only after the native engine confirms shutdown.
        internal static void Release() {
            Detach();
            if(mask!=null) { mask.Release(); UnityEngine.Object.Destroy(mask); mask=null; }
            maskPointer=IntPtr.Zero; owner=null;
        }
    }

    // Explicit visual-only experiment: one managed effect, one input/output pair.
    // No game shader/state replacement. Immutable copies are read after native finish.
    internal static class FrameGenScreenFilterProbe
    {
        private static Harmony harmony;
        private static RenderTexture before,after,mask;
        private static bool captured,saved;
        private static int frame;
        private static double song;
        private static string error="";
        private static readonly float[] values=new float[4];
        internal static void Install() {
            captured=saved=false; error="";
            var type=AccessTools.TypeByName("CameraFilterPack_TV_WideScreenHV");
            var method=type==null?null:AccessTools.Method(type,"OnRenderImage",new[]{typeof(RenderTexture),typeof(RenderTexture)});
            if(method==null) throw new InvalidOperationException("WideScreenHV render method unavailable");
            harmony=new Harmony("StutterFix.FrameGen.WideScreenPair");
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(FrameGenScreenFilterProbe),nameof(Prefix)),
                postfix:new HarmonyMethod(typeof(FrameGenScreenFilterProbe),nameof(Postfix)));
        }
        private static RenderTexture Copy(RenderTexture source) {
            if(source==null) throw new InvalidOperationException("Filter destination is the backbuffer; no exact pair");
            var descriptor=source.descriptor; descriptor.depthBufferBits=0;
            var texture=new RenderTexture(descriptor) {hideFlags=HideFlags.HideAndDontSave};
            try { texture.Create(); Graphics.CopyTexture(source,texture); return texture; }
            catch { texture.Release(); UnityEngine.Object.Destroy(texture); throw; }
        }
        private static void Prefix(MonoBehaviour __instance,RenderTexture __0,out bool __state) {
            __state=false; if(captured || !Hitch.Playing || scrConductor.instance==null) return;
            double now=scrConductor.instance.songposition_minusi;
            if(now<20 || now>=20.25) return; // Exclude pre-song audio time and retries.
            captured=true;
            try {
                frame=Time.frameCount; song=now;
                var names=new[]{"Size","Smooth","StretchX","StretchY"};
                for(int i=0;i<names.Length;i++) values[i]=Convert.ToSingle(__instance.GetType().GetField(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(__instance),CultureInfo.InvariantCulture);
                before=Copy(__0); __state=true;
            } catch(Exception ex) { error=ex.ToString(); }
        }
        private static void Postfix(MonoBehaviour __instance,RenderTexture __1,bool __state) {
            if(!__state) return;
            try {
                after=Copy(__1);
                var material=(Material)__instance.GetType().GetField("SCMaterial",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(__instance);
                mask=new RenderTexture(after.width,after.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) {hideFlags=HideFlags.HideAndDontSave}; mask.Create();
                var original=material.mainTexture;
                try { Graphics.Blit(Texture2D.whiteTexture,mask,material); }
                finally { material.mainTexture=original; }
            } catch(Exception ex) { error=ex.ToString(); }
        }
        private static void Save(RenderTexture source,string path) {
            var previous=RenderTexture.active; Texture2D image=null;
            try {
                RenderTexture.active=source;
                image=new Texture2D(source.width,source.height,TextureFormat.RGB24,false,true);
                image.ReadPixels(new Rect(0,0,source.width,source.height),0,0); image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            } finally { RenderTexture.active=previous; if(image!=null) UnityEngine.Object.Destroy(image); }
        }
        internal static void Finish() {
            if(harmony==null || saved) return; saved=true;
            var directory=Path.Combine(Main.Entry.Path,"framegen-outside");
            using(var f=new StreamWriter(Path.Combine(directory,"filter-pair.txt"))) {
                f.WriteLine(FormattableString.Invariant($"frame={frame} song_s={song:R} captured={captured}"));
                f.WriteLine(FormattableString.Invariant($"Size={values[0]:R} Smooth={values[1]:R} StretchX={values[2]:R} StretchY={values[3]:R}"));
                f.WriteLine("error="+error);
            }
            if(error!="" || before==null || after==null || mask==null) return;
            Save(before,Path.Combine(directory,"filter-input.png")); Save(after,Path.Combine(directory,"filter-output.png"));
            Save(mask,Path.Combine(directory,"filter-mask.png"));
        }
        internal static void Uninstall() {
            harmony?.UnpatchAll("StutterFix.FrameGen.WideScreenPair"); harmony=null;
            if(before!=null) { before.Release(); UnityEngine.Object.Destroy(before); before=null; }
            if(after!=null) { after.Release(); UnityEngine.Object.Destroy(after); after=null; }
            if(mask!=null) { mask.Release(); UnityEngine.Object.Destroy(mask); mask=null; }
        }
    }
}
