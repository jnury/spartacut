#!/bin/bash
# Clear macOS icon cache
# Run this if icons don't update after regeneration

echo "🧹 Clearing macOS icon cache..."

# Remove system icon cache
sudo rm -rf /Library/Caches/com.apple.iconservices.store 2>/dev/null

# Find and remove user caches
sudo find /private/var/folders/ \
    \( -name com.apple.dock.iconcache -or -name com.apple.iconservices \) \
    -exec rm -rf {} \; 2>/dev/null

# Touch the app bundle to force refresh
if [ -d "../../build/SpartaCut.app" ]; then
    touch "../../build/SpartaCut.app"
fi

# Restart Dock and Finder
killall Dock 2>/dev/null
killall Finder 2>/dev/null

echo "✓ Icon cache cleared"
echo "✓ Dock and Finder restarted"
echo ""
echo "The app icon should now update. Try opening the app again."
