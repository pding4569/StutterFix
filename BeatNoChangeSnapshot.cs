#if DEV || AUTOTEST
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // beat-verify 전용. 정상 실행의 목록 관리에는 렌더러 속성 읽기가 없다.
    internal struct BeatNoChangeSnapshot
    {
        private static readonly AccessTools.FieldRef<scrFloor, int> randomRef =
            AccessTools.FieldRefAccess<scrFloor, int>("randomNumberForLitAndGlow");
        private SpriteRenderer top, bottom;
        private Color topColor, bottomColor, floorColor;
        private Sprite topSprite, bottomSprite, floorSprite;
        private int random;

        internal static BeatNoChangeSnapshot Read(scrFloor f)
        {
            var s = new BeatNoChangeSnapshot { top = f.topGlow, bottom = f.bottomGlow,
                floorColor = f.floorRenderer.color, floorSprite = f.floorRenderer.sprite, random = randomRef(f) };
            if (s.top) { s.topColor = s.top.color; s.topSprite = s.top.sprite; }
            if (s.bottom) { s.bottomColor = s.bottom.color; s.bottomSprite = s.bottom.sprite; }
            return s;
        }

        internal bool Same(BeatNoChangeSnapshot s) => ReferenceEquals(top, s.top) && ReferenceEquals(bottom, s.bottom)
            && topColor.Equals(s.topColor) && bottomColor.Equals(s.bottomColor) && floorColor.Equals(s.floorColor)
            && ReferenceEquals(topSprite, s.topSprite) && ReferenceEquals(bottomSprite, s.bottomSprite)
            && ReferenceEquals(floorSprite, s.floorSprite) && random == s.random;
    }
}
#endif
