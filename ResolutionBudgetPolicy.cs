using System;

namespace StutterFix
{
    internal static class ResolutionBudgetPolicy
    {
        internal static int Direction(float sampleMs,float budgetMs)
        {
            if(!(sampleMs>0)||!(budgetMs>0)||float.IsInfinity(sampleMs)||float.IsInfinity(budgetMs))return 0;
            if(sampleMs>budgetMs*.8f)return -1;
            if(sampleMs<budgetMs*.55f)return 1;
            return 0;
        }
    }
}
