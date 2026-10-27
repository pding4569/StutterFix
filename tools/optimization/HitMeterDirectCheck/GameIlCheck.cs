using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static class GameIlCheck
{
    static readonly Dictionary<short,OpCode> Ops=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
    internal static void Check(string managed)
    {
        using var file=File.OpenRead(Path.Combine(managed,"Assembly-CSharp.dll"));
        using var pe=new PEReader(file); var md=pe.GetMetadataReader();
        string Method(EntityHandle h)
        {
            if(h.Kind==HandleKind.MethodSpecification) return Method(md.GetMethodSpecification((MethodSpecificationHandle)h).Method);
            if(h.Kind==HandleKind.MemberReference)
            {
                var member=md.GetMemberReference((MemberReferenceHandle)h);
                string type=member.Parent.Kind==HandleKind.TypeReference ? md.GetString(md.GetTypeReference((TypeReferenceHandle)member.Parent).Name) : "";
                return type+"."+md.GetString(member.Name);
            }
            var m=md.GetMethodDefinition((MethodDefinitionHandle)h);
            return md.GetString(md.GetTypeDefinition(m.GetDeclaringType()).Name)+"."+md.GetString(m.Name);
        }
        List<string> Calls(string type,string method)
        {
            var td=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)==type);
            var m=td.GetMethods().Select(h=>md.GetMethodDefinition(h)).Single(t=>md.GetString(t.Name)==method);
            var il=pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes();
            var result=new List<string>(); int pc=0;
            while(pc<il.Length)
            {
                short value=il[pc++]; if(value==0xfe) value=(short)(0xfe00|il[pc++]); var op=Ops[value];
                switch(op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: pc++; break;
                    case OperandType.InlineVar: pc+=2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: pc+=8; break;
                    case OperandType.InlineSwitch: int n=BitConverter.ToInt32(il,pc); pc+=4+4*n; break;
                    default:
                        if(op.OperandType==OperandType.InlineMethod) result.Add(Method(MetadataTokens.EntityHandle(BitConverter.ToInt32(il,pc))));
                        pc+=4; break;
                }
            }
            return result;
        }
        void Count(List<string> calls,string name,int expected)
        { if(calls.Count(c=>c==name)!=expected) throw new Exception("Installed game IL changed: "+name); }
        var show=Calls("ErrorMeterTick","Show"); var add=Calls("scrHitErrorMeter","AddHit"); var reset=Calls("scrHitErrorMeter","Reset");
        Count(show,"DOTween.Kill",2); Count(show,"TweenSettingsExtensions.OnKill",2); Count(show,"DOTweenModuleUI.DOColor",2);
        Count(add,"ShortcutExtensions.DORotateQuaternion",1); Count(add,"DOTweenModuleUI.DOAnchorPos",1); Count(reset,"DOTween.Kill",2);
        Console.WriteLine("Installed game IL: Show Kill=2 OnKill=2 DOColor=2; AddHit rotation=1 anchor=1; Reset global Kill sites=2");
    }
}
