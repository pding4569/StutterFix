// Original diagnostic fixture, MIT, StutterFix contributors.
// Read Unity's generated tiled geometry without changing the live Sprite asset.
using System;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class TileMeshFixture
{
    [Serializable] public sealed class Result
    {
        public string editor, device, error, scope="read-only generated mesh API; not trace admission, game pixels or FPS";
        public int conditions, tiledConditions, maxVertices, maxIndices, verticesRead, invalidIndices, nonfinitePositions, pictureDifferences, negativeControls;
        public bool passed;
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static Color32[] Draw(Camera cam,RenderTexture rt,Texture2D read)
    {
        var old=RenderTexture.active;
        try{cam.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);return read.GetPixels32();}
        finally{RenderTexture.active=old;}
    }
    static int Difference(Color32[] a,Color32[] b){int n=0;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))n++;return n;}
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion,device=SystemInfo.graphicsDeviceName};int exit=0;
        GameObject go=null,cameraGo=null;Texture2D texture=null,read=null;Sprite sprite=null;Material material=null;RenderTexture target=null;
        try
        {
            Require(result.editor=="6000.3.10f1","unexpected Editor");
            var method=typeof(SpriteRenderer).GetMethod("GetCurrentMeshData",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            Require(method!=null&&method.ReturnType==typeof(Mesh.MeshDataArray),"generated mesh API unavailable");
            texture=new Texture2D(16,16,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Repeat};
            var colors=new Color32[256];for(int y=0;y<16;y++)for(int x=0;x<16;x++)colors[y*16+x]=new Color32((byte)(x*15),(byte)(y*15),117,(byte)((x+y)%4==0?0:255));
            texture.SetPixels32(colors);texture.Apply(false,true);
            go=new GameObject("Tile mesh read-only fixture");var sr=go.AddComponent<SpriteRenderer>();
            material=new Material(Shader.Find("Sprites/Default"));sr.sharedMaterial=material;
            cameraGo=new GameObject("Tile fixture camera");var cam=cameraGo.AddComponent<Camera>();cam.transform.position=new Vector3(0,0,-10);cam.orthographic=true;cam.orthographicSize=2;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.13f,.19f,.23f,1);
            target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();cam.targetTexture=target;
            read=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            foreach(float pivot in new[]{0f,.5f,1f})foreach(bool flip in new[]{false,true})
            foreach(var size in new[]{new Vector2(.5f,.75f),Vector2.one,new Vector2(2.25f,1.75f),new Vector2(15.3f,2.1f)})
            {
                if(sprite)UnityEngine.Object.DestroyImmediate(sprite);
                sprite=Sprite.Create(texture,new Rect(0,0,16,16),new Vector2(pivot,pivot),16,0,SpriteMeshType.FullRect);
                sr.sprite=sprite;sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=size;sr.flipX=flip;sr.flipY=!flip;
                var before=Draw(cam,target,read);
                using(var data=(Mesh.MeshDataArray)method.Invoke(sr,null))
                {
                    Require(data.Length==1,"unexpected generated mesh count");var mesh=data[0];
                    Require(mesh.vertexCount>0&&mesh.subMeshCount==1&&mesh.HasVertexAttribute(VertexAttribute.Position)&&mesh.HasVertexAttribute(VertexAttribute.TexCoord0),"incomplete native mesh");
                    using(var positions=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
                    using(var uv=new NativeArray<Vector2>(mesh.vertexCount,Allocator.Temp))
                    {
                        mesh.GetVertices(positions);mesh.GetUVs(0,uv);
                        foreach(var p in positions)if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y))result.nonfinitePositions++;
                    }
                    var sub=mesh.GetSubMesh(0);Require(sub.topology==MeshTopology.Triangles&&sub.indexCount%3==0,"unexpected topology");
                    for(int i=sub.indexStart;i<sub.indexStart+sub.indexCount;i++)
                    {
                        uint index=mesh.indexFormat==IndexFormat.UInt16?mesh.GetIndexData<ushort>()[i]:mesh.GetIndexData<uint>()[i];
                        if((long)index+sub.baseVertex<0||(long)index+sub.baseVertex>=mesh.vertexCount)result.invalidIndices++;
                    }
                    result.conditions++;if(mesh.vertexCount>4)result.tiledConditions++;
                    result.maxVertices=Math.Max(result.maxVertices,mesh.vertexCount);result.maxIndices=Math.Max(result.maxIndices,sub.indexCount);result.verticesRead+=mesh.vertexCount;
                }
                result.pictureDifferences+=Difference(before,Draw(cam,target,read));
                sr.enabled=false;if(Difference(before,Draw(cam,target,read))>0)result.negativeControls++;sr.enabled=true;
            }
            Require(result.conditions==24&&result.tiledConditions>0&&result.invalidIndices==0&&result.nonfinitePositions==0&&result.pictureDifferences==0&&result.negativeControls==24,"read-only fixture failed");result.passed=true;
        }
        catch(Exception e){result.error=e.ToString();exit=1;Debug.LogException(e);}
        finally
        {
            if(cameraGo)UnityEngine.Object.DestroyImmediate(cameraGo);if(go)UnityEngine.Object.DestroyImmediate(go);
            if(sprite)UnityEngine.Object.DestroyImmediate(sprite);if(material)UnityEngine.Object.DestroyImmediate(material);
            if(texture)UnityEngine.Object.DestroyImmediate(texture);if(read)UnityEngine.Object.DestroyImmediate(read);
            if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        File.WriteAllText(Path.Combine(Application.dataPath,"../tile-mesh-result.json"),JsonUtility.ToJson(result,true));
        EditorApplication.Exit(exit);
    }
}
