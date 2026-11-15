#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DirectFull
    {
        static string ColorAudit()
        {
            if (preparing) throw new InvalidOperationException("colors requires completed preparation or off");
            var main = Active ? camera : GpuDecoScene.FindMain();
            int original = Active ? lease.Original : main.cullingMask;
            var rb = new MaterialPropertyBlock(); var sb = new MaterialPropertyBlock();
            int colorId = Shader.PropertyToID("_Color"), rendererId = Shader.PropertyToID("_RendererColor");
            var text = new StringBuilder("id\tpath\tshader\tkeywords\tsortingLayer\torder\tqueue\tmask\tspriteColor\tmaterialColor\trendererMPBColor\tslotMPBColor\trendererMPBRendererColor\tslotMPBRendererColor\tbounds\tnativeCB\n");
            int count = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!InCameraScene(r, original) || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                r.GetPropertyBlock(rb); var materials = r.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var m = materials[slot]; if (!m || !m.shader) continue;
                    sb.Clear(); r.GetPropertyBlock(sb, slot);
                    string path = r.name; for (var t = r.transform.parent; t; t = t.parent) path = t.name + "/" + path;
                    var sprite = r as SpriteRenderer;
                    text.Append(r.GetInstanceID()).Append('\t').Append(Clean(path)).Append('\t').Append(m.shader.name).Append('\t').Append(Keywords(m))
                        .Append('\t').Append(r.sortingLayerID).Append('\t').Append(r.sortingOrder).Append('\t').Append(m.renderQueue).Append('\t').Append(sprite ? sprite.maskInteraction.ToString() : "-")
                        .Append('\t').Append(sprite ? sprite.color.ToString("R") : "-").Append('\t').Append(m.HasProperty(colorId) ? m.GetColor(colorId).ToString("R") : "-")
                        .Append('\t').Append(rb.HasProperty(colorId) ? rb.GetVector(colorId).ToString("R") : "-").Append('\t').Append(sb.HasProperty(colorId) ? sb.GetVector(colorId).ToString("R") : "-")
                        .Append('\t').Append(rb.HasProperty(rendererId) ? rb.GetVector(rendererId).ToString("R") : "-").Append('\t').Append(sb.HasProperty(rendererId) ? sb.GetVector(rendererId).ToString("R") : "-")
                        .Append('\t').Append(r.bounds.ToString("R")).Append('\t');
                    if (rendererSources.TryGetValue(r, out var source) && source.Passes != null)
                        foreach (var b in source.Passes) if (b.Slot == slot && b.NativeId != 0)
                            text.Append("pass=").Append(b.Pass.Index).Append(":id=").Append(b.NativeId).Append(":grab=").Append(b.Pass.Grab).Append(":bytes=").Append(Convert.ToBase64String(b.Bytes)).Append(';');
                    text.Append('\n'); count++;
                }
            }
            string file = Path.Combine(Main.Entry.Path, "drfull-colors-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".tsv");
            File.WriteAllText(file, text.ToString(), new UTF8Encoding(false));
            Log("colors slots=" + count + " output=" + file + " actualTintAndMPB=True actualUnityCBuffer=UNMEASURED");
            return "colors slots=" + count + " file=" + file;
        }
        sealed class CensusCount { internal int Total, Live, ForcedOff, CameraVisible, NativeCandidate; }
        static string Clean(string value) => (value ?? "-").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        static string Keywords(Material m)
        {
            if (!m) return "-";
            var names = m.shaderKeywords; Array.Sort(names, StringComparer.Ordinal);
            return names.Length == 0 ? "-" : string.Join(",", names);
        }
        static Mesh RendererMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = r.GetComponent<MeshFilter>(); return filter ? filter.sharedMesh : null;
        }
        static string VertexFormat(Renderer r)
        {
            if (r is SpriteRenderer sr)
                return sr.sprite ? "sprite-cpu:position-float2,uv-float2,index-ushort;drawMode=" + sr.drawMode : "sprite-empty";
            var mesh = RendererMesh(r);
            if (!mesh) return "engine-owned/unknown";
            var result = new StringBuilder();
            foreach (var a in mesh.GetVertexAttributes())
                result.Append(a.attribute).Append(':').Append(a.format).Append('x').Append(a.dimension).Append('@').Append(a.stream).Append(';');
            return result + "indices=" + mesh.indexFormat + ";submeshes=" + mesh.subMeshCount;
        }
        static string Route(Renderer r)
        {
            if (r is ParticleSystemRenderer) return "aux:particles";
            if (r is SkinnedMeshRenderer) return "aux:skinned/deforming";
            if (r.GetComponentInParent<Canvas>(true)) return "aux:canvas";
            if (r.GetType().Name.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 || r.GetComponent<TMPro.TMP_Text>()) return "aux:text";
            if (r.GetComponentInParent<SortingGroup>(true)) return "unverified:sorting-group";
            if (r is SpriteRenderer sr) return sr.drawMode == SpriteDrawMode.Simple ? "candidate:sprite;mask=" + sr.maskInteraction : "aux:sliced/tiled-sprite";
            if (r is MeshRenderer && RendererMesh(r)) return "candidate:mesh;dynamic-state-unverified";
            return "aux:unsupported-renderer";
        }
        static SortedDictionary<string, string> ShaderInventory()
        {
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            using (var stream = typeof(DirectFull).Assembly.GetManifestResourceStream("StutterFix.drfull.shader-census"))
            {
                if (stream == null) throw new InvalidOperationException("drfull shader census resource missing; run audit-original-shaders.py");
                using (var reader = new StreamReader(stream))
                {
                    string line; while ((line = reader.ReadLine()) != null)
                    {
                        int tab = line.IndexOf('\t');
                        if (tab > 0 && !line.StartsWith("shader\t", StringComparison.Ordinal)) rows.Add(line.Substring(0, tab), line.Substring(tab + 1));
                    }
                }
            }
            return rows;
        }
        static string Census()
        {
            CheckLease();
            var main = Active ? camera : GpuDecoScene.FindMain();
            int original = Active ? lease.Original : main.cullingMask, mapMask = ResolveMapMask();
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c && c.gameObject.scene.IsValid()) Log("census camera=" + Clean(c.name) + " active=" + c.isActiveAndEnabled + " mask=" + c.cullingMask +
                    " depth=" + c.depth + " clear=" + c.clearFlags + " target=" + (c.targetTexture ? c.targetTexture.name + ":" + c.targetTexture.width + "x" + c.targetTexture.height : "screen"));
            var shaders = ShaderInventory();
            var counts = new SortedDictionary<string, CensusCount>(StringComparer.Ordinal);
            var usedShaders = new SortedSet<string>(StringComparer.Ordinal);
            var details = new StringBuilder("id\tname\tlayer\tlayerName\tinOriginalMask\tmapLayer\tlive\tforceOff\tmaterialSlot\tsortingLayer\tsortingOrder\tqueue\tshader\tkeywords\tvertexFormat\troute\tmapComponent\tpropertyBlock\n");
            var layers = new SortedDictionary<int, int>();
            int total = 0, affected = 0, liveAffected = 0, mapOutside = 0, canvases = 0, masks = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || !r.gameObject.scene.isLoaded) continue;
                int layer = r.gameObject.layer, bit = 1 << layer;
                bool visible = (original & bit) != 0, map = (mapMask & bit) != 0;
                bool mapComponent = r.GetComponentInParent<scrDecoration>(true) || r.GetComponentInParent<scrFloor>(true) || r.GetComponentInParent<scrPlanet>(true);
                if (!visible && !map && !mapComponent) continue;
                total++; bool live = r.enabled && r.gameObject.activeInHierarchy;
                if (visible && map) { affected++; if (live && !r.forceRenderingOff) liveAffected++; }
                if (mapComponent && !map) mapOutside++;
                layers.TryGetValue(layer, out int layerCount); layers[layer] = layerCount + 1;
                string format = VertexFormat(r), route = Route(r);
                var materials = r.sharedMaterials;
                int slots = Math.Max(1, materials.Length);
                for (int i = 0; i < slots; i++)
                {
                    var m = i < materials.Length ? materials[i] : null;
                    string shader = m && m.shader ? m.shader.name : "-", keywords = Keywords(m);
                    usedShaders.Add(shader);
                    string key = layer + ":" + LayerMask.LayerToName(layer) + "\t" + r.GetType().Name + "\t" + shader + "\t" + keywords + "\t" + format + "\t" + route;
                    if (!counts.TryGetValue(key, out var count)) counts.Add(key, count = new CensusCount());
                    count.Total++; if (live) count.Live++; if (r.forceRenderingOff) count.ForcedOff++;
                    if (live && visible && !r.forceRenderingOff) count.CameraVisible++;
                    if (route.StartsWith("candidate:", StringComparison.Ordinal)) count.NativeCandidate++;
                    details.Append(r.GetInstanceID()).Append('\t').Append(Clean(r.name)).Append('\t').Append(layer).Append('\t').Append(LayerMask.LayerToName(layer))
                        .Append('\t').Append(visible).Append('\t').Append(map).Append('\t').Append(live).Append('\t').Append(r.forceRenderingOff).Append('\t').Append(i)
                        .Append('\t').Append(Clean(r.sortingLayerName)).Append('\t').Append(r.sortingOrder).Append('\t').Append(m ? m.renderQueue : -1)
                        .Append('\t').Append(Clean(shader)).Append('\t').Append(Clean(keywords)).Append('\t').Append(Clean(format)).Append('\t').Append(route)
                        .Append('\t').Append(mapComponent).Append('\t').Append(r.HasPropertyBlock()).Append('\n');
                }
            }
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                if (c && c.gameObject.scene.IsValid() && (c.renderMode == RenderMode.WorldSpace || c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == main))
                    { canvases++; Log("census canvas=" + Clean(c.name) + " layer=" + c.gameObject.layer + " mode=" + c.renderMode + " active=" + c.isActiveAndEnabled); }
            foreach (var m in Resources.FindObjectsOfTypeAll<SpriteMask>())
                if (m && m.gameObject.scene.IsValid()) { masks++; Log("census mask=" + Clean(m.name) + " layer=" + m.gameObject.layer + " active=" + (m.enabled && m.gameObject.activeInHierarchy) + " customRange=" + m.isCustomRangeActive); }
            Log("census " + CanvasCensus(main, original));
            string prefix = Path.Combine(Main.Entry.Path, "drfull-census-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            File.WriteAllText(prefix + "-renderers.tsv", details.ToString(), new UTF8Encoding(false));
            var summary = new StringBuilder("layer\ttype\tshader\tkeywords\tvertexFormat\troute\tmaterialSlots\tlive\tforceOff\tcameraVisible\tnativeCandidateUnverified\n");
            foreach (var pair in counts)
            {
                var c = pair.Value;
                summary.Append(pair.Key).Append('\t').Append(c.Total).Append('\t').Append(c.Live).Append('\t').Append(c.ForcedOff).Append('\t').Append(c.CameraVisible).Append('\t').Append(c.NativeCandidate).Append('\n');
                Log("census slots=" + c.Total + " live=" + c.Live + " cameraVisible=" + c.CameraVisible + " " + pair.Key.Replace('\t', ' '));
            }
            File.WriteAllText(prefix + "-groups.tsv", summary.ToString(), new UTF8Encoding(false));
            var originals = DirectFullShaders.Load();
            var shaderTable = new StringBuilder("shader\textractable\tvertexPrograms\tpixelPrograms\totherPrograms\trdefPrograms\tunityMetadata\tsource\tverifiedOriginalVariants\n");
            foreach (string shader in usedShaders)
            {
                string status = shaders.TryGetValue(shader, out string row) ? row : "UNKNOWN\t0\t0\t0\t0\tUNKNOWN\tmissing-from-offline-inventory";
                int verified = 0; if (originals.TryGetValue(shader, out var originalShader)) foreach (var p in originalShader.Passes) verified += p.Variants.Length;
                shaderTable.Append(Clean(shader)).Append('\t').Append(status).Append('\t').Append(verified).Append('\n');
                Log("census shader=" + Clean(shader) + " " + status.Replace('\t', ' ') + " verifiedOriginalVariants=" + verified + " activeVariantPixelParity=UNMEASURED");
            }
            File.WriteAllText(prefix + "-shaders.tsv", shaderTable.ToString(), new UTF8Encoding(false));
            foreach (var layer in layers) Log("census layer=" + layer.Key + ":" + LayerMask.LayerToName(layer.Key) + " renderers=" + layer.Value + " removed=" + ((original & mapMask & (1 << layer.Key)) != 0));
            Log("census camera=" + main.name + " originalMask=" + original + " mapMask=" + mapMask + " removedLayers=" + LayerNames(original & mapMask) + " output=" + prefix);
            return "census renderers=" + total + " affected=" + affected + " liveAffected=" + liveAffected + " knownMapOutsideRemovedLayers=" + mapOutside + " canvases=" + canvases + " spriteMasks=" + masks + " originalShaderRendering=M4c-available diagnostic-cost=exclude-from-bench";
        }
    }
}
#endif
