# Auditoría 360° — FileFlow Studio

> **Fecha:** 23 de agosto de 2026  
> **Autor:** Principal Engineer / Software Architect  
> **Alcance:** Todo el repositorio (`FileFlow.Sdk`, `FileFlow.Core`, `FileFlow.App`, plugins, tests)  
> **Modo:** Solo lectura — ningún archivo modificado

---

## Resumen Ejecutivo

FileFlow Studio es una aplicación WPF de procesamiento de archivos basada en grafos de nodos con motor de ejecución paralelo, telemetría SQLite in-memory y sistema de plugins modular. El proyecto compila sin advertencias en .NET 9 / C# 13 y pasa 178/178 pruebas unitarias.

La auditoría revela **0 errores críticos bloqueantes** pero identifica **16 hallazgos accionables** distribuidos entre fugas de recursos, ineficiencias de rendimiento en hot paths, defectos de concurrencia latentes y oportunidades de mejora arquitectónica. Los hallazgos más urgentes son una fuga de `SemaphoreSlim` en el motor de ejecución y la acumulación ilimitada de tareas completadas en un `ConcurrentBag`.

---

## Tabla de Hallazgos

| ID | Severidad | Dominio | Archivo(s) | Hallazgo |
|---|---|---|---|---|
| **AUD-01** | 🔴 Alta | Recursos / Memory Leak | [WorkflowExecutor.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowExecutor.cs#L140-L150) | **SemaphoreSlim leak**: Al cambiar `MaxDegreeOfParallelism` se crea un nuevo `SemaphoreSlim` sin llamar a `.Dispose()` en el anterior. Si se ajusta la concurrencia repetidamente durante la vida de la app, cada semáforo antiguo queda huérfano en memoria con su handle nativo. |
| **AUD-02** | 🔴 Alta | Concurrencia / Memoria | [WorkflowExecutor.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowExecutor.cs#L192) | **`ConcurrentBag<Task>` crece sin límite**: `_activeNodeTasks` acumula `Task` completados pero nunca se drenan durante la ejecución. En flujos con miles de archivos, la bolsa retiene miles de objetos `Task` (completados, ~200 bytes cada uno + closures capturadas) que solo se vacían al drainear en L325-328, lo cual es secuencial y no limpia completadas en paralelo. Además, `TryTake` en un `ConcurrentBag` en un bucle `while` no garantiza que capture todas las tareas añadidas concurrentemente por otros threads entre la toma y la comprobación de vacío. |
| **AUD-03** | 🟠 Media | Concurrencia | [WorkflowExecutor.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowExecutor.cs#L453-L462) | **Race condition en `WaitIfPausedAsync`**: La lectura de `_isPaused` (L455) no está bajo lock, y entre la lectura y el `WaitAsync` (L457) el estado puede cambiar. Aunque funcional en la práctica, puede provocar que un hilo entre en espera innecesariamente o pierda una señal de pausa brevemente. |
| **AUD-04** | 🟠 Media | Rendimiento | [LogViewModel.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/LogViewModel.cs#L139-L142) | **`Logs.RemoveAt(0)` en ObservableCollection es O(n)**: Cuando se supera `MaxLiveBufferSize`, cada `RemoveAt(0)` desplaza todos los elementos, generando N notificaciones de cambio. Con 2000 logs y ráfagas altas, esto genera stuttering visible en la UI. El `FastObservableRingBuffer` ya está implementado pero **no se utiliza** aquí. |
| **AUD-05** | 🟠 Media | Recursos | [NodeViewModel.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/NodeViewModel.cs#L208) | **Event handler de localización no desenganchado**: `NodeViewModel` suscribe `LocalizationManager.Instance.LanguageChanged += OnLanguageChanged` en el constructor, pero el `Dispose()` del VM sólo desengancha correctamente si es llamado. `EditorViewModel.ClearGraph()` y `LoadFromGraphModel()` reemplazan la colección `Nodes` sin llamar a `Dispose()` en los nodos antiguos, causando retención de VMs zombis. |
| **AUD-06** | 🟠 Media | Rendimiento / UI | [EditorViewModel.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/EditorViewModel.cs#L258-L264) | **Suscripción `PropertyChanged` sin desenganche en `AddNode()`**: Cada nodo creado suscribe un lambda anónimo a `PropertyChanged`. Al eliminar nodos, el lambda retiene la referencia al `EditorViewModel`, imposibilitando el GC del nodo y del editor si se acumulan. |
| **AUD-07** | 🟡 Baja | Convención .NET 9 | [UserPreferencesService.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Services/UserPreferencesService.cs#L31), [MediaPresetManagerService.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Services/MediaPresetManagerService.cs#L24), [ExternalToolsService.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Services/ExternalToolsService.cs#L22) | **`object _lock` en lugar de `System.Threading.Lock`**: Tres servicios usan `private readonly object _lock = new()` en lugar de la primitiva `Lock` de .NET 9, violando la regla del proyecto (`.agents/rules/rules.md`). |
| **AUD-08** | 🟡 Baja | Code Smell | 26+ archivos | **`catch { }` vacíos omnipresentes**: Hay 26+ `catch { }` sin logging ni rethrow. Esto oculta errores de I/O, reflexión, serialización y permisos que dificultan severamente la depuración en producción. |
| **AUD-09** | 🟡 Baja | Rendimiento | [SqliteLogStore.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Telemetry/SqliteLogStore.cs#L206) | **`cmd.ExecuteNonQuery()` síncrono dentro de batch asíncrono**: `InsertBatchAsync` usa `cmd.ExecuteNonQuery()` (síncrono) en L206 dentro de un método async. Debería ser `await cmd.ExecuteNonQueryAsync()` para liberar el thread pool durante la escritura. |
| **AUD-10** | 🟡 Baja | Arquitectura / Singleton | [SqliteLogStore.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Telemetry/SqliteLogStore.cs#L39-L40) | **Singleton `Lazy<SqliteLogStore>` nunca se dispone**: `SqliteLogStore.Instance` se crea con `Lazy<>` pero ni `App.xaml.cs` ni ningún shutdown hook llama a `DisposeAsync()` al cerrar la aplicación. Esto puede causar pérdida de los últimos logs en el canal de ingestión y dejar la conexión SQLite keep-alive abierta. |
| **AUD-11** | 🟡 Baja | Rendimiento | [ControlBarViewModel.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ControlBarViewModel.cs#L82) | **`PreferencesChanged` sin desenganche**: `ControlBarViewModel` suscribe `UserPreferencesService.Instance.PreferencesChanged += SyncFromPreferences` pero nunca se desuscribe (no implementa `IDisposable`). Como el servicio es singleton, el VM queda retenido en memoria indefinidamente si la ventana se recrea. |
| **AUD-12** | 🟡 Baja | Concurrencia | [WorkflowDebugSession.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowDebugSession.cs#L67) | **`List<NodeDataSnapshot>` bajo `ConcurrentDictionary` con `lock(list)` manual**: El patrón `GetOrAdd` con `lock(list)` es correcto pero frágil. Si un consumidor lee sin el lock (como `SnapshotRecorded?.Invoke(snapshot)` en L72, que ocurre fuera del `lock(list)`), podría observar estado parcial. |
| **AUD-13** | 🟡 Baja | Arquitectura | [ControlBarViewModel.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ControlBarViewModel.cs#L125-L343) | **`RunWorkflowCoreAsync` es un método monolítico de 218 líneas**: Orquesta la creación del executor, la sesión de debug, los timers de UI, el manejo de eventos, la ejecución y la limpieza. Debería descomponerse en responsabilidades (setup, event wiring, teardown). |
| **AUD-14** | 🟡 Baja | Rendimiento | [EditorViewModel.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/EditorViewModel.cs#L477-L487) | **`UpdateEdgeDispatched` hace búsqueda lineal O(n)**: Itera toda la colección `Connections` por cada arista despachada. Con grafos de >100 conexiones y ráfagas de 30 FPS, esto genera microsegundos acumulados en el Dispatcher. |
| **AUD-15** | 🟡 Baja | Seguridad / Resiliencia | [App.xaml.cs](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/App.xaml.cs#L12-L21) | **Handlers de excepción global muestran `MessageBox`**: Si la excepción ocurre antes de que la ventana principal exista o en un thread background, `MessageBox.Show` puede bloquear sin dueño visible. Además, `args.Handled = true` en el Dispatcher swallows la excepción sin log persistente. |
| **AUD-16** | 🔵 Info | Testing | [FileFlow.Tests/](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Tests) | **Cobertura de tests de `WorkflowExecutor` inexistente**: El motor de ejecución (526 líneas, lógica de concurrencia, pausa, breakpoints) no tiene tests unitarios. Es el componente más complejo y crítico del sistema. |

---

## Plan de Actuación (Roadmap)

Propuesta de correcciones priorizadas por urgencia × impacto. Cada fase agrupa hallazgos que pueden corregirse de forma atómica y verificarse con `dotnet test`.

### Sprint 1 — Fugas de Recursos y Concurrencia (Crítico)

| Prioridad | ID | Acción |
|---|---|---|
| P0 | **AUD-01** | Llamar a `_concurrencyThrottle.Dispose()` antes de reasignar el `SemaphoreSlim` en el setter de `MaxDegreeOfParallelism`. |
| P0 | **AUD-02** | Reemplazar `ConcurrentBag<Task>` por un `ConcurrentQueue<Task>` con drenaje periódico de tareas completadas (e.g., drenar cada 100 tareas o al inicio de cada ciclo de dispatch), o bien usar un `Channel<Task>` con un worker que haga `await` y descarte las completadas continuamente. |
| P1 | **AUD-10** | Añadir `await SqliteLogStore.Instance.DisposeAsync()` en un override `OnExit` de `App.xaml.cs` para drenar los logs pendientes al cerrar la aplicación. |

### Sprint 2 — Memory Leaks de Event Handlers en UI

| Prioridad | ID | Acción |
|---|---|---|
| P1 | **AUD-05** | En `EditorViewModel.ClearGraph()` y en el bucle de `LoadFromGraphModel()`, llamar a `node.Dispose()` antes de vaciar la colección `Nodes`. Verificar que `NodeViewModel.Dispose()` desenganche `LanguageChanged`. |
| P1 | **AUD-06** | Reemplazar el lambda anónimo en `AddNode()` por un `WeakEventManager` o almacenar el delegate para poder desengancharlo en `RemoveNode()`. |
| P1 | **AUD-11** | Implementar `IDisposable` en `ControlBarViewModel` para desuscribirse de `PreferencesChanged`. |

### Sprint 3 — Rendimiento UI (ObservableCollection)

| Prioridad | ID | Acción |
|---|---|---|
| P2 | **AUD-04** | Reemplazar el `ObservableCollection<StructuredLogRecord> Logs` en `LogViewModel` por `FastObservableRingBuffer<StructuredLogRecord>` (ya existe en el proyecto), eliminando el costoso `RemoveAt(0)` y las notificaciones O(n). |
| P2 | **AUD-14** | Crear un `Dictionary<string, List<ConnectionViewModel>>` indexado por `sourceNodeId:portName` para O(1) lookup en `UpdateEdgeDispatched`. |

### Sprint 4 — Calidad de Código y Convenciones .NET 9

| Prioridad | ID | Acción |
|---|---|---|
| P3 | **AUD-07** | Migrar los 3 servicios de `object _lock` a `System.Threading.Lock` de .NET 9. |
| P3 | **AUD-09** | Cambiar `cmd.ExecuteNonQuery()` a `await cmd.ExecuteNonQueryAsync()` en `InsertBatchAsync`. |
| P3 | **AUD-08** | Reemplazar los `catch { }` más críticos (PluginLoader, SqliteLogStore, MediaTranscoderNode, ExternalToolsService) por `catch (Exception ex) { Debug.WriteLine(...) }` o `context.Log(...)` según contexto. Dejar los `catch { }` de limpieza de recursos (como `process.Kill()`, `Directory.Delete()`) que son intencionalmente resilientes. |

### Sprint 5 — Robustez y Resiliencia

| Prioridad | ID | Acción |
|---|---|---|
| P3 | **AUD-03** | Proteger la lectura de `_isPaused` en `WaitIfPausedAsync` con `Volatile.Read` o moverla dentro del lock existente del semáforo. |
| P3 | **AUD-15** | Añadir un handler `TaskScheduler.UnobservedTaskException` y persistir las excepciones en un archivo de crash log antes de mostrar el `MessageBox`. Envolver `MessageBox.Show` en un `try/catch` defensivo. |

### Sprint 6 — Tests de Cobertura Crítica

| Prioridad | ID | Acción |
|---|---|---|
| P4 | **AUD-16** | Crear `WorkflowExecutorTests.cs` con tests de: ejecución lineal simple, ramificación multi-arista, pausa/resume, DryRun, cancelación vía CancellationToken y gestión de errores con nodos que lanzan excepciones. |

---

## Open Questions

> [!IMPORTANT]
> **¿Hasta dónde llevar la limpieza de `catch { }`?** Algunos son intencionales (limpieza de procesos, borrado de temporales). ¿Prefieres un approach conservador (solo los que ocultan errores lógicos) o uno estricto (todos deben logear aunque sea a `Debug.WriteLine`)?

> [!IMPORTANT]
> **¿Migrar `LogViewModel.Logs` a `FastObservableRingBuffer`?** Esto cambiará el contrato de la colección (`Insert`, `Remove`, `RemoveAt` lanzarán `NotSupportedException`). ¿Hay algún consumidor de la UI que dependa de estas operaciones? La inspección sugiere que no, pero conviene confirmar.

> [!IMPORTANT]
> **¿Priorizar los tests del `WorkflowExecutor` (AUD-16)?** Son los más valiosos para evitar regresiones pero también los más costosos de implementar al requerir mocks del grafo y del PluginLoader. ¿Lo dejamos para un sprint posterior o lo aceleramos?
