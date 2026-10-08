using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace StutterFix
{
    // 모드 함수를 게임을 켤 때 미리 컴파일(JIT)해 둔다.
    //
    // 측정 (HELLO 2026 곡 시작 5.1초, 개발자용): 처음 쓰는 장식 이동 효과 하나(장식 1개)가 8ms(대상 찾기 3.6ms), 처음 쓰는 타일 이동 효과 하나(타일 1개)가 6ms.
    // Mono 는 함수를 처음 부를 때 컴파일하므로, 효과가 처음 나오는 곡 초반 프레임이 그 값을 낸다. 모드 어셈블리의 일반(제네릭이 아닌) 함수를
    // 모드를 켤 때 MethodHandle.GetFunctionPointer 로 컴파일해 둔다. 하는 일은 같고 컴파일 시점만 앞당긴다.
    internal static class JitWarm
    {
        internal static void Run()
        {
            var sw = Stopwatch.StartNew();
            int ok = 0, fail = 0;
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type[] types;
            try { types = typeof(JitWarm).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            foreach (var t in types)
            {
                if (t == null || t.ContainsGenericParameters) continue;
                // Mono GetFunctionPointer also initializes this experimental type.
                // Keep its FieldRefs/buffers lazy while the feature is OFF.
                if (t == typeof(FrameGenRuntime) || t.DeclaringType == typeof(FrameGenRuntime)) continue;
                MethodInfo[] ms;
                try { ms = t.GetMethods(All); } catch { continue; }
                foreach (var m in ms)
                {
                    if (m.IsAbstract || m.ContainsGenericParameters || (m.MethodImplementationFlags & MethodImplAttributes.InternalCall) != 0 || m.GetMethodBody() == null) continue;
                    try { m.MethodHandle.GetFunctionPointer(); ok++; } catch { fail++; }   // Mono 는 PrepareMethod 가 아무것도 안 했다(1843개 23ms, 첫 효과 비용 그대로). 함수 포인터를 얻으면 컴파일한다
                }
            }
            long modMs = sw.ElapsedMilliseconds;
            // 게임 쪽: 곡 중에 처음 불리는 효과(ffx*)·장식·타일·행성·지휘자 함수. 컴파일만 하고 실행하지 않는다.
            int gok = 0;
            try
            {
                Type[] gt;
                try { gt = typeof(scrFloor).Assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { gt = e.Types; }
                foreach (var t in gt)
                {
                    if (t == null || t.ContainsGenericParameters) continue;
                    string n = t.Name;
                    bool want = typeof(ffxPlusBase).IsAssignableFrom(t) || n.StartsWith("ffx") || n.StartsWith("scrDecoration") || n.EndsWith("Decoration") || n == "scrFloor" || n == "scrPlanet"
                        || n == "scrConductor" || n == "scrController" || n == "scrVfxPlus" || n == "scnGame" || n == "FloorMesh" || n == "FloorRenderer" || n == "scrHitTextMesh" || n == "scrHitTextManager" || n == "scrCamera"
                        || (t.DeclaringType != null && (typeof(ffxPlusBase).IsAssignableFrom(t.DeclaringType) || t.DeclaringType.Name == "scrFloor" || t.DeclaringType.Name == "scrPlanet" || t.DeclaringType.Name == "scrController"));
                    if (!want) continue;
                    MethodInfo[] ms;
                    try { ms = t.GetMethods(All); } catch { continue; }
                    foreach (var m in ms)
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters || (m.MethodImplementationFlags & MethodImplAttributes.InternalCall) != 0 || m.GetMethodBody() == null) continue;
                        try { m.MethodHandle.GetFunctionPointer(); gok++; } catch { fail++; }
                    }
                }
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[미리 컴파일] 게임 함수 실패: " + ex.Message); }
            Main.Entry.Logger.Log("[미리 컴파일] 모드 함수 " + ok + "개 " + modMs + "ms, 게임 함수(효과·장식·타일·행성·지휘자) " + gok + "개 " + (sw.ElapsedMilliseconds - modMs) + "ms" + (fail > 0 ? ", 실패 " + fail : ""));
        }
    }
}
