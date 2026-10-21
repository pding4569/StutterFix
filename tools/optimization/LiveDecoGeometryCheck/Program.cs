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

int boundsStart = checks;
// Reproduce the old category error: exact flat vertices vs a thick culling AABB.
var flatPivot = new Transform(); var flatChild = new Transform { parent = flatPivot };
var flatGeometry = LiveDecoGeometry.Capture(flatPivot, flatChild, flatChild, corners, false, false);
var paddedBox = new Bounds(new Vector3(0, 0, 0), new Vector3(4, 2, .2f));
var flatBox = new Bounds(new Vector3(0, 0, 0), new Vector3(4, 2, 0));
Check(N.Vector3.Distance(flatBox.min.Native, paddedBox.min.Native) > .099f, "old flat-box comparison must fail with 0.1 error");
var corrected = flatGeometry.WorldBounds(paddedBox, Vector3.zero, Vector3.zero, Quaternion.Z(0), new Vector2(1, 1));
Check(corrected.min.Equals(paddedBox.min) && corrected.max.Equals(paddedBox.max), "matching culling boxes must pass without widening tolerance");
// An asymmetric localBounds already describes a flipped renderer; do not flip it again.
for (int sample = 0; sample < 160; sample++)
{
    var pivot = new Transform(); var top = pivot;
    for (int n = 0; n < sample % 3; n++) top = Node(top);
    var child = Node(top); top = child;
    for (int n = 0; n < sample / 3 % 3; n++) top = Node(top);
    var renderer = top;
    if (sample % 5 == 0) child.localScale = new Vector3(0, -2, .5f);
    bool flipX = (sample & 1) != 0, flipY = (sample & 2) != 0;
    var geometry = LiveDecoGeometry.Capture(pivot, child, renderer, corners, flipX, flipY);
    var box = new Bounds(new Vector3(flipX ? -.3f : .3f, flipY ? .2f : -.2f, .07f), new Vector3(4, 2, .2f));
    var logicalOffset = new Vector2(Rand(), Rand());
    var position = new Vector3(Rand(), Rand(), 0); var rotation = Quaternion.Z(Rand());
    var scale = new Vector2(sample % 7 == 0 ? 0 : Rand(), Rand());
    var actual = geometry.WorldBounds(box, geometry.Offset(logicalOffset), position, rotation, scale);
    var referenceMin = new N.Vector3(float.PositiveInfinity); var referenceMax = new N.Vector3(float.NegativeInfinity);
    for (int n = 0; n < 8; n++)
    {
        var point = new N.Vector3((n & 1) == 0 ? box.min.x : box.max.x,
            (n & 2) == 0 ? box.min.y : box.max.y, (n & 4) == 0 ? box.min.z : box.max.z);
        for (var t = renderer; t != pivot; t = t.parent)
        {
            point *= t.localScale.Native;
            point = N.Vector3.Transform(point, t.localRotation.Native);
            point += t == child ? new N.Vector3(logicalOffset.x, logicalOffset.y, 0) : t.localPosition.Native;
        }
        point *= new N.Vector3(scale.x, scale.y, 1);
        point = N.Vector3.Transform(point, rotation.Native) + position.Native;
        referenceMin = N.Vector3.Min(referenceMin, point); referenceMax = N.Vector3.Max(referenceMax, point);
    }
    Check(N.Vector3.Distance(actual.min.Native, referenceMin) < .0001f, "logical culling min must match independent eight-corner reference");
    Check(N.Vector3.Distance(actual.max.Native, referenceMax) < .0001f, "logical culling max must match independent eight-corner reference");
    // The same 1e-4 bound must continue to detect actual translation errors.
    Check(N.Vector3.Distance(actual.min.Native, referenceMin + new N.Vector3(.001f, 0, 0)) > .0001f, "incorrect pose must still fail");
}
Console.WriteLine($"LiveDeco production culling bounds algorithm: {checks - boundsStart} checks passed; includes old 0.1 false rejection, no Unity bounds thickness assumed");

