@echo off
echo ================================
echo DuckyNet RPC Framework Build
echo ================================

echo.
echo [1/6] Building Shared...
dotnet build Shared/DuckyNetShared.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Shared build failed!
    pause
    exit /b 1
)

echo.
echo [2/6] Running Code Generator...
dotnet run --project RPC/DuckyNet.RPC.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Code generation failed!
    pause
    exit /b 1
)

echo.
echo [3/6] Rebuilding Shared (with generated code)...
dotnet build Shared/DuckyNetShared.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Shared rebuild failed!
    pause
    exit /b 1
)

echo.
echo [4/6] Building Server...
dotnet build Server/DuckyNetServer.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Server build failed!
    pause
    exit /b 1
)

echo.
echo [5/6] Building Client...
dotnet build Client/DuckyNetClient.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Client build failed!
    pause
    exit /b 1
)

echo.
echo [6/6] Running Tests...
dotnet test Tests\DuckyNet.RPC.Tests\DuckyNet.RPC.Tests.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] RPC tests failed!
    pause
    exit /b 1
)

dotnet test Tests\DuckyNet.Shared.Tests\DuckyNet.Shared.Tests.csproj -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Shared tests failed!
    pause
    exit /b 1
)

echo.
echo ================================
echo Build Complete! 
echo ================================
echo.
echo Output Files:
echo   Server: Server\bin\Debug\net8.0\DuckyNetServer.exe
echo   Client: Client\bin\Debug\netstandard2.1\DuckyNetClient.dll
echo.
echo To run server:
echo   cd Server\bin\Debug\net8.0
echo   DuckyNetServer.exe
echo.
echo To test:
echo   dotnet test Tests\DuckyNet.RPC.Tests\DuckyNet.RPC.Tests.csproj -c Debug
echo   dotnet test Tests\DuckyNet.Shared.Tests\DuckyNet.Shared.Tests.csproj -c Debug
echo.
pause
