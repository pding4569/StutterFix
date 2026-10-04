using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using ADOFAI;
using HarmonyLib;

namespace StutterFix
{
    // 맵 파일 읽기의 "이벤트로 바꾸기"를 여러 코어에서.
    //
    // LevelData.Decode 는 맵 파일의 이벤트(actions)와 장식(decorations)마다 new LevelEvent(사전)을 차례로 만든다. Arche(이벤트 13만 7천 개)
    // 에서 3.85초, 맵 열기 24초의 16%. 이벤트 하나를 만드는 일(LevelEvent.Decode)은 그 이벤트의 사전만 읽고 고치며(FixDefaultValues),
    // 나머지는 게임의 표(GCS.levelEventsInfo, 속성 정보)를 읽기만 한다. 순서에 따라 번호를 매기거나 다른 이벤트를 보지 않는다.
    // 메인 스레드에서만 읽어야 할 수 있는 값은 둘(LevelEvent.enableProEvents: 설정·에디터 여부, LevelEventInfo.taroDLCCheck: DLC 설치 여부)
    // 이고 맵을 여는 동안 바뀌지 않으므로, 해석 직전 메인 스레드에서 구해 두고 그동안은 그 값을 쓴다(LevelEvent.Decode 안의 두 호출만 바꿈).
    // 이벤트들은 작업 스레드가 미리 만들고, 게임의 원래 반복문은 원래 순서대로 new LevelEvent 대신 미리 만든 것을 받는다(Take).
    // 그래서 이벤트 목록·장식 목록과 그 순서, 활성 검사, 나머지 처리는 원래 코드 그대로다.
    // 미리 만들다 예외가 난 이벤트는 넘기지 않아 원래 반복문이 그 자리에서 직접 만든다(같은 예외가 같은 자리에서 난다).
    // 다른 모드가 이 함수들을 고쳐 두었으면 하지 않는다. 개발자용은 미리 만든 것과 차례로 다시 만든 것을 비교한다.
    internal static class ParallelDecode
    {
        internal static bool Enabled = true;
        private const int MinEvents = 4000;   // 이보다 적으면 그냥 차례로 (HELLO 2026: 1만 9천 개 0.5초)

        [ThreadStatic] internal static bool OnWorker;   // 작업 스레드: 창 멈춤 판정용 진행 알림을 건너뛴다(WindowGhost.TickEvery)
        private static volatile bool active;
        private static bool proCached, officialCached, installedCached;
        private static Dictionary<object, LevelEvent> made;
        private static int installedPatches;
        internal static string Last = "";

        internal static void Install(Harmony h)
        {
            try
            {
                var ld = typeof(LevelData);
                MethodInfo decode = null;
                foreach (var m in ld.GetMethods(AccessTools.all))
                    if (m.Name == "Decode" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Dictionary<string, object>)) decode = m;
                var evDecode = AccessTools.Method(typeof(LevelEvent), "Decode", new[] { typeof(Dictionary<string, object>), typeof(string), typeof(bool) });
                if (decode == null || evDecode == null) { Main.Entry.Logger.Log("[맵 파일 읽기] 여러 코어로 이벤트 만들기: 게임 코드 모양이 달라 끔"); return; }
                installedPatches = 0;
                h.Patch(decode, prefix: new HarmonyMethod(typeof(ParallelDecode), nameof(DecodePrefix)), transpiler: new HarmonyMethod(typeof(ParallelDecode), nameof(DecodeTranspiler)),
                    finalizer: new HarmonyMethod(typeof(ParallelDecode), nameof(DecodeFinalizer)));
                h.Patch(evDecode, transpiler: new HarmonyMethod(typeof(ParallelDecode), nameof(EventTranspiler)));
                if (installedPatches < 3) { Main.Entry.Logger.Log("[맵 파일 읽기] 여러 코어로 이벤트 만들기: 바꿀 곳이 모자라 끔 (" + installedPatches + ")"); Enabled = false; return; }
                Main.Entry.Logger.Log("[맵 파일 읽기] 여러 코어로 이벤트 만들기 설치");
            }
            catch (Exception ex) { Enabled = false; Main.Entry.Logger.Log("[맵 파일 읽기] 여러 코어로 이벤트 만들기 설치 실패: " + ex.Message); }
        }

        // LevelData.Decode 안의 new LevelEvent(Dictionary) -> Take(Dictionary)
        public static IEnumerable<CodeInstruction> DecodeTranspiler(IEnumerable<CodeInstruction> ins)
        {
            var ctor = AccessTools.Constructor(typeof(LevelEvent), new[] { typeof(Dictionary<string, object>) });
            var take = AccessTools.Method(typeof(ParallelDecode), nameof(Take));
            foreach (var c in ins)
            {
                if (c.opcode == OpCodes.Newobj && ReferenceEquals(c.operand, ctor)) { c.opcode = OpCodes.Call; c.operand = take; installedPatches++; }
                yield return c;
            }
        }

