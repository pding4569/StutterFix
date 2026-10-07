param([switch]$Install)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$out = Join-Path $PSScriptRoot 'out\measure'
New-Item -ItemType Directory -Force $out | Out-Null
$main = Get-Content -LiteralPath "$repo\StutterFix.cs" -Raw
$installPoint = '            capacityApplied = true;'
$unloadPoint = '            Try(PhaseWatch.Uninstall);'
if (($main.Split(@($installPoint), [StringSplitOptions]::None).Count -ne 2) -or
    ($main.Split(@($unloadPoint), [StringSplitOptions]::None).Count -ne 2)) {
    throw 'Measurement injection points changed; inspect before building'
}
$main = $main.Replace($installPoint, "$installPoint`n            FrameGenStageProbe.Install();")
$main = $main.Replace($unloadPoint, "            Try(FrameGenStageProbe.Uninstall);`n$unloadPoint")
$mainPath = Join-Path $out 'MeasureMain.cs'
[IO.File]::WriteAllText($mainPath, $main, [Text.UTF8Encoding]::new($false))
$auto = Get-Content -LiteralPath "$repo\AutoTest.cs" -Raw
$quitPoint = '                case "quit":'
if ($auto.Split(@($quitPoint), [StringSplitOptions]::None).Count -ne 2) { throw 'AutoTest quit point changed' }
$auto = $auto.Replace($quitPoint, "$quitPoint`n                    FrameGenStageProbe.Finish();")
$autoPath = Join-Path $out 'MeasureAutoTest.cs'
[IO.File]::WriteAllText($autoPath, $auto, [Text.UTF8Encoding]::new($false))
& dotnet build "$repo\StutterFix.csproj" -v q --nologo -p:Edition=Player -p:AutoTestBuild=1 `
    "-p:CustomAfterMicrosoftCommonTargets=$PSScriptRoot\Measure.targets" "-p:FrameGenProbeMain=$mainPath" `
    "-p:FrameGenProbeAutoTest=$autoPath" `
    "-p:OutputPath=$out\" "-p:IntermediateOutputPath=$repo\obj\FrameGenMeasure\" *> "$out\build.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$out\build.log" -Tail 35; throw 'Measurement build failed' }
Get-Content "$out\build.log" -Tail 4
if ($Install) {
    $mod = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix'
    if (Get-Process -Name 'A Dance of Fire and Ice' -ErrorAction SilentlyContinue) { throw 'Close game normally before installing' }
    if (!(Test-Path "$out\installed-original.dll")) { Copy-Item -LiteralPath "$mod\StutterFix.dll" -Destination "$out\installed-original.dll" }
    Copy-Item -LiteralPath "$out\StutterFix.dll" -Destination "$mod\StutterFix.dll"
    Write-Output 'Installed measurement-only PlayerAuto DLL; restore installed-original.dll after measurements'
}
