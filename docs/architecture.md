# Arquitectura y Diseño Técnico - FileFlow Studio

## 1. Visión General del Sistema

**FileFlow Studio** es una plataforma de automatización y procesamiento masivo de archivos (*Batch File Processing & Workflow Automation System*) desarrollada con **C# 13**, **.NET 9** y **WPF (Windows Presentation Foundation)**. El sistema permite diseñar, simular, depurar y ejecutar flujos de trabajo visuales basados en grafos dirigidos (DAG - *Directed Acyclic Graphs*, tuberías reactivas con buffers, bifurcaciones de control y barreras de sincronización).

El proyecto se rige por un **desacoplamiento estricto por capas**, asegurando que los contratos base (`FileFlow.Sdk`) sean puros y reutilizables, independientes de la lógica de presentación o dependencias externas pesadas.

---

## 2. Diagrama de Arquitectura del Sistema

```mermaid
graph TD
    subgraph Capa_Presentacion ["Capa de Presentación (FileFlow.App)"]
        UI["WPF UI (Nodify / MVVM / Virtualized DataGrid)"]
        VM["ViewModels (Main, Editor, Node, ControlBar, Log)"]
        CV["ValueConverters (LogLevel, Badges, EnumToBool)"]
        UI --> VM
        UI --> CV
    end

    subgraph Capa_Orquestacion ["Capa de Orquestación y Telemetría (FileFlow.Core)"]
        WE["WorkflowExecutor (DAG & Sub-Graphs)"]
        PL["PluginLoader (Assembly Load Context)"]
        FW["FolderWatcherService"]
        JE["ExecutionJournalService"]
        ACM["AdaptiveConcurrencyManager"]
        SQL["SqliteLogStore (In-Memory Ring Buffer & SQLite Analytics)"]
        WE --> JE
        WE --> ACM
        WE --> SQL
    end

    subgraph Capa_Plugins ["Capa de Extensión / 24 Nodos (FileFlow.Plugin.*)"]
        P_FS["FileFlow.Plugin.FileSystem (10 Nodos)"]
        P_ARC["FileFlow.Plugin.Archives (3 Nodos)"]
        P_IMG["FileFlow.Plugin.Images (2 Nodos)"]
        P_INT["FileFlow.Plugin.Integrations (3 Nodos)"]
        P_LOG["FileFlow.Plugin.Logic (5 Nodos)"]
        P_HASH["FileFlow.Plugin.Hashing (2 Nodos)"]
    end

    subgraph Capa_Contratos ["Capa Base de Contratos (FileFlow.Sdk)"]
        SDK_Node["IFlowNode & NodeExecutionStatus"]
        SDK_Item["FileItemContext & Memoized Accessors"]
        SDK_Ctx["IFlowExecutionContext & Telemetry Logger"]
        SDK_Tpl["VariableTemplateResolver"]
        SDK_Rec["StructuredLogRecord"]
    end

    VM --> WE
    VM --> PL
    VM --> SQL
    PL --> P_FS
    PL --> P_ARC
    PL --> P_IMG
    PL --> P_INT
    PL --> P_LOG
    PL --> P_HASH

    P_FS --> SDK_Node
    P_ARC --> SDK_Node
    P_IMG --> SDK_Node
    P_INT --> SDK_Node
    P_LOG --> SDK_Node
    P_HASH --> SDK_Node

    WE --> SDK_Node
    WE --> SDK_Item
    WE --> SDK_Ctx
```

---

## 3. Flujo de Datos Principal (Data Flow Pipeline)

```mermaid
sequenceDiagram
    autonumber
    participant UI as WPF Editor / Nodify
    participant Exec as WorkflowExecutor
    participant Telemetry as SqliteLogStore (In-Memory)
    participant NodeA as FolderSourceNode
    participant NodeB as ImageOptimizerNode
    participant NodeC as DestinationSinkNode
    participant LogView as LogViewModel / DataGrid

    UI->>Exec: ExecuteWorkflowAsync(Graph, Settings)
    Exec->>Telemetry: Iniciar Sesión de Ejecución
    Exec->>NodeA: ExecuteAsync(ItemContext, FlowContext)
    NodeA->>Telemetry: EnqueueLog(StructuredLogRecord [INFO])
    NodeA-->>Exec: Emit(item1, item2...)
    
    par Procesamiento Paralelo / Asíncrono
        Exec->>NodeB: ExecuteAsync(item1, FlowContext)
        NodeB->>Telemetry: EnqueueLog(StructuredLogRecord [INFO/DEBUG])
        NodeB-->>Exec: Emit(optimized_item1)
        Exec->>NodeC: ExecuteAsync(optimized_item1, FlowContext)
        NodeC->>Telemetry: EnqueueLog(StructuredLogRecord [INFO])
        NodeC-->>Exec: Emit(target_item1)
    end

    Telemetry-->>LogView: Flush en Lote cada 40 ms (UI Thread Dispatcher)
    LogView-->>UI: Renderizado Virtualizado 120 FPS en DataGrid
    Exec-->>UI: Notificación de Finalización (Journal & Métricas)
```

