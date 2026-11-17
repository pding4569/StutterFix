using StutterFix;

// Compare overlap-added clips against an independently mixed full timeline.
// This does not model Unity/FMOD resampling or prove that a recorded click is gone.
{
    long overlapSamples = 0;
    double maxOverlapError = 0, maxDspErrorSamples = 0;
    foreach (int rate in new[] { 8000, 44100, 48000, 96000 })
    foreach (int channels in new[] { 1, 2 })
    {
        int step = rate / 2, segments = 4, lead = HitSoundMix.LeadFrames;
        var voices = new List<HitSoundMix.Voice>();
        foreach (int at in new[] { 0, step - lead - 1, step - 1, step, step + 1, 2 * step + 95 })
        {
            var pcm = new float[(step + 173) * channels];
            for (int j = 0; j < pcm.Length; j++) pcm[j] = (float)(Math.Sin(j * .11) * .3 + .1);
            voices.Add(new HitSoundMix.Voice { Pcm = pcm, Start = at + lead, Gain = .7f });
        }
        // An impulse precisely on a handoff, with loud overlap and a nonzero final sample.
        voices.Add(new HitSoundMix.Voice { Pcm = Enumerable.Repeat(1f, channels).ToArray(), Start = step, Gain = 2 });
        voices.Add(new HitSoundMix.Voice { Pcm = Enumerable.Repeat(.25f, channels).ToArray(), Start = segments * step - 1, Gain = 1 });
        int length = (segments * step + HitSoundMix.PadFrames) * channels;
        var expected = new float[length];
        var combined = new float[length];
        foreach (var v in voices)
            for (int j = 0; j < v.Pcm.Length; j++)
            {
                long p = v.Start * channels + j;
                if (p < length) expected[p] += v.Pcm[j] * v.Gain;
            }
        var clip = new float[(step + HitSoundMix.PadFrames) * channels];
        for (int i = 0; i < segments; i++)
        {
            Array.Fill(clip, 99f); // Reused buffers must clear all padding.
            HitSoundMix.RenderSegment(voices.ToArray(), channels, i, segments, step, clip, () => false);
            if (clip.Take(lead * channels).Any(x => x != 0) ||
                clip.Skip(clip.Length - HitSoundMix.TailFrames * channels).Any(x => x != 0))
                throw new Exception("nonzero clip guard");
            for (int j = 0; j < clip.Length; j++) combined[i * step * channels + j] += clip[j];
        }
        for (int j = 0; j < length; j++)
        {
            double error = Math.Abs((double)combined[j] - expected[j]);
            maxOverlapError = Math.Max(maxOverlapError, error);
            overlapSamples++;
            if (error > 5e-7 * Math.Max(1, Math.Abs(expected[j]))) throw new Exception("overlap timeline changed");
        }
        const double oldOrigin = 100.123;
        double shiftedOrigin = oldOrigin - (double)lead / rate;
        for (int at = 0; at < segments * step; at += 137)
        {
            double delta = Math.Abs(shiftedOrigin + (double)(at + lead) / rate - (oldOrigin + (double)at / rate)) * rate;
            maxDspErrorSamples = Math.Max(maxDspErrorSamples, delta);
            if (delta > 1e-6) throw new Exception("absolute DSP time shifted");
        }
        HitSoundMix.RenderSegment(voices.ToArray(), channels, 1, segments, step, clip, () => true);
        if (clip.Any(x => x != 0)) throw new Exception("cancelled segment retains samples");
    }
    var array = new[] { new HitSoundMix.Voice { Pcm = new float[10000], Start = 32, Gain = 1 } };
    var buffer = new float[4000 + HitSoundMix.PadFrames];
    Func<bool> never = () => false;
    for (int i = 0; i < 1000; i++) HitSoundMix.RenderSegment(array, 1, i % 3, 3, 4000, buffer, never);
    long beforeSegment = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1000; i++) HitSoundMix.RenderSegment(array, 1, i % 3, 3, 4000, buffer, never);
    long segmentAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeSegment;
    if (segmentAllocated != 0) throw new Exception("segment render allocation bytes=" + segmentAllocated);
    Console.WriteLine($"PASS segment_cases=8 overlap_samples={overlapSamples} max_overlap_error={maxOverlapError:R} max_DSP_error_samples={maxDspErrorSamples:R} guard_nonzero=0 render_allocated_bytes={segmentAllocated}");
}

int cases = 0;
long compared = 0;
Func<bool> running = () => false;
void Check(bool pass, string why) { cases++; if (!pass) throw new Exception(why); }

