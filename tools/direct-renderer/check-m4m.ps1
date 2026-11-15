param([switch]$SkipBuilds)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4m'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $repo
$sourcePaths = @('StutterFix.csproj', 'SceneRenderOrder.cs', 'tools/direct-renderer/m4-contract/Program.cs', 'tools/direct-renderer/m4d-policy/Program.cs')
$sourcePaths += @(Get-ChildItem -LiteralPath $repo -Filter 'DirectFull*.cs' -File | ForEach-Object { $_.FullName })
$sourcePaths += @(Get-ChildItem "$repo/native/sfnative/drfull*" -File | ForEach-Object { $_.FullName })
$sourcePaths += @(Get-ChildItem "$PSScriptRoot/drfull*.cpp" -File | ForEach-Object { $_.FullName })
$beforeSources = @(Get-FileHash -LiteralPath $sourcePaths -Algorithm SHA256)
$beforeSources | ConvertTo-Json | Set-Content "$out/source-before.json"
try {
    if (!$SkipBuilds) {
        # A fresh snapshot for EACH invocation; never compare against an older
        # session's installed DLLs. This checker only reads the installation.
        $run = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $validation = "out/direct-renderer/m4m/validation-$run"
        @{ directory = $validation } | ConvertTo-Json | Set-Content "$out/validation-latest.json"
        & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory $validation
        if ($LASTEXITCODE) { throw 'M4m four builds/regressions failed' }
    }
    & "$PSScriptRoot/check-m4m-warp.ps1"
    if ($LASTEXITCODE) { throw 'M4m WARP failed' }
    & "$PSScriptRoot/check-m4m-warp.ps1" -PixelOnly
    if ($LASTEXITCODE) { throw 'M4m original DXBC cached pixels failed' }
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
    & py -3.12 "$PSScriptRoot/plan-m4m.py"
    if ($LASTEXITCODE) { throw 'M4m plans failed' }
    # Full Grab is the production policy; rectangle proofs remain fixture-only.
    $afterSources = @(Get-FileHash -LiteralPath $sourcePaths -Algorithm SHA256)
    $afterSources | ConvertTo-Json | Set-Content "$out/source-after.json"
    if (Compare-Object $beforeSources $afterSources -Property Path,Hash) { throw 'M4m source changed during verification' }
    & py -3.12 "$PSScriptRoot/check-m4m-artifacts.py"
    if ($LASTEXITCODE -eq 2) { throw 'M4m builds/fixtures PASS; session installation hash HOLD; see installed-session-result.json' }
    if ($LASTEXITCODE) { throw 'M4m evidence audit failed' }
} finally { Pop-Location }
