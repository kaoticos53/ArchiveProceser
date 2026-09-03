# Resumen Consolidado de Sesiones y Memoria de Proyecto - FileFlow Studio

Este documento se actualiza al finalizar cada sesión de trabajo para consolidar los puntos clave, decisiones arquitectónicas, capacidades del sistema y el estado de la solución, evitando empezar desde cero en futuras conversaciones.

---

## 1. Estado Actual del Repositorio y Calidad
- **Target Framework**: `.NET 9` (`net9.0` / `net9.0-windows` para WPF UI) con preparación para .NET 10.
- **Lenguaje**: `C# 13` (`<LangVersion>13</LangVersion>`), Nullable activado de forma estricta.
- **Estado de Compilación**: `dotnet build FileFlow.slnx --warnaserror` $\rightarrow$ **0 Advertencias, 0 Errores**.
- **Suite de Pruebas**: `dotnet test FileFlow.slnx` $\rightarrow$ **481 / 481 Pruebas Pasadas con 100% de Éxito** (Unit, Integration, Security, Concurrency, JSON Loaders, AppPaths Storage, Portable Mode Provider, CLI Headless Runner, Document Plugins, Network Plugins, Data & Spreadsheet Plugins, AI & Computer Vision Plugins, File QuickPreviewer Providers, Watchdog Multi-Folder, Bottleneck Heatmap, Checkpointing & Resumption, Annotations & Group Boxes, AI Models Manager ViewModel, AI Model Persistence on Disk, LogConsole ViewModel Tests & Node Inspector Sync, PromptObjectDetectorNode & PromptTranslator MarianMT, Log Filtering & SQLite Sync, Toolbox Compact Mode Persistence, LocalAiTranslatorNode, LocalLlmProcessorNode, PromptTransformerNode, Download Error Reporting & Dismissal, AI Models Configurable URLs & Fallback, GPU Performance Metrics, HardwareCapabilityDetector, AiTaskModelResolutionTests, VisionSuiteNodesTests, AudioSuiteNodesTests, SecurityAndSemanticNodesTests, ToolboxOrganizationTests).
- **Nuevas Funcionalidades y Correcciones Implementadas en Sesión**:
  0. **Plan Maestro de Auditoría y Refactorización Limpia (Clean Code & Arquitectura Modular - Fases 2A a 2E Completadas)**:
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

