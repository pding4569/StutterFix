using System;
using System.Linq;
using StutterFix;
using K = StutterFix.SceneRenderOrder.Key;

sealed class Node { internal int Layer; internal bool Dead; }
static class Program
{
    static int checks;
    static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    static K Key(int order, int id) => new K(0, order, 3000, 0, 0, id);
    static int Sort((int[] ids, K[] keys, K leaf) a, (int[] ids, K[] keys, K leaf) b)
    {
        int c = DirectFullOrderPath.Compare(a.ids, a.keys, a.leaf, b.ids, b.keys, b.leaf);
        return c != 0 ? c : a.leaf.CompareTo(b.leaf);
    }
    static int Main()
    {
        try { Run(); return 0; } catch (Exception e) { Console.Error.WriteLine("FAIL " + e.Message); return 1; }
    }
    static void Run()
    {
        var nodes = Enumerable.Range(0, 32).Select(i => new Node { Layer = i }).ToArray();
        bool fail = false; int writes = 0;
        var lease = new DirectFullObjectLayers<Node>(n => n != null && !n.Dead, n => n.Layer,
            (n, layer) => { if (fail && ++writes == 3) throw new InvalidOperationException("injected setter failure"); n.Layer = layer; });
        foreach (var n in nodes) lease.Capture(n);
        Check(lease.Mask == -1 && lease.Objects.Count == 32, "all original layers including sign bit");
        lease.Move(31); lease.Move(30);
        Check(nodes.All(n => n.Layer == 30), "repeat move did not lose object ownership");
        bool rejected = false; try { lease.Capture(nodes[0]); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "capture cannot overwrite original moved layers");
        lease.Restore(); lease.Restore(); Check(nodes.Select((n, i) => n.Layer == i).All(x => x), "idempotent exact restoration");
        fail = true; writes = 0;
        try { lease.Move(29); } catch (InvalidOperationException) { }
        fail = false; lease.Restore(); Check(nodes.Select((n, i) => n.Layer == i).All(x => x), "partial move failure restored every touched object");
        lease.Move(28); nodes[10].Dead = true; lease.Restore();
        Check(nodes.Select((n, i) => i == 10 || n.Layer == i).All(x => x), "destroyed object skipped without blocking siblings");
        nodes[10].Dead = false; nodes[10].Layer = 10;
        lease.Move(27); fail = true; writes = 0; rejected = false;
        try { lease.Restore(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && nodes[31].Layer == 31 && lease.Moved, "restore failure still restored later objects and retains retry state");
        fail = false; lease.Restore(); Check(nodes.Select((n, i) => n.Layer == i).All(x => x), "retry restored failed setter");
        // Stable per-object layer switches do not allocate managed storage.
        for (int i = 0; i < 100; i++) { lease.Move(31); lease.Restore(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { lease.Move(31); lease.Move(30); lease.Restore(); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "stable layer move/restore allocates");
        var firstRun = new DirectFullObjectLayers<Node>(n => n != null && !n.Dead, n => n.Layer, (n, l) => n.Layer = l);
        var secondRun = new DirectFullObjectLayers<Node>(n => n != null && !n.Dead, n => n.Layer, (n, l) => n.Layer = l);
        var one = new Node { Layer = 7 }; var two = new Node { Layer = 9 }; firstRun.Capture(one); secondRun.Capture(two);
        var firstList = new System.Collections.Generic.List<DirectFullObjectLayers<Node>> { firstRun };
        var secondList = new System.Collections.Generic.List<DirectFullObjectLayers<Node>> { secondRun };
        DirectFullObjectLayers<Node>.MoveAll(firstList, 30); DirectFullObjectLayers<Node>.MoveAll(secondList, 30);
        try { DirectFullObjectLayers<Node>.MoveAll(firstList, 31); Check(one.Layer == 31 && two.Layer == 30, "first auxiliary run isolated"); }
        finally { DirectFullObjectLayers<Node>.MoveAll(firstList, 30); }
        try { DirectFullObjectLayers<Node>.MoveAll(secondList, 31); Check(one.Layer == 30 && two.Layer == 31, "previous run returned to parking before next render"); }
        finally { DirectFullObjectLayers<Node>.MoveAll(secondList, 30); }
        firstRun.Restore(); secondRun.Restore(); Check(one.Layer == 7 && two.Layer == 9, "multi-run originals restored");
        var ungrouped = (Array.Empty<int>(), Array.Empty<K>(), Key(5, 50));
        var left = (new[] { 100 }, new[] { Key(0, 100) }, Key(9999, 10));
        var right = (new[] { 200 }, new[] { Key(10, 200) }, Key(-9999, 20));
        Check(Sort(left, ungrouped) < 0 && Sort(ungrouped, right) < 0, "outer group key takes precedence over inner extreme order");
        var nestedA = (new[] { 100, 101 }, new[] { Key(0, 100), Key(1, 101) }, Key(9999, 30));
        var nestedB = (new[] { 100, 102 }, new[] { Key(0, 100), Key(2, 102) }, Key(-9999, 40));
        Check(Sort(nestedA, nestedB) < 0, "nested group external key precedes leaf key");
        var sibling = (new[] { 100, 101 }, new[] { Key(0, 100), Key(1, 101) }, Key(9998, 31));
        Check(Sort(sibling, nestedA) < 0, "common nested group uses internal renderer keys");
        var random = new Random(404); var items = new (int[] ids, K[] keys, K leaf)[200];
        for (int i = 0; i < items.Length; i++)
        {
            int outer = random.Next(6), inner = random.Next(5), depth = random.Next(3);
            items[i] = depth == 0 ? (Array.Empty<int>(), Array.Empty<K>(), Key(random.Next(-4, 10), 10000 + i))
                : depth == 1 ? (new[] { 100 + outer }, new[] { Key(outer, 100 + outer) }, Key(random.Next(-20, 20), 10000 + i))
                : (new[] { 100 + outer, 1000 + outer * 10 + inner }, new[] { Key(outer, 100 + outer), Key(inner, 1000 + outer * 10 + inner) }, Key(random.Next(-20, 20), 10000 + i));
        }
        Array.Sort(items, Sort);
        for (int i = 0; i < items.Length; i++) for (int j = i; j < items.Length; j++)
            Check(Sort(items[i], items[j]) <= 0 && Math.Sign(Sort(items[i], items[j])) == -Math.Sign(Sort(items[j], items[i])), "group comparator transitivity/antisymmetry");
        Console.WriteLine($"PASS production per-object layer journal + nested group order: checks={checks} stable_layer_allocated={allocated}; Unity callbacks/physics=UNMEASURED");
    }
}
