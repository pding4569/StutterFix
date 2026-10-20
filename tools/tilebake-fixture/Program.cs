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
Require(Compile(Tile(), Event(0, 1, 2, 3)).Supported == 0, "scale vector startup explicitly excluded");
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
