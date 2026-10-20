using System.Reflection;
using System.Reflection.Emit;

// Read metadata/IL only. No Unity methods invoked and no game process started.
string managed = args[0];
AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
    var p = Path.Combine(managed, new AssemblyName(e.Name).Name + ".dll");
    return File.Exists(p) ? Assembly.LoadFrom(p) : null;
};
var asm = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
Console.WriteLine("Assembly-CSharp SHA256=" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(asm.Location))));
var ops = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
void Dump(MethodBase m) {
    Console.WriteLine("\nMETHOD " + m.DeclaringType + "::" + m + " convention=" + m.CallingConvention);
    var il = m.GetMethodBody()?.GetILAsByteArray(); if (il == null) return;
    for (int i = 0; i < il.Length;) {
        int at = i; short code = il[i++]; if (code == 0xfe) code = (short)(0xfe00 | il[i++]);
        var op = ops[code]; int n = op.OperandType switch {
            OperandType.InlineNone => 0, OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
            OperandType.InlineVar => 2, OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i), _ => 4
        };
        string value = n == 0 ? "" : Convert.ToHexString(il.AsSpan(i, n));
        if (op.OperandType == OperandType.ShortInlineR) value = BitConverter.ToSingle(il, i).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (op.OperandType == OperandType.InlineI) value = BitConverter.ToInt32(il, i).ToString();
        if (op.OperandType == OperandType.InlineString) value = m.Module.ResolveString(BitConverter.ToInt32(il, i));
        if (op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            try { var member = m.Module.ResolveMember(BitConverter.ToInt32(il, i)); value = member.DeclaringType + "::" + member; } catch { }
        Console.WriteLine($"{at:X4} {op.Name} {value}"); i += n;
    }
}
const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
foreach (var (name, method) in new[] { ("scrVisualDecoration", "SetSprite"), ("scrVisualDecoration", "SetTile"), ("scrVisualDecoration", "SetBlendMode"), ("scrVisualDecoration", "UpdateShader"), ("scrVisualDecoration", "LogicUpdate"), ("scrDecorationManager", "UpdateDecorationTilingLateUpdate"), ("scrDecorationManager", "UpdateDecorationTiling"), ("scrDecorationManager", "LateUpdate"), ("scrDecorationManager", ".cctor"), ("RDUtils", "SetPointSamplingVariant") }) {
    var t = asm.GetType(name, true);
    foreach (var m in t.GetMethods(flags).Cast<MethodBase>().Concat(t.GetConstructors(flags)).Where(m => m.Name == method)) Dump(m);
}
foreach (var (name, method) in new[] { ("scrDecoration", "LogicUpdate"), ("scrDecorationManager", "Awake"), ("ffxMoveDecorationsPlus", "StartEffect") })
    foreach (var m in asm.GetType(name,true).GetMethods(flags).Where(m => m.Name == method)) Dump(m);
foreach (var dll in args.Skip(1)) {
    var context = new System.Runtime.Loader.AssemblyLoadContext(Guid.NewGuid().ToString(),true);
    context.Resolving += (_,name) => {
        var path = Path.Combine(managed,name.Name+".dll");
        if (!File.Exists(path)) path = Path.Combine(managed,"UnityModManager",name.Name+".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    };
    var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
    bool expected = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(dll))) == "Release";
    foreach (string name in new[]{"MatShare","MatShareCompare","MatSharePolicy"}) {
        bool present = assembly.GetType("StutterFix."+name) != null;
        Console.WriteLine($"EDITION {dll} {name} present={present} expected={expected}");
        if (present != expected) throw new Exception("matshare edition mismatch");
    }
    context.Unload();
}

