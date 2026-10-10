#if DEV || AUTOTEST
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
namespace StutterFix {
    // A private, static full-size scene fixture. Does not modify live effects or
    // resolution, does not report gameplay FPS, and is never enabled by config.
    internal sealed class EffectsGpuProbe:MonoBehaviour {
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern IntPtr sf_effects_gpu_event();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]private static extern ulong sf_effects_gpu_stat(int index);
        private static EffectsGpuProbe instance;
        private static RenderTexture frozen;
        private RenderTexture src,dst;
        private Settings config;
        private CommandBuffer command;
        private IntPtr callback,texture;
        private string profile;
        private int warmup;
        internal static void Start(string name) {
            if(name=="off"){Stop();return;}
            if(ScreenEffects.Enabled || FrameGen.Active)throw new Exception("GPU fixture는 화면 효과·프레임 늘리기를 끈 별도 판에서 실행하세요.");
            Stop(true);var scene=ScreenEffects.SceneTexture();if(scene==null || scene.width!=3440 || scene.height!=1440 || Screen.width!=3440 || Screen.height!=1440)throw new Exception("실제3440x1440 게임 그림·창 필요");
            var c=Configuration(name);
            ScreenEffects.PrepareBenchmark(c);
            var go=new GameObject("StutterFix.EffectGpuFixture"){hideFlags=HideFlags.HideAndDontSave};UnityEngine.Object.DontDestroyOnLoad(go);
            instance=go.AddComponent<EffectsGpuProbe>();var r=instance;r.config=c;r.profile=name;
            if(frozen==null){frozen=new RenderTexture(scene.width,scene.height,0,scene.format);frozen.Create();Graphics.Blit(scene,frozen);}
            r.src=frozen;r.dst=new RenderTexture(scene.width,scene.height,0,scene.format);r.dst.Create();
            r.texture=r.src.GetNativeTexturePtr();r.callback=sf_effects_gpu_event();r.command=new CommandBuffer();r.Issue(4);r.warmup=30;
            Main.Entry.Logger.Log("[화면효과GPU] start profile="+name+" size="+scene.width+"x"+scene.height+" static_scene=1 live_effects_unchanged=1");
        }
        internal static Settings Configuration(string name) {
            var c=new Settings();switch(name){
                case "passthrough":break;case "color":c.FxColor=true;break;
                case "combined":c.FxColor=c.FxVignette=c.FxLut=true;c.FxLutPath=System.IO.Path.Combine(Main.Entry.Path,"shots","identity16.png");break;
                case "sharp":c.FxSharp=true;break;case "fxaa":c.FxAA=true;break;case "glow":c.FxGlow=true;break;case "light":c.FxLight=true;break;
                case "clear-strong":ScreenEffects.ConfigurePreset(c,5);break;case "neon-strong":ScreenEffects.ConfigurePreset(c,6);break;
                case "rays":c.FxRays=true;break;case "streak":c.FxStreak=true;break;case "flare":c.FxFlare=true;break;
                case "tone":c.FxToneMap=true;break;case "chromatic":c.FxChromatic=true;break;case "grain":c.FxGrain=true;break;
                case "crt":c.FxCrt=true;break;case "pixel":c.FxPixel=true;break;case "posterize":c.FxPosterize=true;break;case "blur":c.FxBlur=true;break;case "hdr":c.FxHdr=true;break;case "filmic":c.FxFilmic=true;break;case "hdr-filmic":c.FxHdr=c.FxFilmic=true;break;
                case "sepia":c.FxSepia=true;break;case "duotone":c.FxDuotone=true;break;case "tealorange":c.FxTealOrange=true;break;case "invert":c.FxInvert=true;break;
                case "mono":c.FxMono=true;break;case "night":c.FxNight=true;break;case "thermal":c.FxThermal=true;break;case "hue":c.FxHue=true;break;
                case "outline":c.FxOutline=true;break;case "emboss":c.FxEmboss=true;break;case "halftone":c.FxHalftone=true;break;case "tilt":c.FxTiltShift=true;break;case "soft":c.FxSoftFocus=true;break;
                case "barrel":c.FxBarrel=true;break;case "ripple":c.FxRipple=true;break;case "glitch":c.FxGlitch=true;break;case "zoom":c.FxZoomBlur=true;break;
                case "mirror":c.FxMirror=true;break;case "kaleido":c.FxKaleido=true;break;case "letterbox":c.FxLetterbox=true;break;
                case "extras-all":foreach(var s in FxExtras.All)s.ToggleField.SetValue(c,true);c.FxMirror=false;c.FxInvert=false;break;
                default:throw new Exception("알 수 없는 GPU fixture: "+name);
            }return c;
        }
        private void Issue(int id){command.Clear();command.IssuePluginEventAndData(callback,id,texture);Graphics.ExecuteCommandBuffer(command);}
        private void Update(){try{if(warmup>0){ScreenEffects.Benchmark(src,dst,config);if(--warmup==0)Issue(4);return;}Issue(1);ScreenEffects.Benchmark(src,dst,config);Issue(2);}catch(Exception e){Main.Entry.Logger.Log("[화면효과GPU] 실패: "+e.Message);Stop();}}
        internal static void Report(){if(instance==null)return;ulong count=sf_effects_gpu_stat(0);Main.Entry.Logger.Log(FormattableString.Invariant($"[화면효과GPU] profile={instance.profile} samples={count} mean_ms={(count>0?(double)sf_effects_gpu_stat(1)/count/1e6:double.NaN):R} max_ms={(double)sf_effects_gpu_stat(2)/1e6:R} skipped={sf_effects_gpu_stat(3)} errors={sf_effects_gpu_stat(4)}"));}
        internal static void Stop(bool keepScene=false){if(instance!=null){Report();var r=instance;instance=null;r.enabled=false;r.Issue(3);r.command.Release();r.dst.Release();UnityEngine.Object.Destroy(r.dst);UnityEngine.Object.Destroy(r.gameObject);ScreenEffects.Apply();Fsr.Apply();}if(!keepScene&&frozen!=null){frozen.Release();UnityEngine.Object.Destroy(frozen);frozen=null;}}
    }
}
#endif
