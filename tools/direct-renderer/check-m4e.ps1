$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4e'
New-Item -ItemType Directory -Force $out | Out-Null
$process = [System.Diagnostics.Process]::GetCurrentProcess()
$priority = $process.PriorityClass
$process.PriorityClass = 'BelowNormal'
Push-Location $repo
try {
    & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory 'out/direct-renderer/m4e/validation'
    if ($LASTEXITCODE) { throw 'M4d regression checks failed' }
    & py -3.12 "$PSScriptRoot/make-m4e-grab-cases.py"
    if ($LASTEXITCODE) { throw 'M4e Grab cases failed' }
    & "$PSScriptRoot/check-m4e-grab.ps1"
    if ($LASTEXITCODE) { throw 'M4e Grab WARP failed' }
    & py -3.12 "$PSScriptRoot/plan-m4e.py"
    if ($LASTEXITCODE) { throw 'M4e plans failed' }
    & py -3.12 "$PSScriptRoot/check-m4e-artifacts.py"
    if ($LASTEXITCODE) { throw 'M4e artifact audit failed' }
} finally {
    Pop-Location
    $process.PriorityClass = $priority
}
