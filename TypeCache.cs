using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace StutterFix
{
    // 고급 필터 효과의 형식 찾기 Type.GetType("필터이름, Assembly-CSharp-firstpass") 결과를 기억해 두기.
    //
    // 유니티 Mono 에서 어셈블리 이름이 붙은 Type.GetType 은 한 번에 약 0.7ms 다(2026-10-03, 개발자용 측정: 26번에 18.2ms).
    // ffxSetFilterAdvancedPlus.ResetFilters("다른 필터 끄기")가 지금까지 쓴 필터마다 이것을 불러, 효과 하나가 2~18ms 였다.
    // HELLO (BPM) 2026 35초: 고급 필터 7개가 한 프레임에 22ms (곡 중 가장 긴 프레임 49ms 의 절반).
    // Setup(효과 준비, 맵 열기·재생 시작 때 효과마다)도 같은 호출을 한다.
    //
    // 이 두 함수 안의 Type.GetType(string) 호출만 캐시를 거치게 바꾼다(트랜스파일러). 두 곳 모두 어셈블리 이름을 붙여 부르므로
    // 결과는 부르는 쪽과 상관없고, 이미 불러온 어셈블리에서 같은 이름은 늘 같은 형식이다. 없는 이름(null)도 그대로 기억한다.
    // 개발자용: 기억한 값을 쓸 때 64번에 한 번 원래 함수로 다시 찾아 같은지 비교한다.
    internal static class TypeCache
    {
        internal static bool Enabled = true;
        internal static long Hits, Misses, DevChecks, DevMismatch;
        private static readonly Dictionary<string, Type> cache = new Dictionary<string, Type>();
        private static int replaced;

        internal static void Install(Harmony h)
        {
            replaced = 0;
            foreach (var name in new[] { "Setup", "ResetFilters" })
            {
                var m = AccessTools.Method(typeof(ffxSetFilterAdvancedPlus), name);
                if (m == null) { Main.Entry.Logger.Log("[형식 찾기] ffxSetFilterAdvancedPlus." + name + " 없음 - 이 함수는 원래대로"); continue; }
                h.Patch(m, transpiler: new HarmonyMethod(typeof(TypeCache), nameof(Transpiler)));
            }
            if (Edition.Dev) Main.Entry.Logger.Log("[형식 찾기] 고급 필터 형식 찾기 캐시 (바꾼 곳 " + replaced + ")");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            var orig = AccessTools.Method(typeof(Type), nameof(Type.GetType), new[] { typeof(string) });
            var mine = AccessTools.Method(typeof(TypeCache), nameof(Get));
            foreach (var c in ins)
            {
                if (c.opcode == OpCodes.Call && orig != null && ReferenceEquals(c.operand, orig)) { c.operand = mine; replaced++; }
                yield return c;
            }
        }

        public static Type Get(string name)
        {
            if (!Enabled || name == null) return Type.GetType(name);   // null 은 원래처럼 예외
            Type t;
            if (cache.TryGetValue(name, out t))
            {
                Hits++;
                if (Edition.Dev && (Hits & 63) == 0)
                {
                    DevChecks++;
                    if (!ReferenceEquals(Type.GetType(name), t)) { DevMismatch++; Main.Entry.Logger.Log("[형식 찾기] 다름: " + name); }
                }
                return t;
            }
            Misses++;
            t = Type.GetType(name);
            cache[name] = t;
            return t;
        }

        internal static string Summary()
        {
            if (Hits + Misses == 0) return "";
            string s = " | 필터 형식 찾기 기억 " + Hits + "번 (새로 찾음 " + Misses + "번" + (Edition.Dev ? ", 대조 " + DevChecks + "번 중 다름 " + DevMismatch : "") + ")";
            Hits = Misses = 0;
            return s;
        }
    }
}
