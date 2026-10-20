#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    // Read-only live observer. The replay below retains no Unity object references.
    internal static class DecoSim
    {
        internal static bool Capturing;
        private const long Limit = 512L * 1024 * 1024;
        private const int MaxDecorations = 100000, MaxSlots = 200000, MaxFrames = 12000;
        private static Tape tape;
        internal static Tape BakeTape => tape;
        internal static void ForgetBake()
        {
            if (tape == null) return;
            tape.BakeTable = null; tape.BakeGeneration = -1; tape.BakeStarts.Clear();
        }
        internal static void InvalidateBake(string reason)
        {
            if (Capturing && tape.BakeTable != null) tape.BakeInvalidReason = reason;
        }
        internal static void Shutdown()
        {
            Capturing = false; inFrame = false; tape = null;
            decorations.Clear(); tokens.Clear(); slots.Clear(); present.Clear(); removed.Clear();
        }
        private static readonly Dictionary<scrDecoration, int> decorations = new Dictionary<scrDecoration, int>();
        private static readonly Dictionary<DecoAnim.Rec, int> tokens = new Dictionary<DecoAnim.Rec, int>();
        private static readonly Dictionary<int, int> slots = new Dictionary<int, int>();
        private static readonly HashSet<DecoAnim.Rec> present = new HashSet<DecoAnim.Rec>();
        private static readonly List<DecoAnim.Rec> removed = new List<DecoAnim.Rec>();
        private static int serial, frameEvent, frameSample, stepCount;
        private static bool inFrame;
        private static long deadline;
        private static long initialErrors;
        private static void Log(string s) { Main.Entry.Logger.Log("[장식시뮬] " + s); }

        internal struct State
        {
            internal int Slot, Decoration, Key, Token;
            internal Ease Ease;
            internal float Duration, Position, Over, Period;
            internal bool Started, Running;
            internal float FS, FE, FC;
            internal Vector2 VS, VE, VC;
            internal Color CS, CE, CC;
        }
        internal enum Kind : byte { Create, Startup, Complete, Cancel, Remove }
        internal struct Event { internal Kind Kind; internal State State; internal bool HasSample; internal Sample Sample; }
        internal struct Output
        {
            internal Vector2 Position, Scale;
            internal float Rotation, Opacity;
            internal Color Color;
        }
        internal struct Sample
        {
            internal int Slot, Token, Decoration, Key;
            internal float Position, X, Y, Z, W;
            internal bool Running;
        }
        internal struct Frame
        {
            internal float Dt;
            internal double SongTime;
            internal float Pitch, TweenScale, PixelsPerWorld;
            internal int UnityFrame;
            internal int EventBegin, EventEnd, SampleBegin, SampleEnd, Evaluated;
        }
        internal sealed class Tape
        {
            internal int BakeGeneration = -1;
            internal DecoBakeCore.Table BakeTable;
            internal string BakeInvalidReason = "";
            internal readonly List<DecoBake.StartStamp> BakeStarts = new List<DecoBake.StartStamp>();
            internal int[] DecorationIds;
            internal int DecorationCount, SlotCount;
            internal long Reserved;
            internal bool Valid = true;
            internal string Reason = "";
            internal Output[] Baseline;
            internal readonly List<State> Initial = new List<State>();
            internal readonly List<Event> Events = new List<Event>();
            internal readonly List<Sample> Samples = new List<Sample>();
            internal readonly List<Frame> Frames = new List<Frame>();
        }
        private static bool Reserve(long n)
        {
            if (tape.Reserved + n > Limit) { Stop("메모리 상한 512MiB", false); return false; }
            tape.Reserved += n; return true;
        }
        private static void Stop(string reason, bool valid)
        {
            Capturing = false; inFrame = false;
            tape.Valid = valid; tape.Reason = reason;
            Log("캡처 " + reason + " frames=" + tape.Frames.Count + " slots=" + tape.SlotCount
                + " events=" + tape.Events.Count + " samples=" + tape.Samples.Count
                + " 보수적예약MiB=" + (tape.Reserved / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)
                + " valid=" + valid);
            decorations.Clear(); tokens.Clear(); slots.Clear(); present.Clear(); removed.Clear();
        }
        internal static void Command(string arg)
        {
            var a = (arg ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = a.Length == 0 ? "" : a[0].ToLowerInvariant();
            if (cmd == "capture")
            {
                float seconds;
                if (a.Length != 2 || !float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
                    || float.IsNaN(seconds) || seconds <= 0 || seconds > 120)
                { Log("사용: decosim capture <초: 0초 초과~120초> | verify | bench | selftest"); return; }
                if (Capturing) { Log("이미 캡처 중"); return; }
                if (!Hitch.Playing || !DecoAnim.Enabled || !DecoAnim.Installed || !ZeroTween.CanEase)
                { Log("거부: 곡 재생 중이며 DecoAnim/이징 사용 가능 상태여야 함"); return; }
                if (!DecoAnim.SimDefaultPath()) { Log("거부: 연구 경로/ablation을 끄고 기본 경로에서 캡처"); return; }
                var all = scrDecorationManager.instance?.allDecorations;
                if (all == null || all.Count > MaxDecorations) { Log("거부: 장식 목록 없음 또는 100000개 상한"); return; }
                tape = new Tape { DecorationCount = all.Count, Baseline = new Output[all.Count],
                    BakeGeneration = DecoBake.Generation, BakeTable = DecoBake.CaptureTable, DecorationIds = new int[all.Count] };
                // Includes dictionary capacity, references and replay/output headroom.
                tape.Reserved = 1024 * 1024 + all.Count * 128L;
                serial = 0; inFrame = false; stepCount = 0;
                initialErrors = DecoAnim.Errors;
                decorations.Clear(); tokens.Clear(); slots.Clear(); present.Clear(); removed.Clear();
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && !decorations.ContainsKey(all[i])) { decorations.Add(all[i], i); tape.Baseline[i] = DecoAnim.SimOutput(all[i]); tape.DecorationIds[i] = all[i].GetInstanceID(); }
                deadline = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
                Capturing = true;
                foreach (var r in DecoAnim.SimRecords())
                {
                    if (!r.Running) continue;
                    State s;
                    if (!Identify(r, true, out s)) return;
                    if (!Reserve(512)) return;
                    tape.Initial.Add(s);
                }
                Log("캡처 시작 seconds=" + seconds.ToString("R", CultureInfo.InvariantCulture) + " initial=" + tape.Initial.Count
                    + " 장식=" + all.Count + " 표본=장식인덱스%4==0 (Rec 출력/진행 상태)");
                return;
            }
            if (cmd == "selftest")
            {
                if (Hitch.Playing || Capturing) { Log("거부: 곡/캡처 종료 후 selftest"); return; }
                Log(SelfTest()); return;
            }
            if (cmd != "verify" && cmd != "bench") { Log("사용: decosim capture <초> | verify | bench | selftest"); return; }
            if (Capturing) { Log("거부: 캡처 완료 후 실행"); return; }
            if (tape == null || !tape.Valid || tape.Frames.Count == 0)
            { Log("거부: 유효한 완료 테이프 없음 " + (tape?.Reason ?? "")); return; }
            if (cmd == "verify") Verify(tape);
            else
            {
                if (Hitch.Playing) { Log("거부: 곡 종료/stop 후 bench (측정 부하 분리)"); return; }
                Bench(tape);
            }
        }
        private static bool Identify(DecoAnim.Rec r, bool fresh, out State s)
        {
            s = default;
            int d, slot, token;
            if (!decorations.TryGetValue(r.D, out d) || r.Key < 1 || r.Key > 13)
            { Stop("장식 인덱스/Key 해석 불가", false); return false; }
            int key = d * 14 + r.Key;
            if (!slots.TryGetValue(key, out slot))
            {
                if (slots.Count >= MaxSlots || !Reserve(1024)) { if (Capturing) Stop("슬롯 200000개 상한", false); return false; }
                slot = slots.Count; slots.Add(key, slot); tape.SlotCount = slots.Count;
            }
            if (fresh || !tokens.TryGetValue(r, out token))
            {
                if (!tokens.ContainsKey(r) && !Reserve(128)) return false;
                token = ++serial; tokens[r] = token;
            }
            else token = tokens[r];
            s = new State { Slot = slot, Decoration = d, Key = r.Key, Token = token, Ease = r.E,
                Duration = r.Dur, Position = r.Pos, Over = r.Over, Period = r.Period,
                Started = r.Started, Running = r.Running, FS = r.FStart, FE = r.FEnd, FC = r.FChange,
                VS = r.VStart, VE = r.VEnd, VC = r.VChange, CS = r.CStart, CE = r.CEnd, CC = r.CChange };
            return true;
        }
        private static void AddEvent(Kind kind, DecoAnim.Rec r, bool fresh = false)
        {
            if (inFrame && stepCount != 0 && kind != Kind.Startup)
            { Stop("갱신 중 재진입 lifecycle 이벤트: " + kind, false); return; }
            State s;
            if (!Identify(r, fresh, out s) || !Reserve(512)) return;
            bool sample = kind == Kind.Complete && (s.Decoration & 3) == 0;
            tape.Events.Add(new Event { Kind = kind, State = s, HasSample = sample, Sample = sample ? MakeSample(r, s.Slot, s.Token, s.Decoration) : default });
        }
        internal static void Created(DecoAnim.Rec r) { AddEvent(Kind.Create, r, true); }
        internal static void RecordBakeStart(DecoBake.StartStamp stamp)
        {
            if (!Capturing || tape.BakeTable == null) return;
            if (tape.BakeStarts.Count >= 300000 || !Reserve(64)) { if (Capturing) Stop("굽기 시작 기록 상한", false); return; }
            tape.BakeStarts.Add(stamp);
        }
        internal static void Started(DecoAnim.Rec r) { AddEvent(Kind.Startup, r); }
        internal static void Completed(DecoAnim.Rec r) { AddEvent(Kind.Complete, r); }
        internal static void Cancelled(DecoAnim.Rec r) { AddEvent(Kind.Cancel, r); }
        internal static void BeginFrame(List<DecoAnim.Rec> records, bool research)
        {
            if (research || !DecoAnim.SimDefaultPath()) { Stop("연구 경로 변경", false); return; }
            if (!Hitch.Playing) { Stop("곡 재생 종료", tape.Frames.Count != 0); return; }
            // Detect records disappearing even if a future owner bypasses Kill.
            present.Clear();
            foreach (var r in records) present.Add(r);
            removed.Clear();
            foreach (var item in tokens) if (!present.Contains(item.Key)) removed.Add(item.Key);
            foreach (var r in removed)
            {
                AddEvent(Kind.Remove, r); if (!Capturing) return;
                tokens.Remove(r);
            }
            foreach (var r in records)
                if (r.Running && !tokens.ContainsKey(r)) { AddEvent(Kind.Create, r, true); if (!Capturing) return; }
            // Events between updates belong to this next frame, in original order.
            frameEvent = tape.Frames.Count == 0 ? 0 : tape.Frames[tape.Frames.Count - 1].EventEnd;
            frameSample = tape.Samples.Count; stepCount = 0; inFrame = true;
        }
        internal static void Stepped(DecoAnim.Rec r)
        {
            stepCount++;
            int d, token, slot;
            if (!decorations.TryGetValue(r.D, out d) || (d & 3) != 0) return;
            if (!tokens.TryGetValue(r, out token) || !slots.TryGetValue(d * 14 + r.Key, out slot))
            { Stop("표본에 시작 이벤트 없음", false); return; }
            if (!Reserve(96)) return;
            tape.Samples.Add(MakeSample(r, slot, token, d));
        }
        private static Sample MakeSample(DecoAnim.Rec r, int slot, int token, int d)
        {
            var s = new Sample { Slot = slot, Token = token, Decoration = d, Key = r.Key, Position = r.Pos, Running = r.Running };
            if (r.Key == 9) { s.X = r.CLast.r; s.Y = r.CLast.g; s.Z = r.CLast.b; s.W = r.CLast.a; }
            else if (r.Key == 7 || r.Key == 8 || r.Key == 11) { s.X = r.VLast.x; s.Y = r.VLast.y; }
            else s.X = r.FLast;
            return s;
        }
        internal static void EndFrame(List<DecoAnim.Rec> records, float td)
        {
            if (DecoAnim.Errors != initialErrors) { Stop("DecoAnim 예외 발생", false); return; }
            if (!inFrame) { Stop("프레임 경계 누락", false); return; }
            if (tape.Frames.Count >= MaxFrames || !Reserve(128)) { if (Capturing) Stop("프레임 12000개 상한", false); return; }
            var conductor = scrConductor.instance;
            var camera = scrCamera.instance == null ? null : scrCamera.instance.camobj;
            tape.Frames.Add(new Frame { Dt = td, SongTime = conductor == null ? double.NaN : conductor.songposition_minusi,
                Pitch = conductor?.song == null ? float.NaN : conductor.song.pitch, TweenScale = DOTween.timeScale,
                PixelsPerWorld = camera != null && camera.orthographic ? 1440f / (2f * camera.orthographicSize) : float.NaN,
                UnityFrame = Time.frameCount, EventBegin = frameEvent, EventEnd = tape.Events.Count,
                SampleBegin = frameSample, SampleEnd = tape.Samples.Count, Evaluated = stepCount });
            inFrame = false;
            if (Stopwatch.GetTimestamp() >= deadline) Stop("완료", true);
        }

        // Structure of arrays: slot metadata/inputs and decoration-indexed outputs.
        // No Unity reads, Rec access, setter or allocation on the evaluation path.
        internal sealed class DecoBatchEval
        {
            internal readonly int[] Decoration, Key, Token;
            internal readonly Ease[] Ease;
            internal readonly bool[] Running, Started;
            internal readonly float[] Duration, Position, Over, Period;
            internal readonly float[][] Start, End, Change, Last;
            internal readonly float[] PosX, PosY, Rot, ScaleX, ScaleY, Red, Green, Blue, Alpha, Opacity;
            private readonly int workers;
            private readonly Action<int> worker;
            private float dt;
            internal long WorkerTicks;
            internal DecoBatchEval(int count, int decorations, int partitions = 1)
            {
                Decoration = new int[count]; Key = new int[count]; Token = new int[count]; Ease = new Ease[count];
                Running = new bool[count]; Started = new bool[count];
                Duration = new float[count]; Position = new float[count]; Over = new float[count]; Period = new float[count];
                Start = Channels(count); End = Channels(count); Change = Channels(count); Last = Channels(count);
                PosX = new float[decorations]; PosY = new float[decorations]; Rot = new float[decorations];
                ScaleX = new float[decorations]; ScaleY = new float[decorations]; Red = new float[decorations];
                Green = new float[decorations]; Blue = new float[decorations]; Alpha = new float[decorations]; Opacity = new float[decorations];
                workers = Math.Max(1, Math.Min(partitions, Math.Max(1, count)));
                worker = part => {
                    long t = Stopwatch.GetTimestamp();
                    EvaluateRange(part * (long)count / workers, (part + 1L) * count / workers, dt);
                    System.Threading.Interlocked.Add(ref WorkerTicks, Stopwatch.GetTimestamp() - t);
                };
            }
            private static float[][] Channels(int count) { return new[] { new float[count], new float[count], new float[count], new float[count] }; }
            internal void Reset(Tape t)
            {
                Array.Clear(Running, 0, Running.Length); Array.Clear(Started, 0, Started.Length); Array.Clear(Token, 0, Token.Length);
                foreach (var channel in Last) Array.Clear(channel, 0, channel.Length);
                foreach (var output in new[] { PosX, PosY, Rot, ScaleX, ScaleY, Red, Green, Blue, Alpha, Opacity }) Array.Clear(output, 0, output.Length);
                if (t.Baseline != null)
                    for (int d = 0; d < t.Baseline.Length; d++)
                    {
                        var b = t.Baseline[d]; PosX[d] = b.Position.x; PosY[d] = b.Position.y; Rot[d] = b.Rotation;
                        ScaleX[d] = b.Scale.x; ScaleY[d] = b.Scale.y; Opacity[d] = b.Opacity;
                        Red[d] = b.Color.r; Green[d] = b.Color.g; Blue[d] = b.Color.b; Alpha[d] = b.Color.a;
                    }
                foreach (var s in t.Initial) Load(s);
            }
            private void Inputs(State s)
            {
                int i = s.Slot;
                if (s.Key == 9)
                {
                    Start[0][i] = s.CS.r; Start[1][i] = s.CS.g; Start[2][i] = s.CS.b; Start[3][i] = s.CS.a;
                    End[0][i] = s.CE.r; End[1][i] = s.CE.g; End[2][i] = s.CE.b; End[3][i] = s.CE.a;
                    Change[0][i] = s.CC.r; Change[1][i] = s.CC.g; Change[2][i] = s.CC.b; Change[3][i] = s.CC.a;
                }
                else if (s.Key == 7 || s.Key == 8 || s.Key == 11)
                {
                    Start[0][i] = s.VS.x; Start[1][i] = s.VS.y; End[0][i] = s.VE.x; End[1][i] = s.VE.y;
                    Change[0][i] = s.VC.x; Change[1][i] = s.VC.y;
                }
                else { Start[0][i] = s.FS; End[0][i] = s.FE; Change[0][i] = s.FC; }
            }
            private void Load(State s)
            {
                int i = s.Slot; Decoration[i] = s.Decoration; Key[i] = s.Key; Token[i] = s.Token;
                Ease[i] = s.Ease; Duration[i] = s.Duration; Position[i] = s.Position; Over[i] = s.Over; Period[i] = s.Period;
                Running[i] = s.Running; Started[i] = s.Started; Inputs(s);
            }
            internal void Process(Event ev)
            {
                var s = ev.State; int i = s.Slot;
                if (ev.Kind == Kind.Create) { Load(s); return; }
                if (Token[i] != s.Token) return; // an old removed record must not cancel its replacement
                if (ev.Kind == Kind.Startup) { Inputs(s); Started[i] = true; }
                else if (ev.Kind == Kind.Complete)
                {
                    Inputs(s); Started[i] = true; Position[i] = Duration[i];
                    Value(i, ZeroTween.Eval(Ease[i], Duration[i], Duration[i], Over[i], Period[i]), true);
                    Running[i] = false;
                }
                else Running[i] = false;
            }
            internal void Evaluate(float delta, bool parallel)
            {
                WorkerTicks = 0;
                if (delta < 1E-06f && delta > -1E-06f) return;
                if (parallel) { dt = delta; Parallel.For(0, workers, worker); }
                else EvaluateRange(0, Running.Length, delta);
            }
            private void EvaluateRange(long begin, long end, float delta)
            {
                for (int i = (int)begin; i < end; i++)
                {
                    if (!Running[i]) continue;
                    if (!Started[i]) throw new InvalidOperationException("캡처 Startup 누락 slot=" + i);
                    float to = Position[i] + delta;
                    bool done = to >= Duration[i]; if (done) to = Duration[i];
                    Position[i] = to;
                    float e = ZeroTween.Eval(Ease[i], to, Duration[i], Over[i], Period[i]);
                    Value(i, e, done); if (done) Running[i] = false;
                }
            }
            private void Value(int i, float e, bool done)
            {
                int k = Key[i], d = Decoration[i];
                if (k == 9)
                {
                    // Same expression shape as DecoAnim.ApplyE (do not split multiply/add).
                    for (int c = 0; c < 4; c++) { float v = Start[c][i]; v += Change[c][i] * e; Last[c][i] = v; }
                    Red[d] = done ? End[0][i] : Last[0][i]; Green[d] = done ? End[1][i] : Last[1][i];
                    Blue[d] = done ? End[2][i] : Last[2][i]; Alpha[d] = done ? End[3][i] : Last[3][i];
                }
                else if (k == 7 || k == 8 || k == 11)
                {
                    if (k == 7) { Last[0][i] = Start[0][i] + Change[0][i] * e; ScaleX[d] = Last[0][i]; }
                    else if (k == 8) { Last[1][i] = Start[1][i] + Change[1][i] * e; ScaleY[d] = Last[1][i]; }
                    else
                    {
                        float x = Start[0][i]; x += Change[0][i] * e; Last[0][i] = x;
                        float y = Start[1][i]; y += Change[1][i] * e; Last[1][i] = y;
                    }
                }
                else
                {
                    Last[0][i] = Start[0][i] + Change[0][i] * e;
                    float v = done ? End[0][i] : Last[0][i];
                    switch (k) { case 1: PosX[d] = v; break; case 2: PosY[d] = v; break; case 5: Rot[d] = v; break; case 10: Opacity[d] = v; break; }
                }
            }
        }
        private static int Bits(float f) { return BitConverter.SingleToInt32Bits(f); }
        private static string F(float f) { return f.ToString("R", CultureInfo.InvariantCulture) + "(0x" + Bits(f).ToString("X8") + ")"; }
        private static bool Compare(DecoBatchEval eval, Sample s, out string detail)
        {
            int i = s.Slot; detail = "";
            if (eval.Token[i] != s.Token || Bits(eval.Position[i]) != Bits(s.Position) || eval.Running[i] != s.Running)
                detail = "token/pos/running expected=" + s.Token + "/" + F(s.Position) + "/" + s.Running
                    + " actual=" + eval.Token[i] + "/" + F(eval.Position[i]) + "/" + eval.Running[i];
            float expected = s.Key == 8 ? s.Y : s.X, actual = eval.Last[s.Key == 8 ? 1 : 0][i];
            if (Bits(expected) != Bits(actual)) detail += " ch0 expected=" + F(expected) + " actual=" + F(actual);
            if (s.Key == 9 || s.Key == 11)
            {
                if (Bits(s.Y) != Bits(eval.Last[1][i])) detail += " ch1 expected=" + F(s.Y) + " actual=" + F(eval.Last[1][i]);
                if (s.Key == 9)
                {
                    if (Bits(s.Z) != Bits(eval.Last[2][i])) detail += " ch2 expected=" + F(s.Z) + " actual=" + F(eval.Last[2][i]);
                    if (Bits(s.W) != Bits(eval.Last[3][i])) detail += " ch3 expected=" + F(s.W) + " actual=" + F(eval.Last[3][i]);
                }
            }
            return detail.Length == 0;
        }
        private static void Verify(Tape t)
        {
            int partitions = Partitions(t);
            foreach (bool parallel in new[] { false, true })
            {
                var eval = new DecoBatchEval(t.SlotCount, t.DecorationCount, partitions); eval.Reset(t);
                long[] counts = new long[14], diffs = new long[14]; int shown = 0;
                for (int f = 0; f < t.Frames.Count; f++)
                {
                    var frame = t.Frames[f];
                    for (int j = frame.EventBegin; j < frame.EventEnd; j++)
                    {
                        var ev = t.Events[j]; eval.Process(ev);
                        if (ev.HasSample) CheckSample(eval, ev.Sample, counts, diffs, ref shown, f, parallel);
                    }
                    eval.Evaluate(frame.Dt, parallel);
                    for (int j = frame.SampleBegin; j < frame.SampleEnd; j++)
                    {
                        CheckSample(eval, t.Samples[j], counts, diffs, ref shown, f, parallel);
                    }
                }
                long total = 0, mismatch = 0;
                for (int k = 1; k < 14; k++) { total += counts[k]; mismatch += diffs[k]; Log("verify mode=" + (parallel ? "parallel" : "single") + " key=" + k + " comparisons=" + counts[k] + " mismatch=" + diffs[k]); }
                Log("verify " + (parallel ? "parallel" : "single") + " frames=" + t.Frames.Count + " comparisons=" + total
                    + " mismatch=" + mismatch + (total == 0 ? " 판정불가: 표본 없음" : " (Rec 출력 범위; Transform/렌더 검증 제외)"));
            }
        }
        private static void CheckSample(DecoBatchEval eval, Sample s, long[] counts, long[] diffs, ref int shown, int frame, bool parallel)
        {
            counts[s.Key]++; string detail;
            if (Compare(eval, s, out detail)) return;
            diffs[s.Key]++;
            if (shown++ < 8) Log("불일치 mode=" + (parallel ? "parallel" : "single") + " frame=" + frame
                + " decoration=" + s.Decoration + " key=" + s.Key + " " + detail);
        }
        private static void Bench(Tape t)
        {
            int passes = Math.Max(1, (300 + t.Frames.Count - 1) / t.Frames.Count);
            int n = checked(passes * t.Frames.Count), partitions = Partitions(t);
            long evaluated = 0; foreach (var f in t.Frames) evaluated += f.Evaluated;
            foreach (bool parallel in new[] { false, true })
            {
                var eval = new DecoBatchEval(t.SlotCount, t.DecorationCount, partitions);
                Replay(eval, t, parallel); // warm JIT/ease/thread pool, excluded
                double[] evaluation = new double[n], events = new double[n], starts = new double[n], totals = new double[n], workers = new double[n];
                int ix = 0; long creates = 0;
                for (int p = 0; p < passes; p++)
                {
                    eval.Reset(t); // reset/allocation excluded from per-frame cost
                    foreach (var f in t.Frames)
                    {
                        long begin = Stopwatch.GetTimestamp(), startTicks = 0;
                        for (int j = f.EventBegin; j < f.EventEnd; j++)
                        {
                            var ev = t.Events[j];
                            bool start = ev.Kind == Kind.Create || ev.Kind == Kind.Startup;
                            long a = start ? Stopwatch.GetTimestamp() : 0;
                            eval.Process(ev);
                            if (start) { startTicks += Stopwatch.GetTimestamp() - a; if (ev.Kind == Kind.Create) creates++; }
                        }
                        long middle = Stopwatch.GetTimestamp(); eval.Evaluate(f.Dt, parallel); long end = Stopwatch.GetTimestamp();
                        evaluation[ix] = Ms(end - middle); events[ix] = Ms(middle - begin); starts[ix] = Ms(startTicks);
                        totals[ix] = Ms(end - begin); workers[ix] = Ms(eval.WorkerTicks); ix++;
                    }
                }
                Log("bench mode=" + (parallel ? "Parallel.For N=" + partitions : "single") + " frames=" + n
                    + " slots=" + t.SlotCount + " evaluations/frame=" + (evaluated / (double)t.Frames.Count).ToString("F2", CultureInfo.InvariantCulture)
                    + " starts/frame=" + (creates / (double)n).ToString("F2", CultureInfo.InvariantCulture)
                    + " eval" + Stats(evaluation) + " events" + Stats(events) + " starts(subset)" + Stats(starts) + " total" + Stats(totals)
                    + (parallel ? " worker범위경과합" + Stats(workers) + " main-wait=eval 동기 호출 전체(분할+실행+join)" : "")
                    + " checksum=" + Checksum(eval).ToString("X8") + " (≤1ms 판정은 total; p99도 확인; 시작별 타이머 비용 포함)");
            }
        }
        private static int Partitions(Tape t) { return Math.Max(1, Math.Min(Math.Min(Environment.ProcessorCount, 8), t.SlotCount)); }
        private static void Replay(DecoBatchEval eval, Tape t, bool parallel)
        {
            eval.Reset(t);
            foreach (var f in t.Frames) { for (int j = f.EventBegin; j < f.EventEnd; j++) eval.Process(t.Events[j]); eval.Evaluate(f.Dt, parallel); }
        }
        private static uint Checksum(DecoBatchEval e)
        {
            uint hash = 2166136261;
            foreach (var a in new[] { e.PosX, e.PosY, e.Rot, e.ScaleX, e.ScaleY, e.Red, e.Green, e.Blue, e.Alpha, e.Opacity })
                foreach (float v in a) hash = unchecked((hash ^ (uint)Bits(v)) * 16777619);
            return hash;
        }
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string Stats(double[] a)
        {
            double sum = 0; foreach (double v in a) sum += v;
            Array.Sort(a);
            return " mean/p99=" + (sum / a.Length).ToString("F4", CultureInfo.InvariantCulture) + "/"
                + a[Math.Max(0, (int)Math.Ceiling(a.Length * .99) - 1)].ToString("F4", CultureInfo.InvariantCulture) + "ms";
        }
        internal static string SelfTest()
        {
            // Synthetic lifecycle oracle: does not claim real-game Mono equivalence.
            var t = new Tape { SlotCount = 2, DecorationCount = 1 };
            var a = new State { Slot = 0, Decoration = 0, Key = 1, Token = 1, Ease = DG.Tweening.Ease.Linear,
                Duration = 1, FS = 2, FE = 10, FC = 8, Started = true, Running = true };
            var b = new State { Slot = 1, Decoration = 0, Key = 8, Token = 2, Ease = DG.Tweening.Ease.Linear,
                Duration = 1, VE = new Vector2(0, 8), Running = true };
            t.Initial.Add(a); t.Initial.Add(b);
            int checks = 0;
            foreach (bool parallel in new[] { false, true })
            {
                var e = new DecoBatchEval(2, 1, 2); e.Reset(t);
                e.Evaluate(0, parallel); Require(e.Position[0] == 0, "zero dt"); checks++;
                b.Started = true; b.VS = new Vector2(3, 4); b.VC = new Vector2(-3, 4);
                e.Process(new Event { Kind = Kind.Startup, State = b });
                e.Evaluate(.25f, parallel); Require(e.PosX[0] == 4 && e.ScaleY[0] == 5, "advance/lazy scale"); checks++;
                e.Process(new Event { Kind = Kind.Complete, State = a }); Require(e.PosX[0] == 10 && !e.Running[0], "complete"); checks++;
                a.Token = 3; a.FS = 10; a.FE = 14; a.FC = 4;
                e.Process(new Event { Kind = Kind.Create, State = a });
                var stale = a; stale.Token = 1;
                e.Process(new Event { Kind = Kind.Remove, State = stale });
                e.Evaluate(.5f, parallel); Require(e.PosX[0] == 12, "restart/stale remove"); checks++;
                e.Process(new Event { Kind = Kind.Cancel, State = a });
                e.Evaluate(1, parallel); Require(e.PosX[0] == 12 && !e.Running[1], "cancel/completion"); checks++;
                a.Token = 1; a.FS = 2; a.FE = 10; a.FC = 8;
            }
            return "selftest passed=" + checks + " (synthetic; 실제 테이프/Mono 확인 별도)";
        }
        private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException("decosim selftest: " + label); }
    }
    internal static partial class DecoAnim
    {
        internal static List<Rec> SimRecords() { return recs; }
        internal static DecoSim.Output SimOutput(scrDecoration d)
        {
            return new DecoSim.Output { Position = pivotPosRef(d), Scale = scaleRef(d), Rotation = rotRef(d),
                Opacity = opaRef(d), Color = colRef(d) };
        }
        internal static bool SimDefaultPath()
        {
            return !batchOn && !DecoEngine.Enabled && !GroupEnabled && !NativeMotion.Enabled
#if AUTOTEST
                && AblateMask == 0
#endif
                ;
        }
    }
}
#endif
