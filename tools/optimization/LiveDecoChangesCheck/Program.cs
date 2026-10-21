using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StutterFix;
using U = UnityEngine;

try
{
NoticeAdapter.Check();

int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
(MethodInfo Wrapper, int Id) Rewrite(MethodInfo target)
{
    var instruction = new CodeInstruction(target.IsStatic ? OpCodes.Call : OpCodes.Callvirt, target);
    instruction.labels.Add(42);
    instruction.blocks.Add(new() { blockType = ExceptionBlockType.BeginExceptionBlock });
    instruction.blocks.Add(new() { blockType = ExceptionBlockType.EndExceptionBlock });
    var output = LiveDecoChanges.Instrument(new[] { instruction }).ToArray();
    Check(output.Length == 2 && ReferenceEquals(output[0], instruction), "one writer rewritten, labels preserved on original instruction");
    Check(output[0].labels.SequenceEqual(new[] { 42 }) && output[0].opcode == OpCodes.Ldc_I4, "argument stack extended by binding id only");
    Check(output[0].blocks.Single().blockType == ExceptionBlockType.BeginExceptionBlock, "exception start remains before call");
    Check(output[1].blocks.Single().blockType == ExceptionBlockType.EndExceptionBlock, "exception end remains after call");
    return ((MethodInfo)output[1].operand, (int)output[0].operand);
}
void Write(object obj, string method, object[] args, int expected)
{
    var target = obj.GetType().GetMethod(method);
    var rewritten = Rewrite(target);
    var invokeArgs = new[] { obj }.Concat(args).Append((object)rewritten.Id).ToArray();
    int b = LiveDecoRenderer.Before, a = LiveDecoRenderer.After;
    rewritten.Wrapper.Invoke(null, invokeArgs);
    Check(((U.Object)obj).Writes == expected, "receiver and all argument values preserved");
    Check(LiveDecoRenderer.Before == b + 1 && LiveDecoRenderer.After == a + 1, "exactly one notification around writer");
    Check(ReferenceEquals(LiveDecoRenderer.LastBefore, obj) && ReferenceEquals(LiveDecoRenderer.LastAfter, obj), "notification receiver identity");
}
Write(new U.Material(), "SetZero", Array.Empty<object>(), 1);
Write(new U.Renderer(), "set_enabled", new object[] { true }, 1);
Write(new U.Transform(), "set_localPosition", new object[] { 3f }, 3);
Write(new U.Renderer(), "SetPropertyBlock", new object[] { "abc", 4 }, 7);
Write(new U.Material(), "SetFloat", new object[] { "abcd", 4f }, 8);
Write(new U.Material(), "SetThree", new object[] { 4, 3f, "ab" }, 9);
Write(new U.Material(), "SetFour", new object[] { 4, 3f, "ab", true }, 10);
Write(new U.GameObject(), "SetActive", new object[] { false }, 1);
Write(new U.Behaviour(), "set_enabled", new object[] { true }, 1);
Check(LiveDecoChanges.Kind(typeof(U.GameObject).GetMethod("SetActive")) == LiveDecoChanges.State, "visibility edge preserves static admission cache");
Check(LiveDecoChanges.Kind(typeof(U.Transform).GetMethod("set_localPosition")) == LiveDecoChanges.Pose, "pose change classification");
Check(LiveDecoChanges.Kind(typeof(U.Material).GetMethod("SetFloat")) == LiveDecoChanges.Shape, "material write invalidates static classification");
Check(LiveDecoChanges.Kind(typeof(U.Behaviour).GetMethod("set_enabled")) == LiveDecoChanges.Script, "new child-script activation invalidates classification");
Check(LiveDecoChanges.Kind(typeof(U.Material).GetMethod("GetFloat")) == -1, "reader remains unchanged");
var read = new CodeInstruction(OpCodes.Callvirt, typeof(U.Material).GetMethod("GetFloat"));
Check(ReferenceEquals(LiveDecoChanges.Instrument(new[] { read }).Single(), read), "reader instruction untouched");
foreach (int count in new[] { 1, 2, 3, 4 })
{
    var method = typeof(U.Object).GetMethods().Single(m => m.Name == "Instantiate" && !m.IsGenericMethod && m.GetParameters().Length == count);
    var rewritten = Rewrite(method); var original = new U.GameObject { Writes = 10 };
    object[] values = { original, 2, "abc", 4f };
    var result = (U.Object)rewritten.Wrapper.Invoke(null, values.Take(count).Append((object)rewritten.Id).ToArray());
    Check(original.Writes == 10 && !ReferenceEquals(original, result), "spawn result/argument identity preserved");
    Check(result.Writes == (count == 1 ? 11 : count == 2 ? 12 : count == 3 ? 15 : 19), "spawn arguments preserved");
    Check(ReferenceEquals(LiveDecoRenderer.LastBefore, original) && ReferenceEquals(LiveDecoRenderer.LastAfter, result), "spawn observes new subtree");
}
var generic = typeof(U.Object).GetMethods().Single(m => m.Name == "Instantiate" && m.IsGenericMethod).MakeGenericMethod(typeof(U.GameObject));
var closed = Rewrite(generic); var prefab = new U.GameObject();
Check(closed.Wrapper.Invoke(null, new object[] { prefab, closed.Id }) is U.GameObject, "closed generic spawn keeps exact return type");
foreach (var method in typeof(U.GameObject).GetMethods().Where(m => m.Name == "AddComponent"))
{
    var target = method.IsGenericMethod ? method.MakeGenericMethod(typeof(U.Renderer)) : method;
    var rewritten = Rewrite(target);
    object[] values = method.IsGenericMethod ? new object[] { prefab, rewritten.Id } : new object[] { prefab, typeof(U.Renderer), rewritten.Id };
    var added = rewritten.Wrapper.Invoke(null, values);
    Check(added is U.Renderer && ReferenceEquals(LiveDecoRenderer.LastAfter, added), "component creation observes new subtree and retains return value");
}
foreach (string name in new[] { "Destroy", "DestroyImmediate" })
{
    var rewritten = Rewrite(typeof(U.Object).GetMethod(name)); var obj = new U.GameObject();
    rewritten.Wrapper.Invoke(null, name == "Destroy" ? new object[] { obj, rewritten.Id } : new object[] { obj, true, rewritten.Id });
    Check(obj.Writes == (name == "Destroy" ? -1 : -3), "destruction notification preserves call");
}
var throwing = Rewrite(typeof(U.Material).GetMethod("SetThrow")); var mat = new U.Material();
int after = LiveDecoRenderer.After;
try { throwing.Wrapper.Invoke(null, new object[] { mat, 17, throwing.Id }); throw new Exception("missing writer failure"); }
catch (TargetInvocationException ex) { Check(ex.InnerException is InvalidOperationException && mat.Writes == 17, "original exception propagated after mutation"); }
Check(LiveDecoRenderer.After == after, "failed writer relies on pre-write dirty notification");
var hot = Rewrite(typeof(U.Renderer).GetMethod("set_enabled"));
var hotCall = (Action<U.Renderer, bool, int>)hot.Wrapper.CreateDelegate(typeof(Action<U.Renderer, bool, int>));
var renderer = new U.Renderer();
for (int n = 0; n < 20000; n++) hotCall(renderer, true, hot.Id);
long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
for (int n = 0; n < 10000; n++) hotCall(renderer, true, hot.Id);
long wrapperBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
Check(wrapperBytes == 0, "typed writer wrapper allocation bytes=" + wrapperBytes);
LiveDecoChanges.ResetCounts();
int beforeNoop = LiveDecoRenderer.Before, afterNoop = LiveDecoRenderer.After, writesNoop = renderer.Writes;
for (int n = 0; n < 10000; n++) hotCall(renderer, true, hot.Id);
Check(renderer.Writes == writesNoop + 10000, "unchanged setter still executes every original call");
Check(LiveDecoRenderer.Before == beforeNoop && LiveDecoRenderer.After == afterNoop, "unchanged native state does not enqueue notification work");
Check(LiveDecoChanges.EqualityReads == 10000 && LiveDecoChanges.UnchangedWrites == 10000, "no-op counters are exact");
hotCall(renderer, false, hot.Id);
Check(!renderer.enabled && LiveDecoRenderer.Before == beforeNoop + 1 && LiveDecoRenderer.After == afterNoop + 1, "real visibility edge still notifies both sides");
var pose = Rewrite(typeof(U.Transform).GetMethod("set_localPosition"));
var poseCall = (Action<U.Transform, float, int>)pose.Wrapper.CreateDelegate(typeof(Action<U.Transform, float, int>));
var transform = new U.Transform();
int poseBefore = LiveDecoRenderer.Before;
poseCall(transform, 0f, pose.Id);
Check(LiveDecoRenderer.Before == poseBefore, "unchanged unowned pose avoids dirty queue");
LiveDecoRenderer.OwnedWatcher = true;
poseCall(transform, 0f, pose.Id);
Check(LiveDecoRenderer.Before == poseBefore + 1 && LiveDecoChanges.ShadowedPoseWrites == 1, "stale owned pose retains release-before-write notification");
LiveDecoRenderer.OwnedWatcher = false;
poseCall(transform, 1e-7f, pose.Id);
Check(LiveDecoRenderer.Before == poseBefore + 2 && transform.localPosition == 1e-7f, "sub-epsilon pose changes notify using exact equality");
transform.GetterFails = true;
poseCall(transform, 2f, pose.Id);
Check(LiveDecoRenderer.Before == poseBefore + 3 && transform.Writes == 2, "getter failure falls back to original writer and notification");
var vectorPose = Rewrite(typeof(U.Transform).GetMethod("set_position"));
var vectorCall = (Action<U.Transform, (float, float, float), int>)vectorPose.Wrapper.CreateDelegate(typeof(Action<U.Transform, (float, float, float), int>));
int vectorBefore = LiveDecoRenderer.Before;
vectorCall(transform, (0f, 0f, 0f), vectorPose.Id);
vectorCall(transform, (1e-7f, 0f, 0f), vectorPose.Id);
Check(LiveDecoRenderer.Before == vectorBefore + 1, "struct-valued pose uses exact component equality");
var offBit = Rewrite(typeof(U.Renderer).GetMethod("set_forceRenderingOff"));
var offCall = (Action<U.Renderer, bool, int>)offBit.Wrapper.CreateDelegate(typeof(Action<U.Renderer, bool, int>));
int forceBefore = LiveDecoRenderer.Before;
renderer.forceRenderingOff = true;
offCall(renderer, true, offBit.Id);
Check(LiveDecoRenderer.Before == forceBefore + 1, "foreign same force-off value remains an ownership boundary");
LiveDecoRenderer.ObservingWrites = false;
long reads = LiveDecoChanges.EqualityReads;
hotCall(renderer, false, hot.Id);
Check(LiveDecoChanges.EqualityReads == reads, "restore/inactive path performs no equality getter");
LiveDecoRenderer.ObservingWrites = true;
LiveDecoChanges.ResetCounts();
Check(LiveDecoChanges.EqualityReads == 0 && LiveDecoChanges.UnchangedWrites == 0 && LiveDecoChanges.ShadowedPoseWrites == 0, "diagnostic reset clears all interval counters");
Console.WriteLine($"Production write-site instrumentation: {checks} checks; typed arities 0..4, spawn/destroy, labels, EH, exceptions, 10000 zero-allocation calls; no Unity/Harmony installation proof");
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
return 0;
