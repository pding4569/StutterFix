#nullable enable
using System.Numerics;
using StutterFix;

static class HierarchyFixture
{
    sealed class Node
    {
        internal Node? Parent;
        internal Vector3 Position;
        internal Quaternion Rotation = Quaternion.Identity;
        internal Vector3 Scale = Vector3.One;
    }
    readonly struct Path : LiveDecoHierarchy.IPath<Node, Matrix4x4>
    {
        readonly bool owned;
        readonly Vector3 logical;
        internal Path(bool own, Vector3 position) { owned = own; logical = position; }
        public bool Alive(Node node) => node != null;
        public bool Same(Node a, Node b) => ReferenceEquals(a, b);
        public Node Parent(Node node) => node.Parent!;
        public Matrix4x4 Identity => Matrix4x4.Identity;
        public Matrix4x4 Local(Node node, bool child) => LocalMatrix(node, child && owned ? logical : node.Position);
        // Numerics uses row vectors; reverse multiplication to model Unity's
        // column-vector composition while exercising the same production walk.
        public Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b) => b * a;
    }
    static Matrix4x4 LocalMatrix(Node node, Vector3 position) => Matrix4x4.CreateScale(node.Scale)
        * Matrix4x4.CreateFromQuaternion(node.Rotation) * Matrix4x4.CreateTranslation(position);
    internal static int Run()
    {
        int count = 0;
        void Check(bool valid, string label) { count++; if (!valid) throw new Exception("hierarchy: " + label); }
        LiveDecoHierarchy.Failure Compose(Node r, Node c, Node p, Path path, bool calc, out Matrix4x4 matrix, out int depth)
            => LiveDecoHierarchy.Compose<Node, Matrix4x4, Path>(r, c, p, path, calc, out matrix, out depth);
        var pivot = new Node { Position = new(9, -7, 0), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .7f), Scale = new(2, 3, 1) };
        var above = new Node { Parent = pivot, Position = new(3, -2, 0), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -.5f) };
        var child = new Node { Parent = above, Position = new(-1, 4, 0), Scale = new(.4f, 1.7f, 1) };
        var below = new Node { Parent = child, Position = new(2, 1, 0), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .3f) };
        var renderer = new Node { Parent = below, Position = new(-3, 2, 0), Scale = new(-1, 2, 1) };
        var logical = new Vector3(13, -6, 0);
        foreach (bool owned in new[] { false, true })
        {
            var path = new Path(owned, logical);
            Check(Compose(renderer, child, pivot, path, true, out var result, out int depth) == LiveDecoHierarchy.Failure.None && depth == 4, "nested renderer and intermediate above child admitted");
            var expected = LocalMatrix(renderer, renderer.Position) * LocalMatrix(below, below.Position)
                * LocalMatrix(child, owned ? logical : child.Position) * LocalMatrix(above, above.Position) * LocalMatrix(pivot, pivot.Position);
            foreach (var corner in new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) })
                Check(Vector3.Distance(Vector3.Transform(corner, result * LocalMatrix(pivot, pivot.Position)), Vector3.Transform(corner, expected)) < 1e-4f, "full world corners including flip and logical offset");
            Check(child.Position == new Vector3(-1, 4, 0), "owned calculation never writes transform");
            Check(Compose(renderer, child, pivot, path, false, out var untouched, out depth) == LiveDecoHierarchy.Failure.None && untouched == Matrix4x4.Identity && depth == 4, "validation walks without reading local matrices");
        }
        child.Parent = pivot;
        Check(Compose(child, child, pivot, default, true, out var direct, out int directDepth) == LiveDecoHierarchy.Failure.None && directDepth == 1 && direct == LocalMatrix(child, child.Position), "old direct layout preserved");
        Check(Compose(pivot, child, pivot, default, false, out _, out _) == LiveDecoHierarchy.Failure.RendererOutsideChild, "renderer on pivot excluded");
        Check(Compose(above, child, pivot, default, false, out _, out _) == LiveDecoHierarchy.Failure.RendererOutsideChild, "sibling renderer excluded");
        Check(Compose(renderer, pivot, pivot, default, false, out _, out _) == LiveDecoHierarchy.Failure.ChildIsPivot, "child aliases pivot excluded");
        Check(Compose(renderer, null!, pivot, default, false, out _, out _) == LiveDecoHierarchy.Failure.MissingNode, "missing child excluded");
        child.Parent = null;
        Check(Compose(renderer, child, pivot, default, false, out _, out _) == LiveDecoHierarchy.Failure.PivotNotAncestor, "reparent outside pivot excluded");
        child.Parent = pivot;
        Node leaf = child;
        for (int i = 1; i < LiveDecoHierarchy.MaxDepth; i++) leaf = new Node { Parent = leaf };
        Check(Compose(leaf, child, pivot, default, true, out _, out int atLimit) == LiveDecoHierarchy.Failure.None && atLimit == LiveDecoHierarchy.MaxDepth, "exact depth bound accepted");
        leaf = new Node { Parent = leaf };
        Check(Compose(leaf, child, pivot, default, true, out _, out _) == LiveDecoHierarchy.Failure.DepthLimit, "over depth bound rejected");
        child.Parent = renderer;
        Check(Compose(renderer, child, pivot, default, false, out _, out _) == LiveDecoHierarchy.Failure.DepthLimit, "cycle bounded in fixture");
        child.Parent = pivot;
        var ownedPath = new Path(true, logical);
        Compose(renderer, child, pivot, ownedPath, true, out var before, out _);
        renderer.Position += new Vector3(1, 2, 0);
        Compose(renderer, child, pivot, ownedPath, true, out var after, out _);
        Check(before != after, "descendant local edits reflected on next evaluation");
        // Warm the generic walk before checking its steady-state allocation.
        for (int i = 0; i < 100; i++) Compose(renderer, child, pivot, ownedPath, true, out _, out _);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) Compose(renderer, child, pivot, ownedPath, true, out _, out _);
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "shared walk allocates zero bytes in warmed .NET fixture");
        Console.WriteLine($"LiveDeco hierarchy: {count} checks passed; logical child substitution, nested corners, fail-closed topology; Unity untested");
        return count;
    }
}
