# SpartaCut Windows Run Script
# Runs the built executable directly

param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Split-Path -Parent $ScriptDir
$Executable = Join-Path $SrcDir "SpartaCut\bin\$Configuration\net8.0\win-x64\publish\SpartaCut.exe"

# Check if executable exists
if (-not (Test-Path $Executable)) {
    Write-Host "ERROR: Executable not found at $Executable" -ForegroundColor Red
    Write-Host "Run .\build-windows.ps1 first to build the application" -ForegroundColor Yellow
    exit 1
}

Write-Host "🚀 Running SpartaCut..." -ForegroundColor Cyan
Write-Host ""

# Run the executable
& $Executable
