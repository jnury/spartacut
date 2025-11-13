#!/bin/bash
# SpartaCut macOS Bundle Script
# Creates a proper .app bundle for distribution

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$SRC_DIR/SpartaCut/SpartaCut.csproj"
ICON_SOURCE="$SRC_DIR/../media/icon"

# Read version from .csproj
VERSION=$(grep '<Version>' "$PROJECT_FILE" | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/')

echo "🎨 Generating macOS icon (.icns fallback)..."
"$SCRIPT_DIR/generate-icon-macos.sh" "$ICON_SOURCE.png" "$SRC_DIR/SpartaCut/SpartaCut.icns"

echo ""
echo "🎨 Compiling macOS asset catalog (Liquid Glass)..."
"$SCRIPT_DIR/compile-assets-macos.sh" "$ICON_SOURCE.icon" "$SRC_DIR/SpartaCut/Assets.car"

echo ""
echo "📦 Publishing for macOS x64 (Rosetta 2)..."
/usr/local/share/dotnet/dotnet publish "$PROJECT_FILE" --runtime osx-x64 --self-contained true -c Debug

PUBLISH_DIR="$SRC_DIR/SpartaCut/bin/Debug/net8.0/osx-x64/publish"
BUILD_DIR="$SRC_DIR/../build"
APP_BUNDLE="$BUILD_DIR/SpartaCut.app"

echo ""
echo "🎨 Creating macOS .app bundle..."

# Create build directory if it doesn't exist
mkdir -p "$BUILD_DIR"

# Remove old bundle if it exists
rm -rf "$APP_BUNDLE"

# Create .app bundle structure
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy all published files to MacOS directory
cp -R "$PUBLISH_DIR"/* "$APP_BUNDLE/Contents/MacOS/"

# Copy icons to Resources
cp "$SRC_DIR/SpartaCut/SpartaCut.icns" "$APP_BUNDLE/Contents/Resources/"
cp "$SRC_DIR/SpartaCut/Assets.car" "$APP_BUNDLE/Contents/Resources/"

# Create Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Sparta Cut</string>
    <key>CFBundleDisplayName</key>
    <string>Sparta Cut</string>
    <key>CFBundleIdentifier</key>
    <string>com.spartacut.app</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>SpartaCut</string>
    <key>CFBundleIconFile</key>
    <string>SpartaCut.icns</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

echo ""
echo "🔏 Code signing bundle (ad-hoc)..."
# Ad-hoc signing is sufficient for local development
# For distribution, use: codesign -s "Developer ID Application: Your Name" --deep --force "$APP_BUNDLE"
codesign --force --deep --sign - "$APP_BUNDLE" 2>/dev/null && echo "   ✓ Bundle signed" || echo "   ⚠️  Signing failed (bundle may still work)"

echo ""
echo "✓ Bundle created successfully: $APP_BUNDLE"
echo ""
echo "To run the app:"
echo "  arch -x86_64 open -W \"$APP_BUNDLE\""
