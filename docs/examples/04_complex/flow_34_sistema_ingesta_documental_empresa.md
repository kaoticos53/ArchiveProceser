# Sistema de Ingesta Documental Empresarial con Lotes y Formateo

**Nivel**: 🔴 Complejo  
**Archivo de Flujo Importable**: [flow_34_sistema_ingesta_documental_empresa.json](./flow_34_sistema_ingesta_documental_empresa.json)

## 🎯 Caso de Uso Real
Automatizar la ingesta masiva de facturas e informes corporativos empaquetándolos en lotes manejables para el ERP.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[DocumentProcessorNode]
  B -->|Out| C[VariableInjectorNode]
  C -->|Out| D[AdvancedRenamerNode]
  D -->|Out| E[BatchBufferNode: Size 50]
  E -->|BatchFlushed| F[ArchiveCompressorNode]
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
  - `NameTemplate`: `{Year}_DOC_{Guid}`
### `BatchBufferNode` (ID: `node-buf`)
- **Parámetros**: 
  - `BatchSize`: `50`
### `ArchiveCompressorNode` (ID: `node-zip`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `DocumentProcessorNode` analiza conteo de palabras del documento.
2. `VariableInjectorNode` asigna metadatos de auditoría `Company=EnterpriseCorp`.
3. `AdvancedRenamerNode` aplica la plantilla corporativa `{Year}_DOC_{Guid}`.
4. `BatchBufferNode` acumula hasta 50 elementos.
5. `ArchiveCompressorNode` comprime cada lote en un ZIP.

## 📋 Requisitos Previos y Datos de Prueba
Documentos de texto en carpeta de entrada.
