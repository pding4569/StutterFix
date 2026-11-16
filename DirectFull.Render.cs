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
            internal bool ExplicitVisible;
            internal MeshFilter Filter; internal int Index, PoseIndex = -1; internal bool Queued, Live, GroupsDirty, TintOnly, KeyOnly, HierarchyDirty, NeedsInverse, Prebuild;
            internal SceneRenderOrder.Key Key; internal Bound[] Passes;
            internal readonly HashSet<UnityEngine.Object> Indexed = new HashSet<UnityEngine.Object>();
            internal readonly List<Material> Materials = new List<Material>();
            internal readonly MaterialPropertyBlock RendererBlock = new MaterialPropertyBlock(), SlotBlock = new MaterialPropertyBlock();
            internal DirectFullSourceLifetime Lifetime;
            internal Bound[] OrderedPasses; internal bool OrderQueued, LiveQueued, DistanceOnly, StateOnly;
            internal int MaterialCount; internal Sprite MaskSprite; internal (bool,int,int,int,int) MaskOrder;
            internal readonly List<SortingGroup> GroupScratch = new List<SortingGroup>();
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
            internal int[] States = new int[29]; internal long ConfigHash, HashStates, HashTex, HashBounds; internal int HashGeometry; internal bool HasRendererColor, HasFlip;
            internal S.Stage[] Stages; internal bool ConstantsQueued;
            internal Entry Entry;
            internal (S.Variable Variable, int Offset)[] MaterialWrites;
            internal int[] TintOffsets;
            internal bool StatesInitialized;
        }
        sealed class Entry
        {
            internal Source Source; internal Bound Draw; internal Material Fallback; internal int Slot, Layer, Order, Sub;
            internal SceneRenderOrder.Key Key; internal CanvasUnit Canvas; internal int RecordedPhase; internal bool RecordedLive, RecordedIsolated;
            internal int[] GroupIds = Array.Empty<int>(); internal SceneRenderOrder.Key[] GroupKeys = Array.Empty<SceneRenderOrder.Key>();
            internal int[] ExplicitPasses; internal bool RecordedVisible;
            internal DirectFullOrderTree<Entry>.Node Node;
            internal int NativeListIndex = -1, NativeSegment = -1;
            internal int OrderIndex = -1; internal SceneRenderOrder.Key PreviousDistanceKey;

        }
        struct Capture : IJobParallelForTransform
        {
            internal NativeArray<N.Pose> Poses;
            [ReadOnly] internal NativeArray<Vector3> Centers;
            internal NativeArray<Vector3> PreviousCenters;
            internal NativeArray<float> Distances;
            [ReadOnly] internal NativeArray<byte> InverseRequired;
            internal NativeArray<byte> Changed, Reorder;
            internal NativeQueue<int>.ParallelWriter Changes;
            internal Vector3 Axis;
            internal bool AxisChanged;
            public void Execute(int index, TransformAccess access)
            {
                Changed[index] = Reorder[index] = 0;
                if (!access.isValid) { Changed[index] = 2; Changes.Enqueue(index); return; }
                Matrix4x4 w = access.localToWorldMatrix;
                N.Pose old = Poses[index]; bool changed = !old.World.Equals(w);
                if (changed) { Poses[index] = new N.Pose { World = w, Inverse = InverseRequired[index] != 0 ? access.worldToLocalMatrix : default }; Changed[index] = 1; }
                if (!changed && !AxisChanged && PreviousCenters[index].Equals(Centers[index])) return;
                PreviousCenters[index] = Centers[index];
                Vector3 center = w.MultiplyPoint3x4(Centers[index]); float d = Vector3.Dot(center, Axis);
                if (Distances[index] != d) { Distances[index] = d; Reorder[index] = 1; }
                if (changed || Reorder[index] != 0) Changes.Enqueue(index);
            }
        }
        static Harmony warmHooks;
        static double warmMs;
        static void Warm()
        {
            if (warmHooks != null) return;
            if (Active || preparing) throw new InvalidOperationException("drfull warm requires off");
            var owner = new Harmony(Owner + ".Warm"); long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { foreach (var step in DirectSequenceChanges.InstallSteps(owner)) { } DirectFullShaders.Load(); warmHooks = owner; }
            catch { owner.UnpatchAll(owner.Id); throw; }
            warmMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log("warm complete hooks=" + DirectSequenceChanges.Methods + " ms=" + warmMs.ToString("F3") + " retained observers dormant while off");
        }
        static void Cold() { var h = warmHooks; warmHooks = null; h?.UnpatchAll(h.Id); }
        static readonly Dictionary<string,long> prepareStageTicks = new Dictionary<string,long>();
        static void RecordPreparationStage(string stage, long ticks) { if (stage == null) return; prepareStageTicks.TryGetValue(stage, out long old); prepareStageTicks[stage] = old + ticks; }
        static long preparationWorkStart;
        static void BeginPreparationWork() { preparationWorkStart = Stopwatch.GetTimestamp(); }
        static void EndPreparationWork() { if (preparationWorkStart == 0) return; RecordPreparationStage(prepareStage, Stopwatch.GetTimestamp() - preparationWorkStart); preparationWorkStart = 0; }
        static void SetPreparationStage(string next) { if (preparationWorkStart != 0) { RecordPreparationStage(prepareStage, Stopwatch.GetTimestamp() - preparationWorkStart); preparationWorkStart = Stopwatch.GetTimestamp(); } prepareStage = next; }
        static double StageMs(string stage) => prepareStageTicks.TryGetValue(stage, out long ticks) ? ticks * 1000.0 / Stopwatch.Frequency : 0;
        static IEnumerator preparation;
        static bool preparing, unityGrab;
        static long prepareSlices, prepareMaxTicks, prepareWorkTicks, preparationFinishedTicks, firstReadyTicks;
        static string prepareStage;
        static int prepareOriginal;
        static readonly HashSet<Renderer> dormant = new HashSet<Renderer>();
        static readonly List<Renderer> rendererScratch = new List<Renderer>(); static readonly List<Canvas> canvasScratch = new List<Canvas>();
        static readonly HashSet<Renderer> pendingPromotions = new HashSet<Renderer>();
        static readonly HashSet<Canvas> pendingCanvasPromotions = new HashSet<Canvas>(); static bool prebuildDormant; static long prebuiltSources;
        // 미리 만들 대상: 우리가 원래 셰이더로 직접 그릴 수 있는 단순 스프라이트만(유니티 대체 그리기가 되는 것은 제외).
        static bool PrebuildEligible(Renderer r)
        {
            if (!(r is SpriteRenderer sr) || !sr.sprite || sr.drawMode != SpriteDrawMode.Simple) return false;
            var m = sr.sharedMaterial; if (!m || !m.shader || m.renderQueue < 2501) return false;
            string n = m.shader.name; if (n == "Hidden/BlendModes/VisualDeco/Grab") return false;
            return S.Load().ContainsKey(n);
        }
        static long deadSources, canvasPromotions, hierarchyIncremental, refreshCount, refreshTint, refreshTicks, rebindStates, rebindTextures, rebindBounds, rebindGeometry;
        static readonly List<Source> poseSources = new List<Source>();
        static NativeArray<N.Pose> uploadPoses;
        static bool transformsDirty;
        static int poseCapacity, inversePoseCount; static bool frameWorldOnly; static long poseAdds, poseRemoves, incrementalKeys, liveToggles;
        static uint entrySerial;
        static readonly DirectFullOrderTree<Entry> orderTree = new DirectFullOrderTree<Entry>(CompareFull);
        static readonly List<Source> orderChanges = new List<Source>(), liveChanges = new List<Source>();
        static readonly Dictionary<string,long> orderReasons = new Dictionary<string,long>();
        static uint[] liveIds; static byte[] liveValues;
        static void DirtyOrder(Source s, string reason)
        {
            orderReasons.TryGetValue(reason, out long n); orderReasons[reason] = n + 1;
            bool distance = reason == "key-distance";
            if (!s.OrderQueued) { s.OrderQueued = true; s.DistanceOnly = distance; orderChanges.Add(s); }
            else s.DistanceOnly &= distance;
            orderDirty = true;
        }
        static void DirtyAllOrder(string reason) { foreach (var s in sources) DirtyOrder(s, reason); foreach (var u in canvasUnits) u.OrderDirty = true; orderDirty = true; }
        static void LiveChanged(Source s)
        {
            if (AffectsNamedGrab(s)) namedGrabOwnershipDirty = true;
            if (!s.LiveQueued) { s.LiveQueued = true; liveChanges.Add(s); }
            liveToggles++; transformsDirty = true;
            // Native entries retain their order and use a packet-owned live bit.
            if (s.Passes != null) foreach (var b in s.Passes) if (b.NativeId == 0) { orderDirty = true; break; }
        }
        static bool rendering, orderDirty, sendList, globalsDirty, clearStencil, namedGrabOwnershipDirty = true;
        static int fullToken, nativeCount, fallbackCount, removedMask, retainedMask, segmentCount, listCount;
        static readonly List<Source> sources = new List<Source>(), changed = new List<Source>();
        static readonly List<Entry> ordered = new List<Entry>();
        static readonly List<Bound> changedConstants = new List<Bound>();
        static readonly Dictionary<UnityEngine.Object, List<Source>> sourceIndex = new Dictionary<UnityEngine.Object, List<Source>>();
        static readonly Dictionary<(int,int,int), int> geometryCache = new Dictionary<(int,int,int), int>();
        static readonly Dictionary<Mesh, int> meshVersions = new Dictionary<Mesh, int>();
        static readonly Dictionary<(S.Variant,long), int> programs = new Dictionary<(S.Variant,long), int>();
        static readonly Dictionary<UnityEngine.Texture, IntPtr> texturePointers = new Dictionary<UnityEngine.Texture, IntPtr>();
        static readonly Dictionary<string, uint> grabNames = new Dictionary<string, uint>(StringComparer.Ordinal);
        static uint GrabId(string name) { if (string.IsNullOrEmpty(name)) return 1; if (!grabNames.TryGetValue(name, out uint id)) { id = (uint)grabNames.Count + 2; grabNames.Add(name, id); namedGrabOwnershipDirty = true; } return id; }
        static readonly List<DirectFullSourceLifetime> sourceLifetimes = new List<DirectFullSourceLifetime>();
        static readonly Dictionary<Renderer, Source> rendererSources = new Dictionary<Renderer, Source>();
        static CommandBuffer before, opaque;
        static TransformAccessArray transforms;
        static NativeArray<N.Pose> poses;
        static NativeQueue<int> poseChanges;
        static NativeArray<Vector3> centers;
        static NativeArray<Vector3> previousCenters;
        static Vector3 captureAxis; static bool haveCaptureAxis;
        static NativeArray<float> distances;
        static NativeArray<byte> moved, reordered, inverseRequired;
        static bool haveNativeFrame;
        static long commitMaxTicks, replanMaxTicks;
        static bool orderCommandsReady;
        static long avoidedOrderReplans, keyOnlyFrames, commitGrowths, commitGrowthBytes;
        static long maxCollect, maxPose, maxSort, maxCanvas, maxPacket, maxGc, maxFrameNumber;
        static long peakPacketPoses, peakPacketDirty, peakPacketBytes, commitPeakGc, commitPeakGrowths;
        static long collectTicks, poseTicks, sortTicks, packetTicks;
        static long collectSignals, collectTintSignals, collectFullSignals, fillCalls, fillTicks, collectRefreshTicks, collectPromoteTicks;
        static bool sampleCollect;
        static long collectStateSignals, collectVisited, collectCanvasTicks, collectStatsTicks, collectTrackingTicks, poseWaitTicks, poseScheduleTicks, poseResultsTicks, poseChangedSlots;
        static long poseWaitMaxTicks;
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
            if (stats.Frames != 0 && firstReadyTicks == 0) firstReadyTicks = Stopwatch.GetTimestamp();
            return stats.Frames != 0;
        }

        static void StartFull()
        {
            if (probeHidden) throw new InvalidOperationException("probe restore before starting renderer; hidden-reference diagnostic cannot be a normal sample");
            if (rendering || preparing) { CheckLease(); return; }
            if (Active) Stop("switch ceiling to full");
            firstReadyTicks = 0; prepareStageTicks.Clear(); fullStartTicks = Stopwatch.GetTimestamp(); prepareSlices = prepareMaxTicks = prepareWorkTicks = 0;
            camera = GpuDecoScene.FindMain(); prepareOriginal = camera.cullingMask;
            preparationCamera = camera;
            hooks = new Harmony(Owner);
            preparing = true; SetPreparationStage("hooks");
            preparation = PrepareFull();
            preparationObject = new GameObject("StutterFix.DirectFull.Prepare");
            preparationDriver = preparationObject.AddComponent<DirectFullPreparation>();
        }
        static IEnumerator PrepareFull()
        {
            prebuildDormant = System.IO.File.Exists(System.IO.Path.Combine(Main.Entry.Path, "drfull-prebuild.txt")); prebuiltSources = 0;
            if (warmHooks == null) foreach (var step in DirectSequenceChanges.InstallSteps(hooks)) yield return null;


            SetPreparationStage("discover");
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
                SetPreparationStage("canvas");
                foreach (var step in PrepareCanvasesSteps(main, original)) yield return null;
                if (removedMask == 0) throw new InvalidOperationException("M4d empty original camera mask");
                retainedMask = 0;
                SetPreparationStage("shader-resource"); S.Load(); yield return null;
                N.ValidateAbi();
                int capacity = 0;
                foreach (var r in renderers) if (InCameraScene(r, original)) capacity++;
                capacity = Math.Max(1, capacity + canvasUnits.Count + 64); // 캔버스는 run 당 합성 1개(최대 캔버스 수)
                poseCapacity = capacity;
                sources.Capacity = poseSources.Capacity = orderChanges.Capacity = liveChanges.Capacity = capacity;
                ordered.Capacity = capacity * 4;
                SetPreparationStage("native"); CreateNative(rt, capacity); yield return null;
                SetPreparationStage("bind");
                foreach (var r in renderers)
                {
                    yield return null;
                    if (!InCameraScene(r, original)) continue;
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff)
                    {
                        // 실험(drfull-prebuild.txt): 꺼진 장식도 준비 때 draw 를 미리 만들어 둔다. 곡 중 처음 켜질 때 새 draw·재계획 없이 live 비트만 바뀐다.
                        if (prebuildDormant && PrebuildEligible(r)) { var ps = AddSource(r, true); prebuiltSources++; LiveChanged(ps); continue; }
                        dormant.Add(r); continue;
                    }
                    AddSource(r);
                }

                changed.Capacity = Math.Max(changed.Capacity, capacity); changedConstants.Capacity = Math.Max(changedConstants.Capacity, capacity * 4);
                SetPreparationStage("packets"); BuildPoseTracking();
                int packetCapacity = Math.Max(64, sources.Count + canvasUnits.Count);
                dirtyIds = new uint[packetCapacity * 8]; constantOffsets = new uint[packetCapacity * 8 + 1];
                drawList = new uint[capacity * 4]; segments = new uint[131072]; constantBytes = new byte[Math.Max(65536, packetCapacity * 1024)];
                before = new CommandBuffer { name = "StutterFix.DirectFull.BeforeAlpha" };
                opaque = new CommandBuffer { name = "StutterFix.DirectFull.BeforeOpaque" };
                main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, opaque); main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, before);
                InstallTimeline();
                // Install managed write-site observers. Native Unity setters themselves
                // are never patched. Existing game transforms/materials keep updating.
                PromoteSources();
                if (fullRescan) throw new InvalidOperationException("hierarchy changed during preparation; retry on stable map; why=" + rescanWhy + " count=" + rescanCount);
                SetPreparationStage("publish");
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
                preparationFinishedTicks = Stopwatch.GetTimestamp(); preparing = false;
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
            var config = new N.Configuration { Submission = deferredSubmission ? 1u : 0u, ConstantRing = legacyConstants ? 0u : 1u };
            N.Require(N.Op.Configure, fullToken, &config);
            var resources = resourceConfig; N.Require(N.Op.ResourceConfigure, fullToken, &resources);
            var cache = new N.CacheConfiguration { Enabled = commandCache ? 1u : 0u, ChunkSize = 128 }; N.Require(N.Op.CacheConfigure, fullToken, &cache);
            var tune = new N.CacheTune { IndirectBatches = indirectCacheBatches ? 1u : 0u }; N.Require(N.Op.CacheTuning, fullToken, &tune);
            var probe = drawProbeConfig; N.Require(N.Op.DrawProbeConfigure, fullToken, &probe);
        }
        static Source AddSource(Renderer r, bool prebuild = false)
        {
            var s = new Source { R = r, Sprite = r as SpriteRenderer, Mask = r as SpriteMask, Filter = r.GetComponent<MeshFilter>(), Index = sources.Count, Prebuild = prebuild };
            sources.Add(s); rendererSources.Add(r, s); Index(r, s);
            for (Transform t = r.transform; t; t = t.parent) { Index(t, s); Index(t.gameObject, s); var g = t.GetComponent<SortingGroup>(); if (g) Index(g, s); }
            var l = r.gameObject.AddComponent<DirectFullSourceLifetime>(); l.Changed = () => { s.GroupsDirty = true; Queue(s); }; s.Lifetime = l; sourceLifetimes.Add(l);
            Refresh(s, true); s.Prebuild = false; transformsDirty = true; return s;
        }
        static void PromoteSources()
        {
            foreach (var r in pendingPromotions)
                if (r && dormant.Contains(r) && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff)
                { dormant.Remove(r); AddSource(r); }
            pendingPromotions.Clear();
            if (pendingCanvasPromotions.Count != 0) PromoteCanvases();
        }
        static void BuildPoseTracking()
        {
            if (!transforms.isCreated)
            {
                transforms = new TransformAccessArray(poseCapacity);
                poses = new NativeArray<N.Pose>(poseCapacity, Allocator.Persistent);
                uploadPoses = new NativeArray<N.Pose>(poseCapacity, Allocator.Persistent);
                centers = new NativeArray<Vector3>(poseCapacity, Allocator.Persistent);
                previousCenters = new NativeArray<Vector3>(poseCapacity, Allocator.Persistent);
                distances = new NativeArray<float>(poseCapacity, Allocator.Persistent);
                inverseRequired = new NativeArray<byte>(poseCapacity, Allocator.Persistent);
                moved = new NativeArray<byte>(poseCapacity, Allocator.Persistent); reordered = new NativeArray<byte>(poseCapacity, Allocator.Persistent);
                poseChanges = new NativeQueue<int>(Allocator.Persistent);
                // Reserve queue blocks before live frames. Draining visits only
                // changed slots; the main thread no longer scans all transforms.
                for (int i = 0; i < poseCapacity; i++) poseChanges.Enqueue(i);
                poseChanges.Clear();
                poseIds = new uint[poseCapacity]; liveIds = new uint[poseCapacity]; liveValues = new byte[poseCapacity];
            }
            // A visibility edit only touches its slot. The native source ID is stable.
            foreach (var source in liveChanges)
            {
                bool active = source.Live && source.R;
                int index = source.PoseIndex;
                if (!active && index >= 0)
                {
                    int last = poseSources.Count - 1; if (source.NeedsInverse) inversePoseCount--;
                    transforms.RemoveAtSwapBack(index);
                    if (index != last) { var swapped = poseSources[last]; poseSources[index] = swapped; swapped.PoseIndex = index; poses[index] = poses[last]; centers[index] = centers[last]; previousCenters[index] = previousCenters[last]; distances[index] = distances[last]; inverseRequired[index] = inverseRequired[last]; }
                    poseSources.RemoveAt(last); source.PoseIndex = -1; poseRemoves++;
                }
                else if (active && index < 0)
                {
                    index = poseSources.Count;
                    if (index == poseCapacity) throw new InvalidOperationException("fixed pose capacity exhausted");
                    source.PoseIndex = index; if (source.NeedsInverse) inversePoseCount++; poseSources.Add(source); transforms.Add(source.R.transform);
                    poses[index] = default; inverseRequired[index] = source.NeedsInverse ? (byte)1 : (byte)0; centers[index] = LocalCenter(source); distances[index] = source.Key.Distance; poseAdds++;
                }
            }
            transformsDirty = false;
        }
        static void Index(UnityEngine.Object obj, Source s)
        {
            if (!s.Indexed.Add(obj)) return;
            if (!sourceIndex.TryGetValue(obj, out var list)) sourceIndex.Add(obj, list = new List<Source>());
            list.Add(s);
        }
        static UnityEngine.Object rescanObj; static string rescanWriter, rescanWhy = ""; static long rescanCount;
        static void MarkRescan(string site)
        {
            rescanCount++;
            if (!fullRescan)
            {
                string name; try { name = rescanObj ? rescanObj.GetType().Name + ":" + rescanObj.name : (ReferenceEquals(rescanObj, null) ? "null" : "destroyed"); } catch { name = "?"; }
                rescanWhy = site + " obj=" + name + " writer=" + rescanWriter;
                if (rescanCount <= 20) Log("rescan " + rescanWhy);
            }
            fullRescan = true;
        }
        static void Queue(Source s, bool tintOnly = false, bool keyOnly = false, bool stateOnly = false) { if (!s.Queued) { s.TintOnly = tintOnly; s.KeyOnly = keyOnly; s.StateOnly = stateOnly; s.Queued = true; changed.Add(s); } else { if (!tintOnly) s.TintOnly = false; if (!keyOnly) s.KeyOnly = false; if (!stateOnly) s.StateOnly = false; } }
        internal static void NotifyFull(UnityEngine.Object obj, int kind, string writer)
        {
            rescanObj = obj; rescanWriter = writer;
            if (!rendering && !preparing) return;
            if (ReferenceEquals(obj, null)) return;
            if (!obj) { if (sourceIndex.TryGetValue(obj, out var deadList)) { foreach (var ds in deadList) Queue(ds); return; } if (canvasIndex.Contains(obj)) MarkRescan("Render:dead-canvas"); return; }
            if (writer != null) { var key = (obj.GetType(), writer); signalWriters.TryGetValue(key, out long n); signalWriters[key] = n + 1; }
            NotifyCanvasWrite(obj, kind, writer);
            if (obj is Camera) { explicitDistancesDirty = true; return; }
            if (obj is Renderer wake && dormant.Contains(wake))
            {
                if (wake && wake.enabled && wake.gameObject.activeInHierarchy && !wake.forceRenderingOff) { pendingPromotions.Add(wake); }
                return;
            }
            if (obj is GameObject activeGo && writer == "SetActive")
            {
                activeGo.GetComponentsInChildren(true, rendererScratch);
                foreach (var r in rendererScratch)
                    if (dormant.Contains(r) && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff) { pendingPromotions.Add(r); }
                activeGo.GetComponentsInChildren(true, canvasScratch);
                foreach (var c in canvasScratch)
                    if (dormantCanvases.Contains(c) && c.isActiveAndEnabled) pendingCanvasPromotions.Add(c);
            }
            if (!obj) { if (sourceIndex.ContainsKey(obj) || canvasIndex.Contains(obj)) MarkRescan("Render:290"); return; }
            if (kind == DirectSequenceChanges.Hierarchy)
            {
                if (canvasIndex.Contains(obj)) { MarkRescan("Render:293-canvas"); return; }
                if (sourceIndex.TryGetValue(obj, out var moved0)) { foreach (var ms in moved0) { ms.HierarchyDirty = true; ms.GroupsDirty = true; Queue(ms); DirtyOrder(ms, "hierarchy"); } transformsDirty = true; hierarchyIncremental++; }
                var go = obj as GameObject; if (!go && obj is Component component) go = component.gameObject;
                int mapMask = preparing ? prepareOriginal : lease.Original;
                if (go) foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if ((mapMask & 1 << r.gameObject.layer) != 0 && r.gameObject.scene.IsValid() && !rendererSources.ContainsKey(r) && !dormant.Contains(r))
                    { dormant.Add(r); if (r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff) pendingPromotions.Add(r); hierarchyIncremental++; }
                if (go) foreach (var c in go.GetComponentsInChildren<Canvas>(true))
                    if (Relevant(c, camera) && !dormantCanvases.Contains(c) && !canvasOwned.Contains(c.transform))
                    { dormantCanvases.Add(c); if (c.isActiveAndEnabled) pendingCanvasPromotions.Add(c); hierarchyIncremental++; }
                return;
            }
            if (obj is SortingGroup && sourceIndex.TryGetValue(obj, out var grouped)) { foreach (var s in grouped) { s.GroupsDirty = true; Queue(s); DirtyOrder(s, "group-signal"); } return; }
            if (obj is Transform) return; // TransformAccessArray job observes parents and uninstrumented tweens.
            if (obj is Mesh mesh) { meshVersions.TryGetValue(mesh, out int v); meshVersions[mesh] = v + 1; }
            if (obj is Shader) { globalsDirty = true; return; }
            if (obj is MeshFilter filter) { var renderer = filter.GetComponent<Renderer>(); if (renderer && rendererSources.TryGetValue(renderer, out var owner)) Queue(owner); }
            if (obj is UnityEngine.Texture texture && texturePointers.ContainsKey(texture)) { globalsDirty = true; texturePointers.Remove(texture); }
            if (sourceIndex.TryGetValue(obj, out var list)) foreach (var s in list) Queue(s, obj is SpriteRenderer && writer == "set_color",
                obj is Renderer && (writer == "set_sortingOrder" || writer == "set_sortingLayerID" || writer == "set_sortingLayerName"),
                obj is Renderer && writer == "set_enabled" || obj is GameObject && writer == "SetActive");
        }
        internal static void NotifyFullGlobal() { if (rendering || preparing) globalsDirty = true; }
        internal static void BeforeFullWrite(UnityEngine.Object obj, int kind)
        {
            rescanObj = obj; rescanWriter = "before:" + kind;
            if (rendering && kind == DirectSequenceChanges.Hierarchy && !ReferenceEquals(obj, null)
                && canvasIndex.Contains(obj)) MarkRescan("Render:312-canvas");
        }
        static void RetirePasses(Source source)
        {
            if (AffectsNamedGrab(source)) namedGrabOwnershipDirty = true;
            if (source.Passes == null) return;
            foreach (var old in source.Passes)
            {
                if (old.NativeId != 0) { nativeCount--; var retire = new N.DrawReplacement { Previous = (uint)old.NativeId }; N.Require(N.Op.ReplaceDraw, fullToken, &retire); }
                else fallbackCount--;
            }
        }
        static void Refresh(Source s, bool initial)
        {
            if (!initial) { collectSignals++; if (s.TintOnly) collectTintSignals++; else collectFullSignals++; }
            if (!s.R) throw new InvalidOperationException("owned renderer destroyed");
            if (!initial && s.KeyOnly && s.Live && !s.Mask && s.Passes != null)
            {
                s.KeyOnly = false; UpdateKey(s); return;
            }
            s.KeyOnly = false;
            if (!initial && s.StateOnly && s.Passes != null && !s.HierarchyDirty && !s.GroupsDirty)
            {
                s.StateOnly = false; collectStateSignals++;
                bool visible = s.R.enabled && s.R.gameObject.activeInHierarchy && !s.R.forceRenderingOff && (CanvasOwns(s.R.transform) || (removedMask & 1 << s.R.gameObject.layer) != 0);
                if (visible == s.Live) return;
                s.Live = visible; LiveChanged(s);
                // Hidden sources still receive shape/material notifications. On
                // reactivation refresh those values before exposing their draws.
                if (!visible) return;
            }
            s.StateOnly = false;
            if (!initial && s.TintOnly && s.Live && s.Sprite && s.Passes != null)
            {
                s.TintOnly = false; Color tint = s.Sprite.color;
                foreach (var b in s.Passes)
                {
                    if (b.NativeId == 0) continue;
                    fixed (byte* data = b.Bytes)
                    {
                        ((Vector4*)data)[0] = b.HasRendererColor ? Color.white : tint;
                        foreach (int offset in b.TintOffsets) *(Vector4*)(data + offset) = tint;
                    }
                    if (!b.ConstantsQueued) { b.ConstantsQueued = true; changedConstants.Add(b); }
                }
                return;
            }
            s.TintOnly = false;
            if (s.HierarchyDirty)
            {
                foreach (var indexed in s.Indexed) if (sourceIndex.TryGetValue(indexed, out var prior)) prior.Remove(s);
                s.Indexed.Clear(); Index(s.R, s);
                for (Transform t = s.R.transform; t; t = t.parent) { Index(t, s); Index(t.gameObject, s); var group = t.GetComponent<SortingGroup>(); if (group) Index(group, s); }
                s.HierarchyDirty = false;
            }
            if (initial || s.GroupsDirty) { RefreshGroups(s); s.GroupsDirty = false; }
            if (s.Mask)
            {
                var maskOrder = (s.Mask.isCustomRangeActive, s.Mask.frontSortingLayerID, s.Mask.frontSortingOrder, s.Mask.backSortingLayerID, s.Mask.backSortingOrder);
                if (!maskOrder.Equals(s.MaskOrder)) { s.MaskOrder = maskOrder; DirtyOrder(s, "mask-range"); }
            }
            bool live = s.R.enabled && s.R.gameObject.activeInHierarchy && !s.R.forceRenderingOff && (CanvasOwns(s.R.transform) || (removedMask & 1 << s.R.gameObject.layer) != 0);
            if (s.Live != live) { s.Live = live; LiveChanged(s); }
            s.R.GetSharedMaterials(s.Materials); s.R.GetPropertyBlock(s.RendererBlock);
            if (!live && !s.Prebuild) { UpdateKey(s); return; }
            if (centers.IsCreated && s.PoseIndex >= 0) centers[s.PoseIndex] = LocalCenter(s);
            foreach (var mat in s.Materials) if (mat) Index(mat, s);
            if (s.Filter && s.Filter.sharedMesh) Index(s.Filter.sharedMesh, s);
            if (s.Sprite && !s.Sprite.sprite)
            {
                RetirePasses(s);
                bool hadDraws = s.Passes != null && s.Passes.Length != 0;
                s.Passes = Array.Empty<Bound>(); s.MaterialCount = s.Materials.Count;
                if (hadDraws) DirtyOrder(s, "empty-draw"); else avoidedEmptyReplans++;
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
                        try { if (b.NativeId != 0) Fill(b); else RefreshExplicitPasses(b); } catch { structure = true; break; }
                        if (!s.Mask && b.Entry.Key.CompareTo(FullKey(s.R, b.Material, b.Slot)) != 0) DirtyOrder(s, "pass-key");
                    }
                    if (!structure) { if (s.Mask) avoidedMaskReplans++; UpdateKey(s); return; }
                }
                if (!initial) { RetirePasses(s); DirtyOrder(s, "structure"); }
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
            // Keep entry/node identity when a sprite/geometry replacement has the
            // same native pass shape. The general sparse ordering path can then
            // replace IDs without rebuilding any Unity command buffer.
            var previousPasses = s.Passes;
            s.Passes = passes.ToArray(); s.MaterialCount = s.Materials.Count; s.MaskSprite = s.Mask ? s.Mask.sprite : null;
            if (!s.Mask && previousPasses != null && previousPasses.Length == s.Passes.Length)
                for (int i = 0; i < s.Passes.Length; i++)
                    if (previousPasses[i].NativeId != 0 && s.Passes[i].NativeId != 0 && previousPasses[i].Slot == s.Passes[i].Slot && previousPasses[i].Pass.Index == s.Passes[i].Pass.Index)
                    { s.Passes[i].Entry = previousPasses[i].Entry; s.Passes[i].Entry.Draw = s.Passes[i]; }
            foreach (var b in s.Passes) if (b.NativeId != 0) nativeCount++;
            DirtyOrder(s, "passes"); UpdateKey(s);
        }
        static Bound Fallback(Source s, Material m, int slot, string reason)
        {
            namedGrabOwnershipDirty = true;
            // Explicit DrawRenderer bypasses Unity's LOD selection. Such sources
            // need their original camera until a verified LOD path exists.
            if (s.R.GetComponentInParent<LODGroup>()) throw new InvalidOperationException("explicit LODGroup requires original camera");
            explicitSources.Add(s);
            fallbackReasons.TryGetValue(reason, out int n); fallbackReasons[reason] = n + 1; fallbackCount++;
            var b = new Bound { Source = s, Slot = slot, Material = m, UnityShader = m.shader };
            b.Entry = new Entry { Source = s, Fallback = m, Slot = slot, ExplicitPasses = ExplicitPasses(m) }; return b;
        }
        static bool StructureMatches(Source s)
        {
            if (s.Mask) return s.MaskSprite == s.Mask.sprite;
            if (s.MaterialCount != s.Materials.Count || s.Sprite && s.Sprite.drawMode != SpriteDrawMode.Simple) return false;
            if (s.Passes.Length == 0) return false;
            foreach (var b in s.Passes)
            {
                if (b.Slot >= s.Materials.Count || !ReferenceEquals(b.Material, s.Materials[b.Slot])) return false;
                if (b.NativeId == 0) { if (b.Material.shader != b.UnityShader) return false; continue; }
                if (s.R.isPartOfStaticBatch || s.R is MeshRenderer mr && mr.additionalVertexStreams) return false;
                if (b.Material.shader != b.UnityShader || !ReferenceEquals(S.Select(b.Shader, b.Pass, b.Material), b.Variant)) return false;
                if (GeometryFor(s, b.Variant, b.Slot) != b.Geometry) return false;
            }
            return true;
        }
        static Bound Bind(Source s, Material m, S.ShaderData shader, S.Pass pass, int slot, int maskKind)
        {
            if (pass.Grab && !string.IsNullOrEmpty(pass.GrabName)) namedGrabOwnershipDirty = true;
            var v = s.Mask ? pass.Variants[0] : S.Select(shader, pass, m);
            var b = new Bound { Source = s, Material = m, UnityShader = m ? m.shader : null, Shader = shader, Pass = pass, Slot = slot, MaskKind = maskKind, Variant = v, Stages = new[] { v.Vs, v.Ps } };
            b.Entry = new Entry { Source = s, Draw = b, Slot = slot };
            var buffers = new List<N.Buffer>(); var patches = new List<N.Patch>(); int offset = 16;
            var writes = new List<(S.Variable Variable, int Offset)>(); var tintOffsets = new List<int>();
            foreach (var stage in b.Stages)
            {
                uint kind = ReferenceEquals(stage, v.Vs) ? 0u : 1u;
                foreach (var cb in stage.Buffers)
                {
                    buffers.Add(new N.Buffer { Stage = kind, Slot = (uint)cb.Slot, Bytes = (uint)cb.Size, Offset = (uint)offset });
                    foreach (var x in cb.Variables)
                    {
                        int dynamic = Dynamic(x.Name); if ((dynamic == 2 || dynamic == 7) && !s.NeedsInverse) { if (s.PoseIndex >= 0) inversePoseCount++; s.NeedsInverse = true; if (s.PoseIndex >= 0 && inverseRequired.IsCreated) { inverseRequired[s.PoseIndex] = 1; poses[s.PoseIndex] = default; } }
                        if (dynamic != 0) patches.Add(new N.Patch { Offset = (uint)(offset + x.Offset), Kind = (uint)dynamic });
                        else writes.Add((x, offset + x.Offset));
                        if (x.Name == "_RendererColor") tintOffsets.Add(offset + x.Offset);
                        if (x.Name == "_RendererColor") b.HasRendererColor = true;
                        if (shader.Name == "Sprites/Default" && s.Sprite && (x.Name == "_RendererColor" || x.Name == "_Flip")) patches.Add(new N.Patch { Offset = (uint)(offset + x.Offset), Kind = x.Name == "_RendererColor" ? 16u : 17u });
                        if (x.Name == "_Flip") b.HasFlip = true;
                    }
                    offset += cb.Size;
                }
            }
            b.Buffers = buffers.ToArray(); b.Patches = patches.ToArray(); b.Bytes = new byte[offset]; b.Previous = new byte[offset];
            b.MaterialWrites = writes.ToArray(); b.TintOffsets = tintOffsets.ToArray();
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
            long fillStart = sampleCollect ? Stopwatch.GetTimestamp() : 0; fillCalls++;
            var s = b.Source; ReadBlock(s, b.Slot); var mpb = Block(s); long hash = 17;
            for (int i = 0; i < 29; i++)
            {
                var state = b.Pass.States[i]; if (b.StatesInitialized && state.Name.Length == 0) continue;
                float value = state.Name.Length == 0 ? state.Value : Float(b, state.Id, state.Name, state.Value);
                b.States[i] = i == 11 ? checked((int)(value * 1000000f)) : checked((int)value);
            }
            // Mask overrides modify these slots; restore their original values
            // on the next refresh even when the serialized states are static.
            b.StatesInitialized = b.MaskKind == 0 && !(s.Sprite && s.Sprite.maskInteraction != SpriteMaskInteraction.None);
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
            long hStates = hash;
            int texIndex = 0;
            foreach (var stage in b.Stages)
            {
                foreach (var t in stage.Textures)
                {
                    UnityEngine.Texture tex = ResolveTexture(b, t.Name, t.Id); bool grab = t.Name == "_GrabTexture" || t.Name == b.Pass.GrabName;
                    if (t.Name.StartsWith("_BLENDMODES_", StringComparison.Ordinal) && !tex && !grab) throw new InvalidOperationException("unbound blend texture " + t.Name);
                    if (!tex && !grab) tex = DefaultTexture(b, t.Name);
                    if (tex && !(tex is Texture2D || tex is RenderTexture)) throw new InvalidOperationException("non-2D texture " + t.Name);
                    if (tex && tex.mipMapBias != 0) throw new InvalidOperationException("unverified nonzero sampler mip bias: " + t.Name);
                    b.TextureObjects[texIndex] = tex;
                    var nt = new N.Texture { Resource = tex ? TexturePointer(tex) : IntPtr.Zero, Stage = ReferenceEquals(stage, b.Variant.Vs) ? 0 : 1,
                        Slot = t.Slot, Sampler = t.Sampler, Filter = tex ? (int)tex.filterMode : 1, WrapU = tex ? (int)tex.wrapModeU : 1, WrapV = tex ? (int)tex.wrapModeV : 1,
                        Aniso = EffectiveAniso(tex ? tex.anisoLevel : 1), Srgb = grab ? -(int)GrabId(t.Name == "_GrabTexture" ? "" : t.Name) : QualitySettings.activeColorSpace == ColorSpace.Linear && tex && tex.isDataSRGB ? 1 : 0 };
                    b.Textures[texIndex++] = nt;
                    hash = unchecked(hash * 31 + nt.Resource.ToInt64()); hash = unchecked(hash * 31 + nt.Filter + nt.WrapU * 7 + nt.WrapV * 31 + nt.Aniso * 113 + nt.Srgb * 991);
                }
            }
            fixed (byte* data = b.Bytes)
            {
                Color c = s.Sprite && !b.HasRendererColor ? s.Sprite.color : Color.white;
                ((Vector4*)data)[0] = c;
                foreach (var stage in b.Stages) foreach (var cb in stage.Buffers)
                {
                    if (mpb.HasConstantBuffer(cb.Id)) throw new InvalidOperationException("external MPB constant buffer requires an explicit buffer lease: " + cb.Name);
                }
                foreach (var write in b.MaterialWrites)
                {
                        var x = write.Variable; void* dst = data + write.Offset;
                        if (x.Matrix != 0) { if (x.Rows != 4 || x.Cols != 4) throw new InvalidOperationException("unverified matrix shape"); *(Matrix4x4*)dst = mpb.HasProperty(x.Id) ? mpb.GetMatrix(x.Id) : b.Material && b.Material.HasProperty(x.Id) ? b.Material.GetMatrix(x.Id) : Shader.GetGlobalMatrix(x.Id); continue; }
                        if (x.Type == 1) { if (x.Cols != 1) throw new InvalidOperationException("unverified integer vector"); *(int*)dst = Integer(b, x); continue; }
                        Vector4 value = Value(b, x);
                        for (int k = 0; k < x.Cols; k++) ((float*)dst)[k] = value[k];
                }
            }
            long hTex = hash;
            var localBounds = s.R.localBounds;
            for (int i = 0; i < 3; i++) { hash = unchecked(hash * 31 + BitConverter.SingleToInt32Bits(localBounds.center[i])); hash = unchecked(hash * 31 + BitConverter.SingleToInt32Bits(localBounds.extents[i])); }
            long hBounds = hash;
            hash = unchecked(hash * 31 + b.Geometry);
            if (b.NativeId != 0 && hash != b.ConfigHash)
            {
                if (hStates != b.HashStates) rebindStates++;
                else if (hTex != b.HashTex) rebindTextures++;
                else if (hBounds != b.HashBounds) rebindBounds++;
                else if (b.Geometry != b.HashGeometry) rebindGeometry++;
            }
            b.HashStates = hStates; b.HashTex = hTex; b.HashBounds = hBounds; b.HashGeometry = b.Geometry;
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
                        Constants = bytes, ConstantBytes = (uint)b.Bytes.Length, Pose = (uint)s.Index, Grab = b.Pass.Grab ? GrabId(b.Pass.GrabName) : 0u, Sprite = (s.Sprite || s.Mask ? 1u : 0u) | (Batchable(b) ? 2u : 0u) | (ScreenGrab(b) ? 4u : 0u) };
                    for (int i = 0; i < 3; i++) { draw.Center[i] = localBounds.center[i]; draw.Extent[i] = localBounds.extents[i]; }
                    if (b.NativeId != 0) { var replacement = new N.DrawReplacement { Previous = (uint)b.NativeId, Draw = draw }; b.NativeId = N.Require(N.Op.ReplaceDraw, fullToken, &replacement); }
                    else b.NativeId = N.Require(N.Op.Draw, fullToken, &draw);
                    DirtyOrder(s, "draw-rebind"); drawRebinds++;
                }
                Array.Copy(b.Bytes, b.Previous, b.Bytes.Length); // Config already owns its initial constants.
            }
            if (!b.ConstantsQueued) { b.ConstantsQueued = true; changedConstants.Add(b); }
            if (sampleCollect) fillTicks += Stopwatch.GetTimestamp() - fillStart;
        }
        // Original SpritesDefault Grab programs project POSITION directly and
        // sample screen XY/W. Native additionally checks every indexed vertex.
        // Unknown/deformed shaders and pixel snapping keep full snapshots.
        // Hidden/BlendModes/* 의 Grab 셰이더는 자기 화면 위치의 grab 텍셀만 읽는다 → 그 draw 사각형만 복사해도 같다(실제 정점으로 다시 검사).
        // 다른 Grab 셰이더(왜곡 등)는 전체 복사. HELLO 회귀 확인 중(2026-11-16 Claude).
        static bool ScreenGrab(Bound b) => b != null && b.UnityShader && b.UnityShader.name.StartsWith("Hidden/BlendModes/", StringComparison.Ordinal);
        static bool Batchable(Bound b)
        {
            // These original vertex programs only transform POSITION, then use
            // UV/color. Object-space deformations/Grab/masks are hard boundaries.
            return !localPrecision && b.MaskKind == 0 && !b.Pass.Grab && (b.Shader.Name == "Sprites/Tile" || b.Shader.Name == "ADOFAI/FloorMesh" || b.Shader.Name == "Sprites/Default" && b.Source.Sprite && Array.IndexOf(b.Variant.Keywords, "ETC1_EXTERNAL_ALPHA") < 0);
        }
        static int EffectiveAniso(int value)
        {
            if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable || value == 0) return 1;
            return QualitySettings.anisotropicFiltering == AnisotropicFiltering.ForceEnable ? Mathf.Clamp(value, 9, 16) : Mathf.Clamp(value, 1, 16);
        }
        static long StateHash(int[] values) { long h = 19; foreach (int x in values) h = unchecked(h * 397 ^ x); return h; }
        static float Float(Bound b, int id, string name, float fallback = 0)
        {
            var block = Block(b.Source);
            if (block.HasProperty(id)) return block.GetFloat(id);
            if (b.Material && b.Material.HasProperty(id)) return b.Material.GetFloat(id);
            if (b.Shader.PropertiesById.TryGetValue(id, out var p)) return p.Default.x;
            return name.StartsWith("unity_", StringComparison.Ordinal) ? fallback : Shader.GetGlobalFloat(id);
        }
        static int Integer(Bound b, S.Variable v)
        {
            var block = Block(b.Source);
            if (block.HasProperty(v.Id)) return block.HasInteger(v.Id) ? block.GetInteger(v.Id) : (int)block.GetFloat(v.Id);
            if (b.Material && b.Material.HasProperty(v.Id)) return b.Material.HasInteger(v.Id) ? b.Material.GetInteger(v.Id) : (int)b.Material.GetFloat(v.Id);
            if (b.Shader.PropertiesById.TryGetValue(v.Id, out var p)) return (int)p.Default.x;
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
            if (b.Shader.PropertiesById.TryGetValue(v.Id, out var prop))
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
            // Supplement the external Unity buffer with a CPU batch stream; the
            // original geometry/format/baseVertex path remains the single-draw oracle.
            if (mesh.isReadable && list.Exists(a => a.Semantic == 3) && list.Exists(a => a.Semantic == 4 && a.Index == 0))
            {
                var pos = mesh.vertices; var color = mesh.colors; var uv = mesh.uv; var uv1 = mesh.uv2;
                if (pos.Length == color.Length && uv.Length == pos.Length)
                {
                    var vertices = new float[pos.Length * 11]; var rawIndices = mesh.GetIndices(sub); var indices = new uint[rawIndices.Length];
                    for (int i = 0; i < pos.Length; i++) { int o = i * 11; vertices[o] = pos[i].x; vertices[o+1] = pos[i].y; vertices[o+2] = pos[i].z; for (int k = 0; k < 4; k++) vertices[o+3+k] = color[i][k]; vertices[o+7] = uv[i].x; vertices[o+8] = uv[i].y; if (uv1.Length == pos.Length) { vertices[o+9] = uv1[i].x; vertices[o+10] = uv1[i].y; } }
                    for (int i = 0; i < indices.Length; i++) indices[i] = (uint)rawIndices[i];
                    fixed (float* vb = vertices) fixed (uint* ib = indices) { var cpu = new N.CpuGeometry { Geometry = (uint)existing, Vertices = vb, Count = (uint)pos.Length, Indices = ib, IndexCount = (uint)indices.Length }; N.Require(N.Op.GeometryCpu, fullToken, &cpu); }
                }
            }
            geometryCache.Add(meshKey, existing); return existing;
        }
        static string SemanticName(uint x) => x == 0 ? "POSITION" : x == 1 ? "NORMAL" : x == 2 ? "TANGENT" : x == 3 ? "COLOR" : "TEXCOORD";
        static uint Dxgi(VertexAttributeFormat f, int n)
        {
            if (n < 1 || n > 4) throw new InvalidOperationException("invalid vertex dimension " + n);
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
            if (!s.Mask && s.Passes != null && s.Passes.Length != 0 && s.Key.CompareTo(key) != 0)
            {
                var old = s.Key;
                DirtyOrder(s, old.Layer == key.Layer && old.Order == key.Order && old.Queue == key.Queue && old.Shader == key.Shader && old.Material == key.Material && old.Renderer == key.Renderer && old.Slot == key.Slot ? "key-distance" : "key-bucket"); orderKeyChanges++;
            }
            s.Key = key;
        }
        static void UpdateDistanceKey(Source s)
        {
            if (s.Mask || s.Groups.Length != 0) { UpdateKey(s); return; }
            var k = s.Key; float d = distances[s.PoseIndex]; if (k.Distance == d) return;
            s.Key = new SceneRenderOrder.Key(k.Layer, k.Order, k.Queue, d, k.Material, k.Renderer, k.Slot, k.Shader);
            if (s.Passes != null && s.Passes.Length != 0) { DirtyOrder(s, "key-distance"); orderKeyChanges++; }
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
            if (recordingExplicit) { Timeline(cb, 7); recordingExplicit = false; }
            if (listCount == start && segmentCount != 0) return;
            if (segmentCount >= 65535) throw new InvalidOperationException("M4c segment limit");
            segments[segmentCount * 2] = (uint)start; segments[segmentCount * 2 + 1] = (uint)(listCount - start);
            cb.IssuePluginEvent(new IntPtr(N.Call(N.Op.Event, 0)), (fullToken << 16) | segmentCount);
            segmentCount++; start = listCount;
        }
        static long distanceOrderSkips, distanceOrderVisibility, localOrderPatches, localOrderEntries;
        static readonly IComparer<Entry> entryComparer = Comparer<Entry>.Create(CompareFull);
        static bool AffectsNamedGrab(Source source)
        {
            if (source.Passes != null) foreach (var bound in source.Passes)
                if (bound.NativeId == 0 || bound.Pass.Grab && !string.IsNullOrEmpty(bound.Pass.GrabName)) return true;
            return false;
        }
        static bool TryNativeOrderEdits()
        {
            if (!orderCommandsReady || namedGrabOwnershipDirty || explicitVisibilityChanges != distanceOrderVisibility || orderChanges.Count == 0) return false;
            foreach (var u in canvasUnits) if (u.OrderDirty || u.Entry != null && u.Entry.RecordedIsolated != u.Isolated) return false;
            foreach (var source in orderChanges)
            {
                if (!source.R || source.Mask || source.Groups.Length != 0 || source.Passes == null || source.Passes.Length == 0 || source.OrderedPasses == null || source.Passes.Length != source.OrderedPasses.Length) return false;
                for (int i = 0; i < source.Passes.Length; i++)
                {
                    var bound = source.Passes[i]; var entry = bound.Entry;
                    if (bound.NativeId == 0 || !ReferenceEquals(entry, source.OrderedPasses[i].Entry) || entry.Node == null || !entry.Node.Linked
                        || entry.OrderIndex < 0 || entry.Sub != 0 || entry.RecordedPhase != (bound.Material.renderQueue <= 2500 ? 0 : 1)) return false;
                }
            }
            // PatchDistanceCrossing only changes distance. Full material/layer
            // keys are read after unlinking all changed entries below instead.
            return PatchNativeKeys();
        }
        static bool PatchNativeKeys()
        {
            int first = ordered.Count, last = -1;
            foreach (var source in orderChanges) foreach (var bound in source.Passes)
            { var entry = bound.Entry; first = Math.Min(first, entry.OrderIndex); last = Math.Max(last, entry.OrderIndex); entry.PreviousDistanceKey = entry.Key; orderTree.Remove(entry.Node); }
            foreach (var source in orderChanges) foreach (var bound in source.Passes)
            { var entry = bound.Entry; entry.Key = FullKey(source.R, bound.Material, bound.Slot); entry.Layer = entry.Key.Layer; entry.Order = entry.Key.Order; orderTree.Add(entry.Node); }
            foreach (var source in orderChanges) foreach (var bound in source.Passes)
            { int rank = orderTree.Rank(bound.Entry); first = Math.Min(first, rank); last = Math.Max(last, rank); }
            if (!PatchNativeInterval(first, last))
            {
                foreach (var source in orderChanges) foreach (var bound in source.Passes) orderTree.Remove(bound.Entry.Node);
                foreach (var source in orderChanges) foreach (var bound in source.Passes)
                { var entry = bound.Entry; entry.Key = entry.PreviousDistanceKey; entry.Layer = entry.Key.Layer; entry.Order = entry.Key.Order; orderTree.Add(entry.Node); }
                return false;
            }
            foreach (var source in orderChanges) source.OrderedPasses = source.Passes;
            FinishNativePatch(last - first + 1); return true;
        }
        static bool PatchNativeInterval(int first, int last)
        {
            int slot = ordered[first].NativeListIndex, segment = ordered[first].NativeSegment;
            if (slot < 0) return false;
            for (int i = first; i <= last; i++)
            {
                var entry = ordered[i];
                if (entry.Draw == null || entry.Canvas != null || entry.Sub != 0 || entry.GroupIds.Length != 0
                    || entry.NativeSegment != segment || entry.NativeListIndex != slot + i - first) return false;
            }
            ordered.Sort(first, last - first + 1, entryComparer);
            for (int i = first; i <= last; i++) { var entry = ordered[i]; entry.OrderIndex = i; entry.NativeListIndex = slot + i - first; drawList[entry.NativeListIndex] = (uint)entry.Draw.NativeId; }
            return true;
        }
        static void FinishNativePatch(int entries)
        {
            foreach (var source in orderChanges) source.OrderQueued = false;
            orderChanges.Clear(); orderDirty = false; sendList = true; orderBuilds++; localOrderPatches++; localOrderEntries += entries;
        }
        static bool PatchDistanceCrossing()
        {
            int first = ordered.Count, last = -1;
            foreach (var source in orderChanges) foreach (var bound in source.Passes)
            { var entry = bound.Entry; first = Math.Min(first, entry.OrderIndex); last = Math.Max(last, entry.OrderIndex); orderTree.Remove(entry.Node); }
            foreach (var source in orderChanges) foreach (var bound in source.Passes)
            {
                var entry = bound.Entry; var k = entry.Key;
                entry.Key = new SceneRenderOrder.Key(k.Layer, k.Order, k.Queue, source.Key.Distance, k.Material, k.Renderer, k.Slot, k.Shader);
                orderTree.Add(entry.Node);
            }
            foreach (var source in orderChanges) foreach (var bound in source.Passes)
            { int rank = orderTree.Rank(bound.Entry); first = Math.Min(first, rank); last = Math.Max(last, rank); }
            int slot = ordered[first].NativeListIndex, segment = ordered[first].NativeSegment;
            bool patch = slot >= 0;
            for (int i = first; patch && i <= last; i++)
            {
                var entry = ordered[i];
                patch = entry.Draw != null && entry.Canvas == null && entry.Sub == 0 && entry.GroupIds.Length == 0
                    && entry.NativeSegment == segment && entry.NativeListIndex == slot + i - first;
            }
            if (!patch)
            {
                // General rebuilding searches by the OLD keys. Restore both the
                // BST and keys transactionally before leaving the fast path.
                foreach (var source in orderChanges) foreach (var bound in source.Passes) orderTree.Remove(bound.Entry.Node);
                foreach (var source in orderChanges) foreach (var bound in source.Passes) { bound.Entry.Key = bound.Entry.PreviousDistanceKey; orderTree.Add(bound.Entry.Node); }
                return false;
            }
            ordered.Sort(first, last - first + 1, entryComparer);
            for (int i = first; i <= last; i++) { var entry = ordered[i]; entry.OrderIndex = i; entry.NativeListIndex = slot + i - first; drawList[entry.NativeListIndex] = (uint)entry.Draw.NativeId; }
            foreach (var source in orderChanges) source.OrderQueued = false;
            orderChanges.Clear(); orderDirty = false; sendList = true; orderBuilds++; localOrderPatches++; localOrderEntries += last - first + 1;
            return true;
        }
        static bool TryDistanceOrder()
        {
            if (!orderCommandsReady || namedGrabOwnershipDirty || explicitVisibilityChanges != distanceOrderVisibility || orderChanges.Count == 0) return false;
            foreach (var u in canvasUnits) if (u.OrderDirty || u.Entry != null && u.Entry.RecordedIsolated != u.Isolated) return false;
            foreach (var s in orderChanges)
            {
                if (!s.DistanceOnly || s.Mask || s.Groups.Length != 0 || s.Passes == null || s.Passes != s.OrderedPasses) return false;
                foreach (var b in s.Passes)
                {
                    var e = b.Entry;
                    if (e.Node == null || !e.Node.Linked || e.OrderIndex < 0 || e.OrderIndex >= ordered.Count || !ReferenceEquals(ordered[e.OrderIndex], e)
                        || e.Draw == null && (e.RecordedLive != s.Live || e.RecordedVisible != s.ExplicitVisible)) return false;
                }
            }
            // Update every moved key before comparing neighbours. Common motion
            // must not appear as an intermediate crossing between two edits.
            foreach (var s in orderChanges) foreach (var b in s.Passes)
            {
                var e = b.Entry; var k = e.Key; e.PreviousDistanceKey = k;
                e.Key = new SceneRenderOrder.Key(k.Layer, k.Order, k.Queue, s.Key.Distance, k.Material, k.Renderer, k.Slot, k.Shader);
            }
            bool same = true;
            foreach (var s in orderChanges) foreach (var b in s.Passes) same &= orderTree.OrderedAt(ordered, b.Entry.OrderIndex);
            if (!same)
            {
                // Restore BEFORE tree removal: its search uses the old keys.
                foreach (var s in orderChanges) foreach (var b in s.Passes) b.Entry.Key = b.Entry.PreviousDistanceKey;
                return PatchDistanceCrossing();
            }
            foreach (var s in orderChanges) s.OrderQueued = false;
            orderChanges.Clear(); orderDirty = false; avoidedOrderReplans++; keyOnlyFrames++; distanceOrderSkips++; return true;
        }
        static void RebuildFullOrder()
        {
            if (TryDistanceOrder()) return;
            if (TryNativeOrderEdits()) return;
            // Unity explicit draws have their own named-Grab cache. Sharing the
            // same name with our native cache would take two different snapshots.
            if (namedGrabOwnershipDirty) foreach (var source in sources) if (source.Live && source.Passes != null) foreach (var bound in source.Passes)
                if (bound.NativeId == 0 && bound.Material && bound.Material.shader && S.Load().TryGetValue(bound.Material.shader.name, out var shader))
                    foreach (var pass in shader.Passes) if (pass.Grab && !string.IsNullOrEmpty(pass.GrabName) && grabNames.ContainsKey(pass.GrabName))
                        throw new InvalidOperationException("mixed Unity/native named Grab ownership: " + pass.GrabName);
            namedGrabOwnershipDirty = false;
            foreach (var s in orderChanges)
                if (s.OrderedPasses != null) foreach (var b in s.OrderedPasses) if (b.Entry.Node != null) orderTree.Remove(b.Entry.Node);
            foreach (var s in orderChanges)
            {
                s.OrderQueued = false;
                for (int i = 0; i < s.Groups.Length; i++) s.GroupKeys[i] = GroupKey(s, i);
                s.OrderedPasses = s.Passes;
                if (!s.R || s.Passes == null) continue;
                foreach (var b in s.Passes)
                {
                    var e = b.Entry;
                    e.GroupIds = s.GroupIds; e.GroupKeys = s.GroupKeys;
                    if (s.Mask)
                    {
                        if (s.Mask.isCustomRangeActive && (SortingLayer.GetLayerValueFromID(s.Mask.frontSortingLayerID) < SortingLayer.GetLayerValueFromID(s.Mask.backSortingLayerID)
                            || s.Mask.frontSortingLayerID == s.Mask.backSortingLayerID && s.Mask.frontSortingOrder <= s.Mask.backSortingOrder)) continue;
                        int layer = b.MaskKind == 1 ? int.MinValue : int.MaxValue, order = layer;
                        if (s.Mask.isCustomRangeActive) { layer = SortingLayer.GetLayerValueFromID(b.MaskKind == 1 ? s.Mask.backSortingLayerID : s.Mask.frontSortingLayerID); order = b.MaskKind == 1 ? s.Mask.backSortingOrder : s.Mask.frontSortingOrder; }
                        e.Layer = layer; e.Order = order; e.Sub = b.MaskKind == 1 ? -2 : -1;
                        e.Key = new SceneRenderOrder.Key(layer, order, int.MinValue, float.MaxValue, int.MinValue, s.R.GetInstanceID());
                    }
                    else { var key = FullKey(s.R, b.Material, b.Slot); e.Key = key; e.Layer = key.Layer; e.Order = key.Order; }
                    if (e.Node == null) e.Node = new DirectFullOrderTree<Entry>.Node(e, ++entrySerial);
                    orderTree.Add(e.Node); incrementalKeys++;
                }
            }
            orderChanges.Clear();
            AddCanvasEntries();
            bool same = orderCommandsReady && orderTree.Matches(ordered);
            if (same) foreach (var e in ordered)
                if (e.RecordedPhase != FullPhase(e) || e.Draw == null && e.Source != null && (e.RecordedLive != e.Source.Live || e.RecordedVisible != e.Source.ExplicitVisible)
                    || e.Canvas != null && e.RecordedIsolated != e.Canvas.Isolated) { same = false; break; }
            if (same) { distanceOrderVisibility = explicitVisibilityChanges; avoidedOrderReplans++; keyOnlyFrames++; orderDirty = false; return; }
            orderTree.CopyTo(ordered);
            for (int index = 0; index < ordered.Count; index++) { var e = ordered[index]; e.OrderIndex = index; e.RecordedPhase = FullPhase(e); e.RecordedLive = e.Source != null && e.Source.Live; e.RecordedVisible = e.Source != null && e.Source.ExplicitVisible; e.RecordedIsolated = e.Canvas != null && e.Canvas.Isolated; }
            before.Clear(); opaque.Clear(); listCount = segmentCount = 0; recordingExplicit = false;
            Timeline(opaque, 0); Timeline(opaque, 16); Timeline(before, 18);
            bool opaquePhase = ordered.Count != 0 && FullPhase(ordered[0]) == 0;
            var cb = opaquePhase ? opaque : before; int start = 0;
            BeginCanvasRuns();
            bool anyMasks = false; int maskDepth = 0;
            foreach (var e in ordered)
            {
                if (e.Canvas == null && e.Draw == null && !e.Source.Live) continue;
                if (opaquePhase && FullPhase(e) != 0) { RecordSegment(cb, ref start); cb = before; opaquePhase = false; }
                if (e.Sub == -2) maskDepth++; else if (e.Sub == -1) maskDepth--;
                if (maskDepth < 0 || maskDepth > 254) throw new InvalidOperationException("M4c invalid/saturated mask nesting");
                if (e.Canvas != null) { RecordCanvasEntry(cb, e, ref start); continue; }
                EndCanvasRun();
                if (e.Draw != null) { e.NativeListIndex = listCount; e.NativeSegment = segmentCount; if (listCount == drawList.Length) throw new InvalidOperationException("M4c draw list capacity"); drawList[listCount++] = (uint)e.Draw.NativeId; if (e.Sub != 0) anyMasks = true; }
                else RecordExplicit(cb, e, ref start);
            }
            EndCanvasRun();
            RecordSegment(cb, ref start);
            RetireCanvasRuns();
            if (maskDepth != 0) throw new InvalidOperationException("M4c unbalanced mask range");
            if (anyMasks && camera.targetTexture.depth < 24) throw new InvalidOperationException("M4c masks require a stencil-capable camera depth target");
            clearStencil = anyMasks;
            distanceOrderVisibility = explicitVisibilityChanges; sendList = true; orderDirty = false; orderCommandsReady = true; orderBuilds++;
        }
        static void FullPreCull()
        {
            long t = Stopwatch.GetTimestamp();
            long fc = 0, fp = 0, fs = 0, fa = 0, fk = 0;
            int gc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            try
            {
                if (fullRescan) { RecreateFull(); return; }
                long phase = Stopwatch.GetTimestamp(); sampleCollect = fullFrames % 64 == 0;
                long promoteStart = sampleCollect ? phase : 0; PromoteSources();
                if (sampleCollect) collectPromoteTicks += Stopwatch.GetTimestamp() - promoteStart;
                long subphase = Stopwatch.GetTimestamp();
                UpdateCanvasView(camera);
                if (canvasDirty) ReplanCanvases();
                collectCanvasTicks += Stopwatch.GetTimestamp() - subphase;
                if (camera.targetTexture != fullTarget || !fullTarget || fullTarget.width != fullWidth || fullTarget.height != fullHeight || fullTarget.antiAliasing != 1) throw new InvalidOperationException("camRT changed");
                subphase = Stopwatch.GetTimestamp();
                N.Stats stats = default; N.Require(N.Op.Stats, fullToken, &stats);
                collectStatsTicks += Stopwatch.GetTimestamp() - subphase;
                if (stats.Frames != 0 && firstReadyTicks == 0) firstReadyTicks = Stopwatch.GetTimestamp();
                if (stats.Errors != 0 || stats.Dropped != 0) throw new InvalidOperationException("native frame invalid errors=" + stats.Errors + " dropped=" + stats.Dropped + " hr=" + stats.LastHr.ToString("X8"));
                if (sortSignature != SortSignature()) { sortSignature = SortSignature(); DirtyAllOrder("camera-sort"); }
                if (globalsDirty) { foreach (var s in sources) Queue(s); globalsDirty = false; }
                long refreshStart = sampleCollect ? Stopwatch.GetTimestamp() : 0;
                collectVisited += changed.Count;
                long sfRefreshStart = Stopwatch.GetTimestamp(); refreshCount += changed.Count; foreach (var s in changed) { if (s.TintOnly) refreshTint++; }
                foreach (var s in changed) { s.Queued = false; if (!s.R) { if (s.Live) { s.Live = false; LiveChanged(s); } DirtyOrder(s, "destroy"); deadSources++; } else Refresh(s, false); } changed.Clear();
                refreshTicks += Stopwatch.GetTimestamp() - sfRefreshStart;
                if (sampleCollect) collectRefreshTicks += Stopwatch.GetTimestamp() - refreshStart;
                sampleCollect = false;
                if (fullRescan) { RecreateFull(); return; }
                subphase = Stopwatch.GetTimestamp();
                if (transformsDirty) BuildPoseTracking();
                collectTrackingTicks += Stopwatch.GetTimestamp() - subphase;
                fc = Stopwatch.GetTimestamp() - phase; collectTicks += fc; phase = Stopwatch.GetTimestamp();
                var sig = SortSignature();
                var axis = new Vector3(sig.x, sig.y, sig.z);
                var job = new Capture { Poses = poses, Centers = centers, PreviousCenters = previousCenters, Distances = distances, Axis = axis, AxisChanged = !haveCaptureAxis || !captureAxis.Equals(axis), Changed = moved, Reorder = reordered, InverseRequired = inverseRequired, Changes = poseChanges.AsParallelWriter() };
                long scheduled = Stopwatch.GetTimestamp();
                var captureHandle = job.ScheduleReadOnly(transforms, 64);
                poseScheduleTicks += Stopwatch.GetTimestamp() - scheduled;
                captureAxis = axis; haveCaptureAxis = true;
                bool first = !haveNativeFrame; frameWorldOnly = inversePoseCount == 0;
                var upload = (N.Pose*)NativeArrayUnsafeUtility.GetUnsafePtr(uploadPoses); var worlds = (Matrix4x4*)upload;
                poseCount = first ? poseSources.Count : 0;
                // These paths do not mutate capture arrays. Let the transform
                // workers overlap visibility/baked-particle work before joining.
                try { UpdateExplicitVisibility(); UpdateParticleMeshes(); }
                finally
                {
                    long waiting = Stopwatch.GetTimestamp();
                    captureHandle.Complete();
                    long waited = Stopwatch.GetTimestamp() - waiting; poseWaitTicks += waited; poseWaitMaxTicks = Math.Max(poseWaitMaxTicks, waited);
                }
                long results = Stopwatch.GetTimestamp();
                if (first) for (int i = 0; i < poseCount; i++) { poseIds[i] = (uint)poseSources[i].Index; if (frameWorldOnly) worlds[i] = poses[i].World; else upload[i] = poses[i]; }
                while (poseChanges.TryDequeue(out int index))
                {
                    if (moved[index] == 0 && reordered[index] == 0) continue;
                    poseChangedSlots++; var source = poseSources[index];
                    if (moved[index] == 2 || !source.R) { if (source.Live) { source.Live = false; LiveChanged(source); DirtyOrder(source, "invalid-pose"); } continue; }
                    if (!first && moved[index] != 0) { poseIds[poseCount] = (uint)source.Index; if (frameWorldOnly) worlds[poseCount] = poses[index].World; else upload[poseCount] = poses[index]; poseCount++; }
                    if (reordered[index] != 0)
                    {
                        for (int i = 0; i < source.Groups.Length; i++) if (source.GroupKeys[i].CompareTo(GroupKey(source, i)) != 0) { DirtyOrder(source, "group-distance"); break; }
                        UpdateDistanceKey(source);
                    }
                }
                if (transformsDirty) BuildPoseTracking();
                poseResultsTicks += Stopwatch.GetTimestamp() - results;
                fp = Stopwatch.GetTimestamp() - phase; poseTicks += fp; phase = Stopwatch.GetTimestamp();
                if (orderDirty) { long rp = Stopwatch.GetTimestamp(); RebuildFullOrder(); replanMaxTicks = Math.Max(replanMaxTicks, Stopwatch.GetTimestamp() - rp); }
                fs = Stopwatch.GetTimestamp() - phase; sortTicks += fs;
                long canvas = Stopwatch.GetTimestamp(); RenderCanvases(); fa = Stopwatch.GetTimestamp() - canvas;
                long commit = Stopwatch.GetTimestamp(), growths = commitGrowths; int commitGc = GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2);
                CommitFull(); fk = Stopwatch.GetTimestamp() - commit; packetTicks += fk;
                if (fk > commitMaxTicks) { commitMaxTicks = fk; peakPacketPoses = poseCount; peakPacketDirty = dirtyCount; peakPacketBytes = constantCount; commitPeakGc = GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2)-commitGc; commitPeakGrowths = commitGrowths-growths; }
                haveNativeFrame = true;
            }
            catch (Exception e) { Log("FULL frame failed: " + e); Stop("full frame invalid; restored"); }
            finally
            {
                // Include notification rescans and visibility replans, even when
                // their branch returns early after committing the replacement.
                if (rendering) { long ticks = Stopwatch.GetTimestamp() - t; fullFrames++; fullTicks += ticks; if (ticks > fullMaxTicks) { fullMaxTicks = ticks; maxCollect = fc; maxPose = fp; maxSort = fs; maxCanvas = fa; maxPacket = fk; maxFrameNumber = fullFrames; maxGc = GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2)-gc; } }
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
                if (constantCount + b.Bytes.Length > constantBytes.Length) { int size = Math.Max(constantBytes.Length * 2, constantCount + b.Bytes.Length); commitGrowths++; commitGrowthBytes += size; Array.Resize(ref constantBytes, size); }
                Array.Copy(b.Bytes, 0, constantBytes, constantCount, b.Bytes.Length); Array.Copy(b.Bytes, b.Previous, b.Bytes.Length);
                dirtyIds[dirtyCount++] = (uint)b.NativeId; constantCount += b.Bytes.Length; constantOffsets[dirtyCount] = (uint)constantCount;
            }
            changedConstants.Clear();
            var target = camera.targetTexture; var p = camera.transform.position; float time = Time.time, dt = Time.deltaTime, smooth = Time.smoothDeltaTime;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            fixed (uint* pi = poseIds, di = dirtyIds, co = constantOffsets, dl = drawList, seg = segments, li = liveIds) fixed (byte* bytes = constantBytes, lv = liveValues)
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
                for (int i = 0; i < liveChanges.Count; i++) { var source = liveChanges[i]; li[i] = (uint)source.Index; lv[i] = source.Live && source.R ? (byte)1 : (byte)0; }
                var active = new N.FrameActive { Frame = f, Ids = li, Values = lv, Count = (uint)liveChanges.Count };
                N.Require(frameWorldOnly ? N.Op.CommitActiveWorld : N.Op.CommitActive, fullToken, &active); sendList = false;
                foreach (var source in liveChanges) source.LiveQueued = false; liveChanges.Clear();
            }
        }
        static string FullState()
        {
            N.Stats s = default; N.Require(N.Op.Stats, fullToken, &s);
            N.BatchPerformance batch = default; N.Require(N.Op.BatchPerf, fullToken, &batch);
            N.Performance perf = default; N.Require(N.Op.Perf, fullToken, &perf);
            N.UploadPerformance up = default; N.Require(N.Op.UploadPerf, fullToken, &up);
            N.GpuPerformance gpu = default; N.Require(N.Op.GpuPerf, fullToken, &gpu);
            double phaseScale = 1e6 / Stopwatch.Frequency / Math.Max(1, fullFrames);
            return FormattableString.Invariant($"mode=M4h-original active=True native={nativeCount} unityExplicit={fallbackCount} sources={sources.Count} dormant={dormant.Count} poseActive={poseSources.Count} pose_capacity={poseCapacity} pose_world_only={frameWorldOnly} inverse_poses={inversePoseCount} pose_adds={poseAdds} pose_removes={poseRemoves} incremental_keys={incrementalKeys} live_toggles={liveToggles} removedMask={removedMask} retainedMask={retainedMask} frames={s.Frames} draws={s.Draws} errors={s.Errors} dropped={s.Dropped} main_us={(fullFrames != 0 ? fullTicks * 1e6 / Stopwatch.Frequency / fullFrames : 0):F3} main_max_us={fullMaxTicks * 1e6 / Stopwatch.Frequency:F3} render_us={s.RenderUs:F3} commit_us={s.CommitUs:F3} replans={orderBuilds} commit_max_us={commitMaxTicks * 1e6 / Stopwatch.Frequency:F3} replan_max_us={replanMaxTicks * 1e6 / Stopwatch.Frequency:F3} {CanvasState()} collect_visited={collectVisited} collect_state_only={collectStateSignals} collect_canvas_us={collectCanvasTicks*phaseScale:F3} collect_stats_us={collectStatsTicks*phaseScale:F3} collect_tracking_us={collectTrackingTicks*phaseScale:F3} pose_schedule_us={poseScheduleTicks*phaseScale:F3} pose_wait_us={poseWaitTicks*phaseScale:F3} pose_wait_max_us={poseWaitMaxTicks*1e6/Stopwatch.Frequency:F3} pose_results_us={poseResultsTicks*phaseScale:F3} pose_changed_slots={poseChangedSlots} local_order_patches={localOrderPatches} local_order_entries={localOrderEntries} collect_signals={collectSignals} collect_tint_signals={collectTintSignals} collect_full_signals={collectFullSignals} fill_calls={fillCalls} collect_refresh_sample_us_total={collectRefreshTicks*1e6/Stopwatch.Frequency:F3} collect_promote_sample_us_total={collectPromoteTicks*1e6/Stopwatch.Frequency:F3} fill_sample_us_total={fillTicks*1e6/Stopwatch.Frequency:F3} collect_us={collectTicks * phaseScale:F3} pose_us={poseTicks * phaseScale:F3} sort_us={sortTicks * phaseScale:F3} packet_us={packetTicks * phaseScale:F3} pose_uploads={poseCount} dirty_draws={dirtyCount} packet_bytes={constantCount} rt_apply_us={perf.ApplyUs:F3} rt_cull_us={perf.CullUs:F3} rt_constants_us={perf.ConstantsUs:F3} rt_draw_us={perf.DrawUs:F3} rt_execute_us={perf.ExecuteUs:F3} rt_uploads={perf.Uploads} rt_upload_bytes={perf.UploadBytes} upload_work_bytes={up.WorkBytes} baked_vertices={up.BakedVertices} upload_geometry_bytes={up.GeometryBytes} upload_pose_bytes={up.PoseBytes} upload_draw_bytes={up.DrawBytes} upload_index_bytes={up.IndexBytes} upload_constant_bytes={up.ConstantBytes} upload_color_bytes={up.ColorBytes} upload_cpu_batch_bytes={up.CpuBatchBytes} upload_pose_ranges={up.PoseRanges} upload_draw_ranges={up.DrawRanges} upload_index_calls={up.IndexUploads} upload_constant_calls={up.ConstantUploads} single_unverified={up.SingleUnverified} single_geometry={up.SingleGeometry} single_program={up.SingleProgram} single_constants={up.SingleConstants} single_textures={up.SingleTextures} single_segment={up.SingleSegment} single_limit={up.SingleLimit} layouts={perf.LayoutCreates} layout_hits={perf.LayoutHits} rt_sample_frames={perf.SampledFrames} gpu_batch={batch.GpuAvailable != 0} gpu_dispatches={batch.GpuDispatches} gpu_pose_uploads={batch.GpuPoseUploads} gpu_geometry_uploads={batch.GpuGeometryUploads} batch_runs={batch.Batches} batch_sources={batch.Sources} batch_vertices={batch.Vertices} single_draws={batch.SingleDraws} cb_creates={batch.ConstantCreates} rt_batch_us={batch.BuildUs / Math.Max(1.0, perf.SampledFrames):F3} avoided_order_replans={avoidedOrderReplans} key_only_frames={keyOnlyFrames} max_frame={maxFrameNumber} max_collect_us={maxCollect*1e6/Stopwatch.Frequency:F3} max_pose_us={maxPose*1e6/Stopwatch.Frequency:F3} max_sort_us={maxSort*1e6/Stopwatch.Frequency:F3} max_canvas_us={maxCanvas*1e6/Stopwatch.Frequency:F3} max_packet_us={maxPacket*1e6/Stopwatch.Frequency:F3} max_gc_collections={maxGc} commit_peak_poses={peakPacketPoses} commit_peak_dirty={peakPacketDirty} commit_peak_bytes={peakPacketBytes} commit_peak_gc={commitPeakGc} commit_peak_growths={commitPeakGrowths} commit_growths={commitGrowths} commit_growth_bytes={commitGrowthBytes} avoided_mask_replans={avoidedMaskReplans} avoided_empty_replans={avoidedEmptyReplans} canvas_pose_signals={canvasPoseSignals} order_key_changes={orderKeyChanges} draw_rebinds={drawRebinds} prepare_slices={prepareSlices} prepare_max_ms={prepareMaxTicks * 1000.0 / Stopwatch.Frequency:F3} prepare_work_ms={prepareWorkTicks * 1000.0 / Stopwatch.Frequency:F3} ready_ms={(firstReadyTicks == 0 ? 0 : (firstReadyTicks-fullStartTicks)*1000.0/Stopwatch.Frequency):F3} warm_ms={warmMs:F3} warm_cached={warmHooks != null} setup_ms={(preparationFinishedTicks-fullStartTicks)*1000.0/Stopwatch.Frequency:F3} setup_hooks_ms={StageMs("hooks"):F3} setup_discover_ms={StageMs("discover"):F3} setup_canvas_ms={StageMs("canvas"):F3} setup_native_ms={StageMs("native"):F3} setup_packets_ms={StageMs("packets"):F3} setup_shader_ms={StageMs("shader-resource"):F3} setup_bind_ms={StageMs("bind"):F3} setup_publish_ms={StageMs("publish"):F3} dead_sources={deadSources} canvas_promotions={canvasPromotions} hierarchy_incremental={hierarchyIncremental} rescans_marked={rescanCount} safe={(Resilience.SafeMode ? 1 : 0)} grab_copies={gpu.GrabCopies} grab_bytes={gpu.GrabBytes} named_grab_hits={gpu.NamedGrabHits} culled_draws={gpu.CulledDraws} culled_sources={gpu.CulledSources} gpu_event_us={gpu.EventUs:F3} gpu_event_max_us={gpu.MaxEventUs:F3} gpu_samples={gpu.Samples} gpu_pending={gpu.Pending} gpu_disjoint={gpu.Disjoint} {TimelineState()} {MemoryState()} refresh_per_frame={(fullFrames != 0 ? refreshCount / (double)fullFrames : 0):F1} refresh_tint={refreshTint} refresh_us={(fullFrames != 0 ? refreshTicks * 1e6 / Stopwatch.Frequency / fullFrames : 0):F3} canvas_graphic_signals={canvasGraphicSignals} canvas_incremental_replans={canvasIncrementalReplans} canvas_full_replans={canvasFullReplans} canvas_visibility_replans={canvasVisibilityReplans} canvas_material_skipped={canvasMaterialSignalsSkipped} canvas_full_why={CanvasFullReasonsTop()} rebind_states={rebindStates} rebind_textures={rebindTextures} rebind_bounds={rebindBounds} rebind_geometry={rebindGeometry} prebuilt_sources={prebuiltSources} pose_direct={DirectSequenceChanges.PoseDirect} parity=UNMEASURED");
        }
        static string WhyReplan()
        {
            foreach (var pair in orderReasons) Log("order reason=" + pair.Key + " count=" + pair.Value);
            foreach (var pair in signalWriters) Log("signal target=" + pair.Key.Item1.FullName + " writer=" + pair.Key.Item2 + " count=" + pair.Value);
            return "replans=" + orderBuilds + " unchangedMask=" + avoidedMaskReplans + " emptySprite=" + avoidedEmptyReplans + " canvasPose=" + canvasPoseSignals + " changedKey=" + orderKeyChanges + " reboundDraw=" + drawRebinds;
        }
        static void AssertCache()
        {
            N.CachePerformance cache = default; N.Require(N.Op.CachePerf, fullToken, &cache);
            if (!commandCache || cache.Available == 0 || cache.Segments == 0 || cache.Replays <= cache.Records || cache.FallbackSegments != 0)
                throw new InvalidOperationException("drfull INVALID: reusable command cache unavailable, cold or bypassed");
            N.TimelinePerformance timeline = default; N.Require(N.Op.TimelinePerf, fullToken, &timeline);
            N.GpuPerformance gpu = default; N.Require(N.Op.GpuPerf, fullToken, &gpu);
            if (resourceConfig.SkipGrab != 0 || (timeline.VisibleGrabs != 0 && gpu.GrabCopies == 0))
                throw new InvalidOperationException("drfull INVALID: visible Grab snapshots disabled or missing");
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
            rendering = false; sampleCollect = false; collectSignals = collectTintSignals = collectFullSignals = fillCalls = fillTicks = collectRefreshTicks = collectPromoteTicks = 0;
            if (before != null) { if (camera) camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, before); before.Release(); before = null; }
            if (opaque != null) { if (camera) camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, opaque); opaque.Release(); opaque = null; }
            ReleaseTimeline();
            ReleaseParticleMeshes();
            ReleaseCanvases();
            if (fullToken != 0) { N.Call(N.Op.Destroy, fullToken); fullToken = 0; }
            if (transforms.isCreated) transforms.Dispose(); if (poses.IsCreated) poses.Dispose(); if (moved.IsCreated) moved.Dispose();
            if (poseChanges.IsCreated) poseChanges.Dispose();
            if (inverseRequired.IsCreated) inverseRequired.Dispose();
            if (centers.IsCreated) centers.Dispose(); if (distances.IsCreated) distances.Dispose(); if (reordered.IsCreated) reordered.Dispose();
            if (previousCenters.IsCreated) previousCenters.Dispose(); haveCaptureAxis = false;
            foreach (var l in sourceLifetimes) if (l) { l.Changed = null; UnityEngine.Object.Destroy(l); }
            collectStateSignals = collectVisited = collectCanvasTicks = collectStatsTicks = collectTrackingTicks = poseWaitTicks = poseScheduleTicks = poseResultsTicks = poseChangedSlots = poseWaitMaxTicks = 0;
            localOrderPatches = localOrderEntries = 0;
            distanceOrderSkips = distanceOrderVisibility = 0; orderCommandsReady = false; avoidedOrderReplans = keyOnlyFrames = commitGrowths = commitGrowthBytes = 0;
            maxCollect = maxPose = maxSort = maxCanvas = maxPacket = maxGc = maxFrameNumber = 0;
            peakPacketPoses = peakPacketDirty = peakPacketBytes = commitPeakGc = commitPeakGrowths = 0;
            orderTree.Clear(); orderChanges.Clear(); liveChanges.Clear(); orderReasons.Clear(); entrySerial = 0; poseAdds = poseRemoves = incrementalKeys = liveToggles = 0;
            liveIds = null; liveValues = null; inversePoseCount = 0;
            sourceLifetimes.Clear(); sources.Clear(); changed.Clear(); changedConstants.Clear(); ordered.Clear(); sourceIndex.Clear();
            geometryCache.Clear(); meshVersions.Clear(); programs.Clear(); texturePointers.Clear(); grabNames.Clear(); fallbackReasons.Clear();
            rendererSources.Clear(); dormant.Clear(); pendingPromotions.Clear(); pendingCanvasPromotions.Clear(); poseSources.Clear();
            if (uploadPoses.IsCreated) uploadPoses.Dispose();
            poseIds = dirtyIds = constantOffsets = drawList = segments = null; constantBytes = null;
            fullTarget = null;
            fullRescan = canvasDirty = false;
            nativeCount = fallbackCount = 0; avoidedMaskReplans = avoidedEmptyReplans = canvasPoseSignals = orderKeyChanges = drawRebinds = 0; signalWriters.Clear(); orderDirty = sendList = globalsDirty = false;
        }
    }
}
#endif
