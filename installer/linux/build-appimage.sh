#!/usr/bin/env bash
# Script para empaquetar FileFlow Studio en formato AppImage en Linux / WSL
set -e

ORIG_DIR="$(pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPDIR="${1:-${SCRIPT_DIR}/FileFlow.AppDir}"
OUTPUT="${2:-${SCRIPT_DIR}/FileFlow-x86_64.AppImage}"

# Asegurar rutas absolutas y existencia de directorios
if [ -d "${APPDIR}" ]; then
    APPDIR="$(cd "${APPDIR}" && pwd)"
fi

mkdir -p "$(dirname "${OUTPUT}")"
OUTPUT_DIR="$(cd "$(dirname "${OUTPUT}")" && pwd)"
OUTPUT_FILE="$(basename "${OUTPUT}")"
OUTPUT="${OUTPUT_DIR}/${OUTPUT_FILE}"

if [ ! -d "${APPDIR}" ]; then
    echo "Error: El directorio AppDir '${APPDIR}' no existe." >&2
    exit 1
fi

# Configurar permisos de AppRun y ejecutables
chmod +x "${APPDIR}/AppRun" 2>/dev/null || true
find "${APPDIR}/usr/bin" -type f -exec chmod +x {} + 2>/dev/null || true
find "${APPDIR}" -name "*.sh" -exec chmod +x {} + 2>/dev/null || true

# Comprobar o descargar appimagetool y runtime moderno
TOOL_CMD="appimagetool"
RUNTIME_FILE="/tmp/appimagetool-root/runtime-x86_64"

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
        cd "${ORIG_DIR}"
    fi
    TOOL_CMD="/tmp/appimagetool-root/AppRun"
fi

if [ ! -f "${RUNTIME_FILE}" ]; then
    echo "==> Descargando runtime estático universal para AppImage..."
    mkdir -p "$(dirname "${RUNTIME_FILE}")"
    curl -sLo "${RUNTIME_FILE}" "https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64" 2>/dev/null || \
    wget -qO "${RUNTIME_FILE}" "https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64" 2>/dev/null || true
    chmod +x "${RUNTIME_FILE}" 2>/dev/null || true
fi

echo "==> Generando AppImage desde ${APPDIR} hacia ${OUTPUT}..."
export ARCH=x86_64

if [ -f "${RUNTIME_FILE}" ] && [ -s "${RUNTIME_FILE}" ]; then
    ${TOOL_CMD} --runtime-file "${RUNTIME_FILE}" -n "${APPDIR}" "${OUTPUT}"
else
    ${TOOL_CMD} -n "${APPDIR}" "${OUTPUT}"
fi

echo "==> [OK] AppImage generado con éxito en: ${OUTPUT}"
chmod +x "${OUTPUT}" 2>/dev/null || true

