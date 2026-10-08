using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // UMM 목록 안이 아니라 게임 화면 위에 따로 뜨는 설정 창 (기본 단축키 Insert).
    //
    // 플레이어가 기술 용어 없이 "무엇이 좋아지는지"만 보고 고를 수 있게 한다. 한국어/English 전환.
    // 모양은 밝고 깔끔하게: 옅은 회색 바탕 위의 흰 카드, 카드 밑의 아주 옅은 그림자, 강조색은 검정 하나.
    // (어두운 그라데이션은 "AI 티"가 난다, 반투명 유리는 뒤 게임 화면이 비쳐 읽기 어렵다는 의견으로 바꿨다.)
    // IMGUI 로 그리되 기본 회색 상자는 쓰지 않는다. 그림자는 카드 텍스처에 구워 넣고 overflow 로 바깥에 그린다.
    // 글꼴은 윈도우의 Segoe UI + 맑은 고딕.
    public class SettingsWindow : MonoBehaviour
    {
        internal static SettingsWindow Instance;
        internal static bool Open;

        internal static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("StutterFix.SettingsWindow");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SettingsWindow>();
        }

        internal static void Destroy()
        {
            if (Instance == null) return;
            if (Open) Instance.FinishClose();   // 모드를 끌 때는 애니메이션 없이 바로
            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        internal static void Toggle() { if (Instance != null) Instance.SetOpen(!Open || Instance.closing); }

        // (자동 시험) 창을 열고 그 기능 패널을 펼친다. -1 이면 아이콘 줄만, -2 면 닫는다. 화면 캡처로 모양을 확인하는 데 쓴다.
        internal static void ShowForTest(int p, int sub = 0)
        {
            if (Instance != null && p >= 0 && p < 7) Instance.subSel[p] = sub;
            if (Instance == null) return;
            if (p == -2) { Instance.SetOpen(false); return; }
            Instance.SetOpen(true);
            if (p < 0) { Instance.panelOpen = false; return; }
            Instance.GoTo(p);
            Instance.panelOpen = true;
        }

        // ── 언어 ───────────────────────────────────────────────────────
        internal static bool English
        {
            get
            {
                var lang = Main.Config != null ? Main.Config.Language : "";
                if (lang == "en") return true;
                if (lang == "ko") return false;
                return Application.systemLanguage != SystemLanguage.Korean;   // 처음에는 윈도우 언어를 따른다
            }
        }

        internal static string T(string ko, string en) { return English ? en : ko; }

        // 모드 디스코드 서버: 버그 제보, 기능 아이디어 (Info.json 의 HomePage, README 와 같은 주소)
        internal const string DiscordUrl = "https://discord.gg/csys9ZAeD6";

        // ── 색 ─────────────────────────────────────────────────────────
        private static Color Hex(int rgb, float a = 1f) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a); }
        // 2026-10-04 전면 개편, 최종 방향(사용자 선택): Linear / Raycast 처럼. 아주 어두운 바탕, 작고 또렷한 글씨, 상자 없이 촘촘한 목록,
        // 거의 안 보이는 선, 설명은 한 줄(누르면 자세히). 색은 검정·회색·흰색이고, 끊긴 프레임 하나에만 빨강.
        private static readonly Color Page = Hex(0x101012), CardC = Hex(0x16161A), Edge = Hex(0xFFFFFF, 0.07f), EdgeHover = Hex(0xFFFFFF, 0.12f),
            Ink = Hex(0xF2F3F5), Text2 = Hex(0xA4A8B0), Text3 = Hex(0x737881), Rule = Hex(0xFFFFFF, 0.08f),
            TrackOff = Hex(0xFFFFFF, 0.14f), Soft = Hex(0xFFFFFF, 0.06f), Surface2 = Hex(0xFFFFFF, 0.10f), RowHover = Hex(0xFFFFFF, 0.07f),
            Accent = Hex(0xEEEFF1), AccentHover = Hex(0xFFFFFF), OnAccent = Hex(0x101012),
            Alert = Hex(0xF2F3F5), BarC = Hex(0x4A4C52);   // 끊긴 프레임도 흰색 (+ 은은한 빛), 빨강은 쓰지 않는다

        private static string HexStr(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

        // ── 상태 ───────────────────────────────────────────────────────
        private Rect rect;
        private bool needCenter = true;
        private int page;
        private Vector2 scroll;
        private bool cursorWas;
        private bool built;
        private float scale = 1f, warmedScale = -1f;
        // ── 애니메이션 ─────────────────────────────────────────────────
        // show: 창이 나타난 정도(0~1). 닫을 때도 0까지 내려간 뒤에야 실제로 닫는다.
        // pageT: 페이지를 바꾼 뒤 본문이 들어온 정도. navY/tabX: 메뉴 선택 표시와 언어 밑줄의 현재 위치.
        private float show;
        private float windowAlpha = 1f;
        private bool closing;
        private float pageT = 1f;
        private float navY = -1f, tabX = -1f;
        private readonly Dictionary<string, float> anim = new Dictionary<string, float>();

        private static float EaseOut(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }   // 빠르게 시작해 부드럽게 멈춤
        private static float Approach(float cur, float target, float speed) { return cur + (target - cur) * (1f - Mathf.Exp(-speed * Time.unscaledDeltaTime)); }

        private Font font;
        private GUIStyle sCrumb, sPageTitle, sRowTitle, sDetail, sTabHover, sSectionLabel, sWindowGlass, sKey;
        private GUIStyle sBodyText, sSecondary, sDimMid, sGroup, sRow, sRail, sRailOn, sH2, sHero, sTileValue, sMono, sMonoAccent, sRight, sTile, sRailSel, sRightMid, sSmallRight, sValueRight, sWell, sWellThin, sPillWell, sPillOn, sDock;
        private Texture2D tKnobLight, tKnobGray, tKnobDark, tGlow;
        private GUIStyle sWindow, sShadow, sTitle, sSub, sH1, sLead, sBody, sDim, sSmall, sTag, sCard, sCardDark, sNav, sNavOn, sNavText,
            sPrimary, sClose, sTab, sTabOn, sStat, sStatDark, sStatLabel, sStatLabelDark, sScroll, sThumb,
            sSegKnob, sSegText, sSegOnText, sSliderValue, sChip, sChipOn;
        private Texture2D tWhite, tMark;

        private const float W = 900f, H = 590f, SideW = 196f, HeaderH = 62f;

        // 여는 것은 바로, 닫는 것은 사라지는 애니메이션이 끝난 뒤에 한다.
        private void SetOpen(bool open)
        {
            if (Edition.Dev) Main.Entry.Logger.Log("[설정 창] " + (open ? "열기" : "닫기") + " 요청, 프레임 " + Time.frameCount + ", 지금 열림 " + Open + ", 닫는 중 " + closing + ", 준비됨 " + built + ", 글자 미리 만듦 " + prewarmed);
            SetOpenCore(open);
        }
        private void SetOpenCore(bool open)
        {
            if (open)
            {
                if (Open && !closing) return;
                if (!Open) { cursorWas = Cursor.visible; show = 0f; pageT = 1f; glassState = Hitch.Playing ? 0 : 1; }
                Open = true;
                closing = false;
            }
            else if (Open) closing = true;
        }

        private void FinishClose()
        {
            ReleaseGlass(); glassState = 0;
            Open = false;
            panelOpen = false;
            panelT = 0f;
            capturing = -1;
            Hotkey.Capturing = false;
            closing = false;
            show = 0f;
            Cursor.visible = cursorWas;
            SetUiBlocked(false);
        }

        private void Update()
        {
            if (Main.Config == null) return;
            // 닫혀 있고 곡이 도는 동안에는 OnGUI 가 아무것도 그리지 않는다(PreWarm·업데이트 알림 모두 곡 중엔 건너뜀). 유니티가 프레임마다
            // GUILayout 준비를 하지 않게 끈다. 여는 키는 이 Update 에서 받으므로 열리면 같은 프레임의 OnGUI 전에 다시 켜진다.
            useGUILayout = Open || closing || !Hitch.Playing;
            if (Hotkey.Down(Main.Config.WindowKey, Main.Config.WindowMods)) SetOpen(!Open || closing);
            if (!Open) return;
            SampleFrame();
            // Esc: 패널이 펼쳐져 있으면 패널만 접고, 한 번 더 누르면 아이콘 줄까지 닫는다
            if (!closing && !Hotkey.Capturing && Input.GetKeyDown(KeyCode.Escape)) { if (panelOpen) panelOpen = false; else SetOpen(false); }
            Cursor.visible = true;   // 곡 중에는 게임이 커서를 숨긴다

            float dt = Time.unscaledDeltaTime;
            if (closing)
            {
                show = Mathf.MoveTowards(show, 0f, dt / 0.14f);   // 닫기는 빠르게
                if (show <= 0f) FinishClose();
            }
            else show = Mathf.MoveTowards(show, 1f, dt / 0.16f);
            pageT = Mathf.MoveTowards(pageT, 1f, dt / 0.18f);
            panelT = Mathf.MoveTowards(panelT, panelOpen && !closing ? 1f : 0f, dt / (panelOpen ? 0.18f : 0.12f));
        }

        // 메뉴를 바꿀 때: 본문을 처음부터 다시 들여보내고, 스크롤은 맨 위로
        private void GoTo(int p)
        {
            if (p == page) return;
            page = p;
            scroll = Vector2.zero;
            pageT = 0f;
        }

        // 창 위를 누를 때 뒤의 게임 UI(에디터 버튼 등)가 같이 눌리지 않게 막는다 (실시간 모니터와 같이 쓴다).
        private void SetUiBlocked(bool block) { if (!block) UiInputBlock.Clear(this); }

        private void OnDisable() { UiInputBlock.Clear(this); }
        private void OnDestroy() { UiInputBlock.Remove(this); }

        // ── Insert 로 여는 창: 오른쪽 끝의 아이콘 줄 + 아이콘을 누르면 옆에 펼쳐지는 기능 패널 ──
        // 예전에는 화면 가운데에 큰 창(900x590)이 떠서 게임 화면을 가렸다. 이제 처음에는 오른쪽 끝에 반투명(75%) 아이콘만
        // 나오고, 아이콘을 누르면 그 기능 패널이 아이콘 줄 왼쪽에 펼쳐진다. 같은 아이콘을 다시 누르면 접힌다.
        // 패널 안의 내용(스위치, 설명, 버튼)은 예전 페이지 코드를 그대로 쓴다.
        private const float DockW = 60f, IconS = 44f, IconGap = 6f, PanelW = 760f, RailW = 176f;
        private bool panelOpen;
        private float panelT;       // 패널이 펼쳐진 정도(0~1)
        private Texture2D[] icons;
        private GUIStyle sTip, sTipLeft;
        private float restartArmedUntil;
        private float homeArmUntil; private int homeArmChoice;   // 홈 화면 재시작 버튼 확인
        private Rect restartMenuRect;   // 재시작 고르기 상자 (지난 프레임 자리, 바깥 클릭·입력 막기용)

        private string[] PageNames()
        {
            return new[] { T("홈", "Home"), T("플레이", "Gameplay"), T("맵 불러오기", "Level loading"), T("그래픽", "Graphics"), T("모니터", "Monitor"), T("저사양", "Low-end PC"), T("연출 끄기", "Effects off"), T("정보", "About") };
        }

        private Rect DockRect(float sw, float sh, float e)
        {
            float h = 9 * IconS + 8 * IconGap + 20 + 10;   // 기능 8개 + 구분선 + 재시작
            return new Rect(sw - DockW - 12 + (1 - e) * (DockW + 24), (sh - h) / 2f, DockW, h);   // 오른쪽 밖에서 미끄러져 들어온다
        }

        private Rect PanelRect(float sw, float sh, Rect dock, float pe)
        {
            float h = Mathf.Min(H, sh - 24);
            return new Rect(dock.x - 12 - PanelW + (1 - pe) * 24f, (sh - h) / 2f, PanelW, h);
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private static float ScaleNow() { return Mathf.Clamp(Screen.height / 1080f * 1.1f, 0.8f, 2.2f); }

        // 처음 Insert 를 누를 때 창 그림 만들기와 글자 만들기(개발자용 기록 1,205ms)로 멈추고 창도 늦게 떴다.
        // 그 일을 게임이 켜진 직후(로고·불러오기 화면이라 멈춰도 티가 안 나는 때) 창을 연 적이 없어도 미리 한다.
        // 글자는 투명하게 그려서 화면에는 아무것도 안 나온다. 나중에 화면 크기가 바뀌면 창을 열 때 그 크기로 다시 만든다.
        private bool prewarmed;
        private void PreWarm()
        {
            if (prewarmed || Main.Config == null || Hitch.Playing) return;
            if (!built) { if (Event.current.type == EventType.Layout) Build(); return; }
            float s = ScaleNow();
            if (WarmStyles(this, s)) { scale = s; warmedScale = s; prewarmed = true; }
        }

        // ── 반투명 유리: 창을 여는 순간 뒤 화면을 GPU 에서 한 번 복사해 여러 번 줄여(흐리게) 창과 아이콘 줄 자리만 잘라 둔다 ──
        // 그 프레임에는 창을 그리지 않아 복사본에 창이 들어가지 않는다(한 프레임 늦게 보일 뿐). 곡 중에는 뒤 화면이 계속 움직이므로
        // 멈춘 복사본 대신 더 짙은 반투명만 쓴다. 매 프레임 비용은 없다(복사는 열 때 한 번).
        private int glassState;          // 0 안 씀, 1 복사할 차례, 2 복사 중, 3 준비됨
        private RenderTexture glassPanel, glassDock;
        private System.Collections.IEnumerator CaptureGlass(float sw, float sh, float sc)
        {
            yield return new WaitForEndOfFrame();
            RenderTexture full = null, cur = null;
            try
            {
                int W = Screen.width, H = Screen.height;
                full = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                cur = full; int w = W, h = H;
                for (int i = 0; i < 5; i++)
                {
                    w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
                    var nx = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                    nx.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(cur, nx);
                    if (cur != full) RenderTexture.ReleaseTemporary(cur);
                    cur = nx;
                }
                var dock = DockRect(sw, sh, 1f);
                var pan = PanelRect(sw, sh, dock, 1f);
                ReleaseGlass();
                glassPanel = Crop(cur, pan, sc, W, H);
                glassDock = Crop(cur, dock, sc, W, H);
                glassState = 3;
            }
            catch (Exception ex) { glassState = 0; Main.Entry.Logger.Log("[설정 창] 유리 배경 실패: " + ex.Message); }
            finally
            {
                if (full != null) RenderTexture.ReleaseTemporary(full);
                if (cur != null && cur != full) RenderTexture.ReleaseTemporary(cur);
            }
        }
        private static RenderTexture Crop(RenderTexture src, Rect guiRect, float sc, int W, int H)
        {
            var px = new Rect(guiRect.x * sc, guiRect.y * sc, guiRect.width * sc, guiRect.height * sc);
            var rt = new RenderTexture(Mathf.Max(8, (int)(px.width / 6f)), Mathf.Max(8, (int)(px.height / 6f)), 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var scaleUv = new Vector2(px.width / W, px.height / H * (GlassFlip ? -1f : 1f));
            var off = new Vector2(px.x / W, GlassFlip ? 1f - px.y / H : 1f - (px.y + px.height) / H);
            Graphics.Blit(src, rt, scaleUv, off);
            return rt;
        }
        internal static string DumpGlass(string dir)
        {
            var rt = Instance != null ? Instance.glassPanel : null;
            if (rt == null) return "없음";
            System.IO.Directory.CreateDirectory(dir);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = prev;
            string f = System.IO.Path.Combine(dir, "glass-panel.png");
            System.IO.File.WriteAllBytes(f, t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
            return f;
        }
        // 화면 복사본은 텍스처 좌표가 위에서 시작하는 API(D3D11/12, Vulkan, Metal)에서 위아래가 뒤집혀 나온다 (D3D11 에서 확인: 뒤집으면 뒤 화면과 일치도 0.96, 그대로 0.32)
        internal static bool GlassFlip = SystemInfo.graphicsUVStartsAtTop;
        private void ReleaseGlass()
        {
            if (glassPanel != null) { glassPanel.Release(); UnityEngine.Object.Destroy(glassPanel); glassPanel = null; }
            if (glassDock != null) { glassDock.Release(); UnityEngine.Object.Destroy(glassDock); glassDock = null; }
        }

        private void OnGUI()
        {
            if (!Open) { PreWarm(); Updater.DrawNotice(false); return; }
            if (!built) Build();
            CaptureKey();

            scale = ScaleNow();
            if (Mathf.Abs(scale - warmedScale) > 0.001f && WarmStyles(this, scale)) warmedScale = scale;
            float sw = Screen.width / scale, sh = Screen.height / scale;
            if (glassState == 1)
            {
                if (Event.current.type == EventType.Repaint) { glassState = 2; StartCoroutine(CaptureGlass(sw, sh, scale)); }
                return;   // 이 프레임은 그리지 않는다 (복사본에 창이 들어가지 않게)
            }
            if (glassState == 2) return;

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            try
            {
                float e = closing ? show * show : EaseOut(show);
                float pe = EaseOut(panelT);
                if (Event.current.type == EventType.Repaint && pe > 0f)
                {
                    GUI.color = new Color(0, 0, 0, 0.25f * e * pe);   // 패널이 열려 있을 때만 뒤 게임 화면을 살짝 가린다
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tWhite);
                }
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                GUI.color = new Color(1, 1, 1, e);
                windowAlpha = e;

                var dock = DockRect(sw, sh, e);
                rect = PanelRect(sw, sh, dock, pe);
                // 아이콘 줄(과 펼친 패널) 자리에만 보이지 않는 UI 판을 깔아 뒤의 게임이 그 자리 클릭을 받지 않게 한다.
                // 그 바깥을 누르면 창을 닫고, 클릭은 그대로 게임에 간다(닫으려고 한 번, 게임을 누르려고 또 한 번 누를 필요가 없게).
                if (closing) UiInputBlock.Clear(this);
                else
                {
                    var blk = panelT > 0f ? Union(dock, rect) : dock;
                    if (restartMenuRect.width > 0f) blk = Union(blk, restartMenuRect);
                    UiInputBlock.Place(this, new Rect(blk.x * scale, blk.y * scale, blk.width * scale, blk.height * scale));
                    var ev = Event.current;
                    if (ev.type == EventType.MouseDown && !dock.Contains(ev.mousePosition) && !(panelT > 0f && rect.Contains(ev.mousePosition)) && !restartMenuRect.Contains(ev.mousePosition))
                        SetOpen(false);
                }

                bool glass = glassState == 3 && glassDock != null;
                if (glass && Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(dock, glassDock, ScaleMode.StretchToFill, false, 0, new Color(1f, 1f, 1f, e), 0, 12);
                DrawDock(dock);

                if (panelT > 0f)
                {
                    windowAlpha = e * pe;
                    GUI.color = new Color(1, 1, 1, windowAlpha);
                    if (Event.current.type == EventType.Repaint)
                    {
                        sShadow.Draw(new Rect(rect.x - 34, rect.y - 22, rect.width + 68, rect.height + 70), false, false, false, false);
                        if (glass && glassPanel != null) GUI.DrawTexture(rect, glassPanel, ScaleMode.StretchToFill, false, 0, new Color(1f, 1f, 1f, windowAlpha), 0, 12);
                    }
                    GUI.Window(0x5F1A, rect, DrawWindow, GUIContent.none, glass ? sWindowGlass : sWindow);
                }
            }
            finally
            {
                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
            }
        }

        // 오른쪽 아이콘 줄: 75% 불투명한 어두운 판 위에 기능 아이콘 6개. 누르면 그 기능 패널을 펼치거나 접는다.
        private void DrawDock(Rect d)
        {
            bool gl = glassState == 3 && glassDock != null;
            Fill(d, new Color(0.07f, 0.075f, 0.09f, gl ? 0.52f : 0.9f), 12);
            Fill(new Rect(d.x + 12, d.y, d.width - 24, 1), Hex(0xFFFFFF, 0.14f), 0);   // 윗변의 가는 빛
            var names = PageNames();
            var m = Event.current.mousePosition;
            int hover = -1;
            float y = d.y + 10;
            for (int i = 0; i < names.Length; i++)
            {
                var r = new Rect(d.x + (d.width - IconS) / 2f, y, IconS, IconS);
                bool on = panelOpen && page == i;
                bool hov = r.Contains(m);
                if (hov) hover = i;
                if (on) Fill(r, new Color(1, 1, 1, 0.20f), 12);
                else if (hov) Fill(r, new Color(1, 1, 1, 0.09f), 12);
                if (Event.current.type == EventType.Repaint && icons != null)
                {
                    var c = GUI.color;
                    GUI.color = new Color(1, 1, 1, c.a * (on || hov ? 1f : 0.72f));
                    GUI.DrawTexture(new Rect(r.x + 10, r.y + 10, IconS - 20, IconS - 20), icons[i]);
                    GUI.color = c;
                    if (i == 0 && Updater.Available) Fill(new Rect(r.xMax - 13, r.y + 5, 8, 8), new Color(Accent.r, Accent.g, Accent.b, c.a), 4);   // 새 버전 있음
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) TogglePanel(i);
                y += IconS + IconGap;
            }

            // 맨 아래: 게임 재시작 (실수로 눌리지 않게 3초 안에 한 번 더 눌러야 한다). 재시작하면 좋은 때면 흰 점.
            Fill(new Rect(d.x + 14, y + 3, d.width - 28, 1), new Color(1, 1, 1, 0.14f), 0);
            y += 10;
            var rr = new Rect(d.x + (d.width - IconS) / 2f, y, IconS, IconS);
            bool armed = Time.realtimeSinceStartup < restartArmedUntil;
            bool rhov = rr.Contains(m);
            var why = RestartAdvisor.Reasons();
            if (armed) Fill(rr, new Color(0.92f, 0.32f, 0.30f, 0.55f), 12);
            else if (rhov) Fill(rr, new Color(1, 1, 1, 0.09f), 12);
            if (Event.current.type == EventType.Repaint && icons != null && icons.Length > 8)
            {
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, c.a * (armed || rhov ? 1f : 0.72f));
                GUI.DrawTexture(new Rect(rr.x + 10, rr.y + 10, IconS - 20, IconS - 20), icons[8]);
                GUI.color = c;
                if (why.Count > 0) Fill(new Rect(rr.xMax - 13, rr.y + 5, 8, 8), new Color(Alert.r, Alert.g, Alert.b, c.a), 4);
            }
            // 누르면 왼쪽에 고르기 상자: "게임 재시작"(처음 화면으로) / "이 맵으로 재시작"(에디터에서 연 맵이 있을 때). 고르는 것이 곧 확인이다.
            if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                restartArmedUntil = armed ? 0f : Time.realtimeSinceStartup + 4f;
            restartMenuRect = Rect.zero;
            if (armed)
            {
                bool canReopen = RestartAdvisor.WillReopen();
                var blk = RestartAdvisor.RecentBlock();
                const float bw = 200f, bh = 34f;
                int n = (canReopen ? 2 : 1) + 1;   // 마지막 칸은 게임 종료
                float ph = n * (bh + 6f) + 10f + (blk != null ? 42f : 0f);
                var pr = new Rect(d.x - 10 - bw - 16, rr.center.y - ph / 2f, bw + 16, ph);
                restartMenuRect = pr;
                if (pr.Contains(m)) restartArmedUntil = Time.realtimeSinceStartup + 4f;   // 고르는 동안은 닫히지 않게
                Fill(pr, new Color(0.06f, 0.065f, 0.08f, 0.92f), 9);
                float by = pr.y + 8;
                if (blk != null) { var ws = new GUIStyle(sTipLeft) { wordWrap = true }; GUI.Label(new Rect(pr.x + 8, by, bw, 38), blk, ws); by += 42; }
                for (int i = 0; i < n; i++)
                {
                    var b = new Rect(pr.x + 8, by + i * (bh + 6f), bw, bh);
                    bool quit = i == n - 1;
                    bool reopen = !quit && canReopen && i == 1;
                    Fill(b, b.Contains(m) ? new Color(0.92f, 0.32f, 0.30f, 0.75f) : new Color(1, 1, 1, 0.08f), 7);
                    GUI.Label(b, quit ? T("게임 종료", "Quit game") : reopen ? ReopenLabel() : T("게임 재시작", "Restart game"), sTip);
                    if (GUI.Button(b, GUIContent.none, GUIStyle.none))
                    {
                        if (quit) RestartAdvisor.Quit(); else RestartAdvisor.Restart(reopen);
                        if (RestartAdvisor.RecentBlock() == null) restartArmedUntil = 0f;
                    }
                }
            }
            else if (rhov && panelT <= 0f)
            {
                var lines = new List<string>();
                lines.Add(T("게임 재시작 · 종료", "Restart / quit game"));
                if (RestartAdvisor.WillReopen()) lines.Add(string.Format(T("에디터에서 연 맵으로 바로 다시 켤 수도 있습니다: {0}", "Can also restart straight into the editor level: {0}"), RestartAdvisor.ReopenName()));
                if (why.Count > 0) { lines.Add(T("지금 재시작하면 좋은 이유:", "Good time to restart:")); foreach (var s in why) lines.Add("· " + s); }
                float w = 0; foreach (var s in lines) w = Mathf.Max(w, sTip.CalcSize(new GUIContent(s)).x);
                w += 22; float h = lines.Count * 22 + 8;
                var tr = new Rect(d.x - 10 - w, rr.center.y - h / 2f, w, h);
                Fill(tr, new Color(0.06f, 0.065f, 0.08f, 0.9f), 9);
                for (int i = 0; i < lines.Count; i++) GUI.Label(new Rect(tr.x + 11, tr.y + 4 + i * 22, w - 22, 22), lines[i], i == 0 ? sTip : sTipLeft);
            }

            // 이름표: 마우스를 올린 아이콘 왼쪽에 (패널이 펼쳐져 있으면 패널 제목이 대신한다)
            if (hover >= 0 && panelT <= 0f)
            {
                float iy = d.y + 10 + hover * (IconS + IconGap);
                var gc = new GUIContent(names[hover]);
                float w = sTip.CalcSize(gc).x + 22;
                var tr = new Rect(d.x - 10 - w, iy + IconS / 2f - 14, w, 28);
                Fill(tr, new Color(0.06f, 0.065f, 0.08f, 0.9f), 9);
                GUI.Label(tr, gc, sTip);
            }
        }

        // 에디터 안이면 "이 맵으로", 밖이면(메인 메뉴 등) 에디터에서 마지막으로 연 맵으로
        private static string ReopenLabel()
        {
            return RestartAdvisor.InEditor() ? T("이 맵으로 재시작", "Restart into this level") : T("마지막 맵으로 재시작", "Restart into last level");
        }

        private void TogglePanel(int i)
        {
            if (panelOpen && page == i) { panelOpen = false; return; }
            if (page != i) GoTo(i);
            panelOpen = true;
        }

        // 창 내용은 OnGUI 가 끝난 뒤 따로 그려지므로, 스킨 바꿔 끼우기를 여기서 한다.
        private void DrawWindow(int id)
        {
            var oldBar = GUI.skin.verticalScrollbar;
            var oldThumb = GUI.skin.verticalScrollbarThumb;
            var oldColor = GUI.color;
            GUI.skin.verticalScrollbar = sScroll;         // 스크롤바 손잡이 모양은 이름으로 찾으므로 잠깐 바꿔 끼운다
            GUI.skin.verticalScrollbarThumb = sThumb;
            GUI.color = new Color(1, 1, 1, windowAlpha);   // 창 안의 글자와 카드도 같이 나타나고 사라진다
            try { DrawContents(); }
            finally
            {
                GUI.skin.verticalScrollbar = oldBar;
                GUI.skin.verticalScrollbarThumb = oldThumb;
                GUI.color = oldColor;
            }
        }

        private void DrawContents()
        {
            float pw = rect.width, ph = rect.height;
            int[] subIcon;
            string[] subs = SubDefs(page, out subIcon);
            float headH = subs != null ? 98f : HeaderH;
            // ── 제목줄: 로고 · StutterFix / 지금 페이지
            GUI.DrawTexture(new Rect(22, 21, 20, 20), tMark);
            var brand = new GUIContent("StutterFix");
            float bw = sCrumb.CalcSize(brand).x;
            GUI.Label(new Rect(52, 19, bw + 4, 24), brand, sCrumb);
            GUI.Label(new Rect(52 + bw + 8, 19, 14, 24), "/", sCrumb);
            GUI.Label(new Rect(52 + bw + 24, 18, 320, 26), PageNames()[Mathf.Clamp(page, 0, 7)], sPageTitle);

            // 언어 + 닫기 (작게)
            var ko = new Rect(pw - 176, 18, 58, 26);
            var en = new Rect(pw - 116, 18, 62, 26);
            if (GUI.Button(ko, "한국어", English ? sTab : sTabOn)) SetLanguage("ko");
            if (GUI.Button(en, "English", English ? sTabOn : sTab)) SetLanguage("en");
            if (GUI.Button(new Rect(pw - 48, 16, 30, 30), "×", sClose)) panelOpen = false;   // 패널만 접는다 (아이콘 줄은 남는다)

            // ── 갈래 탭 (긴 페이지만): 글자 탭 + 고른 것 밑의 가는 선
            if (subs != null)
            {
                int sel = Mathf.Clamp(subSel[page], 0, subs.Length - 1);
                float x = 22f, ty = 58f;
                var ev = Event.current;
                float selX = 0, selW = 0;
                for (int i = 0; i < subs.Length; i++)
                {
                    var gc = new GUIContent(subs[i]);
                    float w = (i == sel ? sTabOn : sTab).CalcSize(gc).x + 20f;
                    var r = new Rect(x, ty, w, 30);
                    bool hov = r.Contains(ev.mousePosition);
                    GUI.Label(r, gc, i == sel ? sTabOn : hov ? sTabHover : sTab);
                    if (i == sel) { selX = r.x + 10f; selW = w - 20f; }
                    if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition)) { ev.Use(); if (i != sel) { subSel[page] = i; scroll = Vector2.zero; pageT = 0f; } }
                    x += w + 2f;
                }
                if (railPage != page || railY < 0) { railY = selX; railPage = page; }
                if (ev.type == EventType.Repaint) railY = Approach(railY, selX, 20f);
                tabW = tabW <= 0 ? selW : (ev.type == EventType.Repaint ? Approach(tabW, selW, 20f) : tabW);
                Fill(new Rect(railY, ty + 30, tabW, 2), Ink, 1);
            }
            Fill(new Rect(0, headH - 1, pw, 1), Rule, 0);
            Fill(new Rect(14, 0, pw - 28, 1), Hex(0xFFFFFF, 0.12f), 0);   // 윗변 안쪽의 가는 빛 (유리 가장자리)

            // ── 아래 줄 (Raycast): 왼쪽 이름·버전, 오른쪽 키 안내
            const float FootH = 42f;
            Fill(new Rect(0, ph - FootH, pw, 1), Rule, 0);
            Fill(new Rect(0, ph - FootH + 1, pw, FootH - 1), Hex(0xFFFFFF, 0.025f), 0);
            GUI.DrawTexture(new Rect(18, ph - FootH + 12, 18, 18), tMark);
            GUI.Label(new Rect(44, ph - FootH, 260, FootH), "StutterFix " + Main.Entry.Info.Version, sCrumb);
            float kx2 = pw - 18;
            kx2 = KeyHint(kx2, ph - FootH, FootH, "Esc", T("닫기", "Close"));
            kx2 = KeyHint(kx2 - 14, ph - FootH, FootH, Hotkey.Name(Main.Config.WindowKey, Main.Config.WindowMods), T("열기·닫기", "Toggle"));

            // ── 본문 (페이지를 바꾸면 옆에서 살짝 밀려 들어온다)
            const float Gutter = 12f;
            float pe = EaseOut(pageT);
            var body = new Rect(6 + (1 - pe) * 12f, headH + 2, pw - 12, ph - headH - 8 - FootH);
            var oldC = GUI.color;
            GUI.color = new Color(oldC.r, oldC.g, oldC.b, oldC.a * pe);
            GUILayout.BeginArea(body);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, sScroll, GUIStyle.none);
            GUILayout.BeginHorizontal();
            GUILayout.Space(Gutter);
            GUILayout.BeginVertical(GUILayout.Width(body.width - Gutter * 2 - 18));
            GUILayout.Space(Gutter);
            switch (page)
            {
                case 0: PageHome(); break;
                case 1: PagePlay(); break;
                case 2: PageLoad(); break;
                case 3: PageGraphics(); break;
                case 4: PageMonitor(); break;
                case 5: PageLowEnd(); break;
                case 6: PageEffects(); break;
                default: PageAbout(); break;
            }
            GUILayout.Space(Gutter);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.color = oldC;
        }

        // ── 아이콘 그림: 이미지 파일 없이 모양을 계산해 만든다 (가장자리는 4x4 표본으로 부드럽게) ──
        private static Texture2D MakeIcon(Func<float, float, bool> inside)
        {
            const int N = 64, S = 4;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                            if (inside((x + (sx + 0.5f) / S) / N, (y + (sy + 0.5f) / S) / N)) hit++;
                    // 텍스처는 아래가 0 이라 세로를 뒤집어 넣는다(모양은 위가 0 으로 정의)
                    px[(N - 1 - y) * N + x] = new Color32(255, 255, 255, (byte)(255 * hit / (S * S)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static bool InBox(float x, float y, float x0, float y0, float x1, float y1) { return x >= x0 && x <= x1 && y >= y0 && y <= y1; }
        private static bool InCircle(float x, float y, float cx, float cy, float r) { return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r; }
        private static bool InTri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static Texture2D[] MakeIcons()
        {
            return new[]
            {
                // 홈: 지붕 + 몸통 (문 자리는 비움)
                MakeIcon((x, y) => InTri(x, y, 0.5f, 0.10f, 0.06f, 0.50f, 0.94f, 0.50f)
                                || (InBox(x, y, 0.20f, 0.46f, 0.80f, 0.90f) && !InBox(x, y, 0.42f, 0.63f, 0.58f, 0.90f))),
                // 플레이: 재생 삼각형
                MakeIcon((x, y) => InTri(x, y, 0.26f, 0.12f, 0.26f, 0.88f, 0.88f, 0.50f)),
                // 맵 불러오기: 아래 화살표 + 받침
                MakeIcon((x, y) => InBox(x, y, 0.43f, 0.08f, 0.57f, 0.50f) || InTri(x, y, 0.22f, 0.44f, 0.78f, 0.44f, 0.50f, 0.72f)
                                || InBox(x, y, 0.10f, 0.80f, 0.90f, 0.92f) || InBox(x, y, 0.10f, 0.60f, 0.22f, 0.92f) || InBox(x, y, 0.78f, 0.60f, 0.90f, 0.92f)),
                // 그래픽: 그림 틀 + 산 + 해
                MakeIcon((x, y) => (InBox(x, y, 0.06f, 0.16f, 0.94f, 0.84f) && !InBox(x, y, 0.16f, 0.26f, 0.84f, 0.74f))
                                || InTri(x, y, 0.16f, 0.74f, 0.44f, 0.40f, 0.72f, 0.74f) || InCircle(x, y, 0.68f, 0.40f, 0.08f)),
                // 모니터: 막대그래프
                MakeIcon((x, y) => InBox(x, y, 0.12f, 0.56f, 0.30f, 0.90f) || InBox(x, y, 0.41f, 0.34f, 0.59f, 0.90f) || InBox(x, y, 0.70f, 0.12f, 0.88f, 0.90f)),
                // 저사양: 속도계 (반원 테두리 + 바늘)
                MakeIcon((x, y) =>
                {
                    float dx = x - 0.5f, dy = y - 0.62f; float dd = Mathf.Sqrt(dx * dx + dy * dy);
                    bool arc = dy <= 0.02f && dd >= 0.30f && dd <= 0.41f;
                    float t = Mathf.Clamp01(((x - 0.5f) * 0.55f + (0.62f - y) * 0.83f) / 0.38f);   // 바늘: 가운데에서 오른쪽 위로
                    float px = 0.5f + 0.55f * 0.38f * t, py = 0.62f - 0.83f * 0.38f * t;
                    bool needle = (x - px) * (x - px) + (y - py) * (y - py) <= 0.055f * 0.055f;
                    return arc || needle || InCircle(x, y, 0.5f, 0.62f, 0.09f);
                }),
                // 연출 끄기: 눈 + 사선
                MakeIcon((x, y) =>
                {
                    float dx = (x - 0.5f) / 0.44f, dy = (y - 0.5f) / 0.26f;
                    float e = dx * dx + dy * dy;
                    bool eye = e <= 1f && e >= 0.62f;
                    bool pupil = InCircle(x, y, 0.5f, 0.5f, 0.11f);
                    float l = Mathf.Abs((x - 0.14f) - (y - 0.14f) * 1.0f) / 1.414f;
                    bool slash = l <= 0.045f && x > 0.12f && x < 0.88f;
                    return (eye || pupil || slash);
                }),
                // 정보: 동그라미 안에 i
                MakeIcon((x, y) =>
                {
                    float d = (x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f);
                    return (d <= 0.46f * 0.46f && d >= 0.36f * 0.36f) || InCircle(x, y, 0.5f, 0.31f, 0.065f) || InBox(x, y, 0.445f, 0.43f, 0.555f, 0.73f);
                }),
                // 재시작: 위쪽이 끊긴 동그라미 + 끊긴 자리의 화살촉 (시계 방향)
                MakeIcon((x, y) =>
                {
                    float dx = x - 0.5f, dy = y - 0.54f; float dd = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg;
                    bool ring = dd >= 0.25f && dd <= 0.37f && !(ang > 15f && ang < 82f);
                    return ring || InTri(x, y, 0.50f, 0.07f, 0.50f, 0.36f, 0.74f, 0.215f);
                }),
            };
        }

        // 누르는 동안 살짝 줄어드는 버튼 (Emil Kowalski: 눌리는 것은 눌렸다는 걸 보여야 한다, 0.97)
        private readonly GUIContent btnC = new GUIContent();
        private bool Btn(string text, GUIStyle st, params GUILayoutOption[] o)
        {
            btnC.text = text;
            Rect r = GUILayoutUtility.GetRect(btnC, st, o);
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); return r.Contains(e.mousePosition); }
                    break;
                case EventType.Repaint:
                    bool down = GUIUtility.hotControl == id && r.Contains(e.mousePosition);
                    var d = down ? new Rect(r.x + r.width * 0.015f, r.y + r.height * 0.015f, r.width * 0.97f, r.height * 0.97f) : r;
                    st.Draw(d, btnC, r.Contains(e.mousePosition), down, false, false);
                    break;
            }
            return false;
        }

        // ── 이 모드만의 표시: 제목줄 아래를 흐르는 프레임 시간 줄 ──
        private void DrawTrace(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            Fill(new Rect(r.x, r.yMax, r.width, 1), Rule, 0);
            int n = ftCount; if (n == 0) return;
            float med = Median();
            float scaleMs = Mathf.Max(med * 4f, 10f), spike = Mathf.Max(med * 2f, med + 6f);
            float bw = r.width / FtN;
            for (int i = 0; i < n; i++)
            {
                float ms = ft[(ftIdx - n + i + FtN) % FtN];
                bool sp = ms > spike;
                float h = Mathf.Clamp(ms / scaleMs, 0.04f, 1f) * (r.height - 2f);
                if (!sp) h = Mathf.Min(h, r.height * 0.35f);   // 보통 프레임은 낮고 옅게, 튄 프레임만 솟는다
                Color c = sp ? new Color(Alert.r, Alert.g, Alert.b, 0.85f) : new Color(BarC.r, BarC.g, BarC.b, 0.45f);
                Fill(new Rect(r.x + (FtN - n + i) * bw, r.yMax - h, Mathf.Max(1f, bw - 0.6f), h), c, 0);
            }
        }
        private float medAt = -1f, medVal;
        private float Median()
        {
            if (Time.unscaledTime - medAt < 0.25f) return medVal;
            int n = ftCount;
            for (int i = 0; i < n; i++) ftSort[i] = ft[i];
            System.Array.Sort(ftSort, 0, n);
            medVal = n > 0 ? ftSort[n / 2] : 0f; medAt = Time.unscaledTime;
            return medVal;
        }

        // ── 홈의 실시간 프레임 그래프: 창이 열려 있는 동안만 프레임 시간을 모은다 ──
        private const int FtN = 240;
        private readonly float[] ft = new float[FtN];
        private int ftIdx, ftCount;
        private float fpsShown, fpsAcc, fpsAt; private int fpsN;
        private readonly float[] ftSort = new float[FtN];
        private void SampleFrame()
        {
            float dt = Time.unscaledDeltaTime;
            ft[ftIdx] = dt * 1000f; ftIdx = (ftIdx + 1) % FtN; if (ftCount < FtN) ftCount++;
            fpsAcc += dt; fpsN++;
            if (Time.unscaledTime - fpsAt >= 0.5f && fpsAcc > 0f) { fpsShown = fpsN / fpsAcc; fpsAcc = 0f; fpsN = 0; fpsAt = Time.unscaledTime; }
        }

        private readonly GUIContent tmpC = new GUIContent();
        private void DrawLive(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            sTile.Draw(r, false, false, false, false);
            float x = r.x + 12, y = r.y + 10;
            sBody.Draw(new Rect(x, y, 300, 22), T("프레임 시간", "Frame time"), false, false, false, false);
            int n = ftCount; float worst = 0f;
            for (int i = 0; i < n; i++) if (ft[i] > worst) worst = ft[i];
            float med = Median();
            string right = n > 0 ? string.Format(T("{0:F0} FPS     가운데 {1:F1}ms     가장 김 {2:F1}ms", "{0:F0} FPS     median {1:F1} ms     longest {2:F1} ms"), fpsShown, med, worst) : "";
            sRightMid.Draw(new Rect(r.xMax - 432, y + 2, 420, 18), right, false, false, false, false);
            var g = new Rect(x, r.y + 44, r.width - 24, r.height - 44 - 30);
            // 60 FPS(16.7ms) 기준선과 그 이름
            float scaleMs = Mathf.Max(med * 3f, 20f);
            float ly = g.yMax - Mathf.Clamp01(16.7f / scaleMs) * g.height;
            Fill(new Rect(g.x, ly, g.width, 1), Rule, 0);
            sSmallRight.Draw(new Rect(g.xMax - 120, ly - 15, 120, 14), "16.7ms", false, false, false, false);
            Fill(new Rect(g.x, g.yMax, g.width, 1), Rule, 0);
            float bw = g.width / FtN, spike = Mathf.Max(med * 2f, med + 6f);
            for (int i = 0; i < n; i++)
            {
                float ms = ft[(ftIdx - n + i + FtN) % FtN];
                float h = Mathf.Clamp(ms / scaleMs, 0.02f, 1f) * g.height;
                DrawBar(new Rect(g.x + (FtN - n + i) * bw, g.yMax - h, Mathf.Max(1f, bw - 1f), h), ms > spike);
            }
            sSmall.Draw(new Rect(x, g.yMax + 10, 360, 16), T("회색은 보통 프레임, 빛나는 흰 막대는 가운데 값보다 크게 튄 프레임입니다.", "Grey bars are normal frames; glowing white bars spiked well above the median."), false, false, false, false);
        }

        // 막대 하나: 앞면 + 윗면에 맺힌 빛 (평평한 막대보다 입체로 보인다)
        private void DrawBar(Rect b, bool spike)
        {
            Color face = spike ? Alert : BarC;
            if (spike) Bloom(new Rect(b.center.x - 6f, b.y, 12f, b.height), 0.75f);   // 튄 프레임: 흰 막대 둘레로 번지는 빛
            Fill(b, face, 0);

        }

        // 빛 번짐: 넓고 옅은 빛 + 좁고 진한 빛 두 겹 (Repaint 때만)
        private void Bloom(Rect r, float strength)
        {
            if (tGlow == null || Event.current.type != EventType.Repaint) return;
            var oc = GUI.color;
            float wx = Mathf.Max(48f, r.width * 1.6f), wy = Mathf.Max(40f, r.height * 1.5f);
            GUI.color = new Color(1f, 1f, 1f, 0.32f * strength * oc.a);
            GUI.DrawTexture(new Rect(r.center.x - wx, r.center.y - wy, wx * 2f, wy * 2f), tGlow);
            GUI.color = new Color(1f, 1f, 1f, 0.55f * strength * oc.a);
            GUI.DrawTexture(new Rect(r.x - 14f, r.y - 12f, r.width + 28f, r.height + 24f), tGlow);
            GUI.color = oc;
        }

        // 상태 목록: 이름 / 값
        private void StateRow(string name, string value)
        {
            BeginRow();
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, sDimMid);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, sValueRight);
            GUILayout.EndHorizontal();
            EndRow();
        }

        // 작은 칸: 큰 값 + 설명
        private void Tile(string value, string label, float h)
        {
            GUILayout.BeginVertical(sTile, GUILayout.Height(h));
            GUILayout.Label(value, sTileValue);
            GUILayout.FlexibleSpace();
            GUILayout.Label(label, sStatLabel);
            GUILayout.EndVertical();
        }

        // 최근 끊김 (실시간 모니터가 모은 것)
        private readonly List<string[]> hitchRows = new List<string[]>();
        private float hitchAt;
        private void RecentHitches()
        {
            GUILayout.Space(6);
            GUILayout.Label(T("최근 끊김", "Recent hitches"), sSectionLabel);
            GUILayout.Space(2);
            if (Event.current.type == EventType.Layout && Time.unscaledTime >= hitchAt) { PerfOverlay.CopyRecent(hitchRows, 4); hitchAt = Time.unscaledTime + 0.5f; }
            BeginGroup();
            if (hitchRows.Count == 0)
            {
                BeginRow();
                GUILayout.Label(T("아직 없습니다", "None yet"), sBody);
                GUILayout.Space(2);
                P(T("곡 중에 프레임이 끊기면 몇 ms 였는지와 원인이 여기에 나옵니다.", "When a frame spikes during a song, its length and cause show up here."), sDim);
                EndRow();
            }
            for (int i = 0; i < hitchRows.Count; i++)
            {
                var h = hitchRows[i];
                BeginRow();
                GUILayout.BeginHorizontal();
                Rect mr = GUILayoutUtility.GetRect(new GUIContent(h[0]), sMono, GUILayout.Width(80));
                if (h[3] == "1") Bloom(new Rect(mr.x, mr.y + 2f, sMonoAccent.CalcSize(new GUIContent(h[0])).x, mr.height - 4f), 0.42f);   // 30ms 넘은 끊김: 숫자 뒤로 빛
                GUI.Label(mr, h[0], h[3] == "1" ? sMonoAccent : sMono);
                GUILayout.Label(h[1], sDimMid, GUILayout.ExpandWidth(true));
                GUILayout.Label(h[2], sRight, GUILayout.Width(80));
                GUILayout.EndHorizontal();
                EndRow();
            }
            EndGroup();
        }

        // ── 페이지 ─────────────────────────────────────────────────────
        private void PageHome()
        {
            var c = Main.Config;
            int on = (c.GcPause ? 1 : 0) + (c.EffectSplit ? 1 : 0) + (c.RecolorSplit ? 1 : 0) + (c.TweenGuard ? 1 : 0) + (c.SkipSameText ? 1 : 0) + (c.FilterTypeCache ? 1 : 0) + (c.MeshWarm ? 1 : 0) + (c.SoundWarm ? 1 : 0) + (c.JitWarm ? 1 : 0)
                   + (c.ShaderWarm ? 1 : 0) + (c.FastBlend ? 1 : 0) + (c.SkipInvisible ? 1 : 0) + (c.LazyHidden ? 1 : 0) + (c.ZeroTween ? 1 : 0) + (c.InstantDirect ? 1 : 0) + (c.SkipSame ? 1 : 0) + (c.FastLoop ? 1 : 0) + (c.Precheck ? 1 : 0) + (c.DecoAnim ? 1 : 0) + (c.FloorAnim ? 1 : 0) + (c.MoveFinish ? 1 : 0) + (c.DormantSkip ? 1 : 0) + (c.ImagePrefetch ? 1 : 0) + (c.SkipAssetUnload ? 1 : 0) + (c.LegacyGfxJobs ? 1 : 0) + (c.NoGhosting ? 1 : 0) + (c.SkipIdleParticles ? 1 : 0) + (c.LeakFix ? 1 : 0) + (c.LoadCache ? 1 : 0);
            string d = BootConfig.Describe();
            bool jobs = d.Contains("Jobified") || d.Contains("Split");

            // 첫 화면: 지금 상태를 한 문장으로
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            var h1 = new GUIContent(string.Format(T("기능 {0}개가 켜져 있습니다", "{0} features are on"), on));
            Rect h1r = GUILayoutUtility.GetRect(h1, sH1);
            Bloom(new Rect(h1r.x, h1r.y + 4f, sH1.CalcSize(h1).x, h1r.height - 8f), 0.16f);   // 제목 뒤로 아주 옅은 빛
            GUI.Label(h1r, h1, sH1);
            GUILayout.Space(6);
            P(T("곡 중에는 메모리 정리를 미루고, ", "During a song, memory cleanup is deferred and ") + (jobs ? T("그리기는 여러 코어로 나눕니다. ", "rendering is spread over several cores. ") : T("그리기는 한 코어에서 합니다. ", "rendering runs on one core. "))
              + T("연출과 판정은 바꾸지 않습니다.", "Visuals and judgement stay the same."), sLead);
            GUILayout.EndVertical();
            GUILayout.Space(10);

            // 프레임 시간 그래프
            Rect live = GUILayoutUtility.GetRect(10, 170, GUILayout.ExpandWidth(true), GUILayout.Height(170));
            DrawLive(live);
            GUILayout.Space(22);
            RecentHitches();

            // 새 버전 (GitHub 최신 릴리스)
            if (Updater.Available || Updater.Installed) UpdateCard();
            // PC 맞춤 자동 설정 알림 (처음 한 번 자동으로 바꾼 것)
            if (!string.IsNullOrEmpty(c.TuneNotice))
            {
                InfoCard(new[] { T("PC 맞춤 자동 설정", "Auto setup for this PC"), c.TuneLast + "\n" + T("켠 기능: ", "Turned on: ") + c.TuneNotice + "\n" + T("저사양 페이지에서 하나씩 바꾸거나 한 번에 되돌릴 수 있습니다.", "Change them on the Low-end page or undo all at once.") });
                GUILayout.BeginHorizontal();
                if (Btn(T("확인", "OK"), sPrimary, GUILayout.Width(120), GUILayout.Height(34))) { c.TuneNotice = ""; Save(); }
                GUILayout.Space(8);
                if (PcTune.CanUndo && Btn(T("되돌리기", "Undo"), sSecondary, GUILayout.Width(120), GUILayout.Height(34))) PcTune.Undo();
                GUILayout.EndHorizontal();
                GUILayout.Space(14);
            }
            // 다른 모드(Quartz)와 겹치는 기능 안내
            var overlaps = Compat.Notes();
            if (overlaps.Count > 0) InfoCard(new[] { T("다른 모드와 겹치는 기능", "Overlaps with other mods"), string.Join("\n\n", overlaps.ToArray()) });
            // 자동 보호 (오류가 반복된 기능 끄기, 비정상 종료 뒤 안전 모드)
            var rnotes = Resilience.NotesCopy();
            if (rnotes.Count > 0)
            {
                InfoCard(new[] { T("자동 보호", "Automatic protection"), string.Join("\n\n", rnotes.ToArray()) });
                GUILayout.BeginHorizontal();
                if (Resilience.SafeMode && Btn(T("안전 모드 끄기", "Leave safe mode"), sPrimary, GUILayout.Width(170), GUILayout.Height(38))) Resilience.LeaveSafeMode();
                if (Resilience.OffCount > 0) { GUILayout.Space(8); if (Btn(T("꺼 둔 기능 다시 켜기", "Re-enable features"), sPrimary, GUILayout.Width(190), GUILayout.Height(38))) Resilience.ReenableAll(); }
                GUILayout.EndHorizontal();
                GUILayout.Space(14);
            }

            // 화면 합성 대기 (게임 위에 겹친 창·화면 캡처 프로그램 때문에 FPS 가 떨어진 판이 있었음)
            var pn = PresentWatch.Notice;
            if (pn != null && !PresentWatch.NoticeDismissed)
            {
                InfoCard(new[] { T("FPS 가 떨어진 원인 (게임 밖)", "FPS drop caused outside the game"), pn });
                if (Btn(T("닫기", "Dismiss"), sPrimary, GUILayout.Width(120), GUILayout.Height(34))) PresentWatch.NoticeDismissed = true;
                GUILayout.Space(14);
            }

            // 지금 재시작하면 좋은 때 (메모리가 쌓임, 멀티스레드 그리기 변경, 모드 업데이트 등)
            var why = RestartAdvisor.Reasons();
            if (why.Count > 0)
            {
                InfoCard(new[] { T("재시작 권장", "Restart suggested"), string.Join("\n", why.ToArray()) });
                // 버튼마다 한 번 더 눌러야 재시작 (실수 방지). 에디터에서 연 맵이 있으면 "이 맵으로 재시작" 도.
                GUILayout.BeginHorizontal();
                bool homeArmed = Time.realtimeSinceStartup < homeArmUntil;
                for (int i = 0; i < (RestartAdvisor.WillReopen() ? 2 : 1); i++)
                {
                    bool reopen = i == 1, me = homeArmed && homeArmChoice == i;
                    bool warned = RestartAdvisor.RecentBlock() != null && homeArmChoice == i;   // 저장 안 된 편집 알림이 떠 있으면 한 번 더 = 저장 안 하고 재시작
                    if (warned) me = true;
                    string label = warned ? T("저장 안 하고 재시작", "Restart without saving") : me ? T("한 번 더 누르면 재시작", "Click again to restart")
                                      : reopen ? ReopenLabel() : T("게임 재시작", "Restart game");
                    if (Btn(label, sPrimary, GUILayout.Width(190), GUILayout.Height(38)))
                    {
                        if (me) { homeArmUntil = 0; RestartAdvisor.Restart(reopen); }
                        else { homeArmUntil = Time.realtimeSinceStartup + 3f; homeArmChoice = i; }
                    }
                    GUILayout.Space(8);
                }
                GUILayout.EndHorizontal();
                var hb = RestartAdvisor.RecentBlock();
                if (hb != null) { GUILayout.Space(6); GUILayout.Label(hb, sSub); }
                GUILayout.Space(14);
            }

            InfoCard(new[]
            {
                T("마지막 맵 불러오기", "Last level load"), LoadSummary(),
                T("그래픽", "Graphics"), d.Replace("지금 ", ""),
            });

            GUILayout.BeginHorizontal();
            if (Btn(T("모두 권장값으로", "Reset to recommended"), sSecondary, GUILayout.Width(170), GUILayout.Height(38))) ResetDefaults();
            GUILayout.EndHorizontal();
            GUILayout.Space(14);
            KeysCard();
        }

        // ── 단축키 바꾸기 ───────────────────────────────────────────────
        // 버튼을 누르면 다음에 누르는 키(+ Ctrl/Shift/Alt)를 그 단축키로 쓴다. Esc 는 취소.
        private int capturing = -1;          // 0 설정 창, 1 모니터, -1 아님
        private string keyNote = "";

        private void KeysCard()
        {
            var c = Main.Config;
            GUILayout.BeginVertical(sCard);
            GUILayout.Label(T("단축키", "Shortcuts"), sBody);
            GUILayout.Space(8);
            KeyRow(0, T("설정 창 열기/닫기", "Open / close settings"), c.WindowKey, c.WindowMods);
            GUILayout.Space(8);
            KeyRow(1, T("모니터 표시 방식 바꾸기", "Cycle monitor style"), c.OverlayKey, c.OverlayMods);
            if (keyNote.Length > 0) { GUILayout.Space(8); GUILayout.Label(keyNote, sSub); }
            GUILayout.EndVertical();
        }

        private void KeyRow(int id, string label, KeyCode key, int mods)
        {
            GUILayout.BeginHorizontal();
            P(label, sDim, GUILayout.Height(34));
            GUILayout.FlexibleSpace();
            bool on = capturing == id;
            string text = on ? T("키를 누르세요… (Esc 취소)", "Press a key… (Esc to cancel)") : Hotkey.Name(key, mods);
            if (Btn(text, on ? sChipOn : sChip, GUILayout.MinWidth(150), GUILayout.Height(34)))
            {
                capturing = on ? -1 : id;
                Hotkey.Capturing = capturing >= 0;
                keyNote = "";
            }
            GUILayout.EndHorizontal();
        }

        private void CaptureKey()
        {
            if (capturing < 0) return;
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None || Hotkey.IsModifier(e.keyCode)) return;
            e.Use();
            int id = capturing;
            capturing = -1;
            Hotkey.Capturing = false;
            if (e.keyCode == KeyCode.Escape) { keyNote = ""; return; }

            int mods = (e.shift ? Hotkey.Shift : 0) | (e.control ? Hotkey.Ctrl : 0) | (e.alt ? Hotkey.Alt : 0);
            var c = Main.Config;
            KeyCode otherKey = id == 0 ? c.OverlayKey : c.WindowKey;
            int otherMods = id == 0 ? c.OverlayMods : c.WindowMods;
            if (e.keyCode == otherKey && mods == otherMods)
            {
                keyNote = T("다른 단축키와 같은 키입니다. 다른 키를 골라 주세요.", "That is already used by the other shortcut.");
                return;
            }
            if (id == 0) { c.WindowKey = e.keyCode; c.WindowMods = mods; }
            else { c.OverlayKey = e.keyCode; c.OverlayMods = mods; }
            keyNote = Hotkey.MayClashWithGame(e.keyCode, mods)
                ? T("이 키는 플레이 중 박자 입력과 겹칠 수 있습니다. F1~F12, Insert, Home 같은 키나 Ctrl/Shift 조합을 권합니다.",
                    "This key may also count as a tap while playing. F-keys, Insert, Home or a Ctrl/Shift combo are safer.")
                : "";
            Save();
        }

        private void PagePlay()
        {
            var c = Main.Config;
            bool ch = false;
            int sub = Sub();
            if (sub == 0)
            {
                SubHeading(T("기본", "Essentials"), T("끊김이 가장 큰 곳(메모리 정리, 효과 몰림, 타일 수천 개 바꾸기)입니다. 모두 켜 두는 것을 권장합니다.",
                    "The biggest hitch sources: memory cleanup, effect bursts and changes to thousands of tiles. Keep these on."));
                BeginGroup();
                ch |= Option("gc", ref c.GcPause, T("메모리 정리 미루기", "Defer memory cleanup"),
                    T("플레이 중 게임이 메모리를 정리하느라 잠깐 멈추는 것을 막습니다. 쌓인 것은 어차피 멈추는 순간(편집으로 나가기, 다시 하기, 화면 전환)에 한 번에 정리합니다.",
                      "Stops the game from pausing to clean up memory mid-song. What piles up is cleaned at once during a transition that pauses anyway (back to editor, retry, scene change)."),
                    T("효과 가장 큼", "Biggest impact"));
                ch |= Option("fx", ref c.EffectSplit, T("효과 몰림 나누기", "Spread effect bursts"),
                    T("한 순간에 효과 수십 개가 동시에 시작될 때, 몇 프레임에 나눠 시작해 화면이 멈추지 않게 합니다.",
                      "When dozens of effects start on the same beat, starts them over a few frames instead of freezing one frame."), null);
                ch |= Option("recolor", ref c.RecolorSplit, T("타일 색 바꾸기 나누기", "Spread tile recolors"),
                    T("타일 수천 개의 색을 한 번에 바꾸는 이벤트를 조금씩 나눠 칠합니다. 먼 타일이 아주 잠깐 늦게 바뀔 뿐 결과는 같습니다.",
                      "Recolors thousands of tiles in small batches. Far-away tiles update a few frames later; the result is identical."), null);
                ch |= Option("tween", ref c.TweenGuard, T("애니메이션 처리 최적화", "Animation list guard"),
                    T("효과가 많을 때 게임이 애니메이션 목록을 반복해서 다시 정리하느라 느려지는 문제를 막습니다.",
                      "Prevents the game from repeatedly re-sorting its animation list when many effects are running."), null);
                ch |= Option("hittextfade", ref c.HitTextFade, T("판정 글자 가볍게 사라지기", "Lighter judgment fade"),
                    T("판정 글자가 투명해지며 사라질 때 글자를 매번 새로 만들지 않고 색만 바꿉니다. 직접 플레이할 때 판정 글자가 많이 떠 있는 빠른 구간에서 프레임이 가벼워집니다. 보이는 모습은 같습니다.",
                      "When judgment text fades out, only its color is updated instead of rebuilding the text every frame. Lighter frames in dense sections when you play by hand. Looks the same."), null);
                ch |= Option("hitmeterfade", ref c.HitMeterFade, T("판정 오차 막대 가볍게", "Lighter hit error meter"),
                    T("판정 오차 막대의 눈금이 사라질 때 눈금 그림을 매 프레임 다시 만들지 않고 투명도만 바꿉니다. 박자가 빠른 구간에서 프레임이 가벼워집니다. 보이는 모습은 같습니다.",
                      "When hit error meter ticks fade out, only their opacity is updated instead of rebuilding each tick every frame. Lighter frames in fast sections. Looks the same."), null);
                ch |= Option("flooranim", ref c.FloorAnim, T("타일 애니메이션 직접 처리", "Tile move animations"),
                    T("길이가 있는 타일 이동(위치·회전·크기·불투명도)의 애니메이션을 DOTween 대신 모드가 돌립니다. 타일 수천 개를 한 번에 옮기는 효과가 시작될 때의 끊김을 줄입니다. 시간 누적, 이징, 끊기는 DOTween 과 똑같이 합니다.",
                      "Runs tile move animations (position, rotation, scale, opacity) in the mod instead of DOTween, reducing the hitch when an effect moves thousands of tiles at once. Timing, easing and kill behavior match DOTween."), null);
                EndGroup();
            }
            else if (sub == 1)
            {
                SubHeading(T("미리 준비", "Warm-up"), T("곡 중에 처음 쓰는 것(타일 모양, 함수, 소리, 그래픽)을 재생 준비 때 미리 해 둡니다. 화면과 소리는 같습니다.",
                    "Prepares what a song uses for the first time (tile shapes, code, sounds, shaders) before it starts. Looks and sounds the same."));
                BeginGroup();
                ch |= Option("meshwarm", ref c.MeshWarm, T("타일 모양 미리 만들기", "Pre-build tile shapes"),
                    T("타일 색 바꾸기가 스타일을 바꿀 때 곡 중에 새로 만들던 타일 모양을 재생 준비 때 미리 만들어 둡니다. 모양은 같고, 그 순간의 끊김이 없어집니다.",
                      "Tile shapes that a recolor with a style change would build mid-song are built while the level prepares. Same shapes, no hitch at that moment."), null);
                ch |= Option("jitwarm", ref c.JitWarm, T("함수 미리 컴파일", "Precompile functions"),
                    T("게임을 켤 때 모드와 게임의 효과·장식·타일 함수를 미리 컴파일해, 곡 초반에 효과가 처음 나올 때의 끊김을 없앱니다. 게임을 켤 때 0.5초쯤 더 걸리고, 다음 실행부터 적용됩니다.",
                      "Compiles the mod's and the game's effect/decoration/tile functions when the game starts, removing the hitch the first time an effect appears early in a song. Adds about 0.5 s to startup; applies from the next launch."), null);
                ch |= Option("soundwarm", ref c.SoundWarm, T("효과음 미리 불러오기", "Preload hit sounds"),
                    T("곡 중에 처음 쓰는 박자 소리·누르는 박자 소리를 재생 준비 때 미리 불러 둡니다. 처음 쓸 때 소리 파일을 푸느라 생기던 끊김이 없어집니다. 소리는 같습니다.",
                      "Hit sounds and hold sounds first used mid-song are loaded while the level prepares, removing the hitch of decoding them on first use. Sounds are identical."), null);
                ch |= Option("shader", ref c.ShaderWarm, T("그래픽 미리 준비", "Shader warm-up"),
                    T("곡이 시작될 때 그래픽 준비를 미리 해 두어, 효과가 처음 나올 때의 끊김을 줄입니다.",
                      "Prepares shaders when a level starts, reducing the hitch the first time an effect appears."), null);
                ch |= Option("filtertype", ref c.FilterTypeCache, T("고급 필터 빠르게 끄기", "Faster advanced filter reset"),
                    T("고급 필터의 \"다른 필터 끄기\"가 쓴 필터마다 형식을 새로 찾느라(한 번 약 0.7ms) 한 프레임에 수십 ms 멈추던 것을 없앱니다. 결과는 같습니다.",
                      "The advanced filter's \"disable others\" looked up each used filter's type from scratch (about 0.7 ms each), stalling a frame for tens of ms. Results are identical."), null);
                EndGroup();
            }
            else if (sub == 2)
            {
                SubHeading(T("그리기", "Rendering"), T("보이지 않거나 바뀌지 않는 장식·글자를 그리기와 갱신에서 뺍니다. 화면은 같습니다.",
                    "Leaves hidden or unchanged decorations and text out of drawing and updates. Looks identical."));
                BeginGroup();
                ch |= Option("invis", ref c.SkipInvisible, T("투명한 장식 그리지 않기", "Skip invisible decorations"),
                    T("투명도가 0 이라 보이지 않는 이미지 장식을 그리기에서 뺍니다. 다시 보이게 되면 바로 그립니다. 화면은 같고, 나중에 나타날 이미지를 깔아 둔 맵에서 프레임이 오릅니다. 에디터가 아무도 안 보는 썸네일 이미지를 매 프레임 그리던 것도 멈춥니다(썸네일 저장은 그대로). 타일이 3천 개 넘는 맵에서는 화면에서 먼 타일도 그리기에서 빼서, 유니티가 카메라마다 모든 타일을 검사하던 비용을 없앱니다(9만 타일 맵 295→420 FPS). 화면에 가까워지면 그리기 전에 다시 넣고, 움직이는 타일은 빼지 않습니다.",
                      "Leaves fully transparent image decorations out of rendering and draws them again as soon as they become visible. Looks identical; raises FPS on maps that pre-place hidden images. Also stops the editor from redrawing an unused thumbnail image every frame (saving thumbnails still works). On levels with over 3,000 tiles, far off-screen tiles are also left out of rendering so Unity stops testing every tile for every camera (90k-tile level 295 → 420 FPS); they come back before they can appear, and moving tiles are never left out."), null);
                ch |= Option("lazy", ref c.LazyHidden, T("투명한 장식 위치 미루기", "Defer hidden decoration moves"),
                    T("투명해서 안 보이는 장식은 옮겨도 값만 저장했다가, 보이게 되는 순간 한 번 반영합니다. 히트박스·마스크 장식은 제외합니다.",
                      "Hidden decorations only store their new position until they become visible, then apply it once. Hitbox and mask decorations are excluded."),
                    null, 1, Need(c.SkipInvisible, T("투명한 장식 그리지 않기", "Skip invisible decorations")));
                ch |= Option("dormant", ref c.DormantSkip, T("장식 순회 줄이기", "Skip idle decorations"),
                    T("게임은 매 프레임 장식 전부를 훑습니다. 안 보이고 바뀔 일이 없는 장식과, 히트박스가 없는 장식은 그 순회에서 빼 둡니다. 장식이 수만 개인 맵에서 평소 프레임이 크게 오릅니다(Arche 107 → 170 fps). 박자마다 모든 타일에 보내는 박자 알림도, 아무것도 하지 않는 타일(아직 안 지나간 타일)은 건너뜁니다(9만 타일 BPM 32000: 166 → 287 fps, 큰 맵을 연 직후 편집 화면 끊김 해결).",
                      "The game walks every decoration every frame. Idle invisible decorations and decorations without hitboxes are left out of those walks. Big everyday FPS gain on maps with tens of thousands of decorations (Arche 107 → 170 fps). The per-beat notification to every tile also skips tiles where it does nothing (tiles not reached yet): 90k tiles at BPM 32000 166 → 287 fps, and no more editor stutter right after opening a big level."),
                    T("장식 많은 맵", "Decoration-heavy maps"));
                ch |= Option("particleidle", ref c.SkipIdleParticles, T("변화 없는 파티클 갱신 건너뛰기", "Skip idle particle updates"),
                    T("파티클 장식은 값이 그대로여도 매 프레임 모양 크기와 속도를 게임 엔진에 다시 넣습니다. 넣을 값이 지난번과 같으면 건너뜁니다. 화면은 같습니다." + (Compat.QSkipIdleParticles ? " (지금은 Quartz 가 같은 일을 하고 있어 쉬는 중)" : ""),
                      "Particle decorations re-send their shape scale and speed to the engine every frame even when unchanged. Skips the write when the value is the same. Looks identical." + (Compat.QSkipIdleParticles ? " (Idle now: Quartz is doing the same)" : "")),
                    T("파티클 많은 맵", "Particle-heavy maps"));
                ch |= Option("text", ref c.SkipSameText, T("글자 장식 최적화", "Text decoration skip"),
                    T("같은 글자를 매 프레임 다시 쓰는 글자 장식은 건너뜁니다. PACL2 같은 모드를 함께 쓸 때 효과가 큽니다.",
                      "Skips text decorations that are re-set to the same text every frame. Helps a lot with mods like PACL2."), null);
                ch |= Option("blend", ref c.FastBlend, T("블렌드 장식 빠르게 그리기", "Faster blend decorations"),
                    T("더하기(Linear Dodge) 블렌드 장식을 화면 복사 없이 그립니다. 모양은 같고, 블렌드 장식이 많은 맵에서 프레임이 크게 오릅니다.",
                      "Draws additive (Linear Dodge) blend decorations without copying the screen. Looks identical; big FPS gain on maps with many blend decorations."),
                    T("무거운 맵", "Heavy maps"));
                EndGroup();
            }
            else
            {
                SubHeading(T("장식 이동", "Decoration moves"), T("장식을 옮기는 효과를 가볍게 처리합니다. 들여 쓴 기능은 위 기능이 켜져 있을 때만 동작합니다.",
                    "Lighter decoration move effects. Indented features only work while the feature above them is on."));
                BeginGroup();
                ch |= Option("zerotween", ref c.ZeroTween, T("즉시 이동 최적화", "Instant decoration moves"),
                    T("장식을 즉시(길이 0) 옮기는 이벤트를 애니메이션 없이 바로 처리하고, 곧바로 덮어써질 중간 호출은 건너뜁니다. 결과는 게임과 똑같습니다(26만 개를 비트 단위로 비교해 확인).",
                      "Applies instant (zero-length) decoration moves without creating animations and skips intermediate calls that are overwritten right away. Identical results (verified bit-for-bit over 260,000 cases)."),
                    T("장식 많은 맵", "Decoration-heavy maps"));
                string zt = Need(c.ZeroTween, T("즉시 이동 최적화", "Instant decoration moves"));
                ch |= Option("instant", ref c.InstantDirect, T("즉시 이동 직접 처리", "Direct instant moves"),
                    T("즉시 이동이 한꺼번에 몰리는 순간(효과 몰림) 게임 코드가 속성마다 애니메이션 객체를 만드는 과정 자체를 건너뛰고 최종 값만 넣습니다. Arche 효과 몰림 68 → 36ms.",
                      "When many instant moves land at once, skips the game's per-property animation setup entirely and applies only the final values. Arche effect burst 68 → 36 ms."),
                    T("효과 몰림", "Effect bursts"), 1, zt);
                string id = zt ?? Need(c.InstantDirect, T("즉시 이동 직접 처리", "Direct instant moves"));
                ch |= Option("fastloop", ref c.FastLoop, T("장식 이동 루프", "Decoration move loop"),
                    T("장식 이동 효과를 게임 코드 대신 모드의 루프로 돕니다. 게임 코드는 장식마다 객체를 여러 개 만들고 대상 목록을 여러 겹으로 훑는데, 같은 순서로 같은 일만 합니다. 이미지·마스크를 바꾸는 효과는 원래대로 둡니다.",
                      "Runs decoration move effects in the mod's own loop instead of the game code, which allocates several objects per decoration and walks the target list through layered queries. Same work in the same order. Effects that change images or masks are left alone."),
                    T("효과 몰림", "Effect bursts"), 2, id);
                string fl = id ?? Need(c.FastLoop, T("장식 이동 루프", "Decoration move loop"));
                ch |= Option("decoanim", ref c.DecoAnim, T("장식 애니메이션 직접 처리", "Decoration animations"),
                    T("길이가 있는 장식 이동(위치·회전·크기·색·불투명도)의 애니메이션을 DOTween 대신 모드가 돌립니다. 시간 누적, 이징, 콜백 순서, 끊기까지 DOTween 과 똑같이 하고(33만 개를 DOTween 과 나란히 돌려 비트 단위로 확인), 애니메이션 관리 비용만 줄입니다. 피벗·시차가 섞인 효과는 원래대로 둡니다.",
                      "Runs decoration move animations (position, rotation, scale, color, opacity) in the mod instead of DOTween, with the same timing, easing, callback order and kill behavior (verified bit-for-bit against DOTween over 330,000 animations), cutting only the tween bookkeeping. Effects that also animate pivot or parallax stay on DOTween."),
                    T("무거운 구간", "Heavy sections"), 3, fl);
                string ss = id ?? Need(c.SkipInvisible, T("투명한 장식 그리지 않기", "Skip invisible decorations"));
                ch |= Option("samevalue", ref c.SkipSame, T("투명 장식 빠른 처리", "Fast path for hidden decorations"),
                    T("즉시 이동이 투명한 장식을 옮기면 게임 함수를 거치지 않고 위치를 바로 \"보일 때 반영\" 목록에 넣고, 이미 가진 것과 같은 색은 다시 넣지 않으며, 바뀌어도 투명한 채라면 값만 저장합니다. 게임 상태는 원래와 똑같습니다.",
                      "When an instant move touches a transparent decoration, its position goes straight into the apply-when-visible list, re-writing an unchanged color is skipped, and color changes that stay transparent only store values. Game state stays identical."),
                    T("효과 몰림", "Effect bursts"), 2, ss);
                string pc = fl ?? ss ?? Need(c.SkipSame, T("투명 장식 빠른 처리", "Fast path for hidden decorations")) ?? Need(c.LazyHidden, T("투명한 장식 위치 미루기", "Defer hidden decoration moves"));
                ch |= Option("precheck", ref c.Precheck, T("미리 확인", "Look-ahead check"),
                    T("곧 발동할 무거운 장식 이동 효과(대상 200개 이상)가 이미 투명하고 값도 그대로인 장식에만 닿는지 몇 초 앞서 여유 있는 프레임에 나눠 확인해 두고, 발동할 때까지 대상이 하나도 안 바뀌었으면 효과를 통째로 건너뜁니다. 대상이 바뀌는 모든 길을 지켜보다가 바뀐 장식만 원래대로 처리합니다.",
                      "Checks upcoming heavy decoration moves (200+ targets) a few seconds ahead, spread over idle frames, and skips the whole effect when every target is already hidden with the same values. Every write path to a watched decoration is tracked; decorations touched in between are processed normally."),
                    T("효과 몰림", "Effect bursts"), 3, pc);
                ch |= Option("movefinish", ref c.MoveFinish, T("장식 위치 계산 줄이기", "Fewer position updates"),
                    T("장식을 옮길 때 위치 마무리 계산을 한 번으로 묶고, 값이 그대로인 쓰기와 플레이 중 필요 없는 편집기 작업을 건너뜁니다. 보이는 장식의 위치 재계산은 어차피 같은 프레임에 게임이 다시 하므로 그때 한 번만 합니다.",
                      "Batches position finishing per decoration, skips unchanged writes and editor-only work while playing, and leaves visible decorations' position recompute to the game's own once-per-frame pass."), null);
                EndGroup();
            }
            if (ch) Save();
        }

        // ── 페이지 안 갈래 (왼쪽 세로 메뉴) ──────────────────────────────
        // 플레이(23개)·저사양처럼 긴 페이지는 스위치가 한 줄로 길게 이어져 찾기 어려웠다. 갈래를 왼쪽에 아이콘과 함께 두고 고른 것만 보인다.
        private readonly int[] subSel = new int[8];
        private float railY = -1f, tabW = -1f;
        // 아래 줄 키 안내: [키] 설명 을 오른쪽부터 왼쪽으로 놓는다. 다음 자리(x) 를 돌려준다.
        private float KeyHint(float right, float y, float h, string key, string label)
        {
            var lc = new GUIContent(label); float lw = sCrumb.CalcSize(lc).x;
            var kc = new GUIContent(key); float kw = Mathf.Max(26f, sKey.CalcSize(kc).x + 14f);
            float x = right - lw;
            GUI.Label(new Rect(x, y, lw + 2, h), lc, sCrumb);
            var kr = new Rect(x - 8 - kw, y + (h - 24) / 2f, kw, 24);
            Fill(kr, Hex(0xFFFFFF, 0.09f), 5);
            GUI.Label(kr, kc, sKey);
            return kr.x;
        }
        private int railPage = -1;
        private Texture2D[] subIcons;
        private const int IcBolt = 0, IcClock = 1, IcEye = 2, IcMove = 3, IcTune = 4, IcChip = 5, IcTiles = 6, IcCard = 7, IcFlask = 8;

        private string[] SubDefs(int p, out int[] icons)
        {
            icons = null;
            if (p == 1) { icons = new[] { IcBolt, IcClock, IcEye, IcMove }; return new[] { T("기본", "Essentials"), T("미리 준비", "Warm-up"), T("그리기", "Rendering"), T("장식 이동", "Decoration moves") }; }
            if (p == 4) { icons = new[] { IcEye, IcTiles, IcBolt }; return new[] { T("표시", "Display"), T("항목", "Items"), T("끊김 알림", "Hitch alerts") }; }
            if (p == 5) { icons = new[] { IcTune, IcChip, IcTiles, IcCard, IcFlask }; return new[] { T("PC 맞춤", "PC fit"), T("컴퓨터", "System"), T("게임", "Game"), T("그래픽카드", "Graphics card"), T("실험", "Experimental") }; }
            if (p == 6) { icons = new[] { IcEye, IcTiles, IcMove }; return new[] { T("맵 효과", "Level effects"), T("필터", "Filters"), T("판정·조작", "Judgment & input") }; }
            return null;
        }
        private int Sub() { return subSel[Mathf.Clamp(page, 0, 7)]; }

        private void DrawRail(Rect area, string[] names, int[] icons)
        {
            if (subIcons == null) subIcons = MakeSubIcons();
            int sel = Mathf.Clamp(subSel[page], 0, names.Length - 1);
            const float ItemH = 40f, Gap = 4f;
            var ev = Event.current;
            float target = area.y + sel * (ItemH + Gap);
            if (railY < 0 || railPage != page) { railY = target; railPage = page; }
            if (ev.type == EventType.Repaint) railY = Approach(railY, target, 18f);
            // 고른 칸: 흰 바탕 + 가는 테두리가 미끄러져 간다
            var selR = new Rect(area.x, railY, area.width, ItemH);
            if (ev.type == EventType.Repaint) sRailSel.Draw(selR, false, false, false, false);   // 고른 칸: 한 단계 밝은 바탕 + 가는 테두리 (주황은 아이콘만)
            for (int i = 0; i < names.Length; i++)
            {
                var r = new Rect(area.x, area.y + i * (ItemH + Gap), area.width, ItemH);
                bool on = i == sel, hov = !on && r.Contains(ev.mousePosition);
                if (hov) Fill(r, Soft, 10);
                var oc = GUI.color;
                Color ink = on ? Ink : hov ? Ink : Text2;
                GUI.color = new Color(ink.r, ink.g, ink.b, oc.a * (on ? 1f : 0.9f));
                if (ev.type == EventType.Repaint) GUI.DrawTexture(new Rect(r.x + 14, r.y + 11, 18, 18), subIcons[icons[i]]);
                GUI.color = oc;
                GUI.Label(new Rect(r.x + 42, r.y, r.width - 48, ItemH), names[i], on ? sRailOn : sRail);
                if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition))
                {
                    ev.Use();
                    if (i != sel) { subSel[page] = i; scroll = Vector2.zero; pageT = 0f; }
                }
            }
        }

        // 갈래 제목 (본문 맨 위)
        private void SubHeading(string title, string lead)
        {
            GUILayout.Space(4);
            if (!string.IsNullOrEmpty(lead)) P(lead, sLead);
            GUILayout.Space(12);
        }

        // ── 묶음 상자: 스위치마다 따로 떠 있던 카드를 한 상자 안의 줄로 (줄 사이 가는 선) ──
        private bool groupOpen;
        private int groupRow;
        private readonly Dictionary<string, Rect> rowRects = new Dictionary<string, Rect>();
        private void BeginGroup() { GUILayout.BeginVertical(); groupOpen = true; groupRow = 0; }
        private void EndGroup() { if (!groupOpen) return; groupOpen = false; GUILayout.EndVertical(); GUILayout.Space(14); }
        private void RowSeparator() { groupRow++; }   // 줄 사이 선은 쓰지 않는다 (간격과 마우스 올림만으로 나눈다)
        // 스위치가 아닌 줄(고르기, 슬라이더 등)
        private void BeginRow() { if (groupOpen) { RowSeparator(); GUILayout.BeginVertical(sRow); } }
        private void EndRow() { if (groupOpen) GUILayout.EndVertical(); }

        private static Texture2D[] MakeSubIcons()
        {
            Func<float, float, float, float, float, float, bool> ring = (x, y, cx, cy, r0, r1) => { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); return d >= r0 && d <= r1; };
            return new[]
            {
                // 기본: 번개
                MakeIcon((x, y) => InTri(x, y, 0.62f, 0.04f, 0.18f, 0.58f, 0.54f, 0.58f) || InTri(x, y, 0.40f, 0.96f, 0.84f, 0.42f, 0.46f, 0.42f)),
                // 미리 준비: 시계
                MakeIcon((x, y) => ring(x, y, 0.5f, 0.5f, 0.34f, 0.45f) || InBox(x, y, 0.455f, 0.24f, 0.545f, 0.54f) || InBox(x, y, 0.455f, 0.455f, 0.72f, 0.545f)),
                // 그리기: 눈
                MakeIcon((x, y) =>
                {
                    float ex = (x - 0.5f) / 0.46f, ey = (y - 0.5f) / 0.27f; float e = ex * ex + ey * ey;
                    float ix = (x - 0.5f) / 0.34f, iy = (y - 0.5f) / 0.15f;
                    return (e <= 1f && ix * ix + iy * iy >= 1f && !(Mathf.Abs(x - 0.5f) < 0.30f && Mathf.Abs(y - 0.5f) < 0.12f)) || InCircle(x, y, 0.5f, 0.5f, 0.12f);
                }),
                // 장식 이동: 네 방향 화살표
                MakeIcon((x, y) => InBox(x, y, 0.455f, 0.18f, 0.545f, 0.82f) || InBox(x, y, 0.18f, 0.455f, 0.82f, 0.545f)
                                || InTri(x, y, 0.5f, 0.02f, 0.34f, 0.22f, 0.66f, 0.22f) || InTri(x, y, 0.5f, 0.98f, 0.34f, 0.78f, 0.66f, 0.78f)
                                || InTri(x, y, 0.02f, 0.5f, 0.22f, 0.34f, 0.22f, 0.66f) || InTri(x, y, 0.98f, 0.5f, 0.78f, 0.34f, 0.78f, 0.66f)),
                // PC 맞춤: 조절 막대 셋
                MakeIcon((x, y) => InBox(x, y, 0.08f, 0.20f, 0.92f, 0.27f) || InBox(x, y, 0.08f, 0.47f, 0.92f, 0.54f) || InBox(x, y, 0.08f, 0.74f, 0.92f, 0.81f)
                                || InCircle(x, y, 0.32f, 0.235f, 0.11f) || InCircle(x, y, 0.68f, 0.505f, 0.11f) || InCircle(x, y, 0.42f, 0.775f, 0.11f)),
                // 컴퓨터: 칩 (테두리 + 가운데 + 다리)
                MakeIcon((x, y) => (InBox(x, y, 0.20f, 0.20f, 0.80f, 0.80f) && !InBox(x, y, 0.29f, 0.29f, 0.71f, 0.71f)) || InBox(x, y, 0.38f, 0.38f, 0.62f, 0.62f)
                                || ((InBox(x, y, 0.04f, 0.0f, 0.20f, 1f) || InBox(x, y, 0.80f, 0.0f, 0.96f, 1f)) && (InBox(x, y, 0, 0.30f, 1, 0.37f) || InBox(x, y, 0, 0.63f, 1, 0.70f)))
                                || ((InBox(x, y, 0.0f, 0.04f, 1f, 0.20f) || InBox(x, y, 0.0f, 0.80f, 1f, 0.96f)) && (InBox(x, y, 0.30f, 0, 0.37f, 1) || InBox(x, y, 0.63f, 0, 0.70f, 1)))),
                // 게임: 타일 넷
                MakeIcon((x, y) => InBox(x, y, 0.10f, 0.10f, 0.45f, 0.45f) || InBox(x, y, 0.55f, 0.10f, 0.90f, 0.45f) || InBox(x, y, 0.10f, 0.55f, 0.45f, 0.90f)
                                || (InBox(x, y, 0.55f, 0.55f, 0.90f, 0.90f) && !InBox(x, y, 0.63f, 0.63f, 0.82f, 0.82f))),
                // 그래픽카드: 판 + 팬 둘
                MakeIcon((x, y) => (InBox(x, y, 0.04f, 0.24f, 0.96f, 0.76f) && !InBox(x, y, 0.12f, 0.32f, 0.88f, 0.68f)) || ring(x, y, 0.33f, 0.5f, 0.06f, 0.13f) || ring(x, y, 0.67f, 0.5f, 0.06f, 0.13f)
                                || InBox(x, y, 0.16f, 0.76f, 0.30f, 0.88f)),
                // 실험: 플라스크
                MakeIcon((x, y) => (InBox(x, y, 0.38f, 0.06f, 0.62f, 0.40f) && !InBox(x, y, 0.46f, 0.06f, 0.54f, 0.40f)) || InBox(x, y, 0.32f, 0.04f, 0.68f, 0.11f)
                                || (InTri(x, y, 0.38f, 0.36f, 0.08f, 0.94f, 0.92f, 0.94f) && InTri(x, y, 0.62f, 0.36f, 0.92f, 0.94f, 0.08f, 0.94f) && !(InTri(x, y, 0.46f, 0.46f, 0.21f, 0.86f, 0.79f, 0.86f) && y < 0.62f))),
            };
        }

        // 묶음 제목
        private void Section(string title)
        {
            GUILayout.Space(6);
            GUILayout.Label(title, sTag);
            GUILayout.Space(6);
        }

        // 상위 기능이 꺼져 있으면 그 이름("… 꺼짐"), 켜져 있으면 null
        private static string Need(bool on, string name) { return on ? null : name; }

        // 하위 기능: 들여 쓰고 왼쪽에 이어지는 선. 상위 기능이 꺼져 있으면(off != null) 흐리게, 누를 수 없게, 무엇이 꺼져 있는지 적는다.
        private bool Option(string key, ref bool value, string title, string desc, string tag, int depth, string off)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(depth * 22);
            GUILayout.BeginVertical();
            var oldC = GUI.color;
            if (off != null) GUI.color = new Color(oldC.r, oldC.g, oldC.b, oldC.a * 0.45f);
            string t2 = off != null ? T("쉬는 중 · ", "Paused · ") + off + T(" 꺼짐", " is off") : tag;
            bool changed = Option(key, ref value, title, desc, t2, off == null);
            GUI.color = oldC;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (depth > 0 && Event.current.type == EventType.Repaint)
            {
                var r = GUILayoutUtility.GetLastRect();
                float lx = r.x + depth * 26 - 14;
                Fill(new Rect(lx + 4, r.y + 12, 1, r.height - 24), Rule, 0);   // 들여 쓴 하위 기능: 아주 옅은 세로선 하나
            }
            return changed;
        }

        private void PageLoad()
        {
            var c = Main.Config;
            Heading(T("맵 불러오기", "Level loading"), T("맵을 열거나 편집 화면으로 돌아올 때 기다리는 시간을 줄입니다.",
                "Shortens waits when opening a level or returning to the editor."));
            bool ch = false;
            BeginGroup();
            ch |= Option("img", ref c.ImagePrefetch, T("이미지 빠르게 불러오기", "Parallel image loading"),
                T("장식 이미지가 많은 맵을 열 때 CPU 여러 코어로 이미지를 동시에 불러옵니다.",
                  "Decodes decoration images on several CPU cores at once when a level opens."),
                T("예: 67초 → 38초", "e.g. 67s → 38s"));
            ch |= Option("unload", ref c.SkipAssetUnload, T("불필요한 정리 건너뛰기", "Skip asset unload"),
                T("편집으로 돌아올 때 게임이 하는 짧은 정리 작업을 건너뛰어 멈춤을 줄입니다. 맵을 새로 열 때의 정리는 이전 맵 메모리를 풀기 위해 그대로 둡니다.",
                  "Skips a short cleanup the game runs when returning to the editor. The cleanup when opening a new level is kept so the previous level's memory is freed."), null);
            ch |= Option("loadcache", ref c.LoadCache, T("에디터 재생 시작·전환 빠르게", "Faster editor play start & transitions"),
                T("에디터에서 재생을 누를 때 게임이 하는 헛일을 줄입니다.\n① 장식 이미지 파일의 수정 시각을 한 번의 불러오기 안에서는 파일마다 한 번만 읽습니다(원래는 장식마다 디스크에서 다시 읽음).\n② 지난번 뒤로 장식이 하나도 안 바뀌었으면 장식 전체 다시 설정을 한 번만 합니다(원래는 두 번).\n③ 에디터 클릭용 충돌 상자를 끌 때 넣은 반대 순서로 꺼서 물리 엔진이 목록을 매번 끝까지 뒤지지 않게 합니다.\n④ 편집으로 나가거나 에디터에서 죽고 다시 할 때 장식 이미지를 버렸다가 디스크에서 다시 읽지 않고 그대로 씁니다.\n⑤ 에디터에서 죽고 다시 할 때 장식 전체 다시 설정을 한 번만 합니다(원래는 두 번).\n⑥ 편집으로 나갈 때 판 중에 안 바뀐 장식은 다시 설정을 가볍게 합니다(결과가 같은 설정은 건너뜀).\n⑦ 타일의 효과 컴포넌트를 지웠다 새로 붙이지 않고, 새로 만든 것과 같은 상태로 되돌려 다시 씁니다(Arche 재생 시작 3.1초→2.5초, 나가기 1.5초→1.1초). 화면과 동작은 같습니다(자동 비교로 확인).",
                  "Cuts wasted work when pressing Play in the editor:\n(1) reads each decoration image file's modified time once per load instead of once per decoration,\n(2) resets all decorations once instead of twice when nothing changed since the last play,\n(3) disables the editor click colliders in reverse order so the physics engine doesn't scan its whole list each time,\n(4) keeps decoration images when returning to the editor or retrying after a death in the editor instead of throwing them away and reading them from disk again,\n(5) resets all decorations once instead of twice when retrying in the editor,\n(6) when returning to the editor, decorations that did not change during the run get a light reset that skips settings that would come out the same.\n(7) floor effect components are reset and reused instead of destroyed and re-added (Arche play start 3.1 s â 2.5 s, exit 1.5 s â 1.1 s). Looks and plays the same (checked automatically)."),
                T("예: Arche 재생 시작 8.6초 → 4.3초", "e.g. Arche play start 8.6s → 4.3s"));
            ch |= Option("leakfix", ref c.LeakFix, T("게임 메모리 누수 막기", "Fix game memory leaks"),
                T("게임의 사용자 지정 FPS 효과는 켤 때마다 화면 크기 버퍼(4K 급이면 약 40MB)를 새로 만들고 이전 것을 풀지 않으며, 재시작마다 게임 화면 버퍼를 괜히 다시 만듭니다. 이전 버퍼를 풀고 불필요한 재생성을 막습니다. 에디터에서 다른 맵을 열 때마다 옛 타일의 머티리얼(9만 타일이면 약 128MB)이 풀리지 않고 쌓이던 것도 풉니다. 화면은 같습니다." + (Compat.QLeakGuard ? " (지금은 Quartz 의 누수 수정이 켜져 있어 쉬는 중)" : ""),
                  "The game's custom frame-rate effect creates a new screen-sized buffer each time it turns on without freeing the old one (~40 MB at 4K), and needlessly recreates the game view buffer on every restart. Frees the old buffer and avoids the recreate. Also frees the old tiles' materials that piled up every time a level was opened in the editor (~128 MB for 90k tiles). Looks identical." + (Compat.QLeakGuard ? " (Idle now: Quartz leak fix is on)" : "")), null);
            EndGroup();

            // 큰 이미지 줄이기 (화질을 조금 내주고 VRAM 을 아낀다)
            GUILayout.BeginVertical(sCard);
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("큰 이미지 줄이기", "Downscale large images"), sBody, GUILayout.ExpandWidth(false));
            GUILayout.Space(8);
            GUILayout.Label("·  " + T("자동 권장", "Auto recommended"), sTag, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            P(T("장식 이미지가 수천 장인 맵은 그래픽 메모리(VRAM)가 넘쳐 GPU 가 크게 느려집니다. 긴 변이 기준보다 큰 이미지를 줄여 불러옵니다. 장식의 화면 크기는 그대로이고 선명도만 낮아집니다. <b>자동</b>은 처음에는 원본 그대로 불러오고, 플레이 중 그래픽 메모리가 가득 차서 끊긴 맵만 기억해 두었다가 다음에 불러올 때 큰 이미지부터 한 단계씩(3072 → 2048 → 1536 → 1024) 줄입니다. 끊기지 않는 맵은 화질을 건드리지 않습니다. 다음에 여는 맵부터 적용됩니다.",
                "Levels with thousands of decoration images can overflow video memory (VRAM) and slow the GPU badly. Images larger than the limit are loaded smaller. Decorations keep their on-screen size; only sharpness drops. <b>Auto</b> loads images at full size first. If a level stutters because VRAM is full, it remembers that level and caps large images one step lower (3072 → 2048 → 1536 → 1024) the next time it loads. Levels that run fine keep full quality. Applies to the next level you open."), sDim);
            GUILayout.Space(10);
            int cap = c.ImageMaxSide == ImagePrefetch.Auto ? 1 : c.ImageMaxSide >= 4096 ? 2 : c.ImageMaxSide > 0 ? 3 : 0;
            if (Segment("imgcap", ref cap, new[] { T("끔", "Off"), T("자동", "Auto"), T("긴 변 4096", "4096 px"), T("긴 변 2048", "2048 px") }))
            {
                c.ImageMaxSide = cap == 1 ? ImagePrefetch.Auto : cap == 2 ? 4096 : cap == 3 ? 2048 : 0;
                ch = true;
            }
            int remembered = VramGuard.Remembered;
            if (c.ImageMaxSide == ImagePrefetch.Auto && remembered > 0)
            {
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                P(T("자동이 줄이기로 기억한 맵 ", "Levels remembered by Auto: ") + remembered + T("개", ""), sDim, GUILayout.Height(34));
                GUILayout.FlexibleSpace();
                if (Btn(T("기억 지우기", "Forget"), sChip, GUILayout.Height(34), GUILayout.ExpandWidth(false))) VramGuard.Forget();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(12);

            if (ch) Save();
            InfoCard(new[] { T("마지막 맵 불러오기", "Last level load"), LoadSummary() });
        }

        private void PageGraphics()
        {
            var c = Main.Config;
            Heading(T("그래픽", "Graphics"), T("바꾸면 게임을 다시 켜야 적용됩니다.", "Changes apply after restarting the game."));
            bool v = c.LegacyGfxJobs;
            BeginGroup();
            if (Option("jobs", ref v, T("멀티스레드 그리기", "Multithreaded rendering"),
                T("화면을 그리는 준비 작업을 여러 CPU 코어에 나눠 프레임을 높이고 끊김을 줄입니다. 게임 폴더의 boot.config 에 한 줄을 넣고, 모드를 끄면 원래대로 돌려놓습니다.",
                  "Splits render preparation across CPU cores for higher, steadier FPS. Adds one line to the game's boot.config and restores it when the mod is turned off."),
                T("권장", "Recommended")))
            {
                c.LegacyGfxJobs = v;
                BootConfig.Apply(v, c.FlipModel == 1);
                Save();
            }
            bool ghost = c.NoGhosting;
            if (Option("ghost", ref ghost, T("첫 판 FPS 떨어짐 막기", "Prevent first-run FPS drop"),
                T("큰 맵에서 Play 를 누르고 곡이 시작되기까지 게임이 5초 넘게 멈추면, 윈도우가 게임 창을 '응답 없음' 창으로 바꿔치기합니다. 그 뒤로는 그 판 내내 프레임마다 1.7ms 를 더 기다려 FPS 가 크게 떨어졌습니다(Arche 약 320 -> 200 FPS, 죽고 다시 하면 정상). 이 게임에서만 '응답 없음' 창을 끕니다. 게임이 정말 멈췄을 때 '응답 없음' 표시가 안 뜨는 것 말고는 달라지는 것이 없습니다. 끄면 다음 실행부터 적용됩니다.",
                  "When a big level freezes the game for more than 5 seconds after pressing Play, Windows swaps the game window for a 'Not responding' ghost window. After that, every frame of that run waited an extra 1.7 ms and FPS dropped a lot (Arche about 320 -> 200 FPS; fine again after a retry). This turns off the 'Not responding' window for this game only. The only other change is that a real hang won't show 'Not responding'. Turning it off applies after a restart."),
                T("권장", "Recommended")))
            {
                c.NoGhosting = ghost; WindowGhost.KeepResponsive = ghost;
                if (ghost) WindowGhost.Disable();
                Save();
            }
            bool flip = c.FlipModel == 1;
            if (Option("flip", ref flip, T("최신 화면 출력 방식", "Modern presentation"),
                T("게임은 D3D11 에서 윈도우가 게임 화면을 통째로 복사해 합성하는 옛 방식으로 화면을 내보냅니다. 이것을 최신 방식(Flip)으로 바꿉니다. 측정: 같은 구간 300 -> 318 FPS, 화면에 나오기까지 약 6.2 -> 4.4ms. 게임 위에 다른 창이 없으면 더 빨라질 수 있고, 그때 수직동기가 꺼져 있으면 화면이 가로로 찢어져 보일 수 있습니다. boot.config 의 한 줄을 빼고, 끄거나 모드를 끄면 되돌립니다.",
                  "The game presents through the legacy D3D11 path where Windows copies and composites the whole frame. This switches to the modern flip model. Measured: 300 -> 318 FPS on the same section, frame-to-screen about 6.2 -> 4.4 ms. With no other windows on top it can get faster still, and with vsync off you may see tearing. Removes one line from boot.config; turning it off or disabling the mod restores it."),
                T("실험", "Experimental")))
            {
                c.FlipModel = flip ? 1 : 0;
                BootConfig.Apply(c.LegacyGfxJobs, flip);
                Save();
            }
            bool frameGen = c.FrameGenOutside >= 2 && c.FrameGenOutside <= 8;
            if (Option("framegen", ref frameGen, T("프레임 늘리기 (실험)", "Increase frames (experimental)"),
                T("진짜 프레임 사이에 공을 포함한 지난 화면을 카메라 움직임에 맞춰 옮겨 출력합니다. 입력·판정과 공 자체의 움직임은 진짜 프레임 속도를 따릅니다. UI는 지난 진짜 프레임 것을 유지합니다. 배율이 높을수록 원본 FPS가 낮아질 수 있습니다. 화면이 흔들리거나 어색하면 끄세요. D3D11에서 사용할 수 있고 기본 꺼짐입니다. 바로 적용됩니다.",
                  "Presents camera-reprojected frames between real frames, including the planets. Input, judgment and planet motion still follow real frames; UI retains the last real frame. Higher multipliers can reduce real FPS. Turn off if motion looks wrong. Requires D3D11. Off by default; applies immediately."), T("실험", "Experimental")))
            {
                c.FrameGenOutside = frameGen ? Mathf.Clamp(c.FrameGenMultiplier, 2, 8) : 0;
                Save();
            }
            if (frameGen)
            {
                float multiplier = Mathf.Clamp(c.FrameGenOutside, 2, 8);
                if (Slider("framegenmultiplier", ref multiplier, 2, 8, T("출력 배율", "Output multiplier"), Mathf.RoundToInt(multiplier) + T("배", "×")))
                {
                    c.FrameGenMultiplier = c.FrameGenOutside = Mathf.RoundToInt(multiplier);
                    Save();
                }
                if (HalfRender.Enabled) P(T("'반만 그리기'가 켜져 있어 프레임 늘리기는 쉬고 있습니다. 두 방식 중 하나를 선택하세요.", "Frame increase is suspended while Half rendering is enabled. Choose one method."), sDim);
                else if (FrameGen.Status.Length>0) P(FrameGen.Status, sDim);
            }
            bool exfs = c.ExpFullscreen == 1;
            if (Option("exfs", ref exfs, T("화면 지연 줄이기 (독점 전체 화면)", "Lower display latency (exclusive fullscreen)"),
                T("전체 화면일 때 게임을 독점 전체 화면으로 바꿔, 윈도우가 화면을 한 번 더 합성하는 단계를 건너뜁니다. 측정(HELLO 2026, 165Hz): 프레임이 화면에 나오기까지 15.8 -> 7.9ms 로 절반, 평균 FPS 는 209 -> 187 로 조금 낮아집니다. 수직동기가 꺼져 있으면 화면이 가로로 찢어져 보일 수 있고, Alt+Tab 하면 게임이 최소화되며 켜고 끌 때 화면이 한 번 깜빡입니다. 창 모드에서는 아무것도 하지 않습니다.",
                  "In fullscreen, switches the game to exclusive fullscreen so Windows skips one composition step. Measured (HELLO 2026, 165 Hz): frame-to-screen 15.8 -> 7.9 ms, average FPS 209 -> 187. With vsync off you may see tearing; Alt+Tab minimizes the game and the screen blinks once when switching. Does nothing in windowed mode."),
                T("실험", "Experimental")))
            {
                c.ExpFullscreen = exfs ? 1 : 0;
                Main.ApplyFullscreen();
                Save();
            }
            EndGroup();
            var rows = new List<string> { T("지금 상태", "Current"), BootConfig.Describe().Replace("지금 ", "") };
            if (BootConfig.Status.Contains("다음 실행")) { rows.Add(T("적용", "Pending")); rows.Add(T("게임을 다시 켜면 적용됩니다", "Applies after restart")); }
            if (Main.LaunchWarning.Length > 0) { rows.Add(T("주의", "Warning")); rows.Add(Main.LaunchWarning.Trim()); }
            InfoCard(rows.ToArray());
        }

        // 저사양: 화면·동작이 아주 조금 달라지는 것을 감수하고 약한 컴퓨터에서 프레임을 짜내는 기능들 (전부 기본 꺼짐)
        // 저사양 페이지 맨 위: PC 맞춤 (측정해서 추천 적용, 되돌리기)
        private void TuneCard()
        {
            var c = Main.Config;
            var rows = new List<string> { T("PC 맞춤", "PC fit"), string.IsNullOrEmpty(c.TuneLast) ? T("아직 안 잼", "Not measured yet") : c.TuneLast };
            if (PcTune.HasResult)
            {
                var rec = PcTune.RecommendLabels();
                rows.Add(T("추천", "Suggested")); rows.Add(rec.Count > 0 ? string.Join(", ", rec.ToArray()) : T("지금 설정으로 충분합니다", "Current settings are fine"));
                if (PcTune.Integrated && c.FlipModel != 1) { rows.Add(T("내장 그래픽", "Integrated GPU")); rows.Add(T("그래픽 페이지의 \"최신 화면 출력 방식\"(실험)도 권합니다. 윈도우가 화면을 한 번 더 복사하는 것을 줄여 메모리 대역을 아낍니다. 게임을 다시 켜야 적용됩니다.", "The Modern presentation option (experimental, Graphics page) is also suggested: it saves one full-screen copy per frame. Needs a restart.")); }
            }
            InfoCard(rows.ToArray());
            GUILayout.BeginHorizontal();
            if (Btn(T("측정해서 추천 적용", "Measure and apply"), sPrimary, GUILayout.Width(170), GUILayout.Height(36))) { PcTune.Measure(); c.TuneLast = PcTune.Describe(); var done = PcTune.Apply(); c.TuneNotice = done.Count > 0 ? string.Join(", ", done.ToArray()) : ""; Save(); }
            GUILayout.Space(8);
            if (PcTune.CanUndo && Btn(T("되돌리기", "Undo"), sSecondary, GUILayout.Width(120), GUILayout.Height(36))) PcTune.Undo();
            GUILayout.EndHorizontal();
            GUILayout.Space(14);
        }

        private void PageLowEnd()
        {
            var c = Main.Config;
            int sub = Sub();
            if (sub == 0) SubHeading(T("PC 맞춤", "PC fit"), T("약한 컴퓨터를 위한 기능입니다. 다른 기능과 달리 게임 밖 설정을 바꾸거나 아주 작은 차이를 감수하므로 기본으로 꺼져 있습니다. 필요한 것만 켜세요.",
                "For weak PCs. Unlike the other features, these change settings outside the game or accept tiny differences, so they are off by default."));
            bool ch = false;
            if (sub == 0) TuneCard();
            if (sub == 1)
            {
            SubHeading(T("컴퓨터", "System"), T("다른 프로그램보다 게임이 먼저 돌게 하고, 플레이 밖에서는 쉬게 합니다.", "Lets the game run ahead of other programs and rest outside of play."));
            BeginGroup();
            ch |= Option("lowprio", ref c.LowPriority, T("게임 우선순위 높이기", "Higher game priority"),
                T("브라우저, 방송 프로그램, 업데이트 같은 다른 프로그램이 CPU 를 쓸 때 게임이 먼저 돌게 합니다. 백그라운드 때문에 끊기는 컴퓨터에 효과가 있습니다. 게임을 끄거나 이 기능을 끄면 원래대로 돌아갑니다.",
                  "Lets the game run ahead of browsers, streaming and updates when they compete for the CPU. Reverts when the game or this option is turned off."),
                null);
            ch |= Option("lowthrottle", ref c.LowNoThrottle, T("윈도우 절전 제한 끄기", "No Windows power throttling"),
                T("윈도우 11 이 게임을 '효율 모드'로 느린 코어에 몰아넣지 않게 하고, 타이머 정밀도를 1ms 로 올려 프레임 간격이 덜 흔들리게 합니다. 노트북에 효과가 큽니다. 전기를 조금 더 씁니다.",
                  "Keeps Windows 11 from putting the game in efficiency mode and raises the timer resolution to 1 ms for steadier frame pacing. Helps laptops most; uses slightly more power."),
                null);
            BeginRow();
            GUILayout.Label(T("메뉴·에디터 FPS 제한", "Menu / editor FPS limit"), sBody);
            P(T("플레이 중이 아닐 때(메뉴, 에디터 편집, 맵 고르기) FPS 를 묶어 그래픽카드와 CPU 를 쉬게 합니다. 노트북은 발열이 줄어 플레이할 때 열 때문에 느려지는 일이 덜합니다. 곡을 시작하면 바로 원래 FPS 로 돌아가고, 맵을 불러오는 동안에는 묶지 않습니다. 수직동기가 켜져 있으면 적용되지 않습니다.",
                "Caps FPS outside of play (menus, editing, level select) so the GPU and CPU can rest; laptops run cooler and throttle less during play. Returns to your FPS as soon as a song starts, and never caps while a level loads. Has no effect with VSync on."), sDim);
            GUILayout.Space(6);
            int mf = c.LowMenuFps >= 60 ? 2 : c.LowMenuFps > 0 ? 1 : 0;
            if (Segment("lowmenufps", ref mf, new[] { T("끔", "Off"), "30", "60" })) { c.LowMenuFps = mf == 2 ? 60 : mf == 1 ? 30 : 0; ch = true; }
            EndRow();
            EndGroup();
            }
            if (sub == 2)
            {
            SubHeading(T("게임", "Game"), T("게임 안의 계산을 줄입니다. 아주 작은 차이(몇 프레임 늦게 시작 등)를 감수합니다.", "Cuts in-game work, accepting tiny differences (a few frames of delay)."));
            BeginGroup();
            ch |= Option("lowfloorsplit", ref c.LowFloorSplit, T("타일 이동 나눠 처리", "Split large tile moves"),
                T("타일 1000개 넘게 옮기는 효과를 지금 타일에서 가까운 것부터 몇 프레임에 나눠 처리합니다. 먼 타일이 처음 1~몇 프레임 늦게 움직이고, 끝나는 순간은 같습니다. \"효과 나누기 세기\" 가 프레임당 예산을 정합니다. \"타일 애니메이션 직접 처리\" 가 켜져 있어야 합니다.",
                  "Effects that move more than 1000 tiles are processed over a few frames, nearest tiles first. Far tiles start moving a frame or a few later but finish at the same moment. The effect split strength sets the per-frame budget. Needs tile move animations on."),
                null);
            ch |= Option("lowparticle", ref c.LowPauseParticles, T("화면 밖 파티클 멈추기", "Pause off-screen particles"),
                T("파티클 장식이 화면 밖에 있는 동안 시뮬레이션을 멈춰 CPU 를 아낍니다. 파티클이 많은 맵에서 효과가 있습니다. 다시 화면에 들어오면 멈춘 곳부터 이어가서 원래와 모양·시점이 조금 달라질 수 있습니다." + (Compat.QPauseOffscreenParticles ? " (지금은 Quartz 가 같은 일을 하고 있어 쉬는 중)" : ""),
                  "Stops simulating particle decorations while they are off-screen to save CPU on particle-heavy levels. When they come back on screen they resume where they stopped, so they may look slightly different from the original." + (Compat.QPauseOffscreenParticles ? " (Idle now: Quartz is doing the same)" : "")),
                null);
            ch |= Option("lowfft", ref c.LowNoFft, T("음악 반응 계산 끄기", "Skip music spectrum analysis"),
                T("게임은 매 프레임 음악 주파수를 분석하는데, 이 값을 쓰는 곳은 타일 색 방식 'Volume' 뿐입니다. 그 방식을 쓰는 타일이 없으면 분석을 건너뜁니다. 곡 도중 타일이 Volume 으로 바뀌면 바로 다시 켜며, 그 첫 한 프레임만 색이 한 프레임 늦을 수 있습니다.",
                  "The game analyses the music spectrum every frame, but only 'Volume' track colours use it. Skips it when no tile uses that mode; turns back on immediately if a tile switches to Volume (that first frame may lag by one frame)."),
                null);
            BeginRow();
            GUILayout.Label(T("효과 몰림 더 잘게 나누기", "Split effect bursts finer"), sBody);
            P(T("한 박자에 효과가 몰릴 때 한 프레임에 쓰는 시간을 더 짧게 끊어 여러 프레임에 나눕니다(타일 색 바꾸기도 더 작은 조각으로). CPU 가 약하면 멈칫이 줄어드는 대신, 몰린 효과 중 뒤쪽 것이 몇 프레임(수십 ms) 늦게 시작할 수 있습니다. 판정에는 영향이 없습니다.",
                "When many effects fire on one beat, spreads them over more frames with a shorter per-frame time (and smaller tile-recolour chunks). Fewer hitches on weak CPUs; later effects in a burst may start a few frames (tens of ms) late. Judgement is unaffected."), sDim);
            GUILayout.Space(6);
            int sp = Mathf.Clamp(c.LowSplit, 0, 2);
            if (Segment("lowsplit", ref sp, new[] { T("기본 (10ms)", "Default (10 ms)"), T("잘게 (5ms)", "Fine (5 ms)"), T("아주 잘게 (3ms)", "Finest (3 ms)") })) { c.LowSplit = sp; ch = true; }
            EndRow();
            EndGroup();
            }
            if (sub == 3)
            {
            SubHeading(T("그래픽카드", "Graphics card"), T("그래픽카드가 약할 때 그리는 양과 그래픽 메모리를 줄입니다. 화면이 조금 흐려질 수 있습니다.", "Draws less and uses less video memory on weak graphics cards. The view may get slightly softer."));
            BeginGroup();
            BeginRow();
            GUILayout.Label(T("게임 화면 해상도", "Game view resolution"), sBody);
            P(T("플레이 중 게임 화면(타일, 장식, 배경, 필터)을 이 배율로 작게 그린 뒤 늘려서 보여 줍니다. 그래픽카드가 약할수록 효과가 가장 큽니다(50% 면 그릴 픽셀이 4분의 1). 게임 화면이 흐려지고, 픽셀 크기를 쓰는 일부 필터는 모양이 조금 달라질 수 있습니다. HUD·설정 창 글자는 선명하게 남습니다. 바로 적용됩니다.",
                "Draws the game view (tiles, decorations, background, filters) at this scale during play and stretches it to the screen. Biggest win on weak graphics cards (50% = a quarter of the pixels). The game view gets softer and some pixel-based filters may look slightly different. HUD and this window stay sharp. Applies immediately."), sDim);
            GUILayout.Space(6);
            float fs = Mathf.Clamp(c.LowRenderScale, 10, 100);
            if (Slider("lowscale", ref fs, 10f, 100f, T("배율", "Scale"), Mathf.RoundToInt(fs) + "%"))
            {
                int v = Mathf.Clamp(Mathf.RoundToInt(fs / 5f) * 5, 10, 100);   // 5% 단위
                if (v != c.LowRenderScale) { c.LowRenderScale = v; ch = true; }
            }
            EndRow();
            ch |= Option("lowauto", ref c.LowAutoRes, T("자동 해상도 (목표 FPS 유지)", "Auto resolution (keep target FPS)"),
                T("그래픽카드가 바빠서 목표 FPS 를 못 맞출 때만 게임 화면 해상도를 10%씩 낮추고, 여유가 생기면 다시 올립니다. 가벼운 구간은 선명하게, 무거운 구간만 잠깐 흐려집니다. 위 슬라이더가 최대 배율입니다. CPU 가 한계라 느린 것은 해상도로 풀리지 않아 건드리지 않습니다. 해상도가 바뀌는 순간 아주 짧게 멈칫할 수 있어 바꾸는 간격을 두었습니다.",
                  "Lowers the game-view resolution in 10% steps only when the GPU can't keep the target FPS, and raises it back when there is headroom. Light parts stay sharp; only heavy parts get softer. The slider above is the maximum. CPU-bound slowdowns are left alone. A resolution change can cause a tiny hitch, so changes are spaced out."),
                null);
            bool extra = c.LowAutoRes || !LowEnd.RenderScaleReady || c.LowRenderScale < 100;
            if (extra) BeginRow();
            if (c.LowAutoRes)
            {
                int fi = c.LowAutoFps >= 240 ? 3 : c.LowAutoFps >= 144 ? 2 : c.LowAutoFps >= 120 ? 1 : 0;
                if (Segment("lowautofps", ref fi, new[] { "60 FPS", "120 FPS", "144 FPS", "240 FPS" })) { c.LowAutoFps = fi == 3 ? 240 : fi == 2 ? 144 : fi == 1 ? 120 : 60; ch = true; }
                float mn = Mathf.Clamp(c.LowAutoMin, 10, 100);
                if (Slider("lowautomin", ref mn, 10f, 100f, T("최소 배율", "Minimum"), Mathf.RoundToInt(mn) + "%"))
                {
                    int v = Mathf.Clamp(Mathf.RoundToInt(mn / 5f) * 5, 10, 100);
                    if (v != c.LowAutoMin) { c.LowAutoMin = v; ch = true; }
                }
                if (Hitch.Playing) GUILayout.Label(string.Format(T("지금 {0}% (GPU {1:F1}ms)", "Now {0}% (GPU {1:F1} ms)"), LowEnd.EffectivePct, LowEnd.GpuEma), sSub);
            }
            if (!LowEnd.RenderScaleReady) GUILayout.Label(T("이 게임 버전에서는 쓸 수 없습니다 (게임 코드 모양이 다름)", "Not available in this game version"), sSub);
            if (c.LowRenderScale < 100 || c.LowAutoRes)
            {
                GUILayout.Label(string.Format(T("게임 화면을 {0}×{1} 로 그립니다", "Game view drawn at {0}×{1}"), LowEnd.RTWidth(), LowEnd.RTHeight()), sSub);
                GUILayout.Space(6);
                int up = c.LowSharpUpscale ? 2 : c.LowFsr ? 1 : 0;
                if (Segment("lowsharp", ref up, new[] { T("부드럽게", "Smooth"), "FSR 1", T("도트처럼", "Pixelated") })) { c.LowSharpUpscale = up == 2; c.LowFsr = up == 1; ch = true; }
                if (c.LowFsr)
                {
                    P(T("AMD FSR 1 로 가장자리를 살려 늘리고 선명도를 보정합니다. 보통 늘리기보다 원본에 가깝고 덜 흐립니다. 화면 해상도로 두 번 더 그리므로 그래픽카드 일이 조금 늘어납니다.",
                        "Upscales with AMD FSR 1 (edge-aware upscale + sharpening). Closer to native and less blurry than plain upscaling; costs two extra screen-resolution passes."), sDim);   // 긴 설명은 줄바꿈되는 sDim (sSub 는 한 줄이라 패널이 옆으로 늘어나 페이지가 망가졌다)
                    if (Fsr.Failed) GUILayout.Label(T("이 컴퓨터에서는 쓸 수 없어 부드럽게 늘립니다", "Unavailable on this PC; using smooth upscale"), sSub);
                }
            }
            if (extra) EndRow();
            BeginRow();
            GUILayout.Label(T("장식 이미지 최대 크기", "Max decoration image size"), sBody);
            P(T("장식 이미지를 불러올 때 긴 변을 이 크기로 줄입니다. 그래픽 메모리가 적은 컴퓨터(내장 그래픽, 2~4GB 그래픽카드)에서 이미지가 많은 맵의 끊김과 로딩 시간이 줄어듭니다. 장식의 화면 크기는 그대로이고 선명도만 낮아집니다. '맵 불러오기' 페이지 설정보다 작은 쪽을 쓰며, 다음에 여는 맵부터 적용됩니다.",
                "Shrinks decoration images so their longer side is at most this size when loading. Helps PCs with little video memory on image-heavy levels (less stutter and faster loading). On-screen size stays the same; only sharpness drops. Uses the smaller of this and the Level loading setting; applies to the next level you open."), sDim);
            GUILayout.Space(6);
            int ic = c.LowImageCap >= 1024 ? 1 : c.LowImageCap > 0 ? 2 : 0;
            if (Segment("lowimg", ref ic, new[] { T("그대로", "Unchanged"), "1024", "512" })) { c.LowImageCap = ic == 1 ? 1024 : ic == 2 ? 512 : 0; ch = true; }
            EndRow();
            ch |= Option("lowcompress", ref c.LowCompressImages, T("이미지 압축해서 불러오기", "Compress images on load"),
                T("장식 이미지를 DXT 로 압축해서 그래픽카드에 올립니다. 그래픽 메모리가 4분의 1(투명 없는 이미지는 8분의 1)로 줄고 올리는 시간도 줄어듭니다. 압축은 이미지를 불러올 때 여러 CPU 코어에서 미리 합니다('맵 불러오기' 페이지의 '이미지 빠르게 불러오기' 가 켜져 있어야 함). 손실 압축이라 가까이서 보면 이미지가 조금 뭉개질 수 있습니다. 다음에 여는 맵부터 적용됩니다." +
                  (Compat.Pacl2Lossy ? " (지금 PACL2 의 이미지 손실 압축이 켜져 있어서, 이 옵션과 상관없이 PACL2 대신 여러 코어로 미리 압축하고 있습니다)" : ""),
                  "Uploads decoration images DXT-compressed: 1/4 of the video memory (1/8 for opaque images) and faster uploads. Compression is done ahead on several CPU cores while loading (needs 'Parallel image loading' on the Level loading page). Lossy, so images can look slightly blocky up close. Applies to the next level you open." +
                  (Compat.Pacl2Lossy ? " (PACL2 lossy image compression is on, so images are already pre-compressed on several cores in its place, regardless of this option)" : "")),
                null);
            EndGroup();
            }
            if (sub == 4)
            {
            SubHeading(T("실험", "Experimental"), T("아직 다듬는 중인 기능입니다. 화면이 마음에 들지 않으면 끄세요.", "Still being tuned. Turn off if you don't like how it looks."));
            BeginGroup();
            ch |= Option("lowsharpen", ref c.LowSharpen, T("늘린 화면 선명도 보정", "Sharpen the upscaled view"),
                T("게임 화면 해상도를 낮췄을 때 늘린 화면이 흐려 보이는 것을 선명도 보정으로 덜어 줍니다(FSR 1 의 선명도 단계를 흉내). 게임에 들어 있는 Sharpen 필터 셰이더를 빌려 화면 해상도에서 한 번 겁니다. UI 는 그대로입니다. 해상도가 100% 면 동작하지 않습니다.",
                  "Reduces the blur of a lowered game-view resolution with a sharpening pass (like FSR 1's sharpening step), using the game's built-in Sharpen filter shader at screen resolution. UI is unaffected. Does nothing at 100%."),
                null);
            if (c.LowSharpen)
            {
                BeginRow();
                float sv = c.LowSharpenValue;
                if (Slider("lowsharpv", ref sv, 0.25f, 4f, T("세기", "Strength"), sv.ToString("F2"))) { c.LowSharpenValue = Mathf.Round(sv * 20f) / 20f; ch = true; }
                if (!LowEnd.SharpenReady) GUILayout.Label(T("셰이더를 찾지 못해 쓸 수 없습니다", "Shader not found; unavailable"), sSub);
                EndRow();
            }
            ch |= Option("lowhalf", ref c.LowHalfRender, T("반만 그리기 + 카메라 보정", "Half-rate render + camera reprojection"),
                T("게임 화면을 두 프레임에 한 번만 그리고, 사이 프레임에는 지난 그림을 카메라가 움직인 만큼 밀고·돌리고·키워 보여 줍니다(VR 의 재투영과 같은 방식). 그래픽카드 일이 절반이 되고, 프레임 생성과 달리 지연이 늘지 않습니다. 대신 행성·장식·필터는 절반 속도로 움직이고, 배경 그림은 사이 프레임에 조금 밀릴 수 있으며, 빠르게 움직일 때 화면 가장자리가 잠깐 빌 수 있습니다. 그래픽카드가 한계인 컴퓨터에서만 효과가 있습니다.",
                  "Draws the game view every other frame; in between, the last image is shifted, rotated and scaled by the camera's movement (like VR reprojection). Halves GPU work without adding latency, unlike frame generation. Planets, decorations and filters update at half rate, background art may shift slightly on in-between frames, and edges may briefly show gaps during fast movement. Only helps when the graphics card is the bottleneck."),
                null);
            EndGroup();
            }
            if (ch) Save();
            if (sub != 0) return;
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (Btn(T("모두 켜기", "Turn all on"), sSecondary, GUILayout.Width(150), GUILayout.Height(38)))
            { c.LowPriority = c.LowNoThrottle = c.LowNoFft = true; c.LowRenderScale = 75; c.LowImageCap = 1024; c.LowSplit = 1; c.LowMenuFps = 60; Save(); }
            GUILayout.Space(8);
            if (Btn(T("모두 끄기", "Turn all off"), sSecondary, GUILayout.Width(150), GUILayout.Height(38)))
            { c.LowPriority = c.LowNoThrottle = c.LowNoFft = c.LowSharpUpscale = c.LowFsr = c.LowSharpen = c.LowHalfRender = c.LowAutoRes = c.LowPauseParticles = c.LowCompressImages = c.LowFloorSplit = false; c.LowRenderScale = 100; c.LowImageCap = 0; c.LowSplit = 0; c.LowMenuFps = 0; Save(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(14);
            InfoCard(new[]
            {
                T("게임 설정에서 더 할 수 있는 것", "More in the game's own settings"),
                T("해상도를 낮추기, 판정 오차 막대 끄기, 필터·그림자 효과를 줄이는 게임 옵션이 그래픽카드가 약한 컴퓨터에 가장 효과가 큽니다. 키뷰어 같은 다른 모드의 화면 표시도 매 프레임 비용이 듭니다.",
                  "Lower resolution, turning off the hit error meter and reducing filter effects in the game's options help weak graphics the most. On-screen overlays from other mods (e.g. key viewers) also cost time every frame."),
            });
        }

        private void PageMonitor()
        {
            var c = Main.Config;
            int sub = Sub();
            if (sub == 0) SubHeading(T("표시", "Display"), T("게임 화면 끝에 프레임, CPU, GPU, VRAM, RAM 사용량을 띄웁니다. 끊기면 왜 끊겼는지 알려 줍니다. 모니터를 잡고 끌면 위치를 옮길 수 있습니다.",
                "Shows frame time, CPU, GPU, VRAM and RAM at the screen edge and tells you why a hitch happened. Drag it to move it."));
            bool ch = false;

            if (sub == 0)
            {
            GUILayout.BeginVertical(sCard);
            GUILayout.Label(T("표시 방식", "Style"), sBody);
            GUILayout.Space(3);
            P(T("게임 중 " + Hotkey.Name(c.OverlayKey, c.OverlayMods) + " 로 차례로 바꿀 수 있습니다 (홈에서 키 변경). 아이콘은 누르면 상세 정보가 펼쳐집니다.",
                "Cycle with " + Hotkey.Name(c.OverlayKey, c.OverlayMods) + " in game (change it on Home). Click the icon to expand it."), sDim);
            GUILayout.Space(10);
            ch |= Segment("ovmode", ref c.OverlayMode, new[] { T("끔", "Off"), T("아이콘", "Icon"), T("미니", "Mini"), T("상세", "Detail") });
            GUILayout.Space(12);
            int side = c.OverlayRight ? 1 : 0;
            if (Segment("ovside", ref side, new[] { T("왼쪽 끝", "Left edge"), T("오른쪽 끝", "Right edge") })) { c.OverlayRight = side == 1; ch = true; }
            GUILayout.EndVertical();
            GUILayout.Space(12);

            // 모양
            GUILayout.BeginVertical(sCard);
            GUILayout.Label(T("모양", "Look"), sBody);
            GUILayout.Space(8);
            ch |= Slider("ovop", ref c.OverlayOpacity, 0.3f, 1f, T("불투명도", "Opacity"), (c.OverlayOpacity * 100).ToString("F0") + "%");
            ch |= Slider("ovscale", ref c.OverlayScale, 0.7f, 1.6f, T("크기", "Size"), (c.OverlayScale * 100).ToString("F0") + "%");
            ch |= Slider("ovy", ref c.OverlayY, 0f, 1f, T("세로 위치", "Vertical position"), c.OverlayY < 0.34f ? T("위", "Top") : c.OverlayY > 0.66f ? T("아래", "Bottom") : T("가운데", "Middle"));
            GUILayout.EndVertical();
            GUILayout.Space(12);

            }
            if (sub == 1)
            {
            SubHeading(T("항목", "Items"), T("모니터에 띄울 값을 고릅니다. 고른 것은 흰색으로 바뀝니다.", "Choose what the monitor shows. Selected items turn white."));
            GUILayout.Label(T("FPS 표시", "FPS display"), sBody);
            int fpsSource = c.OverlayFpsSource + 1;
            if (Segment("ovfpssource", ref fpsSource, new[] { T("자동", "Auto"), T("원본", "Real"), T("출력", "Output"), T("둘 다", "Both") })) { c.OverlayFpsSource = fpsSource - 1; ch = true; }
            P(T("기본값인 자동은 프레임 늘리기를 켜면 출력 FPS, 끄면 원본 FPS를 보여 줍니다. 직접 고른 표시 방식은 유지합니다. 출력 FPS는 생성 프레임까지 합친 실제 출력 제출 수이며 모니터 주사율이나 물리적으로 표시된 수가 아닙니다. 프레임 시간·1% low·끊김·이번 곡 통계는 원본 기준입니다.",
                "Auto, the default, shows output FPS when frame generation is active and real FPS otherwise. Manual selections are retained. Output FPS counts successful submissions including generated frames, not physical display refreshes. Frame time, 1% low, hitches and level statistics always use real frames."), sDim);
            GUILayout.Space(12);
            GUILayout.BeginVertical(sCard);
            GUILayout.Label(T("아이콘·미니에 보여 줄 항목", "Items in icon and mini"), sBody);
            GUILayout.Space(3);
            P(T("FPS 는 항상 보입니다. 사용률이 75%를 넘으면 주황, 90%를 넘으면 빨강으로 바뀝니다.",
                "FPS is always shown. Usage turns orange above 75% and red above 90%."), sDim);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            ch |= Chip(ref c.CmMs, T("프레임 시간", "Frame time"));
            ch |= Chip(ref c.CmLow, "1% low");
            ch |= Chip(ref c.CmCpu, "CPU");
            ch |= Chip(ref c.CmGpu, "GPU");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            ch |= Chip(ref c.CmVram, "VRAM");
            ch |= Chip(ref c.CmRam, "RAM");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(12);

            // 상세 정보에 보여 줄 항목
            GUILayout.BeginVertical(sCard);
            GUILayout.Label(T("상세 정보에 보여 줄 항목", "Items in the detail view"), sBody);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            ch |= Chip(ref c.OvGraph, T("그래프", "Graph"));
            ch |= Chip(ref c.OvCpu, "CPU");
            ch |= Chip(ref c.OvGpu, "GPU");
            ch |= Chip(ref c.OvVram, "VRAM");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            ch |= Chip(ref c.OvRam, "RAM");
            ch |= Chip(ref c.OvGc, T("메모리 정리", "GC"));
            ch |= Chip(ref c.OvHitchList, T("최근 끊김", "Recent hitches"));
            ch |= Chip(ref c.OvSession, T("이번 곡", "This level"));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(12);

            }
            if (sub == 2)
            {
            SubHeading(T("끊김 알림", "Hitch alerts"), T("프레임이 튀면 그 순간 원인을 화면에 띄웁니다.", "Shows the cause on screen the moment a frame spikes."));
            GUILayout.BeginVertical(sCard);
            P(T("프레임이 튀면 원인을 띄웁니다: 모드 작업(보라), 메모리 정리, 효과 몰림, GPU 과부하, 게임 처리, 게임 바깥(윈도우나 다른 프로그램). 같은 원인이 연달아 나면 ×2, ×3 으로 묶습니다.",
                "Shows the likely cause when a frame spikes: mod work (purple), memory cleanup, effect burst, GPU overload, game logic, or something outside the game. Repeats are grouped (×2, ×3)."), sDim);
            GUILayout.Space(10);
            int style = !c.HitchAlerts ? 0 : c.AlertDetailed ? 2 : 1;
            if (Segment("alertstyle", ref style, new[] { T("끔", "Off"), T("간단", "Simple"), T("자세히", "Detailed") }))
            {
                c.HitchAlerts = style > 0; c.AlertDetailed = style == 2; ch = true;
            }
            GUILayout.Space(10);
            ch |= Segment("alertpos", ref c.AlertPos, new[] { T("모니터 옆", "Beside monitor"), T("화면 위", "Top center"), T("화면 아래", "Bottom center") });
            GUILayout.Space(12);
            ch |= Slider("alertms", ref c.AlertMs, 20f, 100f, T("알림 기준", "Alert above"), c.AlertMs.ToString("F0") + "ms");
            P(T("이보다 긴 프레임만 알립니다. 33ms 는 60fps 기준 두 프레임이 밀린 것입니다.",
                "Only frames longer than this are reported. 33ms is two frames at 60 fps."), sDim);
            GUILayout.EndVertical();
            GUILayout.Space(12);
            InfoCard(new[]
            {
                T("VRAM 넘침", "VRAM spill"), T("VRAM 줄에 주황색 '넘침'이 뜨면 그래픽 메모리가 모자라 시스템 램으로 밀려난 것입니다. 이때 곡 중에 멈출 수 있습니다.",
                    "An orange 'spill' on the VRAM row means video memory ran out and data moved to system RAM, which can cause mid-song freezes."),
            });
            }
            if (ch) Save();
        }

        // 문제 보고: 바탕화면에 로그 묶음(zip)을 만들어 제작자에게 보낼 수 있게 한다
        private void ReportCard()
        {
            GUILayout.BeginVertical(sCard);
            GUILayout.Label(T("문제 보고용 로그 만들기", "Create a log for bug reports"), sBody);
            GUILayout.Space(4);
            P(T("게임이 끊기거나 오류가 났다면, 그 판을 끝낸 뒤(게임이 튕겼다면 다시 켠 뒤) 눌러 주세요. 바탕화면에 zip 파일이 생기고, 그 파일을 <b>모드 디스코드 서버</b>에 올리거나 디스코드 <b>narooh</b> 에게 DM 으로 보내 주세요. 사양, 설정, 모드 목록, 게임 로그, 끊김 기록이 들어가며 윈도우 사용자 이름은 가려집니다.",
                "If you hit a stutter or an error, press this after that run (or after restarting if the game crashed). A zip file appears on your desktop; post it on the <b>mod's Discord server</b> or send it to <b>narooh</b> on Discord (DM). It contains specs, settings, the mod list, game logs and the hitch record, with your Windows user name hidden."), sLead);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (Btn(T("로그 파일 만들기", "Create log file"), sPrimary, GUILayout.Width(170), GUILayout.Height(38)))
            {
                if (LogExport.Export() != null) LogExport.Reveal();
            }
            GUILayout.Space(10);
            if (Btn(T("디스코드 서버 열기", "Open Discord server"), sChip, GUILayout.Height(38), GUILayout.ExpandWidth(false))) Application.OpenURL(DiscordUrl);
            GUILayout.Space(10);
            if (LogExport.LastPath.Length > 0 && Btn(T("폴더 열기", "Show file"), sChip, GUILayout.Height(38), GUILayout.ExpandWidth(false))) LogExport.Reveal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (LogExport.LastError.Length > 0) { GUILayout.Space(6); P(T("만들지 못했습니다: ", "Failed: ") + LogExport.LastError, sDim); }
            else if (LogExport.LastPath.Length > 0) { GUILayout.Space(6); GUILayout.Label(T("만든 파일: ", "Created: ") + System.IO.Path.GetFileName(LogExport.LastPath) + T("  (바탕화면)", "  (desktop)"), sSub); }
            GUILayout.EndVertical();
        }

        private int filterKindSel;        // 필터 탭: 0 일반 / 1 고급
        private string filterGroupSel;    // 필터 탭: 고른 종류
        private void PageEffects()
        {
            var c = Main.Config;
            int sub = Sub();
            bool ch = false;
            if (sub == 0)
            {
                BeginGroup();
                if (Option("nofx", ref c.NoFx, T("노이펙 모드", "No-effects mode"),
                    T("아래 맵 효과를 전부 끄고 장식도 숨깁니다. 타일과 행성, 판정은 그대로라 맵의 박자만 보며 칠 수 있습니다. 히트박스 장식(닿으면 죽거나 이벤트가 일어나는 것)은 플레이에 필요해서 남깁니다. 게임 화면으로 열면 장식을 아예 만들지 않아 맵이 빨리 열립니다. 이미 켜진 효과는 다음 다시 하기부터, 장식은 다음 재생(게임 화면은 다음 맵 열기)부터 적용됩니다.",
                      "Turns off every level effect below and hides decorations. Tiles, planets and judgment stay, so you play to the rhythm alone. Hitbox decorations (that kill or trigger events) stay, since play needs them. Opened in the game screen, decorations are not created at all so the level opens faster. Effects already on stop from the next retry; decorations from the next play (or next open in the game screen)."), null))
                    ch = true;
                EndGroup();
                SubHeading(T("맵 효과", "Level effects"),
                    T("고른 효과를 시작하지 않습니다. 켜면 맵이 원래와 다르게 보이므로 전부 기본으로 꺼져 있습니다. 이미 켜져 있는 효과는 다음 다시 하기부터 사라집니다.",
                      "Skips the chosen effects. Levels then look different from the original, so everything is off by default. Effects already on disappear from the next retry."));
                BeginGroup();
                bool have = PlayTweaks.HaveLevel;
                foreach (var k in PlayTweaks.Kinds)
                {
                    bool v = k.OffNow;
                    int n = PlayTweaks.CountIn(k.Event);
                    string tag = c.NoFx ? T("노이펙 모드", "No-effects mode") : !have ? null : n > 0 ? string.Format(T("이 맵 {0}개", "{0} in this level"), n) : T("이 맵에 없음", "none here");
                    var oldC = GUI.color;
                    if (c.NoFx) GUI.color = new Color(oldC.r, oldC.g, oldC.b, oldC.a * 0.45f);
                    bool hit = Option("fxoff_" + k.Event, ref v, T(k.Ko + " 끄기", "Turn off " + k.En.ToLowerInvariant()), k.NoteKo != null ? T(k.NoteKo, k.NoteEn) : T("이 효과를 시작하지 않습니다.", "This effect is not started."), tag, !c.NoFx);
                    GUI.color = oldC;
                    if (hit && !c.NoFx)
                    {
                        k.Off = v; ch = true;
                        var list = new List<string>();
                        foreach (var q in PlayTweaks.Kinds) if (q.Off) list.Add(q.Event);
                        c.EffectsOff = string.Join(",", list.ToArray());
                    }
                    if (k.Tiles == PlayTweaks.TileLook.Anim && k.OffNow)
                    {
                        // 앞 타일이 몇 초 앞부터 보일지 (최소 4박자)
                        BeginRow();
                        float sec = Mathf.Clamp(c.NoFxAheadSec, 0.3f, 5f);
                        if (Slider("nofxahead", ref sec, 0.3f, 5f, T("미리 보기", "Look-ahead"), sec.ToString("F1") + T("초", " s")))
                        { c.NoFxAheadSec = Mathf.Round(sec * 10f) / 10f; ch = true; }
                        EndRow();
                    }
                }
                EndGroup();
                if (!have) P(T("맵을 열면 효과마다 이 맵에 몇 개 있는지 나옵니다.", "Open a level to see how many of each effect it has."), sDim);

            }
            else if (sub == 1)
            {
                bool have = PlayTweaks.HaveLevel;
                var fl = have ? PlayTweaks.FiltersInLevel() : null;
                if (fl == null || fl.Count == 0)
                {
                    SubHeading(T("필터 하나씩", "Filters one by one"), have ? T("이 맵은 필터를 쓰지 않습니다.", "This level uses no filters.") : T("맵을 열면 이 맵이 쓰는 필터가 많이 쓰는 순서로 나옵니다.", "Open a level to list the filters it uses, most used first."));
                }
                if (fl != null && fl.Count > 0)
                {
                    SubHeading(T("필터 하나씩", "Filters one by one"),
                        T("이 맵이 쓰는 필터를 하나씩 끌 수 있습니다. 그 필터만 켜지지 않고, 같은 효과의 다른 동작(다른 필터 끄기 등)은 그대로 합니다. 위에서 필터 전체를 끄면 이것과 상관없이 전부 꺼집니다.",
                          "Turn off the filters this level uses one at a time. Only that filter stays off; the rest of the event (such as turning other filters off) still happens. Turning all filters off above overrides this."));
                    // 묶음별로 (묶음 안에서 많이 쓰는 순, 묶음은 합계가 많은 순)
                    var groups = new List<KeyValuePair<string, List<KeyValuePair<string, int>>>>();
                    var gname = new Dictionary<string, string>(); var gtotal = new Dictionary<string, int>();
                    foreach (var kv in fl)
                    {
                        var g = PlayTweaks.FilterGroup(kv.Key);
                        int gi = groups.FindIndex(x => x.Key == g.Key);
                        if (gi < 0) { groups.Add(new KeyValuePair<string, List<KeyValuePair<string, int>>>(g.Key, new List<KeyValuePair<string, int>>())); gi = groups.Count - 1; gname[g.Key] = g.Value; gtotal[g.Key] = 0; }
                        groups[gi].Value.Add(kv); gtotal[g.Key] += kv.Value;
                    }
                    groups.Sort((a, b) => gtotal[b.Key].CompareTo(gtotal[a.Key]));
                    bool changed = false;
                    // 일반 / 고급 나누고, 왼쪽에 종류 목록 · 오른쪽에 고른 종류의 필터
                    var normal = groups.FindAll(x => x.Key.StartsWith("N:"));
                    var adv = groups.FindAll(x => x.Key.StartsWith("A:"));
                    int nN = 0, nA = 0; foreach (var g in normal) nN += g.Value.Count; foreach (var g in adv) nA += g.Value.Count;
                    if (normal.Count == 0) filterKindSel = 1; else if (adv.Count == 0) filterKindSel = 0;
                    if (normal.Count > 0 && adv.Count > 0)
                    {
                        if (Segment("filterkind", ref filterKindSel, new[] { string.Format(T("일반 필터 {0}종", "Filters ({0})"), nN), string.Format(T("고급 필터 {0}종", "Advanced ({0})"), nA) })) { filterGroupSel = null; scroll = Vector2.zero; }
                        GUILayout.Space(14);
                    }
                    var list = filterKindSel == 0 ? normal : adv;
                    if (filterGroupSel == null || !list.Exists(x => x.Key == filterGroupSel)) filterGroupSel = list.Count > 0 ? list[0].Key : null;
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(176));
                    var ev = Event.current;
                    foreach (var g in list)
                    {
                        var r = GUILayoutUtility.GetRect(176, 36, GUILayout.Width(176), GUILayout.Height(36));
                        GUILayout.Space(2);
                        bool on = g.Key == filterGroupSel, hov = !on && r.Contains(ev.mousePosition);
                        if (on) Fill(r, Surface2, 8); else if (hov) Fill(r, Soft, 8);
                        int off = g.Value.FindAll(x => PlayTweaks.FiltersOff.Contains(x.Key)).Count;
                        string right = off == 0 ? string.Format(T("{0}종", "{0}"), g.Value.Count) : off == g.Value.Count ? T("다 끔", "all off") : string.Format(T("{0}/{1} 끔", "{0}/{1} off"), off, g.Value.Count);
                        GUI.Label(new Rect(r.x + 12, r.y, r.width - 70, r.height), gname[g.Key].Replace(T("고급 · ", "Advanced · "), ""), on ? sRailOn : sRail);
                        GUI.Label(new Rect(r.xMax - 80, r.y + (r.height - 18f) * 0.5f, 70, 18), right, sSmallRight);
                        if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition)) { ev.Use(); filterGroupSel = g.Key; }
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(18);
                    GUILayout.BeginVertical();
                    foreach (var g in list)
                    {
                        if (g.Key != filterGroupSel) continue;
                        bool gv = g.Value.TrueForAll(x => PlayTweaks.FiltersOff.Contains(x.Key));
                        BeginGroup();
                        if (Option("filtergroup_" + g.Key, ref gv, string.Format(T("{0} 전부 끄기", "Turn off all {0}"), gname[g.Key]), string.Format(T("이 종류 {0}종을 한 번에 끕니다.", "Turns off all {0} filters of this kind."), g.Value.Count), string.Format(T("{0}번", "{0}x"), gtotal[g.Key])))
                        {
                            foreach (var x in g.Value) { if (gv) PlayTweaks.FiltersOff.Add(x.Key); else PlayTweaks.FiltersOff.Remove(x.Key); }
                            changed = true;
                        }
                        foreach (var kv in g.Value)
                        {
                            bool v = PlayTweaks.FiltersOff.Contains(kv.Key);
                            if (Option("filteroff_" + kv.Key, ref v, PlayTweaks.FilterLabel(kv.Key), T("이 필터를 켜지 않습니다.", "This filter is never turned on."), string.Format(T("{0}번", "{0}x"), kv.Value), 1, null))
                            {
                                if (v) PlayTweaks.FiltersOff.Add(kv.Key); else PlayTweaks.FiltersOff.Remove(kv.Key);
                                changed = true;
                            }
                        }
                        EndGroup();
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    if (changed)
                    {
                        var arr = new List<string>(PlayTweaks.FiltersOff);
                        c.FiltersOff = string.Join("|", arr.ToArray());
                        ch = true;
                    }
                }
            }
            else
            {
                SubHeading(T("판정 글자", "Judgment text"), T("플레이 중 타일 위에 뜨는 판정 글자(완벽, 빠름, 느림…)를 숨깁니다. 판정 자체와 정확도 기록은 그대로입니다.",
                    "Hides the judgment text over the tiles (Perfect, Early, Late...). Judgment and accuracy are unchanged."));
                BeginGroup();
                ch |= Option("hidejudgeall", ref c.HideJudgeAll, T("판정 글자 전부 숨기기", "Hide all judgment text"), T("모든 판정 글자를 띄우지 않습니다.", "Shows no judgment text."), null);
                ch |= Option("hidejudgeperfect", ref c.HideJudgePerfect, T("완벽 판정만 숨기기", "Hide Perfect only"), T("완벽 판정 글자만 띄우지 않고, 나머지(빠름, 느림, 놓침…)는 그대로 띄웁니다.", "Hides only Perfect; Early, Late, Miss and the rest still show."), null);
                EndGroup();
                SubHeading(T("에디터", "Editor"), T("에디터에서 플레이할 때 쓰는 단추와 조작입니다.", "Buttons and input for playing from the editor."));
                BeginGroup();
                ch |= Option("gamescreenbtn", ref c.GameScreenButton, T("에디터에 게임 화면 단추", "Game screen button in the editor"),
                    T("에디터 재생 단추 오른쪽 위에 맵을 게임 화면(커스텀 맵 목록에서 연 것과 같은 화면)으로 여는 단추를 둡니다. 에디터 재생보다 조금 가볍고, 노이펙 모드면 장식을 만들지 않아 더 빨리 열립니다. 에디터로 돌아올 때는 일시정지 메뉴의 에디터 단추를 누릅니다. 저장하지 않은 변경이 있으면 먼저 저장할지 묻습니다.",
                      "Adds a button above-right of the editor's play button that opens the level in the game screen (as from the custom level list). It is a bit lighter than editor play, and in no-effects mode decorations are not created so it opens faster. Use the editor button in the pause menu to come back. Asks to save unsaved changes first."), null);
                ch |= Option("noplayzoom", ref c.NoPlayZoom, T("플레이 중 마우스 휠 확대 막기", "No mouse-wheel zoom while playing"),
                    T("에디터에서 재생하는 동안 마우스 휠을 굴려도 화면 크기가 바뀌지 않습니다. 편집할 때는 그대로 확대·축소됩니다.", "While playing from the editor, the mouse wheel no longer zooms. Zooming while editing still works."), null);
                EndGroup();
            }
            if (ch) Save();
        }

        private void PageAbout()
        {
            Heading("Stutter Fix", "v" + Main.Entry.Info.Version + "  ·  " + (English ? (Edition.Dev ? "developer build" : "player build") : Edition.Name));
            GUILayout.BeginVertical(sCardDark);
            GUILayout.Label("naro & Claude", sStatDark);
            GUILayout.Space(6);
            GUILayout.Label(T("실제 맵에서 끊긴 순간을 하나씩 측정해 원인을 찾고, 원인마다 고친 모드입니다.",
                "Built by measuring each real hitch in real levels, finding its cause, and fixing it."), sStatLabelDark);
            GUILayout.EndVertical();
            GUILayout.Space(14);
            InfoCard(new[]
            {
                T("모드를 끄면", "Turning it off"), T("UMM 에서 끄면 모든 변경을 즉시 되돌립니다. 멀티스레드 그리기는 다음 실행부터 원래대로 돌아갑니다.",
                    "Turning the mod off in UMM reverts everything immediately. Multithreaded rendering reverts on the next launch."),
                T("그래도 끊긴다면", "Still stuttering?"), T("필터가 아주 많이 겹치는 구간은 그래픽카드 한계이고, 백그라운드 프로그램이 순간 끊김을 만들 수도 있습니다. 원격 데스크톱(StarDesk 등)·화면 녹화 프로그램이 켜져 있으면 판마다 FPS 가 크게 떨어질 수 있으니 게임할 때는 끄세요.",
                    "Scenes stacking many full-screen filters are limited by the GPU, and background apps can cause occasional hitches. Remote desktop (StarDesk etc.) or screen recording apps can drop FPS a lot in some runs; close them while playing."),
                T("디스코드 서버", "Discord server"), T("discord.gg/csys9ZAeD6 (버그 제보, 기능 아이디어, 질문)", "discord.gg/csys9ZAeD6 (bug reports, feature ideas, questions)"),
                T("소스", "Source"), "github.com/pding4569/StutterFix",
            });
            GUILayout.Space(4);
            TroubleCard();
            UpdateCard();
            ReportCard();
        }

        // 문제 해결: 다른 모드와 겹치는 곳, 자동 보호 상태, 안전 모드 켜기
        private void TroubleCard()
        {
            string shared = Compat.SharedSummary.Length > 0 ? Compat.SharedSummary : T("없음 (또는 아직 확인 전)", "None (or not checked yet)");
            string state = Resilience.SafeMode ? T("안전 모드로 켜져 있음", "Running in safe mode")
                : Resilience.OffCount > 0 ? T("오류가 반복된 기능을 이번 실행 동안 꺼 둠", "Some features turned off for this run after repeated errors")
                : T("문제 없음", "No problems");
            InfoCard(new[]
            {
                T("다른 모드와 부딪히면", "If another mod conflicts"),
                T("이 모드 기능에서 오류가 5번 반복되면 그 기능만 이번 실행 동안 자동으로 끕니다. 게임이 두 번 연속 비정상 종료되면 다음 실행은 안전 모드(게임에 깊이 관여하는 기능을 끔)로 켜집니다. 문제가 계속되면 아래 버튼으로 안전 모드를 켜 보고, '로그 파일 만들기' 로 생긴 zip 을 보내 주세요.",
                  "If a feature of this mod errors 5 times, only that feature is turned off for this run. If the game ends abnormally twice in a row, the next launch starts in safe mode (features that hook deep into the game are off). If problems continue, try safe mode below and send the zip from 'Create log file'."),
                T("지금 상태", "Status"), state,
                T("같은 게임 함수를 고치는 다른 모드", "Other mods patching the same game functions"), shared,
            });
            GUILayout.BeginHorizontal();
            if (!Resilience.SafeMode && Btn(T("안전 모드로 (이번 실행)", "Safe mode (this run)"), sPrimary, GUILayout.Width(210), GUILayout.Height(38))) Resilience.EnterSafeModeManual();
            if (Resilience.SafeMode && Btn(T("안전 모드 끄기", "Leave safe mode"), sPrimary, GUILayout.Width(170), GUILayout.Height(38))) Resilience.LeaveSafeMode();
            GUILayout.EndHorizontal();
            GUILayout.Space(14);
        }

        // 업데이트: 상태, 받기 / 지금 확인 버튼, 자동 확인 켜기
        private void UpdateCard()
        {
            var c = Main.Config;
            string st = Updater.Status.Length > 0 ? Updater.Status : string.Format(T("지금 v{0}", "Current v{0}"), Main.Entry.Info.Version);
            InfoCard(new[] { T("업데이트", "Updates"), st });
            // 새 버전의 패치노트 (릴리스 설명)
            if ((Updater.Available || Updater.Installed) && Updater.Notes.Length > 0)
                InfoCard(new[] { string.Format(T("v{0} 에서 바뀐 점", "What's new in v{0}"), Updater.Latest), Updater.Notes });
            GUILayout.BeginHorizontal();
            GUI.enabled = !Updater.Busy;
            if (Updater.Available)
            {
                if (Btn(string.Format(T("v{0} 받기", "Get v{0}"), Updater.Latest), sPrimary, GUILayout.Width(170), GUILayout.Height(38))) Updater.Download();
                GUILayout.Space(8);
                if (Btn(T("바뀐 점 보기", "What's new"), sPrimary, GUILayout.Width(150), GUILayout.Height(38))) Application.OpenURL(Updater.NotesUrl);
            }
            else if (!Updater.Installed)
            {
                if (Btn(T("지금 확인", "Check now"), sPrimary, GUILayout.Width(150), GUILayout.Height(38))) Updater.Check();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            if (Option("autoupdate", ref c.CheckUpdates, T("자동 업데이트", "Automatic updates"),
                T("게임을 켜면(그 뒤로 3시간마다) GitHub 에서 새 버전을 확인하고, 있으면 알아서 받아 설치합니다. 게임을 다시 켜면 새 버전이 적용됩니다. 곡(에디터 재생 포함) 중에는 확인도 설치도 하지 않고 곡이 끝난 뒤에 합니다. 끄면 새 버전이 있다는 알림만 보고 버튼으로 받습니다.",
                  "On launch (and every 3 hours) checks GitHub for a newer version and installs it automatically; it applies on the next launch. Nothing runs while a level (or editor playtest) is playing. Turn off to only get a notice and update with the button."), null)) Save();
        }

        private static string LoadSummary()
        {
            if (ImagePrefetch.Last == "아직 안 함") return T("아직 맵을 불러오지 않았습니다", "No level loaded yet");
            return ImagePrefetch.Last;
        }

        // ── 부품 ───────────────────────────────────────────────────────
        private void Heading(string title, string lead)
        {
            GUILayout.Space(4);
            P(lead, sLead);
            GUILayout.Space(14);
        }

        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly Dictionary<string, string[]> descSplit = new Dictionary<string, string[]>();
        // 설명의 첫 문장을 한 줄 요약으로, 나머지를 '자세히'로
        private string[] SplitDesc(string desc)
        {
            string[] r;
            if (descSplit.TryGetValue(desc, out r)) return r;
            int cut = -1;
            int nlp = desc.IndexOf('\n');
            for (int i = 0; i < desc.Length - 1; i++)
                if (desc[i] == '.' && (desc[i + 1] == ' ' || desc[i + 1] == '\n') && (i == 0 || !char.IsDigit(desc[i - 1]))) { cut = i + 1; break; }
            if (nlp >= 0 && (cut < 0 || nlp < cut)) cut = nlp;
            r = cut > 0 && cut < desc.Length - 1 ? new[] { desc.Substring(0, cut).Trim(), desc.Substring(cut).Trim() } : new[] { desc.Trim(), "" };
            if (descSplit.Count > 400) descSplit.Clear();
            descSplit[desc] = r;
            return r;
        }

        private bool Option(string key, ref bool value, string title, string desc, string tag, bool clickable = true)
        {
            var parts = SplitDesc(desc ?? "");
            bool hasMore = parts[1].Length > 0, open = hasMore && expanded.Contains(key);
            Rect last;
            var e0 = Event.current;
            if (e0.type == EventType.Repaint && rowRects.TryGetValue(key, out last) && last.Contains(e0.mousePosition)) Fill(last, RowHover, 9);   // Raycast 처럼 둥근 고른 줄
            GUILayout.BeginHorizontal(sRow);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, sRowTitle, GUILayout.ExpandWidth(false));
            if (tag != null) { GUILayout.Space(8); GUILayout.Label(tag, sTag, GUILayout.ExpandWidth(false)); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            P(parts[0] + (hasMore && !open ? T("  <color=#737881>자세히</color>", "  <color=#737881>More</color>") : ""), sDim);
            if (open) { GUILayout.Space(6); P(parts[1], sDetail); }
            GUILayout.EndVertical();
            GUILayout.Space(20);
            Rect r = GUILayoutUtility.GetRect(34, 20, GUILayout.Width(34), GUILayout.Height(20));
            GUILayout.EndHorizontal();
            Rect row = GUILayoutUtility.GetLastRect();
            if (e0.type == EventType.Repaint) rowRects[key] = row;
            r.y = row.y + sRow.padding.top + 1f;   // 스위치는 제목 높이에
            DrawSwitch(key, r, value);

            // 스위치를 누르면 켜고 끄고, 줄의 나머지를 누르면 자세히 펼치고 접는다
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                var hit = new Rect(r.x - 10, r.y - 8, r.width + 20, r.height + 16);
                if (clickable && (hit.Contains(e.mousePosition) || !hasMore)) { e.Use(); value = !value; return true; }
                if (hasMore) { e.Use(); if (open) expanded.Remove(key); else expanded.Add(key); }
            }
            return false;
        }

        // 여러 개 중 하나 고르기: 옅은 바탕 위에서 흰 선택 칸이 미끄러진다
        private bool Segment(string key, ref int value, string[] labels)
        {
            Rect r = GUILayoutUtility.GetRect(10, 30, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            float w = r.width / labels.Length;
            var e = Event.current;
            bool changed = false;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                int v = Mathf.Clamp((int)((e.mousePosition.x - r.x) / w), 0, labels.Length - 1);
                if (v != value) { value = v; changed = true; }
                e.Use();
            }
            if (e.type == EventType.Repaint)
            {
                float x;
                if (!anim.TryGetValue(key, out x)) x = value;
                x = Mathf.Lerp(x, value, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
                anim[key] = x;
                Fill(r, Soft, 7);
                var sel = new Rect(r.x + 3 + x * w, r.y + 3, w - 6, r.height - 6);
                sSegKnob.Draw(sel, false, false, false, false);
                for (int i = 0; i < labels.Length; i++)
                    (i == value ? sSegOnText : sSegText).Draw(new Rect(r.x + i * w, r.y, w, r.height), labels[i], false, false, false, false);
            }
            return changed;
        }

        // 가로 막대를 끌어서 값 고르기
        private bool Slider(string key, ref float value, float min, float max, string label, string shown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sDimMid, GUILayout.Width(110), GUILayout.Height(28));   // 막대와 같은 높이 가운데 (위에 붙어 막대보다 떠 보였다)
            Rect r = GUILayoutUtility.GetRect(10, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            GUILayout.Label(shown, sSliderValue, GUILayout.Width(64), GUILayout.Height(28));
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            int id = GUIUtility.GetControlID(key.GetHashCode(), FocusType.Passive, r);
            var e = Event.current;
            float old = value;
            var track = new Rect(r.x + 9, r.center.y - 2, r.width - 18, 4);
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; value = Pick(track, e.mousePosition.x, min, max); e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) { value = Pick(track, e.mousePosition.x, min, max); e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.Repaint:
                    float k = Mathf.InverseLerp(min, max, value);
                    var thin = new Rect(track.x, track.center.y - 1f, track.width, 2f);
                    Fill(thin, TrackOff, 1);
                    Fill(new Rect(thin.x, thin.y, thin.width * k, thin.height), Ink, 1);
                    float kx = track.x + track.width * k;
                    float kr = GUIUtility.hotControl == id ? 7f : 6f;
                    Fill(new Rect(kx - kr, track.center.y - kr, kr * 2, kr * 2), Ink, kr);
                    break;
            }
            return !Mathf.Approximately(old, value);
        }

        private static float Pick(Rect track, float x, float min, float max)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01((x - track.x) / track.width));
        }

        // 켜고 끄는 작은 알약 버튼
        private bool Chip(ref bool on, string label)
        {
            var content = new GUIContent(label);
            var st = on ? sChipOn : sChip;
            Rect r = GUILayoutUtility.GetRect(content, st, GUILayout.Height(28));
            GUILayout.Space(6);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { on = !on; e.Use(); return true; }
            if (e.type == EventType.Repaint) st.Draw(r, content, r.Contains(e.mousePosition), false, false, false);
            return false;
        }

        private void DrawSwitch(string key, Rect r, bool on)
        {
            if (Event.current.type != EventType.Repaint) return;
            float target = on ? 1f : 0f, t;
            if (!anim.TryGetValue(key, out t)) t = target;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 8f);
            anim[key] = t;
            float k = Mathf.SmoothStep(0, 1, t);
            var track = new Rect(r.x, r.y, 34, 20);
            Fill(track, Color.Lerp(TrackOff, Ink, k), 10);
            float kx = Mathf.Lerp(track.x + 3, track.xMax - 17, k);
            Fill(new Rect(kx, track.y + 3, 14, 14), Color.Lerp(Text2, Page, k), 7);
        }

        private void Stat(string value, string label, bool dark)
        {
            GUILayout.BeginVertical(dark ? sCardDark : sCard, GUILayout.ExpandWidth(true), GUILayout.Height(88));
            GUILayout.Label(value, dark ? sStatDark : sStat);
            GUILayout.FlexibleSpace();
            GUILayout.Label(label, dark ? sStatLabelDark : sStatLabel);
            GUILayout.EndVertical();
        }

        // 라벨/값 짝을 줄마다 나눠 한 카드에 담는다
        private void InfoCard(string[] kv)
        {
            GUILayout.BeginVertical(sCard);
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                if (i > 0)
                {
                    GUILayout.Space(11);
                    Rect line = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.Height(1));
                    Fill(line, Rule, 1);
                    GUILayout.Space(11);
                }
                GUILayout.Label(kv[i], sSmall);
                GUILayout.Space(3);
                string v = kv[i + 1];
                if (v.Length > 40 || v.EndsWith(".") || v.IndexOf('\n') >= 0) P(v, sBodyText);   // 긴 글은 굵게 하지 않는다 (문단이 무거워 보였다)
                else GUILayout.Label(v, sBody);
            }
            GUILayout.EndVertical();
            GUILayout.Space(14);
        }

        // ── 띄어쓰기 자리에서만 줄 바꾸기 ──
        // 유니티 IMGUI 는 한글을 글자마다 끊을 수 있는 자리로 봐서, 단어 중간에서 줄이 바뀌었다("않습니/다", "떨/어졌습니다").
        // 그려질 폭을 알면(Repaint 의 Rect) 띄어쓰기 자리에서 미리 줄을 바꾼 글을 만들어 그린다. 레이아웃 높이는 지난번 폭으로 만든 글로 잰다.
        private sealed class Para { public float Width = -1f, Height = -1f; public string Shown; public readonly GUIContent Content = new GUIContent(); }
        private readonly Dictionary<KeyValuePair<string, GUIStyle>, Para> paras = new Dictionary<KeyValuePair<string, GUIStyle>, Para>();
        private readonly Dictionary<GUIStyle, GUIStyle> noWrap = new Dictionary<GUIStyle, GUIStyle>();

        // 창이 열려 있으면 프레임마다 불리므로 같은 글에는 새로 할당하지 않는다 (곡 중에 열어 둘 수도 있다)
        private void P(string text, GUIStyle s, params GUILayoutOption[] opts)
        {
            if (string.IsNullOrEmpty(text)) { GUILayout.Label(text, s, opts); return; }
            var key = new KeyValuePair<string, GUIStyle>(text, s);
            Para e;
            if (!paras.TryGetValue(key, out e))
            {
                if (paras.Count > 300) paras.Clear();
                e = new Para { Shown = text }; e.Content.text = text; paras[key] = e;
            }
            // 높이는 그릴 폭에서 잰 값을 쓴다. 유니티 레이아웃이 더 좁은 폭으로 다시 줄을 나눠 재면 아래에 빈 줄만큼 자리가 남았다.
            Rect r = opts.Length == 0 && e.Height > 0f ? GUILayoutUtility.GetRect(e.Content, s, GUILayout.Height(e.Height)) : GUILayoutUtility.GetRect(e.Content, s, opts);
            if (Event.current.type != EventType.Repaint || r.width < 20f) return;
            float inner = r.width - s.padding.horizontal;
            if (Mathf.Abs(e.Width - inner) > 0.5f) { e.Width = inner; e.Shown = Wrapped(text, s, inner); e.Content.text = e.Shown; e.Height = s.CalcHeight(e.Content, r.width); }
            GUI.Label(r, e.Content, s);
        }

        private string Wrapped(string text, GUIStyle s, float width)
        {
            GUIStyle m;
            if (!noWrap.TryGetValue(s, out m)) { m = new GUIStyle(s) { wordWrap = false }; m.padding = new RectOffset(); noWrap[s] = m; }
            float max = width * 0.96f - 4f;   // 잰 폭과 그릴 때 폭이 조금 달라도(배율 적용 글꼴) 유니티가 한 번 더 끊지 않게
            var sb = new System.Text.StringBuilder(text.Length + 8);
            var tmp = new GUIContent();
            var lines = new List<string>();
            string[] parts = text.Split('\n');
            for (int pi = 0; pi < parts.Length; pi++)
            {
                if (pi > 0) sb.Append('\n');
                string[] words = parts[pi].Split(' ');
                // 번호 항목(①, (1))은 둘째 줄부터 번호 뒤 글자 자리에서 시작한다
                string indent = "";
                if (words.Length > 1 && IsMarker(words[0]))
                {
                    tmp.text = words[0] + " "; float mw = m.CalcSize(tmp).x;
                    tmp.text = "          "; float sw = m.CalcSize(tmp).x / 10f;
                    if (sw > 0f) indent = new string(' ', Mathf.Clamp(Mathf.RoundToInt(mw / sw), 0, 8));
                }
                lines.Clear();
                string line = "";
                foreach (string word in words)
                {
                    string cand = line.Length == 0 ? (lines.Count > 0 ? indent + word : word) : line + " " + word;
                    tmp.text = cand;
                    if (line.Length == 0 || m.CalcSize(tmp).x <= max) { line = cand; continue; }
                    lines.Add(line);
                    line = indent + word;
                }
                lines.Add(line);
                // 마지막 줄에 짧은 조각만 남으면("번).") 앞줄 끝 단어를 같이 내린다
                int n = lines.Count;
                if (n >= 2)
                {
                    tmp.text = lines[n - 1]; float lastW = m.CalcSize(tmp).x;
                    string prev = lines[n - 2]; int sp = prev.LastIndexOf(' ');
                    if (lastW < max * 0.18f && sp > indent.Length)
                    {
                        string moved = prev.Substring(sp + 1) + " " + lines[n - 1].TrimStart(' ');
                        tmp.text = indent + moved;
                        if (m.CalcSize(tmp).x <= max) { lines[n - 2] = prev.Substring(0, sp); lines[n - 1] = indent + moved; }
                    }
                }
                for (int li = 0; li < lines.Count; li++) { if (li > 0) sb.Append('\n'); sb.Append(lines[li]); }
            }
            return sb.ToString();
        }

        private static bool IsMarker(string w)
        {
            if (w.Length == 1 && w[0] >= '\u2460' && w[0] <= '\u2473') return true;   // ① ~ ⑳
            return w.Length >= 3 && w[0] == '(' && w[w.Length - 1] == ')' && char.IsDigit(w[1]);
        }

        private void Fill(Rect r, Color c, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, tWhite, ScaleMode.StretchToFill, true, 0, Faded(c), 0, radius);
        }

        // 색 인자를 받는 DrawTexture 는 GUI.color 의 투명도를 따르지 않으므로 직접 곱한다
        private static Color Faded(Color c) { return new Color(c.r, c.g, c.b, c.a * GUI.color.a); }

        private void SetLanguage(string lang)
        {
            Main.Config.Language = lang;
            try { Main.Config.Save(Main.Entry); } catch { }
        }

        private void ResetDefaults()
        {
            var c = Main.Config;
            c.GcPause = c.EffectSplit = c.RecolorSplit = c.TweenGuard = c.SkipSameText = c.FilterTypeCache = c.MeshWarm = c.SoundWarm = c.JitWarm = c.ShaderWarm = c.FastBlend = c.SkipInvisible = c.LazyHidden = c.ZeroTween = c.InstantDirect = c.SkipSame = c.FastLoop = c.Precheck = c.DecoAnim = c.FloorAnim = c.MoveFinish = c.DormantSkip = c.ImagePrefetch = c.SkipAssetUnload = c.SkipIdleParticles = c.LeakFix = c.LoadCache = true;
            if (!c.LegacyGfxJobs) { c.LegacyGfxJobs = true; BootConfig.Apply(true, c.FlipModel == 1); }
            Save();
        }

        private void Save()
        {
            Main.ApplyConfig();
            try { Main.Config.Save(Main.Entry); } catch { }
        }

        // ── 모양 만들기 ─────────────────────────────────────────────────
        // 설정 창과 실시간 모니터가 같이 쓰는 글꼴 (윈도우의 Segoe UI + 맑은 고딕)
        private static Font uiFont;
        internal static Font UiFont()
        {
            if (uiFont != null) return uiFont;
            try { uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Malgun Gothic", "Arial" }, 16); } catch { uiFont = null; }
            if (uiFont == null) uiFont = GUI.skin.font;
            return uiFont;
        }

        // 글자를 처음 그릴 때(글꼴/굵기/크기마다) 글자 이미지를 새로 만들고, 그때 30~90ms 멈춘다.
        // 모니터를 처음 띄우거나 새 알림 문구가 나올 때 "모드 작업 (모니터 그리기)" 끊김으로 잡히던 게 이것이었다.
        // 유니티 6 의 IMGUI 는 TextCore 로 글자를 그린다(IMGUITextHandle). 예전에는 옛 방식
        // (Font.RequestCharactersInTexture)으로 미리 만들었는데 TextCore 와는 상관이 없어 효과가 없었고 1.1초만 먹었다.
        // 지금은 창이 실제로 쓰는 스타일마다 쓰는 글자 전부의 크기를 한 번 잰다. 크기를 재려면 TextCore 가 글자를
        // 만들어야 하므로 그때 한꺼번에 만들어진다. 스타일을 만든 직후(OnGUI 안, 불러오기로 표시) 한 번만 한다.
        internal const string WarmAscii = WarmText;
        private const string WarmText = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·×→←↑↓…°%";
        // scale: 그 창이 GUI.matrix 로 키우는 배율. 글자는 화면에 실제로 그려지는 크기마다 따로 만들어지므로
        // 같은 배율을 걸고 재고, 한 번은 실제로(투명하게) 그려 둔다. 그리기 이벤트(Repaint)에서만 한다. 했으면 true.
        internal static bool WarmStyles(object owner, float scale)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return false;
            var oldM = GUI.matrix; var oldC = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                GUI.color = new Color(1, 1, 1, 0f);
                PerfOverlay.MarkLoading(T("글꼴 준비", "Preparing font"));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var gc = new GUIContent(WarmChars.Text + WarmText);
                int n = 0;
                foreach (var f in owner.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(GUIStyle)) continue;
                    var s = f.GetValue(owner) as GUIStyle;
                    if (s == null) continue;
                    var ws = new GUIStyle(s) { wordWrap = false, richText = false };
                    var size = ws.CalcSize(gc);
                    GUI.Label(new Rect(0, 0, size.x, size.y), gc, ws);   // 투명하게 한 번 그린다
                    n++;
                }
                PerfOverlay.MarkLoading(T("글꼴 준비", "Preparing font"));
                if (Edition.Dev) Main.Entry.Logger.Log("[모니터] 글자 미리 만들기 (" + owner.GetType().Name + "): 배율 " + scale.ToString("F2") + ", 스타일 " + n + "개, " + sw.ElapsedMilliseconds + "ms");
            }
            catch (Exception ex) { if (Edition.Dev) Main.Entry.Logger.Log("[모니터] 글자 미리 만들기 실패: " + ex.Message); }
            finally { GUI.matrix = oldM; GUI.color = oldC; }
            return true;
        }

        private void Build()
        {
            built = true;
            PerfOverlay.MarkLoading(T("모드 창 준비", "Preparing mod window"));   // 둥근 카드 그림을 처음 만드는 프레임
            font = UiFont();
            if (font == null) font = GUI.skin.font;

            tWhite = Texture2D.whiteTexture;
            tMark = Mark(48);
            icons = MakeIcons();

            sWindow = Styled(Card(Hex(0x101012, 0.95f), Hex(0xFFFFFF, 0.10f), 12, 1, 0, 0f), 13);
            sWindowGlass = Styled(Card(Hex(0x14151A, 0.56f), Hex(0xFFFFFF, 0.14f), 12, 1, 0, 0f), 13);   // 유리: 뒤의 흐린 화면이 비친다
            sWindow.padding = new RectOffset(0, 0, 0, 0);
            sShadow = Styled(Shadow(48, 34), 48);

            // 흰 카드 + 옅은 테두리 + 아래로 살짝 떨어지는 그림자 (그림자는 overflow 로 바깥에)
            const int pad = 8;
            // 떠 있는 면: 윗변에 빛이 맺히고(밝은 테두리 + 안쪽 1px), 아랫변은 어둡고, 아래로 부드러운 그림자
            var raised = Panel3D(CardC, Hex(0x2E2E31), Hex(0x1A1A1C), Hex(0xFFFFFF, 0.06f), 14, pad, 0.65f, 3);
            sCard = new GUIStyle();
            sGroup = new GUIStyle();
            sRow = new GUIStyle { padding = new RectOffset(14, 14, 11, 11), margin = new RectOffset(0, 0, 0, 0) };
            sCard.padding = new RectOffset(12, 12, 8, 12); sCard.margin = new RectOffset();
            sCardDark = Styled(Panel3D(Surface2, Hex(0x38383B), Hex(0x1C1C1E), Hex(0xFFFFFF, 0.08f), 14, pad, 0.7f, 4), 14 + pad);
            sCardDark = new GUIStyle(); sCardDark.padding = new RectOffset(12, 12, 8, 12);

            sTitle = Label(16, Ink, FontStyle.Bold);
            sCrumb = Label(14, Text3, FontStyle.Normal); sCrumb.alignment = TextAnchor.MiddleLeft;
            sPageTitle = Label(15, Ink, FontStyle.Bold); sPageTitle.alignment = TextAnchor.MiddleLeft;
            sRowTitle = Label(15, Ink, FontStyle.Normal);
            sKey = Label(12, Text2, FontStyle.Normal); sKey.alignment = TextAnchor.MiddleCenter;
            sSectionLabel = Label(13, Text3, FontStyle.Bold); sSectionLabel.padding = new RectOffset(12, 0, 0, 0);
            sDetail = Label(13, Text2, FontStyle.Normal); sDetail.wordWrap = true;
            sTip = Label(12, Color.white, FontStyle.Bold); sTip.alignment = TextAnchor.MiddleCenter;   // 아이콘 이름표
            sTipLeft = Label(12, Hex(0xFFFFFF, 0.85f), FontStyle.Normal); sTipLeft.alignment = TextAnchor.MiddleLeft;
            sSub = Label(12, Text3, FontStyle.Normal);
            sH1 = Label(19, Ink, FontStyle.Bold); sH1.padding = new RectOffset(12, 12, 1, 1);
            sH2 = Label(15, Ink, FontStyle.Bold);
            sRail = Label(14, Text2, FontStyle.Normal); sRail.alignment = TextAnchor.MiddleLeft;
            sRailOn = Label(14, Ink, FontStyle.Bold); sRailOn.alignment = TextAnchor.MiddleLeft;
            sLead = Label(13, Text2, FontStyle.Normal); sLead.wordWrap = true; sLead.padding = new RectOffset(12, 12, 1, 1);   // 목록 줄과 같은 왼쪽 여백
            sBody = Label(15, Ink, FontStyle.Normal); sBody.wordWrap = true;
            sBodyText = Label(14, Ink, FontStyle.Normal); sBodyText.wordWrap = true;
            sDim = Label(13, Text2, FontStyle.Normal); sDim.wordWrap = true;
            sDimMid = Label(14, Text2, FontStyle.Normal); sDimMid.alignment = TextAnchor.MiddleLeft;
            sSmall = Label(12, Text3, FontStyle.Normal);
            sTag = Label(12, Text3, FontStyle.Normal); sTag.padding = new RectOffset(0, 0, 3, 0);
            sStat = Label(24, Ink, FontStyle.Bold);
            sStatLabel = Label(12, Text2, FontStyle.Normal);
            sStatDark = Label(24, Ink, FontStyle.Bold); sStatDark.wordWrap = true;
            sStatLabelDark = Label(12, Text2, FontStyle.Normal); sStatLabelDark.wordWrap = true;
            sHero = Label(40, Ink, FontStyle.Bold);
            sTileValue = Label(22, Ink, FontStyle.Bold);
            sMono = Label(14, Text2, FontStyle.Bold);
            sMonoAccent = Label(14, Ink, FontStyle.Bold);
            sRight = Label(13, Text3, FontStyle.Normal); sRight.alignment = TextAnchor.UpperRight;
            sTile = new GUIStyle(); sTile.padding = new RectOffset(12, 12, 8, 8);
            sRailSel = Styled(Panel3D(Surface2, Hex(0x38383B), Hex(0x19191B), Hex(0xFFFFFF, 0.07f), 10, 6, 0.6f, 2), 16); sRailSel.overflow = new RectOffset(6, 6, 6, 6);
            sWell = Styled(Well3D(Hex(0x080809), 10), 12);
            sWellThin = Styled(Well3D(Hex(0x080809), 4), 5);
            sPillWell = Styled(Well3D(Hex(0x070708), 12), 13);
            sPillOn = Styled(Panel3D(Hex(0xD9D9DC), Hex(0xE4E4E7), Hex(0xCACACE), Hex(0xFFFFFF, 0.3f), 12, 0, 0f, 0), 13);
            sDock = Styled(Panel3D(Hex(0x0E0E0F, 0.92f), Hex(0xFFFFFF, 0.12f), Hex(0xFFFFFF, 0.03f), Hex(0xFFFFFF, 0.06f), 16, 0, 0f, 0), 17);
            tKnobLight = Knob(Hex(0xFFFFFF), Hex(0xD6D6DA), 0.55f);
            tGlow = Glow(64);
            tKnobGray = Knob(Hex(0x9A9AA0), Hex(0x6A6A70), 0.6f);
            tKnobDark = Knob(Hex(0x1C1C1E), Hex(0x141416), 0.35f);
            sRightMid = Label(13, Text2, FontStyle.Normal); sRightMid.alignment = TextAnchor.MiddleRight;
            sSmallRight = Label(11, Text3, FontStyle.Normal); sSmallRight.alignment = TextAnchor.UpperRight;
            sValueRight = Label(13, Ink, FontStyle.Bold); sValueRight.alignment = TextAnchor.MiddleRight;

            // 메뉴: 선택된 것만 흰 카드로 떠 있다
            sNav = Styled(null, 10);
            sNav.normal.textColor = Text2; sNav.fontSize = 14; sNav.alignment = TextAnchor.MiddleLeft; sNav.padding = new RectOffset(16, 8, 0, 0);
            sNav.hover.textColor = Ink;
            sNavOn = Styled(Card(CardC, Edge, 10, 1, 6, 0.06f), 16);
            sNavOn.overflow = new RectOffset(6, 6, 6, 6);
            sNavOn.normal.textColor = Ink; sNavOn.hover.textColor = Ink; sNavOn.fontSize = 14; sNavOn.fontStyle = FontStyle.Bold;
            sNavOn.alignment = TextAnchor.MiddleLeft; sNavOn.padding = new RectOffset(16, 8, 0, 0);
            sNavOn.hover.background = sNavOn.normal.background;
            sNavText = new GUIStyle(sNav) { fontStyle = FontStyle.Bold };   // 선택 카드 위의 글자 (배경은 따로 미끄러지며 그린다)
            sNavText.normal.textColor = Ink; sNavText.hover.textColor = Ink;

            // 버튼: 평평하게, 모서리 6. 주된 것은 밝은 면에 어두운 글자, 두 번째는 아주 옅은 면.
            sPrimary = Styled(Card(Ink, Ink, 6, 0, 0, 0f), 7);
            sPrimary.normal.textColor = OnAccent; sPrimary.alignment = TextAnchor.MiddleCenter; sPrimary.fontSize = 13; sPrimary.fontStyle = FontStyle.Bold;
            sPrimary.hover.background = Card(Hex(0xFFFFFF), Hex(0xFFFFFF), 6, 0, 0, 0f); sPrimary.hover.textColor = OnAccent;
            sPrimary.active.background = Card(Hex(0xC9CACE), Hex(0xC9CACE), 6, 0, 0, 0f); sPrimary.active.textColor = OnAccent;
            sSecondary = Styled(Card(Surface2, Surface2, 6, 0, 0, 0f), 7);
            sSecondary.normal.textColor = Ink; sSecondary.alignment = TextAnchor.MiddleCenter; sSecondary.fontSize = 13; sSecondary.fontStyle = FontStyle.Bold;
            sSecondary.hover.background = Card(Hex(0xFFFFFF, 0.12f), Hex(0xFFFFFF, 0.12f), 6, 0, 0, 0f); sSecondary.hover.textColor = Ink;
            sSecondary.active.background = Card(Hex(0xFFFFFF, 0.05f), Hex(0xFFFFFF, 0.05f), 6, 0, 0, 0f); sSecondary.active.textColor = Ink;

            sTab = Styled(null, 4);
            sTab.normal.textColor = Text3; sTab.hover.textColor = Text2; sTab.alignment = TextAnchor.MiddleCenter; sTab.fontSize = 14;
            sTabOn = new GUIStyle(sTab) { fontStyle = FontStyle.Bold };
            sTabOn.normal.textColor = Ink; sTabOn.hover.textColor = Ink;
            sTabHover = new GUIStyle(sTab); sTabHover.normal.textColor = Text2;

            sClose = Styled(null, 10);
            sClose.normal.textColor = Text3; sClose.alignment = TextAnchor.MiddleCenter; sClose.fontSize = 20; sClose.padding = new RectOffset(0, 0, 0, 3);
            sClose.hover.background = Card(Soft, Soft, 6, 0, 0, 0f); sClose.hover.textColor = Ink;


            // 모니터 페이지의 조절 도구
            sSegKnob = Styled(Card(Hex(0xFFFFFF, 0.10f), Hex(0xFFFFFF, 0.10f), 5, 0, 0, 0f), 6);

            sSegText = Label(13, Text2, FontStyle.Normal); sSegText.alignment = TextAnchor.MiddleCenter;
            sSegOnText = Label(13, Ink, FontStyle.Bold); sSegOnText.alignment = TextAnchor.MiddleCenter;
            sSliderValue = Label(13, Ink, FontStyle.Bold); sSliderValue.alignment = TextAnchor.MiddleRight;
            sChip = Styled(Card(Soft, Soft, 6, 0, 0, 0f), 7);
            sChip.normal.textColor = Text2; sChip.fontSize = 13; sChip.alignment = TextAnchor.MiddleCenter;
            sChip.padding = new RectOffset(12, 12, 0, 0);
            sChip.hover.background = Card(Surface2, Surface2, 6, 0, 0, 0f); sChip.hover.textColor = Ink;
            // 고른 항목 칩: 주황으로 채우면 한 줄에 여러 개가 너무 시끄러워, 한 단계 밝은 회색 + 어두운 주황 테두리
            sChipOn = Styled(Card(Ink, Ink, 6, 0, 0, 0f), 7);   // 고른 칩: 밝은 면
            sChipOn.normal.textColor = OnAccent; sChipOn.fontSize = 13; sChipOn.fontStyle = FontStyle.Bold; sChipOn.alignment = TextAnchor.MiddleCenter;
            sChipOn.padding = new RectOffset(12, 12, 0, 0);
            sChipOn.hover.background = Card(Hex(0xFFFFFF), Hex(0xFFFFFF), 6, 0, 0, 0f); sChipOn.hover.textColor = OnAccent;

            sScroll = new GUIStyle { fixedWidth = 4, margin = new RectOffset(14, 0, 0, 0), border = new RectOffset(2, 2, 2, 2) };
            sThumb = new GUIStyle { fixedWidth = 4, border = new RectOffset(2, 2, 2, 2) };
            sThumb.normal.background = Card(Hex(0x3A3A3D), Hex(0x3A3A3D), 2, 0, 0, 0f);
        }

        private GUIStyle Label(int size, Color color, FontStyle style)
        {
            var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = style, richText = true, wordWrap = false };
            s.normal.textColor = color;
            s.hover.textColor = color;
            s.padding = new RectOffset(0, 0, 1, 1);
            s.margin = new RectOffset(0, 0, 0, 0);
            return s;
        }

        private GUIStyle Styled(Texture2D bg, int border)
        {
            var s = new GUIStyle { font = font, richText = true };
            s.normal.background = bg;
            s.border = new RectOffset(border, border, border, border);
            s.margin = new RectOffset(0, 0, 0, 0);
            return s;
        }

        // ── 텍스처 ─────────────────────────────────────────────────────
        internal static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        }

        // 한 변이 size 인 정사각형 안의 반지름 r 둥근 사각형까지의 거리 (안쪽이 음수)
        internal static float RoundDist(float x, float y, float size, float r)
        {
            float h = size / 2f;
            float qx = Mathf.Abs(x - h) - (h - r), qy = Mathf.Abs(y - h) - (h - r);
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        // 둥근 카드: 채움 + 테두리(두께 bw) + 바깥 pad 만큼 아래로 살짝 떨어진 옅은 그림자(진하기 shadowA).
        // 9칸 나누기로 늘여 쓰고, 그림자 부분은 GUIStyle.overflow 로 카드 바깥에 그린다.
        internal static Texture2D Card(Color fill, Color border, int r, int bw, int pad, float shadowA)
        {
            int inner = r * 2 + 4, size = inner + pad * 2;
            var t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - pad, fy = y + 0.5f - pad;
                    float d = RoundDist(fx, fy, inner, r);

                    // 그림자: 아래로 2px 내려 부드럽게 퍼진다 (텍스처 y 는 아래가 0)
                    Color bg = Color.clear;
                    if (pad > 0 && shadowA > 0)
                    {
                        float ds = RoundDist(fx, fy + 2f, inner, r);
                        float s = Mathf.Clamp01(1f - Mathf.Max(0, ds) / pad);
                        bg = new Color(0.1f, 0.1f, 0.15f, s * s * shadowA);
                    }

                    float cover = Mathf.Clamp01(0.5f - d);
                    Color c = fill;
                    if (bw > 0) c = Color.Lerp(border, fill, Mathf.Clamp01(-d - bw + 0.5f));
                    float a = c.a * cover;
                    // 카드를 그림자 위에 겹친다
                    float outA = a + bg.a * (1 - a);
                    Color rgb = outA > 0 ? (c * a + bg * bg.a * (1 - a)) / outA : Color.clear;
                    px[y * size + x] = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        // ── 빛을 받은 면 (위에서 빛이 든다): 채움 + 위/아래 테두리 색 + 윗변 안쪽 1px 빛 + 아래로 offY 만큼 내려간 부드러운 그림자 ──
        internal static Texture2D Panel3D(Color fill, Color borderTop, Color borderBottom, Color highlight, int r, int pad, float shadowA, int offY)
        {
            // 어두운 면은 위쪽 띠가 아주 옅게 밝다(빛이 위에서 든다). 9칸 늘리기에서 위 띠는 늘어나지 않고 위 가장자리에만 남는다.
            float luma = fill.r * 0.3f + fill.g * 0.59f + fill.b * 0.11f;
            float sheen = 0f;                                   // (평평하게) 위 띠 밝음은 쓰지 않는다
            highlight.a *= 0.35f; shadowA *= 0.45f;            // 윗변 빛과 그림자도 아주 옅게
            int inner = r * 2 + 8, size = inner + pad * 2;
            var t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - pad, fy = y + 0.5f - pad;   // fy: 아래가 0
                    float d = RoundDist(fx, fy, inner, r);
                    float v = Mathf.Clamp01(fy / inner);          // 0 아래 -> 1 위
                    Color bg = Color.clear;
                    if (pad > 0 && shadowA > 0)
                    {
                        float ds = RoundDist(fx, fy + offY, inner, r);
                        float sh = Mathf.Clamp01(1f - Mathf.Max(0, ds) / pad);
                        bg = new Color(0f, 0f, 0f, sh * sh * sh * shadowA);
                    }
                    float cover = Mathf.Clamp01(0.5f - d);
                    Color border = Color.Lerp(borderBottom, borderTop, Mathf.SmoothStep(0f, 1f, v));
                    Color c = Color.Lerp(border, fill, Mathf.Clamp01(-d - 1f + 0.5f));
                    if (sheen > 0f && d < -1f) { float k2 = Mathf.Clamp01((v - 0.55f) / 0.45f); c = Color.Lerp(c, new Color(1f, 1f, 1f, c.a), sheen * k2 * k2); }
                    // 윗변 안쪽에 맺힌 빛: 위쪽 둥근 가장자리를 따라 1px
                    if (v > 0.5f && d < -1f && d > -2.2f) { float w = highlight.a * Mathf.Clamp01((v - 0.5f) * 2f); c = Color.Lerp(c, new Color(highlight.r, highlight.g, highlight.b, c.a), w); }
                    float a = c.a * cover;
                    float outA = a + bg.a * (1 - a);
                    Color rgb = outA > 0 ? (c * a + bg * bg.a * (1 - a)) / outA : Color.clear;
                    px[y * size + x] = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        // 은은한 빛 번짐: 가운데가 밝고 바깥으로 부드럽게 사라지는 흰 원
        private static Texture2D Glow(int n)
        {
            var t = NewTex(n, n); var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color(1f, 1f, 1f, d * d * (3f - 2f * d) * d);   // 가운데는 진하고 끝은 부드럽게 사라짐
                }
            t.SetPixels(px); t.Apply(false, false);
            return t;
        }

        // ── 파인 홈: 안쪽 윗부분에 그늘, 아랫입술에 아주 옅은 빛 ──
        internal static Texture2D Well3D(Color fill, int r)
        {
            int inner = r * 2 + 8, size = inner;
            var t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = RoundDist(fx, fy, inner, r);
                    float cover = Mathf.Clamp01(0.5f - d);
                    float fromTop = inner - fy, fromBottom = fy;
                    Color c = fill;
                    float shade = Mathf.Clamp01(1f - fromTop / 4f) * 0.18f;            // 위쪽 안에 드리운 옅은 그늘
                    c = Color.Lerp(c, Color.black, shade);
                    if (d > -1.2f) c = Color.Lerp(c, Hex(0x2A2A2D), 0.6f);              // 가장자리 가는 선
                    if (fromBottom < 1.6f && d < -0.2f) c = Color.Lerp(c, Color.white, 0.02f);   // 아랫입술 빛
                    px[y * size + x] = new Color(c.r, c.g, c.b, fill.a * cover);
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        // ── 구슬 손잡이: 위가 밝고 아래가 어두운 공 + 왼쪽 위 반짝임 + 아래 그림자 (지름은 텍스처의 1/2) ──
        internal static Texture2D Knob(Color top, Color bottom, float shadowA)
        {
            const int N = 72; float R = N / 4f, cx = N / 2f, cy = N / 2f;
            var t = NewTex(N, N);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float dx = fx - cx, dy = fy - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 그림자: 아래로 3px, 퍼짐 R*0.9
                    float ds = Mathf.Sqrt(dx * dx + (dy + 3f) * (dy + 3f)) - R;
                    float sh = Mathf.Clamp01(1f - Mathf.Max(0f, ds) / (R * 0.9f));
                    var bg = new Color(0, 0, 0, sh * sh * sh * shadowA * 0.5f);
                    float cover = Mathf.Clamp01(R + 0.5f - dist);
                    float v = Mathf.Clamp01((dy / R + 1f) / 2f);                // 0 아래 -> 1 위
                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    float hx = dx + R * 0.35f, hy = dy - R * 0.4f;                  // 왼쪽 위 반짝임
                    float spec = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / (R * 0.6f));
                    c = Color.Lerp(bottom, top, 0.7f);                              // (평평하게) 한 색
                    if (dist > R - 1.2f) c = Color.Lerp(c, Color.black, 0.15f);    // 테두리
                    float a = cover;
                    float outA = a + bg.a * (1 - a);
                    Color rgb = outA > 0 ? (c * a + bg * bg.a * (1 - a)) / outA : Color.clear;
                    px[y * N + x] = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        // 제목 옆 표시: 검정 둥근 사각형 안의 흰 막대 세 개 (프레임 그래프)
        private static Texture2D Mark(int s)
        {
            var t = NewTex(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = RoundDist(x + 0.5f, y + 0.5f, s, s * 0.26f);
                    float cover = Mathf.Clamp01(0.5f - d);
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                    float bar = Mathf.Max(Bar(u, v, 0.27f, 0.37f, 0.25f, 0.52f, s),
                                Mathf.Max(Bar(u, v, 0.45f, 0.55f, 0.25f, 0.75f, s), Bar(u, v, 0.63f, 0.73f, 0.25f, 0.63f, s)));
                    Color c = Color.Lerp(Accent, OnAccent, bar);   // 밝은 바탕 + 어두운 막대
                    c.a = cover;
                    t.SetPixel(x, y, c);
                }
            t.Apply(false, false);
            return t;
        }

        private static float Bar(float u, float v, float x0, float x1, float bottom, float top, int s)
        {
            float r = (x1 - x0) / 2f, cx = (x0 + x1) / 2f;
            float cy = Mathf.Clamp(v, bottom + r, top - r);
            float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r;
            return Mathf.Clamp01(0.5f - d * s);
        }

        // 창 뒤의 넓고 옅은 그림자
        internal static Texture2D Shadow(int half, int blur)
        {
            int n = half * 2;
            var t = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = RoundDist(x + 0.5f, y + 0.5f, n, blur + 8) + blur;
                    float a = Mathf.Clamp01(1f - d / blur);
                    t.SetPixel(x, y, new Color(0, 0, 0, a * a * 0.38f));
                }
            t.Apply(false, false);
            return t;
        }
    }
}
