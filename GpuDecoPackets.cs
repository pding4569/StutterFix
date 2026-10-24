#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using LiveChannel = StutterFix.GpuDecoGpu.LiveChannel;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Research stage: compile event operands and ordered ownership runs at
        // preparation time. Shadow mode uses the real shared clocks but writes
        // a separate channel buffer. It does not change rendering or clocks.
        sealed class GpuEventPacket
        {
            internal DecoBakeCore.Event Event;
            internal scrDecoration[] Targets;
            internal PacketRun[] Runs;
            internal int[] RunAt;
            internal uint Mask;
            internal PacketTargetGuard TargetGuard;
        }
        sealed class PacketRun
        {
            internal int First;
            internal uint[] Masters;
            internal uint[] SliceItems;
            internal int[] Slots;
            internal int MinBlock,MaxBlock;
        }
        [StructLayout(LayoutKind.Sequential)] struct PacketClock
        {internal float Position,Duration,Easing;internal int Active;}
        [StructLayout(LayoutKind.Sequential)] unsafe struct PacketArgs
        {internal fixed float Operand[16];internal float EndEase;internal int Source;internal uint Mask,Relative;}
        [StructLayout(LayoutKind.Sequential)] struct PacketAnimation
        {internal float Duration,Over,Period;internal uint Ease;}
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        static extern unsafe int sf_deco_packet_apply_v1(LiveChannel* channels,int decorations,PacketClock* sources,int sourceCount,
            PacketArgs* packet,uint* targets,int targetCount,float* origins,int originCount,LiveChannel* scratch,int scratchCount,ulong* dirty,int dirtyCount);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        static extern unsafe int sf_deco_packet_commit_static_v1(LiveChannel* master,GpuDecoGpu.Anchor* anchors,int* bindings,int decorations,
            LiveChannel* expected,LiveChannel* slice,int sliceCount,uint* targets,uint* sliceTargets,int count,uint mask,bool* dirty,int dirtyCount);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        static extern unsafe int sf_deco_packet_commit_animated_v1(LiveChannel* master,GpuDecoGpu.Anchor* anchors,int* bindings,int decorations,
            LiveChannel* expected,LiveChannel* slice,int sliceCount,uint* targets,uint* sliceTargets,int count,
            PacketArgs* packet,PacketAnimation* animation,PacketClock* clocks,int clockCount,float* origins,int originCount,
            float* scratch,int scratchCount,bool* dirty,int dirtyCount);

        static readonly Dictionary<int,GpuEventPacket> gpuPackets=new Dictionary<int,GpuEventPacket>();
        static readonly HashSet<int> gpuPacketAmbiguous=new HashSet<int>();
        static LiveChannel[] gpuPacketShadow=Array.Empty<LiveChannel>(),gpuPacketScratch=Array.Empty<LiveChannel>();
        static float[] gpuPacketOrigins=Array.Empty<float>();
        static float[] gpuPacketTargetScratch=Array.Empty<float>();
        static GpuDecoGpu.Anchor[] gpuPacketExpectedAnchors=Array.Empty<GpuDecoGpu.Anchor>();
        static PacketClock[] gpuPacketClocks=Array.Empty<PacketClock>();
        static ulong[] gpuPacketDirty=Array.Empty<ulong>();
        static int[] gpuPacketOldBindings=Array.Empty<int>(),gpuPacketOldBindingIndices=Array.Empty<int>();
        static DecPlacementType[] gpuPacketOldPlacement=Array.Empty<DecPlacementType>();
        static Vector2[] gpuPacketOldStart=Array.Empty<Vector2>();
        static int gpuPacketGeneration=-1,gpuPacketMode;
        static int gpuPacketClockFrame=-1;
        static GpuEventPacket gpuPacketCurrent;
        static long gpuPacketCalls,gpuPacketTargets,gpuPacketChannels,gpuPacketMismatches,gpuPacketRejected,gpuPacketUnplanned;
        static long gpuPacketCommitted,gpuPacketCommittedTargets;
        static long gpuPacketAnimated,gpuPacketAnimatedTargets;
        static long gpuPacketRollbackChecks;
        static bool gpuPacketRejectOnce;
        static bool gpuPacketRejectAnimated;
        static string gpuPacketFirstError="none";

        static void ClearGpuPackets()
        {
            gpuPackets.Clear();gpuPacketAmbiguous.Clear();gpuPacketCurrent=null;gpuPacketGeneration=-1;
            gpuPacketTargetExpected=null;
            gpuPacketShadow=Array.Empty<LiveChannel>();gpuPacketScratch=Array.Empty<LiveChannel>();
            gpuPacketOrigins=Array.Empty<float>();gpuPacketClocks=Array.Empty<PacketClock>();gpuPacketDirty=Array.Empty<ulong>();
            gpuPacketOldBindings=Array.Empty<int>();gpuPacketOldBindingIndices=Array.Empty<int>();gpuPacketOldPlacement=Array.Empty<DecPlacementType>();gpuPacketOldStart=Array.Empty<Vector2>();
            gpuPacketClockFrame=-1;
            gpuPacketRejectOnce=false;
            gpuPacketTargetScratch=Array.Empty<float>();gpuPacketExpectedAnchors=Array.Empty<GpuDecoGpu.Anchor>();
        }
        static void CompileGpuPackets()
        {
            ClearGpuPackets();
            if(table==null || table.InputEvents==null || gpuLiveOwnedIndices.Length>32768)return;
            int maxRun=0;long references=0;
            var currentEffects=new Dictionary<int,ffxMoveDecorationsPlus>();
            if(gpuPacketMode>=6 && effectList!=null)foreach(var fx in effectList)
                if(fx is ffxMoveDecorationsPlus move)currentEffects[move.GetInstanceID()]=move;
            foreach(var e in table.InputEvents)
            {
                if(e.Reasons.Length!=0 || e.Targets==null || !GpuDecoGpu.Supported(e.Ease))continue;
                if(gpuPacketAmbiguous.Contains(e.Source))continue;
                if(gpuPackets.ContainsKey(e.Source))
                {gpuPackets.Remove(e.Source);gpuPacketAmbiguous.Add(e.Source);continue;}
                var plan=new GpuEventPacket{Event=e,Targets=new scrDecoration[e.Targets.Length],RunAt=new int[e.Targets.Length]};
                for(int c=0;c<16;c++)if(!float.IsNaN(e.Values[c]))plan.Mask|=1u<<c;
                for(int i=0;i<plan.RunAt.Length;i++)plan.RunAt[i]=-1;
                var runs=new List<PacketRun>();var seen=new HashSet<int>();bool valid=true;
                for(int i=0;i<e.Targets.Length;)
                {
                    int first=i;var owned=new List<uint>();
                    while(i<e.Targets.Length)
                    {
                        int baseline=e.Targets[i];
                        if((uint)baseline>=(uint)baselineObjects.Length || !seen.Add(baseline)){valid=false;break;}
                        var d=baselineObjects[baseline];plan.Targets[i]=d;
                        bool own=gpuLiveMasterByDeco.TryGetValue(d,out int master);
                        if(!own)
                        {
                            // Leave CPU targets at precisely their original positions.
                            i++;break;
                        }
                        if(owned.Count==0)first=i;
                        owned.Add((uint)master);i++;
                    }
                    if(!valid)break;
                    if(owned.Count!=0)
                    {
                        var run=new PacketRun{First=first,Masters=owned.ToArray(),MinBlock=int.MaxValue,MaxBlock=-1};
                        int channels=0;for(int c=0;c<16;c++)if((plan.Mask&(1u<<c))!=0)channels++;
                        run.Slots=new int[owned.Count*channels];int slotAt=0;
                        foreach(uint m in run.Masters)for(int c=0;c<16;c++)if((plan.Mask&(1u<<c))!=0)run.Slots[slotAt++]=(int)m*16+c;
                        if(gpuLiveSlices.Count==1)
                        {
                            run.SliceItems=new uint[owned.Count];
                            for(int k=0;k<owned.Count;k++)
                            {
                                int item=gpuLiveMasterToSliceItem[owned[k]][0];
                                if(item<0){run.SliceItems=null;break;}
                                run.SliceItems[k]=(uint)item;run.MinBlock=Math.Min(run.MinBlock,item>>4);run.MaxBlock=Math.Max(run.MaxBlock,item>>4);
                            }
                        }
                        plan.RunAt[first]=runs.Count;runs.Add(run);
                        maxRun=Math.Max(maxRun,owned.Count);references+=owned.Count;
                    }
                }
                if(!valid || runs.Count==0 || plan.Mask==0)continue;
                plan.Runs=runs.ToArray();
                if(currentEffects.TryGetValue(e.Source,out var actual))plan.TargetGuard=CapturePacketTargetGuard(actual,plan);
                gpuPackets.Add(e.Source,plan);
            }
            gpuPacketShadow=new LiveChannel[gpuMasterChannels.Length];gpuPacketScratch=new LiveChannel[maxRun*16];
            gpuPacketOrigins=new float[gpuLiveOwnedIndices.Length*2];gpuPacketClocks=new PacketClock[MaxLiveSources];
            gpuPacketOldBindings=new int[maxRun*16];gpuPacketOldBindingIndices=new int[maxRun*16];gpuPacketOldPlacement=new DecPlacementType[maxRun];gpuPacketOldStart=new Vector2[maxRun];
            gpuPacketTargetScratch=new float[maxRun*16];gpuPacketExpectedAnchors=new GpuDecoGpu.Anchor[maxRun*16];
            gpuPacketDirty=new ulong[(gpuLiveOwnedIndices.Length+1023)/1024];gpuPacketGeneration=generation;
            GpuLog("packets_compiled events="+gpuPackets.Count+" owned_references="+references+" max_run="+maxRun
                +" ambiguous_sources="+gpuPacketAmbiguous.Count+" generation="+generation+" mode="+gpuPacketMode);
        }
        static bool SamePacketInput(DecoBakeCore.Event a,DecoBakeCore.Event b)
        {
            if(a.T0!=b.T0 || a.StartTime!=b.StartTime || a.StartEffectOffset!=b.StartEffectOffset || a.AngleOffset!=b.AngleOffset
                || a.Duration!=b.Duration || a.Ease!=b.Ease || a.Over!=b.Over || a.Period!=b.Period
                || a.RelativePosition!=b.RelativePosition || a.ResetGlobalOrigin!=b.ResetGlobalOrigin || b.Reasons.Length!=0)return false;
            for(int c=0;c<16;c++)if(BitConverter.SingleToInt32Bits(a.Values[c])!=BitConverter.SingleToInt32Bits(b.Values[c]))return false;
            return true;
        }
        internal static bool BeginGpuPacket(ffxMoveDecorationsPlus fx,List<scrDecoration> targets)
        {
            gpuPacketCurrent=null;
            if(fx.imageFilenameUsed)return false; // Image side effects remain in target order.
            if(gpuPacketMode==0 || !gpuLiveActive || gpuPacketGeneration!=generation)return false;
            if(!gpuPackets.TryGetValue(fx.GetInstanceID(),out var p)){gpuPacketUnplanned++;return false;}
            if(targets.Count!=p.Targets.Length)
            {
                if(ReferenceEquals(gpuPacketTargetExpected,p))PacketError("prepared_target_count source="+p.Event.Source);
                gpuPacketRejected++;return false;
            }
            bool trusted=ReferenceEquals(gpuPacketTargetExpected,p)&&p.TargetGuard!=null&&ReferenceEquals(targets,p.TargetGuard.Cache)
                &&packetRowVersion(targets)==p.TargetGuard.CacheVersion;
            if(!trusted)for(int i=0;i<targets.Count;i++)if(!ReferenceEquals(targets[i],p.Targets[i]))
            {
                if(ReferenceEquals(gpuPacketTargetExpected,p))PacketError("prepared_target_oracle source="+p.Event.Source);
                gpuPacketRejected++;return false;
            }
            if(gpuPacketMode==7&&ReferenceEquals(gpuPacketTargetExpected,p))gpuPacketTargetOracle++;
            // Instance IDs alone are not identities: FfxReuse can recycle them.
            // Validate the immutable snapshot against this invocation as well.
            // A diagnostic must not turn the old per-target recovery path into
            // an early whole-effect abort. Let that existing path handle a
            // rejected invocation, without changing any target here.
            try{PrepareGpuEffect(fx);}catch(Exception){gpuPacketRejected++;return false;}
            if(!SamePacketInput(p.Event,gpuCurrentEvent)){gpuPacketRejected++;return false;}
            gpuPacketCurrent=p;return true;
        }
        internal static unsafe int TryGpuPacketRun(ffxMoveDecorationsPlus fx,List<scrDecoration> targets,int first,int keys)
        {
            var p=gpuPacketCurrent;
            if(p==null || !gpuLiveActive || (uint)first>=(uint)p.RunAt.Length || p.RunAt[first]<0)return 0;
            var run=p.Runs[p.RunAt[first]];
            if(gpuPacketMode==3 && p.Event.Duration>0f)return 0;
            if(targets.Count<first+run.Masters.Length)return 0;
            for(int i=0;i<run.Masters.Length;i++)if(!ReferenceEquals(targets[first+i],p.Targets[first+i]))return 0;
            for(int i=0;i<run.Masters.Length;i++)if(GpuCpuFallback((int)run.Masters[i]))return 0;
            PacketArgs args=default;
            try
            {
                for(int i=0;i<run.Masters.Length;i++)
                {
                    int m=(int)run.Masters[i];
                    Array.Copy(gpuMasterChannels,(int)m*16,gpuPacketShadow,(int)m*16,16);
                    var d=targets[first+i];
                    var row=table.Decorations[gpuLiveOwnedIndices[m]];
                    gpuPacketOrigins[m*2]=p.Event.ResetGlobalOrigin?row.GlobalX:d.startPos.x;
                    gpuPacketOrigins[m*2+1]=p.Event.ResetGlobalOrigin?row.GlobalY:d.startPos.y;
                }
                if(gpuPacketClockFrame!=Time.frameCount)for(int i=0;i<gpuSourceHighWater;i++)
                {
                    UpdatePacketClock(gpuSourcePool[i]);
                }
                gpuPacketClockFrame=Time.frameCount;
                args.Mask=p.Mask;args.Relative=p.Event.RelativePosition?1u:0u;
                args.Source=gpuCurrentAnimSource==null?-1:gpuCurrentAnimSource.Slot;
                bool scale=(p.Mask&0x3060u)!=0;
                args.EndEase=scale?ZeroTween.Eval((DG.Tweening.Ease)p.Event.Ease,args.Source<0?1f:p.Event.Duration,
                    args.Source<0?1f:p.Event.Duration,p.Event.Over,p.Event.Period):1f;
                for(int c=0;c<16;c++)args.Operand[c]=p.Event.Values[c];
                int result;
                fixed(LiveChannel* channels=gpuPacketShadow,scratch=gpuPacketScratch)fixed(PacketClock* clocks=gpuPacketClocks)
                fixed(uint* indices=run.Masters)fixed(float* origins=gpuPacketOrigins)fixed(ulong* dirty=gpuPacketDirty)
                    result=sf_deco_packet_apply_v1(channels,gpuLiveOwnedIndices.Length,clocks,gpuSourceHighWater,&args,
                        indices,run.Masters.Length,origins,gpuPacketOrigins.Length,scratch,gpuPacketScratch.Length,dirty,gpuPacketDirty.Length);
                if(result!=1){PacketError("native_status="+result);return 0;}
            }
            catch(Exception ex){PacketError("native_exception="+ex.GetType().Name);return 0;}
            if(gpuPacketMode>=2 && (p.Event.Duration==0f || gpuPacketMode>=4) && run.SliceItems!=null && gpuLiveSlices.Count==1
                && gpuLiveSlices[0].Gpu!=null && gpuLiveSlices[0].Gpu.HasNativeLiveKernel)
            {
                if((gpuPacketMode==2 || gpuPacketMode==4 ||gpuPacketMode==7) && !ValidatePacketChannels(p,run,args))return 0;
                return CommitGpuPacket(p,run,targets,first,args,keys);
            }
            int processed=0;
            for(int i=0;i<run.Masters.Length;i++)
            {
                if(!GpuLiveSkip(fx,targets[first+i],keys))break;
                processed++;int m=(int)run.Masters[i];
                for(int c=0;c<16;c++)if((p.Mask&(1u<<c))!=0)
                {
                    var a=gpuMasterChannels[m*16+c];var b=gpuPacketShadow[m*16+c];gpuPacketChannels++;
                    if(a.sourceIndex!=b.sourceIndex || !PacketBits(a.start,b.start) || !PacketBits(a.change,b.change) || !PacketBits(a.end,b.end))
                        PacketError("channel="+c+" target="+m+" source="+p.Event.Source+" song="+
                            (scrConductor.instance?scrConductor.instance.songposition_minusi:double.NaN).ToString("F6",Inv));
                }
            }
            gpuPacketCalls++;gpuPacketTargets+=processed;return processed;
        }
        static bool PacketBits(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
        static bool ValidatePacketChannels(GpuEventPacket p,PacketRun run,PacketArgs args)
        {
            float ease=args.EndEase;bool anim=args.Source>=0;
            bool ok=true;
            for(int i=0;i<run.Masters.Length;i++)for(int c=0;c<16;c++)if((p.Mask&(1u<<c))!=0)
            {
                uint master=run.Masters[i];
                int at=(int)master*16+c;float target=p.Event.Values[c];
                if(c<2)target=(p.Event.RelativePosition?EvalMasterSlotCurrent(at):gpuPacketOrigins[master*2+c])+target;
                float after=gpuMasterChannels[at].end;
                float end=c==5||c==6||c==12||c==13?after+(target-after)*ease:target;
                var n=gpuPacketShadow[at];gpuPacketChannels++;
                if(!PacketBits(n.start,anim?after:end)||!PacketBits(n.end,end)||!PacketBits(n.change,anim?target-after:0)||n.sourceIndex!=args.Source)
                {ok=false;PacketError("packet_reference channel="+c+" target="+master);}
                gpuPacketExpectedAnchors[i*16+c]=new GpuDecoGpu.Anchor{valid=1,clock=anim?(uint)args.Source:0u,
                    segment=new GpuDecoGpu.Segment{duration=anim?p.Event.Duration:0,start=anim?after:end,end=end,target=anim?target:end,
                        over=anim?p.Event.Over:0,period=anim?p.Event.Period:0,ease=anim?(uint)p.Event.Ease:1u,
                        scale=c==5||c==6||c==12||c==13?1u:0u}};
            }
            return ok;
        }
        static void UpdatePacketClock(GpuLiveSource s)
        {
            if(gpuPacketMode==0 || (uint)s.Slot>=(uint)gpuPacketClocks.Length)return;
            gpuPacketClocks[s.Slot]=new PacketClock{Position=s.Pos,Duration=s.Dur,Easing=s.CurrentE,Active=s.Active?1:0};
        }
        static unsafe int CommitGpuPacket(GpuEventPacket p,PacketRun run,List<scrDecoration> targets,int first,PacketArgs args,int keys)
        {
            var slice=gpuLiveSlices[0];int saved=0;bool committed=false,inject=false;
            bool anim=args.Source>=0;var next=anim?gpuCurrentAnimSource:null;int oldCount=next?.MasterSlots.Count??0;bool appended=false;
            try
            {
                if(anim && next.MasterSlots.Capacity<oldCount+run.Slots.Length)next.MasterSlots.Capacity=checked(oldCount+run.Slots.Length);
                // Source membership remains in the existing clock owner. Keep
                // only its binding undo; arithmetic/mirrors/anchors commit in
                // one native transaction after all validation has succeeded.
                for(int i=0;i<run.Masters.Length;i++)
                {
                    var d=targets[first+i];gpuPacketOldPlacement[i]=d.placementType;gpuPacketOldStart[i]=d.startPos;
                    int m=(int)run.Masters[i];
                    for(int c=0;c<16;c++)
                    {gpuPacketOldBindings[i*16+c]=gpuMasterSlotSource[m*16+c];gpuPacketOldBindingIndices[i*16+c]=-1;}
                    saved++;
                    for(int c=0;c<16;c++)if((p.Mask&(1u<<c))!=0)
                    {
                        int slot=m*16+c,old=gpuMasterSlotSource[slot];
                        if(old<0)continue;
                        int index=gpuSourcePool[old].MasterSlots.IndexOf(slot);
                        if(index<0)throw new InvalidOperationException("packet source membership missing");
                        gpuPacketOldBindingIndices[i*16+c]=index;
                        gpuSourcePool[old].MasterSlots.RemoveAt(index);gpuMasterSlotSource[slot]=-1;
                    }
                    if(p.Event.ResetGlobalOrigin)
                    {var row=table.Decorations[gpuLiveOwnedIndices[m]];d.placementType=DecPlacementType.Global;d.startPos=new Vector2(row.GlobalX,row.GlobalY);}
                }
                if(anim){next.MasterSlots.AddRange(run.Slots);appended=true;}
                if(gpuPacketRejectOnce && (!gpuPacketRejectAnimated||anim))
                {gpuPacketRejectOnce=false;inject=true;throw new InvalidOperationException("explicit packet precommit fault");}
                int result;
                fixed(LiveChannel* master=gpuMasterChannels,expected=gpuPacketShadow,destination=slice.Channels)
                fixed(GpuDecoGpu.Anchor* anchors=gpuMasterAnchors)fixed(int* bindings=gpuMasterSlotSource)
                fixed(uint* masters=run.Masters,items=run.SliceItems)fixed(bool* dirty=slice.DirtyBlocks)
                {
                    if(!anim)result=sf_deco_packet_commit_static_v1(master,anchors,bindings,gpuLiveOwnedIndices.Length,expected,
                        destination,slice.Channels.Length/16,masters,items,run.Masters.Length,p.Mask,dirty,slice.DirtyBlocks.Length);
                    else
                    {
                        var animation=new PacketAnimation{Duration=p.Event.Duration,Over=p.Event.Over,Period=p.Event.Period,Ease=(uint)p.Event.Ease};
                        fixed(PacketClock* clocks=gpuPacketClocks)fixed(float* origins=gpuPacketOrigins,scratch=gpuPacketTargetScratch)
                            result=sf_deco_packet_commit_animated_v1(master,anchors,bindings,gpuLiveOwnedIndices.Length,expected,
                                destination,slice.Channels.Length/16,masters,items,run.Masters.Length,&args,&animation,
                                clocks,gpuSourceHighWater,origins,gpuPacketOrigins.Length,scratch,gpuPacketTargetScratch.Length,dirty,slice.DirtyBlocks.Length);
                    }
                }
                if(result!=1)throw new InvalidOperationException("static commit status "+result);
                committed=true;
                if(anim)foreach(uint m in run.Masters)foreach(int c in gpuRecFirstChannels)
                    if((p.Mask&(1u<<c))!=0)StampGpuMasterRecOrder((int)m,c);
                slice.DirtyBlockMin=Math.Min(slice.DirtyBlockMin,run.MinBlock);slice.DirtyBlockMax=Math.Max(slice.DirtyBlockMax,run.MaxBlock);
                if((gpuZeroAlpha || gpuRenderMergePrepared) && (p.Mask&0xC00u)!=0)
                    foreach(uint item in run.SliceItems)slice.MarkDirty((int)item*16+10);
                if(gpuPacketMode==2 || gpuPacketMode==4 ||gpuPacketMode==7)for(int i=0;i<run.Masters.Length;i++)for(int c=0;c<16;c++)if((p.Mask&(1u<<c))!=0)
                {
                    int slot=(int)run.Masters[i]*16+c,mirror=(int)run.SliceItems[i]*16+c;
                    var a=gpuMasterChannels[slot];var b=gpuPacketShadow[slot];var v=slice.Channels[mirror];
                    var wanted=gpuPacketExpectedAnchors[i*16+c];
                    var actual=gpuMasterAnchors[slot];bool same=true;
                    for(int k=0;k<12;k++)same&=((uint*)&actual)[k]==((uint*)&wanted)[k];
                    if(!same || gpuMasterSlotSource[slot]!=args.Source || a.sourceIndex!=args.Source || v.sourceIndex!=args.Source
                        ||!PacketBits(a.start,b.start)||!PacketBits(a.change,b.change)||!PacketBits(a.end,b.end)
                        ||!PacketBits(v.start,b.start)||!PacketBits(v.change,b.change)||!PacketBits(v.end,b.end))
                        PacketError("static_mirror_or_anchor channel="+c+" target="+run.Masters[i]);
                }
                gpuPacketCommitted++;gpuPacketCommittedTargets+=run.Masters.Length;
                if(anim){gpuPacketAnimated++;gpuPacketAnimatedTargets+=run.Masters.Length;}
                gpuLiveSkippedRecs+=(long)keys*run.Masters.Length;
                gpuPacketCalls++;gpuPacketTargets+=run.Masters.Length;
                return run.Masters.Length;
            }
            catch(Exception ex)
            {
                if(!inject)PacketError("static_commit="+ex.GetType().Name);
                if(!committed)
                {
                    if(appended)next.MasterSlots.RemoveRange(oldCount,run.Slots.Length);
                    for(int i=saved-1;i>=0;i--)
                    {
                        int m=(int)run.Masters[i];var d=targets[first+i];
                        d.placementType=gpuPacketOldPlacement[i];d.startPos=gpuPacketOldStart[i];
                        for(int c=15;c>=0;c--)if((p.Mask&(1u<<c))!=0)
                        {
                            int slot=m*16+c,old=gpuPacketOldBindings[i*16+c];gpuMasterSlotSource[slot]=old;
                            int index=gpuPacketOldBindingIndices[i*16+c];
                            if(old>=0 && index>=0)gpuSourcePool[old].MasterSlots.Insert(index,slot);
                        }
                    }
                    if(inject){gpuPacketRollbackChecks++;GpuLog("packet_precommit_rollback targets="+saved+" native_commit=False");}
                    return 0; // Existing per-target path, exactly once.
                }
                // All targets have already committed. Restore the CPU from the
                // new state and skip this run, so it is never applied twice.
                RecoverGpuLive("packet_postcommit:"+ex.GetType().Name);return run.Masters.Length;
            }
        }
        static void PacketError(string reason)
        {gpuPacketMismatches++;if(gpuPacketFirstError=="none"){gpuPacketFirstError=reason;GpuLog("packet_first_error "+reason);}}
        static string GpuPacketReport()=>"packet_shadow calls="+gpuPacketCalls+" targets="+gpuPacketTargets+" channels="+gpuPacketChannels
            +" mismatch="+gpuPacketMismatches+" rejected="+gpuPacketRejected+" unplanned="+gpuPacketUnplanned
            +" native_static="+(gpuPacketCommitted-gpuPacketAnimated)+" native_static_targets="+(gpuPacketCommittedTargets-gpuPacketAnimatedTargets)
            +" native_animated="+gpuPacketAnimated+" native_animated_targets="+gpuPacketAnimatedTargets
            +" rollback_checks="+gpuPacketRollbackChecks
            +" target_eligible="+gpuPacketTargetEligible+" target_fast="+gpuPacketTargetFast+" target_invalid="+gpuPacketTargetInvalid
            +" target_oracle="+gpuPacketTargetOracle
            +" first="+gpuPacketFirstError+" mode="+gpuPacketMode+" timing="+(gpuPacketMode==3?"NATIVE_STATIC_NO_ORACLE":gpuPacketMode==5?"NATIVE_ALL_NO_ORACLE":gpuPacketMode==6?"NATIVE_COMPILED_TARGETS":"DIAGNOSTIC");
        static bool GpuPacketCommand(string command)
        {
            if(!command.StartsWith("packets ",StringComparison.Ordinal))return false;
            if(command=="packets rollback" || command=="packets rollbackanim")
            {
                if(!gpuLiveActive||(gpuPacketMode!=2&&gpuPacketMode!=4))throw new InvalidOperationException("packet rollback requires live diagnostic mode");
                gpuPacketRejectOnce=true;gpuPacketRejectAnimated=command=="packets rollbackanim";return true;
            }
            if(command=="packets state" || command=="packets verify")
            {
                GpuLog(GpuPacketReport());
                if(command=="packets verify" && (gpuPacketMismatches!=0 || (gpuPacketMode!=3&&gpuPacketMode!=5&&gpuPacketMode!=6)&&gpuPacketChannels==0
                    ||gpuPacketMode>=2&&(gpuPacketCommitted==0||gpuPacketRejectOnce)||gpuPacketMode>=4&&gpuPacketAnimated==0
                    ||gpuPacketMode>=6&&gpuPacketTargetEligible==0||gpuPacketMode==6&&gpuPacketTargetFast==0||gpuPacketMode==7&&gpuPacketTargetOracle==0))
                    throw new InvalidOperationException("packet shadow not verified");
                return true;
            }
            if(gpuLiveActive)throw new InvalidOperationException("packet mode requires inactive playback");
            if(command!="packets shadow" && command!="packets static" && command!="packets native" && command!="packets all" && command!="packets replay" && command!="packets compiled" &&command!="packets compiledcheck"&& command!="packets off")throw new ArgumentException("packets shadow|static|native|all|replay|compiled|compiledcheck|off|state|verify");
            gpuPacketMode=command=="packets shadow"?1:command=="packets static"?2:command=="packets native"?3:command=="packets all"?4:command=="packets replay"?5:command=="packets compiled"?6:command=="packets compiledcheck"?7:0;gpuPacketCurrent=null;
            gpuPacketCalls=gpuPacketTargets=gpuPacketChannels=gpuPacketMismatches=gpuPacketRejected=gpuPacketUnplanned=0;gpuPacketFirstError="none";
            gpuPacketCommitted=gpuPacketCommittedTargets=0;
            gpuPacketAnimated=gpuPacketAnimatedTargets=0;
            gpuPacketRollbackChecks=0;gpuPacketRejectOnce=false;
            gpuPacketTargetEligible=gpuPacketTargetFast=gpuPacketTargetInvalid=0;
            gpuPacketTargetOracle=0;
            if(gpuPacketMode==0)ClearGpuPackets();else if(gpuLivePrepared)CompileGpuPackets();
            GpuLog("packet_mode="+gpuPacketMode+" live_native_static="+(gpuPacketMode>=2));return true;
        }
    }
}
#endif
