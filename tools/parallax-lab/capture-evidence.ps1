$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$evidence = Join-Path $root 'evidence'
$repo = (Resolve-Path (Join-Path $root '../..')).Path
$managed = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$types = @('scrParallax', 'scrDecoration', 'scrVisualDecoration', 'scrDecorationManager',
           'scrCamera', 'ffxMoveDecorationsPlus', 'ADOFAI.LevelEvent', 'ADOFAI.LevelEventInfo')
foreach ($type in $types) {
    ilspycmd --disable-updatecheck -t $type (Join-Path $managed 'Assembly-CSharp.dll') |
        Set-Content -LiteralPath (Join-Path $evidence ($type+'.cs.txt')) -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "ILSpy failed: $type" }
}
foreach ($type in @('UnityEngine.Vector2','UnityEngine.Vector3')) {
    ilspycmd --disable-updatecheck -t $type (Join-Path $managed 'UnityEngine.CoreModule.dll') |
        Set-Content -LiteralPath (Join-Path $evidence ($type+'.cs.txt')) -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "ILSpy failed: $type" }
}
$sources = @('FastMove.cs','DecoEngine.cs','TightLate.cs','MoveApply.cs','GpuDecoExport.cs','GpuDecoGpu.cs','GpuDecoScene.cs','tools/gpudeco-lab/GpuDeco.compute')
$sources += Get-ChildItem -LiteralPath $repo -File | Where-Object { $_.Name -like 'DecoAnim*.cs' -or $_.Name -like 'DecoBake*.cs' -or $_.Name -like 'GpuDeco*.cs' } | ForEach-Object Name
$hashes = @($sources | Sort-Object -Unique | ForEach-Object {
    [pscustomobject]@{File=$_;Sha256=(Get-FileHash -LiteralPath (Join-Path $repo $_) -Algorithm SHA256).Hash}
})
$dlls = @('Assembly-CSharp.dll','UnityEngine.CoreModule.dll') | ForEach-Object {
    $p = Join-Path $managed $_
    [pscustomobject]@{File=$p;Sha256=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash;Bytes=(Get-Item -LiteralPath $p).Length}
}
[pscustomobject]@{
    CapturedUtc=[DateTime]::UtcNow.ToString('o')
    Head=(git -C $repo rev-parse HEAD)
    Branch=(git -C $repo branch --show-current)
    Ilspy=(ilspycmd --version)
    Dlls=$dlls
    Sources=$hashes
    ToolSources=@(Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Extension -in '.cs','.csproj','.py','.ps1','.Config' } | ForEach-Object {
        [pscustomobject]@{File=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'provenance.json') -Encoding utf8
