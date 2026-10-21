# Dormancy lifecycle regression

Run from the repository root:

```powershell
py -3 tools/optimization/DormancyCheck/extract.py
dotnet run --project tools/optimization/DormancyCheck -c Release
```

Links the actual `Dormancy.cs` with managed game/Unity/Harmony adapters. Checks
sleep/wake, capture pause across multiple manager frames, explicit admission
before manager LateUpdate, resume, ordinary stop, source replacement, unload,
disabled settings, and absent source. Also extracts the actual FrameCompare
pause/resume methods and renderer lifetime guard. Reproduces AutoTest.Resume
after the cached Hitch sample but before manager LateUpdate/camera PreCull.
Checks same-frame continuity, expiry on the next frame, controller replacement,
pre-existing pauses, failed restore, scene reset and explicit unload. Field references include the real managed
`List<T>._version`. No installed game methods execute.

`DormancySource` can select a temporary source copy for a negative control:
replacing only `FrameCompare.PlaybackContinuity` with `FrameCompare.Paused`
must fail the same-frame resumed-owner check. The extractor accepts optional
FrameCompare and renderer source paths for controls: disabling the resume-frame
assignment or restoring the old renderer lifetime guard must also fail. These
controls must fail the runtime assertion, not merely fail compilation. Re-extract
the real sources and rerun after each control sequence.
This is a lifecycle regression test, not evidence of actual Unity patch behavior,
pixel identity, ownership coverage, or frame cost.
