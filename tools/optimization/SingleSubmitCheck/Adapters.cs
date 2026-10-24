using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static class SfNative { internal static bool Ready=true; }
    internal static class Adapter
    {
        internal static int Submits,Destroyed;
        internal static bool Reject;
        internal static ulong Low,High;
        internal static int Submit(ulong low,ulong high) {Submits++;Low=low;High=high;return Reject?0:1;}
    }
}
namespace UnityEngine
{
    internal struct Vector4(float x,float y,float z,float w) {internal float x=x,y=y,z=z,w=w;}
    internal class ComputeBuffer { }
    internal sealed class GraphicsBuffer(GraphicsBuffer.Target target,int count,int stride) : IDisposable
    {
        internal enum Target { Raw }
        internal int count=count,stride=stride;
        internal bool Disposed;
        internal IntPtr GetNativeBufferPtr() => (IntPtr)1;
        public void Dispose() {Disposed=true;}
    }
    internal class ComputeShader {internal int FindKernel(string name) => 2;}
    internal static class SystemInfo {internal static GraphicsDeviceType graphicsDeviceType=GraphicsDeviceType.Direct3D11;}
    internal static class Graphics
    {
        internal static int Executions;
        internal static CommandBuffer Last;
        internal static void ExecuteCommandBuffer(CommandBuffer commands) {Executions++;Last=commands;}
    }
}
namespace UnityEngine.Rendering
{
    internal enum GraphicsDeviceType { Direct3D11 }
    internal sealed class CommandBuffer : IDisposable
    {
        internal string name;
        internal bool Disposed;
        internal int Clears;
        internal readonly List<(string Op,string Key,object Value)> Ops=new();
        internal void Clear() {Ops.Clear();Clears++;}
        internal void SetComputeBufferParam(ComputeShader s,int kernel,string key,object value) => Ops.Add(("buffer",kernel+":"+key,value));
        internal void SetComputeIntParam(ComputeShader s,string key,int value) => Ops.Add(("int",key,value));
        internal void SetComputeFloatParam(ComputeShader s,string key,float value) => Ops.Add(("float",key,value));
        internal void SetComputeVectorParam(ComputeShader s,string key,Vector4 value) => Ops.Add(("vector",key,value));
        internal void DispatchCompute(ComputeShader s,int kernel,int x,int y,int z) => Ops.Add(("dispatch",kernel.ToString(),x));
        internal void IssuePluginEvent(IntPtr callback,int token) => Ops.Add(("event","",token));
        public void Dispose() {Disposed=true;}
    }
}
