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
using Vertex = StutterFix.GpuDecoGpu.Vertex;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

// Independent Editor fixture. It never opens a chart or invokes the game.
public static partial class GpuDecoLab
{
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
    public static void Anchors() => Run(AnchorsBody, "anchors");
    public static void Rotations() => Run(() => ProbeRotation(Shader()), "rotations");
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
    sealed class Evaluation : GpuDecoGpu
    { internal Evaluation(ComputeShader shader,GpuDecoData data) : base(shader,data) { } }
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
    static bool Compare(GpuDecoData data,float[] values,Vertex[] vertices,bool real,bool anchored=true,bool requireSynthetic=true)
    {
        var sums=new double[16]; var max=new double[16]; var pixelMax=new double[16]; var pixelSum=new double[16]; var over=new int[16]; double vertexMax=0,vertexSum=0,tableMax=0,tableSum=0,colorMax=0,colorSum=0; int vertexOver=0,colorOver=0;
        var sourcePhase=new double[data.Sources.Length]; var sourceSeen=new bool[data.Sources.Length];
        var phaseMax=new double[16]; var phaseSum=new double[16]; var phaseCount=new int[16]; var phaseOver=new int[16];
        double logicalGeometryMax=0,gpuGeometryMax=0; int logicalGeometryOver=0,gpuGeometryOver=0,gpuColorOver=0;
        for(int i=0;i<data.Items.Length;i++)
        {
            var d=data.Items[i]; var eval=new float[16];
            var logicalWorld=CpuVertices(data,d,d.Actual);
            for(int ch=0;ch<16;ch++)
            {
                eval[ch]=values[i*16+ch]; double error=Math.Abs((double)eval[ch]-d.Actual[ch]); Require(DecoBakeCore.Finite(error),"nonfinite channel");
                max[ch]=Math.Max(max[ch],error); sums[ch]+=error;
                double tableError=Math.Abs((double)eval[ch]-data.Eval(d,ch,Ease,anchored)); tableMax=Math.Max(tableMax,tableError); tableSum+=tableError;
                bool phaseAccepted=false; var anchor=d.Anchors[ch];
                var phaseSegment=anchor.Segment; float phasePosition=0; int phaseClock=-1;
                bool phaseCandidate=anchored && anchor.Valid && anchor.Segment.Duration>0;
                if(phaseCandidate) { phasePosition=data.Sources[anchor.Clock].Progress; phaseClock=anchor.Clock; }
                else if(!anchored)
                {
                    int n=DecoBakeCore.Find(d.Tracks[ch],data.BakeTime);
                    if(n>=0) { phaseSegment=d.Tracks[ch][n]; phasePosition=(float)((data.BakeTime-phaseSegment.T0)/data.Pitch*data.TweenScale); phaseCandidate=phaseSegment.Duration>0; }
                }
                if(phaseCandidate)
                {
                    var s=phaseSegment; float t=phasePosition;
                    // Local value/speed conversion is diagnostic, not a clock fit.
                    float h=Math.Min(.0001f,s.Duration*.001f), a=Math.Max(0,t-h), b=Math.Min(s.Duration,t+h);
                    double speed=b>a ? Math.Abs((double)(s.Target-s.Start)*(Ease(s.Ease,b,s.Duration,s.Over,s.Period)-Ease(s.Ease,a,s.Duration,s.Over,s.Period))/(b-a)) : 0;
                    if(t>0 && t<s.Duration && speed>1e-6)
                    {
                        double seconds=data.MeanProgressStep>0 ? error/speed*data.MeanFrameSeconds/data.MeanProgressStep : double.PositiveInfinity;
                        phaseMax[ch]=Math.Max(phaseMax[ch],seconds*1000); phaseSum[ch]+=seconds*1000; phaseCount[ch]++;
                        // A motion smaller than the value tolerance per frame is
                        // stationary for this gate, even if its derivative is nonzero.
                        bool fast=false; float frameChange=(float)(speed*data.MeanProgressStep);
                        if(ch>=7 && ch<=11) fast=frameChange>1f/255;
                        else if(frameChange>0)
                        {
                            var shifted=(float[])d.Actual.Clone(); shifted[ch]+=frameChange;
                            var shiftedWorld=CpuVertices(data,d,shifted);
                            fast=Enumerable.Range(0,4).Max(k=>Vector3.Distance(logicalWorld[k],shiftedWorld[k]))*data.PixelsPerWorld>.01;
                        }
                        if(phaseClock>=0 && fast) { sourceSeen[phaseClock]=true; sourcePhase[phaseClock]=Math.Max(sourcePhase[phaseClock],seconds); }
                        phaseAccepted=data.FrameSamples>0 && fast;
                        if(seconds>data.MeanFrameSeconds) phaseOver[ch]++;
                    }
                }
                // Convert each geometric channel's error to the maximum sprite-corner
                // displacement, retaining the actual values in all other channels.
                if(ch>=7 && ch<=11) { if(error>1.0/255 && !phaseAccepted) over[ch]++; }
                else
                {
                    var perturbed=(float[])d.Actual.Clone(); perturbed[ch]=eval[ch]; var world=CpuVertices(data,d,perturbed);
                    double px=Enumerable.Range(0,4).Max(k=>Vector3.Distance(logicalWorld[k],world[k]))*data.PixelsPerWorld;
                    Require(DecoBakeCore.Finite(px),"nonfinite projected channel"); pixelMax[ch]=Math.Max(pixelMax[ch],px); pixelSum[ch]+=px; if(px>.01 && !phaseAccepted) over[ch]++;
                }
            }
            var evaluatedWorld=CpuVertices(data,d,eval);
            for(int k=0;k<4;k++)
            {
                double geometry=Vector3.Distance(logicalWorld[k],V(d.World,k*3))*data.PixelsPerWorld;
                logicalGeometryMax=Math.Max(logicalGeometryMax,geometry); if(geometry>.01) logicalGeometryOver++;
                geometry=Vector3.Distance(evaluatedWorld[k],vertices[i*4+k].position)*data.PixelsPerWorld;
                gpuGeometryMax=Math.Max(gpuGeometryMax,geometry); if(geometry>.01) gpuGeometryOver++;
                double error=Vector3.Distance(vertices[i*4+k].position,V(d.World,k*3))*data.PixelsPerWorld;
                Require(DecoBakeCore.Finite(error),"nonfinite vertex"); vertexMax=Math.Max(vertexMax,error); vertexSum+=error; if(error>.01) vertexOver++;
                var actual=vertices[i*4+k].color;
                for(int ch=0;ch<4;ch++) { error=Math.Abs((double)actual[ch]-d.Color[ch]); Require(DecoBakeCore.Finite(error),"nonfinite color"); colorMax=Math.Max(colorMax,error); colorSum+=error; if(error>1.0/255) colorOver++; if(Math.Abs((double)actual[ch]-(ch==3?eval[10]*eval[11]:eval[7+ch]))>1.0/255) gpuColorOver++; }
            }
        }
        for(int ch=0;ch<16;ch++) Log("[gpudeco] G0 t="+N(data.RawTime)+" ch="+(ch==4?"rot":ch==5||ch==6?"scale":ch>=7&&ch<=11?"color":"pos")+" slot="+ch+" max="+N(max[ch])+" mean="+N(sums[ch]/data.Items.Length)+" max_px="+N(pixelMax[ch])+" mean_px="+N(pixelSum[ch]/data.Items.Length)+" over_tol="+over[ch]+" raw_units=True source="+(real?"game-export":"synthetic"));
        Log("[gpudeco-lab] table samples="+(data.Items.Length*16)+" max="+N(tableMax)+" mean="+N(tableSum/(data.Items.Length*16)));
        Log("[gpudeco-lab] vertices samples="+(data.Items.Length*4)+" max_px="+N(vertexMax)+" mean_px="+N(vertexSum/(data.Items.Length*4))+" over_tol="+vertexOver);
        Log("[gpudeco-lab] renderer_color samples="+(data.Items.Length*16)+" max="+N(colorMax)+" mean="+N(colorSum/(data.Items.Length*16))+" over_tol="+colorOver);
        Log("[gpudeco-lab] geometry actual_fields_vs_sprite_max_px="+N(logicalGeometryMax)+" over_tol="+logicalGeometryOver+" gpu_vs_evaluated_fields_max_px="+N(gpuGeometryMax)+" over_tol="+gpuGeometryOver+" gpu_color_formula_over_tol="+gpuColorOver);
        for(int ch=0;ch<16;ch++) Log("[gpudeco] phase slot="+ch+" samples="+phaseCount[ch]+" max_ms="+N(phaseMax[ch])+" mean_ms="+(phaseCount[ch]>0?N(phaseSum[ch]/phaseCount[ch]):"NA")+" over_frame="+phaseOver[ch]+" mode="+(anchored?"anchored":"table"));
        int activeSources=sourceSeen.Count(s=>s), phaseSources=Enumerable.Range(0,sourceSeen.Length).Count(i=>sourceSeen[i] && data.FrameSamples>0 && sourcePhase[i]<=data.MeanFrameSeconds);
        Log("[gpudeco] phase_sources active="+activeSources+" within_frame="+phaseSources+" frame_mean_ms="+N(data.MeanFrameSeconds*1000)+" frame_samples="+data.FrameSamples+" fallback_slots="+data.Items.Sum(d=>d.Anchors.Count(a=>!a.Valid)));
        bool phasePass=activeSources==0 || (data.FrameSamples>0 && phaseSources*100L>=activeSources*99L);
        // Each stationary/unanchored channel retains its value tolerance. Moving
        // channels use the source phase gate; total vertex/pixel errors stay visible.
        bool pass=over.Sum()==0 && phasePass && logicalGeometryOver==0 && gpuGeometryOver==0 && gpuColorOver==0
            && (real && anchored && data.Schema>=2 || vertexOver==0 && colorOver==0);
        if(!real && requireSynthetic) Require(pass,"synthetic vertex/value mismatch");
        return pass;
    }
    static void VerifyBody()
    {
        AuditBody();
    }
    static void AnchorsBody()
    {
        var data=Synthetic(2000); data.Frame=1000; data.MeanProgressStep=data.MeanFrameSeconds=1f/120; data.FrameSamples=256;
        data.Sources=Enumerable.Range(0,257).Select(i=> {
            float progress=0; for(int f=0;f<i%100;f++) progress+=(f%3==0?.0081f:.0084f);
            return new GpuDecoData.SourceClock { Id=100+i,StartFrame=900-i,StartSong=268+i*.001,Progress=progress };
        }).ToArray();
        for(int i=0;i<data.Items.Length;i++)
        {
            var d=data.Items[i];
            for(int ch=0;ch<16;ch++)
            {
                int clock=i%data.Sources.Length; float start=d.Initial[ch], target=start+(ch==4?12:.2f);
                var s=new DecoBakeCore.Segment { Duration=1,Start=start,Target=target,End=target,Ease=1+i%31,Over=1.70158f,IsScale=ch==5||ch==6||ch==12||ch==13 };
                if(s.IsScale) s.End=start+(target-start)*Ease(s.Ease,1,1,s.Over,s.Period);
                if(i%17==0) s.Duration=0;
                d.Anchors[ch]=new GpuDecoData.Anchor { Valid=true,Clock=s.Duration==0?-1:clock,Segment=s };
                float t=data.Sources[clock].Progress;
                d.Actual[ch]=s.Duration==0?s.End:t<=0?start:start+(target-start)*Ease(s.Ease,t,1,s.Over,s.Period);
            }
            var world=CpuVertices(data,d,d.Actual); for(int k=0;k<4;k++) { d.World[k*3]=world[k].x; d.World[k*3+1]=world[k].y; d.World[k*3+2]=world[k].z; }
            d.Color=new[]{d.Actual[7],d.Actual[8],d.Actual[9],d.Actual[10]*d.Actual[11]};
        }
        using(var stream=new MemoryStream())
        {
            data.Write(stream); stream.Position=0; data=GpuDecoData.Read(stream);
            Log("[gpudeco-lab] anchor_schema_roundtrip version="+data.Schema+" bytes="+stream.Length+" sources="+data.Sources.Length);
        }
        using(var gpu=new Evaluation(Shader(),data))
        {
            var result=gpu.Dispatch(data.BakeTime,true); Compare(data,result.values,result.vertices,false);
            gpu.UseAnchors=false; var baseline=gpu.Dispatch(data.BakeTime,true);
            Log("[gpudeco-lab] comparison_mode=synthetic_before_anchor_negative_control");
            Require(!Compare(data,baseline.values,baseline.vertices,false,false,false),"song-clock negative control unexpectedly passed");
            gpu.UseAnchors=true;
            var same=gpu.Dispatch(data.BakeTime+30,true);
            Require(result.values.SequenceEqual(same.values),"paused source values changed with song clock");
            int clock=37; float old=data.Sources[clock].Progress; data.Sources[clock].Progress+=.007f;
            var next=gpu.Dispatch(data.BakeTime,true); int changed=0,foreign=0;
            for(int i=0;i<data.Items.Length;i++) for(int ch=0;ch<16;ch++) if(result.values[i*16+ch]!=next.values[i*16+ch]) { changed++; if(data.Items[i].Anchors[ch].Clock!=clock) foreign++; }
            Require(changed>0 && foreign==0,"source progress upload did not isolate source");
            data.Sources[clock].Progress=old;
            // Truth fields are comparison outputs, never GPU evaluation inputs.
            data.Items[0].Actual[0]+=100; var independent=gpu.Dispatch(data.BakeTime,true);
            Require(result.values.SequenceEqual(independent.values),"Actual truth leaked into GPU input");
            Log("[gpudeco-lab] anchors sources=257 channels=32000 vertices=8000 paused_clock_invariance=True changed_source_channels="+changed+" foreign_changes="+foreign+" truth_input_independent=True");
            MeasureAnchorSubmit(data,gpu);
        }
        data.Sources=Enumerable.Range(0,2000).Select(i=>data.Sources[i%257]).ToArray();
        using(var gpu=new Evaluation(Shader(),data)) MeasureAnchorSubmit(data,gpu);
        if(Environment.GetCommandLineArgs().Contains("-gpudecoInput"))
        {
            try { AuditBody(); throw new InvalidOperationException("legacy gate negative control unexpectedly accepted"); }
            catch(GateFailure ex) when(ex.Message.Contains("v2_real_anchor_export_required"))
            { Log("[gpudeco-lab] legacy_source_gate=PASS expected_refusal="+ex.Message+" real_G0_evaluated=False"); }
        }
        Log("GPUDECO_ANCHORS=PASS synthetic=True real_anchor_export=False game_G0=UNMEASURED game_G1=UNMEASURED");
    }
    static void MeasureAnchorSubmit(GpuDecoData data,Evaluation gpu)
    {
        for(int i=0;i<20;i++) gpu.Dispatch(data.BakeTime,false);
        var timer=Stopwatch.StartNew(); for(int i=0;i<500;i++) gpu.Dispatch(data.BakeTime,false); timer.Stop();
        Log("[gpudeco-lab] anchor_cost source_upload_count="+data.Sources.Length+" upload_bytes="+(data.Sources.Length*4)+" frames=500 dispatches=1 cpu_submit_ms="+N(timer.Elapsed.TotalMilliseconds/500)+" gpu_timestamp_ms=UNAVAILABLE synthetic=True copy_runs=0");
    }
    static void ProbeRotation(ComputeShader shader)
    {
        var angles=Enumerable.Range(0,8193).Select(i=>-720f+i*(1440f/8192))
            .Concat(new[]{-540f,-180f,0f,180f,540f,179.99998f,180.00002f}).ToArray();
        int kernel=shader.FindKernel("RotationProbe"); double maximum=0,sum=0;
        using(var input=new ComputeBuffer(angles.Length,16)) using(var output=new ComputeBuffer(angles.Length,16))
        {
            input.SetData(angles.Select(a=>new Vector4(a,0,0,0)).ToArray());
            shader.SetBuffer(kernel,"_Probe",input); shader.SetBuffer(kernel,"_RotationOut",output); shader.SetInt("_Count",angles.Length);
            shader.Dispatch(kernel,(angles.Length+63)/64,1,1); var result=new Vector4[angles.Length]; output.GetData(result);
            for(int i=0;i<angles.Length;i++) {
                var q=Quaternion.Normalize(Quaternion.Euler(0,0,angles[i]));
                if(angles[i]==90 || angles[i]==180 || angles[i]==270) Log("[gpudeco-lab] rotation_endpoint angle="+N(angles[i])+" cpu_q="+q.ToString("G9")+" gpu_qz_qw_sn_cs="+result[i].ToString("G9"));
                double error=Math.Max(Math.Abs((double)q.w*(q.z*2)-result[i].z),Math.Abs((double)(1-q.z*(q.z*2))-result[i].w));
                maximum=Math.Max(maximum,error); sum+=error;
            }
        }
        Log("[gpudeco-lab] rotation_matrix samples="+angles.Length+" range_degrees=-720..720 max="+N(maximum)+" mean="+N(sum/angles.Length));
        Require(maximum<2e-6,"rotation coefficient regression");
        foreach(float angle in new[]{0f,90f,180f,270f,-180f})
        {
            var data=Synthetic(1); var d=data.Items[0];
            d.Flags=0; d.Constants=new[]{1f,1f,1f,1f,angle,0f,0f,0f};
            d.Initial=new[]{579.417358398f,2193.05566406f,0f,-1.5f,0f,1000f,8f,1f,1f,1f,1f,.6f,1f,.999f,0f,0f};
            d.Actual=(float[])d.Initial.Clone(); for(int ch=0;ch<16;ch++) d.Tracks[ch]=Array.Empty<DecoBakeCore.Segment>();
            // Amplify the observed half-angle error with a five-unit half-width.
            d.Shape=new[]{-5f,-.5f,0f,5f,-.5f,0f,-5f,.5f,0f,5f,.5f,0f};
            var world=CpuVertices(data,d,d.Actual); for(int k=0;k<4;k++) {d.World[k*3]=world[k].x;d.World[k*3+1]=world[k].y;d.World[k*3+2]=world[k].z;}
            d.Color=new[]{1f,1f,1f,.6f};
            using(var gpu=new Evaluation(shader,data)) { var result=gpu.Dispatch(data.BakeTime,true); DiagnoseResidual(data,result.values,result.vertices); Compare(data,result.values,result.vertices,false); }
            Log("[gpudeco-lab] wide_sprite_rotation angle="+N(angle)+" scale_x=1000 half_width=5 PASS");
        }
        Log("GPUDECO_ROTATION=PASS synthetic=True");
    }
    static void BundleBody()
    {
        string output=Path.Combine(Evidence,"bundle"); Directory.CreateDirectory(output);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="stutterfix_gpudeco",assetNames=new[]{"Assets/Shaders/GpuDeco.compute"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        Require(manifest,"bundle build failed");
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"stutterfix_gpudeco")); Require(bundle,"bundle load failed");
        try { var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute"); Probe(shader); ProbeRotation(shader); } finally { bundle.Unload(true); }
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
