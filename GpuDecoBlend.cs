#if DEV || AUTOTEST || GPUDECO
using System.Collections.Generic;
using HarmonyLib;

namespace StutterFix
{
    // GpuDeco 혼합 모드: 장식의 blendMode 를 GpuDecoBatchBlend 셰이더의 모드 번호(Blend Modes 에셋 키워드 순서)로 바꾼다.
    // 0 = 혼합 없음, -1 = 아직 지원하지 않는 모드(원래 방식으로 남김).
    internal static class GpuDecoBlend
    {
        static readonly Dictionary<string, int> ids = new Dictionary<string, int>
        {
            ["Darken"] = 1, ["Multiply"] = 2, ["LinearBurn"] = 4, ["Lighten"] = 6, ["Screen"] = 7, ["LinearDodge"] = 9,
            ["Overlay"] = 11, ["SoftLight"] = 12, ["Difference"] = 18, ["Exclusion"] = 19, ["Subtract"] = 20, ["Divide"] = 21,
        };
        static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> blendField = AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        internal const string GrabShader = "Hidden/BlendModes/SpritesDefault/Grab";   // 실제 장식 재질(그리기마다 화면 복사)
        internal static int Id(scrVisualDecoration d)
        {
            if (!d) return 0;
            string name = blendField(d).ToString();
            if (name == "None") return 0;
            return ids.TryGetValue(name, out int id) ? id : -1;
        }
        // 반복(tile)은 장식 필드(repeatX)가 아니라 재질에 실제로 들어간 값을 쓴다. 최신 게임은 스프라이트 재질에 RepeatX 를 넣지 않는다
        // (SetTile 은 옛 v1.5 호환일 때만 재질 값을 바꾼다). 장식 필드를 쓰면 원래 그리기에 없는 반복이 생겼다(7777 사슬).
        internal static float EffectiveRepeat(UnityEngine.Renderer r, string prop)
        {
            var m = r ? r.sharedMaterial : null;
            return m && m.HasProperty(prop) ? m.GetFloat(prop) : 1f;
        }
        internal static bool Supported(scrVisualDecoration d) { int id = Id(d); return id == 0 || (id > 0 && DrawCollapsePolicy.BlendModes); }
    }
}
#endif
