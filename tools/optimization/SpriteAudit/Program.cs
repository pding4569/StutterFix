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
foreach (var (name, method) in new[] { ("TextureManager+CustomSprite", ".ctor"), ("TextureManager", "LoadNewSprite"), ("TextureManager", "GetOrAddSprite"), ("scrVisualDecoration", "SetBlendMode") }) {
    var t = asm.GetType(name, true);
    foreach (var m in t.GetMethods(flags).Cast<MethodBase>().Concat(t.GetConstructors(flags)).Where(m => m.Name == method)) Dump(m);
}
var core = Assembly.LoadFrom(Path.Combine(managed, "UnityEngine.CoreModule.dll"));
var sprite = core.GetType("UnityEngine.Sprite", true);
var meshType = core.GetType("UnityEngine.SpriteMeshType", true);
foreach (var v in Enum.GetValues(meshType)) Console.WriteLine("ENUM SpriteMeshType " + v + "=" + Convert.ToInt32(v));
foreach (var m in sprite.GetMethods(flags).Where(m => m.Name == "Create")) {
    Console.WriteLine("\nOVERLOAD " + m);
    foreach (var p in m.GetParameters()) Console.WriteLine($"param {p.Name} {p.ParameterType} default={(p.HasDefaultValue ? p.DefaultValue : "<none>")}");
    if (m.GetParameters().Length <= 7) Dump(m);
}
if (args.Length > 1) {
    foreach (var dll in args.Skip(1)) {
        // Separate collectible context prevents the three equal assembly identities from aliasing.
        var context = new System.Runtime.Loader.AssemblyLoadContext(Path.GetFileName(Path.GetDirectoryName(dll)), true);
        context.Resolving += (_, name) => {
            string p = Path.Combine(managed, name.Name + ".dll");
            if (!File.Exists(p)) p = Path.Combine(managed, "UnityModManager", name.Name + ".dll");
            return File.Exists(p) ? context.LoadFromAssemblyPath(p) : null;
        };
        var a = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
        bool player = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(dll))) == "Player";
        foreach (string type in new[] { "GpuSplit", "GpuSplitPolicy", "GpuSplitGroup", "GpuSplitLedger`1", "GpuProbe" }) {
            bool present = a.GetType("StutterFix." + type) != null;
            Console.WriteLine($"EDITION {dll} {type} present={present}");
            if (present == player) throw new Exception("diagnostic edition mismatch");
        }
        context.Unload();
    }
}
