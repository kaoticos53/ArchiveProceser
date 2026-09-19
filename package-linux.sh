#!/usr/bin/env bash
set -e

# Asegurar dotnet en PATH si está en ~/.dotnet
if ! command -v dotnet &> /dev/null; then
    if [ -d "$HOME/.dotnet" ]; then
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"
    else
        echo -e "\033[0;31m[ERROR] No se encontró 'dotnet' en el sistema ni en $HOME/.dotnet.\033[0m"
        exit 1
    fi
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIST_DIR="${SCRIPT_DIR}/dist"
APP_DIR="${DIST_DIR}/app"
VERSION="1.0.0"

echo -e "\033[0;36m==========================================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Empaquetador Universal para Linux     \033[0m"
echo -e "\033[0;36m==========================================================\033[0m"

# Limpieza previa
rm -rf "${DIST_DIR}"
mkdir -p "${APP_DIR}"

# 1. Publicar versión Release autocontenida
echo -e "\n\033[0;33m[1/4] Compilando y publicando FileFlow Studio (Release, Self-Contained)...\033[0m"
dotnet publish "${SCRIPT_DIR}/FileFlow.App/FileFlow.App.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o "${APP_DIR}"

chmod +x "${APP_DIR}/FileFlow.App" 2>/dev/null || true

# 2. Generar Paquete .deb (Ubuntu / Debian / Mint)
echo -e "\n\033[0;33m[2/4] Generando paquete instalable .deb (Ubuntu/Debian)...\033[0m"
DEB_PKG_DIR="${DIST_DIR}/deb-pkg"
mkdir -p "${DEB_PKG_DIR}/DEBIAN"
mkdir -p "${DEB_PKG_DIR}/opt/fileflow"
mkdir -p "${DEB_PKG_DIR}/usr/bin"
mkdir -p "${DEB_PKG_DIR}/usr/share/applications"
mkdir -p "${DEB_PKG_DIR}/usr/share/icons/hicolor/256x256/apps"

cp -r "${APP_DIR}/"* "${DEB_PKG_DIR}/opt/fileflow/"
cp "${SCRIPT_DIR}/assets/FileFlow.png" "${DEB_PKG_DIR}/usr/share/icons/hicolor/256x256/apps/fileflow.png"
cp "${SCRIPT_DIR}/assets/FileFlow.png" "${DEB_PKG_DIR}/opt/fileflow/fileflow.png"
cp "${SCRIPT_DIR}/installer/linux/fileflow.desktop" "${DEB_PKG_DIR}/usr/share/applications/"
ln -sf /opt/fileflow/FileFlow.App "${DEB_PKG_DIR}/usr/bin/fileflow"

cat << EOF > "${DEB_PKG_DIR}/DEBIAN/control"
Package: fileflow
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Depends: libc6, libfontconfig1, libx11-6, libice6, libsm6, libxext6, libxi6, libxrender1, libxtst6
Maintainer: FileFlow Studio <kaoticos@gmail.com>
Description: FileFlow Studio - Node-based visual file processing and ETL automation platform.
 FileFlow Studio is a high-performance visual DAG workflow engine and batch
 file processing studio built with .NET 9 and Avalonia UI.
EOF

cat << 'EOF' > "${DEB_PKG_DIR}/DEBIAN/postinst"
#!/bin/sh
set -e
chmod +x /opt/fileflow/FileFlow.App 2>/dev/null || true
if which update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications 2>/dev/null || true
fi
if which gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
fi
exit 0
EOF
chmod 755 "${DEB_PKG_DIR}/DEBIAN/postinst"

cat << 'EOF' > "${DEB_PKG_DIR}/DEBIAN/postrm"
#!/bin/sh
set -e
if which update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications 2>/dev/null || true
fi
if which gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
fi
exit 0
EOF
chmod 755 "${DEB_PKG_DIR}/DEBIAN/postrm"

dpkg-deb --build --root-owner-group "${DEB_PKG_DIR}" "${DIST_DIR}/fileflow_${VERSION}_amd64.deb" >/dev/null 2>&1 || dpkg-deb --build "${DEB_PKG_DIR}" "${DIST_DIR}/fileflow_${VERSION}_amd64.deb"
rm -rf "${DEB_PKG_DIR}"

