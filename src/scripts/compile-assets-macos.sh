#!/bin/bash
# Compile macOS .icon asset catalog to Assets.car
# Usage: ./compile-assets-macos.sh [icon-source] [output-car]

set -e  # Exit on error

# Default paths
ICON_SOURCE="${1:-../../media/icon.icon}"
OUTPUT_CAR="${2:-../SpartaCut/Assets.car}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Convert relative paths to absolute
ICON_SOURCE="$(cd "$(dirname "$ICON_SOURCE")" && pwd)/$(basename "$ICON_SOURCE")"
OUTPUT_DIR="$(cd "$(dirname "$OUTPUT_CAR")" && pwd)"
OUTPUT_CAR="$OUTPUT_DIR/$(basename "$OUTPUT_CAR")"

echo "🎨 Compiling macOS asset catalog..."
echo "   Source: $ICON_SOURCE"
echo "   Output: $OUTPUT_CAR"

# Validate source directory exists
if [ ! -d "$ICON_SOURCE" ]; then
    echo "ERROR: Icon source not found at $ICON_SOURCE"
    exit 1
fi

# Check if actool is available
if ! command -v actool &> /dev/null; then
    echo "ERROR: actool not found"
    echo "Install Xcode Command Line Tools: xcode-select --install"
    exit 1
fi

# Check for icon.json
if [ ! -f "$ICON_SOURCE/icon.json" ]; then
    echo "ERROR: icon.json not found in $ICON_SOURCE"
    exit 1
fi

# Create temporary output directory
TEMP_OUTPUT="$(mktemp -d)"

echo "   → Running actool..."

# Compile asset catalog to Assets.car
actool "$ICON_SOURCE" \
    --output-format human-readable-text --notices --warnings --errors \
    --compile "$TEMP_OUTPUT" \
    --platform macosx \
    --minimum-deployment-target 10.15 \
    --app-icon AppIcon --include-all-app-icons \
    --enable-on-demand-resources NO \
    --development-region en \
    --target-device mac \
    --output-partial-info-plist "$TEMP_OUTPUT/partial.plist" \
    > /tmp/actool.log 2>&1

# Check if Assets.car was created
if [ ! -f "$TEMP_OUTPUT/Assets.car" ]; then
    echo "ERROR: actool failed to generate Assets.car"
    rm -rf "$TEMP_OUTPUT"
    exit 1
fi

# Move Assets.car to output location
mv "$TEMP_OUTPUT/Assets.car" "$OUTPUT_CAR"

# Cleanup
rm -rf "$TEMP_OUTPUT"

echo "   ✓ Asset catalog compiled successfully"

# Display file size
SIZE=$(du -h "$OUTPUT_CAR" | cut -f1)
echo "   ✓ File size: $SIZE"
