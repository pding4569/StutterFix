using System.Globalization;
using System.Runtime.CompilerServices;

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
