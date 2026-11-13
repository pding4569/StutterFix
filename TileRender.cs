#if DEV || AUTOTEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Diagnostic snapshot backend. Does not claim live instancing admission.
    // In particular, a CommandBuffer event cannot split Unity's transparent list.
    internal sealed class TileRender : MonoBehaviour
    {
        static TileRender instance;
        static bool sampling;
        static Exception sampleError;
        Camera camera;
        CommandBuffer commands;
        Item[] snapshot;
        Matrix4x4 view, projection;
        int cullingMask;
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<bool> originals = new List<bool>();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        sealed class Item
        {
            internal Renderer Renderer;
            internal Mesh Mesh;
            internal Material Material;
            internal Matrix4x4 Matrix;
            internal string Content;
            internal SceneRenderOrder.Key Key;
            internal bool Tile;
        }
        static void Log(string s) => Main.Entry.Logger.Log("[tilerender] " + s);
        static TileRender Get()
        {
            if (!instance)
            {
                instance = new GameObject("StutterFix.TileRender").AddComponent<TileRender>();
                DontDestroyOnLoad(instance.gameObject);
            }
            return instance;
        }
        internal static bool Step(string arg)
        {
            if (arg == "off") { Stop(); Log("off restored"); return true; }
            if (arg == "state")
            {
                Log("active=" + (instance && instance.commands != null) + " sampling=" + sampling
                    + " backend=frozen-original-draw live-instancing=unverified"); return true;
            }
            if (arg == "assertactive")
            {
                if (!instance || instance.commands == null) throw new InvalidOperationException("tilerender candidate is not active; comparison INVALID");
                return true;
            }
            if (arg == "census")
            {
                if (sampling) return false;
                if (sampleError != null) { var e = sampleError; sampleError = null; sampleReady = false; throw e; }
                if (sampleReady) { sampleReady = false; return true; }
                if (instance && instance.commands != null) throw new InvalidOperationException("census requires tilerender off");
                Get().StartCoroutine(Get().CensusGuard()); return false;
            }
            if (arg == "on") { Get().EnableSnapshot(); return true; }
            throw new ArgumentException("tilerender census|on|off|state|assertactive");
        }
        static bool sampleReady;
        static bool Enabled(Renderer r, Camera c) => r && r.gameObject.scene.IsValid()
            && r.gameObject.activeInHierarchy && r.enabled && !r.forceRenderingOff
            && (c.cullingMask & (1 << r.gameObject.layer)) != 0;
        static HashSet<Renderer> TileSet()
        {
            var set = new HashSet<Renderer>();
            // The wrapper also covers FloorSpriteRenderer; do not infer ownership from names.
            foreach (var f in UnityEngine.Object.FindObjectsByType<scrFloor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (f && f.floorRenderer != null && f.floorRenderer.renderer) set.Add(f.floorRenderer.renderer);
            return set;
        }
        // Full canonical content, not just Material.GetHashCode()/instance ID. Equality
        // never depends on the display hash. This enumeration is diagnostic-only.
        static string Bits(float x) => BitConverter.SingleToInt32Bits(x).ToString("X8", Inv);
        static void Vector(StringBuilder b, Vector4 v)
        { b.Append(Bits(v.x)).Append(',').Append(Bits(v.y)).Append(',').Append(Bits(v.z)).Append(',').Append(Bits(v.w)); }
        internal static string Content(Material m)
        {
            if (!m || !m.shader) return "missing";
            var s = m.shader;
            var b = new StringBuilder().Append(s.GetInstanceID()).Append('|').Append(m.renderQueue)
                .Append('|').Append(m.enableInstancing).Append('|').Append(m.doubleSidedGI)
                .Append('|').Append((int)m.globalIlluminationFlags).Append('|');
            var words = m.shaderKeywords; Array.Sort(words, StringComparer.Ordinal);
            foreach (var w in words) b.Append(w.Length).Append(':').Append(w).Append(';');
            for (int p = 0; p < m.passCount; p++) b.Append("pass:").Append(p).Append('=').Append(m.GetShaderPassEnabled(m.GetPassName(p))).Append(';');
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                int id = s.GetPropertyNameId(i); string name = s.GetPropertyName(i);
                b.Append('|').Append(name.Length).Append(':').Append(name).Append('=');
                switch (s.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: Vector(b, m.GetColor(id)); break;
                    case ShaderPropertyType.Vector: Vector(b, m.GetVector(id)); break;
                    case ShaderPropertyType.Texture:
                        var t = m.GetTexture(id); b.Append(t ? t.GetInstanceID() : 0).Append(':');
                        Vector(b, m.GetTextureScale(name)); b.Append(':'); Vector(b, m.GetTextureOffset(name)); break;
                    case ShaderPropertyType.Int: b.Append(m.GetInteger(id)); break;
                    default: b.Append(Bits(m.GetFloat(id))); break;
                }
            }
            return b.ToString();
        }
        static string Hash(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
            return h.ToString("X16", Inv);
        }
        static bool SameMatrix(Matrix4x4 a, Matrix4x4 b)
        { for (int i = 0; i < 16; i++) if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i])) return false; return true; }
        static List<Item> Scene(Camera c, HashSet<Renderer> tiles, out int grouped)
        {
            var result = new List<Item>(); grouped = 0;
            var planes = GeometryUtility.CalculateFrustumPlanes(c);
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!Enabled(r, c) || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                if (r.GetComponentInParent<SortingGroup>()) grouped++;
                var mesh = r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
                var mats = r.sharedMaterials;
                for (int slot = 0; slot < mats.Length; slot++)
                {
                    var m = mats[slot]; if (!m) continue;
                    result.Add(new Item { Renderer = r, Mesh = mesh ? mesh.sharedMesh : null, Material = m,
                        Matrix = r.localToWorldMatrix, Content = tiles.Contains(r) ? Content(m) : null,
                        Tile = tiles.Contains(r), Key = GpuDecoScene.SortKey(r, m, c, slot) });
                }
            }
            result.Sort((a, b) => a.Key.CompareTo(b.Key));
            return result;
        }
        IEnumerator CensusGuard()
        {
            sampling = true; sampleReady = false; sampleError = null;
            var work = Census();
            try
            {
                while (true)
                {
                    bool next;
                    try { next = work.MoveNext(); }
                    catch (Exception e) { sampleError = e; Log("census INVALID: " + e.Message); yield break; }
                    if (!next) yield break;
                    yield return work.Current;
                }
            }
            finally { (work as IDisposable)?.Dispose(); sampling = false; sampleReady = true; }
        }
        IEnumerator Census()
        {
            sampling = true; sampleError = null; sampleReady = false;
            try
            {
                yield return new WaitForEndOfFrame();
                var c = GpuDecoScene.FindMain(); var owner = scrController.instance;
                var tiles = TileSet();
                if (tiles.Count == 0) throw new InvalidOperationException("no scrFloor-owned renderers; census not valid");
                int active = tiles.Count(r => Enabled(r, c));
                int grouped; var scene = Scene(c, tiles, out grouped);
                var visible = scene.Where(x => x.Tile).ToArray();
                var materials = new HashSet<int>(); var contents = new HashSet<string>(StringComparer.Ordinal);
                var shaderKeys = new Dictionary<string, int>(); var meshes = new Dictionary<Mesh, int>();
                var seen = new HashSet<Renderer>(); int blocks = 0, sprite = 0;
                foreach (var r in tiles) if (Enabled(r, c))
                {
                    if (r.HasPropertyBlock()) blocks++;
                    if (r is SpriteRenderer) sprite++;
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh) { int n; meshes.TryGetValue(filter.sharedMesh, out n); meshes[filter.sharedMesh] = n + 1; }
                    foreach (var m in r.sharedMaterials) if (m)
                    {
                        materials.Add(m.GetInstanceID()); contents.Add(Content(m));
                        var words = m.shaderKeywords; Array.Sort(words, StringComparer.Ordinal);
                        string key = (m.shader ? m.shader.name : "missing") + ":queue=" + m.renderQueue + ":keywords=" + string.Join(",", words);
                        int n; shaderKeys.TryGetValue(key, out n); shaderKeys[key] = n + 1;
                    }
                }
                int first = scene.FindIndex(x => x.Tile), last = scene.FindLastIndex(x => x.Tile);
                int runs = 0, between = 0, ties = 0; bool prev = false;
                var intruders = new HashSet<Renderer>();
                for (int i = 0; i < scene.Count; i++)
                {
                    var x = scene[i]; if (x.Tile && !prev) runs++;
                    if (!x.Tile && i > first && i < last) { between++; intruders.Add(x.Renderer); }
                    if (i > 0 && (x.Tile || scene[i - 1].Tile) && x.Key.SameBucket(scene[i - 1].Key)) ties++;
                    prev = x.Tile;
                }
                foreach (var x in visible) seen.Add(x.Renderer);
                Log("census snapshot frame=" + Time.frameCount + " owners=" + tiles.Count + " enabled-main-mask=" + active
                    + " frustum-AABB=" + seen.Count + " slots=" + visible.Length + " mesh-kinds=" + meshes.Count
                    + " materials=" + materials.Count + " equal-content-groups=" + contents.Count + " property-block-renderers=" + blocks + " sprite-renderers=" + sprite);
                Log("census sort modeled-runs=" + runs + " intervening-slots=" + between + " intervening-renderers=" + intruders.Count
                    + " ambiguous-buckets=" + ties + " sorting-group-renderers=" + grouped
                    + " (CPU model only; AABB is not engine occlusion visibility; command-buffer/native draws are not enumerated)");
                if (visible.Length > 0) Log("census sorting-layer-value=" + visible.Min(x => x.Key.Layer) + ".." + visible.Max(x => x.Key.Layer)
                    + " order=" + visible.Min(x => x.Key.Order) + ".." + visible.Max(x => x.Key.Order));
                foreach (var p in meshes.OrderByDescending(x => x.Value).Take(10)) Log("mesh id=" + p.Key.GetInstanceID() + " name=" + p.Key.name + " count=" + p.Value);
                foreach (var p in shaderKeys.OrderByDescending(x => x.Value)) Log("shader-key " + p.Key + " count=" + p.Value);
                foreach (var group in tiles.Where(r => Enabled(r, c)).SelectMany(r => r.sharedMaterials).Where(m => m)
                    .GroupBy(Content).Take(30)) Log("material-content hash=" + Hash(group.Key) + " slots=" + group.Count() + " instances=" + group.Select(m => m.GetInstanceID()).Distinct().Count() + " canonical=" + group.Key);
                Log("material-content detail-limit=30 total-groups=" + contents.Count + " (counts include all groups)");
                foreach (var r in intruders.Take(20)) Log("intervening " + r.GetType().Name + " name=" + r.name + " layer=" + r.sortingLayerID + " order=" + r.sortingOrder);
                // Snapshot is immediate; change sampling must be 30 consecutive playing
                // frames, not 30 render callbacks during a diagnostic pause.
                if (FrameCompare.Paused || !Hitch.Playing || Time.timeScale <= 0 || (owner && owner.paused))
                { Log("census changes=UNMEASURED reason=paused-or-not-playing (run census before pause)"); yield break; }
                var tracked = tiles.Where(r => Enabled(r, c)).ToArray();
                var trackedSet = new HashSet<Renderer>(tracked);
                var matrices = tracked.Select(r => r.localToWorldMatrix).ToArray();
                var values = tracked.Select(RendererContent).ToArray();
                long transformChanges = 0, materialChanges = 0; int frame = Time.frameCount;
                for (int sample = 0; sample < 30; sample++)
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    if (!c || owner != scrController.instance || FrameCompare.Paused || !Hitch.Playing || Time.timeScale <= 0
                        || (owner && owner.paused) || Time.frameCount != frame + 1)
                        throw new InvalidOperationException("census 30-frame sample invalidated; no valid average");
                    frame = Time.frameCount;
                    if (!trackedSet.SetEquals(TileSet().Where(r => Enabled(r, c))))
                        throw new InvalidOperationException("census active owner set changed; no valid average");
                    for (int i = 0; i < tracked.Length; i++)
                    {
                        if (!Enabled(tracked[i], c)) throw new InvalidOperationException("census tracked set changed; no valid average");
                        var matrix = tracked[i].localToWorldMatrix; var content = RendererContent(tracked[i]);
                        if (!SameMatrix(matrix, matrices[i])) transformChanges++;
                        if (content != values[i]) materialChanges++;
                        matrices[i] = matrix; values[i] = content;
                    }
                }
                Log("census changes samples=30 tracked=" + tracked.Length + " transform-mean=" + (transformChanges / 30.0).ToString("F3", Inv)
                    + " material-mean=" + (materialChanges / 30.0).ToString("F3", Inv)
                    + " property-block-values=declared-properties-only (diagnostic allocations; exclude from FPS)");
            }
            finally { sampling = false; sampleReady = true; }
        }
        static string RendererContent(Renderer r)
        {
            var b = new StringBuilder(); var mats = r.sharedMaterials;
            var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block);
            AppendBlock(b, block, mats);
            for (int slot = 0; slot < mats.Length; slot++)
            {
                b.Append(Content(mats[slot])).Append('\n');
                r.GetPropertyBlock(block, slot); AppendBlock(b, block, new[] { mats[slot] });
            }
            return b.ToString();
        }
        static void AppendBlock(StringBuilder b, MaterialPropertyBlock block, Material[] mats)
        {
            b.Append("block-empty=").Append(block.isEmpty).Append(';');
            foreach (var m in mats) if (m && m.shader)
                for (int p = 0; p < m.shader.GetPropertyCount(); p++)
                {
                    int id = m.shader.GetPropertyNameId(p); if (!block.HasProperty(id)) continue;
                    b.Append(m.shader.GetPropertyName(p)).Append('=');
                    switch (m.shader.GetPropertyType(p))
                    {
                        case ShaderPropertyType.Color: Vector(b, block.GetColor(id)); break;
                        case ShaderPropertyType.Vector: Vector(b, block.GetVector(id)); break;
                        case ShaderPropertyType.Texture: var t = block.GetTexture(id); b.Append(t ? t.GetInstanceID() : 0); break;
                        case ShaderPropertyType.Int: b.Append(block.GetInteger(id)); break;
                        default: b.Append(Bits(block.GetFloat(id))); break;
                    }
                    b.Append(';');
                }
        }
        void EnableSnapshot()
        {
            if (commands != null) throw new InvalidOperationException("tilerender already on");
            if (!FrameCompare.Paused) throw new InvalidOperationException("tilerender on: frozen diagnostic only; live dirty tracking/instancing not admitted");
            var c = GpuDecoScene.FindMain(); var tiles = TileSet(); int grouped;
            var scene = Scene(c, tiles, out grouped); var draws = scene.Where(x => x.Tile).ToArray();
            if (draws.Length == 0 || grouped != 0) throw new InvalidOperationException("tilerender refuses empty or SortingGroup scene");
            if (GraphicsSettings.currentRenderPipeline != null || c.actualRenderingPath != RenderingPath.Forward
                || c.transparencySortMode != TransparencySortMode.Default || c.useOcclusionCulling)
                throw new InvalidOperationException("tilerender requires built-in forward/default sorting");
            // Only a transparent PREFIX can move to BeforeForwardAlpha. Even one
            // earlier non-tile transparent or same sort bucket makes this unsafe.
            var alpha = scene.Where(x => x.Material.renderQueue > 2500).ToArray();
            if (draws.Any(x => x.Material.renderQueue <= 2500) || alpha.Length < draws.Length
                || alpha.Take(draws.Length).Any(x => !x.Tile))
                throw new InvalidOperationException("tilerender refuses transparent interleaving/prefix barrier");
            for (int i = 1; i < alpha.Length; i++)
                if ((alpha[i].Tile || alpha[i - 1].Tile) && alpha[i].Key.SameBucket(alpha[i - 1].Key))
                    throw new InvalidOperationException("tilerender refuses ambiguous transparent sort bucket");
            foreach (var x in draws)
            {
                if (!(x.Renderer is MeshRenderer) || !x.Mesh || x.Mesh.subMeshCount != 1 || x.Renderer.sharedMaterials.Length != 1 || x.Renderer.GetComponentInParent<LODGroup>()
                    || x.Renderer.HasPropertyBlock() || !x.Material.shader || x.Material.shader.name != "Sprites/Tile" || x.Material.passCount != 1
                    || x.Renderer.shadowCastingMode != ShadowCastingMode.Off)
                    throw new InvalidOperationException("tilerender unsupported tile state: " + x.Renderer.name);
            }
            foreach (var other in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (other != c && other.enabled && draws.Any(x => (other.cullingMask & (1 << x.Renderer.gameObject.layer)) != 0))
                    throw new InvalidOperationException("tilerender refuses another camera consuming tile layers");
            if (c.GetCommandBuffers(CameraEvent.BeforeForwardAlpha).Length != 0)
                throw new InvalidOperationException("tilerender refuses existing BeforeForwardAlpha commands");
            // Original shader/material/matrix; intentionally no speculative shader
            // translation or mesh baking. This measures renderer-management ceiling,
            // retaining original draw count. It is not a live FPS optimization.
            var cb = new CommandBuffer { name = "StutterFix.TileRender.Frozen" };
            try
            {
                foreach (var x in draws) cb.DrawMesh(x.Mesh, x.Matrix, x.Material, 0, 0);
                snapshot = scene.ToArray(); view = c.worldToCameraMatrix; projection = c.projectionMatrix; cullingMask = c.cullingMask;
                camera = c; commands = cb; c.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
                foreach (var x in draws)
                { hidden.Add(x.Renderer); originals.Add(x.Renderer.forceRenderingOff); x.Renderer.forceRenderingOff = true; }
                Camera.onPreCull += PreCull;
                Log("on backend=frozen-original-draw renderers=" + hidden.Count + " draws=" + draws.Length + " instanced=0; pixel parity UNVERIFIED");
            }
            catch { if (commands == null) cb.Release(); Stop(); throw; }
        }
        void PreCull(Camera c)
        {
            try { ValidateSnapshot(c); }
            catch (Exception e) { Log("snapshot invalidated: " + e.GetType().Name); Stop(); }
        }
        void ValidateSnapshot(Camera c)
        {
            // Never leave a frozen submission alive across resume, disable, scene
            // reset or controller loss. The snapshot is valid only while paused.
            if (!FrameCompare.Paused || !scrController.instance || Main.Entry == null || !Main.Entry.Active || !camera)
            { Stop(); return; }
            if (c != camera)
            {
                for (int i = 0; i < hidden.Count; i++) if (hidden[i] && (c.cullingMask & (1 << hidden[i].gameObject.layer)) != 0)
                { Log("snapshot invalidated: another camera"); Stop(); return; }
                return;
            }
            if (!SameMatrix(camera.worldToCameraMatrix, view) || !SameMatrix(camera.projectionMatrix, projection) || camera.cullingMask != cullingMask)
            { Log("snapshot invalidated: camera changed"); Stop(); return; }
            for (int i = 0; i < snapshot.Length; i++)
            {
                var x = snapshot[i]; var r = x.Renderer;
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || !SameMatrix(r.localToWorldMatrix, x.Matrix)
                    || !x.Material || x.Material.renderQueue != x.Key.Queue || r.sortingOrder != x.Key.Order
                    || SortingLayer.GetLayerValueFromID(r.sortingLayerID) != x.Key.Layer
                    || (x.Tile && (r.sharedMaterial != x.Material || !x.Mesh || !r.GetComponent<MeshFilter>() || r.GetComponent<MeshFilter>().sharedMesh != x.Mesh || r.HasPropertyBlock())))
                { Log("snapshot invalidated: source changed"); Stop(); return; }
            }
        }
        internal static void Stop()
        {
            if (ReferenceEquals(instance, null)) return;
            Camera.onPreCull -= instance.PreCull;
            try
            {
                if (instance.camera && instance.commands != null) instance.camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, instance.commands);
            }
            finally
            {
                try { if (instance.commands != null) instance.commands.Release(); }
                finally
                {
                    instance.commands = null; instance.camera = null; instance.snapshot = null;
                    for (int i = 0; i < instance.hidden.Count; i++)
                        try { if (instance.hidden[i]) instance.hidden[i].forceRenderingOff = instance.originals[i]; }
                        catch (Exception e) { Log("RESTORE FAILED renderer-index=" + i + " " + e.Message); }
                    instance.hidden.Clear(); instance.originals.Clear();
                }
            }
        }
        internal static void Shutdown()
        { Stop(); if (instance) { instance.StopAllCoroutines(); Destroy(instance.gameObject); } instance = null; sampling = sampleReady = false; sampleError = null; }
        void OnDestroy()
        {
            // Destroy is deferred: an old object's callback must not retire a
            // replacement created after Shutdown in the same frame.
            if (ReferenceEquals(instance, this)) { Stop(); instance = null; }
        }
    }
}
#endif
