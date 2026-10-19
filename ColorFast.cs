using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Only collapse the managed call chain. Keep the native color write, even for equal bits.
    internal static class ColorFast
    {
        internal static bool Enabled;
        private static bool compatible, verify, failed;
        private static string compatibility = "off", first = "none";
        private static long direct, samples, checkedN, mismatch;
        private static readonly long[] fallback = new long[7];
        private static readonly AccessTools.FieldRef<scrDecoration, Color> color = AccessTools.FieldRefAccess<scrDecoration, Color>("color");
        private static readonly AccessTools.FieldRef<scrDecoration, float> opacity = AccessTools.FieldRefAccess<scrDecoration, float>("opacity");
        private static readonly AccessTools.FieldRef<scrDecoration, Color> rendered = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        private static readonly AccessTools.FieldRef<scrDecoration, bool> rendererOn = AccessTools.FieldRefAccess<scrDecoration, bool>("rendererEnabled");

        internal static void Configure(string path)
        {
            Uninstall();
            direct = samples = checkedN = mismatch = 0; Array.Clear(fallback, 0, fallback.Length);
            failed = false; first = "none";
            Enabled = File.Exists(Path.Combine(path, "colorfast-on"));
            if (!Enabled) return;
            try
            {
                // Called after InstallAll: enumerate exact patch methods once, never in the hot loop.
                foreach (var spec in new[] {
                    (typeof(scrDecoration), "SetColor"), (typeof(scrDecoration), "SetOpacity"),
                    (typeof(scrVisualDecoration), "ApplyColor"), (typeof(scrVisualDecoration), "GetAlpha"),
                    (typeof(scrVisualDecoration), "GetVisible"), (typeof(scrVisualDecoration), "SetVisible"),
                    (typeof(ExtensionMethods), "WithAlpha"), (typeof(InvisibleSkip), "After"), (typeof(Dormancy), "WakePost") })
                {
                    var method = spec.Item1 == typeof(ExtensionMethods)
                        ? AccessTools.Method(spec.Item1, spec.Item2, new[] { typeof(Color), typeof(float) })
                        : AccessTools.DeclaredMethod(spec.Item1, spec.Item2);
                    if (method == null) { compatibility = "missing " + spec.Item2; return; }
                    var patches = Harmony.GetPatchInfo(method);
                    if (patches == null) continue;
                    foreach (var p in patches.Prefixes) if (!Allowed(p, spec.Item2, false)) return;
                    foreach (var p in patches.Postfixes) if (!Allowed(p, spec.Item2, true)) return;
                    if (patches.Transpilers.Count != 0 || patches.Finalizers.Count != 0)
                    { compatibility = "transpiler/finalizer " + spec.Item2; return; }
                }
                compatible = true; compatibility = "ok (load-time patch snapshot)";
            }
            catch (Exception e) { compatibility = e.GetType().Name; }
        }
        private static bool Allowed(Patch p, string method, bool post)
        {
            var m = p.PatchMethod;
            // Diagnostic setters are intentionally absent from fast-path accounting.
            if ((method == "SetColor" || method == "SetOpacity") && m.DeclaringType == typeof(FrameParts)
                && m.Name == (post ? nameof(FrameParts.SetPost) : nameof(FrameParts.SetPre))) return true;
            if (post && method == "ApplyColor" && m.DeclaringType == typeof(InvisibleSkip) && m.Name == nameof(InvisibleSkip.After)) return true;
            if (post && (method == "ApplyColor" || method == "SetVisible") && m.DeclaringType == typeof(Dormancy) && m.Name == nameof(Dormancy.WakePost)) return true;
            compatibility = method + " patch=" + p.owner + ":" + m.DeclaringType?.Name + "." + m.Name;
            return false;
        }
        internal static void Uninstall() { Enabled = compatible = verify = false; compatibility = "off"; }
        private static bool No(int reason) { fallback[reason]++; return false; }
        internal static bool TryColor(scrDecoration d, Color value) => TrySet(d, value, 0f, false);
        internal static bool TryOpacity(scrDecoration d, float value) => TrySet(d, default(Color), value, true);
        private static bool TrySet(scrDecoration d, Color c, float o, bool onlyOpacity)
        {
            if (!Enabled) return false; // default off: no counters, hash lookup, clock or native calls
            if (!compatible || failed) return No(0);
            if ((object)d == null || d.GetType() != typeof(scrVisualDecoration)) return No(1);
            if (d.stickToFloor) return No(2);
            // Positive alpha alone is insufficient when SetVisible(false) has put it to sleep.
            if (!rendererOn(d) || !(rendered(d).a > 0f)) return No(3);
            if (!InvisibleSkip.Enabled || !Dormancy.Enabled || Precheck.Active != 0) return No(4);
            if (onlyOpacity) c = color(d); else o = opacity(d);
            // Retain BOTH multiplications, in the original IL order; no alpha clamping.
            float a = c.a * o * 1f;
            if (!(a > 0f)) return No(5); // includes zero, negatives and NaN
            var v = (scrVisualDecoration)d;
            var sr = v.spriteRenderer;
            if (sr == null || sr.forceRenderingOff) return No(6);
            var rc = new Color(c.r, c.g, c.b, a);
#if DEV || AUTOTEST
            bool sample = verify && (++samples & 31) == 0;
            bool enabledBefore = sample && sr.enabled;
#endif
            if (onlyOpacity) opacity(d) = o; else color(d) = c;
            rendered(d) = rc;
            sr.color = rc;
            direct++;
#if DEV || AUTOTEST
            if (sample) Check(d, sr, c, o, enabledBefore);
#endif
            return true;
        }
#if DEV || AUTOTEST
        private static bool Bits(float x, float y) => BitConverter.SingleToInt32Bits(x) == BitConverter.SingleToInt32Bits(y);
        private static bool Bits(Color x, Color y) => Bits(x.r,y.r) && Bits(x.g,y.g) && Bits(x.b,y.b) && Bits(x.a,y.a);
        private static void Check(scrDecoration d, SpriteRenderer sr, Color c, float o, bool wasEnabled)
        {
            // Pure original setter + ApplyColor evaluation; no second write or game callback.
            Color expected = c.WithAlpha(c.a * o * 1f);
            checkedN++;
            bool ok = Bits(color(d), c) && Bits(opacity(d), o) && Bits(rendered(d), expected)
                && Bits(sr.color, expected) && !sr.forceRenderingOff && sr.enabled == wasEnabled && rendererOn(d);
            if (!ok) { mismatch++; failed = true; first = "color/opacity/rendererColor/spriteColor/forceOff/enabled/rendererEnabled"; }
        }
        internal static string Command(string arg)
        {
            if (arg == "verify") verify = true;
            else if (arg == "verify off") verify = false;
            else if (arg == "bench") return Bench();
            else if (arg != "state") return "colorfast verify|verify off|state|bench";
            return State();
        }
        private static string Bench()
        {
            if (Hitch.Playing || ADOBase.customLevel != null || ADOBase.editor != null || Precheck.Active != 0)
                return "menu only; close the level first";
            if (!Enabled || !compatible || failed) return "requires colorfast-on and compatibility=ok: " + State();
            var root = new GameObject("colorfast-bench"); root.SetActive(false);
            bool oldVerify = verify;
            long oldDirect = direct, oldSamples = samples;
            var oldFallback = (long[])fallback.Clone();
            try
            {
                verify = false;
                // Managed field container, like TightLate.Bench: do not invoke game Awake/OnDestroy.
                var d = (scrVisualDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                var sr = root.AddComponent<SpriteRenderer>(); d.spriteRenderer = sr;
                rendererOn(d) = true; color(d) = Color.white; opacity(d) = 1f; rendered(d) = Color.white;
                var setColor = AccessTools.MethodDelegate<Action<scrDecoration, Color>>(AccessTools.Method(typeof(scrDecoration), "SetColor", new[] { typeof(Color) }));
                var setOpacity = AccessTools.MethodDelegate<Action<scrDecoration, float>>(AccessTools.Method(typeof(scrDecoration), "SetOpacity", new[] { typeof(float) }));
                const int count = 16384;
                long slow = 0, fast = 0;
                // A/B/B/A, aggregate clocks only, both color and opacity, with warm-up.
                for (int round = -2; round < 4; round++)
                {
                    bool useFast = round == -1 || round == 1 || round == 2;
                    long t = Stopwatch.GetTimestamp();
                    for (int i = 0; i < count; i++)
                    {
                        var c = new Color(.25f + (i & 7) * .03f, .5f, .75f, .8f);
                        float o = .5f + (i & 7) * .02f;
                        if (useFast) { if (!TryColor(d, c) || !TryOpacity(d, o)) return "bench fallback: " + State(); }
                        else { setColor(d, c); setOpacity(d, o); }
                    }
                    long ticks = Stopwatch.GetTimestamp() - t;
                    if (round >= 0) { if (useFast) fast += ticks; else slow += ticks; }
                }
                // Original-path differential on the dummy, outside timing, both setters.
                long pairs = 0, differences = 0;
                for (int i = 0; i < 256; i++)
                {
                    var c = new Color(i * .01f, -.5f, .75f, .9f); float o = .1f + i * .002f;
                    if (!TryColor(d, c)) return "bench color fallback";
                    var a = Snapshot(d); setColor(d, c); if (!Same(a, Snapshot(d))) differences++; pairs++;
                    if (!TryOpacity(d, o)) return "bench opacity fallback";
                    a = Snapshot(d); setOpacity(d, o); if (!Same(a, Snapshot(d))) differences++; pairs++;
                }
                if (differences != 0) { mismatch += differences; failed = true; first = "bench original-path differential"; }
                double k = 1e6 / Stopwatch.Frequency / (count * 4.0);
                return "dummy+real SpriteRenderer ABBA us/call original=" + (slow*k).ToString("F4")
                    + " direct=" + (fast*k).ToString("F4") + " estimated-ms/1800=" + ((slow-fast)*k*1.8).ToString("F4")
                    + " differential=" + pairs + " mismatch=" + differences + "; menu microbenchmark, not gameplay evidence";
            }
            finally
            {
                verify = oldVerify; direct = oldDirect; samples = oldSamples; Array.Copy(oldFallback, fallback, fallback.Length);
                UnityEngine.Object.Destroy(root);
            }
        }
        private struct Snap { internal Color C, R, S; internal float O; internal bool F, E, On; }
        private static Snap Snapshot(scrVisualDecoration d) => new Snap { C=color(d), O=opacity(d), R=rendered(d), S=d.spriteRenderer.color,
            F=d.spriteRenderer.forceRenderingOff, E=d.spriteRenderer.enabled, On=rendererOn(d) };
        private static bool Same(Snap a, Snap b) => Bits(a.C,b.C) && Bits(a.O,b.O) && Bits(a.R,b.R) && Bits(a.S,b.S) && a.F==b.F && a.E==b.E && a.On==b.On;
#endif
        internal static string State() => "enabled=" + Enabled + " compatibility=" + compatibility + " direct=" + direct
            + " verify=" + verify + " checked=" + checkedN + " mismatch=" + mismatch + " failed=" + failed + " first=" + first
            + " fallback(compat/type/stick/old-hidden/watch/new-alpha/renderer)=" + string.Join("/", fallback);
    }
}
