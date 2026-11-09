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
        // 더하기(LinearDodge)는 모드의 FastBlend 가 재생 중 Legacy Particles/Additive 재질(d + a*s)로 바꿔 끼운다.
        // 같은 값은 화면 복사 없이 미리 곱한 섞기(One, OneMinusSrcAlpha)에 (s*a, 0)을 내면 나오므로,
        // 더하기는 화면 복사 없는 일반 구간에서 그리고 Grab 재질/Additive 재질 어느 쪽이어도 같은 장식으로 본다.
        internal const string AddShader = "Legacy Shaders/Particles/Additive";
        internal const int LinearDodge = 9;
        internal static bool NeedsGrab(int mode) { return mode > 0 && mode != LinearDodge; }
        // 이 혼합 모드를 그리는 원래 재질의 셰이더로 맞는가
        internal static bool ShaderFits(UnityEngine.Shader s, int mode)
        {
            if (!s || mode <= 0) return false;
            string n = s.name;
            return n == GrabShader || (mode == LinearDodge && n == AddShader);
        }
        // 맡을 수 있는 혼합 장식: 모드를 지원하고, 재질이 그 모드의 원래 셰이더(Grab, 더하기는 Additive 도)인 것만.
        internal static bool Takeable(scrVisualDecoration d, UnityEngine.Renderer r)
        {
            if (!DrawCollapsePolicy.BlendModes) return false;
            int id = Id(d);
            if (id <= 0) return false;
            if (NeedsGrab(id) && !DrawCollapsePolicy.BlendGrab) return false;
            // 혼합 효과의 마스크(스텐실)·텍스처 오버레이 모드는 화면 식이 다르다: 원래 렌더러로 남긴다(7777 light_flare (9))
            var fx = d.blendModeEffect;
            if (fx && (fx.MaskMode != BlendModes.MaskMode.Disabled || fx.RenderMode != BlendModes.RenderMode.SelfWithScreen)) return false;
            var m = r ? r.sharedMaterial : null;
            return m && ShaderFits(m.shader, id);
        }
        // 준비 때 항목의 혼합 모드를 정한다. 진단: 재질은 혼합인데 0 으로 정해진 첫 몇 개를 적는다.
        static int noted;
        internal static int ItemMode(scrVisualDecoration d, UnityEngine.Renderer r)
        {
            int mode = Takeable(d, r) ? Id(d) : 0;
            var m = r ? r.sharedMaterial : null;
            if (mode == 0 && m && m.shader && (m.shader.name == GrabShader || m.shader.name == AddShader) && noted++ < 6)
                Main.Entry.Logger.Log("[gpudeco blend] 혼합 재질인데 모드 0: " + (d ? d.name : "null") + " field=" + (d ? blendField(d).ToString() : "-")
                    + " id=" + Id(d) + " shader=" + m.shader.name + " effect=" + (d && d.blendModeEffect ? d.blendModeEffect.enabled.ToString() : "null")
                    + " policy=" + DrawCollapsePolicy.BlendModes);
            return mode;
        }
        internal static bool Supported(scrVisualDecoration d) { int id = Id(d); return id == 0 || (id > 0 && DrawCollapsePolicy.BlendModes); }
    }
}
#endif
