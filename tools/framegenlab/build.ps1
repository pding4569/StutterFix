param(
    [string]$MsvcRoot = '',
    [string]$SdkInclude = 'C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0',
    [string]$SdkLib = 'C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c'
)
$ErrorActionPreference = 'Stop'
if (!$MsvcRoot) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (!$vs) { throw 'MSVC x64 tools required' }
    $MsvcRoot = (Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
}
if (!(Test-Path "$SdkInclude\um\d3d11.h") -or !(Test-Path "$SdkLib\um\x64\d3d11.lib")) {
    throw 'Pass -SdkInclude and -SdkLib for your Windows SDK'
}
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $out | Out-Null
$savedInclude = $env:INCLUDE; $savedLib = $env:LIB; $savedPath = $env:PATH
try {
    $env:INCLUDE = "$MsvcRoot\include;$SdkInclude\ucrt;$SdkInclude\um;$SdkInclude\shared;$SdkInclude\winrt"
    $env:LIB = "$MsvcRoot\lib\x64;$SdkLib\ucrt\x64;$SdkLib\um\x64"
    $env:PATH = "$MsvcRoot\bin\Hostx64\x64;$savedPath"
    & "$MsvcRoot\bin\Hostx64\x64\cl.exe" /nologo /std:c++17 /EHsc /O2 /MT /W4 /WX /Brepro "$PSScriptRoot\main.cpp" "/Fo$out\main.obj" "/Fe$out\FrameGenLab.exe" d3d11.lib dxgi.lib d3dcompiler.lib user32.lib gdi32.lib /link /Brepro
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    Copy-Item -LiteralPath "$PSScriptRoot\scene.hlsl" -Destination $out
} finally { $env:INCLUDE = $savedInclude; $env:LIB = $savedLib; $env:PATH = $savedPath }
