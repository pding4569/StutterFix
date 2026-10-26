using System;
using System.IO;
using UnityEngine;

namespace StutterFix
{
    // 전체 화면 런처 설정창 (개발 중, 설정 UiLauncher 가 켜져 있을 때만).
    // 확정한 모양(v12): 어두운 전체 화면 · 가운데 StutterFix 글자(Syne 로 미리 그린 그림)와 움직이는 엠블럼 · 떠다니는 입자 ·
    // 아래 아이콘 줄이 차례로 올라온다 · 아이콘을 누르면 글자가 오른쪽 위로 작아져 가고 박스가 열린다(배경 없이 테두리 전체 + 모서리 괄호).
    // 박스 안은 기존 페이지 그리기(DrawContents)를 그대로 쓴다. 예시 사진·빨간색은 쓰지 않는다.
    public partial class SettingsWindow
    {
        internal static bool UseLauncher { get { return Main.Config != null && Main.Config.UiLauncher; } }

        private static Texture2D tWordmark, tEmblem, tFeather;
        private float launchPageT = 1f; private int launchPageLast = -1;
        private static bool launchTexTried;
        private static GUIStyle sLaunchBox;

        // 입자(결정적 난수라 매번 같은 배치)
        private const int ParticleMax = 72;
        private static float[] pX, pY, pSpeed, pSize, pPhase, pAlpha;

