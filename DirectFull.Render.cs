#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HarmonyLib;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using N = StutterFix.DirectFullNative;
using S = StutterFix.DirectFullShaders;

namespace StutterFix
{
    internal sealed class DirectFullSourceLifetime : MonoBehaviour
    {
        internal Action Changed;
        void OnEnable() { Changed?.Invoke(); }
        void OnDisable() { Changed?.Invoke(); }
        void OnDestroy() { Changed?.Invoke(); Changed = null; }
    }

    internal static unsafe partial class DirectFull
    {
        sealed class Source
        {
            internal Renderer R; internal SpriteRenderer Sprite; internal SpriteMask Mask;
            internal MeshFilter Filter; internal int Index, PoseIndex = -1; internal bool Queued, Live, GroupsDirty, TintOnly;
            internal SceneRenderOrder.Key Key; internal Bound[] Passes;
            internal readonly HashSet<UnityEngine.Object> Indexed = new HashSet<UnityEngine.Object>();
            internal readonly List<Material> Materials = new List<Material>();
            internal readonly MaterialPropertyBlock RendererBlock = new MaterialPropertyBlock(), SlotBlock = new MaterialPropertyBlock();
            internal DirectFullSourceLifetime Lifetime;
            internal int MaterialCount; internal Sprite MaskSprite; internal (bool,int,int,int,int) MaskOrder;
            internal SortingGroup[] Groups = Array.Empty<SortingGroup>();
            internal int[] GroupIds = Array.Empty<int>();
            internal SceneRenderOrder.Key[] GroupKeys = Array.Empty<SceneRenderOrder.Key>();
        }
        sealed class Bound
        {
            internal Source Source; internal int Slot, MaskKind, Geometry, Program, NativeId;
            internal Material Material; internal Shader UnityShader; internal S.ShaderData Shader; internal S.Pass Pass; internal S.Variant Variant;
            internal N.Buffer[] Buffers; internal N.Patch[] Patches; internal N.Texture[] Textures;
            internal UnityEngine.Texture[] TextureObjects; internal byte[] Bytes, Previous;
            internal int[] States = new int[29]; internal long ConfigHash; internal bool HasRendererColor, HasFlip;
            internal S.Stage[] Stages; internal bool ConstantsQueued;
            internal Entry Entry;
        }
        sealed class Entry
        {
            internal Source Source; internal Bound Draw; internal Material Fallback; internal int Slot, Layer, Order, Sub;
            internal SceneRenderOrder.Key Key; internal CanvasUnit Canvas;
        }
        struct Capture : IJobParallelForTransform
        {
            internal NativeArray<N.Pose> Poses;
            [ReadOnly] internal NativeArray<Vector3> Centers;
            internal NativeArray<float> Distances;
            internal NativeQueue<int>.ParallelWriter Changed;
            internal NativeQueue<int>.ParallelWriter Reorder;
            internal Vector3 Axis;
            public void Execute(int index, TransformAccess access)
            {
                if (!access.isValid) { Changed.Enqueue(index); return; }
                Matrix4x4 w = access.localToWorldMatrix;
                N.Pose old = Poses[index]; bool changed = false;
                for (int k = 0; k < 16; k++) if (old.World[k] != w[k]) { changed = true; break; }
                if (changed) { Poses[index] = new N.Pose { World = w, Inverse = access.worldToLocalMatrix }; Changed.Enqueue(index); }
                Vector3 center = w.MultiplyPoint3x4(Centers[index]); float d = Vector3.Dot(center, Axis);
                if (Distances[index] != d) { Distances[index] = d; Reorder.Enqueue(index); }
            }
        }
        static IEnumerator preparation;
        static bool preparing, unityGrab;
        static long prepareSlices, prepareMaxTicks, prepareWorkTicks;
        static string prepareStage;
        static int prepareOriginal;
        static readonly HashSet<Renderer> dormant = new HashSet<Renderer>();
        static readonly HashSet<Renderer> pendingPromotions = new HashSet<Renderer>();
        static readonly List<Source> poseSources = new List<Source>();
        static NativeArray<N.Pose> uploadPoses;
        static bool transformsDirty;
        static bool rendering, orderDirty, sendList, globalsDirty, clearStencil;
        static int fullToken, nativeCount, fallbackCount, removedMask, retainedMask, segmentCount, listCount;
        static readonly List<Source> sources = new List<Source>(), changed = new List<Source>();
        static readonly List<Entry> ordered = new List<Entry>();
        static readonly List<Bound> changedConstants = new List<Bound>();
        static readonly Dictionary<UnityEngine.Object, List<Source>> sourceIndex = new Dictionary<UnityEngine.Object, List<Source>>();
        static readonly Dictionary<(int,int,int), int> geometryCache = new Dictionary<(int,int,int), int>();
        static readonly Dictionary<Mesh, int> meshVersions = new Dictionary<Mesh, int>();
        static readonly Dictionary<(S.Variant,long), int> programs = new Dictionary<(S.Variant,long), int>();
        static readonly Dictionary<UnityEngine.Texture, IntPtr> texturePointers = new Dictionary<UnityEngine.Texture, IntPtr>();
        static readonly List<DirectFullSourceLifetime> sourceLifetimes = new List<DirectFullSourceLifetime>();
        static readonly Dictionary<Renderer, Source> rendererSources = new Dictionary<Renderer, Source>();
        static CommandBuffer before, opaque;
        static TransformAccessArray transforms;
        static NativeArray<N.Pose> poses;
        static NativeArray<Vector3> centers;
        static NativeArray<float> distances;
        static NativeQueue<int> moved, reordered;
        static bool haveNativeFrame;
        static long commitMaxTicks, replanMaxTicks;
        static long collectTicks, poseTicks, sortTicks, packetTicks;
        static long avoidedMaskReplans, avoidedEmptyReplans, canvasPoseSignals, orderKeyChanges, drawRebinds;
        static readonly Dictionary<(Type,string),long> signalWriters = new Dictionary<(Type,string),long>();
        static RenderTexture fullTarget;
        static int fullWidth, fullHeight;
        static uint[] poseIds, dirtyIds, constantOffsets, drawList, segments;
        static byte[] constantBytes;
        static int dirtyCount, constantCount, poseCount;
        static Vector4 sortSignature;
        static long fullFrames, fullTicks, fullMaxTicks, orderBuilds, fullStartTicks;
        static readonly Dictionary<string, int> fallbackReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        internal static bool Rendering => rendering;
        internal static bool Preparing => preparing;
        internal static bool WaitReady()
        {
            if (preparing) return false;
            if (!rendering) throw new InvalidOperationException("drfull preparation did not publish a renderer");
            N.Stats stats = default; N.Require(N.Op.Stats, fullToken, &stats);
            if (stats.Errors != 0 || stats.Dropped != 0) AssertFull();
            return stats.Frames != 0;
        }

