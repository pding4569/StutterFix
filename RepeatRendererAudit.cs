#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Explicit read-only command. No static hooks, renderer writes or per-frame work.
    internal static class RepeatRendererAudit
    {
        [Serializable] sealed class Snapshot
        {
            public string scope="read-only CPU fallback mesh snapshot; not trace admission or performance",unity,device;
            public int frame,screenWidth,screenHeight,targetWidth,targetHeight;
            public int capturedVertices,capturedIndices;
            public bool truncated;
            public List<Row> rows=new List<Row>();
        }
        [Serializable] sealed class Property { public string name,material,block; }
        [Serializable] sealed class Row
        {
            public string name,shader,texture,format,drawMode,tileMode,mask,error;
            public int renderer,decoration,sprite,width,height,vertexCount,indexCount,sortingOrder,sortingLayer;
            public float repeatX,repeatY,ppu;
            public bool flipX,flipY,propertyBlock;
            public Vector2 size,pivot;
            public Vector3 boundsCenter,boundsSize;
            public Matrix4x4 localToWorld;
            public Vector3[] vertices;
            public Vector2[] uv,spriteVertices,spriteUv;
            public int[] indices;
            public List<Property> properties=new List<Property>();
        }
        internal static string Run(string path)
        {
            if(!Path.IsPathRooted(path)||File.Exists(path)||!Directory.Exists(Path.GetDirectoryName(path)))
                throw new ArgumentException("repeataudit requires a new absolute file in an existing evidence directory");
            var main=GpuDecoScene.FindMain();if(!main)throw new InvalidOperationException("main camera unavailable");
            var snapshot=new Snapshot{unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,frame=Time.frameCount,screenWidth=Screen.width,screenHeight=Screen.height,
                targetWidth=main.targetTexture?main.targetTexture.width:Screen.width,targetHeight=main.targetTexture?main.targetTexture.height:Screen.height};
            var planes=GeometryUtility.CalculateFrustumPlanes(main);
            var get=typeof(SpriteRenderer).GetMethod("GetCurrentMeshData",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            if(get==null||get.ReturnType!=typeof(Mesh.MeshDataArray))throw new InvalidOperationException("generated mesh API unavailable");
            foreach(var r in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            {
                if(!r.enabled||r.forceRenderingOff||!r.sprite||r.color.a<=0||(main.cullingMask&(1<<r.gameObject.layer))==0||!GeometryUtility.TestPlanesAABB(planes,r.bounds))continue;
                var d=r.GetComponentInParent<scrVisualDecoration>();if(!d)continue;
                if(snapshot.rows.Count==256){snapshot.truncated=true;break;}
                var row=new Row{name=d.name,renderer=r.GetInstanceID(),decoration=d.GetInstanceID(),sprite=r.sprite.GetInstanceID(),repeatX=d.repeatX,repeatY=d.repeatY,
                    drawMode=r.drawMode.ToString(),tileMode=r.tileMode.ToString(),mask=r.maskInteraction.ToString(),size=r.size,pivot=r.sprite.pivot,ppu=r.sprite.pixelsPerUnit,
                    flipX=r.flipX,flipY=r.flipY,boundsCenter=r.bounds.center,boundsSize=r.bounds.size,localToWorld=r.localToWorldMatrix,sortingLayer=r.sortingLayerID,sortingOrder=r.sortingOrder};
                snapshot.rows.Add(row);
                try
                {
                    var texture=r.sprite.texture;row.texture=texture.name;row.width=texture.width;row.height=texture.height;row.format=texture.format.ToString();
                    row.spriteVertices=r.sprite.vertices;row.spriteUv=r.sprite.uv;
                    var m=r.sharedMaterial;row.shader=m&&m.shader?m.shader.name:"<missing>";
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);row.propertyBlock=!block.isEmpty;
                    if(m&&m.shader)for(int p=0;p<m.shader.GetPropertyCount();p++)
                    {
                        string n=m.shader.GetPropertyName(p);int id=Shader.PropertyToID(n);string a,b="<absent>";
                        switch(m.shader.GetPropertyType(p))
                        {
                            case ShaderPropertyType.Color:case ShaderPropertyType.Vector:
                                a=m.GetVector(id).ToString("R");if(block.HasProperty(id))b=block.GetVector(id).ToString("R");break;
                            case ShaderPropertyType.Texture:
                                var t=m.GetTexture(id);a=t?t.name:"<null>";if(block.HasProperty(id)){var t0=block.GetTexture(id);b=t0?t0.name:"<null>";}break;
                            case ShaderPropertyType.Int:
                                a=m.GetInteger(id).ToString();if(block.HasProperty(id))b=block.GetInt(id).ToString();break;
                            default:a=m.GetFloat(id).ToString("R",System.Globalization.CultureInfo.InvariantCulture);if(block.HasProperty(id))b=block.GetFloat(id).ToString("R",System.Globalization.CultureInfo.InvariantCulture);break;
                        }
                        row.properties.Add(new Property{name=n,material=a,block=b});
                    }
                    using(var meshes=(Mesh.MeshDataArray)get.Invoke(r,null))
                    {
                        if(meshes.Length!=1)throw new InvalidOperationException("unexpected generated mesh count");var mesh=meshes[0];
                        if(mesh.vertexCount>100000-snapshot.capturedVertices||mesh.subMeshCount!=1){snapshot.truncated=true;throw new InvalidOperationException("mesh capture budget or submesh count");}
                        row.vertexCount=mesh.vertexCount;
                        snapshot.capturedVertices+=mesh.vertexCount;
                        using(var vertices=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
                        using(var uv=new NativeArray<Vector2>(mesh.vertexCount,Allocator.Temp))
                        {mesh.GetVertices(vertices);mesh.GetUVs(0,uv);row.vertices=vertices.ToArray();row.uv=uv.ToArray();}
                        var sub=mesh.GetSubMesh(0);if(sub.topology!=MeshTopology.Triangles||sub.indexCount>600000-snapshot.capturedIndices){snapshot.truncated=true;throw new InvalidOperationException("index capture budget or topology");}
                        snapshot.capturedIndices+=sub.indexCount;
                        row.indexCount=sub.indexCount;row.indices=new int[sub.indexCount];
                        for(int i=0;i<sub.indexCount;i++)
                        {
                            uint v=mesh.indexFormat==IndexFormat.UInt16?mesh.GetIndexData<ushort>()[sub.indexStart+i]:mesh.GetIndexData<uint>()[sub.indexStart+i];
                            long index=(long)v+sub.baseVertex;if(index<0||index>=mesh.vertexCount)throw new InvalidOperationException("native index out of bounds");row.indices[i]=(int)index;
                        }
                    }
                }
                catch(Exception e){row.error=e.GetType().Name+": "+e.Message;}
            }
            // Unity's native JsonUtility dropped the nested custom row list in
            // the byte-loaded UMM assembly. Managed field serialization keeps
            // rows/arrays and never traverses Vector3.normalized properties.
            var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Snapshot),
                new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings{MaxItemsInObjectGraph=2000000});
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))serializer.WriteObject(stream,snapshot);
            return "REPEAT_RENDERER_AUDIT rows="+snapshot.rows.Count+" saved="+path+" renderer_writes=0 hooks=0 diagnostic_only=True";
        }
    }
}
#endif
