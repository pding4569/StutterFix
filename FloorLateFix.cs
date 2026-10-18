using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // scrFloor.LateUpdate 는 켜진 타일마다 매 프레임 thisTransform.position / floorRenderer.transform.localScale 을 두 번씩(비교·저장) 읽어
    // isTweening·lastPos·lastScale 을 갱신한다. 이 세 필드는 게임 전체(두 어셈블리, IL 전수 확인)에서 LateUpdate 안의
    // multiplanetLine != null 갈래(다중 행성 선 따라가기)에서만 읽힌다. 그래서 multiplanetLine 이 null 인 타일은 그 읽기를 건너뛴다(기본값을 넣는다).
    // 바꾸는 곳은 네 군데뿐: (ldfld thisTransform; get_position) 2곳, (ldfld floorRenderer; Component.get_transform; get_localScale) 2곳.
    // multiplanetLine 갈래 안의 base.transform·multiplanetLine.transform 은 건드리지 않는다.
    internal static class FloorLateFix
    {
        internal static bool Enabled = true;
        internal static int Replaced;
        private static readonly AccessTools.FieldRef<scrFloor, LineRenderer> lineRef = AccessTools.FieldRefAccess<scrFloor, LineRenderer>("multiplanetLine");

        internal static void Install(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(scrFloor), "LateUpdate");
                if (m == null) { Main.Entry.Logger.Log("[타일 LateUpdate] 없음"); return; }
                h.Patch(m, transpiler: new HarmonyMethod(typeof(FloorLateFix), nameof(Transpiler)));
                Main.Entry.Logger.Log("[타일 LateUpdate] 다중 행성 아닌 타일의 위치·크기 읽기 생략 (바꾼 곳 " + Replaced + "/4)");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[타일 LateUpdate] 설치 실패: " + ex.Message); }
        }

        public static Vector3 Pos(Transform t, scrFloor f) { return Enabled && ReferenceEquals(lineRef(f), null) ? default(Vector3) : t.position; }
        public static Vector3 Scale(Component c, scrFloor f) { return Enabled && ReferenceEquals(lineRef(f), null) ? default(Vector3) : c.transform.localScale; }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var fThis = AccessTools.Field(typeof(scrFloor), "thisTransform");
            var fRend = AccessTools.Field(typeof(scrFloor), "floorRenderer");
            var getPos = AccessTools.PropertyGetter(typeof(Transform), "position");
            var getScale = AccessTools.PropertyGetter(typeof(Transform), "localScale");
            var getTr = AccessTools.PropertyGetter(typeof(Component), "transform");
            var pos = AccessTools.Method(typeof(FloorLateFix), nameof(Pos));
            var scale = AccessTools.Method(typeof(FloorLateFix), nameof(Scale));
            // 먼저 바꿀 곳이 정확히 4곳인지 센다(다르면 아무것도 안 고치고 원래대로)
            int found = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].LoadsField(fThis) && i + 1 < code.Count && code[i + 1].Calls(getPos)) found++;
                else if (code[i].LoadsField(fRend) && i + 2 < code.Count && code[i + 1].Calls(getTr) && code[i + 2].Calls(getScale)) found++;
            }
            if (found != 4) { Replaced = 0; return code; }
            int n = 0;
            for (int i = 0; i < code.Count; i++)
            {
                var c = code[i];
                if (c.LoadsField(fThis) && i + 1 < code.Count && code[i + 1].Calls(getPos))
                {
                    // ldfld thisTransform; callvirt get_position  ->  ldfld thisTransform; ldarg.0; call Pos   (명령 객체를 그대로 고쳐 라벨 유지)
                    code[i + 1].opcode = OpCodes.Call; code[i + 1].operand = pos;
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                    i += 2; n++; continue;
                }
                if (c.LoadsField(fRend) && i + 2 < code.Count && code[i + 1].Calls(getTr) && code[i + 2].Calls(getScale))
                {
                    // ldfld floorRenderer; call get_transform; callvirt get_localScale  ->  ldfld floorRenderer; ldarg.0; call Scale
                    code[i + 1].opcode = OpCodes.Ldarg_0; code[i + 1].operand = null;
                    code[i + 2].opcode = OpCodes.Call; code[i + 2].operand = scale;
                    i += 2; n++; continue;
                }
            }
            Replaced = n;
            return code;
        }
    }
}
