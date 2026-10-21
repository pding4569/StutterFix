using System.Text.Json;
using StutterFix;
using UnityEngine;

try
{
    int checks = 0;
    var logs = Main.Entry.Logger.Lines;
    void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    void Reset(double song = 258)
    {
        LiveDecoGate2.Step("reset"); logs.Clear();
        scrConductor.instance = new() { songposition_minusi = song };
        FrameCompare.Paused = false; Hitch.Playing = true;
        Time.realtimeSinceStartup = 0; Time.frameCount = 0; AutoTest.Stopped = null;
    }
    void Advance(double seconds)
    {
        Time.realtimeSinceStartup += (float)seconds; Time.frameCount++;
        if (!FrameCompare.Paused) scrConductor.instance.songposition_minusi += seconds;
    }
    void BeginRenderer()
    {
        LiveDecoRenderer.Active = true; LiveDecoRenderer.Ready = false;
        LiveDecoGate2.RendererStarted();
        Advance(14.5198183); // Existing run3 setup: actual audio keeps moving unless paused.
    }
    void FirstCull()
    {
        Advance(.0805116); // Existing run3 first-cull admission, outside warmup.
        LiveDecoRenderer.Ready = true; LiveDecoRenderer.Owned = 2025;
        LiveDecoRenderer.TransitionMs = 14600.3299;
        LiveDecoRenderer.enabledAt = Time.realtimeSinceStartup;
    }
    void Reject(string command, string detail)
    {
        try { LiveDecoGate2.Step(command); throw new Exception("accepted invalid " + command); }
        catch (InvalidOperationException ex)
        {
            Check(ex.Message.Contains(detail), "diagnostic: " + detail);
            Check(logs.Last().Contains("ERROR System.InvalidOperationException:") && logs.Last().Contains("StepCore"),
                "full production exception and stack logged");
        }
    }

    Reset(); FrameCompare.Paused = true; BeginRenderer();
    Check(!LiveDecoGate2.Step("ready on"), "pending first cull cannot report ready");
    Check(logs.Count == 0, "poll does not log per frame");
    FirstCull(); Check(LiveDecoGate2.Step("ready on"), "first-cull completion accepted while paused");
    Check(scrConductor.instance.songposition_minusi == 258, "14.6 second preparation did not consume song lead-in");
    Check(logs.Last().Contains("transition_ms=14600.329900"), "transition reported separately");
    FrameCompare.Paused = false; Advance(4);
    Check(LiveDecoGate2.Step("begin on 1"), "262-second start after four actual playing seconds");
    Advance(14); Check(LiveDecoGate2.Step("end"), "unpaused 262-to-276 window completed");

    Reset(252); BeginRenderer(); FirstCull(); Advance(.15);
    Reject("begin on 1", "song=266.");
    Check(logs.Last().Contains("warmup_s=0.150"), "legacy start also lacks three-second warmup");
    // A failed begin does not leave window=1; later reset/abort and lifecycle checks remain coherent.
    scrConductor.instance.songposition_minusi = 262; Advance(3);
    scrConductor.instance.songposition_minusi = 262;
    Check(LiveDecoGate2.Step("begin on 1"), "rejected begin did not leave a phantom open window");

    Reset(); Reject("ready off", "requires paused");
    FrameCompare.Paused = true;
    Check(LiveDecoGate2.Step("ready off"), "off uses the same paused preparation");
    Reject("ready on", "matching renderer");
    LiveDecoRenderer.Active = true; LiveDecoRenderer.Ready = true;
    Reject("ready on", "owned=0");
    LiveDecoRenderer.Owned = 2025;
    foreach (double bad in new[] { 260d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
    { scrConductor.instance.songposition_minusi = bad; Reject("ready on", "invalid ABBA preparation"); }
    Reset(262); BeginRenderer(); FirstCull(); scrConductor.instance.songposition_minusi = 262;
    Reject("begin on 1", "warmup_s=0.000");
    Advance(3); scrConductor.instance.songposition_minusi = 262;
    FrameCompare.Paused = true; Reject("begin on 1", "paused=True"); FrameCompare.Paused = false;
    LiveDecoRenderer.Ready = false; Reject("begin on 1", "ready=False"); LiveDecoRenderer.Ready = true;
    foreach (double bad in new[] { 263d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
    { scrConductor.instance.songposition_minusi = bad; Reject("begin on 1", "invalid ABBA window start"); }

    // Consume the actual generator output: four equally prepared ABBA windows,
    // delayed first cull, and long setup. Real Unity/audio/Harmony are adapted.
    if (args.Length != 1) throw new ArgumentException("generated steps JSON required");
    var steps = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement.GetProperty("steps")
        .EnumerateArray().Select(x => x.GetString()).ToArray();
    int first = Array.IndexOf(steps, "livedeco2 begin off 0");
    int start = Array.FindLastIndex(steps, first, x => x == "stop") - 1;
    Reset();
    for (int i = start; i < steps.Length && steps[i] != "livedeco2 result"; i++)
    {
        string s = steps[i];
        if (s == "stop") scrConductor.instance.songposition_minusi = 0;
        else if (s.StartsWith("seek ")) scrConductor.instance.songposition_minusi = 250;
        else if (s == "pause") FrameCompare.Paused = true;
        else if (s == "resume") FrameCompare.Paused = false;
        else if (s == "livedeco off") LiveDeco.Stop();
        else if (s == "livedeco on") { BeginRenderer(); }
        else if (s.StartsWith("waitsongpos "))
        {
            double target = double.Parse(s.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
            Advance(Math.Max(0, target - scrConductor.instance.songposition_minusi));
        }
        else if (s.StartsWith("livedeco2 "))
        {
            bool done = LiveDecoGate2.Step(s.Substring(10));
            if (!done)
            { Check(s == "livedeco2 ready on", "only pending preparation may poll"); FirstCull(); done = LiveDecoGate2.Step(s.Substring(10)); }
            Check(done, "generated command completes: " + s);
        }
        Advance(.008);
        // Unity also culls after an unpaused legacy "on" command. Do not make
        // the negative control fail merely because it has no ready command.
        if (LiveDecoRenderer.Active && !LiveDecoRenderer.Ready) FirstCull();
    }
    Check(logs.Count(x => x.Contains("window begin")) == 4, "all four actual generated starts accepted");
    Check(logs.Count(x => x.Contains("FPS mode=") && x.Contains("valid=True")) == 4, "all ABBA windows unpaused and valid");
    Check(logs.Count(x => x.Contains("prepared mode=")) == 4, "same preparation protocol in both modes");
    Check(!LiveDecoRenderer.Active && !FrameCompare.Paused, "generated ABBA ends restored and resumed");
    LiveDecoGate2.Step("result");
    Check(logs.Last().Contains("GATE2=ERROR") && logs.Last().Contains("missing_pixel_sample"), "missing real pixels cannot become PASS");
    Reset(); LiveDecoGate2.OwnedLow(799, 1000);
    Check(AutoTest.Stopped == "GATE2=OWNED_LOW" && logs.Any(x => x.Contains("GATE2=OWNED_LOW")), "early low ownership still stops");
    Console.WriteLine($"Production Gate2 window regression: {checks} checks passed; legacy long setup rejected, generated paused preparation accepted; no Unity performance proven");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
