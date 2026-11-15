param([string]$OutputDirectory = 'out/direct-renderer/m4d')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
# Offline only: local artifacts + software WARP. Never launches, installs,
# terminates or controls the game, and never edits the shared measurement lock.
# Children inherit BelowNormal priority, so background compilation yields to play.
$checkProcess = [System.Diagnostics.Process]::GetCurrentProcess()
$checkPriority = $checkProcess.PriorityClass
$checkProcess.PriorityClass = 'BelowNormal'
$out = Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $out | Out-Null
$gameMods = 'D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix'
$installed = @('StutterFix.dll', 'sfnative.dll') | ForEach-Object { Join-Path $gameMods $_ }
if (!(Test-Path "$out/installed-before.json")) {
    @(Get-FileHash -LiteralPath $installed -Algorithm SHA256) | ConvertTo-Json | Set-Content "$out/installed-before.json"
}
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc = (Get-ChildItem "$vs/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp/c/Include/10.0.26100.0'
$sdkl = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp.x64/c'
$dumpbin = "$msvc/bin/Hostx64/x64/dumpbin.exe"
$before = & $dumpbin /nologo /exports "$repo/native/sfnative.dll"
$before | Set-Content "$out/exports-before.txt"
Push-Location $repo
try {
    # Rebuild native first: each managed artifact embeds the rebuilt DLL.
    $env:SF_NATIVE_BUILD_DIR = Join-Path $out 'native-build'
    $ErrorActionPreference = 'Continue'
    & "$repo/native/build-sfnative.bat" $msvc $sdki $sdkl 'C:/SFBundle/tools/ispc-v1.31.0-windows/bin' > "$out/build-native.log" 2>&1
    $nativeExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($nativeExit) { throw "native build failed: $nativeExit" }
    $after = & $dumpbin /nologo /exports "$repo/native/sfnative.dll"
    $after | Set-Content "$out/exports-after.txt"
    $pattern = '^\s+(\d+)\s+([0-9A-F]+)\s+[0-9A-F]+\s+(\S+)\s*$'
    $b = @($before | ForEach-Object { if ($_ -match $pattern) { "$($Matches[1]) $($Matches[2]) $($Matches[3])" } })
    $a = @($after | ForEach-Object { if ($_ -match $pattern) { "$($Matches[1]) $($Matches[2]) $($Matches[3])" } })
    $bOriginal = @($b | Where-Object { [int]($_.Split(' ')[0]) -le 73 })
    $aOriginal = @($a | Where-Object { [int]($_.Split(' ')[0]) -le 73 })
    if ($bOriginal.Count -ne 73 -or (Compare-Object $bOriginal $aOriginal)) { throw 'Native original name/ordinal/hint mismatch' }
    if ($a.Count -ne 74 -or !($a | Where-Object { $_ -match '^74 .* sf_z_drfull_v1$' })) { throw 'M4 appended API export missing' }
    if ($b.Count -ne 74 -or (Compare-Object $b $a)) { throw 'M4d changed an existing export name/ordinal/hint, including sf_z_drfull_v1' }
    'PASS M4d existing exports name/ordinal/hint 74/74 including sf_z_drfull_v1' | Set-Content "$out/exports-m4d-result.txt"
    "PASS native original exports name/ordinal/hint: 73/73; total=$($a.Count)" | Set-Content "$out/exports-result.txt"
    foreach ($edition in @('dev', 'player', 'playerauto')) {
        $argsBuild = @('build', 'StutterFix.csproj', '-c', 'Release')
        if ($edition -ne 'dev') { $argsBuild += '-p:Edition=Player' }
        if ($edition -eq 'playerauto') { $argsBuild += '-p:AutoTestBuild=1' }
        $ErrorActionPreference = 'Continue'
        & dotnet @argsBuild > "$out/build-$edition.log" 2>&1
        $buildExit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($buildExit) { throw "$edition build failed: $buildExit" }
        Write-Output "PASS $edition build"
    }
    $ErrorActionPreference = 'Continue'
    & dotnet run --project tools/direct-renderer/m4-check -c Release -- $repo > "$out/lifecycle.log" 2>&1
    $fixtureExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($fixtureExit) { throw "M4 lifecycle fixture failed: $fixtureExit" }
    Get-Content "$out/lifecycle.log"
    $ErrorActionPreference = 'Continue'
    & dotnet run --project tools/direct-renderer/m4-contract -c Release -- $repo > "$out/contract.log" 2>&1
    $contractExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($contractExit) { throw "M4 resource/ABI contract failed: $contractExit" }
    Get-Content "$out/contract.log" -Tail 1
    # Production native renderer, forced WARP, no window/swapchain/game calls.
    $fixtureDir = Join-Path $out 'warp-build'
    New-Item -ItemType Directory -Force $fixtureDir | Out-Null
    $env:INCLUDE = "$msvc/include;$sdki/ucrt;$sdki/um;$sdki/shared;$sdki/winrt"
    $env:LIB = "$msvc/lib/x64;$sdkl/ucrt/x64;$sdkl/um/x64"
    $env:PATH = "$msvc/bin/Hostx64/x64;$env:PATH"
    Push-Location $fixtureDir
    try {
        $ErrorActionPreference = 'Continue'
        & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /fp:strict /MT /W4 /WX "$repo/tools/direct-renderer/drfull-warp.cpp" "$repo/native/sfnative/drfull.cpp" /Fe:drfull-warp.exe /link d3d11.lib d3dcompiler.lib dxgi.lib > "$out/build-warp.log" 2>&1
        $warpBuildExit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($warpBuildExit) { throw "WARP build failed: $warpBuildExit" }
    } finally { Pop-Location }
    & py -3.12 tools/direct-renderer/make-warp-cases.py
    if ($LASTEXITCODE) { throw 'WARP input generation failed' }
    $ErrorActionPreference = 'Continue'
    & "$fixtureDir/drfull-warp.exe" "$repo/out/direct-renderer/m4bc/warp-cases.bin" "$repo/tools/direct-renderer/drfull-reference.hlsl" "$out/warp" > "$out/warp.log" 2>&1
    $warpExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($warpExit) { throw "WARP fixture failed: $warpExit" }
    Get-Content "$out/warp.log" -Tail 2
    Push-Location $fixtureDir
    try {
        $ErrorActionPreference = 'Continue'
        & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /fp:strict /MT /W4 /WX "$repo/tools/direct-renderer/drfull-m4d-warp.cpp" "$repo/native/sfnative/drfull.cpp" /Fe:drfull-m4d-warp.exe /link d3d11.lib d3dcompiler.lib dxgi.lib > "$out/build-m4d-warp.log" 2>&1
        $m4dBuildExit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($m4dBuildExit) { throw "M4d WARP build failed: $m4dBuildExit" }
    } finally { Pop-Location }
    $ErrorActionPreference = 'Continue'
    & "$fixtureDir/drfull-m4d-warp.exe" "$out/warp-m4d" > "$out/warp-m4d.log" 2>&1
    $m4dExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($m4dExit) { throw "M4d WARP failed: $m4dExit" }
    Get-Content "$out/warp-m4d.log" -Tail 1
    $ErrorActionPreference = 'Continue'
    & dotnet run --project tools/optimization/FrameCompareCheck -c Release > "$out/pixel-metrics.log" 2>&1
    $pixelExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($pixelExit) { throw "pixel metrics fixture failed: $pixelExit" }
    Get-Content "$out/pixel-metrics.log"
    $ErrorActionPreference = 'Continue'
    & dotnet run --project tools/direct-renderer/m4d-policy -c Release > "$out/policy-final.log" 2>&1
    $policyExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($policyExit) { throw "M4d layer/order policy fixture failed: $policyExit" }
    Get-Content "$out/policy-final.log" -Tail 1
    & py -3.12 tools/direct-renderer/plan-m4c.py --output "$out/plans"
    if ($LASTEXITCODE) { throw 'M4d plan generation failed' }
    & py -3.12 tools/direct-renderer/check-m4d-artifacts.py $OutputDirectory
    if ($LASTEXITCODE) { throw 'M4 artifact verification failed' }
} finally {
    Pop-Location
    $checkProcess.PriorityClass = $checkPriority
    $ErrorActionPreference = 'Stop'
    $afterInstalled = @(Get-FileHash -LiteralPath $installed -Algorithm SHA256)
    $afterInstalled | ConvertTo-Json | Set-Content "$out/installed-after.json"
    $beforeInstalled = Get-Content "$out/installed-before.json" -Raw | ConvertFrom-Json
    $changed = @($afterInstalled | Where-Object {
        $item = $_; !($beforeInstalled | Where-Object { $_.Path -eq $item.Path -and $_.Hash -eq $item.Hash })
    })
    if ($changed.Count -or $beforeInstalled.Count -ne $afterInstalled.Count) {
        'HOLD installed DLL hash changed during session; snapshots retained, no installation/restoration performed' | Tee-Object -FilePath "$out/installed-result.txt"
    } else {
        "PASS installed DLL hashes preserved: $($afterInstalled.Count)/$($afterInstalled.Count)" | Tee-Object -FilePath "$out/installed-result.txt"
    }
}
