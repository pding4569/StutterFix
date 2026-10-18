#if UNITY_EDITOR
// Isolated disposable Editor experiment only; excluded from product Compile items.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Unity.Collections;
public static class Gpu4Tight
{
    public static void StartPlay(){SessionState.SetBool("GPU4_RUN",true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod] static void RegisterPlay(){EditorApplication.update+=RunWhenPlaying;}
    static void RunWhenPlaying(){if(!EditorApplication.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool("GPU4_RUN",false))return;SessionState.SetBool("GPU4_RUN",false);try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    static string output; static Camera cam; static RenderTexture rt; static Texture2D read;
    static readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
    static readonly List<SpriteRenderer> renderers=new List<SpriteRenderer>();
    static StreamWriter rows,geometry,alpha,details;
    static T Keep<T>(T x) where T:UnityEngine.Object {owned.Add(x);return x;}
    static void Clear(){foreach(var x in owned)if(x)UnityEngine.Object.DestroyImmediate(x);owned.Clear();renderers.Clear();}
    static Sprite Make(Texture2D t,Vector2 pivot,SpriteMeshType type=SpriteMeshType.FullRect){return Keep(Sprite.Create(t,new Rect(0,0,t.width,t.height),pivot,100,0,type));}
    static Texture2D Load(string path,bool reduced,bool mip,bool dxt)
    {
        var tmp=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!tmp.LoadImage(File.ReadAllBytes(path),false))throw new Exception(path);
        int w=tmp.width,h=tmp.height;var px=tmp.GetPixels32();UnityEngine.Object.DestroyImmediate(tmp);
        if(reduced&&Math.Max(w,h)>1024)
        {
            float f=1024f/Math.Max(w,h);int nw=Math.Max(1,(int)Math.Round(w*(double)f)),nh=Math.Max(1,(int)Math.Round(h*(double)f));
            var a=new Color32[nw*nh];double sx=(double)w/nw,sy=(double)h/nh;
            for(int y=0;y<nh;y++)for(int x=0;x<nw;x++)
            {
                int x0=(int)(x*sx),x1=Math.Max(x0+1,Math.Min(w,(int)((x+1)*sx))),y0=(int)(y*sy),y1=Math.Max(y0+1,Math.Min(h,(int)((y+1)*sy)));
                double r=0,g=0,b=0,al=0,ur=0,ug=0,ub=0;int n=0;
                for(int yy=y0;yy<y1;yy++)for(int xx=x0;xx<x1;xx++){var c=px[yy*w+xx];r+=c.r*c.a;g+=c.g*c.a;b+=c.b*c.a;al+=c.a;ur+=c.r;ug+=c.g;ub+=c.b;n++;}
                a[y*nw+x]=new Color32((byte)((al>0?r/al:ur/n)+.5),(byte)((al>0?g/al:ug/n)+.5),(byte)((al>0?b/al:ub/n)+.5),(byte)(al/n+.5));
            }
            w=nw;h=nh;px=a;
        }
        var t=Keep(new Texture2D(w,h,TextureFormat.RGBA32,mip));t.SetPixels32(px);t.Apply(mip,false);t.wrapMode=TextureWrapMode.Clamp;t.anisoLevel=1;
        if(dxt)t.Compress(false);
        return t;
    }
    static Color32[] GpuPixels(Texture2D t)
    {
        var old=RenderTexture.active;var filter=t.filterMode;t.filterMode=FilterMode.Point;
        var target=new RenderTexture(t.width,t.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
        var reader=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false,true);
        try{Graphics.Blit(t,target);RenderTexture.active=target;reader.ReadPixels(new Rect(0,0,t.width,t.height),0,0,false);reader.Apply(false,false);return reader.GetPixels32();}
        finally{RenderTexture.active=old;t.filterMode=filter;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(reader);}
    }
    // Square dilation in final texture texels, alpha>0 only. Prefix sums avoid radius-dependent work.
    static bool[] Mask(Color32[] p,int w,int h,int pad)
    {
        int stride=w+1;var sum=new int[(w+1)*(h+1)];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)sum[(y+1)*stride+x+1]=(p[y*w+x].a>0?1:0)+sum[y*stride+x+1]+sum[(y+1)*stride+x]-sum[y*stride+x];
        var mask=new bool[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int l=Math.Max(0,x-pad),r=Math.Min(w,x+pad+1),b=Math.Max(0,y-pad),u=Math.Min(h,y+pad+1);mask[y*w+x]=sum[u*stride+r]-sum[b*stride+r]-sum[u*stride+l]+sum[b*stride+l]>0;}return mask;
    }
    static Sprite Candidate(Texture2D t,Vector2 pivot,bool[] mask,string method,bool boundsKeep=false)
    {
        int w=t.width,h=t.height;var s=Make(t,pivot,SpriteMeshType.Tight);var v=new List<Vector2>();var tr=new List<ushort>();var lookup=new Dictionary<long,ushort>();
        ushort V(int x,int y){long key=((long)y<<32)|(uint)x;if(!lookup.TryGetValue(key,out var i)){if(v.Count>=65530)throw new Exception("vertex cap");i=(ushort)v.Count;lookup.Add(key,i);v.Add(new Vector2(x,y));}return i;}
        void Quad(int l,int b,int r,int u){ushort a=V(l,b),c=V(l,u),d=V(r,u),e=V(r,b);tr.AddRange(new[]{a,c,d,a,d,e});}
        if(method=="bbox")
        {int l=w,b=h,r=0,u=0;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(mask[y*w+x]){l=Math.Min(l,x);b=Math.Min(b,y);r=Math.Max(r,x+1);u=Math.Max(u,y+1);}Quad(l,b,r,u);}
        else if(method=="grid16")
        {for(int y=0;y<h;y+=16)for(int x=0;x<w;x+=16){bool hit=false;for(int yy=y;yy<Math.Min(h,y+16)&&!hit;yy++)for(int xx=x;xx<Math.Min(w,x+16);xx++)if(mask[yy*w+xx]){hit=true;break;}if(hit)Quad(x,y,Math.Min(w,x+16),Math.Min(h,y+16));}}
        else
        {
            var m=Keep(new Texture2D(w,h,TextureFormat.RGBA32,false));m.SetPixels32(mask.Select(z=>new Color32(255,255,255,(byte)(z?255:0))).ToArray());m.Apply(false,false);
            var tight=Make(m,pivot,SpriteMeshType.Tight);v.AddRange(tight.vertices.Select(z=>z*100+new Vector2(pivot.x*w,pivot.y*h)));tr.AddRange(tight.triangles);
        }
        if(boundsKeep){V(0,0);V(w,h);} // Unreferenced extremal vertices: test whether Sprite bounds preserve them.
        // Runtime Sprite.Create sprites reject OverrideGeometry in this Editor build.
        // Explicit SpriteDataAccess is a separate geometry path; preserve texture/rect/pivot/ppu.
        s.SetVertexCount(v.Count);
        using(var position=new NativeArray<Vector3>(v.Select(z=>new Vector3((z.x-pivot.x*w)/100,(z.y-pivot.y*h)/100,0)).ToArray(),Allocator.Temp))s.SetVertexAttribute(VertexAttribute.Position,position);
        using(var texcoord=new NativeArray<Vector2>(v.Select(z=>new Vector2(z.x/w,z.y/h)).ToArray(),Allocator.Temp))s.SetVertexAttribute(VertexAttribute.TexCoord0,texcoord);
        using(var index=new NativeArray<ushort>(tr.ToArray(),Allocator.Temp))s.SetIndices(index);
        if(s.vertices.Length!=v.Count||!s.triangles.SequenceEqual(tr))throw new Exception("SpriteDataAccess rejected: "+method);
        for(int i=0;i<v.Count;i++)if(Vector2.Distance(s.vertices[i],(v[i]-new Vector2(pivot.x*w,pivot.y*h))/100)>.0001f)throw new Exception("geometry coordinates rejected");return s;
    }
    static double Area(Sprite s){double a=0;var v=s.vertices;var t=s.triangles;for(int i=0;i<t.Length;i+=3){var b=v[t[i+1]]-v[t[i]];var c=v[t[i+2]]-v[t[i]];a+=Math.Abs((double)b.x*c.y-(double)b.y*c.x)/2;}return a;}
    static void Target(int w,int h){if(rt){cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(read);}rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.antiAliasing=1;rt.Create();read=new Texture2D(w,h,TextureFormat.RGBA32,false,true);cam.targetTexture=rt;}
    static byte[] Draw(){cam.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);read.Apply(false,false);return read.GetRawTextureData<byte>().ToArray();}
    static (int bytes,int pixels,int max,int first) Diff(byte[] a,byte[] b){int n=0,p=0,m=0,f=-1;for(int i=0;i<a.Length;i+=4){bool bad=false;for(int k=0;k<4;k++)if(a[i+k]!=b[i+k]){n++;bad=true;m=Math.Max(m,Math.Abs(a[i+k]-b[i+k]));if(f<0)f=i;}if(bad)p++;}return(n,p,m,f);}
    static void Setup(Sprite s,float angle,float scale,bool flip,int layers)
    {
        foreach(var r in renderers)if(r)UnityEngine.Object.DestroyImmediate(r.gameObject);renderers.Clear();
        for(int i=0;i<layers;i++){var go=new GameObject("tight-"+i);go.layer=30;owned.Add(go);var r=go.AddComponent<SpriteRenderer>();r.sprite=s;r.sharedMaterial=Keep(new Material(Shader.Find("Sprites/Default")));r.sortingOrder=i;r.color=new Color(.83f,.71f,.91f,.57f);go.transform.position=new Vector3(.137f+i*.071f,-.193f+i*.037f,0);go.transform.rotation=Quaternion.Euler(0,0,angle);go.transform.localScale=new Vector3(flip?-scale:scale,scale,1);renderers.Add(r);}
    }
    static void SetSprite(Sprite s){foreach(var r in renderers)r.sprite=s;}
    static void Positive()
    {
        Target(1720,720);cam.orthographicSize=1.2f;var t=Keep(new Texture2D(64,64,TextureFormat.RGBA32,false));t.SetPixels32(Enumerable.Repeat(new Color32(255,255,255,1),4096).ToArray());t.Apply();t.filterMode=FilterMode.Point;
        var bg=cam.backgroundColor;cam.backgroundColor=Color.black;
        var s=Make(t,new Vector2(.5f,.5f));Setup(s,0,3,false,3);foreach(var r in renderers)r.color=Color.white;var a=Draw();foreach(var r in renderers)r.enabled=false;var b=Draw();var d=Diff(a,b);File.WriteAllText(Path.Combine(output,"positive.txt"),"cut alpha=1 entire sprite: "+d+"\n");if(d.bytes==0)throw new Exception("alpha1 positive failed");cam.backgroundColor=bg;Clear();
    }
    public static void ApiProbe()
    {
        output=Environment.GetEnvironmentVariable("SF_GPU4_OUT");Directory.CreateDirectory(output);
        var t=new Texture2D(16,16,TextureFormat.RGBA32,false);t.SetPixels32(Enumerable.Repeat(new Color32(255,255,255,255),256).ToArray());t.Apply(false,false);
        using(var log=new StreamWriter(Path.Combine(output,"api-probe.txt")))foreach(var type in new[]{SpriteMeshType.FullRect,SpriteMeshType.Tight})foreach(int n in new[]{3,4})
        {var s=Sprite.Create(t,new Rect(0,0,16,16),new Vector2(.5f,.5f),100,0,type);var v=n==3?new[]{new Vector2(1,1),new Vector2(1,15),new Vector2(15,15)}:new[]{new Vector2(0,0),new Vector2(0,16),new Vector2(16,16),new Vector2(16,0)};var indices=n==3?new ushort[]{0,1,2}:new ushort[]{0,1,2,0,2,3};s.OverrideGeometry(v,indices);log.WriteLine(type+" n="+n+" vertices="+string.Join(";",s.vertices.Select(x=>x.ToString("F5")))+" triangles="+string.Join(",",s.triangles));UnityEngine.Object.DestroyImmediate(s);}
        var ss=Sprite.Create(t,new Rect(0,0,16,16),new Vector2(.5f,.5f),100,0,SpriteMeshType.Tight);ss.SetVertexCount(3);
        File.AppendAllText(Path.Combine(output,"api-probe.txt"),"channels="+string.Join(",",Enum.GetValues(typeof(VertexAttribute)).Cast<VertexAttribute>().Where(a=>ss.HasVertexAttribute(a)))+"\n");
        using(var vv=new NativeArray<Vector3>(new[]{new Vector3(-.07f,-.07f,0),new Vector3(-.07f,.07f,0),new Vector3(.07f,.07f,0)},Allocator.Temp))ss.SetVertexAttribute(VertexAttribute.Position,vv);
        using(var ii=new NativeArray<ushort>(new ushort[]{0,1,2},Allocator.Temp))ss.SetIndices(ii);
        File.AppendAllText(Path.Combine(output,"api-probe.txt"),"SetVertexCount path vertices="+ss.vertices.Length+" triangles="+string.Join(",",ss.triangles)+"\n");
        UnityEngine.Object.DestroyImmediate(ss);UnityEngine.Object.DestroyImmediate(t);
    }
    public static void Run()
    {
        output=Environment.GetEnvironmentVariable("SF_GPU4_OUT");Directory.CreateDirectory(output);var previous=RenderTexture.active;GameObject cg=null;
        try
        {
            cg=new GameObject("GPU4 independent camera");cam=cg.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.transform.position=new Vector3(0,0,-10);cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.19f,.27f,.37f,1);cam.allowHDR=false;cam.allowMSAA=false;cam.transparencySortMode=TransparencySortMode.Orthographic;
            File.WriteAllText(Path.Combine(output,"environment.txt"),"unity="+Application.unityVersion+"\napi="+SystemInfo.graphicsDeviceType+"\ngpu="+SystemInfo.graphicsDeviceName+"\ncolorspace="+QualitySettings.activeColorSpace+"\nTile_available="+(Shader.Find("Sprites/Tile")!=null)+"\nshader=Sprites/Default\n");
            Positive();
            using(rows=new StreamWriter(Path.Combine(output,"validation.tsv")))using(geometry=new StreamWriter(Path.Combine(output,"geometry.tsv")))using(alpha=new StreamWriter(Path.Combine(output,"alpha.tsv")))using(details=new StreamWriter(Path.Combine(output,"differences.tsv")))
            {
                rows.AutoFlush=geometry.AutoFlush=alpha.AutoFlush=details.AutoFlush=true;
                rows.WriteLine("image\tinput\tmip\tfilter\tpad\tmethod\trt\tangle\tscale\tflip\tpivot\tlayers\tdiff_bytes\tdiff_pixels\tmax_error\trepeat_bytes\tvisible_bytes");
                geometry.WriteLine("image\tinput\tmip\tpad\tmethod\tvertices\ttriangles\tarea_ratio\tfull_bounds\tnew_bounds\tkeep_bounds\tuv_max_error");
                alpha.WriteLine("image\tinput\tmip\tformat\tw\th\tzero\tle8\tpositive\tsha256\tcpu_gpu_alpha_differences");details.WriteLine("condition\tx\ty\tbaseline_rgba\tcandidate_rgba\tlocal_texel\tnearest_alpha\tclassification");
                var files=Directory.GetFiles("D:/얼불춤 맵 파일/Arche","*.png",SearchOption.AllDirectories).Where(x=>new[]{"fog.png","nes_large_fog.png","nes_black_fog.png","nev_fog.png"}.Contains(Path.GetFileName(x))).OrderBy(x=>x).ToArray();
                foreach(var path in files)foreach(string input in (Environment.GetEnvironmentVariable("SF_GPU4_DXTONLY")=="1"?new[]{"cap1024_dxt"}:new[]{"original","cap1024","cap1024_dxt"}))foreach(bool mip in new[]{false,true})
                {
                    var t=Load(path,input!="original",mip,input.EndsWith("dxt"));string image=Path.GetFileName(path);var cpu=t.GetPixels32();var px=GpuPixels(t);int alphaDiff=cpu.Where((c,i)=>c.a!=px[i].a).Count();string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(t.GetRawTextureData<byte>().ToArray())).Replace("-","");
                    alpha.WriteLine(string.Join("\t",image,input,mip,t.format,t.width,t.height,px.Count(x=>x.a==0),px.Count(x=>x.a<=8),px.Count(x=>x.a>0),hash,alphaDiff));
                    if(input.EndsWith("dxt")&&t.format!=TextureFormat.DXT5){File.AppendAllText(Path.Combine(output,"skips.txt"),image+" "+input+" "+t.width+"x"+t.height+" actual="+t.format+" NOT DXT5\n");Clear();continue;}
                    foreach(int pad in new[]{2,3,4,8})
                    {
                        var mask=Mask(px,t.width,t.height,pad);
                        foreach(string method in new[]{"bbox","grid16","tight"})
                        {
                            var pivot=new Vector2(.5f,.5f);var full=Make(t,pivot);var candidate=Candidate(t,pivot,mask,method);var keep=Candidate(t,pivot,mask,method,true);double uvError=0;var vv=candidate.vertices;var uv=candidate.uv;for(int i=0;i<vv.Length;i++)uvError=Math.Max(uvError,Vector2.Distance(uv[i],new Vector2((vv[i].x*100+pivot.x*t.width)/t.width,(vv[i].y*100+pivot.y*t.height)/t.height)));
                            geometry.WriteLine(string.Join("\t",image,input,mip,pad,method,vv.Length,candidate.triangles.Length/3,Area(candidate)/(t.width*t.height/10000.0),full.bounds,candidate.bounds,keep.bounds,uvError));
                            if(Area(candidate)/Area(full)>.99999){File.AppendAllText(Path.Combine(output,"no-saving.txt"),image+" "+input+" mip="+mip+" pad="+pad+" "+method+" full area; not promoted\n");continue;}
                            // Reject independently per filter/resolution at the first nonzero byte. Do not time a rejected method.
                            foreach(FilterMode filter in new[]{FilterMode.Bilinear,FilterMode.Point})foreach(int width in new[]{3440,1720})
                            {
                                t.filterMode=filter;Target(width,width==3440?1440:720);cam.orthographicSize=Math.Max(t.width,t.height)/100f*.8f;bool rejected=false;
                                foreach(var pv in new[]{new Vector2(.5f,.5f),new Vector2(.23f,.71f)})
                                {if(rejected)break;var f=Make(t,pv);var c=Candidate(t,pv,mask,method);
                                    foreach(float angle in new[]{0f,17f,63f}){if(rejected)break;foreach(float scale in new[]{.7f,1.3f,3f}){if(rejected)break;foreach(bool flip in new[]{false,true}){if(rejected)break;foreach(int layers in new[]{1,3})
                                    {
                                        Setup(f,angle,scale,flip,layers);var a=Draw();var repeat=Diff(a,Draw());SetSprite(c);var b=Draw();var diff=Diff(a,b);foreach(var r in renderers)r.enabled=false;int visible=Diff(a,Draw()).bytes;
                                        var condition=string.Join("\t",image,input,mip,filter,pad,method,width,angle,scale,flip,pv,layers);rows.WriteLine(condition+"\t"+string.Join("\t",diff.bytes,diff.pixels,diff.max,repeat.bytes,visible));
                                        if(repeat.bytes!=0||visible==0)throw new Exception("invalid baseline "+condition);
                                        if(diff.bytes>0)
                                        {
                                            int saved=0;for(int bi=0;bi<a.Length&&saved<12;bi+=4)if(a[bi]!=b[bi]||a[bi+1]!=b[bi+1]||a[bi+2]!=b[bi+2]||a[bi+3]!=b[bi+3])
                                            {int x=bi/4%rt.width,y=bi/4/rt.width;var world=cam.ScreenToWorldPoint(new Vector3(x+.5f,y+.5f,10));var local=renderers[0].transform.InverseTransformPoint(world);int tx=Mathf.FloorToInt(local.x*100+pv.x*t.width),ty=Mathf.FloorToInt(local.y*100+pv.y*t.height);int al=tx>=0&&ty>=0&&tx<t.width&&ty<t.height?px[ty*t.width+tx].a:-1;details.WriteLine(condition.Replace('\t','|')+"\t"+x+"\t"+y+"\t"+string.Join(",",a.Skip(bi).Take(4))+"\t"+string.Join(",",b.Skip(bi).Take(4))+"\t"+tx+","+ty+"\t"+al+"\t"+(layers==1&&al>8?"nonzero_interior_nearest":"edge_or_overlap"));saved++;}
                                            rejected=true;break;
                                        }
                                    }}}}
                                }
                            }
                        }
                    }
                    Clear();GC.Collect();
                }
            }
            File.WriteAllText(Path.Combine(output,"done.txt"),"completed; fail-fast per method/pad/input/mip/filter/resolution; unvisited transforms are NOT validated\n");
        }
        finally{Clear();RenderTexture.active=previous;if(cg)UnityEngine.Object.DestroyImmediate(cg);if(read)UnityEngine.Object.DestroyImmediate(read);if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
    }
}
#endif
