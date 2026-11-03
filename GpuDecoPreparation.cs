#if DEV || AUTOTEST || GPUDECO || GPU_PREPARATION_CHECK
namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Run after all row demotions, before allocating slices or consuming Data.
        // An empty slice intentionally has no Data; it is not a preparation fault.
        static bool AcceptGpuOwnedCount(int owned)
        {
            int minimum=GpuDecoPlayer.Product?GpuDecoPlayerPolicy.MinimumOwned:1;
            if(owned>=minimum)return true;
            string reason="소유 장식 "+owned+"개 (최소 "+minimum+")";
            if(GpuDecoPlayer.Product)GpuDecoPlayer.Skip(reason);
            else Main.Entry.Logger.Log("[GPU 장식] 건너뜀: "+reason);
            DisposeGpuLive();
            return false;
        }
    }
}
#endif
