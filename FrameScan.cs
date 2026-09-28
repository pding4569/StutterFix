using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StutterFix
{
    // (개발자용, 자동 시험 framescan N) 곡 중 매 프레임 드는 유니티 쪽 비용의 출처를 찾는다.
    // 엔진 단계 측정에서 캔버스(PlayerUpdateCanvases + PlayerEmitCanvasGeometry)와 그리기 마무리(FinishFrameRendering)가 컸다.
    // - 켜진 캔버스마다 UI 요소 수와 실제로 보이는 것의 수
    // - 켜진 카메라마다 설정과 메인 스레드에서 쓰는 시간(onPreCull ~ onPostRender)
    // - N초 동안 다시 그리기 표시(SetVerticesDirty 등)를 받은 UI 요소와 CanvasUpdateRegistry.PerformUpdate 시간
    internal static class FrameScan
    {
        private static Harmony h;
        private static bool running;
        private static float startAt, secs;
        private static int frames, lastFrame = -1;
        private static readonly Dictionary<int, int> dirty = new Dictionary<int, int>();
        private static readonly Dictionary<int, string> names = new Dictionary<int, string>();
        private static int stackSamples;
        private static readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
        private static long perfTicks;
        private static readonly Stopwatch perfSw = new Stopwatch();
        private static readonly Dictionary<Camera, long> camTicks = new Dictionary<Camera, long>();
        private static readonly Dictionary<Camera, long> camStart = new Dictionary<Camera, long>();

        // 자동 시험 단계: 끝났으면 true
        internal static bool Step(string arg, float now)
        {
            if (!running)
            {
                float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
                if (secs <= 0f) secs = 10f;
                Begin(now);
                return false;
            }
            if (Time.frameCount != lastFrame) { lastFrame = Time.frameCount; frames++; }
            if (now - startAt < secs) return false;
            Report();
            running = false;
            return true;
        }

        private static void Begin(float now)
        {
            if (h == null)
            {
                h = new Harmony("StutterFix.framescan");
                var pre = new HarmonyMethod(typeof(FrameScan), nameof(Dirty));
                int n = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || !typeof(Graphic).IsAssignableFrom(t) || t.ContainsGenericParameters) continue;
                        foreach (var mn in new[] { "SetVerticesDirty", "SetMaterialDirty", "SetLayoutDirty" })
                        {
                            var m = t.GetMethod(mn, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            try { h.Patch(m, prefix: pre); n++; } catch { }
                        }
                    }
                }
                var pu = AccessTools.Method(typeof(CanvasUpdateRegistry), "PerformUpdate");
                if (pu != null) h.Patch(pu, prefix: new HarmonyMethod(typeof(FrameScan), nameof(PerfPre)), postfix: new HarmonyMethod(typeof(FrameScan), nameof(PerfPost)));
                Camera.onPreCull += PreCull;
                Camera.onPostRender += PostRender;
                h.Patch(AccessTools.Method(typeof(scrDecoration), "UpdatePosition"), prefix: new HarmonyMethod(typeof(FrameScan), nameof(PosPre)), postfix: new HarmonyMethod(typeof(FrameScan), nameof(PosPost)));
                Log("다시 그리기 함수 " + n + "개 감쌈");
            }
            posCalls = posSame = posSimple = posSimpleSame = posTicks = 0;
            stacks.Clear(); stackSamples = 0; dirty.Clear(); camTicks.Clear(); camStart.Clear(); perfTicks = 0; frames = 0; lastFrame = Time.frameCount;
            startAt = now; running = true;
            Snapshot();
        }

        private static void Log(string s) { Main.Entry.Logger.Log("[프레임 조사] " + s); }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static void Snapshot()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled) continue;
                var crs = c.GetComponentsInChildren<CanvasRenderer>(false);
                int shown = 0;
                foreach (var cr in crs) if (!cr.cull && cr.GetInheritedAlpha() > 0.001f) shown++;
                var sub = c.GetComponentsInChildren<Canvas>(false).Length - 1;
                sb.Append("\n  캔버스 ").Append(PathOf(c.transform)).Append(" (").Append(c.renderMode).Append(", 순서 ").Append(c.sortingOrder)
                  .Append(", 카메라 ").Append(c.worldCamera != null ? c.worldCamera.name : "-").Append("): UI 요소 ").Append(crs.Length)
                  .Append("개, 보이는 것 ").Append(shown).Append("개, 하위 캔버스 ").Append(sub).Append("개");
            }
            Log("켜진 루트 캔버스:" + sb);
            sb.Length = 0;
            foreach (var cam in Camera.allCameras)
                sb.Append("\n  카메라 ").Append(PathOf(cam.transform)).Append(": 깊이 ").Append(cam.depth).Append(", 마스크 0x").Append(cam.cullingMask.ToString("X"))
                  .Append(", 지우기 ").Append(cam.clearFlags).Append(", 대상 ").Append(cam.targetTexture != null ? cam.targetTexture.name + " " + cam.targetTexture.width + "x" + cam.targetTexture.height : "화면")
                  .Append(", 직교 ").Append(cam.orthographic);
            Log("켜진 카메라 " + Camera.allCameras.Length + "개:" + sb);
            // 카메라가 그리는 텍스처를 화면에서 쓰는 곳 (RawImage, 렌더러 재질)
            sb.Length = 0;
            foreach (var cam in Camera.allCameras)
            {
                var rt = cam.targetTexture;
                if (rt == null) continue;
                int raw = 0, rend = 0;
                foreach (var ri in Resources.FindObjectsOfTypeAll<RawImage>()) if (ri.texture == rt) raw++;
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                    foreach (var m in r.sharedMaterials) if (m != null && m.HasProperty("_MainTex") && m.mainTexture == rt) { rend++; break; }
                sb.Append("\n  ").Append(cam.name).Append(" -> RawImage ").Append(raw).Append("개, 렌더러 ").Append(rend).Append("개");
            }
            Log("카메라 텍스처를 쓰는 곳:" + sb);
        }

        public static void Dirty(Graphic __instance)
        {
            if (!running || __instance == null) return;
            int id = __instance.GetInstanceID();
            int c;
            dirty.TryGetValue(id, out c);
            dirty[id] = c + 1;
            if (c == 0) names[id] = PathOf(__instance.transform) + " <" + __instance.GetType().Name + ">";
            // 누가 부르는지: 처음 400번만 호출 경로를 모은다
            if (stackSamples < 400)
            {
                stackSamples++;
                var st = new StackTrace(1, false);
                var sb = new System.Text.StringBuilder(__instance.name);
                for (int i = 0; i < st.FrameCount && i < 7; i++) { var m = st.GetFrame(i).GetMethod(); if (m != null) sb.Append(" <- ").Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append(".").Append(m.Name); }
                string k = sb.ToString(); int sc; stacks.TryGetValue(k, out sc); stacks[k] = sc + 1;
            }
        }

        public static void PerfPre() { if (running) perfSw.Restart(); }
        public static void PerfPost() { if (running) { perfSw.Stop(); perfTicks += perfSw.ElapsedTicks; } }

        private static void PreCull(Camera cam) { if (running) camStart[cam] = Stopwatch.GetTimestamp(); }
        private static void PostRender(Camera cam)
        {
            long s;
            if (!running || !camStart.TryGetValue(cam, out s)) return;
            long c;
            camTicks.TryGetValue(cam, out c);
            camTicks[cam] = c + Stopwatch.GetTimestamp() - s;
        }

        // 장식 위치 갱신(scrDecoration.UpdatePosition): 불러도 변환(위치·회전·크기)이 그대로인 호출이 얼마나 되나
        private static long posCalls, posSame, posSimple, posSimpleSame, posTicks;
        public struct PosState { public Vector3 lp, wp, ls; public Quaternion r; public long t; }
        public static void PosPre(scrDecoration __instance, out PosState __state)
        {
            __state = default(PosState);
            if (!running || __instance.pivotTrans == null) return;
            var p = __instance.pivotTrans;
            __state.lp = p.localPosition; __state.r = p.rotation; __state.ls = p.localScale; __state.wp = __instance.transform.position;
            __state.t = Stopwatch.GetTimestamp();
        }
        public static void PosPost(scrDecoration __instance, PosState __state)
        {
            if (!running || __state.t == 0) return;
            posTicks += Stopwatch.GetTimestamp() - __state.t;
            posCalls++;
            var p = __instance.pivotTrans;
            bool same = p.localPosition == __state.lp && p.rotation == __state.r && p.localScale == __state.ls && __instance.transform.position == __state.wp;
            if (same) posSame++;
            var par = __instance.parallax;
            bool simple = __instance.followPlanet == null && !__instance.stickToFloor && !__instance.lockRotation && !__instance.lockScale
                && (par == null || (!par.clampToScreen && par.multiplier_x == 0f && par.multiplier_y == 0f && !par.dontAlterX && !par.dontAlterY));
            if (simple) { posSimple++; if (same) posSimpleSame++; }
        }

        private static void Report()
        {
            int f = Math.Max(1, frames);
            double tickMs = 1000.0 / Stopwatch.Frequency;
            Log(frames + "프레임, CanvasUpdateRegistry.PerformUpdate 프레임당 " + (perfTicks * tickMs / f).ToString("F3") + "ms");
            Log("장식 위치 갱신 프레임당 " + ((double)posCalls / f).ToString("F0") + "번 " + (posTicks * tickMs / f).ToString("F3") + "ms(감싼 값 포함) | 변환 그대로 "
                + (100.0 * posSame / Math.Max(1, posCalls)).ToString("F1") + "% | 단순(따라감·바닥붙음·회전/크기 고정·시차 없음) " + (100.0 * posSimple / Math.Max(1, posCalls)).ToString("F1")
                + "%, 그중 그대로 " + (100.0 * posSimpleSame / Math.Max(1, posSimple)).ToString("F1") + "%");
            var sb = new System.Text.StringBuilder();
            foreach (var kv in camTicks) sb.Append("\n  ").Append(kv.Key != null ? kv.Key.name : "(사라짐)").Append(" ").Append((kv.Value * tickMs / f).ToString("F3")).Append("ms");
            Log("카메라별 메인 스레드 시간(컬링~그리기 명령), 프레임당:" + sb);
            var ids = new List<int>(dirty.Keys);
            ids.Sort((a, b) => dirty[b].CompareTo(dirty[a]));
            sb.Length = 0;
            long total = 0;
            foreach (var id in ids) total += dirty[id];
            for (int i = 0; i < ids.Count && i < 25; i++)
                sb.Append("\n  ").Append(names[ids[i]]).Append(" ").Append(((double)dirty[ids[i]] / f).ToString("F2")).Append("번/프레임");
            Log("다시 그리기 표시 받은 UI 요소 " + ids.Count + "개, 프레임당 " + ((double)total / f).ToString("F1") + "번:" + sb);
            var ks = new List<string>(stacks.Keys); ks.Sort((a, b) => stacks[b].CompareTo(stacks[a])); sb.Length = 0;
            for (int i = 0; i < ks.Count && i < 15; i++) sb.Append("\n  ").Append(stacks[ks[i]]).Append("번: ").Append(ks[i]);
            Log("다시 그리기 호출 경로(처음 400번):" + sb);
            Snapshot();
        }
    }
}
