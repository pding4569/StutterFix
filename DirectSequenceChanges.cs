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
    internal static class DirectSequenceChanges
    {
        internal const int State = 0, Shape = 1, Pose = 2, Hierarchy = 3, Script = 4;
        sealed class Binding { internal Delegate Call, Read; internal int Kind; internal string Writer; }
        internal static long EqualityReads, UnchangedWrites;
        static readonly List<Binding> bindings = new List<Binding>();
        static readonly Dictionary<MethodInfo, int> bound = new Dictionary<MethodInfo, int>();
        static readonly MethodInfo readCode = typeof(PatchProcessor).GetMethods().Single(m => m.Name == "GetOriginalInstructions"
            && m.GetParameters().Length == 2 && !m.GetParameters()[1].IsOut);
        internal static int Methods, Sites;

        // Compare only canonical, single-value setters. Never read material(s)
        // (they clone/allocate), or infer equality of MPBs/commands. In particular
        // forceRenderingOff must always notify: while owned its native true bit
        // is ours, so a foreign true write is still an ownership boundary.
        static Delegate EqualityReader(MethodInfo writer)
        {
            if (writer.IsStatic || writer.GetParameters().Length != 1) return null;
            Type t = writer.DeclaringType; string n = writer.Name, getter = null;
            if (t == typeof(GameObject) && n == "SetActive") getter = "get_activeSelf";
            else if ((t == typeof(Behaviour) || typeof(Renderer).IsAssignableFrom(t)) && n == "set_enabled") getter = "get_enabled";
            else if (t == typeof(Transform) && (n == "set_localPosition" || n == "set_position"
                || n == "set_localRotation" || n == "set_rotation" || n == "set_localScale")) getter = "get_" + n.Substring(4);
            if (getter == null) return null;
            var read = t.GetMethod(getter, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Type value = writer.GetParameters()[0].ParameterType;
            if (read == null || read.ReturnType != value) throw new MissingMethodException(t.FullName, getter);
            return read.CreateDelegate(System.Linq.Expressions.Expression.GetFuncType(t, value));
        }

        static bool Notify<T, A>(Binding b, T obj, A value) where T : UnityEngine.Object
        {
            if (b.Read == null || !DirectDeco.Active) return true;
            A current;
            // On a destroyed object preserve the original writer's exception,
            // rather than replacing it with a new getter failure.
            try { EqualityReads++; current = ((Func<T, A>)b.Read)(obj); }
            catch { return true; }
            if (!EqualityComparer<A>.Default.Equals(current, value)) return true;
            UnchangedWrites++; return false;
        }

        internal static void ResetCounts() { EqualityReads = UnchangedWrites = 0; }

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
            if ((typeof(UnityEngine.UI.Graphic).IsAssignableFrom(t) || t == typeof(CanvasRenderer) || t == typeof(CanvasGroup) || t == typeof(Canvas))
                && (n.StartsWith("set_", StringComparison.Ordinal) || n.StartsWith("Set", StringComparison.Ordinal))) return Script;
            if (typeof(Renderer).IsAssignableFrom(t))
            {
                if (typeof(SpriteMask).IsAssignableFrom(t) && n.StartsWith("set_", StringComparison.Ordinal)) return Shape;
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
            foreach (var assembly in new[] { typeof(scrDecoration).Assembly, typeof(DirectSequenceChanges).Assembly })
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
                    h.Patch(method, transpiler: new HarmonyMethod(typeof(DirectSequenceChanges), nameof(Instrument)));
                    Methods++;
                }
            }
        }
        static bool Excluded(Type type)
        {
            while (type.DeclaringType != null) type = type.DeclaringType;
            string name = type.Name;
            return name.StartsWith("DirectDeco", StringComparison.Ordinal) || name == "DirectSequenceChanges"
                || name.StartsWith("FrameCompare", StringComparison.Ordinal);

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
                    var binding = new Binding { Call = target.CreateDelegate(delegateType), Read = EqualityReader(target), Kind = kind, Writer = target.Name };
                    id = bindings.Count; bindings.Add(binding); bound.Add(target, id);
                }
                string name = target.IsStatic ? (returns ? "Spawn" : "Destroy") : returns ? "Add" : "Call";
                int arity = signature.Length - (target.IsStatic && !returns ? 0 : !target.IsStatic && returns ? 2 : 1);
                var wrapper = typeof(DirectSequenceChanges).GetMethod(name + arity, AccessTools.all)
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
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<T>)b.Call)(obj); DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static void Call1<T, A>(T obj, A a, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; bool notify = Notify(b, obj, a);
            if (notify) DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<T, A>)b.Call)(obj, a);
            if (notify) DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static void Call2<T, A, B>(T obj, A a, B c, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<T, A, B>)b.Call)(obj, a, c); DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static void Call3<T, A, B, C>(T obj, A a, B c, C d, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<T, A, B, C>)b.Call)(obj, a, c, d); DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static void Call4<T, A, B, C, D>(T obj, A a, B c, C d, D e, int id) where T : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<T, A, B, C, D>)b.Call)(obj, a, c, d, e); DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static R Spawn1<A, R>(A original, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(original, b.Kind);
            R result = ((Func<A, R>)b.Call)(original); DirectDeco.AfterSequenceSpawn(original, result); DirectDeco.AfterSequenceWrite(result, b.Kind, b.Writer); return result;
        }
        static R Spawn2<A, B, R>(A original, B a, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(original, b.Kind);
            R result = ((Func<A, B, R>)b.Call)(original, a); DirectDeco.AfterSequenceSpawn(original, result); DirectDeco.AfterSequenceWrite(result, b.Kind, b.Writer); return result;
        }
        static R Spawn3<A, B, C, R>(A original, B a, C c, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(original, b.Kind);
            R result = ((Func<A, B, C, R>)b.Call)(original, a, c); DirectDeco.AfterSequenceSpawn(original, result); DirectDeco.AfterSequenceWrite(result, b.Kind, b.Writer); return result;
        }
        static R Spawn4<A, B, C, D, R>(A original, B a, C c, D d, int id) where A : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(original, b.Kind);
            R result = ((Func<A, B, C, D, R>)b.Call)(original, a, c, d); DirectDeco.AfterSequenceSpawn(original, result); DirectDeco.AfterSequenceWrite(result, b.Kind, b.Writer); return result;
        }
        static void Destroy1<A>(A obj, int id) where A : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<A>)b.Call)(obj); DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static void Destroy2<A, B>(A obj, B a, int id) where A : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            ((Action<A, B>)b.Call)(obj, a); DirectDeco.AfterSequenceWrite(obj, b.Kind, b.Writer);
        }
        static R Add0<T, R>(T obj, int id) where T : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            R result = ((Func<T, R>)b.Call)(obj); DirectDeco.AfterSequenceWrite(result, b.Kind, b.Writer); return result;
        }
        static R Add1<T, A, R>(T obj, A a, int id) where T : UnityEngine.Object where R : UnityEngine.Object
        {
            var b = bindings[id]; DirectDeco.BeforeSequenceWrite(obj, b.Kind);
            R result = ((Func<T, A, R>)b.Call)(obj, a); DirectDeco.AfterSequenceWrite(result, b.Kind, b.Writer); return result;
        }
    }
}
