# DuckyNet 服务器启动脚本
# 使用方法：右键点击 -> 使用 PowerShell 运行

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "     DuckyNet 服务器启动脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查是否在正确的目录
if (-not (Test-Path "DuckyNetServer.csproj")) {
    Write-Host "错误：未找到 DuckyNetServer.csproj" -ForegroundColor Red
    Write-Host "请确保在 Server 目录下运行此脚本" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "按任意键退出..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# 检查 .NET SDK
Write-Host "检查 .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "错误：未找到 .NET SDK" -ForegroundColor Red
    Write-Host "请访问 https://dotnet.microsoft.com/download 下载并安装 .NET 8.0 SDK" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "按任意键退出..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}
Write-Host "✓ 找到 .NET SDK 版本: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# 检查是否需要还原依赖
$objFolder = "obj"
if (-not (Test-Path $objFolder)) {
    Write-Host "首次运行，正在还原依赖包..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "错误：依赖还原失败" -ForegroundColor Red
        Write-Host ""
        Write-Host "按任意键退出..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
    Write-Host "✓ 依赖还原完成" -ForegroundColor Green
    Write-Host ""
}

# 启动服务器
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "正在启动 DuckyNet 服务器..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "启动后可访问：" -ForegroundColor Yellow
Write-Host "  • Web 管理后台: http://localhost:5000" -ForegroundColor White
Write-Host "  • API 文档: http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  • RPC 端口: 9050" -ForegroundColor White
Write-Host ""
Write-Host "按 Ctrl+C 停止服务器" -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 运行服务器
dotnet run

# 如果服务器异常退出
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "服务器异常退出，错误代码: $LASTEXITCODE" -ForegroundColor Red
    Write-Host ""
    Write-Host "按任意键退出..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

