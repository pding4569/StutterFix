# First-camera-cull activation regression

Run from the repository root:

```powershell
py -3 tools/optimization/LiveDecoActivationCheck/extract.py
dotnet run --project tools/optimization/LiveDecoActivationCheck -c Release
```

Extracts the unchanged production `StartRendering`, `PreCull`, `SortSupported`
and `EnabledSeconds`. Adapts camera, clock, rendering, logging and restoration.
Reproduces Update Z=0 followed by LateUpdate Z=-10; checks initialization at
the selected camera's first cull, duplicate culls, warmup, capture pause,
cancellation, zero owners and full-stack errors. Later Z/axis/mode/mask changes
still fail. Extraction also checks Begin does not render or admit synchronously.

This verifies managed scheduling and guards. It does not establish actual Unity
callback order, Harmony lifetime, geometry, pixels, allocation or frame cost.
An optional source path to `extract.py` supports a negative control: remove
the `!starting` condition from the production sort-context change guard.
