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
    internal static class GpuCensus
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
            internal string Group, Textures;
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
            string value = Clean(t.name) + "#" + id + ":unity=" + t.width + "x" + t.height + ":" + format + extra + ":mips=" + t.mipmapCount + ":filter=" + t.filterMode + gpu;
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
        internal static void Run()
        {
            textureDescriptions = new Dictionary<int, string>();
            nativeAvailable = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11;
            try
            {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var all = Resources.FindObjectsOfTypeAll<Renderer>();
            // Disabled cameras can still be drawn with Camera.Render (scrCamera custom-FPS path).
            var cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy).OrderBy(c => c.depth).ToArray();
            var text = new StringBuilder();
            text.AppendLine("# gpucensus ONE-TIME snapshot; discard this frame/window from performance results. Bounds/frustum candidates, not actual GPU visibility; sorting/batches are estimates.");
            text.AppendLine("# frame=" + Time.frameCount + " screen=" + Screen.width + "x" + Screen.height + " device=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType);
            var property = new MaterialPropertyBlock(); var perMaterial = new MaterialPropertyBlock();
            int mainTex = Shader.PropertyToID("_MainTex"), baseMap = Shader.PropertyToID("_BaseMap"), color = Shader.PropertyToID("_Color"), baseColor = Shader.PropertyToID("_BaseColor");
            foreach (var c in cameras)
            {
                var list = new List<Draw>(); var planes = GeometryUtility.CalculateFrustumPlanes(c);
                int inactive = 0, disabled = 0, forced = 0, masked = 0, outside = 0, shadowsOnly = 0, noMaterial = 0;
                double rendererArea = 0; int renderers = 0, alphaRenderers = 0;
                foreach (var r in all)
                {
                    if (!r.gameObject.scene.IsValid() || !r.gameObject.activeInHierarchy) { inactive++; continue; }
                    if (!r.enabled) { disabled++; continue; }
                    if (r.forceRenderingOff) { forced++; continue; }
                    if ((c.cullingMask & (1 << r.gameObject.layer)) == 0) { masked++; continue; }
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) { shadowsOnly++; continue; }
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) { outside++; continue; }
                    double area = Area(c, r.bounds); if (area <= 0) { outside++; continue; }
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
                        Vector3 delta = r.bounds.center - c.transform.position;
                        float distance = c.transparencySortMode == TransparencySortMode.CustomAxis ? Vector3.Dot(r.bounds.center, c.transparencySortAxis) : c.orthographic || c.transparencySortMode == TransparencySortMode.Orthographic ? Vector3.Dot(delta, c.transform.forward) : delta.sqrMagnitude;
                        list.Add(new Draw { Renderer = r, Material = mat, Texture = tex, Area = area, AlphaZero = zero, PropertyBlock = hasBlock, Distance = distance, Group = group, Textures = textures.ToString() });
                    }
                    if (zeroRenderer) alphaRenderers++;
                }
                double screen = (double)c.pixelWidth * c.pixelHeight;
                string head = "camera=" + Clean(c.name) + "#" + c.GetInstanceID() + " enabled=" + c.enabled + " (disabled=manual-render-candidates-only) depth=" + c.depth + " mask=" + c.cullingMask + " RT=" + RtInfo(c) + " viewport=" + c.pixelRect + " HDR=" + c.allowHDR + " allowMSAA=" + c.allowMSAA + " path=" + c.actualRenderingPath + " occlusion=" + c.useOcclusionCulling + " sort=" + c.transparencySortMode + " axis=" + c.transparencySortAxis;
                text.AppendLine("# " + head);
                var imageEffects = c.GetComponents<MonoBehaviour>().Where(b => b && b.GetType().GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(RenderTexture), typeof(RenderTexture) }, null) != null);
                foreach (var b in imageEffects) text.AppendLine("# OnRenderImage " + b.GetType().FullName + "#" + b.GetInstanceID() + " active=" + b.isActiveAndEnabled);
                // Approximate ordering only: SortingGroup, sort point, opaque depth sorting and SRP/internal batching are not reconstructed.
                var sorted = list.OrderBy(d => d.Material.renderQueue).ThenBy(d => SortingLayer.GetLayerValueFromID(d.Renderer.sortingLayerID)).ThenBy(d => d.Renderer.sortingOrder).ThenByDescending(d => d.Distance).ToArray();
                int matChanges = 0, texChanges = 0, runs = 0;
                for (int i = 0; i < sorted.Length; i++)
                {
                    bool mc = i == 0 || sorted[i].Material != sorted[i-1].Material, tc = i == 0 || sorted[i].Texture != sorted[i-1].Texture;
                    if (i > 0 && mc) matChanges++; if (i > 0 && tc) texChanges++; if (mc || tc) runs++;
                }
                string summary = "candidates=" + renderers + " material_slots=" + list.Count + " aabb_pixels=" + F(rendererArea) + " aabb_overdraw=" + F(screen == 0 ? 0 : rendererArea / screen) + " material_slot_overdraw=" + F(screen == 0 ? 0 : list.Sum(d => d.Area) / screen) + " alpha0_candidates=" + alphaRenderers + " alpha0_slot_pixels=" + F(list.Where(d => d.AlphaZero).Sum(d => d.Area)) + " material_changes_est=" + matChanges + " texture_changes_est=" + texChanges + " runs_est=" + runs + " excluded(inactive,disabled,forced,mask,outside,shadowsOnly,noMat)=" + inactive + "," + disabled + "," + forced + "," + masked + "," + outside + "," + shadowsOnly + "," + noMaterial;
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
