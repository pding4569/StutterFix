// Original fixture, MIT, StutterFix contributors.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class WrapSamplerFixture
{
    [Serializable] public sealed class Result
    {
        public string editor,device,error,scope="exact-size synthetic 2D/array sampling; not padded arrays, trace admission or game quality";
        public int conditions,pixels,directDifferences,arrayDifferences,noiseDifferences,legacyDifferences,legacyFailingConditions,negativeControls;
        public bool passed;
    }
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static Color32[] Draw(Material m,Mesh mesh,RenderTexture target,Texture2D read,int mode)
    {
        m.SetInteger("_Mode",mode);var previous=RenderTexture.active;GL.PushMatrix();
        try{RenderTexture.active=target;GL.Clear(true,true,Color.black);GL.LoadProjectionMatrix(Matrix4x4.identity);GL.modelview=Matrix4x4.identity;Require(m.SetPass(0),"shader pass failed");Graphics.DrawMeshNow(mesh,Matrix4x4.identity);read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);return read.GetPixels32();}
        finally{GL.PopMatrix();RenderTexture.active=previous;}
    }
    static int Difference(Color32[] a,Color32[] b){int count=0;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))count++;return count;}
    public static void Run()
    {
        var r=new Result{editor=Application.unityVersion,device=SystemInfo.graphicsDeviceName};int exit=0;
        Material m=null;Mesh mesh=null;RenderTexture target=null;Texture2D read=null;
        try
        {
            Require(r.editor=="6000.3.10f1"&&SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"unexpected Editor/API");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/WrapSampler.shader");Require(shader&&shader.isSupported,"shader unavailable");
            foreach(var message in ShaderUtil.GetShaderMessages(shader))Require(message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,message.message);
            m=new Material(shader);mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};
            target=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();read=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            foreach(var size in new[]{new Vector2Int(16,16),new Vector2Int(13,7),new Vector2Int(257,19)})
            {
                var t=new Texture2D(size.x,size.y,TextureFormat.RGBA32,false,true);var a=new Texture2DArray(size.x,size.y,1,TextureFormat.RGBA32,false,true);
                try
                {
                    var colors=new Color32[size.x*size.y];for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)colors[y*size.x+x]=new Color32((byte)(x*47+y*3),(byte)(y*67+x*5),(byte)(31+x*7+y*11),(byte)((x+y)%3==0?0:255));
                    t.SetPixels32(colors);t.Apply(false,true);a.SetPixels32(colors,0);a.Apply(false,true);m.SetTexture("_Source2D",t);m.SetTexture("_Candidate2D",t);m.SetTexture("_CandidateArray",a);m.SetVector("_HalfTexel",new Vector4(.5f/size.x,.5f/size.y,0,0));
                    for(int wrap=0;wrap<4;wrap++)for(int point=0;point<2;point++)foreach(float scale in new[]{.2f,1f,7.7f,51f})foreach(float offset in new[]{-.03125f,0f,.5f/size.x})
                    {
                        t.wrapModeU=(wrap&1)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;t.wrapModeV=(wrap&2)!=0?TextureWrapMode.Repeat:TextureWrapMode.Clamp;t.filterMode=point!=0?FilterMode.Point:FilterMode.Bilinear;
                        m.SetInteger("_Flags",point+((wrap&1)!=0?2:0)+((wrap&2)!=0?4:0));m.SetVector("_ScaleOffset",new Vector4(scale,scale*.73f,offset,offset-.5f));
                        var reference=Draw(m,mesh,target,read,0);int noise=Difference(reference,Draw(m,mesh,target,read,0)),direct=Difference(reference,Draw(m,mesh,target,read,1)),array=Difference(reference,Draw(m,mesh,target,read,2)),legacy=Difference(reference,Draw(m,mesh,target,read,3));
                        r.conditions++;r.pixels+=reference.Length;r.noiseDifferences+=noise;r.directDifferences+=direct;r.arrayDifferences+=array;r.legacyDifferences+=legacy;if(legacy!=0)r.legacyFailingConditions++;
                        Require(noise==0&&direct==0&&array==0,"native sampler mismatch at condition "+r.conditions+" direct="+direct+" array="+array);
                        if(wrap==3&&point==0&&scale==1f&&offset==0f)
                        {
                            m.SetTexture("_Candidate2D",Texture2D.whiteTexture);if(Difference(reference,Draw(m,mesh,target,read,1))>0)r.negativeControls++;m.SetTexture("_Candidate2D",t);
                        }
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(a);}
            }
            Require(r.conditions==288&&r.legacyFailingConditions>0&&r.negativeControls==3,"incomplete wrap controls");r.passed=true;
        }
        catch(Exception e){r.error=e.ToString();exit=1;Debug.LogException(e);}
        finally{if(m)UnityEngine.Object.DestroyImmediate(m);if(mesh)UnityEngine.Object.DestroyImmediate(mesh);if(read)UnityEngine.Object.DestroyImmediate(read);if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}}
        File.WriteAllText(Path.Combine(Application.dataPath,"../wrap-sampler-result.json"),JsonUtility.ToJson(r,true));EditorApplication.Exit(exit);
    }
}
