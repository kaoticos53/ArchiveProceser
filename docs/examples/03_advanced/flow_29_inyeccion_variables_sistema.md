# Plantillas Compuestas con Variables del Sistema y GUID

**Nivel**: 🟠 Avanzado  
**Archivo de Flujo Importable**: [flow_29_inyeccion_variables_sistema.json](./flow_29_inyeccion_variables_sistema.json)

## 🎯 Caso de Uso Real
Generar nombres universales únicos libres de colisión en entornos distribuidos masivos.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[VariableInjectorNode]
  B -->|Out| C[AdvancedRenamerNode: Guid+Vars]
  C -->|Out| D[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `VariableInjectorNode` (ID: `node-inj`)
- **Parámetros**: 
  - `AppID`: `FileFlowV1`
### `AdvancedRenamerNode` (ID: `node-ren`)
- **Parámetros**: 
  - `NameTemplate`: `{Year}_{AppID}_{Guid}_{FileNameNoExt}{FileExt}`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` detecta archivos.
2. `VariableInjectorNode` asigna la variable `AppID=FileFlowV1`.
3. `AdvancedRenamerNode` aplica la plantilla `{Year}_{AppID}_{Guid}_{FileNameNoExt}{FileExt}`.
4. `DestinationSinkNode` guarda los archivos garantizados sin colisión.

## 📋 Requisitos Previos y Datos de Prueba
Archivos de prueba.
