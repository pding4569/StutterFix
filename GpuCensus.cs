#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal sealed class GpuCensus : MonoBehaviour
    {
        [DllImport("gpuprobe", CallingConvention = CallingConvention.Cdecl)] static extern int gp_texture_info(IntPtr texture, [Out] uint[] info);
        static Dictionary<int, string> textureDescriptions;
        static bool nativeAvailable;
        sealed class Draw
        {
            internal Renderer Renderer;
            internal Material Material;
            internal Texture Texture;
            internal double Area;
            internal bool AlphaZero, PropertyBlock;
            internal float Distance;
            internal string Group, Textures, Keywords;
            internal int Slot;
        }
        static string Clean(string s) => (s ?? "null").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        static string F(double n) => n.ToString("F3", CultureInfo.InvariantCulture);
        static string TextureInfo(Texture t)
        {
            if (!t) return "null";
            int id = t.GetInstanceID();
            if (textureDescriptions.TryGetValue(id, out string cached)) return cached;
            string format = t.graphicsFormat.ToString(), extra = "";
            if (t is Texture2D t2) extra = "/" + t2.format + "/streaming=" + t2.streamingMipmaps + "/loadedMip=" + t2.loadedMipmapLevel;
            if (t is RenderTexture rt) extra = "/" + rt.format + "/MSAA=" + rt.antiAliasing;
            string gpu = ":GPUdesc=unavailable";
            if (nativeAvailable)
            {
                try
                {
                    var desc = new uint[6];
                    // Intentional one-time renderer synchronization, cached per unique texture.
                    // This command's frame is excluded from all performance measurements.
                    if (gp_texture_info(t.GetNativeTexturePtr(), desc) != 0) gpu = ":GPU=" + desc[0] + "x" + desc[1] + ":DXGI=" + desc[3] + ":GPUmips=" + desc[2] + ":array=" + desc[4] + ":GPUsamples=" + desc[5];
                }
                catch (DllNotFoundException) { nativeAvailable = false; }
                catch (EntryPointNotFoundException) { nativeAvailable = false; }
            }
            string value = Clean(t.name) + "#" + id + ":unity=" + t.width + "x" + t.height + ":" + format + extra + ":mips=" + t.mipmapCount + ":filter=" + t.filterMode + ":wrap=" + t.wrapMode + ":aniso=" + t.anisoLevel + gpu;
            textureDescriptions.Add(id, value); return value;
        }
        static string RtInfo(Camera c)
        {
            var rt = c.targetTexture;
            return rt ? TextureInfo(rt) + ":depth=" + rt.depth + ":sRGB=" + rt.sRGB : "backbuffer:" + Screen.width + "x" + Screen.height + ":format=unknown:MSAA_quality=" + QualitySettings.antiAliasing;
        }
        // World AABB projection, clipped at near/far planes and viewport. This is an upper bound,
        // not actual covered fragments, sprite alpha coverage, occlusion or a draw-call counter.
        internal static double Area(Camera c, Bounds b)
        {
            var points = new Vector3[8]; var z = new float[8];
            Vector3 min = b.min, max = b.max;
            for (int i = 0; i < 8; i++) { points[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z); z[i] = c.WorldToViewportPoint(points[i]).z; }
            float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
            void Include(Vector3 world)
            {
                var v = c.WorldToViewportPoint(world); x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
            }
            for (int i = 0; i < 8; i++)
            {
                if (z[i] >= c.nearClipPlane && z[i] <= c.farClipPlane) Include(points[i]);
                for (int bit = 1; bit <= 4; bit *= 2)
                {
                    int j = i ^ bit; if (j < i) continue;
                    for (int planeIndex = 0; planeIndex < 2; planeIndex++)
                    {
                        float plane = planeIndex == 0 ? c.nearClipPlane : c.farClipPlane;
                        if ((z[i] < plane && z[j] > plane) || (z[j] < plane && z[i] > plane)) Include(Vector3.LerpUnclamped(points[i], points[j], (plane - z[i]) / (z[j] - z[i])));
                    }
                }
            }
            return Math.Max(0, Math.Min(1, x1) - Math.Max(0, x0)) * Math.Max(0, Math.Min(1, y1) - Math.Max(0, y0)) * c.pixelWidth * c.pixelHeight;
        }
        static GpuCensus pending;
        internal static bool Busy => pending != null;
        internal static void Stop()
        {
            Camera.onPreRender -= Capture;
            if (pending) Destroy(pending.gameObject);
            pending = null;
        }
        internal static void Run()
        {
            if (GpuHide.Busy || GpuSplit.Active) throw new InvalidOperationException("gpuhide/gpusplit active; use a separate census run");
            Stop();
            var go = new GameObject("StutterFix.GpuCensus"); DontDestroyOnLoad(go);
            pending = go.AddComponent<GpuCensus>();
            Camera.onPreRender += Capture;
            Main.Entry.Logger.Log("[gpucensus] queued: next Camera onPreRender, after LateUpdate/culling; timeout 10 realtime seconds");
            pending.StartCoroutine(pending.Timeout());
        }
        void OnDestroy() { if (pending == this) { Camera.onPreRender -= Capture; pending = null; } }
        System.Collections.IEnumerator Timeout()
        {
            yield return new WaitForSecondsRealtime(10);
            Main.Entry.Logger.Log("[gpucensus] no Camera render callback; snapshot not captured"); Stop();
        }
        static void Capture(Camera c)
        {
            if (pending == null || c.name != "Camera") return;
            Stop();
            try { RunNow(c); }
            catch (Exception ex) { Main.Entry.Logger.Log("[gpucensus] failed: " + ex); }
        }
        // A local Sprite bound transformed to world space also diagnoses stale/zero native bounds.
        // Never replace the engine culling bound with this estimate: tiled/sliced sprites and custom
        // bounds can differ. Frustum inclusion and pixel-area estimation are separate decisions.
        static Bounds SpriteBounds(SpriteRenderer r)
        {
            var b = r.sprite.bounds; var m = r.localToWorldMatrix;
            var e = b.extents;
            var x = m.MultiplyVector(new Vector3(e.x, 0, 0));
            var y = m.MultiplyVector(new Vector3(0, e.y, 0));
            var z = m.MultiplyVector(new Vector3(0, 0, e.z));
            return new Bounds(m.MultiplyPoint3x4(b.center), new Vector3(
                Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),
                Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);
        }
        internal static bool IsDecoration(Renderer r) => r.GetComponentInParent<scrDecoration>() != null;
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
        static int Runs(Draw[] draws, int mode, bool blockPresenceOnly = false)
        {
            int runs = 0; string previous = null;
            foreach (var d in draws)
            {
                // MPB values have not been proven equal; even two non-empty blocks must split.
                string key = d.Material.renderQueue + ":" + d.Renderer.sortingLayerID + ":" + d.Renderer.sortingOrder
                    + ":" + d.Renderer.GetType().Name + ":" + (d.Material.shader ? d.Material.shader.GetInstanceID() : 0)
                    + ":" + d.Keywords + ":" + (mode == 0 ? d.Material.GetInstanceID() : 0)
                    + ":" + (mode < 2 && d.Texture ? d.Texture.GetInstanceID() : 0)
                    + ":" + (d.PropertyBlock ? (blockPresenceOnly ? "block" : d.Renderer.GetInstanceID() + "/" + d.Slot) : "no-block");
                if (previous != key) runs++;
                previous = key;
            }
            return runs;
        }
        static void RunNow(Camera captureCamera)
        {
            textureDescriptions = new Dictionary<int, string>();
            nativeAvailable = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11;
            try
            {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var all = Resources.FindObjectsOfTypeAll<Renderer>();
            // Main camera state is captured at its own callback. Other camera states are contextual only.
            // Disabled cameras can still be drawn with Camera.Render (scrCamera custom-FPS path).
            var cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy).OrderBy(c => c.depth).ToArray();
            var text = new StringBuilder();
            text.AppendLine("# gpucensus ONE-TIME snapshot; discard this frame/window from performance results. Bounds/frustum candidates, not actual GPU visibility; sorting/batches are estimates.");
            text.AppendLine("# frame=" + Time.frameCount + " screen=" + Screen.width + "x" + Screen.height + " device=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType);
            var property = new MaterialPropertyBlock(); var perMaterial = new MaterialPropertyBlock();
            int mainTex = Shader.PropertyToID("_MainTex"), baseMap = Shader.PropertyToID("_BaseMap"), color = Shader.PropertyToID("_Color"), baseColor = Shader.PropertyToID("_BaseColor");
            foreach (var c in cameras)
            {
                var list = new List<Draw>(); var planes = GeometryUtility.CalculateFrustumPlanes(c.cullingMatrix);
                var projectionPlanes = GeometryUtility.CalculateFrustumPlanes(c.projectionMatrix * c.worldToCameraMatrix);
                int inactive = 0, disabled = 0, forced = 0, masked = 0, outside = 0, shadowsOnly = 0, noMaterial = 0, areaZero = 0, spriteDisagree = 0, spriteRecovered = 0, projectionDisagree = 0;
                int rejectedDetails = 0;
                double rendererArea = 0; int renderers = 0, alphaRenderers = 0;
                foreach (var r in all)
                {
                    if (!r.gameObject.scene.IsValid() || !r.gameObject.activeInHierarchy) { inactive++; continue; }
                    if (!r.enabled) { disabled++; continue; }
                    if (r.forceRenderingOff) { forced++; continue; }
                    if ((c.cullingMask & (1 << r.gameObject.layer)) == 0) { masked++; continue; }
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) { shadowsOnly++; continue; }
                    var bounds = r.bounds;
                    bool inside = GeometryUtility.TestPlanesAABB(planes, bounds);
                    var sr = r as SpriteRenderer;
                    bool spriteInside = sr && sr.sprite && GeometryUtility.TestPlanesAABB(planes, SpriteBounds(sr));
                    if (sr && sr.sprite && inside != spriteInside) spriteDisagree++;
                    if (inside != GeometryUtility.TestPlanesAABB(projectionPlanes, bounds)) projectionDisagree++;
                    if (!inside && spriteInside) { spriteRecovered++; inside = true; bounds = SpriteBounds(sr); }
                    if (!inside)
                    {
                        outside++;
                        if (rejectedDetails++ < 96) text.AppendLine("# rejected renderer=" + Clean(PathOf(r.transform)) + " type=" + r.GetType().Name + " layer=" + r.gameObject.layer + " bounds=" + bounds.ToString("G9") + " local=" + r.localBounds.ToString("G9") + " z_near_far=" + c.WorldToViewportPoint(bounds.center).z + "/" + c.nearClipPlane + "/" + c.farClipPlane + " viewport_center=" + c.WorldToViewportPoint(bounds.center).ToString("G9") + " sprite_inside=" + spriteInside + " sprite_bounds=" + (sr && sr.sprite ? SpriteBounds(sr).ToString("G9") : "none") + " visible_any_camera_previous=" + r.isVisible);
                        continue;
                    }
                    double area = Area(c, bounds);
                    // Area is diagnostic only. Zero-area projection is not a culling verdict.
                    if (area <= 0 || double.IsNaN(area)) { areaZero++; area = 0; }
                    renderers++; rendererArea += area;
                    var mats = r.sharedMaterials; bool zeroRenderer = false;
                    bool hasBlock = r.HasPropertyBlock(); property.Clear(); if (hasBlock) r.GetPropertyBlock(property);
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m]; if (!mat) { noMaterial++; continue; }
                        perMaterial.Clear(); if (hasBlock) r.GetPropertyBlock(perMaterial, m);
                        // Per-material blocks replace the renderer-wide block, rather than merge it.
                        var block = perMaterial.isEmpty ? property : perMaterial;
                        Texture tex = mat.mainTexture;
                        var sprite = r as SpriteRenderer;
                        if (sprite && sprite.sprite) tex = sprite.sprite.texture;
                        if (block.HasProperty(mainTex)) tex = block.GetTexture(mainTex);
                        else if (block.HasProperty(baseMap)) tex = block.GetTexture(baseMap);
                        float a = sprite ? sprite.color.a : 1f;
                        int colorId = mat.HasProperty(color) ? color : mat.HasProperty(baseColor) ? baseColor : -1;
                        if (colorId != -1) a *= block.HasProperty(colorId) ? block.GetColor(colorId).a : mat.GetColor(colorId).a;
                        bool zero = a == 0; zeroRenderer |= zero;
                        var textures = new StringBuilder();
                        foreach (var prop in mat.GetTexturePropertyNames())
                        {
                            int propId = Shader.PropertyToID(prop);
                            Texture value = block.HasProperty(propId) ? block.GetTexture(propId) : mat.GetTexture(prop);
                            if (propId == mainTex && sprite && sprite.sprite && !block.HasProperty(mainTex)) value = sprite.sprite.texture;
                            textures.Append(prop).Append('=').Append(TextureInfo(value)).Append(';');
                        }
                        string group = mat.GetInstanceID() + ":" + (tex ? tex.GetInstanceID() : 0) + ":" + r.sortingLayerID + ":" + r.sortingOrder + ":" + textures;
                        Vector3 point = sprite && sprite.spriteSortPoint == SpriteSortPoint.Pivot ? sprite.transform.position : bounds.center;
                        Vector3 delta = point - c.transform.position;
                        var sortMode = c.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortMode : c.transparencySortMode;
                        var sortAxis = c.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortAxis : c.transparencySortAxis;
                        float distance = sortMode == TransparencySortMode.CustomAxis ? Vector3.Dot(point, sortAxis) : sortMode == TransparencySortMode.Orthographic || (sortMode == TransparencySortMode.Default && c.orthographic) ? Vector3.Dot(delta, c.transform.forward) : delta.sqrMagnitude;
                        list.Add(new Draw { Renderer = r, Material = mat, Texture = tex, Area = area, AlphaZero = zero, PropertyBlock = hasBlock, Distance = distance, Group = group, Textures = textures.ToString(), Slot = m, Keywords = string.Join(",", mat.shaderKeywords.OrderBy(k => k)) });
                    }
                    if (zeroRenderer) alphaRenderers++;
                }
                double screen = (double)c.pixelWidth * c.pixelHeight;
                string head = "camera=" + Clean(c.name) + "#" + c.GetInstanceID() + " enabled=" + c.enabled + " (disabled=manual-render-candidates-only) depth=" + c.depth + " mask=" + c.cullingMask + " RT=" + RtInfo(c) + " viewport=" + c.pixelRect + " HDR=" + c.allowHDR + " allowMSAA=" + c.allowMSAA + " path=" + c.actualRenderingPath + " occlusion=" + c.useOcclusionCulling + " sort=" + c.transparencySortMode + " axis=" + c.transparencySortAxis;
                text.AppendLine("# " + head);
                text.AppendLine("# snapshot_trigger=" + captureCamera.name + " own_callback=" + (c == captureCamera) + " capture=onPreRender frame=" + Time.frameCount + " near=" + c.nearClipPlane + " far=" + c.farClipPlane + " pos=" + c.transform.position.ToString("G9") + " view=" + Clean(c.worldToCameraMatrix.ToString("G9")) + " projection=" + Clean(c.projectionMatrix.ToString("G9")) + " culling=" + Clean(c.cullingMatrix.ToString("G9")));
                var imageEffects = c.GetComponents<MonoBehaviour>().Where(b => b && b.GetType().GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(RenderTexture), typeof(RenderTexture) }, null) != null);
                foreach (var b in imageEffects) text.AppendLine("# OnRenderImage " + b.GetType().FullName + "#" + b.GetInstanceID() + " active=" + b.isActiveAndEnabled);
                // Approximate ordering only: SortingGroup, sort point, opaque depth sorting and SRP/internal batching are not reconstructed.
                var sorted = list.OrderBy(d => d.Material.renderQueue).ThenBy(d => SortingLayer.GetLayerValueFromID(d.Renderer.sortingLayerID)).ThenBy(d => d.Renderer.sortingOrder).ThenByDescending(d => d.Distance).ThenBy(d => d.Renderer.GetInstanceID()).ThenBy(d => d.Slot).ToArray();
                int matChanges = 0, texChanges = 0, runs = 0;
                for (int i = 0; i < sorted.Length; i++)
                {
                    bool mc = i == 0 || sorted[i].Material != sorted[i-1].Material, tc = i == 0 || sorted[i].Texture != sorted[i-1].Texture;
                    if (i > 0 && mc) matChanges++; if (i > 0 && tc) texChanges++; if (mc || tc) runs++;
                }
                string summary = "area_zero_kept=" + areaZero + " sprite_bounds_disagree=" + spriteDisagree + " conservative_sprite_recovered=" + spriteRecovered + " culling_vs_projection_disagree=" + projectionDisagree + " candidates=" + renderers + " material_slots=" + list.Count + " aabb_pixels=" + F(rendererArea) + " aabb_overdraw=" + F(screen == 0 ? 0 : rendererArea / screen) + " material_slot_overdraw=" + F(screen == 0 ? 0 : list.Sum(d => d.Area) / screen) + " alpha0_candidates=" + alphaRenderers + " alpha0_slot_pixels=" + F(list.Where(d => d.AlphaZero).Sum(d => d.Area)) + " material_changes_est=" + matChanges + " texture_changes_est=" + texChanges + " runs_est=" + runs + " excluded(inactive,disabled,forced,mask,outside,shadowsOnly,noMat)=" + inactive + "," + disabled + "," + forced + "," + masked + "," + outside + "," + shadowsOnly + "," + noMaterial;
                text.AppendLine("# runs_key_presence_est=" + Runs(sorted, 0, true) + "; requested equal MPB-presence key; block values may differ");
                text.AppendLine("# runs_current_est=" + Runs(sorted, 0) + " runs_shared_material_est=" + Runs(sorted, 1) + " runs_atlas_est=" + Runs(sorted, 2) + "; optimistic compatibility model, unknown material values ignored in hypothetical modes; MPB splits individually; tie=instanceID (Unity tie unknown)");
                text.AppendLine("# decoration_slots=" + sorted.Count(d => IsDecoration(d.Renderer)) + " sprite_slots=" + sorted.Count(d => d.Renderer is SpriteRenderer) + " mesh_slots=" + sorted.Count(d => d.Renderer is MeshRenderer));
                text.AppendLine("# unique_primary_textures=" + list.Where(d => d.Texture).Select(d => d.Texture.GetInstanceID()).Distinct().Count() + " unique_materials=" + list.Select(d => d.Material.GetInstanceID()).Distinct().Count());
                var usage = list.GroupBy(d => d.Material.GetInstanceID()).ToDictionary(g => g.Key, g => g.Select(d => d.Renderer.GetInstanceID()).Distinct().Count());
                var uniqueTextures = list.Where(d => d.Texture).Select(d => d.Texture).GroupBy(t => t.GetInstanceID()).Select(g => g.First());
                foreach (var g in uniqueTextures.GroupBy(t => t.width + "x" + t.height).OrderBy(g => g.Key)) text.AppendLine("# texture_dimensions=" + g.Key + " unique_count=" + g.Count());
                text.AppendLine("# record\tordered_slot\trenderer\tkind\tdecoration\tmaterial\tmaterial_renderer_users\truntime_id_negative_hint\tproperty_block\tshader\tkeywords\tqueue\tsorting_layer\torder\tdistance\ttexture");
                for (int j = 0; j < sorted.Length; j++)
                {
                    var d = sorted[j];
                    text.AppendLine("draw\t" + j + "\t" + Clean(PathOf(d.Renderer.transform)) + "#" + d.Renderer.GetInstanceID() + "\t" + d.Renderer.GetType().Name + "\t" + IsDecoration(d.Renderer) + "\t" + Clean(d.Material.name) + "#" + d.Material.GetInstanceID() + "\t" + usage[d.Material.GetInstanceID()] + "\t" + (d.Material.GetInstanceID() < 0) + "\t" + d.PropertyBlock + "\t" + Clean(d.Material.shader ? d.Material.shader.name : "null") + "\t" + d.Keywords + "\t" + d.Material.renderQueue + "\t" + d.Renderer.sortingLayerName + "\t" + d.Renderer.sortingOrder + "\t" + F(d.Distance) + "\t" + TextureInfo(d.Texture));
                }
                text.AppendLine("# " + summary); Main.Entry.Logger.Log("[gpucensus] " + head + " " + summary);
                text.AppendLine("count\taabb_pixels\talpha0_slots\talpha0_pixels\tproperty_blocks\tmaterial\tshader\tkeywords\trenderQueue\tsortingLayer\tsortingOrder\tprimaryTexture\tallTextures");
                foreach (var g in list.GroupBy(d => d.Group).OrderByDescending(g => g.Sum(d => d.Area)))
                {
                    var d = g.First(); var mat = d.Material;
                    text.AppendLine(g.Count() + "\t" + F(g.Sum(x => x.Area)) + "\t" + g.Count(x => x.AlphaZero) + "\t" + F(g.Where(x => x.AlphaZero).Sum(x => x.Area)) + "\t" + g.Count(x => x.PropertyBlock) + "\t" + Clean(mat.name) + "#" + mat.GetInstanceID() + "\t" + Clean(mat.shader ? mat.shader.name : "null") + "\t" + Clean(string.Join(",", mat.shaderKeywords)) + "\t" + mat.renderQueue + "\t" + Clean(d.Renderer.sortingLayerName) + "#" + d.Renderer.sortingLayerID + "\t" + d.Renderer.sortingOrder + "\t" + TextureInfo(d.Texture) + "\t" + d.Textures);
                }
            }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StutterFix"); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "gpu-census-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".tsv");
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            Main.Entry.Logger.Log("[gpucensus] " + path + " elapsed_ms=" + sw.ElapsedMilliseconds + "; exclude census and following recovery frames from timing. AABB overdraw and material transitions are estimates; alpha0 is not proof of invisibility.");
            }
            finally { textureDescriptions = null; nativeAvailable = false; }
        }
    }
}
#endif
