using StutterFix;
using UnityEngine;
try
{
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
Hitch.Playing = false; FrameCompare.Pause();
LiveDecoRenderer.Active = true;
for (int frame = 0; frame < 10; frame++) Frame(manager);
Check(Dormancy.LiveTrackingReady && LiveDecoRenderer.Active, "capture pause preserves live owner and readiness");
Check(LiveDecoRenderer.Resets == resets && LiveDecoRenderer.Rebuilds == rebuilds, "pause neither resets nor rebuilds");
Dormancy.PrepareLiveTracking(manager);
Check(Dormancy.AwakeList.SequenceEqual(new[] { visible }), "paused admission keeps sleep filtering");
hidden.Visible = true; Dormancy.WakePost(hidden); Frame(manager);
Check(Dormancy.AwakeList.Count == 2 && LiveDecoRenderer.Active, "wake delta during capture pause");
// Real ordering: cached Hitch is still false in the manager LateUpdate and
// camera PreCull immediately following AutoTest.Resume in the same Update.
FrameCompare.Resume(); Frame(manager);
Check(!Hitch.Playing && !FrameCompare.Paused && Dormancy.LiveTrackingReady && LiveDecoRenderer.Active,
    "resume same-frame stale paused sample preserves owner");
Check(LiveDecoRenderer.Resets == resets && LiveDecoRenderer.Rebuilds == rebuilds,
    "resume does not rebuild or reset live set");
RendererLifetime.Check(true, SceneReset.Count);
Check(FrameCompare.PlaybackContinuity, "renderer guard accepts same-frame resume");
FrameCompare.Resume();
Check(FrameCompare.PlaybackContinuity, "duplicate resume does not erase continuity");
Time.frameCount++; Hitch.Playing = true; Frame(manager);
Check(Dormancy.LiveTrackingReady && LiveDecoRenderer.Active && !FrameCompare.PlaybackContinuity,
    "next frame uses refreshed playing cache");
Hitch.Playing = false; Frame(manager);
Check(!Dormancy.LiveTrackingReady && !LiveDecoRenderer.Active, "ordinary stop resets and restores");
Check(Dormancy.AwakeList.Count == 0, "ordinary stop clears references");
// Explicit enable can occur before the first manager LateUpdate after seek/pause.
FrameCompare.Pause(); Dormancy.PrepareLiveTracking(manager);
Check(Dormancy.LiveTrackingReady && Dormancy.AwakeList.Count == 2, "cold paused admission bootstraps current list");
var replacement = new scrDecorationManager(); replacement.allDecorations.Add(visible);
LiveDecoRenderer.Active = true; Dormancy.PrepareLiveTracking(replacement);
Check(Dormancy.AwakeList.SequenceEqual(new[] { visible }) && !LiveDecoRenderer.Active, "new scene list replaces old and restores owner");
LiveDecoRenderer.Active = true; Dormancy.ForgetLevel();
Check(!Dormancy.LiveTrackingReady && !LiveDecoRenderer.Active && Dormancy.AwakeList.Count == 0, "unload clears and restores even while paused");
Dormancy.Enabled = false; Denied(manager, "enabled=False"); Dormancy.Enabled = true;
MoveApply.LogicSkip = false; Denied(manager, "logicSkip=False"); MoveApply.LogicSkip = true;
FrameCompare.Resume(); Time.frameCount++; Denied(manager, "capturePaused=False");
FrameCompare.Pause();
var absent = new scrDecorationManager { allDecorations = null }; Denied(absent, "source=False");

void RejectRenderer(string label, bool managerExists = true, int reset = 0)
{
    try { RendererLifetime.Check(managerExists, reset); throw new Exception("accepted " + label); }
    catch (InvalidOperationException ex) { Check(ex.Message == "playback/lifecycle changed", label); }
}
FrameCompare.Resume(); Time.frameCount++;
// A still-stopped cache on the next frame must restore, never extend a grace period.
FrameCompare.Pause(); Dormancy.PrepareLiveTracking(manager); LiveDecoRenderer.Active = true;
FrameCompare.Resume(); Time.frameCount++; Frame(manager);
Check(!LiveDecoRenderer.Active && !Dormancy.LiveTrackingReady, "resume bridge expires even if playback remains stopped");
RejectRenderer("renderer also refuses expired resume bridge");
FrameCompare.Pause(); Dormancy.PrepareLiveTracking(manager); LiveDecoRenderer.Active = true;
FrameCompare.Resume(); SceneReset.Resetting = true; RejectRenderer("scene reset overrides bridge"); SceneReset.Resetting = false;
RejectRenderer("scene generation change overrides bridge", reset: -1);
RejectRenderer("missing manager overrides bridge", managerExists: false);
Dormancy.ForgetLevel();
Check(!LiveDecoRenderer.Active && !Dormancy.LiveTrackingReady, "explicit unload overrides same-frame bridge");
foreach (int kind in Enumerable.Range(0, 5))
{
    Time.frameCount++;
    var original = scrController.instance;
    original.paused = kind == 0;
    AudioListener.pause = kind == 1;
    Time.timeScale = kind == 2 ? 0 : 1;
    DG.Tweening.DOTween.timeScale = kind == 3 ? 0 : 1;
    FrameCompare.Pause(); FrameCompare.Resume();
    if (kind == 4) scrController.instance = new();
    Check(!FrameCompare.PlaybackContinuity, "resume bridge rejects prior pause or replaced controller " + kind);
    RejectRenderer("unsafe resume rejected " + kind);
    scrController.instance = original; original.paused = false;
    AudioListener.pause = false; Time.timeScale = DG.Tweening.DOTween.timeScale = 1;
}
Time.frameCount++; FrameCompare.Pause(); FrameCompare.FailRestore = true;
try { FrameCompare.Resume(); throw new Exception("restore unexpectedly succeeded"); }
catch (InvalidOperationException) { Check(!FrameCompare.Paused && !FrameCompare.PlaybackContinuity, "failed restore never arms bridge"); }
FrameCompare.FailRestore = false;
GpuLateCacheChecks.Run(Check);
Console.WriteLine($"Dormancy production lifecycle: {checks} checks passed; managed adapters, no live pixels/FPS/lifetime proof");
return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
