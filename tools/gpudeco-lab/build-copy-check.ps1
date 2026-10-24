param(
    [string]$MsvcRoot='',
    [string]$SdkInclude='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0',
    [string]$SdkLib='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c',
    [switch]$Masked
)
$ErrorActionPreference='Stop'
if(!$MsvcRoot) {
    $tool="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs=& $tool -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if(!$vs){throw 'MSVC x64 tools required'}
    $MsvcRoot=(Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
}
if(!(Test-Path -LiteralPath "$SdkInclude\um\d3d11.h")){throw 'Windows SDK required'}
$taskOut=Join-Path $PSScriptRoot '../optimization/out/gpudeco-copy-check'
New-Item -ItemType Directory -Force -Path $taskOut | Out-Null
$taskInclude=$env:INCLUDE;$taskLib=$env:LIB;$taskPath=$env:PATH
try {
    $env:INCLUDE="$MsvcRoot\include;$SdkInclude\ucrt;$SdkInclude\um;$SdkInclude\shared;$SdkInclude\winrt"
    $env:LIB="$MsvcRoot\lib\x64;$SdkLib\ucrt\x64;$SdkLib\um\x64"
    $env:PATH="$MsvcRoot\bin\Hostx64\x64;$taskPath"
    $taskName=if($Masked){'mask-check'}else{'copy-check'}
    $taskSource=if($Masked){'native-mask-check.cpp'}else{'native-copy-check.cpp'}
    & "$MsvcRoot\bin\Hostx64\x64\cl.exe" /nologo /std:c++17 /EHsc /O2 /MT /W4 /WX /Brepro "$PSScriptRoot\$taskSource" "/Fo$taskOut\$taskName.obj" "/Fe$taskOut\$taskName.exe" d3d11.lib d3dcompiler.lib dxgi.lib /link /Brepro
    if($LASTEXITCODE -ne 0){throw "Build failed: $LASTEXITCODE"}
} finally {$env:INCLUDE=$taskInclude;$env:LIB=$taskLib;$env:PATH=$taskPath}
