using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Destructive diagnostic only. No settings/file switch and no Player compilation.
    internal static class DecoFreeze
    {
        private const string Owner = "StutterFix.decofreeze";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Modes = { "off", "pos", "anim", "both", "tight", "setters" };
        private static readonly int[] Masks = { 0, 1, 2, 3, 4, 8 };   // 1=둘 다 건너뜀, 2=애니 평가, 4=TightLate 쓰기만, 8=설정 함수·UpdatePosition 만
        private static readonly List<Sample> samples = new List<Sample>();
        private struct Sample { internal int Mode; internal double Ms; }
        private static int mode, modeFrame;
        private static bool installed, session, measuring;
        private static long position, setters, parallax, tight, animation, kPos, kRot, kScale;
        private static long p0, s0, x0, t0, a0, clock0, errors0;
        private static int frame0, unityErrors;
        private static string firstError;
        private static double song0;
        private static readonly Action<scrDecoration> Clamp = AccessTools.MethodDelegate<Action<scrDecoration>>(
            AccessTools.DeclaredMethod(typeof(scrDecoration), "UpdateScreenClamp", Type.EmptyTypes));

        private static void Log(string s) { Main.Entry.Logger.Log("[decofreeze] " + s); }
        private static bool Image(scrDecoration d) { return (object)d != null && d.GetType() == typeof(scrVisualDecoration); }
        private static bool Freeze(scrDecoration d) { return (Masks[mode] & 9) != 0 && Hitch.Playing && !SceneReset.Resetting && Image(d); }
        private static bool FreezeTight(scrDecoration d) { return (Masks[mode] & 5) != 0 && Hitch.Playing && !SceneReset.Resetting && Image(d); }

        // Direct source hooks also cover the default TightLate bypass and every
        // DecoAnim backend (batch, groups, native and DecoEngine) before evaluation.
        internal static bool SkipTight(scrDecoration d)
        {
            if (!FreezeTight(d)) return false;
            tight++;
            return true;
        }
        internal static bool SkipAnimation()
        {
            if ((Masks[mode] & 2) == 0 || !Hitch.Playing || SceneReset.Resetting) return false;
            animation++;
            return true;
        }
        public static bool PositionPrefix(scrDecoration __instance)
        {
            if (!Freeze(__instance)) return true;
            position++;
            return false;
        }
        public static bool SetPositionPrefix(scrDecoration __instance, Vector2 __0, Vector2 __1)
        {
            if (!Freeze(__instance)) return true;
            // Preserve channel values for subsequent starts/evaluation. Child/pivot
            // Transform writes and editor pivot updates are deliberately suppressed.
            if (__instance.parallax != null)
            {
                __instance.pivotPosVec = __0;
                __instance.pivotOffsetVec = __1;
                Clamp(__instance);
            }
            setters++; kPos++;
            return false;
        }
        public static bool RotationPrefix(scrDecoration __instance, float __0)
        {
            if (!Freeze(__instance)) return true;
            __instance.rotAngle = __0; setters++; kRot++; return false;
        }
        public static bool ScalePrefix(scrDecoration __instance, Vector2 __0)
        {
            if (!Freeze(__instance)) return true;
            __instance.scaleVec = __0; setters++; kScale++; return false;
        }
        public static bool ParallaxPrefix(scrParallax __instance)
        {
            if (!Freeze(__instance.decoration)) return true;
            parallax++; return false;
        }
        private static void Patch(Harmony h, Type type, string name, Type[] args, string prefix)
        {
            var method = AccessTools.DeclaredMethod(type, name, args);
            if (method == null || method.IsStatic || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, name);
            h.Patch(method, prefix: new HarmonyMethod(typeof(DecoFreeze), prefix) { priority = Priority.First });
        }
        private static void Install()
        {
            if (installed) return;
            var h = new Harmony(Owner);
            try
            {
                Patch(h, typeof(scrDecoration), "UpdatePosition", Type.EmptyTypes, nameof(PositionPrefix));
                Patch(h, typeof(scrDecoration), "SetPosition", new[] { typeof(Vector2), typeof(Vector2) }, nameof(SetPositionPrefix));
                Patch(h, typeof(scrDecoration), "SetRotation", new[] { typeof(float) }, nameof(RotationPrefix));
                Patch(h, typeof(scrDecoration), "SetScale", new[] { typeof(Vector2) }, nameof(ScalePrefix));
                Patch(h, typeof(scrParallax), "SetTrans", Type.EmptyTypes, nameof(ParallaxPrefix));
                Application.logMessageReceived += OnLog;
                installed = true;
                Log("hooks=scrDecoration.UpdatePosition/SetPosition/SetRotation/SetScale,scrParallax.SetTrans; source=TightLate.Process,DecoAnim.UpdatePrefix; default=off");
            }
            catch { h.UnpatchAll(Owner); throw; }
        }
        private static void OnLog(string condition, string stack, LogType type)
        {
            if (!measuring || (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)) return;
            unityErrors++;
            if (firstError == null) firstError = (condition ?? "").Replace('\n', ' ').Replace('\r', ' ');
        }
        internal static void Command(string arg)
        {
            arg = arg.Trim().ToLowerInvariant();
            if (arg == "state") { State(); return; }
            if (arg == "summary") { Summary(); return; }
            if (arg == "vis") { Vis(); return; }
            if (measuring) throw new InvalidOperationException("decofreeze: fuse end before mode/reset changes");
            if (arg == "reset")
            {
                Install(); session = true; samples.Clear(); SetMode(0); Log("reset samples=0"); return;
            }
            int next = Array.IndexOf(Modes, arg);
            if (next < 0) throw new ArgumentException("decofreeze <off|pos|anim|both|state|reset|summary>");
            if (next != 0) Install();
            session = true;
            SetMode(next);
            State();
        }
        // 깨어 있는 이미지 장식 중 지금 카메라에 실제로 보이는(Renderer.isVisible) 것의 비율: 안 보이는 장식의 Transform 쓰기를 미룰 수 있는지 가늠
        private static readonly FieldInfo spriteField = AccessTools.Field(typeof(scrVisualDecoration), "spriteRenderer");
        private static void Vis()
        {
            int awake = 0, visual = 0, vis = 0, off = 0, zeroAlpha = 0, moving = 0;
            var list = Dormancy.AwakeList;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i] as scrVisualDecoration; awake++;
                if ((object)d == null || d == null) continue;
                var sr = spriteField.GetValue(d) as SpriteRenderer; if (sr == null) continue;
                visual++;
                if (sr.color.a <= 0f) zeroAlpha++;
                if (sr.isVisible) vis++; else off++;
            }
            Log("VIS t=" + (scrConductor.instance != null ? scrConductor.instance.songposition_minusi.ToString("F2", Inv) : "?") + " awake=" + awake + " image=" + visual + " isVisible=" + vis + " notVisible=" + off + " zeroAlpha=" + zeroAlpha);
        }
        private static void SetMode(int next)
        {
            mode = next; modeFrame = Time.frameCount;
            position = setters = parallax = tight = animation = kPos = kRot = kScale = 0;
            // Never allow TightLate's optional same-value cache to survive a freeze.
            TightLate.Invalidate();
        }
        private static string Counts(long p, long s, long x, long t, long a, int frames)
        {
            return string.Format(Inv, "frames={0} skip_updateposition={1} skip_setters={2} skip_parallax={3} skip_tight={4} skip_anim_update={5} per_frame={6:F2}/{7:F2}/{8:F2}/{9:F2}/{10:F2}",
                frames, p, s, x, t, a, p / (double)frames, s / (double)frames, x / (double)frames, t / (double)frames, a / (double)frames);
        }
        private static void State()
        {
            Log("KINDS pos_calls=" + kPos + " rot_calls=" + kRot + " scale_calls=" + kScale + " deferred_rotscale=" + MoveApply.RotScaleDeferred + " late_skips=" + MoveApply.LateSkips + " frames=" + Math.Max(1, Time.frameCount - modeFrame));
            Log("STATE mode=" + Modes[mode] + " hooks=" + installed + " measuring=" + measuring + " "
                + Counts(position, setters, parallax, tight, animation, Math.Max(1, Time.frameCount - modeFrame)));
        }
        internal static void Begin()
        {
            if (!session) return;
            if (measuring) throw new InvalidOperationException("decofreeze: nested fuse begin");
            if (!DecoAnim.Enabled || !DecoAnim.Installed)
                throw new InvalidOperationException("decofreeze: DecoAnim must be enabled and installed");
            if (!Hitch.Playing || scrConductor.instance == null || scrConductor.instance.songposition_minusi < 262
                || scrConductor.instance.songposition_minusi > 263)
                throw new InvalidOperationException("decofreeze: window must begin at song 262 seconds");
            p0 = position; s0 = setters; x0 = parallax; t0 = tight; a0 = animation;
            errors0 = DecoAnim.Errors; unityErrors = 0; firstError = null;
            song0 = scrConductor.instance.songposition_minusi;
            frame0 = Time.frameCount; clock0 = Stopwatch.GetTimestamp(); measuring = true;
        }
        internal static void End(string fuse)
        {
            if (!measuring) return;
            long now = Stopwatch.GetTimestamp(); int frames = Time.frameCount - frame0;
            measuring = false;
            double seconds = (now - clock0) / (double)Stopwatch.Frequency;
            double ms = frames > 0 ? 1000 * seconds / frames : double.NaN;
            long animErrors = DecoAnim.Errors - errors0;
            double songEnd = scrConductor.instance == null ? double.NaN : scrConductor.instance.songposition_minusi;
            bool valid = frames > 0 && seconds > 0 && Hitch.Playing && scrConductor.instance != null
                && songEnd >= 276 && songEnd < 277 && animErrors == 0 && unityErrors == 0 && !Edition.Dev
                && ((Masks[mode] & 13) == 0 || position - p0 + tight - t0 + setters - s0 > 0)
                && ((Masks[mode] & 2) == 0 || animation > a0);
            if (valid) samples.Add(new Sample { Mode = mode, Ms = ms });
            Log(string.Format(Inv, "RESULT mode={0} fps={1:F3} ms={2:F6} seconds={3:F6} valid={4} decoanim_errors={5} unity_errors={6} dev_shadow={7} {8} song_begin={10:F6} song_end={11:F6} fuse={9}",
                Modes[mode], frames / seconds, ms, seconds, valid, animErrors, unityErrors, Edition.Dev,
                Counts(position - p0, setters - s0, parallax - x0, tight - t0, animation - a0, Math.Max(1, frames)), fuse, song0, songEnd));
            if (animErrors != 0 || unityErrors != 0)
                Log("[Error] invalid_window mode=" + Modes[mode] + " decoanim_errors=" + animErrors
                    + " unity_errors=" + unityErrors + " first=" + (firstError ?? "DecoAnim.Errors; see animstat"));
        }
        private static void Summary()
        {
            if (measuring) throw new InvalidOperationException("decofreeze: fuse end before summary");
            var sums = new double[6]; var ns = new int[6];
            foreach (var s in samples) { sums[s.Mode] += s.Ms; ns[s.Mode]++; }
            string row = "SUMMARY complete=" + (ns[0] >= 3 && ns[1] >= 2 && ns[4] >= 2 && ns[5] >= 2)
                + " averaging=arithmetic_mean_of_window_ms";
            double baseline = ns[0] > 0 ? sums[0] / ns[0] : double.NaN;
            for (int i = 0; i < 6; i++)
            {
                double mean = ns[i] > 0 ? sums[i] / ns[i] : double.NaN;
                row += string.Format(Inv, " {0}:n={1},fps={2:F3},ms={3:F6},saved_ms={4:F6}", Modes[i], ns[i], 1000 / mean, mean, baseline - mean);
            }
            Log(row);
            SetMode(0); session = false;
        }
        internal static void Shutdown()
        {
            if (mode != 0) TightLate.Invalidate();
            mode = 0; session = measuring = false;
            if (installed) { Application.logMessageReceived -= OnLog; new Harmony(Owner).UnpatchAll(Owner); }
            installed = false; samples.Clear();
        }
    }
}
