using System.Globalization;
using System.Runtime.CompilerServices;
using StutterFix;
using DG.Tweening;
using DG.Tweening.Core.Easing;

// 계약 반례만 확인한다. 게임/Unity를 실행하거나 굽기 컴파일러를 흉내내지 않는다.
// 원본 식: DecoAnim.Step, DecoSim.DecoBatchEval.EvaluateRange.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
static string F(float x) => $"{x:R} [0x{BitConverter.SingleToInt32Bits(x):X8}]";
static void Require(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    Console.WriteLine("통과: " + label);
}

float slowDt = 1f / 60f, fastDt = slowDt / 2f;
float slow = Accumulate(slowDt, 60), fast = Accumulate(fastDt, 120);
double slowTotal = (double)slowDt * 60, fastTotal = (double)fastDt * 120;
Console.WriteLine($"입력 델타의 정확한 합: 60프레임={slowTotal:R}, 120프레임={fastTotal:R}");
Console.WriteLine($"누적 Pos: 60프레임={F(slow)}, 120프레임={F(fast)}");
Require(slowTotal == fastTotal, "두 델타 이력의 실제 합이 같다");
Require(BitConverter.SingleToInt32Bits(slow) != BitConverter.SingleToInt32Bits(fast),
    "같은 시간 합에서도 기존 float 누적 위치 비트가 다르다");
Console.WriteLine($"Linear 출력(시작 0, 끝 100, 길이 2): {F(slow / 2f * 100f)} / {F(fast / 2f * 100f)}");

// 순수 float 반올림 이외의 차이: 예약 시각과 실제 시작 프레임은 다르다.
// 양쪽 모두 동일 이벤트(예약 .1, duration 1, start 0, end 1), 관측 곡시각 .25.
// Update가 StartEffect를 부른 뒤 해당 프레임 DecoAnim Step이 도는 한 가능한 순서.
float[] historyA = { .125f, .125f };
float[] historyB = { .0625f, .0625f, .0625f, .0625f };
float valueA = Scheduled(historyA), valueB = Scheduled(historyB);
Console.WriteLine($"곡시각 .25, 예약 .1: 2프레임 진행={F(valueA)}, 4프레임 진행={F(valueB)}");
Require(historyA.Sum(x => (double)x) == historyB.Sum(x => (double)x), "예약 반례의 관측 곡시각이 같다");
Require(valueA != valueB, "예약을 지난 첫 프레임이 달라 비트 반올림보다 큰 차이가 가능하다");

// 위치 LastPosition은 상대 목표의 기준을 Kill(true) 전에 잡는다.
float current = 4f, oldTarget = 10f, delta = 2f;
float relativeBase = current;
current = oldTarget; // 이전 기록 Complete -> OnComplete setter
float newStart = current, newTarget = relativeBase + delta;
Console.WriteLine($"교체: 직전 현재=4, 이전 목표=10, 상대 +2 -> 새 시작={newStart}, 새 목표={newTarget}");
Require(newStart == 10 && newTarget == 6, "교체 시 단순 현재값 연결/끝값 기준 상대 계산이 모두 틀릴 수 있다");
Console.WriteLine("계약 검사 통과. .NET float 반례이며 Unity Mono 성능/실게임 대조 증명이 아니다.");

// Link the production compiler, rather than an independent model of its implementation.
static float ActualEase(int id, float time, float duration, float over, float period)
    => EaseManager.Evaluate((Ease)id, null, time, duration, over, period);
static DecoBakeCore.Decoration Decoration(float x = 0) => new() { Id = 1, Initial = new float[16] { x, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 } };
static DecoBakeCore.Event Event(double t, float duration, float target, bool relative = false, int source = 1, Ease ease = Ease.Linear)
{
    bool flash = ease == Ease.Flash || ease == Ease.InFlash || ease == Ease.OutFlash || ease == Ease.InOutFlash;
    var e = new DecoBakeCore.Event { T0 = t, Duration = duration, Targets = new[] { 0 }, RelativePosition = relative, Source = source, Ease = (int)ease, Over = flash ? 1 : 1.70158f };
    e.Values[0] = target; return e;
}
static DecoBakeCore.Table Compile(DecoBakeCore.Decoration d, DecoBakeCore.Event[] events, float pitch = 1, float scale = 1)
    => DecoBakeCore.Compile(new[] { d }, events, pitch, scale, ActualEase, CancellationToken.None);
