#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using N = StutterFix.DirectFullNative;

namespace StutterFix
{
    // Only notification-dirty canvas plans are scanned. No Resources query, LINQ,
    // GetComponents array or material enumeration is on the stable frame path.
    internal static unsafe partial class DirectFull
    {
        sealed class CanvasUnit
        {
            internal Canvas Canvas;
            internal readonly DirectFullObjectLayers<GameObject> LayerLease = new DirectFullObjectLayers<GameObject>(go => go, go => go.layer, (go, layer) => go.layer = layer);
            internal List<GameObject> Nodes => LayerLease.Objects;
            internal readonly List<CanvasRenderer> Graphics = new List<CanvasRenderer>();
            internal readonly List<Canvas> Cameras = new List<Canvas>();
            internal readonly List<Camera> OriginalCameras = new List<Camera>();
            internal int OriginalMask => LayerLease.Mask;
            internal int Run = -1;
            internal bool Live, Isolated, Queued, Active;
            internal int LiveGraphics, PotentialGraphics;
            internal Action Signal; internal UnityEngine.Events.UnityAction GraphicSignal;
            internal UnityEngine.Events.UnityAction<bool> CullSignal;
            internal bool Moved => LayerLease.Moved;
            internal string Reason;
            internal Entry Entry;
        }
        sealed class CanvasRun
        {
            internal readonly List<CanvasUnit> Units = new List<CanvasUnit>();
            internal readonly List<DirectFullObjectLayers<GameObject>> Layers = new List<DirectFullObjectLayers<GameObject>>();
            internal RenderTexture Black, White;
            internal int Draw;
        }
        struct SuppressedRenderer { internal Renderer Renderer; internal bool Forced; }
        struct SuppressedGraphic { internal CanvasRenderer Renderer; internal bool Culled; }
        static readonly HashSet<Canvas> dormantCanvases = new HashSet<Canvas>();
        static readonly List<CanvasUnit> canvasUnits = new List<CanvasUnit>();
        static readonly List<Source> canvasRendererSources = new List<Source>();
        static readonly Dictionary<Shader, string> canvasBlendReasons = new Dictionary<Shader, string>();
        static readonly List<CanvasRun> canvasRuns = new List<CanvasRun>();
        static readonly HashSet<Transform> canvasOwned = new HashSet<Transform>();
        static readonly HashSet<UnityEngine.Object> canvasIndex = new HashSet<UnityEngine.Object>();
        static readonly List<DirectFullSourceLifetime> canvasLifetimes = new List<DirectFullSourceLifetime>();
        static readonly List<SuppressedRenderer> suppressedRenderers = new List<SuppressedRenderer>();
        static readonly List<SuppressedGraphic> suppressedGraphics = new List<SuppressedGraphic>();
        static Camera auxiliary;
        static GameObject auxiliaryObject;
        static int auxiliaryLayer = -1, parkingLayer = -1, runCount, recordingRun = -1;
        static bool canvasDirty, fullRescan, drawingAuxiliary;
        static long auxiliaryDraws, auxiliaryTicks, auxiliaryMaxTicks, rescans;
        static int canvasTotal, canvasActive, canvasGraphics, canvasLiveGraphics, canvasInactiveGraphics, canvasPotentialGraphics, uncertainCanvases;
        static readonly List<CanvasUnit> potentialCanvases = new List<CanvasUnit>();
        static readonly Plane[] canvasPlanes = new Plane[6];
        static Matrix4x4 canvasView; static bool canvasViewValid;
        static readonly List<CanvasUnit> dirtyCanvases = new List<CanvasUnit>();
        static readonly Dictionary<UnityEngine.Object, List<CanvasUnit>> canvasOwners = new Dictionary<UnityEngine.Object, List<CanvasUnit>>();
        static void QueueCanvas(CanvasUnit u)
        {
            if (drawingAuxiliary || u.Queued) return;
            u.Queued = true; dirtyCanvases.Add(u); canvasDirty = true;
        }
        static void IndexCanvas(UnityEngine.Object obj, CanvasUnit u)
        {
            if (!obj) return; canvasIndex.Add(obj);
            if (!canvasOwners.TryGetValue(obj, out var owners)) canvasOwners.Add(obj, owners = new List<CanvasUnit>());
            if (!owners.Contains(u)) owners.Add(u);
        }

        static bool InCameraScene(Renderer r, int mask) => r && r.gameObject.scene.IsValid()
            && r.gameObject.scene.isLoaded && (mask & 1 << r.gameObject.layer) != 0;
        static bool CanvasOwns(Transform t) => canvasOwned.Contains(t);
        static bool Relevant(Canvas c, Camera main) => c && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
            && (c.renderMode == RenderMode.WorldSpace || c.renderMode == RenderMode.ScreenSpaceCamera && (c.worldCamera == main || c.rootCanvas && c.rootCanvas.worldCamera == main))
            && (c.isRootCanvas || c.overrideSorting);

