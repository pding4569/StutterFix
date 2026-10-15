# Filter GPU diagnostics

Build: powershell -ExecutionPolicy Bypass -File tools/filtergpu/build.ps1

Build the PlayerAuto C# DLL. Copy filtergpu.dll beside StutterFix.dll for diagnostic runs only. This DLL is not embedded or shipped. D3D11 only.

Commands (DEV/AUTOTEST):
- filtergpu on|state|off|reset
- waitsong: wait at least five seconds and until the existing song tracker stops; verify OnLandOnPortal in the saved log.
- filterhalf CameraFilterPack_TYPE|off: expensive same-frame original/control/half-resolution comparison, up to eight samples.
- filterhalfperf CameraFilterPack_TYPE|off: half-resolution rendering without readback, for separate performance trials.

Never collect gameplay performance while filterhalf is running. Install and remove hooks before/after playback. Main.Unload removes all three probe IDs.

check.exe is an independent query lifecycle smoke test. It explicitly Flushes only its own device to make a finite offline test; the game plugin never Flushes or waits for GetData.

run.py was the exploratory campaign. final.py is the corrected follow-up used in this mission; its end-of-run restore expects the preserved files under docs/handoff/filters-evidence and should not be reused with another session's backup. summarize.py regenerates metrics.json and filter-table.md from saved final logs.
