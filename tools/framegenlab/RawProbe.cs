// Research only. No original/native Present connection, generation or render patch.
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix {
    internal static class FrameGenRawProbe {
        private static RawFrames owner;
        internal static void Install() { }
        internal static void Start(string arg) {
            if(owner!=null)throw new InvalidOperationException("Preserve the previous raw capture");
            if(Main.Config.FrameGenOutside!=0 || Main.Config.FrameGenRefresh)throw new InvalidOperationException("Raw reference requires FrameGen OFF");
            var words=arg.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(words.Length!=2)throw new ArgumentException("rawstart <begin song seconds> <end>");
            double begin=double.Parse(words[0],CultureInfo.InvariantCulture),end=double.Parse(words[1],CultureInfo.InvariantCulture);
            if(!double.IsFinite(begin)||!double.IsFinite(end)||begin<0||end<=begin||end-begin>4)throw new ArgumentException("Use a finite window <=4s");
            if(!SystemInfo.supportsAsyncGPUReadback)throw new InvalidOperationException("Async readback unsupported");
            var go=new GameObject("StutterFix raw reference"){hideFlags=HideFlags.HideAndDontSave};
            UnityEngine.Object.DontDestroyOnLoad(go);owner=go.AddComponent<RawFrames>();
            try{owner.Initialize(begin,end);}catch{Uninstall();throw;}
        }
        internal static void Finish() { if(owner!=null){owner.Save();owner=null;} }
        internal static void Uninstall() { var old=owner;owner=null;if(old!=null)old.Stop(); }

        private sealed class RawFrames:MonoBehaviour {
            private const int Capacity=256,Slots=4;
            private struct Row {internal double song,qpc,submitMs,copyMs;internal int frame,fxFrame;internal bool ready,glow;}
            private readonly Row[] rows=new Row[Capacity];
            private readonly byte[][] pixels=new byte[Capacity][];
            private readonly bool[] busy=new bool[Slots];
            private readonly int[] pending=new int[Slots];
            private readonly RenderTexture[] small=new RenderTexture[Slots];
            private readonly Action<AsyncGPUReadbackRequest>[] callbacks=new Action<AsyncGPUReadbackRequest>[Slots];
            private RenderTexture full;
            private int width,height,screenWidth,screenHeight,count,dropped,errors,inflight;
            private double begin,end,next;
            private bool stopping,released;
            private string failure="";
            internal void Initialize(double from,double to) {
                begin=from;end=to;screenWidth=Screen.width;screenHeight=Screen.height;width=screenWidth/6;height=screenHeight/6;
                if(screenWidth!=3440||screenHeight!=1440)throw new InvalidOperationException("Actual3440x1440 required");
                full=Make(screenWidth,screenHeight);
                for(int i=0;i<Slots;i++){small[i]=Make(width,height);int slot=i;callbacks[i]=r=>Complete(slot,r);}
                for(int i=0;i<Capacity;i++)pixels[i]=new byte[width*height*4];
                StartCoroutine(Capture());
                Main.Entry.Logger.Log("[원본캡처] started "+from.ToString("R",CultureInfo.InvariantCulture)+".."+to.ToString("R",CultureInfo.InvariantCulture)+" "+FrameGen.Describe());
            }
            private static RenderTexture Make(int w,int h) {
                var rt=new RenderTexture(w,h,0,RenderTextureFormat.ARGB32){filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
                if(!rt.Create())throw new InvalidOperationException("Capture RT creation failed");return rt;
            }
            private IEnumerator Capture() {
                var eof=new WaitForEndOfFrame();
                while(!stopping){
                    yield return eof;
                    if(stopping)break;
                    if(!Hitch.Playing||scrConductor.instance==null)continue;
                    double song=scrConductor.instance.songposition_minusi;
                    if(song<begin||song>=end)continue;
                    if(Screen.width!=screenWidth||Screen.height!=screenHeight||Main.Config.FrameGenOutside!=0||Main.Config.FrameGenRefresh){failure="screen or OFF condition changed";break;}
                    double now=(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
                    if(now<next)continue;next=now+1.0/60;
                    int slot=-1;for(int i=0;i<Slots;i++)if(!busy[i]){slot=i;break;}
                    if(slot<0||count==Capacity){++dropped;continue;}
                    int index=count++;pending[slot]=index;busy[slot]=true;++inflight;
                    rows[index]=new Row{song=song,qpc=now,frame=Time.frameCount,fxFrame=ScreenEffects.LastFrame,glow=ScreenEffects.LastGlow};
                    long tick=Stopwatch.GetTimestamp();
                    var previous=RenderTexture.active;
                    try{
                        ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                        Graphics.Blit(full,small[slot]);
                        AsyncGPUReadback.Request(small[slot],0,TextureFormat.RGBA32,callbacks[slot]);
                        rows[index].submitMs=(Stopwatch.GetTimestamp()-tick)*1000.0/Stopwatch.Frequency;
                    }catch(Exception ex){failure=ex.Message;++errors;busy[slot]=false;--inflight;break;}
                    finally{RenderTexture.active=previous;}
                }
                while(inflight!=0)yield return null;
                if(stopping)Release();
            }
            private void Complete(int slot,AsyncGPUReadbackRequest request) {
                int index=pending[slot];long tick=Stopwatch.GetTimestamp();
                try{if(request.hasError){++errors;}else{request.GetData<byte>().CopyTo(pixels[index]);rows[index].ready=true;}}
                catch(Exception ex){++errors;failure=ex.Message;}
                finally{rows[index].copyMs=(Stopwatch.GetTimestamp()-tick)*1000.0/Stopwatch.Frequency;busy[slot]=false;--inflight;}
            }
            internal void Save() {
                if(inflight!=0)throw new InvalidOperationException("Raw capture still in flight; no blocking GPU wait");
                string folder=Path.Combine(Main.Entry.Path,"framegen-raw");Directory.CreateDirectory(folder);
                using(var csv=new StreamWriter(Path.Combine(folder,"frames.csv"))){
                    csv.WriteLine("index,song_s,qpc_s,unity_frame,ready,submit_cpu_ms,copy_cpu_ms,fx_frame,fx_glow");
                    for(int i=0;i<count;i++){
                        var r=rows[i];csv.WriteLine(FormattableString.Invariant($"{i},{r.song:R},{r.qpc:R},{r.frame},{(r.ready?1:0)},{r.submitMs:R},{r.copyMs:R},{r.fxFrame},{(r.glow?1:0)}"));
                        if(!r.ready)continue;
                        using(var f=File.Create(Path.Combine(folder,$"raw-{i:000}.ppm"))){
                            var header=System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");f.Write(header,0,header.Length);
                            var rgb=new byte[width*3];
                            for(int y=0;y<height;y++){for(int x=0;x<width;x++)Buffer.BlockCopy(pixels[i],(y*width+x)*4,rgb,x*3,3);f.Write(rgb,0,rgb.Length);}
                        }
                    }
                }
                File.WriteAllText(Path.Combine(folder,"state.txt"),FormattableString.Invariant($"count={count} dropped={dropped} errors={errors} inflight={inflight} width={width} height={height}\nfailure={failure}\n{FrameGen.Describe()}\n"));
                Main.Entry.Logger.Log("[원본캡처] saved "+count+" errors="+errors);Stop();
            }
            internal void Stop(){stopping=true;if(inflight==0)Release();}
            private void Release(){
                if(released)return;released=true;
                if(full!=null){full.Release();UnityEngine.Object.Destroy(full);}
                foreach(var rt in small)if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
