param(
    [string]$Chart = "",
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice"
)
$ErrorActionPreference = "Stop"
$repo = "E:\StutterFixWork\wt-gpu"
$modDir = Join-Path $GameDir "Mods\StutterFix"
$logPath = Join-Path $env:USERPROFILE "AppData\LocalLow\7th Beat Games\A Dance of Fire and Ice\Player.log"
$bundle = Join-Path $repo "tools\gpudeco-lab\evidence\bundle\stutterfix_gpudeco"

function Test-DllMarkers([string]$dllPath) {
    if (!(Test-Path -LiteralPath $dllPath)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($dllPath)
    $text = [System.Text.Encoding]::Unicode.GetString($bytes)
    return $text.Contains("oversized-world-quad") -and $text.Contains("UNorm8_equivalent") -and $text.Contains("live_prepare_candidates")
}

if ([string]::IsNullOrEmpty($Chart) -or !(Test-Path -LiteralPath $Chart)) {
    $found = Get-ChildItem -Path "E:\StutterFixWork", "D:\" -Filter "*FULL.adofai" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match "Arche" } | Select-Object -First 1
    if (!$found) { throw "Arche *FULL.adofai 파일을 찾지 못했습니다. .\run-g1-diag.ps1 -Chart '경로' 로 실행해 주세요." }
    $Chart = $found.FullName
}
Write-Host "사용할 맵: $Chart" -ForegroundColor Cyan

$runningProc = Get-Process "A Dance of Fire and Ice" -ErrorAction SilentlyContinue
if ($runningProc) {
    Stop-Process -InputObject $runningProc -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}

$exportDir = Join-Path $repo ("tools\gpudeco-lab\exports\diag-" + [guid]::NewGuid().ToString("N"))
$steps = @(
    "timeout 420",
    "auto on",
    "decobake on",
    "gpudeco off",
    "gpudeco reset",
    "gpudeco prepare",
    "open $Chart",
    "select 0",
    "play",
    "gpudeco scheduled",
    "pause",
    "wait 60",
    "decobake state",
    "wait 60",
    "decobake ready",
    "resume",
    "stop",
    "gpuprobe off",
    "gpusplit off",
    "matshare off",
    "livedeco off",
    "gpudeco bundle $bundle",
    "stop",
    "seek 262 12",
    "decobake ready",
    "waitsongpos 262 40",
    "pause",
    "wait 0.2",
    "gpudeco export $exportDir\262.gpudeco",
    "framecmp mask none",
    "framecmp ui exclude",
    "framecmp filters on",
    "framecmp noise N1 N2",
    "gpudeco diag5",
    "gpudeco bisect 0",
    "gpudeco off",
    "stop",
    "decobake off",
    "quit"
)
$stepsText = ($steps -join "`r`n") + "`r`n"

$dllSource = Join-Path $repo "bin\PlayerAuto\StutterFix.dll"
if (!(Test-DllMarkers $dllSource)) {
    Push-Location $repo
    try {
        Remove-Item "obj\PlayerAuto", "bin\PlayerAuto" -Recurse -Force -ErrorAction SilentlyContinue
        dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1 --no-incremental -v minimal
        if ($LASTEXITCODE -ne 0) { throw "PlayerAuto 빌드 실패" }
    } finally { Pop-Location }
}

Remove-Item (Join-Path $modDir "StutterFix.dll.userbak") -Force -ErrorAction SilentlyContinue
$dllTarget = Join-Path $modDir "StutterFix.dll"
Copy-Item -LiteralPath $dllSource -Destination $dllTarget -Force

Remove-Item (Join-Path $modDir "autotest.done") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $modDir "autotest.end") -Force -ErrorAction SilentlyContinue
[System.IO.File]::WriteAllText((Join-Path $modDir "autotest.txt"), $stepsText, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "=== G1 5대 후보 분리(diag5) 및 이분 탐색(bisect) 진단 시작 ===" -ForegroundColor Yellow
Start-Process "steam://rungameid/977950"

$launchWatch = [diagnostics.stopwatch]::StartNew()
$seenProc = $false
$nextRetry = 20
while ($launchWatch.Elapsed.TotalSeconds -lt 180) {
    $proc = Get-Process "A Dance of Fire and Ice" -ErrorAction SilentlyContinue
    if ($proc) { $seenProc = $true; break }
    if ($launchWatch.Elapsed.TotalSeconds -ge $nextRetry) {
        $exePath = Join-Path $GameDir "A Dance of Fire and Ice.exe"
        if (Test-Path -LiteralPath $exePath) {
            Start-Process -FilePath $exePath -WorkingDirectory $GameDir
        } else {
            Start-Process "steam://rungameid/977950"
        }
        $nextRetry += 20
    }
    Start-Sleep -Seconds 2
}
if (!$seenProc) {
    throw "게임 프로세스가 180초 내에 시작되지 않았습니다."
}

$timeout = [diagnostics.stopwatch]::StartNew()
while ($timeout.Elapsed.TotalMinutes -lt 20) {
    $proc = Get-Process "A Dance of Fire and Ice" -ErrorAction SilentlyContinue
    if (!$proc) { Start-Sleep -Seconds 5; break }
    Start-Sleep -Seconds 2
}

$outLog = Join-Path $repo "g1-diag-result.txt"
if (Test-Path $logPath) {
    Get-Content $logPath -Encoding UTF8 | Where-Object { $_ -match "\[gpudeco\]|\[자동 시험\]|Exception|Error" } | Set-Content $outLog -Encoding UTF8
    Write-Host "`n=== G1 진단 결과 요약 ($outLog 저장 완료) ===" -ForegroundColor Green
    Get-Content $outLog | Select-Object -Last 50 | ForEach-Object { Write-Host $_ }
}
