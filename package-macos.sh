#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${SCRIPT_DIR}"
DIST_DIR="${REPO_ROOT}/dist"
VERSION="${1:-1.0.0}"
ARCH="${2:-arm64}"

echo -e "\033[0;36m==========================================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Empaquetador macOS (.app / DMG)       \033[0m"
echo -e "\033[0;36m  Versión: ${VERSION} | Arquitectura: ${ARCH}             \033[0m"
echo -e "\033[0;36m==========================================================\033[0m"

RID="osx-${ARCH}"
WORK_DIR="${DIST_DIR}/macos_build_${ARCH}"
APP_BUNDLE="${DIST_DIR}/FileFlow Studio.app"

rm -rf "${WORK_DIR}" "${APP_BUNDLE}"
mkdir -p "${WORK_DIR}/payload" "${DIST_DIR}"

echo -e "\n\033[0;33m[1/3] Compilando FileFlow.App para ${RID} (Release, Self-Contained)...\033[0m"
dotnet publish "${REPO_ROOT}/FileFlow.App/FileFlow.App.csproj" \
    -c Release \
    -r "${RID}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o "${WORK_DIR}/payload"

echo -e "\n\033[0;33m[2/3] Ensamblando bundle 'FileFlow Studio.app'...\033[0m"
CONTENTS_DIR="${APP_BUNDLE}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"

mkdir -p "${MACOS_DIR}" "${RESOURCES_DIR}"
cp -r "${WORK_DIR}/payload/"* "${MACOS_DIR}/"
chmod +x "${MACOS_DIR}/FileFlow.App"

echo "APPL????" > "${CONTENTS_DIR}/PkgInfo"

if [ -f "${REPO_ROOT}/installer/macos/Info.plist" ]; then
    sed "s/1\.0\.0/${VERSION}/g" "${REPO_ROOT}/installer/macos/Info.plist" > "${CONTENTS_DIR}/Info.plist"
fi

if [ -f "${REPO_ROOT}/assets/FileFlow.png" ]; then
    cp "${REPO_ROOT}/assets/FileFlow.png" "${RESOURCES_DIR}/FileFlow.png"
fi

echo -e "\n\033[0;33m[3/3] Generando paquetes de distribución (ZIP y DMG)...\033[0m"
cd "${DIST_DIR}"
zip -q -r "FileFlowStudio-v${VERSION}-${RID}.zip" "FileFlow Studio.app"

if command -v hdiutil &> /dev/null; then
    hdiutil create -volname "FileFlow Studio" -srcfolder "FileFlow Studio.app" -ov -format UDZO "FileFlowStudio-v${VERSION}-${RID}.dmg"
    echo -e "\033[0;32m✅ Imagen DMG generada: dist/FileFlowStudio-v${VERSION}-${RID}.dmg\033[0m"
fi

# Limpiar payload intermedio y bundle descomprimido para liberar espacio en disco
rm -rf "${WORK_DIR}" "${APP_BUNDLE}"
echo -e "\n\033[0;32m==========================================================\033[0m"
echo -e "\033[0;32m  ¡Paquetes para macOS listos en ${DIST_DIR}!             \033[0m"
echo -e "\033[0;32m==========================================================\033[0m"
ls -lh "${DIST_DIR}"/*.zip "${DIST_DIR}"/*.dmg 2>/dev/null || true
