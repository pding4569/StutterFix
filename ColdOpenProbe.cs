#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace StutterFix
{
    // Explicit load-time diagnostic only; never included in the shipping Player.
    internal static class ColdOpenProbe
    {
        internal static bool Legacy;
        static readonly Dictionary<Texture2D, bool> loaded = new Dictionary<Texture2D, bool>();
        internal static void Begin() { loaded.Clear(); }
        internal static void Record(Texture2D tex, bool late) { loaded[tex] = late; }
        internal static void Step(string arg)
        {
            if (arg == "legacy" || arg == "wait")
            {
                Legacy = arg == "legacy";
                Main.Entry.Logger.Log("[coldopen] mode=" + (Legacy ? "legacy-half" : "wait-all"));
                return;
            }
            foreach (var g in loaded.Where(p => p.Key).GroupBy(p => (p.Value ? "late/" : "ready/") + p.Key.format + "/readable=" + p.Key.isReadable))
                Main.Entry.Logger.Log("[coldopen] group=" + g.Key + " count=" + g.Count() + " runtime_MB=" + (g.Sum(p => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(p.Key)) / 1048576.0).ToString("F2"));
            var all = Resources.FindObjectsOfTypeAll<Texture2D>();
            Main.Entry.Logger.Log("[coldopen] all_textures=" + all.Length + " profiler_runtime_MB=" + (all.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)) / 1048576.0).ToString("F2") + " game_VRAM_MB=" + SystemMonitor.VramGameMB.ToString("F0") + " total_VRAM_MB=" + SystemMonitor.VramUsedMB.ToString("F0") + " shared_game_MB=" + SystemMonitor.SharedGameMB.ToString("F0") + " RAM_game_MB=" + SystemMonitor.RamGameMB.ToString("F0"));
            loaded.Clear();
        }
    }
}
#endif

