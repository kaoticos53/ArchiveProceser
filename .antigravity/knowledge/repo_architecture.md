# Arquitectura y Mapa del Repositorio - FileFlow Studio

## 1. Visión General del Proyecto
**FileFlow Studio** es un entorno de procesamiento y automatización de flujos de archivos por lotes (Batch Processing & Workflow Automation) de ultra-alta flexibilidad, modular y desacoplado, desarrollado en **C# 13**, **.NET 9** y **WPF (Nodify / MVVM)**.

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
├── FileFlow.Core/                    # Motor de Ejecución Asíncrono, Dry Run, Rollback y Concurrencia
├── FileFlow.App/                     # Aplicación de Escritorio WPF (.NET 9 Windows, Nodify, Sub-flujos, MVVM)
├── FileFlow.Plugin.FileSystem/       # Plugin de E/S de disco, escaneo, renombrado, reubicación y reciclaje Win32
├── FileFlow.Plugin.Logic/            # Plugin de control de flujo (BatchBuffer, Throttle, ForkJoin, SwitchCase, Filter)
├── FileFlow.Plugin.Hashing/          # Plugin de integridad criptográfica (SHA, MD5) y deduplicación en memoria
├── FileFlow.Plugin.Archives/         # Plugin de descompresión inteligente (SharpCompress)
├── FileFlow.Plugin.Images/           # Plugin de optimización y metadatos EXIF (ImageSharp)
├── FileFlow.Plugin.Documents/        # Plugin de documentos y PDFs (PdfSharp, PdfPig: Merge, Split, Text Extract, Metadata)
├── FileFlow.Plugin.Scripting/        # Plugin de scripting dinámico en C# (Roslyn) y JavaScript (Jint)
├── FileFlow.Plugin.Integrations/     # Plugin de integraciones externas (CLI Process Runner, Webhooks HTTP)
└── FileFlow.Tests/                   # Suite de Pruebas Unitarias e Integración xUnit (.NET 9)
```

---

## 3. Puntos de Entrada y Dependencias Clave

### Dependencias por Módulo:
- **`FileFlow.Sdk`**: Solo tipos base de `net9.0`. *Contratos puros, sin librerías externas*.
- **`FileFlow.Core`**: Depende de `FileFlow.Sdk`. Manejo de canales asíncronos (`System.Threading.Channels`), grafos topológicos, `ExecutionJournalService`, `WindowsShellFileRecycler`, `AdaptiveConcurrencyManager`.
- **`FileFlow.Plugin.*`**: Dependen exclusivamente de `FileFlow.Sdk` y librerías de dominio específicas.
- **`FileFlow.App`**: Depende de `FileFlow.Core`, `FileFlow.Sdk`, todos los plugins `FileFlow.Plugin.*`, `Nodify`, `CommunityToolkit.Mvvm` y `Microsoft.Extensions.DependencyInjection`.
- **`FileFlow.Tests`**: Depende de `FileFlow.Core`, `FileFlow.Sdk`, `FileFlow.Plugin.*`, `FileFlow.App`, `xunit`, `Moq`, `FluentAssertions`.

---

## 4. Firmas y Responsabilidades de las Principales Clases

### A. Capa SDK (`FileFlow.Sdk`)
- [`FileItemContext`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/FileItemContext.cs): `record` que representa la unidad de datos inmutable/transmutable a través del grafo.
  - `Guid Id`, `string CurrentPath`, `string OriginalPath`, `bool IsDirectory`, `long FileSizeBytes`
  - `Dictionary<string, object?> Metadata`, `HashSet<string> Tags`, `List<string> ExecutionLog`
  - `FileItemContext DeepClone()`: Clonación profunda para snapshots y bifurcaciones de puertos.
- [`IFlowNode`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/IFlowNode.cs): Contrato base de los nodos de procesamiento.
- [`ISubWorkflowNode`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/ISubWorkflowNode.cs): Contrato base para nodos compuestos y macros con sub-grafos anidados editables.
- [`IFlowExecutionContext`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/IFlowExecutionContext.cs): Contexto de ejecución inyectado a los nodos.
  - `bool IsDryRun { get; }`
  - `Task EmitAsync(string outputPortName, FileItemContext item)`
  - `void ReportProgress(double percentage, string statusMessage)`
  - `void Log(string message, LogLevel level)`
  - `void RegisterPlannedAction(PlannedAction action)`
  - `void RecordJournalEntry(JournalEntry entry)`
- [`PlannedAction`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/PlannedAction.cs): Registro de acción virtual para simulación en modo Dry Run.
- [`JournalEntry`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/JournalEntry.cs): Registro inmutable de operación atómica con delegado inverso `UndoAction`.
- [`VariableTemplateResolver`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/TemplateEngine/VariableTemplateResolver.cs): Motor de resolución de tokens con soporte de dominios `{Exif:*}`, `{Regex:*}`, `{Hash:Algorithm:Length}`, `{Date:Format}`, `{Env:Var}`, `{FileSize:Unit}` y funciones.

---

### B. Capa Core (`FileFlow.Core`)
- [`WorkflowExecutor`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowExecutor.cs): Orquestador asíncrono con soporte de Dry Run, Journaling, telemetría de edges y ejecución concurrente.
- [`ExecutionJournalService`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/ExecutionJournalService.cs): Gestor de transacciones y rollback LIFO de operaciones sobre archivos.
- [`WindowsShellFileRecycler`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WindowsShellFileRecycler.cs): Borrado nativo a la Papelera de reciclaje de Windows mediante P/Invoke a `SHFileOperationW` (`FOF_ALLOWUNDO`).
- [`AdaptiveConcurrencyManager`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/AdaptiveConcurrencyManager.cs): Particionamiento de semáforos por disco/volumen físico (I/O) y CPU.
- [`WorkflowDebugSession`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowDebugSession.cs): Coordinador de depuración interactiva con breakpoints.

---

### C. Capa de Presentación (`FileFlow.App`)
- [`MainViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/MainViewModel.cs): ViewModel raíz que ensambla `ControlBar`, `Editor`, `Toolbox`, `LogConsole` y `NodeInspector`.
- [`ControlBarViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ControlBarViewModel.cs):
  - Comandos de Ejecución, Depuración, Dry Run (`ExecuteDryRunCommand`) y Rollback (`RollbackLastExecutionCommand`).
- [`EditorViewModel`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/EditorViewModel.cs):
  - Gestión de colecciones `Nodes`, `Connections`, y `Breadcrumbs` para navegación jerárquica de sub-flujos.
  - Actualización en tiempo real de conteo de items en conexiones (`UpdateEdgeDispatched`).
- [`PresetWorkflowsService`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Services/PresetWorkflowsService.cs): Catálogo de plantillas de automatización listas para usar.

---

## 5. Principio Arquitectónico de Co-ubicación y Autonomía Total de Plugins (Self-Contained Plugins)

- **Aislamiento y Desacoplamiento (Zero-Touch en FileFlow.App)**:
  - Todo el código, lógica de nodos, servicios de dominio, herramientas modales (`UI/`), configuraciones (`Config/`) y **recursos de cadenas de texto multilingües (`Resources/Strings.resx` y `Resources/Strings.es.resx`)** pertenecientes a cada plugin/nodo **deben residir al 100% dentro del directorio del propio plugin (`FileFlow.Plugin.*`)**.
  - `FileFlow.App/Resources/` queda reservado de forma estricta y exclusiva para la interfaz anfitriona (menús globales, navegación, consola de logs y ajustes generales).
  - El motor de carga `PluginLoader` y los contratos `IPluginInitializer` registran de forma automática e instantánea los recursos del plugin en `LocalizationManager.Instance`, permitiendo extender la aplicación sin modificar el proyecto principal.
