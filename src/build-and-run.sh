#!/bin/bash

# Bref Clean Build and Run Script
# Ensures a fresh build with no cached artifacts

set -e  # Exit on error

echo "🧹 Cleaning previous build artifacts..."
/usr/local/share/dotnet/dotnet clean Bref/Bref.csproj

echo ""
echo "🔨 Building Bref..."
/usr/local/share/dotnet/dotnet build Bref/Bref.csproj --force

echo ""
echo "📦 Publishing for macOS x64 (Rosetta 2)..."
/usr/local/share/dotnet/dotnet publish Bref/Bref.csproj --runtime osx-x64 --self-contained true -c Debug

echo ""
echo "🚀 Launching Bref via Rosetta 2..."
echo ""
arch -x86_64 ./Bref/bin/Debug/net8.0/osx-x64/publish/Bref
