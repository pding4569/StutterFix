using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 맵 열기: 이미지 장식 프리팹을 한 번에 여러 개 복제 (Object.InstantiateAsync(프리팹, 개수, 부모) + WaitForCompletion).
    //
    // scnGame.UpdateDecorationObjects 는 장식마다 scrDecorationManager.CreateDecoration 에서 Object.Instantiate(프리팹, 관리자) 를 한다.
    // 플레이어용 Arche(장식 2만 8,835개): 장식 만들기 8.8초 중 복제가 약 4.2초(하나 약 100us, 유니티 안에서 테두리·히트박스·메시 렌더러 등
    // 여러 오브젝트를 만든다). 같은 프리팹을 개수만큼 한 번에 복제하면 하나 77~98us(게임 안 시험).
    // 순서: 부모 밑 순서(형제 순서)가 원래와 같아야 한다. 그래서 장식 목록에서 "이미지 장식이 연달아 나오는 구간"만 한 번에 만들고
    // (사이의 꺼진 장식은 복제하지 않으므로 끊지 않음), 다른 프리팹(글자·파티클·오브젝트·프리팹 장식)을 복제하기 전에는 구간을 끝낸다.
    // 남은 것(예상과 달리 복제하지 않은 장식 몫)은 그 자리에서 지운다 -> 남은 오브젝트의 형제 순서는 원래와 같다.
    // 각 장식의 Awake 는 자기 컴포넌트만 읽으므로(디컴파일 확인) 복제가 먼저 몰려도 결과는 같다.
    // 개발자용: 구간마다 첫 오브젝트를 원래 방식으로 하나 더 만들어 컴포넌트 구성·켜짐·변환을 비교한다.
    internal static class DecoBatch
    {
        internal static bool Enabled = true;
        private const int MinRun = 64;

        private static bool active;
        private static scrDecorationManager mgr;
        private static GameObject imagePrefab;
        private static Dictionary<LevelEvent, int> indexOf;
        private static byte[] kind;          // 0 복제 안 함, 1 이미지 장식, 2 다른 프리팹
        private static int cur = -1;
        private static GameObject[] pool; private static int poolPos;
        internal static int Batches, Batched, Leftover, DevChecks, DevDiff; internal static string DevFirst;

        private static System.Reflection.PropertyInfo decorationsProp;
        private static FieldInfo decorationsField;

        internal static void Install(Harmony h)
        {
            try
            {
                var update = AccessTools.Method(typeof(scnGame), "UpdateDecorationObjects", new[] { typeof(bool) });
                var create = AccessTools.Method(typeof(scrDecorationManager), "CreateDecoration");
                decorationsProp = AccessTools.Property(typeof(scnGame), "decorations");
                decorationsField = decorationsProp == null ? AccessTools.Field(typeof(scnGame), "decorations") : null;
                if (update == null || create == null || (decorationsProp == null && decorationsField == null)) { Main.Entry.Logger.Log("[장식 한 번에 복제] 게임 코드 모양이 달라 끔"); return; }
                swapped = 0;
                h.Patch(create, prefix: new HarmonyMethod(typeof(DecoBatch), nameof(CreatePrefix)), transpiler: new HarmonyMethod(typeof(DecoBatch), nameof(CreateTranspiler)));
                if (swapped != 1) { Main.Entry.Logger.Log("[장식 한 번에 복제] 바꿀 곳이 " + swapped + "개라 끔"); Enabled = false; return; }
                h.Patch(update, prefix: new HarmonyMethod(typeof(DecoBatch), nameof(UpdatePrefix)) { priority = Priority.Last }, finalizer: new HarmonyMethod(typeof(DecoBatch), nameof(UpdateFinalizer)));
                Main.Entry.Logger.Log("[장식 한 번에 복제] 설치");
            }
            catch (Exception ex) { Enabled = false; Main.Entry.Logger.Log("[장식 한 번에 복제] 설치 실패: " + ex.Message); }
        }

        private static int swapped;
        public static IEnumerable<CodeInstruction> CreateTranspiler(IEnumerable<CodeInstruction> ins)
        {
            var my = AccessTools.Method(typeof(DecoBatch), nameof(Instantiate));
            foreach (var c in ins)
            {
                var m = c.operand as MethodInfo;
                if (c.opcode == OpCodes.Call && m != null && m.Name == "Instantiate" && m.DeclaringType == typeof(UnityEngine.Object) && m.IsGenericMethod
                    && m.GetGenericArguments()[0] == typeof(GameObject) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(Transform))
                { c.operand = my; swapped++; }
                yield return c;
            }
        }

        public static void UpdatePrefix(scnGame __instance, bool reloadDecorations)
        {
            active = false; pool = null; poolPos = 0; cur = -1; Batches = Batched = Leftover = 0;
            if (!Enabled || !reloadDecorations) return;
            try
            {
                mgr = scrDecorationManager.instance;
                imagePrefab = mgr != null ? mgr.prefab_visualDecoration : null;
                var src = (decorationsProp != null ? decorationsProp.GetValue(__instance, null) : decorationsField.GetValue(__instance)) as System.Collections.IEnumerable;
                var list = new List<LevelEvent>();
                if (src != null) foreach (var o in src) list.Add(o as LevelEvent);
                if (imagePrefab == null || list == null || list.Count < MinRun) return;
                indexOf = new Dictionary<LevelEvent, int>(list.Count);
                kind = new byte[list.Count];
                bool prefabAllowed = ADOBase.isUnityEditor || ADOBase.isOfficialLevel;
                for (int i = 0; i < list.Count; i++)
                {
                    var ev = list[i];
                    if (ev == null || indexOf.ContainsKey(ev)) continue;
                    indexOf[ev] = i;
                    if (!ev.active) continue;
                    // CreateDecoration 의 갈래와 같게
                    if (ev.eventType == LevelEventType.AddDecoration)
                    {
                        string text = ev["decorationImage"] as string;
                        if (ADOBase.IsNotAMikoSkipMandatorySprite(text)) continue;
                        bool prefab = text != null && text.StartsWith("prefab:", StringComparison.CurrentCultureIgnoreCase) && prefabAllowed;
                        kind[i] = prefab ? (byte)2 : (byte)1;
                    }
                    else if (ev.eventType == LevelEventType.AddText || ev.eventType == LevelEventType.AddParticle || ev.eventType == LevelEventType.AddObject) kind[i] = 2;
                }
                active = true;
            }
            catch (Exception ex) { active = false; Main.Entry.Logger.Log("[장식 한 번에 복제] 준비 실패, 원래대로: " + ex.Message); }
        }

        public static void CreatePrefix(LevelEvent levelEvent)
        {
            int i;
            cur = active && levelEvent != null && indexOf.TryGetValue(levelEvent, out i) ? i : -1;
        }

        public static GameObject Instantiate(GameObject original, Transform parent)
        {
            if (!active || cur < 0 || mgr == null || (object)parent != mgr.transform)
                return UnityEngine.Object.Instantiate(original, parent);
            if ((object)original == imagePrefab)
            {
                if (pool != null && poolPos < pool.Length) return pool[poolPos++];
                // 지금부터 다른 프리팹이 나오기 전까지의 이미지 장식 수
                int n = 0;
                for (int j = cur; j < kind.Length && kind[j] != 2; j++) if (kind[j] == 1 || j == cur) n++;
                if (n < 2) return UnityEngine.Object.Instantiate(original, parent);
                var op = UnityEngine.Object.InstantiateAsync(original, n, parent);
                op.WaitForCompletion();
                pool = op.Result; poolPos = 0; Batches++; Batched += n;
                if (Edition.Dev && Batches <= 3) DevCompare(original, parent, pool[0]);
                return pool[poolPos++];
            }
            EndRun();
            return UnityEngine.Object.Instantiate(original, parent);
        }

        // 구간 끝: 쓰지 않은 것은 지운다 (그 뒤에 복제하는 오브젝트가 원래 순서 자리에 오도록)
        private static void EndRun()
        {
            if (pool == null) return;
            for (int k = poolPos; k < pool.Length; k++) if (pool[k] != null) { UnityEngine.Object.DestroyImmediate(pool[k]); Leftover++; }
            pool = null; poolPos = 0;
        }

        public static Exception UpdateFinalizer(Exception __exception)
        {
            if (active)
            {
                try { EndRun(); } catch { }
                if (Batches > 0) Main.Entry.Logger.Log(string.Format("[장식 한 번에 복제] {0}번에 {1}개 (안 쓰고 지움 {2}개){3}", Batches, Batched, Leftover,
                    Edition.Dev ? string.Format(" | 원래 방식과 비교 {0}번 다름 {1}{2}", DevChecks, DevDiff, DevFirst != null ? " (" + DevFirst + ")" : "") : ""));
            }
            active = false; indexOf = null; kind = null; mgr = null; imagePrefab = null; cur = -1;
            return __exception;
        }

        // 블렌드 모드 스크립트가 오브젝트마다 재질을 새로 만들므로(원래 방식으로 둘을 만들어도 번호가 다르다) 이름·셰이더로 비교
        private static bool SameMat(Material x, Material y)
        {
            if (x == null || y == null) return x == null && y == null;
            return x == y || (x.name == y.name && x.shader == y.shader);
        }

        // 개발자용: 같은 프리팹을 원래 방식으로 하나 만들어 오브젝트 구성·켜짐·변환·컴포넌트 켜짐을 비교하고 지운다
        private static void DevCompare(GameObject original, Transform parent, GameObject got)
        {
            GameObject want = null;
            try
            {
                want = UnityEngine.Object.Instantiate(original, parent);
                // 루트 이름은 다르다("X(Clone)" / "X (Clone)") - CreateDecoration 이 곧바로 장식 이미지 이름으로 바꾸므로 보지 않는다
                var a = want.GetComponentsInChildren<Component>(true); var b = got.GetComponentsInChildren<Component>(true);
                DevChecks++;
                string why = null;
                if (a.Length != b.Length) why = "컴포넌트 수 " + a.Length + "/" + b.Length;
                for (int i = 0; why == null && i < a.Length; i++)
                {
                    if (a[i].GetType() != b[i].GetType()) { why = "형식 " + a[i].GetType().Name; break; }
                    if (a[i].gameObject.activeSelf != b[i].gameObject.activeSelf || a[i].gameObject.layer != b[i].gameObject.layer || (a[i].gameObject != want && a[i].gameObject.name != b[i].gameObject.name)) { why = string.Format("오브젝트 {0}/{1} 켜짐 {2}/{3} 레이어 {4}/{5}", a[i].gameObject.name, b[i].gameObject.name, a[i].gameObject.activeSelf, b[i].gameObject.activeSelf, a[i].gameObject.layer, b[i].gameObject.layer); break; }
                    var ta = a[i] as Transform; var tb = b[i] as Transform;
                    if (ta != null && (ta.localPosition != tb.localPosition || ta.localRotation != tb.localRotation || ta.localScale != tb.localScale)) { why = "변환 " + ta.name; break; }
                    var ba = a[i] as Behaviour; var bb = b[i] as Behaviour;
                    if (ba != null && ba.enabled != bb.enabled) { why = "켜짐 " + ba.GetType().Name; break; }
                    var ra = a[i] as Renderer; var rb = b[i] as Renderer;
                    if (ra != null && (ra.enabled != rb.enabled || ra.sortingOrder != rb.sortingOrder || !SameMat(ra.sharedMaterial, rb.sharedMaterial))) { why = string.Format("렌더러 {0}: 켜짐 {1}/{2} 순서 {3}/{4} 재질 {5}/{6}", ra.name, ra.enabled, rb.enabled, ra.sortingOrder, rb.sortingOrder, ra.sharedMaterial != null ? ra.sharedMaterial.name + "#" + ra.sharedMaterial.GetInstanceID() : "없음", rb.sharedMaterial != null ? rb.sharedMaterial.name + "#" + rb.sharedMaterial.GetInstanceID() : "없음"); break; }
                }
                if (why != null) { DevDiff++; if (DevFirst == null) DevFirst = why; }
            }
            catch (Exception ex) { DevDiff++; if (DevFirst == null) DevFirst = "비교 실패: " + ex.Message; }
            finally { if (want != null) UnityEngine.Object.DestroyImmediate(want); }
        }
    }
}
