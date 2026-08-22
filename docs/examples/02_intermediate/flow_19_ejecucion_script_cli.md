# Ejecución de Comandos CLI Externos

**Nivel**: 🟡 Intermedio  
**Archivo de Flujo Importable**: [flow_19_ejecucion_script_cli.json](./flow_19_ejecucion_script_cli.json)

## 🎯 Caso de Uso Real
Integrar scripts propios o herramientas CLI externas en la cadena de procesamiento de FileFlow.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[CliExecutionNode]
  B -->|Success| C[LogOutputNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `CliExecutionNode` (ID: `node-cli`)
- **Parámetros**: 
  - `ExecutablePath`: `cmd.exe`
  - `ArgumentsTemplate`: `/c echo Verificando {FileName}`
### `LogOutputNode` (ID: `node-log`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` detecta archivos.
2. `CliExecutionNode` ejecuta `ffmpeg -i {CurrentPath} -f null -` para verificar integridad.
3. `LogOutputNode` registra el código de salida de la ejecución CLI.

## 📋 Requisitos Previos y Datos de Prueba
Tener ejecutable accesible en PATH.
