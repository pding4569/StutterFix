#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // 형태 칸: 투명 여백이 많은 텍스처(속이 빈 윤곽선·가는 삼각형 등)를 칸 사각형 몇 개로만 그린다.
    // 원래 렌더러는 사각형 전체를 래스터한다. 알파>0 텍셀에서 여백 2 텍셀 밖의 칸은 어떤 샘플도 알파 0 이라
    // 어느 섞기 모드에서도 화면을 바꾸지 않으므로 그 칸을 그리지 않아도 화면이 같다. 래스터·픽셀 셰이더 실행 수만 준다.
    // DD 55초: 화면 면적의 95% 가 thin_triangle.png(알파>0 1.2%)였고 16x16 칸이면 면적 19.7% 만 남는다.
    internal static partial class DecoBake
    {
        static readonly Dictionary<int, Vector4[]> gpuShapeRects = new Dictionary<int, Vector4[]>();   // 텍스처 → 칸 사각형(UV u0,v0,u1,v1). 줄일 가치가 없으면 빈 배열
        static int[] gpuItemShapeStart, gpuItemShapeCount;   // 항목 → 기록 표 안의 시작(0부터)·개수(0 = 형태 없음)
        static Vector4[] gpuShapeTable = Array.Empty<Vector4>();
        static uint[] gpuSlotBits;   // 자리 → 지도 값 높은 비트(형태 기록 번호 << 18)
        static int gpuShapeItems, gpuShapeRectsUsed, gpuShapeActors;
        static readonly Dictionary<(int, int), int> gpuShapeKeyStart = new Dictionary<(int, int), int>();   // (텍스처, 자리 수 K) → 기록 시작
        const int ShapeMaxRecords = 16383, ShapeMaxItems = 0x3FFFF;

        static Vector4[] ShapeRectsFor(ComputeShader cs, Texture2D t)
        {
            int id = t.GetInstanceID();
            if (gpuShapeRects.TryGetValue(id, out var cached)) return cached;
            var none = Array.Empty<Vector4>();
            int side = Math.Max(t.width, t.height);
            if (!cs || !cs.HasKernel("AlphaCells") || side < DrawCollapsePolicy.ShapeMinSide || t.mipmapCount != 1) { gpuShapeRects[id] = none; return none; }
            int G = side >= 256 ? DrawCollapsePolicy.ShapeGrid : Math.Max(4, DrawCollapsePolicy.ShapeGrid / 2);
            int k = cs.FindKernel("AlphaCells");
            var buf = new ComputeBuffer(G * G, 4); var cells = new uint[G * G];
            try
            {
                buf.SetData(cells);
                cs.SetTexture(k, "_TrimTex", t); cs.SetBuffer(k, "_CellOut", buf);
                cs.SetInts("_TrimSize", t.width, t.height); cs.SetInt("_CellGrid", G); cs.SetInt("_CellMargin", 2);
                cs.Dispatch(k, (t.width + 15) / 16, (t.height + 15) / 16, 1);
                buf.GetData(cells);
            }
            finally { buf.Release(); }
            // 가로로 이어진 칸 → 같은 범위면 아래 줄과 합친다
            var open = new List<int[]>(); var rects = new List<int[]>(); // x0,x1,y0,y1 (칸 단위, x1·y1 포함)
            for (int y = 0; y < G; y++)
            {
                var runs = new List<int[]>();
                for (int x = 0; x < G; x++) if (cells[y * G + x] != 0) { int x0 = x; while (x + 1 < G && cells[y * G + x + 1] != 0) x++; runs.Add(new[] { x0, x }); }
                var next = new List<int[]>();
                foreach (var r in runs)
                {
                    var o = open.Find(q => q[0] == r[0] && q[1] == r[1]);
                    if (o != null) { open.Remove(o); o[3] = y; next.Add(o); } else next.Add(new[] { r[0], r[1], y, y });
                }
                rects.AddRange(open); open = next;
            }
            rects.AddRange(open);
            long kept = 0; foreach (var r in rects) kept += (long)(r[1] - r[0] + 1) * (r[3] - r[2] + 1);
            if (rects.Count == 0 || rects.Count > DrawCollapsePolicy.ShapeMaxRects || kept > DrawCollapsePolicy.ShapeMaxKeep * G * G) { gpuShapeRects[id] = none; return none; }
            var result = new Vector4[rects.Count];
            for (int i = 0; i < rects.Count; i++) { var r = rects[i]; result[i] = new Vector4(r[0] / (float)G, r[2] / (float)G, (r[1] + 1) / (float)G, (r[3] + 1) / (float)G); }
            gpuShapeRects[id] = result; return result;
        }

        // 준비(PrepareDrawCollapse) 때: 맡을 항목마다 형태 칸 기록을 정한다. 기록은 텍스처마다 한 벌(같은 텍스처 항목끼리 공유).
        static void PrepareShapes(GpuLiveSlice source)
        {
            int n = source.Data.Items.Length;
            gpuItemShapeStart = new int[n]; gpuItemShapeCount = new int[n]; gpuShapeItems = 0; gpuShapeRectsUsed = 0; gpuShapeActors = 0;
            gpuShapeTable = Array.Empty<Vector4>();
            if (!DrawCollapsePolicy.ShapeCells || !DrawCollapsePolicy.GlobalVerts || n >= ShapeMaxItems || !gpuLiveShader || !gpuLiveShader.HasKernel("AlphaCells")) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            // 고정 기록: 1 = 사각형 전체, 2 = 빈 칸(이미지가 준비 안 한 텍스처로 바뀌면 첫 자리 전체 + 나머지 빈 칸 = 원래 그림)
            var table = new List<Vector4> { new Vector4(0, 0, 1, 1), new Vector4(0, 0, 0, 0) }; var texStart = new Dictionary<int, int>(); gpuShapeKeyStart.Clear();
            var why = new Dictionary<string, int>();
            void W(string k, Texture2D tx) { string key = k + ":" + (tx ? tx.name : "-"); why.TryGetValue(key, out int c0); why[key] = c0 + 1; }
            for (int i = 0; i < n; i++)
            {
                var row = gpuCollapseOwnedRows[i]; var sr = row?.R as SpriteRenderer; var it = source.Data.Items[i];
                if (!sr || !sr.sprite || it == null) continue;
                int master = source.MasterItems[i];
                var tx0 = sr.sprite.texture;
                if ((uint)master < (uint)gpuMasterDepths.Length && gpuMasterDepths[master] != null) { W("depth", tx0); continue; }   // 깊이 인스턴스 항목은 아직 제외
                bool actor = (uint)master < (uint)gpuLiveOwnedIndices.Length && GpuImageActor(gpuLiveOwnedIndices[master]);   // 이미지가 바뀌는 항목: 칸은 텍스처마다 다름
                if (DrawCollapsePolicy.Repeat && (it.RepeatX != 1f || it.RepeatY != 1f)) { W("repeat", tx0); continue; }
                if (!ShapeUvIsRect(it.UV)) { W("uv", tx0); continue; }
                var tex = sr.sprite.texture; if (!tex || tex.GetInstanceID() != it.TextureId) continue;
                // 이 항목이 곡 중 가질 수 있는 텍스처들(이미지가 바뀌는 항목은 이미지 이벤트의 변형까지)
                var texs = new List<Texture2D> { tex };
                if (actor && gpuImageTimeline != null && gpuImageVariants != null)
                    foreach (int e in gpuImageTimeline.EventsForDecoration(gpuLiveOwnedIndices[master]))
                    {
                        var vv = gpuImageVariants[gpuImageTimeline.Event(e).Selector];
                        var vt = vv != null && vv.Status == GpuDecoImageVariant.Kind.ReadyQuad && vv.Source != null && vv.Source.sprite ? vv.Source.sprite.texture : null;
                        if (vt && !texs.Contains(vt)) texs.Add(vt);
                    }
                int K = 0; bool any = false;
                var sets = new List<Vector4[]>();
                foreach (var tx in texs) { var r0 = ShapeRectsFor(gpuLiveShader, tx); if (r0.Length > 0) any = true; else r0 = new[] { new Vector4(0, 0, 1, 1) }; sets.Add(r0); K = Math.Max(K, r0.Length); }
                if (!any) { W(actor ? "actor-norects" : "norects", tex); continue; }
                if (K > DrawCollapsePolicy.ShapeMaxRects) { W("toomany", tex); continue; }
                bool fit = true;
                for (int q = 0; q < texs.Count; q++)
                {
                    var key = (texs[q].GetInstanceID(), K);
                    if (gpuShapeKeyStart.ContainsKey(key)) continue;
                    if (table.Count + K > ShapeMaxRecords) { fit = false; break; }
                    gpuShapeKeyStart[key] = table.Count; table.AddRange(sets[q]); for (int pad = sets[q].Length; pad < K; pad++) table.Add(new Vector4(0, 0, 0, 0));
                    texStart[texs[q].GetInstanceID()] = 1;
                }
                if (!fit) { W("table-full", tex); continue; }
                gpuItemShapeStart[i] = gpuShapeKeyStart[(tex.GetInstanceID(), K)]; gpuItemShapeCount[i] = K; gpuShapeItems++;
                if (actor) gpuShapeActors++;
            }
            gpuShapeTable = table.ToArray(); gpuShapeRectsUsed = table.Count;
            GpuLog("SHAPE_CELLS_SKIP " + string.Join(" | ", System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(why, x => x.Value), 10)));
            GpuLog("SHAPE_CELLS actors=" + gpuShapeActors + " items=" + gpuShapeItems + " records=" + table.Count + " textures=" + texStart.Count + " ms=" + ((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F1"));
        }
        // 이미지가 바뀌었다(UpdateDrawCollapseTextureSlot): 그 텍스처의 칸 기록으로 자리들을 바꾼다. 준비 안 한 텍스처면 첫 자리 전체 + 나머지 빈 칸.
        static void UpdateShapeForTexture(int item, Texture2D tex)
        {
            if (gpuItemShapeCount == null || (uint)item >= (uint)gpuItemShapeCount.Length || gpuItemShapeCount[item] == 0) return;
            int K = gpuItemShapeCount[item];
            int start = tex && gpuShapeKeyStart.TryGetValue((tex.GetInstanceID(), K), out int s0) ? s0 : -1;
            gpuItemShapeStart[item] = start;
            if (gpuItemSlots == null || gpuSlotBits == null || !gpuItemSlots.TryGetValue(item, out var slots)) return;
            for (int j = 0; j < slots.Count && j < K; j++) { int rec = start >= 0 ? start + j + 1 : (j == 0 ? 1 : 2); gpuSlotBits[slots[j]] = (uint)rec << 18; }
            SyncG1CollapseItem(item);
        }
        // 네 모서리가 UV 범위의 네 꼭짓점에 정확히 놓인 사각형일 때만(쌍선형 = 아핀 가정)
        static bool ShapeUvIsRect(float[] uv)
        {
            if (uv == null || uv.Length != 8) return false;
            float uA = Mathf.Min(Mathf.Min(uv[0], uv[2]), Mathf.Min(uv[4], uv[6])), uB = Mathf.Max(Mathf.Max(uv[0], uv[2]), Mathf.Max(uv[4], uv[6]));
            float vA = Mathf.Min(Mathf.Min(uv[1], uv[3]), Mathf.Min(uv[5], uv[7])), vB = Mathf.Max(Mathf.Max(uv[1], uv[3]), Mathf.Max(uv[5], uv[7]));
            float du = uB - uA, dv = vB - vA; if (du <= 1e-5f || dv <= 1e-5f) return false;
            int mask = 0;
            for (int k = 0; k < 4; k++)
            {
                float s = (uv[k * 2] - uA) / du, t = (uv[k * 2 + 1] - vA) / dv;
                if ((s > 1e-4f && s < 1 - 1e-4f) || (t > 1e-4f && t < 1 - 1e-4f)) return false;
                mask |= 1 << ((s > .5f ? 1 : 0) | (t > .5f ? 2 : 0));
            }
            return mask == 15;
        }
        // 계획 뒤: 형태 칸 항목을 기록 수만큼 자리로 늘린다(같은 정렬 키, 칸끼리는 겹치지 않음). recs: 자리마다 기록 번호(1부터, 0 = 전체)
        static List<int[]> ExpandShapeSpans(List<int[]> plan, out List<int[]> recs)
        {
            recs = null;
            if (gpuItemShapeCount == null || gpuShapeItems == 0) return plan;
            recs = new List<int[]>(plan.Count); var outPlan = new List<int[]>(plan.Count);
            foreach (var items in plan)
            {
                var ids = new List<int>(items.Length); var rs = new List<int>(items.Length);
                foreach (int id in items)
                {
                    int c = id < gpuInstBase && (uint)id < (uint)gpuItemShapeCount.Length ? gpuItemShapeCount[id] : 0;
                    if (c == 0) { ids.Add(id); rs.Add(0); continue; }
                    for (int j = 0; j < c; j++) { ids.Add(id); rs.Add(gpuItemShapeStart[id] >= 0 ? gpuItemShapeStart[id] + j + 1 : (j == 0 ? 1 : 2)); }
                }
                outPlan.Add(ids.ToArray()); recs.Add(rs.ToArray());
            }
            return outPlan;
        }
    }
}
#endif
