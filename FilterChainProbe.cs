#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 시험 전용: 화면 필터 합치기 연구(docs/filter-fusion-research.md). 프레임마다 카메라별 CameraFilterPack 사슬 순서를 모아
    // "연달아 놓인 픽셀 단위 필터 묶음"을 한 패스로 합치면 몇 패스가 줄어드는지 센다. 픽셀 단위 목록은 모드 폴더 fusable.txt (shaderlab/classify.py).
    internal static class FilterChainProbe
    {
        private const string Id = "StutterFix.FilterChainProbe";
        private static readonly Dictionary<Type, int> ids = new Dictionary<Type, int>();
        private static readonly List<string> names = new List<string>();
        private static readonly HashSet<string> fusable = new HashSet<string>();
        private static bool[] isPixel = new bool[0];
        private static int frame = -1;
        private static readonly List<(int cam, int id)> cur = new List<(int, int)>();
        private static long frames, framesWithFilters, passes, removable, maxRemovable;
        private static readonly long[] removableHist = new long[32];
        private static readonly Dictionary<string, long> runCounts = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> formats = new Dictionary<string, long>();
        private static bool on;

        internal static string Command(string arg)
        {
            var a = (arg ?? "").Trim();
            if (a == "on")
            {
                Stop(); fusable.Clear();
                var path = Path.Combine(Main.Entry.Path, "fusable.txt");
                if (File.Exists(path)) foreach (var l in File.ReadAllLines(path)) { var s = l.Trim(); if (s.Length > 0) fusable.Add(s); }
                ids.Clear(); names.Clear(); frames = framesWithFilters = passes = removable = maxRemovable = 0; Array.Clear(removableHist, 0, removableHist.Length); runCounts.Clear(); formats.Clear(); frame = -1; cur.Clear();
                var h = new Harmony(Id); var pix = new List<bool>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] ts; try { ts = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types; }
                    foreach (var t in ts)
                    {
                        if (t == null || !t.Name.StartsWith("CameraFilterPack_")) continue;
                        var m = t.GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(RenderTexture), typeof(RenderTexture) }, null);
                        if (m == null || ids.ContainsKey(t)) continue;
                        ids[t] = names.Count; names.Add(t.Name); pix.Add(fusable.Contains(t.Name.Substring("CameraFilterPack_".Length)));
                        h.Patch(m, prefix: new HarmonyMethod(typeof(FilterChainProbe), nameof(Before)));
                    }
                }
                isPixel = pix.ToArray(); on = true;
                return "on types=" + names.Count + " fusable_listed=" + fusable.Count;
            }
            if (a == "off") { Stop(); return "off"; }
            return State();
        }
        internal static void Stop() { if (!on) return; on = false; Flush(); new Harmony(Id).UnpatchAll(Id); }
        public static void Before(object __instance, RenderTexture __0)
        {
            if (!on || !Hitch.Playing) return;
            int f = Time.frameCount;
            if (f != frame) { Flush(); frame = f; }
            int id; if (!ids.TryGetValue(__instance.GetType(), out id)) return;
            int cam = ((Component)__instance).gameObject.GetInstanceID();
            cur.Add((cam, id));
            if (__0 != null) { string k = __0.format + (__0.sRGB ? "/sRGB" : ""); long c; formats.TryGetValue(k, out c); formats[k] = c + 1; }
        }
        private static void Flush()
        {
            if (frame < 0) return;
            frames++;
            if (cur.Count == 0) return;
            framesWithFilters++; passes += cur.Count;
            int rem = 0, run = 0, lastCam = int.MinValue; var runNames = new List<string>();
            for (int i = 0; i <= cur.Count; i++)
            {
                if (i < cur.Count && isPixel[cur[i].id] && (run == 0 || cur[i].cam == lastCam)) { run++; runNames.Add(names[cur[i].id].Substring(17)); lastCam = cur[i].cam; continue; }
                if (run >= 2) { rem += run - 1; string k = string.Join("+", runNames); long c; runCounts.TryGetValue(k, out c); runCounts[k] = c + 1; }
                run = 0; runNames.Clear();
                if (i < cur.Count) { lastCam = cur[i].cam; if (isPixel[cur[i].id]) { run = 1; runNames.Add(names[cur[i].id].Substring(17)); } }
            }
            removable += rem; if (rem > maxRemovable) maxRemovable = rem; removableHist[Math.Min(31, rem)]++;
            cur.Clear();
        }
        private static string State()
        {
            Flush(); frame = -1;
            var sb = new System.Text.StringBuilder();
            sb.Append("frames=").Append(frames).Append(" with_filters=").Append(framesWithFilters).Append(" passes=").Append(passes)
              .Append(" removable_passes=").Append(removable).Append(" (per frame ").Append(frames > 0 ? ((double)removable / frames).ToString("F3") : "0")
              .Append(", per filtered frame ").Append(framesWithFilters > 0 ? ((double)removable / framesWithFilters).ToString("F3") : "0").Append(") max=").Append(maxRemovable);
            sb.Append(" hist:"); for (int i = 0; i < 32; i++) if (removableHist[i] > 0) sb.Append(' ').Append(i).Append('=').Append(removableHist[i]);
            sb.Append(" | formats:"); foreach (var kv in formats) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            var top = new List<KeyValuePair<string, long>>(runCounts); top.Sort((x, y) => y.Value.CompareTo(x.Value));
            sb.Append(" | top runs:"); for (int i = 0; i < Math.Min(12, top.Count); i++) sb.Append(" [").Append(top[i].Key).Append(" x").Append(top[i].Value).Append(']');
            return sb.ToString();
        }
    }
}
#endif
