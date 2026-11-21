using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace StutterFix
{
    // 로그 정책은 여기 한 곳에서 관리한다. DEV/AUTOTEST 는 기존 진단 출력을 그대로 보낸다.
    internal static class Lg
    {
        internal enum Level { Info, Warning, Error }

        internal static bool Verbose
        {
#if DEV || AUTOTEST
            get => true;
            set { }
#else
            get => verbose;
            set => verbose = value;
#endif
        }
#if !DEV && !AUTOTEST
        private static volatile bool verbose;
#endif

        // 대소문자도 기존 메시지 그대로 비교한다 (설치 기록의 FailAction 등은 실패가 아니다).
        private static readonly string[] EssentialWords =
        {
            "실패", "오류", "예외", "경고", "못 ", "Error", "error", "fail", "Exception"
        };
        private static readonly string[] EssentialPrefixes =
        {
            "This game is modded", "[안정성]", "[곡]", "Version '",
            "켜짐: 패치 설치 완료", "[곡 시작]", "[캐시] 남은 공간 부족",
            "[프레임 통계] 게임 기본:", "지금 Direct3D"
        };

        internal static bool IsEssential(string message)
        {
            if (message == null) return false;
            foreach (string word in EssentialWords)
                if (message.IndexOf(word, StringComparison.Ordinal) >= 0) return true;
            foreach (string prefix in EssentialPrefixes)
                if (message.StartsWith(prefix, StringComparison.Ordinal)) return true;
            // 멈춘 창 진단만 남긴다. 이 범주의 설치·상태 줄은 자세히 모드에서만 보낸다.
            return message.StartsWith("[첫 판 FPS]", StringComparison.Ordinal)
                && message.IndexOf("멈춘 프레임", StringComparison.Ordinal) >= 0;
        }

        private static Limiter limiter = new Limiter(Milliseconds, Emit);
        internal static void I(string message) => Write(Level.Info, message);
        internal static void W(string message) => Write(Level.Warning, message);
        internal static void E(string message) => Write(Level.Error, message);

        private static void Write(Level level, string message)
        {
#if DEV || AUTOTEST
            Emit(level, message);
#else
            if (level != Level.Info || Verbose || IsEssential(message)) limiter.Write(level, message);
#endif
        }

        private static long Milliseconds()
        {
            long ticks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency;
            return ticks / frequency * 1000 + ticks % frequency * 1000 / frequency;
        }

#if LOG_CHECK
        private static Action<Level, string> testOutput;
        internal static void SetTarget(Func<long> clock, Action<Level, string> output)
        {
            testOutput = output;
            limiter = new Limiter(clock, output);
        }
        private static void Emit(Level level, string message) => testOutput(level, message);
#else
        private static void Emit(Level level, string message)
        {
            switch (level)
            {
                case Level.Warning: Main.Entry.Logger.Warning(message); break;
                case Level.Error: Main.Entry.Logger.Error(message); break;
                default: Main.Entry.Logger.Log(message); break;
            }
        }
#endif

#if !LOG_CHECK && !DEV && !AUTOTEST
        private static System.Threading.Timer timer;
        private static int stopped;
        internal static void Start()
        {
            Volatile.Write(ref stopped, 0);
            var created = new System.Threading.Timer(_ =>
            {
                // 훅에서 로그가 더 안 나와도 창이 끝나면 요약한다. 게임 API 는 호출하지 않는다.
                try { if (Volatile.Read(ref stopped) == 0) limiter.Flush(); } catch { }
            }, null, 1000, 1000);
            if (Interlocked.CompareExchange(ref timer, created, null) != null) created.Dispose();
        }
        internal static void Stop()
        {
            Volatile.Write(ref stopped, 1);
            Interlocked.Exchange(ref timer, null)?.Dispose();
        }
#else
        internal static void Start() { }
        internal static void Stop() { }
#endif

        // 유한한 표: 키/창 검사에는 문자열 생성이나 잠금이 없다.
        // 슬롯 경합·표 포화·해시 충돌이면 같은 줄 제한만 느슨하게 하고 전체 제한은 계속 적용한다.
        internal sealed class Limiter
        {
            private struct Slot
            {
                internal int Busy, Count, Skipped;
                internal ulong Hash;
                internal long Started;
                internal string Sample;
            }
            private readonly Slot[] slots = new Slot[1024];
            private readonly Func<long> clock;
            private readonly Action<Level, string> output;
            // 상위 32비트: 초 + 1, 하위 32비트: 그 창에서 시도한 줄 수(요약 포함).
            private long global;
            [ThreadStatic] private static bool writing;

            internal Limiter(Func<long> clock, Action<Level, string> output)
            {
                this.clock = clock;
                this.output = output;
            }

            internal void Write(Level level, string message)
            {
                // 출력 대리자가 다시 로깅해도 무한 재귀를 만들지 않는다.
                if (writing) return;
                writing = true;
                try
                {
                    long now = clock();
                    if (SameLine(message, now)) Global(level, message, now);
                }
                finally { writing = false; }
            }

            private bool SameLine(string message, long now)
            {
                ulong hash = KeyHash(message);
                int first = (int)(hash & 1023);
                for (int probe = 0; probe < 4; probe++)
                {
                    ref Slot slot = ref slots[(first + probe) & 1023];
                    if (Interlocked.CompareExchange(ref slot.Busy, 1, 0) != 0) return true;
                    try
                    {
                        bool expired = slot.Sample != null && now - slot.Started >= 10000;
                        if (slot.Sample == null || expired)
                        {
                            Summarize(ref slot, now);
                            slot.Hash = hash; slot.Sample = message ?? "";
                            slot.Started = now; slot.Count = 1;
                            return true;
                        }
                        if (slot.Hash != hash || !SameKey(slot.Sample, message)) continue;
                        if (slot.Count < 3) { slot.Count++; return true; }
                        if (slot.Skipped < int.MaxValue) slot.Skipped++;
                        return false;
                    }
                    finally { Volatile.Write(ref slot.Busy, 0); }
                }
                return true;
            }

            internal void Flush()
            {
                if (writing) return;
                writing = true;
                try
                {
                    long now = clock();
                    Global(Level.Info, null, now, false);
                    for (int i = 0; i < slots.Length; i++)
                    {
                        ref Slot slot = ref slots[i];
                        if (Interlocked.CompareExchange(ref slot.Busy, 1, 0) != 0) continue;
                        try
                        {
                            if (slot.Sample != null && now - slot.Started >= 10000)
                            {
                                Summarize(ref slot, now);
                                slot.Sample = null; slot.Count = 0;
                            }
                        }
                        finally { Volatile.Write(ref slot.Busy, 0); }
                    }
                }
                finally { writing = false; }
            }

            private void Summarize(ref Slot slot, long now)
            {
                if (slot.Skipped == 0) return;
                int count = slot.Skipped;
                slot.Skipped = 0;
                Global(Level.Info, "(같은 로그 " + count + "번 생략: " + DisplayKey(slot.Sample) + ")", now);
            }

            private void Global(Level level, string message, long now, bool hasMessage = true)
            {
                uint second = unchecked((uint)(now / 1000 + 1));
                while (true)
                {
                    long old = Interlocked.Read(ref global);
                    uint oldSecond = (uint)((ulong)old >> 32), count = (uint)old;
                    // 다른 스레드에서 늦게 도착한 시각으로 창을 뒤로 돌리지 않는다.
                    bool next = oldSecond == 0 || unchecked((int)(second - oldSecond)) > 0;
                    uint skipped = next && count > 100 ? count - 100 : 0;
                    uint used = next ? (skipped > 0 ? 1u : 0u) : count;
                    if (!hasMessage && !next) return;
                    uint updated = hasMessage && used < uint.MaxValue ? used + 1 : used;
                    long value = unchecked((long)((ulong)(next ? second : oldSecond) << 32 | updated));
                    if (Interlocked.CompareExchange(ref global, value, old) != old) continue;
                    if (skipped > 0) output(Level.Info, "(로그 초과 " + skipped + "줄 생략)");
                    if (hasMessage && used < 100) output(level, message);
                    return;
                }
            }

            // 숫자를 뺀 첫 60글자를 바로 해시한다. 키 문자열은 요약을 실제로 쓸 때만 만든다.
            private static ulong KeyHash(string message)
            {
                ulong hash = 14695981039346656037UL;
                if (message == null) return hash;
                int length = 0;
                foreach (char c in message)
                {
                    if (char.IsDigit(c)) continue;
                    hash = unchecked((hash ^ c) * 1099511628211UL);
                    if (++length == 60) break;
                }
                return hash;
            }

            private static bool SameKey(string a, string b)
            {
                b = b ?? "";
                int ai = 0, bi = 0;
                for (int n = 0; n < 60; n++)
                {
                    while (ai < a.Length && char.IsDigit(a[ai])) ai++;
                    while (bi < b.Length && char.IsDigit(b[bi])) bi++;
                    if (ai == a.Length || bi == b.Length) return ai == a.Length && bi == b.Length;
                    if (a[ai++] != b[bi++]) return false;
                }
                return true;
            }

            private static string DisplayKey(string sample)
            {
                var key = new StringBuilder(60);
                foreach (char c in sample)
                {
                    if (char.IsDigit(c)) continue;
                    key.Append(c);
                    if (key.Length == 60) break;
                }
                return key.ToString();
            }
        }
    }
}
