#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static bool gpuCollapsePrepare,gpuCollapseOn;
        static string gpuCollapseFault;
        static readonly List<CollapseSpan> gpuCollapseSpans=new List<CollapseSpan>();
        static readonly List<Texture2DArray> gpuCollapseArrays=new List<Texture2DArray>();
        static readonly List<Texture2D> gpuCollapseDirect=new List<Texture2D>(); // Borrowed, never destroyed.
        static readonly List<GpuLiveRow> gpuCollapseBarriers=new List<GpuLiveRow>();
        static readonly List<GpuLiveRow> gpuCollapseAuxiliaries=new List<GpuLiveRow>();
        static ComputeBuffer gpuCollapseTexture;
        static bool[] gpuCollapseIncluded;
        static GpuLiveRow[] gpuCollapseOwnedRows;
        static Vector3 gpuCollapseCameraPosition,gpuCollapseCameraForward;
        sealed class CollapseSpan : IDisposable
        {
            internal int[] Items;
            internal Mesh Mesh;
            internal MeshRenderer Renderer;
            internal Material Material;
            internal GraphicsBuffer Vertices;
            internal ComputeBuffer Map;
            internal SceneRenderOrder.Key First,Last;
            public void Dispose()
            {
                if(Renderer){Renderer.enabled=false;UnityEngine.Object.Destroy(Renderer.gameObject);}
                Vertices?.Dispose();Map?.Dispose();
                if(Mesh)UnityEngine.Object.Destroy(Mesh);
                if(Material)UnityEngine.Object.Destroy(Material);
            }
        }
        static void HideDrawCollapse()
        {
            gpuCollapseOn=false;
            foreach(var s in gpuCollapseSpans)if(s.Renderer)s.Renderer.enabled=false;
        }
        static void DisposeDrawCollapse()
        {
            HideDrawCollapse();
            foreach(var s in gpuCollapseSpans)s.Dispose();gpuCollapseSpans.Clear();
            gpuCollapseTexture?.Dispose();gpuCollapseTexture=null;
            foreach(var a in gpuCollapseArrays)if(a)UnityEngine.Object.Destroy(a);
            gpuCollapseArrays.Clear();gpuCollapseBarriers.Clear();gpuCollapseAuxiliaries.Clear();
            gpuCollapseDirect.Clear();
            gpuCollapseIncluded=null;gpuCollapseOwnedRows=null;
        }
        static int CollapseSize(Texture2D t)=>DrawCollapsePolicy.SmallDxtTier(t.width,t.height,
            t.graphicsFormat==GraphicsFormat.RGBA_DXT5_UNorm || t.graphicsFormat==GraphicsFormat.RGBA_DXT5_SRGB,t.mipmapCount);
        static string CollapseTier(Texture2D t)=>CollapseSize(t)+"/"+t.graphicsFormat;
        static void ValidateDrawCollapseItem(int item)
        {
            if(gpuCollapseIncluded!=null && !gpuCollapseIncluded[item])
                RequireLive(MergeHidden(item),"drawcollapse visible item outside prepared window item="+item);
        }
        static void PrepareDrawCollapse(List<GpuLiveRow> masterRows)
        {
            if(!gpuCollapsePrepare)return;
            RequireLive(!gpuRenderMergePrepare && gpuScope.WholeSong && gpuLiveSlices.Count==1
                && gpuLiveSlices[0].Independent && FrameCompare.Paused,"drawcollapse needs independent paused song preparation");
            RequireLive(gpuLiveShader.HasKernel("EvaluateCollapse"),"drawcollapse bundle missing EvaluateCollapse");
            var sortMode=gpuLiveCamera.transparencySortMode;
            if(sortMode==TransparencySortMode.Default)sortMode=GraphicsSettings.transparencySortMode;
            RequireLive(gpuLiveCamera.orthographic && (sortMode==TransparencySortMode.Default || sortMode==TransparencySortMode.Orthographic),
                "drawcollapse requires orthographic camera sorting");
            RequireLive(UnityEngine.Object.FindObjectsByType<SortingGroup>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .All(g=>!g.enabled || !g.gameObject.activeInHierarchy),"drawcollapse unsupported active SortingGroup");
            RequireLive(UnityEngine.Object.FindObjectsByType<SpriteMask>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .All(m=>!m.enabled || !m.gameObject.activeInHierarchy),"drawcollapse unsupported active SpriteMask");
            var shader=gpuLiveBundle.LoadAsset<Shader>("Assets/Shaders/GpuDecoBatch.shader");
            RequireLive(shader && shader.isSupported,"drawcollapse bundle missing/unsupported GpuDecoBatch.shader");
            var source=gpuLiveSlices[0];
            gpuCollapseCameraPosition=gpuLiveCamera.transform.position;gpuCollapseCameraForward=gpuLiveCamera.transform.forward;
            var local=source.Data.Items.Select((x,i)=>new{ x.Index, Item=i }).ToDictionary(x=>x.Index,x=>x.Item);
            gpuCollapseIncluded=new bool[source.Data.Items.Length];
            gpuCollapseOwnedRows=new GpuLiveRow[source.Data.Items.Length];
            var rows=new List<DrawCollapsePolicy.Row>();
            foreach(var row in masterRows)
            {
                if(row.AuxiliaryOwner>=0)
                {
                    RequireLive(!row.R.enabled || !row.R.gameObject.activeInHierarchy || row.R.forceRenderingOff,
                        "drawcollapse admitted auxiliary is visible");
                    gpuCollapseAuxiliaries.Add(row);continue;
                }
                if(row.Item<0)
                {
                    // Keep potential CPU barriers even when currently invisible.
                    // They may wake during the ten-second measurement window.
                    gpuCollapseBarriers.Add(row);
                    rows.Add(new DrawCollapsePolicy.Row(row.Key,false,row.R.GetInstanceID()));continue;
                }
                int i=local[row.Item];gpuCollapseOwnedRows[i]=row;
                if(!VisibleInWindow(table,row.Item,259.75,270.25))continue;
                gpuCollapseIncluded[i]=true;
                rows.Add(new DrawCollapsePolicy.Row(row.Key,true,i));
            }
            var plan=DrawCollapsePolicy.Plan(rows);
            RequireLive(plan.Count>0 && plan.Count<=150,"drawcollapse conservative prepared spans="+plan.Count+" exceeds renderer budget");
            var textures=new Dictionary<int,Texture2D>();
            var info=new Vector4[source.Data.Items.Length*2];
            for(int i=0;i<source.Data.Items.Length;i++)
            {
                var row=gpuCollapseOwnedRows[i];var sprite=row?.R as SpriteRenderer;
                RequireLive(sprite && sprite.sprite && row.M && row.M.shader.name=="Sprites/Tile"
                    && sprite.maskInteraction==SpriteMaskInteraction.None,"drawcollapse unsupported owned material/mask");
                RequireLive(row.M.shaderKeywords.All(k=>k=="USE_POINT_SAMPLING"),"drawcollapse unsupported shader keywords: "+string.Join(",",row.M.shaderKeywords));
                RequireLive((!row.M.HasProperty("_Color") || row.M.GetColor("_Color")==Color.white)
                    && (!row.M.HasProperty("_Opacity") || row.M.GetFloat("_Opacity")==1),
                    "drawcollapse unsupported material tint/opacity");
                var t=sprite.sprite.texture;
                RequireLive(t && t.GetInstanceID()==source.Data.Items[i].TextureId && t.mipmapCount==1
                    && t.wrapModeU==TextureWrapMode.Clamp && t.wrapModeV==TextureWrapMode.Clamp,
                    "drawcollapse texture changed or unsupported mip/wrap");
                textures[t.GetInstanceID()]=t;
            }
            RequireLive(SystemInfo.supports2DArrayTextures && (SystemInfo.copyTextureSupport&CopyTextureSupport.DifferentTypes)!=0,
                "drawcollapse Texture2D-to-array copy unsupported");
            var addresses=new Dictionary<int,Vector4>();long total=0;int copies=0;
            var small=textures.Values.Where(t=>CollapseSize(t)>0).ToArray();
            var direct=textures.Values.Where(t=>CollapseSize(t)==0).ToArray();
            int arrays=0;
            foreach(var tier in small.GroupBy(CollapseTier))
            {
                var group=tier.ToArray();int size=CollapseSize(group[0]);
                total=checked(total+(long)size*size*group.Length);
                RequireLive(SystemInfo.maxTextureArraySlices>0,"drawcollapse texture array slices unavailable");
                arrays+=(group.Length+SystemInfo.maxTextureArraySlices-1)/SystemInfo.maxTextureArraySlices;
            }
            GpuLog("DRAWCOLLAPSE_ARRAY_TOTAL layout=small_dxt5_tiers_plus_borrowed_2d bytes_known="+total+" total_mb="+(total/1048576.0).ToString("F6",Inv)
                +" planned_arrays_known="+arrays+" direct_textures="+direct.Length+" active_texture_slots="+(arrays+direct.Length)
                +" shader_declared_slots=70 samplers=2 arrays_allocated=0 copy_calls=0 free_vram=UNVERIFIED");
            RequireLive(SystemInfo.graphicsMemorySize>0 && total<(long)SystemInfo.graphicsMemorySize*1048576,
                "drawcollapse array budget exceeds total VRAM"); // Free VRAM still needs external measurement.
            RequireLive(arrays<=6 && direct.Length<=64,"drawcollapse texture binding budget exceeds 6 arrays + 64 borrowed 2D");
            foreach(var tier in small.GroupBy(CollapseTier))
            {
                var group=tier.ToArray();var t=group[0];
                for(int start=0;start<group.Length;start+=SystemInfo.maxTextureArraySlices)
                {
                    RequireLive(SystemInfo.maxTextureArraySlices>0 && gpuCollapseArrays.Count<6,"drawcollapse exceeds six array bindings");
                    int depth=Math.Min(SystemInfo.maxTextureArraySlices,group.Length-start),array=gpuCollapseArrays.Count;
                    int size=CollapseSize(t);
                    var a=new Texture2DArray(size,size,depth,t.graphicsFormat,TextureCreationFlags.None,1)
                        {name="GpuDeco collapse tier "+array,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                    gpuCollapseArrays.Add(a);
                    RequireLive(a.graphicsFormat==t.graphicsFormat,"drawcollapse array format conversion rejected");
                    // No Blit/Apply/CPU readback/decompression or sRGB conversion.
                    for(int slice=0;slice<depth;slice++)
                    {
                        var tex=group[start+slice];addresses.Add(tex.GetInstanceID(),new Vector4(array,slice,tex.width/(float)size,tex.height/(float)size));
                        Graphics.CopyTexture(tex,0,0,0,0,tex.width,tex.height,a,slice,0,0,0);copies++;
                    }
                }
            }
            foreach(var t in direct)
            {
                addresses.Add(t.GetInstanceID(),new Vector4(6+gpuCollapseDirect.Count,0,1,1));gpuCollapseDirect.Add(t);
            }
            for(int i=0;i<source.Data.Items.Length;i++)
            {
                var address=addresses[source.Data.Items[i].TextureId];
                var t=textures[source.Data.Items[i].TextureId];
                info[i*2]=new Vector4(address.x+(gpuCollapseOwnedRows[i].M.IsKeywordEnabled("USE_POINT_SAMPLING")?128:0),address.y,0,0);
                info[i*2+1]=new Vector4(address.z,address.w,address.z/t.width*.5f,address.w/t.height*.5f);
            }
            gpuCollapseTexture=new ComputeBuffer(info.Length,16);gpuCollapseTexture.SetData(info);
            foreach(var items in plan)
            {
                var first=gpuCollapseOwnedRows[items[0]];var last=gpuCollapseOwnedRows[items[items.Length-1]];
                var s=new CollapseSpan{Items=items,First=first.Key,Last=last.Key};gpuCollapseSpans.Add(s);
                RequireLive(items.All(i=>gpuCollapseOwnedRows[i].R.gameObject.layer==first.R.gameObject.layer),
                    "drawcollapse span crosses camera culling layers");
                s.Material=new Material(shader){name="GpuDeco collapse span",renderQueue=first.M.renderQueue};
                foreach(string p in new[]{"_SrcBlend","_DstBlend","_BlendOp"})
                {
                    float expected=first.M.HasProperty(p)?first.M.GetFloat(p):(p=="_SrcBlend"?1:p=="_DstBlend"?10:0);
                    RequireLive(items.All(i=>!gpuCollapseOwnedRows[i].M.HasProperty(p) || gpuCollapseOwnedRows[i].M.GetFloat(p)==expected),
                        "drawcollapse incompatible blend states "+p);
                    s.Material.SetFloat(p,expected);
                }
                for(int a=0;a<6 && gpuCollapseArrays.Count>0;a++)s.Material.SetTexture("_Tex"+a,gpuCollapseArrays[Math.Min(a,gpuCollapseArrays.Count-1)]);
                for(int a=0;a<64 && gpuCollapseDirect.Count>0;a++)s.Material.SetTexture("_Big"+a,gpuCollapseDirect[Math.Min(a,gpuCollapseDirect.Count-1)]);
                s.Mesh=new Mesh{name="GpuDeco collapse span",indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw};
                s.Mesh.SetVertexBufferParams(items.Length*4,
                    new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4));
                s.Mesh.SetVertexBufferData(new float[items.Length*60],0,0,items.Length*60);
                var indices=new int[items.Length*6];
                for(int n=0;n<items.Length;n++)for(int t=0;t<6;t++)indices[n*6+t]=n*4+source.Data.Items[items[n]].Triangles[t];
                s.Mesh.subMeshCount=1;s.Mesh.SetIndices(indices,MeshTopology.Triangles,0,false);
                s.Mesh.bounds=source.Meshes[items[0]].bounds;s.Mesh.UploadMeshData(true);
                s.Vertices=s.Mesh.GetVertexBuffer(0);
                RequireLive(s.Mesh.GetVertexBufferStride(0)==60 && s.Mesh.subMeshCount==1,"drawcollapse raw mesh layout");
                s.Map=new ComputeBuffer(items.Length,4);s.Map.SetData(items.Select(i=>(uint)i).ToArray());
                var go=new GameObject("GpuDeco collapse span"){hideFlags=HideFlags.HideAndDontSave,layer=first.R.gameObject.layer};
                go.transform.SetParent(gpuLiveRoot.transform,false);go.AddComponent<MeshFilter>().sharedMesh=s.Mesh;
                s.Renderer=go.AddComponent<MeshRenderer>();s.Renderer.enabled=false;s.Renderer.sharedMaterial=s.Material;
                s.Renderer.sortingLayerID=first.R.sortingLayerID;s.Renderer.sortingOrder=first.R.sortingOrder;
                s.Renderer.shadowCastingMode=ShadowCastingMode.Off;s.Renderer.receiveShadows=false;
                s.Renderer.lightProbeUsage=LightProbeUsage.Off;s.Renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                RequireLive(s.Renderer.sharedMaterials.Length==1,"drawcollapse requires one material");
            }
            GpuLog("DRAWCOLLAPSE_PREPARED spans="+plan.Count+" included="+gpuCollapseIncluded.Count(x=>x)
                +" arrays="+gpuCollapseArrays.Count+" bytes="+total+" copy_calls="+copies
                +" submeshes_per_renderer=1 direct_evaluate=True window=260..270 conservative_hidden_barriers=True free_vram=UNVERIFIED");
        }
        static void ValidateDrawCollapse(bool full=true)
        {
            RequireDrawCollapse();
            RequireLive(gpuCollapseSpans.Count>0 && gpuCollapseTexture!=null,"drawcollapse prepare before live prepare required");
            RequireLive(gpuLiveCpuFallbackCount==0,"drawcollapse requires zero CPU fallback; disable before fallback");
            RequireLive(gpuLiveCamera.transform.position.z==gpuCollapseCameraPosition.z
                && gpuLiveCamera.transform.forward==gpuCollapseCameraForward,"drawcollapse camera sort axis changed");
            // Check all omissions at entry, then alpha dirty events check only
            // changed items. Never scan 23,547 channels each playback frame.
            if(full)for(int i=0;i<gpuCollapseIncluded.Length;i++)ValidateDrawCollapseItem(i);
            foreach(var r in gpuCollapseAuxiliaries)
                RequireLive(!r.R || !r.R.enabled || !r.R.gameObject.activeInHierarchy || r.R.forceRenderingOff,
                    "drawcollapse omitted auxiliary became visible");
            foreach(var b in gpuCollapseBarriers)
            {
                RequireLive(b.R && b.M && b.Key.SameBucket(MatShare.SortKey(b.R,b.M,gpuLiveCamera)),
                    "drawcollapse potential barrier sort key changed");
            }
            if(!full)return;
            // Representative material IDs differ from original IDs. Check the
            // actual proxy against EVERY potential barrier, including equal-key ties.
            foreach(var s in gpuCollapseSpans)
            {
                var proxy=MatShare.SortKey(s.Renderer,s.Material,gpuLiveCamera);
                foreach(var b in gpuCollapseBarriers)
                {
                    RequireLive(b.R && b.M,"drawcollapse barrier destroyed");
                    var key=MatShare.SortKey(b.R,b.M,gpuLiveCamera);
                    bool before=key.CompareTo(s.First)<0,after=key.CompareTo(s.Last)>0;
                    RequireLive((before && key.CompareTo(proxy)<0) || (after && key.CompareTo(proxy)>0),
                        "drawcollapse proxy/barrier order changed renderer="+b.R.GetInstanceID());
                }
            }
        }
        static void DispatchDrawCollapse(double time)
        {
            RequireLive(time>=259.75 && time<=270.25,"drawcollapse outside prepared 260..270 window");
            ValidateDrawCollapse(false);var source=gpuLiveSlices[0];
            foreach(var s in gpuCollapseSpans)
            {source.Gpu.DispatchCollapse(s.Vertices,s.Map,gpuCollapseTexture,s.Items.Length);s.Renderer.enabled=true;}
        }
    }
}
#endif
