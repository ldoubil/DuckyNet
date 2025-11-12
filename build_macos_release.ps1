# DuckyNet macOS Release Build Script
# PowerShell 5.1 compatible
# Supports both Intel (x64) and Apple Silicon (arm64)

Write-Host "================================" -ForegroundColor Cyan
Write-Host "DuckyNet macOS Release Build" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# Ask for architecture
Write-Host "Select target architecture:" -ForegroundColor Yellow
Write-Host "  1) Intel (x64)" -ForegroundColor White
Write-Host "  2) Apple Silicon (arm64)" -ForegroundColor White
Write-Host "  3) Both (Universal)" -ForegroundColor White
$choice = Read-Host "Enter choice (1-3)"

$BuildIntel = $false
$BuildArm = $false

switch ($choice) {
    "1" { $BuildIntel = $true; $ArchName = "x64" }
    "2" { $BuildArm = $true; $ArchName = "arm64" }
    "3" { $BuildIntel = $true; $BuildArm = $true; $ArchName = "universal" }
    default { 
        Write-Host "Invalid choice, defaulting to Intel (x64)" -ForegroundColor Yellow
        $BuildIntel = $true
        $ArchName = "x64"
    }
}

# Clean old dist
$MacOSDistDir = "dist/macos-$ArchName"
if (Test-Path $MacOSDistDir) {
    Write-Host "Cleaning old dist..." -ForegroundColor Yellow
    Remove-Item -Path $MacOSDistDir -Recurse -Force
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
Write-Host "[4/6] Publishing Server (macOS)..." -ForegroundColor Green

# Build Intel version
if ($BuildIntel) {
    Write-Host "  Building for Intel (x64)..." -ForegroundColor Cyan
    $IntelDir = if ($BuildArm) { "$MacOSDistDir/Server-Intel" } else { "$MacOSDistDir/Server" }
    dotnet publish Server/DuckyNetServer.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -o $IntelDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Intel server publish failed!" -ForegroundColor Red
        exit 1
    }
}

# Build ARM version (Apple Silicon)
if ($BuildArm) {
    Write-Host "  Building for Apple Silicon (arm64)..." -ForegroundColor Cyan
    $ArmDir = if ($BuildIntel) { "$MacOSDistDir/Server-ARM" } else { "$MacOSDistDir/Server" }
    dotnet publish Server/DuckyNetServer.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -o $ArmDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: ARM server publish failed!" -ForegroundColor Red
        exit 1
    }
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

$ClientDistDir = "$MacOSDistDir/Client"
New-Item -ItemType Directory -Path $ClientDistDir -Force | Out-Null
Copy-Item -Path "Client/res/*" -Destination $ClientDistDir -Recurse -Force

# Create start scripts
if ($BuildIntel -and -not $BuildArm) {
    # Single Intel version
    $StartScript = "#!/bin/bash`ncd `"`$(dirname `"`$0`")`"`nchmod +x ./DuckyNet.Server`n./DuckyNet.Server"
    $StartScript | Out-File -FilePath "$MacOSDistDir/Server/start_server.sh" -Encoding ASCII -NoNewline
}
elseif ($BuildArm -and -not $BuildIntel) {
    # Single ARM version
    $StartScript = "#!/bin/bash`ncd `"`$(dirname `"`$0`")`"`nchmod +x ./DuckyNet.Server`n./DuckyNet.Server"
    $StartScript | Out-File -FilePath "$MacOSDistDir/Server/start_server.sh" -Encoding ASCII -NoNewline
}
else {
    # Universal - create scripts for both
    $StartScriptIntel = "#!/bin/bash`ncd `"`$(dirname `"`$0`")`"/Server-Intel`nchmod +x ./DuckyNet.Server`n./DuckyNet.Server"
    $StartScriptIntel | Out-File -FilePath "$MacOSDistDir/start_server_intel.sh" -Encoding ASCII -NoNewline
    
    $StartScriptArm = "#!/bin/bash`ncd `"`$(dirname `"`$0`")`"/Server-ARM`nchmod +x ./DuckyNet.Server`n./DuckyNet.Server"
    $StartScriptArm | Out-File -FilePath "$MacOSDistDir/start_server_arm.sh" -Encoding ASCII -NoNewline
    
    # Auto-detect script
    $AutoScript = @"
#!/bin/bash
cd "`$(dirname "`$0`")"

# Detect architecture
ARCH=`$(uname -m)

if [ "`$ARCH" = "arm64" ]; then
    echo "Detected Apple Silicon (ARM64)"
    chmod +x ./start_server_arm.sh
    ./start_server_arm.sh
elif [ "`$ARCH" = "x86_64" ]; then
    echo "Detected Intel (x64)"
    chmod +x ./start_server_intel.sh
    ./start_server_intel.sh
else
    echo "Unknown architecture: `$ARCH"
    exit 1
fi
"@
    $AutoScript | Out-File -FilePath "$MacOSDistDir/start_server.sh" -Encoding ASCII -NoNewline
}

