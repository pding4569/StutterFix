#if UNITY_EDITOR
// Disposable D3D11 Editor experiment. Never included in product Compile items.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class Gpu5Discard
{
    [DllImport("gpuprobe")] static extern IntPtr gp_event();
    [DllImport("gpuprobe")] static extern ulong gp_status(int k);
    [DllImport("gpuprobe")] static extern int gp_snapshot([Out] ulong[] data,int n);
    static string OutDir => Environment.GetEnvironmentVariable("SF_GPU5_OUT");
    static string ShaderText = @"Shader ""Hidden/Gpu5Tile"" {
Properties { _MainTex(""Texture"",2D)=""white""{} _Color(""Color"",Color)=(1,1,1,1) _Tile(""Tile"",Vector)=(1,1,0,0) _Src(""Src"",Float)=1 _Dst(""Dst"",Float)=10 }
SubShader { Tags { ""Queue""=""Transparent"" } Cull Off ZWrite Off ZTest LEqual Blend [_Src] [_Dst]
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 4.0
#pragma multi_compile_local __ POINT_SAMPLE
#pragma multi_compile_local __ PIXELSNAP_ON
#pragma multi_compile_local __ DISCARD_ZERO DISCARD_TEX DISCARD_LOOSE DISCARD_ALPHA
#pragma multi_compile_local __ NONPREMULT
#include ""UnityCG.cginc""
sampler2D _MainTex; float4 _MainTex_TexelSize,_Color,_Tile;
struct app { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct vary { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
vary vert(app v) { vary o; o.vertex=mul(unity_ObjectToWorld,v.vertex);
#ifdef PIXELSNAP_ON
o.vertex=UnityPixelSnap(o.vertex);
#endif
o.color=v.color*_Color; o.uv=v.uv*_Tile.xy; return o; }
float4 frag(vary i):SV_Target {
float2 uv=i.uv;
#ifdef POINT_SAMPLE
uv=(floor(uv*_MainTex_TexelSize.zw)+0.5)/_MainTex_TexelSize.zw;
#endif
float4 t=tex2D(_MainTex,uv); float4 c=t*i.color;
#ifndef NONPREMULT
c.rgb*=c.a;
#endif
#ifdef DISCARD_ZERO
if(c.a==0 && all(c.rgb==0)) discard;
#endif
#ifdef DISCARD_TEX
if(t.a==0 && c.a==0 && all(c.rgb==0)) discard;
#endif
#ifdef DISCARD_LOOSE
if(c.a<=8.0/255.0) discard;
#endif
#ifdef DISCARD_ALPHA
if(c.a==0) discard;
#endif
return c;
}
ENDCG
} } }";
    public static void Prepare() { File.WriteAllText("Assets/Gpu5Tile.shader",ShaderText); AssetDatabase.Refresh(); }
    static RenderTexture rt; static Texture2D reader; static Mesh mesh; static Material mat; static IntPtr ev;
    static void Target(int w,int h) {
        if(rt){RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(reader);}
        rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.Create();
        reader=new Texture2D(w,h,TextureFormat.RGBA32,false,true);
    }
    static void Keyword(string k,bool on) {if(on)mat.EnableKeyword(k);else mat.DisableKeyword(k);}
    static void Mode(int mode) { foreach(var k in new[]{"DISCARD_ZERO","DISCARD_TEX","DISCARD_LOOSE","DISCARD_ALPHA"})mat.DisableKeyword(k); if(mode>0)mat.EnableKeyword(new[]{"","DISCARD_ZERO","DISCARD_TEX","DISCARD_LOOSE","DISCARD_ALPHA"}[mode]); }
    static void Sync() {RenderTexture.active=rt;reader.ReadPixels(new Rect(0,0,1,1),0,0,false);}
    static byte[] Read() {RenderTexture.active=rt;reader.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);reader.Apply(false,false);return reader.GetRawTextureData<byte>().ToArray();}
    static long Diff(byte[] a,byte[] b) {long n=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])n++;return n;}
    static void Event(int id) {var cb=new CommandBuffer();cb.IssuePluginEventAndData(ev,id,id==4?rt.GetNativeTexturePtr():IntPtr.Zero);Graphics.ExecuteCommandBuffer(cb);cb.Release();Sync();}
    static void Draw(int count,float angle,float area,bool flip,bool timed,int scope=3) {
        var cb=new CommandBuffer();cb.SetRenderTarget(rt);cb.SetGlobalVector("_ScreenParams",new Vector4(rt.width,rt.height,1f+1f/rt.width,1f+1f/rt.height));cb.ClearRenderTarget(true,true,new Color(.19f,.27f,.37f,.43f));
        if(timed){cb.IssuePluginEventAndData(ev,1,IntPtr.Zero);cb.IssuePluginEventAndData(ev,1000+scope,IntPtr.Zero);}
        float side=Mathf.Sqrt(area),aspect=(float)rt.width/rt.height;
        for(int i=0;i<count;i++) {
            var tr=Matrix4x4.TRS(new Vector3((i%3-1)*.0137f,(i%5-2)*.0091f,0),Quaternion.Euler(0,0,angle),new Vector3((flip?-1:1)*side/Mathf.Sqrt(aspect),side*Mathf.Sqrt(aspect),1));
            cb.DrawMesh(mesh,tr,mat,0,0);
        }
        if(timed){cb.IssuePluginEventAndData(ev,2000+scope,IntPtr.Zero);cb.IssuePluginEventAndData(ev,2,IntPtr.Zero);}
        Graphics.ExecuteCommandBuffer(cb);cb.Release();Sync();
    }
    static Texture2D Load(string path,bool cap) {
        var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!t.LoadImage(File.ReadAllBytes(path),false))throw new Exception(path);
        // Same alpha-weighted integer box reduction as Gpu4 fixture; not a game-resident texture capture.
        if(cap&&Math.Max(t.width,t.height)>1024) {
            int w=t.width,h=t.height,nw=(int)Math.Round(w*(double)(1024f/Math.Max(w,h))),nh=(int)Math.Round(h*(double)(1024f/Math.Max(w,h)));var p=t.GetPixels32();var q=new Color32[nw*nh];
            for(int y=0;y<nh;y++)for(int x=0;x<nw;x++) {
                int x0=(int)(x*(double)w/nw),x1=Math.Max(x0+1,(int)((x+1)*(double)w/nw)),y0=(int)(y*(double)h/nh),y1=Math.Max(y0+1,(int)((y+1)*(double)h/nh));long r=0,g=0,b=0,a=0,ur=0,ug=0,ub=0,n=0;
                for(int yy=y0;yy<y1;yy++)for(int xx=x0;xx<x1;xx++){var c=p[yy*w+xx];r+=c.r*c.a;g+=c.g*c.a;b+=c.b*c.a;a+=c.a;ur+=c.r;ug+=c.g;ub+=c.b;n++;}
                q[y*nw+x]=new Color32((byte)(a>0?(r+a/2)/a:(ur+n/2)/n),(byte)(a>0?(g+a/2)/a:(ug+n/2)/n),(byte)(a>0?(b+a/2)/a:(ub+n/2)/n),(byte)((a+n/2)/n));
            }
            UnityEngine.Object.DestroyImmediate(t);t=new Texture2D(nw,nh,TextureFormat.RGBA32,false);t.SetPixels32(q);t.Apply(false,false);
        }
        t.wrapMode=TextureWrapMode.Clamp;return t;
    }
    public static void Extra() {
        Directory.CreateDirectory(OutDir);
        mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();mesh.triangles=new[]{0,1,2,0,2,3};mat=new Material(Shader.Find("Hidden/Gpu5Tile"));
        var tex=new Texture2D(8,8,TextureFormat.RGBA32,false);tex.SetPixels32(Enumerable.Range(0,64).Select(i=>new Color32((byte)(50+i*3),90,200,(byte)(i%3==0?0:64))).ToArray());tex.Apply();tex.wrapMode=TextureWrapMode.Clamp;mat.mainTexture=tex;
        using(var f=new StreamWriter(Path.Combine(OutDir,"synthetic.tsv"))) {
            f.WriteLine("width\tfilter\tpoint_variant\tzero_color_alpha\tlayers\tsafe_diff\ttex_diff\tloose_diff");
            foreach(int w in new[]{3440,1720,513})foreach(bool point in new[]{false,true})foreach(bool pv in new[]{false,true})foreach(bool zero in new[]{false,true})foreach(int n in new[]{1,3,100}) {
                Target(w,w==3440?1440:w==1720?720:287);tex.filterMode=point?FilterMode.Point:FilterMode.Bilinear;Keyword("POINT_SAMPLE",pv);mat.color=new Color(.71f,.83f,.91f,zero?0:.57f);
                Mode(0);Draw(n,63,.5f,true,false);var a=Read();Mode(1);Draw(n,63,.5f,true,false);long safe=Diff(a,Read());Mode(2);Draw(n,63,.5f,true,false);long td=Diff(a,Read());Mode(3);Draw(n,63,.5f,true,false);long loose=Diff(a,Read());f.WriteLine(string.Join("\t",w,point,pv,zero,n,safe,td,loose));f.Flush();if(safe!=0||td!=0)throw new Exception("synthetic parity failed");
            }
        }
        File.WriteAllText(Path.Combine(OutDir,"extra-done.txt"),"completed");
    }
    public static void Retest() {
        Extra();mesh.colors=Enumerable.Repeat(new Color(.83f,.71f,.91f,.57f),4).ToArray();
        mat.color=new Color(.79f,.87f,.67f,.63f);Keyword("POINT_SAMPLE",false);Keyword("PIXELSNAP_ON",false);Mode(0);Target(3440,1440);ev=gp_event();Event(4);
        using(var f=new StreamWriter(Path.Combine(OutDir,"timing-retest.tsv"))) {
            f.WriteLine("image\tcap\twidth\tarea\tlayers\tblock\tmode\tsamples\tmean_ms\tbad\tfull\terrors");f.AutoFlush=true;
            foreach(var path in Directory.GetFiles("D:/얼불춤 맵 파일/Arche","*.png",SearchOption.AllDirectories).Where(p=>new[]{"fog.png","nes_black_fog.png","nes_large_fog.png"}.Contains(Path.GetFileName(p))).OrderBy(p=>p)) {
            var tex=Load(path,true);tex.filterMode=FilterMode.Bilinear;mat.mainTexture=tex;
            for(int block=0;block<8;block++) {
                int mode=new[]{0,1,1,0}[block%4];Mode(mode);for(int i=0;i<64;i++)Draw(50,0,.5f,false,true);Event(5);
                for(int i=0;i<64;i++)Draw(50,0,.5f,false,true);for(int i=0;i<8;i++)Draw(0,0,1,false,true,4);
                var stats=new ulong[2048];if(gp_snapshot(stats,2048)!=1||stats[12]!=64)throw new Exception("retest missing query");f.WriteLine(string.Join("\t",Path.GetFileName(path),true,3440,.5f,50,block,mode,stats[12],stats[13]/(double)stats[12]/1e6,gp_status(2),gp_status(3),gp_status(6)));if(gp_status(2)+gp_status(3)+gp_status(6)>0)throw new Exception("retest bad query");
            }
            UnityEngine.Object.DestroyImmediate(tex);
            }
        }
        Event(3);File.WriteAllText(Path.Combine(OutDir,"retest-done.txt"),"completed");
    }
    public static void Run() {
        Directory.CreateDirectory(OutDir);if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11)throw new Exception("D3D11 required");
        File.WriteAllText(Path.Combine(OutDir,"environment.txt"),Application.unityVersion+"\n"+SystemInfo.graphicsDeviceName+"\n"+SystemInfo.graphicsDeviceType+"\n"+QualitySettings.activeColorSpace);
        mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};mesh.colors=Enumerable.Repeat(new Color(.83f,.71f,.91f,.57f),4).ToArray();mesh.triangles=new[]{0,1,2,0,2,3};
        mat=new Material(Shader.Find("Hidden/Gpu5Tile"));if(!mat.shader.isSupported)throw new Exception("shader unsupported");ev=gp_event();Target(128,128);Event(4);if(gp_status(0)!=1)throw new Exception("query init failed");
        var paths=Directory.GetFiles("D:/얼불춤 맵 파일/Arche","*.png",SearchOption.AllDirectories).Where(p=>new[]{"fog.png","nes_large_fog.png","nes_black_fog.png"}.Contains(Path.GetFileName(p))).OrderBy(p=>p).ToArray();if(paths.Length!=3)throw new Exception("expected three fog files: "+paths.Length);
        using(var val=new StreamWriter(Path.Combine(OutDir,"validation.tsv")))using(var times=new StreamWriter(Path.Combine(OutDir,"timing.tsv")))using(var alpha=new StreamWriter(Path.Combine(OutDir,"alpha.tsv"))) {
            val.AutoFlush=times.AutoFlush=alpha.AutoFlush=true;
            val.WriteLine("image\tcap\twidth\tfilter\tvariant\ttransform\tlayers\tcolorzero\tdiff\ttexdiff\trepeat\tvisible\tloose");
            times.WriteLine("image\tcap\twidth\tarea\tlayers\tblock\tmode\tsamples\tmean_ms\tbad\tfull\terrors");alpha.WriteLine("image\tcap\tw\th\tzero\tzero_rgb_nonzero\ttotal");
            foreach(var path in paths)foreach(bool cap in new[]{false,true}) {
                var tex=Load(path,cap);var px=tex.GetPixels32();alpha.WriteLine(string.Join("\t",Path.GetFileName(path),cap,tex.width,tex.height,px.Count(c=>c.a==0),px.Count(c=>c.a==0&&(c.r!=0||c.g!=0||c.b!=0)),px.Length));mat.mainTexture=tex;
                foreach(int w in new[]{3440,1720,513}) {
                    Target(w,w==3440?1440:w==1720?720:287);
                    foreach(bool point in new[]{false,true})foreach(int variant in new[]{0,1,2,3})foreach(int transform in new[]{0,1})foreach(int n in new[]{1,3,100})foreach(bool zero in new[]{false,true}) {
                        tex.filterMode=point?FilterMode.Point:FilterMode.Bilinear;Keyword("POINT_SAMPLE",(variant&1)!=0);Keyword("PIXELSNAP_ON",(variant&2)!=0);mat.color=new Color(.79f,.87f,.67f,zero?0:.63f);
                        float angle=transform==0?0:17,area=transform==0?.5f:3;bool flip=transform!=0;
                        Mode(0);Draw(n,angle,area,flip,false);var a=Read();Draw(n,angle,area,flip,false);long repeat=Diff(a,Read());
                        Mode(1);Draw(n,angle,area,flip,false);long diff=Diff(a,Read());Mode(2);Draw(n,angle,area,flip,false);long td=Diff(a,Read());Mode(3);Draw(n,angle,area,flip,false);long loose=Diff(a,Read());Draw(0,0,1,false,false);long visible=Diff(a,Read());
                        val.WriteLine(string.Join("\t",Path.GetFileName(path),cap,w,point,variant,transform,n,zero,diff,td,repeat,visible,loose));if(diff!=0||td!=0||repeat!=0||(!zero&&visible==0))throw new Exception("validation failed");
                    }
                }
                // Actual integer-cap RGBA32, default bilinear variant; independent condition blocks ABBA repeated.
                if(cap)foreach(int w in new[]{3440,1720})foreach(float area in new[]{.5f,3f})foreach(int n in new[]{50,100,200}) {
                    Target(w,w==3440?1440:720);tex.filterMode=FilterMode.Bilinear;Keyword("POINT_SAMPLE",false);Keyword("PIXELSNAP_ON",false);mat.color=new Color(.79f,.87f,.67f,.63f);
                    for(int block=0;block<8;block++) {
                        int mode=new[]{0,1,1,0}[block%4];Mode(mode);for(int i=0;i<8;i++)Draw(n,0,area,false,false);Event(5);
                        for(int i=0;i<32;i++)Draw(n,0,area,false,true);
                        for(int i=0;i<8;i++)Draw(0,0,1,false,true,4);
                        var stats=new ulong[2048];if(gp_snapshot(stats,stats.Length)!=1||stats[12]!=32)throw new Exception("missing timestamps "+stats[12]);
                        times.WriteLine(string.Join("\t",Path.GetFileName(path),cap,w,area,n,block,mode,stats[12],stats[13]/(double)stats[12]/1e6,gp_status(2),gp_status(3),gp_status(6)));
                        if(gp_status(2)+gp_status(3)+gp_status(6)>0)throw new Exception("invalid query status");
                    }
                }
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
        // Non-premultiplied alpha=0 / RGB!=0: alpha-only discard must fail; safe discard must pass.
        Target(513,287);var synthetic=new Texture2D(2,2,TextureFormat.RGBA32,false);synthetic.SetPixels32(Enumerable.Repeat(new Color32(200,80,30,0),4).ToArray());synthetic.Apply();mat.mainTexture=synthetic;Keyword("NONPREMULT",true);mat.color=Color.white;
        using(var p=new StreamWriter(Path.Combine(OutDir,"positive.tsv"))) {
            p.WriteLine("src\tdst\tsafe_diff\talpha_only_diff");
            foreach(var blend in new[]{new[]{1,1},new[]{5,10},new[]{1,10}}) {
                mat.SetFloat("_Src",blend[0]);mat.SetFloat("_Dst",blend[1]);Mode(0);Draw(3,0,.5f,false,false);var a=Read();Mode(1);Draw(3,0,.5f,false,false);long safe=Diff(a,Read());Mode(4);Draw(3,0,.5f,false,false);long unsafeDiff=Diff(a,Read());p.WriteLine(string.Join("\t",blend[0],blend[1],safe,unsafeDiff));if(safe!=0||(blend[0]==1&&unsafeDiff==0))throw new Exception("positive failed");
            }
        }
        Event(3);File.WriteAllText(Path.Combine(OutDir,"done.txt"),"completed");
    }
}
#endif
