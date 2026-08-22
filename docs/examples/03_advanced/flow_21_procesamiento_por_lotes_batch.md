# Agrupación en Lotes (Batch Buffer) para Compresión Massiva

**Nivel**: 🟠 Avanzado  
**Archivo de Flujo Importable**: [flow_21_procesamiento_por_lotes_batch.json](./flow_21_procesamiento_por_lotes_batch.json)

## 🎯 Caso de Uso Real
Evitar crear 100 archivos ZIP individuales cuando se pueden empaquetar en lotes de 10 archivos.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[BatchBufferNode]
  B -->|BatchFlushed| C[ArchiveCompressorNode]
  C -->|Out| D[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `BatchBufferNode` (ID: `node-buf`)
- **Parámetros**: 
  - `BatchSize`: `10`
  - `FlushTimeoutMs`: `5000`
### `ArchiveCompressorNode` (ID: `node-zip`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` lee archivos continuos.
2. `BatchBufferNode` acumula hasta 10 elementos o 5000ms.
3. Al descargarse el lote (`BatchFlushed`), `ArchiveCompressorNode` empaqueta el grupo en ZIP.

## 📋 Requisitos Previos y Datos de Prueba
10 o más archivos en la carpeta de entrada.
