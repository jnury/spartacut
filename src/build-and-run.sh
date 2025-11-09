#!/bin/bash

# SpartaCut Clean Build and Run Script
# Ensures a fresh build with no cached artifacts

set -e  # Exit on error

echo "🧹 Cleaning previous build artifacts..."
/usr/local/share/dotnet/dotnet clean SpartaCut/SpartaCut.csproj

echo ""
echo "🔨 Building SpartaCut..."
/usr/local/share/dotnet/dotnet build SpartaCut/SpartaCut.csproj --force

echo ""
echo "📦 Publishing for macOS x64 (Rosetta 2)..."
/usr/local/share/dotnet/dotnet publish SpartaCut/SpartaCut.csproj --runtime osx-x64 --self-contained true -c Debug

echo ""
echo "🚀 Launching SpartaCut via Rosetta 2..."
echo ""
arch -x86_64 ./SpartaCut/bin/Debug/net8.0/osx-x64/publish/SpartaCut
