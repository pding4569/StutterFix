#if !DEV && !AUTOTEST
using System.Collections.Generic;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Only diagnostic commands can prepare render merging. Keep shared
        // visibility checks constant without its plans or resource storage.
        const bool gpuRenderMergePrepare=false,gpuRenderMergePrepared=false,gpuRenderMergeOn=false;
        static bool MergeHidden(int item)=>false;
        static void PrepareGpuMerge(List<GpuLiveRow> rows) { }
        static void RefreshGpuMergeItem(int item) { }
        static void FlushGpuMergeGroups() { }
        static void DispatchGpuMerge(double time) { }
        static void HideGpuMerge() { }
        static void DisposeGpuMerge() { }
    }
}
#endif
