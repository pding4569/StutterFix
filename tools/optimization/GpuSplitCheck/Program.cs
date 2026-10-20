using System;
using StutterFix;

class RendererStub { internal bool Off, Destroyed, Enabled = true, Active = true; }
class Program
{
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("check " + checks); }
    static void Main()
    {
        // Exhaust the decision space: exclusions and ownership must partition all scene renderers.
        for (int bits = 0; bits < 512; bits++)
        {
            bool B(int n) => (bits & (1 << n)) != 0;
            var got = GpuSplitPolicy.Classify(B(0), B(1), B(2), B(3), B(4), B(5), B(6), B(7), B(8));
            var expected = !B(0) || !B(1) || B(2) ? GpuSplitGroup.None :
                B(3) && B(6) ? GpuSplitGroup.Particle : B(3) && B(5) ? GpuSplitGroup.Text :
                B(3) && B(4) ? GpuSplitGroup.Image : B(7) ? GpuSplitGroup.Tiles : B(8) ? GpuSplitGroup.Planets : GpuSplitGroup.Other;
            Check(got == expected);
            Check(got == 0 || ((int)got & ((int)got - 1)) == 0);
        }
        Check(GpuSplitPolicy.Parse("deco-text+deco-particle") == (GpuSplitGroup.Text | GpuSplitGroup.Particle));
        Check(GpuSplitPolicy.Parse("all") == GpuSplitGroup.All);
        Check(GpuSplitPolicy.Parse("none") == GpuSplitGroup.None);
        foreach (var bad in new[] { "", "bad", "tiles+", "none+tiles", "all+other" })
        { bool thrown = false; try { GpuSplitPolicy.Parse(bad); } catch (ArgumentException) { thrown = true; } Check(thrown); }
        var filtered = GpuSplitPolicy.ParseSelection("deco-image:onscreen,big=1.5,alpha,transparent");
        Check(filtered.Groups == GpuSplitGroup.Image && filtered.Percent == 1.5);
        Check(filtered.Matches(true, 2, "alpha", true));
        Check(!filtered.Matches(false, 2, "alpha", true));
        Check(!filtered.Matches(true, 1.5, "alpha", true));
        Check(!filtered.Matches(true, 2, "additive", true));
        Check(!filtered.Matches(true, 2, "alpha", false));
        Check(GpuSplitPolicy.ParseSelection("deco-image:small").Matches(true, 1, "unknown", false));
        Check(GpuSplitPolicy.ParseSelection("deco-image:big=100").Matches(true, double.PositiveInfinity, "unknown", false));
        Check(GpuSplitPolicy.ParseSelection("deco-image:offscreen,additive").Matches(false, 0, "additive", false));
        foreach (var bad in new[] { "deco-image:", "tiles:big", "deco-image:big=NaN", "deco-image:big=Infinity", "deco-image:big=-1", "deco-image:big=", "deco-image:onscreen,offscreen", "deco-image:big,small", "deco-image:alpha,additive", "deco-image:alpha=1", "deco-image:big=1,big=2", "deco-image:alpha,alpha", "deco-image:alpha+tiles" })
        { bool thrown = false; try { GpuSplitPolicy.ParseSelection(bad); } catch (ArgumentException) { thrown = true; } Check(thrown); }
        foreach (var filter in new[] { "live", "live:all", "live:visible", "live:invisible", "live:tiny", "live:invisible+tiny" })
        {
            var sel = GpuSplitPolicy.ParseSelection(filter);
            Check(sel.Live && sel.Groups == GpuSplitGroup.Image && sel.Percent == 0.01 && sel.AlphaThreshold == 0);
            for (int bits = 0; bits < 16; bits++)
                foreach (double alpha in new[] { -1d, 0d, double.Epsilon, 1d, double.NaN })
                    foreach (double area in new[] { 0d, 0.009d, 0.01d, 1d, double.PositiveInfinity, double.NaN })
                    {
                        bool enabled = (bits & 1) != 0, active = (bits & 2) != 0, forced = (bits & 4) != 0, onscreen = (bits & 8) != 0;
                        bool inv = !onscreen || alpha <= 0, tiny = area < 0.01;
                        bool expected = enabled && active && !forced && (sel.LiveFilter == "all" ||
                            (sel.LiveFilter == "visible" ? onscreen && alpha > 0 : sel.LiveFilter == "invisible" ? inv : sel.LiveFilter == "tiny" ? tiny : inv || tiny));
                        Check(sel.MatchesLive(enabled && active, forced, onscreen, alpha, area) == expected);
                    }
        }
        var fade = GpuSplitPolicy.ParseSelection("live:invisible");
        Check(fade.MatchesLive(true, false, true, 0, 1));
        Check(!fade.MatchesLive(true, false, true, 1, 1)); // next frame fade-in must leave membership
        Check(fade.MatchesLive(true, false, false, 1, 1)); // moving off screen enters immediately
        foreach (var bad in new[] { "live:", "live:visible+tiny", "live:tiny=1", "live:invisible,alpha", "live:invisible+tiny+visible" })
        { bool thrown = false; try { GpuSplitPolicy.ParseSelection(bad); } catch (ArgumentException) { thrown = true; } Check(thrown); }
        var rgba = new uint[] { 0, 0x01020304, 0xffffffff };
        GpuSplitPolicy.PixelDiff(3, i => rgba[i], i => rgba[i], out int max, out int pixels);
        Check(max == 0 && pixels == 0);
        var changed = new uint[] { 0xff000000, 0x01020305, 0xffffffff };
        GpuSplitPolicy.PixelDiff(3, i => rgba[i], i => changed[i], out max, out pixels);
        Check(max == 255 && pixels == 2); // alpha-only difference counts as a different pixel
        var projection = new GpuSplitPolicy.ProjectionArea();
        projection.Include(-1, -1, 10, 0.1); projection.Include(2, 2, 10, 0.1);
        Check(projection.Percent == 900); // orthographic-style coordinates, deliberately unclipped
        projection.Include(0, 0, 0, 0.1); Check(double.IsPositiveInfinity(projection.Percent));
        projection = new GpuSplitPolicy.ProjectionArea();
        projection.Include(0, 0, -1, 0.1); Check(projection.Percent == 0);
        projection.Include(0, 0, 1, 0.1); Check(double.IsPositiveInfinity(projection.Percent));
        projection = new GpuSplitPolicy.ProjectionArea();
        projection.Include(double.NaN, 0, 1, 0.1); Check(double.IsPositiveInfinity(projection.Percent));
        // Dynamic ownership releases a newly-visible object while retaining externally forced objects.
        var dynamicA = new RendererStub(); var external = new RendererStub { Off = true };
        var dynamicLedger = new GpuSplitLedger<RendererStub>(x => !x.Destroyed, x => x.Off, (x, v) => x.Off = v);
        if (fade.MatchesLive(true, dynamicA.Off, true, 0, 1)) dynamicLedger.Hide(dynamicA);
        Check(dynamicA.Off && external.Off);
        dynamicLedger.Restore();
        if (fade.MatchesLive(true, dynamicA.Off, true, 1, 1)) dynamicLedger.Hide(dynamicA);
        Check(!dynamicA.Off && external.Off && dynamicLedger.Count == 0);
        var a = new RendererStub(); var b = new RendererStub { Off = true, Enabled = false, Active = false };
        var c = new RendererStub();
        var ledger = new GpuSplitLedger<RendererStub>(x => !x.Destroyed, x => x.Off, (x, v) => x.Off = v);
        ledger.Hide(a); ledger.Hide(b); ledger.Hide(a);
        Check(ledger.Count == 2 && a.Off && b.Off && !b.Enabled && !b.Active);
        a.Off = false; Check(ledger.Enforce() == 1 && a.Off);
        ledger.Hide(a); // refresh must retain original false, not overwrite with hidden true
        ledger.Enforce();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) ledger.Enforce();
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated);
        ledger.Hide(c); c.Destroyed = true;
        Check(ledger.Count == 3);
        ledger.Restore(); Check(!a.Off && b.Off && !b.Enabled && !b.Active && ledger.Count == 0);
        ledger.Restore(); Check(!a.Off && b.Off);
        a.Off = true; ledger.Hide(a); ledger.Restore(); Check(a.Off);
        ledger.Hide(b); b.Destroyed = true; ledger.Restore(); Check(ledger.Count == 0);
        Console.WriteLine($"GpuSplit policy/restore checks={checks}, errors=0; Unity runtime not exercised");
    }
}
