#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Optional research backend. A single quantizing dispatch precedes an
    // ordered Unity render event; no immediate context is used from C#.
    internal sealed class GpuDecoCopy : IDisposable
    {
        internal static bool Enabled;
        internal static bool EightUavEnabled=false;
        internal static bool MaskedEnabled=false; // explicit research candidate, default off
        internal static bool SingleSubmission=false; // 연구 전용, 기본 꺼짐
        internal static long SingleExecutions, SingleFallbacks;
        internal static string Status="off";
#if AUTOTEST
        internal static bool AblateDestinations;
#endif
        [StructLayout(LayoutKind.Sequential)]
        internal struct Totals {internal ulong Submitted,Executed,Ticks,MaxTicks,Errors;}
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_version();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_create(IntPtr source,[In] IntPtr[] targets,[In] uint[] offsets,int count,uint bytes);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_create8(IntPtr source,[In] IntPtr[] targets,[In] uint[] offsets,int count,uint bytes);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_create8masked(IntPtr source,[In] IntPtr[] targets,[In] uint[] offsets,int count,uint bytes);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_create_masked(IntPtr source,[In] IntPtr[] targets,[In] uint[] offsets,int count,uint bytes);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_submit_mask(int id,ulong low,ulong high);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sf_deco_copy_event();
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_submit(int id);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern int sf_deco_copy_stats(int id,out Totals output);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern void sf_deco_copy_destroy(int id);
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)] static extern uint sf_deco_copy_thread(int id);
        static readonly List<GpuDecoCopy> instances=new List<GpuDecoCopy>();
        static ulong managedFaults;
        GraphicsBuffer normalized;
        CommandBuffer commands;
        CommandBuffer singleCommands;
        ComputeShader copyShader;
        GraphicsBuffer copySource;
        IntPtr copyCallback;
        int copyKernel,copyVertexCount;
        int token;
        bool masked,failed;
        internal bool Ready=>token!=0;

        internal static GpuDecoCopy TryCreate(ComputeShader shader,GraphicsBuffer source,IReadOnlyList<GraphicsBuffer> targets,int vertexCount,int[] order=null)
        {
            if(!Enabled)return null;
            var result=new GpuDecoCopy();
            result.masked=MaskedEnabled;
            try
            {
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11 || !SfNative.Ready || vertexCount!=targets.Count*4
                    || sf_deco_copy_version()!=1)throw new InvalidOperationException("D3D11/native copy contract unavailable");
                result.normalized=new GraphicsBuffer(GraphicsBuffer.Target.Raw,vertexCount*9,4);
                var pointers=new IntPtr[targets.Count];var offsets=new uint[targets.Count];
                for(int i=0;i<targets.Count;i++)
                {
                    int item=order==null?i:order[i];
                    if(targets[item].count*targets[item].stride!=144)throw new InvalidOperationException("quad target size changed");
                    pointers[i]=targets[item].GetNativeBufferPtr();offsets[i]=checked((uint)(item*144));
                }
                result.token=result.masked
                    ? EightUavEnabled
                        ? sf_deco_copy_create8masked(result.normalized.GetNativeBufferPtr(),pointers,offsets,pointers.Length,144)
                        : sf_deco_copy_create_masked(result.normalized.GetNativeBufferPtr(),pointers,offsets,pointers.Length,144)
                    : EightUavEnabled
                    ? sf_deco_copy_create8(result.normalized.GetNativeBufferPtr(),pointers,offsets,pointers.Length,144)
                    : sf_deco_copy_create(result.normalized.GetNativeBufferPtr(),pointers,offsets,pointers.Length,144);
                IntPtr callback=sf_deco_copy_event();
                if(result.token==0 || callback==IntPtr.Zero)throw new InvalidOperationException("native context rejected buffer descriptors");
                result.copyShader=shader;result.copySource=source;result.copyCallback=callback;
                result.copyKernel=shader.FindKernel("CopyRun");result.copyVertexCount=vertexCount;
                int kernel=shader.FindKernel("CopyRun");result.commands=new CommandBuffer {name="GpuDeco deferred native copies"};
                result.commands.SetComputeBufferParam(shader,kernel,"_CopySource",source);result.commands.SetComputeBufferParam(shader,kernel,"_Vertices",result.normalized);
                result.commands.SetComputeIntParam(shader,"_CopyFirstVertex",0);result.commands.SetComputeIntParam(shader,"_CopyVertexCount",vertexCount);
                result.commands.SetComputeIntParam(shader,"_CopyQuantizeColor",1);result.commands.DispatchCompute(shader,kernel,(vertexCount+63)/64,1,1);
                result.commands.IssuePluginEvent(callback,result.token);
                instances.Add(result);Status="ready";return result;
            }
            catch(Exception e) {Status=e.GetType().Name+": "+e.Message;result.Dispose();return null;}
        }
        internal bool Execute(ulong low=ulong.MaxValue,ulong high=ulong.MaxValue,
            GpuDecoGpu evaluator=null,Vector4 camera=default,double time=0)
        {
            try
            {
                if(failed)return false;
                var submitted=commands;
                if(evaluator!=null)
                {
                    // 객체는 재사용한다. 시각/카메라가 매 프레임 달라 명령은 다시 기록한다.
                    // 기록 실패 때 native mask 큐를 소비하지 않도록 submit보다 먼저 한다.
                    if(singleCommands==null)singleCommands=new CommandBuffer {name="GpuDeco evaluate and native copy"};
                    singleCommands.Clear();
                    evaluator.RecordLiveEvaluation(singleCommands,camera,time);
                    singleCommands.SetComputeBufferParam(copyShader,copyKernel,"_CopySource",copySource);
                    singleCommands.SetComputeBufferParam(copyShader,copyKernel,"_Vertices",normalized);
                    singleCommands.SetComputeIntParam(copyShader,"_CopyFirstVertex",0);
                    singleCommands.SetComputeIntParam(copyShader,"_CopyVertexCount",copyVertexCount);
                    singleCommands.SetComputeIntParam(copyShader,"_CopyQuantizeColor",1);
                    singleCommands.DispatchCompute(copyShader,copyKernel,(copyVertexCount+63)/64,1,1);
                    singleCommands.IssuePluginEvent(copyCallback,token);
                    submitted=singleCommands;
                }
#if AUTOTEST
                // Consume the ordinary event/mask queue and retain normalization;
                // an empty mask omits only destination-copy command chunks.
                if(AblateDestinations) {if(!masked)throw new InvalidOperationException("unmasked copy ablation");low=high=0;}
#endif
                if(token==0 || (masked?sf_deco_copy_submit_mask(token,low,high):sf_deco_copy_submit(token))==0){managedFaults++;failed=true;return false;}
                Graphics.ExecuteCommandBuffer(submitted);
                if(evaluator!=null)SingleExecutions++;
                return true;
            }
            catch(Exception e) {managedFaults++;failed=true;Status=e.GetType().Name+": "+e.Message;return false;}
        }
        internal static Totals ReadTotals()
        {
            var result=new Totals {Errors=managedFaults};
            foreach(var instance in instances)
            {
                if(instance.token==0)continue;
                try
                {
                    if(sf_deco_copy_stats(instance.token,out var value)==0)throw new InvalidOperationException("native stats unavailable");
                    result.Submitted+=value.Submitted;result.Executed+=value.Executed;result.Ticks+=value.Ticks;result.Errors+=value.Errors;
                    result.MaxTicks=Math.Max(result.MaxTicks,value.MaxTicks);
                }
                catch(Exception e) {Status=e.GetType().Name+": "+e.Message;result.Errors++;}
            }
            return result;
        }
        internal static int ActiveContexts=>instances.Count;
