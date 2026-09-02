# Resumen Consolidado de Sesiones y Memoria de Proyecto - FileFlow Studio

Este documento se actualiza al finalizar cada sesión de trabajo para consolidar los puntos clave, decisiones arquitectónicas, capacidades del sistema y el estado de la solución, evitando empezar desde cero en futuras conversaciones.

---

## 1. Estado Actual del Repositorio y Calidad
- **Target Framework**: `.NET 9` (`net9.0` / `net9.0-windows` para WPF UI) con preparación para .NET 10.
- **Lenguaje**: `C# 13` (`<LangVersion>13</LangVersion>`), Nullable activado de forma estricta.
- **Estado de Compilación**: `dotnet build FileFlow.slnx --warnaserror` $\rightarrow$ **0 Advertencias, 0 Errores**.
- **Suite de Pruebas**: `dotnet test FileFlow.slnx` $\rightarrow$ **303 / 303 Pruebas Pasadas con 100% de Éxito** (Unit, Integration, Security, Concurrency, JSON Configuration Loaders, AppPaths Storage & Portable Mode Provider & Performance Benchmarks en xUnit).
- **Proveedor Centralizado de Rutas (`AppPaths`) y Soporte Nativo para Modo Portable**:
  - Unificación de carpetas de usuario bajo `%AppData%/FileFlow/` y soporte autónomo para modo portable (`portable.dat` o carpeta `data/` local al ejecutable) sin tocar el sistema anfitrión ni el registro.
  - Resolución de rutas relativas y auto-detección de herramientas portables (FFmpeg, 7-Zip, Python) ubicadas en `tools/`.
  - Script automatizado de distribución portable [`installer/build-portable.ps1`](file:///installer/build-portable.ps1) para generar paquetes `.zip` listos para usar en memorias USB o carpetas portables, integrado en [`.github/workflows/release.yml`](file:///.github/workflows/release.yml).
  - Externalización completa de muestras sintéticas, presets de renombrado, presets multimedia, catálogo de regex y plantillas de scripting a ficheros JSON independientes con fallback determinista.
- **Pipelines de CI/CD en GitHub Actions Actualizados ([`.github/workflows/release.yml`](file:///.github/workflows/release.yml) y [`.github/workflows/ci.yml`](file:///.github/workflows/ci.yml))**:
  - Integración de `build-portable.ps1` en el workflow de releases para generar los ZIPs portables oficiales con el marcador `portable.dat`, la estructura preconfigurada `data/` y la carpeta `docs/` con manuales en PDF.
  - Publicación como assets directos en cada GitHub Release del instalador Inno Setup (`.exe`), paquete portable (`.zip`), sumas de verificación SHA-256 (`checksums.txt`) y los 3 manuales en PDF (`Manual de Usuario`, `Guía para Principiantes`, `Manual de Scripting`).
- **Scripts de Limpieza Integral (`clean.ps1` y `clean.bat`)**:
  - Limpieza automatizada y determinista de todas las carpetas `bin` y `obj` en todos los proyectos, artefactos de publicación (`installer/publish`), empaquetado (`installer/output`), resultados de tests (`TestResults`, `coverage-report`) y temporales (`.vs`, `.dotnet_tmp`, `*.user`, `*.suo`, `crash.log`).
  - Soporte de simulación no destructiva (`-DryRun`), opción de incluir PDFs (`-IncludePdfs`) y cálculo de espacio liberado en disco.
- **Nuevo Plugin de Scripting Dinámico Dual (`FileFlow.Plugin.Scripting`)**:
  - Motor de ejecución dual que permite al usuario programar en **C# (Roslyn JIT en memoria con cacheo SHA256)** o **JavaScript (Sandbox administrado Jint en .NET 9)**.
  - Soporte de funciones de resolución de plantillas y variables implícitas (`Resolve(template)`, `resolve(template)` y `getVar(varName)`).
  - Nodo `CustomScriptNode` con puertos dinámicos configurables (`Inputs` y `Outputs` editables), timeouts y acción `OpenScriptStudio`.
  - Ventana de edición `ScriptStudioWindow` completamente integrada con el **sistema dinámico de temas** (`{DynamicResource ...}`) y **localización dinámica bilingüe i18n** (`LocalizationManager.Instance`), editor `AvalonEdit` temático, botón **`📖 Manual PDF...`**, probador en tiempo real con consola de salida y telemetría de emisiones, y biblioteca de scripts con presets incorporados (`.ffscript` en `%AppData%/FileFlow/Scripts/`).
- **Manual Didáctico de Usuario para Principiantes y Compilación a PDF**:
  - Creado [`docs/manual_usuario_principiantes.md`](file:///docs/manual_usuario_principiantes.md) redactado con lenguaje cercano, analogías cotidianas y 4 recetas prácticas paso a paso.
  - Compilado a [`docs/manual_usuario_principiantes.pdf`](file:///docs/manual_usuario_principiantes.pdf) (1053.1 KB) para su distribución en el instalador y la versión portable.
- **Manual Didáctico de Scripting, Compilación a PDF e Instalador**:
  - Creado `docs/manual_nodo_scripting.md` orientado a niveles básico y medio.
  - Compilado automáticamente a `docs/manual_nodo_scripting.pdf` (1003.8 KB) mediante Microsoft Edge Chromium Headless.
  - Integrado en `installer/FileFlow.iss` y `installer/publish.ps1` con acceso directo en el Menú de Inicio de Windows.
- **Actualización Total de Flujos de Ejemplo (Sin Código de Retrocompatibilidad)**:
  - Todos los 40 JSONs de ejemplo en `docs/examples/` (`01_basic`, `02_intermediate`, `03_advanced`, `04_complex`) y `docs/flujo_test.json` fueron actualizados a los nuevos contratos de nodos, tipos canónicos y nombres de puertos actuales (`True`/`False`, `Deleted`, `TriggerIn`, `ItemIn`/`ItemOut`, `Fork1`/`Fork2`/`AllCompleted`).
- **Encapsulación Total de UI en Plugins (Arquitectura Zero-Touch en FileFlow.App)**:
  - Cada plugin (`FileFlow.Plugin.*`) es un módulo 100% autónomo y auto-contenido con soporte WPF en .NET 9 (`net9.0-windows`).
  - Todas las ventanas modales, vistas XAML y servicios de soporte (`AdvancedRenamerEditorWindow` con paneles y vista previa redimensionables, 12 presets profesionales y 18 muestras sintéticas enriquecidas, `MediaPresetManagerWindow`, `PasswordManagerWindow`, `RegexHelperWindow`) residen dentro del directorio `UI/` de su respectivo plugin.
  - El SDK despacha las acciones modales de forma universal mediante `INodeCustomActionProvider` (`ExecuteCustomAction`), desacoplando por completo la aplicación anfitriona (`FileFlow.App`).
  - Para crear o modificar un plugin nuevo (con o sin interfaz gráfica), **solo se escribe código dentro del directorio del propio plugin, sin tocar `FileFlow.App`**.
- **Desacoplamiento Total de Vistas XAML y Sistema de CustomActions**:
  - `NodeActionDescriptor` en `FileFlow.Sdk` para que cada nodo declare sus herramientas modales y botones de acción avanzada (`IFlowNode.CustomActions`).
  - Erradicados todos los condicionales hardcodeados (`IsAdvancedRenamerNode`, `IsVariableInjectorNode`, `IsSwitchCaseNode`) de `NodeCardView.xaml` y `NodeInspectorPanelView.xaml`, reemplazándolos por un despachador genérico `ItemsControl ItemsSource="{Binding CustomActions}"`.
- **Arquitectura Híbrida de Plugins con Esquema Declarativo de Parámetros (Opción C)**:
  - `NodeParameterDescriptor` y `ParameterEditorType` en `FileFlow.Sdk` con soporte nativo `ParameterDescriptors` en `IFlowNode`.
  - Co-ubicación total: cada plugin (`FileFlow.Plugin.*`) declara el orden, tipos de editor (Slider, Dropdown, Toggle, FolderPath, FilePath), valores por defecto y opciones de sus nodos en su propio directorio.
  - `NodeParameterManager.cs` en `FileFlow.App` transformado en un motor de renderizado universal guiado por esquemas (*Schema-Driven UI*), eliminando el código acoplado y filtrando rigurosamente cualquier clave residual legada (`Pattern`, `NameTemplate`, `CaseTransformation`, `MethodSteps`).
- **Dimensiones por Defecto de ImageOptimizerNode**:
  - `Height` configurado en `"100%"` y `Width` en `""` (*Automático*) por defecto, garantizando la preservación completa del tamaño original y la relación de aspecto sin deformación.
- **Localización Dinámica y Reactiva al 100% en la Interfaz Gráfica**:
  - `NodeParameterViewModel.DisplayName`: Mapeo y traducción reactiva de los parámetros de los 27 nodos del sistema (`Width` $\rightarrow$ `Ancho` / `Width`, `Quality` $\rightarrow$ `Calidad` / `Quality`, `DestinationRoot` $\rightarrow$ `Carpeta Destino` / `Destination Folder`, etc.) manteniendo las claves técnicas de código en inglés.
  - `LocalizationManager.cs`: Notificación `OnPropertyChanged("Item[]")` y `OnPropertyChanged("Item")` para refrescar instantáneamente todos los bindings XAML en caliente sin reiniciar la aplicación.
  - Vistas XAML actualizadas (`ControlBarView`, `LogView`, `NodeInspectorPanelView`, `NodeToolboxView`) eliminando cadenas fijas.
  - Diccionarios completos de recursos en español e inglés (`Strings.resx` y `Strings.es.resx`).
  - Incorporada como **Regla de Diseño e Ingeniería Obligatoria** en `.agents/rules/rules.md`, `AGENTS.md`, `GEMINI.md` y `docs/architecture.md` (ADR-005).
- **Principio de Inmutabilidad del Archivo de Origen (*Source Immutability by Default*)**:
  - Incorporadas las reglas maestras de seguridad en `.agents/rules/rules.md`, `AGENTS.md`, `GEMINI.md` y `docs/architecture.md` (ADR-004).
  - Los flujos son no destructivos por defecto. Los archivos originales permanecen inmutables.
  - La alteración del archivo de origen queda centralizada en `OriginalFileActionNode` con soporte completo para `Keep`, `MoveToRecycleBin` (API Shell nativa), `MoveToQuarantine` y `PermanentDelete`.
  - `FileRelocatorNode` configurado con `Operation = "Copy"` por defecto.
- **Desacoplamiento de Renombrado Virtual en AdvancedRenamerNode y Destino Final**:
  - Incorporado el parámetro `RenameMode` (`Virtual` por defecto, o `DirectInPlace`).
  - En modo `Virtual`, `AdvancedRenamerNode` proyecta el nuevo nombre en `FileItemContext` sin alterar el archivo original en disco.
  - `DestinationSinkNode` y `FileRelocatorNode` leen de forma transparente desde `GetExistingPhysicalPath()` y copian/mueven el archivo con el nuevo nombre a la carpeta destino, preservando el archivo original intacto.
- **Rediseño Inteligente de ImageOptimizerNode**:
  - `Width` y `Height` situados en las dos primeras posiciones.
  - Parseo unificado de dimensiones: soporte automático de píxeles (`1920`, `800px`), porcentajes (`50%`) y auto-cálculo para preservar la relación de aspecto.
  - Eliminados los campos redundantes `SizeMode`, `ScalePercentage`, `ScalePercentageY` y `MaintainAspectRatio`.
- **Optimizaciones de Seguridad, Recursos y Concurrencia (.NET 10 / C# 13)**:
  - Disposición determinista de `archive?.Dispose()` en `SafeArchiveExtractor.cs`.
  - Configuración de `SocketsHttpHandler` con `PooledConnectionLifetime` en `WebhookNotificationNode.cs`.
  - Despacho asíncrono no bloqueante con `Dispatcher.InvokeAsync` en `NodeViewModel.cs`.
  - Gestión segura de memoria no administrada para doble null en `SafeRecycleDeleteNode.cs`.
  - Eliminación de antipatrones `.Result` en `CliExecutionNode.cs`.
  - Captura defensiva de `IOException` en `FolderSourceNode.cs`.
  - Simplificación de tareas y delegados en `AdvancedRenamerNode.cs`.
  - Guarda `HasShutdownStarted` en `FastObservableRingBuffer.cs`.
  - Drenaje determinista y agregación de excepciones en `WorkflowExecutor.cs`.
  - Protección de rutas idénticas y *Safe Move* con validación SHA-256 en `FileRelocatorNode.cs`.
  - Tipo de operación `DeletedPermanently` en `EmptyDirectoryCleanerNode.cs` y enum `JournalOperationType`.
  - Resiliencia ante Regex inválidas en `SearchReplaceStepHandler.cs` y `NormalizeNumbersStepHandler.cs`.
  - Caché concurrente en memoria `ConcurrentDictionary` para ejecutables externos en `ExternalToolsService.cs`.
  - Soporte completo de simulación `DryRun` y `PlannedOperationType.TransformMedia` en `ImageOptimizerNode.cs`.
  - Suite especializada de tests `SecurityAndRobustnessAuditTests.cs` (7 tests).
- **Refactorización Modular Clean Code (SRP & OCP) - 10 Módulos Desacoplados**:
  - `RenameTransformEngine.cs` (Sdk): Reducido a 124L con 9 Strategy Handlers en `FileFlow.Sdk/Renaming/Handlers/`.
  - `CustomThemeService.cs` (App): Reducido a 140L delegando en `BuiltInThemesCatalog.cs` y `ThemeResourceApplier.cs`.
  - `ControlBarViewModel.cs` (App): Reducido a 463L delegando en `WorkflowExecutionCoordinator.cs` y `AppResourceLocator.cs`.
  - `AdvancedRenamerEditorViewModel.cs` (App): Reducido a 390L delegando en `RenamerTagCatalogService.cs`, `RenamerSampleDataProvider.cs` y `RenamerLivePreviewService.cs`.
  - `WorkflowExecutor.cs` (Core): Reducido a 468L delegando en `WorkflowTelemetryTracker.cs`.
  - `SqliteLogStore.cs` (Core): Reducido a 389L delegando en `SqliteLogSchema.cs` y `SqliteLogMetricsReader.cs`.
  - `SmartUnpackNode.cs` (Archives): Reducido a 157L delegando en `SafeArchiveExtractor.cs`.
  - `SystemVariablesResolver.cs` (Sdk): Reducido a 198L delegando en `DomainVariableResolver.cs` y `PathRelativeCalculator.cs`.
  - `EditorViewModel.cs` (App): Reducido a 417L delegando en `EditorViewportCalculator.cs` y `WorkflowGraphSerializer.cs`.
  - `NodeViewModel.cs` (App): Reducido a 371L delegando en `NodeCategoryStyling.cs` y `NodeSwitchCaseCoordinator.cs`.
- **Pantalla de Carga Fluida (SplashScreen)**:
  - `SplashScreenWindow.xaml` con estética Dark Glow, bordes redondeados (`CornerRadius="16"`), resplandor violeta/índigo, barra de progreso multicolor y reporte reactivo de etapas de arranque con animaciones de Fade-In y Fade-Out.
- **Instalador y Empaquetado**: Incluye la suite completa de 40 ejemplos de flujos (`Examples/` organizados en 4 niveles) y el **Manual de Usuario en Formato PDF** (`Docs/manual_de_usuario.pdf`), generado automáticamente durante la publicación para el instalador Inno Setup, el paquete portable ZIP y las GitHub Releases, con accesos directos en el menú de inicio y botón en el menú drawer de la app.
- **Throughput de Telemetría**: **>82.000 logs/segundo** en 28 núcleos en paralelo con SQLite In-Memory.

- **Capacidades Avanzadas en ImageOptimizerNode**:
  - Redimensionamiento proporcional especificando solo ancho o solo alto (`MaintainAspectRatio = true`).
  - Redimensionamiento por porcentaje (`SizeMode = "Percentage"` con `ScalePercentage` y `ScalePercentageY`).
  - Control de escalado hacia arriba (`OnlyDownscale = true`) para no agrandar ni pixelar imágenes más pequeñas que el tamaño objetivo.
  - Normalización en `NodeParameterManager` (migración transparente de `MaxWidth`/`MaxHeight`) y soporte completo en la UI de la app (`SizeMode` dropdown, `CheckBox` para booleanos).
- **Manual de Usuario en PDF**:
  - Script [`installer/build-pdf-manual.ps1`](file:///installer/build-pdf-manual.ps1) para compilar `docs/manual_de_usuario.md` a `docs/manual_de_usuario.pdf` con diseño tipográfico y estilos A4 mediante Chromium/Edge headless.
  - Integración en [`publish.ps1`](file:///installer/publish.ps1), [`build-installer.ps1`](file:///installer/build-installer.ps1), [`FileFlow.iss`](file:///installer/FileFlow.iss), [`ControlBarViewModel.cs`](file:///FileFlow.App/ViewModels/ControlBarViewModel.cs) y [`.github/workflows/release.yml`](file:///.github/workflows/release.yml).
- **Sistema de Versiones SemVer 2.0 con Auto-Incremento de Build**:
  - Archivo de configuración central [`version.props`](file:///version.props) con versión base establecida en **`1.0.0-beta`** (`VersionMajor=1`, `VersionMinor=0`, `VersionPatch=0`, `VersionPreRelease=beta`).
  - Tarea MSBuild en [`Directory.Build.props`](file:///Directory.Build.props) para auto-incrementar de forma segura el contador en [`.build_number`](file:///.build_number) en cada compilación (`1.0.0-beta+build.N`).
  - [`AppVersionInfo`](file:///FileFlow.Sdk/AppVersionInfo.cs) y visualización en el pie del menú Drawer lateral de [`MainWindow.xaml`](file:///FileFlow.App/MainWindow.xaml).
  - Auto-detección en [`.github/workflows/release.yml`](file:///.github/workflows/release.yml) para crear etiquetas y lanzamientos GitHub Release automáticamente con la versión SemVer + Build actual.
- **Estudio de Personalización Visual de Temas (Theme Studio)**:
  - `ThemeDefinition` en `FileFlow.Sdk/Themes/` y `CustomThemeService` en `FileFlow.App/Services/` con 8 presets de fábrica (*Oscuro Fluent*, *Claro Minimalista*, *Cyber Neón*, *Primavera Pastel*, *Midnight OLED*, *Nord Slate*, *Dracula Purple*, *Emerald Forest*) y persistencia de temas de usuario en `%APPDATA%\FileFlow\custom_themes.json`.
  - `ThemeCustomizerWindow`, `ThemeCustomizerViewModel` y `ColorPickerButton` con edición interactiva de colores, tipografías, tamaños, radios de esquina, sombras y gradiente del cable conector, con vista previa reactiva en tiempo real sobre componentes de nodo, botones y tablas.
  - **Selectores Dinámicos en la App**: Los menús desplegables del Drawer lateral (`MainWindow.xaml`) y del diálogo de Ajustes (`WorkflowSettingsWindow.xaml`) se pueblan automáticamente en tiempo real con todos los temas de fábrica y todos los temas personalizados creados o importados por el usuario.
- **Asistente y Probador Visual de Expresiones Regulares (Regex Studio)**:
  - `RegexPatternItem` en `FileFlow.Sdk/Renaming/` y `RegexLibraryService` en `FileFlow.App/Services/` con biblioteca predefinida de presets y persistencia de patrones de usuario en JSON (`%APPDATA%\FileFlow\regex_library.json`).
  - `RegexHelperWindow` y `RegexHelperViewModel` con probador en tiempo real, validación sintáctica segura, inspección de grupos de captura (`$1`, `$2`), flags y simulación de reemplazo en vivo con soporte de funciones de plantilla y variables.
- **Motor de Renombrado Avanzado (9 Métodos Acumulativos)**:
  - Métodos: *1. Nuevos Nombres*, *2. Búsqueda y Reemplazo*, *3. Inserción*, *4. Eliminación*, *5. Mayúsculas*, *6. Numeración*, *7. Tabla de Sustituciones*, *8. Limpieza/Normalización*, *9. Normalizar Números (01, 02...)*.
  - Soporte integral de variables inyectadas aguas arriba, variables de sistema, funciones de plantilla y grupos de captura regex.
  - Carga automática de hasta 100 archivos reales desde `FolderSourceNode` para previsualización en vivo sobre el dataset del usuario.
- **Documentación y CI/CD**: Documentación actualizada en [`docs/manual_de_usuario.md`](file:///docs/manual_de_usuario.md), [`docs/PROJECT_WALKTHROUGH.md`](file:///docs/PROJECT_WALKTHROUGH.md) y pipelines GitHub Actions.
- **Git**: Repositorio limpio y sincronizado con batería de pruebas al 100%.

---

## 2. Capa de Telemetría Atómica, Silenciado Selectivo y Observabilidad en 24 Nodos (Agosto 2026)
1. **Auditoría y Estandarización de Observabilidad en los 24 Nodos de Producción**:
   - Estandarización de `context.Log` en todos los plugins (`Logic`, `FileSystem`, `Archives`, `Images`, `Hashing`, `Integrations`).
   - Métricas de tiempo de ejecución con `Stopwatch` (`durationMs`), identificadores y nombres de archivo no nulos auto-vinculados, y payloads estructurados JSON (`detailsJson`).
   - Niveles disciplinados: `Debug` para trazas internas de alta frecuencia y desvíos rutinarios, `Information` para hitos de negocio enriquecidos con métricas, `Warning` y `Error` con serialización estructurada de causas y rutas.
2. **Botón de Toggle de Emisión de Logs por Nodo (Estilo Breakpoint)**:
   - `NodeCardView.xaml` incorpora un botón interactivo en la cabecera junto al breakpoint.
   - Indicador visual (`≡`): cian brillante (`#06B6D4`) encendido (emite logs) y gris atenuado (`#475569`) apagado (silenciado).
   - Menú contextual y ToolTips reactivos: *"Logs: Habilitados (clic para silenciar)"* / *"Logs: Silenciados (clic para activar)"*.
3. **Supresión en Motor de Ejecución (`WorkflowExecutor.cs`)**:
   - Nodos silenciados descartan de inmediato sus logs en $O(1)$ sin generar objetos ni saturar la base de datos SQLite.
4. **Memoización en `FileItemContext.cs` (Zero-Alloc Hot Paths)**:
   - Cacheo interno e inmutable de `IdString` y `ShortIdString`.
   - Propiedad `FileName` reactiva a mutaciones en `CurrentPath`.
5. **Formateo Zero-Boxing en `StructuredLogRecord.cs`**:
   - `FormattedFileSize` optimizado con formateo numérico directo en lugar de `FormattableString.Invariant`.
6. **Reutilización de Conexión y Transacciones Masivas en `SqliteLogStore.cs`**:
   - `InsertBatchAsync` reutiliza `_keepAliveConnection` protegida bajo `_flushLock`.
7. **Consola Rediseñada con Alineación Vertical Perfecta y Toolbar Compacta (`LogView.xaml` / `LogViewModel.cs`)**:
   - Barra superior unificada con contadores en tiempo real (`Errores`, `Warn`, `Info`, `Debug`, `Todos`), input de búsqueda reactivo con botón de limpieza (`✕`), contador total de logs en BD y controles de depuración (`⚡ En Vivo`, `💾 Exportar`, `🗑 Limpiar`).
   - Celdas estandarizadas a `RowHeight="24"` con `VerticalContentAlignment="Center"`.
   - Pill badges de severidad con fondo translúcido y texto coloreado (`LogLevelToBadgeBackgroundConverter`, `LogLevelToBadgeForegroundConverter`).
   - Trazabilidad sin interrupciones: resuelto el listener de scroll que interfería al filtrar por archivo.
8. **Refactorización Modular (Clean Code & SRP)**:
   - `WorkflowExecutionContext.cs` extraído a archivo independiente.
   - `SqliteLogQueryBuilder.cs` encapsula la construcción de SQL parametrizado.
   - `ValueConverters.cs` dividido en `BooleanConverters.cs`, `TelemetryConverters.cs` y `GraphConverters.cs`.
9. **Auditoría de Seguridad y Depuración de Errores**:
   - Mitigación estricta de Zip Slip en `SmartUnpackNode.cs` con separador final.
   - Eliminación de borrado destructivo permanente en `SafeRecycleDeleteNode.cs` y soporte x64 en P/Invoke.
   - Medición segura con `Stopwatch` en `CliExecutionNode.cs`.
   - Invocación segura en UI Dispatcher de `FastObservableRingBuffer.cs`.
   - Limpieza determinista de tareas en `FolderWatcherService.cs` y drenaje en `SqliteLogStore.cs`.
10. **Batería de Testing Exhaustivo**:
   - 35 nuevos tests unitarios y de integración para `FileItemContext`, `SystemVariablesResolver`, `AdvancedRenamerNode`, `CliExecutionNode`, `SafeRecycleDeleteNode`, `SqliteLogQueryBuilder`, `ValueConverters` y `WorkflowExecutor`.
   - Suite total: **181 / 181 pruebas superadas con 100% de éxito (0 errores, 0 fallos)** en 3s.

---

## 3. Suite Completa de Documentación y Directrices (`docs/` & `AGENTS.md`)
- `AGENTS.md`: Guía maestra universal para cualquier agente de IA (Antigravity, Cursor, Claude Code, Copilot, etc.) con protocolo de arranque, mapa de archivos auxiliares y estándares .NET 9.
- `docs/architecture.md`, `docs/setup_and_deployment.md`, `docs/api_reference.md`, `docs/user_guide.md`, `docs/contributing.md`, `docs/README.md`.

---

## 4. Auditoría Integral 360° y Refactorización Ejecutada (Agosto 2026)
Se completó la **Fase 1 (Auditoría 360°)** y la **Fase 2 (Refactorización)** corrigiendo el 100% de los 16 hallazgos detectados en 6 Sprints atómicos:

### Resumen de Mejoras Aplicadas y Validadas:
- **Sprint 1 — Fugas de Recursos y Concurrencia (Crítico)**:
  - `AUD-01`: `_concurrencyThrottle.Dispose()` al cambiar `MaxDegreeOfParallelism`.
  - `AUD-02`: Reemplazado `ConcurrentBag<Task>` por `List<Task>` sincronizado con `Lock` y drenaje de completados.
  - `AUD-10`: Disposición asíncrona de `SqliteLogStore.Instance` en `App.OnExit`.
- **Sprint 2 — Memory Leaks de Event Handlers en UI**:
  - `AUD-05`: Invocación implícita de `Dispose()` en `NodeViewModel` en `EditorViewModel.ClearGraph()` y `LoadFromGraphModel()`.
  - `AUD-06`: Desenganche de `PropertyChanged` mediante handler nominal `OnNodePropertyChanged`.
  - `AUD-11`: `IDisposable` en `ControlBarViewModel` para desuscribir `PreferencesChanged`.
- **Sprint 3 — Rendimiento UI**:
  - `AUD-04`: Migrado `LogViewModel.Logs` a `FastObservableRingBuffer` eliminando `RemoveAt(0)` O(n).
  - `AUD-14`: Indexado `UpdateEdgeDispatched` con `Dictionary` O(1).
- **Sprint 4 — Calidad de Código y Convenciones .NET 9**:
  - `AUD-07`: Migrado `object _lock` a `System.Threading.Lock` en `UserPreferencesService`, `MediaPresetManagerService` y `ExternalToolsService`.
  - `AUD-09`: `await cmd.ExecuteNonQueryAsync()` en `InsertBatchAsync` de `SqliteLogStore.cs`.
  - `AUD-08`: Logging contextual en bloques `catch` de `PluginLoader`.
- **Sprint 5 — Robustez y Resiliencia**:
  - `AUD-03`: `Volatile.Read` en `WaitIfPausedAsync`.
  - `AUD-15`: Crash log persistente (`crash.log`) y handler `TaskScheduler.UnobservedTaskException` en `App.xaml.cs`.
- **Sprint 6 — Tests de Cobertura Crítica**:
  - `AUD-16`: Suite `WorkflowExecutorTests.cs` ampliada con paralelismo, pausa/resume, DryRun, errores y cancelación.

---

## 5. Reglas de Mantenimiento Memorizadas
1. **Consulta al Inicio de Sesión**: Consultar siempre `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y `.antigravity/knowledge/repo_architecture.md` antes de escanear archivos.
2. **Actualización Continua**: Mantener actualizados `docs/PROJECT_WALKTHROUGH.md` (por fechas), `.antigravity/knowledge/session_summary.md` y los artefactos de plan ante cualquier modificación de código.
3. **Repositorio Git**: Garantizar que el repositorio Git permanezca limpio, probado y sincronizado.

