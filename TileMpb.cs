#if DEV || AUTOTEST
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 시험 전용: 타일 색을 재질 대신 MaterialPropertyBlock 으로 쓴다. 재질 값을 쓰면 렌더 스레드와 공유한 재질 자료를 복사한다.
    // 값 비교용이라 다시 켜기(되돌리기)와 그림 대조는 아직 없다.
    internal static class TileMpb
    {
        private static Harmony harmony;
        private static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        internal static void Enable()
        {
            if (harmony != null) return;
            harmony = new Harmony("StutterFix.TileMpb.test");
            harmony.Patch(AccessTools.PropertySetter(typeof(FloorMeshRenderer), "color"), prefix: new HarmonyMethod(typeof(TileMpb), nameof(SetPrefix)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(FloorMeshRenderer), "color"), prefix: new HarmonyMethod(typeof(TileMpb), nameof(GetPrefix)));
            // 지금 재질에 있는 색을 기억값으로 맞춘다
            foreach (var f in Object.FindObjectsByType<FloorMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.material != null) f.cachedColor = f.material.GetColor(scrFloor.ShaderProperty_Color);
            Main.Entry.Logger.Log("[tilempb] 켬");
        }

        public static bool SetPrefix(FloorMeshRenderer __instance, Color value)
        {
            var r = __instance.renderer;
            r.GetPropertyBlock(block);
            block.SetColor(scrFloor.ShaderProperty_Color, value);
            r.SetPropertyBlock(block);
            __instance.cachedColor = value;
            return false;
        }

        public static bool GetPrefix(FloorMeshRenderer __instance, ref Color __result)
        {
            __result = __instance.cachedColor;
            return false;
        }
    }
}
#endif
