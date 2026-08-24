---
applyTo: "FileFlow.Tests/**/*.cs"
---
# Instrucciones para FileFlow.Tests

- Framework: xUnit.
- Estructura: pruebas de nodos en `Unit/Nodes/`, siguiendo el patrón de nombres `<NodeName>Tests.cs`.
- Cada nodo debe tener cobertura mínima de: caso exitoso, manejo de excepciones/errores controlados, respeto de `CancellationToken`.
- Evitar dependencias de disco real cuando sea posible; usar rutas temporales (`Path.GetTempPath()`) y limpiarlas en `Dispose`/`IAsyncLifetime`.
