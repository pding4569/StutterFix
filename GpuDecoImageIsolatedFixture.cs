#if AUTOTEST
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Explicit paused command, no frame hook. Draw the actual source and
        // replacement separately with the actual camera matrices. No material,
        // transform, visibility or sorting state is changed on either object.
        static void RunGpuImageIsolatedFixture()
        {
            RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image isolated requires paused activation");
            RequireLive(gpuLiveShader.HasKernel("ImagePixelProbe"),"pixel probe bundle required");
            int kernel=gpuLiveShader.FindKernel("ImagePixelProbe"),samples=0,different=0,blank=0;long pixels=0;uint maximum=0;
            var previous=RenderTexture.active;var counters=new uint[4];
            var a=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);
            var b=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);
            try
            {
                using(var count=new ComputeBuffer(4,4))using(var commands=new CommandBuffer{name="GpuDeco isolated image boundary"})
                {
                    foreach(var slice in gpuLiveSlices)for(int i=0;i<slice.MasterItems.Length;i++)
                    {
                        int master=slice.MasterItems[i];if(GpuCpuFallback(master)||!GpuImageActor(gpuLiveOwnedIndices[master]))continue;
                        var d=(scrVisualDecoration)baselineObjects[gpuLiveOwnedIndices[master]];var source=d.spriteRenderer;
                        if(source.color.a<=0||!source.enabled||!source.gameObject.activeInHierarchy)continue;
                        foreach(var camera in Camera.allCameras)
                        {
                            if(!camera.enabled||!camera.targetTexture||(camera.cullingMask&(1<<source.gameObject.layer))==0)continue;
                            RequireLive(camera.targetTexture.width==a.width&&camera.targetTexture.height==a.height,"isolated fixture viewport changed");
                            var corners=source.sprite.vertices;var lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var hi=-lo;
                            for(int c=0;c<4;c++)
                            {
                                Vector3 local=corners[c];if(source.flipX)local.x=-local.x;if(source.flipY)local.y=-local.y;
                                Vector2 point=camera.WorldToScreenPoint(source.transform.TransformPoint(local));lo=Vector2.Min(lo,point);hi=Vector2.Max(hi,point);
                            }
                            int x=Mathf.Clamp(Mathf.FloorToInt(lo.x)-2,0,a.width),y=Mathf.Clamp(Mathf.FloorToInt(lo.y)-2,0,a.height);
                            int w=Mathf.Clamp(Mathf.CeilToInt(hi.x)+2,0,a.width)-x,h=Mathf.Clamp(Mathf.CeilToInt(hi.y)+2,0,a.height)-y;
                            if(w<=0||h<=0)continue;
                            void Draw(RenderTexture target,Renderer renderer)
                            {
                                commands.Clear();commands.SetRenderTarget(target);commands.ClearRenderTarget(false,true,Color.clear);
                                commands.SetViewport(new Rect(0,0,a.width,a.height));
                                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                                commands.DrawRenderer(renderer,renderer.sharedMaterial,0,0);Graphics.ExecuteCommandBuffer(commands);
                            }
                            Draw(a,source);Draw(b,slice.Renderers[i]);
                            Array.Clear(counters,0,counters.Length);count.SetData(counters);
                            gpuLiveShader.SetTexture(kernel,"_ImageDiffA",a);gpuLiveShader.SetTexture(kernel,"_ImageDiffB",b);
                            gpuLiveShader.SetBuffer(kernel,"_ImageDiff",count);gpuLiveShader.SetInts("_ImageDiffRect",x,y,w,h);
                            gpuLiveShader.Dispatch(kernel,(w*h+63)/64,1,1);count.GetData(counters);
                            if(counters[2]==0&&counters[3]==0){blank++;continue;}
                            RequireLive(counters[2]>0&&counters[3]>0,"isolated fixture one draw blank");samples++;pixels+=counters[0];maximum=Math.Max(maximum,counters[1]);
                            if(counters[0]>0)
                            {
                                different++;GpuLog("image_isolated_bad index="+gpuLiveOwnedIndices[master]+" pixels="+counters[0]+" max_RGBA="+counters[1]+" lit="+counters[2]+"/"+counters[3]+" angle="+d.rotAngle.ToString("R",Inv));
                            }
                        }
                    }
                }
                GpuLog("image_isolated samples="+samples+" different="+different+" pixels="+pixels+" max_RGBA="+maximum+" blank="+blank+" scope=separate_actual_renderers_pass0_camera_matrices performance=False");
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
    }
}
#endif
