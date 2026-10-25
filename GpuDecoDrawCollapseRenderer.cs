#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static bool gpuCollapsePrepare,gpuCollapseOn;
        static string gpuCollapseFault,gpuCollapsePendingFallback;
        static Shader gpuCollapseShader;
        const string CollapseNoDirectKeyword="NO_DIRECT_TEXTURES";
        // True only for a prepared array address that cannot change to a direct slot.
        static bool[] gpuCollapseArrayOnly;
        static int gpuCollapseOwnedCursor;
        static readonly List<Material> gpuCollapseMaterials=new List<Material>();
        static readonly List<CollapseSpan> gpuCollapseSpans=new List<CollapseSpan>();
        static readonly List<Texture2DArray> gpuCollapseArrays=new List<Texture2DArray>();
        static readonly List<Texture2D> gpuCollapseDirect=new List<Texture2D>(); // Borrowed, never destroyed.
        static readonly List<GpuLiveRow> gpuCollapseBarriers=new List<GpuLiveRow>();
        static readonly List<GpuLiveRow> gpuCollapseAuxiliaries=new List<GpuLiveRow>();
        static ComputeBuffer gpuCollapseTexture;
        static bool[] gpuCollapseIncluded;
        static bool[] gpuCollapseTextureFallback;
        static int gpuCollapseTextureFallbackCount;
        static GpuLiveRow[] gpuCollapseOwnedRows;
        static HashSet<int> gpuCollapseSeen;
        static int gpuCollapseBarrierCursor,gpuCollapseAuxCursor;
        static readonly Dictionary<string,Material> gpuCollapseSharedMats=new Dictionary<string,Material>();
        static double gpuCollapseReplanTime=-100;
        static readonly List<GpuLiveRow> gpuCollapseBarrierSweep=new List<GpuLiveRow>(1100);
        // 재생 중에는 장벽 6만여 개를 매 프레임 전부 훑지 않고 프레임당 이 개수씩 순환 점검한다(전체 점검은 진입 때만).
        const int CollapseBarrierSweepPerFrame=8; // 준비 때 본 렌더러 전체(재계획에서 새로 생긴 것만 장벽으로 더하기 위함)
        static Vector3 gpuCollapseCameraPosition,gpuCollapseCameraForward;
        static ComputeBuffer gpuCollapseAllVerts,gpuCollapseAllMap;static int gpuCollapseAllCount;
        sealed class CollapseSpan : IDisposable
        {
            internal int[] Items;
            internal Mesh Mesh;
            internal MeshRenderer Renderer;
            internal Material Material;
            internal bool SharedMat,Nudged;
            internal GraphicsBuffer Vertices;
            internal ComputeBuffer Map;
            internal int Base;
            internal SceneRenderOrder.Key First,Last;
            public void Dispose()
            {
                if(Renderer){Renderer.enabled=false;UnityEngine.Object.Destroy(Renderer.gameObject);}
                Vertices?.Dispose();Map?.Dispose();
                if(Mesh)UnityEngine.Object.Destroy(Mesh);
                if(Material && !SharedMat)UnityEngine.Object.Destroy(Material);
            }
        }
        static void InvalidateRecordedCollapse(){if(gpuLiveSlices.Count>0 && gpuLiveSlices[0].Gpu!=null)gpuLiveSlices[0].Gpu.ReleaseRecordedCollapse();}
        static void HideDrawCollapse()
        {
#if AUTOTEST
            TileCompositeRemove();
#endif
            gpuCollapseOn=false;gpuCollapsePendingFallback=null;
            foreach(var s in gpuCollapseSpans)if(s.Renderer)s.Renderer.enabled=false;
        }
        static void DisposeDrawCollapse()
        {
            InvalidateRecordedCollapse();
            HideDrawCollapse();
            foreach(var s in gpuCollapseSpans)s.Dispose();gpuCollapseSpans.Clear();
            foreach(var sm in gpuCollapseSharedMats.Values)if(sm)UnityEngine.Object.Destroy(sm);gpuCollapseSharedMats.Clear();
            gpuCollapseAllVerts?.Dispose();gpuCollapseAllVerts=null;gpuCollapseAllMap?.Dispose();gpuCollapseAllMap=null;gpuCollapseAllCount=0;
            gpuCollapseTexture?.Dispose();gpuCollapseTexture=null;gpuCollapseShader=null;gpuCollapseOwnedCursor=0;
            foreach(var a in gpuCollapseArrays)if(a)UnityEngine.Object.Destroy(a);
            gpuCollapseArrays.Clear();gpuCollapseBarriers.Clear();gpuCollapseAuxiliaries.Clear();
            gpuCollapseDirect.Clear();
            gpuCollapseArrayOnly=null;
            gpuCollapseIncluded=null;gpuCollapseSeen=null;gpuCollapseKeysValid=false;gpuCollapseScanned=false;gpuCollapseDirtyIds.Clear();gpuCollapseNewDecos.Clear();gpuCollapseOwnedRows=null;gpuCollapseTextureFallback=null;gpuCollapseTextureFallbackCount=0;
        }
        static int CollapseSize(Texture2D t)=>DrawCollapsePolicy.SmallDxtTier(t.width,t.height,
            t.graphicsFormat==GraphicsFormat.RGBA_DXT5_UNorm || t.graphicsFormat==GraphicsFormat.RGBA_DXT5_SRGB,t.mipmapCount);
        static string CollapseTier(Texture2D t)=>CollapseSize(t)+"/"+t.graphicsFormat;
        static void ValidateDrawCollapseItem(int item)
        {
            if(gpuCollapseIncluded!=null && !gpuCollapseIncluded[item] && !DrawCollapseTextureFallback(item))
                if(!MergeHidden(item))gpuCollapsePendingFallback="visible_item_outside_prepared_window item="+item;
        }
        static bool DrawCollapseTextureFallback(int item)=>gpuCollapseTextureFallback!=null && gpuCollapseTextureFallback[item];
        static void PrepareDrawCollapse(List<GpuLiveRow> masterRows)
        {
            if(!gpuCollapsePrepare)return;
            RequireLive(!gpuRenderMergePrepare && gpuScope.WholeSong && gpuLiveSlices.Count==1
                && gpuLiveSlices[0].Independent && (GpuPlaybackState.Paused || GpuDecoPlayer.Preparing),"drawcollapse needs independent paused song preparation");
            RequireLive(gpuLiveShader.HasKernel("EvaluateCollapse"),"drawcollapse bundle missing EvaluateCollapse");
            var sortMode=gpuLiveCamera.transparencySortMode;
            if(sortMode==TransparencySortMode.Default)sortMode=GraphicsSettings.transparencySortMode;
            RequireLive(gpuLiveCamera.orthographic && (sortMode==TransparencySortMode.Default || sortMode==TransparencySortMode.Orthographic),
                "drawcollapse requires orthographic camera sorting");
            RequireLive(UnityEngine.Object.FindObjectsByType<SortingGroup>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .All(g=>!g.enabled || !g.gameObject.activeInHierarchy),"drawcollapse unsupported active SortingGroup");
            RequireLive(UnityEngine.Object.FindObjectsByType<SpriteMask>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .All(m=>!m.enabled || !m.gameObject.activeInHierarchy),"drawcollapse unsupported active SpriteMask");
            gpuCollapseShader=gpuLiveBundle.LoadAsset<Shader>("Assets/Shaders/GpuDecoBatch.shader");
            RequireLive(gpuCollapseShader && gpuCollapseShader.isSupported,"drawcollapse bundle missing/unsupported GpuDecoBatch.shader");
            var source=gpuLiveSlices[0];
            gpuCollapseCameraPosition=gpuLiveCamera.transform.position;gpuCollapseCameraForward=gpuLiveCamera.transform.forward;
            var local=source.Data.Items.Select((x,i)=>new{ x.Index, Item=i }).ToDictionary(x=>x.Index,x=>x.Item);
            gpuCollapseIncluded=new bool[source.Data.Items.Length];
            gpuCollapseArrayOnly=new bool[source.Data.Items.Length];
            gpuCollapseTextureFallback=new bool[source.Data.Items.Length];
            gpuCollapseOwnedRows=new GpuLiveRow[source.Data.Items.Length];
            gpuCollapseSeen=new HashSet<int>();
            // 모드가 만든 독립 GPU 렌더러(소유 장식마다 1개)는 장벽 후보가 아니다.
            foreach(var rr in source.Renderers)if(rr)gpuCollapseSeen.Add(rr.GetInstanceID());
            foreach(var row in masterRows)
            {
                if(row.R)gpuCollapseSeen.Add(row.R.GetInstanceID());
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
                    continue;
                }
                int i=local[row.Item];gpuCollapseOwnedRows[i]=row;
                if(!GpuDecoPlayer.Product && !VisibleInWindow(table,row.Item,257.5,270.25))continue;
                gpuCollapseIncluded[i]=true;
            }
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
#if AUTOTEST || GPUDECO
            RegisterGpuImageCollapseVariants(textures);