Check(HitSoundMix.Sample(0.5 / 48000, 48000) == 1, "positive half sample");
Check(HitSoundMix.Sample(-0.5 / 48000, 48000) == -1, "negative half sample");
Check(HitSoundMix.Sample(0, 44100) == 0, "zero time");
foreach (var format in new[] { (44100, 48000, 1), (44100, 44100, 3), (44101, 44101, 1), (4000, 4000, 1), (192000, 192000, 2) })
{
    bool rejected = false;
    try { HitSoundMix.RequireFormat(format.Item1, format.Item2, format.Item3); }
    catch (NotSupportedException) { rejected = true; }
    Check(rejected, "unsupported format must fall back");
}
var rng = new Random(20261019);
foreach (int channels in new[] { 1, 2 })
foreach (int block in new[] { 7, 32, 24000 })
{
    var voices = new List<HitSoundMix.Voice>();
    for (int i = 0; i < 41; i++)
    {
        int frames = rng.Next(1, block * 3 + 1);
        var pcm = new float[frames * channels];
        for (int j = 0; j < pcm.Length; j++) pcm[j] = (float)(rng.NextDouble() * 2 - 1);
        voices.Add(new HitSoundMix.Voice { Pcm = pcm, Start = rng.Next(-block, block * 5), Gain = (float)rng.NextDouble() });
    }
    // Explicit impulse on a boundary, overlapping loud voices and tails > one segment.
    voices.Add(new HitSoundMix.Voice { Pcm = Enumerable.Repeat(1f, block * 3 * channels).ToArray(), Start = block, Gain = 1 });
    voices.Add(new HitSoundMix.Voice { Pcm = Enumerable.Repeat(1f, block * 3 * channels).ToArray(), Start = block, Gain = 1 });
    var array = voices.ToArray();
    var full = new float[block * 9 * channels];
    // Whole-timeline oracle: deliberately no interval clipping/boundary calculations.
    foreach (var voice in array)
        for (int frame = 0; frame < voice.Pcm.Length / channels; frame++)
            for (int c = 0; c < channels; c++)
            {
                long sample = (voice.Start + frame) * channels + c;
                if (sample >= 0 && sample < full.Length) full[sample] += voice.Pcm[frame * channels + c] * voice.Gain;
            }
    var output = new float[block * channels];
    var reference = new float[output.Length];
    for (int segment = 0; segment < 9; segment++)
    {
        HitSoundMix.Render(array, channels, (long)segment * block, output, running);
        for (int j = 0; j < output.Length; j++)
        {
            compared++;
            Check(output[j] == full[segment * output.Length + j], "timeline mismatch");
        }
        double error = HitSoundMix.Verify(array, channels, (long)segment * block, output, reference, running, out long diff);
        Check(error == 0 && diff == 0, "independent verifier mismatch");
    }
    HitSoundMix.Render(array, channels, block, output, running);
    Check(output.Any(x => x > 1), "mix must not clip or normalize");
    output[0] += 0.25f;
    Check(HitSoundMix.Verify(array, channels, block, output, reference, running, out long bad) > 0 && bad == 1, "verifier must catch corruption");
    HitSoundMix.Render(array, channels, 0, output, () => true);
    Check(output.All(x => x == 0), "cancelled buffer must not retain previous data");
}
var tiny = new[] { new HitSoundMix.Voice { Pcm = new[] { 1f, -1f }, Gain = 1, Start = 0 } };
var reuse = new float[16];
HitSoundMix.Render(tiny, 1, 0, reuse, running);
long before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 1000; i++) HitSoundMix.Render(tiny, 1, 0, reuse, running);
long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Check(allocated == 0, "render must allocate no memory with reused buffers");
Console.WriteLine($"PASS assertions={cases} compared_samples={compared} max_error=0 differing_samples=0 render_allocated_bytes={allocated}");

