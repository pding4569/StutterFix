using System;
using System.Reflection;

namespace StutterFix
{
    // 화면 효과 "더 많은 효과": 색 조정 / 질감·디테일 / 렌즈·왜곡. 표 하나로 설정 창, 초기화, 공유(FxShare)가 같은 필드를 쓴다.
    // 셰이더는 effects/ScreenEffects.shader 의 패스 13(grade) · 14(detail) · 15(lens). 모두 기본 꺼짐이고 켜는 효과만 패스를 더한다.
    internal static class FxExtras
    {
        internal sealed class Spec
        {
            internal string Group, Key, Toggle, Value, Value2;
            internal float Min, Max, Min2, Max2;
            internal string Ko, En, DescKo, DescEn, ValueKo, ValueEn, Value2Ko, Value2En;
            internal FieldInfo ToggleField, ValueField, Value2Field;
        }

        private static Spec S(string group, string key, string toggle, string ko, string en, string descKo, string descEn,
            string value = null, float min = 0, float max = 1, string valueKo = "세기", string valueEn = "Strength",
            string value2 = null, float min2 = 0, float max2 = 1, string value2Ko = null, string value2En = null)
        {
            var t = typeof(Settings);
            return new Spec
            {
                Group = group, Key = key, Toggle = toggle, Value = value, Value2 = value2, Min = min, Max = max, Min2 = min2, Max2 = max2,
                Ko = ko, En = en, DescKo = descKo, DescEn = descEn, ValueKo = valueKo, ValueEn = valueEn, Value2Ko = value2Ko, Value2En = value2En,
                ToggleField = t.GetField(toggle), ValueField = value == null ? null : t.GetField(value), Value2Field = value2 == null ? null : t.GetField(value2),
            };
        }

        internal const string Color = "color", Detail = "detail", Lens = "lens";

