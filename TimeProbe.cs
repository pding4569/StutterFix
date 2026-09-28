using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace StutterFix
{
    // (개발자용, time-probe 파일) 곡 중 후보 함수의 시간(안에서 부른 것 포함)을 함수별로 더한다. 고BPM 에서 타일을 밟을 때 무엇이 무거운지 찾기용.
    internal static class TimeProbe
    {
        private static readonly string[][] Targets =
        {
            new[] { "scrController", "UpdateInput" }, new[] { "scrController", "Hit" }, new[] { "scrPlayer", "Hit" }, new[] { "scrPlayer", "OttoHoldHit" },
            new[] { "scrPlayer", "Simulated_PlayerControl_Update" }, new[] { "scrPlayer", "HitAutoFloors" }, new[] { "scrPlanet", "MoveToNextFloor" }, new[] { "scrPlanet", "SwitchChosen" },
            new[] { "scrFloor", "LightUp" }, new[] { "scrFloor", "SetToRandomColor" }, new[] { "AudioManager", "Play" }, new[] { "scrHitTextManager", "ShowHitText" },
            new[] { "scrConductor", "PlayWithEndTime" }, new[] { "scnEditor", "OttoBlink" }, new[] { "scrPlanet", "Update_RefreshAngles" }, new[] { "scrMistakesManager", "AddHit" },
            new[] { "DG.Tweening.DOTween", "Kill" }, new[] { "DG.Tweening.TweenExtensions", "Kill" }, new[] { "scrPlanet", "Rewind" }, new[] { "scrFloor", "Update" },
            new[] { "scrController", "OnLandOnPortal" }, new[] { "scrPlayer", "UpdateHoldBehavior" }, new[] { "scrPlanet", "AutoShouldHitNow" }, new[] { "scrConductor", "PropagateOnBeat" },
            new[] { "scrVfxPlus", "Update" }, new[] { "scrPlanet", "MarkFail" }, new[] { "scrFloor", "UpdateAngle" }, new[] { "scrCamera", "Update" }, new[] { "scrPlayer", "CheckPostHoldFail" },
            new[] { "ADOFAI.Common.Platform.Windows.PlatformHelperWindows", "Update" }, new[] { "UnityEngine.AudioSource", "GetSpectrumData" }, new[] { "AsyncInputUtils", "UpdateOffsetTime" },
            new[] { "ffxPlusBase", "StartEffect" }, new[] { "scrFloor", "TweenOpacity" }, new[] { "scrPlanet", "ScrubToFloorNumber" }, new[] { "scrConductor", "Update" },
        };
        private static readonly Dictionary<MethodBase, int> index = new Dictionary<MethodBase, int>();
        private static readonly List<string> names = new List<string>();
        private static long[] ticks = new long[0], calls = new long[0];
        private static int frames0;
        internal static bool Force;   // (개발자용 자동 시험 campan) 곡 밖에서도 잰다

        internal static void Install(Harmony h)
        {
            var pre = new HarmonyMethod(typeof(TimeProbe), nameof(Pre));
            var post = new HarmonyMethod(typeof(TimeProbe), nameof(Post));
            foreach (var t in Targets)
            {
                var type = AccessTools.TypeByName(t[0]);
                if (type == null) continue;
                foreach (var m in type.GetMethods(AccessTools.all))
                {
                    if (m.Name != t[1] || m.DeclaringType != type || m.IsAbstract || m.ContainsGenericParameters) continue;
                    try
                    {
                        h.Patch(m, prefix: pre, postfix: post);
                        string n = t[0].Substring(t[0].LastIndexOf('.') + 1) + "." + t[1];
                        int i = names.IndexOf(n);
                        if (i < 0) { i = names.Count; names.Add(n); }
                        index[m] = i;
                    }
                    catch { }
                }
            }
            ticks = new long[names.Count]; calls = new long[names.Count];
            Main.Entry.Logger.Log("[시간 후보] 함수 " + index.Count + "개 감쌈");
        }

        public static void Pre(out long __state) { __state = Stopwatch.GetTimestamp(); }
        public static void Post(MethodBase __originalMethod, long __state)
        {
            if (!Hitch.Playing && !Force) return;
            int i;
            if (!index.TryGetValue(__originalMethod, out i)) return;
            ticks[i] += Stopwatch.GetTimestamp() - __state;
            calls[i]++;
        }

        internal static void ResetSong() { Array.Clear(ticks, 0, ticks.Length); Array.Clear(calls, 0, calls.Length); frames0 = UnityEngine.Time.frameCount; }

        internal static void Report()
        {
            if (ticks.Length == 0) return;
            int f = Math.Max(1, UnityEngine.Time.frameCount - frames0);
            var ix = new List<int>();
            for (int i = 0; i < names.Count; i++) if (calls[i] > 0) ix.Add(i);
            ix.Sort((a, b) => ticks[b].CompareTo(ticks[a]));
            var sb = new System.Text.StringBuilder();
            double ms = 1000.0 / Stopwatch.Frequency;
            foreach (var i in ix)
                sb.Append(", ").Append(names[i]).Append(' ').Append((ticks[i] * ms / f).ToString("F3")).Append("ms (").Append(((double)calls[i] / f).ToString("F1")).Append("번, 한 번 ")
                  .Append((ticks[i] * ms * 1000 / calls[i]).ToString("F1")).Append("us)");
            Main.Entry.Logger.Log("[시간 후보] 곡 " + f + "프레임, 프레임당 (안에서 부른 것 포함)" + sb);
        }
    }
}
