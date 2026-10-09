param([string]$Unity='D:/StutterFixTools/UnityEditors/6000.3.10f1/Editor/Unity.exe', [string]$Project='C:/SFBundle/Fsr3')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot/../..").Path
if(!(Test-Path -LiteralPath $Unity)){throw 'Unity6000.3.10f1 is required; no automatic installation'}
if((Get-Content -LiteralPath "$Project/ProjectSettings/ProjectVersion.txt" -Raw) -notmatch 'm_EditorVersion: 6000\.3\.10f1\s') {throw 'Use the matching6000.3.10f1 empty project'}
$out=Join-Path $PSScriptRoot 'out/bundle';New-Item -ItemType Directory -Force $out | Out-Null
New-Item -ItemType Directory -Force "$Project/Assets/Effects","$Project/Assets/Editor" | Out-Null
Copy-Item -LiteralPath "$repo/effects/ScreenEffects.shader" -Destination "$Project/Assets/Effects/ScreenEffects.shader"
Copy-Item -LiteralPath "$repo/effects/BuildEffects.cs.txt" -Destination "$Project/Assets/Editor/BuildEffects.cs"
$log=Join-Path $out 'build.log';$started=Get-Date
$buildProcess=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-quit','-nographics','-projectPath',('"'+$Project+'"'),'-executeMethod','BuildEffects.Build','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru -Wait
if($buildProcess.ExitCode -ne 0){Get-Content -LiteralPath $log -Tail 35;throw "Effects bundle exit $($buildProcess.ExitCode)"}
$stamp=Get-Item -LiteralPath "$Project/EffectsOutput/build.txt"
if($stamp.LastWriteTime -lt $started -or (Get-Content $stamp.FullName -Raw).Trim() -ne 'OK Unity 6000.3.10f1'){throw 'Fresh matching-version output not confirmed'}
Copy-Item -LiteralPath "$Project/EffectsOutput/stutterfix_effects" -Destination "$repo/effects/stutterfix_effects"
Get-FileHash -LiteralPath "$repo/effects/stutterfix_effects" -Algorithm SHA256
