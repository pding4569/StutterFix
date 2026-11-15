param([switch]$PixelOnly,[switch]$LocalOnly,[switch]$Hardware)
$ErrorActionPreference = 'Stop'
$checkProcess = [System.Diagnostics.Process]::GetCurrentProcess()
$checkPriority = $checkProcess.PriorityClass
$checkProcess.PriorityClass = 'BelowNormal'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4n'
$build = Join-Path $out 'warp-build'
New-Item -ItemType Directory -Force $build | Out-Null
$vs = & "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc = (Get-ChildItem "$vs/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp/c/Include/10.0.26100.0'
$sdkl = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp.x64/c'
$env:INCLUDE = "$msvc/include;$sdki/ucrt;$sdki/um;$sdki/shared;$sdki/winrt"
$env:LIB = "$msvc/lib/x64;$sdkl/ucrt/x64;$sdkl/um/x64"
$env:PATH = "$msvc/bin/Hostx64/x64;$env:PATH"
Push-Location $build
try {
    $fixtures = @('drfull-m4n-warp','drfull-m4m-warp','drfull-m4l-warp','drfull-m4l-lifecycle','drfull-m4j-warp','drfull-m4k-warp','drfull-m4d-warp','drfull-warp')
    if ($LocalOnly) { $fixtures = @('drfull-m4n-warp','drfull-m4m-warp') }
    if ($Hardware) {
        if ((Test-Path 'E:/StutterFixWork/game.lock') -or (Test-Path 'E:/StutterFixWork/user_playing.flag')) { throw 'Hardware fixture deferred: external measurement/user-play guard exists' }
        if (Get-Process -Name 'ADanceOfFireAndIce','A Dance of Fire and Ice' -ErrorAction SilentlyContinue) { throw 'Hardware fixture requires no concurrent game process' }
        $fixtures = @('drfull-m4n-warp')
    }
    if ($PixelOnly) { $fixtures = @('drfull-warp') }
    if ($Hardware) {
        $hardwareSources = @((Get-ChildItem "$repo/native/sfnative/drfull*" -File).FullName) + @("$repo/tools/direct-renderer/drfull-m4n-warp.cpp","$repo/tools/direct-renderer/drfull-warp.cpp")
        $hardwareBefore = @(Get-FileHash -LiteralPath $hardwareSources -Algorithm SHA256)
        $hardwareBefore | ConvertTo-Json | Set-Content "$out/hardware-source-before.json"
    }
    foreach ($fixture in $fixtures) {
        & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /fp:strict /MT /W4 /WX "$repo/tools/direct-renderer/$fixture.cpp" "$repo/native/sfnative/drfull.cpp" "/Fe:$fixture.exe" /link d3d11.lib d3dcompiler.lib dxgi.lib *> "$out/build-$fixture.log"
        if ($LASTEXITCODE) { throw "$fixture build failed; see $out/build-$fixture.log" }
    }
    if ($Hardware) { $env:SF_M4N_HARDWARE='1' }
    try {
        if ($Hardware -and ((Get-Process -Name 'ADanceOfFireAndIce','A Dance of Fire and Ice' -ErrorAction SilentlyContinue) -or (Test-Path 'E:/StutterFixWork/game.lock') -or (Test-Path 'E:/StutterFixWork/user_playing.flag'))) { throw 'Hardware fixture deferred: game/measurement began during compilation' }
        foreach ($fixture in $fixtures | Where-Object { $_ -ne 'drfull-warp' -and $_ -ne 'drfull-m4d-warp' }) {
            $log = if ($Hardware) { "$out/$fixture-hardware.log" } else { "$out/$fixture.log" }
            & "$build/$fixture.exe" *> $log
            if ($LASTEXITCODE) { throw "$fixture failed; see $log" }
            Get-Content $log
        }
    } finally { Remove-Item Env:SF_M4N_HARDWARE -ErrorAction SilentlyContinue }
    if ($Hardware) {
        if (Get-Process -Name 'ADanceOfFireAndIce','A Dance of Fire and Ice' -ErrorAction SilentlyContinue) { throw 'Hardware comparison invalidated by concurrent game process' }
        $hardwareAfter = @(Get-FileHash -LiteralPath $hardwareSources -Algorithm SHA256)
        if (Compare-Object $hardwareBefore $hardwareAfter -Property Path,Hash) { throw 'Hardware comparison source changed while compiling/running' }
        $hardwareAfter | ConvertTo-Json | Set-Content "$out/hardware-source-after.json"
    }
    if ($LocalOnly -or $Hardware) { return }
    if (!$PixelOnly) {
        & "$build/drfull-m4d-warp.exe" "$out/composite" *> "$out/composite.log"
        if ($LASTEXITCODE) { throw 'M4d compositor failed' }
    }
    & py -3.12 "$PSScriptRoot/make-m4e-grab-cases.py" $out
    & py -3.12 "$PSScriptRoot/make-m4e-grab-cases.py" $out --named
    foreach ($mode in @('cached','immediate','deferred','legacy')) {
        if ($mode -eq 'cached') { $env:SF_DRFULL_TEST_CACHE='1'; $env:SF_DRFULL_TEST_PIXEL_ONLY='1'; $env:SF_DRFULL_TEST_RECT='1' }
        if ($mode -eq 'deferred') { $env:SF_DRFULL_TEST_DEFERRED='1' }
        if ($mode -eq 'legacy') { $env:SF_DRFULL_TEST_LEGACY_CB='1' }
        try {
            foreach ($pair in @(@('all',"$repo/out/direct-renderer/m4bc/warp-cases.bin"),@('grab',"$out/grab-cases.bin"),@('named-grab',"$out/named-grab-cases.bin"))) {
                & "$build/drfull-warp.exe" $pair[1] "$PSScriptRoot/drfull-reference.hlsl" "$out/$mode-$($pair[0])" *> "$out/$mode-$($pair[0]).log"
                if ($LASTEXITCODE) { throw "$mode $($pair[0]) failed" }
                Get-Content "$out/$mode-$($pair[0]).log" -Tail 1
            }
        } finally { Remove-Item Env:SF_DRFULL_TEST_CACHE,Env:SF_DRFULL_TEST_PIXEL_ONLY,Env:SF_DRFULL_TEST_RECT,Env:SF_DRFULL_TEST_DEFERRED,Env:SF_DRFULL_TEST_LEGACY_CB -ErrorAction SilentlyContinue }
    }
} finally { Pop-Location; $checkProcess.PriorityClass = $checkPriority }
