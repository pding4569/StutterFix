param([switch]$CompositeOnly)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4i'
$build = Join-Path $out 'warp-build'
New-Item -ItemType Directory -Force $build | Out-Null
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc = (Get-ChildItem "$vs/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp/c/Include/10.0.26100.0'
$sdkl = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp.x64/c'
$env:INCLUDE = "$msvc/include;$sdki/ucrt;$sdki/um;$sdki/shared;$sdki/winrt"
$env:LIB = "$msvc/lib/x64;$sdkl/ucrt/x64;$sdkl/um/x64"
$env:PATH = "$msvc/bin/Hostx64/x64;$env:PATH"
$process = [System.Diagnostics.Process]::GetCurrentProcess(); $priority = $process.PriorityClass
$process.PriorityClass = 'BelowNormal'
Push-Location $build
try {
    $fixtures = @('drfull-m4d-warp')
    if (!$CompositeOnly) { $fixtures += 'drfull-warp' }
    foreach ($fixture in $fixtures) {
        & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /fp:strict /MT /W4 /WX "$repo/tools/direct-renderer/$fixture.cpp" "$repo/native/sfnative/drfull.cpp" "/Fe:$fixture.exe" /link d3d11.lib d3dcompiler.lib dxgi.lib *> "$out/build-$fixture.log"
        if ($LASTEXITCODE) { throw "$fixture build failed" }
    }
    & "$build/drfull-m4d-warp.exe" "$out/composite" *> "$out/composite.log"
    if ($LASTEXITCODE) { throw 'M4h compositor/rounding controls failed' }
    Get-Content "$out/composite.log"
    if (!$CompositeOnly) {
        & py -3.12 "$repo/tools/direct-renderer/make-m4e-grab-cases.py" $out
        if ($LASTEXITCODE) { throw 'Grab cases failed' }
        & py -3.12 "$repo/tools/direct-renderer/make-m4e-grab-cases.py" $out --named
        if ($LASTEXITCODE) { throw 'Named Grab cases failed' }
        & py -3.12 "$repo/tools/direct-renderer/make-warp-cases.py" --default
        if ($LASTEXITCODE) { throw 'Default cases failed' }
        foreach ($pair in @(@('grab', "$out/grab-cases.bin"), @('named-grab', "$out/named-grab-cases.bin"), @('default', "$repo/out/direct-renderer/m4f/default-cases.bin"))) {
            & "$build/drfull-warp.exe" $pair[1] "$repo/tools/direct-renderer/drfull-reference.hlsl" "$out/$($pair[0])-warp" *> "$out/$($pair[0]).log"
            if ($LASTEXITCODE) { throw "$($pair[0]) WARP failed" }
            Get-Content "$out/$($pair[0]).log" -Tail 1
        }
    }
} finally { Pop-Location; $process.PriorityClass = $priority }

