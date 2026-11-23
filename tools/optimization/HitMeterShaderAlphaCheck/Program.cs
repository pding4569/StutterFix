using System;
using StutterFix;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

class Program
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static Image Image(Canvas canvas = null)
    {
        var tick = new GameObject(); tick.AddComponent<ADOFAI.ErrorMeterTick>();
        var container = new GameObject(); container.transform.parent = tick.transform;
        var go = new GameObject(); go.transform.parent = container.transform;
        go.AddComponent<CanvasRenderer>(); var image = go.AddComponent<Image>();
        image.canvas = canvas ?? new GameObject().AddComponent<Canvas>(); return image;
    }
    static HitMeterShaderAlpha.Entry Attach(Image image) { var e = HitMeterShaderAlpha.TryAttach(image); Check(e != null, "attach"); return e; }
    static void Main()
    {
        var image = Image();
        Check(!HitMeterShaderAlpha.Enabled && HitMeterShaderAlpha.TryAttach(image) == null, "default off");
        HitMeterShaderAlpha.Enabled = true;
        HitMeterShaderAlpha.Calibrate(-1, "failure"); Check(HitMeterShaderAlpha.TryAttach(image) == null, "uncalibrated rejects");
        HitMeterShaderAlpha.Calibrate(1, "simulated");
        var entry = Attach(image); var renderer = image.canvasRenderer;
        var vh = new VertexHelper(); entry.Effect.ModifyMesh(vh);
        Check(vh.Vertices[3].uv1.x == entry.Slot, "mesh slot");
        int writes = renderer.Writes;
        HitMeterShaderAlpha.Write(entry, renderer, .37125f);
        Check(renderer.Writes == writes && renderer.GetAlpha() == 1 && HitMeterShaderAlpha.Alphas[entry.Slot] == .37125f, "array only");
        Canvas.Raise(); Check(Shader.Snapshot[entry.Slot] == .37125f, "upload");
        long uploads = HitMeterShaderAlpha.Uploads; Canvas.Raise(); Check(HitMeterShaderAlpha.Uploads == uploads, "once per frame");
        Time.frameCount++; Canvas.Raise(); Check(HitMeterShaderAlpha.Uploads == uploads + 1, "next frame");
        HitMeterShaderAlpha.Write(entry, renderer, .3f); Canvas.Raise();
        Check(Shader.Snapshot[entry.Slot] == .3f, "forced early draw cannot leave later alpha stale");
        for (int i = 0; i < 100; i++) { Time.frameCount++; Canvas.Raise(); HitMeterShaderAlpha.Write(entry, renderer, .25f); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) { HitMeterShaderAlpha.Write(entry, renderer, .25f); Time.frameCount++; Canvas.Raise(); }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "steady state allocated");
        HitMeterShaderAlpha.Enabled = false;
        Check(!entry.Active && image.Raw == null && renderer.GetAlpha() == .25f, "off restores null material and latest alpha");
        HitMeterShaderAlpha.Write(entry, renderer, .125f); Check(renderer.GetAlpha() == .125f, "existing tween after off");
        Check(image.canvas.additionalShaderChannels == 0, "channel restored");
        HitMeterShaderAlpha.Enabled = true;
        image.canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.TexCoord1;
        entry = Attach(image); HitMeterShaderAlpha.Write(entry, renderer, .41f);
        image.SetVerticesDirty(); Check(!entry.Active && image.Raw == null && renderer.GetAlpha() == .41f, "external color mesh dirty fallback");
        Check(image.canvas.additionalShaderChannels == (AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.TexCoord1), "preexisting channels preserved");
        var oldEffect = entry.Effect;
        entry = Attach(image); Check(ReferenceEquals(oldEffect, entry.Effect), "same-frame kill restart reuses modifier");
        var custom = new Material(new Shader()); image.material = custom;
        Check(!entry.Active && image.material == custom, "external material retained");
        Check(HitMeterShaderAlpha.TryAttach(image) == null, "custom material rejected"); image.material = null;
        image.gameObject.AddComponent<Mask>(); Check(HitMeterShaderAlpha.TryAttach(image) == null, "mask rejected");
        image = Image(); image.gameObject.AddComponent<CanvasGroup>(); Check(HitMeterShaderAlpha.TryAttach(image) == null, "group rejected");
        image = Image(); image.gameObject.AddComponent<RectMask2D>(); Check(HitMeterShaderAlpha.TryAttach(image) == null, "rect mask rejected");
        image = Image(); entry = Attach(image); renderer = image.canvasRenderer;
        HitMeterShaderAlpha.Write(entry, renderer, 1.1f);
        Check(!entry.Active && renderer.GetAlpha() == 1.1f, "out of calibration domain fallback");
        entry = Attach(image); HitMeterShaderAlpha.Write(entry, renderer, float.NaN);
        Check(!entry.Active && float.IsNaN(renderer.GetAlpha()), "NaN fallback");
        entry = Attach(image);
        HitMeterShaderAlpha.Write(entry, renderer, .19f); renderer.Inherited = .5f; Time.frameCount++; Canvas.Raise();
        Check(!entry.Active && renderer.GetAlpha() == .19f, "inherited alpha fallback");
        image = Image(); entry = Attach(image); HitMeterShaderAlpha.Write(entry, image.canvasRenderer, .18f); SceneManager.Change();
        Check(!entry.Active && image.Raw == null && image.canvasRenderer.GetAlpha() == .18f, "scene change");
        entry = Attach(image); SceneManager.Unload(); Check(!entry.Active, "scene unload");
        entry = Attach(image); HitMeterShaderAlpha.Write(entry, image.canvasRenderer, .17f);
        Shader.ThrowUpload = true; Time.frameCount++; Canvas.Raise(); Shader.ThrowUpload = false;
        Check(!entry.Active && image.Raw == null && image.canvasRenderer.GetAlpha() == .17f, "upload exception restoration");
        Check(HitMeterShaderAlpha.TryAttach(image) == null, "failure activation blocked");
        HitMeterShaderAlpha.Enabled = false; HitMeterShaderAlpha.Enabled = true; entry = Attach(image);
        HitMeterShaderAlpha.Shutdown(); Check(!entry.Active && image.Raw == null, "shutdown");
        uploads = HitMeterShaderAlpha.Uploads; Time.frameCount++; Canvas.Raise(); Check(uploads == HitMeterShaderAlpha.Uploads, "shutdown unhooks");
        AssetBundle.ThrowLoad = true; HitMeterShaderAlpha.Enabled = true;
        Check(HitMeterShaderAlpha.TryAttach(image) == null, "missing bundle safe fallback"); AssetBundle.ThrowLoad = false;
        HitMeterShaderAlpha.Shutdown();
        Console.WriteLine("PASS checks=" + checks + " steadyWrites=100000 managedBytes=" + bytes);
        Console.WriteLine("Production owner source + storage/event adapters. Native Unity, shader math, pixels, batching, Mono and FPS NOT tested.");
    }
}
