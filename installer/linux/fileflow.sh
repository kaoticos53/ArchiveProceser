#!/usr/bin/env bash
set -e

# FileFlow Studio - Linux Launcher Script
SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do
  DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE"
done
APP_DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
export FILEFLOW_HOME="$APP_DIR"

if [ -f "$APP_DIR/FileFlow.App" ]; then
    exec "$APP_DIR/FileFlow.App" "$@"
elif [ -f "$APP_DIR/engine/FileFlow.Core.dll" ]; then
    exec dotnet "$APP_DIR/engine/FileFlow.Core.dll" "$@"
elif [ -f "$APP_DIR/FileFlow.Core.dll" ]; then
    exec dotnet "$APP_DIR/FileFlow.Core.dll" "$@"
else
    echo "[ERROR] No se pudo encontrar el binario de FileFlow en $APP_DIR" >&2
    exit 1
fi
