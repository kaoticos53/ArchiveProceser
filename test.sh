#!/usr/bin/env bash
set -e

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

echo -e "\033[0;36m=========================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Test Suite (Linux)   \033[0m"
echo -e "\033[0;36m=========================================\033[0m"

dotnet test "$SCRIPT_DIR/FileFlow.Tests/FileFlow.Tests.csproj" "$@"
