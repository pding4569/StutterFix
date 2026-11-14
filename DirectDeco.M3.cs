using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static unsafe partial class DirectDeco
    {
        // M2 remains source-only diagnostic history; all public dr2 activation uses M3.
        static readonly bool ProxyMode = true;
        static AssetBundle proxyBundle;
        static Shader proxyShader, proxyGrabShader;
        static ComputeBuffer proxyChannels, proxyStatics;
        static Vector4[] proxyChannelData, proxyStaticData;
        static readonly List<DirectProxyPolicy.Row> proxyRows = new List<DirectProxyPolicy.Row>();
        static readonly List<DirectProxyPolicy.Span> proxyPlan = new List<DirectProxyPolicy.Span>();
        static readonly List<int> proxyReturns = new List<int>();
        static readonly List<ProxySpan> proxies = new List<ProxySpan>();
        static readonly List<ProxySpan> previousProxies = new List<ProxySpan>();
        static readonly Dictionary<int, ProxySpan> proxyReuse = new Dictionary<int, ProxySpan>();
        static readonly HashSet<Renderer> proxyRenderers = new HashSet<Renderer>();
        static readonly Dictionary<int, ProxySpan> proxyOfSlot = new Dictionary<int, ProxySpan>();
        static readonly List<Item> proxyBoundsDirty = new List<Item>();
        static readonly List<Item> proxyStaticDirty = new List<Item>();
        static readonly Dictionary<int, Bounds> proxyItemBounds = new Dictionary<int, Bounds>();
        static readonly Dictionary<int, Vector3> proxySortPoints = new Dictionary<int, Vector3>();
        static Vector4 proxyLastCam, proxyLastOrtho;
        static long proxyUploads, proxyUploadBytes, proxyReplans;
        static int proxyTies, proxyVisible, proxyReturnedTies;
        static int proxyTotal, proxyImages;
        static long proxyCreated, proxyReused;
        static readonly Dictionary<Transform, List<Item>> proxyTransformIndex = new Dictionary<Transform, List<Item>>();
        static readonly List<Canvas> proxyCanvases = new List<Canvas>();
        static readonly List<SortingGroup> proxyGroups = new List<SortingGroup>();
        static readonly int proxyVpId = Shader.PropertyToID("_SFDr2VP"), proxyCamId = Shader.PropertyToID("_SFDr2Cam"), proxyOrthoId = Shader.PropertyToID("_SFDr2Ortho");
        sealed class ProxySpan
        {
            internal GameObject Go;
            internal MeshRenderer Renderer;
            internal Mesh Mesh;
            internal Material Material;
            internal ComputeBuffer Slots;
            internal Item[] Items;
            internal bool Dirty = true;
            internal bool Visible;
            internal bool Disposed;
            internal int Texture, SourceLayer, SortingLayer, SortingOrder, Queue;
            internal uint Flags;
        }
        static void EnsureProxyShader()
        {
            if (proxyShader && proxyGrabShader) return;
            using (var stream = typeof(DirectDeco).Assembly.GetManifestResourceStream("StutterFix.directdeco"))
            {
                if (stream == null) throw new InvalidOperationException("DirectDeco shader bundle missing");
                using (var bytes = new MemoryStream()) { stream.CopyTo(bytes); proxyBundle = AssetBundle.LoadFromMemory(bytes.ToArray()); }
            }
            if (!proxyBundle) throw new InvalidOperationException("DirectDeco bundle load failed");
            proxyShader = proxyBundle.LoadAsset<Shader>("Assets/DirectDeco/DirectDeco.shader");
            proxyGrabShader = proxyBundle.LoadAsset<Shader>("Assets/DirectDeco/DirectDecoGrab.shader");
            if (!proxyShader || !proxyGrabShader || !proxyShader.isSupported || !proxyGrabShader.isSupported)
                throw new InvalidOperationException("DirectDeco shader unsupported");
        }
        static void ActivateProxy()
        {
            if (Active) return;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) { Log("M3 건너뜀: D3D11 아님"); return; }
            if (Main.Config.GpuDeco == 1 && GpuDecoPlayer.Available) { Log("M3 건너뜀: GpuDeco 켜짐"); return; }
            if (ADOBase.customLevel == null || ADOBase.controller == null || !ADOBase.controller.disableV15Features) return;
            manager = scrDecorationManager.instance; main = GpuDecoScene.FindMain();
            if (!manager || !main || !main.orthographic || main.rect != new Rect(0, 0, 1, 1)) return;
            managerTransform = manager.transform;
            if (!Identity(managerTransform)) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            EnsureProxyShader(); ResetM2();
            rejectWhy.Clear(); rejected = 0; released = 0; staticFailures = failureLogs = 0;
            itemOf.Clear(); leftovers.Clear(); texSlot.Clear(); texGeom.Clear(); structQueue.Clear();
            generation++; polls = pollDirty = commits = pollTicks = commitTicks = commitMaxTicks = 0;
            proxyUploads = proxyUploadBytes = proxyReplans = proxyCreated = proxyReused = 0; proxyReturnedTies = 0;
            bgValue = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("Bg"));
            fgValue = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("Default"));
            var candidates = new List<Item>(manager.allDecorations.Count);
            proxyTotal = manager.allDecorations.Count; proxyImages = 0;
            foreach (var d in manager.allDecorations)
            {
                if (d && d.GetType() == typeof(scrVisualDecoration)) proxyImages++;
                string why = Classify(d);
                if (why != null) { rejected++; Count(why); continue; }
                var v = (scrVisualDecoration)d; var r = v.spriteRenderer;
                if (Only == 1 && r.sortingLayerName != "Bg" || Only == 2 && r.sortingLayerName == "Bg") { rejected++; Count("진단: 한쪽만"); continue; }
                candidates.Add(new Item { D = v, R = r, M = v.spriteMask, P = d.parallax, Child = d.childTransform,
                    BackForceR = r.forceRenderingOff, BackForceM = v.spriteMask && v.spriteMask.forceRenderingOff });
            }
            int cap = Math.Max(1, candidates.Count);
            channels = new float[cap * ChannelFloats]; statics = new float[cap * StaticFloats]; dirty = new uint[cap]; dirtyStamp = new int[cap];
            hChannels = GCHandle.Alloc(channels, GCHandleType.Pinned); pChannels = (float*)hChannels.AddrOfPinnedObject();
            hStatics = GCHandle.Alloc(statics, GCHandleType.Pinned); pStatics = (float*)hStatics.AddrOfPinnedObject();
            proxyChannelData = new Vector4[cap * 4]; proxyStaticData = new Vector4[cap * 8];
            proxyChannels = new ComputeBuffer(cap * 4, 16); proxyStatics = new ComputeBuffer(cap * 8, 16);
            items = candidates.ToArray(); dirtyCount = 0; stamp = 1;
            Active = true; level = ADOBase.customLevel.levelData; camFrame = -1; CaptureCamera();
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i]; it.Slot = i; it.Layer = int.MinValue; itemOf[it.D] = it;
                nativeRendererIndex[it.R] = it;
                if (LazySprite.Count > 0 && rendererOn(it.D) && rendererColor(it.D).a != 0f) LazySprite.Flush(it.D);
                Refresh(it);
                for (int k = 0; k < ChannelFloats; k++) pChannels[i * ChannelFloats + k] = float.NaN;
                if (it.Released) continue;
                Poll(it); it.R.forceRenderingOff = true;
                it.ProxyLifetime = it.D.gameObject.AddComponent<DirectDecoLifetime>();
                it.ProxyLifetime.Changed = _ => { if (Active && !it.Released) { if (it.D && it.R) Poll(it); else ReleaseItem(it, "M3 파괴됨"); } };
            }
            FlushProxyBounds(); IndexProxyBarriers();
            RebuildProxyOrder(); UploadProxyData(); UpdateProxySpans();
            PrepareM2();
            activationOwned = itemOf.Count; RecountOwned(); prepTicks = sw.ElapsedTicks;
            polls = pollDirty = pollTicks = 0;
            proxyLastCam = camLogic; proxyLastOrtho = orthoLogic; orderDirty = listDirty = boundaryDirty = cbDirty = false;
            Log("M3 켜짐: " + ProxyState() + " 준비 " + sw.ElapsedMilliseconds + "ms");
        }
        static void QueueProxyStatic(Item it)
        {
            if (!it.ProxyStaticQueued) { it.ProxyStaticQueued = true; proxyStaticDirty.Add(it); }
            QueueProxyBounds(it);
        }
        static void QueueProxyBounds(Item it)
        {
            if (it.Released) return;
            if (!it.ProxyBoundsQueued) { it.ProxyBoundsQueued = true; proxyBoundsDirty.Add(it); }
            if (proxyOfSlot.TryGetValue(it.Slot, out var sp)) sp.Dirty = true;
        }
        static void ProxyVisibility(Item it)
        {
            float on = it.Drawable && !it.BackForceR && it.R && it.R.enabled ? 1f : 0f;
            int index = it.Slot * StaticFloats + 28;
            if (statics[index] != on) { statics[index] = on; QueueProxyStatic(it); }
        }
        static void UpdateProxyItemBounds(Item it)
        {
            if (it.Released || !it.Ok || !it.D || !it.R) return;
            ShaderBasis(it, out Vector3 a, out Vector3 x, out Vector3 y);
            Vector3 b = x + a, c = y + a, d = (x + y) + a;
            Bounds bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c); bounds.Encapsulate(d);
            if (!Finite(bounds.center) || !Finite(bounds.extents)) { ReleaseItem(it, "M3 비유한 위치/크기"); return; }
            proxyItemBounds[it.Slot] = bounds;
            Vector3 center = bounds.center;
            if (it.R.spriteSortPoint == SpriteSortPoint.Pivot && it.R.sprite)
            {
                var sb = it.R.sprite.bounds;
                center = (x * (-sb.min.x / sb.size.x) + y * (-sb.min.y / sb.size.y)) + a;
            }
            proxySortPoints[it.Slot] = center;
            var old = it.Key;
            it.Layer = SortingLayer.GetLayerValueFromID(it.R.sortingLayerID); it.Order = it.R.sortingOrder;
            var mode = main.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = TransparencySortMode.Orthographic;
            // Perspective sorting on an orthographic camera is legal and requires squared distance.
            float distance = mode == TransparencySortMode.Perspective ? (center - main.transform.position).sqrMagnitude : Vector3.Dot(center,
                mode == TransparencySortMode.CustomAxis ? (main.transparencySortMode == TransparencySortMode.CustomAxis ? main.transparencySortAxis : GraphicsSettings.transparencySortAxis) : main.transform.forward);
            it.Key = new SceneRenderOrder.Key(it.Layer, it.Order, it.R.sharedMaterial.renderQueue, distance,
                it.R.sharedMaterial.GetInstanceID(), it.R.GetInstanceID(), 0, SceneRenderOrder.ShaderRank(it.R.sharedMaterial));
            if (old.CompareTo(it.Key) != 0) orderDirty = true;
        }
        static void FlushProxyBounds()
        {
            foreach (var it in proxyBoundsDirty) { it.ProxyBoundsQueued = false; UpdateProxyItemBounds(it); }
            proxyBoundsDirty.Clear();
        }
        static void RebuildProxyOrder()
        {
                CheckProxyReservedOrders(); FlushProxyBounds(); proxyRows.Clear();
                foreach (var it in items) if (!it.Released && it.Ok)
                    proxyRows.Add(new DirectProxyPolicy.Row(it.Key, it.Slot, it.Tex, it.Flags, it.R.gameObject.layer));
                foreach (var lf in leftovers)
                {
                    if (!lf.R) continue;
                    for (int k = 0; k < lf.Materials.Length; k++) if (lf.Materials[k]) proxyRows.Add(new DirectProxyPolicy.Row(lf.MaterialEntries[k].Key, -1, -1, 0, lf.GoLayer));
                }
                proxyRows.Sort((a, b) => a.Key.CompareTo(b.Key));
                proxyTies = DirectProxyPolicy.Plan(proxyRows, proxyPlan, proxyReturns);
                foreach (int slot in proxyReturns) { ReleaseItem(items[slot], "M3 동점 재질/장벽"); proxyReturnedTies++; }
                previousProxies.Clear(); previousProxies.AddRange(proxies); proxyReuse.Clear();
                foreach (var sp in previousProxies) proxyReuse[sp.Items[0].Slot] = sp;
                proxies.Clear(); proxyOfSlot.Clear();
                foreach (var span in proxyPlan)
                {
                    var first = items[proxyRows[span.Start].Slot];
                    if (proxyReuse.TryGetValue(first.Slot, out var reuse) && ProxySpanFits(reuse, span))
                    {
                        proxies.Add(reuse); proxyReuse.Remove(first.Slot); proxyReused++;
                        foreach (var it in reuse.Items) proxyOfSlot[it.Slot] = reuse;
                    }
                    else CreateProxySpan(span);
                }
                foreach (var sp in proxyReuse.Values) DestroyProxySpan(sp);
                previousProxies.Clear(); proxyReuse.Clear();
                totalSegs = proxies.Count; proxyReplans++; orderDirty = false;
        }
        static bool ProxySpanFits(ProxySpan sp, DirectProxyPolicy.Span plan)
        {
            var first = items[proxyRows[plan.Start].Slot];
            if (sp.Items.Length != plan.Count || sp.Texture != first.Tex || sp.Flags != first.Flags || sp.SourceLayer != first.R.gameObject.layer
                || sp.SortingLayer != first.R.sortingLayerID || sp.SortingOrder != first.R.sortingOrder || sp.Queue != first.Key.Queue) return false;
            for (int k = 0; k < plan.Count; k++) if (sp.Items[k].Slot != proxyRows[plan.Start + k].Slot) return false;
            return true;
        }
        static void CreateProxySpan(DirectProxyPolicy.Span span)
        {
            // Register immediately so activation exceptions always have a complete cleanup owner.
            var sp = new ProxySpan(); proxies.Add(sp);
            sp.Items = new Item[span.Count]; uint[] slots = new uint[span.Count];
            var vertices = new Vector3[span.Count * 4]; var uv = new Vector2[span.Count * 4]; var indices = new int[span.Count * 6];
            for (int k = 0; k < span.Count; k++)
            {
                int slot = proxyRows[span.Start + k].Slot; sp.Items[k] = items[slot]; slots[k] = (uint)slot; proxyOfSlot[slot] = sp;
                uv[k * 4] = Vector2.zero; uv[k * 4 + 1] = Vector2.right; uv[k * 4 + 2] = Vector2.up; uv[k * 4 + 3] = Vector2.one;
                int v = k * 4, j = k * 6;
                indices[j] = v; indices[j + 1] = v + 2; indices[j + 2] = v + 1;
                indices[j + 3] = v + 1; indices[j + 4] = v + 2; indices[j + 5] = v + 3;
            }
            var first = sp.Items[0]; bool grab = (first.Flags >> BlendShift) != 0, add = (first.Flags & FAdd) != 0;
            sp.Texture = first.Tex; sp.Flags = first.Flags; sp.SourceLayer = first.R.gameObject.layer;
            sp.SortingLayer = first.R.sortingLayerID; sp.SortingOrder = first.R.sortingOrder; sp.Queue = first.Key.Queue; proxyCreated++;
            sp.Material = new Material(grab ? proxyGrabShader : proxyShader) { name = "StutterFix.DirectDeco", renderQueue = first.Key.Queue };
            sp.Material.SetTexture("image", first.R.sprite.texture);
            sp.Material.SetFloat("_Point", (first.Flags & FPoint) != 0 ? 1 : 0); sp.Material.SetFloat("_Add", add ? 1 : 0);
            sp.Material.SetInt("_SrcBlend", (int)(add ? BlendMode.SrcAlpha : BlendMode.One));
            sp.Material.SetInt("_DstBlend", (int)(add ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            sp.Material.SetInt("_ColorMask", add ? 7 : 15);
            sp.Material.SetInt("_StencilRef", 0);
            sp.Material.SetInt("_StencilComp", (int)((first.Flags & FInside) != 0 ? CompareFunction.NotEqual : (first.Flags & FOutside) != 0 ? CompareFunction.Equal : CompareFunction.Always));
            sp.Slots = new ComputeBuffer(span.Count, 4); sp.Slots.SetData(slots);
            sp.Material.SetBuffer("channels", proxyChannels); sp.Material.SetBuffer("statics", proxyStatics); sp.Material.SetBuffer("_Slots", sp.Slots);
            sp.Mesh = new Mesh { name = "DirectDeco span", indexFormat = IndexFormat.UInt32 };
            sp.Mesh.vertices = vertices; sp.Mesh.uv = uv; sp.Mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            sp.Go = new GameObject("StutterFix.DirectDeco.Proxy") { hideFlags = HideFlags.HideAndDontSave, layer = first.R.gameObject.layer };
            sp.Go.AddComponent<MeshFilter>().sharedMesh = sp.Mesh;
            sp.Renderer = sp.Go.AddComponent<MeshRenderer>(); proxyRenderers.Add(sp.Renderer);
            sp.Renderer.sharedMaterial = sp.Material; sp.Renderer.sortingLayerID = first.R.sortingLayerID; sp.Renderer.sortingOrder = first.R.sortingOrder;
            sp.Renderer.shadowCastingMode = ShadowCastingMode.Off; sp.Renderer.receiveShadows = false;
            sp.Renderer.lightProbeUsage = LightProbeUsage.Off; sp.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sp.Renderer.allowOcclusionWhenDynamic = false;
        }
        static void UploadProxyData()
        {
            // Slot granularity, no full-buffer upload on stable frames. Vector4 arrays match StructuredBuffer stride.
            Array.Sort(dirty, 0, dirtyCount);
            for (int d = 0; d < dirtyCount; d++)
            {
                int slot = (int)dirty[d], first = slot * 4;
                for (int k = 0; k < 4; k++) { int p = slot * 16 + k * 4; proxyChannelData[first + k] = new Vector4(channels[p], channels[p + 1], channels[p + 2], channels[p + 3]); }
            }
            for (int d = 0; d < dirtyCount;)
            {
                int start = (int)dirty[d], end = start + 1; d++;
                while (d < dirtyCount && dirty[d] == end) { end++; d++; }
                proxyChannels.SetData(proxyChannelData, start * 4, start * 4, (end - start) * 4); proxyUploads++; proxyUploadBytes += (end - start) * 64;
            }
            proxyStaticDirty.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            foreach (var it in proxyStaticDirty)
            {
                it.ProxyStaticQueued = false; int first = it.Slot * 8;
                for (int k = 0; k < 8; k++) { int p = it.Slot * 32 + k * 4; proxyStaticData[first + k] = new Vector4(statics[p], statics[p + 1], statics[p + 2], statics[p + 3]); }
            }
            for (int d = 0; d < proxyStaticDirty.Count;)
            {
                int start = proxyStaticDirty[d].Slot, end = start + 1; d++;
                while (d < proxyStaticDirty.Count && proxyStaticDirty[d].Slot == end) { end++; d++; }
                proxyStatics.SetData(proxyStaticData, start * 8, start * 8, (end - start) * 8); proxyUploads++; proxyUploadBytes += (end - start) * 128;
            }
            proxyStaticDirty.Clear(); dirtyCount = 0;
            if (++stamp == int.MaxValue) { Array.Clear(dirtyStamp, 0, dirtyStamp.Length); stamp = 1; }
        }
        static void UpdateProxySpans()
        {
            proxyVisible = 0;
            var vp = GL.GetGPUProjectionMatrix(main.projectionMatrix, main.targetTexture != null) * main.worldToCameraMatrix;
            Shader.SetGlobalMatrix(proxyVpId, vp); Shader.SetGlobalVector(proxyCamId, camLogic); Shader.SetGlobalVector(proxyOrthoId, orthoLogic);
            foreach (var sp in proxies)
            {
                if (sp.Dirty)
                {
                    Vector3 center = proxySortPoints[sp.Items[0].Slot], extents = Vector3.zero; bool visible = false;
                    foreach (var it in sp.Items) if (!it.Released && statics[it.Slot * 32 + 28] != 0)
                    {
                        Bounds b = proxyItemBounds[it.Slot];
                        extents = Vector3.Max(extents, Vector3.Max(Abs(b.min - center), Abs(b.max - center))); visible = true;
                    }
                    sp.Go.transform.position = center;
                    // Symmetric conservative union keeps the sort center EXACTLY at the first source.
                    sp.Renderer.bounds = new Bounds(center, extents * 2 + Vector3.one * 2e-4f);
                    sp.Renderer.enabled = visible; sp.Visible = visible; sp.Dirty = false;
                }
                if (sp.Visible) proxyVisible++;
            }
        }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        static void CommitProxy()
        {
            if (!main || !manager || !ReferenceEquals(level, ADOBase.customLevel?.levelData)) { Deactivate("맵 바뀜"); return; }
            RefreshProxyChanges(); CaptureCamera();
            if (!proxyLastCam.Equals(camLogic) || !proxyLastOrtho.Equals(orthoLogic) || !sequenceSortSignature.Equals(SortSignature()))
            {
                foreach (var it in items) if (!it.Released) QueueProxyBounds(it);
                if (!sequenceSortSignature.Equals(SortSignature())) { foreach (var lf in leftovers) if (lf.R) RefreshProxyLeftover(lf); orderDirty = true; }
                proxyLastCam = camLogic; proxyLastOrtho = orthoLogic; sequenceSortSignature = SortSignature();
            }
            for (int i = 0; i < structQueue.Count; i++) { var it = structQueue[i]; if (!it.D || !it.R) { ReleaseItem(it, "M3 파괴됨"); continue; } Refresh(it); if (!it.Released) Poll(it); }
            structQueue.Clear();
            FlushProxyBounds();
            if (orderDirty) RebuildProxyOrder();
            UploadProxyData(); UpdateProxySpans(); commits++;
            listDirty = boundaryDirty = cbDirty = false;
        }
        static void RefreshProxyChanges()
        {
            if (createdObjects.Count > 0)
            {
                // Hierarchy mutation is rare; rescan only then, never in a stable frame.
                IndexProxyBarriers(); createdObjects.Clear(); orderDirty = true;
                foreach (var it in items) if (!it.Released) QueueStruct(it);
            }
            foreach (var lf in changedRenderers) { lf.M2Dirty = false; if (lf.R) RefreshProxyLeftover(lf); orderDirty = true; }
            changedRenderers.Clear();
        }
        static void RefreshProxyLeftover(Leftover lf)
        {
            lf.R.GetSharedMaterials(lf.MaterialScratch);
            bool changed = lf.MaterialScratch.Count != lf.Materials.Length;
            for (int k = 0; !changed && k < lf.Materials.Length; k++) changed = !ReferenceEquals(lf.Materials[k], lf.MaterialScratch[k]);
            if (changed)
            {
                lf.Materials = lf.MaterialScratch.ToArray(); lf.MaterialEntries = new Entry[lf.Materials.Length];
                for (int k = 0; k < lf.Materials.Length; k++) lf.MaterialEntries[k] = new Entry { Left = lf, MaterialIndex = k };
                IndexMaterials(lf);
            }
            lf.GoLayer = lf.Go.layer;
            for (int k = 0; k < lf.Materials.Length; k++) lf.MaterialEntries[k].Key = SequenceKey(lf.R, lf.Materials[k], main, k);
        }
        static void CheckProxyReservedOrders()
        {
            // Group/Canvas order is not the individual Renderer key. Reserve only matching layer/order.
            foreach (var c in proxyCanvases) if (c)
                foreach (var it in items) if (!it.Released && it.Layer == SortingLayer.GetLayerValueFromID(c.sortingLayerID) && it.Order == c.sortingOrder)
                    ReleaseItem(it, "M3 Canvas 같은 정렬 자리");
            foreach (var group in proxyGroups) if (group)
                foreach (var it in items) if (!it.Released && it.Layer == SortingLayer.GetLayerValueFromID(group.sortingLayerID) && it.Order == group.sortingOrder)
                    ReleaseItem(it, "M3 SortingGroup 같은 정렬 자리");
        }
        static void IndexProxyBarriers()
        {
            leftovers.Clear(); rendererIndex.Clear(); transformIndex.Clear(); materialIndex.Clear();
            proxyTransformIndex.Clear();
            foreach (var it in items) if (!it.Released && it.R)
                for (var t = it.R.transform; t; t = t.parent)
                {
                    if (!proxyTransformIndex.TryGetValue(t, out var indexed)) proxyTransformIndex[t] = indexed = new List<Item>();
                    indexed.Add(it);
                }
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || r is SpriteMask || proxyRenderers.Contains(r) || nativeRendererIndex.TryGetValue(r, out var own) && !own.Released) continue;
                if ((main.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                var vd = r.GetComponentInParent<scrVisualDecoration>(true);
                if (vd && ReferenceEquals(r, vd.meshRenderer) && !meshOn(vd)) continue;
                var lf = MakeLeftover(r); lf.Seq = false; leftovers.Add(lf); RefreshProxyLeftover(lf); IndexRenderer(lf);
            }
            // CanvasRenderer is not a Renderer. Its layer/order is reserved locally, without global boundaries.
            proxyCanvases.Clear(); proxyGroups.Clear();
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!c || !c.gameObject.scene.IsValid() || c.renderMode != RenderMode.WorldSpace || !c.isRootCanvas || (main.cullingMask & (1 << c.gameObject.layer)) == 0) continue;
                proxyCanvases.Add(c);
            }
            foreach (var group in Resources.FindObjectsOfTypeAll<SortingGroup>()) if (group && group.gameObject.scene.IsValid()) proxyGroups.Add(group);
        }
        static void ClearProxySpans()
        {
            foreach (var sp in proxies) DestroyProxySpan(sp);
            foreach (var sp in previousProxies) DestroyProxySpan(sp);
            proxies.Clear(); previousProxies.Clear(); proxyReuse.Clear(); proxyOfSlot.Clear(); proxyRenderers.Clear();
        }
        static void DestroyProxySpan(ProxySpan sp)
        {
                if (sp.Disposed) return; sp.Disposed = true;
                proxyRenderers.Remove(sp.Renderer);
                if (sp.Renderer) sp.Renderer.enabled = false;
                if (sp.Go) UnityEngine.Object.Destroy(sp.Go);
                if (sp.Mesh) UnityEngine.Object.Destroy(sp.Mesh);
                if (sp.Material) UnityEngine.Object.Destroy(sp.Material);
                sp.Slots?.Dispose();
        }
        static void ReleaseProxy()
        {
            ClearProxySpans(); proxyChannels?.Dispose(); proxyChannels = null; proxyStatics?.Dispose(); proxyStatics = null;
            foreach (var it in items) if (it.ProxyLifetime) { it.ProxyLifetime.Changed = null; UnityEngine.Object.Destroy(it.ProxyLifetime); }
            proxyBoundsDirty.Clear(); proxyStaticDirty.Clear(); proxyItemBounds.Clear(); proxySortPoints.Clear(); proxyRows.Clear(); proxyPlan.Clear(); proxyReturns.Clear();
            proxyChannelData = proxyStaticData = null;
            proxyTransformIndex.Clear();
            proxyCanvases.Clear(); proxyGroups.Clear();
            if (proxyBundle) proxyBundle.Unload(true); proxyBundle = null; proxyShader = proxyGrabShader = null;
        }
        static string ProxyState()
        {
            double tick = 1e6 / System.Diagnostics.Stopwatch.Frequency;
            RecountOwned(); var reasons = new StringBuilder();
            foreach (var pair in rejectWhy) { if (reasons.Length > 0) reasons.Append(", "); reasons.Append(pair.Key).Append('=').Append(pair.Value); }
            return "active=" + Active + " mode=M3-proxy enabled=" + Enabled + " owned=" + itemOf.Count + " imageTotal=" + proxyImages + " total=" + proxyTotal + " preparedSlots=" + items.Length + " proxies=" + proxies.Count + " visibleProxies=" + proxyVisible
                + " bg=" + ownedBg + " fg=" + ownedFg + " masked=" + ownedMasked + " add=" + ownedAdd
                + " tieBuckets=" + proxyTies + " returnedTies=" + proxyReturnedTies + " released=" + released + " rejected=" + rejected + " reasons=" + reasons
                + " replans=" + proxyReplans + " createdProxies=" + proxyCreated + " reusedProxies=" + proxyReused + " drawRenderer=0 hiddenLayers=0 nativeEvents=0 uploads=" + proxyUploads + " uploadBytes=" + proxyUploadBytes
                + " commit_us=" + (commitTicks * tick / Math.Max(1, commits)).ToString("F1") + " commit_max_us=" + (commitMaxTicks * tick).ToString("F1")
                + " poll_us/frame=" + (pollTicks * tick / Math.Max(1, commits)).ToString("F1") + " prep_us=" + (prepTicks * tick).ToString("F1")
                + " mutationMethods=" + mutationMethods + " maskPolicy=Unity-original (runtime drPix required)";
        }
        static string ProxySequenceCheck()
        {
            if (!Active) return "seqcheck inactive";
            int stale = 0, missing = 0, order = 0, ties = 0, reserved = 0;
            var seen = new HashSet<int>();
            var reference = new List<DirectProxyPolicy.Row>();
            foreach (var it in items) if (!it.Released && it.Ok)
            {
                if (Classify(it.D) != null) stale++;
                var old = it.Key; UpdateProxyItemBounds(it); if (old.CompareTo(it.Key) != 0) stale++;
                reference.Add(new DirectProxyPolicy.Row(it.Key, it.Slot, it.Tex, it.Flags, it.R.gameObject.layer));
            }
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || r is SpriteMask || proxyRenderers.Contains(r) || nativeRendererIndex.TryGetValue(r, out var it) && !it.Released) continue;
                if ((main.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                var vd = r.GetComponentInParent<scrVisualDecoration>(true); if (vd && ReferenceEquals(r, vd.meshRenderer) && !meshOn(vd)) continue;
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) if (mats[k]) reference.Add(new DirectProxyPolicy.Row(SequenceKey(r, mats[k], main, k), -1, -1, 0, r.gameObject.layer));
            }
            reference.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>()) if (c && c.gameObject.scene.IsValid() && c.isRootCanvas && c.renderMode == RenderMode.WorldSpace && (main.cullingMask & (1 << c.gameObject.layer)) != 0)
                foreach (var it in items) if (!it.Released && it.Layer == SortingLayer.GetLayerValueFromID(c.sortingLayerID) && it.Order == c.sortingOrder) reserved++;
            foreach (var group in Resources.FindObjectsOfTypeAll<SortingGroup>()) if (group && group.gameObject.scene.IsValid())
                foreach (var it in items) if (!it.Released && it.Layer == SortingLayer.GetLayerValueFromID(group.sortingLayerID) && it.Order == group.sortingOrder) reserved++;
            var plan = new List<DirectProxyPolicy.Span>(); var returns = new List<int>(); ties = DirectProxyPolicy.Plan(reference, plan, returns);
            if (returns.Count != 0) order += returns.Count;
            if (plan.Count != proxies.Count) order++;
            for (int p = 0; p < proxies.Count; p++)
            {
                var sp = proxies[p];
                if (p >= plan.Count || plan[p].Count != sp.Items.Length) { order++; continue; }
                for (int k = 0; k < sp.Items.Length; k++)
                {
                    var it = sp.Items[k]; if (!seen.Add(it.Slot)) order++;
                    if (it.Slot != reference[plan[p].Start + k].Slot) order++;
                }
                if (sp.Renderer && Vector3.Distance(sp.Renderer.bounds.center, proxySortPoints[sp.Items[0].Slot]) > 1e-6f) stale++;
            }
            foreach (var it in items) if (!it.Released && it.Ok && !seen.Contains(it.Slot)) missing++;
            return "seqcheck M3 proxies=" + proxies.Count + " entries=" + seen.Count + " stale=" + stale + " missing=" + missing + " orderMismatch=" + order
                + " tieBuckets=" + ties + " reservedOrderConflicts=" + reserved + " pass=" + (stale == 0 && missing == 0 && order == 0 && reserved == 0) + " (independent scene snapshot; GPU pixels require drPix)";
        }
    }
}
