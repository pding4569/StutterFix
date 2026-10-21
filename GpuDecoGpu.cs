#if DEV || AUTOTEST || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
namespace StutterFix
{
    internal class GpuDecoGpu : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Segment { public float hi, lo, duration, start, end, target, over, period; public uint ease, scale; }
        [StructLayout(LayoutKind.Sequential)]
        struct Track { public uint first, count; public float initial; public uint pad; }
        [StructLayout(LayoutKind.Sequential)]
        struct Anchor { public Segment segment; public uint clock, valid; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Vertex { public Vector3 position; public Color color; public Vector2 uv; }
        static void Require(bool ok,string reason) { if(!ok) throw new InvalidOperationException(reason); }
        internal static bool Supported(int id) => id >= 0 && id <= 31 || id == 36;

        readonly ComputeShader shader; readonly GpuDecoData data; readonly int kernel;
        readonly List<IDisposable> resources = new List<IDisposable>();
        ComputeBuffer values, progress; readonly float[] sourceProgress; GraphicsBuffer vertices;
        internal bool UseAnchors = true;
        internal Mesh Mesh; internal int SegmentCount;
        internal GraphicsBuffer VertexBuffer => vertices;
        ComputeBuffer Buffer<T>(IList<T> source, int stride) where T:struct
        {
            var b = new ComputeBuffer(Math.Max(1,source.Count),stride); resources.Add(b);
            if(source.Count>0) b.SetData(source.ToArray()); return b;
        }
        internal GpuDecoGpu(ComputeShader shader,GpuDecoData data)
        {
            this.shader=shader; this.data=data; kernel=shader.FindKernel("Evaluate");
            sourceProgress=new float[data.Sources.Length];
            try
            {
                var segments=new List<Segment>(); var tracks=new List<Track>(); var anchors=new List<Anchor>(); var shape=new List<Vector4>(); var uv=new List<Vector2>(); var triangles=new List<int>();
                for(int i=0;i<data.Items.Length;i++)
                {
                    var d=data.Items[i];
                    for(int ch=0;ch<16;ch++)
                    {
                        var a=d.Anchors[ch]; var s0=a.Segment;
                        if(a.Valid) Require(Supported(s0.Ease),"unsupported anchor ease " + s0.Ease);
                        anchors.Add(new Anchor { valid=a.Valid?1u:0u, clock=(uint)a.Clock, segment=new Segment {
                            duration=s0.Duration,start=s0.Start,end=s0.End,target=s0.Target,over=s0.Over,period=s0.Period,ease=(uint)s0.Ease,scale=s0.IsScale?1u:0u } });
                        tracks.Add(new Track { first=(uint)segments.Count,count=(uint)d.Tracks[ch].Length,initial=d.Initial[ch] });
                        foreach(var s in d.Tracks[ch])
                        {
                            Require(Supported(s.Ease),"unsupported ease " + s.Ease + " decoration="+d.Id);
                            float hi=(float)s.T0;
                            segments.Add(new Segment { hi=hi,lo=(float)(s.T0-hi),duration=s.Duration,start=s.Start,end=s.End,target=s.Target,over=s.Over,period=s.Period,ease=(uint)s.Ease,scale=s.IsScale?1u:0u });
                        }
                    }
                    for(int v=0;v<4;v++) { shape.Add(new Vector4(d.Shape[v*3],d.Shape[v*3+1],d.Shape[v*3+2],0)); uv.Add(new Vector2(d.UV[v*2],d.UV[v*2+1])); }
                    for(int v=0;v<3;v++) shape.Add(new Vector4(d.OffsetBasis[v*3],d.OffsetBasis[v*3+1],d.OffsetBasis[v*3+2],0));
                    var c=d.Constants; shape.Add(new Vector4(c[0],c[1],c[2],c[3])); shape.Add(new Vector4(c[5],c[6],c[7],d.Flags)); shape.Add(new Vector4(c[4],0,0,0));
                    triangles.AddRange(d.Triangles.Select(x=>i*4+x));
                }
                SegmentCount=segments.Count;
                shader.SetBuffer(kernel,"_Segments",Buffer(segments,40)); shader.SetBuffer(kernel,"_Tracks",Buffer(tracks,16));
                Require(Marshal.SizeOf<Anchor>()==48,"anchor GPU stride");
                shader.SetBuffer(kernel,"_Anchors",Buffer(anchors,48));
                progress=Buffer(data.Sources.Select(s=>s.Progress).ToArray(),4); shader.SetBuffer(kernel,"_Progress",progress);
                shader.SetBuffer(kernel,"_Static",Buffer(shape,16)); shader.SetBuffer(kernel,"_UV",Buffer(uv,8));
                values=new ComputeBuffer(data.Items.Length*16,4); resources.Add(values); shader.SetBuffer(kernel,"_Values",values);
                Mesh=new Mesh { name="GpuDeco raw",indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw };
                Mesh.SetVertexBufferParams(data.Items.Length*4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
                Mesh.SetVertexBufferData(new Vertex[data.Items.Length*4],0,0,data.Items.Length*4);
                Mesh.SetIndices(triangles.ToArray(),MeshTopology.Triangles,0,false); Mesh.bounds=new Bounds(Vector3.zero,Vector3.one*1000000);
                // The CPU shadow contains only initialization zeros. Discard it
                // before acquiring the GPU buffer so CPU batching cannot consume it.
                Mesh.UploadMeshData(true);
                vertices=Mesh.GetVertexBuffer(0); resources.Add(vertices); shader.SetBuffer(kernel,"_Vertices",vertices);
                Require(Marshal.SizeOf<Vertex>()==36 && Mesh.GetVertexBufferStride(0)==36,"raw vertex stride");
                shader.SetFloat("_Pitch",data.Pitch); shader.SetFloat("_TweenScale",data.TweenScale);
                shader.SetVector("_Camera",new Vector4(data.Camera[0],data.Camera[1],data.Camera[2],data.Camera[3]));
                shader.SetInt("_Count",data.Items.Length);
            }
            catch { Dispose(); throw; }
        }
        internal (float[] values,Vertex[] vertices) Dispatch(double time,bool read)
        {
            shader.SetInt("_UseAnchors",UseAnchors?1:0);
            // One upload for source progress. Static geometry/segments are never uploaded here.
            if(UseAnchors && sourceProgress.Length>0)
            {
                for(int i=0;i<sourceProgress.Length;i++) sourceProgress[i]=data.Sources[i].Progress;
                progress.SetData(sourceProgress);
            }
            float hi=(float)time; shader.SetFloat("_TimeHi",hi); shader.SetFloat("_TimeLo",(float)(time-hi));
            shader.Dispatch(kernel,(data.Items.Length+63)/64,1,1);
            if(!read) return (null,null);
            var channels=new float[data.Items.Length*16]; var points=new Vertex[data.Items.Length*4]; values.GetData(channels); vertices.GetData(points); return (channels,points);
        }
        public void Dispose() { for(int n=resources.Count-1;n>=0;n--) resources[n].Dispose(); resources.Clear(); if(Mesh) {if(Application.isPlaying)UnityEngine.Object.Destroy(Mesh);else UnityEngine.Object.DestroyImmediate(Mesh);} Mesh=null; }
    }
}
#endif
