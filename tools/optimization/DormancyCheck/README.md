# Dormancy lifecycle regression

Run `dotnet run --project tools/optimization/DormancyCheck -c Release` from the repository root.

Links the actual `Dormancy.cs` with managed game/Unity/Harmony adapters. Checks
sleep/wake, capture pause across multiple manager frames, explicit admission
before manager LateUpdate, resume, ordinary stop, source replacement, unload,
disabled settings, and absent source. Field references include the real managed
`List<T>._version`. No installed game methods execute.

`DormancySource` can select a temporary source copy for a negative control:
removing only `tracking |= FrameCompare.Paused;` must fail the paused-owner check.
This is a lifecycle regression test, not evidence of actual Unity patch behavior,
pixel identity, ownership coverage, or frame cost.
