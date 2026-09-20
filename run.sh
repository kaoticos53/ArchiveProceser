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
NO_BUILD=false
APP_ARGS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-build|--fast|-n)
            NO_BUILD=true
            shift
            ;;
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

echo -e "\033[0;36m=========================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Launcher (Linux)     \033[0m"
echo -e "\033[0;36m=========================================\033[0m"

if [ "$NO_BUILD" = false ]; then
    echo -e "\n\033[0;33mCompilando FileFlow.slnx ($CONFIG)...\033[0m"
    dotnet build "$SCRIPT_DIR/FileFlow.slnx" -c "$CONFIG"
    if [ $? -ne 0 ]; then
        echo -e "\n\033[0;31m[ERROR] La compilación falló.\033[0m"
        exit 1
    fi
    echo -e "\033[0;32mCompilación exitosa.\033[0m"
else
    echo -e "\n\033[0;33m[Modo Rápido] Omitiendo compilación...\033[0m"
fi

DLL_PATH="$SCRIPT_DIR/FileFlow.App/bin/$CONFIG/net10.0/FileFlow.App.dll"

if [ ! -f "$DLL_PATH" ]; then
    if [ "$CONFIG" = "Debug" ]; then
        FALLBACK_CONFIG="Release"
    else
        FALLBACK_CONFIG="Debug"
    fi
    FALLBACK_PATH="$SCRIPT_DIR/FileFlow.App/bin/$FALLBACK_CONFIG/net10.0/FileFlow.App.dll"
    if [ -f "$FALLBACK_PATH" ]; then
        DLL_PATH="$FALLBACK_PATH"
        CONFIG="$FALLBACK_CONFIG"
    else
        echo -e "\n\033[0;31m[ERROR] No se encontró el binario en '$DLL_PATH'.\033[0m"
        echo -e "Ejecuta './run.sh' sin --no-build para compilar primero."
        exit 1
    fi
fi

echo -e "\n\033[0;32mIniciando FileFlow Studio ($CONFIG)...\033[0m"
exec dotnet "$DLL_PATH" "${APP_ARGS[@]}"
