using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (개발자용) 필터 이벤트가 언제 무엇을 켜고 껐는지 최근 것만 기억해 둔다.
    // GPU 과부하 끊김이 났을 때 바로 앞 몇 프레임의 필터 변화를 같이 적어, 어떤 필터가 원인인지 좁힌다.
    // (69.4초의 200ms: 필터 셰이더를 미리 데워도 같은 자리에서 144ms 가 남았다)
    internal static class FilterTrace
    {
        private struct Rec { public int Frame; public string What; }
        private static readonly List<Rec> recent = new List<Rec>();
        private static FieldInfo advName, advOn, plusFilter, plusOn;

        internal static void Install(Harmony h)
        {
            var adv = AccessTools.TypeByName("ffxSetFilterAdvancedPlus");
            var plus = AccessTools.TypeByName("ffxSetFilterPlus");
            if (adv != null)
            {
                advName = AccessTools.Field(adv, "filterName"); advOn = AccessTools.Field(adv, "enableFilter");
                var m = AccessTools.DeclaredMethod(adv, "StartEffect");   // 직접 선언하지 않은 판(부모 ffxPlusBase 것)은 못 걸고, 걸면 모든 효과에 걸린다
                if (m != null) h.Patch(m, prefix: new HarmonyMethod(typeof(FilterTrace), nameof(Adv)), postfix: new HarmonyMethod(typeof(FilterTrace), nameof(AdvPost)));
                else Main.Entry.Logger.Log("[필터 추적] ffxSetFilterAdvancedPlus.StartEffect 가 없어 고급 필터는 추적 안 함");
                // 고급 필터 하나가 3ms 씩 걸린다(2026 맵 35초, 7개 22ms). 어디서 쓰는지 나누어 잰다 (곡 하나에 수백 번뿐이라 Stopwatch 로 잰다)
                var rf = AccessTools.Method(adv, "ResetFilters");
                if (rf != null) h.Patch(rf, prefix: new HarmonyMethod(typeof(FilterTrace), nameof(RfPre)), postfix: new HarmonyMethod(typeof(FilterTrace), nameof(RfPost)));
                advOthers = AccessTools.Field(adv, "disableOthers"); advDur = AccessTools.Field(adv, "duration"); advUsed = AccessTools.Field(adv, "usedFilters");
            }
            if (plus != null)
            {
                plusFilter = AccessTools.Field(plus, "filter"); plusOn = AccessTools.Field(plus, "enableFilter");
                var m = AccessTools.DeclaredMethod(plus, "StartEffect");
                if (m != null) h.Patch(m, prefix: new HarmonyMethod(typeof(FilterTrace), nameof(Plus)));
                else Main.Entry.Logger.Log("[필터 추적] ffxSetFilterPlus.StartEffect 가 없어 일반 필터는 추적 안 함");
            }
        }

        private static FieldInfo advOthers, advDur, advUsed;
        private static long advStart, rfTicks; private static int rfCalls, slowLogged;
        private static void RfPre(out long __state) { __state = System.Diagnostics.Stopwatch.GetTimestamp(); }
        private static void RfPost(long __state) { rfTicks += System.Diagnostics.Stopwatch.GetTimestamp() - __state; rfCalls++; }
        private static void AdvPost(object __instance)
        {
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - advStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (ms < 1.0 || slowLogged >= 80) return;
            slowLogged++;
            try
            {
                int used = 0;
                var d = advUsed.GetValue(null) as System.Collections.IDictionary;
                if (d != null) foreach (System.Collections.DictionaryEntry kv in d) { var hs = kv.Value as System.Collections.ICollection; if (hs != null) used = System.Math.Max(used, hs.Count); }
                Main.Entry.Logger.Log(string.Format("[필터 비용] {0} {1} 다른것끄기 {2} 길이 {3} : {4:F2}ms (그중 ResetFilters {5}번 {6:F2}ms, 쓴 필터 {7}개) 곡 {8:F1}초",
                    advName.GetValue(__instance), true.Equals(advOn.GetValue(__instance)) ? "켬" : "끔", advOthers.GetValue(__instance), advDur.GetValue(__instance), ms,
                    rfCalls, rfTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency, used, Time.timeSinceLevelLoad));
            }
            catch { }
        }

        private static void Adv(object __instance)
        {
            advStart = System.Diagnostics.Stopwatch.GetTimestamp(); rfTicks = 0; rfCalls = 0;
            try { Add(advName.GetValue(__instance) + (true.Equals(advOn.GetValue(__instance)) ? " 켬" : " 끔") + "(고급)"); } catch { }
        }

        private static void Plus(object __instance)
        {
            try { Add(plusFilter.GetValue(__instance) + (true.Equals(plusOn.GetValue(__instance)) ? " 켬" : " 끔")); } catch { }
        }

        private static void Add(string what)
        {
            recent.Add(new Rec { Frame = Time.frameCount, What = what });
            if (recent.Count > 400) recent.RemoveRange(0, 200);
        }

        // frame 과 그 앞 back 프레임 동안의 필터 변화 (같은 것은 개수로 묶음)
        internal static string Recent(int frame, int back)
        {
            var count = new Dictionary<string, int>();
            var order = new List<string>();
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                var r = recent[i];
                if (r.Frame > frame) continue;
                if (r.Frame < frame - back) break;
                int c;
                if (count.TryGetValue(r.What, out c)) count[r.What] = c + 1;
                else { count[r.What] = 1; order.Add(r.What); }
            }
            if (order.Count == 0) return "없음";
            var sb = new StringBuilder();
            foreach (var w in order) { if (sb.Length > 0) sb.Append(", "); sb.Append(w); if (count[w] > 1) sb.Append(" ×").Append(count[w]); }
            return sb.ToString();
        }
    }
}
