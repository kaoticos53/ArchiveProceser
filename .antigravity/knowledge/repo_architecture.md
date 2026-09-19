# Arquitectura y Mapa del Repositorio - FileFlow Studio

## 1. Visión General del Proyecto
**FileFlow Studio** es un entorno de procesamiento y automatización de flujos de archivos por lotes (Batch Processing & Workflow Automation) de ultra-alta flexibilidad, modular y desacoplado, desarrollado en **C# 14**, **.NET 10 LTS** y **Avalonia UI 12 (Nodify / MVVM)**.

---

## 2. Mapa de Estructura de Directorios y Módulos

```
ArchiveProceser/
├── FileFlow.slnx                     # Solución XML de .NET 10 LTS
├── LICENSE                           # Licencia GNU General Public License v3.0 (GNU GPLv3)
├── GEMINI.md                         # Directivas de contexto y persistencia
├── AGENTS.md                         # Protocolo de arranque y estándares de agentes
├── .antigravity/
│   ├── mcp.json                      # Configuración de servidores MCP
│   └── knowledge/                    # Base de conocimiento viva
│       ├── session_summary.md        # Resumen ejecutivo de la última sesión
│       └── repo_architecture.md      # Este documento de arquitectura
├── FileFlow.Sdk/                     # Capa de contratos puros (C# 14, cero dependencias de UI/IO pesadas)
├── FileFlow.Core/                    # Motor DAG asíncrono, canales, telemetría, temp workspace y carga de plugins
├── FileFlow.App/                     # Aplicación de escritorio UI (Nodify, MVVM, Theme Studio, Inspector)
├── FileFlow.Plugin.FileSystem/       # Ingesta de carpetas, renombrado avanzado (9 métodos), reubicación, papelera
├── FileFlow.Plugin.Logic/            # Control de flujo (Subflujos, BatchBuffer, Throttle, ForkJoin, SwitchCase, Filter)
├── FileFlow.Plugin.Hashing/          # Integridad criptográfica (SHA, MD5) y deduplicación en memoria
├── FileFlow.Plugin.Archives/         # Descompresión universal (.NET 10, 7-Zip CLI, SharpCompress), Fan-Out, Fan-In
├── FileFlow.Plugin.Images/           # Optimización WebP/PNG/JPEG, redimensionamiento y metadatos EXIF (ImageSharp)
├── FileFlow.Plugin.Documents/        # Manipulación de documentos y PDFs (PdfSharp, PdfPig: Merge, Split, OCR)
├── FileFlow.Plugin.Data/             # Exportación e ingesta tabular/bases de datos (MiniExcel, CsvHelper, SQLite)
├── FileFlow.Plugin.Network/          # Transferencia remota y nube (HTTP, FTP/FTPS, SFTP/SSH, WebDAV, SMB)
├── FileFlow.Plugin.AI/               # Inferencia local VLM (Qwen2.5-VL/Ollama), CLIP ONNX, UltraFace, OCR Tesseract
├── FileFlow.Plugin.Scripting/        # Scripting dinámico en C# (Roslyn) y JavaScript (Jint)
├── FileFlow.Plugin.Integrations/     # Integraciones externas (CLI Process Runner, Webhooks HTTP, FFmpeg)
└── FileFlow.Tests/                   # Suite de pruebas unitarias e integración xUnit (1.064 tests, 100% éxito)
```

---

## 3. Puntos de Entrada y Dependencias Clave

### Dependencias por Módulo:
- **`FileFlow.Sdk`**: Solo tipos base de `net10.0`. *Contratos puros, sin librerías de UI ni dependencias pesadas*.
- **`FileFlow.Core`**: Depende de `FileFlow.Sdk`. Orquesta canales asíncronos (`System.Threading.Channels`), grafos DAG, `WorkflowWorkspaceManager`, `ExecutionJournalService`, `SqliteLogStore` y `AdaptiveConcurrencyManager`.
- **`FileFlow.Plugin.*`**: Dependen exclusivamente de `FileFlow.Sdk` y librerías de dominio específicas.
- **`FileFlow.App`**: Depende de `FileFlow.Core` y `FileFlow.Sdk`. Consume plugins dinámicamente mediante `PluginLoader`, Nodify, `CommunityToolkit.Mvvm` y `Microsoft.Extensions.DependencyInjection`.
- **`FileFlow.Tests`**: Batería de pruebas que valida el 100% de los componentes con `xUnit`, `FluentAssertions` y `Moq`.

---

## 4. Firmas y Responsabilidades de las Principales Clases

