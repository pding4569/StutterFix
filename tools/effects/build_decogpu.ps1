param(
 [string]$Unity='D:/StutterFixTools/UnityEditors/6000.3.10f1/Editor/Unity.exe',
 [string]$Project="$env:TEMP/StutterFix-DecoGpu-6000.3.10f1"
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot/../..").Path
if(!(Test-Path -LiteralPath $Unity)){throw 'Unity Editor required; no automatic install'}
if(!(Test-Path -LiteralPath "$Project/ProjectSettings/ProjectVersion.txt")){
 New-Item -ItemType Directory -Force "$Project/Assets/DecoGpu","$Project/Assets/Editor","$Project/ProjectSettings","$Project/Packages" | Out-Null
 [IO.File]::WriteAllText("$Project/ProjectSettings/ProjectVersion.txt","m_EditorVersion: 6000.3.10f1`n",[Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText("$Project/Packages/manifest.json",'{"dependencies":{"com.unity.modules.assetbundle":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}}',[Text.UTF8Encoding]::new($false))
}
if((Get-Content -LiteralPath "$Project/ProjectSettings/ProjectVersion.txt" -Raw) -notmatch 'm_EditorVersion: 6000\.3\.10f1\s'){throw 'matching 6000.3.10f1 project required'}
New-Item -ItemType Directory -Force "$Project/Assets/DecoGpu","$Project/Assets/Editor" | Out-Null
Copy-Item -LiteralPath "$repo/effects/DecoGpu.shader" -Destination "$Project/Assets/DecoGpu/DecoGpu.shader"
Copy-Item -LiteralPath "$repo/tools/effects/BuildDecoGpu.cs.txt" -Destination "$Project/Assets/Editor/BuildDecoGpu.cs"
$started=Get-Date
$p=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-quit','-nographics','-projectPath',('"'+$Project+'"'),'-executeMethod','BuildDecoGpu.Build','-logFile',('"'+$Project+'/build.log"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){Get-Content -LiteralPath "$Project/build.log" -Tail 35;throw "bundle exit $($p.ExitCode)"}
$stamp=Get-Item -LiteralPath "$Project/DecoGpuOutput/build.txt"
if($stamp.LastWriteTime -lt $started -or (Get-Content -LiteralPath $stamp.FullName -Raw).Trim() -ne 'OK Unity 6000.3.10f1'){throw 'fresh output not verified'}
Copy-Item -LiteralPath "$Project/DecoGpuOutput/stutterfix_decogpu" -Destination "$repo/effects/stutterfix_decogpu"
Get-FileHash -LiteralPath "$repo/effects/stutterfix_decogpu" -Algorithm SHA256
