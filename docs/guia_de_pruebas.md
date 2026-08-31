# Guía y Catálogo Exhaustivo de Pruebas - FileFlow Studio

Este documento constituye el manual técnico oficial de pruebas automatizadas de **FileFlow Studio**, documentando el **Objeto**, el **Qué** (comportamiento validado) y el **Cómo** (estrategia técnica AAA: *Arrange, Act, Assert*) de las 190 pruebas que componen la suite de calidad del sistema.

---

## 🏛️ 1. Arquitectura y Estrategia de Testing

El sistema de pruebas de FileFlow Studio está diseñado bajo los siguientes principios de ingeniería en **.NET 9 / C# 13**:

1. **Aislamiento Total y Limpieza Determinista**:
   - Toda prueba que manipula archivos reales crea un directorio temporal único con GUID (`Path.Combine(Path.GetTempPath(), "FileFlowTest_" + Guid.NewGuid())`) dentro de un bloque `try-finally` para asegurar su eliminación incluso ante fallos inesperados.
2. **Mocking y Desacoplamiento**:
   - Se utiliza **Moq** para simular interfaces del orquestador (`IFlowExecutionContext`, `IExecutionJournal`, `ILogStore`), garantizando pruebas unitarias puras y ultrarrápidas sin dependencias de I/O innecesarias.
3. **Validación de Concurrencia y Thread-Safety**:
   - Pruebas de estrés multihilo con `Parallel.For`, `Task.WhenAll` y primitivas de .NET 9 (`System.Threading.Lock`) para certificar la ausencia de condiciones de carrera en buffers, telemetría y canales DAG.
4. **Modo Simulación (Dry Run)**:
   - Verificación de que ningún nodo modifique el sistema de archivos cuando el contexto declare `IsDryRun = true`, validando el registro exclusivo de instancias `PlannedAction`.

---

## 📋 2. Comandos de Ejecución

```powershell
# Ejecutar todas las pruebas unitarias, integración y rendimiento
dotnet test FileFlow.slnx

# Ejecutar mediante el script PowerShell del proyecto
.\test.ps1

# Ejecutar con análisis y generación de cobertura de código
.\coverage.ps1

# Ejecutar solo una categoría específica
dotnet test --filter "FullyQualifiedName~FileFlow.Tests.Unit.Plugins"
dotnet test --filter "FullyQualifiedName~FileFlow.Tests.Integration"
dotnet test --filter "FullyQualifiedName~FileFlow.Tests.Performance"
```

---

## 🗺️ 3. Catálogo Detallado de Pruebas por Módulos

---

### 📦 Módulo 1: SDK y Contratos (`FileFlow.Tests/Unit/Sdk`)

#### 1.1. `FileItemContextTests.cs` y `FileItemContextExhaustiveTests.cs`
- **`Constructor_ShouldInitializePropertiesCorrectly`**:
  - **Objeto:** Constructor base de `FileItemContext`.
  - **Qué:** Valida que las propiedades esenciales (`Id`, `OriginalPath`, `CurrentPath`, `FileName`, `Extension`, `Tags`, `Metadata`) se inicialicen con valores correctos y no nulos.
  - **Cómo:** *Arrange:* Se crea un archivo temporal. *Act:* Se instancia `FileItemContext(path)`. *Assert:* Se comprueba mediante FluentAssertions que `CurrentPath` y `OriginalPath` coincidan y las colecciones estén vacías.
- **`AddLog_ShouldAppendLogWithTimestamp`**:
  - **Objeto:** Método `AddLog(message)`.
  - **Qué:** Verifica que cada mensaje agregado al log de ejecución contenga una marca de tiempo formateada y se almacene en orden cronológico.
  - **Cómo:** *Act:* Se invoca `item.AddLog("Paso 1")`. *Assert:* Se comprueba que `item.ExecutionLog` tenga un elemento con el prefijo temporal `[yyyy-MM-dd HH:mm:ss]`.
- **`Clone_ShouldPerformDeepCopyOfCollections`**:
  - **Objeto:** Método `Clone()` de `FileItemContext`.
  - **Qué:** Garantiza la inmutabilidad y el aislamiento entre ramas paralelas del grafo DAG al clonar metadatos, tags y trazas sin compartir referencias en memoria.
  - **Cómo:** *Arrange:* Se crea un ítem con tags y metadatos. *Act:* Se clona el ítem y se modifican los tags en el clon. *Assert:* Se valida que el ítem original permanezca inalterado.

