#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using StutterFix;
using UnityEngine;
using LiveChannel = StutterFix.GpuDecoGpu.LiveChannel;

public static partial class GpuDecoLab
{
    public static void SourceCapacity() => Run(()=>
    {
        var bundle=AssetBundle.LoadFromFile(Path.Combine(Evidence,"bundle/stutterfix_gpudeco"));
        Require(bundle,"bundle reload failed");
        try
        {
            var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");
            var indices=new[]{0,2047,2048,4095,4096,8191};
            var data=Synthetic(indices.Length);
            var channels=new LiveChannel[data.Items.Length*16];
            var phases=new float[8192];
            for(int i=0;i<indices.Length;i++)
            {
                for(int c=0;c<16;c++)channels[i*16+c]=new LiveChannel{sourceIndex=-1,end=data.Items[i].Initial[c]};
                channels[i*16]=new LiveChannel{sourceIndex=indices[i],start=1,change=2,end=3};
                phases[indices[i]]=(i+1)/8f;
            }
            using(var gpu=new GpuDecoGpu(shader,data,phases.Length))
            {
                Require(gpu.HasNativeLiveKernel,"EvaluateLive missing");
                gpu.UseLiveMode=true;gpu.UploadDirtyLiveChannels(channels,null,0,channels.Length-1);
                gpu.SetLiveUniforms(new Vector4(data.Camera[0],data.Camera[1],data.Camera[2],data.Camera[3]),data.BakeTime);
                gpu.Bind();
                int comparisons=0;
                for(int pass=0;pass<2;pass++)
                {
                    if(pass==1)phases[8191]=.125f;
                    gpu.UploadSourceE(phases,null,phases.Length);gpu.DispatchEvaluate();
                    var values=new float[channels.Length];gpu.ReadValues(values);
                    for(int i=0;i<indices.Length;i++)for(int c=0;c<16;c++)
                    {
                        float expected=c==0?1+2*phases[indices[i]]:data.Items[i].Initial[c];
                        Require(BitConverter.SingleToInt32Bits(values[i*16+c])==BitConverter.SingleToInt32Bits(expected),"source capacity value/isolation mismatch");
                        comparisons++;
                    }
                }
                Log("GPUDECO_SOURCE_CAPACITY=PASS capacity=8192 indices=0,2047,2048,4095,4096,8191 passes=2 bit_exact_channels="+comparisons+" game_full_song=UNMEASURED");
            }
        }
        finally {bundle.Unload(true);}
    },"source-capacity");
    public static void Live() => Run(()=>LiveBody(Shader()),"live");
    static void LiveBody(ComputeShader shader)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-gpudecoInput");
        Require(at>=0 && at+1<args.Length,"live probe requires -gpudecoInput");
        var files=Directory.GetFiles(args[at+1],"*.gpudeco").OrderBy(x=>x).ToArray();
        Require(files.Length==3,"live probe requires three real snapshots");
        foreach(var file in files)
        {
            GpuDecoData data;using(var input=File.OpenRead(file))data=GpuDecoData.Read(input);
            Require(data.Schema==2,"actual source clocks required");
            var channels=new LiveChannel[data.Items.Length*16];
            var e=new float[Math.Max(1,data.Sources.Length)];
            var phases=new bool[e.Length];
            for(int i=0;i<data.Items.Length;i++)for(int c=0;c<16;c++)
            {
                var d=data.Items[i];var a=d.Anchors[c];var s=a.Segment;
                var lc=new LiveChannel {sourceIndex=-1,end=data.Eval(d,c,Ease)};
                if(a.Valid && s.Duration>0)
                {
                    float t=data.Sources[a.Clock].Progress;
                    if(t>0 && t<s.Duration)
                    {
                        float phase=Ease(s.Ease,t,s.Duration,s.Over,s.Period);
                        Require(!phases[a.Clock] || e[a.Clock].Equals(phase),"shared source has inconsistent easing");
                        phases[a.Clock]=true;e[a.Clock]=phase;
                        lc=new LiveChannel {start=s.Start,change=s.Target-s.Start,end=s.End,sourceIndex=a.Clock};
                    }
                }
                channels[i*16+c]=lc;
            }
            using(var gpu=new GpuDecoGpu(shader,data))
            {
                Require(gpu.HasNativeLiveKernel,"bundle is missing EvaluateLive");
                gpu.UploadDirtyLiveChannels(channels,null,0,channels.Length-1);
                gpu.UploadSourceE(e,null,e.Length);gpu.UseLiveMode=true;gpu.Bind();
                gpu.SetLiveUniforms(new Vector4(data.Camera[0],data.Camera[1],data.Camera[2],data.Camera[3]),data.BakeTime);
                gpu.DispatchEvaluate();
                var values=new float[channels.Length];var points=new StutterFix.GpuDecoGpu.Vertex[data.Items.Length*4];
                gpu.ReadValues(values);gpu.VertexBuffer.GetData(points);
                Require(Compare(data,values,points,true),"EvaluateLive snapshot G0 failed");
                Log("[gpudeco-live-probe] file="+Path.GetFileName(file)+" owned="+data.Items.Length+" sources="+data.Sources.Length+" sourceE="+phases.Count(x=>x)+" G0=PASS shader=EvaluateLive_sourceE actual_truth_as_input=False");
            }
        }
        Log("GPUDECO_LIVE_OFFLINE=PASS snapshots=3 game_playback=UNMEASURED cost=UNMEASURED");
    }
}
#endif
