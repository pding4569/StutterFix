#if UNITY_EDITOR
using System;
using Vertex = StutterFix.GpuDecoGpu.Vertex;
using System.IO;
using System.Linq;
using StutterFix;
using UnityEngine;

public static partial class GpuDecoLab
{
    // Read-only attribution on the exported game truth. Diagnostic clock fits
    // below are oracles, never corrections to the export or acceptance gates.
    public static void Audit() => Run(AuditBody, "real-audit");
    // A game gate can stop before exporting snapshot three. Diagnose those
    // files without relabeling a partial run as the complete three-point gate.
    public static void Residual() => Run(() => ResidualBody(Shader()), "residual");
    static void ResidualBody(ComputeShader shader) {
        var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-gpudecoInput");
        Require(at>=0 && at+1<args.Length,"-gpudecoInput required");
        var files=Directory.GetFiles(args[at+1],"*.gpudeco").OrderBy(x=>x).ToArray();
        Require(files.Length>0,"no residual snapshots");
        bool pass=true;
        foreach(var file in files) {
            GpuDecoData data; using(var stream=File.OpenRead(file)) data=GpuDecoData.Read(stream);
            Require(data.Schema==2,"residual requires actual source clocks");
            Log("[gpudeco-residual] file="+Path.GetFileName(file)+" owned="+data.Items.Length+" awake="+data.Awake);
            using(var gpu=new Evaluation(shader,data)) {
                var result=gpu.Dispatch(data.BakeTime,true);
                bool sample=Compare(data,result.values,result.vertices,true); pass &= sample;
                Log("[gpudeco-residual] snapshot_G0="+(sample?"PASS":"FAIL"));
                DiagnoseResidual(data,result.values,result.vertices);
            }
        }
        Log("GPUDECO_RESIDUAL=MEASURED snapshots="+files.Length+" full_gate=NOT_EVALUATED game_pixels=UNMEASURED");
        Require(pass,"residual snapshot G0 failed");
    }
    static void AuditBody()
    {
        string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-gpudecoInput");
        Require(at >= 0 && at + 1 < args.Length, "-gpudecoInput <export directory> required");
        var files = Directory.GetFiles(args[at + 1], "*.gpudeco").OrderBy(x => x).ToArray();
        Require(files.Length == 3, "exactly three paused snapshots required");
        string fingerprint = null; bool pass = true, auxiliaryPass = true, low = false; int measured = 0, primary = 0;
        foreach (string file in files)
        {
            GpuDecoData data; using (var stream = File.OpenRead(file)) data = GpuDecoData.Read(stream);
            fingerprint = fingerprint ?? data.Fingerprint; Require(data.Fingerprint == fingerprint, "export table changed");
            Log("[gpudeco-lab] export=" + Path.GetFileName(file) + " schema="+data.Schema+" sources="+data.Sources.Length+" owned=" + data.Items.Length + " awake=" + data.Awake
                + " coverage=" + N((double)data.Items.Length / Math.Max(1, data.Awake)) + " raw=" + N(data.RawTime) + " tb=" + N(data.BakeTime));
            bool auxiliary = GpuDecoCoverage.Auxiliary(data.Awake);
            Log("[gpudeco-lab] auxiliary=" + auxiliary + " reason=" + (auxiliary ? "awake_below_800" : "primary_coverage_gate"));
            if (GpuDecoCoverage.OwnedLow(data.Awake, data.Items.Length)) { low = true; Log("[gpudeco-lab] stop=OWNED_LOW required_coverage=0.7"); break; }
            if(data.Schema<2) throw new GateFailure("GPUDECO_G1=ERROR G0=UNMEASURED stage=source-clock reason=v2_real_anchor_export_required legacy_schema="+data.Schema+" integration=BLOCKED");
            if(!auxiliary && (data.Sources.Length==0 || data.FrameSamples==0)) throw new GateFailure("GPUDECO_G1=G0_FAIL stage=source-clock reason=actual_source_progress_or_frame_samples_missing integration=BLOCKED");
            var unsupported = data.Items.Where(d => d.Tracks.Any(t => t.Any(s => !Supported(s.Ease))) || d.Anchors.Any(a=>a.Valid && !Supported(a.Segment.Ease))).ToArray();
            Log("[gpudeco-lab] unsupported_ease_decorations=" + unsupported.Length + " fallback=original-path");
            data.Items = data.Items.Except(unsupported).ToArray();
            var missingClocks = data.Items.Where(d => data.MotionWithoutClock(d) >= 0).ToArray();
            foreach (var group in missingClocks.GroupBy(d => data.MotionWithoutClock(d)))
                Log("[gpudeco-lab] ownership_reject=source-clock-missing slot="+group.Key+" count="+group.Count()+" predicate=metadata_only original_path=True");
            data.Items = data.Items.Except(missingClocks).ToArray();
            Log("[gpudeco-lab] eligible="+data.Items.Length+" awake="+data.Awake+" coverage="+N((double)data.Items.Length/Math.Max(1,data.Awake)));
            if (GpuDecoCoverage.OwnedLow(data.Awake, data.Items.Length)) { low = true; Log("[gpudeco-lab] stop=OWNED_LOW after_ease_filter=" + data.Items.Length + " required_coverage=0.7"); break; }
            bool samplePass = true;
            if (data.Items.Length == 0) Log("[gpudeco-lab] auxiliary_empty=True G0=NO_SAMPLES");
            else using (var gpu = new Evaluation(Shader(), data))
            {
                var result = gpu.Dispatch(data.BakeTime, true);
                Log("[gpudeco-lab] comparison_mode=anchored");
                samplePass = Compare(data, result.values, result.vertices, true);
                DiagnoseResidual(data, result.values, result.vertices);
                gpu.UseAnchors=false; var baseline=gpu.Dispatch(data.BakeTime,true);
                Log("[gpudeco-lab] comparison_mode=before_anchor_table");
                Compare(data,baseline.values,baseline.vertices,true,false);
                // Attribute() includes a disproven global-clock fit; do not repeat it.
                Log("[gpudeco-lab] uniform_clock_scan=SKIPPED reason=previously_disproven");
            }
            if (auxiliary) auxiliaryPass &= samplePass;
            else { primary++; pass &= samplePass; }
            measured++;
        }
        Log("GPUDECO_G0_OFFLINE=" + (pass && measured == 3 && primary == 2 && !low ? "PASS" : "FAIL") + " measured=" + measured
            + "/3 primary=" + primary + "/2 auxiliary=" + (auxiliaryPass ? "PASS" : "FAIL") + " start_phase=UNMEASURED");
        if (!pass || measured != 3 || primary != 2 || low)
            throw new GateFailure("GPUDECO_G1=" + (low ? "OWNED_LOW" : "G0_FAIL")
                + " G0=" + (pass ? "INCOMPLETE" : "FAIL") + " game_pixels=UNMEASURED reason=real_export_gate_failed integration=BLOCKED");
        Log("GPUDECO_G1=ERROR G0=PASS game_pixels=UNMEASURED reason=game_pixel_capture_required integration=BLOCKED");
    }

