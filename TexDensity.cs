#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // 진단: 보이는 장식 스프라이트마다 "텍스처 픽셀 / 화면에서 차지하는 픽셀" 비율을 잰다.
    // 비율이 1보다 크면 축소되어 그려진다(텍스처가 화면보다 크다). 곡 중 한 프레임만, 할당 있음(측정 창에서 제외).
    internal static class TexDensity
    {
        internal static void Run(string tag)
        {
            var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
            if (!cam) { Main.Entry.Logger.Log("[texdensity] 본 카메라 없음"); return; }
            float sw = cam.targetTexture ? cam.targetTexture.width : Screen.width, sh = cam.targetTexture ? cam.targetTexture.height : Screen.height;
            var per = new Dictionary<string, List<float>>();
            var area = new Dictionary<string, double>();
            var tight = new Dictionary<string, string>();
            var orders = new Dictionary<string, HashSet<long>>(); var perOrder = new Dictionary<long, int>();
            int n = 0;
            foreach (var sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!sr.enabled || sr.forceRenderingOff || !sr.sprite || !sr.isVisible) continue;
                if (sr.color.a <= 0f) continue;
                var tex = sr.sprite.texture; if (!tex) continue;
                var b = sr.bounds;
                Vector3 a0 = cam.WorldToScreenPoint(b.min), a1 = cam.WorldToScreenPoint(b.max);
                float wpx = Mathf.Abs(a1.x - a0.x), hpx = Mathf.Abs(a1.y - a0.y);
                if (wpx < 1 || hpx < 1) continue;
                // 스프라이트가 쓰는 텍스처 영역(아틀라스면 rect)
                var r = sr.sprite.textureRect;
                float ratio = Mathf.Max(r.width / wpx, r.height / hpx);
                string key = tex.name + " " + tex.width + "x" + tex.height + (tex.mipmapCount > 1 ? " mip" : "");
                if (!per.TryGetValue(key, out var l)) { l = new List<float>(); per[key] = l; area[key] = 0; }
                l.Add(ratio);
                long ok = ((long)sr.sortingLayerID << 32) ^ (uint)sr.sortingOrder;
                if (!orders.TryGetValue(key, out var os)) orders[key] = os = new HashSet<long>(); os.Add(ok);
                if (!tight.ContainsKey(key))
                {
                    // 스프라이트 도형(Tight 면 그림 모양 다각형)의 넓이 / 사각형 넓이
                    var sv = sr.sprite.vertices; var st = sr.sprite.triangles; double pa = 0;
                    for (int q = 0; q + 2 < st.Length; q += 3) { var p0 = sv[st[q]]; var p1 = sv[st[q + 1]]; var p2 = sv[st[q + 2]]; pa += Mathf.Abs((p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y)) * 0.5; }
                    var bs = sr.sprite.bounds.size; double ra = (double)bs.x * bs.y;
                    tight[key] = " 정점 " + sv.Length + " 도형/사각형 " + (ra > 0 ? (pa / ra).ToString("F3", CultureInfo.InvariantCulture) : "?");
                }
                float cw = Mathf.Min(wpx, sw), ch = Mathf.Min(hpx, sh);
                area[key] += (double)cw * ch / (sw * sh);
                n++;
            }
            Main.Entry.Logger.Log(string.Format(CultureInfo.InvariantCulture, "[texdensity] {0} 보이는 스프라이트 {1}개, 화면 {2}x{3}", tag, n, sw, sh));
            foreach (var rr in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!rr.enabled || rr.forceRenderingOff || !rr.isVisible) continue;
                long ok = ((long)rr.sortingLayerID << 32) ^ (uint)rr.sortingOrder;
                perOrder.TryGetValue(ok, out int c); perOrder[ok] = c + 1;
            }
            foreach (var kv in per.OrderByDescending(k => area[k.Key]).Take(15))
            {
                var v = kv.Value; v.Sort();
                Main.Entry.Logger.Log(string.Format(CultureInfo.InvariantCulture, "[texdensity] {0} | 개수 {1} 화면덮음 {2:F1}장 | 텍스처/화면 비율 최소 {3:F2} 중앙 {4:F2} 최대 {5:F2}{6}",
                    kv.Key, v.Count, area[kv.Key], v[0], v[v.Count / 2], v[v.Count - 1], tight[kv.Key] + OrderInfo(orders[kv.Key], perOrder, v.Count)));
            }
        }
        static string OrderInfo(HashSet<long> os, Dictionary<long, int> perOrder, int own)
        {
            int all = 0; var parts = new List<string>();
            foreach (var o in os) { perOrder.TryGetValue(o, out int c); all += c; if (parts.Count < 4) parts.Add((int)(uint)o + ":" + c); }
            return " | 순서 " + os.Count + "종(" + string.Join(",", parts) + ") 같은 순서의 보이는 렌더러 " + all + " (자기 " + own + ")";
        }
    }
}
#endif
