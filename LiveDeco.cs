#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Gate 1 only. No objects, textures, materials, meshes or Harmony patches are created.
    internal static class LiveDeco
    {
        sealed class Row
        {
            internal Renderer Renderer;
            internal LiveDecoPolicy.Item Item;
        }
        static Camera pending;
        static bool requested, done;
        static Exception failure;
        static readonly AccessTools.FieldRef<scrVisualDecoration, bool> MeshMode =
            AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> BlendMode =
            AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        static void Log(string s) => Main.Entry.Logger.Log("[livedeco] " + s);
        internal static bool Step(string arg)
        {
            switch (arg.Trim())
            {
                case "off": Stop(); Log("on=False gate=1 restored=1 mutations=0"); return true;
                case "state": Log("on=False gate=1 pending=" + requested + " prototype_available=0"); return true;
                case "on": throw new InvalidOperationException("livedeco gate 1 runtime results required; renderer prototype not implemented");
                case "census":
                    if (!requested)
                    {
                        if (MatShare.Active || FrameCompare.Paused || !Hitch.Playing)
                            throw new InvalidOperationException("livedeco census requires natural playback with matshare off and framecmp resumed");
                        if (SceneReset.Resetting || GpuSplit.Busy || GpuHide.Busy || GpuCensus.Busy)
                            throw new InvalidOperationException("scene resetting or GPU diagnostics busy");
                        pending = GpuSplit.FindMain();
                        if (!scrDecorationManager.instance) throw new InvalidOperationException("decoration manager not ready");
                        requested = true; done = false; failure = null;
                        Camera.onPreCull += OnPreCull;
                        return false;
                    }
                    if (!done) return false;
                    var error = failure; Stop();
                    if (error != null) throw new InvalidOperationException("livedeco census failed", error);
                    return true;
                default: throw new ArgumentException("livedeco census|off|state (gate 1 only)");
            }
        }
        static void OnPreCull(Camera camera)
        {
            if (camera != pending || done) return;
            Camera.onPreCull -= OnPreCull;
            try { Census(camera); } catch (Exception e) { failure = e; }
            finally { done = true; }
        }
        internal static void Stop()
        {
            Camera.onPreCull -= OnPreCull;
            pending = null; requested = done = false; failure = null;
        }
        static bool Live(Renderer r) => r && LiveDecoPolicy.RendererLive(true, r.gameObject.scene.IsValid(),
            r.enabled, r.gameObject.activeInHierarchy, r.forceRenderingOff);

        static string Blend(scrVisualDecoration d) => BlendMode(d) + "(" + (int)BlendMode(d) + ")";
        static string Effect(Behaviour effect) => !effect ? "missing" :
            "enabled:" + effect.enabled + ",active:" + effect.isActiveAndEnabled;
        static string MeshState(scrVisualDecoration d, Camera camera)
        {
            var m = d.meshRenderer;
            return "sprite_live=" + Live(d.spriteRenderer) + " mesh_exists=" + (bool)m
                + " mesh_mode=" + MeshMode(d) + " blend=" + Blend(d)
                + " sprite_effect=" + Effect(d.blendModeEffect) + " mesh_effect=" + Effect(d.meshBlendModeEffect)
                + (!m ? "" : " mesh_enabled=" + m.enabled + " mesh_active_self=" + m.gameObject.activeSelf
                    + " mesh_active_hierarchy=" + m.gameObject.activeInHierarchy + " mesh_forced_off=" + m.forceRenderingOff
                    + " mesh_scene_valid=" + m.gameObject.scene.IsValid() + " mesh_live=" + Live(m)
                    + " mesh_camera_layer=" + ((camera.cullingMask & (1 << m.gameObject.layer)) != 0)
                    + " mesh_shared_material=" + (bool)m.sharedMaterial
                    + " mesh_shader=" + (m.sharedMaterial && m.sharedMaterial.shader ? m.sharedMaterial.shader.name : "missing"));
        }

        static string Reason(scrVisualDecoration d, SpriteRenderer r, Material[] materials, Camera camera)
        {
            if ((camera.cullingMask & (1 << r.gameObject.layer)) == 0) return "camera-layer";
            if (d.GetType() != typeof(scrVisualDecoration)) return "subclass";
            if (d.isMask() || r.maskInteraction != SpriteMaskInteraction.None) return "mask";
            if (d.hitbox != 0) return "hitbox";
            // The game toggles meshRendererObj.SetActive(), not Renderer.enabled.
            // A dormant prefab mesh with enabled=true is not a live draw barrier.
            if (Live(d.meshRenderer)) return "mesh-live:blend=" + Blend(d);
            if (r.GetComponentInParent<SortingGroup>()) return "sorting-group";
            if (!r.sprite || !r.sprite.texture) return "missing-sprite-texture";
            if (r.sprite.associatedAlphaSplitTexture) return "split-alpha";
            if (r.drawMode != SpriteDrawMode.Simple) return "sliced-or-tiled";
            if (d.repeatX != 1 || d.repeatY != 1) return "repeat";
            if (d.blendModeEffect && d.blendModeEffect.enabled) return "blend-effect";
            if (materials.Length != 1 || !materials[0] || !materials[0].shader) return "material-slots";
            var m = materials[0];
            if (m.shader.name != "Sprites/Tile" || m.passCount != 1 || m.renderQueue != 3000) return "shader-pass-queue";
            if (!m.GetShaderPassEnabled(m.GetPassName(0))) return "disabled-pass";
            if (m.shaderKeywords.Length != 0) return "keywords";
            // Engine-owned sprite MPBs may be normal. Do not pretend we can enumerate
            // and bake arbitrary overrides with Unity's non-enumerating MPB API.
            if (r.HasPropertyBlock()) return "property-block-unverified";
            return null;
        }
        static string Signature(Material material)
        {
            var properties = MatShare.Properties(material);
            // Atlas replaces ONLY the main texture; retain texture scale/offset, all
            // other textures, colors, blend/stencil values and shader/pass state.
            if (properties.TryGetValue("_MainTex", out var value))
            {
                int colon = value.IndexOf(':');
                if (colon >= 0) properties["_MainTex"] = "atlas" + value.Substring(colon);
            }
            return string.Join(";", properties.Select(p => p.Key.Length + ":" + p.Key + "=" + p.Value.Length + ":" + p.Value));
        }
        static void Census(Camera camera)
        {
            var watch = Stopwatch.StartNew();
            var decorations = new Dictionary<int, scrVisualDecoration>();
            var meshOwners = new Dictionary<int, scrVisualDecoration>();
            var meshStates = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int meshOnly = 0, meshAndSprite = 0, legacyFalseRejects = 0;
            foreach (var d in Resources.FindObjectsOfTypeAll<scrVisualDecoration>())
            {
                if (!d || !d.gameObject.scene.IsValid() || !d.spriteRenderer) continue;
                int id = d.spriteRenderer.GetInstanceID();
                if (decorations.ContainsKey(id)) throw new InvalidOperationException("multiple image owners for renderer " + id);
                decorations.Add(id, d);
                Increment(meshStates, MeshState(d, camera));
                if (d.meshRenderer)
                {
                    int meshId = d.meshRenderer.GetInstanceID();
                    if (meshOwners.ContainsKey(meshId)) throw new InvalidOperationException("multiple image owners for mesh " + meshId);
                    meshOwners.Add(meshId, d);
                    if (Live(d.meshRenderer)) { if (Live(d.spriteRenderer)) meshAndSprite++; else meshOnly++; }
                    if (Live(d.spriteRenderer) && d.meshRenderer.enabled && !Live(d.meshRenderer)) legacyFalseRejects++;
                }
            }
            int live = decorations.Values.Count(d => Live(d.spriteRenderer));
            int owned = 0, basic = 0, grouped = 0, unsupportedQueues = 0, missingMaterials = 0;
            var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var barriers = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var signatures = new Dictionary<int, string>();
            var rows = new List<Row>();
            foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!Live(renderer)) continue;
                var materials = renderer.sharedMaterials; // never renderer.material(s)
                decorations.TryGetValue(renderer.GetInstanceID(), out var decoration);
                string reason = decoration ? Reason(decoration, decoration.spriteRenderer, materials, camera) : "nonimage";
                if (decoration)
                {
                    if (reason == null || reason == "property-block-unverified") basic++;
                    if (reason == null) owned++;
                    else Increment(reasons, reason);
                }
                if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if (renderer.GetComponentInParent<SortingGroup>()) grouped++;
                if (materials.Length == 0 || materials.Any(m => !m || !m.shader)) missingMaterials++;
                // Null-material rows remain barriers. They make the ordering incomplete.
                for (int slot = 0; slot < Math.Max(1, materials.Length); slot++)
                {
                    var m = slot < materials.Length ? materials[slot] : null;
                    if (m && m.renderQueue <= 2500) unsupportedQueues++;
                    string signature = "";
                    if (reason == null && m && !signatures.TryGetValue(m.GetInstanceID(), out signature))
                    { signature = Signature(m); signatures.Add(m.GetInstanceID(), signature); }
                    rows.Add(new Row { Renderer = renderer, Item = new LiveDecoPolicy.Item(
                        MatShare.SortKey(renderer, m, camera, slot), reason == null, signature, renderer.gameObject.layer) });
                    if (reason != null)
                    {
                        meshOwners.TryGetValue(renderer.GetInstanceID(), out var meshOwner);
                        Increment(barriers, decoration ? "image:" + reason : meshOwner ? "image-mesh:blend=" + Blend(meshOwner) : renderer.GetType().Name);
                    }
                }
            }
            rows.Sort((a, b) => a.Item.Key.CompareTo(b.Item.Key));
            var result = LiveDecoPolicy.Count(rows.Select(r => r.Item).ToArray());
            if (result.Candidates != owned || owned + reasons.Values.Sum() != live || meshStates.Values.Sum() != decorations.Count)
                throw new InvalidOperationException("census partition mismatch");
            bool orderSupported = grouped == 0 && missingMaterials == 0 && unsupportedQueues == 0;
            string verdict = LiveDecoPolicy.Gate(live, result, orderSupported);
            double song = scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
            Log("census frame=" + Time.frameCount + " song=" + song.ToString("F6", CultureInfo.InvariantCulture)
                + " camera_id=" + camera.GetInstanceID() + " camera_mask=" + camera.cullingMask
                + " phase=main-preCull classifier_version=2 live_definition=enabled-active-unforced frustum_filter=0 alpha_filter=0");
            Log("images_all=" + decorations.Count + " live=" + live + " basic_candidates=" + basic
                + " owned_candidates=" + owned + " coverage_pct=" + (live == 0 ? 0 : 100.0 * owned / live).ToString("F3", CultureInfo.InvariantCulture)
                + " remaining_live=" + (live - owned) + " renderer_material_slots=" + rows.Count);
            Log("mesh_diagnostics images_all=" + decorations.Count + " mesh_only_live=" + meshOnly
                + " mesh_and_sprite_live=" + meshAndSprite + " legacy_mesh_false_rejects=" + legacyFalseRejects
                + " distribution_total=" + meshStates.Values.Sum() + " image_union_live=" + (live + meshOnly)
                + " gate_denominator=sprite-live");
            foreach (var p in meshStates) Log("mesh_state " + p.Key + " count=" + p.Value
                + " images_pct=" + (decorations.Count == 0 ? 0 : 100.0 * p.Value / decorations.Count).ToString("F3", CultureInfo.InvariantCulture)
                + " sprite_live_pct=" + (live == 0 || !p.Key.StartsWith("sprite_live=True ", StringComparison.Ordinal) ? 0 : 100.0 * p.Value / live).ToString("F3", CultureInfo.InvariantCulture)
                + " union_live_pct=" + (live + meshOnly == 0 || (!p.Key.StartsWith("sprite_live=True ", StringComparison.Ordinal) && !p.Key.Contains(" mesh_live=True ")) ? 0 : 100.0 * p.Value / (live + meshOnly)).ToString("F3", CultureInfo.InvariantCulture));
            Log("barrier_runs=" + result.BarrierRuns + " mesh_runs=" + result.MeshRuns
                + " key_splits=" + result.KeySplits + " signature_splits=" + result.SignatureSplits
                + " unity_layer_splits=" + result.LayerSplits + " ambiguous_material_ties=" + result.AmbiguousTies
                + " sorting_groups=" + grouped + " opaque_slots=" + unsupportedQueues + " missing_materials=" + missingMaterials);
            foreach (var p in reasons) Log("remaining_reason=" + p.Key + " count=" + p.Value
                + " live_pct=" + (live == 0 ? 0 : 100.0 * p.Value / live).ToString("F3", CultureInfo.InvariantCulture));
            foreach (var p in barriers) Log("barrier_type=" + p.Key + " slots=" + p.Value);
            foreach (var g in rows.Where(r => r.Item.Owned).GroupBy(r => r.Item.Signature).OrderByDescending(g => g.Count()).Take(10))
                Log("candidate_signature count=" + g.Count() + " content=" + g.Key);
            for (int i = 0; i < Math.Min(12, result.MeshRuns); i++)
            {
                var row = rows[result.First[i]]; var k = row.Item.Key;
                Log("run=" + i + " members=" + result.Counts[i] + " first_renderer=" + k.Renderer
                    + " borrowed_material=" + k.Material + " sorting_layer_value=" + k.Layer + " order=" + k.Order
                    + " queue=" + k.Queue + " distance=" + k.Distance.ToString("R", CultureInfo.InvariantCulture));
            }
            Log("GATE1=" + verdict + " required_mesh_runs_le=200 required_coverage_ge=80 runtime_snapshot_only=1"
                + " atlas_assumed=1 draw_estimate_only=1 census_cpu_ms=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        }
        static void Increment(IDictionary<string, int> counts, string key)
        { counts.TryGetValue(key, out int count); counts[key] = count + 1; }
    }
}
#endif
