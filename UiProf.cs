using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StutterFix
{
    // (개발자용) 곡 중에 UI 가 매 프레임 무엇을 다시 만드는지 센다.
    // 논이펙 맵 측정(2.0.2): 엔진 단계 중 PlayerUpdateCanvases 가 프레임당 0.59ms 였다. 여기에는
    //   CanvasUpdateRegistry.PerformUpdate(레이아웃·그래픽 다시 만들기) 와 유니티 안쪽의 캔버스 묶기가 들어 있다.
    // 그래픽은 더러워졌을 때만 다시 만들어지므로(Graphic.Rebuild 는 등록된 것만 불림), 어떤 물체가 몇 번 불렸는지 보면
    // 매 프레임 헛으로 더러워지는 것을 찾을 수 있다.
    internal static class UiProf
    {
        private static bool installed;
        private static long performTicks, performCalls, graphicCalls, tmpCalls, layoutCalls, t0;
        private static int startFrame = -1;
        private static readonly Dictionary<int, long> counts = new Dictionary<int, long>();
        private static readonly Dictionary<int, string> names = new Dictionary<int, string>();

        internal static void Install(Harmony harmony)
        {
            if (!Edition.Dev || installed) return;
            try
            {
                var pu = AccessTools.Method(typeof(CanvasUpdateRegistry), "PerformUpdate");
                if (pu != null) harmony.Patch(pu, prefix: new HarmonyMethod(typeof(UiProf), nameof(PerfPre)), postfix: new HarmonyMethod(typeof(UiProf), nameof(PerfPost)));
                var gr = AccessTools.Method(typeof(Graphic), "Rebuild", new[] { typeof(CanvasUpdate) });
                if (gr != null) harmony.Patch(gr, prefix: new HarmonyMethod(typeof(UiProf), nameof(GraphicPre)), postfix: new HarmonyMethod(typeof(UiProf), nameof(GraphicPost)));
                var lr = AccessTools.Method(typeof(LayoutRebuilder), "Rebuild", new[] { typeof(CanvasUpdate) });
                if (lr != null) harmony.Patch(lr, prefix: new HarmonyMethod(typeof(UiProf), nameof(LayoutPre)));
                int tmp = 0;
                foreach (var tn in new[] { "TMPro.TextMeshProUGUI", "TMPro.TMP_SubMeshUI" })
                {
                    var t = AccessTools.TypeByName(tn);
                    var m = t == null ? null : AccessTools.DeclaredMethod(t, "Rebuild", new[] { typeof(CanvasUpdate) });
                    if (m != null) { harmony.Patch(m, prefix: new HarmonyMethod(typeof(UiProf), nameof(TmpPre)), postfix: new HarmonyMethod(typeof(UiProf), nameof(GraphicPost))); tmp++; }
                }
                InstallMore(harmony);
                installed = true;
                Main.Entry.Logger.Log("[UI 측정] 설치 (PerformUpdate " + (pu != null) + ", Graphic.Rebuild " + (gr != null) + ", 레이아웃 " + (lr != null) + ", TMP " + tmp + "개)");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[UI 측정] 설치 실패: " + ex.Message); }
        }

        // ── 보강: PerformUpdate 안을 쪼갠다 ──
        private static long cullTicks, cullT0, rebuildTicks, rebuildT0, realRebuilds, maskCulls, tmpInputRebuilds;
        private static readonly AccessTools.FieldRef<Graphic, bool> vertsDirtyRef = AccessTools.FieldRefAccess<Graphic, bool>("m_VertsDirty");
        private static readonly AccessTools.FieldRef<Graphic, bool> matDirtyRef = AccessTools.FieldRefAccess<Graphic, bool>("m_MaterialDirty");
        public static void CullPre() { cullT0 = Stopwatch.GetTimestamp(); }
        public static void CullPost() { cullTicks += Stopwatch.GetTimestamp() - cullT0; }
        public static void MaskCullPre() { maskCulls++; }
        public static void TmpInputRebuildPre(CanvasUpdate __0) { if (__0 == CanvasUpdate.PreRender) tmpInputRebuilds++; }
        private static void InstallMore(Harmony harmony)
        {
            var cr = AccessTools.Method(typeof(ClipperRegistry), "Cull");
            if (cr != null) harmony.Patch(cr, prefix: new HarmonyMethod(typeof(UiProf), nameof(CullPre)), postfix: new HarmonyMethod(typeof(UiProf), nameof(CullPost)));
            var mc = AccessTools.Method(typeof(MaskableGraphic), "Cull", new[] { typeof(Rect), typeof(bool) });
            if (mc != null) harmony.Patch(mc, prefix: new HarmonyMethod(typeof(UiProf), nameof(MaskCullPre)));
            var caret = AccessTools.TypeByName("TMPro.TMP_SelectionCaret");
            var cc = caret == null ? null : AccessTools.DeclaredMethod(caret, "Cull", new[] { typeof(Rect), typeof(bool) });
            if (cc != null) harmony.Patch(cc, prefix: new HarmonyMethod(typeof(UiProf), nameof(MaskCullPre)));
            var inp = AccessTools.TypeByName("TMPro.TMP_InputField");
            var ir = inp == null ? null : AccessTools.DeclaredMethod(inp, "Rebuild", new[] { typeof(CanvasUpdate) });
            if (ir != null) harmony.Patch(ir, prefix: new HarmonyMethod(typeof(UiProf), nameof(TmpInputRebuildPre)));
            // TMP 글꼴에 처음 보는 글자를 넣는 함수 (동적 글꼴: 글자 그림 그리기 + 텍스처 올리기)
            int fa = 0;
            var fat = AccessTools.TypeByName("TMPro.TMP_FontAsset");
            if (fat != null)
                foreach (var m in fat.GetMethods(AccessTools.all))
                {
                    if (m.DeclaringType != fat || m.IsAbstract || !(m.Name.StartsWith("TryAddCharacter") || m.Name.StartsWith("TryAddGlyph"))) continue;
                    try { harmony.Patch(m, prefix: new HarmonyMethod(typeof(UiProf), nameof(FontPre)), postfix: new HarmonyMethod(typeof(UiProf), nameof(FontPost))); fa++; } catch { }
                }
            Main.Entry.Logger.Log("[UI 측정] TMP 글자 넣기 함수 " + fa + "개 감쌈");
            Main.Entry.Logger.Log("[UI 측정] 보강 설치 (자르기 " + (cr != null) + ", Cull " + (mc != null) + "/" + (cc != null) + ", 입력칸 " + (ir != null) + ")");
        }
        private static string CanvasList()
        {
            var sb = new System.Text.StringBuilder(" | 켜진 최상위 캔버스:");
            try
            {
                foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!c.isRootCanvas || !c.isActiveAndEnabled) continue;
                    int g = c.GetComponentsInChildren<Graphic>(false).Length;
                    var cg = c.GetComponent<CanvasGroup>();
                    sb.AppendFormat(" [{0} {1} 그래픽 {2}개{3}]", c.name, c.renderMode, g, cg != null ? " 투명도 " + cg.alpha.ToString("F2") : "");
                }
            }
            catch (Exception ex) { sb.Append(" 실패 " + ex.Message); }
            return sb.ToString();
        }
        public static void PerfPre() { t0 = Stopwatch.GetTimestamp(); callRebuilds = 0; slow1 = slow2 = 0; slowName1 = slowName2 = null; callFontMs = 0; }
        // 한 번의 PerformUpdate 가 오래 걸린 경우: 그 안에서 가장 오래 걸린 다시 만들기 둘과 TMP 글자 넣기
        private static int callRebuilds, heavyLogged, fontLogged;
        private static double slow1, slow2, callFontMs;
        private static string slowName1, slowName2;
        private static int fontDepth; private static long fontT0;
        public static void FontPre() { if (fontDepth++ == 0) fontT0 = Stopwatch.GetTimestamp(); }
        public static void FontPost(UnityEngine.Object __instance, MethodBase __originalMethod)
        {
            if (--fontDepth != 0) return;
            double ms = (Stopwatch.GetTimestamp() - fontT0) * 1000.0 / Stopwatch.Frequency;
            callFontMs += ms;
            if (ms > 1 && fontLogged < 40) { fontLogged++; Main.Entry.Logger.Log(string.Format("[UI 측정] TMP 글자 넣기 {0} '{1}' {2:F1}ms, 곡 중 {3}, 실시간 {4:F1}초", __originalMethod.Name, __instance != null ? __instance.name : "?", ms, Hitch.Playing, Time.realtimeSinceStartup)); }
        }
        private static string snap; // 곡 도중(시작 1000프레임 뒤) 한 번 찍은 캔버스 목록 (곡 끝에 찍으면 에디터가 다시 나온 뒤라)
        public static void PerfPost()
        {
            double callMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (callMs > 5 && heavyLogged < 40 && Hitch.Playing)
            {
                heavyLogged++;
                Main.Entry.Logger.Log(string.Format("[UI 측정] 무거운 UI 갱신 {0:F1}ms (다시 만들기 {1}번, TMP 글자 넣기 {2:F1}ms) 가장 오래: {3} {4:F1}ms, {5} {6:F1}ms | 실시간 {7:F1}초",
                    callMs, callRebuilds, callFontMs, slowName1 ?? "-", slow1, slowName2 ?? "-", slow2, Time.realtimeSinceStartup));
            }
            performTicks += Stopwatch.GetTimestamp() - t0; performCalls++; if (snap == null && startFrame >= 0 && Hitch.Playing && Time.frameCount - startFrame > 1000) snap = CanvasList(); }
        public static void GraphicPre(Component __instance, CanvasUpdate __0) { lastRebuilt = __instance; var g = __instance as Graphic; if (g == null) { rebuildT0 = Stopwatch.GetTimestamp(); return; } if (__0 == CanvasUpdate.PreRender) { graphicCalls++; if (vertsDirtyRef(g) || matDirtyRef(g)) { realRebuilds++; Count(g); } } rebuildT0 = Stopwatch.GetTimestamp(); }
        private static Component lastRebuilt;
        public static void GraphicPost()
        {
            long d = Stopwatch.GetTimestamp() - rebuildT0;
            rebuildTicks += d; callRebuilds++;
            double ms = d * 1000.0 / Stopwatch.Frequency;
            if (ms > slow2 && (object)lastRebuilt != null)
            {
                string n = lastRebuilt.name + " (" + lastRebuilt.GetType().Name + ")";
                var p = lastRebuilt.transform.parent; if (p != null) n = p.name + "/" + n;
                if (ms > slow1) { slow2 = slow1; slowName2 = slowName1; slow1 = ms; slowName1 = n; } else { slow2 = ms; slowName2 = n; }
            }
        }
        public static void TmpPre(Component __instance, CanvasUpdate __0) { if (__0 == CanvasUpdate.PreRender) { tmpCalls++; Count(__instance); } lastRebuilt = __instance; rebuildT0 = Stopwatch.GetTimestamp(); }
        public static void LayoutPre(CanvasUpdate __0) { if (__0 == CanvasUpdate.Layout) layoutCalls++; }

        private static void Count(Component c)
        {
            if (!Hitch.Playing || (object)c == null) return;
            int id = c.GetInstanceID();
            long n; counts.TryGetValue(id, out n); counts[id] = n + 1;
            if (!names.ContainsKey(id))
            {
                string path = c.name;
                var p = c.transform.parent;
                for (int i = 0; i < 2 && p != null; i++, p = p.parent) path = p.name + "/" + path;
                names[id] = path + " (" + c.GetType().Name + ")";
            }
        }

        internal static void ResetSong()
        {
            if (!installed) return;
            performTicks = performCalls = graphicCalls = tmpCalls = layoutCalls = cullTicks = rebuildTicks = realRebuilds = maskCulls = tmpInputRebuilds = 0;
            counts.Clear(); names.Clear();
            startFrame = Time.frameCount;
        }

        internal static void ReportSong()
        {
            if (!installed || startFrame < 0) return;
            long frames = Math.Max(1, Time.frameCount - startFrame);
            double ms = performTicks * 1000.0 / Stopwatch.Frequency;
            var top = new List<KeyValuePair<int, long>>(counts);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder();
            double f = frames, tk = Stopwatch.Frequency / 1000.0;
            sb.AppendFormat("[UI 측정] 곡 {0}프레임: PerformUpdate 프레임당 {1:F3}ms (그중 자르기 {5:F3}ms, 그래픽 다시 만들기 {6:F3}ms) | 그래픽 다시 만들기 불림 {2:F2}번/프레임 중 실제로 더러운 것 {7:F2}번, TMP 글자 {3:F2}번, TMP 입력칸 {8:F2}번, 레이아웃 {4:F2}번, Cull 불림 {9:F1}번/프레임 | 많이 다시 만든 것(실제로 더러운 것):",
                frames, ms / frames, graphicCalls / f, tmpCalls / f, layoutCalls / f, cullTicks / tk / f, rebuildTicks / tk / f, realRebuilds / f, tmpInputRebuilds / f, maskCulls / f);
            for (int i = 0; i < top.Count && i < 12; i++)
                sb.AppendFormat(" [{0} {1:F2}/프레임]", names[top[i].Key], (double)top[i].Value / frames);
            sb.Append(snap ?? " | (곡 도중 캔버스 목록 없음)"); snap = null;
            Main.Entry.Logger.Log(sb.ToString());
            startFrame = -1;
        }
    }
}
