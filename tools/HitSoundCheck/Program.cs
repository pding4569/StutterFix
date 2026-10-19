using StutterFix;

int cases = 0;
long compared = 0;
Func<bool> running = () => false;
void Check(bool pass, string why) { cases++; if (!pass) throw new Exception(why); }

Check(HitSoundMix.Sample(0.5 / 48000, 48000) == 1, "positive half sample");
Check(HitSoundMix.Sample(-0.5 / 48000, 48000) == -1, "negative half sample");
Check(HitSoundMix.Sample(0, 44100) == 0, "zero time");
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
