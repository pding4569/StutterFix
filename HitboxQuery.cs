using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 공(scrPlanet.Update)은 칠 수 있는 동안 매 프레임 Physics2D.OverlapCircleAll(위치, 0.3, 21번 레이어)로 히트박스 장식을 찾는다.
    // 21번 레이어 충돌체는 장식 히트박스뿐이고, 결과는 장식의 히트박스 동작에만 쓴다(디컴파일로 확인).
    // 장식 히트박스 값은 scrDecoration.Awake/Setup 에서만 바뀌고 Dormancy 가 그 둘을 감시해 매 프레임(장식 관리자 Update, 실행 순서 -5)
    // 히트박스 장식 목록을 다시 확인한다. 그 목록이 이번 프레임에 비어 있으면 질의 결과도 비어 있으므로 질의를 건너뛴다.
    // 프로파일(3440, 효과 켬): Exit·HELLO 메인 스레드 표본의 1.8%, Windflower 0.5%.
    internal static class HitboxQuery
    {
        internal static bool Enabled;
        internal static long Skipped, Queried;
        private static readonly Collider2D[] Empty = new Collider2D[0];
        private static readonly MethodInfo Original = AccessTools.Method(typeof(Physics2D), nameof(Physics2D.OverlapCircleAll),
            new[] { typeof(Vector2), typeof(float), typeof(int) });

        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(scrPlanet), "Update"), transpiler: new HarmonyMethod(typeof(HitboxQuery), nameof(Transpiler)));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            int n = 0;
            var replacement = AccessTools.Method(typeof(HitboxQuery), nameof(Overlap));
            foreach (var c in code)
            {
                if (c.Calls(Original)) { c.operand = replacement; n++; }
                yield return c;
            }
            Main.Entry.Logger.Log("[히트박스 질의] 공 Update 의 OverlapCircleAll 바꿔 끼움 " + n + "곳 (예상 1)");
        }

        public static Collider2D[] Overlap(Vector2 point, float radius, int layerMask)
        {
            if (Enabled && Dormancy.NoHitboxThisFrame()) { Skipped++; return Empty; }
            Queried++;
            return Physics2D.OverlapCircleAll(point, radius, layerMask);
        }
    }
}
