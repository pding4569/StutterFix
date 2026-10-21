using StutterFix;

DeltaAdapter.Check();

namespace StutterFix
{
    // Engine-free adapters only. Ordering, dirty neighbors, connected-component
    // repair and arena allocation are extracted verbatim from production.
    partial class DeltaAdapter
    {
        sealed class Source { internal Row[] Rows; }
        sealed class Image
        {
            internal Source Source;
            internal string Signature;
            internal int TextureId;
            internal bool Rejected;
            internal string Reason;
        }
        sealed class Row
        {
            internal SceneRenderOrder.Key Key;
            internal bool Listed, Owned;
            internal int Layer;
            internal Image Image;
            internal Run Run;
        }
        sealed class Run
        {
            internal Image[] Members;
            internal int Index, Offset, VertexCount;
            internal bool Disposed;
        }
        struct Vertex { internal int Id; }
        static readonly List<Row> ordered = new();
        static readonly List<Run> runs = new();
        static readonly HashSet<Row> repair = new();
        static readonly HashSet<Run> invalidRuns = new();
        static readonly List<KeyValuePair<int, int>> freeVertices = new();
        static readonly HashSet<Source> notified = new();
        static void Queue(Source source) { notified.Add(source); }
        static Vertex[] vertexBuffer = Array.Empty<Vertex>();
        static int vertexEnd, creates, checks;
        static bool topology;
        static long transitionCount, reconcileTicks;
        static double transitionMs;
        static Run CreateRun(int a, int b)
        {
            var run = new Run { Members = ordered.Skip(a).Take(b - a).Select(x => x.Image).ToArray(), VertexCount = (b - a) * 4 };
            run.Offset = AllocateVertices(run.VertexCount); creates++;
            for (int n = a; n < b; n++) for (int k = 0; k < 4; k++)
                vertexBuffer[run.Offset + (n - a) * 4 + k].Id = ordered[n].Key.Renderer;
            return run;
        }
        static void Dispose(Run run)
        {
            Assert(!run.Disposed, "double disposal"); run.Disposed = true; FreeVertices(run);
        }
        static void Assert(bool value, string why) { checks++; if (!value) throw new Exception(why); }
        static void Verify()
        {
            var expected = LiveDecoPolicy.Count(ordered.Select(x => new LiveDecoPolicy.Item(x.Key, x.Owned,
                x.Image.Signature, x.Layer, x.Image.TextureId)).ToArray(), true);
            Assert(runs.Count == expected.MeshRuns, "partition count differs from independent full oracle");
            var actual = runs.OrderBy(r => r.Members[0].Source.Rows[0].Key).ToArray();
            var ranges = runs.OrderBy(r => r.Offset).ToArray();
            for (int n = 0; n < ranges.Length; n++)
            {
                var r = ranges[n];
                Assert(r.Offset >= 0 && r.Offset + r.VertexCount <= vertexEnd, "arena bounds");
                if (n > 0) Assert(ranges[n - 1].Offset + ranges[n - 1].VertexCount <= r.Offset, "live vertex ranges overlap");
            }
            for (int n = 0; n < actual.Length; n++)
            {
                var run = actual[n];
                Assert(run.Members.Length == expected.Counts[n], "partition length");
                for (int k = 0; k < run.Members.Length; k++)
                {
                    var row = ordered[expected.First[n] + k];
                    Assert(ReferenceEquals(row.Image, run.Members[k]) && ReferenceEquals(row.Run, run), "partition identity/link");
                    for (int v = 0; v < 4; v++) Assert(vertexBuffer[run.Offset + k * 4 + v].Id == row.Key.Renderer, "arena contents corrupted");
                }
            }
            foreach (var row in ordered) Assert(row.Owned == (row.Run != null), "row ownership/run mismatch");
            var snapshots = runs.Select(r => (Run: r, r.Offset)).ToArray(); int prior = creates;
            Reconcile();
            Assert(prior == creates, "steady frame created runs");
            foreach (var s in snapshots) Assert(!s.Run.Disposed && s.Run.Offset == s.Offset, "steady frame moved buffer");
        }
        internal static void Check()
        {
            var random = new Random(20261021); var live = new List<Row>(); int nextId = 1;
            for (int step = 0; step < 6000; step++)
            {
                if (live.Count == 0 || (live.Count < 140 && random.Next(3) == 0))
                {
                    int id = nextId++;
                    var row = new Row { Key = new SceneRenderOrder.Key(0, random.Next(3), 3000, 0, id * 11, id),
                        Owned = random.Next(4) != 0, Layer = random.Next(2),
                        Image = new Image { Signature = "s" + random.Next(2), TextureId = random.Next(2) } };
                    row.Image.Source = new Source { Rows = new[] { row } }; live.Add(row); Insert(row);
                }
                else
                {
                    var row = live[random.Next(live.Count)]; Remove(row);
                    if (random.Next(4) == 0) live.Remove(row);
                    else
                    {
                        row.Owned = random.Next(4) != 0;
                        row.Image.Signature = "s" + random.Next(2); row.Image.TextureId = random.Next(2);
                        row.Key = new SceneRenderOrder.Key(0, random.Next(3), 3000, 0, row.Key.Material, row.Key.Renderer);
                        Insert(row);
                    }
                }
                Reconcile(); Verify();
            }
            foreach (var row in live) Remove(row);
            Reconcile(); Verify();
            Assert(runs.Count == 0 && vertexEnd == 0 && freeVertices.Count == 0, "all storage returned after final sleep");
            var tiedRows = new List<Row>();
            for (int n = 0; n < 3; n++)
            {
                var row = new Row { Key = new SceneRenderOrder.Key(0, 0, 3000, 0, 99, n),
                    Image = new Image { Signature = "tie", Rejected = true, Reason = "material-tie" } };
                row.Image.Source = new Source { Rows = new[] { row } }; Insert(row); tiedRows.Add(row);
            }
            foreach (var row in tiedRows) Assert(HasOtherTie(row), "same-material neighbors detected");
            Remove(tiedRows[1]); Remove(tiedRows[2]);
            Assert(!HasOtherTie(tiedRows[0]) && notified.Contains(tiedRows[0].Image.Source), "last tie survivor notified for re-admission");
            Remove(tiedRows[0]); Reconcile(); Verify();
            Console.WriteLine($"Production delta/index/arena: {checks} checks; 6000 mutations vs full partition oracle; no Unity/FPS proof");
        }
    }
}
