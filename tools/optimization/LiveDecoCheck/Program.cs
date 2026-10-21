using StutterFix;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
SceneRenderOrder.Key Key(int material, int renderer, int layer = 0, int order = 0, int queue = 3000, float distance = 1)
    => new(layer, order, queue, distance, material, renderer);
LiveDecoPolicy.Item Row(int material, bool own, int renderer = 0, int order = 0, string signature = "base", int unityLayer = 0)
    => new(Key(material, renderer, order: order), own, signature, unityLayer);
// Regression: prefab meshes retain enabled=true while the game deactivates their object.
for (int bits = 0; bits < 32; bits++)
{
    bool exists = (bits & 1) != 0, scene = (bits & 2) != 0, enabled = (bits & 4) != 0,
        active = (bits & 8) != 0, forced = (bits & 16) != 0;
    Check(LiveDecoPolicy.RendererLive(exists, scene, enabled, active, forced) == (bits == 15), "mesh live state " + bits);
}
var dormantMesh = LiveDecoPolicy.RendererLive(true, true, true, false, false);
var activeMesh = LiveDecoPolicy.RendererLive(true, true, true, true, false);
var corrected = LiveDecoPolicy.Count(new[] { Row(-3, !dormantMesh), Row(-2, !activeMesh), Row(-1, !dormantMesh) });
Check(corrected.Candidates == 2 && corrected.MeshRuns == 2 && corrected.BarrierRuns == 2, "dormant mesh eligible, live mesh remains barrier");
var empty = LiveDecoPolicy.Count(Array.Empty<LiveDecoPolicy.Item>());
Check(empty.MeshRuns == 0 && LiveDecoPolicy.Gate(0, empty, true) == "INCONCLUSIVE", "empty is not success");
Check(Key(int.MinValue, 0).CompareTo(Key(int.MaxValue, 0)) < 0, "signed ID no overflow");
Check(Key(-1, 0).CompareTo(Key(1, 0)) < 0, "negative before positive");
Check(Key(1, 0, order: 0).CompareTo(Key(-100, 0, order: 1)) < 0, "order before material");
Check(Key(1, 0, layer: 0).CompareTo(Key(-100, 0, layer: 1)) < 0, "layer before material");
Check(Key(1, 0, queue: 2999).CompareTo(Key(-100, 0)) < 0, "queue before material");
Check(Key(1, 0, distance: 2).CompareTo(Key(-100, 0, distance: 1)) < 0, "distance descending before material");
foreach (var distance in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    bool rejected = false; try { Key(0, 0, distance: distance); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "nonfinite key rejected");
}
var barriers = LiveDecoPolicy.Count(new[] { Row(-9, false), Row(-8, true), Row(-7, true), Row(-6, false), Row(-5, true), Row(-4, false) });
Check(barriers.Candidates == 3 && barriers.BarrierRuns == 2 && barriers.MeshRuns == 2, "external draws split atlas runs");
var split = LiveDecoPolicy.Count(new[] { Row(-9, true), Row(-8, true, order: 1), Row(-7, true, order: 1, signature: "other"), Row(-6, true, order: 1, signature: "other", unityLayer: 1) });
Check(split.BarrierRuns == 1 && split.MeshRuns == 4 && split.KeySplits == 1 && split.SignatureSplits == 1 && split.LayerSplits == 1, "real mesh state boundaries exceed barrier lower bound");
var tied = LiveDecoPolicy.Count(new[] { Row(-1, true, 10), Row(-1, false, 20), Row(-1, true, 30) });
Check(tied.AmbiguousTies == 1 && LiveDecoPolicy.Gate(2, tied, true) == "INCONCLUSIVE", "external same-material tie cannot rely on renderer ID");
bool unsorted = false;
try { LiveDecoPolicy.Count(new[] { Row(1, true), Row(-1, true) }); } catch (ArgumentException) { unsorted = true; }
Check(unsorted, "unsorted rejected");

// Gate boundaries use integer ratios; no rounding an actual <80% into a pass.
var four = LiveDecoPolicy.Count(Enumerable.Range(0, 4).Select(i => Row(i, true)).ToArray());
Check(LiveDecoPolicy.Gate(5, four, true) == "PASS", "80 percent passes");
Check(LiveDecoPolicy.Gate(6, four, true) == "FAIL", "below 80 fails");
Check(LiveDecoPolicy.Gate(5, four, false) == "INCONCLUSIVE", "unsupported ordering never passes");
Check(LiveDecoPolicy.Gate(5, four, true, false) == "INCONCLUSIVE", "named MPB probes cannot certify numerical pass");
Check(LiveDecoPolicy.Gate(6, four, true, false) == "INCONCLUSIVE", "unverified MPB candidates do not certify numerical failure either");
foreach (int n in new[] { 200, 201 })
{
    var many = LiveDecoPolicy.Count(Enumerable.Range(0, n).Select(i => Row(i, true, order: i)).ToArray());
    Check(LiveDecoPolicy.Gate(n, many, true) == (n == 200 ? "PASS" : "FAIL"), "run threshold " + n);
}