        private static Texture2D LoadUiTex(string name)
        {
            try
            {
                using (var s = typeof(SettingsWindow).Assembly.GetManifestResourceStream("StutterFix.ui." + name))
                {
                    if (s == null) return null;
                    var b = new byte[s.Length]; int got = 0;
                    while (got < b.Length) { int n = s.Read(b, got, b.Length - got); if (n <= 0) break; got += n; }
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    if (!t.LoadImage(b, false)) { UnityEngine.Object.Destroy(t); return null; }
                    return t;
                }
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[런처] 그림 불러오기 실패 " + name + ": " + ex.Message); return null; }
        }

        private void EnsureLauncherAssets()
        {
            if (launchTexTried) return;
            launchTexTried = true;
            tWordmark = LoadUiTex("wordmark.png");
            tEmblem = LoadUiTex("emblem.png");
            var r = new System.Random(7);
            pX = new float[ParticleMax]; pY = new float[ParticleMax]; pSpeed = new float[ParticleMax]; pSize = new float[ParticleMax]; pPhase = new float[ParticleMax]; pAlpha = new float[ParticleMax];
            for (int i = 0; i < ParticleMax; i++)
            {
                pX[i] = (float)r.NextDouble(); pY[i] = (float)r.NextDouble();
                pSpeed[i] = 0.004f + (float)r.NextDouble() * 0.014f;
                pSize[i] = 1.5f + (float)r.NextDouble() * 2.5f;
                pPhase[i] = (float)r.NextDouble() * 6.283f;
                pAlpha[i] = 0.10f + (float)r.NextDouble() * 0.30f;
            }
            sLaunchBox = new GUIStyle(GUIStyle.none);
            // 가로로 가운데 1, 양 끝 0 으로 부드럽게 줄어드는 띠
            tFeather = new Texture2D(64, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 64; i++) { float x = i / 63f; float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(x, 1f - x) / 0.26f)); tFeather.SetPixel(i, 0, new Color(1f, 1f, 1f, a)); }
            tFeather.Apply(false);   // 박스 배경 없음: 테두리는 직접 그린다
        }

        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        // 가는 테두리 전체 + 모서리 괄호
        private void DrawBoxFrame(Rect r, float a)
        {
            if (Event.current.type != EventType.Repaint) return;
            var line = new Color(1f, 1f, 1f, 0.13f * a);
            GUI.color = line;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), tWhite);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), tWhite);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), tWhite);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), tWhite);
            GUI.color = new Color(1f, 1f, 1f, 0.62f * a);
            const float L = 18f, T = 2f;
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, L, T), tWhite); GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, T, L), tWhite);
            GUI.DrawTexture(new Rect(r.xMax - L + 1, r.y - 1, L, T), tWhite); GUI.DrawTexture(new Rect(r.xMax - T + 1, r.y - 1, T, L), tWhite);
            GUI.DrawTexture(new Rect(r.x - 1, r.yMax - T + 1, L, T), tWhite); GUI.DrawTexture(new Rect(r.x - 1, r.yMax - L + 1, T, L), tWhite);
            GUI.DrawTexture(new Rect(r.xMax - L + 1, r.yMax - T + 1, L, T), tWhite); GUI.DrawTexture(new Rect(r.xMax - T + 1, r.yMax - L + 1, T, L), tWhite);
        }

        private void DrawLauncher(float sw, float sh, float e, float pe)
        {
            EnsureLauncherAssets();
            float t = Time.unscaledTime;
            var ev = Event.current;
            bool repaint = ev.type == EventType.Repaint;
            var full = new Rect(0, 0, sw, sh);

            // 뒤 게임 입력 막기(전체 화면)
            if (closing) UiInputBlock.Clear(this);
            else UiInputBlock.Place(this, new Rect(0, 0, Screen.width, Screen.height));

            // ── 배경: 거의 검게, 입자
            if (repaint)
            {
                GUI.color = new Color(0.02f, 0.021f, 0.026f, 0.78f * e);
                GUI.DrawTexture(full, tWhite);
                // 내용이 놓이는 가운데 열 뒤에만 더 어둡고 가장자리가 부드러운 띠(글자가 게임 화면과 섞여 안 읽히는 것을 막는다)
                if (tFeather != null && pe > 0.001f)
                {
                    float cw = Mathf.Min(1180f, sw - 40f);
                    GUI.color = new Color(0.02f, 0.021f, 0.026f, 0.975f * e * pe);
                    GUI.DrawTexture(new Rect((sw - cw) / 2f, 0, cw, sh), tFeather);
                }
                int n = Hitch.Playing ? 24 : ParticleMax;   // 곡 중에는 줄인다
                float spd = Hitch.Playing ? 0.5f : 1f;
                for (int i = 0; i < n; i++)
                {
                    float y = pY[i] - t * pSpeed[i] * spd; y -= Mathf.Floor(y);
                    float x = pX[i] + Mathf.Sin(t * 0.35f + pPhase[i]) * 0.012f;
                    float tw = 0.65f + 0.35f * Mathf.Sin(t * 1.3f + pPhase[i] * 2f);
                    GUI.color = new Color(1f, 1f, 1f, pAlpha[i] * tw * e);
                    GUI.DrawTexture(new Rect(x * sw, y * sh, pSize[i], pSize[i]), tWhite);
                }
            }

            // ── 글자와 엠블럼 (패널이 열리면 글자는 오른쪽 위로 작아진다)
            if (repaint && tWordmark != null)
            {
                float aspect = (float)tWordmark.height / tWordmark.width;
                float bigW = Mathf.Min(560f, sw * 0.5f), smallW = 150f;
                float w = Lerp(bigW, smallW, pe);
                float cx = Lerp(sw / 2f, sw - 40f - smallW / 2f, pe), cy = Lerp(sh * 0.43f, 54f, pe);
                float ap = Mathf.Clamp01(e * 1.4f);
                GUI.color = new Color(0.93f, 0.94f, 0.96f, ap);
                GUI.DrawTexture(new Rect(cx - w / 2f, cy - w * aspect / 2f, w, w * aspect), tWordmark);
                if (tEmblem != null)
                {
                    float es = Lerp(132f, 132f, pe) * (1f + 0.025f * Mathf.Sin(t * 1.1f));
                    var ec = new Vector2(sw / 2f, sh * 0.43f - bigW * aspect / 2f - 96f);
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(5f * Mathf.Sin(t * 0.55f), ec);
                    GUI.color = new Color(0.93f, 0.94f, 0.96f, Mathf.Clamp01(e * 1.2f) * (1f - EaseOut(pe)));
                    if (1f - EaseOut(pe) > 0.01f) GUI.DrawTexture(new Rect(ec.x - es / 2f, ec.y - es / 2f, es, es), tEmblem);
                    GUI.matrix = old;
                }
            }

            // ── 아래 아이콘 줄: 차례로 올라온다
            const float IS = 48f, GAP = 12f;
            int slots = DockOrder.Length;
            float dockW = slots * IS + (slots - 1) * GAP;
            float dockX = (sw - dockW) / 2f, dockY = sh - 92f;
            var dockRect = new Rect(dockX - 16, dockY - 16, dockW + 32, IS + 32);
            var names = PageNames();
            var m = ev.mousePosition;
            int hover = -1;
            for (int slot = 0; slot < slots; slot++)
            {
                int i = DockOrder[slot];
                float ap = Mathf.Clamp01(e * 1.7f - slot * 0.075f);
                float ease = EaseOut(ap);
                var r = new Rect(dockX + slot * (IS + GAP), dockY + (1f - ease) * 30f, IS, IS);
                bool on = panelOpen && page == i;
                bool hov = r.Contains(m) && ap > 0.6f;
                if (hov) hover = i;
                if (repaint)
                {
                    if (on) Fill(r, new Color(1f, 1f, 1f, 0.16f * ease), 12);
                    else if (hov) Fill(r, new Color(1f, 1f, 1f, 0.08f * ease), 12);
                    if (icons != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, ease * (on || hov ? 1f : 0.62f));
                        GUI.DrawTexture(new Rect(r.x + 11, r.y + 11, IS - 22, IS - 22), icons[i >= 8 ? i + 1 : i]);
                    }
                    if (on) Fill(new Rect(r.center.x - 8, r.yMax + 6, 16, 2), new Color(1f, 1f, 1f, 0.8f * ease), 1);
                    if (i == 0 && Updater.Available) Fill(new Rect(r.xMax - 12, r.y + 4, 7, 7), new Color(1f, 1f, 1f, ease), 4);
                }
                if (ap > 0.6f && GUI.Button(r, GUIContent.none, GUIStyle.none)) TogglePanel(i);
            }
            if (repaint && hover >= 0 && panelT <= 0.01f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.7f * e);
                GUI.Label(new Rect(0, dockY - 34, sw, 22), names[hover], sTip);
            }

            // ── 박스
            Rect box = Rect.zero;
            if (panelT > 0f)
            {
                // 페이지가 바뀌면(아이콘을 다른 것으로 눌렀을 때) 내용이 아래에서 떠오르는 애니메이션을 다시 시작한다
                if (page != launchPageLast) { launchPageLast = page; launchPageT = pe >= 0.999f ? 0f : 1f; }
                if (repaint) launchPageT = Mathf.MoveTowards(launchPageT, 1f, Time.unscaledDeltaTime / 0.34f);
                float pg = EaseOut(launchPageT);
                float topPad = Mathf.Max(84f, sh * 0.10f);
                float bw = Mathf.Min(900f, sw - 120f);
                float bh = Mathf.Min(680f, dockY - 56f - topPad);
                float rise = (1f - pe * pg) * 26f;   // 열릴 때와 바뀔 때 아래에서 올라온다
                box = new Rect((sw - bw) / 2f, topPad + rise, bw, Mathf.Max(260f, bh));
                rect = box;
                float a = e * pe * pg;
                windowAlpha = a;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Window(0x5F1A, box, DrawWindow, GUIContent.none, sLaunchBox);
            }

            // ── 닫기: Esc, 박스·아이콘 줄 바깥 클릭
            if (!closing)
            {
                if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape && !EditingText)
                {
                    ev.Use();
                    if (panelOpen) TogglePanel(page); else SetOpen(false);
                }
                else if (ev.type == EventType.MouseDown && !dockRect.Contains(ev.mousePosition) && !(panelT > 0f && box.Contains(ev.mousePosition)))
                {
                    if (panelOpen) TogglePanel(page); else SetOpen(false);
                }
            }
        }
    }
}
