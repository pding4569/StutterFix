using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StutterFix
{
    // (개발자용 검증) 장식 하나의 상태를 넓게 찍는다: 장식 스크립트의 모든 필드, 자식까지 모든 트랜스폼(위치·회전·크기·켜짐·레이어·이름),
    // 모든 컴포넌트(켜짐, 렌더러의 정렬·재질·그리기 끔, 스프라이트·색·마스크, 재질은 셰이더·렌더 큐·키워드·셰이더 속성 값 전부,
    // 충돌체의 크기·위치, 다른 스크립트의 모든 필드), 이미지 텍스처의 반복 방식. 매니저는 모든 필드(태그 목록은 순서까지).
    // "모드가 빠르게 한 결과" 와 "게임 원래 방식 결과" 를 같은 프레임에 찍어 이름별로 비교한다. 느리다(장식 하나 수십 us).
    internal static class StateDump
    {
        internal struct Item { public string Key; public long H; public Item(string k, long h) { Key = k; H = h; } }

        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, PropertyInfo[]> colliderProps = new Dictionary<Type, PropertyInfo[]>();
        private static readonly string[] ColliderPropNames = { "offset", "size", "radius", "isTrigger", "direction", "edgeRadius", "usedByComposite", "pathCount" };

        private static FieldInfo[] Fields(Type t)
        {
            FieldInfo[] f;
            if (fieldCache.TryGetValue(t, out f)) return f;
            var list = new List<FieldInfo>();
            for (var c = t; c != null && c != typeof(MonoBehaviour) && c != typeof(Behaviour) && c != typeof(Component) && c != typeof(object); c = c.BaseType)
                foreach (var fi in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    list.Add(fi);
            f = list.ToArray();
            fieldCache[t] = f;
            return f;
        }

        internal static long Hash(object v, int depth = 0)
        {
            unchecked
            {
                if (v == null) return 7;
                var uo = v as UnityEngine.Object;
                if (!ReferenceEquals(uo, null)) return uo == null ? -1 : uo.GetInstanceID();
                if (v is string) return v.GetHashCode();
                var t = v.GetType();
                if (t.IsPrimitive || t.IsEnum || t.IsValueType) return v.GetHashCode();
                if (v is Delegate) return 11;
                if (depth > 3) return 13;
                var dict = v as IDictionary;
                if (dict != null)
                {
                    long h = dict.Count * 1000003L;
                    foreach (DictionaryEntry e in dict) h += Hash(e.Key, depth + 1) * 31 + Hash(e.Value, depth + 1) * 17 + 1;   // 순서 무관
                    return h;
                }
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    long h = 99991;
                    foreach (var x in (IEnumerable)v) h += Hash(x, depth + 1) * 2654435761L + 1;   // 순서 무관
                    return h;
                }
                var en = v as IEnumerable;
                if (en != null)
                {
                    long h = 17; int n = 0;
                    foreach (var x in en) { h = h * 31 + Hash(x, depth + 1); n++; }   // 순서대로
                    return h * 7 + n;
                }
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v);   // 그 밖의 클래스(이벤트, 커스텀 스프라이트 등)는 같은 객체인지
            }
        }

        private static void AddFields(List<Item> o, string prefix, object obj)
        {
            foreach (var fi in Fields(obj.GetType()))
            {
                long h;
                try { h = Hash(fi.GetValue(obj)); } catch { h = 3; }
                o.Add(new Item(prefix + fi.Name, h));
            }
        }

        private static long MaterialHash(Material m)
        {
            if (m == null) return 7;
            unchecked
            {
                long h = m.GetInstanceID();
                var sh = m.shader;
                h = h * 31 + (sh == null ? 0 : sh.GetInstanceID());
                h = h * 31 + m.renderQueue;
                var kw = m.shaderKeywords; Array.Sort(kw, StringComparer.Ordinal);
                foreach (var k in kw) h = h * 31 + k.GetHashCode();
                if (sh != null)
                {
                    int n = sh.GetPropertyCount();
                    for (int i = 0; i < n; i++)
                    {
                        int id = sh.GetPropertyNameId(i);
                        if (!m.HasProperty(id)) continue;
                        switch (sh.GetPropertyType(i))
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Color: h = h * 31 + m.GetColor(id).GetHashCode(); break;
                            case UnityEngine.Rendering.ShaderPropertyType.Vector: h = h * 31 + m.GetVector(id).GetHashCode(); break;
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range: h = h * 31 + m.GetFloat(id).GetHashCode(); break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture: var tx = m.GetTexture(id); h = h * 31 + (tx == null ? 0 : tx.GetInstanceID()); break;
                            case UnityEngine.Rendering.ShaderPropertyType.Int: h = h * 31 + m.GetInteger(id); break;
                        }
                    }
                }
                return h;
            }
        }

        // 장식 하나
        internal static List<Item> Deco(scrDecoration d)
        {
            var o = new List<Item>(256);
            if ((object)d == null || d == null) { o.Add(new Item("null", 0)); return o; }
            AddFields(o, "f.", d);
            var ts = d.GetComponentsInChildren<Transform>(true);
            var typeCount = new Dictionary<string, int>();
            for (int k = 0; k < ts.Length; k++)
            {
                var t = ts[k];
                string p = "t" + k + ".";
                o.Add(new Item(p + "pos", t.localPosition.GetHashCode()));
                o.Add(new Item(p + "rot", t.localRotation.GetHashCode()));
                o.Add(new Item(p + "scl", t.localScale.GetHashCode()));
                o.Add(new Item(p + "wpos", t.position.GetHashCode()));
                o.Add(new Item(p + "act", t.gameObject.activeSelf ? 1 : 0));
                o.Add(new Item(p + "layer", t.gameObject.layer));
                o.Add(new Item(p + "name", t.gameObject.name.GetHashCode()));
                typeCount.Clear();
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    string tn = c.GetType().Name; int j; typeCount.TryGetValue(tn, out j); typeCount[tn] = j + 1;
                    string q = p + tn + j + ".";
                    var r = c as Renderer;
                    if (r != null)
                    {
                        o.Add(new Item(q + "en", r.enabled ? 1 : 0));
                        o.Add(new Item(q + "fro", r.forceRenderingOff ? 1 : 0));
                        o.Add(new Item(q + "layer", r.sortingLayerID));
                        o.Add(new Item(q + "order", r.sortingOrder));
                        var mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++) o.Add(new Item(q + "mat" + m, MaterialHash(mats[m])));
                        var sr = r as SpriteRenderer;
                        if (sr != null)
                        {
                            o.Add(new Item(q + "sprite", Hash(sr.sprite)));
                            o.Add(new Item(q + "color", sr.color.GetHashCode()));
                            o.Add(new Item(q + "mask", (int)sr.maskInteraction));
                            o.Add(new Item(q + "flip", (sr.flipX ? 1 : 0) + (sr.flipY ? 2 : 0)));
                            o.Add(new Item(q + "draw", (int)sr.drawMode * 1000003L + sr.size.GetHashCode()));
                            var tex = sr.sprite != null ? sr.sprite.texture : null;
                            if (tex != null) o.Add(new Item(q + "tex", (int)tex.wrapModeU * 10 + (int)tex.wrapModeV + (int)tex.filterMode * 100));
                        }
                        var sm = r as SpriteMask;
                        if (sm != null)
                        {
                            o.Add(new Item(q + "sprite", Hash(sm.sprite)));
                            o.Add(new Item(q + "range", (sm.isCustomRangeActive ? 1 : 0) + sm.frontSortingLayerID * 3L + sm.frontSortingOrder * 7L + sm.backSortingLayerID * 11L + sm.backSortingOrder * 13L));
                            o.Add(new Item(q + "cutoff", sm.alphaCutoff.GetHashCode()));
                        }
                        continue;
                    }
                    var b = c as Behaviour;
                    if (b != null) o.Add(new Item(q + "en", b.enabled ? 1 : 0));
                    if (ReferenceEquals(c, d)) continue;
                    if (c is MonoBehaviour) { AddFields(o, q + "f.", c); continue; }
                    // 충돌체 등 엔진 컴포넌트 (물리 2D 어셈블리를 참조하지 않아 이름으로)
                    PropertyInfo[] props;
                    var ct = c.GetType();
                    if (!colliderProps.TryGetValue(ct, out props))
                    {
                        var l = new List<PropertyInfo>();
                        foreach (var n in ColliderPropNames) { var pi = ct.GetProperty(n, BindingFlags.Instance | BindingFlags.Public); if (pi != null && pi.CanRead && pi.GetIndexParameters().Length == 0) l.Add(pi); }
                        props = l.ToArray(); colliderProps[ct] = props;
                    }
                    foreach (var pi in props) { long h; try { h = Hash(pi.GetValue(c, null)); } catch { h = 3; } o.Add(new Item(q + pi.Name, h)); }
                }
            }
            return o;
        }

        // 매니저 (모든 필드) + 마스크 캐시 다시 만들기 표시
        internal static List<Item> Manager(scrDecorationManager m)
        {
            var o = new List<Item>(64);
            if (m == null) return o;
            AddFields(o, "m.", m);
            o.Add(new Item("SpriteAlphaMaskUtils.doRefreshMaskCache", SpriteAlphaMaskUtils.doRefreshMaskCache ? 1 : 0));
            return o;
        }

        // 같은 순서로 찍은 두 목록을 이름별로 비교. 다른 이름을 byKey 에 센다(인덱스 k 는 이름에 남는다). 다른 항목 수를 돌려준다.
        internal static int Compare(List<Item> a, List<Item> b, Dictionary<string, int> byKey, List<string> firstKeys)
        {
            int diff = 0;
            if (a.Count != b.Count)
            {
                Count(byKey, "(항목 수 " + a.Count + " 대 " + b.Count + ")");
                // 이름으로 맞춰 본다
                var map = new Dictionary<string, long>();
                foreach (var it in b) map[it.Key] = it.H;
                foreach (var it in a) { long h; if (!map.TryGetValue(it.Key, out h) || h != it.H) { diff++; Count(byKey, it.Key); if (firstKeys != null && firstKeys.Count < 8) firstKeys.Add(it.Key); } }
                return Math.Max(diff, 1);
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Key == b[i].Key && a[i].H == b[i].H) continue;
                diff++;
                Count(byKey, a[i].Key == b[i].Key ? a[i].Key : a[i].Key + "|" + b[i].Key);
                if (firstKeys != null && firstKeys.Count < 8) firstKeys.Add(a[i].Key);
            }
            return diff;
        }
        private static void Count(Dictionary<string, int> d, string k) { int n; d.TryGetValue(k, out n); d[k] = n + 1; }

        internal static string Top(Dictionary<string, int> byKey, int max)
        {
            var l = new List<KeyValuePair<string, int>>(byKey);
            l.Sort((x, y) => y.Value.CompareTo(x.Value));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < l.Count && i < max; i++) sb.Append(i == 0 ? "" : ", ").Append(l[i].Key).Append(' ').Append(l[i].Value);
            if (l.Count > max) sb.Append(" 외 ").Append(l.Count - max).Append("가지");
            return sb.ToString();
        }
    }
}
