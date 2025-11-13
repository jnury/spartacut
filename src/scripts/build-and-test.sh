#!/bin/bash

# Build and test script for Sparta Cut
# Run from src/scripts/ directory

set -e  # Exit on error

cd "$(dirname "$0")/../.."  # Navigate to project root

echo "🔨 Building Sparta Cut..."
/usr/local/share/dotnet/dotnet build SpartaCut.sln

echo "📦 Publishing for macOS (x64)..."
/usr/local/share/dotnet/dotnet publish src/SpartaCut/SpartaCut.csproj \
    --runtime osx-x64 \
    --self-contained \
    -c Debug

echo "🚀 Launching Sparta Cut..."
arch -x86_64 src/SpartaCut/bin/Debug/net8.0/osx-x64/publish/SpartaCut
