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
