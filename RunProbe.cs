#if DEV || AUTOTEST
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // 측정 전용: 본 카메라에 보이는 렌더러를 그리기 순서(정렬층 값, 순서, 큐, 거리)로 줄 세우고,
    // 확대 배율이 큰 이미지 장식이 연속으로 몇 개씩 붙는지(묶음 LOD 가능성) 센다.
    internal static class RunProbe
    {
        internal static string Run(float minMag)
        {
            var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
            if (!cam) return "본 카메라 없음";
            var fwd = cam.transform.forward; var cp = cam.transform.position;
            var rows = new List<(int lv, int ord, int q, float dist, bool big, string tex)>();
            var other = new Dictionary<string, (int n, float cov)>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff || !r.isVisible || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                var m = r.sharedMaterial; if (!m) continue;
                bool big = false; string tex = "";
                if (r is SpriteRenderer sr && sr.sprite && m.shader && (m.shader.name == "Sprites/Tile" || m.shader.name == "Sprites/Default"))
                {
                    var b = sr.bounds; Vector3 a0 = cam.WorldToScreenPoint(b.min), a1 = cam.WorldToScreenPoint(b.max);
                    var tr = sr.sprite.textureRect;
                    float mag = Mathf.Min(Mathf.Abs(a1.x - a0.x) / tr.width, Mathf.Abs(a1.y - a0.y) / tr.height);
                    big = mag >= minMag; tex = sr.sprite.texture.name;
                }
                else
                {
                    // 후보가 아닌 렌더러 중 화면을 크게 덮는 것(셰이더·섞기별): 지원을 넓힐 가치가 있는지 본다
                    var bb = r.bounds; Vector3 c0 = cam.WorldToViewportPoint(bb.min), c1 = cam.WorldToViewportPoint(bb.max);
                    float cov = Mathf.Clamp01(Mathf.Abs(c1.x - c0.x)) * Mathf.Clamp01(Mathf.Abs(c1.y - c0.y));
                    if (cov > 0.25f)
                    {
                        string k = r.GetType().Name + ":" + (m.shader ? m.shader.name : "?") + (m.HasProperty("_SrcBlend") ? "/" + m.GetFloat("_SrcBlend") + "," + m.GetFloat("_DstBlend") : "");
                        other.TryGetValue(k, out var o); other[k] = (o.n + 1, o.cov + cov);
                    }
                }
                float dist = Vector3.Dot(r.bounds.center - cp, fwd);
                rows.Add((SortingLayer.GetLayerValueFromID(r.sortingLayerID), r.sortingOrder, m.renderQueue, dist, big, tex));
            }
            // 투명 큐만(2500 초과), 같은 키 안에서는 먼 것부터
            var t = rows.Where(x => x.q > 2500).OrderBy(x => x.q).ThenBy(x => x.lv).ThenBy(x => x.ord).ThenByDescending(x => x.dist).ToList();
            var runs = new List<int>(); int cur = 0, bigCount = 0;
            foreach (var x in t) { if (x.big) { cur++; bigCount++; } else { if (cur > 0) runs.Add(cur); cur = 0; } }
            if (cur > 0) runs.Add(cur);
            runs.Sort(); runs.Reverse();
            int saved = runs.Sum(k => k - 1);
            string others = string.Join("; ", other.OrderByDescending(kv => kv.Value.cov).Take(8).Select(kv => kv.Key + " " + kv.Value.n + "개 " + kv.Value.cov.ToString("F1", CultureInfo.InvariantCulture) + "장"));
            Main.Entry.Logger.Log("[runprobe] 후보 밖 큰 렌더러(화면 1/4 넘게 덮음): " + others);
            return string.Format(CultureInfo.InvariantCulture, "투명 렌더러 {0}, 확대 {1}배 이상 {2}개, 연속 묶음 {3}개 (길이 상위: {4}), 합치면 줄어드는 전체 화면 층 {5}",
                t.Count, minMag, bigCount, runs.Count, string.Join(",", runs.Take(20)), saved);
        }
    }
}
#endif