var replacement = Compile(Decoration(), new[] { Event(0, 10, 10), Event(4, 2, 2, true, 2) });
Require(replacement.Tracks[0][1].Start == 10 && replacement.Tracks[0][1].End == 6,
    "실제 컴파일러: 현재4/끝10/상대+2 -> 시작10/목표6");
Require(replacement.Eval(0, 0, 3.999) < 4.001f && replacement.Eval(0, 0, 4) == 10 && replacement.Eval(0, 0, 5) == 8,
    "교체 직전/경계/중간 평가와 이전 구간 cutoff");
Require(replacement.Replaced[0], "교체 장식 분류");
Require(replacement.Eval(0, 0, -1) == 0 && replacement.Eval(0, 0, 99) == 6 && replacement.Eval(0, 0, 5) == 8,
    "미래/과거/되감기 평가가 같은 입력에서 같은 값");
var instant = Compile(Decoration(), new[] { Event(2, 0, 7), Event(2, 0, 3, true, 2) });
Require(instant.Eval(0, 0, 1.999) == 0 && instant.Eval(0, 0, 2) == 10, "길이0 즉시 적용/동일T0 원본 순서/상대 반복");
var tile = Decoration(105); tile.OriginalX = 105; tile.GlobalX = 5;
Require(DecoBakeCore.PlacementReason("Tile", false) == null, "Tile 정적 초기 배치 허용");
var resetOrigin = Event(2, 0, float.NaN); resetOrigin.ResetGlobalOrigin = true;
var tileTrack = Compile(tile, new[] { Event(0, 0, 2), resetOrigin, Event(3, 0, 4), Event(4, 0, 2, true) });
Require(tileTrack.Supported == 1 && tileTrack.Eval(0, 0, 0) == 107 && tileTrack.Eval(0, 0, 2) == 107
    && tileTrack.Eval(0, 0, 3) == 9 && tileTrack.Eval(0, 0, 4) == 11 && tileTrack.Eval(0, 0, -1) == 105,
    "Tile Setup 원점/위치 없는 Global 전환/생략 시 원점 유지/LastPosition/되감기");
var globalNow = Event(0, 0, 4); globalNow.ResetGlobalOrigin = true;
Require(Compile(tile, new[] { globalNow }).Eval(0, 0, 0) == 9, "같은 효과 Global 변경이 목표 계산에 선행");
Require(DecoBakeCore.MovementReason("Tile", false) == null && DecoBakeCore.MovementReason("LastPosition", true) == null,
    "disabled Tile은 재배치 안 함, LastPosition은 원점 유지");
foreach (var example in new[] { ("Tile", true), ("Camera", false), ("CameraAspect", false) })
{
    var dynamicDec = Decoration();
    dynamicDec.Reasons.Add(DecoBakeCore.PlacementReason(example.Item1, example.Item2)!);
    var dynamicTable = Compile(dynamicDec, new[] { Event(0, 1, 2), ScaleEvent(1, 1, 2, 2) });
    Require(dynamicTable.Supported == 0 && dynamicTable.Segments == 0, example.Item1 + " 동적 초기 배치는 크기 포함 장식 전체 제외");
}
foreach (string placement in new[] { "Tile", "Camera", "CameraAspect" })
{
    var dynamicEvent = Event(2, 1, 3);
    dynamicEvent.Reasons = new[] { DecoBakeCore.MovementReason(placement, true)! };
    var dynamicTable = Compile(Decoration(), new[] { Event(0, 1, 2), dynamicEvent });
    Require(dynamicTable.Supported == 0 && dynamicTable.Segments == 0, placement + " 나중 동적 배치 변경도 앞선 트랙 전부 제외");
}
// Camera raw snapshot alone can be constant; projection still changes with zoom.
float screenCoordinate = 5f / 20f + .5f;
Require(screenCoordinate == .75f && (screenCoordinate - .5f) * 20 != (screenCoordinate - .5f) * 40,
    "Camera 정적 좌표 fixture도 줌에 따른 월드 투영은 동적: 정적 지원 갈래 없음");
