# TileRange offline checks

## T3 current

`TileRangeIntegration.csproj` now compiles production `FloorRange.cs` with
`PristineIntegration.cs`. The 76 assertions cover clean chunks (including 8,171,
8,933 and 90,000 tiles), dirty channels, partial edges, active/proxy tweens,
time-sliced renewal and late-write detection, budget, shadow preflight/oracle
failure without double application, rejection, map cleanup and failed installation.
Six assertions check default-off site diagnostics, top-12 ranking and counter reset.
`WriterRewriteCheck.csproj` adds 16 checks using the production writer transpiler,
including emitted bookkeeping stores that must preserve values without dirty marks,
tracked writes, address/setter replacement, labels/exception entry and original IL offsets.
Unity/FloorAnim and writer installation are models, not engine/detour evidence.

```powershell
dotnet run --project tools/tilerange-fixture/TileRangeIntegration.csproj -c Release
dotnet run --project tools/tilerange-fixture/WriterRewriteCheck.csproj -c Release
dotnet run --project tools/tilerange-fixture/WriterMetadataCheck.csproj -c Release -- '<Managed>/Assembly-CSharp.dll' FloorRangeWriterManifest.cs
py -3 tools/tilerange-fixture/summarize-t3.py
```

For T3b, save current logs under `t3b-*` and run
`py -3 tools/tilerange-fixture/summarize-t3.py --t3b` (historical `t3-*` evidence is preserved).
Run `py -3 tools/tilerange-fixture/audit-t3b.py '<Managed>/Assembly-CSharp.dll'`
to regenerate all 53 writer bodies, the bookkeeping reader inventory and the audit table.
In-game diagnosis is off by default: `tilerange sites on` clears site counts and enables
fixed-slot atomic counts; `tilerange status` reports the top 12; `tilerange sites off`
stops counting and retains the report. These commands do not reinstall or prepare.

`WriterMetadataCheck` resolves each of the 53 real game methods/constructors by
name plus signature blob and validates managed bodies and expected fields without
running game methods. This does not test Harmony/Mono installation. To regenerate
the finite manifest, rebuild `tools/ilscan`, save `TILE_WRITERS=1` output for the
game as `docs/handoff/tilerange-evidence/t3-game-writers.txt`, then run
`generate-writer-manifest.ps1`. The 64-target cap must hold; runtime never scans
AppDomain assemblies. Saved T3 evidence uses `t3-` filenames.

## T2 historical

The ledger and old `Integration.cs` below describe the retired deferred-opacity
experiment. `FloorRangeLedger.cs` is no longer in the mod build. Old integration
source is retained as history and is not compiled; `summarize.py` checks old T2
evidence, while `summarize-t3.py` checks current builds.

```powershell
dotnet run --project tools/tilerange-fixture/TileRangeFixture.csproj -c Release
dotnet run --project tools/tilerange-fixture/TileRangeIntegration.csproj -c Release
```

The ledger check compiles the production interval/cursor source. It compares eager
and deferred writes across partial boundaries, repeated reads, intervening writes,
NaN/infinity/signed zero, alias fences, depth overflow, and injected callback
failures. It includes 8,171, 8,933 and 90,000 tile counts. Its enqueue timer is a
.NET 8 data-structure measurement, not a Unity/Mono/game benchmark.

The former integration check compiled T2 `FloorRange.cs` and the ledger, with
modeled Unity state, FloorAnim opacity operations and Harmony registration.
It exercises preparation, enable/disable, reference checking, legacy predecessor
completion, read/write/renderer/dictionary barriers, chunk fallback, split/capture
rejection, depth overflow, scene cleanup and typed IL replacement. It does not
execute engine property getters, the actual FloorAnim binary, or Harmony detours.

`Directory.Build.props` isolates each project's restore, intermediates and apphost.
Both projects must retain separate output state to avoid running a stale sibling
fixture executable after switching projects.
