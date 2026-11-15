$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4h'
New-Item -ItemType Directory -Force $out | Out-Null
$paths = @('StutterFix.dll','sfnative.dll') | ForEach-Object { Join-Path 'D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix' $_ }
if (!(Test-Path "$out/installed-resume-before.json")) {
    @(Get-FileHash -LiteralPath $paths -Algorithm SHA256) | ConvertTo-Json | Set-Content "$out/installed-resume-before.json"
}
Push-Location $repo
try {
    & "$PSScriptRoot/check-m4d.ps1" -OutputDirectory 'out/direct-renderer/m4h/validation'
    if ($LASTEXITCODE) { throw 'M4h builds and regression checks failed' }
    & "$PSScriptRoot/check-m4h-warp.ps1"
    if ($LASTEXITCODE) { throw 'M4h WARP controls failed' }
    & py -3.12 "$PSScriptRoot/plan-m4h.py"
    if ($LASTEXITCODE) { throw 'M4h plans failed' }
    & py -3.12 "$PSScriptRoot/check-m4h-artifacts.py"
    if ($LASTEXITCODE) { throw 'M4h evidence audit failed' }
} finally {
    @(Get-FileHash -LiteralPath $paths -Algorithm SHA256) | ConvertTo-Json | Set-Content "$out/installed-resume-after.json"
    $before = Get-Content "$out/installed-resume-before.json" -Raw | ConvertFrom-Json
    $after = Get-Content "$out/installed-resume-after.json" -Raw | ConvertFrom-Json
    $same = @($before | Where-Object { $item = $_; $after | Where-Object { $_.Path -eq $item.Path -and $_.Hash -eq $item.Hash } }).Count
    "Installed DLLs preserved against resume snapshot: $same/2; no install/restore performed" | Set-Content "$out/installed-resume-result.txt"
    Pop-Location
}