#### 1.2. `VariableTemplateResolverTests.cs` y `SystemVariablesResolverExhaustiveTests.cs`
- **`Resolve_ShouldReplaceSystemTokensCorrectly`**:
  - **Objeto:** Motor de resolución de plantillas `VariableTemplateResolver`.
  - **Qué:** Valida el reemplazo de tokens globales del sistema (`{Date:yyyyMMdd}`, `{Year}`, `{Month}`, `{SourceDir}`, `{FileNameWithoutExtension}`).
  - **Cómo:** *Arrange:* Se define un patrón con tokens y un `FileItemContext`. *Act:* Se ejecuta `VariableTemplateResolver.Resolve(pattern, context)`. *Assert:* Se verifica que las cadenas resultantes coincidan con las fechas y rutas reales.
- **`Resolve_ShouldHandleCustomVariablesAndFallback`**:
  - **Objeto:** Resolución de variables definidas por el usuario (`{CustomVar:-default}`).
  - **Qué:** Asegura que si la variable existe en `context.Variables` se use su valor, y si no, se aplique el valor fallback por defecto.
  - **Cómo:** *Arrange:* Patrón con variables con y sin valor en el contexto. *Act:* Se evalúa la resolución. *Assert:* Se verifica la sustitución adecuada o el valor por defecto.

