#if DEV || AUTOTEST || GPUDECO
namespace StutterFix
{
    internal static class GpuDecoAlphaPolicy
    {
        // Conservative: both alpha factors must already be constants. No
        // epsilon, quantization cutoff, predicted phase, or delayed GPU readback.
        internal static bool Hidden(float colorAlpha,int colorSource,float opacity,int opacitySource)
            =>colorSource<0 && opacitySource<0 && !float.IsNaN(colorAlpha) && !float.IsInfinity(colorAlpha)
                && !float.IsNaN(opacity) && !float.IsInfinity(opacity) && (colorAlpha==0f || opacity==0f);
    }
}
#endif
