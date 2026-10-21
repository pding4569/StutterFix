using StutterFix;
using UnityEngine;
using N = System.Numerics;

int checks = 0;
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
var corners = new[] { new Vector2(-2, -1), new Vector2(2, -1), new Vector2(-2, 1), new Vector2(2, 1) };
var random = new Random(2172);
float Rand() => (float)(random.NextDouble() * 4 - 2);
Transform Node(Transform parent) => new Transform { parent = parent,
    localPosition = new Vector3(Rand(), Rand(), 0), localRotation = Quaternion.Z(Rand()),
    localScale = new Vector3(Rand(), Rand(), 1) };
for (int sample = 0; sample < 160; sample++)
{
    var pivot = new Transform();
    var top = pivot;
    for (int n = 0; n < sample % 4; n++) top = Node(top);
    var child = Node(top); top = child;
    for (int n = 0; n < sample / 4 % 4; n++) top = Node(top);
    var renderer = top;
    // Singular child scales must not require a world-matrix inverse.
    if (sample % 5 == 0) child.localScale = new Vector3(0, -2, 1);
    bool flipX = (sample & 1) != 0, flipY = (sample & 2) != 0;
    var geometry = LiveDecoGeometry.Capture(pivot, child, renderer, corners, flipX, flipY);
    Check(geometry != null, "supported chain rejected");
    Check(geometry.Matches(pivot, child, renderer), "initial snapshot");
    for (int frame = 0; frame < 3; frame++)
    {
        var offset = new Vector2(Rand(), Rand()); var translated = geometry.Offset(offset);
        for (int k = 0; k < 4; k++)
        {
            var expected = new N.Vector3(corners[k].x * (flipX ? -1 : 1), corners[k].y * (flipY ? -1 : 1), 0);
            // Independent reference: apply every local S/R/T in leaf-to-pivot order.
            for (var t = renderer; t != pivot; t = t.parent)
            {
                expected *= t.localScale.Native;
                expected = N.Vector3.Transform(expected, t.localRotation.Native);
                expected += t == child ? new N.Vector3(offset.x, offset.y, 0) : t.localPosition.Native;
            }
            var actual = geometry.Shape[k].Native + translated.Native;
            Check(N.Vector3.Distance(actual, expected) < 0.0001f, "chain/offset/flip transform mismatch");
        }
    }
    child.localPosition = new Vector3(90, 30, 0);
    Check(geometry.Matches(pivot, child, renderer), "logical child XY is intentionally stale");
    child.localPosition = new Vector3(90, 30, 1);
    Check(!geometry.Matches(pivot, child, renderer), "child Z change must release");
    child.localPosition = new Vector3(90, 30, 0);
    child.localScale = new Vector3(3, 4, 1);
    Check(!geometry.Matches(pivot, child, renderer), "scale change must release");
}
var root = new Transform(); var c = Node(root); var leaf = Node(c);
var saved = LiveDecoGeometry.Capture(root, c, leaf, corners, false, false);
leaf.localPosition = new Vector3(8, 9, 0);
Check(!saved.Matches(root, c, leaf), "fixed segment position change");
leaf = Node(c); saved = LiveDecoGeometry.Capture(root, c, leaf, corners, false, false);
leaf.parent = root; Check(!saved.Matches(root, c, leaf), "reparent must release");
Check(LiveDecoGeometry.Capture(root, c, leaf, corners, false, false) == null, "unrelated child");
leaf.parent = c; leaf.localRotation = new Quaternion { x = 1, w = 1 };
Check(LiveDecoGeometry.Capture(root, c, leaf, corners, false, false) == null, "nonplanar renderer");
leaf.localRotation = Quaternion.Z(0); leaf.parent = leaf;
Check(LiveDecoGeometry.Capture(root, c, leaf, corners, false, false) == null, "cycle bounded");
leaf.parent = c; saved = LiveDecoGeometry.Capture(root, c, leaf, corners, false, false);
leaf.Destroyed = true; Check(!saved.Matches(root, c, leaf), "destroyed segment");
leaf = Node(c); saved = LiveDecoGeometry.Capture(root, c, leaf, corners, false, false);
leaf.localRotation = Quaternion.Z(1);
Check(!saved.Matches(root, c, leaf), "fixed segment rotation change");
// Exactly 16 local segments are supported; deeper chains must fail closed.
var deepRoot = new Transform(); var deepChild = Node(deepRoot); var deepLeaf = deepChild;
for (int n = 1; n < 16; n++) deepLeaf = Node(deepLeaf);
var deep = LiveDecoGeometry.Capture(deepRoot, deepChild, deepLeaf, corners, false, false);
Check(deep != null && deep.Depth == 16, "maximum supported depth");
deepLeaf = Node(deepLeaf);
Check(LiveDecoGeometry.Capture(deepRoot, deepChild, deepLeaf, corners, false, false) == null,
    "depth limit must fail closed");
Console.WriteLine($"LiveDeco production hierarchy algorithm: {checks} checks passed; managed math adapter, no Unity pixels/performance proven");
