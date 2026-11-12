// Original bounded GPU fixture, MIT, StutterFix contributors.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
public static class SpriteChannelFixture
{
    [Serializable] public sealed class Result {public string editor,device,error,scope="RGBA8 linear grayscale plus alpha to RG8, original resolution; own formula, not game parity or performance";public int conditions,pixels,differences,noiseDifferences,negativeControls,nongrayControls;public bool passed,bundleBuilt;}
    static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    internal static Color32[] Draw(Material m,Mesh mesh,RenderTexture rt,Texture2D read)
    {
        var previous=RenderTexture.active;GL.PushMatrix();
        try{RenderTexture.active=rt;GL.Clear(true,true,new Color(.13f,.19f,.23f,.7f));GL.LoadProjectionMatrix(Matrix4x4.identity);GL.modelview=Matrix4x4.identity;Require(m.SetPass(0),"pass missing");Graphics.DrawMeshNow(mesh,Matrix4x4.identity);read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);return read.GetPixels32();}
        finally{GL.PopMatrix();RenderTexture.active=previous;}
    }
    internal static int Difference(Color32[] a,Color32[] b){int n=0;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))n++;return n;}
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion,device=SystemInfo.graphicsDeviceName};int exit=0;
        Material reference=null,candidate=null;Mesh mesh=null;Texture2D source=null,read=null;RenderTexture target=null,packed=null;ComputeBuffer invalid=null;
        try
        {
            Require(result.editor=="6000.3.10f1"&&SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"unexpected Editor/API");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SpriteChannels.shader");var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/SpriteChannels.compute");
            Require(shader&&shader.isSupported&&compute,"channel shader missing");foreach(var m in ShaderUtil.GetShaderMessages(shader))Require(m.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,m.message);
            foreach(var m in ShaderUtil.GetComputeShaderMessages(compute))Require(m.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,m.message);
            reference=new Material(shader);candidate=new Material(shader);candidate.EnableKeyword("SF_PACKED_GRAY");
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)};mesh.uv=new[]{new Vector2(-.03125f,-.5f),new Vector2(1.1f,-.5f),new Vector2(1.1f,1.2f),new Vector2(-.03125f,1.2f)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.colors=new[]{new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f)};
            source=new Texture2D(13,7,TextureFormat.RGBA32,false,true);packed=new RenderTexture(new RenderTextureDescriptor(13,7){graphicsFormat=GraphicsFormat.R8G8_UNorm,depthBufferBits=0,msaaSamples=1,enableRandomWrite=true});Require(packed.Create(),"RG8 unsupported");
            invalid=new ComputeBuffer(1,4);int kernel=compute.FindKernel("Pack");compute.SetInt("Width",13);compute.SetInt("Height",7);compute.SetTexture(kernel,"Source",source);compute.SetTexture(kernel,"Packed",packed);compute.SetBuffer(kernel,"Invalid",invalid);
            reference.SetTexture("_MainTex",source);candidate.SetTexture("_SfPackedTex",packed);
            target=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();read=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            var colors=new Color32[91];
            for(int pattern=0;pattern<2;pattern++)
            {
                for(int y=0;y<7;y++)for(int x=0;x<13;x++){byte gray=pattern==0?(byte)255:(byte)((x*19+y*37)%256);colors[y*13+x]=new Color32(gray,gray,gray,(byte)((x+y)%3==0?0:(x+y)%2==0?127:255));}
                source.SetPixels32(colors);source.Apply(false,false);invalid.SetData(new uint[]{0});compute.Dispatch(kernel,2,1,1);var validation=new uint[1];invalid.GetData(validation);Require(validation[0]==0,"gray rejected");
                for(int point=0;point<2;point++)for(int wrap=0;wrap<4;wrap++)foreach(float repeat in new[]{.5f,1f,7.7f,800f})foreach(float alpha in new[]{0f,.71f,1f})
                {
                    source.filterMode=packed.filterMode=point==0?FilterMode.Bilinear:FilterMode.Point;source.wrapModeU=packed.wrapModeU=(wrap&1)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;source.wrapModeV=packed.wrapModeV=(wrap&2)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                    foreach(var m in new[]{reference,candidate}){m.SetFloat("RepeatX",repeat);m.SetFloat("RepeatY",repeat*.73f);m.SetColor("_Color",new Color(.87f,.41f,.59f,alpha));}
                    var a=Draw(reference,mesh,target,read);result.noiseDifferences+=Difference(a,Draw(reference,mesh,target,read));int delta=Difference(a,Draw(candidate,mesh,target,read));result.conditions++;result.pixels+=a.Length;result.differences+=delta;Require(delta==0,"channel pixel mismatch condition "+result.conditions);
                    if(point==0&&wrap==3&&repeat==1f&&alpha==.71f){candidate.EnableKeyword("SF_CHANNEL_CONTROL");if(Difference(a,Draw(candidate,mesh,target,read))>0)result.negativeControls++;candidate.DisableKeyword("SF_CHANNEL_CONTROL");}
                }
            }
            colors[0]=new Color32(255,0,0,0);source.SetPixels32(colors);source.Apply(false,false);invalid.SetData(new uint[]{0});compute.Dispatch(kernel,2,1,1);var bad=new uint[1];invalid.GetData(bad);Require(bad[0]!=0,"hidden nongray RGB accepted");result.nongrayControls++;
            Require(result.conditions==192&&result.noiseDifferences==0&&result.negativeControls==2&&result.nongrayControls==1,"incomplete controls");result.passed=true;
            string destination=Path.GetFullPath(Path.Combine(Application.dataPath,"../channel-bundle"));Directory.CreateDirectory(destination);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            Require(BuildPipeline.BuildAssetBundles(destination,new[]{new AssetBundleBuild{assetBundleName="stutterfix_sprite_channels",assetNames=new[]{"Assets/Shaders/SpriteChannels.shader","Assets/Shaders/SpriteChannels.compute"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64),"bundle build failed");result.bundleBuilt=true;
        }
        catch(Exception e){result.error=e.ToString();exit=1;Debug.LogException(e);}
        finally{invalid?.Dispose();if(reference)UnityEngine.Object.DestroyImmediate(reference);if(candidate)UnityEngine.Object.DestroyImmediate(candidate);if(source)UnityEngine.Object.DestroyImmediate(source);if(read)UnityEngine.Object.DestroyImmediate(read);if(mesh)UnityEngine.Object.DestroyImmediate(mesh);foreach(var rt in new[]{target,packed})if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
        File.WriteAllText(Path.Combine(Application.dataPath,"../sprite-channel-result.json"),JsonUtility.ToJson(result,true));EditorApplication.Exit(exit);
    }
}
