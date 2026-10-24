#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex = StutterFix.GpuDecoGpu.Vertex;

public static partial class GpuDecoLab
{
    // Isolate rasterization from animation: even exact CPU TransformPoint
    // vertices need not rasterize like the original object's local vertices.
    public static void Geometry() => Run(GeometryBody, "geometry");
    static void GeometryBody()
    {
        var owned = new List<UnityEngine.Object>();
        var previous = RenderTexture.active;
        bool async = UnityEditor.ShaderUtil.allowAsyncCompilation;
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        try
        {
            var texture = new Texture2D(128,128,TextureFormat.RGBA32,false) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp}; owned.Add(texture);
            var pixels = new Color32[128*128];
            for(int y=0;y<128;y++) for(int x=0;x<128;x++)
                pixels[y*128+x]=new Color32((byte)(x*73+y*29),(byte)(x*31+y*47),(byte)(x*13+y*61),255);
            texture.SetPixels32(pixels);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),128,0,SpriteMeshType.FullRect);owned.Add(sprite);
            var material=new Material(UnityEngine.Shader.Find("Sprites/Default")) {mainTexture=texture};owned.Add(material);
            var cameraObject=new GameObject("geometry camera");owned.Add(cameraObject);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
            var rt=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32);owned.Add(rt);camera.targetTexture=rt;
            var image=new Texture2D(256,256,TextureFormat.RGBA32,false);owned.Add(image);
            Func<Color32[]> capture=()=>{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();return image.GetPixels32();};
            var original=new GameObject("native geometry") {layer=30};owned.Add(original);
            var sr=original.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=material;sr.color=Color.white;
            var replacement=new GameObject("mesh geometry") {layer=30};owned.Add(replacement);
            var mesh=new Mesh {vertexBufferTarget=GraphicsBuffer.Target.Raw};owned.Add(mesh);replacement.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=replacement.AddComponent<MeshRenderer>();mr.sharedMaterial=material;mr.shadowCastingMode=ShadowCastingMode.Off;
            mr.lightProbeUsage=LightProbeUsage.Off;mr.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",texture);props.SetColor("_RendererColor",Color.white);props.SetVector("_Flip",Vector4.one);mr.SetPropertyBlock(props);
            var shape=sprite.vertices.Select(v=>new Vector3(v.x,v.y,0)).ToArray();
            mesh.SetVertexBufferParams(shape.Length,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
            var points=shape.Select((v,i)=>new Vertex {position=v,color=Color.white,uv=sprite.uv[i]}).ToArray();
            mesh.SetVertexBufferData(points,0,0,points.Length);mesh.triangles=sprite.triangles.Select(x=>(int)x).ToArray();
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(2000000,2000000,1));
            int cases=0,worldDifferent=0,localDifferent=0,worldMax=0,localMax=0;
            foreach(float origin in new[]{0f,128f,8192f,92842.6171875f})
            foreach(float angle in new[]{0f,13.0625f,89.8125f,179.9375f})
            foreach(float cameraAngle in new[]{0f,17.125f})
            {
                cases++;
                original.transform.SetPositionAndRotation(new Vector3(origin+.137f,origin-.213f,0),Quaternion.Euler(0,0,angle));
                original.transform.localScale=new Vector3(1.31f,.73f,1);
                camera.transform.SetPositionAndRotation(new Vector3(origin,origin,-10),Quaternion.Euler(0,0,cameraAngle));
                sr.enabled=true;mr.enabled=false;var reference=capture();
                sr.enabled=false;mr.enabled=true;
                replacement.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);replacement.transform.localScale=original.transform.localScale;
                for(int k=0;k<points.Length;k++)points[k].position=shape[k];
                mesh.SetVertexBufferData(points,0,0,points.Length);var local=capture();
                replacement.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);replacement.transform.localScale=Vector3.one;
                for(int k=0;k<points.Length;k++)points[k].position=original.transform.TransformPoint(shape[k]);
                using(var buffer=mesh.GetVertexBuffer(0)) {
                    buffer.SetData(points);var copied=new Vertex[points.Length];buffer.GetData(copied);
                    for(int k=0;k<points.Length;k++)Require(copied[k].position.Equals(points[k].position) && copied[k].uv.Equals(points[k].uv) && copied[k].color.Equals(points[k].color),"truth world buffer upload mismatch");
                }
                var world=capture();
                var worldAgain=capture();mr.enabled=false;sr.enabled=true;var restored=capture();
                int lc=0,wc=0,lm=0,wm=0,lit=0;
                for(int i=0;i<reference.Length;i++)
                {
                    if(reference[i].r!=0 || reference[i].g!=0 || reference[i].b!=0)lit++;
                    Require(reference[i].Equals(restored[i]),"native A/A2 baseline unstable");
                    Require(world[i].Equals(worldAgain[i]),"world mesh repeat unstable");
                    int ld=0,wd=0;
                    for(int c=0;c<4;c++){ld=Math.Max(ld,Math.Abs(reference[i][c]-local[i][c]));wd=Math.Max(wd,Math.Abs(reference[i][c]-world[i][c]));}
                    if(ld>0)lc++;if(wd>0)wc++;lm=Math.Max(lm,ld);wm=Math.Max(wm,wd);
                }
                Require(lit>1000,"geometry positive control blank");
                if(lc>0)localDifferent++;if(wc>0)worldDifferent++;worldMax=Math.Max(worldMax,wm);localMax=Math.Max(localMax,lm);
                Log("[gpudeco-geometry] origin="+N(origin)+" angle="+N(angle)+" camera_angle="+N(cameraAngle)+" local_changed="+lc+" local_max="+lm+" world_changed="+wc+" world_max="+wm+" aa2=0 world_repeat=0 geometry_truth=CPU_TransformPoint shader=Sprites/Default");
            }
            Log("GPUDECO_GEOMETRY=MEASURED cases="+cases+" local_different_cases="+localDifferent+" local_max="+localMax+" world_different_cases="+worldDifferent+" world_max="+worldMax+" game_cause=UNCONFIRMED game_G1=UNMEASURED");
        }
        finally
        {
            RenderTexture.active=previous;
            for(int i=owned.Count-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(owned[i]);
            UnityEditor.ShaderUtil.allowAsyncCompilation=async;
        }
    }
}
#endif
