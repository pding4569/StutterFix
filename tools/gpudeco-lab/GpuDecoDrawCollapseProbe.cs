#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using StutterFix;

public static partial class GpuDecoLab
{
    // Dedicated build/output: never overwrites the previous baseline bundle.
    public static void CollapseBundle()=>Run(()=>
    {
        string output=Path.Combine(Evidence,"drawcollapse4/bundle");Directory.CreateDirectory(output);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{
            assetBundleName="stutterfix_gpudeco",assetNames=new[]{"Assets/Shaders/GpuDeco.compute","Assets/Shaders/GpuDecoBatch.shader"}}},
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        Require(manifest,"drawcollapse bundle build failed");
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"stutterfix_gpudeco"));Require(bundle,"drawcollapse bundle reload failed");
        try
        {
            var compute=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/GpuDecoBatch.shader");
            Require(compute && compute.HasKernel("EvaluateCollapse") && shader && shader.isSupported,"drawcollapse bundle assets/kernel unsupported");
            CollapseDirectProbe(compute);CollapseTextureProbe(shader);
        }
        finally {bundle.Unload(true);}
        Log("DRAWCOLLAPSE_LAB=PASS scope=synthetic_direct_vertices_and_array_pixels game_FPS=NA game_pixels=NA path="+output);
    },"drawcollapse4-bundle");
    static void CollapseDirectProbe(ComputeShader shader)
    {
        var data=Synthetic(65);data.Items[3].Initial[10]=0;
        using(var gpu=new GpuDecoGpu(shader,data))
        using(var map=new ComputeBuffer(65,4))
        using(var texture=new ComputeBuffer(130,16))
        {
            gpu.UseLiveMode=true;gpu.SetLiveUniforms(new Vector4(data.Camera[0],data.Camera[1],data.Camera[2],data.Camera[3]),data.BakeTime);
            gpu.DispatchEvaluate();var reference=new GpuDecoGpu.Vertex[65*4];gpu.VertexBuffer.GetData(reference);
            var order=Enumerable.Range(0,65).Reverse().Select(i=>(uint)i).ToArray();map.SetData(order);
            var meta=Enumerable.Range(0,130).Select(i=>i%2==0?new Vector4(i%70+(i%3==0?128:0),i%3,0,0):new Vector4(.5f,.25f,.01f,.02f)).ToArray();texture.SetData(meta);
            var mesh=new Mesh{vertexBufferTarget=GraphicsBuffer.Target.Raw};
            try
            {
                mesh.SetVertexBufferParams(65*4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4));
                mesh.SetVertexBufferData(new float[65*60],0,0,65*60);mesh.UploadMeshData(true);
                using(var target=mesh.GetVertexBuffer(0))
                {
                    gpu.DispatchCollapse(target,map,texture,65);var actual=new float[65*60];target.GetData(actual);
                    for(int slot=0;slot<65;slot++)for(int v=0;v<4;v++)
                    {
                        int src=(int)order[slot],p=(slot*4+v)*15;var a=reference[src*4+v];
                        var expected=a.color.a<=0?Vector3.zero:a.position;
                        Require(actual[p]==expected.x && actual[p+1]==expected.y && actual[p+2]==expected.z,"direct mapped position");
                        var color=GpuDecoGpu.DrawColor(a.color);
                        for(int c=0;c<4;c++)Require(Mathf.Abs(actual[p+3+c]-color[c])<1e-7f,"direct draw color");
                        Require(actual[p+7]==a.uv.x && actual[p+8]==a.uv.y && actual[p+9]==meta[src*2].x && actual[p+10]==meta[src*2].y,"direct texture address");
                        for(int c=0;c<4;c++)Require(actual[p+11+c]==meta[src*2+1][c],"direct padding region");
                    }
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(mesh);}
        }
        Log("DRAWCOLLAPSE_DIRECT vertices=260 reversed_map=True tail_threads=True alpha_zero_degenerate=True errors=0 copy_dispatches=0");
    }
    static void CollapseTextureProbe(Shader shader)
    {
        var objects=new List<GameObject>();var assets=new List<UnityEngine.Object>();
        var previous=RenderTexture.active;bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            var go=new GameObject("collapse fixture camera");objects.Add(go);var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;camera.transform.position=new Vector3(0,0,-10);
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.2f,.3f,1);
            var rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32);assets.Add(rt);camera.targetTexture=rt;
            var image=new Texture2D(64,64,TextureFormat.RGBA32,false);assets.Add(image);
            Func<Color32[]> capture=()=>{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();return image.GetPixels32();};
            var material=new Material(shader);assets.Add(material);
            var referenceMaterial=new Material(UnityEngine.Shader.Find("Sprites/Default"));assets.Add(referenceMaterial);
            var quad=new Mesh();assets.Add(quad);
            quad.vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(-1,1),new Vector3(1,1)};
            quad.triangles=new[]{0,2,1,2,3,1};quad.colors=Enumerable.Repeat(Color.white,4).ToArray();
            var target=new GameObject("collapse fixture quad"){layer=30};objects.Add(target);
            target.AddComponent<MeshFilter>().sharedMesh=quad;var renderer=target.AddComponent<MeshRenderer>();
            int cases=0;
            foreach(bool compressed in new[]{false,true})foreach(int size in new[]{8,16})
            {
                var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);assets.Add(tex);
                tex.SetPixels(Enumerable.Range(0,size*size).Select(i=>new Color((i%size)/(float)size,.3f,(i/size)/(float)size,.2f+(i%3)*.2f)).ToArray());
                tex.Apply();if(compressed)tex.Compress(false);tex.wrapMode=TextureWrapMode.Clamp;
                var array=new Texture2DArray(size*2,size*2,2,tex.graphicsFormat,UnityEngine.Experimental.Rendering.TextureCreationFlags.None,1);assets.Add(array);
                Graphics.CopyTexture(tex,0,0,0,0,size,size,array,1,0,0,0);
                for(int a=0;a<6;a++)material.SetTexture("_Tex"+a,array);
                for(int a=0;a<64;a++)material.SetTexture("_Big"+a,tex);
                quad.SetUVs(1,Enumerable.Repeat(new Vector4(.5f,.5f,.25f/size,.25f/size),4).ToList());
                foreach(bool point in new[]{false,true})foreach(int tier in new[]{0,5,6,31,69})
                {
                    tex.filterMode=point?FilterMode.Point:FilterMode.Bilinear;
                    quad.SetUVs(0,new List<Vector4>{new Vector4(0,0,tier+(point?128:0),1),new Vector4(1,0,tier+(point?128:0),1),
                        new Vector4(0,1,tier+(point?128:0),1),new Vector4(1,1,tier+(point?128:0),1)});
                    renderer.sharedMaterial=referenceMaterial;referenceMaterial.mainTexture=tex;var original=capture();
                    renderer.sharedMaterial=material;var actual=capture();
                    PixelDiff(original,actual,"collapse_array_"+compressed+"_"+size+"_"+point+"_"+tier,true);cases++;
                }
            }
            Log("DRAWCOLLAPSE_TEXTURE cases="+cases+" compressed_and_rgba=True bindings=0,5,6,31,69 padded_rect=True point_and_bilinear=True errors=0 shader_oracle=Sprites/Default game_Sprites/Tile=UNVERIFIED");
        }
        finally
        {
            RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=async;
            foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);
            for(int i=assets.Count-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(assets[i]);
        }
    }
}
#endif
