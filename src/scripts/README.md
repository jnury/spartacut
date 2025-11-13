# SpartaCut Build Scripts

This directory contains build automation scripts for generating icons and creating distributable packages for macOS and Windows.

## Icon Sources

- **Master PNG**: `media/icon.png` (1024x1024) - Used for traditional .icns/.ico icons
- **Liquid Glass**: `media/icon.icon/` - macOS Sequoia asset catalog for modern translucent icons

All platform-specific icon files are generated from these sources.

## Icon Generation

Icons are automatically generated during the build process, but you can also generate them manually:

### macOS Traditional Icon (.icns)

```bash
./generate-icon-macos.sh [source-png] [output-icns]
```

**Default:** Generates `src/SpartaCut/SpartaCut.icns` from `media/icon.png`

**Outputs:** Creates a .icns file with all 10 required sizes for macOS (16×16 through 1024×1024 with @2x variants)

### macOS Liquid Glass (Assets.car)

```bash
./compile-assets-macos.sh [icon-source] [output-car]
```

**Default:** Compiles `media/icon.icon/` to `src/SpartaCut/Assets.car`

**Requirements:** Xcode Command Line Tools (`xcode-select --install`)

**Effect:** Creates modern translucent icon with gradient fills for macOS Sequoia+

### Windows Icon (.ico)

```powershell
.\generate-icon-windows.ps1 [-SourcePng path] [-OutputIco path]
```

**Default:** Generates `src/SpartaCut/SpartaCut.ico` from `media/icon.png`

**Requirements:** Works with or without ImageMagick. For best results:
```powershell
winget install ImageMagick.ImageMagick
```

## macOS Workflow

### Build

```bash
cd src/scripts
./build-macos.sh
```

**Actions:**
1. Cleans previous build artifacts
2. Generates .icns icon (fallback for older macOS)
3. Compiles Assets.car (Liquid Glass for macOS Sequoia+)
4. Publishes self-contained application for osx-x64 (includes .NET runtime)

### Run

```bash
cd src/scripts
./run-macos.sh
```

**Actions:**
- Runs the built executable directly via Rosetta 2
- No .app bundle creation
- Useful for quick testing during development

### Bundle

```bash
cd src/scripts
./bundle-macos.sh
```

**Actions:**
1. Generates fresh icons (.icns + Assets.car)
2. Publishes self-contained application
3. Creates proper .app bundle structure
4. Copies both Assets.car (Liquid Glass) and .icns (fallback) to Resources
5. Creates Info.plist with `CFBundleIconName=AppIcon` (for asset catalog)
6. Embeds icon into executable resource fork (for Dock/Activity Monitor)
7. Code signs bundle (ad-hoc for development)
8. Outputs bundle to `build/SpartaCut.app`

**Run the bundle:**
```bash
arch -x86_64 open -W build/SpartaCut.app
```

## Windows Workflow

### Build

```powershell
cd src\scripts
.\build-windows.ps1 [-Configuration Debug|Release]
```

**Actions:**
1. Cleans previous build artifacts
2. Generates .ico icon from `media\icon.png`
3. Publishes self-contained application for win-x64 (includes .NET runtime)

### Run

```powershell
cd src\scripts
.\run-windows.ps1 [-Configuration Debug|Release]
```

**Actions:**
- Runs the built executable directly
- Useful for quick testing during development

### Bundle

```powershell
cd src\scripts
.\bundle-windows.ps1 [-Configuration Release] [-Runtime win-x64|win-arm64]
```

**Actions:**
1. Generates fresh .ico icon
2. Publishes self-contained application for specified runtime
3. Creates distribution bundle in `build/SpartaCut-{version}-{runtime}/`

**Supported runtimes:**
- `win-x64` - 64-bit Intel/AMD (default)
- `win-arm64` - ARM64 (Surface, etc.)

## Icon Embedding (macOS)

The `embed-icon-macos.sh` script is called automatically by `bundle-macos.sh` but can be used manually:

```bash
./embed-icon-macos.sh <executable-path> <icon-path>
```

**Purpose:** Embeds the icon into the macOS executable's resource fork, which is required for the icon to appear in:
- Dock when app is running
- Activity Monitor
- Force Quit dialog

**Requirements:** Uses macOS built-in tools (sips, DeRez, Rez, SetFile)

## Utilities

### Verify Icon Setup (macOS)

```bash
./verify-icon-macos.sh
```

Checks:
- Bundle exists
- Icon files in Resources (both .icns and Assets.car)
- Color profiles (should have none)
- Custom icon flag on executable
- Info.plist configuration

### Clear Icon Cache (macOS)

```bash
./clear-icon-cache-macos.sh
```

Clears system icon cache and restarts Dock/Finder. Run this if:
- Icons don't update after regeneration
- Old icon still appears after rebuilding

## Project Structure