---

## 4. Descripción de Módulos y Capas

### 4.1. `FileFlow.Sdk` (Capa de Contratos Puros)
- **Propósito**: Define los contratos de interfaces, modelos de dominio fundamentales y utilidades compartidas. Cero dependencias externas pesadas.
- **Componentes Clave**:
  - `IFlowNode`: Contrato unificado que deben implementar todos los nodos ejecutables (`ExecuteAsync`, `ValidateConfiguration`, `Category`, `Inputs`, `Outputs`).
  - `FileItemContext`: Encapsula el ciclo de vida de un archivo en el grafo (`Id`, `OriginalPath`, `CurrentPath`, `Size`, `Variables`, `Metadata`). Incluye memoización zero-alloc para `IdString`, `ShortIdString` y resolución reactiva de `FileName`.
  - `IFlowExecutionContext`: Proporciona al nodo acceso al token de cancelación (`CancellationToken`), resolución de variables, almacenamiento de estado en memoria compartida, emisión de elementos y telemetría estructurada (`context.Log`).
  - `StructuredLogRecord`: Registro inmutable con metadatos de ejecución, timestamps precisos, identificador de nodo, `ItemId`, `DurationMs`, tamaño y payload `DetailsJson`.
  - `VariableTemplateResolver`: Motor de interpolación de cadenas que sustituye sintaxis `{Ext}`, `{FileName}`, `{Date:yyyy-MM-dd}`, `{SizeMB}`, `{Hash:sha256}` y variables inyectadas.

### 4.2. `FileFlow.Core` (Capa de Orquestación y Telemetría)
- **Propósito**: Ejecución determinista del DAG, resolución de dependencias topológicas, paralelismo adaptativo y almacenamiento analítico de logs.
- **Componentes Clave**:
  - `WorkflowExecutor`: Motor de ejecución asíncrono no bloqueante con soporte para sub-grafos, paralelismo multinúcleo configurable, puntos de interrupción (*Breakpoints*) y silenciado selectivo de logs.
  - `SqliteLogStore`: Motor analítico y almacén de logs estructurados en memoria de ultra-alto rendimiento basado en SQLite (`:memory:`). Emplea canal no bloqueante `Channel<StructuredLogRecord>`, inserción transaccional por lotes en una conexión persistente `_keepAliveConnection` protegida por `System.Threading.Lock`, alcanzando más de 82.000 logs/segundo.
  - `PluginLoader`: Cargador dinámico de extensiones basado en `AssemblyLoadContext` aislado, capaz de descubrir e instanciar nodos desde ensamblados externos.
  - `FolderWatcherService`: Servicio de monitorización reactiva de directorios basado en `FileSystemWatcher` con amortiguación anti-rebote (*debounce*) y control de bloqueo de lectura.
  - `ExecutionJournalService`: Sistema de auditoría y persistencia histórica de ejecuciones y transacciones atómicas.
  - `AdaptiveConcurrencyManager`: Ajusta dinámicamente el número de tareas concurrentes según la saturación de I/O y CPU.

