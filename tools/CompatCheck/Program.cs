// 우리 DLL 이 참조하는 게임(Assembly-CSharp 등) 필드·메서드가 설치된 게임에 실제로 있는지 이름 수준으로 전수 확인한다.
// 사용: CompatCheck <StutterFix.dll> <Managed 폴더> [어셈블리접두사,...]
// 못 찾으면 그 DLL 이 해당 게임 버전에서 JIT 때 MissingFieldException/MissingMethodException 을 낼 수 있다.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var mod = args[0]; var managed = args[1];
var games = new[] { "Assembly-CSharp", "Assembly-CSharp-firstpass" };
if (args.Length > 2) games = args[2].Split(',');

// 게임 어셈블리의 타입 -> (필드 이름 집합, 메서드 이름 -> 매개변수 개수 집합)
var types = new Dictionary<string, (HashSet<string> fields, Dictionary<string, HashSet<int>> methods)>();
foreach (var g in games)
{
    var path = Path.Combine(managed, g + ".dll"); if (!File.Exists(path)) continue;
    using var pe = new PEReader(File.OpenRead(path)); var md = pe.GetMetadataReader();
    foreach (var th in md.TypeDefinitions)
    {
        var td = md.GetTypeDefinition(th);
        string Full(TypeDefinition t) { var ns = md.GetString(t.Namespace); var nm = md.GetString(t.Name); var dt = t.GetDeclaringType(); return dt.IsNil ? (ns.Length > 0 ? ns + "." + nm : nm) : Full(md.GetTypeDefinition(dt)) + "+" + nm; }
        var fields = new HashSet<string>(); foreach (var fh in td.GetFields()) fields.Add(md.GetString(md.GetFieldDefinition(fh).Name));
        var methods = new Dictionary<string, HashSet<int>>();
        foreach (var mh in td.GetMethods()) { var m = md.GetMethodDefinition(mh); var n = md.GetString(m.Name); if (!methods.TryGetValue(n, out var set)) methods[n] = set = new HashSet<int>(); set.Add(m.GetParameters().Count); }
        types[Full(td)] = (fields, methods);
    }
}

using var ours = new PEReader(File.OpenRead(mod)); var om = ours.GetMetadataReader();
string TypeName(EntityHandle h)
{
    if (h.Kind == HandleKind.TypeReference)
    {
        var tr = om.GetTypeReference((TypeReferenceHandle)h);
        string ns = om.GetString(tr.Namespace), nm = om.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference) { var outer = TypeName(tr.ResolutionScope); return outer == null ? null : outer + "+" + nm; }
        if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference)
        {
            var an = om.GetString(om.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name);
            if (!games.Contains(an)) return null;
        }
        return ns.Length > 0 ? ns + "." + nm : nm;
    }
    return null;
}
var missing = new SortedSet<string>(); int checkedN = 0;
foreach (var mrh in om.MemberReferences)
{
    var mr = om.GetMemberReference(mrh);
    var tn = TypeName(mr.Parent); if (tn == null) continue;
    string name = om.GetString(mr.Name);
    if (!types.TryGetValue(tn, out var info)) { missing.Add($"TYPE  {tn} (멤버 {name})"); continue; }
    var sig = om.GetBlobReader(mr.Signature); var hdr = sig.ReadSignatureHeader();
    checkedN++;
    if (hdr.Kind == SignatureKind.Field) { if (!info.fields.Contains(name)) missing.Add($"FIELD {tn}.{name}"); }
    else if (hdr.Kind == SignatureKind.Method)
    {
        if (hdr.IsGeneric) sig.ReadCompressedInteger();
        int pc = sig.ReadCompressedInteger();
        if (name == ".ctor" || name == ".cctor") { if (!info.methods.ContainsKey(name) || !info.methods[name].Contains(pc)) missing.Add($"CTOR  {tn}({pc})"); continue; }
        if (!info.methods.TryGetValue(name, out var set) || !set.Contains(pc)) missing.Add($"METHOD {tn}.{name}/{pc}" + (set != null ? " (있는 개수: " + string.Join(",", set) + ")" : " (이름 없음)"));
    }
}
Console.WriteLine($"확인한 게임 멤버 참조 {checkedN}개, 못 찾은 것 {missing.Count}개");
foreach (var m in missing) Console.WriteLine("  " + m);
