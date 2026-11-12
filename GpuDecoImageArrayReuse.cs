#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        sealed class ImageArrayBank
        {
            internal ImageArrayLeasePlan Plan;
            internal Texture2DArray Texture;
            internal int Array;
            internal int[] Loaded;
            internal Texture2D[] Originals;
            internal Texture2DArray Reference;
            internal Dictionary<int,int> ReferenceSlices;
        }
        static readonly Dictionary<int,ImageArrayBank> gpuImageArrayBanks=new Dictionary<int,ImageArrayBank>();
        static long gpuImageArrayCopies,gpuImageArrayBytes;
        static bool gpuImageArrayReference;
        static Texture2DArray gpuImageArrayControl;
        internal static string ImageArrayReuseCommand(string arg)
        {
            if(arg.Trim().StartsWith("gpuonly ",StringComparison.Ordinal))
            {
                RequireLive(!gpuLivePrepared && !gpuLiveActive,"gpuonly must precede preparation");
                DrawCollapsePolicy.ImageArrayGpuOnly=arg.Trim()=="gpuonly on";
                return ImageArrayReuseCommand("state");
            }
#if DEV || AUTOTEST
            if(arg.Trim()=="audit")return AuditImageArrayPayload();
            if(arg.Trim()=="refresh") {RefreshImageArrayView();return ImageArrayView();}
            if(arg.Trim()=="view")return ImageArrayView();
            if(arg.Trim().StartsWith("control ",StringComparison.Ordinal))
            {SetImageArrayControl(arg.Trim()=="control on");return ImageArrayReuseCommand("state");}
            if(arg.Trim().StartsWith("reference ",StringComparison.Ordinal))
            {SwapImageArrayReference(arg.Trim()=="reference on");return ImageArrayReuseCommand("state");}
#endif
            if(arg.Trim()=="state")return "enabled="+DrawCollapsePolicy.ImageArrayReuse+" bank_textures="+gpuImageArrayBanks.Count
                +" copies="+gpuImageArrayCopies+" copied_payload_bytes="+gpuImageArrayBytes+" reference="+gpuImageArrayReference
                +" gpuonly="+DrawCollapsePolicy.ImageArrayGpuOnly+" readable_arrays="+gpuImageArrayBanks.Values.Distinct().Count(b=>b.Texture&&b.Texture.isReadable)
                +" readable_originals="+gpuImageArrayBanks.Values.Distinct().Sum(b=>b.Originals.Count(t=>t&&t.isReadable))+" control="+(bool)gpuImageArrayControl;
            RequireLive(!gpuLivePrepared && !gpuLiveActive,"imagearrayreuse must be selected before map preparation");
            DrawCollapsePolicy.ImageArrayReuse=arg.Trim()=="on";
            return ImageArrayReuseCommand("state");
        }
        static ImageArrayLeasePlan PlanImageArray(Texture2D[] group,GpuLiveSlice source)
        {
            if(!DrawCollapsePolicy.ImageArrayReuse)return null;
            int n=source.Data.Items.Length;
            var initial=new int[n];var variants=new int[n][];
            for(int i=0;i<n;i++)
            {
                initial[i]=source.Data.Items[i].TextureId;
                int owner=gpuLiveOwnedIndices[source.MasterItems[i]];
                if(!GpuImageActor(owner)) {variants[i]=Array.Empty<int>();continue;}
                RequireLive(gpuImageTimeline!=null && gpuImageVariants!=null,"image array missing timeline");
                var images=new HashSet<int>{initial[i]};
                foreach(int e in gpuImageTimeline.EventsForDecoration(owner))
                {
                    int selector=gpuImageTimeline.Event(e).Selector;
                    var v=gpuImageVariants[selector];
                    if(v.Status==GpuDecoImageVariant.Kind.ReadyQuad)images.Add(v.TextureId);
                }
                variants[i]=images.ToArray();
            }
            var p=new ImageArrayLeasePlan(group.Select(t=>t.GetInstanceID()).ToArray(),initial,variants);
            bool use=p.Slices>0 && p.Slices<group.Length;
            GpuLog("IMAGEARRAY_PLAN textures="+group.Length+" actor_slices="+p.ActorSlots.Count+" static_slices="+p.StaticSlots.Count
                +" proposed_slices="+p.Slices+" use="+use+" resolution_unchanged=True prediction=False eviction=False");
            return use?p:null;
        }
        static void CopyImageArraySlice(ImageArrayBank bank,int item,Texture2D texture)
        {
            int slice=bank.Plan.Resolve(item,texture.GetInstanceID());
            if(bank.Loaded[slice]==texture.GetInstanceID())return;
            RequireLive(bank.Texture && bank.Texture.width==texture.width && bank.Texture.height==texture.height
                && bank.Texture.graphicsFormat==texture.graphicsFormat && texture.mipmapCount==1,"image array lease format changed");
            // Called by the existing image-event path before its metadata
            // upload. Unity orders the GPU copy before the later draw; no CPU
            // readback, Flush or wait is introduced.
            Graphics.CopyTexture(texture,0,0,0,0,texture.width,texture.height,bank.Texture,slice,0,0,0);
            bank.Loaded[slice]=texture.GetInstanceID();gpuImageArrayCopies++;
            gpuImageArrayBytes+=CollapseArrayBytes(texture.width,texture.height,1,texture.graphicsFormat);
        }
        static Vector4 ResolveImageArrayAddress(int item,Texture2D texture,Vector4 address)
        {
            if(!gpuImageArrayBanks.TryGetValue(texture.GetInstanceID(),out var bank))return address;
            if(gpuImageArrayReference)
            {
                RequireLive(bank.Reference && bank.ReferenceSlices!=null && bank.ReferenceSlices.ContainsKey(texture.GetInstanceID()),"image reference missing");
                return new Vector4(bank.Array,bank.ReferenceSlices[texture.GetInstanceID()],1,1);
            }
            int slice=bank.Plan.Resolve(item,texture.GetInstanceID());
            CopyImageArraySlice(bank,item,texture);
            return new Vector4(bank.Array,slice,1,1);
        }
        static void ClearImageArrayBanks()
        {
            if(gpuImageArrayBanks.Count>0)GpuLog("IMAGEARRAY_RELEASE textures="+gpuImageArrayBanks.Count+" copies="+gpuImageArrayCopies+" copied_payload_bytes="+gpuImageArrayBytes);
            foreach(var bank in gpuImageArrayBanks.Values.Distinct())
            {
                if(bank.Reference && !gpuCollapseArrays.Contains(bank.Reference))UnityEngine.Object.Destroy(bank.Reference);
                if(bank.Texture && !gpuCollapseArrays.Contains(bank.Texture))UnityEngine.Object.Destroy(bank.Texture);
            }
            gpuImageArrayBanks.Clear();gpuImageArrayCopies=gpuImageArrayBytes=0;gpuImageArrayReference=false;
            if(gpuImageArrayControl)UnityEngine.Object.Destroy(gpuImageArrayControl);gpuImageArrayControl=null;
        }
