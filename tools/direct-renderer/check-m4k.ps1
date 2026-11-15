$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4k'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $repo
try {
    & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory 'out/direct-renderer/m4k/validation'
    if ($LASTEXITCODE) { throw 'M4k four builds/regressions failed' }
    & "$PSScriptRoot/check-m4k-warp.ps1"
    if ($LASTEXITCODE) { throw 'M4k WARP failed' }
    foreach ($mode in @('deferred', 'immediate-legacy')) {
        try {
            if ($mode -eq 'deferred') { $env:SF_DRFULL_TEST_DEFERRED = '1' } else { $env:SF_DRFULL_TEST_LEGACY_CB = '1' }
            foreach ($pair in @(@('default', "$repo/out/direct-renderer/m4f/default-cases.bin"), @('grab', "$out/grab-cases.bin"), @('named-grab', "$out/named-grab-cases.bin"))) {
                & "$out/warp-build/drfull-warp.exe" $pair[1] "$repo/tools/direct-renderer/drfull-reference.hlsl" "$out/$mode-$($pair[0])-warp" *> "$out/$mode-$($pair[0]).log"
                if ($LASTEXITCODE) { throw "$mode $($pair[0]) failed" }
                Get-Content "$out/$mode-$($pair[0]).log" -Tail 1
            }
        } finally { Remove-Item Env:SF_DRFULL_TEST_DEFERRED, Env:SF_DRFULL_TEST_LEGACY_CB -ErrorAction SilentlyContinue }
    }
    & dotnet run --project "$PSScriptRoot/m4j-enums" -c Release > "$out/unity-enums.json"
    if ($LASTEXITCODE) { throw 'Unity enum metadata audit failed' }
    & py -3.12 "$PSScriptRoot/summarize-m4k-drawgpu.py" --self-test > "$out/draw-analyzer.log"
    if ($LASTEXITCODE) { throw 'M4k analyzer failed' }
    & py -3.12 "$PSScriptRoot/plan-m4k.py"
    if ($LASTEXITCODE) { throw 'M4k plans failed' }
    & py -3.12 "$PSScriptRoot/check-m4k-artifacts.py"
    if ($LASTEXITCODE) { throw 'M4k evidence audit failed' }
} finally { Pop-Location }
