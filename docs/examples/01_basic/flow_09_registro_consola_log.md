# Auditoría y Registro Formateado en Consola

**Nivel**: 🟢 Básico  
**Archivo de Flujo Importable**: [flow_09_registro_consola_log.json](./flow_09_registro_consola_log.json)

## 🎯 Caso de Uso Real
Monitorear en tiempo real el paso de elementos a través de la tubería sin alterar los archivos.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[LogOutputNode]
  B -->|Out| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `LogOutputNode` (ID: `node-log`)
- **Parámetros**: 
  - `LogLevel`: `Information`
  - `LogMessageTemplate`: `Procesando archivo: {FileName} ({FileSizeBytes} bytes)`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` lee archivos.
2. `LogOutputNode` imprime en el log el nombre del archivo y tamaño.
3. `DestinationSinkNode` deposita el archivo.

## 📋 Requisitos Previos y Datos de Prueba
Cualquier archivo de prueba.
