using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Keep the original order, including fallback calls. Returning an empty list suppresses
    // only the manager's already-completed loop, not its tiling/mask preparation or epilogue.
    internal static class TightLate
    {
        internal static bool Enabled = true;
        private sealed class RefEq : IEqualityComparer<scrDecoration>
        {
            public bool Equals(scrDecoration a, scrDecoration b) => ReferenceEquals(a, b);
            public int GetHashCode(scrDecoration d) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d);
        }
        private sealed class Entry
        {
            internal scrDecoration D;
            internal Transform Pivot;
            internal int Epoch = -1;
            internal bool ParentOK, Rejected;
        }
        private static readonly Dictionary<scrDecoration, Entry> records = new Dictionary<scrDecoration, Entry>(new RefEq());
        private static readonly List<scrDecoration> empty = new List<scrDecoration>(0);
        private static Entry[] slots = Array.Empty<Entry>();
        private static readonly AccessTools.FieldRef<scrDecoration, bool> rendererOn = AccessTools.FieldRefAccess<scrDecoration, bool>("rendererEnabled");
        private static readonly AccessTools.FieldRef<scrDecoration, Color> rendererColor = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        private static readonly AccessTools.FieldRef<scrVisualDecoration, bool> meshOn = AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        private static readonly AccessTools.FieldRef<scrParallax, bool> inited = AccessTools.FieldRefAccess<scrParallax, bool>("inited");
        private static readonly AccessTools.FieldRef<scrParallax, bool> isDeco = AccessTools.FieldRefAccess<scrParallax, bool>("isDecoration");
        private static readonly AccessTools.FieldRef<scrParallax, Transform> cached = AccessTools.FieldRefAccess<scrParallax, Transform>("cachedTransform");
        private static readonly AccessTools.FieldRef<scrParallax, Vector2> start = AccessTools.FieldRefAccess<scrParallax, Vector2>("startPosition");
        private static readonly AccessTools.FieldRef<scrParallax, Vector2> end = AccessTools.FieldRefAccess<scrParallax, Vector2>("endPosition");
        private static scrDecorationManager owner, completedOwner;
        private static scrCamera ownerCamera;
        private static Transform parentTransform, cameraTransform;
        private static bool parentIdentity, compatible;
        private static int preparedEpoch = -1;
        private static string compatibility = "pending";
        private static int epoch, completedFrame = -1;
        private static bool installed;
        internal static bool Verify;
        private static long frames, managerFrames, processed, checks, mismatches, invalidations, loopTicks, managerTicks, sample;
        private static int lastProcessed;
        private static readonly long[] reasons = new long[8];
        private static string first = "none";

        internal static void Install(Harmony h)
        {
            // No additional patches on per-decoration UpdatePosition/LogicUpdate/setters.
            // All mutable managed data is read in place; only the parent relationship is cached.
            h.Patch(AccessTools.Method(typeof(scrDecoration), "Setup"),
                postfix: new HarmonyMethod(typeof(TightLate), nameof(Invalidate)));
            var reorderType = AccessTools.TypeByName("ADOFAI.LevelEditor.Controls.PropertyControl_DecorationsList")
                ?? AccessTools.TypeByName("PropertyControl_DecorationsList");
            var reorder = reorderType == null ? null : AccessTools.Method(reorderType, "OnItemDropSides");
            if (reorder != null) h.Patch(reorder, prefix: new HarmonyMethod(typeof(TightLate), nameof(Invalidate)),
                finalizer: new HarmonyMethod(typeof(TightLate), nameof(ReorderEnd)));
            h.Patch(AccessTools.Method(typeof(scrDecorationManager), "LateUpdate"),
                prefix: new HarmonyMethod(typeof(TightLate), nameof(ManagerStart)) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(TightLate), nameof(ManagerEnd)) { priority = Priority.Last });
            installed = true;
        }
        public static void Invalidate() { unchecked { epoch++; } invalidations++; }
        public static Exception ReorderEnd(Exception __exception) { Invalidate(); return __exception; }
        internal static void Forget()
        {
            records.Clear(); Array.Clear(slots, 0, slots.Length); owner = completedOwner = null;
            ownerCamera = null; parentTransform = cameraTransform = null; preparedEpoch = -1;
            completedFrame = -1; Invalidate();
        }
        internal static void Uninstall() { installed = false; Forget(); }
        public static void ManagerStart(out long __state)
        {
            __state = installed ? Stopwatch.GetTimestamp() : 0;
            completedFrame = -1; // a second invocation in the same frame still runs independently
        }
        public static void ManagerEnd(long __state)
        {
            if (__state != 0) { managerTicks += Stopwatch.GetTimestamp() - __state; managerFrames++; }
        }

        // Called at exactly the old allDecorations load, after manager mask preparation.
        public static List<scrDecoration> List(scrDecorationManager mgr)
        {
            if (completedFrame == Time.frameCount && ReferenceEquals(completedOwner, mgr)) return empty;
            var list = Dormancy.List(mgr);
            if (!installed || !Enabled || !Hitch.Playing || SceneReset.Resetting
                || !MoveApply.Enabled || !Dormancy.Enabled || ADOBase.controller == null
                || !ADOBase.controller.disableV15Features || ADOBase.customLevel == null) return list;
            if (!ReferenceEquals(owner, mgr)) { Forget(); owner = mgr; }
            if (preparedEpoch != epoch || !ReferenceEquals(ownerCamera, ADOBase.controller.camy))
            {
                ownerCamera = ADOBase.controller.camy; cameraTransform = ownerCamera.transform;
                parentTransform = mgr.transform; parentIdentity = Identity(parentTransform);
                compatible = Compatible(); preparedEpoch = epoch;
            }
            if (!compatible) return list;
            var camera = cameraTransform;
            Vector3 cam = camera.position; // once per manager invocation
            var parent = parentTransform;
            bool parentOK = parentIdentity;
            if (slots.Length < list.Count) Array.Resize(ref slots, Math.Max(list.Count, slots.Length * 2 + 64));
            long t = Stopwatch.GetTimestamp();
            lastProcessed = 0;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var d = list[i];
                    var e = slots[i];
                    if (e == null || !ReferenceEquals(e.D, d))
                    {
                        e = null;
                        if ((object)d != null)
                        {
                            if (!records.TryGetValue(d, out e)) { e = new Entry { D = d }; records.Add(d, e); }
                        }
                        slots[i] = e;
                    }
                    if (Process(e, d, parent, parentOK, camera, cam)) { lastProcessed++; processed++; }
                }
            }
            finally { loopTicks += Stopwatch.GetTimestamp() - t; frames++; }
            completedOwner = mgr; completedFrame = Time.frameCount;
            return empty;
        }
        private static bool Process(Entry e, scrDecoration d, Transform parent, bool parentOK, Transform camera, Vector3 cam)
        {
            int reason = Eligible(e, parent, parentOK, camera);
            if (reason != 0)
            {
                reasons[reason]++;
                // Preserve Unity's destroyed-object null test on fallback, as the original manager does.
                if (d != null) MoveApply.LogicMaybe(d, true);
                return false;
            }
            Apply(e.D, e.D.parallax, e.Pivot, cam);
#if DEV || AUTOTEST
            if (Verify && (++sample & 63) == 0) Compare(e);
#endif
            return true;
        }
        private static int Eligible(Entry e, Transform parent, bool parentOK, Transform camera)
        {
            if (e == null || e.D.GetType() != typeof(scrVisualDecoration)) return 1;
            var d = e.D; var v = (scrVisualDecoration)d;
            if (e.Rejected) return 7;
            // Exact GetVisible equivalence for non-mask, hitbox-free visual decorations.
            if (d.hitbox != 0 || v.isMask() || meshOn(v)) return 2;
            if (!rendererOn(d) || rendererColor(d).a == 0f) return 3;
            if ((object)d.followPlanet != null || d.stickToFloor || d.lockRotation || d.lockScale) return 4;
            var p = d.parallax; var pivot = d.pivotTrans;
            if ((object)p == null || (object)pivot == null || !inited(p) || !isDeco(p)
                || !ReferenceEquals(p.decoration, d) || !ReferenceEquals(cached(p), pivot)
                || !ReferenceEquals(p.cameraTransform, camera) || p.clampToScreen || p.dontAlterX || p.dontAlterY) return 5;
            if (e.Epoch != epoch || !ReferenceEquals(e.Pivot, pivot))
            {
                e.Pivot = pivot; e.ParentOK = ReferenceEquals(pivot.parent, parent); e.Epoch = epoch;
            }
            if (!parentOK || !e.ParentOK) return 6;
            return 0;
        }
        private static bool Identity(Transform t)
        {
            var p = t.position; var r = t.rotation; var s = t.lossyScale;
            return p.x == 0f && p.y == 0f && p.z == 0f && r.x == 0f && r.y == 0f && r.z == 0f
                && r.w == 1f && s.x == 1f && s.y == 1f && s.z == 1f;
        }
        private static bool Compatible()
        {
            // Bypassing a foreign prefix/postfix would change that mod's semantics. Check on
            // lifecycle invalidation, never on each decoration. Keep Enabled intact for diagnosis.
            foreach (var spec in new[] {
                Tuple.Create(typeof(scrDecoration), "LogicUpdate"), Tuple.Create(typeof(scrDecoration), "UpdatePosition"),
                Tuple.Create(typeof(scrDecoration), "SetRotation"), Tuple.Create(typeof(scrDecoration), "SetScale"),
                Tuple.Create(typeof(scrDecoration), "get_camScaleMultiplier"), Tuple.Create(typeof(scrDecoration), "get_trueScaleMultiplier"),
                Tuple.Create(typeof(scrVisualDecoration), "GetVisible"), Tuple.Create(typeof(scrVisualDecoration), "GetAlpha"),
                Tuple.Create(typeof(scrVisualDecoration), "isMask"), Tuple.Create(typeof(scrVisualDecoration), "UpdateShader"),
                Tuple.Create(typeof(scrVisualDecoration), "EnableMeshRenderer"), Tuple.Create(typeof(scrParallax), "SetTrans"),
                Tuple.Create(typeof(scrParallax), "SetNewStartPosition") })
            {
                MethodBase method = AccessTools.Method(spec.Item1, spec.Item2);
                var patches = method == null ? null : Harmony.GetPatchInfo(method);
                if (patches == null) continue;
                foreach (var id in patches.Owners)
                    if (id != Main.Entry.Info.Id)
                    {
                        string message = spec.Item1.Name + "." + spec.Item2 + " owner=" + id;
                        if (compatibility != message) Main.Entry.Logger.Log("[tightlate] original loop retained: " + message);
                        compatibility = message; return false;
                    }
            }
            compatibility = "ok"; return true;
        }
        private static void Apply(scrDecoration d, scrParallax p, Transform pivot, Vector3 camera)
        {
            Vector2 pos = d.pivotPosVec + Vector2.zero;
            Vector3 pos3 = pos;
            start(p) = pos3;
            p.posCamAtStart = pos;
            Vector2 u = (Vector2)(camera - p.posCamAtStart) * new Vector2(p.multiplier_x, p.multiplier_y) + start(p);
            // lockScale=false: retain scalar multiply by 1 and both vector multiply orders.
            Vector2 camScale = 1f * (d.scaleMultiplier * d.textureScaleMultiplier);
            Vector2 offset = d.parallaxOffset * camScale / d.textureScaleMultiplier;
            Vector2 result = u + offset;
            end(p) = result;
            pivot.position = new Vector3(result.x, result.y, 0f);
            pivot.rotation = Quaternion.Euler(0f, 0f, d.startRot + d.rotAngle);
            Vector2 scale = d.scaleVec * camScale * (Vector2)Vector3.one;
            pivot.localScale = new Vector3(scale.x, scale.y, 1f);
        }
