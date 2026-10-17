@echo off
rem Original StutterFix D3D11 fixture. Same MSVC/SDK arguments as native/build-sfnative.bat.
setlocal
set "MSVC=%~1"
set "SDKI=%~2"
set "SDKL=%~3"
set "PATH=%MSVC%\bin\Hostx64\x64;%PATH%"
set "INCLUDE=%MSVC%\include;%SDKI%\ucrt;%SDKI%\um;%SDKI%\shared;%SDKI%\winrt"
set "LIB=%MSVC%\lib\x64;%SDKL%\ucrt\x64;%SDKL%\um\x64"
set "OUT=%~dp0out"
if not exist "%OUT%" mkdir "%OUT%"
if "%~4"=="block" (
cl /nologo /std:c++17 /EHsc /O2 /MT /W4 /WX /DFG_NORMAL_FIXTURE "%~dp0block_flow_lab.cpp" /Fe"%OUT%\constant-ring-block.exe" /Fo"%OUT%\constant-ring-block.obj" /link d3d11.lib dxgi.lib d3dcompiler.lib user32.lib
) else (
cl /nologo /std:c++17 /EHsc /O2 /MT /W4 /WX "%~dp0constant_ring_lab.cpp" /Fe"%OUT%\constant-ring.exe" /Fo"%OUT%\constant-ring.obj" /link d3d11.lib d3dcompiler.lib
)
exit /b %errorlevel%
