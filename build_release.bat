@echo off
echo ================================
echo DuckyNet RPC Framework Build (Release)
echo ================================

echo.
echo [1/5] Building Shared (Release)...
dotnet build Shared/DuckyNetShared.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Shared build failed!
    pause
    exit /b 1
)

echo.
echo [2/5] Running Code Generator...
dotnet run --project RPC/DuckyNet.RPC.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Code generation failed!
    pause
    exit /b 1
)

echo.
echo [3/5] Rebuilding Shared (with generated code, Release)...
dotnet build Shared/DuckyNetShared.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Shared rebuild failed!
    pause
    exit /b 1
)

echo.
echo [4/5] Building Server (Release)...
dotnet build Server/DuckyNetServer.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Server build failed!
    pause
    exit /b 1
)

echo.
echo [5/5] Building Client (Release, no console)...
dotnet build Client/DuckyNetClient.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Client build failed!
    pause
    exit /b 1
)

echo.
echo ================================
echo Build Complete (Release)! 
echo ================================
echo.
echo Output Files:
echo   Server: Server\bin\Release\net8.0\DuckyNetServer.exe
echo   Client: Client\bin\Release\netstandard2.1\DuckyNetClient.dll
echo.
echo Release features:
echo   - Optimized performance
echo   - No debug console window
echo   - Smaller file size
echo.
echo To run server:
echo   cd Server\bin\Release\net8.0
echo   DuckyNetServer.exe
echo.
pause

