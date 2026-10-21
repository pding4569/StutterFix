# Gate2 ABBA preparation regression

```powershell
New-Item -ItemType Directory -Force tools/optimization/out | Out-Null
py -3 tools/optimization/livedeco2-steps.py --chart 'Arche FULL.adofai' --output tools/optimization/out/gate2-window-steps.json
dotnet run --project tools/optimization/LiveDecoWindowCheck -c Release -- tools/optimization/out/gate2-window-steps.json
```

Links the unchanged production `LiveDecoGate2.cs` and gate policy. Simulates
run3's 14.52-second synchronous preparation followed by first camera cull.
The old unpaused 252-second preparation overshoots 262 and is rejected.
The actual generated ABBA sequence freezes setup at 258, waits for first-cull
readiness in both modes, resumes and plays to 262 before measuring through 276.
Checks polling, cancellation/mode mismatches, nonfinite times, full diagnostics,
failed-start state, warmup and OWNED_LOW. Missing pixel evidence still fails.

Audio/DSP, Unity rendering, Harmony, clocks and logging are managed adapters.
This does not measure actual activation cost, pixels, FPS, allocation or lifetime.
