# DuckyNet Linux Release Build Script
# PowerShell 5.1 compatible

Write-Host "================================" -ForegroundColor Cyan
Write-Host "DuckyNet Linux Release Build" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# Clean old dist
$LinuxDistDir = "dist/linux"
if (Test-Path $LinuxDistDir) {
    Write-Host "Cleaning old dist..." -ForegroundColor Yellow
    Remove-Item -Path $LinuxDistDir -Recurse -Force
}

Write-Host ""
Write-Host "[1/6] Building Shared (Release)..." -ForegroundColor Green
dotnet build Shared/DuckyNetShared.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Shared build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[2/6] Running Code Generator..." -ForegroundColor Green
dotnet run --project Tools/RpcCodeGen/RpcCodeGen.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Code generation failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/6] Rebuilding Shared..." -ForegroundColor Green
dotnet build Shared/DuckyNetShared.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Shared rebuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[4/6] Publishing Server (Linux x64)..." -ForegroundColor Green
dotnet publish Server/DuckyNetServer.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -o "$LinuxDistDir/Server"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Server publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[5/6] Building Client..." -ForegroundColor Green
dotnet build Client/DuckyNetClient.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Client build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[6/6] Packaging..." -ForegroundColor Green

$ClientDistDir = "$LinuxDistDir/Client"
New-Item -ItemType Directory -Path $ClientDistDir -Force | Out-Null
Copy-Item -Path "Client/res/*" -Destination $ClientDistDir -Recurse -Force

# Create start script
$StartScript = "#!/bin/bash`ncd `"`$(dirname `"`$0`")`"`nchmod +x ./DuckyNet.Server`n./DuckyNet.Server"
$StartScript | Out-File -FilePath "$LinuxDistDir/Server/start_server.sh" -Encoding ASCII -NoNewline

# Create README
$Readme = "# DuckyNet Linux Release`n`n## Server`n`n"
$Readme += "Start: cd Server && chmod +x start_server.sh && ./start_server.sh`n`n"
$Readme += "Port: 9050`n`n"
$Readme += "## Client (Unity Mod)`n`n"
$Readme += "Copy Client/*.dll to game Mods folder`n`n"
$Readme += "Build: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$Readme | Out-File -FilePath "$LinuxDistDir/README.txt" -Encoding UTF8

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output Directory: $LinuxDistDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files:" -ForegroundColor White
Write-Host "  Server: dist/linux/Server/DuckyNet.Server (67 MB)" -ForegroundColor Gray
Write-Host "  Client: dist/linux/Client/*.dll (3 MB)" -ForegroundColor Gray
Write-Host "  README: dist/linux/README.txt" -ForegroundColor Gray
Write-Host ""
Write-Host "Ready to deploy!" -ForegroundColor Green
Write-Host ""
