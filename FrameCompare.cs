#if DEV || AUTOTEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Explicit diagnostic only. Never calls Camera.Render or replaces the camera chain.
    internal sealed class FrameCompare : MonoBehaviour
    {
        sealed class Shot
        {
            internal byte[] Pixels;
            internal int Width, Height, Frame;
            internal ulong State;
            internal string Mode;
        }
        static FrameCompare instance;
        internal static bool Paused { get; private set; }
        static scrController controller;
        static bool oldPaused, oldAudio;
        static float oldTime, oldTween;
        readonly Dictionary<string, Shot> shots = new Dictionary<string, Shot>(StringComparer.Ordinal);
        readonly Dictionary<Behaviour, bool> filters = new Dictionary<Behaviour, bool>();
        readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
        readonly Dictionary<Camera, int> masks = new Dictionary<Camera, int>();
        bool filtersOff, includeUi, busy, ready, retiring;
        string error;
        RenderTexture pendingTexture;
        AsyncGPUReadbackRequest pending;
        bool hasPending;
        Action restoreControl;
        const int MaxShots = 8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static void Log(string s) => Main.Entry.Logger.Log("[framecmp] " + s);
        static FrameCompare Get()
        {
            if (instance && instance.retiring) throw new InvalidOperationException("timed-out GPU readback still draining; retry after completion");
            if (!instance) { instance = new GameObject("StutterFix.FrameCompare").AddComponent<FrameCompare>(); DontDestroyOnLoad(instance.gameObject); }
            return instance;
        }
        internal static void Pause()
        {
            if (Paused) return;
            controller = scrController.instance;
            if (!controller) throw new InvalidOperationException("pause requires a loaded controller");
            oldPaused = controller.paused; oldAudio = AudioListener.pause;
            oldTime = Time.timeScale; oldTween = DOTween.timeScale;
            Paused = true;
            try { controller.paused = true; Time.timeScale = 0; DOTween.timeScale = 0; AudioListener.pause = true; }
            catch { Resume(); throw; }
            Log("pause controller.paused=1 Time.timeScale=0 DOTween.timeScale=0 AudioListener.pause=1; UI pause menu not invoked");
        }
        internal static void Resume()
        {
            if (!Paused) return;
            try { if (controller) controller.paused = oldPaused; }
            finally { Time.timeScale = oldTime; DOTween.timeScale = oldTween; AudioListener.pause = oldAudio; Paused = false; controller = null; }
            Log("resume restored");
        }
        internal static bool Step(string arg)
        {
            var p = Get();
            if (p.busy)
            {
                if (!p.ready) return false;
                p.busy = p.ready = false;
                if (p.error != null) throw new InvalidOperationException(p.error);
                return true;
            }
            var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 2 && a[0] == "filters" && (a[1] == "off" || a[1] == "on"))
            {
                p.filtersOff = a[1] == "off";
                if (!p.filtersOff) p.RestoreFilters();
                Log("filters=" + a[1] + " (off applies only during each capture)"); return true;
            }
            if (a.Length == 2 && a[0] == "ui" && (a[1] == "include" || a[1] == "exclude"))
            { p.includeUi = a[1] == "include"; Log("ui=" + a[1]); return true; }
            if (a.Length >= 3 && a[0] == "diff")
            {
                if (a.Length > 5) throw new ArgumentException("framecmp diff A B [0..255] [png]");
                int threshold = a.Length > 3 && a[3] != "png" ? int.Parse(a[3], Inv) : 0;
                var result = p.Diff(a[1], a[2], threshold);
                if (Array.IndexOf(a, "png") >= 0) { p.Save(a[1]); p.Save(a[2]); }
                return true;
            }
            if (a.Length == 2 && a[0] == "save") { p.Save(a[1]); return true; }
            if (a.Length == 1 && a[0] == "clear") { p.shots.Clear(); return true; }
            IEnumerator work;
            if (a.Length == 2 && a[0] == "grab") work = p.Grab(a[1]);
            else if (a.Length == 3 && a[0] == "noise") work = p.Noise(a[1], a[2]);
            else if (a.Length == 2 && a[0] == "control" && (a[1] == "hide" || a[1] == "order")) work = p.Control(a[1]);
            else throw new ArgumentException("framecmp filters off|on; ui exclude|include; grab NAME; diff A B [threshold] [png]; noise A B; control hide|order; save NAME; clear");
            p.error = null; p.ready = false; p.busy = true;
            p.StartCoroutine(p.Guarded(work)); return false;
        }
        IEnumerator Guarded(IEnumerator work)
        {
            // Flatten nested iterators so every exception reaches AutoTest, with restoration.
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            try
            {
                while (stack.Count > 0)
                {
                    object next = null; bool more = false;
                    try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                    catch (Exception e) { error = e.Message; Log("valid=0 error=" + error); break; }
                    if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (next is IEnumerator child) stack.Push(child); else yield return next;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                RestoreControl(); RestoreCapture(); ready = true;
            }
        }
        void RequirePaused()
        {
            if (!Paused || !controller || !controller.paused || Time.timeScale != 0 || DOTween.timeScale != 0 || !AudioListener.pause)
                throw new InvalidOperationException("capture requires intact pause; use pause first");
        }
        IEnumerator Grab(string name)
        {
            RequirePaused(); ValidateName(name);
            if (hasPending && !pending.done) throw new InvalidOperationException("previous timed-out readback still pending");
            ReleasePending(false);
            if (!shots.ContainsKey(name) && shots.Count >= MaxShots) throw new InvalidOperationException("8-shot limit; framecmp clear first");
            if (!SystemInfo.supportsAsyncGPUReadback) throw new InvalidOperationException("AsyncGPUReadback unsupported on " + SystemInfo.graphicsDeviceType);
            // Permit capture-scope changes to settle for one natural frame.
            ApplyCapture();
            Camera.onPreCull += EnforceCapture;
            yield return null;
            yield return new WaitForEndOfFrame();
            RequirePaused();
            if (filters.Keys.Any(b => b && b.enabled) || canvases.Keys.Any(c => c && c.enabled) ||
                masks.Keys.Any(c => c && c.cullingMask != (masks[c] & ~(1 << LayerMask.NameToLayer("UI")))))
                throw new InvalidOperationException("capture options overwritten during rendering");
            int w = Screen.width, h = Screen.height;
            ulong state = State();
            pendingTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "SF.framecmp.RGBA8", antiAliasing = 1 };
            if (!pendingTexture.Create()) throw new InvalidOperationException($"RT create failed {w}x{h} ARGB32 non-MSAA");
            var active = RenderTexture.active;
            try { ScreenCapture.CaptureScreenshotIntoRenderTexture(pendingTexture); }
            finally { RenderTexture.active = active; }
            int frame = Time.frameCount;
            pending = AsyncGPUReadback.Request(pendingTexture, 0, TextureFormat.RGBA32); hasPending = true;
            double start = Time.realtimeSinceStartupAsDouble;
            while (!pending.done)
            {
                if (Time.realtimeSinceStartupAsDouble - start > 15) throw new TimeoutException("GPU readback exceeded 15s; pending RT retained until completion");
                yield return null;
            }
            if (pending.hasError) throw new InvalidOperationException($"readback failed {w}x{h} ARGB32->RGBA32 device={SystemInfo.graphicsDeviceType}");
            RequirePaused();
            if (w != Screen.width || h != Screen.height || state != State()) throw new InvalidOperationException("resolution or renderer state changed during readback");
            shots[name] = new Shot { Pixels = pending.GetData<byte>().ToArray(), Width = w, Height = h, Frame = frame, State = state,
                Mode = $"filtersOff={filtersOff},includeUi={includeUi},device={SystemInfo.graphicsDeviceType},color={QualitySettings.activeColorSpace}" };
            Log($"grab {name} valid=1 {w}x{h} frame={frame} state={state:X16} {shots[name].Mode} disabledFilters={filters.Count} disabledCanvases={canvases.Count} maskedCameras={masks.Count}");
            RestoreCapture();
        }
        IEnumerator Noise(string a, string b)
        {
            if (a == b) throw new ArgumentException("noise needs distinct names");
            yield return Grab(a); yield return Grab(b);
            var r = Diff(a, b, 0);
            bool stable = shots[a].State == shots[b].State;
            Log($"noise rendererStateEqual={stable} zero={r.Changed == 0}");
            if (!stable || r.Changed != 0) throw new InvalidOperationException("nonzero/unstable noise floor: inspect time shaders, filters, MSAA, uploads; equivalence invalid");
        }
        FrameCompareMetrics.Result Diff(string a, string b, int threshold)
        {
            if (!shots.TryGetValue(a, out var x) || !shots.TryGetValue(b, out var y)) throw new ArgumentException("missing shot");
            if (x.Width != y.Width || x.Height != y.Height || x.Mode != y.Mode) throw new InvalidOperationException("capture dimensions/options differ");
            var r = FrameCompareMetrics.Compare(x.Pixels, y.Pixels, x.Width, x.Height, threshold);
            Log(string.Format(Inv, "diff {0} {1} threshold={2} maxRGBA={3} changed={4}/{5} ratio={6:R} PSNR={7:R} top32(bottom-left,x,y,w,h,L1)={8}",
                a, b, threshold, string.Join(",", r.Max), r.Changed, (long)x.Width * x.Height, r.Ratio, r.Psnr, r.Regions));
            return r;
        }
        void ApplyCapture()
        {
            if (filtersOff)
            {
                var filterTypes = new Dictionary<Type, bool>();
                foreach (var b in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                {
                    if (!b || !b.gameObject.scene.IsValid()) continue;
                    var type = b.GetType(); string n = type.Name;
                    if (!filterTypes.TryGetValue(type, out bool isFilter))
                    {
                        isFilter = n.StartsWith("CameraFilterPack", StringComparison.Ordinal) || n.IndexOf("Bloom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                            .Any(m => m.Name == "OnRenderImage" && m.GetParameters().Length == 2);
                        filterTypes.Add(type, isFilter);
                    }
                    if (isFilter && !filters.ContainsKey(b))
                    { filters.Add(b, b.enabled); b.enabled = false; }
                }
            }
            if (includeUi) return;
            // Disable explicit canvases, then remove only Unity's dedicated UI layer from cameras.
            // OverlayCam itself is NEVER disabled: it may composite the game camRT.
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                if (c && c.gameObject.scene.IsValid() && !canvases.ContainsKey(c)) { canvases.Add(c, c.enabled); c.enabled = false; }
            int layer = LayerMask.NameToLayer("UI");
            // A final composition quad may share the UI layer. Keep that layer intact in that case.
            bool sharedWithWorld = layer >= 0 && Resources.FindObjectsOfTypeAll<Renderer>().Any(r => r && r.gameObject.scene.IsValid() &&
                r.gameObject.layer == layer && r.enabled && r.gameObject.activeInHierarchy && !r.GetComponentInParent<Canvas>(true));
            if (layer >= 0 && !sharedWithWorld) foreach (var c in Camera.allCameras)
                if (!masks.ContainsKey(c)) { masks.Add(c, c.cullingMask); c.cullingMask &= ~(1 << layer); }
        }
        void RestoreFilters() { foreach (var x in filters) if (x.Key) x.Key.enabled = x.Value; filters.Clear(); }
        void RestoreCapture()
        {
            Camera.onPreCull -= EnforceCapture;
            ReleasePending(false);
            RestoreFilters();
            foreach (var x in canvases) if (x.Key) x.Key.enabled = x.Value; canvases.Clear();
            foreach (var x in masks) if (x.Key) x.Key.cullingMask = x.Value; masks.Clear();
        }
        void EnforceCapture(Camera camera)
        {
            foreach (var b in filters.Keys) if (b) b.enabled = false;
            foreach (var c in canvases.Keys) if (c) c.enabled = false;
            if (masks.TryGetValue(camera, out int mask)) camera.cullingMask = mask & ~(1 << LayerMask.NameToLayer("UI"));
        }
        void ReleasePending(bool drain)
        {
            if (hasPending)
            {
                if (!pending.done && drain) pending.WaitForCompletion();
                if (!pending.done) return; // Timeout reports failure without freeing an in-flight RT.
                hasPending = false;
            }
            if (pendingTexture) { pendingTexture.Release(); Destroy(pendingTexture); pendingTexture = null; }
        }
        void Update()
        {
            if (!busy && hasPending && pending.done) ReleasePending(false);
            if (retiring && !hasPending) { if (instance == this) instance = null; Destroy(gameObject); }
        }
        static ulong State()
        {
            // Detect transform/visibility/sprite/material/order/color drift (not arbitrary shader globals).
            ulong hash = 14695981039346656037UL;
            Action<int> add = v => { unchecked { hash = (hash ^ (uint)v) * 1099511628211UL; } };
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r && r.gameObject.scene.IsValid()).OrderBy(r => r.GetInstanceID()))
            {
                add(r.GetInstanceID()); add(r.enabled ? 1 : 0); add(r.forceRenderingOff ? 1 : 0); add(r.gameObject.activeInHierarchy ? 1 : 0);
                var matrix = r.localToWorldMatrix; for (int i = 0; i < 16; i++) add(matrix[i].GetHashCode());
                add(r.sortingLayerID); add(r.sortingOrder); foreach (var m in r.sharedMaterials) add(m ? m.GetInstanceID() : 0);
                if (r is SpriteRenderer s) { add(s.sprite ? s.sprite.GetInstanceID() : 0); add(s.color.GetHashCode()); add(s.flipX ? 1 : 0); add(s.flipY ? 1 : 0); }
            }
            return hash;
        }
        IEnumerator Control(string kind)
        {
            RequirePaused(); shots.Clear();
            yield return Noise("_controlA", "_controlN");
            var camera = GpuSplit.FindMain(); var planes = GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix);
            var candidates = Resources.FindObjectsOfTypeAll<SpriteRenderer>().Where(r => GpuSplit.IsLiveImage(r, camera) && r.enabled &&
                r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.color.a > 0 && r.sprite &&
                GeometryUtility.TestPlanesAABB(planes, GpuSplit.ConservativeBounds(r))).OrderBy(r => r.GetInstanceID()).Take(2048).ToArray();
            int attempts = 0;
            var tested = new HashSet<int>();
            var testedPairs = new HashSet<string>();
            while (attempts++ < 16)
            {
            if (kind == "hide")
            {
                var r = candidates.FirstOrDefault(c => !tested.Contains(c.GetInstanceID()));
                if (!r) throw new InvalidOperationException("no remaining visible decoration candidate");
                tested.Add(r.GetInstanceID()); bool old = r.forceRenderingOff;
                restoreControl = () => { if (r) r.forceRenderingOff = old; }; r.forceRenderingOff = true;
                Log($"control hide renderer={r.GetInstanceID()}");
            }
            else
            {
                SpriteRenderer first = null, second = null;
                for (int i = 0; i < candidates.Length && !first; i++) for (int j = i + 1; j < candidates.Length; j++)
                {
                    var x = candidates[i]; var y = candidates[j];
                    if (testedPairs.Contains(x.GetInstanceID() + ":" + y.GetInstanceID())) continue;
                    if (x.sortingLayerID != y.sortingLayerID || x.sortingOrder != y.sortingOrder || x.sharedMaterial == y.sharedMaterial ||
                        !x.sharedMaterial || !y.sharedMaterial || x.sharedMaterial.renderQueue != y.sharedMaterial.renderQueue ||
                        x.GetComponentInParent<UnityEngine.Rendering.SortingGroup>() || y.GetComponentInParent<UnityEngine.Rendering.SortingGroup>()) continue;
                    if (!camera.orthographic || camera.transparencySortMode == TransparencySortMode.CustomAxis ||
                        Math.Abs(Vector3.Dot(x.bounds.center - y.bounds.center, camera.transform.forward)) > 0.00001f) continue;
                    if (!Projected(camera, x.bounds).Overlaps(Projected(camera, y.bounds))) continue;
                    first = x; second = y; break;
                }
                if (!first) throw new InvalidOperationException("no overlapping same-key distinct-material decoration pair in first 2048 candidates");
                testedPairs.Add(first.GetInstanceID() + ":" + second.GetInstanceID());
                int order = first.sortingOrder;
                if (order <= -32768 || order >= 32767) throw new InvalidOperationException("sortingOrder at boundary");
                var x1 = first; var x2 = second;
                restoreControl = () => { if (x1) x1.sortingOrder = order; if (x2) x2.sortingOrder = order; };
                // Known tie rule: ascending material instance ID is back-to-front. Force the reverse.
                bool firstBack = first.sharedMaterial.GetInstanceID() < second.sharedMaterial.GetInstanceID();
                first.sortingOrder = order + (firstBack ? 1 : -1); second.sortingOrder = order + (firstBack ? -1 : 1);
                Log($"control order renderers={first.GetInstanceID()},{second.GetInstanceID()} materialIDs={first.sharedMaterial.GetInstanceID()},{second.sharedMaterial.GetInstanceID()} originalOrder={order} forcedOrders={first.sortingOrder},{second.sortingOrder}");
            }
            yield return Grab("_controlB"); RestoreControl(); yield return Grab("_controlA2");
            var restored = Diff("_controlA", "_controlA2", 0); var changed = Diff("_controlA", "_controlB", 0);
            if (restored.Changed != 0 || shots["_controlA"].State != shots["_controlA2"].State)
                throw new InvalidOperationException("control failed: restoration must be zero/stable");
            if (changed.Changed > 0) { Log("control " + kind + " valid=1 restored=0 positive=" + changed.Changed + " attempt=" + attempts); yield break; }
            Log("control " + kind + " candidate produced zero; retry (occluded/commutative)");
            }
            throw new InvalidOperationException("control inconclusive: 16 candidates produced zero; no positive evidence");
        }
        static Rect Projected(Camera c, Bounds b)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < 8; i++)
            {
                var p = c.WorldToScreenPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (p.z <= 0) return new Rect();
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        void RestoreControl() { var restore = restoreControl; restoreControl = null; restore?.Invoke(); }
        static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64 || name.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
                throw new ArgumentException("shot name: 1..64 letters/digits/_/-");
        }
        void Save(string name)
        {
            ValidateName(name);
            if (!shots.TryGetValue(name, out var s)) throw new ArgumentException("missing shot");
            string dir = Path.Combine(Main.Entry.Path, "framecmp"); Directory.CreateDirectory(dir);
            var t = new Texture2D(s.Width, s.Height, TextureFormat.RGBA32, false, true);
            try { t.LoadRawTextureData(s.Pixels); t.Apply(false, false); File.WriteAllBytes(Path.Combine(dir, name + ".png"), t.EncodeToPNG()); }
            finally { Destroy(t); }
            Log("saved=" + Path.Combine(dir, name + ".png"));
        }
        internal static void Stop() => StopCore(false);
        internal static void Unload() => StopCore(true);
        static void StopCore(bool drain)
        {
            try
            {
                if (instance)
                {
                    instance.StopAllCoroutines(); instance.RestoreControl(); instance.RestoreCapture();
                    if (drain) instance.ReleasePending(true);
                    instance.shots.Clear(); instance.busy = false;
                    if (instance.hasPending) instance.retiring = true;
                    else { Destroy(instance.gameObject); instance = null; }
                }
            }
            finally { Resume(); }
        }
        void OnDestroy()
        {
            RestoreControl(); RestoreCapture(); ReleasePending(true);
            if (instance == this) { instance = null; Resume(); }
        }
    }
}
#endif
