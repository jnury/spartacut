#!/bin/bash
# SpartaCut macOS Build Script
# Builds the application and generates icon files

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$SRC_DIR/SpartaCut/SpartaCut.csproj"
ICON_SOURCE="$SRC_DIR/../media/icon"

echo "🧹 Cleaning previous build artifacts..."
/usr/local/share/dotnet/dotnet clean "$PROJECT_FILE"

echo ""
echo "🎨 Generating macOS icon (.icns)..."
"$SCRIPT_DIR/generate-icon-macos.sh" "$ICON_SOURCE.png" "$SRC_DIR/SpartaCut/SpartaCut.icns"

echo ""
echo "🎨 Compiling macOS asset catalog (Liquid Glass)..."
"$SCRIPT_DIR/compile-assets-macos.sh" "$ICON_SOURCE.icon" "$SRC_DIR/SpartaCut/Assets.car"

echo ""
echo "🔨 Building SpartaCut (self-contained)..."
/usr/local/share/dotnet/dotnet publish "$PROJECT_FILE" --runtime osx-x64 --self-contained true -c Debug

echo ""
echo "✓ Build completed successfully"
echo ""
echo "Next steps:"
echo "  • Run the app:    ./run-macos.sh"
echo "  • Create bundle:  ./bundle-macos.sh"
