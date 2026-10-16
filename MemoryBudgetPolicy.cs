using System;

namespace StutterFix
{
    // Pure policy, MB throughout. Working set is not the managed heap; reserve
    // space for native/driver memory before assigning a managed-heap budget.
    internal static class MemoryBudgetPolicy
    {
        internal static int Legacy(int configured, long total)
            => total > 0 ? Math.Min(configured, (int)Math.Max(1500, Math.Min(int.MaxValue, total * 2 / 5))) : configured;

        internal static int Calculate(int fallback, long total, long available, long workingSet, long heap)
        {
            if(total<=0||total>16L*1024*1024||available<0||available>total||workingSet<0||heap<0)
                return Legacy(fallback,total>0&&total<=16L*1024*1024?total:0);
            long nativeResident=Math.Max(0,workingSet-heap);
            long totalBudget=total*7/10-nativeResident;
            long reserve=Math.Max(256,total/20);
            long growthBudget=heap+available*7/10-reserve;
            return (int)Math.Max(256,Math.Min(int.MaxValue,Math.Min(totalBudget,growthBudget)));
        }
    }
}
