param([switch]$Game)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot\..\..").Path
if($Game) {
    $base=Join-Path $PSScriptRoot 'out\ingame-native'
    $needed=@('sfnative.obj','sfinflate.obj','cpu_features.obj','utils.obj','sfdxt.obj','sfdxt_sse2.obj','sfdxt_sse4.obj','sfdxt_avx2.obj')
    if(@($needed | Where-Object { !(Test-Path -LiteralPath (Join-Path $base $_)) }).Count) {
        & "$PSScriptRoot\build_ingame_native.ps1"
    }
}
$vs=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$msvc=(Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdki='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0'
$sdkl='C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c'
$out=Join-Path $PSScriptRoot 'out\outside-native'
New-Item -ItemType Directory -Force $out | Out-Null
$oldInclude=$env:INCLUDE; $oldLib=$env:LIB; $oldPath=$env:PATH
Push-Location $out
try {
    $env:INCLUDE="$msvc\include;$sdki\ucrt;$sdki\um;$sdki\shared;$sdki\winrt"
    $env:LIB="$msvc\lib\x64;$sdkl\ucrt\x64;$sdkl\um\x64"
    $env:PATH="$msvc\bin\Hostx64\x64;$oldPath"
    & cl.exe /nologo /std:c++17 /EHsc /O2 /MT /Brepro /W4 /WX "$PSScriptRoot\outside_lab.cpp" /Fe:OutsideLab.exe d3d11.lib dxgi.lib d3dcompiler.lib user32.lib
    if($LASTEXITCODE -ne 0) { throw 'Outside lab failed' }
    if($Game) {
        & cl.exe /nologo /std:c++17 /EHsc /c /O2 /MT /Brepro /W4 /WX "$PSScriptRoot\native\outside_game.cpp"
        if($LASTEXITCODE -ne 0) { throw 'Outside game native failed' }
        $base=Join-Path $PSScriptRoot 'out\ingame-native'
        & link.exe /nologo /DLL /Brepro /OUT:sfnative.dll "$base\sfnative.obj" "$base\sfinflate.obj" "$base\cpu_features.obj" "$base\utils.obj" "$base\sfdxt.obj" "$base\sfdxt_sse2.obj" "$base\sfdxt_sse4.obj" "$base\sfdxt_avx2.obj" outside_game.obj d3d11.lib dxgi.lib d3dcompiler.lib user32.lib
        if($LASTEXITCODE -ne 0) { throw 'Outside game native link failed' }
    }
} finally { Pop-Location; $env:INCLUDE=$oldInclude; $env:LIB=$oldLib; $env:PATH=$oldPath }
