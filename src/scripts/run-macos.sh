#!/bin/bash
# SpartaCut macOS Run Script
# Runs the built executable directly (no bundle)

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
EXECUTABLE="$SRC_DIR/SpartaCut/bin/Debug/net8.0/osx-x64/publish/SpartaCut"

# Check if executable exists
if [ ! -f "$EXECUTABLE" ]; then
    echo "ERROR: Executable not found at $EXECUTABLE"
    echo "Run ./build-macos.sh first to build the application"
    exit 1
fi

echo "🚀 Running SpartaCut via Rosetta 2..."
echo ""

# Run via Rosetta 2 (required for LibVLC on Apple Silicon)
arch -x86_64 "$EXECUTABLE"
