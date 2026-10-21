#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using StutterFix;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

// Independent Editor fixture. It never opens a chart or invokes the game.
public static partial class GpuDecoLab
{
    [StructLayout(LayoutKind.Sequential)]
    struct Segment { public float hi, lo, duration, start, end, target, over, period; public uint ease, scale; }
    [StructLayout(LayoutKind.Sequential)]
    struct Track { public uint first, count; public float initial; public uint pad; }
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex { public Vector3 position; public Color color; public Vector2 uv; }
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly List<string> report = new List<string>();
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
    static string Evidence => Path.Combine(Root, "evidence");
    static void Log(string s) { report.Add(s); Debug.Log(s); }
    static float Ease(int id, float t, float d, float over, float period) => EaseManager.Evaluate((DG.Tweening.Ease)id, null, t, d, over, period);
    static bool Supported(int id) => id >= 0 && id <= 31 || id == 36;
    static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    static string N(double v) => v.ToString("G12", Inv);
    sealed class GateFailure : Exception { internal GateFailure(string verdict) : base(verdict) { } }
    static void Run(Action action, string name)
    {
        Directory.CreateDirectory(Evidence); report.Clear(); int code = 0;
        try
        {
            Require(Application.unityVersion == "6000.3.10f1", "wrong Editor: " + Application.unityVersion);
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SystemInfo.supportsComputeShaders, "D3D11 compute unavailable");
            Log("[gpudeco-lab] editor=" + Application.unityVersion + " device=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType);
            action();
        }
        catch (GateFailure ex) { code = 1; Log(ex.Message); }
        catch (Exception ex) { code = 1; Log("GPUDECO_G1=ERROR stage=lab reason=" + ex); }
        finally { File.WriteAllLines(Path.Combine(Evidence, name + ".txt"), report); }
        EditorApplication.Exit(code);
    }
    public static void Smoke() => Run(SmokeBody, "smoke");
    public static void Verify() => Run(VerifyBody, "verify");
    public static void Bundle() => Run(BundleBody, "bundle");
    public static void Runs() => Run(RunsBody, "runs");
    static ComputeShader Shader() => AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/GpuDeco.compute");
    static void SmokeBody()
    {
        Probe(Shader());
        var data = Synthetic(2000);
        using (var gpu = new Evaluation(Shader(), data))
        {
            var result = gpu.Dispatch(data.BakeTime, true);
            Compare(data, result.values, result.vertices, false);
            Draw(gpu.Mesh);
            // One raw vertex-buffer writer. No per-frame upload or Transform writes.
            for (int n=0;n<20;n++) gpu.Dispatch(data.BakeTime, false);
            var timer = Stopwatch.StartNew();
            for(int n=0;n<200;n++) gpu.Dispatch(data.BakeTime + n*.0001, false);
            timer.Stop();
            double submit = timer.Elapsed.TotalMilliseconds/200;
            timer.Restart(); gpu.Dispatch(data.BakeTime,true); timer.Stop();
            Log("[gpudeco-lab] cost decorations=2000 segments=" + gpu.SegmentCount + " dispatches_per_frame=1 cpu_submit_ms=" + N(submit)
                + " sync_readback_ms=" + N(timer.Elapsed.TotalMilliseconds) + " gpu_timestamp_ms=UNAVAILABLE runs=1 synthetic=True");
        }
        Log("GPUDECO_LAB_SMOKE=PASS real_export=False G0=UNMEASURED G1=UNMEASURED");
    }
    static void Probe(ComputeShader shader)
    {
        Require(shader, "compute import failed");
        var input = new List<Vector4>(); var ids = new List<uint>(); var expected = new List<float>();
        foreach(int id in Enumerable.Range(0,32).Concat(new[]{36}))
        foreach (var setting in new[] { new Vector3(.125f,.3f,0), new Vector3(3.7f,1.70158f,0), new Vector3(8f,2.3f,.7f) })
        for(int k=0;k<=1024;k++)
        {
            float t=setting.x*k/1024f;
            input.Add(new Vector4(t,setting.x,setting.y,setting.z)); ids.Add((uint)id); expected.Add(Ease(id,t,setting.x,setting.y,setting.z));
        }
        using(var p=new ComputeBuffer(input.Count,16)) using(var e=new ComputeBuffer(input.Count,4)) using(var o=new ComputeBuffer(input.Count,4))
        {
            p.SetData(input); e.SetData(ids); int kernel=shader.FindKernel("EaseProbe");
            shader.SetBuffer(kernel,"_Probe",p); shader.SetBuffer(kernel,"_ProbeIds",e); shader.SetBuffer(kernel,"_ProbeOut",o);
            shader.SetInt("_Count",input.Count); shader.Dispatch(kernel,(input.Count+63)/64,1,1);
            var actual = new float[input.Count]; o.GetData(actual); double maximum=0, sum=0; int over=0;
            for(int i=0;i<actual.Length;i++) { double error=Math.Abs((double)actual[i]-expected[i]); Require(DecoBakeCore.Finite(error),"nonfinite ease id="+ids[i]); maximum=Math.Max(maximum,error); sum+=error; if(error>2e-5) over++; }
            Log("[gpudeco-lab] ease samples="+actual.Length+" max="+N(maximum)+" mean="+N(sum/actual.Length)+" over_2e-5="+over+" supported=0..31,36 fallback=Flash,custom");
            foreach(uint id in ids.Distinct())
            {
                var indices=Enumerable.Range(0,actual.Length).Where(i=>ids[i]==id).ToArray();
                int worst=indices.OrderByDescending(i=>Math.Abs((double)actual[i]-expected[i])).First();
                Log("[gpudeco-lab] ease_id="+id+" max="+N(Math.Abs((double)actual[worst]-expected[worst]))
                    +" mean="+N(indices.Average(i=>Math.Abs((double)actual[i]-expected[i])))
                    +" over_2e-5="+indices.Count(i=>Math.Abs((double)actual[i]-expected[i])>2e-5)
                    +" worst_input="+input[worst].ToString("G9")+" cpu="+N(expected[worst])+" gpu="+N(actual[worst]));
            }
            Require(over==0,"ease probe tolerance exceeded");
        }
    }
    sealed class Evaluation : IDisposable
    {
        readonly ComputeShader shader; readonly GpuDecoData data; readonly int kernel;
        readonly List<IDisposable> resources = new List<IDisposable>();
        ComputeBuffer values; GraphicsBuffer vertices;
        internal Mesh Mesh; internal int SegmentCount;
        internal GraphicsBuffer VertexBuffer => vertices;
        ComputeBuffer Buffer<T>(IList<T> source, int stride) where T:struct
        {
            var b = new ComputeBuffer(Math.Max(1,source.Count),stride); resources.Add(b);
            if(source.Count>0) b.SetData(source.ToArray()); return b;
        }
        internal Evaluation(ComputeShader shader,GpuDecoData data)
        {
            this.shader=shader; this.data=data; kernel=shader.FindKernel("Evaluate");
            try
            {
                var segments=new List<Segment>(); var tracks=new List<Track>(); var shape=new List<Vector4>(); var uv=new List<Vector2>(); var triangles=new List<int>();
                for(int i=0;i<data.Items.Length;i++)
                {
                    var d=data.Items[i];
                    for(int ch=0;ch<16;ch++)
                    {
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
                shader.SetBuffer(kernel,"_Static",Buffer(shape,16)); shader.SetBuffer(kernel,"_UV",Buffer(uv,8));
                values=new ComputeBuffer(data.Items.Length*16,4); resources.Add(values); shader.SetBuffer(kernel,"_Values",values);
                Mesh=new Mesh { name="GpuDecoLab raw",indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw };
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
            float hi=(float)time; shader.SetFloat("_TimeHi",hi); shader.SetFloat("_TimeLo",(float)(time-hi));
            shader.Dispatch(kernel,(data.Items.Length+63)/64,1,1);
            if(!read) return (null,null);
            var channels=new float[data.Items.Length*16]; var points=new Vertex[data.Items.Length*4]; values.GetData(channels); vertices.GetData(points); return (channels,points);
        }
        public void Dispose() { for(int n=resources.Count-1;n>=0;n--) resources[n].Dispose(); resources.Clear(); if(Mesh) UnityEngine.Object.DestroyImmediate(Mesh); Mesh=null; }
    }
    static Vector3 V(float[] a,int at) => new Vector3(a[at],a[at+1],a[at+2]);
    static Vector3[] CpuVertices(GpuDecoData data,GpuDecoData.Item d,float[] v)
    {
        var c=d.Constants; var ts=new Vector2(c[0],c[1]); var camScale=((d.Flags&1)!=0 ? data.Camera[3] : 1)*ts;
        var position=new Vector2(v[0],v[1]); var world=(new Vector2(data.Camera[0],data.Camera[1])-position)*new Vector2(v[12],v[13])+position;
        if((d.Flags&4)!=0) world.x=position.x; if((d.Flags&8)!=0) world.y=position.y;
        world+=new Vector2(v[14],v[15])*camScale/new Vector2(c[2],c[3]);
        var offset=(new Vector2(v[2],v[3])+new Vector2(c[5],c[6]))/ts;
        var translated=V(d.OffsetBasis,0)+V(d.OffsetBasis,3)*offset.x+V(d.OffsetBasis,6)*offset.y;
        var scale=new Vector2(v[5],v[6])*camScale;
        var rotation=Quaternion.Normalize(Quaternion.Euler(0,0,c[4]+v[4]+((d.Flags&2)!=0 ? data.Camera[2] : 0)));
        return Enumerable.Range(0,4).Select(n=> { var local=V(d.Shape,n*3)+translated; return new Vector3(world.x,world.y,c[7])+rotation*new Vector3(local.x*scale.x,local.y*scale.y,local.z); }).ToArray();
    }
    static bool Compare(GpuDecoData data,float[] values,Vertex[] vertices,bool real)
    {
        var sums=new double[16]; var max=new double[16]; var pixelMax=new double[16]; var pixelSum=new double[16]; var over=new int[16]; double vertexMax=0,vertexSum=0,tableMax=0,tableSum=0,colorMax=0,colorSum=0; int vertexOver=0,colorOver=0;
        for(int i=0;i<data.Items.Length;i++)
        {
            var d=data.Items[i]; var eval=new float[16];
            var table=new DecoBakeCore.Table { Decorations=new[]{new DecoBakeCore.Decoration{Initial=d.Initial}},Tracks=d.Tracks,Pitch=data.Pitch,TweenScale=data.TweenScale,Ease=Ease };
            var logicalWorld=CpuVertices(data,d,d.Actual);
            for(int ch=0;ch<16;ch++)
            {
                eval[ch]=values[i*16+ch]; double error=Math.Abs((double)eval[ch]-d.Actual[ch]); Require(DecoBakeCore.Finite(error),"nonfinite channel");
                max[ch]=Math.Max(max[ch],error); sums[ch]+=error;
                double tableError=Math.Abs((double)eval[ch]-table.Eval(0,ch,data.BakeTime)); tableMax=Math.Max(tableMax,tableError); tableSum+=tableError;
                // Convert each geometric channel's error to the maximum sprite-corner
                // displacement, retaining the actual values in all other channels.
                if(ch>=7 && ch<=11) { if(error>1.0/255) over[ch]++; }
                else
                {
                    var perturbed=(float[])d.Actual.Clone(); perturbed[ch]=eval[ch]; var world=CpuVertices(data,d,perturbed);
                    double px=Enumerable.Range(0,4).Max(k=>Vector3.Distance(logicalWorld[k],world[k]))*data.PixelsPerWorld;
                    Require(DecoBakeCore.Finite(px),"nonfinite projected channel"); pixelMax[ch]=Math.Max(pixelMax[ch],px); pixelSum[ch]+=px; if(px>.01) over[ch]++;
                }
            }
            for(int k=0;k<4;k++)
            {
                double error=Vector3.Distance(vertices[i*4+k].position,V(d.World,k*3))*data.PixelsPerWorld;
                Require(DecoBakeCore.Finite(error),"nonfinite vertex"); vertexMax=Math.Max(vertexMax,error); vertexSum+=error; if(error>.01) vertexOver++;
                var actual=vertices[i*4+k].color;
                for(int ch=0;ch<4;ch++) { error=Math.Abs((double)actual[ch]-d.Color[ch]); Require(DecoBakeCore.Finite(error),"nonfinite color"); colorMax=Math.Max(colorMax,error); colorSum+=error; if(error>1.0/255) colorOver++; }
            }
        }
        for(int ch=0;ch<16;ch++) Log("[gpudeco] G0 t="+N(data.RawTime)+" ch="+(ch==4?"rot":ch==5||ch==6?"scale":ch>=7&&ch<=11?"color":"pos")+" slot="+ch+" max="+N(max[ch])+" mean="+N(sums[ch]/data.Items.Length)+" max_px="+N(pixelMax[ch])+" mean_px="+N(pixelSum[ch]/data.Items.Length)+" over_tol="+over[ch]+" raw_units=True source="+(real?"game-export":"synthetic"));
        Log("[gpudeco-lab] table samples="+(data.Items.Length*16)+" max="+N(tableMax)+" mean="+N(tableSum/(data.Items.Length*16)));
        Log("[gpudeco-lab] vertices samples="+(data.Items.Length*4)+" max_px="+N(vertexMax)+" mean_px="+N(vertexSum/(data.Items.Length*4))+" over_tol="+vertexOver);
        Log("[gpudeco-lab] renderer_color samples="+(data.Items.Length*16)+" max="+N(colorMax)+" mean="+N(colorSum/(data.Items.Length*16))+" over_tol="+colorOver);
        bool pass=vertexOver==0 && colorOver==0 && over.Sum()==0;
        if(!real) Require(pass,"synthetic vertex/value mismatch");
        return pass;
    }
    static void VerifyBody()
    {
        AuditBody();
    }
    static void BundleBody()
    {
        string output=Path.Combine(Evidence,"bundle"); Directory.CreateDirectory(output);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="stutterfix_gpudeco",assetNames=new[]{"Assets/Shaders/GpuDeco.compute"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        Require(manifest,"bundle build failed");
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"stutterfix_gpudeco")); Require(bundle,"bundle load failed");
        try { Probe(bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute")); } finally { bundle.Unload(true); }
        Log("GPUDECO_BUNDLE=PASS editor="+Application.unityVersion+" path="+output);
    }
    static void RunsBody()
    {
        var shader=Shader(); var data=Synthetic(2000); var meshes=new List<Mesh>(); var buffers=new List<GraphicsBuffer>();
        using(var gpu=new Evaluation(shader,data)) using(var commands=new CommandBuffer { name="GpuDeco 1000 raw runs" })
        try
        {
            int copy=shader.FindKernel("CopyRun");
            commands.SetComputeBufferParam(shader,copy,"_CopySource",gpu.VertexBuffer);
            commands.SetComputeIntParam(shader,"_CopyVertexCount",8);
            for(int run=0;run<1000;run++)
            {
                var mesh=new Mesh { name="GpuDeco run "+run,indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw };
                meshes.Add(mesh);
                mesh.SetVertexBufferParams(8,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
                mesh.SetVertexBufferData(new Vertex[8],0,0,8);
                mesh.SetIndices(new[]{0,2,1,2,3,1,4,6,5,6,7,5},MeshTopology.Triangles,0,false);
                mesh.bounds=gpu.Mesh.bounds;
                mesh.SetSubMesh(0,new SubMeshDescriptor(0,12,MeshTopology.Triangles) { bounds=mesh.bounds,firstVertex=0,vertexCount=8 },MeshUpdateFlags.DontRecalculateBounds);
                mesh.UploadMeshData(true);
                var buffer=mesh.GetVertexBuffer(0); buffers.Add(buffer);
                commands.SetComputeIntParam(shader,"_CopyFirstVertex",run*8);
                commands.SetComputeBufferParam(shader,copy,"_Vertices",buffer);
                commands.DispatchCompute(shader,copy,1,1,1);
            }
            // Validate actual independent Mesh buffers against the combined writer.
            var evaluated=gpu.Dispatch(data.BakeTime,true);
            Compare(data,evaluated.values,evaluated.vertices,false);
            var reference=evaluated.vertices;
            Log("[gpudeco-lab] run_reference position="+reference[4].position.ToString("G9")+" color="+reference[4].color.ToString("G9"));
            Graphics.ExecuteCommandBuffer(commands);
            double maximum=0; int mismatches=0;
            for(int run=0;run<1000;run++)
            {
                var result=new Vertex[8]; buffers[run].GetData(result);
                for(int k=0;k<8;k++)
                {
                    var expected=reference[run*8+k]; double error=Vector3.Distance(result[k].position,expected.position);
                    maximum=Math.Max(maximum,error);
                    if(error!=0 || !result[k].color.Equals(expected.color) || !result[k].uv.Equals(expected.uv)) mismatches++;
                }
            }
            Require(mismatches==0,"run copy differs from evaluated raw buffer");
            Log("[gpudeco-lab] runs raw_buffers=1000 decorations=2000 vertices=8000 max_world="+N(maximum)+" mismatches="+mismatches+" readable="+meshes[0].isReadable);
            Draw(gpu.Mesh);
            Draw(meshes.ToArray(),"raw-runs.png");
            for(int n=0;n<20;n++) { gpu.Dispatch(data.BakeTime,false); Graphics.ExecuteCommandBuffer(commands); }
            var timer=Stopwatch.StartNew();
            for(int n=0;n<100;n++) { gpu.Dispatch(data.BakeTime+n*.0001,false); Graphics.ExecuteCommandBuffer(commands); }
            timer.Stop(); double submit=timer.Elapsed.TotalMilliseconds/100;
            timer.Restart(); gpu.Dispatch(data.BakeTime,true); timer.Stop();
            Log("[gpudeco-lab] cost runs=1000 decorations=2000 segments="+gpu.SegmentCount+" dispatches_per_frame=1001 cpu_submit_ms="+N(submit)
                +" sync_readback_ms="+N(timer.Elapsed.TotalMilliseconds)+" gpu_timestamp_ms=UNAVAILABLE command_buffer_prepared=True synthetic=True");
            Log("GPUDECO_LAB_RUNS=PASS real_export=False G0=UNMEASURED G1=UNMEASURED");
        }
        finally { foreach(var buffer in buffers) buffer.Dispose(); foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh); }
    }
    static void Draw(Mesh mesh)
        => Draw(new[]{mesh},"raw-mesh.png");
    static void Draw(Mesh[] meshes,string filename)
    {
        bool oldAsync=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var objects=new List<GameObject>(); var cameraObject=new GameObject("lab camera"); var material=new Material(UnityEngine.Shader.Find("Sprites/Default"));
        var rt=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32); var image=new Texture2D(128,128,TextureFormat.RGBA32,false); var old=RenderTexture.active;
        try
        {
            foreach(var mesh in meshes)
            {
                var go=new GameObject("lab mesh"); objects.Add(go); go.layer=30;
                go.AddComponent<MeshFilter>().sharedMesh=mesh; var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.sortingOrder=objects.Count;
            }
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true; camera.orthographicSize=8;
            camera.transform.position=new Vector3(0,0,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.cullingMask=1<<30; camera.targetTexture=rt;
            camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply();
            int covered=image.GetPixels32().Count(p=>p.r!=0||p.g!=0||p.b!=0);
            File.WriteAllBytes(Path.Combine(Evidence,filename),image.EncodeToPNG()); Log("[gpudeco-lab] raw_mesh_draw runs="+meshes.Length+" covered_pixels="+covered+" vertex_stride=36 shader=Sprites/Default game_shader=False");
            Require(covered>0,"raw mesh draw empty");
        }
        finally { ShaderUtil.allowAsyncCompilation=oldAsync; RenderTexture.active=old; foreach(var go in objects) UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static GpuDecoData Synthetic(int count)
    {
        var data=new GpuDecoData{Fingerprint="SYNTHETIC",RawTime=270,BakeTime=270,Pitch=1,TweenScale=1,PixelsPerWorld=72,Awake=count,Supported=count,Camera=new[]{.1f,.2f,7f,1.1f},Items=new GpuDecoData.Item[count]};
        var random=new System.Random(21821);
        for(int i=0;i<count;i++)
        {
            var d=data.Items[i]=new GpuDecoData.Item{Id=i,Flags=i%16,Placement="Global",Initial=new[]{0f,0f,0f,0f,0f,1f,1f,.8f,.6f,.4f,.7f,1f,.2f,.3f,0f,0f},
                Constants=new[]{1f,1f,1f,1f,5f,.03f,-.02f,0f},Shape=new[]{-.1f,-.1f,0f,.1f,-.1f,0f,-.1f,.1f,0f,.1f,.1f,0f},
                OffsetBasis=new[]{0f,0f,0f,1f,0f,0f,0f,1f,0f},UV=new[]{0f,0f,1f,0f,0f,1f,1f,1f},Triangles=new[]{0,2,1,2,3,1}};
            for(int ch=0;ch<16;ch++)
            {
                d.Tracks[ch]=Array.Empty<DecoBakeCore.Segment>();
                if(ch==0||ch==1||ch==4||ch==5||ch==7)
                {
                    // Identical-T0 replacement, before/after endpoints and scale Target != End are covered.
                    double time=269.123456789 + (i%5==0?1.0:0.0);
                    float target=ch<2?(float)(random.NextDouble()*10-5):ch==4?(float)(random.NextDouble()*360-180):.2f;
                    int ease=i%32;
                    var s=new DecoBakeCore.Segment{T0=time,Duration=1.7f,Start=d.Initial[ch],Target=target,End=target,Over=1.70158f,Period=0,Ease=ease,IsScale=ch==5};
                    var first=s; first.Target=first.End=123;
                    d.Tracks[ch]=new[]{first,s};
                }
                int index=DecoBakeCore.Find(d.Tracks[ch],data.BakeTime);
                d.Actual[ch]=index<0?d.Initial[ch]:DecoBakeCore.Value(d.Tracks[ch][index],data.BakeTime,1,1,Ease);
            }
            var world=CpuVertices(data,d,d.Actual); for(int k=0;k<4;k++) { d.World[k*3]=world[k].x; d.World[k*3+1]=world[k].y; d.World[k*3+2]=world[k].z; }
            d.Color=new[]{d.Actual[7],d.Actual[8],d.Actual[9],d.Actual[10]*d.Actual[11]};
        }
        // Check the diagnostic format using the exact writer/reader used by export.
        using(var memory=new MemoryStream()) { data.Write(memory); memory.Position=0; return GpuDecoData.Read(memory); }
    }
}
#endif
