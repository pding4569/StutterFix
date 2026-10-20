#if DEV || AUTOTEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace StutterFix
{
    // Offline IL inventory -> finite game-only manifest. No AppDomain scan, no
    // system/Unity/third-party patch target, no overload-name resolution.
    internal static partial class FloorRangeWriters
    {
        private static Harmony patches;
        internal static int Installed, Failures;
        private static bool lifecycle;
        private static readonly BindingFlags Declared=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
        internal static void InstallLifecycle(Harmony h)
        {
            try
            {
                var m=typeof(scnGame).GetMethod("ApplyEventsToFloors",Declared,null,new[]{typeof(List<scrFloor>),typeof(ADOFAI.LevelData),typeof(scrLevelMaker),typeof(List<ADOFAI.LevelEvent>)},null);
                if(m==null) throw new MissingMethodException("ApplyEventsToFloors exact signature");
                h.Patch(m,postfix:new HarmonyMethod(typeof(FloorRange),nameof(FloorRange.PreparedHook)));
                lifecycle=true;
            }
            catch(Exception e) { lifecycle=false; FloorRange.Reason("lifecycle:"+e.GetType().Name); }
        }
        internal static bool Install()
        {
            if(patches!=null) return Installed==Manifest.Length&&Failures==0;
            Failures=Installed=0;
            if(!lifecycle) { Failures++; FloorRange.Reason("lifecycle-not-ready"); return false; }
            patches=new Harmony("StutterFix.FloorRange.Writers");
            if(effectFloor==null||previousFloor==null) { Failures++; FloorRange.Reason("effect-floor-fields-missing"); Shutdown(); return false; }
            FloorRange.Log("writer-targets="+Manifest.Length+" limit=64 game="+typeof(scrFloor).Assembly.GetName().Name);
            if(Manifest.Length>64) { Failures++; FloorRange.Reason("writer-target-budget"); Shutdown(); return false; }
            foreach(var spec in Manifest)
            {
                try
                {
                    var type=typeof(scrFloor).Assembly.GetType(spec.Type,true);
                    MethodBase found=null;
                    var candidates=new List<MethodBase>(type.GetMethods(Declared));
                    candidates.AddRange(type.GetConstructors(Declared));
                    foreach(var m in candidates)
                        if(m.Name==spec.Name && Hex(m.Module.ResolveSignature(m.MetadataToken))==spec.Signature)
                        { if(found!=null) throw new AmbiguousMatchException(spec.Name); found=m; }
                    if(found==null) throw new MissingMethodException(spec.Type,spec.Name+" signature="+spec.Signature);
                    HarmonyMethod prefix=null, transpiler=null;
                    if(spec.Mode==1) prefix=new HarmonyMethod(typeof(FloorRangeWriters),nameof(FloorPrefix));
                    else if(spec.Mode==2) prefix=new HarmonyMethod(typeof(FloorRangeWriters),nameof(ResetPrefix));
                    else if(spec.Mode==4) prefix=new HarmonyMethod(typeof(FloorRangeWriters),nameof(EffectPrefix));
                    else if(spec.Mode==5) prefix=new HarmonyMethod(typeof(FloorRangeWriters),nameof(OwnerPrefix));
                    else transpiler=new HarmonyMethod(typeof(FloorRangeWriters),spec.Mode==3?nameof(ResetBody):nameof(Rewrite));
                    patches.Patch(found,prefix:prefix,transpiler:transpiler);
                    Installed++; FloorRange.Log("writer-ok "+found.DeclaringType.FullName+"::"+found+" signature="+spec.Signature);
                }
                catch(Exception e)
                { Failures++; FloorRange.Log("writer-fail "+spec.Type+"::"+spec.Name+" "+e.GetType().Name+" "+e.Message); }
            }
            if(Failures!=0) { patches.UnpatchAll(patches.Id); patches=null; Installed=0; return false; }
            return true;
        }
        internal static void Shutdown()
        { if(patches!=null) patches.UnpatchAll(patches.Id); patches=null; Installed=0; }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-",""); }
        public static void FloorPrefix(scrFloor __instance) { FloorRange.Mark(__instance); }
        public static void OwnerPrefix(scrFloor __0) { FloorRange.Mark(__0); }
        private static readonly FieldInfo effectFloor=typeof(ffxPlusBase).GetField("floor",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo previousFloor=typeof(ffxFloorAppearPlus).GetField("prevFloor",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        public static void EffectPrefix(ffxPlusBase __instance)
        {
            var floor=(scrFloor)effectFloor.GetValue(__instance);
            FloorRange.Mark(floor);
            if(floor!=null) FloorRange.Mark(floor.nextfloor);
            if(__instance is ffxFloorAppearPlus) FloorRange.Mark((scrFloor)previousFloor.GetValue(__instance));
        }
        public static void ResetPrefix() { FloorRange.ResetMap(); FloorRange.Reason("lifecycle-invalidated"); }
        public static IEnumerable<CodeInstruction> ResetBody(IEnumerable<CodeInstruction> source)
        {
            yield return new CodeInstruction(OpCodes.Call,Own(nameof(ResetPrefix)));
            foreach(var ins in source) yield return ins;
        }
        public static void Address(scrFloor f) { FloorRange.Mark(f); }
        public static void WriteOpacity(scrFloor f,float v) { FloorRange.Mark(f,FloorRange.Opacity); f.opacity=v; }
        public static void WriteStartPos(scrFloor f,Vector3 v) { FloorRange.Mark(f,FloorRange.Position); f.startPos=v; }
        public static void WriteStartRot(scrFloor f,Vector3 v) { FloorRange.Mark(f,FloorRange.Rotation); f.startRot=v; }
        public static void WriteTweenRot(scrFloor f,Vector3 v) { FloorRange.Mark(f,FloorRange.Rotation); f.tweenRot=v; }
        public static void Position(Transform t,Vector3 v) { FloorRange.Mark(t,FloorRange.Position); t.position=v; }
        public static void LocalPosition(Transform t,Vector3 v) { FloorRange.Mark(t,FloorRange.Position); t.localPosition=v; }
        public static void Euler(Transform t,Vector3 v) { FloorRange.Mark(t,FloorRange.Rotation); t.eulerAngles=v; }
        public static void LocalEuler(Transform t,Vector3 v) { FloorRange.Mark(t,FloorRange.Rotation); t.localEulerAngles=v; }
        public static void Scale(Transform t,Vector3 v) { FloorRange.Mark(t,FloorRange.Scale); t.localScale=v; }
        public static void Rotation(Transform t,Quaternion v) { FloorRange.Mark(t,FloorRange.Rotation); t.rotation=v; }
        public static void SetTR(Transform t,Vector3 p,Quaternion r) { FloorRange.Mark(t,FloorRange.All); t.SetPositionAndRotation(p,r); }
        private static MethodInfo Own(string name)
        { return typeof(FloorRangeWriters).GetMethod(name,BindingFlags.Public|BindingFlags.Static); }
        public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source)
        {
            foreach(var ins in source)
            {
                string wrapper=null;
                var field=ins.operand as FieldInfo;
                if(field!=null&&field.DeclaringType==typeof(scrFloor))
                {
                    bool tracked=field.Name=="opacity"||field.Name=="startPos"||field.Name=="startRot"||field.Name=="tweenRot";
                    if(tracked&&ins.opcode==OpCodes.Ldflda)
                    {
                        // Keep incoming labels/exception starts on the injected
                        // first instruction, so branches cannot bypass marking.
                        var dup=new CodeInstruction(OpCodes.Dup);
                        // Harmony is built against framework mscorlib.Label;
                        // cross that ABI through non-generic IList only here.
                        var labels=(IList)typeof(CodeInstruction).GetField("labels").GetValue(ins);
                        var dest=(IList)typeof(CodeInstruction).GetField("labels").GetValue(dup);
                        foreach(var label in labels) dest.Add(label);
                        labels.Clear();
                        for(int b=ins.blocks.Count-1;b>=0;b--)
                            if(ins.blocks[b].blockType!=ExceptionBlockType.EndExceptionBlock)
                            { dup.blocks.Insert(0,ins.blocks[b]); ins.blocks.RemoveAt(b); }
                        yield return dup; yield return new CodeInstruction(OpCodes.Call,Own(nameof(Address)));
                    }
                    if(ins.opcode==OpCodes.Stfld)
                    {
                        switch(field.Name)
                        {
                            case "opacity": wrapper=nameof(WriteOpacity); break;
                            case "startPos": wrapper=nameof(WriteStartPos); break;
                            case "startRot": wrapper=nameof(WriteStartRot); break;
                            case "tweenRot": wrapper=nameof(WriteTweenRot); break;
                        }
                    }
                }
                var method=ins.operand as MethodInfo;
                if(method!=null&&method.DeclaringType==typeof(Transform)&&(ins.opcode==OpCodes.Call||ins.opcode==OpCodes.Callvirt))
                {
                    switch(method.Name)
                    {
                        case "set_position": wrapper=nameof(Position); break;
                        case "set_localPosition": wrapper=nameof(LocalPosition); break;
                        case "set_eulerAngles": wrapper=nameof(Euler); break;
                        case "set_localEulerAngles": wrapper=nameof(LocalEuler); break;
                        case "set_localScale": wrapper=nameof(Scale); break;
                        case "set_rotation": wrapper=nameof(Rotation); break;
                        case "SetPositionAndRotation": wrapper=nameof(SetTR); break;
                    }
                }
                if(wrapper!=null) { ins.opcode=OpCodes.Call; ins.operand=Own(wrapper); }
                yield return ins;
            }
        }
        private sealed class Spec
        {
            internal readonly string Type,Name,Signature; internal readonly int Mode;
            internal Spec(string type,string name,string signature,int mode=0) { Type=type; Name=name; Signature=signature; Mode=mode; }
        }
    }
}
#endif
