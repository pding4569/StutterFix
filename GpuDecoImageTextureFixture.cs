#if AUTOTEST
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Isolated pixel fixture: borrowed sprites/material properties, temporary
        // renderers only. No live decoration ownership, native VB or camera hook.
        static void RunGpuImageTextureFixture()
        {
            RequireLive(FrameCompare.Paused&&!gpuLiveActive&&!gpuLivePrepared&&!gpuImageShadow&&g2Window<0,"texture fixture requires paused CPU and no prepared GPU resources");
            RequireLive(gpuImageVariants!=null&&!string.IsNullOrEmpty(gpuBundlePath),"texture fixture requires variants and bundle");
            var variants=gpuImageVariants.Where(v=>v.Status==GpuDecoImageVariant.Kind.ReadyQuad).ToArray();
            RequireLive(variants.Length>0,"texture fixture has no quads");
            Material source=null;
            foreach(var obj in baselineObjects)
            {
                var d=obj as scrVisualDecoration;
                if(d&&d.spriteRenderer&&d.spriteRenderer.sharedMaterial&&d.spriteRenderer.sharedMaterial.HasProperty("_MainTex"))
                {source=d.spriteRenderer.sharedMaterial;break;}
            }
            RequireLive(source,"texture fixture has no source material");
            AssetBundle bundle=null;GameObject cpuObject=null,gpuObject=null;Material material=null;
            RenderTexture target=null;Texture2D read=null;CommandBuffer commands=null;GpuDecoGpu gpu=null;
            var oldActive=RenderTexture.active;
            int samples=0,different=0,max=0,changedImages=0,nonempty=0,topologyRejected=0;
            long changedPixels=0;Color32[] first=null;
            try
            {
                bundle=AssetBundle.LoadFromFile(System.IO.Path.GetFullPath(gpuBundlePath));RequireLive(bundle,"texture fixture bundle");
                var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");RequireLive(shader,"texture fixture compute");
                material=new Material(source){name="GpuDeco image pixel fixture",hideFlags=HideFlags.HideAndDontSave};
                cpuObject=new GameObject("GpuDeco CPU image fixture"){hideFlags=HideFlags.HideAndDontSave};
                gpuObject=new GameObject("GpuDeco GPU image fixture"){hideFlags=HideFlags.HideAndDontSave};
                var cpu=cpuObject.AddComponent<SpriteRenderer>();cpu.sharedMaterial=material;cpu.color=Color.white;cpu.enabled=false;
                var draw=gpuObject.AddComponent<MeshRenderer>();draw.sharedMaterial=material;draw.enabled=false;
                var filter=gpuObject.AddComponent<MeshFilter>();var block=new MaterialPropertyBlock();
                target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32){antiAliasing=1,hideFlags=HideFlags.HideAndDontSave};
                RequireLive(target.Create(),"texture fixture RT creation");
                read=new Texture2D(256,256,TextureFormat.RGBA32,false);commands=new CommandBuffer{name="GpuDeco image fixture"};
                GpuDecoData.Item Item(GpuDecoImageVariant v)
                {
                    var item=new GpuDecoData.Item{Constants=new[]{1f,1f,1f,1f,0f,0f,0f,0f},Triangles=v.Triangles.Select(x=>(int)x).ToArray()};
                    item.Initial[5]=item.Initial[6]=item.Initial[7]=item.Initial[8]=item.Initial[9]=item.Initial[10]=item.Initial[11]=1;
                    item.OffsetBasis[3]=item.OffsetBasis[7]=1;
                    for(int i=0;i<16;i++)item.Tracks[i]=Array.Empty<DecoBakeCore.Segment>();
                    for(int i=0;i<4;i++)
                    {item.Shape[i*3]=v.Vertices[i].x;item.Shape[i*3+1]=v.Vertices[i].y;item.UV[i*2]=v.UV[i].x;item.UV[i*2+1]=v.UV[i].y;}
                    return item;
                }
                var initial=Item(variants[0]);
                var data=new GpuDecoData{Items=new[]{initial},Pitch=1,TweenScale=1,Camera=new[]{0f,0f,0f,1f}};
                gpu=new GpuDecoGpu(shader,data){UseAnchors=false};filter.sharedMesh=gpu.Mesh;
                Color32[] Render(Renderer renderer,Matrix4x4 view,Matrix4x4 projection)
                {
                    commands.Clear();commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,new Color(.17f,.13f,.11f,.19f));
                    commands.SetViewProjectionMatrices(view,GL.GetGPUProjectionMatrix(projection,true));
                    commands.DrawRenderer(renderer,material,0,0);Graphics.ExecuteCommandBuffer(commands);
                    RenderTexture.active=target;read.ReadPixels(new Rect(0,0,256,256),0,0,false);read.Apply(false,false);
                    return read.GetPixels32();
                }
                foreach(var v in variants)
                {
                    var next=Item(v);
                    if(!next.Triangles.SequenceEqual(initial.Triangles)){topologyRejected++;continue;}
                    next.Initial=initial.Initial;next.Tracks=initial.Tracks;next.Anchors=initial.Anchors;
                    gpu.UpdateImageGeometry(0,next);gpu.Dispatch(0,false);
                    cpu.sprite=v.Source.sprite;
                    block.Clear();block.SetTexture(Shader.PropertyToID("_MainTex"),v.Source.sprite.texture);draw.SetPropertyBlock(block);
                    var lo=v.Vertices[0];var hi=lo;
                    foreach(var point in v.Vertices){lo=Vector2.Min(lo,point);hi=Vector2.Max(hi,point);}
                    var center=(lo+hi)*.5f;float extent=Mathf.Max(hi.x-lo.x,hi.y-lo.y)*.6f;
                    RequireLive(extent>0&&DecoBakeCore.Finite(extent),"texture fixture projection");
                    var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.Translate(new Vector3(-center.x,-center.y,10));
                    var projection=Matrix4x4.Ortho(-extent,extent,-extent,extent,.1f,100);
                    var a=Render(cpu,view,projection);var b=Render(draw,view,projection);
                    int bad=0;bool changed=false,visible=false;
                    for(int i=0;i<a.Length;i++)
                    {
                        if(!a[i].Equals(b[i]))
                        {
                            bad++;max=Math.Max(max,Math.Abs(a[i].r-b[i].r));max=Math.Max(max,Math.Abs(a[i].g-b[i].g));
                            max=Math.Max(max,Math.Abs(a[i].b-b[i].b));max=Math.Max(max,Math.Abs(a[i].a-b[i].a));
                        }
                        if(first!=null&&!first[i].Equals(a[i]))changed=true;
                        if(!a[i].Equals(a[0]))visible=true;
                    }
                    if(first==null)first=a;
                    if(changed)changedImages++;if(visible)nonempty++;
                    if(bad>0)different++;changedPixels+=bad;samples++;
                }
            }
            finally
            {
                RenderTexture.active=oldActive;commands?.Dispose();gpu?.Dispose();
                if(cpuObject)UnityEngine.Object.Destroy(cpuObject);if(gpuObject)UnityEngine.Object.Destroy(gpuObject);
                if(material)UnityEngine.Object.Destroy(material);if(read)UnityEngine.Object.Destroy(read);
                if(target){target.Release();UnityEngine.Object.Destroy(target);}if(bundle)bundle.Unload(true);
            }
            GpuLog("image_texture_fixture samples="+samples+" different_images="+different+" different_pixels="+changedPixels+" max_RGBA="+max
                +" nonuniform_images="+nonempty+" different_from_first="+changedImages+" topology_rejected="+topologyRejected
                +" CPU_decoration_setters_changed=0 live_ownership_changed=0 borrowed_assets_destroyed=0 performance=False scope=isolated_256x256_white_quad_pass0");
            RequireLive(samples>0&&different==0&&nonempty>0&&changedImages>0,"image texture pixel fixture failed");
        }
    }
}
#endif
