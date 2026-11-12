param([switch]$Smoke,[switch]$Controls)
$ErrorActionPreference='Stop'
$labRoot=$PSScriptRoot
$editor='E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe'
$evidence=Join-Path $labRoot 'evidence/composite-20261112a'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
Copy-Item -LiteralPath (Join-Path $labRoot 'CompositeBench.cs') -Destination (Join-Path $labRoot 'project/Assets/Editor/CompositeBench.cs')
foreach($asset in @('CompositeBench.shader','CompositeBench.compute')) {
    Copy-Item -LiteralPath (Join-Path $labRoot $asset) -Destination (Join-Path $labRoot "project/Assets/Shaders/$asset")
}
$tag=if($Controls){'controls'}elseif($Smoke){'smoke'}else{'full'}
$snapshot=Get-ChildItem -LiteralPath (Join-Path $labRoot '../..') -Filter '*.cs' -File | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash
$snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence "$tag-product-hashes-before.json") -Encoding utf8
$argsList=@('-batchmode','-quit','-force-d3d11','-projectPath',('"'+(Join-Path $labRoot 'project')+'"'),'-executeMethod','GpuDecoLab.CompositeBench','-logFile',('"'+(Join-Path $evidence "$tag-editor.log")+'"'))
if($Smoke){$argsList+='-compositeSmoke'}
if($Controls){$argsList[$argsList.IndexOf('GpuDecoLab.CompositeBench')]='GpuDecoLab.CompositeBenchControls'}
$proc=Start-Process -FilePath $editor -ArgumentList $argsList -WindowStyle Hidden -PassThru
Write-Output "CompositeBench $tag PID=$($proc.Id)"
$counter=0
while(-not $proc.HasExited) {
    $stamp=[DateTime]::UtcNow.ToString('O')
    $gpu=& nvidia-smi --query-gpu=utilization.gpu,temperature.gpu,clocks.gr,power.draw --format=csv,noheader,nounits 2>&1
    Add-Content -LiteralPath (Join-Path $evidence "$tag-gpu.log") -Value "$stamp GPU=$gpu"
    if($counter%6 -eq 0) {
        $engines=Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue
        $active=$engines.CounterSamples | Where-Object {$_.CookedValue -gt .1} | ForEach-Object {
            $enginePid=if($_.InstanceName -match 'pid_(\d+)'){$Matches[1]}else{'?'}
            $name=(Get-Process -Id $enginePid -ErrorAction SilentlyContinue).ProcessName
            "$enginePid/$name/$($_.InstanceName)=$([Math]::Round($_.CookedValue,3))"
        }
        Add-Content -LiteralPath (Join-Path $evidence "$tag-gpu-processes.log") -Value "$stamp $($active -join '; ')"
    }
    Start-Sleep -Seconds 5
    $proc.Refresh();$counter++
}
$proc.WaitForExit()
$after=Get-ChildItem -LiteralPath (Join-Path $labRoot '../..') -Filter '*.cs' -File | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash
$after | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence "$tag-product-hashes-after.json") -Encoding utf8
$diff=Compare-Object $snapshot $after -Property Path,Hash
if($diff){throw 'Product C# changed during benchmark; see snapshots'}
Write-Output "CompositeBench $tag exit=$($proc.ExitCode) product_hashes_unchanged=true"
if($proc.ExitCode -ne 0){exit $proc.ExitCode}
