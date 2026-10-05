// IL 스캐너 (디컴파일러 대신). 게임 DLL 의 IL 을 직접 읽어 호출 관계를 찾는다.
//   ILScan <dll> <호출이름>                 그 이름을 부르는(call/callvirt/newobj/ldftn) 곳 전부
//   TYPES=<형식> ILScan <dll> ZZZ           형식 이름에 <형식> 이 들어간 형식의 필드/메서드 목록
//   METHOD=<형식> ILScan <dll> ZZZ          그 형식의 메서드마다 부르는 것과 정적 필드(ldsfld/stsfld/ldsflda)
// 인스턴스 필드(ldfld)는 METHOD 에서 FIELDS=1 일 때만 적는다. 제네릭 호출은 "제네릭 X::Y" 로 풀린다.
// 예전에는 %TEMP%\ilscan 에 있었는데 윈도우 임시 파일 정리에 지워져서 저장소에 둔다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static class Program
{
    static MetadataReader md;
    static PEReader pe;

    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Length < 2) { Console.WriteLine("ILScan <dll> <호출이름>   (TYPES=형식 / METHOD=형식 [FIELDS=1])"); return 1; }
        using var fs = File.OpenRead(args[0]);
        pe = new PEReader(fs);
        md = pe.GetMetadataReader();
        string types = Environment.GetEnvironmentVariable("TYPES");
        string method = Environment.GetEnvironmentVariable("METHOD");
        bool fields = Environment.GetEnvironmentVariable("FIELDS") == "1";
        if (!string.IsNullOrEmpty(types)) { ListTypes(types); return 0; }
        if (!string.IsNullOrEmpty(method)) { ListMethods(method, fields); return 0; }
        FindCallers(args[1]);
        return 0;
    }

    static string TypeName(TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        string n = md.GetString(t.Name);
        string ns = md.GetString(t.Namespace);
        var decl = t.GetDeclaringType();
        if (!decl.IsNil) return TypeName(decl) + "+" + n;
        return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
    }

    static string EntityTypeName(EntityHandle h)
    {
        switch (h.Kind)
        {
            case HandleKind.TypeDefinition: return TypeName((TypeDefinitionHandle)h);
            case HandleKind.TypeReference:
            {
                var r = md.GetTypeReference((TypeReferenceHandle)h);
                string ns = md.GetString(r.Namespace), n = md.GetString(r.Name);
                if (r.ResolutionScope.Kind == HandleKind.TypeReference) return EntityTypeName((TypeReferenceHandle)r.ResolutionScope) + "+" + n;
                return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
            }
            case HandleKind.TypeSpecification:
            {
                // 제네릭 형식: 서명 첫 부분의 형식만 읽는다
                var s = md.GetTypeSpecification((TypeSpecificationHandle)h);
                var br = md.GetBlobReader(s.Signature);
                var code = br.ReadSignatureTypeCode();
                if (code == SignatureTypeCode.GenericTypeInstance)
                {
                    br.ReadSignatureTypeCode();
                    var inner = br.ReadTypeHandle();
                    return "제네릭 " + EntityTypeName(inner);
                }
                return "형식명세";
            }
        }
        return "?";
    }

    // 호출 대상 (형식::이름)
    static string MemberName(EntityHandle h)
    {
        switch (h.Kind)
        {
            case HandleKind.MethodDefinition:
            {
                var m = md.GetMethodDefinition((MethodDefinitionHandle)h);
                return TypeName(m.GetDeclaringType()) + "::" + md.GetString(m.Name);
            }
            case HandleKind.MemberReference:
            {
                var r = md.GetMemberReference((MemberReferenceHandle)h);
                return EntityTypeName(r.Parent) + "::" + md.GetString(r.Name);
            }
            case HandleKind.MethodSpecification:
            {
                var s = md.GetMethodSpecification((MethodSpecificationHandle)h);
                return "제네릭 " + MemberName(s.Method);
            }
            case HandleKind.FieldDefinition:
            {
                var f = md.GetFieldDefinition((FieldDefinitionHandle)h);
                return TypeName(f.GetDeclaringType()) + "::" + md.GetString(f.Name);
            }
        }
        return "?";
    }

    static string ShortName(EntityHandle h)
    {
        string full = MemberName(h);
        int i = full.LastIndexOf("::", StringComparison.Ordinal);
        return i >= 0 ? full.Substring(i + 2) : full;
    }

    // IL 을 훑으며 (opcode, 토큰) 을 돌려준다. 토큰이 없는 명령은 건너뛴다.
    static IEnumerable<(ILOpCode op, int token)> Tokens(MethodDefinition m)
    {
        if (m.RelativeVirtualAddress == 0) yield break;
        var body = pe.GetMethodBody(m.RelativeVirtualAddress);
        var r = body.GetILReader();
        while (r.RemainingBytes > 0)
        {
            ILOpCode op;
            byte b = r.ReadByte();
            if (b == 0xFE) op = (ILOpCode)(0xFE00 | r.ReadByte()); else op = (ILOpCode)b;
            switch (OperandSize(op, ref r, out bool isToken))
            {
                case -1: yield break;
                case 0: break;
                case 1: r.ReadByte(); break;
                case 2: r.ReadInt16(); break;
                case 4: { int v = r.ReadInt32(); if (isToken) yield return (op, v); break; }
                case 8: r.ReadInt64(); break;
            }
        }
    }

    static int OperandSize(ILOpCode op, ref BlobReader r, out bool isToken)
    {
        isToken = false;
        switch (op)
        {
            case ILOpCode.Switch: { int n = r.ReadInt32(); for (int i = 0; i < n; i++) r.ReadInt32(); return 0; }
            case ILOpCode.Ldc_i8: case ILOpCode.Ldc_r8: return 8;
            case ILOpCode.Ldc_r4: case ILOpCode.Ldc_i4: case ILOpCode.Br: case ILOpCode.Brfalse: case ILOpCode.Brtrue: case ILOpCode.Beq: case ILOpCode.Bge:
            case ILOpCode.Bgt: case ILOpCode.Ble: case ILOpCode.Blt: case ILOpCode.Bne_un: case ILOpCode.Bge_un: case ILOpCode.Bgt_un: case ILOpCode.Ble_un:
            case ILOpCode.Blt_un: case ILOpCode.Leave: return 4;
            case ILOpCode.Ldc_i4_s: case ILOpCode.Br_s: case ILOpCode.Brfalse_s: case ILOpCode.Brtrue_s: case ILOpCode.Beq_s: case ILOpCode.Bge_s: case ILOpCode.Bgt_s:
            case ILOpCode.Ble_s: case ILOpCode.Blt_s: case ILOpCode.Bne_un_s: case ILOpCode.Bge_un_s: case ILOpCode.Bgt_un_s: case ILOpCode.Ble_un_s: case ILOpCode.Blt_un_s:
            case ILOpCode.Leave_s: case ILOpCode.Ldarg_s: case ILOpCode.Ldarga_s: case ILOpCode.Starg_s: case ILOpCode.Ldloc_s: case ILOpCode.Ldloca_s: case ILOpCode.Stloc_s:
            case ILOpCode.Unaligned: return 1;
            case ILOpCode.Ldarg: case ILOpCode.Ldarga: case ILOpCode.Starg: case ILOpCode.Ldloc: case ILOpCode.Ldloca: case ILOpCode.Stloc: return 2;
            case ILOpCode.Call: case ILOpCode.Callvirt: case ILOpCode.Newobj: case ILOpCode.Ldftn: case ILOpCode.Ldvirtftn: case ILOpCode.Jmp:
            case ILOpCode.Ldfld: case ILOpCode.Ldflda: case ILOpCode.Stfld: case ILOpCode.Ldsfld: case ILOpCode.Ldsflda: case ILOpCode.Stsfld:
            case ILOpCode.Ldtoken: case ILOpCode.Ldstr: case ILOpCode.Calli:
            case ILOpCode.Box: case ILOpCode.Unbox: case ILOpCode.Unbox_any: case ILOpCode.Castclass: case ILOpCode.Isinst: case ILOpCode.Newarr: case ILOpCode.Ldelema:
            case ILOpCode.Ldelem: case ILOpCode.Stelem: case ILOpCode.Ldobj: case ILOpCode.Stobj: case ILOpCode.Cpobj: case ILOpCode.Initobj: case ILOpCode.Sizeof:
            case ILOpCode.Mkrefany: case ILOpCode.Refanyval: case ILOpCode.Constrained:
                isToken = true; return 4;
            default: return 0;
        }
    }

    static bool IsCall(ILOpCode op) => op == ILOpCode.Call || op == ILOpCode.Callvirt || op == ILOpCode.Newobj || op == ILOpCode.Ldftn || op == ILOpCode.Ldvirtftn;

    static EntityHandle Handle(int token) => MetadataTokens.EntityHandle(token);

    static string MethodTitle(MethodDefinitionHandle h)
    {
        var m = md.GetMethodDefinition(h);
        return TypeName(m.GetDeclaringType()) + "::" + md.GetString(m.Name);
    }

    static void FindCallers(string name)
    {
        int hits = 0;
        foreach (var mh in md.MethodDefinitions)
        {
            var m = md.GetMethodDefinition(mh);
            var seen = new HashSet<string>();
            foreach (var (op, tok) in Tokens(m))
            {
                if (!IsCall(op)) continue;
                var h = Handle(tok);
                if (h.Kind != HandleKind.MethodDefinition && h.Kind != HandleKind.MemberReference && h.Kind != HandleKind.MethodSpecification) continue;
                if (ShortName(h) != name) continue;
                string target = MemberName(h);
                if (!seen.Add(target)) continue;
                Console.WriteLine(MethodTitle(mh) + "  ->  " + target);
                hits++;
            }
        }
        Console.WriteLine("(" + hits + "곳)");
    }

    static IEnumerable<TypeDefinitionHandle> MatchTypes(string part)
    {
        foreach (var th in md.TypeDefinitions)
        {
            string n = TypeName(th);
            if (n == part || n.EndsWith("." + part) || n.EndsWith("+" + part)) yield return th;
        }
    }

    static void ListTypes(string part)
    {
        var exact = MatchTypes(part).ToList();
        var list = exact.Count > 0 ? exact : md.TypeDefinitions.Where(th => TypeName(th).Contains(part, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var th in list)
        {
            var t = md.GetTypeDefinition(th);
            string bt = t.BaseType.IsNil ? "" : " : " + EntityTypeName(t.BaseType);
            Console.WriteLine("== " + TypeName(th) + bt);
            foreach (var fh in t.GetFields())
            {
                var f = md.GetFieldDefinition(fh);
                Console.WriteLine("  필드 " + ((f.Attributes & System.Reflection.FieldAttributes.Static) != 0 ? "static " : "") + md.GetString(f.Name));
            }
            foreach (var mh in t.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                var sig = m.DecodeSignature(new Names(), null);
                Console.WriteLine("  메서드 " + ((m.Attributes & System.Reflection.MethodAttributes.Static) != 0 ? "static " : "") + sig.ReturnType + " " + md.GetString(m.Name) + "(" + string.Join(", ", sig.ParameterTypes) + ")");
            }
        }
        Console.WriteLine("(" + list.Count + "개 형식)");
    }

    static void ListMethods(string part, bool fields)
    {
        foreach (var th in MatchTypes(part))
        {
            var t = md.GetTypeDefinition(th);
            Console.WriteLine("== " + TypeName(th));
            foreach (var mh in t.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                Console.WriteLine("  " + md.GetString(m.Name));
                var seen = new HashSet<string>();
                foreach (var (op, tok) in Tokens(m))
                {
                    if ((tok >> 24) == 0x70) continue;   // ldstr 의 문자열 토큰
                    var h = Handle(tok);
                    string line = null;
                    if (IsCall(op) && (h.Kind == HandleKind.MethodDefinition || h.Kind == HandleKind.MemberReference || h.Kind == HandleKind.MethodSpecification)) line = "    호출 " + MemberName(h);
                    else if (op == ILOpCode.Ldsfld || op == ILOpCode.Stsfld || op == ILOpCode.Ldsflda) line = "    정적필드 " + (op == ILOpCode.Stsfld ? "쓰기 " : "읽기 ") + MemberName(h);
                    else if (fields && (op == ILOpCode.Ldfld || op == ILOpCode.Stfld || op == ILOpCode.Ldflda)) line = "    필드 " + (op == ILOpCode.Stfld ? "쓰기 " : "읽기 ") + MemberName(h);
                    if (line != null && seen.Add(line)) Console.WriteLine(line);
                }
            }
        }
    }

    // 메서드 서명을 글자로
    sealed class Names : ISignatureTypeProvider<string, object>
    {
        public string GetArrayType(string e, ArrayShape s) => e + "[" + new string(',', s.Rank - 1) + "]";
        public string GetByReferenceType(string e) => "ref " + e;
        public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
        public string GetGenericInstantiation(string g, System.Collections.Immutable.ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
        public string GetGenericMethodParameter(object c, int i) => "!!" + i;
        public string GetGenericTypeParameter(object c, int i) => "!" + i;
        public string GetModifiedType(string m, string u, bool r) => u;
        public string GetPinnedType(string e) => e;
        public string GetPointerType(string e) => e + "*";
        public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString().ToLowerInvariant();
        public string GetSZArrayType(string e) => e + "[]";
        public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => TypeName(h);
        public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => EntityTypeName(h);
        public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => EntityTypeName(h);
    }
}
