#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo -e "\033[0;36m=========================================\033[0m"
echo -e "\033[0;36m  FileFlow Studio - Clean Artifacts      \033[0m"
echo -e "\033[0;36m=========================================\033[0m"

echo "Eliminando carpetas bin y obj..."
find "$SCRIPT_DIR" -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true

echo -e "\033[0;32m[OK] Limpieza completada con éxito.\033[0m"
