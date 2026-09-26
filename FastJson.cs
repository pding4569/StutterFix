using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace StutterFix
{
    // 맵 파일(.adofai) 읽기를 빠르게: 게임의 JSON 해석기(Assembly-CSharp-firstpass 의 Json.Deserialize, MiniJSON 변형)와
    // 똑같은 결과를 내는 해석기로 바꾼다.
    //
    // 원래 해석기는 StringReader 로 한 글자씩 Peek/Read(가상 호출)하고, 글자마다 Convert.ToChar 와 문자열 IndexOf 를 부르고,
    // 모든 문자열·숫자를 StringBuilder 에 한 글자씩 붙인다. Arche(42MB) 맵 파일 읽기가 6.4초였다.
    // 여기서는 문자열을 위치 번호로 직접 읽는다. 규칙은 IL 그대로 옮겼다(2026-09-26 확인):
    //   공백 = ' ' '\t' '\n' '\r' U+FEFF, 낱말 끝 = ' ' '\t' '\n' '\r' { } [ ] , : "
    //   토큰: '{' '[' '"' ':' 숫자·'-' 는 읽지 않고, '}' ']' ',' 는 하나 읽고. 그 밖은 낱말을 읽어 true/false/null, 아니면 없음(0)
    //   객체: 키는 무조건 ParseString(첫 글자 건너뜀), ':' 가 아니면 null, 같은 키는 덮어씀. 배열: ',' 는 넘김, 없음이면 null
    //   문자열: 따옴표나 끝까지. 이스케이프 \" \\ \/ \b \f \n \r \t \uXXXX, 그 밖(\s 등)은 버림. 끝에서 \ 뒤가 없으면 거기까지
    //   숫자: 낱말에 '.' 이 없으면 int.TryParse, 있으면 float.TryParse (실패하면 0), 상자에 담은 Int32 / Single
    //   파일 끝에서 글자를 보려 하면 원래처럼 Convert.ToChar(-1) 이 OverflowException 을 던진다(잘린 파일 등). 맨 앞 BOM 하나는 건너뜀.
    // 검증: 맵 폴더의 .adofai 전부와 잘라 낸 조각들을 원래 해석기와 결과(형식, 값, 키 순서, 예외 종류)까지 비교.
    internal static class FastJson
    {
        internal static bool Enabled = true;
        internal static int MinLength = 16 * 1024;   // 작은 JSON(설정 등)은 원래대로
        internal static long Parses; internal static double LastMs;

        internal static void Install(Harmony h)
        {
            try
            {
                Type json = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Assembly-CSharp-firstpass") continue;
                    Type[] types; try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    json = types.FirstOrDefault(t => t.Name == "Json" && t.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null) != null);
                }
                var m = json == null ? null : json.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (m == null || m.ReturnType != typeof(object)) { Main.Entry.Logger.Log("[맵 파일 읽기] 빠른 해석기: 게임 코드 모양이 달라 끔"); return; }
                // 개발자용 대조: 원래 해석기 본체(Json 안의 Parser.Parse, 패치하지 않은 것)
                var parser = json.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(t => t.Name == "Parser");
                origParse = parser == null ? null : parser.GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
                h.Patch(m, prefix: new HarmonyMethod(typeof(FastJson), nameof(Prefix)));
                Main.Entry.Logger.Log("[맵 파일 읽기] 빠른 JSON 해석기 설치 (" + json.FullName + ")");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[맵 파일 읽기] 빠른 해석기 설치 실패: " + ex.Message); }
        }

        private static MethodInfo origParse;
        private static bool devChecked;

        public static bool Prefix(string __0, ref object __result)
        {
            if (!Enabled || __0 == null || __0.Length < MinLength) return true;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            __result = Parse(__0);   // 예외도 원래처럼 부른 쪽으로 간다
            LastMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Parses++;
            if (Edition.Dev && !devChecked && origParse != null && __0.Length >= 1000000) { devChecked = true; DevCompare(__0, __result); }   // 맵 파일(1MB 넘는 것)로
            return false;
        }

        // 개발자용: 이번 실행의 첫 큰 파일을 원래 해석기로도 읽어 결과 전체(형식, 값, 키 순서)를 비교
        private static void DevCompare(string text, object fast)
        {
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                object orig = origParse.Invoke(null, new object[] { text });
                double origMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                string diff = Diff(orig, fast, "$");
                Main.Entry.Logger.Log(string.Format("[맵 파일 읽기] 원래 해석기와 대조({0}만 글자): {1} | 빠른 해석기 {2:F0}ms, 원래 {3:F0}ms",
                    text.Length / 10000, diff == null ? "결과 전부 같음" : "다름 " + diff, LastMs, origMs));
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[맵 파일 읽기] 원래 해석기와 대조 실패: " + (ex.InnerException ?? ex).Message); }
        }

        private static string Diff(object a, object b, string path)
        {
            if (a == null || b == null) return a == null && b == null ? null : path + " null 한쪽만";
            if (a.GetType() != b.GetType()) return path + " 형식 " + a.GetType().Name + " / " + b.GetType().Name;
            var da = a as System.Collections.IDictionary;
            if (da != null)
            {
                var db = (System.Collections.IDictionary)b;
                if (da.Count != db.Count) return path + " 키 수";
                var ea = da.GetEnumerator(); var eb = db.GetEnumerator();
                while (ea.MoveNext() && eb.MoveNext())
                {
                    if (!Equals(ea.Key, eb.Key)) return path + " 키 순서 " + ea.Key + " / " + eb.Key;
                    var r = Diff(ea.Value, eb.Value, path + "." + ea.Key); if (r != null) return r;
                }
                return null;
            }
            var la = a as System.Collections.IList;
            if (la != null)
            {
                var lb = (System.Collections.IList)b;
                if (la.Count != lb.Count) return path + " 길이";
                for (int i = 0; i < la.Count; i++) { var r = Diff(la[i], lb[i], path + "[" + i + "]"); if (r != null) return r; }
                return null;
            }
            if (a is float) return BitConverter.ToInt32(BitConverter.GetBytes((float)a), 0) == BitConverter.ToInt32(BitConverter.GetBytes((float)b), 0) ? null : path + " 실수";
            if (a is string) return string.Equals((string)a, (string)b, StringComparison.Ordinal) ? null : path + " 문자열";
            return a.Equals(b) ? null : path + " 값";
        }

        internal static object Parse(string json) { return FastJsonParser.Parse(json); }
    }
}
