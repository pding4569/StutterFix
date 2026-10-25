#if DEV || AUTOTEST || GPUDECO || UNITY_EDITOR
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
        internal struct Segment { public float hi, lo, duration, start, end, target, over, period; public uint ease, scale; }
        [StructLayout(LayoutKind.Sequential)]
        struct Track { public uint first, count; public float initial; public uint pad; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Anchor { public Segment segment; public uint clock, valid; }
        // Request B (G2): per-channel active descriptor updated only on effect start/end.
        // sourceIndex < 0 => static/completed value = end; sourceIndex >= 0 => start + change * _SourceE[sourceIndex].
        [StructLayout(LayoutKind.Sequential)]
        internal struct LiveChannel { public float start, change, end; public int sourceIndex; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Vertex { public Vector3 position; public Color color; public Vector2 uv; }
        static void Require(bool ok,string reason) { if(!ok) throw new InvalidOperationException(reason); }
        internal static bool Supported(int id) => id >= 0 && id <= 31 || id == 36;
        // Native SpriteRenderer uses half-up UNorm8 conversion, unlike the
        // managed Color32 cast's ties-to-even. Only draw buffers use this rule.
        internal static Color DrawColor(Color logical)
        {
            var result=new Color();
            for(int c=0;c<4;c++)result[c]=Mathf.Floor(Mathf.Clamp01(logical[c])*255f+.5f)/255f;
            return result;
        }
        internal static bool EquivalentDrawColor(Color actual,Color logical)
        {
            Color expected=DrawColor(logical);
            for(int c=0;c<4;c++)
            {
                // HLSL division by 255 may differ from CPU division by one
                // float ULP. Keep the existing 1e-7 draw tolerance and also
                // require exactly the same half-up UNorm8 component.
                if(!DecoBakeCore.Finite(actual[c]) || Mathf.Abs(actual[c]-expected[c])>1e-7f
                    || Mathf.Floor(Mathf.Clamp01(actual[c])*255f+.5f)!=Mathf.Floor(Mathf.Clamp01(expected[c])*255f+.5f))return false;
            }
            return true;
        }

        readonly ComputeShader shader; readonly GpuDecoData data;
        readonly int kernel, liveKernel;
        readonly bool hasLiveKernel,hasImageGeometry;
        readonly List<IDisposable> resources = new List<IDisposable>();
        ComputeBuffer imageShapeBuffer;
        ComputeBuffer segmentsBuffer, tracksBuffer, anchorsBuffer, liveChannelsBuffer, sourceEBuffer, values, progress, staticBuffer, uvBuffer;
        float[] sourceProgress;
        GraphicsBuffer vertices;
        internal bool UseAnchors = true;
        internal bool UseLiveMode = false;
        internal Mesh Mesh; internal int SegmentCount;
        internal GraphicsBuffer VertexBuffer => vertices;
        internal void ReadValues(float[] destination) => values.GetData(destination);
        internal GpuDecoData Data => data;
        internal bool HasNativeLiveKernel => hasLiveKernel;

        ComputeBuffer Buffer<T>(IList<T> source, int stride) where T:struct
        {
            var b = new ComputeBuffer(Math.Max(1,source.Count),stride); resources.Add(b);
            if(source.Count>0) b.SetData(source.ToArray()); return b;
        }
        internal GpuDecoGpu(ComputeShader shader, GpuDecoData data, int maxLiveSources = 2048)
        {
            this.shader=shader; this.data=data;
            kernel=shader.FindKernel("Evaluate");
            hasLiveKernel=shader.HasKernel("EvaluateLive");
            hasImageGeometry=shader.HasKernel("EvaluateImageLive");
            liveKernel=hasLiveKernel?shader.FindKernel("EvaluateLive"):kernel;
            sourceProgress=new float[Math.Max(Math.Max(1,data.Sources.Length), maxLiveSources)];
            try
            {
                var segments=new List<Segment>(); var tracks=new List<Track>(); var anchors=new List<Anchor>();
                var liveChannels=new LiveChannel[Math.Max(1, data.Items.Length*16)];
                var shape=new List<Vector4>(); var uv=new List<Vector2>(); var triangles=new List<int>();
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
                        float initVal = d.Initial[ch];
                        liveChannels[i*16+ch] = new LiveChannel { start=initVal, change=0f, end=initVal, sourceIndex=-1 };
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
                segmentsBuffer=Buffer(segments,40);
                tracksBuffer=Buffer(tracks,16);
                Require(Marshal.SizeOf<Anchor>()==48,"anchor GPU stride");
                anchorsBuffer=Buffer(anchors,48);
                Require(Marshal.SizeOf<LiveChannel>()==16,"live channel GPU stride");
                liveChannelsBuffer=Buffer(liveChannels,16);
                sourceEBuffer=new ComputeBuffer(sourceProgress.Length,4); resources.Add(sourceEBuffer);
                progress=new ComputeBuffer(sourceProgress.Length,4); resources.Add(progress);
                if(data.Sources.Length>0)
                {
                    for(int i=0;i<data.Sources.Length;i++) sourceProgress[i]=data.Sources[i].Progress;
                    progress.SetData(sourceProgress,0,0,data.Sources.Length);
                }
                staticBuffer=Buffer(shape,16);
                uvBuffer=Buffer(uv,8);
                if(hasImageGeometry)imageShapeBuffer=Buffer(new Vector4[Math.Max(1,data.Items.Length*8)],16);
                values=new ComputeBuffer(Math.Max(1,data.Items.Length*16),4); resources.Add(values);
                Mesh=new Mesh { name="GpuDeco raw",indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw };
                Mesh.SetVertexBufferParams(data.Items.Length*4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
                Mesh.SetVertexBufferData(new Vertex[data.Items.Length*4],0,0,data.Items.Length*4);
                Mesh.SetIndices(triangles.ToArray(),MeshTopology.Triangles,0,false); Mesh.bounds=new Bounds(Vector3.zero,Vector3.one*1000000);
                // The CPU shadow contains only initialization zeros. Discard it
                // before acquiring the GPU buffer so CPU batching cannot consume it.
                Mesh.UploadMeshData(true);
                vertices=Mesh.GetVertexBuffer(0); resources.Add(vertices);
                Require(Marshal.SizeOf<Vertex>()==36 && Mesh.GetVertexBufferStride(0)==36,"raw vertex stride");
                Bind();
            }
            catch { Dispose(); throw; }
        }
#if AUTOTEST || GPUDECO || UNITY_EDITOR
        // Event-bound image update. Never resize or replace a mesh/VB while a
        // native copy context may retain its pointer. Caller must recover CPU
        // ownership if a GPU upload throws; validation completes before writes.
        internal Vertex[] ReadImageVertices()
        {var result=new Vertex[data.Items.Length*4];vertices.GetData(result);return result;}
        readonly Vector4[] imageStaticUpload=new Vector4[10];
        readonly Vector2[] imageUvUpload=new Vector2[4];
        internal void UpdateImageGeometry(int index,GpuDecoData.Item next)
        {
            Require((uint)index<(uint)data.Items.Length && next!=null,"image item index");
            var prior=data.Items[index];
            Require(next.Shape?.Length==12 && next.OffsetBasis?.Length==9 && next.Constants?.Length==8
                && next.UV?.Length==8 && next.Triangles?.Length==6,"image geometry layout");
            Require(next.Id==prior.Id && next.Index==prior.Index && ReferenceEquals(next.Initial,prior.Initial)
                && ReferenceEquals(next.Tracks,prior.Tracks) && ReferenceEquals(next.Anchors,prior.Anchors),"image update changed logical channels");
            // Triangles remain in the existing index buffer; changed topology
            // must use CPU fallback, not SetIndices on an active native target.
            for(int i=0;i<6;i++)Require(next.Triangles[i]==prior.Triangles[i],"image topology changed");
            foreach(float v in next.Shape)Require(DecoBakeCore.Finite(v),"nonfinite image shape");
            foreach(float v in next.OffsetBasis)Require(DecoBakeCore.Finite(v),"nonfinite image basis");
            foreach(float v in next.Constants)Require(DecoBakeCore.Finite(v),"nonfinite image constants");
            foreach(float v in next.UV)Require(DecoBakeCore.Finite(v),"nonfinite image UV");
            Require(next.Constants[0]!=0 && next.Constants[1]!=0 && next.Constants[2]!=0 && next.Constants[3]!=0
                && (next.Flags&~(hasImageGeometry?127:63))==0,"image geometry constants");
            for(int i=0;i<4;i++)
            {
                imageStaticUpload[i]=new Vector4(next.Shape[i*3],next.Shape[i*3+1],next.Shape[i*3+2],0);
                imageUvUpload[i]=new Vector2(next.UV[i*2],next.UV[i*2+1]);
            }
            for(int i=0;i<3;i++)imageStaticUpload[i+4]=new Vector4(next.OffsetBasis[i*3],next.OffsetBasis[i*3+1],next.OffsetBasis[i*3+2],0);
            var c=next.Constants;
            imageStaticUpload[7]=new Vector4(c[0],c[1],c[2],c[3]);
            imageStaticUpload[8]=new Vector4(c[5],c[6],c[7],next.Flags);
            imageStaticUpload[9]=new Vector4(c[4],0,0,0);
            staticBuffer.SetData(imageStaticUpload,0,index*10,10);
            uvBuffer.SetData(imageUvUpload,0,index*4,4);
            // Logical channels/source clocks are owned by the existing effect
            // path. This operation deliberately does not replace them.
            data.Items[index]=next;
        }
#endif
        internal ComputeShader Shader=>shader;
        internal bool HasImageGeometry=>hasImageGeometry;
#if AUTOTEST || GPUDECO || UNITY_EDITOR
        readonly Vector4[] imageMatrixUpload=new Vector4[8];
        internal void UpdateImageMatrix(int index,Matrix4x4 local,Vector2[] corners,bool flipX,bool flipY)
        {
            Require(hasImageGeometry&&imageShapeBuffer!=null&&(uint)index<(uint)data.Items.Length&&corners!=null&&corners.Length==4,"image matrix unsupported");
            for(int i=0;i<16;i++)Require(DecoBakeCore.Finite(local[i]),"nonfinite image matrix");
            for(int i=0;i<4;i++)Require(DecoBakeCore.Finite(corners[i].x)&&DecoBakeCore.Finite(corners[i].y),"nonfinite image corner");
            for(int i=0;i<4;i++)imageMatrixUpload[i]=local.GetColumn(i);
            for(int i=0;i<4;i++)imageMatrixUpload[i+4]=new Vector4(flipX?-corners[i].x:corners[i].x,flipY?-corners[i].y:corners[i].y,0,0);
            imageShapeBuffer.SetData(imageMatrixUpload,0,index*8,8);
        }
#endif
        internal void Bind()
        {
            shader.SetBuffer(kernel,"_Segments",segmentsBuffer); shader.SetBuffer(kernel,"_Tracks",tracksBuffer);
            shader.SetBuffer(kernel,"_Anchors",anchorsBuffer); shader.SetBuffer(kernel,"_Progress",progress);
            shader.SetBuffer(kernel,"_Static",staticBuffer); shader.SetBuffer(kernel,"_UV",uvBuffer);
            shader.SetBuffer(kernel,"_Values",values); shader.SetBuffer(kernel,"_Vertices",vertices);
            if(hasLiveKernel)
            {
                shader.SetBuffer(liveKernel,"_LiveChannels",liveChannelsBuffer);
                shader.SetBuffer(liveKernel,"_SourceE",sourceEBuffer);
                shader.SetBuffer(liveKernel,"_Static",staticBuffer);
                shader.SetBuffer(liveKernel,"_UV",uvBuffer);
                shader.SetBuffer(liveKernel,"_Values",values);
                shader.SetBuffer(liveKernel,"_Vertices",vertices);
            }
            if(hasImageGeometry)
            {
                shader.SetBuffer(kernel,"_ImageShape",imageShapeBuffer);
                if(hasLiveKernel)shader.SetBuffer(liveKernel,"_ImageShape",imageShapeBuffer);
            }
            shader.SetFloat("_Pitch",data.Pitch); shader.SetFloat("_TweenScale",data.TweenScale);
            shader.SetVector("_Camera",new Vector4(data.Camera[0],data.Camera[1],data.Camera[2],data.Camera[3]));
            shader.SetInt("_Count",data.Items.Length);
        }
        // Uploads dirty channel range [dirtyMin..dirtyMax] only when an effect starts or finishes.
        internal void UploadDirtyLiveChannels(LiveChannel[] liveChannels, Anchor[] fallbackAnchors, int dirtyMin, int dirtyMax)
        {
            if(dirtyMin > dirtyMax || dirtyMin < 0) return;
            int count = dirtyMax - dirtyMin + 1;
            if(hasLiveKernel)
                liveChannelsBuffer.SetData(liveChannels, dirtyMin, dirtyMin, count);
            else if(fallbackAnchors != null)
                anchorsBuffer.SetData(fallbackAnchors, dirtyMin, dirtyMin, count);
        }
        // Uploads CPU-evaluated sourceE[] (and sourceProgress[] for fallback bundle) once per frame.
        internal void UploadSourceE(float[] sourceE, float[] rawProgress, int count)
        {
            if(count <= 0) return;
            if(hasLiveKernel)
                sourceEBuffer.SetData(sourceE, 0, 0, count);
            else
                progress.SetData(rawProgress, 0, 0, count);
        }
        internal void SetLiveUniforms(Vector4 camera, double time)
        {
            shader.SetInt("_UseAnchors", 1);
            shader.SetInt("_UseLiveChannels", (UseLiveMode && hasLiveKernel) ? 1 : 0);
            shader.SetVector("_Camera", camera);
            float hi = (float)time;
            shader.SetFloat("_TimeHi", hi);
            shader.SetFloat("_TimeLo", (float)(time - hi));
        }
        internal void DispatchEvaluate()
        {
            int activeKernel = (UseLiveMode && hasLiveKernel) ? liveKernel : kernel;
            shader.Dispatch(activeKernel, (data.Items.Length + 63) / 64, 1, 1);
        }
        // 구간 디스패치를 한 번만 기록해 두고(구간 버퍼는 재계획 때만 바뀐다) 프레임마다는 유니폼 작은 버퍼 + 이 버퍼 실행만 한다.
        CommandBuffer collapseRecorded,collapseUniforms;
        internal bool HasRecordedCollapse=>collapseRecorded!=null;
        internal void ReleaseRecordedCollapse(){collapseRecorded?.Release();collapseRecorded=null;collapseUniforms?.Release();collapseUniforms=null;}
        internal void RecordCollapse(ComputeBuffer texture,IList<GraphicsBuffer> targets,IList<ComputeBuffer> maps,IList<int> counts)
        {
            Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");
            ReleaseRecordedCollapse();
            int k=shader.FindKernel("EvaluateCollapse");collapseKernel=k;
            var c=new CommandBuffer{name="GpuDeco collapse spans"};
            c.SetComputeBufferParam(shader,k,"_LiveChannels",liveChannelsBuffer);c.SetComputeBufferParam(shader,k,"_SourceE",sourceEBuffer);
            c.SetComputeBufferParam(shader,k,"_Static",staticBuffer);c.SetComputeBufferParam(shader,k,"_UV",uvBuffer);
            c.SetComputeBufferParam(shader,k,"_Values",values);
            if(hasImageGeometry)c.SetComputeBufferParam(shader,k,"_ImageShape",imageShapeBuffer);
            c.SetComputeBufferParam(shader,k,"_CollapseTexture",texture);
            c.SetComputeFloatParam(shader,"_Pitch",data.Pitch);c.SetComputeFloatParam(shader,"_TweenScale",data.TweenScale);
            c.SetComputeIntParam(shader,"_Count",data.Items.Length);
            for(int i=0;i<targets.Count;i++)
            {
                c.SetComputeBufferParam(shader,k,"_Vertices",targets[i]);c.SetComputeBufferParam(shader,k,"_CollapseMap",maps[i]);
                c.SetComputeIntParam(shader,"_CollapseCount",counts[i]);
                c.DispatchCompute(shader,k,(counts[i]+63)/64,1,1);
            }
            collapseRecorded=c;collapseUniforms=new CommandBuffer{name="GpuDeco collapse uniforms"};
        }
        // 전역 정점 버퍼 하나에 모든 구간을 한 번의 디스패치로 쓴다.
        internal void RecordCollapseAll(ComputeBuffer texture,ComputeBuffer verts,ComputeBuffer map,int count)
        {
            Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");
            ReleaseRecordedCollapse();
            int k=shader.FindKernel("EvaluateCollapse");collapseKernel=k;
            var c=new CommandBuffer{name="GpuDeco collapse all"};
            c.SetComputeBufferParam(shader,k,"_LiveChannels",liveChannelsBuffer);c.SetComputeBufferParam(shader,k,"_SourceE",sourceEBuffer);
            c.SetComputeBufferParam(shader,k,"_Static",staticBuffer);c.SetComputeBufferParam(shader,k,"_UV",uvBuffer);
            c.SetComputeBufferParam(shader,k,"_Values",values);
            if(hasImageGeometry)c.SetComputeBufferParam(shader,k,"_ImageShape",imageShapeBuffer);
            c.SetComputeBufferParam(shader,k,"_CollapseTexture",texture);
            c.SetComputeFloatParam(shader,"_Pitch",data.Pitch);c.SetComputeFloatParam(shader,"_TweenScale",data.TweenScale);
            c.SetComputeIntParam(shader,"_Count",data.Items.Length);
            c.SetComputeBufferParam(shader,k,"_Vertices",verts);c.SetComputeBufferParam(shader,k,"_CollapseMap",map);
            c.SetComputeIntParam(shader,"_CollapseCount",count);
            c.DispatchCompute(shader,k,(count+63)/64,1,1);
            collapseRecorded=c;collapseUniforms=new CommandBuffer{name="GpuDeco collapse uniforms"};
        }
        // 프레임마다 명령 버퍼 두 개를 실행하던 것을 즉시 디스패치 한 번으로(관리→네이티브 호출 수와 명령 버퍼 제출 비용을 줄인다).
        internal void DispatchCollapseAllDirect(ComputeBuffer texture,ComputeBuffer verts,ComputeBuffer map,int count,Vector4 camera,double time,Vector4 clip)
        {
            BeginCollapse(texture);int k=collapseKernel;
            shader.SetFloat("_Pitch",data.Pitch);shader.SetFloat("_TweenScale",data.TweenScale);shader.SetInt("_Count",data.Items.Length);
            shader.SetInt("_UseAnchors",1);shader.SetInt("_UseLiveChannels",(UseLiveMode&&hasLiveKernel)?1:0);
            shader.SetVector("_Camera",camera);shader.SetVector("_CameraClip",clip);
            float hi=(float)time;shader.SetFloat("_TimeHi",hi);shader.SetFloat("_TimeLo",(float)(time-hi));
            shader.SetBuffer(k,"_Vertices",verts);shader.SetBuffer(k,"_CollapseMap",map);
            shader.SetInt("_CollapseCount",count);shader.Dispatch(k,(count+63)/64,1,1);
        }
        internal void DispatchCollapseAll(ComputeBuffer texture,ComputeBuffer verts,ComputeBuffer map,int count)
        {
            BeginCollapse(texture);int k=collapseKernel;
            shader.SetBuffer(k,"_Vertices",verts);shader.SetBuffer(k,"_CollapseMap",map);
            shader.SetInt("_CollapseCount",count);shader.Dispatch(k,(count+63)/64,1,1);
        }
        internal void ExecuteRecordedCollapse(Vector4 camera,double time,Vector4 clip)
        {
            var u=collapseUniforms;u.Clear();
            u.SetComputeIntParam(shader,"_UseAnchors",1);u.SetComputeIntParam(shader,"_UseLiveChannels",(UseLiveMode&&hasLiveKernel)?1:0);
            u.SetComputeVectorParam(shader,"_Camera",camera);u.SetComputeVectorParam(shader,"_CameraClip",clip);
            float hi=(float)time;u.SetComputeFloatParam(shader,"_TimeHi",hi);u.SetComputeFloatParam(shader,"_TimeLo",(float)(time-hi));
            Graphics.ExecuteCommandBuffer(u);Graphics.ExecuteCommandBuffer(collapseRecorded);
        }
        int collapseKernel=-1;
        // 프레임당 한 번: 구간마다 같은 공통 버퍼를 다시 설정하지 않도록 먼저 묶어 둔다(구간당 SetBuffer 6번+Require 절약).
        internal void BeginCollapse(ComputeBuffer texture)
        {
            Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");
            collapseKernel=shader.FindKernel("EvaluateCollapse");int k=collapseKernel;
            shader.SetVector("_CameraClip",Vector4.zero);
            shader.SetBuffer(k,"_LiveChannels",liveChannelsBuffer);shader.SetBuffer(k,"_SourceE",sourceEBuffer);
            shader.SetBuffer(k,"_Static",staticBuffer);shader.SetBuffer(k,"_UV",uvBuffer);
            shader.SetBuffer(k,"_Values",values);
            if(hasImageGeometry)shader.SetBuffer(k,"_ImageShape",imageShapeBuffer);
            shader.SetBuffer(k,"_CollapseTexture",texture);
        }
        internal void DispatchCollapseSpan(GraphicsBuffer target, ComputeBuffer map, int count)
        {
            int k=collapseKernel;
            shader.SetBuffer(k,"_Vertices",target);shader.SetBuffer(k,"_CollapseMap",map);
            shader.SetInt("_CollapseCount",count);
            shader.Dispatch(k,(count+63)/64,1,1);
        }
        internal void DispatchCollapse(GraphicsBuffer target, ComputeBuffer map, ComputeBuffer texture, int count)
        {
            Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");
            int k=shader.FindKernel("EvaluateCollapse");
            shader.SetBuffer(k,"_LiveChannels",liveChannelsBuffer);shader.SetBuffer(k,"_SourceE",sourceEBuffer);
            shader.SetBuffer(k,"_Static",staticBuffer);shader.SetBuffer(k,"_UV",uvBuffer);
            shader.SetBuffer(k,"_Values",values);shader.SetBuffer(k,"_Vertices",target);
            if(hasImageGeometry)shader.SetBuffer(k,"_ImageShape",imageShapeBuffer);
            shader.SetBuffer(k,"_CollapseMap",map);shader.SetBuffer(k,"_CollapseTexture",texture);
            shader.SetInt("_CollapseCount",count);
            shader.Dispatch(k,(count+63)/64,1,1);
        }
        // 연구 후보: 현재 프레임의 모든 입력을 명시적으로 기록한다.
        // 공유 ComputeShader의 즉시 설정 상태나 이전 CommandBuffer 실행에 의존하지 않는다.
        internal void RecordLiveEvaluation(CommandBuffer commands, Vector4 camera, double time)
        {
            Require(UseLiveMode && hasLiveKernel, "single submission requires native live kernel");
            commands.SetComputeBufferParam(shader,liveKernel,"_LiveChannels",liveChannelsBuffer);
            commands.SetComputeBufferParam(shader,liveKernel,"_SourceE",sourceEBuffer);
            commands.SetComputeBufferParam(shader,liveKernel,"_Static",staticBuffer);
            commands.SetComputeBufferParam(shader,liveKernel,"_UV",uvBuffer);
            commands.SetComputeBufferParam(shader,liveKernel,"_Values",values);
            commands.SetComputeBufferParam(shader,liveKernel,"_Vertices",vertices);
            if(hasImageGeometry)commands.SetComputeBufferParam(shader,liveKernel,"_ImageShape",imageShapeBuffer);
            commands.SetComputeFloatParam(shader,"_Pitch",data.Pitch);
            commands.SetComputeFloatParam(shader,"_TweenScale",data.TweenScale);
            commands.SetComputeIntParam(shader,"_Count",data.Items.Length);
            commands.SetComputeIntParam(shader,"_UseAnchors",1);
            commands.SetComputeIntParam(shader,"_UseLiveChannels",1);
            commands.SetComputeVectorParam(shader,"_Camera",camera);
            float hi=(float)time;
            commands.SetComputeFloatParam(shader,"_TimeHi",hi);
            commands.SetComputeFloatParam(shader,"_TimeLo",(float)(time-hi));
            commands.DispatchCompute(shader,liveKernel,(data.Items.Length+63)/64,1,1);
        }
        internal (float[] values,Vertex[] vertices) Dispatch(double time,bool read)
        {
            Bind();
            shader.SetInt("_UseAnchors",UseAnchors?1:0);
            shader.SetInt("_UseLiveChannels", 0);
            // One upload for source progress. Static geometry/segments are never uploaded here.
            if(UseAnchors && data.Sources.Length>0)
            {
                for(int i=0;i<data.Sources.Length;i++) sourceProgress[i]=data.Sources[i].Progress;
                progress.SetData(sourceProgress, 0, 0, data.Sources.Length);
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