### 4.3. `FileFlow.Plugin.*` (Capa de Plugins y Nodos de Producción)
Colección modular de 24 nodos de procesamiento organizados por dominio:
1. **`FileFlow.Plugin.FileSystem` (10 Nodos)**: `FolderSourceNode`, `DestinationSinkNode`, `FileRelocatorNode`, `AdvancedRenamerNode`, `DocumentProcessorNode`, `DirectoryInspectorNode`, `EmptyDirectoryCleanerNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`, `VariableInjectorNode`.
2. **`FileFlow.Plugin.Archives` (3 Nodos)**: `SmartUnpackNode` (descompresión inteligente auto-aplanado), `ArchiveCompressorNode` (Zip, Tar, GZip, 7z, BZip2 con ratios de compresión), `ArchiveFilterNode` (detección de partes .r00, .part1).
3. **`FileFlow.Plugin.Images` (2 Nodos)**: `ImageOptimizerNode` (redimensionamiento, calidad WebP/JPEG/PNG y métricas de ahorro %), `ExifMetadataNode` (extracción estructurada de metadatos de cámara y geolocalización).
4. **`FileFlow.Plugin.Integrations` (3 Nodos)**: `CliExecutionNode` (subprocesos externos asíncronos), `WebhookNotificationNode` (notificaciones HTTP POST/PUT con payloads JSON dinámicos), `MediaTranscoderNode` (transcodificación de audio/video con FFmpeg y telemetría periódica).
5. **`FileFlow.Plugin.Logic` (5 Nodos)**: `SwitchCaseNode` (enrutamiento condicional multi-rama), `ExpressionFilterNode` (evaluador de expresiones booleanas), `ThrottleDelayNode` (control de caudal temporal), `BatchBufferNode` (acumulación por lotes/tamaño), `ForkJoinBarrierNode` (sincronización de ramas paralelas).
6. **`FileFlow.Plugin.Hashing` (2 Nodos)**: `HashCalculatorNode` (MD5, SHA1, SHA256, SHA384, SHA512, xxHash3, xxHash64), `DeduplicationFilterNode` (detección de duplicados en tiempo real por firma criptográfica).

### 4.4. `FileFlow.App` (Capa de Presentación WPF / MVVM)
- **Propósito**: Interfaz de usuario rica, reactiva y accesible construida sobre el patrón MVVM y la biblioteca de grafos Nodify.
- **Componentes Clave**:
  - `EditorView` & `EditorViewModel`: Lienzo visual interactivo con soporte de drag & drop, conexión de pines, zoom infinito, minimapa y control visual de ejecución.
  - `LogView` & `LogViewModel`: Consola de telemetría moderna y compacta con virtualización completa (`Recycling`), selector de filtros por severidad con contadores en vivo, input de búsqueda instantáneo con borrado rápido (`✕`), pill badges translúcidos, alineación vertical uniforme (`RowHeight="24"`), y acordeón de detalles JSON con botón de **Trazabilidad** por `#ShortItemId`.
  - `NodeCardView` & `NodeViewModel`: Tarjetas visuales de nodo con controles de cabecera: Toggle de Breakpoint (rojo) y Toggle de Silenciado de Logs (`≡` cian brillante / gris atenuado).

---

## 5. Registros de Decisiones Arquitectónicas (ADRs)

### ADR-001: Adopción de .NET 9 y C# 13
- **Contexto**: El procesamiento masivo de archivos requiere máxima eficiencia de memoria, paralelismo sin sobrecarga y sincronización ligera.
- **Decisión**: Utilizar `net9.0` con `<LangVersion>13</LangVersion>`, `<Nullable>enable</Nullable>` y las nuevas primitivas `System.Threading.Lock`.
- **Consecuencias**: Código más seguro frente a nulos, menor presión en el Garbage Collector y rendimiento I/O optimizado mediante `ValueTask` y `IAsyncEnumerable`.

### ADR-002: Telemetría Analítica en Memoria con SQLite In-Memory
- **Contexto**: Mostrar cientos de miles de registros de logs en un DataGrid sin congelar la UI y permitiendo búsquedas instantáneas por texto, nodo, archivo o ID de flujo.
- **Decisión**: Implementar `SqliteLogStore` con base de datos en memoria (`Data Source=:memory:;Mode=Memory;Cache=Shared`), canalización asíncrona `System.Threading.Channels.Channel` e inserciones transaccionales por lotes reutilizando una conexión persistente.
- **Consecuencias**: Búsquedas e indexación instantáneas sin I/O en disco, rendimiento >82.000 logs/segundo y desacoplamiento total entre los hilos de trabajo y el hilo de la UI.

### ADR-003: Pureza Absoluta de `FileFlow.Sdk`
- **Contexto**: Los desarrolladores de plugins necesitan una base estable sin arrastrar dependencias pesadas de UI (WPF) o librerías externas innecesarias.
- **Decisión**: Mantener `FileFlow.Sdk` exclusivamente con dependencias del BCL estándar de .NET.
- **Consecuencias**: Facilidad para crear nuevos plugins, pruebas unitarias ultrarrápidas y arquitectura desacoplada y mantenible.
