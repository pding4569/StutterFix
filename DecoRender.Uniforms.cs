#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal sealed partial class DecoRender
    {
        sealed class MaterialSnapshot
        {
            internal Shader Shader;
            internal int Queue;
            internal string[] Keywords;
            internal readonly List<Uniform> Values = new List<Uniform>();
        }
        struct Uniform
        {
            internal string Name;
            internal int Id, Integer;
            internal ShaderPropertyType Type;
            internal Vector4 Value;
            internal Texture Texture;
            internal Vector2 Scale, Offset;
        }
        MaterialSnapshot ReadUniforms(SpriteRenderer r)
        {
            var m = r.sharedMaterial; var shader = m.shader;
            block.Clear(); r.GetPropertyBlock(block);
            var snapshot = new MaterialSnapshot { Shader = shader, Queue = m.renderQueue, Keywords = m.shaderKeywords };
            for (int p = 0; p < shader.GetPropertyCount(); p++) {
                string name = shader.GetPropertyName(p);
                if (name == "_RendererColor" || name == "_Flip") continue;
                var u = new Uniform { Name = name, Id = Shader.PropertyToID(name), Type = shader.GetPropertyType(p) };
                ReadUniform(m, ref u); snapshot.Values.Add(u);
            }
            return snapshot;
        }
        void ReadUniform(Material m, ref Uniform u)
        {
            int id = u.Id;
            switch (u.Type) {
                case ShaderPropertyType.Color: u.Value = block.HasColor(id) ? block.GetColor(id) : m.GetColor(id); break;
                case ShaderPropertyType.Vector: u.Value = block.HasVector(id) ? block.GetVector(id) : m.GetVector(id); break;
                case ShaderPropertyType.Texture:
                    u.Texture = block.HasTexture(id) ? block.GetTexture(id) : m.GetTexture(id);
                    u.Scale = m.GetTextureScale(id); u.Offset = m.GetTextureOffset(id); break;
                case ShaderPropertyType.Int: u.Integer = block.HasInteger(id) ? block.GetInteger(id) : m.GetInteger(id); break;
                default: u.Value.x = block.HasFloat(id) ? block.GetFloat(id) : m.GetFloat(id); break;
            }
        }
        bool UniformsUnchanged(SpriteRenderer r, MaterialSnapshot snapshot)
        {
            var m = r.sharedMaterial;
            if (m.shader != snapshot.Shader || m.renderQueue != snapshot.Queue) return false;
            var keywords = m.shaderKeywords;
            if (keywords.Length != snapshot.Keywords.Length) return false;
            for (int n = 0; n < keywords.Length; n++) if (keywords[n] != snapshot.Keywords[n]) return false;
            block.Clear(); r.GetPropertyBlock(block);
            if (block.HasColor("_RendererColor") && block.GetColor("_RendererColor") != Color.white
                && block.GetColor("_RendererColor") != r.color) return false;
            if (block.HasVector("_Flip")) {
                var flip = block.GetVector("_Flip");
                if ((flip.x != 1 || flip.y != 1) && (flip.x != (r.flipX ? -1 : 1) || flip.y != (r.flipY ? -1 : 1))) return false;
            }
            if (block.HasVector("_MainTex_ST") && !block.GetVector("_MainTex_ST").Equals(new Vector4(1,1,0,0))) return false;
            if (block.HasTexture("_AlphaTex") && block.GetTexture("_AlphaTex")) return false;
            foreach (var baseline in snapshot.Values) {
                var value = baseline; ReadUniform(m, ref value);
                if (value.Type == ShaderPropertyType.Texture) {
                    if (!value.Scale.Equals(baseline.Scale) || !value.Offset.Equals(baseline.Offset)) return false;
                    if (value.Name == "_MainTex") {
                        if (block.HasTexture(value.Id) && value.Texture != r.sprite.texture) return false;
                    } else if (value.Texture != baseline.Texture) return false;
                } else if (value.Type == ShaderPropertyType.Int) {
                    if (value.Integer != baseline.Integer) return false;
                } else for (int n = 0; n < 4; n++) if (!SameBits(value.Value[n], baseline.Value[n])) return false;
            }
            return (!m.HasProperty("_EnableExternalAlpha") || m.GetFloat("_EnableExternalAlpha") == 0)
                && (!block.HasFloat("_EnableExternalAlpha") || block.GetFloat("_EnableExternalAlpha") == 0);
        }
    }
}
#endif
