#if DEV || AUTOTEST || GPUDECO || UNITY_EDITOR || GPUDECO_FIXTURE
using System;
using System.IO;

namespace StutterFix
{
    // Shared by the exporter and offline lab. Sparse scenes remain auxiliary
    // measurements; coverage is a gate only when enough decorations are awake.
#if DEV || AUTOTEST || GPUDECO_FIXTURE || DRAWCOLLAPSE_CHECK
    internal static class GpuDecoCoverage
    {
        internal static bool Auxiliary(int awake) => awake < 800;
        internal static bool OwnedLow(int awake, int owned) => !Auxiliary(awake) && (long)owned * 10 < (long)awake * 7;
    }

    internal sealed class GpuDecoPixelGate
    {
        readonly bool[] seen=new bool[3];
        internal string Error;
        internal void Diagnostic() { Error="diagnostic_run_not_acceptance"; }
        internal void Pixel(int index,int awake,int owned,bool g0,long changed,long total,int max,long restored,bool stable)
        {
            if(index<0 || index>=3 || seen[index] || awake<0 || owned<0 || owned>awake || changed<0 || total<=0 || changed>total || max<0 || max>255){Error="invalid_pixel_sample";return;}
            seen[index]=true;
            if(index<2 && GpuDecoCoverage.Auxiliary(awake)){Error="primary_density_missing";return;}
            if(GpuDecoCoverage.OwnedLow(awake,owned)){Error="OWNED_LOW";return;}
            if(!g0 && !GpuDecoCoverage.Auxiliary(awake)){Error="G0_FAIL";return;}
            if(restored!=0 || !stable){Error="unstable_or_unrestored_baseline";return;}
            if(!GpuDecoCoverage.Auxiliary(awake) && (max>3 || changed*1000>total*3))Error="PIXEL_FAIL";
        }
        internal string Verdict(out string reason)
        {
            reason=Error;
            if(Error!=null)return Error=="OWNED_LOW" || Error=="G0_FAIL" || Error=="PIXEL_FAIL"?Error:"ERROR";
            if(!seen[0] || !seen[1] || !seen[2]){reason="missing_pixel_sample";return "ERROR";}
            reason="paused_G0_primary_pixels_and_restoration_passed";return "PASS";
        }
    }

