// Original fixture, MIT, StutterFix contributors.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class SpriteDiscardFixture
{
    [Serializable] public sealed class Result
    {
        public string editor,device,error,scope="discard vs own premultiplied formula; game shader equivalence UNCONFIRMED";
        public int conditions,pixels,differences,noiseDifferences,negativeControls;
        public bool passed,bundleBuilt;
    }
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static Color32[] Draw(Material m,Mesh mesh,RenderTexture rt,Texture2D read)
    {
        var old=RenderTexture.active;GL.PushMatrix();
        try{RenderTexture.active=rt;GL.Clear(true,true,new Color(.13f,.19f,.23f,.7f));GL.LoadProjectionMatrix(Matrix4x4.identity);GL.modelview=Matrix4x4.identity;Require(m.SetPass(0),"pass unavailable");Graphics.DrawMeshNow(mesh,Matrix4x4.identity);read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);return read.GetPixels32();}
        finally{GL.PopMatrix();RenderTexture.active=old;}
    }
    static int Difference(Color32[] a,Color32[] b){int n=0;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))n++;return n;}
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion,device=SystemInfo.graphicsDeviceName};int exit=0;
        Material reference=null,candidate=null;Mesh mesh=null;Texture2D texture=null,read=null;RenderTexture rt=null;
        try
        {
            Require(result.editor=="6000.3.10f1"&&SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"unexpected Editor/API");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SpriteDiscard.shader");Require(shader&&shader.isSupported,"shader unavailable");
            foreach(var message in ShaderUtil.GetShaderMessages(shader))Require(message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,message.message);
            reference=new Material(shader);candidate=new Material(shader);candidate.EnableKeyword("SF_SPRITE_DISCARD");
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)};mesh.uv=new[]{new Vector2(-.03125f,-.5f),new Vector2(1.1f,-.5f),new Vector2(1.1f,1.2f),new Vector2(-.03125f,1.2f)};mesh.triangles=new[]{0,1,2,0,2,3};
            mesh.colors=new[]{new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f),new Color(.65f,.73f,.91f,.83f)};
            texture=new Texture2D(13,7,TextureFormat.RGBA32,false,true);var colors=new Color32[91];for(int y=0;y<7;y++)for(int x=0;x<13;x++)colors[y*13+x]=new Color32((byte)(x*19),(byte)(y*37),(byte)(31+x*y*3),(byte)((x+y)%3==0?0:(x+y)%2==0?127:255));texture.SetPixels32(colors);texture.Apply(false,true);
            reference.SetTexture("_MainTex",texture);candidate.SetTexture("_MainTex",texture);
            rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.Create();read=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            for(int vertex=0;vertex<2;vertex++)for(int point=0;point<2;point++)for(int wrap=0;wrap<4;wrap++)foreach(float repeat in new[]{.5f,1f,800f})foreach(float alpha in new[]{0f,.71f,1f})
            {
                texture.filterMode=point!=0?FilterMode.Point:FilterMode.Bilinear;texture.wrapModeU=(wrap&1)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;texture.wrapModeV=(wrap&2)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                foreach(var m in new[]{reference,candidate}){m.SetFloat("RepeatX",repeat);m.SetFloat("RepeatY",repeat*.73f);m.SetColor("_Color",new Color(.87f,.41f,.59f,alpha));if(vertex!=0)m.EnableKeyword("SF_REPEAT_IN_VERTEX");else m.DisableKeyword("SF_REPEAT_IN_VERTEX");}
                var a=Draw(reference,mesh,rt,read);result.noiseDifferences+=Difference(a,Draw(reference,mesh,rt,read));int difference=Difference(a,Draw(candidate,mesh,rt,read));result.conditions++;result.pixels+=a.Length;result.differences+=difference;Require(difference==0,"discard changed pixels at condition "+result.conditions);
                if(point==0&&wrap==3&&repeat==1f&&alpha==.71f){Shader.SetGlobalFloat("_SfSpriteDiscardControl",1);if(Difference(a,Draw(candidate,mesh,rt,read))>0)result.negativeControls++;Shader.SetGlobalFloat("_SfSpriteDiscardControl",0);}
            }
            Require(result.conditions==144&&result.noiseDifferences==0&&result.negativeControls==2,"incomplete discard controls");result.passed=true;
            string destination=Path.GetFullPath(Path.Combine(Application.dataPath,"../sprite-bundle"));Directory.CreateDirectory(destination);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            Require(BuildPipeline.BuildAssetBundles(destination,new[]{new AssetBundleBuild{assetBundleName="stutterfix_sprite_discard",assetNames=new[]{"Assets/Shaders/SpriteDiscard.shader"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64),"bundle build failed");result.bundleBuilt=true;
        }
        catch(Exception e){result.error=e.ToString();exit=1;Debug.LogException(e);}
        finally{Shader.SetGlobalFloat("_SfSpriteDiscardControl",0);if(reference)UnityEngine.Object.DestroyImmediate(reference);if(candidate)UnityEngine.Object.DestroyImmediate(candidate);if(texture)UnityEngine.Object.DestroyImmediate(texture);if(read)UnityEngine.Object.DestroyImmediate(read);if(mesh)UnityEngine.Object.DestroyImmediate(mesh);if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
        File.WriteAllText(Path.Combine(Application.dataPath,"../sprite-discard-result.json"),JsonUtility.ToJson(result,true));EditorApplication.Exit(exit);
    }
}
