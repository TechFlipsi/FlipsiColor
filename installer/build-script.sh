#!/usr/bin/env bash
# =============================================================================
# FlipsiColor — Linux Installer Build Script
# Baut .deb Package (Ubuntu/Debian) und .AppImage (universelle Linux-Distributionen)
#
# Voraussetzungen:
#   - .NET 10 SDK installiert
#   - dotnet-tool "linuxdeb" oder manuelles dpkg-deb (für .deb)
#   - AppImageTool (appimagetool) für .AppImage
#
# Usage:
#   ./build-script.sh                  # Baut beide (.deb + .AppImage)
#   ./build-script.sh --deb            # Nur .deb
#   ./build-script.sh --appimage       # Nur .AppImage
#
# Author: TechFlipsi
# Version: 0.4.3
# =============================================================================
set -euo pipefail

# ── Konfiguration ──
APP_NAME="flipsicolor"
APP_DISPLAY_NAME="FlipsiColor"
APP_VERSION="0.5.0"
APP_EXEC="FlipsiColor.Avalonia"
ICON_NAME="flipsicolor"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
AVALONIA_CSProj="$PROJECT_DIR/FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LINUX_DIR="$SCRIPT_DIR/linux"
BUILD_DIR="$SCRIPT_DIR/build"
PUBLISH_DIR="$BUILD_DIR/publish"
DEB_DIR="$BUILD_DIR/deb-staging"
APPDIR_DIR="$BUILD_DIR/AppDir"

# Argumente parsen
BUILD_DEB=true
BUILD_APPIMAGE=true
if [[ "${1:-}" == "--deb" ]]; then
    BUILD_APPIMAGE=false
elif [[ "${1:-}" == "--appimage" ]]; then
    BUILD_DEB=false
fi

echo "=============================================="
echo " FlipsiColor Linux Installer Build Script"
echo " Version: $APP_VERSION"
echo "=============================================="
echo ""

# ── Build-Verzeichnis vorbereiten ──
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR" "$PUBLISH_DIR"

# =============================================================================
# Schritt 1: Avalonia für Linux veröffentlichen
# =============================================================================
echo "[1/4] Veröffentliche FlipsiColor.Avalonia für linux-x64..."
dotnet publish "$AVALONIA_CSProj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_DIR"

if [[ ! -f "$PUBLISH_DIR/$APP_EXEC" ]]; then
    echo "FEHLER: Publish fehlgeschlagen — $APP_EXEC nicht gefunden in $PUBLISH_DIR"
    exit 1
fi

echo "  ✓ Publish abgeschlossen: $PUBLISH_DIR"

