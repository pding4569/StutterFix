using System;
using System.Linq;
using System.Runtime.InteropServices;
unsafe class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Unfilter(byte* dst,byte* src,byte* prior,int n,int bpp,int filter);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Dxt(byte* src,int w,int h,int layout,int dxt5,byte* dst,int first,int last);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Init();
    static int Paeth(int a,int b,int c){int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;}
    static int Main(string[] args)
    {
        var lib=NativeLibrary.Load(System.IO.Path.GetFullPath(args[0]));
        try
        {
            var unfilter=Marshal.GetDelegateForFunctionPointer<Unfilter>(NativeLibrary.GetExport(lib,"sf_unfilter_to"));
            var reference=Marshal.GetDelegateForFunctionPointer<Dxt>(NativeLibrary.GetExport(lib,"sf_dxt_encode_rows_ref"));
            var native=Marshal.GetDelegateForFunctionPointer<Dxt>(NativeLibrary.GetExport(lib,"sf_dxt_encode_rows"));
            Marshal.GetDelegateForFunctionPointer<Init>(NativeLibrary.GetExport(lib,"sf_dxt_init"))();
            var random=new Random(121225);int checks=0,different=0;
            for(int k=0;k<300;k++)
            {
                int n=random.Next(1,2049),bpp=k%4+1,filter=k%5;
                byte[] src=new byte[n],prior=new byte[n],expected=new byte[n],actual=new byte[n];random.NextBytes(src);random.NextBytes(prior);
                for(int i=0;i<n;i++)
                {
                    int left=i>=bpp?expected[i-bpp]:0,up=prior[i],upperLeft=i>=bpp?prior[i-bpp]:0;
                    int predictor=filter==0?0:filter==1?left:filter==2?up:filter==3?(left+up)/2:Paeth(left,up,upperLeft);
                    expected[i]=unchecked((byte)(src[i]+predictor));
                }
                fixed(byte* s=src,p=prior,o=actual)if(unfilter(o,s,p,n,bpp,filter)!=1)throw new Exception("unfilter rejected valid row");
                checks++;if(!actual.SequenceEqual(expected))different++;
            }
            for(int k=0;k<300;k++)
            {
                // Internal ABI uses 0/1/2, not TextureFormat enum 4/5/3.
                // The encoder consumes complete blocks; callers pad beforehand.
                int w=random.Next(1,21)*4,h=random.Next(1,17)*4,layout=k%3,dxt5=k%2,bpp=layout==2?3:4;
                byte[] pixels=new byte[w*h*bpp];random.NextBytes(pixels);
                int rows=(h+3)/4,bytes=((w+3)/4)*rows*(dxt5==0?8:16);
                byte[] a=new byte[bytes],b=new byte[bytes];
                fixed(byte* s=pixels,o=a,p=b){reference(s,w,h,layout,dxt5,o,0,rows);native(s,w,h,layout,dxt5,p,0,rows);}
                checks++;if(!a.SequenceEqual(b))different++;
            }
            Console.WriteLine($"Rebuilt native image regression {checks} cases, mismatch {different}; synthetic PNG rows and RGB/RGBA/ARGB DXT1/5, current CPU dispatcher only");
            return different==0?0:1;
        }
        finally{NativeLibrary.Free(lib);}
    }
}
