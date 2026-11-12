using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // 번쩍임 사각형 건너뛰기: 게임은 번쩍임(Flash, FlashPlusBackground)을 전체 화면 사각형(월드 31x22) 두 장으로 그리고,
    // 효과가 없을 때도 재질 알파 0 으로 매 프레임 칠한다(Arche 3440x1440: 약 700만 픽셀 호출). 재질 알파가 0 이하면 그리기 결과가
    // 원래와 같으므로(Sprites/Default 는 rgb 에 알파를 곱해 미리 곱한 섞기로 낸다 -> 항상 (0,0,0,0)) 그 프레임만 안 그린다.
    // 알파가 0 보다 크면 아무것도 건드리지 않는다. 번쩍임이 끝난 뒤 알파가 남는 색(endColor)도 그대로 그린다.
    // 측정(2026-11-12, Arche, 2560x1417): 레이어 10 카메라를 통째로 끄면 GPU 1.48 -> 1.37ms, 메인 2.92 -> 2.84ms (사각형 + 카메라 준비 비용).
    internal static class FlashSkip
    {
        internal static bool Enabled;
        internal static long Skipped, Drawn;
        private static bool hooked;
        private static int lastFrame = -1;
        private static Renderer[] rend = new Renderer[0];
        private static Material[] mat = new Material[0];
        private static bool[] off = new bool[0];
        private static int n;
        private static object lastCam;
        private static int findFrame = -9999;

        internal static void SetEnabled(bool on)
        {
            Enabled = on;
            if (on && !hooked) { Camera.onPreCull += PreCull; hooked = true; }
            if (!on) Restore();
        }
        internal static void Shutdown() { if (hooked) { Camera.onPreCull -= PreCull; hooked = false; } Enabled = false; Restore(); }
        internal static string State() { return "enabled=" + Enabled + " quads=" + n + " skipped=" + Skipped + " drawn=" + Drawn; }

        private static void Restore()
        {
            for (int i = 0; i < n; i++) if (off[i] && rend[i]) rend[i].forceRenderingOff = false;
            for (int i = 0; i < n; i++) off[i] = false;
        }

        // 번쩍임 렌더러 찾기: 카메라가 가진 flashPlus 두 장과 옛 번쩍임(scrFlash). 장면이 바뀌면 다시 찾는다.
        private static void Find()
        {
            Restore();
            var list = new List<Renderer>();
            var cam = scrCamera.instance;
            if (cam)
            {
                if (cam.flashPlusRendererBg) list.Add(cam.flashPlusRendererBg);
                if (cam.flashPlusRendererFg) list.Add(cam.flashPlusRendererFg);
            }
            foreach (var f in Object.FindObjectsByType<scrFlash>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var r = f ? f.GetComponent<Renderer>() : null;
                if (r && !list.Contains(r)) list.Add(r);
            }
            n = list.Count; rend = list.ToArray(); mat = new Material[n]; off = new bool[n];
            findFrame = Time.frameCount;
        }

        private static void PreCull(Camera c)
        {
            if (!Enabled) return;
            int fc = Time.frameCount;
            if (fc == lastFrame) return;          // 프레임마다 한 번(처음 그리는 카메라에서)
            lastFrame = fc;
            if (n == 0 || (rend.Length > 0 && !rend[0] && !(n > 1 && rend[1]))) { if (fc - findFrame > 60) Find(); if (n == 0) return; }
            for (int i = 0; i < n; i++)
            {
                var r = rend[i];
                if (!r) { if (fc - findFrame > 60) Find(); return; }
                // 재질은 게임이 .material 로 만든 사본(scrFlash.Start, 효과 FlashSetup): sharedMaterial 이 그 사본이다
                var m = r.sharedMaterial; mat[i] = m;
                bool clear = m && m.HasProperty("_Color") && m.color.a <= 0f;
                if (clear != off[i]) { r.forceRenderingOff = clear; off[i] = clear; }
                if (clear) Skipped++; else Drawn++;
            }
        }
    }
}
