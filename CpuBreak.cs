using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (자동 시험 cpubreak on / begin / end) 메인 스레드의 스크립트 단계(ScriptRunBehaviourUpdate/LateUpdate) 안에서
    // 어느 게임 클래스의 Update·LateUpdate·FixedUpdate 가 프레임당 몇 ms 인지 잰다.
    //   on    : Assembly-CSharp 의 MonoBehaviour 가 직접 가진 Update/LateUpdate/FixedUpdate 를 모두 감싼다(곡 시작 앞에서 한 번, 수 초 걸림)
    //   begin : 지금부터 센다.  end : 프레임당 ms·호출 수를 큰 순서로 로그에 남긴다.
    // 감싸는 비용(호출 1번당 약 0.05us 추정)이 호출 수 x 0.05us 로 합계에 섞이므로 호출 수를 같이 적는다.
    // 켜지 않으면 비용 0. 측정 전용(성능 A/B 판에서는 쓰지 않는다).
    internal static class CpuBreak
    {
        private static bool installed, counting;
        private static readonly Dictionary<MethodBase, int> idx = new Dictionary<MethodBase, int>();
        private static string[] names = new string[0];
        private static long[] ticks = new long[0], calls = new long[0];
        private static int frame0; private static float t0;

        internal static string On()
        {
            if (installed) return "이미 켜짐 (메서드 " + names.Length + "개)";
            installed = true;
            var list = new List<MethodBase>();
            var asm = typeof(scrController).Assembly;
            Type[] types; try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; }
            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || t.ContainsGenericParameters || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                foreach (var n in new[] { "Update", "LateUpdate", "FixedUpdate" })
                {
                    var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (m != null && m.GetMethodBody() != null && m.GetMethodBody().GetILAsByteArray().Length > 8) list.Add(m);
                }
            }
            var dt = AccessTools.TypeByName("DG.Tweening.Core.DOTweenComponent");
            if (dt != null) foreach (var n in new[] { "Update", "LateUpdate", "FixedUpdate" }) { var m = AccessTools.DeclaredMethod(dt, n); if (m != null) list.Add(m); }
            names = new string[list.Count]; ticks = new long[list.Count]; calls = new long[list.Count];
            var h = new Harmony("StutterFix.cpubreak");
            var pre = new HarmonyMethod(typeof(CpuBreak), nameof(Pre)) { priority = Priority.First };
            var post = new HarmonyMethod(typeof(CpuBreak), nameof(Post)) { priority = Priority.Last };
            int ok = 0;
            for (int i = 0; i < list.Count; i++)
            {
                names[i] = list[i].DeclaringType.Name + "." + list[i].Name;
                try { idx[list[i]] = i; h.Patch(list[i], prefix: pre, postfix: post); ok++; } catch { idx.Remove(list[i]); }
            }
            return "감쌈 " + ok + "/" + list.Count;
        }

        public static void Pre(out long __state) { __state = counting ? Stopwatch.GetTimestamp() : 0; }
        public static void Post(MethodBase __originalMethod, long __state)
        {
            if (__state == 0) return;
            int i; if (!idx.TryGetValue(__originalMethod, out i)) return;
            ticks[i] += Stopwatch.GetTimestamp() - __state; calls[i]++;
        }

        internal static string Begin()
        {
            if (!installed) return "먼저 cpubreak on";
            Array.Clear(ticks, 0, ticks.Length); Array.Clear(calls, 0, calls.Length);
            frame0 = Time.frameCount; t0 = Time.realtimeSinceStartup; counting = true;
            var c = scrConductor.instance;
            return "세기 시작 (곡 " + (c == null ? "?" : c.songposition_minusi.ToString("F1")) + "초)";
        }

        internal static string End()
        {
            counting = false;
            int f = Math.Max(1, Time.frameCount - frame0);
            double tk = Stopwatch.Frequency / 1000.0;
            var ix = new List<int>(); long totCalls = 0; double tot = 0;
            for (int i = 0; i < names.Length; i++) { if (calls[i] > 0) ix.Add(i); totCalls += calls[i]; tot += ticks[i] / tk / f; }
            ix.Sort((a, b) => ticks[b].CompareTo(ticks[a]));
            var sb = new System.Text.StringBuilder();
            sb.Append(f).Append("프레임 ").Append((Time.realtimeSinceStartup - t0).ToString("F1")).Append("초 | 합계 프레임당 ").Append(tot.ToString("F2")).Append("ms, 호출 ").Append((totCalls / (double)f).ToString("F0")).Append("번/프레임(감싸기 비용 추정 ").Append((totCalls / (double)f * 0.05 / 1000).ToString("F2")).Append("ms) | ");
            for (int n = 0; n < ix.Count && n < 22; n++)
            {
                int i = ix[n]; if (n > 0) sb.Append(", ");
                sb.Append(names[i]).Append(' ').Append((ticks[i] / tk / f).ToString("F2")).Append("ms(").Append((calls[i] / (double)f).ToString("F1")).Append("x)");
            }
            return sb.ToString();
        }
    }
}
