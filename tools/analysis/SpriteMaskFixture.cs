// Original conservative empty-cell fixture, MIT, StutterFix contributors.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
public static class SpriteMaskFixture
{
    [Serializable] public sealed class Result {public string editor,device,error,scope="unchanged quad/UV and RGBA image, exact-zero conservative mask; own formula, not game parity or FPS";public int conditions,pixels,differences,noiseDifferences,negativeControls,sparseControls;public bool passed,bundleBuilt;}
    static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion,device=SystemInfo.graphicsDeviceName};int exit=0;
        Material reference=null,candidate=null;Mesh mesh=null;Texture2D source=null,read=null;RenderTexture target=null,mask=null;ComputeBuffer count=null;
        try
        {
            Require(result.editor=="6000.3.10f1"&&SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"unexpected Editor/API");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SpriteChannels.shader");var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/SpriteChannels.compute");
            Require(shader&&shader.isSupported&&compute,"mask shader missing");foreach(var m in ShaderUtil.GetShaderMessages(shader))Require(m.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,m.message);
            foreach(var m in ShaderUtil.GetComputeShaderMessages(compute))Require(m.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,m.message);
            reference=new Material(shader);candidate=new Material(shader);candidate.EnableKeyword("SF_EMPTY_MASK");
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)};mesh.uv=new[]{new Vector2(-.03125f,-.5f),new Vector2(1.1f,-.5f),new Vector2(1.1f,1.2f),new Vector2(-.03125f,1.2f)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.colors=new[]{new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f)};
            source=new Texture2D(65,33,TextureFormat.RGBA32,false,true);mask=new RenderTexture(new RenderTextureDescriptor(8,8){graphicsFormat=GraphicsFormat.R8_UNorm,depthBufferBits=0,msaaSamples=1,enableRandomWrite=true}){filterMode=FilterMode.Point};Require(mask.Create(),"R8 unsupported");
            count=new ComputeBuffer(1,4);int kernel=compute.FindKernel("MaskCells");compute.SetInt("Width",65);compute.SetInt("Height",33);compute.SetInt("Grid",8);compute.SetTexture(kernel,"Source",source);compute.SetTexture(kernel,"Mask",mask);compute.SetBuffer(kernel,"Invalid",count);
            reference.SetTexture("_MainTex",source);candidate.SetTexture("_MainTex",source);candidate.SetTexture("_SfPackedTex",mask);
            target=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();read=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            var colors=new Color32[65*33];
            for(int pattern=0;pattern<3;pattern++)
            {
                for(int y=0;y<33;y++)for(int x=0;x<65;x++){
                    byte gray=(byte)((x*19+y*37)%256);byte alpha=(byte)((x>11&&x<29&&y>9&&y<20)||(x==0&&y<4)||(x==64&&y>28)?(x+y)%2==0?127:255:0);
                    colors[y*65+x]=new Color32(pattern==0?(byte)255:gray,pattern==2?(byte)(gray/2):pattern==0?(byte)255:gray,pattern==2?(byte)(255-gray):pattern==0?(byte)255:gray,alpha);
                }
                source.SetPixels32(colors);source.Apply(false,false);
                for(int wrap=0;wrap<4;wrap++)
                {
                    source.wrapModeU=mask.wrapModeU=(wrap&1)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;source.wrapModeV=mask.wrapModeV=(wrap&2)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                    compute.SetInt("WrapU",(wrap&1)!=0?1:0);compute.SetInt("WrapV",(wrap&2)!=0?1:0);count.SetData(new uint[]{0});compute.Dispatch(kernel,1,1,1);var occupied=new uint[1];count.GetData(occupied);Require(occupied[0]>0&&occupied[0]<64,"mask is not sparse");result.sparseControls++;
                    for(int point=0;point<2;point++)foreach(float repeat in new[]{.5f,1f,7.7f,800f,-800f,100000f})foreach(float alpha in new[]{0f,.71f,1f})
                    {
                        source.filterMode=point==0?FilterMode.Bilinear:FilterMode.Point;
                        foreach(var m in new[]{reference,candidate}){m.SetFloat("RepeatX",repeat);m.SetFloat("RepeatY",repeat*.73f);m.SetColor("_Color",new Color(.87f,.41f,.59f,alpha));}
                        var a=SpriteChannelFixture.Draw(reference,mesh,target,read);result.noiseDifferences+=SpriteChannelFixture.Difference(a,SpriteChannelFixture.Draw(reference,mesh,target,read));int delta=SpriteChannelFixture.Difference(a,SpriteChannelFixture.Draw(candidate,mesh,target,read));result.conditions++;result.pixels+=a.Length;result.differences+=delta;Require(delta==0,"mask pixel mismatch condition "+result.conditions);
                        if(point==0&&wrap==3&&repeat==1f&&alpha==.71f){candidate.EnableKeyword("SF_CHANNEL_CONTROL");if(SpriteChannelFixture.Difference(a,SpriteChannelFixture.Draw(candidate,mesh,target,read))>0)result.negativeControls++;candidate.DisableKeyword("SF_CHANNEL_CONTROL");}
                    }
                }
            }
            Require(result.conditions==432&&result.noiseDifferences==0&&result.negativeControls==3&&result.sparseControls==12,"incomplete mask controls");result.passed=true;
            string destination=Path.GetFullPath(Path.Combine(Application.dataPath,"../mask-bundle"));Directory.CreateDirectory(destination);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            Require(BuildPipeline.BuildAssetBundles(destination,new[]{new AssetBundleBuild{assetBundleName="stutterfix_sprite_mask",assetNames=new[]{"Assets/Shaders/SpriteChannels.shader","Assets/Shaders/SpriteChannels.compute"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64),"mask bundle failed");result.bundleBuilt=true;
        }
        catch(Exception e){result.error=e.ToString();exit=1;Debug.LogException(e);}
        finally{count?.Dispose();if(reference)UnityEngine.Object.DestroyImmediate(reference);if(candidate)UnityEngine.Object.DestroyImmediate(candidate);if(source)UnityEngine.Object.DestroyImmediate(source);if(read)UnityEngine.Object.DestroyImmediate(read);if(mesh)UnityEngine.Object.DestroyImmediate(mesh);foreach(var rt in new[]{target,mask})if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
        File.WriteAllText(Path.Combine(Application.dataPath,"../sprite-mask-result.json"),JsonUtility.ToJson(result,true));EditorApplication.Exit(exit);
    }
}
