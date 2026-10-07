param([string]$ResultName = 'matched-admin', [int]$Seconds = 20, [double]$Base = 0, [switch]$Borderless,
    [string]$PresentMon = 'C:\SFBundle\PresentMon.exe')
$ErrorActionPreference = 'Stop'
if ($ResultName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Simple result name required' }
$py = (Get-Command py.exe).Source
New-Item -ItemType Directory -Force "$PSScriptRoot\results" | Out-Null
$pythonArgs = @('-3')
$pythonArgs += @("$PSScriptRoot\run_suite.py", '--seconds', "$Seconds", '--base', "$Base", '--out', "$PSScriptRoot\results\$ResultName",
    '--pm-out', "C:\Users\Public\StutterFixTrace\framegenlab\$ResultName", '--presentmon', $PresentMon)
if ($Borderless) { $pythonArgs += '--borderless' }
$ErrorActionPreference = 'Continue' # Native stderr must be preserved, not turned into a terminating PS5 error.
& $py @pythonArgs *> "$PSScriptRoot\results\$ResultName-run.log"
$code = $LASTEXITCODE
"exit=$code" | Add-Content "$PSScriptRoot\results\$ResultName-run.log"
exit $code
