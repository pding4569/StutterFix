param([switch]$Install, [ValidateSet('Stage','Capture','InGame')][string]$Probe = 'Stage')
$ErrorActionPreference = 'Stop'
if ($Install -and $Probe -eq 'InGame') { throw 'Use measure_ingame.py: in-game experiment requires native DLL and exact settings backups/restoration' }
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$out = Join-Path $PSScriptRoot $(if ($Probe -eq 'Stage') {'out\measure'} elseif ($Probe -eq 'Capture') {'out\capture'} else {'out\ingame'})
$probeType = "FrameGen$($Probe)Probe"
New-Item -ItemType Directory -Force $out | Out-Null
$main = Get-Content -LiteralPath "$repo\StutterFix.cs" -Raw
$installPoint = '            capacityApplied = true;'
$unloadPoint = '            Try(PhaseWatch.Uninstall);'
if (($main.Split(@($installPoint), [StringSplitOptions]::None).Count -ne 2) -or
    ($main.Split(@($unloadPoint), [StringSplitOptions]::None).Count -ne 2)) {
    throw 'Measurement injection points changed; inspect before building'
}
$main = $main.Replace($installPoint, "$installPoint`n            $probeType.Install();")
$main = $main.Replace($unloadPoint, "            Try($probeType.Uninstall);`n$unloadPoint")
if ($Probe -eq 'InGame') {
    $point='        public bool LowHalfRender = false;'
    if (!$main.Contains($point)) { throw 'Settings injection point changed' }
    $main=$main.Replace($point,"        public int FrameGenExperiment = 0;`n        public bool FrameGenFlipY = true;`n        public bool FrameGenCapture = false;`n$point")
    $gui='            if (Edition.Dev) DevGUI(); else PlayerGUI();'
    if (!$main.Contains($gui)) { throw 'GUI injection point changed' }
    $main=$main.Replace($gui,"            FrameGenInGameProbe.DrawGUI();`n$gui")
}
$mainPath = Join-Path $out 'MeasureMain.cs'
[IO.File]::WriteAllText($mainPath, $main, [Text.UTF8Encoding]::new($false))
$auto = Get-Content -LiteralPath "$repo\AutoTest.cs" -Raw
$quitPoint = '                case "quit":'
if ($auto.Split(@($quitPoint), [StringSplitOptions]::None).Count -ne 2) { throw 'AutoTest quit point changed' }
$auto = $auto.Replace($quitPoint, "$quitPoint`n                    $probeType.Finish();")
$autoPath = Join-Path $out 'MeasureAutoTest.cs'
[IO.File]::WriteAllText($autoPath, $auto, [Text.UTF8Encoding]::new($false))
& dotnet build "$repo\StutterFix.csproj" -v q --nologo -p:Edition=Player -p:AutoTestBuild=1 `
    "-p:CustomAfterMicrosoftCommonTargets=$PSScriptRoot\Measure.targets" "-p:FrameGenProbeMain=$mainPath" `
    "-p:FrameGenProbeAutoTest=$autoPath" `
    "-p:FrameGenProbeSource=$PSScriptRoot\$($Probe)Probe.cs" `
    "-p:FrameGenProbeKind=$Probe" "-p:FrameGenNative=$PSScriptRoot\out\ingame-native\sfnative.dll" `
    "-p:OutputPath=$out\" "-p:IntermediateOutputPath=$repo\obj\FrameGen$($Probe)Measure\" *> "$out\build.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$out\build.log" -Tail 35; throw 'Measurement build failed' }
Get-Content "$out\build.log" -Tail 4
if ($Install) {
    $mod = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix'
    if (Get-Process -Name 'A Dance of Fire and Ice' -ErrorAction SilentlyContinue) { throw 'Close game normally before installing' }
    if (!(Test-Path "$out\installed-original.dll")) { Copy-Item -LiteralPath "$mod\StutterFix.dll" -Destination "$out\installed-original.dll" }
    Copy-Item -LiteralPath "$out\StutterFix.dll" -Destination "$mod\StutterFix.dll"
    Write-Output 'Installed measurement-only PlayerAuto DLL; restore installed-original.dll after measurements'
}
