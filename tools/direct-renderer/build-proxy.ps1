param([string]$Unity='D:/StutterFixTools/UnityEditors/6000.3.10f1/Editor/Unity.exe', [string]$Project="$env:TEMP/StutterFix-DirectProxy-6000.3.10f1", [switch]$RunFixture)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot/../..").Path
if(!(Test-Path -LiteralPath $Unity)){throw 'Unity 6000.3.10f1 Editor required'}
New-Item -ItemType Directory -Force "$Project/Assets/DirectDeco","$Project/Assets/Editor","$Project/ProjectSettings","$Project/Packages" | Out-Null
if(Test-Path -LiteralPath "$Project/ProjectSettings/ProjectVersion.txt") {
 if((Get-Content -LiteralPath "$Project/ProjectSettings/ProjectVersion.txt" -Raw) -notmatch 'm_EditorVersion: 6000\.3\.10f1\s'){throw 'Unity version mismatch'}
} else {
 [IO.File]::WriteAllText("$Project/ProjectSettings/ProjectVersion.txt","m_EditorVersion: 6000.3.10f1`n")
 [IO.File]::WriteAllText("$Project/Packages/manifest.json",'{"dependencies":{"com.unity.modules.assetbundle":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}}')
}
& py -3.12 "$PSScriptRoot/generate-proxy-shader.py"
if($LASTEXITCODE){throw 'shader generation failed'}
Get-ChildItem -LiteralPath "$repo/effects/direct-deco" -File | Where-Object Extension -in '.shader','.cginc','.hlsli' | Copy-Item -Destination "$Project/Assets/DirectDeco"
Copy-Item -LiteralPath "$PSScriptRoot/BuildProxy.cs.txt" -Destination "$Project/Assets/Editor/BuildProxy.cs"
Copy-Item -LiteralPath "$PSScriptRoot/ProxyFixture.cs.txt" -Destination "$Project/Assets/Editor/ProxyFixture.cs"
$started=Get-Date
$p=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-quit','-nographics','-projectPath',('"'+$Project+'"'),'-executeMethod','BuildProxy.Build','-logFile',('"'+$Project+'/build.log"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){Get-Content -LiteralPath "$Project/build.log" -Tail 60;throw "proxy bundle failed: $($p.ExitCode)"}
$stamp=Get-Item -LiteralPath "$Project/ProxyOutput/build.txt"
if($stamp.LastWriteTime -lt $started -or (Get-Content -LiteralPath $stamp.FullName -Raw).Trim() -ne 'OK Unity 6000.3.10f1'){throw 'fresh bundle not verified'}
Copy-Item -LiteralPath "$Project/ProxyOutput/stutterfix_directdeco" -Destination "$repo/effects/direct-deco/stutterfix_directdeco"
Get-FileHash -LiteralPath "$repo/effects/direct-deco/stutterfix_directdeco"
if($RunFixture) {
 $started=Get-Date
 $p=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-quit','-force-d3d11','-projectPath',('"'+$Project+'"'),'-executeMethod','ProxyFixture.Run','-logFile',('"'+$Project+'/fixture.log"')) -WindowStyle Hidden -PassThru -Wait
 if($p.ExitCode -ne 0){Get-Content -LiteralPath "$Project/fixture.log" -Tail 60;throw "proxy fixture failed: $($p.ExitCode)"}
 if((Get-Item -LiteralPath "$Project/ProxyOutput/fixture.txt").LastWriteTime -lt $started){throw 'stale fixture output'}
 New-Item -ItemType Directory -Force "$repo/out/direct-renderer/m3" | Out-Null
 Copy-Item -LiteralPath "$Project/fixture.log" -Destination "$repo/out/direct-renderer/m3/unity-fixture.log"
 Copy-Item -LiteralPath "$Project/ProxyOutput/fixture.txt" -Destination "$repo/out/direct-renderer/m3/unity-fixture.txt"
 Get-Content -LiteralPath "$Project/ProxyOutput/fixture.txt"
}
