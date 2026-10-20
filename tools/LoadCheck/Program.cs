using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

// StutterFix.dll 불러오기 미리 확인 (게임을 켜지 않고).
// 모드 IL 에서 AccessTools.FieldRefAccess<클래스, 형식>("필드 이름") 호출을 모두 찾아, 게임 어셈블리에 그 필드가 있는지와
// Harmony 규칙(참조 형식은 형식이 필드 형식을 담을 수 있어야, 값 형식은 같아야)에 맞는지 본다.
// 정적 필드 초기값에서 이게 틀리면 그 클래스의 정적 초기화가 실패해 모드 전체가 안 켜진다(2026-09-26 실제로 그랬다).
// (Harmony 자체는 .NET 8 에서 돌지 않아 실제로 불러 볼 수는 없다.)
class P
{
    static int Main(string[] a)
    {
        string managed = @"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed";
        string[] dirs = { managed, Path.Combine(managed, "UnityModManager") };
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string n = new AssemblyName(e.Name).Name + ".dll";
            foreach (var d in dirs) { var p = Path.Combine(d, n); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        var one = new Dictionary<short, OpCode>();
        foreach (var f in typeof(OpCodes).GetFields()) { var o = (OpCode)f.GetValue(null); one[o.Value] = o; }

        var asm = Assembly.LoadFrom(a[0]);
        var mod = asm.ManifestModule;
        if (a.Length > 1 && (a[1] == "--bake" || a[1] == "--no-bake"))
        {
            bool want = a[1] == "--bake";
            string[] bakeTypes = { "StutterFix.DecoBake", "StutterFix.DecoBakeCore", "StutterFix.DecoBakeCore+AdoptState" };
            bool have = bakeTypes.All(t => asm.GetType(t, false) != null);
            Console.WriteLine("DecoBake 개발 타입: " + have + ", 기대: " + want);
            if (have != want) return 3;
            if (!want && bakeTypes.Any(t => asm.GetType(t, false) != null)) return 3;
            bool haveSetter = asm.GetType("StutterFix.DecoAnim", true).GetMethod("BakeWrite", BindingFlags.Static | BindingFlags.NonPublic) != null;
            Console.WriteLine("DecoAnim.BakeWrite: " + haveSetter + ", 기대: " + want);
            if (haveSetter != want) return 3;
            if (want)
            {
                var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
                var levelEvent = game.GetType("ADOFAI.LevelEvent", true);
                var floorList = typeof(List<>).MakeGenericType(game.GetType("scrFloor", true));
                var eventList = typeof(List<>).MakeGenericType(levelEvent);
                CheckHook(game.GetType("scrDecoration", true), "Setup", false, new[] { levelEvent, typeof(bool).MakeByRefType(), typeof(bool) });
                CheckHook(game.GetType("scnGame", true), "PrepVfx", true, new[] { floorList, typeof(int), eventList, typeof(bool) });
            }
        }
        Type[] types; try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
        int checkedN = 0, bad = 0, dynamicN = 0;
        foreach (var t in types)
        {
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            foreach (var m in methods)
            {
                byte[] il; try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch { continue; }
                if (il == null) continue;
                string lastStr = null;
                int i = 0;
                while (i < il.Length)
                {
                    short v = il[i++]; if (v == 0xFE) v = (short)(0xFE00 | il[i++]);
                    if (!one.TryGetValue(v, out var op)) break;
                    int sz = op.OperandType switch { OperandType.InlineNone => 0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1, OperandType.InlineVar => 2, OperandType.InlineI8 or OperandType.InlineR => 8, OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i), _ => 4 };
                    if (op == OpCodes.Ldstr) lastStr = mod.ResolveString(BitConverter.ToInt32(il, i));
                    else if (op == OpCodes.Call)
                    {
                        MethodBase callee = null;
                        try { callee = mod.ResolveMethod(BitConverter.ToInt32(il, i), t.IsGenericType ? t.GetGenericArguments() : null, m.IsGenericMethod ? m.GetGenericArguments() : null); } catch { }
                        if (callee is MethodInfo mi && mi.Name == "FieldRefAccess" && mi.DeclaringType?.Name == "AccessTools" && mi.IsGenericMethod)
                        {
                            var ga = mi.GetGenericArguments();
                            var ps = mi.GetParameters();
                            if (ga.Length == 2 && ps.Length == 1 && ps[0].ParameterType == typeof(string))
                            {
                                checkedN++;
                                if (lastStr == null) { dynamicN++; lastStr = null; i += sz; continue; }   // 이름을 변수로 받는 도우미(부르는 쪽이 이름을 줌)
                                string why = Check(ga[0], ga[1], lastStr);
                                if (why != null) { bad++; Console.WriteLine($"틀림: {t.Name}.{m.Name}: FieldRefAccess<{ga[0].Name}, {ga[1]}>(\"{lastStr}\") -> {why}"); }
                            }
                            else dynamicN++;
                        }
                        lastStr = null;
                    }
                    else if (op != OpCodes.Nop) { if (op.OperandType != OperandType.InlineNone || op == OpCodes.Ret) { } }
                    i += sz;
                }
            }
        }
        Console.WriteLine($"{Path.GetFileName(Path.GetDirectoryName(a[0]))}/{Path.GetFileName(a[0])}: FieldRefAccess(이름) {checkedN}곳 확인, 틀림 {bad}, 이름을 실행 중에 정하는 곳 {dynamicN}(실행 중 확인)");
        return bad == 0 ? 0 : 2;
    }

    // Harmony AccessTools.FieldRefAccess<T, F>(name) 과 같은 규칙: T 와 부모에서 이름으로 필드를 찾고, 형식 확인
    static void CheckHook(Type type, string name, bool isStatic, Type[] parameters)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == name && !m.IsGenericMethod && m.IsStatic == isStatic && m.ReturnType == typeof(void)
                && m.CallingConvention == (isStatic ? CallingConventions.Standard : CallingConventions.Standard | CallingConventions.HasThis)
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters)).ToArray();
        Console.WriteLine("정적 후크 계약 " + type.Name + "." + name + " 정확일치=" + methods.Length);
        if (methods.Length != 1) throw new InvalidOperationException("후크 모호/없음");
    }

    static string Check(Type owner, Type fieldType, string name)
    {
        if (name == null) return "필드 이름을 IL 에서 못 찾음";
        FieldInfo f = null;
        for (var t = owner; t != null && f == null; t = t.BaseType)
            f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (f == null) return "게임에 그 필드가 없음";
        if (fieldType.IsValueType ? f.FieldType != fieldType : !fieldType.IsAssignableFrom(f.FieldType)) return "형식이 다름 (실제: " + f.FieldType + ")";
        return null;
    }
}