# 3. Generar AppImage Universal
echo -e "\n\033[0;33m[3/4] Generando AppImage Universal...\033[0m"
APPIMAGE_DIR="${DIST_DIR}/FileFlow.AppDir"
mkdir -p "${APPIMAGE_DIR}/usr/bin"
mkdir -p "${APPIMAGE_DIR}/usr/share/icons/hicolor/256x256/apps"

cp -r "${APP_DIR}/"* "${APPIMAGE_DIR}/usr/bin/"
cp "${SCRIPT_DIR}/assets/FileFlow.png" "${APPIMAGE_DIR}/fileflow.png"
cp "${SCRIPT_DIR}/assets/FileFlow.png" "${APPIMAGE_DIR}/usr/share/icons/hicolor/256x256/apps/fileflow.png"
cp "${SCRIPT_DIR}/installer/linux/fileflow.desktop" "${APPIMAGE_DIR}/fileflow.desktop"
cp "${SCRIPT_DIR}/installer/linux/AppRun" "${APPIMAGE_DIR}/AppRun"
chmod +x "${APPIMAGE_DIR}/AppRun" "${APPIMAGE_DIR}/usr/bin/FileFlow.App"

"${SCRIPT_DIR}/installer/linux/build-appimage.sh" "${APPIMAGE_DIR}" "${DIST_DIR}/FileFlow-${VERSION}-x86_64.AppImage"
rm -rf "${APPIMAGE_DIR}"

# 4. Generar Tarball Portable .tar.gz
echo -e "\n\033[0;33m[4/5] Generando archivo portable comprimido .tar.gz...\033[0m"
PORTABLE_DIR="${DIST_DIR}/FileFlow-Linux-Portable"
mkdir -p "${PORTABLE_DIR}"
cp -r "${APP_DIR}/"* "${PORTABLE_DIR}/"
cp "${SCRIPT_DIR}/installer/linux/install.sh" "${PORTABLE_DIR}/" 2>/dev/null || true
cp "${SCRIPT_DIR}/installer/linux/uninstall.sh" "${PORTABLE_DIR}/" 2>/dev/null || true
cp "${SCRIPT_DIR}/installer/linux/fileflow.desktop" "${PORTABLE_DIR}/" 2>/dev/null || true
cp "${SCRIPT_DIR}/assets/FileFlow.png" "${PORTABLE_DIR}/fileflow.png" 2>/dev/null || true
chmod +x "${PORTABLE_DIR}/install.sh" "${PORTABLE_DIR}/uninstall.sh" "${PORTABLE_DIR}/FileFlow.App" 2>/dev/null || true

tar -czf "${DIST_DIR}/FileFlow-${VERSION}-Linux-x64-Portable.tar.gz" -C "${DIST_DIR}" "FileFlow-Linux-Portable"
rm -rf "${PORTABLE_DIR}" "${APP_DIR}"

# 5. Generar Paquete Flatpak (.flatpak) si flatpak-builder está presente
if command -v flatpak-builder &> /dev/null; then
    echo -e "\n\033[0;33m[5/5] Generando bundle autónomo Flatpak (.flatpak)...\033[0m"
    bash "${SCRIPT_DIR}/installer/linux/flatpak/build-flatpak.sh" "${VERSION}" "${DIST_DIR}/FileFlow-${VERSION}-x86_64.flatpak" || echo -e "\033[0;33m[ADVERTENCIA] No se pudo generar el Flatpak automáticamente.\033[0m"
else
    echo -e "\n\033[0;33m[5/5] 'flatpak-builder' no detectado: omitiendo generación de .flatpak (usa: sudo apt install flatpak-builder)\033[0m"
fi

echo -e "\n\033[0;32m==========================================================\033[0m"
echo -e "\033[0;32m  ¡Paquetes para Linux generados con éxito en dist/!       \033[0m"
echo -e "\033[0;32m==========================================================\033[0m"
ls -lh "${DIST_DIR}"
