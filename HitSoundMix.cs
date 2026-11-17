using System;
using System.Threading;

namespace StutterFix
{
    // Pure PCM arithmetic. No Unity objects or other-mod implementation is used here.
    internal static class HitSoundMix
    {
        internal const int LeadFrames = 32, CrossfadeFrames = 64, TailFrames = 32;
        internal const int PadFrames = LeadFrames + CrossfadeFrames + TailFrames;

        internal sealed class Voice
        {
            internal float[] Pcm;
            internal long Start;
            internal float Gain;
        }

        internal static long Sample(double seconds, int rate)
            => checked((long)Math.Round(seconds * rate, MidpointRounding.AwayFromZero));

        // At most two native layouts at one common, even sample rate. Keeping the
        // rate common makes every half-second boundary identical in both groups.
        internal static void RequireFormat(int commonRate, int rate, int channels)
        {
            if (rate < 8000 || rate > 96000 || (rate & 1) != 0 || rate != commonRate || channels < 1 || channels > 2)
                throw new NotSupportedException("unsupported native PCM format or mixed sample rates");
        }

        internal static void Render(Voice[] voices, int channels, long start, float[] output,
            Func<bool> cancelled)
        {
            Array.Clear(output, 0, output.Length);
            long end = start + output.Length / channels;
            foreach (var v in voices)
            {
                if (cancelled()) return;
                long a = Math.Max(start, v.Start), b = Math.Min(end, v.Start + v.Pcm.Length / channels);
                if (a >= b) continue;
                int src = checked((int)((a - v.Start) * channels));
                int dst = checked((int)((a - start) * channels));
                int count = checked((int)((b - a) * channels));
                for (int i = 0; i < count; i++)
                {
                    if ((i & 4095) == 0 && cancelled()) return;
                    output[dst + i] += v.Pcm[src + i] * v.Gain;
                }
            }
        }

        // Complementary overlap windows preserve the summed PCM within float rounding.
        // Capture shifts the origin so the first segment also has a silent lead.
        // Silent guards avoid nonzero clip edges; actual resampler output needs a recording.
        internal static void RenderSegment(Voice[] voices, int channels, int index, int segments,
            int stepFrames, float[] output, Func<bool> cancelled)
        {
            Array.Clear(output, 0, output.Length);
            if (segments <= 0 || stepFrames <= 0 || index < 0 || index >= segments) return;
            int activeFrames = index + 1 < segments ? stepFrames + LeadFrames + CrossfadeFrames : stepFrames;
            long start = (long)index * stepFrames;
            long end = start + activeFrames;
            foreach (var v in voices)
            {
                if (cancelled()) return;
                long a = Math.Max(start, v.Start), b = Math.Min(end, v.Start + v.Pcm.Length / channels);
                if (a >= b) continue;
                int src = checked((int)((a - v.Start) * channels));
                int dst = checked((int)((a - start) * channels));
                int count = checked((int)((b - a) * channels));
                for (int i = 0; i < count; i++)
                {
                    if ((i & 4095) == 0 && cancelled()) return;
                    output[dst + i] += v.Pcm[src + i] * v.Gain;
                }
            }
            if (index > 0)
            {
                Array.Clear(output, 0, LeadFrames * channels);
                for (int f = 0; f < CrossfadeFrames; f++)
                {
                    float wIn = FadeInWeight(f);
                    int baseIdx = (LeadFrames + f) * channels;
                    for (int c = 0; c < channels; c++) output[baseIdx + c] *= wIn;
                }
            }
            if (index + 1 < segments)
            {
                for (int f = 0; f < CrossfadeFrames; f++)
                {
                    float wOut = 1f - FadeInWeight(f);
                    int baseIdx = (stepFrames + LeadFrames + f) * channels;
                    for (int c = 0; c < channels; c++) output[baseIdx + c] *= wOut;
                }
            }
        }

        internal static float FadeInWeight(int frame)
            => (float)(0.5 - 0.5 * Math.Cos(Math.PI * (frame + 0.5) / CrossfadeFrames));

        // Independent voice/channel/frame traversal for explicit diagnostics and offline fixtures.
        internal static double Verify(Voice[] voices, int channels, long start, float[] actual,
            float[] expected, Func<bool> cancelled, out long differences)
        {
            Array.Clear(expected, 0, expected.Length);
            foreach (var v in voices)
            {
                if (cancelled()) break;
                int frames = v.Pcm.Length / channels;
                int first = (int)Math.Max(0, start - v.Start);
                int last = (int)Math.Min(frames, start + actual.Length / channels - v.Start);
                for (int c = 0; c < channels; c++)
                    for (int f = first; f < last; f++)
                    {
                        if ((f & 4095) == 0 && cancelled()) break;
                        expected[(int)(v.Start + f - start) * channels + c] += v.Pcm[f * channels + c] * v.Gain;
                    }
            }
            double max = 0;
            differences = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                double delta = Math.Abs((double)expected[i] - actual[i]);
                if (delta != 0) differences++;
                if (delta > max) max = delta;
            }
            return max;
        }
    }
}
