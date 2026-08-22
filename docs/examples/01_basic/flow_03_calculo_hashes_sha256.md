# Generación e Inyección de Hash SHA-256 en Metadatos

**Nivel**: 🟢 Básico  
**Archivo de Flujo Importable**: [flow_03_calculo_hashes_sha256.json](./flow_03_calculo_hashes_sha256.json)

## 🎯 Caso de Uso Real
Verificar la integridad de archivos recibidos mediante el cálculo inmediato del hash SHA-256 antes de moverlos al almacén.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[HashCalculatorNode]
  B -->|Out| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `HashCalculatorNode` (ID: `node-hash`)
- **Parámetros**: 
  - `Algorithm`: `SHA256`
  - `HashMetadataKey`: `Hash:SHA256`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` captura archivos entrantes.
2. `HashCalculatorNode` calcula el hash SHA-256 y lo guarda en `Hash:SHA256`.
3. `DestinationSinkNode` deposita el archivo en el destino conservando sus metadatos.

## 📋 Requisitos Previos y Datos de Prueba
Archivos de prueba en la carpeta de entrada.
