using StutterFix;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
// Playback frozen by framecmp makes the game's editor flag true. Only that
// provenance can bypass the global editor exclusion; actual readers still win.
for (int bits = 0; bits < 32; bits++)
{
    bool editing = (bits & 1) != 0, frozen = (bits & 2) != 0, selection = (bits & 4) != 0,
        hitbox = (bits & 8) != 0, selectedOrGizmo = (bits & 16) != 0;
    // The only allowed states are ordinary playback (0), non-editor frozen
    // playback (2), and editor frozen playback (3), all without actual readers.
    bool allowed = bits == 0 || bits == 2 || bits == 3;
    Check(LiveDecoPolicy.EditorTransformReader(editing, frozen, selection, hitbox, selectedOrGizmo) != allowed, "editor reader admission " + bits);
}
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
        random.Next(4) != 0, "base", random.Next(2))).OrderBy(r => r.Key).ToArray();
    var result = LiveDecoPolicy.Count(original);
    var reconstructed = new List<(SceneRenderOrder.Key Key, int[] Members)>();
    for (int i = 0; i < count; i++)
        if (!original[i].Owned) reconstructed.Add((original[i].Key, new[] { i }));
    for (int r = 0; r < result.MeshRuns; r++)
    {
        int start = result.First[r], length = result.Counts[r];
        reconstructed.Add((original[start].Key, Enumerable.Range(start, length).ToArray()));
        Check(original.Skip(start).Take(length).All(x => x.Owned && x.Key.SameBucket(original[start].Key)), "anchor bucket invariant");
    }
    var expanded = reconstructed.OrderBy(x => x.Key).SelectMany(x => x.Members).ToArray();
    Check(expanded.SequenceEqual(Enumerable.Range(0, count)), "borrowed first-member anchor preserves external order");
    Check(result.Counts.Sum() == original.Count(x => x.Owned), "partition exhaustive");
}
Console.WriteLine($"LiveDeco policy/anchor model: {checks} checks passed; synthetic, no Unity sorting or pixels proven");
int atlasChecks = checks;
foreach (int w in new[] { 1, 2, 3, 4, 5, 31, 32, 63 }) foreach (int h in new[] { 1, 2, 5, 16, 31 })
{
    var src = Enumerable.Range(0, w * h).ToArray(); var padded = LiveDecoAtlasPolicy.Extrude(src, w, h);
    int stride = LiveDecoAtlasPolicy.Align(w + 4);
    for (int y = 0; y < LiveDecoAtlasPolicy.Align(h + 4); y++) for (int x = 0; x < stride; x++)
        Check(padded[y * stride + x] == src[Math.Clamp(y - 2, 0, h - 1) * w + Math.Clamp(x - 2, 0, w - 1)], "true edge extrusion including corners");
}
for (int trial = 0; trial < 100; trial++)
{
    const int size = 128; var shelf = new LiveDecoAtlasPolicy.Shelf(size); var occupied = new HashSet<(int, int)>();
    for (int t = 0; t < 50; t++)
    {
        int w = random.Next(1, 34), h = random.Next(1, 34);
        if (!shelf.Place(w, h, out int x, out int y)) continue;
        Check(x % 2 == 0 && y % 2 == 0 && (x - 2) % 4 == 0 && (y - 2) % 4 == 0, "even origin and block alignment");
        Check(x >= 2 && y >= 2 && x + w + 2 <= size && y + h + 2 <= size, "padding in bounds");
        for (int py = y - 2; py < y - 2 + LiveDecoAtlasPolicy.Align(h + 4); py++)
            for (int px = x - 2; px < x - 2 + LiveDecoAtlasPolicy.Align(w + 4); px++) Check(occupied.Add((px, py)), "padded regions do not overlap");
        foreach (float uv in new[] { 0f, 0.1234567f, 0.5f, 1f })
            Check(LiveDecoAtlasPolicy.UV(x, w, size, uv) == (float)((double)x / size + (double)uv * ((double)(x + w) / size - (double)x / size)), "single double-to-float UV conversion");
    }
}
Check(LiveDecoAtlasPolicy.Verdict(0, true, true, true, 100, 115) == "PASS", "exact 15 percent boundary");
Check(LiveDecoAtlasPolicy.Verdict(0, true, true, true, 100, 114.99) == "NO_GAIN", "gain below threshold");
Check(LiveDecoAtlasPolicy.Verdict(1, true, true, true, 100, 150) == "PIXEL_FAIL", "one pixel never passes");
Check(LiveDecoAtlasPolicy.Verdict(0, false, true, true, 100, 150) == "ERROR", "restore required");
Check(LiveDecoAtlasPolicy.Verdict(0, true, false, true, 100, 150) == "ERROR", "three valid pixels required");
Check(LiveDecoAtlasPolicy.Verdict(0, true, true, false, 100, 150) == "ERROR", "four valid samples required");
Console.WriteLine($"LiveDeco atlas/gate policy: {checks - atlasChecks} checks passed; packing, extrusion, rounding, verdict boundaries; GPU untested");
if (args.Length == 3)
{
    int start = checks;
    for (int i = 0; i < 3; i++)
    {
        using var stream = File.OpenRead(args[i]); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
        var names = md.TypeDefinitions.Select(h => md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
        foreach (string name in new[] { "LiveDeco", "LiveDecoPolicy", "SceneRenderOrder", "LiveDecoAtlas", "LiveDecoAtlasPolicy", "LiveDecoPatchPlan" })
            Check(names.Contains(name) == (i != 0), "edition types " + name + " " + args[i]);
        if (i == 0) Check(!md.TypeReferences.Any(h => md.GetString(md.GetTypeReference(h).Name).StartsWith("LiveDeco")
            || md.GetString(md.GetTypeReference(h).Name) == "SceneRenderOrder"), "Player no experiment type references");
    }
    Console.WriteLine($"LiveDeco edition metadata: {checks - start} checks passed; Player absent, DEV/PlayerAuto present");
}
