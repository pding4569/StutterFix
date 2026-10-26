$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$evidence = Join-Path $root 'evidence'
$project = Join-Path $root 'ParallaxLab.csproj'
$dll = Join-Path $root 'bin/Release/net8.0/ParallaxLab.dll'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
dotnet restore $project --configfile (Join-Path $root 'NuGet.Config') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Offline restore failed (.NET 8 SDK/reference pack required)' }
dotnet build $project -c Release --no-restore --nologo |
    Set-Content -LiteralPath (Join-Path $evidence 'build.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
foreach ($mode in @('selftest','stats')) {
    $file = if ($mode -eq 'stats') { 'map-stats.json' } else { 'selftest.json' }
    dotnet $dll $mode | Set-Content -LiteralPath (Join-Path $evidence $file) -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "$mode failed" }
}
py -3 (Join-Path $root 'validate_stats.py') |
    Set-Content -LiteralPath (Join-Path $evidence 'independent-validation.json') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Independent validation failed' }
$previousTier = $env:DOTNET_TieredCompilation
$previousHW = $env:DOTNET_EnableHWIntrinsic
try {
    $env:DOTNET_TieredCompilation = '0'
    $env:DOTNET_EnableHWIntrinsic = '0'
    foreach ($spec in @(@('arche',28835,2091),@('hello',2476,400),@('windflower',6175,355),@('arche-no-sharing',28835,28835))) {
        dotnet $dll bench $spec[1] $spec[2] 600 9 |
            Set-Content -LiteralPath (Join-Path $evidence ('bench-'+$spec[0]+'.json')) -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "Bench failed: $($spec[0])" }
    }
} finally {
    $env:DOTNET_TieredCompilation = $previousTier
    $env:DOTNET_EnableHWIntrinsic = $previousHW
}
& (Join-Path $root 'capture-evidence.ps1')
py -3 (Join-Path $root 'summarize.py')
if ($LASTEXITCODE -ne 0) { throw 'Summary failed' }
Write-Output 'Offline evidence complete. No game launch/install/commit performed.'
