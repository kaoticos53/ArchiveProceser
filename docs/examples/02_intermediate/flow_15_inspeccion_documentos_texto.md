# Inspección de Contenido y Conteo de Palabras en Documentos

**Nivel**: 🟡 Intermedio  
**Archivo de Flujo Importable**: [flow_15_inspeccion_documentos_texto.json](./flow_15_inspeccion_documentos_texto.json)

## 🎯 Caso de Uso Real
Auditar documentos recibidos y rechazar/filtrar aquellos que estén vacíos o tengan menos de 10 palabras.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[DocumentProcessorNode]
  B -->|Out| C[ExpressionFilterNode]
  C -->|Match| D[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `DocumentProcessorNode` (ID: `node-doc`)
- **Parámetros**: 
  - `ExtractWordCount`: `True`
### `ExpressionFilterNode` (ID: `node-flt`)
- **Parámetros**: 
  - `PropertyPath`: `WordCount`
  - `Operator`: `>=`
  - `TargetValue`: `10`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` busca archivos `.txt` / `.md`.
2. `DocumentProcessorNode` analiza el contenido y extrae `WordCount`.
3. `ExpressionFilterNode` verifica `WordCount >= 10`.
4. Se archiva en el destino si cumple la condición.

## 📋 Requisitos Previos y Datos de Prueba
Archivos de texto en la entrada.
