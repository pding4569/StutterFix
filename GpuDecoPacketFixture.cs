#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // Explicit AUTOTEST arithmetic experiment only. No live playback calls,
    // native resource ownership, renderer changes, or installed hooks.
    internal static unsafe class GpuDecoPacketFixture
    {
        [StructLayout(LayoutKind.Sequential)] struct Channel {internal float Start,Change,End;internal int Source;}
        [StructLayout(LayoutKind.Sequential)] struct Source {internal float Position,Duration,Easing;internal int Active;}
        [StructLayout(LayoutKind.Sequential)] struct Packet
        {internal fixed float Operand[16];internal float EndEase;internal int Source;internal uint Mask,Relative;}
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        static extern int sf_deco_packet_apply_v1(Channel* channels,int decorations,Source* sources,int sourceCount,
            Packet* packet,uint* targets,int targetCount,float* origins,int originCount,Channel* scratch,int scratchCount,ulong* dirty,int dirtyCount);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        static extern int sf_deco_packet_commit_static_v1(Channel* master,GpuDecoGpu.Anchor* anchors,int* bindings,int decorations,
            Channel* expected,Channel* slice,int sliceCount,uint* targets,uint* sliceTargets,int count,uint mask,bool* dirty,int dirtyCount);
        [StructLayout(LayoutKind.Sequential)] struct Animation{internal float Duration,Over,Period;internal uint Ease;}
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        static extern int sf_deco_packet_commit_animated_v1(Channel* master,GpuDecoGpu.Anchor* anchors,int* bindings,int decorations,
            Channel* expected,Channel* slice,int sliceCount,uint* targets,uint* sliceTargets,int count,
            Packet* packet,Animation* animation,Source* clocks,int clockCount,float* origins,int originCount,
            float* scratch,int scratchCount,bool* dirty,int dirtyCount);
        static int Call(Channel[] channels,Source[] sources,Packet packet,uint[] targets,float[] origins,Channel[] scratch,ulong[] dirty)
        {
            fixed(Channel* c=channels,s=scratch)fixed(Source* src=sources)fixed(uint* t=targets)
            fixed(float* o=origins)fixed(ulong* d=dirty)
                return sf_deco_packet_apply_v1(c,channels.Length/16,src,sources.Length,&packet,t,targets.Length,o,origins.Length,s,scratch.Length,d,dirty.Length);
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("packet fixture: "+message);}
        static bool Bits(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
        static float Current(Channel c,Source[] sources)
        {
            if(c.Source<0)return c.End;var s=sources[c.Source];
            if(s.Active==0||s.Position>=s.Duration)return c.End;
            if(s.Position<=0)return c.Start;
            double product=(double)c.Change*(double)s.Easing;
            return (float)((double)c.Start+product);
        }
        // Matches current ApplyGpuLiveEffectToTarget's channel arithmetic;
        // source membership/order, anchors and Unity metadata are NOT modeled.
        static void Reference(Channel[] channels,Source[] sources,Packet p,uint[] targets,float[] origins,ulong[] dirty)
        {
            if(p.Mask==0)return;
            foreach(uint target in targets)
            {
                for(int c=0;c<16;c++)if((p.Mask&(1u<<c))!=0)
                {
                    int at=(int)target*16+c;var old=channels[at];float value=p.Operand[c];
                    if(c<2){float origin=p.Relative!=0?Current(old,sources):origins[(int)target*2+c];value=origin+value;}
                    bool scale=c==5||c==6||c==12||c==13;float end=value;
                    // Keep the exact existing expression: introducing float
                    // locals here changes Mono's intermediate rounding.
                    if(scale)end=old.End+(value-old.End)*p.EndEase;
                    channels[at]=p.Source<0?new Channel{Start=end,Change=0,End=end,Source=-1}
                        :new Channel{Start=old.End,Change=value-old.End,End=end,Source=p.Source};
                }
                int block=(int)target>>4;dirty[block>>6]|=1ul<<(block&63);
            }
        }
        static void Equal(Channel[] a,Channel[] b,ulong[] ad,ulong[] bd)
        {
            Require(a.Length==b.Length&&ad.Length==bd.Length,"shape mismatch");
            for(int i=0;i<a.Length;i++)Require(a[i].Source==b[i].Source&&Bits(a[i].Start,b[i].Start)
                &&Bits(a[i].Change,b[i].Change)&&Bits(a[i].End,b[i].End),"channel bits at "+i
                +" native="+Describe(a[i])+" mono="+Describe(b[i]));
            for(int i=0;i<ad.Length;i++)Require(ad[i]==bd[i],"dirty bits");
        }
        static float Value(Random r)=>r.Next(-100000,100001)*.03125f;
        static string Describe(Channel c)=>BitConverter.SingleToInt32Bits(c.Start).ToString("X8")+","+
            BitConverter.SingleToInt32Bits(c.Change).ToString("X8")+","+BitConverter.SingleToInt32Bits(c.End).ToString("X8")+","+c.Source;
        static int StaticCommitFixture()
        {
            Require(Marshal.SizeOf<GpuDecoGpu.Anchor>()==48,"static anchor ABI");
            const int n=32,sn=40;uint[] targets={7,0,31},items={1,30,6};var random=new Random(0xA12);
            for(int test=0;test<128;test++)
            {
                var master=new Channel[n*16];var expected=new Channel[n*16];var destination=new Channel[sn*16];
                var anchors=new GpuDecoGpu.Anchor[n*16];var bindings=new int[n*16];var dirty=new bool[(sn+15)/16];
                uint mask=test<16?1u<<test:(uint)random.Next(1,65536);
                for(int i=0;i<master.Length;i++)
                {master[i]=new Channel{Start=-12,End=6,Change=18,Source=3};expected[i]=new Channel{Start=i*.125f,End=i*.125f,Source=-1};bindings[i]=-1;}
                var oracle=(Channel[])master.Clone();var sliceOracle=(Channel[])destination.Clone();
                foreach(uint m in targets)for(int c=0;c<16;c++)if((mask&(1u<<c))!=0)oracle[m*16+c]=expected[m*16+c];
                for(int k=0;k<targets.Length;k++)for(int c=0;c<16;c++)if((mask&(1u<<c))!=0)sliceOracle[items[k]*16+c]=expected[targets[k]*16+c];
                fixed(Channel* m=master,e=expected,s=destination)fixed(GpuDecoGpu.Anchor* a=anchors)fixed(int* b=bindings)
                fixed(uint* t=targets,si=items)fixed(bool* d=dirty)
                {
                    Require(sf_deco_packet_commit_static_v1(m,a,b,n,e,s,sn,t,si,targets.Length,mask,d,dirty.Length)==1,"static commit");
                    Equal(master,oracle,Array.Empty<ulong>(),Array.Empty<ulong>());Equal(destination,sliceOracle,Array.Empty<ulong>(),Array.Empty<ulong>());
                    Require(dirty[0]&&dirty[1]&&!dirty[2],"static dirty blocks");
                    for(int i=0;i<anchors.Length;i++)
                    {
                        int ch=i&15;bool written=Array.IndexOf(targets,(uint)(i/16))>=0&&(mask&(1u<<ch))!=0;
                        var v=anchors[i];var seg=v.segment;
                        Require(v.valid==(written?1u:0u)&&v.clock==0&&seg.hi==0&&seg.lo==0&&seg.duration==0
                            &&seg.over==0&&seg.period==0&&seg.ease==(written?1u:0u)
                            &&seg.scale==(written&&(ch==5||ch==6||ch==12||ch==13)?1u:0u)
                            &&Bits(seg.start,written?expected[i].End:0)&&Bits(seg.end,seg.start)&&Bits(seg.target,seg.start),"static anchor "+i);
                    }
                    // Rejection at the LAST destination must not partially
                    // commit the first target or any mirror/anchor/dirty byte.
                    var before=(Channel[])master.Clone();var anchorBefore=(GpuDecoGpu.Anchor[])anchors.Clone();
                    var destBefore=(Channel[])destination.Clone();var dirtyBefore=(bool[])dirty.Clone();
                    uint saved=si[2];si[2]=sn;
                    Require(sf_deco_packet_commit_static_v1(m,a,b,n,e,s,sn,t,si,targets.Length,mask,d,dirty.Length)==-3,"static rejection");si[2]=saved;
                    Equal(master,before,Array.Empty<ulong>(),Array.Empty<ulong>());Equal(destination,destBefore,Array.Empty<ulong>(),Array.Empty<ulong>());
                    byte* x=(byte*)a;
                    fixed(GpuDecoGpu.Anchor* old=anchorBefore)for(int i=0;i<anchors.Length*48;i++)Require(x[i]==((byte*)old)[i],"static rejected anchor byte");
                    for(int i=0;i<dirty.Length;i++)Require(dirty[i]==dirtyBefore[i],"static rejected dirty byte");
                    Require(sf_deco_packet_commit_static_v1(m,a,b,n,m,s,sn,t,si,targets.Length,mask,d,dirty.Length)==-2,"static alias rejection");
                }
            }
            return 128;
        }
        static int AnimatedCommitFixture()
        {
            Require(Marshal.SizeOf<Animation>()==16,"animated ABI");
            const int n=32,sn=40;uint[] targets={7,0,31},items={1,30,6};var random=new Random(0xD12);
            for(int test=0;test<128;test++)
            {
                var master=new Channel[n*16];var destination=new Channel[sn*16];var anchors=new GpuDecoGpu.Anchor[n*16];
                var sources=new Source[8];var bindings=new int[n*16];var origins=new float[n*2];var dirty=new bool[(sn+15)/16];
                var scratch=new Channel[targets.Length*16];var targetScratch=new float[targets.Length*16];var flags=new ulong[1];
                for(int i=0;i<master.Length;i++)
                {float start=Value(random),change=Value(random);master[i]=new Channel{Start=start,Change=change,End=start+change,Source=i%7};bindings[i]=-1;}
                for(int i=0;i<sources.Length;i++)sources[i]=new Source{Position=test%4==0?0:1,Duration=2,Easing=.75f,Active=1};
                sources[7]=new Source{Position=0,Duration=2,Active=1};
                for(int i=0;i<origins.Length;i++)origins[i]=Value(random);
                Packet p=default;p.Mask=test<16?1u<<test:(uint)random.Next(1,65536);p.Source=7;p.Relative=(uint)(test&1);
                p.EndEase=test%3==0?1:test%3==1?1.25f:-.25f;for(int c=0;c<16;c++)p.Operand[c]=Value(random);
                var expected=(Channel[])master.Clone();var reference=(Channel[])master.Clone();
                Require(Call(expected,sources,p,targets,origins,scratch,flags)==1,"animated arithmetic");
                var rf=new ulong[1];Reference(reference,sources,p,targets,origins,rf);Equal(expected,reference,flags,rf);
                var wanted=new GpuDecoGpu.Anchor[n*16];var sliceOracle=new Channel[sn*16];
                for(int i=0;i<targets.Length;i++)for(int c=0;c<16;c++)if((p.Mask&(1u<<c))!=0)
                {
                    int at=(int)targets[i]*16+c;float target=p.Operand[c];
                    if(c<2)target=(p.Relative!=0?Current(master[at],sources):origins[targets[i]*2+c])+target;
                    wanted[at]=new GpuDecoGpu.Anchor{valid=1,clock=7,segment=new GpuDecoGpu.Segment{duration=2,start=expected[at].Start,end=expected[at].End,
                        target=target,over=1.7f,period=.3f,ease=1,scale=c==5||c==6||c==12||c==13?1u:0u}};
                    sliceOracle[items[i]*16+c]=expected[at];
                }
                Animation animation=new Animation{Duration=2,Over=1.7f,Period=.3f,Ease=1};
                fixed(Channel* m=master,e=expected,s=destination)fixed(GpuDecoGpu.Anchor* a=anchors)fixed(int* b=bindings)
                fixed(uint* t=targets,si=items)fixed(Source* clocks=sources)fixed(float* o=origins,work=targetScratch)fixed(bool* d=dirty)
                {
                    uint saved=si[2];si[2]=sn;
                    Require(sf_deco_packet_commit_animated_v1(m,a,b,n,e,s,sn,t,si,targets.Length,&p,&animation,clocks,sources.Length,o,origins.Length,
                        work,targetScratch.Length,d,dirty.Length)==-3,"animated last target rejection");si[2]=saved;
                    Require(!dirty[0]&&!dirty[1]&&!dirty[2],"animated rejected dirty");
                    for(int i=0;i<master.Length;i++)Require(b[i]==-1&&a[i].valid==0,"animated rejected metadata");
                    var untouched=(Channel[])master.Clone();
                    Require(sf_deco_packet_commit_animated_v1(m,a,b,n,e,s,sn,t,si,targets.Length,&p,&animation,clocks,sources.Length,o,origins.Length,
                        o,origins.Length,d,dirty.Length)==-2,"animated scratch alias rejection");
                    Equal(master,untouched,Array.Empty<ulong>(),Array.Empty<ulong>());
                    Require(sf_deco_packet_commit_animated_v1(m,a,b,n,e,s,sn,t,si,targets.Length,&p,&animation,clocks,sources.Length,o,origins.Length,
                        work,targetScratch.Length,d,dirty.Length)==1,"animated commit");
                    Equal(master,expected,Array.Empty<ulong>(),Array.Empty<ulong>());Equal(destination,sliceOracle,Array.Empty<ulong>(),Array.Empty<ulong>());
                    Require(dirty[0]&&dirty[1]&&!dirty[2],"animated dirty");
                    fixed(GpuDecoGpu.Anchor* w=wanted)for(int i=0;i<anchors.Length*48;i++)Require(((byte*)a)[i]==((byte*)w)[i],"animated anchor byte "+i);
                    for(int i=0;i<bindings.Length;i++)Require(b[i]==(Array.IndexOf(targets,(uint)(i/16))>=0&&(p.Mask&(1u<<(i&15)))!=0?7:-1),"animated binding");
                }
            }
            return 128;
        }
        internal static string Run()
        {
            Require(Marshal.SizeOf<Channel>()==16&&Marshal.SizeOf<Source>()==16&&Marshal.SizeOf<Packet>()==80,"ABI size");
            const int decorations=128,sourceCount=8;var random=new Random(0xB4A3);
            var actual=new Channel[decorations*16];var sources=new Source[sourceCount];var origins=new float[decorations*2];
            for(int i=0;i<actual.Length;i++)
            {float start=Value(random),change=Value(random);actual[i]=new Channel{Start=start,Change=change,End=start+change,Source=i%3==0?i%(sourceCount-1):-1};}
            var expected=(Channel[])actual.Clone();var scratch=new Channel[decorations*16];
            var dirty=new ulong[1];var expectedDirty=new ulong[1];int events=0;
            for(int i=0;i<origins.Length;i++)origins[i]=Value(random);
            for(int step=0;step<1024;step++)
            {
                for(int i=0;i<sources.Length;i++)sources[i]=new Source{Duration=2,Position=step%5==0?0:step%5==1?3:random.Next(1,199)*.01f,
                    Easing=(random.Next(0,301)-100)*.01f,Active=(step+i)%7==0?0:1};
                // A fresh allocated source starts at zero. Allocation/reuse
                // policy itself is outside this arithmetic fixture's scope.
                sources[7]=new Source{Duration=2,Position=0,Easing=0,Active=1};
                Packet p=default;p.Mask=(uint)random.Next(0,65536);p.Relative=(uint)(step&1);p.Source=(step&2)==0?-1:7;
                p.EndEase=step%4==0?0:step%4==1?1:step%4==2?1.25f:-.25f;
                for(int c=0;c<16;c++)p.Operand[c]=Value(random);
                int count=random.Next(0,decorations+1);var permutation=new uint[decorations];
                for(int i=0;i<decorations;i++)permutation[i]=(uint)i;
                for(int i=decorations-1;i>0;i--){int j=random.Next(i+1);uint v=permutation[i];permutation[i]=permutation[j];permutation[j]=v;}
                var targets=new uint[count];Array.Copy(permutation,targets,count);
                var before=(Channel[])expected.Clone();
                Require(Call(actual,sources,p,targets,origins,scratch,dirty)==1,"valid packet rejected");
                Reference(expected,sources,p,targets,origins,expectedDirty);
                try{Equal(actual,expected,dirty,expectedDirty);}catch(Exception ex)
                {
                    int at=0;while(at<actual.Length && Bits(actual[at].End,expected[at].End))at++;
                    int c=at%16;float old=before[at].End,value=p.Operand[c];
                    float full=(float)((double)old+((double)value-(double)old)*p.EndEase);
                    throw new InvalidOperationException(ex.Message+" step="+step+" old="+old.ToString("R",CultureInfo.InvariantCulture)
                        +" operand="+value.ToString("R",CultureInfo.InvariantCulture)+" ease="+p.EndEase
                        +" double="+BitConverter.SingleToInt32Bits(full).ToString("X8"));
                }
                events++;
            }
            // Same-frame overwrite / signed zero, with no frame-clock advance.
            int signCases=0;foreach(float sign in new[]{0f,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000))})
            {
                for(int i=0;i<actual.Length;i++)actual[i]=new Channel{Start=sign,End=sign,Source=-1};
                expected=(Channel[])actual.Clone();Array.Clear(dirty,0,dirty.Length);Array.Clear(expectedDirty,0,expectedDirty.Length);
                Packet p=default;p.Mask=65535;p.EndEase=1;p.Source=-1;
                for(int c=0;c<16;c++)p.Operand[c]=sign;
                uint[] targets={7,2,17};
                for(int i=0;i<3;i++){p.Relative=(uint)(i&1);Require(Call(actual,sources,p,targets,origins,scratch,dirty)==1,"signed zero rejected");
                    Reference(expected,sources,p,targets,origins,expectedDirty);Equal(actual,expected,dirty,expectedDirty);signCases++;}
            }
            Packet bad=default;bad.Mask=1;bad.EndEase=1;bad.Source=-1;
            int rejected=0;
            Action<Packet,uint[],Channel[],ulong[],int> reject=(p,targets,work,flags,code)=>
            {
                var before=(Channel[])actual.Clone();var beforeDirty=(ulong[])dirty.Clone();
                int result=Call(actual,sources,p,targets,origins,work,flags);
                Require(result==code,"rejection status "+result+" expected "+code);
                Equal(actual,before,dirty,beforeDirty);rejected++;
            };
            reject(bad,new uint[]{1,1},scratch,dirty,-3); // Never sort/deduplicate silently.
            reject(bad,new uint[]{1,128},scratch,dirty,-3);
            reject(bad,new uint[]{1,2},new Channel[16],dirty,-1);
            reject(bad,new uint[]{1},scratch,Array.Empty<ulong>(),-1);
            reject(bad,new uint[]{1},actual,dirty,-2);
            bad.Operand[0]=float.NaN;reject(bad,new uint[]{1},scratch,dirty,-4);bad.Operand[0]=0;
            actual[16].Source=sourceCount;reject(bad,new uint[]{0,1},scratch,dirty,-4);actual[16].Source=-1;
            bad.Source=sourceCount;reject(bad,new uint[]{1},scratch,dirty,-1);bad.Source=-1;
            bad.Mask=65536;reject(bad,new uint[]{1},scratch,dirty,-1);bad.Mask=1;
            bad.Relative=2;reject(bad,new uint[]{1},scratch,dirty,-1);bad.Relative=0;
            float saved=origins[2];origins[2]=float.NaN;reject(bad,new uint[]{0,1},scratch,dirty,-4);origins[2]=saved;
            origins[2]=float.MaxValue;bad.Operand[0]=float.MaxValue;reject(bad,new uint[]{0,1},scratch,dirty,-4);origins[2]=saved;
            int staticCases=StaticCommitFixture();
            int animatedCases=AnimatedCommitFixture();
            // Native call + full preflight/scratch/commit, NOT a gameplay timing.
            var bench=new Channel[4096*16];for(int i=0;i<bench.Length;i++)bench[i].Source=-1;
            var benchScratch=new Channel[4000*16];var benchTargets=new uint[4000];for(int i=0;i<4000;i++)benchTargets[i]=(uint)i;
            var benchOrigins=new float[4096*2];var benchDirty=new ulong[4];Packet bp=default;bp.Mask=0x33;bp.Source=-1;bp.EndEase=1;
            for(int c=0;c<16;c++)bp.Operand[c]=c*.125f;
            for(int i=0;i<16;i++)Require(Call(bench,sources,bp,benchTargets,benchOrigins,benchScratch,benchDirty)==1,"benchmark warmup");
            long begin=Stopwatch.GetTimestamp();for(int i=0;i<128;i++)Require(Call(bench,sources,bp,benchTargets,benchOrigins,benchScratch,benchDirty)==1,"benchmark packet");
            double ms=(Stopwatch.GetTimestamp()-begin)*1000.0/Stopwatch.Frequency/128;
            return "packet_fixture events="+events+" signed_overwrites="+signCases+" rejects="+rejected+" mismatch=0"
                +" static_commit_cases="+staticCases+" static_reject_cases="+staticCases*2
                +" animated_commit_cases="+animatedCases+" animated_reject_cases="+animatedCases*2
                +" native_4000x4_mean_ms="+ms.ToString("F6",CultureInfo.InvariantCulture)
                +" scope=arithmetic_only live_integration=False GPU_time=UNMEASURED";
        }
    }
}
#endif
