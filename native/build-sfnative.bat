@echo off
rem Builds native\sfnative.dll from native\sfnative\sfnative.c + sfdxt.ispc (StutterFix's own code: PNG unfilter + DXT encoder, x64, static CRT).
rem Same toolchain as build-libdeflate.bat, plus the Intel ISPC compiler for the DXT encoder:
rem   ISPC https://github.com/ispc/ispc v1.31.0 (ispc-v1.31.0-windows.zip, BSD-3-Clause)
rem   Targets SSE2 / SSE4.1 / AVX2 in one DLL; the ISPC dispatcher picks the best one for the CPU at run time.
rem   --opt=disable-fma keeps the float rounding of the C code, so the output is byte-identical to the scalar encoder.
rem /Brepro makes the output identical on every build.
rem Usage:   build-sfnative.bat MSVC_DIR SDK_INCLUDE_DIR SDK_X64_LIB_DIR ISPC_BIN_DIR
rem Example: build-sfnative.bat "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207" C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0 C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c C:\SFBundle\tools\ispc-v1.31.0-windows\bin
rem (ASCII only: cmd reads .bat files in the console code page)
setlocal
set "MSVC=%~1"
set "SDKI=%~2"
set "SDKL=%~3"
set "PATH=%MSVC%\bin\Hostx64\x64;%~4;%PATH%"
set "INCLUDE=%MSVC%\include;%SDKI%\ucrt;%SDKI%\um;%SDKI%\shared"
set "LIB=%MSVC%\lib\x64;%SDKL%\ucrt\x64;%SDKL%\um\x64"
set "OUT=%TEMP%\sfnative_build"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"
cd /d "%OUT%"
ispc "%~dp0sfnative\sfdxt.ispc" -O2 --arch=x86-64 --target-os=windows --target=sse2-i32x4,sse4.1-i32x4,avx2-i32x8 --opt=disable-fma -o sfdxt.obj
if errorlevel 1 exit /b 1
cl /nologo /O2 /GL /MT /LD /Brepro /W3 "%~dp0sfnative\sfnative.c" sfdxt.obj sfdxt_sse2.obj sfdxt_sse4.obj sfdxt_avx2.obj /Fe:sfnative.dll /link /LTCG /Brepro
if errorlevel 1 exit /b 1
copy /y sfnative.dll "%~dp0sfnative.dll" >nul
echo OK %~dp0sfnative.dll
