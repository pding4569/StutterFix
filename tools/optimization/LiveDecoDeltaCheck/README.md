# Offline delta validation

From the repository root:

```powershell
python tools/optimization/LiveDecoDeltaCheck/extract.py
dotnet run --project tools/optimization/LiveDecoDeltaCheck -c Release
dotnet run --project tools/optimization/LiveDecoChangesCheck -c Release
```

`extract.py` compiles the current production ordering, connected-component repair,
and vertex arena methods verbatim with engine-free adapters. The fixture compares
6,000 deterministic mutations against an independent full partition oracle and
checks buffer contents, overlap, steady identity, tie survivor notification, and
final reclamation. It does not simulate Unity rendering or prove performance.

`LiveDecoChangesCheck` compiles the production write-site rewriter with managed
Unity/Harmony adapters. It checks typed argument/return preservation, spawn and
destroy, instruction labels, exception boundaries, writer exceptions, and managed
allocation after warmup. Real Harmony installation on Unity Mono remains untested.
