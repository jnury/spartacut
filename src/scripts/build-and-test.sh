#!/bin/bash

# Build and test script for Sparta Cut
# Builds, bundles, and runs the app (required for native menus on macOS)

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR/../.."  # Navigate to project root

echo "🔨 Building Sparta Cut..."
/usr/local/share/dotnet/dotnet build SpartaCut.sln

echo "📦 Creating macOS .app bundle..."
"$SCRIPT_DIR/bundle-macos.sh"

echo ""
echo "🚀 Launching Sparta Cut.app..."
echo "   (File menu will appear in macOS menu bar at top of screen)"
echo ""
arch -x86_64 open build/SpartaCut.app