# =============================================================================
# Schritt 2: .deb Package bauen (Ubuntu/Debian)
# =============================================================================
build_deb() {
    echo ""
    echo "[2/4] Baue .deb Package für Ubuntu/Debian..."

    # DEB-Struktur erstellen
    rm -rf "$DEB_DIR"
    mkdir -p "$DEB_DIR/DEBIAN"
    mkdir -p "$DEB_DIR/opt/$APP_NAME"
    mkdir -p "$DEB_DIR/usr/bin"
    mkdir -p "$DEB_DIR/usr/share/applications"
    mkdir -p "$DEB_DIR/usr/share/icons/hicolor/256x256/apps"

    # Publish-Dateien kopieren
    cp -r "$PUBLISH_DIR"/* "$DEB_DIR/opt/$APP_NAME/"

    # Symlink im /usr/bin erstellen
    ln -sf "/opt/$APP_NAME/$APP_EXEC" "$DEB_DIR/usr/bin/$APP_NAME"

    # .desktop File kopieren
    cp "$LINUX_DIR/flipsicolor.desktop" "$DEB_DIR/usr/share/applications/"

    # Control-Datei kopieren (mit Abhängigkeiten: ffmpeg, libraw-dev)
    cp "$LINUX_DIR/control" "$DEB_DIR/DEBIAN/control"

    # Icon (falls vorhanden, sonst Platzhalter)
    if [[ -f "$LINUX_DIR/flipsicolor.png" ]]; then
        cp "$LINUX_DIR/flipsicolor.png" "$DEB_DIR/usr/share/icons/hicolor/256x256/apps/"
    elif [[ -f "$PUBLISH_DIR/flipsicolor.png" ]]; then
        cp "$PUBLISH_DIR/flipsicolor.png" "$DEB_DIR/usr/share/icons/hicolor/256x256/apps/"
    else
        # Platzhalter-Icon erstellen (1x1 PNG)
        printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x02\x00\x00\x00\x90wS\xde\x00\x00\x00\x0cIDATx\x9cc\xf8\x0f\x00\x00\x01\x01\x00\x05\xfe\x02\xfe\xa1Yz\xc8\x00\x00\x00\x00IEND\xaeB\x82\xbf' > "$DEB_DIR/usr/share/icons/hicolor/256x256/apps/$ICON_NAME.png"
    fi

    # Berechtigungen setzen
    chmod 755 "$DEB_DIR/DEBIAN"
    chmod 644 "$DEB_DIR/DEBIAN/control"
    chmod 755 "$DEB_DIR/opt/$APP_NAME/$APP_EXEC"
    chmod 644 "$DEB_DIR/usr/share/applications/flipsicolor.desktop"

    # .deb bauen mit dpkg-deb
    local deb_file="$BUILD_DIR/FlipsiColor-$APP_VERSION-linux.deb"
    if command -v dpkg-deb &>/dev/null; then
        dpkg-deb --build --root-owner-group "$DEB_DIR" "$deb_file"
        echo "  ✓ .deb erstellt: $deb_file"
    else
        echo "  ⚠ dpkg-deb nicht verfügbar — .deb-Package wurde staged in $DEB_DIR"
        echo "    Installiere mit: sudo apt install dpkg-dev && dpkg-deb --build $DEB_DIR $deb_file"
    fi
}

# =============================================================================
# Schritt 3: .AppImage bauen (universelle Linux-Distributionen)
# =============================================================================
build_appimage() {
    echo ""
    echo "[3/4] Baue .AppImage..."

    # AppDir-Struktur erstellen
    rm -rf "$APPDIR_DIR"
    mkdir -p "$APPDIR_DIR/usr/bin"
    mkdir -p "$APPDIR_DIR/usr/share/applications"
    mkdir -p "$APPDIR_DIR/usr/share/icons/hicolor/256x256/apps"

    # Publish-Dateien kopieren
    cp -r "$PUBLISH_DIR"/* "$APPDIR_DIR/usr/bin/"

    # AppRun-Script erstellen (AppImage Entry-Point)
    cat > "$APPDIR_DIR/AppRun" << 'APPRUN_EOF'
#!/usr/bin/env bash
# FlipsiColor AppRun — Entry-Point für AppImage
SELF=$(readlink -f "$0")
HERE=$(dirname "$SELF")
export PATH="$HERE/usr/bin:$PATH"
export LD_LIBRARY_PATH="$HERE/usr/lib:$HERE/usr/bin:$LD_LIBRARY_PATH"
exec "$HERE/usr/bin/FlipsiColor.Avalonia" "$@"
APPRUN_EOF
    chmod +x "$APPDIR_DIR/AppRun"

    # .desktop File kopieren (AppImage-Format benötigt .desktop im Root)
    cp "$LINUX_DIR/flipsicolor.desktop" "$APPDIR_DIR/"
    # Exec anpassen für AppImage
    sed -i "s|^Exec=.*|Exec=FlipsiColor.Avalonia|" "$APPDIR_DIR/flipsicolor.desktop"

    # Icon
    if [[ -f "$LINUX_DIR/flipsicolor.png" ]]; then
        cp "$LINUX_DIR/flipsicolor.png" "$APPDIR_DIR/$ICON_NAME.png"
        cp "$LINUX_DIR/flipsicolor.png" "$APPDIR_DIR/usr/share/icons/hicolor/256x256/apps/"
    elif [[ -f "$PUBLISH_DIR/flipsicolor.png" ]]; then
        cp "$PUBLISH_DIR/flipsicolor.png" "$APPDIR_DIR/$ICON_NAME.png"
        cp "$PUBLISH_DIR/flipsicolor.png" "$APPDIR_DIR/usr/share/icons/hicolor/256x256/apps/"
    fi

    # .AppImage bauen mit appimagetool
    local appimage_file="$BUILD_DIR/FlipsiColor-$APP_VERSION.AppImage"
    if command -v appimagetool &>/dev/null; then
        appimagetool "$APPDIR_DIR" "$appimage_file"
        echo "  ✓ .AppImage erstellt: $appimage_file"
    else
        echo "  ⚠ appimagetool nicht verfügbar — AppDir wurde staged in $APPDIR_DIR"
        echo "    Installiere mit:"
        echo "      wget https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage -O appimagetool"
        echo "      chmod +x appimagetool"
        echo "      ./appimagetool $APPDIR_DIR $appimage_file"
    fi
}

# =============================================================================
# Build ausführen
# =============================================================================
if [[ "$BUILD_DEB" == true ]]; then
    build_deb
else
    echo "[2/4] .deb Build übersprungen (--appimage Mode)"
fi

if [[ "$BUILD_APPIMAGE" == true ]]; then
    build_appimage
else
    echo "[3/4] .AppImage Build übersprungen (--deb Mode)"
fi

# =============================================================================
# Schritt 4: Zusammenfassung
# =============================================================================
echo ""
echo "[4/4] Build-Zusammenfassung"
echo "=============================================="
echo " Build-Verzeichnis:  $BUILD_DIR"
if [[ "$BUILD_DEB" == true ]]; then
    if [[ -f "$BUILD_DIR/FlipsiColor-$APP_VERSION-linux.deb" ]]; then
        echo " ✓ .deb Package:     $BUILD_DIR/FlipsiColor-$APP_VERSION-linux.deb"
    else
        echo " ⚠ .deb staged in:   $DEB_DIR (dpkg-deb benötigt)"
    fi
fi
if [[ "$BUILD_APPIMAGE" == true ]]; then
    if [[ -f "$BUILD_DIR/FlipsiColor-$APP_VERSION.AppImage" ]]; then
        echo " ✓ .AppImage:        $BUILD_DIR/FlipsiColor-$APP_VERSION.AppImage"
    else
        echo " ⚠ AppDir staged in: $APPDIR_DIR (appimagetool benötigt)"
    fi
fi
echo "=============================================="
echo " Fertig!"
echo ""