    // Attribute remaining errors without fitting clocks or copying truth into inputs.
    static void DiagnoseResidual(GpuDecoData data, float[] values, Vertex[] vertices)
    {
        var groups = new System.Collections.Generic.Dictionary<string, int>(); int details = 0;
        for (int i = 0; i < data.Items.Length; i++)
        {
            var d = data.Items[i]; var fields = new float[16]; Array.Copy(values, i*16, fields, 0, 16);
            var logical = CpuVertices(data, d, d.Actual); var pose = CpuVertices(data, d, fields);
            double geometry = Enumerable.Range(0,4).Max(k => Vector3.Distance(pose[k], vertices[i*4+k].position))*data.PixelsPerWorld;
            if (geometry > .01) { Log("[gpudeco-residual] geometry index="+d.Index+" id="+d.Id+" max_px="+N(geometry)
                +" fields="+string.Join(",",fields.Select(x=>N(x)))+" constants="+string.Join(",",d.Constants.Select(x=>N(x)))
                +" cpu="+pose[0].ToString("G9")+" gpu="+vertices[i*4].position.ToString("G9"));
                var shader=Shader(); int kernel=shader.FindKernel("RotationProbe"); float angle=d.Constants[4]+fields[4]+((d.Flags&2)!=0?data.Camera[2]:0);
                using(var input=new ComputeBuffer(1,16)) using(var output=new ComputeBuffer(1,16)) {
                    input.SetData(new[]{new Vector4(angle,0,0,0)}); shader.SetBuffer(kernel,"_Probe",input); shader.SetBuffer(kernel,"_RotationOut",output);
                    shader.SetInt("_Count",1); shader.Dispatch(kernel,1,1,1); var result=new Vector4[1]; output.GetData(result);
                    var q=Quaternion.Normalize(Quaternion.Euler(0,0,angle));
                    Log("[gpudeco-residual] rotation angle="+N(angle)+" cpu_q="+q.ToString("G9")+" gpu_qz_qw_sn_cs="+result[0].ToString("G9"));
                    shader.SetInt("_Count",data.Items.Length);
                }
            }
            for (int ch=0; ch<16; ch++)
            {
                double error=Math.Abs((double)fields[ch]-d.Actual[ch]);
                if(ch>=7 && ch<=11) { if(error<=1.0/255) continue; }
                else { var shifted=(float[])d.Actual.Clone(); shifted[ch]=fields[ch]; var p=CpuVertices(data,d,shifted);
                    if(Enumerable.Range(0,4).Max(k=>Vector3.Distance(logical[k],p[k]))*data.PixelsPerWorld<=.01) continue; }
                var a=d.Anchors[ch]; int n=DecoBakeCore.Find(d.Tracks[ch],data.BakeTime);
                string key="slot="+ch+" anchor="+a.Valid; groups.TryGetValue(key,out int count); groups[key]=count+1;
                if(details++>=12) continue;
                var s=a.Valid?a.Segment:n>=0?d.Tracks[ch][n]:default(DecoBakeCore.Segment);
                Log("[gpudeco-residual] "+key+" index="+d.Index+" id="+d.Id+" actual="+N(d.Actual[ch])+" evaluated="+N(fields[ch])
                    +" cpu="+N(data.Eval(d,ch,Ease))+" source="+s.Source+" t0="+N(s.T0)+" duration="+N(s.Duration)
                    +" start="+N(s.Start)+" target="+N(s.Target)+" end="+N(s.End)+" ease="+s.Ease
                    +" progress="+(a.Valid&&s.Duration>0?N(data.Sources[a.Clock].Progress):"NA"));
            }
        }
        foreach(var pair in groups) Log("[gpudeco-residual] group "+pair.Key+" count="+pair.Value);
    }

