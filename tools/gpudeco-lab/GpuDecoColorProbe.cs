#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StutterFix;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex = StutterFix.GpuDecoGpu.Vertex;

public static partial class GpuDecoLab
{
    public static void Colors() => Run(ColorBody,"colors");
    static void ColorBody() => ColorBody(Shader());
    static void ColorBody(ComputeShader shader)
    {
        var objects=new List<UnityEngine.Object>();
        var draws=new List<GameObject>();
        var previous=RenderTexture.active;
        bool async=UnityEditor.ShaderUtil.allowAsyncCompilation;
        UnityEditor.ShaderUtil.allowAsyncCompilation=false;
        try {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);objects.Add(texture);
            texture.SetPixels(Enumerable.Repeat(Color.white,4).ToArray());texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),2,0,SpriteMeshType.FullRect);objects.Add(sprite);
            var material=new Material(UnityEngine.Shader.Find("Sprites/Default"));objects.Add(material);material.mainTexture=texture;
            var cameraObject=new GameObject("color probe camera");objects.Add(cameraObject);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
            var rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32);objects.Add(rt);camera.targetTexture=rt;
            var image=new Texture2D(64,64,TextureFormat.RGBA32,false);objects.Add(image);
            Func<Color32[]> capture=()=>{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();return image.GetPixels32();};
            Action clear=()=>{foreach(var go in draws)UnityEngine.Object.DestroyImmediate(go);draws.Clear();};
            var colors=new[]{new Color(.31719f,.61739f,.91357f,.003909087f),new Color(.73139f,.11957f,.51137f,.0348075181f),new Color(.45f,.15f,.75f,.15f),
                new Color(-.3f,1.3f,.5f,1.1f),new Color(.5f/255,1.5f/255,2.5f/255,1),new Color(127.5f/255,128.5f/255,129.5f/255,.5f)};
            int negative=0,checks=0;
            int copy=shader.FindKernel("CopyRun");
            foreach(var color in colors) foreach(int count in new[]{1,32,128}) {
                for(int n=0;n<count;n++) {
                    var go=new GameObject("native sprite");draws.Add(go);go.layer=30;
                    var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;r.color=color;r.sortingOrder=n;
                }
                var expected=capture();clear();
                foreach(int mode in new[]{0,1,2,3,4}) {
                    var points=new Vertex[4];
                    for(int k=0;k<4;k++)points[k]=new Vertex {position=new Vector3(k%2==0?-.5f:.5f,k<2?-.5f:.5f,0),uv=new Vector2(k%2,k/2),color=mode==2?Color.white:mode==1?(Color)(Color32)color:mode==4?GpuDecoGpu.DrawColor(color):color};
                    var mesh=RawRun(0,1,points,1);objects.Add(mesh);
                    if(mode==3) using(var source=new GraphicsBuffer(GraphicsBuffer.Target.Raw,36,4)) using(var target=mesh.GetVertexBuffer(0)) {
                        source.SetData(points);shader.SetBuffer(copy,"_CopySource",source);shader.SetBuffer(copy,"_Vertices",target);
                        shader.SetInt("_CopyFirstVertex",0);shader.SetInt("_CopyVertexCount",4);shader.SetInt("_CopyQuantizeColor",1);shader.Dispatch(copy,1,1,1);
                        var read=new Vertex[4];target.GetData(read);
                        for(int k=0;k<4;k++) {Require(read[k].position.Equals(points[k].position) && read[k].uv.Equals(points[k].uv),"color copy changed geometry");
                            Color expectedColor=GpuDecoGpu.DrawColor(color);for(int c=0;c<4;c++)Require(DecoBakeCore.Finite(read[k].color[c]) && Mathf.Abs(read[k].color[c]-expectedColor[c])<=1e-7f,"GPU normalized color mismatch");}
                    }
                    for(int n=0;n<count;n++) {
                        var go=new GameObject("color mesh");draws.Add(go);go.layer=30;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=n;
                        var block=new MaterialPropertyBlock();block.SetColor("_RendererColor",mode==2?color:Color.white);block.SetVector("_Flip",Vector4.one);r.SetPropertyBlock(block);
                    }
                    var actual=capture();int changed=0,max=0;
                    for(int i=0;i<actual.Length;i++) {var a=actual[i];var b=expected[i];int d=Math.Max(Math.Max(Math.Abs(a.r-b.r),Math.Abs(a.g-b.g)),Math.Max(Math.Abs(a.b-b.b),Math.Abs(a.a-b.a)));if(d>0)changed++;max=Math.Max(max,d);}
                    Log("[gpudeco-lab] color_probe mode="+mode+" count="+count+" input="+color.ToString("G9")+" changed="+changed+" max="+max+" native="+expected[2080]+" mesh="+actual[2080]);
                    if(mode==0 && changed>0)negative++;
                    if(mode==4 || mode==3){checks++;Require(changed==0,"quantized draw differs from SpriteRenderer");}
                    clear();
                }
            }
            Require(negative>0,"float color negative control did not detect mismatch");
            // Every 8-bit midpoint, with nearby floats on both sides;
            // alpha is also exercised against a black background. Unlike a
            // managed cast oracle this observes the native renderer itself.
            int gridChecks=0;
            foreach(bool alpha in new[]{false,true}) foreach(float offset in new[]{-1e-7f,0f,1e-7f}) {
                var points=new Vertex[1024];
                for(int i=0;i<256;i++) {
                    float f=Mathf.Clamp01((i+.5f)/255f+offset);
                    var color=alpha?new Color(.73f,.37f,.91f,f):new Color(f,f,f,1);
                    var center=new Vector3((i%16-7.5f)*.125f,(i/16-7.5f)*.125f,0);
                    var go=new GameObject("native midpoint");draws.Add(go);go.layer=30;go.transform.position=center;go.transform.localScale=Vector3.one*.09375f;
                    var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;
                    r.color=color;r.sortingOrder=i;
                    for(int k=0;k<4;k++)points[i*4+k]=new Vertex {position=center+new Vector3(k%2==0?-.046875f:.046875f,k<2?-.046875f:.046875f,0),uv=new Vector2(k%2,k/2),color=color};
                }
                var expected=capture();clear();
                var mesh=RawRun(0,256,null,1);objects.Add(mesh);
                using(var source=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1024*9,4)) using(var target=mesh.GetVertexBuffer(0)) {
                    source.SetData(points);shader.SetBuffer(copy,"_CopySource",source);shader.SetBuffer(copy,"_Vertices",target);
                    shader.SetInt("_CopyFirstVertex",0);shader.SetInt("_CopyVertexCount",1024);shader.SetInt("_CopyQuantizeColor",1);shader.Dispatch(copy,16,1,1);
                }
                var combined=new GameObject("GPU midpoint mesh");draws.Add(combined);combined.layer=30;combined.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=combined.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                var props=new MaterialPropertyBlock();props.SetColor("_RendererColor",Color.white);props.SetVector("_Flip",Vector4.one);renderer.SetPropertyBlock(props);
                PixelDiff(expected,capture(),"native_color_midpoints_alpha_"+alpha+"_offset_"+N(offset),true);gridChecks++;clear();
            }
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-gpudecoInput");
            if(at>=0){Require(at+1<args.Length,"missing color snapshot directory");ColorSnapshots(shader,args[at+1]);}
            Log("GPUDECO_LAB_COLOR=PASS equal_checks="+checks+" midpoint_grids="+gridChecks+" float_negative_cases="+negative+" shader=Sprites/Default game_G1=UNMEASURED");
        }
        finally {RenderTexture.active=previous;foreach(var go in draws)UnityEngine.Object.DestroyImmediate(go);for(int n=objects.Count-1;n>=0;n--)UnityEngine.Object.DestroyImmediate(objects[n]);UnityEditor.ShaderUtil.allowAsyncCompilation=async;}
    }
    static void ColorSnapshots(ComputeShader shader,string directory)
    {
        foreach(var file in Directory.GetFiles(directory,"*.gpudeco").OrderBy(x=>x)) {
            GpuDecoData data;using(var input=File.OpenRead(file))data=GpuDecoData.Read(input);
            data.Items=data.Items.Where(d=>d.Tracks.All(t=>t.All(s=>Supported(s.Ease))) && d.Anchors.All(a=>!a.Valid || Supported(a.Segment.Ease)) && data.MotionWithoutClock(d)<0).ToArray();
            using(var gpu=new GpuDecoGpu(shader,data)) {
                var evaluated=gpu.Dispatch(data.BakeTime,true);var mesh=RawRun(0,data.Items.Length,null,1);
                try {using(var target=mesh.GetVertexBuffer(0)) {
                    int copy=shader.FindKernel("CopyRun");shader.SetBuffer(copy,"_CopySource",gpu.VertexBuffer);shader.SetBuffer(copy,"_Vertices",target);
                    shader.SetInt("_CopyFirstVertex",0);shader.SetInt("_CopyVertexCount",mesh.vertexCount);shader.SetInt("_CopyQuantizeColor",1);shader.Dispatch(copy,(mesh.vertexCount+63)/64,1,1);
                    var read=new Vertex[mesh.vertexCount];target.GetData(read);int over=0;double max=0;
                    for(int i=0;i<data.Items.Length;i++)for(int c=0;c<4;c++) {
                        var raw=data.Items[i].Color;Color expected=GpuDecoGpu.DrawColor(new Color(raw[0],raw[1],raw[2],raw[3]));
                        for(int k=0;k<4;k++){double error=Math.Abs(read[i*4+k].color[c]-expected[c]);max=Math.Max(max,error);if(error>1e-7)over++;
                            Require(DecoBakeCore.Finite(error),"nonfinite draw color");
                            Require(read[i*4+k].position.Equals(evaluated.vertices[i*4+k].position),"draw conversion changed logical geometry");}
                    }
                    Log("[gpudeco-lab] real_draw_color export="+Path.GetFileName(file)+" owned="+data.Items.Length+" components="+(mesh.vertexCount*4)+" max_normalized_error="+N(max)+" byte_mismatches="+over);
                    Require(over==0,"real draw color differs from native quantization");
                }} finally {UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
#endif
