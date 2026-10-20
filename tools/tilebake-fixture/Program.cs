using System.Diagnostics;
using System.Globalization;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using StutterFix;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
static void Require(bool ok, string why) { if (!ok) throw new Exception(why); Console.WriteLine("PASS " + why); }
static float EaseValue(int id, float t, float d, float o, float p) => EaseManager.Evaluate((Ease)id, null, t, d, o, p);
static FloorBakeCore.Tile Tile(float initial = 0, float origin = 0) => new() { X = origin, Initial = new[] { initial, 0f, 0f, 1f, 1f, 1f } };
static FloorBakeCore.Event Event(double t, float d, float value, int c = 0, Ease ease = Ease.Linear, int id = 1)
{
    var e = new FloorBakeCore.Event { T0 = t, Duration = d, Targets = new[] { 0 }, Source = id, Ease = (int)ease,
        Over = ease is Ease.Flash or Ease.InFlash or Ease.OutFlash or Ease.InOutFlash ? 1 : 1.70158f };
    e.Values[c] = value; return e;
}
static FloorBakeCore.Table Compile(FloorBakeCore.Tile tile, params FloorBakeCore.Event[] events)
    => FloorBakeCore.Compile(new[] { tile }, events, 1, 1, EaseValue, CancellationToken.None);
var replacement = Compile(Tile(), Event(0, 10, 10), Event(4, 2, 6, id: 2));
Require(replacement.Eval(0, 0, 3) == 3 && replacement.Eval(0, 0, 4) == 10 && replacement.Eval(0, 0, 5) == 8, "Kill(true) -> previous endpoint -> new start");
Require(replacement.Tracks[0][1].Replacement && replacement.Tracks[0][1].Source == 2, "replacement/source instance retained");
var relative = Compile(Tile(100, 100), Event(1, 2, 2), Event(4, 2, 3));
Require(relative.Eval(0, 0, 3) == 102 && relative.Eval(0, 0, 6) == 103, "offset relative to startPos, not current or previous endpoint");
Require(relative.Eval(0, 0, 2) == 101 && relative.Eval(0, 0, 8) == 103 && relative.Eval(0, 0, 2) == 101, "arbitrary seek/rewind pure evaluation");
var zero = Event(2, 0, 7); zero.ZeroK = .25f;
var instant = Compile(Tile(), zero, Event(2, 0, 9, id: 2));
Require(instant.Eval(0, 0, 1.99) == 0 && instant.Eval(0, 0, 2) == 9, "zero duration + equal T0 order");
Require(Compile(Tile(), zero).Eval(0, 0, 2) == 1.75f, "transform zero uses easing endpoint");
var opa = Event(2, 0, .3f, 5); opa.ZeroK = .25f;
var opacity = Compile(Tile(), opa);
Require(opacity.Eval(0, 5, 2) == .3f && opacity.Eval(0, 0, 9) == 0 && opacity.Eval(0, 3, 9) == 1, "OpaOnly/zero opacity direct target + unused NaN axes");
var near = Compile(Tile(1), Event(0, 2, 1.0000001f));
Require(near.Eval(0, 0, 1) == 1, "Approximately skips creation after Kill");
var exclusion = Event(4, 2, 4); exclusion.Reasons = new[] { "other-writer" };
var excluded = Compile(Tile(), Event(0, 2, 2), exclusion);
Require(excluded.Supported == 0 && excluded.Segments == 0 && excluded.Tiles[0].Reasons.Contains("other-writer"), "late unsupported writer excludes whole tile");
Require(Compile(Tile(), Event(0, 1, 2, 3)).Supported == 1, "scale axis supported");
Require(Compile(Tile(), Event(0, 1, 400, 2)).Supported == 0, "Euler wrap explicitly excluded");
Require(Compile(Tile(), Event(0, -1, 1)).Supported == 0 && Compile(Tile(), Event(0, 1, float.PositiveInfinity)).Supported == 0, "invalid duration/target excluded");
int samples = 0;
foreach (Ease ease in Enum.GetValues<Ease>()) {
    if (ease is Ease.Unset or Ease.INTERNAL_Custom or Ease.INTERNAL_Zero) continue;
    var e = Event(2, 4, 100, ease: ease); var t = Compile(Tile(), e);
    for (int i = 1; i < 100; i++) {
        float time = i / 25f;
        float expected = 100 * EaseValue((int)ease, time, 4, e.Over, 0);
        if (BitConverter.SingleToInt32Bits(t.Eval(0, 0, 2 + (double)time)) != BitConverter.SingleToInt32Bits(expected)) throw new Exception("ease " + ease);
        samples++;
    }
}
Require(samples == 3465, "DOTween intermediate easing values=" + samples);
var sx = Event(0, 10, 3, 3); sx.Values[4] = 5;
var sy = Event(4, 2, 7, 4, id: 2);
var scaleReplace = Compile(Tile(), sx, sy);
Require(scaleReplace.Eval(0, 3, 5) == 2 && scaleReplace.Eval(0, 4, 4) == 5 && scaleReplace.Eval(0, 4, 5) == 6,
    "scale Kill(true), NaN X leaves old X running");
