# TileRange offline checks

```powershell
dotnet run --project tools/tilerange-fixture/TileRangeFixture.csproj -c Release
dotnet run --project tools/tilerange-fixture/TileRangeIntegration.csproj -c Release
```

The ledger check compiles the production interval/cursor source. It compares eager
and deferred writes across partial boundaries, repeated reads, intervening writes,
NaN/infinity/signed zero, alias fences, depth overflow, and injected callback
failures. It includes 8,171, 8,933 and 90,000 tile counts. Its enqueue timer is a
.NET 8 data-structure measurement, not a Unity/Mono/game benchmark.

The integration check compiles production `FloorRange.cs` and the ledger, with
modeled Unity state, FloorAnim opacity operations and Harmony registration.
It exercises preparation, enable/disable, reference checking, legacy predecessor
completion, read/write/renderer/dictionary barriers, chunk fallback, split/capture
rejection, depth overflow, scene cleanup and typed IL replacement. It does not
execute engine property getters, the actual FloorAnim binary, or Harmony detours.

`Directory.Build.props` isolates each project's restore, intermediates and apphost.
Both projects must retain separate output state to avoid running a stale sibling
fixture executable after switching projects.
