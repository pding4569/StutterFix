using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StutterFix
{
    // Reflection only: the same plan can be checked against the installed DLL
    // without running game methods, Unity native calls, or Harmony detours.
    internal static class LiveDecoPatchPlan
    {
        const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal sealed class Entry
        {
            internal readonly MethodInfo Method;
            internal readonly string Prefix;
            internal Entry(MethodInfo method, string prefix) { Method = method; Prefix = prefix; }
        }
        internal sealed class Plan
        {
            internal readonly List<Entry> Entries = new List<Entry>();
            internal readonly List<MethodInfo> AbstractDeclarations = new List<MethodInfo>();
        }
        internal static Plan Build(Type decoration, Type visual, Type parallax, Type vector2, Type color)
        {
            var plan = new Plan();
            Add(decoration, "UpdatePosition", Type.EmptyTypes, "PositionPrefix");
            Add(decoration, "SetRotation", new[] { typeof(float) }, "RotationPrefix");
            Add(decoration, "SetScale", new[] { vector2 }, "ScalePrefix");
            Add(decoration, "SetPosition", new[] { vector2, vector2 }, "SetPositionPrefix");
            Add(decoration, "SetColor", new[] { color }, "ColorPrefix");
            Add(decoration, "SetOpacity", new[] { typeof(float) }, "OpacityPrefix");
            Add(visual, "ApplyColor", Type.EmptyTypes, "ApplyColorPrefix");
            Add(parallax, "SetTrans", Type.EmptyTypes, "ParallaxPrefix");
            var structural = new HashSet<string>(new[] { "Setup", "SetSprite", "SetDepth", "SetTile", "SetBlendMode", "SetMaskingType", "SetParallax", "SetVisible", "UpdateHitboxState" }, StringComparer.Ordinal);
            foreach (var type in new[] { decoration, visual })
                foreach (var method in type.GetMethods(Declared))
                {
                    if (!structural.Contains(method.Name)) continue;
                    if (method.IsAbstract)
                    {
                        // Abstract declarations cannot be detoured. Require the
                        // concrete visual override so ownership is still released.
                        var implementation = visual.GetMethod(method.Name, Declared, null,
                            method.GetParameters().Select(p => p.ParameterType).ToArray(), null);
                        RequireBody(implementation, type.Name + "." + method.Name + " override");
                        if (implementation.GetBaseDefinition() != method.GetBaseDefinition())
                            throw new InvalidOperationException("patch-override-slot " + type.Name + "." + method.Name);
                        plan.AbstractDeclarations.Add(method);
                        continue;
                    }
                    Add(type, method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray(), "StructuralPrefix");
                }
            return plan;

            void Add(Type type, string name, Type[] args, string prefix)
            {
                var found = type.GetMethods(Declared).Where(m => m.Name == name && !m.IsStatic && !m.ContainsGenericParameters
                    && m.ReturnType == typeof(void) && (m.CallingConvention & CallingConventions.HasThis) != 0
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(args)).ToArray();
                if (found.Length != 1) throw new InvalidOperationException("patch-target-count=" + found.Length + " " + type.Name + "." + name);
                RequireBody(found[0], type.Name + "." + name);
                plan.Entries.Add(new Entry(found[0], prefix));
            }
        }
        static void RequireBody(MethodInfo method, string label)
        {
            if (method == null || method.IsAbstract || method.IsStatic || method.ContainsGenericParameters
                || method.ReturnType != typeof(void) || (method.CallingConvention & CallingConventions.HasThis) == 0
                || method.GetMethodBody() == null)
                throw new InvalidOperationException("patch-target-no-managed-body " + label);
        }
    }
}
