$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$dist = Join-Path $repo 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$stage = Join-Path $PSScriptRoot 'out\tester\FrameGenLab'
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\out\FrameGenLab.exe","$PSScriptRoot\scene.hlsl","$PSScriptRoot\README.md" -Destination $stage
foreach ($n in 2,3,4) {
    $cmd = "@echo off`r`ncd /d `"%~dp0`"`r`nif not exist results mkdir results`r`nFrameGenLab.exe --base 0 --multiplier $n --seconds 60 --csv results\$($n)x.csv`r`npause`r`n"
    [IO.File]::WriteAllText((Join-Path $stage "$($n)x.cmd"),$cmd,[Text.ASCIIEncoding]::new())
}
$cmd = "@echo off`r`ncd /d `"%~dp0`"`r`nif not exist results mkdir results`r`nFrameGenLab.exe --base 60 --multiplier 2 --seconds 60 --csv results\60fps-2x.csv`r`npause`r`n"
[IO.File]::WriteAllText((Join-Path $stage '60fps-2x.cmd'),$cmd,[Text.ASCIIEncoding]::new())
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $dist 'FrameGenLab-2026-10-07-tester.zip'
if (Test-Path $zip) { Remove-Item -LiteralPath $zip }
$archive = [IO.Compression.ZipFile]::Open($zip,'Create')
try {
    foreach ($name in 'FrameGenLab.exe','scene.hlsl','README.md','2x.cmd','3x.cmd','4x.cmd','60fps-2x.cmd') {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $stage $name),'FrameGenLab/'+$name,'Optimal') | Out-Null
    }
} finally { $archive.Dispose() }
Get-FileHash -LiteralPath $zip -Algorithm SHA256
Get-Item -LiteralPath $zip | Select-Object FullName,Length
