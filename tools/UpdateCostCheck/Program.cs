using System;
using System.IO;
using System.Reflection;
using DG.Tweening;
namespace HarmonyLib {
    static class AccessTools {
        internal const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static Type TypeByName(string n) => typeof(Ease).Assembly.GetType(n);
    }
    sealed class PatchInfo { internal string[] Owners = Array.Empty<string>(); }
    static class Harmony { internal static PatchInfo GetPatchInfo(MethodBase m) => null; }
}
namespace StutterFix {
    static class InstantMove { internal static int Bits(float x) => BitConverter.SingleToInt32Bits(x); }
    static class ZeroTween {
        private delegate float EvalFn(Ease e, EaseFunction f, float t, float d, float o, float p);
        private static readonly EvalFn eval = (EvalFn)Delegate.CreateDelegate(typeof(EvalFn), typeof(Ease).Assembly.GetType("DG.Tweening.Core.Easing.EaseManager").GetMethod("Evaluate", HarmonyLib.AccessTools.all, null, new[]{typeof(Ease),typeof(EaseFunction),typeof(float),typeof(float),typeof(float),typeof(float)}, null));
        internal static float Eval(Ease e,float t,float d,float o,float p) => eval(e,null,t,d,o,p);
    }
    class Program {
        static void Require(bool v,string m) { if(!v) throw new Exception(m); }
        static void DumpLinear() {
            var m=typeof(Ease).Assembly.GetType("DG.Tweening.Core.Easing.EaseManager").GetMethod("Evaluate",HarmonyLib.AccessTools.all,null,new[]{typeof(Ease),typeof(EaseFunction),typeof(float),typeof(float),typeof(float),typeof(float)},null);
            var body=m.GetMethodBody().GetILAsByteArray();
            var ops=new System.Collections.Generic.Dictionary<short,System.Reflection.Emit.OpCode>();
            foreach(var f in typeof(System.Reflection.Emit.OpCodes).GetFields()) { var op=(System.Reflection.Emit.OpCode)f.GetValue(null); ops[op.Value]=op; }
            Console.WriteLine(m+" Linear enum="+(int)Ease.Linear);
            int pc=0; while(pc<body.Length) {
                int at=pc; short code=body[pc++]; if(code==0xfe) code=(short)(0xfe00|body[pc++]); var op=ops[code]; string operand="";
                switch(op.OperandType) {
                    case System.Reflection.Emit.OperandType.InlineNone: break;
                    case System.Reflection.Emit.OperandType.ShortInlineBrTarget: operand="IL_"+(pc+1+(sbyte)body[pc]).ToString("x4");pc++;break;
                    case System.Reflection.Emit.OperandType.InlineBrTarget: operand="IL_"+(pc+4+BitConverter.ToInt32(body,pc)).ToString("x4");pc+=4;break;
                    case System.Reflection.Emit.OperandType.InlineSwitch: int n=BitConverter.ToInt32(body,pc);pc+=4;int end=pc+4*n;for(int j=0;j<n;j++)operand+=j+":IL_"+(end+BitConverter.ToInt32(body,pc+4*j)).ToString("x4")+" ";pc=end;break;
                    case System.Reflection.Emit.OperandType.ShortInlineI: case System.Reflection.Emit.OperandType.ShortInlineVar: operand=body[pc++].ToString();break;
                    case System.Reflection.Emit.OperandType.InlineVar: operand=BitConverter.ToUInt16(body,pc).ToString();pc+=2;break;
                    case System.Reflection.Emit.OperandType.ShortInlineR: operand=BitConverter.ToSingle(body,pc).ToString("R");pc+=4;break;
                    case System.Reflection.Emit.OperandType.InlineR: operand=BitConverter.ToDouble(body,pc).ToString("R");pc+=8;break;
                    case System.Reflection.Emit.OperandType.InlineI8: operand=BitConverter.ToInt64(body,pc).ToString();pc+=8;break;
                    case System.Reflection.Emit.OperandType.InlineI: operand=BitConverter.ToInt32(body,pc).ToString();pc+=4;break;
                    default: int token=BitConverter.ToInt32(body,pc);pc+=4;try {operand=m.Module.ResolveMember(token).ToString();}catch{operand="token "+token.ToString("x8");}break;
                }
                Console.WriteLine($"IL_{at:x4}: {op} {operand}");
            }
        }
        static void Main(string[] args) {
            if(args.Length!=0 && args[0]=="--il") { DumpLinear(); return; }
            string dir=Path.Combine(Path.GetTempPath(),"sf-updatecost-check-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
            try {
                UpdateEvalMemo.Configure(dir); Require(!UpdateEvalMemo.Enabled,"default off");
                File.WriteAllText(Path.Combine(dir,"evalmemo-on"),""); UpdateEvalMemo.Configure(dir); Require(UpdateEvalMemo.Enabled,"opt in");
                File.WriteAllText(Path.Combine(dir,"evalmemo-off"),""); UpdateEvalMemo.Configure(dir); Require(!UpdateEvalMemo.Enabled,"off dominates");
                File.Delete(Path.Combine(dir,"evalmemo-off")); UpdateEvalMemo.Configure(dir); UpdateEvalMemo.Verify=true;
                var rng=new Random(712018); long compared=0;
                float[] edge={0f,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),float.NaN,float.PositiveInfinity,float.NegativeInfinity,float.Epsilon,-float.Epsilon,1f,-1f};
                for(int i=0;i<100000;i++) {
                    float t=i<edge.Length*edge.Length?edge[i%edge.Length]:BitConverter.Int32BitsToSingle(rng.NextInt64(int.MinValue,(long)int.MaxValue+1) is long n?(int)n:0);
                    float d=i<edge.Length*edge.Length?edge[i/edge.Length%edge.Length]:(float)(rng.NextDouble()*100+.001);
                    float over=(float)rng.NextDouble(),period=(float)rng.NextDouble();
                    UpdateEvalMemo.NewFrame(); float expected=ZeroTween.Eval(Ease.Linear,t,d,over,period);
                    for(int k=0;k<5;k++) { float actual=UpdateEvalMemo.Eval(Ease.Linear,t,d,over,period); Require(InstantMove.Bits(actual)==InstantMove.Bits(expected),"bits "+i); compared++; }
                    long hits=UpdateEvalMemo.Hits;
                    UpdateEvalMemo.Eval(Ease.Linear,t,d,BitConverter.Int32BitsToSingle(InstantMove.Bits(over)^1),period); Require(UpdateEvalMemo.Hits==hits,"over key");
                    UpdateEvalMemo.NewFrame(); UpdateEvalMemo.Eval(Ease.Linear,t,d,over,period); Require(UpdateEvalMemo.Hits==hits,"frame reset");
                    UpdateEvalMemo.Eval(Ease.InQuad,0.5f,1f,over,period);
                    UpdateEvalMemo.Eval(Ease.Linear,t,d,over,period); Require(UpdateEvalMemo.Hits==hits,"nonlinear invalidates");
                }
                Require(UpdateEvalMemo.Checks>0 && UpdateEvalMemo.Mismatch==0,"shadow");
                Console.WriteLine($"actual installed DOTween / .NET 8: compared={compared} checks={UpdateEvalMemo.Checks} mismatch={UpdateEvalMemo.Mismatch}; flags/frame/input invalidation passed. Harmony is stubbed; Unity/Mono and patch installation are NOT tested.");
            } finally { foreach(var f in Directory.GetFiles(dir)) File.Delete(f); Directory.Delete(dir); }
        }
    }
}
