using StutterFix;

static class VertexUpdateFixture
{
    sealed class Member { internal int Value; internal bool Dirty; }
    readonly struct Writer : LiveDecoPolicy.IVertexWriter<Member, int>
    {
        public bool Dirty(Member m) => m.Dirty;
        public void Write(Member m, int[] target, int offset)
        { for (int k = 0; k < 4; k++) target[offset + k] = m.Value * 10 + k; m.Dirty = false; }
    }
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string label) { checks++; if (!ok) throw new Exception("vertex update: " + label); }
        bool Update(Member[] members, int[] target, int offset, bool force) =>
            LiveDecoPolicy.UpdateVertices<Member, int, Writer>(members, target, offset, force, default);
        var a = new Member { Value = 1 }; var b = new Member { Value = 2 };
        var members = new[] { a, b }; var target = Enumerable.Repeat(-1, 24).ToArray();
        Check(Update(members, target, 4, true), "new run always uploads even when every member is clean");
        Check(target.Skip(4).Take(8).SequenceEqual(new[] { 10, 11, 12, 13, 20, 21, 22, 23 }), "initial full run");
        var saved = (int[])target.Clone();
        Check(!Update(members, target, 4, false) && target.SequenceEqual(saved), "stable run leaves CPU and GPU contents alone");
        a.Value = 3; a.Dirty = true;
        Check(Update(members, target, 12, false), "one dirty member uploads moved reused run");
        Check(target.Skip(12).Take(8).SequenceEqual(new[] { 30, 31, 32, 33, 20, 21, 22, 23 }), "unchanged neighbour rewritten at NEW shared-array offset");
        Check(!a.Dirty && !b.Dirty && target.Take(4).All(x => x == -1), "dirty flags cleared and unrelated ranges intact");
        Check(Update(new[] { b, a }, target, 0, true) && target.Take(8).SequenceEqual(new[] { 20, 21, 22, 23, 30, 31, 32, 33 }), "repartition forces correct new order");
        // Exercise arbitrary stable/dirty runs and moved offsets against a full
        // reference upload, including unchanged neighbours in a shared buffer.
        var random = new Random(42);
        for (int trial = 0; trial < 200; trial++)
        {
            int count = random.Next(1, 35), offset = random.Next(0, 9) * 4;
            var rows = Enumerable.Range(0, count).Select(i => new Member { Value = i, Dirty = random.Next(3) == 0 }).ToArray();
            bool dirty = rows.Any(m => m.Dirty); bool force = random.Next(5) == 0;
            var buffer = Enumerable.Repeat(-1, offset + count * 4 + 4).ToArray();
            Check(Update(rows, buffer, offset, force) == (dirty || force), "upload iff forced or any dirty");
            Check(buffer.Take(offset).All(x => x == -1) && buffer.Skip(offset + count * 4).All(x => x == -1), "no adjacent run overwritten");
            if (dirty || force) Check(buffer.Skip(offset).Take(count * 4).SequenceEqual(rows.SelectMany(m => Enumerable.Range(m.Value * 10, 4))), "entire dirty run equals full reference");
        }
        for (int i = 0; i < 100; i++) { a.Dirty = true; Update(members, target, 0, false); }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { a.Dirty = (i & 1) == 0; Update(members, target, 0, false); }
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "warmed stable and dirty production loop allocate zero bytes in .NET fixture");
        Console.WriteLine($"LiveDeco dirty vertex uploads: {checks} checks passed; shared production writer, moved offsets, stable skip; Unity GPU untested");
        return checks;
    }
}
