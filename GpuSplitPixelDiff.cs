#if DEV || AUTOTEST
using System;
using System.Collections;
using UnityEngine;

namespace StutterFix
{
    // Explicit synchronous readback. Never schedule inside a timing window.
    internal sealed class GpuSplitPixelDiff : MonoBehaviour
    {
        internal static bool Busy;
        static GpuSplitPixelDiff instance;
        readonly GpuSplitLedger<Renderer> ledger = new GpuSplitLedger<Renderer>(r => r, r => r.forceRenderingOff, (r, v) => r.forceRenderingOff = v);
        GpuSplitPolicy.Selection selection;
        string selector;
        Camera camera;
        bool hiding;
        int selected;
        Exception classificationError;
        RenderTexture seed, source;
        int seedFrame = -1;
        Matrix4x4 seedView, seedProjection, seedCulling;
        CameraClearFlags seedClear;
        Exception seedError;
        internal static void Begin(GpuSplitPolicy.Selection selection, string selector)
        {
            Busy = true;
            var p = new GameObject("StutterFix.GpuSplit.PixelDiff").AddComponent<GpuSplitPixelDiff>();
            instance = p;
            p.selection = selection; p.selector = selector;
            try { p.camera = GpuSplit.FindMain(); Camera.onPreCull += p.CaptureSeed; p.StartCoroutine(p.Capture()); }
            catch { p.Cleanup(); Destroy(p.gameObject); throw; }
        }
        void CaptureSeed(Camera c)
        {
            if (c != camera) return;
            Camera.onPreCull -= CaptureSeed;
            var active = RenderTexture.active;
            try
            {
                source = c.targetTexture;
                if (!source || source.format != RenderTextureFormat.ARGB32 || source.antiAliasing != 1 ||
                    source.dimension != UnityEngine.Rendering.TextureDimension.Tex2D || source.volumeDepth != 1)
                    throw new InvalidOperationException("requires main ARGB32 non-MSAA 2D targetTexture; unsupported format is not a pass");
                if (c.clearFlags == CameraClearFlags.Nothing)
                    throw new InvalidOperationException("Nothing clear mode requires depth preservation; unsupported");
                seedFrame = Time.frameCount;
                seedView = c.worldToCameraMatrix; seedProjection = c.projectionMatrix; seedCulling = c.cullingMatrix; seedClear = c.clearFlags;
                seed = RenderTexture.GetTemporary(source.descriptor);
                // Capture the INPUT of the real main-camera render, never its already drawn output.
                Graphics.Blit(source, seed);
            }
            catch (Exception e) { seedError = e; }
            finally { RenderTexture.active = active; }
        }
        IEnumerator Capture()
        {
            // Render outside camera callbacks, after all game LateUpdates and normal rendering.
            yield return new WaitForEndOfFrame();
            try { Compare(); }
            catch (Exception e) { GpuSplit.Log("pixeldiff filter=" + selector + " valid=0 error=" + e.Message); }
            finally { Cleanup(); Destroy(gameObject); }
        }
        void BeforeCamera(Camera c)
        {
            if (c != camera || !hiding) return;
            try { Select(c); }
            catch (Exception e) { classificationError = e; ledger.Restore(); }
        }
        void Select(Camera c)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(c.cullingMatrix);
            // Slow diagnostic discovery intentionally includes renderers created this frame.
            foreach (var r in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (!GpuSplit.IsLiveImage(r, c) || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                var b = GpuSplit.ConservativeBounds(r);
                if (selection.MatchesLive(true, false, GeometryUtility.TestPlanesAABB(planes, b), r.color.a, GpuSplit.AreaPercent(c, b)))
                { ledger.Hide(r); selected++; }
            }
        }
        void Compare()
        {
            if (seedError != null) throw new InvalidOperationException("input capture failed: " + seedError.Message);
            if (!camera || !seed || seedFrame != Time.frameCount) throw new InvalidOperationException("no same-frame natural main-camera input captured");
            var original = camera.targetTexture;
            if (original != source || camera.worldToCameraMatrix != seedView || camera.projectionMatrix != seedProjection ||
                camera.cullingMatrix != seedCulling || camera.clearFlags != seedClear)
                throw new InvalidOperationException("main camera state changed after natural input capture");
            if (camera.rect != new Rect(0, 0, 1, 1)) throw new InvalidOperationException("requires full main viewport");
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                if (canvas && canvas.enabled && canvas.gameObject.activeInHierarchy && canvas.gameObject.scene.IsValid()
                    && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    && (canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera == camera :
                        (camera.cullingMask & (1 << canvas.gameObject.layer)) != 0))
                    throw new InvalidOperationException("main camera contains UI; main-only UI-free comparison unavailable");
            var descriptor = original.descriptor;
            RenderTexture shot = null;
            var active = RenderTexture.active;
            int captureFrame = Time.frameCount;
            try
            {
                shot = RenderTexture.GetTemporary(descriptor);
                var natural = Read(original);
                Camera.onPreCull += BeforeCamera;
                Color32[] Take(bool hide)
                {
                    ledger.Restore(); hiding = hide;
                    RenderTexture.active = shot; GL.Clear(true, true, Color.clear);
                    Graphics.Blit(seed, shot); // identical color seed for Depth/Nothing clear modes
                    camera.targetTexture = shot;
                    try
                    {
                        camera.Render();
                        if (classificationError != null) throw new InvalidOperationException("classification failed", classificationError);
                        return Read(shot);
                    }
                    finally { hiding = false; ledger.Restore(); camera.targetTexture = original; }
                }
                var a0 = Take(false); var a1 = Take(false); var b = Take(true); var a2 = Take(false);
                GpuSplitPolicy.PixelDiff(a0.Length, i => Pack(natural[i]), i => Pack(a0[i]), out int naturalMax, out int naturalPixels);
                GpuSplitPolicy.PixelDiff(a0.Length, i => Pack(a0[i]), i => Pack(a1[i]), out int controlMax, out int controlPixels);
                GpuSplitPolicy.PixelDiff(a1.Length, i => Pack(a1[i]), i => Pack(a2[i]), out int afterMax, out int afterPixels);
                GpuSplitPolicy.PixelDiff(a1.Length, i => Pack(a1[i]), i => Pack(b[i]), out int max, out int pixels);
                bool valid = Time.frameCount == captureFrame && naturalPixels == 0 && controlPixels == 0 && afterPixels == 0 && selected > 0;
                GpuSplit.Log("pixeldiff filter=" + selector + " frame=" + captureFrame + " camera_id=" + camera.GetInstanceID()
                    + " size=" + original.width + "x" + original.height + " selected=" + selected + " rgba_max=" + max + " different_pixels=" + pixels
                    + " control_max=" + Math.Max(controlMax, afterMax) + " control_pixels=" + (controlPixels + afterPixels)
                    + " natural_max=" + naturalMax + " natural_pixels=" + naturalPixels
                    + " valid=" + (valid ? 1 : 0) + " sample_equal=" + (valid && pixels == 0 ? 1 : 0)
                    + " scope=main-camera-RT-RGBA8-only timing_contaminated=1; no whole-song or other-camera certificate");
            }
            finally
            {
                Camera.onPreCull -= BeforeCamera; hiding = false; ledger.Restore();
                camera.targetTexture = original; RenderTexture.active = active;
                if (shot) RenderTexture.ReleaseTemporary(shot);
            }
        }
        static uint Pack(Color32 p) => (uint)p.r | ((uint)p.g << 8) | ((uint)p.b << 16) | ((uint)p.a << 24);
        static Color32[] Read(RenderTexture rt)
        {
            var active = RenderTexture.active;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(false);
                return tex.GetPixels32();
            }
            finally { RenderTexture.active = active; Destroy(tex); }
        }
        internal static void Stop()
        {
            if (!instance) return;
            var p = instance;
            p.StopAllCoroutines(); p.Cleanup(); Destroy(p.gameObject);
        }
        void Cleanup()
        {
            Camera.onPreCull -= CaptureSeed; Camera.onPreCull -= BeforeCamera; ledger.Restore();
            if (seed) { RenderTexture.ReleaseTemporary(seed); seed = null; }
            if (instance == this) { Busy = false; instance = null; }
        }
        void OnDisable() { Cleanup(); }
        void OnDestroy() { Cleanup(); }
    }
}
#endif
