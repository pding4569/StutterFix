using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 타일 색 쓰기(FloorMeshRenderer.color 세터)는 타일마다 가진 재질에 SetColor(_Color) 를 부른다.
    // 프로파일(3440 효과 켬): Exit 메인 표본 4.7%, HELLO 3.8%, Windflower 1.5% 가 이 함수 안이다.
    // 게임에서 타일 재질의 _Color 를 쓰는 곳은 이 세터뿐이다(SetTrackStyle 은 _ShadowColor, IL 전체 검색).
    // 그래서 이 세터로 마지막에 쓴 값과 같으면 재질 값도 그대로이므로 건너뛴다. 처음 쓰기와 재질이 바뀐 경우는 그대로 쓴다.
    internal static class FloorColorSkip
    {
        internal static bool Enabled;
        internal static long Same, Written;
        // 이 세터로 한 번이라도 쓴 렌더러와 그때의 재질. 재질이 다르면(교체) 다시 쓴다.
        private sealed class RefEq : IEqualityComparer<FloorMeshRenderer> { public bool Equals(FloorMeshRenderer a, FloorMeshRenderer b) { return ReferenceEquals(a, b); } public int GetHashCode(FloorMeshRenderer o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); } }
        private static readonly Dictionary<FloorMeshRenderer, Material> written = new Dictionary<FloorMeshRenderer, Material>(new RefEq());

        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.PropertySetter(typeof(FloorMeshRenderer), "color"), prefix: new HarmonyMethod(typeof(FloorColorSkip), nameof(Prefix)));
            h.Patch(AccessTools.Method(typeof(scrLevelMaker), "DrawHolds"), postfix: new HarmonyMethod(typeof(FloorColorSkip), nameof(Clear)));
        }

        public static bool Prefix(FloorMeshRenderer __instance, Color value)
        {
            if (!Enabled) return true;
            var m = __instance.material;
            if (written.TryGetValue(__instance, out var last) && ReferenceEquals(last, m) && Exact(__instance.cachedColor, value)) { Same++; return false; }
            written[__instance] = m;
            Written++;
            return true;
        }

        // Unity 의 Color == 는 근사 비교라 성분을 그대로 비교한다
        private static bool Exact(Color a, Color b) { return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; }

        // 맵을 새로 만들 때 지난 타일을 놓는다
        public static void Clear() { written.Clear(); }

        internal static string State() { return "[타일 색 같은 값] 건너뜀 " + Same + " / 씀 " + Written; }
    }
}
