using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace StutterFix
{
    // (개발자용 자동 시험 whoholds) 지워진 유니티 물체(예: 지워진 타일)를 관리 코드에서 누가 붙잡고 있는지 찾는다.
    // 모든 어셈블리의 정적 필드에서 출발해 객체의 필드·배열을 따라가며(너비 우선), 찾는 형식의 "지워진" 물체에 닿으면 경로를 남긴다.
    internal static class HeapPath
    {
        private sealed class RefEq : IEqualityComparer<object>
        {
            internal static readonly RefEq I = new RefEq();
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        private static bool Skip(Type ft) { var ns = ft.Namespace ?? ""; if (ns.StartsWith("System.Reflection") || ns.StartsWith("System.Runtime") || ns.StartsWith("Mono") || ns.StartsWith("System.Threading") || ns.StartsWith("Unity.Collections") || typeof(Type).IsAssignableFrom(ft) || typeof(System.Reflection.MemberInfo).IsAssignableFrom(ft)) return true; return ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft.IsPointer || ft.IsByRef || ft == typeof(IntPtr) || ft == typeof(UIntPtr) || ft == typeof(TypedReference); }
        private static FieldInfo[] Fields(Type t)
        {
            FieldInfo[] f;
            if (fieldCache.TryGetValue(t, out f)) return f;
            var l = new List<FieldInfo>();
            for (var x = t; x != null && x != typeof(object); x = x.BaseType)
                foreach (var fi in x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!Skip(fi.FieldType)) l.Add(fi);
            f = l.ToArray();
            fieldCache[t] = f;
            return f;
        }

        internal static Func<object, bool> SkipInto;   // 이 객체 안으로는 들어가지 않는다 (살아 있는 타일 등, 탐색 한도 아끼기)
        private static Dictionary<object, KeyValuePair<object, string>> parent;
        private static Queue<object> queue;

        private static void Push(object v, object from, string how)
        {
            if (v == null || parent.ContainsKey(v)) return;
            if (Skip(v.GetType())) return;
            parent[v] = new KeyValuePair<object, string>(from, how);
            queue.Enqueue(v);
        }

        internal static string Find(Type target, int maxObjects, int maxHits) { return Find(o => target.IsInstanceOfType(o) && o is UnityEngine.Object uo && uo == null, target.Name, maxObjects, maxHits); }
        internal static string Find(Func<object, bool> isTarget, string what, int maxObjects, int maxHits) { return Find(isTarget, what, maxObjects, maxHits, null, null); }
        // roots 가 있으면 그것에서만 출발, asmFilter 가 있으면 그 어셈블리의 정적 필드에서만 출발
        internal static string Find(Func<object, bool> isTarget, string what, int maxObjects, int maxHits, List<KeyValuePair<object, string>> roots, Func<Assembly, bool> asmFilter)
        {
            parent = new Dictionary<object, KeyValuePair<object, string>>(RefEq.I);
            queue = new Queue<object>();
            if (roots != null) foreach (var r in roots) Push(r.Key, null, r.Value);
            if (roots == null) foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                if (asmFilter != null && !asmFilter(asm)) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.ContainsGenericParameters) continue;
                    FieldInfo[] sf;
                    try { sf = t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); } catch { continue; }
                    foreach (var fi in sf)
                    {
                        if (fi.IsLiteral || Skip(fi.FieldType)) continue;
                        try { Push(fi.GetValue(null), null, t.FullName + "." + fi.Name); } catch { }
                    }
                }
            }
            var hitRoots = new Dictionary<string, int>();
            int seen = 0;
            while (queue.Count > 0 && seen < maxObjects && hitRoots.Count < maxHits)
            {
                var o = queue.Dequeue();
                seen++;
                try
                {
                    var t = o.GetType();
                    if (isTarget(o))
                    {
                        string norm = System.Text.RegularExpressions.Regex.Replace(Path(o), @"\[\d+\]", "[]");
                        string key = norm.Length > 240 ? norm.Substring(norm.Length - 240) : norm;
                        int c; hitRoots.TryGetValue(key, out c); hitRoots[key] = c + 1;
                        continue;   // 그 너머는 안 본다
                    }
                    if (SkipInto != null && SkipInto(o)) continue;
                    if (o is Delegate dg) { foreach (var inv in dg.GetInvocationList()) Push(inv.Target, o, "delegate:" + inv.Method.Name); continue; }
                    if (t.IsArray)
                    {
                        var et = t.GetElementType();
                        if (Skip(et)) continue;
                        var arr = (Array)o;
                        if (arr.Rank != 1) continue;
                        for (int i = 0; i < arr.Length; i++) Push(arr.GetValue(i), o, "[" + i + "]");
                        continue;
                    }
                    foreach (var fi in Fields(t))
                    {
                        object v;
                        try { v = fi.GetValue(o); } catch { continue; }
                        Push(v, o, fi.Name);
                    }
                }
                catch { }
            }
            var sb = new System.Text.StringBuilder("살펴본 객체 " + seen + "개, " + what + " 경로 " + hitRoots.Count + "가지:");
            foreach (var kv in hitRoots) sb.Append("\n  ").Append(kv.Value).Append("개: ").Append(kv.Key);
            parent = null; queue = null;
            return sb.ToString();
        }

        private static string Path(object o)
        {
            var parts = new List<string>();
            var cur = o;
            for (int n = 0; n < 40 && cur != null; n++)
            {
                KeyValuePair<object, string> p;
                if (!parent.TryGetValue(cur, out p)) break;
                parts.Add(p.Value + (p.Key != null ? "(" + p.Key.GetType().Name + ")" : ""));
                cur = p.Key;
            }
            parts.Reverse();
            return string.Join(" > ", parts.ToArray());
        }
    }
}
