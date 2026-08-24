# Instrucciones para agentes IA - FileFlow Studio

> Documentación ampliada en `docs/` (índice en `docs/README.md`).

## Contexto del proyecto
FileFlow Studio es una plataforma de automatización y procesamiento masivo de archivos (Batch File Processing & Workflow Automation) construida con **C# 13**, **.NET 9** y **WPF**. Ejecuta flujos de trabajo visuales basados en grafos dirigidos (DAG) con nodos de procesamiento extensibles vía plugins.

## Proyectos de la solución (`FileFlow.slnx`)
- `FileFlow.Sdk` — contratos base puros.
- `FileFlow.Core` — orquestación y telemetría.
- `FileFlow.Plugin.Archives`, `FileFlow.Plugin.FileSystem`, `FileFlow.Plugin.Hashing`, `FileFlow.Plugin.Images`, `FileFlow.Plugin.Integrations`, `FileFlow.Plugin.Logic` — nodos de dominio (24 nodos en total).
- `FileFlow.App` — UI WPF (MVVM).
- `FileFlow.Tests` — suite xUnit (178+ tests).

## Arquitectura por capas (respetar estrictamente)
- `FileFlow.Sdk`: contratos puros (`IFlowNode`, `FileItemContext`, `IFlowExecutionContext`, `VariableTemplateResolver`, `StructuredLogRecord`). Cero dependencias de UI o librerías externas pesadas.
- `FileFlow.Plugin.*`: solo pueden referenciar `FileFlow.Sdk` y sus propias dependencias de dominio.
- `FileFlow.Core`: `WorkflowExecutor`, `PluginLoader`, `FolderWatcherService`, `SqliteLogStore`, `AdaptiveConcurrencyManager`.
- `FileFlow.App`: MVVM estricto con `CommunityToolkit.Mvvm`.

No romper este desacoplamiento al generar o modificar código.

## Estándares de código
- **Target Framework**: `net9.0` (`net9.0-windows` para `FileFlow.App`).
- **Lenguaje**: C# 13 (`<LangVersion>13</LangVersion>`).
- **Nullable Reference Types**: estricto, sin advertencias de desreferencia nula sin mitigar.
- **Sincronización**: usar `System.Threading.Lock`, nunca `object` como monitor.
- **I/O asíncrono**: todas las operaciones de disco/red deben ser `Task`/`ValueTask` con `CancellationToken` propagado obligatoriamente, usando `.ConfigureAwait(false)` en librerías.
- **Recursos**: `await using` / `using var` para liberación determinista.
- **Rendimiento**: evitar asignaciones innecesarias en hot paths (`ReadOnlySpan<T>`, accesores memoizados como en `FileItemContext`).

## Creación de nuevos nodos de procesamiento (`IFlowNode`)
1. Ubicar la clase en el `FileFlow.Plugin.<Dominio>` correspondiente.
2. Implementar `IFlowNode` (contrato completo y ejemplo en `docs/api_reference.md`).
3. Medir tiempo con `Stopwatch` y emitir telemetría vía `context.Log(...)` con `nodeId`, `nodeName`, `durationMs`, `itemId`, `detailsJson` (JSON serializado).
4. Emitir el resultado con `context.EmitAsync(item, pinName)`.
5. Validar configuración en `ValidateConfiguration()`.
6. Añadir tests xUnit en `FileFlow.Tests/Unit/Nodes/` cubriendo: caso exitoso, manejo de excepciones, respeto de `CancellationToken`.

## Git y commits
- Ramas: `feature/<nombre>`, `fix/<nombre>`, `refactor/<nombre>`. `main` siempre debe compilar y pasar el 100% de tests.
- Conventional Commits: `feat:`, `fix:`, `perf:`, `test:`, `docs:`.

## Validación antes de commit
```powershell
dotnet build FileFlow.slnx -c Release
dotnet test FileFlow.slnx -c Release
dotnet format --verify-no-changes
```
Todos los tests (178+) deben pasar al 100%.

## Referencias detalladas
- Arquitectura y diagramas: `docs/architecture.md`
- Contratos SDK completos: `docs/api_reference.md`
- Guía de contribución completa: `docs/contributing.md`
- Manual de usuario: `docs/user_guide.md`
- Instalación/despliegue: `docs/setup_and_deployment.md`

## Paquete npm para búsqueda
- El paquete npm `@modelcontextprotocol/server-ripgrep` no existe (404). La alternativa real y mantenida para búsqueda ripgrep vía MCP es `mcp-ripgrep` (github.com/mcollina/mcp-ripgrep).
