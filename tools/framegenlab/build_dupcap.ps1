$ErrorActionPreference='Stop'
$vs=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc=(Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0'
$sdkl='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c'
$out=Join-Path $PSScriptRoot 'out\dupcap'
New-Item -ItemType Directory -Force $out | Out-Null
$oldInclude=$env:INCLUDE; $oldLib=$env:LIB; $oldPath=$env:PATH
Push-Location $out
try {
    $env:INCLUDE="$msvc\include;$sdki\ucrt;$sdki\um;$sdki\shared;$sdki\winrt"
    $env:LIB="$msvc\lib\x64;$sdkl\ucrt\x64;$sdkl\um\x64"
    $env:PATH="$msvc\bin\Hostx64\x64;$oldPath"
    & cl.exe /nologo /std:c++17 /EHsc /O2 /MT /W3 "$PSScriptRoot\dupcap.cpp" /Fe:dupcap.exe d3d11.lib dxgi.lib user32.lib
    if($LASTEXITCODE -ne 0) { throw 'dupcap build failed' }
} finally { Pop-Location; $env:INCLUDE=$oldInclude; $env:LIB=$oldLib; $env:PATH=$oldPath }
