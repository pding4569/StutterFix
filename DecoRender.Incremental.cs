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
        readonly Samples auditSamples = new Samples(), dirtySamples = new Samples(), changeSamples = new Samples(), layoutSamples = new Samples(),
            scheduleSamples = new Samples(), waitSamples = new Samples(), otherSamples = new Samples(),
            unaccountedSamples = new Samples(), drawOnSamples = new Samples(), drawOffSamples = new Samples();
        readonly Samples frameCpuSamples = new Samples();
        double frameCpuMs;
        int frameCpuFrame = -1;
        double auditMs, dirtyMs, changeMs, layoutMs, scheduleMs, waitMs, otherMs;
        int auditFrame = -1, polledBarriers, eventBarriers, ignoredBarriers, dirtyLogFrames;
        long layoutResets, dirtyFrames, changedSum, awakeSum;
        int dirtyUnwritten, dirtyPose, dirtyVisibility;
        static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        readonly Samples totalSamples = new Samples(), collectSamples = new Samples(),
            computeSamples = new Samples(), uploadSamples = new Samples(), topologySamples = new Samples();

        sealed class Samples
        {
            const int Window = 2048;
            readonly double[] values = new double[Window];
            int count, next;
            double sum;
            long calls;
            internal void Add(double v) { calls++; if (count == Window) sum -= values[next]; else count++; sum += v; values[next] = v; next = (next + 1) % Window; }
            internal string State() {
                var copy = new double[count]; Array.Copy(values, copy, count); Array.Sort(copy);
                return "mean=" + F(sum / Math.Max(1, count)) + ",p99="
                    + F(count == 0 ? 0 : copy[Math.Max(0, (int)Math.Ceiling(count * .99) - 1)])
                    + ",max=" + F(count == 0 ? 0 : copy[count - 1]) + ",n=" + count + ",calls=" + calls;
            }
            internal void Clear() { count = next = 0; sum = 0; calls = 0; }
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
            layoutResets++;
            // Existing runs retain their meshes. Preserve output for them across slot relocation;
            // only a newly created run needs its members written again.
            var retained = new HashSet<Item>();
            var oldStarts = new Dictionary<Item, int>();
            FloatVertex[] oldFloats = null; ByteVertex[] oldBytes = null;
            if (layoutReady) {
                if (floatColor) { oldFloats = new FloatVertex[floatOutput.Length]; floatOutput.CopyTo(oldFloats); }
                else { oldBytes = new ByteVertex[byteOutput.Length]; byteOutput.CopyTo(oldBytes); }
                foreach (var g in groups) foreach (var i in g.Items)
                    if (i.Written && ReferenceEquals(i.Run, g)) { retained.Add(i); oldStarts[i] = i.Start; }
            }
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
                i.Written = retained.Contains(i);
                if (!i.Written) i.Visible = false;
                else if (i.Visible) i.Run.VisibleItems.Add(i);
                i.PoseFrame = -1; i.PoseValidated = false; i.LocalBounds = i.Sprite.bounds;
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
                    if (retained.Contains(i)) {
                        if (floatColor) floatOutput[i.Start + n] = oldFloats[oldStarts[i] + n];
                        else byteOutput[i.Start + n] = oldBytes[oldStarts[i] + n];
                    }
                }
                if (i.Visible) i.Run.HasVisible = true;
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
            // Sleeping sources keep their cached geometry. Uniform changes arrive via Notify.
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
            layoutMs += Milliseconds(Stopwatch.GetTimestamp() - start);
            long dirtyStart = Stopwatch.GetTimestamp();
            GatherWork(initial || fullBuild); fullBuild = false;
            dirtyMs += Milliseconds(Stopwatch.GetTimestamp() - dirtyStart);
            long collectStart = Stopwatch.GetTimestamp();
            changedItems = uploadedVertices = nativeReads = poseHits = 0; collectCount = work.Count;
            dirtyUnwritten = dirtyPose = dirtyVisibility = 0;
            long changeTicks = 0;
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
                long changeStart = Stopwatch.GetTimestamp();
                bool moved = !MatrixEqual(i.Matrix, matrix) || !ColorEqual(i.Color, color) || i.FlipX != flipX || i.FlipY != flipY;
                i.Dirty = !i.Written || moved || i.Visible != visible;
                if (!i.Written) dirtyUnwritten++;
                else if (i.Visible != visible) dirtyVisibility++;
                else if (moved) dirtyPose++;
                if (i.Visible != visible) { if (visible) i.Run.VisibleItems.Add(i); else i.Run.VisibleItems.Remove(i); }
                // A z change invalidates the run ordering. Use originals until the next full rebuild.
                if (i.Written && visible && !SameBits(i.Matrix.m23, matrix.m23)) { topologyDirty = true; fallbackKeys.Add(OrderKey(i)); }
                if (moved && !i.Moved) movingCandidates++;
                i.Moved |= moved; i.Matrix = matrix; i.Color = color; i.FlipX = flipX; i.FlipY = flipY; i.Visible = visible;
                if (!i.Dirty) { changeTicks += Stopwatch.GetTimestamp() - changeStart; continue; }
                i.Run.BoundsDirty = true;
                geometry[changedGeometry.Count] = new Geometry { Matrix = matrix, Color = color, Start = i.Start,
                    Count = i.Vertices.Length, Visible = visible, FlipX = flipX, FlipY = flipY };
                changedGeometry.Add(i); i.Written = true;
                changeTicks += Stopwatch.GetTimestamp() - changeStart;
            }
            changeMs = Milliseconds(changeTicks);
            collectMs = Milliseconds(Stopwatch.GetTimestamp() - collectStart) - changeMs;
            changedItems = changedGeometry.Count;
            long computeStart = Stopwatch.GetTimestamp();
            JobHandle handle = default;
            if (changedItems != 0) handle = new VerticesJob { Inputs = geometry, Local = localVertices, UV = atlasUV,
                Floats = floatOutput, Bytes = byteOutput, FloatColor = floatColor }.Schedule(changedItems, 32);
            long waitStart = Stopwatch.GetTimestamp();
            scheduleMs = Milliseconds(waitStart - computeStart);
            if (changedItems != 0) handle.Complete();
            waitMs = Milliseconds(Stopwatch.GetTimestamp() - waitStart);
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
            long tailStart = Stopwatch.GetTimestamp();
            dirtyFrames++; changedSum += changedItems; awakeSum += awakeSet.Count;
            if (dirtyLogFrames++ < 10) Log("dirty frame=" + Time.frameCount + " changed=" + changedItems
                + " awake=" + awakeSet.Count + " unwritten=" + dirtyUnwritten + " pose=" + dirtyPose
                + " visibility=" + dirtyVisibility + " layout-resets=" + layoutResets);
            otherMs += Milliseconds(Stopwatch.GetTimestamp() - tailStart);
        }
        string IncrementalState() => " build-cpu-ms/window2048={total:" + totalSamples.State() + " collect:" + collectSamples.State()
            + " vertices-job+wait:" + computeSamples.State() + " upload+bounds:" + uploadSamples.State()
            + " topology:" + topologySamples.State() + "} collect-items=" + collectCount + " soa-hits=" + poseHits
            + " transform-fallbacks=" + nativeReads + " soa-verify=" + verifySoA + " soa-checks=" + soaChecks + " soa-mismatches=" + soaMismatches;
        string StageState() => "\n stage-ms/window2048 (mean,p99,max,n,calls):"
            + "\n audit=" + auditSamples.State() + "\n dirty+prune=" + dirtySamples.State()
            + "\n change-compare=" + changeSamples.State()
            + "\n layout=" + layoutSamples.State() + "\n collect=" + collectSamples.State()
            + "\n job-schedule=" + scheduleSamples.State() + "\n job-wait=" + waitSamples.State()
            + "\n upload+bounds=" + uploadSamples.State() + "\n topology=" + topologySamples.State()
            + "\n other=" + otherSamples.State() + "\n unaccounted-ms=" + unaccountedSamples.State()
            + "\n draw-on/callback=" + drawOnSamples.State() + "\n draw-off/callback=" + drawOffSamples.State()
            + "\n build+draw/completed-frame=" + frameCpuSamples.State()
            + "\n barrier-poll=" + polledBarriers + " barrier-event=" + eventBarriers + " barrier-ignored=" + ignoredBarriers
            + " layout-resets=" + layoutResets + " dirty-mean=" + F((double)changedSum / Math.Max(1, dirtyFrames))
            + " awake-mean=" + F((double)awakeSum / Math.Max(1, dirtyFrames))
            + " dirty-last/unwritten=" + dirtyUnwritten + "/pose=" + dirtyPose + "/visibility=" + dirtyVisibility;
        void ResetBuildStats()
        {
            totalSamples.Clear(); collectSamples.Clear(); computeSamples.Clear(); uploadSamples.Clear(); topologySamples.Clear();
            foreach (var s in new[] { auditSamples, dirtySamples, changeSamples, layoutSamples, scheduleSamples, waitSamples,
                otherSamples, unaccountedSamples, drawOnSamples, drawOffSamples }) s.Clear();
            frameCpuSamples.Clear(); frameCpuMs = 0; frameCpuFrame = -1;
            dirtyFrames = changedSum = awakeSum = 0; dirtyLogFrames = 0;
            buildTicks = buildCount = measuredFrames = frameTicks = 0;
            lastBuildMs = collectMs = computeMs = uploadMs = topologyMs = 0;
            // Preserve cumulative correctness failures and all geometry/session ownership.
        }

        void AuditLateBarriers()
        {
            if (auditFrame == Time.frameCount) return;
            auditFrame = Time.frameCount;
            // New topology must be created before culling. A late change therefore falls back
            // to originals for the affected keys until the next frame's normal rebuild.
            foreach (var watch in LiveBarrierChecks())
            {
                var r = watch.R;
                bool wasDrawing = watch.Enabled && watch.Active && !watch.ForceOff;
                bool enabled = r && r.enabled, active = r && r.gameObject.activeInHierarchy, off = r && r.forceRenderingOff;
                bool drawing = enabled && active && !off;
                // Waiting renderers need only the three visibility reads. Sorting is irrelevant until they draw.
                string key = drawing ? OrderKey(r.sortingLayerID, r.sortingOrder) : watch.Key;
                var material = drawing ? r.sharedMaterial : null;
                int queue = material ? material.renderQueue : int.MinValue, id = material ? material.GetInstanceID() : 0;
                float distance = drawing ? SortDistance(r, mainCamera) : watch.Distance;
                if (wasDrawing != drawing || (drawing && (key != watch.Key || queue != watch.Queue
                    || id != watch.Material || !SameBits(distance, watch.Distance))))
                {
                    topologyDirty = true; fallbackKeys.Add(watch.Key);
                    if (r) fallbackKeys.Add(key);
                    if (watch.Owner && watch.Owner.spriteRenderer)
                        fallbackKeys.Add(OrderKey(watch.Owner.spriteRenderer.sortingLayerID, watch.Owner.spriteRenderer.sortingOrder));
                }
                watch.Enabled = enabled; watch.Active = active; watch.ForceOff = off;
                watch.Key = key; watch.Queue = queue; watch.Material = id; watch.Distance = distance;
            }
            foreach (var d in notifiedOwners) { bool mesh;
                Item i;
                if (meshStates.TryGetValue(d, out mesh) && mesh && residents.TryGetValue(d, out i))
                    { fallbackKeys.Add(OrderKey(i)); topologyDirty = true; }
            }
            notifiedOwners.Clear();
        }
        void IndexBarriers()
        {
            ownerBarriers.Clear(); liveBarriers.Clear(); meshStates.Clear(); ignoredBarriers = 0;
            foreach (var i in items) meshStates[i.D] = MeshOn(i.D);
            foreach (var w in barrierWatches) {
                if (!ReferenceEquals(w.Owner, null)) {
                    List<BarrierWatch> list;
                    if (!ownerBarriers.TryGetValue(w.Owner, out list)) ownerBarriers[w.Owner] = list = new List<BarrierWatch>();
                    list.Add(w);
                    meshStates[w.Owner] = MeshOn(w.Owner);
                }
                // No per-frame walk of the disabled child-mesh population, even in managed code.
                if (ReferenceEquals(w.Owner, null)) {
                    // Active objects with disabled renderers (e.g. tile meshes) can start drawing.
                    // Inactive waiting objects are classified once and never polled.
                    if (w.Active) liveBarriers.Add(w); else ignoredBarriers++;
                }
            }
        }
        IEnumerable<BarrierWatch> LiveBarrierChecks()
        {
            barrierChecks.Clear();
            polledBarriers = liveBarriers.Count; eventBarriers = 0;
            foreach (var w in liveBarriers) barrierChecks.Add(w);
            foreach (var d in notifiedOwners) {
                List<BarrierWatch> list;
                if (ownerBarriers.TryGetValue(d, out list)) foreach (var w in list) if (barrierChecks.Add(w)) eventBarriers++;
            }
            return barrierChecks;
        }
    }
}
#endif
