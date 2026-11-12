#if AUTOTEST
using System;
using UnityEngine;
namespace StutterFix
{
    internal static partial class DecoBake
    {
        static bool gpuMapBatch,gpuMapResync;
        static MapUploadRange gpuMapPending;
        static uint[] gpuMapPendingArray;
        static ComputeBuffer gpuMapPendingBuffer;
        static long gpuMapCalls,gpuMapBytes,gpuMapDeferred,gpuMapFlushes;
        internal static void ResetMapUploadExperiment()
        {gpuMapBatch=false;NewMapUploadContext();gpuMapCalls=gpuMapBytes=gpuMapDeferred=gpuMapFlushes=0;}
        static void FlushMapRange(ComputeBuffer target,uint[] shadow,ref MapUploadRange range)
        {
            if(!range.Pending)return;
            target.SetData(shadow,range.First,range.First,range.Count);range.Clear();
        }
        static void ResetMapPending()
        {gpuMapPending.Clear();gpuMapPendingArray=null;gpuMapPendingBuffer=null;}
        static void NewMapUploadContext()
        {ResetMapPending();gpuMapResync=false;}
        static void EndMapUploadContext()
        {
            // A retained map may be reused after a stop. Its CPU shadow is newer
            // than the GPU if we discard pending transfers during teardown.
            if(gpuMapPending.Pending)gpuMapResync=true;
            ResetMapPending();
        }
        static void UploadCollapseMapSlot(int slot)
        {
            if(gpuMapBatch)
            {
                if(!gpuMapPending.Pending){gpuMapPendingArray=gpuCollapseMapCpu;gpuMapPendingBuffer=gpuCollapseAllMap;}
                RequireLive(ReferenceEquals(gpuMapPendingArray,gpuCollapseMapCpu) && ReferenceEquals(gpuMapPendingBuffer,gpuCollapseAllMap),"map upload storage changed");
                gpuMapPending.Add(slot,gpuCollapseMapCpu.Length);gpuMapDeferred++;return;
            }
            if(gpuCollapseAllMap==null)return;
            gpuCollapseAllMap.SetData(gpuCollapseMapCpu,slot,slot,1);gpuMapCalls++;gpuMapBytes+=4;
        }
        static void UploadCollapseMapAll()
        {
            if(gpuCollapseAllMap==null)return;
            gpuCollapseAllMap.SetData(gpuCollapseMapCpu);gpuMapCalls++;gpuMapBytes+=(long)gpuCollapseMapCpu.Length*4;
            NewMapUploadContext();
        }
        static void FlushCollapseMapUploads()
        {
            if(gpuMapResync){UploadCollapseMapAll();return;}
            if(!gpuMapPending.Pending)return;
            RequireLive(gpuCollapseAllMap!=null && ReferenceEquals(gpuMapPendingArray,gpuCollapseMapCpu)
                && ReferenceEquals(gpuMapPendingBuffer,gpuCollapseAllMap),"map flush storage changed");
            int first=gpuMapPending.First,count=gpuMapPending.Count;
            FlushMapRange(gpuCollapseAllMap,gpuCollapseMapCpu,ref gpuMapPending);
            gpuMapCalls++;gpuMapBytes+=(long)count*4;gpuMapFlushes++;ResetMapPending();
        }
        static string MapBatchCommand(string arg)
        {
            RequireLive(g2Window==-1,"map upload experiment outside G2 measurement only");
            if(arg=="on" || arg=="off")
            {
                RequireLive(!gpuLiveActive || GpuPlaybackState.Paused,"map upload policy requires inactive or paused boundary");
                if(arg=="off")FlushCollapseMapUploads();gpuMapBatch=arg=="on";
            }
            else if(arg=="reset")gpuMapCalls=gpuMapBytes=gpuMapDeferred=gpuMapFlushes=0;
            else if(arg=="fixture")
            {
                RequireLive(GpuPlaybackState.Paused,"map scratch fixture requires pause");
                const int n=2048;var shadow=new uint[n];var a=new uint[n];var b=new uint[n];
                for(int i=0;i<n;i++)shadow[i]=(uint)i|((uint)(i%13)<<18);
                using(var old=new ComputeBuffer(n,4))using(var batched=new ComputeBuffer(n,4))
                {
                    old.SetData(shadow);batched.SetData(shadow);var pending=new MapUploadRange();
                    int comparisons=0;var rng=new System.Random(11409);
                    for(int round=0;round<12;round++)
                    {
                        int writes=round==0?1008:73+round;
                        for(int i=0;i<writes;i++)
                        {
                            int slot=round==0?i:rng.Next(n);shadow[slot]=(i&1)==0?uint.MaxValue:(uint)rng.Next();
                            old.SetData(shadow,slot,slot,1);pending.Add(slot,n);
                        }
                        FlushMapRange(batched,shadow,ref pending);old.GetData(a);batched.GetData(b);
                        for(int i=0;i<n;i++){RequireLive(a[i]==b[i] && b[i]==shadow[i],"map scratch fixture mismatch");comparisons++;}
                    }
                    uint saved=shadow[17];shadow[17]^=1;batched.SetData(shadow,17,17,1);batched.GetData(b);
                    RequireLive(b[17]!=a[17],"map scratch negative control not detected");
                    shadow[17]=saved;batched.SetData(shadow,17,17,1);batched.GetData(b);
                    for(int i=0;i<n;i++)RequireLive(a[i]==b[i],"map scratch restore mismatch");
                    GpuLog("MAP_UPLOAD_FIXTURE comparisons="+comparisons+" differences=0 negative_control=1 restored=True live_buffers_mutated=False timing_contaminated=True");
                }
            }
            else if(arg=="check")
            {
                RequireLive(GpuPlaybackState.Paused && gpuCollapseOn,"map readback is paused diagnostic only");
                FlushCollapseMapUploads();var read=new uint[gpuCollapseMapCpu.Length];gpuCollapseAllMap.GetData(read);
                int differences=0;for(int i=0;i<read.Length;i++)if(read[i]!=gpuCollapseMapCpu[i])differences++;
                RequireLive(differences==0,"map readback mismatch");
                GpuLog("MAP_UPLOAD_CHECK slots="+read.Length+" differences="+differences+" timing_contaminated=True");
            }
            else RequireLive(arg=="state","mapbatch on|off|reset|state|check|fixture");
            return "MAP_UPLOAD_STATE batch="+gpuMapBatch+" calls="+gpuMapCalls+" bytes="+gpuMapBytes+" deferred_writes="+gpuMapDeferred
                +" flushes="+gpuMapFlushes+" pending="+gpuMapPending.Pending+" resync="+gpuMapResync+" extra_clock_reads=0";
        }
    }
}
#endif
