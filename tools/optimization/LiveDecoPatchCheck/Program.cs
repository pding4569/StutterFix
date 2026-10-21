using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using StutterFix;

// No game methods or Harmony operations run in this checker.
string game = args[0], managed = Path.GetDirectoryName(game)!;
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    string name = new AssemblyName(e.Name).Name + ".dll";
    foreach (var dir in new[] { managed, Path.Combine(managed, "UnityModManager") })
    {
        string path = Path.Combine(dir, name);
        if (File.Exists(path)) return Assembly.LoadFrom(path);
    }
    return null;
};
int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
var asm = Assembly.LoadFrom(game);
Type Get(string name) => asm.GetType(name, true)!;
var core = Assembly.LoadFrom(Path.Combine(managed, "UnityEngine.CoreModule.dll"));
var decoration = Get("scrDecoration"); var visual = Get("scrVisualDecoration");
var plan = LiveDecoPatchPlan.Build(decoration, visual, Get("scrParallax"), core.GetType("UnityEngine.Vector2", true)!, core.GetType("UnityEngine.Color", true)!);
Console.WriteLine("Assembly-CSharp SHA256=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(game))));
Check(plan.AbstractDeclarations.Count == 2, "both abstract structural declarations detected");
Check(plan.AbstractDeclarations.Select(m => m.Name).Order().SequenceEqual(new[] { "SetDepth", "SetVisible" }), "expected abstract declarations");
Check(plan.Entries.Select(e => e.Method).Distinct().Count() == plan.Entries.Count, "unique hooks");
Check(plan.Entries.Count(e => e.Prefix != "StructuralPrefix") == 8, "all eight state-write hooks");
using var stream = File.OpenRead(game); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
foreach (var method in plan.AbstractDeclarations)
{
    var def = md.GetMethodDefinition(System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(method.MetadataToken & 0xffffff));
    Check((def.Attributes & MethodAttributes.Abstract) != 0 && def.RelativeVirtualAddress == 0, "abstract is bodyless in actual PE");
    Check(!plan.Entries.Any(e => e.Method == method), "abstract never installed");
    var implementation = plan.Entries.Single(e => e.Method.DeclaringType == visual && e.Method.Name == method.Name);
    Check(implementation.Prefix == "StructuralPrefix" && implementation.Method.GetBaseDefinition() == method, "visual override releases ownership");
    Console.WriteLine($"SKIP {method.DeclaringType!.Name}.{method.Name} abstract=True RVA=0; concrete_override={implementation.Method.DeclaringType!.Name}");
}
foreach (var entry in plan.Entries)
{
    var def = md.GetMethodDefinition(System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(entry.Method.MetadataToken & 0xffffff));
    Check(!entry.Method.IsAbstract && entry.Method.GetMethodBody() != null && def.RelativeVirtualAddress != 0, "patch has managed body");
    Console.WriteLine($"PATCH {entry.Method.DeclaringType!.Name}.{entry.Method.Name} args={entry.Method.GetParameters().Length} RVA=0x{def.RelativeVirtualAddress:X} prefix={entry.Prefix}");
}
Check(plan.Entries.Count(e => e.Method.Name == "SetSprite") == 2, "both SetSprite overloads retained");
foreach (string name in new[] { "Setup", "SetDepth", "SetTile", "SetBlendMode", "SetMaskingType", "SetParallax", "SetVisible", "UpdateHitboxState" })
    Check(plan.Entries.Any(e => e.Method.Name == name && e.Prefix == "StructuralPrefix"), "structural coverage " + name);
// A wrong required target must fail before callers can install any entry.
bool rejected = false;
try { LiveDecoPatchPlan.Build(decoration, decoration, Get("scrParallax"), core.GetType("UnityEngine.Vector2", true)!, core.GetType("UnityEngine.Color", true)!); }
catch (InvalidOperationException e) { rejected = e.Message == "patch-target-no-managed-body scrDecoration.ApplyColor"; }
Check(rejected, "required abstract method fails with target name");
Console.WriteLine($"LiveDeco actual-DLL patch plan: {checks} checks passed; targets={plan.Entries.Count}; skipped_abstract={plan.AbstractDeclarations.Count}; no game/native/Harmony execution");