var repeat = Compile(Decoration(), new[] { Event(0, 1, 2, true, 1), Event(2, 1, 2, true, 2), Event(4, 1, 2, true, 3) });
Require(repeat.Segments == 3 && repeat.Eval(0, 0, 5) == 6 && repeat.Tracks[0][2].Source == 3, "전개된 반복 인스턴스를 합치지 않는다");
var pitchTable = Compile(Decoration(), new[] { Event(1, 2, 10) }, 2, .5f);
Require(pitchTable.Eval(0, 0, 5) == 5 && pitchTable.Eval(0, 0, 9) == 10, "곡 시각 간격 / 고정pitch * 고정DOTween배율");
static DecoBakeCore.Event ScaleEvent(double t, float duration, float x, float y, bool relativePosition = false, Ease ease = Ease.Linear)
{
    var e = Event(t, duration, float.NaN, relativePosition, ease: ease);
    DecoBakeCore.CopyScale(e, true, float.NaN, x, y); return e;
}
var sizes = Compile(Decoration(), new[] { ScaleEvent(0, 10, 11, 21), ScaleEvent(4, 2, 3, float.NaN, true) });
Require(sizes.Supported == 1 && sizes.Tracks[5][1].Start == 11 && sizes.Tracks[5][1].End == 3,
    "크기 교체: 현재5/이전끝11 -> 새시작11, LastPosition도 크기는 상대 덧셈 없이 절대3");
Require(sizes.Eval(0, 5, 4) == 11 && sizes.Eval(0, 5, 5) == 7 && sizes.Eval(0, 6, 5) == 11,
    "크기 NaN Y축은 기존 Y 트랙 계속, X 교체가 Y를 덮어쓰지 않음");
Require(sizes.Eval(0, 5, 100) == 3 && sizes.Eval(0, 5, 5) == 7 && sizes.Eval(0, 5, -1) == 1,
    "크기 무상태 미래/되감기/시작 이전");
var sameFrame = Compile(Decoration(), new[] { ScaleEvent(2, 4, 9, 13), ScaleEvent(2, 2, 5, float.NaN), ScaleEvent(2, 0, float.NaN, -2) });
Require(sameFrame.Tracks[5][1].Start == 9 && sameFrame.Tracks[6][1].Start == 13
    && sameFrame.Eval(0, 5, 2) == 9 && sameFrame.Eval(0, 6, 2) == -2 && sameFrame.Eval(0, 5, 3) == 7,
    "첫Step 전 동일시각 여러 크기: Kill(true) 미시작 완료, 축별 순서와 길이0/음수 목표");
var legacy = ScaleEvent(0, 0, 2, 3);
DecoBakeCore.CopyScale(legacy, true, 4, 2, 3);
Require(Compile(Decoration(), new[] { legacy }).Eval(0, 6, 0) == 4, "legacy targetScale scalar 우선, 이미 Decode된 비율 그대로");
var disabledScale = Event(0, 0, float.NaN);
DecoBakeCore.CopyScale(disabledScale, false, 4, 2, 3);
Require(Compile(Decoration(), new[] { disabledScale }).Segments == 0, "미사용/forceDontTweenMovement 크기 채널 미생성");
var nanScale = Compile(Decoration(), new[] { ScaleEvent(0, 0, float.NaN, float.NaN) });
Require(nanScale.Segments == 0 && nanScale.Eval(0, 5, 0) == 1, "null에서 Decode된 NaN 양축은 미적용");
var oddEase = Compile(Decoration(), new[] { ScaleEvent(0, 0, 8, float.NaN, ease: Ease.INTERNAL_Zero) });
Require(oddEase.Eval(0, 5, 0) == 1 + 7 * ActualEase((int)Ease.INTERNAL_Zero, 1, 1, 1.70158f, 0), "길이0도 실제 EaseManager 끝점 사용");
var cancellationSize = Decoration(); cancellationSize.Initial[5] = 1e20f;
var cancellationTrack = Compile(cancellationSize, new[] { ScaleEvent(0, 0, 1, float.NaN) });
Require(cancellationTrack.Eval(0, 5, 0) == 0, "크기 완료는 목표1 강제 대입이 아닌 float 시작+변화량 산술(상쇄 결과0)");
var overflowSize = Decoration(); overflowSize.Initial[5] = -float.MaxValue;
var overflowTrack = Compile(overflowSize, new[] { Event(0, 1, 7), ScaleEvent(2, 1, float.MaxValue, 1) });
Require(overflowTrack.Supported == 0 && overflowTrack.Segments == 0 && overflowSize.Reasons.Contains("크기 산술 비유한"),
    "크기 변화량 overflow는 사유를 구분하고 앞의 위치 트랙까지 장식 전체 제외");