    // Versioned diagnostic interchange, never a product asset or a live frame buffer.
#endif
    internal sealed class GpuDecoData
    {
        internal const int Version = 2;
        internal int Schema = Version;
        internal float MeanFrameSeconds, MeanProgressStep;
        internal int FrameSamples;
        internal SourceClock[] Sources = new SourceClock[0];
        // A scheduled moving segment cannot stand in for an unobserved actual
        // source. This metadata-only rule is shared by snapshot export and lab;
        // it never consults Actual, World or renderer color to choose ownership.
        internal int MotionWithoutClock(Item d)
        {
            for (int ch = 0; ch < 16; ch++)
            {
                if (d.Anchors[ch].Valid) continue;
                int n = DecoBakeCore.Find(d.Tracks[ch], BakeTime); if (n < 0) continue;
                var s = d.Tracks[ch][n];
                if (s.Duration > 0 && (BakeTime - s.T0) / Pitch * TweenScale < s.Duration
                    && s.Start != (s.IsScale ? s.Target : s.End)) return ch;
            }
            return -1;
        }
        internal float Eval(Item d, int ch, DecoBakeCore.EaseFn ease, bool anchored = true)
        {
            var a = d.Anchors[ch];
            if (anchored && a.Valid)
            {
                var s = a.Segment; if (s.Duration == 0) return s.End;
                float t = Sources[a.Clock].Progress;
                return t >= s.Duration ? s.End : t <= 0 ? s.Start : s.Start + (s.Target - s.Start) * ease(s.Ease, t, s.Duration, s.Over, s.Period);
            }
            int n = DecoBakeCore.Find(d.Tracks[ch], BakeTime);
            return n < 0 ? d.Initial[ch] : DecoBakeCore.Value(d.Tracks[ch][n], BakeTime, Pitch, TweenScale, ease);
        }
        internal sealed class SourceClock
        {
            internal int Id, StartFrame;
            internal double StartSong;
            internal float Progress;
        }
        internal struct Anchor
        {
            internal bool Valid;
            internal int Clock;
            internal DecoBakeCore.Segment Segment;
        }
        internal double RawTime, BakeTime;
        internal float Pitch, TweenScale, PixelsPerWorld;
        internal int Frame, Awake, Supported;
        internal string Fingerprint;
        // camera XY, angle degrees, lock-scale multiplier
        internal float[] Camera = new float[4];
        internal Item[] Items;
        internal sealed class Item
        {
            internal int Index, Id, MaterialId, TextureId, Layer, Order, Flags;
            internal string Placement;
            internal float[] Initial = new float[16], Actual = new float[16];
            internal DecoBakeCore.Segment[][] Tracks = new DecoBakeCore.Segment[16][];
            internal Anchor[] Anchors = new Anchor[16];
            // trueScale XY, textureScale XY, startRot, croppedPivot XY, worldZ
            internal float[] Constants = new float[8];
            internal float[] Shape = new float[12], OffsetBasis = new float[9];
            internal float[] Corners = new float[8], UV = new float[8], World = new float[12], Color = new float[4];
            internal int[] Triangles = new int[6];
        }
        static void Floats(BinaryWriter w, float[] values) { foreach (float v in values) { Check(v); w.Write(v); } }
        static void Floats(BinaryReader r, float[] values) { for (int i = 0; i < values.Length; i++) { values[i] = r.ReadSingle(); Check(values[i]); } }
        static void Check(double v) { if (!DecoBakeCore.Finite(v)) throw new InvalidDataException("nonfinite GPU decoration data"); }
        static int Count(BinaryReader r, int max) { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("GPU decoration count"); return n; }
        internal void Write(Stream stream)
        {
            if (Schema != Version) throw new InvalidDataException("legacy export cannot be relabeled as source-clock evidence");
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                w.Write(0x31445047); w.Write(Version); w.Write(Fingerprint); Check(RawTime); Check(BakeTime);
                w.Write(RawTime); w.Write(BakeTime); w.Write(Pitch); w.Write(TweenScale); w.Write(PixelsPerWorld);
                w.Write(Frame); w.Write(Awake); w.Write(Supported); Floats(w, Camera); w.Write(Items.Length);
                foreach (var d in Items)
                {
                    w.Write(d.Index); w.Write(d.Id); w.Write(d.MaterialId); w.Write(d.TextureId); w.Write(d.Layer); w.Write(d.Order); w.Write(d.Flags); w.Write(d.Placement);
                    Floats(w, d.Initial); Floats(w, d.Actual); Floats(w, d.Constants); Floats(w, d.Shape); Floats(w, d.OffsetBasis);
                    Floats(w, d.Corners); Floats(w, d.UV); Floats(w, d.World); Floats(w, d.Color);
                    foreach (int v in d.Triangles) w.Write(v);
                    foreach (var track in d.Tracks)
                    {
                        w.Write(track.Length);
                        foreach (var s in track)
                        {
                            Check(s.T0); w.Write(s.T0); w.Write(s.Duration); w.Write(s.Start); w.Write(s.End); w.Write(s.Target);
                            w.Write(s.Over); w.Write(s.Period); w.Write(s.Ease); w.Write(s.IsScale);
                            w.Write(s.Source); w.Write(s.Replacement);
                        }
                    }
                    foreach (var a in d.Anchors)
                    {
                        w.Write(a.Valid); if (!a.Valid) continue;
                        w.Write(a.Clock); var s = a.Segment;
                        w.Write(s.Duration); w.Write(s.Start); w.Write(s.End); w.Write(s.Target);
                        w.Write(s.Over); w.Write(s.Period); w.Write(s.Ease); w.Write(s.IsScale);
                    }
                }
                w.Write(Sources.Length);
                foreach (var s in Sources) { w.Write(s.Id); w.Write(s.StartFrame); Check(s.StartSong); w.Write(s.StartSong); Check(s.Progress); w.Write(s.Progress); }
                w.Write(MeanFrameSeconds); w.Write(FrameSamples); w.Write(MeanProgressStep);
            }
        }
        internal static GpuDecoData Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                if (r.ReadInt32() != 0x31445047) throw new InvalidDataException("GPU decoration magic");
                int version = r.ReadInt32(); if (version != 1 && version != Version) throw new InvalidDataException("GPU decoration schema");
                var data = new GpuDecoData { Schema = version, Fingerprint = r.ReadString(), RawTime = r.ReadDouble(), BakeTime = r.ReadDouble(),
                    Pitch = r.ReadSingle(), TweenScale = r.ReadSingle(), PixelsPerWorld = r.ReadSingle(), Frame = r.ReadInt32(), Awake = r.ReadInt32(), Supported = r.ReadInt32() };
                Check(data.RawTime); Check(data.BakeTime); Check(data.Pitch); Check(data.TweenScale); Check(data.PixelsPerWorld);
                if (data.Pitch <= 0 || data.TweenScale <= 0 || data.PixelsPerWorld <= 0) throw new InvalidDataException("GPU decoration clock/projection");
                Floats(r, data.Camera); data.Items = new Item[Count(r, 100000)]; int totalSegments = 0;
                foreach (int i in System.Linq.Enumerable.Range(0, data.Items.Length))
                {
                    var d = data.Items[i] = new Item { Index = r.ReadInt32(), Id = r.ReadInt32(), MaterialId = r.ReadInt32(), TextureId = r.ReadInt32(),
                        Layer = r.ReadInt32(), Order = r.ReadInt32(), Flags = r.ReadInt32(), Placement = r.ReadString() };
                    Floats(r, d.Initial); Floats(r, d.Actual); Floats(r, d.Constants); Floats(r, d.Shape); Floats(r, d.OffsetBasis);
                    Floats(r, d.Corners); Floats(r, d.UV); Floats(r, d.World); Floats(r, d.Color);
                    for (int j = 0; j < 6; j++) { d.Triangles[j] = r.ReadInt32(); if ((uint)d.Triangles[j] >= 4) throw new InvalidDataException("GPU decoration triangle"); }
                    for (int ch = 0; ch < 16; ch++)
                    {
                        int n = Count(r, 1000000); totalSegments = checked(totalSegments + n);
                        if (totalSegments > 4000000) throw new InvalidDataException("GPU decoration segment budget");
                        var track = d.Tracks[ch] = new DecoBakeCore.Segment[n]; double previous = double.NegativeInfinity;
                        for (int j = 0; j < n; j++)
                        {
                            var s = new DecoBakeCore.Segment { T0 = r.ReadDouble(), Duration = r.ReadSingle(), Start = r.ReadSingle(), End = r.ReadSingle(),
                                Target = r.ReadSingle(), Over = r.ReadSingle(), Period = r.ReadSingle(), Ease = r.ReadInt32(), IsScale = r.ReadBoolean() };
                            if (version >= 2) { s.Source = r.ReadInt32(); s.Replacement = r.ReadBoolean(); }
                            Check(s.T0); Check(s.Duration); Check(s.Start); Check(s.End); Check(s.Target); Check(s.Over); Check(s.Period);
                            if (s.T0 < previous || s.Duration < 0) throw new InvalidDataException("GPU decoration track order");
                            previous = s.T0; track[j] = s;
                        }
                    }
                    if (version >= 2) for (int ch = 0; ch < 16; ch++)
                    {
                        if (!r.ReadBoolean()) continue;
                        var a = new Anchor { Valid = true, Clock = r.ReadInt32(), Segment = new DecoBakeCore.Segment {
                            Duration = r.ReadSingle(), Start = r.ReadSingle(), End = r.ReadSingle(), Target = r.ReadSingle(),
                            Over = r.ReadSingle(), Period = r.ReadSingle(), Ease = r.ReadInt32(), IsScale = r.ReadBoolean() } };
                        var s = a.Segment; Check(s.Duration); Check(s.Start); Check(s.End); Check(s.Target); Check(s.Over); Check(s.Period);
                        if (s.Duration < 0 || (s.Duration > 0 ? a.Clock < 0 : a.Clock != -1)) throw new InvalidDataException("GPU decoration anchor");
                        d.Anchors[ch] = a;
                    }
                }
                if (version >= 2)
                {
                    data.Sources = new SourceClock[Count(r, 1000000)];
                    for (int i = 0; i < data.Sources.Length; i++)
                    {
                        var s = data.Sources[i] = new SourceClock { Id = r.ReadInt32(), StartFrame = r.ReadInt32(), StartSong = r.ReadDouble(), Progress = r.ReadSingle() };
                        Check(s.StartSong); Check(s.Progress);
                        if (s.StartFrame < 0 || s.StartFrame > data.Frame || s.Progress < 0) throw new InvalidDataException("GPU decoration source clock");
                    }
                    foreach (var d in data.Items) foreach (var a in d.Anchors)
                        if (a.Valid && a.Segment.Duration > 0 && a.Clock >= data.Sources.Length) throw new InvalidDataException("GPU decoration anchor clock index");
                    data.MeanFrameSeconds = r.ReadSingle(); data.FrameSamples = r.ReadInt32(); Check(data.MeanFrameSeconds);
                    data.MeanProgressStep = r.ReadSingle(); Check(data.MeanProgressStep);
                    if (data.MeanFrameSeconds < 0 || data.FrameSamples < 0 || data.FrameSamples > 256 || (data.FrameSamples == 0) != (data.MeanFrameSeconds == 0))
                        throw new InvalidDataException("GPU decoration frame interval");
                    if (data.MeanProgressStep < 0 || (data.FrameSamples == 0) != (data.MeanProgressStep == 0)) throw new InvalidDataException("GPU decoration progress interval");
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("GPU decoration trailing bytes");
                return data;
            }
        }
    }
}
#endif
