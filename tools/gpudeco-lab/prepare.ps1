param([string]$GameManaged = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed')
$ErrorActionPreference = 'Stop'
$labRoot = $PSScriptRoot
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $labRoot '../..')).Path
$projectRoot = Join-Path $labRoot 'project'
foreach ($dir in @('Assets/Editor','Assets/Shaders','Assets/LocalReferences','ProjectSettings','Packages')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot $dir) | Out-Null
}
foreach ($file in @('DecoBake.Core.cs','GpuDecoData.cs','GpuDecoGpu.cs')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination (Join-Path $projectRoot "Assets/Editor/$file")
}
$coreSource = Get-Content -LiteralPath (Join-Path $repoRoot 'DecoBake.Core.cs') -Raw
Set-Content -LiteralPath (Join-Path $projectRoot 'Assets/Editor/DecoBake.Core.cs') -Value ("#define DECOBAKE_FIXTURE`n" + $coreSource) -Encoding utf8
Copy-Item -LiteralPath (Join-Path $labRoot 'GpuDecoLab.cs') -Destination (Join-Path $projectRoot 'Assets/Editor/GpuDecoLab.cs')
Copy-Item -LiteralPath (Join-Path $labRoot 'GpuDecoSubmeshes.cs') -Destination (Join-Path $projectRoot 'Assets/Editor/GpuDecoSubmeshes.cs')
Copy-Item -LiteralPath (Join-Path $labRoot 'GpuDecoAudit.cs') -Destination (Join-Path $projectRoot 'Assets/Editor/GpuDecoAudit.cs')
Copy-Item -LiteralPath (Join-Path $labRoot 'GpuDeco.compute') -Destination (Join-Path $projectRoot 'Assets/Shaders/GpuDeco.compute')
# Local read-only CPU oracle. Generated directory is ignored and never packaged.
Copy-Item -LiteralPath (Join-Path $GameManaged 'DOTween.dll') -Destination (Join-Path $projectRoot 'Assets/LocalReferences/DOTween.dll')
Set-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Value "m_EditorVersion: 6000.3.10f1`nm_EditorVersionWithRevision: 6000.3.10f1 (e35f0c77bd8e)" -Encoding ascii
Set-Content -LiteralPath (Join-Path $projectRoot 'Packages/manifest.json') -Value '{"dependencies":{"com.unity.modules.imageconversion":"1.0.0","com.unity.modules.assetbundle":"1.0.0"}}' -Encoding ascii
Write-Output "Prepared independent lab: $projectRoot"
