#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        sealed class GpuStorageBackup
        {
            internal GpuLiveSlice Slice;
            internal MeshRenderer[] Renderers;
            internal Mesh[] Meshes;
            internal GraphicsBuffer[] Buffers;
            internal GpuDecoCopy Copy;
            internal GpuDecoCopyMask Mask;
        }
        static GpuStorageBackup gpuStorageBackup;
        static bool GpuStorageCommand(string command)
        {
            if(!command.StartsWith("storage ",StringComparison.Ordinal))return false;
            string mode=command.Substring(8);
            RequireLive(mode=="shared"||mode=="sharedraw"||mode=="sharedbuckets"||mode=="independent"||mode=="state","storage shared|sharedraw|sharedbuckets|independent|state");
            if(mode=="state")
            {
                GpuLog("storage shared="+(gpuStorageBackup!=null)+" active="+gpuLiveActive+" common_live_channels=True default_off=True");return true;
            }
            RequireLive(gpuLivePrepared&&FrameCompare.Paused&&g2Window<0,"storage swap requires paused prepared ownership outside performance windows");
            if(mode=="independent") {ResetGpuStorage();return true;}
            RequireLive(gpuStorageBackup==null&&gpuLiveSlices.Count==1,"storage swap requires one independent slice");
            RequireLive(!GpuDecoCopy.AblateDestinations&&gpuAblationDrawUndo.Count==0&&gpuLiveRoot&&gpuLiveRoot.activeSelf,"storage comparison cannot use an ablated scene");
            var s=gpuLiveSlices[0];
            RequireLive(s.Independent&&s.SharedBatch==null&&s.NativeCopy!=null,"independent native backend required");
            // Freeze source identity: no prepare, CPU resume, source recapture,
            // event recompile or new clock. Readback occurs only while paused.
            if(gpuLiveActive){DispatchGpuLiveSlice(BakeTime);gpuLiveLastDispatchFrame=Time.frameCount;}
            else
            {
                s.Gpu.Bind();var camera=s.Data.Camera;
                s.Gpu.SetLiveUniforms(new Vector4(camera[0],camera[1],camera[2],camera[3]),s.Data.BakeTime);
                s.Gpu.DispatchEvaluate();
            }
            var vertices=new GpuDecoGpu.Vertex[s.Data.Items.Length*4];s.Gpu.VertexBuffer.GetData(vertices);
            var originals=s.Data.Items.Select(x=>((scrVisualDecoration)baselineObjects[x.Index]).spriteRenderer).ToArray();
            var backup=new GpuStorageBackup{Slice=s,Renderers=s.Renderers.ToArray(),Meshes=s.Meshes.ToArray(),Buffers=s.Buffers.ToArray(),Copy=s.NativeCopy,Mask=s.CopyMask};
            var batch=GpuDecoNativeBatch.Build(gpuLiveRoot.transform,s.Data,vertices,originals,gpuLiveShader,s.Gpu.VertexBuffer,backup.Renderers,mode=="sharedraw",mode=="sharedbuckets");
            try
            {
                s.SetEnabled(false);gpuStorageBackup=backup;
                s.SharedBatch=batch;
                s.Renderers.Clear();s.Renderers.AddRange(batch.LiveRenderers);
                s.Meshes.Clear();s.Meshes.AddRange(batch.LiveMeshes);s.Buffers.Clear();
                s.CopyMask=null;s.NativeCopy=null;
                if(gpuLiveActive){s.SetEnabled(true);DispatchGpuLiveSlice(BakeTime);gpuLiveLastDispatchFrame=Time.frameCount;}
                GpuLog("storage swapped=shared renderers="+s.Renderers.Count+" buffers="+batch.BufferCount+" same_data=True same_channels=True same_clocks=True exact_old_renderer_state=True raw_color="+(mode=="sharedraw")+" split_sort_buckets="+(mode=="sharedbuckets")+" performance=False");
            }
            catch
            {
                if(gpuStorageBackup!=null)ResetGpuStorage();else batch.Dispose();
                throw;
            }
            return true;
        }
        static void ResetGpuStorage(bool redraw=true)
        {
            var b=gpuStorageBackup;if(b==null)return;
            gpuStorageBackup=null;var s=b.Slice;
            s.SetEnabled(false);var batch=s.SharedBatch;s.SharedBatch=null;
            s.Renderers.Clear();s.Renderers.AddRange(b.Renderers);
            s.Meshes.Clear();s.Meshes.AddRange(b.Meshes);
            s.Buffers.Clear();s.Buffers.AddRange(b.Buffers);
            s.NativeCopy=b.Copy;s.CopyMask=b.Mask;
            try {batch?.Dispose();}
            finally
            {
                if(gpuLiveActive&&redraw)
                {
                    s.SetEnabled(true);DispatchGpuLiveSlice(BakeTime);gpuLiveLastDispatchFrame=Time.frameCount;
                }
            }
            GpuLog("storage restored=independent common_live_channels=True");
        }
        static void MarkGpuStorageBackup(Action<UnityEngine.Object> add)
        {
            var b=gpuStorageBackup;if(b==null)return;
            foreach(var m in b.Meshes)add(m);
            foreach(var r in b.Renderers)if(r)add(r.gameObject);
        }
    }
}
#endif