```
SpartaCut/
├── media/
│   ├── icon.png                    # Master PNG icon (1024x1024)
│   └── icon.icon/            # Liquid Glass asset catalog
│       ├── icon.json               # Asset configuration
│       └── Assets/
│           └── logo.png            # Icon image for Liquid Glass
├── src/
│   ├── scripts/
│   │   ├── generate-icon-macos.sh       # .icns generation
│   │   ├── compile-assets-macos.sh      # Assets.car compilation
│   │   ├── generate-icon-windows.ps1    # .ico generation
│   │   ├── embed-icon-macos.sh          # Resource fork embedding
│   │   ├── build-macos.sh               # macOS build
│   │   ├── run-macos.sh                 # macOS run
│   │   ├── bundle-macos.sh              # macOS bundle creation
│   │   ├── build-windows.ps1            # Windows build
│   │   ├── run-windows.ps1              # Windows run
│   │   ├── bundle-windows.ps1           # Windows bundle creation
│   │   ├── verify-icon-macos.sh         # Icon verification
│   │   └── clear-icon-cache-macos.sh    # Cache clearing
│   └── SpartaCut/
│       ├── SpartaCut.icns               # Generated macOS .icns
│       ├── Assets.car                   # Generated macOS asset catalog
│       └── SpartaCut.ico                # Generated Windows .ico
└── build/
    └── SpartaCut.app                    # macOS bundle output
```

## Updating Icons

### Traditional Icon (Both Platforms)

1. Replace `media/icon.png` with your new icon (must be 1024x1024 PNG)
2. Run the appropriate build script - icons are regenerated automatically

For manual regeneration:
- macOS: `./generate-icon-macos.sh`
- Windows: `.\generate-icon-windows.ps1`

### Liquid Glass Icon (macOS Sequoia+)

1. Update `media/icon.icon/Assets/logo.png` with your new icon image
2. Edit `media/icon.icon/icon.json` to adjust:
   - Gradient colors (fill-specializations)
   - Translucency level
   - Shadow settings
3. Run `./compile-assets-macos.sh` or `./bundle-macos.sh`

**Configuration Tips:**
- `translucency.value`: 0.0-1.0 (0.5 = 50% transparent)
- `fill-specializations`: Separate gradients for light/dark mode
- `glass: false`: Keeps original colors, prevents monochrome effect

## macOS Icon Formats Explained

### Traditional (.icns) - Fallback

- **Used by:** macOS 10.15-14.x (Catalina through Sonoma)
- **Appearance:** Flat, opaque icon with original colors
- **Location:** `Contents/Resources/SpartaCut.icns`
- **Info.plist:** `CFBundleIconFile`

### Liquid Glass (Assets.car) - Modern

- **Used by:** macOS 15+ (Sequoia and later)
- **Appearance:** Translucent 3D icon with gradient fills
- **Location:** `Contents/Resources/Assets.car`
- **Info.plist:** `CFBundleIconName`

**Both formats are included** in the bundle. macOS automatically chooses:
- **Sequoia+**: Uses Assets.car (Liquid Glass)
- **Older macOS**: Falls back to .icns (traditional)

## Notes

- **macOS requires x64**: LibVLCSharp requires Rosetta 2 on Apple Silicon Macs
- **Icon formats**: .icns + Assets.car for macOS, .ico for Windows
- **Resource fork**: macOS icons must be embedded in executable to appear in Dock
- **Code signing**: Required for Assets.car to work properly
- **Clean builds**: Build scripts automatically clean artifacts for fresh icons

## Troubleshooting

### macOS: Icon doesn't appear in Dock

1. Verify icon was embedded: `GetFileInfo path/to/executable | grep "custom icon"`
2. Clear icon cache: `./clear-icon-cache-macos.sh`
3. Rebuild with: `./bundle-macos.sh`

### macOS: Icon appears monochrome/transformed

**If you want traditional flat icon:**
- This happens when Assets.car is missing or invalid
- Rebuild with `./bundle-macos.sh` to regenerate Assets.car

**If you want Liquid Glass icon:**
- This is normal behavior for macOS Sequoia with asset catalogs
- Icon becomes translucent with gradient fills
- Check `media/icon.icon/icon.json` for customization

### macOS: actool command not found

Install Xcode Command Line Tools:
```bash
xcode-select --install
```

### Windows: Icon generation fails

- Install ImageMagick for best results: `winget install ImageMagick.ImageMagick`
- The script has a fallback .NET-based method but it's less reliable

### Permission errors on scripts

Make scripts executable:
```bash
chmod +x src/scripts/*.sh
```

### Icon cache not clearing

Try manually:
```bash
sudo rm -rf /Library/Caches/com.apple.iconservices.store
sudo find /private/var/folders/ -name "com.apple.dock.iconcache" -exec rm -rf {} \; 2>/dev/null
killall Dock && killall Finder
```
