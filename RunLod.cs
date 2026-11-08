#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        internal static Shader RunLodShader()
        {
            if (gpuLiveBundle == null) gpuLiveBundle = LoadPlayerGpuBundle();
            return gpuLiveBundle ? gpuLiveBundle.LoadAsset<Shader>("Assets/Shaders/RunLodComposite.shader") : null;
        }
    }

    // 묶음 LOD(원래 렌더러): 그리기 순서에서 연속으로 붙은 "크게 확대된 이미지 장식"들을 저해상도 그림 하나에 순서대로 모아 그리고
    // (본 카메라의 투명 그리기 직전 CommandBuffer), 묶음 첫 장식의 정렬 자리에서 화면 사각형 하나로 합친다.
    // 확대된 그림은 원래 저주파라 눈으로는 같다. 화면 전체 층 수가 묶음 수로 준다.
    internal static class RunLod
    {
        internal static bool On;
        internal static float MinMag = 4f;
        internal static int MinRun = 2;
        internal static int Frames, RunsDrawn, MembersDrawn;
        const int HideLayer = 31;

        sealed class VisTag : MonoBehaviour
        {
            internal Cand C;
            void OnBecameVisible() { if (C != null) { C.Vis = true; if (!C.Listed) { C.Listed = true; visible.Add(C); } } }
            void OnBecameInvisible() { if (C != null) C.Vis = false; }
        }
        static readonly List<Cand> visible = new List<Cand>();
        sealed class BarrierTag : MonoBehaviour
        {
            internal bool Vis;
            void OnBecameVisible() { Vis = true; }
            void OnBecameInvisible() { Vis = false; }
        }
        static BarrierTag[] barrierTag = new BarrierTag[0];
        sealed class Cand { public bool Vis, Listed; public VisTag Tag; public SpriteRenderer R; public int Layer; public bool Moved; public Item It = new Item(); public float TexW, TexH; public Material M; public int Shader, Mat; }
        static readonly Comparison<Item> order = (x, y) =>
        {
            int k = x.Q.CompareTo(y.Q); if (k != 0) return k;
            k = x.Lv.CompareTo(y.Lv); if (k != 0) return k;
            k = x.Ord.CompareTo(y.Ord); if (k != 0) return k;
            k = y.Dist.CompareTo(x.Dist); if (k != 0) return k;
            k = x.Shader.CompareTo(y.Shader); if (k != 0) return k;
            return x.Mat.CompareTo(y.Mat);
        };
        sealed class Item { public Renderer R; public Cand C; public int Q, Lv, Ord; public float Dist; public int Shader, Mat; public float X0, Y0, X1, Y1; }
        internal static float MinSave = 1.0f;   // 묶음으로 아끼는 화면 층 수가 이보다 커야 묶는다
        internal static int MaxCands = 8000;     // 후보가 이보다 많은 맵은 끈다(매 프레임 비용)
        static bool mapOff, active, anyMoved;
        internal static float ActiveSave = 3f, MinGpuWait = 0.3f;
        static readonly FrameTiming[] ft = new FrameTiming[1]; static float waitEma = 1f; internal static int ProbeEvery = 30;
        static float savedNow, savedEma; internal static int ModeSwitches, ActiveOffFrames;

        static Camera cam;
        static CommandBuffer cb;
        static Shader shader;
        static readonly List<Cand> cands = new List<Cand>();
        static readonly List<Renderer> barriers = new List<Renderer>();
        static long[] barrierKey = new long[0]; static Renderer[] barrierByKey = new Renderer[0];
        internal static int BarrierChecks;
        static int gatheredFrame = -9999;
        static readonly List<Item> items = new List<Item>();
        static readonly List<GameObject> comps = new List<GameObject>();
        static readonly List<Material> compMats = new List<Material>();
        static readonly List<RenderTexture> rts = new List<RenderTexture>();
        static Mesh quad;

        internal static string Command(string arg)
        {
            var a = arg.Trim().Split(' ');
            if (a[0] == "off") { Off(); return "꺼짐"; }
            if (a[0] == "state") return State();
            if (a.Length > 0 && float.TryParse(a[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0) MinMag = m;
            if (!On)
            {
                shader = DecoBake.RunLodShader();
                if (!shader || !shader.isSupported) return "합치기 셰이더 없음";
                On = true; Camera.onPreCull += PreCull;
            }
            return State();
        }

        static string State() => "on=" + On + " minMag=" + MinMag + " frames=" + Frames + " runs=" + RunsDrawn + " members=" + MembersDrawn
            + " 후보 " + cands.Count + " 장벽 " + barriers.Count + " 장벽검사/프레임 " + (Frames > 0 ? (BarrierChecks / (float)Frames).ToString("F0") : "-") + string.Format(" ms/프레임 전체 {0:F3} 후보 {1:F3} 정렬 {2:F3} 묶음 {3:F3}", Ms(tTotal), Ms(tCand), Ms(tSort), Ms(tRuns)) + " 이득평균 " + savedEma.ToString("F2") + " GPU대기 " + waitEma.ToString("F2") + " 켜짐 " + active + " 전환 " + ModeSwitches + " 쉰프레임 " + ActiveOffFrames + " 묶음당 평균 " + (RunsDrawn > 0 ? (MembersDrawn / (float)RunsDrawn).ToString("F1") : "-");

        static double Ms(long ticks) => Frames > 0 ? ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Frames : 0;

        internal static void Off()
        {
            if (On) Camera.onPreCull -= PreCull;
            On = false;
            if (cam && cb != null) cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
            cb?.Release(); cb = null; cam = null;
            foreach (var c in cands) { Restore(c); if (c.Tag) UnityEngine.Object.Destroy(c.Tag); }
            foreach (var tg in barrierTag) if (tg) UnityEngine.Object.Destroy(tg);
            barrierTag = new BarrierTag[0]; barrierKey = new long[0]; barrierByKey = new Renderer[0];
            cands.Clear(); barriers.Clear(); visible.Clear(); gatheredFrame = -9999;
            foreach (var g in comps) if (g) UnityEngine.Object.Destroy(g);
            foreach (var m in compMats) if (m) UnityEngine.Object.Destroy(m);
            foreach (var r in rts) if (r) { r.Release(); UnityEngine.Object.Destroy(r); }
            comps.Clear(); compMats.Clear(); rts.Clear();
        }

        static void Restore(Cand c) { if (c.Moved && c.R) c.R.gameObject.layer = c.Layer; c.Moved = false; }

        static void Gather()
        {
            foreach (var c in cands) { Restore(c); if (c.Tag) c.Tag.C = null; }
            cands.Clear(); barriers.Clear(); visible.Clear();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r) continue;
                bool cand = false;
                if (r is SpriteRenderer sr && sr.drawMode == SpriteDrawMode.Simple && sr.maskInteraction == SpriteMaskInteraction.None
                    && r.sharedMaterials.Length == 1 && r.sharedMaterial && r.sharedMaterial.shader
                    && (r.sharedMaterial.shader.name == "Sprites/Tile" || r.sharedMaterial.shader.name == "Sprites/Default")
                    && r.GetComponentInParent<scrVisualDecoration>() && !r.GetComponent<Collider2D>() && !r.GetComponentInParent<SortingGroup>())
                {
                    var m = r.sharedMaterial;
                    // 미리 곱한 알파 덮기(One, OneMinusSrcAlpha)만
                    bool okBlend = !m.HasProperty("_SrcBlend") || (m.GetFloat("_SrcBlend") == 1f && m.GetFloat("_DstBlend") == 10f);
                    cand = okBlend;
                }
                if (cand)
                {
                    var cd = new Cand { R = (SpriteRenderer)r, Layer = r.gameObject.layer };
                    var tag = r.GetComponent<VisTag>() ?? r.gameObject.AddComponent<VisTag>(); tag.C = cd; cd.Tag = tag;
                    if (r.isVisible) { cd.Vis = true; cd.Listed = true; visible.Add(cd); }
                    cands.Add(cd);
                }
                else barriers.Add(r);
            }
            // 장벽은 정렬 키(큐·층·순서) 순으로 정리해 두고, 후보 둘 사이 키 범위의 장벽만 매 프레임 본다.
            var keyed = new List<(long k, Renderer r)>();
            foreach (var r in barriers)
            {
                var m = r.sharedMaterial; if (!m || m.renderQueue <= 2500) continue;
                keyed.Add((Key(m.renderQueue, SortingLayer.GetLayerValueFromID(r.sortingLayerID), r.sortingOrder), r));
            }
            keyed.Sort((a, b) => a.k.CompareTo(b.k));
            // 후보 키 범위 안의 장벽에만 보임 알림을 붙인다(그 밖은 후보 사이에 끼지 않는다)
            long lo = long.MaxValue, hi = long.MinValue;
            foreach (var cd in cands) { var m = cd.R.sharedMaterial; long k = Key(m.renderQueue, SortingLayer.GetLayerValueFromID(cd.R.sortingLayerID), cd.R.sortingOrder); if (k < lo) lo = k; if (k > hi) hi = k; }
            keyed.RemoveAll(x => x.k < lo || x.k > hi);
            barrierKey = keyed.Select(x => x.k).ToArray(); barrierByKey = keyed.Select(x => x.r).ToArray();
            barrierTag = new BarrierTag[barrierByKey.Length];
            for (int i = 0; i < barrierByKey.Length; i++)
            {
                var r = barrierByKey[i];
                var tg = r.GetComponent<BarrierTag>() ?? r.gameObject.AddComponent<BarrierTag>(); tg.Vis = r.isVisible; barrierTag[i] = tg;
            }
            Main.Entry.Logger.Log("[runlod] 모음: 후보 " + cands.Count + ", 후보 키 범위 안 장벽 " + barrierByKey.Length + " (전체 " + barriers.Count + ")");
            gatheredFrame = Time.frameCount;
        }

        static long Key(int q, int lv, int ord) => ((long)(q & 0xFFFF) << 48) | ((long)((lv + 32768) & 0xFFFF) << 32) | (uint)(ord + int.MinValue) ;

        static int LowerBound(long k) { int lo = 0, hi = barrierKey.Length; while (lo < hi) { int mid = (lo + hi) >> 1; if (barrierKey[mid] < k) lo = mid + 1; else hi = mid; } return lo; }

        // 키 [a, b] 안에 지금 보이는 장벽이 있나
        static bool BarrierBetween(long a, long b, int camMask)
        {
            for (int i = LowerBound(a); i < barrierKey.Length && barrierKey[i] <= b; i++)
            {
                var tg = barrierTag[i]; BarrierChecks++;
                // 보임 알림 플래그(관리 메모리 읽기). 지워진 장벽은 알림이 끊기므로 숨은 것으로 본다.
                if (tg is object && tg.Vis) { if (!tg) { barrierTag[i] = null; continue; } return true; }
            }
            return false;
        }

        static void ViewRect(Bounds b, Item it)
        {
            Vector3 a = cam.WorldToViewportPoint(b.min), c = cam.WorldToViewportPoint(b.max);
            Vector3 d = cam.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, b.center.z)), e = cam.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, b.center.z));
            it.X0 = Mathf.Clamp01(Mathf.Min(Mathf.Min(a.x, c.x), Mathf.Min(d.x, e.x))); it.X1 = Mathf.Clamp01(Mathf.Max(Mathf.Max(a.x, c.x), Mathf.Max(d.x, e.x)));
            it.Y0 = Mathf.Clamp01(Mathf.Min(Mathf.Min(a.y, c.y), Mathf.Min(d.y, e.y))); it.Y1 = Mathf.Clamp01(Mathf.Max(Mathf.Max(a.y, c.y), Mathf.Max(d.y, e.y)));
        }

        static bool OnScreen(Bounds b, out float magDummy)
        {
            magDummy = 0;
            Vector3 a = cam.WorldToViewportPoint(b.min), c = cam.WorldToViewportPoint(b.max);
            Vector3 d = cam.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, b.center.z)), e = cam.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, b.center.z));
            float x0 = Mathf.Min(Mathf.Min(a.x, c.x), Mathf.Min(d.x, e.x)), x1 = Mathf.Max(Mathf.Max(a.x, c.x), Mathf.Max(d.x, e.x));
            float y0 = Mathf.Min(Mathf.Min(a.y, c.y), Mathf.Min(d.y, e.y)), y1 = Mathf.Max(Mathf.Max(a.y, c.y), Mathf.Max(d.y, e.y));
            return x1 >= 0 && y1 >= 0 && x0 <= 1 && y0 <= 1;
        }

        static void PreCull(Camera c)
        {
            if (!On || !c || c.name != "Camera") return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { PreCullBody(c); }
            catch (Exception e) { gatheredFrame = -9999; if (++errors <= 3) Main.Entry.Logger.Log("[runlod] 오류, 다시 모음: " + e.GetType().Name + " " + e.Message); }
            finally { tTotal += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }
        static int errors;
        static long tTotal, tCand, tSort, tRuns;

        static void PreCullBody(Camera c)
        {
            if (cam != c)
            {
                if (cam && cb != null) cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
                cam = c; cb = cb ?? new CommandBuffer { name = "StutterFix RunLod" };
                cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
                gatheredFrame = -9999;
            }
            if (gatheredFrame < 0) { Gather(); mapOff = cands.Count > MaxCands; if (mapOff) { Main.Entry.Logger.Log("[runlod] 이 맵은 끔: 후보 " + cands.Count + " > " + MaxCands); foreach (var cd in cands) { Restore(cd); if (cd.Tag) UnityEngine.Object.Destroy(cd.Tag); } foreach (var tg in barrierTag) if (tg) UnityEngine.Object.Destroy(tg); cands.Clear(); visible.Clear(); barrierTag = new BarrierTag[0]; barrierKey = new long[0]; barrierByKey = new Renderer[0]; } }
            if (mapOff) { cb.Clear(); for (int k = 0; k < comps.Count; k++) if (comps[k] && comps[k].activeSelf) comps[k].SetActive(false); return; }
            // GPU 를 기다리는 시간(메인 스레드의 present 대기). 쉬는 동안의 값만 켜기 판단에 쓴다(켜져 있으면 LOD 가 대기를 없앤다).
            FrameTimingManager.CaptureFrameTimings();
            if (!active && FrameTimingManager.GetLatestTimings(1, ft) == 1) waitEma += ((float)ft[0].cpuMainThreadPresentWaitTime - waitEma) * 0.05f;
            // 쉬는 모드: 최근 이득(아낀 화면 층 수의 이동 평균)이 작으면 30프레임에 한 번만 살펴본다.
            if (!active && Time.frameCount % ProbeEvery != 0)
            {
                if (anyMoved) { foreach (var cd in visible) Restore(cd); anyMoved = false; cb.Clear(); for (int k = 0; k < comps.Count; k++) if (comps[k] && comps[k].activeSelf) comps[k].SetActive(false); }
                return;
            }
            savedNow = 0;
            Frames++;
            cb.Clear();
            var target = cam.targetTexture;
            int w = target ? target.width : Screen.width, h = target ? target.height : Screen.height;
            Vector3 cp = cam.transform.position, fwd = cam.transform.forward;
            items.Clear();
            int camMask = cam.cullingMask;
            // 후보: 지난 프레임에 옮겼더라도 원래 층 기준으로 본다
            // 화면 1 월드 단위의 픽셀 수(직교 카메라)
            float ppw = h / (2f * cam.orthographicSize);
            long s0 = System.Diagnostics.Stopwatch.GetTimestamp();
            // 보이는 후보(보임 알림으로 유지)와 옮겨 둔 묶음 장식만 본다
            for (int vi = visible.Count - 1; vi >= 0; vi--)
            {
                var cd = visible[vi];
                if (!cd.Vis && !cd.Moved) { cd.Listed = false; visible[vi] = visible[visible.Count - 1]; visible.RemoveAt(visible.Count - 1); continue; }
                var r = cd.R;
                if (!r) { cd.Listed = false; visible[vi] = visible[visible.Count - 1]; visible.RemoveAt(visible.Count - 1); continue; }
                if (!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || (camMask & (1 << cd.Layer)) == 0) { Restore(cd); continue; }
                var b = r.bounds;
                if (cd.Moved && !OnScreen(b, out _)) { Restore(cd); continue; }
                var m = r.sharedMaterial;
                if (!ReferenceEquals(m, cd.M)) { if (!m || !r.sprite) { Restore(cd); continue; } cd.M = m; cd.Shader = m.shader.GetInstanceID(); cd.Mat = m.GetInstanceID(); var tr = r.sprite.textureRect; cd.TexW = Mathf.Max(1f, tr.width); cd.TexH = Mathf.Max(1f, tr.height); }
                var sz = b.size;
                float mag = Mathf.Min(sz.x * ppw / cd.TexW, sz.y * ppw / cd.TexH);
                var it = cd.It; it.R = r; it.C = mag >= MinMag && r.color.a > 0 ? cd : null; it.Q = m.renderQueue; it.Lv = SortingLayer.GetLayerValueFromID(r.sortingLayerID); it.Ord = r.sortingOrder;
                it.Dist = Vector3.Dot(b.center - cp, fwd); it.Shader = cd.Shader; it.Mat = cd.Mat;
                if (it.C != null) ViewRect(b, it);
                else Restore(cd);
                items.Add(it);
            }
            // 유니티 투명 정렬: 큐, 정렬층, 순서, 먼 것부터, 셰이더 ID, 재질 ID
            long s1 = System.Diagnostics.Stopwatch.GetTimestamp(); tCand += s1 - s0;
            items.Sort(order);
            long s2 = System.Diagnostics.Stopwatch.GetTimestamp(); tSort += s2 - s1;
            // 후보끼리 이웃한 두 장식 사이(키 포함 범위)에 보이는 장벽이 있으면 묶음을 끊는다. 키가 같은 장벽도 끊는다(동점 자리 보장 불가).
            int run = 0, compIdx = 0;
            for (int i = 0; i <= items.Count; i++)
            {
                bool ok = i < items.Count && items[i].C != null;
                if (ok)
                {
                    long ki = Key(items[i].Q, items[i].Lv, items[i].Ord);
                    if (run > 0 && BarrierBetween(Key(items[i - 1].Q, items[i - 1].Lv, items[i - 1].Ord), ki, camMask))
                    {
                        Flush(i, run, ref compIdx, w, h); run = 0;
                    }
                    if (run == 0 && BarrierBetween(ki, ki, camMask)) { Restore(items[i].C); items[i].C = null; ok = false; }
                }
                if (ok) { run++; continue; }
                Flush(i, run, ref compIdx, w, h); run = 0;
            }
            tRuns += System.Diagnostics.Stopwatch.GetTimestamp() - s2;
            // 켜진 동안은 매 프레임, 쉬는 동안은 살펴본 프레임만으로 평균을 갱신한다
            savedEma += (savedNow - savedEma) * (active ? 1f / 60f : 0.25f);
            bool was = active; active = (active ? savedEma >= ActiveSave * 0.6f : savedEma >= ActiveSave && waitEma >= MinGpuWait);
            if (was != active) { ModeSwitches++; }
            if (!active) ActiveOffFrames++;
            anyMoved = compIdx > 0;
            for (int k = compIdx; k < comps.Count; k++) if (comps[k] && comps[k].activeSelf) comps[k].SetActive(false);
            if (compIdx > 0) cb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        }

        static void Flush(int end, int run, ref int compIdx, int w, int h)
        {
            bool go = false;
            if (run >= MinRun)
            {
                // 아끼는 층 수 = 장식들 화면 넓이 합 - 저해상도 몫(1/16) - 합치기 사각형 넓이
                float sum = 0, x0 = 1, y0 = 1, x1 = 0, y1 = 0;
                for (int k = end - run; k < end; k++) { var it = items[k]; sum += (it.X1 - it.X0) * (it.Y1 - it.Y0); x0 = Mathf.Min(x0, it.X0); y0 = Mathf.Min(y0, it.Y0); x1 = Mathf.Max(x1, it.X1); y1 = Mathf.Max(y1, it.Y1); }
                float union = Mathf.Max(0, x1 - x0) * Mathf.Max(0, y1 - y0);
                float save = sum * (15f / 16f) - union;
                go = save > MinSave;
                if (go) savedNow += save;
                if (go) { rx0 = x0; ry0 = y0; rx1 = x1; ry1 = y1; }
            }
            if (go) Emit(end - run, run, compIdx++, w, h);
            else for (int k = end - run; k < end; k++) Restore(items[k].C);
        }

        static float rx0, ry0, rx1, ry1;
        static bool SameKey(Item a, Item b) => a.Q == b.Q && a.Lv == b.Lv && a.Ord == b.Ord && a.Dist == b.Dist;

        static void Emit(int start, int count, int idx, int w, int h)
        {
            int div = 4;
            int lw = Math.Max(1, w / div), lh = Math.Max(1, h / div);
            while (rts.Count <= idx) rts.Add(null);
            var rt = rts[idx];
            if (!rt || rt.width != lw || rt.height != lh)
            {
                if (rt) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                var desc = cam.targetTexture ? cam.targetTexture.descriptor : new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0);
                desc.width = lw; desc.height = lh; desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.useMipMap = false;
                rt = new RenderTexture(desc) { name = "StutterFix.RunLod", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; rt.Create(); rts[idx] = rt;
            }
            cb.SetRenderTarget(rt); cb.ClearRenderTarget(false, true, Color.clear);
            for (int k = start; k < start + count; k++)
            {
                var it = items[k];
                if (!it.C.Moved) { it.C.Layer = it.R.gameObject.layer; it.R.gameObject.layer = HideLayer; it.C.Moved = true; }
                cb.DrawRenderer(it.R, it.R.sharedMaterial, 0, 0);
            }
            RunsDrawn++; MembersDrawn += count;
            // 합치기 사각형: 묶음 첫 장식의 정렬 자리
            while (comps.Count <= idx)
            {
                if (!quad)
                {
                    quad = new Mesh { name = "StutterFix.RunLod quad" };
                    quad.SetVertices(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(3, 0, 0) });
                    quad.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0, false);
                }
                var g = new GameObject("StutterFix.RunLod composite") { hideFlags = HideFlags.HideAndDontSave };
                var mf = g.AddComponent<MeshFilter>(); mf.sharedMesh = UnityEngine.Object.Instantiate(quad);
                var mat = new Material(shader); compMats.Add(mat);
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                comps.Add(g);
            }
            var go = comps[idx]; if (!go.activeSelf) go.SetActive(true);
            var first = items[start];
            go.layer = first.C.Layer;
            var rend = go.GetComponent<MeshRenderer>();
            rend.sortingLayerID = first.R.sortingLayerID; rend.sortingOrder = first.R.sortingOrder;
            var cmat = compMats[idx]; cmat.renderQueue = first.Q; cmat.mainTexture = rt; cmat.SetVector("_LodTarget", new Vector4(w, h, 1f / w, 1f / h));
            // 합치기 사각형은 묶음이 덮는 화면 범위만(저해상도 4픽셀 여유). 그림(RT)에 그릴 때 유니티는 투영의 위아래를 뒤집는다.
            float mx = 4f * div / w, my = 4f * div / h;
            float cx0 = Mathf.Clamp01(rx0 - mx) * 2 - 1, cx1 = Mathf.Clamp01(rx1 + mx) * 2 - 1;
            float vy0 = Mathf.Clamp01(ry0 - my) * 2 - 1, vy1 = Mathf.Clamp01(ry1 + my) * 2 - 1;
            bool flip = cam.targetTexture != null;
            float cy0 = flip ? -vy1 : vy0, cy1 = flip ? -vy0 : vy1;
            cmat.SetVector("_Rect", new Vector4(cx0, cy0, cx1, cy1));
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.bounds = new Bounds(go.transform.InverseTransformPoint(first.R.bounds.center), new Vector3(1e6f, 1e6f, 0));
        }
    }
}
#endif
