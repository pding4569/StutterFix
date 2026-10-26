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

        // ── 어제 시안(v11)의 움직임: 아이콘은 70ms 간격으로 18px 아래에서 0.6초(cubic-bezier .2,.9,.25,1.1: 살짝 넘쳤다 돌아옴), 기능 줄은 40ms 간격으로 6px 아래에서 0.32초
        private float launchOpenAt, launchPageAt; private int launchLastFrame = -10, launchRow;
        private static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f; if (x >= 1f) return 1f;
            float lo = 0f, hi = 1f, t = x;
            for (int i = 0; i < 24; i++)
            {
                float u = 1f - t;
                float bx = 3f * u * u * t * x1 + 3f * u * t * t * x2 + t * t * t;
                if (bx < x) lo = t; else hi = t;
                t = (lo + hi) * 0.5f;
            }
            float v = 1f - t;
            return 3f * v * v * t * y1 + 3f * v * t * t * y2 + t * t * t;
        }
        private float RowIn(int r) { return EaseOut(Mathf.Clamp01((Time.unscaledTime - launchPageAt - (r * 0.04f + 0.08f)) / 0.32f)); }

        // 런처 왼쪽 위의 큰 페이지 제목과 번호(틀 없이 글자만)
        private GUIStyle sLaunchTitle, sLaunchIdx;
        private void LauncherTitle(string title)
        {
            if (sLaunchTitle == null)
            {
                sLaunchTitle = new GUIStyle(sPageTitle) { fontSize = Mathf.RoundToInt(sPageTitle.fontSize * 1.9f), alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.93f, 0.94f, 0.96f, 1f) } };
                sLaunchIdx = new GUIStyle(sCrumb) { alignment = TextAnchor.LowerLeft };
            }
            int slot = Array.IndexOf(DockOrder, page);
            GUI.Label(new Rect(0, 4, 640, 46), title, sLaunchTitle);
            GUI.Label(new Rect(Mathf.Min(560f, sLaunchTitle.CalcSize(new GUIContent(title)).x + 16f), 26, 120, 24), (slot + 1).ToString("00") + " / " + DockOrder.Length.ToString("00"), sLaunchIdx);
        }

        // 화면 오른쪽 위: 언어 / 화면 왼쪽 아래: 버전과 키 안내 (틀 없이 작은 글자)
        private void DrawLauncherCorners(float sw, float sh, float e)
        {
            if (Event.current.type == EventType.Repaint) { }
            var ko = new Rect(sw - 40f - 150f, 86f, 70f, 26f);
            var en = new Rect(sw - 40f - 76f, 86f, 76f, 26f);
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, e);
            if (GUI.Button(ko, "한국어", English ? sTab : sTabOn)) SetLanguage("ko");
            if (GUI.Button(en, "English", English ? sTabOn : sTab)) SetLanguage("en");
            GUI.color = new Color(0.55f, 0.56f, 0.6f, 0.9f * e);
            GUI.Label(new Rect(40f, sh - 40f, 760f, 22f), "StutterFix " + Main.Entry.Info.Version + "     " + Hotkey.Name(Main.Config.WindowKey, Main.Config.WindowMods) + "  " + T("열기·닫기", "toggle") + "     Esc  " + T("닫기", "close"), sCrumb);
            GUI.color = old;
        }

        // 기능 한 줄: 런처에서는 줄마다 차례로 떠오른다
        private bool Option(string key, ref bool value, string title, string desc, string tag, bool clickable = true)
        {
            if (!UseLauncher) return OptionCore(key, ref value, title, desc, tag, clickable);
            float ra = RowIn(launchRow++);
            var om = GUI.matrix; var oc = GUI.color;
            GUI.matrix = om * Matrix4x4.Translate(new Vector3(0f, (1f - ra) * 6f, 0f));
            GUI.color = new Color(oc.r, oc.g, oc.b, oc.a * ra);
            try { return OptionCore(key, ref value, title, desc, tag, clickable); }
            finally { GUI.matrix = om; GUI.color = oc; }
        }

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
            if (Time.frameCount - launchLastFrame > 2) { launchOpenAt = t; launchPageAt = t; launchPageLast = -1; }
            launchLastFrame = Time.frameCount;
            float since = t - launchOpenAt;
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
                    float lx = Mathf.Max(64f, sw * 0.07f), lw = Mathf.Min(820f, sw * 0.58f);
                    float cw = lw + 360f, ccx = lx + lw / 2f;
                    GUI.color = new Color(0.02f, 0.021f, 0.026f, 0.95f * e * pe);
                    GUI.DrawTexture(new Rect(ccx - cw / 2f, 0, cw, sh), tFeather);
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

            // ── 아래 아이콘 줄(v11 시안): 칸 배경 없이 어두운 글리프, 올리면 밝아지고 선택은 밑줄. 70ms 간격으로 0.6초 올라온다
            const float GL = 26f, HIT = 46f, GAP = 6f;
            int slots = DockOrder.Length;
            float dockW = slots * HIT + (slots - 1) * GAP;
            float dockX = (sw - dockW) / 2f, dockY = sh - 88f;
            var dockRect = new Rect(dockX - 20, dockY - 22, dockW + 40, HIT + 40);
            var names = PageNames();
            var m = ev.mousePosition;
            int hover = -1;
            for (int slot = 0; slot < slots; slot++)
            {
                int i = DockOrder[slot];
                float tt = (since - (0.65f + slot * 0.07f)) / 0.6f;
                float rise = Bezier(0.2f, 0.9f, 0.25f, 1.1f, Mathf.Clamp01(tt));
                float vis = Mathf.Clamp01(tt * 2.2f) * e;
                var hit = new Rect(dockX + slot * (HIT + GAP), dockY + (1f - rise) * 18f, HIT, HIT);
                bool on = panelOpen && page == i;
                bool hov = hit.Contains(m) && tt > 0.5f;
                if (hov) hover = i;
                if (repaint && icons != null)
                {
                    float br = on || hov ? 1f : 0.40f;
                    GUI.color = new Color(0.93f, 0.94f, 0.96f, vis * br);
                    float sc = hov && Event.current.type == EventType.Repaint && Input.GetMouseButton(0) ? 0.92f : 1f;
                    float g = GL * sc;
                    GUI.DrawTexture(new Rect(hit.center.x - g / 2f, hit.center.y - g / 2f, g, g), icons[i >= 8 ? i + 1 : i]);
                    if (on) Fill(new Rect(hit.center.x - 6, hit.yMax - 4, 12, 2), new Color(0.93f, 0.94f, 0.96f, vis), 1);
                    if (i == 0 && Updater.Available) Fill(new Rect(hit.center.x + 9, hit.y + 9, 6, 6), new Color(0.93f, 0.94f, 0.96f, vis), 3);
                }
                if (tt > 0.5f && GUI.Button(hit, GUIContent.none, GUIStyle.none)) { TogglePanel(i); launchPageAt = t; }
            }
            if (repaint && hover >= 0 && panelT <= 0.01f)
            {
                GUI.color = new Color(0.55f, 0.56f, 0.6f, e);
                GUI.Label(new Rect(0, dockY - 30, sw, 22), names[hover], sTip);
            }

            DrawLauncherCorners(sw, sh, e);

            // ── 박스
            Rect box = Rect.zero;
            if (panelT > 0f)
            {
                // 페이지가 바뀌면(아이콘을 다른 것으로 눌렀을 때) 내용이 아래에서 떠오르는 애니메이션을 다시 시작한다
                if (page != launchPageLast) { if (launchPageLast >= 0) launchPageAt = t; launchPageLast = page; }
                float pg = EaseOut(Mathf.Clamp01((t - launchPageAt) / 0.22f));   // 틀(제목줄)은 빨리, 항목 줄은 하나씩 따로 떠오른다
                float topPad = Mathf.Max(84f, sh * 0.10f);
                float bw = Mathf.Min(820f, sw * 0.58f);
                float bh = Mathf.Min(700f, dockY - 56f - topPad);
                box = new Rect(Mathf.Max(64f, sw * 0.07f), topPad, bw, Mathf.Max(260f, bh));   // 왼쪽에 기능 목록(박스 없음)
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
