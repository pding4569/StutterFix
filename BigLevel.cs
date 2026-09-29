using System;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 타일이 아주 많은 맵(9만~100만 타일) 열기가 타일 수의 제곱으로 느려지던 것.
    //
    // 게임(scrLevelMaker.InstantiateFloatFloors)은 새 타일마다 Instantiate 한 뒤 "Floors" 오브젝트 밑으로 옮긴다(transform.parent = Floors).
    // 옮길 때마다 유니티가 이미 들어 있는 타일 전부가 든 계층에 새 타일을 합치느라, 타일이 늘수록 한 번 옮기는 값이 커진다.
    // 2026-09-27 시험 맵(실제 큰 맵 앞부분·패턴 되풀이, 에디터에서 열기, 개발자용 자동 시험), 타일 만들기(MakeLevel):
    //   4만 5.0초, 9만 15.4초, 18만 61.7초, 36만 271초 (2배마다 4배 = 제곱)
    //   Floors 계층 용량 미리 잡기: 효과 없음(18만 76.7초). 만드는 동안 자동 GC 멈춤: 58.5초(거의 그대로)
    //   처음부터 Floors 밑에 만들기(Instantiate 에 부모를 줌): 18만 18.3초, 36만 36.8초로 열기는 빨라졌지만, 그 뒤 Play·편집 복귀 때
    //     타일마다 위치를 다시 잡는 ResetFloor 가 12초 -> 77초로 느려졌다(한 계층에 수십만 개가 몰려 변환 갱신이 느림)
    //   Floors 밑으로 옮기지 않고 각자 둠: 36만 열기 294초 -> 60초 (타일 만들기 271초 -> 36초), Play 26초·편집 복귀 25초는 원래와 같음
    // 그래서 타일이 많은 맵(3만 개 이상)은 새 타일을 Floors 밑으로 옮기지 않는다. Floors 는 원점에 있는 빈 정리용 오브젝트라 타일의 위치·회전·
    // 크기는 같다. 게임 전체 IL 에서 "Floors" 를 쓰는 곳은 이 함수와 GetFloorContainer 뿐이고, 타일 코드(scrFloor)는 부모를 쓰지 않는다.
    // 이미 있는 타일(작은 맵에서 넘어온 것)을 다시 쓰는 것은 게임 그대로다.
    internal static class BigLevel
    {
        internal static bool Enabled = true;
        internal static int MinFloors = 30000;
        internal static int NoNumMin = 200000;   // 이만큼 넘는 맵은 타일마다 붙은 에디터 번호 표시(Canvas+UI 글자)를 만들자마자 없앤다 (메모리)
        internal static long Stripped;
        private static bool strip;
        internal static long Skips, Maps;
        private static bool active;
        private static readonly AccessTools.FieldRef<scrLevelMaker, float[]> anglesRef = AccessTools.FieldRefAccess<scrLevelMaker, float[]>("floorAngles");

        internal static void Install(Harmony h)
        {
            try
            {
                var inst = AccessTools.Method(typeof(scrLevelMaker), "InstantiateFloatFloors");
                var make = AccessTools.Method(typeof(scrLevelMaker), "MakeLevel");
                if (inst == null || make == null) { Main.Entry.Logger.Log("[큰 맵] 게임 코드 모양이 달라 끔"); return; }
                h.Patch(inst, prefix: new HarmonyMethod(typeof(BigLevel), nameof(InstPrefix)), transpiler: new HarmonyMethod(typeof(BigLevel), nameof(InstTranspiler)),
                        finalizer: new HarmonyMethod(typeof(BigLevel), nameof(InstFinalizer)));
                if (Edition.Dev) h.Patch(make, finalizer: new HarmonyMethod(typeof(BigLevel), nameof(MakeFinalizer)));
                foreach (var name in new[] { "DrawFloorNums", "Play" })
                {
                    var m = AccessTools.Method(typeof(scnEditor), name, Type.EmptyTypes);
                    if (m != null) h.Patch(m, transpiler: new HarmonyMethod(typeof(BigLevel), nameof(NumTranspiler)));
                    else Main.Entry.Logger.Log("[큰 맵] scnEditor." + name + " 없음");
                }
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[큰 맵] 설치 실패: " + ex.Message); }
        }

        private static int FloorCount(scrLevelMaker lm)
        {
            try { var a = anglesRef(lm); return a == null ? 0 : a.Length + 1; } catch { return 0; }
        }

        public static void InstPrefix(scrLevelMaker __instance)
        {
            int n = Enabled ? FloorCount(__instance) : 0;
            active = n >= MinFloors;
            strip = active && n >= NoNumMin;
            if (strip) Main.Entry.Logger.Log("[큰 맵] 타일 " + n + "개: 타일마다 붙은 에디터 번호 표시를 없애 메모리를 줄임 (이 맵에서는 타일 번호 보기가 안 나옴)");
            if (active) { Maps++; Main.Entry.Logger.Log("[큰 맵] 타일 " + n + "개: 새 타일을 Floors 밑으로 옮기지 않고 각자 둠 (열기가 타일 수의 제곱으로 느려지는 것 막기)"); }
        }
        public static Exception InstFinalizer(Exception __exception) { active = false; strip = false; return __exception; }

        // 타일 만들기(Instantiate) 대신. 아주 큰 맵이면 새 타일의 에디터 번호 표시(꺼져 있는 Canvas + UI 글자 + 그림자 + 외곽선, 컴포넌트 11개)를 바로 없앤다.
        // 타일 하나의 컴포넌트 절반이 이것이다. 이 필드를 확인 없이 쓰는 곳(scnEditor.DrawFloorNums, scnEditor.Play)은 NumObj 로 바꾼다.
        public static GameObject InstMaybe(GameObject original, Vector3 position, Quaternion rotation)
        {
            var go = UnityEngine.Object.Instantiate(original, position, rotation);
            if (strip)
            {
                try
                {
                    var f = go.GetComponent<scrFloor>();
                    if (f != null && f.editorNumText != null) { UnityEngine.Object.DestroyImmediate(f.editorNumText.gameObject); f.editorNumText = null; Stripped++; }
                }
                catch { }
            }
            return go;
        }

        // floor.editorNumText.gameObject 대신: 번호 표시를 없앤 타일이면 아무도 안 쓰는 빈 오브젝트를 준다(SetActive 해도 아무 일 없음)
        private static GameObject dummy;
        public static GameObject NumObj(scrLetterPress t)
        {
            if (t != null) return t.gameObject;
            if (dummy == null) { dummy = new GameObject("StutterFix.NoFloorNum"); dummy.hideFlags = HideFlags.HideAndDontSave; dummy.SetActive(false); }
            return dummy;
        }
        public static System.Collections.Generic.IEnumerable<CodeInstruction> NumTranspiler(System.Collections.Generic.IEnumerable<CodeInstruction> code)
        {
            var field = AccessTools.Field(typeof(scrFloor), "editorNumText");
            var getGo = AccessTools.PropertyGetter(typeof(Component), "gameObject");
            var mine = AccessTools.Method(typeof(BigLevel), nameof(NumObj));
            bool afterField = false;
            foreach (var ci in code)
            {
                if (afterField && ci.opcode == System.Reflection.Emit.OpCodes.Callvirt && ci.operand is System.Reflection.MethodInfo m && m == getGo) { ci.opcode = System.Reflection.Emit.OpCodes.Call; ci.operand = mine; }
                afterField = ci.opcode == System.Reflection.Emit.OpCodes.Ldfld && ci.operand is System.Reflection.FieldInfo fi && fi == field;
                yield return ci;
            }
        }

        // transform.parent = Floors 대신. 큰 맵이면 옮기지 않는다.
        public static void SetParentMaybe(Transform child, Transform parent)
        {
            if (active) { Skips++; return; }
            child.parent = parent;
        }

        public static System.Collections.Generic.IEnumerable<CodeInstruction> InstTranspiler(System.Collections.Generic.IEnumerable<CodeInstruction> code)
        {
            var setParent = AccessTools.PropertySetter(typeof(Transform), "parent");
            var maybe = AccessTools.Method(typeof(BigLevel), nameof(SetParentMaybe));
            System.Reflection.MethodInfo inst = null;
            foreach (var m in typeof(UnityEngine.Object).GetMethods())
                if (m.Name == "Instantiate" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3 && m.GetParameters()[1].ParameterType == typeof(Vector3)) { inst = m.MakeGenericMethod(typeof(GameObject)); break; }
            var instMaybe = AccessTools.Method(typeof(BigLevel), nameof(InstMaybe));
            int p = 0, n = 0;
            foreach (var ci in code)
            {
                if (setParent != null && ci.opcode == System.Reflection.Emit.OpCodes.Callvirt && ci.operand is System.Reflection.MethodInfo sp && sp == setParent)
                { ci.opcode = System.Reflection.Emit.OpCodes.Call; ci.operand = maybe; p++; }
                else if (inst != null && ci.opcode == System.Reflection.Emit.OpCodes.Call && ci.operand is System.Reflection.MethodInfo im && im == inst)
                { ci.operand = instMaybe; n++; }
                yield return ci;
            }
            if (p != 2 || n != 2) Main.Entry.Logger.Log("[큰 맵] 바꿔치기: 부모 지정 " + p + "곳, 타일 만들기 " + n + "곳 (예상 2곳씩)");
        }

        // (개발자용) 큰 맵: 타일 하나의 구성(오브젝트, 컴포넌트)과 부모를 한 번 적는다. 메모리 줄이기 조사용
        private static bool reported;
        public static Exception MakeFinalizer(Exception __exception, scrLevelMaker __instance)
        {
            try
            {
                if (!reported && __instance != null && __instance.listFloors != null && __instance.listFloors.Count >= MinFloors)
                {
                    reported = true;
                    var f = __instance.listFloors[__instance.listFloors.Count / 2];
                    var sb = new System.Text.StringBuilder();
                    int objs = 0;
                    foreach (var t in f.GetComponentsInChildren<Transform>(true))
                    {
                        objs++;
                        sb.Append(" | ").Append(t == f.transform ? "(타일)" : t.name).Append(t.gameObject.activeSelf ? "" : "(꺼짐)").Append(":");
                        foreach (var c in t.GetComponents<Component>()) if (c != null) sb.Append(" ").Append(c.GetType().Name);
                    }
                    Main.Entry.Logger.Log(string.Format("[큰 맵] 타일 하나: 오브젝트 {0}개{1} | 부모 {2}", objs, sb, f.transform.parent == null ? "없음(각자)" : f.transform.parent.name));
                }
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[큰 맵] 타일 구성 기록 실패: " + ex.Message); }
            return __exception;
        }
    }
}
