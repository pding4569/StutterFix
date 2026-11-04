#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace StutterFix
{
    // Paused-frame research only. No product hooks, pixel reads or background work.
    internal static class TightSpriteProbe
    {
        sealed class Polygon { public float[] xy; public float ratio; }
        sealed class Image { public int width, height; public Polygon polygon; }
        sealed class Change { internal SpriteRenderer Renderer; internal Sprite Before, After; }
        static readonly List<Change> changes = new List<Change>();
        internal static void Stop()
        {
            int restored = 0, replaced = 0;
            var owned = new HashSet<Sprite>();
            foreach (var c in changes)
            {
                if (c.Renderer && c.Renderer.sprite == c.After) { c.Renderer.sprite = c.Before; restored++; }
                else replaced++;
                if (c.After) owned.Add(c.After);
            }
            foreach (var sprite in owned) UnityEngine.Object.Destroy(sprite);
            changes.Clear();
            Main.Entry.Logger.Log("[tightsprite] restored=" + restored + " externally_replaced=" + replaced);
        }
        internal static void Step(string arg)
        {
            if (arg == "off") { Stop(); return; }
            if (!FrameCompare.Paused) throw new InvalidOperationException("tightsprite needs pause");
            var parts = arg.Split(new[] { ' ' }, 2);
            bool dataApi = parts[0].StartsWith("data", StringComparison.Ordinal);
            bool cloneOnly = parts[0].StartsWith("clone", StringComparison.Ordinal);
            int cap = int.Parse(dataApi ? parts[0].Substring(4) : cloneOnly ? parts[0].Substring(5) : parts[0]);
            if (parts.Length != 2 || (cap != 4 && cap != 6 && cap != 8)) throw new ArgumentException("tightsprite 4|6|8 manifest.json; off");
            Stop();
            var lookup = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(parts[1]).Skip(1))
            {
                var cells = line.Split('\t'); int column = 3 + (cap - 4);
                if (cells.Length != 9) throw new InvalidDataException("manifest column count");
                if (string.IsNullOrWhiteSpace(cells[column+1])) continue;
                lookup.Add(cells[0], new Image { width = int.Parse(cells[1]), height = int.Parse(cells[2]),
                    polygon = new Polygon { ratio = float.Parse(cells[column], CultureInfo.InvariantCulture),
                        xy = cells[column+1].Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray() } });
            }
            int candidates = 0, rejected = 0, boundsChanged = 0, apiRejected = 0;
            var reasons = new Dictionary<string, int>();
            var additiveTextures = new Dictionary<string, int>();
            var clones = new Dictionary<Tuple<Sprite, Texture2D>, Sprite>();
            var rendererBlock = new MaterialPropertyBlock();
            var materialBlock = new MaterialPropertyBlock();
            int mainTex = Shader.PropertyToID("_MainTex"), texTransform = Shader.PropertyToID("_MainTex_ST");
            try
            {
                foreach (var r in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
                {
                    if (!r || !r.gameObject.scene.IsValid() || !r.sprite) continue;
                    var s = r.sprite; var t = s.texture;
                    rendererBlock.Clear(); materialBlock.Clear();
                    r.GetPropertyBlock(rendererBlock); r.GetPropertyBlock(materialBlock, 0);
                    var block = materialBlock.isEmpty ? rendererBlock : materialBlock;
                    if (block.HasProperty(mainTex)) t = block.GetTexture(mainTex) as Texture2D;
                    if (!t || !lookup.TryGetValue(t.name, out var image)) continue;
                    var poly = image.polygon;
                    if (poly == null || poly.xy == null || poly.xy.Length < 6 || poly.ratio >= .8f) continue;
                    candidates++;
                    var d = r.GetComponentInParent<scrDecoration>(true);
                    var mat = r.sharedMaterial;
                    if (mat && mat.shader && mat.shader.name == "Legacy Shaders/Particles/Additive")
                    {
                        string info = t.name+"/"+t.width+"x"+t.height+"/"+t.format;
                        additiveTextures.TryGetValue(info, out int count); additiveTextures[info] = count+1;
                    }
                    // Only a neutral-at-alpha-zero known blend; Grab, premultiply,
                    // masks, hitboxes and compressed/downscaled source support excluded.
                    if (!d || !(d is scrVisualDecoration) || d.hitbox != 0 || ((scrVisualDecoration)d).isMask()
                        || r.drawMode != SpriteDrawMode.Simple || r.maskInteraction != SpriteMaskInteraction.None
                        || !mat || !mat.shader || mat.shader.name != "Legacy Shaders/Particles/Additive"
                        || mat.mainTextureScale != Vector2.one || mat.mainTextureOffset != Vector2.zero
                        || (block.HasProperty(texTransform) && block.GetVector(texTransform) != new Vector4(1,1,0,0))
                        || t.format != TextureFormat.RGBA32 || t.mipmapCount != 1 || t.anisoLevel > 1
                        || t.filterMode != FilterMode.Bilinear || t.wrapMode != TextureWrapMode.Repeat
                        || t.width != image.width || t.height != image.height || s.packed
                        || r.sharedMaterials.Length != 1
                        || s.rect != new Rect(0, 0, s.texture.width, s.texture.height) || s.border != Vector4.zero)
                    {
                        rejected++;
                        string reason = !d ? "no-decoration" : !(d is scrVisualDecoration) ? "type/"+d.GetType().Name : d.hitbox != 0 ? "hitbox" : ((scrVisualDecoration)d).isMask() ? "mask"
                            : !mat || !mat.shader ? "no-shader" : mat.shader.name != "Legacy Shaders/Particles/Additive" ? "shader/"+mat.shader.name
                            : r.drawMode != SpriteDrawMode.Simple ? "draw-mode" : r.maskInteraction != SpriteMaskInteraction.None ? "mask-interaction"
                            : mat.mainTextureScale != Vector2.one || mat.mainTextureOffset != Vector2.zero ? "texture-transform"
                            : t.format != TextureFormat.RGBA32 ? "format/"+t.format : t.width != image.width || t.height != image.height ? "dimensions"
                            : t.mipmapCount != 1 || t.anisoLevel > 1 ? "mips-aniso" : t.filterMode != FilterMode.Bilinear || t.wrapMode != TextureWrapMode.Repeat ? "sampler"
                            : s.packed ? "packed" : s.rect != new Rect(0,0,t.width,t.height) ? "rect" : "border";
                        reasons.TryGetValue(reason, out int count); reasons[reason] = count+1;
                        continue;
                    }
                    var key = Tuple.Create(s, t);
                    if (!clones.TryGetValue(key, out var clone))
                    {
                        clone = UnityEngine.Object.Instantiate(s);
                        clone.name = s.name + ".tight-probe";
                        int n = poly.xy.Length / 2;
                        var v = new Vector2[n + 2];
                        for (int i = 0; i < n; i++) v[i] = new Vector2(poly.xy[i * 2]*s.rect.width/t.width, poly.xy[i * 2 + 1]*s.rect.height/t.height);
                        // Unreferenced original extrema test native bound preservation.
                        v[n] = Vector2.zero; v[n + 1] = s.rect.size;
                        var indices = new ushort[(n - 2) * 3];
                        for (int i = 1; i < n - 1; i++)
                        { indices[(i-1)*3] = 0; indices[(i-1)*3+1] = (ushort)i; indices[(i-1)*3+2] = (ushort)(i+1); }
                        if (!dataApi && !cloneOnly) clone.OverrideGeometry(v, indices);
                        else if (dataApi)
                        {
                            clone.SetVertexCount(v.Length);
                            using (var position = new NativeArray<Vector3>(v.Select(p => new Vector3((p.x-s.pivot.x)/s.pixelsPerUnit, (p.y-s.pivot.y)/s.pixelsPerUnit, 0)).ToArray(), Allocator.Temp))
                                clone.SetVertexAttribute(VertexAttribute.Position, position);
                            using (var uv = new NativeArray<Vector2>(v.Select(p => new Vector2(p.x/s.rect.width, p.y/s.rect.height)).ToArray(), Allocator.Temp))
                                clone.SetVertexAttribute(VertexAttribute.TexCoord0, uv);
                            using (var triangles = new NativeArray<ushort>(indices, Allocator.Temp)) clone.SetIndices(triangles);
                        }
                        if (!cloneOnly && (clone.vertices.Length != v.Length || !clone.triangles.SequenceEqual(indices)))
                        { apiRejected++; UnityEngine.Object.Destroy(clone); continue; }
                        if (!clone.bounds.Equals(s.bounds) || !clone.rect.Equals(s.rect) || !clone.pivot.Equals(s.pivot) || clone.pixelsPerUnit != s.pixelsPerUnit)
                        { boundsChanged++; UnityEngine.Object.Destroy(clone); continue; }
                        clones.Add(key, clone);
                    }
                    var before = r.bounds;
                    r.sprite = clone;
                    changes.Add(new Change { Renderer = r, Before = s, After = clone });
                    if (!r.bounds.Equals(before)) boundsChanged++;
                }
                Main.Entry.Logger.Log("[tightsprite] api=" + (cloneOnly ? "clone" : dataApi ? "data" : "override") + " cap=" + cap + " candidates=" + candidates + " changed=" + changes.Count + " shader_or_state_rejected=" + rejected + " bounds_changed=" + boundsChanged + " api_rejected=" + apiRejected + " cloned_sprites=" + clones.Count + " vertices_include_two_unused_extrema=" + (cloneOnly ? 0 : 1));
                Main.Entry.Logger.Log("[tightsprite] rejected=" + string.Join(";", reasons.Select(p => p.Key+"="+p.Value)));
                Main.Entry.Logger.Log("[tightsprite] additive_textures=" + string.Join(";", additiveTextures.OrderByDescending(p => p.Value).Take(24).Select(p => p.Key+"="+p.Value)));
                if (boundsChanged != 0) { Stop(); throw new InvalidOperationException("bounds changed"); }
            }
            catch { Stop(); throw; }
        }
    }
}
#endif
