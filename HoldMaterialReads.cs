#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Reuse Renderer.material only within a single original method invocation.
    // No cross-frame cache: external material changes remain observable.
    internal static class HoldMaterialReads
    {
        static Harmony hooks;
        static bool enabled,verify;
        static long checks,mismatches;
        static readonly MethodInfo getter=AccessTools.PropertyGetter(typeof(Renderer),"material");
        static readonly MethodInfo transpiler=AccessTools.Method(typeof(HoldMaterialReads),nameof(Transpiler));
        static readonly List<MethodBase> targets=new List<MethodBase>();
        static Material Read(Renderer renderer,ref Material cached)
        {
            if(ReferenceEquals(cached,null))return cached=renderer.material;
            if(verify)
            {
                var actual=renderer.material;checks++;
                if(!ReferenceEquals(actual,cached))mismatches++;
                return actual;
            }
            return cached;
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input,ILGenerator il)
        {
            var code=new List<CodeInstruction>(input);int count=0;
            foreach(var c in code)if(c.Calls(getter))count++;
            if(count<2)throw new InvalidOperationException("hold material getter shape changed");
            var cache=il.DeclareLocal(typeof(Material));
            foreach(var c in code)
            {
                if(!c.Calls(getter)){yield return c;continue;}
                c.opcode=OpCodes.Ldloca;c.operand=cache;
                yield return c;
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(HoldMaterialReads),nameof(Read)));
            }
        }
        internal static string Command(string arg)
        {
            if(arg=="on" || arg=="off" || arg=="check")
            {
                if(hooks==null)
                {
                    hooks=new Harmony("StutterFix.HoldMaterialReads.Research");
                    foreach(var m in typeof(scrHoldRenderer).GetMethods(AccessTools.all))
                        if(m.Name=="UpdateMesh" || m.Name=="UpdateColor" && m.GetParameters().Length==2)targets.Add(m);
                    if(targets.Count!=2)throw new InvalidOperationException("hold methods changed");
                }
                bool next=arg!="off";
                if(next!=enabled)foreach(var target in targets)
                    if(next)hooks.Patch(target,transpiler:new HarmonyMethod(transpiler));
                    else hooks.Unpatch(target,transpiler);
                enabled=next;verify=arg=="check";checks=mismatches=0;
            }
            else if(arg!="state" && arg!="verify")throw new ArgumentException("holdreads on|off|check|state|verify");
            if(arg=="verify" && (!verify || checks==0 || mismatches!=0))throw new InvalidOperationException("hold material oracle absent or mismatch");
            return "enabled="+enabled+" oracle="+verify+" checks="+checks+" mismatches="+mismatches;
        }
    }
}
#endif
