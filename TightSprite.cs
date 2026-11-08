using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // GPU 덧칠 줄이기(눈으로 같게): 화면을 크게 덮지만 대부분 투명한 장식 그림(예: HELLO 의 얇은 원 고리, 알파>0 3.5%)의
    // 사각형 도형을 "알파가 있는 칸(격자, 한 칸 넓힘)만 덮는 도형"으로 바꾼다. 칠하는 픽셀 수 자체가 준다.
    // 도형이 바뀌면 GPU 의 UV 보간이 미세하게 달라져(부동소수점 반올림) 테두리 근처 일부 픽셀이 1~3/255 다를 수 있다.
    internal static class TightSprite
    {
        internal static int Grid = 128;
        internal static float MaxCoverage = 0.5f;
        internal static bool UnityTight; // tightshape unity <크기>: 유니티 Tight 외곽선(구멍 포함, 정점 적음)을 줄인 점유 그림에서 만든다
        internal const string TargetShader = "Sprites/Tile";
        sealed class Orig { public Vector2[] V; public ushort[] T; public float Cover; }
        static readonly Dictionary<Sprite, Orig> changed = new Dictionary<Sprite, Orig>();
        static readonly Dictionary<Texture, bool[,]> occCache = new Dictionary<Texture, bool[,]>();
        static readonly Dictionary<Texture, float> covCache = new Dictionary<Texture, float>();

        static bool[,] Occupancy(Sprite sp, out float cover)
        {
            var tex = sp.texture;
            if (occCache.TryGetValue(tex, out var cached)) { cover = covCache[tex]; return cached; }
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var cpu = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
            cpu.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = cpu.GetPixels32(); Object.Destroy(cpu);
            var r = sp.textureRect; int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height, tw = tex.width;
            var occ = new bool[Grid, Grid];
            for (int y = 0; y < h; y++)
            {
                int gy = y * Grid / h, row = (y0 + y) * tw + x0;
                // 읽어 온 텍스처의 위아래 방향을 확신할 수 없으므로 정방향과 뒤집은 방향을 모두 덮는다(실제 그림이 잘리지 않게)
                for (int x = 0; x < w; x++) if (px[row + x].a != 0) { occ[x * Grid / w, gy] = true; occ[x * Grid / w, Grid - 1 - gy] = true; }
            }
            var dil = new bool[Grid, Grid]; int n = 0;
            for (int gx = 0; gx < Grid; gx++)
                for (int gy = 0; gy < Grid; gy++)
                {
                    bool any = false;
                    for (int dx = -1; dx <= 1 && !any; dx++)
                        for (int dy = -1; dy <= 1 && !any; dy++)
                        {
                            int xx = gx + dx, yy = gy + dy;
                            if (xx >= 0 && yy >= 0 && xx < Grid && yy < Grid && occ[xx, yy]) any = true;
                        }
                    dil[gx, gy] = any; if (any) n++;
                }
            cover = n / (float)(Grid * Grid);
            occCache[tex] = dil; covCache[tex] = cover;
            return dil;
        }

        static bool Build(Sprite sp, bool[,] occ, out Vector2[] v, out ushort[] t)
        {
            var r = sp.rect; float cw = r.width / Grid, ch = r.height / Grid;
            var vl = new List<Vector2>(); var tl = new List<ushort>();
            for (int gy = 0; gy < Grid; gy++)
            {
                int gx = 0;
                while (gx < Grid)
                {
                    if (!occ[gx, gy]) { gx++; continue; }
                    int s = gx; while (gx < Grid && occ[gx, gy]) gx++;
                    if (vl.Count + 4 > 65535) { v = null; t = null; return false; }
                    ushort b = (ushort)vl.Count;
                    float xa = s * cw, xb = gx * cw, ya = gy * ch, yb = (gy + 1) * ch;
                    vl.Add(new Vector2(xa, ya)); vl.Add(new Vector2(xb, ya)); vl.Add(new Vector2(xb, yb)); vl.Add(new Vector2(xa, yb));
                    tl.Add(b); tl.Add((ushort)(b + 1)); tl.Add((ushort)(b + 2)); tl.Add(b); tl.Add((ushort)(b + 2)); tl.Add((ushort)(b + 3));
                }
            }
            v = vl.ToArray(); t = tl.ToArray();
            return vl.Count > 0;
        }

        internal static string Apply()
        {
            int applied = 0, wide = 0, other = 0;
            var seen = new HashSet<Sprite>();
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var sp = sr ? sr.sprite : null;
                if (!sp || seen.Contains(sp) || changed.ContainsKey(sp)) continue;
                var m = sr.sharedMaterial;
                if (!m || !m.shader || m.shader.name != TargetShader || !sr.GetComponentInParent<scrVisualDecoration>()) continue;
                seen.Add(sp);
                if (m.GetFloat("RepeatX") != 1f || m.GetFloat("RepeatY") != 1f || sr.drawMode != SpriteDrawMode.Simple || sp.packed) { other++; continue; }
                var occ = Occupancy(sp, out float cov);
                if (cov > MaxCoverage) { wide++; continue; }
                Vector2[] v; ushort[] t;
                if (UnityTight ? !BuildUnity(sp, occ, out v, out t) : !Build(sp, occ, out v, out t)) { other++; continue; }
                // 원래 도형(로컬 단위)을 사각형 공간 픽셀로 바꿔 보관
                var lv = sp.vertices; var ov = new Vector2[lv.Length]; float ppu = sp.pixelsPerUnit; var piv = sp.pivot;
                for (int i = 0; i < lv.Length; i++) ov[i] = new Vector2(lv[i].x * ppu + piv.x, lv[i].y * ppu + piv.y);
                changed[sp] = new Orig { V = ov, T = sp.triangles, Cover = cov };
                sp.OverrideGeometry(v, t);
                applied++;
            }
            string covs = "";
            foreach (var kv in covCache) covs += " " + kv.Key.name + "=" + (kv.Value * 100).ToString("F0") + "%";
            return "도형 바꾼 스프라이트 " + applied + " / 덮개 넓음 " + wide + " / 조건 밖 " + other + " |" + covs;
        }

        // 점유 칸(한 칸 넓힘)을 작은 그림으로 만들어 유니티의 Tight 도형 생성기에 맡긴다. 정점 좌표는 원래 사각형 텍셀 공간으로 늘린다.
        static bool BuildUnity(Sprite sp, bool[,] occ, out Vector2[] v, out ushort[] t)
        {
            v = null; t = null;
            var small = new Texture2D(Grid, Grid, TextureFormat.RGBA32, false, true);
            var px = new Color32[Grid * Grid];
            for (int gy = 0; gy < Grid; gy++) for (int gx = 0; gx < Grid; gx++) px[gy * Grid + gx] = occ[gx, gy] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            small.SetPixels32(px); small.Apply(false, false);
            Sprite tmp = null;
            try
            {
                tmp = Sprite.Create(small, new Rect(0, 0, Grid, Grid), Vector2.zero, 1f, 0, SpriteMeshType.Tight);
                var lv = tmp.vertices; var lt = tmp.triangles;
                if (lv.Length < 3 || lt.Length < 3 || lv.Length > 65535) return false;
                var r = sp.rect; float sx = r.width / Grid, sy = r.height / Grid;
                v = new Vector2[lv.Length];
                // 작은 스프라이트는 pivot 0, ppu 1 이라 정점 = 작은 그림 픽셀 좌표
                for (int i = 0; i < lv.Length; i++) v[i] = new Vector2(Mathf.Clamp(lv[i].x * sx, 0, r.width), Mathf.Clamp(lv[i].y * sy, 0, r.height));
                t = lt;
                return true;
            }
            catch { return false; }
            finally { if (tmp) Object.Destroy(tmp); Object.Destroy(small); }
        }

        internal static string Restore()
        {
            int n = 0;
            foreach (var kv in changed) { if (kv.Key) { try { kv.Key.OverrideGeometry(kv.Value.V, kv.Value.T); n++; } catch { } } }
            changed.Clear();
            return "되돌린 스프라이트 " + n;
        }

        internal static void ForgetLevel() { changed.Clear(); occCache.Clear(); covCache.Clear(); }
    }
}
