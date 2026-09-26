@echo off
rem Rebuilds native\turbojpeg.dll from the official libjpeg-turbo source (x64, SIMD, static CRT).
rem   Source:   https://github.com/libjpeg-turbo/libjpeg-turbo  tag 3.2.0 (commit c85e6b905bf237038faa936dab160ebfc5da0344)
rem   Compiler: Visual Studio 2022 MSVC 14.44 (x64), CMake 4.4.3, NASM 3.02, NMake
rem   Windows SDK: an installed SDK, or the NuGet packages Microsoft.Windows.SDK.CPP and
rem                Microsoft.Windows.SDK.CPP.x64 (10.0.26100.4188) extracted to a folder
rem Usage:   build-turbojpeg.bat SRC_DIR MSVC_DIR SDK_INCLUDE_DIR SDK_X64_LIB_DIR SDK_BIN_DIR CMAKE_BIN_DIR NASM_DIR
rem Example: build-turbojpeg.bat C:\SFBundle\libjpeg-turbo "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207" C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0 C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\bin\10.0.26100.0\x64 C:\SFBundle\tools\cmake-4.4.3-windows-x86_64\bin C:\SFBundle\tools\nasm-3.02
rem /Brepro makes the output identical on every build (no timestamps), so the shipped DLL can be re-created bit for bit.
rem (ASCII only: cmd reads .bat files in the console code page)
setlocal
set "SRC=%~1"
set "MSVC=%~2"
set "SDKI=%~3"
set "SDKL=%~4"
set "SDKB=%~5"
set "PATH=%MSVC%\bin\Hostx64\x64;%SDKB%;%~6;%~7;%PATH%"
set "INCLUDE=%MSVC%\include;%SDKI%\ucrt;%SDKI%\um;%SDKI%\shared"
set "LIB=%MSVC%\lib\x64;%SDKL%\ucrt\x64;%SDKL%\um\x64"
set "OUT=%SRC%\build-sf"
if exist "%OUT%" rmdir /s /q "%OUT%"
cmake -S "%SRC%" -B "%OUT%" -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Release -DENABLE_STATIC=OFF -DENABLE_SHARED=ON -DWITH_TURBOJPEG=ON -DWITH_TOOLS=OFF -DWITH_TESTS=OFF -DCMAKE_ASM_NASM_COMPILER=nasm -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded "-DCMAKE_C_FLAGS=/Brepro" "-DCMAKE_SHARED_LINKER_FLAGS=/Brepro"
if errorlevel 1 exit /b 1
cmake --build "%OUT%" --target turbojpeg
if errorlevel 1 exit /b 1
copy /y "%OUT%\turbojpeg.dll" "%~dp0turbojpeg.dll" >nul
echo OK %~dp0turbojpeg.dll
