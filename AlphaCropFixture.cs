#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    internal static class AlphaCropFixture
    {
        // Explicit offscreen fixture, never performance evidence or a game asset edit.
        internal static string Run()
        {
            var shader=Shader.Find("Sprites/Default");
            if(shader==null||!shader.isSupported)return "unavailable=Sprites/Default";
            const int width=40,height=32,size=128;
            var previous=RenderTexture.active;var owned=new List<UnityEngine.Object>();RenderTexture rt=null;
            bool oldSrgb=GL.sRGBWrite;int cases=0,different=0,affected=0,nativeCases=0,lit=0,draws=0;
            var perCase=new List<string>();
            try
            {
                rt=new RenderTexture(size,size,0,RenderTextureFormat.ARGB32);rt.Create();
                var read=new Texture2D(size,size,TextureFormat.RGBA32,false);owned.Add(read);
                var material=new Material(shader);owned.Add(material);
                var pixels=new Color32[width*height];
                // Nonzero RGB in transparent pixels exercises bilinear border behavior.
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    pixels[y*width+x]=new Color32((byte)(31+x*3),(byte)(29+y*5),117,
                        (byte)(x>=11&&x<25&&y>=9&&y<23?40+(x+y)%200:0));
                var raw=new byte[pixels.Length*4];
                for(int i=0;i<pixels.Length;i++){raw[i*4]=pixels[i].r;raw[i*4+1]=pixels[i].g;raw[i*4+2]=pixels[i].b;raw[i*4+3]=pixels[i].a;}
                int[] box;if(!AlphaBoundsProbe.TryFind(raw,width,height,width*4,3,out box))return "unavailable=native-alpha-bounds";
                if(box[0]!=11||box[1]!=9||box[2]!=14||box[3]!=14)throw new InvalidOperationException("Alpha fixture bounds mismatch");
                nativeCases++;
                var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);owned.Add(tex);
                tex.wrapMode=TextureWrapMode.Clamp;tex.SetPixels32(pixels);tex.Apply(false,false);
                byte[] Draw(Mesh mesh,Texture2D image,Matrix4x4 matrix)
                {
                    RenderTexture.active=rt;GL.Clear(false,true,new Color(.11f,.16f,.23f,1));GL.PushMatrix();
                    try
                    {
                        GL.LoadOrtho();material.mainTexture=image;
                        if(!material.SetPass(0))throw new InvalidOperationException("Sprite pass unavailable");
                        Graphics.DrawMeshNow(mesh,matrix);draws++;
                    }
                    finally{GL.PopMatrix();}
                    read.ReadPixels(new Rect(0,0,size,size),0,0,false);read.Apply(false,false);
                    return read.GetRawTextureData<byte>().ToArray();
                }
                Mesh MeshOf(Sprite sprite)
                {
                    var mesh=new Mesh();owned.Add(mesh);var v=sprite.vertices;var vertices=new Vector3[v.Length];var colors=new Color32[v.Length];
                    for(int i=0;i<v.Length;i++){vertices[i]=new Vector3(v[i].x,v[i].y,0);colors[i]=new Color32(255,255,255,255);}
                    var tr=sprite.triangles;var triangles=new int[tr.Length];for(int i=0;i<tr.Length;i++)triangles[i]=tr[i];
                    mesh.vertices=vertices;mesh.uv=sprite.uv;mesh.colors32=colors;mesh.triangles=triangles;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
                }
                foreach(var pivot in new[]{new Vector2(.5f,.5f),new Vector2(.3f,.7f)})
                {
                    AlphaCropPlan plan;if(!AlphaCropPlan.TryCreate(width,height,box[0],box[1],box[2],box[3],pivot.x,pivot.y,1,true,true,true,false,false,false,false,out plan))throw new InvalidOperationException("Alpha fixture plan rejected");
                    var crop=new Texture2D(plan.Width,plan.Height,TextureFormat.RGBA32,false);owned.Add(crop);crop.wrapMode=TextureWrapMode.Clamp;
                    var cut=new Color32[plan.Width*plan.Height];
                    for(int y=0;y<plan.Height;y++)Array.Copy(pixels,(y+plan.Y)*width+plan.X,cut,y*plan.Width,plan.Width);
                    crop.SetPixels32(cut);crop.Apply(false,false);
                    var fullSprite=Sprite.Create(tex,new Rect(0,0,width,height),pivot,40,0,SpriteMeshType.FullRect);owned.Add(fullSprite);
                    var cropSprite=Sprite.Create(crop,new Rect(0,0,plan.Width,plan.Height),new Vector2((float)plan.PivotX,(float)plan.PivotY),40,0,SpriteMeshType.FullRect);owned.Add(cropSprite);
                    var fullMesh=MeshOf(fullSprite);var cropMesh=MeshOf(cropSprite);
                    foreach(var filter in new[]{FilterMode.Point,FilterMode.Bilinear})foreach(float angle in new[]{0f,17f})foreach(float scale in new[]{.7f,1.13f})
                    {
                        tex.filterMode=crop.filterMode=filter;
                        var matrix=Matrix4x4.TRS(new Vector3(.53f,.47f,0),Quaternion.Euler(0,0,angle),new Vector3(scale,scale,1));
                        var baseline=Draw(fullMesh,tex,matrix);var actual=Draw(cropMesh,crop,matrix);
                        int bad=0;for(int i=0;i<baseline.Length;i+=4)
                            if(baseline[i]!=actual[i]||baseline[i+1]!=actual[i+1]||baseline[i+2]!=actual[i+2]||baseline[i+3]!=actual[i+3])bad++;
                        cases++;different+=bad;if(bad>0)affected++;
                        if(!Equal(baseline,Draw(fullMesh,tex,Matrix4x4.TRS(new Vector3(2,2,0),Quaternion.identity,Vector3.one))))lit++;
                        perCase.Add(filter+"/pivot="+pivot+"/angle="+angle+"/scale="+scale+"/different="+bad);
                    }
                }
                return "cases="+cases+" affected="+affected+" different_pixels="+different+" pixels_per_case="+(size*size)+" lit_controls="+lit+" native_cases="+nativeCases+" draws="+draws+" game_texture_mutations=0 actual_loading_hook=0 details="+string.Join(";",perCase);
            }
            finally
            {
                RenderTexture.active=previous;GL.sRGBWrite=oldSrgb;
                foreach(var obj in owned)if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
        private static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
}
#endif
