using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 곡 중 쓰레기 줄이기. 곡 중에는 GC 를 미루므로 쓰레기가 쌓이면 긴 곡에서 힙 한계에 닿는다(GcControl).
    // 2026-09-28 개발자용 측정(1시간 맵 230BPM 100ksub, alloc-phase / alloc-probe): 곡 중 초당 2.2MB.
    //
    // 1) scrFloor.Update: 볼륨 색 모드(TrackColorType.Volume)에서만 람다가 쓰는 객체(컴파일러가 만든 DisplayClass, 32바이트)를
    //    함수 첫 줄에서 모든 타일이 매 프레임 만든다(IL_0000 newobj). 초당 1만 2천 번 = 0.38MB/s.
    //    이 객체는 볼륨 모드 분기의 람다(AppendCallback)만 붙잡는다. 그래서 볼륨 모드이거나 아직 Start 전인 타일(Start 가 색 모드를
    //    바꿀 수 있음)은 원래대로 새로 만들고, 나머지는 하나를 돌려쓴다. 돌려쓰는 객체는 아무도 붙잡지 않으므로 결과가 같다.
    internal static class AllocFix
    {
        internal static bool Enabled = true;
        internal static long FloorReused;
        private static object floorDummy;
        private static Type floorDisplay;
        private static AccessTools.FieldRef<scrFloor, bool> didRunStart;

        internal static void Install(Harmony h)
        {
            try
            {
                var upd = AccessTools.Method(typeof(scrFloor), "Update");
                didRunStart = AccessTools.FieldRefAccess<scrFloor, bool>("didRunStart");
                if (upd != null) h.Patch(upd, transpiler: new HarmonyMethod(typeof(AllocFix), nameof(FloorTranspiler)));
                InstallInput(h);
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[쓰레기 줄이기] 설치 실패: " + ex.Message); }
        }

        // 볼륨 모드가 될 수 있으면 true (원래대로 새로 만든다)
        public static bool NeedNew(scrFloor f)
        {
            if (!Enabled || f == null) return true;
            if (!didRunStart(f) || f.specialColorType == TrackColorType.Volume) return true;
            FloorReused++;
            return false;
        }
        // 원래 newobj 대신: 볼륨 모드가 될 수 있으면 새로(원래대로), 아니면 돌려쓰는 것
        public static object MakeOrReuse(scrFloor f) { return NeedNew(f) ? Activator.CreateInstance(floorDisplay) : floorDummy; }

        public static IEnumerable<CodeInstruction> FloorTranspiler(IEnumerable<CodeInstruction> code)
        {
            var list = new List<CodeInstruction>(code);
            // 첫 명령이 DisplayClass 의 newobj 인지 확인 (게임이 바뀌었으면 손대지 않는다)
            if (list.Count == 0 || list[0].opcode != OpCodes.Newobj || !(list[0].operand is ConstructorInfo ctor) || !ctor.DeclaringType.Name.Contains("DisplayClass"))
            {
                Main.Entry.Logger.Log("[쓰레기 줄이기] scrFloor.Update 모양이 달라 끔");
                return list;
            }
            floorDisplay = ctor.DeclaringType;
            floorDummy = Activator.CreateInstance(floorDisplay, true);
            // 첫 명령을 제자리에서 바꿔 라벨·예외 블록을 그대로 둔다
            list[0].opcode = OpCodes.Ldarg_0; list[0].operand = null;
            list.InsertRange(1, new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AllocFix), nameof(MakeOrReuse))),
                new CodeInstruction(OpCodes.Castclass, floorDisplay),
            });
            Main.Entry.Logger.Log("[쓰레기 줄이기] scrFloor.Update: 볼륨 색 모드용 객체를 필요한 타일만 새로 만듦");
            return list;
        }

        // 2) 비동기 키 입력(RDInputType_AsyncKeyboard.Main): 프레임마다 여러 번(눌림/뗌/누르고 있음) 불리며, 부를 때마다 HashSet, LINQ Where,
        //    람다, 특수 키 목록(List + Array.ForEach 람다들)을 새로 만든다. 한 번에 약 640바이트, 초당 0.45MB.
        //    원래: 결과 목록 = new List; 원천 집합(keyDownMask 등)에서 주 키만 골라 새 HashSet(같은 비교자, 원천이 이미 서로 다르므로
        //    순서·구성 그대로) -> (눌림이면) 특수 키와 == 인 것 빼기 -> 키 제한 통과한 것만 순서대로 목록에.
        //    여기서는 새 HashSet 대신 다시 쓰는 목록에 같은 순서로 모으고, 특수 키 목록은 고른 키가 있을 때만 원래 함수로 만든다
        //    (고른 키가 없으면 빼기가 아무 일도 안 하고, 특수 키 함수는 입력 상태를 읽기만 한다). 결과 목록은 원래처럼 매번 새로 만든다
        //    (게임 여러 곳이 이 목록을 참조). "떼어져 있음"(IsUp)과 그 밖의 상태는 원래 코드 그대로.
        //    개발자용(input-verify 파일): 부를 때마다 원래 코드도 돌려 결과(개수, 키 순서)를 비교한다.
        private static AccessTools.FieldRef<RDInputType_AsyncKeyboard, SkyHook.KeyLabel[]> mainKeysRef;
        private static Func<RDInputType, ButtonState, RDInputType.MainStateCount> getStateCount;
        private static Func<RDInputType_AsyncKeyboard, List<AsyncKeyCode>> getSpecial;
        private static readonly List<AsyncKeyCode> tmp = new List<AsyncKeyCode>();
        internal static bool VerifyInput;
        internal static long InputFast, VerifyCalls, VerifyDiffs, VerifyWithKeys;

        private static void InstallInput(Harmony h)
        {
            var main = AccessTools.Method(typeof(RDInputType_AsyncKeyboard), "Main", new[] { typeof(ButtonState) });
            var gsc = AccessTools.Method(typeof(RDInputType), "GetStateCount");
            var gsi = AccessTools.Method(typeof(RDInputType_AsyncKeyboard), "GetSpecialInput");
            if (main == null || gsc == null || gsi == null) { Main.Entry.Logger.Log("[쓰레기 줄이기] 비동기 키 입력: 게임 코드 모양이 달라 끔"); return; }
            mainKeysRef = AccessTools.FieldRefAccess<RDInputType_AsyncKeyboard, SkyHook.KeyLabel[]>("mainKeys");
            getStateCount = AccessTools.MethodDelegate<Func<RDInputType, ButtonState, RDInputType.MainStateCount>>(gsc);
            getSpecial = AccessTools.MethodDelegate<Func<RDInputType_AsyncKeyboard, List<AsyncKeyCode>>>(gsi);
            if (Edition.Dev) VerifyInput = System.IO.File.Exists(System.IO.Path.Combine(Main.Entry.Path, "input-verify"));
            h.Patch(main, prefix: new HarmonyMethod(typeof(AllocFix), nameof(AsyncMainPrefix)), postfix: VerifyInput ? new HarmonyMethod(typeof(AllocFix), nameof(AsyncMainVerify)) : null);
            Main.Entry.Logger.Log("[쓰레기 줄이기] 비동기 키 입력: 임시 객체 없이" + (VerifyInput ? " (개발자용: 원래 코드와 매번 비교)" : ""));
            InstallMouse(h);
            InstallKeyboard(h);
        }

        // 원래 결과를 목록에 채우고 개수를 돌려준다.
        private static int Compute(RDInputType_AsyncKeyboard kb, ButtonState state, List<AnyKeyCode> outKeys)
        {
            HashSet<AsyncKeyCode> src = state == ButtonState.IsDown ? AsyncInputManager.keyMask : state == ButtonState.WentDown ? AsyncInputManager.keyDownMask : AsyncInputManager.keyUpMask;
            var mk = mainKeysRef(kb);
            tmp.Clear();
            foreach (var k in src)
            {
                bool main = false;
                for (int i = 0; i < mk.Length; i++) if (mk[i] == k.label) { main = true; break; }
                if (main) tmp.Add(k);
            }
            if (state == ButtonState.WentDown && tmp.Count > 0)
                foreach (var key in getSpecial(kb))
                    for (int i = tmp.Count - 1; i >= 0; i--) if (tmp[i] == key) tmp.RemoveAt(i);
            var cache = Persistence.keyLimiterKeys.asyncKeysCache;
            for (int i = 0; i < tmp.Count; i++)
            {
                var item = tmp[i];
                if (!RDInput.useKeyLimiter || cache.Count <= 0 || cache.Contains(item.key)) outKeys.Add(new AnyKeyCode(item));
            }
            return outKeys.Count;
        }

        public static bool AsyncMainPrefix(RDInputType_AsyncKeyboard __instance, ButtonState state, ref int __result, out List<AnyKeyCode> __state)
        {
            __state = null;
            if (!Enabled) return true;
            if (state != ButtonState.IsDown && state != ButtonState.WentDown && state != ButtonState.WentUp) return true;
            if (VerifyInput)
            {
                // 원래 코드를 그대로 돌리고, 끝나면 우리 계산(원래보다 먼저, 같은 입력 상태)과 비교한다(Postfix)
                if (__instance.isActive) { __state = new List<AnyKeyCode>(); Compute(__instance, state, __state); }
                return true;
            }
            if (!__instance.isActive) { __result = 0; return false; }
            var sc = getStateCount(__instance, state);
            sc.lastFrameUpdated = UnityEngine.Time.frameCount;
            sc.keys = new List<AnyKeyCode>();
            __result = Compute(__instance, state, sc.keys);
            InputFast++;
            return false;
        }

        public static void AsyncMainVerify(RDInputType_AsyncKeyboard __instance, ButtonState state, int __result, List<AnyKeyCode> __state)
        {
            if (__state == null) return;
            VerifyCalls++; if (__result > 0 || __state.Count > 0) VerifyWithKeys++;
            var keys = getStateCount(__instance, state).keys;
            bool same = __result == __state.Count && keys != null && keys.Count == __state.Count;
            for (int i = 0; same && i < __state.Count; i++)
            {
                var a = (AsyncKeyCode)keys[i].value; var b = (AsyncKeyCode)__state[i].value;
                same = a.key == b.key && a.label == b.label;
            }
            if (same) return;
            VerifyDiffs++;
            if (VerifyDiffs <= 5) Main.Entry.Logger.Log(string.Format("[쓰레기 줄이기 검증] 비동기 키 입력 {0}: 원래 {1}개, 우리 {2}개", state, __result, __state.Count));
        }

        // 3) 마우스 입력(RDInputType_Mouse.Main): 프레임마다 상태별로 (from key in MouseKeys where CheckKeyState(key, state) select new AnyKeyCode(key)).ToList()
        //    - LINQ 반복기 둘과 state 를 붙잡는 람다 객체를 매번 만든다(한 번에 약 200바이트, 초당 0.14MB). 같은 조건 검사, 같은 순서로 키를 확인해
        //    새 목록에 넣는다(결과 목록은 원래처럼 새로). 개발자용(input-verify): 원래 코드 결과를 우리 계산과 비교.
        private static KeyCode[] mouseKeys;
        private static Func<RDInputType, bool> isPlayingGet;
        private static Func<KeyCode, ButtonState, bool> checkKey;
        internal static long MouseFast, MouseVerify, MouseDiffs;

        private static void InstallMouse(Harmony h)
        {
            var main = AccessTools.Method(typeof(RDInputType_Mouse), "Main", new[] { typeof(ButtonState) });
            var keysField = AccessTools.Field(typeof(RDInputType_Mouse), "MouseKeys");
            var playing = AccessTools.PropertyGetter(typeof(RDInputType), "isPlaying");
            var ck = AccessTools.Method(typeof(RDInputType_Keyboard), "CheckKeyState", new[] { typeof(KeyCode), typeof(ButtonState) });
            if (main == null || keysField == null || playing == null || ck == null || getStateCount == null) { Main.Entry.Logger.Log("[쓰레기 줄이기] 마우스 입력: 게임 코드 모양이 달라 끔"); return; }
            mouseKeys = (KeyCode[])keysField.GetValue(null);
            isPlayingGet = AccessTools.MethodDelegate<Func<RDInputType, bool>>(playing);
            checkKey = AccessTools.MethodDelegate<Func<KeyCode, ButtonState, bool>>(ck);
            h.Patch(main, prefix: new HarmonyMethod(typeof(AllocFix), nameof(MousePrefix)), postfix: VerifyInput ? new HarmonyMethod(typeof(AllocFix), nameof(MouseVerifyPost)) : null);
            Main.Entry.Logger.Log("[쓰레기 줄이기] 마우스 입력: 임시 객체 없이");
        }

        private static bool MouseOff(RDInputType_Mouse m)
        {
            return !m.isActive || ADOBase.isMobile || !isPlayingGet(m) || ((bool)scnCLS.instance && scnCLS.instance.optionsPanels.showingAnyPanel);
        }

        public static bool MousePrefix(RDInputType_Mouse __instance, ButtonState state, ref int __result)
        {
            if (!Enabled || VerifyInput) return true;
            if (MouseOff(__instance)) { __result = 0; return false; }
            var sc = getStateCount(__instance, state);
            int frame = UnityEngine.Time.frameCount;
            if (sc.lastFrameUpdated == frame) { __result = sc.keys.Count; return false; }
            sc.lastFrameUpdated = frame;
            var list = new List<AnyKeyCode>();
            for (int i = 0; i < mouseKeys.Length; i++) if (checkKey(mouseKeys[i], state)) list.Add(new AnyKeyCode(mouseKeys[i]));
            sc.keys = list;
            __result = list.Count;
            MouseFast++;
            return false;
        }

        // (개발자용) 원래 코드가 돈 뒤, 같은 프레임의 입력 상태로 우리 계산을 해 비교
        public static void MouseVerifyPost(RDInputType_Mouse __instance, ButtonState state, int __result)
        {
            MouseVerify++;
            int expect = 0;
            List<AnyKeyCode> keys = null;
            if (!MouseOff(__instance))
            {
                keys = getStateCount(__instance, state).keys;
                for (int i = 0; i < mouseKeys.Length; i++) if (checkKey(mouseKeys[i], state)) expect++;
            }
            bool same = __result == expect && (keys == null || keys.Count == expect);
            if (same && keys != null)
            {
                int j = 0;
                for (int i = 0; i < mouseKeys.Length && same; i++) if (checkKey(mouseKeys[i], state)) same = (KeyCode)keys[j++].value == mouseKeys[i];
            }
            if (same) return;
            MouseDiffs++;
            if (MouseDiffs <= 5) Main.Entry.Logger.Log(string.Format("[쓰레기 줄이기 검증] 마우스 입력 {0}: 원래 {1}개, 우리 {2}개", state, __result, expect));
        }

        // 4) 레거시 키보드 입력(RDInputType_Keyboard.MainIgnoreActive): 비동기 입력을 써도 scrController.UpdateInput 이 매 프레임 부른다.
        //    부를 때마다 람다(state, keys 를 붙잡음) + 임시 List + (눌림이면) 특수 키 List·람다들을 만든다. 같은 순서로 다시 쓰는 목록에 모으고,
        //    특수 키 목록(CountSpecialInput, 입력 상태만 읽음)은 고른 키가 있을 때만 원래 함수로 만든다(없으면 빼기가 아무 일도 안 함).
        //    결과 목록은 원래처럼 새로. 개발자용(input-verify): 원래 코드 결과와 비교.
        private static AccessTools.FieldRef<RDInputType_Keyboard, KeyCode[]> kbMainKeys;
        private static Func<RDInputType_Keyboard, List<KeyCode>> kbSpecial;
        private static readonly List<KeyCode> ktmp = new List<KeyCode>();
        internal static long KbFast, KbVerify, KbDiffs;

        private static void InstallKeyboard(Harmony h)
        {
            var m = AccessTools.Method(typeof(RDInputType_Keyboard), "MainIgnoreActive", new[] { typeof(ButtonState) });
            var sp = AccessTools.Method(typeof(RDInputType_Keyboard), "CountSpecialInput");
            if (m == null || sp == null || getStateCount == null || checkKey == null) { Main.Entry.Logger.Log("[쓰레기 줄이기] 레거시 키보드 입력: 게임 코드 모양이 달라 끔"); return; }
            kbMainKeys = AccessTools.FieldRefAccess<RDInputType_Keyboard, KeyCode[]>("mainKeys");
            kbSpecial = AccessTools.MethodDelegate<Func<RDInputType_Keyboard, List<KeyCode>>>(sp);
            h.Patch(m, prefix: new HarmonyMethod(typeof(AllocFix), nameof(KbPrefix)), postfix: VerifyInput ? new HarmonyMethod(typeof(AllocFix), nameof(KbVerifyPost)) : null);
            Main.Entry.Logger.Log("[쓰레기 줄이기] 레거시 키보드 입력: 임시 객체 없이");
        }

        private static void KbCompute(RDInputType_Keyboard kb, ButtonState state, List<AnyKeyCode> outKeys)
        {
            var mk = kbMainKeys(kb);
            ktmp.Clear();
            for (int i = 0; i < mk.Length; i++) if (checkKey(mk[i], state)) ktmp.Add(mk[i]);
            if (state == ButtonState.WentDown && ktmp.Count > 0)
                foreach (var item in kbSpecial(kb)) ktmp.Remove(item);
            var cache = Persistence.keyLimiterKeys.unityKeysCache;
            for (int i = 0; i < ktmp.Count; i++)
                if (!RDInput.useKeyLimiter || cache.Count <= 0 || cache.Contains(ktmp[i])) outKeys.Add(new AnyKeyCode(ktmp[i]));
        }

        public static bool KbPrefix(RDInputType_Keyboard __instance, ButtonState state, ref int __result, out List<AnyKeyCode> __state)
        {
            __state = null;
            if (!Enabled) return true;
            if (VerifyInput) { __state = new List<AnyKeyCode>(); KbCompute(__instance, state, __state); return true; }
            var sc = getStateCount(__instance, state);
            int frame = UnityEngine.Time.frameCount;
            if (sc.lastFrameUpdated == frame) { __result = sc.keys.Count; return false; }
            sc.lastFrameUpdated = frame;
            sc.keys = new List<AnyKeyCode>();
            KbCompute(__instance, state, sc.keys);
            __result = sc.keys.Count;
            KbFast++;
            return false;
        }

        public static void KbVerifyPost(RDInputType_Keyboard __instance, ButtonState state, int __result, List<AnyKeyCode> __state)
        {
            if (__state == null) return;
            KbVerify++;
            var keys = getStateCount(__instance, state).keys;
            bool same = keys != null && __result == keys.Count && keys.Count == __state.Count;
            for (int i = 0; same && i < __state.Count; i++) same = (KeyCode)keys[i].value == (KeyCode)__state[i].value;
            if (same) return;
            KbDiffs++;
            if (KbDiffs <= 5) Main.Entry.Logger.Log(string.Format("[쓰레기 줄이기 검증] 레거시 키보드 {0}: 원래 {1}개, 우리 {2}개", state, __result, __state.Count));
        }

        internal static string VerifySummary()
        {
            return VerifyInput ? string.Format("[쓰레기 줄이기 검증] 비동기 키 입력 비교 {0}번(키가 있던 것 {2}번) 중 다름 {1}번, 마우스 입력 비교 {3}번 중 다름 {4}번, 레거시 키보드 비교 {5}번 중 다름 {6}번", VerifyCalls, VerifyDiffs, VerifyWithKeys, MouseVerify, MouseDiffs, KbVerify, KbDiffs) : "";
        }
    }
}
