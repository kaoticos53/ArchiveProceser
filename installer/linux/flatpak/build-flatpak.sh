#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"
VERSION="${1:-1.0.0}"
OUTPUT_FILE="${2:-${REPO_ROOT}/dist/FileFlow-${VERSION}-x86_64.flatpak}"

echo -e "\033[0;36m==========================================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Compilador de Paquete Flatpak         \033[0m"
echo -e "\033[0;36m==========================================================\033[0m"

if ! command -v flatpak-builder &> /dev/null; then
    echo -e "\033[0;31m[ERROR] 'flatpak-builder' no está instalado. Instálalo con: sudo apt install flatpak-builder\033[0m"
    exit 1
fi

BUILD_DIR="/tmp/fileflow-flatpak-build"
REPO_DIR="/tmp/fileflow-flatpak-repo"
rm -rf "${BUILD_DIR}" "${REPO_DIR}"
mkdir -p "$(dirname "${OUTPUT_FILE}")"
OUTPUT_DIR="$(cd "$(dirname "${OUTPUT_FILE}")" && pwd)"
OUTPUT_FILE_NAME="$(basename "${OUTPUT_FILE}")"
OUTPUT_FILE="${OUTPUT_DIR}/${OUTPUT_FILE_NAME}"

echo -e "\n\033[0;33m[1/2] Compilando e integrando en Flatpak sandbox...\033[0m"
flatpak-builder \
    --force-clean \
    --repo="${REPO_DIR}" \
    --install-deps-from=flathub \
    --default-branch=stable \
    "${BUILD_DIR}" \
    "${SCRIPT_DIR}/com.fileflowstudio.FileFlow.yml"

echo -e "\n\033[0;33m[2/2] Exportando bundle .flatpak autónomo...\033[0m"
flatpak build-bundle \
    "${REPO_DIR}" \
    "${OUTPUT_FILE}" \
    com.fileflowstudio.FileFlow \
    stable

rm -rf "${BUILD_DIR}" "${REPO_DIR}"

if [ -f "${OUTPUT_FILE}" ]; then
    SIZE=$(du -h "${OUTPUT_FILE}" | cut -f1)
    echo -e "\n\033[0;32m[OK] Paquete Flatpak generado con éxito: ${OUTPUT_FILE} (${SIZE})\033[0m"
else
    echo -e "\n\033[0;31m[ERROR] No se pudo generar el archivo .flatpak\033[0m"
    exit 1
fi
