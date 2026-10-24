#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace StutterFix
{
    // (자동 시험 monodump <파일>) 진단 전용: PerfView 기록의 이름 없는 managed 주소(!0x1a…)를 메서드 이름으로 바꾸기 위한 표.
    // 게임 어셈블리·모드·DOTween·UnityEngine 의 메서드마다 MethodHandle.GetFunctionPointer() 로 JIT 코드 시작 주소를 모은다
    // (이미 JIT 된 것은 주소만, 아직 안 된 것은 이때 JIT 된다 -> 기록이 끝난 뒤 같은 게임 프로세스에서만 부를 것).
    // 파일 한 줄: <16진 주소> <어셈블리>:<타입>.<메서드>. 주소는 이 게임 프로세스에서만 유효하다.
    internal static class MonoDump
    {
        [DllImport("mono-2.0-bdwgc", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_pmip(IntPtr ip);
        [DllImport("mono-2.0-bdwgc", CallingConvention = CallingConvention.Cdecl)] private static extern void mono_free(IntPtr p);
        // monosyms <주소파일> <출력파일> : 주소파일(한 줄에 16진 주소 하나)이 생길 때까지 최대 600초 기다린 뒤 mono_pmip 로 이름을 구해 "주소 이름" 으로 쓴다.
        // 같은 게임 프로세스에서 JIT 된 주소만 풀린다(기록 직후 이 게임이 살아 있는 동안).
        internal static bool Syms(string input, string output, float waited, out string result)
        {
            result = null;
            if (!File.Exists(input)) { if (waited > 600) throw new Exception("monosyms: 주소 파일이 안 생김 " + input); return false; }
            var sb = new StringBuilder(); int ok = 0, fail = 0;
            foreach (var line in File.ReadAllLines(input))
            {
                string t = line.Trim(); if (t.Length == 0) continue;
                long a; if (!long.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out a)) continue;
                string name = null;
                try { var p = mono_pmip(new IntPtr(a)); if (p != IntPtr.Zero) { name = Marshal.PtrToStringAnsi(p); mono_free(p); } } catch { }
                if (string.IsNullOrEmpty(name)) { fail++; continue; }
                ok++; sb.Append(t).Append(' ').Append(name.Replace((char)10, ' ')).Append((char)10);
            }
            File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
            result = "monosyms resolved=" + ok + " unresolved=" + fail + " out=" + output;
            return true;
        }

        internal static string Run(string path)
        {
            var rows = new List<KeyValuePair<long, string>>(); int skipped = 0, errors = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name;
                if (asm.IsDynamic || an.StartsWith("System") || an == "mscorlib" || an.StartsWith("Mono.") || an.Contains("Harmony") || an.StartsWith("Microsoft")) continue;
                Type[] types; try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t.ContainsGenericParameters) { skipped++; continue; }
                    MethodBase[] ms;
                    try { ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)).ToArray(); } catch { errors++; continue; }
                    foreach (var m in ms)
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters || m.IsGenericMethod) { skipped++; continue; }
                        try
                        {
                            var body = m.GetMethodBody(); if (body == null) { skipped++; continue; }
                            long p = m.MethodHandle.GetFunctionPointer().ToInt64();
                            if (p != 0) rows.Add(new KeyValuePair<long, string>(p, an + ":" + (t.DeclaringType != null ? t.DeclaringType.Name + "/" : "") + t.Name + "." + m.Name));
                        }
                        catch { errors++; }
                    }
                }
            }
            rows.Sort((a, b) => a.Key.CompareTo(b.Key));
            var sb = new StringBuilder(rows.Count * 60);
            foreach (var r in rows) sb.Append(r.Key.ToString("x")).Append(' ').Append(r.Value).Append('\n');
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return "monodump rows=" + rows.Count + " skipped=" + skipped + " errors=" + errors + " path=" + path;
        }
    }
}
#endif
