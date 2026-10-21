using StutterFix;
int checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition) { Console.Error.WriteLine("FAIL " + label); Environment.Exit(1); }
}
void Frame(scrDecorationManager manager) { Dormancy.NewFrame(); Dormancy.List(manager); }
void Denied(scrDecorationManager manager, string reason)
{
    try { Dormancy.PrepareLiveTracking(manager); throw new Exception("unexpected admission"); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains(reason), "diagnostic " + reason); }
    Check(!Dormancy.LiveTrackingReady, "tracking refused");
}
var manager = new scrDecorationManager();
var hidden = new scrVisualDecoration(); var visible = new scrVisualDecoration { Visible = true };
manager.allDecorations.AddRange(new[] { hidden, visible });
Dormancy.Install(new HarmonyLib.Harmony());
Hitch.Playing = true; Frame(manager);
Check(Dormancy.LiveTrackingReady, "playing initialized");
Dormancy.Sleep(hidden); Frame(manager);
Check(Dormancy.AwakeList.SequenceEqual(new[] { visible }), "sleep removes hidden member");
int rebuilds = LiveDecoRenderer.Rebuilds, resets = LiveDecoRenderer.Resets;
Hitch.Playing = false; FrameCompare.Paused = true;
LiveDecoRenderer.Active = true;
for (int frame = 0; frame < 10; frame++) Frame(manager);
Check(Dormancy.LiveTrackingReady && LiveDecoRenderer.Active, "capture pause preserves live owner and readiness");
Check(LiveDecoRenderer.Resets == resets && LiveDecoRenderer.Rebuilds == rebuilds, "pause neither resets nor rebuilds");
Dormancy.PrepareLiveTracking(manager);
Check(Dormancy.AwakeList.SequenceEqual(new[] { visible }), "paused admission keeps sleep filtering");
hidden.Visible = true; Dormancy.WakePost(hidden); Frame(manager);
Check(Dormancy.AwakeList.Count == 2 && LiveDecoRenderer.Active, "wake delta during capture pause");
FrameCompare.Paused = false; Hitch.Playing = true; Frame(manager);
Check(Dormancy.LiveTrackingReady && LiveDecoRenderer.Active, "resume keeps tracking");
Hitch.Playing = false; Frame(manager);
Check(!Dormancy.LiveTrackingReady && !LiveDecoRenderer.Active, "ordinary stop resets and restores");
Check(Dormancy.AwakeList.Count == 0, "ordinary stop clears references");
// Explicit enable can occur before the first manager LateUpdate after seek/pause.
FrameCompare.Paused = true; Dormancy.PrepareLiveTracking(manager);
Check(Dormancy.LiveTrackingReady && Dormancy.AwakeList.Count == 2, "cold paused admission bootstraps current list");
var replacement = new scrDecorationManager(); replacement.allDecorations.Add(visible);
LiveDecoRenderer.Active = true; Dormancy.PrepareLiveTracking(replacement);
Check(Dormancy.AwakeList.SequenceEqual(new[] { visible }) && !LiveDecoRenderer.Active, "new scene list replaces old and restores owner");
LiveDecoRenderer.Active = true; Dormancy.ForgetLevel();
Check(!Dormancy.LiveTrackingReady && !LiveDecoRenderer.Active && Dormancy.AwakeList.Count == 0, "unload clears and restores even while paused");
Dormancy.Enabled = false; Denied(manager, "enabled=False"); Dormancy.Enabled = true;
MoveApply.LogicSkip = false; Denied(manager, "logicSkip=False"); MoveApply.LogicSkip = true;
FrameCompare.Paused = false; Denied(manager, "capturePaused=False");
FrameCompare.Paused = true;
var absent = new scrDecorationManager { allDecorations = null }; Denied(absent, "source=False");
Console.WriteLine($"Dormancy production lifecycle: {checks} checks passed; managed adapters, no live pixels/FPS/lifetime proof");
