---
applyTo: "FileFlow.Plugin.*/**/*.cs"
---
# Instrucciones para proyectos FileFlow.Plugin.*

- Solo se permite referenciar `FileFlow.Sdk` y librerías de dominio específicas del plugin (p. ej. compresión en `Archives`, hashing en `Hashing`).
- Cada nodo debe implementar `IFlowNode` completo, incluyendo `ValidateConfiguration()`.
- Emitir telemetría estructurada con `context.Log(...)` incluyendo `nodeId`, `nodeName`, `durationMs`, `itemId`, `detailsJson`.
- Propagar siempre `context.CancellationToken` en operaciones de I/O.
- Añadir tests unitarios xUnit correspondientes en `FileFlow.Tests/Unit/Nodes/` para cada nodo nuevo o modificado.
