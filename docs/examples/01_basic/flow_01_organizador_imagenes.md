# Optimización y Conversión Automática de Imágenes a WebP

**Nivel**: 🟢 Básico  
**Archivo de Flujo Importable**: [flow_01_organizador_imagenes.json](./flow_01_organizador_imagenes.json)

## 🎯 Caso de Uso Real
Un fotógrafo o diseñador recibe continuamente imágenes pesadas (JPEG, PNG) y necesita optimizarlas a WebP con calidad 80% para web sin hacerlo manualmente.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[ImageOptimizerNode]
  B -->|Out| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - `SourcePath`: `{RelativeDir}\Input`
  - `Recursive`: `True`
### `ImageOptimizerNode` (ID: `node-opt`)
- **Parámetros**: 
  - `TargetFormat`: `WebP`
  - `Quality`: `80`
  - `MaxWidth`: `1920`
  - `MaxHeight`: `1080`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - `DestinationRoot`: `{RelativeDir}\Output`

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` escanea la carpeta `{RelativeDir}\Input` en busca de imágenes.
2. `ImageOptimizerNode` redimensiona (Max 1920x1080) y convierte a formato WebP con calidad 80.
3. `DestinationSinkNode` guarda los archivos resultantes en `{RelativeDir}\Output`.

## 📋 Requisitos Previos y Datos de Prueba
Tener imágenes (JPG/PNG) en la carpeta de entrada configurada.