Require(scaleReplace.Eval(0, 4, 2) == 1.8f && scaleReplace.Eval(0, 4, 8) == 7 && scaleReplace.Eval(0, 4, 2) == 1.8f,
    "scale seek/rewind has no state");
var replaceX = Compile(Tile(), sx, Event(4, 2, 7, 3, id: 3));
Require(replaceX.Eval(0, 3, 4) == 3 && replaceX.Eval(0, 3, 5) == 5 && replaceX.Eval(0, 4, 5) == 3,
    "scale X replacement completes X only; NaN Y keeps its own track");
var sameScaleTime = Compile(Tile(), Event(0, 10, 3, 3), Event(0, 2, 7, 3, id: 2));
Require(sameScaleTime.Eval(0, 3, 0) == 3 && sameScaleTime.Eval(0, 3, 1) == 5,
    "same T0 scale replacement completes even before first Step");
var sz = Event(2, 0, 9, 3); sz.Values[4] = 5; sz.ZeroK = .25f;
Require(Compile(Tile(), sz).Eval(0, 3, 2) == 3 && Compile(Tile(), sz).Eval(0, 4, 2) == 2,
    "zero scale uses per-axis getter/ease endpoint");
var tinyY = Event(2, 0, 2, 3); tinyY.Values[4] = 1.0000005f;
Require(Compile(Tile(), tinyY).Eval(0, 4, 2) == tinyY.Values[4],
    "zero X write forces near-Y tween via cached XY comparison");
tinyY.Duration = 2;
Require(Compile(Tile(), tinyY).Eval(0, 4, 4) == 1,
    "deferred X does not immediately write: near-Y remains skipped");
var nearAfterKill = Event(2, 2, 2, 3); nearAfterKill.Values[4] = 5.000001f;
var ambiguous = Compile(Tile(), sx, nearAfterKill);
Require(ambiguous.Supported == 0 && ambiguous.Segments == 0
    && ambiguous.Tiles[0].Reasons.Contains("scale-cross-axis-frame-dependent-skip"),
    "frame-dependent cross-axis near skip excludes whole tile including previous tracks");
var exactY = Event(2, 2, 1, 3); exactY.Values[4] = 5;
Require(Compile(Tile(), sx, exactY).Supported == 1, "exact equal Y after completion has no value ambiguity");
var bothNaN = Event(1, 3, float.NaN, 3);
Require(Compile(Tile(), bothNaN).Segments == 0, "both scale NaN: no tracks");
var hugeScale = Tile(); hugeScale.Initial[3] = -float.MaxValue;
Require(Compile(hugeScale, Event(0, 1, float.MaxValue, 3)).Tiles[0].Reasons.Contains("scale-arithmetic-nonfinite"),
    "scale change overflow explicitly excludes tile");
var returnBase = Event(531.8, 1, 0); returnBase.Values = new[] { 0f, 0f, 0f, 1f, 1f, 1f };
var large = Tile(); large.Initial = new[] { 2f, 4f, 90f, 2f, 3f, .2f };
var reset = Compile(large, returnBase);
Require(reset.Supported == 1 && reset.Eval(0, 3, 532.3) == 1.5f && reset.Eval(0, 4, 532.8) == 1
    && reset.Eval(0, 0, 532.8) == 0 && reset.Eval(0, 5, 532.8) == 1,
    "531.8s full baseline reset shape included (synthetic)");
