$ErrorActionPreference='Stop'
$fxRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fxBuild=Join-Path $fxRepo 'tools/framegenlab/out/effects-reload-build'
if(Get-Process -Name 'A Dance of Fire and Ice' -ErrorAction SilentlyContinue){throw 'Close game normally before building the reload fixture'}
if(Test-Path -LiteralPath $fxBuild){throw 'Preserve existing reload build; do not overwrite evidence'}
New-Item -ItemType Directory -Path $fxBuild | Out-Null
$fxBefore=Join-Path $fxRepo 'bin/PlayerAuto/StutterFix.dll'
Copy-Item -LiteralPath $fxBefore -Destination (Join-Path $fxBuild 'before.dll')
$fxAfterDir=Join-Path $fxBuild 'after'
& dotnet build (Join-Path $fxRepo 'StutterFix.csproj') -v q --nologo -p:Edition=Player -p:AutoTestBuild=1 "-p:OutputPath=$fxAfterDir/" *> (Join-Path $fxBuild 'build.log')
if($LASTEXITCODE -ne 0){Get-Content (Join-Path $fxBuild 'build.log') -Tail 30;throw 'Reload build failed'}
$fxAfter=Join-Path $fxAfterDir 'StutterFix.dll'
$fxBeforeId=[System.Reflection.Assembly]::LoadFile((Join-Path $fxBuild 'before.dll')).ManifestModule.ModuleVersionId.ToString()
$fxAfterId=[System.Reflection.Assembly]::LoadFile($fxAfter).ManifestModule.ModuleVersionId.ToString()
if($fxBeforeId -eq $fxAfterId){throw 'Reload fixture did not produce a new MVID'}
@{before_mvid=$fxBeforeId;after_mvid=$fxAfterId;before_sha256=(Get-FileHash $fxBefore).Hash.ToLowerInvariant();after_sha256=(Get-FileHash $fxAfter).Hash.ToLowerInvariant()} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $fxBuild 'identity.json')
& (Join-Path $fxRepo 'tools/LoadCheck/bin/LoadCheck.exe') $fxAfter
if($LASTEXITCODE -ne 0){throw 'Reload fixture LoadCheck failed'}
Get-Content (Join-Path $fxBuild 'identity.json')
