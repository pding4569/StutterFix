using StutterFix;
using A = StutterFix.ActivationAdapter;

try
{
int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
void Ready()
{
    A.Prepare();
    Check(A.starting && A.EnabledSeconds == 0 && A.ownedCount == 0 && A.Renders == 0,
        "Update setup leaves originals unowned and warmup at zero");
    // Game scrCamera.Update writes Z=0, LateUpdate.SetPosition writes Z=-10.
    A.camera.transform.position = new Vector3(-10);
    A.Cull(A.camera);
    Check(A.Active && !A.starting && A.ownedCount == 2026 && A.sortCameraZ == -10,
        "first actual camera cull captures post-LateUpdate sort context");
    Check(A.EnabledSeconds == 0 && A.reports == 1 && A.logs.Contains("phase=activation"),
        "first admission excluded from warmup and steady cost samples");
}
Ready();
Check(A.Renders == 1 && LiveDecoGate2.Error == null, "transient Z=0 does not abort activation");
A.Cull(A.camera);
Check(A.Renders == 1, "duplicate camera cull in same frame skipped");
Time.frameCount++; A.Cull(new Camera());
Check(A.Renders == 1, "other cameras cannot initialize or update ownership");
A.Cull(A.camera);
Check(A.Renders == 2 && A.Active, "next stable camera frame renders normally");
A.camera.transform.position = new Vector3(0); Time.frameCount++; A.Cull(A.camera);
Check(!A.Active && A.Stops == 1 && LiveDecoGate2.Error.Contains("camera_z=-10->0"),
    "later camera-Z change still fails and restores owners");
Check(A.logs.Any(x => x.Contains("ERROR System.InvalidOperationException:") && x.Contains("SortSupported")),
    "deferred failure retains full exception stack");

Ready(); A.camera.cullingMask ^= 1; Time.frameCount++; A.Cull(A.camera);
Check(!A.Active && LiveDecoGate2.Error.Contains("mask="), "later culling-mask change rejected");
Ready(); A.camera.transform.forward = new Vector3(-1); Time.frameCount++; A.Cull(A.camera);
Check(!A.Active && LiveDecoGate2.Error.Contains("axis="), "later sorting-axis change rejected");
Ready(); A.camera.transparencySortMode = TransparencySortMode.CustomAxis; Time.frameCount++; A.Cull(A.camera);
Check(!A.Active && LiveDecoGate2.Error.Contains("mode="), "later sorting-mode change rejected");
foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    A.Prepare(); A.camera.transform.position = new Vector3(bad); A.Cull(A.camera);
    Check(!A.Active && A.Stops == 1, "nonfinite initial camera Z rejected");
}
A.Prepare(); A.camera.orthographic = false; A.Cull(A.camera);
Check(!A.Active, "perspective initial camera rejected");
A.Prepare(); A.camera.transform.forward = new Vector3 { x = 1, z = 1 }; A.Cull(A.camera);
Check(!A.Active, "non-Z initial sorting rejected");
A.Prepare(); A.NextOwned = 0; A.camera.transform.position = new Vector3(-10); A.Cull(A.camera);
Check(!A.Active && LiveDecoGate2.Error.Contains("no eligible images"), "zero-owner first cull fails and restores");
A.Prepare(); A.Cancel(); A.Cull(A.camera);
Check(A.Renders == 0 && !A.Active, "cancellation before first cull creates no ownership");
A.Prepare(paused: true); A.camera.transform.position = new Vector3(-10); A.Cull(A.camera);
Check(A.Active && !A.starting && A.ownedCount == 2026, "paused comparison also initializes at camera cull");
Console.WriteLine($"Production activation/sort regression: {checks} checks passed; managed adapters, no Unity timing/pixels/FPS proven");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
return 0;
