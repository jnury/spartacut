# SpartaCut Windows Bundle Script
# Creates a self-contained distributable package

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Split-Path -Parent $ScriptDir
$ProjectFile = Join-Path $SrcDir "SpartaCut\SpartaCut.csproj"
$IconSource = Join-Path (Split-Path -Parent $SrcDir) "media\icon.png"

# Read version from .csproj
$ProjectXml = [xml](Get-Content $ProjectFile)
$Version = $ProjectXml.Project.PropertyGroup.Version

Write-Host "🎨 Generating Windows icon..." -ForegroundColor Cyan
& "$ScriptDir\generate-icon-windows.ps1" -SourcePng "$IconSource" -OutputIco "$SrcDir\SpartaCut\SpartaCut.ico"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Icon generation failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📦 Publishing for Windows ($Runtime)..." -ForegroundColor Cyan
& dotnet publish "$ProjectFile" --runtime $Runtime --self-contained true -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed" -ForegroundColor Red
    exit 1
}

$PublishDir = Join-Path $SrcDir "SpartaCut\bin\$Configuration\net8.0\$Runtime\publish"
$BuildDir = Join-Path (Split-Path -Parent $SrcDir) "build"
$BundleDir = Join-Path $BuildDir "SpartaCut-$Version-$Runtime"

Write-Host ""
Write-Host "📦 Creating distribution bundle..." -ForegroundColor Cyan

# Create build directory if it doesn't exist
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

# Remove old bundle if it exists
if (Test-Path $BundleDir) {
    Remove-Item -Path $BundleDir -Recurse -Force
}

# Create bundle directory
New-Item -ItemType Directory -Force -Path $BundleDir | Out-Null

# Copy published files
Copy-Item -Path "$PublishDir\*" -Destination $BundleDir -Recurse

Write-Host ""
Write-Host "✓ Bundle created successfully: $BundleDir" -ForegroundColor Green
Write-Host ""
Write-Host "To run the app:" -ForegroundColor Yellow
Write-Host "  $BundleDir\SpartaCut.exe" -ForegroundColor Gray
Write-Host ""
Write-Host "For Microsoft Store distribution:" -ForegroundColor Yellow
Write-Host "  Create MSIX package using Windows Application Packaging Project" -ForegroundColor Gray
