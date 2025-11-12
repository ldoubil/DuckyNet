@echo off
chcp 65001 >nul
title DuckyNet 完整系统启动

echo ========================================
echo   DuckyNet 完整系统启动脚本
echo ========================================
echo.
echo 将启动：
echo   1. 后端 Server (RPC + Web API)
echo   2. 前端 Web Admin (Vue3)
echo.
echo 按任意键继续...
pause >nul

echo.
echo [1/2] 启动后端 Server...
start "DuckyNet Server" cmd /c "cd /d E:\git\DuckyNet\Server && dotnet run"

echo.
echo 等待后端启动（5秒）...
timeout /t 5 >nul

echo.
echo [2/2] 启动前端 Web Admin...
start "DuckyNet WebAdmin" cmd /c "cd /d E:\git\DuckyNet\WebAdmin && npm run dev"

echo.
echo ========================================
echo 完整系统已启动！
echo ========================================
echo.
echo 访问地址：
echo   • 前端界面: http://localhost:3000
echo   • 后端API: http://localhost:5000/swagger
echo   • WebSocket: ws://localhost:5000/ws
echo.
echo 关闭此窗口不会停止服务，
echo 请在各自的窗口中按 Ctrl+C 停止。
echo.
pause

