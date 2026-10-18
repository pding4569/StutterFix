#if UNITY_EDITOR
// Standalone Editor fixture. Never compiled into StutterFix or copied to Mods.
// Run only in a disposable built-in-renderer Unity project, without -nographics.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class Gpu3Fixture
{
    static readonly string Out=Environment.GetEnvironmentVariable("SF_GPU3_OUT");
    static readonly string Input="D:/얼불춤 맵 파일/Arche";
    static Camera cam; static RenderTexture rt; static Texture2D read;
    static readonly List<UnityEngine.Object> objects=new List<UnityEngine.Object>();
    static readonly List<string> sortRows=new List<string>();
    static readonly List<string> sampleRows=new List<string>();
    static T Keep<T>(T o) where T:UnityEngine.Object {objects.Add(o);return o;}
    static void ClearObjects(){foreach(var x in objects)if(x)UnityEngine.Object.DestroyImmediate(x);objects.Clear();}
    static int[][] P={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
    static Texture2D White(int w,int h){var t=Keep(new Texture2D(w,h,TextureFormat.RGBA32,false));var c=Enumerable.Repeat(new Color32(255,255,255,255),w*h).ToArray();t.SetPixels32(c);t.Apply(false,false);return t;}
    static Sprite Make(Texture2D t,Rect r,Vector2 pivot,float ppu){return Keep(Sprite.Create(t,r,pivot,ppu,0,SpriteMeshType.FullRect));}
    static byte[] Draw(){cam.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,128,128),0,0,false);read.Apply(false,false);return read.GetRawTextureData<byte>().ToArray();}
    static int Diff(byte[] a,byte[] b){int n=0;for(int i=0;i<a.Length;i+=4)if(a[i]!=b[i]||a[i+1]!=b[i+1]||a[i+2]!=b[i+2]||a[i+3]!=b[i+3])n++;return n;}
    static int Lit(byte[] a){int n=0;for(int i=0;i<a.Length;i+=4)if(a[i]+a[i+1]+a[i+2]>0)n++;return n;}
    public static void Run()
    {
        var previous=RenderTexture.active;GameObject cg=null;
        try
        {
            Directory.CreateDirectory(Out);
            cg=new GameObject("GPU3 isolated fixture camera");cam=cg.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=1.1f;cam.transform.position=new Vector3(0,0,-10);cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.allowHDR=false;cam.allowMSAA=false;cam.transparencySortMode=TransparencySortMode.Orthographic;
            rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.antiAliasing=1;rt.Create();cam.targetTexture=rt;read=new Texture2D(128,128,TextureFormat.RGBA32,false,true);
            File.WriteAllText(Path.Combine(Out,"environment.txt"),"unity="+Application.unityVersion+"\ndevice="+SystemInfo.graphicsDeviceName+"\napi="+SystemInfo.graphicsDeviceType+"\ncolorspace="+QualitySettings.activeColorSpace+"\nshader=Sprites/Default\nscope=Editor-not-game-not-Tile\n");
            if(Environment.GetEnvironmentVariable("SF_GPU3_SORTONLY")=="1") Sorting(); else if(Environment.GetEnvironmentVariable("SF_GPU3_COMPRESSED")=="1") Sampling(true); else if(Environment.GetEnvironmentVariable("SF_GPU3_SAMPLEONLY")=="1") Sampling(); else { Sorting(); Sampling(); SerializedSettings(); }
            File.WriteAllText(Path.Combine(Out,"done.txt"),"completed");
        }
        finally {ClearObjects();RenderTexture.active=previous;if(cg)UnityEngine.Object.DestroyImmediate(cg);if(read)UnityEngine.Object.DestroyImmediate(read);if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
    }
    static void Sorting()
    {
        sortRows.Add("layers\ttexture_creation\tmaterial_creation\trenderer_creation\tbaseline_order_matches\tshared_order_matches\tatlas_order_matches\tmaterial_ids\trenderer_ids\ttexture_ids\tshared_diff_pixels\tatlas_diff_pixels\tpositive_pixels\tshared_material_confirmed\thierarchy_reverse_diff\tpositive_order_diff\tsame_state_repeat_diff");
        foreach(int layers in new[]{1,2})foreach(int textureDirection in new[]{0,1})foreach(var mp in P)foreach(var rp in P)
        {
            var ts=new Texture2D[3];var ss=new Sprite[3];var ms=new Material[3];var rs=new SpriteRenderer[3];
            foreach(int i in (textureDirection==0?P[0]:P[5])){ts[i]=White(4,4);ss[i]=Make(ts[i],new Rect(0,0,4,4),new Vector2(.5f,.5f),4);}
            foreach(int i in mp)ms[i]=Keep(new Material(Shader.Find("Sprites/Default")));
            foreach(int i in rp){var go=Keep(new GameObject("GPU3-"+i));go.layer=30;var r=go.AddComponent<SpriteRenderer>();rs[i]=r;r.sprite=ss[i];r.sharedMaterial=ms[i];r.sortingOrder=layers==1?0:(i==1?1:0);r.color=i==0?new Color(1,0,0,.55f):i==1?new Color(0,1,0,.55f):new Color(0,0,1,.55f);}
            var baseline=Draw();int repeat=Diff(baseline,Draw());int[] orders=rs.Select(x=>x.sortingOrder).ToArray();
            // First share while keeping distinct original texture objects.
            var shared=Keep(new Material(ms[0]));foreach(var r in rs)r.sharedMaterial=shared;var pooled=Draw();
            // Then force all renderer sprites to the same texture, preserving full rect and pivot.
            var atlas=White(32,8);for(int i=0;i<3;i++)rs[i].sprite=Make(atlas,new Rect(2+i*8,2,4,4),new Vector2(.5f,.5f),4);
            var packed=Draw();foreach(var i in P[5])rs[i].transform.SetAsLastSibling();var hierarchy=Draw();
            var references=new List<byte[]>();
            foreach(var p in P){for(int i=0;i<3;i++)rs[p[i]].sortingOrder=orders[p[i]]*10+i;references.Add(Draw());}
            for(int i=0;i<3;i++)rs[i].sortingOrder=orders[i];
            string Match(byte[] b){return string.Join("|",P.Where((p,i)=>Diff(b,references[i])==0).Select(p=>string.Join("",p)));}
            int positive=Diff(references[0],references[5]);if(positive==0||Lit(baseline)==0)throw new Exception("sorting positive control failed");

            sortRows.Add(string.Join("\t",layers,textureDirection,string.Join("",mp),string.Join("",rp),Match(baseline),Match(pooled),Match(packed),string.Join(",",ms.Select(x=>x.GetInstanceID())),string.Join(",",rs.Select(x=>x.GetInstanceID())),string.Join(",",ts.Select(x=>x.GetInstanceID())),Diff(baseline,pooled),Diff(baseline,packed),Lit(baseline),rs.All(x=>x.sharedMaterial==shared),Diff(packed,hierarchy),positive,repeat));
            ClearObjects();
        }
        File.WriteAllLines(Path.Combine(Out,"sorting.tsv"),sortRows);
    }
    static Texture2D Atlas(Texture2D source,int padding,bool pot,bool mip)
    {
        int w=source.width+2*padding,h=source.height+2*padding;
        if(pot){w=Mathf.NextPowerOfTwo(w);h=Mathf.NextPowerOfTwo(h);}
        var t=Keep(new Texture2D(w,h,TextureFormat.RGBA32,mip));t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.anisoLevel=1;
        var s=source.GetPixels32();var a=new Color32[w*h];
        // Extrude full RGBA including transparent RGB; fill entire remaining page from clamped edges.
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)a[y*w+x]=s[Mathf.Clamp(y-padding,0,source.height-1)*source.width+Mathf.Clamp(x-padding,0,source.width-1)];
        t.SetPixels32(a);t.Apply(mip,false);return t;
    }
    static void Sampling(bool compressed=false)
    {
        cam.backgroundColor=new Color(.11f,.17f,.23f,1);var empty=Draw();var skipped=new List<string>();
        sampleRows.Add("image\tinput_format\twidth\theight\tmip\tpadding\tpot\trotation\tscale\tpivot\tdifferent_pixels\tmax_byte_error\tcovered_pixels\toriginal_texture_unchanged\tsame_state_repeat_diff");
        var files=Directory.GetFiles(Input,"*.png",SearchOption.AllDirectories).Where(x=>Path.GetFileName(x).StartsWith("nev_travel_")||Path.GetFileName(x).IndexOf("fog",StringComparison.OrdinalIgnoreCase)>=0).OrderBy(x=>x).ToArray();
        foreach(var file in files)foreach(bool mip in (compressed?new[]{false}:new[]{false,true}))
        {
            var source=Keep(new Texture2D(2,2,TextureFormat.RGBA32,mip));if(!ImageConversion.LoadImage(source,File.ReadAllBytes(file),false))throw new Exception("image decode failed "+file);
            if(compressed)
            {
                // Editor-produced DXT, not the game's native downscale/encoder or resident texture.
                if(Mathf.Max(source.width,source.height)>1024)
                {
                    float factor=1024f/Mathf.Max(source.width,source.height);int w=Mathf.Max(1,(int)(source.width*factor)),h=Mathf.Max(1,(int)(source.height*factor));
                    var tmp=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32);var prev=RenderTexture.active;
                    try {Graphics.Blit(source,tmp);RenderTexture.active=tmp;var smaller=Keep(new Texture2D(w,h,TextureFormat.RGBA32,false));smaller.ReadPixels(new Rect(0,0,w,h),0,0,false);smaller.Apply(false,false);UnityEngine.Object.DestroyImmediate(source);objects.Remove(source);source=smaller;}
                    finally {RenderTexture.active=prev;RenderTexture.ReleaseTemporary(tmp);}
                }
                source.Compress(false);source.Apply(false,false);
                if(source.format!=TextureFormat.DXT1 && source.format!=TextureFormat.DXT5){skipped.Add(Path.GetFileName(file)+" returned "+source.format+" size="+source.width+"x"+source.height);ClearObjects();continue;}
            }
            source.wrapMode=TextureWrapMode.Clamp;source.filterMode=FilterMode.Bilinear;source.anisoLevel=1;
            // LoadImage may replace the mip layout. Record the actual value rather than assume.
            source.Apply(mip,false);var originalBytes=source.GetRawTextureData<byte>().ToArray();
            var go=Keep(new GameObject("GPU3 sampling"));go.layer=30;var r=go.AddComponent<SpriteRenderer>();r.sharedMaterial=Keep(new Material(Shader.Find("Sprites/Default")));r.color=new Color(.83f,.64f,.91f,.77f);
            foreach(int padding in new[]{1,4,16})foreach(bool pot in new[]{false,true})
            {
                var atlas=Atlas(source,padding,pot,mip);
                foreach(float angle in new[]{0f,17f,63f})foreach(float scale in new[]{.7f,-1.3f})foreach(var pivot in new[]{new Vector2(.5f,.5f),new Vector2(.23f,.71f)})
                {
                    float ppu=Mathf.Max(source.width,source.height)/1.3f;
                    var a=Make(source,new Rect(0,0,source.width,source.height),pivot,ppu);var b=Make(atlas,new Rect(padding,padding,source.width,source.height),pivot,ppu);
                    go.transform.localRotation=Quaternion.Euler(0,0,angle);go.transform.localScale=new Vector3(scale,Mathf.Abs(scale),1);go.transform.position=new Vector3(.07f,-.03f,0);
                    r.sprite=a;var before=Draw();int repeat=Diff(before,Draw());r.sprite=b;var after=Draw();int error=0;for(int i=0;i<before.Length;i++)error=Math.Max(error,Math.Abs((int)before[i]-after[i]));
                    sampleRows.Add(string.Join("\t",Path.GetFileName(file),source.format,source.width,source.height,source.mipmapCount,padding,pot,angle,scale,pivot,Diff(before,after),error,Diff(before,empty),source.GetRawTextureData<byte>().ToArray().SequenceEqual(originalBytes),repeat));
                    UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);objects.Remove(a);objects.Remove(b);
                }
                UnityEngine.Object.DestroyImmediate(atlas);objects.Remove(atlas);
            }
            ClearObjects();
            File.WriteAllLines(Path.Combine(Out,"sampling.tsv"),sampleRows);
        }
        File.WriteAllLines(Path.Combine(Out,"compression-skips.txt"),skipped);
    }
    static void SerializedSettings()
    {
        // Offline deserialization attempt only. Do not write/apply any game objects or assets.
        try
        {
            string path="D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/globalgamemanagers";
            var items=UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(path);var lines=new List<string>();
            foreach(var item in items){lines.Add(item.GetType().FullName+" "+item.name);var so=new SerializedObject(item);var it=so.GetIterator();while(it.Next(true))if(it.propertyPath.IndexOf("batch",StringComparison.OrdinalIgnoreCase)>=0)lines.Add(it.propertyPath+" type="+it.type+" value="+(it.propertyType==SerializedPropertyType.Boolean?it.boolValue.ToString():it.propertyType==SerializedPropertyType.Integer?it.intValue.ToString():"unread"));}
            File.WriteAllLines(Path.Combine(Out,"serialized-settings.txt"),lines);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Out,"serialized-settings.txt"),"unavailable: "+e);}
    }
}

#endif
