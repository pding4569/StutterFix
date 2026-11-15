using System;
using System.Linq;
using StutterFix;
using K = StutterFix.SceneRenderOrder.Key;

sealed class OrderValue { internal int Key, Id; }
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
        int legacyChecks = checks;
        // Persistent node ownership, sparse edits, duplicate priorities and long
        // remove/reinsert sequences must preserve total order without allocations.
        const int size = 7000;
        var values = Enumerable.Range(0, size).Select(i => new OrderValue { Key = i, Id = i }).ToArray();
        var order = new DirectFullOrderTree<OrderValue>((a,b) => { int c = a.Key.CompareTo(b.Key); return c != 0 ? c : a.Id.CompareTo(b.Id); });
        var treeNodes = values.Select((v,i) => new DirectFullOrderTree<OrderValue>.Node(v,(uint)i+1)).ToArray();
        var actual = new System.Collections.Generic.List<OrderValue>(size);
        foreach (var n in treeNodes) order.Add(n);
        var expected = new OrderValue[size];
        for (int iteration=0;iteration<1000;iteration++)
        {
            for(int j=0;j<7;j++){int i=random.Next(size);order.Remove(treeNodes[i]);values[i].Key=random.Next(-10000,10000);order.Add(treeNodes[i]);}
            order.CopyTo(actual);Array.Copy(values,expected,size);Array.Sort(expected,(a,b)=>{int c=a.Key.CompareTo(b.Key);return c!=0?c:a.Id.CompareTo(b.Id);});
            for(int i=0;i<size;i++)Check(ReferenceEquals(actual[i],expected[i]),"incremental order differs from total order oracle");
        }
        long treeBefore=GC.GetAllocatedBytesForCurrentThread();var stamp=System.Diagnostics.Stopwatch.GetTimestamp();
        for(int iteration=0;iteration<10000;iteration++){int i=iteration%size;order.Remove(treeNodes[i]);values[i].Key=-iteration;order.Add(treeNodes[i]);}
        long treeAllocated=GC.GetAllocatedBytesForCurrentThread()-treeBefore;
        double treeUs=(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1e6/System.Diagnostics.Stopwatch.Frequency/10000;
        Check(treeAllocated==0,"sparse order edits allocate");
        order.CopyTo(actual);
        Check(order.Matches(actual),"identical order must skip command rebuild");
        // All keys move together without crossing. Reinsertions update the tree's
        // search keys, but the emitted sequence and its command buffers stay valid.
        for(int i=0;i<size;i++)order.Remove(treeNodes[i]);
        for(int i=0;i<size;i++)values[i].Key+=20000;
        foreach(var n in treeNodes)order.Add(n);
        Check(order.Matches(actual),"common distance drift incorrectly replans");
        var crossing=treeNodes[actual[size/2].Id];order.Remove(crossing);crossing.Value.Key=int.MinValue;order.Add(crossing);
        Check(!order.Matches(actual),"actual relative-order crossing was skipped");
        order.CopyTo(actual);Check(order.Matches(actual),"crossing sequence cannot stabilize");
        order.Remove(crossing);Check(!order.Matches(actual),"removed node hidden by order gate");order.Add(crossing);
        long matchesBefore=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)order.Matches(actual);
        Check(GC.GetAllocatedBytesForCurrentThread()==matchesBefore,"order identity gate allocates");
        Console.WriteLine("PASS M4g order identities common-drift=skip crossing=replan removed=replan stable_checks=10000 allocated=0");
        // M4m production neighbour proof: mutate the entire co-moving set before
        // checking. The tree stays valid without remove/reinsert or a full walk.
        order.CopyTo(actual);
        for(int i=0;i<size;i++)values[i].Key+=50000;
        for(int i=0;i<size;i++)Check(order.OrderedAt(actual,i),"co-motion neighbour proof failed");
        Check(order.Matches(actual),"in-place noncrossing update corrupted tree");
        var moving=actual[size/2];int oldKey=moving.Key;moving.Key=int.MinValue;
        Check(!order.OrderedAt(actual,size/2),"crossing passed neighbour proof");
        moving.Key=oldKey; // rollback before tree search/removal
        order.Remove(treeNodes[moving.Id]);moving.Key=int.MinValue;order.Add(treeNodes[moving.Id]);
        Check(!order.Matches(actual),"crossing fallback failed");order.CopyTo(actual);
        long neighbourBefore=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++)order.OrderedAt(actual,i%size);
        Check(GC.GetAllocatedBytesForCurrentThread()==neighbourBefore,"neighbour proof allocates");
        Console.WriteLine("PASS M4m distance neighbours sources=7000 common-motion=in_place crossing=rollback_reinsert checks=10000 allocated=0");
        foreach(var mode in new[]{"", "Always", "ForwardBase"})
            Check(DirectFullExplicitPolicy.CameraPass(mode)&&!DirectFullExplicitPolicy.NonCameraPass(mode),"forward explicit pass rejected: "+mode);
        foreach(var mode in new[]{"ShadowCaster", "Meta", "MotionVectors", "DepthOnly", "DepthNormals", "Deferred", "PrepassBase", "PrepassFinal"})
            Check(!DirectFullExplicitPolicy.CameraPass(mode)&&DirectFullExplicitPolicy.NonCameraPass(mode),"non-camera explicit pass submitted: "+mode);
        foreach(var mode in new[]{"ForwardAdd", "Vertex", "Unknown"})
            Check(!DirectFullExplicitPolicy.CameraPass(mode)&&!DirectFullExplicitPolicy.NonCameraPass(mode),"unverified light mode silently accepted: "+mode);
        Console.WriteLine("PASS M4h explicit pass policy forward=3 excluded=8 unknown_rejected=3");
        Console.WriteLine("PASS M4f incremental order sources="+size+" oracle_edits=7000 allocated="+treeAllocated+" edit_us="+treeUs.ToString("F3"));
        Console.WriteLine($"PASS production per-object layer journal + nested group order: checks={legacyChecks} stable_layer_allocated={allocated}; Unity callbacks/physics=UNMEASURED");
    }
}
