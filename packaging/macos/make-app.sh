#!/bin/bash
# Builds ESA.app, a double-clickable macOS application bundle.
#
#   packaging/macos/make-app.sh            # for this Mac's own chip
#   packaging/macos/make-app.sh osx-x64    # for an Intel Mac
#   VERSION=1.2 packaging/macos/make-app.sh
#
# The bundle lands in artifacts/macos/<runtime>/ESA.app, which git ignores. It is
# signed ad hoc, which Apple Silicon needs before it will run anything; that is
# enough for the Mac it was built on. Another Mac will refuse it until it is
# signed with a Developer ID and notarised, or until its quarantine flag is
# cleared with `xattr -dr com.apple.quarantine ESA.app`.

set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "make-app.sh needs macOS: it uses codesign and plutil." >&2
    exit 1
fi

case "${1:-}" in
    "")
        case "$(uname -m)" in
            arm64)  RUNTIME="osx-arm64" ;;
            x86_64) RUNTIME="osx-x64" ;;
            *) echo "Unrecognised chip '$(uname -m)'; pass osx-arm64 or osx-x64." >&2; exit 1 ;;
        esac ;;
    osx-arm64|osx-x64) RUNTIME="$1" ;;
    *) echo "Usage: $0 [osx-arm64|osx-x64]" >&2; exit 1 ;;
esac

VERSION="${VERSION:-1.0}"

# Work from the repository root, wherever the script is called from.
cd "$(dirname "$0")/../.."

OUT="artifacts/macos/$RUNTIME"
PUBLISH="$OUT/publish"
APP="$OUT/ESA.app"

echo "Publishing for $RUNTIME..."
rm -rf "$OUT"
dotnet publish src/App.Ui/App.Ui.csproj -c Release -r "$RUNTIME" --self-contained -o "$PUBLISH"

echo "Assembling $APP..."
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# The whole publish folder, not just the executable: Avalonia loads its native
# Skia and HarfBuzz libraries from beside App.Ui. ESA.ini is read from there too
# (AppContext.BaseDirectory), so in a bundle it lives in Contents/MacOS.
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/App.Ui"
cp packaging/macos/ESA.icns "$APP/Contents/Resources/ESA.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>ESA</string>
    <key>CFBundleDisplayName</key>
    <string>ESA - Engine Simulation and Analysis</string>
    <key>CFBundleIdentifier</key>
    <string>com.pangtuwi.esa</string>
    <key>CFBundleExecutable</key>
    <string>App.Ui</string>
    <key>CFBundleIconFile</key>
    <string>ESA</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST
plutil -lint "$APP/Contents/Info.plist"

echo "Signing ad hoc..."
codesign --force --deep --sign - "$APP"
codesign --verify --deep "$APP"

echo "Built $APP"
echo "Open it with: open \"$APP\""
