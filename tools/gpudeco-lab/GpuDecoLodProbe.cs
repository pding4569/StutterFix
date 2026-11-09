#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class GpuDecoLab
{
    // Independent GPU regression: current bounds and partial clear versus full-screen/full-clear.
    public static void LodCompositeProbe() => Run(LodCompositeProbeBody, "lod-composite-codex");
    static void LodCompositeProbeBody()
    {
        var bundle = AssetBundle.LoadFromFile(Path.Combine(Evidence, "drawcollapse5/bundle/stutterfix_gpudeco"));
        Require(bundle, "LOD bundle missing");
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var previous = RenderTexture.active;
        var shader = bundle.LoadAsset<Shader>("Assets/Shaders/GpuDecoBatch.shader");
        var clearShader = bundle.LoadAsset<Shader>("Assets/Shaders/GpuDecoLodClear.shader");
        var compute = bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDecoLod.compute");
        var material = new Material(shader); var clear = new Material(clearShader);
        var quad = new Mesh(); quad.vertices = new[] { Vector3.zero, Vector3.right, Vector3.right * 2, Vector3.right * 3 };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        var tex = new Texture2DArray(4, 4, 1, TextureFormat.RGBA32, false, true);
        var colors = new Color[16]; for (int i=0;i<16;i++) colors[i]=new Color(i/16f,.7f,.2f,.2f+i/32f);
        tex.SetPixels(colors,0); tex.Apply(); material.SetTexture("_SfDecoTex0",tex);
        material.EnableKeyword("NO_DIRECT_TEXTURES");
        int cases=0;
        try
        {
            Require(shader.isSupported && clearShader.isSupported && material.HasProperty("_LodMode"), "LOD shader unsupported/property missing");
            using(var verts=new ComputeBuffer(60,4,ComputeBufferType.Raw))
            using(var slots=new ComputeBuffer(1,4))
            using(var output=new ComputeBuffer(2,4))
            using(var box=new ComputeBuffer(4,4))
            {
                slots.SetData(new uint[]{0});
                int kc=compute.FindKernel("SpanLodClear"), k=compute.FindKernel("SpanLod");
                compute.SetInt("_LodSpans",1);compute.SetInt("_LodSlots",1);
                compute.SetBuffer(kc,"_LodOut",output);compute.SetBuffer(kc,"_LodBox",box);
                compute.SetBuffer(k,"_LodOut",output);compute.SetBuffer(k,"_LodBox",box);
                compute.SetBuffer(k,"_LodSpanOfSlot",slots);compute.SetBuffer(k,"_LodVerts",verts);
                // Check explicit GPU projection against Unity's actual targetTexture camera path.
                var go=new GameObject("LOD projection oracle");var camera=go.AddComponent<Camera>();
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
                camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=0;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.allowHDR=false;camera.allowMSAA=false;
                try
                {
                    foreach(int divisor in new[]{2,4})
                    {
                        var rt=new RenderTexture(131/divisor,103/divisor,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.Create();
                        try
                        {
                            camera.targetTexture=rt;var data=new float[60];
                            for(int c=0;c<4;c++)
                            {
                                int a=c*15;data[a]=(c==1||c==2)?.35f:-.8f;data[a+1]=c>=2?.6f:-.3f;
                                data[a+3]=.6f;data[a+4]=.9f;data[a+5]=.4f;data[a+6]=.7f;
                                data[a+7]=(c==1||c==2)?1:0;data[a+8]=c>=2?1:0;
                                data[a+11]=data[a+12]=1;data[a+13]=data[a+14]=.125f;
                            }
                            verts.SetData(data);Color32[] baseline=null;
                            for(int mode=0;mode<=2;mode+=2)
                            {
                                using(var cb=new CommandBuffer())
                                {
                                    var p=new MaterialPropertyBlock();p.SetFloat("_LodMode",mode);
                                    p.SetMatrix("_LodDrawVP",GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix);
                                    cb.SetGlobalBuffer("_CollapseVerts",verts);cb.DrawMesh(quad,Matrix4x4.identity,material,0,0,p);
                                    camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,cb);camera.Render();camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,cb);
                                }
                                RenderTexture.active=rt;var read=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);
                                read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);read.Apply();var pixels=read.GetPixels32();UnityEngine.Object.DestroyImmediate(read);
                                if(mode==0)baseline=pixels;
                                else
                                {
                                    int nonzero=0;for(int i=0;i<pixels.Length;i++){Require(pixels[i].Equals(baseline[i]),"camera targetTexture projection mismatch");if(pixels[i].a!=0)nonzero++;}
                                    Require(nonzero>0,"camera projection vacuous coverage");
                                }
                            }
                        }
                        finally{camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                    }
                    Log("LOD_CAMERA_PROJECTION=PASS cases=2 rgba_max_error=0 oracle=UnityObjectToClipPos_targetTexture");
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
                foreach(int width in new[]{128,131}) foreach(int divisor in new[]{2,4})
                foreach(bool target in new[]{false,true}) foreach(bool point in new[]{false,true})
                {
                    int height=width-27,lw=width/divisor,lh=height/divisor;
                    var low=new RenderTexture(lw,lh,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                    var full=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                    low.filterMode=FilterMode.Bilinear;low.wrapMode=TextureWrapMode.Clamp;low.Create();full.Create();
                    try
                    {
                        // Both GPU projection conventions are exercised with asymmetric geometry.
                        var vp=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1,1,-1,1,-1,1),target);
                        compute.SetMatrix("_LodVP",vp);compute.SetVector("_LodScreen",new Vector4(width,height,0,0));
                        var size=new Vector4(width,height,1f/width,1f/height);
                        for(int frame=0;frame<5;frame++)
                        {
                            var data=new float[60];
                            float x=frame==0?-.83f:frame==1?.24f:frame==2?-.99f:.66f;
                            float y=frame==0?-.53f:frame==1?.32f:frame==2?.87f:-.91f;
                            for(int c=0;c<4;c++)
                            {
                                int a=c*15;
                                data[a]=frame==3?0:x+((c==1||c==2)?.23f:0);
                                data[a+1]=frame==3?0:y+(c>=2?.17f:0);
                                data[a+3]=.6f;data[a+4]=.9f;data[a+5]=.4f;data[a+6]=.7f;
                                data[a+7]=(c==1||c==2)?1:0;data[a+8]=c>=2?1:0;
                                data[a+9]=point?128:0;data[a+11]=data[a+12]=1;data[a+13]=data[a+14]=.125f;
                            }
                            verts.SetData(data);compute.Dispatch(kc,1,1,1);compute.Dispatch(k,1,1,1);
                            var current=new int[4];box.GetData(current);
                            Color32[] baseline=null;
                            for(int partial=0;partial<2;partial++)
                            {
                                box.SetData(current);
                                using(var cb=new CommandBuffer())
                                {
                                    cb.SetGlobalBuffer("_CollapseVerts",verts);cb.SetGlobalBuffer("_LodBox",box);
                                    cb.SetRenderTarget(low);cb.SetViewport(new Rect(0,0,lw,lh));
                                    if(partial==0)cb.ClearRenderTarget(false,true,Color.clear);
                                    else
                                    {
                                        // Deliberately dirty every unused texel: a stale sample becomes magenta.
                                        cb.ClearRenderTarget(false,true,Color.magenta);
                                        var cp=new MaterialPropertyBlock();cp.SetVector("_LodTarget",size);
                                        cp.SetVector("_LodClearSize",new Vector4(lw,lh,0,0));cp.SetFloat("_LodSpan",0);
                                        cb.DrawMesh(quad,Matrix4x4.identity,clear,0,0,cp);
                                    }
                                    var p=new MaterialPropertyBlock();p.SetFloat("_LodMode",2);p.SetMatrix("_LodDrawVP",vp);
                                    cb.DrawMesh(quad,Matrix4x4.identity,material,0,0,p);
                                    Graphics.ExecuteCommandBuffer(cb);
                                }
                                if(partial==0)box.SetData(new[]{0,0,width,height});
                                using(var cb=new CommandBuffer())
                                {
                                    cb.SetRenderTarget(full);cb.SetViewport(new Rect(0,0,width,height));cb.ClearRenderTarget(false,true,Color.clear);
                                    cb.SetGlobalBuffer("_LodBox",box);cb.SetGlobalBuffer("_CollapseVerts",verts);
                                    var p=new MaterialPropertyBlock();p.SetFloat("_LodMode",1);p.SetFloat("_LodSpan",0);
                                    p.SetTexture("_LodTex",low);p.SetVector("_LodTarget",size);
                                    cb.DrawMesh(quad,Matrix4x4.identity,material,0,0,p);Graphics.ExecuteCommandBuffer(cb);
                                }
                                RenderTexture.active=full;var read=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                                read.ReadPixels(new Rect(0,0,width,height),0,0);read.Apply();var pixels=read.GetPixels32();UnityEngine.Object.DestroyImmediate(read);
                                if(partial==0)baseline=pixels;
                                else
                                {
                                    int nonzero=0;
                                    for(int i=0;i<pixels.Length;i++)
                                    {
                                        Require(pixels[i].Equals(baseline[i]),"LOD pixel mismatch case="+cases+" frame="+frame+" pixel="+i+" bounds="+string.Join(",",current));
                                        if(pixels[i].a!=0)nonzero++;
                                    }
                                    Require(frame==3?nonzero==0:nonzero>0,"LOD vacuous/empty coverage");cases++;
                                }
                            }
                        }
                    }
                    finally {low.Release();full.Release();UnityEngine.Object.DestroyImmediate(low);UnityEngine.Object.DestroyImmediate(full);}
                }
            }
            Log("LOD_COMPOSITE=PASS cases="+cases+" rgba_max_error=0 projections=screen,texture divisors=2,4 sizes=even,odd filters=point,linear frames=moving,edge,empty,reappear partial_clear_dirty=magenta");
        }
        finally
        {
            RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=async;
            UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(quad);
            UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(clear);bundle.Unload(true);
        }
    }
}
#endif
