using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StutterFix
{
    // 에디터 재생 단추 옆 "게임 화면으로 플레이": 커스텀 맵 목록에서 연 것처럼 게임 화면(scnGame)으로 연다.
    // 에디터 재생보다 조금 가볍다(에디터 화면·도구가 없다). 노이펙 모드면 장식을 아예 만들지 않아 열기도 빠르다.
    // 에디터로 돌아오기는 게임의 일시정지 메뉴 "에디터에서 열기"(커스텀 맵 게임 화면에 원래 있음)로 한다.
    // 단추는 에디터의 되감기 단추를 복제해 재생 단추 위 오른쪽(되감기와 대칭)에 둔다.
    internal static class GameScreenButton
    {
        internal static bool Enabled = true;
        private static GameObject btn;
        private static scnEditor owner;
        private static Sprite icon;
        private static bool shown;

        // 모드 OnUpdate 에서 매 프레임 (에디터가 없으면 바로 돌아간다)
        private static bool broken;
        internal static void Tick()
        {
            if (broken) return;
            try { TickImpl(); }
            catch (Exception ex) { broken = true; Remove(); Main.Entry.Logger.Log("[게임 화면] 단추 끔 (게임 코드가 달라짐): " + ex.Message); }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void TickImpl()
        {
            var ed = scnEditor.instance;
            if (!Enabled || ed == null) { if (btn != null && !Enabled) Remove(); return; }
            if (btn == null || !ReferenceEquals(owner, ed)) { Remove(); Create(ed); if (btn == null) return; }
            bool want = !ed.playMode && !string.IsNullOrEmpty(ADOBase.levelPath);
            if (want != shown) { btn.SetActive(want); shown = want; }
        }

        private static void Create(scnEditor ed)
        {
            try
            {
                var rewind = ed.playPause != null ? ed.playPause.transform.parent.Find("rewind") : null;
                if (rewind == null) return;
                btn = UnityEngine.Object.Instantiate(rewind.gameObject, rewind.parent);
                btn.name = "StutterFix.gameScreen";
                owner = ed; shown = true;
                var rt = (RectTransform)btn.transform;
                var src = (RectTransform)rewind;
                var play = (RectTransform)ed.playPause.transform;
                // 되감기는 재생 단추 중심에서 왼쪽 위. 같은 거리만큼 오른쪽 위에.
                rt.anchoredPosition = new Vector2(2f * play.anchoredPosition.x - src.anchoredPosition.x, src.anchoredPosition.y);
                var b = btn.GetComponent<Button>();
                b.onClick = new Button.ButtonClickedEvent();   // 복제한 되감기 동작은 버린다
                b.onClick.AddListener(Click);
                var ic = btn.transform.Find("icon");
                if (ic != null)
                {
                    var img = ic.GetComponent<Image>();
                    if (img != null) { img.sprite = Icon(); img.preserveAspect = true; }
                    ((RectTransform)ic).sizeDelta = new Vector2(50f, 50f);
                }
                var sc = btn.transform.Find("shortcut");
                if (sc != null) sc.gameObject.SetActive(false);
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[게임 화면] 단추 만들기 실패: " + ex.Message); Remove(); }
        }

        internal static void Remove()
        {
            if (btn != null) UnityEngine.Object.Destroy(btn);
            btn = null; owner = null; shown = false;
        }

        private static void Click()
        {
            var ed = scnEditor.instance;
            if (ed == null || ed.playMode) return;
            try { PlayTweaks.CallLoose(ed, "CheckUnsavedChanges", (Action)Go); }   // 저장하지 않은 변경이 있으면 게임의 "저장할까요" 창을 먼저 띄운다
            catch (Exception ex) { Main.Entry.Logger.Log("[게임 화면] 저장 확인 실패: " + ex.Message); }
        }

        // 자동 시험(gamebtn)도 이것을 부른다
        internal static void Go()
        {
            string path = ADOBase.levelPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                // 게임의 QuitToMenu 처럼 편집기 종료 확인 콜백을 뺀다 (안 빼면 옛 편집기가 정적 이벤트에 남는다)
                var ed = scnEditor.instance;
                var m = AccessTools.Method(typeof(scnEditor), "TryApplicationQuit");
                if (ed != null && m != null) Application.wantsToQuit -= (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), ed, m);
            }
            catch { }
            Main.Entry.Logger.Log("[게임 화면] 에디터에서 게임 화면으로 열기: " + Path.GetFileName(path));
            try { PlayTweaks.CallLoose(ADOBase.controller, "LoadCustomLevel", path); }
            catch (Exception ex) { Main.Entry.Logger.Log("[게임 화면] 열기 실패: " + ex.Message); }
        }

        // 화면(모니터 테두리) 안에 재생 삼각형
        private static Sprite Icon()
        {
            if (icon != null) return icon;
            const int S = 96;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float fx = (x + 0.5f) / S, fy = (y + 0.5f) / S;
                    // 테두리: 둥근 사각형 (0.03~0.97, 0.2~0.92), 두께 0.1 (되감기 아이콘처럼 굵게)
                    float a = RoundRect(fx, fy, 0.03f, 0.2f, 0.97f, 0.92f, 0.14f) - RoundRect(fx, fy, 0.13f, 0.3f, 0.87f, 0.82f, 0.05f);
                    // 받침: 아래 가로선
                    if (fy > 0.03f && fy < 0.12f && fx > 0.26f && fx < 0.74f) a = 1f;
                    // 재생 삼각형 (가운데)
                    float tx = (fx - 0.39f) / 0.27f, ty = (fy - 0.56f) / 0.18f;
                    if (tx >= 0f && tx <= 1f && Mathf.Abs(ty) <= 1f - tx) a = 1f;
                    byte v = (byte)(Mathf.Clamp01(a) * 255f);
                    px[y * S + x] = new Color32(24, 24, 24, v);   // 원래 단추 아이콘처럼 어둡게 (단추 바탕이 밝다)
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            icon = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return icon;
        }

        private static float RoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
            float dx = x - cx, dy = y - cy;
            return (x >= x0 && x <= x1 && y >= y0 && y <= y1 && dx * dx + dy * dy <= r * r) ? 1f : 0f;
        }
    }
}
