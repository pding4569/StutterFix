using StutterFix;

static class AtlasPlanFixture
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
        const long MiB = 1024L * 1024;
        LiveDecoAtlasPolicy.Source S(int w, int h, int group = 0) => new(w, h, group);
        void Validate(LiveDecoAtlasPolicy.Source[] sources, LiveDecoAtlasPolicy.Layout plan, long budget)
        {
            Check(plan.Slots.Length == sources.Length, "every input reserved");
            Check(plan.Bytes == plan.Pages.Sum(p => (long)p.Size * p.Size * 4) && plan.Bytes <= budget, "total bytes, all sampler pages included");
            // Check arbitrary (reverse) admission and late additions against ALL
            // reservations, including sources not admitted yet.
            var rects = new List<(int page, int x, int y, int w, int h)>();
            foreach (int i in Enumerable.Range(0, sources.Length).Reverse())
            {
                var s = sources[i]; var slot = plan.Slots[i]; var p = plan.Pages[slot.Page];
                int w = LiveDecoAtlasPolicy.Align(s.Width + 4), h = LiveDecoAtlasPolicy.Align(s.Height + 4);
                int x = slot.X - 2, y = slot.Y - 2;
                Check(plan.Groups[slot.Page] == s.Group, "samplers never mixed");
                Check(x % 4 == 0 && y % 4 == 0 && x >= 0 && y >= 0 && x + w <= p.Size && y + h <= p.Size, "padding/alignment bounds");
                foreach (var r in rects) Check(r.page != slot.Page || x + w <= r.x || r.x + r.w <= x || y + h <= r.y || r.y + r.h <= y, "no overlap in reverse admission");
                rects.Add((slot.Page, x, y, w, h));
            }
            for (int page = 0; page < plan.Pages.Count; page++)
                for (int n = 0; n < 10; n++)
                    if (plan.Pages[page].Place(1, 1, out int x, out int y))
                    {
                        x -= 2; y -= 2;
                        foreach (var r in rects) Check(r.page != page || x + 8 <= r.x || r.x + r.w <= x || y + 8 <= r.y || r.y + r.h <= y, "late source cannot overwrite pending reservation");
                        rects.Add((page, x, y, 8, 8));
                    }
        }
        var empty = LiveDecoAtlasPolicy.Plan(Array.Empty<LiveDecoAtlasPolicy.Source>(), 16384);
        Check(empty.Pages.Count == 0 && empty.Bytes == 0, "empty allocates zero");
        var tie = LiveDecoAtlasPolicy.Plan(new[] { S(2044, 2044, 0), S(2044, 2044, 1) }, 16384);
        Check(tie.Pages.Count == 2 && tie.Bytes == 32 * MiB, "same page count chooses smallest bytes");
        // Old global one-page sizing picks 16384 => 1GiB; two 8192
        // pages fit exactly 512MiB without resizing a source or a texture page.
        var wide = Enumerable.Repeat(S(2044, 2044), 17).ToArray();
        var widePlan = LiveDecoAtlasPolicy.Plan(wide, 16384);
        Check(widePlan.Pages.Count == 2 && widePlan.Bytes == 512 * MiB && widePlan.Pages.All(p => p.Size == 8192), "single-page budget regression");
        Validate(wide, widePlan, 512 * MiB);
        // Global sizing gives 8192 to all three sampler groups => 768MiB.
        // Joint selection fits five pages in 512MiB (one group on one page,
        // two groups on two pages each); group order does not override budget.
        var mixed = Enumerable.Range(0, 3).SelectMany(g => Enumerable.Repeat(S(2044, 2044, g), 5)).ToArray();
        var mixedPlan = LiveDecoAtlasPolicy.Plan(mixed, 16384);
        Check(mixedPlan.Pages.Count == 5 && mixedPlan.Bytes == 512 * MiB, "sampler multiplication regression");
        Validate(mixed, mixedPlan, 512 * MiB);
        var small = new[] { S(31, 31), S(31, 7), S(7, 31), S(7, 7), S(1, 1, 1) };
        var smallPlan = LiveDecoAtlasPolicy.Plan(small, 128, 2 * 128 * 128 * 4);
        Check(smallPlan.Pages.Count == 2 && smallPlan.Bytes == 2 * 128 * 128 * 4, "separate samplers respect device max");
        Validate(small, smallPlan, 2 * 128 * 128 * 4);
        bool rejected = false;
        try { LiveDecoAtlasPolicy.Plan(Enumerable.Repeat(S(2044, 2044), 33).ToArray(), 16384); }
        catch (InvalidOperationException e) { rejected = e.Message == "atlas-vram-budget-512MiB"; }
        Check(rejected, "genuine padded content over budget stays failure");
        var rng = new Random(5192026);
        for (int trial = 0; trial < 80; trial++)
        {
            var sources = Enumerable.Range(0, rng.Next(1, 50)).Select(_ => S(rng.Next(1, 28), rng.Next(1, 28), rng.Next(3))).ToArray();
            var a = LiveDecoAtlasPolicy.Plan(sources, 128, 32L * 128 * 128 * 4);
            var b = LiveDecoAtlasPolicy.Plan(sources, 128, 32L * 128 * 128 * 4);
            Check(a.Bytes == b.Bytes && a.Slots.Select(s => (s.Page, s.X, s.Y)).SequenceEqual(b.Slots.Select(s => (s.Page, s.X, s.Y))), "deterministic coordinates");
            Validate(sources, a, 32L * 128 * 128 * 4);
        }
        Console.WriteLine($"LiveDeco atlas plan: {checks} checks passed; budget/sampler regressions, reservations, transitions; GPU untested");
        return checks;
    }
}
