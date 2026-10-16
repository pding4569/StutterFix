#if DEV || AUTOTEST
using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // Explicit research call only; no new PNG decoder or loading hook.
    internal static unsafe class AlphaBoundsProbe
    {
        [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
        private static extern int sf_alpha_bounds_v1(byte* pixels,UIntPtr bytes,int width,int height,int stride,int alpha,int* bounds);
        internal static bool TryFind(byte[] pixels,int width,int height,int stride,int alpha,out int[] bounds)
        {
            bounds=null;if(pixels==null||width<=0||height<=0||stride<=0||alpha<0||alpha>3)return false;
            if((long)width*4>stride||(long)(height-1)*stride+(long)width*4>pixels.Length)return false;
            try
            {
                int* box=stackalloc int[4];
                fixed(byte* source=pixels)
                {
                    if(sf_alpha_bounds_v1(source,new UIntPtr((uint)pixels.Length),width,height,stride,alpha,box)!=1)return false;
                    bounds=new[]{box[0],box[1],box[2],box[3]};return true;
                }
            }
            catch{return false;}
        }
    }
}
#endif