        // LevelEvent.Decode 안의 두 값 읽기 -> 해석 중이면 메인 스레드에서 구해 둔 값
        public static IEnumerable<CodeInstruction> EventTranspiler(IEnumerable<CodeInstruction> ins)
        {
            var pro = AccessTools.PropertyGetter(typeof(LevelEvent), "enableProEvents");
            var taro = AccessTools.PropertyGetter(typeof(LevelEventInfo), "taroDLCCheck");
            var myPro = AccessTools.Method(typeof(ParallelDecode), nameof(ProEvents));
            var myTaro = AccessTools.Method(typeof(ParallelDecode), nameof(TaroCheck));
            foreach (var c in ins)
            {
                if ((c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && ReferenceEquals(c.operand, pro)) { c.opcode = OpCodes.Call; c.operand = myPro; installedPatches++; }
                else if ((c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && ReferenceEquals(c.operand, taro)) { c.opcode = OpCodes.Call; c.operand = myTaro; installedPatches++; }
                yield return c;
            }
        }

        public static bool ProEvents() { return active ? proCached : LevelEvent.enableProEvents; }

        // 원래: isOfficialLevel 이면 true, 아니면 NeoCosmos(DLC) 설치면 true, 아니면 !taroDLC
        public static bool TaroCheck(LevelEventInfo info) { return active ? (officialCached || installedCached || !info.taroDLC) : info.taroDLCCheck; }

        public static LevelEvent Take(Dictionary<string, object> dict)
        {
            LevelEvent ev;
            var m = made;
            if (m != null && dict != null && m.TryGetValue(dict, out ev)) { m.Remove(dict); return ev; }
            return new LevelEvent(dict);
        }

        private static bool othersChecked, othersFound;
        private static bool OthersPatched()
        {
            if (othersChecked) return othersFound;
            othersChecked = true;
            var ms = new List<MethodBase>
            {
                AccessTools.Constructor(typeof(LevelEvent), new[] { typeof(Dictionary<string, object>) }),
                AccessTools.Method(typeof(LevelEvent), "Decode", new[] { typeof(Dictionary<string, object>), typeof(string), typeof(bool) }),
                AccessTools.Method(typeof(LevelEvent), "FixDefaultValues"),
                AccessTools.PropertyGetter(typeof(LevelEvent), "enableProEvents"),
                AccessTools.PropertyGetter(typeof(LevelEventInfo), "taroDLCCheck"),
            };
            foreach (var m in ms)
            {
                if (m == null) { othersFound = true; continue; }
                var info = Harmony.GetPatchInfo(m);
                if (info == null) continue;
                foreach (var o in info.Owners)
                    if (!o.StartsWith("StutterFix", StringComparison.OrdinalIgnoreCase)) { othersFound = true; Main.Entry.Logger.Log("[맵 파일 읽기] 여러 코어로 이벤트 만들기: 다른 모드(" + o + ")가 " + m.Name + " 을 고쳐 끔"); }
            }
            return othersFound;
        }

        private static Stopwatch sw;
        public static void DecodePrefix(Dictionary<string, object> dict)
        {
            made = null; active = false; Last = "";
            if (!Enabled || dict == null) return;
            try
            {
                object a, d;
                var actions = dict.TryGetValue("actions", out a) ? a as List<object> : null;
                var decos = dict.TryGetValue("decorations", out d) ? d as List<object> : null;
                int total = (actions != null ? actions.Count : 0) + (decos != null ? decos.Count : 0);
                if (total < MinEvents || OthersPatched()) return;

                // 메인 스레드에서: 해석 중 바뀌지 않는 값, 게임 표가 준비됐는지
                proCached = LevelEvent.enableProEvents;
                officialCached = ADOBase.isOfficialLevel;
                installedCached = NeoCosmosManager.instance != null && NeoCosmosManager.instance.installed;
                if (GCS.levelEventsInfo == null) return;

                var items = new List<Dictionary<string, object>>(total);
                if (actions != null) foreach (var o in actions) { var x = o as Dictionary<string, object>; if (x != null) items.Add(x); }
                if (decos != null) foreach (var o in decos) { var x = o as Dictionary<string, object>; if (x != null) items.Add(x); }
                var results = new LevelEvent[items.Count];
                sw = Stopwatch.StartNew();
                active = true;
                int next = 0, failed = 0, done = 0;
                int n = Math.Max(1, Math.Min(6, Environment.ProcessorCount - 1));
                var threads = new Thread[n];
                for (int t = 0; t < n; t++)
                {
                    threads[t] = new Thread(() =>
                    {
                        OnWorker = true;
                        try
                        {
                            while (true)
                            {
                                int i0 = Interlocked.Add(ref next, 256) - 256;
                                if (i0 >= items.Count) break;
                                int i1 = Math.Min(items.Count, i0 + 256);
                                for (int i = i0; i < i1; i++)
                                {
                                    try { results[i] = new LevelEvent(items[i]); }
                                    catch { Interlocked.Increment(ref failed); }   // 원래 반복문이 그 자리에서 직접 만든다(같은 예외)
                                }
                            }
                        }
                        finally { OnWorker = false; Interlocked.Increment(ref done); }
                    }) { IsBackground = true, Name = "StutterFix.Decode" + t };
                    threads[t].Start();
                }
                // 메인 스레드는 기다리며 창 멈춤 판정을 막는다
                while (Volatile.Read(ref done) < n) { WindowGhost.Tick(); Thread.Sleep(5); }
                active = false;
                var map = new Dictionary<object, LevelEvent>(items.Count, RefEq.Instance);
                for (int i = 0; i < items.Count; i++) if (results[i] != null) map[items[i]] = results[i];
                if (Edition.Dev) DevCompare(items, results);
                made = map;
                Last = string.Format("이벤트 {0}개 여러 코어({1}개)로 {2:F0}ms{3}", items.Count, n, sw.Elapsed.TotalMilliseconds, failed > 0 ? ", 실패해 원래대로 " + failed + "개" : "");
            }
            catch (Exception ex) { active = false; made = null; Main.Entry.Logger.Log("[맵 파일 읽기] 여러 코어로 이벤트 만들기 실패, 원래대로: " + ex.Message); }
        }

        public static Exception DecodeFinalizer(Exception __exception)
        {
            active = false;
            if (made != null && made.Count > 0 && __exception == null) Last += ", 쓰이지 않음 " + made.Count + "개";
            made = null;
            return __exception;
        }

        // 개발자용: 미리 만든 것과 같은 사전으로 차례로 다시 만든 것이 같은가(종류·타일·켜짐·데이터·끈 속성)
        private static void DevCompare(List<Dictionary<string, object>> items, LevelEvent[] results)
        {
            int checkedN = 0, diff = 0; string first = null;
            for (int i = 0; i < items.Count; i++)
            {
                if (results[i] == null || (i > 3000 && i % 37 != 0)) continue;
                LevelEvent b;
                try { b = new LevelEvent(items[i]); } catch { continue; }
                checkedN++;
                string why = Same(results[i], b);
                if (why != null) { diff++; if (first == null) first = "#" + i + " " + why; }
            }
            Main.Entry.Logger.Log(string.Format("[맵 파일 읽기 검증] 여러 코어로 만든 이벤트 {0}개를 차례로 다시 만든 것과 비교: 다름 {1}개{2}", checkedN, diff, first != null ? " (처음: " + first + ")" : ""));
        }

        private static readonly FieldInfo dataF = AccessTools.Field(typeof(LevelEvent), "data");
        private static string Same(LevelEvent a, LevelEvent b)
        {
            if (a.eventType != b.eventType) return "종류";
            if (a.floor != b.floor || a.active != b.active || a.visible != b.visible || a.locked != b.locked) return "타일·켜짐";
            if (!ReferenceEquals(a.info, b.info)) return "정보";
            var da = dataF.GetValue(a) as Dictionary<string, object>; var db = dataF.GetValue(b) as Dictionary<string, object>;
            if ((da == null) != (db == null) || (da != null && da.Count != db.Count)) return "데이터 수";
            if (da != null)
            {
                var ka = new List<string>(da.Keys); var kb = new List<string>(db.Keys);
                for (int k = 0; k < ka.Count; k++)
                {
                    if (ka[k] != kb[k]) return "데이터 순서 " + ka[k];
                    if (!ValueEq(da[ka[k]], db[kb[k]])) return "데이터 " + ka[k];
                }
            }
            if ((a.disabled == null) != (b.disabled == null) || (a.disabled != null && a.disabled.Count != b.disabled.Count)) return "끈 속성 수";
            if (a.disabled != null) foreach (var kv in a.disabled) { bool v; if (!b.disabled.TryGetValue(kv.Key, out v) || v != kv.Value) return "끈 속성 " + kv.Key; }
            return null;
        }

        private static bool ValueEq(object x, object y)
        {
            if (x == null || y == null) return x == null && y == null;
            if (ReferenceEquals(x, y) || x.Equals(y)) return true;
            var lx = x as System.Collections.IList; var ly = y as System.Collections.IList;
            if (lx != null && ly != null)
            {
                if (lx.Count != ly.Count) return false;
                for (int i = 0; i < lx.Count; i++) if (!ValueEq(lx[i], ly[i])) return false;
                return true;
            }
            var dx = x as System.Collections.IDictionary; var dy = y as System.Collections.IDictionary;
            if (dx != null && dy != null)
            {
                if (dx.Count != dy.Count) return false;
                foreach (System.Collections.DictionaryEntry e in dx) if (!dy.Contains(e.Key) || !ValueEq(e.Value, dy[e.Key])) return false;
                return true;
            }
            return x.GetType() == y.GetType() && x.ToString() == y.ToString();   // 직렬화 객체 등: 같은 문자열이면 같다고 본다
        }

        private sealed class RefEq : IEqualityComparer<object>
        {
            internal static readonly RefEq Instance = new RefEq();
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
    }
}
