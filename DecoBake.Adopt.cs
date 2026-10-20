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
        private static DecoBakeCore.AdoptState adopt;
        private static bool resumeAdopt;
        private static int adoptGeneration, adoptResync, adoptFrame;
        private static readonly Dictionary<scrDecoration, int> owned = new Dictionary<scrDecoration, int>();
        private static readonly HashSet<int> adoptSources = new HashSet<int>();
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
            if (!ready || table == null || table.Supported == 0 || baselineObjects == null) throw new InvalidOperationException("같은 세대 ready/O>0 필요");
            if (ForeignSetter() || Compat.QuartzOptimizer) throw new InvalidOperationException("Compat: 외부 장식 setter/Quartz optimizer");
            if (FrameGen.Active && Main.Config.FrameGenDecoHints)
                throw new InvalidOperationException("FrameGen 장식 힌트는 Rec 목록을 요구하므로 채택과 동시 사용 불가");
            if (!FastMove.Enabled || !FastMove.Installed || !DecoAnim.Active || !DecoAnim.CanPivotParallax
                || !InstantMove.Enabled || !InstantMove.Patched || !ZeroTween.Enabled || !ZeroTween.Patched || EffectBudget.InCatchUp)
                throw new InvalidOperationException("FastMove/DecoAnim/InstantMove/ZeroTween 경로 필요, catch-up 종료 후 켜기");
            ValidateBaseline();
            var sources = BindAdoptSources();
            adopt = new DecoBakeCore.AdoptState(table); adoptGeneration = generation;
            owned.Clear(); adoptSources.Clear();
            for (int d = 0; d < adopt.Owned.Length; d++) if (adopt.Owned[d]) owned.Add(baselineObjects[d], d);
            foreach (int source in sources) adoptSources.Add(source);
            writeMasks = new int[adopt.Owned.Length]; writeValues = new float[adopt.Last.Length]; dirty.Clear();
            if (adoptSamples == null) adoptSamples = new double[65536];
            BeginAdoptMeasure();
            adoptFrame = -1; adoptResync = -1;
            try
            {
                foreach (var d in owned.Keys) FastMove.CompleteBakeTweens(d);
                AdvanceClock(); DriveAdopt(true); AdoptReport();
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
        private static void FlushWrites(bool recovery = false)
        {
            Exception first = null;
            try
            {
                foreach (int d in dirty)
                    try
                    {
                        if (baselineObjects[d] == null) throw new InvalidOperationException("adopt 장식 소멸");
                        // Color is one setter even when just one component is active.
                        for (int c = 7; c <= 10; c++) if ((writeMasks[d] & (1<<c)) == 0)
                            writeValues[d*16+c] = adopt.Last[d*16+c];
                        if (Precheck.Active != 0) InvisibleSkip.TouchDeco(baselineObjects[d], "굽기 채택 쓰기");
                        DecoAnim.BakeWrite(baselineObjects[d], writeValues, d*16, writeMasks[d]);
                    }
                    catch (Exception e) { if (!recovery) throw; if (first == null) first = e; }
            }
            finally { foreach (int d in dirty) writeMasks[d] = 0; dirty.Clear(); }
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
                adopt.Step(clock.Time, sync, stageWrite); FlushWrites(); adoptResync = clock.Resyncs;
                // No original Rec exists: compare stateless table with last committed values only.
                if ((adoptFrames % 120) == 0 && owned.Count != 0)
                {
                    int start = (int)((adoptFrames / 120) % adopt.Owned.Length);
                    for (int n = 0, checkedCount = 0; n < adopt.Owned.Length && checkedCount < 4; n++)
                    {
                        int d = (start+n) % adopt.Owned.Length; if (!adopt.Owned[d]) continue; checkedCount++;
                        for (int c = 0; c < 16; c++)
                        { adoptChecks++; if (table.Eval(d,c,clock.Time) != adopt.Last[d*16+c]) throw new InvalidOperationException("adopt 표/마지막 쓰기 불일치"); }
                    }
                }
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
        }
    }
}
#endif
