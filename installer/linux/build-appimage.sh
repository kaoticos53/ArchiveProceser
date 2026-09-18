#!/usr/bin/env bash
# Script para empaquetar FileFlow Studio en formato AppImage en Linux / WSL
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPDIR="${1:-${SCRIPT_DIR}/FileFlow.AppDir}"
OUTPUT="${2:-${SCRIPT_DIR}/FileFlow-x86_64.AppImage}"

if [ ! -d "${APPDIR}" ]; then
    echo "Error: El directorio AppDir '${APPDIR}' no existe." >&2
    exit 1
fi

# Configurar permisos de AppRun y ejecutables
chmod +x "${APPDIR}/AppRun" 2>/dev/null || true
find "${APPDIR}/usr/bin" -type f -exec chmod +x {} + 2>/dev/null || true
find "${APPDIR}" -name "*.sh" -exec chmod +x {} + 2>/dev/null || true

# Comprobar o descargar appimagetool
TOOL_CMD="appimagetool"
if ! command -v appimagetool >/dev/null 2>&1; then
    if [ ! -f "/tmp/appimagetool-root/AppRun" ]; then
        echo "==> Descargando appimagetool en /tmp..."
        mkdir -p /tmp/appimage-kit-download
        cd /tmp/appimage-kit-download
        rm -rf appimagetool* squashfs-root
        curl -sLo appimagetool-x86_64.AppImage "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage" || \
        wget -qO appimagetool-x86_64.AppImage "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
        chmod +x appimagetool-x86_64.AppImage
        ./appimagetool-x86_64.AppImage --appimage-extract >/dev/null 2>&1 || true
        rm -rf /tmp/appimagetool-root
        mv squashfs-root /tmp/appimagetool-root
        cd "${SCRIPT_DIR}"
    fi
    TOOL_CMD="/tmp/appimagetool-root/AppRun"
fi

echo "==> Generando AppImage desde ${APPDIR} hacia ${OUTPUT}..."
export ARCH=x86_64
${TOOL_CMD} "${APPDIR}" "${OUTPUT}"

echo "==> [OK] AppImage generado con éxito en: ${OUTPUT}"
chmod +x "${OUTPUT}" 2>/dev/null || true
