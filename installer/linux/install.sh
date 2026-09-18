#!/usr/bin/env bash
# ==============================================================================
# FileFlow Studio - Universal Linux Installer
# Compatible with Ubuntu, Debian, Fedora, Arch, CentOS, openSUSE, Alpine, etc.
# ==============================================================================
set -e

APP_NAME="fileflow"
DISPLAY_NAME="FileFlow Studio"
SCRIPT_DIR="$( cd -P "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}====================================================${NC}"
echo -e "${CYAN}      ${DISPLAY_NAME} - Instalador para Linux     ${NC}"
echo -e "${CYAN}====================================================${NC}"

# Detect install mode (System-wide vs User-local)
INSTALL_MODE="system"
if [ "$1" == "--user" ] || [ "$EUID" -ne 0 ]; then
    INSTALL_MODE="user"
fi

if [ "$INSTALL_MODE" == "system" ]; then
    INSTALL_PREFIX="/opt/fileflow"
    BIN_DIR="/usr/bin"
    APPS_DIR="/usr/share/applications"
    ICONS_DIR="/usr/share/icons/hicolor/256x256/apps"
    echo -e "${YELLOW}Modo de instalación: Sistema (${INSTALL_PREFIX})${NC}"
else
    INSTALL_PREFIX="$HOME/.local/share/fileflow"
    BIN_DIR="$HOME/.local/bin"
    APPS_DIR="$HOME/.local/share/applications"
    ICONS_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
    echo -e "${YELLOW}Modo de instalación: Usuario (${INSTALL_PREFIX})${NC}"
fi

# Create target directories
mkdir -p "$INSTALL_PREFIX"
mkdir -p "$BIN_DIR"
mkdir -p "$APPS_DIR"
mkdir -p "$ICONS_DIR"

echo -e "${CYAN}-> Copiando archivos de la aplicación a ${INSTALL_PREFIX}...${NC}"
if [ -d "$SCRIPT_DIR/app" ]; then
    cp -r "$SCRIPT_DIR/app/"* "$INSTALL_PREFIX/"
else
    cp -r "$SCRIPT_DIR/"* "$INSTALL_PREFIX/" 2>/dev/null || true
fi

# Ensure execution permissions
chmod +x "$INSTALL_PREFIX/FileFlow.App" 2>/dev/null || true
chmod +x "$INSTALL_PREFIX/fileflow" 2>/dev/null || true
chmod +x "$INSTALL_PREFIX/fileflow.sh" 2>/dev/null || true

echo -e "${CYAN}-> Creando enlace ejecutable en ${BIN_DIR}/fileflow...${NC}"
cat << 'EOF' > "$BIN_DIR/fileflow"
#!/usr/bin/env bash
INSTALL_DIR="__INSTALL_DIR__"
if [ -f "$INSTALL_DIR/fileflow.sh" ]; then
    exec "$INSTALL_DIR/fileflow.sh" "$@"
elif [ -f "$INSTALL_DIR/FileFlow.App" ]; then
    exec "$INSTALL_DIR/FileFlow.App" "$@"
elif [ -f "$INSTALL_DIR/engine/FileFlow.Core.dll" ]; then
    exec dotnet "$INSTALL_DIR/engine/FileFlow.Core.dll" "$@"
elif [ -f "$INSTALL_DIR/FileFlow.Core.dll" ]; then
    exec dotnet "$INSTALL_DIR/FileFlow.Core.dll" "$@"
else
    echo "Error: No se encontró FileFlow en $INSTALL_DIR" >&2
    exit 1
fi
EOF
sed -i "s|__INSTALL_DIR__|$INSTALL_PREFIX|g" "$BIN_DIR/fileflow"
chmod +x "$BIN_DIR/fileflow"

echo -e "${CYAN}-> Instalando icono y acceso directo de escritorio...${NC}"
if [ -f "$SCRIPT_DIR/assets/fileflow.png" ]; then
    cp "$SCRIPT_DIR/assets/fileflow.png" "$ICONS_DIR/fileflow.png"
elif [ -f "$INSTALL_PREFIX/assets/fileflow.png" ]; then
    cp "$INSTALL_PREFIX/assets/fileflow.png" "$ICONS_DIR/fileflow.png"
fi

if [ -f "$SCRIPT_DIR/fileflow.desktop" ]; then
    cp "$SCRIPT_DIR/fileflow.desktop" "$APPS_DIR/fileflow.desktop"
    if [ "$INSTALL_MODE" == "user" ]; then
        sed -i "s|Exec=fileflow|Exec=$BIN_DIR/fileflow|g" "$APPS_DIR/fileflow.desktop"
    fi
fi

# Update desktop database and icon cache if available
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APPS_DIR" 2>/dev/null || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$(dirname "$ICONS_DIR")" 2>/dev/null || true
fi

echo -e "\n${GREEN}====================================================${NC}"
echo -e "${GREEN}  ¡Instalación de FileFlow completada con éxito!   ${NC}"
echo -e "${GREEN}  Ejecuta 'fileflow' en tu terminal o búscalo en el  ${NC}"
echo -e "${GREEN}  menú de aplicaciones del sistema.                 ${NC}"
echo -e "${GREEN}====================================================${NC}"
