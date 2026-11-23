using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StutterFix
{
    // Explicit, synchronous offscreen diagnostic. Never called in the steady-state frame path.
    internal static class HitMeterAlphaProbe
    {
        private struct Sample { internal byte Vertex; internal float Alpha; }
        // quick: 켤 때 자동 보정용. 이 PC 에서 엔진과 맞은 반올림(모드 1)만, c 는 1/256 간격 + 바이트 경계 이웃(렌더 약 150번).
        // 전체(진단 명령 alphaprobe)는 모드 4개 × 74,503 표본이라 메인 스레드를 약 2.3초 잡는다.
        internal static string Run(bool quick = false)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            HitMeterShaderAlpha.RestoreAll();
            HitMeterShaderAlpha.Calibrate(-1, "running");
            if (!HitMeterShaderAlpha.Load())
            { HitMeterShaderAlpha.Calibrate(-1, "shader load failed"); return "probe: shader load failed"; }
            GameObject root = null;
            RenderTexture rt = null;
            Texture2D read = null;
            var previous = RenderTexture.active;
            var failures = new long[4];
            var first = new string[4];
            long pixels = 0;
            int negative = 0;
            try
            {
                // Regular samples plus representable neighbors of every byte half-way boundary.
                var samples = new List<Sample>();
                byte[] vertices = { 0, 1, 2, 3, 7, 15, 31, 63, 64, 95, 127, 128, 159, 191, 223, 254, 255 };
                foreach (byte v in vertices)
                {
                    int steps = quick ? 256 : 4096;
                    for (int c = 0; c <= steps; c++) samples.Add(new Sample { Vertex = v, Alpha = c / (float)steps });
                    for (int b = 0; b < v; b++)
                    {
                        float c = (b + .5f) / v;
                        int bits = BitConverter.ToInt32(BitConverter.GetBytes(c), 0);
                        for (int d = -1; d <= 1; d++) samples.Add(new Sample { Vertex = v,
                            Alpha = BitConverter.ToSingle(BitConverter.GetBytes(bits + d), 0) });
                    }
                }
                root = new GameObject("SF alpha probe") { hideFlags = HideFlags.HideAndDontSave };
                var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.cullingMask = 1 << 31;
                camera.orthographic = true; camera.orthographicSize = 32;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2;
                camera.transform.position = new Vector3(0, 0, -10000);
                camera.allowHDR = false; camera.allowMSAA = false;
                rt = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create(); camera.targetTexture = rt;
                read = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = 31; canvasObject.transform.SetParent(root.transform);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                var images = new Image[HitMeterShaderAlpha.Capacity];
                var renderers = new CanvasRenderer[images.Length];
                var allImages = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Sprite body = null;
                foreach (var image in allImages) if (image.name == "Body" && image.sprite != null
                    && image.transform.parent != null && image.transform.parent.parent != null
                    && image.transform.parent.parent.GetComponent("ADOFAI.ErrorMeterTick") != null) { body = image.sprite; break; }
                for (int i = 0; i < images.Length; i++)
                {
                    var go = new GameObject("Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = 31; go.transform.SetParent(canvasObject.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
                    rect.sizeDelta = new Vector2(4, 4); rect.anchoredPosition = new Vector2((i % 16) * 4, (i / 16) * 4);
                    images[i] = go.GetComponent<Image>(); images[i].raycastTarget = false;
                    renderers[i] = images[i].canvasRenderer;
                    var effect = go.AddComponent<HitMeterAlphaSlot>();
                    effect.Owner = new HitMeterShaderAlpha.Entry { Slot = i, Active = true };
                }
                // White and colored inputs; actual Body sprite as a second texture when available.
                for (int texture = 0; texture < (body == null ? 1 : 2); texture++)
                for (int tint = 0; tint < 2; tint++)
                for (int start = 0; start < samples.Count; start += images.Length)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var s = samples[Math.Min(start + i, samples.Count - 1)];
                        images[i].sprite = texture == 0 ? null : body;
                        images[i].material = null;
                        images[i].color = tint == 0 ? new Color(1, 1, 1, s.Vertex / 255f)
                            : new Color(.31f, .57f, .83f, s.Vertex / 255f);
                        renderers[i].SetAlpha(s.Alpha);
                        HitMeterShaderAlpha.Alphas[i] = s.Alpha;
                    }
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    var baseline = Read(rt, read);
                    for (int i = 0; i < images.Length; i++) { renderers[i].SetAlpha(1); images[i].material = HitMeterShaderAlpha.ProbeMaterial; }
                    Shader.SetGlobalFloatArray("_SFHitMeterAlpha", HitMeterShaderAlpha.Alphas);
                    for (int mode = quick ? 1 : 0; mode < (quick ? 2 : 4); mode++)
                    {
                        HitMeterShaderAlpha.ProbeMaterial.SetFloat("_SFAlphaMode", mode);
                        Canvas.ForceUpdateCanvases(); camera.Render();
                        var candidate = Read(rt, read);
                        for (int p = 0; p < baseline.Length; p++)
                        {
                            if (baseline[p].Equals(candidate[p])) continue;
                            failures[mode]++;
                            if (first[mode] == null)
                            {
                                int cell = ((p / 64) / 4) * 16 + (p % 64) / 4;
                                var sample = samples[Math.Min(start + cell, samples.Count - 1)];
                                first[mode] = "v=" + sample.Vertex + " c=" + sample.Alpha.ToString("R")
                                    + " tint=" + tint + " texture=" + texture + " pixel=" + p
                                    + " original=" + baseline[p] + " candidate=" + candidate[p];
                            }
                        }
                    }
                    pixels += baseline.Length;
                    if (start == 0)
                    {
                        // Deliberately wrong opaque input must visibly differ (prevents empty-render passes).
                        for (int i = 0; i < images.Length; i++) { images[i].material = null; images[i].color = Color.white; renderers[i].SetAlpha(1); }
                        Canvas.ForceUpdateCanvases(); camera.Render();
                        var control = Read(rt, read);
                        for (int p = 0; p < baseline.Length; p++) if (!control[p].Equals(baseline[p])) negative++;
                    }
                }
                int selected = -1;
                for (int mode = 0; mode < failures.Length; mode++)
                {
                    if (quick && mode != 1) continue;   // 시험하지 않은 모드는 0 으로 남아 있다
                    Lg.I("[alphaprobe] mode=" + mode + " mismatches=" + failures[mode] + " first=" + (first[mode] ?? "none"));
                    if (selected < 0 && failures[mode] == 0) selected = mode;
                }
                if (negative == 0) selected = -1;
                var result = (quick ? "quick " : "") + "mode=" + selected + " samples=" + samples.Count + " pixelsPerMode=" + pixels
                    + " negativePixels=" + negative + " bodySprite=" + (body != null)
                    + " device=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType
                    + " unity=" + Application.unityVersion + " colorSpace=" + QualitySettings.activeColorSpace + " ms=" + watch.ElapsedMilliseconds;
                HitMeterShaderAlpha.Calibrate(selected, result);
                Lg.I("[alphaprobe] " + result);
                return result;
            }
            catch (Exception ex)
            {
                HitMeterShaderAlpha.Calibrate(-1, ex.ToString());
                Lg.I("[alphaprobe] failed: " + ex);
                return "probe failed: " + ex.Message;
            }
            finally
            {
                RenderTexture.active = previous;
                if (root != null) { root.SetActive(false); UnityEngine.Object.DestroyImmediate(root); }
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (read != null) UnityEngine.Object.DestroyImmediate(read);
            }
        }
        private static Color32[] Read(RenderTexture rt, Texture2D read)
        {
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            read.Apply(false, false);
            return read.GetPixels32();
        }
    }
}
