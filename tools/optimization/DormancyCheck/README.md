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

추가 GPU 목록 캐시 검사는 실제 `Dormancy.cs`와 `GpuLateListCache.cs`를 사용한다.
변경 없는 프레임의 무순회, Sleep/Wake, 같은 개수 교체/순서 변경,
소유권 세대 교체, CPU fallback/null/중복 순서, 예외 후 재구축과 참조 해제를 확인한다.
TightLate/DecoBake 연결은 managed adapter이므로 실제 Harmony 연결·픽셀·FPS는 별도 시험이 필요하다.
