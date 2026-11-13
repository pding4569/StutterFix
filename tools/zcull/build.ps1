param([string]$Editor='E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'project'
foreach($dir in @('Assets/Editor','Packages','ProjectSettings','../bundle')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $project $dir) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BuildZCull.cs') -Destination (Join-Path $project 'Assets/Editor/BuildZCull.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ZCullGpuFixture.cs') -Destination (Join-Path $project 'Assets/Editor/ZCullGpuFixture.cs')
foreach($file in @('ZCull.shader','ZCullReduce.compute')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $project "Assets/$file")
}
Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Encoding ascii -Value 'm_EditorVersion: 6000.3.10f1'
Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Encoding ascii -Value '{"dependencies":{"com.unity.modules.assetbundle":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}}'
$log=Join-Path $PSScriptRoot 'bundle/editor.log'
$process=Start-Process -FilePath $Editor -ArgumentList @('-batchmode','-quit','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','BuildZCull.Bundle','-logFile',('"'+$log+'"')) -PassThru -WindowStyle Hidden
if(!$process.WaitForExit(300000)) { $process.Kill(); throw 'ZCull bundle build exceeded 5 minutes' }
if($process.ExitCode -ne 0) { throw "ZCull bundle failed; see $log" }
if(!(Select-String -LiteralPath $log -Pattern 'ZCULL_BUNDLE=PASS' -Quiet)) {throw "Bundle verification marker missing: $log"}
Write-Output "ZCULL_BUNDLE=PASS $PSScriptRoot/bundle/stutterfix_zcull"