int scriptStart = checks;
var decoration = new scrVisualDecoration();
var sprite = new SpriteRenderer();
var toggle = new ToggleVisible { transform = sprite.transform, scriptToToggle = decoration };
var scripts = new MonoBehaviour[] { decoration, toggle };
string Reason() => LiveDecoScripts.Reason(scripts, decoration, sprite);
foreach (bool ignore in new[] { false, true })
foreach (bool targetActive in new[] { false, true })
{
    toggle.ignoreIfDisabled = ignore;
    decoration.isActiveAndEnabled = targetActive;
    Check(Reason() == null, "bound prefab callback accepted even when decoration disabled");
}
toggle.scriptToToggle = new MonoBehaviour();
Check(Reason() == "visibility-target:UnityEngine.MonoBehaviour", "foreign target cannot consume stale transform");
toggle.scriptToToggle = null;
Check(Reason() == "visibility-target:missing", "missing target rejected");
toggle.scriptToToggle = new scrVisualDecoration();
Check(Reason() == "visibility-target:scrVisualDecoration", "other decoration rejected");
toggle.scriptToToggle = decoration;
toggle.transform = new Transform();
Check(Reason() == "visibility-node", "other renderer callback rejected");
toggle.transform = sprite.transform;
scripts = new MonoBehaviour[] { new ForeignScripts.ToggleVisible() };
Check(Reason() == "foreign-child-script:ForeignScripts.ToggleVisible", "same short name cannot bypass exact type");
scripts = new MonoBehaviour[] { new DerivedVisibility() };
Check(Reason() == "foreign-child-script:DerivedVisibility", "subclass may override callbacks");
var unknown = new MonoBehaviour { isActiveAndEnabled = false };
scripts = new MonoBehaviour[] { toggle, unknown };
Check(Reason() == null, "inactive unknown script does not run");
unknown.isActiveAndEnabled = true;
Check(Reason() == "foreign-child-script:UnityEngine.MonoBehaviour", "activation after admission forces release");
unknown.Destroyed = true;
Check(Reason() == null, "destroyed component ignored");
toggle.scriptToToggle = new MonoBehaviour();
Check(Reason() != null, "rebinding after admission forces release");
toggle.scriptToToggle = decoration;
decoration.parallax = new MonoBehaviour(); decoration.blendModeEffect = new MonoBehaviour();
decoration.meshBlendModeEffect = new MonoBehaviour(); decoration.spriteAlphaMask = new MonoBehaviour();
scripts = new[] { decoration, decoration.parallax, decoration.blendModeEffect,
    decoration.meshBlendModeEffect, decoration.spriteAlphaMask, toggle };
Check(Reason() == null, "existing role references preserved");
Console.WriteLine($"LiveDeco production script/binding policy: {checks - scriptStart} checks passed; managed roles only, no Unity callbacks proven");

int poseStart = checks;
// Exercise the actual production cache against an independent S/R/T oracle.
// Each input changes alone; even subpixel motion must invalidate the cache.
var poseRoot = new Transform(); var poseChild = Node(poseRoot); var poseLeaf = Node(poseChild);
var pose = LiveDecoGeometry.Capture(poseRoot, poseChild, poseLeaf, corners, true, false);
var inputs = new[] {
    (P: new Vector2(10, 20), S: new Vector2(2, -3), O: new Vector2(1, 2), A: 17f),
    (P: new Vector2(10.000001f, 20), S: new Vector2(2, -3), O: new Vector2(1, 2), A: 17f),
    (P: new Vector2(10.000001f, 20), S: new Vector2(0, -3), O: new Vector2(1, 2), A: 17f),
    (P: new Vector2(10.000001f, 20), S: new Vector2(0, -3), O: new Vector2(-1, 2), A: 17f),
    (P: new Vector2(10.000001f, 20), S: new Vector2(0, -3), O: new Vector2(-1, 2), A: 17.000002f),
};
foreach (var input in inputs)
{
    Check(pose.UpdatePose(input.P, input.S, input.O, input.A), "every changed pose component invalidates");
    for (int k = 0; k < 4; k++)
    {
        var expected = new N.Vector3(-corners[k].x, corners[k].y, 0);
        for (var t = poseLeaf; t != poseRoot; t = t.parent)
        {
            expected *= t.localScale.Native;
            expected = N.Vector3.Transform(expected, t.localRotation.Native);
            expected += t == poseChild ? new N.Vector3(input.O.x, input.O.y, 0) : t.localPosition.Native;
        }
        expected *= new N.Vector3(input.S.x, input.S.y, 1);
        expected = N.Vector3.Transform(expected, N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitZ, input.A * MathF.PI / 180));
        expected += new N.Vector3(input.P.x, input.P.y, 0);
        Check(N.Vector3.Distance(expected, pose.World[k].Native) < 1e-4f, "cached production pose vs leaf-to-world oracle");
    }
    var savedWorld = pose.World.Select(v => v.Native).ToArray();
    Check(!pose.UpdatePose(input.P, input.S, input.O, input.A), "unchanged frame skips vertex computation");
    Check(savedWorld.SequenceEqual(pose.World.Select(v => v.Native)), "skipped frame preserves all vertex bits");
    Check(pose.UpdatePose(input.P, input.S, input.O, input.A, true), "admission always validates a fresh pose");
}
Console.WriteLine($"LiveDeco production pose/dirty cache: {checks - poseStart} checks passed; exact invalidation, managed adapter only");
int uploadStart = checks;
foreach (bool relocated in new[] { false, true })
foreach (bool positionChanged in new[] { false, true })
foreach (bool colorChanged in new[] { false, true })
{
    var upload = new LiveDecoUploadState(relocated);
    Check(upload.Fill(positionChanged, colorChanged) == (relocated || positionChanged || colorChanged), "changed member fill");
    Check(upload.Fill(false, false) == relocated, "dirty member does not dirty unchanged neighbor");
    Check(upload.Dirty == (relocated || positionChanged || colorChanged), "run upload scheduled once for all member changes");
    // A subsequent stable frame must not inherit the previous upload bit.
    var nextFrame = new LiveDecoUploadState(false);
    Check(!nextFrame.Fill(false, false) && !nextFrame.Dirty, "unchanged next frame skips upload");
}
Console.WriteLine($"LiveDeco production upload invalidation: {checks - uploadStart} checks passed; new/relocated buffers, color-only, pose-only, unchanged neighbors");

class DerivedVisibility : ToggleVisible { }
namespace ForeignScripts { class ToggleVisible : MonoBehaviour { } }
