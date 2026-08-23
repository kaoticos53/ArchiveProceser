# Fase 2 — Task List de Correcciones

## Sprint 1 — Fugas de Recursos y Concurrencia (Crítico)
- [ ] **AUD-01**: Dispose del SemaphoreSlim antiguo en `WorkflowExecutor.MaxDegreeOfParallelism`
- [ ] **AUD-02**: Reemplazar `ConcurrentBag<Task>` por estructura con drenaje de completados
- [ ] **AUD-10**: Añadir `SqliteLogStore.DisposeAsync()` en `App.OnExit`

## Sprint 2 — Memory Leaks de Event Handlers en UI
- [ ] **AUD-05**: Dispose de NodeViewModels al limpiar/recargar grafos
- [ ] **AUD-06**: Desenganche de PropertyChanged en AddNode/RemoveNode
- [ ] **AUD-11**: IDisposable en ControlBarViewModel + desuscripción de PreferencesChanged

## Sprint 3 — Rendimiento UI
- [ ] **AUD-04**: Migrar LogViewModel.Logs a FastObservableRingBuffer
- [ ] **AUD-14**: Indexar UpdateEdgeDispatched con Dictionary

## Sprint 4 — Calidad de Código y Convenciones .NET 9
- [ ] **AUD-07**: Migrar 3 servicios de `object _lock` a `Lock`
- [ ] **AUD-09**: ExecuteNonQuery → ExecuteNonQueryAsync en InsertBatchAsync
- [ ] **AUD-08**: Añadir logging a catch vacíos críticos

## Sprint 5 — Robustez y Resiliencia
- [ ] **AUD-03**: Volatile.Read en WaitIfPausedAsync
- [ ] **AUD-15**: Crash log persistente + TaskScheduler.UnobservedTaskException

## Sprint 6 — Tests de Cobertura Crítica
- [ ] **AUD-16**: WorkflowExecutorTests.cs

## Validación Final
- [ ] `dotnet build FileFlow.slnx` — 0 warnings, 0 errors
- [ ] `dotnet test FileFlow.slnx` — 100% pass
- [ ] Actualizar docs/PROJECT_WALKTHROUGH.md y session_summary.md
