using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Section 12 only. Explicitly injected into a source copy, never a normal build.
    internal static class FrameGenCaptureProbe
    {
        private static readonly AccessTools.FieldRef<scrCamera, Camera> Cam = AccessTools.FieldRefAccess<scrCamera, Camera>("camobj");
        private static readonly AccessTools.FieldRef<scrCamera, Camera> Overlay = AccessTools.FieldRefAccess<scrCamera, Camera>("Overlaycam");
        private static readonly AccessTools.FieldRef<scrCamera, RenderTexture> RT = AccessTools.FieldRefAccess<scrCamera, RenderTexture>("camRT");
        private struct Row
        {
            public int frame, rendered, mainOrder, overlayOrder, texture;
            public double realtime;
            public Vector3 camera, red, blue;
            public Quaternion rotation;
            public float size, fov;
            public Matrix4x4 projection, view;
        }
        private static Row[] rows;
        private static int count, overflow, missing, renderFrame = -1, order, mainOrder, overlayOrder;
        private static Camera mainCamera, overlayCamera;
        private static Runner runner;
        private static bool finished, inspected, uiExperiment;
        private static double started;
        private static string directory;
        private static readonly WaitForEndOfFrame EndFrame = new WaitForEndOfFrame();

        internal static void Install()
        {
            if (runner != null) return;
            rows = new Row[65536]; count = overflow = missing = 0; finished = inspected = false;
            directory = Path.Combine(Main.Entry.Path, "framegen-capture");
            Directory.CreateDirectory(directory);
            uiExperiment = File.Exists(Path.Combine(Main.Entry.Path, "framegen-ui-experiment.txt"));
            var go = new GameObject("StutterFix.FrameGenCaptureExperiment") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            Camera.onPreCull += PreCull; Camera.onPostRender += PostRender;
            runner.StartCoroutine(Capture());
            Log("experiment only; WaitForEndOfFrame; numeric buffer=65536; files only at inspection/quit");
        }
        private static void PreCull(Camera c)
        {
            if (mainCamera == null && Hitch.Playing && scrCamera.instance != null)
            { mainCamera = Cam(scrCamera.instance); overlayCamera = Overlay(scrCamera.instance); }
            int frame = Time.frameCount;
            if (renderFrame != frame) { renderFrame = frame; order = mainOrder = overlayOrder = 0; }
            ++order;
            if (c == mainCamera) mainOrder = order;
            if (c == overlayCamera) overlayOrder = order;
        }
        private static int lastMainRender = -1;
        private static void PostRender(Camera c) { if (c == mainCamera) lastMainRender = Time.frameCount; }
        private static IEnumerator Capture()
        {
            while (!finished)
            {
                yield return EndFrame;
                if (!Hitch.Playing) continue;
                var sc = scrCamera.instance; var controller = scrController.instance;
                if (sc == null || controller == null) { missing++; continue; }
                mainCamera = Cam(sc); overlayCamera = Overlay(sc);
                var red = controller.planetRed; var blue = controller.planetBlue; var rt = RT(sc);
                if (mainCamera == null || red == null || blue == null) { missing++; continue; }
                if (count == rows.Length) { overflow++; continue; }
                if (count == 0) started = Time.realtimeSinceStartupAsDouble;
                rows[count++] = new Row {
                    frame = Time.frameCount, rendered = lastMainRender, mainOrder = mainOrder, overlayOrder = overlayOrder,
                    realtime = Time.realtimeSinceStartupAsDouble - started, texture = rt == null ? 0 : rt.GetInstanceID(),
                    camera = mainCamera.transform.position, rotation = mainCamera.transform.rotation,
                    size = mainCamera.orthographicSize, fov = mainCamera.fieldOfView,
                    projection = mainCamera.projectionMatrix, view = mainCamera.worldToCameraMatrix,
                    red = red.transform.position, blue = blue.transform.position
                };
                if (!inspected && rows[count - 1].realtime > 8)
                {
                    inspected = true;
                    try { Inspect(rt); } catch (Exception e) { Log("inspection failed: " + e); }
                }
            }
        }
        private static void Inspect(RenderTexture rt)
        {
            using (var f = new StreamWriter(Path.Combine(directory, "inventory.txt")))
            {
                f.WriteLine("screen=" + Screen.width + "x" + Screen.height + " camRT=" + (rt == null ? "null" : rt.width + "x" + rt.height + " " + rt.format));
                foreach (var c in Camera.allCameras)
                    f.WriteLine("camera=" + c.name + " id=" + c.GetInstanceID() + " depth=" + c.depth + " mask=" + c.cullingMask
                        + " target=" + (c.targetTexture == null ? "screen" : c.targetTexture.GetInstanceID().ToString()) + " ortho=" + c.orthographic + " enabled=" + c.enabled);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    f.WriteLine("canvas=" + canvas.name + " root=" + canvas.isRootCanvas + " mode=" + canvas.renderMode + " layer=" + canvas.gameObject.layer
                        + " camera=" + (canvas.worldCamera == null ? "null" : canvas.worldCamera.name) + " enabled=" + canvas.enabled + " order=" + canvas.sortingOrder);
            }
            if (rt != null) Save(rt, "camRT.png");
            Texture2D screen = null;
            try { screen = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(directory, "screen.png"), screen.EncodeToPNG()); }
            finally { if (screen != null) UnityEngine.Object.Destroy(screen); }
            Log("inventory + camRT/screen PNG saved once; capture/readback frame is diagnostic, not a performance sample");
            if (uiExperiment) CaptureOverlayUI();
        }
        private struct CanvasState
        {
            public Canvas canvas; public Camera camera; public float distance;
            public RenderMode mode;
        }
        private struct RectState
        {
            public RectTransform rect; public Vector3 anchored, position; public Vector2 size, min, max, pivot;
            public Vector3 scale; public Quaternion rotation;
        }
        private static bool RectChanged(RectState s)
        {
            return s.rect != null && (!Same(s.rect.anchoredPosition3D, s.anchored) || !Same(s.rect.sizeDelta, s.size) || !Same(s.rect.anchorMin, s.min) || !Same(s.rect.anchorMax, s.max) ||
                !Same(s.rect.pivot, s.pivot) || !Same(s.rect.localScale, s.scale) || !Same(s.rect.localRotation, s.rotation));
        }
        private static bool Same(Vector2 a, Vector2 b) { return a.x.Equals(b.x) && a.y.Equals(b.y); }
        private static bool Same(Vector3 a, Vector3 b) { return a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z); }
        private static bool Same(Quaternion a, Quaternion b) { return a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z) && a.w.Equals(b.w); }
        private static void RestoreRect(RectState s)
        {
            if (s.rect == null) return;
            if (!Same(s.rect.anchorMin, s.min)) s.rect.anchorMin = s.min;
            if (!Same(s.rect.anchorMax, s.max)) s.rect.anchorMax = s.max;
            if (!Same(s.rect.pivot, s.pivot)) s.rect.pivot = s.pivot;
            if (!Same(s.rect.sizeDelta, s.size)) s.rect.sizeDelta = s.size;
            if (!Same(s.rect.localScale, s.scale)) s.rect.localScale = s.scale;
            if (!Same(s.rect.localRotation, s.rotation)) s.rect.localRotation = s.rotation;
            // anchoredPosition is derived from anchors/parent geometry. Re-setting it
            // introduces float rounding; restore the underlying localPosition directly.
            if (!Same(s.rect.localPosition, s.position)) s.rect.localPosition = s.position;
        }
        private static void CaptureOverlayUI()
        {
            // One diagnostic render after the ordinary frame. Restore all borrowed state in finally.
            var canvases = new List<CanvasState>(); var layers = new Dictionary<GameObject, int>();
            var rects = new List<RectState>();
            GameObject go = null; RenderTexture rt = null;
            int changedAfterRestore = 0;
            try
            {
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (renderer.gameObject.layer == 31) throw new InvalidOperationException("UI experiment layer 31 already used by a scene renderer");
                go = new GameObject("FrameGen.UI.Experiment") { hideFlags = HideFlags.HideAndDontSave };
                var camera = go.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = Screen.height * .5f;
                camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0, 0, 0, 0); camera.allowHDR = camera.allowMSAA = false;
                rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
                rt.Create(); camera.targetTexture = rt;
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || !canvas.enabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvases.Add(new CanvasState { canvas = canvas, mode = canvas.renderMode, camera = canvas.worldCamera, distance = canvas.planeDistance });
                    foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                    {
                        if (!layers.ContainsKey(t.gameObject)) layers.Add(t.gameObject, t.gameObject.layer);
                        if (t is RectTransform r) rects.Add(new RectState { rect = r, anchored = r.anchoredPosition3D, position = r.localPosition, size = r.sizeDelta,
                            min = r.anchorMin, max = r.anchorMax, pivot = r.pivot, scale = r.localScale, rotation = r.localRotation });
                        t.gameObject.layer = 31;
                    }
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); Save(rt, "overlay-ui.png");
                Log("UI experiment: overlay roots=" + canvases.Count + "; world-space UI excluded; exact composition unverified");
            }
            finally
            {
                foreach (var s in canvases) if (s.canvas != null)
                { s.canvas.renderMode = s.mode; s.canvas.worldCamera = s.camera; s.canvas.planeDistance = s.distance; }
                foreach (var pair in layers) if (pair.Key != null) pair.Key.layer = pair.Value;
                foreach (var s in rects) RestoreRect(s);
                Canvas.ForceUpdateCanvases();
                // Layout callbacks can rewrite borrowed values during ForceUpdateCanvases.
                // Reapply only those changed values after that flush; report both stages.
                int layoutChanges = 0;
                foreach (var s in rects) if (RectChanged(s))
                { layoutChanges++; RestoreRect(s); }
                foreach (var s in rects) if (RectChanged(s))
                {
                    changedAfterRestore++;
                    if (changedAfterRestore <= 12) Log("UI rect difference: " + s.rect.name + " anchored=" + V(s.anchored) + " -> " + V(s.rect.anchoredPosition3D)
                        + " size=" + s.size.ToString("F6") + " -> " + s.rect.sizeDelta.ToString("F6")
                        + " scale=" + V(s.scale) + " -> " + V(s.rect.localScale) + " rotation=" + s.rotation.ToString("F6") + " -> " + s.rect.localRotation.ToString("F6"));
                }
                int changedCanvases = 0, changedLayers = 0;
                foreach (var s in canvases) if (s.canvas != null && (s.canvas.renderMode != s.mode || s.canvas.worldCamera != s.camera || s.canvas.planeDistance != s.distance)) changedCanvases++;
                foreach (var pair in layers) if (pair.Key != null && pair.Key.layer != pair.Value) changedLayers++;
                if (go != null) UnityEngine.Object.Destroy(go);
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                Log("UI restore: roots=" + canvases.Count + " layers=" + layers.Count + " rects=" + rects.Count + " layout_rewrites=" + layoutChanges
                    + " changed_rects=" + changedAfterRestore + " changed_canvases=" + changedCanvases + " changed_layers=" + changedLayers);
            }
        }
        private static void Save(RenderTexture rt, string name)
        {
            var old = RenderTexture.active; Texture2D texture = null;
            try
            {
                RenderTexture.active = rt; texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; if (texture != null) UnityEngine.Object.Destroy(texture); }
        }
        private static void Log(string text) { Main.Entry.Logger.Log("[프레임생성 캡처] " + text); }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string V(Vector3 v) { return v.x.ToString("R", Inv) + "," + v.y.ToString("R", Inv) + "," + v.z.ToString("R", Inv); }
        internal static void Finish()
        {
            if (finished || rows == null) return;
            finished = true;
            using (var f = new StreamWriter(Path.Combine(directory, "frames.csv")))
            {
                f.Write("frame,realtime_s,main_render_frame,main_order,overlay_order,camRT_id,camera_x,camera_y,camera_z,q_x,q_y,q_z,q_w,ortho_size,fov,red_x,red_y,red_z,blue_x,blue_y,blue_z");
                for (int i = 0; i < 16; i++) f.Write(",projection_" + i);
                for (int i = 0; i < 16; i++) f.Write(",view_" + i);
                f.WriteLine();
                for (int n = 0; n < count; n++)
                {
                    Row r = rows[n];
                    f.Write(r.frame + "," + r.realtime.ToString("R", Inv) + "," + r.rendered + "," + r.mainOrder + "," + r.overlayOrder + "," + r.texture + "," + V(r.camera)
                        + "," + r.rotation.x.ToString("R", Inv) + "," + r.rotation.y.ToString("R", Inv) + "," + r.rotation.z.ToString("R", Inv) + "," + r.rotation.w.ToString("R", Inv)
                        + "," + r.size.ToString("R", Inv) + "," + r.fov.ToString("R", Inv) + "," + V(r.red) + "," + V(r.blue));
                    for (int i = 0; i < 16; i++) f.Write("," + r.projection[i].ToString("R", Inv));
                    for (int i = 0; i < 16; i++) f.Write("," + r.view[i].ToString("R", Inv));
                    f.WriteLine();
                }
            }
            Log("frames=" + count + " missing=" + missing + " overflow=" + overflow + " frames.csv saved at quit");
            foreach (int i in new[] { 0, count / 2, count - 1 }) if (i >= 0 && i < count)
            {
                Row r = rows[i]; Log("sample frame=" + r.frame + " rendered=" + r.rendered + " camera=" + V(r.camera) + " ortho=" + r.size.ToString("R", Inv)
                    + " quaternion=" + r.rotation.ToString("F6") + " red=" + V(r.red) + " blue=" + V(r.blue));
            }
        }
        internal static void Uninstall()
        {
            Camera.onPreCull -= PreCull; Camera.onPostRender -= PostRender;
            if (runner != null) { runner.StopAllCoroutines(); UnityEngine.Object.Destroy(runner.gameObject); runner = null; }
            finished = true; rows = null; mainCamera = overlayCamera = null; lastMainRender = renderFrame = -1;
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
