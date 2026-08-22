# Empaquetado y Compresión Automática en ZIP

**Nivel**: 🟢 Básico  
**Archivo de Flujo Importable**: [flow_08_compresion_zip_automatica.json](./flow_08_compresion_zip_automatica.json)

## 🎯 Caso de Uso Real
Reducir espacio en disco archivando automáticamente archivos listos para guardar.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[ArchiveCompressorNode]
  B -->|Out| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `ArchiveCompressorNode` (ID: `node-zip`)
- **Parámetros**: 
  - `ArchiveFormat`: `ZIP`
  - `CompressionType`: `Deflate`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` toma archivos entrantes.
2. `ArchiveCompressorNode` empaqueta en archivo `.zip`.
3. `DestinationSinkNode` guarda los paquetes compresos.

## 📋 Requisitos Previos y Datos de Prueba
Archivos sueltos en carpeta de entrada.
