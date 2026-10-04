using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 효과 컴포넌트 다시 쓰기: 에디터 나가기·재생 시작 때 타일마다 효과 컴포넌트(ffx) 수만~수십만 개를 지웠다 새로 붙이던 것.
    //
    // 2026-09-28 Arche(효과 약 11만 개), 개발자용 측정:
    //   나가기: scrLevelMaker.ResetFloor 가 타일마다 효과를 DestroyImmediate (0.6~0.76초, 컴포넌트 하나 5.2us - 유니티 안쪽 값)
    //   재생 시작: scnGame.ApplyEventsToFloors 가 이벤트마다 AddComponent (하나 2~3.7us) + Awake + Decode
    // 지우고 붙이는 것 자체는 유니티 값이라 줄일 수 없어서, 지우지 않고 남겨 뒀다가 다음 ApplyEventsToFloors 에서 같은 타일·같은 종류를
    // 요청하면 "새로 만든 것과 같은 상태"로 되돌려 돌려준다.
    //   되돌리기 = 효과 클래스(ffx..ADOBase)의 필드를 모두 기본값으로 지우고 생성자를 다시 실행(필드 초기값) -> enabled 켬 -> Awake.
    //     유니티 기본 클래스(MonoBehaviour..Object) 생성자는 비어 있다(설치할 때 IL 길이로 확인, 아니면 끔).
    //   다시 쓰는 종류: Awake 말고 유니티 메시지(Start, Update, OnDestroy, OnEnable 등)가 없고 코루틴·Invoke 를 쓰지 않는 효과만.
    //     (MoveTrack 은 Start, 고급 필터는 OnDestroy 가 있어 원래대로 지운다.) 체크포인트 조건 목록(onCheckpointEffects,
    //     게임이 비우지 않는다)에 들어 있는 효과도 원래대로 지운다.
    //   순서: 다시 쓴 효과는 게임오브젝트 안에서 옛 자리에 있고, 새로 붙인 것은 맨 뒤에 붙는다. 그래서 붙일 때 돌려준 순서를 기록해 두고,
    //     게임의 효과 찾기(GetComponent·GetComponents·GetComponentsInChildren·GetOrAddComponent, 효과를 찾는 게임 함수 전부를 바꿔 끼움)가
    //     그 순서(= 원래 방식의 컴포넌트 순서)로 돌려주게 한다. 게임은 SendMessage 를 쓰지 않는다(전체 코드 검색).
    //   남겨 둔 효과(원래라면 지워졌을 것)는 같은 효과 찾기에서 빼 준다. 그래서 나가기 뒤 편집 화면에서도 게임에는 원래처럼 없는 것으로
    //     보인다. 쓰지 않은 것은 다음 붙이기에서 쓸 수 있게 남겨 두고(편집 화면의 붙이기는 재생 때 쓰는 효과의 일부만 붙인다),
    //     붙이기 8번 연속 쓰이지 않으면 지운다.
    // 개발자용 검증(ffx-verify.txt): ApplyEventsToFloors 마다, 다시 쓴 결과를 찍어 두고 같은 프레임에 원래 방식(전부 지우고 새로 붙임)으로
    //   한 번 더 해서 타일별 효과 순서·종류·모든 필드(목록·사전·델리게이트 안까지)를 비교한다(끝 상태는 원래 방식 결과).
    internal static class FfxReuse
    {
        internal static bool Enabled = true;
        internal static bool Verify;   // (개발자용) ffx-verify.txt
        internal static bool ForceOff;  // (개발자용 자동 시험 ffxreuse off) 비교 측정용
        private static bool installed, inPass, verifying;

        // 원래라면 지워졌을 효과 (게임의 효과 찾기에서 뺀다)
        private static readonly HashSet<Component> pending = new HashSet<Component>(RefEq<Component>.I);
        // 이 모드의 붙이기(Add, 효과 붙이기 안)로 붙은 효과. 이것만 남겨 둔다. 게임이 붙이기 밖에서 직접 붙인 것(ffxChangeTrack.PrepFloor 의
        // 등장·사라짐 효과)은 다음에 같은 자리로 다시 요청되지 않아, 남겨 두면 판마다 쌓였다(2026 맵 판당 약 1만 9천 개). 그런 것은 원래대로 지운다.
        private static readonly HashSet<Component> ours = new HashSet<Component>(RefEq<Component>.I);
        private sealed class Slot
        {
            public readonly Dictionary<Type, List<ffxPlusBase>> byType = new Dictionary<Type, List<ffxPlusBase>>();   // 남겨 둔 것, 종류별 (원래 순서)
            public readonly Dictionary<Type, int> used = new Dictionary<Type, int>();
            public readonly List<Component> logical = new List<Component>();   // 이번 붙이기에서 돌려준 순서
            public bool active;
        }
        private static readonly Dictionary<Component, int> age = new Dictionary<Component, int>(RefEq<Component>.I);
        // 붙인 순서 기록: 다시 쓴 효과는 게임오브젝트 안에서 원래 자리에 있으므로, 게임의 효과 찾기가 이 순서로 돌려준다
        private static readonly Dictionary<GameObject, List<Component>> order = new Dictionary<GameObject, List<Component>>(RefEq<GameObject>.I);
        private static readonly Dictionary<Component, int> logicalIndex = new Dictionary<Component, int>(RefEq<Component>.I);
        // 남겨 둔 것이 붙어 있거나 효과를 되돌려 쓴 게임오브젝트. 여기에 없으면 컴포넌트 순서가 원래 방식과 같아서 게임 호출을 그대로 쓴다.
        // (순서 기록은 처음 연 맵에서도 모든 타일에 생겨, 곡 중 타일마다 GetComponents 배열을 만들고 해시를 찾았다)
        private static readonly HashSet<GameObject> touched = new HashSet<GameObject>(RefEq<GameObject>.I);
        private static readonly Dictionary<GameObject, Slot> slots = new Dictionary<GameObject, Slot>(RefEq<GameObject>.I);
        private static readonly HashSet<ffxPlusBase> checkpointHeld = new HashSet<ffxPlusBase>(RefEq<ffxPlusBase>.I);
        internal static int Reused, Fresh, Dropped;

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly RefEq<T> I = new RefEq<T>();
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        // ── 종류별 정보 ──
        private sealed class Info { public bool ok; public string why; public Action<object> reset; public FieldInfo[] fields; }
        private static readonly Dictionary<Type, Info> infos = new Dictionary<Type, Info>();
        private static readonly string[] Messages = { "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnBecameVisible", "OnBecameInvisible",
            "OnGUI", "OnRenderObject", "OnWillRenderObject", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderImage", "OnValidate", "Reset", "OnApplicationFocus", "OnApplicationPause",
            "OnApplicationQuit", "OnTransformParentChanged", "OnTransformChildrenChanged", "OnCanvasGroupChanged", "OnRectTransformDimensionsChange", "OnAnimatorMove", "OnAnimatorIK", "OnAudioFilterRead" };

        private static Info GetInfo(Type t)
        {
            Info inf;
            if (infos.TryGetValue(t, out inf)) return inf;
            inf = new Info();
            try { Build(t, inf); }
            catch (Exception ex) { inf.ok = false; inf.why = ex.Message; }
            infos[t] = inf;
            if (!inf.ok && Edition.Dev) Main.Entry.Logger.Log("[효과 재사용] " + t.Name + " 는 원래대로 지움: " + inf.why);
            return inf;
        }

        private static void Build(Type t, Info inf)
        {
            const BindingFlags D = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            if (t.IsAbstract || t.ContainsGenericParameters) { inf.why = "추상"; return; }
            var ctor = t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor == null) { inf.why = "기본 생성자 없음"; return; }
            var fields = new List<FieldInfo>();
            for (var x = t; x != null && x != typeof(MonoBehaviour); x = x.BaseType)
            {
                foreach (var m in x.GetMethods(D))
                {
                    if (Array.IndexOf(Messages, m.Name) >= 0 && !m.IsStatic && !EmptyBody(m)) { inf.why = "유니티 메시지 " + x.Name + "." + m.Name; return; }
                    if (m.Name == "Awake" && !m.IsStatic && m.GetParameters().Length == 0)
                    {
                        // 유니티가 부를 Awake 가 ffxPlusBase.Awake 를 덮어쓴 것이어야 가상 호출로 같은 함수를 부를 수 있다
                        var bd = m.GetBaseDefinition();
                        if (bd == null || bd.DeclaringType != typeof(ffxPlusBase)) { inf.why = "Awake 가 가상 함수가 아님 (" + x.Name + ")"; return; }
                    }
                    if (UsesCoroutine(m)) { inf.why = "코루틴/Invoke (" + x.Name + "." + m.Name + ")"; return; }
                }
                foreach (var f in x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) fields.Add(f);
            }
            inf.fields = fields.ToArray();
            // 되돌리기: 필드 전부 기본값 -> 생성자 다시 실행
            var dm = new DynamicMethod("SF_FfxReset_" + t.Name, typeof(void), new[] { typeof(object) }, typeof(FfxReuse), true);
            var il = dm.GetILGenerator();
            var loc = il.DeclareLocal(t);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, t); il.Emit(OpCodes.Stloc, loc);
            foreach (var f in fields)
            {
                il.Emit(OpCodes.Ldloc, loc);
                if (f.FieldType.IsValueType) { il.Emit(OpCodes.Ldflda, f); il.Emit(OpCodes.Initobj, f.FieldType); }
                else { il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Stfld, f); }
            }
            il.Emit(OpCodes.Ldloc, loc); il.Emit(OpCodes.Call, ctor);
            il.Emit(OpCodes.Ret);
            inf.reset = (Action<object>)dm.CreateDelegate(typeof(Action<object>));
            inf.ok = true;
        }

        private static readonly MethodInfo[] CoroutineCalls = BuildCoroutineCalls();
        private static MethodInfo[] BuildCoroutineCalls()
        {
            var l = new List<MethodInfo>();
            foreach (var m in typeof(MonoBehaviour).GetMethods(BindingFlags.Instance | BindingFlags.Public))
                if (m.Name == "StartCoroutine" || m.Name == "Invoke" || m.Name == "InvokeRepeating") l.Add(m);
            return l.ToArray();
        }
        private static bool UsesCoroutine(MethodBase m)
        {
            if (m.IsAbstract || m.GetMethodBody() == null) return false;
            foreach (var ins in PatchProcessor.ReadMethodBody(m))
                if (ins.Value is MethodInfo mi && Array.IndexOf(CoroutineCalls, mi) >= 0) return true;
            return false;
        }

        // 유니티 기본 클래스 생성자가 비어 있는지 (ldarg.0 / call base ctor / ret 만)
        private static bool BaseCtorsTrivial(out string why)
        {
            why = "";
            foreach (var t in new[] { typeof(MonoBehaviour), typeof(Behaviour), typeof(Component), typeof(UnityEngine.Object), typeof(ADOBase), typeof(ffxPlusBase) })
            {
                if (t == typeof(ffxPlusBase)) continue;   // 효과 클래스 자신은 다시 실행하는 것이 목적
                var c = t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (c == null) continue;
                var b = c.GetMethodBody();
                int len = b != null ? b.GetILAsByteArray().Length : 0;
                // ADOBase 는 필드 초기값이 있을 수 있어 길이를 보지 않는다(필드를 지운 뒤 다시 실행되므로 같다). 유니티 쪽만 본다.
                if (t != typeof(ADOBase) && len > 8) { why = t.Name + " 생성자 IL " + len + "바이트"; return false; }
            }
            return true;
        }

        // ── 설치 ──
        internal static void Install(Harmony h)
        {
            string why;
            if (!BaseCtorsTrivial(out why)) { Main.Entry.Logger.Log("[효과 재사용] 끔: " + why); return; }
            var tr = new HarmonyMethod(typeof(FfxReuse), nameof(Transpiler));
            int n = 0;
            var aetf = AccessTools.Method(typeof(scnGame), "ApplyEventsToFloors", new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) });
            h.Patch(aetf, prefix: new HarmonyMethod(typeof(FfxReuse), nameof(PassPrefix)), finalizer: new HarmonyMethod(typeof(FfxReuse), nameof(PassFinalizer)), transpiler: tr); n++;
            var targets = new List<MethodBase>
            {
                AccessTools.Method(typeof(scnGame), "ApplyEvent", new[] { typeof(LevelEvent), typeof(float), typeof(float), typeof(List<scrFloor>), typeof(float), typeof(int?) }),
                AccessTools.Method(typeof(scrLevelMaker), "ResetFloor"),
                AccessTools.Method(typeof(scnGame), "FinishCustomLevelLoading"), AccessTools.Method(typeof(scnGame), "SetFxPlusFromComponents"),
                AccessTools.Method(typeof(scnGame), "ResetScene"),
                AccessTools.Method(typeof(scnGame), "PrepVfx", new[] { typeof(List<scrFloor>), typeof(int), typeof(List<LevelEvent>), typeof(bool) }),
                AccessTools.Method(typeof(scrFloor), "Awake"), AccessTools.Method(typeof(scrFloor), "UpdateIconSprite"),
                AccessTools.Method(typeof(scrConductor), "PlayHitTimes"), AccessTools.Method(typeof(scrPlanet), "MoveToNextFloor"), AccessTools.Method(typeof(scrPlanet), "Die"),
            };
            var bf = AccessTools.TypeByName("scrButterfliesOnFail");
            if (bf != null) targets.Add(AccessTools.Method(bf, "Update"));
            // scrController.WaitForStartCo 는 코루틴이라 본문이 숨은 클래스의 MoveNext 에 있다
            foreach (var nt in typeof(scrController).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
                if (nt.Name.Contains("WaitForStartCo")) targets.Add(AccessTools.Method(nt, "MoveNext"));
            // ApplyEventsToFloors·ApplyEvent 안의 로컬 함수(ApplyCoreEventsToFloors 등)와 람다
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var holders = new List<Type> { typeof(scnGame) };
            holders.AddRange(typeof(scnGame).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public));
            int local = 0;
            foreach (var ht in holders)
                foreach (var m in ht.GetMethods(All))
                    if ((m.Name.Contains("<ApplyEventsToFloors>") || m.Name.Contains("<ApplyEvent>")) && !m.IsAbstract && !m.ContainsGenericParameters) { targets.Add(m); local++; }
            if (local == 0) { Main.Entry.Logger.Log("[효과 재사용] ApplyEventsToFloors 안의 로컬 함수를 못 찾음 - 끔"); h.Unpatch(aetf, HarmonyPatchType.All, h.Id); return; }
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] == null) { Main.Entry.Logger.Log("[효과 재사용] 대상 함수 " + i + "번 없음 - 끔"); h.Unpatch(aetf, HarmonyPatchType.All, h.Id); return; }
            foreach (var m in targets) { h.Patch(m, transpiler: tr); n++; }
            installed = true;
            if (Edition.Dev) Main.Entry.Logger.Log("[효과 재사용] 함수 " + n + "개, 바꾼 호출 " + replaced + "개");
        }

        private static int replaced;
        private static readonly MethodInfo goAdd = typeof(GameObject).GetMethod("AddComponent", Type.EmptyTypes);
        private static readonly MethodInfo goGet = typeof(GameObject).GetMethod("GetComponent", Type.EmptyTypes);
        private static readonly MethodInfo cGet = typeof(Component).GetMethod("GetComponent", Type.EmptyTypes);
        private static readonly MethodInfo goGets = typeof(GameObject).GetMethod("GetComponents", Type.EmptyTypes);
        private static readonly MethodInfo cGets = typeof(Component).GetMethod("GetComponents", Type.EmptyTypes);
        private static readonly MethodInfo cKids = typeof(Component).GetMethod("GetComponentsInChildren", Type.EmptyTypes);
        private static readonly MethodInfo destroyImm = typeof(UnityEngine.Object).GetMethod("DestroyImmediate", new[] { typeof(UnityEngine.Object) });

        private static MethodInfo Ours(string name, Type t) { return typeof(FfxReuse).GetMethod(name, BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(t); }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            bool destroyHere = __originalMethod.Name == "ResetFloor" || __originalMethod.Name == "ApplyEventsToFloors";
            foreach (var ins in instructions)
            {
                var mi = ins.operand as MethodInfo;
                if (mi != null && (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt))
                {
                    if (destroyHere && mi == destroyImm) { ins.opcode = OpCodes.Call; ins.operand = typeof(FfxReuse).GetMethod(nameof(Destroy)); replaced++; }
                    else if (mi.IsGenericMethod)
                    {
                        var def = mi.GetGenericMethodDefinition();
                        var t = mi.GetGenericArguments()[0];
                        if (typeof(ffxPlusBase).IsAssignableFrom(t) || t.IsAssignableFrom(typeof(ffxPlusBase)) && t != typeof(object))
                        {
                            MethodInfo rep = null;
                            if (def == goAdd) rep = typeof(ffxPlusBase).IsAssignableFrom(t) ? Ours(nameof(Add), t) : null;
                            else if (def == goGet) rep = Ours(nameof(GoGet), t);
                            else if (def == cGet) rep = Ours(nameof(CGet), t);
                            else if (def == goGets && __originalMethod.Name != "ApplyEventsToFloors" && __originalMethod.Name != "ResetFloor") rep = Ours(nameof(GoGets), t);
                            else if (def == cGets && __originalMethod.Name != "ApplyEventsToFloors" && __originalMethod.Name != "ResetFloor") rep = Ours(nameof(CGets), t);
                            else if (def == cKids) rep = Ours(nameof(CKids), t);
                            else if (def.DeclaringType != null && def.DeclaringType.Name == "GOExtensions" && def.Name == "GetOrAddComponent")
                            {
                                var p = def.GetParameters()[0].ParameterType;
                                rep = Ours(p == typeof(GameObject) ? nameof(GetOrAddGo) : nameof(GetOrAddB), t);
                            }
                            if (rep != null) { ins.opcode = OpCodes.Call; ins.operand = rep; replaced++; }
                        }
                    }
                }
                yield return ins;
            }
        }

        // ── 지우기 대신 남기기 (ResetFloor, ApplyEventsToFloors 의 지우기 반복) ──
        public static void Destroy(UnityEngine.Object o)
        {
            var f = o as ffxPlusBase;
            if (verifying && !ReferenceEquals(f, null) && pending.Contains(f)) return;   // (검증) 원래라면 이미 없는 것
            if (!Enabled || ForceOff || verifying || ReferenceEquals(f, null) || !GetInfo(f.GetType()).ok || checkpointHeld.Contains(f) || !ours.Contains(f))
            {
                if (!ReferenceEquals(f, null)) { pending.Remove(f); age.Remove(f); ours.Remove(f); }
                UnityEngine.Object.DestroyImmediate(o);
                return;
            }
            pending.Add(f);
            touched.Add(f.gameObject);
            Unlink(f);
            if (inPass)
            {
                var s = SlotOf(f.gameObject);
                List<ffxPlusBase> q;
                var t = f.GetType();
                if (!s.byType.TryGetValue(t, out q)) { q = new List<ffxPlusBase>(); s.byType[t] = q; }
                q.Add(f);
            }
        }

        // 원래라면 지워진 효과를 게임이 필드로 들고 있으면 "== null" 이 참이 된다. 남겨 둔 것은 살아 있어서 그 필드가 옛 효과를 계속 가리킨다.
        // scrFloor.setHitsound / setGameSound (ffxSetHitsound.Decode 가 넣고 scrConductor.PlayHitTimes 가 "!= null" 로 읽는다):
        // 히트사운드 이벤트를 옮기거나 지운 타일이 옛 히트사운드를 계속 써서 소리가 바뀌었다. 원래처럼 비운다.
        // (타일의 다른 효과 목록은 ApplyEventsToFloors·scrFloor.Reset 이 비운다)
        private static void Unlink(ffxPlusBase f)
        {
            var sh = f as ffxSetHitsound;
            if (ReferenceEquals(sh, null)) return;
            var fl = sh.GetComponent<scrFloor>();
            if (ReferenceEquals(fl, null)) return;
            if (ReferenceEquals(fl.setHitsound, sh)) fl.setHitsound = null;
            if (ReferenceEquals(fl.setGameSound, sh)) fl.setGameSound = null;
        }

        private static Slot SlotOf(GameObject go)
        {
            Slot s;
            if (slots.TryGetValue(go, out s)) return s;
            s = new Slot { active = go.activeInHierarchy };
            slots[go] = s;
            order[go] = s.logical;   // 이번 붙이기의 순서를 바로 쓴다 (붙이는 중에 게임이 찾아도 맞게)
            return s;
        }

        // ── 붙이기: 남겨 둔 것 중 같은 종류가 있으면 되돌려서 쓴다 ──
        public static T Add<T>(GameObject go) where T : Component
        {
            if (!inPass) return go.AddComponent<T>();
            var s = SlotOf(go);
            T r;
            List<ffxPlusBase> q;
            int idx;
            s.used.TryGetValue(typeof(T), out idx);
            if (s.active && s.byType.TryGetValue(typeof(T), out q) && idx < q.Count)
            {
                var c = q[idx];
                s.used[typeof(T)] = idx + 1;
                pending.Remove(c); age.Remove(c);
                GetInfo(typeof(T)).reset(c);
                if (!c.enabled) c.enabled = true;
                c.Awake();
                Reused++;
                touched.Add(go);
                r = (T)(Component)c;
            }
            else { r = go.AddComponent<T>(); Fresh++; }
            ours.Add(r);
            logicalIndex[r] = s.logical.Count;
            s.logical.Add(r);
            return r;
        }

        // ── 게임의 효과 찾기: 남겨 둔 것은 없는 것으로, 순서는 붙인 순서(원래 방식의 컴포넌트 순서)로 ──
        // 붙인 순서가 기록된 타일은 기록된 것을 그 순서대로 먼저, 기록 뒤에 게임이 따로 붙인 것은 원래 순서대로 뒤에 둔다
        // (원래 방식에서도 그것들은 나중에 붙어 뒤에 있다).
        private static bool Plain { get { return pending.Count == 0 && logicalIndex.Count == 0; } }
        public static T GoGet<T>(GameObject go) where T : Component
        {
            if (Plain) return go.GetComponent<T>();
            if (!touched.Contains(go)) { var q = go.GetComponent<T>(); if (Edition.Dev) CheckQuick(q, go); return q; }
            var a = GoGets<T>(go);
            return a.Length > 0 ? a[0] : null;
        }
        public static T CGet<T>(Component x) where T : Component
        {
            if (Plain) return x.GetComponent<T>();
            if (!touched.Contains(x.gameObject)) { var q = x.GetComponent<T>(); if (Edition.Dev) CheckQuick(q, x.gameObject); return q; }
            var a = GoGets<T>(x.gameObject);
            return a.Length > 0 ? a[0] : null;
        }
        // (개발자용) 빠른 길이 예전 길(배열 + 붙인 순서)과 같은 것을 돌려주는지
        internal static long QuickN, QuickDiff;
        private static void CheckQuick<T>(T q, GameObject go) where T : Component
        {
            QuickN++;
            var a = GoGets<T>(go);
            var old = a.Length > 0 ? a[0] : null;
            if (!ReferenceEquals(old, q)) { QuickDiff++; if (QuickDiff <= 5) Main.Entry.Logger.Log("[효과 재사용] (개발자용) 빠른 길이 다름: " + go.name + " " + typeof(T).Name + " " + (q == null ? "null" : q.GetType().Name) + " / " + (old == null ? "null" : old.GetType().Name)); }
        }
        public static T[] GoGets<T>(GameObject go) where T : Component { return Arrange(go.GetComponents<T>()); }
        public static T[] CGets<T>(Component x) where T : Component { return Arrange(x.GetComponents<T>()); }
        public static T[] CKids<T>(Component x) where T : Component { return Arrange(x.GetComponentsInChildren<T>()); }

        private static T[] Arrange<T>(T[] a) where T : Component
        {
            if (Plain || a.Length == 0) return a;
            // 빠른 길: 남겨 둔 것이 없고, 기록된 것이 이미 붙인 순서대로면(기록 밖의 것은 뒤에) 그대로 돌려준다 (곡 중 타일마다 불리므로 할당 없이)
            bool drop = false, sorted = true, sawPlain = false;
            int last = -1;
            foreach (var c in a)
            {
                if (pending.Contains(c)) { drop = true; break; }
                int li;
                if (logicalIndex.TryGetValue(c, out li)) { if (sawPlain || li < last) sorted = false; last = li; }
                else sawPlain = true;
            }
            if (!drop && sorted) return a;
            // 게임오브젝트마다 처음 나온 자리를 지키고, 그 안에서는 붙인 순서 -> 기록 밖의 것은 원래 순서
            var list = new List<KeyValuePair<long, T>>(a.Length);
            var firstSeen = new Dictionary<GameObject, int>(RefEq<GameObject>.I);
            for (int i = 0; i < a.Length; i++)
            {
                var c = a[i];
                if (pending.Contains(c)) continue;
                var go = c.gameObject;
                int g;
                if (!firstSeen.TryGetValue(go, out g)) { g = firstSeen.Count; firstSeen[go] = g; }
                int li;
                long inner = logicalIndex.TryGetValue(c, out li) ? li : (1L << 30) + i;
                list.Add(new KeyValuePair<long, T>(((long)g << 32) | inner, c));
            }
            list.Sort((p, q) => p.Key.CompareTo(q.Key));
            var r = new T[list.Count];
            for (int i = 0; i < r.Length; i++) r[i] = list[i].Value;
            return r;
        }
        public static T GetOrAddGo<T>(GameObject go) where T : Component
        {
            var c = GoGet<T>(go);
            return c != null ? c : Add<T>(go);
        }
        public static T GetOrAddB<T>(Behaviour b) where T : Component
        {
            var c = CGet<T>(b);
            return c != null ? c : Add<T>(b.gameObject);
        }

        // ── ApplyEventsToFloors 앞뒤 ──
        private static readonly Stopwatch passSw = new Stopwatch();
        private const int MaxAge = 8;   // 붙이기 8번 연속 안 쓰인 남겨 둔 효과는 지운다(편집을 되풀이하며 쌓이지 않게)
        public static void PassPrefix(List<scrFloor> floors)
        {
            if (verifying) return;
            inPass = Enabled && !ForceOff;
            slots.Clear();
            Reused = Fresh = Dropped = 0;
            passSw.Restart();
            if (!inPass) return;
            // 지워진 물체(타일째 없어진 것)는 목록에서 뺀다
            if (pending.Count > 0) pending.RemoveWhere(c => c == null);
            if (ours.Count > 0) ours.RemoveWhere(c => c == null);
            // 이번 붙이기가 다시 만들 타일의 순서 기록은 붙이면서 새로 쓴다 (나머지 타일 기록은 그대로)
            if (floors != null) foreach (var fl in floors) if (fl != null) order.Remove(fl.gameObject);
            // 표시는 남겨 둔 것이 붙은 오브젝트와 순서 기록이 남은 오브젝트 중 되돌려 쓴 적이 있는 것만 남긴다 (지워진 것은 뺀다)
            if (touched.Count > 0) touched.RemoveWhere(g => g == null);
            CleanLogical();
            checkpointHeld.Clear();
            var all = ADOBase.lm != null ? ADOBase.lm.listFloors : floors;
            if (all != null) foreach (var f in all) if (f != null && f.onCheckpointEffects.Count > 0) foreach (var e in f.onCheckpointEffects) if (!ReferenceEquals(e, null)) checkpointHeld.Add(e);
        }

        // 순서 기록에서 지워진 것·기록이 없어진 타일의 것을 뺀다
        private static void CleanLogical()
        {
            logicalIndex.Clear();
            var dead = new List<GameObject>();
            foreach (var kv in order)
            {
                if (kv.Key == null) { dead.Add(kv.Key); continue; }
                var l = kv.Value;
                for (int i = 0; i < l.Count; i++) if (l[i] != null) logicalIndex[l[i]] = i;
            }
            foreach (var g in dead) order.Remove(g);
        }

        public static Exception PassFinalizer(Exception __exception, object[] __args)
        {
            if (verifying) return __exception;
            bool was = inPass;
            inPass = false;
            // 쓰지 않은 것은 지우지 않고 남겨 둔다(게임에는 없는 것으로 보인다). 편집 화면의 효과 붙이기는 재생 때 쓰는 효과의 일부만 붙이므로
            // 여기서 지우면 다음 재생 때 다시 붙여야 한다. 다만 붙이기 MaxAge 번 연속 쓰이지 않으면 지운다.
            foreach (var s in slots.Values)
                foreach (var kv in s.byType)
                {
                    int used;
                    s.used.TryGetValue(kv.Key, out used);
                    for (int j = used; j < kv.Value.Count; j++)
                    {
                        var c = kv.Value[j];
                        if (!pending.Contains(c)) continue;
                        int a;
                        age.TryGetValue(c, out a);
                        if (++a >= MaxAge || c == null) { pending.Remove(c); age.Remove(c); ours.Remove(c); if (c != null) { UnityEngine.Object.DestroyImmediate(c); Dropped++; } }
                        else age[c] = a;
                    }
                }
            slots.Clear();
            passSw.Stop();
            if (was && (Reused + Fresh > 1000 || Edition.Dev))
                Main.Entry.Logger.Log("[효과 재사용] 다시 씀 " + Reused + "개, 새로 붙임 " + Fresh + "개, 지움 " + Dropped + "개, 남겨 둔 것 " + pending.Count + "개, 순서가 다를 수 있는 오브젝트 " + touched.Count + "개 | " + passSw.ElapsedMilliseconds + "ms" + (Edition.Dev ? " | (개발자용) 빠른 찾기 " + QuickN + "번 중 다름 " + QuickDiff : ""));
            if (was && Edition.Dev && pending.Count > 0)
            {
                // (개발자용) 남겨 둔 것이 무엇인지: 종류별 수, 꺼진 게임오브젝트에 붙은 수
                var byT = new Dictionary<string, int>(); int inactive = 0;
                foreach (var c in pending) { if (c == null) continue; var k = c.GetType().Name; int v; byT.TryGetValue(k, out v); byT[k] = v + 1; if (!c.gameObject.activeInHierarchy) inactive++; }
                var l = new List<KeyValuePair<string, int>>(byT); l.Sort((a, b) => b.Value.CompareTo(a.Value));
                var sb = new System.Text.StringBuilder("[효과 재사용] 남겨 둔 것: 꺼진 물체에 " + inactive + "개 |");
                for (int i = 0; i < l.Count && i < 8; i++) sb.Append(' ').Append(l[i].Key).Append('=').Append(l[i].Value);
                Main.Entry.Logger.Log(sb.ToString());
            }
            if (was && Verify && __exception == null) VerifyPass(__args);
            return __exception;
        }

        // 체크포인트 조건 목록이 바뀔 수 있는 타일 다시 만들기(나가기 등) 앞에서도 목록을 새로 본다
        public static void MakeLevelPrefix()
        {
            checkpointHeld.Clear();
            var lm = ADOBase.lm;
            if (lm == null || lm.listFloors == null) return;
            foreach (var f in lm.listFloors) if (f != null && f.onCheckpointEffects.Count > 0) foreach (var e in f.onCheckpointEffects) if (!ReferenceEquals(e, null)) checkpointHeld.Add(e);
        }
        internal static void InstallMakeLevel(Harmony h)
        {
            if (!installed) return;
            h.Patch(AccessTools.Method(typeof(scrLevelMaker), "MakeLevel"), prefix: new HarmonyMethod(typeof(FfxReuse), nameof(MakeLevelPrefix)));
        }

        // ── (개발자용) 검증: 같은 프레임에 원래 방식으로 다시 해서 비교 ──
        private static void VerifyPass(object[] args)
        {
            var floors = (List<scrFloor>)args[0];
            var sw = Stopwatch.StartNew();
            var a = Snap(floors);
            verifying = true;
            try
            {
                if (scrDecorationManager.instance != null) scrDecorationManager.instance.ResetDecorationHitboxEvents();
                scnGame.ApplyEventsToFloors(floors, (LevelData)args[1], (scrLevelMaker)args[2], (List<LevelEvent>)args[3]);
            }
            finally { verifying = false; }
            var b = Snap(floors);
            int comps = 0, diff = 0;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < a.Count; i++)
            {
                var la = a[i]; var lb = b[i];
                if (la.Count != lb.Count) { diff++; if (diff <= 5) sb.Append(" | 타일 " + i + " 효과 수 " + la.Count + " 대 " + lb.Count); continue; }
                for (int k = 0; k < la.Count; k++)
                {
                    comps++;
                    string d = CompareComp(la[k], lb[k]);
                    if (d != null) { diff++; if (diff <= 8) sb.Append(" | 타일 " + i + " #" + k + " " + d); }
                }
            }
            Main.Entry.Logger.Log("[효과 재사용 검증] 효과 " + comps + "개 중 원래 방식과 다른 것 " + diff + "개" + sb + " | 검증 " + sw.ElapsedMilliseconds + "ms");
        }

        // 본문이 ret 하나뿐인 함수 (빈 OnDestroy 등): 불려도 아무 일도 없다
        private static bool EmptyBody(MethodBase m)
        {
            var b = m.GetMethodBody();
            if (b == null) return false;
            var il = b.GetILAsByteArray();
            return il.Length == 1 && il[0] == 0x2A;
        }

        // 스냅샷은 찍는 순간 깊게 문자열로 만든다(원래 방식으로 다시 할 때 앞 결과가 가리키던 것이 지워지거나 바뀌므로).
        // 효과 참조는 "타일#순번", 컴포넌트는 "종류@게임오브젝트", 그 밖의 유니티 물체는 id. 같은 물체는 한 번만 풀어 쓴다.
        private sealed class CompSnap { public Type type; public long[] vals; }
        private static readonly Dictionary<Component, string> canon = new Dictionary<Component, string>(RefEq<Component>.I);
        private static readonly Dictionary<object, string> memo = new Dictionary<object, string>(RefEq<object>.I);
        private static List<List<CompSnap>> Snap(List<scrFloor> floors)
        {
            canon.Clear(); memo.Clear();
            var live = new List<ffxPlusBase[]>();
            for (int i = 0; i < floors.Count; i++)
            {
                var fl = floors[i];
                var cs = fl != null ? GoGets<ffxPlusBase>(fl.gameObject) : new ffxPlusBase[0];   // 게임이 보는 대로 (남겨 둔 것은 빼고, 붙인 순서로)
                for (int k = 0; k < cs.Length; k++) canon[cs[k]] = i + "#" + k;
                live.Add(cs);
            }
            var all = new List<List<CompSnap>>();
            foreach (var cs in live)
            {
                var l = new List<CompSnap>();
                foreach (var c in cs)
                {
                    var t = c.GetType();
                    var inf = GetInfo(t);
                    var fields = inf.fields ?? AllFields(t);
                    var vals = new long[fields.Length];
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var sb = new System.Text.StringBuilder();
                        Ser(sb, fields[j].GetValue(c), 0, new HashSet<object>(RefEq<object>.I));
                        vals[j] = Hash(sb);   // 메모리를 아끼려고 필드마다 해시만 남긴다 (Arche 11만 개를 문자열로 들고 있다가 메모리가 모자랐다)
                    }
                    l.Add(new CompSnap { type = t, vals = vals });
                }
                all.Add(l);
            }
            memo.Clear();
            return all;
        }
        private static FieldInfo[] AllFields(Type t)
        {
            var l = new List<FieldInfo>();
            for (var x = t; x != null && x != typeof(MonoBehaviour); x = x.BaseType)
                l.AddRange(x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            return l.ToArray();
        }

        private static void Ser(System.Text.StringBuilder sb, object v, int depth, HashSet<object> path)
        {
            if (v == null) { sb.Append("null"); return; }
            var t = v.GetType();
            if (v is string s) { sb.Append('"').Append(s).Append('"'); return; }
            if (v is float f) { sb.Append(f.ToString("R")); return; }
            if (v is double d) { sb.Append(d.ToString("R")); return; }
            if (t.IsPrimitive || t.IsEnum || v is decimal) { sb.Append(v); return; }
            if (v is IntPtr || v is UIntPtr) { sb.Append("ptr"); return; }   // 네이티브 포인터 값은 물체마다 다르다
            if (v is AnimationCurve ac)
            {
                sb.Append("Curve[").Append(ac.preWrapMode).Append(',').Append(ac.postWrapMode);
                foreach (var k in ac.keys) sb.Append(';').Append(k.time.ToString("R")).Append(',').Append(k.value.ToString("R")).Append(',').Append(k.inTangent.ToString("R")).Append(',').Append(k.outTangent.ToString("R")).Append(',').Append(k.inWeight.ToString("R")).Append(',').Append(k.outWeight.ToString("R")).Append(',').Append(k.weightedMode);
                sb.Append(']'); return;
            }
            if (v is Gradient gr)
            {
                sb.Append("Grad[").Append(gr.mode);
                foreach (var k in gr.colorKeys) sb.Append(";c").Append(k.time.ToString("R")).Append(',').Append(k.color);
                foreach (var k in gr.alphaKeys) sb.Append(";a").Append(k.time.ToString("R")).Append(',').Append(k.alpha.ToString("R"));
                sb.Append(']'); return;
            }
            if (v is UnityEngine.Object uo)
            {
                if (uo is Component comp)
                {
                    string k;
                    if (canon.TryGetValue(comp, out k)) { sb.Append("ffx:").Append(k); return; }
                    if (comp == null) { sb.Append("dead:").Append(t.Name); return; }
                    sb.Append("C:").Append(t.Name).Append('@').Append(comp.gameObject.GetInstanceID());
                    return;
                }
                if (uo == null) { sb.Append("dead:").Append(t.Name); return; }
                sb.Append("U:").Append(t.Name).Append(':').Append(uo.GetInstanceID());
                return;
            }
            if (depth > 4) { sb.Append('~').Append(t.Name); return; }
            if (!t.IsValueType)
            {
                string m;
                if (memo.TryGetValue(v, out m)) { sb.Append(m); return; }   // 같은 물체는 내용 해시 토큰으로
                if (!path.Add(v)) { sb.Append("cycle"); return; }
            }
            var inner = new System.Text.StringBuilder();
            if (v is Delegate del)
            {
                foreach (var x in del.GetInvocationList())
                {
                    inner.Append("D:").Append(x.Method.DeclaringType != null ? x.Method.DeclaringType.Name : "?").Append('.').Append(x.Method.Name).Append('(');
                    Ser(inner, x.Target, depth + 1, path);
                    inner.Append(')');
                }
            }
            else if (v is IDictionary dict)
            {
                var items = new List<string>();
                foreach (DictionaryEntry e in dict)
                {
                    var kb = new System.Text.StringBuilder();
                    Ser(kb, e.Key, depth + 1, path); kb.Append("=>"); Ser(kb, e.Value, depth + 1, path);
                    items.Add(kb.ToString());
                }
                items.Sort(StringComparer.Ordinal);
                inner.Append('{').Append(string.Join(",", items.ToArray())).Append('}');
            }
            else if (v is IEnumerable en)
            {
                bool set = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>);
                var items = new List<string>();
                foreach (var x in en) { var ib = new System.Text.StringBuilder(); Ser(ib, x, depth + 1, path); items.Add(ib.ToString()); }
                if (set) items.Sort(StringComparer.Ordinal);
                inner.Append('[').Append(string.Join(",", items.ToArray())).Append(']');
            }
            else
            {
                inner.Append(t.Name).Append('{');
                foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    inner.Append(fi.Name).Append('=');
                    Ser(inner, fi.GetValue(v), depth + 1, path);
                    inner.Append(';');
                }
                inner.Append('}');
            }
            if (!t.IsValueType) { path.Remove(v); string tok = "#" + Hash(inner).ToString("x16") + ":" + inner.Length; memo[v] = tok; sb.Append(tok); return; }
            sb.Append(inner);
        }

        private static long Hash(System.Text.StringBuilder s)
        {
            ulong h = 14695981039346656037UL;   // FNV-1a 64
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 1099511628211UL; }
            return (long)h;
        }

        private static string CompareComp(CompSnap a, CompSnap b)
        {
            if (a.type != b.type) return "종류 " + a.type.Name + " 대 " + b.type.Name;
            var fields = GetInfo(a.type).fields ?? AllFields(a.type);
            for (int j = 0; j < fields.Length; j++)
                if (a.vals[j] != b.vals[j]) return a.type.Name + "." + fields[j].Name + " 다름";
            return null;
        }
    }
}