#### 1.3. `TokenEngineTests.cs` y `TemplateEngineAdvancedEdgeCasesTests.cs`
- **`Sanitize_ShouldRemoveWindowsIllegalCharacters`**:
  - **Objeto:** Sanitización de nombres de archivo y rutas.
  - **Qué:** Comprueba que caracteres prohibidos en sistemas Windows (`\`, `/`, `:`, `*`, `?`, `"`, `<`, `>`, `|`) sean neutralizados o sustituidos para prevenir excepciones de I/O.
  - **Cómo:** *Arrange:* Cadena con caracteres ilegales. *Act:* Se pasa por el sanitizador. *Assert:* Se valida que la cadena final sea un nombre de archivo válido.

#### 1.4. `GlobalOutputDirTests.cs`
- **`ResolveGlobalOutput_ShouldApplyConfiguredRoot`**:
  - **Objeto:** Resolución del directorio de salida global.
  - **Qué:** Valida que si se configura una ruta base global en el entorno, las rutas relativas se anclen a dicho directorio raíz.
  - **Cómo:** *Act:* Se evalúa el resolvedor con y sin ruta global. *Assert:* Se compara la ruta combinada final.

#### 1.5. `LocalizationManagerTests.cs`
- **`Get_ShouldReturnLocalizedStringsInEnglishAndSpanish`**:
  - **Objeto:** Gestor de localización `LocalizationManager`.
  - **Qué:** Asegura la correcta recuperación de textos según el idioma seleccionado (`en`, `es`).
  - **Cómo:** *Act:* Se alterna la cultura y se solicitan claves de recursos. *Assert:* Se verifican las traducciones exactas de las cadenas.

---

### ⚙️ Módulo 2: Core y Motor de Flujos (`FileFlow.Tests/Unit/Core` y `Unit/`)

#### 2.1. `WorkflowExecutorTests.cs`
- **`ExecuteWorkflowAsync_ShouldProcessLinearGraphSuccessfully`**:
  - **Objeto:** Orquestador de flujos `WorkflowExecutor`.
  - **Qué:** Valida la ejecución de extremo a extremo de un grafo lineal de nodos interconectados (Source $\rightarrow$ Transform $\rightarrow$ Sink).
  - **Cómo:** *Arrange:* Se crea un workflow con 3 nodos conectados. *Act:* Se ejecuta con `CancellationToken.None`. *Assert:* Se verifica que todos los elementos lleguen al nodo final y se emita el estado de éxito.
- **`ExecuteWorkflowAsync_ShouldPropagateCancellationImmediately`**:
  - **Objeto:** Cancelación asíncrona en `WorkflowExecutor`.
  - **Qué:** Verifica que al cancelar el `CancellationTokenSource`, el motor detenga inmediatamente los canales `Channel<T>` sin bloquear hilos de trabajo.
  - **Cómo:** *Arrange:* Grafo con retardo inducido. *Act:* Se cancela el token tras 50ms. *Assert:* Se comprueba que el flujo termine en estado `Cancelled`.

#### 2.2. `GraphValidatorTests.cs`
- **`Validate_ShouldDetectCyclesInGraph`**:
  - **Objeto:** Validador topológico de grafos `GraphValidator`.
  - **Qué:** Detecta bucles infinitos y ciclos en las conexiones entre nodos (DAG Invariant).
  - **Cómo:** *Arrange:* Se crea un grafo con conexión cíclica (A $\rightarrow$ B $\rightarrow$ C $\rightarrow$ A). *Act:* Se valida el grafo. *Assert:* Se comprueba que `ValidationResult.IsValid` sea `false` y contenga el error de ciclo.
- **`Validate_ShouldDetectOrphanAndMismatchedPorts`**:
  - **Objeto:** Validación de compatibilidad de tipos de puertos.
  - **Qué:** Comprueba que no se puedan conectar puertos de salida con tipos incompatibles a puertos de entrada.
  - **Cómo:** *Arrange:* Conexión de tipos dispares. *Act:* Se valida. *Assert:* Error reportado indicando discrepancia de tipos.

#### 2.3. `WorkflowDebugSessionTests.cs`
- **`StepOver_ShouldPauseAtNextNode`**:
  - **Objeto:** Sesión interactiva de depuración `WorkflowDebugSession`.
  - **Qué:** Valida la pausa determinista en puntos de interrupción (Breakpoints) y la ejecución paso a paso (Step-Over) inspeccionando los metadatos en tránsito.
  - **Cómo:** *Arrange:* Se fijan breakpoints en un nodo intermedio. *Act:* Se lanza la sesión de depuración. *Assert:* El motor se suspende en el nodo, permitiendo inspeccionar el `FileItemContext` antes de reanudar.

#### 2.4. `DryRunAndJournalTests.cs`
- **`DryRun_ShouldRegisterPlannedActionsWithoutTouchingDisk`**:
  - **Objeto:** Modo de simulación `IsDryRun` y diario de ejecución `IExecutionJournal`.
  - **Qué:** Garantiza que al activar el modo Dry Run no se creen, muevan o eliminen archivos en el disco físico, registrando únicamente las acciones planeadas en el diario.
  - **Cómo:** *Arrange:* Nodos de borrado y movimiento configurados con `IsDryRun = true`. *Act:* Se ejecuta el flujo. *Assert:* No hay cambios en el sistema de archivos real y el diario contiene las `PlannedAction` correspondientes.

#### 2.5. `SqliteLogStoreTests.cs`, `PagedLogStoreTests.cs` y `ExecutionTelemetryTests.cs`
- **`AppendLogAsync_ShouldPersistLogsToMemorySqliteHighSpeed`**:
  - **Objeto:** Almacén de telemetría y logs `SqliteLogStore`.
  - **Qué:** Verifica el rendimiento y la persistencia de miles de trazas en una base de datos SQLite In-Memory protegida contra escrituras concurrentes.
  - **Cómo:** *Arrange:* Base de datos SQLite en memoria. *Act:* Se insertan 1.000 trazas concurrentes con `Parallel.For`. *Assert:* Todas las trazas se consultan con integridad y paginación correcta.

---

### 🧩 Módulo 3: Plugins y Nodos (`FileFlow.Tests/Unit/Plugins`)

#### 3.1. `OperationReportNodeTests.cs` (12 Pruebas)
- **`ExecuteAsync_ShouldGenerateHtmlReport_WhenFormatIsHtml`**:
  - **Objeto:** Renderizado de reporte HTML en `OperationReportNode`.
  - **Qué:** Valida la creación de un informe HTML interactivo con estilos modernos, timeline de pasos y tablas de metadatos.
  - **Cómo:** *Arrange:* Ítem con trazas de EXIF y Hash. *Act:* Se procesa con `ReportFormat = "HTML"`. *Assert:* Archivo `.html` generado conteniendo los KPIs y nombres de archivos.
- **`ExecuteAsync_ShouldGenerateMarkdownReport_WhenFormatIsMarkdown`**:
  - **Objeto:** Renderizado Markdown.
  - **Qué:** Verifica la generación de sintaxis Markdown válida con tablas y bloques colapsables `<details>`.
  - **Cómo:** *Act:* `ReportFormat = "Markdown"`. *Assert:* Archivo `.md` generado con estructura de tablas de resumen.
- **`ExecuteAsync_ShouldGenerateTextReport_WhenFormatIsText`**:
  - **Objeto:** Renderizado en texto plano.
  - **Qué:** Valida la estructura limpia y tabulada en formato `.txt`.
  - **Cómo:** *Act:* `ReportFormat = "Text"`. *Assert:* Archivo `.txt` generado con cabeceras y detalles.
- **`ExecuteAsync_ShouldGenerateJsonAndCsvReports`**:
  - **Objeto:** Renderizadores estructurados JSON y CSV.
  - **Qué:** Asegura la exportación deserializable en JSON y el formato tabular delimitado por comas en CSV con la columna `Directory`.
  - **Cómo:** *Act:* Se generan ambos reportes. *Assert:* El JSON es parseado con `JsonDocument` y el CSV contiene las cabeceras requeridas.
- **`ExecuteAsync_ShouldProduceOnlyOneConsolidatedReportFile_WhenMultipleFilesProcessed`**:
  - **Objeto:** Ámbito de reporte consolidado (`ReportScope = "Consolidated"`).
  - **Qué:** Valida que cuando un lote de $N$ archivos pasa por el nodo, se genere **exactamente un único archivo de informe consolidado** acumulativo y no múltiples archivos dispersos.
  - **Cómo:** *Arrange:* Lote de 5 archivos en la misma ejecución. *Act:* Se ejecutan secuencialmente en el nodo. *Assert:* Se comprueba mediante `Directory.GetFiles` que solo exista 1 fichero de reporte y que contenga los 5 elementos.
- **`ExecuteAsync_ShouldGroupOperationsByDirectory_WhenGroupByIsDirectory`**:
  - **Objeto:** Agrupación dinámica por directorios (`GroupBy = "Directory"`).
  - **Qué:** Verifica que el reporte agrupe jerárquicamente los archivos por sus carpetas de origen en acordeones colapsables con sus respectivos recuentos y volúmenes.
  - **Cómo:** *Arrange:* Archivos en 2 subcarpetas distintas (`Fotos_2026` y `Documentos_PDF`). *Act:* Se procesan con `GroupBy = "Directory"`. *Assert:* El HTML contiene los grupos `Fotos_2026` y `Documentos_PDF` con sus pastillas de conteo y botones de despliegue.

#### 3.2. `FolderSourceNodeTests.cs`
- **`ExecuteAsync_ShouldScanFilesRecursively_AndEmitToOutPort`**:
  - **Objeto:** Nodo de escaneo `FolderSourceNode`.
  - **Qué:** Valida la enumeración recursiva asíncrona de archivos en disco respetando filtros globbing (`*.jpg`, `*.pdf`).
  - **Cómo:** *Arrange:* Árbol de carpetas con archivos de prueba. *Act:* Se ejecuta el nodo. *Assert:* Se verifica la emisión de cada archivo individual al puerto `Out`.

#### 3.3. `DestinationSinkNodeTests.cs`
- **`ExecuteAsync_ShouldHandleCollisionStrategiesCorrectly`**:
  - **Objeto:** Nodo de escritura final `DestinationSinkNode`.
  - **Qué:** Comprueba las políticas de colisión de nombres (`Overwrite`, `Skip`, `AutoIncrement` ej. `archivo_1.txt`).
  - **Cómo:** *Arrange:* Archivo preexistente en destino. *Act:* Se envía un nuevo archivo con el mismo nombre bajo cada estrategia. *Assert:* En `AutoIncrement` se genera `archivo_1.txt`, en `Overwrite` se sobreescribe y en `Skip` se omite.

#### 3.4. `AdvancedRenamerExhaustiveTests.cs`
- **`ExecuteAsync_ShouldRenameFilesWithDynamicTokensAndSequence`**:
  - **Objeto:** Nodo de renombrado masivo `AdvancedRenamerNode`.
  - **Qué:** Valida la sustitución de tokens dinámicos, secuencias numéricas con padding (`{Seq:000}`) y sanitización de nombres.
  - **Cómo:** *Arrange:* Archivo original `documento.pdf`. *Act:* Patrón `{Date:yyyy-MM-dd}_Doc_{Seq:000}`. *Assert:* Nombre resultante `2026-08-31_Doc_001.pdf`.

#### 3.5. `SafeRecycleDeleteNodeExhaustiveTests.cs` y `OriginalFileActionNodeTests.cs`
- **`ExecuteAsync_ShouldMoveFilesToQuarantineOrRecycleBin`**:
  - **Objeto:** Políticas de ciclo de vida del archivo origen.
  - **Qué:** Valida las acciones de preservar (`Keep`), mover a cuarentena o eliminar mediante la Papelera de reciclaje de Windows.
  - **Cómo:** *Arrange:* Archivo de prueba en carpeta temporal. *Act:* Se ejecuta con `ActionType = MoveToQuarantine`. *Assert:* El archivo desaparece del origen y aparece en el directorio de cuarentena configurado.

#### 3.6. `BatchBufferNodeRuleTests.cs`
- **`ExecuteAsync_ShouldBufferItemsUntilBatchSizeReached`**:
  - **Objeto:** Nodo de agrupación en memoria `BatchBufferNode`.
  - **Qué:** Acumula elementos hasta alcanzar el tamaño de lote configurado antes de emitirlos juntos.
  - **Cómo:** *Arrange:* `BatchSize = 3`. *Act:* Se envían 2 elementos (no se emiten), se envía el 3° elemento. *Assert:* Se emiten los 3 elementos en lote y se notifica en `BatchCompleted`.

#### 3.7. `LogicAndHashingNodesTests.cs`
- **`SwitchCase_ShouldRouteToMatchedPort`**:
  - **Objeto:** Enrutador condicional `SwitchCaseNode`.
  - **Qué:** Evalúa expresiones o metadatos y desvía el archivo al puerto correspondiente.
  - **Cómo:** *Arrange:* Reglas para extensiones `.jpg` y `.zip`. *Act:* Se envía un `.jpg`. *Assert:* El ítem sale exclusivamente por el puerto asignado a imágenes.

---

### 🖥️ Módulo 4: UI y Presentación MVVM (`FileFlow.Tests/Unit/App`)

#### 4.1. `EditorViewModelTests.cs`
- **`RunWorkflowCommand_ShouldStartExecutionAndSetRunningState`**:
  - **Objeto:** ViewModel del lienzo `EditorViewModel`.
  - **Qué:** Valida que el comando de ejecución inicie el motor DAG, actualice el estado a `IsRunning = true` y deshabilite comandos incompatibles.
  - **Cómo:** *Arrange:* ViewModel con grafo válido. *Act:* Se ejecuta `RunWorkflowCommand`. *Assert:* Se comprueban los flags reactivos y la llamada al motor.

#### 4.2. `ToolboxViewModelTests.cs`
- **`Constructor_ShouldDiscoverAllRegisteredPluginsAndNodes`**:
  - **Objeto:** ViewModel de la paleta de herramientas `ToolboxViewModel`.
  - **Qué:** Asegura el descubrimiento de los 27 nodos disponibles en el catálogo mediante `IPluginManager`, con protección de concurrencia `Lock` e iconos asignados.
  - **Cómo:** *Arrange:* `PluginLoader` configurado. *Act:* Se instancia `ToolboxViewModel`. *Assert:* `AvailableNodes` contiene 27 nodos clasificados por categoría.

#### 4.3. `WorkflowStorageServiceTests.cs`
- **`SaveAndLoad_ShouldPreserveGraphTopologyAndParameters`**:
  - **Objeto:** Servicio de persistencia `WorkflowStorageService`.
  - **Qué:** Verifica la serialización y deserialización polimórfica JSON de grafos de flujo, posiciones X/Y, conexiones y parámetros sin pérdida de tipos.
  - **Cómo:** *Arrange:* Grafo complejo en memoria. *Act:* Se serializa a JSON y se vuelve a cargar. *Assert:* El grafo reconstruido es idéntico en nodos, conexiones y valores de parámetros.

---

### 🔗 Módulo 5: Pruebas de Integración y Rendimiento (`Integration/` y `Performance/`)

#### 5.1. `WorkflowIntegrationTests.cs`
- **`FullPipeline_IngestUnpackRenameReportAndSink_ShouldSucceed`**:
  - **Objeto:** Flujo E2E integral de procesamiento masivo.
  - **Qué:** Valida la interoperabilidad de 6 nodos en cadena (FolderSource $\rightarrow$ ArchiveFilter $\rightarrow$ SmartUnpack $\rightarrow$ AdvancedRenamer $\rightarrow$ OperationReport $\rightarrow$ DestinationSink).
  - **Cómo:** *Arrange:* Archivo ZIP con contenido mixto. *Act:* Se procesa el flujo completo. *Assert:* Los archivos se descomprimen, se renombran con metadatos, se genera el reporte consolidado y se escriben en el destino final.

#### 5.2. `EngineParallelStressTests.cs` y `PerformanceBenchmarkSuiteTests.cs`
- **`TelemetryStress_ShouldExceed80kLogsPerSecond`**:
  - **Objeto:** Benchmark de rendimiento y saturación de telemetría.
  - **Qué:** Mide el throughput del sistema de logs SQLite In-Memory bajo saturación máxima multihilo.
  - **Cómo:** *Arrange:* 28 tareas en paralelo. *Act:* Se registran 100.000 logs en ráfaga. *Assert:* La velocidad media de inserción supera los **80.000 registros/segundo** sin bloqueos ni excepciones.
