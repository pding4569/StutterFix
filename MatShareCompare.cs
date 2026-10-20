#if DEV
using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StutterFix
{
    internal static class MatShareCompare
    {
        // Isolate ACTUAL renderers on an unused layer. No copy of game camera callbacks/chain.
        // A0/A1/B/A2 tests their composition in one synchronous frame; not whole-screen equivalence.
        internal static void Run(SpriteRenderer[] rows, Func<SpriteRenderer, Material> original,
            Func<SpriteRenderer, Material> shared, Camera source)
        {
            if (!source || !source.orthographic) throw new InvalidOperationException("pair compare requires orthographic main camera");
            if (GpuSplit.Busy || GpuHide.Busy || GpuCensus.Busy) throw new InvalidOperationException("other diagnostic busy");
            int layer = -1;
            var scene = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r && r.gameObject.scene.IsValid()).ToArray();
            for (int i = 31; i >= 0; i--) if (!scene.Any(r => r.gameObject.layer == i)) { layer = i; break; }
            if (layer < 0) throw new InvalidOperationException("no renderer-free layer");
            var previous = RenderTexture.active;
            GameObject go = null; RenderTexture rt = null; Texture2D read = null;
            int tested = 0, changed = 0, invalid = 0;
            try
            {
                go = new GameObject("StutterFix.MatShare.PairCompare") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>(); cam.CopyFrom(source); cam.enabled = false;
                cam.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                cam.worldToCameraMatrix = source.worldToCameraMatrix; cam.projectionMatrix = source.projectionMatrix;
                cam.cullingMask = 1 << layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
                cam.rect = new Rect(0,0,1,1); cam.allowHDR = false; cam.allowMSAA = false;
                int width = source.targetTexture ? source.targetTexture.width : source.pixelWidth;
                int height = source.targetTexture ? source.targetTexture.height : source.pixelHeight;
                rt = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) { antiAliasing = 1 }; rt.Create(); cam.targetTexture = rt;
                read = new Texture2D(width,height,TextureFormat.RGBA32,false);
                for (int i = 0; i + 1 < rows.Length && tested < 8; i++)
                {
                    var a = rows[i]; var b = rows[i+1];
                    if (!a.bounds.Intersects(b.bounds) || a.sortingLayerID != b.sortingLayerID || a.sortingOrder != b.sortingOrder) continue;
                    int al = a.gameObject.layer, bl = b.gameObject.layer;
                    int ao = a.sortingOrder, bo = b.sortingOrder;
                    var am = a.sharedMaterial; var bm = b.sharedMaterial;
                    try
                    {
                        a.gameObject.layer = b.gameObject.layer = layer;
                        a.sharedMaterial = original(a); b.sharedMaterial = original(b);
                        var a0 = Capture(cam,rt,read); var a1 = Capture(cam,rt,read);
                        // Order-sensitive positive control: force each member in front in turn.
                        a.sortingOrder = 1; b.sortingOrder = 0; var frontA = Capture(cam,rt,read);
                        a.sortingOrder = 0; b.sortingOrder = 1; var frontB = Capture(cam,rt,read);
                        a.sortingOrder = ao; b.sortingOrder = bo;
                        Diff(frontA,frontB,out int sensitivity,out _);
                        // Solo images prove both selected objects contribute non-background pixels.
                        bool ae = a.enabled, be = b.enabled; Color32[] soloA, soloB;
                        try { b.enabled = false; soloA = Capture(cam,rt,read); b.enabled = be; a.enabled = false; soloB = Capture(cam,rt,read); }
                        finally { a.enabled = ae; b.enabled = be; }
                        a.sharedMaterial = shared(a); b.sharedMaterial = shared(b);
                        var candidate = Capture(cam,rt,read);
                        a.sharedMaterial = original(a); b.sharedMaterial = original(b);
                        var a2 = Capture(cam,rt,read);
                        Diff(a0,a1,out int control0,out _); Diff(a1,a2,out int control1,out _);
                        Diff(a1,candidate,out int max,out int pixels);
                        int overlap = 0;
                        for (int k = 0; k < soloA.Length; k++) if (soloA[k].a != 0 && soloB[k].a != 0) overlap++;
                        Diff(a1,frontA,out int matchesA,out _); Diff(a1,frontB,out int matchesB,out _);
                        Diff(candidate,frontA,out int candidateA,out _); Diff(candidate,frontB,out int candidateB,out _);
                        bool valid = control0 == 0 && control1 == 0 && overlap > 0 && sensitivity > 0 && (matchesA == 0 || matchesB == 0);
                        tested++; if (!valid) invalid++; else if (pixels != 0) changed++;
                        Main.Entry.Logger.Log("[matshare] pair frame=" + Time.frameCount + " a=" + a.GetInstanceID() + " b=" + b.GetInstanceID()
                            + " same_run=" + (shared(a) == shared(b)) + " valid=" + valid + " original_front=" + (matchesA == 0 ? "a" : matchesB == 0 ? "b" : "unknown")
                            + " candidate_front=" + (candidateA == 0 ? "a" : candidateB == 0 ? "b" : "unknown") + " sensitivity=" + sensitivity + " overlap_pixels=" + overlap + " rgba_max=" + max + " different_pixels=" + pixels
                            + " controls=" + control0 + "," + control1 + " scope=isolated-actual-renderer-pair-RGBA8 timing_contaminated=1");
                    }
                    finally { a.sharedMaterial = am; b.sharedMaterial = bm; a.sortingOrder = ao; b.sortingOrder = bo; a.gameObject.layer = al; b.gameObject.layer = bl; }
                }
                Main.Entry.Logger.Log("[matshare] compare tested=" + tested + " invalid=" + invalid + " changed=" + changed + " whole_screen_proven=0");
            }
            finally
            {
                RenderTexture.active = previous;
                if (go) Object.DestroyImmediate(go);
                if (read) Object.DestroyImmediate(read);
                if (rt) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
        static Color32[] Capture(Camera cam, RenderTexture rt, Texture2D read)
        {
            cam.Render(); RenderTexture.active = rt;
            read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false); read.Apply(false); return read.GetPixels32();
        }
        static void Diff(Color32[] a, Color32[] b, out int max, out int pixels)
        {
            max = pixels = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int d = Math.Max(Math.Max(Math.Abs(a[i].r-b[i].r),Math.Abs(a[i].g-b[i].g)),Math.Max(Math.Abs(a[i].b-b[i].b),Math.Abs(a[i].a-b[i].a)));
                max = Math.Max(max,d); if (d != 0) pixels++;
            }
        }
    }
}
#endif
