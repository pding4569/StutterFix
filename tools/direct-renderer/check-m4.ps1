param([string]$OutputDirectory = 'out/direct-renderer/m4')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
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
    if (!$b.Count -or (Compare-Object $b $a)) { throw 'Native name/ordinal/hint mismatch' }
    "PASS native exports name/ordinal/hint: $($a.Count)" | Set-Content "$out/exports-result.txt"
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
    & py -3.12 tools/direct-renderer/check-m4-artifacts.py $OutputDirectory
    if ($LASTEXITCODE) { throw 'M4 artifact verification failed' }
} finally {
    Pop-Location
    $ErrorActionPreference = 'Stop'
    $afterInstalled = @(Get-FileHash -LiteralPath $installed -Algorithm SHA256)
    $afterInstalled | ConvertTo-Json | Set-Content "$out/installed-after.json"
    $beforeInstalled = @(Get-Content "$out/installed-before.json" -Raw | ConvertFrom-Json)
    $changed = @($afterInstalled | Where-Object {
        $item = $_; !($beforeInstalled | Where-Object { $_.Path -eq $item.Path -and $_.Hash -eq $item.Hash })
    })
    if ($changed.Count -or $beforeInstalled.Count -ne $afterInstalled.Count) {
        'HOLD installed DLL hash changed during session; snapshots retained, no installation/restoration performed' | Tee-Object -FilePath "$out/installed-result.txt"
    } else {
        "PASS installed DLL hashes preserved: $($afterInstalled.Count)/$($afterInstalled.Count)" | Tee-Object -FilePath "$out/installed-result.txt"
    }
}
