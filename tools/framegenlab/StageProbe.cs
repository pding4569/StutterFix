using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.LowLevel;

namespace StutterFix
{
    // Only compiled by build_measure.ps1. No method patches, heap queries, per-frame strings or files.
    // Boundary clocks cover the entire frame, including the gap after the last PlayerLoop child.
    internal static class FrameGenStageProbe
    {
        private struct Marker { }
        private static string[] names;
        private static long[] frameTicks, totals;
        private static long stamp, previousFrame;
        private static int last = -1, samples;
        private static bool installed, sampling, active;
        private static double songStart, frameMs, excludedMs;
        private static int excluded;

        internal static void Install()
        {
            if (installed) return;
            var root = PlayerLoop.GetCurrentPlayerLoop();
            var list = new List<string>();
            bool baseline = System.IO.File.Exists(System.IO.Path.Combine(Main.Entry.Path, "framegen-probe-baseline.txt"));
            for (int t = 0; t < root.subSystemList.Length; t++)
            {
                var top = root.subSystemList[t];
                var children = new List<PlayerLoopSystem>();
                if (top.subSystemList != null)
                    foreach (var child in top.subSystemList)
                    {
                        if (!baseline || list.Count == 0)
                        {
                            int index = list.Count;
                            list.Add(baseline ? "Frame/all" : (top.type == null ? "?" : top.type.Name) + "/" + (child.type == null ? "?" : child.type.Name));
                            children.Add(Make(index));
                        }
                        children.Add(child);
                    }
                // Captures everything from this top-level system's end until the next marker.
                if (!baseline || t == root.subSystemList.Length - 1)
                {
                    int end = list.Count; list.Add((top.type == null ? "?" : top.type.Name) + "/outside"); children.Add(Make(end));
                }
                top.subSystemList = children.ToArray(); root.subSystemList[t] = top;
            }
            names = list.ToArray(); frameTicks = new long[names.Length]; totals = new long[names.Length];
            stamp = Stopwatch.GetTimestamp(); last = -1; installed = true;
            PlayerLoop.SetPlayerLoop(root);
            Main.Entry.Logger.Log("[프레임생성 단계] 측정 전용 PlayerAuto, 경계 " + names.Length + "곳; 시작 5초 제외; 프레임 전체와 바깥 구간 포함");
        }
        private static PlayerLoopSystem Make(int index)
        {
            return new PlayerLoopSystem { type = typeof(Marker), updateDelegate = () => Record(index) };
        }
        private static void Record(int index)
        {
            long time = Stopwatch.GetTimestamp();
            if (last >= 0) frameTicks[last] += time - stamp;
            if (index == 0)
            {
                bool playing = Hitch.Playing;
                if (playing && !active)
                {
                    Array.Clear(totals, 0, totals.Length); samples = excluded = 0; frameMs = excludedMs = 0;
                    songStart = (double)time / Stopwatch.Frequency;
                }
                if (sampling && previousFrame > 0)
                {
                    double ms = (time - previousFrame) * 1000.0 / Stopwatch.Frequency;
                    // An edit/load/menu frame crossing the song boundary is not a playable frame.
                    if (playing)
                    {
                        for (int i = 0; i < totals.Length; i++) totals[i] += frameTicks[i];
                        frameMs += ms; samples++;
                    }
                    else { excluded++; excludedMs += ms; }
                }
                if (!playing && active) Report();
                active = playing; sampling = playing && (double)time / Stopwatch.Frequency - songStart >= 5;
                Array.Clear(frameTicks, 0, frameTicks.Length); previousFrame = time;
            }
            stamp = time; last = index;
        }
        private static void Report()
        {
            if (samples < 30) return;
            var inv = CultureInfo.InvariantCulture;
            Main.Entry.Logger.Log("[프레임생성 단계] frames=" + samples + " mean_ms=" + (frameMs / samples).ToString("F6", inv)
                + " excluded=" + excluded + " excluded_ms=" + excludedMs.ToString("F3", inv));
            for (int i = 0; i < names.Length; i++)
                if (totals[i] > 0) Main.Entry.Logger.Log("[프레임생성 단계] " + names[i] + "=" + (totals[i] * 1000.0 / Stopwatch.Frequency / samples).ToString("F6", inv));
        }
        internal static void Finish()
        {
            if (!installed) return;
            Report(); active = sampling = false;
        }
        private static PlayerLoopSystem Remove(PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return loop;
            var kept = new List<PlayerLoopSystem>();
            foreach (var child in loop.subSystemList) if (child.type != typeof(Marker)) kept.Add(Remove(child));
            loop.subSystemList = kept.ToArray(); return loop;
        }
        internal static void Uninstall()
        {
            if (!installed) return;
            PlayerLoop.SetPlayerLoop(Remove(PlayerLoop.GetCurrentPlayerLoop()));
            installed = active = sampling = false; last = -1; previousFrame = 0;
        }
    }
}
