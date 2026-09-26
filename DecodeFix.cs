using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace StutterFix
{
    // 맵 파일 읽기의 "이벤트로 바꾸기"(LevelData.Decode -> 이벤트마다 LevelEvent.Decode)를 빠르게.
    //
    // Arche: 이벤트 137,460개에 3.9초. 속성마다 열거형 값을 만들며 Enum.ToObject(Type, int) 를 173만 번, Enum.Parse(Type, string) 을
    // 43만 번 불렀다(개발자용 측정). 둘 다 같은 입력이면 늘 같은 값을 돌려주는 함수인데, 유니티 Mono 에서는 한 번에 리플렉션을 거쳐 느리다.
    // LevelEvent.Decode 안의 이 두 호출만 캐시를 거치게 바꾼다(트랜스파일러). 캐시에 없으면 원래 함수를 불러 그 결과를 담고,
    // 예외(없는 이름 등)는 담지 않고 그대로 던진다. 돌려주는 값은 원래와 같은 열거형 값(상자에 담긴 같은 값)이다.
    internal static class DecodeFix
    {
        internal static bool Enabled = true;
        internal static Action Progress;   // 이벤트를 읽는 동안 가끔 부른다(윈도우 멈춘 창 판정 막기, WindowGhost)
        internal static long Hits, Misses;
        private static readonly object sync = new object();
        private static readonly Dictionary<Type, Dictionary<int, object>> byInt = new Dictionary<Type, Dictionary<int, object>>();
        private static readonly Dictionary<Type, Dictionary<string, object>> byName = new Dictionary<Type, Dictionary<string, object>>();
        private static int replaced;

        internal static void Install(Harmony h)
        {
            try
            {
                var ev = AccessTools.TypeByName("ADOFAI.LevelEvent") ?? AccessTools.TypeByName("LevelEvent");
                if (ev == null) { Main.Entry.Logger.Log("[맵 파일 읽기] 이벤트 바꾸기: 게임 코드 모양이 달라 끔"); return; }
                replaced = 0;
                foreach (var m in ev.GetMethods(AccessTools.all))
                    if (m.Name == "Decode" && m.DeclaringType == ev && !m.IsAbstract)
                        h.Patch(m, transpiler: new HarmonyMethod(typeof(DecodeFix), nameof(Transpiler)));
                if (replaced == 0) { Main.Entry.Logger.Log("[맵 파일 읽기] 이벤트 바꾸기: 바꿀 호출이 없어 효과 없음"); return; }
                Main.Entry.Logger.Log("[맵 파일 읽기] 이벤트로 바꾸기의 열거형 변환 캐시 설치 (바꾼 곳 " + replaced + ")");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[맵 파일 읽기] 이벤트 바꾸기 설치 실패: " + ex.Message); }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            var toObj = AccessTools.Method(typeof(Enum), "ToObject", new[] { typeof(Type), typeof(int) });
            var parse = AccessTools.Method(typeof(Enum), "Parse", new[] { typeof(Type), typeof(string) });
            var myToObj = AccessTools.Method(typeof(DecodeFix), nameof(ToObject));
            var myParse = AccessTools.Method(typeof(DecodeFix), nameof(Parse));
            foreach (var c in ins)
            {
                if (c.opcode == OpCodes.Call && toObj != null && ReferenceEquals(c.operand, toObj)) { c.operand = myToObj; replaced++; }
                else if (c.opcode == OpCodes.Call && parse != null && ReferenceEquals(c.operand, parse)) { c.operand = myParse; replaced++; }
                yield return c;
            }
        }

        public static object ToObject(Type enumType, int value)
        {
            var pg = Progress; if (pg != null) pg();
            if (!Enabled || enumType == null) return Enum.ToObject(enumType, value);
            object o;
            lock (sync)
            {
                Dictionary<int, object> m;
                if (byInt.TryGetValue(enumType, out m) && m.TryGetValue(value, out o)) { Hits++; return o; }
            }
            o = Enum.ToObject(enumType, value);   // 예외는 그대로
            lock (sync)
            {
                Dictionary<int, object> m;
                if (!byInt.TryGetValue(enumType, out m)) { m = new Dictionary<int, object>(); byInt[enumType] = m; }
                m[value] = o; Misses++;
            }
            return o;
        }

        public static object Parse(Type enumType, string value)
        {
            var pg = Progress; if (pg != null) pg();
            if (!Enabled || enumType == null || value == null) return Enum.Parse(enumType, value);
            object o;
            lock (sync)
            {
                Dictionary<string, object> m;
                if (byName.TryGetValue(enumType, out m) && m.TryGetValue(value, out o)) { Hits++; return o; }
            }
            o = Enum.Parse(enumType, value);   // 없는 이름 등의 예외는 그대로(담지 않음)
            lock (sync)
            {
                Dictionary<string, object> m;
                if (!byName.TryGetValue(enumType, out m)) { m = new Dictionary<string, object>(StringComparer.Ordinal); byName[enumType] = m; }
                m[value] = o; Misses++;
            }
            return o;
        }
    }
}
