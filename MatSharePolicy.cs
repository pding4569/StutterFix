using System;
using System.Collections.Generic;

namespace StutterFix
{
    internal static class MatSharePolicy
    {
        // Input must already be in the original back-to-front order. Barriers never merge.
        internal static int[] Runs(string[] keys, bool[] barriers)
        {
            if (keys.Length != barriers.Length) throw new ArgumentException("length");
            var result = new int[keys.Length]; int run = -1;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == 0 || barriers[i] || barriers[i - 1] || keys[i] != keys[i - 1]) run++;
                result[i] = run;
            }
            return result;
        }
        internal static int[] MaterialOrder(int[] ids)
        {
            var order = new int[ids.Length]; for (int i = 0; i < ids.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => ids[a].CompareTo(ids[b]));
            for (int i = 1; i < order.Length; i++)
                if (ids[order[i]] == ids[order[i - 1]]) throw new ArgumentException("duplicate material ID");
            return order;
        }
    }
}
