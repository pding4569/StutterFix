using UnityEngine;

namespace StutterFix
{
    // 프레임 늘리기 고정 배율(찢어짐 출력)에서 진짜 프레임을 고르게: 게임에 FPS 제한이 없으면 곡 중에만 그 PC가 지킬 수 있는
    // 진짜 FPS 로 묶는다. 제한이 없으면 진짜 Present 간격이 2~18ms 로 흔들려(HELLO 2026, 129 FPS) 사이 화면의 그림 시각이
    // 저장된 진짜 프레임 밖으로 나가 멈추거나 넘쳤다(출력의 14.6%). 60 FPS 로 묶인 판은 0% 였다.
    // 처음 1초를 재서 중앙값의 85% 로 시작하고, 못 지키는 프레임이 10% 를 넘으면 내리고 잘 지키면 5% 씩 올려 본다.
    // 게임이 정한 제한이 있으면(목표 FPS 1000 미만 또는 수직동기) 건드리지 않는다. 곡이 끝나면 원래 값으로.
    internal static class SteadyCap
    {
        private static bool capped;
        private static int savedFps, cap;
        private static readonly float[] ring = new float[240];
        private static int n, idx;
        private static float judgeAt;
        internal static string State => capped ? "cap=" + cap : "off";

        internal static void Tick()
        {
            try
            {
                var c = Main.Config;
                bool want = c != null && c.FrameGenSteady && Hitch.Playing && !c.FrameGenRefresh && c.FrameGenOutside >= 2 && c.FrameGenOutside <= 8
                            && !HalfRender.Enabled && QualitySettings.vSyncCount == 0;
                if (!want) { Release(); return; }
                if (!capped)
                {
                    int cur = Application.targetFrameRate;
                    if (cur > 0 && cur < 1000) return;   // 게임(사용자)이 이미 제한을 걸었다
                    savedFps = cur; capped = true; cap = 0; n = idx = 0; judgeAt = Time.realtimeSinceStartup + 1f;
                    return;
                }
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f && dt < 0.25f) { ring[idx] = dt; idx = (idx + 1) % ring.Length; if (n < ring.Length) n++; }
                float now = Time.realtimeSinceStartup;
                if (now < judgeAt || n < 30) return;
                judgeAt = now + 2f;
                var tmp = new float[n]; System.Array.Copy(ring, tmp, n); System.Array.Sort(tmp);
                float median = tmp[n / 2];
                int next = cap;
                if (cap == 0) next = Round5(0.85f / median);
                else
                {
                    float limit = 1f / cap * 1.15f; int slow = 0;
                    for (int i = 0; i < n; i++) if (tmp[i] > limit) slow++;
                    if (slow > n / 10) next = Round5(0.9f / median);
                    else if (slow < n / 50) next = Round5(cap * 1.05f + 1f);
                }
                next = Mathf.Clamp(next, 30, 1000);
                if (next != cap)
                {
                    cap = next; Application.targetFrameRate = cap;
                    Main.Entry.Logger.Log("[프레임 늘리기] 진짜 FPS 고르게: 곡 중 제한 " + cap + " (중앙값 " + (1f / median).ToString("F0") + " FPS)");
                }
                n = idx = 0;
            }
            catch { }
        }
        private static int Round5(float fps) => Mathf.Max(30, Mathf.FloorToInt(fps / 5f) * 5);
        internal static void Release()
        {
            if (!capped) return;
            capped = false;
            try { if (Application.targetFrameRate == cap) Application.targetFrameRate = savedFps; } catch { }
        }
    }
}
