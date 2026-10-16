using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
unsafe class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Motion(float* s,float* c,float* p,float* d,float* o,int n,int rounding);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]static float Scalar(float s,float c,float p,float d){float f=p/d;float delta=c*f;return s+delta;}
    static int Main(string[] args)
    {
        IntPtr lib=NativeLibrary.Load(System.IO.Path.GetFullPath(args[0]));
        var motion=Marshal.GetDelegateForFunctionPointer<Motion>(NativeLibrary.GetExport(lib,"sf_motion_linear"));
        var rnd=new Random(148);int checks=0,bad=0;
        const int size=65536;
        float[] s=new float[size],c=new float[size],p=new float[size],d=new float[size],o=new float[size];
        for(int mode=0;mode<2;mode++)for(int pass=0;pass<32;pass++)
        {
            for(int i=0;i<size;i++)
            {
                s[i]=(float)(rnd.NextDouble()-.5)*100000;c[i]=(float)(rnd.NextDouble()-.5)*100000;
                d[i]=(float)(rnd.NextDouble()*120+.000001);p[i]=i%7==0?d[i]:(float)rnd.NextDouble()*d[i];
            }
            fixed(float* a=s,b=c,at=p,dur=d,dst=o)
            {
                if(motion(a,b,at,dur,dst,size,mode)!=1)throw new Exception("native result");
                for(int i=0;i<size;i++)
                {
                    float e=p[i]/d[i];
                    float expected=mode==0?Scalar(s[i],c[i],p[i],d[i]):(float)((double)s[i]+(double)c[i]*(double)e);
                    checks++;if(BitConverter.SingleToInt32Bits(expected)!=BitConverter.SingleToInt32Bits(o[i]))bad++;
                }
                if(motion(a,b,at,dur,dst,65537,mode)!=0||motion(null,b,at,dur,dst,1,mode)!=0||motion(a,b,at,dur,dst,1,2)!=0)throw new Exception("bounds guard");
            }
        }
        Console.WriteLine($"Linear native bit checks {checks}, mismatch {bad}; dispatched CPU path only");
        Console.WriteLine(".NET 8 offline arithmetic microbenchmark; excludes Unity setters and Mono, not game FPS.");
        foreach(int n in new[]{512,2048,8192,32768})
        {
            const int iterations=300;float sink=0;
            fixed(float* a=s,b=c,at=p,dur=d,dst=o)
            {
                for(int k=0;k<100;k++)motion(a,b,at,dur,dst,n,0);
                var sw=Stopwatch.StartNew();for(int k=0;k<iterations;k++)for(int i=0;i<n;i++)o[i]=Scalar(s[i],c[i],p[i],d[i]);sw.Stop();double managed=sw.Elapsed.TotalMicroseconds/iterations;
                sink+=o[n-1];sw.Restart();for(int k=0;k<iterations;k++)motion(a,b,at,dur,dst,n,0);sw.Stop();double native=sw.Elapsed.TotalMicroseconds/iterations;sink+=o[n-1];
                Console.WriteLine($"n={n} scalar_us={managed:F3} native_us={native:F3} sink={sink:R}; gather cost excluded");
            }
        }
        NativeLibrary.Free(lib);return bad==0?0:1;
    }
}
