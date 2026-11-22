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
            // scrController 가 매 프레임 AudioListener.volume 에 같은 값을 다시 쓴다(HELLO 프로파일 메인 스레드 1.0%). 값이 같으면 건너뛴다.
            try { h.Patch(AccessTools.Method(typeof(scrController), "UpdateListenerVolume"), prefix: new HarmonyMethod(typeof(HitboxQuery), nameof(ListenerPrefix))); } catch (System.Exception e) { Lg.I("[소리 크기] 설치 실패: " + e.Message); }
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
            Lg.I("[히트박스 질의] 공 Update 의 OverlapCircleAll 바꿔 끼움 " + n + "곳 (예상 1)");
        }

        // 히트박스 장식은 있어도 그 충돌체가 모두 꺼져 있으면(숨은 장식·화면 밖) 질의 결과는 비어 있다.
        // 그런데 질의는 autoSyncTransforms 때문에 매 프레임 모든 2D 충돌체 위치 맞추기(PhysicsManager2D::SyncTransforms)까지 부른다.
        // HELLO 프로파일(2026-11-22): 질의 3.1% + SyncTransforms 3.0%, 활성 충돌체 48개 모두 8번 레이어(장식 히트박스 레이어 아님).
        private static AccessTools.FieldRef<scrDecoration, Collider2D> activeRef;
        internal static long SkippedInactive, VerifyChecks, VerifyMismatch;
        internal static bool Verify;
        internal static long WhyNullList, WhyBig, WhyNullDeco, WhyActive; internal static string LastActive = "";
        private static bool NoActiveCollider(int layerMask)
        {
            var list = Dormancy.PreparedHitboxes();
            if (list == null) { WhyNullList++; return false; }
            if (list.Count > 1024) { WhyBig++; return false; }
            if (activeRef == null) { try { activeRef = AccessTools.FieldRefAccess<scrDecoration, Collider2D>("activeCollider"); } catch { return false; } }
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if ((object)d == null) { WhyNullDeco++; return false; }
                if (!d) continue;
                var c = activeRef(d);
                if ((object)c == null || !c) continue;
                if (c.enabled && c.gameObject.activeInHierarchy && (layerMask & (1 << c.gameObject.layer)) != 0) { WhyActive++; LastActive = c.GetType().Name + "@" + c.gameObject.layer + " mask=" + layerMask + " n=" + list.Count; return false; }
            }
            return true;
        }

        internal static long ListenerSkipped;
        public static bool ListenerPrefix()
        {
            float num = (float)scrController.volume * 0.1f;
            float v = 0.5f * num * num;
            if (AudioListener.volume == v) { ListenerSkipped++; return false; }
            return true;
        }

        public static Collider2D[] Overlap(Vector2 point, float radius, int layerMask)
        {
            if (Enabled && Dormancy.NoHitboxThisFrame()) { Skipped++; return Empty; }
            if (Enabled && NoActiveCollider(layerMask))
            {
                SkippedInactive++;
                if (Verify) { VerifyChecks++; if (Physics2D.OverlapCircleAll(point, radius, layerMask).Length != 0) VerifyMismatch++; }
                return Empty;
            }
            Queried++;
            return Physics2D.OverlapCircleAll(point, radius, layerMask);
        }
    }
}
