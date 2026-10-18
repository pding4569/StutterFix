#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Opt-in census only: no Harmony patches and no callbacks when stopped.
    // onPreRender sees completed LateUpdate transforms. One sample per rendered
    // main-camera frame; missing callbacks break runs rather than inventing frames.
    internal static class OffscreenProbe
    {
        private sealed class State
        {
            internal int Seen = -1, Mask, Count, Saved, Bucket, LastFrame = -2, Run;
            internal bool LeftCensored, RunFar;
        }
        // Buckets: inside, outside <=25%, <=50%, <=100%, >100%, alpha0, unknown.
        private static readonly string[] labels = { "inside", "outside_0_25", "outside_25_50", "outside_50_100", "outside_gt100", "alpha0", "unknown" };
        private static readonly string[] parLabels = { "parallax0", "parallax1", "parallaxOther", "noParallax" };
        private static readonly Dictionary<scrDecoration, State> states = new Dictionary<scrDecoration, State>();
        private static readonly List<scrDecoration> expired = new List<scrDecoration>();
        private static readonly List<Renderer> renderers = new List<Renderer>();
        private static readonly Plane[][] planes = { new Plane[6], new Plane[6], new Plane[6], new Plane[6] };
        private static readonly float[] margins = { 0, .25f, .5f, 1 };
        private static readonly long[,,] totals = new long[4, 7, 3]; // decorations, records, cheap-skip record subset
        private static readonly long[,] runs = new long[2, 4]; // completed / censored; frame length bins
        private static readonly AccessTools.FieldRef<scrDecoration, Color> color = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        private static readonly AccessTools.FieldRef<scrDecoration, scrParallax> parallax = AccessTools.FieldRefAccess<scrDecoration, scrParallax>("parallax");
        private static bool active;
        private static int frame = -1, samples, firstFrame, lastFrame, startFrame;
        private static float startSong, lastSong;
        private static long ticks, entriesNear, entriesFar, xy, all, farXY, farSaved, farDecos, unsafeFar, depthExcluded, renderExcluded, duplicateCallbacks;
        private static string aborted;

        internal static string Begin()
        {
            Stop();
            Array.Clear(totals, 0, totals.Length); Array.Clear(runs, 0, runs.Length);
            frame = -1; samples = 0; ticks = entriesNear = entriesFar = xy = all = farXY = farSaved = farDecos = unsafeFar = depthExcluded = renderExcluded = duplicateCallbacks = 0;
            aborted = null; startFrame = Time.frameCount; startSong = lastSong = Song();
            active = true;
            Camera.onPreRender += Capture;
            return "begin: every rendered main-camera frame, actual Renderer.bounds; margin is fraction of full width/height added on EACH side; song=" + startSong.ToString("F3");
        }
        internal static void Stop()
        {
            active = false; Camera.onPreRender -= Capture;
            states.Clear(); expired.Clear(); renderers.Clear();
        }
        private static float Song() => scrConductor.instance == null ? float.NaN : (float)scrConductor.instance.songposition_minusi;
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool Valid(Bounds b) => Finite(b.center.x) && Finite(b.center.y) && Finite(b.center.z) && Finite(b.extents.x) && Finite(b.extents.y) && Finite(b.extents.z);
        private static int Bin(int n) => n == 1 ? 0 : n <= 5 ? 1 : n <= 20 ? 2 : 3;
        private static void Close(State s, bool censored)
        {
            if (s.Run > 0) runs[censored || s.LeftCensored ? 1 : 0, Bin(s.Run)]++;
            s.Run = 0; s.RunFar = false;
        }
        private static bool Side(Plane p, Bounds b)
        {
            var e = b.extents; var n = p.normal;
            return p.GetDistanceToPoint(b.center) + Mathf.Abs(n.x)*e.x + Mathf.Abs(n.y)*e.y + Mathf.Abs(n.z)*e.z >= 0;
        }
        private static void Capture(Camera c)
        {
            if (!active || c == null || scrCamera.instance == null || c != scrCamera.instance.camobj) return;
            if (frame == Time.frameCount) { duplicateCallbacks++; return; }
            long t0 = Stopwatch.GetTimestamp();
            try { Sample(c); }
            catch (Exception e)
            {
                aborted = e.GetType().Name + ": " + e.Message;
                // Partial sample must not be interpreted as a valid census.
                active = false; Camera.onPreRender -= Capture;
                states.Clear(); renderers.Clear(); expired.Clear();
                Main.Entry.Logger.Log("[offscreenstat] ABORTED; discard entire interval: " + aborted);
            }
            finally { ticks += Stopwatch.GetTimestamp() - t0; }
        }
        private static void Sample(Camera c)
        {
            int now = Time.frameCount;
            float song = Song();
            if (!Hitch.Playing || SceneReset.Resetting || (Finite(lastSong) && Finite(song) && song < lastSong - .01f))
                throw new InvalidOperationException("play ended/reset/seek detected; begin again in a continuous song interval");
            frame = now; lastSong = song;
            if (samples == 0) firstFrame = now;
            lastFrame = now; samples++;
            var matrix = c.cullingMatrix;
            for (int i = 0; i < 4; i++)
            {
                var expanded = matrix;
                expanded.SetRow(0, matrix.GetRow(0) / (1 + 2*margins[i]));
                expanded.SetRow(1, matrix.GetRow(1) / (1 + 2*margins[i]));
                GeometryUtility.CalculateFrustumPlanes(expanded, planes[i]);
                for (int p = 0; p < 6; p++)
                    if (!Finite(planes[i][p].distance) || !Finite(planes[i][p].normal.sqrMagnitude) || planes[i][p].normal.sqrMagnitude < .5f)
                        throw new InvalidOperationException("invalid camera culling matrix");
            }
            var recs = DecoAnim.ProbeRecords;
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i]; if (r == null || !r.Running || r.D == null) continue;
                State s;
                if (!states.TryGetValue(r.D, out s)) { s = new State(); states.Add(r.D, s); }
                if (s.Seen != now) { s.Seen = now; s.Mask = s.Count = s.Saved = 0; }
                s.Count++;
                if (r.Key >= 0 && r.Key < 31) s.Mask |= 1 << r.Key;
                if (r.Key == 5 || r.Key == 7 || r.Key == 8 || r.Key == 9 || r.Key == 10) s.Saved++;
            }
            expired.Clear();
            foreach (var kv in states)
            {
                var d = kv.Key; var s = kv.Value;
                if (s.Seen != now || d == null) { Close(s, true); expired.Add(d); continue; }
                int bucket = Classify(d, c);
                var par = parallax(d);
                int group = par == null ? 3 : par.multiplier_x == 0 && par.multiplier_y == 0 ? 0 : par.multiplier_x == 1 && par.multiplier_y == 1 ? 1 : 2;
                totals[group, bucket, 0]++; totals[group, bucket, 1] += s.Count; totals[group, bucket, 2] += s.Saved;
                bool both = (s.Mask & 6) == 6;
                all++; if (both) xy++;
                if (bucket == 4)
                {
                    farDecos++; farSaved += s.Saved; if (both) farXY++;
                    if (d.hitbox != 0 || !(d is scrVisualDecoration)) unsafeFar++;
                }
                bool previousOutside = s.LastFrame == now - 1 && s.Bucket >= 1 && s.Bucket <= 4;
                bool outside = bucket >= 1 && bucket <= 4;
                if (!previousOutside) Close(s, true);
                if (outside)
                {
                    if (s.Run == 0) s.LeftCensored = s.LastFrame != now - 1 || s.Bucket != 0;
                    s.Run++; s.RunFar |= bucket == 4;
                }
                else if (previousOutside)
                {
                    if (bucket == 0) { if (s.RunFar) entriesFar++; else entriesNear++; }
                    Close(s, bucket != 0);
                }
                s.Bucket = bucket; s.LastFrame = now;
            }
            foreach (var d in expired) states.Remove(d);
            expired.Clear(); renderers.Clear();
        }
        private static int Classify(scrDecoration d, Camera c)
        {
            // Logical applied alpha includes floor opacity; do not equate forceRenderingOff with alpha0.
            float a = color(d).a;
            if (!Finite(a)) return 6;
            if (a <= 0) return 5;
            renderers.Clear(); d.GetComponentsInChildren<Renderer>(true, renderers);
            int nearest = 6; bool invalid = false;
            foreach (var r in renderers)
            {
                if (r == null || r.GetComponentInParent<scrDecoration>() != d) continue;
                var visual = d as scrVisualDecoration;
                if (visual != null && r != visual.spriteRenderer && r != visual.meshRenderer) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff || (c.cullingMask & (1 << r.gameObject.layer)) == 0) { renderExcluded++; continue; }
                var b = r.bounds;
                if (!Valid(b) || (b.extents.x == 0 && b.extents.y == 0 && b.extents.z == 0)) { invalid = true; continue; }
                if (!Side(planes[0][4], b) || !Side(planes[0][5], b)) { depthExcluded++; continue; }
                int found = 4;
                for (int i = 0; i < 4; i++) if (GeometryUtility.TestPlanesAABB(planes[i], b)) { found = i; break; }
                if (found < nearest) nearest = found;
            }
            // A missing/invalid bound cannot safely establish that a decoration is far.
            return invalid && nearest != 0 ? 6 : nearest;
        }
        internal static string End()
        {
            active = false; Camera.onPreRender -= Capture;
            foreach (var kv in states) Close(kv.Value, true);
            var sb = new StringBuilder();
            sb.Append("samples=").Append(samples).Append(" rendered-frame-span=").Append(samples == 0 ? 0 : lastFrame-firstFrame+1)
                .Append(" begin-frame=").Append(startFrame).Append(" song=").Append(startSong.ToString("F3")).Append("..").Append(lastSong.ToString("F3"))
                .Append(" duplicate-callbacks=").Append(duplicateCallbacks).Append(" probe-ms/sample=").Append((ticks*1000.0/Stopwatch.Frequency/Math.Max(1,samples)).ToString("F3"))
                .Append(" status=").Append(aborted ?? (samples == 0 ? "NO_SAMPLES" : "OK"));
            double n = Math.Max(1,samples);
            for (int g = 0; g < 4; g++)
                for (int b = 0; b < 7; b++)
                    sb.Append('\n').Append(parLabels[g]).Append(' ').Append(labels[b]).Append(" decos/frame=").Append((totals[g,b,0]/n).ToString("F3"))
                        .Append(" anims/frame=").Append((totals[g,b,1]/n).ToString("F3")).Append(" rotScaleColorOpacity/frame=").Append((totals[g,b,2]/n).ToString("F3"));
            long anims = 0, skip = 0;
            for (int g = 0; g < 4; g++) for (int b = 0; b < 7; b++) { anims += totals[g,b,1]; skip += totals[g,b,2]; }
            sb.Append("\nXY-record-pair=").Append(xy).Append('/').Append(all).Append(" farXY=").Append(farXY).Append('/').Append(farDecos)
                .Append(" far-risk-hitbox-or-nonvisual=").Append(unsafeFar).Append(" renderer-excluded=").Append(renderExcluded).Append(" depth-excluded=").Append(depthExcluded)
                .Append(" entries-near/far=").Append(entriesNear).Append('/').Append(entriesFar).Append(" entries/song-sec=").Append(((entriesNear+entriesFar)/Math.Max(.001,lastSong-startSong)).ToString("F3")).Append(" entries-per-1000samples=").Append(((entriesNear+entriesFar)*1000.0/n).ToString("F3"));
            for (int k = 0; k < 2; k++) sb.Append("\noutside-run ").Append(k == 0 ? "completed" : "censored-observed-lower-bound")
                .Append(" [1,2-5,6-20,>20]=").Append(runs[k,0]).Append(',').Append(runs[k,1]).Append(',').Append(runs[k,2]).Append(',').Append(runs[k,3]);
            double q = anims == 0 ? 0 : (double)farSaved/anims;
            double p = all == 0 ? 0 : (double)farDecos/all;
            double pLate = Math.Min(1, farDecos/n/2150.0);
            sb.Append("\nMODEL-ONLY q=far-skippable-records/all-records=").Append(q.ToString("F6"))
                .Append(" qWithinSkippable=").Append((skip == 0 ? 0 : (double)farSaved/skip).ToString("F6"))
                .Append(" p=far/animated-decos=").Append(p.ToString("F6"))
                .Append(" pLateProxy=(far-decos/frame)/2150=").Append(pLate.ToString("F6"))
                .Append(" uniform-cost-model-ms=3.3*q+2.4*pLateProxy=").Append((3.3*q+2.4*pLate).ToString("F4"))
                .Append(" check-cost-ms=(animated-decos/frame)*us/1000; animated-decos/frame=").Append((all/n).ToString("F3"))
                .Append("; count-weighted estimate, NOT measured time savings; LateUpdate denominator needs manager census");
            Stop(); return sb.ToString();
        }
    }
}
#endif