### A. Capa SDK (`FileFlow.Sdk`)
- [`FileItemContext`](file:///FileFlow.Sdk/FileItemContext.cs): `record` inmutable/transmutable que viaja a través del grafo.
  - `Guid Id`, `string CurrentPath`, `string OriginalPath`, `bool IsDirectory`, `long FileSizeBytes`
  - `Dictionary<string, object?> Metadata`, `HashSet<string> Tags`, `List<string> ExecutionLog`
  - `FileItemContext DeepClone()`: Clonación profunda para snapshots y bifurcaciones de puertos.
- [`IFlowNode`](file:///FileFlow.Sdk/IFlowNode.cs): Contrato base de los nodos de procesamiento con soporte de `MaxConcurrency`.
- [`IFlowExecutionContext`](file:///FileFlow.Sdk/IFlowExecutionContext.cs): Contexto de ejecución inyectado a los nodos.
  - `bool IsDryRun { get; }`, `ITempWorkspaceManager TempWorkspace { get; }`
  - `Task EmitAsync(string outputPortName, FileItemContext item)`
  - `void ReportProgress(double percentage, string statusMessage)`
  - `void ReportExecutionDuration(double durationMs)`
  - `void Log(string message, LogLevel level)`
- [`ITempWorkspaceManager`](file:///FileFlow.Sdk/Storage/ITempWorkspaceManager.cs): Gestión de ciclo de vida de archivos y directorios temporales de ejecución.
- [`IUiDispatcher`](file:///FileFlow.Sdk/Services/IUiDispatcher.cs) & [`IClipboardService`](file:///FileFlow.Sdk/Services/IClipboardService.cs): Abstracciones de infraestructura de UI desacopladas del framework de presentación.
- [`IStorageService`](file:///FileFlow.Sdk/Storage/IStorageService.cs): Operaciones de sistema de archivos físico y virtual con resolución de colisiones.
- [`IOsPlatformService`](file:///FileFlow.Sdk/Platform/IOsPlatformService.cs): Servicios de SO (shells, argumentos, papelera de reciclaje y memoria).
- [`JsonDefaults`](file:///FileFlow.Sdk/Serialization/JsonDefaults.cs): Serialización y deserialización relajada UTF-8 y formateo seguro de logs JSON.
- [`VariableTemplateResolver`](file:///FileFlow.Sdk/TemplateEngine/VariableTemplateResolver.cs): Motor de resolución de tokens `{Archive:...}`, `{Exif:...}`, `{Hash:...}`, `{Date:...}`, etc.

---

### B. Capa Core (`FileFlow.Core`)
- [`WorkflowExecutor`](file:///FileFlow.Core/Engine/WorkflowExecutor.cs): Orquestador asíncrono con control de concurrencia, canales, breakpoints y limpieza determinista de temporales.
- [`WorkflowWorkspaceManager`](file:///FileFlow.Core/Engine/WorkflowWorkspaceManager.cs): Aislador y gestor de carpetas de ejecución temporal (`Runs/{ExecutionId}/`).
- [`WorkflowTelemetryTracker`](file:///FileFlow.Core/Engine/WorkflowTelemetryTracker.cs): Telemetría atómica con deduplicación concurrente de elementos completados.
- [`SqliteLogStore`](file:///FileFlow.Core/Telemetry/SqliteLogStore.cs): Base de datos SQLite in-memory / WAL con throughput >82.000 logs/seg.
- [`PluginLoader`](file:///FileFlow.Core/Plugins/PluginLoader.cs): Carga aislada mediante `AssemblyLoadContext` con auto-registro de localización.

---

### C. Capa de Presentación (`FileFlow.App`)
- [`MainViewModel`](file:///FileFlow.App/ViewModels/MainViewModel.cs): ViewModel raíz que ensambla barra de control, editor DAG, caja de herramientas, consola y panel de inspección.
- [`EditorViewModel`](file:///FileFlow.App/ViewModels/EditorViewModel.cs): Gestión del lienzo interactivo de nodos, conexiones y sub-flujos.
- [`WpfUiDispatcher`](file:///FileFlow.App/Services/WpfUiDispatcher.cs) & [`WpfClipboardService`](file:///FileFlow.App/Services/WpfClipboardService.cs): Adaptadores concretos para WPF.
- [`CustomThemeService`](file:///FileFlow.App/Services/CustomThemeService.cs): Gestión reactiva de temas visuales (8 temas de fábrica + personalización en vivo).

---

## 5. Principios de Ingeniería y Arquitectura

1. **Inmutabilidad del Archivo de Origen por Defecto**: Los pipelines son no destructivos. La modificación del original está centralizada en `OriginalFileActionNode`.
2. **Co-ubicación y Autonomía Total de Plugins (Zero-Touch en App)**: Todo código, vistas modales (`UI/`), configuraciones (`Config/`) y diccionarios multilingües (`Resources/`) de cada nodo residen exclusivamente dentro de su propio plugin (`FileFlow.Plugin.*`).
3. **Arquitectura de Adaptadores para Modelos de IA (`Model Adapter Architecture`)**: Inferencia desacoplada mediante factorías (`[Task]AdapterFactory`) y contratos canónicos (`IVlmAdapter`, `IObjectDetectorAdapter`, `IImageClassifierAdapter`).
4. **Localización e Internacionalización Dinámica (i18n)**: Español (`es-ES`) e Inglés (`en-US`) con cambio en caliente reactivo.
