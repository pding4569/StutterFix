using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
namespace StutterFix
{
    // Admission, material identity and painter keys shared with the research paths.
    internal static class GpuDecoScene
    {
        sealed class Image { internal scrVisualDecoration D; internal SpriteRenderer R; internal Renderer[] Dependents; }
        static void Increment(SortedDictionary<string,int> values,string key) { values.TryGetValue(key,out int n);values[key]=n+1; }
        static readonly AccessTools.FieldRef<scrParallax, bool> Inited = AccessTools.FieldRefAccess<scrParallax, bool>("inited");
        static readonly AccessTools.FieldRef<scrParallax, bool> IsDecoration = AccessTools.FieldRefAccess<scrParallax, bool>("isDecoration");
        static readonly AccessTools.FieldRef<scrParallax, Transform> Cached = AccessTools.FieldRefAccess<scrParallax, Transform>("cachedTransform");
        static readonly AccessTools.FieldRef<scrVisualDecoration, bool> MeshOn = AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> Blend = AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        static string F(float f) => BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture);
        static string V(Vector4 v) => F(v.x) + "," + F(v.y) + "," + F(v.z) + "," + F(v.w);
        internal static SortedDictionary<string, string> Properties(Material m)
        {
            var p = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!m || !m.shader) { p["missing"] = "1"; return p; }
            p["shader"] = m.shader.GetInstanceID().ToString(); p["queue"] = m.renderQueue.ToString();
            p["keywords"] = string.Join(",", m.shaderKeywords.OrderBy(k => k, StringComparer.Ordinal));
            p["instancing"] = m.enableInstancing.ToString(); p["doubleSidedGI"] = m.doubleSidedGI.ToString();
            p["GI"] = m.globalIlluminationFlags.ToString();
            foreach (string n in new[] { "_SrcBlend", "_DstBlend", "_BlendOp", "_ZTest", "_ZWrite", "_Cull" })
                p[n] = m.HasProperty(n) ? F(m.GetFloat(n)) : "<absent>";
            for (int i = 0; i < m.passCount; i++) p["pass:" + m.GetPassName(i)] = m.GetShaderPassEnabled(m.GetPassName(i)).ToString();
            for (int i = 0; i < m.shader.GetPropertyCount(); i++)
            {
                string n = m.shader.GetPropertyName(i); string value;
                switch (m.shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: value = V(m.GetColor(n)); break;
                    case ShaderPropertyType.Vector: value = V(m.GetVector(n)); break;
                    case ShaderPropertyType.Texture:
                        var t = m.GetTexture(n); var s = m.GetTextureScale(n); var o = m.GetTextureOffset(n);
                        value = (t ? t.GetInstanceID() : 0) + ":" + V(new Vector4(s.x,s.y,o.x,o.y)); break;
                    case ShaderPropertyType.Int: value = m.GetInteger(n).ToString(); break;
                    default: value = F(m.GetFloat(n)); break;
                }
                p[n] = value;
            }
            return p;
        }
        internal static SceneRenderOrder.Key SortKey(Renderer r, Material m, Camera cam, int slot = 0)
            => new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(r.sortingLayerID), r.sortingOrder,
                m ? m.renderQueue : -1, Distance(r, cam), m ? m.GetInstanceID() : 0, r.GetInstanceID(), slot);
        internal static float Distance(Renderer r, Camera cam)
        {
            Vector3 pos = r.bounds.center;
            var sr = r as SpriteRenderer;
            if (sr && sr.spriteSortPoint == SpriteSortPoint.Pivot) pos = r.transform.position;
            float distance;
            var mode = cam.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = cam.orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective;
            if (mode == TransparencySortMode.CustomAxis)
                distance = Vector3.Dot(pos, cam.transparencySortMode == TransparencySortMode.CustomAxis ? cam.transparencySortAxis : GraphicsSettings.transparencySortAxis);
            else distance = mode == TransparencySortMode.Orthographic ? Vector3.Dot(pos - cam.transform.position, cam.transform.forward) : (pos - cam.transform.position).sqrMagnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance)) throw new InvalidOperationException("nonfinite sort distance");
            return distance;
        }
        internal static Camera FindMain()
        {
            Camera main = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c.gameObject.scene.IsValid() && c.name == "Camera" && c.gameObject.activeInHierarchy)
                {
                    if (main) throw new InvalidOperationException("gpusplit: ambiguous main Camera");
                    main = c;
                }
            if (!main) throw new InvalidOperationException("gpusplit: main Camera missing");
            return main;
        }
        internal class BlockCensus
        {
            readonly MaterialPropertyBlock rendererBlock = new MaterialPropertyBlock();
            readonly MaterialPropertyBlock slotBlock = new MaterialPropertyBlock();
            readonly Dictionary<int, string[]> shaderNames = new Dictionary<int, string[]>();
            internal readonly SortedDictionary<string, int> Names = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal readonly SortedDictionary<string, int> States = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal readonly SortedDictionary<string, int> Custom = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal bool Nonempty;

            static readonly string[] Defaults = { "_MainTex", "_MainTex_ST", "_Color", "_RendererColor", "_Flip", "_EnableExternalAlpha", "_AlphaTex", "_AlphaTex_ST" };
            // Names recovered from ShaderUtilities, ADOStartup and SpriteAlphaMask.
            static IEnumerable<string> KnownNames()
            {
                foreach (string n in Defaults) yield return n;
                foreach (string n in new[] { "_BLENDMODES_OverlayColor", "_BLENDMODES_OverlayTexture", "_BLENDMODES_OverlayTexture_ST",
                    "_BLENDMODES_StencilId", "_BLENDMODES_BlendStencilComp", "_BLENDMODES_NormalStencilComp",
                    "RepeatX", "RepeatY", "_Opacity", "_Tile", "_ARot", "_VisibleOutsideMask", "_DisableTexture1" }) yield return n;
                for (int i = 1; i <= 5; i++)
                {
                    yield return "_Texture" + i;
                    if (i == 1) continue;
                    foreach (string suffix in new[] { "Scale", "Pos", "Rot" }) yield return "_Texture" + i + suffix;
                    yield return "_DisableTexture" + i;
                }
            }
            string[] ProbeNames(Material material)
            {
                var shader = material ? material.shader : null;
                int id = shader ? shader.GetInstanceID() : 0;
                if (shaderNames.TryGetValue(id, out var names)) return names;
                var all = new SortedSet<string>(KnownNames(), StringComparer.Ordinal);
                if (shader) for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    string name = shader.GetPropertyName(i); all.Add(name);
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture) all.Add(name + "_ST");
                }
                names = all.ToArray(); shaderNames.Add(id, names); return names;
            }
            static string Types(MaterialPropertyBlock block, string name)
            {
                string types = "";
                if (block.HasTexture(name)) types += "texture,";
                if (block.HasFloat(name)) types += "float,";
                if (block.HasVector(name)) types += "vector,";
                if (block.HasColor(name)) types += "color,";
                if (block.HasMatrix(name)) types += "matrix,";
                if (block.HasInteger(name)) types += "integer,";
                if (block.HasBuffer(name)) types += "buffer,";
                if (block.HasConstantBuffer(name)) types += "constant-buffer,";
                return types.Length == 0 ? "other" : types.TrimEnd(',');
            }
            internal string Read(SpriteRenderer renderer, Material[] materials)
            {
                rendererBlock.Clear(); slotBlock.Clear(); Nonempty = false;
                bool has = renderer.HasPropertyBlock();
                if (has) renderer.GetPropertyBlock(rendererBlock);
                // Eligible images have one material. Other images are diagnosed at
                // slot 0 only and are still rejected by the material-slots guard.
                if (has && materials.Length != 0) renderer.GetPropertyBlock(slotBlock, 0);
                var effective = slotBlock.isEmpty ? rendererBlock : slotBlock;
                Nonempty = !effective.isEmpty;
                Increment(States, "has=" + has + ",renderer_empty=" + rendererBlock.isEmpty + ",slot0_empty=" + slotBlock.isEmpty
                    + ",effective=" + (slotBlock.isEmpty ? "renderer" : "slot0"));
                string firstCustom = null; int observed = 0;
                foreach (string name in ProbeNames(materials.Length == 0 ? null : materials[0]))
                {
                    if (rendererBlock.HasProperty(name)) Increment(Names, "renderer:" + name + ":" + Types(rendererBlock, name));
                    if (slotBlock.HasProperty(name)) Increment(Names, "slot0:" + name + ":" + Types(slotBlock, name));
                    if (!effective.HasProperty(name)) continue;
                    observed++; Increment(Names, "effective:" + name + ":" + Types(effective, name));
                    bool basic = Defaults.Contains(name);
                    if (!basic || !DefaultValue(effective, name, renderer))
                    {
                        Increment(Custom, name);
                        if (firstCustom == null) firstCustom = name;
                    }
                }
                if (firstCustom != null) return "property-block-custom:" + firstCustom;
                return Nonempty && observed == 0 ? "property-block-unresolved" : null;
            }
            static bool DefaultValue(MaterialPropertyBlock block, string name, SpriteRenderer renderer)
            {
                if (name == "_MainTex") return block.HasTexture(name) && renderer.sprite && block.GetTexture(name) == renderer.sprite.texture;
                if (name == "_AlphaTex") return block.HasTexture(name) && !block.GetTexture(name);
                if (name == "_EnableExternalAlpha") return block.HasFloat(name) && block.GetFloat(name) == 0;
                if (name == "_Color" || name == "_RendererColor")
                    return block.HasColor(name) && (block.GetColor(name).Equals(Color.white) || block.GetColor(name).Equals(renderer.color));
                if (!block.HasVector(name)) return false;
                var value = block.GetVector(name);
                if (name == "_Flip") return value.x == (renderer.flipX ? -1 : 1) && value.y == (renderer.flipY ? -1 : 1);
                return value.Equals(new Vector4(1, 1, 0, 0)); // identity texture ST only
            }
        }

        internal static string Reason(scrVisualDecoration d, SpriteRenderer r, Material[] materials, Camera camera)
        {
            if ((camera.cullingMask & (1 << r.gameObject.layer)) == 0) return "camera-layer";
            if (d.GetType() != typeof(scrVisualDecoration)) return "subclass";
            if (d.isMask() || r.maskInteraction != SpriteMaskInteraction.None) return "mask";
            if (d.hitbox != 0) return "hitbox";
            // The game toggles meshRendererObj.SetActive(), not Renderer.enabled.
            // A dormant prefab mesh with enabled=true is not a live draw barrier.
            if ((d.meshRenderer && d.meshRenderer.enabled && d.meshRenderer.gameObject.activeInHierarchy && !d.meshRenderer.forceRenderingOff)) return "mesh-live:blend=" + Blend(d) + "(" + (int)Blend(d) + ")";
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
            return null;
        }
        static string GeometryReason(Image i, scrDecorationManager expectedManager)
        {
            var d = i.D; var r = i.R; var p = d.parallax;
            if (!d || !r || d.GetType() != typeof(scrVisualDecoration)) return "subclass";
            if (d.hitbox != 0 || d.isMask() || r.maskInteraction != SpriteMaskInteraction.None) return "hitbox-mask";
            if (d.followPlanet || d.stickToFloor || d.syncFloorDepth) return "parent-follow";
            if (d.repeatX != 1 || d.repeatY != 1 || r.drawMode != SpriteDrawMode.Simple) return "repeat-tile";
            if (MeshOn(d) || (d.meshRenderer && d.meshRenderer.enabled && d.meshRenderer.gameObject.activeInHierarchy)) return "mesh-mode";
            if ((int)Blend(d) != 0 || (d.blendModeEffect && d.blendModeEffect.enabled)) return "blend";
            if (!p || !Inited(p) || !IsDecoration(p) || !ReferenceEquals(p.decoration, d)
                || !ReferenceEquals(Cached(p), d.pivotTrans) || p.enabled) return "parallax-binding";
            if (!d.pivotTrans || d.pivotTrans.parent != expectedManager.transform) return "pivot-parent";
            if (!d.childTransform) return "child-missing";
            var cr = d.childTransform.localRotation;
            if (cr.x != 0 || cr.y != 0) return "nonplanar-child";
            if (!ADOBase.controller || p.cameraTransform != ADOBase.controller.camy.transform) return "camera-binding";
            if (d.cfpCache != null && d.cfpCache.Length != 0) return "component-filter";
            if (i.Dependents != null) foreach (var dependent in i.Dependents)
                if (dependent && dependent != r && dependent.enabled && dependent.gameObject.activeInHierarchy
                    && !dependent.forceRenderingOff) return "live-child-renderer";
            if ((d.selectionBordersObject && d.selectionBordersObject.activeInHierarchy)
                || (d.hitboxBordersObject && d.hitboxBordersObject.activeInHierarchy)) return "editor-borders";
            if (!r.sprite || !r.sprite.texture || r.sprite.associatedAlphaSplitTexture) return "sprite-texture";
            return null;
        }
        internal static string LiveGpuReason(scrVisualDecoration d, Camera cam, scrDecorationManager mgr, BlockCensus blocks)
        {
            var r = d.spriteRenderer;
            // At t=0 (gpudeco live prepare), decorations that fade in during 258..276s have alpha==0 (d.GetVisible()==false).
            // Do not reject by current alpha here; VisibleInWindow already verified window visibility.
            if (!r || !d.pivotTrans || !r.enabled || !r.gameObject.activeInHierarchy
                || (r.forceRenderingOff && !InvisibleSkip.IsHidden(d))) return "inactive";
            var p = d.parallax;
            if (p && (!Inited(p) || p.cameraTransform == null))
            {
                try { d.UpdatePosition(); } catch { }
            }
            bool simpleHierarchy = d.pivotTrans.childCount == 1 && d.childTransform && d.childTransform.childCount == 1
                && r.transform.parent == d.childTransform && r.transform.childCount == 0;
            var deps = simpleHierarchy ? null : d.pivotTrans.GetComponentsInChildren<Renderer>(true);
            var image = new Image { D = d, R = r, Dependents = deps };
            string why = GeometryReason(image, mgr) ?? Reason(d, r, r.sharedMaterials, cam);
            if (why != null) return why;
            if (r.HasPropertyBlock())
            {
                why = blocks.Read(r, r.sharedMaterials);
                if (why != null) return why;
            }
            return simpleHierarchy ? null : LiveDecoScripts.Reason(d.pivotTrans.GetComponentsInChildren<MonoBehaviour>(true), d, r);
        }
    }
}
