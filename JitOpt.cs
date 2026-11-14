using System.Runtime.InteropServices;

namespace StutterFix
{
    // Mono JIT 의 aggressive-inlining 을 켠다. 이후 컴파일되는 함수에만 적용된다.
    // 곡 전체 끔/켬 4맵: HELLO +5%, Windflower +2%, Exit +5.6%, DDONGSSADA +5.6%, 전 판 완주.
    internal static class JitOpt
    {
        [DllImport("mono-2.0-bdwgc")] private static extern uint mono_parse_default_optimizations(string names);
        [DllImport("mono-2.0-bdwgc")] private static extern void mono_set_defaults(int verbose, uint opts);
        private static bool applied;
        // 주의(2026-11-14 제보·실측): mono_parse_default_optimizations 는 Unity 가 실제로 쓰던 묶음이 아니라 Mono 기본 묶음에서 시작한다.
        // 그 기본 묶음에는 float32(float 를 single 로만 계산)가 들어 있어, 인라인만 켜려던 호출이 게임 전체의 float 계산 방식을 바꿨다
        // (AUTOTEST jitprobe: 16777216+1-16777216 이 끔 1 → 켬 0, KreScent 140~182초 Blur_Movie 0 나누기 예외 2,267 → 2,543).
        // Unity 원래 방식(중간값 double)을 지키도록 float32 를 명시적으로 끈다.
        internal static string Options = "aggressive-inlining,-float32";

        internal static void Apply(bool on)
        {
            if (on == applied) return;
            try
            {
                mono_set_defaults(0, mono_parse_default_optimizations(on ? Options : "-aggressive-inlining,-float32"));
                applied = on;
            }
            catch (System.Exception ex) { Main.Entry.Logger.Log("[JIT 최적화] 적용 안 함: " + ex.Message); }
        }
    }
}
