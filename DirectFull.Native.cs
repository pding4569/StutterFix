#if DEV || AUTOTEST
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StutterFix
{
    internal static unsafe class DirectFullNative
    {
        internal enum Op { Create, Program, Geometry, Draw, Commit, Event, Stats, Destroy, Error, Abi, Composite, Perf, CommitPacked, CommitActive, BatchPerf, GeometryCpu, CommitActiveWorld, UploadPerf, GpuPerf }
        [StructLayout(LayoutKind.Sequential)] internal struct Performance { internal double ApplyUs, CullUs, ConstantsUs, DrawUs, ExecuteUs; internal ulong Uploads, UploadBytes, LayoutCreates, LayoutHits, SampledFrames; }
        [StructLayout(LayoutKind.Sequential)] internal struct Composite { internal IntPtr Black, White; }
        [StructLayout(LayoutKind.Sequential)] internal struct Abi { internal uint Version, Create, Program, Geometry, Draw, Frame, Stats, Pose; }
        internal static void ValidateAbi()
        {
            Abi a = default; Require(Op.Abi, 0, &a);
            if (a.Version != 1 || a.Create != sizeof(Create) || a.Program != sizeof(Program) || a.Geometry != sizeof(Geometry) || a.Draw != sizeof(Draw) || a.Frame != sizeof(Frame) || a.Stats != sizeof(Stats) || a.Pose != sizeof(Pose))
                throw new InvalidOperationException("DRF managed/native ABI mismatch");
        }
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr sf_z_drfull_v1(int op, int token, void* args);
        internal static long Call(Op op, int token, void* args = null) => sf_z_drfull_v1((int)op, token, args).ToInt64();
        internal static int Require(Op op, int token, void* args)
        {
            long n = Call(op, token, args);
            if (n == 0) throw new InvalidOperationException("DRF native " + op + " hr=0x" + unchecked((uint)Call(Op.Error, 0)).ToString("X8"));
            return checked((int)n);
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Create { internal IntPtr Resource; internal uint Capacity, Abi; }
        [StructLayout(LayoutKind.Sequential)] internal struct Attribute { internal uint Semantic, Index, Format, Stream, Offset; }
        [StructLayout(LayoutKind.Sequential)] internal struct Program { internal byte* Vs; internal byte* Ps; internal uint VsBytes, PsBytes; internal fixed int State[29]; }
        [StructLayout(LayoutKind.Sequential)] internal struct Geometry
        {
            internal fixed long Buffers[8]; internal fixed uint Strides[8], Offsets[8];
            internal IntPtr Indices; internal uint IndexFormat, IndexStart, IndexCount; internal int BaseVertex;
            internal Attribute* Attributes; internal uint AttributeCount;
            internal void* Vertices; internal uint VertexBytes; internal uint* CpuIndices; internal uint CpuIndexCount;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Buffer { internal uint Stage, Slot, Bytes, Offset; }
        [StructLayout(LayoutKind.Sequential)] internal struct Patch { internal uint Offset, Kind; }
        [StructLayout(LayoutKind.Sequential)] internal struct Texture { internal IntPtr Resource; internal int Stage, Slot, Sampler, Filter, WrapU, WrapV, Aniso, Srgb; }
        [StructLayout(LayoutKind.Sequential)] internal struct Draw
        {
            internal uint Program, Geometry; internal Buffer* Buffers; internal uint BufferCount;
            internal Patch* Patches; internal uint PatchCount; internal Texture* Textures; internal uint TextureCount;
            internal byte* Constants; internal uint ConstantBytes, Pose, Grab, Sprite;
            internal fixed float Center[3], Extent[3];
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Pose { internal Matrix4x4 World, Inverse; }
        [StructLayout(LayoutKind.Sequential)] internal struct Frame
        {
            internal Matrix4x4 Vp, View, Projection;
            internal Vector4 Camera, Screen, ProjectionParams, Ortho, Time, SinTime, CosTime, Delta;
            internal Pose* Poses; internal uint* PoseIds; internal uint PoseCount;
            internal uint* DrawIds; internal uint* ConstantOffsets; internal byte* Constants; internal uint DirtyCount, ConstantBytes;
            internal uint* List; internal uint* Segments; internal uint ListCount, SegmentCount, ListChanged, ClearStencil;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct CpuGeometry { internal uint Geometry; internal void* Vertices; internal uint Count; internal uint* Indices; internal uint IndexCount; }
        [StructLayout(LayoutKind.Sequential)] internal struct BatchPerformance { internal ulong Batches, Sources, Vertices, SingleDraws, LiveEdits, ConstantCreates; internal double BuildUs; internal ulong GpuDispatches, GpuPoseUploads, GpuGeometryUploads, GpuAvailable; }
        [StructLayout(LayoutKind.Sequential)] internal struct UploadPerformance
        {
            internal ulong GeometryBytes, PoseBytes, DrawBytes, IndexBytes, ConstantBytes, ColorBytes, CpuBatchBytes;
            internal ulong PoseRanges, DrawRanges, IndexUploads, ConstantUploads;
            internal ulong SingleUnverified, SingleGeometry, SingleProgram, SingleConstants, SingleTextures, SingleSegment, SingleLimit;
            internal ulong WorkBytes, BakedVertices;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct GpuPerformance { internal ulong GrabCopies, GrabBytes, NamedGrabHits, CulledDraws, CulledSources; internal double EventUs, MaxEventUs; internal ulong Samples, Pending, Disjoint; }
        [StructLayout(LayoutKind.Sequential)] internal struct FrameActive { internal Frame Frame; internal uint* Ids; internal byte* Values; internal uint Count; }
        [StructLayout(LayoutKind.Sequential)] internal struct Stats { internal ulong Frames, Draws, Errors, Dropped, Bytes, Events; internal double RenderUs, CommitUs; internal int LastHr; }
    }
}
#endif