#if AUTOTEST
        internal static int[] ProbeTokens()
        {
            var result=new List<int>();foreach(var instance in instances)if(instance.token!=0)result.Add(instance.token);
            return result.ToArray();
        }
        internal static int ProbeRetired(int[] tokens)
        {
            int alive=0;foreach(int id in tokens)
            {
                if(id<=0)throw new ArgumentOutOfRangeException(nameof(tokens));
                if(sf_deco_copy_stats(id,out var _)!=0)alive++;
            }
            return alive;
        }
#endif
        internal static uint RenderThreadId()
        {
            uint found=0;
            try
            {
                foreach(var instance in instances)
                {
                    uint id=sf_deco_copy_thread(instance.token);
                    if(id==0)continue;
                    if(found!=0 && id!=found)return 0;
                    found=id;
                }
            }
            catch {return 0;}
            return found;
        }
        public void Dispose()
        {
            int old=token;token=0;instances.Remove(this);
            if(old!=0)try {sf_deco_copy_destroy(old);}catch(Exception e){Status=e.GetType().Name+": "+e.Message;}
            try {commands?.Dispose();}
            finally
            {
                commands=null;
                try {singleCommands?.Dispose();}
                finally {singleCommands=null;copyShader=null;copySource=null;normalized?.Dispose();normalized=null;}
            }
        }
    }
}
#endif