int scaleEaseSamples = 0;
foreach (Ease ease in Enum.GetValues<Ease>())
{
    if (ease == Ease.Unset || ease == Ease.INTERNAL_Custom) continue;
    var track = Compile(Decoration(), new[] { ScaleEvent(0, 4, -7, 3, ease: ease) });
    float over = track.Tracks[5][0].Over;
    for (int i = 1; i <= 100; i++)
    {
        float t = i / 25f;
        float expected = 1 + (-7 - 1) * ActualEase((int)ease, t, 4, over, 0);
        RequireSilent(BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(track.Eval(0, 5, t)), "scale ease " + ease);
        scaleEaseSamples++;
    }
}
Console.WriteLine("통과: 크기 이징 중간/완료 plugin 산술 " + scaleEaseSamples + "개");
var excluded = Event(10, 1, 100); excluded.Reasons = new[] { "미지원 fixture" };
var exclusion = Compile(Decoration(), new[] { Event(0, 1, 10), excluded });
Require(exclusion.Supported == 0 && exclusion.Segments == 0 && exclusion.Decorations[0].Reasons.Contains("미지원 fixture"), "나중의 미지원도 장식 전체를 제외");
int easeSamples = 0;
foreach (Ease ease in Enum.GetValues<Ease>())
{
    if (ease == Ease.Unset || ease == Ease.INTERNAL_Custom || ease == Ease.INTERNAL_Zero) continue;
    var track = Compile(Decoration(), new[] { Event(2, 4, 100, false, 1, ease) });
    for (int i = 1; i < 100; i++)
    {
        float time = i / 25f;
        float expected = 100 * ActualEase((int)ease, time, 4, track.Tracks[0][0].Over, 0);
        float got = track.Eval(0, 0, 2 + (double)time);
        RequireSilent(BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(got), "easing " + ease);
        easeSamples++;
    }
    RequireSilent(track.Eval(0, 0, 6) == 100, "completion " + ease);
}
Console.WriteLine("통과: 게임 DOTween EaseManager 직접 재사용 " + easeSamples + "개 중간값/각 완료 목표값");
bool badOrder = false;
try { Compile(Decoration(), new[] { Event(2, 1, 1), Event(1, 1, 1) }); } catch (ArgumentException) { badOrder = true; }
Require(badOrder, "예약 정렬이 깨지면 추정 재정렬 대신 거부");
bool cancelled = false;
try { DecoBakeCore.Compile(new[] { Decoration() }, new[] { Event(0, 1, 1) }, 1, 1, ActualEase, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
Require(cancelled, "Unload/세대 변경 취소");
Console.WriteLine("굽기 프로토타입 fixture 통과. 게임 실행/설치/Mono 런타임 측정은 하지 않음.");

Require(DecoBakeCore.ScheduledTime(12, 2) == 10, "예약시계는 startTime-startEffectOffset, 별도 calibration/addoffset 보정 없음");
var clockFixture = Compile(Decoration(), new[] { Event(DecoBakeCore.ScheduledTime(12, 2), 8, 80) });
double Loss(double offset) => Enumerable.Range(0, 100).Average(i => {
    double song = 11 + i * .04;
    double actual = clockFixture.Eval(0, 0, song + .75);
    double error = clockFixture.Eval(0, 0, song + offset) - actual;
    return error * error;
});
double fitted = DecoBakeCore.FitOffset(Loss);
Require(Math.Abs(fitted - .75) <= .000125 && Loss(fitted) < 1e-9, "진단 오프셋 부호/알려진 +0.75초 복원과 값 잔차");
Require(DecoBakeCore.FitOffset(_ => 1) == 0, "평탄 손실은 임의의 -32초를 선택하지 않고 0 유지(시간 식별 증거 아님)");
var cursorTable = Compile(Decoration(), new[] { Event(1, 2, 10), Event(2, 1, 20), Event(4, 0, 30) });
var cursor = new DecoBakeCore.ActiveEvaluator(cursorTable);
Require(cursor.Step(0) == 0 && cursor.Evaluated == 0, "활성 평가: 미래 예약은 평가하지 않음");
Require(cursor.Step(1.5) == cursorTable.Eval(0, 0, 1.5) && cursor.Evaluated == 1, "활성 평가: 예약 dispatch와 채널 커서");
Require(cursor.Step(2.5) == cursorTable.Eval(0, 0, 2.5) && cursor.Dispatched == 1, "활성 평가: 교체 구간으로 커서 이동");
Require(cursor.Step(3) == 20 && cursor.Evaluated == 1 && cursor.Step(3.5) == 0 && cursor.Evaluated == 0, "활성 평가: 완료값 한 번 후 비활성 제거");
Require(cursor.Step(4) == 30 && cursor.Step(4) == 0, "활성 평가: 길이0 및 같은 시각 중복 호출");
Require(cursor.Step(1.5) == cursorTable.Eval(0, 0, 1.5), "활성 평가: 되감기는 명시적 scheduler 초기화");
Require(System.Runtime.InteropServices.Marshal.SizeOf<DecoBakeCore.Segment>() == 48, "Segment 실제 unmanaged stride 48B, payload 산식과 일치");

// Bake-6: exact evidence rows; no actual/RecPos value seeds or calibrates the tested clock.
string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/handoff/bake5early-evidence.txt"));
var rowPattern = new System.Text.RegularExpressions.Regex(@"진단 행 장식=-587466 채널=위치X frame=(\d+) songposition_minusi=([^ ]+) actual=([^ ]+) baked=[^ ]+ dt=([^ ]+) RecPos=([^\s]+)");
var rows = rowPattern.Matches(File.ReadAllText(evidence)).Select(m => new {
    Frame = int.Parse(m.Groups[1].Value), Song = double.Parse(m.Groups[2].Value), Actual = double.Parse(m.Groups[3].Value),
    Dt = double.Parse(m.Groups[4].Value), Rec = double.Parse(m.Groups[5].Value) }).ToArray();
Require(rows.Length == 12 && rows[0].Frame == 22630 && rows[11].Frame == 22641, "증거 파일 12개 행 고정 재현");
var smooth = new DecoBakeCore.SongClock();
var measuredRaw = new List<double>(); var measuredSmooth = new List<double>();
const double speedX = 11460 / .9;
foreach (var row in rows)
{
    double tb = smooth.Step(row.Song, row.Dt, 1, 1, true);
    double Eval(double time) => 5812.06055 - speedX * (time - 9.9);
    RequireSilent(Math.Abs(Eval(9.9 + row.Rec) - row.Actual) < .003, "실측 RecPos/actual 일치");
    measuredRaw.Add(Math.Abs(Eval(row.Song) - row.Actual) / speedX * 1000);
    measuredSmooth.Add(Math.Abs(Eval(tb) - row.Actual) / speedX * 1000);
}
Console.WriteLine($"실측12행 cold raw 초기화: raw 최대={measuredRaw.Max():F6}ms RMS={Math.Sqrt(measuredRaw.Average(x=>x*x)):F6}ms; tb 최대={measuredSmooth.Max():F6}ms RMS={Math.Sqrt(measuredSmooth.Average(x=>x*x)):F6}ms; raw/tb 원단위최대={measuredRaw.Max()*speedX/1000:F6}/{measuredSmooth.Max()*speedX/1000:F6}");
Console.WriteLine($"실측12행 tb 최대프레임={measuredSmooth.Select((x,i)=>x/(rows[i].Dt*1000)).Max():F6}; cold 12행만으로 전체 p99 판정 불가");
Require(measuredSmooth.Max() < measuredRaw.Max() && measuredSmooth.Average(x=>x*x) < measuredRaw.Average(x=>x*x),
    "실측12행 cold clock 최대/RMS 시간오차 감소(2프레임 기준 충족으로 간주하지 않음)");
foreach (bool buffer in new[] { false, true })
{
    var testClock = new DecoBakeCore.SongClock(); var random = new Random(621);
    const double dt = 1.0 / 480; double maximum = 0, previous = 0, tail = 0;
    testClock.Step(0, dt, 1, 1, true);
    for (int i = 1; i <= 288000; i++)
    {
        double truth = i * dt;
        double jitter = buffer ? ((i % 7) - 3) * .002 : (random.NextDouble() * 2 - 1) * .006;
        double tb = testClock.Step(truth + jitter, dt, 1, 1, true);
        RequireSilent(tb >= previous, "합성 지터 단조 보장"); previous = tb;
        maximum = Math.Max(maximum, Math.Abs(tb-truth));
        if (i > 280000) tail += tb-truth;
    }
    Console.WriteLine($"합성 {(buffer ? "버퍼계단" : "균등±6ms")} 600초@480FPS: 최대={maximum*1000:F6}ms 평균말미={tail/8000*1000:F6}ms");
    Require(maximum <= dt * 2 && Math.Abs(tail/8000) < .0002, "합성 최대 2프레임 이내/장기 드리프트 없음");
    Require(testClock.Step(42, dt, 1, 1, false) == 42 && testClock.Step(42, dt, 1, 1, false) == 42, "일시정지 즉시 동기/정지 유지");
    Require(testClock.Step(12, dt, 1, 1, true) == 12 && testClock.Step(2, dt, 1, 1, true) == 2,
        "재개/되감기/재시작 즉시 동기");
    Require(testClock.Step(262, dt, 1, 1, true) == 262, "에디터 임의 점프 즉시 동기");
}
var pitchClock = new DecoBakeCore.SongClock(); pitchClock.Step(0, 0, 2, .5, true);
Require(Math.Abs(pitchClock.Step(.02, .005, 2, .5, true) - .02) < 1e-12, "Rec 델타의 tweenScale 제거/pitch 반영");
// Bake-7: immutable first timeline survives consumed scheduling lists and repeated Setup.
var identities = new[] { new object(), new object() }; var identityList = identities.ToList();
var identityIds = new[] { 10, 20 };
int Identity(object o) => ReferenceEquals(o, identities[0]) ? 10 : 20;
Require(DecoBakeCore.SameInstances(identityList, identities, identityIds, Identity), "동일 맵 객체/ID 목록 재사용");
identityList[1] = new object();
Require(!DecoBakeCore.SameInstances(identityList, identities, identityIds, Identity), "같은 ID라도 재생성 객체 거부");
identityList = identities.Reverse().ToList();
Require(!DecoBakeCore.SameInstances(identityList, identities, identityIds, Identity), "목록 순서 변경 거부");
identityList.RemoveAt(0);
Require(!DecoBakeCore.SameInstances(identityList, identities, identityIds, Identity), "목록 개수 변경 거부");
var savedDecs = new[] { Decoration() }; var savedEvents = new[] { Event(0, 4, 8), ScaleEvent(1, 3, 5, 6) };
var firstBake = DecoBakeCore.Compile(DecoBakeCore.CloneDecorations(savedDecs), savedEvents, 1, 1, ActualEase, CancellationToken.None);
var consumed = savedEvents.ToList(); consumed.RemoveAt(0);
var rebuilt = DecoBakeCore.Compile(DecoBakeCore.CloneDecorations(savedDecs), savedEvents, 1, 1, ActualEase, CancellationToken.None);
Require(firstBake.Supported == rebuilt.Supported && firstBake.Segments == rebuilt.Segments
    && firstBake.Eval(0, 0, 2) == rebuilt.Eval(0, 0, 2), "seek로 예약 목록 소모 후 기준 재굽기 지원/구간/과거값 보존");
var cloned = DecoBakeCore.CloneDecorations(savedDecs); cloned[0].Initial[0] = 999; cloned[0].Reasons.Add("changed");
Require(savedDecs[0].Initial[0] != 999 && savedDecs[0].Reasons.Count == 0, "재굽기 배열/제외사유 깊은 복사");
foreach (bool smoothedDelta in new[] { false, true })
foreach (double stall in new[] { .03, .06 })
{
    var heavyClock = new DecoBakeCore.SongClock(); var rng = new Random(707);
    double truth = 262, previous = truth, filtered = .011;
    var errors = new List<double>(); int longFrames = 0;
    heavyClock.Step(truth, 0, 1, 1, true);
    for (int i = 1; truth < 276; i++)
    {
        double dt = i % 97 == 0 ? stall : .011 + (rng.NextDouble() * 2 - 1) * .002;
        if (dt > .02) longFrames++;
        truth += dt; filtered += .1 * (dt - filtered);
        double tweenDt = smoothedDelta ? filtered : dt;
        double raw = truth + (rng.NextDouble() * 2 - 1) * .006;
        double tb = heavyClock.Step(raw, tweenDt, 1, 1, true);
        RequireSilent(tb >= previous, "무거운 합성 구간 단조(역행 seek 없음)"); previous = tb;
        errors.Add(Math.Abs(tb - truth) * 1000);
    }
    errors.Sort();
    Console.WriteLine($"bake7 합성262~276 dt={(smoothedDelta ? "EMA0.1(게임 smoothDeltaTime 재현 아님)" : "실제프레임")} 기본11±2ms stall={stall*1000:F0}ms n={errors.Count} 긴프레임={longFrames} 최대={errors[^1]:F6}ms p99={errors[(int)((errors.Count-1)*.99)]:F6}ms 재동기={heavyClock.Resyncs}; 1% 시정수@11ms={-.011/Math.Log(.99):F6}s @480FPS={-(1.0/480)/Math.Log(.99):F6}s");
    Require(errors.All(double.IsFinite), "무거운 구간 시계 유한값");
}
var thresholdClock = new DecoBakeCore.SongClock(); thresholdClock.Step(262, 0, 1, 1, true);
Require(Math.Abs(thresholdClock.Step(262.06, .011, 1, 1, true) - 262.01149) < 1e-9
    && thresholdClock.Resyncs == 1, "60ms 긴 프레임이어도 예측차49ms이면 50ms 재동기 미발동");
Require(thresholdClock.Step(262.13, .011, 1, 1, true) == 262.13 && thresholdClock.Resyncs == 2,
    "예측차50ms 초과시 재동기");
var mutable = new List<int> { 10, 20 }; int oldCount = mutable.Count;
mutable.RemoveAt(1); bool recovered = false;
try { DecoBakeCore.SnapshotItem(mutable, 1, oldCount, 1, 1); }
catch (DecoBakeCore.SnapshotChangedException) { recovered = true; }
Require(recovered, "seek 후 축소 목록: IndexOutOfRange 대신 재준비 신호");
recovered = false; mutable.Add(30);
try { DecoBakeCore.SnapshotItem(mutable, 1, oldCount, 1, 2); }
catch (DecoBakeCore.SnapshotChangedException) { recovered = true; }
Require(recovered && DecoBakeCore.SnapshotItem(mutable, 1, mutable.Count, 2, 2) == 30, "동일 크기 새 세대 거부/새 스냅샷 복구");
Require(Math.Abs(DecoBakeCore.Speed(cursorTable, 0, 0, 1.5) - 5) < .01
    && double.IsNaN(DecoBakeCore.Speed(cursorTable, 0, 0, 4)), "시간환산 선형 속도/불연속 NA");
var resetTable = Compile(Decoration(), new[] { Event(-2, 100, 100) });
var resetCursor = new DecoBakeCore.ActiveEvaluator(resetTable);
double lastTime = -1; int measuredFrames = 0, resetFrames = 0;
resetCursor.Step(lastTime);
foreach (double time in new[] { -.99, -.98, -1.2, -1.19, -1.18, 10, 10.01 })
{
    double value = resetCursor.Step(time);
    RequireSilent(Math.Abs(value - resetTable.Eval(0, 0, time)) < 1e-6, "되감기 전후 커서값 대조");
    if (time < lastTime || time - lastTime > .05) resetFrames++; else measuredFrames++;
    lastTime = time;
}
Require(measuredFrames == 5 && resetFrames == 2, "카운트다운 되감기/seek 프레임 제외 후 단조 5프레임 측정 지속");

if (args.Contains("--bench"))
{
    // Only shape/scale smoke evidence. One target per event is deliberately sparse and is NOT the Arche event fanout.
    const int decorationCount = 28828, eventCount = 95211;
    var decorations = Enumerable.Range(0, decorationCount).Select(i => { var d = Decoration(); d.Id = i; return d; }).ToArray();
    var events = Enumerable.Range(0, eventCount).Select(i => {
        var e = Event(i / 1000.0, 1, i % 200, true, i); e.Targets[0] = i % decorationCount; return e;
    }).ToArray();
    GC.Collect(); long beforeBytes = GC.GetTotalMemory(true);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var baked = DecoBakeCore.Compile(decorations, events, 1, 1, ActualEase, CancellationToken.None);
    double compileMs = sw.Elapsed.TotalMilliseconds;
    long afterBytes = GC.GetTotalMemory(false);
    var samples = new List<double>(); double checksum = 0;
    for (int pass = 0; pass < 33; pass++)
    {
        sw.Restart();
        for (int d = 0; d < decorationCount; d++) for (int c = 0; c < 16; c++) checksum += baked.Eval(d, c, pass * 3.125);
        if (pass > 0) samples.Add(sw.Elapsed.TotalMilliseconds);
    }
    samples.Sort();
    Console.WriteLine($"합성 희소 fixture(.NET8, Arche 아님): 장식={decorationCount}, 이벤트={eventCount}, 대상참조={eventCount}, 구간={baked.Segments}");
    Console.WriteLine($"worker함수 굽기={compileMs:F3}ms, payload={baked.PayloadBytes}B, GC전heap차={afterBytes-beforeBytes}B, 16채널평가중앙={samples[16]:F3}ms, p99={samples[31]:F3}ms, checksum={checksum:R}");
    Require(baked.Supported == decorationCount && baked.Events == eventCount, "합성 크기 처리 완료");
    var activeEval = new DecoBakeCore.ActiveEvaluator(baked); activeEval.Step(30);
    var activeSamples = new List<double>(); int channels = 0;
    for (int i = 1; i <= 720; i++) { sw.Restart(); checksum += activeEval.Step(30 + i / 60.0);
        activeSamples.Add(sw.Elapsed.TotalMilliseconds); channels += activeEval.Evaluated; }
    activeSamples.Sort();
    Console.WriteLine($"합성 evalActive(.NET8, Arche/Unity 아님): frames=720, 중앙={activeSamples[360]:F6}ms, p99={activeSamples[712]:F6}ms, 평균채널={channels/720.0:F3}, checksum={checksum:R}; scheduler 구축/첫seek 제외");
}

static void RequireSilent(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }

[MethodImpl(MethodImplOptions.NoInlining)]
static float Accumulate(float dt, int n)
{
    float pos = 0;
    for (int i = 0; i < n; i++) pos += dt;
    return pos;
}
static float Scheduled(float[] deltas)
{
    double song = 0;
    float pos = 0;
    bool started = false;
    foreach (float dt in deltas)
    {
        song += dt;
        if (song >= .1) started = true;
        if (started) pos += dt;
    }
    return pos;
}
