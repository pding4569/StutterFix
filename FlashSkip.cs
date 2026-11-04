#if DEV || AUTOTEST
using UnityEngine;

namespace StutterFix
{
    // 시험 전용: 카메라에 붙은 전체 화면 플래시 판은 투명할 때도 렌더러가 켜져 있어 매 프레임 화면 전체를 그린다.
    // 투명(재질 색 알파 0)이면 이번 프레임만 끈다. Sprites/Default 는 미리 곱한 알파라 알파 0 이면 화면을 바꾸지 않는다.
    // 효과가 색을 바꾸는 Update 뒤에 판단하려고 첫 카메라 onPreCull 에서 본다.
    internal static class FlashSkip
    {
        private static Renderer[] planes;
        private static int lastFrame = -1;
        internal static long SkippedPlanes, Frames;

        internal static void Enable()
        {
            var cam = scrCamera.instance.transform;
            string[] names = { "Flash", "FlashPlusBackground", "FlashPlusForeground", "FlashEndscreen" };
            planes = new Renderer[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var t = cam.Find(names[i]);
                planes[i] = t != null ? t.GetComponent<Renderer>() : null;
                Main.Entry.Logger.Log("[flashskip] " + names[i] + " " + (planes[i] != null ? "enabled=" + planes[i].enabled + " active=" + planes[i].gameObject.activeInHierarchy + " 색=" + planes[i].sharedMaterial.color : "없음"));
            }
            Camera.onPreCull += PreCull;
        }

        internal static void Report() { Main.Entry.Logger.Log("[flashskip] 프레임 " + Frames + " 끈 판 합계 " + SkippedPlanes); }

        private static void PreCull(Camera c)
        {
            int frame = Time.frameCount;
            if (frame == lastFrame) return;
            lastFrame = frame; Frames++;
            for (int i = 0; i < planes.Length; i++)
            {
                var r = planes[i];
                if (r == null) continue;
                bool visible = r.sharedMaterial.color.a > 0f;
                if (r.enabled != visible) r.enabled = visible;
                if (!visible) SkippedPlanes++;
            }
        }
    }
}
#endif
