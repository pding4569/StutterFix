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
using DG.Tweening;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Opt-in observer only. Owns no game state, renderer, tween or decoration.
    internal static class DecoBake
    {
        private const string Owner = "StutterFix.DecoBake.Shadow";
        private const int MaxDecorations = 100000, MaxEvents = 300000, MaxTargetRefs = 4000000;
        private static Harmony patches;
        private static bool enabled;
        private static int generation;
        private static readonly Dictionary<scrDecoration, DecoBakeCore.Decoration> initial = new Dictionary<scrDecoration, DecoBakeCore.Decoration>();
        internal struct StartStamp { internal int Source, Frame; internal double Song; }
        private static IEnumerator collector;
        private static Task<DecoBakeCore.Table> job;
        private static CancellationTokenSource cancellation;
        private static DecoBakeCore.Table table;
        private static bool ready;
        private static scrDecorationManager manager;
        private static List<ffxPlusBase> effectList;
        private static string status = "꺼짐";
        private static double snapshotMs, snapshotMaxMs, workerWallMs;
        private static long heapBefore, heapDelta;
        private static Stopwatch wall;
        private static float frozenPitch, frozenScale;
        private static float frozenOver, frozenPeriod;
        private static double publishedSnapshotMs, publishedMaxMs;
        private static double setupMs, setupFrameMs, setupFrameMaxMs;
        private static int setupFrame = -1;
        private static int publishedSortedCount;
        internal static int Generation => ready ? generation : -1;
        internal static DecoBakeCore.Table CaptureTable => ready ? table : null;
        internal static bool Enabled => enabled;
        private static void Log(string s) { Main.Entry.Logger.Log("[장식굽기/그림자] " + s); }
        private static string N(double x) => x.ToString("G9", CultureInfo.InvariantCulture);
        private static MethodInfo Exact(Type type, string name, bool isStatic, params Type[] parameters)
        {
            var found = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == name && !m.IsGenericMethod && m.IsStatic == isStatic && m.ReturnType == typeof(void)
                    && m.CallingConvention == (isStatic ? CallingConventions.Standard : CallingConventions.Standard | CallingConventions.HasThis)
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException(type.Name + "." + name + " 후보=" + found.Length);
            return found[0];
        }
        internal static void Command(string arg)
        {
            try
            {
                switch ((arg ?? "").Trim().ToLowerInvariant())
                {
                    case "on":
                        if (enabled) { Log(status); return; }
                        if (Hitch.Playing || DecoSim.Capturing) { Log("거부: 곡/캡처 종료 후 on, 그 다음 맵 다시 open"); return; }
                        if (!ZeroTween.CanEase || !FastMove.Installed) { Log("거부: 기존 이징/FastMove 시작 관찰 후크 필요"); return; }
                        // Resolve ALL hooks before installing any. No metadata-token resolution on Mono.
                        var setup = Exact(typeof(scrDecoration), "Setup", false, typeof(LevelEvent), typeof(bool).MakeByRefType(), typeof(bool));
                        var prep = Exact(typeof(scnGame), "PrepVfx", true, typeof(List<scrFloor>), typeof(int), typeof(List<LevelEvent>), typeof(bool));
                        patches = new Harmony(Owner);
                        try
                        {
                            patches.Patch(setup, postfix: new HarmonyMethod(typeof(DecoBake), nameof(SetupPostfix)));
                            patches.Patch(prep, postfix: new HarmonyMethod(typeof(DecoBake), nameof(PrepPostfix)));
                        }
                        catch { patches.UnpatchAll(Owner); patches = null; throw; }
                        enabled = true; status = "켜짐: 새 맵 open 대기"; Log(status); return;
                    case "off": Shutdown(); Log(status); return;
                    case "verify": Verify(); return;
                    case "bench": Bench(); return;
                    default: Log("사용: decobake on (맵 open 전) | off | verify | bench; " + status); return;
                }
            }
            catch (Exception e) { Log("실패: " + e.GetType().Name + " " + e.Message); }
        }
        internal static bool Step(string arg)
        {
            if ((arg ?? "").Trim().ToLowerInvariant() != "ready") { Command(arg); return true; }
            if (!enabled || status.StartsWith("중단:")) throw new InvalidOperationException("decobake ready: " + status);
            return ready;
        }
        private static void CancelBuild()
        {
            generation++; ready = false; collector = null;
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            // Observe late faults even when an old generation is discarded. Worker snapshots contain no Unity references.
            if (job != null) job.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            job = null;
        }
        internal static void Shutdown()
        {
            enabled = false; CancelBuild(); initial.Clear(); manager = null; effectList = null;
            DecoSim.ForgetBake();
            table = null;
            patches?.UnpatchAll(Owner); patches = null; status = "꺼짐";
        }
        internal static void ForgetLevel()
        {
            if (!enabled) return;
            CancelBuild(); initial.Clear(); manager = null; effectList = null; status = "새 맵 스냅샷 대기";
            setupMs = setupFrameMs = setupFrameMaxMs = 0; setupFrame = -1;
        }
        private static void SetupPostfix(scrDecoration __instance)
        {
            if (!enabled) return;
            long begin = Stopwatch.GetTimestamp();
            try
            {
                if (ready || collector != null || job != null) CancelBuild();
                var d = __instance;
                if (manager != d.manager) { initial.Clear(); manager = d.manager; setupMs = setupFrameMs = setupFrameMaxMs = 0; setupFrame = -1; }
                if (initial.Count >= MaxDecorations && !initial.ContainsKey(d)) throw new InvalidOperationException("장식 상한");
                var row = new DecoBakeCore.Decoration { Id = d.GetInstanceID(), OriginalX = d.startPos.x, OriginalY = d.startPos.y };
                var c = d.color; var p = d.parallax == null ? Vector2.zero : d.parallax.multiplier;
                row.Initial = new[] { d.pivotPosVec.x, d.pivotPosVec.y, d.pivotOffsetVec.x, d.pivotOffsetVec.y, d.rotAngle,
                    d.scaleVec.x, d.scaleVec.y, c.r, c.g, c.b, c.a, d.opacity, p.x, p.y, d.parallaxOffset.x, d.parallaxOffset.y };
                if (d is scrVisualDecoration visual && visual.spriteRenderer != null && visual.spriteRenderer.sprite != null)
                {
                    var size = visual.spriteRenderer.sprite.bounds.size;
                    var multiplier = d.trueScaleMultiplier;
                    row.ScaleUnitsX = Math.Abs(size.x * multiplier.x); row.ScaleUnitsY = Math.Abs(size.y * multiplier.y);
                }
                if (!(d is scrVisualDecoration)) row.Reasons.Add("이미지 이외 장식");
                if (d.useHitbox || d.hitboxEvents.Count != 0) row.Reasons.Add("히트박스/충돌");
                if (d.sourceLevelEvent != null && d.sourceLevelEvent.TryGet<MaskingType>("maskingType", out var mask) && mask != MaskingType.None) row.Reasons.Add("초기 마스크");
                if (d.parallax == null) row.Reasons.Add("시차 부품 없음");
                if (d.placementType != DecPlacementType.Global) row.Reasons.Add("초기 배치 Global 이외");
                if (d.lockRotation || d.lockScale || d.stickToFloor || d.followPlanet != null) row.Reasons.Add("추종/고정/바닥 배치");
                if (row.Initial.Any(v => !DecoBakeCore.Finite(v))) row.Reasons.Add("초기값 비유한");
                initial[d] = row;
            }
            catch (Exception e) { Fail("초기값 수집 " + e.Message); }
            finally
            {
                double ms = (Stopwatch.GetTimestamp() - begin) * 1000.0 / Stopwatch.Frequency;
                if (setupFrame != Time.frameCount) { setupFrame = Time.frameCount; setupFrameMs = 0; }
                setupFrameMs += ms; setupMs += ms; setupFrameMaxMs = Math.Max(setupFrameMaxMs, setupFrameMs);
            }
        }
        // Existing FastMove prefix calls this before deciding which implementation runs.
        internal static void ObserveStart(ffxMoveDecorationsPlus fx)
        {
            if (!enabled || !DecoSim.Capturing) return;
            try
            {
                DecoSim.RecordBakeStart(new StartStamp { Source = fx.GetInstanceID(), Frame = Time.frameCount, Song = scrConductor.instance.songposition_minusi });
            }
            catch (Exception e) { Fail("시작 관찰 " + e.Message); }
        }
        private static void PrepPostfix(List<scrFloor> __0)
        {
            if (!enabled) return;
            try
            {
                CancelBuild();
                if (manager == null || manager != scrDecorationManager.instance || initial.Count == 0) throw new InvalidOperationException("open 전 on 필요: Setup 초기값 없음");
                if (ADOBase.isOfficialLevel || ADOBase.levelIsMikoSkip) throw new InvalidOperationException("커스텀 일반 맵만 지원");
                if (ADOBase.controller.visualQuality != VisualQuality.High) throw new InvalidOperationException("High 시각 설정만 지원");
                effectList = scrVfxPlus.instance.effects;
                if (effectList == null || effectList.Count > MaxEvents) throw new InvalidOperationException("효과 목록/상한");
                wall = Stopwatch.StartNew(); snapshotMs = setupMs; snapshotMaxMs = setupFrameMaxMs; heapBefore = GC.GetTotalMemory(false);
                frozenPitch = scrConductor.instance.song.pitch; frozenScale = DOTween.timeScale;
                frozenOver = DOTween.defaultEaseOvershootOrAmplitude; frozenPeriod = DOTween.defaultEasePeriod;
                status = "메인 스냅샷 수집 중";
                collector = Collect(__0, generation);
            }
            catch (Exception e) { Fail("PrepVfx " + e.Message); }
        }
        private static bool Conditional(ffxPlusBase fx) => fx.runManually || fx.runOnHit || (fx.conditionalInfo != null && fx.conditionalInfo.Any(x => x));
        private static string[] Reasons(ffxMoveDecorationsPlus f)
        {
            var r = new List<string>();
            if (Conditional(f)) r.Add("조건/수동/타격");
            if (f.disableIfMinFx || f.disableIfMaxFx) r.Add("시각 효과 설정 조건");
            if (f.imageFilenameUsed || f.originalSizeUsed || f.smoothingUsed || f.cropPivotOffsetUsed) r.Add("이미지/원래크기/크롭/wrap");
            if (f.maskingTypeUsed || f.maskingTargetUsed || f.useMaskingDepthUsed || f.maskingFrontDepthUsed || f.maskingBackDepthUsed) r.Add("마스크 변경");
            if (f.visibleUsed || f.depthUsed) r.Add("가시성/깊이 변경");
            if (f.movementTypeUsed && f.movementType != DecPlacementType.LastPosition && f.movementType != DecPlacementType.Global) r.Add("Global 이외 배치 변경");
            if (!Enum.IsDefined(typeof(Ease), f.ease) || f.ease == Ease.INTERNAL_Custom || f.ease == Ease.Unset) r.Add("미확인/사용자 이징");
            if (!DecoBakeCore.Finite(f.duration) || f.duration < 0) r.Add("길이 비유한/음수");
            return r.ToArray();
        }
        private static DecoBakeCore.Event CopyEvent(ffxMoveDecorationsPlus f)
        {
            var e = new DecoBakeCore.Event { T0 = f.startTime - f.startEffectOffset, Duration = f.duration,
                StartTime = f.startTime, StartEffectOffset = f.startEffectOffset, AngleOffset = f.degreeOffset,
                Source = f.GetInstanceID(), Ease = (int)f.ease, RelativePosition = f.movementType == DecPlacementType.LastPosition, Reasons = Reasons(f) };
            // Same parameters as DecoAnim.EaseParams -> Tween.SetEase, without touching ZeroTween's shared proxy.
            e.Over = frozenOver;
            if (f.ease == Ease.Flash || f.ease == Ease.InFlash || f.ease == Ease.OutFlash || f.ease == Ease.InOutFlash) e.Over = (int)e.Over;
            e.Period = frozenPeriod;
            if (!f.forceDontTweenMovement)
            {
                if (f.positionUsed) { e.Values[0] = f.targetPos.x; e.Values[1] = f.targetPos.y; }
                if (f.pivotUsed) { e.Values[2] = f.targetPivot.x; e.Values[3] = f.targetPivot.y; }
                if (f.rotationUsed) e.Values[4] = f.targetRot;
                DecoBakeCore.CopyScale(e, f.scaleUsed, f.targetScale, f.targetScaleV2.x, f.targetScaleV2.y);
                if (f.parallaxOffsetUsed) { e.Values[14] = f.targetParallaxOffset.x; e.Values[15] = f.targetParallaxOffset.y; }
            }
            if (f.colorUsed) { e.Values[7] = f.targetColor.r; e.Values[8] = f.targetColor.g; e.Values[9] = f.targetColor.b; e.Values[10] = f.targetColor.a; }
            if (f.opacityUsed) e.Values[11] = f.targetOpacity;
            if (f.parallaxUsed) { e.Values[12] = f.targetParallax.x / 100f; e.Values[13] = f.targetParallax.y / 100f; }
            if (e.Values.Any(x => float.IsInfinity(x))) e.Reasons = e.Reasons.Concat(new[] { "목표값 비유한" }).ToArray();
            return e;
        }
        private static bool UnknownWriter(ffxPlusBase fx)
        {
            if (fx == null || fx is ffxMoveDecorationsPlus) return false;
            string name = fx.GetType().Name;
            return name.IndexOf("Decora", StringComparison.OrdinalIgnoreCase) >= 0 || name == "ffxCallMethod" || name == "ffxAddComponent";
        }
        private static IEnumerator Collect(List<scrFloor> floors, int epoch)
        {
            var all = manager.allDecorations;
            int count = all.Count, effects = effectList.Count, refs = 0;
            if (count > MaxDecorations) throw new InvalidOperationException("장식 상한");
            var decs = new DecoBakeCore.Decoration[count];
            var ids = new Dictionary<scrDecoration, int>();
            var ordered = new HashSet<ffxPlusBase>();
            var events = new List<DecoBakeCore.Event>();
            float pitch = frozenPitch, tweenScale = frozenScale;
            bool foreignWriter = typeof(scrDecoration).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal) || m.Name == "Setup")
                .Select(Harmony.GetPatchInfo).Where(info => info != null).Any(info => info.Owners.Any(o => !o.StartsWith("StutterFix", StringComparison.Ordinal)));
            var slice = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var d = all[i];
                if (d == null || ids.ContainsKey(d)) throw new InvalidOperationException("null/중복 장식");
                ids.Add(d, i);
                if (!initial.TryGetValue(d, out var source)) throw new InvalidOperationException("초기 Setup 누락");
                var row = new DecoBakeCore.Decoration { Id = source.Id, Initial = (float[])source.Initial.Clone(), OriginalX = source.OriginalX, OriginalY = source.OriginalY,
                    ScaleUnitsX = source.ScaleUnitsX, ScaleUnitsY = source.ScaleUnitsY };
                foreach (var reason in source.Reasons) row.Reasons.Add(reason);
                if (foreignWriter) row.Reasons.Add("외부 모드 장식 setter 패치");
                if (d.parallax != null && (d.parallax.dontAlterX || d.parallax.dontAlterY || d.parallax.clampToScreen)) row.Reasons.Add("시차 축 고정/화면 고정");
                if (d.hitboxEvents.Count != 0 || d.hasConditionalChange) row.Reasons.Add("히트박스/조건 연결");
                if (d is scrVisualDecoration v && v.spriteRenderer != null && v.spriteRenderer.maskInteraction != SpriteMaskInteraction.None) row.Reasons.Add("마스크 대상");
                decs[i] = row;
                if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
            }
            // Copy the game's final sorted sequence. Repeat-expanded objects remain distinct Source IDs.
            for (int i = 0; i < effects; i++)
            {
                var fx = effectList[i]; ordered.Add(fx);
                if (fx is ffxMoveDecorationsPlus f)
                {
                    var e = CopyEvent(f); var targets = new List<int>();
                    foreach (var d in f.decManager.GetTaggedDecorations((IEnumerable<string>)f.targetTags))
                    {
                        if (!ids.TryGetValue(d, out int id)) throw new InvalidOperationException("대상/초기값 불일치");
                        targets.Add(id); if (++refs > MaxTargetRefs) throw new InvalidOperationException("대상 참조 상한");
                        if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
                    }
                    e.Targets = targets.ToArray(); events.Add(e);
                }
                else if (UnknownWriter(fx))
                    foreach (var d in decs) { d.Reasons.Add("다른 장식 효과/태그 변경 가능"); if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); } }
                if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
            }
            // Conditional/manual effects are omitted by PrepVfx. Include their possible targets in exclusion counts.
            foreach (var floor in floors)
            foreach (var fx in floor.plusEffects)
            {
                if (fx is ffxMoveDecorationsPlus f && !ordered.Contains(fx))
                {
                    foreach (var d in f.decManager.GetTaggedDecorations((IEnumerable<string>)f.targetTags))
                    {
                        if (!ids.TryGetValue(d, out int id)) throw new InvalidOperationException("조건 대상 불일치");
                        decs[id].Reasons.Add("예약 목록 밖/조건/수동/타격");
                        if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
                    }
                }
                else if (UnknownWriter(fx))
                    foreach (var d in decs) { d.Reasons.Add("다른 장식 효과/태그 변경 가능"); if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); } }
                if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); }
            }
            if (epoch != generation || manager.allDecorations.Count != count || effectList.Count != effects) throw new InvalidOperationException("수집 중 맵 변경");
            cancellation = new CancellationTokenSource(); var token = cancellation.Token;
            var copied = events.ToArray();
            job = Task.Run(() => DecoBakeCore.Compile(decs, copied, pitch, tweenScale, EvaluateEase, token));
            status = "작업 스레드 굽기 중";
        }
        private static float EvaluateEase(int id, float t, float dur, float over, float period) => ZeroTween.Eval((Ease)id, t, dur, over, period);
        internal static void Tick()
        {
            if (!enabled) return;
            try
            {
                if (ready && (manager != scrDecorationManager.instance || effectList != scrVfxPlus.instance?.effects)) { ForgetLevel(); return; }
                if ((ready || collector != null || job != null) && (scrConductor.instance?.song == null
                    || scrConductor.instance.song.pitch != frozenPitch || DOTween.timeScale != frozenScale
                    || DOTween.defaultEaseOvershootOrAmplitude != frozenOver || DOTween.defaultEasePeriod != frozenPeriod))
                { Fail("수집 후 pitch/DOTween 배율/이징 기본값 변경: 고정 계약에서 제외"); return; }
                if (collector != null)
                {
                    var sw = Stopwatch.StartNew(); bool more = collector.MoveNext();
                    double ms = sw.Elapsed.TotalMilliseconds; snapshotMs += ms;
                    snapshotMaxMs = Math.Max(snapshotMaxMs, ms + (setupFrame == Time.frameCount ? setupFrameMs : 0));
                    if (!more) collector = null;
                }
                if (job != null && job.IsCompleted)
                {
                    var completed = job; job = null; table = completed.GetAwaiter().GetResult();
                    ready = true;
                    heapDelta = GC.GetTotalMemory(false) - heapBefore; workerWallMs = wall.Elapsed.TotalMilliseconds;
                    publishedSnapshotMs = snapshotMs; publishedMaxMs = snapshotMaxMs;
                    publishedSortedCount = effectList.Count;
                    status = "준비됨 (읽기/계산만)";
                    Log(status + " 장식=" + table.Decorations.Length + " 지원=" + table.Supported + " 이벤트=" + table.Events + " 구간=" + table.Segments);
                }
            }
            catch (Exception e) { Fail(e.GetType().Name + " " + e.Message); }
        }
        private static void Fail(string why) { DecoSim.InvalidateBake(why); CancelBuild(); status = "중단: " + why; Log(status); }
        private static void Bench()
        {
            if (Hitch.Playing || DecoSim.Capturing) { Log("거부: 곡/캡처 종료 후 bench"); return; }
            if (table == null) { Log(status); return; }
            double checksum = 0; var timings = new List<double>();
            // All supported decorations and all 16 channels, at varying song times; no Unity writes.
            for (int pass = 0; pass < 33; pass++)
            {
                var sw = Stopwatch.StartNew();
                for (int d = 0; d < table.Decorations.Length; d++) if (table.Decorations[d].Reasons.Count == 0)
                    for (int c = 0; c < DecoBakeCore.Channels; c++) checksum += table.Eval(d, c, pass * 2.125);
                if (pass != 0) timings.Add(sw.Elapsed.TotalMilliseconds);
            }
            timings.Sort();
            Log("bench 실수집 장식=" + table.Decorations.Length + "/맵참조28828 이동효과인스턴스=" + table.Events
                + " 전체예약효과=" + publishedSortedCount + " 원본맵이벤트참조95211(집계단계다름) 구간=" + table.Segments
                + " worker굽기ms=" + N(table.CompileMs) + " 수집CPUms=" + N(publishedSnapshotMs) + " 최대메인sliceMs=" + N(publishedMaxMs)
                + " Prep후완료wallMs=" + N(workerWallMs) + " 표payloadMiB=" + N(table.PayloadBytes / 1048576.0)
                + " 프로세스heap변화MiB=" + N(heapDelta / 1048576.0) + " eval중앙ms=" + N(timings[16]) + " evalp99ms=" + N(timings[31]) + " checksum=" + N(checksum));
            Log("메모리 payload는 객체 header/임시 list/스냅샷 제외; heap변화는 다른 게임 할당 포함. Arche 실수집 수와 지원율을 함께 판단.");
        }
        private sealed class Errors
        {
            internal readonly List<double> Values = new List<double>();
            internal readonly List<double> ReplacementValues = new List<double>();
            internal readonly Dictionary<int, double> Top = new Dictionary<int, double>();
            internal double Max, ReplacementMax;
            internal int ReplacementN;
            internal void Add(double value, bool replacement, int decoration = -1) {
                Values.Add(value); Max = Math.Max(Max, value);
                if (decoration >= 0 && (!Top.TryGetValue(decoration, out double old) || value > old)) Top[decoration] = value;
                if (replacement) { ReplacementN++; ReplacementMax = Math.Max(ReplacementMax, value); ReplacementValues.Add(value); }
            }
            internal double P99() { Values.Sort(); return Values.Count == 0 ? double.NaN : Values[(int)Math.Ceiling(Values.Count * .99) - 1]; }
            internal double ReplacementP99() { ReplacementValues.Sort(); return ReplacementValues.Count == 0 ? double.NaN : ReplacementValues[(int)Math.Ceiling(ReplacementValues.Count * .99) - 1]; }
        }
        private static readonly string[] Names = { "위치X", "위치Y", "피벗X", "피벗Y", "회전", "크기X", "크기Y", "색R", "색G", "색B", "색A", "불투명도", "시차X", "시차Y", "시차오프셋X", "시차오프셋Y" };
        private static int Channel(int key) { switch (key) { case 1: return 0; case 2: return 1; case 3: return 2; case 4: return 3; case 5: return 4; case 7: return 5; case 8: return 6; case 9: return 7; case 10: return 11; case 11: return 12; case 12: return 14; case 13: return 15; default: return -1; } }
        private static void Verify()
        {
            if (DecoSim.Capturing) { Log("거부: 테이프 캡처 완료 후 verify"); return; }
            var tape = DecoSim.BakeTape;
            var table = tape?.BakeTable;
            if (table == null || tape == null || !tape.Valid || tape.Frames.Count == 0 || tape.BakeGeneration < 0)
            { Log("거부: 같은 굽기 세대의 유효한 완료 테이프 필요; " + status); return; }
            if (tape.BakeInvalidReason.Length != 0) { Log("거부: 캡처 중 굽기 계약 무효화: " + tape.BakeInvalidReason); return; }
            var byId = new Dictionary<int, int>(); for (int i = 0; i < table.Decorations.Length; i++) byId.Add(table.Decorations[i].Id, i);
            var stats = Enumerable.Range(0, DecoBakeCore.Channels).Select(_ => new Errors()).ToArray();
            var scaleRelative = new[] { new Errors(), new Errors() }; var scalePixels = new[] { new Errors(), new Errors() };
            var scaleZero = new int[2]; var scaleNoPixels = new int[2];
            var top = new Dictionary<int, double>(); var pixels = new Errors(); int skipped = 0, badClock = 0, nonfinite = 0;
            foreach (var frame in tape.Frames)
            {
                if (!DecoBakeCore.Finite(frame.SongTime) || frame.Pitch != table.Pitch || frame.TweenScale != table.TweenScale)
                { badClock++; continue; }
                for (int i = frame.SampleBegin; i < frame.SampleEnd; i++)
                {
                    var s = tape.Samples[i]; int c = Channel(s.Key);
                    if (c < 0 || !byId.TryGetValue(tape.DecorationIds[s.Decoration], out int d) || table.Decorations[d].Reasons.Count != 0) { skipped++; continue; }
                    int components = s.Key == 9 ? 4 : s.Key == 11 ? 2 : 1;
                    for (int v = 0; v < components; v++)
                    {
                        float actual = v == 0 ? s.X : v == 1 ? s.Y : v == 2 ? s.Z : s.W;
                        if (s.Key == 8) actual = s.Y;
                        double error = Math.Abs((double)table.Eval(d, c + v, frame.SongTime) - actual);
                        if (!DecoBakeCore.Finite(error)) { nonfinite++; continue; }
                        stats[c + v].Add(error, table.Replaced[d], d);
                        if (c == DecoBakeCore.ScaleX || c == DecoBakeCore.ScaleY)
                        {
                            int axis = c - DecoBakeCore.ScaleX;
                            if (actual == 0) scaleZero[axis]++;
                            else scaleRelative[axis].Add(error / Math.Abs((double)actual), table.Replaced[d], d);
                            float units = axis == 0 ? table.Decorations[d].ScaleUnitsX : table.Decorations[d].ScaleUnitsY;
                            if (DecoBakeCore.Finite(units) && units > 0 && DecoBakeCore.Finite(frame.PixelsPerWorld) && frame.PixelsPerWorld > 0)
                                scalePixels[axis].Add(error * units * frame.PixelsPerWorld, table.Replaced[d], d);
                            else scaleNoPixels[axis]++;
                        }
                        if (!top.TryGetValue(d, out double old) || error > old) top[d] = error;
                        if (c < 2 && DecoBakeCore.Finite(frame.PixelsPerWorld) && frame.PixelsPerWorld > 0) pixels.Add(error * frame.PixelsPerWorld, table.Replaced[d]);
                    }
                }
            }
            Log("verify 표본=DecoSim 인덱스%4==0 Rec.Step 출력; 완료 이벤트의 중간 Kill 값은 최종 프레임과 혼합하지 않음. 제외표본=" + skipped + " 시계변경제외프레임=" + badClock + " 비유한=" + nonfinite);
            for (int c = 0; c < stats.Length; c++)
                Log(Names[c] + " n=" + stats[c].Values.Count + " 최대절대=" + (stats[c].Values.Count == 0 ? "NA" : N(stats[c].Max)) + " p99=" + N(stats[c].P99())
                    + " 교체장식n=" + stats[c].ReplacementN + " 교체최대=" + (stats[c].ReplacementN == 0 ? "NA" : N(stats[c].ReplacementMax)) + " 교체p99=" + N(stats[c].ReplacementP99())
                    + " 상위장식8=" + string.Join(";", stats[c].Top.OrderByDescending(x => x.Value).Take(8).Select(x => table.Decorations[x.Key].Id + ":" + N(x.Value))));
            for (int axis = 0; axis < 2; axis++)
                Log("크기" + (axis == 0 ? "X" : "Y") + " 상대오차(|차|/|실제|) n=" + scaleRelative[axis].Values.Count
                    + " 최대=" + (scaleRelative[axis].Values.Count == 0 ? "NA" : N(scaleRelative[axis].Max)) + " p99=" + N(scaleRelative[axis].P99())
                    + " 실제0으로상대NA=" + scaleZero[axis] + " 교체n=" + scaleRelative[axis].ReplacementN + " 교체최대=" + (scaleRelative[axis].ReplacementN == 0 ? "NA" : N(scaleRelative[axis].ReplacementMax))
                    + " 교체p99=" + N(scaleRelative[axis].ReplacementP99())
                    + " 명목축길이px n=" + scalePixels[axis].Values.Count + " 최대=" + (scalePixels[axis].Values.Count == 0 ? "NA" : N(scalePixels[axis].Max))
                    + " p99=" + N(scalePixels[axis].P99()) + " 환산불가=" + scaleNoPixels[axis]
                    + "; 초기 sprite.bounds*trueScaleMultiplier*직교배율, 지연스프라이트/크기0은NA, 회전/시차/피벗/타일링 합성 렌더 오차 아님");
            Log("위치 축별 화면환산(1440 높이/직교 카메라) n=" + pixels.Values.Count + " 최대px=" + (pixels.Values.Count == 0 ? "NA" : N(pixels.Max)) + " p99px=" + N(pixels.P99())
                + " 제안기준<=0.01px; pivotPosVec 진단이며 시차/피벗/회전 합성 후 렌더 위치 검증은 별도");
            Log("상위장식8(속성 원단위 최대; 단위 혼합 순위) " + string.Join("; ", top.OrderByDescending(x => x.Value).Take(8).Select(x => table.Decorations[x.Key].Id + ":" + N(x.Value))));
            var reasons = new Dictionary<string, int>();
            foreach (var d in table.Decorations) foreach (string reason in d.Reasons) { reasons.TryGetValue(reason, out int n); reasons[reason] = n + 1; }
            Log("지원=" + table.Supported + "/" + table.Decorations.Length + " 비율=" + N(100.0 * table.Supported / Math.Max(1, table.Decorations.Length)) + "% (사유별 중복 가능)");
            foreach (var p in reasons.OrderByDescending(x => x.Value)) Log("미지원 " + p.Key + " 장식=" + p.Value + " 비율=" + N(100.0 * p.Value / Math.Max(1, table.Decorations.Length)) + "%");
            Phase(tape, table);
        }
        private static void Phase(DecoSim.Tape tape, DecoBakeCore.Table table)
        {
            var scheduled = new Dictionary<int, double>();
            foreach (var track in table.Tracks) foreach (var s in track) scheduled[s.Source] = s.T0;
            var frames = tape.Frames; var gaps = new List<int>(); int missing = 0;
            for (int i = 1; i < frames.Count; i++) if (frames[i].SongTime < frames[i - 1].SongTime)
            { Log("효과시작위상 NA: 테이프에 되감기/비단조 곡 시각 있음 (값 평가는 무상태)"); return; }
            foreach (var stamp in tape.BakeStarts)
            {
                if (stamp.Frame < frames[0].UnityFrame || stamp.Frame > frames[frames.Count - 1].UnityFrame || !scheduled.TryGetValue(stamp.Source, out double t0)) continue;
                // First captured frame crossing T0; starts before the tape are explicitly censored.
                int crossing = -1;
                for (int i = 1; i < frames.Count; i++) if (frames[i - 1].SongTime < t0 && frames[i].SongTime >= t0) { crossing = frames[i].UnityFrame; break; }
                if (crossing < 0) { missing++; continue; }
                gaps.Add(stamp.Frame - crossing);
            }
            gaps.Sort();
            Log("효과시작위상(실제 StartEffect UnityFrame - 테이프 T0 통과 UnityFrame) n=" + gaps.Count
                + " 최소=" + (gaps.Count == 0 ? "NA" : gaps[0].ToString()) + " 최대=" + (gaps.Count == 0 ? "NA" : gaps[gaps.Count - 1].ToString())
                + " 경계/되감기등미대응=" + missing + " 프레임차별빈도=" + string.Join(",", gaps.GroupBy(x => x).Select(g => g.Key + ":" + g.Count()))
                + "; 단조 재생 구간만 해석, FastMove 기존 후크 관찰");
        }
    }
}
#endif
