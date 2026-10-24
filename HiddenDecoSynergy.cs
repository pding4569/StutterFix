#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Explicit research switch; no patch, worker, new FieldRef, or Player code.
    internal static class HiddenDecoSynergy
    {
        internal static bool TransformOn, ColorOn;
        internal static long Transforms, Colors;
        private static bool compatible;
        private static string reason = "off";
        private static long measureAt, measureTransforms, measureColors;
        private static int measureFrame;
        private static double measureSong;

        internal static bool TransformEligible(scrDecoration d)
        {
            if (!TransformOn || !compatible || ReferenceEquals(d, null)
                || d.GetType() != typeof(scrVisualDecoration) || d.stickToFloor
                || Precheck.Active != 0) return false;
            // A GPU owner has its own visibility and logical-state contract.
            if (LiveDecoRenderer.Owns(d) || DecoBake.GpuLiveOwns((scrVisualDecoration)d) || DecoGpu.Owns(d)) return false;
            return InvisibleSkip.LazyCan(d);
        }
        internal static bool DeferTransform(scrDecoration d)
        {
            if (!TransformOn) return false;
            try
            {
                if (!TransformEligible(d)) return false;
                // Queue before the caller writes rotAngle/scaleVec. Existing visibility,
                // scene reset, song end and Unload drain this same queue.
                InvisibleSkip.LazyAdd(d);
                Transforms++;
                return true;
            }
            catch (Exception ex) { TransformOn = false; reason = "fallback " + ex.GetType().Name; return false; }
        }
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool Finite(Vector2 v) => Finite(v.x) && Finite(v.y);

        private static bool CheckCompatibility()
        {
            foreach (var name in new[] { "SetRotation", "SetScale", "UpdatePosition" })
            {
                var m = AccessTools.DeclaredMethod(typeof(scrDecoration), name);
                if (m == null) { reason = "missing " + name; return false; }
                var p = Harmony.GetPatchInfo(m);
                if (p == null) continue;
                // Even an own patch is rejected unless its exact implementation is
                // the known optional setter diagnostic (not a gameplay hook).
                foreach (var x in p.Prefixes)
                    if (x.PatchMethod.DeclaringType != typeof(FrameParts) || x.PatchMethod.Name != nameof(FrameParts.SetPre))
                    { reason = name + " prefix " + x.owner; return false; }
                foreach (var x in p.Postfixes)
                    if (x.PatchMethod.DeclaringType != typeof(FrameParts) || x.PatchMethod.Name != nameof(FrameParts.SetPost))
                    { reason = name + " postfix " + x.owner; return false; }
                foreach (var x in p.Transpilers)
                    if (name != "UpdatePosition" || x.PatchMethod.DeclaringType != typeof(MoveApply)
                        || x.PatchMethod.Name != nameof(MoveApply.Transpiler))
                    { reason = name + " transpiler " + x.owner; return false; }
                if (p.Finalizers.Count != 0)
                { reason = name + " finalizer"; return false; }
            }
            reason = "ok (explicit switch-time snapshot)";
            return true;
        }
        internal static void Reset()
        {
            TransformOn = ColorOn = false;
            compatible = false;
            measureAt = 0;
            // Do not leave stale native transforms when switching the candidate off.
            reason = "off";
            try { InvisibleSkip.ApplyAllLazy(); }
            catch (Exception ex) { reason = "off drain " + ex.GetType().Name; }
        }
        internal static string Command(string arg)
        {
            if (arg == "begin")
            {
                measureFrame = Time.frameCount; measureSong = scrConductor.instance.songposition_minusi;
                measureTransforms = Transforms; measureColors = Colors;
                measureAt = Stopwatch.GetTimestamp();
                return "window_begin transform=" + TransformOn + " color=" + ColorOn
                    + " song=" + measureSong.ToString("R", CultureInfo.InvariantCulture);
            }
            if (arg == "end")
            {
                if (measureAt == 0) return "window_end UNMEASURED no begin";
                double seconds = (Stopwatch.GetTimestamp() - measureAt) / (double)Stopwatch.Frequency;
                int frames = Time.frameCount - measureFrame; measureAt = 0;
                return "window_end transform=" + TransformOn + " color=" + ColorOn + " frames=" + frames
                    + " seconds=" + seconds.ToString("R", CultureInfo.InvariantCulture)
                    + " fps=" + (frames / seconds).ToString("R", CultureInfo.InvariantCulture)
                    + " song_start=" + measureSong.ToString("R", CultureInfo.InvariantCulture)
                    + " song_end=" + scrConductor.instance.songposition_minusi.ToString("R", CultureInfo.InvariantCulture)
                    + " transforms=" + (Transforms - measureTransforms) + " colors=" + (Colors - measureColors)
                    + " scope=CPU_frame_rate_no_per_frame_probe worst_ms=UNMEASURED";
            }
            if (arg == "off") Reset();
            else if (arg == "transform" || arg == "color" || arg == "on")
            {
                try
                {
                    Reset();
                    compatible = CheckCompatibility();
                    TransformOn = compatible && arg != "color";
                    ColorOn = compatible && arg != "transform";
                }
                catch (Exception ex) { TransformOn = ColorOn = compatible = false; reason = ex.GetType().Name; }
            }
            else if (arg == "fixture") return DecoAnim.HiddenFixture();
            else if (arg != "state") return "hiddensynergy off|transform|color|on|state|fixture";
            return "transform=" + TransformOn + " color=" + ColorOn + " transforms=" + Transforms
                + " colors=" + Colors + " compatibility=" + reason + " scope=research_only";
        }
    }
}
#endif
