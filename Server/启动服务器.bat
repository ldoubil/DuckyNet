@echo off
chcp 65001 >nul
title DuckyNet 服务器

echo ========================================
echo      DuckyNet 服务器启动脚本
echo ========================================
echo.

cd /d "%~dp0"

if not exist "DuckyNetServer.csproj" (
    echo 错误：未找到 DuckyNetServer.csproj
    echo 请确保在 Server 目录下运行此脚本
    echo.
    pause
    exit /b 1
)

echo 检查 .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo 错误：未找到 .NET SDK
    echo 请访问 https://dotnet.microsoft.com/download 下载并安装 .NET 8.0 SDK
    echo.
    pause
    exit /b 1
)

echo ✓ 找到 .NET SDK
echo.

if not exist "obj" (
    echo 首次运行，正在还原依赖包...
    dotnet restore
    if errorlevel 1 (
        echo 错误：依赖还原失败
        echo.
        pause
        exit /b 1
    )
    echo ✓ 依赖还原完成
    echo.
)

echo ========================================
echo 正在启动 DuckyNet 服务器...
echo ========================================
echo.
echo 启动后可访问：
echo   • Web 管理后台: http://localhost:5000
echo   • API 文档: http://localhost:5000/swagger
echo   • RPC 端口: 9050
echo.
echo 按 Ctrl+C 停止服务器
echo.
echo ========================================
echo.

dotnet run

if errorlevel 1 (
    echo.
    echo 服务器异常退出
    echo.
    pause
)

