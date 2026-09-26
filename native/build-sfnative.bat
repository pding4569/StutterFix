@echo off
rem Builds native\sfnative.dll from native\sfnative\sfnative.c (StutterFix's own code: PNG unfilter + DXT encoder, x64, static CRT).
rem Same toolchain as build-libdeflate.bat. /Brepro makes the output identical on every build.
rem Usage:   build-sfnative.bat MSVC_DIR SDK_INCLUDE_DIR SDK_X64_LIB_DIR
rem Example: build-sfnative.bat "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207" C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0 C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c
setlocal
set "MSVC=%~1"
set "SDKI=%~2"
set "SDKL=%~3"
set "PATH=%MSVC%\bin\Hostx64\x64;%PATH%"
set "INCLUDE=%MSVC%\include;%SDKI%\ucrt;%SDKI%\um;%SDKI%\shared"
set "LIB=%MSVC%\lib\x64;%SDKL%\ucrt\x64;%SDKL%\um\x64"
set "OUT=%TEMP%\sfnative_build"
if not exist "%OUT%" mkdir "%OUT%"
cd /d "%OUT%"
if exist sfnative.dll del sfnative.dll
cl /nologo /O2 /GL /MT /LD /Brepro /W3 "%~dp0sfnative\sfnative.c" /Fe:sfnative.dll /link /LTCG /Brepro
if errorlevel 1 exit /b 1
copy /y sfnative.dll "%~dp0sfnative.dll" >nul
echo OK %~dp0sfnative.dll
