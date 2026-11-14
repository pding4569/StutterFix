#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;
using HarmonyLib;
namespace StutterFix {
    // M0 synthetic objects only. Explicit command lease, no settings or persisted enable bit.
    internal static class DirectRenderer {
        [StructLayout(LayoutKind.Sequential)] struct CameraData { internal Matrix4x4 Vp; internal Vector4 Position; }
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_dr_create(uint n,uint m,uint anim,uint threads,IntPtr texture);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_dr_submit(int token,double time,ref CameraData camera,IntPtr target);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern void sf_dr_destroy(int token);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sf_dr_event();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern double sf_dr_stat(int token,int index);
        struct Marker { }
        static readonly AccessTools.FieldRef<scrCamera,RenderTexture> SceneTarget=AccessTools.FieldRefAccess<scrCamera,RenderTexture>("camRT");
        static RenderTexture SceneTexture()=>scrCamera.instance!=null?SceneTarget(scrCamera.instance):null;
        static int token; static Camera camera; static RenderTexture target; static Texture2DArray textures;
        static CommandBuffer commands; static object level; static IntPtr targetPtr; static int width,height;
        static readonly long[] mainSamples=new long[4096];static long mainCount,mainSum;
        static void Log(string s)=>Main.Entry.Logger.Log("[dr] "+s);
        internal static bool Step(string arg){
            var a=arg.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(a.Length==1&&a[0]=="off"){Stop();Log("off");return true;}
            if(a.Length==1&&a[0]=="state"){Report();return true;}
            if(a.Length==2&&a[0]=="shot"){Report();Log("shot="+a[1]+" capture: framecmp grab "+a[1]);return true;}
            if(a.Length<3||a.Length>4||a[0]!="bench")throw new ArgumentException("dr bench N M [anim%=100] | state | off | shot name");
            int n=int.Parse(a[1],CultureInfo.InvariantCulture),m=int.Parse(a[2],CultureInfo.InvariantCulture),anim=a.Length==4?int.Parse(a[3].Replace("anim%=",""),CultureInfo.InvariantCulture):100;
            if(n<1||n>100000||m<1||m>2048||anim<0||anim>100)throw new ArgumentOutOfRangeException("dr bench range");
            Stop();
            try{
                if(Marshal.SizeOf<CameraData>()!=80)throw new InvalidOperationException("DR camera ABI mismatch");
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11||!SystemInfo.supports2DArrayTextures)throw new InvalidOperationException("D3D11 Texture2DArray required");
                target=SceneTexture();if(!target)throw new InvalidOperationException("camRT required");
                foreach(var c in Camera.allCameras)if(c.targetTexture==target){camera=c;break;}
                if(!camera)throw new InvalidOperationException("camRT camera not found");
                textures=new Texture2DArray(8,8,m,TextureFormat.RGBA32,false,false){name="DR.M0.Synthetic",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[64];for(int k=0;k<m;k++){var c=new Color32((byte)(64+k*53%192),(byte)(64+k*97%192),(byte)(64+k*131%192),192);for(int p=0;p<64;p++)pixels[p]=c;textures.SetPixels32(pixels,k);}
                textures.Apply(false,true);token=sf_dr_create((uint)n,(uint)m,(uint)anim,3,textures.GetNativeTexturePtr());if(token==0)throw new InvalidOperationException("native DR setup failed");
                commands=new CommandBuffer{name="DR.M0.AfterForwardAlpha"};commands.IssuePluginEvent(sf_dr_event(),token);camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha,commands);
                level=ADOBase.customLevel!=null?ADOBase.customLevel.levelData:null;
                targetPtr=target.GetNativeTexturePtr();width=target.width;height=target.height;mainCount=mainSum=0;
                int found=0;var loop=EditLoop(PlayerLoop.GetCurrentPlayerLoop(),true,ref found);if(found!=1)throw new InvalidOperationException("DR PlayerLoop insertion count="+found);PlayerLoop.SetPlayerLoop(loop);
                Log("bench n="+n+" textures="+m+" animated_percent="+anim+" workers=3 rtsize="+width+"x"+height+" GPU_ms=unmeasured");
            }catch{Stop();throw;}return true;
        }
        static PlayerLoopSystem EditLoop(PlayerLoopSystem node,bool insert,ref int found){
            if(node.subSystemList==null)return node;var list=new List<PlayerLoopSystem>();
            foreach(var child in node.subSystemList){if(child.type==typeof(Marker))continue;list.Add(EditLoop(child,insert,ref found));
                if(insert&&child.type==typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)){list.Add(new PlayerLoopSystem{type=typeof(Marker),updateDelegate=Tick});found++;}}
            node.subSystemList=list.ToArray();return node;
        }
        static void Tick(){
            if(token==0)return;long start=Stopwatch.GetTimestamp();
            try{
                if(!camera||!target||camera.targetTexture!=target||SceneTexture()!=target||!ReferenceEquals(level,ADOBase.customLevel!=null?ADOBase.customLevel.levelData:null)){Stop();return;}
                // Pointer refresh only on a resize; native retains its own COM reference until consumed.
                if(width!=target.width||height!=target.height){width=target.width;height=target.height;targetPtr=target.GetNativeTexturePtr();}
                var p=camera.transform.position;CameraData c=new CameraData{Vp=GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix,Position=new Vector4(p.x,p.y,p.z,target.sRGB?1:0)};
                double time=scrConductor.instance!=null?scrConductor.instance.songposition_minusi:Time.timeAsDouble;
                time=((time%60)+60)%60;sf_dr_submit(token,time,ref c,targetPtr);
                long elapsed=Stopwatch.GetTimestamp()-start;mainSamples[mainCount++%mainSamples.Length]=elapsed;mainSum+=elapsed;
            }catch(Exception e){Log("failed: "+e.Message);Stop();}
        }
        internal static void Report(){
            if(token==0){Log("active=0");return;}
            long[] copy=(long[])mainSamples.Clone();int n=(int)Math.Min(mainCount,copy.Length);Array.Sort(copy,0,n);double factor=1e6/Stopwatch.Frequency;
            Log(FormattableString.Invariant($"frames={sf_dr_stat(token,0)} drawn={sf_dr_stat(token,1)} draws={sf_dr_stat(token,2)} active_tracks={sf_dr_stat(token,3)} skipped={sf_dr_stat(token,4)} errors={sf_dr_stat(token,5)} main_us_mean={(mainCount>0?(double)mainSum/mainCount*factor:0):F3} main_us_p95={(n>0?copy[(n*95+99)/100-1]*factor:0):F3}"));
            string[] names={"native_submit","worker_sum","worker_wall","render_with_wait","render_without_wait","render_wait"};
            for(int i=0;i<6;i++)Log(FormattableString.Invariant($"{names[i]}_us_mean={sf_dr_stat(token,6+i*2):F3} p95={sf_dr_stat(token,7+i*2):F3}"));
            Log("p95_window=last4096 mean_window=since_enable GPU_ms=unmeasured");
        }
        internal static void Stop(){
            if(commands!=null){if(camera)camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha,commands);commands.Release();commands=null;}
            int old=token;token=0;if(old!=0){int found=0;PlayerLoop.SetPlayerLoop(EditLoop(PlayerLoop.GetCurrentPlayerLoop(),false,ref found));sf_dr_destroy(old);}
            if(textures)UnityEngine.Object.Destroy(textures);textures=null;target=null;camera=null;level=null;targetPtr=IntPtr.Zero;
        }
    }
}
#endif
