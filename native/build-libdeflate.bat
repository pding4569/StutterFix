@echo off
rem Rebuilds native\libdeflate.dll from the official source (decompression only, x64, static CRT).
rem   Source:   https://github.com/ebiggers/libdeflate  tag v1.24 (commit 96836d7d9d10e3e0d53e6edb54eb908514e336c4)
rem   Compiler: Visual Studio 2022 MSVC 14.44 (x64)
rem   Windows SDK: an installed SDK, or the NuGet packages Microsoft.Windows.SDK.CPP and
rem                Microsoft.Windows.SDK.CPP.x64 (10.0.26100.4188) extracted to a folder
rem Usage:   build-libdeflate.bat SRC_DIR MSVC_DIR SDK_INCLUDE_DIR SDK_X64_LIB_DIR
rem Example: build-libdeflate.bat C:\SFBundle\libdeflate "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207" C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0 C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c
rem /Brepro makes the output identical on every build (no timestamps), so the shipped DLL can be re-created bit for bit.
rem (ASCII only: cmd reads .bat files in the console code page)
setlocal
set "SRC=%~1"
set "MSVC=%~2"
set "SDKI=%~3"
set "SDKL=%~4"
set "PATH=%MSVC%\bin\Hostx64\x64;%PATH%"
set "INCLUDE=%MSVC%\include;%SDKI%\ucrt;%SDKI%\um;%SDKI%\shared"
set "LIB=%MSVC%\lib\x64;%SDKL%\ucrt\x64;%SDKL%\um\x64"
cd /d "%SRC%"
if exist libdeflate.dll del libdeflate.dll
cl /nologo /O2 /GL /MT /LD /Brepro /DLIBDEFLATE_DLL /I. lib\deflate_decompress.c lib\zlib_decompress.c lib\adler32.c lib\utils.c lib\x86\cpu_features.c /Fe:libdeflate.dll /link /LTCG /Brepro
if errorlevel 1 exit /b 1
copy /y libdeflate.dll "%~dp0libdeflate.dll" >nul
echo OK %~dp0libdeflate.dll