// First-member material anchor claim, under the specified sorting rule: replace
// each contiguous same-bucket/state owned run by its first member, then expand.
// External labels must remain in precisely the same order among owned labels.
var random = new Random(20261021);
for (int trial = 0; trial < 1000; trial++)
{
    int count = random.Next(1, 80);
    var original = Enumerable.Range(0, count).Select(i => new LiveDecoPolicy.Item(
        Key(unchecked(int.MinValue + i * 7919), i, order: random.Next(4), distance: random.Next(3)),
        random.Next(4) != 0, "base", random.Next(2), random.Next(5))).OrderBy(r => r.Key).ToArray();
    foreach (bool splitTextures in new[] { false, true })
    {
    var result = LiveDecoPolicy.Count(original, splitTextures);
    var reconstructed = new List<(SceneRenderOrder.Key Key, int[] Members)>();
    for (int i = 0; i < count; i++)
        if (!original[i].Owned) reconstructed.Add((original[i].Key, new[] { i }));
    for (int r = 0; r < result.MeshRuns; r++)
    {
        int start = result.First[r], length = result.Counts[r];
        reconstructed.Add((original[start].Key, Enumerable.Range(start, length).ToArray()));
        Check(original.Skip(start).Take(length).All(x => x.Owned && x.Key.SameBucket(original[start].Key)), "anchor bucket invariant");
        Check(original.Skip(start).Take(length).All(x => LiveDecoPolicy.CanJoin(original[start], x, splitTextures)), "runtime CanJoin matches model partition");
    }
    var expanded = reconstructed.OrderBy(x => x.Key).SelectMany(x => x.Members).ToArray();
    Check(expanded.SequenceEqual(Enumerable.Range(0, count)), "borrowed first-member anchor preserves external order");
    Check(result.Counts.Sum() == original.Count(x => x.Owned), "partition exhaustive");
    }
}
Console.WriteLine($"LiveDeco policy/anchor model: {checks} checks passed; synthetic, no Unity sorting or pixels proven");
int gateStart = checks;
var textures = new[] {
    new LiveDecoPolicy.Item(Key(-4, 1), true, "base", 0, 11),
    new LiveDecoPolicy.Item(Key(-3, 2), true, "base", 0, 12),
    new LiveDecoPolicy.Item(Key(-2, 3), true, "base", 0, 12),
    new LiveDecoPolicy.Item(Key(-1, 4), true, "base", 0, 11) };
