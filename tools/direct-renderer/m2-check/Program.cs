using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HarmonyLib;
using StutterFix;

static class Program
{
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    static readonly Dictionary<short, OpCode> ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
    static IEnumerable<MethodInfo> Calls(MethodInfo method)
    {
        using var stream = File.OpenRead(method.Module.FullyQualifiedName);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var il = method.GetMethodBody().GetILAsByteArray();
        for (int i = 0; i < il.Length;)
        {
            short value = il[i++]; if (value == 0xfe) value = (short)(0xfe00 | il[i++]);
            var op = ops[value]; int width;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: width = 0; break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: width = 1; break;
                case OperandType.InlineVar: width = 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: width = 8; break;
                case OperandType.InlineSwitch: width = 4 + 4 * BitConverter.ToInt32(il, i); break;
                default: width = 4; break;
            }
            if (op == OpCodes.Call || op == OpCodes.Callvirt)
            {
                int token = BitConverter.ToInt32(il, i);
                EntityHandle handle = MetadataTokens.EntityHandle(token);
                if (handle.Kind == HandleKind.MethodSpecification) handle = metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
                if (handle.Kind == HandleKind.MemberReference)
                {
                    var parent = metadata.GetMemberReference((MemberReferenceHandle)handle).Parent;
                    if (parent.Kind == HandleKind.TypeReference && metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Namespace).StartsWith("UnityEngine"))
                    {
                        var target = method.Module.ResolveMethod(token, method.DeclaringType.GetGenericArguments(), method.GetGenericArguments()) as MethodInfo;
                        if (target != null) yield return target;
                    }
                }
            }
            i += width;
        }
    }
    static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        string managed = "D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed";
        AssemblyLoadContext.Default.Resolving += (ctx, n) =>
        {
            foreach (var folder in new[] { managed, Path.Combine(managed, "UnityModManager"), Path.Combine(root, "bin/Release") })
            {
                string path = Path.Combine(folder, n.Name + ".dll");
                if (File.Exists(path)) return ctx.LoadFromAssemblyPath(path);
            }
            return null;
        };
        // Metadata / IL only: no game type is instantiated and no Unity native API is called.
        var sf = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, "bin/Release/StutterFix.dll"));
        var changes = sf.GetType("StutterFix.DirectSequenceChanges", true);
        var kind = changes.GetMethod("Kind", BindingFlags.NonPublic | BindingFlags.Static);
        var instrument = changes.GetMethod("Instrument", BindingFlags.NonPublic | BindingFlags.Static);
        var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "Assembly-CSharp.dll"));
        int methods = 0, sites = 0;
        var unique = new HashSet<MethodInfo>();
        foreach (var assembly in new[] { game, sf }) foreach (var type in assembly.GetTypes())
        {
            if (type.ContainsGenericParameters) continue;
            var outer = type; while (outer.DeclaringType != null) outer = outer.DeclaringType;
            if (outer.Name.StartsWith("DirectDeco") || outer.Name == "DirectSequenceChanges" || outer.Name.StartsWith("FrameCompare")) continue;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                bool used = false;
                foreach (var target in Calls(method))
                    if ((int)kind.Invoke(null, new object[] { target }) >= 0)
                    { sites++; used = true; unique.Add(target); }
                if (used) methods++;
            }
        }
        int wrappers = 0;
        foreach (var target in unique)
        {
            // Fully enumerate the production transpiler, including delegate binding and generic
            // wrapper constraints, but never install a patch or execute the replaced game call.
            var original = new CodeInstruction(OpCodes.Call, target);
            original.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
            var code = (IEnumerable<CodeInstruction>)instrument.Invoke(null, new object[] { new[] { original } });
            var emitted = code.ToArray();
            Check(emitted.Length == 2 && emitted[0].opcode == OpCodes.Ldc_I4 && emitted[1].opcode == OpCodes.Call, "invalid rewrite: " + target);
            Check(emitted[1].blocks.Count == 1 && emitted[0].blocks.Count == 0, "exception boundary moved incorrectly");
            wrappers++;
        }
        Console.WriteLine($"PASS game+mod IL audit: methods={methods} sites={sites} uniqueWrappers={wrappers}; no patch installed, no game method invoked");

        // Independent lexicographic reference, including intermediate tile/planet layers and
        // multiple materials on one renderer. Exercise the actual production key implementation.
        var rng = new Random(20261115);
        var keys = new List<SceneRenderOrder.Key>();
        for (int i = 0; i < 20000; i++) keys.Add(new SceneRenderOrder.Key(rng.Next(-8, 5), rng.Next(-20, 20), rng.Next(2000, 4001), rng.Next(-10, 10), rng.Next(-30, 30), rng.Next(-300, 300), rng.Next(0, 4), rng.Next(-20, 20)));
        var expected = keys.OrderBy(k => k.Layer).ThenBy(k => k.Order).ThenBy(k => k.Queue).ThenByDescending(k => k.Distance)
            .ThenBy(k => k.Shader).ThenBy(k => k.Material).ThenBy(k => k.Renderer).ThenBy(k => k.Slot).ToArray();
        keys.Sort();
        for (int i = 0; i < keys.Count; i++) Check(keys[i].CompareTo(expected[i]) == 0, "full sequence key order");
        Console.WriteLine("PASS 20000 full-sequence keys vs independent 8-field reference (layers/queues/distance/shader/material/renderer/submesh)");
        var visibility = new DirectRenderVisibilityIndex(); visibility.Reset(20000);
        for (int i = 0; i < 20000; i += 3) visibility.Set(i, true);
        long sum = 0;
        for (int j = 0; j < 500; j++) for (int i = visibility.Next(-1); i >= 0; i = visibility.Next(i)) sum += i;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int j = 0; j < 1000; j++)
        {
            visibility.Set(19999, (j & 1) == 0);
            for (int i = visibility.Next(-1); i >= 0; i = visibility.Next(i)) sum += i;
        }
        long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocation == 0, "visibility allocation");
        Console.WriteLine($"PASS 20000-entry visibility sequence: allocation={allocation} bytes checksum={sum} (.NET 8, data structure only)");
        return 0;
    }
}
