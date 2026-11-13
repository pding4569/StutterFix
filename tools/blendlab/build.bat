@echo off
setlocal
set "MSVC=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207"
set "SDKI=C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0"
set "SDKL=C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c"
set "PATH=%MSVC%\bin\Hostx64\x64;%PATH%"
set "INCLUDE=%MSVC%\include;%SDKI%\ucrt;%SDKI%\um;%SDKI%\shared;%SDKI%\winrt"
set "LIB=%MSVC%\lib\x64;%SDKL%\ucrt\x64;%SDKL%\um\x64"
cl /nologo /std:c++17 /EHsc /O2 /MT /W4 "%~dp0main.cpp" /Fe"%~dp0blendlab.exe" /Fo"%~dp0blendlab.obj" /link d3d11.lib dxgi.lib d3dcompiler.lib
exit /b %errorlevel%

