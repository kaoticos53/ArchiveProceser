# Extracción Automática de Audio MP3 desde Vídeos

**Nivel**: 🟢 Básico  
**Archivo de Flujo Importable**: [flow_02_conversion_media_mp3.json](./flow_02_conversion_media_mp3.json)

## 🎯 Caso de Uso Real
Extracción rápida de pistas de voz o podcasts en MP3 desde archivos de vídeo grabado sin abrir un editor de vídeo.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[MediaTranscoderNode]
  B -->|Out| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - `SourcePath`: `{RelativeDir}\Input`
### `MediaTranscoderNode` (ID: `node-tr`)
- **Parámetros**: 
  - `Preset`: `Extraer Audio MP3`
  - `CustomArguments`: `-vn -c:a libmp3lame -b:a 192k`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - `DestinationRoot`: `{RelativeDir}\Output`

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` detecta vídeos entrantes.
2. `MediaTranscoderNode` aplica el preset 'Extraer Audio MP3'.
3. `DestinationSinkNode` almacena los archivos `.mp3` extraídos.

## 📋 Requisitos Previos y Datos de Prueba
Tener instaladas las herramientas externas FFmpeg o configuradas en la app.
