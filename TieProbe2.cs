#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StutterFix
{
    // 측정 전용: 같은 (층,순서,큐,거리)에서 서로 다른 셰이더(Sprites/Tile, Blend Modes Grab, GpuDeco 구간 셰이더, Sprites/Default)의
    // 실제 그리기 순서를 잰다. 위에서부터 하나씩 끄며 맨 위 색을 읽는다(TieProbe 와 같은 방식).
    internal static class TieProbe2
    {
        const int Layer = 31;
        sealed class Row { public Renderer R; public Material M; public Color32 C; }
        // 두 장: Sprites/Tile(색 A) 과 Blend Modes Grab Difference(색 B). 결과가 A 면 Tile 이 나중, |A-B| 면 Grab 이 나중.
        internal static string RunPair()
        {
            var trash = new List<UnityEngine.Object>();
            T Own<T>(T o) where T : UnityEngine.Object { trash.Add(o); return o; }
            var goCam = Own(new GameObject("tp3-cam")); goCam.transform.position = new Vector3(6000, 6000, -10);
            var cam = goCam.AddComponent<Camera>(); cam.enabled = false; cam.orthographic = true; cam.orthographicSize = 1; cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.allowHDR = false; cam.allowMSAA = false;
            var rt = Own(new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); cam.targetTexture = rt;
            var read = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false));
            var all = Resources.FindObjectsOfTypeAll<Shader>();
            Shader find(string n) => Shader.Find(n) ?? all.FirstOrDefault(s => s.name == n);
            var tile = find("Sprites/Tile"); var grab = find("Hidden/BlendModes/SpritesDefault/Grab");
            if (!tile || !grab) return "missing shader";
            var rand = new System.Random(3); int tileTop = 0, grabTop = 0, other = 0; var sb = new StringBuilder();
            try
            {
                for (int trial = 0; trial < 12; trial++)
                {
                    var mt = Own(new Material(tile)); mt.renderQueue = 3000; mt.SetTexture("_MainTex", Texture2D.whiteTexture);
                    var mg = Own(new Material(grab)); mg.renderQueue = 3000; if (mg.HasProperty("_MainTex")) mg.SetTexture("_MainTex", Texture2D.whiteTexture);
                    mg.EnableKeyword("BLENDMODES_MODE_DIFFERENCE");
                    var A = new Color32(200, 60, 40, 255); var B = new Color32(50, 50, 200, 255);
                    var objs = new List<GameObject>();
                    bool tileFirst = rand.Next(2) == 0;
                    for (int k = 0; k < 2; k++)
                    {
                        bool isTile = (k == 0) == tileFirst;
                        var go = Own(new GameObject("tp3")); go.layer = Layer; go.transform.position = new Vector3(6000, 6000, 0);
                        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Own(Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 1));
                        sr.sharedMaterial = isTile ? mt : mg; sr.color = isTile ? (Color)A : (Color)B; sr.sortingOrder = 0;
                        go.transform.localScale = new Vector3(2, 2, 1); objs.Add(go);
                    }
                    cam.Render(); RenderTexture.active = rt; read.ReadPixels(new Rect(4, 4, 1, 1), 0, 0); read.Apply(false); RenderTexture.active = null;
                    Color32 p = read.GetPixel(0, 0);
                    int dA = Math.Abs(p.r - A.r) + Math.Abs(p.g - A.g) + Math.Abs(p.b - A.b);
                    if (dA <= 12) tileTop++; else grabTop++;
                    if (trial < 3) sb.Append(" px=" + p.r + "," + p.g + "," + p.b + " tileShader=" + tile.GetInstanceID() + " grabShader=" + grab.GetInstanceID() + " mt=" + mt.GetInstanceID() + " mg=" + mg.GetInstanceID() + " tileFirstCreated=" + tileFirst);
                    foreach (var go in objs) UnityEngine.Object.DestroyImmediate(go);
                }
            }
            finally { foreach (var o in trash) if (o) UnityEngine.Object.DestroyImmediate(o, true); }
            return "pair tile_on_top=" + tileTop + " grab_on_top=" + grabTop + sb;
        }
        internal static string Run(Shader extra)
        {
            var trash = new List<UnityEngine.Object>();
            T Own<T>(T o) where T : UnityEngine.Object { trash.Add(o); return o; }
            var sb = new StringBuilder();
            var goCam = Own(new GameObject("tieprobe2-cam")); goCam.transform.position = new Vector3(5000, 5000, -10);
            var cam = goCam.AddComponent<Camera>(); cam.enabled = false; cam.orthographic = true; cam.orthographicSize = 1; cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.allowHDR = false; cam.allowMSAA = false;
            var rt = Own(new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); cam.targetTexture = rt;
            var read = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false));
            var all = Resources.FindObjectsOfTypeAll<Shader>();
            Shader find(string n) => Shader.Find(n) ?? all.FirstOrDefault(s => s.name == n);
            var shaders = new List<Shader> { find("Sprites/Tile"), find("Hidden/BlendModes/SpritesDefault/Grab"), find("Sprites/Default"), extra }.Where(x => x).ToList();
            foreach (var s in shaders) sb.Append("shader ").Append(s.name).Append(" id=").Append(s.GetInstanceID()).Append(" | ");
            var rand = new System.Random(7); var rows = new List<Row>();
            var tally = new Dictionary<string, int>();
            try
            {
                for (int t = 0; t < 10; t++)
                {
                    var mats = new List<Material>();
                    foreach (var s in shaders) for (int k = 0; k < 2; k++) { var m = Own(new Material(s)); m.renderQueue = 3000; if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture); mats.Add(m); }
                    var order = mats.OrderBy(_ => rand.Next()).ToList();
                    for (int i = 0; i < order.Count; i++)
                    {
                        var go = Own(new GameObject("tp2")); go.layer = Layer; go.transform.position = new Vector3(5000, 5000, 0);
                        var mesh = Own(new Mesh());
                        mesh.vertices = new[] { new Vector3(-2, -2), new Vector3(2, -2), new Vector3(-2, 2), new Vector3(2, 2) };
                        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                        var c = new Color32((byte)(20 + i * 25), (byte)(235 - i * 25), 128, 255); mesh.colors32 = new[] { c, c, c, c };
                        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                        go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = order[i];
                        rows.Add(new Row { R = r, M = order[i], C = c });
                    }
                    var live = new List<Row>(rows); var drawn = new List<Row>(); bool fail = false;
                    while (live.Count > 0)
                    {
                        cam.Render(); RenderTexture.active = rt; read.ReadPixels(new Rect(4, 4, 1, 1), 0, 0); read.Apply(false); RenderTexture.active = null;
                        Color32 p = read.GetPixel(0, 0); int best = -1, bd = int.MaxValue;
                        for (int i = 0; i < live.Count; i++) { var c = live[i].C; int d = Math.Abs(c.r - p.r) + Math.Abs(c.g - p.g); if (d < bd) { bd = d; best = i; } }
                        if (bd > 16) { fail = true; break; }
                        drawn.Add(live[best]); live[best].R.enabled = false; live.RemoveAt(best);
                    }
                    drawn.Reverse();   // 아래 -> 위
                    if (fail) { tally.TryGetValue("FAIL", out int f0); tally["FAIL"] = f0 + 1; }
                    else
                    {
                        string seq = string.Join(">", drawn.Select(r => r.M.shader.name.Split('/').Last()));
                        // 셰이더 단위 순서만 남김(같은 셰이더 두 개는 하나로)
                        var sh = new List<string>(); foreach (var r in drawn) { var n = r.M.shader.name.Split('/').Last(); if (sh.Count == 0 || sh[sh.Count - 1] != n) sh.Add(n); }
                        string k = string.Join(" < ", sh); tally.TryGetValue(k, out int v); tally[k] = v + 1;
                    }
                    foreach (var r in rows) UnityEngine.Object.DestroyImmediate(r.R.gameObject); rows.Clear();
                }
            }
            finally { foreach (var o in trash) if (o) UnityEngine.Object.DestroyImmediate(o, true); }
            foreach (var kv in tally) sb.Append(" [").Append(kv.Key).Append("]=").Append(kv.Value);
            return sb.ToString();
        }
    }
}
#endif

