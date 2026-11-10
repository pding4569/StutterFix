#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Explicit experiment. No product switch; discovery/preparation occurs only on command.
    internal sealed class Occlude : MonoBehaviour
    {
        sealed class Item
        {
            internal Renderer R;
            internal SpriteRenderer SR;
            internal Sprite Sprite;
            internal scrVisualDecoration Deco;
            internal bool Decoration, Rectangle;
            internal Bounds Bounds;
        }
        sealed class ShaderInfo
        {
            internal LocalKeyword[] Keywords;
            internal string Pass;
            internal bool Tile;
        }
        struct Saved { internal Renderer R; internal bool Off; }
        static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> Blend =
            AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        static Occlude instance;
        internal static bool Busy => instance != null;
        readonly List<Item> items = new List<Item>();
        readonly Dictionary<Shader, ShaderInfo> shaders = new Dictionary<Shader, ShaderInfo>();
        readonly List<Material> materials = new List<Material>(8);
        readonly List<Saved> saved = new List<Saved>();
        readonly Plane[] planes = new Plane[6];
        readonly float[] quad = new float[8];
        Camera main;
        bool hiding, census;
        long ticks, calls, allocated;
        int candidates, hidden, other;
        double area;
        Item bottom;
        float deadline;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");
        static readonly int FlipId = Shader.PropertyToID("_Flip");
        static readonly int StencilId = Shader.PropertyToID("_Stencil");
        static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        static readonly int BlendStencil = Shader.PropertyToID("_BLENDMODES_BlendStencilComp");
        static void Log(string s) => Main.Entry.Logger.Log("[occlude] " + s);

        internal static void Command(string arg)
        {
            arg = arg.Trim();
            if (arg == "hide off") { Stop(); return; }
            if (arg != "census" && arg != "hide on") throw new ArgumentException("occlude census; occlude hide on|off");
            if (GpuHide.Busy || GpuCensus.Busy || GpuSplit.Busy || GpuSplitPixelDiff.Busy)
                throw new InvalidOperationException("stop gpuhide/gpucensus/gpusplit first");
            Stop();
            var go = new GameObject("StutterFix.Occlude"); DontDestroyOnLoad(go);
            var p = go.AddComponent<Occlude>(); instance = p;
            try
            {
                p.main = GpuDecoScene.FindMain();
                p.deadline = Time.realtimeSinceStartup + 10;
                p.Prepare();
                p.hiding = arg == "hide on"; p.census = !p.hiding;
                Camera.onPreCull += p.PreCull;
                Camera.onPostRender += p.PostRender;
                Log("queued " + arg + " sprites=" + p.items.Count + "; discard setup frame; snapshot discovery, live eligibility each Camera render");
            }
            catch { Stop(); throw; }
        }
        void Prepare()
        {
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r.gameObject.scene.IsValid() || r is SpriteMask) continue;
                var sr = r as SpriteRenderer;
                var s = sr ? sr.sprite : null;
                var it = new Item { R = r, SR = sr, Sprite = s, Deco = r.GetComponentInParent<scrVisualDecoration>(), Decoration = GpuCensus.IsDecoration(r) };
                if (s)
                {
                    it.Bounds = s.bounds;
                    // Prove the generated tight mesh fills its bounding rectangle; a hollow/custom
                    // mesh is not a cover even when the underlying texture is opaque.
                    var v = s.vertices; var tri = s.triangles;
                    double sum = 0;
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                    {
                        var a = v[tri[i]]; var b = v[tri[i+1]]; var c = v[tri[i+2]];
                        sum += Math.Abs((double)(b.x-a.x)*(c.y-a.y) - (double)(b.y-a.y)*(c.x-a.x)) * .5;
                    }
                    double rect = (double)it.Bounds.size.x * it.Bounds.size.y;
                    bool corners = v.Length == 4 && tri.Length == 6;
                    int cornerMask = 0;
                    foreach (var p in v)
                    {
                        int x = p.x == it.Bounds.min.x ? 0 : p.x == it.Bounds.max.x ? 1 : -1;
                        int y = p.y == it.Bounds.min.y ? 0 : p.y == it.Bounds.max.y ? 1 : -1;
                        if (x < 0 || y < 0) corners = false;
                        else cornerMask |= 1 << (y*2+x);
                    }
                    // Equal summed area alone also accepts overlapping triangles. Their
                    // common edge must be a rectangle diagonal, not a boundary edge.
                    int sharedA = -1, sharedB = -1, shared = 0;
                    if (tri.Length == 6) for (int i = 0; i < 3; i++)
                        for (int j = 3; j < 6; j++) if (tri[i] == tri[j])
                        { if (shared++ == 0) sharedA = tri[i]; else sharedB = tri[i]; }
                    bool diagonal = shared == 2 && v[sharedA].x != v[sharedB].x && v[sharedA].y != v[sharedB].y;
                    it.Rectangle = corners && cornerMask == 15 && diagonal && rect > 0 && Math.Abs(sum - rect) <= rect * 1e-7;
                }
                items.Add(it);
                r.GetSharedMaterials(materials);
                foreach (var m in materials)
                    if (m && m.shader && !shaders.ContainsKey(m.shader)
                        && (m.shader.name == "Sprites/Default" || m.shader.name == "Sprites/Tile") && m.passCount == 1)
                        shaders.Add(m.shader, new ShaderInfo { Keywords = m.shader.keywordSpace.keywords, Pass = m.GetPassName(0), Tile = m.shader.name == "Sprites/Tile" });
            }
            // No List growth or shader keyword arrays in callbacks.
            saved.Capacity = items.Count;
        }
        bool SafeMaterial(Renderer r, out Material m)
        {
            r.GetSharedMaterials(materials); m = materials.Count == 1 ? materials[0] : null;
            if (!m || !m.shader || m.renderQueue != 3000 || m.passCount != 1
                || !shaders.TryGetValue(m.shader, out var info) || !m.GetShaderPassEnabled(info.Pass)) return false;
            foreach (var k in info.Keywords)
                if ((m.IsKeywordEnabled(k) || k.isOverridable && Shader.IsKeywordEnabled(k.name))
                    && k.name != "USE_POINT_SAMPLING" && k.name != "PIXELSNAP_ON") return false;
            // These two known sprite shaders have no hard-coded stencil/GrabPass side effects.
            if (m.HasProperty(StencilId) || m.HasProperty(StencilComp) || m.HasProperty(BlendStencil)) return false;
            return true;
        }
        bool Visible(Item it, Camera c)
        {
            var r = it.R;
            return r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff
                && (c.cullingMask & (1 << r.gameObject.layer)) != 0
                && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly
                && !r.GetComponentInParent<SortingGroup>()
                && it.Decoration == GpuCensus.IsDecoration(r) && it.Deco == r.GetComponentInParent<scrVisualDecoration>()
                && (!it.SR || it.SR.maskInteraction == SpriteMaskInteraction.None)
                && (!it.Deco || !it.Deco.isMask()) && GeometryUtility.TestPlanesAABB(planes, r.bounds);
        }
        bool Opaque(Item it, Material m)
        {
            var r = it.SR; if (!r) return false;
            var s = r.sprite; var d = it.Deco;
            if (!it.Decoration || !d || (int)Blend(d) != 0 || (d.blendModeEffect && d.blendModeEffect.enabled)
                || r.color.a != 1 || (m.HasProperty(ColorId) && m.GetColor(ColorId).a != 1)
                || (m.HasProperty(RendererColorId) && m.GetColor(RendererColorId).a != 1)
                || r.HasPropertyBlock() || !s || s != it.Sprite || !it.Rectangle
                || s.associatedAlphaSplitTexture || s.packed || r.drawMode != SpriteDrawMode.Simple) return false;
            var t = s.texture;
            // Renderer flips are modeled by the quad below; extra material vertex flips are not.
            if (m.HasProperty(FlipId))
            {
                var flip = m.GetVector(FlipId);
                if (flip.x != 1 || flip.y != 1) return false;
            }
            if (!t || t.mipmapCount != 1 || t.filterMode == FilterMode.Trilinear || t.anisoLevel > 1
                || !ShapeCellsCpu.TryGetOpaque(t, out var bits, out bool all)) return false;
            if (all) return true;
            // Sprites/Tile repeats sample outside a sub-rect: only an entirely opaque texture
            // proves every repeat opaque. For normal sprites include a full bilinear texel halo.
            if (shaders[m.shader].Tile || d.repeatX != 1 || d.repeatY != 1
                || t.wrapModeU != TextureWrapMode.Clamp || t.wrapModeV != TextureWrapMode.Clamp
                || m.mainTextureScale != Vector2.one || m.mainTextureOffset != Vector2.zero) return false;
            Rect rect = s.rect; int grid = ShapeCellsCpu.GridFor(t.width, t.height);
            int x0 = Math.Max(0, Mathf.FloorToInt(rect.xMin)-1), x1 = Math.Min(t.width-1, Mathf.CeilToInt(rect.xMax));
            int y0 = Math.Max(0, Mathf.FloorToInt(rect.yMin)-1), y1 = Math.Min(t.height-1, Mathf.CeilToInt(rect.yMax));
            for (int y = y0*grid/t.height; y <= y1*grid/t.height; y++)
                for (int x = x0*grid/t.width; x <= x1*grid/t.width; x++)
                { int bit = y*grid+x; if ((bits[bit>>6] & (1UL << (bit&63))) == 0) return false; }
            return true;
        }
        // 진단(census): 화면을 다 덮는 장식이 왜 가리개가 못 되는지 첫 사유
        string Why(Item it, Camera c)
        {
            var r = it.SR; if (!r) return "not-sprite";
            var d = it.Deco; var s = r.sprite; var m = r.sharedMaterial;
            string head = (s && s.texture ? s.texture.name + " " + s.texture.width + "x" + s.texture.height + " " + s.texture.format : "no-tex") + " sh=" + (m && m.shader ? m.shader.name : "-") + " L" + SortingLayer.GetLayerValueFromID(r.sortingLayerID) + "/O" + r.sortingOrder + ": ";
            if (!SafeMaterial(it.R, out var sm)) return head + "unsafe-material";
            if (!it.Decoration || !d) return head + "not-deco";
            if ((int)Blend(d) != 0 || (d.blendModeEffect && d.blendModeEffect.enabled)) return head + "blend";
            if (r.color.a != 1) return head + "color.a=" + r.color.a.ToString("F3", CultureInfo.InvariantCulture);
            if (sm.HasProperty(ColorId) && sm.GetColor(ColorId).a != 1) return head + "mat.a=" + sm.GetColor(ColorId).a.ToString("F3", CultureInfo.InvariantCulture);
            if (sm.HasProperty(RendererColorId) && sm.GetColor(RendererColorId).a != 1) return head + "renderercolor.a";
            if (r.HasPropertyBlock()) return head + "mpb";
            if (!s || s != it.Sprite || !it.Rectangle) return head + "sprite-changed-or-not-rect";
            if (r.drawMode != SpriteDrawMode.Simple) return head + "drawmode";
            var tx = s.texture;
            if (!tx || tx.mipmapCount != 1 || tx.filterMode == FilterMode.Trilinear || tx.anisoLevel > 1) return head + "tex-sampling";
            if (!ShapeCellsCpu.TryGetOpaque(tx, out var bits, out bool all)) return head + "opaque-unknown";
            if (!all) return head + "has-transparent-texels" + (Opaque(it, sm) ? "(rect-opaque)" : "");
            return head + "OK";
        }
        bool Covers(Item it, Camera c)
        {
            var r = it.SR; var b = it.Bounds; var lo = b.min; var hi = b.max;
            // Sprites/Tile RepeatX/Y scales sampling UV, not geometry (the bake's effective
            // repeat has the same rule). The actual mesh rectangle is the coverage authority.
            for (int i = 0; i < 4; i++)
            {
                float x = i == 0 || i == 3 ? lo.x : hi.x, y = i < 2 ? lo.y : hi.y;
                var p = c.WorldToViewportPoint(r.transform.TransformPoint(new Vector3(r.flipX ? -x : x, r.flipY ? -y : y, b.center.z)));
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || p.z <= c.nearClipPlane || p.z >= c.farClipPlane) return false;
                quad[i*2] = p.x; quad[i*2+1] = p.y;
            }
            return OccludePolicy.Covers(quad,c.pixelWidth,c.pixelHeight);
        }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Before(Renderer a, Renderer b)
        {
            int al = SortingLayer.GetLayerValueFromID(a.sortingLayerID), bl = SortingLayer.GetLayerValueFromID(b.sortingLayerID);
            return OccludePolicy.Before(al,a.sortingOrder,bl,b.sortingOrder);
        }
        void Restore()
        {
            foreach (var s in saved) if (s.R) s.R.forceRenderingOff = s.Off;
            saved.Clear();
        }
        void PreCull(Camera c)
        {
            // forceRenderingOff is global: always restore before any other camera can cull.
            Restore();
            if (c != main) return;
            long begin = Stopwatch.GetTimestamp(), bytes = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                GeometryUtility.CalculateFrustumPlanes(c.cullingMatrix, planes);
                candidates = hidden = other = 0; area = 0; bottom = null;
                foreach (var it in items)
                    if (Visible(it,c) && SafeMaterial(it.R,out var m) && Opaque(it,m) && Covers(it,c))
                    { candidates++; if (bottom == null || Before(it.R,bottom.R)) bottom = it; }
                if (bottom != null) foreach (var it in items)
                {
                    if (!Visible(it,c) || !Before(it.R,bottom.R) || !SafeMaterial(it.R,out _)) continue;
                    if (!it.Decoration) { other++; continue; }
                    hidden++;
                    if (census) area += GpuCensus.Area(c,it.R.bounds); // allocates only in one-shot census
                    if (hiding) { saved.Add(new Saved { R = it.R, Off = it.R.forceRenderingOff }); it.R.forceRenderingOff = true; }
                }
            }
            catch (Exception e) { Restore(); Log("failed; restored: " + e); Stop(); return; }
            ticks += Stopwatch.GetTimestamp()-begin; calls++;
            allocated += GC.GetAllocatedBytesForCurrentThread()-bytes;
            if (census)
            {
                var whyCount = new System.Collections.Generic.Dictionary<string,int>();
                foreach (var it in items)
                {
                    if (!it.SR || !it.Decoration) continue;
                    var rr = it.R; if (!rr || !rr.enabled || !rr.gameObject.activeInHierarchy || rr.forceRenderingOff) continue;
                    bool cov; try { cov = Covers(it, c); } catch { cov = false; }
                    if (!cov) continue;
                    string k = Why(it, c); whyCount.TryGetValue(k, out int n0); whyCount[k] = n0 + 1;
                }
                foreach (var kv in whyCount) Log("census fullscreen " + kv.Value + "x " + kv.Key);
                Log("census covers=" + candidates + " lowest=" + (bottom == null ? "none" : bottom.R.name + "/layer="
                    + SortingLayer.GetLayerValueFromID(bottom.R.sortingLayerID) + "/order=" + bottom.R.sortingOrder)
                    + " hidden_deco=" + hidden + " area_screens=" + (area / Math.Max(1d,(double)c.pixelWidth*c.pixelHeight)).ToString("F3",CultureInfo.InvariantCulture)
                    + " hidden_non_deco=" + other + " (safe sprite shaders only)");
                Stop();
            }
        }
        void PostRender(Camera c) { if (c == main) Restore(); }
        void LateUpdate()
        {
            if (!main) Stop();
            else if (census && Time.realtimeSinceStartup > deadline) { Log("census timed out: no main camera callback"); Stop(); }
        }
        void OnDisable() { Restore(); }
        void OnDestroy()
        {
            Camera.onPreCull -= PreCull; Camera.onPostRender -= PostRender; Restore();
            if (instance == this) instance = null;
        }
        internal static void Stop()
        {
            var p = instance; if (!p) return;
            instance = null;
            Camera.onPreCull -= p.PreCull; Camera.onPostRender -= p.PostRender;
            p.Restore();
            if (p.hiding) Log("hide off restored; calls=" + p.calls + " mean_us="
                + (p.calls == 0 ? 0 : p.ticks*1000000d/Stopwatch.Frequency/p.calls).ToString("F3",CultureInfo.InvariantCulture)
                + " callback_alloc_bytes=" + p.allocated + " last_covers=" + p.candidates + " last_hidden=" + p.hidden);
            Destroy(p.gameObject);
        }
    }
}
#endif