    static void Attribute(GpuDecoData data, float[] gpuValues, Vertex[] gpuVertices)
    {
        var tables = data.Items.Select(d => new DecoBakeCore.Table {
            Decorations = new[] { new DecoBakeCore.Decoration { Initial = d.Initial } }, Tracks = d.Tracks,
            Pitch = data.Pitch, TweenScale = data.TweenScale, Ease = Ease }).ToArray();
        var cpu = tables.Select(t => Enumerable.Range(0, 16).Select(ch => t.Eval(0, ch, data.BakeTime)).ToArray()).ToArray();
        double geometryMax = 0, geometrySum = 0, gpuVertexMax = 0, gpuVertexSum = 0, poseMax = 0, poseSum = 0;
        int geometryOver = 0, gpuVertexOver = 0, poseOver = 0;
        for (int i = 0; i < data.Items.Length; i++)
        {
            var d = data.Items[i]; var logical = CpuVertices(data, d, d.Actual); var table = CpuVertices(data, d, cpu[i]);
            var gpuFields = new float[16]; Array.Copy(gpuValues, i * 16, gpuFields, 0, 16);
            var gpuPose = CpuVertices(data, d, gpuFields);
            for (int k = 0; k < 4; k++)
            {
                double error = Vector3.Distance(logical[k], V(d.World, k * 3)) * data.PixelsPerWorld;
                geometryMax = Math.Max(geometryMax, error); geometrySum += error; if (error > .01) geometryOver++;
                error = Vector3.Distance(table[k], gpuVertices[i * 4 + k].position) * data.PixelsPerWorld;
                gpuVertexMax = Math.Max(gpuVertexMax, error); gpuVertexSum += error; if (error > .01) gpuVertexOver++;
                error = Vector3.Distance(gpuPose[k], gpuVertices[i * 4 + k].position) * data.PixelsPerWorld;
                poseMax = Math.Max(poseMax, error); poseSum += error; if (error > .01) poseOver++;
            }
        }
        Log("[gpudeco-audit] geometry_actual_fields_vs_sprite max_px=" + N(geometryMax) + " mean_px=" + N(geometrySum / (data.Items.Length * 4)) + " over_tol=" + geometryOver);
        Log("[gpudeco-audit] geometry_gpu_vs_cpu_table max_px=" + N(gpuVertexMax) + " mean_px=" + N(gpuVertexSum / (data.Items.Length * 4)) + " over_tol=" + gpuVertexOver);
        Log("[gpudeco-audit] geometry_gpu_vs_gpu_fields max_px=" + N(poseMax) + " mean_px=" + N(poseSum / (data.Items.Length * 4)) + " over_tol=" + poseOver);
        for (int ch = 0; ch < 16; ch++)
        {
            double max = 0, sum = 0, gpuMax = 0, gpuSum = 0; int worst = 0, active = 0, completed = 0;
            var offsets = new System.Collections.Generic.List<double>(); int noFit = 0;
            for (int i = 0; i < data.Items.Length; i++)
            {
                var d = data.Items[i]; double error = Math.Abs((double)cpu[i][ch] - d.Actual[ch]);
                if (error > max) { max = error; worst = i; } sum += error;
                error = Math.Abs((double)gpuValues[i * 16 + ch] - cpu[i][ch]); gpuMax = Math.Max(gpuMax, error); gpuSum += error;
                int n = DecoBakeCore.Find(d.Tracks[ch], data.BakeTime); if (n < 0) continue;
                var s = d.Tracks[ch][n];
                if (s.Duration > 0 && (data.BakeTime - s.T0) / data.Pitch * data.TweenScale < s.Duration)
                {
                    active++;
                    // Infer a per-channel time only on monotone local intervals.
                    // Remaining error and the offset distribution expose whether
                    // a single uniform song clock can represent actual fields.
                    if (s.Ease < 1 || s.Ease > 22) continue;
                    double lo = Math.Max(s.T0, data.BakeTime - .05), hi = Math.Min(s.T0 + s.Duration * data.Pitch / data.TweenScale, data.BakeTime + .05);
                    if (DecoBakeCore.Find(d.Tracks[ch], lo) != n || DecoBakeCore.Find(d.Tracks[ch], hi) != n) continue;
                    float a = tables[i].Eval(0, ch, lo), b = tables[i].Eval(0, ch, hi), target = d.Actual[ch];
                    if (a == b) continue;
                    if (target < Math.Min(a, b) || target > Math.Max(a, b)) { noFit++; continue; }
                    for (int step = 0; step < 32; step++) { double mid = (lo + hi) * .5; float v = tables[i].Eval(0, ch, mid); if ((v < target) == (a < b)) lo = mid; else hi = mid; }
                    offsets.Add(((lo + hi) * .5 - data.BakeTime) * 1000);
                }
                else completed++;
            }
            Log("[gpudeco-audit] slot=" + ch + " cpu_table_vs_actual max=" + N(max) + " mean=" + N(sum / data.Items.Length)
                + " gpu_vs_table_max=" + N(gpuMax) + " gpu_vs_table_mean=" + N(gpuSum / data.Items.Length) + " active=" + active + " completed=" + completed);
            if (offsets.Count > 0)
            {
                offsets.Sort(); Log("[gpudeco-audit] slot=" + ch + " oracle_local_fit_ms min=" + N(offsets[0]) + " p50=" + N(offsets[offsets.Count / 2])
                    + " p95=" + N(offsets[(offsets.Count - 1) * 95 / 100]) + " max=" + N(offsets[offsets.Count - 1]) + " fitted=" + offsets.Count + " outside_50ms=" + noFit + " changes_gate=False");
            }
            if (max > 0)
            {
                var d = data.Items[worst]; int n = DecoBakeCore.Find(d.Tracks[ch], data.BakeTime);
                Log("[gpudeco-audit] worst slot=" + ch + " index=" + d.Index + " id=" + d.Id + " flags=" + d.Flags + " initial=" + N(d.Initial[ch])
                    + " actual=" + N(d.Actual[ch]) + " cpu=" + N(cpu[worst][ch]) + " gpu=" + N(gpuValues[worst * 16 + ch]) + " track_index=" + n);
                foreach (int j in Enumerable.Range(Math.Max(0, n - 1), Math.Min(d.Tracks[ch].Length, n + 2) - Math.Max(0, n - 1)))
                {
                    var s = d.Tracks[ch][j]; Log("[gpudeco-audit] segment=" + j + " t0=" + N(s.T0) + " duration=" + N(s.Duration) + " start=" + N(s.Start) + " end=" + N(s.End) + " target=" + N(s.Target) + " ease=" + s.Ease);
                }
            }
        }
        ScanUniformClock(data, tables);
    }

