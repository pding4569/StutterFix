#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Observe managed write sites, never patch Unity's native/internal-call setters.
    // A source is indexed by its renderer, materials, and all Transform ancestors.
    // Notifications are deduplicated until PreCull. Only spawn/reparent events
    // inspect their changed subtree; ordinary writes perform indexed lookups.
    internal static class LiveDecoChanges
    {
        internal const int State = 0, Shape = 1, Pose = 2, Hierarchy = 3, Script = 4;
        sealed class Binding { internal Delegate Call; internal int Kind; }
        static readonly List<Binding> bindings = new List<Binding>();
        static readonly Dictionary<MethodInfo, int> bound = new Dictionary<MethodInfo, int>();
        static readonly MethodInfo readCode = typeof(PatchProcessor).GetMethods().Single(m => m.Name == "GetOriginalInstructions"
            && m.GetParameters().Length == 2 && !m.GetParameters()[1].IsOut);
        internal static int Methods, Sites;

        internal static int Kind(MethodInfo m)
        {
            if (m == null || m.ContainsGenericParameters
                || m.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer)) return -1;
            var t = m.DeclaringType; string n = m.Name;
            if (t == typeof(UnityEngine.Object) && m.IsStatic && m.GetParameters().Length > 0)
            {
                if ((n == "Destroy" || n == "DestroyImmediate") && m.ReturnType == typeof(void)) return Hierarchy;
                if (n == "Instantiate" && typeof(UnityEngine.Object).IsAssignableFrom(m.ReturnType)) return Hierarchy;
            }
            if (t == typeof(GameObject) && n == "AddComponent" && !m.IsStatic
                && typeof(UnityEngine.Object).IsAssignableFrom(m.ReturnType)) return Hierarchy;
            if (m.IsStatic || m.ReturnType != typeof(void)) return -1;
            if (t == typeof(Behaviour) && n == "set_enabled") return Script;
            if (typeof(Renderer).IsAssignableFrom(t))
            {
                if (n == "set_enabled" || n == "set_forceRenderingOff" || n == "set_sortingLayerID"
                    || n == "set_sortingLayerName" || n == "set_sortingOrder") return State;
                if (n == "set_material" || n == "set_materials" || n == "set_sharedMaterial" || n == "SetMaterials" || n == "SetSharedMaterials"
                    || n == "set_sharedMaterials" || n == "SetPropertyBlock" || n == "set_sprite"
                    || n == "set_flipX" || n == "set_flipY" || n == "set_drawMode" || n == "set_maskInteraction") return Shape;
                if (n == "set_bounds" || n == "set_localBounds" || n == "ResetBounds" || n == "ResetLocalBounds"
                    || n == "set_spriteSortPoint" || n == "set_size") return Shape;
            }
            if (t == typeof(Material) && (n.StartsWith("Set", StringComparison.Ordinal) || n.StartsWith("set_", StringComparison.Ordinal)
                || n == "EnableKeyword" || n == "DisableKeyword" || n == "CopyPropertiesFromMaterial")) return Shape;
            if (t == typeof(GameObject) && (n == "SetActive" || n == "set_layer")) return State;
            if (t == typeof(Transform))
            {
                if (n == "SetParent" || n == "set_parent" || n == "DetachChildren") return Hierarchy;
                if (n == "set_position" || n == "set_localPosition" || n == "set_rotation"
                    || n == "set_localRotation" || n == "set_eulerAngles" || n == "set_localEulerAngles"
                    || n == "set_localScale" || n == "SetPositionAndRotation" || n == "SetLocalPositionAndRotation"
                    || n == "Translate" || n == "Rotate" || n == "RotateAround" || n == "LookAt"
                    || n == "set_forward" || n == "set_right" || n == "set_up") return Pose;
            }
            return -1;
        }

        internal static void Install(Harmony h)
        {
            Methods = Sites = 0;
            // These two assemblies contain the game and our competing optimizations.
            // Own writes/restoration and diagnostics are intentionally not instrumented.
            foreach (var assembly in new[] { typeof(scrDecoration).Assembly, typeof(LiveDecoChanges).Assembly })
            foreach (var type in assembly.GetTypes())
            {
                if (type.ContainsGenericParameters || Excluded(type)) continue;
                foreach (var method in type.GetMethods(AccessTools.all).Where(m => m.DeclaringType == type
                    && !m.IsAbstract && !m.ContainsGenericParameters && m.GetMethodBody() != null))
                {
                    // The installed Harmony targets Mono mscorlib's ILGenerator.
                    // Reflection here avoids importing that incompatible signature
                    // into this netstandard assembly. No game method is invoked.
                    var code = (IEnumerable<CodeInstruction>)readCode.Invoke(null, new object[] { method, null });
                    if (!code.Any(c => (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt)
                        && c.operand is MethodInfo target && Kind(target) >= 0)) continue;
                    // A failed hook aborts Begin and restores all owners, rather than
                    // silently running with a partial change protocol.
                    h.Patch(method, transpiler: new HarmonyMethod(typeof(LiveDecoChanges), nameof(Instrument)));
                    Methods++;
                }
            }
        }
        static bool Excluded(Type type)
        {
            while (type.DeclaringType != null) type = type.DeclaringType;
            string name = type.Name;
            return name.StartsWith("LiveDeco", StringComparison.Ordinal)
                || name.StartsWith("FrameCompare", StringComparison.Ordinal)
                || name.StartsWith("Gpu", StringComparison.Ordinal) || name == "MatShare"
                || name == "SortProbe" || name == "InstBench" || name == "MeshBench";
        }

        internal static IEnumerable<CodeInstruction> Instrument(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                int kind = instruction.operand is MethodInfo m
                    && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) ? Kind(m) : -1;
                if (kind < 0) { yield return instruction; continue; }
                var target = (MethodInfo)instruction.operand;
                bool returns = target.ReturnType != typeof(void);
                Type[] signature = (target.IsStatic ? Enumerable.Empty<Type>() : new[] { target.DeclaringType })
                    .Concat(target.GetParameters().Select(p => p.ParameterType))
                    .Concat(returns ? new[] { target.ReturnType } : Type.EmptyTypes).ToArray();
                if (signature.Length > 5) throw new NotSupportedException("change writer arity: " + target);
                if (!bound.TryGetValue(target, out int id))
                {
                    Type delegateType = returns ? System.Linq.Expressions.Expression.GetFuncType(signature)
                        : System.Linq.Expressions.Expression.GetActionType(signature);
                    var binding = new Binding { Call = target.CreateDelegate(delegateType), Kind = kind };
                    id = bindings.Count; bindings.Add(binding); bound.Add(target, id);
                }
                string name = target.IsStatic ? (returns ? "Spawn" : "Destroy") : returns ? "Add" : "Call";
                int arity = signature.Length - (target.IsStatic && !returns ? 0 : !target.IsStatic && returns ? 2 : 1);
                var wrapper = typeof(LiveDecoChanges).GetMethod(name + arity, AccessTools.all)
                    .MakeGenericMethod(signature);
                // Keep the original instruction (and its labels) at the start.
                // The wrapper consumes the original receiver/arguments plus id.
                var finish = new CodeInstruction(OpCodes.Call, wrapper);
                finish.blocks.AddRange(instruction.blocks.Where(b => b.blockType == ExceptionBlockType.EndExceptionBlock));
                instruction.blocks.RemoveAll(b => b.blockType == ExceptionBlockType.EndExceptionBlock);
                instruction.opcode = OpCodes.Ldc_I4; instruction.operand = id;
                yield return instruction; yield return finish;
                Sites++;
            }
        }
        static void Call0<T>(T obj, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<T>)b.Call)(obj); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static void Call1<T, A>(T obj, A a, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<T, A>)b.Call)(obj, a); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static void Call2<T, A, B>(T obj, A a, B c, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<T, A, B>)b.Call)(obj, a, c); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static void Call3<T, A, B, C>(T obj, A a, B c, C d, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<T, A, B, C>)b.Call)(obj, a, c, d); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static void Call4<T, A, B, C, D>(T obj, A a, B c, C d, D e, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<T, A, B, C, D>)b.Call)(obj, a, c, d, e); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static R Spawn1<A, R>(A original, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(original, b.Kind);
            R result = ((Func<A, R>)b.Call)(original); LiveDecoRenderer.AfterWrite(result, b.Kind); return result;
        }
        static R Spawn2<A, B, R>(A original, B a, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(original, b.Kind);
            R result = ((Func<A, B, R>)b.Call)(original, a); LiveDecoRenderer.AfterWrite(result, b.Kind); return result;
        }
        static R Spawn3<A, B, C, R>(A original, B a, C c, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(original, b.Kind);
            R result = ((Func<A, B, C, R>)b.Call)(original, a, c); LiveDecoRenderer.AfterWrite(result, b.Kind); return result;
        }
        static R Spawn4<A, B, C, D, R>(A original, B a, C c, D d, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(original, b.Kind);
            R result = ((Func<A, B, C, D, R>)b.Call)(original, a, c, d); LiveDecoRenderer.AfterWrite(result, b.Kind); return result;
        }
        static void Destroy1<A>(A obj, int id) where A : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<A>)b.Call)(obj); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static void Destroy2<A, B>(A obj, B a, int id) where A : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            ((Action<A, B>)b.Call)(obj, a); LiveDecoRenderer.AfterWrite(obj, b.Kind);
        }
        static R Add0<T, R>(T obj, int id) where T : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            R result = ((Func<T, R>)b.Call)(obj); LiveDecoRenderer.AfterWrite(result, b.Kind); return result;
        }
        static R Add1<T, A, R>(T obj, A a, int id) where T : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; LiveDecoRenderer.BeforeWrite(obj, b.Kind);
            R result = ((Func<T, A, R>)b.Call)(obj, a); LiveDecoRenderer.AfterWrite(result, b.Kind); return result;
        }
    }
}
#endif
