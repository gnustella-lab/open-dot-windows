# Build & Package Script for Open Dot for Windows
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $SolutionDir "dist"
$WinX64Dir = Join-Path $DistDir "OpenDot-win-x64"
$ZipOutput = Join-Path $DistDir "OpenDot-Windows-v$Version-x64.zip"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Open Dot for Windows - Packaging Script" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Check dotnet
$userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path $userDotnet) {
    $dotnet = $userDotnet
} else {
    $dotnet = "dotnet"
}

# 2. Publish self-contained executable
Write-Host "`n[1/3] Publishing self-contained win-x64 executable..." -ForegroundColor Yellow
if (Test-Path $WinX64Dir) {
    Remove-Item $WinX64Dir -Recurse -Force
}
$appProj = Join-Path $SolutionDir "src\OpenDot.App\OpenDot.App.csproj"
& $dotnet publish $appProj -c $Configuration -r win-x64 --self-contained true -p:WindowsPackageType=None -p:Platform=x64 -o $WinX64Dir

Write-Host "Publish completed at: $WinX64Dir" -ForegroundColor Green

# Ensure resources.pri exists for unpackaged WinUI resource resolution
if ((Test-Path "$WinX64Dir\OpenDot.App.pri") -and -not (Test-Path "$WinX64Dir\resources.pri")) {
    Copy-Item "$WinX64Dir\OpenDot.App.pri" "$WinX64Dir\resources.pri" -Force
    Write-Host "Ensured resources.pri mapping in publish output." -ForegroundColor Cyan
}

# Ensure Assets folder exists with application icons
$assetsSource = Join-Path $SolutionDir "src\OpenDot.App\Assets"
$assetsDest = Join-Path $WinX64Dir "Assets"
if (Test-Path $assetsSource) {
    if (-not (Test-Path $assetsDest)) { New-Item -ItemType Directory -Path $assetsDest -Force | Out-Null }
    Copy-Item "$assetsSource\*" $assetsDest -Recurse -Force
    Write-Host "Ensured Assets directory with application icons in publish output." -ForegroundColor Cyan
}

# 3. Create ZIP archive
Write-Host "`n[2/3] Creating portable ZIP archive: $ZipOutput..." -ForegroundColor Yellow
if (Test-Path $ZipOutput) {
    Remove-Item $ZipOutput -Force
}
Compress-Archive -Path "$WinX64Dir\*" -DestinationPath $ZipOutput -CompressionLevel Optimal
Write-Host "Portable ZIP created successfully ($((Get-Item $ZipOutput).Length / 1MB | ForEach-Object { '{0:N2} MB' -f $_ }))" -ForegroundColor Green

# 4. Optional Inno Setup compilation
Write-Host "`n[3/3] Checking for Inno Setup compiler (ISCC.exe)..." -ForegroundColor Yellow
$iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
if (-not $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $iscc = $cand
            break
        }
    }
}

if ($iscc) {
    Write-Host "Found Inno Setup at $iscc. Building installer..." -ForegroundColor Green
    $issFile = Join-Path $ScriptDir "installer.iss"
    & $iscc $issFile
    Write-Host "Installer built successfully in dist/installer/" -ForegroundColor Green
} else {
    Write-Host "Inno Setup compiler not found. Skipping installer exe creation (portable ZIP is ready)." -ForegroundColor Gray
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Packaging completed successfully!" -ForegroundColor Green
Write-Host "Portable package: $ZipOutput" -ForegroundColor Cyan
Write-Host "Executable:       $WinX64Dir\OpenDot.App.exe" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
