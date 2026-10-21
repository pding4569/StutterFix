#if DEV || AUTOTEST
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // Cache fixed local segments, never invert a world matrix (animated scale
    // may be zero). The game's logical offset belongs to Child, not Renderer.
    internal sealed class LiveDecoGeometry
    {
        struct Node
        {
            internal Transform T, Parent;
            internal Vector3 Position, Scale;
            internal Quaternion Rotation;
        }
        readonly List<Node> nodes = new List<Node>();
        readonly Transform pivot, child, renderer;
        readonly Matrix4x4 above;
        internal readonly Vector3[] Shape = new Vector3[4];
        internal int Depth => nodes.Count;
        LiveDecoGeometry(Transform pivot, Transform child, Transform renderer, Matrix4x4 above)
        { this.pivot = pivot; this.child = child; this.renderer = renderer; this.above = above; }

        internal static LiveDecoGeometry Capture(Transform pivot, Transform child, Transform renderer,
            Vector2[] corners, bool flipX, bool flipY)
        {
            if (!pivot || !child || !renderer || child == pivot || corners.Length != 4) return null;
            var path = new List<Node>();
            var t = renderer;
            int childIndex = -1;
            while (t && t != pivot && path.Count < 16)
            {
                var p = t.localPosition; var q = t.localRotation; var s = t.localScale;
                // Keep Z/sort-distance flat. Other geometry stays on the original path.
                if (p.z != 0 || q.x != 0 || q.y != 0) return null;
                if (t == child) childIndex = path.Count;
                path.Add(new Node { T = t, Parent = t.parent, Position = p, Rotation = q, Scale = s });
                t = t.parent;
            }
            if (t != pivot || childIndex < 0) return null;
            Matrix4x4 below = Matrix4x4.identity, above = Matrix4x4.identity;
            for (int n = 0; n < childIndex; n++)
            { var x = path[n]; below = Matrix4x4.TRS(x.Position, x.Rotation, x.Scale) * below; }
            for (int n = childIndex + 1; n < path.Count; n++)
            { var x = path[n]; above = Matrix4x4.TRS(x.Position, x.Rotation, x.Scale) * above; }
            var c = path[childIndex];
            var shape = Matrix4x4.TRS(Vector3.zero, c.Rotation, c.Scale) * below;
            var result = new LiveDecoGeometry(pivot, child, renderer, above);
            result.nodes.AddRange(path);
            for (int n = 0; n < 4; n++)
            {
                Vector3 local = corners[n];
                if (flipX) local.x = -local.x;
                if (flipY) local.y = -local.y;
                result.Shape[n] = above.MultiplyVector(shape.MultiplyPoint3x4(local));
            }
            return result;
        }
        internal bool Matches(Transform currentPivot, Transform currentChild, Transform currentRenderer)
        {
            if (pivot != currentPivot || child != currentChild || renderer != currentRenderer) return false;
            foreach (var n in nodes)
                if (!n.T || n.T.parent != n.Parent || !n.T.localRotation.Equals(n.Rotation)
                    || !n.T.localScale.Equals(n.Scale) || n.T.localPosition.z != 0
                    || (n.T != child && !n.T.localPosition.Equals(n.Position))) return false;
            return true;
        }
        internal Vector3 Offset(Vector2 logical) => above.MultiplyPoint3x4(logical);
    }
}
#endif
