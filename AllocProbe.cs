using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StutterFix
{
    // (개발자용, alloc-probe 파일) 곡 중 쓰레기를 만드는 함수 찾기: 후보 함수 몇 개만 감싸 호출 전후 힙 차이를 더한다.
    // 곡 중에는 GC 가 멈춰 있어 힙이 늘기만 하므로 차이 = 그 함수(안에서 부른 것 포함)가 잡은 메모리.
    // AllocScan 은 컴포넌트 593개를 감싸 감싸기 자체가 초당 49MB 를 만들었다. 여기서는 몇 개만, 값 형식 상태로만 잰다.
    internal static class AllocProbe
    {
        private static readonly string[][] Targets =
        {
            new[] { "scrConductor", "Update" }, new[] { "scrController", "Update" }, new[] { "scrFloor", "Update" }, new[] { "scrPlanet", "Update" },
            new[] { "scrCamera", "Update" }, new[] { "scrCamera", "LateUpdate" }, new[] { "scnGame", "Update" }, new[] { "scnEditor", "Update" },
            new[] { "DG.Tweening.Core.DOTweenComponent", "Update" }, new[] { "scrController", "Hit" }, new[] { "scrController", "Simulated_PlayerControl_Update" },
            new[] { "scrConductor", "OnBeat" }, new[] { "scrMisc", "GetHitMargin" }, new[] { "StutterFix.Main", "OnUpdate" },
        };
        private static readonly Dictionary<MethodBase, int> index = new Dictionary<MethodBase, int>();
        private static readonly List<string> names = new List<string>();
        private static long[] bytes = new long[0];
        private static long[] calls = new long[0];
        private static float since;

        internal static void Install(Harmony h)
        {
            var pre = new HarmonyMethod(typeof(AllocProbe), nameof(Pre));
            var post = new HarmonyMethod(typeof(AllocProbe), nameof(Post));
            foreach (var t in Targets)
            {
                var type = AccessTools.TypeByName(t[0]);
                if (type == null) continue;
                foreach (var m in type.GetMethods(AccessTools.all))
                    if (m.Name == t[1] && m.DeclaringType == type && !m.IsAbstract && !m.ContainsGenericParameters) Add(h, m, t[0].Substring(t[0].LastIndexOf('.') + 1) + "." + t[1], pre, post);
            }
            // 효과 시작: 효과 종류마다 따로
            var ffx = AccessTools.TypeByName("ffxPlusBase");
            if (ffx != null)
                foreach (var type in AccessTools.GetTypesFromAssembly(ffx.Assembly))
                {
                    if (type == null || !ffx.IsAssignableFrom(type)) continue;
                    foreach (var m in type.GetMethods(AccessTools.all))
                        if ((m.Name == "StartEffect" || m.Name == "ScrubToTime") && m.DeclaringType == type && !m.IsAbstract) Add(h, m, type.Name + "." + m.Name, pre, post);
                }
            bytes = new long[names.Count]; calls = new long[names.Count];
            Main.Entry.Logger.Log("[할당 후보] 함수 " + index.Count + "개 감쌈");
        }

        private static void Add(Harmony h, MethodBase m, string name, HarmonyMethod pre, HarmonyMethod post)
        {
            try
            {
                h.Patch(m, prefix: pre, postfix: post);
                int i = names.IndexOf(name);
                if (i < 0) { i = names.Count; names.Add(name); }
                index[m] = i;
            }
            catch { }
        }

        public static void Pre(out long __state) { __state = GC.GetTotalMemory(false); }
        public static void Post(MethodBase __originalMethod, long __state)
        {
            if (PerfOverlay.SongBucket < 0) return;   // 곡 중만
            int i;
            if (!index.TryGetValue(__originalMethod, out i)) return;
            long d = GC.GetTotalMemory(false) - __state;
            if (d > 0) bytes[i] += d;
            calls[i]++;
        }

        internal static void ResetSong() { Array.Clear(bytes, 0, bytes.Length); Array.Clear(calls, 0, calls.Length); since = UnityEngine.Time.realtimeSinceStartup; }

        internal static void Report()
        {
            if (bytes.Length == 0) return;
            float secs = Math.Max(1f, UnityEngine.Time.realtimeSinceStartup - since);
            var ix = new List<int>();
            for (int i = 0; i < names.Count; i++) if (bytes[i] > 0) ix.Add(i);
            ix.Sort((a, b) => bytes[b].CompareTo(bytes[a]));
            var sb = new System.Text.StringBuilder();
            for (int n = 0; n < ix.Count && n < 20; n++)
            {
                int i = ix[n];
                if (n > 0) sb.Append(", ");
                sb.Append(names[i]).Append(' ').Append((bytes[i] / 1048576.0 / secs).ToString("F3")).Append("MB/s (").Append((bytes[i] / Math.Max(1, calls[i])).ToString()).Append("B x ").Append((calls[i] / secs).ToString("F0")).Append("/s)");
            }
            Main.Entry.Logger.Log("[할당 후보] 곡 중 (안에서 부른 것 포함): " + sb);
        }
    }
}
