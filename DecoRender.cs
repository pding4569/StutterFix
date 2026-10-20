#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    // Explicit visual experiment: never patches decoration evaluation or rewinds animation.
    internal sealed partial class DecoRender : MonoBehaviour
    {
        static DecoRender session;
        static string lastCompare = "not run", lastStop = "none";
        static bool compareFinished, onWaiting;
        static string lastSelection = "not run", lastChanges = "none";
        static string lastOrders = "not run", lastColorFormat = "not run";
        static string lastKeys = "not run";
        static string lastRuns = "not run";
        static readonly AccessTools.FieldRef<scrVisualDecoration, bool> MeshOn =
            AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        const int Padding = 4;
        long atlasBudget = 128L * 1048576;
        const int DetailLimit = 20;
        sealed class Image
        {
            internal Texture2D Source;
            internal Atlas Atlas;
            internal int X, Y;
        }
        sealed class Atlas
        {
            internal Texture2D Texture;
            internal readonly List<Image> Images = new List<Image>();
            internal int Width, Height, Mips, X, Y, Row;
            internal bool Linear, Dedicated;
            internal string Sampler;
        }
        sealed class Item
        {
            internal scrVisualDecoration D;
            internal SpriteRenderer R;
            internal Sprite Sprite;
            internal Material Source;
            internal string Signature;
            internal Image Image;
            internal Vector2[] Vertices, UV;
            internal ushort[] Triangles;
            internal int Layer, Order, UnityLayer, Id, MaterialId;
            internal bool WasOff, Hidden, Moved;
            internal int Index;
            internal float RepeatX, RepeatY;
            internal Matrix4x4 Matrix;
            internal Color Color;
            internal bool FlipX, FlipY, Visible;
            internal bool Written, Dirty;
            internal string GroupKey;
            internal Group Run;
            internal int Slot, Start, Offset, PoseFrame = -1, ExactPoseFrame = -1;
            internal Matrix4x4 ExactPose;
            internal Matrix4x4 ChildMatrix;
            internal Transform PivotTransform, RenderTransform;
            internal bool ChildIdentity;
            internal bool NativeFallback;
            internal bool PoseValidated;
            internal bool PoseRejected;
            internal Bounds LocalBounds;
            internal MaterialSnapshot Uniforms;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct FloatVertex
        {
            internal Vector3 Position;
            internal Color Color;
            internal Vector2 UV;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct ByteVertex
        {
            internal Vector3 Position;
            internal Color32 Color;
            internal Vector2 UV;
        }
        sealed class KeySelection
        {
            internal int Total, Eligible, Renderers;
            internal string Rejected;
            internal readonly Dictionary<string, int> Reasons = new Dictionary<string, int>();
        }
        struct RenderOrder
        {
            internal int Queue, Material, Id;
            internal float Distance;
        }
        sealed class BarrierWatch
        {
            internal Renderer R;
            internal scrVisualDecoration Owner;
            internal string Key;
            internal bool Enabled, Active, ForceOff, RequiresDiscovery;
            internal int Queue, Material;
            internal float Distance;
        }
        sealed class Group
        {
            internal GameObject Object;
            internal MeshRenderer Renderer;
            internal Mesh Mesh;
            internal Material Material;
            internal Texture OriginalTexture, AtlasTexture;
            internal bool Borrowed;
            internal string RunKey;
            internal string SortKey;
            internal readonly List<Item> Items = new List<Item>();
            internal int VertexCount;
            internal readonly List<int> Indices = new List<int>();
            internal Bounds CullBounds;
            internal bool Fresh = true, HasVisible;
            internal bool BoundsDirty;
            internal readonly List<Item> VisibleItems = new List<Item>();
        }
        readonly List<Item> items = new List<Item>();
        readonly List<Atlas> atlases = new List<Atlas>();
        readonly List<Group> groups = new List<Group>();
        readonly HashSet<int> ownRenderers = new HashSet<int>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Dictionary<int, int> cameras = new Dictionary<int, int>();
        scrDecorationManager manager;
        object decorationList;
        int decorationCount;
        Camera mainCamera;
        int scene, vertexCount, pending;
        long resetCount, buildCount, buildTicks, measuredFrames;
        long frameTicks;
        double lastBuildMs;
        bool stopping, comparing, initialized, beganEditorPlayback;
        string outputDir;
        bool observing, floatColor;
        bool scopeAll = true, mergeCameras = true;
        struct CameraSortSnapshot
        {
            internal TransparencySortMode Mode;
            internal Vector3 Axis, Position;
            internal bool Orthographic;
        }
        readonly Dictionary<int, CameraSortSnapshot> preparedCameraSort = new Dictionary<int, CameraSortSnapshot>();
        readonly HashSet<int> cameraSet = new HashSet<int>();
        readonly Dictionary<int, string> rejectionReasons = new Dictionary<int, string>();
        const int RunSampleWindow = 120;
        readonly Queue<int> visibleRunSamples = new Queue<int>();
        readonly Queue<int> totalRunSamples = new Queue<int>();
        int visibleRunSampleFrame = -1, visibleRunSum, totalRunSum;
        readonly List<BarrierWatch> barrierWatches = new List<BarrierWatch>();
        readonly HashSet<string> fallbackKeys = new HashSet<string>();
        readonly Dictionary<string, int> waitingReasons = new Dictionary<string, int>();
        readonly Dictionary<string, int> breakReasons = new Dictionary<string, int>();
        Camera compareCamera;
        int preparedFrame = -1, candidateCount, changedItems, uploadedVertices;
        double computeMs, uploadMs;
        string transitionReason;
        int movingCandidates, unmanagedBreaks;
        readonly Dictionary<string, int> observationChanges = new Dictionary<string, int>();
        int observeTarget, observeFrames, observeFrame = -1, detailEvents, selected;
        long atlasBytes;
        readonly Dictionary<string, int> excluded = new Dictionary<string, int>();
        readonly Dictionary<string, KeySelection> keys = new Dictionary<string, KeySelection>();
        readonly Dictionary<string, int> excludedKeys = new Dictionary<string, int>();
        readonly Dictionary<string, int> changes = new Dictionary<string, int>();
        static void Count(Dictionary<string, int> counts, string reason)
        { int n; counts.TryGetValue(reason, out n); counts[reason] = n + 1; }
        static string Counts(Dictionary<string, int> counts)
        {
            var keys = new List<string>(counts.Keys); keys.Sort(StringComparer.Ordinal);
            var result = new StringBuilder();
            foreach (var key in keys) result.Append(key).Append('=').Append(counts[key]).Append(' ');
            return result.ToString().TrimEnd();
        }

        static void Log(string s) => Main.Entry.Logger.Log("[decorender] " + s);
        static string F(double x) => x.ToString("F6", CultureInfo.InvariantCulture);
        internal static bool Step(string arg, string modDir)
        {
            arg = arg.Trim().ToLowerInvariant();
            if (arg == "compare")
            {
                if (compareFinished) { compareFinished = false; Log(lastCompare); return true; }
                if (!session) { Log("inactive; use decorender on"); return true; }
                if (session.observing) return false;
                if (session.pending == 0) { session.pending = 1; Log("compare queued for main-camera post-render"); }
                return false; // AutoTest retries this step; only ONE comparison is submitted.
            }
            if (arg == "off") { Stop(); onWaiting = false; Log(State()); return true; }
            if (arg == "verify" || arg == "verify off")
            { if (session) session.verifySoA = arg == "verify"; Log(State()); return true; }
            if (arg == "stats reset") { if (session) session.ResetBuildStats(); Log(State()); return true; }
            if (arg == "state") { if (session) session.LogStaticTextures(); Log(State()); return true; }
            var args = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int frames = 20;
            bool floatArg = false, validArgs = true, allArg = true;
            int atlasMiB = 128;
            int option = 1;
            int parsedFrames;
            if (args.Length > 1 && int.TryParse(args[1], out parsedFrames)) { frames = parsedFrames; option = 2; }
            for (int n = option; n < args.Length; n++)
                switch (args[n])
                {
                    case "floatcolor": floatArg = true; break;
                    case "scope=all": allArg = true; break;
                    case "scope=moving": allArg = false; break;
                    default:
                        if (!args[n].StartsWith("atlas=") || !int.TryParse(args[n].Substring(6), out atlasMiB)
                            || atlasMiB < 1 || atlasMiB > 256) validArgs = false;
                        break;
                }
            if (args.Length == 0 || args[0] != "on" || !validArgs || frames < 0 || frames > 600 || (!allArg && frames == 0))
            { Log("decorender on [frames=20,0..600; moving requires 1+] [scope=all|moving] [atlas=128,1..256 MiB] [floatcolor]|off|compare|state|verify [off]|stats reset"); return true; }
            if (onWaiting)
            {
                if (session && session.observing) return false;
                onWaiting = false; Log(State()); return true;
            }
            if (session) { Log(State()); return true; }
            compareFinished = false;
            try
            {
                var go = new GameObject("StutterFix.DecoRender") { hideFlags = HideFlags.HideAndDontSave };
                session = go.AddComponent<DecoRender>();
                session.observeTarget = frames;
                session.floatColor = floatArg;
                session.scopeAll = allArg; session.atlasBudget = (long)atlasMiB * 1048576;
                session.Begin(modDir);
                onWaiting = session.observing;
                lastStop = "none";
                Log(State());
            }
            catch (Exception e) { Stop("on failed: " + e); Log(lastStop); }
            return !onWaiting;
        }

        float EffectiveFloat(Material m, string name) => block.HasFloat(name) ? block.GetFloat(name) : m.GetFloat(name);

        string Exclusion(scrVisualDecoration d)
        {
            if (!d || d.GetType() != typeof(scrVisualDecoration)) return "other";
            var r = d.spriteRenderer;
            if (d.hitbox != 0 || d.isMask() || MeshOn(d)
                || (r && (r.maskInteraction != SpriteMaskInteraction.None || r.GetComponentInParent<SortingGroup>()))) return "mask-hitbox-mesh";
            // Eligibility is independent of visibility, including inactive/force-off decorations.
            if (!r || !r.sprite) return "other";
            var m = r.sharedMaterial; var t = r.sprite.texture;
            if (!m || !m.shader || !t || r.sprite.associatedAlphaSplitTexture) return "other";
            bool tile = m.shader.name == "Sprites/Tile";
            // Explicit allowlist: names that merely start with Sprites/Default may run custom code.
            if ((!tile && m.shader.name != "Sprites/Default") || m.passCount != 1) return "shader";
            // Count point sampling separately even when another keyword is also present.
            if (Array.IndexOf(m.shaderKeywords, "USE_POINT_SAMPLING") >= 0) return "point-sampling";
            foreach (var keyword in m.shaderKeywords)
                if (tile || keyword != "PIXELSNAP_ON") return "special-keyword";
            block.Clear(); r.GetPropertyBlock(block);
            if (d.repeatX != 1 || d.repeatY != 1) return "repeat-not-one";
            if (tile)
            {
                if (!m.HasProperty("RepeatX") || !m.HasProperty("RepeatY") || !m.HasProperty("PixelSnap")) return "shader";
                if (EffectiveFloat(m, "RepeatX") != 1 || EffectiveFloat(m, "RepeatY") != 1) return "repeat-not-one";
                if (EffectiveFloat(m, "PixelSnap") != 0) return "pixel-snap";
            }
            if (!m.GetTextureScale("_MainTex").Equals(Vector2.one) || !m.GetTextureOffset("_MainTex").Equals(Vector2.zero)
                || (block.HasVector("_MainTex_ST") && !block.GetVector("_MainTex_ST").Equals(new Vector4(1, 1, 0, 0)))) return "maintex-st";
            if (t.wrapModeU != TextureWrapMode.Clamp || t.wrapModeV != TextureWrapMode.Clamp) return "wrap";
            if (t.width > 2048 || t.height > 2048) return "large-texture";
            if (r.drawMode != SpriteDrawMode.Simple) return "other";
            if (r.spriteSortPoint != SpriteSortPoint.Center) return "sprite-sort-point";
            return null;
        }

        void Begin(string modDir)
        {
            if (GraphicsSettings.currentRenderPipeline != null) throw new InvalidOperationException("Built-in RP required");
            manager = scrDecorationManager.instance;
            mainCamera = scrCamera.instance ? scrCamera.instance.camobj : Camera.main;
            if (!manager || !mainCamera || SceneReset.Resetting) throw new InvalidOperationException("decoration manager/camera not ready");
            scene = SceneManager.GetActiveScene().handle; resetCount = SceneReset.Count;
            beganEditorPlayback = scnEditor.instance && scnEditor.instance.playMode;
            decorationList = manager.allDecorations;
            decorationCount = manager.allDecorations.Count;
            foreach (var c in SceneCameras()) cameraSet.Add(c.GetInstanceID());
            outputDir = Path.Combine(modDir, "shots");
            lastSelection = "observing"; lastChanges = "none";
            lastOrders = "pending-observation";
            lastKeys = "observing";
            lastColorFormat = floatColor ? "Float32x4(explicit-buffer)" : "UNorm8x4(Color32)";
            foreach (var key in new[] { "repeat-not-one", "point-sampling", "pixel-snap", "maintex-st", "shader", "special-keyword", "wrap", "large-texture", "static", "mask-hitbox-mesh", "other", "material-unsupported", "atlas-budget", "changed-during-observation" }) { excluded[key] = 0; excludedKeys[key] = 0; }
            excludedKeys["changed"] = 0;
            foreach (var field in new[] { "sprite", "material", "sortingOrder", "sortingLayer", "mask", "other" }) observationChanges[field] = 0;
            foreach (var key in new[] { "sprite", "material", "material-properties", "sortingOrder", "sortingLayer", "mask", "destroyed", "renderer-owner", "unityLayer", "sortingGroup", "drawMode", "repeat", "wrap", "restored-items", "restored-keys" }) changes[key] = 0;
            int index = 0;
            var visuals = new HashSet<scrVisualDecoration>();
            foreach (var d in manager.allDecorations)
                if (d is scrVisualDecoration v && v) visuals.Add(v);
            // Include inactive scene decorations, even if absent from the manager's current list.
            foreach (var v in UnityEngine.Object.FindObjectsOfType<scrVisualDecoration>(true)) visuals.Add(v);
            foreach (var v in visuals)
            {
                int decorationIndex = index++;
                var r = v.spriteRenderer;
                string reason = Exclusion(v);
                if (!r)
                {
                    // Mesh-only/malformed visuals cannot enter an atlas. Block every renderer key they own.
                    foreach (var renderer in v.GetComponentsInChildren<Renderer>(true))
                        RegisterDecoration(OrderKey(renderer.sortingLayerID, renderer.sortingOrder), "mask-hitbox-mesh");
                    continue;
                }
                string signature = null;
                if (reason == null)
                    try { signature = MaterialState(r, null); }
                    catch (InvalidOperationException) { reason = "material-unsupported"; }
                RegisterDecoration(OrderKey(r.sortingLayerID, r.sortingOrder), reason);
                if (reason != null) rejectionReasons[r.GetInstanceID()] = reason;
                if (reason != null) continue;
                var sprite = r.sprite;
                items.Add(new Item { D = v, R = r, Sprite = sprite, Source = r.sharedMaterial,
                    MaterialId = r.sharedMaterial.GetInstanceID(), Signature = signature,
                    Image = new Image { Source = sprite.texture }, Index = decorationIndex,
                    Layer = r.sortingLayerID, Order = r.sortingOrder, UnityLayer = r.gameObject.layer,
                    Id = r.GetInstanceID(), WasOff = r.forceRenderingOff, RepeatX = v.repeatX, RepeatY = v.repeatY,
                    Matrix = r.transform.localToWorldMatrix, Color = r.color, FlipX = r.flipX, FlipY = r.flipY });
            }
            candidateCount = items.Count;
            foreach (var i in items) i.Uniforms = ReadUniforms(i.R);
            LogStaticTextures();
            observing = !scopeAll; initialized = true;
            Camera.onPreCull += PreCull; Camera.onPreRender += PreRender; Camera.onPostRender += PostRender;
            if (scopeAll) { FinishSelection(); return; }
            Log("observing frames=" + observeTarget + " candidates=" + items.Count + " excluded={" + Counts(excluded)
                + "}; sources remain SpriteRenderers; frames counted once per Time.frameCount, sampled every camera PreRender");
        }

        void FinishObservation()
        {
            movingCandidates = items.FindAll(i => i.Moved).Count;
            items.RemoveAll(i => { if (i.Moved) return false; Count(excluded, "static"); rejectionReasons[i.Id] = "static"; return true; });
            FinishSelection();
        }
        void LogStaticTextures()
        {
            // Same observational definition as moving scope, without delaying all-scope takeover.
            var counts = new Dictionary<Texture2D, int>();
            foreach (var i in items)
            {
                if (i.Moved) continue;
                int n; counts.TryGetValue(i.Image.Source, out n); counts[i.Image.Source] = n + 1;
            }
            var ordered = new List<Texture2D>(counts.Keys);
            ordered.Sort((a,b) => ((long)b.width * b.height).CompareTo((long)a.width * a.height));
            Log("static-textures classification=unchanged-in-sampled-frames textures=" + ordered.Count + " sampled-frames=" + (measuredFrames + observeFrames)
                + " top=20 by-source-area; initial report includes all candidates; state report uses remaining candidates");
            for (int n = 0; n < Math.Min(20, ordered.Count); n++)
            { var t = ordered[n]; Log("static-texture rank=" + (n + 1) + " id=" + t.GetInstanceID() + " size=" + t.width + "x" + t.height + " decorations=" + counts[t] + " name=" + t.name); }
        }
        void FinishSelection()
        {
            // Plan before GPU allocation, including sampler splits, padding, power-of-two slack and all mips.
            Dictionary<Texture2D, Image> images;
            while (true)
            {
                atlases.Clear(); images = new Dictionary<Texture2D, Image>();
                foreach (var item in items)
                {
                    Image image;
                    if (!images.TryGetValue(item.Sprite.texture, out image))
                    { image = new Image { Source = item.Sprite.texture }; images.Add(image.Source, image); }
                    item.Image = image;
                }
                var ordered = new List<Image>(images.Values);
                ordered.Sort((a, b) => { int c = (b.Source.width * b.Source.height).CompareTo(a.Source.width * a.Source.height);
                    return c != 0 ? c : a.Source.GetInstanceID().CompareTo(b.Source.GetInstanceID()); });
                foreach (var image in ordered) Place(image);
                atlasBytes = 0; long sourcePixels = 0;
                foreach (var image in ordered) sourcePixels += (long)image.Source.width * image.Source.height;
                foreach (var atlas in atlases) { TrimAtlas(atlas); atlasBytes += AtlasBytes(atlas); }
                if (atlasBytes <= atlasBudget) break;
                var largest = ordered[0].Source; int removed = 0;
                int before = items.Count;
                items.RemoveAll(i => {
                    if (i.Image.Source != largest) return false;
                    rejectionReasons[i.Id] = "atlas-budget"; Count(excluded, "atlas-budget");
                    Count(keys[OrderKey(i)].Reasons, "atlas-budget"); return true;
                });
                removed = before - items.Count;
                Log("budget exclude texture=" + largest.GetInstanceID() + " size=" + largest.width + "x" + largest.height
                    + " decorations=" + removed + " planned-bytes=" + atlasBytes + " source-pixels=" + sourcePixels);
            }

            // Keep the already planned page coordinates, but do not allocate unused textures/pages.
            var used = new HashSet<Image>();
            foreach (var item in items) used.Add(item.Image);
            var unused = new List<Texture2D>();
            foreach (var pair in images) if (!used.Contains(pair.Value)) unused.Add(pair.Key);
            foreach (var texture in unused) images.Remove(texture);
            atlasBytes = 0;
            for (int n = atlases.Count - 1; n >= 0; n--)
            {
                atlases[n].Images.RemoveAll(image => !used.Contains(image));
                if (atlases[n].Images.Count == 0) atlases.RemoveAt(n);
                else atlasBytes += AtlasBytes(atlases[n]);
            }
            foreach (var image in images.Values)
            {
                var t = image.Source;
                Log("texture id=" + t.GetInstanceID() + " name=" + t.name + " size=" + t.width + "x" + t.height
                    + " format=" + t.format + "/" + t.graphicsFormat + " wrap=" + t.wrapMode + " U=" + t.wrapModeU
                    + " V=" + t.wrapModeV + " filter=" + t.filterMode + " mips=" + t.mipmapCount + " readable=" + t.isReadable
                    + " aniso=" + t.anisoLevel + " bias=" + t.mipMapBias);
            }
            foreach (var item in items)
            { item.Vertices = item.Sprite.vertices; item.Triangles = item.Sprite.triangles; item.UV = item.Sprite.uv; vertexCount += item.Vertices.Length; }
            foreach (var atlas in atlases) BuildAtlas(atlas);
            TopologyCause("on"); RebuildRuns(mainCamera);
            LogTopologyBuild("full/on");
            Build(); // Initial on statistics include actual emitted geometry.
            SampleVisibleRuns();
            observing = false; selected = items.Count;
            lastSelection = "selected=" + selected + " excluded={" + Counts(excluded) + "} source-textures=" + images.Count
                + " scope=" + (scopeAll ? "all" : "moving") + " atlas-bytes=" + atlasBytes + "/" + atlasBudget;
            Log(lastSelection);
            Log("on " + RunState() + "; order=queue ascending,distance descending,signed material-ID ascending(back-to-front)"
                + "; direction-evidence=decorender-5 CSV analysis materials 50/50,49/50,50/50; raw CSV currently unavailable"
                + "; borrowed source material; vertex-color=" + lastColorFormat);

        }

        void RegisterDecoration(string key, string reason)
        {
            KeySelection selection;
            if (!keys.TryGetValue(key, out selection)) keys.Add(key, selection = new KeySelection());
            selection.Total++;
            if (reason == null) selection.Eligible++;
            else { Count(selection.Reasons, reason); Count(excluded, reason); }
        }
        static bool SourceOff(Item i) => i.Hidden ? i.WasOff : i.R.forceRenderingOff;
        static bool SourceVisible(Item i) => i.R.enabled && i.R.gameObject.activeInHierarchy
            && !SourceOff(i) && i.R.color.a > 0;
        static void HideSource(Item i)
        {
            if (!i.R || i.Hidden) return;
            i.WasOff = i.R.forceRenderingOff; i.Hidden = true; i.R.forceRenderingOff = true;
        }
        static void RestoreSource(Item i)
        { if (i.R && i.Hidden) i.R.forceRenderingOff = i.WasOff; i.Hidden = false; }
        string KeyState()
        {
            var managed = new HashSet<string>(); int visible = 0, vertices = 0, rejected = 0;
            var perKey = new Dictionary<string, int>();
            foreach (var i in items)
            {
                managed.Add(OrderKey(i));
                Count(perKey, OrderKey(i));
                if (i.R && SourceVisible(i)) visible++;
            }
            foreach (var g in groups) vertices += g.VertexCount;
            foreach (var pair in excludedKeys) rejected += pair.Value;
            var detail = new StringBuilder();
            var sorted = new List<string>(keys.Keys); sorted.Sort(StringComparer.Ordinal);
            foreach (var key in sorted)
            {
                var k = keys[key]; int count; perKey.TryGetValue(key, out count);
                detail.Append('[').Append(key).Append(":candidates=").Append(k.Eligible)
                    .Append(",renderers=").Append(k.Renderers)
                    .Append(",managed=").Append(observing ? 0 : count).Append(",reason=").Append(k.Rejected ?? "managed")
                    .Append(",reasons={").Append(Counts(k.Reasons)).Append("}]");
            }
            return "managed-keys=" + (observing ? 0 : managed.Count) + " managed-visible=" + (observing ? 0 : visible)
                + " managed-hidden=" + (observing ? 0 : items.Count - visible) + " excluded-keys=" + rejected
                + " excluded-key-reasons={" + Counts(excludedKeys) + "} emitted-vertices=" + vertices + " keys=" + detail;
        }

        static string OrderKey(int layer, int order) => layer + "/" + order;
        static string OrderKey(Item item) => OrderKey(item.Layer, item.Order);
        string GroupKey(Item i) => OrderKey(i) + "/" + atlases.IndexOf(i.Image.Atlas)
            + "/" + i.UnityLayer + "/" + i.Signature;

        static float SortDistance(Renderer r, Camera camera)
        {
            var mode = camera.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.CustomAxis)
                return Vector3.Dot(r.bounds.center, camera.transparencySortMode == TransparencySortMode.Default
                    ? GraphicsSettings.transparencySortAxis : camera.transparencySortAxis);
            return mode == TransparencySortMode.Perspective || (mode == TransparencySortMode.Default && !camera.orthographic)
                ? Vector3.Distance(r.bounds.center, camera.transform.position)
                : Vector3.Dot(r.bounds.center, camera.transform.forward); // Camera translation is common to every sort key.
        }
        static void Borrow(Group g, bool on)
        {
            if (!g.Material || g.Borrowed == on) return;
            if (on) { g.OriginalTexture = g.Material.mainTexture; g.Material.mainTexture = g.AtlasTexture; }
            else g.Material.mainTexture = g.OriginalTexture;
            g.Borrowed = on;
        }
        static void ReleaseGroup(Group g)
        {
            if (g.Renderer) g.Renderer.enabled = false;
            Borrow(g, false);
            if (g.Object) Destroy(g.Object);
            if (g.Mesh) Destroy(g.Mesh);
            // Material belongs to the game. Never destroy it.
        }
        Group CreateRun(List<Item> members, string key)
        {
            var i = members[0];
            var g = new Group { Material = i.Source, AtlasTexture = i.Image.Atlas.Texture, RunKey = key, SortKey = OrderKey(i) };
            groups.Add(g); // Failure cleanup owns the group before any allocations.
            g.Items.AddRange(members);
            g.Object = new GameObject("decorender-run") { hideFlags = HideFlags.HideAndDontSave, layer = i.UnityLayer };
            g.Renderer = g.Object.AddComponent<MeshRenderer>(); g.Renderer.enabled = false;
            ownRenderers.Add(g.Renderer.GetInstanceID()); // Keep IDs through deferred Destroy this frame.
            g.Mesh = new Mesh { name = "DecoRender run", indexFormat = IndexFormat.UInt32 };
            g.Mesh.MarkDynamic(); g.Object.AddComponent<MeshFilter>().sharedMesh = g.Mesh;
            g.Renderer.sharedMaterial = g.Material;
            g.Renderer.sortingLayerID = i.Layer; g.Renderer.sortingOrder = i.Order;
            g.Renderer.shadowCastingMode = ShadowCastingMode.Off; g.Renderer.receiveShadows = false;
            g.Renderer.lightProbeUsage = LightProbeUsage.Off; g.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Keep game material properties intact. Sprite-specific uniforms and effective MPB
            // values go on this renderer only; the material ID remains the source ID.
            i.R.GetPropertyBlock(block);
            block.SetColor("_RendererColor", Color.white); block.SetVector("_Flip", new Vector4(1, 1, 1, 1));
            block.SetTexture("_MainTex", g.AtlasTexture);
            g.Renderer.SetPropertyBlock(block);
            foreach (var member in members)
            {
                int offset = g.VertexCount;
                g.VertexCount += member.Vertices.Length;
                foreach (var t in member.Triangles) g.Indices.Add(offset + t);
            }
            UploadMesh(g); // Nonempty topology must exist before the first cull.
            return g;
        }
        bool RebuildRuns(Camera camera)
        {
            // Re-audit even hidden external renderers: they can move/change sorting or be created.
            topologyFullRebuilds++;
            orderedKeys.Clear(); orderedCandidates.Clear(); orderedState.Clear(); runsByKey.Clear();
            topologyKeys.Clear(); reorderKeys.Clear();
            var old = new List<Group>(groups);
            var all = new Dictionary<string, List<Renderer>>();
            var uses = new Dictionary<int, int>();
            var unsafeKeys = new HashSet<string>();
            var drawingMeshOwners = new HashSet<scrVisualDecoration>();
            var sceneRenderers = new List<Renderer>();
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r.gameObject.scene.IsValid() || !r.gameObject.scene.isLoaded || ownRenderers.Contains(r.GetInstanceID())) continue;
                sceneRenderers.Add(r);
                if (r is MeshRenderer && Drawing(r))
                {
                    var owner = r.GetComponentInParent<scrVisualDecoration>();
                    if (owner) drawingMeshOwners.Add(owner);
                }
                foreach (var m in r.sharedMaterials) if (m) { int n; uses.TryGetValue(m.GetInstanceID(), out n); uses[m.GetInstanceID()] = n + 1; }
                // A SortingGroup sorts at its own key, which may differ from every child key.
                if (Drawing(r)) foreach (var sg in r.GetComponentsInParent<SortingGroup>(true))
                    unsafeKeys.Add(OrderKey(sg.sortingLayerID, sg.sortingOrder));
                string key = OrderKey(r.sortingLayerID, r.sortingOrder);
                if (!keys.ContainsKey(key)) continue;
                List<Renderer> list; if (!all.TryGetValue(key, out list)) all[key] = list = new List<Renderer>();
                list.Add(r);
            }
            var candidates = new Dictionary<int, Item>();
            // A material shared with any other renderer is unsafe to lend, even outside this key.
            for (int n = items.Count - 1; n >= 0; n--)
            {
                var i = items[n]; int count;
                bool childMesh = drawingMeshOwners.Contains(i.D);
                if (childMesh || !uses.TryGetValue(i.MaterialId, out count) || count != 1)
                {
                    RestoreSource(i); items.RemoveAt(n);
                    if (i.Vertices != null) vertexCount -= i.Vertices.Length;
                    string reason = childMesh ? "decoration-child-mesh-active" : "shared-material";
                    Count(excluded, reason); rejectionReasons[i.Id] = reason; continue;
                }
                candidates[i.Id] = i;
            }
            barrierWatches.Clear();
            foreach (var r in sceneRenderers) if (!candidates.ContainsKey(r.GetInstanceID()))
                barrierWatches.Add(new BarrierWatch { R = r, Key = OrderKey(r.sortingLayerID, r.sortingOrder),
                    Owner = r.GetComponentInParent<scrVisualDecoration>(), Enabled = r.enabled,
                    RequiresDiscovery = r.sharedMaterials.Length != 1 || r.GetComponentInParent<SortingGroup>(),
                    Active = r.gameObject.activeInHierarchy, ForceOff = r.forceRenderingOff,
                    Queue = r.sharedMaterial ? r.sharedMaterial.renderQueue : int.MinValue,
                    Material = r.sharedMaterial ? r.sharedMaterial.GetInstanceID() : 0, Distance = SortDistance(r, camera) });
            IndexBarriers();
            foreach (var pair in all) {
                var ordered = new List<Renderer>(pair.Value);
                SortKeyRenderers(ordered, camera); orderedKeys[pair.Key] = ordered;
            }
            foreach (var i in items) i.GroupKey = GroupKey(i);
            var plans = new List<List<Item>>(); unmanagedBreaks = 0; breakReasons.Clear(); waitingReasons.Clear();
            foreach (var pair in all)
            {
                var list = new List<Renderer>(orderedKeys[pair.Key]); keys[pair.Key].Renderers = list.Count;
                // Managed sources remain in the order even while hidden by our takeover.
                // Waiting unmanaged renderers are watched, but cannot split a run or poison its comparator.
                list.RemoveAll(r => {
                    if (candidates.ContainsKey(r.GetInstanceID()) || Drawing(r)) return false;
                    Count(waitingReasons, BarrierReason(r)); return true;
                });
                // Unknown submesh queues or SortingGroup ownership cannot be represented by
                // this renderer-level comparator. Restore candidates individually in this key.
                if (unsafeKeys.Contains(pair.Key) || list.Exists(r => !r.sharedMaterial || r.sharedMaterials.Length != 1 || r.GetComponentInParent<SortingGroup>()))
                {
                    for (int n = items.Count - 1; n >= 0; n--) if (OrderKey(items[n]) == pair.Key)
                    {
                        var i = items[n]; RestoreSource(i); items.RemoveAt(n);
                        if (i.Vertices != null) vertexCount -= i.Vertices.Length;
                        Count(excluded, "unsupported-order"); rejectionReasons[i.Id] = "unsupported-order";
                    }
                    foreach (var r in list) if (Drawing(r)) Count(breakReasons, "unsupported-order:" + r.GetType().Name);
                    continue;
                }
                // Already sorted once above, with waiting positions retained in orderedKeys.
                List<Item> run = null; bool gap = false, hadRun = false;
                foreach (var r in list)
                {
                    Item i;
                    if (!candidates.TryGetValue(r.GetInstanceID(), out i))
                    {
                        if (hadRun) gap = true; Count(breakReasons, BarrierReason(r));
                        run = null; continue;
                    }
                    if (gap) { unmanagedBreaks++; gap = false; }
                    // Different pages, z, queues, Unity layers or effective uniforms also split a run.
                    var mode = camera.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortMode : camera.transparencySortMode;
                    var axis = mode == TransparencySortMode.CustomAxis
                        ? (camera.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortAxis : camera.transparencySortAxis)
                        : camera.transform.forward;
                    bool merge = mergeCameras && mode != TransparencySortMode.Perspective && (mode != TransparencySortMode.Default || camera.orthographic)
                        && axis.x == 0 && axis.y == 0;
                    if (!merge || run == null || run[0].GroupKey != i.GroupKey || SortDistance(run[0].R, camera) != SortDistance(r, camera))
                    { run = new List<Item>(); plans.Add(run); }
                    run.Add(i); hadRun = true;
                }
            }
            foreach (var i in items) orderedCandidates[i.R] = i;
            var used = new HashSet<Group>();
            var oldByFirst = new Dictionary<int, Group>();
            foreach (var g in old) oldByFirst[g.Items[0].Id] = g;
            foreach (var plan in plans)
            {
                string key = plan[0].GroupKey; Group match = null, g;
                if (oldByFirst.TryGetValue(plan[0].Id, out g) && g.RunKey == key && g.Items.Count == plan.Count)
                {
                    bool same = true; for (int n = 0; n < plan.Count; n++) same &= ReferenceEquals(g.Items[n], plan[n]);
                    if (same) match = g;
                }
                if (match == null) match = CreateRun(plan, key);
                used.Add(match);
            }
            foreach (var g in old) if (!used.Contains(g)) { ReleaseGroup(g); groups.Remove(g); }
            foreach (var g in groups) {
                List<Group> list;
                if (!runsByKey.TryGetValue(g.SortKey, out list)) runsByKey[g.SortKey] = list = new List<Group>();
                list.Add(g);
            }
            // Original memberships on unaffected runs and their material instances are retained.
            lastOrders = "queue ascending; distance descending; signed material-ID ascending=back-to-front; hidden managed sources included; active barriers only; awake/event live audit; merge-cameras=" + mergeCameras;
            return old.Count != groups.Count || old.Exists(g => !used.Contains(g));
        }
        static bool Drawing(Renderer r) => r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff;

        int VisibleRunCount()
        {
            int count = 0;
            foreach (var g in groups) if (g.HasVisible && !fallbackKeys.Contains(g.SortKey)) count++;
            return count;
        }
        void SampleVisibleRuns()
        {
            int count = VisibleRunCount();
            // Replace this frame's sample when a later camera triggers a fallback.
            if (visibleRunSampleFrame == Time.frameCount)
            {
                var totals = totalRunSamples.ToArray();
                totalRunSum -= totals[totals.Length - 1]; totalRunSamples.Clear();
                for (int n = 0; n < totals.Length - 1; n++) totalRunSamples.Enqueue(totals[n]);
                var values = visibleRunSamples.ToArray();
                visibleRunSum -= values[values.Length - 1];
                visibleRunSamples.Clear();
                for (int n = 0; n < values.Length - 1; n++) visibleRunSamples.Enqueue(values[n]);
            }
            else if (visibleRunSamples.Count == RunSampleWindow)
            {
                visibleRunSum -= visibleRunSamples.Dequeue(); totalRunSum -= totalRunSamples.Dequeue();
            }
            totalRunSamples.Enqueue(groups.Count); totalRunSum += groups.Count;
            visibleRunSamples.Enqueue(count); visibleRunSum += count; visibleRunSampleFrame = Time.frameCount;
        }
        string BarrierReason(Renderer r)
        {
            string reason;
            if (rejectionReasons.TryGetValue(r.GetInstanceID(), out reason)) return reason;
            var v = r.GetComponentInParent<scrVisualDecoration>();
            if (!v) return "non-decoration:" + r.GetType().Name;
            if (v.spriteRenderer != r) return r == v.hitboxRenderer ? "mask-hitbox-mesh" : "decoration-child:" + r.GetType().Name;
            return Exclusion(v) ?? "unmanaged-decoration";
        }
        string RunState()
        {
            int one = 0, small = 0, medium = 0, large = 0, huge = 0, max = 0; var managedKeys = new HashSet<string>();
            foreach (var g in groups) { int n = g.Items.Count; max = Math.Max(max, n); if (n == 1) one++; else if (n <= 5) small++; else if (n <= 20) medium++; else if (n <= 100) large++; else huge++; }
            foreach (var i in items) managedKeys.Add(OrderKey(i));
            int totalMax = 0; foreach (var value in totalRunSamples) totalMax = Math.Max(totalMax, value);
            int visibleMax = 0; foreach (var value in visibleRunSamples) visibleMax = Math.Max(visibleMax, value);
            int activeBarriers = 0, waitingBarriers = 0;
            foreach (var count in breakReasons.Values) activeBarriers += count;
            foreach (var count in waitingReasons.Values) waitingBarriers += count;
            return "scope=" + (scopeAll ? "all" : "moving") + " candidates=" + candidateCount + " moving-candidates=" + movingCandidates + " runs=" + groups.Count + " taken=" + (observing ? 0 : items.Count)
                + " average-run=" + F((double)items.Count / Math.Max(1, groups.Count))
                + " max-run=" + max + " run-sizes={1=" + one + " 2~5=" + small + " 6~20=" + medium + " 21~100=" + large + " 101+=" + huge + "} unmanaged-breaks=" + unmanagedBreaks
                + " run-mean=" + F((double)totalRunSum / Math.Max(1, totalRunSamples.Count)) + " run-max=" + totalMax
                + " visible-runs=" + VisibleRunCount() + " drawing-runs=" + VisibleRunCount()
                + " visible-run-mean=" + F((double)visibleRunSum / Math.Max(1, visibleRunSamples.Count))
                + " visible-run-max=" + visibleMax + " visible-run-samples=" + visibleRunSamples.Count + "/" + RunSampleWindow
                + " active-barriers=" + activeBarriers + " waiting-barriers=" + waitingBarriers
                + " active-barrier-renderers={" + Counts(breakReasons) + "} waiting-barrier-renderers={" + Counts(waitingReasons) + "}"
                + " fallback-keys=" + fallbackKeys.Count
                + " keys=" + managedKeys.Count + " observation-change-fields={" + Counts(observationChanges) + "}";
        }

        void UploadMesh(Group g)
        {
            var mesh = g.Mesh;
            int count = g.VertexCount;
            mesh.SetVertexBufferParams(count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, floatColor ? VertexAttributeFormat.Float32 : VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            mesh.SetTriangles(g.Indices, 0, false);
            int stride = floatColor ? Marshal.SizeOf(typeof(FloatVertex)) : Marshal.SizeOf(typeof(ByteVertex));
            if (mesh.GetVertexAttributeFormat(VertexAttribute.Color) != (floatColor ? VertexAttributeFormat.Float32 : VertexAttributeFormat.UNorm8)
                || mesh.GetVertexAttributeDimension(VertexAttribute.Color) != 4 || mesh.GetVertexBufferStride(0) != stride)
                throw new InvalidOperationException("vertex buffer layout mismatch");
        }

        // Compare all shader-declared effective values, not material instance IDs. This preserves
        // independent instances with identical state while splitting distinct material/MPB state.
        string MaterialState(SpriteRenderer r, Material copy)
        {
            var m = r.sharedMaterial; var shader = m.shader;
            block.Clear(); r.GetPropertyBlock(block);
            if (block.HasColor("_RendererColor") && block.GetColor("_RendererColor") != Color.white
                && block.GetColor("_RendererColor") != r.color)
                throw new InvalidOperationException("custom MPB _RendererColor unsupported");
            if (block.HasVector("_Flip"))
            {
                var flip = block.GetVector("_Flip");
                if ((flip.x != 1 || flip.y != 1) && (flip.x != (r.flipX ? -1 : 1) || flip.y != (r.flipY ? -1 : 1)))
                    throw new InvalidOperationException("custom MPB _Flip unsupported");
            }
            if (block.HasVector("_MainTex_ST") && !block.GetVector("_MainTex_ST").Equals(new Vector4(1, 1, 0, 0)))
                throw new InvalidOperationException("custom MPB _MainTex_ST unsupported");
            if (block.HasTexture("_AlphaTex") && block.GetTexture("_AlphaTex"))
                throw new InvalidOperationException("MPB split-alpha unsupported");
            var key = new StringBuilder().Append(shader.GetInstanceID()).Append('|').Append(m.renderQueue)
                .Append('|').Append(string.Join(",", m.shaderKeywords));
            for (int p = 0; p < shader.GetPropertyCount(); p++)
            {
                string n = shader.GetPropertyName(p); int id = Shader.PropertyToID(n);
                if (n == "_RendererColor" || n == "_Flip") continue;
                key.Append('|').Append(n).Append('=');
                switch (shader.GetPropertyType(p))
                {
                    case ShaderPropertyType.Color:
                        Color c = block.HasColor(id) ? block.GetColor(id) : m.GetColor(id);
                        key.Append(c.r.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.g.ToString("R", CultureInfo.InvariantCulture))
                            .Append(',').Append(c.b.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.a.ToString("R", CultureInfo.InvariantCulture));
                        if (copy) copy.SetColor(id, c); break;
                    case ShaderPropertyType.Vector:
                        var v = block.HasVector(id) ? block.GetVector(id) : m.GetVector(id);
                        for (int k = 0; k < 4; k++) key.Append(v[k].ToString("R", CultureInfo.InvariantCulture)).Append(',');
                        if (copy) copy.SetVector(id, v); break;
                    case ShaderPropertyType.Texture:
                        var t = block.HasTexture(id) ? block.GetTexture(id) : m.GetTexture(id);
                        if (n == "_MainTex")
                        {
                            if (block.HasTexture(id) && t != r.sprite.texture) throw new InvalidOperationException("MPB _MainTex override");
                            if (!m.GetTextureScale(n).Equals(Vector2.one) || !m.GetTextureOffset(n).Equals(Vector2.zero))
                                throw new InvalidOperationException("nonidentity _MainTex scale/offset unsupported by atlas remapping");
                        }
                        else key.Append(t ? t.GetInstanceID() : 0);
                        key.Append(m.GetTextureScale(n).ToString("R")).Append(m.GetTextureOffset(n).ToString("R"));
                        if (copy) copy.SetTexture(id, t); break;
                    case ShaderPropertyType.Int:
                        int integer = block.HasInteger(id) ? block.GetInteger(id) : m.GetInteger(id);
                        key.Append(integer); if (copy) copy.SetInteger(id, integer); break;
                    default:
                        float f = block.HasFloat(id) ? block.GetFloat(id) : m.GetFloat(id);
                        key.Append(f.ToString("R", CultureInfo.InvariantCulture)); if (copy) copy.SetFloat(id, f); break;
                }
            }
            if ((m.HasProperty("_EnableExternalAlpha") && m.GetFloat("_EnableExternalAlpha") != 0)
                || (block.HasFloat("_EnableExternalAlpha") && block.GetFloat("_EnableExternalAlpha") != 0))
                throw new InvalidOperationException("external alpha unsupported");
            return key.ToString();
        }

        static string Sampler(Texture2D t) => t.graphicsFormat + "/" + t.wrapModeU + "/" + t.wrapModeV + "/"
            + t.filterMode + "/" + t.anisoLevel + "/" + t.mipMapBias.ToString("R", CultureInfo.InvariantCulture);
        void Place(Image image)
        {
            var t = image.Source; string sampler = Sampler(t);
            // Mipmapped textures use dedicated atlases: exact source dimensions/LOD and wrap,
            // no cross-image mip bleed. Mipless sources are shelf-packed without downscaling.
            bool dedicated = t.mipmapCount > 1 || t.width + Padding * 2 > SystemInfo.maxTextureSize
                || t.height + Padding * 2 > SystemInfo.maxTextureSize;
            Atlas a = null;
            if (!dedicated)
            {
                foreach (var candidate in atlases)
                {
                    if (candidate.Dedicated || candidate.Sampler != sampler) continue;
                    int x = candidate.X, y = candidate.Y;
                    if (x + t.width + Padding * 2 > candidate.Width) { x = 0; y += candidate.Row; }
                    if (y + t.height + Padding * 2 > candidate.Height) continue;
                    if (x == 0 && candidate.X != 0) { candidate.Y = y; candidate.Row = 0; }
                    candidate.X = x; a = candidate; break;
                }
            }
            if (a == null)
            {
                // Begin with a bounded shelf, growing for larger sources without downscaling.
                int side = Mathf.Min(SystemInfo.maxTextureSize, Mathf.Max(2048,
                    Mathf.NextPowerOfTwo(Mathf.Max(t.width, t.height) + Padding * 2)));
                a = new Atlas { Width = dedicated ? t.width : side, Height = dedicated ? t.height : side,
                    Mips = t.mipmapCount, Dedicated = dedicated, Sampler = sampler,
                    Linear = !GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat) };
                atlases.Add(a);
            }
            image.Atlas = a; image.X = a.Dedicated ? 0 : a.X + Padding; image.Y = a.Dedicated ? 0 : a.Y + Padding;
            a.Images.Add(image);
            if (!a.Dedicated) { a.X += t.width + Padding * 2; a.Row = Mathf.Max(a.Row, t.height + Padding * 2); }
        }

        static int Wrapped(int i, int length, TextureWrapMode wrap)
        {
            if (wrap == TextureWrapMode.Clamp) return Mathf.Clamp(i, 0, length - 1);
            if (wrap == TextureWrapMode.MirrorOnce) return Mathf.Clamp(i < 0 ? -i - 1 : i, 0, length - 1);
            if (wrap == TextureWrapMode.Mirror)
            { int k = ((i % (length * 2)) + length * 2) % (length * 2); return k < length ? k : length * 2 - k - 1; }
            return ((i % length) + length) % length;
        }
        static void TrimAtlas(Atlas a)
        {
            if (!a.Dedicated)
            {
                // Trim empty shelf space; power-of-two sizes keep UV scaling stable.
                int w = 1, h = 1;
                foreach (var image in a.Images) { w = Mathf.Max(w, image.X + image.Source.width + Padding); h = Mathf.Max(h, image.Y + image.Source.height + Padding); }
                a.Width = Mathf.Min(a.Width, Mathf.NextPowerOfTwo(w)); a.Height = Mathf.Min(a.Height, Mathf.NextPowerOfTwo(h));
            }
        }
        static long AtlasBytes(Atlas a)
        {
            long bytes = 0;
            for (int m = 0; m < a.Mips; m++) bytes += (long)Mathf.Max(1, a.Width >> m) * Mathf.Max(1, a.Height >> m) * 4;
            return bytes;
        }
        void BuildAtlas(Atlas a)
        {
            var source = a.Images[0].Source;
            a.Texture = new Texture2D(a.Width, a.Height, TextureFormat.RGBA32, a.Mips, a.Linear)
            { name = "DecoRender atlas", hideFlags = HideFlags.HideAndDontSave, filterMode = source.filterMode,
                wrapModeU = source.wrapModeU, wrapModeV = source.wrapModeV, anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias };
            long bytes = 0;
            for (int mip = 0; mip < a.Mips; mip++)
            {
                int w = Mathf.Max(1, a.Width >> mip), h = Mathf.Max(1, a.Height >> mip);
                var pixels = new Color32[w * h]; bytes += (long)pixels.Length * 4;
                foreach (var image in a.Images)
                {
                    var t = image.Source; int sw = Mathf.Max(1, t.width >> mip), sh = Mathf.Max(1, t.height >> mip);
                    var decoded = Decode(t, mip, a.Linear);
                    int pad = a.Dedicated ? 0 : Padding;
                    for (int y = -pad; y < sh + pad; y++) for (int x = -pad; x < sw + pad; x++)
                        pixels[(image.Y + y) * w + image.X + x] = decoded[Wrapped(y, sh, t.wrapModeV) * sw + Wrapped(x, sw, t.wrapModeU)];
                }
                a.Texture.SetPixels32(pixels, mip);
            }
            a.Texture.Apply(false, true);
            Log("atlas=" + atlases.IndexOf(a) + " size=" + a.Width + "x" + a.Height + " RGBA32 mips=" + a.Mips
                + " gpu-bytes=" + bytes + " gpu-MiB=" + F(bytes / 1048576.0) + " sources=" + a.Images.Count
                + " padding=" + (a.Dedicated ? 0 : Padding) + " dedicated=" + a.Dedicated + " linear=" + a.Linear);
        }

        static Color32[] Decode(Texture2D source, int mip, bool linear)
        {
            int w = Mathf.Max(1, source.width >> mip), h = Mathf.Max(1, source.height >> mip);
            Texture2D single = null, read = null;
            RenderTexture rt = null; var old = RenderTexture.active; bool oldSRGB = GL.sRGBWrite;
            try
            {
                Texture input = source;
                if (source.mipmapCount > 1)
                {
                    // Isolate a source mip BEFORE Blit so LOD is explicit, including compressed sources.
                    single = new Texture2D(w, h, source.format, 1, linear);
                    Graphics.CopyTexture(source, 0, mip, single, 0, 0); input = single;
                }
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32,
                    linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = !linear && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(input, rt); RenderTexture.active = rt;
                read = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false); read.Apply(false, false);
                return read.GetPixels32();
            }
            finally
            {
                GL.sRGBWrite = oldSRGB; RenderTexture.active = old;
                if (read) Destroy(read); if (single) Destroy(single); if (rt) RenderTexture.ReleaseTemporary(rt);
            }
        }

        Camera[] SceneCameras()
        {
            var result = new List<Camera>();
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c != compareCamera && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded) result.Add(c);
            return result.ToArray();
        }
        void Update()
        {
            if (!initialized || stopping || comparing) return;
            if (!ValidSession()) Stop(transitionReason);
        }
        bool ValidSession()
        {
            transitionReason = null;
            if (!manager || manager != scrDecorationManager.instance) transitionReason = "manager changed/destroyed";
            else if (!mainCamera || !scrCamera.instance || mainCamera != scrCamera.instance.camobj) transitionReason = "main camera changed/destroyed";
            else if (SceneReset.Resetting || SceneReset.Count != resetCount) transitionReason = "scene reset";
            else if (SceneManager.GetActiveScene().handle != scene) transitionReason = "active scene changed";
            else if (!ReferenceEquals(manager.allDecorations, decorationList)) transitionReason = "decoration list replaced";
            else if (manager.allDecorations.Count != decorationCount) transitionReason = "decoration list count changed";
            else if (beganEditorPlayback && (!scnEditor.instance || !scnEditor.instance.playMode)) transitionReason = "returned to editor";
            // No Hitch.Playing check: pause/end-state/monitor lag is not a map transition.
            return transitionReason == null;
        }
        void PrepareFrame()
        {
            if (preparedFrame == Time.frameCount) return;
            if (frameCpuFrame >= 0) frameCpuSamples.Add(frameCpuMs);
            frameCpuMs = 0; frameCpuFrame = Time.frameCount;
            long start = Stopwatch.GetTimestamp();
            auditMs = dirtyMs = changeMs = layoutMs = scheduleMs = waitMs = otherMs = 0;
            long cameraStart = Stopwatch.GetTimestamp();
            var sceneCameras = SceneCameras();
            var current = new HashSet<int>();
            bool previousMerge = mergeCameras;
            mergeCameras = true;
            foreach (var c in sceneCameras)
            {
                current.Add(c.GetInstanceID());
                var mode = c.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortMode : c.transparencySortMode;
                var axis = mode == TransparencySortMode.CustomAxis
                    ? (c.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortAxis : c.transparencySortAxis)
                    : c.transform.forward;
                if (mode == TransparencySortMode.Perspective || (mode == TransparencySortMode.Default && !c.orthographic)
                    || axis.x != 0 || axis.y != 0) mergeCameras = false;
                // Camera-specific snapshots prevent one camera from overwriting another.
                // Translation participates only in perspective distance; it cancels in axis sorting.
                var sort = new CameraSortSnapshot { Mode = mode, Orthographic = c.orthographic, Axis = axis,
                    Position = mode == TransparencySortMode.Perspective || (mode == TransparencySortMode.Default && !c.orthographic)
                        ? c.transform.position : Vector3.zero };
                CameraSortSnapshot prior;
                if (preparedCameraSort.TryGetValue(c.GetInstanceID(), out prior) &&
                    (prior.Mode != sort.Mode || prior.Orthographic != sort.Orthographic
                        || !SameBits(prior.Axis, sort.Axis) || !SameBits(prior.Position, sort.Position))) DirtyAllKeys("sort-axis");
                preparedCameraSort[c.GetInstanceID()] = sort;
            }
            if (!cameraSet.SetEquals(current)) { TopologyCause("camera-list"); Stop("scene camera list changed"); return; }
            if (previousMerge != mergeCameras) DirtyAllKeys("merge-cameras");
            cameraMs = Milliseconds(Stopwatch.GetTimestamp() - cameraStart);
            long borrowStart = Stopwatch.GetTimestamp();
            foreach (var g in groups) Borrow(g, false);
            borrowMs = Milliseconds(Stopwatch.GetTimestamp() - borrowStart);
            fallbackKeys.Clear();
            otherMs = Milliseconds(Stopwatch.GetTimestamp() - start);
            long dirtyStart = Stopwatch.GetTimestamp();
            GatherWork(false);
            long pruneStart = Stopwatch.GetTimestamp();
            PruneChanged(work);
            pruneMs = Milliseconds(Stopwatch.GetTimestamp() - pruneStart);
            dirtyMs = Milliseconds(Stopwatch.GetTimestamp() - dirtyStart);
            long auditStart = Stopwatch.GetTimestamp();
            AuditLateBarriers();
            auditMs = Milliseconds(Stopwatch.GetTimestamp() - auditStart);
            long topologyStart = Stopwatch.GetTimestamp();
            bool resetLayout = false;
            topologyChangedLast = 0;
            if (topologyDirty) { DirtyAllKeys("other"); topologyDirty = false; }
            if (topologyKeys.Count != 0) resetLayout = RebuildChangedKeys(mainCamera);
            LogTopologyBuild(topologyChangedLast == 0 ? "stable" : "keys");
            topologyMs = (Stopwatch.GetTimestamp() - topologyStart) * 1000.0 / Stopwatch.Frequency;
            long layoutStart = Stopwatch.GetTimestamp();
            if (resetLayout) ResetLayout();
            layoutMs = Milliseconds(Stopwatch.GetTimestamp() - layoutStart);
            Build();
            long otherStart = Stopwatch.GetTimestamp();
            SampleVisibleRuns();
            preparedFrame = Time.frameCount;
            otherMs += Milliseconds(Stopwatch.GetTimestamp() - otherStart);
            long ticks = Stopwatch.GetTimestamp() - start;
            buildTicks += ticks; buildCount++; measuredFrames++;
            frameTicks = ticks; lastBuildMs = ticks * 1000.0 / Stopwatch.Frequency;
            frameCpuMs += lastBuildMs;
            totalSamples.Add(lastBuildMs); collectSamples.Add(collectMs); computeSamples.Add(computeMs);
            uploadSamples.Add(uploadMs); topologySamples.Add(topologyMs);
            cameraSamples.Add(cameraMs); borrowSamples.Add(borrowMs); pruneSamples.Add(pruneMs);
            auditSamples.Add(auditMs); dirtySamples.Add(dirtyMs); changeSamples.Add(changeMs); layoutSamples.Add(layoutMs);
            scheduleSamples.Add(scheduleMs); waitSamples.Add(waitMs); otherSamples.Add(otherMs);
            unaccountedSamples.Add(lastBuildMs - (auditMs + dirtyMs + changeMs + layoutMs + collectMs
                + scheduleMs + waitMs + uploadMs + topologyMs + otherMs));
        }
        void PreCull(Camera camera)
        {
            long callbackStart = Stopwatch.GetTimestamp();
            if (stopping || comparing || !initialized || observing) return;
            try
            {
                if (!ValidSession()) { Stop(transitionReason); return; }
                PrepareFrame();
                if (!stopping) Takeover(true);
            }
            catch (Exception e) { Stop("pre-cull failed: " + e); }
            finally { preCullSamples.Add(Milliseconds(Stopwatch.GetTimestamp() - callbackStart)); }
        }

        void PreRender(Camera camera)
        {
            long callbackStart = Stopwatch.GetTimestamp();
            if (stopping || comparing || !initialized) return;
            try
            {
                if (!ValidSession()) { Stop(transitionReason); return; }
                int id = camera.GetInstanceID(); int count; cameras.TryGetValue(id, out count); cameras[id] = count + 1;
                if (count == 0) Log("camera id=" + id + " name=" + camera.name + " mask=" + camera.cullingMask
                    + " orthographic=" + camera.orthographic + " sortMode=" + camera.transparencySortMode
                    + " sortAxis=" + camera.transparencySortAxis + " graphicsSortMode=" + GraphicsSettings.transparencySortMode
                    + " graphicsSortAxis=" + GraphicsSettings.transparencySortAxis);
                if (observing)
                {
                    PruneChanged();
                    foreach (var i in items)
                    {
                        var matrix = i.R.transform.localToWorldMatrix; var color = i.R.color;
                        if (!MatrixEqual(matrix, i.Matrix) || !ColorEqual(color, i.Color) || i.FlipX != i.R.flipX || i.FlipY != i.R.flipY) i.Moved = true;
                        i.Matrix = matrix; i.Color = color; i.FlipX = i.R.flipX; i.FlipY = i.R.flipY;
                    }
                    if (observeFrame != Time.frameCount) { observeFrame = Time.frameCount; observeFrames++; }
                    if (observeFrames >= observeTarget) FinishObservation();
                    return; // New groups need the NEXT camera's PreCull before takeover.
                }
                if (preparedFrame == Time.frameCount)
                {
                    Takeover(true);
                }
            }
            catch (Exception e) { Stop("pre-render failed: " + e); }
            finally { preRenderSamples.Add(Milliseconds(Stopwatch.GetTimestamp() - callbackStart)); }
        }

        static bool MatrixEqual(Matrix4x4 a, Matrix4x4 b)
        { for (int n = 0; n < 16; n++) if (BitConverter.SingleToInt32Bits(a[n]) != BitConverter.SingleToInt32Bits(b[n])) return false; return true; }
        static bool ColorEqual(Color a, Color b) => BitConverter.SingleToInt32Bits(a.r) == BitConverter.SingleToInt32Bits(b.r)
            && BitConverter.SingleToInt32Bits(a.g) == BitConverter.SingleToInt32Bits(b.g)
            && BitConverter.SingleToInt32Bits(a.b) == BitConverter.SingleToInt32Bits(b.b)
            && BitConverter.SingleToInt32Bits(a.a) == BitConverter.SingleToInt32Bits(b.a);
        static string Identity(UnityEngine.Object value) => value ? value.name + "#" + value.GetInstanceID() : "destroyed/null";
        bool Changed(Item i, string field, object before, object after)
        {
            Count(changes, field);
            if (observing) Count(observationChanges, observationChanges.ContainsKey(field) ? field : "other");
            if (detailEvents++ < DetailLimit)
                Log("change index=" + i.Index + " renderer=" + i.Id + " field=" + field + " before={" + before + "} after={" + after + "}");
            return true;
        }
        void PruneChanged(IEnumerable<Item> candidates = null)
        {
            var removed = new List<Item>();
            foreach (var i in candidates ?? items)
            {
                bool remove = false;
                if (!i.D || !i.R) remove = Changed(i, "destroyed", "decoration/renderer alive", "decoration=" + (bool)i.D + " renderer=" + (bool)i.R);
                else
                {
                    var r = i.R;
                    if (i.D.spriteRenderer != r) remove |= Changed(i, "renderer-owner", Identity(r), Identity(i.D.spriteRenderer));
                    if (r.sprite != i.Sprite) remove |= Changed(i, "sprite", Identity(i.Sprite), Identity(r.sprite));
                    if (r.sharedMaterial != i.Source) remove |= Changed(i, "material", Identity(i.Source), Identity(r.sharedMaterial));
                    if (r.sortingLayerID != i.Layer) remove |= Changed(i, "sortingLayer", i.Layer, r.sortingLayerID);
                    if (r.sortingOrder != i.Order) remove |= Changed(i, "sortingOrder", i.Order, r.sortingOrder);
                    if (r.gameObject.layer != i.UnityLayer) remove |= Changed(i, "unityLayer", i.UnityLayer, r.gameObject.layer);
                    if (i.D.hitbox != 0 || i.D.isMask() || MeshOn(i.D) || r.maskInteraction != SpriteMaskInteraction.None)
                        remove |= Changed(i, "mask", "hitbox=0 mask=False mesh=False interaction=None",
                            "hitbox=" + i.D.hitbox + " mask=" + i.D.isMask() + " mesh=" + MeshOn(i.D) + " interaction=" + r.maskInteraction);
                    if (r.GetComponentInParent<SortingGroup>()) remove |= Changed(i, "sortingGroup", "none", "present");
                    if (r.drawMode != SpriteDrawMode.Simple) remove |= Changed(i, "drawMode", SpriteDrawMode.Simple, r.drawMode);
                    if (r.spriteSortPoint != SpriteSortPoint.Center) remove |= Changed(i, "sprite-sort-point", SpriteSortPoint.Center, r.spriteSortPoint);
                    if (i.D.repeatX != i.RepeatX || i.D.repeatY != i.RepeatY)
                        remove |= Changed(i, "repeat", i.RepeatX + "," + i.RepeatY, i.D.repeatX + "," + i.D.repeatY);
                    if (!remove)
                    {
                        try
                        {
                            if (i.Uniforms == null) i.Uniforms = ReadUniforms(r);
                            if (!UniformsUnchanged(r, i.Uniforms)) remove |= Changed(i, "material-properties", i.Signature, "effective uniform changed");
                            var t = i.Image.Source;
                            if (t.wrapModeU != TextureWrapMode.Clamp || t.wrapModeV != TextureWrapMode.Clamp)
                                remove |= Changed(i, "wrap", "Clamp/Clamp", t.wrapModeU + "/" + t.wrapModeV);
                        }
                        catch (Exception e) { remove |= Changed(i, "material-properties", i.Signature, e.Message); }
                    }
                }
                if (remove)
                {
                    removed.Add(i);
                }
            }
            foreach (var i in removed)
            {
                rejectionReasons[i.Id] = "changed";
                RetireKey(OrderKey(i), "prune");
                if (i.R) {
                    string currentKey = OrderKey(i.R.sortingLayerID, i.R.sortingOrder);
                    if (currentKey != OrderKey(i)) RetireKey(currentKey, "prune-key-migration");
                    if (i.R.sharedMaterial) RetireMaterialUsers(i.R.sharedMaterial.GetInstanceID());
                }
                Count(changes, "restored-items");
                if (observing) Count(excluded, "changed-during-observation");
            }
            // Key planning sees restored originals as barriers and replaces only affected runs.
        }

        void PostRender(Camera camera)
        {
            if (stopping || comparing || observing) return;
            try
            {
                if (pending != 0 && camera == mainCamera)
                {
                    pending = 0;
                    try { Compare(); }
                    catch (Exception e) { lastCompare = "compare failed: " + e; Stop(lastCompare); }
                    finally { compareFinished = true; }
                }
            }
            finally
            {
                long drawStart = Stopwatch.GetTimestamp();
                // Our temporary force-off must not mask the game's next visibility update.
                foreach (var g in groups) Borrow(g, false);
                foreach (var i in hiddenSources) RestoreSource(i);
                hiddenSources.Clear();
                foreach (var g in groups) if (g.Renderer) g.Renderer.enabled = false;
                double drawMs = Milliseconds(Stopwatch.GetTimestamp() - drawStart);
                drawOffSamples.Add(drawMs); frameCpuMs += drawMs;
            }
        }

        void Takeover(bool on)
        {
            if (on && stopping) return;
            long drawStart = Stopwatch.GetTimestamp();
            if (!on) foreach (var g in groups) Borrow(g, false);
            foreach (var g in groups)
            {
                bool take = on && !fallbackKeys.Contains(g.SortKey);
                Borrow(g, take);
                if (take) foreach (var i in g.VisibleItems) {
                    if (!i.Hidden) { HideSource(i); hiddenSources.Add(i); }
                }
                else foreach (var i in g.VisibleItems) RestoreSource(i);
                if (g.Renderer) g.Renderer.enabled = take && g.HasVisible;
            }
            double drawMs = Milliseconds(Stopwatch.GetTimestamp() - drawStart);
            (on ? drawOnSamples : drawOffSamples).Add(drawMs); frameCpuMs += drawMs;
        }
        bool SnapshotUnchanged()
        {
            try
            {
            if (fallbackKeys.Count != 0) return false;
            foreach (var i in items)
                if (!i.D || !i.R || (i.Visible && (!MatrixEqual(i.R.transform.localToWorldMatrix, i.Matrix) || !ColorEqual(i.R.color, i.Color)
                    || i.R.flipX != i.FlipX || i.R.flipY != i.FlipY)) || i.R.sprite != i.Sprite
                    || i.R.sharedMaterial != i.Source || i.R.sortingLayerID != i.Layer || i.R.sortingOrder != i.Order
                    || i.R.gameObject.layer != i.UnityLayer || i.D.spriteRenderer != i.R
                    || i.D.hitbox != 0 || i.D.isMask() || MeshOn(i.D) || i.R.maskInteraction != SpriteMaskInteraction.None
                    || i.R.drawMode != SpriteDrawMode.Simple || i.R.spriteSortPoint != SpriteSortPoint.Center || i.R.GetComponentInParent<SortingGroup>()
                    || i.D.repeatX != i.RepeatX || i.D.repeatY != i.RepeatY
                    || SourceVisible(i) != i.Visible
                    || MaterialState(i.R, null) != i.Signature) return false;
            return ValidSession();
            }
            catch (Exception) { return false; } // A callback mutation invalidates this sample; finally restores that item.
        }
        bool[] SelectedPixels(Camera camera, int width, int height)
        {
            var mask = new bool[width * height];
            foreach (var i in items)
            {
                if (!i.Visible || (camera.cullingMask & (1 << i.UnityLayer)) == 0) continue;
                var bounds = i.R.bounds;
                float minX = width, minY = height, maxX = 0, maxY = 0;
                int behind = 0;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var screen = camera.WorldToScreenPoint(point);
                    if (screen.z < camera.nearClipPlane) behind++;
                    minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
                    minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
                }
                if (behind == 8) continue;
                // A box crossing the near plane has an unbounded projection: conservative full image.
                if (behind != 0) { minX = minY = 0; maxX = width; maxY = height; }
                int x0 = Mathf.Clamp(Mathf.FloorToInt(minX) - 1, 0, width), x1 = Mathf.Clamp(Mathf.CeilToInt(maxX) + 1, 0, width);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(minY) - 1, 0, height), y1 = Mathf.Clamp(Mathf.CeilToInt(maxY) + 1, 0, height);
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) mask[y * width + x] = true;
            }
            return mask;
        }
        void Compare()
        {
            comparing = true;
            GameObject go = null; RenderTexture rt = null; Texture2D a = null, b = null, diff = null;
            var old = RenderTexture.active; bool oldSRGB = GL.sRGBWrite;
            try
            {
                // Snapshot once after LateUpdate. No decoration UpdatePosition/logic calls here.
                // Reuse the once-per-frame mesh; mutations invalidate this compare instead of rebuilding.
                if (preparedFrame != Time.frameCount || !SnapshotUnchanged()) throw new InvalidOperationException("frame snapshot changed before compare");
                int w = mainCamera.pixelWidth, h = mainCamera.pixelHeight;
                if (w <= 0 || h <= 0) throw new InvalidOperationException("empty camera dimensions");
                go = new GameObject("DecoRender compare camera") { hideFlags = HideFlags.HideAndDontSave };
                var c = go.AddComponent<Camera>(); compareCamera = c; c.enabled = false; c.CopyFrom(mainCamera); c.enabled = false;
                c.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
                c.worldToCameraMatrix = mainCamera.worldToCameraMatrix; c.projectionMatrix = mainCamera.projectionMatrix;
                c.rect = new Rect(0, 0, 1, 1); c.stereoTargetEye = StereoTargetEyeMask.None;
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1; c.targetTexture = rt;
                var selectedPixels = SelectedPixels(c, w, h);
                Takeover(false); ClearTarget(rt, c.backgroundColor); c.Render(); a = Read(rt);
                if (stopping) throw new InvalidOperationException("compare interrupted: " + lastStop);
                bool same = SnapshotUnchanged();
                Takeover(true); ClearTarget(rt, c.backgroundColor); c.Render(); b = Read(rt); same &= SnapshotUnchanged();
                if (stopping) throw new InvalidOperationException("compare interrupted: " + lastStop);
                var aa = a.GetPixels32(); var bb = b.GetPixels32(); var dd = new Color32[aa.Length];
                int[] thresholds = { 0, 1, 2, 4, 8 }; long[] counts = new long[5]; int[] max = new int[4];
                long[] inside = new long[5], outside = new long[5]; long insidePixels = 0;
                int insideMax = 0, outsideMax = 0;
                int greatest = -1, location = 0;
                for (int n = 0; n < aa.Length; n++)
                {
                    int dr = Math.Abs(aa[n].r - bb[n].r), dg = Math.Abs(aa[n].g - bb[n].g),
                        db = Math.Abs(aa[n].b - bb[n].b), da = Math.Abs(aa[n].a - bb[n].a);
                    max[0] = Math.Max(max[0], dr); max[1] = Math.Max(max[1], dg); max[2] = Math.Max(max[2], db); max[3] = Math.Max(max[3], da);
                    int d = Math.Max(Math.Max(dr, dg), Math.Max(db, da)); if (d > greatest) { greatest = d; location = n; }
                    if (selectedPixels[n]) { insidePixels++; insideMax = Math.Max(insideMax, d); }
                    else outsideMax = Math.Max(outsideMax, d);
                    for (int k = 0; k < 5; k++) if (d > thresholds[k])
                    { counts[k]++; if (selectedPixels[n]) inside[k]++; else outside[k]++; }
                    // Include alpha-only differences in blue; output alpha remains opaque for inspection.
                    dd[n] = new Color32((byte)Math.Min(255, dr * 16), (byte)Math.Min(255, dg * 16), (byte)Math.Min(255, Math.Max(db, da) * 16), 255);
                }
                diff = new Texture2D(w, h, TextureFormat.RGBA32, false); diff.SetPixels32(dd); diff.Apply(false);
                Directory.CreateDirectory(outputDir);
                string name = "decorender-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + "-f" + Time.frameCount;
                string prefix = Path.Combine(outputDir, name);
                File.WriteAllBytes(prefix + "-original.png", a.EncodeToPNG()); File.WriteAllBytes(prefix + "-group.png", b.EncodeToPNG());
                File.WriteAllBytes(prefix + "-diff16.png", diff.EncodeToPNG());
                var result = new StringBuilder("frame=").Append(Time.frameCount).Append(" song=")
                    .Append(scrConductor.instance ? F(scrConductor.instance.songposition_minusi) : "n/a")
                    .Append(" size=").Append(w).Append('x').Append(h).Append(" maxRGBA=").Append(string.Join(",", max))
                    .Append(" max=").Append(greatest).Append(" xy-bottom-left=").Append(location % w).Append(',').Append(location / w)
                    .Append(" snapshotStable=").Append(same);
                for (int k = 0; k < 5; k++) result.Append(" >").Append(thresholds[k]).Append('=').Append(counts[k]).Append('/').Append(aa.Length)
                    .Append(" ratio=").Append(F((double)counts[k] / aa.Length));
                result.Append(" selected-bounds-pixels=").Append(insidePixels).Append(" inside-max=").Append(insideMax).Append(" outside-max=").Append(outsideMax);
                for (int k = 0; k < 5; k++)
                {
                    long outsidePixels = aa.Length - insidePixels;
                    result.Append(" inside>").Append(thresholds[k]).Append('=').Append(inside[k]).Append('/').Append(insidePixels)
                        .Append(" ratio=").Append(F((double)inside[k] / Math.Max(1, insidePixels)))
                        .Append(" outside>").Append(thresholds[k]).Append('=').Append(outside[k]).Append('/').Append(outsidePixels)
                        .Append(" ratio=").Append(F((double)outside[k] / Math.Max(1, outsidePixels)));
                }
                result.Append(" bounds=union-of-projected-world-AABBs-plus-1px; conservative-region-NOT-object-coverage");
                result.Append(" files=").Append(prefix).Append("-{original,group,diff16}.png")
                    .Append("; clone camera only; source image effects/command buffers/other cameras NOT copied; temporal/global callbacks may differ; performance UNMEASURED");
                lastCompare = result.ToString(); File.WriteAllText(prefix + "-metrics.txt", lastCompare, Encoding.UTF8);
            }
            finally
            {
                if (!stopping) Takeover(false);
                RenderTexture.active = old; GL.sRGBWrite = oldSRGB;
                compareCamera = null;
                if (go) { var c = go.GetComponent<Camera>(); if (c) c.targetTexture = null; Destroy(go); }
                if (rt) RenderTexture.ReleaseTemporary(rt);
                if (a) Destroy(a); if (b) Destroy(b); if (diff) Destroy(diff); comparing = false;
            }
        }
        static void ClearTarget(RenderTexture rt, Color color)
        { RenderTexture.active = rt; GL.Clear(true, true, color); }
        static Texture2D Read(RenderTexture rt)
        {
            RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            try { t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false); t.Apply(false); return t; }
            catch { Destroy(t); throw; }
        }
        internal static string State()
        {
            var s = session; var result = new StringBuilder("active=").Append((bool)s);
            if (s)
            {
                result.Append(" observing=").Append(s.observing).Append(" observation=").Append(s.observeFrames).Append('/').Append(s.observeTarget)
                    .Append(" excluded={").Append(Counts(s.excluded)).Append("}")
                    .Append(" atlas-bytes=").Append(s.atlasBytes).Append('/').Append(s.atlasBudget)
                    .Append(" selected=").Append(s.selected).Append(" managed=").Append(s.items.Count).Append(" groups=").Append(s.groups.Count)
                    .Append(" vertices=").Append(s.vertexCount).Append(" atlases=").Append(s.atlases.Count);
                foreach (var a in s.atlases) result.Append(' ').Append(a.Width).Append('x').Append(a.Height).Append("/mips=").Append(a.Mips);
                result.Append(" build-ms/last-frame=").Append(F(s.lastBuildMs)).Append(" build-ms/mean-build=")
                    .Append(F(s.buildTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, s.buildCount)))
                    .Append(" build-ms/current-frame=").Append(F(s.frameTicks * 1000.0 / Stopwatch.Frequency))
                    .Append(" build-ms/mean-frame=").Append(F(s.buildTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, s.measuredFrames)))
                    .Append(" compute-ms=").Append(F(s.computeMs)).Append(" upload-ms=").Append(F(s.uploadMs))
                    .Append(" changed-items=").Append(s.changedItems).Append(" uploaded-vertices=").Append(s.uploadedVertices)
                    .Append(" prepared-frame=").Append(s.preparedFrame).Append(" builds=").Append(s.buildCount).Append(" cameras=").Append(s.cameras.Count).Append(" comparePending=").Append(s.pending);
            }
            if (s) result.Append(s.IncrementalState()).Append(s.StageState()).Append("\n tightlate-context: ").Append(TightLate.State());
            return result.Append(" vertex-color=").Append(lastColorFormat).Append(" order-audit={").Append(lastOrders).Append('}')
                .Append(" run-state={").Append(s ? s.RunState() : lastRuns).Append('}')
                .Append(" key-state={").Append(s ? s.KeyState() : lastKeys).Append('}')
                .Append(" tile-vertex-input=Float32x4(saved-D3D11-asm) tile-color=미확인(vertex=SpriteRenderer.color,_Color=effective-source,_RendererColor=white-if-declared;compare-required)")
                .Append(" selection={").Append(lastSelection).Append("} changes={").Append(s ? Counts(s.changes) : lastChanges).Append("} detail-limit=").Append(DetailLimit).Append(" stop=").Append(lastStop).Append(" lastCompare={").Append(lastCompare).Append('}').ToString();
        }
        internal static void Stop() => Stop("off/unload/reset");
        static void Stop(string reason)
        {
            var s = session; if (!s || s.stopping) return;
            s.stopping = true; lastRuns = s.RunState(); lastStop = reason; lastChanges = Counts(s.changes); lastKeys = s.KeyState();
            Camera.onPreCull -= s.PreCull; Camera.onPreRender -= s.PreRender; Camera.onPostRender -= s.PostRender;
            if (s.pending != 0) { lastCompare = "cancelled: " + reason; compareFinished = true; }
            // Disable immediately: Destroy is deferred until end of frame.
            foreach (var g in s.groups) if (g.Renderer) g.Renderer.enabled = false;
            foreach (var g in s.groups) Borrow(g, false);
            foreach (var i in s.items) RestoreSource(i);
            foreach (var g in s.groups)
                ReleaseGroup(g);
            s.DisposeBuffers();
            foreach (var a in s.atlases) if (a.Texture) Destroy(a.Texture);
            session = null; Destroy(s.gameObject); Log("stopped: " + reason + "; original forceRenderingOff restored");
        }
        void OnDestroy() { if (session == this) Stop("session object destroyed"); }
    }
}
#endif
