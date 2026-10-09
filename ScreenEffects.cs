using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace StutterFix {
    // Own scene texture, after level filters/FSR and before the overlay/UI and FrameGen.
    // Fsr owns the single PreCull coordinator; default OFF registers no new callback.
    internal static class ScreenEffects {
        private static readonly AccessTools.FieldRef<scrCamera,Camera> CameraRef=AccessTools.FieldRefAccess<scrCamera,Camera>("camobj");
        private static readonly AccessTools.FieldRef<scrCamera,MeshRenderer> Quad=AccessTools.FieldRefAccess<scrCamera,MeshRenderer>("camQuadMesh");
        private static Material material,quadMat;
        private static Shader cachedShader;
        private static RenderTexture result;
        private static Texture input;
        private static Texture2D lut;
        private static int done=-1,revision,doneRevision=-1;
        private static string lutPath="";
        private static Behaviour[] bloom=Array.Empty<Behaviour>();
        private static Camera cachedCamera;
        private static float nextBloomScan;
        private static RenderTexture finalTarget;
        private static int passesLeft;
        internal static bool Failed;
        internal static string Status="";
        internal static long Frames,Reused,GlowRest;
        internal static long CustomSourceSkipped;
#if DEV || AUTOTEST
        internal static int LastFrame=-1;
        internal static bool LastGlow;
#endif
        private static bool injectionChecked;
        internal static bool ReShadeFiles;
        internal static bool Enabled {
            get {var c=Main.Config;return c!=null && (c.FxColor||c.FxSharp||c.FxAA||c.FxGlow||c.FxVignette||c.FxLut||c.FxLight||c.FxToneMap||c.FxRays||c.FxStreak||c.FxFlare||c.FxChromatic||c.FxGrain||c.FxCrt||c.FxPixel||c.FxPosterize||c.FxBlur);}
        }
        internal static bool IsOwn(Texture t)=>result!=null && t==result;
        internal static void Apply() {
            if(!injectionChecked){injectionChecked=true;string root=Path.GetFullPath(Path.Combine(Main.Entry.Path,"..",".."));ReShadeFiles=File.Exists(Path.Combine(root,"ReShade.ini"))&&(File.Exists(Path.Combine(root,"dxgi.dll"))||File.Exists(Path.Combine(root,"d3d11.dll")));if(ReShadeFiles)Main.Entry.Logger.Log("[화면 효과] ReShade 설정·DLL 파일 있음 (실제 연결 확인 안 됨)");}
            ++revision;RestoreQuad();
            if(!Enabled) {Release();Status="";Failed=false;return;}
            if(material==null && !Failed) try {
                if(cachedShader==null) {
                var stream=typeof(ScreenEffects).Assembly.GetManifestResourceStream("StutterFix.effects");
                if(stream==null) throw new Exception("화면 효과 번들 없음");
                byte[] bytes;using(stream) {bytes=new byte[stream.Length];int n=0;while(n<bytes.Length){int r=stream.Read(bytes,n,bytes.Length-n);if(r==0)throw new EndOfStreamException();n+=r;}}
                var bundle=AssetBundle.LoadFromMemory(bytes);if(bundle==null)throw new Exception("화면 효과 번들 열기 실패");
                Shader shader;try {shader=bundle.LoadAsset<Shader>("Assets/Effects/ScreenEffects.shader");}finally{bundle.Unload(false);}
                if(shader==null || !shader.isSupported)throw new Exception("화면 효과 셰이더 지원 안 됨");
                cachedShader=shader;
                }
                material=new Material(cachedShader){hideFlags=HideFlags.HideAndDontSave};
                Main.Entry.Logger.Log("[화면 효과] 번들 준비 "+SystemInfo.graphicsDeviceType);
            }catch(Exception e){Failed=true;Status=e.Message;Main.Entry.Logger.Log("[화면 효과] 실패: "+e.Message);}
            LoadLut();
        }
        private static void LoadLut() {
            string path=Main.Config.FxLutPath??"";
            if(path==lutPath && (lut!=null || path.Length==0))return;
            if(lut!=null)UnityEngine.Object.Destroy(lut);lut=null;lutPath=path;
            if(path.Length==0)return;
            Texture2D tex=null;
            try {
                string full=Path.IsPathRooted(path)?path:Path.Combine(Main.Entry.Path,"luts",path);
                tex=new Texture2D(2,2,TextureFormat.RGBA32,false,true){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                if(!tex.LoadImage(File.ReadAllBytes(full)) || tex.width!=tex.height*tex.height || tex.height<2 || tex.height>64)throw new Exception("LUT는 N²×N PNG(N=2~64)입니다.");
                lut=tex;tex=null;
            }catch(Exception e){Status="LUT: "+e.Message;Main.Entry.Logger.Log("[화면 효과] "+Status);}
            finally{if(tex!=null)UnityEngine.Object.Destroy(tex);}
        }
        internal static void RestoreQuad() {
            if(quadMat!=null && result!=null && quadMat.mainTexture==result && input!=null)quadMat.mainTexture=input;
        }
        internal static void Render(scrCamera sc,int content) {
            if(!Enabled || Failed || material==null || sc==null)return;
            try {
                var mesh=Quad(sc);if(mesh==null)return;quadMat=mesh.material;
                var src=quadMat.mainTexture as RenderTexture;if(src==null || IsOwn(src))return;
                // Unknown/custom FPS buffers are already rejected by Fsr's coordinator.
                if(result==null || result.width!=src.width || result.height!=src.height || result.format!=src.format) {
                    ReleaseRT();result=new RenderTexture(src.width,src.height,0,src.format,RenderTextureReadWrite.Default){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};result.Create();done=-1;
                }
                input=src;
                if(content!=done || revision!=doneRevision){Process(src,result);done=content;doneRevision=revision;++Frames;}else ++Reused;
                quadMat.mainTexture=result;
            }catch(Exception e){Failed=true;Status=e.Message;RestoreQuad();Main.Entry.Logger.Log("[화면 효과] 실패: "+e.Message);}
        }
        private static void Process(RenderTexture src,RenderTexture dst,Settings fixture=null) {
            var c=fixture??Main.Config;
            material.SetVector("_Color",new Vector4(c.FxColor?c.FxVibrance:0,c.FxColor?c.FxContrast:1,c.FxColor?c.FxBrightness:0,c.FxColor?c.FxTemperature:0));
            material.SetVector("_Controls",new Vector4(c.FxVignette?c.FxVignetteAmount:0,c.FxSharp?Mathf.Clamp(c.FxSharpAmount,0,2):0,0,0));
            material.SetVector("_LutInfo",new Vector4(lut!=null?lut.height:2,c.FxLut && lut!=null?1:0,0,0));
            if(lut!=null)material.SetTexture("_Lut",lut);
            bool color=c.FxColor||c.FxVignette||(c.FxLut&&lut!=null),rest=fixture==null && !c.FxGlowStack && MapBloom();
            bool style=c.FxToneMap||c.FxChromatic||c.FxGrain||c.FxCrt||c.FxPixel||c.FxPosterize;
            if(style) {
                material.SetVector("_StyleA",new Vector4(c.FxChromatic?Mathf.Clamp(c.FxChromaticAmount,0,12):0,c.FxGrain?Mathf.Clamp(c.FxGrainAmount,0,.2f):0,c.FxCrt?Mathf.Clamp01(c.FxCrtAmount):0,c.FxPixel?Mathf.Clamp(c.FxPixelSize,2,32):0));
                material.SetVector("_StyleB",new Vector4(c.FxPosterize?Mathf.Clamp(Mathf.Round(c.FxPosterizeLevels),2,32):0,c.FxToneMap?1:0,Mathf.Clamp(c.FxCeiling,.5f,1),c.FxGrain?Time.unscaledTime:0));
            }
            passesLeft=(color?1:0)+(c.FxSharp?1:0)+(c.FxAA?1:0)+(c.FxGlow&&!rest?1:0)+(c.FxLight?1:0)+(c.FxRays?1:0)+(c.FxStreak?1:0)+(c.FxFlare?1:0)+(c.FxBlur?1:0)+(style?1:0);finalTarget=dst;
            RenderTexture current=src;var saved=RenderTexture.active;
            try {
                if(color)Pass(ref current,src,0);
                if(c.FxSharp)Pass(ref current,src,1);
                if(c.FxAA)Pass(ref current,src,2);
                if(c.FxGlow && !rest)Spread(ref current,src,false,c);
                else if(c.FxGlow && rest)++GlowRest;
                if(c.FxLight)Spread(ref current,src,true,c);
                if(c.FxRays)Optical(ref current,src,7,c);
                if(c.FxStreak)Optical(ref current,src,8,c);
                if(c.FxFlare)Optical(ref current,src,9,c);
                if(c.FxBlur)Optical(ref current,src,11,c);
                if(style)Pass(ref current,src,10);
                if(current!=dst)Graphics.Blit(current,dst);
#if DEV || AUTOTEST
                if(fixture==null){LastFrame=Time.frameCount;LastGlow=c.FxGlow&&!rest;}
#endif
            }finally{if(current!=src && current!=dst)RenderTexture.ReleaseTemporary(current);RenderTexture.active=saved;finalTarget=null;}
        }
        private static void Pass(ref RenderTexture current,RenderTexture original,int pass) {
            var next=--passesLeft==0?finalTarget:RenderTexture.GetTemporary(original.width,original.height,0,original.format);next.filterMode=FilterMode.Bilinear;
            try {Graphics.Blit(current,next,material,pass);}catch{if(next!=finalTarget)RenderTexture.ReleaseTemporary(next);throw;}
            if(current!=original)RenderTexture.ReleaseTemporary(current);current=next;
        }
        private static void Spread(ref RenderTexture current,RenderTexture original,bool light,Settings c) {
            int divisor=light?(1<<Mathf.Clamp(c.FxLightQuality,1,3)):4;
            var a=RenderTexture.GetTemporary(Math.Max(1,original.width/divisor),Math.Max(1,original.height/divisor),0,original.format);
            var b=RenderTexture.GetTemporary(a.width,a.height,0,a.format);a.filterMode=b.filterMode=FilterMode.Bilinear;
            try {
                material.SetVector("_Glow",new Vector4(c.FxGlowThreshold,light?c.FxLightAmount:c.FxGlowAmount,c.FxToneMap?1:0,0));
                Graphics.Blit(current,a,material,3);
                int loops=light?3:1;
                for(int k=0;k<loops;k++) {
                    material.SetVector("_Direction",new Vector4(light?4<<k:c.FxGlowStack?4:1,0,0,0));Graphics.Blit(a,b,material,4);
                    material.SetVector("_Direction",new Vector4(0,light?4<<k:c.FxGlowStack?4:1,0,0));Graphics.Blit(b,a,material,4);
                }
                material.SetTexture("_GlowTex",a);Pass(ref current,original,light?6:5);
            }finally{RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        private static void Optical(ref RenderTexture current,RenderTexture original,int kind,Settings c) {
            var a=RenderTexture.GetTemporary(Math.Max(1,original.width/4),Math.Max(1,original.height/4),0,original.format);
            var b=RenderTexture.GetTemporary(a.width,a.height,0,a.format);a.filterMode=b.filterMode=FilterMode.Bilinear;
            try {
                float amount=kind==7?c.FxRaysAmount:kind==8?c.FxStreakAmount:kind==9?c.FxFlareAmount:1;
                material.SetVector("_Glow",new Vector4(Mathf.Clamp01(c.FxGlowThreshold),Mathf.Clamp(amount,0,2),1,0));
                if(kind==11) {
                    Graphics.Blit(current,a);
                    material.SetVector("_Direction",new Vector4(Mathf.Clamp(c.FxBlurRadius,1,8),0,0,0));Graphics.Blit(a,b,material,4);
                    material.SetVector("_Direction",new Vector4(0,Mathf.Clamp(c.FxBlurRadius,1,8),0,0));Graphics.Blit(b,a,material,4);
                    material.SetTexture("_GlowTex",a);Pass(ref current,original,11);
                } else {
                    Graphics.Blit(current,a,material,3);
                    material.SetVector("_Rays",new Vector4(Mathf.Clamp01(c.FxRaysX),Mathf.Clamp01(c.FxRaysY),Mathf.Clamp01(c.FxRaysLength),.97f));
                    material.SetVector("_Direction",new Vector4(.012f,0,0,0));Graphics.Blit(a,b,material,kind);
                    material.SetTexture("_GlowTex",b);Pass(ref current,original,5);
                }
            }finally{RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        private static bool MapBloom() {
            var sc=scrCamera.instance;var cam=sc==null?null:CameraRef(sc);
            if(!Main.Config.FxGlow)return false;
            if(cam!=cachedCamera || Time.realtimeSinceStartup>=nextBloomScan){cachedCamera=cam;nextBloomScan=Time.realtimeSinceStartup+1;
                bloom=cam==null?Array.Empty<Behaviour>():cam.GetComponents<Behaviour>().Where(b=>b!=null && (b.GetType().Name.IndexOf("Bloom",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();}
            foreach(var b in bloom)if(b!=null && b.enabled && b.gameObject.activeInHierarchy)return true;
            return false;
        }
        internal static void Preset(int p) {
            ConfigurePreset(Main.Config,p);
        }
        internal static void ConfigurePreset(Settings c,int p) {
            if(p<0 || p>6 || p==4)throw new ArgumentOutOfRangeException(nameof(p));
            c.FxPreset=p;c.FxColor=p>=2;c.FxSharp=p>=1;c.FxAA=p==1;c.FxGlow=p==3||p==6;c.FxVignette=p==3||p==6;c.FxLut=c.FxLight=false;
            c.FxGlowStack=c.FxToneMap=p==6;c.FxRays=c.FxStreak=c.FxFlare=c.FxChromatic=c.FxGrain=c.FxCrt=c.FxPixel=c.FxPosterize=c.FxBlur=false;
            c.FxVibrance=.2f;c.FxContrast=1.08f;c.FxBrightness=0;c.FxTemperature=0;c.FxSharpAmount=.4f;c.FxGlowAmount=.25f;c.FxGlowThreshold=.75f;c.FxVignetteAmount=.15f;
            if(p==5){c.FxVibrance=.65f;c.FxContrast=1.22f;}
            if(p==6){c.FxVibrance=.55f;c.FxContrast=1.12f;c.FxBrightness=-.1f;c.FxGlowAmount=1.2f;c.FxGlowThreshold=.28f;c.FxCeiling=.97f;}
        }
        private static void ReleaseRT(){RestoreQuad();if(result!=null){result.Release();UnityEngine.Object.Destroy(result);}result=null;input=null;done=-1;}
        private static void Release(){ReleaseRT();if(material!=null)UnityEngine.Object.Destroy(material);material=null;if(lut!=null)UnityEngine.Object.Destroy(lut);lut=null;lutPath="";cachedCamera=null;bloom=Array.Empty<Behaviour>();}
        internal static void Shutdown(){
#if DEV || AUTOTEST
            EffectsGpuProbe.Stop();
            if(shotRunner!=null){UnityEngine.Object.Destroy(shotRunner.gameObject);shotRunner=null;}
#endif
            Release();quadMat=null;if(cachedShader!=null)Resources.UnloadAsset(cachedShader);cachedShader=null;injectionChecked=false;ReShadeFiles=false;}
        internal static string Describe()=>"enabled="+Enabled+" ready="+(material!=null)+" failed="+Failed+" frames="+Frames+" reused="+Reused+" glow_rest="+GlowRest+" custom_source_skipped="+CustomSourceSkipped+" coordinator="+Fsr.CoordinatorActive+" size="+(result!=null?result.width+"x"+result.height:"0x0")+" status="+Status;
        internal static void Inventory() {
            string dir=Path.Combine(Main.Entry.Path,"shots");Directory.CreateDirectory(dir);
            var names=new System.Collections.Generic.HashSet<string>(Resources.FindObjectsOfTypeAll<Shader>().Select(s=>s.name));
            var candidates=new System.Collections.Generic.List<string>();var ignored=new System.Collections.Generic.List<string>();
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("Assembly-CSharp",StringComparison.Ordinal)))
            foreach(var type in assembly.GetTypes().Where(t=>t.Name.StartsWith("CameraFilterPack",StringComparison.Ordinal)))
            foreach(var method in type.GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                ShaderWarm.ScanStrings(method,candidates,ignored);
            foreach(var name in candidates)names.Add(name);
            var shaders=names.OrderBy(n=>n,StringComparer.Ordinal).Select(n=>{var s=Shader.Find(n);return n+"\tfound="+(s!=null)+"\tsupported="+(s!=null&&s.isSupported);}).ToArray();
            File.WriteAllLines(Path.Combine(dir,"shader-inventory.txt"),shaders);
            Main.Entry.Logger.Log("[화면 효과] shader 목록 "+shaders.Length+"개 (로드된 Shader+게임 필터의 Find 이름, 효과 전체 지원 증명 아님)");
        }
#if DEV || AUTOTEST
        private static ShotRunner shotRunner;
        // Explicit visual-only command. Synchronous GPU read/PNG work is kept
        // outside performance runs and the capture frame's FX identity is logged.
        internal static void TimedShot(string name,double target) {
            if(shotRunner!=null)throw new Exception("캡처가 아직 끝나지 않았습니다.");
            if(name!=Path.GetFileName(name))throw new Exception("캡처 파일 이름만 사용하세요.");
            var go=new GameObject("StutterFix.FxShot"){hideFlags=HideFlags.HideAndDontSave};
            shotRunner=go.AddComponent<ShotRunner>();shotRunner.StartCoroutine(shotRunner.Run(name,target));
        }
        private sealed class ShotRunner:MonoBehaviour {
            internal System.Collections.IEnumerator Run(string name,double target) {
                yield return new WaitForEndOfFrame();Texture2D image=null;
                try {
                    int frame=Time.frameCount;double song=scrConductor.instance.songposition_minusi;
                    image=ScreenCapture.CaptureScreenshotAsTexture();
                    string dir=Path.Combine(Main.Entry.Path,"shots");Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());
                    bool paired=Enabled && LastFrame==frame && input is RenderTexture && result!=null;
                    if(paired){Save((RenderTexture)input,Path.Combine(dir,name+".scene-input.png"));Save(result,Path.Combine(dir,name+".scene-output.png"));}
                    Main.Entry.Logger.Log(FormattableString.Invariant($"[FX원본캡처] target={target:R} song={song:R} frame={frame} effect_frame={LastFrame} enabled={Enabled} glow={LastGlow} size={image.width}x{image.height} scene_pair={paired} color_space={QualitySettings.activeColorSpace}"));
                }finally{if(image!=null)UnityEngine.Object.Destroy(image);shotRunner=null;UnityEngine.Object.Destroy(gameObject);}
            }
        }
        internal static void PrepareBenchmark(Settings config){var old=Main.Config;bool color=config.FxColor;try{Main.Config=config;if(!Enabled)config.FxColor=true;Apply();if(material==null || (config.FxLut&&lut==null))throw new Exception("셰이더/LUT 준비 실패");}finally{config.FxColor=color;Main.Config=old;}}
        internal static void Benchmark(RenderTexture src,RenderTexture dst,Settings config){Process(src,dst,config);}
        internal static RenderTexture SceneTexture(){var sc=scrCamera.instance;return sc==null?null:AccessTools.FieldRefAccess<scrCamera,RenderTexture>("camRT")(sc);}
        private static Color32[] Read(RenderTexture rt){var saved=RenderTexture.active;Texture2D t=null;try{RenderTexture.active=rt;t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();return t.GetPixels32();}finally{RenderTexture.active=saved;if(t!=null)UnityEngine.Object.Destroy(t);}}
        private static void Save(RenderTexture rt,string path){var saved=RenderTexture.active;Texture2D t=null;try{RenderTexture.active=rt;t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());}finally{RenderTexture.active=saved;if(t!=null)UnityEngine.Object.Destroy(t);}}
        internal static void Capture() {
            if(result==null || !(input is RenderTexture))throw new Exception("효과를 켜고 진짜 프레임을 그린 뒤 캡처하세요.");
            string dir=Path.Combine(Main.Entry.Path,"shots");Directory.CreateDirectory(dir);
            Save((RenderTexture)input,Path.Combine(dir,"fx-input.png"));Save(result,Path.Combine(dir,"fx-output.png"));
            Main.Entry.Logger.Log("[화면 효과] 같은 프레임 입력/출력 캡처 song="+scrConductor.instance.songposition_minusi+" "+Describe());
        }
        internal static void Fixture() {
            var old=Main.Config;var config=new Settings();var saved=RenderTexture.active;
            var src=new RenderTexture(320,180,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);src.Create();
            var dst=new RenderTexture(320,180,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);dst.Create();
            var pattern=new Texture2D(320,180,TextureFormat.RGBA32,false,true);var pixels=new Color32[320*180];
            for(int y=0;y<180;y++)for(int x=0;x<320;x++)pixels[y*320+x]=new Color32((byte)(x*7),(byte)(y*11),(byte)((x/8+y/8)%2*255),255);
            pattern.SetPixels32(pixels);pattern.Apply();Graphics.Blit(pattern,src);
            try {
                Main.Config=config;config.FxColor=true;Apply();if(material==null)throw new Exception("효과 셰이더 준비 실패");
                config.FxColor=false;Process(src,dst);var off=Read(dst);var baseline=Read(src);
                int mismatch=0;for(int i=0;i<off.Length;i++)if(!off[i].Equals(baseline[i]))++mismatch;
                Main.Entry.Logger.Log("[화면 효과 fixture] off_pixels="+off.Length+" mismatch="+mismatch);
                if(mismatch!=0)throw new Exception("OFF 픽셀 다름");
                for(int p=1;p<=3;p++){Preset(p);Process(src,dst);var output=Read(dst);int changed=0;for(int i=0;i<output.Length;i++)if(!output[i].Equals(baseline[i]))++changed;
                    var retained=Read(src);int altered=0;for(int i=0;i<retained.Length;i++)if(!retained[i].Equals(baseline[i]))++altered;
                    Main.Entry.Logger.Log("[화면 효과 fixture] preset="+p+" changed="+changed+" total="+output.Length+" source_altered="+altered);if(changed==0||altered!=0)throw new Exception("출력 또는 원본 보존 검사 실패");}
                config.FxLight=true;Process(src,dst);Main.Entry.Logger.Log("[화면 효과 fixture] light_pass=1");
                foreach(string name in new[]{"clear-strong","neon-strong","rays","streak","flare","tone","chromatic","grain","crt","pixel","posterize","blur"}) {
                    var candidate=EffectsGpuProbe.Configuration(name);Process(src,dst,candidate);
                    var output=Read(dst);int changed=0,altered=0,bright=0;
                    for(int i=0;i<output.Length;i++){if(!output[i].Equals(baseline[i]))++changed;bright=Math.Max(bright,Math.Max(output[i].r,Math.Max(output[i].g,output[i].b)));}
                    var retained=Read(src);for(int i=0;i<retained.Length;i++)if(!retained[i].Equals(baseline[i]))++altered;
                    Main.Entry.Logger.Log("[화면 효과 fixture] profile="+name+" changed="+changed+" total="+output.Length+" source_altered="+altered+" max_channel="+bright);
                    if(changed==0 || altered!=0 || candidate.FxToneMap && bright>Math.Ceiling(candidate.FxCeiling*255)+1)throw new Exception("추가 효과 픽셀 검사 실패: "+name);
                }
                string identity=Path.Combine(Main.Entry.Path,"shots","identity16.png");
                if(File.Exists(identity)) {
                    config.FxColor=config.FxSharp=config.FxAA=config.FxGlow=config.FxVignette=config.FxLight=false;
                    config.FxLut=true;config.FxLutPath=identity;LoadLut();if(lut==null)throw new Exception("identity LUT load failed");
                    Process(src,dst);var identityPixels=Read(dst);int maximum=0;
                    for(int i=0;i<identityPixels.Length;i++){var a=identityPixels[i];var b=baseline[i];maximum=Math.Max(maximum,Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b))));}
                    Main.Entry.Logger.Log("[화면 효과 fixture] identity_lut_max_byte_error="+maximum);
                    if(maximum>2)throw new Exception("identity LUT pixel mismatch");
                }
            }finally{Main.Config=old;Apply();Fsr.Apply();RenderTexture.active=saved;src.Release();dst.Release();UnityEngine.Object.Destroy(src);UnityEngine.Object.Destroy(dst);UnityEngine.Object.Destroy(pattern);}
        }
#endif
    }
}
