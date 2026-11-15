param([switch]$SkipBuilds)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4l'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $repo
try {
    if (!$SkipBuilds) {
        # A fresh snapshot for EACH invocation; never compare against an older
        # session's installed DLLs. This checker only reads the installation.
        $run = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $validation = "out/direct-renderer/m4l/validation-$run"
        @{ directory = $validation } | ConvertTo-Json | Set-Content "$out/validation-latest.json"
        & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory $validation
        if ($LASTEXITCODE) { throw 'M4l four builds/regressions failed' }
    }
    & "$PSScriptRoot/check-m4l-warp.ps1"
    if ($LASTEXITCODE) { throw 'M4l WARP failed' }
    & "$PSScriptRoot/check-m4l-warp.ps1" -PixelOnly
    if ($LASTEXITCODE) { throw 'M4l original DXBC cached pixels failed' }
    # Preserve the non-cache controls as well as the normal immediate path.
    foreach ($mode in @('deferred', 'immediate-legacy')) {
        try {
            if ($mode -eq 'deferred') { $env:SF_DRFULL_TEST_DEFERRED = '1' } else { $env:SF_DRFULL_TEST_LEGACY_CB = '1' }
            foreach ($pair in @(@('default', "$repo/out/direct-renderer/m4f/default-cases.bin"), @('grab', "$out/grab-cases.bin"), @('named-grab', "$out/named-grab-cases.bin"))) {
                & "$out/warp-build/drfull-warp.exe" $pair[1] "$PSScriptRoot/drfull-reference.hlsl" "$out/$mode-$($pair[0])-warp" *> "$out/$mode-$($pair[0]).log"
                if ($LASTEXITCODE) { throw "$mode $($pair[0]) failed" }
                Get-Content "$out/$mode-$($pair[0]).log" -Tail 1
            }
        } finally { Remove-Item Env:SF_DRFULL_TEST_DEFERRED, Env:SF_DRFULL_TEST_LEGACY_CB -ErrorAction SilentlyContinue }
    }
    & py -3.12 "$PSScriptRoot/plan-m4l.py"
    if ($LASTEXITCODE) { throw 'M4l plans failed' }
    & py -3.12 "$PSScriptRoot/audit-m4l-shaders.py"
    if ($LASTEXITCODE) { throw 'M4l Grab shader contract failed' }
    & py -3.12 "$PSScriptRoot/check-m4l-artifacts.py"
    if ($LASTEXITCODE) { throw 'M4l evidence audit failed' }
} finally { Pop-Location }
