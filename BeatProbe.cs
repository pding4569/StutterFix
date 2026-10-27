#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // (자동 시험 beatprobe) 빛난 타일에 박자 알림(OnBeat)을 한 번 불러 보고, 불러서 바뀐 것이 있는지 센다.
    // 빛난 타일의 OnBeat 가 아무것도 안 바꾸면, 박자 때마다 그 타일을 부르지 않아도 화면이 같다는 근거가 된다.
    // 일시정지한 상태에서 부른다(불러서 바뀐 값은 원래 박자 때 바뀔 값과 같다).
    internal static class BeatProbe
    {
        private static readonly HarmonyLib.AccessTools.FieldRef<scrFloor, int> rnRef = HarmonyLib.AccessTools.FieldRefAccess<scrFloor, int>("randomNumberForLitAndGlow");
        internal static string Run()
        {
            var lm = ADOBase.lm;
            if (lm == null || lm.listFloors == null) return "타일 목록 없음";
            var lf = lm.listFloors;
            var lit = new List<scrFloor>();
            foreach (var f in lf) if (f != null && f.hasLit && f.GetType() == typeof(scrFloor)) lit.Add(f);
            int n = lit.Count;
            var tg = new Color[n]; var bg = new Color[n]; var fc = new Color[n];
            var ts = new Sprite[n]; var bs = new Sprite[n]; var fs = new Sprite[n]; var rn = new int[n];
            int topA = 0, botNull = 0, noGlow = 0, dont = 0;
            for (int i = 0; i < n; i++)
            {
                var f = lit[i];
                if (f.topGlow) { tg[i] = f.topGlow.color; ts[i] = f.topGlow.sprite; if (tg[i].a > 0f) topA++; } else noGlow++;
                if (f.bottomGlow) { bg[i] = f.bottomGlow.color; bs[i] = f.bottomGlow.sprite; } else botNull++;
                fc[i] = f.floorRenderer.color; fs[i] = f.floorRenderer.sprite; rn[i] = rnRef(f);
                if (f.dontChangeMySprite) dont++;
            }
            for (int i = 0; i < n; i++) lit[i].OnBeat();
            int chTop = 0, chBot = 0, chFloor = 0, chSpr = 0, chRn = 0, chAny = 0;
            for (int i = 0; i < n; i++)
            {
                var f = lit[i]; bool any = false;
                if (f.topGlow && (f.topGlow.color != tg[i])) { chTop++; any = true; }
                if (f.bottomGlow && (f.bottomGlow.color != bg[i])) { chBot++; any = true; }
                if (f.floorRenderer.color != fc[i]) { chFloor++; any = true; }
                if ((f.topGlow && f.topGlow.sprite != ts[i]) || (f.bottomGlow && f.bottomGlow.sprite != bs[i]) || f.floorRenderer.sprite != fs[i]) { chSpr++; any = true; }
                if (rnRef(f) != rn[i]) { chRn++; any = true; }
                if (any) chAny++;
            }
            var v = scrVfx.instance;
            string style = v == null ? "vfx 없음" : "tileFlashStyle=" + v.tileFlashStyle + " overrideStyle=" + v.overrideStyle + " overrideTileSprites=" + v.overrideTileSprites + " overrideGlowSprites=" + v.overrideGlowSprites;
            return "빛난 " + n + "개 / 바뀐 것 " + chAny + "개 (topGlow " + chTop + ", bottomGlow " + chBot + ", 타일색 " + chFloor + ", 스프라이트 " + chSpr + ", 난수번호 " + chRn + ") | 글로우 알파>0 " + topA + "개, 글로우 없음 " + noGlow + ", bottomGlow 없음 " + botNull + ", dontChangeMySprite " + dont + " | " + style;
        }
    }
}
#endif
