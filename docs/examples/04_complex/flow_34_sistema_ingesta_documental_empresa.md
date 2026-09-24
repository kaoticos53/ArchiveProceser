# Sistema de Ingesta Documental Empresarial con Lotes y Formateo

**Nivel**: 🔴 Complejo  
**Archivo de Flujo Importable**: [flow_34_sistema_ingesta_documental_empresa.json](./flow_34_sistema_ingesta_documental_empresa.json)

## 🎯 Caso de Uso Real
Automatizar la ingesta masiva de facturas e informes corporativos empaquetándolos en tandas manejables para el ERP, sin que la etapa de empaquetado arranque con toda la entrada a la vez.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[DocumentProcessorNode]
  B -->|Out| C[VariableInjectorNode]
  C -->|Out| D[AdvancedRenamerNode]
  D -->|Out| E[BatchBufferNode: Size 50]
  E -->|ItemOut| F[ArchiveCompressorNode]
  F -->|Out| G[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `DocumentProcessorNode` (ID: `node-doc`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `VariableInjectorNode` (ID: `node-inj`)
- **Parámetros**: 
  - `Company`: `EnterpriseCorp`
### `AdvancedRenamerNode` (ID: `node-ren`)
- **Parámetros**: 
  - `PipelineName`: `Renombrado Corporativo`
  - `RenameMode`: `DirectInPlace` *(el renombrado tiene que ocurrir en disco: el compresor de más abajo trabaja sobre rutas reales, y en modo `Virtual` el nombre solo cambia en los metadatos)*
  - `CollisionStrategy`: `AutoIncrement`
  - `MethodSteps`: `NewName` sobre `FullName` con la plantilla `{Year}_DOC_{Guid}`
### `BatchBufferNode` (ID: `node-buf`)
- **Parámetros**: 
  - `BatchSize`: `50`
### `ArchiveCompressorNode` (ID: `node-zip`)
- **Parámetros**: 
  - `DestinationFolder`: `{GlobalOutputDir}/Archivado` *(los comprimidos van a una subcarpeta propia de la carpeta de salida del flujo, no junto a los documentos; el destino por omisión del nodo ya es la salida del flujo, y aquí se le añade su propia carpeta para no mezclarlos con el resto)*
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `DocumentProcessorNode` analiza el documento (tipo, páginas estimadas y número de líneas).
2. `VariableInjectorNode` asigna metadatos de auditoría `Company=EnterpriseCorp`.
3. `AdvancedRenamerNode` renombra en disco con la plantilla corporativa `{Year}_DOC_{Guid}` (`CollisionStrategy: AutoIncrement` resuelve cualquier choque de nombre).
4. `BatchBufferNode` acumula los documentos renombrados hasta llegar a 50.
5. Al soltarse el lote por `ItemOut`, `ArchiveCompressorNode` empaqueta cada elemento del lote en un ZIP dentro de `{GlobalOutputDir}/Archivado`.
6. **La tanda que no llega a llenarse también sale**: al terminar la ejecución, lo que quede dentro del búfer se entrega igual —con su marcador por `BatchCompleted` y `BatchIncomplete = true`—, de modo que una ingesta de menos de 50 documentos no termina sin empaquetar.

## 📋 Requisitos Previos y Datos de Prueba
Documentos de texto en carpeta de entrada. El flujo se comporta igual si la entrada supera el lote que si no.
