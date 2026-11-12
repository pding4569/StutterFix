#if AUTOTEST
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // 측정 전용: 이 프레임에 장식이 칠하는 화면 넓이(화면 장수, 화면 안으로 자른 사각형 기준)를 센다.
    // 원래 렌더러 경로와 GpuDeco 구간 경로를 같은 시점에 비교해, 어느 쪽이 더 넓게 칠하는지 본다.
    internal static partial class DecoBake
    {
        internal static string CoverageProbe()
        {
            var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
            if (!cam) return "본 카메라 없음";
            var vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            double native = 0; int nativeN = 0;
            double nativeUsed = 0, unknownArea = 0; var wasteTop = new System.Collections.Generic.Dictionary<string, double>();
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!sr.enabled || sr.forceRenderingOff || !sr.isVisible || !sr.sprite || sr.color.a <= 0) continue;
                if (!sr.GetComponentInParent<scrDecoration>()) continue;
                var b = sr.bounds;
                double ar0 = RectArea(vp, new[] { new Vector3(b.min.x, b.min.y, b.center.z), new Vector3(b.max.x, b.min.y, b.center.z), new Vector3(b.max.x, b.max.y, b.center.z), new Vector3(b.min.x, b.max.y, b.center.z) });
                native += ar0;
                nativeN++;
                // 그림의 빈 칸(알파 0) 비율: 해독 때 만든 형태 칸으로 추정(그림 전체 기준, 화면에 걸친 부분만의 비율은 아님)
                double occ = 1; var tx0 = sr.sprite.texture;
                if (tx0 && ShapeCellsCpu.TryGet(tx0.GetInstanceID(), out var cellBits) && cellBits != null)
                { int on = 0; foreach (var wd in cellBits) { ulong v = wd; while (v != 0) { v &= v - 1; on++; } } occ = (double)on / (cellBits.Length * 64); }
                else if (tx0) unknownArea += ar0;
                nativeUsed += ar0 * occ;
                if (tx0 && occ < 1) { string wk = tx0.name + " " + tx0.width + "x" + tx0.height + " 채움 " + occ.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); wasteTop.TryGetValue(wk, out double pw); wasteTop[wk] = pw + ar0 * (1 - occ); }
            }
            double gpu = 0; int gpuN = 0; double zeroA = 0; int zeroN = 0; var top = new System.Collections.Generic.Dictionary<string, double>();
            if (gpuCollapseOn && gpuCollapseAllVerts != null && gpuCollapseAllCount > 0)
            {
                var raw = new float[gpuCollapseAllCount * 60];
                gpuCollapseAllVerts.GetData(raw);
                var root = gpuCollapseSpans.Count > 0 && gpuCollapseSpans[0].Renderer ? gpuCollapseSpans[0].Renderer.localToWorldMatrix : Matrix4x4.identity;
                var pts = new Vector3[4];
                for (int s = 0; s < gpuCollapseAllCount; s++)
                {
                    bool nz = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int o = (s * 4 + k) * 15;
                        var p = new Vector3(raw[o], raw[o + 1], raw[o + 2]);
                        if (p != Vector3.zero) nz = true;
                        pts[k] = root.MultiplyPoint3x4(p);
                    }
                    if (nz && raw[(s * 4) * 15 + 6] <= 0) { zeroA += RectArea(vp, pts); zeroN++; }
                    if (!nz || raw[(s * 4) * 15 + 6] <= 0) continue;   // 정점 0 = 안 그림, 색 알파 0
                    double ar = RectArea(vp, pts); gpu += ar; gpuN++;
                    int itm = (int)(gpuCollapseMapCpu[s] & 0x3FFFFu);
                    if (itm >= 0 && itm < gpuCollapseOwnedRows.Length && gpuCollapseOwnedRows[itm] != null && gpuCollapseOwnedRows[itm].R is SpriteRenderer srr && srr.sprite)
                    { var tx = srr.sprite.texture; string k = tx.name + " " + tx.width + "x" + tx.height + (TryGetTrimBounds(tx, out var tb) ? " trim=" + ((tb.z - tb.x) * (tb.w - tb.y)).ToString("F2") : " notrim"); top.TryGetValue(k, out double pv); top[k] = pv + ar; }
                }
            }
            var wl = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(wasteTop); wl.Sort((x, y) => y.Value.CompareTo(x.Value));
            var wsb = new System.Text.StringBuilder();
            for (int wi = 0; wi < wl.Count && wi < 8; wi++) wsb.Append(wl[wi].Key).Append(" 빈칸 ").Append(wl[wi].Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append("장; ");
            Main.Entry.Logger.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "[coverage2] 원래 장식 {0}개 사각형 {1:F1}장, 채워진 칸 기준 {2:F1}장(빈 칸 {3:F1}장, 형태 정보 없는 넓이 {4:F1}장) | 빈 칸 상위: {5}", nativeN, native, nativeUsed, native - nativeUsed, unknownArea, wsb));
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "원래 장식 렌더러 {0}개 {1:F1}장 | GpuDeco 구간 항목 {2}개 {3:F1}장 | 알파0인데 정점 있는 항목 {4}개 {5:F1}장 | 면적 상위: {6}", nativeN, native, gpuN, gpu, zeroN, zeroA, string.Join(" ; ", top.OrderByDescending(x => x.Value).Take(8).Select(x => x.Key + "=" + x.Value.ToString("F1"))));
        }

        // 네 점의 화면 사각형(축 정렬)을 화면 [0,1] 로 잘라 넓이(화면 장수)
        static double RectArea(Matrix4x4 vp, Vector3[] w)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in w) { var n = vp.MultiplyPoint(p); float x = n.x * .5f + .5f, y = n.y * .5f + .5f; x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            return Mathf.Max(0, Mathf.Min(1, x1) - Mathf.Max(0, x0)) * Mathf.Max(0, Mathf.Min(1, y1) - Mathf.Max(0, y0));
        }
    }
}
#endif
