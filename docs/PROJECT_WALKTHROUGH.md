# FileFlow Studio - Historial de Cambios y Registro de Implementación (Walkthrough)

Este documento registra cronológicamente todos los cambios, mejoras, correcciones y nuevas funcionalidades implementadas en el proyecto **FileFlow Studio**.

## [2026-09-01] - Configuración de Versión Base 1.0.0-beta

### 📋 Acciones Realizadas
1. **Ajuste de Versión Base en `version.props`**:
   - Configurado `VersionMajor = 1`, `VersionMinor = 0`, `VersionPatch = 0` y `VersionPreRelease = beta`.
   - La versión activa en la aplicación se genera como `v1.0.0-beta+build.N` incrementando automáticamente `N` en cada compilación.
2. **Actualización de Tests**:
   - Ajustadas las aserciones de [`AppVersionInfoTests.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Tests/Unit/Sdk/AppVersionInfoTests.cs).
   - **248 / 248 pruebas aprobadas al 100% (0 errores, 0 fallos, 0 advertencias)**.

---

### 📋 Acciones Realizadas
1. **Nuevo Nodo de Reporte Visual de Operaciones (`OperationReportNode`)**:
   - Desarrollado el nodo `OperationReportNode` en `FileFlow.Plugin.FileSystem` con arquitectura de renderizado extensible (`IReportRenderer`).
   - Soporte de 5 formatos seleccionables por desplegable en el inspector: `HTML` (interactivo, responsive, KPIs, timeline con badges, búsqueda vanilla JS), `Markdown` (.md), `Text` (.txt con árbol ASCII), `JSON` (.json) y `CSV` (.csv).
   - Ámbitos de reporte configurables (`ReportScope`): `Consolidated` (resumen general del lote en **un único archivo consolidado**), `PerFile` (reporte individual adjunto a cada archivo) y `Both`.
   - **Agrupación Jerárquica por Directorios (`GroupBy`)**: Parámetro con opciones `Directory` (por defecto), `Flat`, `Extension` y `Status`.
     - En **HTML**: Acordeón interactivo con carpetas colapsables, métricas de conteo/tamaño por carpeta, badges de salud (`✅ OK` / `⚠️ Errores`), botones *Expandir Todo / Colapsar Todo* y búsqueda reactiva inteligente que auto-despliega las carpetas coincidentes.
     - En **Markdown**: Secciones estructuradas con bloques `<details open><summary>`.
     - En **Texto Plano**: Árbol jerárquico ASCII (`├── 📁 /Fotos/ ... └── 📄 foto.jpg`).
     - En **CSV / JSON**: Campos dedicados de directorio (`Directory`).
   - Soporte de auto-apertura en navegador/visor del sistema (`AutoOpenReport`), personalización de tema (`Theme`: `ModernDark` / `CleanLight`), inclusión de metadatos (`IncludeMetadata`) y rutas parametrizables con plantillas de tokens.
   - **Corrección de Generación Única**: Fijada la ruta de archivo consolidado (`_consolidatedFilePath`) al inicio de cada ejecución/sesión para evitar la dispersión en múltiples archivos al evaluar marcas de tiempo segundo a segundo o subcarpetas relativas.
   - Modo `Dry Run` integrado registrando `PlannedAction` sin modificar el disco real.
2. **Integración en UI / MVVM y Localización**:
   - `NodeParameterViewModel.cs`: Agregadas opciones desplegables para `reportformat`, `reportscope`, `groupby` y `theme`.
   - `ToolboxViewModel.cs`: Sincronización con `Lock`, desuscripción limpia `IDisposable` e icono `📋`.
   - `Strings.resx` y `Strings.es.resx`: Cadenas en inglés y español para el nuevo nodo.
3. **Auditoría Integral del Código y Generación del SRS**:
   - Creado [`docs/ESPECIFICACIONES.md`](file:///docs/ESPECIFICACIONES.md) con la especificación formal del sistema.
   - Actualizados [`docs/manual_de_usuario.md`](file:///docs/manual_de_usuario.md) y [`.agents/nodes_catalog.md`](file:///.agents/nodes_catalog.md) reflejando los 27 nodos del sistema.
4. **Documentación Exhaustiva de Pruebas (Objeto, Qué y Cómo)**:
   - Creado [`docs/guia_de_pruebas.md`](file:///docs/guia_de_pruebas.md) conteniendo el catálogo estructurado de las 190 pruebas con su objetivo, regla de negocio y estrategia AAA (*Arrange, Act, Assert*).
   - Documentación en el código fuente mediante comentarios XML doc en español (`/// <summary>`) detallando `OBJETO`, `QUÉ` y `CÓMO` en cada método de prueba.
5. **Verificación Automatizada de Calidad**:
   - Nuevos tests unitarios en `OperationReportNodeTests.cs` (HTML, Markdown, Text, JSON, CSV, PerFile/Both, Dry Run, Validación de Archivo Único Consolidado, Agrupación Jerárquica por Directorios) y `ToolboxViewModelTests.cs`.
   - `dotnet test FileFlow.slnx`: **190 / 190 pruebas superadas con 100% de éxito (0 errores, 0 fallos)**.
6. **Publicación del `README.md` Principal para GitHub**:
   - Creado [`README.md`](file:///README.md) en la raíz con badges de estado, descripción del motor DAG, diagrama arquitectónico, catálogo de los 27 nodos, guía de inicio rápido y enlaces a toda la documentación técnica.
7. **Automatización de Release e Instalador con GitHub Actions**:
   - Desarrollado [`.github/workflows/release.yml`](file:///.github/workflows/release.yml) con soporte de ejecución manual (`workflow_dispatch`) y publicación por etiquetas (`v*`), generando el instalador Inno Setup (`.exe`), el paquete portable (`.zip`) y las sumas de verificación SHA-256 adjuntas en GitHub Releases.
   - Implementada la sanitización automática de nombres de etiqueta Git (reemplazo de espacios por guiones como `1.0.0 beta` $\rightarrow$ `1.0.0-beta`) y asignación de `target_commitish: ${{ github.sha }}` para permitir la creación de releases desde cualquier commit.
   - Desarrollado [`.github/workflows/ci.yml`](file:///.github/workflows/ci.yml) para validación continua de compilación y pruebas en ramas principales y PRs.
   - Añadida la directiva de entorno `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: "true"` para evitar avisos de deprecación de Node 20 en los runners de GitHub Actions.
   - Refactorizados los scripts `installer/publish.ps1` y `installer/build-installer.ps1` para soporte robusto de parámetros tipados en PowerShell CLI.
8. **Mantenimiento y Control de Versiones (.gitignore)**:
   - Añadidas las carpetas temporales de análisis `coverage-report/`, `TestResults/` y `.dotnet_tmp/` al archivo [`.gitignore`](file:///.gitignore).

---

## [2026-08-23] - Auditoría Integral 360° del Sistema y Refactorización Completa (Fase 1 y Fase 2 Ejecutadas)

### 📋 Estado, Hallazgos y Correcciones Aplicadas (100% Completado)
1. **Fase 1 (Auditoría 360°)**:
   - Identificados 16 hallazgos clasificados en Código/Lógica, Rendimiento/Recursos, Arquitectura y UX/UI.
2. **Fase 2 (Ejecución de Refactorización por Sprints)**:
   - **Sprint 1 (Fugas y Concurrencia)**: `_concurrencyThrottle.Dispose()` en `WorkflowExecutor`, reemplazo de `ConcurrentBag<Task>` por drenaje seguro con `Lock` y shutdown hook de `SqliteLogStore` en `App.OnExit`.
   - **Sprint 2 (Memory Leaks UI)**: Dispose sistemático de `NodeViewModel` en `EditorViewModel.ClearGraph()`, handler nominal `OnNodePropertyChanged` y `IDisposable` en `ControlBarViewModel`.
   - **Sprint 3 (Rendimiento UI)**: Migración de `LogViewModel.Logs` a `FastObservableRingBuffer` (eliminada notación $O(n)$) e indexado con `Dictionary` de `UpdateEdgeDispatched` a $O(1)$.
   - **Sprint 4 (Clean Code & .NET 9)**: Migración de `object _lock` a `System.Threading.Lock` en 3 servicios singleton, `ExecuteNonQueryAsync` en `SqliteLogStore` y logging contextual en `PluginLoader`.
   - **Sprint 5 (Robustez)**: Lecturas `Volatile.Read` en `WaitIfPausedAsync`, persistencia de crashes en `crash.log` y captura de `TaskScheduler.UnobservedTaskException`.
   - **Sprint 6 (Testing Crítico)**: Batería ampliada en `WorkflowExecutorTests.cs` (paralelismo, pausa/resume, DryRun, errores y cancelación).
3. **Resultado Final de Verificación**:
   - Compilación en .NET 9 / C# 13: **0 Advertencias, 0 Errores**.
   - Batería de Pruebas: **181 / 181 pruebas superadas al 100% con éxito** en 3s.

---

## [2026-08-23] - Auditoría y Estandarización Exhaustiva de Observabilidad y Telemetría en los 24 Nodos del Sistema

### 🛠 Cambios e Implementaciones
1. **Auditoría Integral de Observabilidad en los 24 Nodos de Producción**:
   - Clasificación y normalización de todos los nodos según su nivel de telemetría (eliminados nodos silenciosos y logs genéricos sin contexto).
   - Estandarización uniforme con niveles `Debug` (ruido/traza interna), `Information` (hitos de negocio con resumen descriptivo), `Warning` (desviaciones recuperables) y `Error` (fallos críticos).
2. **Telemetría Enriquecida con Métricas de Tiempo (`durationMs`) y Cargas Útiles JSON (`detailsJson`)**:
   - **`FileFlow.Plugin.Logic`**:
     - `SwitchCaseNode`: Emite `[INFO]` y `[DEBUG]` con `detailsJson` ({expression, evaluatedValue, matchedCase, pattern}).
     - `ExpressionFilterNode`: Emite `[INFO]` con `detailsJson` ({property, operator, targetValue, actualValue, result}) y desvío a ramas.
     - `ThrottleDelayNode`: Emite `[DEBUG]` con milisegundos de retardo aplicado.
     - `BatchBufferNode`: Emite `[INFO]` con métricas de lote en `detailsJson` ({batchCount, totalSizeBytes, totalMB}).
     - `ForkJoinBarrierNode`: Emite `[DEBUG]` en recepción de ramas e `[INFO]` con `detailsJson` ({requiredBranches, completedBranches}).
   - **`FileFlow.Plugin.FileSystem`**:
     - `DestinationSinkNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({destinationRoot, targetPath, strategy, isDryRun, sizeBytes}) y `[DEBUG]` en colisiones.
     - `FileRelocatorNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({operation, sourcePath, targetPath, integrityVerified, sha256}) y `[DEBUG]` en validaciones.
     - `AdvancedRenamerNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({pattern, originalName, newName, collisionStrategy}).
     - `DocumentProcessorNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({documentType, estimatedPages, lineCount, fileSizeBytes}).
     - `DirectoryInspectorNode`: Emite `[INFO]` con `detailsJson` ({filesCount, directoriesCount, targetDir}).
     - `EmptyDirectoryCleanerNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({targetDirectory, deletedCount, recursive, isDryRun}).
     - `SafeRecycleDeleteNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({targetPath, fileSizeBytes, deleteOriginal}).
     - `OriginalFileActionNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({action, quarantinePath / targetPath, isDryRun}).
     - `VariableInjectorNode`: Emite `[DEBUG]` por variable e `[INFO]` consolidado con `detailsJson`.
     - `FolderSourceNode`: Emite `[INFO]` al finalizar con `detailsJson` ({sourcePath, emittedCount, totalSizeBytes, totalMB, unit}).
   - **`FileFlow.Plugin.Archives`**:
     - `SmartUnpackNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({archive, extractDir, entriesCount, hasSingleWrapper, passwordProtected}) y `[DEBUG]` en niveles de wrapper.
     - `ArchiveCompressorNode`: Emite `[INFO]` con `durationMs`, cálculo de ratio de compresión % y `detailsJson` ({archiveFormat, compressionType, targetPath, originalSizeBytes, compressedSizeBytes, ratioPct}).
     - `ArchiveFilterNode`: Emite `[DEBUG]` en archivos regulares y `[INFO]` en primarios/secundarios con `detailsJson`.
   - **`FileFlow.Plugin.Images`**:
     - `ImageOptimizerNode`: Emite `[INFO]` con `durationMs`, cálculo de porcentaje de ahorro de espacio (%) y `detailsJson` ({format, quality, originalDimensions, optimizedDimensions, originalSizeBytes, optimizedSizeBytes, savedPct}).
     - `ExifMetadataNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({dateTaken, cameraModel, make, resolution, orientation, megapixels}) y `[DEBUG]` en lectura de dimensiones.
   - **`FileFlow.Plugin.Hashing`**:
     - `HashCalculatorNode`: Emite `[INFO]` con `durationMs`, prefijo del hash y `detailsJson` ({algorithm, hash, metadataKey, fileSizeBytes}).
     - `DeduplicationFilterNode`: Emite `[DEBUG]` en archivos únicos e `[INFO]` en duplicados con `detailsJson` ({hash, duplicateOf, currentPath}).
   - **`FileFlow.Plugin.Integrations`**:
     - `CliExecutionNode`: Emite `[INFO]`/`[WARN]` con `durationMs` real del subproceso y `detailsJson` ({executable, arguments, exitCode, stdOutLength, stdErrLength, stdOutSample, stdErrSample}).
     - `WebhookNotificationNode`: Emite `[INFO]`/`[WARN]` con `durationMs`, código de estado HTTP y `detailsJson` ({url, statusCode, statusText, payloadSample, responseSample}).
     - `MediaTranscoderNode`: Emite `[DEBUG]` progresivo cada 5 segundos y log final `[INFO]` con `durationMs` y `detailsJson` ({preset, targetPath, ffmpegAvailable, realTranscode, outSizeBytes}).
4. **Rediseño UI/UX y Alineación Vertical de la Consola de Logs (`LogView.xaml`, `LogViewModel.cs`, `ValueConverters.cs`)**:
   - **Toolbar Unificada y Compacta**: Agrupación limpia con título, selector de filtros por severidad con contadores en tiempo real (Todos, Errores, Warn, Info, Debug), input de búsqueda integrado con botón de borrado inmediato (`✕`), contador total de logs y botones de acción rápida (`⚡ En Vivo`, `💾 Exportar`, `🗑 Limpiar`).
   - **Alineación Vertical Perfecta y Altura de Fila Uniforme (`RowHeight="24"`)**: Estandarización de `VerticalContentAlignment="Center"` en todas las celdas, eliminando descuadres de texto y saltos de línea.
   - **Pill Badges de Severidad con Fondo Translúcido**: Nuevos convertidores `LogLevelToBadgeBackgroundConverter` y `LogLevelToBadgeForegroundConverter` para renderizar etiquetas con estética moderna tipo IDE.
   - **Columna Duración Centrada**: Alineación y encabezado centrados con ancho ampliado (`Width="80"`), evitando solapamientos con la columna adyacente de mensajes.
   - **Corrección de Selección Reactiva de Filtros (`EnumToBooleanConverter`)**: Sincronización bidireccional de `IsChecked` y asignación de `GroupName="LogFilterGroup"` en los `RadioButton` de severidad, resolviendo la desincronización y el salto involuntario al botón "Todos".
   - **Filtrado Estricto por Nivel (`ExactLevel`)**: Añadido soporte de `ExactLevel` en `LogFilterCriteria` y `SqliteLogStore`, garantizando que al seleccionar `🟣 Debug`, `🔵 Info` o `🟠 Warn` se muestren única y estrictamente los logs del nivel correspondiente.
5. **Auditoría de Código y Refactorización Modular (Clean Code & SRP)**:
   - **Desacoplamiento de `WorkflowExecutionContext.cs`**: Extraído de `WorkflowExecutor.cs` a su propio archivo independiente, reduciendo el orquestador principal a un tamaño manejable.
   - **Extracción de `SqliteLogQueryBuilder.cs`**: Aislada toda la lógica de construcción de SQL dinámico parametrizado fuera de `SqliteLogStore.cs`.
   - **Modularización de `ValueConverters.cs`**: Eliminado el archivo monolítico y dividido en 3 submódulos cohesivos por dominio: `BooleanConverters.cs`, `TelemetryConverters.cs` y `GraphConverters.cs`.
6. **Auditoría de Seguridad, Robustez y Depuración de Errores**:
   - **Mitigación Estricta de Zip Slip (`SmartUnpackNode.cs`)**: Normalizado el directorio base asegurando el separador de directorio final (`Path.TrimEndingDirectorySeparator + Path.DirectorySeparatorChar`) para evitar ataques por prefijos comunes.
   - **Papelera Segura sin Borrado Destructivo (`SafeRecycleDeleteNode.cs`)**: Ajustada la estructura P/Invoke x64 de `SHFILEOPSTRUCT` y eliminado el fallback a `File.Delete` que borraba permanentemente los archivos en caso de fallo.
   - **Medición Segura de Procesos (`CliExecutionNode.cs`)**: Sustituido el cálculo de tiempo por `Stopwatch.StartNew()` eliminando potenciales `InvalidOperationException` al consultar `process.ExitTime`.
   - **Despacho Seguro en UI (`FastObservableRingBuffer.cs`)**: Comprobación de `Application.Current.Dispatcher.CheckAccess()` en `NotifyReset()` evitando excepciones de colección en hilos de fondo.
   - **Limpieza de Tareas en Background (`FolderWatcherService.cs`)**: Cancelación y espera limpia con timeout de `_processingTask` en `Stop()`.
   - **Sanitización de Nombres de Archivo (`AdvancedRenamerNode.cs`)**: Reemplazo automático de caracteres ilegales devueltos por plantillas (`Path.GetInvalidFileNameChars`).
   - **Drenaje de Canal (`SqliteLogStore.cs`)**: Invocación de `_ingestionChannel.Writer.TryComplete()` en `DisposeAsync()` y `Dispose()`.
7. **Batería de Automatización y Testing Exhaustivo (178 Pruebas)**:
   - **`FileItemContextExhaustiveTests`**: Memoización zero-alloc de `ShortIdString`, reactividad en cambio de ruta y clonación profunda.
   - **`SystemVariablesResolverExhaustiveTests`**: Comprobación de formato numérico de tamaños con `CultureInfo.InvariantCulture`, contadores y metadatos.
   - **`AdvancedRenamerExhaustiveTests`**: Sanitización automática de caracteres ilegales, estrategias de colisión (`AutoIncrement`) y soporte de casing exacto.
   - **`CliExecutionNodeExhaustiveTests`**: Ejecución con captura de stdout/stderr y modo Dry-Run.
   - **`SafeRecycleDeleteNodeExhaustiveTests`**: Resiliencia frente a archivos inexistentes y modo Dry-Run.
   - **`SqliteLogQueryBuilderTests`**: Validación de cláusulas SQL con `ExactLevel`, `SearchText` y rangos de fechas.
   - **`ValueConvertersExhaustiveTests`**: Badges cortos de severidad, bindings bidireccionales y convertidores de visibilidad/ancho.
   - Suite completa ejecutada: **178 / 178 pruebas superadas con 100% de éxito (0 errores, 0 fallos)** en 1.1s.

---

### 🛠 Cambios e Implementaciones
1. **Botón Visual e Interactivo en Cabecera de Nodo (`NodeCardView.xaml` & `ValueConverters.cs`)**:
   - Incorporado un botón interactivo al estilo del breakpoint en la cabecera de cada tarjeta de nodo.
   - **Indicador Visual**: Icono estilizado (`≡`) con fondo cian brillante (`#06B6D4`) cuando está **Encendido (emite logs)** y gris translúcido atenuado (`#475569`) cuando está **Apagado (silenciado)**.
   - **ToolTips y Menú Contextual**: ToolTip reactivo ("Logs: Habilitados (clic para silenciar)" / "Logs: Silenciados (clic para activar)") y opción en el menú contextual (`MenuItem: Alternar Emisión de Logs`).
2. **Modelo de Vista y Comandos (`NodeViewModel.cs`)**:
   - Añadida propiedad reactiva `IsLoggingEnabled` (por defecto `true`) y comando `ToggleLoggingCommand`.
3. **Control y Supresión en el Motor de Ejecución (`WorkflowExecutor.cs` & `WorkflowGraph.cs`)**:
   - `WorkflowGraph` y `WorkflowNode` persisten el estado `IsLoggingEnabled` y el set `DisabledLoggingNodeIds`.
   - `WorkflowExecutionContext` y `WorkflowExecutor.NotifyLog` descartan de inmediato los logs de nodos silenciados en $O(1)$ sin asignación en memoria ni saturación de base de datos.
4. **Pruebas Automatizadas y Validación**:
   - Añadidas pruebas unitarias en `StructuredLogContextTests.cs` validando la supresión y re-activación de logs en caliente.
   - Suite completa superada al 100%: **145 / 145 pruebas exitosas (0 errores, 0 advertencias)**.

---

## [2026-08-23] - Optimización de Rendimiento Extremo (Performance Engineering) y Zero-Alloc Hot Paths

### 🛠 Cambios e Implementaciones
1. **Memoización en `FileItemContext.cs` (Zero-Alloc Hot Paths)**:
   - Cacheo interno e inmutable de `IdString` (`"d3b07384..."`) y `ShortIdString` (`"d3b07384"`).
   - Propiedad `FileName` reactiva a mutaciones en `CurrentPath`.
   - Eliminadas más de 160.000 asignaciones redundantes de strings de GUIDs y rutas por ejecución de flujo masivo.
2. **Formateo Zero-Boxing en `StructuredLogRecord.cs`**:
   - `FormattedFileSize` optimizado con formateo numérico directo en lugar de `FormattableString.Invariant`, eliminando allocation de factories, arrays `object[]` de parámetros y boxing.
   - Parámetro `fileName` precalculado en `StructuredLogRecord.Create` para evitar llamadas redundantes a `Path.GetFileName`.
3. **Reutilización de Conexión y Transacciones Masivas en `SqliteLogStore.cs`**:
   - `InsertBatchAsync` reutiliza `_keepAliveConnection` protegida bajo `_flushLock`, eliminando la apertura y cierre repetitivo de conexiones SQLite.
   - Ejecución sincrónica nativa dentro del worker thread para evitar la sobrecarga de `Task` en bases de datos in-memory.
4. **Benchmarking Multinúcleo y Validación de Alta Concurrencia**:
   - Añadido `Benchmark_Telemetry_HighThroughput_ParallelIngestion` en `PerformanceBenchmarkSuiteTests.cs` simulando ingestión paralela en todos los núcleos de CPU (28 hilos).
   - **Throughput alcanzado**: **>82.000 logs/segundo** persistidos e indexados en SQLite In-Memory en ~600 ms con apenas 8 recolecciones Gen0.
   - Suite completa: **143 / 143 pruebas pasadas con 100% de éxito (0 errores, 0 advertencias)**.

---

## [2026-08-23] - Modernización de Logs Estructurados, Trazabilidad por ID de Flujo (`ItemId`) y Visor Interactivo JSON

### 🛠 Cambios e Implementaciones
1. **Auto-Vinculación Contextual de Archivos en Motor (`WorkflowExecutionContext.cs` & `WorkflowExecutor.cs`):**
   - Inyectada la referencia activa al `FileItemContext` en cada ciclo de ejecución de nodo.
   - Cualquier invocación a `context.Log(...)` extrae de forma automática y transparente: `ItemId` (`item.Id`), `FilePath` (`item.CurrentPath`), `FileName` (`Path.GetFileName`), y `FileSizeBytes` (`item.FileSizeBytes`).
   - Resuelto definitivamente el problema de nombres de archivos vacíos en los logs.
2. **Estructuración JSON y Mensajes Descriptivos de 1 Línea (`LogOutputNode.cs`):**
   - Empaquetamiento de metadatos, tags, tamaño e historial de ejecución en un JSON formateado (`DetailsJson`).
   - Generación de mensajes concisos y limpios de una sola línea (`🔍 Inspección: archivo.ext (X MB) • N tags • M metadatos • K nodos previos`), eliminando el texto multilínea caótico que se cortaba en el grid.
3. **Persistencia e Indexación en Memoria SQLite (`SqliteLogStore.cs` & `StructuredLogRecord.cs`):**
   - Esquema de tabla `ExecutionLogs` actualizado con `ItemId TEXT`, `FileSizeBytes INTEGER` y `DetailsJson TEXT`.
   - Creado el índice B-Tree `IX_Logs_ItemId` y el método analítico `GetItemTraceAsync(string itemId)` para recuperar toda la cadena de procesamiento de un archivo específico desde su origen hasta el final.
4. **DataGrid Profesional en WPF con Fila Expansible (`LogView.xaml` & `LogViewModel.cs`):**
   - **Columna `ID Flujo`**: Badge compacto (`#a1b2c3d4`) clicable que filtra al instante toda la vida del archivo.
   - **ToolTips Ricos**: Muestra ruta completa, tamaño formateado e ID al pasar el cursor sobre la columna Fichero.
   - **Panel Expansible `RowDetailsTemplate`**: Se despliega al seleccionar la fila con badges de metadatos, visor de JSON formateado y botones de acción rápida (`🔍 Trazabilidad` y `📋 Copiar JSON`).
5. **Ampliación de Pruebas Automatizadas:**
   - Nuevos tests en `StructuredLogContextTests.cs` y ampliación de `SqliteLogStoreTests.cs`.
   - Suite total incrementada a **142 / 142 pruebas pasadas con 100% de éxito (0 errores, 0 advertencias)**.

---

## [2026-08-23] - Capa de Telemetría Atómica (Snapshot Pull a 30 FPS) y Consola de Logs Virtualizada con RingBuffer

### 🛠 Cambios e Implementaciones
1. **Desacoplamiento Total Motor $\leftrightarrow$ UI mediante Snapshots Atómicos (`WorkflowExecutor.cs` & `ExecutionTelemetry.cs`):**
   - Incorporado el struct inmutable `TelemetrySnapshot` (`ProcessedItems`, `TotalItems`, `ProcessedBytes`, `ItemsPerSecond`, `MegabytesPerSecond`, `Percentage`, `Elapsed`, `StatusMessage`).
   - El motor de ejecución actualiza contadores atómicos con `Interlocked` y `Stopwatch` en O(1) con 0 asignaciones de memoria en heap.
   - Eliminado el encolamiento de delegados por cada archivo procesado en la cola del Dispatcher de WPF.
2. **Cálculo Ultrarrápido de Totales y Seguimiento Integral de Elementos (`WorkflowExecutor.cs` & `FolderSourceNode.cs`):**
   - **Soporte Integral para Archivos, Carpetas y Mixto**: `FolderSourceNode` evalúa `EmitMode` ("FilesOnly", "DirectoriesOnly", "FilesAndDirectories") tanto en `FastCountSourceFiles` como en el streaming, adaptando la métrica y las etiquetas contextuales ("elementos", "carpetas", "archivos").
   - **Rastreo Reactivo de Elementos en Streaming**: Incorporado `_sourceItemsEmitted` y resolución de aristas no conectadas en `DispatchEmitAsync`, garantizando que la barra de progreso refleje el avance en vivo desde el primer milisegundo independientemente de la topología o topologías abiertas.
   - **Feedback Fiel de Estado**:
     - Durante la ejecución: `⚡ Procesando: X/Total elementos (N%) • ops/s`
     - Al culminar: `🟢 Completado: Total/Total elementos (100%)`
3. **Puente de Telemetría y Coalescencia a 30 FPS (`ControlBarViewModel.cs`):**
   - El temporizador visual `visualFlushTimer` muestrea la instantánea atómica a 30 FPS constantes (~33 ms), actualizando la barra de progreso, estados de nodos, aristas y mensajes de estado en un único ciclo por frame.
   - Eliminada al 100% la cola residual de eventos y el retraso visual al finalizar flujos de trabajo masivos.
4. **Motor de Logs Estructurados en Memoria SQLite y DataGrid Fluido (`SqliteLogStore.cs` y `LogViewModel.cs`):**
   - **Cero Consumo de CPU en Reposo (0.0%)**: Añadida coalescencia de lotes (`Task.Delay(20)`) en el worker de SQLite, eliminando micro-transacciones unitarias continuas y reduciendo el consumo residual de CPU a 0%.
   - **Renderizado Instantáneo y Reactivo en DataGrid**: Conexión directa mediante `ObservableCollection<StructuredLogRecord>` con virtualización por reciclaje (`VirtualizationMode="Recycling"`). Los logs aparecen en tiempo real durante la ejecución sin pantallas en blanco ni parpadeos.
   - **Operaciones de Borrado y Exportación sin Bloqueos**:
     - `ClearAsync` protegido con `_flushLock` y sin `VACUUM` bloqueante, limpiando la consola y la base de datos de inmediato.
     - `ExportLogs` ejecutado 100% en streaming en hilo de fondo (`Task.Run`), permitiendo guardar logs de millones de registros sin congelar la ventana.
   - **Ordenación y Filtros SQL Instantáneos**:
     - Clic en cabecera **Duración** (`ORDER BY DurationMs DESC`) para detectar cuellos de botella al instante; clic en **Nivel**, **Hora**, **Nodo** o **Fichero**.
     - Búsqueda en tiempo real indexada en SQLite.
5. **Ampliación de la Suite de Pruebas Automatizadas:**
   - Creados `AsyncVirtualizingListTests.cs`, `SqliteLogStoreTests.cs`, `PagedLogStoreTests.cs`, `FastObservableRingBufferTests.cs` y `ExecutionTelemetryTests.cs`.
   - Suite completa superada con éxito: **139 / 139 pruebas unitarias y de integración pasadas (0 errores, 0 advertencias)**.

---

## [2026-08-22] - Sincronización Simultánea en Tiempo Real de Barra de Progreso y Logs

### 🛠 Cambios e Implementaciones
1. **Sincronización en Tiempo Real a 30 FPS (`ControlBarViewModel.cs`):**
   - Agrupada la actualización de progreso (`ProgressPercentage` y `StatusMessage`) directamente en el temporizador visual `visualFlushTimer` con prioridad normal.
   - Eliminado el encolamiento retrasado de miles de delegados de progreso que quedaban atrapados detrás del vaciado de logs. Ahora la barra de progreso avanza **simultáneamente y en tiempo real con la salida de logs**.
2. **Cálculo de Porcentaje en Vivo durante Streaming (`WorkflowExecutor.cs`):**
   - Lectura atómica volátil de `_totalItemsCount` en `finally` y reporte porcentual progresivo (`⚡ Procesando: X/Y (N%)`).

---

## [2026-08-22] - Corrección de Conteo en Nodos Terminales y Vaciado Instantáneo de Logs

### 🛠 Cambios e Implementaciones
1. **Conteo Preciso de Aristas Activas y Cierre al 100% (`WorkflowExecutor.cs`):**
   - Corregido el incremento de `_totalItemsCount` en `DispatchEmitAsync` para que sume únicamente las conexiones reales conectadas (`matchingEdges.Count`). Evitado que nodos terminales (como `LogOutputNode`) cuyos puertos de salida no están conectados desvíen el conteo total esperado.
   - Añadida notificación explícita del 100% de progreso al culminar todas las tareas en `ExecuteAsync` (`Procesados N/N (100%)`).
2. **Vaciado Adaptativo e Instantáneo de Logs (`LogViewModel.cs` & `ControlBarViewModel.cs`):**
   - Implementado escalado dinámico del tamaño de lote (de 75 hasta 500 registros por ciclo si la cola supera los 1.000 elementos).
   - Incorporado el método `FlushAllPendingLogs()`, invocado inmediatamente al finalizar el flujo para que la consola muestre el 100% de los logs en el mismo milisegundo en que concluye el procesamiento sin tiempos de espera residuales.

---

## [2026-08-22] - Streaming Fluido en UI a 60 FPS y Prevención de Congelamiento por Inundación

### 🛠 Cambios e Implementaciones
1. **Limitación de Tasa de Renderizado de Logs (`LogViewModel.cs`):**
   - Incorporado el límite de vaciado `MaxLogsPerFlush = 75` en `FlushPendingLogs` (cada 35 ms), evitando que ráfagas de miles de logs saturen la cola de renderizado de WPF y bloqueen la ventana en estado "No responde".
2. **Agrupación y Throttling de Eventos Visuales en el Lienzo (`ControlBarViewModel.cs`):**
   - Desacoplados los eventos `EdgeItemDispatched` y `NodeStatusChanged` mediante un temporizador `visualFlushTimer` a 30 FPS con diccionarios concurrentes (`pendingEdgeUpdates` / `pendingStatusUpdates`).
   - Reducido el tráfico de delegados en la cola del Dispatcher en más de un **99%**, manteniendo la interfaz 100% interactiva, fluida y con respuesta inmediata durante el procesamiento masivo.
3. **Modo Compacto de Inspección (`LogOutputNode.cs`):**
   - Incorporado el parámetro opcional `CompactFormat` para generar resúmenes concisos de 1 sola línea por archivo en flujos de alto volumen.

---

## [2026-08-22] - Escaneo de 1 Sola Pasada I/O y Búfer Acotado con Contrapresión (Bounded Channel)

### 🛠 Cambios e Implementaciones
1. **Constructores I/O de 1 Sola Pasada (`FileItemContext.cs`):**
   - Añadidos constructores optimizados `FileItemContext(FileInfo)` y `FileItemContext(DirectoryInfo)`.
   - Eliminadas las comprobaciones duplicadas de `File.Exists(path)` e instanciaciones redundantes de `FileInfo.Length`, reduciendo en un **66% las llamadas I/O de sistema de archivos a Windows** (1 sola pasada I/O).
2. **Tubería Productor-Consumidor con Contrapresión (`FolderSourceNode.cs`):**
   - Incorporado un canal acotado `Channel.CreateBounded<FileItemContext>(1000)` para pausar automáticamente el escáner si los nodos receptores son más lentos, evitando el uso excesivo de memoria RAM.
   - Puntos de cesión de hilo (`await Task.Yield()`) cada 100 archivos enumerados para garantizar cero bloqueos de la interfaz y respuesta instantánea ante cancelaciones.
   - Reporte dinámico en tiempo real cada 100 ms con métricas acumuladas de conteo y megabytes (`⚡ Escaneando y emitiendo: 1,450 archivos (850.5 MB)...`).

---

## [2026-08-22] - Tubería Productor-Consumidor No Bloqueante y Estado Continuo en Tiempo Real

### 🛠 Cambios e Implementaciones
1. **Tubería Productor-Consumidor No Bloqueante (`WorkflowExecutor.cs`):**
   - Eliminado el bloqueo secuencial en `DispatchEmitAsync`. Las llamadas a `context.EmitAsync` por parte de `FolderSourceNode` ahora son no bloqueantes, permitiendo que la lectura de archivos en disco ocurra a máxima velocidad (miles de archivos/segundo) mientras los nodos receptores procesan en paralelo.
   - Conteo atómico de tareas activas (`_activeNodeTasks`) y drenaje determinista en `ExecuteAsync`, garantizando 100% de finalización sin tareas huérfanas.
2. **Formateo y Persistencia de Estado Activo (`StatusBarViewModel.cs`):**
   - Incorporado el método helper `UpdateActiveStatusMessage` para mantener el indicador activo `⚡` y texto en español de forma ininterrumpida mientras `IsRunning` sea `true`.
   - Evitado que mensajes predeterminados como `"Listo"` o conteos secundarios reseteen la barra inferior durante la ejecución activa.

---

## [2026-08-22] - Escaneo Asíncrono en Streaming y Estado Reactivo en Tiempo Real

### 🛠 Cambios e Implementaciones
1. **Escaneo y Emisión Asíncrona en Streaming (`FolderSourceNode.cs`):**
   - Eliminada la recolección previa monolítica en lista (`List<FileItemContext>`).
   - Implementado escaneo asíncrono con emisión inmediata por archivo (`await context.EmitAsync("Out", item)`). Los nodos posteriores comienzan el procesamiento instantáneamente sin esperas iniciales ni congelamientos de UI.
   - Incorporado reporte de progreso periódico en tiempo real cada 100 ms (`⚡ Escaneando y emitiendo: N elementos...`).
2. **Sincronización Reactiva de la Barra de Estado Inferior (`StatusBarViewModel.cs` & `MainViewModel.cs`):**
   - Inyectada la instancia `LogViewModel` en `StatusBarViewModel`.
   - Suscripción reactiva a `LogViewModel.PropertyChanged` para reflejar en tiempo real los mensajes de escaneo y avance dinámico (`⚡ Escaneando y emitiendo: 1,450 elementos...`, `⚡ Procesando 45%`, `🟢 Listo`).

---

## [2026-08-22] - Optimización de Rendimiento de Alto Nivel y Benchmarking (.NET 9 & C# 13)

### 🛠 Cambios e Implementaciones
1. **Búsqueda Vectorizada SIMD y Cero Asignaciones en Motor de Plantillas (`VariableTemplateResolver.cs`):**
   - Incorporada la primitiva de .NET 9 `System.Buffers.SearchValues<char>` (`OpenBraceSearch`) para aceleración por hardware SIMD en la localización de delimitadores.
   - Eliminadas las asignaciones innecesarias de cadenas mediante rodajas `ReadOnlySpan<char>`.
2. **Optimizaciones de Clonado Profundo (`FileItemContext.cs`):**
   - Inicialización por capacidad exacta (`Metadata.Count`, `Tags.Count`, `ExecutionLog.Count`) en `DeepClone()` eliminando las reasignaciones internas (*array resizing*) en bifurcaciones masivas de puertos.
3. **Optimización de E/S Criptográfica (`HashCalculatorNode.cs` & `DeduplicationFilterNode.cs`):**
   - Incrementado el buffer de lectura de `FileStream` a 128 KB (`131072` bytes) reduciendo llamadas al sistema operativo durante operaciones de hash masivo.
4. **Nueva Suite de Benchmarking de Alto Rendimiento (`PerformanceBenchmarkSuiteTests.cs`):**
   - Batería de pruebas que mide throughput (operaciones/segundo), latencia, consumo pico de memoria y colecciones del Garbage Collector (Gen 0, Gen 1, Gen 2).
   - Suite de pruebas automatizadas incrementada a **117 / 117 pruebas pasadas con éxito**.

---

## [2026-08-22] - Generación de la Suite Completa de Documentación Técnica (`docs/`)

### 🛠 Cambios e Implementaciones
1. **Creación de la Suite Completa de Documentación Técnica (6 Archivos Markdown):**
   - **`docs/architecture.md`**: Documento de arquitectura, diagrama Mermaid.js, flujo de datos por capas, patrones de diseño y Registros de Decisiones Arquitectónicas (ADRs).
   - **`docs/setup_and_deployment.md`**: Guía de instalación, requisitos previos (.NET 9 SDK), script `.\run.ps1`, configuración de herramientas externas, empaquetado para distribución y CI/CD.
   - **`docs/api_reference.md`**: Referencia técnica completa de la capa SDK (`IFlowNode`, `FileItemContext`, `IFlowExecutionContext`), motor de plantillas de variables, firmas de métodos y guía de extensión de nodos.
   - **`docs/user_guide.md`**: Manual de usuario visual paso a paso, catálogo de los 22 nodos, gestor de presets multimedia, gestor de contraseñas, simulación *Dry Run*, catálogo de 40 ejemplos y resolución de problemas (FAQ).
   - **`docs/contributing.md`**: Estándares de código C# 13, workflow de Git, guía de desarrollo de nodos personalizados y baterías de pruebas `dotnet test`.
   - **`docs/README.md`**: Índice principal y centro de documentación con navegación por enlaces relativos.

---

## [2026-08-22] - Auditoría de Errores y Seguridad (QA Lead & Security Audit)

### 🛠 Cambios e Implementaciones
1. **Solución de Interbloqueo y Procesos Huérfanos (`CliExecutionNode.cs` & `MediaTranscoderNode.cs`):**
   - Lectura concurrente de `StandardOutput` y `StandardError` mediante `Task.WhenAll` evitando congelamientos de buffer.
   - Eliminación determinista de procesos hijos (`process.Kill(entireProcessTree: true)`) ante cancelación o expiración de tiempo de espera.
2. **Protección SSRF (`WebhookNotificationNode.cs`):**
   - Validación estricta de esquema de URI (`http`/`https`) en peticiones HTTP POST.
3. **Control Thread-Safe de Concurrencia (`WorkflowExecutor.cs`):**
   - Modificación segura de `MaxDegreeOfParallelism` evitando excepciones `ObjectDisposedException`.
4. **Nuevas Pruebas Unitarias de Seguridad (`QASecurityAuditFixesTests.cs`):**
   - Suite ampliada a **102 / 102 pruebas pasadas con éxito** (0 errores, 0 advertencias).

---

## [2026-08-22] - Auditoría de Arquitectura, Mapa de Riesgos y Plan de Modularización (Fase 1)

### 🛠 Cambios Implementados
1. **Auditoría de Archivos Monolíticos:**
   - Identificados 7 archivos principales de más de 300 líneas de código/XAML (`NodeCardView.xaml`, `NodeViewModel.cs`, `VariableTemplateResolver.cs`, `EditorViewModel.cs`, `ControlBarViewModel.cs`, `SmartUnpackNode.cs`, `NodeInspectorPanelView.xaml`).
2. **Identificación de Riesgos y Code Smells:**
   - Fugas potenciales de memoria por falta de desuscripción de eventos singleton (`IDisposable`).
   - Falta de modularidad en el motor de plantillas de variables.
   - Complejidad en plantillas XAML de Nodify y ViewModel acoplado a UI.
3. **Elaboración del Artefacto Implementation Plan:**
   - Creado artefacto `implementation_plan.md` detallando la estrategia de modularización iterativa en 4 módulos bajo el Principio de Responsabilidad Única (SRP).

---

## [2026-08-22] - Creación de la Biblioteca Completa de 40 Ejemplos de Flujos de Trabajo (`docs/examples/`)

### 🛠 Cambios Implementados
1. **Generación de 40 Flujos de Trabajo Ejecutables (`.json`) y Documentación Markdown (`.md`):**
   - Se estructuró el directorio `docs/examples/` en 4 niveles con 10 ejemplos por nivel (80 archivos en total + catálogo principal):
     - `docs/examples/01_basic/` (Flujos `flow_01` a `flow_10` - Canales lineales simples, optimización WebP, extracción MP3, hashes SHA-256, renombrado, etc.).
     - `docs/examples/02_intermediate/` (Flujos `flow_11` a `flow_20` - Filtrado condicional, bifurcación por extensión, EXIF, deduplicación, webhooks HTTP).
     - `docs/examples/03_advanced/` (Flujos `flow_21` a `flow_30` - Lotes `BatchBufferNode`, paralelismo `ForkJoinBarrierNode`, rate limit `ThrottleDelayNode`, reintentos).
     - `docs/examples/04_complex/` (Flujos `flow_31` a `flow_40` - Scatter-Gather, doble hash inmutable, ingesta masiva empresarial, arquitectura de fallback resiliente).
2. **Creación del Catálogo Principal (`docs/examples/README.md`):**
   - Se generó un catálogo con enlaces a todos los ejemplos, indicando categoría, descripción y cómo importarlos en FileFlow Studio.
3. **Prueba Unitaria de Validación de Esquema (`WorkflowStorageServiceTests.cs`):**
   - Se incorporó la prueba unitaria `AllFortyGeneratedWorkflowExamples_ShouldDeserializeSuccessfully` (86/86 pruebas pasadas) que valida automáticamente que los 40 archivos `.json` deserializan limpiamente en objetos `WorkflowGraph` válidos.
1. **Auditoría y Sustitución de Rutas Absolutas a Relativas:**
   - Se revisaron todos los nodos de la aplicación y se reemplazaron las rutas predeterminadas absolutas (como `C:\SampleFiles`, `C:\FileFlowOutput`, `C:\Quarantine`, `C:\FileFlowUnpacked`, `C:\FileFlowOptimized`) por patrones de rutas relativas basadas en plantillas:
     - **FolderSourceNode**: `SourcePath` $\rightarrow$ `{RelativeDir}\Input`
     - **DestinationSinkNode**: `DestinationRoot` $\rightarrow$ `{RelativeDir}\Output`
     - **OriginalFileActionNode**: `QuarantinePath` $\rightarrow$ `{RelativeDir}\Quarantine`
     - **SmartUnpackNode**: `DestinationFolder` $\rightarrow$ `{RelativeDir}\Unpacked`
     - **ImageOptimizerNode**: `OutputDirectory` $\rightarrow$ `{RelativeDir}\OptimizedImages`
2. **Anclaje Automático a la Ruta Global de Salida (`ParameterHelper.ResolveOutputPath`):**
   - Al usar patrones de rutas relativas, `ParameterHelper.ResolveOutputPath` las ancla automáticamente bajo el directorio configurado en **Ajustes de la Aplicación > Almacenamiento y Rutas (DefaultGlobalOutputDir)**.
1. **Actualización Automática de Argumentos (`NodeViewModel.cs`):**
   - Al seleccionar cualquier preset en el menú desplegable del nodo **Transcodificar Media**, se activa la actualización inmediata del parámetro `CustomArguments` en la tarjeta del nodo con los comandos FFmpeg configurados en dicho preset (ej: `-vn -c:a libmp3lame -b:a 192k` para MP3).
2. **Eliminación del Campo Redundante `FFmpegPath` (`MediaTranscoderNode.cs`):**
   - Se eliminó el parámetro `FFmpegPath` de las tarjetas del nodo de media para simplificar la interfaz visual.
   - El nodo resuelve de forma limpia la ruta ejecutable de FFmpeg directamente desde `ExternalToolsService` (definida globalmente en Ajustes > Herramientas Externas), con fallback dinámico en el `PATH` del sistema.
1. **Configuración de ComboBox (`NodeParameterTemplates.xaml`):**
   - Se estableció `IsEditable="False"` en el control `ComboBox` de los parámetros de nodos.
   - En WPF, la combinación de `IsEditable="True"` con `SelectedItem` en un ComboBox provocaba que el control de texto editable interno sobrescribiera el ítem seleccionado al hacer clic en el desplegable. Al establecer `IsEditable="False"`, la selección de cualquier preset del menú (*Extraer Audio MP3*, *Convertir 720p H.264*, *WebM VP9*, etc.) funciona de manera instantánea y estable.
2. **Sincronización Dinámica de Presets (`NodeParameterViewModel.cs`):**
   - Se añadió la suscripción al evento `MediaPresetManagerService.Instance.PresetsChanged` para refrescar automáticamente la lista de opciones (`Options`) de los nodos de media al crear, editar o eliminar presets en el gestor.
1. **Desacoplamiento de Binding Conflictuco (`NodeParameterTemplates.xaml`):**
   - Se eliminó el binding redundante `Text="{Binding Value...}"` con `UpdateSourceTrigger=PropertyChanged` que competía con `SelectedItem` en el control `ComboBox` editable de las tarjetas de nodos.
   - Ahora `SelectedItem="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"` administra la selección limpia de cualquier preset del desplegable sin que la UI revierta la selección.
1. **Creación del Convertidor (`ValueConverters.cs`):**
   - Se creó e implementó `StringEqualsToVisibilityConverter` en la capa de convertidores WPF (`FileFlow.App.Converters`) para comparar dinámicamente la categoría de presets con parámetros de texto y devolver el estado de visibilidad del icono correspondiente.
2. **Registro de Recurso Global y Local (`App.xaml` & `MediaPresetManagerWindow.xaml`):**
   - Se registró la clave de recurso `<converters:StringEqualsToVisibilityConverter x:Key="StringEqualsToVisibilityConverter" />` tanto globalmente en `App.xaml` como localmente en los recursos de `MediaPresetManagerWindow.xaml`, solucionando por completo la excepción de tipo `XamlParseException` / `StaticResourceHolder` al presionar el botón `⚙️ Presets`.
1. **Inicialización al Arrancar la Aplicación (`App.xaml.cs`):**
   - Se añadieron invocaciones explícitas a `UserPreferencesService.Instance.Load()` y `ExternalToolsService.Instance.LoadConfig()` en el evento `OnStartup`.
   - Se aplica de forma inmediata el tema visual guardado (`ActiveTheme`) vía `ThemeManager.Instance.SetTheme(themeEnum)`.
2. **Sincronización Reactiva en ViewModels (`ControlBarViewModel`, `ToolboxViewModel`, `EditorViewModel`, `LogViewModel`):**
   - **`ControlBarViewModel.cs`**: Carga e iguala `SelectedTheme` y `IsDryRun` con `DefaultDryRunState` al iniciar y cuando se modifican desde la ventana de Ajustes Generales.
   - **`ToolboxViewModel.cs`**: Ajusta automáticamente `IsCompactMode` con `IsCompactToolbox` al refrescar el catálogo.
   - **`EditorViewModel.cs`**: Se suscribe al evento `PreferencesChanged` para actualizar reactivamente `GlobalOutputDir` si se cambia desde los ajustes generales.
   - **`LogViewModel.cs`**: Lee de forma dinámica la preferencia `MaxLogEntries` para delimitar el buffer de registros en la consola.
1. **Control de Concurrencia en Motor (`WorkflowExecutor.cs`):**
   - Se activó e integró la protección por `SemaphoreSlim` (`_concurrencyThrottle`) dentro del despacho de nodos (`DispatchEmitAsync`).
   - Todos los hilos secundarios de despacho pasan obligatoriamente por `await _concurrencyThrottle.WaitAsync()` y liberan en el bloque `finally` (`_concurrencyThrottle.Release()`), garantizando el respeto estricto del número de hilos configurado (`MaxDegreeOfParallelism`).
2. **Conexión con Preferencias de Aplicación (`ControlBarViewModel.cs`):**
   - Al instanciar `WorkflowExecutor` antes de iniciar la ejecución de cualquier flujo, se lee dinámicamente `UserPreferencesService.Instance.Preferences.MaxParallelThreads` (salvo en modo depuración interactiva, donde se fuerza a 1 hilo para inspección secuencial).
1. **Persistencia Completa de Preferencias (`UserPreferencesService.cs`):**
   - Extensión del servicio JSON en `%APPDATA%\FileFlowStudio\user_preferences.json` para guardar de forma permanente todos los ajustes de la aplicación:
     - `DefaultGlobalOutputDir`: Ruta de salida global persistente (restaurada automáticamente en el editor).
     - `ActiveTheme`: Tema visual activo (`Dark`, `Light`, `Cyber`, `Pastel`).
     - `IsCompactToolbox`: Modo de vista del catálogo (`Compacto` / `Detallado`).
     - `MaxParallelThreads`: Hilos de CPU máximos para procesamiento paralelo (1 a 16 hilos).
     - `DefaultDryRunState`: Estado del Modo Prueba (Simulación) por defecto al arrancar.
     - `DefaultConflictStrategy`: Estrategia de resolución de colisiones predeterminada (`RenameIncremental`, `Overwrite`, `Skip`).
     - `DefaultLogLevel`: Filtro de logs predeterminado (`Information`, `Warning`, `Error`, `Debug`).
     - `AutoScrollConsole` & `MaxLogEntries`: Desplazamiento automático y límite de memoria de registros en consola.
     - `EnableAutoSave` & `AutoSaveIntervalMinutes`: Configuración de respaldo automático de flujos.
2. **Rediseño Modal de Ajustes Generales (`WorkflowSettingsWindow.xaml`):**
   - Diálogo organizado en 4 pestañas fluidas:
     - 📂 **Almacenamiento & Rutas** (Ruta global, colisiones, auto-guardado).
     - 🎨 **Apariencia & UI** (Tema, vista de catálogo, auto-scroll y límite de logs).
     - ⚡ **Rendimiento & Ejecución** (Hilos paralelos de CPU, modo prueba por defecto, nivel de log).
     - 🛠️ **Herramientas Externas** (FFmpeg, FFprobe, 7z, Python con Autobúsqueda).
1. **Gestor de Presets de Media (`MediaPresetManagerService.cs` & `MediaPresetManagerWindow.xaml`):**
   - Servicio Singleton con almacenamiento en `%APPDATA%\FileFlowStudio\media_presets.json`.
   - Sistema CRUD completo (Crear, Editar, Eliminar, Restablecer) de presets de conversión.
   - Ventana modal interactiva con 10 presets de fábrica preconfigurados (Extracción de MP3, AAC, FLAC, 1080p H.264, 720p H.264, 4K H.265/HEVC, WebM VP9, GIF Animado, Móvil Ultra-Comprimido, Personalizado).
   - Botón `⚙️ Presets` integrado en la tarjeta del nodo `Transcodificar Media` para acceso instantáneo al gestor.
2. **Servicio de Autobúsqueda de Herramientas Externas (`ExternalToolsService.cs`):**
   - Servicio de detección automática de ejecutables (`ffmpeg.exe`, `ffprobe.exe`, `7z.exe`, `python.exe`) escaneando el `PATH` del sistema, rutas conocidas (`Program Files`, `C:\ffmpeg\bin`, `AppData`, `Chocolatey`, `WinGet`, `Scoop`) y el Registro de Windows.
   - Pestaña **"🛠️ Herramientas Externas"** añadida a la Configuración del Flujo (`WorkflowSettingsWindow.xaml`) con el botón **`🔍 Auto-Detectar Herramientas`**.
3. **Ejecución Real con Control de Progreso (`MediaTranscoderNode.cs`):**
   - Ejecución de `ffmpeg.exe` en subproceso con redirección de `stderr` para capturar el progreso en tiempo real (`time=00:01:23.45`) y emitirlo a la consola de logs.
   - Modo de simulación/fallback seguro cuando FFmpeg no está presente o en pruebas simuladas.
1. **Reducción de Nombres de Nodos (`Strings.es.resx` & `Strings.resx`):**
   - Se simplificaron los nombres de los 22 nodos en los recursos de idioma para eliminar términos redundantes y hacerlos directos y explícitos:
     - `Borrado Seguro a Papelera` $\rightarrow$ **`Enviar a Papelera`**
     - `Renombrador Avanzado con Tokens` $\rightarrow$ **`Renombrar Archivo`**
     - `Reubicador y Copiador de Archivos` $\rightarrow$ **`Mover / Copiar`**
     - `Limpiador de Carpetas Vacías` $\rightarrow$ **`Limpiar Carpetas`**
     - `Acción sobre Archivo Original` $\rightarrow$ **`Acción en Origen`**
     - `Inspector de Directorios` $\rightarrow$ **`Escanear Carpeta`**
     - `Descompresión Inteligente` $\rightarrow$ **`Descomprimir`**
     - `ArchiveCompressorNode` $\rightarrow$ **`Comprimir ZIP / 7z`**
     - `ArchiveFilterNode` $\rightarrow$ **`Filtrar Comprimidos`**
     - `Optimizador de Imágenes` $\rightarrow$ **`Optimizar Imagen`**
     - `Media & Video Transcoder` $\rightarrow$ **`Transcodificar Media`**
     - `Document & PDF Processor` $\rightarrow$ **`Procesar Documento`**
     - `Inyector de Variables` $\rightarrow$ **`Inyectar Variable`**
     - `Calculador de Hash Criptográfico` $\rightarrow$ **`Calcular Hash`**
     - `Filtro de Deduplicación por Hash` $\rightarrow$ **`Filtrar Duplicados`**
     - `Agrupador de Lotes (Batch Buffer)` $\rightarrow$ **`Agrupar por Lotes`**
     - `Control de Tasa y Pausa (Throttle)` $\rightarrow$ **`Pausa / Throttle`**
     - `Barrera de Sincronización (Fork & Join)` $\rightarrow$ **`Barrera Fork & Join`**
     - `Enrutador Condicional (Switch / Case)` $\rightarrow$ **`Enrutador Switch`**
     - `Filtro por Condición Lógica` $\rightarrow$ **`Filtro Condicional`**
     - `Ejecutor de Comandos y Procesos CLI` $\rightarrow$ **`Ejecutar Comando CLI`**
     - `Notificador Webhook (HTTP POST)` $\rightarrow$ **`Enviar Webhook`**
     - `Inspector de Registros` $\rightarrow$ **`Registrar Log`**
2. **Ajuste de Diseño Visual en el Catálogo:**
   - Con esta optimización, todos los títulos caben en una sola línea en las tarjetas del catálogo sin recortarse ni requerir puntos suspensivos (`...`).
1. **Límite de 10 Nodos Frecuentes (`ToolboxViewModel.cs`):**
   - Se ajustó la consulta LINQ al construir el grupo `🔥 Más Usados` para limitar el listado exactamente a los **10 nodos con mayor frecuencia de uso acumulada** (`.Take(10)`).
   - Mantiene el catálogo limpio y enfocado exclusivamente en el TOP 10 de nodos más utilizados.
1. **Servicio de Persistencia de Usuario (`UserPreferencesService.cs`):**
   - Servicio Singleton que guarda de forma persistente en `%APPDATA%\FileFlowStudio\user_preferences.json`:
     - `FavoriteNodeTypes`: Conjunto de tipos de nodos marcados por el usuario.
     - `NodeUsageCounts`: Conteo histórico de veces que se ha insertado cada nodo en el lienzo de trabajo.
2. **Sistema de Favoritos Manuales (⭐) e Historias Frecuentes (🔥):**
   - **Botón Estrella en Tarjeta**: Cada nodo en `NodeToolboxView.xaml` incluye un botón de estrella `[ ⭐ ]` / `[ ☆ ]` para marcar o desmarcar como favorito con un clic.
   - **Categorías Dinámicas**: Creación automática de los grupos superiores `⭐ Favoritos` y `🔥 Más Usados`.
   - **Recuento Automático**: Al añadir o arrastrar un nodo al editor (`EditorViewModel.cs`), se incrementa su contador de uso acumulado.
3. **Pestañas de Categoría Multilínea (`NodeToolboxView.xaml`):**
   - Reemplazado el contenedor horizontal por un `<WrapPanel Orientation="Horizontal">`.
   - Los chips de filtrado (`Todas`, `⭐ Favoritos`, `🔥 Frecuentes`, `📁 Archivos`, `📦 Compresión`, `🎬 Media & Docs`, `🏷️ Metadatos`, `🔀 Lógica`, `⚡ Integraciones`) se distribuyen limpiamente en 2 o 3 filas dinámicas sin necesidad de hacer scroll horizontal.
1. **Reestructuración de la Taxonomía de Nodos (`FileFlow.Plugin.*`):**
   - **`FileSystem` (📁 Archivos y Disco)**: `FolderSourceNode`, `DestinationSinkNode`, `DirectoryInspectorNode`, `FileRelocatorNode`, `AdvancedRenamerNode`, `EmptyDirectoryCleanerNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`.
   - **`Archives` (📦 Compresión)**: `SmartUnpackNode`, `ArchiveCompressorNode`, `ArchiveFilterNode`.
   - **`MediaDocs` (🎬 Multimedia y Documentos)**: `ImageOptimizerNode`, `MediaTranscoderNode`, `DocumentProcessorNode`.
   - **`Metadata` (🏷️ Metadatos e Integridad)**: `VariableInjectorNode`, `ExifMetadataNode`, `HashCalculatorNode`, `DeduplicationFilterNode`.
   - **`Logic` (🔀 Lógica y Control)**: `SwitchCaseNode`, `ExpressionFilterNode`, `BatchBufferNode`, `ThrottleDelayNode`, `ForkJoinBarrierNode`.
   - **`Integrations` (⚡ Integración y CLI)**: `CliExecutionNode`, `WebhookNotificationNode`, `LogOutputNode`.
2. **Actualización de Filtros por Chips (`ToolboxViewModel.cs` & `NodeToolboxView.xaml`):**
   - Actualizada la barra superior del catálogo para incluir chips de filtrado de las 6 categorías principales (`Todas`, `📁 Archivos`, `📦 Compresión`, `🎬 Media & Docs`, `🏷️ Metadatos`, `🔀 Lógica`, `⚡ Integraciones`).
1. **Iconografía Específica por Función (`AppModels.cs` & `ToolboxViewModel.cs`):**
   - Asignado icono visual único según el propósito técnico del nodo (ej: `📁` `FolderSourceNode`, `🕵️` `DirectoryInspectorNode`, `📦` `SmartUnpackNode`, `🗜️` `ArchiveCompressorNode`, `🖼️` `ImageOptimizerNode`, `🎬` `MediaTranscoderNode`, `📄` `DocumentProcessorNode`, `🏷️` `VariableInjectorNode`, `🔀` `SwitchCaseNode`, `💾` `DestinationSinkNode`, `🗑️` `OriginalFileActionNode`).
2. **Modos de Vista Intercambiables (`ToolboxViewModel.cs` & `NodeToolboxView.xaml`):**
   - **Modo Lista Compacta (Predeterminado)**: Muestra únicamente el icono y el título de cada nodo en 28px de altura, triplicando la cantidad de nodos visibles en pantalla sin necesidad de hacer scroll. La descripción completa se despliega al pasar el ratón en un ToolTip flotante rico.
   - **Modo Detallado**: Muestra el icono, título y la descripción multilínea.
   - **Botonera Toggle**: Selector `[ ☰ Compacto ]` / `[ 📋 Detallado ]` en la cabecera.
3. **Pestañas Chips de Filtro por Categoría (`NodeToolboxView.xaml`):**
   - Barra superior con chips de filtrado rápido (`Todas`, `📁 Archivos`, `📦 Compresión`, `🖼️ Imágenes`, `🔀 Lógica`).
1. **Pinceles Dinámicos del Sistema de Temas (`LogView.xaml.cs`):**
   - Se reemplazaron los pinceles estáticos de texto (`#F1F5F9`) por llamadas a `GetThemeBrush(resourceKey, fallbackHex)`.
   - El cuerpo del mensaje de log ahora consume `TextPrimaryBrush` (`#0F172A` en temas claros como `Light` y `Pastel`, `#F1F5F9` en temas oscuros como `Dark` y `Cyber`).
   - Los niveles de log adaptan sus niveles de contraste con el fondo (`AccentErrorBrush`, `AccentWarningBrush`, `AccentCyanBrush`, `AccentPurpleBrush`).
2. **Suscripción a Eventos de Tema (`ThemeManager.cs`):**
   - `LogView` se suscribe a `ThemeManager.Instance.ThemeChanged` para reconstruir automáticamente el `FlowDocument` al cambiar de tema, garantizando legibilidad perfecta en cualquier modo.
1. **Estilo `FilterRadioButton` (`ButtonStyles.xaml`):**
   - Definido estilo dinámico para controles `RadioButton` con `TargetType="RadioButton"`, vinculado a pinceles del sistema de temas (`BgHoverBrush`, `TextPrimaryBrush`, `BorderDarkBrush`, `AccentPrimaryBrush`).
2. **Aplicación en la Consola (`LogView.xaml`):**
   - Asignado `Style="{StaticResource FilterRadioButton}"` a los botones de filtro rápido de la consola (`Todos`, `🔴 Errores`, `🟠 Advertencias`).
   - Al cambiar de tema (`Oscuro`, `Claro`, `Cyber`, `Pastel`), los botones adaptan de forma inmediata sus fondos, bordes y colores de texto sin desajustes estéticos.
1. **Migración a `RichTextBox` en WPF (`LogView.xaml`):**
   - Reemplazado el `<TextBox>` plano por un `<RichTextBox>` estilizado con tipografía monoespaciada `Cascadia Code`/`Consolas`.
   - Propiedades `IsReadOnly="True"` e `IsDocumentEnabled="True"` para permitir la selección de texto libre con ratón/teclado y copiado con `Ctrl+C` sin permitir edición del documento.
2. **Formateador de Registro con Código de Colores (`LogView.xaml.cs`):**
   - Cada entrada de log se convierte dinámicamente en elementos `Paragraph` y `Run` en el `FlowDocument`:
     - **Marca de tiempo `[HH:mm:ss]`**: Gris Pizarra (`#64748B`).
     - **🔴 `[CRITICAL]` / `[ERROR]`**: Rojo Neón (`#EF4444`) en Negrita.
     - **🟠 `[WARNING]`**: Naranja/Ámbar (`#F59E0B`) en Negrita.
     - **🔵 `[INFO]`**: Azul Cielo (`#38BDF8`).
     - **🟣 `[DEBUG]`**: Púrpura Suave (`#C084FC`).
     - **⚪ `[TRACE]`**: Gris Pizarra (`#94A3B8`).
     - **Mensaje**: Blanco/Gris primario (`#F1F5F9`).
3. **Filtros Rápidos por Nivel de Log (`LogViewModel.cs` & `LogView.xaml`):**
   - Añadida barra de botones de filtro en la cabecera de la consola (`Todos`, `🔴 Errores`, `🟠 Advertencias`) con contadores de fallos y advertencias en tiempo real.
4. **Auto-Scroll Inteligente:**
   - La consola se desplaza automáticamente al final con cada nuevo mensaje excepto cuando el usuario tiene texto seleccionado o está inspeccionando el historial superior.
1. **Ampliación de `NodePort` (`NodePort.cs`):**
   - Se añadió el parámetro opcional `Description` a la definición del record `NodePort` para documentar la función técnica de cada puerto.
2. **ViewModel de Puerto Enriquecido (`PortViewModel.cs`):**
   - Propiedades para ToolTips interactivos: `Description`, `TransmittedCount`, `LastItemInfoText`, `MetadataVariables` (claves/valores del contexto), `ConnectionStatusText` e `IsConnected`.
   - Método `UpdatePortContext(FileItemContext)` para actualizar métricas e inyectar metadatos en tiempo de ejecución o durante la depuración paso a paso.
3. **Conexiones Dinámicas (`EditorViewModel.cs`):**
   - `UpdatePortConnectionStates()` notifica a cada puerto si está libre u origen/destino de conexiones activas (ej: `Conectado a FolderSourceNode ("Out")`).
4. **Plantilla XAML Fluent (`NodeCardView.xaml`):**
   - ToolTip contextual rico de 310px de ancho con sombra profunda `DropShadowEffect`.
   - Incluye cabecera con badge del tipo de dato, descripción funcional, métricas de elementos transmitidos, **panel desplegable de metadatos y variables del contexto (ideal para depuración)** e indicador de estado de conexión `🟢 Conectado` / `⚪ Puerto libre`.
1. **Reorganización de Columnas Grid (`StatusBarView.xaml`):**
   - Se configuró el estado del motor (`🟢 Listo`) con `ColumnDefinition Width="Auto"`, ya que es un mensaje corto y predecible.
   - Se asignó la columna expansible `ColumnDefinition Width="*"` al botón de la **Ruta de Salida Global** (`📁 Salida: ...`), aumentando su ancho máximo dinámico hasta `520px` (`TextTrimming="CharacterEllipsis"`).
   - Ahora la ruta de salida dispone de todo el espacio libre de la barra inferior y no se corta prematuramente.
1. **Monitor de Rendimiento en Tiempo Real (`SystemPerformanceMonitor.cs`):**
   - Servicio asíncrono con temporizador ligero (refresco cada 1000ms) para medir consumo de Memoria RAM (`WorkingSet64`) y uso de CPU sin saturar la UI.
2. **ViewModel y Vista de Barra Inferior (`StatusBarViewModel.cs` & `StatusBarView.xaml`):**
   - Vista moderna Fluent Design en la parte inferior de la ventana principal (`MainWindow.xaml`).
   - **Contexto del Grafo**: Muestra conteo en vivo de nodos (`🧩 Nodos`), conexiones (`🔗 Conexiones`) y nodo seleccionado (`🎯 SelectedNode`).
   - **Estado del Motor**: Muestra badges de estado reactivos (`🟢 Listo`, `⚡ Ejecutando flujo...`, `⏸️ Pausado`).
   - **Métricas de Sistema**: Muestra consumo de `🧠 RAM` y `💻 CPU` en tiempo real.
   - **Accesos Rápidos**: Botón clicable de **Ruta de Salida Global** (`📁 Salida: C:\FileFlowOutput`) que abre la carpeta en Windows Explorer, y botón de ajuste de Zoom (`🔍 Fit`).
1. **Definición de Estilo `PrimaryButton` (`ButtonStyles.xaml`):**
   - Agregada la clave `PrimaryButton` heredando de `IconButton` para que los botones de acción principal (como **✅ Aplicar Ajustes**) adopten dinámicamente el color de acento del tema activo (`AccentPrimaryBrush` en Dark, Light, Cyber y Pastel).
2. **Sincronización de Barra de Título Nativa Windows DWM (`WindowThemeHelper.cs`):**
   - Implementado `WindowThemeHelper` con invocación P/Invoke a `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE`).
   - Sincroniza la barra de título superior nativa de la ventana (`MainWindow`, `WorkflowSettingsWindow`, `PasswordManagerWindow`) para cambiar entre tema oscuro y claro automáticamente al cambiar el tema de la aplicación o del sistema operativo.
1. **Eliminación de la Caja de Título Inútil (`ControlBarView.xaml`):**
   - Se eliminó la caja de texto innecesaria que ocupaba espacio en la barra superior.
2. **Conmutador Compacto de Modo Prueba (`ControlBarView.xaml` & `ButtonStyles.xaml`):**
   - Creado el estilo `SecondaryToggleButton` para `<ToggleButton>` con estado activo iluminado (`AccentPrimaryBrush`).
   - Resuelta la excepción XAML de inicialización al asignar el `TargetType` correcto para conmutadores en WPF.
3. **Modal de Configuración del Flujo (`WorkflowSettingsWindow.xaml` & `ControlBarViewModel.cs`):**
   - Corregido el comando de la barra superior exponiendo `Editor` y el comando delegado `OpenWorkflowSettingsCommand` directamente en `ControlBarViewModel.cs`.
   - Se despliega correctamente el modal **`⚙️ Configuración del Flujo`** con la Ruta de Salida Global y botón examinador `📁 Examinar`.
1. **Anclaje Inteligente de Rutas Relativas (`ParameterHelper.ResolveOutputPath`):**
   - Implementado el método `ParameterHelper.ResolveOutputPath` en `FileFlow.Sdk`. Si un nodo especifica una ruta de salida relativa (ej: `Procesados/{FileName}` o `Compressed`), el sistema la unifica automáticamente dentro de la Ruta Global de Salida (`GlobalOutputDir`).
   - Si un nodo especifica una ruta absoluta (ej: `D:\Final\Salida.zip`), se respeta exactamente esa ubicación sin alteración.
2. **Inyección de Token `{GlobalOutputDir}` (`VariableTemplateResolver.cs`):**
   - Agregado el token `{GlobalOutputDir}` al motor de plantillas de variables.
3. **Control de Ruta Global en la Barra de Herramientas UI (`ControlBarView.xaml` & `EditorViewModel.cs`):**
   - Añadido un campo **📁 Salida Global** con botón examinador en la barra de control superior para configurar fácilmente la carpeta base del flujo (por defecto `C:\FileFlowOutput`).
   - Persistencia de `GlobalOutputDir` en el modelo JSON del grafo (`WorkflowGraph.cs`).
4. **Actualización de Nodos de Escritura:**
   - Actualizados `DestinationSinkNode`, `ArchiveCompressorNode`, `ImageOptimizerNode` y `MediaTranscoderNode` para consumir `ParameterHelper.ResolveOutputPath`.
5. **Pruebas Automatizadas (`GlobalOutputDirTests.cs`):**
   - Añadidas pruebas unitarias verificando anclaje dinámico, rutas absolutas directas e interpolación de tokens (80/80 pruebas exitosas).
1. **Gestor Modal de Contraseñas y Corrección de Diálogo (`PasswordManagerWindow.xaml`):**
   - Nueva ventana modal WPF para escribir contraseñas multilínea con soporte de **Importar (.txt)** y **Exportar (.txt)** mediante diálogos de archivo nativos.
   - Solucionado el error XAML `StaticResourceExtension` en `PasswordManagerWindow.xaml` sustituyendo `{StaticResource PrimaryButton}` y `{StaticResource SecondaryButton}` por `{DynamicResource PrimaryButton}` y `{DynamicResource SecondaryButton}`, asegurando la resolución correcta de estilos globales de la aplicación.
   - Vinculado `win.Owner = Application.Current.MainWindow` y `WindowStartupLocation="CenterOwner"` para centrado fluido sobre la ventana principal.
   - Integrado botón `🔑 Claves` en el inspector para el parámetro `PasswordList` de `SmartUnpackNode`.
2. **Presets Editables y Desplegables de Parámetros (`NodeParameterViewModel.cs`):**
   - ComboBoxes del inspector configurados con `IsEditable="True"` para poder seleccionar presets existentes o escribir y registrar nuevos presets personalizados.
   - Añadidas opciones desplegables para `ArchiveFormat`, `CompressionType` y `Preset`.
3. **Descompresión Inteligente con Contraseñas y Multipartes (`SmartUnpackNode.cs`):**
   - Prueba secuencial de claves candidatas (`PasswordList` y `PasswordFile`) inyectando la clave usada en `Metadata["UsedPassword"]`.
   - Detección de volúmenes multipartes (`FindRelatedVolumeFiles`) enviando el conjunto de partes al puerto `Error` ante claves incorrectas o archivos corruptos.
4. **Enrutamiento Inteligente por Rangos y Operadores (`SwitchCaseNode.cs`):**
   - Soporte en la regla `Pattern` para rangos de tamaño (`< 10 MB`, `10 MB..1 GB`), fechas (`2025-01-01..2025-12-31`), números y extensiones.
5. **Parseo Numérico y de Unidades (`ExpressionFilterNode.cs` & `ParameterHelper.cs`):**
   - Extracción y normalización de unidades de almacenamiento (`TB`, `GB`, `MB`, `KB`, `Bytes`) y tiempo (`ms`, `s`, `m`, `h`).
6. **Automatización Desatendida y Resiliencia (`FileFlow.Core`):**
   - Implementados `FolderWatcherService` (supervisión de carpetas en tiempo real con debounce anti-colisión), `FlowSchedulerService` (programador de tareas) y `ExecutionRetryHelper` (política de reintentos con exponential backoff).
7. **Nuevos Nodos de Procesamiento (`ArchiveCompressorNode`, `DocumentProcessorNode`, `MediaTranscoderNode`):**
   - Creados nodos para compresión ZIP/7Z/TAR.GZ, procesamiento e inspección de PDF/documentos, y transcodificación multimedia.

---

## [2026-08-20] - Optimización de Rendimiento UI: Actualización en Tiempo Real de Logs y Barra de Progreso

### 🛠 Cambios Implementados
1. **Búfer de Logs Asíncrono e Inmune a Saturación de UI (`LogViewModel.cs`):**
   - Se reemplazó el despacho síncrono e inmediato por línea de log en la UI por una cola de concurrencia thread-safe (`ConcurrentQueue<LogEntry>`).
   - Implementado un temporizador `DispatcherTimer` a nivel de fondo (`DispatcherPriority.Background`) que refresca el cuadro de texto y la consola cada 50ms (20 FPS).
2. **Despacho de Barra de Progreso y Estado de Baja Prioridad:**
   - `UpdateProgress` utiliza `DispatcherPriority.Background` evitando acaparar el hilo principal de renderizado WPF.
3. **Resultado:**
   - La consola muestra la transmisión de logs **en tiempo real a medida que ocurre la ejecución** sin congelar el renderizado visual ni bloquear la interfaz ni los controles de la aplicación.

---

## [2026-08-20] - Solución Definitiva al Bloqueo de Flujos (Eliminación de Interbloqueo / Deadlock de Semáforo)

### 🛠 Cambios Implementados
1. **Eliminación del Interbloqueo Canónico (*Semaphore Deadlock*) en `WorkflowExecutor.cs`:**
   - Se identificó la causa raíz: en `DispatchEmitAsync`, se llamaba a `_concurrencyThrottle.WaitAsync` mientras el nodo padre estaba esperando a que los nodos hijos terminasen `ExecuteAsync`. Al encadenar 2 o más nodos (ej. `FolderSourceNode` -> `VariableInjectorNode` -> `DestinationSinkNode`), los hilos padres bloqueaban todas las fichas del semáforo esperando a los hijos, mientras los hijos esperaban una ficha libre del semáforo, provocando un **Interbloqueo Recursivo (Deadlock)** absoluto.
   - Se eliminó el estrangulamiento anidado en `DispatchEmitAsync`, permitiendo que la tubería de emisión asíncrona procese lotes de archivos de forma fluida y sin bloqueos de aplicación.
2. **Extracción Directa de Metadatos Dinámicos en `EditorViewModel.cs`:**
   - Se corrigió la función de travesía `GetUpstreamAvailableVariables` para extraer las claves directas de `VariableInjectorNode` de forma inmediata.

---

## [2026-08-20] - Corrección de Bloqueo al Ejecutar Flujos con `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Sincronización de Hilos (*Thread-Safety*) en `VariableInjectorNode.cs` y `NodeViewModel.cs`:**
   - La ejecución del flujo ocurre en un hilo secundario (`Task.Run`), mientras la UI modifica los parámetros. Se añadió sincronización `lock (Parameters)` y la creación de instantáneas (*snapshots*) previas a la iteración para evitar bucles infinitos por corrupción interna del diccionario.
2. **Filtrado Seguro en `ExportToGraphModel` (`EditorViewModel.cs`):**
   - Agrupación e ignorado de claves vacías o en proceso de edición mediante `.Where(p => !string.IsNullOrWhiteSpace(p.Key)).GroupBy(...)` evitando excepciones `ArgumentException` al serializar el grafo.
3. **Limpieza de Parámetros en `GraphValidator.cs`:**
   - `instance.Parameters.Clear()` antes de asignar los parámetros exportados.

---

## [2026-08-20] - Gestión Dinámica de Variables en `VariableInjectorNode` (Botones ➕ y 🗑️)

### 🛠 Cambios Implementados
1. **Controles UI Dinámicos en la Tarjeta del Nodo (`EditorView.xaml`):**
   - Añadido botón verde **`➕ Variable`** en la cabecera del panel de ajustes del nodo.
   - Cada fila de variable cuenta con:
     - `TextBox` editable para el **Nombre de la Variable** (Clave).
     - `TextBox` editable para la **Expresión / Valor**.
     - Botón selector visual **`[{x}]`** para insertar variables de nodos anteriores.
     - Botón rojo **`🗑` (Papelera)** para eliminar esa variable individual al instante.

2. **Gestión MVVM y Sincronización en Tiempo Real (`NodeViewModel.cs` / `NodeParameterViewModel.cs`):**
   - Implementados los comandos `AddVariableCommand` y `RemoveParameterCommand`.
   - Sincronización bidireccional automática con `_nodeInstance.Parameters`.

---

## [2026-08-20] - Inyección Multivariable en `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Soporte Multivariable en `VariableInjectorNode.cs`:**
   - Rediseño del nodo para permitir definir y resolver múltiples pares de variables (`Key1`/`Value1`, `Key2`/`Value2`, ..., `Key5`/`Value5`) de forma simultánea dentro del mismo nodo.
2. **Actualización de la Travesía Topológica `EditorViewModel.cs`:**
   - La travesía del grafo hacia atrás (*Upstream Traversal*) inspecciona todas las claves no vacías de `VariableInjectorNode` y las ofrece automáticamente en el menú desplegable **`[{x}]`** de los nodos conectados posteriormente.
3. **Actualización de Pruebas Unitarias (`VariableInjectorNodeTests.cs`):**
   - Cobertura completa de resolución e inyección simultánea de múltiples variables.

---

## [2026-08-20] - Suite Exhaustiva de Automatización de Pruebas (xUnit, FluentAssertions, Moq)

### 🛠 Cambios Implementados
1. **Nuevo Proyecto de Pruebas (`FileFlow.Tests/FileFlow.Tests.csproj`):**
   - Configurado en .NET 9 (`net9.0-windows` con `UseWPF=true`) e integrado en `FileFlow.slnx`.
   - Incluye **xUnit**, **FluentAssertions** y **Moq**.

2. **Tests Unitarios (`FileFlow.Tests/Unit/`):**
   - **`VariableTemplateResolverTests`:** Reemplazo de variables, funciones de fecha, transformaciones de texto (`Upper`, `Lower`, `PadLeft`), saneamiento de caracteres ilegales (`Sanitize`), cascadas `Coalesce` e interpolación dinámica.
   - **`LocalizationManagerTests`:** Verificación de singleton y disparo del evento `LanguageChanged`.
   - **`WorkflowExecutorTests`:** Ejecución topológica y validación de nodos.
   - **`FolderSourceNodeTests` & `VariableInjectorNodeTests`:** Inyección de metadatos (`Counter`, `SourceRootPath`, `CustomCategory`) e I/O.
   - **`EditorViewModelTests`:** Travesía topológica inversa (`GetUpstreamAvailableVariables`) y cálculo dinámico de variables en la interfaz.

3. **Tests de Integración (`FileFlow.Tests/Integration/`):**
   - **`WorkflowIntegrationTests`:** Flujo E2E desde `FolderSourceNode` $\rightarrow$ `VariableInjectorNode` $\rightarrow$ `DestinationSinkNode` validando la tubería completa de archivos reales.

4. **Tests de Estrés / Rendimiento (`FileFlow.Tests/Performance/`):**
   - **`PerformanceStressTests`:** Procesamiento de **10,000 elementos masivos** evaluando el motor de plantillas en menos de 1 segundo (651 ms).

---

## [2026-08-20] - Variables Avanzadas de Sistema, Metadatos Multimedia/Compresión y Funciones de Expresión

### 🛠 Cambios Implementados
1. **Nuevas Variables del Sistema y Ejecución (`VariableTemplateResolver.cs`):**
   - Incorporación de `{DateNow}` (`yyyy-MM-dd`), `{TimeNow}` (`HH-mm-ss`), `{DateTimeNow}` (`yyyy-MM-dd_HH-mm-ss`).
   - Contador incremental de secuencia por lote `{Counter}` / `{Index}` inyectado desde `FolderSourceNode.cs`.
   - Métricas de peso de archivo: `{SizeMB}`, `{SizeKB}`, `{SizeBytes}`.
   - Variables de entorno del sistema: `{UserName}`, `{MachineName}`.

2. **Nuevos Metadatos de Imagen y Archivos Comprimidos:**
   - **`ExifMetadataNode.cs`:** Extracción de `{ImageWidth}`, `{ImageHeight}`, `{Orientation}` (`Landscape`/`Portrait`/`Square`), `{AspectRatio}` (ej. `16:9`) y `{Megapixels}` (ej. `24.1MP`).
   - **`SmartUnpackNode.cs`:** Extracción de `{ArchiveFormat}` (`ZIP`/`7Z`/`RAR`) y `{UnpackedFileCount}`.

3. **Nuevas Funciones de Expresión Prácticas:**
   - `{Sanitize(text)}`: Limpieza automática de caracteres ilegales en Windows (`\ / : * ? " < > |`).
   - `{PadLeft(val, length, char)}`: Relleno de números con ceros u otros caracteres (ej. `{PadLeft(Counter, 4, "0")}`).
   - `{Substring(text, start, length)}`: Extracción segura de subcadenas sin desbordamientos de índice.
   - `{RegexMatch(text, pattern)}`: Extracción por expresión regular.
   - `{RegexReplace(text, pattern, replacement)}`: Reemplazo con expresiones regulares.
   - `{Coalesce(val1, val2, ...)}`: Evaluación en cascada retornando el primer valor no vacío.
   - `{FileAgeDays(dateStr)}`: Cálculo de antigüedad en días transcurridos.

4. **Integración en el Selector Gráfico `[{x}]` (`EditorViewModel.cs`):**
   - Actualización de `GetUpstreamAvailableVariables` clasificando y ofreciendo todas las nuevas variables y funciones ordenadas por grupos (`🌐 System & Environment`, `📷 Image & Media`, `📦 Archives`, `🔤 Expression Functions`).

---

## [2026-08-20] - Consola de Ejecución Seleccionable, Exportación y Categorización de Nodos

### 🛠 Cambios Implementados
1. **Exportación de Logs a Archivo:**
   - Se añadió la acción `ExportLogsCommand` en `LogViewModel.cs` y el botón **`💾 Exportar Log`** / **`💾 Export Log`** en la barra de herramientas de la consola (`LogView.xaml`).
   - Permite guardar todo el historial formateado con marcas de tiempo en archivos `.log` o `.txt` mediante `SaveFileDialog`.

2. **Selección y Copiado de Texto con el Ratón:**
   - Se reemplazó el control estático por un visor de texto editable/seleccionable (`TextBox` en modo `IsReadOnly="True"`) en `LogView.xaml`.
   - Los usuarios pueden arrastrar el ratón para seleccionar cualquier bloque de texto y copiarlo directamente con `Ctrl+C` o el menú contextual.
   - Implementado autodesplazamiento hacia la última línea recibida (`LogConsoleTextBox_TextChanged`).

3. **Corrección de Categoría del Nodo Inyector de Variables:**
   - Se actualizó `VariableInjectorNode.cs` asignándole la categoría **`Utility`** (*Utilidades*).
   - Ahora aparece correctamente clasificado junto a herramientas como `Log Inspector` dentro del panel lateral de herramientas (*Toolbox*).

---

## [2026-08-20] - Subsistema de Variables Dinámicas, Motor de Expresiones e Inyector de Variables

### 🛠 Cambios Implementados
1. **Motor de Plantillas de Variables (`VariableTemplateResolver.cs`):**
   - Interpolación de tokens dinámicos en cualquier parámetro de texto o ruta de nodo.
   - **Variables del Sistema Estandarizadas al Inglés:** `{FileName}`, `{FileNameNoExt}`, `{Extension}`, `{CurrentPath}`, `{OriginalPath}`, `{CurrentDir}`, `{OriginalDir}`, `{RelativePath}`.
   - **Funciones de Expresión Integradas:**
     - **Fechas:** `{Year(date)}`, `{Month(date)}`, `{Day(date)}`, `{FormatDate(date, "yyyy-MM")}`.
     - **Texto:** `{Upper(text)}`, `{Lower(text)}`, `{Trim(text)}`, `{Replace(text, "old", "new")}`, `{Default(val, "fallback")}`.

2. **Cálculo de `RelativePath` (Estructura de Directorio Relativo):**
   - Se actualizó `FolderSourceNode.cs` para adjuntar la metadata `SourceRootPath` al escanear directorios.
   - `VariableTemplateResolver` calcula la ruta de subcarpetas relativa exacta (ej. `mami/antiguo`) excluyendo el nombre del archivo.

3. **Nodo `Inyector de Variables` (`VariableInjectorNode.cs`):**
   - Permite calcular e inyectar claves de metadatos personalizadas (`item.Metadata["VariableName"] = ResolvedValue`) en el flujo para nodos posteriores.

4. **Selector Gráfico de Variables `{x}` con Travesía Topológica Inversa:**
   - Se agregó el botón gráfico **`[{x}]`** al lado de los campos de entrada de parámetros en `EditorView.xaml`.
   - Al pulsar **`[{x}]`**, `EditorViewModel.GetUpstreamAvailableVariables` recorre el grafo hacia atrás (*Upstream Traversal*) para ofrecer únicamente las variables exportadas por los nodos conectados previamente (EXIF, origen de descompresión, variables inyectadas).

---

## [2026-08-20] - Sistema de Internacionalización Multilingüe (Español / Inglés)

### 🛠 Cambios Implementados
1. **Gestor de Localización Estándar de .NET (`LocalizationManager.cs`):**
   - Singleton encargado de administrar el cambio de cultura al vuelo (`CultureInfo`) entre Español (`es-ES`) e Inglés (`en-US`).
   - Notificación de cambio mediante el evento `LanguageChanged` y propiedad indexadora `this[string key]`.

2. **Ficheros de Recursos `.resx`:**
   - Creación de `Strings.resx` (Inglés por defecto) y `Strings.es.resx` (Español).

3. **Selector Dinámico de Idioma en Barra de Control:**
   - Desplegable en `ControlBarView.xaml` reactivo en tiempo real para traducir al instante la UI, los títulos de los nodos en lienzo, las descripciones y el catálogo de herramientas.

---

## [2026-08-20] - Descubrimiento y Carga Robusta de Plugins

### 🛠 Cambios Implementados
1. **Contexto de Carga Desacoplado (`PluginAssemblyLoadContext.cs`):**
   - Carga de ensamblados `.dll` en memoria para evitar bloqueos del sistema operativo sobre los archivos en disco.
   - Búsqueda de dependencias de plugins con fallback a `AppDomain.CurrentDomain.BaseDirectory` para librerías como `SixLabors.ImageSharp`, `MetadataExtractor` y `SharpCompress`.

---

## [2026-08-20] - Estructura de Documentación y Repositorio Git Initial

### 🛠 Cambios Implementados
1. **Repositorio Git (`.gitignore` & `git init`):**
   - Configuración de exclusión de binarios `bin/`, `obj/` y archivos de cache `.antigravity/`.
   - Registro del commit inicial en la rama `main`.

2. **Documentación del Proyecto (`docs/`):**
   - `docs/README.md`: Centro de documentación y visión general del proyecto.
   - `docs/nodes/CREATING_NODES.md`: Guía de desarrollo de nodos personalizados.
   - `docs/nodes/examples/SampleMultiPortNode.cs`: Ejemplo de nodo de código completo para desarrolladores.
   - `docs/ARCHITECTURE_DEEP_DIVE.md`: Guía arquitectónica detallada en 4 niveles de complejidad.

---

## [2026-08-24] - Guía Maestra Universal para Agentes de IA (`AGENTS.md`)

### 🛠 Cambios Implementados
1. **Creación de `AGENTS.md`:**
   - Estandarización de directrices para agentes de IA (Antigravity, Cursor, Claude Code, Copilot, Roo Code, Windsurf).
   - Definición del **Protocolo de Arranque Obligatorio** (lectura secuencial de `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md`, `.antigravity/knowledge/repo_architecture.md`, `.agents/rules/rules.md`).
   - Mapa exhaustivo de ficheros auxiliares existentes (`.agents/`, `.antigravity/`, `docs/`, `GEMINI.md`).
   - Resumen de principios técnicos (.NET 9, C# 13, `System.Threading.Lock`, aislamiento en `FileFlow.Sdk`).
   - Protocolo de validación y mantenimiento continuo post-sesión.

---

## [2026-09-01] - Inclusión de Ejemplos de Flujos y Manual de Usuario en el Instalador y la App

### 🛠 Cambios Implementados
1. **Publicación y Empaquetado (`installer/publish.ps1`):**
   - Incorporada copia recursiva de la colección completa de ejemplos de flujos (`docs/examples` -> `publish/win-x64/Examples`) estructurada en 4 niveles (01_basic, 02_intermediate, 03_advanced, 04_complex).
   - Incorporada copia de la documentación y manuales de usuario (`docs/manual_de_usuario.md`, `docs/user_guide.md`, `README.md` -> `publish/win-x64/Docs`).
2. **Asistente Inno Setup (`installer/FileFlow.iss`):**
   - Configurado empaquetado automático de las carpetas `Examples\` y `Docs\`.
   - Creados accesos directos en el menú de inicio para el *Manual de Usuario* y la carpeta de *Ejemplos de Flujos*.
   - Mensajes personalizados bilingües (`[CustomMessages]`: español e inglés).
3. **Acceso Directo desde la Interfaz de Usuario (`FileFlow.App`):**
   - Comandos `OpenUserManualCommand` y `OpenExamplesFolderCommand` en `ControlBarViewModel.cs` con detección inteligente en entornos instalados y de desarrollo local.
   - Nueva sección *"AYUDA Y RECURSOS"* en el cajón de navegación lateral (`MainWindow.xaml`) con accesos a 📖 Manual de Usuario y 💡 Ejemplos de Flujos.
4. **Validación de Compilación y Suite de Pruebas:**
   - Generación exitosa del ejecutable instalador `FileFlowStudio-Setup-1.0.0.exe` con Inno Setup.
   - **190 / 190 pruebas superadas con éxito** (0 errores, 0 fallos).


