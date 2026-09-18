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
CONFIG="Debug"
APP_ARGS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --release|-r)
            CONFIG="Release"
            shift
            ;;
        --debug|-d)
            CONFIG="Debug"
            shift
            ;;
        *)
            APP_ARGS+=("$1")
            shift
            ;;
    esac
done

DLL_PATH="$SCRIPT_DIR/FileFlow.App/bin/$CONFIG/net9.0/FileFlow.App.dll"

if [ ! -f "$DLL_PATH" ]; then
    if [ "$CONFIG" = "Debug" ]; then
        FALLBACK_CONFIG="Release"
    else
        FALLBACK_CONFIG="Debug"
    fi
    FALLBACK_PATH="$SCRIPT_DIR/FileFlow.App/bin/$FALLBACK_CONFIG/net9.0/FileFlow.App.dll"
    if [ -f "$FALLBACK_PATH" ]; then
        DLL_PATH="$FALLBACK_PATH"
        CONFIG="$FALLBACK_CONFIG"
    else
        echo -e "\033[0;33m[AVISO] No se encontró el binario compilado en:\033[0m"
        echo -e "  $DLL_PATH"
        echo -e "\nPor favor, compila la solución ejecutando: ./run.sh"
        exit 1
    fi
fi

echo -e "\033[0;36m=========================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Fast Launch (NoBuild)\033[0m"
echo -e "\033[0;36m=========================================\033[0m"
echo -e "\033[0;32m[OK] Iniciando FileFlow Studio ($CONFIG)...\033[0m"

exec dotnet "$DLL_PATH" "${APP_ARGS[@]}"
