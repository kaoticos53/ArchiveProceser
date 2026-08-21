# Arquitectura y Mapa del Repositorio - FileFlow Studio

## 1. Visión General del Proyecto
**FileFlow Studio** es un entorno de procesamiento y automatización de flujos de archivos por lotes (Batch Processing & Workflow Automation) de alto rendimiento, modular y desacoplado, desarrollado en **C# 13**, **.NET 9** y **WPF (Nodify / MVVM)**.

---

## 2. Mapa de Estructura de Directorios y Módulos

```
ArchiveProceser/
├── FileFlow.slnx                     # Solución XML de .NET 9
├── GEMINI.md                         # Directivas de contexto y persistencia
├── .antigravity/
│   ├── mcp.json                      # Configuración de servidores MCP (Memory & Filesystem)
│   ├── memory_store.json             # Almacén de grafo de conocimiento persistente
│   └── knowledge/
│       └── repo_architecture.md      # Este documento (Knowledge Item principal)
├── FileFlow.Sdk/                     # Capa de Contratos puros (C# 13, Cero dependencias externas)
├── FileFlow.Core/                    # Motor de Ejecución Asíncrono, Validación y Debugging
├── FileFlow.App/                     # Aplicación de Escritorio WPF (.NET 9 Windows, Nodify, MVVM)
├── FileFlow.Plugin.FileSystem/       # Plugin de E/S de disco, escaneo e inspección
├── FileFlow.Plugin.Archives/         # Plugin de descompresión inteligente (SharpCompress)
├── FileFlow.Plugin.Images/           # Plugin de optimización y metadatos EXIF (ImageSharp)
└── FileFlow.Tests/                   # Suite de Pruebas Unitarias xUnit (.NET 9)
```

---

## 3. Puntos de Entrada y Dependencias Clave

### Dependencias por Módulo:
- **`FileFlow.Sdk`**: Solo tipos base de `net9.0`. *Contratos puros, sin librerías externas*.
- **`FileFlow.Core`**: Depende de `FileFlow.Sdk`. Manejo de canales asíncronos (`System.Threading.Channels`), grafos topológicos y sesiones de depuración.
- **`FileFlow.Plugin.*`**: Dependen de `FileFlow.Sdk` y librerías de dominio (`SharpCompress`, `SixLabors.ImageSharp`).
- **`FileFlow.App`**: Depende de `FileFlow.Core`, `FileFlow.Sdk`, `FileFlow.Plugin.*`, `Nodify`, `CommunityToolkit.Mvvm` y `Microsoft.Extensions.DependencyInjection`.
- **`FileFlow.Tests`**: Depende de `FileFlow.Core`, `FileFlow.Sdk`, `FileFlow.App`, `xunit`, `Moq`, `FluentAssertions`.

---

## 4. Firmas y Responsabilidades de las Principales Clases

### A. Capa SDK (`FileFlow.Sdk`)
- [`FileItemContext`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/FileItemContext.cs): `record` que representa la unidad de datos inmutable/transmutable a través del grafo.
  - `Guid Id`, `string CurrentPath`, `string OriginalPath`, `bool IsDirectory`, `long FileSizeBytes`
  - `Dictionary<string, object?> Metadata`, `HashSet<string> Tags`, `List<string> ExecutionLog`
  - `FileItemContext DeepClone()`: Clonación profunda para snapshots y bifurcaciones de puertos.
- [`IFlowNode`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/IFlowNode.cs): Contrato base de los nodos de procesamiento.
  - `string Id`, `string Name`, `string Category`, `string Description`
  - `IReadOnlyList<NodePort> Inputs`, `IReadOnlyList<NodePort> Outputs`, `Dictionary<string, object?> Parameters`
  - `Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)`
- [`IFlowExecutionContext`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/IFlowExecutionContext.cs): Contexto de ejecución inyectado a los nodos.
  - `Task EmitAsync(string outputPortName, FileItemContext item)`
  - `void ReportProgress(double percentage, string statusMessage)`
  - `void Log(string message, LogLevel level)`