        internal static readonly Spec[] All =
        {
            S(Color, "fxsepia", "FxSepia", "세피아", "Sepia", "오래된 사진처럼 따뜻한 갈색 톤으로 바꿉니다.", "Warm brown tone like an old photograph.", "FxSepiaAmount"),
            S(Color, "fxduo", "FxDuotone", "듀오톤", "Duotone", "어두운 곳과 밝은 곳에 서로 다른 두 색조를 입힙니다. 색조 값으로 고릅니다.", "Maps dark and bright areas to two tints of one hue; pick the hue below.", "FxDuotoneAmount", 0, 1, "세기", "Strength", "FxDuotoneHue", 0, 1, "색조", "Hue"),
            S(Color, "fxteal", "FxTealOrange", "틸 & 오렌지", "Teal & orange", "그림자는 청록, 밝은 곳은 주황으로 나눠 영화 같은 색 분위기를 만듭니다.", "Splits tone: teal shadows and orange highlights for a cinematic look.", "FxTealOrangeAmount"),
            S(Color, "fxmono", "FxMono", "흑백", "Black & white", "색을 빼 흑백으로 만듭니다. 세기를 낮추면 색이 조금 남습니다.", "Removes color; lower values keep some.", "FxMonoAmount"),
            S(Color, "fxinvert", "FxInvert", "색 반전", "Invert", "색을 뒤집습니다.", "Inverts the colors.", "FxInvertAmount"),
            S(Color, "fxnight", "FxNight", "야간 투시", "Night vision", "녹색 야간 투시경처럼 보이게 합니다. 가는 줄과 잡음이 움직입니다.", "Green night-vision look with moving noise and scanlines.", "FxNightAmount"),
            S(Color, "fxthermal", "FxThermal", "열화상", "Thermal", "밝기를 열화상 카메라의 색 띠로 바꿉니다.", "Maps brightness onto a thermal-camera color ramp.", "FxThermalAmount"),
            S(Color, "fxhue", "FxHue", "색조 회전", "Hue shift", "모든 색을 색상환에서 돌립니다. 흰색과 회색은 그대로입니다.", "Rotates all hues around the color wheel; grays stay.", "FxHueShift", -180, 180, "각도", "Degrees"),

            S(Detail, "fxoutline", "FxOutline", "윤곽선", "Outline", "밝기 차이가 큰 경계에 검은 선을 그려 만화 느낌을 줍니다.", "Draws dark lines on strong edges for a comic look.", "FxOutlineAmount"),
            S(Detail, "fxemboss", "FxEmboss", "엠보스", "Emboss", "부조처럼 입체로 눌린 질감을 만듭니다.", "Raised relief texture on top of the picture.", "FxEmbossAmount"),
            S(Detail, "fxhalftone", "FxHalftone", "하프톤", "Halftone", "인쇄물처럼 밝기에 따른 점 무늬로 표시합니다.", "Draws the scene as print-style dots.", "FxHalftoneSize", 3, 24, "점 크기", "Dot size", "FxHalftoneAmount", 0, 1, "세기", "Strength"),
            S(Detail, "fxtilt", "FxTiltShift", "틸트 시프트", "Tilt-shift", "위아래를 흐리게 해 미니어처처럼 보이게 합니다.", "Blurs top and bottom for a miniature look.", "FxTiltShiftAmount"),
            S(Detail, "fxsoft", "FxSoftFocus", "소프트 포커스", "Soft focus", "밝은 곳이 은은하게 번져 몽환적인 분위기를 만듭니다.", "Dreamy glow that blends a blurred copy with highlights.", "FxSoftFocusAmount"),

            S(Lens, "fxbarrel", "FxBarrel", "렌즈 왜곡", "Lens distortion", "화면을 볼록하게(+) 또는 오목하게(−) 휘게 합니다.", "Bulges (+) or pinches (−) the picture like a lens.", "FxBarrelAmount", -.6f, 1, "휨", "Curve"),
            S(Lens, "fxripple", "FxRipple", "물결", "Ripple", "화면이 물속처럼 일렁입니다.", "Underwater-style wobble.", "FxRippleAmount", 0, 2),
            S(Lens, "fxglitch", "FxGlitch", "글리치", "Glitch", "가로 줄이 어긋나고 색이 갈라지는 노이즈입니다.", "Horizontal band shifts with color splitting.", "FxGlitchAmount", 0, 1),
            S(Lens, "fxzoom", "FxZoomBlur", "줌 블러", "Zoom blur", "화면 가운데에서 바깥으로 번지는 흐림입니다.", "Radial blur streaming from the center.", "FxZoomBlurAmount", 0, 1),
            S(Lens, "fxmirror", "FxMirror", "좌우 거울", "Mirror", "화면 왼쪽을 오른쪽에 거울처럼 비춥니다.", "Mirrors the left half onto the right.", null),
            S(Lens, "fxkaleido", "FxKaleido", "만화경", "Kaleidoscope", "화면 가운데를 중심으로 조각을 반복합니다.", "Repeats a wedge around the center.", "FxKaleidoSegments", 2, 16, "조각 수", "Segments"),
            S(Lens, "fxletter", "FxLetterbox", "시네마 바", "Letterbox", "위아래에 검은 띠를 넣어 영화 화면비로 만듭니다. UI는 가리지 않습니다.", "Adds cinema bars to a wide aspect ratio. UI is not covered.", "FxLetterboxAspect", 1.5f, 3, "화면비", "Aspect"),
        };

        // 기본값으로 되돌림 (미리 설정을 고를 때): 켜짐 끄기 + 값 기본값
        private static Settings defaults;
        internal static void Reset(Settings c)
        {
            if (defaults == null) defaults = new Settings();
            foreach (var s in All)
            {
                s.ToggleField.SetValue(c, false);
                if (s.ValueField != null) s.ValueField.SetValue(c, s.ValueField.GetValue(defaults));
                if (s.Value2Field != null) s.Value2Field.SetValue(c, s.Value2Field.GetValue(defaults));
            }
        }

        internal static bool Any(Settings c) => AnyColor(c) || AnyDetail(c) || AnyLens(c);
        internal static bool AnyColor(Settings c) => c.FxSepia || c.FxDuotone || c.FxTealOrange || c.FxInvert || c.FxMono || c.FxNight || c.FxThermal || c.FxHue;
        internal static bool AnyDetail(Settings c) => c.FxOutline || c.FxEmboss || c.FxHalftone || c.FxTiltShift || c.FxSoftFocus;
        internal static bool AnyLens(Settings c) => c.FxBarrel || c.FxRipple || c.FxGlitch || c.FxZoomBlur || c.FxMirror || c.FxKaleido || c.FxLetterbox;
    }
}
