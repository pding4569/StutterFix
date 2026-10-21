#if DEV || AUTOTEST || UNITY_EDITOR || GPUDECO_FIXTURE
using System;
using System.IO;

namespace StutterFix
{
    // Shared by the exporter and offline lab. Sparse scenes remain auxiliary
    // measurements; coverage is a gate only when enough decorations are awake.
    internal static class GpuDecoCoverage
    {
        internal static bool Auxiliary(int awake) => awake < 800;
        internal static bool OwnedLow(int awake, int owned) => !Auxiliary(awake) && (long)owned * 10 < (long)awake * 7;
    }

    // Versioned diagnostic interchange, never a product asset or a live frame buffer.
    internal sealed class GpuDecoData
    {
        internal const int Version = 1;
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
                        }
                    }
                }
            }
        }
        internal static GpuDecoData Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                if (r.ReadInt32() != 0x31445047 || r.ReadInt32() != Version) throw new InvalidDataException("GPU decoration schema");
                var data = new GpuDecoData { Fingerprint = r.ReadString(), RawTime = r.ReadDouble(), BakeTime = r.ReadDouble(),
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
                            Check(s.T0); Check(s.Duration); Check(s.Start); Check(s.End); Check(s.Target); Check(s.Over); Check(s.Period);
                            if (s.T0 < previous || s.Duration < 0) throw new InvalidDataException("GPU decoration track order");
                            previous = s.T0; track[j] = s;
                        }
                    }
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("GPU decoration trailing bytes");
                return data;
            }
        }
    }
}
#endif
