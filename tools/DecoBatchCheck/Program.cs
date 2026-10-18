using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using DG.Tweening;
using UnityEngine;
using StutterFix;

namespace UnityEngine {
    public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
    public struct Color { public float r,g,b,a; }
}
class scrDecoration { internal Vector2 scaleVec,pivotPosVec; internal float rotAngle; }
namespace StutterFix {
    internal static partial class DecoAnim {
        internal static int AblateMask;
        internal sealed class Rec {
            internal int BatchShadowSlot=-1;
            internal scrDecoration D; internal int Key; internal Ease E;
            internal bool Started=true,Running=true;
            internal float Dur,Pos,Over,Period,FStart,FChange,FLast;
            internal Vector2 VStart,VChange,VLast;
            internal Color CStart,CChange,CLast;
        }
        internal static readonly List<Rec> ProbeRecords=new List<Rec>();
        static partial void BatchShadowStart(Rec r);
        static partial void BatchShadowCancel(Rec r);
        static partial void BatchShadowBegin(float td);
        static partial void BatchShadowEnd();
        static partial void BatchShadowScale(Rec r,Vector2 v);
        private static Vector2 pivotPosRef(scrDecoration d)=>d.pivotPosVec;
        private static Vector2 scaleRef(scrDecoration d)=>d.scaleVec;
        private static float rotRef(scrDecoration d)=>d.rotAngle;
    }
}
unsafe class Program {
    delegate float EvalFn(Ease e,EaseFunction custom,float t,float d,float over,float period);
    static readonly EvalFn dotween=(EvalFn)Delegate.CreateDelegate(typeof(EvalFn),typeof(Ease).Assembly.GetType("DG.Tweening.Core.Easing.EaseManager").GetMethod("Evaluate",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(Ease),typeof(EaseFunction),typeof(float),typeof(float),typeof(float),typeof(float)},null));
    static long checks;
    static void Assert(bool value,string text){checks++;if(!value)throw new Exception(text);}
    static bool Same(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    static void Main(string[] args){
        var module=NativeLibrary.Load(System.IO.Path.GetFullPath(args[0]));
        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly,(name,assembly,path)=>name=="sfnative"?module:IntPtr.Zero);
        Arithmetic(); Lifecycle(); SteadyAllocation(); Isolation(args);
        Console.WriteLine($"PASS assertions={checks}; runtime={RuntimeInformation.FrameworkDescription}; live-game measurements=0");
    }
    static void Arithmetic(){
        const int n=4096; var random=new Random(20261019);
        int[] es=new int[n],ks=new int[n],md=new int[n],nd=new int[n];
        float[] dur=new float[n],mp=new float[n],np=new float[n],rp=new float[n],s=new float[n*4],c=new float[n*4],mo=new float[n*4],no=new float[n*4],ov=new float[n],pe=new float[n];
        Ease[] ease={Ease.Linear,Ease.InQuad,Ease.OutQuad,Ease.InCubic,Ease.OutCubic,Ease.InOutQuad,Ease.InOutCubic};
        int[] key={1,2,5,7,8,9,10};
        foreach(var e in ease)foreach(var k in key){
            for(int i=0;i<n;i++){
                es[i]=DecoBatchShadow.EaseCode(e);ks[i]=k;dur[i]=(float)(random.NextDouble()*8+.001);
                rp[i]=mp[i]=np[i]=i%5==0?dur[i]:(float)random.NextDouble()*dur[i];
                for(int j=0;j<4;j++){s[i*4+j]=(float)(random.NextDouble()-.5)*1000;c[i*4+j]=(float)(random.NextDouble()-.5)*1000;}
                if(i%11==0){s[i*4]=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));c[i*4]=0;}
            }
            for(int f=0;f<12;f++){
                float td=f==0?-.02f:f==1?0:f==2?.0000001f:f==3?float.Epsilon:.016f;
                for(int i=0;i<n;i++){float next=rp[i]+td;rp[i]=next>=dur[i]?dur[i]:next;}
                DecoBatchShadow.ManagedStep(n,es,ks,dur,mp,s,c,mo,md,td);
                fixed(int* ep=es,kp=ks,dnp=nd)fixed(float* dp=dur,pp=np,sp=s,cp=c,op=no,ovp=ov,pep=pe)
                    Assert(DecoBatchShadow.sf_decobatch_step(n,ep,kp,dp,pp,sp,cp,ovp,pep,op,dnp,td)==1,"native return");
                for(int i=0;i<n;i++){
                    Assert(Same(mp[i],rp[i])&&Same(np[i],rp[i])&&md[i]==nd[i]&&md[i]==(rp[i]>=dur[i]?1:0),"clock/done");
                    float factor=dotween(e,null,mp[i],dur[i],1.70158f,0);
                    Assert(Same(DecoBatchShadow.Eval(es[i],mp[i],dur[i]),factor),$"ease {e} at {mp[i]:R}/{dur[i]:R}");
                    int lanes=k==9?4:k==7||k==8?2:1;
                    for(int j=0;j<lanes;j++){
                        int b=i*4+j;float expected=(k==7&&j==1)||(k==8&&j==0)?s[b]:s[b]+c[b]*factor;
                        Assert(Same(expected,mo[b]),"managed value");Assert(Same(expected,no[b]),"native value");
                    }
                }
            }
        }
        es[0]=-1; mp[0]=np[0]=.25f;mo[0]=no[0]=123;
        DecoBatchShadow.ManagedStep(1,es,ks,dur,mp,s,c,mo,md,1);
        fixed(int* ep=es,kp=ks,dnp=nd)fixed(float* dp=dur,pp=np,sp=s,cp=c,op=no,ovp=ov,pep=pe)
            Assert(DecoBatchShadow.sf_decobatch_step(1,ep,kp,dp,pp,sp,cp,ovp,pep,op,dnp,1)==1,"unsupported call");
        Assert(md[0]==-1&&nd[0]==-1&&mp[0]==.25f&&np[0]==.25f&&mo[0]==123&&no[0]==123,"unsupported untouched");
        es[0]=0;ks[0]=3;
        DecoBatchShadow.ManagedStep(1,es,ks,dur,mp,s,c,mo,md,1);
        fixed(int* ep=es,kp=ks,dnp=nd)fixed(float* dp=dur,pp=np,sp=s,cp=c,op=no,ovp=ov,pep=pe)
            Assert(DecoBatchShadow.sf_decobatch_step(1,ep,kp,dp,pp,sp,cp,ovp,pep,op,dnp,1)==1,"unsupported key call");
        Assert(md[0]==-1&&nd[0]==-1&&mp[0]==.25f&&np[0]==.25f&&mo[0]==123&&no[0]==123,"unsupported key untouched");
        Assert(DecoBatchShadow.sf_decobatch_step(-1,null,null,null,null,null,null,null,null,null,null,0)==0,"invalid count");
        Assert(DecoBatchShadow.sf_decobatch_step(0,null,null,null,null,null,null,null,null,null,null,0)==1,"empty batch");
        Console.WriteLine("Arithmetic: 49 ease/key combinations, 12 steps, 4096 rows, signed zero/negative delta/end/unsupported checked");
    }
    static void Lifecycle(){
        var d=new scrDecoration{scaleVec=new Vector2(2,3)};
        var x=new DecoAnim.Rec{D=d,Key=7,E=Ease.OutCubic,Dur=1,VStart=d.scaleVec,VChange=new Vector2(5,0)};
        var y=new DecoAnim.Rec{D=d,Key=8,E=Ease.InCubic,Dur=1,VStart=d.scaleVec,VChange=new Vector2(0,8)};
        DecoAnim.ProbeRecords.Add(x);DecoBatchShadow.Begin();
        Assert(x.BatchShadowSlot==0,"adopt started");
        DecoBatchShadow.FrameBegin(.25f);DecoBatchShadow.Add(y);
        foreach(var r in new[]{x,y}){
            DecoBatchShadow.ScaleInput(r,d.scaleVec);r.Pos=.25f;
            var v=d.scaleVec;float e=dotween(r.E,null,r.Pos,r.Dur,1.7f,0);
            if(r.Key==7)v.x=r.VStart.x+r.VChange.x*e;else v.y=r.VStart.y+r.VChange.y*e;
            r.VLast=d.scaleVec=v;
        }
        DecoBatchShadow.FrameEnd();
        var state=DecoBatchShadow.State();Console.WriteLine(state);
        Assert(state.Contains("managed checks/diff=2/0")&&state.Contains("native checks/diff=2/0"),"first ordered scale frame");
        Assert(d.scaleVec.x==x.VLast.x&&d.scaleVec.y==y.VLast.y,"shadow did not write fields");
        DecoBatchShadow.Remove(x);Assert(x.BatchShadowSlot==-1&&y.BatchShadowSlot==0,"swap removal");
        DecoBatchShadow.Remove(x);Assert(y.BatchShadowSlot==0,"double remove safe");
        DecoBatchShadow.FrameBegin(.75f);DecoBatchShadow.ScaleInput(y,d.scaleVec);y.Pos=1;y.Running=false;
        y.VLast=new Vector2(d.scaleVec.x,11);DecoBatchShadow.FrameEnd();
        state=DecoBatchShadow.State();Assert(state.Contains("slots=0")&&state.Contains("managed checks/diff=3/0")&&state.Contains("native checks/diff=3/0"),"done compare before release");
        y.Running=true;y.Pos=0;DecoBatchShadow.Add(y);Assert(y.BatchShadowSlot==0,"reuse after completion");
        DecoBatchShadow.Stop();Assert(y.BatchShadowSlot==-1,"stop clears slot and roots");DecoBatchShadow.Stop();
        DecoAnim.ProbeRecords.Clear();
        // Forced completion and cancellation are not normal delta steps; never compare stale output.
        DecoBatchShadow.Begin();DecoBatchShadow.Add(x);DecoBatchShadow.Remove(x);Assert(x.BatchShadowSlot==-1,"cancel outside frame");
        DecoBatchShadow.FrameBegin(.25f);DecoBatchShadow.Add(y);DecoBatchShadow.Remove(y);DecoBatchShadow.FrameEnd();
        Assert(DecoBatchShadow.State().Contains("checks/diff=0/0"),"cancel inside frame");DecoBatchShadow.Stop();
        // Capacity and decoration ID reuse; no unbounded growth over churn.
        DecoBatchShadow.Begin();var rows=new DecoAnim.Rec[DecoBatchShadow.Capacity+1];
        for(int i=0;i<rows.Length;i++){rows[i]=new DecoAnim.Rec{D=new scrDecoration(),Key=5,E=Ease.Linear,Dur=1};DecoBatchShadow.Add(rows[i]);}
        Assert(rows[^1].BatchShadowSlot==-1&&DecoBatchShadow.State().Contains("overflow=1"),"capacity guard");
        for(int i=0;i<rows.Length-1;i++)DecoBatchShadow.Remove(rows[i]);
        DecoBatchShadow.Add(rows[^1]);Assert(rows[^1].BatchShadowSlot==0,"decoration ID recycled");DecoBatchShadow.Stop();
        // Deliberate errors must be visible, including the non-first color component.
        DecoBatchShadow.Begin();var color=new DecoAnim.Rec{D=d,Key=9,E=Ease.Linear,Dur=1,CStart=new Color{r=1,g=2,b=3,a=4}};
        DecoBatchShadow.Add(color);DecoBatchShadow.FrameBegin(.25f);color.Pos=.25f;color.CLast=new Color{r=1,g=99,b=3,a=4};DecoBatchShadow.FrameEnd();
        state=DecoBatchShadow.State();Assert(state.Contains("managed checks/diff=1/1")&&state.Contains("native checks/diff=1/1")&&state.Contains("lane=1"),"injected color mismatch detected");
        DecoBatchShadow.Stop();DecoAnim.AblateMask=1;Assert(DecoBatchShadow.Begin().Contains("거부"),"ablation refuses begin");
        DecoAnim.AblateMask=0;DecoBatchShadow.Begin();DecoAnim.AblateMask=1;DecoBatchShadow.FrameBegin(.25f);
        Assert(!DecoBatchShadow.On&&DecoBatchShadow.State().Contains("shadowError=InvalidOperationException"),"live ablation stops shadow only");
        DecoAnim.AblateMask=0;DecoBatchShadow.Stop();
        Console.WriteLine("Lifecycle: adoption/new startup/ordered scale/normal completion/cancel/swap/reuse/stop/capacity checked");
    }
    static void Isolation(string[] args){
        if(args.Length<4)return;
        foreach(var path in args[1..]){
            using var file=System.IO.File.OpenRead(path);using var pe=new System.Reflection.PortableExecutable.PEReader(file);
            var md=System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            bool found=false,hook=false;
            foreach(var h in md.TypeDefinitions)if(md.GetString(md.GetTypeDefinition(h).Name)=="DecoBatchShadow")found=true;
            foreach(var h in md.MethodDefinitions)if(md.GetString(md.GetMethodDefinition(h).Name).StartsWith("BatchShadow"))hook=true;
            bool player=path.Contains("Player/")||path.Contains("Player\\");
            Assert(found!=player&&hook!=player,"Player isolation "+path);Console.WriteLine($"Isolation {path}: probe={found}, hooks={hook}");
            foreach(var h in md.ManifestResources){
                var resource=md.GetManifestResource(h);
                if(md.GetString(resource.Name)!="StutterFix.sfnative.dll")continue;
                var data=pe.GetSectionData(pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetContent();
                int offset=checked((int)resource.Offset);int length=BitConverter.ToInt32(data.AsSpan(offset,4));
                string embedded=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data.AsSpan(offset+4,length)));
                string built=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(args[0])));
                Assert(embedded==built,"embedded native hash "+path);Console.WriteLine($"Embedded native SHA256 {path}: {embedded}");
            }
        }
    }
    static void SteadyAllocation(){
        DecoBatchShadow.Begin();var rows=new DecoAnim.Rec[4096];var d=new scrDecoration();
        for(int i=0;i<rows.Length;i++){rows[i]=new DecoAnim.Rec{D=d,Key=5,E=Ease.OutCubic,Dur=100000};DecoBatchShadow.Add(rows[i]);}
        void Frame(){DecoBatchShadow.FrameBegin(.01f);for(int i=0;i<rows.Length;i++)rows[i].Pos+=.01f;DecoBatchShadow.FrameEnd();}
        for(int f=0;f<32;f++)Frame();
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<256;f++)Frame();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert(bytes==0,"steady shadow allocated "+bytes);var state=DecoBatchShadow.State();
        Assert(state.Contains("managed checks/diff=1179648/0")&&state.Contains("native checks/diff=1179648/0"),"steady persistent positions");
        Console.WriteLine($"Steady shadow: 4096 rows, 256 measured frames, allocation={bytes} bytes; managed/native mismatch=0; stubs/.NET8 only");
        DecoBatchShadow.Stop();
    }
}
