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
  - `DestinationFolder`: `{GlobalOutputDir}`
  - `ArchiveFormat`: `ZIP`
  - `CompressionType`: `Deflate`

> El compresor **dice dónde escribe**: el comprimido va a la carpeta de salida del flujo, que es lo que el nodo
> hace por omisión desde el hito 209. Este flujo declara su salida como `{RelativeDir}`, de modo que aquí
> «la carpeta de salida del flujo» es la carpeta del archivo dentro del origen; para un ejemplo que enseña a
> empaquetar, la carpeta se declara en lugar de dejarla al valor por omisión.
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` toma archivos entrantes.
2. `ArchiveCompressorNode` empaqueta cada archivo en un `.zip` dentro de `{GlobalOutputDir}`.
3. `DestinationSinkNode` guarda los paquetes compresos.

## 📋 Requisitos Previos y Datos de Prueba
Archivos sueltos en carpeta de entrada.
