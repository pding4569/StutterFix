param()
$ErrorActionPreference='Stop'
$vs=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc=(Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0'
$sdkl='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c'
$env:INCLUDE="$msvc\include;$sdki\ucrt;$sdki\um;$sdki\shared;$sdki\winrt"
$env:LIB="$msvc\lib\x64;$sdkl\ucrt\x64;$sdkl\um\x64"
& "$msvc\bin\Hostx64\x64\cl.exe" /nologo /std:c++17 /EHsc /LD /O2 /MT "$PSScriptRoot/query.cpp" "/Fo$PSScriptRoot/query.obj" "/Fe$PSScriptRoot/filtergpu.dll" d3d11.lib
if($LASTEXITCODE){throw 'native build failed'}

& "$msvc\bin\Hostx64\x64\cl.exe" /nologo /std:c++17 /EHsc /O2 /MT "$PSScriptRoot/check.cpp" "/Fo$PSScriptRoot/check.obj" "/Fe$PSScriptRoot/check.exe" d3d11.lib
if($LASTEXITCODE){throw "native check build failed"}