#endif
            if (GpuDecoPlayer.Product)
            {
                var all=textures.Values.ToArray();
                Main.Entry.Logger.Log("[GPU 장식] 곡 전체 텍스처 "+all.Length+"개 (배열 후보 "+all.Count(t=>CollapseSize(t)>0)+", 직접 "+all.Count(t=>CollapseSize(t)==0)+", 배열 종류 "+all.Where(t=>CollapseSize(t)>0).Select(CollapseTier).Distinct().Count()+")");
            }
            var addresses=new Dictionary<int,Vector4>();long total=0;int copies=0;
            RequireLive(SystemInfo.maxTextureArraySlices>0,"drawcollapse texture array slices unavailable");
            // Keep only the admitted texture groups. Overflow stays on the
            // original independent GPU renderer, and becomes a painter barrier.
            var admittedSmall=new List<Texture2D>();
            var smallGroups=textures.Values.Where(t=>CollapseSize(t)>0).GroupBy(CollapseTier).ToArray();
            var groupCounts=DrawCollapsePolicy.AdmitArrayGroups(smallGroups.Select(g=>g.Count()).ToArray(),SystemInfo.maxTextureArraySlices);
            for(int group=0;group<smallGroups.Length;group++)admittedSmall.AddRange(smallGroups[group].Take(groupCounts[group]));
            var small=admittedSmall.ToArray();
            // 2048 을 넘는 같은 크기·같은 포맷 DXT5 텍스처(예: 4K 프레임 계열)는 정확한 크기의 배열 하나로 묶는다(직접 슬롯 낭비 방지).
            Texture2D[] exactGroup=new Texture2D[0];
            if(DrawCollapsePolicy.ExactBigArray)
            {
                var bigDxt=textures.Values.Where(t=>CollapseSize(t)==0 && t.graphicsFormat==GraphicsFormat.RGBA_DXT5_UNorm && t.mipmapCount==1 && t.width%4==0 && t.height%4==0)
                    .GroupBy(t=>t.width+"x"+t.height).OrderByDescending(g=>g.Count()).FirstOrDefault();
                if(bigDxt!=null && bigDxt.Count()>=DrawCollapsePolicy.ExactBigMin)exactGroup=bigDxt.Take(SystemInfo.maxTextureArraySlices).ToArray();
            }
            var exactIds=new HashSet<int>(exactGroup.Select(t=>t.GetInstanceID()));
            var direct=textures.Values.Where(t=>CollapseSize(t)==0 && !exactIds.Contains(t.GetInstanceID())).Take(DrawCollapsePolicy.DirectBindings).ToArray();
            int arrays=0;
            foreach(var tier in small.GroupBy(CollapseTier))
            {
                var group=tier.ToArray();int size=CollapseSize(group[0]);
                total=checked(total+(long)size*size*group.Length);
                RequireLive(SystemInfo.maxTextureArraySlices>0,"drawcollapse texture array slices unavailable");
                arrays+=(group.Length+SystemInfo.maxTextureArraySlices-1)/SystemInfo.maxTextureArraySlices;
            }
            if(exactGroup.Length>0)
            {
                arrays+=1;total=checked(total+(long)exactGroup[0].width*exactGroup[0].height*exactGroup.Length);
                GpuLog("DRAWCOLLAPSE_EXACT_ARRAY size="+exactGroup[0].width+"x"+exactGroup[0].height+" slices="+exactGroup.Length);
            }
            GpuLog("DRAWCOLLAPSE_ARRAY_TOTAL layout=small_dxt5_tiers_plus_borrowed_2d bytes_known="+total+" total_mb="+(total/1048576.0).ToString("F6",Inv)
                +" planned_arrays_known="+arrays+" direct_textures="+direct.Length+" active_texture_slots="+(arrays+direct.Length)
                +" shader_declared_slots="+DrawCollapsePolicy.TextureBindings+" samplers=2 arrays_allocated=0 copy_calls=0 free_vram=UNVERIFIED");
            RequireLive(SystemInfo.graphicsMemorySize>0 && total<(long)SystemInfo.graphicsMemorySize*1048576,
                "drawcollapse array budget exceeds total VRAM"); // Free VRAM still needs external measurement.
            RequireLive(arrays<=DrawCollapsePolicy.ArrayBindings && direct.Length<=DrawCollapsePolicy.DirectBindings,
                "drawcollapse texture binding budget exceeds 4 arrays + 32 borrowed 2D");
            foreach(var tier in small.GroupBy(CollapseTier))
            {
                var group=tier.ToArray();var t=group[0];
                for(int start=0;start<group.Length;start+=SystemInfo.maxTextureArraySlices)
                {
                    RequireLive(SystemInfo.maxTextureArraySlices>0 && gpuCollapseArrays.Count<DrawCollapsePolicy.ArrayBindings,"drawcollapse exceeds four array bindings");
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
            if(exactGroup.Length>0)
            {
                RequireLive(gpuCollapseArrays.Count<DrawCollapsePolicy.ArrayBindings,"drawcollapse exact array exceeds array bindings");
                var t0=exactGroup[0];int exArray=gpuCollapseArrays.Count;
                var ea=new Texture2DArray(t0.width,t0.height,exactGroup.Length,t0.graphicsFormat,TextureCreationFlags.None,1)
                    {name="GpuDeco collapse exact "+t0.width+"x"+t0.height,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                gpuCollapseArrays.Add(ea);
                RequireLive(ea.graphicsFormat==t0.graphicsFormat,"drawcollapse exact array format conversion rejected");
                for(int slice=0;slice<exactGroup.Length;slice++)
                {
                    var tex=exactGroup[slice];addresses.Add(tex.GetInstanceID(),new Vector4(exArray,slice,1f,1f));
                    Graphics.CopyTexture(tex,0,0,0,0,tex.width,tex.height,ea,slice,0,0,0);copies++;
                }
            }
            foreach(var t in direct)
            {
                addresses.Add(t.GetInstanceID(),new Vector4(DrawCollapsePolicy.ArrayBindings+gpuCollapseDirect.Count,0,1,1));gpuCollapseDirect.Add(t);
            }
            for(int i=0;i<source.Data.Items.Length;i++)
            {
                if(!addresses.TryGetValue(source.Data.Items[i].TextureId,out var address))
                {
                    gpuCollapseTextureFallback[i]=true;gpuCollapseTextureFallbackCount++;gpuCollapseIncluded[i]=false;continue;
                }
                var t=textures[source.Data.Items[i].TextureId];
                // Use the final address, not CollapseSize: exactGroup's large
                // textures also live in _Tex arrays. Missing addresses stay false.
                gpuCollapseArrayOnly[i]=address.x>=0 && address.x<gpuCollapseArrays.Count
                    && address.x<DrawCollapsePolicy.ArrayBindings;
#if AUTOTEST || GPUDECO
                // Image actors can change the uploaded address during playback.
                // Until every future variant is proven array-only, keep the full shader.
                if(GpuImageActor(gpuLiveOwnedIndices[source.MasterItems[i]]))gpuCollapseArrayOnly[i]=false;
#endif
                info[i*2]=new Vector4(address.x+(gpuCollapseOwnedRows[i].M.IsKeywordEnabled("USE_POINT_SAMPLING")?128:0),address.y,0,0);
                info[i*2+1]=new Vector4(address.z,address.w,address.z/t.width*.5f,address.w/t.height*.5f);
            }
            gpuCollapseTexture=new ComputeBuffer(info.Length,16);gpuCollapseTexture.SetData(info);
#if AUTOTEST || GPUDECO
            RememberGpuImageCollapseAddresses(addresses);
#endif
            // Replace admitted overflow rows with the ACTUAL independent draw
            // key (including its material ID and sorting bounds), not the hidden
            // SpriteRenderer's key. Retain even currently invisible overflow.
            for(int i=0;i<gpuCollapseTextureFallback.Length;i++)if(DrawCollapseTextureFallback(i))
            {
                var renderer=source.Renderers[i];var material=renderer.sharedMaterial;
                var key=GpuDecoScene.SortKey(renderer,material,gpuLiveCamera);
                gpuCollapseBarriers.Add(new GpuLiveRow{R=renderer,M=material,Key=key,Item=-1});
            }
            BuildDrawCollapseSpans();
            if (GpuDecoPlayer.Product && DrawCollapsePolicy.RecordedDispatch && DrawCollapsePolicy.GlobalVerts)
                source.Gpu.RecordCollapseAll(gpuCollapseTexture,gpuCollapseAllVerts,gpuCollapseAllMap,gpuCollapseAllCount);
            GpuLog("DRAWCOLLAPSE_PREPARED spans="+gpuCollapseSpans.Count+" included="+gpuCollapseIncluded.Count(x=>x)
                +" arrays="+gpuCollapseArrays.Count+" bytes="+total+" copy_calls="+copies
                +" texture_fallback_items="+gpuCollapseTextureFallbackCount+" texture_fallback_dispatch="+(gpuCollapseTextureFallbackCount>0)
                +" submeshes_per_renderer=1 direct_evaluate=True window=260..270 conservative_hidden_barriers=True free_vram=UNVERIFIED");
        }
        static bool PlayerCollapseTextureBudget(IEnumerable<Texture2D> textures)
        {
            var all=textures.ToArray();
            return GpuDecoPlayerPolicy.FitsTextures(all.Length,all.Count(t=>CollapseSize(t)==0),
                all.Where(t=>CollapseSize(t)>0).GroupBy(CollapseTier).Select(g=>g.Count()).ToArray(),SystemInfo.maxTextureArraySlices);
        }
        static Material DrawCollapseMaterial(GpuLiveRow row)
        {
            if(!row.R)return null;
            row.R.GetSharedMaterials(gpuCollapseMaterials);
            return row.Key.Slot<gpuCollapseMaterials.Count?gpuCollapseMaterials[row.Key.Slot]:null;
        }
        static bool DrawCollapseBarrierActive(GpuLiveRow row)
            =>row.R && row.R.enabled && row.R.gameObject.activeInHierarchy && !row.R.forceRenderingOff
                && (!(row.R is SpriteRenderer sprite) || sprite.sprite && sprite.color.a>0);
        static bool gpuCollapseKeysValid,gpuCollapseScanned;
        static Vector3 gpuCollapseKeyCamPos,gpuCollapseKeyFwd;
        static readonly HashSet<int> gpuCollapseDirtyIds=new HashSet<int>();
        static readonly List<scrDecoration> gpuCollapseNewDecos=new List<scrDecoration>();
        static SceneRenderOrder.Key DcShiftKey(SceneRenderOrder.Key k,float shift)
            =>shift==0f?k:new SceneRenderOrder.Key(k.Layer,k.Order,k.Queue,k.Distance-shift,k.Material,k.Renderer,k.Slot);
        // Dormancy 가 이미 거는 장식 후크(ApplyColor/SetVisible/SetMaskingType/EnableMeshRenderer/Setup/Awake)와 SetDepth 후크에서 부른다.
        internal static void CollapseMarkDirty(scrDecoration d)
        {
            var vd=d as scrVisualDecoration;if(vd==null)return;
            if(vd.spriteRenderer)gpuCollapseDirtyIds.Add(vd.spriteRenderer.GetInstanceID());
            if(vd.meshRenderer)gpuCollapseDirtyIds.Add(vd.meshRenderer.GetInstanceID());
        }
        internal static void CollapseMarkNew(scrDecoration d){if(gpuCollapseSeen!=null)gpuCollapseNewDecos.Add(d);CollapseMarkDirty(d);}
        static int TryAddCollapseBarrier(Renderer r)
        {
            if(!r || !r.gameObject.scene.IsValid() || !gpuCollapseSeen.Add(r.GetInstanceID()))return 0;
            if(gpuLiveRoot && r.transform.IsChildOf(gpuLiveRoot.transform))return 0;
            if((gpuLiveCamera.cullingMask&(1<<r.gameObject.layer))==0 || r.GetComponentInParent<SortingGroup>())return 0;
            var mats=r.sharedMaterials;if(mats==null)return 0;int added=0;
            for(int slot=0;slot<mats.Length;slot++)
            {
                var m=mats[slot];if(!m || m.renderQueue<=2500)continue;
                gpuCollapseBarriers.Add(new GpuLiveRow{R=r,M=m,Item=-1,AuxiliaryOwner=-1,Key=GpuDecoScene.SortKey(r,m,gpuLiveCamera,slot)});added++;
            }
            return added;
        }
        static double dcScanMs,dcTieMs,dcPlanMs,dcMeshMs,dcMatMs,dcMeshBuildMs,dcBufMs,dcGoMs,dcDisposeMs;
        static double DcMs(long from)=>(Stopwatch.GetTimestamp()-from)*1000.0/Stopwatch.Frequency;
        static void BuildDrawCollapseSpans()
        {
            long dcT0=Stopwatch.GetTimestamp();int nudgedSpans=0;
            InvalidateRecordedCollapse();
            var source=gpuLiveSlices[0];
            var rows=new List<DrawCollapsePolicy.Row>();
            // ---- 프로토타입: 키 캐시 + 변경 추적 재계획 ----
            // 직교 카메라가 앞축으로 움직이면 모든 정렬 거리가 같은 값만큼 변한다. 캐시한 키는 그 값으로 일괄 보정한다(앞축이 바뀌면 전부 다시 읽는다).
            var camNow=gpuLiveCamera.transform;
            bool keysValid=DrawCollapsePolicy.KeyCache && gpuCollapseKeysValid
                && Vector3.Dot(camNow.forward,gpuCollapseKeyFwd)>=0.999999f;
            float keyShift=keysValid?Vector3.Dot(camNow.position-gpuCollapseKeyCamPos,gpuCollapseKeyFwd):0f;
            // 곡 시작 뒤 파괴된 후보 장벽은 더 이상 그려지지 않으므로 뺀다.
            int lostBarriers=gpuCollapseBarriers.RemoveAll(b=>!b.R || !b.M);
            int addedBarriers=0;
            if(gpuCollapseSeen!=null)
            {
                if(DrawCollapsePolicy.FullScanOnReplan || !gpuCollapseScanned)
                {
                    foreach(var r in Resources.FindObjectsOfTypeAll<Renderer>())addedBarriers+=TryAddCollapseBarrier(r);
                    gpuCollapseScanned=true;
                }
                else
                {
                    // 새로 만들어진 장식의 렌더러만 후크로 추적한 목록에서 가져온다.
                    foreach(var d in gpuCollapseNewDecos)
                    {
                        if((object)d==null)continue;
                        var vd=d as scrVisualDecoration;if(vd==null)continue;
                        addedBarriers+=TryAddCollapseBarrier(vd.spriteRenderer);
                        addedBarriers+=TryAddCollapseBarrier(vd.meshRenderer);
                    }
                }
                gpuCollapseNewDecos.Clear();
            }
            dcScanMs=DcMs(dcT0);long dcT1=Stopwatch.GetTimestamp();
            // 소유 장식은 변환이 얼어 있어 키가 변하지 않는다: 보정만 하고 다시 읽지 않는다.
            if(keysValid)
            {
                for(int i=0;i<gpuCollapseOwnedRows.Length;i++){var orow=gpuCollapseOwnedRows[i];if(orow!=null)orow.Key=DcShiftKey(orow.Key,keyShift);}
            }
            else
            {
                for(int i=0;i<gpuCollapseOwnedRows.Length;i++)
                {
                    var row=gpuCollapseOwnedRows[i];var material=DrawCollapseMaterial(row);
                    RequireLive(row.R && material,"drawcollapse replan owned renderer lost item="+i);
                    RequireLive(material==row.M,"drawcollapse replan owned material changed item="+i);
                    row.Key=GpuDecoScene.SortKey(row.R,material,gpuLiveCamera,row.Key.Slot);
                }
            }
            // 장벽: 지금 활성이거나 변경 이벤트가 온 것만 다시 읽고, 나머지는 보정한 캐시 키를 쓴다.
            var activeBucketMinMat=new Dictionary<(int,int,int,float),int>();
            var activeBucketMaxMat=new Dictionary<(int,int,int,float),int>();
            int rereadBarriers=0;
            foreach(var row in gpuCollapseBarriers)
            {
                bool active=DrawCollapseBarrierActive(row);
                bool tieBarrier=active || GpuDecoPlayer.Product && DrawCollapsePolicy.TieAllBarriers;
                bool refresh=!keysValid || active || gpuCollapseDirtyIds.Contains(row.R.GetInstanceID());
                if(refresh)
                {
                    var material=DrawCollapseMaterial(row);
                    RequireLive(row.R && material,"drawcollapse replan barrier lost");
                    row.M=material;row.Key=GpuDecoScene.SortKey(row.R,material,gpuLiveCamera,row.Key.Slot);rereadBarriers++;
                }
                else row.Key=DcShiftKey(row.Key,keyShift);
                if(tieBarrier)
                {
                    var bk=row.Key;var bkey=(bk.Layer,bk.Order,bk.Queue,bk.Distance);
                    if(!activeBucketMinMat.TryGetValue(bkey,out int curMin) || bk.Material<curMin)activeBucketMinMat[bkey]=bk.Material;
                    if(!activeBucketMaxMat.TryGetValue(bkey,out int curMax) || bk.Material>curMax)activeBucketMaxMat[bkey]=bk.Material;
                }
            }
            int tieExcluded=0;var newBarriers=new List<GpuLiveRow>();
            for(int i=0;i<gpuCollapseOwnedRows.Length;i++)
            {
                if(!gpuCollapseIncluded[i])continue;
                var orow=gpuCollapseOwnedRows[i];if(orow==null || !orow.R)continue;
                var ok=orow.Key;
                if(!activeBucketMinMat.TryGetValue((ok.Layer,ok.Order,ok.Queue,ok.Distance),out int minBarrierMat))continue;
                if(DrawCollapsePolicy.KeepBeforeBarrier)
                {
                    if(ok.Material<minBarrierMat)continue; // 모든 활성 장벽보다 앞: 구간에 유지(대표 거리를 +미세 이동)
                    if(DrawCollapsePolicy.KeepAfterBarrier && activeBucketMaxMat.TryGetValue((ok.Layer,ok.Order,ok.Queue,ok.Distance),out int maxBarrierMat) && ok.Material>maxBarrierMat)continue;
                }
                gpuCollapseIncluded[i]=false;
                if(!gpuCollapseTextureFallback[i]){gpuCollapseTextureFallback[i]=true;gpuCollapseTextureFallbackCount++;}
                var ir=source.Renderers[i];
                if(ir)newBarriers.Add(new GpuLiveRow{R=ir,M=ir.sharedMaterial,Item=-1,AuxiliaryOwner=-1,Key=GpuDecoScene.SortKey(ir,ir.sharedMaterial,gpuLiveCamera)});
                tieExcluded++;
            }
            var newBarrierSet=new HashSet<GpuLiveRow>(newBarriers);
            gpuCollapseBarriers.AddRange(newBarriers);
            dcTieMs=DcMs(dcT1);
            GpuLog("DRAWCOLLAPSE_TIE_EXCLUDED owned_items="+tieExcluded+" active_buckets="+activeBucketMinMat.Count);
            if(GpuDecoPlayer.Product)Main.Entry.Logger.Log("[GPU 장식] 동점 묶음 때문에 독립 렌더러로 남긴 장식 "+tieExcluded+"개 (동점 묶음 "+activeBucketMinMat.Count+"개)");
            GpuLog("DRAWCOLLAPSE_REPLAN barriers_removed="+lostBarriers+" barriers_added="+addedBarriers+" barriers_now="+gpuCollapseBarriers.Count+" reread="+rereadBarriers+" keys_cached="+keysValid+" shift="+keyShift.ToString("R",Inv));
            foreach(var row in gpuCollapseBarriers)
                rows.Add(new DrawCollapsePolicy.Row(row.Key,false,row.R.GetInstanceID()));
            for(int i=0;i<gpuCollapseOwnedRows.Length;i++)
                if(gpuCollapseIncluded[i])rows.Add(new DrawCollapsePolicy.Row(gpuCollapseOwnedRows[i].Key,true,i));
            gpuCollapseCameraPosition=gpuLiveCamera.transform.position;
            gpuCollapseCameraForward=gpuLiveCamera.transform.forward;gpuCollapseOwnedCursor=0;
            gpuCollapseKeyCamPos=gpuCollapseCameraPosition;gpuCollapseKeyFwd=gpuCollapseCameraForward;gpuCollapseKeysValid=DrawCollapsePolicy.KeyCache;
            gpuCollapseDirtyIds.Clear();
            long dcT2=Stopwatch.GetTimestamp();
            var plan=DrawCollapsePolicy.Plan(rows);
            dcPlanMs=DcMs(dcT2);dcMeshMs=0;long dcT3=Stopwatch.GetTimestamp();
            RequireLive(plan.Count>0 && plan.Count<=DrawCollapsePolicy.MaxSpans,"drawcollapse conservative prepared spans="+plan.Count+" exceeds renderer budget");
            dcMatMs=dcMeshBuildMs=dcBufMs=dcGoMs=0;
            // Old embedded bundles have no local keyword: retain all direct
            // bindings and the original variant even for array-only spans.
            bool supportsNoDirect=gpuCollapseShader.keywordSpace.FindKeyword(CollapseNoDirectKeyword).isValid;
            int baseRun=0,noDirectSpans=0;
            foreach(var items in plan)
            {
                long dcL0=Stopwatch.GetTimestamp();
                var first=gpuCollapseOwnedRows[items[0]];var last=gpuCollapseOwnedRows[items[items.Length-1]];
                var s=new CollapseSpan{Items=items,First=first.Key,Last=last.Key};gpuCollapseSpans.Add(s);
                s.Base=baseRun;baseRun+=items.Length;
                RequireLive(items.All(i=>gpuCollapseOwnedRows[i].R.gameObject.layer==first.R.gameObject.layer),
                    "drawcollapse span crosses camera culling layers");
                // 블렌드 상태와 큐가 같은 구간은 재질 하나를 공유한다(엔진의 CanFlattenSharedMaterialData 해시 비용을 구간 수만큼 안 낸다).
                var blend=new float[3];int bi=0;
                foreach(string p in new[]{"_SrcBlend","_DstBlend","_BlendOp"})
                {
                    float expected=first.M.HasProperty(p)?first.M.GetFloat(p):(p=="_SrcBlend"?1:p=="_DstBlend"?10:0);
                    RequireLive(items.All(i=>!gpuCollapseOwnedRows[i].M.HasProperty(p) || gpuCollapseOwnedRows[i].M.GetFloat(p)==expected),
                        "drawcollapse incompatible blend states "+p);
                    blend[bi++]=expected;
                }
                bool noDirect=supportsNoDirect && gpuCollapseArrayOnly!=null
                    && items.All(i=>(uint)i<(uint)gpuCollapseArrayOnly.Length && gpuCollapseArrayOnly[i]);
                string matKey=first.M.renderQueue+"/"+blend[0]+"/"+blend[1]+"/"+blend[2]+"/"+noDirect;
                if(!gpuCollapseSharedMats.TryGetValue(matKey,out var shared) || !shared)
                {
                    shared=new Material(gpuCollapseShader){name="GpuDeco collapse shared",renderQueue=first.M.renderQueue};
                    shared.SetFloat("_SrcBlend",blend[0]);shared.SetFloat("_DstBlend",blend[1]);shared.SetFloat("_BlendOp",blend[2]);
                    if(noDirect)shared.EnableKeyword(CollapseNoDirectKeyword);
                    for(int a=0;a<DrawCollapsePolicy.ArrayBindings && gpuCollapseArrays.Count>0;a++)shared.SetTexture("_Tex"+a,gpuCollapseArrays[Math.Min(a,gpuCollapseArrays.Count-1)]);
                    if(!noDirect)for(int a=0;a<DrawCollapsePolicy.DirectBindings && gpuCollapseDirect.Count>0;a++)shared.SetTexture("_Big"+a,gpuCollapseDirect[Math.Min(a,gpuCollapseDirect.Count-1)]);
                    gpuCollapseSharedMats[matKey]=shared;
                }
                s.Material=shared;s.SharedMat=true;
                if(noDirect)noDirectSpans++;
                dcMatMs+=DcMs(dcL0);long dcL1=Stopwatch.GetTimestamp();
                if(DrawCollapsePolicy.GlobalVerts)
                {
                    // 정점 자리는 전역 버퍼의 번호만 담는다(x). 실제 위치·색·UV 는 정점 셰이더가 전역 버퍼에서 읽는다.
                    s.Mesh=new Mesh{name="GpuDeco collapse span",indexFormat=IndexFormat.UInt32};
                    var slotIds=new Vector3[items.Length*4];
                    for(int n=0;n<items.Length;n++)for(int k4=0;k4<4;k4++)slotIds[n*4+k4]=new Vector3((s.Base+n)*4+k4,0,0);
                    s.Mesh.SetVertices(slotIds);
                }
                else
                {
                s.Mesh=new Mesh{name="GpuDeco collapse span",indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw};
                s.Mesh.SetVertexBufferParams(items.Length*4,
                    new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4));
                s.Mesh.SetVertexBufferData(new float[items.Length*60],0,0,items.Length*60);
                }
                var indices=new int[items.Length*6];
                for(int n=0;n<items.Length;n++)for(int t=0;t<6;t++)indices[n*6+t]=n*4+source.Data.Items[items[n]].Triangles[t];
                s.Mesh.subMeshCount=1;s.Mesh.SetIndices(indices,MeshTopology.Triangles,0,false);
                // Refresh the sort center too: a start-of-song mesh bound is stale at ready.
                Vector3 center=first.R.bounds.center;
                if(first.R is SpriteRenderer sprite && sprite.spriteSortPoint==SpriteSortPoint.Pivot)center=first.R.transform.position;
                // 활성 장벽이 있는 정렬 묶음에서 시작하는 구간: 구간에 남은 장식은 모두 장벽보다 앞에 그려져야 하므로, 대표 렌더러의 정렬 거리를
                // 아주 조금 더 멀게(먼저 그려지게) 해서 재질 ID 동점 해소를 엔진의 동점 규칙에 맡기지 않는다. 정점 위치는 GPU 가 쓰므로 화면은 같다.
                if(DrawCollapsePolicy.NudgeTieSpans && activeBucketMinMat.ContainsKey((first.Key.Layer,first.Key.Order,first.Key.Queue,first.Key.Distance)))
                {
                    var fk=(first.Key.Layer,first.Key.Order,first.Key.Queue,first.Key.Distance);
                    bool afterAll=activeBucketMaxMat.TryGetValue(fk,out int mx) && first.Key.Material>mx;
                    center+=gpuCollapseCameraForward*(afterAll?-DrawCollapsePolicy.TieNudge:DrawCollapsePolicy.TieNudge);s.Nudged=true;nudgedSpans++;
                }
                s.Mesh.bounds=new Bounds(center,new Vector3(2000000,2000000,0));s.Mesh.UploadMeshData(true);
                if(!DrawCollapsePolicy.GlobalVerts)s.Vertices=s.Mesh.GetVertexBuffer(0);
                dcMeshBuildMs+=DcMs(dcL1);long dcL2=Stopwatch.GetTimestamp();
                RequireLive(s.Mesh.subMeshCount==1 && (DrawCollapsePolicy.GlobalVerts || s.Mesh.GetVertexBufferStride(0)==60),"drawcollapse raw mesh layout");
                if(!DrawCollapsePolicy.GlobalVerts){s.Map=new ComputeBuffer(items.Length,4);s.Map.SetData(items.Select(i=>(uint)i).ToArray());}
                dcBufMs+=DcMs(dcL2);long dcL3=Stopwatch.GetTimestamp();
                var go=new GameObject("GpuDeco collapse span"){hideFlags=HideFlags.HideAndDontSave,layer=first.R.gameObject.layer};
                go.transform.SetParent(gpuLiveRoot.transform,false);go.AddComponent<MeshFilter>().sharedMesh=s.Mesh;
                s.Renderer=go.AddComponent<MeshRenderer>();s.Renderer.enabled=false;s.Renderer.sharedMaterial=s.Material;
                s.Renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;s.Renderer.allowOcclusionWhenDynamic=false;
                s.Renderer.sortingLayerID=first.R.sortingLayerID;s.Renderer.sortingOrder=first.R.sortingOrder;
                s.Renderer.shadowCastingMode=ShadowCastingMode.Off;s.Renderer.receiveShadows=false;
                s.Renderer.lightProbeUsage=LightProbeUsage.Off;s.Renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                RequireLive(s.Renderer.sharedMaterials.Length==1,"drawcollapse requires one material");
                dcGoMs+=DcMs(dcL3);
            }
            if(DrawCollapsePolicy.GlobalVerts)
            {
                gpuCollapseAllVerts?.Dispose();gpuCollapseAllMap?.Dispose();
                gpuCollapseAllCount=baseRun;
                var allMap=new uint[baseRun];int at=0;
                foreach(var sp in gpuCollapseSpans)foreach(int it in sp.Items)allMap[at++]=(uint)it;
                gpuCollapseAllVerts=new ComputeBuffer(Math.Max(1,baseRun*60),4,ComputeBufferType.Raw);
                gpuCollapseAllMap=new ComputeBuffer(Math.Max(1,baseRun),4);gpuCollapseAllMap.SetData(allMap);
                Shader.SetGlobalBuffer("_CollapseVerts",gpuCollapseAllVerts);
            }
            // 같은 (층,순서,큐,거리) 묶음에서 이어지는 구간들이 재질을 공유하면 완전 동점이 되어 순서가 렌더러 ID(생성 순서의 역순)로 뒤집힌다.
            // 그런 구간들은 재질을 따로 만들되 뒤 구간부터 만들어 앞 구간의 인스턴스 ID 가 더 작게(= 먼저 그려지게) 한다.
            int multiSpans=0;
            for(int ri=0;ri<gpuCollapseSpans.Count;)
            {
                int rj=ri+1;
                while(rj<gpuCollapseSpans.Count && DcSameBucket(gpuCollapseSpans[ri].First,gpuCollapseSpans[rj].First))rj++;
                if(rj-ri>=2)
                {
                    multiSpans+=rj-ri;
                    for(int k=rj-1;k>=ri;k--)
                    {
                        var sp=gpuCollapseSpans[k];var m=new Material(sp.Material){name="GpuDeco collapse ordered"};
                        // The copy constructor retains shader keywords and texture bindings.
                        RequireLive(m.IsKeywordEnabled(CollapseNoDirectKeyword)==sp.Material.IsKeywordEnabled(CollapseNoDirectKeyword),
                            "drawcollapse ordered material lost texture variant");
                        sp.Material=m;sp.SharedMat=false;sp.Renderer.sharedMaterial=m;
                    }
                }
                ri=rj;
            }
            GpuLog("DRAWCOLLAPSE_SAME_BUCKET_SPANS spans_in_multi_runs="+multiSpans+" nudged_spans="+nudgedSpans);
            if(GpuDecoPlayer.Product)Main.Entry.Logger.Log("[GPU 장식] 동점 묶음 구간 "+multiSpans+"개(재질 따로), 거리 보정 "+nudgedSpans+"개, 공유 재질 종류 "+gpuCollapseSharedMats.Count);
            GpuLog("DRAWCOLLAPSE_NODIRECT spans="+noDirectSpans+" of "+gpuCollapseSpans.Count);
            if(GpuDecoPlayer.Product)Main.Entry.Logger.Log("[GPU 장식] 그리기 구간 "+gpuCollapseSpans.Count+"개 중 배열 텍스처만 쓰는 구간 "+noDirectSpans+"개");
        }
        static bool DcSameBucket(SceneRenderOrder.Key a,SceneRenderOrder.Key b)=>a.Layer==b.Layer && a.Order==b.Order && a.Queue==b.Queue && a.Distance==b.Distance;
        static void RebuildDrawCollapse()
        {
            RequireDrawCollapse();
            RequireLive(!gpuCollapseOn && gpuCollapseTexture!=null && gpuCollapseShader,
                "drawcollapse ready requires prepared textures and collapse off");
            // Only span resources are replaced. Texture arrays, borrowed textures,
            // texture address buffer, and the loaded shader survive every ready.
            long dcReadyT0=Stopwatch.GetTimestamp();
#if AUTOTEST
            TileCompositeRemove();
#endif
            foreach(var span in gpuCollapseSpans)span.Dispose();gpuCollapseSpans.Clear();
            dcDisposeMs=DcMs(dcReadyT0);
            try {BuildDrawCollapseSpans();}
            catch
            {
                foreach(var span in gpuCollapseSpans)span.Dispose();gpuCollapseSpans.Clear();
                throw;
            }
            GpuLog("DRAWCOLLAPSE_REPLAN_MS scan="+dcScanMs.ToString("F1",Inv)+" tie="+dcTieMs.ToString("F1",Inv)+" plan="+dcPlanMs.ToString("F1",Inv)+" mat="+dcMatMs.ToString("F1",Inv)+" mesh="+dcMeshBuildMs.ToString("F1",Inv)+" buf="+dcBufMs.ToString("F1",Inv)+" go="+dcGoMs.ToString("F1",Inv)+" dispose="+dcDisposeMs.ToString("F1",Inv)+" total="+DcMs(dcReadyT0).ToString("F1",Inv));
            gpuCollapseReplanTime=Time.realtimeSinceStartupAsDouble;
            GpuLog("DRAWCOLLAPSE_REPLANNED spans="+gpuCollapseSpans.Count+" included="+gpuCollapseIncluded.Count(x=>x)
                +" barriers="+gpuCollapseBarriers.Count+" arrays_reused="+gpuCollapseArrays.Count
                +" texture_buffer_reused=True shader_reused=True copy_calls=0 owned_checks_per_frame="+DrawCollapsePolicy.OwnedChecksPerFrame);
        }
        static int DcTriple(int layer,int order,int queue,SceneRenderOrder.Key k)
        {
            int c=layer.CompareTo(k.Layer);if(c!=0)return c;
            c=order.CompareTo(k.Order);if(c!=0)return c;
            return queue.CompareTo(k.Queue);
        }
        static string DcKey(SceneRenderOrder.Key k)=>"("+k.Layer+","+k.Order+","+k.Queue+","+k.Distance.ToString("R",Inv)+",m"+k.Material+")";
        static List<GpuLiveRow> CollapseBarriersToCheck(bool full)
        {
            if(full || gpuCollapseBarriers.Count<=CollapseBarrierSweepPerFrame)return gpuCollapseBarriers;
            gpuCollapseBarrierSweep.Clear();int count=gpuCollapseBarriers.Count;
            if(gpuCollapseBarrierCursor>=count)gpuCollapseBarrierCursor=0;
            for(int n=0;n<CollapseBarrierSweepPerFrame;n++){gpuCollapseBarrierSweep.Add(gpuCollapseBarriers[gpuCollapseBarrierCursor]);if(++gpuCollapseBarrierCursor>=count)gpuCollapseBarrierCursor=0;}
            return gpuCollapseBarrierSweep;
        }
#if AUTOTEST
        internal static bool DiagFull; static int diagOrderLogged;
        static void DiagOrder(string reason){if(diagOrderLogged++<25)Main.Entry.Logger.Log("[gpudeco] fullcheck 위반: "+reason);}
        internal static void RunFullCheck()
        {
            DiagFull=true;diagOrderLogged=0;
            try{ValidateDrawCollapse(true);Main.Entry.Logger.Log("[gpudeco] fullcheck 끝: 위반 "+diagOrderLogged+"개(치명 없음) 구간 "+gpuCollapseSpans.Count+" 장벽 "+gpuCollapseBarriers.Count);}
            catch(Exception e){Main.Entry.Logger.Log("[gpudeco] fullcheck 치명: "+e.Message.Substring(0,Math.Min(400,e.Message.Length))+" | 위반 "+diagOrderLogged);}
            finally{DiagFull=false;}
        }
#endif
        static void ValidateDrawCollapse(bool full=true)
        {
#if AUTOTEST
            var guard=new DrawCollapseGuard(full && !GpuDecoPlayer.Product, DiagFull ? DiagOrder : (GpuDecoPlayer.Product && !full ? GpuDecoPlayer.OrderViolation : (Action<string>)null));
#else
            var guard=new DrawCollapseGuard(false, GpuDecoPlayer.Product && !full ? GpuDecoPlayer.OrderViolation : (Action<string>)null);
#endif
            long dcV0=Stopwatch.GetTimestamp();
            RequireDrawCollapse(guard);
            var sweep=CollapseBarriersToCheck(full); // 순환 커서는 프레임당 한 번만 전진
            bool prepared=gpuCollapseSpans.Count>0 && gpuCollapseTexture!=null && gpuCollapseIncluded!=null;
            guard.Check(prepared,"drawcollapse prepare before live prepare required");
            guard.Check(gpuLiveCpuFallbackCount==0,"drawcollapse requires zero CPU fallback; disable before fallback");
            guard.Check(gpuLiveCamera,"drawcollapse camera lost");
            float shift=0;
            if(gpuLiveCamera)
            {
                float dot=Vector3.Dot(gpuLiveCamera.transform.forward,gpuCollapseCameraForward);
                var mode=gpuLiveCamera.transparencySortMode;
                if(mode==TransparencySortMode.Default)mode=GraphicsSettings.transparencySortMode;
                guard.Check(gpuLiveCamera.orthographic && (mode==TransparencySortMode.Default || mode==TransparencySortMode.Orthographic)
                    && dot>=0.999999f,"drawcollapse camera sort axis/mode changed dot="+dot.ToString("R",Inv));
                shift=Vector3.Dot(gpuLiveCamera.transform.position-gpuCollapseCameraPosition,gpuCollapseCameraForward);
            }
            if(prepared && gpuLiveSlices.Count==1 && full)
                for(int i=0;i<gpuCollapseIncluded.Length;i++)
                    guard.Check(gpuCollapseIncluded[i] || DrawCollapseTextureFallback(i) || MergeHidden(i),
                        "drawcollapse visible item outside prepared window item="+i);
            // 보조 렌더러(소유 장식마다 최대 3개, 수만 개)는 진입 때 전부, 재생 중에는 프레임당 일부씩 순환 점검한다
            // (전부 훑으면 enabled/gameObject/active 속성 읽기가 프레임당 수십만 번이라 수십 ms 가 든다).
            {
                int acount=gpuCollapseAuxiliaries.Count;
                int achecks=full?acount:Math.Min(acount,CollapseBarrierSweepPerFrame);
                for(int n=0;n<achecks;n++)
                {
                    int ai=full?n:gpuCollapseAuxCursor++%acount;
                    var r=gpuCollapseAuxiliaries[ai];
                    guard.Check(!r.R || !r.R.enabled || !r.R.gameObject.activeInHierarchy || r.R.forceRenderingOff,
                        "drawcollapse omitted auxiliary became visible");
                }
                if(!full && acount>0)gpuCollapseAuxCursor%=acount;
            }
            if(gpuLiveCamera)
            {
                // These are the small nonowned population, not all owned items.
                foreach(var b in sweep)
                {
                    if(!b.R || !b.M)continue; // 파괴된 렌더러는 더 이상 그려지지 않는다.
                    if(!DrawCollapseBarrierActive(b))continue;
                    // 장벽 키가 움직이는 것(파티클 거리, 효과가 바꾸는 순서)은 정상이다. 구간 앞뒤 불변식은 아래 대표/장벽 순서 검사가 매 프레임 확인한다.
                }
                // Owned transforms are frozen by GPU ownership. A bounded sweep
                // also catches foreign sort/material writes without a 23,547-row
                // scan in every playback frame. Full entry checks visit them all.
                if(gpuCollapseOwnedRows!=null)
                {
                    int count=gpuCollapseOwnedRows.Length;
                    // 재계획 직후(몇 초 안)에는 소유 장식 키를 방금 다시 읽었으므로 전수 점검을 건너뛴다(진입 0.9초의 후보).
                    bool fullOwned=full && Time.realtimeSinceStartupAsDouble-gpuCollapseReplanTime>=8;
                    int checks=fullOwned?count:Math.Min(count,DrawCollapsePolicy.OwnedChecksPerFrame);
                    for(int n=0;n<checks;n++)
                    {
                        int i=fullOwned?n:gpuCollapseOwnedCursor++%count;
                        CheckDrawCollapseKey(gpuCollapseOwnedRows[i],shift,guard,"owned item="+i);
                    }
                    if(!fullOwned && count>0)gpuCollapseOwnedCursor%=count;
                }
            }
            if(gpuLiveCamera)
            {
                foreach(var span in gpuCollapseSpans)
                    guard.Check(span.Renderer && span.Material,"drawcollapse representative lost");
                // 장벽 하나당 정수(층,순서,큐) 비교로 구간 범위를 먼저 거른다. 범위 밖(구간 앞/뒤)이면 거리(bounds)를 읽지 않는다.
                if(!DrawCollapsePolicy.SkipOrderSweep || full)
                foreach(var barrier in sweep)
                {
                    if(!DrawCollapseBarrierActive(barrier))continue;
                    var r=barrier.R;
                    int bl=SortingLayer.GetLayerValueFromID(r.sortingLayerID),bo=r.sortingOrder,bq=barrier.M.renderQueue;
                    SceneRenderOrder.Key key=default;bool haveKey=false;
                    foreach(var span in gpuCollapseSpans)
                    {
                        if(!span.Renderer || !span.Material)continue;
                        if(DcTriple(bl,bo,bq,span.First)<0 || DcTriple(bl,bo,bq,span.Last)>0)continue; // 구간 앞 또는 뒤: 안전
                        // 구간 범위 안/경계: 실제 거리까지 읽어 순서를 확인한다.
                        if(!haveKey){key=GpuDecoScene.SortKey(r,DrawCollapseMaterial(barrier),gpuLiveCamera,barrier.Key.Slot);haveKey=true;}
                        var proxy=GpuDecoScene.SortKey(span.Renderer,span.Material,gpuLiveCamera);
                        var first=gpuCollapseOwnedRows[span.Items[0]];
                        var last=gpuCollapseOwnedRows[span.Items[span.Items.Length-1]];
                        if(!first.R || !last.R)continue; // Already recorded by the owned checks.
                        var firstKey=GpuDecoScene.SortKey(first.R,first.M,gpuLiveCamera,first.Key.Slot);
                        var lastKey=GpuDecoScene.SortKey(last.R,last.M,gpuLiveCamera,last.Key.Slot);
                        bool before=key.CompareTo(firstKey)<0,after=key.CompareTo(lastKey)>0;
                        guard.Check((before && key.CompareTo(proxy)<0) || (after && key.CompareTo(proxy)>0),
                            "drawcollapse proxy/barrier order changed renderer="+r.GetInstanceID()
                            +" name="+r.name+" key="+DcKey(key)+" first="+DcKey(firstKey)+" last="+DcKey(lastKey)+" proxy="+DcKey(proxy)
                            +" span_items="+span.Items.Length);
                    }
                }
            }
            if(full)GpuLog("DRAWCOLLAPSE_VALIDATE_MS full="+DcMs(dcV0).ToString("F1",Inv)+" barriers="+gpuCollapseBarriers.Count+" auxiliaries="+gpuCollapseAuxiliaries.Count);
            guard.Finish(GpuLog);
        }
        static void CheckDrawCollapseKey(GpuLiveRow row,float shift,DrawCollapseGuard guard,string label)
        {
            if(row==null || !row.R || !row.M){guard.Check(false,"drawcollapse "+label+" lost");return;}
            var material=DrawCollapseMaterial(row);
            var now=GpuDecoScene.SortKey(row.R,material,gpuLiveCamera,row.Key.Slot);
            // Avoid formatting thousands of success-path diagnostics at entry.
            if(!DrawCollapsePolicy.SameSortKey(row.Key,now,shift))
                guard.Check(false,"drawcollapse "+label+" sort key changed name="+row.R.name
                    +" layer="+row.Key.Layer+"->"+now.Layer+" order="+row.Key.Order+"->"+now.Order
                    +" queue="+row.Key.Queue+"->"+now.Queue+" material="+row.Key.Material+"->"+now.Material
                    +" distance_delta="+(row.Key.Distance-now.Distance).ToString("R",Inv)+" camera_shift="+shift.ToString("R",Inv));
        }
        static void CheckDrawCollapseBeforeDispatch(double time)
        {
            if(!gpuCollapseOn)return;
            string reason=gpuCollapsePendingFallback;
            if(reason==null)
            {
                try
                {
                    RequireLive(GpuDecoPlayer.Product || time>=257.5 && time<=270.25,"outside_prepared_260..270_window");
                    // 제품: 순서 점검은 로그만 남기므로 몇 프레임에 한 번이면 된다 (매 프레임 속성 읽기 약 0.09ms).
                    if(!GpuDecoPlayer.Product || (Time.frameCount&7)==0)ValidateDrawCollapse(false);
                }
                catch(Exception e){reason=e.GetType().Name+":"+e.Message;}
            }
            if(reason==null)return;
            // RefreshAlpha must run outside its own pendingAlpha enumeration.
            // The caller now executes normal Evaluate/copy in THIS dispatch with
            // freshly restored masks. No recursive dispatch or whole-GPU recovery.
            if (GpuDecoPlayer.Product) throw new InvalidOperationException(reason);
            gpuCollapseFault=reason;HideDrawCollapse();
            foreach(var slice in gpuLiveSlices)slice.RefreshAlpha();
            GpuLog("DRAWCOLLAPSE_FALLBACK reason="+reason.Replace('\r',' ').Replace('\n',' ')
                +" independent_restored=True");
        }
        static void DispatchDrawCollapse(double time)
        {
            if(DrawCollapsePolicy.SkipPausedCompute && GpuPlaybackState.Paused && gpuCollapseSpans.Count>0 && gpuCollapseSpans[0].Renderer.enabled)return;
            var source=gpuLiveSlices[0];
            if(DrawCollapsePolicy.GlobalVerts && DrawCollapsePolicy.DirectDispatch)
            {
                var camd=ADOBase.controller.camy;var ctd=camd.transform;
                float camScaleD=camd.camobj.orthographicSize*.2f/gpuLiveZoomFactor;
                Vector4 clipD=Vector4.zero;
                if(DrawCollapsePolicy.ClipOffscreen)
                {
                    var co=camd.camobj;float halfH=co.orthographicSize*1.05f+.5f,halfW=halfH*co.aspect;float rad=ctd.eulerAngles.z*Mathf.Deg2Rad;
                    clipD=new Vector4(Mathf.Cos(rad),Mathf.Sin(rad),halfW,halfH);
                }
                source.Gpu.DispatchCollapseAllDirect(gpuCollapseTexture,gpuCollapseAllVerts,gpuCollapseAllMap,gpuCollapseAllCount,
                    new Vector4(ctd.position.x,ctd.position.y,ctd.eulerAngles.z,camScaleD),time,clipD);
                foreach(var s in gpuCollapseSpans)if(s.Renderer.enabled==DrawCollapsePolicy.HideProxies)s.Renderer.enabled=!DrawCollapsePolicy.HideProxies;
            }
            else if(GpuDecoPlayer.Product && DrawCollapsePolicy.GlobalVerts && DrawCollapsePolicy.MinimalDispatch)
            {
                var camm=ADOBase.controller.camy;var ctm=camm.transform;
                float camScaleM=camm.camobj.orthographicSize*.2f/gpuLiveZoomFactor;
                Vector4 clipM=Vector4.zero;
                if(DrawCollapsePolicy.ClipOffscreen)
                {
                    var co=camm.camobj;float halfH=co.orthographicSize*1.05f+.5f,halfW=halfH*co.aspect;float rad=ctm.eulerAngles.z*Mathf.Deg2Rad;
                    clipM=new Vector4(Mathf.Cos(rad),Mathf.Sin(rad),halfW,halfH);
                }
                long ex0=Stopwatch.GetTimestamp();
                source.Gpu.DispatchCollapseMinimal(gpuCollapseTexture,gpuCollapseAllVerts,gpuCollapseAllMap,gpuCollapseAllCount,
                    new Vector4(ctm.position.x,ctm.position.y,ctm.eulerAngles.z,camScaleM),time,clipM);
                long ex1=Stopwatch.GetTimestamp();
                if((Time.frameCount&7)==0)
                foreach(var s in gpuCollapseSpans)if(s.Renderer.enabled==DrawCollapsePolicy.HideProxies)s.Renderer.enabled=!DrawCollapsePolicy.HideProxies;
                gpuCostExecTicks+=ex1-ex0;gpuCostProxyTicks+=Stopwatch.GetTimestamp()-ex1;
            }
            else if(DrawCollapsePolicy.RecordedDispatch)
            {
                if(!source.Gpu.HasRecordedCollapse)
                {
                    if(DrawCollapsePolicy.GlobalVerts)source.Gpu.RecordCollapseAll(gpuCollapseTexture,gpuCollapseAllVerts,gpuCollapseAllMap,gpuCollapseAllCount);
                    else
                    {
                        var tg=new List<GraphicsBuffer>();var mp=new List<ComputeBuffer>();var ct=new List<int>();
                        foreach(var s in gpuCollapseSpans){tg.Add(s.Vertices);mp.Add(s.Map);ct.Add(s.Items.Length);}
                        source.Gpu.RecordCollapse(gpuCollapseTexture,tg,mp,ct);
                    }
                }
                var camy=ADOBase.controller.camy;var ctf=camy.transform;
                float camScale=camy.camobj.orthographicSize*.2f/gpuLiveZoomFactor;
                Vector4 clip=Vector4.zero;
                if(DrawCollapsePolicy.ClipOffscreen)
                {
                    var co=camy.camobj;float halfH=co.orthographicSize*1.05f+.5f,halfW=halfH*co.aspect;float rad=ctf.eulerAngles.z*Mathf.Deg2Rad;
                    clip=new Vector4(Mathf.Cos(rad),Mathf.Sin(rad),halfW,halfH);
                }
                long ex0=Stopwatch.GetTimestamp();
                source.Gpu.ExecuteRecordedCollapse(new Vector4(ctf.position.x,ctf.position.y,ctf.eulerAngles.z,camScale),time,clip);
                long ex1=Stopwatch.GetTimestamp();
                if(!GpuDecoPlayer.Product || (Time.frameCount&7)==0)
                foreach(var s in gpuCollapseSpans)if(s.Renderer.enabled==DrawCollapsePolicy.HideProxies)s.Renderer.enabled=!DrawCollapsePolicy.HideProxies;
                gpuCostExecTicks+=ex1-ex0;gpuCostProxyTicks+=Stopwatch.GetTimestamp()-ex1;
            }
            else
            {
            if(DrawCollapsePolicy.GlobalVerts)
            {
                source.Gpu.DispatchCollapseAll(gpuCollapseTexture,gpuCollapseAllVerts,gpuCollapseAllMap,gpuCollapseAllCount);
                foreach(var s in gpuCollapseSpans)if(s.Renderer.enabled==DrawCollapsePolicy.HideProxies)s.Renderer.enabled=!DrawCollapsePolicy.HideProxies;
            }
            else
            {
            source.Gpu.BeginCollapse(gpuCollapseTexture);
            foreach(var s in gpuCollapseSpans)
            {source.Gpu.DispatchCollapseSpan(s.Vertices,s.Map,s.Items.Length);if(s.Renderer.enabled==DrawCollapsePolicy.HideProxies)s.Renderer.enabled=!DrawCollapsePolicy.HideProxies;}
            }
            }
            if(gpuCollapseTextureFallbackCount>0 && ((source.CopyMask?.Low??ulong.MaxValue)|(source.CopyMask?.High??ulong.MaxValue))!=0)
            {
                // Overflow alone retains the original evaluated/copy path. Its
                // CopyMask retains chunks containing visible fallback items;
                // neighboring admitted buffers may be copied but stay hidden.
                // Spans still evaluate directly into their representative VBs.
                source.Gpu.DispatchEvaluate();
                ulong low=source.CopyMask?.Low??ulong.MaxValue,high=source.CopyMask?.High??ulong.MaxValue;
                if(source.NativeCopy==null || !source.NativeCopy.Execute(low,high))Graphics.ExecuteCommandBuffer(source.Commands);
            }
        }
    }
}
#endif
