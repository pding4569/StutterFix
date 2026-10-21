#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        private static BakeLive adopt;
        private static bool resumeAdopt;
        private static int adoptGeneration, adoptResync, adoptFrame;
        private static readonly Dictionary<scrDecoration, int> owned = new Dictionary<scrDecoration, int>();
        private static readonly HashSet<int> adoptSources = new HashSet<int>();
        private static readonly Dictionary<int,int> sourceBinding = new Dictionary<int,int>();
        private static readonly List<int> canaries = new List<int>();
        private static double evalMs, flushMs, checkMs, triggerMs, canaryMs, canaryWorst;
        private static long evalChannels, anchorChannels, fallbackChannels, endedChannels, dirtyCount, longChannels;
        private static long activeDecorations, activeSources;
        private static long listPrepareTicks, listResetTicks, flushVisits;
        private static readonly long[] channelCounts = new long[16];
        private static double guardMs, bindMs, constructMs, completeMs, initialMs, prepareMs;
        private static long canaryChecks, canaryMismatch, canaryHideMismatch, canaryFrames;
        private static long canaryAnchoredSlots, canaryTbSlots;
        private static double canaryPositionMax, canaryScaleMax, canaryAlphaMax;
        private static int canaryFrame = -1;
        private static int[] writeMasks;
        private static float[] writeValues;
        private static readonly List<int> dirty = new List<int>();
        private static readonly Action<int, float> stageWrite = StageWrite;
        private static HashSet<int> BindAdoptSources()
        {
            var result = new HashSet<int>(); var ids = new Dictionary<scrDecoration, int>();
            for (int d = 0; d < baselineObjects.Length; d++) ids.Add(baselineObjects[d], d);
            var remaining = new List<DecoBakeCore.Event>();
            foreach (var fx in effectList) if (fx is ffxMoveDecorationsPlus f)
            {
                var current = CopyEvent(f); var targets = new List<int>();
                foreach (var d in f.decManager.GetTaggedDecorations((IEnumerable<string>)f.targetTags))
                {
                    if (!ids.TryGetValue(d, out int target)) throw new InvalidOperationException("adopt 대상 변경");
                    targets.Add(target);
                }
                current.Targets = targets.ToArray();
                remaining.Add(current);
                result.Add(f.GetInstanceID());
            }
            DecoBakeCore.ValidateAdoptRemainder(table.InputEvents, remaining.ToArray(),
                scrConductor.instance == null ? double.NaN : scrConductor.instance.songposition_minusi);
            sourceBinding.Clear(); int index = 0;
            foreach (var current in remaining)
            {
                while (!DecoBakeCore.SameAdoptEvent(table.InputEvents[index],current)) index++;
                sourceBinding.Add(current.Source, table.InputEvents[index++].Source);
            }
            return result;
        }
        private static long adoptFrames, skippedRecs, adoptChecks;
        private static double adoptMs, adoptWorst, resyncWorst;
        private static double[] adoptSamples;
        internal static bool Adopting => adopt != null && adopt.Enabled;
        internal static void AdoptFrame() { if (Adopting) DriveAdopt(); }
        internal static void BeginAdoptMeasure()
        {
            adoptFrames = skippedRecs = adoptChecks = 0; adoptMs = adoptWorst = resyncWorst = 0;
            evalMs = flushMs = checkMs = triggerMs = canaryMs = canaryWorst = 0;
            evalChannels = anchorChannels = fallbackChannels = endedChannels = dirtyCount = longChannels = 0;
            activeDecorations = activeSources = 0;
            listPrepareTicks = listResetTicks = flushVisits = 0;
            if (adopt != null) adopt.Replacements = 0;
            Array.Clear(channelCounts,0,16);
            canaryChecks = canaryMismatch = canaryHideMismatch = canaryFrames = 0;
            canaryAnchoredSlots = canaryTbSlots = 0;
            canaryPositionMax = canaryScaleMax = canaryAlphaMax = 0;
            DecoAnim.BeginBakeMeasure();
        }
        internal static bool ForeignSetter()
        {
            return new[] { typeof(scrDecoration), typeof(scrVisualDecoration), typeof(scrParallax), typeof(ffxMoveDecorationsPlus) }
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal) || m.Name == "Setup" || m.Name == "UpdatePosition" || m.Name == "StartEffect")
                .Select(Harmony.GetPatchInfo).Where(p => p != null)
                .Any(p => p.Owners.Any(o => !o.StartsWith("StutterFix", StringComparison.Ordinal)));
        }
        private static void AdoptCommand(string arg)
        {
            if (arg == "off") { StopAdopt("명령"); return; }
            if (arg == "state") { AdoptReport(); return; }
            if (arg != "on") throw new ArgumentException("adopt on|off|state");
            if (Adopting) { AdoptReport(); return; }
            if (Hitch.Playing && (ADOBase.controller == null || !ADOBase.controller.paused))
                throw new InvalidOperationException("adopt 준비는 정지/일시정지에서만 허용: catch-up 종료 후 게임 일시정지, adopt on, 재개");
            if (!ready || table == null || table.Supported == 0 || baselineObjects == null) throw new InvalidOperationException("같은 세대 ready/O>0 필요");
            long prepareStart = Stopwatch.GetTimestamp();
            if (ForeignSetter() || Compat.QuartzOptimizer) throw new InvalidOperationException("Compat: 외부 장식 setter/Quartz optimizer");
            if (FrameGen.Active && Main.Config.FrameGenDecoHints)
                throw new InvalidOperationException("FrameGen 장식 힌트는 Rec 목록을 요구하므로 채택과 동시 사용 불가");
            if (!FastMove.Enabled || !FastMove.Installed || !DecoAnim.Active || !DecoAnim.CanPivotParallax
                || !InstantMove.Enabled || !InstantMove.Patched || !ZeroTween.Enabled || !ZeroTween.Patched || EffectBudget.InCatchUp || !TightLate.CaptureReady)
                throw new InvalidOperationException("FastMove/DecoAnim/InstantMove/ZeroTween 경로 필요, catch-up 종료 후 켜기");
            ValidateBaseline();
            guardMs = Elapsed(prepareStart);
            long prepare = Stopwatch.GetTimestamp();
            var sources = BindAdoptSources(); bindMs = Elapsed(prepare); prepare = Stopwatch.GetTimestamp();
            adopt = new BakeLive(table); adoptGeneration = generation;
            // Fixed hash of baseline index (not Unity instance ID). Keep original Rec ownership.
            canaries.Clear();
            canaries.AddRange(Enumerable.Range(0,adopt.Owned.Length).Where(d => adopt.Owned[d]
                && Enumerable.Range(0,16).Any(c => table.Tracks[d*16+c].Length != 0))
                .OrderBy(d => unchecked((uint)d*2654435761u)).Take(Math.Min(96,Math.Max(0,table.Supported-1))));
            foreach (int d in canaries) adopt.Owned[d] = false;
            owned.Clear(); adoptSources.Clear();
            for (int d = 0; d < adopt.Owned.Length; d++) if (adopt.Owned[d]) owned.Add(baselineObjects[d], d);
            foreach (int source in sources) adoptSources.Add(source);
            writeMasks = new int[adopt.Owned.Length]; writeValues = new float[adopt.Last.Length]; dirty.Clear();
            if (adoptSamples == null) adoptSamples = new double[65536];
            BeginAdoptMeasure();
            constructMs = Elapsed(prepare);
            adoptFrame = -1; adoptResync = -1;
            try
            {
                prepare = Stopwatch.GetTimestamp();
                foreach (var d in owned.Keys) FastMove.CompleteBakeTweens(d);
                completeMs = Elapsed(prepare); prepare = Stopwatch.GetTimestamp();
                AdvanceClock(); DriveAdopt(true); initialMs = Elapsed(prepare); prepareMs = Elapsed(prepareStart); AdoptReport();
            }
            catch (Exception e) { AdoptFailure(e); }
        }
        internal static bool SkipAdopt(ffxMoveDecorationsPlus fx, scrDecoration d, int keys)
        {
            if (!Adopting || !owned.ContainsKey(d)) return false;
            if (adoptGeneration != generation || !ready || !adoptSources.Contains(fx.GetInstanceID()))
            { StopAdopt("미확인 효과/세대 변경"); return false; }
            skippedRecs += keys; return true;
        }
        private static void StageWrite(int slot, float value)
        {
            int d = slot / DecoBakeCore.Channels, c = slot % DecoBakeCore.Channels;
            if (writeMasks[d] == 0) dirty.Add(d);
            writeMasks[d] |= 1 << c; writeValues[slot] = value;
        }
        private static double Elapsed(long start) => (Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        internal static void AdoptStart(ffxMoveDecorationsPlus fx)
        {
            if (!Adopting) return;
            long start = Stopwatch.GetTimestamp();
            try
            {
                if (!sourceBinding.TryGetValue(fx.GetInstanceID(),out int source))
                    throw new InvalidOperationException("adopt 미확인 시작 소스");
                adopt.Start(source,Time.frameCount,stageWrite); FlushWrites();
            }
            catch (Exception e) { AdoptFailure(e); }
            finally { triggerMs += Elapsed(start); }
        }
        private static void FlushWrites(bool recovery = false)
        {
            Exception first = null;
            try
            {
                dirtyCount += dirty.Count;
                foreach (int d in dirty)
                    try
                    {
                        bool sample = (++flushVisits & 63) == 0; long listStart = sample ? Stopwatch.GetTimestamp() : 0;
                        if (baselineObjects[d] == null) throw new InvalidOperationException("adopt 장식 소멸");
                        // Color is one setter even when just one component is active.
                        for (int c = 7; c <= 10; c++) if ((writeMasks[d] & (1<<c)) == 0)
                            writeValues[d*16+c] = adopt.Last[d*16+c];
                        if (Precheck.Active != 0) InvisibleSkip.TouchDeco(baselineObjects[d], "굽기 채택 쓰기");
                        if (sample) listPrepareTicks += (Stopwatch.GetTimestamp()-listStart)*64;
                        DecoAnim.BakeWrite(baselineObjects[d], writeValues, d*16, writeMasks[d]);
                    }
                    catch (Exception e) { if (!recovery) throw; if (first == null) first = e; }
            }
            finally { long reset = Stopwatch.GetTimestamp(); foreach (int d in dirty) writeMasks[d] = 0; dirty.Clear(); listResetTicks += Stopwatch.GetTimestamp()-reset; }
            if (first != null) throw first;
        }
        private static void DriveAdopt(bool force = false)
        {
            if (!Adopting || (!force && adoptFrame == Time.frameCount)) return;
            adoptFrame = Time.frameCount;
            long begin = Stopwatch.GetTimestamp(); bool sync = force || adoptResync != clock.Resyncs;
            try
            {
                if (adoptGeneration != generation || !ready || !DecoAnim.Active || !FastMove.Enabled)
                    throw new InvalidOperationException("adopt 기반 경로/세대 변경");
                if (FrameGen.Active && Main.Config.FrameGenDecoHints)
                    throw new InvalidOperationException("FrameGen 장식 힌트 활성화");
                if ((adoptFrames % 120) == 0 && ForeignSetter()) throw new InvalidOperationException("외부 setter 패치 변경");
                checkMs += Elapsed(begin); long stage = Stopwatch.GetTimestamp();
                // Match DecoAnim.UpdatePrefix literally, including its tiny-delta test. Do not
                // infer dt=0 from controller.paused: the original driver does not do that.
                float dt = (DG.Tweening.DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime)*DG.Tweening.DOTween.timeScale;
                adopt.Step(clock.Time,sync,stageWrite,dt,Time.frameCount); evalMs += Elapsed(stage);
                evalChannels += adopt.Evaluated; anchorChannels += adopt.Anchored; fallbackChannels += adopt.Fallback;
                endedChannels += adopt.Ended; longChannels += adopt.LongChannels;
                activeDecorations += adopt.DecorationsEvaluated; activeSources += adopt.SourcesEvaluated;
                for (int c = 0; c < 16; c++) channelCounts[c] += adopt.ByChannel[c];
                stage = Stopwatch.GetTimestamp(); FlushWrites(); flushMs += Elapsed(stage); adoptResync = clock.Resyncs;
            }
            catch (Exception e) { AdoptFailure(e); }
            finally
            {
                double ms = (Stopwatch.GetTimestamp()-begin)*1000.0/Stopwatch.Frequency;
                adoptSamples[adoptFrames % adoptSamples.Length] = ms; adoptFrames++; adoptMs += ms;
                adoptWorst = Math.Max(adoptWorst, ms); if (sync) resyncWorst = Math.Max(resyncWorst, ms);
            }
        }
        internal static void AdoptFailure(Exception e)
        {
            Resilience.RecordFeatureFailure("DecoBakeAdopt", e.Message);
            StopAdopt("평가/쓰기 실패: " + e.GetType().Name + " " + e.Message);
        }
        internal static void StopAdopt(string reason)
        {
            resumeAdopt = false;
            if (adopt == null) return;
            try { adopt.Stop(clock.Time, stageWrite); FlushWrites(true); }
            catch (Exception e) { Log("adopt 복귀 setter 실패: " + e.Message); Resilience.RecordFeatureFailure("DecoBakeAdopt", "복귀: " + e.Message); }
            finally { adopt = null; owned.Clear(); adoptSources.Clear(); dirty.Clear(); Log("adopt 해제: " + reason); AdoptReport(); }
        }
        private static void AdoptReport()
        {
            int n = adoptSamples == null ? 0 : (int)Math.Min(adoptFrames, adoptSamples.Length); var values = new double[n];
            if (n != 0) Array.Copy(adoptSamples, values, n); Array.Sort(values);
            Log("[장식 이동/굽기채택] on=" + Adopting + " O=" + owned.Count + " skippedRec=" + skippedRecs
                + " frames=" + adoptFrames + " 평균ms=" + N(adoptFrames == 0 ? 0 : adoptMs/adoptFrames)
                + " p99ms=" + N(n == 0 ? 0 : values[(int)Math.Ceiling(n*.99)-1]) + " 최악ms=" + N(adoptWorst)
                + " resync최악ms=" + N(resyncWorst) + " checks=" + adoptChecks + " (p99 최근65536프레임, FuseLate 별도)");
            double frames = Math.Max(1,adoptFrames);
            Log("[굽기채택 분해] 평가ms="+N(evalMs/frames)+" 쓰기목록ms="+N(flushMs/frames)
                +" 검사ms="+N(checkMs/frames)+" 트리거누적ms="+N(triggerMs)
                +" 목록준비정리추정ms="+N((listPrepareTicks+listResetTicks)*1000.0/Stopwatch.Frequency/frames)
                +" 채널/프레임="+N(evalChannels/frames)+" anchored="+N(anchorChannels/frames)+" tb="+N(fallbackChannels/frames)
                +" 종료="+endedChannels+" 장식쓰기/프레임="+N(dirtyCount/frames)+" 길이10초이상/프레임="+N(longChannels/frames)
                +" 평가장식/프레임="+N(activeDecorations/frames)+" anchored소스/프레임="+N(activeSources/frames)
                +" 교체채널="+(adopt?.Replacements ?? 0)
                +" 채널별합="+string.Join(",",channelCounts));
            Log("[굽기채택 준비] 전체ms="+N(prepareMs)+" 검사ms="+N(guardMs)+" 바인딩ms="+N(bindMs)+" 구축ms="+N(constructMs)+" Kill완료ms="+N(completeMs)+" 초기평가쓰기ms="+N(initialMs)+"; 일시정지 전용");
            DecoAnim.BakeReport(frames);
            Log("[굽기채택 카나리아] 표본="+canaries.Count+" 비교="+canaryChecks+" 불일치="+canaryMismatch+" 숨김불일치="+canaryHideMismatch
                +" 위치최대="+N(canaryPositionMax)+" 크기최대="+N(canaryScaleMax)+" 알파최대="+N(canaryAlphaMax)
                +" 관찰시작채널="+canaryAnchoredSlots+" tb="+canaryTbSlots
                +" 평균ms="+N(canaryMs/Math.Max(1,canaryFrames))+" 최악ms="+N(canaryWorst)+"; 실제 예산 <0.05ms 미검증");
        }
        internal static void CanaryFrame()
        {
            if (!Adopting || adoptFrame != Time.frameCount || canaryFrame == Time.frameCount) return;
            canaryFrame = Time.frameCount; long start = Stopwatch.GetTimestamp();
            try
            {
                foreach (int d in canaries)
                {
                    var obj = baselineObjects[d]; int o = d*16; var v = adopt.Last;
                    for (int c = 0; c < 16; c++)
                        if (table.Tracks[o+c].Length != 0) { if (adopt.AnchoredSlot(o+c)) canaryAnchoredSlots++; else canaryTbSlots++; }
                    if (obj == null) throw new InvalidOperationException("canary destroyed");
                    double p = Math.Max(Math.Abs(obj.pivotPosVec.x-v[o]), Math.Abs(obj.pivotPosVec.y-v[o+1]));
                    p = Math.Max(p,Math.Max(Math.Abs(obj.pivotOffsetVec.x-v[o+2]),Math.Abs(obj.pivotOffsetVec.y-v[o+3])));
                    p = Math.Max(p,Math.Max(Math.Abs(obj.parallaxOffset.x-v[o+14]),Math.Abs(obj.parallaxOffset.y-v[o+15])));
                    double s = Math.Max(Math.Abs(obj.scaleVec.x-v[o+5]), Math.Abs(obj.scaleVec.y-v[o+6]));
                    double alpha = Math.Abs(obj.color.a*obj.opacity-v[o+10]*v[o+11]);
                    canaryChecks++; adoptChecks++; canaryPositionMax = Math.Max(canaryPositionMax,p); canaryScaleMax = Math.Max(canaryScaleMax,s);
                    canaryAlphaMax = Math.Max(canaryAlphaMax,alpha);
                    if (!DecoBakeCore.Finite(p+s+alpha) || p > .0001 || s > .0001 || alpha > .00001) canaryMismatch++;
                    var visual = obj as scrVisualDecoration;
                    if (visual != null && InvisibleSkip.BakeHideMismatch(visual,v[o+10]*v[o+11]*1f)) canaryHideMismatch++;
                }
            }
            catch (Exception e) { AdoptFailure(e); }
            finally { double ms = Elapsed(start); canaryMs += ms; canaryFrames++; canaryWorst = Math.Max(canaryWorst,ms); }
        }
    }
}
#endif
