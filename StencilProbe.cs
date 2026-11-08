#if AUTOTEST
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // 진단: Sprites/Tile 장식을 "스텐실 비트 128 이 있을 때만 그림" 셰이더(원본과 명령어 같음)로 바꾸되 비트를 세우지 않는다.
    // 스텐실이 동작하면 그 장식들이 전부 걸러져 GPU 시간이 크게 준다. 화면이 달라진다(측정 전용).
    internal static class StencilProbe
    {
        static Shader sh;
        static readonly List<KeyValuePair<Material, Shader>> swapped = new List<KeyValuePair<Material, Shader>>();
        internal static string Run(string arg)
        {
            var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
            string fmt = cam && cam.targetTexture ? ("rt depth=" + cam.targetTexture.depth + " ds=" + cam.targetTexture.depthStencilFormat + " color=" + cam.targetTexture.graphicsFormat) : "no rt";
            if (arg == "off")
            {
                foreach (var kv in swapped) if (kv.Key) { var kw = kv.Key.shaderKeywords; int q = kv.Key.renderQueue; kv.Key.shader = kv.Value; kv.Key.shaderKeywords = kw; kv.Key.renderQueue = q; }
                int n0 = swapped.Count; swapped.Clear(); return "되돌림 " + n0 + " | " + fmt;
            }
            if (!sh)
            {
                using (var s = typeof(StencilProbe).Assembly.GetManifestResourceStream("StutterFix.stencilfill"))
                { var ms = new MemoryStream(); s.CopyTo(ms); var b = AssetBundle.LoadFromMemory(ms.ToArray()); sh = b.LoadAllAssets<Shader>().First(x => x.name == "StutterFix/SpritesTileStencil"); }
            }
            int n = 0;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (!m || !m.shader || m.shader.name != "Sprites/Tile") continue;
                var kw = m.shaderKeywords; int q = m.renderQueue;
                swapped.Add(new KeyValuePair<Material, Shader>(m, m.shader)); m.shader = sh; m.shaderKeywords = kw; m.renderQueue = q; m.SetFloat("_SfRef", 128); n++;
            }
            return "바꿈 " + n + " | " + fmt;
        }
    }
}
#endif
