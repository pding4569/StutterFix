param([switch]$ReuseBase)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot\..\..").Path
$vs=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc=(Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0'
$sdkl='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c'
$ispc='C:\SFBundle\tools\ispc-v1.31.0-windows\bin\ispc.exe'
$out=Join-Path $PSScriptRoot 'out\ingame-native'
New-Item -ItemType Directory -Force $out | Out-Null
$oldInclude=$env:INCLUDE; $oldLib=$env:LIB; $oldPath=$env:PATH
Push-Location $out
try {
    $env:INCLUDE="$msvc\include;$sdki\ucrt;$sdki\um;$sdki\shared;$sdki\winrt"
    $env:LIB="$msvc\lib\x64;$sdkl\ucrt\x64;$sdkl\um\x64"
    $env:PATH="$msvc\bin\Hostx64\x64;$oldPath"
    if (!$ReuseBase) {
    & $ispc "$repo\native\sfnative\sfdxt.ispc" -O2 --arch=x86-64 --target-os=windows --target=sse2-i32x4,sse4.1-i32x4,avx2-i32x8 --opt=disable-fma -o sfdxt.obj *> ispc-build.log
    if($LASTEXITCODE -ne 0) { throw 'ISPC failed' }
    & cl.exe /nologo /c /O2 /MT /Brepro /W3 "$repo\native\sfnative\sfnative.c" "$repo\native\sfnative\sfinflate.c" "$repo\native\sfnative\libdeflate\lib\x86\cpu_features.c" "$repo\native\sfnative\libdeflate\lib\utils.c"
    if($LASTEXITCODE -ne 0) { throw 'Base sfnative failed' }
    }
    & cl.exe /nologo /std:c++17 /EHsc /c /O2 /MT /Brepro /W4 /WX "$PSScriptRoot\native\framegen.cpp"
    if($LASTEXITCODE -ne 0) { throw 'Framegen native failed' }
    & link.exe /nologo /DLL /Brepro /OUT:sfnative.dll sfnative.obj sfinflate.obj cpu_features.obj utils.obj sfdxt.obj sfdxt_sse2.obj sfdxt_sse4.obj sfdxt_avx2.obj framegen.obj d3d11.lib dxgi.lib d3dcompiler.lib user32.lib
    if($LASTEXITCODE -ne 0) { throw 'Link failed' }
    Write-Output "Research-only $out\sfnative.dll; normal native/sfnative.dll untouched"
} finally { Pop-Location; $env:INCLUDE=$oldInclude; $env:LIB=$oldLib; $env:PATH=$oldPath }
