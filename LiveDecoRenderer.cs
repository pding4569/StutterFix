#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // No atlas, material clone, texture mutation or DrawMesh submission. Scene
    // MeshRenderers borrow the first member's exact material identity.
    internal static class LiveDecoRenderer
    {
        const string PatchId = "StutterFix.livedeco";
        const float Limit = 1e-4f, Extent = 1000000f;
        const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly AccessTools.FieldRef<scrParallax, bool> Inited = AccessTools.FieldRefAccess<scrParallax, bool>("inited");
        static readonly AccessTools.FieldRef<scrParallax, bool> IsDecoration = AccessTools.FieldRefAccess<scrParallax, bool>("isDecoration");
        static readonly AccessTools.FieldRef<scrParallax, Transform> Cached = AccessTools.FieldRefAccess<scrParallax, Transform>("cachedTransform");
        static readonly AccessTools.FieldRef<scrParallax, Vector2> Start = AccessTools.FieldRefAccess<scrParallax, Vector2>("startPosition");
        static readonly AccessTools.FieldRef<scrParallax, Vector2> End = AccessTools.FieldRefAccess<scrParallax, Vector2>("endPosition");
        static readonly AccessTools.FieldRef<scrVisualDecoration, bool> MeshOn = AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> Blend = AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        static readonly AccessTools.FieldRef<scrDecoration, Color> Rendered = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        [StructLayout(LayoutKind.Sequential)]
        internal struct Vertex { internal Vector3 Position; internal Color Color; internal Vector2 UV; }
        sealed class Image
        {
            internal scrVisualDecoration D;
            internal SpriteRenderer R;
            internal Sprite Sprite;
            internal Material Material;
            internal Shader Shader;
            internal Texture Texture;
            internal int TextureId;
            internal string Signature;
            internal Vector2[] Corners, UV;
            internal ushort[] Indices;
            internal Vector3[] World => Geometry.World;
            internal LiveDecoGeometry Geometry;
            internal Color UploadedColor;
            internal bool VertexDirty = true;
            internal Bounds PredictedBounds;
            internal bool FlipX, FlipY;
            internal bool Owned, Rejected, OriginalOff, Hidden;
            internal bool WasLive;
            internal int Version;
            internal string Reason;
            internal Renderer[] Dependents;
            internal MonoBehaviour[] Scripts;
        }
        sealed class Row
        {
            internal Renderer R;
            internal Image Image;
            internal int Slot, Layer;
            internal Material Material;
            internal SceneRenderOrder.Key Key;
            internal bool Listed, Owned;
        }
        sealed class Source
        {
            internal Renderer R;
            internal Image Image;
            internal Row[] Rows = Array.Empty<Row>();
            internal bool Group;
        }
        sealed class Run
        {
            internal GameObject Object;
            internal Mesh Mesh;
            internal MeshFilter Filter;
            internal MeshRenderer Renderer;
            internal Image[] Members;
            internal int[] Versions;
            internal int Offset, VertexCount;
            internal float Z;
            internal bool Reused;
            internal bool Enabled;
            internal bool NeedsUpload = true;
        }
        static Harmony harmony;
        static Camera camera;
        static scrDecorationManager manager;
        static GameObject root;
        static bool restoring, refreshRegistry, topology;
        static int lastFrame = -1, ownedCount, frameSamples;
        static long reset;
        static long computeTicks, uploadTicks, reconcileTicks, scanTicks, uploadCalls, heapDelta;
        static long dirtyImages, skippedUploads;
        static Vector2 cameraPosition, screenOrigin, screenX, screenY;
        static float cameraAngle, cameraScale;
        static long transitionCount, restoredCount;
        static long allocatedBytes, steadyAllocatedBytes;
        static int steadySamples;
        static float checkMax, checkVertexMax, checkBoundsMax, checkQuadBoundsMax;
        static int checkCount, checkRejected, checkNested, checkVisibility;
        static string error = "none";
        static readonly List<Source> sources = new List<Source>();
        static readonly List<Row> ordered = new List<Row>();
        static readonly List<Material> materials = new List<Material>(4);
        static readonly List<Run> runs = new List<Run>();
        static readonly List<Run> nextRuns = new List<Run>();
        static Vertex[] vertexBuffer = Array.Empty<Vertex>();
        static readonly Dictionary<int, Run> byFirst = new Dictionary<int, Run>();
        static readonly Dictionary<scrDecoration, Image> images = new Dictionary<scrDecoration, Image>();
        static readonly Dictionary<int, Source> known = new Dictionary<int, Source>();
        static readonly Dictionary<string, int> rejected = new Dictionary<string, int>();
        static readonly VertexAttributeDescriptor[] Layout = {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2) };
        internal static bool Active => root && !restoring;
        internal static int Owned => ownedCount;
        static void Log(string s) => Main.Entry.Logger.Log("[livedeco] " + s);
        static string Ms(long ticks, int n = 1) => (ticks * 1000.0 / Stopwatch.Frequency / Math.Max(1, n)).ToString("F6", Inv);
        internal static string State() => "on=" + Active + " gate=2 variant=A owned=" + ownedCount
            + " runs=" + runs.Count + " registry=" + sources.Count + " transitions=" + transitionCount
            + " restored=" + restoredCount + " error=" + error + " atlas=0";

        // Exact signature selection, performed only on explicit enable/admission.
        static MethodInfo One(Type type, string name, int count)
        {
            var methods = type.GetMethods(AccessTools.all).Where(m => m.DeclaringType == type && m.Name == name
                && !m.IsStatic && !m.IsAbstract && !m.IsGenericMethod && m.GetParameters().Length == count
                && (m.CallingConvention & CallingConventions.HasThis) != 0
                && (m.CallingConvention & CallingConventions.VarArgs) == 0).ToArray();
            if (methods.Length != 1) throw new MissingMethodException(type.Name + "." + name + " count=" + methods.Length);
            return methods[0];
        }
        static void Hook(Type type, string name, int count, string prefix)
            => harmony.Patch(One(type, name, count), prefix: new HarmonyMethod(typeof(LiveDecoRenderer), prefix) { priority = Priority.First });
        internal static void Begin()
        {
            if (Active) return;
            if (SceneReset.Resetting || (!Hitch.Playing && !FrameCompare.Paused) || !ADOBase.controller || !ADOBase.customLevel
                || !ADOBase.controller.disableV15Features || DecoEngine.Enabled || MatShare.Active
                || GpuSplit.Busy || GpuHide.Busy || GpuCensus.Busy)
                throw new InvalidOperationException("livedeco requires playing legacy chart; competing diagnostics/engine off");
            if (GraphicsSettings.currentRenderPipeline != null) throw new InvalidOperationException("Built-in pipeline required");
            camera = GpuSplit.FindMain(); manager = scrDecorationManager.instance;
            if (!camera || !manager || !SortSupported() || !Identity(manager.transform))
                throw new InvalidOperationException("requires orthographic Z sorting and identity manager parent");
            if (DecoGpu.Active) throw new InvalidOperationException("competing owner");
            error = "none"; reset = SceneReset.Count; lastFrame = -1;
            try
            {
                // Refuse foreign hooks before any rendering mutation.
                foreach (var type in new[] { typeof(scrDecoration), typeof(scrVisualDecoration), typeof(scrParallax), typeof(ToggleVisible) })
                    foreach (var method in type.GetMethods(AccessTools.all))
                    {
                        if (method.DeclaringType != type || method.IsAbstract) continue;
                        var patches = Harmony.GetPatchInfo(method);
                        if (patches != null && patches.Owners.Any(id => id != Main.Entry.Info.Id
                            && !id.StartsWith("StutterFix.", StringComparison.Ordinal)))
                            throw new InvalidOperationException("foreign decoration hook: " + method.Name);
                    }
                root = new GameObject("StutterFix.LiveDeco.A") { hideFlags = HideFlags.HideAndDontSave };
                root.AddComponent<LiveDecoLifetime>();
                Register();
                harmony = new Harmony(PatchId);
                Hook(typeof(scrDecoration), "UpdatePosition", 0, nameof(PositionPrefix));
                Hook(typeof(scrDecoration), "SetPosition", 2, nameof(SetPositionPrefix));
                Hook(typeof(scrDecoration), "SetRotation", 1, nameof(RotationPrefix));
                Hook(typeof(scrDecoration), "SetScale", 1, nameof(ScalePrefix));
                Hook(typeof(scrDecoration), "SetColor", 1, nameof(ColorPrefix));
                Hook(typeof(scrDecoration), "SetOpacity", 1, nameof(OpacityPrefix));
                Hook(typeof(scrVisualDecoration), "ApplyColor", 0, nameof(ApplyColorPrefix));
                Hook(typeof(scrParallax), "SetTrans", 0, nameof(ParallaxPrefix));
                foreach (var spec in new[] { Tuple.Create(typeof(scrDecoration), "Setup", 3),
                    Tuple.Create(typeof(scrVisualDecoration), "SetSprite", 1),
                    Tuple.Create(typeof(scrVisualDecoration), "SetSprite", 2),
                    Tuple.Create(typeof(scrVisualDecoration), "SetTile", 1),
                    Tuple.Create(typeof(scrVisualDecoration), "SetBlendMode", 1),
                    Tuple.Create(typeof(scrVisualDecoration), "SetVisible", 1),
                    Tuple.Create(typeof(scrVisualDecoration), "SetMaskingType", 1),
                    Tuple.Create(typeof(scrVisualDecoration), "SetTextureScaleMultiplier", 1),
                    Tuple.Create(typeof(scrVisualDecoration), "SetDepth", 1) })
                    Hook(spec.Item1, spec.Item2, spec.Item3, nameof(BoundaryPrefix));
                Camera.onPreCull += PreCull;
                // Includes vertex check and first upload; originals hide only after a valid run exists.
                RenderFrame();
                if (ownedCount == 0) throw new InvalidOperationException("no eligible images; see rejection counts");
                lastFrame = -1; // Re-evaluate after this frame's camera LateUpdate.
                Log("begin variant=A renderer_version=5 color_format=Float32x4 vertex_stride=36 dirty_policy=exact-logical-pose-and-color bounds_policy=renderer-local-aabb visibility_policy=bound-self currentRenderPipeline=null atlas=0 material_clone=0 " + State());
                LiveDecoGate2.RendererStarted();
            }
            catch { Stop(); throw; }
        }
        static void Register()
        {
            foreach (var d in Resources.FindObjectsOfTypeAll<scrVisualDecoration>())
                if (d is scrVisualDecoration v && v && !images.ContainsKey(d) && v.spriteRenderer)
                    images.Add(d, new Image { D = v, R = v.spriteRenderer });
            var owners = new Dictionary<int, Image>();
            foreach (var i in images.Values) if (i.R) owners[i.R.GetInstanceID()] = i;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || (root && r.transform.IsChildOf(root.transform)) || known.ContainsKey(r.GetInstanceID())) continue;
                owners.TryGetValue(r.GetInstanceID(), out var i);
                var s = new Source { R = r, Image = i, Group = r.GetComponentInParent<SortingGroup>() };
                known.Add(r.GetInstanceID(), s); sources.Add(s);
            }
            refreshRegistry = false;
        }
        static bool Identity(Transform t) => t && t.position.Equals(Vector3.zero)
            && t.rotation.Equals(Quaternion.identity) && t.lossyScale.Equals(Vector3.one);
        static bool SortSupported()
        {
            if (!camera.orthographic) return false;
            var mode = camera.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Perspective) return false;
            Vector3 axis = mode == TransparencySortMode.CustomAxis
                ? (camera.transparencySortMode == TransparencySortMode.CustomAxis ? camera.transparencySortAxis : GraphicsSettings.transparencySortAxis)
                : camera.transform.forward;
            return axis.x == 0 && axis.y == 0 && axis.z != 0;
        }
        static bool Live(Source s)
        {
            var r = s.R;
            return r && r.enabled && r.gameObject.activeInHierarchy && r.gameObject.scene.IsValid()
                && ((camera.cullingMask & (1 << r.gameObject.layer)) != 0)
                && (!r.forceRenderingOff || (s.Image != null && s.Image.Owned));
        }
        static string GeometryReason(Image i)
        {
            var d = i.D; var r = i.R; var p = d.parallax;
            if (!d || !r || d.GetType() != typeof(scrVisualDecoration)) return "subclass";
            if (d.hitbox != 0 || d.isMask() || r.maskInteraction != SpriteMaskInteraction.None) return "hitbox-mask";
            if (d.followPlanet || d.stickToFloor || d.syncFloorDepth) return "parent-follow";
            if (d.repeatX != 1 || d.repeatY != 1 || r.drawMode != SpriteDrawMode.Simple) return "repeat-tile";
            if (MeshOn(d) || (d.meshRenderer && d.meshRenderer.enabled && d.meshRenderer.gameObject.activeInHierarchy)) return "mesh-mode";
            if ((int)Blend(d) != 0 || (d.blendModeEffect && d.blendModeEffect.enabled)) return "blend";
            if (!p || !Inited(p) || !IsDecoration(p) || !ReferenceEquals(p.decoration, d)
                || !ReferenceEquals(Cached(p), d.pivotTrans) || p.enabled) return "parallax-binding";
            if (!d.pivotTrans || d.pivotTrans.parent != manager.transform) return "pivot-parent";
            if (!d.childTransform) return "child-missing";
            if (i.Owned && (!i.Geometry.Matches(d.pivotTrans, d.childTransform, r.transform)
                || r.flipX != i.FlipX || r.flipY != i.FlipY)) return "child-state-changed";
            var cr = d.childTransform.localRotation;
            if (cr.x != 0 || cr.y != 0) return "nonplanar-child";
            if (!ADOBase.controller || p.cameraTransform != ADOBase.controller.camy.transform) return "camera-binding";
            if (d.cfpCache != null && d.cfpCache.Length != 0) return "component-filter";
            if (i.Dependents != null) foreach (var dependent in i.Dependents)
                if (dependent && dependent != r && dependent.enabled && dependent.gameObject.activeInHierarchy
                    && !dependent.forceRenderingOff) return "live-child-renderer";
            if ((d.selectionBordersObject && d.selectionBordersObject.activeInHierarchy)
                || (d.hitboxBordersObject && d.hitboxBordersObject.activeInHierarchy)) return "editor-borders";
            if (!r.sprite || !r.sprite.texture || r.sprite.associatedAlphaSplitTexture) return "sprite-texture";
            if (i.Owned) return LiveDecoScripts.Reason(i.Scripts, d, r);
            return null;
        }
        static void Reject(Image i, string why)
        {
            i.Rejected = true; i.Reason = why;
            rejected.TryGetValue(why, out int n); rejected[why] = n + 1;
        }
        static void Admit(Image i)
        {
            if (i.Rejected || i.Owned) return;
            string reason = GeometryReason(i);
            if (reason != null) { Reject(i, reason); return; }
            var m = i.R.sharedMaterials; // Admission/transition only, never steady-state.
            reason = LiveDeco.Reason(i.D, i.R, m, camera);
            if (reason != null) { Reject(i, reason); return; }
            // Known-name checks retain the gate-1 limitations; the pixel gate is mandatory.
            var block = new LiveDeco.BlockCensus();
            reason = block.Read(i.R, m);
            if (reason != null) { Reject(i, reason); return; }
            i.Sprite = i.R.sprite; i.Texture = i.Sprite.texture; i.Material = m[0];
            i.TextureId = i.Texture.GetInstanceID();
            i.Shader = i.Material.shader;
            i.Dependents = i.D.pivotTrans.GetComponentsInChildren<Renderer>(true);
            reason = GeometryReason(i);
            if (reason != null) { Reject(i, reason); return; }
            i.Scripts = i.D.pivotTrans.GetComponentsInChildren<MonoBehaviour>(true);
            reason = LiveDecoScripts.Reason(i.Scripts, i.D, i.R);
            if (reason != null) { Reject(i, reason); return; }
            i.Signature = string.Join(";", MatShare.Properties(i.Material).Select(x => x.Key + "=" + x.Value));
            i.Corners = i.Sprite.vertices; i.UV = i.Sprite.uv; i.Indices = i.Sprite.triangles;
            if (i.Corners.Length != 4 || i.UV.Length != 4 || i.Indices.Length != 6) { Reject(i, "nonquad"); return; }
            i.FlipX = i.R.flipX; i.FlipY = i.R.flipY;
            i.Geometry = LiveDecoGeometry.Capture(i.D.pivotTrans, i.D.childTransform,
                i.R.transform, i.Corners, i.FlipX, i.FlipY);
            if (i.Geometry == null) { Reject(i, "child-chain-unsupported"); return; }
            // Do not depend on stale engine state on re-entry.
            restoring = true;
            try { i.D.UpdatePosition(); } finally { restoring = false; }
            Compute(i, true);
            float vertexError = 0;
            for (int v = 0; v < 4; v++)
            {
                Vector3 local = i.Corners[v];
                if (i.R.flipX) local.x = -local.x;
                if (i.R.flipY) local.y = -local.y;
                vertexError = Mathf.Max(vertexError, Vector3.Distance(i.World[v], i.R.transform.TransformPoint(local)));
            }
            var bounds = new Bounds(i.World[0], Vector3.zero);
            for (int v = 1; v < 4; v++) bounds.Encapsulate(i.World[v]);
            var actualBounds = i.R.bounds;
            float quadBoundsError = Mathf.Max(Vector3.Distance(bounds.min, actualBounds.min), Vector3.Distance(bounds.max, actualBounds.max));
            float boundsError = Mathf.Max(Vector3.Distance(i.PredictedBounds.min, actualBounds.min), Vector3.Distance(i.PredictedBounds.max, actualBounds.max));
            float max = Mathf.Max(vertexError, boundsError);
            checkVertexMax = Mathf.Max(checkVertexMax, vertexError);
            checkBoundsMax = Mathf.Max(checkBoundsMax, boundsError);
            checkQuadBoundsMax = Mathf.Max(checkQuadBoundsMax, quadBoundsError);
            if (checkCount < 3) Log("vertexdetail vertex_err=" + vertexError.ToString("R", Inv)
                + " bounds_err=" + boundsError.ToString("R", Inv) + " quad_bounds_err=" + quadBoundsError.ToString("R", Inv)
                + " local_bounds_z=" + i.R.localBounds.size.z.ToString("R", Inv));
            checkCount++; checkMax = Mathf.Max(checkMax, max);
            if (!Finite(vertexError) || vertexError > Limit) { checkRejected++; Reject(i, "vertex-error"); return; }
            if (!Finite(boundsError) || boundsError > Limit) { checkRejected++; Reject(i, "bounds-error"); return; }
            if (i.Geometry.Depth > 1) checkNested++;
            foreach (var component in i.Scripts)
                if (component && component.isActiveAndEnabled && component.GetType() == typeof(ToggleVisible)) checkVisibility++;
            i.OriginalOff = i.R.forceRenderingOff; i.Owned = true; i.VertexDirty = true; i.Version++; ownedCount++; topology = true;
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static void CaptureCamera()
        {
            var camy = ADOBase.controller.camy;
            var t = camy.transform; var c = camy.camobj;
            cameraPosition = t.position;
            cameraAngle = t.rotation.eulerAngles.z;
            cameraScale = c.orthographicSize * .2f / (ADOBase.customLevel.levelData.camZoom / 100f);
            // Orthographic projection is affine. Cache the screen basis once,
            // rather than calling ScreenToWorldPoint for each owned decoration.
            Rect rect = c.pixelRect;
            screenOrigin = c.ScreenToWorldPoint(new Vector3(rect.x, rect.y, 0));
            screenX = (Vector2)c.ScreenToWorldPoint(new Vector3(rect.x + rect.width, rect.y, 0)) - screenOrigin;
            screenY = (Vector2)c.ScreenToWorldPoint(new Vector3(rect.x, rect.y + rect.height, 0)) - screenOrigin;
        }
        static void Compute(Image i, bool validateBounds = false)
        {
            var d = i.D; var p = d.parallax;
            Vector2 position = d.pivotPosVec + Vector2.zero;
            Vector3 position3 = position;
            Start(p) = position; p.posCamAtStart = position3;
            Vector2 trueScale = d.scaleMultiplier * d.textureScaleMultiplier;
            Vector2 camScale = (d.lockScale ? cameraScale : 1f) * trueScale;
            Vector2 world;
            if (p.clampToScreen)
            {
                world = screenOrigin + screenX * p.screenRelativePos.x + screenY * p.screenRelativePos.y;
            }
            else
            {
                world = (cameraPosition - (Vector2)p.posCamAtStart) * new Vector2(p.multiplier_x, p.multiplier_y) + Start(p);
                if (p.dontAlterX) world.x = position.x;
                if (p.dontAlterY) world.y = position.y;
            }
            world += d.parallaxOffset * camScale / d.textureScaleMultiplier;
            End(p) = world;
            float angle = d.startRot + d.rotAngle;
            if (d.lockRotation) angle += cameraAngle;
            Vector2 scale = d.scaleVec * camScale * (Vector2)Vector3.one;
            Vector2 offset = (d.pivotOffsetVec + d.croppedPivotOffset) / trueScale;
            bool changed = i.Geometry.UpdatePose(world, scale, offset, angle, validateBounds);
            i.VertexDirty |= changed;
            if (changed) for (int n = 0; n < 4; n++)
            {
                Vector3 v = i.World[n];
                if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z) || Mathf.Abs(v.x) > Extent || Mathf.Abs(v.y) > Extent)
                    throw new InvalidOperationException("nonfinite/out-of-fixed-bounds vertex");
            }
            if (validateBounds)
                i.PredictedBounds = i.Geometry.WorldBounds(i.R.localBounds, i.Geometry.Offset(offset),
                    new Vector3(world.x, world.y, 0), i.Geometry.Rotation, scale);
        }
        static void Release(Image i)
        {
            if (!i.Owned) return;
            i.Owned = false; ownedCount--; topology = true;
            i.Hidden = false;
            if (i.R) i.R.forceRenderingOff = i.OriginalOff;
            if (i.D && i.D.pivotTrans)
            {
                restoring = true;
                try
                {
                    // SetPosition may be lazily suppressed by InvisibleSkip. Restore
                    // the child explicitly before returning to normal bookkeeping.
                    i.D.childTransform.localPosition = (i.D.pivotOffsetVec + i.D.croppedPivotOffset) / i.D.trueScaleMultiplier;
                    ScreenClamp(i.D);
                    i.D.UpdatePosition(); i.D.SetColor(i.D.color); restoredCount++;
                }
                finally { restoring = false; }
            }
        }
        internal static bool SkipLate(scrDecoration d)
        {
            if (!Active || restoring || (object)d == null || !images.TryGetValue(d, out var i) || !i.Owned) return false;
            if (SceneReset.Resetting || !manager || !Identity(manager.transform) || GeometryReason(i) != null)
            { Release(i); return false; }
            return true;
        }
        public static bool PositionPrefix(scrDecoration __instance) => !SkipLate(__instance);
        static bool Owns(scrDecoration d) => Active && !restoring && (object)d != null
            && images.TryGetValue(d, out var i) && i.Owned;
        static void ScreenClamp(scrDecoration d)
        {
            var p = d.parallax;
            p.clampToScreen = d.placementType == DecPlacementType.Camera || d.placementType == DecPlacementType.CameraAspect;
            Vector2 v = d.pivotPosVec + d.pivotOffsetVec;
            if (d.placementType == DecPlacementType.CameraAspect) v.x *= (float)Screen.height / (float)Screen.width;
            p.screenRelativePos = v / 20f + new Vector2(.5f, .5f);
        }
        public static bool SetPositionPrefix(scrDecoration __instance, Vector2 __0, Vector2 __1)
        {
            if (!Owns(__instance)) return true;
            if (!__instance.pivotPosVec.Equals(__0) || !__instance.pivotOffsetVec.Equals(__1)) images[__instance].VertexDirty = true;
            __instance.pivotPosVec = __0; __instance.pivotOffsetVec = __1; ScreenClamp(__instance); return false;
        }
        internal static bool TryLogicalColor(scrDecoration d, Color color, float opacity, bool onlyOpacity)
        {
            if (!Owns(d)) return false;
            if (onlyOpacity) d.opacity = opacity; else d.color = color;
            RefreshColor(d);
            if (Rendered(d).a <= 0) Release(images[d]); // Return hidden-state bookkeeping to InvisibleSkip/Dormancy.
            return true;
        }
        static void RefreshColor(scrDecoration d)
        {
            var color = d.color.WithAlpha(d.color.a * d.opacity * 1f);
            if (!Rendered(d).Equals(color)) images[d].VertexDirty = true;
            Rendered(d) = color;
        }
        public static bool ColorPrefix(scrDecoration __instance, Color __0) => !TryLogicalColor(__instance, __0, 0, false);
        public static bool OpacityPrefix(scrDecoration __instance, float __0) => !TryLogicalColor(__instance, default(Color), __0, true);
        public static bool ApplyColorPrefix(scrVisualDecoration __instance)
        { if (!Owns(__instance)) return true; RefreshColor(__instance); return false; }
        public static bool RotationPrefix(scrDecoration __instance, float __0)
        {
            if (!SkipLate(__instance)) return true;
            if (!__instance.rotAngle.Equals(__0)) images[__instance].VertexDirty = true;
            __instance.rotAngle = __0; return false;
        }
        public static bool ScalePrefix(scrDecoration __instance, Vector2 __0)
        {
            if (!SkipLate(__instance)) return true;
            if (!__instance.scaleVec.Equals(__0)) images[__instance].VertexDirty = true;
            __instance.scaleVec = __0; return false;
        }
        public static bool ParallaxPrefix(scrParallax __instance) => !SkipLate(__instance.decoration);
        public static void BoundaryPrefix(scrDecoration __instance)
        {
            if (!Active || restoring) return;
            if (images.TryGetValue(__instance, out var i)) { Release(i); i.Rejected = false; }
            else refreshRegistry = true;
        }
        static void PreCull(Camera c)
        {
            if (!Active || c != camera || lastFrame == Time.frameCount) return;
            try { RenderFrame(); }
            catch (Exception e)
            {
                error = e.GetType().Name + ":" + e.Message;
                LiveDecoGate2.Fail("renderer:" + error); Log("ERROR " + error); Stop();
            }
        }
        static void Remove(Row row)
        { if (row.Listed) { ordered.Remove(row); row.Listed = false; topology = true; } }
        static void Insert(Row row)
        {
            int lo = 0, hi = ordered.Count;
            while (lo < hi) { int mid = (lo + hi) / 2; if (ordered[mid].Key.CompareTo(row.Key) < 0) lo = mid + 1; else hi = mid; }
            ordered.Insert(lo, row); row.Listed = true; topology = true;
        }
        static void Scan()
        {
            foreach (var s in sources)
            {
                bool live = Live(s); var image = s.Image;
                if (image != null)
                {
                    if (live && !image.WasLive) image.Rejected = false;
                    image.WasLive = live;
                    if (!live) Release(image);
                    else
                    {
                        if (image.Owned && (GeometryReason(image) != null || image.R.sprite != image.Sprite || image.R.sharedMaterial != image.Material
                            || !image.Material || image.Material.shader != image.Shader || image.Material.renderQueue != 3000 || image.Material.passCount != 1))
                        { Release(image); image.Rejected = false; }
                        Admit(image);
                    }
                }
                if (!live) { foreach (var old in s.Rows) Remove(old); continue; }
                if (s.Group) throw new InvalidOperationException("live SortingGroup not supported");
                materials.Clear(); s.R.GetSharedMaterials(materials);
                if (s.Rows.Length != Math.Max(1, materials.Count))
                {
                    foreach (var old in s.Rows) Remove(old);
                    s.Rows = new Row[Math.Max(1, materials.Count)];
                    for (int n = 0; n < s.Rows.Length; n++) s.Rows[n] = new Row { R = s.R, Image = image, Slot = n };
                }
                foreach (var row in s.Rows)
                {
                    var material = row.Slot < materials.Count ? materials[row.Slot] : null;
                    if (!material || material.renderQueue <= 2500) throw new InvalidOperationException("unsupported/missing live material");
                    var key = image != null && image.Owned ? new SceneRenderOrder.Key(
                        SortingLayer.GetLayerValueFromID(s.R.sortingLayerID), s.R.sortingOrder, material.renderQueue,
                        0, material.GetInstanceID(), s.R.GetInstanceID(), row.Slot) : MatShare.SortKey(s.R, material, camera, row.Slot);
                    if (image != null && image.Owned)
                    {
                        // The supported sorting axis is Z only; sprite/mesh centers have identical Z.
                        var z = image.World[0].z;
                        var mode = camera.transparencySortMode;
                        if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
                        var axis = mode == TransparencySortMode.CustomAxis
                            ? (camera.transparencySortMode == TransparencySortMode.CustomAxis ? camera.transparencySortAxis : GraphicsSettings.transparencySortAxis)
                            : camera.transform.forward;
                        float distance = mode == TransparencySortMode.CustomAxis ? z * axis.z : (z - camera.transform.position.z) * axis.z;
                        key = new SceneRenderOrder.Key(key.Layer, key.Order, key.Queue, distance, key.Material, key.Renderer, key.Slot);
                    }
                    bool owned = image != null && image.Owned;
                    int layer = s.R.gameObject.layer;
                    if (row.Listed && row.Key.CompareTo(key) == 0 && row.Owned == owned && row.Layer == layer && row.Material == material) continue;
                    Remove(row); row.Key = key; row.Material = material; row.Layer = layer; row.Owned = owned; Insert(row);
                }
            }
            // Same-material ties have no proven renderer-ID rule, even within an
            // owned run: exclude the whole tie rather than invent an order.
            for (int a = 0; a < ordered.Count;)
            {
                int b = a + 1;
                while (b < ordered.Count && ordered[a].Key.SameMaterialTie(ordered[b].Key)) b++;
                if (b - a > 1) for (int n = a; n < b; n++) if (ordered[n].Owned)
                { var i = ordered[n].Image; Release(i); Reject(i, "material-tie"); ordered[n].Owned = false; }
                a = b;
            }
        }
        static bool Together(Row a, Row b) => a.Owned && b.Owned && LiveDecoPolicy.CanJoin(
            new LiveDecoPolicy.Item(a.Key, true, a.Image.Signature, a.Layer, a.Image.TextureId),
            new LiveDecoPolicy.Item(b.Key, true, b.Image.Signature, b.Layer, b.Image.TextureId), true);
        static void Reconcile()
        {
            if (!topology) return;
            long begin = Stopwatch.GetTimestamp(); int created = 0, reused = 0, disposed = 0;
            byFirst.Clear(); nextRuns.Clear();
            foreach (var run in runs) { run.Reused = false; byFirst.Add(run.Members[0].R.GetInstanceID(), run); }
            for (int a = 0; a < ordered.Count;)
            {
                if (!ordered[a].Owned) { a++; continue; }
                int b = a + 1;
                while (b < ordered.Count && Together(ordered[b - 1], ordered[b])) b++;
                byFirst.TryGetValue(ordered[a].R.GetInstanceID(), out var run);
                bool same = run != null && run.Members.Length == b - a
                    && run.Renderer.sharedMaterial == ordered[a].Material && run.Renderer.sortingLayerID == ordered[a].R.sortingLayerID
                    && run.Renderer.sortingOrder == ordered[a].R.sortingOrder && run.Object.layer == ordered[a].Layer;
                if (same) for (int n = a; n < b; n++) if (!ReferenceEquals(run.Members[n - a], ordered[n].Image)
                    || run.Versions[n - a] != ordered[n].Image.Version) { same = false; break; }
                if (same) { run.Reused = true; reused++; }
                else { run = CreateRun(a, b); created++; }
                nextRuns.Add(run); a = b;
            }
            foreach (var run in runs) if (!run.Reused) { Dispose(run); disposed++; }
            runs.Clear(); runs.AddRange(nextRuns); nextRuns.Clear(); topology = false; transitionCount++;
            int vertices = 0;
            foreach (var run in runs)
            { run.NeedsUpload |= run.Offset != vertices; run.Offset = vertices; vertices += run.VertexCount; }
            if (vertexBuffer.Length < vertices) Array.Resize(ref vertexBuffer, Math.Max(vertices, vertexBuffer.Length * 2));
            long ticks = Stopwatch.GetTimestamp() - begin; reconcileTicks += ticks;
            Log("transition frame=" + Time.frameCount + " owned=" + ownedCount + " runs=" + runs.Count
                + " created=" + created + " reused=" + reused + " destroyed=" + disposed + " reconcile_ms=" + Ms(ticks));
        }
        static Run CreateRun(int a, int b)
        {
            var first = ordered[a]; var run = new Run();
            try
            {
            // Put every partially-created resource under the root immediately.
            run.Object = new GameObject("LiveDeco.Run") { hideFlags = HideFlags.HideAndDontSave, layer = first.Layer };
            run.Object.transform.SetParent(root.transform, false);
            run.Renderer = run.Object.AddComponent<MeshRenderer>(); run.Renderer.enabled = false;
            run.Mesh = new Mesh { name = "LiveDeco.A", hideFlags = HideFlags.HideAndDontSave };
            run.Filter = run.Object.AddComponent<MeshFilter>(); run.Filter.sharedMesh = run.Mesh;
            run.Members = new Image[b - a]; run.Versions = new int[b - a]; run.VertexCount = (b - a) * 4;
            var indices = new int[(b - a) * 6];
            for (int n = a; n < b; n++)
            {
                var i = ordered[n].Image; run.Members[n - a] = i; run.Versions[n - a] = i.Version;
                for (int k = 0; k < 6; k++) indices[(n - a) * 6 + k] = (n - a) * 4 + i.Indices[k];
            }
            run.Mesh.indexFormat = run.VertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            run.Mesh.MarkDynamic(); run.Mesh.SetVertexBufferParams(run.VertexCount, Layout);
            if (Marshal.SizeOf(typeof(Vertex)) != 36 || run.Mesh.GetVertexBufferStride(0) != 36
                || run.Mesh.GetVertexAttributeFormat(VertexAttribute.Color) != VertexAttributeFormat.Float32)
                throw new InvalidOperationException("Float32 color vertex layout mismatch");
            run.Mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            run.Mesh.bounds = new Bounds(Vector3.zero, new Vector3(Extent * 2, Extent * 2, 0));
            run.Renderer.sharedMaterial = first.Material;
            run.Renderer.sortingLayerID = first.R.sortingLayerID; run.Renderer.sortingOrder = first.R.sortingOrder;
            run.Renderer.shadowCastingMode = ShadowCastingMode.Off; run.Renderer.receiveShadows = false;
            run.Renderer.lightProbeUsage = LightProbeUsage.Off; run.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var props = new MaterialPropertyBlock();
            props.SetTexture("_MainTex", first.Image.Texture);
            props.SetColor("_RendererColor", Color.white); props.SetVector("_Flip", new Vector4(1, 1, 1, 1));
            run.Renderer.SetPropertyBlock(props);
            return run;
            }
            catch { Dispose(run); throw; }
        }
        static void Dispose(Run run)
        {
            if (run.Renderer) run.Renderer.enabled = false;
            if (run.Filter) run.Filter.sharedMesh = null;
            if (run.Mesh) UnityEngine.Object.Destroy(run.Mesh);
            if (run.Object) UnityEngine.Object.Destroy(run.Object);
        }
        static void RenderFrame()
        {
            if (!manager || reset != SceneReset.Count || SceneReset.Resetting || (!Hitch.Playing && !FrameCompare.Paused))
                throw new InvalidOperationException("playback/lifecycle changed");
            if (!SortSupported() || !Identity(manager.transform)) throw new InvalidOperationException("sort/parent changed");
            if (DecoGpu.Active || MatShare.Active || DecoEngine.Enabled) throw new InvalidOperationException("competing owner enabled");
            lastFrame = Time.frameCount;
            long heap = GC.GetTotalMemory(false), alloc = GC.GetAllocatedBytesForCurrentThread(), transitions = transitionCount;
            checkMax = checkVertexMax = checkBoundsMax = checkQuadBoundsMax = 0;
            checkCount = checkRejected = checkNested = checkVisibility = 0;
            long compute = Stopwatch.GetTimestamp();
            CaptureCamera();
            computeTicks += Stopwatch.GetTimestamp() - compute;
            long scan = Stopwatch.GetTimestamp();
            if (refreshRegistry) Register();
            Scan(); scanTicks += Stopwatch.GetTimestamp() - scan;
            Reconcile();
            compute = Stopwatch.GetTimestamp();
            foreach (var run in runs)
            {
                float z = run.Members[0].World[0].z;
                bool depthChanged = run.Z != z;
                if (depthChanged) { run.Z = z; run.Object.transform.position = new Vector3(0, 0, run.Z); }
                var uploadState = new LiveDecoUploadState(run.NeedsUpload || depthChanged);
                for (int n = 0; n < run.Members.Length; n++)
                {
                    var i = run.Members[n]; Compute(i); Color color = Rendered(i.D);
                    if (!uploadState.Fill(i.VertexDirty, !color.Equals(i.UploadedColor))) continue;
                    dirtyImages++;
                    for (int k = 0; k < 4; k++)
                    {
                        var v = i.World[k]; v.z -= run.Z;
                        vertexBuffer[run.Offset + n * 4 + k] = new Vertex { Position = v, Color = color, UV = i.UV[k] };
                    }
                    i.UploadedColor = color; i.VertexDirty = false;
                    // Do not use NeedsUpload here as the per-image fill predicate:
                    // one dirty image need not cause later unchanged images to fill.
                }
                run.NeedsUpload = uploadState.Dirty;
            }
            computeTicks += Stopwatch.GetTimestamp() - compute;
            long upload = Stopwatch.GetTimestamp();
            foreach (var run in runs)
            {
                if (!run.NeedsUpload) { skippedUploads++; continue; }
                run.Mesh.SetVertexBufferData(vertexBuffer, run.Offset, 0, run.VertexCount, 0, Flags);
                run.NeedsUpload = false;
                if (!run.Enabled) { run.Renderer.enabled = true; run.Enabled = true; }
                uploadCalls++;
            }
            foreach (var run in runs) foreach (var i in run.Members)
                if (!i.Hidden) { i.R.forceRenderingOff = true; i.Hidden = true; }
            uploadTicks += Stopwatch.GetTimestamp() - upload;
            heapDelta += GC.GetTotalMemory(false) - heap; frameSamples++;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - alloc; allocatedBytes += bytes;
            if (transitionCount == transitions && checkCount == 0) { steadyAllocatedBytes += bytes; steadySamples++; }
            if (checkCount != 0) Log("vertexcheck max_err=" + checkMax.ToString("R", Inv) + " count=" + checkCount
                + " excluded=" + checkRejected + " nested_admitted=" + checkNested
                + " visibility_admitted=" + checkVisibility + " threshold=0.0001"
                + " vertex_max_err=" + checkVertexMax.ToString("R", Inv) + " bounds_max_err=" + checkBoundsMax.ToString("R", Inv)
                + " quad_bounds_max_err=" + checkQuadBoundsMax.ToString("R", Inv) + " bounds_source=renderer-local-aabb");
            if (frameSamples >= 120 || checkCount != 0) ReportCost();
            if (rejected.Count != 0 && frameSamples == 0)
            { foreach (var p in rejected) Log("excluded reason=" + p.Key + " count=" + p.Value); rejected.Clear(); }
        }
        static void ReportCost()
        {
            if (frameSamples == 0) return;
            Log("cost per_frame=1 samples=" + frameSamples + " owned=" + ownedCount + " runs=" + runs.Count
                + " vertex_ms=" + Ms(computeTicks, frameSamples) + " upload_ms=" + Ms(uploadTicks, frameSamples)
                + " reconcile_ms=" + Ms(reconcileTicks, frameSamples) + " scan_ms=" + Ms(scanTicks, frameSamples)
                + " upload_calls=" + (uploadCalls / (double)frameSamples).ToString("F3", Inv)
                + " dirty_images=" + (dirtyImages / (double)frameSamples).ToString("F3", Inv)
                + " skipped_uploads=" + (skippedUploads / (double)frameSamples).ToString("F3", Inv)
                + " vertex_target_ms=0.5 upload_target_ms=0.4 mesh_targets_met="
                + (computeTicks * 1000.0 / Stopwatch.Frequency / frameSamples <= .5
                    && uploadTicks * 1000.0 / Stopwatch.Frequency / frameSamples <= .4)
                + " allocated_bytes=" + allocatedBytes + " steady_allocated_bytes=" + steadyAllocatedBytes + " steady_samples=" + steadySamples
                + " heap_delta=" + heapDelta + " heap_scope=frame-work-excludes-cost-and-vertex-logs");
            computeTicks = uploadTicks = reconcileTicks = scanTicks = uploadCalls = heapDelta = 0; frameSamples = 0;
            allocatedBytes = steadyAllocatedBytes = 0; steadySamples = 0;
            dirtyImages = skippedUploads = 0;
        }
        internal static void Stop()
        {
            Camera.onPreCull -= PreCull;
            if (!root && harmony == null) return;
            LiveDecoGate2.RendererStopped();
            foreach (var p in rejected) Log("excluded reason=" + p.Key + " count=" + p.Value);
            ReportCost(); restoring = true;
            if (root) root.SetActive(false);
            try
            {
                foreach (var run in runs) Dispose(run);
                // Covers a failed CreateRun before it entered runs, too.
                if (root) foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh) UnityEngine.Object.Destroy(f.sharedMesh);
                foreach (var i in images.Values)
                    try { Release(i); } catch (Exception e) { error = "restore:" + e.Message; LiveDecoGate2.Fail(error); }
            }
            finally
            {
                try { harmony?.UnpatchAll(PatchId); }
                finally
                {
                    if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
                    root = null; harmony = null; camera = null; manager = null;
                    sources.Clear(); known.Clear(); images.Clear(); ordered.Clear(); runs.Clear(); nextRuns.Clear(); byFirst.Clear();
                    materials.Clear(); rejected.Clear(); ownedCount = 0; restoring = false; topology = false;
                    vertexBuffer = Array.Empty<Vertex>();
                }
            }
        }
        internal static void Destroyed(GameObject target) { if (ReferenceEquals(root, target)) Stop(); }
    }
    internal sealed class LiveDecoLifetime : MonoBehaviour
    {
        void OnDestroy() { LiveDecoRenderer.Destroyed(gameObject); }
    }
}
#endif
