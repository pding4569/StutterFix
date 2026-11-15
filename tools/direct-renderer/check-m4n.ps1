param([switch]$SkipBuilds)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4n'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $repo
try {
    $sourcePaths = @('StutterFix.csproj','SceneRenderOrder.cs','DirectSequenceChanges.cs','tools/direct-renderer/m4-contract/Program.cs','tools/direct-renderer/m4d-policy/Program.cs')
    $sourcePaths += @(Get-ChildItem -LiteralPath $repo -Filter 'DirectFull*.cs' -File | ForEach-Object FullName)
    $sourcePaths += @(Get-ChildItem "$repo/native/sfnative/drfull*" -File | ForEach-Object FullName)
    $sourcePaths += @(Get-ChildItem "$PSScriptRoot/drfull*.cpp" -File | ForEach-Object FullName)
    $beforeSources = @(Get-FileHash -LiteralPath $sourcePaths -Algorithm SHA256)
    $beforeSources | ConvertTo-Json | Set-Content "$out/source-before.json"
    if (!$SkipBuilds) {
        $run = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $validation = "out/direct-renderer/m4n/validation-$run"
        @{directory=$validation} | ConvertTo-Json | Set-Content "$out/validation-latest.json"
        & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory $validation
        if ($LASTEXITCODE) { throw 'M4n four builds/regressions failed' }
    }
    & "$PSScriptRoot/check-m4n-warp.ps1"
    if ($LASTEXITCODE) { throw 'M4n WARP regression failed' }
    & py -3.12 "$PSScriptRoot/plan-m4n.py"
    if ($LASTEXITCODE) { throw 'M4n plan generation failed' }
    $afterSources = @(Get-FileHash -LiteralPath $sourcePaths -Algorithm SHA256)
    $afterSources | ConvertTo-Json | Set-Content "$out/source-after.json"
    if (Compare-Object $beforeSources $afterSources -Property Path,Hash) { throw 'M4n source changed during validation' }
    & py -3.12 "$PSScriptRoot/check-m4n-artifacts.py"
    if ($LASTEXITCODE) { throw 'M4n evidence audit failed or installed session HOLD' }
} finally { Pop-Location }
