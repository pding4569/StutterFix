#if DEV || AUTOTEST
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // A relocated/new run needs every shared-buffer entry filled. In an existing
    // run a changed member schedules one upload, without dirtying its neighbors.
    internal struct LiveDecoUploadState
    {
        readonly bool fillAll;
        internal bool Dirty { get; private set; }
        internal LiveDecoUploadState(bool invalidBuffer) { fillAll = invalidBuffer; Dirty = invalidBuffer; }
        internal bool Fill(bool poseChanged, bool colorChanged)
        {
            bool fill = fillAll || poseChanged || colorChanged;
            Dirty |= fill; return fill;
        }
    }

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
        readonly Matrix4x4 above, shape;
        internal readonly Vector3[] Shape = new Vector3[4];
        internal readonly Vector3[] World = new Vector3[4];
        Vector2 lastPosition, lastScale, lastOffset;
        float lastAngle;
        Quaternion rotation;
        bool poseValid, rotationValid;
        internal Quaternion Rotation => rotation;

        // Only managed values enter this hot path. Exact comparisons intentionally
        // retain subpixel changes; no epsilon-based dirty suppression.
        internal bool UpdatePose(Vector2 position, Vector2 scale, Vector2 offset, float angle, bool force = false)
        {
            if (!force && poseValid && position.Equals(lastPosition) && scale.Equals(lastScale)
                && offset.Equals(lastOffset) && angle.Equals(lastAngle)) return false;
            if (!rotationValid || !angle.Equals(lastAngle))
            {
                rotation = Quaternion.Normalize(Quaternion.Euler(0, 0, angle));
                rotationValid = true;
            }
            Vector3 translated = Offset(offset);
            for (int n = 0; n < 4; n++)
            {
                Vector3 local = Shape[n] + translated;
                local = rotation * new Vector3(local.x * scale.x, local.y * scale.y, local.z);
                World[n] = new Vector3(position.x + local.x, position.y + local.y, local.z);
            }
            lastPosition = position; lastScale = scale; lastOffset = offset; lastAngle = angle; poseValid = true;
            return true;
        }
        internal int Depth => nodes.Count;
        internal bool SupportsImageMatrix=>child.parent==pivot&&renderer.parent==child&&renderer.localPosition==Vector3.zero
            &&renderer.localRotation==Quaternion.identity&&renderer.localScale==Vector3.one&&above==Matrix4x4.identity;
        internal Matrix4x4 ImageMatrix=>shape;
        LiveDecoGeometry(Transform pivot, Transform child, Transform renderer, Matrix4x4 above, Matrix4x4 shape)
        { this.pivot = pivot; this.child = child; this.renderer = renderer; this.above = above; this.shape = shape; }

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
            var result = new LiveDecoGeometry(pivot, child, renderer, above, shape);
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

        // Renderer.localBounds is a culling box, not the flat sprite polygon.
        // Transform all eight corners with the same logical pose as the vertices.
        // localBounds already includes the renderer's flip; do not flip it twice.
        internal Bounds WorldBounds(Bounds localBounds, Vector3 offset, Vector3 position,
            Quaternion rotation, Vector2 scale)
        {
            Vector3 min = localBounds.min, max = localBounds.max;
            var result = new Bounds();
            for (int n = 0; n < 8; n++)
            {
                var point = new Vector3((n & 1) == 0 ? min.x : max.x,
                    (n & 2) == 0 ? min.y : max.y, (n & 4) == 0 ? min.z : max.z);
                Vector3 local = above.MultiplyVector(shape.MultiplyPoint3x4(point)) + offset;
                local = rotation * new Vector3(local.x * scale.x, local.y * scale.y, local.z);
                Vector3 world = position + local;
                if (n == 0) result = new Bounds(world, Vector3.zero); else result.Encapsulate(world);
            }
            return result;
        }
    }
}
#endif
