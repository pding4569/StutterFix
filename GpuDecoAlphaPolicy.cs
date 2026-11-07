#if DEV || AUTOTEST || GPUDECO
namespace StutterFix
{
    internal static class GpuDecoAlphaPolicy
    {
        // Conservative: both alpha factors must already be constants. No
        // epsilon, quantization cutoff, predicted phase, or delayed GPU readback.
        internal static bool Hidden(float colorAlpha,int colorSource,float opacity,int opacitySource)
            =>!float.IsNaN(colorAlpha) && !float.IsInfinity(colorAlpha)
                && !float.IsNaN(opacity) && !float.IsInfinity(opacity)
                && ((colorSource<0 && colorAlpha<=0f) || (opacitySource<0 && opacity<=0f));
    }
}
#endif
