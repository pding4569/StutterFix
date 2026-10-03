using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (진단, 개발자용·측정용 플레이어 빌드, 모드 폴더에 scriptprobe.txt 가 있을 때만) 곡 중 무거운 프레임의 스크립트별 시간.
    //
    // 엔진 단계로는 "Update 스크립트 30ms" 까지만 보인다. 불러온 어셈블리(게임·다른 모드·이 모드)의 MonoBehaviour.Update/LateUpdate 를
    // 모두 감싸 프레임마다 이름별로 더하고, 합계가 기준(scriptprobe.txt 의 숫자, 없으면 10ms)을 넘는 프레임만 위에서 6개를 남긴다.
    // 2026-10-04 Windflower 94.7초·99.0초 끊김(개발자용에서는 재현 안 됨)을 이 방식으로 scrController.LateUpdate -> FloorMesh.UpdateAllRequired 로
    // 좁혔다(MeshWarm). 감싸는 데 곡 시작 전 1~2초, 호출마다 1us 안팎이 들어 평소에는 끈다.
    internal static class ScriptProbe
    {
        private static readonly Dictionary<MethodBase, int> idx = new Dictionary<MethodBase, int>();
        private static readonly List<string> names = new List<string>();
        private static double[] acc = new double[0];
        private static int[] cnt = new int[0];
        private static int frame = -1, logged;
        private static double threshold = 10.0;

        internal static void Install(Harmony h)
        {
            string flag = System.IO.Path.Combine(Main.Entry.Path, "scriptprobe.txt");
            if (!System.IO.File.Exists(flag)) return;
            try { double v; if (double.TryParse(System.IO.File.ReadAllText(flag).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0) threshold = v; } catch { }
            var sw = Stopwatch.StartNew();
            var pre = new HarmonyMethod(typeof(ScriptProbe), nameof(Pre));
            var post = new HarmonyMethod(typeof(ScriptProbe), nameof(Post));
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name;
                if (an.StartsWith("Unity") || an.StartsWith("System") || an == "mscorlib" || an.StartsWith("Mono") || an.StartsWith("0Harmony")) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t == typeof(ScriptProbe) || !typeof(MonoBehaviour).IsAssignableFrom(t) || t.ContainsGenericParameters) continue;
                    foreach (var mn in new[] { "Update", "LateUpdate" })
                    {
                        var m = t.GetMethod(mn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                        if (m == null || m.IsAbstract) continue;
                        try { h.Patch(m, prefix: pre, postfix: post); idx[m] = names.Count; names.Add(t.Name + "." + mn + " [" + an + "]"); } catch { }
                    }
                }
            }
            acc = new double[names.Count]; cnt = new int[names.Count];
            Main.Entry.Logger.Log(string.Format("[스크립트 측정] Update/LateUpdate {0}개 감쌈 {1}ms, 기준 {2}ms", names.Count, sw.ElapsedMilliseconds, threshold));
        }

        public static void Pre(out long __state) { __state = Stopwatch.GetTimestamp(); }
        public static void Post(MethodBase __originalMethod, long __state)
        {
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            int f = Time.frameCount;
            if (f != frame) { Flush(); frame = f; }
            int i;
            if (idx.TryGetValue(__originalMethod, out i)) { acc[i] += ms; cnt[i]++; }
        }

        private static void Flush()
        {
            double total = 0;
            for (int i = 0; i < acc.Length; i++) total += acc[i];
            if (total > threshold && logged < 100 && frame >= 0 && Hitch.Playing)
            {
                logged++;
                var order = new List<int>();
                for (int i = 0; i < acc.Length; i++) if (acc[i] > 0.3) order.Add(i);
                order.Sort((a, b) => acc[b].CompareTo(acc[a]));
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < order.Count && k < 6; k++) sb.Append(" [").Append(names[order[k]]).Append(' ').Append(acc[order[k]].ToString("F1")).Append("ms x").Append(cnt[order[k]]).Append(']');
                Main.Entry.Logger.Log(string.Format("[스크립트 측정] 프레임 {0} 합계 {1:F1}ms 실시간 {2:F1}초:{3}", frame, total, Time.realtimeSinceStartup, sb));
            }
            Array.Clear(acc, 0, acc.Length);
            Array.Clear(cnt, 0, cnt.Length);
        }
    }
}
