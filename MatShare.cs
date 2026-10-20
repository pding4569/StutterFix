#if DEV
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StutterFix
{
    // Post-processing experiment: no Harmony hooks and no material getter (which would clone).
    internal sealed class MatShare : MonoBehaviour
    {
        static MatShare instance;
        readonly List<Row> rows = new List<Row>();
        readonly List<Material> owned = new List<Material>();
        long reset;
        sealed class Row
        {
            internal SpriteRenderer R;
            internal scrVisualDecoration D;
            internal Material Original, Shared;
            internal string Signature, Bucket, Key, Reason;
            internal Sprite Sprite;
            internal int Layer, Order;
            internal float Distance;
        }
        static void Log(string s) => Main.Entry.Logger.Log("[matshare] " + s);
        static Camera MainCamera => scrCamera.instance ? scrCamera.instance.camobj : Camera.main;
        internal static void Command(string arg)
        {
            switch (arg.Trim())
            {
                case "off": Stop(); Log("off restored=1"); return;
                case "state": Log("on=" + (instance != null) + " renderers=" + (instance ? instance.rows.Count : 0) + " run_materials=" + (instance ? instance.owned.Count : 0)); return;
                case "census": Census(); return;
                case "compare": if (!instance) throw new InvalidOperationException("matshare on first"); instance.Compare(); return;
                case "on":
                    if (instance) return;
                    if (!ADOBase.isLevelEditor || !scnEditor.instance || scnEditor.instance.playMode || Hitch.Playing || SceneReset.Resetting)
                        throw new InvalidOperationException("open loaded editor and stop playback before matshare on");
                    if (GpuSplit.Busy || GpuHide.Busy || GpuCensus.Busy) throw new InvalidOperationException("other GPU diagnostics busy");
                    var go = new GameObject("StutterFix.MatShare") { hideFlags = HideFlags.HideAndDontSave };
                    instance = go.AddComponent<MatShare>();
                    try { instance.Begin(); } catch { Stop(); throw; }
                    return;
                default: throw new ArgumentException("matshare census|on|off|state|compare");
            }
        }
        static string F(float f) => BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture);
        static string V(Vector4 v) => F(v.x) + "," + F(v.y) + "," + F(v.z) + "," + F(v.w);
        internal static SortedDictionary<string, string> Properties(Material m)
        {
            var p = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!m || !m.shader) { p["missing"] = "1"; return p; }
            p["shader"] = m.shader.GetInstanceID().ToString(); p["queue"] = m.renderQueue.ToString();
            p["keywords"] = string.Join(",", m.shaderKeywords.OrderBy(k => k, StringComparer.Ordinal));
            p["instancing"] = m.enableInstancing.ToString(); p["doubleSidedGI"] = m.doubleSidedGI.ToString();
            p["GI"] = m.globalIlluminationFlags.ToString();
            foreach (string n in new[] { "_SrcBlend", "_DstBlend", "_BlendOp", "_ZTest", "_ZWrite", "_Cull" })
                p[n] = m.HasProperty(n) ? F(m.GetFloat(n)) : "<absent>";
            for (int i = 0; i < m.passCount; i++) p["pass:" + m.GetPassName(i)] = m.GetShaderPassEnabled(m.GetPassName(i)).ToString();
            for (int i = 0; i < m.shader.GetPropertyCount(); i++)
            {
                string n = m.shader.GetPropertyName(i); string value;
                switch (m.shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: value = V(m.GetColor(n)); break;
                    case ShaderPropertyType.Vector: value = V(m.GetVector(n)); break;
                    case ShaderPropertyType.Texture:
                        var t = m.GetTexture(n); var s = m.GetTextureScale(n); var o = m.GetTextureOffset(n);
                        value = (t ? t.GetInstanceID() : 0) + ":" + V(new Vector4(s.x,s.y,o.x,o.y)); break;
                    case ShaderPropertyType.Int: value = m.GetInteger(n).ToString(); break;
                    default: value = F(m.GetFloat(n)); break;
                }
                p[n] = value;
            }
            return p;
        }
        static string Signature(Material m) => string.Join(";", Properties(m).Select(k => k.Key.Length + ":" + k.Key + "=" + k.Value.Length + ":" + k.Value));
        static string Bucket(Renderer r, Camera cam)
        {
            return SortingLayer.GetLayerValueFromID(r.sortingLayerID) + "/" + r.sortingLayerID + "/" + r.sortingOrder + "/" + (r.sharedMaterial ? r.sharedMaterial.renderQueue : -1) + "/" + F(Distance(r,cam));
        }
        static float Distance(Renderer r, Camera cam)
        {
            Vector3 pos = r.bounds.center;
            var sr = r as SpriteRenderer;
            if (sr && sr.spriteSortPoint == SpriteSortPoint.Pivot) pos = r.transform.position;
            float distance;
            var mode = cam.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = cam.orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective;
            if (mode == TransparencySortMode.CustomAxis)
                distance = Vector3.Dot(pos, cam.transparencySortMode == TransparencySortMode.CustomAxis ? cam.transparencySortAxis : GraphicsSettings.transparencySortAxis);
            else distance = mode == TransparencySortMode.Orthographic ? Vector3.Dot(pos - cam.transform.position, cam.transform.forward) : (pos - cam.transform.position).sqrMagnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance)) throw new InvalidOperationException("nonfinite sort distance");
            return distance;
        }
        static List<Row> Scan()
        {
            var cam = MainCamera; var mgr = scrDecorationManager.instance;
            if (!cam || !mgr || SceneReset.Resetting) throw new InvalidOperationException("camera/manager not ready");
            // Every tagged decoration is conservatively dynamic, including hidden objects and unknown event types.
            // This over-excludes static tags rather than pretending an incomplete event inventory is safe.
            var tagged = new HashSet<scrDecoration>();
            if (mgr.taggedDecorations == null) throw new InvalidOperationException("tag inventory unavailable");
            foreach (var pair in mgr.taggedDecorations) foreach (var d in pair.Value) if (d) tagged.Add(d);
            var result = new List<Row>();
            foreach (var d in Resources.FindObjectsOfTypeAll<scrVisualDecoration>())
            {
                if (!d || !d.gameObject.scene.IsValid() || !d.spriteRenderer) continue;
                var r = d.spriteRenderer; var m = r.sharedMaterial;
                string reason = null;
                if (tagged.Contains(d)) reason = "tagged-potential-event";
                else if (d.GetType() != typeof(scrVisualDecoration) || d.isMask() || (d.meshRenderer && d.meshRenderer.enabled) || d.hitbox != 0 || r.GetComponentInParent<SortingGroup>() || r.maskInteraction != SpriteMaskInteraction.None) reason = "special-renderer";
                else if (!m || !m.shader || m.shader.name != "Sprites/Tile" || m.passCount != 1 || !r.sprite || r.sprite.associatedAlphaSplitTexture) reason = "unsupported-material";
                else if (d.repeatX != 1 || d.repeatY != 1 || d.blendModeEffect && d.blendModeEffect.enabled) reason = "tiling-or-blend";
                // SpriteRenderer's engine-owned MPB may be nonempty: do not reinterpret it here.
                else if (r.HasPropertyBlock()) reason = "property-block";
                var sig = Signature(m); var bucket = Bucket(r, cam);
                result.Add(new Row { R = r, D = d, Original = m, Signature = sig, Bucket = bucket,
                    Key = sig + "/spriteTexture=" + (r.sprite && r.sprite.texture ? r.sprite.texture.GetInstanceID() : 0),
                    Reason = reason, Sprite = r.sprite, Layer = r.sortingLayerID, Order = r.sortingOrder, Distance = Distance(r,cam) });
            }
            return result.OrderBy(x => SortingLayer.GetLayerValueFromID(x.Layer)).ThenBy(x => x.Order)
                .ThenBy(x => x.Original ? x.Original.renderQueue : -1).ThenByDescending(x => x.Distance)
                .ThenBy(x => x.Original ? x.Original.GetInstanceID() : 0).ThenBy(x => x.R.GetInstanceID()).ToList();
        }
        static bool Live(Row x) => x.R && x.R.enabled && x.R.gameObject.activeInHierarchy && !x.R.forceRenderingOff;
        static int RunCount(IEnumerable<Row> source)
        {
            var a = source.ToArray(); var ids = MatSharePolicy.Runs(a.Select(x => x.Bucket + "/" + x.Key).ToArray(), new bool[a.Length]);
            return ids.Length == 0 ? 0 : ids[ids.Length - 1] + 1;
        }
        static void Census()
        {
            if (instance) throw new InvalidOperationException("census requires matshare off");
            var all = Scan(); var live = all.Where(Live).ToArray();
            Log("census images_all=" + all.Count + " live=" + live.Length + " material_ids=" + all.Where(x => x.Original).Select(x => x.Original.GetInstanceID()).Distinct().Count()
                + " signatures=" + all.Select(x => x.Signature).Distinct().Count() + " runs_all=" + RunCount(all) + " runs_live=" + RunCount(live)
                + " estimate_only=1 excludes_nonimage_interleaving=1");
            foreach (var g in all.GroupBy(x => x.Signature).OrderByDescending(x => x.Count()).Take(10))
                Log("signature count=" + g.Count() + " content=" + g.Key);
            var props = all.Where(x => x.Original).Select(x => Properties(x.Original)).ToArray();
            foreach (var p in props.SelectMany(x => x.Keys).Distinct().Select(k => new { Key = k, Count = props.Select(x => x.TryGetValue(k,out var v) ? v : "<absent>").Distinct().Count() }).OrderByDescending(x => x.Count).Take(5))
                Log("split_property=" + p.Key + " distinct_values=" + p.Count);
            foreach (var g in all.GroupBy(x => x.Reason ?? "candidate")) Log("eligibility=" + g.Key + " count=" + g.Count());
        }
        void Begin()
        {
            var all = Scan(); reset = SceneReset.Count;
            var external = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r && r.gameObject.scene.IsValid()).ToArray();
            foreach (var group in all.GroupBy(x => x.Bucket))
            {
                var a = group.ToArray(); var set = new HashSet<Renderer>(a.Select(x => (Renderer)x.R));
                // Fresh IDs cannot straddle unchanged material IDs. Keep the WHOLE tied bucket original if any barrier exists.
                if (a.Any(x => x.Reason != null) || external.Any(r => !set.Contains(r) && Bucket(r, MainCamera) == group.Key)) continue;
                var run = MatSharePolicy.Runs(a.Select(x => x.Key).ToArray(), new bool[a.Length]);
                int count = run[run.Length - 1] + 1;
                if (count == a.Length) continue;
                var mats = new Material[count];
                for (int i = 0; i < count; i++) { mats[i] = new Material(a[Array.IndexOf(run,i)].Original) { hideFlags = HideFlags.HideAndDontSave }; owned.Add(mats[i]); }
                // Read actual signed IDs: recycling/allocator direction never determines the assignment.
                var order = MatSharePolicy.MaterialOrder(mats.Select(x => x.GetInstanceID()).ToArray());
                for (int i = 0; i < a.Length; i++)
                {
                    var x = a[i]; x.Shared = mats[order[run[i]]];
                    if (Signature(x.Shared) != x.Signature) throw new InvalidOperationException("clone signature mismatch");
                    rows.Add(x); x.R.sharedMaterial = x.Shared;
                }
            }
            if (rows.Count == 0) throw new InvalidOperationException("no complete eligible tied bucket; all materials unchanged");
            Log("on renderers=" + rows.Count + " runs=" + owned.Count + " original_materials=" + rows.Select(x => x.Original.GetInstanceID()).Distinct().Count() + " default_off=1 experimental=1");
        }
        void LateUpdate()
        {
            // Lifecycle/foreign writes: restore before subsequent frames. Not a guarantee against later callbacks.
            if (SceneReset.Resetting || SceneReset.Count != reset || !scrDecorationManager.instance) { Stop(); return; }
            foreach (var x in rows)
                if (!x.R || !x.D || x.R.sharedMaterial != x.Shared || x.R.sprite != x.Sprite || x.R.sortingLayerID != x.Layer || x.R.sortingOrder != x.Order
                    || x.D.repeatX != 1 || x.D.repeatY != 1) { Log("foreign/dynamic change; stopping"); Stop(); return; }
        }
        internal static void Stop()
        {
            var p = instance; instance = null; if (!p) return;
            p.Restore(); Destroy(p.gameObject);
        }
        void Restore()
        {
            foreach (var x in rows) if (x.R)
            {
                if (x.Original) x.R.sharedMaterial = x.Original;
                else if (x.Shared)
                {
                    // Foreign code explicitly destroyed the source. Preserve a usable per-renderer material.
                    x.R.sharedMaterial = new Material(x.Shared);
                    Log("restore fallback clone renderer=" + x.R.GetInstanceID() + " original_destroyed=1 original_id_order_not_recoverable=1");
                }
            }
            rows.Clear(); foreach (var m in owned) if (m) Destroy(m); owned.Clear();
        }
        void OnDestroy() { Restore(); if (instance == this) instance = null; }
        void Compare() => MatShareCompare.Run(rows.Where(Live).Select(x => x.R).ToArray(),
            r => rows.First(x => x.R == r).Original, r => rows.First(x => x.R == r).Shared, MainCamera);
    }
}
#endif
