#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal sealed partial class DecoRender
    {
        readonly Dictionary<scrDecoration, Item> residents = new Dictionary<scrDecoration, Item>();
        readonly Dictionary<Transform, Item> pivots = new Dictionary<Transform, Item>();
        readonly HashSet<Item> signalled = new HashSet<Item>();
        readonly HashSet<Item> workSet = new HashSet<Item>();
        readonly List<Item> work = new List<Item>(), previousAwake = new List<Item>();
        readonly List<Item> changedGeometry = new List<Item>(), hiddenSources = new List<Item>();
        readonly HashSet<scrDecoration> awakeSet = new HashSet<scrDecoration>();
        readonly HashSet<scrDecoration> notifiedOwners = new HashSet<scrDecoration>();
        readonly Dictionary<scrDecoration, List<BarrierWatch>> ownerBarriers = new Dictionary<scrDecoration, List<BarrierWatch>>();
        readonly Dictionary<scrDecoration, bool> meshStates = new Dictionary<scrDecoration, bool>();
        readonly List<BarrierWatch> liveBarriers = new List<BarrierWatch>();
        readonly HashSet<BarrierWatch> barrierChecks = new HashSet<BarrierWatch>();
        NativeArray<Vector2> localVertices, atlasUV;
        NativeArray<Geometry> geometry;
        NativeArray<FloatVertex> floatOutput;
        NativeArray<ByteVertex> byteOutput;
        // Managed SoA: no Transform/Renderer objects cross into worker jobs.
        float[] px, py, pz, qx, qy, qz, qw, sx, sy, sz, cr, cg, cb, ca;
        bool layoutReady, topologyDirty, verifySoA;
        bool fullBuild;
        int workFrame = -1;
        int collectCount, nativeReads, poseHits;
        long soaChecks, soaMismatches, verifySequence;
        double collectMs, topologyMs;
        readonly Samples totalSamples = new Samples(), collectSamples = new Samples(),
            computeSamples = new Samples(), uploadSamples = new Samples(), topologySamples = new Samples();

        sealed class Samples
        {
            const int Window = 2048;
            readonly double[] values = new double[Window];
            int count, next;
            double sum;
            internal void Add(double v) { if (count == Window) sum -= values[next]; else count++; sum += v; values[next] = v; next = (next + 1) % Window; }
            internal string State() {
                var copy = new double[count]; Array.Copy(values, copy, count); Array.Sort(copy);
                return "mean=" + F(sum / Math.Max(1, count)) + ",p99="
                    + F(count == 0 ? 0 : copy[Math.Max(0, (int)Math.Ceiling(count * .99) - 1)]) + ",n=" + count;
            }
            internal void Clear() { count = next = 0; sum = 0; }
        }
        struct Geometry
        {
            internal Matrix4x4 Matrix;
            internal Color Color;
            internal int Start, Count;
            internal bool Visible, FlipX, FlipY;
        }
        struct VerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Geometry> Inputs;
            [ReadOnly] public NativeArray<Vector2> Local, UV;
            [NativeDisableParallelForRestriction] public NativeArray<FloatVertex> Floats;
            [NativeDisableParallelForRestriction] public NativeArray<ByteVertex> Bytes;
            public bool FloatColor;
            public void Execute(int index)
            {
                var x = Inputs[index];
                for (int n = x.Start; n < x.Start + x.Count; n++)
                {
                    var p = Local[n]; p.x *= x.FlipX ? -1 : 1; p.y *= x.FlipY ? -1 : 1;
                    var position = x.Visible ? x.Matrix.MultiplyPoint3x4(new Vector3(p.x, p.y, 0)) : Vector3.zero;
                    var color = x.Visible ? x.Color : Color.clear;
                    if (FloatColor) Floats[n] = new FloatVertex { Position = position, Color = color, UV = UV[n] };
                    else Bytes[n] = new ByteVertex { Position = position, Color = color, UV = UV[n] };
                }
            }
        }
        internal static void Notify(scrDecoration d)
        {
            var s = session;
            if (ReferenceEquals(s, null) || s.stopping || !s.layoutReady || ReferenceEquals(d, null)) return;
            Item i;
            if (s.residents.TryGetValue(d, out i)) s.signalled.Add(i);
            s.notifiedOwners.Add(d); // Recheck this owner's disabled child renderers only on notifications.
            if (s.preparedFrame == Time.frameCount && !s.comparing) {
                if (i != null) s.fallbackKeys.Add(OrderKey(i));
                else { List<BarrierWatch> list; if (s.ownerBarriers.TryGetValue(d, out list))
                    foreach (var w in list) s.fallbackKeys.Add(w.Key); }
            }
        }
        internal static void NotifySetup(scrDecoration d)
        {
            Notify(d);
            var s = session;
            if (!ReferenceEquals(s, null) && s.layoutReady && !s.stopping) s.topologyDirty = true;
        }
        internal static void ObserveMesh(scrDecoration d, bool enabled)
        {
            var s = session; bool old;
            if (ReferenceEquals(s, null) || !s.layoutReady || s.stopping || !s.meshStates.TryGetValue(d, out old) || old == enabled) return;
            s.meshStates[d] = enabled; Notify(d); s.topologyDirty = true;
        }
        internal static void RecordLocal(Transform t, Vector3 position)
        {
            var s = session; Item i;
            if (ReferenceEquals(s, null) || !s.layoutReady || s.stopping) return;
            if (s.pivots.TryGetValue(t, out i)) {
                if (ReferenceEquals(t, i.RenderTransform) && !ReferenceEquals(t, i.PivotTransform)) {
                    i.ChildMatrix.m03 = position.x; i.ChildMatrix.m13 = position.y; i.ChildMatrix.m23 = position.z;
                    i.ChildIdentity = MatrixEqual(i.ChildMatrix, Matrix4x4.identity);
                } else i.PoseFrame = -1;
                s.signalled.Add(i);
                if (s.preparedFrame == Time.frameCount && !s.comparing) s.fallbackKeys.Add(OrderKey(i));
            }
        }
        internal static void Capture(scrDecoration d, Vector3 p, Quaternion q, Vector3 scale)
        {
            var s = session; Item i;
            if (ReferenceEquals(s, null) || !s.layoutReady || s.stopping || !s.residents.TryGetValue(d, out i)) return;
            int n = i.Slot;
            s.px[n] = p.x; s.py[n] = p.y; s.pz[n] = p.z;
            s.qx[n] = q.x; s.qy[n] = q.y; s.qz[n] = q.z; s.qw[n] = q.w;
            s.sx[n] = scale.x; s.sy[n] = scale.y; s.sz[n] = scale.z;
            var c = RenderColor(d);
            s.cr[n] = c.r; s.cg[n] = c.g; s.cb[n] = c.b; s.ca[n] = c.a;
            i.PoseFrame = Time.frameCount;
            if (s.preparedFrame == Time.frameCount && !s.comparing) s.fallbackKeys.Add(OrderKey(i));
        }
        static readonly HarmonyLib.AccessTools.FieldRef<scrDecoration, Color> RenderColor =
            HarmonyLib.AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        static readonly HarmonyLib.AccessTools.FieldRef<scrDecoration, bool> RenderEnabled =
            HarmonyLib.AccessTools.FieldRefAccess<scrDecoration, bool>("rendererEnabled");

        void ResetLayout()
        {
            DisposeBuffers(); residents.Clear(); pivots.Clear(); previousAwake.Clear(); signalled.Clear();
            int count = items.Count, vertices = 0;
            px = new float[count]; py = new float[count]; pz = new float[count];
            qx = new float[count]; qy = new float[count]; qz = new float[count]; qw = new float[count];
            sx = new float[count]; sy = new float[count]; sz = new float[count];
            cr = new float[count]; cg = new float[count]; cb = new float[count]; ca = new float[count];
            foreach (var g in groups) { g.VisibleItems.Clear(); g.HasVisible = false; }
            foreach (var g in groups) { int offset = 0; foreach (var i in g.Items) {
                i.Start = vertices; vertices += i.Vertices.Length; i.Run = g; i.Offset = offset; offset += i.Vertices.Length;
            } }
            for (int n = 0; n < count; n++)
            {
                var i = items[n]; i.Slot = n;
                i.Written = false; i.Visible = false; i.PoseFrame = -1; i.PoseValidated = false; i.LocalBounds = i.Sprite.bounds;
                residents[i.D] = i;
                i.Uniforms = ReadUniforms(i.R);
                var pivot = i.D.pivotTrans;
                i.PivotTransform = pivot; i.RenderTransform = i.R.transform;
                if (pivot) {
                    pivots[pivot] = i; pivots[i.RenderTransform] = i;
                    // Direct child local translation is maintained by MoveApply.RecordLocal.
                    i.NativeFallback = i.PoseRejected || (i.RenderTransform != pivot && i.RenderTransform.parent != pivot);
                    i.ChildMatrix = i.RenderTransform == pivot ? Matrix4x4.identity : Matrix4x4.TRS(
                        i.RenderTransform.localPosition, i.RenderTransform.localRotation, i.RenderTransform.localScale);
                    i.ChildIdentity = MatrixEqual(i.ChildMatrix, Matrix4x4.identity);
                } else i.NativeFallback = true;
            }
            localVertices = new NativeArray<Vector2>(Math.Max(1, vertices), Allocator.Persistent);
            atlasUV = new NativeArray<Vector2>(Math.Max(1, vertices), Allocator.Persistent);
            geometry = new NativeArray<Geometry>(Math.Max(1, count), Allocator.Persistent);
            // Both job fields must be valid even though only one output format is used.
            floatOutput = new NativeArray<FloatVertex>(floatColor ? Math.Max(1, vertices) : 1, Allocator.Persistent);
            byteOutput = new NativeArray<ByteVertex>(floatColor ? 1 : Math.Max(1, vertices), Allocator.Persistent);
            foreach (var i in items) {
                var a = i.Image.Atlas;
                int width = i.Image.Source.width, height = i.Image.Source.height;
                for (int n = 0; n < i.Vertices.Length; n++) {
                    localVertices[i.Start + n] = i.Vertices[n]; var uv = i.UV[n];
                    atlasUV[i.Start + n] = new Vector2((i.Image.X + uv.x * width) / a.Width, (i.Image.Y + uv.y * height) / a.Height);
                }
            }
            layoutReady = true; fullBuild = true; workFrame = -1;
        }
        void DisposeBuffers()
        {
            layoutReady = false;
            if (localVertices.IsCreated) localVertices.Dispose(); if (atlasUV.IsCreated) atlasUV.Dispose();
            if (geometry.IsCreated) geometry.Dispose(); if (floatOutput.IsCreated) floatOutput.Dispose();
            if (byteOutput.IsCreated) byteOutput.Dispose();
        }
        void AddWork(Item i) { if (workSet.Add(i)) work.Add(i); }
        void GatherWork(bool initial)
        {
            if (!initial && workFrame == Time.frameCount) return;
            workFrame = Time.frameCount;
            work.Clear(); workSet.Clear(); awakeSet.Clear();
            if (initial) foreach (var i in items) AddWork(i);
            // A sleeping run leader still lends its material to visible members. Audit that
            // one source per run as well; it never emits geometry unless it is awake/visible.
            foreach (var g in groups) if (g.Items.Count != 0) AddWork(g.Items[0]);
            foreach (var i in previousAwake) AddWork(i);
            previousAwake.Clear();
            foreach (var d in Dormancy.RenderAwake(manager)) {
                if (ReferenceEquals(d, null)) continue;
                awakeSet.Add(d); Item i;
                if (residents.TryGetValue(d, out i)) { AddWork(i); previousAwake.Add(i); }
            }
            foreach (var i in signalled) if (residents.ContainsKey(i.D)) AddWork(i);
            signalled.Clear();
        }
        Matrix4x4 ReadMatrix(Item i)
        {
            if (i.PoseFrame == Time.frameCount && !i.NativeFallback)
            {
                int n = i.Slot;
                var p = new Vector3(px[n], py[n], pz[n]); var q = new Quaternion(qx[n], qy[n], qz[n], qw[n]);
                var scale = new Vector3(sx[n], sy[n], sz[n]);
                var matrix = Matrix4x4.TRS(p, q, scale);
                if (!i.ChildIdentity) matrix = matrix * i.ChildMatrix;
                if (!i.PoseValidated || (verifySoA && (++verifySequence & 63) == 0)) {
                    soaChecks++; var t = i.D.pivotTrans;
                    var actual = i.R.transform.localToWorldMatrix;
                    if (!SameBits(t.position, p) || !SameBits(t.rotation, q) || !SameBits(t.localScale, scale)
                        || !MatrixEqual(actual, matrix) || !ColorEqual(i.R.color, new Color(cr[n], cg[n], cb[n], ca[n]))) {
                        soaMismatches++; i.NativeFallback = i.PoseRejected = true; nativeReads++; return actual;
                    }
                    i.PoseValidated = true;
                }
                poseHits++; return matrix;
            }
            nativeReads++; return i.R.transform.localToWorldMatrix;
        }
        static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static bool SameBits(Vector3 a, Vector3 b) => SameBits(a.x,b.x) && SameBits(a.y,b.y) && SameBits(a.z,b.z);
        static bool SameBits(Quaternion a, Quaternion b) => SameBits(a.x,b.x) && SameBits(a.y,b.y) && SameBits(a.z,b.z) && SameBits(a.w,b.w);

        void Build()
        {
            long start = Stopwatch.GetTimestamp(); bool initial = !layoutReady;
            if (initial) ResetLayout();
            GatherWork(initial || fullBuild); fullBuild = false;
            changedItems = uploadedVertices = nativeReads = poseHits = 0; collectCount = work.Count;
            changedGeometry.Clear();
            foreach (var i in work)
            {
                if (!residents.ContainsKey(i.D) || !i.R) continue;
                bool visible = awakeSet.Contains(i.D) && !Dormancy.ProbeDormant(i.D) && RenderEnabled(i.D)
                    && i.R.enabled && i.R.gameObject.activeInHierarchy && !SourceOff(i) && RenderColor(i.D).a > 0;
                var color = visible ? (i.PoseFrame == Time.frameCount && !i.NativeFallback ? RenderColor(i.D) : i.R.color) : i.Color;
                int slot = i.Slot;
                cr[slot] = color.r; cg[slot] = color.g; cb[slot] = color.b; ca[slot] = color.a;
                bool wasFallback = i.NativeFallback;
                var matrix = visible ? ReadMatrix(i) : i.Matrix;
                if (visible && !wasFallback && i.NativeFallback) {
                    color = i.R.color; cr[slot] = color.r; cg[slot] = color.g; cb[slot] = color.b; ca[slot] = color.a;
                }
                bool flipX = visible ? i.R.flipX : i.FlipX, flipY = visible ? i.R.flipY : i.FlipY;
                bool moved = !MatrixEqual(i.Matrix, matrix) || !ColorEqual(i.Color, color) || i.FlipX != flipX || i.FlipY != flipY;
                i.Dirty = !i.Written || moved || i.Visible != visible;
                if (i.Visible != visible) { if (visible) i.Run.VisibleItems.Add(i); else i.Run.VisibleItems.Remove(i); }
                // A z change invalidates the run ordering. Use originals until the next full rebuild.
                if (i.Written && visible && !SameBits(i.Matrix.m23, matrix.m23)) { topologyDirty = true; fallbackKeys.Add(OrderKey(i)); }
                if (moved && !i.Moved) movingCandidates++;
                i.Moved |= moved; i.Matrix = matrix; i.Color = color; i.FlipX = flipX; i.FlipY = flipY; i.Visible = visible;
                if (!i.Dirty) continue;
                i.Run.BoundsDirty = true;
                geometry[changedGeometry.Count] = new Geometry { Matrix = matrix, Color = color, Start = i.Start,
                    Count = i.Vertices.Length, Visible = visible, FlipX = flipX, FlipY = flipY };
                changedGeometry.Add(i); i.Written = true;
            }
            collectMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            changedItems = changedGeometry.Count;
            long computeStart = Stopwatch.GetTimestamp();
            if (changedItems != 0) new VerticesJob { Inputs = geometry, Local = localVertices, UV = atlasUV,
                Floats = floatOutput, Bytes = byteOutput, FloatColor = floatColor }.Schedule(changedItems, 32).Complete();
            computeMs = (Stopwatch.GetTimestamp() - computeStart) * 1000.0 / Stopwatch.Frequency;
            long uploadStart = Stopwatch.GetTimestamp();
            // Only dirty item ranges are uploaded. Stable, sleeping ranges retain degenerate geometry.
            changedGeometry.Sort((a,b) => a.Start.CompareTo(b.Start));
            for (int n = 0; n < changedGeometry.Count; n++) {
                var i = changedGeometry[n]; int count = i.Vertices.Length;
                while (n + 1 < changedGeometry.Count && ReferenceEquals(changedGeometry[n+1].Run, i.Run)
                    && changedGeometry[n+1].Start == i.Start + count) count += changedGeometry[++n].Vertices.Length;
                if (count == 0) continue;
                var flags = MeshUpdateFlags.DontRecalculateBounds;
                if (floatColor) i.Run.Mesh.SetVertexBufferData(floatOutput, i.Start, i.Offset, count, 0, flags);
                else i.Run.Mesh.SetVertexBufferData(byteOutput, i.Start, i.Offset, count, 0, flags);
                uploadedVertices += count;
            }
            foreach (var g in groups) {
                if (!g.BoundsDirty) continue;
                g.BoundsDirty = false;
                bool first = true;
                foreach (var i in g.VisibleItems) {
                    // Bounds from the same matrix as the job, with no per-item Renderer.bounds call.
                    var b = i.LocalBounds; var center = i.Matrix.MultiplyPoint3x4(new Vector3(b.center.x * (i.FlipX ? -1 : 1), b.center.y * (i.FlipY ? -1 : 1), 0));
                    var ex = i.Matrix.MultiplyVector(new Vector3(b.extents.x,0,0));
                    var ey = i.Matrix.MultiplyVector(new Vector3(0,b.extents.y,0));
                    var bounds = new Bounds(center, new Vector3(Math.Abs(ex.x)+Math.Abs(ey.x), Math.Abs(ex.y)+Math.Abs(ey.y), Math.Abs(ex.z)+Math.Abs(ey.z))*2);
                    if (first) { g.CullBounds = bounds; first = false; } else g.CullBounds.Encapsulate(bounds);
                }
                g.HasVisible = !first; g.Fresh = false;
                if (!first) { g.CullBounds.Expand(.01f); g.Renderer.bounds = g.CullBounds; }
            }
            uploadMs = (Stopwatch.GetTimestamp() - uploadStart) * 1000.0 / Stopwatch.Frequency;
        }
        string IncrementalState() => " build-cpu-ms/window2048={total:" + totalSamples.State() + " collect:" + collectSamples.State()
            + " vertices-job+wait:" + computeSamples.State() + " upload+bounds:" + uploadSamples.State()
            + " topology:" + topologySamples.State() + "} collect-items=" + collectCount + " soa-hits=" + poseHits
            + " transform-fallbacks=" + nativeReads + " soa-verify=" + verifySoA + " soa-checks=" + soaChecks + " soa-mismatches=" + soaMismatches;
        void ResetBuildStats()
        {
            totalSamples.Clear(); collectSamples.Clear(); computeSamples.Clear(); uploadSamples.Clear(); topologySamples.Clear();
            buildTicks = buildCount = measuredFrames = frameTicks = 0;
            lastBuildMs = collectMs = computeMs = uploadMs = topologyMs = 0;
            // Preserve cumulative correctness failures and all geometry/session ownership.
        }

        void IndexBarriers()
        {
            ownerBarriers.Clear(); liveBarriers.Clear(); meshStates.Clear();
            foreach (var i in items) meshStates[i.D] = MeshOn(i.D);
            foreach (var w in barrierWatches) {
                if (!ReferenceEquals(w.Owner, null)) {
                    List<BarrierWatch> list;
                    if (!ownerBarriers.TryGetValue(w.Owner, out list)) ownerBarriers[w.Owner] = list = new List<BarrierWatch>();
                    list.Add(w);
                    meshStates[w.Owner] = MeshOn(w.Owner);
                }
                // No per-frame walk of the disabled child-mesh population, even in managed code.
                if (ReferenceEquals(w.Owner, null) || (w.Enabled && w.Active && !w.ForceOff)) liveBarriers.Add(w);
            }
        }
        IEnumerable<BarrierWatch> LiveBarrierChecks()
        {
            barrierChecks.Clear();
            foreach (var w in liveBarriers) barrierChecks.Add(w);
            foreach (var d in notifiedOwners) {
                List<BarrierWatch> list;
                if (ownerBarriers.TryGetValue(d, out list)) foreach (var w in list) barrierChecks.Add(w);
            }
            return barrierChecks;
        }
    }
}
#endif
