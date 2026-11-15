$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo 'out/direct-renderer/m4g/baseline'
New-Item -ItemType Directory -Force $out | Out-Null
$process=[System.Diagnostics.Process]::GetCurrentProcess();$priority=$process.PriorityClass;$process.PriorityClass='BelowNormal'
Push-Location $repo
try {
    # Build HEAD in an isolated output directory. Never change the checkout or DLLs.
    $utf8=[Text.UTF8Encoding]::new($false)
    foreach($path in @('native/sfnative/drfull.cpp','native/sfnative/drfull.h','tools/direct-renderer/drfull-warp.cpp')) {
        $content=(& git show "HEAD:$path") -join "`n"
        if($LASTEXITCODE){throw "git show failed: $path"}
        $content=$content.Replace('../../native/sfnative/drfull.h','drfull.h')
        [IO.File]::WriteAllText((Join-Path $out ([IO.Path]::GetFileName($path))),$content,$utf8)
    }
    $vswhere="${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
    $vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $msvc=(Get-ChildItem "$vs/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
    $sdki='C:/SFBundle/winsdk/microsoft.windows.sdk.cpp/c/Include/10.0.26100.0';$sdkl='C:/SFBundle/winsdk/microsoft.windows.sdk.cpp.x64/c'
    $env:INCLUDE="$msvc/include;$sdki/ucrt;$sdki/um;$sdki/shared;$sdki/winrt"
    $env:LIB="$msvc/lib/x64;$sdkl/ucrt/x64;$sdkl/um/x64";$env:PATH="$msvc/bin/Hostx64/x64;$env:PATH"
    Push-Location $out
    try {
        & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /fp:strict /MT /W4 /WX drfull-warp.cpp drfull.cpp /Fe:baseline-warp.exe /link d3d11.lib d3dcompiler.lib dxgi.lib > build.log 2>&1
        if($LASTEXITCODE){throw 'Baseline WARP build failed'}
        & ./baseline-warp.exe "$repo/out/direct-renderer/m4bc/warp-cases.bin" "$repo/tools/direct-renderer/drfull-reference.hlsl" "$out/warp" > warp.log 2>&1
        if($LASTEXITCODE){throw 'Baseline WARP failed'}
        Get-Content warp.log | Select-String 'submission|moving|PASS cases'
    } finally {Pop-Location}
} finally {Pop-Location;$process.PriorityClass=$priority}