#if DEV || AUTOTEST
        // Paused, explicitly requested GPU readback fixture. This has waits and
        // allocations and is excluded from performance measurements. Never read
        // the whole array; sample initialized actor leases plus one static slot.
        static string AuditImageArrayPayload()
        {
            RequireLive(FrameCompare.Paused && GpuPlaybackState.Paused && gpuLiveActive && gpuCollapseOn
                && !gpuImageArrayReference && !gpuImageArrayControl && SystemInfo.supportsAsyncGPUReadback,"image array audit requires paused GPU leases");
            long requested=0,compared=0,different=0;int samples=0,controls=0;
            const long budget=256L*1024*1024;
            byte[] Read(Texture texture)
            {
                long size=(long)texture.width*texture.height*4;
                RequireLive(requested+size<=budget,"image array audit readback budget");requested+=size;
                var request=UnityEngine.Rendering.AsyncGPUReadback.Request(texture,0);
                request.WaitForCompletion();RequireLive(!request.hasError,"image array audit GPU readback error");
                var data=request.GetData<byte>();RequireLive(data.Length==size,"image array audit byte layout");
                var bytes=new byte[data.Length];data.CopyTo(bytes);return bytes;
            }
            long Differences(byte[] a,byte[] b)
            {RequireLive(a.Length==b.Length,"image array audit length");long n=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])n++;return n;}
            foreach(var bank in gpuImageArrayBanks.Values.Distinct())
            {
                RequireLive(bank.Texture.graphicsFormat==UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,"image array audit supports exact RGBA8 UNorm only");
                var slots=new HashSet<int>(bank.Plan.ActorSlots.Values.Where(s=>bank.Loaded[s]!=0));
                foreach(int slot in bank.Plan.StaticSlots.Values)if(bank.Loaded[slot]!=0){slots.Add(slot);break;}
                long size=(long)bank.Texture.width*bank.Texture.height*4;
                RequireLive(slots.Count>0 && requested+size*(slots.Count*2L+2)<=budget,"image array audit fixture budget preflight");
                var scratch=new Texture2D(bank.Texture.width,bank.Texture.height,TextureFormat.RGBA32,false,true){name="Image array audit scratch"};
                Texture2D patch=null;
                try
                {
                    scratch.Apply(false,true);bool negativeDone=false;
                    foreach(int slot in slots)
                    {
                        int id=bank.Loaded[slot];var original=bank.Originals.First(t=>t.GetInstanceID()==id);
                        var expected=Read(original);
                        Graphics.CopyTexture(bank.Texture,slot,0,scratch,0,0);
                        var actual=Read(scratch);long delta=Differences(expected,actual);
                        different+=delta;compared+=expected.Length;samples++;
                        GpuLog("IMAGEARRAY_AUDIT array="+bank.Array+" slot="+slot+" texture="+id+" bytes="+expected.Length+" different_bytes="+delta+" timing_contaminated=True");
                        if(!negativeDone)
                        {
                            patch=new Texture2D(1,1,TextureFormat.RGBA32,false,true);
                            var pixel=new byte[4];for(int c=0;c<4;c++)pixel[c]=(byte)(expected[c]^255);
                            patch.LoadRawTextureData(pixel);patch.Apply(false,true);
                            Graphics.CopyTexture(patch,0,0,0,0,1,1,scratch,0,0,0,0);
                            long control=Differences(expected,Read(scratch));
                            Graphics.CopyTexture(bank.Texture,slot,0,scratch,0,0);
                            long restore=Differences(expected,Read(scratch));
                            RequireLive(control==4 && restore==0,"image array audit negative control/restore");
                            GpuLog("IMAGEARRAY_AUDIT_CONTROL array="+bank.Array+" different_bytes="+control+" restore_bytes="+restore+" live_textures_mutated=False");
                            controls++;negativeDone=true;
                        }
                    }
                }
                finally{if(patch)UnityEngine.Object.Destroy(patch);UnityEngine.Object.Destroy(scratch);}
            }
            RequireLive(samples>0 && different==0,"image array audit data mismatch");
            return "audit_samples="+samples+" compared_bytes="+compared+" different_bytes="+different+" negative_controls="+controls
                +" requested_bytes="+requested+" visible_scene_coverage=UNVERIFIED timing_contaminated=True";
        }
        static string ImageArrayView()
        {
            var c=ADOBase.controller?ADOBase.controller.camy:null;
            if(!c)return "camera missing";
            var field=HarmonyLib.AccessTools.Field(typeof(scrCamera),"camQuadMesh");
            var q=field?.GetValue(c) as MeshRenderer;
            var t=q&&q.sharedMaterial?q.sharedMaterial.mainTexture:null;
            var rt=c.camobj?c.camobj.targetTexture:null;
            return "custom_fps="+c.enableCustomFPS+" rate="+c.frameRate+" camera_enabled="+(c.camobj&&c.camobj.enabled)
                +" camera_target="+(rt?rt.GetInstanceID():0)+" display_texture="+(t?t.GetInstanceID():0)+" display_is_camera_target="+(t==rt);
        }
        static void RefreshImageArrayView()
        {
            RequireLive(FrameCompare.Paused && GpuPlaybackState.Paused && gpuLiveActive,"image refresh requires paused GPU");
            // Explicit diagnostic; refresh computed slice addresses first.
            DispatchGpuLiveSlice(BakeTime);BindImageArrayViews();
            var c=ADOBase.controller.camy;
            if(c.enableCustomFPS)
            {
                // Time.time is frozen while paused. Let the game's own custom
                // FPS rendering method update its displayed copy. Preserve its
                // schedule; do not disable any map filter or FPS effect.
                var clock=HarmonyLib.AccessTools.Field(typeof(scrCamera),"lastCustomFPSUpdate");
                var render=HarmonyLib.AccessTools.Method(typeof(scrCamera),"UpdateCustomFrameRateScreen",Type.EmptyTypes);
                RequireLive(clock!=null && clock.FieldType==typeof(double) && render!=null,"custom FPS refresh signature");
                object old=clock.GetValue(c);
                try{render.Invoke(c,null);}finally{clock.SetValue(c,old);}
            }
            GpuLog("IMAGEARRAY_VIEW "+ImageArrayView()+" timing_contaminated=True");
        }
        // Explicit paused diagnostic only. Recreates the old full arrays while
        // retaining exactly the same shaders, geometry, ordering and source
        // state. It must never run in a performance window.
        static void SwapImageArrayReference(bool on)
        {
            RequireLive(FrameCompare.Paused && GpuPlaybackState.Paused && gpuLiveActive && gpuCollapseOn
                && gpuImageArrayBanks.Count>0 && gpuLiveSlices.Count==1 && !gpuImageArrayControl,"image reference requires paused active leased arrays");
            if(on==gpuImageArrayReference)return;
            var banks=gpuImageArrayBanks.Values.Distinct().ToArray();
            if(on)
            {
                foreach(var bank in banks)
                {
                    var t=bank.Originals[0];
                    bank.Reference=new Texture2DArray(t.width,t.height,bank.Originals.Length,t.graphicsFormat,UnityEngine.Experimental.Rendering.TextureCreationFlags.None,1)
                        {name="GpuDeco reference array",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                    RequireLive(bank.Reference.graphicsFormat==t.graphicsFormat,"image reference format");
                    bank.ReferenceSlices=new Dictionary<int,int>();
                    for(int slice=0;slice<bank.Originals.Length;slice++)
                    {
                        var tex=bank.Originals[slice];
                        Graphics.CopyTexture(tex,0,0,0,0,tex.width,tex.height,bank.Reference,slice,0,0,0);
                        bank.ReferenceSlices.Add(tex.GetInstanceID(),slice);
                    }
                }
            }
            foreach(var bank in banks)gpuCollapseArrays[bank.Array]=on?bank.Reference:bank.Texture;
            gpuImageArrayReference=on;
            var slice0=gpuLiveSlices[0];
            // Preserve the exact live sampler/blend/repeat/shape metadata.
            // Only array slice changes; this explicit readback is diagnostic.
            var info=new Vector4[gpuCollapseTexture.count];gpuCollapseTexture.GetData(info);
            for(int i=0;i<slice0.Data.Items.Length;i++)
            {
                int texture=slice0.Data.Items[i].TextureId;
                if(!gpuImageArrayBanks.TryGetValue(texture,out var bank))continue;
                RequireLive(gpuImageCollapseRegisteredTextures.TryGetValue(texture,out var source),"image reference registry");
                RequireLive((((int)info[i*2].x)&127)==bank.Array,"image reference live binding mismatch");
                info[i*2].y=ResolveImageArrayAddress(i,source,gpuImageCollapseAddresses[texture]).y;
            }
            gpuCollapseTexture.SetData(info);BindImageArrayViews();
            if(!on)foreach(var bank in banks)
            {UnityEngine.Object.Destroy(bank.Reference);bank.Reference=null;bank.ReferenceSlices=null;}
            GpuLog("IMAGEARRAY_REFERENCE enabled="+on+" banks="+banks.Length+" timing_contaminated=True");
        }
        static void BindImageArrayViews()
        {
            var materials=new HashSet<Material>(gpuCollapseSharedMats.Values);
            foreach(var span in gpuCollapseSpans)
            {
                if(span.Material)materials.Add(span.Material);
                if(span.Renderer && span.Renderer.sharedMaterial)materials.Add(span.Renderer.sharedMaterial);
            }
            foreach(var material in materials)
                if(material && (material.shader==gpuCollapseShader || material.shader==gpuCollapseBlendShader))
                    foreach(var bank in gpuImageArrayBanks.Values.Distinct())
                        material.SetTexture("_SfDecoTex"+bank.Array,gpuImageArrayControl?gpuImageArrayControl:gpuCollapseArrays[bank.Array]);
            GpuLog("IMAGEARRAY_BIND materials="+materials.Count+" reference="+gpuImageArrayReference+" control="+(bool)gpuImageArrayControl+" timing_contaminated=True");
        }
        static void SetImageArrayControl(bool on)
        {
            RequireLive(FrameCompare.Paused && GpuPlaybackState.Paused && gpuLiveActive && gpuCollapseOn
                && gpuImageArrayBanks.Count>0 && !gpuImageArrayReference,"image array control requires paused leases");
            if(on)
            {
                if(gpuImageArrayControl)return;
                int depth=gpuImageArrayBanks.Values.Max(b=>b.Plan.Slices);
                gpuImageArrayControl=new Texture2DArray(1,1,depth,TextureFormat.RGBA32,false,true){name="GpuDeco negative control",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var zero=new Color32[1];
                for(int i=0;i<depth;i++)gpuImageArrayControl.SetPixels32(zero,i,0);
                gpuImageArrayControl.Apply(false,true);BindImageArrayViews();
            }
            else
            {
                var old=gpuImageArrayControl;gpuImageArrayControl=null;BindImageArrayViews();
                if(old)UnityEngine.Object.Destroy(old);
            }
        }
#endif
    }
}
#endif
