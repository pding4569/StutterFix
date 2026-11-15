$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4g'
New-Item -ItemType Directory -Force $out | Out-Null
$process = [System.Diagnostics.Process]::GetCurrentProcess()
$priority = $process.PriorityClass
$process.PriorityClass = 'BelowNormal'
Push-Location $repo
try {
    # Local builds + software WARP only. No game, installation, commit or lock changes.
    & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory 'out/direct-renderer/m4g/validation'
    if ($LASTEXITCODE) { throw 'M4g builds/regression checks failed' }
    & py -3.12 "$PSScriptRoot/make-m4e-grab-cases.py" 'out/direct-renderer/m4g'
    if ($LASTEXITCODE) { throw 'Grab cases failed' }
    & py -3.12 "$PSScriptRoot/make-m4e-grab-cases.py" 'out/direct-renderer/m4g' --named
    if ($LASTEXITCODE) { throw 'Named Grab cases failed' }
    & "$PSScriptRoot/check-m4e-grab.ps1" -OutputDirectory 'out/direct-renderer/m4g'
    if ($LASTEXITCODE) { throw 'Grab WARP failed' }
    & "$out/grab-warp.exe" "$out/named-grab-cases.bin" "$repo/tools/direct-renderer/drfull-reference.hlsl" "$out/named-grab" > "$out/named-grab.log" 2>&1
    if ($LASTEXITCODE) { throw 'Named Grab WARP failed' }
    & py -3.12 "$PSScriptRoot/make-warp-cases.py" --default
    if ($LASTEXITCODE) { throw 'Sprite Default cases failed' }
    & "$out/grab-warp.exe" "$repo/out/direct-renderer/m4f/default-cases.bin" "$repo/tools/direct-renderer/drfull-reference.hlsl" "$out/default-warp" > "$out/default-warp.log" 2>&1
    if ($LASTEXITCODE) { throw 'Sprite Default WARP failed' }
    Get-Content "$out/default-warp.log" -Tail 1
    & py -3.12 "$PSScriptRoot/plan-m4g.py"
    if ($LASTEXITCODE) { throw 'M4g plans failed' }
    & py -3.12 "$PSScriptRoot/check-m4g-artifacts.py"
    if ($LASTEXITCODE) { throw 'M4g artifact audit failed' }
} finally {
    Pop-Location
    $process.PriorityClass = $priority
}
