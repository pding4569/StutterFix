#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // 정렬 순서상 사이에 아무것도 끼지 않은 비소유 타일 장식(Sprites/Tile, 반복) 레이어들을 한 패스로 합성한다.
    // 원본은 forceRenderingOff 로 숨기고, 그룹 첫 레이어와 같은 (층, 순서, 큐) 자리에 합성 렌더러 하나를 둔다.
    internal static partial class DecoBake
    {
        sealed class TileLayer
        {
            internal SpriteRenderer R; internal scrVisualDecoration D; internal Sprite Sp; internal Texture2D Tex; internal int Slot;
            internal GpuLiveRow Row; internal bool WasOff;
            internal Vector2 VMin, VSize; internal float Auv00, Auv11, Buvx, Buvy; internal float RepX, RepY;
            internal Vector4 SBounds = new Vector4(0, 0, 1, 1);
            internal bool FlipX, FlipY; internal Color Tint; internal float PlaneZ;
        }
        sealed class TileGroup
        {
            internal List<TileLayer> Layers = new List<TileLayer>();
            internal GameObject Go; internal MeshRenderer Mr; internal Material Mat; internal Mesh Mesh;
            internal Vector4[] P0 = new Vector4[8], P1 = new Vector4[8], P2 = new Vector4[8], P3 = new Vector4[8], Col = new Vector4[8];
        }
        static readonly List<TileGroup> tileGroups = new List<TileGroup>();
        static bool tileApplied, tileHooked;
        static string tileFault;
        static int tileFrames, tileDbg;

        static readonly Dictionary<string,int> tileWhy = new Dictionary<string,int>();
        static bool TWhy(string w){tileWhy.TryGetValue(w,out int n);tileWhy[w]=n+1;return false;}
        static bool TileIgnoredAux(GpuLiveRow row) => row.R is MeshRenderer && (!row.R.enabled || !row.R.gameObject.activeInHierarchy) && row.M && row.M.shader && row.M.shader.name.StartsWith("Hidden/BlendModes", StringComparison.Ordinal);
        static readonly MaterialPropertyBlock tileMpb = new MaterialPropertyBlock();
        static float TileRepeat(SpriteRenderer sr, int axis, float fallback)
        {
            if (!sr.HasPropertyBlock()) return fallback;
            sr.GetPropertyBlock(tileMpb);
            string n = axis == 0 ? "_RepeatX" : "_RepeatY";
            return tileMpb.HasProperty(n) ? tileMpb.GetFloat(n) : fallback;
        }
        // 정점 색(렌더러 색) * 재질 _Color(프로퍼티 블록이 덮으면 그 값)
        static Color TileTint(SpriteRenderer sr, Material m)
        {
            Color c = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            if (sr.HasPropertyBlock())
            {
                sr.GetPropertyBlock(tileMpb);
                if (tileMpb.HasProperty("_Color")) c = tileMpb.GetColor("_Color");
            }
            return sr.color * c;
        }
        static bool TileMemberOk(GpuLiveRow row, out TileLayer layer)
        {
            layer = null;
            var sr = row.R as SpriteRenderer;
            if (!sr) return TWhy("not_sprite"); if (!row.M || !row.M.shader) return TWhy("no_mat"); if (row.M.shader.name != "Sprites/Tile") return TWhy("shader:" + row.M.shader.name);
            if (!sr.enabled || !sr.gameObject.activeInHierarchy || sr.forceRenderingOff || !sr.sprite) return TWhy("inactive");
            var vd = sr.GetComponentInParent<scrVisualDecoration>();
            if (!vd) return TWhy("no_deco"); if (vd.repeatX == 1 && vd.repeatY == 1) return TWhy("norepeat");
            if (sr.drawMode != SpriteDrawMode.Simple) return TWhy("drawmode"); if (sr.maskInteraction != SpriteMaskInteraction.None) return TWhy("mask"); 
            if (vd.blendModeEffect && vd.blendModeEffect.enabled) return TWhy("blendmode");
            var sp = sr.sprite; var tex = sp.texture;
            if (!tex || sp.associatedAlphaSplitTexture) return TWhy("tex"); if (tex.mipmapCount != 1) return TWhy("mips"); if (tex.filterMode == FilterMode.Point) return TWhy("point"); if (tex.wrapMode != TextureWrapMode.Repeat) return TWhy("wrap:" + tex.wrapMode);
            if (row.M.HasProperty("_SrcBlend") && (row.M.GetFloat("_SrcBlend") != 1f || row.M.GetFloat("_DstBlend") != 10f)) return TWhy("blend");
            var vs = sp.vertices; var uv = sp.uv;
            if (vs.Length != 4 || uv.Length != 4 || sp.triangles.Length != 6) return TWhy("notquad");
            // 축 정렬 쿼드와 대각 UV 사상만 허용한다.
            float minx = vs.Min(v => v.x), maxx = vs.Max(v => v.x), miny = vs.Min(v => v.y), maxy = vs.Max(v => v.y);
            if (maxx - minx < 1e-6f || maxy - miny < 1e-6f) return TWhy("degenerate");
            float ux0 = uv.Min(v => v.x), ux1 = uv.Max(v => v.x), uy0 = uv.Min(v => v.y), uy1 = uv.Max(v => v.y);
            // UV 가 정점 위치의 직교 아핀 함수인지 정점 네 개로 확인한다.
            float auv00 = 0, auv11 = 0, bx = 0, by = 0;
            {
                float dxv = vs.Max(v => v.x) - vs.Min(v => v.x), dyv = vs.Max(v => v.y) - vs.Min(v => v.y);
                // x 에 대해: 정점 중 x 가 최소/최대인 것의 uv.x
                int iminx = Array.IndexOf(vs, vs.First(v => Mathf.Approximately(v.x, minx))), imaxx = Array.IndexOf(vs, vs.First(v => Mathf.Approximately(v.x, maxx)));
                int iminy = Array.IndexOf(vs, vs.First(v => Mathf.Approximately(v.y, miny))), imaxy = Array.IndexOf(vs, vs.First(v => Mathf.Approximately(v.y, maxy)));
                auv00 = (uv[imaxx].x - uv[iminx].x) / dxv; auv11 = (uv[imaxy].y - uv[iminy].y) / dyv;
                bx = uv[iminx].x - auv00 * minx; by = uv[iminy].y - auv11 * miny;
                for (int k = 0; k < 4; k++)
                    if (Mathf.Abs(auv00 * vs[k].x + bx - uv[k].x) > 1e-4f || Mathf.Abs(auv11 * vs[k].y + by - uv[k].y) > 1e-4f) return TWhy("uvmap");
            }
            layer = new TileLayer
            {
                R = sr, D = vd, Sp = sp, Tex = tex, Row = row, VMin = new Vector2(minx, miny), VSize = new Vector2(maxx - minx, maxy - miny),
                Auv00 = auv00, Auv11 = auv11, Buvx = bx, Buvy = by, RepX = TileRepeat(sr, 0, vd.repeatX), RepY = TileRepeat(sr, 1, vd.repeatY),
                FlipX = sr.flipX, FlipY = sr.flipY, PlaneZ = sr.transform.position.z
            };
            layer.Tint = TileTint(sr, row.M);
            return true;
        }

        static void TileCompositeApply()
        {
            if (tileApplied || !DrawCollapsePolicy.TileComposite || gpuLiveCamera == null || gpuLiveBundle == null) return;
            tileFault = null;
            try
            {
                var shader = gpuLiveBundle.LoadAsset<Shader>("Assets/Shaders/GpuTileComposite.shader");
                if (!shader || !shader.isSupported) { GpuLog("TILECOMP skip=shader_missing"); return; }
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                tileWhy.Clear();
                var members = new List<TileLayer>(); var memberRows = new HashSet<GpuLiveRow>();
                foreach (var row in gpuCollapseBarriers)
                    if (TileMemberOk(row, out var l)) { members.Add(l); memberRows.Add(row); }
                if (members.Count < 2) { GpuLog("TILECOMP skip=members<2 count=" + members.Count + " barriers=" + gpuCollapseBarriers.Count + " why=" + string.Join(",", tileWhy.OrderByDescending(k => k.Value).Take(10).Select(k => k.Key + ":" + k.Value))); return; }
                members.Sort((a, b) => a.Row.Key.CompareTo(b.Row.Key));
                // 사이에 낄 수 있는 다른 모든 것(활성이 아닌 장벽 포함)과 소유 구간의 (층,순서,큐) 범위
                var others = new List<(int l, int o, int q)>();
                // 멤버 장식 자신의 숨은 블렌드 모드용 메시 렌더러는 그리지 않으므로 끼어들기로 세지 않는다(블렌드 모드 효과가 꺼진 멤버만 허용했다).
                var memberDecos = new HashSet<scrVisualDecoration>(members.Select(m => m.D));
                int ownAux = 0;
                foreach (var row in gpuCollapseBarriers)
                {
                    if (memberRows.Contains(row)) continue;
                    // 꺼져 있는 블렌드 모드용 메시(Hidden/BlendModes/*)는 블렌드 모드 효과를 켜야만 그려진다. 이 맵 구간에서는 켜지지 않는다고 보고 끼어들기로 세지 않는다.
                    if (TileIgnoredAux(row)) { ownAux++; continue; }
                    others.Add((row.Key.Layer, row.Key.Order, row.Key.Queue));
                }
                foreach (var sp in gpuCollapseSpans) { others.Add((sp.First.Layer, sp.First.Order, sp.First.Queue)); others.Add((sp.Last.Layer, sp.Last.Order, sp.Last.Queue)); }
                others.Sort((a, b) => { int c = a.l.CompareTo(b.l); if (c != 0) return c; c = a.o.CompareTo(b.o); return c != 0 ? c : a.q.CompareTo(b.q); });
                int Cmp((int l, int o, int q) a, (int l, int o, int q) b) { int c = a.l.CompareTo(b.l); if (c != 0) return c; c = a.o.CompareTo(b.o); return c != 0 ? c : a.q.CompareTo(b.q); }
                void DumpBetween((int l, int o, int q) lo, (int l, int o, int q) hi)
                {
                    int n = 0;
                    foreach (var row in gpuCollapseBarriers)
                    {
                        if (memberRows.Contains(row)) continue;
                        if (TileIgnoredAux(row)) continue;
                        var t3 = (row.Key.Layer, row.Key.Order, row.Key.Queue);
                        if (Cmp(t3, lo) >= 0 && Cmp(t3, hi) <= 0 && n++ < 6)
                        {
                            var r = row.R; var d = r ? r.GetComponentInParent<scrDecoration>() : null;
                            GpuLog("TILECOMP_BETWEEN " + (r ? r.name : "null") + " type=" + (r ? r.GetType().Name : "-") + " active=" + (r && r.enabled && r.gameObject.activeInHierarchy) + " off=" + (r && r.forceRenderingOff)
                                + " deco=" + (d ? d.GetType().Name : "-") + " shader=" + (row.M && row.M.shader ? row.M.shader.name : "-") + " layer=" + (r ? r.gameObject.layer : -1));
                        }
                    }
                    foreach (var sp in gpuCollapseSpans)
                    {
                        var f = (sp.First.Layer, sp.First.Order, sp.First.Queue); var l2 = (sp.Last.Layer, sp.Last.Order, sp.Last.Queue);
                        if (Cmp(f, hi) <= 0 && Cmp(l2, lo) >= 0 && n++ < 8) GpuLog("TILECOMP_BETWEEN span items=" + sp.Items.Length + " first=" + DcKey(sp.First) + " last=" + DcKey(sp.Last));
                    }
                }
                bool AnyBetween((int l, int o, int q) lo, (int l, int o, int q) hi)
                {
                    int a = 0, b = others.Count; // 첫 others >= lo
                    while (a < b) { int m = (a + b) / 2; if (Cmp(others[m], lo) < 0) a = m + 1; else b = m; }
                    return a < others.Count && Cmp(others[a], hi) <= 0;
                }
                // 연속 구간 찾기 + 8레이어/4텍스처 제한으로 쪼개기
                var runs = new List<List<TileLayer>>(); var cur = new List<TileLayer>();
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    if (cur.Count > 0)
                    {
                        var prev = cur[cur.Count - 1];
                        var tp = (prev.Row.Key.Layer, prev.Row.Key.Order, prev.Row.Key.Queue); var tm = (m.Row.Key.Layer, m.Row.Key.Order, m.Row.Key.Queue);
                        bool sameZ = Mathf.Abs(prev.PlaneZ - m.PlaneZ) < 1e-3f;
                        bool between = AnyBetween(tp, tm);
                        if (between && tileDbg < 4) DumpBetween(tp, tm);
                        if (tileDbg < 40) { tileDbg++; GpuLog("TILECOMP_DBG prev=(" + tp.Item1 + "," + tp.Item2 + "," + tp.Item3 + ") next=(" + tm.Item1 + "," + tm.Item2 + "," + tm.Item3 + ") between=" + between + " sameZ=" + sameZ + " z=" + prev.PlaneZ + "/" + m.PlaneZ); }
                        if (between || !sameZ) { runs.Add(cur); cur = new List<TileLayer>(); }
                    }
                    cur.Add(m);
                }
                if (cur.Count > 0) runs.Add(cur);
                var textures = new Dictionary<int, Texture2D>();
                foreach (var m in members) textures[m.Tex.GetInstanceID()] = m.Tex;
                TrimBoundsFor(textures);
                var cam = gpuLiveCamera;
                int hidden = 0, groupsMade = 0, skippedRuns = 0;
                foreach (var run in runs)
                {
                    var chunk = new List<TileLayer>(); var texIds = new List<int>();
                    void Flush()
                    {
                        bool single = false;
                        if (chunk.Count == 1)
                        {
                            TileComputeSBounds(chunk[0]); var sb1 = chunk[0].SBounds;
                            single = (sb1.z - sb1.x) * (sb1.w - sb1.y) < DrawCollapsePolicy.TileSingleMaxArea;
                        }
                        if (chunk.Count >= 2 || single) { MakeTileGroup(shader, chunk, texIds); groupsMade++; hidden += chunk.Count; } else if (chunk.Count == 1) skippedRuns++;
                        chunk = new List<TileLayer>(); texIds = new List<int>();
                    }
                    foreach (var l in run)
                    {
                        int tid = l.Tex.GetInstanceID();
                        if (!texIds.Contains(tid) && texIds.Count >= 4) Flush();
                        if (chunk.Count >= (DrawCollapsePolicy.TileMerge ? 8 : 1)) Flush();
                        if (!texIds.Contains(tid)) texIds.Add(tid);
                        l.Slot = texIds.IndexOf(tid);
                        chunk.Add(l);
                    }
                    Flush();
                }
                if (tileGroups.Count == 0) { GpuLog("TILECOMP skip=no_groups members=" + members.Count + " runs=" + runs.Count); return; }
                foreach (var g in tileGroups) foreach (var l in g.Layers) { l.WasOff = l.R.forceRenderingOff; l.R.forceRenderingOff = true; }
                if (!tileHooked) { Camera.onPreCull += TileCompositePreCull; tileHooked = true; }
                tileApplied = true;
                GpuLog("TILECOMP applied ignored_hidden_blendmode_aux=" + ownAux + " groups=" + groupsMade + " hidden_layers=" + hidden + " members=" + members.Count + " singles_skipped=" + skippedRuns
                    + " ms=" + (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
            catch (Exception e)
            {
                GpuLog("TILECOMP fault_on_apply=" + e.GetType().Name + ":" + e.Message);
                TileCompositeRemove();
            }
        }

        static void TileComputeSBounds(TileLayer l)
        {
            if (!TryGetTrimBounds(l.Tex, out var tb)) return;
            if (Mathf.Approximately(l.RepX, 1f))
            {
                float uMinAt = l.Auv00 * l.VMin.x + l.Buvx, uMaxAt = l.Auv00 * (l.VMin.x + l.VSize.x) + l.Buvx;
                float s0 = (tb.x - uMinAt) / (uMaxAt - uMinAt), s1 = (tb.z - uMinAt) / (uMaxAt - uMinAt);
                l.SBounds.x = Mathf.Clamp01(Mathf.Min(s0, s1)); l.SBounds.z = Mathf.Clamp01(Mathf.Max(s0, s1));
            }
            if (Mathf.Approximately(l.RepY, 1f))
            {
                float vMinAt = l.Auv11 * l.VMin.y + l.Buvy, vMaxAt = l.Auv11 * (l.VMin.y + l.VSize.y) + l.Buvy;
                float s0 = (tb.y - vMinAt) / (vMaxAt - vMinAt), s1 = (tb.w - vMinAt) / (vMaxAt - vMinAt);
                l.SBounds.y = Mathf.Clamp01(Mathf.Min(s0, s1)); l.SBounds.w = Mathf.Clamp01(Mathf.Max(s0, s1));
            }
        }
        static void MakeTileGroup(Shader shader, List<TileLayer> layers, List<int> texIds)
        {
            var g = new TileGroup { Layers = new List<TileLayer>(layers) };
            var first = layers[0];
            g.Mat = new Material(shader) { name = "GpuTileComposite", renderQueue = first.Row.M.renderQueue };
            for (int i = 0; i < 4; i++)
            {
                var tid = i < texIds.Count ? texIds[i] : texIds[0];
                var tex = layers.First(l => l.Tex.GetInstanceID() == tid).Tex;
                g.Mat.SetTexture("_Tex" + i, tex);
            }
            foreach (var l in layers) TileComputeSBounds(l);
            g.Mesh = new Mesh { name = "GpuTileComposite quad" };
            g.Mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0) };
            g.Mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            var center = first.R.bounds.center;
            g.Mesh.bounds = new Bounds(center, new Vector3(2000000, 2000000, 0));
            g.Go = new GameObject("GpuTileComposite") { hideFlags = HideFlags.HideAndDontSave, layer = first.R.gameObject.layer };
            g.Go.transform.SetParent(gpuLiveRoot.transform, false);
            g.Go.AddComponent<MeshFilter>().sharedMesh = g.Mesh;
            g.Mr = g.Go.AddComponent<MeshRenderer>(); g.Mr.sharedMaterial = g.Mat;
            g.Mr.sortingLayerID = first.R.sortingLayerID; g.Mr.sortingOrder = first.R.sortingOrder;
            g.Mr.shadowCastingMode = ShadowCastingMode.Off; g.Mr.receiveShadows = false;
            g.Mr.lightProbeUsage = LightProbeUsage.Off; g.Mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            g.Mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; g.Mr.allowOcclusionWhenDynamic = false;
            g.Mr.enabled = true;
            tileGroups.Add(g);
        }

        static void TileCompositePreCull(Camera cam)
        {
            if (!tileApplied || cam != gpuLiveCamera || tileFault != null) return;
            try
            {
                var ct = cam.transform;
                float halfH = cam.orthographicSize * 1.03f + .5f, halfW = halfH * cam.aspect;
                Vector2 C = ct.position; Vector2 right = ct.right; Vector2 up = ct.up;
                var rightV = right * halfW; var upV = up * halfH;
                foreach (var g in tileGroups)
                {
                    if (!g.Mr) { tileFault = "composite_lost"; break; }
                    for (int i = 0; i < g.Layers.Count; i++)
                    {
                        var l = g.Layers[i];
                        if (!l.R || l.R.sprite != l.Sp || l.R.flipX != l.FlipX || l.R.flipY != l.FlipY) { tileFault = "layer_changed"; break; }
                        if (!l.R.forceRenderingOff) l.R.forceRenderingOff = true;
                        l.Tint = TileTint(l.R, l.Row.M);
                        var w2l = l.R.transform.worldToLocalMatrix;
                        double qx = w2l.m00 * (double)C.x + w2l.m01 * (double)C.y + w2l.m02 * l.PlaneZ + w2l.m03;
                        double qy = w2l.m10 * (double)C.x + w2l.m11 * (double)C.y + w2l.m12 * l.PlaneZ + w2l.m13;
                        double fx = l.FlipX ? -1 : 1, fy = l.FlipY ? -1 : 1;
                        double vx = fx * qx, vy = fy * qy;
                        double j00 = fx * w2l.m00, j01 = fx * w2l.m01, j10 = fy * w2l.m10, j11 = fy * w2l.m11;
                        double sx0 = (vx - l.VMin.x) / l.VSize.x, sy0 = (vy - l.VMin.y) / l.VSize.y;
                        double m00 = j00 / l.VSize.x, m01 = j01 / l.VSize.x, m10 = j10 / l.VSize.y, m11 = j11 / l.VSize.y;
                        double kx = l.RepX * l.Auv00 * l.VSize.x, ky = l.RepY * l.Auv11 * l.VSize.y;
                        double ux0 = l.RepX * (l.Auv00 * vx + l.Buvx), uy0 = l.RepY * (l.Auv11 * vy + l.Buvy);
                        if (Math.Abs(l.RepX - 1.0) > 1e-4) ux0 -= Math.Floor(ux0);
                        if (Math.Abs(l.RepY - 1.0) > 1e-4) uy0 -= Math.Floor(uy0);
                        g.P0[i] = new Vector4((float)m00, (float)m01, (float)sx0, (float)kx);
                        g.P1[i] = new Vector4((float)m10, (float)m11, (float)sy0, (float)ky);
                        g.P2[i] = new Vector4((float)ux0, (float)uy0, l.Slot, 0);
                        g.P3[i] = l.SBounds;
                        g.Col[i] = new Vector4(l.Tint.r, l.Tint.g, l.Tint.b, l.Tint.a);
                    }
                    if (tileFault != null) break;
                    // 레이어 쿼드가 화면에서 덮는 영역의 합집합만 그린다(NDC 단위, 카메라 축 기준).
                    double rx0 = 1e30, ry0 = 1e30, rx1 = -1e30, ry1 = -1e30;
                    Vector2 rdir = right.normalized, udir = up.normalized;
                    for (int i = 0; i < g.Layers.Count; i++)
                    {
                        double m00 = g.P0[i].x, m01 = g.P0[i].y, s0x = g.P0[i].z, m10 = g.P1[i].x, m11 = g.P1[i].y, s0y = g.P1[i].z;
                        double det = m00 * m11 - m01 * m10;
                        if (Math.Abs(det) < 1e-18) { rx0 = -1; ry0 = -1; rx1 = 1; ry1 = 1; break; }
                        var sb = g.P3[i];
                        for (int c = 0; c < 4; c++)
                        {
                            double sx = (c & 1) == 0 ? sb.x : sb.z, sy = (c & 2) == 0 ? sb.y : sb.w;
                            double ax = sx - s0x, ay = sy - s0y;
                            double dpx = (m11 * ax - m01 * ay) / det, dpy = (-m10 * ax + m00 * ay) / det;
                            double nx = (dpx * rdir.x + dpy * rdir.y) / halfW, ny = (dpx * udir.x + dpy * udir.y) / halfH;
                            rx0 = Math.Min(rx0, nx); rx1 = Math.Max(rx1, nx); ry0 = Math.Min(ry0, ny); ry1 = Math.Max(ry1, ny);
                        }
                    }
                    rx0 = Math.Max(-1, rx0); ry0 = Math.Max(-1, ry0); rx1 = Math.Min(1, rx1); ry1 = Math.Min(1, ry1);
                    bool empty = rx1 <= rx0 || ry1 <= ry0;
                    g.Mr.enabled = !empty;
                    var m = g.Mat;
                    m.SetVector("_Rect", new Vector4((float)rx0, (float)ry0, (float)rx1, (float)ry1));
                    m.SetInt("_LayerCount", g.Layers.Count);
                    m.SetVector("_Cam", new Vector4(C.x, C.y, g.Layers[0].PlaneZ, 0));
                    m.SetVector("_Right", new Vector4(rightV.x, rightV.y, 0, 0)); m.SetVector("_Up", new Vector4(upV.x, upV.y, 0, 0));
                    m.SetVectorArray("_P0", g.P0); m.SetVectorArray("_P1", g.P1); m.SetVectorArray("_P2", g.P2); m.SetVectorArray("_P3", g.P3); m.SetVectorArray("_Col", g.Col);
                }
                tileFrames++;
            }
            catch (Exception e) { tileFault = e.GetType().Name + ":" + e.Message; }
            if (tileFault != null)
            {
                GpuLog("TILECOMP fault=" + tileFault + " frames=" + tileFrames + " restoring");
                foreach (var g in tileGroups) { if (g.Mr) g.Mr.enabled = false; foreach (var l in g.Layers) if (l.R) l.R.forceRenderingOff = l.WasOff; }
                tileApplied = false;
            }
        }

        static void TileCompositeRemove()
        {
            if (tileHooked) { Camera.onPreCull -= TileCompositePreCull; tileHooked = false; }
            if (tileGroups.Count == 0) { tileApplied = false; return; }
            foreach (var g in tileGroups)
            {
                foreach (var l in g.Layers) if (l.R) l.R.forceRenderingOff = l.WasOff;
                if (g.Go) UnityEngine.Object.Destroy(g.Go);
                if (g.Mat) UnityEngine.Object.Destroy(g.Mat);
                if (g.Mesh) UnityEngine.Object.Destroy(g.Mesh);
            }
            GpuLog("TILECOMP removed groups=" + tileGroups.Count + " frames=" + tileFrames);
            tileGroups.Clear(); tileApplied = false; tileFrames = 0;
        }
    }
}
#endif
