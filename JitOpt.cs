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

        internal static void Apply(bool on)
        {
            if (on == applied) return;
            try
            {
                mono_set_defaults(0, mono_parse_default_optimizations(on ? "aggressive-inlining" : "-aggressive-inlining"));
                applied = on;
            }
            catch (System.Exception ex) { Main.Entry.Logger.Log("[JIT 최적화] 적용 안 함: " + ex.Message); }
        }
    }
}
