#if DEV || AUTOTEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ADOFAI;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Shadow only: never calls StartEffect, setters, Kill or AdjustDurationForHardbake.
    internal static class FloorBake
    {
        private const string Owner = "StutterFix.FloorBake.Shadow";
        private static Harmony patches;
        internal static bool Enabled { get; private set; }
        private static int generation;
        private static bool preparing, late;
        private static FloorBakeCore.Tile[] initial;
        private static FloorBakeCore.Tile[] baseline;
        private static int[] baselineIds;
        private static FloorBakeCore.Table table;
        private static List<scrFloor> level;
        private static readonly Dictionary<scrFloor, int> levelIndices = new Dictionary<scrFloor, int>();
        private static List<ffxPlusBase> source;
        private static ffxPlusBase[] sourceSnapshot;
        private static int sourceCount;
        private static IEnumerator collector;
        private static Task<FloorBakeCore.Table> job;
        private static CancellationTokenSource cancel;
        private static string status = "꺼짐";
        private static float pitch, scale, over, period;
        private static double snapshotMs, maxSliceMs;
        private static long heapBefore, heapDelta;
        internal static FloorBakeCore.Table CaptureTable => table;
        internal static int Generation => table == null ? -1 : generation;
        internal struct Start { internal int Source, Frame; internal double Song; }
        private static void Log(string s) => Main.Entry.Logger.Log("[타일굽기/그림자] " + s);
        private static string N(double x) => x.ToString("G9", CultureInfo.InvariantCulture);
        private static MethodInfo Exact(Type type, string name, bool isStatic, params Type[] args)
        {
            var matches = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == name && !m.IsGenericMethod && m.ReturnType == typeof(void) && m.IsStatic == isStatic
                    && m.CallingConvention == (isStatic ? CallingConventions.Standard : CallingConventions.Standard | CallingConventions.HasThis)
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(args)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException(type.Name + "." + name + " exact=" + matches.Length);
            return matches[0];
        }
        internal static bool Step(string arg)
        {
            arg = (arg ?? "").Trim().ToLowerInvariant();
            if (arg == "ready") { Tick(); if (!Enabled || status.StartsWith("중단:")) throw new InvalidOperationException(status); return table != null; }
            try
            {
                switch (arg)
                {
                    case "on":
                        if (Enabled) { Log(status); break; }
                        if (Hitch.Playing || TileSim.Capturing || !ZeroTween.CanEase) throw new InvalidOperationException("정지 상태/이징 필요");
                        var prep = Exact(typeof(scnGame), "PrepVfx", true, typeof(List<scrFloor>), typeof(int), typeof(List<LevelEvent>), typeof(bool));
                        var scrub = Exact(typeof(scrVfxPlus), "ScrubToTime", false, typeof(float));
                        patches = new Harmony(Owner);
                        try {
                            patches.Patch(prep, prefix: new HarmonyMethod(typeof(FloorBake), nameof(PrepPrefix)), postfix: new HarmonyMethod(typeof(FloorBake), nameof(PrepPostfix)));
                            patches.Patch(scrub, prefix: new HarmonyMethod(typeof(FloorBake), nameof(ScrubPrefix)), postfix: new HarmonyMethod(typeof(FloorBake), nameof(ScrubPostfix)));
                        } catch { patches.UnpatchAll(Owner); patches = null; throw; }
                        Enabled = true; status = "켜짐: 맵 준비 대기";
                        // The requested open -> on order is supported conservatively: time-zero touched tiles are excluded.
                        if (ADOBase.lm?.listFloors != null && scrVfxPlus.instance?.effects != null) {
                            late = true; level = ADOBase.lm.listFloors; Snapshot(); BeginCollect();
                        }
                        Log(status); break;
                    case "off": Shutdown(); Log(status); break;
                    case "verify": Verify(); break;
                    case "bench": Bench(); break;
                    default: Log("tilebake on/off/ready/verify/bench; " + status); break;
                }
            }
            catch (Exception e) { if (arg == "on") Shutdown(); Fail(e.GetType().Name + " " + e.Message); }
            return true;
        }
        private static void Cancel()
        {
            generation++; table = null; collector = null; sourceSnapshot = null;
            cancel?.Cancel(); cancel?.Dispose(); cancel = null;
            if (job != null) job.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            job = null;
        }
        internal static void ForgetLevel()
        {
            TileSim.InvalidateBake("맵 변경"); Cancel(); initial = null; baseline = null; baselineIds = null; level = null; source = null; sourceSnapshot = null; preparing = false; levelIndices.Clear();
            status = Enabled ? "새 맵 준비 대기" : "꺼짐";
        }
        internal static void Shutdown()
        {
            Enabled = false; ForgetLevel(); TileSim.ForgetBake(); patches?.UnpatchAll(Owner); patches = null;
        }
        private static void Fail(string why) { TileSim.InvalidateBake(why); Cancel(); initial = null; preparing = false; status = "중단: " + why; Log(status); }
        private static void PrepPrefix(List<scrFloor> __0, int __1)
        {
            if (!Enabled) return;
            try {
                if (__1 != 0) {
                    // A selected-tile restart has no ScrubToTime(0). Reuse only the captured same-level baseline.
                    if (baseline == null || baselineIds == null || baselineIds.Length != __0.Count
                        || __0.Where((f, i) => f == null || f.GetInstanceID() != baselineIds[i]).Any())
                        throw new InvalidOperationException("selected-tile restart needs same-map zero baseline; first play from tile 0");
                    TileSim.InvalidateBake("선택 타일 재생"); Cancel(); level = __0; initial = Clone(baseline); preparing = true;
                    levelIndices.Clear(); for (int i = 0; i < level.Count; i++) levelIndices.Add(level[i], i);
                } else {
                    ForgetLevel(); level = __0; late = false; preparing = true;
                }
            } catch (Exception e) { Fail(e.Message); }
        }
        private static void ScrubPrefix(float __0)
        {
            if (!Enabled) return;
            try {
                if (preparing && __0 == 0) Snapshot();
                else { TileSim.InvalidateBake("seek/scrub: 이전 테이프 무효"); }
            } catch (Exception e) { Fail(e.Message); }
        }
        private static void ScrubPostfix()
        {
            // Game scrub removes already-triggered objects from the scheduling list.
            // The pre-scrub frozen sequence still belongs to the same pure timeline.
            if (Enabled && source != null && scrVfxPlus.instance?.effects == source) sourceCount = source.Count;
        }
        private static void PrepPostfix()
        {
            if (!Enabled || !preparing) return;
            preparing = false;
            try { if (initial == null) throw new InvalidOperationException("pre-scrub snapshot missing"); BeginCollect(); }
            catch (Exception e) { Fail(e.Message); }
        }
        private static void Snapshot()
        {
            var sw = Stopwatch.StartNew();
            if (level == null || level.Count == 0 || level.Count > 100000) throw new InvalidOperationException("tile-limit/missing");
            initial = new FloorBakeCore.Tile[level.Count];
            levelIndices.Clear();
            for (int i = 0; i < level.Count; i++) {
                var f = level[i]; if (f == null) throw new InvalidOperationException("null-tile");
                if (levelIndices.ContainsKey(f)) throw new InvalidOperationException("duplicate-tile");
                levelIndices.Add(f, i);
                var t = f.transform; var p = t.position; var s = t.localScale;
                var row = new FloorBakeCore.Tile { Id = f.GetInstanceID(), X = f.startPos.x, Y = f.startPos.y, Rotation = f.startRot.z,
                    Initial = new[] { p.x, p.y, f.tweenRot.z, s.x, s.y, f.opacity } };
                if (f.freeroamArea != null || f.freeroamGenerated) row.Reasons.Add("freeroam-unmapped");
                if (f.moveTweens != null && f.moveTweens.Values.Any(x => x != null && x.active)) row.Reasons.Add("existing-tween");
                if (!Mathf.Approximately(t.eulerAngles.z, f.tweenRot.z)) row.Reasons.Add("rotation-getter-wrap");
                initial[i] = row;
            }
            baseline = Clone(initial); baselineIds = initial.Select(x => x.Id).ToArray();
            snapshotMs = sw.Elapsed.TotalMilliseconds; maxSliceMs = snapshotMs;
        }
        private static FloorBakeCore.Tile[] Clone(FloorBakeCore.Tile[] rows)
        {
            return rows.Select(row => {
                var copy = new FloorBakeCore.Tile { Id = row.Id, X = row.X, Y = row.Y, Rotation = row.Rotation, Initial = (float[])row.Initial.Clone() };
                foreach (string reason in row.Reasons) copy.Reasons.Add(reason);
                return copy;
            }).ToArray();
        }
        private static bool Conditional(ffxPlusBase f) => f.runManually || f.runOnHit || (f.conditionalInfo != null && f.conditionalInfo.Any(x => x));
        private static void BeginCollect()
        {
            if (ADOBase.isOfficialLevel || ADOBase.levelIsMikoSkip || ADOBase.controller.visualQuality != VisualQuality.High)
                throw new InvalidOperationException("custom-normal/High only");
            source = scrVfxPlus.instance.effects; sourceCount = source.Count;
            if (source.Count > 300000) throw new InvalidOperationException("event-limit");
            sourceSnapshot = source.ToArray();
            pitch = scrConductor.instance.song.pitch; scale = DOTween.timeScale;
            over = DOTween.defaultEaseOvershootOrAmplitude; period = DOTween.defaultEasePeriod;
            heapBefore = GC.GetTotalMemory(false); cancel = new CancellationTokenSource();
            collector = Collect(); status = "스냅샷 수집 중";
        }
        private static readonly string[] SceneWriters = { "DivineIntervention", "LaserSpawner", "Mawaru", "SingSing_Notefield", "TaroBGScript", "ThirdSun", "scnMinesweeper", "Cutscene1", "PuzzleRoom", "scnTaroMenu2" };
        private static IEnumerator Collect()
        {
            var rows = initial; var frozen = sourceSnapshot;
            var events = new List<FloorBakeCore.Event>(); var scheduled = new HashSet<ffxPlusBase>(frozen);
            var slice = Stopwatch.StartNew(); long refs = 0;
            // T3 writers with unknown tile scope invalidate all tiles rather than guessing a range.
            foreach (string name in SceneWriters) {
                var type = AccessTools.TypeByName(name);
                if (type != null && UnityEngine.Object.FindObjectsOfType(type, true).Length != 0)
                    foreach (var row in rows) row.Reasons.Add("scene-writer:" + name);
            }
            foreach (var f in frozen) {
                if (f == null) continue;
                if (f is ffxMoveFloorPlus m) {
                    var e = Copy(m, rows.Length); events.Add(e); refs += e.Targets.Length;
                    if (refs > 4000000) throw new InvalidOperationException("target-ref-limit");
                    if (late && e.T0 <= 0) foreach (int d in e.Targets) rows[d].Reasons.Add("late-on-time-zero-initial");
                }
                else ExcludeWriter(f, rows);
                if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
            }
            for (int i = 0; i < level.Count; i++) {
                foreach (var f in level[i].plusEffects) {
                    if (f is ffxMoveFloorPlus m && !scheduled.Contains(f)) {
                        var e = Copy(m, rows.Length);
                        foreach (int d in e.Targets) rows[d].Reasons.Add("unscheduled-conditional/manual/hit");
                    } else ExcludeWriter(f, rows);
                }
                if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
            }
            // External patches on floor/movement writers cannot be assigned a safe scope.
            foreach (var type in new[] { typeof(scrFloor), typeof(ffxMoveFloorPlus) })
                foreach (var m in type.GetMethods(AccessTools.all)) {
                    var info = Harmony.GetPatchInfo(m);
                    if (info != null && info.Owners.Any(o => !o.StartsWith("StutterFix", StringComparison.Ordinal)))
                        foreach (var row in rows) row.Reasons.Add("foreign-writer-patch");
                }
            var input = events.ToArray(); float p = pitch, s = scale; var token = cancel.Token;
            job = Task.Run(() => FloorBakeCore.Compile(rows, input, p, s, (id, t, dur, ov, per) => ZeroTween.Eval((Ease)id, t, dur, ov, per), token));
            initial = null; sourceSnapshot = null; status = "worker 굽기 중";
        }
        private static bool IsWriter(ffxPlusBase f)
        {
            if (f == null || f is ffxMoveFloorPlus) return false;
            string n = f.GetType().Name;
            return n.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0 || n == "ffxLightBridge" || n == "ffxCallMethod" || n == "ffxAddComponent";
        }
        private static void ExcludeWriter(ffxPlusBase f, FloorBakeCore.Tile[] rows)
        {
            if (!IsWriter(f)) return;
            if (f is ffxFloorAppearPlus || f is ffxFloorDisappearPlus) {
                int d = f.floor != null && levelIndices.TryGetValue(f.floor, out int index) ? index : -1;
                if (d >= 0) { rows[d].Reasons.Add("writer:" + f.GetType().Name); return; }
            }
            string reason = "writer-unknown-range:" + f.GetType().Name;
            if (!rows[0].Reasons.Contains(reason)) foreach (var row in rows) row.Reasons.Add(reason);
        }
        private static FloorBakeCore.Event Copy(ffxMoveFloorPlus f, int count)
        {
            int start = Math.Min(f.start, f.end), end = Math.Max(f.start, f.end);
            long step = 1L + f.gapLength;
            if (step <= 0 || start < 0 || end >= count) throw new InvalidOperationException("range/gap invalid; cannot scope safely");
            var targets = new List<int>();
            for (long i = start; i <= end; i += step) {
                targets.Add((int)i);
                // Freeroam landing lists can include off-list objects. Exclude the whole map conservatively.
                if (level[(int)i].freeroamArea != null && !initial[0].Reasons.Contains("freeroam-target-scope"))
                    foreach (var row in initial) row.Reasons.Add("freeroam-target-scope");
            }
            var reasons = new List<string>();
            if (Conditional(f)) reasons.Add("conditional/manual/hit");
            if (f.disableIfMinFx || f.disableIfMaxFx) reasons.Add("visual-setting-condition");
            if (!Enum.IsDefined(typeof(Ease), f.ease) || f.ease == Ease.Unset || f.ease == Ease.INTERNAL_Custom) reasons.Add("unknown-ease");
            var e = new FloorBakeCore.Event { T0 = f.startTime - f.startEffectOffset, Duration = f.duration, Source = f.GetInstanceID(),
                Ease = (int)f.ease, Over = over, Period = period, Targets = targets.ToArray(), Reasons = reasons.ToArray() };
            if (f.ease == Ease.Flash || f.ease == Ease.InFlash || f.ease == Ease.OutFlash || f.ease == Ease.InOutFlash) e.Over = (int)e.Over;
            if (f.duration == 0 && reasons.Count == 0) e.ZeroK = ZeroTween.EaseEnd(f.ease);
            if (f.positionUsed) { e.Values[0] = f.targetPos.x; e.Values[1] = f.targetPos.y; }
            if (f.rotationUsed) e.Values[2] = f.targetRot;
            if (f.scaleUsed) { e.Values[3] = f.targetScaleV2.x; e.Values[4] = f.targetScaleV2.y; }
            if (!float.IsNaN(f.targetScale)) reasons.Add("legacy-Start-scale");
            if (f.opacityUsed) e.Values[5] = f.targetOpacity;
            if ((f.rotationUsed && float.IsNaN(f.targetRot)) || (f.opacityUsed && float.IsNaN(f.targetOpacity))) reasons.Add("scalar-NaN-target");
            e.Reasons = reasons.ToArray(); return e;
        }
        internal static void Tick()
        {
            if (!Enabled) return;
            try {
                if ((collector != null || job != null || table != null) && (scrConductor.instance?.song == null || pitch != scrConductor.instance.song.pitch
                    || scale != DOTween.timeScale || over != DOTween.defaultEaseOvershootOrAmplitude || period != DOTween.defaultEasePeriod))
                    throw new InvalidOperationException("clock/ease defaults changed");
                if ((collector != null || job != null || table != null) && (ADOBase.lm?.listFloors != level || scrVfxPlus.instance?.effects != source || source.Count != sourceCount))
                    throw new InvalidOperationException("level/effect-list changed");
                if (collector != null) {
                    var sw = Stopwatch.StartNew(); bool more = collector.MoveNext();
                    snapshotMs += sw.Elapsed.TotalMilliseconds; maxSliceMs = Math.Max(maxSliceMs, sw.Elapsed.TotalMilliseconds);
                    if (!more) collector = null;
                }
                if (job != null && job.IsCompleted) {
                    var done = job; job = null; table = done.GetAwaiter().GetResult(); heapDelta = GC.GetTotalMemory(false) - heapBefore;
                    status = "준비됨 타일=" + table.Tiles.Length + " 지원=" + table.Supported + " 효과=" + table.Events + " 구간=" + table.Segments;
                    Log(status); Exclusions(table);
                }
            } catch (Exception e) { Fail(e.Message); }
        }
        internal static void ObserveStart(ffxMoveFloorPlus f)
        {
            if (!Enabled || !TileSim.Capturing) return;
            try { TileSim.RecordBakeStart(new Start { Source = f.GetInstanceID(), Frame = Time.frameCount, Song = scrConductor.instance.songposition_minusi }); }
            catch (Exception e) { TileSim.InvalidateBake(e.Message); }
        }
        private static void Exclusions(FloorBakeCore.Table t)
        {
            foreach (var group in t.Tiles.SelectMany(row => row.Reasons).GroupBy(x => x).OrderBy(x => x.Key))
                Log("제외 " + group.Key + " tiles=" + group.Count() + " ratio=" + N(group.Count() / (double)Math.Max(1, t.Tiles.Length)));
        }
        private sealed class Errors
        {
            internal readonly List<double> Values = new List<double>();
            internal readonly Dictionary<int, double> Top = new Dictionary<int, double>();
            internal void Add(int d, double v) { Values.Add(v); if (!Top.TryGetValue(d, out double old) || v > old) Top[d] = v; }
            internal string Report() { Values.Sort(); return "n=" + Values.Count + " max=" + N(Values.Count == 0 ? double.NaN : Values[Values.Count - 1])
                + " p99=" + N(Values.Count == 0 ? double.NaN : Values[(int)Math.Ceiling(Values.Count * .99) - 1])
                + " top8=" + string.Join(",", Top.OrderByDescending(x => x.Value).Take(8).Select(x => x.Key + ":" + N(x.Value))); }
        }
        private static void Verify()
        {
            var tape = TileSim.BakeTape; var t = tape?.BakeTable;
            if (TileSim.Capturing || tape == null || !tape.Valid || t == null || tape.BakeInvalid.Length != 0 || tape.Frames.Count == 0)
                throw new InvalidOperationException("완료된 유효한 같은 세대 테이프 필요 " + tape?.BakeInvalid);
            var stats = Enumerable.Range(0, 6).Select(_ => new Errors()).ToArray(); var pixels = new Errors();
            int skipped = 0, badClock = 0, nonfinite = 0;
            foreach (var frame in tape.Frames) {
                if (!DecoBakeCore.Finite(frame.SongTime) || frame.Pitch != t.Pitch || frame.TweenScale != t.Scale) { badClock++; continue; }
                for (int i = frame.SampleBegin; i < frame.SampleEnd; i++) {
                    var sample = tape.Samples[i]; int d = sample.Tile, c = sample.Key;
                    if (d >= t.Tiles.Length || t.Tiles[d].Id != tape.TileIds[d] || t.Tiles[d].Reasons.Count != 0) { skipped++; continue; }
                    double error = Math.Abs((double)t.Eval(d, c, frame.SongTime) - sample.X);
                    if (!DecoBakeCore.Finite(error)) { nonfinite++; continue; }
                    stats[c].Add(d, error);
                    if (c < 2 && DecoBakeCore.Finite(frame.PixelsPerWorld)) pixels.Add(d, error * frame.PixelsPerWorld);
                }
            }
            string[] names = { "위치X/world", "위치Y/world", "회전/degree", "크기X", "크기Y", "불투명도" };
            for (int c = 0; c < 6; c++) Log(names[c] + " " + stats[c].Report());
            Log("위치px " + pixels.Report() + " skipped=" + skipped + " badClock=" + badClock + " nonfinite=" + nonfinite + " (Rec 출력, tile%4; 화면상 전체 타일 검증 아님)");
            var stamps = new Dictionary<int, Start>(); foreach (var stamp in tape.Starts) if (!stamps.ContainsKey(stamp.Source)) stamps.Add(stamp.Source, stamp);
            var phases = new Dictionary<int, int>(); int missing = 0;
            var scheduled = new Dictionary<int, double>();
            foreach (var track in t.Tracks) foreach (var segment in track) scheduled[segment.Source] = segment.T0;
            foreach (var pair in stamps) {
                if (!scheduled.TryGetValue(pair.Key, out double t0)) { missing++; continue; }
                var expected = tape.Frames.FindIndex(f => f.SongTime >= t0);
                if (expected < 0 || t0 < tape.Frames[0].SongTime - tape.Frames[0].Dt * t.Pitch) { missing++; continue; }
                int delta = pair.Value.Frame - tape.Frames[expected].UnityFrame;
                phases.TryGetValue(delta, out int n); phases[delta] = n + 1;
            }
            Log("시작 위상 frame delta=" + string.Join(",", phases.OrderBy(x => x.Key).Select(x => x.Key + ":" + x.Value)) + " unmeasured=" + missing + "; 제안 위치<=0.01px/시작<=1프레임");
            Exclusions(t);
        }
        private static void Bench()
        {
            if (Hitch.Playing || TileSim.Capturing || table == null) throw new InvalidOperationException("정지/준비 완료 후 bench");
            double sum = 0; var times = new List<double>();
            for (int pass = 0; pass < 33; pass++) {
                var sw = Stopwatch.StartNew();
                for (int d = 0; d < table.Tiles.Length; d++) if (table.Tiles[d].Reasons.Count == 0)
                    for (int c = 0; c < 6; c++) sum += table.Eval(d, c, pass * 17.125);
                if (pass != 0) times.Add(sw.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            Log("실수집 bench tiles=" + table.Tiles.Length + " effects=" + table.Events + " segments=" + table.Segments + " bakeMs=" + N(table.CompileMs)
                + " snapshotMs=" + N(snapshotMs) + " maxSliceMs=" + N(maxSliceMs) + " payloadB=" + table.PayloadBytes + " heapDeltaB=" + heapDelta
                + " evalMedianMs=" + N(times[16]) + " evalP99Ms=" + N(times[31]) + " checksum=" + N(sum));
            Log("그림자이므로 기존 시작 비용 유지. 평가기는 곡 시각 이진탐색, 효과 시작 동작 0. 소유권 전환 성능은 미측정. 합성은 tools/tilebake-fixture --bench.");
            foreach (int count in new[] { 8171, 90000 }) {
                var synthetic = FloorBakeCore.Synthetic(count, (id, t, d, o, p) => ZeroTween.Eval((Ease)id, t, d, o, p));
                sum = 0; times.Clear();
                for (int pass = 0; pass < 33; pass++) {
                    var sw = Stopwatch.StartNew();
                    for (int d = 0; d < count; d++) for (int c = 0; c < 6; c++) sum += synthetic.Eval(d, c, pass * 17.125);
                    if (pass != 0) times.Add(sw.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                Log("합성(실맵 측정 아님) tiles=" + count + " effects=" + synthetic.Events + " refs=" + (count + 4096 * 32)
                    + " segments=" + synthetic.Segments + " bakeMs=" + N(synthetic.CompileMs) + " payloadB=" + synthetic.PayloadBytes
                    + " evalMedianMs=" + N(times[16]) + " evalP99Ms=" + N(times[31]) + " checksum=" + N(sum));
            }
        }
    }
}
#endif