- [`NodeExecutionStatus`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/NodeExecutionStatus.cs): Enum de estados (`Idle`, `Running`, `PausedAtBreakpoint`, `PausedOnError`, `Completed`, `Faulted`).
- [`NodeDataSnapshot`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/NodeDataSnapshot.cs): Telemetría inmutable de entradas, salidas y errores por nodo.

---

### B. Capa Core (`FileFlow.Core`)
- [`WorkflowExecutor`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowExecutor.cs): Orquestador asíncrono con control de concurrencia y semáforos.
  - `Task ExecuteAsync(WorkflowGraph graph, PluginLoader loader, CancellationToken cancellationToken)`
  - `Task DispatchEmitAsync(...)`: Despacho multicanal topológico con propagación de snapshots.
  - `event Action<string, NodeExecutionStatus>? NodeStatusChanged`
  - `event Action<string, double, string>? NodeProgressChanged`
  - `event Action<double, string>? ProgressChanged`
- [`WorkflowDebugSession`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowDebugSession.cs): Coordinador de depuración interactiva.
  - `ValueTask CheckBreakpointOrStepAsync(...)`: Intercepción y pausa basada en `TaskCompletionSource` y `System.Threading.Lock`.
  - `ValueTask HandleNodeErrorAsync(...)`: Pausa automática en excepciones (*Break on Exception*).
  - `void StepNext()`, `void Continue()`, `void Pause()`
  - `void SetBreakpoints(IEnumerable<string> nodeIds)`, `void ToggleBreakpoint(string nodeId)`
- [`GraphValidator`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/GraphValidator.cs): Validación estructural, detección de ciclos y ordenación topológica (Kahn).
- [`WorkflowGraph`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowGraph.cs): DTO serializable a JSON del grafo (`Nodes`, `Edges`, `BreakpointNodeIds`).

---

### C. Capa de Presentación (`FileFlow.App`)
- [`MainViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/MainViewModel.cs): ViewModel raíz que ensambla `ControlBar`, `Editor`, `Toolbox`, `LogConsole` y `NodeInspector`.
- [`ControlBarViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ControlBarViewModel.cs):
  - `ExecuteWorkflowCommand`, `DebugWorkflowCommand`, `StepNextCommand`, `ContinueWorkflowCommand`, `TogglePauseCommand`, `StopWorkflowCommand`, `ToggleInspectorCommand`.
  - Sincronización de eventos de background mediante `Application.Current.Dispatcher.InvokeAsync`.
- [`EditorViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/EditorViewModel.cs):
  - Gestión de colecciones `Nodes` y `Connections` de Nodify.
  - `ExportToGraphModel()`, `LoadFromGraphModel()`, `ClearDebugStates()`.
- [`NodeViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/NodeViewModel.cs):
  - `ExecutionStatus`, `IsLedOn`, `ProgressPercentage`, `ProgressMessage`, `IsProgressActive`, `HasBreakpoint`.
  - Evento estático `NodeSelected` para apertura reactiva del inspector.
- [`NodeInspectorViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/NodeInspectorViewModel.cs):
  - Cálculo de Diff de metadatos (`Added`, `Modified`, `Removed`, `Unchanged`).
  - Inyección de mock files para pruebas aisladas (`TestNodeWithCustomFileCommand`).
- [`ThemeManager`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Services/ThemeManager.cs):
  - Gestor dinámico de temas con detección de preferencias de Windows en tiempo real (`AppTheme`: `Dark`, `Light`, `Pastel`, `Cyber`, `System`).
  - Diccionarios de recursos: `DarkTheme.xaml`, `LightTheme.xaml`, `PastelTheme.xaml`, `CyberTheme.xaml`, `FluentStyles.xaml`.
- [`ValueConverters.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Converters/ValueConverters.cs):
  - `BooleanToGridLengthConverter`, `NodeExecutionStatusToBrushConverter`, `DiffChangeTypeToBrushConverter`, `BreakpointToBrushConverter`, `NullToVisibilityConverter`, `InverseNullToVisibilityConverter`.
