#!/usr/bin/env bash
# FileFlow Studio - Host Uno (Skia Desktop) Launcher rápido para Linux/macOS: no compila.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${CONFIGURATION:-Debug}"
EXE="$SCRIPT_DIR/FileFlow.App.Uno/bin/$CONFIG/net10.0-desktop/FileFlow.App.Uno"

echo "========================================="
echo "  FileFlow Studio - Host Uno (Desktop)   "
echo "========================================="

if [ ! -f "$EXE" ]; then
    echo "[ERROR] No se encontró el ejecutable en '$EXE'."
    echo "Compila primero con: ./run.sh"
    exit 1
fi

exec "$EXE" "$@"
