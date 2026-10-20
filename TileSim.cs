#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    // Read-only live observer. The replay below retains no Unity object references.
    internal static class TileSim
    {
        internal static bool Capturing;
        private const long Limit = 512L * 1024 * 1024;
        private const int MaxTiles = 100000, MaxSlots = 200000, MaxFrames = 12000;
        private static Tape tape;
        internal static Tape BakeTape => Capturing ? null : tape;
        internal static void InvalidateBake(string reason) { if (tape?.BakeTable != null) tape.BakeInvalid = reason; }
        internal static void ForgetBake() {
            if (tape == null) return;
            tape.BakeTable = null; tape.BakeGeneration = -1; tape.Starts.Clear(); tape.BakeInvalid = "tilebake off/unload";
        }
        internal static void RecordBakeStart(FloorBake.Start stamp) {
            if (Capturing && tape.BakeTable != null && tape.Starts.Count < 300000 && Reserve(32)) tape.Starts.Add(stamp);
        }
        private static readonly Dictionary<scrFloor, int> floors = new Dictionary<scrFloor, int>();
        private static readonly Dictionary<FloorAnim.Rec, int> tokens = new Dictionary<FloorAnim.Rec, int>();
        private static readonly Dictionary<int, int> slots = new Dictionary<int, int>();
        private static readonly Dictionary<int, FloorAnim.Rec> owners = new Dictionary<int, FloorAnim.Rec>();
        private static int serial, frameEvent, frameSample, stepCount;
        private static bool inFrame;
        private static long deadline;
        private static long initialErrors;
        private static long zero0, real0, fallback0, created0, completed0, killed0, dropped0;
        private static readonly Dictionary<string, long> reasons = new Dictionary<string, long>();
        private static void Log(string s) { Main.Entry.Logger.Log("[타일시뮬] " + s); }
        private static void Reject(string reason)
        {
            long n; reasons.TryGetValue(reason, out n); reasons[reason] = n + 1;
            Log("거부 reason=" + reason + " count=" + (n + 1));
        }
        internal static void Shutdown()
        {
            if (Capturing) Stop("unload", false);
            tape = null; floors.Clear(); tokens.Clear(); slots.Clear(); owners.Clear();
        }

        internal struct State
        {
            internal int Slot, Tile, Key, Token;
            internal Ease Ease;
            internal float Duration, Position, Over, Period;
            internal bool Started, Running;
            internal float FS, FE, FC;
            internal Vector3 VS, VE, VC;
        }
        internal enum Kind : byte { Create, Startup, Complete, Cancel, Remove }
        internal struct Event { internal Kind Kind; internal State State; internal bool HasSample; internal Sample Sample; }
        internal struct Sample
        {
            internal int Slot, Token, Tile, Key;
            internal float Position, X;
            internal bool Running;
        }
        internal struct Frame
        {
            internal float Dt;
            internal double SongTime;
            internal int UnityFrame;
            internal float Pitch, TweenScale, PixelsPerWorld;
            internal int EventBegin, EventEnd, SampleBegin, SampleEnd, Evaluated;
        }
        internal sealed class Tape
        {
            internal FloorBakeCore.Table BakeTable;
            internal int BakeGeneration;
            internal int[] TileIds;
            internal string BakeInvalid = "";
            internal readonly List<FloorBake.Start> Starts = new List<FloorBake.Start>();
            internal int TileCount, SlotCount;
            internal long Reserved;
            internal bool Valid = true;
            internal string Reason = "";
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
            long n; reasons.TryGetValue(reason, out n); reasons[reason] = n + 1;
            Log("캡처 " + reason + " frames=" + tape.Frames.Count + " slots=" + tape.SlotCount
                + " events=" + tape.Events.Count + " samples=" + tape.Samples.Count
                + " 보수적예약MiB=" + (tape.Reserved / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)
                + " valid=" + valid + " reason-count=" + (n + 1)
                + " creates=" + (FloorAnim.Created - created0) + " natural-complete=" + (FloorAnim.Completed - completed0)
                + " complete/kill=" + (FloorAnim.Killed - killed0) + " cancelled=" + (FloorAnim.Dropped - dropped0)
                + " excluded-zero-writes=" + (FloorAnim.ZeroWrites - zero0) + " excluded-real-overlap=" + (FloorAnim.Real - real0)
                + " excluded-original-effects=" + (FloorAnim.Fallbacks - fallback0)
                + " coverage=FloorAnim-six-keys; ColorFloor/TweenColor는 FloorAnim 기록이 없어 이 검증 범위 밖");
            floors.Clear(); tokens.Clear(); slots.Clear(); owners.Clear();
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
                { Reject("usage: tilesim capture <초: 0초 초과~120초> | verify | bench | selftest"); return; }
                if (Capturing) { Reject("already-capturing"); return; }
                if (TileCost.Active) { Reject("tilecost-active"); return; }
                if (!Hitch.Playing || !FloorAnim.Enabled || !FloorAnim.Installed || !ZeroTween.CanEase)
                { Reject("not-playing-or-flooranim/ease-unavailable"); return; }
                if (!FloorAnim.SimDefaultPath()) { Reject("split-pending"); return; }
                var all = ADOBase.lm?.listFloors;
                if (all == null || all.Count > MaxTiles) { Reject("floor-list-missing-or-limit"); return; }
                tape = new Tape { TileCount = all.Count };
                tape.BakeTable = FloorBake.CaptureTable; tape.BakeGeneration = FloorBake.Generation;
                if (tape.BakeTable != null && (tape.BakeTable.Tiles.Length != all.Count || tape.BakeTable.Tiles.Where((x, i) => all[i] == null || x.Id != all[i].GetInstanceID()).Any()))
                    tape.BakeInvalid = "capture/table tile identity mismatch";
                tape.TileIds = new int[all.Count];
                for (int i = 0; i < all.Count; i++) tape.TileIds[i] = all[i] == null ? 0 : all[i].GetInstanceID();
                // Includes dictionary capacity, references and replay/output headroom.
                tape.Reserved = 1024 * 1024 + all.Count * 128L;
                serial = 0; inFrame = false; stepCount = 0;
                initialErrors = FloorAnim.Errors;
                zero0 = FloorAnim.ZeroWrites; real0 = FloorAnim.Real; fallback0 = FloorAnim.Fallbacks;
                created0 = FloorAnim.Created; completed0 = FloorAnim.Completed; killed0 = FloorAnim.Killed; dropped0 = FloorAnim.Dropped;
                floors.Clear(); tokens.Clear(); slots.Clear(); owners.Clear();
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && !floors.ContainsKey(all[i])) { floors.Add(all[i], i);  }
                deadline = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
                Capturing = true;
                foreach (var r in FloorAnim.SimRecords())
                {
                    if (!r.Running) continue;
                    State s;
                    if (!Identify(r, true, out s)) return;
                    if (!Reserve(512)) return;
                    tape.Initial.Add(s);
                }
                Log("캡처 시작 seconds=" + seconds.ToString("R", CultureInfo.InvariantCulture) + " initial=" + tape.Initial.Count
                    + " 타일=" + all.Count + " 표본=타일인덱스%4==0 (Rec 출력/진행 상태)");
                return;
            }
            if (cmd == "selftest")
            {
                if (Hitch.Playing || Capturing || TileCost.Active) { Reject("selftest-during-live-work"); return; }
                Log(SelfTest()); return;
            }
            if (cmd != "verify" && cmd != "bench") { Reject("usage: tilesim capture <초> | verify | bench | selftest"); return; }
            if (Capturing || TileCost.Active) { Reject("capture-or-tilecost-active"); return; }
            if (tape == null || !tape.Valid || tape.Frames.Count == 0)
            { Reject("유효한 완료 테이프 없음 " + (tape?.Reason ?? "")); return; }
            if (cmd == "verify") Verify(tape);
            else
            {
                if (Hitch.Playing) { Reject("bench-during-song"); return; }
                Bench(tape);
            }
        }
        private static bool Identify(FloorAnim.Rec r, bool fresh, out State s)
        {
            s = default;
            int d, slot, token;
            if (!floors.TryGetValue(r.F, out d) || r.Key < 0 || r.Key > 5)
            { Stop("타일 인덱스/Key 해석 불가", false); return false; }
            int key = d * 6 + r.Key;
            if (fresh)
            {
                FloorAnim.Rec old;
                if (owners.TryGetValue(key, out old) && !ReferenceEquals(old, r) && old.Running)
                { Stop("active-slot-overlap tile=" + d + " key=" + r.Key, false); return false; }
                owners[key] = r;
            }
            else if (!tokens.ContainsKey(r)) { Stop("missing-create", false); return false; }
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
            s = new State { Slot = slot, Tile = d, Key = r.Key, Token = token, Ease = r.E,
                Duration = r.Dur, Position = r.Pos, Over = r.Over, Period = r.Period,
                Started = r.Started, Running = r.Running, FS = r.FStart, FE = r.FEnd, FC = r.FChange,
                VS = r.VStart, VE = r.VEnd, VC = r.VChange };
            return true;
        }
        private static void AddEvent(Kind kind, FloorAnim.Rec r, bool fresh = false)
        {
            if (inFrame && stepCount != 0 && kind != Kind.Startup)
            { Stop("갱신 중 재진입 lifecycle 이벤트: " + kind, false); return; }
            State s;
            if (!Identify(r, fresh, out s) || !Reserve(512)) return;
            bool sample = kind == Kind.Complete && (s.Tile & 3) == 0;
            tape.Events.Add(new Event { Kind = kind, State = s, HasSample = sample, Sample = sample ? MakeSample(r, s.Slot, s.Token, s.Tile) : default });
        }
        internal static void Created(FloorAnim.Rec r) { AddEvent(Kind.Create, r, true); }
        internal static void Started(FloorAnim.Rec r) { AddEvent(Kind.Startup, r); }
        internal static void Completed(FloorAnim.Rec r) { AddEvent(Kind.Complete, r); }
        internal static void Cancelled(FloorAnim.Rec r) { AddEvent(Kind.Cancel, r); }
        internal static void BeginFrame(List<FloorAnim.Rec> records, bool research)
        {
            if (research || !FloorAnim.SimDefaultPath()) { Stop("연구 경로 변경", false); return; }
            if (!Hitch.Playing) { Stop("곡 재생 종료", tape.Frames.Count != 0); return; }
            // Events between updates belong to this next frame, in original order.
            frameEvent = tape.Frames.Count == 0 ? 0 : tape.Frames[tape.Frames.Count - 1].EventEnd;
            frameSample = tape.Samples.Count; stepCount = 0; inFrame = true;
        }
        internal static void Stepped(FloorAnim.Rec r)
        {
            stepCount++;
            int d, token, slot;
            if (!floors.TryGetValue(r.F, out d) || (d & 3) != 0) return;
            if (!tokens.TryGetValue(r, out token) || !slots.TryGetValue(d * 6 + r.Key, out slot))
            { Stop("표본에 시작 이벤트 없음", false); return; }
            if (!Reserve(96)) return;
            tape.Samples.Add(MakeSample(r, slot, token, d));
        }
        private static Sample MakeSample(FloorAnim.Rec r, int slot, int token, int d)
        {
            var s = new Sample { Slot = slot, Token = token, Tile = d, Key = r.Key, Position = r.Pos, Running = r.Running };
            s.X = r.Key == 3 ? r.VLast.x : r.Key == 4 ? r.VLast.y : r.FLast;
            return s;
        }
        internal static void EndFrame(List<FloorAnim.Rec> records, float td)
        {
            if (FloorAnim.Errors != initialErrors) { Stop("FloorAnim 예외 발생", false); return; }
            if (!inFrame) { Stop("프레임 경계 누락", false); return; }
            if (tape.Frames.Count >= MaxFrames || !Reserve(128)) { if (Capturing) Stop("프레임 12000개 상한", false); return; }
            var conductor = scrConductor.instance;
            var camera = scrCamera.instance == null ? null : scrCamera.instance.camobj;
            if (tape.BakeTable != null && tape.Frames.Count != 0 && conductor != null
                && conductor.songposition_minusi < tape.Frames[tape.Frames.Count - 1].SongTime)
                tape.BakeInvalid = "song clock moved backwards during capture";
            tape.Frames.Add(new Frame { Dt = td, SongTime = conductor == null ? double.NaN : conductor.songposition_minusi,
                UnityFrame = Time.frameCount, Pitch = conductor?.song == null ? float.NaN : conductor.song.pitch,
                TweenScale = DOTween.timeScale, PixelsPerWorld = camera != null && camera.orthographic ? camera.pixelHeight / (2f * camera.orthographicSize) : float.NaN,
                EventBegin = frameEvent, EventEnd = tape.Events.Count,
                SampleBegin = frameSample, SampleEnd = tape.Samples.Count, Evaluated = stepCount });
            inFrame = false;
            if (Stopwatch.GetTimestamp() >= deadline) Stop("완료", true);
        }

        // Structure of arrays: slot metadata/inputs and decoration-indexed outputs.
        // No Unity reads, Rec access, setter or allocation on the evaluation path.
        internal sealed class TileBatchEval
        {
            internal readonly int[] Tile, Key, Token;
            internal readonly Ease[] Ease;
            internal readonly bool[] Running, Started;
            internal readonly float[] Duration, Position, Over, Period;
            internal readonly float[][] Start, End, Change, Last;
            internal readonly float[] PosX, PosY, Rot, ScaleX, ScaleY, Opacity;
            private readonly int workers;
            private readonly Action<int> worker;
            private float dt;
            internal long WorkerTicks;
            internal TileBatchEval(int count, int floors, int partitions = 1)
            {
                Tile = new int[count]; Key = new int[count]; Token = new int[count]; Ease = new Ease[count];
                Running = new bool[count]; Started = new bool[count];
                Duration = new float[count]; Position = new float[count]; Over = new float[count]; Period = new float[count];
                Start = Channels(count); End = Channels(count); Change = Channels(count); Last = Channels(count);
                PosX = new float[floors]; PosY = new float[floors]; Rot = new float[floors];
                ScaleX = new float[floors]; ScaleY = new float[floors]; Opacity = new float[floors];
                workers = Math.Max(1, Math.Min(partitions, Math.Max(1, count)));
                worker = part => {
                    long t = Stopwatch.GetTimestamp();
                    EvaluateRange(part * (long)count / workers, (part + 1L) * count / workers, dt);
                    System.Threading.Interlocked.Add(ref WorkerTicks, Stopwatch.GetTimestamp() - t);
                };
            }
            private static float[][] Channels(int count) { return new[] { new float[count] }; }
            internal void Reset(Tape t)
            {
                Array.Clear(Running, 0, Running.Length); Array.Clear(Started, 0, Started.Length); Array.Clear(Token, 0, Token.Length);
                foreach (var channel in Last) Array.Clear(channel, 0, channel.Length);
                foreach (var output in new[] { PosX, PosY, Rot, ScaleX, ScaleY, Opacity }) Array.Clear(output, 0, output.Length);
                foreach (var s in t.Initial) Load(s);
            }
            private void Inputs(State s)
            {
                int i = s.Slot;
                Start[0][i] = s.Key == 3 ? s.VS.x : s.Key == 4 ? s.VS.y : s.FS;
                End[0][i] = s.Key == 3 ? s.VE.x : s.Key == 4 ? s.VE.y : s.FE;
                Change[0][i] = s.Key == 3 ? s.VC.x : s.Key == 4 ? s.VC.y : s.FC;
            }
            private void Load(State s)
            {
                int i = s.Slot; Tile[i] = s.Tile; Key[i] = s.Key; Token[i] = s.Token;
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
                int k = Key[i], d = Tile[i];
                Last[0][i] = Start[0][i] + Change[0][i] * e;
                float v = Last[0][i]; // FloorAnim never snaps to End, even on completion.
                switch (k) { case 0: PosX[d] = v; break; case 1: PosY[d] = v; break;
                    case 2: Rot[d] = v; break; case 3: ScaleX[d] = v; break;
                    case 4: ScaleY[d] = v; break; case 5: Opacity[d] = v; break; }
            }
        }
        private static int Bits(float f) { return BitConverter.SingleToInt32Bits(f); }
        private static string F(float f) { return f.ToString("R", CultureInfo.InvariantCulture) + "(0x" + Bits(f).ToString("X8") + ")"; }
        private static bool Compare(TileBatchEval eval, Sample s, out string detail)
        {
            int i = s.Slot; detail = "";
            if (eval.Token[i] != s.Token || Bits(eval.Position[i]) != Bits(s.Position) || eval.Running[i] != s.Running)
                detail = "token/pos/running expected=" + s.Token + "/" + F(s.Position) + "/" + s.Running
                    + " actual=" + eval.Token[i] + "/" + F(eval.Position[i]) + "/" + eval.Running[i];
            if (Bits(s.X) != Bits(eval.Last[0][i])) detail += " value expected=" + F(s.X) + " actual=" + F(eval.Last[0][i]);
            return detail.Length == 0;
        }
        private static void Verify(Tape t)
        {
            int partitions = Partitions(t);
            foreach (bool parallel in new[] { false, true })
            {
                var eval = new TileBatchEval(t.SlotCount, t.TileCount, partitions); eval.Reset(t);
                long[] counts = new long[6], diffs = new long[6]; int shown = 0;
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
                for (int k = 0; k < 6; k++) { total += counts[k]; mismatch += diffs[k]; Log("verify mode=" + (parallel ? "parallel" : "single") + " key=" + k + " comparisons=" + counts[k] + " mismatch=" + diffs[k]); }
                Log("verify " + (parallel ? "parallel" : "single") + " frames=" + t.Frames.Count + " comparisons=" + total
                    + " mismatch=" + mismatch + (total == 0 ? " 판정불가: 표본 없음" : " (Rec 출력 범위; Transform/렌더 검증 제외)"));
            }
        }
        private static void CheckSample(TileBatchEval eval, Sample s, long[] counts, long[] diffs, ref int shown, int frame, bool parallel)
        {
            counts[s.Key]++; string detail;
            if (Compare(eval, s, out detail)) return;
            diffs[s.Key]++;
            if (shown++ < 8) Log("불일치 mode=" + (parallel ? "parallel" : "single") + " frame=" + frame
                + " decoration=" + s.Tile + " key=" + s.Key + " " + detail);
        }
        private static void Bench(Tape t)
        {
            int passes = Math.Max(1, (300 + t.Frames.Count - 1) / t.Frames.Count);
            int n = checked(passes * t.Frames.Count), partitions = Partitions(t);
            long evaluated = 0; foreach (var f in t.Frames) evaluated += f.Evaluated;
            foreach (bool parallel in new[] { false, true })
            {
                var eval = new TileBatchEval(t.SlotCount, t.TileCount, partitions);
                Replay(eval, t, parallel); // warm JIT/ease/thread pool, excluded
                double[] evaluation = new double[n], events = new double[n], starts = new double[n], totals = new double[n], workers = new double[n], unaccounted = new double[n];
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
                        totals[ix] = Ms(end - begin); workers[ix] = Ms(eval.WorkerTicks);
                        unaccounted[ix] = totals[ix] - events[ix] - evaluation[ix]; ix++;
                    }
                }
                Log("bench mode=" + (parallel ? "Parallel.For N=" + partitions : "single") + " frames=" + n
                    + " slots=" + t.SlotCount + " evaluations/frame=" + (evaluated / (double)t.Frames.Count).ToString("F2", CultureInfo.InvariantCulture)
                    + " starts/frame=" + (creates / (double)n).ToString("F2", CultureInfo.InvariantCulture)
                    + " eval" + Stats(evaluation) + " events" + Stats(events) + " starts(subset)" + Stats(starts) + " total" + Stats(totals)
                    + (parallel ? " worker범위경과합" + Stats(workers) + " main-wait=eval 동기 호출 전체(분할+실행+join)" : "")
                    + " unaccounted" + Stats(unaccounted) + " checksum=" + Checksum(eval).ToString("X8") + " (total=events+eval; p99도 확인; 시작별 타이머 비용 포함)");
            }
        }
        private static int Partitions(Tape t) { return Math.Max(1, Math.Min(Math.Min(Environment.ProcessorCount, 8), t.SlotCount)); }
        private static void Replay(TileBatchEval eval, Tape t, bool parallel)
        {
            eval.Reset(t);
            foreach (var f in t.Frames) { for (int j = f.EventBegin; j < f.EventEnd; j++) eval.Process(t.Events[j]); eval.Evaluate(f.Dt, parallel); }
        }
        private static uint Checksum(TileBatchEval e)
        {
            uint hash = 2166136261;
            foreach (var a in new[] { e.PosX, e.PosY, e.Rot, e.ScaleX, e.ScaleY, e.Opacity })
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
            var t = new Tape { SlotCount = 2, TileCount = 1 };
            var a = new State { Slot = 0, Tile = 0, Key = 0, Token = 1, Ease = DG.Tweening.Ease.Linear,
                Duration = 1, FS = 2, FE = 10, FC = 8, Started = true, Running = true };
            var b = new State { Slot = 1, Tile = 0, Key = 4, Token = 2, Ease = DG.Tweening.Ease.Linear,
                Duration = 1, VE = new Vector3(0, 8, 0), Running = true };
            t.Initial.Add(a); t.Initial.Add(b);
            int checks = 0;
            foreach (bool parallel in new[] { false, true })
            {
                var e = new TileBatchEval(2, 1, 2); e.Reset(t);
                e.Evaluate(0, parallel); Require(e.Position[0] == 0, "zero dt"); checks++;
                b.Started = true; b.VS = new Vector3(3, 4, 1); b.VC = new Vector3(-3, 4, 0);
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
                var round = a; round.Token = 4; round.FS = 1e20f; round.FE = 1; round.FC = -1e20f;
                e.Process(new Event { Kind = Kind.Create, State = round });
                e.Process(new Event { Kind = Kind.Complete, State = round });
                Require(Bits(e.PosX[0]) == Bits(0f), "completion arithmetic must not snap to End"); checks++;
                a.Token = 1; a.FS = 2; a.FE = 10; a.FC = 8;
            }
            return "selftest passed=" + checks + " (synthetic; 실제 테이프/Mono 확인 별도)";
        }
        private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException("tilesim selftest: " + label); }
    }
}
#endif
