$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$out=Join-Path $root 'out/gpuprobe-safe'
if(Get-Process -Name 'A Dance of Fire and Ice' -ErrorAction SilentlyContinue){throw 'Do not build during a game run'}
& py -3.12 "$PSScriptRoot/prepare-gpuprobe-safe.py"
if($LASTEXITCODE){throw 'query preparation failed'}
$vs=& "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc=(Get-ChildItem "$vs/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki='C:/SFBundle/winsdk/microsoft.windows.sdk.cpp/c/Include/10.0.26100.0'
$sdkl='C:/SFBundle/winsdk/microsoft.windows.sdk.cpp.x64/c'
$env:INCLUDE="$msvc/include;$sdki/ucrt;$sdki/um;$sdki/shared;$sdki/winrt"
$env:LIB="$msvc/lib/x64;$sdkl/ucrt/x64;$sdkl/um/x64"
Push-Location $out
try{
 & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /LD /O2 /MT query.cpp /Fegpuprobe.dll d3d11.lib
 if($LASTEXITCODE){throw 'native plugin build failed'}
 & "$msvc/bin/Hostx64/x64/cl.exe" /nologo /std:c++17 /EHsc /O2 /MT "$PSScriptRoot/gpuprobe-safe-check.cpp" /Focheck.obj /Fecheck.exe d3d11.lib
 if($LASTEXITCODE){throw 'native fixture build failed'}
 & ./check.exe
 if($LASTEXITCODE){throw 'native fixture failed'}
 @{ passed=$true; fixture='17 completed frames; blocked publication, invalid scope, reset and active release';
    plugin_sha256=(Get-FileHash ./gpuprobe.dll -Algorithm SHA256).Hash;
    source_sha256=(Get-FileHash ./query.cpp -Algorithm SHA256).Hash;
    scope='diagnostic correctness; not game performance or physical display latency' } |
    ConvertTo-Json | Set-Content -Encoding UTF8 ./check-passed.json
}finally{Pop-Location}
