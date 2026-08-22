# Pipeline de Transcodificación Vídeo a MP4 y GIF Animado

**Nivel**: 🟠 Avanzado  
**Archivo de Flujo Importable**: [flow_24_transcodificacion_multiformato.json](./flow_24_transcodificacion_multiformato.json)

## 🎯 Caso de Uso Real
Generar automáticamente vistas previas animadas (GIF) junto con la versión principal transcodificada en MP4.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[MediaTranscoder: MP4 1080p]
  B -->|Out| C[MediaTranscoder: GIF Animado]
  C -->|Out| D[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `MediaTranscoderNode` (ID: `node-mp4`)
- **Parámetros**: 
  - `Preset`: `Convertir 1080p H.264 (Universal MP4)`
### `MediaTranscoderNode` (ID: `node-gif`)
- **Parámetros**: 
  - `Preset`: `Convertir a GIF Animado`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` detecta vídeo.
2. `MediaTranscoderNode` transcodifica a MP4 1080p.
3. Un segundo `MediaTranscoderNode` genera el GIF animado a partir del resultado.
4. `DestinationSinkNode` almacena los artefactos generados.

## 📋 Requisitos Previos y Datos de Prueba
Archivo de vídeo MP4/MKV.
