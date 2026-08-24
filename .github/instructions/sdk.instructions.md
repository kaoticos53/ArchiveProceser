---
applyTo: "FileFlow.Sdk/**/*.cs"
---
# Instrucciones para FileFlow.Sdk

- Este proyecto debe permanecer **puro**: prohibido añadir referencias a paquetes NuGet de UI o librerías externas pesadas.
- Todo tipo público debe ser inmutable donde sea posible (usar `record` para DTOs como `StructuredLogRecord`).
- Los accesores calculados sobre `FileItemContext` deben estar memoizados para evitar asignaciones repetidas.
- Cambios en `IFlowNode`, `IFlowExecutionContext` o `FileItemContext` son *breaking changes*: deben documentarse en `docs/api_reference.md` y revisarse su impacto en todos los `FileFlow.Plugin.*`.
