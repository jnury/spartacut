#!/bin/bash
# SpartaCut macOS Run Script
# Runs the app bundle (required for native menus to work)

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$SRC_DIR/../build"
APP_BUNDLE="$BUILD_DIR/SpartaCut.app"

# Check if bundle exists and is newer than source
BUNDLE_EXISTS=false
if [ -d "$APP_BUNDLE" ]; then
    BUNDLE_EXISTS=true
fi

# If bundle doesn't exist, create it
if [ "$BUNDLE_EXISTS" = false ]; then
    echo "📦 App bundle not found, creating it..."
    "$SCRIPT_DIR/bundle-macos.sh"
fi

echo "🚀 Running SpartaCut.app via Rosetta 2..."
echo "   (File menu will appear in macOS menu bar at top of screen)"
echo ""

# Run via Rosetta 2 (required for LibVLC on Apple Silicon)
# Note: Use 'open' to run as proper macOS app (enables native menus)
arch -x86_64 open "$APP_BUNDLE"