    static void ScanUniformClock(GpuDecoData data, DecoBakeCore.Table[] tables)
    {
        double bestMean = double.PositiveInfinity, bestMeanMax = 0, bestMeanOffset = 0;
        double bestMax = double.PositiveInfinity, bestMaxMean = 0, bestMaxOffset = 0;
        int bestMeanOver = 0, bestMaxOver = 0;
        // Bounded diagnostic search. This does not prove a continuous optimum,
        // measure trigger phase, modify data, or relax the fixed-time G0 gate.
        for (int step = -100; step <= 100; step++)
        {
            double offset = step * .0005, sum = 0, max = 0; int over = 0;
            for (int i = 0; i < data.Items.Length; i++)
            {
                var fields = new float[16]; for (int ch = 0; ch < 16; ch++) fields[ch] = tables[i].Eval(0, ch, data.BakeTime + offset);
                var points = CpuVertices(data, data.Items[i], fields);
                for (int k = 0; k < 4; k++)
                {
                    double error = Vector3.Distance(points[k], V(data.Items[i].World, k * 3)) * data.PixelsPerWorld;
                    sum += error; max = Math.Max(max, error); if (error > .01) over++;
                }
            }
            double mean = sum / (data.Items.Length * 4);
            if (mean < bestMean) { bestMean = mean; bestMeanMax = max; bestMeanOffset = offset; bestMeanOver = over; }
            if (max < bestMax) { bestMax = max; bestMaxMean = mean; bestMaxOffset = offset; bestMaxOver = over; }
        }
        Log("[gpudeco-audit] uniform_clock_scan candidates=201 range_ms=-50..50 step_ms=0.5 oracle=True changes_gate=False");
        Log("[gpudeco-audit] uniform_clock_best_mean offset_ms=" + N(bestMeanOffset * 1000) + " max_px=" + N(bestMeanMax) + " mean_px=" + N(bestMean) + " over_tol=" + bestMeanOver);
        Log("[gpudeco-audit] uniform_clock_best_max offset_ms=" + N(bestMaxOffset * 1000) + " max_px=" + N(bestMax) + " mean_px=" + N(bestMaxMean) + " over_tol=" + bestMaxOver);
    }
}
#endif
