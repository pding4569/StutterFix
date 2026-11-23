$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $workspace 'out/hitmeter-alpha-lab'
$editor = 'E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe'
New-Item -ItemType Directory -Force "$project/Assets/Editor", "$project/Packages", "$project/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/HitMeterAlpha.shader" -Destination "$project/Assets/HitMeterAlpha.shader"
Copy-Item -LiteralPath "$PSScriptRoot/Build.cs" -Destination "$project/Assets/Editor/Build.cs"
Set-Content -LiteralPath "$project/Packages/manifest.json" -Encoding utf8 -Value '{"dependencies":{"com.unity.modules.assetbundle":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}}'
Set-Content -LiteralPath "$project/ProjectSettings/ProjectVersion.txt" -Encoding utf8 -Value 'm_EditorVersion: 6000.3.10f1'
$process = Start-Process -FilePath $editor -ArgumentList @('-batchmode', '-quit', '-force-d3d11', '-noUpm', '-projectPath', $project, '-executeMethod', 'HitMeterAlphaBuild.Run', '-logFile', "$project/editor-build.log") -PassThru -WindowStyle Hidden
if (!$process.WaitForExit(180000)) {
    $process.Kill() # Only this script's own batch Editor, never the game or shared licensing client.
    throw "Unity batch Editor timed out. See $project/editor-build.log"
}
if ($process.ExitCode -ne 0) { throw "Unity bundle build failed: $($process.ExitCode). See $project/editor-build.log" }
Copy-Item -LiteralPath "$project/bundle/hitmeteralpha.bundle" -Destination "$PSScriptRoot/hitmeteralpha.bundle"
Get-FileHash -LiteralPath "$PSScriptRoot/hitmeteralpha.bundle" -Algorithm SHA256
