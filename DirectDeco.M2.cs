using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace StutterFix
{
    internal static unsafe partial class DirectDeco
    {
        static bool splitSequence, canvasDirty;
        static long recordTicks;
        static int drawRendererCommands, grabCommands, mutationMethods, mutationFailures;
        static readonly int grabRT = Shader.PropertyToID("_SFDr2Grab");
        static readonly int grabTexture = Shader.PropertyToID("_GrabTexture");
        static readonly Dictionary<Renderer, Leftover> rendererIndex = new Dictionary<Renderer, Leftover>();
        static readonly Dictionary<Transform, List<Leftover>> transformIndex = new Dictionary<Transform, List<Leftover>>();
        static readonly Dictionary<Material, List<Leftover>> materialIndex = new Dictionary<Material, List<Leftover>>();
        static readonly Dictionary<Material, List<Item>> nativeMaterialIndex = new Dictionary<Material, List<Item>>();
        static readonly Dictionary<Renderer, Item> nativeRendererIndex = new Dictionary<Renderer, Item>();
        static readonly List<Leftover> changedRenderers = new List<Leftover>();
        static readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        static Vector4 sequenceSortSignature;
        static int sequenceWidth, sequenceHeight;
        // Orthographic translation adds the same constant to every distance. Omitting that
        // constant preserves the exact comparison and avoids a full reorder on every pan.
        static SceneRenderOrder.Key SequenceKey(Renderer r, Material m, Camera camera, int slot = 0)
        {
            var mode = camera.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = camera.orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective;
            Vector3 pos = r.bounds.center;
            if (r is SpriteRenderer sr && sr.spriteSortPoint == SpriteSortPoint.Pivot) pos = r.transform.position;
            float distance = mode == TransparencySortMode.Perspective ? (pos - camera.transform.position).sqrMagnitude : Vector3.Dot(pos,
                mode == TransparencySortMode.CustomAxis ? (camera.transparencySortMode == TransparencySortMode.CustomAxis ? camera.transparencySortAxis : GraphicsSettings.transparencySortAxis) : camera.transform.forward);
            return new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(r.sortingLayerID), r.sortingOrder, m ? m.renderQueue : -1, distance,
                m ? m.GetInstanceID() : 0, r.GetInstanceID(), slot, SceneRenderOrder.ShaderRank(m));
        }
        static Vector4 SortSignature()
        {
            var mode = main.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = main.orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective;
            Vector3 v = mode == TransparencySortMode.Perspective ? main.transform.position : mode == TransparencySortMode.CustomAxis ?
                (main.transparencySortMode == TransparencySortMode.CustomAxis ? main.transparencySortAxis : GraphicsSettings.transparencySortAxis) : main.transform.forward;
            return new Vector4(v.x, v.y, v.z, (int)mode);
        }
        sealed class CanvasWatch
        {
            internal Canvas Canvas;
            internal Graphic[] Graphics;
            internal CanvasRenderer[] Renderers;
            internal readonly List<DirectDecoLifetime> Lifetimes = new List<DirectDecoLifetime>();
            internal UnityAction Dirty;
            internal UnityAction<bool> Cull;
            internal bool Queued, Reserved;
            internal int Layer, Order;
        }
        static readonly List<CanvasWatch> canvasWatches = new List<CanvasWatch>();
        static readonly Dictionary<Canvas, CanvasWatch> canvasIndex = new Dictionary<Canvas, CanvasWatch>();
        static readonly Dictionary<GameObject, List<CanvasWatch>> canvasGoIndex = new Dictionary<GameObject, List<CanvasWatch>>();
        static readonly List<CanvasWatch> changedCanvases = new List<CanvasWatch>();
        static readonly Vector3[] canvasCorners = new Vector3[4];
        static int canvasActive, canvasGraphics, canvasVisible, canvasCulled, canvasAlphaZero, canvasOffscreen, canvasEmptyMesh;

        static void ResetM2()
        {
            ReleaseM2(); splitSequence = false; recordTicks = 0; drawRendererCommands = grabCommands = 0;
            canvasActive = canvasGraphics = canvasVisible = canvasCulled = canvasAlphaZero = canvasOffscreen = canvasEmptyMesh = 0;
        }
        static void ReleaseM2()
        {
            foreach (var w in canvasWatches)
            {
                foreach (var g in w.Graphics) if (g)
                {
                    g.UnregisterDirtyVerticesCallback(w.Dirty); g.UnregisterDirtyMaterialCallback(w.Dirty);
                    g.UnregisterDirtyLayoutCallback(w.Dirty); if (g is MaskableGraphic mg) mg.onCullStateChanged.RemoveListener(w.Cull);
                }
                foreach (var l in w.Lifetimes) if (l) { l.Changed = null; UnityEngine.Object.Destroy(l); }
            }
            canvasWatches.Clear(); canvasIndex.Clear(); canvasGoIndex.Clear(); changedCanvases.Clear(); rendererIndex.Clear(); transformIndex.Clear(); materialIndex.Clear(); nativeMaterialIndex.Clear(); nativeRendererIndex.Clear(); changedRenderers.Clear(); createdObjects.Clear();
            canvasDirty = false;
        }
        static void AuditCanvas(Canvas c)
        {
            var w = new CanvasWatch { Canvas = c, Graphics = c.GetComponentsInChildren<Graphic>(true), Renderers = c.GetComponentsInChildren<CanvasRenderer>(true) };
            w.Dirty = () => QueueCanvas(w); w.Cull = _ => QueueCanvas(w);
            var gos = new HashSet<GameObject> { c.gameObject };
            foreach (var g in w.Graphics) if (g)
            {
                g.RegisterDirtyVerticesCallback(w.Dirty); g.RegisterDirtyMaterialCallback(w.Dirty);
                g.RegisterDirtyLayoutCallback(w.Dirty); if (g is MaskableGraphic mg) mg.onCullStateChanged.AddListener(w.Cull);
                gos.Add(g.gameObject);
            }
            foreach (var go in gos)
            {
                var l = go.AddComponent<DirectDecoLifetime>(); l.Changed = _ => QueueCanvas(w); w.Lifetimes.Add(l);
                for (var t = go.transform; t; t = t.parent)
                {
                    if (!canvasGoIndex.TryGetValue(t.gameObject, out var ws)) canvasGoIndex[t.gameObject] = ws = new List<CanvasWatch>();
                    if (!ws.Contains(w)) ws.Add(w);
                }
            }
            canvasWatches.Add(w);
            canvasIndex[c] = w;
            w.Reserved = CanvasMayDraw(c) && (main.cullingMask & (1 << c.gameObject.layer)) != 0;
            w.Layer = SortingLayer.GetLayerValueFromID(c.sortingLayerID); w.Order = c.sortingOrder;
        }
        static void QueueCanvas(CanvasWatch w) { if (!w.Queued) { w.Queued = true; changedCanvases.Add(w); } }
        static void MarkCanvases(GameObject go)
        {
            if (canvasGoIndex.TryGetValue(go, out var ws)) foreach (var w in ws) QueueCanvas(w);
        }
        // Cached graphics are revisited only after a UI/lifetime/write/camera notification.
        static bool CanvasMayDraw(Canvas c)
        {
            if (canvasIndex.TryGetValue(c, out var w))
            {
                if (!c.isActiveAndEnabled) return false;
                foreach (var r in w.Renderers)
                {
                    if (!r || !r.gameObject.activeInHierarchy || r.cull || r.GetInheritedAlpha() <= 0 || r.materialCount == 0) continue;
                    var g = r.GetComponent<Graphic>(); if (g && (!g.isActiveAndEnabled || g.color.a <= 0)) continue;
                    var mesh = r.GetMesh(); if (mesh && mesh.vertexCount > 0) return true;
                }
                return false;
            }
            return false;
        }
        static void UpdateCanvasBounds()
        {
            if (canvasDirty) { foreach (var w in canvasWatches) QueueCanvas(w); canvasDirty = false; }
            foreach (var w in changedCanvases)
            {
                w.Queued = false; var c = w.Canvas;
                bool reserve = c && (main.cullingMask & (1 << c.gameObject.layer)) != 0 && CanvasMayDraw(c);
                int layer = c ? SortingLayer.GetLayerValueFromID(c.sortingLayerID) : w.Layer, order = c ? c.sortingOrder : w.Order;
                if (reserve == w.Reserved && (!reserve || layer == w.Layer && order == w.Order)) continue;
                w.Reserved = reserve; w.Layer = layer; w.Order = order;
                if (reserve) { if (!canvasBounds.Contains(c)) canvasBounds.Add(c); } else canvasBounds.Remove(c);
                boundaryDirty = true;
            }
            changedCanvases.Clear();
        }
        static string CanvasAudit()
        {
            canvasActive = canvasGraphics = canvasVisible = canvasCulled = canvasAlphaZero = canvasOffscreen = canvasEmptyMesh = 0;
            var names = new Dictionary<string, int>();
            foreach (var w in canvasWatches)
            {
                var c = w.Canvas; if (!c || !c.isActiveAndEnabled) continue; canvasActive++;
                foreach (var g in w.Graphics)
                {
                    if (!g || !g.isActiveAndEnabled) continue;
                    canvasGraphics++; string type = g.GetType().FullName; names.TryGetValue(type, out int n); names[type] = n + 1;
                    var r = g.canvasRenderer;
                    var mesh = r.GetMesh();
                    if (!mesh || mesh.vertexCount == 0 || r.materialCount == 0) { canvasEmptyMesh++; continue; }
                    if (r.cull) { canvasCulled++; continue; }
                    if (r.GetInheritedAlpha() <= 0 || g.color.a <= 0) { canvasAlphaZero++; continue; }
                    g.rectTransform.GetWorldCorners(canvasCorners); bool on = false;
                    float xmin = float.PositiveInfinity, ymin = xmin, xmax = float.NegativeInfinity, ymax = xmax;
                    foreach (var p in canvasCorners)
                    {
                        var v = main.WorldToViewportPoint(p); if (v.z < main.nearClipPlane || v.z > main.farClipPlane) continue;
                        xmin = Mathf.Min(xmin, v.x); xmax = Mathf.Max(xmax, v.x); ymin = Mathf.Min(ymin, v.y); ymax = Mathf.Max(ymax, v.y);
                        on = true;
                    }
                    if (!on || xmax < 0 || xmin > 1 || ymax < 0 || ymin > 1) { canvasOffscreen++; continue; }
                    canvasVisible++;
                    if (canvasVisible <= 10) Log("canvas visible" + canvasVisible + " root=" + c.name + " graphic=" + g.name + " type=" + type + " vertices=" + mesh.vertexCount + " alpha=" + r.GetInheritedAlpha());
                }
            }
            return "canvas roots=" + canvasWatches.Count + " active=" + canvasActive + " graphics=" + canvasGraphics + " visibleCandidates=" + canvasVisible
                + " emptyMesh=" + canvasEmptyMesh + " cull=" + canvasCulled + " zeroAlpha=" + canvasAlphaZero + " offscreen=" + canvasOffscreen + " types=" + WhyText(names)
                + " (snapshot; visibleCandidates is not a GPU draw count)";
        }
        static void IndexRenderer(Leftover lf)
        {
            if (rendererIndex.TryGetValue(lf.R, out var previous) && ReferenceEquals(previous, lf)) { MarkChanged(lf); return; }
            rendererIndex[lf.R] = lf;
            IndexMaterials(lf);
            for (var t = lf.R.transform; t; t = t.parent)
            {
                if (!transformIndex.TryGetValue(t, out var ls)) transformIndex[t] = ls = new List<Leftover>();
                ls.Add(lf);
            }
            MarkChanged(lf);
        }
        static void IndexMaterials(Leftover lf)
        {
            foreach (var m in lf.Materials) if (m)
            {
                if (!materialIndex.TryGetValue(m, out var ls)) materialIndex[m] = ls = new List<Leftover>();
                if (!ls.Contains(lf)) ls.Add(lf);
            }
        }
        static void IndexNativeMaterial(Item it)
        {
            if (it.R) nativeRendererIndex[it.R] = it;
            if (it.M) nativeRendererIndex[it.M] = it;
            var m = it.R ? it.R.sharedMaterial : null; if (!m) return;
            if (!nativeMaterialIndex.TryGetValue(m, out var ls)) nativeMaterialIndex[m] = ls = new List<Item>();
            if (!ls.Contains(it)) ls.Add(it);
        }
        static void MarkChanged(Leftover lf)
        {
            if (lf.M2Dirty) return;
            lf.M2Dirty = true; changedRenderers.Add(lf);
        }
        static void RendererChanged(Renderer r) { if (Active && r && rendererIndex.TryGetValue(r, out var lf)) MarkChanged(lf); }
        static void TransformChanged(Transform t)
        {
            if (!Active || !t) return;
            if (ProxyMode)
            {
                if (proxyTransformIndex.TryGetValue(t, out var owned)) foreach (var it in owned) if (!it.Released) QueueStruct(it);
            }
            if (transformIndex.TryGetValue(t, out var ls)) foreach (var lf in ls) MarkChanged(lf);
        }
        static void RefreshChangedRenderers()
        {
            DiscoverCreatedObjects();
            // A rotated/custom-axis camera can change relative distance order. Its one
            // matrix comparison replaces polling every renderer in a stationary scene.
            if (sequenceSortSignature != SortSignature())
            {
                sequenceSortSignature = SortSignature();
                foreach (var lf in sequenced) MarkChanged(lf);
                foreach (var it in items) if (!it.Released) it.Key = SequenceKey(it.R, it.R.sharedMaterial, main);
                orderDirty = true;
            }
            int width = main.targetTexture ? main.targetTexture.width : main.pixelWidth, height = main.targetTexture ? main.targetTexture.height : main.pixelHeight;
            if (width != sequenceWidth || height != sequenceHeight) { sequenceWidth = width; sequenceHeight = height; cbDirty = true; }
            UpdateCanvasBounds();
            for (int i = 0; i < changedRenderers.Count; i++)
            {
                var lf = changedRenderers[i]; lf.M2Dirty = false;
                if (!lf.R) { SetLeftoverVisible(lf); continue; }
                if (!lf.Suppressed)
                {
                    lf.R.GetSharedMaterials(lf.MaterialScratch);
                    bool different = lf.MaterialScratch.Count != lf.Materials.Length;
                    for (int k = 0; !different && k < lf.Materials.Length; k++) different = !ReferenceEquals(lf.MaterialScratch[k], lf.Materials[k]);
                    bool maskedNow = lf.R is SpriteRenderer sprite && sprite.maskInteraction != SpriteMaskInteraction.None || lf.R is ParticleSystemRenderer particles && particles.maskInteraction != SpriteMaskInteraction.None;
                    if (different || maskedNow != lf.Masked)
                    {
                        var now = MakeLeftover(lf.R); lf.Seq = now.Seq; lf.Masked = now.Masked; lf.Materials = now.Materials;
                        lf.MaterialEntries = now.MaterialEntries; foreach (var e in lf.MaterialEntries) e.Left = lf;
                        IndexMaterials(lf);
                    }
                    lf.Key = SequenceKey(lf.R, lf.R.sharedMaterial, main); lf.Bg = lf.Key.Layer <= bgValue;
                    if (!lf.Seq) { if (AddLimit(lf)) boundaryDirty = true; continue; }
                    if (!WithinLimits(lf.Bg, lf.Key)) continue;
                    SequenceLeftover(lf); orderDirty = true;
                }
                SetLeftoverVisible(lf); if (!lf.R) continue;
                if (lf.Go.layer != HiddenLayer) { movedLayers[lf.Go] = lf.Go.layer; lf.Go.layer = HiddenLayer; }
                lf.R.GetSharedMaterials(lf.MaterialScratch);
                bool materials = lf.MaterialScratch.Count != lf.Materials.Length;
                for (int k = 0; !materials && k < lf.Materials.Length; k++) materials = !ReferenceEquals(lf.MaterialScratch[k], lf.Materials[k]);
                var mat = lf.MaterialScratch.Count > 0 ? lf.MaterialScratch[0] : null;
                var key = SequenceKey(lf.R, mat, main);
                bool masked = lf.R is SpriteRenderer sr && sr.maskInteraction != SpriteMaskInteraction.None || lf.R is ParticleSystemRenderer ps && ps.maskInteraction != SpriteMaskInteraction.None;
                if (materials || masked != lf.Masked) RefreshLeftover(lf);
                else if (key.CompareTo(lf.Key) != 0)
                {
                    lf.Key = key; lf.Bg = key.Layer <= bgValue; orderDirty = true;
                    if (!WithinLimits(lf.Bg, key)) RetireSequence(lf.Go);
                }
                foreach (var e in lf.MaterialEntries) if (SequenceKey(lf.R, lf.Materials[e.MaterialIndex], main, e.MaterialIndex).CompareTo(e.Key) != 0) { orderDirty = true; break; }
            }
            changedRenderers.Clear();
        }
        static void PrepareM2()
        {
            sequenceSortSignature = SortSignature();
            sequenceWidth = main.targetTexture ? main.targetTexture.width : main.pixelWidth;
            sequenceHeight = main.targetTexture ? main.targetTexture.height : main.pixelHeight;
            foreach (var lf in changedRenderers) lf.M2Dirty = false;
            changedRenderers.Clear();
        }
        internal static void BeforeSequenceWrite(UnityEngine.Object obj, int kind)
        {
#if DEV || AUTOTEST
            DirectFull.BeforeFullWrite(obj, kind);
#endif
            // DestroyImmediate can make the post-write argument compare equal to null.
            if (!Active || !ProxyMode || !obj || kind != DirectSequenceChanges.Hierarchy) return;
            var go = obj as GameObject; if (!go && obj is Component component) go = component.gameObject;
            if (!go) return;
            TransformChanged(go.transform); createdObjects.Add(go);
        }
        internal static void AfterSequenceWrite(UnityEngine.Object obj, int kind, string writer = null)
        {
#if DEV || AUTOTEST
            DirectFull.NotifyFull(obj, kind, writer);
#endif
            if (!Active || !obj) return;
            if (obj is Renderer r)
            {
                if (nativeRendererIndex.TryGetValue(r, out var native) && !native.Released)
                {
                    if (writer == "set_forceRenderingOff")
                    {
                        if (ReferenceEquals(r, native.R)) native.BackForceR = r.forceRenderingOff; else native.BackForceM = r.forceRenderingOff;
                        r.forceRenderingOff = true;
                    }
                    QueueStruct(native); return;
                }
                if (r is SpriteMask mask) { ReserveChangedMask(mask); return; }
                RendererChanged(r);
                if ((ProxyMode || kind == DirectSequenceChanges.Hierarchy) && !rendererIndex.ContainsKey(r)) createdObjects.Add(r.gameObject);
                return;
            }
            if (obj is Material mat)
            {
                if (materialIndex.TryGetValue(mat, out var ls)) foreach (var lf in ls) MarkChanged(lf);
                if (nativeMaterialIndex.TryGetValue(mat, out var ns)) foreach (var it in ns) if (!it.Released) QueueStruct(it);
                return;
            }
            if (obj is Transform t)
            {
                TransformChanged(t);
                if (kind == DirectSequenceChanges.Hierarchy) createdObjects.Add(t.gameObject);
                return;
            }
            if (obj is GameObject go)
            {
                TransformChanged(go.transform);
                if (kind == DirectSequenceChanges.Hierarchy || ProxyMode && writer == "set_layer") createdObjects.Add(go);
                MarkCanvases(go); return;
            }
            if (obj is Component c)
            {
                if (ProxyMode && (c is Canvas || c is CanvasGroup || c is CanvasRenderer || c is Graphic || c is SortingGroup)) orderDirty = true;
                if (kind == DirectSequenceChanges.Hierarchy) createdObjects.Add(c.gameObject);
                if (c is Graphic || c is Canvas || c is CanvasGroup || c is CanvasRenderer) MarkCanvases(c.gameObject);
            }
        }
        static void DiscoverCreatedObjects()
        {
            bool hierarchy = createdObjects.Count != 0;
            foreach (var obj in createdObjects)
            {
                var go = obj as GameObject; if (!go || !go.scene.IsValid()) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SpriteMask mask) { ReserveChangedMask(mask); continue; }
                    if (rendererIndex.ContainsKey(r)) continue;
                    if ((main.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    var d = r.GetComponentInParent<scrDecoration>(true);
                    if (d && itemOf.ContainsKey(d)) continue;
                    var lf = MakeLeftover(r); leftovers.Add(lf);
                    if (lf.Seq && WithinLimits(lf.Bg, lf.Key)) SequenceLeftover(lf);
                    else if (!lf.Seq) { AddLimit(lf); boundaryDirty = true; }
                    orderDirty = true;
                }
                foreach (var c in go.GetComponentsInChildren<Canvas>(true))
                {
                    if (c.renderMode != RenderMode.WorldSpace || !c.isRootCanvas) continue;
                    bool known = false; foreach (var w in canvasWatches) if (ReferenceEquals(w.Canvas, c)) { known = true; break; }
                    if (!known) { AuditCanvas(c); var watch = canvasIndex[c]; watch.Reserved = false; QueueCanvas(watch); }
                }
                var parentCanvas = go.GetComponentInParent<Canvas>(true);
                if (parentCanvas && canvasIndex.TryGetValue(parentCanvas.rootCanvas, out var current)) RefreshCanvasGraphics(current);
            }
            createdObjects.Clear();
            if (hierarchy)
            {
                foreach (var ls in transformIndex.Values) ls.Clear();
                foreach (var lf in sequenced) if (lf.R) for (var t = lf.R.transform; t; t = t.parent)
                {
                    if (!transformIndex.TryGetValue(t, out var ls)) transformIndex[t] = ls = new List<Leftover>();
                    ls.Add(lf);
                }
            }
        }
        static void RefreshCanvasGraphics(CanvasWatch w)
        {
            var graphics = w.Canvas.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (Array.IndexOf(w.Graphics, g) >= 0) continue;
                g.RegisterDirtyVerticesCallback(w.Dirty); g.RegisterDirtyMaterialCallback(w.Dirty); g.RegisterDirtyLayoutCallback(w.Dirty);
                if (g is MaskableGraphic mg) mg.onCullStateChanged.AddListener(w.Cull);
                var l = g.gameObject.AddComponent<DirectDecoLifetime>(); l.Changed = _ => QueueCanvas(w); w.Lifetimes.Add(l);
                for (var t = g.transform; t; t = t.parent)
                {
                    if (!canvasGoIndex.TryGetValue(t.gameObject, out var ws)) canvasGoIndex[t.gameObject] = ws = new List<CanvasWatch>();
                    if (!ws.Contains(w)) ws.Add(w);
                }
            }
            w.Graphics = graphics; w.Renderers = w.Canvas.GetComponentsInChildren<CanvasRenderer>(true); QueueCanvas(w);
        }
        static void ReserveChangedMask(SpriteMask mask)
        {
            if (ProxyMode) return; // Engine retains every mask; M3 never reserves whole layer boundaries.
            if (nativeRendererIndex.TryGetValue(mask, out var owner) && !owner.Released) { QueueStruct(owner); return; }
            int backLayer = mask.isCustomRangeActive ? SortingLayer.GetLayerValueFromID(mask.backSortingLayerID) : int.MinValue;
            int backOrder = mask.isCustomRangeActive ? mask.backSortingOrder : int.MinValue;
            int frontLayer = mask.isCustomRangeActive ? SortingLayer.GetLayerValueFromID(mask.frontSortingLayerID) : int.MaxValue;
            int frontOrder = mask.isCustomRangeActive ? mask.frontSortingOrder : int.MaxValue;
            var lo = new SceneRenderOrder.Key(backLayer, backOrder, int.MinValue, float.MaxValue, int.MinValue, int.MinValue);
            var hi = new SceneRenderOrder.Key(frontLayer, frontOrder, int.MaxValue, float.MinValue, int.MaxValue, int.MaxValue);
            bool expanded = false;
            if (backLayer <= bgValue && (!bgLimit || lo.CompareTo(bgMin) < 0)) { bgLimit = true; bgMin = lo; expanded = true; }
            if (frontLayer > bgValue && (!fgLimit || hi.CompareTo(fgMax) > 0)) { fgLimit = true; fgMax = hi; expanded = true; }
            foreach (var it in items) if (!it.Released && NeedsMask(it)) { ReleaseItem(it, "외부 마스크 범위 변경"); expanded = true; }
            if (expanded) boundaryDirty = true;
        }
        internal static void AfterSequenceSpawn(UnityEngine.Object original, UnityEngine.Object result)
        {
            if (!Active || !original || !result) return;
            var source = original is GameObject a ? a.transform : original is Component b ? b.transform : null;
            var target = result is GameObject c ? c.transform : result is Component d ? d.transform : null;
            if (source && target) RestoreCloneLayers(source, target);
        }
        static void RestoreCloneLayers(Transform source, Transform target)
        {
            if (movedLayers.TryGetValue(source.gameObject, out int original) && target.gameObject.layer == HiddenLayer) target.gameObject.layer = original;
            var from = source.GetComponents<Renderer>(); var to = target.GetComponents<Renderer>();
            for (int k = 0; k < Math.Min(from.Length, to.Length); k++) foreach (var it in items)
            {
                if (ReferenceEquals(it.R, from[k]) && !it.Released) to[k].forceRenderingOff = it.BackForceR;
                if (ReferenceEquals(it.M, from[k]) && !it.Released) to[k].forceRenderingOff = it.BackForceM;
            }
            foreach (var l in target.GetComponents<DirectDecoLifetime>()) { l.Changed = null; UnityEngine.Object.Destroy(l); }
            for (int k = 0; k < Math.Min(source.childCount, target.childCount); k++) RestoreCloneLayers(source.GetChild(k), target.GetChild(k));
        }
        static int Submesh(Renderer r, int material)
        {
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else if (r is MeshRenderer) { var filter = r.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
            return mesh ? Math.Min(material, Math.Max(0, mesh.subMeshCount - 1)) : material;
        }
        static void RecordGrab(CommandBuffer cb, Material mat)
        {
            // The installed Blend Modes sprite Grab shader has an unnamed GrabPass.
            // DrawRenderer does not execute a ShaderLab GrabPass: bind its screen input explicitly.
            if (!mat.shader || mat.shader.name != GpuDecoBlend.GrabShader) return;
            var desc = main.targetTexture ? main.targetTexture.descriptor : new RenderTextureDescriptor(main.pixelWidth, main.pixelHeight, main.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 0);
            desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.useMipMap = false; desc.autoGenerateMips = false;
            cb.GetTemporaryRT(grabRT, desc, FilterMode.Bilinear);
            cb.Blit(BuiltinRenderTextureType.CameraTarget, grabRT);
            cb.SetGlobalTexture(grabTexture, grabRT);
            cb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            grabCommands++;
            // Lifetime ends after the consuming draw (see RebuildCommands).
        }
        static string M2State() => " sequence=" + (splitSequence ? "canvas-mask-islands" : "unified") + " canvasRoots=" + canvasWatches.Count + " canvasDirty=" + changedCanvases.Count + " rendererDirty=" + changedRenderers.Count + " drawRenderer=" + drawRendererCommands + " cb_record_us="
            + (recordTicks * (1e6 / System.Diagnostics.Stopwatch.Frequency) / Math.Max(1, cbBuilds)).ToString("F1")
            + " sequenceGrab=" + grabCommands + " mutationMethods=" + mutationMethods + " mutationFailures=" + mutationFailures;
        static string SequenceCheck()
        {
            if (ProxyMode) return ProxySequenceCheck();
            if (!Active) return "seqcheck inactive";
            // Diagnostic only: refresh all cached keys independently of the dirty hooks.
            int stale = 0, missing = 0, ordering = 0, ties = 0, count = 0;
            var reference = new List<SceneRenderOrder.Key>();
            var native = new HashSet<Renderer>();
            var maskImages = new HashSet<Renderer>();
            foreach (var it in items) if (!it.Released)
            {
                if (it.IsMask) { maskImages.Add(it.R); continue; }
                native.Add(it.R);
                var key = SequenceKey(it.R, it.R.sharedMaterial, main);
                if (key.CompareTo(it.Key) != 0) stale++; reference.Add(key);
            }
            int unregistered = 0, island = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || r is SpriteMask || native.Contains(r) || maskImages.Contains(r)) continue;
                int layer = movedLayers.TryGetValue(r.gameObject, out int original) ? original : r.gameObject.layer;
                if ((main.cullingMask & (1 << layer)) == 0) continue;
                var pv = r.GetComponentInParent<scrVisualDecoration>(true);
                if (pv && ReferenceEquals(r, pv.meshRenderer) && (!r.gameObject.activeInHierarchy || !meshOn(pv))) continue;
                var key = SequenceKey(r, r.sharedMaterial, main);
                if (!WithinLimits(key.Layer <= bgValue, key)) { island++; continue; }
                if (!rendererIndex.TryGetValue(r, out var lf) || !lf.Suppressed) unregistered++;
                else if (key.CompareTo(lf.Key) != 0) stale++;
                var materials = r.sharedMaterials;
                for (int k = 0; k < materials.Length; k++) reference.Add(SequenceKey(r, materials[k], main, k));
            }
            reference.Sort();
            SceneRenderOrder.Key previous = default; bool first = true;
            foreach (var e in entries)
            {
                if (e.Kind != 0) continue;
                if (count >= reference.Count || e.Key.CompareTo(reference[count]) != 0) ordering++;
                if (!first && previous.SameMaterialTie(e.Key)) ties++;
                first = false; previous = e.Key; count++;
            }
            missing = Math.Abs(reference.Count - count);
            return "seqcheck entries=" + count + " stale=" + stale + " missing=" + missing + " unregistered=" + unregistered + " unityIsland=" + island + " orderMismatch=" + ordering + " sameMaterialTies=" + ties
                + " pass=" + (stale == 0 && missing == 0 && ordering == 0 && unregistered == 0) + " (SceneRenderOrder model; orthographic distance omits common camera offset; Unity same-material ties require drPix)";
        }

        static void InstallMutationHooks()
        {
            DirectSequenceChanges.Install(hooks);
            mutationMethods = DirectSequenceChanges.Methods;
            mutationFailures = 0;
            foreach (string name in new[] { "Rebuild", "OnCanvasGroupChanged", "OnEnable", "OnDisable" })
            {
                var m = AccessTools.DeclaredMethod(typeof(Graphic), name);
                if (m != null && m.GetMethodBody() != null) hooks.Patch(m, postfix: new HarmonyMethod(typeof(DirectDeco), nameof(CanvasChanged)));
            }
        }
        static void CanvasChanged(Graphic __instance) { if (Active && __instance) MarkCanvases(__instance.gameObject); }
    }
}
