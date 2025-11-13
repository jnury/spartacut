# SpartaCut Windows Build Script
# Builds the application and generates icon files

param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Split-Path -Parent $ScriptDir
$ProjectFile = Join-Path $SrcDir "SpartaCut\SpartaCut.csproj"
$IconSource = Join-Path (Split-Path -Parent $SrcDir) "media\icon.png"

Write-Host "🧹 Cleaning previous build artifacts..." -ForegroundColor Cyan
& dotnet clean "$ProjectFile"

Write-Host ""
Write-Host "🎨 Generating Windows icon..." -ForegroundColor Cyan
& "$ScriptDir\generate-icon-windows.ps1" -SourcePng "$IconSource" -OutputIco "$SrcDir\SpartaCut\SpartaCut.ico"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Icon generation failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔨 Building SpartaCut (self-contained)..." -ForegroundColor Cyan
& dotnet publish "$ProjectFile" --runtime win-x64 --self-contained true -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✓ Build completed successfully" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  • Run the app:    .\run-windows.ps1" -ForegroundColor Gray
Write-Host "  • Create bundle:  .\bundle-windows.ps1" -ForegroundColor Gray
