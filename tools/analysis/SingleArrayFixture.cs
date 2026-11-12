// Original test code, MIT (repository LICENSE). Isolated Editor fixture only.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SingleArrayFixture
{
    [Serializable] public sealed class Result
    {
        public string editor, device, api, error, scope="isolated_synthetic_pixels_not_game_or_performance";
        public int conditions, directConditions, pixels, differences, genericDifferences, noiseDifferences, negativeControls, negativeControlsDetected, leanConditions;
        public bool bundleBuilt;
        public int textureQueries, textureQueryUnavailable, textureQueryMismatches;
    }
    static uint Bits(float f)=>BitConverter.ToUInt32(BitConverter.GetBytes(f),0);
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion,device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString()};
        int exit=0;
        try
        {
            Require(result.editor=="6000.3.10f1","unexpected Editor");
            Require(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"D3D11 required");
            Check(result);Build();result.bundleBuilt=true;
        }
        catch(Exception e){result.error=e.ToString();exit=1;Debug.LogException(e);}
        File.WriteAllText(Path.Combine(Root,"result.json"),JsonUtility.ToJson(result,true));
        Debug.Log("SINGLE_ARRAY_FIXTURE "+JsonUtility.ToJson(result));
        EditorApplication.Exit(exit);
    }
    static Color32[] Pattern(int bank,int layer)
    {
        var p=new Color32[256];
        for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            p[y*16+x]=new Color32((byte)(17+bank*43+x*3),(byte)(11+layer*61+y*4),(byte)(31+x*5+y*3),
                (byte)((x+y)%5==0?0:(x*y+layer)%3==0?127:255));
        return p;
    }
    static Texture2DArray Array(int bank)
    {
        var a=new Texture2DArray(16,16,3,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        for(int layer=0;layer<3;layer++)
        {
            a.SetPixels32(Pattern(bank,layer),layer);
        }
        a.Apply(false,true);return a;
    }
    static void Input(uint[] data,int flags,int layer,bool padded)
    {
        for(int i=0;i<4;i++)
        {
            bool right=i==1||i==2,top=i>=2;int at=i*15;
            float[] values={right?1:-1,top?1:-1,0, .65f,.47f,.83f,.71f,
                right?1.27f:-.31f,top?1.19f:-.23f,flags,layer,
                padded?.75f:1,padded?.625f:1,.5f/16,.5f/16};
            for(int k=0;k<15;k++)data[at+k]=Bits(values[k]);
        }
    }
    static Color32[] Draw(Material material,Mesh mesh,RenderTexture target,Texture2D read)
    {
        var before=RenderTexture.active;
        GL.PushMatrix();
        try
        {
            RenderTexture.active=target;GL.Clear(true,true,new Color(.13f,.19f,.23f,.7f));
            GL.LoadProjectionMatrix(Matrix4x4.identity);GL.modelview=Matrix4x4.identity;
            Require(material.SetPass(0),"material pass unavailable");Graphics.DrawMeshNow(mesh,Matrix4x4.identity);
            read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);return read.GetPixels32();
        }
        finally{GL.PopMatrix();RenderTexture.active=before;}
    }
    static int Difference(Color32[] a,Color32[] b)
    {
        Require(a.Length==b.Length,"pixel count mismatch");int n=0;
        for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))n++;
        return n;
    }
    static void Check(Result result)
    {
        var stock=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Reference.shader");
        var single=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SingleArray.shader");
        Require(stock&&single&&stock.isSupported&&single.isSupported,"shader unavailable");
        var reference=new Material(stock);var candidate=new Material(single);
        var arrays=new Texture2DArray[4];var direct=new Texture2D[4,3];var mesh=new Mesh();
        var target=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var read=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
        using(var buffer=new ComputeBuffer(60,4,ComputeBufferType.Raw))
        try
        {
            for(int b=0;b<4;b++)
            {
                arrays[b]=Array(b);reference.SetTexture("_SfDecoTex"+b,arrays[b]);candidate.SetTexture("_SfDecoTex"+b,arrays[b]);
                for(int layer=0;layer<3;layer++)
                {
                    var t=new Texture2D(16,16,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Clamp};
                    t.SetPixels32(Pattern(b,layer));t.Apply(false,true);direct[b,layer]=t;
                }
            }
            mesh.vertices=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(2,0,0),new Vector3(3,0,0)};
            mesh.triangles=new[]{0,1,2,0,2,3};target.Create();
            reference.SetBuffer("_CollapseVerts",buffer);candidate.SetBuffer("_CollapseVerts",buffer);
            var data=new uint[60];
            // 0=array, 1=full direct dispatch, 2=DIRECT_FEW remapped dispatch.
            for(int lean=0;lean<2;lean++)for(int kind=0;kind<3;kind++)for(int bank=0;bank<4;bank++)for(int layer=0;layer<3;layer++)
            for(int point=0;point<2;point++)for(int wrap=0;wrap<4;wrap++)
            for(int padded=0;padded<2;padded++)for(int additive=0;additive<2;additive++)
            {
                foreach(var m in new[]{reference,candidate})
                {
                    m.DisableKeyword("NO_DIRECT_TEXTURES");m.DisableKeyword("DIRECT_FEW");
                    if(kind==0)m.EnableKeyword("NO_DIRECT_TEXTURES");
                    else
                    {
                        if(kind==2){m.EnableKeyword("DIRECT_FEW");m.SetFloatArray("_DirectMap",new float[48]);}
                        m.SetTexture("_Big"+(kind==2?0:bank),direct[bank,layer]);
                    }
                }
                int flags=(kind==0?bank:4+bank)+point*128+((wrap&1)!=0?256:0)+((wrap&2)!=0?512:0)+(additive*9<<10);
                if(lean!=0)candidate.EnableKeyword("SF_LEAN_VERTEX");else candidate.DisableKeyword("SF_LEAN_VERTEX");
                Input(data,flags,layer,padded!=0);buffer.SetData(data);
                Texture expectedTexture=kind==0?(Texture)arrays[bank]:direct[bank,layer];
                string textureProperty=kind==0?"_SfSingleArray":"_SfSingle2D";
                candidate.SetTexture(textureProperty,expectedTexture);
                var queried=candidate.GetTexture(textureProperty);result.textureQueries++;
                if(!queried)result.textureQueryUnavailable++;else if(queried!=expectedTexture)result.textureQueryMismatches++;
                var a=Draw(reference,mesh,target,read);var again=Draw(reference,mesh,target,read);
                candidate.DisableKeyword("SF_SINGLE_ARRAY");candidate.DisableKeyword("SF_SINGLE_2D");var generic=Draw(candidate,mesh,target,read);
                result.genericDifferences+=Difference(a,generic);
                candidate.EnableKeyword(kind==0?"SF_SINGLE_ARRAY":"SF_SINGLE_2D");var b=Draw(candidate,mesh,target,read);
                int different=Difference(a,b);result.conditions++;result.pixels+=a.Length;
                if(lean!=0)result.leanConditions++;
                if(kind!=0)result.directConditions++;
                result.differences+=different;result.noiseDifferences+=Difference(a,again);
                Require(different==0,"candidate pixel mismatch at condition "+result.conditions);
                // Wrong-bank test proves the comparison actually samples the bound image.
                if(layer==1&&point==0&&wrap==0&&padded==0&&additive==0)
                {
                    if(kind==0)candidate.SetTexture("_SfSingleArray",arrays[(bank+1)%4]);
                    else candidate.SetTexture("_SfSingle2D",direct[(bank+1)%4,layer]);
                    result.negativeControls++;if(Difference(a,Draw(candidate,mesh,target,read))>0)result.negativeControlsDetected++;
                }
            }
            Require(result.noiseDifferences==0,"reference changed between repeats");
            Require(result.genericDifferences==0,"generic fallback differs from stock shader");
            Require(result.leanConditions==1152,"lean vertex fixture incomplete");
            Require(result.negativeControls==24&&result.negativeControlsDetected==24,"negative controls not detected");
            foreach(var shader in new[]{stock,single})foreach(var message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,message.message);
        }
        finally
        {
            foreach(var a in arrays)if(a)UnityEngine.Object.DestroyImmediate(a);
            foreach(var t in direct)if(t)UnityEngine.Object.DestroyImmediate(t);
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(read);
            UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(reference);UnityEngine.Object.DestroyImmediate(candidate);
        }
    }
    static void Build()
    {
        var destination=Path.Combine(Root,"bundle");Directory.CreateDirectory(destination);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        var manifest=BuildPipeline.BuildAssetBundles(destination,new[]{new AssetBundleBuild{
            assetBundleName="stutterfix_singlearray_research",assetNames=new[]{"Assets/Shaders/SingleArray.shader"}}},
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        Require(manifest,"isolated bundle build failed");
    }
}
