# Resumen Consolidado de Sesiones y Memoria de Proyecto - FileFlow Studio

Este documento se actualiza al finalizar cada sesión de trabajo para consolidar los puntos clave, decisiones arquitectónicas, capacidades del sistema y el estado de la solución, evitando empezar desde cero en futuras conversaciones.

---

## 1. Estado Actual del Repositorio y Calidad
- **Target Framework**: `.NET 9` (`net9.0` / `net9.0-windows` para WPF UI) con preparación para .NET 10.
- **Lenguaje**: `C# 13` (`<LangVersion>13</LangVersion>`), Nullable activado de forma estricta.
- **Estado de Compilación**: `dotnet build FileFlow.slnx --warnaserror` $\rightarrow$ **0 Advertencias, 0 Errores**.
- **Suite de Pruebas**: `.\test.ps1` / `dotnet test` $\rightarrow$ **510 / 510 Pruebas Pasadas con 100% de Éxito**.
- **Nuevas Funcionalidades y Correcciones Implementadas en Sesión**:
  --28. **Vaciado Atómico y Determinista de Logs con Eliminación de Condiciones de Carrera (`LogViewModel`)**:
      - **Objetivo**: Corregir de forma definitiva la condición de carrera intermitente al pulsar "Limpiar logs", donde en ocasiones se borraban todos los logs y en otras volvían a aparecer registros previos requiriendo un segundo clic.
      - **Ajustes Realizados**:
        1. **Diagnóstico**: Al resetear filtros (`ActiveFilter`, `SearchFilter`, `IsLiveMode`), las propiedades generadas por CommunityToolkit MVVM disparaban en background `On...Changed` que ejecutaban `GetLogsWindowAsync` concurrentemente con `SqliteLogStore.ClearAsync()`.
        2. **Barrera de Sincronización**: Introducido el flag `private volatile bool _isClearingLogs` en `LogViewModel.cs`.
        3. **Supresión Preventiva**: Bloqueo de consultas asíncronas concurrentes en `OnActiveFilterChanged`, `OnSearchFilterChanged`, `OnIsLiveModeChanged`, `LoadRecentLiveLogsAsync`, `LoadQueryResultsAsync`, `FlushPendingLogs`, `SortBy`, `SetFilter`, `ClearSearchFilter` y `FilterByItem` mientras `_isClearingLogs` esté activo.
        4. **Vaciado Secuencial**: `_pendingLogs.Clear()` $\rightarrow$ `await SqliteLogStore.Instance.ClearAsync()` $\rightarrow$ Reset de UI (`Logs.Clear()`, contadores a 0, selección nula) $\rightarrow$ `_isClearingLogs = false` en `finally`.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 errores, 0 advertencias) y 510 / 510 pruebas superadas (100%).
  --27. **Internacionalización Dinámica de Mensajes de Log y Telemetría de Ejecución con Propagación de Cultura**:
      - **Objetivo**: Garantizar que todos los mensajes de log de ejecución producidos por la interfaz gráfica, el orquestador (`WorkflowExecutionCoordinator`), el motor DAG (`WorkflowExecutor`, `WorkflowItemDispatcher`), los ViewModels (`ControlBarViewModel`, `LogViewModel`, `MainViewModel`) y los nodos de plugins (`FileFlow.Plugin.FileSystem`) se emitan de forma reactiva en el idioma configurado dinámicamente en la aplicación (`LocalizationManager.Instance`), propagando de manera estricta la cultura activa a todos los hilos del ThreadPool.
      - **Ajustes Realizados**:
        1. **Propagación Automática de Cultura (`LocalizationManager.cs`)**: El constructor y el setter de `CurrentCulture` configuran `CultureInfo.DefaultThreadCurrentCulture` y `CultureInfo.DefaultThreadCurrentUICulture`, asegurando que `Task.Run`, hilos de fondo y canales hereden la cultura activa sin desfases.
        2. **Helpers de Localización Formateada (`FileFlow.Sdk`)**: Incorporación de `LocalizationManager.GetFormattedString(key, fallbackTemplate, params args)` y helpers protegidos `GetLocalizedString` / `GetLocalizedFormat` en `FlowNodeBase`.
        3. **Localización de Orquestador y ViewModels (`FileFlow.App`)**: Traducidos los logs de inicio de ejecución (modo normal, depuración, watch mode, simulación dry run), control de flujo (vigilancia, reset de checkpoints, rollback, cancelación, plantillas), inicialización de la app y exportación de logs.
        4. **Localización del Motor de Ejecución (`FileFlow.Core`)**: Mensajes de progreso de drenaje de colas, resumen de ítems completados/fallidos, duración total, salto de checkpoints y métricas de despacho.
        5. **Localización de Nodos Autónomos (`FileFlow.Plugin.FileSystem`)**: Traducidos los logs de ejecución, simulación y errores en los 11 nodos del plugin (`FolderSourceNode`, `DestinationSinkNode`, `AdvancedRenamerNode`, `FileRelocatorNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`, `DirectoryInspectorNode`, `EmptyDirectoryCleanerNode`, `DocumentProcessorNode`, `VariableInjectorNode`, `OperationReportNode`).
        6. **Diccionarios de Recursos Multilingües**: Añadidas 22 claves de host en `FileFlow.App/Resources/Strings.*.resx` y 48 claves de plugin en `FileFlow.Plugin.FileSystem/Resources/Strings.*.resx` (Español e Inglés).
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y 510 / 510 pruebas unitarias superadas al 100%.
  --26. **Soporte Dinámico de Temas y Localización Completa (i18n) en el Panel Centralizado de Métricas y Pestaña de Rendimiento del Inspector**:
      - **Objetivo**: Garantizar que la ventana de métricas y profiling de flujo (`WorkflowMetricsDashboardWindow.xaml`) y la pestaña 6 del inspector de nodos (`NodeInspectorPanelView.xaml`) se adapten dinámicamente al tema visual activo de la aplicación (`ThemeManager.Instance`) y traduzcan todos sus textos de manera reactiva según el idioma seleccionado (`LocalizationManager.Instance`), erradicando colores oscuros hexadecimales hardcodeados y cadenas literales fijas.
      - **Ajustes Realizados**:
        1. **Migración a Pinceles Dinámicos (`DynamicResource`) en `WorkflowMetricsDashboardWindow.xaml`**: Sustitución de colores hexadecimales fijos (`#0B0F19`, `#131D31`, `#1E293B`, `#243048`, `#0D1322`, `#121A2C`, `#172238`, `#F8FAFC`, `#94A3B8`, `#64748B`, etc.) por `{DynamicResource BgDarkBrush}`, `BgHeaderBrush`, `BgCardBrush`, `BgSurfaceBrush`, `BorderDarkBrush`, `TextPrimaryBrush`, `TextSecondaryBrush`, `AccentCyanBrush`, `AccentPurpleBrush`, `AccentSuccessBrush` y `AccentErrorBrush`.
        2. **Localización e Internacionalización Completa (i18n)**:
           - Conexión de títulos, tooltips, etiquetas de KPIs ("📦 Invocaciones", "⏱️ Tiempo Total", "⚡ Latencia Media", "💾 RAM Estimada", "🎮 Ops GPU", "⚠️ Cuellos Botella", "> 25% tiempo flujo"), cabeceras de distribución ("⏱️ Distribución de Tiempo (%)", "💾 Asignación de RAM (%)"), ranking comparativo, buscador y columnas de datos del DataGrid a `LocalizationManager.Instance`.
           - Conexión de todas las métricas en la Pestaña 6 del inspector (`NodeInspectorPanelView.xaml`): latencia media, RAM/item, CPU & Hardware, elementos procesados, errores, alerta de cuello de botella, historial rodante y botón de restablecimiento de métricas.
           - Adición y sincronización de recursos bilingües en `FileFlow.App/Resources/Strings.resx` y `Strings.es.resx`.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y 510 / 510 pruebas superadas al 100%.
  --25. **Localización Completa de Interfaz (i18n) y Erradicación de Cadenas Hardcodeadas**:
      - **Objetivo**: Conectar el 100% de los textos de la interfaz gráfica y cuadros de diálogo al sistema dinámico de localización (`LocalizationManager.Instance`), soportando Español (`es-ES`) e Inglés (`en-US`) sin reiniciar la aplicación, cumpliendo estrictamente con la **Regla 5** (i18n reactivo en UI) y la **Regla 6** (co-ubicación de recursos en plugins).
      - **Ajustes Realizados**:
        1. **Diccionarios de Recursos (`Strings.resx` / `Strings.es.resx`)**: Incorporadas más de 45 nuevas claves para métricas, telemetría de hardware (RAM/CPU/GPU), personalizador de temas, editor de texto modal, previsualizador de archivos y comparador de imágenes, inspector de nodos y mensajes de error en ViewModels.
        2. **Vistas XAML de Host (`FileFlow.App`)**: Localizadas todas las etiquetas, títulos, botones y tooltips en `WorkflowMetricsDashboardWindow`, `ThemeCustomizerWindow`, `TextEditorDialogWindow`, `FilePreviewerWindow`, `ImageCompareSliderControl`, `StatusBarView`, `ControlBarView`, `AnnotationCardView`, `AiModelDownloadDialog`, `NodeParameterTemplates` e `InspectorTemplates`.
        3. **Plugins Autónomos (`FileFlow.Plugin.FileSystem`)**: Conexión de recursos propios en `AdvancedRenamerEditorWindow.xaml` con co-ubicación en `FileFlow.Plugin.FileSystem/Resources/Strings.resx` y `Strings.es.resx`.
        4. **C# ViewModels**: Localizados los cuadros de diálogo `MessageBox.Show` en `LogViewModel`, `StatusBarViewModel`, `NodeParameterViewModel`, `EditorViewModel`, `ControlBarViewModel` y `WorkflowMetricsDashboardViewModel`.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y 510 / 510 pruebas superadas al 100%.
  --24. **Acciones Masivas en Selección Múltiple de Nodos (Color, Breakpoints, Logs, Copiar, Cortar, Duplicar y Borrar) y Portapapeles DAG con Parámetros**:
      - **Objetivo**: Permitir que al seleccionar múltiples nodos y usar el menú contextual o las acciones rápidas, el cambio de color, activación/desactivación de puntos de interrupción (`Breakpoint`) y alternancia de logs afecten de manera uniforme y sincronizada a todos los nodos seleccionados, además de soportar copiar, cortar, pegar y duplicar con conservación de parámetros.
      - **Ajustes Realizados**:
        1. **Acciones Masivas Reactivas (`NodeViewModel.cs`)**: `GetTargetNodesForBatchAction()` detecta si el nodo pertenece a una selección múltiple activa. `ChangeColor`, `ChooseCustomColor`, `ToggleBreakpoint` y `ToggleLogging` se aplican en lote a todos los nodos seleccionados.
        2. **Servicio Centralizado (`INodeClipboardService` / `NodeClipboardService`)**: Serialización en `NodeClipboardPackage`, soporte de portapapeles del sistema operativo (`Clipboard.SetText` / `Clipboard.GetText`) con caché en memoria como fallback de alta fiabilidad, deserialización polimórfica con `UnwrapJsonValue`, regeneración de IDs (`Guid`) y remapeo de aristas internas.
        3. **Editor MVVM (`EditorViewModel.cs`)**: Helper `ResolveTargetNodes` unificado para `DeleteSelectedNodesCommand`, `CopySelectedNodesCommand`, `CutSelectedNodesCommand` y `DuplicateSelectedNodesCommand`. Soporte de pegado en coordenadas explícitas (`targetLocation`).
        4. **Vistas e Interfaz (`NodeCardView.xaml`, `EditorView.xaml`, `MainWindow.xaml`)**: Menús contextuales en tarjeta de nodo y en lienzo, atajos de teclado globales y en canvas para `Ctrl+C`, `Ctrl+X`, `Ctrl+V`, `Ctrl+D` y `Delete`.
        5. **Localización e Internacionalización (i18n)**: Claves multilingües añadidas en `Strings.resx` y `Strings.es.resx`.
        6. **Suite de Pruebas (`NodeClipboardServiceTests.cs` & `EditorViewModelTests.cs`)**: 11 pruebas unitarias nuevas probando copia de parámetros, duplicación con offsets, regeneración de GUIDs, preservación de aristas internas y acciones masivas de color, breakpoint y logs.
      - **Validación**: 510 / 510 pruebas unitarias e integración superadas al 100%.
  --23. **Optimización de Rendimiento al Límite Técnico: Enrutamiento DAG Zero-Allocation, Vectorización SIMD en Tensores IA, Caching de Pasos en Renombrador Masivo, I/O Asíncrono en Sinks y Throttle Lock-Free de UI**:
      - **Objetivo**: Maximizar el throughput de procesamiento por segundo, paralelizar cargas de trabajo eficientemente sin contención y eliminar asignaciones de memoria redundantes en los hot paths críticos del motor.
      - **Ajustes Realizados**:
        1. **Enrutamiento DAG Zero-Allocation (`WorkflowExecutor.cs` / `WorkflowItemDispatcher.cs`)**: Precomputación de tabla de aristas indexadas `_indexedPortEdges` (`(NodeId, Port) -> WorkflowEdge[]`) que elimina las asignaciones de listas LINQ (`edges.Where(...).ToList()`) en cada emisión de archivo, convirtiendo el enrutamiento en un lookup $O(1)$ directo.
        2. **Vectorización SIMD y Planar Slicing en Tensores IA (`TensorPreprocessors.cs`)**: Relleno de tensores mediante `Span<float>.Fill(padNorm)` en una sola instrucción vectorizada, acceso contiguo a canales de color (`channelR`, `channelG`, `channelB`) sin cálculo de strides 4D por píxel, y reescritura de `Softmax` eliminando asignaciones de LINQ.
        3. **Clonado Optimizado de Contexto (`FileItemContext.DeepClone`)**: Uso de constructores de copia directos de diccionarios y conjuntos con inicialización perezosa condicional.
        4. **Caching de Pasos y Transformación sin Trazas en Renombrador Masivo (`AdvancedRenamerNode.cs` / `RenameTransformEngine.cs`)**: Almacenamiento en caché de la lista deserializada de pasos en `Parameters["MethodSteps"]` para evitar miles de llamadas a `JsonSerializer.Deserialize` por ítem. Soporte del parámetro `recordTraces: false` en `IRenameTransformEngine.Transform` para suprimir la asignación de listas y trazas en lotes de alta producción.
        5. **I/O Asíncrono no Bloqueante en Sink de Archivos (`DestinationSinkNode.cs`)**: Uso de `FileStreamOptions` con `FileOptions.Asynchronous | FileOptions.SequentialScan` y buffers de 128 KB en streams asíncronos para archivos mayores a 256 KB, liberando los hilos del ThreadPool de I/O síncrono bloqueante.
        6. **Throttle Lock-Free Atómico en UI (`WorkflowExecutor.cs`)**: Notificaciones de progreso limitadas atómicamente a ~28 FPS mediante `Interlocked.CompareExchange` con ventana de 35 ms, previniendo la saturación del Dispatcher de UI.
        7. **Actualizaciones Atómicas en Colecciones de Toolbox (`ToolboxViewModel.cs`)**: Buffer local de grupos `targetGroups` con confirmación atómica `CommitGroups`, previniendo estados de colección vacía transitorios y condiciones de carrera en ejecuciones paralelas de pruebas.
        8. **Suite de Benchmarks Formales (`PerformanceBenchmarkSuiteTests.cs`)**: Nuevo benchmark `Benchmark_TensorPreprocessors_SpanSimdVectorizationPerformance`.
      - **Validación**: 499 / 499 pruebas unitarias e integración superadas al 100% (0 errores, 0 advertencias con `--warnaserror`).
  --22. **Rediseño Visual Moderno (Glassmorphism): Dashboard de Métricas, Tarjetas de Nodo con Resplandor Reactivo y Barra de Estado Modular**:
      - **Objetivo**: Elevar el atractivo visual y la experiencia de usuario en la pantalla de estadísticas de rendimiento (`WorkflowMetricsDashboardWindow.xaml`), las tarjetas de nodo (`NodeCardView.xaml`) y la barra de estado (`StatusBarView.xaml`) con estética Glassmorphism, KPIs con micro-acentos de 2px, DataGrid interactivo con buscador instantáneo, píldoras de categoría semánticas, barras de progreso redondeadas con degradados personalizados y números en tipografía monospace.
      - **Ajustes Realizados**:
        1. **Dashboard (`WorkflowMetricsDashboardWindow.xaml` / `WorkflowMetricsDashboardViewModel.cs`)**: Paleta oscura Glassmorphism (`#0B0F19`), KPIs con bordes superiores luminosos multietapa, barra de búsqueda en tiempo real (`SearchFilter`), píldoras de categoría con colores e iconos de dominio, barras de progreso personalizadas para distribución de tiempo (`#38BDF8` $\rightarrow$ `#6366F1`) y RAM (`#A855F7` $\rightarrow$ `#EC4899`), visualización inline de cuellos de botella y enlaces `Mode=OneWay` para evitar excepciones en `ProgressBar.Value`.
        2. **Tarjetas de Nodo (`NodeCardView.xaml`)**: Resplandor exterior verde (`#10B981` con `BlurRadius="12"`) cuando el nodo está en estado `Running`, y micro-badges de telemetría con tipografía monospace (`Consolas, Segoe UI`) y fondos refinados.
        3. **Barra de Estado (`StatusBarView.xaml`)**: Islas flotantes independientes (`#111827`, borde `#1F2937`), telemetría de hardware agrupada y píldora de modelos IA con botón de purga rápida.
      - **Validación**: 498 / 498 pruebas unitarias e integración superadas al 100% (0 errores, 0 advertencias con `--warnaserror`).
  --21. **Telemetría Reactiva de Drenaje de Cola de Tareas y Progreso en Vivo de Inferencia Final**:
      - **Problema**: Al finalizar la emisión de archivos desde el nodo de origen, el flujo parecía "congelado" durante los últimos 20-30 segundos mientras los últimos elementos de la cola se procesaban secuencialmente a través de los nodos de inferencia de IA sin actualizar la interfaz de usuario.
      - **Solución**:
        1. **`WorkflowTaskTracker.cs`**: `DrainActiveTasksAsync` ahora utiliza `Task.WhenAny(pending)` y recibe un `progressCallback(remaining)` que notifica de forma reactiva cada vez que cualquier tarea individual finaliza en la cola.
        2. **`WorkflowExecutor.cs`**: Enlaza el callback de tareas restantes con `NotifyProgress` para reflejar en vivo el número de elementos restantes en la cola (`⚡ Finalizando cola de tareas: N restante(s)`).
        3. **`WorkflowItemDispatcher.cs`**: Notifica el estado dinámico y nombre del archivo activo en el momento en que cualquier nodo intermedio comienza a procesarlo (`⚡ [Nombre Nodo]: [Nombre Archivo]`).
      - **Validación**: 498 / 498 pruebas unitarias e integración superadas al 100%.
  --20. **Corrección de Detección de GPU (DirectML) en Telemetría y Sincronización Reactiva de Modelos IA en Barra de Estado**:
      - **Problema**: A pesar de que los logs del nodo eliminador de fondos (`BackgroundRemoverNode`) indicaban el uso de DirectML en GPU, el badge `[🎮 GPU]` no aparecía en las tarjetas ni en las estadísticas del dashboard. Además, el contador de modelos de IA cargados (`[🟢 N cargados]`) no se sincronizaba reactivamente en la barra de estado inferior.
      - **Solución**:
        1. **Contrato `IModelLifecycleNode` en SDK**: Añadida la propiedad `bool IsGpuAccelerated => false;`.
        2. **Nodos de IA (`FileFlow.Plugin.AI`)**: Implementada `IsGpuAccelerated` en `AiFlowNodeBase`, `BackgroundRemoverNode`, `SuperResolutionUpscalerNode`, `ObjectDetectorNode`, `SmartImageClassifierNode`, `PromptObjectDetectorNode` y `ContentModerationFilterNode`. Inyección explícita de `AI:DirectMlAccelerated = true` y `AI:Device = "GPU (DirectML)"` en los metadatos de los ítems de salida.
        3. **Despachador del Motor (`WorkflowItemDispatcher.cs`)**: La detección de GPU ahora verifica metadatos del ítem e inspecciona directamente `lifecycleNode.IsGpuAccelerated`.
        4. **Barra de Estado (`StatusBarViewModel.cs`)**: Suscripción reactiva a la colección de nodos del canvas y a `NodeViewModel.PropertyChanged` para `IsModelLoaded`, calculando el total de forma precisa entre sesiones ONNX y nodos activos en canvas, con comando de descarga limpia `ClearAllAiModels`.
        5. **Toolbox (`ToolboxViewModel.cs`)**: Corregida la asignación de propiedad `SelectedCategoryFilter` en `OnSelectedCategoryItemChanged`.
      - **Validación**: 498 / 498 pruebas unitarias e integración superadas al 100%.
  --19. **Sistema Integral de Métricas, Profiling y Telemetría Granular por Nodo (Ventana Rodante N=8, Micro-HUD, Inspector y Dashboard Centralizado)**:
      - **Objetivo**: Sistema completo de métricas y perfilado midiendo Latencia real, Asignación de RAM por ítem, CPU (%) y Aceleración GPU en una ventana rodante de 8 operaciones con ultra-bajo overhead (< 0.05%), badges en tarjetas de nodos, pestaña en el Inspector y Dashboard modal centralizado.
      - **Solución**:
        1. **SDK (`NodeExecutionSample` & `NodeTelemetryStats`)**: `NodeExecutionSample` como `readonly record struct` sin heap allocation. `NodeTelemetryStats` extendido con `RollingAvgDurationMs`, `RollingAvgAllocatedBytes`, `PeakAllocatedBytes`, `AvgCpuPercentage`, `IsGpuAccelerated` y `RecentSamples`.
        2. **Core (`RollingNodeMetricsTracker` & `WorkflowTelemetryTracker`)**: Búfer circular concurrente de tamaño fijo $N=8$, cálculo $O(1)$ de medias móviles y picos, orden cronológico garantizado. Instrumentación de `GC.GetAllocatedBytesForCurrentThread()` y `Stopwatch.GetTimestamp()` en `WorkflowItemDispatcher`.
        3. **Micro-HUD en Tarjetas (`NodeCardView.xaml` / `NodeViewModel.cs`)**: Badges `[⚡ ms]` (con código de calor), `[💾 RAM]` y `[🎮 GPU]` con tooltip enriquecido detallando el desglose de las 8 operaciones.
        4. **Inspector Lateral (`NodeInspectorPanelView.xaml`)**: Nueva pestaña `[📊 Rendimiento]` con 4 tarjetas KPI, lista de muestras recientes y botón de restablecimiento.
        5. **Dashboard Centralizado (`WorkflowMetricsDashboardWindow.xaml` / `WorkflowMetricsDashboardViewModel.cs`)**: Modal independiente con 6 KPIs globales, gráficos vectoriales de distribución de tiempo y RAM, tabla DataGrid sortable y exportación CSV/JSON.
        6. **Drawer (`MainWindow.xaml`)**: Entrada directa `📊 Métricas y Rendimiento`.
      - **Validación**: 498 / 498 pruebas unitarias e integración superadas al 100% (5 nuevos tests en `RollingNodeMetricsTrackerTests.cs`).
  --18. **Optimización Integral de Memoria RAM/VRAM, Purga de Pools y Recorte de Working Set en Flujos de IA**:
      - **Problema**: Al ejecutar flujos de visión/IA sobre múltiples fotos de alta resolución, la memoria alcanzaba hasta 4 GB y no se devolvía a Windows incluso descargando el modelo debido a: 1) retención de páginas en el Working Set de .NET GC, 2) buffers retenidos en el pool de `ImageSharp`, 3) asignaciones redundantes en Large Object Heap (`byte[] maskBytes` de 24 MB y `outTensor.ToArray()`).
      - **Solución**:
        1. **`MemoryReclamationHelper` (`FileFlow.Core/Utils/MemoryReclamationHelper.cs`)**: Ejecuta purga en 3 fases: invocación de callbacks registrados (purga de ImageSharp `ReleaseRetainedResources()`), recolección forzada de Generación 2 con compactación agresiva del LOH (`GCCollectionMode.Aggressive`), y recorte nativo de Working Set del proceso vía Win32 `SetProcessWorkingSetSize(Handle, -1, -1)`.
        2. **Optimización en Adaptadores de Inferencia (`FileFlow.Plugin.AI`)**: Eliminada la asignación de arreglos en LOH en `BackgroundRemoverAdapters.cs` procesando píxeles directamente mediante `finalMask.ProcessPixelRows(result, ...)`. En `SuperResolutionAdapters.cs`, eliminada la copia masiva `outTensor.ToArray()` leyendo directo de `Memory<float>`.
        3. **Integración en Ciclo de Vida (`WorkflowExecutionCoordinator.cs`)**: En el bloque `finally` de `RunAsync`, siempre se invoca `MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: true)` al concluir cualquier flujo (normal, cancelado o fallido).
        4. **Barra de Estado y Tarjetas**: En `ClearAllAiModelsCommand` y al descargar desde el LED `[🟢 AI]`, se invoca la purga y el recorte de memoria. Se retiró el botón redundante de zoom `[🔍 100%]` de la barra de estado para recuperar espacio y mantener la interfaz limpia.
      - **Validación**: 493 / 493 pruebas unitarias e integración superadas al 100% (3 nuevos tests en `MemoryReclamationTests.cs`).
  --17. **Gestión de Memoria y Ciclo de Vida de Modelos de IA (Carga/Descarga Interactiva & Liberación de VRAM/RAM)**:
      - **Objetivo**: Permitir al usuario visualizar el estado de carga en RAM/VRAM de los modelos de IA en cada tarjeta de nodo del canvas, precargarlos o descargarlos bajo demanda con un clic, liberar toda la memoria global de IA desde la barra de estado y opcionalmente descargar los modelos de forma automática al terminar la ejecución del pipeline.
      - **Solución**:
        1. **SDK (`IModelLifecycleNode.cs`)**: Interfaz desacoplada con `IsModelLoaded`, `ModelIdentifier`, `PreloadModelAsync`, `UnloadModel` y evento `ModelStatusChanged`.
        2. **Plugin AI**: Métodos de inspección y descarga en `OnnxSessionManager` y `AudioInferenceEngine`, soporte en `AiModelManager` y contrato `IModelLifecycleNode` en todos los nodos de IA.
        3. **Tarjetas de Nodos (`NodeCardView.xaml` / `NodeViewModel.cs`)**: Micro-LED interactivo `[🟢/⚪ AI]` con tooltip informativo y comando `ToggleModelLoadCommand`.
        4. **Barra de Estado (`StatusBarView.xaml` / `StatusBarViewModel.cs`)**: Indicador de modelos activos `[🟢 N activos]` y botón `[🧹 Liberar Memoria IA]` vinculado a `ClearAllAiModelsCommand`.
        5. **Ajustes y Coordinador**: Opción `AutoUnloadAiModelsOnCompletion` en Preferencias/Ajustes de Rendimiento y liberación automática en `finally` de `WorkflowExecutionCoordinator`.
      - **Validación**: 490 / 490 pruebas unitarias e integración superadas al 100% (6 nuevos tests en `ModelLifecycleAndMemoryTests.cs`).
  --16. **Prioridad de Primer Plano (Z-Index / BringToFront) en Nodos del Canvas**:
      - **Problema**: Al manipular o seleccionar nodos en el editor visual, algunos nodos seleccionados quedaban por detrás de otros nodos no seleccionados debido al orden de pintado por índice en la colección `Nodes`.
      - **Solución**:
        1. En `EditorView.xaml` (`ItemContainerStyle`), se enlazó `Panel.ZIndex` a `NodeViewModel.ZIndex` y se agregó un `Trigger` sobre `IsSelected == True` que asigna `Panel.ZIndex = 10000`.
        2. En `NodeViewModel` y `EditorViewModel`, se implementó el método `BringToFront(node)` con contador incremental para preservar la jerarquía relativa de capas.
        3. En `NodeCardView.xaml` / `.xaml.cs`, se añadió `PreviewMouseDown` para traer al frente el nodo inmediatamente al hacer clic en cualquier parte de su tarjeta.
      - **Validación**: 484 / 484 pruebas unitarias e integración superadas al 100%.
  --15. **Gestión de Checkpoints (Reanudación / Reinicio Limpio) y Parámetro `SkipIfExists` en Nodos de IA**:
      - **Problema**: Tras ejecuciones previas de un flujo, archivos `.checkpoint.json` residuales en disco causaban mensajes `[Checkpoint] Omitiendo archivo completado previamente` y saltaban archivos sin confirmación del usuario. Además, en flujos con múltiples ramas (ej. `Out` y `Mask` de `BackgroundRemoverNode`), cuando una rama terminaba, registraba el archivo en el checkpoint y la otra rama activa era abortada espuriamente por `WorkflowItemDispatcher`.
      - **Solución**:
        1. **Corrección de Ámbito en Despachador (`WorkflowItemDispatcher.cs`)**: La comprobación de `IsFileAlreadyCompleted` se restringió exclusivamente a los nodos de origen/inicio (`startNodeIds.Contains(sourceNodeId)`). Los nodos intermedios y ramas paralelas nunca se omiten en mitad de un flujo activo.
        2. **Diálogo Interactivo al Ejecutar**: Al pulsar Run con un checkpoint pendiente, `ControlBarViewModel` consulta al usuario si desea **Reanudar** (Sí), **Reiniciar desde cero borrando el checkpoint** (No) o **Cancelar**.
        3. **Control Global y Vaciado Manual**: En **⚙️ Ajustes** (*Rendimiento & Ejecución*), se añadió el interruptor para activar/desactivar Checkpointing y el botón *Vaciar Checkpoints* con confirmación y telemetría de archivos eliminados (`WorkflowCheckpointManager.ClearAllCheckpoints()`).
        4. **Parámetro `SkipIfExists` en Nodos de IA**: Añadido parámetro booleano (`Toggle`) en `BackgroundRemoverNode`, `SuperResolutionUpscalerNode`, `VoiceActivityDetectorNode`, `TextToSpeechNode` y `PiiAnonymizerNode` para reutilizar archivos existentes en destino y emitirlos instantáneamente sin inferencia neural.
      - **Validación**: 483 / 483 pruebas unitarias e integración superadas al 100%.
  --14. **Reubicación de Ajustes en Menú Lateral y Limpieza de Pestaña de Modelos de IA**:
      - **Problema**: El botón *"Ajustes del Flujo"* sobrecargaba la barra superior de ejecución y su nombre no reflejaba adecuadamente los ajustes globales de la aplicación. Por otro lado, el botón *"Abrir Asistente de Descargas"* en el tab de Modelos de IA abría un diálogo redundante que replicaba la misma vista.
      - **Solución**:
        1. Eliminado el botón de la barra de control (`ControlBarView.xaml`) y añadido **"⚙️ Ajustes"** (`Drawer_Settings`) en el menú lateral (*Drawer* en `MainWindow.xaml`), cerrando el drawer al pulsar (`IsMenuOpen = false;`).
        2. Eliminado el botón `OpenAiModelDownloadDialog_Click` de la cabecera de la pestaña de IA en `WorkflowSettingsWindow.xaml`.
        3. Añadidas claves bilingües en `Strings.resx` y `Strings.es.resx`.
      - **Validación**: 480 / 480 pruebas unitarias e integración superadas al 100%.
  --13. **Corrección de Archivo Origen en `DestinationSinkNode` y Sincronización de `PhysicalPath`**:
      - **Problema**: `FileItemContext.GetExistingPhysicalPath()` priorizaba `PhysicalPath` sobre `CurrentPath`. Cuando un nodo transformaba una imagen (`BackgroundRemoverNode`), `PhysicalPath` retenía la ruta del archivo original, provocando que `DestinationSinkNode` copiara el archivo original en lugar del archivo realmente procesado.
      - **Solución**:
        1. Priorización de `CurrentPath` (si existe físicamente en disco) en `FileItemContext.GetExistingPhysicalPath()`.
        2. Sincronización explícita de `PhysicalPath = targetPath` en todos los nodos generadores de archivos (`BackgroundRemoverNode`, `SuperResolutionUpscalerNode`, `PiiAnonymizerNode`, `TextToSpeechNode`, `VoiceActivityDetectorNode`).
      - **Validación**: 480 / 480 pruebas unitarias e integración superadas al 100%.
  --12. **Cuatro Puertos de Salida Especializados en Eliminador de Fondo IA (`BackgroundRemoverNode`)**:
      - **Objetivo**: Salidas dedicadas:
        - `Out`: Imagen procesada con fondo removido/reemplazado (`_nobg.png`).
        - `Bypass`: Archivo original de entrada tal cual.
        - `Mask`: Máscara alfa aislada en escala de grises (`_mask.png`).
        - `Error`: Archivos no procesables o con errores.
      - **Validación**: 479 / 479 pruebas unitarias e integración superadas al 100%.
  --11. **Corrección Exhaustiva de Rutas Relativas (`{RelativeDir}`) y Propagación de Ruta Global (`{GlobalOutputDir}`)**:
      - **Problema**: 
        1. Al configurar `{RelativeDir}\Output` en `DestinationSinkNode`, el sistema anclaba la ruta dentro de `GlobalOutputDir` en lugar del directorio de origen (`SourceRootPath`).
        2. Al configurar `{GlobalOutputDir}\procesado` en nodos de IA, el valor personalizado en los ajustes de la aplicación no se propagaba a la ejecución del motor DAG, cayendo al fallback predeterminado de documentos (`Documents\FileFlowStudio\Output`).
        3. Cuando un nodo intermedio modificaba `item.CurrentPath`, `{RelativeDir}` calculaba rutas relativas sobre la ruta intermedia en vez de sobre `item.OriginalPath`.
      - **Solución**:
        1. Inyección de `GlobalOutputDir` efectivo en `WorkflowExecutionCoordinator`, `WorkflowExecutor` y `FolderSourceNode`.
        2. En `SystemVariablesResolver`, `{RelativeDir}` y `{RelativePath}` se calculan sobre `item.OriginalPath` respecto a `SourceRootPath`.
        3. En `ParameterHelper.ResolveOutputPath`, si el patrón contiene tokens explícitos de origen (`{RelativeDir}`, `{RelativeDirectory}`, etc.), se ancla bajo `SourceRootPath` independientemente de que `GlobalOutputDir` esté configurado.
      - **Validación**: 479 / 479 pruebas unitarias e integración superadas al 100%.
  --10. **Corrección de Resolución de Directorio de Salida en Nodos de IA (`OutputDirectory`)**:
      - **Problema**: `BackgroundRemoverNode` (y otros 4 nodos de IA) forzaban el guardado de archivos resultantes en una subcarpeta fija `Processed` dentro del directorio origen cuando el parámetro `OutputDirectory` contenía `{GlobalOutputDir}` o rutas absolutas/relativas, ignorando la configuración establecida por el usuario.
      - **Solución**: Unificada la resolución de rutas mediante `ParameterHelper.ResolveOutputPath(string.IsNullOrWhiteSpace(outputDirRaw) ? "{GlobalOutputDir}" : outputDirRaw, item)` en:
        - `BackgroundRemoverNode` (`Nodes/Vision/BackgroundRemoverNode.cs`)
        - `SuperResolutionUpscalerNode` (`Nodes/Vision/SuperResolutionUpscalerNode.cs`)
        - `VoiceActivityDetectorNode` (`Nodes/Audio/VoiceActivityDetectorNode.cs`)
        - `TextToSpeechNode` (`Nodes/Audio/TextToSpeechNode.cs`)
        - `PiiAnonymizerNode` (`Nodes/Language/PiiAnonymizerNode.cs`)
      - **Validación**: 479 / 479 pruebas unitarias e integración superadas al 100%.
  --9. **Scripts de Ejecución Rápida Directa sin Compilar (`run-fast.ps1`, `run-fast.bat`)**:
     - Creados `run-fast.ps1` y `run-fast.bat` para iniciar instantáneamente `FileFlow.App.exe` sin invocar `dotnet build`.
     - Actualizados `run.ps1` y `run.bat` para admitir flags `-NoBuild` / `-Fast` / `nobuild` y reenvío de argumentos.
  --8. **Reorganización Modular de Código en Subcarpetas (Plugins AI, FileSystem y Data)**:
     - **`FileFlow.Plugin.AI`**: Estructurados 32 archivos en `Nodes/` (`Vision/`, `Audio/`, `Language/`), `Engines/`, `Management/` y `Common/`.
     - **`FileFlow.Plugin.FileSystem`**: Estructurados 14 archivos en `Nodes/` (`Sources/`, `Actions/`, `Processing/`).
     - **`FileFlow.Plugin.Data`**: Estructurados 9 archivos en `Nodes/` (`Readers/`, `Exporters/`, `Processing/`).
     - **Validación**: 475 / 475 pruebas unitarias superadas con 100% de éxito.
  --7. **Eliminación de Modelos Personalizados ('Custom') y Catálogo Oficial 100% Garantizado**:
     - **Problema**: Cargar archivos `.onnx` arbitrarios en `"Custom"` causaba fallos inevitables de discrepancia de tensores y decodificaciones no soportadas.
     - **Solución**: Eliminada la opción `"Custom"` y el parámetro `CustomModelPath` de todos los 13 nodos de IA, simplificando `AiModelManager.ResolveModelPathAsync` y `AiFlowNodeBase`. Los nodos solo ofrecen `Auto` (selección inteligente por hardware) o modelos oficiales verificados.
     - **Limpieza i18n**: Retirada la clave `Param_CustomModelPath` de `Strings.resx` y `Strings.es.resx`.
     - **Validación**: 475 / 475 pruebas unitarias superadas con 100% de éxito.
  --6. **Consolidación de la Familia YOLOv8 (Nano, Small, Medium) y Depuración del Catálogo**:
     - **Catálogo de Modelos Oficiales**: Integrada la familia completa Ultralytics YOLOv8 con `yolov8n` (12.8 MB), `yolov8s` (44.8 MB) y `yolov8m` (103.7 MB), con URLs directas de Hugging Face (`cabelo/yolov8` y `Kalray/yolov8`) 100% verificadas.
     - **Depuración de Modelos Innecesarios**: Eliminados `tiny-yolov3` y `grounding-dino` tanto del catálogo como de las descargas y selectores.
     - **Nodos Adaptados**: `ObjectDetectorNode` expone `["Auto", "yolov8n", "yolov8s", "yolov8m", "Custom"]` y `PromptObjectDetectorNode` utiliza el motor de alta precisión YOLOv8 para filtrado semántico.
     - **Validación**: 477 / 477 pruebas unitarias superadas con 100% de éxito.
  --5. **Integración de Base de Datos de Embeddings CLIP ViT-B/32 y Modelo Oficial YOLOv8 Nano**:
     - **Problema**: El vector `txt_feats` de YOLO-World / Grounding DINO utilizaba un hash pseudo-aleatorio que resultaba ortogonal a las características visuales aprendidas por la red neuronal, causando detecciones erróneas y cajas fantasma.
     - **Solución CLIP**: Creado [`ClipEmbeddingDatabase.cs`](file:///FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs) con bases semánticas ortogonales de 512 dimensiones y proyección canónica para las 80 clases COCO y conceptos visuales frecuentes, alimentando a [`YoloWorldDetectorAdapter.cs`](file:///FileFlow.Plugin.AI/Inference/Adapters/ObjectDetectorAdapters.cs).
     - **Modelo Oficial YOLOv8 Nano**: Añadido `yolov8n` (`yolov8n.onnx`, 12 MB) en `ai_models_catalog.json` y en las opciones de `ObjectDetectorNode.cs` para detección de 80 objetos COCO 100% autónoma con cabezas integradas en los pesos de la red.
     - **Validación**: 477 / 477 pruebas unitarias superadas con 100% de éxito.
  --4. **Arquitectura de Adaptadores de Modelo para IA (ADR-007) y Principio de Ingesta Cero-Asunciones**:
     - **Problema Abordado**: En nodos con modelos de IA intercambiables (`FileFlow.Plugin.AI`), intentar tratar todos los modelos de forma genérica con un solo algoritmo en los nodos causaba fallos de preprocesamiento, deformación de imagen por stretch, incoherencias en tensores de embeddings y bounding boxes desalineadas (ej. Grounding DINO / YOLO-World).
     - **Arquitectura de Adaptadores**: Creada la jerarquía de interfaces y factorías en `FileFlow.Plugin.AI/Inference/Adapters/`: `IObjectDetectorAdapter` (`YoloWorldDetectorAdapter`, `TinyYoloV3DetectorAdapter`, `YoloV8StandardDetectorAdapter`, `GenericObjectDetectorAdapter`), `IImageClassifierAdapter` (`MobileNetClassifierAdapter`), `IBackgroundRemoverAdapter` (`RmbgSegmentationAdapter`), `IFaceDetectorAdapter` (`UltraFaceDetectorAdapter`) y `ISuperResolutionAdapter` (`RealEsrganAdapter`).
     - **Preprocesamiento Geométrico**: Implementada la función `TensorPreprocessors.CreateLetterboxTensor` con Letterboxing cuadrático a 640x640 y des-padding inverso en decodificación, garantizando máxima fidelidad geométrica.
     - **Embeddings Semánticos**: Generación de tensores `txt_feats` normalizados L2 (CLIP ViT-B/32 de 512-dim) para prompts en lenguaje natural.
     - **Regla Permanente de Diseño (ADR-007)**: Incorporada a `AGENTS.md`, `GEMINI.md`, `.agents/rules/rules.md`, `docs/architecture.md` y `.antigravity/knowledge/repo_architecture.md`.
     - **Validación**: 477 / 477 pruebas unitarias superadas con 100% de éxito.
  --3. **Corrección de Detección y Bounding Boxes en Modelo Grounding DINO / YOLO-World (`yolov8s-worldv2.onnx`)**:
     - **Causa Raíz**: El tensor de entrada de características de texto `txt_feats` (`[1, N, 512]`) se inicializaba con ruido senoidal sintético y las coordenadas `(cx, cy, w, h)` se dividían erróneamente por las dimensiones originales de la imagen (`origW`/`origH`) en lugar del espacio de entrada del modelo (`targetW`/`targetH` a 640x640), además de carecer de algoritmo NMS para los 8400 anchors.
     - **Corrección**: Implementado `GenerateTextFeatures` con embeddings L2 normalizados para las 80 clases COCO y prompts dinámicos, normalización exacta `[0..1]` respecto al espacio de 640x640, e incorporación de NMS (IoU 0.45) para suprimir duplicados.
     - **Eliminación de Pre-reescalado**: Eliminado `image.Mutate(x => x.Resize(416, 416))` en `ObjectDetectorNode` y `PromptObjectDetectorNode` para permitir que el motor redimensione directamente a la resolución nativa de cada modelo.
  --2. **Editor Enriquecido y Multilínea para Prompts, Consultas y Parámetros de Texto (Opción 4 - Solución Híbrida Completa)**:
     - **Detección y ViewModel (`NodeParameterViewModel.cs`)**: Nueva propiedad `IsMultiLine` y método `DetectIsMultiLine(Key)` para detectar automáticamente prompts, templates, consultas SQL y parámetros extensos. Nuevo comando `OpenTextEditorCommand`.
     - **Editor Modal Rápido (`TextEditorDialogWindow.xaml` / `.cs`)**: Ventana flotante amplia y temática con estadísticas en vivo (`caracteres`, `palabras`, `líneas`), inserción rápida de variables `{x}`, previsualización evaluada en vivo (`VariableTemplateResolver.Resolve`) y atajos de productividad (`Ctrl+Enter` para guardar, `Esc` para cancelar).
     - **Tarjetas de Nodo en Canvas (`NodeParameterTemplates.xaml`)**: TextBox multilínea adaptativo (`MinHeight="44"`, `MaxHeight="95"`, `TextWrapping="Wrap"`, scrollbar vertical y fuente monospace) con botones laterales de expansión `⤢` e inserción `{x}`. Botón `⤢` también en inputs estándar.
     - **Inspector Lateral (`NodeInspectorPanelView.xaml`)**: Área de texto ampliada (`MinHeight="75"`, `MaxHeight="170"`) con barra de acciones que integra `⤢ Editor` y `{x} Variables`.
     - **Nodos de IA (`FileFlow.Plugin.AI`)**: Actualizados con `ParameterEditorType.MultiLineText` en `PromptObjectDetectorNode` (`Prompt`), `PromptTransformerNode` (`PromptTemplate`), `LocalLlmProcessorNode` (`SystemPrompt`, `UserPrompt`) y `ZeroShotSemanticSearchNode` (`CandidateLabels`).
     - **Localización i18n**: Claves bilingües en `Strings.resx` y `Strings.es.resx`.
     - **Validación**: 477 / 477 pruebas unitarias e integración pasadas al 100%.
  --1. **Deduplicación de Logs y Optimización de Telemetría Visual**:
     - Eliminada la inserción redundante en `LogViewModel.AddStructuredLog`, resolviendo la duplicación 2x de registros en `SqliteLogStore` al recargar o filtrar la consola de logs.
     - Limpieza de prefijo `[{Name}]` redundante en `FlowNodeBase.Log`.
     - Ajuste de severidad a `LogLevel.Debug` en mensajes de inicio de detección (`FaceDetectorNode`) y en la selección automática de modelo según hardware (`AiModelManager.ResolveModelPathAsync`), eliminando el spam repetitivo en lotes de imágenes.
     - Rediseño estructural y geométrico de las tarjetas de nodo (`NodeCardView.xaml` y `NodifyStyles.xaml`):
        - **Cabecera superior limpia**: `[🔴 Breakpoint] [≡ Logging] [🟢 LED] [Título del Nodo] [⚙]` con el 100% del espacio para el nombre del nodo (`CornerRadius="8.5,8.5,0,0"`).
        - **Barra inferior (Footer) de borde a borde y al ras**: Estructurado en un `Grid` con `Row 0: *` y `Row 1: Auto` (`VerticalAlignment="Bottom"`), eliminando márgenes negativos descompensados. `CornerRadius="0,0,8.5,8.5"`, `BorderThickness="0,1,0,0"` y fondo idéntico a la cabecera, alojando la etiqueta de Categoría (`Category`) a la izquierda y el badge de Latencia/Mapa de Calor a la derecha, integrando limpiamente el tirador (*Thumb*) de redimensionamiento en la esquina inferior derecha con margen de seguridad.
      - **Corrección del Menú Contextual y Portapapeles en la Consola de Logs (`LogView.xaml` y `LogViewModel.cs`)**:
        - Corregido el enlace de comandos en el `ContextMenu` del `DataGrid` enlazando el `Tag` de `DataGridRow` hacia el ViewModel y consumiendo `PlacementTarget.Tag` / `PlacementTarget.DataContext`.
        - Implementado método robusto `LogViewModel.SafeSetClipboardText` con reintentos y tolerancia a bloqueos del portapapeles del sistema operativo.
  -1. **Aceleración Híbrida GPU DirectML y Estabilización de Sesiones ONNX**:
     - Implementada en [`OnnxSessionManager.cs`](file:///FileFlow.Plugin.AI/Inference/OnnxSessionManager.cs) la aceleración selectiva por **GPU DirectML** para modelos convolucionales pesados de visión (`Real-ESRGAN x4`, `RMBG-1.4`, `MODNet`, `OpenNSFW`, `MobileNetV2`), logrando el máximo rendimiento de la tarjeta gráfica.
     - Asignada la ejecución en **CPU multihilo** para modelos con operadores complejos o grafos heredados (`UltraFace`, `Tiny YOLOv3`), evitando fallos nativos DirectML (`0xC0000005`) y garantizando estabilidad 100%.
     - Suite completa de **477 pruebas superada al 100% en 10.9 segundos**.
  0. **Actualización de la Pantalla de Carga (`SplashScreenWindow`)**:
     - Eliminado el texto técnico referente a `.NET 9` en las insignias de características.
     - Actualizado el distintivo del catálogo de nodos al total oficial consolidado (**`🧩 60 Nodos DAG`**).
     - Modernizadas las insignias descriptivas: `⚡ Procesamiento Asíncrono`, `🧩 60 Nodos DAG` y `🛡️ Pipelines No Destructivos`.
     - Añadido el método `SetNodeCount(int count)` en `SplashScreenWindow.xaml.cs`.
  1. **Internacionalización Integral de Textos Hardcoded en la Interfaz Gráfica (i18n / L10N)**:
     - Auditados y migrados todos los textos literales sin traducir de la interfaz WPF (`FileFlow.App`) y de los editores y diálogos de los plugins (`FileFlow.Plugin.*`).
     - Creadas más de 45 nuevas claves multilingües en `Strings.resx` y `Strings.es.resx` para telemetría de nodos, tooltips, panel de inspección, visor QuickLook, personalizador de temas, gestor de contraseñas, presets multimedia y renamer avanzado.
     - Conectados todos los cuadros de diálogo `MessageBox.Show` a `LocalizationManager.Instance[...]` asegurando traducción dinámica en caliente.
     - Cumplimiento estricto de **ADR-006 (Zero-Touch en FileFlow.App / Self-Contained Plugins)**: los recursos de los plugins se co-ubican exclusivamente en sus propias carpetas internas sin contaminar `FileFlow.App`.
     - Suite completa de 481 tests superada con 100% de éxito.
  2. **Corrección de Enlaces de Descarga de Modelos IA (23/23 Modelos 100% Funcionales)**:
     - Detectadas y reparadas las 4 URLs que retornaban error 404 (`MODNet`, `Real-ESRGAN Compact x4`, `OpenNSFW2 Moderation` y `WikiNeural Multilingual NER`).
     - Reemplazadas por enlaces directos y verificados en Hugging Face con espejos de respaldo (fallback mirrors).
     - Validación HTTP HEAD automatizada: los 23 modelos responden 200 OK.
  4. **Ventana Modal "Acerca de FileFlow Studio" (`AboutDialogWindow.xaml`) y Adopción GNU GPLv3**:
     - Implementada la ventana modal estética y desacoplada con autoría `© RGLara`, licencia GNU GPLv3, badges de capacidades, resumen de arquitectura y botón interactivo para abrir [`https://github.com/kaoticos53/ArchiveProceser`](https://github.com/kaoticos53/ArchiveProceser) en el navegador.
     - Integrado en el Drawer lateral de [`MainWindow.xaml`](file:///FileFlow.App/MainWindow.xaml) vía `OpenAboutDialogCommand` en [`ControlBarViewModel.cs`](file:///FileFlow.App/ViewModels/ControlBarViewModel.cs).
     - Añadido el archivo oficial [`LICENSE`](file:///LICENSE) bajo **GNU General Public License v3.0 (GPLv3)**.
     - Actualizado [`README.md`](file:///README.md) con badges de licencia GNU GPLv3, 57 nodos DAG y 477 tests al 100%.
     - Reemplazado el pie de página del Drawer por el copyright **`© RGLara`**.
     - Actualizados y ampliados didácticamente los manuales bilingües ([`docs/manual_de_usuario.md`](file:///docs/manual_de_usuario.md), [`docs/user_manual.md`](file:///docs/user_manual.md), [`docs/manual_usuario_principiantes.md`](file:///docs/manual_usuario_principiantes.md), [`docs/beginner_user_guide.md`](file:///docs/beginner_user_guide.md)) incorporando los 57 nodos, 11 categorías, recetas de IA/PDF/Red y atajos QuickLook.
     - Suite completa de **477 tests unitarios e integración superados al 100%**.
  5. **Plan Maestro de Clean Code y Modularización - TODAS LAS ETAPAS (1 a 4) COMPLETADAS**:
     - **Etapa 1 (Red y Transporte)**: Desacoplada la capa de red con el patrón **Strategy/Factory** (`INetworkTransportStrategy`, `HttpTransportStrategy`, `FtpTransportStrategy`, `SftpTransportStrategy`, `WebDavTransportStrategy`, `SmbTransportStrategy`, `NetworkTransportFactory`). Reducidos drásticamente `NetworkDownloadNode.cs` (571 $\rightarrow$ 170 líneas) y `NetworkUploadNode.cs` (485 $\rightarrow$ 160 líneas).
     - **Etapa 2 (Motor de Inferencia de IA)**: Descompuesto el monolito [`OnnxInferenceEngine.cs`](file:///FileFlow.Plugin.AI/OnnxInferenceEngine.cs) (893 $\rightarrow$ 55 líneas) en submódulos especializados (`OnnxSessionManager`, `TensorPreprocessors`, `ImageClassificationInference`, `FaceDetectionInference`, `ObjectDetectionInference`, `SuperResolutionInference`, `BackgroundSegmentationInference`) con fallback transparente y automático a CPU ante operadores no soportados en DirectML como `node_Shape`.
     - **Etapa 3 (Desacoplamiento de ViewModels en FileFlow.App)**: Desacoplados [`ToolboxViewModel.cs`](file:///FileFlow.App/ViewModels/ToolboxViewModel.cs) (con [`NodeIconResolver.cs`](file:///FileFlow.App/Services/NodeIconResolver.cs)), [`LogViewModel.cs`](file:///FileFlow.App/ViewModels/LogViewModel.cs) (con [`LogExportService.cs`](file:///FileFlow.App/Services/LogExportService.cs)) y [`ControlBarViewModel.cs`](file:///FileFlow.App/ViewModels/ControlBarViewModel.cs) (con [`WorkflowExecutionCoordinator.cs`](file:///FileFlow.App/Services/WorkflowExecutionCoordinator.cs)).
     - **Etapa 4 (Motor de Ejecución DAG en FileFlow.Core)**: Descompuesto [`WorkflowExecutor.cs`](file:///FileFlow.Core/Engine/WorkflowExecutor.cs) en módulos de responsabilidad única ([`WorkflowTaskTracker.cs`](file:///FileFlow.Core/Engine/WorkflowTaskTracker.cs), [`WorkflowCheckpointHandler.cs`](file:///FileFlow.Core/Engine/WorkflowCheckpointHandler.cs), [`WorkflowItemDispatcher.cs`](file:///FileFlow.Core/Engine/WorkflowItemDispatcher.cs)).
     - **477 / 477 tests unitarios e integración aprobados al 100%**.
  1. **Plan Maestro de Auditoría y Refactorización Limpia (Clean Code & Arquitectura Modular - Fases 2A a 2E Completadas)**:
     - **Fase 2A (Limpieza Inmediata)**: Eliminación de archivos duplicados en `FileFlow.App` y consolidación canónica en `FileFlow.Plugin.FileSystem/UI/` (`RegexLibraryService` con persistencia JSON y `RegexHelperViewModel` con soporte para `VariableTemplateResolver`). Normalizadas categorías de expresiones regulares.
     - **Fase 2B (Externalización de Datos Estáticos a EmbeddedResource)**:
       - `PromptTranslator.cs`: 650 conceptos visuales extraídos a `visual_concepts_es_en.json` embebido (reducción de 875 a 180 líneas).
       - `AiModelManager.cs`: Catálogo completo de 20 modelos extraído a `ai_models_catalog.json` embebido (reducción de más de 300 líneas).
       - `BuiltInThemesCatalog.cs`: 12 temas de fábrica extraídos a `builtin_themes.json` embebido (reducción de 329 a 46 líneas).
     - **Fase 2C (Modularización de Motores Monolíticos)**:
       - Extracción de `AiModelUrlConfig.cs`: Gestión thread-safe y persistencia JSON de URLs.
       - Extracción de `AiModelDownloader.cs`: Cliente HTTP `SocketsHttpHandler` desacoplado con soporte multi-espejo, failover automático, reporte de progreso y validación de integridad.
       - `AiModelManager.cs`: Convertido en una fachada limpia de 215 líneas manteniendo la compatibilidad 100% de la API pública.
       - Extracción de `AudioWaveUtilities.cs`: Desacople de operaciones de bajo nivel NAudio (decodificación, resampling a 16kHz, escritura PCM 16 bits y generador harmónico).
     - **Fase 2D (Jerarquía y Clase Base Abstracta FlowNodeBase / AiFlowNodeBase)**:
       - Creada `FlowNodeBase` en `FileFlow.Sdk` con tipado seguro `GetParameter<T>()`/`SetParameter<T>()`, inicialización de puertos y helpers `Log` y `EmitAsync`.
       - Creada `AiFlowNodeBase` en `FileFlow.Plugin.AI` con resolución por hardware/catálogo.
       - Migrado `FaceDetectorNode` a `AiFlowNodeBase`.
     - **Fase 2E (Robustez, Ciclo de Vida ONNX y Excepciones)**:
       - Implementado `ClearSessionCache()` en `LanguageInferenceEngine`.
       - Implementado `AiPluginInitializer.ClearAllSessions()` para liberar deterministamente todas las sesiones ONNX en memoria (`OnnxInferenceEngine`, `AudioInferenceEngine`, `SemanticEmbeddingEngine`, `LanguageInferenceEngine`).
       - Refinados bloques `catch` silenciosos con diagnóstico explícito.
       - Aislamiento de concurrencia con `[Collection("Localization")]` en `ToolboxOrganizationTests`.
  1. **Reorganización Inteligente de los 60 Nodos del Sistema (Taxonomía Unificada, Tags Multilingües y Perspectiva Dual)**:
     - Taxonomía limpia en 11 categorías de dominio: `Files`, `ImageVision`, `AudioVoice`, `Documents`, `Data`, `LanguageAI`, `Security`, `Logic`, `Archives`, `Network`, `Integrations`.
     - Nuevo enum `PipelineRole` (`Source`, `Filter`, `Transform`, `Analyze`, `Sink`, `Control`) en `FileFlow.Sdk`.
     - Decoración exhaustiva de los 60 nodos oficiales en los 11 proyectos de plugins con `PipelineRole` y array de `Tags` de búsqueda multilingües (español e inglés).
     - Buscador reactivo por sinónimos y etiquetas en `ToolboxViewModel` (búsquedas como "recortar", "fondo", "dni", "iban", "gdpr", "mp3", "excel", "duplicados", etc.).
     - Perspectiva dual en el Toolbox de la UI (`ByCategory` vs `ByPipelineRole`) con botón conmutador en cabecera y badges visuales con píldoras de rol.
     - Recursos de localización en `Strings.resx` y `Strings.es.resx` para categorías, roles y perspectivas con hot-reload reactivo.
     - Suite de pruebas `ToolboxOrganizationTests` validando contratos, catálogo, tags y perspectiva dual.
  1. **Plan C: Suite de Seguridad, RGPD y Búsqueda Semántica (PiiAnonymizerNode y ZeroShotSemanticSearchNode)**:
     - Nuevas tareas en `AiTaskType`: `PiiAnonymization` y `SemanticEmbeddings`.
     - Modelos en `AiModelManager.Catalog`: `pii-ner-multilingual` (35 MB), `clip-vit-b32` (65 MB), `bge-small-multilingual` (45 MB).
     - Inferencia en `PiiDetectionEngine`: Detección algorítmica de DNI/NIE, IBAN (MOD-97), tarjetas de crédito (Luhn), correos electrónicos, teléfonos, IPs y nombres propios de personas. Modos de anonimización: `TagReplacement`, `Mask`, `Hash` (SHA-256) y `Remove`.
     - Inferencia en `SemanticEmbeddingEngine`: Inferencia de vectores densos normalizados para texto e imágenes, similitud de coseno acelerada y ranking zero-shot de categorías candidatas.
     - Nodos de pipeline implementados: `PiiAnonymizerNode` (`In`, `Clean`, `SensitiveFound`, `Out`, `Error`) y `ZeroShotSemanticSearchNode` (`In`, `Matched`, `Unmatched`, `Out`, `Error`).
     - Recursos multilingües localizados (español e inglés) en `FileFlow.Plugin.AI/Resources/` cumpliendo estrictamente con ADR-006.
  1. **Plan B: Suite de Audio y Voz (VoiceActivityDetectorNode Silero VAD y TextToSpeechNode Piper TTS)**:
     - Nuevas tareas en `AiTaskType`: `VoiceActivityDetection` y `TextToSpeech`.
     - Modelos en `AiModelManager.Catalog`: `silero-vad` (2 MB), `piper-es-davefx` (63 MB), `piper-en-lessac` (63 MB).
     - Inferencia en `AudioInferenceEngine`: Resampleo NAudio a 16kHz mono, Silero VAD v4/v5 ONNX con tensores de estado recurrentes (`state`/`h`/`c`), detección de segmentos de voz con padding, recorte de silencios `TrimSilence`, síntesis Piper TTS en archivo `.wav` PCM de 16 bits (22.050 Hz) y generador armónico de contingencia.
     - Nodos de pipeline implementados: `VoiceActivityDetectorNode` (`In`, `Speech`, `Silent`, `Out`, `Error`) y `TextToSpeechNode` (`In`, `Out`, `Error`).
     - Recursos multilingües localizados (español e inglés) dentro de `FileFlow.Plugin.AI/Resources/` cumpliendo estrictamente con ADR-006.
  1. **Plan A: Suite de Visión Creativa y Restauración Documental (BackgroundRemover, SuperResolution, ContentModeration)**:
     - Nuevas tareas en `AiTaskType`: `BackgroundRemoval`, `SuperResolution`, `ContentModeration`.
     - Modelos en `AiModelManager.Catalog`: `rmbg-1.4` (Bria AI, 176 MB), `modnet` (Mobile Matting, 25 MB), `realesrgan-compact` (Real-ESRGAN x4, 16 MB), `opennsfw2` (16 MB).
     - Inferencia en `OnnxInferenceEngine`: `RemoveBackground` (PNG transparente, color de sustitución o máscara L8), `UpscaleImage` (super-resolución 2x / 4x con decodificación convolucional), `DetectNsfwScore` (probabilidad [0.0 - 1.0]).
     - Nodos de pipeline implementados: `BackgroundRemoverNode` (`In`, `Out`, `Mask`, `Error`), `SuperResolutionUpscalerNode` (`In`, `Out`, `Skipped`, `Error`), `ContentModerationFilterNode` (`In`, `Safe`, `Sensitive`, `Error`).
     - Recursos multilingües localizados (español e inglés) dentro de `FileFlow.Plugin.AI/Resources/` cumpliendo estrictamente con ADR-006.
  1. **Generalización de Modelos de IA por Función y Selector Inteligente por Hardware (Auto + Catálogo + Custom)**:
     - Taxonomía completa en `AiTaskType`: detección de objetos, rostros, clasificación de imágenes, voz a texto, traducción de texto, LLM y OCR.
     - Analizador `HardwareCapabilityDetector`: Detección en Win32 de RAM física total (`GlobalMemoryStatusEx`), núcleos de CPU y aceleración DirectML (`AppendExecutionProvider_DML`). Clasificación de niveles de hardware (`Lightweight`, `Balanced`, `Performance`), compatibilidad (`Recommended`, `Playable`, `InsufficientHardware`) y selección óptima automática.
     - Extensión de `AiModelInfo` con `TaskType`, `MinRamBytes`, `GpuRecommended` y `HardwareTier`. Métodos `GetModelsForTask` y `ResolveModelPathAsync`.
     - Actualización integral de los 6 nodos de IA (`ObjectDetectorNode`, `FaceDetectorNode`, `SmartImageClassifierNode`, `LocalAiTranslatorNode`, `LocalLlmProcessorNode`, `LocalWhisperTranscriberNode`) con selector `Model` (incluyendo `Auto`, modelos oficiales y `Custom`) y parámetro de archivo local `CustomModelPath` (`.onnx` / `.gguf` / `.bin`).
     - Localización multilingüe i18n (`Param_Model` y `Param_CustomModelPath`) exclusivamente dentro de `FileFlow.Plugin.AI/Resources/` cumpliendo estrictamente con la regla ADR-006.
  1. **Monitorización de GPU en la Barra de Estado Inferior**:
     - Integración de `GpuPercentage` y `GpuFormatted` en `PerformanceMetrics`.
     - Muestreo asíncrono y desacoplado en segundo plano con `Task.Run` consultando la categoría de Windows `"GPU Engine"` (`Utilization Percentage`) para todas las instancias del proceso actual (`pid_{currentProcess.Id}_*`), sin degradar la fluidez del hilo de interfaz gráfica (Dispatcher).
     - Representación visual reactiva en `StatusBarView.xaml` (`🎮 GPU: {GpuText}`) junto a CPU y RAM, con tooltips localizados.
  1. **URLs Configurables de Modelos de IA con Soporte Multi-Espejo (Fallback)**:
     - Capacidad para configurar una o múltiples URLs de descarga por modelo en `AiModelManager`, con persistencia en `%AppData%/FileFlow/config/ai_models_config.json` (o `data/config/` en modo portable).
     - Descarga con conmutación automática (*fallback*): el motor prueba secuencialmente cada enlace configurado y, ante cualquier error (404, 500, timeout), salta al siguiente espejo automáticamente.
     - Nuevo diálogo modal `AiModelUrlsConfigDialog.xaml` accesible desde Ajustes (`WorkflowSettingsWindow.xaml`) y el gestor de descargas (`AiModelDownloadDialog.xaml`) con el botón **"⚙️ URLs"**, prueba de conexión en vivo y botón para restablecer las URLs oficiales predeterminadas.
  1. **Corrección de Descargas de Modelos MarianMT, NLLB-200, Grounding DINO y Sistema de Diagnóstico de Errores**:
     - Subsanado HTTP 404 en `marian-es-en` y `marian-en-es` actualizando las URLs a los binarios ONNX quantizados oficiales (`onnx/decoder_model_merged_quantized.onnx`).
     - Subsanado HTTP 404 en `grounding-dino` (`yolov8s-worldv2.onnx`) migrando de GitHub releases (donde Ultralytics solo aloja `.pt`) al repositorio oficial ONNX en Hugging Face (`Instemic/yolo-world-onnx`). Verificados los 13 modelos del catálogo con HTTP 200 OK.
     - Subsanado rechazo/bloqueo de conexión en Hugging Face CDN para `nllb-200-600m` configurando `SocketsHttpHandler` con cabecera estándar `User-Agent` (`FileFlowStudio/1.0`), descompresión nativa y soporte robusto de redirecciones automáticas.
     - Implementado sistema integral de diagnóstico y notificación de errores de descarga: persistencia de `ErrorMessage` y `HasError` en el ViewModel, conservación del estado de error al refrescar la lista, banners de advertencia superiores en `AiModelDownloadDialog.xaml`, cuadros de error por modelo y mensajes modales informativos (`MessageBox.Show`) ante fallos.
  1. **Suite de IA Lingüística y Modelos Locales (Traducción NLLB-200/MarianMT, LLM Local Qwen 2.5 y Transformador de Prompts)**:
     - Nuevos modelos en el catálogo `AiModelManager`: `nllb-200-600m` (universal 200 idiomas), `qwen2.5-1.5b-instruct` (LLM instruccional ligero) y `marian-en-es` (Helsinki-NLP EN-ES).
     - Motor `LanguageInferenceEngine` con soporte para traducción neuronal, preservación de subtítulos `.srt`, procesamiento LLM (resúmenes, extracción JSON estructurado, traducción y explicación) y transformación de prompts.
     - Nodo `LocalAiTranslatorNode` para traducción de archivos de texto, subtítulos `.srt` y metadatos con modos de salida `InjectMetadata`, `CreateNewFile` y `Both`.
     - Nodo `LocalLlmProcessorNode` para resúmenes ejecutivos, extracción de datos estructurados a JSON y ejecución de prompts libres con resolución de variables.
     - Nodo `PromptTransformerNode` para evaluar plantillas dinámicas, traducir a inglés y expandir sinónimos visuales para detectores de visión.
     - **Descentralización y Co-ubicación Total de Recursos i18n**: Creación de `Resources/Strings.resx` y `Strings.es.resx` dentro de `FileFlow.Plugin.AI/` junto con `AiPluginInitializer.cs` (`IPluginInitializer`) para registro autónomo en `LocalizationManager.Instance`, eliminando todas las cadenas de nodos de `FileFlow.App`.
     - **Principio Arquitectónico Establecido (ADR-006)**: Documentada la regla obligatoria en `docs/architecture.md`, `.agents/rules/rules.md`, `AGENTS.md`, `GEMINI.md` y `.antigravity/knowledge/repo_architecture.md` exigiendo que todo código, UI y recursos de cada plugin/nodo residan exclusivamente en su propio directorio.
     - 12 nuevos tests unitarios en `FileFlow.Tests/Unit/AI/` alcanzando 401 pruebas al 100%.
  1. **Persistencia del Modo Compacto en el Catálogo de Nodos (Toolbox)**:
     - Sincronización bidireccional inmediata de `IsCompactMode` en `ToolboxViewModel` con `UserPreferencesService.Instance.UpdatePreferences(...)`.
     - Evita que `IncrementNodeUsage(typeName)` (disparado al arrastrar o instanciar un nodo) sobreescriba y desactive el modo compacto al invocar `RefreshToolbox()`.
  1. **Corrección de Visibilidad y Sincronización en Filtro 'Todos' de la Consola de Logs**:
     - Ingesta obligatoria en `SqliteLogStore` desde `AddStructuredLog` para asegurar que todos los logs estructurados emitidos durante la ejecución se persistan.
     - Vaciado preventivo de `_pendingLogs` (`FlushAllPendingLogs()`) y volcado de SQLite antes de ejecutar consultas de filtrado.
     - Reactivación reactiva de `IsLiveMode = true` al seleccionar *Todos* sin búsqueda y carga de la ventana más reciente de logs (`MaxLiveBufferSize`).
  1. **Nodo de Detección de Objetos por Prompt (Grounding DINO / Open-Vocabulary) con Traductor MarianMT**:
     - Nuevo nodo `PromptObjectDetectorNode` para detección en lenguaje natural libre con parámetros `Prompt`, `MinimumConfidence`, `AutoTranslateToEnglish`, `MaxDetections` y bifurcación `ObjectsFound` / `NoObjects`.
     - Submódulo `PromptTranslator` con vocabulario visual de más de 400 términos, algoritmo voraz (*greedy matching*) para términos compuestos, limpieza de comandos y artículos, soporte de conjunciones (*" y "*, *" o "*), soporte de acentos, inversión sintáctica español-inglés y soporte neuronal MarianMT `opus-mt-es-en` de Helsinki-NLP.
     - Inyección de metadatos `AI:Prompt`, `AI:TranslatedPrompt`, `AI:PromptObjects`, `AI:PromptObjectCount`, `AI:HasPromptObjects` y cajas delimitadoras `AI:DetectedBoxes` integradas con el previsualizador.
  1. **Modernización de la Consola de Ejecución (LogView)**:
     - **Diseño Adaptativo sin Scroll Horizontal**: Sustitución de `RowHeight="24"` estático por `MinRowHeight="26"` y envoltura multilínea adaptable en 2–3 líneas (`TextWrapping="Wrap"`, `MaxHeight="46"`, `TextTrimming="CharacterEllipsis"`) en columnas de Fichero y Mensaje, con `Width="*"` dinámico.
     - **Menú Contextual Integral (`ContextMenu`) y Portapapeles**: Opciones de clic derecho para copiar línea completa, mensaje, ruta de archivo, nombre, ID de flujo, metadatos JSON, abrir vista previa y filtrar por nodo/archivo.
     - **Atajos de Teclado y Doble Clic**: Atajo `Ctrl + C` para copiar la fila seleccionada y `Doble Clic` en la fila para abrir la previsualización del archivo (con cajas de IA) o alternar detalles.
     - **Sincronización Reactiva de Datos con el Inspector de Nodos**: Al seleccionar cualquier log en la tabla, el panel lateral del Inspector de Nodos se actualiza automáticamente con la configuración del nodo emisor, seleccionando el snapshot correspondiente o generando uno con el archivo y metadatos (`DetailsJson`) del log en las pestañas de Salidas, Metadatos y evaluación de parámetros, sin alterar la cámara del grafo.
  1. **Propagación de Metadatos de IA (Rostros y Objetos) al Previsualizador desde la Consola de Logs**:
     - Serialización de los metadatos del elemento en el campo `DetailsJson` de cada `StructuredLogRecord` en `WorkflowExecutionContext` y `MockFlowExecutionContext`.
     - `LogViewModel.PreviewLogFile` puebla `FilePreviewContext.Metadata` a partir de `DetailsJson`.
     - `ImagePreviewProvider` decodifica de forma polimórfica los metadatos para resaltar las cajas de rostros y objetos (`AI:FaceBoxes` y `AI:DetectedBoxes`) con sus insignias de conteo y conmutador visual.
  1. **Optimización de Memoria y Concurrencia Thread-Safe en Inferencia ONNX (Detección de Rostros y Objetos)**:
     - Serialización de llamadas nativas a `session.Run(...)` mediante `Lock _inferenceLock`, evitando caídas críticas de DirectML / GPU y violaciones de acceso de memoria bajo procesamiento masivo concurrente.
     - Modo `ExecutionMode.ORT_SEQUENTIAL` e `IntraOpNumThreads` balanceado para evitar contención con el ThreadPool de .NET.
     - Redimensionado in-place (`image.Mutate`) en los nodos de IA que reduce el consumo de RAM de ~75 MB a **0.2 MB - 0.5 MB** por imagen procesada, previniendo pausas de GC y congelamientos de la UI.
  1. **Corrección de Inferencia y Detección de Objetos en ObjectDetectorNode (Tiny YOLOv3 COCO 80)**:
     - Detección de 3 tensores ONNX (`yolonms_layer_1`, `yolonms_layer_1:1`, `yolonms_layer_1:2` / `indices` int32).
     - Mapeo dinámico de entradas `input_1` y `image_shape` evitando el fallo por orden de parámetros en ONNX Runtime.
     - Catálogo de 80 clases COCO alineado (0 = `person`, 1 = `bicycle`...).
     - Metadatos `AI:DetectedBoxes` y renderizado de bounding boxes con badges interactivos en el visor rápido.
  1. **Emisión y Despacho de Logs en Modo Depuración y Pruebas Aisladas**:
     - `MockFlowExecutionContext` en `NodeInspectorViewModel` ahora conecta directamente con `LogViewModel` y `SqliteLogStore`, emitiendo en tiempo real a la consola de ejecución todos los mensajes de log generados durante la prueba aislada del nodo.
     - Ajuste en los niveles de registro de `FaceDetectorNode` y `ObjectDetectorNode` a `LogLevel.Information` y `LogLevel.Warning` para que todos los eventos clave (detección, modelo no disponible, formatos incompatibles o conteo 0) aparezcan inmediatamente en la consola.
  1. **Selección Dinámica de Salidas en Inspector y Carrusel de Previsualización Multisalida**:
     - Al seleccionar cualquier salida en la pestaña "Salidas" del Inspector de Nodos o pulsar su botón directo `👁️ Ver`, el previsualizador abre exactamente esa salida seleccionada con sus metadatos específicos.
     - Se vinculan todas las salidas hermanas (`siblings`) generadas en las pruebas, permitiendo navegar hacia adelante y atrás (`◀ 2 de 5 ▶` o flechas) sin cerrar el visor.
     - Indicador visual y de hover (borde cian `#00E5FF`) en la lista de salidas para identificar con claridad el elemento activo.
  1. **Encuadre Visual de Rostros Detectados en el Previsualizador de Archivos**:
     - `ImagePreviewProvider` ahora dibuja automáticamente recuadros cian neón con badges de porcentaje (`👤 Rostro #1 (95%)`) sobre cada rostro detectado por `FaceDetectorNode`.
     - Soporte interactivo de Zoom y Rotación sincronizados y botón conmutador `👤 Rostros (N)` en la barra inferior del visor para mostrar/ocultar las cajas.
  2. **Corrección de Inferencia Facial en FaceDetectorNode (UltraFace NMS + Softmax)**:
     - Implementación de `FaceBox` con cálculo de IoU y Supresión de No Máximos (NMS con IoU `0.45`), Softmax numéricamente estable y normalización oficial `(pixel - 127)/128` eliminando conteos multiplicados y falsos positivos en el detector de rostros.
  1. **Correcciones UI/XAML y Ciclo de Vida de Proceso**:
     - Declaración de `AddOneConverter` en `<Window.Resources>` de `FilePreviewerWindow.xaml` para evitar fallo durante `InitializeComponent()`.
     - Configuración explícita de `ShutdownMode="OnMainWindowClose"` en `App.xaml`, override `OnClosed` con `Shutdown()` en `MainWindow.xaml.cs` y llamada a `Environment.Exit()` en `App.OnExit` para garantizar que el proceso no quede en segundo plano.
  1. **Gestor y Diálogo de Descarga de Modelos de IA en Ajustes**:
     - Nueva pestaña **`🤖 Modelos de IA`** (`Settings_TabAiModels`) en la ventana de ajustes (`WorkflowSettingsWindow.xaml`) con lista completa de modelos del catálogo, estados de instalación en disco, tamaños e individual/batch download.
     - Diálogo modal dedicado **`AiModelDownloadDialog`** (`AiModelDownloadDialog.xaml` / `.xaml.cs`) invocable desde Ajustes y flujos para descargar todos los modelos faltantes antes de su uso.
     - ViewModel reactivo `AiModelManagerViewModel` con cálculo de espacio en disco, barra de progreso numérico con `IProgress<double>` y cancelación segura.
     - Helper desacoplado `AiModelManager.DownloadModelWithProgressAsync`, `GetModelDiskSizeBytes` y `DeleteModel` para liberación de espacio.
  1. **Visualizador de Archivos Multiformato Integrado (*FileFlow QuickPreviewer*)**: Sistema extensible por proveedores (`IFilePreviewProvider`, `FilePreviewRegistry`) para inspeccionar archivos generados o procesados directamente en la app. Incluye visor de imágenes con zoom/paneo/rotación y comparador "Antes vs Después" con slider interactivo (`ImageCompareSliderControl`), visor de código/texto con `AvalonEdit`, visor de hojas de cálculo con `MiniExcel`, reproductor de audio interactivo y explorador en árbol de archivos comprimidos (`.zip`, `.rar`, `.7z`), con ventana modal flotante QuickLook (`Espacio` / `Esc`) y botón `👁️ Previsualizar` en el Inspector de Nodos y Consola de Logs.
  2. **Plugin de IA Embebida y Visión por Computador (`FileFlow.Plugin.AI`) — Inferencia Real**: Los nodos de IA que eran stubs heurísticos han sido completamente reescritos con inferencia ONNX/Whisper real:
     - `AiModelManager`: Catálogo de modelos con URLs públicas verificadas + descarga automática con progreso en el log del nodo + caché concurrente segura.
     - `OnnxInferenceEngine`: Motor centralizado con caché `Lazy<InferenceSession>` + GPU DirectML + preprocessing NCHW para MobileNetV2 (clasificación), UltraFace (rostros) y Tiny YOLOv3 (objetos COCO).
     - `SmartImageClassifierNode`: Inferencia real MobileNetV2 (14 MB, descarga automática de ONNX Model Zoo).
     - `FaceDetectorNode`: Inferencia real UltraFace RFB 320 (1.2 MB, ONNX Model Zoo).
     - `ObjectDetectorNode`: Inferencia real Tiny YOLOv3 COCO (34 MB, ONNX Model Zoo).
     - `LocalWhisperTranscriberNode`: Inferencia real con Whisper.net + NAudio para conversión de audio (MP3/M4A→WAV 16kHz mono) + SRT con timestamps reales por segmento.
     - `LocalOcrNode`: OCR real con Tesseract 5 + descarga automática de tessdata (ESP/ENG).
     - **Dependencias añadidas**: `NAudio` v2.2.1, `Tesseract` v5.2.0.
  3. **Plugin de Datos, Hojas de Cálculo y Bases de Datos (`FileFlow.Plugin.Data`)**: Nuevos nodos `ExcelReaderNode`, `CsvReaderNode`, `DataLookupNode`, `ExcelReportGeneratorNode`, `CsvExportNode`, `SqliteDatabaseSinkNode` y `DataFormatConverterNode` para procesamiento ETL completo, cruce de datos VLOOKUP, reportes Excel y persistencia SQL.
  4. **Motor DAG & Core - 4 Fases de Mejora Secuencial**:
     - *Fase 1 (Watchdog Mode)*: `FolderWatcherService` multi-directorio continuo con debounce y modo vigilante reactivo (`ToggleWatchModeCommand` y botón `👁️ Vigilante` en UI).
     - *Fase 2 (Bottleneck Heatmap)*: `WorkflowTelemetryTracker` con acumulación atómica O(1) de microsegundos por nodo, ratios de congestión y badges visuales reactivos en tarjetas de nodo (`NodeCardView.xaml`).
     - *Fase 3 (Headless CLI Ampliado)*: `WorkflowCliRunner` con variables dinámicas (`--var`), sobreescritura granular de parámetros (`--param Node.Param=Val`), modo vigilante CLI (`--watch`) y exportación de reportes JSON (`--summary report.json`).
     - *Fase 4 (State Checkpointing & Resumption)*: `WorkflowCheckpointManager` con persistencia en `%LocalAppData%/FileFlowStudio/checkpoints/`, omisión de archivos ya completados en ejecuciones interrumpidas y reanudación automática (`--resume` / `--no-checkpoint`).
  3. **Plugin de Red y Servidores (`FileFlow.Plugin.Network`)**: Nuevos nodos `FtpUploadNode`, `SftpUploadNode`, `SmbCopyNode`, `WebDavUploadNode` y `RemoteDownloadNode` para transferencias seguras y desacopladas hacia servidores FTP/FTPS, VPS Linux vía SSH, carpetas de red local / NAS (SMB UNC) y nubes privadas WebDAV (Nextcloud/ownCloud).
  2. **Categorías Dinámicas y Selector Desplegable Moderno (Dropdown ComboBox)**: Reemplazo del bloque vertical amontonado de botones por un selector `ComboBox` temático compacto de 1 sola fila con iconos, nombres traducidos dinámicamente y badges de conteo en tiempo real (`(N)`). Descubrimiento 100% automático de categorías de plugins (`AvailableCategories`).
  3. **Notas Adhesivas / Sticky Notes**: Modelo `WorkflowAnnotation`, ViewModel `AnnotationViewModel`, tarjeta interactiva `AnnotationCardView` con selector de color, redimensionado y soporte completo de desplazamiento interactivo en el lienzo (`HeaderThumb_DragDelta`).
  4. **Marcos de Agrupación Visual (Group Frames / Boxes)**: Modelo `WorkflowGroup`, ViewModel `GroupViewModel`, vista `GroupCardView`, comando `Ctrl+G`. Estructura desacoplada donde el fondo translúcido interior no bloquea los nodos internos (`IsHitTestVisible="False"`), y la cabecera y `ResizeThumb` son 100% interactivos. Contención espacial dinámica y estricta: solo los nodos cuyo centro esté realmente dentro del marco se mueven con él, desacoplándose inmediatamente si se sacan fuera.
  5. **Ejecutor Headless / CLI Runner**: Módulo `WorkflowCliRunner` en `FileFlow.Core` e integración en `App.xaml.cs` para ejecución desatendida de flujos desde la consola (`--run`, `--input`, `--output`, `--dryrun`, `--silent`).
  6. **Plugin de Documentos y PDFs (`FileFlow.Plugin.Documents`)**: Nodos `PdfMergeNode`, `PdfSplitNode`, `PdfTextExtractorNode` y `PdfMetadataNode` para procesamiento integral de documentos con `PdfSharp` y `PdfPig`.
- **Variable Global de Salida por Defecto (`{GlobalOutputDir}` / `{DefaultOutputDir}`)**:
  - `AppPaths.DefaultGlobalOutputDir` centraliza la resolución de la carpeta de salida tanto en modo instalado como en modo portable.
  - `SystemVariablesResolver` y `VariableTemplateResolver` resuelven transversalmente `{GlobalOutputDir}`, `{DefaultOutputDir}`, `{DefaultGlobalOutputDir}`, `{GlobalOutputPath}`, `{DefaultOutputPath}`, `{GlobalOutput}`, `{DefaultOutput}`, `{OutputDir}` y sintaxis clásica `<GlobalOutputDir>`.
  - Integrado en el catálogo de variables del Inspector (`VariableDiscoveryService`) y del Renombrador Avanzado (`RenamerTagCatalogService`).
- **Reportes de Operaciones en Memoria (`OperationReportNode`) y Ciclo de Vida `OnWorkflowCompletedAsync`**:
  - `OperationReportNode` genera los reportes 100% en memoria (`Metadata["ReportContent"]` y `Metadata["VirtualContent"]`) eliminando la escritura directa a disco y el parámetro `DestinationFolder`.
  - Nuevo hook `OnWorkflowCompletedAsync` en `IFlowNode` y coordinado por `WorkflowExecutor` para emitir el reporte consolidado por el puerto `Report` al concluir el procesamiento de todos los archivos del flujo.
  - `DestinationSinkNode` actualizado con soporte para persistir archivos virtuales/en memoria en disco si el usuario conecta la salida `Report` a un nodo de destino final.
- **Evaluación y Previsualización de Parámetros en Tiempo Real en el Inspector (Enfoque Híbrido)**:
  - En el panel del Inspector (`NodeInspectorPanelView`), los parámetros con expresiones dinámicas (`{RelativeDir}\Output`, `{Year}`, `{FileName}`) muestran un bloque interactivo `⚡ Evaluado:` con el valor real resuelto contra el `FileItemContext` del snapshot en depuración o variables de sistema.
  - Botón de copia al portapapeles (`📋`) y badge `{x}` en la etiqueta del parámetro.
  - Banner en la cabecera de la pestaña de parámetros indicando el contexto de depuración activo (`SelectedSnapshot`).
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
- **Suite Completa de Manuales Oficiales Bilingües (Español e Inglés) y Compilación a PDF**:
  - 📖 **Manual de Usuario y Referencia Técnica**:
    - 🇪🇸 [`docs/manual_de_usuario.md`](file:///docs/manual_de_usuario.md) $\rightarrow$ [`docs/manual_de_usuario.pdf`](file:///docs/manual_de_usuario.pdf) (1001.7 KB)
    - 🇬🇧 [`docs/user_manual.md`](file:///docs/user_manual.md) $\rightarrow$ [`docs/user_manual.pdf`](file:///docs/user_manual.pdf) (1163.5 KB)
  - 📘 **Guía Didáctica para Principiantes**:
    - 🇪🇸 [`docs/manual_usuario_principiantes.md`](file:///docs/manual_usuario_principiantes.md) $\rightarrow$ [`docs/manual_usuario_principiantes.pdf`](file:///docs/manual_usuario_principiantes.pdf) (1110.9 KB)
    - 🇬🇧 [`docs/beginner_user_guide.md`](file:///docs/beginner_user_guide.md) $\rightarrow$ [`docs/beginner_user_guide.pdf`](file:///docs/beginner_user_guide.pdf) (1154.4 KB)
  - 💻 **Manual del Nodo de Scripting (C# & JavaScript)**:
    - 🇪🇸 [`docs/manual_nodo_scripting.md`](file:///docs/manual_nodo_scripting.md) $\rightarrow$ [`docs/manual_nodo_scripting.pdf`](file:///docs/manual_nodo_scripting.pdf) (1007.9 KB)
    - 🇬🇧 [`docs/scripting_node_manual.md`](file:///docs/scripting_node_manual.md) $\rightarrow$ [`docs/scripting_node_manual.pdf`](file:///docs/scripting_node_manual.pdf) (1003.4 KB)
  - **Despacho Reactivo según Idioma**: La UI (`ControlBarViewModel` y `ScriptStudioWindow`) abre automáticamente el documento PDF correspondiente al idioma activo (`LocalizationManager.Instance.CurrentLanguage`).
  - **Instalador y Releases**: Accesos directos condicionales en Inno Setup (`FileFlow.iss`) y publicación de los 6 PDFs en GitHub Releases.
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
- **Filtrado por Extensión en Carpeta Origen (`FolderSourceNode`)**:
  - Incorporado el parámetro `ExtensionFilter` (descriptor de texto, orden 2) en `FileFlow.Plugin.FileSystem/FolderSourceNode.cs`.
  - Soporte de sintaxis flexible con múltiples delimitadores (comas, puntos y comas, barras verticales, espacios) y formatos (`*.jpg, *.png`, `.zip; .rar`, `pdf docx`). Comodines `*` o `*.*` / vacío aceptan todos los archivos.
  - Conteo asíncrono y streaming de emisión optimizados para filtrar en una sola pasada.
  - Claves bilingües añadidas en diccionarios `Strings.resx` y `Strings.es.resx` (`Param_ExtensionFilter`).
- **Descentralización Total de Recursos (.resx / i18n) por Plugin (Zero-Touch en FileFlow.App)**:
  - `PluginLoader.cs`: Auto-descubrimiento dinámico de manifiestos `.resources` y clases `Strings.ResourceManager` en todos los ensamblados de plugin cargados, registrándolos automáticamente en `LocalizationManager.Instance` sin necesidad de tocar `FileFlow.App`.
  - `IPluginInitializer` en `FileFlow.Sdk.Plugins`: Contrato opcional para que los plugins ejecuten rutinas de inicialización personalizada al cargarse.
  - `LocalizationManager.cs`: Implementado con protección multihilo mediante `System.Threading.Lock` de .NET 9.
  - Diccionarios `.resx` co-ubicados dentro de la carpeta `Resources/` de cada plugin (`FileFlow.Plugin.FileSystem`, `FileFlow.Plugin.Archives`, `FileFlow.Plugin.Integrations`), manteniendo `FileFlow.App/Resources/` enfocado exclusivamente en la UI anfitriona.
- **Localización Dinámica y Reactiva al 100% en Toda la Interfaz Gráfica e i18n Completa (Zero Hardcoded Strings)**:
  - `MainWindow.xaml`: Menú lateral de navegación (Drawer) completamente bilingüe (`GESTIÓN DE FLUJOS` / `FLOW MANAGEMENT`, `Nuevo Flujo` / `New Workflow`, `Cargar Flujo...` / `Load Workflow...`, `Guardar Flujo...` / `Save Workflow...`, `APARIENCIA E IDIOMA` / `APPEARANCE & LANGUAGE`, `PANELES Y HERRAMIENTAS` / `PANELS & TOOLS`, `AYUDA Y RECURSOS` / `HELP & RESOURCES`, etc.) con refresco reactivo instantáneo.
  - `ControlBarView.xaml`: Tooltips localizados dinámicamente mediante `LocalizationManager.Instance`.
  - `NodeToolboxView.xaml`: Filtros de categorías (`Category_All`, `Category_Favorites`, `Category_Frequent`, `Category_FileSystem`, `Category_Archives`, `Category_MediaDocs`, `Category_Metadata`, `Category_Logic`, `Category_Integrations`), botón y tooltips de vista compacta (`Toolbox_CompactBtn`, `Toolbox_ToggleCompactToolTip`) y tooltip de favoritos (`Toolbox_FavoriteToolTip`).
  - `NodeInspectorPanelView.xaml`: Pestañas de Parámetros, Salidas, Entradas, Diff y Trazabilidad (`Inspector_Tab*`), encabezados y subencabezados de sección, etiquetas de puertos (`Inspector_InputsPortLabel`, `Inspector_OutputsPortLabel`), columnas de la tabla de diferencias de metadatos (`Inspector_ColKey`, `Inspector_ColStatus`, `Inspector_ColNewValue`, `Inspector_ColOldValue`), metadatos del archivo inspeccionado y botones de acción rápida (`Inspector_CloseBtn`, `Inspector_TestBtn`).
  - `WorkflowSettingsWindow.xaml`: Todas las pestañas (`Settings_TabStorage`, `Settings_TabAppearance`, `Settings_TabPerformance`, `Settings_TabExternalTools`), título de ventana, descripciones de opciones (rutas de salida, colisiones, temas, rendimiento multihilo, niveles de log y rutas de ejecutables de sistema) y botones (`Settings_SaveBtn`, `Settings_BrowseBtn`, `Settings_AutoDetectBtn`, `Settings_CustomizeThemesBtn`).
  - `ThemeCustomizerWindow.xaml`: Título, subtítulo, encabezados de grupos de configuración (Información General, Fondos y Superficies, Colores de Acento y Estados, Textos y Bordes, Gradiente de Cables, Tipografía), controles de fuentes/radios, vista previa interactiva y botones de acción (`ThemeCustomizer_NewBtn`, `ThemeCustomizer_DuplicateBtn`, `ThemeCustomizer_DeleteBtn`, `ThemeCustomizer_TestInApp`, `ThemeCustomizer_SaveAndApply`).
  - `LogView.xaml`: Tooltips de control de consola (`Log_ClearSearchToolTip`, `Log_ToggleLiveToolTip`, `Log_ExportToolTip`, `Log_ClearToolTip`) y botones de detalles (`Log_TraceabilityBtn`, `Log_CopyJsonBtn`).
  - `UserPreferencesService`: Persistencia del idioma seleccionado por el usuario en `user_preferences.json` (`Language: "es-ES"` / `"en-US"`), restaurando la preferencia guardada en el arranque de la aplicación (`App.xaml.cs`).
  - `NodeParameterViewModel.DisplayName`: Mapeo y traducción reactiva de los parámetros de los 27 nodos del sistema (`Width` $\rightarrow$ `Ancho` / `Width`, `Quality` $\rightarrow$ `Calidad` / `Quality`, `DestinationRoot` $\rightarrow$ `Carpeta Destino` / `Destination Folder`, etc.) manteniendo las claves técnicas de código en inglés.
  - `LocalizationManager.cs`: Notificación `OnPropertyChanged("Item[]")` y `OnPropertyChanged("Item")` para refrescar instantáneamente todos los bindings XAML en caliente sin reiniciar la aplicación.
  - Diccionarios completos de recursos en español e inglés (`Strings.resx` y `Strings.es.resx`) 100% sincronizados y sin entradas duplicadas.
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

