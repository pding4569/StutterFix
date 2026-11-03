using System;

namespace StutterFix
{
    // Value-only gate: a failure or a second PrepVfx cannot replan the current round.
    internal struct GpuDecoPlayerLifetime
    {
        internal bool PreparationAllowed, Prepared, Attempted, Blocked;
        internal void NewRound() { PreparationAllowed=true; Prepared=Attempted=false; Blocked=true; }
        internal bool TryPrepare()
        {
            if(!PreparationAllowed)return false;
            PreparationAllowed=false; Prepared=Attempted=Blocked=false; return true;
        }
        internal void Ready() { if(!Blocked)Prepared=true; }
        internal bool TryActivate()
        {
            if(Blocked || !Prepared || Attempted)return false;
            Attempted=true; return true;
        }
        internal void Retry() { if(!Blocked)Attempted=false; }
        internal void Fail() { Blocked=true; PreparationAllowed=false; }
    }
    internal static class GpuDecoPlayerPolicy
    {
        internal const int MinimumOwned=10000; // 실측: 소유 124~2,999개 맵은 손해, 2만 8천 개는 큰 이득. 그 사이 맵은 표본 없음.
        // Lower observed beneficial workload, rounded down at six decimals.
        // Calibration: cpu-profile-ar, cpu-profile-wf, cpu-notify-exit-diagnose.
        // Research gate remains off: these three workloads are not a general
        // predictor of performance on unseen charts.
        internal const double MinimumOwnership=.989179;
        internal const double MinimumVisibleApplications=250.8495;
        internal const double MinimumVisibleMoving=173.265625;
        internal static bool ProfitableWorkload(double ownership,double visibleApplications,double visibleMoving)
            => !double.IsNaN(ownership) && !double.IsNaN(visibleApplications) && !double.IsNaN(visibleMoving)
                && !double.IsInfinity(ownership) && !double.IsInfinity(visibleApplications) && !double.IsInfinity(visibleMoving)
                && ownership>=MinimumOwnership && ownership<=1
                && visibleApplications>=MinimumVisibleApplications && visibleMoving>=MinimumVisibleMoving;
        // Conservative whole-song mode (c). We deliberately do not promise that
        // every possible packing within 52 bindings will be attempted.
        internal static bool FitsTextures(int textures,int direct,int[] arrayGroups,int maxSlices)
        {
            if(textures<0 || direct<0 || textures>DrawCollapsePolicy.TextureBindings
                || direct>DrawCollapsePolicy.DirectBindings || maxSlices<=0)return false;
            long arrays=0;
            foreach(int count in arrayGroups)
            {
                if(count<0)return false;
                arrays+=(count+(long)maxSlices-1)/maxSlices;
            }
            return arrays<=DrawCollapsePolicy.ArrayBindings;
        }
    }
}
