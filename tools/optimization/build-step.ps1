param([Parameter(Mandatory=$true)][string]$Step, [switch]$AutoTest)
$ErrorActionPreference = 'Stop'
$repoPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repoPath
try {
    $changed = @(& git diff --name-only) + @(& git ls-files --others --exclude-standard)
    $textFiles = @($changed | Where-Object { ($_ -match '^[^/]+\.cs$' -or $_ -eq 'docs/optimization-36.md') -and (Test-Path -LiteralPath $_) } | Sort-Object -Unique)
    if ($textFiles.Count -gt 0) { & "$PSScriptRoot/check-text.ps1" -Files $textFiles }
    $outputPath = Join-Path $PSScriptRoot 'out'
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    foreach ($editionName in @('Developer','Player')) {
        $buildLog = Join-Path $outputPath "$Step-$editionName.log"
        if (Test-Path -LiteralPath $buildLog) { throw "Preserve prior build evidence: $buildLog" }
        $buildArgs = @('build','StutterFix.csproj','-c','Release','-v','q','--nologo')
        if ($editionName -eq 'Player') { $buildArgs += '-p:Edition=Player' }
        & dotnet @buildArgs *> $buildLog
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $buildLog" }
        Write-Output "$Step $editionName build: errors 0"
    }
    if ($AutoTest) {
        & dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo *> (Join-Path $outputPath "$Step-Auto.log")
        if ($LASTEXITCODE -ne 0) { throw 'Automatic test build failed' }
    }
} finally { Pop-Location }
