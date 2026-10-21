#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class GpuDecoLab
{
    public static void Submeshes() => Run(SubmeshBody, "submeshes");

    static Mesh RawRun(int first, int count, Vertex[] reference, int submeshes)
    {
        var mesh = new Mesh { name="GpuDeco submesh run", indexFormat=IndexFormat.UInt32, vertexBufferTarget=GraphicsBuffer.Target.Raw };
        mesh.SetVertexBufferParams(count*4, new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
        var initial = new Vertex[count*4];
        if(reference!=null) Array.Copy(reference,first*4,initial,0,initial.Length);
        mesh.SetVertexBufferData(initial,0,0,initial.Length);
        mesh.subMeshCount=submeshes;
        for(int s=0;s<submeshes;s++)
        {
            int a=s*count/submeshes, b=(s+1)*count/submeshes;
            var indices=new List<int>();
            for(int d=a;d<b;d++) indices.AddRange(new[]{d*4,d*4+2,d*4+1,d*4+2,d*4+3,d*4+1});
            mesh.SetIndices(indices.ToArray(),MeshTopology.Triangles,s,false);
        }
        mesh.bounds=new Bounds(Vector3.zero,Vector3.one*1000000);
        mesh.UploadMeshData(true);
        return mesh;
    }

    static void SubmeshBody()
    {
        var shader=Shader(); var data=Synthetic(2000);
        var meshes=new List<Mesh>(); var buffers=new List<GraphicsBuffer>();
        using(var gpu=new Evaluation(shader,data)) using(var commands=new CommandBuffer {name="GpuDeco 80 buffers 1200 submeshes"})
        try
        {
            var evaluated=gpu.Dispatch(data.BakeTime,true);
            Compare(data,evaluated.values,evaluated.vertices,false);
            int copy=shader.FindKernel("CopyRun");
            commands.SetComputeBufferParam(shader,copy,"_CopySource",gpu.VertexBuffer);
            for(int run=0;run<80;run++)
            {
                var mesh=RawRun(run*25,25,null,15); meshes.Add(mesh);
                var buffer=mesh.GetVertexBuffer(0); buffers.Add(buffer);
                commands.SetComputeIntParam(shader,"_CopyFirstVertex",run*100);
                commands.SetComputeIntParam(shader,"_CopyVertexCount",100);
                commands.SetComputeBufferParam(shader,copy,"_Vertices",buffer);
                commands.DispatchCompute(shader,copy,2,1,1);
            }
            Graphics.ExecuteCommandBuffer(commands);
            int mismatches=0;
            for(int run=0;run<80;run++)
            {
                var actual=new Vertex[100]; buffers[run].GetData(actual);
                for(int k=0;k<100;k++)
                {
                    var expected=evaluated.vertices[run*100+k];
                    if(!actual[k].position.Equals(expected.position)||!actual[k].color.Equals(expected.color)||!actual[k].uv.Equals(expected.uv)) mismatches++;
                }
            }
            Require(mismatches==0,"submesh raw copy mismatch");
            Log("[gpudeco-lab] submeshes raw_buffers=80 submeshes=1200 decorations=2000 vertices=8000 mismatches="+mismatches);
            // No renders/readback inside submission measurements. Keep all samples,
            // including driver backpressure; the median alone is not the budget gate.
            for(int n=0;n<20;n++) { gpu.Dispatch(data.BakeTime,false); Graphics.ExecuteCommandBuffer(commands); }
            var costs=new List<double>();
            for(int batch=0;batch<5;batch++)
            {
                var timer=Stopwatch.StartNew();
                for(int n=0;n<100;n++) { gpu.Dispatch(data.BakeTime+(batch*100+n)*.0001,false); Graphics.ExecuteCommandBuffer(commands); }
                timer.Stop(); costs.Add(timer.Elapsed.TotalMilliseconds/100);
            }
            Log("[gpudeco-lab] cost raw_buffers=80 submeshes=1200 dispatches_per_frame=81 cpu_submit_ms="+N(costs.Average())
                +" batch_min_ms="+N(costs.Min())+" batch_max_ms="+N(costs.Max())+" batches="+string.Join(",",costs.Select(N))
                +" gpu_timestamp_ms=UNAVAILABLE engine_render_cost=UNMEASURED synthetic=True");
            // Timed dispatches advance the synthetic clock. Restore the oracle's
            // exact time before comparing the two rendered images.
            gpu.Dispatch(data.BakeTime,false); Graphics.ExecuteCommandBuffer(commands);
            TextureAndOrderFixture(meshes,evaluated.vertices);
            Log("GPUDECO_LAB_SUBMESH=PASS submit_budget="+(costs.Max()<=.3?"PASS":"FAIL")+" real_export=False G0=UNMEASURED G1=UNMEASURED");
        }
        finally { foreach(var buffer in buffers) buffer.Dispose(); foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh); }
    }

    // Actual MeshRenderers and borrowed material instances, no CommandBuffer.DrawMesh
    // ordering shortcut. Test both the large explicit boundaries and tied keys.
    static void TextureAndOrderFixture(List<Mesh> gpuMeshes,Vertex[] vertices)
    {
        var resources=new List<UnityEngine.Object>(); var objects=new List<GameObject>();
        bool oldAsync=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var oldRT=RenderTexture.active;
        try
        {
            var textures=new Texture2D[3]; var materials=new Material[3];
            var colors=new[]{new Color(.9f,.2f,.1f,.45f),new Color(.1f,.9f,.3f,.6f),new Color(.2f,.1f,.9f,.75f)};
            for(int n=0;n<3;n++)
            {
                textures[n]=new Texture2D(2,2,TextureFormat.RGBA32,false); resources.Add(textures[n]);
                textures[n].SetPixels(Enumerable.Repeat(colors[n],4).ToArray()); textures[n].Apply();
                materials[n]=new Material(UnityEngine.Shader.Find("Sprites/Default")); resources.Add(materials[n]); materials[n].mainTexture=textures[n];
            }
            var cameraObject=new GameObject("submesh fixture camera"); resources.Add(cameraObject);
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true; camera.orthographicSize=8;
            camera.transform.position=new Vector3(0,0,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.cullingMask=1<<30;
            var rt=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32); resources.Add(rt); camera.targetTexture=rt;
            var image=new Texture2D(128,128,TextureFormat.RGBA32,false); resources.Add(image);
            Func<Color32[]> capture=()=>{camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply(); return image.GetPixels32();};
            Action clear=()=>{foreach(var go in objects) UnityEngine.Object.DestroyImmediate(go); objects.Clear();};
            Action<Mesh,Material[],int> add=(mesh,mats,order)=>{
                var go=new GameObject("submesh fixture renderer"); objects.Add(go); go.layer=30;
                go.AddComponent<MeshFilter>().sharedMesh=mesh; var r=go.AddComponent<MeshRenderer>(); r.sharedMaterials=mats; r.sortingOrder=order;
                var block=new MaterialPropertyBlock(); block.SetColor("_RendererColor",Color.white); block.SetVector("_Flip",Vector4.one); r.SetPropertyBlock(block);
            };
            // 80 renderers x 15 texture runs. Reference uses separate renderers for
            // each run, ordered explicitly inside the same non-owned boundaries.
            for(int run=0;run<80;run++)
            for(int sub=0;sub<15;sub++)
            {
                int a=sub*25/15, b=(sub+1)*25/15;
                var mesh=RawRun(run*25+a,b-a,vertices,1); resources.Add(mesh);
                add(mesh,new[]{materials[sub%3]},run*16+sub);
            }
            var reference=capture();
            RenderCost(camera,"reference_1200_mesh_renderers"); clear();
            for(int run=0;run<80;run++) add(gpuMeshes[run],Enumerable.Range(0,15).Select(s=>materials[s%3]).ToArray(),run*16);
            var actual=capture();
            PixelDiff(reference,actual,"scale_80x15",true);
            RenderCost(camera,"candidate_80_renderers_1200_submeshes");
            File.WriteAllBytes(Path.Combine(Evidence,"submeshes.png"),image.EncodeToPNG()); clear();
            // Tied key sorting, alpha overlap, a non-owned renderer between groups.
            // Build six distinct material instances, then derive the oracle order
            // from their signed IDs just as the measured game rule requires.
            for(int variant=0;variant<6;variant++)
            {
                var borrowed=Enumerable.Range(0,7).Select(i=>{var m=new Material(materials[(i+variant)%3]);resources.Add(m);return m;}).OrderBy(m=>m.GetInstanceID()).ToArray();
                var points=new Vertex[28];
                for(int d=0;d<7;d++) for(int k=0;k<4;k++) points[d*4+k]=new Vertex{position=new Vector3(k%2==0?-3:3,k<2?-3:3,0),color=Color.white,uv=new Vector2(k%2,k/2)};
                var singles=new Mesh[7];
                for(int d=0;d<7;d++) {singles[d]=RawRun(d,1,points,1);resources.Add(singles[d]);}
                var left=RawRun(0,3,points,3); var right=RawRun(4,3,points,3); resources.Add(left);resources.Add(right);
                // Renderer construction order is independently varied.
                var sequence=Enumerable.Range(0,7).OrderBy(i=>(i*(variant%2==0?3:5)+variant)%7);
                foreach(int d in sequence)
                {
                    var go=new GameObject("original SpriteRenderer"); objects.Add(go);go.layer=30;go.transform.localScale=Vector3.one*6;
                    var sprite=Sprite.Create((Texture2D)borrowed[d].mainTexture,new Rect(0,0,2,2),new Vector2(.5f,.5f),2,0,SpriteMeshType.FullRect);resources.Add(sprite);
                    var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=borrowed[d];
                }
                var spriteReference=capture();clear();
                foreach(int d in sequence) add(singles[d],new[]{borrowed[d]},0);
                reference=capture(); clear();
                PixelDiff(spriteReference,reference,"sprite_oracle_"+variant,true);
                if(variant%2==0) {add(left,borrowed.Take(3).ToArray(),0);add(singles[3],new[]{borrowed[3]},0);add(right,borrowed.Skip(4).ToArray(),0);}
                else {add(right,borrowed.Skip(4).ToArray(),0);add(singles[3],new[]{borrowed[3]},0);add(left,borrowed.Take(3).ToArray(),0);}
                actual=capture(); PixelDiff(reference,actual,"tied_keys_"+variant,true); clear();
                add(right,borrowed.Skip(4).Reverse().ToArray(),0);add(singles[3],new[]{borrowed[3]},0);add(left,borrowed.Take(3).Reverse().ToArray(),0);
                var negative=capture(); PixelDiff(reference,negative,"reversed_materials_"+variant,false); clear();
            }
        }
        finally { RenderTexture.active=oldRT; foreach(var go in objects) UnityEngine.Object.DestroyImmediate(go); for(int n=resources.Count-1;n>=0;n--) UnityEngine.Object.DestroyImmediate(resources[n]); ShaderUtil.allowAsyncCompilation=oldAsync; }
    }

    static void PixelDiff(Color32[] reference,Color32[] actual,string label,bool requireEqual)
    {
        int changed=0,maximum=0,covered=0;
        for(int i=0;i<reference.Length;i++)
        {
            var a=reference[i];var b=actual[i]; if(a.r!=0||a.g!=0||a.b!=0) covered++;
            int d=Math.Max(Math.Max(Math.Abs(a.r-b.r),Math.Abs(a.g-b.g)),Math.Max(Math.Abs(a.b-b.b),Math.Abs(a.a-b.a)));
            if(d!=0) changed++;maximum=Math.Max(maximum,d);
        }
        Log("[gpudeco-lab] pixels fixture="+label+" changed="+changed+" max_channel="+maximum+" covered="+covered+" expected_equal="+requireEqual+" shader=Sprites/Default game_shader=False");
        Require(covered>0,"empty reference: "+label);
        Require(requireEqual?changed==0:changed>0,"pixel oracle failed: "+label);
    }

    static void RenderCost(Camera camera,string label)
    {
        for(int n=0;n<5;n++) camera.Render();
        var costs=new List<double>();
        for(int batch=0;batch<5;batch++)
        {
            var watch=Stopwatch.StartNew();
            for(int n=0;n<20;n++) camera.Render();
            watch.Stop();costs.Add(watch.Elapsed.TotalMilliseconds/20);
        }
        Log("[gpudeco-lab] render fixture="+label+" camera_render_cpu_wall_ms="+N(costs.Average())
            +" batch_min_ms="+N(costs.Min())+" batch_max_ms="+N(costs.Max())
            +" samples=100 resolution=128x128 includes=culling,submission,possible_driver_wait gpu_timestamp_ms=UNAVAILABLE game_FPS=UNMEASURED");
    }
}
#endif