        static void StartFull()
        {
            if (rendering || preparing) { CheckLease(); return; }
            if (Active) Stop("switch ceiling to full");
            fullStartTicks = Stopwatch.GetTimestamp(); prepareSlices = prepareMaxTicks = prepareWorkTicks = 0;
            camera = GpuDecoScene.FindMain(); prepareOriginal = camera.cullingMask;
            preparationCamera = camera;
            hooks = new Harmony(Owner);
            preparing = true; prepareStage = "hooks";
            preparation = PrepareFull();
            preparationObject = new GameObject("StutterFix.DirectFull.Prepare");
            preparationDriver = preparationObject.AddComponent<DirectFullPreparation>();
        }
        static IEnumerator PrepareFull()
        {
            foreach (var step in DirectSequenceChanges.InstallSteps(hooks)) yield return null;


            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) throw new InvalidOperationException("drfull requires D3D11");
            if (DrProbe.Active || DirectDeco.Enabled || DirectDeco.Active || Main.Config.GpuDeco != 0 || Main.Config.RunLod)
                throw new InvalidOperationException("drfull requires drx off, dr2 off, GpuDeco=0, RunLod=false");
            var main = GpuDecoScene.FindMain();
            if (!main.orthographic || main.stereoEnabled) throw new InvalidOperationException("M4c requires mono orthographic Camera");
            if (QualitySettings.activeColorSpace != ColorSpace.Gamma) throw new InvalidOperationException("M4c linear color transfer needs Unity pixel validation");
            var rt = main.targetTexture;
            if (!rt || rt.antiAliasing != 1 || !rt.IsCreated()) throw new InvalidOperationException("M4c requires created, non-MSAA camRT");
            foreach (CameraEvent evt in Enum.GetValues(typeof(CameraEvent)))
                foreach (var cb in main.GetCommandBuffers(evt))
                    if (cb.name.StartsWith("DR", StringComparison.Ordinal) || cb.name.StartsWith("StutterFix.Direct", StringComparison.Ordinal))
                        throw new InvalidOperationException("drfull competing command buffer: " + cb.name);
            {
                camera = main; sortSignature = SortSignature(); int original = prepareOriginal;
                fullTarget = rt; fullWidth = rt.width; fullHeight = rt.height;
                // Own every main-camera Renderer, including mask/Flash objects on
                // non-map layers. A whole layer is NEVER retained for one object.
                // All original main-camera layers are removed, including currently
                // empty ones. Later OnEnable cannot create an unowned Unity island.
                removedMask = original;
                var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
                prepareStage = "canvas";
                foreach (var step in PrepareCanvasesSteps(main, original)) yield return null;
                if (removedMask == 0) throw new InvalidOperationException("M4d empty original camera mask");
                retainedMask = 0;
                prepareStage = "shader-resource"; S.Load(); yield return null;
                N.ValidateAbi();
                int capacity = 0;
                foreach (var r in renderers) if (InCameraScene(r, original)) capacity++;
                capacity = Math.Max(1, capacity + canvasUnits.Count + 64); // 캔버스는 run 당 합성 1개(최대 캔버스 수)
                CreateNative(rt, capacity);
                prepareStage = "bind";
                foreach (var r in renderers)
                {
                    yield return null;
                    if (!InCameraScene(r, original)) continue;
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) { dormant.Add(r); continue; }
                    AddSource(r);
                }

