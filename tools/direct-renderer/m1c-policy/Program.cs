using System;
using StutterFix;

static class Program
{
    static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    static void Match(DirectRenderVisibilityIndex index, bool[] reference)
    {
        int actual = -1;
        for (int i = 0; i < reference.Length; i++) if (reference[i])
        {
            actual = index.Next(actual);
            Require(actual == i, "visibility order/lost entry: " + i + "/" + actual);
        }
        Require(index.Next(actual) == -1, "stale tail entry");
    }
    static void Main()
    {
        var index = new DirectRenderVisibilityIndex(); var random = new Random(715);
        long changes = 0;
        foreach (int size in new[] { 0, 1, 63, 64, 65, 127, 128, 129, 4200, 11001, 31, 8192, 0 })
        {
            index.Reset(size); var reference = new bool[size]; Match(index, reference);
            for (int step = 0; step < 10000 && size > 0; step++)
            {
                int slot = random.Next(size); bool on = random.Next(2) == 0;
                index.Set(slot, on); reference[slot] = on; changes++;
                if (step % 127 == 0) Match(index, reference);
            }
            Match(index, reference);
        }
        Console.WriteLine("PASS random enable/disable, 64-bit edges, shrink/regrow: " + changes);

        // 4,200 pooled renderers sleep while native events and a few renderers remain in exact order.
        index.Reset(4300); var sparse = new bool[4300];
        foreach (int i in new[] { 0, 17, 64, 2799, 2800, 4199, 4299 }) { index.Set(i, true); sparse[i] = true; }
        Match(index, sparse);
        for (int i = 0; i < 4300; i++) { index.Set(i, true); sparse[i] = true; }
        Match(index, sparse);
        for (int i = 4299; i >= 0; i--) { index.Set(i, false); sparse[i] = false; }
        Match(index, sparse);
        index.Set(63, true); index.Set(64, true); index.Set(4299, true);
        long total = 0;
        for (int j = 0; j < 10000; j++) for (int i = index.Next(-1); i >= 0; i = index.Next(i)) total += i;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int j = 0; j < 100000; j++)
        {
            index.Set(17, (j & 1) == 0);
            for (int i = index.Next(-1); i >= 0; i = index.Next(i)) total += i;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated == 0, "index steady/transition allocation=" + allocated);
        Console.WriteLine("PASS pooled visibility order and hot-path allocation: " + allocated + " bytes checksum=" + total);

        // Native sprites and returned Unity renderers use exactly the production sorting relation.
        var behind = new SceneRenderOrder.Key(0, 1, 3000, 0, 1, 1);
        var returned = new SceneRenderOrder.Key(0, 2, 3000, 0, 1, 2);
        var ahead = new SceneRenderOrder.Key(0, 3, 3000, 0, 1, 3);
        Require(behind.CompareTo(returned) < 0 && ahead.CompareTo(returned) > 0, "fallback boundary direction");
        Require(new SceneRenderOrder.Key(0, 2, 3000, 10, 1, 2).CompareTo(returned) < 0, "transparent distance");
        Console.WriteLine("PASS production render-key boundary ordering");
    }
}
