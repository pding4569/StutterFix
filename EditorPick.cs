using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace StutterFix
{
    // 편집 화면에서 마우스를 누를 때 끊김 (큰 맵).
    //
    // scnEditor.ObjectsAtMouse(누를 때마다, 끌기 시작·타일 고르기)는 마우스 아래 물체를 찾으려고 맵의 모든 타일을 돌며
    // floor.transform.position 과 마우스 점의 거리를 잰다(가까운 타일에만 충돌 상자를 만들고 레이캐스트).
    // 2026-09-29 사용자 기록(93858타일 맵, 2.4.5): 화면을 조금씩 끌 때마다 61ms "게임 처리". 위치 읽기가 타일 하나 약 0.65us.
    // 그래서 반복의 대상만 바꾼다: 타일 목록 대신 "마우스 점에서 반경+여유 안의 타일"(원래 순서)을 돌게 한다. 반복 안의 거리 판정은
    // 게임 코드 그대로 다시 하므로, 후보가 진짜 가까운 타일을 모두 포함하기만 하면 결과가 같다.
    // 후보 찾기: 타일들의 Transform 을 TransformAccessArray 로 들고 있다가(타일 목록이 바뀌면 다시 만듦), 누를 때 유니티 잡으로
    // 위치를 한꺼번에 읽는다(여러 스레드). 지워진 타일이 목록에 있으면 원래처럼 반복이 만나게(예외까지 같게) 후보에 넣는다.
    // 개발자용: 누를 때마다 원래 방식(모든 타일 transform.position)으로 가까운 타일을 구해 후보가 빠뜨린 것이 있는지 센다.
    internal static class EditorPick
    {
        internal static bool Enabled = true;
        internal const int MinFloors = 2000;
        private const float Margin = 0.25f;   // 반경 여유 (월드 단위, 타일 반경 약 0.85)
        internal static long Calls, Fast, Missed, Checks;
        internal static double LastMs;
        private static bool broken;

        private static AccessTools.FieldRef<scnEditor, Camera> camRef;

        internal static void Install(Harmony h)
        {
            var m = AccessTools.Method(typeof(scnEditor), "ObjectsAtMouse");
            var get = AccessTools.PropertyGetter(typeof(scnEditor), "floors");
            camRef = AccessTools.FieldRefAccess<scnEditor, Camera>("camera");
            if (m == null || get == null) { Main.Entry.Logger.Log("[편집 화면 클릭] 게임 코드 모양이 달라 쓰지 않음"); return; }
            int found = 0;
            var tr = new HarmonyMethod(AccessTools.Method(typeof(EditorPick), nameof(Transpiler)));
            getFloors = get;
            h.Patch(m, transpiler: tr);
            found = patched;
            if (found != 1) { h.Unpatch(m, HarmonyPatchType.Transpiler, h.Id); Main.Entry.Logger.Log("[편집 화면 클릭] 타일 반복을 " + found + "곳 찾음 (1곳이어야 함) - 쓰지 않음"); return; }
            // 준비(TransformAccessArray 만들기, 9만 타일 약 170ms)는 첫 클릭이 아니라 어차피 멈추는 순간에 미리 한다:
            // 편집 화면의 타일 다시 만들기(scnEditor.RemakePath, 맵 열기·편집) 끝과 편집으로 나가기 끝. 재생 시작 안의 것은 건너뛴다.
            var pre = new HarmonyMethod(AccessTools.Method(typeof(EditorPick), nameof(Prebuild)));
            foreach (var name in new[] { "RemakePath", "SwitchToEditMode" })
                foreach (var mm in typeof(scnEditor).GetMethods(AccessTools.all))
                    if (mm.Name == name && mm.DeclaringType == typeof(scnEditor) && !mm.IsAbstract) h.Patch(mm, finalizer: pre);
            if (Edition.Dev) Main.Entry.Logger.Log("[편집 화면 클릭] 설치: 마우스 근처 타일만 돌기");
        }

        public static Exception Prebuild(Exception __exception)
        {
            if (__exception != null || !Enabled || broken || ExitFix.PlayStarting) return __exception;
            try
            {
                var ed = scnEditor.instance;
                var floors = ed != null && ADOBase.lm != null ? ADOBase.lm.listFloors : null;
                if (floors == null || floors.Count < MinFloors) return __exception;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (Ensure(floors) && Edition.Dev) Main.Entry.Logger.Log(string.Format("[편집 화면 클릭] 미리 준비 {0:F0}ms (타일 {1}개)", Ms(t0, System.Diagnostics.Stopwatch.GetTimestamp()), floors.Count));
            }
            catch (Exception ex) { broken = true; Drop(); Main.Entry.Logger.Log("[편집 화면 클릭] 준비 실패, 원래대로: " + ex.Message); }
            return __exception;
        }

        // 타일 목록에 맞는 준비가 되어 있게. 새로 만들었으면 true. 지워진 타일이 섞여 있으면 준비하지 않는다(have=false).
        private static bool Ensure(List<scrFloor> floors)
        {
            int n = floors.Count;
            bool same = have && cached.Length == n;
            if (same) for (int i = 0; i < n; i++) if (!ReferenceEquals(cached[i], floors[i])) { same = false; break; }
            if (same) return false;
            Drop();
            var ts = new Transform[n];
            var c = new scrFloor[n];
            for (int i = 0; i < n; i++)
            {
                var f = floors[i];
                if (f == null) return false;
                c[i] = f;
                ts[i] = f.transform;
            }
            cached = c;
            taa = new TransformAccessArray(ts);
            pos = new NativeArray<Vector3>(n, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            have = true;
            return true;
        }

        private static MethodInfo getFloors;
        private static int patched;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var getEnum = AccessTools.Method(typeof(List<scrFloor>), "GetEnumerator");
            var near = AccessTools.Method(typeof(EditorPick), nameof(Near));
            patched = 0;
            for (int i = 0; i + 1 < list.Count; i++)
            {
                // get_floors() 바로 뒤가 GetEnumerator() 인 곳 = foreach (scrFloor floor in floors)
                if (list[i].operand as MethodInfo == getFloors && list[i + 1].operand as MethodInfo == getEnum)
                {
                    list.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                    list.Insert(i + 2, new CodeInstruction(OpCodes.Call, near));
                    patched++;
                    i += 2;
                }
            }
            return list;
        }

        private struct PosJob : IJobParallelForTransform
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> pos;
            public void Execute(int index, TransformAccess t) { pos[index] = t.position; }
        }

        private static scrFloor[] cached = new scrFloor[0];
        private static TransformAccessArray taa;
        private static NativeArray<Vector3> pos;
        private static bool have;
        private static readonly List<scrFloor> result = new List<scrFloor>();
        private static Vector3[] buf = new Vector3[0];
        // 타일(MonoBehaviour)이 지워졌는지: 유니티의 == null 과 같은 판단(m_CachedPtr 가 0)을 필드로 바로 읽는다 (9만 번 부르면 약 4ms 였다)
        private static readonly AccessTools.FieldRef<UnityEngine.Object, IntPtr> ptrRef = PtrRef();
        private static AccessTools.FieldRef<UnityEngine.Object, IntPtr> PtrRef()
        {
            try { var f = AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr"); return f != null && f.FieldType == typeof(IntPtr) ? AccessTools.FieldRefAccess<UnityEngine.Object, IntPtr>(f) : null; }
            catch { return null; }
        }

        private static void Drop()
        {
            if (have) { try { taa.Dispose(); } catch { } try { pos.Dispose(); } catch { } }
            have = false; cached = new scrFloor[0];
        }

        // 원래 목록 대신 돌 목록 (같은 List<scrFloor> 타입, 원래 순서)
        public static List<scrFloor> Near(List<scrFloor> floors, scnEditor ed)
        {
            if (!Enabled || broken || floors == null || floors.Count < MinFloors || ed == null) return floors;
            Calls++;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var cam = camRef(ed);
                if (cam == null) return floors;
                Vector2 v = cam.ScreenToWorldPoint(Input.mousePosition).xy();
                int n = floors.Count;
                Ensure(floors);
                if (!have) return floors;   // 지워진 타일이 섞여 있으면 원래대로 (원래 반복이 그대로 만나게)
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                new PosJob { pos = pos }.Schedule(taa).Complete();
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                float r = scrFloor.LongDimensions.magnitude + Margin;
                float r2 = r * r;
                result.Clear();
                if (buf.Length < n) buf = new Vector3[n];
                pos.CopyTo(buf);   // 관리 배열로 한 번에 (NativeArray 인덱서를 9만 번 부르지 않게)
                var b = buf; var c = cached;
                for (int i = 0; i < n; i++)
                {
                    var f = c[i];
                    // 지워진 타일(드묾)은 위치가 없으므로 원래처럼 반복이 만나게 넣는다 (원래 반복의 예외까지 같게)
                    if (ptrRef != null ? ptrRef(f) == IntPtr.Zero : f == null) { result.Add(f); continue; }
                    var p = b[i];
                    float ex = p.x - v.x, ey = p.y - v.y;
                    if (ex * ex + ey * ey <= r2) result.Add(f);
                }
                Fast++;
                LastMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (Edition.Dev) Check(floors, v);   // 시간 재기 뒤에 (원래 방식 전체 훑기라 느림)
                if (Edition.Dev && (Fast <= 3 || LastMs > 25)) Main.Entry.Logger.Log(string.Format("[편집 화면 클릭] 타일 {0}개 중 후보 {1}개, {2:F2}ms (확인 {3:F2}, 잡 {4:F2}, 고르기 {5:F2}, 작업 스레드 {6})", n, result.Count, LastMs, Ms(t0, t1), Ms(t1, t2), Ms(t2, System.Diagnostics.Stopwatch.GetTimestamp()), Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount));
                return result;
            }
            catch (Exception ex)
            {
                broken = true; Drop();
                Main.Entry.Logger.Log("[편집 화면 클릭] 실패, 원래대로 (이번 실행 동안): " + ex.Message);
                return floors;
            }
        }

        private static double Ms(long a, long b) { return (b - a) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }

        // (개발자용) 원래 방식으로 가까운 타일을 구해 후보에 다 있는지
        private static void Check(List<scrFloor> floors, Vector2 v)
        {
            float mag = scrFloor.LongDimensions.magnitude;
            var set = new HashSet<scrFloor>(result);
            int miss = 0;
            foreach (var f in floors)
                if (f != null && Vector2.Distance(f.transform.position.xy(), v) <= mag && !set.Contains(f)) miss++;
            Checks++; Missed += miss;
            if (miss > 0) Main.Entry.Logger.Log("[편집 화면 클릭 검증] 후보가 빠뜨린 가까운 타일 " + miss + "개");
        }
    }
}
