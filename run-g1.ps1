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
    if (!$found) { throw "Arche *FULL.adofai 파일을 찾지 못했습니다. .\run-g1.ps1 -Chart '경로' 로 실행해 주세요." }
    $Chart = $found.FullName
}
Write-Host "사용할 맵: $Chart" -ForegroundColor Cyan

$runningProc = Get-Process "A Dance of Fire and Ice" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Host "실행 중인 게임 프로세스를 먼저 종료합니다..." -ForegroundColor Yellow
    Stop-Process -InputObject $runningProc -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}

$exportDir = Join-Path $repo ("tools\gpudeco-lab\exports\run-" + [guid]::NewGuid().ToString("N"))
$jsonPath = Join-Path $repo "g1-steps.json"
Push-Location $repo
try {
    py -3 tools\optimization\gpudeco-steps.py --chart "$Chart" --export-dir "$exportDir" --bundle "$bundle" --output "$jsonPath"
} finally { Pop-Location }

$stepsObj = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stepsText = ($stepsObj.steps -join "`r`n") + "`r`n"

$dllSource = Join-Path $repo "bin\PlayerAuto\StutterFix.dll"
$csCheck = Get-Content (Join-Path $repo "GpuDecoExport.cs") -Raw -Encoding UTF8
if ($csCheck -notmatch "oversized-world-quad") {
    throw "E:\StutterFixWork\wt-gpu 에 최신 패치가 복사되지 않았습니다! 먼저 apply-g1-g2.ps1 을 실행해 주세요."
}
if (!(Test-DllMarkers $dllSource)) {
    Write-Host "PlayerAuto DLL 클린 재빌드 중 (--no-incremental)..." -ForegroundColor Cyan
    Push-Location $repo
    try {
        Remove-Item "obj\PlayerAuto", "bin\PlayerAuto" -Recurse -Force -ErrorAction SilentlyContinue
        dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1 --no-incremental -v minimal
        if ($LASTEXITCODE -ne 0) { throw "PlayerAuto 빌드 실패" }
    } finally { Pop-Location }
}
if (!(Test-DllMarkers $dllSource)) {
    throw "빌드된 PlayerAuto DLL에 패치 시그니처가 없습니다: $dllSource"
}

Remove-Item (Join-Path $modDir "StutterFix.dll.userbak") -Force -ErrorAction SilentlyContinue
$dllTarget = Join-Path $modDir "StutterFix.dll"
Copy-Item -LiteralPath $dllSource -Destination $dllTarget -Force
$srcHash = (Get-FileHash -LiteralPath $dllSource -Algorithm SHA256).Hash
$dstHash = (Get-FileHash -LiteralPath $dllTarget -Algorithm SHA256).Hash
if ($srcHash -ne $dstHash -or !(Test-DllMarkers $dllTarget)) {
    throw "모드 폴더 DLL 복사 검증 실패 ($dllTarget)"
}
Write-Host ("모드 DLL 배포 및 시그니처 검증 완료: $dllTarget (SHA256: " + $dstHash.Substring(0, 16) + "...)") -ForegroundColor Green

Remove-Item (Join-Path $modDir "autotest.done") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $modDir "autotest.end") -Force -ErrorAction SilentlyContinue
[System.IO.File]::WriteAllText((Join-Path $modDir "autotest.txt"), $stepsText, (New-Object System.Text.UTF8Encoding($false)))

$runStart = Get-Date
Write-Host "=== G1 정지 픽셀 시험 시작 (게임 자동 실행 중, 마우스/키보드를 건드리지 마세요) ===" -ForegroundColor Yellow
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
    throw "게임 프로세스가 180초 내에 시작되지 않았습니다. Steam 실행 상태를 확인해 주세요."
}
Write-Host "  게임 프로세스 시작 확인됨! G1 시험 완료 대기 중..." -ForegroundColor Cyan

$timeout = [diagnostics.stopwatch]::StartNew()
while ($timeout.Elapsed.TotalMinutes -lt 20) {
    $proc = Get-Process "A Dance of Fire and Ice" -ErrorAction SilentlyContinue
    if (!$proc) { Start-Sleep -Seconds 5; break }
    Start-Sleep -Seconds 2
}

$outLog = Join-Path $repo "g1-result.txt"
if (Test-Path $logPath) {
    Get-Content $logPath -Encoding UTF8 | Where-Object { $_ -match "\[gpudeco\]|\[자동 시험\]|\[장식굽기|Exception|Error" } | Set-Content $outLog -Encoding UTF8
    Write-Host "`n=== G1 결과 요약 ($outLog 저장 완료) ===" -ForegroundColor Green
    Get-Content $outLog | Select-Object -Last 45 | ForEach-Object { Write-Host $_ }
}
