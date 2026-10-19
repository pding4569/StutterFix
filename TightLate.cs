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
        internal sealed class Entry
        {
            internal DecoEngine.State Engine;
            internal bool EngineRejected;
            internal int EngineDeferredFrame = -100;
            internal scrDecoration D;
            internal Transform Pivot;
            internal int Epoch = -1;
            internal bool ParentOK, Rejected;
            // 직전에 엔진에 쓴 값(입력). 같으면 설정자를 건너뛴다(같은 값을 다시 써도 엔진 상태는 같고, 다음 렌더 갱신만 헛일이다).
            internal bool Has;
            internal int LastFrame = -100;   // 마지막으로 빠른 길에서 처리한 Unity 프레임 (건너뛴 프레임이 있으면 직전 값을 믿지 않는다)
            internal Vector3 LastPos, LastScale;
            internal Quaternion LastRot;
        }
        internal static bool SkipSame = true;   // 모드 폴더 tightskip-off 파일로 끔
        internal static bool EulerMemo;          // 모드 폴더 eulermemo-on 파일로 켬 (같은 각도의 Quaternion.Euler 결과 재사용, 순수 함수라 비트 동일)
        private static bool eulerValid; private static int eulerBits; private static Quaternion eulerQ; internal static long eulerCalls, eulerHits;
        internal static long writes, skipPos, skipRot, skipScale;
        private static readonly Dictionary<scrDecoration, Entry> records = new Dictionary<scrDecoration, Entry>(new RefEq());
        private static readonly List<scrDecoration> empty = new List<scrDecoration>(0);
        private static readonly List<Entry> engineDeferred = new List<Entry>();
        internal static long EngineFixups;
        private static readonly long[] engineAdmissionReasons = new long[8];
        internal static string EngineAdmissionReasons => string.Join("/", engineAdmissionReasons);
        internal static bool EngineAdmissionVisible(scrDecoration d)
            => rendererOn(d) && rendererColor(d).a != 0f;
        internal static void EngineDefer(Entry e)
        {
            if (e.EngineDeferredFrame == Time.frameCount) return;
            e.EngineDeferredFrame = Time.frameCount; engineDeferred.Add(e);
        }
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
                postfix: new HarmonyMethod(typeof(TightLate), nameof(EngineLatePostfix))
                    { priority = Priority.Low, before = new[] { "StutterFix.fuseprobe" } },
                finalizer: new HarmonyMethod(typeof(TightLate), nameof(ManagerEnd)) { priority = Priority.Last });
            installed = true;
        }
        public static void Invalidate() { unchecked { epoch++; } invalidations++; }
        public static Exception ReorderEnd(Exception __exception) { Invalidate(); return __exception; }
        internal static void Forget()
        {
            DecoEngine.RestoreAll();
            records.Clear(); Array.Clear(slots, 0, slots.Length); owner = completedOwner = null;
            engineDeferred.Clear();
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
            if (engineDeferred.Count != 0) EngineLatePostfix(); // exception recovery; normal postfix already drained it
            if (__state != 0) { managerTicks += Stopwatch.GetTimestamp() - __state; managerFrames++; }
        }
        public static void EngineLatePostfix()
        {
            // Same final input/time as MoveApply.ManagerLatePostfix's hidden
            // fixup. Keep entries even when the final Rec completed or the owner
            // was released after Update. No per-decoration HashSet registration.
            for (int i = 0; i < engineDeferred.Count; i++)
            {
                var e = engineDeferred[i];
                if (e.EngineDeferredFrame == -100) continue;
                e.EngineDeferredFrame = -100;
                var d = e.D;
                if (d == null) continue;
                try { if (!d.GetVisible()) { d.UpdatePosition(); e.Has = false; EngineFixups++; } }
                catch { e.EngineRejected = true; }
            }
            engineDeferred.Clear();
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
#if DEV || AUTOTEST
            if (DecoGpu.SkipLate(d)) { if (e != null) e.Has = false; return true; }
#endif
            int reason = Eligible(e, parent, parentOK, camera);
            if (reason == 3 && e.Engine != null && Eligible(e, parent, parentOK, camera, true) == 0)
            {
                // Hidden residents stay owned. LogicMaybe retains the original
                // shader/hidden behavior; it does not write UpdatePosition here.
                e.Engine.Visible = false; e.Has = false; e.LastFrame = Time.frameCount;
                MoveApply.LogicMaybe(d, true); return false;
            }
            if (reason != 0)
            {
                if (e != null && e.Engine != null) DecoEngine.ExitDecoration(d, reason);
                reasons[reason]++;
                if (e != null) e.Has = false;   // 원래 경로가 Transform 을 쓸 수 있다
                // Preserve Unity's destroyed-object null test on fallback, as the original manager does.
                if (d != null) MoveApply.LogicMaybe(d, true);
                return false;
            }
            int fc = Time.frameCount;
            if (e.LastFrame != fc - 1 && e.LastFrame != fc) e.Has = false;   // 지난 프레임을 건너뛰었다(숨김·잠듦·원래 경로 등): 처음부터 다시 쓴다
            e.LastFrame = fc;
            bool engine = e.Engine != null;
            if (e.Engine != null) DecoEngine.Late(e.Engine, cam);
            else Apply(e, e.D, e.D.parallax, e.Pivot, cam);
#if DEV || AUTOTEST
            bool lateSample = engine && MoveApply.VerifyLate && ((MoveApply.LateChecked++ & 31) == 0);
            if ((Verify && (++sample & 63) == 0) || lateSample) Compare(e, lateSample);
#endif
            return true;
        }
        private static int Eligible(Entry e, Transform parent, bool parentOK, Transform camera, bool allowHidden = false)
        {
            if (e == null || e.D.GetType() != typeof(scrVisualDecoration)) return 1;
            var d = e.D; var v = (scrVisualDecoration)d;
            if (e.Rejected) return 7;
            // Exact GetVisible equivalence for non-mask, hitbox-free visual decorations.
            if (d.hitbox != 0 || v.isMask() || meshOn(v)) return 2;
            if (!allowHidden && (!rendererOn(d) || rendererColor(d).a == 0f)) return 3;
            if ((object)d.followPlanet != null || d.stickToFloor || d.lockRotation || d.lockScale) return 4;
            var p = d.parallax; var pivot = d.pivotTrans;
            if ((object)p == null || (object)pivot == null || !inited(p) || !isDeco(p)
                || !ReferenceEquals(p.decoration, d) || !ReferenceEquals(cached(p), pivot)
                || !ReferenceEquals(p.cameraTransform, camera) || p.clampToScreen || p.dontAlterX || p.dontAlterY) return 5;
            if (e.Epoch != epoch || !ReferenceEquals(e.Pivot, pivot))
            {
                e.Pivot = pivot; e.ParentOK = ReferenceEquals(pivot.parent, parent); e.Epoch = epoch; e.Has = false;
            }
            if (!parentOK || !e.ParentOK) return 6;
            return 0;
        }
        internal static List<scrDecoration> AllDecorations() { var l = new List<scrDecoration>(records.Count); foreach (var k in records.Keys) l.Add(k); return l; }
        internal static Entry EngineEntry(scrDecoration d, out int missing)
        {
            missing = 0;
            if (!installed || !Enabled || !compatible || !Hitch.Playing || SceneReset.Resetting
                || !MoveApply.Enabled || !Dormancy.Enabled || preparedEpoch != epoch) { missing = 1; return null; }
            if (!records.TryGetValue(d, out var e)) { missing = 2; return null; }
            if (e.EngineRejected) { missing = 3; return null; }
            int reason = Eligible(e, parentTransform, parentIdentity, cameraTransform);
            if (reason != 0) engineAdmissionReasons[reason]++;
            if (reason != 0) { missing = reason == 3 ? 4 : 5; return null; }
            // Admission follows the original Step. A just-unhidden decoration can
            // safely join now: this Late will establish its first current input.
            // Stale cached Transform values must never suppress that first write.
            if (e.LastFrame != Time.frameCount - 1 && e.LastFrame != Time.frameCount) { e.Has = false; missing = 6; }
            else if (e.LastFrame == Time.frameCount) missing = 7;
            return e;
        }
        internal static string EngineReasonDetail(Entry e)
        {
            var l = new List<string>();
            if (e.EngineRejected) l.Add("EngineRejected");
            if (!installed) l.Add("!installed"); if (!Enabled) l.Add("!Enabled"); if (!compatible) l.Add("!compatible");
            if (!Hitch.Playing) l.Add("!Hitch.Playing"); if (SceneReset.Resetting) l.Add("SceneReset.Resetting");
            if (!MoveApply.Enabled) l.Add("!MoveApply.Enabled"); if (!Dormancy.Enabled) l.Add("!Dormancy.Enabled");
            if (preparedEpoch != epoch) l.Add("epoch " + preparedEpoch + "!=" + epoch);
            if (completedFrame != Time.frameCount - 1 && completedFrame != Time.frameCount) l.Add("completedFrame=" + completedFrame + " fc=" + Time.frameCount);
            return string.Join(",", l);
        }
        internal static int EngineReason(Entry e)
        {
            if (e.EngineRejected) return 7;
            if (!installed || !Enabled || !compatible || !Hitch.Playing || SceneReset.Resetting
                || !MoveApply.Enabled || !Dormancy.Enabled || preparedEpoch != epoch) return 7;
            // Dormancy may remove a hidden resident from the manager list. The
            // manager heartbeat, rather than that entry's visible heartbeat,
            // proves that deferral is available on the next visible frame.
            if (completedFrame != Time.frameCount - 1 && completedFrame != Time.frameCount) return 7;
            int reason = Eligible(e, parentTransform, parentIdentity, cameraTransform);
            if (reason == 3 && e.Engine != null)
                reason = Eligible(e, parentTransform, parentIdentity, cameraTransform, true);
            return reason;
        }
        // List() 안(Process 루프)에서는 completedFrame 이 아직 -1 이다(ManagerStart 가 지움, 루프 끝에서 채움). 그 검사만 뺀 판.
        internal static int EngineReasonInLoop(Entry e)
        {
            if (e.EngineRejected) return 7;
            if (!installed || !Enabled || !compatible || !Hitch.Playing || SceneReset.Resetting
                || !MoveApply.Enabled || !Dormancy.Enabled || preparedEpoch != epoch) return 7;
            int reason = Eligible(e, parentTransform, parentIdentity, cameraTransform);
            if (reason == 3 && e.Engine != null)
                reason = Eligible(e, parentTransform, parentIdentity, cameraTransform, true);
            return reason;
        }
        internal static void EngineApply(Entry e, Vector3 cam, Vector2 pos, float angle, Vector2 scale)
            => Apply(e, e.D, e.D.parallax, e.Pivot, cam, pos, angle, scale);
        internal static Entry EngineBenchEntry(scrDecoration d)
        {
            var p = d.parallax;
            rendererOn(d) = true; rendererColor(d) = Color.white;
            inited(p) = true; isDeco(p) = true; cached(p) = d.pivotTrans; p.decoration = d;
            return new Entry { D = d, Pivot = d.pivotTrans, ParentOK = true, Epoch = epoch };
        }
        internal static int EngineBenchReason(Entry e, Transform parent) => Eligible(e, parent, true, null);
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
        private static bool Same(Vector3 a, Vector3 b) => BitConverter.SingleToInt32Bits(a.x) == BitConverter.SingleToInt32Bits(b.x) && BitConverter.SingleToInt32Bits(a.y) == BitConverter.SingleToInt32Bits(b.y) && BitConverter.SingleToInt32Bits(a.z) == BitConverter.SingleToInt32Bits(b.z);
        private static bool Same(Quaternion a, Quaternion b) => BitConverter.SingleToInt32Bits(a.x) == BitConverter.SingleToInt32Bits(b.x) && BitConverter.SingleToInt32Bits(a.y) == BitConverter.SingleToInt32Bits(b.y) && BitConverter.SingleToInt32Bits(a.z) == BitConverter.SingleToInt32Bits(b.z) && BitConverter.SingleToInt32Bits(a.w) == BitConverter.SingleToInt32Bits(b.w);
        private static void Apply(Entry e, scrDecoration d, scrParallax p, Transform pivot, Vector3 camera)
            => Apply(e, d, p, pivot, camera, d.pivotPosVec, d.rotAngle, d.scaleVec);
        private static void Apply(Entry e, scrDecoration d, scrParallax p, Transform pivot, Vector3 camera,
            Vector2 position, float angle, Vector2 scaling)
        {
            Vector2 pos = position + Vector2.zero;
            Vector3 pos3 = pos;
            start(p) = pos3;
            p.posCamAtStart = pos;
            Vector2 u = (Vector2)(camera - p.posCamAtStart) * new Vector2(p.multiplier_x, p.multiplier_y) + start(p);
            // lockScale=false: retain scalar multiply by 1 and both vector multiply orders.
            Vector2 camScale = 1f * (d.scaleMultiplier * d.textureScaleMultiplier);
            Vector2 offset = d.parallaxOffset * camScale / d.textureScaleMultiplier;
            Vector2 result = u + offset;
            end(p) = result;
            var np = new Vector3(result.x, result.y, 0f);
            Quaternion nr;
            if (EulerMemo)
            {
                float ang = d.startRot + angle; int ab = BitConverter.SingleToInt32Bits(ang); eulerCalls++;
                if (eulerValid && ab == eulerBits) { nr = eulerQ; eulerHits++; }
                else { nr = Quaternion.Euler(0f, 0f, ang); eulerQ = nr; eulerBits = ab; eulerValid = true; }
            }
            else nr = Quaternion.Euler(0f, 0f, d.startRot + angle);
            Vector2 scale = scaling * camScale * (Vector2)Vector3.one;
            var ns = new Vector3(scale.x, scale.y, 1f);
            if (e.Engine != null && DecoEngine.ShouldVerify(e.Engine))
                DecoEngine.CheckLate(e.Engine, camera, start(p), p.posCamAtStart, end(p), np, nr, ns);
            writes++;
            if (SkipSame && e.Has)
            {
                if (Same(np, e.LastPos)) skipPos++; else { pivot.position = np; e.LastPos = np; }
                if (Same(nr, e.LastRot)) skipRot++; else { pivot.rotation = nr; e.LastRot = nr; }
                if (Same(ns, e.LastScale)) skipScale++; else { pivot.localScale = ns; e.LastScale = ns; }
                return;
            }
            pivot.position = np; pivot.rotation = nr; pivot.localScale = ns;
            e.LastPos = np; e.LastRot = nr; e.LastScale = ns; e.Has = true;
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
        private static void Compare(Entry e, bool lateSample = false)
        {
            var a = Read(e);
            // TightLate never prefixes UpdatePosition: direct call runs the original path and all existing patches.
            e.D.UpdatePosition();
            var b = Read(e); checks++;
            if (Bit(a.P,b.P) && Bit(a.R,b.R) && Bit(a.S,b.S) && Bit(a.Cam,b.Cam)
                && Bit(a.Start,b.Start) && Bit(a.End,b.End) && Bit(a.Scale,b.Scale) && Bit(a.Angle,b.Angle)) return;
            mismatches++; e.Rejected = true; // persist for this decoration until the level is released
            if (lateSample) MoveApply.LateMismatch++;
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
            + " writes=" + writes + " skipped(pos/rot/scale)=" + skipPos + "/" + skipRot + "/" + skipScale + " skipSame=" + SkipSame + " eulerMemo=" + EulerMemo + " euler(hit/calls)=" + eulerHits + "/" + eulerCalls
            + " verify=" + Verify + " checked=" + checks + " mismatch=" + mismatches + " first=" + first
            + " compatibility=" + compatibility
            + " excluded(type/hitbox-mask-mesh/hidden/follow-stick-lock/parallax/parent/rejected)=" + string.Join("/", reasons);
    }
}