        static void WatchCanvas(CanvasUnit u, GameObject go)
        {
            IndexCanvas(go, u);
            if (go == u.Canvas.gameObject) { var l = go.AddComponent<DirectFullSourceLifetime>(); l.Changed = u.Signal; canvasLifetimes.Add(l); }
            IndexCanvas(go.transform, u);
        }
        static void GatherCanvas(CanvasUnit u, Transform t, bool root)
        {
            var nested = t.GetComponent<Canvas>();
            if (!root && nested && nested.overrideSorting) return;
            u.LayerLease.Capture(t.gameObject);
            canvasOwned.Add(t); WatchCanvas(u, t.gameObject);
            if (nested) { u.Cameras.Add(nested); u.OriginalCameras.Add(nested.worldCamera); IndexCanvas(nested, u); }
            for (Transform ancestor = t.parent; ancestor; ancestor = ancestor.parent)
            { IndexCanvas(ancestor, u); IndexCanvas(ancestor.gameObject, u); var group = ancestor.GetComponent<CanvasGroup>(); if (group) IndexCanvas(group, u); }
            var cr = t.GetComponent<CanvasRenderer>();
            if (cr) { u.Graphics.Add(cr); IndexCanvas(cr, u); }
            var ownGroup = t.GetComponent<CanvasGroup>(); if (ownGroup) IndexCanvas(ownGroup, u);
            var graphic = t.GetComponent<Graphic>();
            if (graphic)
            {
                graphic.RegisterDirtyMaterialCallback(u.GraphicSignal); graphic.RegisterDirtyVerticesCallback(u.GraphicSignal);
                graphic.RegisterDirtyLayoutCallback(u.GraphicSignal);
                if (graphic is MaskableGraphic mg) mg.onCullStateChanged.AddListener(u.CullSignal);
                IndexCanvas(graphic, u);
            }
            for (int i = 0; i < t.childCount; i++) GatherCanvas(u, t.GetChild(i), false);
        }
        static bool LiveGraphic(CanvasRenderer cr)
        {
            if (!cr || !cr.gameObject.activeInHierarchy || cr.cull || cr.materialCount == 0
                || cr.GetAlpha() * cr.GetInheritedAlpha() <= 0.00001f) return false;
            var graphic = cr.GetComponent<Graphic>();
            if (graphic && (!graphic.isActiveAndEnabled || graphic.color.a <= 0.00001f)) return false;
            var mesh = cr.GetMesh(); return mesh && mesh.vertexCount > 0;
        }
        static bool VisibleCanvasGraphic(CanvasRenderer cr, Plane[] planes)
        {
            var mesh = cr.GetMesh(); if (!mesh) return false;
            var bounds = mesh.bounds; var w = cr.transform.localToWorldMatrix; var e = bounds.extents;
            var extent = new Vector3(Mathf.Abs(w.m00) * e.x + Mathf.Abs(w.m01) * e.y + Mathf.Abs(w.m02) * e.z,
                Mathf.Abs(w.m10) * e.x + Mathf.Abs(w.m11) * e.y + Mathf.Abs(w.m12) * e.z,
                Mathf.Abs(w.m20) * e.x + Mathf.Abs(w.m21) * e.y + Mathf.Abs(w.m22) * e.z);
            return GeometryUtility.TestPlanesAABB(planes, new Bounds(w.MultiplyPoint3x4(bounds.center), extent * 2));
        }
        static void UpdateCanvasView(Camera view)
        {
            var signature = view.projectionMatrix * view.worldToCameraMatrix;
            if (canvasViewValid && canvasView.Equals(signature)) return;
            canvasView = signature; canvasViewValid = true; GeometryUtility.CalculateFrustumPlanes(signature, canvasPlanes);
            foreach (var u in potentialCanvases) QueueCanvas(u);
        }
        static bool CanvasInMask(Canvas c, int original)
        {
            if ((original & 1 << c.gameObject.layer) != 0) return true;
            foreach (var cr in c.GetComponentsInChildren<CanvasRenderer>(true)) if (cr && (original & 1 << cr.gameObject.layer) != 0) return true;
            return false;
        }
        static bool CanvasBlendKnown(string n) => n == "UI/Default" || n == "UI/DefaultETC1" || n == "UI/Default Font"
            || n == "TextMeshPro/Distance Field" || n == "TextMeshPro/Mobile/Distance Field"
            || n == "TextMeshPro/Sprite" || n == "UI/Unlit/Transparent";
        static string CanvasReason(CanvasUnit u)
        {
            // Alpha reconstruction requires destination-independent fragment output
            // and a scalar source-over destination factor, for RGB AND alpha.
            foreach (var cr in u.Graphics)
            {
                if (!LiveGraphic(cr)) continue;
                for (int i = 0; i < cr.materialCount; i++)
                {
                    var m = cr.GetMaterial(i); if (!m || !m.shader) continue;
                    var shader = m.shader;
                    if (!canvasBlendReasons.TryGetValue(shader, out string reason))
                    {
                        string name = shader.name; reason = CanvasBlendKnown(name) ? null : "unverified-canvas-blend:" + name;
                        canvasBlendReasons.Add(shader, reason);
                    }
                    if (reason != null) return reason;
                }
            }
            return null;
        }
        static bool PhysicsDependent(CanvasUnit u)
        {
            foreach (var go in u.Nodes)
                if (go && (go.GetComponent<Collider>() || go.GetComponent<Collider2D>()
                    || go.GetComponentInParent<Rigidbody>() || go.GetComponentInParent<Rigidbody2D>()
                    || go.GetComponent<Joint>() || go.GetComponent<Joint2D>())) return true;
            return false;
        }
        static void FindAuxiliaryLayers()
        {
            auxiliaryLayer = parkingLayer = -1;
            uint used = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t && t.gameObject.scene.IsValid()) used |= 1u << t.gameObject.layer;
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c && c != camera && c != auxiliary && c.isActiveAndEnabled) used |= unchecked((uint)c.cullingMask);
            for (int i = 31; i >= 0; i--)
            {
                if ((used & 1u << i) != 0 || LayerMask.LayerToName(i).Length != 0) continue;
                if (auxiliaryLayer < 0) auxiliaryLayer = i;
                else { parkingLayer = i; break; }
            }
        }
        static System.Collections.Generic.IEnumerable<int> PrepareCanvasesSteps(Camera main, int original)
        {
            canvasTotal = canvasActive = canvasGraphics = canvasLiveGraphics = canvasInactiveGraphics = canvasPotentialGraphics = uncertainCanvases = 0;
            UpdateCanvasView(main);
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                yield return 0;
                if (!Relevant(c, main) || !CanvasInMask(c, original)) continue;
                if (!c.isActiveAndEnabled) { dormantCanvases.Add(c); canvasTotal++; continue; }
                var u = new CanvasUnit { Canvas = c };
                u.Signal = () => QueueCanvas(u); u.GraphicSignal = () => QueueCanvas(u); u.CullSignal = _ => QueueCanvas(u);
                canvasUnits.Add(u); GatherCanvas(u, c.transform, true);
                if ((u.OriginalMask & original) == 0) continue;
                canvasTotal++; u.Active = c.isActiveAndEnabled; if (u.Active) canvasActive++;
                foreach (var cr in u.Graphics)
                {
                    canvasGraphics++;
                    bool potential = c.isActiveAndEnabled && LiveGraphic(cr);
                    if (potential) { u.PotentialGraphics++; canvasPotentialGraphics++; }
                    if (potential && VisibleCanvasGraphic(cr, canvasPlanes)) { canvasLiveGraphics++; u.LiveGraphics++; u.Live = true; }
                    else canvasInactiveGraphics++;
                }
                if (u.PotentialGraphics != 0) potentialCanvases.Add(u);
                IndexCanvasMaterials(u);
                u.Reason = CanvasReason(u);
                if (u.Live && u.Reason != null) uncertainCanvases++;
                u.Isolated = PhysicsDependent(u);
                if (u.Isolated) Log("canvas original-layer isolation (physics): " + c.name);
            }
            bool any = false; foreach (var u in canvasUnits) any |= u.Live;
            if (any)
            {
                FindAuxiliaryLayers();
                foreach (var u in canvasUnits) if (u.Live)
                {
                    if (auxiliaryLayer < 0 || parkingLayer < 0) { u.Isolated = true; Log("canvas original-layer isolation: no two unused unnamed layers"); }

                }
            }
            suppressedRenderers.Capacity = Math.Max(suppressedRenderers.Capacity, sources.Count + 1024);
            suppressedGraphics.Capacity = Math.Max(suppressedGraphics.Capacity, canvasGraphics);
            dirtyCanvases.Capacity = Math.Max(dirtyCanvases.Capacity, canvasUnits.Count);
            foreach (var u in dirtyCanvases) u.Queued = false; dirtyCanvases.Clear();
            canvasDirty = false;
        }
        static void ParkLiveCanvases()
        {
            suppressedRenderers.Capacity = Math.Max(suppressedRenderers.Capacity, sources.Count);
            foreach (var s in sources) if (CanvasOwns(s.R.transform)) canvasRendererSources.Add(s);
            foreach (var u in canvasUnits) if (u.Live && !u.Isolated) MoveCanvas(u, parkingLayer);
        }
        static void MoveCanvas(CanvasUnit u, int layer)
        {
            u.LayerLease.Move(layer);
        }
        static void RestoreCanvasLayers(CanvasUnit u) => u.LayerLease.Restore();
        static void IndexCanvasMaterials(CanvasUnit u)
        {
            foreach (var cr in u.Graphics) if (cr)
                for (int i = 0; i < cr.materialCount; i++) IndexCanvas(cr.GetMaterial(i), u);
        }
        static void NotifyCanvasWrite(UnityEngine.Object obj, int kind, string writer)
        {
            if (drawingAuxiliary) return;
            if (obj is Canvas c && dormantCanvases.Contains(c) && c.isActiveAndEnabled) { fullRescan = true; return; }
            if (canvasOwners.TryGetValue(obj, out var owners))
            {
                if (obj is Transform && kind == DirectSequenceChanges.Pose) canvasPoseSignals++;
                foreach (var u in owners) QueueCanvas(u);
                if (kind == DirectSequenceChanges.Hierarchy) fullRescan = true;
            }
        }
        static void ReplanCanvases()
        {
            long start = Stopwatch.GetTimestamp(); canvasDirty = false;
            foreach (var u in dirtyCanvases)
            {
                u.Queued = false;
                if (!u.Canvas) { fullRescan = true; continue; }
                bool active = u.Canvas.isActiveAndEnabled; int graphics = 0, potential = 0;
                if (active) foreach (var cr in u.Graphics) if (LiveGraphic(cr)) { potential++; if (VisibleCanvasGraphic(cr, canvasPlanes)) graphics++; }
                if (u.PotentialGraphics == 0 && potential != 0) potentialCanvases.Add(u);
                else if (u.PotentialGraphics != 0 && potential == 0) potentialCanvases.Remove(u);
                canvasPotentialGraphics += potential - u.PotentialGraphics; u.PotentialGraphics = potential;
                canvasActive += (active ? 1 : 0) - (u.Active ? 1 : 0); u.Active = active;
                canvasLiveGraphics += graphics - u.LiveGraphics; canvasInactiveGraphics += u.LiveGraphics - graphics; u.LiveGraphics = graphics;
                bool live = graphics != 0;
                if (u.Live && u.Reason != null) uncertainCanvases--;
                if (u.Live != live) { u.Live = live; orderDirty = true; }
                if (!live) RestoreCanvasLayers(u);
                else if (!u.Moved && !u.Isolated)
                {
                    if (auxiliaryLayer < 0 || parkingLayer < 0) FindAuxiliaryLayers();
                    u.Isolated = auxiliaryLayer < 0 || parkingLayer < 0 || PhysicsDependent(u);
                    if (!u.Isolated) MoveCanvas(u, parkingLayer);
                }
                IndexCanvasMaterials(u); u.Reason = CanvasReason(u);
                if (live && u.Reason != null) uncertainCanvases++;
                if (live && u.Entry != null && u.Entry.Key.CompareTo(CanvasKey(u)) != 0) orderDirty = true;
            }
            dirtyCanvases.Clear();
            // Material/vertex-only changes stay on Unity's auxiliary render path.
            replanMaxTicks = Math.Max(replanMaxTicks, Stopwatch.GetTimestamp() - start);
        }
        static void RecreateFull()
        {
            long start = Stopwatch.GetTimestamp(); long count = rescans + 1;
            long priorMax = replanMaxTicks, oldAuxDraws = auxiliaryDraws, oldAuxTicks = auxiliaryTicks, oldAuxMax = auxiliaryMaxTicks;
            Stop("hierarchy rescan; Unity visible during preparation"); StartFull(); rescans = count;
            auxiliaryDraws += oldAuxDraws; auxiliaryTicks += oldAuxTicks; auxiliaryMaxTicks = Math.Max(auxiliaryMaxTicks, oldAuxMax);
            replanMaxTicks = Math.Max(priorMax, Stopwatch.GetTimestamp() - start);
        }
        static SceneRenderOrder.Key CanvasKey(CanvasUnit u)
        {
            var c = u.Canvas; float d = Vector3.Dot(c.transform.position, new Vector3(sortSignature.x, sortSignature.y, sortSignature.z));
            return new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(c.sortingLayerID), c.sortingOrder, 3000, d, 0, c.GetInstanceID());
        }
        static void AddCanvasEntries()
        {
            foreach (var u in canvasUnits)
            {
                if (!u.Live || !u.Canvas) continue;
                var key = CanvasKey(u);
                if (u.Entry == null) u.Entry = new Entry { Canvas = u };
                u.Entry.Key = key; u.Entry.Layer = key.Layer; u.Entry.Order = key.Order; ordered.Add(u.Entry);
            }
        }
        static void BeginCanvasRuns() { foreach (var u in canvasUnits) u.Run = -1; runCount = 0; recordingRun = -1; foreach (var r in canvasRuns) { r.Units.Clear(); r.Layers.Clear(); } }
        static RenderTexture CanvasTarget(string name)
        {
            var t = new RenderTexture(fullWidth, fullHeight, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                { name = name, antiAliasing = 1, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!t.Create()) { UnityEngine.Object.Destroy(t); throw new InvalidOperationException("M4d canvas RT creation failed"); } return t;
        }
        static void RecordCanvasEntry(CommandBuffer cb, Entry e, ref int start)
        {
            if (recordingRun < 0)
            {
                RecordSegment(cb, ref start); recordingRun = runCount++;
                if (recordingRun == canvasRuns.Count) canvasRuns.Add(new CanvasRun());
                var r = canvasRuns[recordingRun];
                if (!r.Black)
                {
                    r.Black = CanvasTarget("drfull canvas black"); r.White = CanvasTarget("drfull canvas white");
                    var input = new N.Composite { Black = r.Black.GetNativeTexturePtr(), White = r.White.GetNativeTexturePtr() };
                    r.Draw = N.Require(N.Op.Composite, fullToken, &input);
                }
                if (listCount == drawList.Length) throw new InvalidOperationException("M4d canvas list capacity");
                drawList[listCount++] = (uint)r.Draw;
                RecordSegment(cb, ref start);
            }
            e.Canvas.Run = recordingRun; canvasRuns[recordingRun].Units.Add(e.Canvas);
            if (!e.Canvas.Isolated) canvasRuns[recordingRun].Layers.Add(e.Canvas.LayerLease);
        }
        static void EndCanvasRun() { recordingRun = -1; }
        static void RenderCanvases()
        {
            if (runCount == 0) return;
            long start = Stopwatch.GetTimestamp();
            if (!auxiliary)
            {
                auxiliaryObject = new GameObject("StutterFix.DirectFull.Auxiliary");
                auxiliary = auxiliaryObject.AddComponent<Camera>(); auxiliary.enabled = false;
            }
            drawingAuxiliary = true;
            try
            {
                auxiliary.CopyFrom(camera); auxiliary.enabled = false; auxiliary.depth = camera.depth - 1;
                auxiliary.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                auxiliary.worldToCameraMatrix = camera.worldToCameraMatrix; auxiliary.projectionMatrix = camera.projectionMatrix;
                auxiliary.clearFlags = CameraClearFlags.SolidColor; auxiliary.RemoveAllCommandBuffers();
                auxiliary.useOcclusionCulling = false; auxiliary.allowMSAA = false;
                for (int i = 0; i < runCount; i++)
                {
                    var run = canvasRuns[i]; int mask = 0; bool isolate = false;
                    foreach (var u in run.Units)
                    {
                        if (!u.Canvas) continue;
                        if (u.Isolated) { mask |= u.OriginalMask; isolate = true; }
                        else mask |= 1 << auxiliaryLayer;
                        for (int j = 0; j < u.Cameras.Count; j++) if (u.Cameras[j] && u.Cameras[j].renderMode == RenderMode.ScreenSpaceCamera) u.Cameras[j].worldCamera = auxiliary;
                    }
                    try
                    {
                        DirectFullObjectLayers<GameObject>.MoveAll(run.Layers, auxiliaryLayer);
                        SuppressOtherObjects(i, mask, isolate);
                        auxiliary.cullingMask = mask;
                        auxiliary.targetTexture = run.Black; auxiliary.backgroundColor = Color.clear; auxiliary.Render(); auxiliaryDraws++;
                        auxiliary.targetTexture = run.White; auxiliary.backgroundColor = Color.white; auxiliary.Render(); auxiliaryDraws++;
                    }
                    finally
                    {
                        RestoreSuppressed();
                        foreach (var u in run.Units)
                        {
                            for (int j = 0; j < u.Cameras.Count; j++) if (u.Cameras[j]) u.Cameras[j].worldCamera = u.OriginalCameras[j];
                        }
                        DirectFullObjectLayers<GameObject>.MoveAll(run.Layers, parkingLayer);
                    }
                }
            }
            finally { drawingAuxiliary = false; long ticks = Stopwatch.GetTimestamp() - start; auxiliaryTicks += ticks; auxiliaryMaxTicks = Math.Max(auxiliaryMaxTicks, ticks); }
        }
        static void SuppressOtherObjects(int run, int mask, bool isolate)
        {
            foreach (var s in isolate ? sources : canvasRendererSources) if (s.R && (mask & 1 << s.R.gameObject.layer) != 0)
            { suppressedRenderers.Add(new SuppressedRenderer { Renderer = s.R, Forced = s.R.forceRenderingOff }); s.R.forceRenderingOff = true; }
            if (!isolate) return;
            foreach (var u in canvasUnits) if (u.Run != run)
            {
                foreach (var cr in u.Graphics) if (cr && (mask & 1 << cr.gameObject.layer) != 0)
                { suppressedGraphics.Add(new SuppressedGraphic { Renderer = cr, Culled = cr.cull }); cr.cull = true; }
            }
        }
        static void RestoreSuppressed()
        {
            foreach (var s in suppressedRenderers) if (s.Renderer) s.Renderer.forceRenderingOff = s.Forced;
            foreach (var s in suppressedGraphics) if (s.Renderer) s.Renderer.cull = s.Culled;
            suppressedRenderers.Clear(); suppressedGraphics.Clear();
        }
        static string CanvasState() => FormattableString.Invariant($"canvasTotal={canvasTotal} canvasDormant={dormantCanvases.Count} canvasActive={canvasActive} canvasGraphics={canvasGraphics} canvasPotentialGraphics={canvasPotentialGraphics} canvasLiveGraphics={canvasLiveGraphics} canvasInactiveGraphics={canvasInactiveGraphics} auxRT={canvasRuns.Count * 2} auxActiveRT={runCount * 2} auxRuns={runCount} auxDraws={auxiliaryDraws} aux_us={(fullFrames > 0 ? auxiliaryTicks * 1e6 / Stopwatch.Frequency / fullFrames : 0):F3} aux_max_us={auxiliaryMaxTicks * 1e6 / Stopwatch.Frequency:F3} rescans={rescans} uncertainCanvas={uncertainCanvases}");
        static void LogCanvasSummary(string where) { Log(where + " " + CanvasState()); foreach (var u in canvasUnits) if (u.Live && u.Reason != null) Log("canvas reason=" + u.Reason + " name=" + u.Canvas.name); }
        static void ReleaseCanvases()
        {
            drawingAuxiliary = true;
            try
            {
                RestoreSuppressed();
                foreach (var u in canvasUnits)
                {
                    RestoreCanvasLayers(u);
                    for (int i = 0; i < u.Cameras.Count; i++) if (u.Cameras[i]) u.Cameras[i].worldCamera = u.OriginalCameras[i];
                    foreach (var cr in u.Graphics) if (cr)
                    {
                        var g = cr.GetComponent<Graphic>(); if (g) { g.UnregisterDirtyMaterialCallback(u.GraphicSignal); g.UnregisterDirtyVerticesCallback(u.GraphicSignal);
                            g.UnregisterDirtyLayoutCallback(u.GraphicSignal); if (g is MaskableGraphic mg) mg.onCullStateChanged.RemoveListener(u.CullSignal); }
                    }
                }
                foreach (var l in canvasLifetimes) if (l) { l.Changed = null; UnityEngine.Object.Destroy(l); }
                foreach (var r in canvasRuns) { if (r.Black) { r.Black.Release(); UnityEngine.Object.Destroy(r.Black); } if (r.White) { r.White.Release(); UnityEngine.Object.Destroy(r.White); } }
                if (auxiliaryObject) UnityEngine.Object.Destroy(auxiliaryObject);
                auxiliary = null; auxiliaryObject = null; dormantCanvases.Clear(); canvasUnits.Clear(); canvasRuns.Clear(); canvasRendererSources.Clear(); canvasBlendReasons.Clear(); canvasOwned.Clear(); canvasIndex.Clear(); canvasOwners.Clear(); dirtyCanvases.Clear(); potentialCanvases.Clear(); canvasViewValid = false; canvasLifetimes.Clear();
                auxiliaryLayer = parkingLayer = recordingRun = -1; runCount = 0; auxiliaryDraws = auxiliaryTicks = auxiliaryMaxTicks = 0;
            }
            finally { drawingAuxiliary = false; }
        }

        static readonly Func<SortingGroup, bool> groupAtRoot = typeof(SortingGroup).GetProperty("sortAtRoot")?.GetGetMethod()?.CreateDelegate(typeof(Func<SortingGroup, bool>)) as Func<SortingGroup, bool>;
        static void RefreshGroups(Source s)
        {
            var groups = s.R.GetComponentsInParent<SortingGroup>(true); int count = 0;
            foreach (var g in groups) if (g.isActiveAndEnabled) { count++; if (groupAtRoot != null && groupAtRoot(g)) break; }
            bool same = count == s.Groups.Length; int at = count - 1;
            if (same) foreach (var g in groups) if (g.isActiveAndEnabled) { if (at < 0) break; if (s.Groups[at--] != g) { same = false; break; } }
            if (same) return;
            s.Groups = new SortingGroup[count]; s.GroupIds = new int[count]; s.GroupKeys = new SceneRenderOrder.Key[count]; at = count - 1;
            foreach (var g in groups) if (g.isActiveAndEnabled) { if (at < 0) break; s.Groups[at] = g; s.GroupIds[at--] = g.GetInstanceID(); }
            orderDirty = true;
        }
        static SceneRenderOrder.Key GroupKey(Source s, int index)
        {
            var g = s.Groups[index]; float d = index == 0 ? Vector3.Dot(g.transform.position, new Vector3(sortSignature.x, sortSignature.y, sortSignature.z)) : 0;
            return new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(g.sortingLayerID), g.sortingOrder, 3000, d, 0, g.GetInstanceID());
        }
        static int CompareGroups(Entry a, Entry b)
        {
            return DirectFullOrderPath.Compare(a.Source?.GroupIds ?? Array.Empty<int>(), a.Source?.GroupKeys ?? Array.Empty<SceneRenderOrder.Key>(), a.Key,
                b.Source?.GroupIds ?? Array.Empty<int>(), b.Source?.GroupKeys ?? Array.Empty<SceneRenderOrder.Key>(), b.Key);
        }

        static string PlanSlot(Renderer r, Material m)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) return "inactive:watch";
            if (r is SpriteMask mask) return mask.sprite ? "native:SpriteMask" : "inactive:empty-mask";
            if (!m || !m.shader) return "inactive:no-material";
            if (m.renderQueue <= 2500) return "DrawRenderer:opaque";
            if (!(r is SpriteRenderer || r is MeshRenderer)) return "DrawRenderer:" + r.GetType().Name;
            if (r.isPartOfStaticBatch) return "DrawRenderer:static-batch";
            if (r is SpriteRenderer sprite)
            {
                if (!sprite.sprite) return "inactive:empty-sprite";
                if (sprite.drawMode != SpriteDrawMode.Simple) return "DrawRenderer:non-simple-sprite";
            }
            if (r is MeshRenderer meshRenderer && meshRenderer.additionalVertexStreams) return "DrawRenderer:additional-vertex-streams";
            if (!DirectFullShaders.Load().TryGetValue(m.shader.name, out var shader)) return "DrawRenderer:shader:" + m.shader.name;
            var mesh = r is MeshRenderer ? RendererMesh(r) : null;
            if (r is MeshRenderer && !mesh) return "DrawRenderer:empty-mesh";
            foreach (var pass in shader.Passes)
            {
                var variant = DirectFullShaders.Select(shader, pass, m);
                if (!mesh) continue;
                if (mesh.vertexBufferCount > 8) return "DrawRenderer:mesh-stream-limit";
                for (int i = 0; i < mesh.subMeshCount; i++) if (mesh.GetTopology(i) != MeshTopology.Triangles) return "DrawRenderer:mesh-topology";
                var attributes = mesh.GetVertexAttributes();
                foreach (var input in variant.Vs.Inputs) if (input.System == 0)
                {
                    bool found = false;
                    foreach (var attr in attributes)
                    {
                        uint sem = (uint)attr.attribute, index = 0; if (sem >= 4) { index = sem - 4; sem = 4; }
                        if (SemanticName(sem) == input.Name && index == input.Index) { Dxgi(attr.format, attr.dimension); found = true; }
                    }
                    if (!found) return "DrawRenderer:missing-semantic:" + input.Name + input.Index;
                }
            }
            return "native:candidate-bind-at-on";
        }
        static string CanvasCensus(Camera main, int original, StringBuilder details = null, Action<string> add = null)
        {
            int total = 0, active = 0, graphics = 0, graphicActive = 0, liveGraphics = 0, liveCanvas = 0, zeroAlpha = 0, culled = 0, emptyMesh = 0, offscreen = 0;
            var planes = GeometryUtility.CalculateFrustumPlanes(main);
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!Relevant(c, main)) continue;
                var all = c.GetComponentsInChildren<CanvasRenderer>(true); bool admitted = (original & 1 << c.gameObject.layer) != 0;
                foreach (var cr in all) if (cr && (original & 1 << cr.gameObject.layer) != 0) admitted = true;
                if (!admitted) continue;
                total++; if (c.isActiveAndEnabled) active++;
                bool live = false; string reason = null; int localGraphics = 0;
                foreach (var cr in all)
                {
                    if (!cr) continue;
                    var owner = cr.GetComponentInParent<Canvas>(true);
                    while (owner && !owner.isRootCanvas && !owner.overrideSorting)
                        owner = owner.transform.parent ? owner.transform.parent.GetComponentInParent<Canvas>(true) : null;
                    if (owner != c) continue;
                    graphics++; localGraphics++;
                    var g = cr.GetComponent<Graphic>();
                    if (cr.gameObject.activeInHierarchy && (!g || g.isActiveAndEnabled)) graphicActive++;
                    if (cr.cull) culled++;
                    if (cr.GetAlpha() * cr.GetInheritedAlpha() <= 0.00001f || g && g.color.a <= 0.00001f) zeroAlpha++;
                    var mesh = cr.GetMesh(); if (!mesh || mesh.vertexCount == 0) emptyMesh++;
                    bool potential = c.isActiveAndEnabled && LiveGraphic(cr);
                    bool draw = potential && VisibleCanvasGraphic(cr, planes);
                    if (potential && !draw) offscreen++;
                    if (draw) { liveGraphics++; live = true; }
                    if (draw) for (int i = 0; i < cr.materialCount; i++)
                    {
                        var m = cr.GetMaterial(i); if (!m || !m.shader) continue;
                        string name = m.shader.name;
                        if (!CanvasBlendKnown(name)) reason = "unverified-canvas-blend:" + name;
                    }
                    if (details != null) details.Append(cr.GetInstanceID()).Append('\t').Append(Clean(cr.name)).Append('\t').Append(cr.gameObject.layer).Append('\t').Append(draw).Append("\tCanvasRenderer\t").Append(mesh ? mesh.vertexCount : 0).Append('\n');
                }
                if (live) liveCanvas++;
                string route = !live ? "canvas:inactive-watch" : reason ?? "canvas:auxiliary";
                add?.Invoke(route);
                if (details != null) details.Append(c.GetInstanceID()).Append('\t').Append(Clean(c.name)).Append('\t').Append(c.gameObject.layer).Append('\t').Append(live).Append('\t').Append(route).Append("\tgraphics=").Append(localGraphics).Append('\n');
            }
            return "canvasBounds=" + total + " canvasActive=" + active + " canvasInactive=" + (total - active)
                + " canvasGraphics=" + graphics + " graphicHierarchyActive=" + graphicActive + " graphicHierarchyInactive=" + (graphics - graphicActive)
                + " liveCanvas=" + liveCanvas + " liveGraphics=" + liveGraphics + " nonDrawingGraphics=" + (graphics - liveGraphics)
                + " culled=" + culled + " zeroAlpha=" + zeroAlpha + " emptyMesh=" + emptyMesh + " offscreen=" + offscreen + " visibility=potential-not-GPU-draw-count";
        }
        static string Plan()
        {
            var main = GpuDecoScene.FindMain(); int original = Active ? lease.Original : main.cullingMask;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var text = new StringBuilder("id\tname\tlayer\tactive\troute\tdetail\n");
            int renderers = 0, active = 0;
            Action<string> add = reason => { counts.TryGetValue(reason, out int n); counts[reason] = n + 1; };
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!InCameraScene(r, original)) continue; renderers++;
                bool live = r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff; if (live) active++;
                var materials = r.sharedMaterials; int slots = r is SpriteMask ? 1 : Math.Max(1, materials.Length);
                for (int i = 0; i < slots; i++)
                {
                    string reason;
                    try { reason = PlanSlot(r, i < materials.Length ? materials[i] : null); }
                    catch (Exception ex) { reason = "DrawRenderer:bind:" + ex.Message; }
                    add(reason);
                    text.Append(r.GetInstanceID()).Append('\t').Append(Clean(r.name)).Append('\t').Append(r.gameObject.layer).Append('\t').Append(live).Append('\t').Append(Clean(reason)).Append("\tslot=").Append(i).Append(";groups=").Append(r.GetComponentsInParent<SortingGroup>(true).Length).Append('\n');
                }
            }
            string census = CanvasCensus(main, original, text, add);
            string path = Path.Combine(Main.Entry.Path, "drfull-plan-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".tsv");
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            foreach (var p in counts) Log("plan reason=" + p.Key + " count=" + p.Value);
            string result = "plan renderers=" + renderers + " active=" + active + " inactive=" + (renderers - active)
                + " " + census + " output=" + path + " dryRun=True nativeBind=at-on";
            Log(result); return result;
        }
    }
}
#endif
