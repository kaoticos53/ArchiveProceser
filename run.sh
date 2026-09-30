#!/usr/bin/env bash
# FileFlow Studio - Host Uno (Skia Desktop) Launcher para Linux/macOS.
# Compila el host Uno (net10.0-desktop) y lo ejecuta. Es el hermano .sh de run.ps1.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${CONFIGURATION:-Debug}"
NO_BUILD=0
for arg in "$@"; do
    [ "$arg" = "--no-build" ] && NO_BUILD=1
done

PROJECT="$SCRIPT_DIR/FileFlow.App.Uno/FileFlow.App.Uno.csproj"
EXE="$SCRIPT_DIR/FileFlow.App.Uno/bin/$CONFIG/net10.0-desktop/FileFlow.App.Uno"

echo "========================================="
echo "  FileFlow Studio - Host Uno (Desktop)   "
echo "========================================="

if [ "$NO_BUILD" -eq 0 ]; then
    echo "Compilando el host Uno ($CONFIG)..."
    dotnet build "$PROJECT" -c "$CONFIG" -p:FileFlowTarget=desktop --nologo -v:m
fi

if [ ! -f "$EXE" ]; then
    echo "[ERROR] No se encontró el ejecutable en '$EXE'. Ejecuta './run.sh' sin --no-build."
    exit 1
fi

exec "$EXE" "$@"
