using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StutterFix
{
    // (개발자용 자동 시험 psdump) 지금 켜진 파티클 시스템을 세어 곡 중 ParticleSystemBegin/EndUpdateAll 이 무거운 이유를 본다.
    // 총 개수, 재생 중, 입자 수 합계, 화면에 보이는(renderer.isVisible) 것, 입자 수 많은 위쪽 몇 개(오브젝트 이름·입자 수·보임·알파 0 인지).
    // (개발자용, matprobe 파일) 곡 중 new Material(Shader) / Shader.Find 횟수·시간
    internal static class MatProbe
    {
        internal static long Mats, Finds; internal static double MatMs, FindMs, WorstMs; internal static float WorstAt;
        internal static void Install(HarmonyLib.Harmony h)
        {
            var ctor = typeof(Material).GetConstructor(new[] { typeof(Shader) });
            var find = typeof(Shader).GetMethod("Find", new[] { typeof(string) });
            h.Patch(ctor, prefix: new HarmonyLib.HarmonyMethod(typeof(MatProbe), nameof(Pre)), postfix: new HarmonyLib.HarmonyMethod(typeof(MatProbe), nameof(PostMat)));
            h.Patch(find, prefix: new HarmonyLib.HarmonyMethod(typeof(MatProbe), nameof(Pre)), postfix: new HarmonyLib.HarmonyMethod(typeof(MatProbe), nameof(PostFind)));
        }
        public static void Pre(out long __state) { __state = System.Diagnostics.Stopwatch.GetTimestamp(); }
        private static double Ms(long t0) { return (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
        public static void PostMat(long __state) { if (!Hitch.Playing) return; double ms = Ms(__state); Mats++; MatMs += ms; Worst(ms); }
        public static void PostFind(long __state) { if (!Hitch.Playing) return; double ms = Ms(__state); Finds++; FindMs += ms; Worst(ms); }
        private static void Worst(double ms) { if (ms > WorstMs) { WorstMs = ms; WorstAt = scrConductor.instance != null ? (float)scrConductor.instance.songposition_minusi : -1f; } }
        internal static string State { get { return "곡 중 new Material " + Mats + "번 " + MatMs.ToString("F2") + "ms, Shader.Find " + Finds + "번 " + FindMs.ToString("F2") + "ms, 가장 긴 한 번 " + WorstMs.ToString("F2") + "ms @" + WorstAt.ToString("F1") + "초"; } }
    }

    internal static class ParticleDump
    {
        internal static string Run(int top)
        {
            var all = Resources.FindObjectsOfTypeAll<ParticleSystem>();
            int total = 0, active = 0, playing = 0, visible = 0, withParticles = 0, simulating = 0;
            long particles = 0, visibleParticles = 0, offscreenParticles = 0;
            var rows = new List<KeyValuePair<int, string>>();
            foreach (var ps in all)
            {
                if (ps == null || !ps.gameObject.scene.IsValid()) continue;
                total++;
                if (!ps.gameObject.activeInHierarchy) continue;
                active++;
                int n = ps.particleCount;
                bool vis = false;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null) vis = r.isVisible;
                if (ps.isPlaying) playing++;
                if (n > 0) { withParticles++; particles += n; if (vis) { visibleParticles += n; } else offscreenParticles += n; }
                if (vis) visible++;
                if (ps.isPlaying || n > 0) simulating++;
                if (n > 0)
                {
                    var sb = new StringBuilder();
                    string path = ps.gameObject.name; var t = ps.transform.parent; int d = 0;
                    while (t != null && d++ < 2) { path = t.name + "/" + path; t = t.parent; }
                    sb.Append(path).Append(' ').Append(n).Append("개 보임=").Append(vis).Append(" 재생=").Append(ps.isPlaying)
                      .Append(" 컬링=").Append(ps.main.cullingMode).Append(" 최대=").Append(ps.main.maxParticles).Append(" 시뮬=").Append(ps.main.simulationSpace)
                      .Append(" 방출=").Append(ps.emission.enabled ? ps.emission.rateOverTime.constantMax.ToString("F1") : "끔");
                    rows.Add(new KeyValuePair<int, string>(n, sb.ToString()));
                }
            }
            rows.Sort((a, b) => b.Key.CompareTo(a.Key));
            var o = new StringBuilder();
            o.Append("전체 ").Append(total).Append(", 켜짐 ").Append(active).Append(", 재생 중 ").Append(playing).Append(", 입자 있는 것 ").Append(withParticles)
             .Append(", 입자 합계 ").Append(particles).Append(" (보이는 것 ").Append(visibleParticles).Append(", 안 보이는 것 ").Append(offscreenParticles).Append("), 보이는 시스템 ").Append(visible);
            for (int i = 0; i < rows.Count && i < top; i++) o.Append("\n    ").Append(rows[i].Value);
            return o.ToString();
        }
    }
}
