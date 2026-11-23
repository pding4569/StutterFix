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
            // 2026-11-23: 맡는 비율이 낮으면 GPU 장식 관리 비용만 더해진다(11맵 켬/끔: 맡는 비율 Exit 86% +11%, Timeline 80% +21%,
            // WF 63% +1.8%, HELLO 61% +1.5%, PLUM 37% 0%, 7777 30% -2.5%, Lost 25% -5%, DD 9% -13%). 장식 수 대신 맡는 비율로 정한다.
            int total=scrDecorationManager.instance?scrDecorationManager.instance.allDecorations.Count:0;
            int percent=Main.Config==null?0:Main.Config.GpuDecoMinOwnedPercent;
            bool ratioOk=!GpuDecoPlayer.Product||total<=0||(long)owned*100>=(long)total*percent;
            if(owned>=minimum&&ratioOk)return true;
            string reason=ratioOk?"소유 장식 "+owned+"개 (최소 "+minimum+")":"소유 장식 "+owned+"/"+total+"개 = "+(total>0?(100L*owned/total):0)+"% < 기준 "+percent+"%: 맡는 장식이 적어 원래 경로 사용";
            if(GpuDecoPlayer.Product)GpuDecoPlayer.Skip(reason);
            else Lg.I("[GPU 장식] 건너뜀: "+reason);
            DisposeGpuLive();
            return false;
        }
    }
}
#endif
