#!/usr/bin/env bash
# ==============================================================================
# FileFlow Studio - Linux Uninstaller
# ==============================================================================
set -e

DISPLAY_NAME="FileFlow Studio"

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}====================================================${NC}"
echo -e "${CYAN}     ${DISPLAY_NAME} - Desinstalador para Linux   ${NC}"
echo -e "${CYAN}====================================================${NC}"

# Detect if system or user install
if [ -d "/opt/fileflow" ] && ([ "$EUID" -eq 0 ] || [ -w "/opt/fileflow" ]); then
    echo -e "${YELLOW}Eliminando instalación a nivel de sistema (/opt/fileflow)...${NC}"
    rm -rf "/opt/fileflow"
    rm -f "/usr/bin/fileflow" "/usr/local/bin/fileflow"
    rm -f "/usr/share/applications/fileflow.desktop"
    rm -f "/usr/share/icons/hicolor/256x256/apps/fileflow.png"
    echo -e "${GREEN}Instalación de sistema eliminada.${NC}"
fi

USER_INSTALL="$HOME/.local/share/fileflow"
if [ -d "$USER_INSTALL" ]; then
    echo -e "${YELLOW}Eliminando instalación de usuario ($USER_INSTALL)...${NC}"
    rm -rf "$USER_INSTALL"
    rm -f "$HOME/.local/bin/fileflow"
    rm -f "$HOME/.local/share/applications/fileflow.desktop"
    rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/fileflow.png"
    echo -e "${GREEN}Instalación de usuario eliminada.${NC}"
fi

# Refresh desktop database
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
    update-desktop-database "/usr/share/applications" 2>/dev/null || true
fi

echo -e "\n${GREEN}====================================================${NC}"
echo -e "${GREEN}  ¡FileFlow Studio ha sido desinstalado con éxito!  ${NC}"
echo -e "${GREEN}====================================================${NC}"
