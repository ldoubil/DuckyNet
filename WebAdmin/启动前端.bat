@echo off
chcp 65001 >nul
title DuckyNet Web Admin - 前端

echo ========================================
echo   DuckyNet Web Admin - 前端启动脚本
echo ========================================
echo.

cd /d "%~dp0"

if not exist "package.json" (
    echo 错误：未找到 package.json
    echo 请确保在 WebAdmin 目录下运行此脚本
    echo.
    pause
    exit /b 1
)

echo 检查 Node.js...
node --version >nul 2>&1
if errorlevel 1 (
    echo 错误：未找到 Node.js
    echo 请访问 https://nodejs.org 下载并安装 Node.js
    echo.
    pause
    exit /b 1
)

echo ✓ 找到 Node.js
echo.

if not exist "node_modules" (
    echo 首次运行，正在安装依赖...
    call npm install
    if errorlevel 1 (
        echo 错误：依赖安装失败
        echo.
        pause
        exit /b 1
    )
    echo ✓ 依赖安装完成
    echo.
)

echo ========================================
echo 正在启动前端开发服务器...
echo ========================================
echo.
echo 启动后可访问：
echo   • 前端界面: http://localhost:3000
echo.
echo 按 Ctrl+C 停止服务器
echo.
echo ========================================
echo.

call npm run dev

if errorlevel 1 (
    echo.
    echo 服务器异常退出
    echo.
    pause
)