var noAtlas = LiveDecoPolicy.Count(textures, true);
Check(noAtlas.MeshRuns == 3 && noAtlas.TextureSplits == 2 && noAtlas.Counts.SequenceEqual(new[] { 1, 2, 1 }), "A splits on texture and does not merge nonadjacent images");
Check(LiveDecoPolicy.Count(textures).MeshRuns == 1, "gate1 atlas estimate preserved");
LiveDecoGate2Policy Complete(double on = 115, long changed = 0, int max = 0)
{
    var p = new LiveDecoGate2Policy();
    for (int i = 0; i < 3; i++) p.Pixel(i, changed, max, true, true);
    for (int i = 0; i < 4; i++) p.Window(i, LiveDecoGate2Policy.On(i), LiveDecoGate2Policy.On(i) ? on : 100, true);
    return p;
}
Check(Complete().Verdict(out _) == "PASS", "exact 15 percent passes");
Check(Complete(114.999999).Verdict(out _) == "NO_GAIN", "no rounded threshold pass");
Check(Complete(120, 1, 1).Verdict(out _) == "PIXEL_FAIL", "one changed byte is not PASS");
Check(Complete(120, 500, 255).Verdict(out _) == "PIXEL_FAIL", "visible differences fail");
Check(new LiveDecoGate2Policy().Verdict(out _) == "ERROR", "missing evidence is ERROR");
var bad = Complete(); bad.Pixel(0, 0, 0, true, true);
Check(bad.Verdict(out _) == "ERROR", "duplicate pixel sample rejected");
bad = new LiveDecoGate2Policy(); bad.Pixel(0, 0, 0, false, true);
Check(bad.Verdict(out var why) == "ERROR" && why == "restoration_failed", "restoration failure dominates");
bad = new LiveDecoGate2Policy(); bad.Pixel(0, 0, 0, true, false);
Check(bad.Verdict(out why) == "ERROR" && why == "unstable_baseline", "unstable baseline fails even at zero pixels");
foreach (double invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
{
    bad = new LiveDecoGate2Policy(); bad.Window(0, false, invalid, true);
    Check(bad.Verdict(out _) == "ERROR", "invalid FPS rejected");
}
bad = new LiveDecoGate2Policy(); bad.Window(1, true, 120, true);
Check(bad.Verdict(out why) == "ERROR" && why == "abba_order", "out-of-order performance rejected");
bad = new LiveDecoGate2Policy(); bad.Window(0, true, 120, true);
Check(bad.Verdict(out _) == "ERROR", "wrong ABBA mode rejected");
bad = Complete(120); bad.Window(0, false, 100, true);
Check(bad.Verdict(out _) == "ERROR", "duplicate FPS rejected");
bad = Complete(120); bad.Error = "renderer_failure";
Check(bad.Verdict(out _) == "ERROR", "runtime error cannot be hidden by good samples");
Console.WriteLine($"LiveDeco gate2 policy: {checks - gateStart} checks passed; no runtime pixels/FPS proven");
if (args.Length >= 3)
{
    int start = checks;
    for (int i = 0; i < 3; i++)
    {
        using var stream = File.OpenRead(args[i]); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
        var names = md.TypeDefinitions.Select(h => md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
        foreach (string name in new[] { "LiveDeco", "LiveDecoPolicy", "SceneRenderOrder", "LiveDecoRenderer", "LiveDecoGeometry", "LiveDecoLifetime", "LiveDecoGate2", "LiveDecoGate2Policy" })
            Check(names.Contains(name) == (i != 0), "edition types " + name + " " + args[i]);
        var decoAnim = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == "DecoAnim");
        var prof = decoAnim.GetFields().Select(md.GetFieldDefinition).Single(f => md.GetString(f.Name) == "Prof");
        Check((prof.Attributes & System.Reflection.FieldAttributes.InitOnly) != 0 == (i == 0),
            "profiling writable only in DEV/PlayerAuto " + args[i]);
        if (i == 0) Check(!md.TypeReferences.Any(h => md.GetString(md.GetTypeReference(h).Name).StartsWith("LiveDeco")
            || md.GetString(md.GetTypeReference(h).Name) == "SceneRenderOrder"), "Player no experiment type references");
    }
    Console.WriteLine($"LiveDeco edition metadata: {checks - start} checks passed; Player absent, DEV/PlayerAuto present");
}
if (args.Length == 4)
{
    int start = checks;
    using var stream = File.OpenRead(args[3]); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
    foreach (var (type, method, count) in new[] {
        ("scrDecoration", "UpdatePosition", 0), ("scrDecoration", "SetPosition", 2),
        ("scrDecoration", "SetRotation", 1), ("scrDecoration", "SetScale", 1),
        ("scrDecoration", "SetColor", 1), ("scrDecoration", "SetOpacity", 1),
        ("scrVisualDecoration", "ApplyColor", 0), ("scrParallax", "SetTrans", 0),
        ("scrDecoration", "Setup", 3), ("scrVisualDecoration", "SetSprite", 1),
        ("scrVisualDecoration", "SetSprite", 2), ("scrVisualDecoration", "SetTile", 1),
        ("scrVisualDecoration", "SetBlendMode", 1), ("scrVisualDecoration", "SetVisible", 1),
        ("scrVisualDecoration", "SetMaskingType", 1), ("scrVisualDecoration", "SetTextureScaleMultiplier", 1),
        ("scrVisualDecoration", "SetDepth", 1) })
    {
        var t = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == type);
        int matches = t.GetMethods().Select(md.GetMethodDefinition).Count(m => md.GetString(m.Name) == method
            && (m.Attributes & (System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.Abstract)) == 0
            && m.GetGenericParameters().Count == 0
            && m.GetParameters().Select(md.GetParameter).Count(p => p.SequenceNumber > 0) == count);
        Check(matches == 1, $"unique installed-game instance hook {type}.{method}/{count}: {matches}");
    }
    Console.WriteLine($"LiveDeco installed-game hook metadata: {checks - start} unique targets passed; no game methods invoked");
}
