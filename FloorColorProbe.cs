#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 진단 전용: 타일 색 쓰기(FloorMeshRenderer.set_color)의 같은 값 비율과 쓰기 방식별 비용.
    // floorcolor census | same on|off | bench mat|mpb|get <타일 수> <프레임>
    internal sealed class FloorColorProbe : MonoBehaviour
    {
        const string Id = "StutterFix.FloorColorProbe";
        static int colorId = -1;
        static long same, diff;
        static readonly long[] byType = new long[16], sameByType = new long[16];
        static FloorColorProbe bench;
        static readonly AccessTools.FieldRef<FloorRenderer, Material> matRef = AccessTools.FieldRefAccess<FloorRenderer, Material>("material");
        static readonly AccessTools.FieldRef<FloorRenderer, Renderer> rendRef = AccessTools.FieldRefAccess<FloorRenderer, Renderer>("renderer");
        static readonly AccessTools.FieldRef<scrFloor, TrackColorType> typeRef = AccessTools.FieldRefAccess<scrFloor, TrackColorType>("specialColorType");
        static int Cid() { if (colorId < 0) colorId = (int)AccessTools.Field(typeof(scrFloor), "ShaderProperty_Color").GetValue(null); return colorId; }

        internal static void Command(string arg)
        {
            var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (a.Length > 0 ? a[0] : "")
            {
                case "census": Census(); break;
                case "same":
                    var h = new Harmony(Id);
                    if (a.Length > 1 && a[1] == "on")
                    {
                        same = diff = 0; Array.Clear(byType, 0, 16); Array.Clear(sameByType, 0, 16);
                        h.Patch(AccessTools.PropertySetter(typeof(FloorMeshRenderer), "color"), prefix: new HarmonyMethod(typeof(FloorColorProbe), nameof(SetPrefix)));
                        Lg.I("[floorcolor] same on");
                    }
                    else
                    {
                        h.UnpatchAll(Id);
                        var s = "[floorcolor] same=" + same + " diff=" + diff;
                        for (int i = 0; i < 16; i++) if (byType[i] > 0) s += " " + (TrackColorType)i + "=" + sameByType[i] + "/" + byType[i];
                        Lg.I(s);
                    }
                    break;
                case "bench":
                    if (bench != null) throw new Exception("bench running");
                    var go = new GameObject(Id); DontDestroyOnLoad(go); bench = go.AddComponent<FloorColorProbe>();
                    bench.mode = a.Length > 1 ? a[1] : "mat"; bench.n = a.Length > 2 ? int.Parse(a[2]) : 1000; bench.framesLeft = a.Length > 3 ? int.Parse(a[3]) : 300;
                    Lg.I("[floorcolor] bench " + bench.mode + " n=" + bench.n + " frames=" + bench.framesLeft);
                    break;
                default: throw new Exception("floorcolor census|same on|off|bench mat|mpb|get N frames");
            }
        }
        static void Census()
        {
            var fl = ADOBase.lm?.listFloors; if (fl == null) { Lg.I("[floorcolor] no floors"); return; }
            var all = new int[16]; var on = new int[16]; var vis = new int[16];
            foreach (var f in fl)
            {
                if (f == null) continue; int t = (int)typeRef(f) & 15; all[t]++;
                if (f.isActiveAndEnabled) on[t]++;
                var fr = f.floorRenderer; var r = fr == null ? null : rendRef(fr); if (r != null && r.isVisible) vis[t]++;
            }
            var s = "[floorcolor] census floors=" + fl.Count;
            for (int i = 0; i < 16; i++) if (all[i] > 0) s += " " + (TrackColorType)i + "=" + all[i] + "/enabled" + on[i] + "/visible" + vis[i];
            Lg.I(s);
        }
        static void SetPrefix(FloorMeshRenderer __instance, Color value)
        {
            var m = matRef(__instance); if (m == null) return;
            var f = __instance.GetComponentInParent<scrFloor>(); int t = f == null ? 15 : (int)typeRef(f) & 15;
            var c = m.GetColor(Cid()); byType[t]++;
            if (c.r == value.r && c.g == value.g && c.b == value.b && c.a == value.a) { same++; sameByType[t]++; } else diff++;
        }

        string mode; int n, framesLeft, frames;
        long ticks, calls;
        readonly List<scrFloor> pick = new List<scrFloor>();
        MaterialPropertyBlock block;
        void LateUpdate()
        {
            if (pick.Count == 0)
            {
                foreach (var f in ADOBase.lm.listFloors)
                {
                    if (pick.Count >= n) break;
                    if (f == null || typeRef(f) != TrackColorType.Single || f.floorRenderer == null) continue;
                    var r = rendRef(f.floorRenderer); if (r == null || !r.isVisible || matRef(f.floorRenderer) == null) continue;
                    pick.Add(f);
                }
                block = new MaterialPropertyBlock();
                Lg.I("[floorcolor] bench picked " + pick.Count);
                if (pick.Count == 0) { Finish(); return; }
            }
            int id = Cid();
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < pick.Count; i++)
            {
                var fr = pick[i].floorRenderer; var m = matRef(fr);
                if (mode == "mat") m.SetColor(id, m.GetColor(id));
                else if (mode == "get") m.GetColor(id);
                else { var r = rendRef(fr); r.GetPropertyBlock(block); block.SetColor(id, m.GetColor(id)); r.SetPropertyBlock(block); }
            }
            ticks += Stopwatch.GetTimestamp() - t0; calls += pick.Count; frames++;
            if (--framesLeft <= 0) Finish();
        }
        void Finish()
        {
            if (mode == "mpb") foreach (var f in pick) { var r = f.floorRenderer == null ? null : rendRef(f.floorRenderer); if (r != null) r.SetPropertyBlock(null); }
            double us = calls == 0 ? 0 : ticks * 1e6 / Stopwatch.Frequency / calls;
            Lg.I(FormattableString.Invariant($"[floorcolor] bench {mode} floors={pick.Count} frames={frames} us_per_call={us:F3} ms_per_frame={(frames == 0 ? 0 : ticks * 1000.0 / Stopwatch.Frequency / frames):F3}"));
            bench = null; Destroy(gameObject);
        }
    }
}
#endif
