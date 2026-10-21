#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    internal static partial class LiveDeco
    {
        const string RuntimeId = "StutterFix.livedeco2";
        static Runtime runtime;
        static string runtimeFailure = "none";
        static readonly AccessTools.FieldRef<scrParallax, bool> ParInited = AccessTools.FieldRefAccess<scrParallax, bool>("inited");
        static readonly AccessTools.FieldRef<scrParallax, bool> ParDeco = AccessTools.FieldRefAccess<scrParallax, bool>("isDecoration");
        static readonly AccessTools.FieldRef<scrParallax, Transform> ParTransform = AccessTools.FieldRefAccess<scrParallax, Transform>("cachedTransform");
        static readonly AccessTools.FieldRef<scrParallax, Vector2> ParStart = AccessTools.FieldRefAccess<scrParallax, Vector2>("startPosition");
        static readonly AccessTools.FieldRef<scrParallax, Vector2> ParEnd = AccessTools.FieldRefAccess<scrParallax, Vector2>("endPosition");
        static readonly AccessTools.FieldRef<scrDecoration, Color> RenderedColor = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        static readonly AccessTools.FieldRef<DecoTransformGizmoHolder, scrDecoration> GizmoDecoration = AccessTools.FieldRefAccess<DecoTransformGizmoHolder, scrDecoration>("currentDec");
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex { internal Vector3 Position; internal Color32 Color; internal Vector2 UV; }
        readonly struct VertexWriter : LiveDecoPolicy.IVertexWriter<Member, Vertex>
        {
            readonly float z;
            internal VertexWriter(float depth) { z = depth; }
            public bool Dirty(Member m) => m.VerticesDirty;
            public void Write(Member m, Vertex[] vertices, int offset)
            {
                for (int k = 0; k < 4; k++)
                {
                    var point = m.Matrix.MultiplyPoint3x4(m.Corners[k]); point.z -= z;
                    vertices[offset + k] = new Vertex { Position = point, Color = m.LastColor, UV = m.AtlasUV[k] };
                }
                m.VerticesDirty = false;
            }
        }
        readonly struct HierarchyPath : LiveDecoHierarchy.IPath<Transform, Matrix4x4>
        {
            readonly bool owned;
            readonly Vector3 childPosition;
            internal HierarchyPath(Member m) { owned = m.Owned; childPosition = m.ChildPosition; }
            public bool Alive(Transform node) => node;
            public bool Same(Transform a, Transform b) => a == b;
            public Transform Parent(Transform node) => node.parent;
            public Matrix4x4 Identity => Matrix4x4.identity;
            public Matrix4x4 Local(Transform node, bool child) => Matrix4x4.TRS(
                child && owned ? childPosition : node.localPosition, node.localRotation, node.localScale);
            public Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b) => a * b;
        }
        sealed class Member
        {
            internal scrVisualDecoration D;
            internal SpriteRenderer R;
            internal Sprite Sprite;
            internal Material Material;
            internal string Signature;
            internal bool Owned, OriginalOff;
            internal Vector2[] Corners, UV;
            internal Vector2[] AtlasUV;
            internal ushort[] Triangles;
            internal LiveDecoAtlas.Entry Atlas;
            internal Matrix4x4 Matrix;
            internal Transform[] PathNodes;
            internal Matrix4x4[] PathLocals;
            internal Vector3[] PathPositions, PathScales;
            internal Quaternion[] PathRotations;
            internal int ChildIndex;
            internal Quaternion PivotRotation;
            internal Vector3 SpriteCenter;
            internal Matrix4x4 Flip;
            internal bool FlipX, FlipY, GeometryDirty = true, VerticesDirty = true, HasCalculation;
            internal Vector2 LastEnd, LastScale;
            internal float LastAngle;
            internal Color32 LastColor;
            internal Vector3 ChildPosition;
            internal string Rejected;
            internal int InitialHeight, InitialWidth;
        }
        sealed class MeshRow
        {
            internal Renderer R;
            internal Material Material;
            internal Member Member;
            internal SceneRenderOrder.Key Key;
            internal int Slot;
        }
        sealed class Run
        {
            internal GameObject Object;
            internal Mesh Mesh;
            internal MeshRenderer Renderer;
            internal Member[] Members;
            internal int VertexOffset;
            internal Material Material;
            internal LiveDecoAtlas.Page Page;
            internal string Signature;
            internal int Layer, Order, UnityLayer;
            internal float Z;
            internal bool Dirty = true;
        }
        sealed class RowComparer : IComparer<MeshRow>
        { internal static readonly RowComparer Instance = new RowComparer(); public int Compare(MeshRow a, MeshRow b) => a.Key.CompareTo(b.Key); }
        sealed class Runtime : IDisposable
        {
            internal Camera Camera;
            internal scrDecorationManager Manager;
            internal Renderer[] Renderers;
            internal DecoTransformGizmoHolder[] EditorGizmos;
            internal int DecoCount, Frame = -1, Stamp;
            internal readonly Dictionary<scrDecoration, Member> Members = new Dictionary<scrDecoration, Member>();
            internal readonly Dictionary<int, Member> Images = new Dictionary<int, Member>();
            internal readonly List<Member> AdmissionOrder = new List<Member>();
            internal readonly Dictionary<int, MeshRow[]> CachedRows = new Dictionary<int, MeshRow[]>();
            internal readonly List<MeshRow> Rows = new List<MeshRow>();
            internal readonly List<Material> Materials = new List<Material>();
            internal readonly Material[] OneMaterial = new Material[1];
            internal readonly List<Run> Runs = new List<Run>();
            internal readonly List<Run> NextRuns = new List<Run>();
            internal readonly List<Member> Segment = new List<Member>();
            internal readonly SortedDictionary<string, int> Exclusions = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal readonly List<string> ReasonKeys = new List<string>();
            internal readonly LiveDecoAtlas Atlas = new LiveDecoAtlas();
            internal Vertex[] Vertices = Array.Empty<Vertex>();
            internal readonly BlockCensus Blocks = new BlockCensus();
            internal Harmony Harmony;
            internal bool Disposing;
            internal bool TransitionPending;
            internal int Owned, LiveImages, NewRuns, ReusedRuns;
            internal int ChangedMembers, UploadedRuns;
            internal readonly Dictionary<Transform, Vector3> Positions = new Dictionary<Transform, Vector3>();
            internal bool ControllerPaused;
            internal float LockedScale, CameraAngle;
            internal Rect CameraRect;
            internal Camera GameCamera;
            internal double VertexMax;
            internal int VertexCount, VertexRejected;
            internal long ComputeTicks, UploadTicks, RebuildTicks, Samples, StableAllocated, TransitionAllocated;
            internal void Begin()
            {
                Camera = GpuSplit.FindMain(); Manager = scrDecorationManager.instance;
                if (!Camera || !Manager) throw new InvalidOperationException("camera/manager missing");
                if (GraphicsSettings.currentRenderPipeline != null || !Camera.orthographic) throw new InvalidOperationException("requires Built-in orthographic camera");
                var patches = LiveDecoPatchPlan.Build(typeof(scrDecoration), typeof(scrVisualDecoration), typeof(scrParallax), typeof(Vector2), typeof(Color));
                // Preflight every target before allocating atlas pages or applying
                // any hook; a bad target must not leave a partially installed plan.
                foreach (var patch in patches.Entries)
                {
                    var other = HarmonyLib.Harmony.GetPatchInfo(patch.Method);
                    if (other != null && other.Owners.Any(id => id != Main.Entry.Info.Id && id != "StutterFix.fuseprobe" && id != RuntimeId))
                        throw new InvalidOperationException("foreign-patch " + patch.Method.DeclaringType.Name + "." + patch.Method.Name);
                }
                Log("patch_plan version=2 targets=" + patches.Entries.Count + " abstract_declarations_skipped=" + patches.AbstractDeclarations.Count);
                Log("pipeline_builtin=True atlas_path=RGBA32 compressed_copy=0 reason=DXT-exact-edge-extrusion-unavailable pixel_equivalence=UNVERIFIED");
                Log("admission version=4 hierarchy=renderer-to-pivot logical_child=1 max_depth=" + LiveDecoHierarchy.MaxDepth
                    + " editing=" + ADOBase.isEditingLevel + " diagnostic_playback_pause=" + FrameCompare.FrozenPlayback);
                Log("update version=6 cached_uv=1 cached_hierarchy=1 exact_dirty=1 dirty_upload=1 validation=each-frame cpu_total_includes_validation=1");
                EditorGizmos = Resources.FindObjectsOfTypeAll<DecoTransformGizmoHolder>();
                DecoCount = Manager.allDecorations.Count;
                foreach (var d in Manager.allDecorations)
                    if (d is scrVisualDecoration v && v && v.spriteRenderer)
                    {
                        var m = new Member { D = v, R = v.spriteRenderer };
                        var texture = m.R.sprite ? m.R.sprite.texture : null;
                        m.InitialHeight = texture ? texture.height : 0; m.InitialWidth = texture ? texture.width : 0;
                        Members.Add(d, m); Images.Add(m.R.GetInstanceID(), m);
                        AdmissionOrder.Add(m);
                    }
                AdmissionOrder.Sort((a, b) => { int c = b.InitialHeight.CompareTo(a.InitialHeight); return c != 0 ? c : b.InitialWidth.CompareTo(a.InitialWidth); });
                var hierarchyCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var m in AdmissionOrder)
                    if (Live(m.R))
                    {
                        var d = m.D;
                        var status = LiveDecoHierarchy.Compose<Transform, Matrix4x4, HierarchyPath>(m.R.transform, d.childTransform,
                            d.pivotTrans, new HierarchyPath(m), false, out _, out int depth);
                        string key = "renderer_is_child=" + (m.R.transform == d.childTransform)
                            + " child_parent_is_pivot=" + (d.childTransform && d.childTransform.parent == d.pivotTrans)
                            + " depth=" + depth + " path=" + status;
                        hierarchyCounts.TryGetValue(key, out int count); hierarchyCounts[key] = count + 1;
                    }
                foreach (var pair in hierarchyCounts) Log("hierarchy " + pair.Key + " count=" + pair.Value);
                Atlas.Plan(AdmissionOrder.Where(m => Live(m.R) && m.R.sprite).Select(m => m.R.sprite.texture));
                Renderers = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r && r.gameObject.scene.IsValid()).ToArray();
                foreach (var r in Renderers)
                {
                    r.GetSharedMaterials(Materials);
                    var slots = new MeshRow[Math.Max(1, Materials.Count)];
                    for (int n = 0; n < slots.Length; n++) slots[n] = new MeshRow { R = r, Slot = n };
                    CachedRows.Add(r.GetInstanceID(), slots);
                }
                Harmony = new Harmony(RuntimeId);
                foreach (var patch in patches.Entries)
                    try { Harmony.Patch(patch.Method, prefix: new HarmonyMethod(typeof(LiveDeco), patch.Prefix) { priority = Priority.First }); }
                    catch (Exception e) { throw new InvalidOperationException("patch-install " + patch.Method.DeclaringType.Name + "." + patch.Method.Name + ":" + e.GetType().Name + ":" + e.Message, e); }
                Tick();
                if (Owned == 0) throw new InvalidOperationException("zero-owned-see-exclusions");
                Camera.onPreCull += RuntimePreCull;
                SceneManager.activeSceneChanged += RuntimeSceneChanged;
                Log(RuntimeState());
            }
            internal string InputReason(Member m)
            {
                var d = m.D; var r = m.R;
                if (!d || !r || d.manager != Manager) return "owner-lifetime";
                if (!r.enabled || !r.gameObject.activeInHierarchy) return "inactive";
                if (!m.Owned && r.forceRenderingOff) return "external-forced-off";
                var hierarchy = LiveDecoHierarchy.Compose<Transform, Matrix4x4, HierarchyPath>(r.transform, d.childTransform,
                    d.pivotTrans, new HierarchyPath(m), false, out _, out _);
                if (hierarchy != LiveDecoHierarchy.Failure.None) return HierarchyReason(hierarchy);
                var parent = d.pivotTrans.parent;
                if (!parent || parent.localToWorldMatrix != Matrix4x4.identity) return "nonidentity-parent";
                if (d.hitbox != 0 || d.canHitPlanets || (d.activeCollider && d.activeCollider.enabled && d.activeCollider.gameObject.activeInHierarchy)) return "transform-reader-hitbox";
                if (LiveDecoPolicy.EditorTransformReader(ADOBase.isEditingLevel, FrameCompare.FrozenPlayback,
                    d.selectionBordersObject && d.selectionBordersObject.activeInHierarchy,
                    d.hitboxBordersObject && d.hitboxBordersObject.activeInHierarchy, EditorReads(d))) return "transform-reader-editor";
                if (d.stickToFloor) return "transform-reader-floor-parent";
                if (d.cfpCache != null) foreach (var effect in d.cfpCache) if (effect && effect.enabled) return "transform-reader-filter";
                var p = d.parallax;
                if (!p || !ParInited(p) || !ParDeco(p) || ParTransform(p) != d.pivotTrans || p.decoration != d || !p.cameraTransform) return "parallax-state";
                if (p.dontAlterX || p.dontAlterY) return "parallax-axis-history";
                if (d.textureScaleMultiplier.x == 0 || d.textureScaleMultiplier.y == 0) return "zero-texture-scale";
                if (Live(d.meshRenderer) || d.isMask() || r.maskInteraction != SpriteMaskInteraction.None || (d.spriteMask && d.spriteMask.enabled)) return "mask-or-mesh";
                if (d.repeatX != 1 || d.repeatY != 1 || r.drawMode != SpriteDrawMode.Simple) return "repeat-or-drawmode";
                if (!r.sprite || !r.sharedMaterial) return "missing-sprite-material";
                if (m.Owned && (r.sprite != m.Sprite || r.sharedMaterial != m.Material || !m.Atlas.Valid)) return "input-changed";
                if (m.Owned) RefreshGeometry(m);
                if ((Camera.cullingMask & (1 << r.gameObject.layer)) == 0) return "camera-layer";
                return LiveDecoAtlas.Reason(r.sprite.texture);
            }
            bool EditorReads(scrDecoration d)
            {
                if (!ADOBase.isEditingLevel) return false;
                if (ADOBase.editor && ADOBase.editor.selectedDecorations.Contains(d.sourceLevelEvent)) return true;
                // LateUpdate reads currentDec before checking gizmo visibility;
                // a cached target still matters even after deselection.
                foreach (var gizmo in EditorGizmos)
                    if (gizmo && gizmo.isActiveAndEnabled && GizmoDecoration(gizmo) == d) return true;
                return false;
            }
            Matrix4x4 Calculate(Member m, bool publish)
            {
                var d = m.D; var p = d.parallax;
                Vector2 follow = !ControllerPaused && d.followPlanet != null ? (Vector2)Position(d.followPlanet.transform) : Vector2.zero;
                Vector2 start = d.pivotPosVec + follow;
                Vector2 end;
                if (p.clampToScreen)
                {
                    var rect = CameraRect;
                    end = GameCamera.ScreenToWorldPoint(new Vector2(rect.x, rect.y) + new Vector2(rect.width * p.screenRelativePos.x, rect.height * p.screenRelativePos.y));
                }
                else end = (Vector2)(Position(p.cameraTransform) - (Vector3)start) * p.multiplier + start;
                Vector2 camScale = (d.lockScale ? LockedScale : 1f) * d.trueScaleMultiplier;
                end += d.parallaxOffset * camScale / d.textureScaleMultiplier;
                if (publish) { ParStart(p) = start; p.posCamAtStart = start; ParEnd(p) = end; }
                float angle = d.startRot + d.rotAngle;
                if (d.lockRotation) angle += CameraAngle;
                Vector2 scale = d.scaleVec * camScale * (Vector2)Vector3.one;
                if (m.HasCalculation && !m.GeometryDirty && m.LastEnd.Equals(end) && m.LastScale.Equals(scale) && m.LastAngle.Equals(angle)) return m.Matrix;
                var child = m.PathLocals[m.ChildIndex];
                var childMatrix = LiveDecoHierarchy.ComposeCached<Transform, Matrix4x4, HierarchyPath>(m.PathLocals, m.ChildIndex, child, default);
                if (!m.HasCalculation || !m.LastAngle.Equals(angle)) m.PivotRotation = Quaternion.Euler(0, 0, angle);
                var matrix = Matrix4x4.TRS(new Vector3(end.x, end.y, 0), m.PivotRotation, new Vector3(scale.x, scale.y, 1))
                    * childMatrix;
                m.LastEnd = end; m.LastScale = scale; m.LastAngle = angle; m.HasCalculation = true; m.GeometryDirty = false;
                m.VerticesDirty = true;
                return matrix * m.Flip;
            }
            Vector3 Position(Transform t)
            {
                if (!Positions.TryGetValue(t, out var value)) { value = t.position; Positions.Add(t, value); }
                return value;
            }
            void RefreshGeometry(Member m)
            {
                // Validation remains explicit and conservative. Capture native
                // locals here, never inside the corner/UV computation loop.
                var node = m.R.transform;
                int depth = 0;
                while (node != m.D.pivotTrans)
                {
                    if (!node || depth == LiveDecoHierarchy.MaxDepth) throw new InvalidOperationException("hierarchy-changed-during-validation");
                    if (m.PathNodes == null || depth >= m.PathNodes.Length || m.PathNodes[depth] != node)
                    { CaptureGeometry(m); return; }
                    bool child = node == m.D.childTransform;
                    var position = child ? m.ChildPosition : node.localPosition;
                    var rotation = node.localRotation; var scale = node.localScale;
                    if (!m.PathPositions[depth].Equals(position) || !m.PathRotations[depth].Equals(rotation) || !m.PathScales[depth].Equals(scale))
                    {
                        m.PathPositions[depth] = position; m.PathRotations[depth] = rotation; m.PathScales[depth] = scale;
                        m.PathLocals[depth] = Matrix4x4.TRS(position, rotation, scale); m.GeometryDirty = true;
                    }
                    if (child) m.ChildIndex = depth;
                    depth++; node = node.parent;
                }
                if (depth != m.PathNodes.Length) { CaptureGeometry(m); return; }
                bool x = m.R.flipX, y = m.R.flipY;
                if (m.FlipX != x || m.FlipY != y) { m.FlipX = x; m.FlipY = y; m.Flip = Matrix4x4.Scale(new Vector3(x ? -1 : 1, y ? -1 : 1, 1)); m.GeometryDirty = true; }
            }
            void CaptureGeometry(Member m)
            {
                var nodes = new List<Transform>(); var locals = new List<Matrix4x4>();
                var positions = new List<Vector3>(); var rotations = new List<Quaternion>(); var scales = new List<Vector3>();
                var node = m.R.transform; m.ChildIndex = -1;
                while (node && node != m.D.pivotTrans && nodes.Count < LiveDecoHierarchy.MaxDepth)
                {
                    bool child = node == m.D.childTransform;
                    if (child) m.ChildIndex = nodes.Count;
                    var position = child ? m.ChildPosition : node.localPosition; var rotation = node.localRotation; var scale = node.localScale;
                    positions.Add(position); rotations.Add(rotation); scales.Add(scale);
                    nodes.Add(node); locals.Add(Matrix4x4.TRS(position, rotation, scale));
                    node = node.parent;
                }
                if (node != m.D.pivotTrans || m.ChildIndex < 0) throw new InvalidOperationException("hierarchy-changed-during-capture");
                m.PathNodes = nodes.ToArray(); m.PathLocals = locals.ToArray();
                m.PathPositions = positions.ToArray(); m.PathRotations = rotations.ToArray(); m.PathScales = scales.ToArray();
                m.FlipX = m.R.flipX; m.FlipY = m.R.flipY;
                m.Flip = Matrix4x4.Scale(new Vector3(m.FlipX ? -1 : 1, m.FlipY ? -1 : 1, 1)); m.GeometryDirty = true;
            }
            bool Admit(Member m)
            {
                string reason = InputReason(m);
                if (reason != null) { Reject(reason); return false; }
                if (!m.R.color.Equals(RenderedColor(m.D))) { Reject("renderer-logical-color-mismatch"); return false; }
                if (m.Sprite == m.R.sprite && m.Material == m.R.sharedMaterial && m.Rejected != null) { Reject(m.Rejected); return false; }
                m.R.GetSharedMaterials(Materials);
                if (Materials.Count != 1) { Reject("material-slots"); return false; }
                OneMaterial[0] = Materials[0];
                reason = Reason(m.D, m.R, OneMaterial, Camera) ?? Blocks.Read(m.R, OneMaterial);
                var material = Materials[0];
                if (reason == null && material.HasProperty("_MainTex") && (material.GetTextureScale("_MainTex") != Vector2.one || material.GetTextureOffset("_MainTex") != Vector2.zero)) reason = "atlas-material-texture-transform";
                if (reason == null && ((material.HasProperty("RepeatX") && material.GetFloat("RepeatX") != 1) || (material.HasProperty("RepeatY") && material.GetFloat("RepeatY") != 1))) reason = "atlas-material-repeat";
                if (reason != null) { m.Sprite = m.R.sprite; m.Material = m.R.sharedMaterial; m.Corners = null; m.Rejected = reason; Reject(reason); return false; }
                if (m.R.sprite.associatedAlphaSplitTexture) { Reject("split-alpha"); return false; }
                var sprite = m.R.sprite;
                if (m.Sprite != sprite || m.Corners == null)
                {
                    m.Sprite = sprite; m.Corners = sprite.vertices; m.UV = sprite.uv; m.Triangles = sprite.triangles;
                }
                if (m.Corners.Length != 4 || m.UV.Length != 4 || m.Triangles.Length != 6) { m.Material = Materials[0]; m.Rejected = "nonquad-sprite"; Reject(m.Rejected); return false; }
                m.ChildPosition = m.D.childTransform.localPosition; CaptureGeometry(m);
                m.SpriteCenter = sprite.bounds.center;
                m.Matrix = Calculate(m, false);
                var actual = m.R.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(m.R.flipX ? -1 : 1, m.R.flipY ? -1 : 1, 1));
                double error = 0;
                for (int k = 0; k < 4; k++) error = Math.Max(error, Vector3.Distance(m.Matrix.MultiplyPoint3x4(m.Corners[k]), actual.MultiplyPoint3x4(m.Corners[k])));
                VertexMax = Math.Max(VertexMax, error); VertexCount++;
                if (double.IsNaN(error) || error > 1e-4) { VertexRejected++; m.Material = Materials[0]; m.Rejected = "vertex-error"; Reject(m.Rejected); return false; }
                m.Material = Materials[0]; m.Signature = Signature(m.Material); m.Rejected = null;
                m.Atlas = Atlas.Get(sprite.texture);
                m.AtlasUV = new Vector2[4]; for (int k = 0; k < 4; k++) m.AtlasUV[k] = m.Atlas.Map(m.UV[k]);
                m.VerticesDirty = true;
                m.OriginalOff = m.R.forceRenderingOff; m.Owned = true; Owned++; TransitionPending = true;
                return true;
            }
            void Reject(string reason) { if (!Exclusions.ContainsKey(reason)) { Exclusions.Add(reason, 0); ReasonKeys.Add(reason); } Exclusions[reason]++; }
            internal void Release(Member m)
            {
                if (!m.Owned) return;
                m.Owned = false; Owned--; TransitionPending = true;
                // Disable all old draws until the next incremental partition update.
                foreach (var run in Runs) if (run.Renderer) run.Renderer.enabled = false;
                try { if (m.R) { m.R.forceRenderingOff = m.OriginalOff; m.R.color = RenderedColor(m.D); } }
                finally { if (m.D) { m.D.childTransform.localPosition = m.ChildPosition; m.D.UpdatePosition(); } }
            }
            internal void Tick()
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread(), begin = Stopwatch.GetTimestamp();
                if (!Manager || Manager != scrDecorationManager.instance || SceneReset.Resetting || Manager.allDecorations.Count != DecoCount
                    || (FrameCompare.Paused ? !FrameCompare.FrozenPlayback : !Hitch.Playing)) throw new InvalidOperationException("runtime-lifetime");
                var forward = Camera.transform.forward;
                var mode = Camera.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortMode : Camera.transparencySortMode;
                if (!Camera.orthographic || Math.Abs(forward.x) > 1e-6 || Math.Abs(forward.y) > 1e-6 || mode == TransparencySortMode.CustomAxis || mode == TransparencySortMode.Perspective)
                    throw new InvalidOperationException("unsupported-sort-camera");
                Frame = Time.frameCount; Stamp++; LiveImages = 0;
                Positions.Clear();
                ControllerPaused = ADOBase.controller.paused;
                var camy = ADOBase.controller.camy;
                GameCamera = camy.camobj; CameraRect = GameCamera.pixelRect;
                CameraAngle = camy.transform.rotation.eulerAngles.z;
                LockedScale = GameCamera.orthographicSize * 0.2f / (ADOBase.customLevel.levelData.camZoom / 100f);
                long calculationTicks = 0;
                ChangedMembers = UploadedRuns = 0;
                for (int n = 0; n < ReasonKeys.Count; n++) Exclusions[ReasonKeys[n]] = 0;
                int previousVertices = VertexCount;
                foreach (var m in AdmissionOrder)
                {
                    if (m.Owned && InputReason(m) != null) Release(m);
                    if (!m.R || !m.R.enabled || !m.R.gameObject.activeInHierarchy || (!m.Owned && m.R.forceRenderingOff)) continue;
                    LiveImages++;
                    if (!m.Owned && !Admit(m)) continue;
                    long calculationStart = Stopwatch.GetTimestamp();
                    m.Matrix = Calculate(m, true);
                    Color32 color = RenderedColor(m.D);
                    if (m.LastColor.r != color.r || m.LastColor.g != color.g || m.LastColor.b != color.b || m.LastColor.a != color.a)
                    { m.LastColor = color; m.VerticesDirty = true; }
                    if (m.VerticesDirty) ChangedMembers++;
                    calculationTicks += Stopwatch.GetTimestamp() - calculationStart;
                }
                long computed = Stopwatch.GetTimestamp();
                Rows.Clear();
                foreach (var renderer in Renderers)
                {
                    if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || (Camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    Images.TryGetValue(renderer.GetInstanceID(), out var member);
                    bool own = member != null && member.Owned;
                    if (renderer.forceRenderingOff && !own) continue;
                    if (renderer.GetComponentInParent<SortingGroup>()) throw new InvalidOperationException("external-sorting-group");
                    renderer.GetSharedMaterials(Materials);
                    var cache = CachedRows[renderer.GetInstanceID()];
                    if (cache.Length != Math.Max(1, Materials.Count)) throw new InvalidOperationException("renderer-slot-count-changed");
                    for (int slot = 0; slot < cache.Length; slot++)
                    {
                        var material = slot < Materials.Count ? Materials[slot] : null;
                        if (!material || material.renderQueue <= 2500) throw new InvalidOperationException("missing-or-opaque-material");
                        var row = cache[slot]; row.Material = material; row.Member = own ? member : null;
                        if (own)
                        {
                            Vector3 center = member.Matrix.MultiplyPoint3x4(member.SpriteCenter);
                            if (member.R.spriteSortPoint == SpriteSortPoint.Pivot) center = member.Matrix.MultiplyPoint3x4(Vector3.zero);
                            row.Key = new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), renderer.sortingOrder,
                                material.renderQueue, Vector3.Dot(center - Position(Camera.transform), forward), material.GetInstanceID(), renderer.GetInstanceID(), slot);
                        }
                        else row.Key = MatShare.SortKey(renderer, material, Camera, slot);
                        Rows.Add(row);
                    }
                }
                Rows.Sort(RowComparer.Instance);
                // Unknown same-material external ordering is never invented.
                for (int start = 0; start < Rows.Count;)
                {
                    int end = start + 1; bool own = Rows[start].Member != null, other = !own;
                    while (end < Rows.Count && Rows[start].Key.SameMaterialTie(Rows[end].Key)) { own |= Rows[end].Member != null; other |= Rows[end].Member == null; end++; }
                    if (own && other) throw new InvalidOperationException("ambiguous-material-tie");
                    start = end;
                }
                NewRuns = ReusedRuns = 0; NextRuns.Clear(); Segment.Clear(); MeshRow first = null;
                foreach (var row in Rows)
                {
                    if (row.Member == null) { Flush(first); first = null; continue; }
                    if (first != null && (!first.Key.SameBucket(row.Key) || first.Member.Signature != row.Member.Signature
                        || first.Member.Atlas.Page != row.Member.Atlas.Page || first.R.gameObject.layer != row.R.gameObject.layer)) { Flush(first); first = null; }
                    if (first == null) first = row;
                    Segment.Add(row.Member);
                }
                Flush(first);
                foreach (var old in Runs) if (!NextRuns.Contains(old)) DestroyRun(old);
                Runs.Clear(); Runs.AddRange(NextRuns);
                long atlasStart = Stopwatch.GetTimestamp();
                Atlas.Upload();
                long atlasEnd = Stopwatch.GetTimestamp();
                if (Vertices.Length < Owned * 4) Array.Resize(ref Vertices, Mathf.NextPowerOfTwo(Owned * 4));
                long rebuilt = Stopwatch.GetTimestamp();
                int vertexOffset = 0;
                foreach (var run in Runs)
                {
                    run.VertexOffset = vertexOffset;
                    run.Dirty = LiveDecoPolicy.UpdateVertices<Member, Vertex, VertexWriter>(run.Members, Vertices,
                        vertexOffset, run.Dirty, new VertexWriter(run.Z));
                    vertexOffset += run.Members.Length * 4;
                }
                long verticesEnd = Stopwatch.GetTimestamp();
                foreach (var run in Runs)
                {
                    if (run.Dirty)
                    {
                        run.Mesh.SetVertexBufferData(Vertices, run.VertexOffset, 0, run.Members.Length * 4, 0,
                            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers);
                        UploadedRuns++; run.Dirty = false;
                    }
                    run.Renderer.enabled = true;
                }
                foreach (var m in Members.Values) if (m.Owned) m.R.forceRenderingOff = true;
                long endTime = Stopwatch.GetTimestamp();
                long vertexTicks = calculationTicks + verticesEnd - rebuilt;
                ComputeTicks += vertexTicks; RebuildTicks += rebuilt - computed; UploadTicks += endTime - verticesEnd; Samples++;
                long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
                bool transition = TransitionPending || NewRuns != 0 || VertexCount != previousVertices;
                TransitionPending = false;
                if (transition) TransitionAllocated += delta; else StableAllocated += delta;
                if (Samples == 1 || transition || Samples % 120 == 0)
                {
                    Log("cost frame=" + Frame + " vertex_ms=" + Ms(vertexTicks) + " upload_ms=" + Ms(endTime - verticesEnd)
                        + " runs_ms=" + Ms(rebuilt - computed - (atlasEnd - atlasStart)) + " runs=" + Runs.Count + " owned=" + Owned + " live=" + LiveImages
                        + " new_runs=" + NewRuns + " reused_runs=" + ReusedRuns + " heap_delta=" + delta + " stable_alloc_bytes=" + StableAllocated
                        + " transition_alloc_bytes=" + TransitionAllocated + " atlas_pages=" + Atlas.Pages + " atlas_textures=" + Atlas.Count + " atlas_bytes=" + Atlas.Bytes
                        + " prepare_ms=" + Ms(computed - begin - calculationTicks) + " atlas_upload_ms=" + Ms(atlasEnd - atlasStart)
                        + " cpu_ms=" + Ms(endTime - begin) + " changed_members=" + ChangedMembers + " uploaded_runs=" + UploadedRuns
                        + " sample=" + (transition ? "transition" : "stable"));
                    Log("vertexcheck max_err=" + VertexMax.ToString("R", CultureInfo.InvariantCulture) + " count=" + VertexCount + " excluded=" + VertexRejected + " threshold=0.0001");
                    foreach (var why in Exclusions) if (why.Value != 0) Log("remaining_reason=" + why.Key + " count=" + why.Value);
                }
            }
            void Flush(MeshRow first)
            {
                if (first == null || Segment.Count == 0) return;
                Run match = null;
                foreach (var r in Runs)
                {
                    if (r.Members.Length != Segment.Count || r.Material != first.Material || r.Page != first.Member.Atlas.Page
                        || r.Signature != first.Member.Signature || r.Layer != first.R.sortingLayerID || r.Order != first.R.sortingOrder
                        || r.UnityLayer != first.R.gameObject.layer) continue;
                    bool equal = true; for (int i = 0; i < Segment.Count; i++) if (r.Members[i] != Segment[i]) { equal = false; break; }
                    if (equal) { match = r; break; }
                }
                if (match == null)
                {
                    match = new Run { Members = Segment.ToArray(), Material = first.Material, Signature = first.Member.Signature, Page = first.Member.Atlas.Page,
                        Layer = first.R.sortingLayerID, Order = first.R.sortingOrder, UnityLayer = first.R.gameObject.layer };
                    NextRuns.Add(match); // exception-safe ownership, including partially built run
                    match.Object = new GameObject("StutterFix.LiveDecoRun") { hideFlags = HideFlags.HideAndDontSave, layer = match.UnityLayer };
                    match.Mesh = new Mesh { name = "StutterFix.LiveDecoRun", hideFlags = HideFlags.HideAndDontSave };
                    match.Mesh.MarkDynamic();
                    match.Mesh.SetVertexBufferParams(Segment.Count * 4, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4), new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
                    var indices = new int[Segment.Count * 6];
                    for (int i = 0; i < Segment.Count; i++) for (int k = 0; k < 6; k++) indices[i * 6 + k] = i * 4 + Segment[i].Triangles[k];
                    match.Mesh.indexFormat = IndexFormat.UInt32; match.Mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
                    match.Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
                    match.Object.AddComponent<MeshFilter>().sharedMesh = match.Mesh;
                    match.Renderer = match.Object.AddComponent<MeshRenderer>(); match.Renderer.enabled = false;
                    match.Renderer.sharedMaterial = match.Material; match.Renderer.sortingLayerID = match.Layer; match.Renderer.sortingOrder = match.Order;
                    match.Renderer.shadowCastingMode = ShadowCastingMode.Off; match.Renderer.receiveShadows = false;
                    match.Renderer.lightProbeUsage = LightProbeUsage.Off; match.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    var block = new MaterialPropertyBlock(); block.SetTexture("_MainTex", match.Page.Texture);
                    block.SetColor("_RendererColor", Color.white); block.SetVector("_Flip", new Vector4(1, 1, 1, 1)); block.SetFloat("_EnableExternalAlpha", 0);
                    match.Renderer.SetPropertyBlock(block); NewRuns++;
                }
                else { NextRuns.Add(match); ReusedRuns++; }
                float z = first.Member.Matrix.MultiplyPoint3x4(first.Member.SpriteCenter).z;
                if (match.Dirty || !match.Z.Equals(z))
                { match.Z = z; match.Dirty = true; match.Object.transform.position = new Vector3(0, 0, match.Z); }
                Segment.Clear();
            }
            public void Dispose()
            {
                Disposing = true;
                Camera.onPreCull -= RuntimePreCull; SceneManager.activeSceneChanged -= RuntimeSceneChanged;
                try { Harmony?.UnpatchAll(RuntimeId); } catch (Exception e) { Log("unpatch_error=" + e.GetType().Name); }
                foreach (var run in Runs) if (run.Renderer) run.Renderer.enabled = false;
                foreach (var run in NextRuns) if (run.Renderer) run.Renderer.enabled = false;
                foreach (var m in Members.Values)
                    if (m.Owned) { try { Release(m); } catch (Exception e) { Log("restore_error=" + e.GetType().Name); } }
                foreach (var run in Runs) DestroyRun(run);
                foreach (var run in NextRuns) if (!Runs.Contains(run)) DestroyRun(run);
                Runs.Clear(); NextRuns.Clear(); Members.Clear(); Images.Clear(); AdmissionOrder.Clear(); Rows.Clear(); CachedRows.Clear(); Vertices = Array.Empty<Vertex>();
                Atlas.Dispose(); TightLate.Invalidate();
            }
        }
        static void DestroyRun(Run r)
        { if (r.Renderer) r.Renderer.enabled = false; if (r.Object) UnityEngine.Object.Destroy(r.Object); if (r.Mesh) UnityEngine.Object.Destroy(r.Mesh); }
        static string Ms(long ticks) => (1000.0 * ticks / Stopwatch.Frequency).ToString("F4", CultureInfo.InvariantCulture);
        static string HierarchyReason(LiveDecoHierarchy.Failure failure)
        {
            switch (failure)
            {
                case LiveDecoHierarchy.Failure.MissingNode: return "child-hierarchy-missing";
                case LiveDecoHierarchy.Failure.ChildIsPivot: return "child-hierarchy-child-is-pivot";
                case LiveDecoHierarchy.Failure.RendererOutsideChild: return "child-hierarchy-renderer-outside-child";
                case LiveDecoHierarchy.Failure.DepthLimit: return "child-hierarchy-depth-limit";
                default: return "child-hierarchy-pivot-not-ancestor";
            }
        }
        internal static bool SkipTransform(scrDecoration d)
        {
            var s = runtime;
            if (s == null || s.Disposing || (object)d == null || !s.Members.TryGetValue(d, out var m) || !m.Owned) return false;
            // Per-frame native/hierarchy validation runs in Tick. Structural game
            // setters release first. Do not repeat dozens of native getters for
            // every animation channel; these mutable reader flags are managed.
            if (SceneReset.Resetting || !ReferenceEquals(d.manager, s.Manager) || d.hitbox != 0 || d.stickToFloor || d.canHitPlanets)
            { s.Release(m); return false; }
            return true;
        }
        internal static bool Owns(scrDecoration d) => runtime != null && !runtime.Disposing && (object)d != null
            && runtime.Members.TryGetValue(d, out var m) && m.Owned;
        public static bool PositionPrefix(scrDecoration __instance) => !SkipTransform(__instance);
        public static bool RotationPrefix(scrDecoration __instance, float __0)
        { if (!SkipTransform(__instance)) return true; __instance.rotAngle = __0; return false; }
        public static bool ScalePrefix(scrDecoration __instance, Vector2 __0)
        { if (!SkipTransform(__instance)) return true; __instance.scaleVec = __0; return false; }
        internal static bool TryPosition(scrDecoration d, Vector2 position, Vector2 offset)
        {
            if (!SkipTransform(d)) return false;
            d.pivotPosVec = position; d.pivotOffsetVec = offset;
            runtime.Members[d].ChildPosition = (offset + d.croppedPivotOffset) / d.trueScaleMultiplier;
            Vector2 screen = position + offset;
            if (d.placementType == DecPlacementType.CameraAspect) screen.x *= (float)Screen.height / Screen.width;
            d.parallax.screenRelativePos = screen / 20f + new Vector2(0.5f, 0.5f);
            return true;
        }
        internal static bool TryColor(scrDecoration d, Color c, float opacity, bool onlyOpacity)
        {
            if (!SkipTransform(d)) return false;
            if (onlyOpacity) d.opacity = opacity; else d.color = c;
            UpdateLogicalColor(d); return true;
        }
        static void UpdateLogicalColor(scrDecoration d)
        { var c = d.color; c.a = c.a * d.opacity * 1f; RenderedColor(d) = c; }
        internal static bool TryRotation(scrDecoration d, float value)
        { if (!SkipTransform(d)) return false; d.rotAngle = value; return true; }
        internal static bool TryScale(scrDecoration d, Vector2 value)
        { if (!SkipTransform(d)) return false; d.scaleVec = value; return true; }
        internal static bool TryParallaxOffset(scrDecoration d, float value, bool x)
        { if (!SkipTransform(d)) return false; var v = d.parallaxOffset; if (x) v.x = value; else v.y = value; d.parallaxOffset = v; return true; }
        public static bool SetPositionPrefix(scrDecoration __instance, Vector2 __0, Vector2 __1) => !TryPosition(__instance, __0, __1);
        public static bool ColorPrefix(scrDecoration __instance, Color __0) => !TryColor(__instance, __0, 0, false);
        public static bool OpacityPrefix(scrDecoration __instance, float __0) => !TryColor(__instance, default, __0, true);
        public static bool ApplyColorPrefix(scrVisualDecoration __instance)
        { if (!SkipTransform(__instance)) return true; UpdateLogicalColor(__instance); return false; }
        public static bool ParallaxPrefix(scrParallax __instance) => !SkipTransform(__instance.decoration);
        public static void StructuralPrefix(scrDecoration __instance)
        { if (runtime != null && runtime.Members.TryGetValue(__instance, out var m)) { runtime.Release(m); m.Rejected = null; } }
        static void RuntimePreCull(Camera camera)
        {
            var s = runtime; if (s == null || s.Disposing || camera != s.Camera || s.Frame == Time.frameCount) return;
            try { s.Tick(); } catch (Exception e) { runtimeFailure = e.GetType().Name + ":" + e.Message; StopRuntime(); Gate2Error(runtimeFailure); Log("runtime_error=" + runtimeFailure); }
        }
        static void RuntimeSceneChanged(Scene a, Scene b) => StopRuntime();
        static void StartRuntime()
        {
            if (runtime != null) return;
            if ((FrameCompare.Paused ? !FrameCompare.FrozenPlayback : !Hitch.Playing) || SceneReset.Resetting || MatShare.Active || GpuSplit.Busy || GpuHide.Busy || DecoEngine.Enabled)
                throw new InvalidOperationException("livedeco runtime requires playback/pause with other render experiments off");
            runtimeFailure = "none"; runtime = new Runtime();
            long begin = Stopwatch.GetTimestamp();
            try { runtime.Begin(); Log("startup version=6 ms=" + Ms(Stopwatch.GetTimestamp() - begin) + " frozen=" + FrameCompare.FrozenPlayback); }
            catch (Exception e) { runtimeFailure = e.GetType().Name + ":" + e.Message; StopRuntime(); Gate2Error(runtimeFailure); throw; }
        }
        static void StopRuntime()
        { var s = runtime; if (s == null) return; runtime = null; s.Dispose(); Log("on=False released=1 reason=" + runtimeFailure); }
        static string RuntimeState() => "on=" + (runtime != null) + " gate=2 variant=B owned=" + (runtime?.Owned ?? 0) + " runs=" + (runtime?.Runs.Count ?? 0)
            + " failure=" + runtimeFailure + " pixel_equivalence=UNVERIFIED default_off=1";
    }
}
#endif
