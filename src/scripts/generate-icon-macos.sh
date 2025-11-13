#!/bin/bash
# Generate macOS .icns icon from source PNG
# Usage: ./generate-icon-macos.sh [source-png] [output-icns]

set -e  # Exit on error

# Default paths
SOURCE_PNG="${1:-../../media/icon.png}"
OUTPUT_ICNS="${2:-../SpartaCut/SpartaCut.icns}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Convert relative paths to absolute
SOURCE_PNG="$(cd "$(dirname "$SOURCE_PNG")" && pwd)/$(basename "$SOURCE_PNG")"
OUTPUT_DIR="$(cd "$(dirname "$OUTPUT_ICNS")" && pwd)"
OUTPUT_ICNS="$OUTPUT_DIR/$(basename "$OUTPUT_ICNS")"

echo "🎨 Generating macOS icon (.icns)..."
echo "   Source: $SOURCE_PNG"
echo "   Output: $OUTPUT_ICNS"

# Validate source file exists
if [ ! -f "$SOURCE_PNG" ]; then
    echo "ERROR: Source PNG not found at $SOURCE_PNG"
    exit 1
fi

# Create temporary iconset directory
TEMP_ICONSET="$(mktemp -d)/SpartaCut.iconset"
mkdir -p "$TEMP_ICONSET"

echo "   → Generating icon sizes..."

# Generate all required sizes for macOS .icns
# Format: sips -z height width input --out output
# Note: Remove color profiles to prevent any rendering issues
sips -z 16 16 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_16x16.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_16x16.png" > /dev/null 2>&1
sips -z 32 32 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_16x16@2x.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_16x16@2x.png" > /dev/null 2>&1
sips -z 32 32 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_32x32.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_32x32.png" > /dev/null 2>&1
sips -z 64 64 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_32x32@2x.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_32x32@2x.png" > /dev/null 2>&1
sips -z 128 128 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_128x128.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_128x128.png" > /dev/null 2>&1
sips -z 256 256 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_128x128@2x.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_128x128@2x.png" > /dev/null 2>&1
sips -z 256 256 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_256x256.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_256x256.png" > /dev/null 2>&1
sips -z 512 512 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_256x256@2x.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_256x256@2x.png" > /dev/null 2>&1
sips -z 512 512 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_512x512.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_512x512.png" > /dev/null 2>&1
sips -z 1024 1024 "$SOURCE_PNG" --out "$TEMP_ICONSET/icon_512x512@2x.png" > /dev/null 2>&1
sips -d profile "$TEMP_ICONSET/icon_512x512@2x.png" > /dev/null 2>&1

echo "   → Converting to .icns format..."
iconutil -c icns "$TEMP_ICONSET" -o "$OUTPUT_ICNS"

# Cleanup temporary files
rm -rf "$TEMP_ICONSET"

echo "   ✓ Icon generated successfully: $OUTPUT_ICNS"

# Verify the .icns file was created
if [ ! -f "$OUTPUT_ICNS" ]; then
    echo "ERROR: Failed to generate .icns file"
    exit 1
fi

# Display file size
SIZE=$(du -h "$OUTPUT_ICNS" | cut -f1)
echo "   ✓ File size: $SIZE"