#if DEV || AUTOTEST
        private struct Snapshot
        {
            internal Vector3 P, S, Cam;
            internal Quaternion R;
            internal Vector2 Start, End, Scale;
            internal float Angle;
        }
        private static Snapshot Read(Entry e) => new Snapshot { P = e.Pivot.position, R = e.Pivot.rotation,
            S = e.Pivot.localScale, Cam = e.D.parallax.posCamAtStart, Start = start(e.D.parallax),
            End = end(e.D.parallax), Scale = e.D.scaleVec, Angle = e.D.rotAngle };
        private static bool Bit(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        private static bool Bit(Vector2 a, Vector2 b) => Bit(a.x, b.x) && Bit(a.y, b.y);
        private static bool Bit(Vector3 a, Vector3 b) => Bit(a.x, b.x) && Bit(a.y, b.y) && Bit(a.z, b.z);
        private static bool Bit(Quaternion a, Quaternion b) => Bit(a.x, b.x) && Bit(a.y, b.y) && Bit(a.z, b.z) && Bit(a.w, b.w);
        private static void Compare(Entry e)
        {
            var a = Read(e);
            // TightLate never prefixes UpdatePosition: direct call runs the original path and all existing patches.
            e.D.UpdatePosition();
            var b = Read(e); checks++;
            if (Bit(a.P,b.P) && Bit(a.R,b.R) && Bit(a.S,b.S) && Bit(a.Cam,b.Cam)
                && Bit(a.Start,b.Start) && Bit(a.End,b.End) && Bit(a.Scale,b.Scale) && Bit(a.Angle,b.Angle)) return;
            mismatches++; e.Rejected = true; // persist for this decoration until the level is released
            if (first == "none") first = e.D.name + " position=" + Bit(a.P,b.P) + " rotation=" + Bit(a.R,b.R)
                + " scale=" + Bit(a.S,b.S) + " parallax=" + (Bit(a.Cam,b.Cam) && Bit(a.Start,b.Start) && Bit(a.End,b.End))
                + " fast/original position=" + a.P.ToString("R") + "/" + b.P.ToString("R")
                + " rotation=" + a.R.ToString("R") + "/" + b.R.ToString("R")
                + " localScale=" + a.S.ToString("R") + "/" + b.S.ToString("R")
                + " start=" + a.Start.ToString("R") + "/" + b.Start.ToString("R")
                + " posCamAtStart=" + a.Cam.ToString("R") + "/" + b.Cam.ToString("R")
                + " end=" + a.End.ToString("R") + "/" + b.End.ToString("R")
                + " rotAngle=" + a.Angle.ToString("R") + "/" + b.Angle.ToString("R")
                + " scaleVec=" + a.Scale.ToString("R") + "/" + b.Scale.ToString("R");
            Main.Entry.Logger.Log("[tightlate] mismatch=" + mismatches + " decoration=" + e.D.name + "; fallback for this decoration; first=" + first);
        }
        internal static string Command(string arg)
        {
            if (arg == "verify") { Verify = true; return "verify on (1/64); " + State(); }
            if (arg == "verify off") { Verify = false; return "verify off; " + State(); }
            if (arg == "bench") return Bench();
            return State();
        }
        private static string Bench()
        {
            const int n = 2150, repeat = 200;
            var root = new GameObject("tightlate-bench");
            var ds = new scrDecoration[n]; var ps = new scrParallax[n]; var ts = new Transform[n];
            var es = new Entry[n];
            bool oldVerify = Verify;
            try
            {
                Verify = false;
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject("tl" + i); ts[i] = go.transform; ts[i].SetParent(root.transform, false);
                    // Managed field containers only: no AddComponent/Awake or game callbacks.
                    ds[i] = (scrDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                    ps[i] = (scrParallax)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrParallax));
                    ds[i].pivotPosVec = new Vector2(i * .01f, -i * .02f); ds[i].scaleVec = Vector2.one;
                    ds[i].scaleMultiplier = 1f; ds[i].textureScaleMultiplier = Vector2.one;
                    ds[i].rotAngle = i; ps[i].multiplier_x = .3f; ps[i].multiplier_y = .6f;
                    ds[i].parallax = ps[i]; ds[i].pivotTrans = ts[i]; rendererOn(ds[i]) = true; rendererColor(ds[i]) = Color.white;
                    inited(ps[i]) = true; isDeco(ps[i]) = true; cached(ps[i]) = ts[i]; ps[i].decoration = ds[i];
                    es[i] = new Entry { D = ds[i], Pivot = ts[i], ParentOK = true, Epoch = epoch };
                }
                var parent = root.transform;
                var cam = new Vector3(2f, 3f, -10f);
                for (int r = 0; r < 5; r++) for (int i = 0; i < n; i++) Process(es[i], ds[i], parent, true, null, cam);
                long t = Stopwatch.GetTimestamp();
                for (int r = 0; r < repeat; r++)
                {
                    cam.x = 2f + r * .01f;
                    for (int i = 0; i < n; i++) Process(es[i], ds[i], parent, true, null, cam);
                }
                double us = (Stopwatch.GetTimestamp() - t) * 1e6 / Stopwatch.Frequency / repeat / n;
                return "Process (eligibility+Apply) N=2150 R=200 us/deco=" + us.ToString("F4") + " ms/frame=" + (us*n/1000).ToString("F3")
                    + "; excludes slot synchronization/cold parent check/fallback/verify; budget<=0.5us=" + (us <= .5);
            }
            finally { Verify = oldVerify; UnityEngine.Object.Destroy(root); }
        }
#endif
        internal static string State() => "enabled=" + Enabled + " installed=" + installed + " cached=" + records.Count
            + " batch/last=" + lastProcessed + " processed=" + processed + " frames=" + frames + " invalidations=" + invalidations
            + " loop-ms/frame=" + (frames == 0 ? 0 : loopTicks * 1000.0 / Stopwatch.Frequency / frames).ToString("F4")
            + " manager-frames=" + managerFrames + " manager-ms/frame=" + (managerFrames == 0 ? 0 : managerTicks * 1000.0 / Stopwatch.Frequency / managerFrames).ToString("F4")
            + " verify=" + Verify + " checked=" + checks + " mismatch=" + mismatches + " first=" + first
            + " compatibility=" + compatibility
            + " excluded(type/hitbox-mask-mesh/hidden/follow-stick-lock/parallax/parent/rejected)=" + string.Join("/", reasons);
    }
}
