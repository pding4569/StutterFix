#if DEV || AUTOTEST
using System;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        // Explicit, synchronous diagnostic on real hidden objects, never timed as
        // gameplay. No new FieldRefs: use the module's existing logical fields.
        internal static string HiddenFixture()
        {
            if (!HiddenDecoSynergy.TransformOn || !HiddenDecoSynergy.ColorOn || !Hitch.Playing
                || FrameCompare.Paused || scrDecorationManager.instance == null)
                return "fixture requires hiddensynergy on during playback";
            int objects = 0, pairs = 0, differences = 0, fallbacks = 0, colorPairs = 0;
            string error = "none";
            int precheckBefore = Precheck.Active;
            try
            {
                // A prepared no-op plan may conservatively reject every target in
                // this exact command frame. Invalidate that cache for this explicit
                // diagnostic only; production still rejects active plans.
                Precheck.ResetAll();
                InvisibleSkip.ApplyAllLazy();
                foreach (var d in scrDecorationManager.instance.allDecorations)
                {
                    if (!HiddenDecoSynergy.TransformEligible(d) || d.pivotTrans == null) continue;
                    float originalRot = rotRef(d); Vector2 originalScale = scaleRef(d);
                    try
                    {
                        // Normalize both paths at the same camera/time. A deferred
                        // SetPosition must agree with an actual UpdatePosition.
                        d.UpdatePosition();
                        foreach (float angle in new[] { 0f, 45.125f, -179.875f, 720f })
                        {
                            setRot(d, angle);
                            var expected = HiddenSnapshot(d);
                            setRot(d, originalRot);
                            if (!HiddenDecoSynergy.DeferTransform(d)) { fallbacks++; continue; }
                            rotRef(d) = angle;
                            InvisibleSkip.ApplyAllLazy();
                            if (!HiddenSame(expected, HiddenSnapshot(d))) differences++;
                            pairs++;
                        }
                        foreach (var scale in new[] { new Vector2(0f, 0f), new Vector2(.125f, -.75f), new Vector2(3.5f, 2f) })
                        {
                            setScale(d, scale);
                            var expected = HiddenSnapshot(d);
                            setScale(d, originalScale);
                            if (!HiddenDecoSynergy.DeferTransform(d)) { fallbacks++; continue; }
                            scaleRef(d) = scale;
                            InvisibleSkip.ApplyAllLazy();
                            if (!HiddenSame(expected, HiddenSnapshot(d))) differences++;
                            pairs++;
                        }
                        int tested;
                        differences += ColorFast.HiddenFixture(d, out tested);
                        colorPairs += tested;
                    }
                    finally
                    {
                        rotRef(d) = originalRot; scaleRef(d) = originalScale;
                        InvisibleSkip.ApplyAllLazy();
                        d.UpdatePosition();
                    }
                    if (++objects >= 32) break;
                }
            }
            catch (Exception ex) { error = ex.GetType().Name + ":" + ex.Message; }
            if (objects == 0 && error == "none") error = "UNMEASURED_no_eligible_objects";
            if (differences != 0 || error != "none") HiddenDecoSynergy.Reset();
            return "fixture objects=" + objects + " transform_pairs=" + pairs + " color_pairs=" + colorPairs
                + " mismatch=" + differences + " fallback=" + fallbacks + " error=" + error + " precheck_before=" + precheckBefore
                + " scope=same_frame_logical_and_flushed_transform_not_gameplay_performance";
        }
        private static float[] HiddenSnapshot(scrDecoration d)
        {
            var t = d.pivotTrans; var p = t.localPosition; var q = t.rotation; var s = t.localScale;
            var scale = scaleRef(d);
            return new[] { rotRef(d), scale.x, scale.y, p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z };
        }
        private static bool HiddenSame(float[] a, float[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i])) return false;
            return true;
        }
    }
}
#endif
