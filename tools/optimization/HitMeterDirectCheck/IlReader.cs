using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// The game's old Harmony IL reader depends on Mono-only APIs. Decode on .NET 8,
// then pass real CodeInstruction objects through the unchanged production transpilers.
static class IlReader
{
    static readonly Dictionary<short,OpCode> Ops=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
    internal static List<CodeInstruction> Read(MethodInfo method,ILGenerator gen)
    {
        var bytes=method.GetMethodBody().GetILAsByteArray();
        var code=new List<(int Offset,CodeInstruction Code)>();
        var labels=new Dictionary<int,Label>();
        Label Target(int at) { if(!labels.TryGetValue(at,out var l)) labels[at]=l=gen.DefineLabel(); return l; }
        int pc=0;
        while(pc<bytes.Length)
        {
            int at=pc; short value=bytes[pc++]; if(value==0xfe) value=(short)(0xfe00|bytes[pc++]);
            var op=Ops[value]; object operand=null;
            int Token() { int t=BitConverter.ToInt32(bytes,pc); pc+=4; return t; }
            switch(op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: { int d=(sbyte)bytes[pc++]; operand=Target(pc+d); break; }
                case OperandType.InlineBrTarget: { int d=Token(); operand=Target(pc+d); break; }
                case OperandType.InlineSwitch:
                    int n=Token(),end=pc+4*n; var targets=new Label[n];
                    for(int i=0;i<n;i++) targets[i]=Target(end+Token()); operand=targets; break;
                case OperandType.ShortInlineI: operand=(sbyte)bytes[pc++]; break;
                case OperandType.InlineI: operand=Token(); break;
                case OperandType.ShortInlineVar: operand=bytes[pc++]; break;
                case OperandType.InlineVar: operand=BitConverter.ToInt16(bytes,pc); pc+=2; break;
                case OperandType.ShortInlineR: operand=BitConverter.ToSingle(bytes,pc); pc+=4; break;
                case OperandType.InlineR: operand=BitConverter.ToDouble(bytes,pc); pc+=8; break;
                case OperandType.InlineI8: operand=BitConverter.ToInt64(bytes,pc); pc+=8; break;
                case OperandType.InlineMethod: operand=method.Module.ResolveMethod(Token()); break;
                case OperandType.InlineField: operand=method.Module.ResolveField(Token()); break;
                case OperandType.InlineType: operand=method.Module.ResolveType(Token()); break;
                case OperandType.InlineString: operand=method.Module.ResolveString(Token()); break;
                case OperandType.InlineTok: operand=method.Module.ResolveMember(Token()); break;
                default: throw new Exception("Unsupported operand "+op);
            }
            code.Add((at,new CodeInstruction(op,operand)));
        }
        foreach(var item in code) if(labels.TryGetValue(item.Offset,out var label)) item.Code.labels.Add(label);
        if(labels.ContainsKey(bytes.Length)) throw new Exception("End label not supported by fixture emitter");
        return code.Select(c=>c.Code).ToList();
    }
}