                changed.Capacity = Math.Max(changed.Capacity, sources.Count); changedConstants.Capacity = Math.Max(changedConstants.Capacity, capacity * 4);
                BuildPoseTracking();
                int packetCapacity = Math.Max(64, sources.Count + canvasUnits.Count);
                dirtyIds = new uint[packetCapacity * 8]; constantOffsets = new uint[packetCapacity * 8 + 1];
                drawList = new uint[capacity * 4]; segments = new uint[131072]; constantBytes = new byte[65536];
                before = new CommandBuffer { name = "StutterFix.DirectFull.BeforeAlpha" };
                opaque = new CommandBuffer { name = "StutterFix.DirectFull.BeforeOpaque" };
                main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, opaque); main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, before);
                // Install managed write-site observers. Native Unity setters themselves
                // are never patched. Existing game transforms/materials keep updating.
                PromoteSources();
                if (fullRescan) throw new InvalidOperationException("hierarchy changed during preparation; retry on stable map");
                prepareStage = "publish";
                BeginLease(main, removedMask, "StutterFix.DirectFull.Render");
                ParkLiveCanvases();
                rendering = true;
                if (globalsDirty) foreach (var source in sources) Queue(source);
                foreach (var source in changed) { source.Queued = false; Refresh(source, false); } changed.Clear();
                if (transformsDirty) BuildPoseTracking();
                sortSignature = SortSignature(); orderDirty = true; globalsDirty = false;
                { collectTicks = poseTicks = sortTicks = packetTicks = 0; fullFrames = fullTicks = fullMaxTicks = orderBuilds = commitMaxTicks = replanMaxTicks = rescans = 0; }
                haveNativeFrame = false;
                // Every pose is initialized in the first immutable frame packet.
                poseCount = 0; // active dense poses are packed on the first PreCull
                RebuildFullOrder();
                preparing = false;
                LogCanvasSummary("on");
                Log("FULL ON native=" + nativeCount + " unityExplicit=" + fallbackCount + " sources=" + sources.Count + " removedMask=" + removedMask + " retainedMask=" + retainedMask +
                    " setup_ms=" + ((Stopwatch.GetTimestamp() - fullStartTicks) * 1000.0 / Stopwatch.Frequency).ToString("F2") + " originalDXBC=True parity=UNMEASURED");
                foreach (var pair in fallbackReasons) Log("fallback " + pair.Key + " count=" + pair.Value);
            }
        }

        static void CreateNative(RenderTexture rt, int capacity)
        {
            var create = new N.Create { Resource = rt.GetNativeTexturePtr(), Capacity = (uint)capacity, Abi = 1 };
            fullToken = N.Require(N.Op.Create, 0, &create);
        }
        static Source AddSource(Renderer r)
        {
            var s = new Source { R = r, Sprite = r as SpriteRenderer, Mask = r as SpriteMask, Filter = r.GetComponent<MeshFilter>(), Index = sources.Count };
            sources.Add(s); rendererSources.Add(r, s); Index(r, s);
            for (Transform t = r.transform; t; t = t.parent) { Index(t, s); Index(t.gameObject, s); var g = t.GetComponent<SortingGroup>(); if (g) Index(g, s); }
            var l = r.gameObject.AddComponent<DirectFullSourceLifetime>(); l.Changed = () => { s.GroupsDirty = true; Queue(s); }; s.Lifetime = l; sourceLifetimes.Add(l);
            Refresh(s, true); transformsDirty = true; return s;
        }
        static void PromoteSources()
        {
            foreach (var r in pendingPromotions)
                if (r && dormant.Contains(r) && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff)
                { dormant.Remove(r); AddSource(r); }
            pendingPromotions.Clear();
        }
        static void BuildPoseTracking()
        {
            if (transforms.isCreated) transforms.Dispose();
            if (poses.IsCreated) poses.Dispose(); if (centers.IsCreated) centers.Dispose(); if (distances.IsCreated) distances.Dispose();
            if (uploadPoses.IsCreated) uploadPoses.Dispose();
            poseSources.Clear();
            foreach (var source in sources) { source.PoseIndex = -1; if (source.Live && source.R) poseSources.Add(source); }
            int count = poseSources.Count;
            transforms = new TransformAccessArray(count);
            poses = new NativeArray<N.Pose>(count, Allocator.Persistent);
            uploadPoses = new NativeArray<N.Pose>(count, Allocator.Persistent);
            centers = new NativeArray<Vector3>(count, Allocator.Persistent);
            distances = new NativeArray<float>(count, Allocator.Persistent);
            if (!moved.IsCreated) moved = new NativeQueue<int>(Allocator.Persistent); else moved.Clear();
            if (!reordered.IsCreated) reordered = new NativeQueue<int>(Allocator.Persistent); else reordered.Clear();
            poseIds = new uint[count]; poseCount = 0;
            for (int i = 0; i < count; i++)
            {
                var source = poseSources[i]; source.PoseIndex = i; transforms.Add(source.R.transform);
                poses[i] = new N.Pose { World = source.R.localToWorldMatrix, Inverse = source.R.worldToLocalMatrix };
                centers[i] = LocalCenter(source); distances[i] = source.Key.Distance;
            }
            transformsDirty = false; haveNativeFrame = false;
        }
        static void Index(UnityEngine.Object obj, Source s)
        {
            if (!s.Indexed.Add(obj)) return;
            if (!sourceIndex.TryGetValue(obj, out var list)) sourceIndex.Add(obj, list = new List<Source>());
            list.Add(s);
        }
        static void Queue(Source s, bool tintOnly = false) { if (!s.Queued) { s.TintOnly = tintOnly; s.Queued = true; changed.Add(s); } else if (!tintOnly) s.TintOnly = false; }
        internal static void NotifyFull(UnityEngine.Object obj, int kind, string writer)
        {
            if (!rendering && !preparing) return;
            if (ReferenceEquals(obj, null)) return;
            if (!obj) { if (sourceIndex.ContainsKey(obj) || canvasIndex.Contains(obj) || obj is Renderer dead && dormant.Contains(dead)) fullRescan = true; return; }
            if (writer != null) { var key = (obj.GetType(), writer); signalWriters.TryGetValue(key, out long n); signalWriters[key] = n + 1; }
            NotifyCanvasWrite(obj, kind, writer);
            if (obj is Renderer wake && dormant.Contains(wake))
            {
                if (wake && wake.enabled && wake.gameObject.activeInHierarchy && !wake.forceRenderingOff) { pendingPromotions.Add(wake); }
                return;
            }
            if (obj is GameObject activeGo && writer == "SetActive")
            {
                foreach (var r in activeGo.GetComponentsInChildren<Renderer>(true))
                    if (dormant.Contains(r) && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff) { pendingPromotions.Add(r); }
                foreach (var c in activeGo.GetComponentsInChildren<Canvas>(true))
                    if (dormantCanvases.Contains(c) && c.isActiveAndEnabled) { fullRescan = true; break; }
            }
            if (!obj) { if (sourceIndex.ContainsKey(obj) || canvasIndex.Contains(obj)) fullRescan = true; return; }
            if (kind == DirectSequenceChanges.Hierarchy)
            {
                if (sourceIndex.ContainsKey(obj) || canvasIndex.Contains(obj)) { fullRescan = true; return; }
                var go = obj as GameObject; if (!go && obj is Component component) go = component.gameObject;
                if (go) foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (((preparing ? prepareOriginal : lease.Original) & 1 << r.gameObject.layer) != 0) { fullRescan = true; return; }
                if (go) foreach (var c in go.GetComponentsInChildren<Canvas>(true)) if (Relevant(c, camera)) { fullRescan = true; break; }
                return;
            }
            if (obj is SortingGroup && sourceIndex.TryGetValue(obj, out var grouped)) { foreach (var s in grouped) { s.GroupsDirty = true; Queue(s); } orderDirty = true; return; }
            if (obj is Transform) return; // TransformAccessArray job observes parents and uninstrumented tweens.
            if (obj is Mesh mesh) { meshVersions.TryGetValue(mesh, out int v); meshVersions[mesh] = v + 1; }
            if (obj is Shader) { globalsDirty = true; return; }
            if (obj is MeshFilter filter) { var renderer = filter.GetComponent<Renderer>(); if (renderer && rendererSources.TryGetValue(renderer, out var owner)) Queue(owner); }
            if (obj is UnityEngine.Texture texture && texturePointers.ContainsKey(texture)) { globalsDirty = true; texturePointers.Remove(texture); }
            if (sourceIndex.TryGetValue(obj, out var list)) foreach (var s in list) Queue(s, obj is SpriteRenderer && writer == "set_color");
        }
        internal static void NotifyFullGlobal() { if (rendering || preparing) globalsDirty = true; }
        internal static void BeforeFullWrite(UnityEngine.Object obj, int kind)
        {
            if (rendering && kind == DirectSequenceChanges.Hierarchy && !ReferenceEquals(obj, null)
                && (sourceIndex.ContainsKey(obj) || canvasIndex.Contains(obj) || obj is UnityEngine.Texture texture && texturePointers.ContainsKey(texture))) fullRescan = true;
        }
        static void Refresh(Source s, bool initial)
        {
            if (!s.R) throw new InvalidOperationException("owned renderer destroyed");
            if (!initial && s.TintOnly && s.Live && s.Sprite && s.Passes != null)
            {
                s.TintOnly = false; Color tint = s.Sprite.color;
                foreach (var b in s.Passes)
                {
                    if (b.NativeId == 0) continue;
                    fixed (byte* data = b.Bytes)
                    {
                        ((Vector4*)data)[0] = b.HasRendererColor ? Color.white : tint;
                        int bi = 0;
                        foreach (var stage in b.Stages) foreach (var cb in stage.Buffers)
                        {
                            int offset = (int)b.Buffers[bi++].Offset;
                            foreach (var v in cb.Variables) if (v.Name == "_RendererColor") *(Vector4*)(data + offset + v.Offset) = tint;
                        }
                    }
                    if (!b.ConstantsQueued) { b.ConstantsQueued = true; changedConstants.Add(b); }
                }
                return;
            }
            s.TintOnly = false;
            if (initial || s.GroupsDirty) { RefreshGroups(s); s.GroupsDirty = false; }
            if (s.Mask)
            {
                var maskOrder = (s.Mask.isCustomRangeActive, s.Mask.frontSortingLayerID, s.Mask.frontSortingOrder, s.Mask.backSortingLayerID, s.Mask.backSortingOrder);
                if (!maskOrder.Equals(s.MaskOrder)) { s.MaskOrder = maskOrder; orderDirty = true; }
            }
            bool live = s.R.enabled && s.R.gameObject.activeInHierarchy && !s.R.forceRenderingOff && (CanvasOwns(s.R.transform) || (removedMask & 1 << s.R.gameObject.layer) != 0);
            if (s.Live != live) { orderDirty = true; transformsDirty = true; } s.Live = live;
            s.R.GetSharedMaterials(s.Materials); s.R.GetPropertyBlock(s.RendererBlock);
            if (!live) { UpdateKey(s); return; }
            if (centers.IsCreated && s.PoseIndex >= 0) centers[s.PoseIndex] = LocalCenter(s);
            foreach (var mat in s.Materials) if (mat) Index(mat, s);
            if (s.Filter && s.Filter.sharedMesh) Index(s.Filter.sharedMesh, s);
            if (s.Sprite && !s.Sprite.sprite)
            {
                if (s.Passes != null) foreach (var old in s.Passes) { if (old.NativeId != 0) nativeCount--; else fallbackCount--; }
                bool hadDraws = s.Passes != null && s.Passes.Length != 0;
                s.Passes = Array.Empty<Bound>(); s.MaterialCount = s.Materials.Count;
                if (hadDraws) orderDirty = true; else avoidedEmptyReplans++;
                UpdateKey(s); return;
            }
            if (s.Passes != null)
            {
                // Rebind only when structure changes. Constant-only writes reuse all
                // arrays, D3D objects, layouts and texture COM references.
                bool structure;
                try { structure = !StructureMatches(s); } catch { structure = true; }
                if (!structure)
                {
                    foreach (var b in s.Passes)
                    {
                        try { if (b.NativeId != 0) Fill(b); } catch { structure = true; break; }
                        if (!s.Mask && b.Entry.Key.CompareTo(FullKey(s.R, b.Material, b.Slot)) != 0) orderDirty = true;
                    }
                    if (!structure) { if (s.Mask) avoidedMaskReplans++; UpdateKey(s); return; }
                }
                if (!initial) { foreach (var old in s.Passes) { if (old.NativeId != 0) nativeCount--; else fallbackCount--; } orderDirty = true; }
            }
            var passes = new List<Bound>();
            if (s.Mask)
            {
                if (!s.Mask.sprite) { s.Passes = Array.Empty<Bound>(); UpdateKey(s); return; }
                var shader = S.Load()["Sprites/Mask"];
                foreach (var pass in shader.Passes) for (int k = 1; k <= 2; k++) passes.Add(Bind(s, null, shader, pass, 0, k));
            }
            else
            {
                for (int slot = 0; slot < s.Materials.Count; slot++)
                {
                    var m = s.Materials[slot]; if (!m || !m.shader) continue;

                    string reason = m.renderQueue < 2501 ? "opaque:explicit" : null;
                    if (unityGrab && m.shader.name.IndexOf("BlendModes/", StringComparison.Ordinal) >= 0) reason = "diagnostic:Unity-blend";
                    if (reason == null && !(s.Sprite || s.R is MeshRenderer && s.Filter && s.Filter.sharedMesh)) reason = "renderer:" + s.R.GetType().Name;
                    else if (s.R.isPartOfStaticBatch) reason = "mesh:Unity-static-batch";
                    else if (s.R is MeshRenderer mr && mr.additionalVertexStreams) reason = "mesh:additional-vertex-streams";
                    else if (s.Sprite && s.Sprite.drawMode != SpriteDrawMode.Simple) reason = "non-simple sprite";
                    else if (!S.Load().TryGetValue(m.shader.name, out var shader)) reason = "shader:" + m.shader.name;
                    if (reason != null)
                    {
                        passes.Add(Fallback(s, m, slot, reason)); continue;
                    }
                    var data = S.Load()[m.shader.name];
                    if (s.Sprite && !s.Sprite.sprite) continue;
                    int first = passes.Count;
                    try { foreach (var pass in data.Passes) passes.Add(Bind(s, m, data, pass, slot, 0)); }
                    catch (Exception ex)
                    {
                        passes.RemoveRange(first, passes.Count - first);
                        passes.Add(Fallback(s, m, slot, "bind:" + ex.Message));
                    }
                }
            }
            s.Passes = passes.ToArray(); s.MaterialCount = s.Materials.Count; s.MaskSprite = s.Mask ? s.Mask.sprite : null;
            foreach (var b in s.Passes) if (b.NativeId != 0) nativeCount++;
            orderDirty = true; UpdateKey(s);
        }
        static Bound Fallback(Source s, Material m, int slot, string reason)
        {
            fallbackReasons.TryGetValue(reason, out int n); fallbackReasons[reason] = n + 1; fallbackCount++;
            var b = new Bound { Source = s, Slot = slot, Material = m };
            b.Entry = new Entry { Source = s, Fallback = m, Slot = slot }; return b;
        }
        static bool StructureMatches(Source s)
        {
            if (s.Mask) return s.MaskSprite == s.Mask.sprite;
            if (s.MaterialCount != s.Materials.Count || s.Sprite && s.Sprite.drawMode != SpriteDrawMode.Simple) return false;
            if (s.Passes.Length == 0) return false;
            foreach (var b in s.Passes)
            {
                if (b.Slot >= s.Materials.Count || !ReferenceEquals(b.Material, s.Materials[b.Slot])) return false;
                if (b.NativeId == 0) continue;
                if (s.R.isPartOfStaticBatch || s.R is MeshRenderer mr && mr.additionalVertexStreams) return false;
                if (b.Material.shader != b.UnityShader || !ReferenceEquals(S.Select(b.Shader, b.Pass, b.Material), b.Variant)) return false;
                if (GeometryFor(s, b.Variant, b.Slot) != b.Geometry) return false;
            }
            return true;
        }
        static Bound Bind(Source s, Material m, S.ShaderData shader, S.Pass pass, int slot, int maskKind)
        {
            var v = s.Mask ? pass.Variants[0] : S.Select(shader, pass, m);
            var b = new Bound { Source = s, Material = m, UnityShader = m ? m.shader : null, Shader = shader, Pass = pass, Slot = slot, MaskKind = maskKind, Variant = v, Stages = new[] { v.Vs, v.Ps } };
            b.Entry = new Entry { Source = s, Draw = b, Slot = slot };
            var buffers = new List<N.Buffer>(); var patches = new List<N.Patch>(); int offset = 16;
            foreach (var stage in b.Stages)
            {
                uint kind = ReferenceEquals(stage, v.Vs) ? 0u : 1u;
                foreach (var cb in stage.Buffers)
                {
                    buffers.Add(new N.Buffer { Stage = kind, Slot = (uint)cb.Slot, Bytes = (uint)cb.Size, Offset = (uint)offset });
                    foreach (var x in cb.Variables)
                    {
                        int dynamic = Dynamic(x.Name); if (dynamic != 0) patches.Add(new N.Patch { Offset = (uint)(offset + x.Offset), Kind = (uint)dynamic });
                        if (x.Name == "_RendererColor") b.HasRendererColor = true;
                        if (x.Name == "_Flip") b.HasFlip = true;
                    }
                    offset += cb.Size;
                }
            }
            b.Buffers = buffers.ToArray(); b.Patches = patches.ToArray(); b.Bytes = new byte[offset]; b.Previous = new byte[offset];
            b.Textures = new N.Texture[v.Vs.Textures.Length + v.Ps.Textures.Length]; b.TextureObjects = new UnityEngine.Texture[b.Textures.Length];
            b.Geometry = GeometryFor(s, v, slot); Fill(b); return b;
        }
        static int Dynamic(string name)
        {
            switch (name)
            {
                case "unity_ObjectToWorld": return 1; case "unity_WorldToObject": return 2; case "unity_MatrixVP": return 3;
                case "unity_MatrixV": return 4; case "glstate_matrix_projection": return 5; case "unity_MatrixMVP": return 6;
                // No current verified program uses MatrixITMV: reject if encountered.
                case "unity_MatrixITMV": throw new InvalidOperationException("unverified MatrixITMV packing");
                case "_WorldSpaceCameraPos": return 8; case "_ScreenParams": return 9; case "_ProjectionParams": return 10;
                case "unity_OrthoParams": return 11; case "_Time": return 12; case "_SinTime": return 13;
                case "_CosTime": return 14; case "unity_DeltaTime": return 15; default: return 0;
            }
        }
        static void ReadBlock(Source s, int slot)
        {
            s.SlotBlock.Clear(); if (slot < s.Materials.Count) s.R.GetPropertyBlock(s.SlotBlock, slot);
        }
        static MaterialPropertyBlock Block(Source s) => !s.SlotBlock.isEmpty ? s.SlotBlock : s.RendererBlock;
        static void Fill(Bound b)
        {
            var s = b.Source; ReadBlock(s, b.Slot); var mpb = Block(s); long hash = 17;
            for (int i = 0; i < 29; i++)
            {
                var state = b.Pass.States[i]; float value = state.Name.Length == 0 ? state.Value : Float(b, state.Id, state.Name, state.Value);
                b.States[i] = i == 11 ? checked((int)(value * 1000000f)) : checked((int)value);
            }
            int interaction = s.Sprite ? (int)s.Sprite.maskInteraction : 0;
            if (b.MaskKind != 0 || interaction != 0)
            {
                b.States[16] = 0;
                for (int k = 17; k < 29; k++) b.States[k] = 0;
                for (int k = 17; k < 29; k += 4) { b.States[k] = b.MaskKind == 1 ? 3 : b.MaskKind == 2 ? 4 : 0; b.States[k + 3] = b.MaskKind != 0 ? 8 : interaction == 1 ? 6 : 3; }
                b.States[14] = 255; b.States[15] = b.MaskKind != 0 ? 255 : 0;
                if (b.MaskKind != 0) { b.States[6] = 0; b.States[8] = 8; b.States[9] = 0; }
            }
            foreach (int x in b.States) hash = unchecked(hash * 31 + x);
            int texIndex = 0;
            foreach (var stage in b.Stages)
            {
                foreach (var t in stage.Textures)
                {
                    UnityEngine.Texture tex = ResolveTexture(b, t.Name, t.Id); bool grab = t.Name == "_GrabTexture";
                    if (t.Name.StartsWith("_BLENDMODES_", StringComparison.Ordinal) && !tex && !grab) throw new InvalidOperationException("unbound blend texture " + t.Name);
                    if (!tex && !grab) tex = DefaultTexture(b, t.Name);
                    if (tex && !(tex is Texture2D || tex is RenderTexture)) throw new InvalidOperationException("non-2D texture " + t.Name);
                    b.TextureObjects[texIndex] = tex;
                    var nt = new N.Texture { Resource = tex ? TexturePointer(tex) : IntPtr.Zero, Stage = ReferenceEquals(stage, b.Variant.Vs) ? 0 : 1,
                        Slot = t.Slot, Sampler = t.Sampler, Filter = tex ? (int)tex.filterMode : 1, WrapU = tex ? (int)tex.wrapModeU : 1, WrapV = tex ? (int)tex.wrapModeV : 1,
                        Aniso = tex ? tex.anisoLevel : 1, Srgb = grab ? -1 : QualitySettings.activeColorSpace == ColorSpace.Linear && tex && tex.isDataSRGB ? 1 : 0 };
                    b.Textures[texIndex++] = nt;
                    hash = unchecked(hash * 31 + nt.Resource.ToInt64()); hash = unchecked(hash * 31 + nt.Filter + nt.WrapU * 7 + nt.WrapV * 31 + nt.Aniso * 113 + nt.Srgb * 991);
                }
            }
            fixed (byte* data = b.Bytes)
            {
                Color c = s.Sprite && !b.HasRendererColor ? s.Sprite.color : Color.white;
                ((Vector4*)data)[0] = c;
                int bi = 0;
                foreach (var stage in b.Stages) foreach (var cb in stage.Buffers)
                {
                    if (mpb.HasConstantBuffer(Shader.PropertyToID(cb.Name))) throw new InvalidOperationException("external MPB constant buffer requires an explicit buffer lease: " + cb.Name);
                    uint offset = b.Buffers[bi++].Offset;
                    foreach (var x in cb.Variables)
                    {
                        if (Dynamic(x.Name) != 0) continue;
                        void* dst = data + offset + x.Offset;
                        if (x.Matrix != 0) { if (x.Rows != 4 || x.Cols != 4) throw new InvalidOperationException("unverified matrix shape"); *(Matrix4x4*)dst = mpb.HasProperty(x.Id) ? mpb.GetMatrix(x.Id) : b.Material && b.Material.HasProperty(x.Id) ? b.Material.GetMatrix(x.Id) : Shader.GetGlobalMatrix(x.Id); continue; }
                        if (x.Type == 1) { if (x.Cols != 1) throw new InvalidOperationException("unverified integer vector"); *(int*)dst = Integer(b, x); continue; }
                        Vector4 value = Value(b, x);
                        for (int k = 0; k < x.Cols; k++) ((float*)dst)[k] = value[k];
                    }
                }
            }
            var localBounds = s.R.localBounds;
            for (int i = 0; i < 3; i++) { hash = unchecked(hash * 31 + BitConverter.SingleToInt32Bits(localBounds.center[i])); hash = unchecked(hash * 31 + BitConverter.SingleToInt32Bits(localBounds.extents[i])); }
            hash = unchecked(hash * 31 + b.Geometry);
            if (b.NativeId == 0 || hash != b.ConfigHash)
            {
                b.ConfigHash = hash;
                if (!programs.TryGetValue((b.Variant, StateHash(b.States)), out int program))
                {
                    fixed (byte* vs = b.Variant.Vs.Code, ps = b.Variant.Ps.Code)
                    {
                        N.Program p = new N.Program { Vs = vs, Ps = ps, VsBytes = (uint)b.Variant.Vs.Code.Length, PsBytes = (uint)b.Variant.Ps.Code.Length };
                        for (int i = 0; i < 29; i++) p.State[i] = b.States[i];
                        program = N.Require(N.Op.Program, fullToken, &p); programs.Add((b.Variant, StateHash(b.States)), program);
                    }
                }
                b.Program = program;
                fixed (N.Buffer* buffers = b.Buffers) fixed (N.Patch* patches = b.Patches) fixed (N.Texture* textures = b.Textures) fixed (byte* bytes = b.Bytes)
                {
                    var draw = new N.Draw { Program = (uint)program, Geometry = (uint)b.Geometry, Buffers = buffers, BufferCount = (uint)b.Buffers.Length,
                        Patches = patches, PatchCount = (uint)b.Patches.Length, Textures = textures, TextureCount = (uint)b.Textures.Length,
                        Constants = bytes, ConstantBytes = (uint)b.Bytes.Length, Pose = (uint)s.Index, Grab = b.Pass.Grab ? 1u : 0u, Sprite = s.Sprite || s.Mask ? 1u : 0u };
                    for (int i = 0; i < 3; i++) { draw.Center[i] = localBounds.center[i]; draw.Extent[i] = localBounds.extents[i]; }
                    b.NativeId = N.Require(N.Op.Draw, fullToken, &draw); orderDirty = true; drawRebinds++;
                }
                Array.Copy(b.Bytes, b.Previous, b.Bytes.Length); // Config already owns its initial constants.
            }
            if (!b.ConstantsQueued) { b.ConstantsQueued = true; changedConstants.Add(b); }
        }
        static long StateHash(int[] values) { long h = 19; foreach (int x in values) h = unchecked(h * 397 ^ x); return h; }
        static float Float(Bound b, int id, string name, float fallback = 0)
        {
            var block = Block(b.Source);
            if (block.HasProperty(id)) return block.GetFloat(id);
            if (b.Material && b.Material.HasProperty(id)) return b.Material.GetFloat(id);
            foreach (var p in b.Shader.Properties) if (p.Id == id) return p.Default.x;
            return name.StartsWith("unity_", StringComparison.Ordinal) ? fallback : Shader.GetGlobalFloat(id);
        }
        static int Integer(Bound b, S.Variable v)
        {
            var block = Block(b.Source);
            if (block.HasProperty(v.Id)) return block.HasInteger(v.Id) ? block.GetInteger(v.Id) : (int)block.GetFloat(v.Id);
            if (b.Material && b.Material.HasProperty(v.Id)) return b.Material.HasInteger(v.Id) ? b.Material.GetInteger(v.Id) : (int)b.Material.GetFloat(v.Id);
            foreach (var p in b.Shader.Properties) if (p.Id == v.Id) return (int)p.Default.x;
            return Shader.GetGlobalInt(v.Id);
        }
        static Vector4 Value(Bound b, S.Variable v)
        {
            var s = b.Source; var mpb = Block(s);
            if (v.Name == "_RendererColor" && s.Sprite) return s.Sprite.color;
            if (v.Name == "_Flip") return new Vector4(s.Sprite && s.Sprite.flipX ? -1 : 1, s.Sprite && s.Sprite.flipY ? -1 : 1, 1, 1);
            if (v.Name == "_Cutoff" && s.Mask) return new Vector4(s.Mask.alphaCutoff, 0, 0, 0);
            if (mpb.HasProperty(v.Id)) return v.Cols == 1 ? new Vector4(v.Type == 1 && mpb.HasInteger(v.Id) ? mpb.GetInteger(v.Id) : mpb.GetFloat(v.Id), 0, 0, 0) : mpb.GetVector(v.Id);
            if (v.TextureName != null && !v.TextureST)
            {
                var tex = ResolveTexture(b, v.TextureName, v.TextureId);
                float w = tex ? tex.width : camera.targetTexture.width, h = tex ? tex.height : camera.targetTexture.height;
                return new Vector4(1 / w, 1 / h, w, h);
            }
            if (v.TextureName != null && v.TextureST)
            {
                int id = v.TextureId;
                if (b.Material && b.Material.HasProperty(id)) { var a = b.Material.GetTextureScale(id); var o = b.Material.GetTextureOffset(id); return new Vector4(a.x, a.y, o.x, o.y); }
            }
            foreach (var prop in b.Shader.Properties) if (prop.Id == v.Id)
            {
                if (!b.Material || !b.Material.HasProperty(v.Id)) return prop.Default;
                if (prop.Type == 0) return b.Material.GetColor(v.Id);
                if (prop.Type == 1) return b.Material.GetVector(v.Id);
                return new Vector4(v.Type == 1 && b.Material.HasInteger(v.Id) ? b.Material.GetInteger(v.Id) : b.Material.GetFloat(v.Id), 0, 0, 0);
            }
            if (v.Name == "unity_SpriteProps") return new Vector4(1, 1, -1, 0);
            if (v.Name == "unity_WorldTransformParams") return new Vector4(0, 0, 0, 1);
            return v.Cols == 1 ? new Vector4(v.Type == 1 ? Shader.GetGlobalInt(v.Id) : Shader.GetGlobalFloat(v.Id), 0, 0, 0) : Shader.GetGlobalVector(v.Id);
        }
        static UnityEngine.Texture ResolveTexture(Bound b, string name, int id)
        {
            var block = Block(b.Source);
            if (block.HasProperty(id)) return block.GetTexture(id);
            if (name == "_MainTex" && (b.Source.Sprite || b.Source.Mask)) return b.Source.Sprite ? b.Source.Sprite.sprite.texture : b.Source.Mask.sprite.texture;
            if (name == "_AlphaTex" && b.Source.Sprite) return b.Source.Sprite.sprite.associatedAlphaSplitTexture;
            if (b.Material && b.Material.HasProperty(id)) return b.Material.GetTexture(id);
            return Shader.GetGlobalTexture(id);
        }
        static UnityEngine.Texture DefaultTexture(Bound b, string name)
        {
            foreach (var p in b.Shader.Properties) if (p.Name == name) return p.DefaultTexture == "black" ? Texture2D.blackTexture : p.DefaultTexture == "gray" ? Texture2D.grayTexture : p.DefaultTexture == "bump" ? Texture2D.normalTexture : Texture2D.whiteTexture;
            return Texture2D.whiteTexture;
        }
        static IntPtr TexturePointer(UnityEngine.Texture texture)
        {
            if (!texturePointers.TryGetValue(texture, out var p)) { p = texture.GetNativeTexturePtr(); if (p == IntPtr.Zero) throw new InvalidOperationException("texture has no D3D resource"); texturePointers.Add(texture, p); }
            return p;
        }
        static bool Has(S.Variant v, string name) { foreach (var cb in v.Vs.Buffers) foreach (var x in cb.Variables) if (x.Name == name) return true; return false; }
        static int GeometryFor(Source s, S.Variant variant, int slot)
        {
            if (s.Sprite || s.Mask)
            {
                Sprite sp = s.Sprite ? s.Sprite.sprite : s.Mask.sprite; if (!sp) throw new InvalidOperationException("sprite not ready");
                int flip = s.Sprite && !Has(variant, "_Flip") ? (s.Sprite.flipX ? 1 : 0) | (s.Sprite.flipY ? 2 : 0) : 0;
                var key = (sp.GetInstanceID(), -1, flip); if (geometryCache.TryGetValue(key, out int id)) return id;
                var pos = sp.vertices; var uv = sp.uv; var triangles = sp.triangles;
                var vertices = new float[pos.Length * 9]; var indices = new uint[triangles.Length];
                for (int i = 0; i < pos.Length; i++) { int o = i * 9; vertices[o] = pos[i].x * ((flip & 1) != 0 ? -1 : 1); vertices[o + 1] = pos[i].y * ((flip & 2) != 0 ? -1 : 1); vertices[o + 2] = 0; for (int k = 3; k < 7; k++) vertices[o + k] = 1; vertices[o + 7] = uv[i].x; vertices[o + 8] = uv[i].y; }
                for (int i = 0; i < indices.Length; i++) indices[i] = triangles[i];
                var attrs = new[] { new N.Attribute { Semantic = 0, Format = 6, Offset = 0 }, new N.Attribute { Semantic = 3, Format = 2, Offset = 12 }, new N.Attribute { Semantic = 4, Format = 16, Offset = 28 } };
                fixed (float* vb = vertices) fixed (uint* ib = indices) fixed (N.Attribute* at = attrs)
                { var g = new N.Geometry { Attributes = at, AttributeCount = 3, Vertices = vb, VertexBytes = (uint)(vertices.Length * 4), CpuIndices = ib, CpuIndexCount = (uint)indices.Length }; g.Strides[0] = 36; id = N.Require(N.Op.Geometry, fullToken, &g); }
                geometryCache.Add(key, id); return id;
            }
            var mesh = s.Filter.sharedMesh; meshVersions.TryGetValue(mesh, out int version); int sub = Math.Min(slot, mesh.subMeshCount - 1);
            var meshKey = (mesh.GetInstanceID(), sub, version); if (geometryCache.TryGetValue(meshKey, out int existing)) return existing;
            if (mesh.vertexBufferCount > 8 || mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("unverified mesh layout/topology");
            var desc = mesh.GetSubMesh(sub); var attributes = mesh.GetVertexAttributes(); var list = new List<N.Attribute>();
            foreach (var a in attributes)
            {
                uint semantic = (uint)a.attribute; uint index = 0; if (semantic >= 4) { index = semantic - 4; semantic = 4; }
                list.Add(new N.Attribute { Semantic = semantic, Index = index, Format = Dxgi(a.format, a.dimension), Stream = (uint)a.stream, Offset = (uint)mesh.GetVertexAttributeOffset(a.attribute) });
            }
            // Missing mesh semantics cannot be silently substituted with white/zero.
            foreach (var input in variant.Vs.Inputs) if (input.System == 0)
            {
                bool found = false; foreach (var a in list) if (SemanticName(a.Semantic) == input.Name && a.Index == input.Index) found = true;
                if (!found) throw new InvalidOperationException("mesh lacks original signature " + input.Name + input.Index);
            }
            var array = list.ToArray(); fixed (N.Attribute* at = array)
            {
                var g = new N.Geometry { Attributes = at, AttributeCount = (uint)array.Length, Indices = mesh.GetNativeIndexBufferPtr(), IndexFormat = mesh.indexFormat == IndexFormat.UInt16 ? 57u : 42u, IndexStart = (uint)desc.indexStart, IndexCount = (uint)desc.indexCount, BaseVertex = desc.baseVertex };
                for (int i = 0; i < mesh.vertexBufferCount; i++) { g.Buffers[i] = mesh.GetNativeVertexBufferPtr(i).ToInt64(); g.Strides[i] = (uint)mesh.GetVertexBufferStride(i); }
                existing = N.Require(N.Op.Geometry, fullToken, &g);
            }
            geometryCache.Add(meshKey, existing); return existing;
        }
        static string SemanticName(uint x) => x == 0 ? "POSITION" : x == 1 ? "NORMAL" : x == 2 ? "TANGENT" : x == 3 ? "COLOR" : "TEXCOORD";
        static uint Dxgi(VertexAttributeFormat f, int n)
        {
            switch (f)
            {
                case VertexAttributeFormat.Float32: return n == 1 ? 41u : n == 2 ? 16u : n == 3 ? 6u : 2u;
                case VertexAttributeFormat.Float16: if (n == 2) return 34; if (n == 4) return 10; break;
                case VertexAttributeFormat.UNorm8: if (n == 4) return 28; if (n == 2) return 49; break;
                case VertexAttributeFormat.SNorm8: if (n == 4) return 31; if (n == 2) return 51; break;
                case VertexAttributeFormat.UNorm16: if (n == 2) return 35; if (n == 4) return 11; break;
                case VertexAttributeFormat.SNorm16: if (n == 2) return 37; if (n == 4) return 13; break;
                case VertexAttributeFormat.UInt32: return n == 1 ? 42u : n == 2 ? 17u : n == 3 ? 7u : 3u;
                case VertexAttributeFormat.SInt32: return n == 1 ? 43u : n == 2 ? 18u : n == 3 ? 8u : 4u;
            }
            throw new InvalidOperationException("unverified DXGI vertex format " + f + "x" + n);
        }
        static void UpdateKey(Source s)
        {
            var key = FullKey(s.R, s.Materials.Count != 0 ? s.Materials[0] : null, 0);
            if (!s.Mask && s.Passes != null && s.Passes.Length != 0 && s.Key.CompareTo(key) != 0) { orderDirty = true; orderKeyChanges++; } s.Key = key;
        }
        static Vector3 LocalCenter(Source s) => s.Sprite && s.Sprite.spriteSortPoint == SpriteSortPoint.Pivot ? Vector3.zero : s.R.localBounds.center;
        static Vector4 SortSignature()
        {
            var mode = camera.transparencySortMode; if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = TransparencySortMode.Orthographic;
            Vector3 axis = mode == TransparencySortMode.CustomAxis ? (camera.transparencySortMode == TransparencySortMode.CustomAxis ? camera.transparencySortAxis : GraphicsSettings.transparencySortAxis) : camera.transform.forward;
            if (mode == TransparencySortMode.Perspective) throw new InvalidOperationException("M4c perspective sort not verified");
            return new Vector4(axis.x, axis.y, axis.z, (int)mode);
        }
        static SceneRenderOrder.Key FullKey(Renderer r, Material m, int slot)
        {
            var signature = sortSignature; float d;
            if (distances.IsCreated && rendererSources.TryGetValue(r, out var source) && source.PoseIndex >= 0) d = distances[source.PoseIndex];
            else { var p = r.bounds.center; if (r is SpriteRenderer sr && sr.spriteSortPoint == SpriteSortPoint.Pivot) p = r.transform.position; d = p.x * signature.x + p.y * signature.y + p.z * signature.z; }
            if (rendererSources.TryGetValue(r, out var groupedSource) && groupedSource.Groups.Length != 0) d = 0;
            return new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(r.sortingLayerID), r.sortingOrder, m ? m.renderQueue : 3000, d, m ? m.GetInstanceID() : 0, r.GetInstanceID(), slot, SceneRenderOrder.ShaderRank(m));
        }
        static int FullPhase(Entry e) => e.Sub == 0 && e.Canvas == null && e.Key.Queue <= 2500 ? 0 : 1;
        static int CompareFull(Entry a, Entry b)
        {
            int phase = FullPhase(a).CompareTo(FullPhase(b)); if (phase != 0) return phase;
            int grouped = CompareGroups(a, b); if (grouped != 0) return grouped;
            int c = a.Layer.CompareTo(b.Layer); if (c != 0) return c;
            c = a.Order.CompareTo(b.Order); if (c != 0) return c;
            c = a.Sub.CompareTo(b.Sub); if (c != 0) return c;
            if (a.Sub != 0) return a.Source.Index.CompareTo(b.Source.Index);
            c = a.Key.CompareTo(b.Key); return c != 0 ? c : (a.Draw?.Pass.Index ?? 0).CompareTo(b.Draw?.Pass.Index ?? 0);
        }
        static void RecordSegment(CommandBuffer cb, ref int start)
        {
            if (segmentCount >= 65535) throw new InvalidOperationException("M4c segment limit");
            segments[segmentCount * 2] = (uint)start; segments[segmentCount * 2 + 1] = (uint)(listCount - start);
            cb.IssuePluginEvent(new IntPtr(N.Call(N.Op.Event, 0)), (fullToken << 16) | segmentCount);
            segmentCount++; start = listCount;
        }
        static void RebuildFullOrder()
        {
            ordered.Clear();
            foreach (var s in sources) for (int i = 0; i < s.Groups.Length; i++) s.GroupKeys[i] = GroupKey(s, i);
            foreach (var s in sources)
            {
                if (!s.Live || s.Passes == null) continue;
                foreach (var b in s.Passes)
                {
                    if (s.Mask)
                    {
                        if (s.Mask.isCustomRangeActive && (SortingLayer.GetLayerValueFromID(s.Mask.frontSortingLayerID) < SortingLayer.GetLayerValueFromID(s.Mask.backSortingLayerID)
                            || s.Mask.frontSortingLayerID == s.Mask.backSortingLayerID && s.Mask.frontSortingOrder <= s.Mask.backSortingOrder)) continue;
                        int layer = b.MaskKind == 1 ? int.MinValue : int.MaxValue, order = layer;
                        if (s.Mask.isCustomRangeActive) { layer = SortingLayer.GetLayerValueFromID(b.MaskKind == 1 ? s.Mask.backSortingLayerID : s.Mask.frontSortingLayerID); order = b.MaskKind == 1 ? s.Mask.backSortingOrder : s.Mask.frontSortingOrder; }
                        var e = b.Entry; e.Layer = layer; e.Order = order; e.Sub = b.MaskKind == 1 ? -2 : -1;
                        e.Key = new SceneRenderOrder.Key(layer, order, int.MinValue, float.MaxValue, int.MinValue, s.R.GetInstanceID()); ordered.Add(e);
                    }
                    else
                    {
                        var key = FullKey(s.R, b.Material, b.Slot);
                        var e = b.Entry; e.Key = key; e.Layer = key.Layer; e.Order = key.Order; ordered.Add(e);
                    }
                }
            }
            AddCanvasEntries();
            ordered.Sort(CompareFull);
            before.Clear(); opaque.Clear(); listCount = segmentCount = 0;
            bool opaquePhase = ordered.Count != 0 && FullPhase(ordered[0]) == 0;
            var cb = opaquePhase ? opaque : before; int start = 0;
            BeginCanvasRuns();
            bool anyMasks = false; int maskDepth = 0;
            foreach (var e in ordered)
            {
                if (opaquePhase && FullPhase(e) != 0) { RecordSegment(cb, ref start); cb = before; opaquePhase = false; }
                if (e.Sub == -2) maskDepth++; else if (e.Sub == -1) maskDepth--;
                if (maskDepth < 0 || maskDepth > 254) throw new InvalidOperationException("M4c invalid/saturated mask nesting");
                if (e.Canvas != null) { RecordCanvasEntry(cb, e, ref start); continue; }
                EndCanvasRun();
                if (e.Draw != null) { if (listCount == drawList.Length) throw new InvalidOperationException("M4c draw list capacity"); drawList[listCount++] = (uint)e.Draw.NativeId; if (e.Sub != 0) anyMasks = true; }
                else { RecordSegment(cb, ref start); cb.DrawRenderer(e.Source.R, e.Fallback, e.Source.Filter && e.Source.Filter.sharedMesh ? Math.Min(e.Slot, e.Source.Filter.sharedMesh.subMeshCount - 1) : 0, -1); }
            }
            EndCanvasRun();
            RecordSegment(cb, ref start);
            if (maskDepth != 0) throw new InvalidOperationException("M4c unbalanced mask range");
            if (anyMasks && camera.targetTexture.depth < 24) throw new InvalidOperationException("M4c masks require a stencil-capable camera depth target");
            clearStencil = anyMasks;
            sendList = true; orderDirty = false; orderBuilds++;
        }
        static void FullPreCull()
        {
            long t = Stopwatch.GetTimestamp();
            try
            {
                if (fullRescan) { RecreateFull(); return; }
                long phase = Stopwatch.GetTimestamp();
                PromoteSources();
                UpdateCanvasView(camera);
                if (canvasDirty) ReplanCanvases();
                if (camera.targetTexture != fullTarget || !fullTarget || fullTarget.width != fullWidth || fullTarget.height != fullHeight || fullTarget.antiAliasing != 1) throw new InvalidOperationException("camRT changed");
                N.Stats stats = default; N.Require(N.Op.Stats, fullToken, &stats);
                if (stats.Errors != 0 || stats.Dropped != 0) throw new InvalidOperationException("native frame invalid errors=" + stats.Errors + " dropped=" + stats.Dropped + " hr=" + stats.LastHr.ToString("X8"));
                if (sortSignature != SortSignature()) { sortSignature = SortSignature(); orderDirty = true; }
                if (globalsDirty) { foreach (var s in sources) Queue(s); globalsDirty = false; }
                foreach (var s in changed) { s.Queued = false; if (!s.R) fullRescan = true; else Refresh(s, false); } changed.Clear();
                if (fullRescan) { RecreateFull(); return; }
                if (transformsDirty) BuildPoseTracking();
                collectTicks += Stopwatch.GetTimestamp() - phase; phase = Stopwatch.GetTimestamp();
                var sig = SortSignature();
                var job = new Capture { Poses = poses, Centers = centers, Distances = distances, Axis = new Vector3(sig.x, sig.y, sig.z), Changed = moved.AsParallelWriter(), Reorder = reordered.AsParallelWriter() }; job.ScheduleReadOnly(transforms, 64).Complete();
                bool first = !haveNativeFrame;
                poseCount = first ? poseSources.Count : 0;
                if (first) for (int i = 0; i < poseCount; i++) { poseIds[i] = (uint)poseSources[i].Index; uploadPoses[i] = poses[i]; }
                while (moved.TryDequeue(out int index)) { var s = poseSources[index]; if (!s.R) { fullRescan = true; continue; } if (!first) { poseIds[poseCount] = (uint)s.Index; uploadPoses[poseCount++] = poses[index]; } }
                if (fullRescan) { RecreateFull(); return; }
                while (reordered.TryDequeue(out int index)) if (poseSources[index].R) { var source = poseSources[index]; if (source.Groups.Length != 0) orderDirty = true; UpdateKey(source); }
                poseTicks += Stopwatch.GetTimestamp() - phase; phase = Stopwatch.GetTimestamp();
                if (orderDirty) { long rp = Stopwatch.GetTimestamp(); RebuildFullOrder(); replanMaxTicks = Math.Max(replanMaxTicks, Stopwatch.GetTimestamp() - rp); }
                sortTicks += Stopwatch.GetTimestamp() - phase;
                RenderCanvases();
                long commit = Stopwatch.GetTimestamp(); CommitFull(); packetTicks += Stopwatch.GetTimestamp() - commit; commitMaxTicks = Math.Max(commitMaxTicks, Stopwatch.GetTimestamp() - commit); haveNativeFrame = true;
            }
            catch (Exception e) { Log("FULL frame failed: " + e); Stop("full frame invalid; restored"); }
            finally
            {
                // Include notification rescans and visibility replans, even when
                // their branch returns early after committing the replacement.
                if (rendering) { long ticks = Stopwatch.GetTimestamp() - t; fullFrames++; fullTicks += ticks; if (ticks > fullMaxTicks) fullMaxTicks = ticks; }
            }
        }
        static void CommitFull()
        {
            if (dirtyIds.Length < changedConstants.Count) { Array.Resize(ref dirtyIds, changedConstants.Count * 2); Array.Resize(ref constantOffsets, changedConstants.Count * 2 + 1); }
            dirtyCount = constantCount = 0; constantOffsets[0] = 0;
            // Only notification-dirty constants are compared; poses and built-ins
            // have separate job/native update paths.
            foreach (var b in changedConstants)
            {
                b.ConstantsQueued = false;
                if (b.NativeId == 0) continue;
                bool different = false; for (int i = 0; i < b.Bytes.Length; i++) if (b.Bytes[i] != b.Previous[i]) { different = true; break; }
                if (!different) continue;
                if (constantCount + b.Bytes.Length > constantBytes.Length) Array.Resize(ref constantBytes, Math.Max(constantBytes.Length * 2, constantCount + b.Bytes.Length));
                Array.Copy(b.Bytes, 0, constantBytes, constantCount, b.Bytes.Length); Array.Copy(b.Bytes, b.Previous, b.Bytes.Length);
                dirtyIds[dirtyCount++] = (uint)b.NativeId; constantCount += b.Bytes.Length; constantOffsets[dirtyCount] = (uint)constantCount;
            }
            changedConstants.Clear();
            var target = camera.targetTexture; var p = camera.transform.position; float time = Time.time, dt = Time.deltaTime, smooth = Time.smoothDeltaTime;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            fixed (uint* pi = poseIds, di = dirtyIds, co = constantOffsets, dl = drawList, seg = segments) fixed (byte* bytes = constantBytes)
            {
                var f = new N.Frame { Vp = projection * camera.worldToCameraMatrix, View = camera.worldToCameraMatrix, Projection = projection,
                    Camera = new Vector4(p.x, p.y, p.z, 1), Screen = new Vector4(target.width, target.height, 1f + 1f / target.width, 1f + 1f / target.height),
                    ProjectionParams = new Vector4(-1, camera.nearClipPlane, camera.farClipPlane, 1f / camera.farClipPlane),
                    Ortho = new Vector4(camera.orthographicSize * camera.aspect, camera.orthographicSize, 0, 1), Time = new Vector4(time / 20, time, time * 2, time * 3),
                    SinTime = new Vector4(Mathf.Sin(time / 8), Mathf.Sin(time / 4), Mathf.Sin(time / 2), Mathf.Sin(time)), CosTime = new Vector4(Mathf.Cos(time / 8), Mathf.Cos(time / 4), Mathf.Cos(time / 2), Mathf.Cos(time)),
                    Delta = new Vector4(dt, dt != 0 ? 1 / dt : 0, smooth, smooth != 0 ? 1 / smooth : 0),
                    Poses = (N.Pose*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(uploadPoses), PoseIds = pi, PoseCount = (uint)poseCount,
                    DrawIds = di, ConstantOffsets = co, Constants = bytes, DirtyCount = (uint)dirtyCount, ConstantBytes = (uint)constantCount,
                    List = dl, Segments = seg, ListCount = (uint)listCount, SegmentCount = (uint)segmentCount, ListChanged = sendList ? 1u : 0u, ClearStencil = clearStencil ? 1u : 0u };
                N.Require(N.Op.CommitPacked, fullToken, &f); sendList = false;
            }
        }
        static string FullState()
        {
            N.Stats s = default; N.Require(N.Op.Stats, fullToken, &s);
            N.Performance perf = default; N.Require(N.Op.Perf, fullToken, &perf);
            double phaseScale = 1e6 / Stopwatch.Frequency / Math.Max(1, fullFrames);
            return FormattableString.Invariant($"mode=M4e-original active=True native={nativeCount} unityExplicit={fallbackCount} sources={sources.Count} dormant={dormant.Count} poseActive={poseSources.Count} removedMask={removedMask} retainedMask={retainedMask} frames={s.Frames} draws={s.Draws} errors={s.Errors} dropped={s.Dropped} main_us={(fullFrames != 0 ? fullTicks * 1e6 / Stopwatch.Frequency / fullFrames : 0):F3} main_max_us={fullMaxTicks * 1e6 / Stopwatch.Frequency:F3} render_us={s.RenderUs:F3} commit_us={s.CommitUs:F3} replans={orderBuilds} commit_max_us={commitMaxTicks * 1e6 / Stopwatch.Frequency:F3} replan_max_us={replanMaxTicks * 1e6 / Stopwatch.Frequency:F3} {CanvasState()} collect_us={collectTicks * phaseScale:F3} pose_us={poseTicks * phaseScale:F3} sort_us={sortTicks * phaseScale:F3} packet_us={packetTicks * phaseScale:F3} pose_uploads={poseCount} dirty_draws={dirtyCount} packet_bytes={constantCount} rt_apply_us={perf.ApplyUs:F3} rt_cull_us={perf.CullUs:F3} rt_constants_us={perf.ConstantsUs:F3} rt_draw_us={perf.DrawUs:F3} rt_execute_us={perf.ExecuteUs:F3} rt_uploads={perf.Uploads} rt_upload_bytes={perf.UploadBytes} layouts={perf.LayoutCreates} layout_hits={perf.LayoutHits} rt_sample_frames={perf.SampledFrames} avoided_mask_replans={avoidedMaskReplans} avoided_empty_replans={avoidedEmptyReplans} canvas_pose_signals={canvasPoseSignals} order_key_changes={orderKeyChanges} draw_rebinds={drawRebinds} prepare_slices={prepareSlices} prepare_max_ms={prepareMaxTicks * 1000.0 / Stopwatch.Frequency:F3} prepare_work_ms={prepareWorkTicks * 1000.0 / Stopwatch.Frequency:F3} parity=UNMEASURED");
        }
        static string WhyReplan()
        {
            foreach (var pair in signalWriters) Log("signal target=" + pair.Key.Item1.FullName + " writer=" + pair.Key.Item2 + " count=" + pair.Value);
            return "replans=" + orderBuilds + " unchangedMask=" + avoidedMaskReplans + " emptySprite=" + avoidedEmptyReplans + " canvasPose=" + canvasPoseSignals + " changedKey=" + orderKeyChanges + " reboundDraw=" + drawRebinds;
        }
        static void AssertFull()
        {
            N.Stats s = default; N.Require(N.Op.Stats, fullToken, &s);
            if (uncertainCanvases != 0) throw new InvalidOperationException("drfull INVALID: unverified canvas blend; " + CanvasState());
            if (s.Frames == 0 || s.Draws == 0 || s.Errors != 0 || s.Dropped != 0 || Time.frameCount - startedFrame > cameraFrames + 8)
                throw new InvalidOperationException("drfull INVALID: missing/stale native draws or errors/drops; " + FullState());
        }
        static void ReleaseFull()
        {
            rendering = false;
            if (before != null) { if (camera) camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, before); before.Release(); before = null; }
            if (opaque != null) { if (camera) camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, opaque); opaque.Release(); opaque = null; }
            ReleaseCanvases();
            if (fullToken != 0) { N.Call(N.Op.Destroy, fullToken); fullToken = 0; }
            if (transforms.isCreated) transforms.Dispose(); if (poses.IsCreated) poses.Dispose(); if (moved.IsCreated) moved.Dispose();
            if (centers.IsCreated) centers.Dispose(); if (distances.IsCreated) distances.Dispose(); if (reordered.IsCreated) reordered.Dispose();
            foreach (var l in sourceLifetimes) if (l) { l.Changed = null; UnityEngine.Object.Destroy(l); }
            sourceLifetimes.Clear(); sources.Clear(); changed.Clear(); changedConstants.Clear(); ordered.Clear(); sourceIndex.Clear();
            geometryCache.Clear(); meshVersions.Clear(); programs.Clear(); texturePointers.Clear(); fallbackReasons.Clear();
            rendererSources.Clear(); dormant.Clear(); pendingPromotions.Clear(); poseSources.Clear();
            if (uploadPoses.IsCreated) uploadPoses.Dispose();
            poseIds = dirtyIds = constantOffsets = drawList = segments = null; constantBytes = null;
            fullTarget = null;
            fullRescan = canvasDirty = false;
            nativeCount = fallbackCount = 0; avoidedMaskReplans = avoidedEmptyReplans = canvasPoseSignals = orderKeyChanges = drawRebinds = 0; signalWriters.Clear(); orderDirty = sendList = globalsDirty = false;
        }
    }
}
#endif
