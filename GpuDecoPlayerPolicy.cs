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
        internal const int MinimumOwned=64;
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
