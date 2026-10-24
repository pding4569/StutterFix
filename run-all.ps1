param(
    [string]$Chart = "",
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice"
)
$ErrorActionPreference = "Stop"
$repo = "E:\StutterFixWork\wt-gpu"
$src = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " StutterFix G1 & G2 올인원 전자동 빌드·검증·시험 러너 (run-all.ps1)" -ForegroundColor Cyan
Write-Host "=================================================================`n" -ForegroundColor Cyan

# 1. 패치 적용 및 3종 빌드 + LoadCheck + DataCheck
Write-Host "=== 1단계: 패치 적용 및 3종 빌드 / 계약 검증 ===" -ForegroundColor Yellow
& "$src\apply-g1-g2.ps1"
if ($LASTEXITCODE -ne 0) { throw "1단계 패치 적용 및 빌드 실패" }

# 2. G1 정지 스냅샷 픽셀 검증 (262 / 270 / 276초)
Write-Host "`n=== 2단계: G1 정지 픽셀 검증 실행 (run-g1.ps1) ===" -ForegroundColor Yellow
if ([string]::IsNullOrEmpty($Chart)) {
    & (Join-Path $repo "run-g1.ps1") -GameDir "$GameDir"
} else {
    & (Join-Path $repo "run-g1.ps1") -Chart "$Chart" -GameDir "$GameDir"
}

# 3. G2 실시간 GPU 렌더러 ABBA + 안정성 4단계 검증
Write-Host "`n=== 3단계: G2 실시간 GPU 재생 ABBA + 안정성 검증 실행 (run-g2.ps1) ===" -ForegroundColor Yellow
if ([string]::IsNullOrEmpty($Chart)) {
    & (Join-Path $repo "run-g2.ps1") -GameDir "$GameDir"
} else {
    & (Join-Path $repo "run-g2.ps1") -Chart "$Chart" -GameDir "$GameDir"
}

# 4. 종합 최종 판정 리포트 생성
$g1Log = Join-Path $repo "g1-result.txt"
$g2Log = Join-Path $repo "g2-result.txt"
$finalLog = Join-Path $repo "FINAL-RESULT.txt"

$g1Lines = if (Test-Path $g1Log) { Get-Content $g1Log -Encoding UTF8 } else { @() }
$g2Lines = if (Test-Path $g2Log) { Get-Content $g2Log -Encoding UTF8 } else { @() }

$g1Verdict = ($g1Lines | Where-Object { $_ -match "GPUDECO_G1=" } | Select-Object -Last 1)
$g2Verdict = ($g2Lines | Where-Object { $_ -match "GPUDECO_G2=" } | Select-Object -Last 1)

$g1Pass = ($g1Verdict -match "GPUDECO_G1=PASS")
$g2Pass = ($g2Verdict -match "GPUDECO_G2=PASS")

$summary = @"
=================================================================
                 StutterFix 종합 최종 검증 보고서
=================================================================
시각: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
리포지토리: $repo

[1] G1 정지 스냅샷 픽셀 게이트 (262 / 270 / 276초)
  - 판정: $(if ($g1Pass) { "PASS (100% 동일성 통과)" } else { "FAIL / CHECK" })
  - 상세 로그: $g1Verdict

[2] G2 실시간 GPU 렌더러 (ABBA 4창 + 안정성 4단계)
  - 판정: $(if ($g2Pass) { "PASS (실시간 GPU 가속 및 장식 CPU 비용 0 달성)" } else { "FAIL / CHECK" })
  - 상세 로그: $g2Verdict

[3] 핵심 지표 요약
$($g2Lines | Where-Object { $_ -match "FPS mode=|cost scope=|g2_window|live_prepare|owned_union" } | Out-String)
=================================================================
최종 결론: $(if ($g1Pass -and $g2Pass) { "SUCCESS - 장식 비용 0 및 G1/G2 모든 목표 달성 완료!" } else { "PARTIAL / REQUIRES ATTENTION" })
=================================================================
"@

$summary | Set-Content $finalLog -Encoding UTF8
Write-Host "`n$summary" -ForegroundColor $(if ($g1Pass -and $g2Pass) { "Green" } else { "Yellow" })
Write-Host "종합 결과 파일 저장 완료: $finalLog" -ForegroundColor Cyan
