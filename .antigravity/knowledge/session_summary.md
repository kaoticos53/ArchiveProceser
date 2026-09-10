# Resumen Consolidado de Sesiones y Memoria de Proyecto - FileFlow Studio

Este documento se actualiza al finalizar cada sesión de trabajo para consolidar los puntos clave, decisiones arquitectónicas, capacidades del sistema y el estado de la solución, evitando empezar desde cero en futuras conversaciones.

---

## 1. Estado Actual del Repositorio y Calidad
- **Target Framework**: `.NET 9` (`net9.0` / `net9.0-windows` para WPF UI) con preparación para .NET 10.
- **Lenguaje**: `C# 13` (`<LangVersion>13</LangVersion>`), Nullable activado de forma estricta.
- **Estado de Compilación**: `dotnet build FileFlow.slnx --warnaserror` $\rightarrow$ **0 Advertencias, 0 Errores**.
- **Suite de Pruebas**: `.\test.ps1` / `dotnet test` → **753 / 753 Pruebas Pasadas con 100% de Éxito**.
- **Nuevas Funcionalidades y Correcciones Implementadas en Sesión**:
  --60. **Arquitectura de Adaptadores y Motor In-Process para `MultimodalVisionLlmNode` (`IVlmAdapter`)**:
      - **Motivación**: Brindar la opción de elegir entre servidores externos (LM Studio, Ollama, API OpenAI) y un motor interno 100% in-process dentro de FileFlow Studio sin necesidad de dependencias externas ni procesos en segundo plano.
      - **Solución Implementada**:
        1. *Patrón Adaptador (`IVlmAdapter`, `VlmExecutionRequest`, `VlmAdapterFactory`)*: Desacopla la invocación del modelo VLM en adaptadores intercambiables. `OpenAiCompatibleVlmAdapter` para HTTP y `InProcessVlmAdapter` para ejecución autónoma local.
        2. *Motor In-Process (`InProcessVlmAdapter`)*: Inspecciona características de geometría visual con `ImageTypeAnalyzerEngine` (contraste bimodal, densidad de texto, relación de aspecto) e integra texto OCR o léxico documental con `LanguageInferenceEngine` para resolver tareas visuales (`ExtractInvoiceReceiptJson`, `DocumentOcrAndSummary`, `TranslateDocument`, `ClassifyAndTag`, `QualityInspection`, `CustomPrompt`).
        3. *Ciclo de Vida y Zero-Touch (`IModelLifecycleNode`)*: Implementado en `MultimodalVisionLlmNode` para inspeccionar y precargar/descargar recursos. Opción `"Internal Engine (In-Process)"` añadida en el parámetro `Provider`.
        4. *Pruebas Unitarias*: 9 tests añadidos en `MultimodalVisionLlmNodeTests.cs` evaluando la factoría de adaptadores, la extracción de facturas a JSON in-process, la clasificación visual in-process, el resumen documental y el ciclo de vida.
      - **Validación**: 753 / 753 pruebas superadas (100%), compilación con `--warnaserror` con 0 advertencias y 0 errores.
  --59. **Nuevo Nodo de IA Multimodal `MultimodalVisionLlmNode` y Motor Cliente `MultimodalVlmClientEngine` (Qwen2.5-VL / LM Studio / Ollama)**:
      - **Motivación**: Dotar a FileFlow Studio de capacidades multimodales avanzadas de visión y lenguaje con resolución dinámica (Dynamic Resolution ViT), superando el OCR tradicional y la clasificación ciega de CLIP.
      - **Solución Implementada**:
        1. *Motor Cliente HTTP Resiliente (`MultimodalVlmClientEngine`)*: Preprocesado de imagen en memoria con ImageSharp (downscale bicúbico a 1536 px y compresión JPEG 85% a Base64 URI), conexión a servidores locales (LM Studio en `localhost:1234` u Ollama en `localhost:11434`) vía OpenAI Chat Completions Multimodal (`image_url`), sanitización de bloques JSON y 6 presets de tareas (`ExtractInvoiceReceiptJson`, `DocumentOcrAndSummary`, `TranslateDocument`, `ClassifyAndTag`, `QualityInspection`, `CustomPrompt`).
        2. *Nodo de Flujo (`MultimodalVisionLlmNode`)*: 3 puertos de salida (`Out`, `Structured` para JSON extraído y `Error`). Inyección de metadatos `AI:VlmResponse`, `AI:VlmJson`, `AI:VlmTokens`, etc.
        3. *Resolución de Recursos LIFO*: Optimizado `LocalizationManager.cs` a búsqueda inversa (LIFO) garantizando la precedencia absoluta de los recursos autónomos de plugins.
        4. *Pruebas Unitarias*: 8 pruebas en `MultimodalVisionLlmNodeTests.cs` evaluando codificación, mock HTTP, extracción JSON y manejo de errores.
      - **Validación**: 744 / 744 pruebas superadas (100%), 0 advertencias y 0 errores.
  --58. **Nuevo Nodo Especializado `ImageTypeClassifierNode` y Motor de Visión Estructural e IA (`ImageTypeAnalyzerEngine`)**:
      - **Motivación**: La limitación arquitectónica de modelos multimodales zero-shot generalistas (como CLIP ViT-B/32) sobre imágenes escaneadas de documentos/recibos, debido al downsampling destructivo a 224x224 que desvanece el texto y a los márgenes estrechos de similitud de coseno, impedía una clasificación determinista y fiable.
      - **Solución Implementada**:
        1. *Motor Determinista de Visión y Análisis Estructural (`ImageTypeAnalyzerEngine`)*: Análisis en un único pase de píxeles combinando detección de luminancia y fondo claro (>60%), densidad de transiciones de texto horizontal, ratios de aspecto estándar (A4/Carta, ID-1, tickets verticales > 1.8, 16:9), paleta de colores planos (ilustraciones/gráficos), metadatos EXIF fotográficos reales y detección facial neuronal con UltraFace RFB-320 (distinción inequívoca de retratos y fotos grupales).
        2. *Nodo de Flujo con Enrutamiento Directo Multi-Puerto (`ImageTypeClassifierNode`)*: 11 puertos de salida (`Document`, `Receipt`, `Portrait`, `GroupPhoto`, `Photo`, `Screenshot`, `Illustration`, `IDCard`, `Other`, `Out`, `Error`). Inyección de metadatos `AI:ImageType`, `AI:ImageTypeConfidence`, `AI:ImageTypeScoresJson`, `AI:HasFaces`, etc.
        3. *Localización Multilingüe Autónoma (Zero-Touch)*: Claves añadidas exclusivamente a `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx`.
        4. *Pruebas Unitarias*: 8 pruebas unitarias en `ImageTypeClassifierNodeTests.cs` evaluando documentos sintéticos, tickets térmicos, capturas de pantalla, umbrales y motor directo.
      - **Validación**: 736 / 736 pruebas superadas (100%), 0 advertencias y 0 errores.
  --57. **BUGFIX CRÍTICO: Fallo en Inferencia ONNX de CLIP ViT-B/32 y Clasificación Semántica Zero-Shot en Imágenes (`SemanticEmbeddingEngine`)**:
      - **Causa Raíz Descubierta**:
        1. *Desajuste de Tensores en Grafo Multimodal*: El grafo ONNX de CLIP (`clip-vit-base-patch32.onnx`) requiere simultáneamente tensores `pixel_values` (Single [1, 3, 224, 224]), `input_ids` (Int64 [1, seq_len]) y `attention_mask` (Int64 [1, seq_len]). En `GetImageEmbedding`, el código anterior asignaba el tensor de píxeles a `session.InputNames[0]` (que en CLIP es `input_ids` de tipo Int64), arrojando la excepción silenciosa `[ErrorCode:InvalidArgument] Tensor element data type discovered: Float metadata expected: Int64`. El bloque `catch` tragaba la excepción y caía al fallback léxico de 384 dimensiones basado en el nombre del archivo.
        2. *Selección de Salida Errónea en Texto*: En `GetTextEmbedding`, se extraía `outputs.First()`, que en CLIP corresponde a `logits_per_image` (un escalar 1x1) en lugar de `text_embeds` (512 dimensiones).
        3. *Similitud Coseno Nula (0.0)*: `CosineSimilarity` recibía un vector léxico de 384 dimensiones y un vector de 1 dimensión, devolviendo `0.0` por discrepancia de longitudes (`vecA.Length != vecB.Length`).
        4. *Alineación de Vocabulario y Conceptos en Español*: CLIP de OpenAI fue preentrenado con textos en inglés. Al consultar etiquetas en español como `"documento, foto, retrato"`, no existía mapeo hacia los tokens BPE nativos de CLIP.
      - **Ajustes Realizados**:
        1. *Inferencia Multimodal Robusta en `GetImageEmbedding`*: Detección de grafos CLIP con `pixel_values` e inyección correcta de tensores tipados: píxeles normalizados RGB 224x224, tokens dummy (`49406L`, `49407L`) y máscara de atención, extrayendo y normalizando el tensor `image_embeds` de 512 dimensiones.
        2. *Inferencia Multimodal en `GetTextEmbedding`*: Inyección de `input_ids`, `attention_mask` y tensor de ceros para `pixel_values`, extrayendo el tensor `text_embeds` de 512 dimensiones.
        3. *Traductor y Tokenizador Especializado para CLIP*: Integrado `TranslateConceptToEnglish` con diccionario de conceptos visuales/documentales frecuentes (`documento`, `factura`, `recibo`, `retrato`, `paisaje`, `pantallazo`, etc.) con fallback a `PromptTranslator.TranslateSegment`, y tokenizador BPE con vocabulario CLIP (`ClipVocab`).
      - **Validación**: Pruebas automáticas con el modelo real descargado en `ClipModel_Diagnostic_Test` evaluando imágenes sintéticas contra etiquetas en inglés y español con similitud coseno positiva real (> 0.25). `dotnet test` $\rightarrow$ **728 / 728 pruebas superadas al 100% (0 errores, 0 advertencias)**.
  --56. **BUGFIX: ZeroShotSemanticSearchNode — `IsQueryMatch` siempre `false`**:
      - **Causa Raíz**: Dos bugs acumulados en `ZeroShotSemanticSearchNode.cs`:
        1. *Bypassing de `IStorageService`*: Se usaba `File.Exists(item.CurrentPath)` directamente, rompiendo el modo VFS/virtual.
        2. *Embedding sobre la ruta del archivo*: Se pasaba `item.CurrentPath` (ej. `C:\Temp\invoice.txt`) a `SemanticEmbeddingEngine.ClassifyZeroShot`. En el fallback léxico (sin modelo ONNX), el embedding se calculaba sobre la ruta del archivo —texto sin valor semántico— produciendo similitud de coseno ≈ 0 contra cualquier query o label, y por tanto `IsQueryMatch = false` siempre.
      - **Corrección Aplicada** (`ZeroShotSemanticSearchNode.cs`):
        1. Reemplazado `File.Exists` por `await storage.FileExistsAsync()` vía `context.GetStorage()` (compatible con VFS).
        2. Añadido método privado `ResolveContentForEmbeddingAsync` que lee el contenido real del archivo de texto (hasta 2000 chars) vía `IStorageService.OpenReadAsync()` y lo pasa al engine. Para imágenes físicas pasa la ruta real (CLIP ONNX puede leerla); para imágenes VFS o binarios usa el nombre base del archivo como texto representativo.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` → 0 advertencias, 0 errores. `dotnet test` → **727 / 727 pruebas superadas al 100%**.
  --55. **FASE 8: Transformación del Diseñador de Datos Sintéticos a Explorador de Archivos en Árbol Jerárquico (Hierarchical TreeView Explorer)**:
      - **Objetivo**: Rediseñar la vista principal del Diseñador de Conjuntos de Datos Sintéticos (`SyntheticDataSetDesignerWindow`) para que los archivos y carpetas se visualicen como un árbol jerárquico anidado interactivo (estilo explorador de archivos de sistema operativo), reemplazando la vista de lista plana y permitiendo agregar, organizar, inspeccionar y manipular subcarpetas, archivos y paquetes comprimidos simulados.
      - **Ajustes Realizados**:
        1. *Modelo Jerárquico Observable y ViewModel (`SyntheticTreeNodeItem`, `SyntheticDataSetDesignerViewModel`)*:
           - Creado `SyntheticTreeNodeItem.cs` con propiedades `Children`, `Parent`, `IsExpanded`, `IsSelected`, iconografía contextual `IconGlyph` (🎬, 🎵, 🖼️, 📄, 📊, 📦, 💾, 📜, 📁), badges de peso recursivo y conteo de archivos.
           - Implementados métodos bidireccionales `BuildTreeFromItems()` y `SyncItemsFromTree()` con soporte completo para directorios vacíos, metadata tipada y entradas de archivos ZIP simulados.
           - Añadidos comandos de manipulación de árbol: `AddFileToTreeCommand`, `AddFolderToTreeCommand`, `AddArchiveToTreeCommand`, `RemoveTreeNodeCommand`, `ExpandAllTreeCommand` y `CollapseAllTreeCommand`.
        2. *Interfaz de Usuario WPF (`SyntheticDataSetDesignerWindow.xaml`)*:
           - Diseñada interfaz en 2 columnas: `TreeView` jerárquico con `HierarchicalDataTemplate` a la izquierda y Panel de Inspección y Edición de propiedades a la derecha.
           - Creado `InverseBooleanToVisibilityConverter` en `FileSystemUiConverters.cs`.
           - Sincronización transparente con las pestañas de DSL de Árbol Rápido y JSON Crudo.
        3. *Localización i18n y Pruebas Unitarias*:
           - Actualizados `Strings.resx` y `Strings.es.resx` con claves para pestañas, comandos e inspectores.
           - Creadas pruebas unitarias en `SyntheticDataSetDesignerViewModelTests.cs` evaluando anidamiento, inserción contextual y sincronización.
      - **Validación**: Compilación estricta con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` en 0 advertencias / 0 errores, y `dotnet test` $\rightarrow$ **652 / 652 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --54. **FASE 7: Auditoría QA Integral y Corrección Sistemática de 21 Hallazgos (Críticos, Altos, Medios y Bajos)**:
      - **Objetivo**: Ejecutar un análisis y depuración exhaustiva en todo el repositorio (SDK, Core, 11 Plugins, App, Tests) cubriendo errores lógicos/runtime, seguridad y validación, concurrencia, y fugas de recursos.
      - **Ajustes Realizados**:
        1. *Seguridad y Validación (Críticos)*:
           - `CRIT-01` & `CRIT-03` (`SqliteDatabaseSinkNode`): Sanitización estricta por regex (`^[a-zA-Z_]\w{0,127}$`) del parámetro `TableName` previniendo inyecciones SQL. Reemplazado `HashSet<string>` estático por `ConcurrentDictionary<string, bool>` para eliminar data race en double-check locking.
           - `CRIT-02` & `LOW-01` (`RoslynCSharpEngine`): Añadido timeout de seguridad (60s) enlazado al `CancellationToken` para ejecución de scripts y política de expulsión LRU con capacidad máxima de 256 scripts en caché.
        2. *Concurrencia, Recursos y Red (Altos)*:
           - `HIGH-01` (`PdfMergeNode`): Sobrecarga síncrona `MergePdfFiles` marcada con `[Obsolete(..., true)]` lanzando `NotSupportedException`, erradicando el riesgo de deadlock por sync-over-async.
           - `HIGH-02`, `HIGH-05` & `LOW-04` (`HttpTransportStrategy`, `WebDavTransportStrategy`, `SmbTransportStrategy`, `SftpTransportStrategy`): Refactorizado `HttpClient` a singleton estático compartido con `SocketsHttpHandler` (pooling de 15m, idle 2m, connect 30s) evitando agotamiento de sockets (socket exhaustion). Migradas todas las operaciones de archivos y verificación a `IStorageService` (`context.GetStorage()`). Sanitizado el timestamp fallback con `CultureInfo.InvariantCulture`.
           - `HIGH-03` (`AdvancedRenamerEditorViewModel`): Reemplazado el bloqueo sobre diccionario público `lock(_node.Parameters)` por un objeto `Lock` privado y dedicado (`_parameterSyncLock`).
           - `HIGH-04` (`FolderSourceNode`): Añadido `.ContinueWith` con control de excepciones y registro de diagnóstico en el fallback en background (`Task.Run`) para pre-conteo de archivos.
           - `HIGH-06` (`AiModelManagerViewModel`, `LogViewModel`): Reemplazado `Dispatcher.Invoke` síncrono bloqueante por `Dispatcher.InvokeAsync` en callbacks de progreso y flushing de logs en vivo.
        3. *Resiliencia, Diálogos y Robustez (Medios y Bajos)*:
           - `MED-01` (`AdvancedRenamerEditorWindow`, `SyntheticDataSetDesignerWindow`): Añadido registro diagnóstico de excepciones en `InitializeComponentSafe` para fallbacks de carga XAML.
           - `MED-03` (`SystemPerformanceMonitor`): Filtradas excepciones críticas (`OutOfMemoryException`, `StackOverflowException`) en el handler de temporizador `OnTimerTick`.
           - `MED-06` & `LOW-03` (`AiModelDownloader`): Erradicado el busy-wait con bucle de polling mediante compartición concurrente de tareas con `ConcurrentDictionary<string, Task<string?>>`. Añadido logging de excepciones en bloques `catch` de limpieza temporal y reemplazo atómico de modelos.
           - `LOW-02` (`SftpTransportStrategy`): Eliminadas llamadas redundantes a `client.Disconnect()` previas al `Dispose` del `using`.
           - `LOW-05` (`FlowSchedulerService`): Sellada la clase (`public sealed class FlowSchedulerService : IDisposable`).
      - **Validación**: Compilación estricta con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` en 0 advertencias / 0 errores, y `dotnet test` $\rightarrow$ **646 / 646 pruebas superadas al 100% (0 errores, 0 omitidas)**.
      - **Objetivo**: Eliminar bloqueos de sincronización sobre asincronía (`.GetAwaiter().GetResult()`), promover la interfaz canónica `IDialogService` al SDK para permitir testing sin WPF, y desacoplar en ViewModels limpios los tres diálogos restantes con alta densidad de code-behind (`AiModelUrlsConfigDialog`, `VariablePickerWindow`, `TextEditorDialogWindow`).
      - **Ajustes Realizados**:
        1. *Sub-fase 6A (Sync-Over-Async)*:
           - `SafeArchiveExtractor.cs`: Eliminados wrappers síncronos `GetPasswordCandidates` y `ExtractNestedArchives`. Migrados a llamadas asíncronas no bloqueantes.
           - `MediaTranscoderNode.cs`: Sustituido `CanExecuteCommand(...).GetAwaiter().GetResult()` por `await CanExecuteCommandAsync(...)`.
           - `PdfMergeNode.cs`: Marcado obsoleto `MergePdfFiles` y migrado a `MergePdfFilesAsync`.
           - `IExternalToolsService.cs` y `IMediaTranscoderService.cs`: Añadido `IsToolAvailableAsync` e `IsAvailableAsync` en contratos e implementaciones de Core.
        2. *Sub-fase 6C (Generalización de IDialogService y Reemplazo de MessageBox.Show)*:
           - Creados `IDialogService`, `DialogResult` y `NullDialogService` en `FileFlow.Sdk.Services`.
           - Enlazado `WpfDialogService` en `FileFlow.App` a la interfaz de SDK y definidos type-forwarders globales.
           - Inyectado `IDialogService` en ~30 sitios de llamada en ViewModels y code-behinds sustituyendo llamadas directas a `MessageBox.Show`.
        3. *Sub-fase 6B (Clean MVVM en 3 Diálogos)*:
           - `AiModelUrlsConfigDialog`: Creado `AiModelUrlsConfigViewModel.cs` (gestión de URLs, health check concurrente, validación). 4 tests en `AiModelUrlsConfigViewModelTests.cs`.
           - `VariablePickerWindow`: Creado `VariablePickerViewModel.cs` (aplanado de variables, categorización, filtrado reactivo). Tests en `VariablePickerAndIntelliSenseTests.cs`.
           - `TextEditorDialogWindow`: Creado `TextEditorDialogViewModel.cs` (estadísticas en vivo, live preview con `VariableTemplateResolver`, motor de autocompletado `{query` e inserción). 8 tests en `TextEditorDialogViewModelTests.cs`.
      - **Validación**: Compilación estricta con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` en 0 advertencias / 0 errores, y `dotnet test` $\rightarrow$ **641 / 641 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --52. **FASE 5: Universal Storage Service (IStorageService), Canonical Constants, Clean MVVM Settings and Async Cancellation Hardening**:
      - **Objetivo**: Erradicar los últimos bipaseos de I/O físico directo en nodos de flujo, centralizar constantes canónicas de puertos y metadatos en `FileFlow.Sdk`, desacoplar la ventana de configuración en `WorkflowSettingsViewModel` eliminando servicios wrapper redundantes y reforzar la propagación de cancelación asíncrona.
      - **Ajustes Realizados**:
        1. *Erradicación Total de Bipaseos de I/O Físico*:
           - `DeduplicationFilterNode`: Migrado a `await storage.FileExistsAsync()` y `await storage.OpenReadAsync()`.
           - `VersionRouterNode`, `SwitchActiveFileNode`, `IntermediateCleanupNode`, `FileForkNode`, `BestVersionSelectorNode`: Migrados a `IStorageService` para comprobaciones, lecturas de tamaño y purgas no destructivas.
           - `MediaTranscoderNode`: Migrado a `await storage.CreateDirectoryAsync()`, `await storage.CopyAsync()` y `await storage.GetFileSizeAsync()`.
           - `NetworkDownloadNode`: Migrada la creación del directorio de destino a `await storage.CreateDirectoryAsync()`.
        2. *Constantes Canónicas en `FileFlow.Sdk`*:
           - Introducidos `WellKnownPorts` y `WellKnownMetadataKeys` en `FileFlow.Sdk.Common`, sustituyendo cadenas mágicas sueltas por constantes fuertemente tipadas en SDK y plugins.
        3. *Desacoplamiento Clean MVVM para Ajustes (`WorkflowSettingsViewModel`)*:
           - Creado `WorkflowSettingsViewModel` en `FileFlow.App.ViewModels`, eliminando ~270 líneas de code-behind en `WorkflowSettingsWindow.xaml.cs` y enlazando propiedades y comandos vía XAML.
           - Eliminado `FileFlow.App.Services.ExternalToolsService.cs` redundante y promovido `ExternalToolsConfig` a `FileFlow.Sdk.Services`, ampliando el contrato `IExternalToolsService`.
           - Creada suite de pruebas `WorkflowSettingsViewModelTests.cs` (5 pruebas).
      - **Validación**: Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia (0 advertencias, 0 errores) y `dotnet test` $\rightarrow$ **627 / 627 pruebas superadas al 100%**.
  --51. **FASE 4: Organización, Encapsulación y Convenciones de Solución (Clean Architecture & C# 13)**:
      - **Objetivo**: Concluir la auditoría arquitectónica integral asegurando el encapsulamiento estricto de las capas internas de los plugins, devirtualización de clases terminales mediante `sealed` en C# 13 y sincronización moderna de concurrencia con `System.Threading.Lock` de .NET 9.
      - **Ajustes Realizados**:
        1. *Encapsulación Estricta de Adaptadores y Renderers*:
           - Convertidas todas las interfaces y clases de adaptadores de inferencia en `FileFlow.Plugin.AI` (`ISuperResolutionAdapter`, `IObjectDetectorAdapter`, `IImageClassifierAdapter`, `IFaceDetectorAdapter`, `IBackgroundRemoverAdapter` y sus 8 implementaciones concretas) a `internal` e `internal sealed`, junto con sus fábricas a `internal static`.
           - Convertida la interfaz `IReportRenderer` y sus 5 implementaciones concretas en `FileFlow.Plugin.FileSystem` a `internal` e `internal sealed`.
        2. *Sellado Sistemático de Nodos de Pipeline (`sealed class`)*:
           - Selladas todas las clases concretas de nodos de flujo (`IFlowNode`) en los 11 plugins de la solución (AI, FileSystem, Archives, Images, Documents, Data, Logic, Scripting, Network, Integrations, Hashing), permitiendo al compilador JIT devirtualizar llamadas y optimizar el despacho en caliente.
        3. *Sincronización Concurrente con `System.Threading.Lock`*:
           - Reemplazados bloqueos sobre `Parameters` en `VariableInjectorNode` y `SmartUnpackNode` por instancias privadas `Lock _lock = new()`.
      - **Validación**: Compilación limpia bajo `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (0 advertencias, 0 errores) y `dotnet test` $\rightarrow$ **622 / 622 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --50. **FASE 2 & FASE 3: Desacoplamiento e Inyección de Dependencias (DI), Abstracción de I/O Universal y Consolidación Arquitectónica**:
      - **Objetivo**: Ejecutar los planes aprobados en la auditoría estática, eliminando el bipaseo restante de `IStorageService` en nodos de IA (visión, lenguaje y audio), deduplicando lógica de carga de imágenes e inyectando dependencias formalmente en los ViewModels de la capa de presentación.
      - **Ajustes Realizados**:
        1. *Abstracción Total de I/O (`IStorageService`) en Nodos de IA ([CRIT-02])*:
           - Centralizado en `AiFlowNodeBase` el método protegido `LoadInputRgb24ImageAsync` para abrir y decodificar streams asíncronos con `storage.OpenReadAsync()`.
           - Migrados `FaceDetectorNode`, `ObjectDetectorNode`, `PromptObjectDetectorNode`, `SmartImageClassifierNode` y `ContentModerationFilterNode` a operaciones basadas en streams de almacenamiento, eliminando acoplamiento al disco físico y llamadas a `File.Exists`.
           - `LocalOcrNode`: Lectura de bytes desde stream de almacenamiento y pasaje en memoria a `Pix.LoadFromMemory(imageBytes)`, permitiendo OCR en archivos virtuales y en memoria.
           - `VoiceActivityDetectorNode`: Verificación de archivo migrada a `await storage.FileExistsAsync()`.
        2. *Desacoplamiento e Inyección de Dependencias en UI ([MED-02])*:
           - Registrado `AiModelManagerViewModel` en el contenedor de inversión de control (`ServiceCollectionExtensions.cs`).
           - Inyectado `ILocalizationService` en `AiModelManagerViewModel` con fallback a `LocalizationManager.Instance`.
           - Inyectado y resuelto `AiModelManagerViewModel` en `WorkflowSettingsWindow` mediante DI.
           - Inyectados `IUserPreferencesService` e `ILocalizationService` en `ToolboxViewModel`, eliminando llamadas duras a Singletons durante el refresco del panel de herramientas.
      - **Validación**: Compilación limpia bajo `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (0 advertencias, 0 errores) y `dotnet test` $\rightarrow$ **622 / 622 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --49. **FASE 2: Auditoría de Software y Refactorización Integral - Desacoplamiento, Inyección de Dependencias (DI) e I/O Universal (`IStorageService`)**:
      - **Objetivo**: Ejecutar la refactorización arquitectónica aprobada en la auditoría estática (Fase 1), solventando los hallazgos críticos de bipaseo de I/O, acoplamiento fuerte UI-Plugins, ausencia de abstracción en invocación de procesos externos, vulnerabilidades en librerías y duplicidad de código.
      - **Ajustes Realizados**:
        1. *Seguridad y Limpieza de Dependencias (Bloque 2.1)*:
           - Actualizado `SSH.NET` a `2026.0.0` en `FileFlow.Plugin.Network`, mitigando la vulnerabilidad `GHSA-q939-rpr3-3284` y eliminando supresiones de advertencias.
           - Eliminadas referencias innecesarias a `CommunityToolkit.Mvvm` en `FileFlow.Plugin.Archives` y `FileFlow.Plugin.Integrations`.
           - Eliminadas clases/interfaces redundantes muertas `FileFlow.Core/Engine/IFileRecycler.cs` y `FileFlow.Core/Storage/VirtualStorageService.cs`.
           - Subsanada advertencia CA2024 en `TextCodePreviewProvider.cs`.
        2. *Abstracción de Procesos Externos (`IProcessRunner`) (Bloque 2.2)*:
           - Diseñado `IProcessRunner` e implementado `ProcessRunner` en `FileFlow.Sdk.Platform`.
           - Incorporado `context.ProcessRunner` en `IFlowExecutionContext` e inyectado en `ServiceCollectionExtensions`.
           - Refactorizados `CliExecutionNode`, `MediaTranscoderNode`, `ExternalToolsService`, `FallbackPreviewProvider` y `FilePreviewerViewModel`.
           - Creada suite `ProcessRunnerTests.cs` (5 tests).
        3. *Desacoplamiento Estricto UI - Plugins (Bloque 2.3)*:
           - Trasladado `ThemeDefinition.cs` a `FileFlow.App.Themes`, preservando la pureza de `FileFlow.Sdk`.
           - Centralizado el autodescubrimiento de plugins en `PluginRegistryHelper.cs`.
           - Eliminada la instanciación redundante en `MainWindow.xaml` usando `d:DataContext`.
           - Creado `ISwitchCaseNode` en SDK desacoplando `NodeSwitchCaseCoordinator`.
           - Creado `ModelSessionRegistry` en SDK desacoplando `StatusBarViewModel` y `WorkflowExecutionCoordinator` de `FileFlow.Plugin.AI`.
           - Desacoplado el diseñador de datasets sintéticos en `ControlBarViewModel`.
        4. *Migración Masiva a `IStorageService` y Unificación de Plantillas (Bloque 2.4 - [CRIT-01])*:
           - Refactorizado `NetworkTemplateHelper` para utilizar `VariableTemplateResolver.Resolve` y añadidos alias de tokens en `SystemVariablesResolver`.
           - Incorporados `GetCreationTimeAsync`, `GetLastWriteTimeAsync` y `OpenAppendAsync` en `IStorageService`.
           - Implementado `DefaultPhysicalStorageService` en SDK como fallback seguro para contextos de ejecución sin mocks explícitos.
           - Migrados todos los nodos de pipeline restantes a `context.GetStorage()`:
             - Images: `ImageOptimizerNode`, `ExifMetadataNode`.
             - Documents: `PdfTextExtractorNode`, `PdfSplitNode`, `PdfMetadataNode`, `PdfMergeNode`.
             - Data: `ExcelReaderNode`, `CsvReaderNode`, `DataFormatConverterNode`, `CsvExportNode`, `ExcelReportGeneratorNode`, `SqliteDatabaseSinkNode`, `DataLookupTableLoader`, `DataLookupNode`.
             - Archives: `ArchiveCompressorNode`, `SmartUnpackNode`, `SafeArchiveExtractor`.
             - AI: `SuperResolutionUpscalerNode`, `BackgroundRemoverNode`, `PiiAnonymizerNode`, `LocalLlmProcessorNode`, `LocalWhisperTranscriberNode`.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y `dotnet test` $\rightarrow$ **622 / 622 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --48. **Arquitectura de Portabilidad Multiplataforma (OS-Agnostic), Desacoplamiento de I/O y Abstracción de Servicios Tecnológicos**:
      - **Objetivo**: Desacoplar todo el código dependiente de sistemas operativos (Win32, P/Invoke, Shells, Garbage Collection / Working Set) y tecnologías específicas (FFmpeg, transcoders, herramientas externas), así como encapsular todas las operaciones de I/O en un servicio de almacenamiento unificado (`IStorageService`) para que los nodos de plugins no realicen lecturas, escrituras, copias o resoluciones de colisiones directas ni distingan manualmente entre disco real y VFS.
      - **Ajustes Realizados**:
        1. *Contratos en `FileFlow.Sdk`*:
           - `Storage/IStorageService.cs`, `StorageCollisionStrategy`, `StorageOperationResult`, `VirtualStorageService`, `NullStorageService`, `FlowExecutionContextStorageExtensions`.
           - `Platform/IOsPlatformService.cs`, `NullOsPlatformService`, `IFileRecycler.cs`.
           - `Services/IExternalToolsService.cs`, `IMediaTranscoderService.cs`, `NullExternalToolsService.cs`.
           - `IFlowExecutionContext`: Nuevas propiedades `Storage`, `Platform` y `Tools`.
        2. *Servicios y Adaptadores en `FileFlow.Core`*:
           - Adaptadores de SO: `WindowsPlatformService` (encapsula P/Invoke Win32 `SHFileOperationW` y `EmptyWorkingSet`), `LinuxPlatformService` (`gio trash` / `~/.local/share/Trash`, `/bin/bash`), `MacPlatformService` (`osascript` / `~/.Trash`, `/bin/zsh`) y `OsPlatformServiceFactory.Instance`.
           - Almacenamiento: `PhysicalStorageService` (streams con buffer de 128 KB, `FileOptions.SequentialScan`, resolución automática de colisiones e interceptor de dry-run `onRegisterPlannedAction`), `VirtualStorageService`.
           - Servicios externos: `ExternalToolsService` (auto-detección y verificación de binarios multiplataforma), `FfmpegMediaTranscoderService`.
           - `WorkflowExecutionContext`: Instanciación y cableado dinámico de `Storage`, `Platform` y `Tools`.
        3. *Desacoplamiento de Nodos de Plugins*:
           - `FileFlow.Plugin.FileSystem`: `DestinationSinkNode`, `FileRelocatorNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`, `AdvancedRenamerNode` refactorizados para usar `context.GetStorage()`. Eliminado P/Invoke directo de Win32.
           - `FileFlow.Plugin.Hashing`: `HashCalculatorNode` refactorizado para abrir streams asíncronos mediante `storage.OpenReadAsync()`.
           - `FileFlow.Plugin.Integrations`: `MediaTranscoderNode` utiliza `context.Tools.FfmpegExecutable`; `CliExecutionNode` utiliza `context.Platform.GetDefaultShellExecutable()` y `GetDefaultShellArguments()`.
           - `FileFlow.Plugin.AI`: `HardwareCapabilityDetector` utiliza `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` eliminando `GlobalMemoryStatusEx`.
        4. *Capa de Presentación `FileFlow.App`*:
           - `ProcessLauncherService` delega lanzamiento de procesos a `OsPlatformServiceFactory.Instance`.
           - `ExternalToolsService` deriva limpiamente de la implementación central de Core.
        5. *Pruebas Unitarias Dedicadas*:
           - `StorageServiceTests.cs` (4 pruebas) y `OsPlatformServiceTests.cs` (3 pruebas).
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y `dotnet test` $\rightarrow$ **617 / 617 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --47. **Separación y Visualización Dual de Carpetas de Origen y Destino en el Sistema de Archivos Virtual (VFS)**:
      - **Objetivo**: Resolver la limitación donde el sistema de archivos virtual y su explorador visual solo contemplaban la modificación de archivos en su carpeta de origen. Permitir ver el resultado final del flujo de forma dual y organizada: tanto en la carpeta de origen (mostrando el estado original, modificado o trasladado) como en las carpetas de destino (archivos generados o movidos con sus nuevos nombres y metadatos).
      - **Ajustes Realizados**:
        1. *Roles Semánticos y Operaciones en SDK (`FileFlow.Sdk/VirtualFileSystem`)*: Añadido `VirtualFileRole` (`Source`, `Destination`, `Intermediate`) y operaciones `Original` y `Renamed` en `VirtualFileEntry.cs`. Nuevas propiedades `DestinationPath` (en origen enlazando al destino) y `RelatedSourcePath` (en destino enlazando al origen original).
        2. *Contratos y Métodos en `IVirtualFileSystemStore` & `VirtualFileSystemStore`*: Métodos `GetSourceFiles()`, `GetDestinationFiles()` y `RenameFile(...)`. Actualizado `MoveFile` para preservar el archivo de origen marcado como `Moved` con su `DestinationPath`, creando la entrada destino como `Destination`.
        3. *Definición Refinada de Actividad (`IsActive`)*: Detecta como inactivo en su ruta local aquel fichero que ha sido movido hacia otra ruta (`OperationType == Moved` con `DestinationPath` asignado), garantizando que `FileExists(origen)` sea `false`, `FileExists(destino)` sea `true` y las métricas de bytes y archivos no sufran duplicación.
        4. *Nodos de Pipeline (`FileFlow.Plugin.FileSystem`)*:
           - `SyntheticDataSourceNode`: Registra entradas virtuales con `Role = VirtualFileRole.Source` y `OperationType = VirtualOperationType.Original`.
           - `AdvancedRenamerNode`: En modo virtual o items virtuales, invoca `context.VirtualFileSystem.RenameFile(...)` sin exigir presencia en disco físico.
           - `DestinationSinkNode`: Registra entradas destino con `Role = VirtualFileRole.Destination` y vincula el `DestinationPath` en la entrada origen del VFS.
           - `FileRelocatorNode` y `OriginalFileActionNode`: Soporte total para operaciones virtuales de movimiento, copia y reciclaje preservando roles.
        5. *Explorador Visual VFS Dual (`FileFlow.App`)*:
           - `VirtualFileSystemExplorerViewModel`: Árbol particionado en ramas `📥 Carpetas de Origen` y `📤 Carpetas de Destino`, selector desplegable de rol (`Todos`, `📥 Origen`, `📤 Destino`), KPIs dedicados para orígenes y destinos, e inspector con trazabilidad de origen y destino vinculados.
           - `VirtualFileSystemExplorerWindow.xaml`: Columna `Rol` con badges por color, columna `Vinculado`, y pestaña `Antes / Después` con desglose visual.
           - Cadenas multilingües añadidas a `Strings.resx` y `Strings.es.resx`.
        6. *Suite de Pruebas*: Creado `VirtualPipelineSourceDestinationTests.cs` (4 pruebas).
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 errores, 0 warnings) y `dotnet test` $\rightarrow$ **610 / 610 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --46. **Diseñador Visual de Conjuntos de Datos Sintéticos, Estructuras Jerárquicas y Simulación Híbrida de Comprimidos**:
      - **Objetivo**: Proporcionar una solución completa para que los usuarios puedan crear, modificar y guardar conjuntos de datos de prueba personalizados de forma persistente y reutilizable; soportar estructuras jerárquicas con carpetas y subcarpetas (`IsDirectory`, `RelativePath`); y soportar simulación híbrida de archivos comprimidos (ZIP, RAR, 7Z) con extracción virtual directa hacia el VFS (`SmartUnpackNode`) y generación de archivos ZIP reales y ligeros en modo físico (`PhysicalMock`).
      - **Ajustes Realizados**:
        1. *Modelos en el SDK (`FileFlow.Sdk/SyntheticData`)*: Modelos `SyntheticArchiveEntryDefinition`, `SyntheticFileDefinition` y `SyntheticDataSet` con clonación profunda preservando identidad/inmutabilidad.
        2. *Parser DSL Jerárquico Rápido (`SyntheticTreeDslParser.cs`)*: Parser bidireccional que convierte entre texto indentado con tamaños humanos (`50KB`, `1.5GB`), metadatos (`key=val`) y entradas de archivo `[archive: inner1.txt; inner2.png]` y listas de `SyntheticFileDefinition`.
        3. *Servicio de Persistencia (`SyntheticDataSetStorageService.cs`)*: Almacenamiento thread-safe en `%AppData%/FileFlow/SyntheticDataSets/`, carga inicial desde los 200 items de muestra categorizados, exportación/importación JSON y protección de datasets de sistema (`IsBuiltIn`).
        4. *Diseñador Visual WPF (`SyntheticDataSetDesignerWindow.xaml` / `SyntheticDataSetDesignerViewModel.cs`)*: Ventana moderna modal con panel izquierdo de catálogo (búsqueda, métricas, nuevo, duplicar, eliminar) y área de trabajo con 3 pestañas sincronizadas (Tabla Visual, Árbol Rápido DSL y JSON Puro).
        5. *Emisión Jerárquica y Modo Físico (`SyntheticDataSourceNode.cs`)*: Emisión de carpetas (`EmitDirectories`), resolución de rutas relativas con subdirectorios, empaquetado de metadata y generación automática de `.zip` reales con `ZipArchive` en modo `PhysicalMock`.
        6. *Descompresión Virtual en VFS (`FileFlow.Plugin.Archives/SmartUnpackNode.cs`)*: Intercepción de archivos virtuales o con metadatos `Archive:Entries` para desplegar sus entradas directamente en `context.VirtualFileSystem` sin requerir archivos en disco.
        7. *Puntos de Acceso en UI*: Botón de acción personalizada en `SyntheticDataSourceNode`, botón `📊 Diseñador...` en `AdvancedRenamerEditorWindow.xaml`, y opción en el Drawer de `MainWindow.xaml`.
        8. *Suite de Pruebas*: 18 nuevos tests (Total 606): `SyntheticDataSetStorageServiceTests` (5), `SyntheticTreeDslParserTests` (3), `SyntheticDataSourceHierarchicalTests` (3), `SyntheticArchiveSimulationTests` (2) y `SyntheticDataSetDesignerViewModelTests` (5).
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 errores, 0 warnings) y `dotnet test` $\rightarrow$ **606 / 606 pruebas superadas al 100%**.
  --45. **Sistema de Archivos Virtual (VFS) Híbrido, Persistencia No Destructiva en Pruebas y Explorador Visual VFS**:
      - **Objetivo**: Implementar un sistema de archivos virtual (VFS) transparente para pruebas con datos sintéticos (`SyntheticDataSourceNode`), de modo que los nodos de persistencia (`DestinationSinkNode`, `FileRelocatorNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`) escriban y organicen en memoria sin ensuciar discos físicos ni arrojar errores de I/O, complementado con un explorador visual en WPF para inspección jerárquica de carpetas, archivos, badges de operaciones y metadatos.
      - **Ajustes Realizados**:
        1. *Modelos y Contratos (`FileFlow.Sdk`)*: Creado `VirtualFileEntry` (registro con ruta virtual, origen, marcas de tiempo, tipo de operación `Saved`, `Copied`, `Moved`, `ConflictRenamed`, `Deleted`, `Recycled`, metadatos y logs), contrato `IVirtualFileSystemStore`, y propiedades `IsVirtual` en `FileItemContext` y `VirtualFileSystem` / `IsVirtualFileSystemEnabled` en `IFlowExecutionContext`.
        2. *Motor VFS Concurrente (`FileFlow.Core`)*: Creado `VirtualFileSystemStore` con sincronización `System.Threading.Lock` de .NET 9, resolución incremental de colisiones (`_1`, `_2`), generador de diagrama de árbol jerárquico ASCII (`GenerateAsciiTree`) y exportador a disco físico sandbox (`%TEMP%/FileFlow_VFS_Sandbox/...`) con sanitización de unidades de disco Windows (`C_Drive/...`).
        3. *Activación Automática en Motor DAG (`WorkflowExecutor.cs` & `WorkflowExecutionContext.cs`)*: Auto-detección al arrancar el pipeline si hay nodos de origen sintético o contextos virtuales, instanciando un VFS store aislado y entregándolo en `WorkflowExecutionResult`.
        4. *Nodos de Persistencia Redirigidos (`FileFlow.Plugin.FileSystem`)*: Interceptores no destructivos en `DestinationSinkNode`, `FileRelocatorNode`, `SafeRecycleDeleteNode` y `OriginalFileActionNode` con logging localizado.
        5. *Explorador Visual VFS (`FileFlow.App`)*: Creados `VirtualFileSystemExplorerViewModel` y `VirtualFileSystemExplorerWindow.xaml` (SplitView 3 columnas: Árbol de Directorios, Tabla de Archivos con Badges y panel de Metadatos categorizado en EXIF, Audio, Vídeo, Documentos y Hashes, con comandos para copiar árbol ASCII y abrir carpeta sandbox).
        6. *Acceso en UI Anfitriona*: Añadido botón y badge reactivo `🗂️ VFS (N)` en `ControlBarView.xaml` / `ControlBarViewModel.cs` tras ejecuciones virtuales y enlace directo en el Drawer lateral de `MainWindow.xaml`.
        7. *Suite de Pruebas*: Añadidos `VirtualFileSystemStoreTests.cs` (6 tests), `VirtualPipelineExecutionTests.cs` (4 tests) y `VirtualFileSystemExplorerViewModelTests.cs` (5 tests).
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 errores, 0 advertencias) y `dotnet test` $\rightarrow$ **588 / 588 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --44. **Metadata Enriquecida para Datos Sintéticos y Nuevas Categorías Especializadas (Música, Fotos y Documentos)**:
      - **Objetivo**: Dotar a los datos sintéticos de metadata exhaustiva y especializada por dominio (ID3 para audio, EXIF/dimensiones para fotografía, y campos corporativos/fiscales para documentos) para posibilitar pruebas profundas tanto en el lienzo DAG con `SyntheticDataSourceNode` como en el Estudio de Renombrado (`AdvancedRenamerEditorWindow`).
      - **Ajustes Realizados**:
        1. *Banco de Pruebas Extendido a 200 Muestras (`Config/renamer_samples.json`)*:
           - **Música (40 items)**: Tags ID3 reales (`Audio:Artist`, `Audio:Album`, `Audio:Title`, `Audio:Track`, `Audio:Year`, `Audio:Genre`, `Audio:Bitrate`, `Audio:SampleRate`, `Audio:Duration`, `Hash:SHA256`). Formatos: `.mp3`, `.flac`, `.m4a`, `.wav`, `.aac`, `.dsf`, `.zip`.
           - **Fotos (20 items - Nueva Categoría `Fotos`)**: Tags EXIF fotográficos reales (`Exif:CameraMake`, `Exif:CameraModel`, `Exif:LensModel`, `Exif:DateTaken`, `Date Taken`, `Exif:ISO`, `Exif:FNumber`, `Exif:ExposureTime`, `Exif:FocalLength`, `Img:Width`, `Img:Height`, `Orientation`, `AspectRatio`, `Megapixels`, `Exif:GPSCity`, `Exif:GPSCountry`, `Hash:SHA256`). Formatos: `.jpg`, `.jpeg`, `.png`, `.cr3`, `.nef`, `.arw`, `.dng`, `.heic`.
           - **Documentos (20 items)**: Tags documentales y fiscales (`Doc:Author`, `Doc:Title`, `Doc:PageCount`, `Doc:WordCount`, `Doc:CreationDate`, `CustomCategory`, `FiscalYear`, `Department`, `Doc:Currency`, `Doc:TotalAmount`, `Hash:SHA256`). Formatos: `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.md`, `.txt`, `.csv`.
           - **Películas (40 items)**, **Series (40 items)** y **Cómics y Manga (40 items)** enriquecidos con resolución de vídeo, códecs, duraciones y hashes (`Video:Resolution`, `Video:Width`, `Video:Height`, `Video:Codec`, `Video:Duration`, `Audio:Codec`, `ReleaseGroup`, etc.).
        2. *Servicio de Datos de Muestra (`RenamerSampleDataProvider.cs`)*: Añadida la categoría `"Fotos"` en `AvailableCategories` e incorporadas muestras enriquecidas con metadatos reales en `GetFallbackItems()`.
        3. *Nodo de Flujo (`SyntheticDataSourceNode.cs`)*: Añadida la opción `"Fotos"` en el descriptor de parámetros del desplegable `Category`.
        4. *Estudio de Renombrado*: El selector de categorías ahora incluye `"Fotos"`, permitiendo filtrar y previsualizar de inmediato la resolución de etiquetas como `<Exif:CameraModel>`, `<Audio:Artist>`, `<Doc:Title>`, `<Img Width>` y `{AspectRatio}`.
        5. *Suite de Pruebas*: Ampliados [`SyntheticDataSourceNodeTests.cs`](file:///d:/Users/Ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Tests/Unit/Plugins/SyntheticDataSourceNodeTests.cs) y [`AdvancedRenamerEditorViewModelTests.cs`](file:///d:/Users/Ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Tests/Unit/App/AdvancedRenamerEditorViewModelTests.cs) con pruebas específicas para las nuevas categorías y metadatos.
      - **Validación**: `dotnet test` $\rightarrow$ **573 / 573 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --43. **Nodo Generador de Datos de Prueba (SyntheticDataSourceNode), Banco de Pruebas Categorizado y Entrada Manual en Estudio de Renombrado**:
      - **Objetivo**: Proveer un nodo fuente dedicado en el lienzo DAG para depurar y simular flujos sin requerir archivos pesados en disco, integrando los 160 nombres de releases del usuario por categorías (Películas, Series, Cómics y Manga, Música) con selector en el Estudio de Renombrado y entrada manual rápida al vuelo.
      - **Ajustes Realizados**:
        1. *Nuevo Nodo `SyntheticDataSourceNode` (`FileFlow.Plugin.FileSystem`)*: Rol `Source`, categorías `Películas`, `Series`, `Cómics y Manga`, `Música`, `Documentos`, `Personalizada` o `Todas`. Modos `Virtual` (en memoria) y `PhysicalMock` (crea archivos dummy temporales en `%TEMP%/FileFlow_MockData`). Parámetros de límite `MaxItems`, retardo `EmissionDelayMs`, lista editable `CustomItems` y carpeta de salida.
        2. *Catálogo de Muestras Ampliado (`renamer_samples.json` & `RenamerSampleDataProvider.cs`)*: 165 muestras estructuradas con metadatos de categoría y tipo de media. Soporte para registro y limpieza en memoria de muestras manuales del usuario.
        3. *Estudio de Renombrado Mejorado (`AdvancedRenamerEditorWindow.xaml` / `AdvancedRenamerEditorViewModel.cs`)*: Barra de herramientas en panel inferior con ComboBox de categorías (`SampleCategories`), campo `TextBox` de entrada rápida con botón `➕ Probar` y botón `🗑️ Limpiar`.
        4. *Motor de Previsualización (`RenamerLivePreviewService.cs`)*: Soporte para filtrado de muestras por categoría y elementos explícitos en `GeneratePreview`.
        5. *Suite de Pruebas*: Creado `SyntheticDataSourceNodeTests.cs` (5 tests) y actualizado `AdvancedRenamerEditorViewModelTests.cs`.
      - **Validación**: `dotnet test` $\rightarrow$ **570 / 570 pruebas superadas al 100% (0 errores, 0 omitidas)**.
  --42. **Presets de Limpieza Multimedia para AdvancedRenamer (Pipeline "🧹 Limpiar Nombre" con Fases 1 a 6 y Biblioteca Regex)**:
      - **Objetivo**: Integrar presets completos y modulares a partir de especificaciones regex para limpieza de releases audiovisuales y archivos multimedia (URLs publicitarias, etiquetas de calidad/códecs, plataformas de streaming, idiomas/subtítulos, grupos scene/trackers/corchetes y normalización de separadores/espacios), tanto en el selector de presets del pipeline de métodos como en el asistente de expresiones regulares (`RegexLibraryService`).
      - **Ajustes Realizados**:
        1. *Preset Maestro Unificado (`RenamerPresetService.cs` y `renamer_presets.json`)*: Creado `"🧹 Limpiar Nombre"` (categoría `"Limpieza"`) y `"🎬 Pipeline Limpieza Multimedia (Scene, Rips, Códecs y URLs)"` (categoría `"Multimedia"`) con los 10 pasos secuenciales de las fases 1 a 6 sobre el nombre base (conservando intacta la extensión).
        2. *Presets Modulares por Fases*: Creados 6 presets individuales (Fases 1 a 6) para aplicación específica y granular.
        3. *Biblioteca Regex (`RegexLibraryService.cs` y `regex_patterns.json`)*: Añadidos 9 patrones categorizados en `"Releases y Multimedia"`. Ajustado límite de frontera de palabras `\b` en Fase 1 para prevenir falsos positivos en prefijos de palabras (ej. `.re` en `Remux`).
        4. *Resolución Directa por Nombre de Pipeline (`AdvancedRenamerNode.cs`)*: Soporte automático para ejecutar presets incorporados si `MethodSteps` está vacío y `PipelineName` coincide con el preset (ej. `"Limpiar Nombre"` o `"🧹 Limpiar Nombre"`).
        5. *Unificación de Carga de Presets Oficiales*: `GetBuiltinPresets()` y `GetBuiltInPatterns()` inician con las definiciones base en memoria y fusionan configuraciones externas y de usuario, evitando que presets oficiales queden ocultos si existe un archivo `%AppData%` previo.
        6. *Suite de Pruebas*: Creado `MultimediaReleaseCleaningPresetsTests.cs` (9 pruebas unitarias cubriendo resolución por nombre, fases individuales y limpieza completa).
      - **Validación**: `dotnet test` $\rightarrow$ 565 / 565 pruebas superadas al 100%.
  --41. **Corrección de Error XAML/UI en AdvancedRenamerEditorWindow (Pack URIs y Bypassing ALC Duplicado)**:
      - **Objetivo**: Resolver el error de interfaz (`Error de interfaz (XAML/UI): El componente 'FileFlow.Plugin.FileSystem.UI.Views.AdvancedRenamerEditorWindow' no tiene ningún recurso identificado por el URI '/FileFlow.Plugin.FileSystem;V1.0.0.1578;component/ui/views/advancedrenamereditorwindow.xaml'`) al pulsar el botón "🏷️ Pipeline de Métodos..." del nodo de renombrado de archivos (`AdvancedRenamerNode`).
      - **Ajustes Realizados**:
        1. *Fijación de AssemblyVersion Canónico (`Directory.Build.props`)*: Establecido `<AssemblyVersion>$(VersionMajor).$(VersionMinor).0.0</AssemblyVersion>` de modo que WPF no incruste la versión de compilación volátil (`$(BuildNumber)`) en los Pack URIs de los BAML generados. `<FileVersion>` e `<InformationalVersion>` preservan el build number incremental.
        2. *Prevención de Carga ALC Duplicada (`PluginLoader.cs`)*: Comprobación de existencia en `AppDomain.CurrentDomain.GetAssemblies()` antes de instanciar un `PluginAssemblyLoadContext` aislado. Reutiliza el ensamblado del contexto principal, evitando duplicación de tipos y pérdidas de resolución de recursos Pack URI de WPF.
        3. *Inicialización Defensiva con Fallback Seguro (`AdvancedRenamerEditorWindow.xaml.cs` & `RegexHelperWindow.xaml.cs`)*: Añadido método `InitializeComponentSafe()` con fallback programático a `Application.LoadComponent(this, uri)` ante cualquier contingencia.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 errores, 0 advertencias) y `dotnet test` (556/556 pruebas superadas al 100%).
  --40. **Catálogo de Nodos: Modo Acordeón, Colapso por Defecto, Preservación de Estado y Blindaje Anti-Crash en BestVersionSelectorNode**:
      - **Objetivo**: Garantizar que las categorías del catálogo de nodos aparezcan colapsadas por defecto excepto "🔥 Más Usados", funcionen en modo acordeón exclusivo (al abrir una se colapsan las demás), conserven fielmente su estado de expansión al arrastrar o añadir nuevos nodos al lienzo (evitando el molesto descolapso total provocado por el refresco de uso), y eliminar el crash de cierre abrupto por `StackOverflowException` al insertar `BestVersionSelectorNode`.
      - **Ajustes Realizados**:
        1. *Modelo de Categoría (`ToolboxCategoryGroup` en `ToolboxViewModel.cs`)*: Añadidas las propiedades observables `IsExpanded`, `CategoryKey` y el callback reactivo `Action<ToolboxCategoryGroup>? _onExpanded` invocado en `OnIsExpandedChanged(true)`.
        2. *Orquestación de Acordeón (`ToolboxViewModel.cs`)*: Implementado `HandleGroupExpanded(expandedGroup)` que colapsa todas las demás categorías abiertas al expandirse una.
        3. *Preservación de Estado en Refresco (`ToolboxViewModel.RefreshToolbox`)*: Detección previa de `previouslyExpandedKey`. Al dispararse `UserPreferencesService.PreferencesChanged` (cuando el editor incrementa el uso de un nodo al soltarlo en el canvas), se preserva intacta la categoría abierta por el usuario sin reiniciar todas a expandidas.
        4. *Regla por Defecto y Búsqueda*: Por defecto, solo la categoría con `CategoryKey == "Frequent"` ("🔥 Más Usados") se expande; si el usuario introduce un término de búsqueda (`SearchText`), todas las categorías con coincidencias se expanden automáticamente para visualización directa de resultados.
        5. *Enlace Bidireccional en UI (`NodeToolboxView.xaml`)*: Sustituido `IsExpanded="True"` estático por `<Expander IsExpanded="{Binding IsExpanded, Mode=TwoWay}">`.
        6. *Blindaje Anti-Crash en Selector de Versiones (`NodeParameterViewModel.cs`)*: Eliminada la recursión infinita en el getter `AvailableVersionOptions` (`Count <= 2`) y la notificación síncrona redundante `OnPropertyChanged(nameof(AvailableVersionOptions))`. Implementadas banderas `_hasLoadedVersions` y `_isRefreshingVersions` y comparación `isSame` para evitar recreación de chips y eliminar definitivamente el StackOverflow en el Dispatcher de UI.
        7. *Suite de Pruebas (`ToolboxViewModelTests.cs` & `VariableDiscoveryServiceTests.cs`)*: 5 nuevas pruebas unitarias cubriendo acordeón, colapso por defecto, preservación tras inserción e inicialización segura de `BestVersionSelectorNode`.
      - **Validación**: 556 / 556 pruebas superadas al 100% (0 errores, 0 omitidas).
  --39. **Selector Visual e Inteligente de Versiones de Archivo (Chips + Autodescubrimiento Upstream) y Reclasificación de FileForkNode**:
      - **Objetivo**: Eliminar la ambigüedad y baja intuición al elegir versiones en nodos como `SwitchActiveFileNode`, `BestVersionSelectorNode`, `VersionRouterNode` y `FileRelocatorNode` (donde antes se mostraban cadenas crudas como `{OriginalPath}` o `{CurrentPath}`), unificar chips rápidos con autodescubrimiento topológico y editor de expresiones avanzadas, y clarificar `FileForkNode` (Opción 3B).
      - **Ajustes Realizados**:
        1. *SDK & Descriptores (`ParameterEditorType.cs`)*: Añadido `FileVersionSelector` como nuevo tipo de editor especializado.
        2. *Modelo de Presentación (`AppModels.cs`)*: Creado el record `FileVersionOption` con `Tag`, `Token`, `DisplayName`, `Icon`, `Description`, `IsUpstream`, `SourceNodeTitle` y etiqueta con chip.
        3. *Nodos Actualizados (`SwitchActiveFileNode.cs`, `BestVersionSelectorNode.cs`, `VersionRouterNode.cs`, `FileRelocatorNode.cs`, `FileForkNode.cs`)*: Asignado `EditorType = ParameterEditorType.FileVersionSelector` a parámetros de selección de versiones. `FileRelocatorNode` soporta resolución directa de etiquetas sin corchetes mediante `item.GetVersionPath`. `FileForkNode` reclasificado en macrocategoría `Logic` con `SubCategory = "Advanced"`, descripción clarificada y descriptores con tooltips.
        4. *Servicio de Descubrimiento Topológico (`IVariableDiscoveryService.cs`, `VariableDiscoveryService.cs`)*: Implementado `GetAvailableFileVersions(targetNode, connections)` con traversal inverso BFS en el grafo DAG comparando `Id` de nodo, detectando transformadores encadenados multi-nodo (`ImageOptimizer` $\rightarrow$ `Optimized`, `BackgroundRemover` $\rightarrow$ `NoBackground`, `SuperResolution` $\rightarrow$ `SuperResolution`) junto a las opciones fijas `Original` y `Actual`.
        5. *ViewModel y Comandos Reactivos (`NodeParameterViewModel.cs`)*: Propiedad `IsFileVersionSelector`, colección reactiva `AvailableVersionOptions`, propiedad `ActiveVersionTag`, comandos `SelectVersionOptionCommand` y `ToggleCustomExpressionModeCommand`.
        6. *UI & Converters XAML (`BooleanConverters.cs`, `App.xaml`, `NodeParameterTemplates.xaml`, `NodeInspectorPanelView.xaml`)*: Creados `StringEqualsToBooleanConverter` y `StringsEqualMultiConverter`. Interfaz híbrida con barra de chips seleccionables con 1 clic, badges de color diferenciado, conmutador `{x}` a modo expresión libre y botón de apertura al editor ampliado.
        7. *Editor Ampliado (`TextEditorDialogWindow.xaml` / `.cs`)*: Barra superior `BorderVersionChips` para inserción rápida de versiones en el cursor con refresco preventivo.
        8. *Reactividad Global en Tiempo Real (`EditorViewModel.cs` & `NodeInspectorViewModel.cs`)*: Auto-refresco en caliente de versiones al añadir/quitar conexiones (`Connections.CollectionChanged`), nodos (`Nodes.CollectionChanged`), abrir el inspector (`InspectNode`) y al terminar `LoadFromGraphModel`.
        9. *Pruebas Unitarias*: Actualizado `VariableDiscoveryServiceTests.cs` (9 pruebas cubriendo autodescubrimiento multi-nodo en cadena, grafo JSON exacto del usuario y reconexión dinámica reactiva).
      - **Validación**: 551 / 551 pruebas superadas al 100% (0 errores, 0 omitidas).
  --38. **Soporte Multiversión de Archivos en Pipeline, Auto-Purga de Temporales, Selector de Mejor Versión y Enrutador de Decisiones**:
      - **Objetivo**: Permitir en pipelines complejos donde se encadenan transformadores intermedios comparar versiones (ej. imagen optimizada vs original), seleccionar automáticamente o mediante condiciones cuál guardar, autopurgar los archivos temporales perdedores/descartados sin tocar el archivo original inmutable, y permitir a `FileRelocatorNode` trasladar versiones específicas (`SourcePath`) con limpieza opcional.
      - **Ajustes Realizados**:
        1. *Historial de Versiones en Contexto (`FileItemContext.cs`)*: Añadido `FileVersions` (`Dictionary<string, string>`), `RegisterVersion(tag, path)` con auto-poblado de metadatos (`File:Tag`, `FileSize:Tag`, `FileSizeKB:Tag`, `FileSizeMB:Tag`), y `GetVersionPath(tag)` transparente para `"Original"`, `"Current"` y etiquetas personalizadas.
        2. *Resolución de Plantillas Dinámicas (`SystemVariablesResolver.cs`)*: Soporte para `{File:Tag}`, `{FileSize:Tag}`, `{FileSizeBytes:Tag}`, `{FileSizeKB:Tag}`, `{FileSizeMB:Tag}` leyendo directamente en disco o mediante metadatos en memoria.
        3. *Traslado con Selección de Origen (`FileRelocatorNode.cs`)*: Parámetro `SourcePath` (default `"{CurrentPath}"`, soporta `{OriginalPath}`, `{File:Optimized}`, etc.) y `CleanupSource` (default `false`) con garantía estricta de nunca eliminar `OriginalPath`.
        4. *Selector de Mejor Versión con Autopurga (`BestVersionSelectorNode.cs`)*: Compara Candidato A vs Candidato B por menor tamaño (`SmallestSize`), mayor tamaño, umbral de ahorro (`SavedPercentThreshold`), o selección forzada. Emite por `Out`, `WonA` y `WonB`. Autopurga por defecto (`DiscardLoser = true`) del archivo intermedio perdedor.
        5. *Enrutador de Versiones con Autopurga (`VersionRouterNode.cs`)*: Enruta por `True` o `False` según condiciones numéricas o textuales entre versiones, activando el archivo de la rama ganadora y autopurgando por defecto (`PurgeUnselectedTemps = true`) el archivo descartado.
        6. *Nodos de Control y Ciclo de Vida (`SwitchActiveFileNode.cs`, `FileForkNode.cs`, `IntermediateCleanupNode.cs`)*: Cambio de archivo activo, clonación en ramas paralelas independientes, y recolección de basura de archivos temporales.
        7. *Auto-Registro en Transformadores (`ImageOptimizerNode.cs`, `BackgroundRemoverNode.cs`, `SuperResolutionUpscalerNode.cs`)*: Auto-registro de `"Optimized"`, `"NoBackground"`, `"SuperResolution"`.
        8. *Catálogo de Variables e i18n*: Exposición de variables en `VariableDiscoveryService.cs`, recursos multilingües `Strings.resx` y `Strings.es.resx` en `FileFlow.Plugin.Logic`, e iconos en `NodeIconResolver.cs`.
        9. *Suite de Pruebas*: Creada `FileVersionAndSelectionTests.cs` con 8 pruebas unitarias exhaustivas.
      - **Validación**: 544 / 544 pruebas superadas al 100% (0 errores, 0 omitidas).
  --37. **Catálogo Visual de Variables, Autocompletado IntelliSense y Filtrado Upstream Dinámico en el DAG**:
      - **Objetivo**: Resolver los problemas de usabilidad con las variables: sobrecarga y corte vertical del menú contextual por falta de scroll, ausencia de categorización y búsqueda rápida, falta de autocompletado en el editor ampliado, ambigüedad sobre qué variables existen realmente según los nodos precedentes y ausencia de una vista previa evaluada realista.
      - **Ajustes Realizados**:
        1. *Catálogo Visual Dedicado (`VariablePickerWindow.xaml` / `.cs`)*: Ventana modal con buscador reactivo, filtros por categoría mediante chips redondeados (Todas, Nodos Anteriores, Sistema, Fechas, Tamaños, Funciones), lista de tarjetas enriquecidas con resaltado de tokens, badges de procedencia upstream y valores de muestra calculados, panel lateral de detalle y vista previa evaluada, e inserción por doble clic o Enter.
        2. *Menú Contextual Reorganizado (`NodeParameterViewModel.cs`)*: Despliegue en submenús categorizados con altura máxima (`MaxHeight = 500`) y `ScrollViewer` vertical habilitado para evitar que el menú se corte por abajo en cualquier resolución. Primer elemento destacado: *"🔍 Abrir Catálogo Completo de Variables..."*.
        3. *Editor Ampliado con IntelliSense y Panel Lateral Plegable (`TextEditorDialogWindow.xaml` / `.cs`)*: Popup flotante de autocompletado activado al escribir `{`, con navegación por flechas, selección por `Enter`/`Tab` y cierre con `Esc`. Panel lateral plegable con buscador integrado. Vista previa evaluada en tiempo real usando `CreatePreviewItem`.
        4. *Descubrimiento Topológico Upstream en el DAG y Vista Previa Realista (`VariableDiscoveryService.cs`, `IVariableDiscoveryService.cs`)*: Recorrido topológico inverso para detectar variables procedentes de nodos anteriores (`ImageOptimizer`, `BackgroundRemover`, `SuperResolution`, etc.). Método `CreatePreviewItem` para generar contextos `FileItemContext` con datos reales y enriquecidos de muestra para previsualización inmediata.
        5. *Internacionalización (i18n)*: Claves añadidas en `Strings.resx` y `Strings.es.resx`.
        6. *Suite de Pruebas*: Creada `VariablePickerAndIntelliSenseTests.cs` y actualizada `VariableDiscoveryServiceTests.cs`.
      - **Validación**: 536 / 536 pruebas superadas al 100% (0 errores, 0 omitidas).
  --36. **Directorio de Trabajo Temporal en Ajustes, Nodos Intermedios Anti-Colisión y Comparación de Tamaños de Archivos**:
      - **Objetivo**: Añadir en Ajustes la definición de un directorio de trabajo temporal configurable para nodos intermedios (optimizar imágenes, eliminar fondo, super-resolución, etc.) cuando su parámetro de salida esté vacío, generando subcarpetas aleatorias anti-colisión, proveyendo variables `{TempDir}` y `{RandomId}`, y emitiendo metadatos de tamaño para evaluación condicional en nodos posteriores como `ExpressionFilterNode`.
      - **Ajustes Realizados**:
        1. *Directorio Temporal en Ajustes (`AppPaths.cs`, `UserPreferencesService.cs`, `WorkflowSettingsWindow.xaml`)*: Añadida la propiedad `TemporaryDirectory` en preferencias, con fallback en `AppPaths.DefaultTempDirectory` (`%TEMP%\FileFlowStudio\Temp` o `data\temp` en portable), control en pestaña Almacenamiento & Rutas, botón examinador y localización multilingüe.
        2. *Motor DAG e Inyección de Metadatos (`IFlowExecutionContext.cs`, `WorkflowGraph.cs`, `WorkflowExecutor.cs`, `WorkflowItemDispatcher.cs`)*: Propagación de `TemporaryDirectory` al contexto de ejecución e inyección automática en `item.Metadata["TemporaryDirectory"]`.
        3. *Nodos Intermedios Anti-Colisión (`ImageOptimizerNode.cs`, `BackgroundRemoverNode.cs`, `SuperResolutionUpscalerNode.cs`, `ParameterHelper.cs`)*: Parámetro `OutputDirectory` vacío por defecto (`""`). Si está vacío, `ParameterHelper.ResolveIntermediateOutputDir` crea una subcarpeta con identificador aleatorio anti-colisiones (`{TempDir}\{randomId}\`).
        4. *Variables del Sistema (`SystemVariablesResolver.cs`)*: Soporte de `{TempDir}`, `{TemporaryDir}`, `{TempWorkingDir}`, `{RandomId}`, `{Guid}` y variables de tamaño: `{OriginalFileSize}`, `{OriginalFileSizeBytes}`, `{OriginalFileSizeKB}`, `{OriginalFileSizeMB}`, `{OutputFileSize}`, `{OutputFileSizeBytes}`, `{OutputFileSizeKB}`, `{OutputFileSizeMB}`, `{SavedBytes}`, `{SavedPercent}`, `{CompressionRatio}` bajo `CultureInfo.InvariantCulture`.
        5. *Evaluación Condicional en `ExpressionFilterNode.cs`*: Soporte de resolución de variables en `ComparisonValue` (`compVal`) para permitir comparaciones directas entre variables (ej: `{OutputFileSize} > {OriginalFileSize}`).
        6. *Suite de Pruebas*: Creada `TemporaryDirectoryAndSizeVariablesTests.cs` validando todas las funcionalidades.
      - **Validación**: 530 / 530 pruebas superadas al 100% (0 errores, 0 omitidas).
  --35. **Personalización de Título de Nodos en el Flujo y Trazabilidad en Logs y Telemetría**:
      - **Objetivo**: Permitir modificar el título de cada nodo dentro del flujo mediante menú contextual ("Renombrar nodo..." / atajo `F2`), restableciendo al nombre por defecto ante textos vacíos y asegurando que en el log y telemetría aparezca como nombre del nodo el título activo (original o modificado), eliminando la doble pulsación sobre el título para no interferir con la apertura del inspector de nodos.
      - **Ajustes Realizados**:
        1. *Modelo DAG y Persistencia (`WorkflowGraph.cs` & `WorkflowGraphSerializer.cs`)*: Añadida la propiedad `CustomTitle` a `WorkflowNode`, con serialización/deserialización en JSON e importación/exportación en el serializador de grafo.
        2. *Motor de Ejecución y Telemetría (`WorkflowExecutor.cs`)*: Mapeo de `_nodeDisplayNames` al iniciar la ejecución. En `NotifyLog`, `StructuredLogRecord.NodeName` resuelve el título personalizado con fallback al nombre del descriptor, reflejándose en SQLite (`SqliteLogStore`) y la consola de logs.
        3. *Tarjetas de Nodo y Edición Inline (`NodeViewModel.cs`, `NodeCardView.xaml`, `NodeCardView.xaml.cs`)*: Implementado editor en línea (`TextBox`) con `Enter` para confirmar, `Escape` para cancelar y `LostFocus` para commit automático. La doble pulsación sobre cualquier punto del nodo se preserva exclusivamente para abrir el inspector (`InspectNode`), y el renombrado se dispara por menú contextual o tecla `F2`. Sincronización automática entre `Title` y `CustomTitle`.
        4. *Atajos y Portapapeles (`EditorView.xaml.cs`, `NodeClipboardService.cs`)*: Soporte de `F2` para renombrar el nodo seleccionado y preservación de `CustomTitle` en copia, corte, duplicado y pegado.
        5. *Internacionalización (i18n)*: Claves `RenameNode`, `DoubleClickToRenameToolTip` y `ResetTitleToDefault` en `Strings.resx` y `Strings.es.resx`. Preservación del título personalizado ante cambios de idioma en caliente, y fallback al nombre traducido del descriptor cuando el título no está personalizado.
        6. *Suite de Pruebas*: Creada `NodeTitleCustomizationTests.cs` con 7 pruebas unitarias completas.
      - **Validación**: 523 / 523 pruebas superadas al 100% y compilación sin advertencias bajo `--warnaserror`.
  --34. **Corrección de Visualización y Cálculo Intermitente de Métricas de Telemetría en el Flujo de Ejecución**:
      - **Objetivo**: Resolver de raíz el problema por el cual las métricas de rendimiento y telemetría de los nodos (latencia, RAM asignada, aceleración GPU, porcentaje de tiempo y cuellos de botella) a veces se mostraban y a veces no al ejecutar un flujo.
      - **Causa Raíz Identificada**:
        1. *Race condition en el temporizador visual*: `WorkflowExecutionCoordinator` refrescaba las métricas cada 33 ms con un `DispatcherTimer`. En flujos rápidos (ejecución < 33 ms o completados entre ticks), la ejecución terminaba y en el `finally` se detenía el timer y se ponía `_activeExecutor = null` sin realizar un volcado final síncrono de `_activeExecutor.GetNodeTelemetryStats()`.
        2. *Borrado involuntario de métricas en Idle*: En `NodeViewModel.cs`, el manejador `OnExecutionStatusChanged` limpiaba `LatencyText`, `RollingRamText` y `DetailedMetricsToolTip` cuando el estado volvía a `Idle`, borrando las métricas calculadas.
        3. *Falta de reset explícito al iniciar*: No había un método determinista para reiniciar métricas de ejecuciones anteriores antes de comenzar una nueva ejecución.
        4. *Falta de telemetría de memoria/hardware en nodos de inicio*: En `WorkflowExecutor.cs`, la ejecución de `startNode` solo registraba tiempo transcurrido, pasando 0 en bytes y false en GPU.
        5. *Concurrencia en SqliteLogStore*: `SingleReader = true` causaba condición de carrera entre el worker de fondo y llamadas de `FlushPendingLogsAsync` o `ClearAsync`.
      - **Ajustes Realizados**:
        1. En `WorkflowExecutionCoordinator.cs`: Añadido reset explícito inicial `_editorViewModel.ResetAllNodeMetrics()` y volcado final síncrono de telemetría en el bloque `finally` antes de liberar `_activeExecutor`.
        2. En `NodeViewModel.cs`: Modificado `OnExecutionStatusChanged` para preservar `LatencyText` y `RollingRamText` al pasar a `Idle` (solo se resetea la barra de progreso y badges transitorios), y asegurada la ejecución en el dispatcher en `UpdateTelemetryStats`.
        3. En `EditorViewModel.cs`: Implementado `ResetAllNodeMetrics()` para limpiar explícitamente las métricas de todos los nodos al inicio de cada ejecución.
        4. En `WorkflowExecutor.cs`: Instrumentada la medición de memoria GC (`GC.GetAllocatedBytesForCurrentThread()`) y detección de GPU en la ejecución de los nodos raíz de inicio tanto en modo batch normal como en watch mode.
        5. En `SqliteLogStore.cs`: Cambiado `SingleReader = false` y serializado el consumo del canal y la inserción SQLite bajo `_flushLock`.
      - **Validación**: 516 / 516 pruebas unitarias e integración superadas al 100%.
  --33. **Corrección de Emisión en ImageOptimizerNode: Exclusividad Mutua entre Puertos Out y Error**:
      - **Objetivo**: Garantizar que ante fallos en la optimización o archivos que no pueden ser procesados/corruptos, el archivo solo sea emitido por el puerto `"Error"` y nunca por `"Out"`.
      - **Ajustes Realizados**:
        1. Refactorizado el flujo de `ImageOptimizerNode.ExecuteAsync` separando la ejecución del bloque `try-catch` de la emisión por el puerto `"Out"`.
        2. Ante cualquier fallo en ImageSharp o el sistema de archivos, el `catch` emite exclusivamente a `"Error"` y termina la ejecución con `return`.
        3. Preservada la inmutabilidad y linaje del archivo original asignando `OriginalPath = item.OriginalPath` en el ítem de salida.
        4. Agregadas pruebas de regresión en `ImageOptimizerNodeTests` comprobando que `EmitAsync("Out", ...)` jamás sea invocado cuando ocurre un fallo.
      - **Validación**: 516 / 516 pruebas superadas al 100%.
  --32. **Refactorización Integral hacia Clean Architecture, Inversión de Control (IoC) y Puertos & Adaptadores**:
      - **Objetivo**: Elevar la arquitectura del proyecto hacia los más altos estándares de Clean Architecture y SOLID mediante Inversión de Dependencias (IoC), abstrayendo todos los servicios de infraestructura en contratos de puertos e inyectándolos desacoplados en los ViewModels.
      - **Ajustes Realizados**:
        1. **Puertos de Dominio y Core (`FileFlow.Sdk` / `FileFlow.Core`)**:
           - `ILocalizationService`: Abstracción para internacionalización y recursos de cadenas multilingües.
           - `ILogStore`: Abstracción para el almacenamiento e ingesta analítica de telemetría sobre SQLite.
           - `IFileRecycler`: Abstracción para eliminación segura y envío a la Papelera de reciclaje de Windows.
           - `IFolderWatcherService`: Abstracción para la supervisión reactiva de carpetas en tiempo real.
        2. **Puertos y Adaptadores de UI / Presentación (`FileFlow.App`)**:
           - `ISystemPerformanceMonitor`: Contrato para monitoreo en vivo de CPU, RAM y GPU.
           - `IThemeService`: Abstracción para la gestión reactiva de temas.
           - `IUserPreferencesService`: Abstracción para persistencia de configuraciones de usuario.
           - `IDialogService` & `WpfDialogService`: Abstracción de cuadros de diálogo y alertas (`MessageBox.Show`).
           - `IProcessLauncherService` & `ProcessLauncherService`: Abstracción para abrir rutas, carpetas y navegadores sin acoplamiento a `Process.Start`.
        3. **Composición IoC (`Microsoft.Extensions.DependencyInjection`)**:
           - Creado `ServiceCollectionExtensions.cs` registrando servicios Singleton y Transient para toda la aplicación.
           - Configurado `App.Services` en `App.xaml.cs` para inicializar el contenedor y resolver `MainViewModel` y sus ViewModels hijos (`EditorViewModel`, `ControlBarViewModel`, `StatusBarViewModel`, `LogViewModel`, `ToolboxViewModel`, `NodeInspectorViewModel`).
        4. **Pruebas Automatizadas de Arquitectura**:
           - Creado `DependencyInjectionAndPortsTests.cs` validando el registro completo de dependencias y ejecución aislada de ViewModels con fakes.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y 515 / 515 pruebas superadas con éxito (100%).
  --31. **Blindaje de Cancelación Asíncrona (CancellationToken) y Manejo de Excepciones en Nodos**:
      - **Objetivo**: Asegurar que las cancelaciones voluntarias de ejecución (botón Detener / Stop) se propaguen de manera instantánea y limpia sin emitir falsos errores en los puertos de salida ni en el registro de telemetría.
      - **Ajustes Realizados**:
        1. **Filtros de Excepción**: Incorporación de cláusulas `when (ex is not OperationCanceledException)` en todos los nodos de procesamiento asíncrono y estrategias de red en `FileFlow.Plugin.Network`, `FileFlow.Plugin.AI`, `FileFlow.Plugin.Images`, `FileFlow.Plugin.Integrations`, `FileFlow.Plugin.Archives`, `FileFlow.Plugin.FileSystem`, `FileFlow.Plugin.Hashing` y `FileFlow.Plugin.Documents`.
        2. **Validación**: Compilación con `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y 512 / 512 pruebas superadas con éxito (100%).
  --30. **Auditoría Integral de la Aplicación: Corrección del Ciclo de Vida de Fusión PDF, Aislamiento de Ejecución y Reactividad i18n**:
      - **Objetivo**: Corregir inconsistencias detectadas en el ciclo de vida de nodos acumuladores, aislamiento entre ejecuciones sucesivas del motor DAG y reactividad de localización en la barra de estado.
      - **Ajustes Realizados**:
        1. **`PdfMergeNode`**: Implementado `OnWorkflowCompletedAsync` para fusionar y emitir el PDF resultante por el puerto `"Out"` al concluir el lote, con soporte DryRun (`PlannedAction`) y aislamiento por `_lastExecutionId`.
        2. **`ExcelReportGeneratorNode`**: Limpieza de `_collectedRows` y detección de cambio en `WorkflowExecutionId` para evitar duplicación de datos entre re-ejecuciones del grafo.
        3. **`MediaTranscoderNode`**: Guarda de seguridad para evitar `File.Copy` cuando las rutas de origen y destino coinciden exactamente en modo fallback.
        4. **`StatusBarViewModel`**: Integración de claves de localización (`StatusBar_Ready`, `StatusBar_ReadyToExecute`, `StatusBar_Running`, `StatusBar_Paused`) y refresco reactivo automático ante `LanguageChanged` en caliente.
        5. **Pruebas Automatizadas**: Añadida prueba unitaria en `DocumentsTests.cs` validando el ciclo de vida completo de `PdfMergeNode`.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 errores, 0 advertencias) y 512 / 512 pruebas superadas al 100%.
  --29. **Optimización de Rendimiento Extremo, Paralelismo Multinúcleo y Reducción de Asignaciones GC**:
      - **Objetivo**: Maximizar el throughput por segundo, aprovechar al 100% los hilos de CPU disponibles y minimizar las pausas y asignaciones de memoria en los hot paths del motor DAG, SQLite Log Store, contratos del SDK e I/O criptográfico.
      - **Ajustes Realizados**:
        1. **Motor DAG (`WorkflowItemDispatcher.cs`)**: Static delegates en `ConcurrentDictionary`, rate-limiting de formateo de strings para progreso UI (múltiplos de 10) para evitar inundar el despachador WPF.
        2. **Telemetría de Cero Latencia (`SqliteLogStore.cs`)**: Sustituido el `Task.Delay(20)` arbitrario por drenaje con `Task.Yield()`, alcanzando **59.312 logs/seg** en SQLite in-memory across 16 CPU cores.
        3. **Data Locality y Zero-Allocation en `FileItemContext` (`FileFlow.Sdk`)**: `DeepClone()` con constructores de capacidad cero (`capacity: 0`) para colecciones vacías y propagación de `_idString` / `_shortIdString` cacheados sin regenerar `Guid.ToString()`, alcanzando **689.655 clones/seg** (20.000 clones en 29 ms).
        4. **I/O Criptográfico Asíncrono (`HashCalculatorNode.cs`)**: Apertura con `FileOptions.Asynchronous | FileOptions.SequentialScan` y buffers de 128 KB, alcanzando **264,55 MB/seg** en SHA-256.
        5. **Suite de Benchmarks (`PerformanceBenchmarkSuiteTests.cs`)**: Integradas pruebas automatizadas de throughput, clonación, vectorización SIMD, hashing e ingesta de telemetría.
      - **Validación**: `dotnet build FileFlow.slnx --warnaserror` (0 advertencias, 0 errores) y 511 / 511 pruebas superadas al 100%.
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

