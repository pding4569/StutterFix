#if AUTOTEST
using System.IO;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // 시험 전용: 모드 폴더의 mono-opt.txt 에 적힌 JIT 최적화를 이후 컴파일되는 함수에 적용한다.
    internal static class MonoOptProbe
    {
        [DllImport("mono-2.0-bdwgc")] static extern uint mono_parse_default_optimizations(string names);
        [DllImport("mono-2.0-bdwgc")] static extern void mono_set_defaults(int verbose, uint opts);

        internal static void Apply(string modDir, UnityModManagerNet.UnityModManager.ModEntry.ModLogger log)
        {
            string file = Path.Combine(modDir, "mono-opt.txt");
            if (!File.Exists(file)) return;
            string names = File.ReadAllText(file).Trim();
            uint opts = mono_parse_default_optimizations(names);
            mono_set_defaults(0, opts);
            log.Log("[자동 시험] Mono JIT 최적화 " + names + " = 0x" + opts.ToString("X"));
        }
    }
}
#endif
