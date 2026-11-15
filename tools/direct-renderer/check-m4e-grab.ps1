param([string]$OutputDirectory = 'out/direct-renderer/m4e')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo $OutputDirectory
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc = (Get-ChildItem "$vs/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp/c/Include/10.0.26100.0'
$sdkl = 'C:/SFBundle/winsdk/microsoft.windows.sdk.cpp.x64/c'
$env:INCLUDE = "$msvc/include;$sdki/ucrt;$sdki/um;$sdki/shared;$sdki/winrt"
$env:LIB = "$msvc/lib/x64;$sdkl/ucrt/x64;$sdkl/um/x64"
$env:PATH = "$msvc/bin/Hostx64/x64;$env:PATH"
Push-Location $out
try {
    $ErrorActionPreference = 'Continue'
    & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /fp:strict /MT /W4 /WX "$repo/tools/direct-renderer/drfull-warp.cpp" "$repo/native/sfnative/drfull.cpp" /Fe:grab-warp.exe /link d3d11.lib d3dcompiler.lib dxgi.lib > build-grab.log 2>&1
    if ($LASTEXITCODE) { throw 'grab WARP build failed' }
    & ./grab-warp.exe grab-cases.bin "$repo/tools/direct-renderer/drfull-reference.hlsl" grab > grab.log 2>&1
    if ($LASTEXITCODE) { Get-Content grab.log; throw 'grab WARP failed' }
    Get-Content grab.log
} finally { Pop-Location }
