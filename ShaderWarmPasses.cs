using UnityEngine;

namespace StutterFix
{
    internal static class ShaderWarmPasses
    {
        // Preparation only. Own all temporary resources and preserve the caller's
        // render target even when allocation, material creation or a pass fails.
        internal static bool TryWarm(Shader shader)
        {
            RenderTexture previous = null, a = null, b = null;
            Material material = null;
            bool captured = false;
            try
            {
                previous = RenderTexture.active;
                captured = true;
                if (shader == null || !shader.isSupported) return false;
                a = RenderTexture.GetTemporary(16, 16, 0);
                b = RenderTexture.GetTemporary(16, 16, 0);
                if (a == null || b == null) return false;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                int count = material.passCount;
                if (count <= 0) return false;
                RenderTexture.active = a;
                GL.Clear(false, true, Color.clear);
                RenderTexture.active = b;
                GL.Clear(false, true, Color.clear);
                for (int pass = 0; pass < count; pass++) Graphics.Blit(a, b, material, pass);
                return true;
            }
            catch { return false; }
            finally
            {
                if (captured) { try { RenderTexture.active = previous; } catch { } }
                if (material != null) { try { Object.Destroy(material); } catch { } }
                if (b != null) { try { RenderTexture.ReleaseTemporary(b); } catch { } }
                if (a != null) { try { RenderTexture.ReleaseTemporary(a); } catch { } }
            }
        }
    }
}
