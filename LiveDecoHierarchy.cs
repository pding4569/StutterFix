namespace StutterFix
{
    // Shared with the offline fixture. Walk local transforms from the renderer
    // to (but excluding) the pivot, replacing only the logical child position.
    internal static class LiveDecoHierarchy
    {
        internal const int MaxDepth = 16;
        internal enum Failure { None, MissingNode, ChildIsPivot, RendererOutsideChild, PivotNotAncestor, DepthLimit }
        internal interface IPath<TNode, TMatrix>
        {
            bool Alive(TNode node);
            bool Same(TNode a, TNode b);
            TNode Parent(TNode node);
            TMatrix Identity { get; }
            TMatrix Local(TNode node, bool child);
            TMatrix Multiply(TMatrix a, TMatrix b);
        }
        // Same multiplication order as the live walk: no reassociation of TRS
        // products (which could introduce a new floating point difference).
        internal static TMatrix ComposeCached<TNode, TMatrix, TPath>(TMatrix[] locals, int childIndex,
            TMatrix child, TPath path) where TPath : struct, IPath<TNode, TMatrix>
        {
            var matrix = path.Identity;
            for (int i = 0; i < locals.Length; i++) matrix = path.Multiply(i == childIndex ? child : locals[i], matrix);
            return matrix;
        }
        internal static Failure Compose<TNode, TMatrix, TPath>(TNode renderer, TNode child, TNode pivot,
            TPath path, bool calculate, out TMatrix matrix, out int depth) where TPath : struct, IPath<TNode, TMatrix>
        {
            matrix = path.Identity; depth = 0;
            if (!path.Alive(renderer) || !path.Alive(child) || !path.Alive(pivot)) return Failure.MissingNode;
            if (path.Same(child, pivot)) return Failure.ChildIsPivot;
            bool sawChild = false;
            var node = renderer;
            while (path.Alive(node) && !path.Same(node, pivot))
            {
                if (depth == MaxDepth) return Failure.DepthLimit;
                bool isChild = path.Same(node, child);
                sawChild |= isChild;
                if (calculate) matrix = path.Multiply(path.Local(node, isChild), matrix);
                depth++;
                node = path.Parent(node);
            }
            if (!path.Alive(node)) return Failure.PivotNotAncestor;
            return sawChild ? Failure.None : Failure.RendererOutsideChild;
        }
    }
}