var tiles8171 = Enumerable.Range(0, 8171).Select(i => {
    var row = Tile(); row.Id = i; row.Initial = new[] { 2f, 4f, 90f, 2f, 3f, .2f }; return row;
}).ToArray();
returnBase.Targets = Enumerable.Range(0, 8171).ToArray();
var reset8171 = FloorBakeCore.Compile(tiles8171, new[] { returnBase }, 1, 1, EaseValue, CancellationToken.None);
Require(reset8171.Supported == 8171 && reset8171.Segments == 8171 * 6
    && Enumerable.Range(0, 8171).All(i => reset8171.Eval(i, 3, 532.3) == 1.5f && reset8171.Eval(i, 4, 532.8) == 1),
    "8171-tile baseline return compiles all six channels (synthetic, not actual map coverage)");
int scaleSamples = 0;
foreach (Ease ease in Enum.GetValues<Ease>()) {
    if (ease is Ease.Unset or Ease.INTERNAL_Custom or Ease.INTERNAL_Zero) continue;
    var ev = Event(2, 4, 3.75f, 3, ease); ev.Values[4] = -.25f;
    var tile = Tile(); tile.Initial[3] = 1.25f; tile.Initial[4] = 2.5f;
    var baked = Compile(tile, ev);
    for (int i = 1; i < 100; i++) for (int axis = 3; axis <= 4; axis++) {
        float time = i / 25f;
        // Independent Vector3Plugin axis formula with real DOTween easing.
        float expected = tile.Initial[axis] + (ev.Values[axis] - tile.Initial[axis]) * EaseValue((int)ease, time, 4, ev.Over, 0);
        if (BitConverter.SingleToInt32Bits(baked.Eval(0, axis, 2 + (double)time)) != BitConverter.SingleToInt32Bits(expected))
            throw new Exception("scale ease " + ease + " axis=" + axis);
        scaleSamples++;
    }
}
Require(scaleSamples == 6930, "scale DOTween intermediate axis values=" + scaleSamples);
bool sorted = false, cancelled = false;
try { Compile(Tile(), Event(2, 1, 2), Event(1, 1, 2)); } catch (ArgumentException) { sorted = true; }
try { FloorBakeCore.Compile(new[] { Tile() }, new[] { Event(0, 1, 2) }, 1, 1, EaseValue, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
Require(sorted && cancelled, "invalid order rejected / cancellation");
var clock = FloorBakeCore.Compile(new[] { Tile() }, new[] { Event(1, 2, 10) }, 2, .5f, EaseValue, CancellationToken.None);
Require(clock.Eval(0, 0, 5) == 5, "songTime / pitch * timeScale contract");

if (args.Contains("--bench")) foreach (int count in new[] { 8171, 90000 }) {
    GC.Collect(); long before = GC.GetTotalMemory(true); var sw = Stopwatch.StartNew();
    var t = FloorBakeCore.Synthetic(count, EaseValue);
    double total = sw.Elapsed.TotalMilliseconds; long delta = GC.GetTotalMemory(false) - before;
    var times = new List<double>(); double sum = 0;
    for (int pass = 0; pass < 33; pass++) {
        sw.Restart(); for (int d = 0; d < count; d++) for (int c = 0; c < 6; c++) sum += t.Eval(d, c, pass * 17.125);
        if (pass != 0) times.Add(sw.Elapsed.TotalMilliseconds);
    }
    times.Sort();
    Console.WriteLine($"SYNTHETIC .NET8 tiles={count} events={t.Events} refs={count+4096*32} segments={t.Segments} bakeMs={t.CompileMs:F3} inputAndBakeMs={total:F3} payloadB={t.PayloadBytes} heapDeltaB={delta} evalMedianMs={times[16]:F3} evalP99Ms={times[31]:F3} checksum={sum:R}");
}
Console.WriteLine("No game launch/install; synthetic evidence is not Lost Requiem measurement.");