# Create README
$Readme = "# DuckyNet macOS Release`n`n"

if ($BuildIntel -and $BuildArm) {
    $Readme += "## Server (Universal Binary)`n`n"
    $Readme += "Auto-start (detects your Mac type):`n"
    $Readme += "  chmod +x start_server.sh && ./start_server.sh`n`n"
    $Readme += "Or manually:`n"
    $Readme += "  Intel Mac: chmod +x start_server_intel.sh && ./start_server_intel.sh`n"
    $Readme += "  Apple Silicon: chmod +x start_server_arm.sh && ./start_server_arm.sh`n`n"
}
elseif ($BuildIntel) {
    $Readme += "## Server (Intel x64)`n`n"
    $Readme += "Start: cd Server && chmod +x start_server.sh && ./start_server.sh`n`n"
}
else {
    $Readme += "## Server (Apple Silicon ARM64)`n`n"
    $Readme += "Start: cd Server && chmod +x start_server.sh && ./start_server.sh`n`n"
}

$Readme += "Port: 9050`n`n"
$Readme += "## Client (Unity Mod)`n`n"
$Readme += "Copy Client/*.dll to game Mods folder`n`n"
$Readme += "## System Requirements`n`n"

if ($BuildIntel -and $BuildArm) {
    $Readme += "- macOS 10.15+ (Catalina or later)`n"
    $Readme += "- Works on both Intel and Apple Silicon Macs`n`n"
}
elseif ($BuildIntel) {
    $Readme += "- macOS 10.15+ (Catalina or later)`n"
    $Readme += "- Intel-based Mac only`n`n"
}
else {
    $Readme += "- macOS 11.0+ (Big Sur or later)`n"
    $Readme += "- Apple Silicon Mac only`n`n"
}

$Readme += "Build: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$Readme += "Architecture: $ArchName`n"
$Readme | Out-File -FilePath "$MacOSDistDir/README.txt" -Encoding UTF8

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output Directory: $MacOSDistDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files:" -ForegroundColor White

if ($BuildIntel -and $BuildArm) {
    Write-Host "  Server (Intel): dist/macos-universal/Server-Intel/DuckyNet.Server (~67 MB)" -ForegroundColor Gray
    Write-Host "  Server (ARM): dist/macos-universal/Server-ARM/DuckyNet.Server (~67 MB)" -ForegroundColor Gray
    Write-Host "  Auto-start: dist/macos-universal/start_server.sh" -ForegroundColor Gray
}
elseif ($BuildIntel) {
    Write-Host "  Server: dist/macos-x64/Server/DuckyNet.Server (~67 MB)" -ForegroundColor Gray
}
else {
    Write-Host "  Server: dist/macos-arm64/Server/DuckyNet.Server (~67 MB)" -ForegroundColor Gray
}

Write-Host "  Client: dist/macos-$ArchName/Client/*.dll (3 MB)" -ForegroundColor Gray
Write-Host "  README: dist/macos-$ArchName/README.txt" -ForegroundColor Gray
Write-Host ""
Write-Host "Ready to deploy!" -ForegroundColor Green
Write-Host ""

