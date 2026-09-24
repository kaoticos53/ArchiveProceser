# Inspección de Documentos por Número de Líneas

**Nivel**: 🟡 Intermedio  
**Archivo de Flujo Importable**: [flow_15_inspeccion_documentos_texto.json](./flow_15_inspeccion_documentos_texto.json)

## 🎯 Caso de Uso Real
Auditar documentos recibidos y descartar los que no tienen contenido suficiente: se exige un mínimo de **2 líneas**.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[DocumentProcessorNode]
  B -->|Out| C[ExpressionFilterNode]
  C -->|True| D[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `DocumentProcessorNode` (ID: `node-doc`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

> Publica `DocumentType`, `EstimatedPageCount` y `DocumentLineCount` sobre cada documento.
> **No** publica `WordCount`: ningún nodo del producto cuenta palabras, así que un filtro sobre esa propiedad no
> se cumple nunca y el ejemplo terminaba en verde sin entregar un solo archivo (destapado en el hito 204). El
> ejemplo filtra por el número de líneas, que es lo que el nodo realmente mide.

### `ExpressionFilterNode` (ID: `node-flt`)
- **Parámetros**: 
  - `Property`: `DocumentLineCount`
  - `Operator`: `>=`
  - `ComparisonValue`: `2`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` busca archivos `.txt` / `.md`.
2. `DocumentProcessorNode` analiza el contenido y publica `DocumentLineCount`.
3. `ExpressionFilterNode` verifica `DocumentLineCount >= 2`.
4. Se archiva en el destino si cumple la condición.

## 📋 Requisitos Previos y Datos de Prueba
Archivos de texto en la entrada.
