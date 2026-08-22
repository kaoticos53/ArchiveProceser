# Limpieza Automática de Carpetas Vacías

**Nivel**: 🟢 Básico  
**Archivo de Flujo Importable**: [flow_06_limpieza_carpetas_vacias.json](./flow_06_limpieza_carpetas_vacias.json)

## 🎯 Caso de Uso Real
Mantener limpio el almacenamiento eliminando carpetas residuales huérfanas tras migraciones o borradores.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[EmptyDirectoryCleanerNode]
  B -->|Done| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - `EmitMode`: `DirectoriesOnly`
### `EmptyDirectoryCleanerNode` (ID: `node-clean`)
- **Parámetros**: 
  - `CleanSubdirectories`: `True`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` escanea en modo directorios (`EmitMode=DirectoriesOnly`).
2. `EmptyDirectoryCleanerNode` remueve las carpetas sin archivos.
3. `DestinationSinkNode` reporta la finalización.

## 📋 Requisitos Previos y Datos de Prueba
Estructura de carpetas con subcarpetas vacías.
