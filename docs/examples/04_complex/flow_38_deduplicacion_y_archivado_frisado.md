# Deduplicación Masiva y Compresión de Archivo Congelado (Cold Storage)

**Nivel**: 🔴 Complejo  
**Archivo de Flujo Importable**: [flow_38_deduplicacion_y_archivado_frisado.json](./flow_38_deduplicacion_y_archivado_frisado.json)

## 🎯 Caso de Uso Real
Archivar colecciones históricas masivas eliminando duplicados y comprimiendo la copia única en 7Z.

## 📊 Diagrama del Flujo
```mermaid
graph TD
  A[FolderSourceNode] -->|Out| B[HashCalculatorNode: SHA512]
  B -->|Out| C[DeduplicationFilterNode]
  C -->|Unique| D[ArchiveCompressorNode: 7Z]
  C -->|Duplicate| E[OriginalFileActionNode: Cuarentena]
  D -->|Out| F[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `HashCalculatorNode` (ID: `node-hash`)
- **Parámetros**: 
  - `Algorithm`: `SHA512`
### `DeduplicationFilterNode` (ID: `node-dedup`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `ArchiveCompressorNode` (ID: `node-7z`)
- **Parámetros**: 
  - `ArchiveFormat`: `7Z`
### `OriginalFileActionNode` (ID: `node-quar`)
- **Parámetros**: 
  - `ActionType`: `MoveToQuarantine`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` lee la biblioteca.
2. `HashCalculatorNode` calcula el hash SHA-512 de máxima seguridad.
3. `DeduplicationFilterNode` separa elementos únicos de duplicados.
4. Los únicos se comprimen con `ArchiveCompressorNode` (formato 7Z) y se guardan.
5. Los duplicados se mueven a cuarentena.

## 📋 Requisitos Previos y Datos de Prueba
Biblioteca de archivos con elementos duplicados.
