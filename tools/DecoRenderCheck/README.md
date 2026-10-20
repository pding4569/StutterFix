# DecoRender incremental control-flow fixture

```powershell
dotnet run --project tools/DecoRenderCheck -v q -- bin/Debug/StutterFix.dll bin/Player/StutterFix.dll bin/PlayerAuto/StutterFix.dll
```

Links the production `DecoRender.Incremental.cs`; checks sleep/wake retirement, stable frames, dirty range coalescing and offsets, both vertex formats, SoA fallback, child translation, late notifications, mesh-state transitions and buffer ownership. Also checks retained output across layout refresh, unchanged captures, event-only decoration barriers, once-per-frame audit, waiting/drawing transitions and timing counters. Optional DLL arguments also check diagnostic type boundaries using PE metadata.

Unity objects, native arrays and job scheduling are surrogates. This does not establish Unity worker execution, native numeric parity, rendering correctness or performance. Use the game procedure in `docs/handoff/codex-decorender-11-20261020-result.md` for those gates.
