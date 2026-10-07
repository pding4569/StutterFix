param(
    [int[]]$Multipliers = @(2,3,4),
    [int]$Seconds = 15,
    [string]$TraceDir = 'C:\Users\Public\StutterFixTrace\framegenlab\wpr-native',
    [string]$PresentMon = 'C:\SFBundle\PresentMon.exe'
)
$ErrorActionPreference = 'Stop'
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (!$admin) { throw 'Run this script in an administrator PowerShell' }
if ($Seconds -lt 5 -or $Seconds -gt 60 -or !$Multipliers -or ($Multipliers | Where-Object { $_ -notin 2,3,4 })) { throw 'Seconds 5..60, multipliers 2/3/4 required' }
if (Test-Path -LiteralPath $TraceDir) { throw 'Use a new trace directory' }
New-Item -ItemType Directory -Path $TraceDir | Out-Null
$started = $false
$ErrorActionPreference = 'Continue' # PS5 must retain native stderr, not abort an ETL merge on it.
try {
    & wpr -start GPU -start DesktopComposition -filemode *> "$TraceDir\wpr-start.log"
    if ($LASTEXITCODE -ne 0) { throw 'WPR start failed; existing sessions are not cancelled' }
    $started = $true
    foreach ($n in $Multipliers) {
        & "$PSScriptRoot\out\FrameGenLab.exe" --base 0 --multiplier $n --seconds $Seconds --csv "$TraceDir\$($n)x-app.csv" *> "$TraceDir\$($n)x-app.log"
        if ($LASTEXITCODE -ne 0) { throw "Native app failed; inspect $($n)x-app.log" }
    }
} finally {
    if ($started) {
        & wpr -stop "$TraceDir\gpu.etl" -skipPdbGen *> "$TraceDir\wpr-stop.log"
        if ($LASTEXITCODE -ne 0) {
            & wpr -cancel *> "$TraceDir\wpr-cancel.log" # Only the recording successfully started above.
            throw 'WPR stop failed; our recording cancelled; inspect wpr-stop.log'
        }
    }
}
& $PresentMon --etl_file "$TraceDir\gpu.etl" --output_file "$TraceDir\presentmon.csv" --v1_metrics --no_console_stats *> "$TraceDir\presentmon.log"
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath "$TraceDir\presentmon.csv")) { throw 'Offline PresentMon failed; inspect presentmon.log' }
Get-Item "$TraceDir\presentmon.csv" | Select-Object FullName,Length | Out-File "$TraceDir\done.txt"
