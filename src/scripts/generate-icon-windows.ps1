# Generate Windows .ico icon from source PNG
# Usage: .\generate-icon-windows.ps1 [source-png] [output-ico]

param(
    [string]$SourcePng = "..\..\media\icon.png",
    [string]$OutputIco = "..\SpartaCut\SpartaCut.ico"
)

$ErrorActionPreference = "Stop"

# Convert to absolute paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourcePng = [System.IO.Path]::GetFullPath((Join-Path $ScriptDir $SourcePng))
$OutputIco = [System.IO.Path]::GetFullPath((Join-Path $ScriptDir $OutputIco))

Write-Host "🎨 Generating Windows icon (.ico)..." -ForegroundColor Cyan
Write-Host "   Source: $SourcePng" -ForegroundColor Gray
Write-Host "   Output: $OutputIco" -ForegroundColor Gray

# Validate source file exists
if (-not (Test-Path $SourcePng)) {
    Write-Host "ERROR: Source PNG not found at $SourcePng" -ForegroundColor Red
    exit 1
}

# Check if magick (ImageMagick) is available
$magickPath = Get-Command magick -ErrorAction SilentlyContinue

if ($magickPath) {
    Write-Host "   → Using ImageMagick to generate .ico..." -ForegroundColor Gray

    # Generate .ico with multiple sizes using ImageMagick
    # Windows .ico typically includes: 16x16, 32x32, 48x48, 64x64, 128x128, 256x256
    & magick convert "$SourcePng" `
        -define icon:auto-resize=256,128,64,48,32,16 `
        "$OutputIco"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: ImageMagick conversion failed" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "   → ImageMagick not found, using .NET System.Drawing..." -ForegroundColor Gray

    # Load System.Drawing assembly
    Add-Type -AssemblyName System.Drawing

    # Create temporary directory for intermediate PNGs
    $TempDir = New-Item -ItemType Directory -Path ([System.IO.Path]::GetTempPath()) -Name "spartacut-icon-$([Guid]::NewGuid())"

    try {
        # Load source image
        $SourceImage = [System.Drawing.Image]::FromFile($SourcePng)

        # Generate required sizes for Windows .ico
        $Sizes = @(16, 32, 48, 64, 128, 256)
        $TempFiles = @()

        Write-Host "   → Generating icon sizes..." -ForegroundColor Gray
        foreach ($Size in $Sizes) {
            $ResizedImage = New-Object System.Drawing.Bitmap($Size, $Size)
            $Graphics = [System.Drawing.Graphics]::FromImage($ResizedImage)
            $Graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $Graphics.DrawImage($SourceImage, 0, 0, $Size, $Size)
            $Graphics.Dispose()

            $TempFile = Join-Path $TempDir "icon_${Size}x${Size}.png"
            $ResizedImage.Save($TempFile, [System.Drawing.Imaging.ImageFormat]::Png)
            $ResizedImage.Dispose()
            $TempFiles += $TempFile
        }

        $SourceImage.Dispose()

        Write-Host "   → Combining into .ico format..." -ForegroundColor Gray

        # Create .ico file using .NET IconWriter
        # This is a simplified approach - for production, consider using ImageMagick
        $IcoStream = [System.IO.File]::Create($OutputIco)

        try {
            # ICO header (6 bytes)
            $IcoStream.WriteByte(0) # Reserved
            $IcoStream.WriteByte(0)
            $IcoStream.WriteByte(1) # Type: 1 = ICO
            $IcoStream.WriteByte(0)
            $IcoStream.WriteByte($TempFiles.Count) # Number of images
            $IcoStream.WriteByte(0)

            # Calculate image data offsets
            $HeaderSize = 6 + ($TempFiles.Count * 16)
            $Offset = $HeaderSize
            $ImageDataList = @()

            # Write image directory entries and collect image data
            foreach ($TempFile in $TempFiles) {
                $ImageBytes = [System.IO.File]::ReadAllBytes($TempFile)
                $ImageDataList += $ImageBytes

                # Parse PNG dimensions
                $Size = [int]([System.IO.Path]::GetFileNameWithoutExtension($TempFile) -replace 'icon_(\d+)x\d+','$1')
                $SizeByte = if ($Size -eq 256) { 0 } else { $Size }

                # Write directory entry (16 bytes)
                $IcoStream.WriteByte($SizeByte) # Width
                $IcoStream.WriteByte($SizeByte) # Height
                $IcoStream.WriteByte(0) # Color palette
                $IcoStream.WriteByte(0) # Reserved
                $IcoStream.WriteByte(1) # Color planes
                $IcoStream.WriteByte(0)
                $IcoStream.WriteByte(32) # Bits per pixel
                $IcoStream.WriteByte(0)

                # Image data size (4 bytes, little-endian)
                $DataSize = $ImageBytes.Length
                $IcoStream.WriteByte($DataSize -band 0xFF)
                $IcoStream.WriteByte(($DataSize -shr 8) -band 0xFF)
                $IcoStream.WriteByte(($DataSize -shr 16) -band 0xFF)
                $IcoStream.WriteByte(($DataSize -shr 24) -band 0xFF)

                # Image data offset (4 bytes, little-endian)
                $IcoStream.WriteByte($Offset -band 0xFF)
                $IcoStream.WriteByte(($Offset -shr 8) -band 0xFF)
                $IcoStream.WriteByte(($Offset -shr 16) -band 0xFF)
                $IcoStream.WriteByte(($Offset -shr 24) -band 0xFF)

                $Offset += $ImageBytes.Length
            }

            # Write image data
            foreach ($ImageData in $ImageDataList) {
                $IcoStream.Write($ImageData, 0, $ImageData.Length)
            }
        } finally {
            $IcoStream.Close()
        }
    } finally {
        # Cleanup temporary files
        Remove-Item -Path $TempDir -Recurse -Force
    }
}

Write-Host "   ✓ Icon generated successfully: $OutputIco" -ForegroundColor Green

# Verify the .ico file was created
if (-not (Test-Path $OutputIco)) {
    Write-Host "ERROR: Failed to generate .ico file" -ForegroundColor Red
    exit 1
}

# Display file size
$Size = (Get-Item $OutputIco).Length
$SizeKB = [math]::Round($Size / 1KB, 2)
Write-Host "   ✓ File size: $SizeKB KB" -ForegroundColor Green

Write-Host ""
Write-Host "NOTE: For best results, install ImageMagick:" -ForegroundColor Yellow
Write-Host "      winget install ImageMagick.ImageMagick" -ForegroundColor Gray
