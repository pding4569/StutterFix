#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StutterFix
{
    // Own objects only. One complete trial per AutoTest poll; no persistent Unity objects.
    internal static class SortProbe
    {
        static readonly string[] Cases = { "creation", "creation-reverse", "siblings", "parents",
            "z-small", "z-large", "materials", "queue", "textures", "sprites",
            "enable", "force-off", "move", "reparent", "mesh-first", "mesh-last", "mixed-random" };
        static readonly string[] Hypotheses = { "id-asc", "id-desc", "creation-asc", "creation-desc",
            "sibling-asc", "sibling-desc", "distance-id", "distance-sibling", "queue-distance-id", "material-id", "mesh-before-sprite", "priority-asc", "priority-desc" };
        static readonly string[] PriorityCases = { "priority-unc" };
        static readonly string[] MeshCases = { "mesh-material-below", "mesh-material-above", "mesh-material-six" };
        static readonly List<string> invalidReasons = new List<string>();
        static string observationPhase;
        static Run current;
        sealed class Run
        {
            internal int Repeat, Seed, Index;
            internal string Directory;
            internal bool MeshMaterial, Priority;
            internal readonly List<string> Invalid = new List<string>();
            internal readonly Dictionary<string, int[]> Counts = new Dictionary<string, int[]>();
        }
        sealed class Item
        {
            internal Renderer R;
            internal int Label, Creation;
            internal Color Color;
        }
        static void Log(string text) { Main.Entry.Logger.Log("[sortprobe] " + text); }
        internal static void Cancel() { if (current != null) Finish("cancelled: AutoTest finished"); }
        static bool EditorReady()
        { return ADOBase.isLevelEditor && scnEditor.instance != null && !scnEditor.instance.playMode; }

        internal static bool Step(string arg, string modDir)
        {
            arg = arg.Trim().ToLowerInvariant();
            if (arg == "stop") { if (current != null) Finish("cancelled"); return true; }
            if (arg == "state") { Log(current == null ? "idle" : "trial=" + current.Index); return true; }
            if (current == null)
            {
                var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int repeat = 50, seed = 20261020;
                if (a.Length < 1 || (a[0] != "all" && a[0] != "mesh-material" && a[0] != "priority") || a.Length > 3 ||
                    (a.Length > 1 && (!int.TryParse(a[1], out repeat) || repeat < 50 || repeat > 200)) ||
                    (a.Length > 2 && !int.TryParse(a[2], out seed)))
                { Log("sortprobe all|mesh-material [repeats=50,50..200] [seed=20261020]|state|stop"); return true; }
                if (!EditorReady()) { Log("REFUSED: open editor and stop playback first"); return true; }
                try
                {
                    string dir = Path.Combine(modDir, "sortprobe", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "trials.csv"),
                        "case,n,trial,seed,initial_top,pre_mutation_top,post_mutation_top,before_back_to_front,after_back_to_front,changed,stable,valid,details\n", Encoding.UTF8);
                    current = new Run { Repeat = repeat, Seed = seed, Directory = dir, MeshMaterial = a[0] == "mesh-material", Priority = a[0] == "priority" };
                    File.WriteAllText(Path.Combine(dir, "environment.txt"),
                        "unity=" + Application.unityVersion + "\ngpu=" + SystemInfo.graphicsDeviceName + "\napi=" + SystemInfo.graphicsDeviceType +
                        "\nseed=" + seed + "\nrepeat=" + repeat + "\nmvid=" + typeof(Main).Module.ModuleVersionId +
                        "\nSynthetic Sprites/Tile; opaque vertex colors; isolated unused layer; no game camera callbacks copied.\n", Encoding.UTF8);
                    Log("begin " + dir);
                }
                catch (Exception e) { Log("FAILED: " + e); current = null; return true; }
            }
            try
            {
                if (!EditorReady()) { Finish("aborted: editor/playback changed"); return true; }
                int perCase = current.Repeat * (current.MeshMaterial ? 1 : 3);
                int ci = current.Index / perCase, n = current.MeshMaterial ? (ci == 2 ? 6 : 2) : new[] { 2, 3, 6 }[(current.Index % perCase) / current.Repeat];
                int trial = current.Index % current.Repeat;
                var cases = current.MeshMaterial ? MeshCases : current.Priority ? PriorityCases : Cases;
                Trial(cases[ci], n, trial);
                current.Index++;
                if (current.Index % current.Repeat == 0) Log(cases[ci] + " n=" + n + " complete");
                if (current.Index == cases.Length * perCase) { Finish("complete"); return true; }
                return false;
            }
            catch (Exception e) { Finish("FAILED: " + e); return true; }
        }

        static T Own<T>(List<Object> owned, T obj) where T : Object { owned.Add(obj); return obj; }
        static GameObject Go(List<Object> owned, string name, int layer)
        { var go = Own(owned, new GameObject("SortProbe." + name)); go.hideFlags = HideFlags.HideAndDontSave; go.layer = layer; return go; }
        static void Shuffle<T>(T[] a, System.Random rng)
        { for (int i = a.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); T t = a[i]; a[i] = a[j]; a[j] = t; } }
        static Texture2D Texture(List<Object> owned)
        {
            var t = Own(owned, new Texture2D(8, 8, TextureFormat.RGBA32, false));
            t.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray()); t.Apply(); t.filterMode = FilterMode.Point; return t;
        }
        static Sprite Sprite(List<Object> owned, Texture2D t)
        { return Own(owned, UnityEngine.Sprite.Create(t, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f), 4, 0, SpriteMeshType.FullRect)); }
        static Mesh Quad(List<Object> owned, Color c)
        {
            var m = Own(owned, new Mesh());
            m.vertices = new[] { new Vector3(-1,-1), new Vector3(-1,1), new Vector3(1,1), new Vector3(1,-1) };
            m.uv = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
            m.colors = new[] { c, c, c, c }; m.triangles = new[] { 0, 1, 2, 0, 2, 3 }; m.RecalculateBounds(); return m;
        }
        static Material Material(List<Object> owned, Shader shader, Texture2D t)
        {
            var m = Own(owned, new Material(shader)); m.mainTexture = t; m.renderQueue = 3000;
            foreach (string p in new[] { "_Color", "_RendererColor" }) if (m.HasProperty(p)) m.SetColor(p, Color.white);
            return m;
        }

        static void Trial(string name, int n, int trial)
        {
            invalidReasons.Clear(); observationPhase = "setup";
            var owned = new List<Object>();
            var previous = RenderTexture.active; bool srgb = GL.sRGBWrite;
            try
            {
                Camera source = Camera.allCameras.FirstOrDefault(c => c && c.isActiveAndEnabled && c.orthographic);
                if (!source) throw new InvalidOperationException("active orthographic camera required");
                // Refuse a contaminated layer rather than changing any scene camera/renderer.
                var scene = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int layer = -1;
                for (int l = 31; l >= 0; l--) if (!scene.Any(r => r && r.gameObject.layer == l)) { layer = l; break; }
                if (layer < 0) throw new InvalidOperationException("no renderer-free layer");
                var shader = Shader.Find("Sprites/Tile");
                if (!shader || !shader.isSupported) throw new InvalidOperationException("Sprites/Tile unavailable; no fallback");
                var root = Go(owned, "root", layer);
                var p0 = Go(owned, "parent0", layer).transform; p0.SetParent(root.transform, false);
                var p1 = Go(owned, "parent1", layer).transform; p1.SetParent(root.transform, false);
                var cam = Go(owned, "camera", layer).AddComponent<Camera>(); cam.enabled = false;
                cam.orthographic = true; cam.orthographicSize = 2; cam.aspect = 1;
                cam.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                cam.nearClipPlane = .1f; cam.farClipPlane = 100;
                cam.transparencySortMode = source.transparencySortMode; cam.transparencySortAxis = source.transparencySortAxis;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.magenta;
                cam.cullingMask = 1 << layer; cam.allowHDR = false; cam.allowMSAA = false;
                cam.useOcclusionCulling = false;
                var rt = Own(owned, new RenderTexture(16, 16, 24, RenderTextureFormat.ARGB32)); rt.Create(); cam.targetTexture = rt;
                var read = Own(owned, new Texture2D(3, 3, TextureFormat.RGBA32, false));
                var tex = Texture(owned); var sprite = Sprite(owned, tex); var shared = Material(owned, shader, tex);
                int seed = unchecked(current.Seed + current.Index * 7919);
                var rng = new System.Random(seed); var order = Enumerable.Range(0, n).ToArray();
                Shuffle(order, rng);
                if (name == "creation") order = Enumerable.Range(0, n).ToArray();
                if (name == "creation-reverse") order = Enumerable.Range(0, n).Reverse().ToArray();
                int meshLabel = rng.Next(n);
                if (name == "mesh-first" || name == "mesh-last")
                    order = order.Where(x => x != meshLabel).Concat(new[] { meshLabel }).ToArray();
                if (name == "mesh-first") Array.Reverse(order);
                var items = new Item[n];
                for (int k = 0; k < n; k++)
                {
                    int label = order[k];
                    // White/black pair; six separated gray levels for unambiguous solo-calibrated decoding.
                    float gray = n == 2 ? label : label / (float)(n - 1);
                    Color color = new Color(gray, gray, gray, 1);
                    var go = Go(owned, "item" + label, layer); go.transform.SetParent(p0, false);
                    go.transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * 10, cam.transform.rotation);
                    Renderer r;
                    bool mesh = ((name == "mesh-first" || name == "mesh-last") && label == meshLabel) || (name == "mixed-random" && rng.Next(2) == 0)
                        || (name.StartsWith("mesh-material-", StringComparison.Ordinal) && label < n / 2);
                    if (mesh) { go.AddComponent<MeshFilter>().sharedMesh = Quad(owned, color); r = go.AddComponent<MeshRenderer>(); }
                    else { var s = go.AddComponent<SpriteRenderer>(); s.sprite = sprite; s.color = color; r = s; }
                    r.sharedMaterial = shared; r.sortingLayerID = 0; r.sortingOrder = 1;
                    items[label] = new Item { R = r, Label = label, Creation = k, Color = color };
                }
                var targets = Enumerable.Range(0, n).ToArray(); Shuffle(targets, rng);
                // Static cases change exactly one family of properties; dynamic cases are measured before/after.
                for (int k = 0; k < n; k++)
                {
                    var x = items[targets[k]];
                    if (name == "siblings") x.R.transform.SetSiblingIndex(k);
                    if (name == "parents") x.R.transform.SetParent(k % 2 == 0 ? p0 : p1, true);
                    if (name == "z-small" || name == "z-large")
                    { var v = x.R.transform.position; v.z += (k - (n-1)*.5f) * (name == "z-small" ? .002f : .2f); x.R.transform.position = v; }
                    if (name == "materials" || name == "queue" || name == "mixed-random")
                    {
                        x.R.sharedMaterial = Material(owned, shader, tex);
                        if (name == "queue") x.R.sharedMaterial.renderQueue = 3000 + k;
                    }
                    if (name == "priority-unc") x.R.rendererPriority = k;
                    if (name == "priority" || name == "priority-materials" || name == "priority-z")
                    {
                        x.R.rendererPriority = k;   // targets 는 섞인 순서라 k 와 label 은 무관: 우선순위 순열
                        if (name == "priority-materials") x.R.sharedMaterial = Material(owned, shader, tex);
                        if (name == "priority-z") { var v = x.R.transform.position; v.z += (n-1-k) * .2f; x.R.transform.position = v; }   // 거리 순서는 우선순위와 반대로
                    }
                    if (name == "textures" && x.R is SpriteRenderer ts) ts.sprite = Sprite(owned, Texture(owned));
                    if (name == "sprites" && x.R is SpriteRenderer s) s.sprite = Sprite(owned, tex);
                    if (name == "mixed-random")
                    { x.R.transform.SetSiblingIndex(rng.Next(n)); x.R.transform.position += cam.transform.forward * (rng.Next(3)-1)*.1f; }
                }
                if (name == "priority-unc")
                {
                    // 우선순위(targets 순열)와 무관한 별도 순열로 재질을 만든다: 재질 ID 순서와 우선순위 상관을 끊는다.
                    var perm2 = Enumerable.Range(0, n).ToArray(); Shuffle(perm2, rng);
                    foreach (int label in perm2) items[label].R.sharedMaterial = Material(owned, shader, tex);
                }
                var details = new StringBuilder();
                bool placement = true;
                if (name.StartsWith("mesh-material-", StringComparison.Ordinal))
                {
                    // Random assignment breaks the correlation between creation/type and material IDs.
                    foreach (int label in targets) items[label].R.sharedMaterial = Material(owned, shader, tex);
                    if (n == 2)
                    {
                        placement = false;
                        for (int attempt = 1; attempt <= 64; attempt++)
                        {
                            int meshId = items[0].R.sharedMaterial.GetInstanceID(), spriteId = items[1].R.sharedMaterial.GetInstanceID();
                            bool match = name == "mesh-material-below" ? meshId < spriteId : meshId > spriteId;
                            details.Append("placement-attempt=").Append(attempt).Append(":mesh-mat=").Append(meshId)
                                .Append(":sprite-mat=").Append(spriteId).Append(":matched=").Append(match).Append(';');
                            if (match) { placement = true; break; }
                            if (attempt == 64) break;
                            // Replace alternating sides; never assume newly allocated IDs increase.
                            int side = attempt % 2;
                            var old = items[side].R.sharedMaterial;
                            items[side].R.sharedMaterial = Material(owned, shader, tex);
                            Object.DestroyImmediate(old);
                        }
                    }
                }
                if (current.Index == 0)
                    File.AppendAllText(Path.Combine(current.Directory, "environment.txt"), "source_camera=" + source.name +
                        "\nsource_sort=" + source.transparencySortMode + "\nsource_axis=" + source.transparencySortAxis +
                        "\nglobal_sort=" + GraphicsSettingsMode() + "\nprobe_camera=orthographic,size2,near0.1,far100,16x16,depth24,MSAA1\n", Encoding.UTF8);
                // Record the untouched group's first draw BEFORE calibration toggles visibility.
                Color32 initialPixel = Pixel(cam, rt, read);
                // Calibrate shader output, including color-space conversion, on each isolated renderer.
                var colors = new Color32[n];
                for (int k = 0; k < n; k++) { foreach (var x in items) x.R.enabled = x.Label == k; colors[k] = Pixel(cam, rt, read); }
                foreach (var x in items) x.R.enabled = true;
                bool valid = true;
                if (!placement) Invalid(ref valid, "material-ID placement failed after 64 attempts; requested case not satisfied");
                for (int a = 0; a < n; a++) for (int b = a+1; b < n; b++) if (Difference(colors[a], colors[b]) < 15) Invalid(ref valid, "solo colors too close: " + a + "/" + b);
                observationPhase = "initial";
                int initialTop = Decode(items, initialPixel, colors, ref valid);
                observationPhase = "before";
                int[] before = Observe(items, cam, rt, read, colors, ref valid);
                if (initialTop != before[n-1]) Invalid(ref valid, "initial top differs from before order: " + initialTop + "/" + before[n-1]);
                int preMutationTop = Winner(items,cam,rt,read,colors,ref valid);
                var mutation = (Item[])items.Clone(); Shuffle(mutation, rng);
                foreach (var x in mutation)
                {
                    if (name == "enable") { x.R.enabled = false; cam.Render(); x.R.enabled = true; }
                    if (name == "force-off") { x.R.forceRenderingOff = true; cam.Render(); x.R.forceRenderingOff = false; }
                    if (name == "move") { var v = x.R.transform.position; x.R.transform.position = v + cam.transform.right * 5; cam.Render(); x.R.transform.position = v; }
                    if (name == "reparent") x.R.transform.SetParent(x.R.transform.parent == p0 ? p1 : p0, true);
                }
                observationPhase = "after";
                int postMutationTop = Winner(items,cam,rt,read,colors,ref valid);
                int[] after = Observe(items, cam, rt, read, colors, ref valid);
                if (postMutationTop != after[n-1]) Invalid(ref valid, "post-mutation top differs from after order: " + postMutationTop + "/" + after[n-1]);
                bool stable = true;
                for (int k = 0; k < 3; k++)
                {
                    observationPhase = "repeat-" + k;
                    bool same = after.SequenceEqual(Observe(items, cam, rt, read, colors, ref valid)); stable &= same;
                    if (!same) Invalid(ref valid, "repeated no-mutation order differs from after order");
                }
                foreach (var x in items)
                    details.AppendFormat(CultureInfo.InvariantCulture, "label={0}:kind={1}:id={2}:created={3}:parent={4}:sibling={5}:z={6:R}:distance={7:R}:mat={8}:queue={9}:sprite={10}:tex={11};",
                        x.Label, x.R.GetType().Name, x.R.GetInstanceID(), x.Creation, x.R.transform.parent.name,
                        x.R.transform.GetSiblingIndex(), x.R.transform.position.z, Distance(x, cam), x.R.sharedMaterial.GetInstanceID(),
                        x.R.sharedMaterial.renderQueue, x.R is SpriteRenderer sp ? sp.sprite.GetInstanceID() : 0,
                        x.R is SpriteRenderer st ? st.sprite.texture.GetInstanceID() : x.R.sharedMaterial.mainTexture.GetInstanceID());
                string key = name + "/" + n;
                if (!current.Counts.TryGetValue(key, out var counts)) current.Counts[key] = counts = new int[5 + Hypotheses.Length];
                counts[0]++; if (valid) counts[1]++; if (valid && stable) counts[2]++;
                if (valid && !before.SequenceEqual(after)) counts[3]++;
                // A repeated no-mutation discrepancy is evidence of instability even if later reads settle.
                if (valid && !new[] { "enable", "force-off", "move", "reparent" }.Contains(name) && !before.SequenceEqual(after)) counts[4]++;
                for (int h = 0; h < Hypotheses.Length; h++)
                    if (valid && (h == 10 ? MeshBeforeSprite(items, after) : after.SequenceEqual(Predict(items, cam, h)))) counts[5+h]++;
                if (!valid)
                {
                    string reason = string.Join("; ", invalidReasons);
                    details.Append("invalid-reasons=").Append(reason).Append(';');
                    current.Invalid.Add("- " + name + " n=" + n + " trial=" + trial + " seed=" + seed + ": " + reason
                        + "; before=" + Join(before) + "; after=" + Join(after)
                        + "; label:kind:material=" + string.Join(" ", items.Select(x => x.Label + ":" + x.R.GetType().Name + ":" + x.R.sharedMaterial.GetInstanceID())));
                }
                File.AppendAllText(Path.Combine(current.Directory, "trials.csv"),
                    string.Join(",", name, n, trial, seed, initialTop, preMutationTop, postMutationTop, Quote(Join(before)), Quote(Join(after)), !before.SequenceEqual(after), stable, valid, Quote(details.ToString())) + "\n", Encoding.UTF8);
            }
            finally
            {
                RenderTexture.active = previous; GL.sRGBWrite = srgb;
                // Destroy only objects created by this trial, dependents before their resources.
                foreach (var go in owned.OfType<GameObject>()) if (go) Object.DestroyImmediate(go);
                for (int k = owned.Count-1; k >= 0; k--) if (owned[k] && !(owned[k] is GameObject)) Object.DestroyImmediate(owned[k]);
            }
        }

        static string GraphicsSettingsMode()
        { return UnityEngine.Rendering.GraphicsSettings.transparencySortMode + "/" + UnityEngine.Rendering.GraphicsSettings.transparencySortAxis; }
        static float Distance(Item x, Camera cam)
        {
            var mode = cam.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = UnityEngine.Rendering.GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.CustomAxis)
                return Vector3.Dot(x.R.bounds.center, cam.transparencySortMode == TransparencySortMode.Default ? UnityEngine.Rendering.GraphicsSettings.transparencySortAxis : cam.transparencySortAxis);
            return mode == TransparencySortMode.Perspective ? Vector3.Distance(x.R.bounds.center, cam.transform.position) : Vector3.Dot(x.R.bounds.center - cam.transform.position, cam.transform.forward);
        }
        static int[] Predict(Item[] items, Camera cam, int h)
        {
            return items.OrderBy(x => x, Comparer<Item>.Create((a,b) => {
                int v = 0;
                if (h == 10) v = (a.R is MeshRenderer ? 0 : 1).CompareTo(b.R is MeshRenderer ? 0 : 1);
                if (h == 11) v = a.R.rendererPriority.CompareTo(b.R.rendererPriority);
                if (h == 12) v = b.R.rendererPriority.CompareTo(a.R.rendererPriority);
                if (h == 8) v = a.R.sharedMaterial.renderQueue.CompareTo(b.R.sharedMaterial.renderQueue);
                if (v == 0 && h >= 6 && h <= 8) v = Distance(b,cam).CompareTo(Distance(a,cam));
                if (v == 0 && h == 9) v = a.R.sharedMaterial.GetInstanceID().CompareTo(b.R.sharedMaterial.GetInstanceID());
                if (v == 0) switch (h) {
                    case 1: v = b.R.GetInstanceID().CompareTo(a.R.GetInstanceID()); break;
                    case 2: v = a.Creation.CompareTo(b.Creation); break;
                    case 3: v = b.Creation.CompareTo(a.Creation); break;
                    case 4: case 7: v = a.R.transform.GetSiblingIndex().CompareTo(b.R.transform.GetSiblingIndex()); break;
                    case 5: v = b.R.transform.GetSiblingIndex().CompareTo(a.R.transform.GetSiblingIndex()); break;
                    default: v = a.R.GetInstanceID().CompareTo(b.R.GetInstanceID()); break;
                }
                return v == 0 ? a.R.GetInstanceID().CompareTo(b.R.GetInstanceID()) : v;
            })).Select(x => x.Label).ToArray();
        }
        static bool MeshBeforeSprite(Item[] items, int[] order)
        {
            bool spriteSeen = false;
            foreach (int label in order)
                if (items[label].R is SpriteRenderer) spriteSeen = true;
                else if (items[label].R is MeshRenderer && spriteSeen) return false;
            return true;
        }
        static Color32 Pixel(Camera cam, RenderTexture rt, Texture2D read)
        {
            cam.Render(); RenderTexture.active = rt; read.ReadPixels(new Rect(7,7,3,3),0,0,false); read.Apply(false);
            var pixels = read.GetPixels32(); var c = pixels[4];
            if (pixels.Any(x => Difference(x,c) > 4)) throw new InvalidOperationException("nonuniform center patch");
            return c;
        }
        static int Difference(Color32 a, Color32 b)
        { return Math.Abs(a.r-b.r) + Math.Abs(a.g-b.g) + Math.Abs(a.b-b.b) + Math.Abs(a.a-b.a); }
        static int Winner(Item[] items, Camera cam, RenderTexture rt, Texture2D read, Color32[] colors, ref bool valid)
        {
            return Decode(items, Pixel(cam,rt,read), colors, ref valid);
        }
        static int Decode(Item[] items, Color32 c, Color32[] colors, ref bool valid)
        {
            int winner = -1, matches = 0;
            foreach (var x in items) if (x.R.enabled && Difference(c,colors[x.Label]) <= 6) { winner = x.Label; matches++; }
            if (matches != 1) Invalid(ref valid, "pixel decode matches=" + matches);
            return winner;
        }
        static int[] Observe(Item[] items, Camera cam, RenderTexture rt, Texture2D read, Color32[] colors, ref bool valid)
        {
            // Pairwise tournament, checked against full-group top and a peeling reconstruction.
            // Any visibility-dependent reorder/transitivity failure is invalid, never silently a rule.
            foreach (var x in items) x.R.enabled = true;
            int top = Winner(items,cam,rt,read,colors,ref valid);
            var wins = new int[items.Length];
            for (int a = 0; a < items.Length; a++) for (int b = a+1; b < items.Length; b++)
            {
                foreach (var x in items) x.R.enabled = x.Label == a || x.Label == b;
                int w = Winner(items,cam,rt,read,colors,ref valid);
                if (w >= 0) wins[w]++;
            }
            var order = items.OrderBy(x => wins[x.Label]).Select(x => x.Label).ToArray();
            for (int k = 0; k < items.Length; k++) if (wins[order[k]] != k) Invalid(ref valid, "pairwise non-transitive wins=" + string.Join(" ", wins));
            foreach (var x in items) x.R.enabled = true;
            if (top != order[order.Length-1]) Invalid(ref valid, "full-group top differs from pairwise top");
            for (int k = order.Length-1; k >= 0; k--)
            {
                if (Winner(items,cam,rt,read,colors,ref valid) != order[k]) Invalid(ref valid, "peeling differs from pairwise order at " + k);
                items[order[k]].R.enabled = false;
            }
            foreach (var x in items) x.R.enabled = true;
            if (Winner(items,cam,rt,read,colors,ref valid) != top) Invalid(ref valid, "full-group top changed after visibility toggles");
            return order;
        }
        static string Join(int[] order) { return string.Join(" ", order); }
        static void Invalid(ref bool valid, string reason)
        {
            valid = false; reason = observationPhase + ": " + reason;
            if (!invalidReasons.Contains(reason)) invalidReasons.Add(reason);
        }
        static string Quote(string s) { return "\"" + s.Replace("\"", "\"\"") + "\""; }
        static void Finish(string status)
        {
            var r = current; current = null;
            if (r == null) return;
            var s = new StringBuilder("# sortprobe runtime report\n\nStatus: " + status + "\n\n");
            s.AppendLine("Order = back to front. Rates use valid trials only; invalid trials are excluded explicitly. A 100% match is bounded evidence, not an engine guarantee.");
            s.AppendLine("mesh-before-sprite hypothesis checks all mesh/sprite cross pairs (mesh behind sprite); within-type ties are unconstrained. material-id checks full signed material-ID order.");
            s.AppendLine("\n|case/n|total|valid|stable|changed|no-mutation changed|" + string.Join("|", Hypotheses) + "|");
            s.AppendLine("|---|---:|---:|---:|---:|---:|" + string.Join("|", Hypotheses.Select(_ => "---:")) + "|");
            foreach (var kv in r.Counts)
            {
                var c = kv.Value;
                s.Append("|").Append(kv.Key).Append('|').Append(c[0]).Append('|').Append(c[1]);
                foreach (int i in new[] { 2,3,4 }.Concat(Enumerable.Range(5,Hypotheses.Length)))
                    s.Append('|').Append(c[1] == 0 ? "N/A" : (100.0*c[i]/c[1]).ToString("F2",CultureInfo.InvariantCulture) + "% (" + c[i] + "/" + c[1] + ")");
                s.AppendLine("|");
            }
            s.AppendLine("## 무효 표본 원인 (표본당 한 줄)\n" + (r.Invalid.Count == 0 ? "없음" : string.Join("\n", r.Invalid)) + "\n");
            s.AppendLine("\n## 표본 내 가장 간단한 설명 후보\n");
            foreach (var kv in r.Counts)
            {
                var c = kv.Value;
                var fits = Enumerable.Range(0,Hypotheses.Length).Where(h => c[1] > 0 && c[5+h] == c[1]).Select(h => Hypotheses[h]).ToArray();
                s.AppendLine("- " + kv.Key + ": " + (c[0] != c[1] || c[2] != c[1] || c[4] > 0 ? "유효성/안정성 미달; 규칙 미확정" :
                    fits.Length == 1 ? "이 표본의 유일한 100% 후보: " + fits[0] : fits.Length > 1 ? "100% 후보 다수; 구별 불가: " + string.Join(", ",fits) : "100% 후보 없음; 규칙 미확정"));
            }
            int totalValid = r.Counts.Values.Sum(c => c[1]), idMatches = r.Counts.Values.Sum(c => c[5]);
            s.AppendLine("\ndecorender instanceID 오름차순 가설: " + idMatches + "/" + totalValid + " valid observations. " +
                (totalValid == 0 ? "판정 불가." : idMatches < totalValid ? "전체 케이스를 설명하지 못함. 같은 거리/재질/queue 케이스별 표를 따로 확인할 것." : "표본 내 일치; 보편 규칙은 미확정."));
            s.AppendLine("\n가설은 관측과의 일치율이다. 동률 가설이 여럿이면 규칙은 미확정. invalid>0 또는 stable<100%이면 결정적 규칙으로 결론내리지 않는다.");
            s.AppendLine("전체 순서는 pairwise tournament + full-group top + peeling으로 교차 검사한다. 검사 자체가 enabled 상태를 바꾸므로 native 초기 삽입 순서 및 프레임을 넘긴 동적 갱신은 이 시험만으로 확정할 수 없다.");
            s.AppendLine("고정 seed 재실행과 다른 seed/새 프로세스 결과를 비교해야 실행 간 비결정성을 평가할 수 있다. MeshRenderer 한 개의 정렬 키로 외부 SpriteRenderer를 메시 내부 삼각형 사이에 삽입할 수는 없다. 실제 장식 재질/카메라 compare는 별도 검증이다.");
            try { File.WriteAllText(Path.Combine(r.Directory,"report.md"),s.ToString(),Encoding.UTF8); Log(status + " trials=" + r.Index + " report=" + Path.Combine(r.Directory,"report.md")); }
            catch (Exception e) { Log("report write FAILED: " + e); }
        }
    }
}
#endif
