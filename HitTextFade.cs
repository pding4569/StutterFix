using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace StutterFix
{
    // 판정 글자가 사라질 때(투명해질 때) 글자 메시 전체 대신 정점 색만 바꾼다.
    //
    // 판정 글자(scrHitTextMesh, TextMeshPro 3글자 안팎)는 띄운 뒤 0.5초부터 0.7초 동안 DOFade 로 투명해진다. TMP 는 색·투명도만 바뀌어도
    // m_havePropertiesChanged 를 세워 다음 그리기 전에 메시 전체를 다시 만든다(글자 해석·배치·정점·UV). 직접 플레이하면 판정마다 글자가
    // 떠서(자동 플레이는 띄우지 않음) 빠른 맵에서 20~25개가 매 프레임 다시 만들어진다(하나 약 10us/프레임, 게임 안 시험).
    // 색 태그·그라디언트가 없는 글자는 TMP 가 모든 글자의 네 정점에 같은 색(m_fontColor32)을 넣으므로(TMP 3 GenerateTextMesh), 투명도만
    // 바뀐 경우에는 그 색만 바꿔 넣으면 전체를 다시 만든 것과 같다. 단 다시 만들 때는 그 순간의 크기로 SDF 선명도 값(uv0.w)도 새로
    // 계산하므로, 마지막으로 전체를 만든 때와 크기(lossyScale.y)가 정확히 같을 때만 이 길을 쓴다. 다른 바뀐 것이 이미 있거나(Quartz 의
    // 판정 글꼴 바꾸기 등), 꺼져 있거나, 글자에 '<' 가 있으면 원래대로 둔다.
    // 개발자용: 이 길로 바꾼 뒤 64번에 한 번 전체를 다시 만들어 정점·UV·색을 비교한다.
    internal static class HitTextFade
    {
        internal static bool Enabled = true;
        internal static long Fast, Slow, DevChecks, DevDiff; internal static string DevFirst;

        private static readonly HashSet<TMP_Text> hitTexts = new HashSet<TMP_Text>();
        private static readonly Dictionary<TMP_Text, float> genLossy = new Dictionary<TMP_Text, float>();

        private static readonly AccessTools.FieldRef<TMP_Text, Color> fontColor = AccessTools.FieldRefAccess<TMP_Text, Color>("m_fontColor");
        private static readonly AccessTools.FieldRef<TMP_Text, Color32> fontColor32 = AccessTools.FieldRefAccess<TMP_Text, Color32>("m_fontColor32");
        private static readonly AccessTools.FieldRef<TMP_Text, bool> changed = AccessTools.FieldRefAccess<TMP_Text, bool>("m_havePropertiesChanged");
        private static readonly AccessTools.FieldRef<TMP_Text, bool> layoutDirty = AccessTools.FieldRefAccess<TMP_Text, bool>("m_isLayoutDirty");
        private static readonly AccessTools.FieldRef<TMP_Text, bool> gradient = AccessTools.FieldRefAccess<TMP_Text, bool>("m_enableVertexGradient");

        internal static void Install(Harmony h)
        {
            try
            {
                var init = AccessTools.Method(typeof(scrHitTextMesh), "Init");
                var setColor = AccessTools.PropertySetter(typeof(TMP_Text), "color");
                var setAlpha = AccessTools.PropertySetter(typeof(TMP_Text), "alpha");
                var gen = AccessTools.Method(typeof(TextMeshPro), "GenerateTextMesh");
                if (init == null || setColor == null || setAlpha == null || gen == null) { Main.Entry.Logger.Log("[판정 글자] 게임 코드 모양이 달라 끔"); return; }
                h.Patch(init, postfix: new HarmonyMethod(typeof(HitTextFade), nameof(InitPostfix)));
                h.Patch(gen, postfix: new HarmonyMethod(typeof(HitTextFade), nameof(GenPostfix)));
                h.Patch(setColor, prefix: new HarmonyMethod(typeof(HitTextFade), nameof(ColorPrefix)) { priority = Priority.Last });
                h.Patch(setAlpha, prefix: new HarmonyMethod(typeof(HitTextFade), nameof(AlphaPrefix)) { priority = Priority.Last });
                // 이미 만들어진 판정 글자(모드를 다시 불러온 경우)
                foreach (var m in UnityEngine.Object.FindObjectsOfType<scrHitTextMesh>(true)) if (m.text != null) hitTexts.Add(m.text);
                Main.Entry.Logger.Log("[판정 글자] 사라질 때 색만 바꾸기 설치");
            }
            catch (Exception ex) { Enabled = false; Main.Entry.Logger.Log("[판정 글자] 설치 실패: " + ex.Message); }
        }

        internal static void Unload() { hitTexts.Clear(); genLossy.Clear(); }

        public static void InitPostfix(scrHitTextMesh __instance)
        {
            if (__instance != null && __instance.text != null) { hitTexts.Add(__instance.text); genLossy.Remove(__instance.text); }
        }

        public static void GenPostfix(TextMeshPro __instance)
        {
            if (hitTexts.Contains(__instance)) genLossy[__instance] = __instance.transform.lossyScale.y;
        }

        public static bool ColorPrefix(TMP_Text __instance, Color value) { return !TryFast(__instance, value); }

        public static bool AlphaPrefix(TMP_Text __instance, float value)
        {
            Color c = fontColor(__instance); c.a = value;
            return !TryFast(__instance, c);
        }

        // true = 정점 색만 바꿨다 (원래 setter 는 건너뜀)
        private static bool TryFast(TMP_Text t, Color value)
        {
            if (!Enabled || t == null || !hitTexts.Contains(t)) return false;
            try
            {
                if (fontColor(t) == value) return false;   // 원래도 아무것도 안 한다
                float lossy;
                var info = t.textInfo;
                if (changed(t) || layoutDirty(t) || gradient(t) || !t.isActiveAndEnabled || info == null || info.characterCount <= 0 || info.materialCount != 1
                    || !genLossy.TryGetValue(t, out lossy) || lossy != t.transform.lossyScale.y || t.font == null
                    || ((int)t.font.atlasRenderMode & 65536) != 0 || (t.text != null && t.text.IndexOf('<') >= 0))
                { Slow++; return false; }

                fontColor(t) = value;
                Color32 c = value;
                fontColor32(t) = c;
                var mesh = info.meshInfo[0];
                var colors = mesh.colors32;
                var chars = info.characterInfo;
                for (int i = 0; i < info.characterCount; i++)
                {
                    if (!chars[i].isVisible) continue;
                    chars[i].vertex_BL.color = c; chars[i].vertex_TL.color = c; chars[i].vertex_TR.color = c; chars[i].vertex_BR.color = c;
                    int v = chars[i].vertexIndex;
                    if (colors == null || v + 3 >= colors.Length) { Slow++; changed(t) = true; t.SetVerticesDirty(); return true; }
                    colors[v] = c; colors[v + 1] = c; colors[v + 2] = c; colors[v + 3] = c;
                }
                t.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                Fast++;
                if (Edition.Dev && (Fast & 63) == 0) DevCompare(t);
                return true;
            }
            catch { return false; }
        }

        // 개발자용: 지금 메시를 복사해 두고 전체를 다시 만들어 비교
        private static void DevCompare(TMP_Text t)
        {
            try
            {
                var m = t.textInfo.meshInfo[0];
                var v = (Vector3[])m.vertices.Clone(); var u0 = (Vector4[])m.uvs0.Clone(); var u2 = (Vector2[])m.uvs2.Clone(); var c = (Color32[])m.colors32.Clone();
                t.ForceMeshUpdate();
                var n = t.textInfo.meshInfo[0];
                int count = Math.Min(t.textInfo.characterCount * 4, Math.Min(v.Length, n.vertices.Length));
                DevChecks++;
                for (int i = 0; i < count; i++)
                {
                    string why = null;
                    if (v[i] != n.vertices[i]) why = "정점";
                    else if (u0[i] != n.uvs0[i]) why = "UV0";
                    else if (u2[i] != n.uvs2[i]) why = "UV2";
                    else if (!c[i].Equals(n.colors32[i])) why = "색 " + c[i] + "/" + n.colors32[i];
                    if (why != null) { DevDiff++; if (DevFirst == null) DevFirst = why + " #" + i; break; }
                }
            }
            catch (Exception ex) { DevDiff++; if (DevFirst == null) DevFirst = "비교 실패: " + ex.Message; }
        }

        internal static string Summary()
        {
            if (Fast + Slow == 0) return "";
            string s = string.Format("판정 글자 사라질 때 색만 바꿈 {0}번 (원래대로 {1}번)", Fast, Slow);
            if (Edition.Dev) s += string.Format(", 전체 다시 만든 것과 비교 {0}번 다름 {1}{2}", DevChecks, DevDiff, DevFirst != null ? " (" + DevFirst + ")" : "");
            Fast = Slow = 0;
            return s;
        }
    }
}