// Interleaved original list; retain stable order within each native channel format.
// The oracle writes the entire timeline directly, independently of segment clipping.
foreach (int rate in new[] { 8000, 44100, 48000, 96000 })
{
    int frames = rate / 2;
    var groups = new[] { new List<HitSoundMix.Voice>(), new List<HitSoundMix.Voice>() };
    var originals = new List<(int channels, HitSoundMix.Voice voice)>();
    for (int i = 0; i < 20; i++)
    {
        int channels = i % 2 + 1;
        HitSoundMix.RequireFormat(rate, rate, channels);
        // Each group starts at offset zero; include overlaps and long boundary tails.
        double time = i < 2 ? 0 : (i / 2 % 3) * 0.5 + 0.25;
        var pcm = new float[(frames + 17) * channels];
        for (int p = 0; p < pcm.Length; p++) pcm[p] = (p % 29 - 14) / 32f;
        var voice = new HitSoundMix.Voice { Pcm = pcm, Gain = (i % 4 + 1) / 4f,
            Start = HitSoundMix.Sample(time, rate) };
        groups[channels - 1].Add(voice);
        originals.Add((channels, voice));
    }
    var timeline = new[] { new float[frames * 5], new float[frames * 5 * 2] };
    foreach (var (channels, voice) in originals)
        for (int f = 0; f < voice.Pcm.Length / channels; f++)
            for (int c = 0; c < channels; c++)
                timeline[channels - 1][(voice.Start + f) * channels + c] += voice.Pcm[f * channels + c] * voice.Gain;
    for (int group = 0; group < groups.Length; group++)
    {
        Check(groups[group].Count == 10 && groups[group][0].Start == 0, "mixed group count/zero offset");
        int channels = group + 1;
        var voices = groups[group].ToArray();
        var output = new float[frames * channels];
        var expected = new float[output.Length];
        for (int segment = 0; segment < 5; segment++)
        {
            long start = (long)segment * frames;
            double monoDsp = 100 + (double)start / rate;
            double stereoDsp = 100 + (double)segment * frames / rate;
            Check(monoDsp == stereoDsp && HitSoundMix.Sample(monoDsp - 100, rate) == start,
                "group DSP/sample boundary alignment");
            HitSoundMix.Render(voices, channels, start, output, running);
            for (int j = 0; j < output.Length; j++)
            {
                compared++;
                Check(output[j] == timeline[group][segment * output.Length + j], "mixed full-timeline mismatch");
            }
            Check(HitSoundMix.Verify(voices, channels, start, output, expected, running, out var diff) == 0 && diff == 0,
                "mixed independent verifier");
        }
    }
    long poolBytes = 16L * frames * 3 * 4 * 2;
    Check(poolBytes <= 64L * 1024 * 1024 && frames * 3L * 4 <= 768000, "mixed pool/upload cap");
}

// HELLO-sized synthetic list, reused PCM and buffers. This is .NET offline CPU
// evidence only: Unity SetData, scheduling, native memory and FMOD are not run.
{
    const int rate = 44100, frames = rate / 2, count = 14262;
    var groups = new[] { new List<HitSoundMix.Voice>(), new List<HitSoundMix.Voice>() };
    var mono = Enumerable.Repeat(0.125f, 9499).ToArray();
    var stereo = Enumerable.Repeat(-0.0625f, 9499 * 2).ToArray();
    long end = 0;
    for (int i = 0; i < count; i++)
    {
        var voice = new HitSoundMix.Voice { Pcm = i % 2 == 0 ? mono : stereo,
            Start = HitSoundMix.Sample(i * 0.02, rate), Gain = 0.5f };
        groups[i % 2].Add(voice);
        end = Math.Max(end, voice.Start + 9499);
    }
    int segments = (int)((end + frames - 1) / frames);
    long differences = 0;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    long renderTicks = 0, maxRenderTicks = 0, renderAllocated = 0;
    foreach (int group in new[] { 0, 1 })
    {
        int channels = group + 1;
        var voices = groups[group].ToArray();
        var output = new float[frames * channels];
        var expected = new float[output.Length];
        for (int segment = 0; segment < segments; segment++)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            HitSoundMix.Render(voices, channels, (long)segment * frames, output, running);
            renderAllocated += GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            renderTicks += ticks; maxRenderTicks = Math.Max(maxRenderTicks, ticks);
            Check(HitSoundMix.Verify(voices, channels, (long)segment * frames, output, expected, running, out var diff) == 0,
                "14262-event render mismatch");
            differences += diff;
        }
    }
    watch.Stop();
    Check(differences == 0, "14262-event differing samples");
    Check(renderAllocated == 0, "14262-event render allocation");
    Console.WriteLine($"PASS mixed_events=20 groups=2 rates=8000,44100,48000,96000 compared_samples={compared} group_alignment_samples=0 differing_samples=0");
    Console.WriteLine($"PASS stress_events={count} groups=2 group_segments={segments * 2} differing_samples={differences} total_ms={watch.Elapsed.TotalMilliseconds:F2} render_ms={renderTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F2} max_render_block_ms={maxRenderTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3} render_allocated_bytes={renderAllocated} PCM_plus_pool_bytes={((long)mono.Length + stereo.Length) * 4 + 16L * frames * 3 * 4 * 2}");
}
