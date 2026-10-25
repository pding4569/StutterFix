#if DEV || AUTOTEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // Explicit destructive-to-picture diagnostic, not an optimization. No object or callback off.
    internal sealed class GpuHide : MonoBehaviour
    {
        static GpuHide instance;
        internal static bool Busy => instance != null;
        struct Saved { internal Renderer Renderer; internal bool Off; }
        readonly List<Saved> saved = new List<Saved>();
        bool restored;
        long enforceTicks, enforceCalls, overwrites;
        int enforceFrame = -1;
        string group;
        float seconds;
        int initiallyOn, candidates;
        internal static void Command(string arg)
        {
            if (arg.Trim() == "off") { Stop(); return; }
            if (Busy || GpuCensus.Busy || GpuSplit.Busy) throw new InvalidOperationException("gpuhide/census/gpusplit already active");
            int space = arg.LastIndexOf(' ');
            if (space < 1 || !float.TryParse(arg.Substring(space+1), NumberStyles.Float, CultureInfo.InvariantCulture, out float duration)
                || float.IsNaN(duration) || float.IsInfinity(duration) || duration < 1 || duration > 30)
                throw new ArgumentException("gpuhide <deco|deco-even|layer:NAME|shader:NAME|size:MIN-MAX|tiles|planets-particles> <1..30 realtime seconds per phase>; gpuhide off");
            string selector = arg.Substring(0, space).Trim();
            // Parse/validate before creating a session. No implicit probe installation during playback.
            Select(selector, null, 0);
            if (!GpuProbe.Active) throw new InvalidOperationException("gpuprobe on required BEFORE press");
            var go = new GameObject("StutterFix.GpuHide"); DontDestroyOnLoad(go);
            var p = go.AddComponent<GpuHide>(); instance = p;
            p.group = selector; p.seconds = duration;
            try { p.StartCoroutine(p.Run()); }
            catch { Stop(); throw; }
        }
        // 진단: probe 없이 한 묶음의 forceRenderingOff 를 직접 켜고 끈다(PresentMon 으로 GPU 시간 비교용). 화면이 틀려진다.
        static readonly List<Saved> toggled = new List<Saved>();
        internal static string Toggle(string group, bool on)
        {
            if (!on) { foreach (var t in toggled) if (t.Renderer) t.Renderer.forceRenderingOff = t.Off; int n = toggled.Count; toggled.Clear(); return "restored=" + n; }
            if (toggled.Count > 0) throw new InvalidOperationException("already hidden");
            var rs = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.gameObject.scene.IsValid());
            foreach (var r in rs)
            {
                bool pick;
                if (group == "all") pick = r && r.enabled && r.gameObject.activeInHierarchy;
                else if (group == "tiles") pick = r && r.GetComponentInParent<scrFloor>() != null && !GpuCensus.IsDecoration(r);
                else if (group == "planets-particles") pick = r && (r is ParticleSystemRenderer || r.GetComponentInParent<scrPlanet>() != null) && !GpuCensus.IsDecoration(r);
                else if (group == "deco") pick = r && GpuCensus.IsDecoration(r);
                else if (group == "unowned") pick = r && GpuCensus.IsDecoration(r) && !r.forceRenderingOff && r.enabled && r.gameObject.activeInHierarchy;
                else if (group.StartsWith("unowned-order:", StringComparison.Ordinal))
                {
                    var q = group.Substring(14).Split(',');
                    int lo = int.Parse(q[0]), hi = int.Parse(q[1]);
                    pick = r && GpuCensus.IsDecoration(r) && !r.forceRenderingOff && r.enabled && r.gameObject.activeInHierarchy && r.sortingOrder >= lo && r.sortingOrder <= hi;
                }
                else if (group == "unowned-big") pick = r && GpuCensus.IsDecoration(r) && !r.forceRenderingOff && r.enabled && r.gameObject.activeInHierarchy && (r.bounds.size.x > 25f || r.bounds.size.y > 25f);
                else pick = Select(group, r, 0);
                if (!pick) continue;
                toggled.Add(new Saved { Renderer = r, Off = r.forceRenderingOff }); r.forceRenderingOff = true;
            }
            if (group.StartsWith("unowned", StringComparison.Ordinal))
            {
                foreach (var t in toggled.Take(60))
                {
                    var r = t.Renderer; if (!r) continue;
                    var d = r.GetComponentInParent<scrDecoration>();
                    Main.Entry.Logger.Log("[hidegroup] item name=" + r.name + " type=" + r.GetType().Name + " deco=" + (d ? d.GetType().Name + "#" + d.GetInstanceID() : "-")
                        + " bounds=" + r.bounds.size.x.ToString("F1") + "x" + r.bounds.size.y.ToString("F1") + " shader=" + (r.sharedMaterial && r.sharedMaterial.shader ? r.sharedMaterial.shader.name : "-")
                        + " layer=" + r.sortingLayerName + " order=" + r.sortingOrder + " queue=" + (r.sharedMaterial ? r.sharedMaterial.renderQueue : 0));
                }
            }
            return "hidden=" + toggled.Count;
        }
        static bool Select(string group, Renderer r, int decorationIndex)
        {
            if (group == "deco") return r && GpuCensus.IsDecoration(r);
            if (group == "deco-even") return r && GpuCensus.IsDecoration(r) && (decorationIndex & 1) == 0;
            if (group == "tiles") return r && r.GetComponentInParent<scrFloor>() != null && !GpuCensus.IsDecoration(r);
            if (group == "planets-particles") return r && (r is ParticleSystemRenderer || r.GetComponentInParent<scrPlanet>() != null) && !GpuCensus.IsDecoration(r);
            if (group.StartsWith("layer:", StringComparison.Ordinal))
            {
                string name = group.Substring(6);
                if (!SortingLayer.layers.Any(l => l.name == name)) throw new ArgumentException("unknown sorting layer: " + name);
                return r && GpuCensus.IsDecoration(r) && r.sortingLayerName == name;
            }
            if (group.StartsWith("shader:", StringComparison.Ordinal))
            {
                string name = group.Substring(7);
                if (name.Length == 0) throw new ArgumentException("empty shader");
                return r && GpuCensus.IsDecoration(r) && r.sharedMaterials.Any(m => m && m.shader && m.shader.name == name);
            }
            if (group.StartsWith("size:", StringComparison.Ordinal))
            {
                var range = group.Substring(5).Split('-');
                if (range.Length != 2 || !int.TryParse(range[0], out int min) || !int.TryParse(range[1], out int max) || min < 0 || max < min)
                    throw new ArgumentException("size:MIN-MAX inclusive longest side in pixels");
                if (!r || !GpuCensus.IsDecoration(r)) return false;
                var sr = r as SpriteRenderer;
                Texture t = sr && sr.sprite ? sr.sprite.texture : r.sharedMaterial ? r.sharedMaterial.mainTexture : null;
                return t && Math.Max(t.width, t.height) >= min && Math.Max(t.width, t.height) <= max;
            }
            throw new ArgumentException("unknown gpuhide group: " + group);
        }
        IEnumerator Run()
        {
            try
            {
                // One scan per command; decoration identity, not renderer order, defines the even half.
                var rs = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.gameObject.scene.IsValid()).OrderBy(r => r.GetInstanceID()).ToArray();
                var ids = rs.Select(r => r.GetComponentInParent<scrDecoration>()).Where(d => d).Select(d => d.GetInstanceID()).Distinct().OrderBy(id => id).Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
                foreach (var r in rs)
                {
                    var d = r.GetComponentInParent<scrDecoration>();
                    if (!Select(group, r, d ? ids[d.GetInstanceID()] : 0)) continue;
                    saved.Add(new Saved { Renderer = r, Off = r.forceRenderingOff });
                }
                Main.Entry.Logger.Log("[gpuhide] group=" + group + " selected=" + saved.Count + " duration_per_phase=" + seconds + "; scan/warmup excluded; fixed cohort; realtime windows drift in song time");
                yield return new WaitForSecondsRealtime(0.5f);
                GpuProbe.Window before = null, hidden = null, after = null;
                yield return Measure("before", w => before = w);
                if (!GpuProbe.Active) yield break;
                var camera = Resources.FindObjectsOfTypeAll<Camera>().FirstOrDefault(c => c.name == "Camera" && c.gameObject.activeInHierarchy);
                var planes = camera ? GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix) : null;
                hiding = true;
                Camera.onPreCull += Enforce;
                foreach (var item in saved)
                {
                    var r = item.Renderer;
                    if (!r) continue;
                    if (!r.forceRenderingOff && r.enabled && r.gameObject.activeInHierarchy)
                    {
                        initiallyOn++;
                        if (planes != null && (camera.cullingMask & (1 << r.gameObject.layer)) != 0 && GeometryUtility.TestPlanesAABB(planes, r.bounds)) candidates++;
                    }
                    r.forceRenderingOff = true;
                }
                Main.Entry.Logger.Log("[gpuhide] hidden originally_renderable=" + initiallyOn + " main_camera_frustum_candidates_at_hide=" + candidates + "; candidates are not actual draws");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Measure("hidden", w => hidden = w);
                if (!GpuProbe.Active) yield break;
                Restore();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Measure("after", w => after = w);
                if (before != null && hidden != null && after != null && before.Completed > 0 && hidden.Completed > 0 && after.Completed > 0)
                {
                    double baseline = (before.Frame + after.Frame) / 2;
                    Main.Entry.Logger.Log(FormattableString.Invariant($"[gpuhide] group={group} selected={saved.Count} changed_renderable={initiallyOn} main_candidates={candidates} completed={before.Completed}/{hidden.Completed}/{after.Completed} frame_delta_ms_est={baseline-hidden.Frame:F4} control_drift_ms={after.Frame-before.Frame:F4} ms_per_candidate_est={(candidates == 0 ? double.NaN : (baseline-hidden.Frame)/candidates):F6}; time-varying content, not causal proof"));
                    foreach (var key in before.Means.Keys.Where(k => k.StartsWith("camera:", StringComparison.Ordinal)))
                    {
                        double b = before.Means[key], h = hidden.Means.TryGetValue(key, out double hv) ? hv : 0, a = after.Means.TryGetValue(key, out double av) ? av : 0;
                        Main.Entry.Logger.Log(FormattableString.Invariant($"[gpuhide] {key} before_ms={b:F4} hidden_ms={h:F4} after_ms={a:F4} delta_ms_est={(b+a)/2-h:F4} control_drift_ms={a-b:F4} ms_per_main_candidate_est={(candidates == 0 ? double.NaN : ((b+a)/2-h)/candidates):F6}"));
                    }
                }
                else Main.Entry.Logger.Log("[gpuhide] insufficient completed snapshots; no delta");
            }
            finally { Restore(); if (instance == this) instance = null; Destroy(gameObject); }
        }
        IEnumerator Measure(string phase, Action<GpuProbe.Window> receive)
        {
            if (!GpuProbe.Active) { Main.Entry.Logger.Log("[gpuhide] probe stopped; abort phase"); yield break; }
            GpuProbe.Command("reset");
            float start = Time.realtimeSinceStartup;
            Main.Entry.Logger.Log("[gpuhide] phase=" + phase + " start_realtime=" + start + " frame=" + Time.frameCount);
            yield return new WaitForSecondsRealtime(seconds);
            GpuProbe.Window w = null;
            for (int retry = 0; retry < 8; retry++) { w = GpuProbe.Snapshot(); if (w != null) break; yield return null; }
            receive(w);
            Main.Entry.Logger.Log("[gpuhide] phase=" + phase + " end_realtime=" + Time.realtimeSinceStartup + " frame=" + Time.frameCount);
            GpuProbe.Report();
        }
        // Enforce only the selected cohort while the hidden phase is active. Other diagnostics/game
        // code can also write this property (InvisibleSkip); restore the exact command-start value.
        bool hiding;
        void Enforce(Camera camera)
        {
            if (!hiding || enforceFrame == Time.frameCount) return;
            enforceFrame = Time.frameCount;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (var item in saved)
                if (item.Renderer && !item.Renderer.forceRenderingOff) { item.Renderer.forceRenderingOff = true; overwrites++; }
            enforceTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start; enforceCalls++;
        }
        void Restore()
        {
            hiding = false;
            Camera.onPreCull -= Enforce;
            if (restored) return;
            restored = true;
            foreach (var item in saved) if (item.Renderer) item.Renderer.forceRenderingOff = item.Off;
            Main.Entry.Logger.Log(FormattableString.Invariant($"[gpuhide] enforcement_frames={enforceCalls} external_unhide_writes={overwrites} mean_cpu_ms={(enforceCalls == 0 ? 0 : enforceTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / enforceCalls):F4}; hidden-only CPU work may bias elapsed timestamps; if external_unhide_writes>0 after-control flags may be stale, reject causal estimate"));
        }
        void OnDisable() { if (instance == this) Stop(); else Restore(); }
        void OnDestroy() { Restore(); if (instance == this) instance = null; }
        internal static void Stop()
        {
            var p = instance; if (!p) return;
            instance = null; p.Restore(); p.StopAllCoroutines(); Destroy(p.gameObject);
            Main.Entry.Logger.Log("[gpuhide] cancelled; renderer flags restored");
        }
    }
}
#endif
