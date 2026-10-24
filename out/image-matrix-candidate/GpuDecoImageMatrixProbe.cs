#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using StutterFix;

public static partial class GpuDecoLab
{
    public static void ImageMatrixBundle()=>Run(()=>
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-imageBundleOut");
        Require(at>=0&&at+1<args.Length,"new image bundle output required");string output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(output);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="stutterfix_gpudeco",assetNames=new[]{"Assets/Shaders/GpuDeco.compute"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        Require(manifest,"image bundle build failed");
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"stutterfix_gpudeco"));Require(bundle,"image bundle reload failed");
        try
        {
            var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");Require(shader&&shader.HasKernel("EvaluateImageLive"),"image matrix kernel missing");
            ProbeLinear(shader);ProbeRotation(shader);ImageMatrixBody(shader);
        }
        finally{bundle.Unload(true);}
    },"image-matrix");
    static void ImageMatrixBody(ComputeShader shader)
    {
        var owned=new List<UnityEngine.Object>();var previous=RenderTexture.active;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        int cases=0,badCases=0,maxRGBA=0;long changedPixels=0;float maxWorld=0;
        try
        {
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(texture);
            var pixels=new Color32[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=new Color32((byte)(x*73+y*29),(byte)(x*31+y*47),(byte)(x*13+y*61),255);
            texture.SetPixels32(pixels);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),128,0,SpriteMeshType.FullRect);owned.Add(sprite);
            var material=new Material(UnityEngine.Shader.Find("Sprites/Default")){mainTexture=texture};owned.Add(material);
            var cameraObject=new GameObject("image matrix camera");owned.Add(cameraObject);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
            var rt=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32);owned.Add(rt);camera.targetTexture=rt;
            var image=new Texture2D(512,512,TextureFormat.RGBA32,false);owned.Add(image);
            Color32[] Capture(){camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();return image.GetPixels32();}
            var parent=new GameObject("image matrix parent");owned.Add(parent);
            var source=new GameObject("image matrix sprite"){layer=30};owned.Add(source);source.transform.SetParent(parent.transform,false);
            var sr=source.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=material;sr.color=Color.white;
            var replacement=new GameObject("image matrix GPU"){layer=30};owned.Add(replacement);var mr=replacement.AddComponent<MeshRenderer>();mr.sharedMaterial=material;
            mr.shadowCastingMode=ShadowCastingMode.Off;mr.lightProbeUsage=LightProbeUsage.Off;mr.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var filter=replacement.AddComponent<MeshFilter>();var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",texture);props.SetColor("_RendererColor",Color.white);props.SetVector("_Flip",Vector4.one);mr.SetPropertyBlock(props);
            var corners=sprite.vertices;var uv=sprite.uv;var offset=new Vector3(.173f,-.213f,0);var childScale=new Vector3(.73f,.91f,1);var childRotation=Quaternion.Euler(0,0,7.125f);
            var local=Matrix4x4.TRS(Vector3.zero,childRotation,childScale);
            foreach(float origin in new[]{0f,624f,8192f,92842.6171875f})
            foreach(float angle in new[]{-90.17515f,18.8939457f,34.07774f,2880f})
            foreach(float cameraAngle in new[]{0f,17.125f})
            {
                cases++;parent.transform.SetPositionAndRotation(new Vector3(origin+.137f,origin-.213f,0),Quaternion.Euler(0,0,angle));parent.transform.localScale=new Vector3(1.31f,.73f,1);
                source.transform.SetLocalPositionAndRotation(offset,childRotation);source.transform.localScale=childScale;
                camera.transform.SetPositionAndRotation(new Vector3(origin,origin,-10),Quaternion.Euler(0,0,cameraAngle));
                var item=new GpuDecoData.Item{Flags=64,Constants=new[]{1f,1f,1f,1f,0f,0f,0f,0f},Triangles=sprite.triangles.Select(x=>(int)x).ToArray()};
                item.Initial[0]=parent.transform.position.x;item.Initial[1]=parent.transform.position.y;item.Initial[2]=offset.x;item.Initial[3]=offset.y;item.Initial[4]=angle;
                item.Initial[5]=1.31f;item.Initial[6]=.73f;for(int i=7;i<=11;i++)item.Initial[i]=1;
                item.OffsetBasis[3]=item.OffsetBasis[7]=1;
                for(int i=0;i<16;i++)item.Tracks[i]=Array.Empty<DecoBakeCore.Segment>();
                for(int i=0;i<4;i++){var shape=local.MultiplyPoint3x4(corners[i]);item.Shape[i*3]=shape.x;item.Shape[i*3+1]=shape.y;item.UV[i*2]=uv[i].x;item.UV[i*2+1]=uv[i].y;}
                var data=new GpuDecoData{Items=new[]{item},Pitch=1,TweenScale=1,Camera=new[]{0f,0f,0f,1f}};
                using(var gpu=new GpuDecoGpu(shader,data){UseAnchors=false})
                {
                    gpu.UpdateImageMatrix(0,local,corners,false,false);gpu.Dispatch(0,false);filter.sharedMesh=gpu.Mesh;
                    sr.enabled=true;mr.enabled=false;var a=Capture();sr.enabled=false;mr.enabled=true;var b=Capture();var repeat=Capture();sr.enabled=true;mr.enabled=false;var restored=Capture();
                    int count=0,maximum=0,lit=0;
                    for(int i=0;i<a.Length;i++)
                    {
                        Require(a[i].Equals(restored[i])&&b[i].Equals(repeat[i]),"image matrix repeat noise");
                        if(a[i].r!=0||a[i].g!=0||a[i].b!=0)lit++;
                        if(!a[i].Equals(b[i])){count++;for(int c=0;c<4;c++)maximum=Math.Max(maximum,Math.Abs(a[i][c]-b[i][c]));}
                    }
                    Require(lit>1000,"image matrix blank reference");
                    var vertices=gpu.ReadImageVertices();float error=0;for(int i=0;i<4;i++)error=Mathf.Max(error,Vector3.Distance(vertices[i].position,source.transform.TransformPoint(corners[i])));
                    if(count>0)badCases++;changedPixels+=count;maxRGBA=Math.Max(maxRGBA,maximum);maxWorld=Mathf.Max(maxWorld,error);
                    Log("[image-matrix] origin="+N(origin)+" angle="+N(angle)+" camera="+N(cameraAngle)+" different="+count+" max_RGBA="+maximum+" world_error="+N(error)+" repeat=0");
                }
            }
            Log("GPUDECO_IMAGE_MATRIX cases="+cases+" different_cases="+badCases+" different_pixels="+changedPixels+" max_RGBA="+maxRGBA+" max_world_error="+N(maxWorld)+" game_pixels=UNMEASURED");
            Require(badCases==0,"image matrix pixel fixture failed");
        }
        finally
        {
            RenderTexture.active=previous;for(int i=owned.Count-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(owned[i]);ShaderUtil.allowAsyncCompilation=async;
        }
    }
}
#endif
