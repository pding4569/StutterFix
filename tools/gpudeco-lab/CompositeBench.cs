#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class GpuDecoLab
{
    public static void CompositeBench() => Run(CompositeBenchBody, "composite-bench");
    public static void CompositeBenchControls() => Run(CompositeControlsBody, "composite-controls");
    [StructLayout(LayoutKind.Sequential)]
    struct CompositeLayer { public Vector4 row0, row1, tint, info; }
    const int CW=2560, CH=1440, CTX=(CW+31)/32, CTY=(CH+31)/32;
    static readonly string[] CMethods={"A1","A2","B1","B2","B3"};
    static readonly int[] CRepeats={4,12,24};
    sealed class CompositeCase : IDisposable
    {
        public ComputeBuffer layers, counts, indices;
        public Material material;
        public ComputeShader compute;
        public Mesh whole, combined;
        public Mesh[] singles;
        public int count, kernel;
        public RenderTexture target, tiny;
        public CompositeLayer[] data;
        public CompositeCase(int scene,int n,Texture2DArray texture,Shader shader,ComputeShader cs)
        {
            count=n; data=MakeCompositeLayers(scene,n);
            layers=new ComputeBuffer(n,64);layers.SetData(data);
            counts=new ComputeBuffer(CTX*CTY,4); indices=new ComputeBuffer(CTX*CTY*64,4);
            material=new Material(shader);material.SetBuffer("_Layers",layers);
            material.SetBuffer("_TileCounts",counts);material.SetBuffer("_TileIndices",indices);
            material.SetTexture("_LayerTex",texture);material.SetInt("_Count",n);
            material.SetVector("_Size",new Vector4(CW,CH,CTX,CTY));
            compute=cs;kernel=cs.FindKernel("BuildTiles");
            whole=CompositeMesh(null,0,1);combined=CompositeMesh(data,0,n);
            singles=new Mesh[n];for(int i=0;i<n;i++)singles[i]=CompositeMesh(data,i,1);
            target=new RenderTexture(CW,CH,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){antiAliasing=1};target.Create();
            tiny=new RenderTexture(1,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);tiny.Create();
        }
        public CommandBuffer Commands(int method,int repeats,bool background,bool stats=false,RenderTexture rt=null)
        {
            var cb=new CommandBuffer{name="CompositeBench "+CMethods[method]+" K="+repeats};
            cb.SetRenderTarget(rt?rt:target);
            if(background)cb.DrawMesh(whole,Matrix4x4.identity,material,0,0);
            var p=new MaterialPropertyBlock();p.SetInt("_Mode",method==2?1:method==3?2:3);
            for(int k=0;k<repeats;k++)
            {
                if(method==0)for(int i=0;i<count;i++)cb.DrawMesh(singles[i],Matrix4x4.identity,material,0,1);
                else if(method==1)cb.DrawMesh(combined,Matrix4x4.identity,material,0,1);
                else
                {
                    if(method==4)
                    {
                        cb.SetComputeIntParam(compute,"_Count",count);
                        cb.SetComputeVectorParam(compute,"_Size",new Vector4(CW,CH,CTX,CTY));
                        cb.SetComputeBufferParam(compute,kernel,"_Layers",layers);
                        cb.SetComputeBufferParam(compute,kernel,"_TileCounts",counts);
                        cb.SetComputeBufferParam(compute,kernel,"_TileIndices",indices);
                        cb.DispatchCompute(compute,kernel,(CTX*CTY+63)/64,1,1);
                    }
                    cb.DrawMesh(whole,Matrix4x4.identity,material,0,stats?3:2,p);
                }
            }
            if(!stats)cb.CopyTexture(target,0,0,0,0,1,1,tiny,0,0,0,0);
            return cb;
        }
        public void Fence()
        {
            var read=AsyncGPUReadback.Request(tiny,0);read.WaitForCompletion();
            Require(!read.hasError,"Composite GPU fence readback failed");
        }
        public void Dispose()
        {
            layers.Dispose();counts.Dispose();indices.Dispose();
            foreach(var mesh in singles)UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(whole);UnityEngine.Object.DestroyImmediate(combined);
            UnityEngine.Object.DestroyImmediate(material);
            target.Release();tiny.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(tiny);
        }
    }
    static CompositeLayer[] MakeCompositeLayers(int scene,int n)
    {
        var result=new CompositeLayer[n];var random=new System.Random(20261112);
        for(int i=0;i<n;i++)
        {
            float theta=(float)(random.NextDouble()*Math.PI*2),c=Mathf.Cos(theta),s=Mathf.Sin(theta);
            float area=.3f+.7f*(float)random.NextDouble();
            float sx=scene==2?Mathf.Sqrt(area):1.65f+.35f*(float)random.NextDouble();
            float sy=scene==2?sx:1.65f+.35f*(float)random.NextDouble();
            float x=.5f+(float)(random.NextDouble()-.5)*(scene==2?.25f:.08f);
            float y=.5f+(float)(random.NextDouble()-.5)*(scene==2?.25f:.08f);
            var r0=new Vector4(c/sx,s/sx,.5f-(c*x+s*y)/sx,0);
            var r1=new Vector4(-s/sy,c/sy,.5f-(-s*x+c*y)/sy,0);
            result[i]=new CompositeLayer{row0=r0,row1=r1,
                tint=new Vector4(.5f+.5f*(float)random.NextDouble(),.5f+.5f*(float)random.NextDouble(),.5f+.5f*(float)random.NextDouble(),.65f+.35f*(float)random.NextDouble()),
                info=new Vector4(i%16,scene==3&&i%4==3?1:0,area,0)};
        }
        return result;
    }
    static Mesh CompositeMesh(CompositeLayer[] layers,int start,int n)
    {
        var v=new Vector3[n*4];var uv=new Vector2[n*4];var ids=new List<Vector2>(n*4);var t=new int[n*6];
        var corners=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
        for(int i=0;i<n;i++)
        {
            for(int j=0;j<4;j++)
            {
                var p=corners[j];uv[i*4+j]=p;ids.Add(new Vector2(start+i,0));
                if(layers!=null)
                {
                    var l=layers[start+i];float a=p.x-l.row0.z,b=p.y-l.row1.z;
                    float det=l.row0.x*l.row1.y-l.row0.y*l.row1.x;
                    p=new Vector2((a*l.row1.y-b*l.row0.y)/det,(b*l.row0.x-a*l.row1.x)/det);
                }
                v[i*4+j]=new Vector3(p.x,p.y,0);
            }
            int z=i*6,k=i*4;t[z]=k;t[z+1]=k+1;t[z+2]=k+2;t[z+3]=k;t[z+4]=k+2;t[z+5]=k+3;
        }
        var mesh=new Mesh{name="Composite bench quads"};mesh.vertices=v;mesh.uv=uv;mesh.SetUVs(1,ids);mesh.triangles=t;
        mesh.bounds=new Bounds(Vector3.zero,Vector3.one*100);return mesh;
    }
    static Texture2DArray MakeCompositeTexture()
    {
        const int size=1024;
        var tex=new Texture2DArray(size,size,16,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];var random=new System.Random(39281);
        for(int layer=0;layer<16;layer++)
        {
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float a=layer<8?.4f+.2f*Mathf.Sin(u*(12+layer))*Mathf.Cos(v*(9+layer)):
                    layer<12?(random.NextDouble()<.8?0:.3f+.7f*(float)random.NextDouble()):
                    Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Max(Mathf.Abs(u-.5f),Mathf.Abs(v-.5f))-.32f)/.15f));
                pixels[y*size+x]=new Color(.25f+.65f*u,.25f+.65f*v,.25f+.5f*(.5f+.5f*Mathf.Sin(u*8+layer)),a);
            }
            tex.SetPixels32(pixels,layer);
        }
        tex.Apply(false,true);return tex;
    }
    static Color32[] CompositeRead(RenderTexture rt,string png=null)
    {
        var prev=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(CW,CH,TextureFormat.RGBA32,false,true);
        try {tex.ReadPixels(new Rect(0,0,CW,CH),0,0);tex.Apply();if(png!=null)File.WriteAllBytes(png,tex.EncodeToPNG());return tex.GetPixels32();}
        finally {RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(tex);}
    }
    static void CompositeDiff(Color32[] a,Color32[] b,out int[] max,out double[] mean,out int changed)
    {
        max=new int[4];mean=new double[4];changed=0;
        for(int i=0;i<a.Length;i++)
        {
            int[] d={Math.Abs(a[i].r-b[i].r),Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b),Math.Abs(a[i].a-b[i].a)};
            bool any=false;for(int j=0;j<4;j++){max[j]=Math.Max(max[j],d[j]);mean[j]+=d[j];any|=d[j]!=0;}if(any)changed++;
        }
        for(int j=0;j<4;j++)mean[j]/=a.Length;
    }
    static double Median(double[] values){var sorted=(double[])values.Clone();Array.Sort(sorted);return (sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])*.5;}
    static double[] CompositeStats(CompositeCase c,int method)
    {
        var rt=new RenderTexture(CW,CH,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
        var prev=RenderTexture.active;var tex=new Texture2D(CW,CH,TextureFormat.RGBAFloat,false,true);
        try
        {
            using(var cmd=c.Commands(method,1,false,true,rt))Graphics.ExecuteCommandBuffer(cmd);
            RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,CW,CH),0,0);tex.Apply();
            var values=tex.GetPixelData<Color>(0);var sum=new double[4];
            for(int i=0;i<values.Length;i++){var p=values[i];sum[0]+=p.r;sum[1]+=p.g;sum[2]+=p.b;sum[3]+=p.a;}
            for(int j=0;j<4;j++)sum[j]/=values.Length;return sum;
        }
        finally {RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    static void CompositeBenchBody()
    {
        string output=Path.Combine(Evidence,"composite-20261112a");Directory.CreateDirectory(output);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CompositeBench.shader");
        var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/CompositeBench.compute");
        Require(shader&&compute&&shader.isSupported,"Composite shaders missing/unsupported");
        bool oldAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var texture=MakeCompositeTexture();
        string smoke=Environment.GetCommandLineArgs().Contains("-compositeSmoke")?"smoke-":"";
        int samples=smoke.Length>0?2:20;
        File.WriteAllText(Path.Combine(output,smoke+"metadata.txt"),"UTC="+DateTime.UtcNow.ToString("O")+"\nUnity="+Application.unityVersion+"\nGPU="+SystemInfo.graphicsDeviceName+"\nAPI="+SystemInfo.graphicsDeviceVersion+"\nCPU="+SystemInfo.processorType+"\nRT=2560x1440 ARGB32 linear MSAA1\nTexture=1024x1024x16 RGBA32 linear bilinear clamp no mips\nSeed=20261112/39281\nTimer=Stopwatch ExecuteCommandBuffer + CopyTexture1x1 + AsyncGPUReadback.WaitForCompletion; no GPU timestamp\nK=4,12,24; samples="+samples+"; warmup=8 per method/K, randomized measurement order\n");
        using(var csv=new StreamWriter(Path.Combine(output,smoke+"results.csv")))
        using(var raw=new StreamWriter(Path.Combine(output,smoke+"samples.csv")))
        {
            csv.WriteLine("scene,N,method,K,median_ms,p10_ms,p90_ms,speedup_A1,maxR,maxG,maxB,maxA,meanR,meanG,meanB,meanA,changedPixels,earlySkipped,iterations,samples,Tmean,meanTileEntries,coverage");
            raw.WriteLine("scene,N,method,K,sample,total_ms,per_pass_ms");
            try
            {
                foreach(int scene in new[]{1,2,3})foreach(int n in new[]{8,16,32,64})
                {
                    if(smoke.Length>0 && n!=16)continue;
                    using(var c=new CompositeCase(scene,n,texture,shader,compute))
                    {
                        var images=new Color32[5][];var maxes=new int[5][];var means=new double[5][];var changed=new int[5];
                        var stats=new double[5][];
                        for(int m=0;m<5;m++)
                        {
                            using(var cmd=c.Commands(m,1,true)){Graphics.ExecuteCommandBuffer(cmd);c.Fence();}
                            images[m]=CompositeRead(c.target,Path.Combine(output,smoke+"S"+scene+"-N"+n+"-"+CMethods[m]+".png"));
                            CompositeDiff(images[0],images[m],out maxes[m],out means[m],out changed[m]);
                            stats[m]=m>=2?CompositeStats(c,m):new double[4];
                        }
                        Require(changed[1]==0,"A2 must exactly match A1");
                        int[] tileMax;double[] tileMeanDiff;int tileChanged;
                        CompositeDiff(images[3],images[4],out tileMax,out tileMeanDiff,out tileChanged);
                        Require(tileChanged==0,"B3 tile-list output must exactly match B2");
                        int[] earlyMax;double[] earlyMean;int earlyChanged;
                        CompositeDiff(images[2],images[3],out earlyMax,out earlyMean,out earlyChanged);
                        Log("COMPOSITE_PARITY S"+scene+" N="+n+" A2_vs_A1=exact B3_vs_B2=exact B2_vs_B1_max="+string.Join("/",earlyMax)+" mean="+string.Join("/",earlyMean.Select(N))+" changed="+earlyChanged);
                        Require(images[0].Any(p=>p.r!=images[0][0].r),"Vacuous uniform output");
                        var tileCounts=new uint[CTX*CTY];c.counts.GetData(tileCounts);
                        double tileMean=tileCounts.Average(v=>(double)v),coverage=stats[2][2]/n;
                        var commands=new CommandBuffer[5,3];var timings=new double[5,3][];
                        try
                        {
                            for(int m=0;m<5;m++)for(int ki=0;ki<3;ki++)
                            {
                                commands[m,ki]=c.Commands(m,CRepeats[ki],false);timings[m,ki]=new double[samples];
                                for(int w=0;w<8;w++){Graphics.ExecuteCommandBuffer(commands[m,ki]);c.Fence();}
                            }
                            var random=new System.Random(298+n+scene);
                            for(int sample=0;sample<samples;sample++)
                            {
                                var order=Enumerable.Range(0,15).OrderBy(_=>random.Next()).ToArray();
                                foreach(int index in order)
                                {
                                    int m=index/3,ki=index%3;var watch=Stopwatch.StartNew();
                                    Graphics.ExecuteCommandBuffer(commands[m,ki]);c.Fence();watch.Stop();
                                    double ms=watch.Elapsed.TotalMilliseconds;
                                    timings[m,ki][sample]=ms/CRepeats[ki];
                                    raw.WriteLine(string.Join(",",scene,n,CMethods[m],CRepeats[ki],sample,N(ms),N(ms/CRepeats[ki])));
                                }
                            }
                            for(int m=0;m<5;m++)for(int ki=0;ki<3;ki++)
                            {
                                var ts=timings[m,ki].OrderBy(v=>v).ToArray();double median=Median(ts);
                                csv.WriteLine(string.Join(",",scene,n,CMethods[m],CRepeats[ki],N(median),N(ts[(int)((samples-1)*.1)]),N(ts[(int)((samples-1)*.9)]),N(Median(timings[0,ki])/median),
                                    maxes[m][0],maxes[m][1],maxes[m][2],maxes[m][3],N(means[m][0]),N(means[m][1]),N(means[m][2]),N(means[m][3]),changed[m],
                                    N(stats[m][0]),N(stats[m][1]),N(stats[m][2]),N(stats[m][3]),N(tileMean),N(coverage)));
                            }
                            csv.Flush();raw.Flush();
                            Log("COMPOSITE S"+scene+" N="+n+" K12 ms="+string.Join("/",Enumerable.Range(0,5).Select(m=>N(Median(timings[m,1]))))+" B1 max="+string.Join("/",maxes[2])+" B2 skipped="+N(stats[3][0])+" B3 skipped="+N(stats[4][0])+" coverage="+N(coverage));
                        }
                        finally {foreach(var cmd in commands)if(cmd!=null)cmd.Dispose();}
                    }
                }
                Log("COMPOSITE_BENCH=COMPLETE samples="+samples+" output="+output);
            }
            finally {UnityEngine.Object.DestroyImmediate(texture);ShaderUtil.allowAsyncCompilation=oldAsync;}
        }
    }
    static void CompositeControlsBody()
    {
        string output=Path.Combine(Evidence,"composite-20261112a");
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CompositeBench.shader");
        var cs=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/CompositeBench.compute");
        bool oldAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var white=new Texture2DArray(1,1,16,TextureFormat.RGBA32,false,true);
        for(int i=0;i<16;i++)white.SetPixels(new[]{Color.white},i);white.Apply();
        try
        {
            // Controlled proof that intermediate UNorm saturation breaks mixed-additive associativity.
            using(var c=new CompositeCase(1,3,white,shader,cs))
            {
                for(int i=0;i<3;i++)c.data[i]=new CompositeLayer{row0=new Vector4(1,0,0,0),row1=new Vector4(0,1,0,0),tint=new Vector4(i==0?.9f:i==1?1:0,i==0?.9f:i==1?1:0,i==0?.9f:i==1?1:0,i==1?.8f:i==2?.5f:1),info=new Vector4(0,i==1?1:0,0,0)};
                c.layers.SetData(c.data);
                for(int i=0;i<3;i++){UnityEngine.Object.DestroyImmediate(c.singles[i]);c.singles[i]=CompositeMesh(c.data,i,1);}
                Color32[] baseline=null;
                for(int m=0;m<5;m++)
                {
                    if(m==1)continue;
                    using(var cmd=c.Commands(m,1,true)){Graphics.ExecuteCommandBuffer(cmd);c.Fence();}
                    var image=CompositeRead(c.target,Path.Combine(output,"clamp-control-"+CMethods[m]+".png"));
                    if(m==0)baseline=image;
                    var p=image[(CH/2)*CW+CW/2];
                    Log("CLAMP_CONTROL method="+CMethods[m]+" rgba="+p.r+"/"+p.g+"/"+p.b+"/"+p.a+" expected_ideal_A1=.5 expected_ideal_B=.85");
                    Require(m==0?Math.Abs(p.r-128)<=1:Math.Abs(p.r-217)<=1,"Clamp negative control unexpected");
                }
                // Empty submission + the same fence establishes the additive wall-time floor.
                var empty=new CommandBuffer();empty.CopyTexture(c.target,0,0,0,0,1,1,c.tiny,0,0,0,0);
                using(empty)
                {
                    var times=new double[20];for(int i=0;i<28;i++){var w=Stopwatch.StartNew();Graphics.ExecuteCommandBuffer(empty);c.Fence();w.Stop();if(i>=8)times[i-8]=w.Elapsed.TotalMilliseconds;}
                    Log("EMPTY_SUBMIT_FENCE median_ms="+N(Median(times))+" samples=20");
                }
            }
            using(var geometry=new StreamWriter(Path.Combine(output,"layers.csv")))
            using(var costs=new StreamWriter(Path.Combine(output,"tile-build-cost.csv")))
            {
                geometry.WriteLine("scene,N,id,uX,uY,uOffset,vX,vY,vOffset");
                costs.WriteLine("scene,N,K,median_ms,p10_ms,p90_ms");
                foreach(int scene in new[]{1,2,3})foreach(int n in new[]{8,16,32,64})
                using(var c=new CompositeCase(scene,n,white,shader,cs))
                {
                    for(int i=0;i<n;i++){var l=c.data[i];geometry.WriteLine(string.Join(",",scene,n,i,N(l.row0.x),N(l.row0.y),N(l.row0.z),N(l.row1.x),N(l.row1.y),N(l.row1.z)));}
                    foreach(int k in CRepeats)
                    using(var cmd=new CommandBuffer())
                    {
                        cmd.SetComputeIntParam(cs,"_Count",n);cmd.SetComputeVectorParam(cs,"_Size",new Vector4(CW,CH,CTX,CTY));
                        cmd.SetComputeBufferParam(cs,c.kernel,"_Layers",c.layers);cmd.SetComputeBufferParam(cs,c.kernel,"_TileCounts",c.counts);cmd.SetComputeBufferParam(cs,c.kernel,"_TileIndices",c.indices);
                        for(int j=0;j<k;j++)cmd.DispatchCompute(cs,c.kernel,(CTX*CTY+63)/64,1,1);
                        // Read the count buffer written by the final build to enforce completion.
                        var times=new double[20];
                        for(int i=0;i<28;i++)
                        {
                            var w=Stopwatch.StartNew();Graphics.ExecuteCommandBuffer(cmd);
                            var read=AsyncGPUReadback.Request(c.counts);read.WaitForCompletion();Require(!read.hasError,"tile build fence error");w.Stop();
                            if(i>=8)times[i-8]=w.Elapsed.TotalMilliseconds/k;
                        }
                        Array.Sort(times);costs.WriteLine(string.Join(",",scene,n,k,N(Median(times)),N(times[1]),N(times[17])));
                    }
                }
            }
            Log("COMPOSITE_CONTROLS=COMPLETE");
        }
        finally {UnityEngine.Object.DestroyImmediate(white);ShaderUtil.allowAsyncCompilation=oldAsync;}
    }
}
#endif
