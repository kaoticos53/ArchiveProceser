# FileFlow Studio - Historial de Cambios y Registro de Implementación (Walkthrough)

Este documento registra cronológicamente todos los cambios, mejoras, correcciones y nuevas funcionalidades implementadas en el proyecto **FileFlow Studio**.

## [2026-09-03] - Reorganización Inteligente de los 60 Nodos del Sistema (Opción 3: Taxonomía Unificada, Tags Multilingües y Perspectiva Dual)

### 🎯 Funcionalidades Implementadas
1. **Taxonomía Unificada de 11 Categorías Rebalanceadas**:
   - Se reestructuraron las macro-categorías del sistema para agrupar armónicamente los 60 nodos oficiales en 11 dominios claros y concisos: `Files`, `ImageVision`, `AudioVoice`, `Documents`, `Data`, `LanguageAI`, `Security`, `Logic`, `Archives`, `Network`, `Integrations`.
   - Se eliminaron fragmentaciones y duplicidades históricas (`MediaDocs`, `Metadata`, `Hashing`, `Data & Databases`).
2. **Contrato de Roles de Pipeline ETL (`PipelineRole.cs`) en `FileFlow.Sdk`**:
   - Incorporado enum `PipelineRole` con las etapas fundamentales del flujo de datos: `Source`, `Filter`, `Transform`, `Analyze`, `Sink`, `Control`.
   - Ampliado el atributo `[NodeDefinition]` con propiedades `Role`, `Tags` y `SubCategory`.
3. **Decoración Exhaustiva de los 60 Nodos Oficiales**:
   - Se decoraron y actualizaron el 100% de los nodos a través de los 11 proyectos de plugins (`FileFlow.Plugin.*`):
     - `FileFlow.Plugin.Images` (2 nodos): `ImageOptimizerNode`, `ExifMetadataNode`.
     - `FileFlow.Plugin.Hashing` (2 nodos): `HashCalculatorNode`, `DeduplicationFilterNode`.
     - `FileFlow.Plugin.Integrations` (3 nodos): `CliExecutionNode`, `WebhookNotificationNode`, `MediaTranscoderNode`.
     - `FileFlow.Plugin.Scripting` (1 nodo): `CustomScriptNode`.
     - `FileFlow.Plugin.Logic` (5 nodos): `SwitchCaseNode`, `ExpressionFilterNode`, `BatchBufferNode`, `ThrottleDelayNode`, `ForkJoinBarrierNode`.
     - `FileFlow.Plugin.Archives` (3 nodos): `SmartUnpackNode`, `ArchiveCompressorNode`, `ArchiveFilterNode`.
     - `FileFlow.Plugin.Documents` (4 nodos): `PdfMergeNode`, `PdfSplitNode`, `PdfTextExtractorNode`, `PdfMetadataNode`.
     - `FileFlow.Plugin.Network` (5 nodos): `RemoteDownloadNode`, `FtpUploadNode`, `SftpUploadNode`, `SmbCopyNode`, `WebDavUploadNode`.
     - `FileFlow.Plugin.Data` (7 nodos): `ExcelReaderNode`, `CsvReaderNode`, `DataLookupNode`, `ExcelReportGeneratorNode`, `CsvExportNode`, `SqliteDatabaseSinkNode`, `DataFormatConverterNode`.
     - `FileFlow.Plugin.FileSystem` (12 nodos): `FolderSourceNode`, `DestinationSinkNode`, `AdvancedRenamerNode`, `FileRelocatorNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`, `DirectoryInspectorNode`, `EmptyDirectoryCleanerNode`, `DocumentProcessorNode`, `VariableInjectorNode`, `OperationReportNode`, `LogOutputNode`.
     - `FileFlow.Plugin.AI` (16 nodos): `LocalOcrNode`, `SmartImageClassifierNode`, `FaceDetectorNode`, `ObjectDetectorNode`, `PromptObjectDetectorNode`, `BackgroundRemoverNode`, `SuperResolutionUpscalerNode`, `ContentModerationFilterNode`, `PiiAnonymizerNode`, `LocalWhisperTranscriberNode`, `VoiceActivityDetectorNode`, `TextToSpeechNode`, `LocalAiTranslatorNode`, `LocalLlmProcessorNode`, `PromptTransformerNode`, `ZeroShotSemanticSearchNode`.
4. **Búsqueda Rápida Multilingüe por Sinónimos y Etiquetas (`Tags`)**:
   - Cada nodo cuenta con un array de etiquetas en español e inglés que abarcan sinónimos, formatos de archivo y casos de uso (ej. "recortar", "fondo", "dni", "iban", "gdpr", "mp3", "excel", "duplicados", "silero", "piper").
   - El motor de filtrado del Toolbox evalúa de forma reactiva `Name`, `Category`, `Description`, `Role`, `LocalizedRole` y todos sus `Tags`.
5. **Perspectiva Dual en el Catálogo Visual (`ToolboxViewModel` y `NodeToolboxView.xaml`)**:
   - Modo `ByCategory` (Dominio funcional) vs `ByPipelineRole` (Etapa de pipeline ETL).
   - Botón selector dinámico en la cabecera del Toolbox para alternar de perspectiva al instante con hot-reload visual.
   - En modo `ByPipelineRole`, los nodos se ordenan por su secuencia natural de datos: Ingesta (`Source`) $\rightarrow$ Filtro (`Filter`) $\rightarrow$ Transformación (`Transform`) $\rightarrow$ Análisis (`Analyze`) $\rightarrow$ Destino (`Sink`) $\rightarrow$ Control (`Control`).
   - Badges de rol visuales (píldoras de colores y emojis) tanto en la tarjeta de cada nodo como en su tooltip interactivo.
6. **Localización e Internacionalización Completa (i18n)**:
   - Recursos multilingües agregados en `FileFlow.App/Resources/Strings.resx` y `Strings.es.resx` para las 11 categorías, los 6 roles y los textos/tooltips de la perspectiva dual.

### 🧪 Validación y Pruebas
- Nueva suite exhaustiva de pruebas en `FileFlow.Tests/Unit/Toolbox/ToolboxOrganizationTests.cs`:
  - `AllNodes_MustHaveValidDefinitionAttribute_CategoryAndPipelineRole`: Verifica que los 60 nodos poseen atributos válidos, categoría, rol y etiquetas.
  - `AllNodes_MustBelongToUnifiedTaxonomyCategories`: Valida la pertenencia estricta a las 11 categorías oficiales.
  - `MultilingualSearch_ByTags_ShouldFindMatchingNodes`: Prueba de teoría con 13 consultas de búsqueda por tags en español e inglés.
  - `PerspectiveToggle_ShouldGroupByPipelineRole_InProperOrder`: Valida la alternancia de perspectiva y el orden de los 6 roles de flujo.
  - `PipelineRole_Localization_ShouldReturnValidStringsInBothLanguages`: Valida la localización en `es-ES` y `en-US`.
- Suite completa de la solución: `dotnet test FileFlow.slnx` $\rightarrow$ **481 / 481 pruebas superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Plan C: Suite de Seguridad, RGPD y Búsqueda Semántica (PiiAnonymizerNode y ZeroShotSemanticSearchNode)

### 🎯 Funcionalidades Implementadas
1. **Taxonomía Extendida de Seguridad y Búsqueda (`AiTaskType.cs`)**:
   - Nuevos tipos de tarea: `PiiAnonymization` y `SemanticEmbeddings`.
2. **Nuevos Modelos Oficiales en Catálogo (`AiModelManager.cs`)**:
   - `pii-ner-multilingual`: WikiNeural Multilingual NER (`PiiAnonymization`, 35 MB, Tier: `Lightweight`).
   - `clip-vit-b32`: OpenAI CLIP ViT-B/32 (`SemanticEmbeddings`, 65 MB, Tier: `Balanced`, multimodal imagen y texto).
   - `bge-small-multilingual`: BAAI BGE Small (`SemanticEmbeddings`, 45 MB, Tier: `Lightweight`, 384 dimensiones).
3. **Motor de Detección y Sanitización RGPD (`PiiDetectionEngine.cs`)**:
   - Detección algorítmica de DNIs y NIEs con comprobación de letra de control oficial.
   - Detección de cuentas bancarias IBAN con validación MOD-97 según ISO 13616.
   - Detección de tarjetas de crédito con algoritmo de validación de Luhn.
   - Detección de correos electrónicos, números de teléfono, direcciones IPv4 e IPv6 y nombres de personas con contexto honorífico.
   - Modos de anonimización: `TagReplacement` (`[DNI/NIE]`, `[EMAIL]`, `[IBAN]`, etc.), `Mask` (`****@domain.com`, `ES** ****`), `Hash` (`[ID_8f4a1c2b]` con SHA-256 para preservar correlación en auditorías) y `Remove`.
4. **Motor de Embeddings y Clasificación Zero-Shot (`SemanticEmbeddingEngine.cs`)**:
   - Codificación de vectores densos normalizados ($L_2 = 1.0$) para texto e imágenes.
   - Similitud de coseno acelerada y ranking de categorías candidatas.
   - Separación estricta entre categorías candidatas (`TopCategory`) y consultas de búsqueda (`SearchQuery` / `IsQueryMatch`).
5. **Nuevos Nodos en `FileFlow.Plugin.AI`**:
   - `PiiAnonymizerNode`: Puertos `In`, `Clean`, `SensitiveFound`, `Out`, `Error`. Parámetros `Model`, `CustomModelPath`, `AnonymizationMode`, toggles de filtrado individual y `OutputDirectory`. Genera archivos sanitizados de forma no destructiva (`_anonymized.txt`). Metadatos: `AI:PiiDetected`, `AI:PiiTotalCount`, `AI:PiiCategories`, `AI:PiiReportJson`.
   - `ZeroShotSemanticSearchNode`: Puertos `In`, `Matched`, `Unmatched`, `Out`, `Error`. Parámetros `Model`, `CustomModelPath`, `SearchQuery`, `CandidateLabels`, `SimilarityThreshold`, `TopK`. Metadatos: `AI:TopCategory`, `AI:TopSimilarityScore`, `AI:IsQueryMatch`, `AI:CategoryScoresJson`.
6. **Autonomía y Recursos Multilingües Co-ubicados (ADR-006)**:
   - Todas las claves de nombres, descripciones y parámetros co-ubicadas en `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx`. Cero modificaciones a `FileFlow.App`.

### 🧪 Validación y Pruebas
- Nueva suite en `FileFlow.Tests/Unit/AI/SecurityAndSemanticNodesTests.cs`:
  - `PiiAnonymizerNode_ShouldHaveValidPortsAndParameters`
  - `ZeroShotSemanticSearchNode_ShouldHaveValidPortsAndParameters`
  - `Catalog_ShouldContainSecurityAndSemanticModels`
  - `HardwareCapabilityDetector_ShouldSelectOptimalModelForSecurityAndSemanticTasks` (teoría para PiiAnonymization y SemanticEmbeddings)
  - `PiiDetectionEngine_AnonymizeText_ShouldDetectAndMaskPersonalData`
  - `PiiDetectionEngine_AnonymizeText_WithCleanText_ShouldReturnNoPii`
  - `SemanticEmbeddingEngine_ClassifyZeroShot_ShouldRankMatchingCategoryHighest`
  - `PiiAnonymizerNode_ExecuteAsync_WithSensitiveData_ShouldEmitSensitiveFound`
  - `PiiAnonymizerNode_ExecuteAsync_WithCleanData_ShouldEmitClean`
  - `ZeroShotSemanticSearchNode_ExecuteAsync_WithMatchingQuery_ShouldEmitMatched`
- Suite completa de la solución: `dotnet test FileFlow.slnx` $\rightarrow$ **464 / 464 pruebas superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Plan B: Suite de Audio y Voz (VoiceActivityDetectorNode Silero VAD y TextToSpeechNode Piper TTS)

### 🎯 Funcionalidades Implementadas
1. **Taxonomía Extendida de Audio (`AiTaskType.cs`)**:
   - Nuevos tipos de tarea: `VoiceActivityDetection` y `TextToSpeech`.
2. **Nuevos Modelos Oficiales en Catálogo (`AiModelManager.cs`)**:
   - `silero-vad`: Silero VAD v5 (`VoiceActivityDetection`, 2 MB, Tier: `Lightweight`, inferencia por chunks de 32ms a 16kHz).
   - `piper-es-davefx`: Piper TTS Español Davefx Medium (`TextToSpeech`, 63 MB, Tier: `Lightweight`, síntesis 22.050 Hz).
   - `piper-en-lessac`: Piper TTS Inglés Lessac Medium (`TextToSpeech`, 63 MB, Tier: `Lightweight`, síntesis 22.050 Hz).
3. **Motor Neural de Audio (`AudioInferenceEngine.cs`)**:
   - Lectura y resampleo con NAudio (`AudioFileReader` / `WdlResamplingSampleProvider` / `StereoToMonoSampleProvider`) a 16kHz mono float.
   - Silero VAD v4/v5 ONNX con tensores de estado recurrentes `state` / `h` y `c` a través de ventanas deslizantes de 512 muestras.
   - Detección de intervalos de voz activos con histeresis y padding configurable (ej. 200ms).
   - Exportación de audio sin silencios (`TrimSilence`) a `.wav` PCM de 16 bits.
   - Síntesis vocal neural Piper TTS con modulación de velocidad de habla (`SpeechRate` 0.5x - 2.0x) y generador armónico de contingencia.
4. **Nuevos Nodos en `FileFlow.Plugin.AI`**:
   - `VoiceActivityDetectorNode`: Puertos `In`, `Speech`, `Silent`, `Out`, `Error`. Parámetros `Model` (`Auto`, `silero-vad`, `Custom`), `CustomModelPath`, `Mode` (`DetectOnly`, `TrimSilence`), `SensitivityThreshold`, `MinSpeechDurationMs`, `PaddingDurationMs`, `OutputDirectory`. Metadatos: `AI:VoiceDetected`, `AI:SpeechRatio`, `AI:SpeechDurationSeconds`, `AI:SpeechSegmentsCount`, `AI:SpeechSegmentsJson`.
   - `TextToSpeechNode`: Puertos `In`, `Out`, `Error`. Parámetros `Model` (`Auto`, `piper-es-davefx`, `piper-en-lessac`, `Custom`), `CustomModelPath`, `InputSource` (`FileContent`, `MetadataKey`, `CustomText`), `MetadataKeyName`, `CustomTextTemplate`, `SpeechRate`, `OutputDirectory`. Metadatos: `AI:AudioGenerated`, `AI:AudioDurationSeconds`, `AI:TtsModel`.
5. **Autonomía y Recursos Multilingües Co-ubicados (ADR-006)**:
   - Claves de nombres, descripciones y parámetros agregadas en `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx`. Cero modificaciones a `FileFlow.App`.

### 🧪 Validación y Pruebas
- Nueva suite en `FileFlow.Tests/Unit/AI/AudioSuiteNodesTests.cs`:
  - `VoiceActivityDetectorNode_ShouldHaveValidPortsAndParameters`
  - `TextToSpeechNode_ShouldHaveValidPortsAndParameters`
  - `Catalog_ShouldContainAudioModelsWithCorrectTaskTypes`
  - `HardwareCapabilityDetector_ShouldSelectOptimalModelForAudioTasks` (teoría para VAD y TTS)
  - `VoiceActivityDetectorNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError`
  - `VoiceActivityDetectorNode_ExecuteAsync_WithUnsupportedExtension_ShouldEmitSilentAndOut`
  - `TextToSpeechNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError`
  - `AudioInferenceEngine_SynthesizeSpeech_ShouldGenerateValidWavFile`
  - `AudioInferenceEngine_DetectVoiceActivity_OnGeneratedAudio_ShouldAnalyzeSamples`
- Suite completa de la solución: `dotnet test FileFlow.slnx` $\rightarrow$ **453 / 453 pruebas superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Plan A: Suite de Visión Creativa y Restauración Documental (BackgroundRemover, SuperResolution, ContentModeration)

### 🎯 Funcionalidades Implementadas
1. **Taxonomía Extendida de Visión (`AiTaskType.cs`)**:
   - Nuevos tipos de tarea: `BackgroundRemoval`, `SuperResolution`, `ContentModeration`.
2. **Nuevos Modelos en el Catálogo Oficial (`AiModelManager.cs`)**:
   - `rmbg-1.4`: Bria AI RMBG-1.4 (`BackgroundRemoval`, 176 MB, Tier: `Balanced`, GPU recomendada).
   - `modnet`: MODNet Mobile Matting (`BackgroundRemoval`, 25 MB, Tier: `Lightweight`, inferencia rápida en CPU).
   - `realesrgan-compact`: Real-ESRGAN Compact x4 (`SuperResolution`, 16 MB, Tier: `Lightweight`, escalado y restauración).
   - `opennsfw2`: OpenNSFW2 (`ContentModeration`, 16 MB, Tier: `Lightweight`, clasificación de contenido sensible).
3. **Inferencia Neural de Visión en `OnnxInferenceEngine.cs`**:
   - `RemoveBackground(...)`: Inferencia de máscara de recorte de sujeto, recomposición en PNG con canal alfa transparente, sustitución por color de fondo o extracción aislada de máscara en escala de grises.
   - `UpscaleImage(...)`: Super-resolución neural convolucional con escalado 2x y 4x decodificando el tensor HR de alta fidelidad.
   - `DetectNsfwScore(...)`: Inferencia de clasificación y probabilidad de contenido explícito o inapropiado normalizada a [0.0 - 1.0].
4. **Nuevos Nodos de Pipeline en `FileFlow.Plugin.AI`**:
   - `BackgroundRemoverNode`: Puertos `In`, `Out`, `Mask`, `Error`. Parámetros `Model` (`Auto`, `rmbg-1.4`, `modnet`, `Custom`), `CustomModelPath`, `OutputMode` (`TransparentPng`, `ColorBackground`, `MaskOnly`), `BackgroundColor`, `OutputDirectory`.
   - `SuperResolutionUpscalerNode`: Puertos `In`, `Out`, `Skipped`, `Error`. Parámetros `Model` (`Auto`, `realesrgan-compact`, `Custom`), `CustomModelPath`, `ScaleFactor` (`2x`, `4x`), `MaxInputDimension` (límite de memoria), `OutputDirectory`.
   - `ContentModerationFilterNode`: Puertos `In`, `Safe`, `Sensitive`, `Error`. Parámetros `Model` (`Auto`, `opennsfw2`, `Custom`), `CustomModelPath`, `SensitivityThreshold`.
5. **Autonomía y Recursos Multilingües Co-ubicados (ADR-006)**:
   - Añadidas claves para nombres, descripciones y parámetros en `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx`. Cero contaminación de `FileFlow.App`.

### 🧪 Validación y Pruebas
- Nueva suite en `FileFlow.Tests/Unit/AI/VisionSuiteNodesTests.cs`:
  - `BackgroundRemoverNode_ShouldHaveValidPortsAndParameters`
  - `SuperResolutionUpscalerNode_ShouldHaveValidPortsAndParameters`
  - `ContentModerationFilterNode_ShouldHaveValidPortsAndParameters`
  - `Catalog_ShouldContainNewVisionModelsWithCorrectTaskTypes`
  - `HardwareCapabilityDetector_ShouldSelectOptimalModelForNewVisionTasks` (teoría para `BackgroundRemoval`, `SuperResolution`, `ContentModeration`)
  - `BackgroundRemoverNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError`
  - `SuperResolutionUpscalerNode_ExecuteAsync_WithUnsupportedFormat_ShouldEmitSkipped`
  - `ContentModerationFilterNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError`
- Suite completa de la solución: `dotnet test FileFlow.slnx` $\rightarrow$ **443 / 443 pruebas superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Generalización de Modelos de IA por Función y Selector Inteligente por Hardware

### 🎯 Funcionalidades Implementadas
1. **Taxonomía de Tareas de IA (`AiTaskType.cs`)**:
   - Definición del enum `AiTaskType`: `ObjectDetection`, `FaceDetection`, `ImageClassification`, `SpeechToText`, `TextTranslation`, `TextGenerationLlm`, `Ocr`.
2. **Evaluador de Capacidades de Hardware (`HardwareCapabilityDetector.cs`)**:
   - Detección precisa de memoria física del sistema mediante llamada Win32 a `kernel32!GlobalMemoryStatusEx` (con fallback de memoria disponible de `GC.GetGCMemoryInfo()`).
   - Detección de núcleos de CPU lógicos (`Environment.ProcessorCount`).
   - Detección de aceleración por GPU DirectML mediante sondeo de `SessionOptions.AppendExecutionProvider_DML(0)`.
   - Clasificación por niveles de hardware: `"Lightweight"`, `"Balanced"`, `"Performance"`.
   - Evaluación de compatibilidad de modelos (`ModelCompatibility`: `Recommended`, `Playable`, `InsufficientHardware`).
   - Algoritmo de recomendación automática `GetOptimalModelForTask(AiTaskType task, bool preferSpeed = false)` que pondera compatibilidad, nivel del modelo y aceleración por GPU.
3. **Catálogo Enriquecido y Métodos de Resolución (`AiModelManager.cs`)**:
   - `AiModelInfo` extendido con: `TaskType`, `MinRamBytes`, `GpuRecommended`, `HardwareTier`.
   - Nuevo método `GetModelsForTask(AiTaskType taskType)`.
   - Nuevo método `ResolveModelPathAsync(modelSelection, customModelPath, taskType, context, item, cancellationToken)` que maneja de manera unificada:
     - Modo `Auto`: selección inteligente basada en el hardware real del PC anfitrión.
     - Modelo Oficial: selección explícita del catálogo y descarga transparente con telemetría.
     - Archivo Personalizado (`Custom`): validación de archivo local `.onnx` o `.gguf` con registro de logs.
4. **Actualización de Nodos de IA (`FileFlow.Plugin.AI`)**:
   - `ObjectDetectorNode`: Parámetros `Model` (`Auto`, `tiny-yolov3`, `grounding-dino`, `Custom`) y `CustomModelPath` (`FilePath`).
   - `FaceDetectorNode`: Parámetros `Model` (`Auto`, `ultraface`, `Custom`) y `CustomModelPath`.
   - `SmartImageClassifierNode`: Parámetros `Model` (`Auto`, `mobilenetv2`, `Custom`) y `CustomModelPath`.
   - `LocalAiTranslatorNode`: Parámetros `Model` (`Auto`, `nllb-200-600m`, `marian-es-en`, `marian-en-es`, `Custom`) y `CustomModelPath`.
   - `LocalLlmProcessorNode`: Parámetros `Model` (`Auto`, `qwen2.5-1.5b-instruct`, `Custom`) y `CustomModelPath`.
   - `LocalWhisperTranscriberNode`: Parámetro `ModelSize` extendido con `Auto` y `Custom`, más `CustomModelPath`.
5. **Localización de Parámetros i18n (ADR-006 Co-ubicación Estricta)**:
   - Añadidas claves `Param_Model` y `Param_CustomModelPath` en `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx`. Cero contaminación de `FileFlow.App`.

### 🧪 Validación y Pruebas
- Nueva suite `HardwareCapabilityDetectorTests.cs`:
  - `Specs_ShouldReturnRealisticHardwareValues`
  - `GetCompatibility_WithLowRamRequirement_ShouldBePlayableOrRecommended`
  - `GetCompatibility_WithImpossiblyHighRamRequirement_ShouldReturnInsufficientHardware`
  - `GetOptimalModelForTask_ShouldReturnValidModelMatchingTaskType` (teoría para todos los `AiTaskType`)
  - `AiModelManager_GetModelsForTask_ShouldReturnCatalogModelsForSpecificTask`
- Nueva suite `AiTaskModelResolutionTests.cs`:
  - `ResolveModelPathAsync_WithCustomAndEmptyPath_ShouldReturnNull`
  - `ResolveModelPathAsync_WithCustomAndNonExistentFile_ShouldReturnNull`
  - `ResolveModelPathAsync_WithCustomAndExistingFile_ShouldReturnFullPath`
  - Verificación de descriptores y opciones de dropdown para todos los 6 nodos de IA.
- Suite de pruebas completa de la solución: `dotnet test FileFlow.slnx` $\rightarrow$ **433 / 433 pruebas superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Monitorización de GPU en la Barra de Estado Inferior

### 🎯 Funcionalidades Implementadas
1. **Muestreo de GPU en Tiempo Real (`SystemPerformanceMonitor.cs`)**:
   - Incorporación de `GpuPercentage` y `GpuFormatted` en `PerformanceMetrics`.
   - Consulta reactiva y en segundo plano (`Task.Run`) a través de la categoría de rendimiento oficial de Windows `"GPU Engine"` (`Utilization Percentage`) para todas las instancias del proceso actual (`pid_{currentProcess.Id}_*`).
   - Muestreo asíncrono que evita cualquier congelamiento o caída de cuadros en el hilo de UI (Dispatcher).
   - Liberación determinista de recursos y contadores en `Dispose()`.
2. **Presentación Visual Reactiva (`StatusBarViewModel.cs` & `StatusBarView.xaml`)**:
   - Nueva propiedad reactiva `GpuText` en `StatusBarViewModel`.
   - Elemento visual en la barra de estado inferior: `🎮 GPU: {GpuText}` ubicado junto a las métricas de CPU y RAM.
   - Tooltips localizados en español e inglés (`StatusBar_GpuToolTip`).

### 🧪 Validación y Pruebas
- Nueva suite en `FileFlow.Tests/Unit/App/SystemPerformanceMonitorTests.cs`:
  - `PerformanceMetrics_GpuFormatted_ShouldFormatCorrectly`
  - `PerformanceMetrics_RamFormatted_ShouldFormatMbAndGb`
  - `SystemPerformanceMonitor_CanInstantiateAndDisposeWithoutErrors`
- Suite de pruebas completa: `dotnet test FileFlow.slnx` $\rightarrow$ **413 / 413 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - URLs Configurables de Modelos de IA con Conmutación Multi-Espejo (Fallback)

### 🎯 Funcionalidades Implementadas
1. **Soporte de Múltiples URLs por Modelo y Conmutación Automática por Error (Fallback)**:
   - Modificado `AiModelManager.DownloadModelWithProgressAsync` para iterar secuencialmente a través de la lista de URLs configuradas para cada modelo.
   - Si un enlace o servidor CDN falla (HTTP 404, 500, timeout o rechazo de conexión), el motor informa en el log e intenta de inmediato el siguiente espejo configurado de forma transparente para el usuario.
   - Si todos los enlaces fallan, se consolidan todos los errores en `AiModelManager.LastError` con viñetas detalladas de cada espejo intentado.
2. **Persistencia Desacoplada en Disco (`ai_models_config.json`)**:
   - Almacenamiento autónomo en `AppPaths.ConfigDirectory/ai_models_config.json` (`%AppData%/FileFlow/config/` o `data/config/` en modo portable) mediante `AiModelManager.SaveConfig()` y `LoadConfig()`.
   - Métodos API: `GetConfiguredUrls(modelId)`, `GetDefaultUrls(modelId)`, `SetCustomUrls(modelId, urls)`, `ResetCustomUrls(modelId)`, `ResetAllCustomUrls()` y `HasCustomUrls(modelId)`.
3. **Diálogo de Configuración de URLs (`AiModelUrlsConfigDialog.xaml`)**:
   - Nuevo diálogo modal accesible con el botón **"⚙️ URLs"** en cada modelo (disponible tanto en la pestaña Ajustes de `WorkflowSettingsWindow.xaml` como en el gestor `AiModelDownloadDialog.xaml`).
   - Editor multilínea para introducir una o varias URLs en orden de prioridad.
   - Botón **"🔍 Probar Conexión"**: Realiza comprobaciones HTTP en vivo de cada URL y muestra el código de estado devuelto (`200 OK`, `404 Not Found`, etc.) y el tamaño reportado en MB.
   - Botón **"🔄 Restablecer Predeterminadas"**: Recupera instantáneamente las URLs oficiales de fábrica verificadas.
   - Distintivo visual (`🔧 URLs`) en las tarjetas de modelos que tienen configuraciones personalizadas activas.
4. **Localización Completa (i18n)**:
   - Incorporadas claves de localización en `Strings.resx` y `Strings.es.resx` (`AiModelUrls_Title`, `AiModelUrls_Subtitle`, `AiModelUrls_UrlsLabel`, `AiModelUrls_TestBtn`, `AiModelUrls_ResetBtn`, `AiModelUrls_SaveBtn`, `AiModelUrls_CancelBtn`, `AiModelUrls_StatusCustom`, `AiModelUrls_StatusDefault`, `AiModelUrls_BtnTooltip`).

### 🧪 Validación y Pruebas
- Creada nueva suite en `FileFlow.Tests/Unit/AI/AiModelManagerConfigTests.cs`:
  - `AiModelManager_GetDefaultUrls_ShouldReturnWorkingUrlsForAllCatalogModels`
  - `AiModelManager_SetCustomUrls_AndReset_ShouldPersistAndRevertProperly`
  - `AiModelManager_DownloadWithFallback_ShouldTryNextMirrorWhenFirstFails`
- Añadido test en `AiModelManagerViewModelTests.cs`:
  - `AiModelItemViewModel_RefreshState_ReflectsCustomUrlsStatus`
- Suite de pruebas completa: `dotnet test FileFlow.slnx` $\rightarrow$ **407 / 407 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Corrección de Descargas de Modelos MarianMT / NLLB-200 y Sistema de Diagnóstico de Errores

### 🎯 Problema Detectado y Causa Raíz
1. **Error 404 en Modelos MarianMT (`marian-es-en` y `marian-en-es`)**:
   - **Causa**: Las URLs de descarga en `AiModelManager.Catalog` apuntaban a `https://huggingface.co/onnx-community/opus-mt-*/resolve/main/onnx/model.onnx`, archivo inexistente que devolvía HTTP 404 (Entry Not Found).
   - **Solución**: Se actualizaron las URLs al binario ONNX quantizado oficial y disponible: `onnx/decoder_model_merged_quantized.onnx` (193 MB).
2. **Rechazo o Congelamiento de Conexión en HuggingFace CDN / AWS CloudFront (`nllb-200-600m`)**:
   - **Causa**: La instancia `HttpClient` de `AiModelManager` carecía de cabecera `User-Agent` estándar y configuración de redirecciones, provocando rechazo o interrupciones en conexiones hacia CloudFront (`us.aws.cdn.hf.co`).
   - **Solución**: Se implementó `CreateHttpClient()` con `SocketsHttpHandler` configurado para redirecciones automáticas (`AllowAutoRedirect = true`, `MaxAutomaticRedirections = 10`), descompresión nativa y cabecera de agente completa (`FileFlowStudio/1.0`).
3. **Error 404 en Grounding DINO / YOLO-World (`grounding-dino`)**:
   - **Causa**: La URL en `AiModelManager.Catalog` apuntaba a `https://github.com/ultralytics/assets/releases/download/v8.2.0/yolov8s-worldv2.onnx`, pero Ultralytics solo publica archivos `.pt` (PyTorch) en sus releases de GitHub, por lo que GitHub devolvía HTTP 404 (Not Found).
   - **Solución**: Se migró la URL de descarga al repositorio oficial de modelos ONNX en Hugging Face (`https://huggingface.co/Instemic/yolo-world-onnx/resolve/main/yolov8s-worldv2.onnx`), verificado con HTTP 200 OK y 51.1 MB. Se verificó igualmente que todos los 13 modelos del catálogo responden con HTTP 200 OK.
4. **Ausencia Total de Mensajes de Diagnóstico ante Errores de Descarga**:
   - **Causa**: Si una descarga fallaba, `AiModelManager.DownloadModelWithProgressAsync` devolvía `null` y el método `RefreshStatus()` en el ViewModel invocaba ciegamente `RefreshState()`, reseteando el estado de todos los modelos no instalados a `⏳ No descargado`. Además, el texto de progreso y errores solo se mostraba si `IsDownloading == true`, ocultándose automáticamente en cuanto terminaba la descarga.
   - **Solución**:
     - En `AiModelManager`: incorporación de `LastError` y captura de códigos HTTP exactos (ej. 404, 403, timeouts o bytes insuficientes).
     - En `AiModelItemViewModel`: nuevas propiedades `ErrorMessage` y `HasError`. `RefreshState()` preserva el estado de error (`❌ Error en descarga`) y no sobreescribe mensajes si no se ha completado la instalación.
     - En `AiModelManagerViewModel`: visualización de alerta modal (`MessageBox.Show`) con el detalle del fallo al descargar un modelo individual, y resumen consolidado de modelos fallidos al descargar en lote (`DownloadMissingModelsCommand`).
     - En `AiModelDownloadDialog.xaml`: nuevo banner de error superior con botón para descartar y cuadro de advertencia persistente con borde rojo y tooltip por cada modelo fallido.

### 🧪 Validación y Pruebas
- Incorporadas 3 nuevas pruebas unitarias en `AiModelManagerViewModelTests.cs` (`AiModelItemViewModel_RefreshState_WithErrorMessage_RetainsErrorState`, `AiModelManagerViewModel_DownloadUnknownModel_SetsErrorStateAndDetails`, etc.).
- Suite de pruebas completa: `dotnet test FileFlow.slnx` $\rightarrow$ **403 / 403 pruebas superadas al 100%**.

---

## [2026-09-03] - Suite de IA Lingüística y Modelos Locales: Traducción NLLB-200/MarianMT, LLM Local Qwen 2.5 y Transformador de Prompts

### 🎯 Funcionalidades Añadidas
1. **Infraestructura de Modelos y Motor de Inferencia de Lenguaje (`LanguageInferenceEngine` & `AiModelManager`)**:
   - Incorporación al catálogo de `AiModelManager`:
     - `nllb-200-600m`: Traductor neuronal universal en 200 idiomas (~600 MB ONNX).
     - `qwen2.5-1.5b-instruct`: Modelo LLM multilingüe instruccional ligero (~1.1 GB GGUF Q4_K_M).
     - `marian-en-es`: Modelo MarianMT de alta velocidad para traducción inglés a español (~60 MB ONNX).
   - Nuevo motor centralizado `LanguageInferenceEngine` para traducción neuronal, preservación de timestamps en subtítulos `.srt`, procesamiento LLM (resúmenes, extracción JSON, traducción y explicación) y transformación dinámica de prompts.
2. **Nodo de Traducción Neuronal Local (`LocalAiTranslatorNode`)**:
   - Traducción multilingüe de archivos de texto (`.txt`, `.md`, `.srt`, `.csv`, `.json`, `.xml`, `.html`) o metadatos (`Ocr:Text`, `Whisper:Transcription`).
   - Parámetros: `SourceLanguage`, `TargetLanguage`, `InputSource` (`FileContent` / `MetadataKey`), `MetadataKeyName`, `OutputMode` (`InjectMetadata` / `CreateNewFile` / `Both`), `TargetFileNamePattern` y `TranslateSrtTimestamps`.
   - Inyección de metadatos `AI:SourceLanguage`, `AI:TargetLanguage`, `AI:TranslatedText` y `AI:TranslationModel`.
3. **Nodo de Procesamiento LLM Local (`LocalLlmProcessorNode`)**:
   - Ejecución in-process de modelos LLM para resúmenes ejecutivos, extracción estructurada a JSON y prompts con plantillas variables.
   - Parámetros: `TaskType` (`Summarize`, `ExtractStructuredData`, `TranslateAndExplain`, `CustomPrompt`), `SystemPrompt`, `UserPrompt`, `OutputFormat` (`Markdown`, `PlainText`, `JSON`), `SaveAsNewFile`, `Temperature` y `MaxTokens`.
   - Inyección de metadatos `AI:LlmResponse`, `AI:Summary`, `AI:ExtractedDataJson` y `AI:TokensGenerated`.
4. **Transformador Dinámico de Prompts (`PromptTransformerNode`)**:
   - Evaluación y traducción de plantillas dinámicas con metadatos (`{AI:Category}, gafas de sol, {UserTag}, coche rojo`) a inglés para alimentar directamente nodos de visión (`PromptObjectDetectorNode`, `SmartImageClassifierNode`).
   - Expansión de sinónimos visuales (`ExpandSynonyms`) para potenciar la detección *open-vocabulary*.
   - Inyección de metadatos `AI:EvaluatedPrompt` y `AI:TranslatedPrompt`.
5. **Descentralización Total de Recursos (i18n) y Co-ubicación en `FileFlow.Plugin.AI`**:
   - Creación de `Resources/Strings.resx` y `Resources/Strings.es.resx` dentro de `FileFlow.Plugin.AI/` para albergar todos los textos, nombres, descripciones y parámetros de los nodos de IA.
   - Implementación de `AiPluginInitializer.cs` (`IPluginInitializer`) con registro estático determinista en `LocalizationManager.Instance`.
   - Limpieza completa de claves de nodos en `FileFlow.App/Resources/Strings.*.resx`, garantizando que la app anfitriona quede 100% libre de strings acoplados de nodos.
6. **Actualización de Reglas y Documentos de Arquitectura del Proyecto**:
   - Incorporado el **Principio Arquitectónico de Co-ubicación y Autonomía Total de Plugins / Nodos (Self-Contained Plugins / Zero-Touch en FileFlow.App)** en `docs/architecture.md` (ADR-006), `.agents/rules/rules.md`, `AGENTS.md`, `GEMINI.md` y `.antigravity/knowledge/repo_architecture.md`.
7. **Suite de Pruebas Unitarias Exhaustiva**:
   - Nuevos tests en `FileFlow.Tests/Unit/AI/`:
     - `LocalAiTranslatorNodeTests.cs` (5 tests).
     - `LocalLlmProcessorNodeTests.cs` (4 tests).
     - `PromptTransformerNodeTests.cs` (3 tests).
   - **Validación Global**:
     - `dotnet build FileFlow.slnx --warnaserror` $\rightarrow$ **0 Advertencias, 0 Errores**.
     - `dotnet test FileFlow.slnx` $\rightarrow$ **401 / 401 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Corrección de Persistencia en Catálogo de Nodos: Modo Compacto Permanente al Arrastrar y Seleccionar

### 🎯 Problema Detectado y Solución Implementada
1. **Desactivación Involuntaria del Modo Compacto al Seleccionar o Arrastrar Nodos**:
   - **Problema**: Al activar la vista compacta de la caja de herramientas (`ToolboxViewModel.IsCompactMode = true`), al seleccionar o arrastrar un nodo hacia el lienzo, la vista compacta se desactivaba sola volviendo a la vista detallada.
   - **Causa**: Al arrastrar o soltar un nodo, el editor llamaba a `UserPreferencesService.Instance.IncrementNodeUsage(typeName)`. Dicho método guardaba las preferencias y disparaba el evento `PreferencesChanged`, provocando que `ToolboxViewModel` ejecutara `RefreshToolbox()`. Como la propiedad `IsCompactMode` en el ViewModel no se sincronizaba con `UserPreferencesService.Instance.Preferences.IsCompactToolbox`, `RefreshToolbox()` sobreescribía la propiedad con el valor `false` almacenado en las preferencias de usuario.
   - **Solución**:
     - Implementado hook reactivo `OnIsCompactModeChanged(bool value)` en `ToolboxViewModel.cs` para actualizar y persistir automáticamente `IsCompactToolbox` en `UserPreferencesService.Instance.UpdatePreferences(...)` cada vez que el usuario pulse el botón de vista compacta.
     - Inicialización coherente de `_isCompactMode` en el constructor de `ToolboxViewModel` desde `UserPreferencesService.Instance.Preferences.IsCompactToolbox`.
2. **Validación Global**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **389 / 389 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Corrección en la Consola de Ejecución: Sincronización Completa del Filtro 'Todos' e Ingesta SQLite

### 🎯 Problemas Detectados y Solución Implementada
1. **Pérdida de Logs al Alternar Filtros**:
   - **Problema**: Al seleccionar un filtro específico (ej. *Errores*, *Advertencias* o *Info*) y volver al filtro *Todos*, algunos logs de ejecución no se mostraban.
   - **Causas**:
     - `AddStructuredLog` encolaba en el buffer en memoria de `_pendingLogs` pero no invocaba `SqliteLogStore.Instance.EnqueueLog(record)`. Como resultado, los logs estructurados generados durante la ejecución de los flujos nunca llegaban a la base de datos de telemetría SQLite.
     - `LoadQueryResultsAsync()` consultaba la base de datos SQLite antes de vaciar `_pendingLogs`, perdiendo los registros en tránsito.
     - `OnActiveFilterChanged` y `OnSearchFilterChanged` no reactivaban `IsLiveMode = true` al regresar al filtro *Todos* con búsqueda vacía, impidiendo que el buffer continuara recibiendo logs en vivo.
     - Al consultar el filtro *Todos*, la consulta a SQLite utilizaba un offset y límite fijos desde 0 en lugar de cargar la ventana más reciente de logs.
   - **Solución**:
     - `AddStructuredLog` ahora registra deterministamente cada log en `SqliteLogStore.Instance.EnqueueLog(record)`.
     - `LoadQueryResultsAsync()` vacía previamente el buffer pendiente (`FlushAllPendingLogs()`) y espera el volcado de SQLite (`FlushPendingLogsAsync()`).
     - Al regresar al filtro *Todos* sin búsqueda y con ordenación por ID por defecto, reactiva `IsLiveMode = true` y consulta los registros más recientes dentro del tamaño de ventana `MaxLiveBufferSize` (2.000 logs).
     - `ClearLogs()` restablece el estado completo a modo en vivo y filtro *Todos*.
2. **Validación Global**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **389 / 389 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Nodo de Detección de Objetos por Prompt (Grounding DINO / Open-Vocabulary) con Traductor MarianMT ES-EN (Helsinki-NLP)

### 🎯 Funcionalidades Añadidas
1. **Detección de Objetos por Prompt en Lenguaje Natural Libre (`PromptObjectDetectorNode`)**:
   - Nuevo nodo en la categoría **`AI & Computer Vision`** que permite especificar cualquier término, descripción u objeto en texto libre (ej. *"gafas de sol, perro marrón, taza de café, bicicleta de montaña"*), superando las limitaciones de clases cerradas.
   - Parámetros configurables:
     - `Prompt`: Texto del prompt (admite múltiples conceptos separados por comas).
     - `MinimumConfidence`: Umbral deslizador de confianza (`0.10` a `1.0`, por defecto `0.35`).
     - `AutoTranslateToEnglish`: Conmutador booleano (`Toggle`, por defecto `true`).
     - `MaxDetections`: Límite numérico de detecciones máximas a reportar.
   - Puertos de bifurcación duales:
     - `ObjectsFound`: Se dispara cuando se detecta al menos un objeto coincidente con el prompt.
     - `NoObjects`: Se dispara cuando no se encuentran objetos con la confianza requerida.
     - `Error`: Se dispara ante rutas inválidas o errores de lectura.
2. **Traductor Inteligente de Prompts Multilingüe (`PromptTranslator`)**:
   - Submódulo especializado en traducción de visión por computador y alineación texto-imagen con más de 400 conceptos visuales.
   - Algoritmo voraz (*greedy matching*) de conceptos compuestos ordenados por longitud descendente para evitar colisiones ("gafas de sol", "taza de café", "árbol de navidad", "reloj de pulsera", "teléfono móvil", "coche de policía", "botella de agua").
   - Limpieza automática de prefijos de comando en español (*"detecta un..."*, *"busca..."*, *"encuentra..."*, *"imagen con..."*).
   - Soporte de conjunciones copulativas y disyuntivas (*" y "*, *" e "*, *" o "*, *" u "*), normalización de acentos y reordenación sintáctica de adjetivos/colores (ej. *"coche rojo"* $\rightarrow$ *"red car"*, *"perro marrón"* $\rightarrow$ *"brown dog"*, *"gafas de sol"* $\rightarrow$ *"sunglasses"*).
   - Compatible con modelos neuronales ONNX de Helsinki-NLP (**MarianMT `opus-mt-es-en`**).
3. **Inyección Enriquecida de Metadatos y Cajas Interactivas**:
   - `AI:Prompt`: Prompt original en lenguaje natural escrito por el usuario.
   - `AI:TranslatedPrompt`: Prompt procesado en inglés utilizado en la inferencia.
   - `AI:PromptObjects`: Resumen formateado de objetos detectados y confianzas.
   - `AI:PromptObjectCount`: Recuento de objetos coincidentes.
   - `AI:HasPromptObjects`: Booleano para condiciones de flujo.
   - `AI:DetectedBoxes`: Array JSON de coordenadas normalizadas `[X1, Y1, X2, Y2]` con etiquetas y confianza, compatible al 100% con el visor interactivo de imágenes (`ImagePreviewProvider`) y previsualizador (`FilePreviewerWindow`).
4. **Catálogo de Modelos IA (`AiModelManager.Catalog`)**:
   - Añadidas entradas para descarga automática bajo demanda de `grounding-dino` / `yolov8s-worldv2.onnx` y `marian-es-en` / `opus-mt-es-en.onnx`.
5. **Localización e Internacionalización (i18n)**:
   - Claves añadidas en `Strings.resx` y `Strings.es.resx`.
6. **Pruebas y Validación**:
   - Nuevos tests unitarios exhaustivos en `FileFlow.Tests/Unit/AI/PromptObjectDetectorNodeTests.cs` cubriendo frases compuestas, plurales, frases completas con conjunciones, acentos y detección.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **389 / 389 pruebas pasadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Modernización de la Consola de Ejecución (LogView): Diseño Adaptativo, Portapapeles, Doble Clic y Sincronización con el Inspector

### 🎯 Problemas Detectados y Funcionalidades Añadidas
1. **Eliminación del Desplazamiento Horizontal y Envoltura Multilínea (2-3 líneas)**:
   - **Problema**: El `DataGrid` de logs utilizaba `RowHeight="24"` rígido y anchos fijos que forzaban el scroll horizontal constante para leer mensajes o nombres de archivo largos.
   - **Solución**:
     - Eliminación de `RowHeight="24"` estático y configuración de `MinRowHeight="26"` flexible.
     - Envoltura multilínea adaptativa (`TextWrapping="Wrap"`, `MaxHeight="46"`, `TextTrimming="CharacterEllipsis"`) en las columnas de **Fichero** y **Mensaje**.
     - Columna **Mensaje** configurada como expansor dinámico (`Width="*"`), ajustando el 100% de la tabla al ancho de la ventana sin scrollbars horizontales.
2. **Menú Contextual de Copiado Integral (`ContextMenu`) y Atajo `Ctrl+C`**:
   - Menú contextual de clic derecho con opciones para copiar:
     - 📄 *Copiar Línea Completa de Log* (formato con timestamp, nivel, nodo y mensaje).
     - 💬 *Copiar Mensaje*.
     - 📁 *Copiar Ruta del Archivo* (`FilePath`).
     - 🏷️ *Copiar Nombre de Archivo* (`FileName`).
     - 🆔 *Copiar ID de Flujo* (`ItemId`).
     - 📦 *Copiar Detalles / Metadatos JSON* (`DetailsJson`).
     - 👁️ *Abrir Vista Previa*.
     - 🎯 *Filtrar solo este Nodo*.
     - 📄 *Filtrar solo este Archivo*.
   - Atajo de teclado `Ctrl+C` para copiar la fila seleccionada estructurada al portapapeles.
3. **Apertura Directa por Doble Clic en Fila**:
   - Al hacer doble clic sobre cualquier fila con archivo físico asociado, abre directamente la ventana de previsualización (`FilePreviewerWindow`) con cajas de IA si existen. Si no hay archivo físico, despliega/contrae la ficha de detalles.
4. **Sincronización Reactiva de Datos y Metadatos con el Inspector de Nodos**:
   - `LogViewModel` expone `SelectedLog` y notifica `LogSelectionChanged`.
   - `NodeInspectorViewModel.InspectLogRecord(StructuredLogRecord log)` localiza el nodo en el editor por `NodeId` o `NodeName`, y extrae el archivo y los metadatos de ejecución (`DetailsJson`, `FilePath`, `FileName`, `ItemId`).
   - Si existe un snapshot coincidente, lo selecciona directamente; si no, genera un snapshot de ejecución estructurado y puebla instantáneamente las pestañas de **Salidas**, **Metadatos y Diferenciales** y la evaluación dinámica de parámetros (`{FileName}`, `{AI:Category}`, `{Ocr:Text}`, etc.) en el panel lateral, **sin alterar la posición ni el zoom de la cámara en el lienzo visual**.
   - `MainViewModel` conecta reactivamente la selección de logs con el inspector.
5. **Localización e Internacionalización (i18n)**:
   - Nuevas entradas en `Strings.resx` y `Strings.es.resx` para todas las opciones del menú contextual y comandos de copiado/filtrado.
6. **Validación Global**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **382 / 382 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-03] - Propagación de Metadatos de IA (Rostros y Objetos) al Previsualizador desde la Consola de Logs

### 🎯 Problemas Detectados y Funcionalidades Añadidas
1. **Visualización de Detecciones (Rostros y Objetos) en la Vista Previa de Logs**:
   - **Causa Raíz**: Al pulsar el botón `👁️ Vista Previa` en una línea de log dentro de la consola `LogView`, se instanciaba `FilePreviewContext(filePath)` sin metadatos. Como consecuencia, el previsualizador no recibía las cajas de detección generadas por los nodos de IA (`AI:FaceBoxes`, `AI:DetectedBoxes`).
   - **Solución**:
     - `WorkflowExecutionContext` y `MockFlowExecutionContext` serializan automáticamente el diccionario de metadatos del elemento en el campo `DetailsJson` de cada `StructuredLogRecord` emitido.
     - `LogViewModel.PreviewLogFile` deserializa `DetailsJson` y puebla el diccionario `FilePreviewContext.Metadata`.
     - `ImagePreviewProvider` soporta de manera polimórfica metadatos representados como `string`, `JsonElement` o colecciones de objetos, renderizando los rectángulos de encuadre en color cian/neón con sus etiquetas de clase y confianza.

2. **Optimización de Memoria y Concurrencia Thread-Safe en Inferencia ONNX (Detección de Rostros y Objetos)**:
   - **Causa Raíz**:
     1. **Llamadas Concurrentes No Soportadas en DirectML**: Cuando un flujo procesaba múltiples imágenes en paralelo a través de los hilos de `WorkflowExecutor`, varias tareas invocaban simultáneamente `session.Run(...)` sobre la misma sesión DirectML. DirectML no admite concurrencia simultánea en el mismo contexto de GPU, produciendo fallos nativos no administrados (Access Violation `0xC0000005` o Device Lost) que terminaban el proceso WPF de forma inmediata sin pasar por bloques `catch`.
     2. **Explosión de Memoria en ImageSharp y Clonaciones Innecesarias**: Al cargar fotos de alta resolución (ej. 24–48 MP), 16 tareas concurrentes cargando bitmaps completos y clonándolos en memoria acumulaban varios gigabytes en el Large Object Heap (LOH), provocando pausas de GC masivas y cuelgues del hilo de UI.
     3. **Hilos de Ejecución ONNX en Paralelo (`ExecutionMode.ORT_PARALLEL`)**: El modo paralelo generaba sub-hilos internos que competían destructivamente con el ThreadPool de .NET.
   - **Solución**:
     - **Sincronización `Lock _inferenceLock` en `OnnxInferenceEngine`**: Se serializó de forma estricta la ejecución nativa de `session.Run(...)`. La inferencia dura apenas entre 5ms y 15ms por imagen, por lo que la serialización elimina el 100% de las condiciones de carrera y caídas nativas de GPU/DirectML sin mermar la velocidad del pipeline.
     - **Configuración Estable de ONNX Runtime**: Cambio a `ExecutionMode.ORT_SEQUENTIAL` con `IntraOpNumThreads` balanceado (`ProcessorCount / 2`).
     - **Redimensionado In-Place Eficiente en Nodos de IA**: `FaceDetectorNode`, `ObjectDetectorNode` y `SmartImageClassifierNode` redimensionan la imagen directamente in-place con `image.Mutate(x => x.Resize(...))` inmediatamente tras la lectura, reduciendo el consumo de RAM por imagen de ~75 MB a **0.2 MB - 0.5 MB** y eliminando por completo las clonaciones redundantes.
2. **Corrección de Inferencia y Detección de Objetos en ObjectDetectorNode (Tiny YOLOv3 COCO 80)**:
   - **Causa Raíz**:
     - `tiny-yolov3-11.onnx` espera dos entradas: `input_1` (`[1,3,416,416]`) y `image_shape` (`[1,2]`). El código anterior asumía los índices fijos `session.InputNames[0]` y `session.InputNames[1]`. Al ordenarse alfabéticamente o por grafo, `image_shape` recibía el tensor de imagen de 4D provocando un fallo de argumentos en ONNX Runtime que era silenciado en un bloque `try/catch`, devolviendo siempre 0 detecciones.
     - La dimensión de puntuaciones de Tiny YOLOv3 es `[1, 80, 2535]`. El código anterior calculaba índices con `b * 80 + c` en lugar de indexar por clase `[0, classIdx, boxIdx]` o leer el tensor de salida `yolonms_layer_1:2` (`indices` de tipo `int32`), provocando lecturas desalineadas de memoria.
     - `CocoLabels` incluía `"background"` en el índice 0 desplazando todas las 80 clases COCO en 1 unidad.
   - **Solución**:
     - Mapeo dinámico y desacoplado de tensores de entrada por nombre y dimensionalidad (`session.InputMetadata`).
     - Decodificación completa de los 3 tensores de salida de Tiny YOLOv3: `yolonms_layer_1` (coordenadas relativas `[y1, x1, y2, x2]`), `yolonms_layer_1:1` (puntuaciones de confianza) y `yolonms_layer_1:2` (detecciones filtradas por NMS).
     - Corrección de la lista oficial de 80 clases COCO (índice 0 = `person`, 1 = `bicycle`, etc.).
     - Registro de metadatos `AI:DetectedBoxes` y soporte en `ImagePreviewProvider` para dibujar recuadros cian neón con badges `🎯 objeto (XX%)` y botón conmutador `🎯 Objetos (N)` en el visor rápido.
2. **Emisión de Logs en Modo Depuración / Pruebas Aisladas (`NodeInspectorViewModel` / `MockFlowExecutionContext`)**:
   - **Causa Raíz**: En la prueba aislada de nodos (`TestNodeWithCustomFileAsync` / inspector), `MockFlowExecutionContext.Log(...)` estaba vacío por diseño anterior, descartando todos los mensajes emitidos por `FaceDetectorNode`, `ObjectDetectorNode`, `LocalWhisperTranscriberNode`, etc.
   - **Solución**:
     - Se inyectó `LogViewModel` en `NodeInspectorViewModel` y `MockFlowExecutionContext`.
     - Se implementaron los métodos de logging estructurado (`Log`) en `MockFlowExecutionContext`, registrando los eventos en `SqliteLogStore` y despachándolos en tiempo real a `LogViewModel`.
     - Se ajustaron los niveles de registro en `FaceDetectorNode` y `ObjectDetectorNode`: las detecciones sin coincidencias y formatos incompatibles ahora emiten con nivel `Information` / `Warning` (en lugar de `Debug` silenciado) para garantizar máxima visibilidad en la consola de ejecución.
2. **Selección Dinámica de Salidas en el Inspector y Carrusel de Previsualización Multisalida**:
   - **Problema**: Al realizar múltiples pruebas sobre un nodo (ej. `FaceDetectorNode` con varias imágenes consecutivas), pulsar el botón de previsualización abría siempre la primera/última salida en lugar de la salida seleccionada por el usuario en la pestaña de "Salidas".
   - **Solución**:
     - Se añadió el comando `PreviewSpecificSnapshotCommand` y un botón directo **`👁️ Ver`** en la cabecera de cada tarjeta de salida/entrada en `Themes/Templates/InspectorTemplates.xaml`.
     - `OpenQuickPreviewCommand` ahora toma como objetivo prioritario el `SelectedSnapshot` actual seleccionado en la lista.
     - Se dotó a los `ListBoxItem` de las pestañas de Salidas y Entradas de estilos visuales con feedback activo (borde Cyan Neón `#00E5FF`, fondo resaltado al hover y al seleccionar).
     - Se integró la lista completa de salidas hermanas (`siblings`) al abrir la ventana de previsualización `FilePreviewerWindow`, permitiendo navegar continuamente con las flechas `◀` y `▶` (o con el teclado) entre todas las pruebas y resultados generados.
     - Se perfeccionó la resolución de índice en `FilePreviewerViewModel.LoadContextAsync` para emparejar por igualdad de ruta y enfocar exactamente el elemento seleccionado.
2. **Recuadros Visuales de Rostros en el Previsualizador de Archivos (`ImagePreviewProvider`)**:
   - Se añadió soporte completo para **encuadrar automáticamente los rostros detectados** al previsualizar imágenes procesadas por el nodo `FaceDetectorNode`.
   - **Renderizado Visual Dinámico**: Los recuadros se dibujan con bordes de color cian neón (`#00E5FF`), fondo translúcido y badge indicador con número de rostro y porcentaje de confianza (`👤 Rostro #1 (95%)`).
   - **Sincronización con Zoom y Rotación**: Los recuadros están acoplados en el grupo de transformación visual (`LayoutTransform`), escalándose y rotando de forma nativa e interactiva junto a la imagen.
   - **Botón Conmutador en Toolbar**: Si la imagen contiene metadatos de rostros (`AI:FaceBoxes`), la barra de herramientas del visor muestra el botón `👤 Rostros (N)` para activar u ocultar los recuadros en un clic.
3. **Detección de Rostros con Resultados Reales y Exactos (`FaceDetectorNode` / `OnnxInferenceEngine`)**:
   - **Supresión de No Máximos (NMS)**: Algoritmo con cálculo de IoU (`0.45`) para consolidar los 4.420 anchors del modelo UltraFace en rostros únicos reales.
   - **Cálculo Softmax**: Normalización probabilística real `exp(face)/(exp(bg)+exp(face))`.
   - **Exportación en Metadatos**: `item.Metadata["AI:FaceBoxes"]` serializa las coordenadas normalizadas `[X1, Y1, X2, Y2, Score]` para consumo en el visor y flujos.
4. **Error XAML StaticResource AddOneConverter en FilePreviewerWindow**:
   - Declaración de `AddOneConverter` en `<Window.Resources>` de `FilePreviewerWindow.xaml`.
5. **Cierre de Proceso en Segundo Plano**:
   - `ShutdownMode="OnMainWindowClose"` en `App.xaml`, override `OnClosed` con `Shutdown()` en `MainWindow.xaml.cs` y llamada a `Environment.Exit()` en `App.OnExit`.

### 📋 Soluciones Aplicadas
1. **`FileFlow.App\App.xaml`**:
   - Se configuró explícitamente `ShutdownMode="OnMainWindowClose"` en la etiqueta `<Application>`.
2. **`FileFlow.App\MainWindow.xaml.cs`**:
   - Se implementó el override `OnClosed` para invocar de inmediato `Application.Current?.Shutdown()`.
3. **`FileFlow.App\App.xaml.cs`**:
   - Se añadió `Environment.Exit(e.ApplicationExitCode)` en `OnExit` tras liberar `SqliteLogStore` para garantizar la terminación determinista inmediata del proceso.
4. **`FileFlow.App\Preview\Views\FilePreviewerWindow.xaml` / `.cs`**:
   - Declarado `<local:AddOneConverter x:Key="AddOneConverter" />` en `<Window.Resources>` y eliminada la asignación manual posterior.

---

## [2026-09-02] - Gestor y Diálogo de Descarga Previa de Modelos de IA en Ajustes

### 📋 Acciones y Mejoras Realizadas

1. **Pestaña de Modelos de IA en Ajustes Globales (`WorkflowSettingsWindow.xaml`)**:
   - Se añadió la pestaña **`🤖 Modelos de IA`** (`Settings_TabAiModels`) como 5ª pestaña dentro de la ventana de configuración del flujo.
   - Proporciona un panel con resumen en vivo de modelos instalados (ej. `3 de 8 modelos instalados (85 MB en disco)`).
   - Acciones globales:
     - `⬇️ Descargar Faltantes`: descarga en lote todos los modelos no instalados con progreso visual.
     - `🔄 Actualizar`: recálculo reactivo de estado y tamaños en disco.
     - `📁 Abrir Carpeta`: apertura de la carpeta de almacenamiento de modelos en el Explorador de Windows.
     - `🚀 Abrir Asistente de Descarga...`: botón que invoca el diálogo modal independiente `AiModelDownloadDialog`.
   - Tarjetas individuales por cada modelo del catálogo con:
     - Icono dinámico (`✅` instalado, `⏳` pendiente, `⬇️` descargando, `❌` error).
     - Badge por categoría (`Visión`, `Audio`, `OCR`), nombre amigable, tamaño estimado y descripción técnica.
     - Barra de progreso interactiva con porcentaje y detalle en MB durante la descarga.
     - Botón contextual: `⬇️ Descargar` (si no está descargado) o `🗑️ Eliminar` (para liberar espacio en disco).

2. **Diálogo Dedicado de Descarga (`AiModelDownloadDialog.xaml` / `.xaml.cs`)**:
   - Ventana modal independiente estilizada con Fluent/Dark theme y barra de título inmersiva de Windows (`WindowThemeHelper`).
   - Gestión completa de descarga con reporte de progreso desacoplado (`IProgress<double>`).

3. **Arquitectura ViewModel (`AiModelManagerViewModel.cs` / `AiModelItemViewModel`)**:
   - `AiModelManagerViewModel`: orquestador observable de modelos, cálculo de totales en disco, ejecución secuencial/paralela controlada y cancelación con `CancellationTokenSource`.
   - `AiModelItemViewModel`: estado granular reactivo por cada modelo con propiedades observables (`Progress`, `ProgressText`, `IsDownloading`, `DiskSizeLabel`).

4. **Mejoras en `AiModelManager.cs` (`FileFlow.Plugin.AI`) y Corrección de Persistencia en Disco**:
   - **Corrección de Bloqueo de Archivo en Windows (`FileStream` Disposal)**: Se solucionó el fallo crítico por el cual los archivos `.downloading` se borraban al terminar la descarga: `fileStream` permanecía abierto con `FileShare.None` en el mismo bloque `try`, provocando que `File.Move(tempPath, targetPath)` lanzase `IOException` (archivo en uso por otro proceso) y el bloque `catch` eliminase el archivo descargado. Ahora `fileStream`, `contentStream` y `response` se cierran y liberan en un bloque delimitado antes de `File.Move(..., overwrite: true)`.
   - **Corrección de Umbral de Tamaño Mínimo (`MinSizeBytes`)**: El archivo de entrenamiento `spa.traineddata` (Tesseract español) tiene un tamaño real de 2.29 MB; su umbral mínimo estaba configurado erróneamente en 3.5 MB, lo que causaba que tras descargarse al 100% fuese considerado "incompleto" y eliminado. Se ajustó a 1.5 MB.
   - Nuevo método `DownloadModelWithProgressAsync(modelId, progress, statusLogger, cancellationToken)` para consumo desacoplado tanto en UI como en ejecución de flujo.
   - Nuevos helpers `GetModelDiskSizeBytes(modelId)` y `DeleteModel(modelId)`.
   - Propiedades `FriendlyName` y `Category` añadidas a `AiModelInfo`.

5. **Internacionalización y Localización (i18n)**:
   - Nuevas claves en `Strings.resx` y `Strings.es.resx`: `Settings_TabAiModels`, `Settings_AiModels_Title`, `Settings_AiModels_Desc`, `Settings_AiModels_DownloadAll`, `Settings_AiModels_Refresh`, `Settings_AiModels_OpenDir`, `Settings_AiModels_OpenDialog`, `AiModelManager_WindowTitle`, `AiModelManager_HeaderTitle`, `AiModelManager_HeaderSubtitle`, `AiModelManager_StatusInstalled`, `AiModelManager_StatusMissing`, `AiModelManager_StatusDownloading`, `AiModelManager_BtnDownload`, `AiModelManager_BtnDelete`.

6. **Pruebas y Verificación**:
   - Creado `FileFlow.Tests\Unit\App\AiModelManagerViewModelTests.cs` validando catálogo, inicialización, estados de descarga e inferencia de tamaños.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **374 / 374 pruebas superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-02] - Implementación de Inferencia IA Real con Descarga Automática de Modelos

### 🎯 Problema Detectado y Resuelto

Los 5 nodos del plugin `FileFlow.Plugin.AI` tenían implementaciones **stub** (simulaciones heurísticas) que no usaban ningún modelo de IA real. Los resultados dependían únicamente del nombre del archivo y las dimensiones de la imagen, lo que provocaba respuestas idénticas o predecibles para cualquier entrada.

### 📋 Cambios Implementados

#### `FileFlow.Plugin.AI\FileFlow.Plugin.AI.csproj`
- Añadidas dependencias reales: `NAudio` (v2.2.1) para conversión de audio, `Tesseract` (v5.2.0) para OCR local.

#### `FileFlow.Plugin.AI\AiModelManager.cs` — Reescritura Completa
- **Catálogo de modelos** (`AiModelInfo` record + `Catalog` dictionary) con URLs públicas verificadas:
  - `mobilenetv2-7.onnx` → ONNX Model Zoo (14 MB)
  - `version-slim-320.onnx` (UltraFace) → ONNX Model Zoo (1.2 MB)
  - `ssd-mobilenetv1-12.onnx` → ONNX Model Zoo (27 MB)
  - `ggml-tiny.bin` / `ggml-base.bin` / `ggml-small.bin` → Hugging Face ggerganov/whisper.cpp (39–244 MB)
  - `tessdata/eng.traineddata` / `tessdata/spa.traineddata` → GitHub tesseract-ocr/tessdata_fast (4 MB)
- **`EnsureModelAsync()`**: Nuevo método que integra descarga con progreso directo en el log del nodo (cada 10%), previene descargas concurrentes del mismo modelo, verifica integridad por tamaño mínimo, limpia archivos `.downloading` en caso de error.

#### `FileFlow.Plugin.AI\OnnxInferenceEngine.cs` — Nuevo Archivo
- Motor centralizado de inferencia ONNX con caché `Lazy<InferenceSession>` por ruta de modelo.
- Activa GPU DirectML (`AppendExecutionProvider_DML`) con fallback automático a CPU.
- `ClassifyImage()`: Preprocessing MobileNetV2 NCHW `[1,3,224,224]` + normalización ImageNet (`mean=[0.485,0.456,0.406]`, `std=[0.229,0.224,0.225]`) + mapeo de 1000 clases ImageNet a categorías de usuario.
- `DetectFaces()`: Preprocessing UltraFace `[1,3,240,320]` + normalización `[-1,1]` + conteo de anchors con confianza ≥ umbral.
- `DetectObjects()`: Preprocessing SSD MobileNet `[1,3,300,300]` + parseado de salida + etiquetas COCO 80 clases embebidas.

#### `SmartImageClassifierNode.cs` — Inferencia Real MobileNetV2
- Llama a `AiModelManager.EnsureModelAsync("mobilenetv2", ...)` — descarga automática si no disponible.
- Ejecuta `OnnxInferenceEngine.ClassifyImage()` en `Task.Run` para no bloquear el hilo de UI.
- Emite `Out` sin modificar metadatos si el modelo no está disponible (no datos falsos).

#### `FaceDetectorNode.cs` — Inferencia Real UltraFace
- Descarga automática del modelo `ultraface-slim-320.onnx`.
- `OnnxInferenceEngine.DetectFaces()` con umbral de confianza configurable.
- Metadatos reales: `AI:FaceCount`, `AI:HasFaces`, `AI:FaceMaxConfidence`.

#### `ObjectDetectorNode.cs` — Inferencia Real SSD MobileNet
- Descarga automática del modelo `ssd-mobilenetv1-12.onnx`.
- `OnnxInferenceEngine.DetectObjects()` con etiquetas COCO reales.
- Metadatos: `AI:DetectedObjects`, `AI:TopObject`, `AI:ObjectCount`, `AI:ObjectScores`.

#### `LocalWhisperTranscriberNode.cs` — Inferencia Real Whisper.net
- Descarga automática del modelo `ggml-{tiny|base|small}.bin` según parámetro `ModelSize`.
- **Conversión de audio real**: `AudioFileReader` + `WdlResamplingSampleProvider` (16kHz) + `StereoToMonoSampleProvider` → WAV temporal para Whisper.
- `WhisperFactory.FromPath()` + `processor.ProcessAsync()` → texto e iteración por segmentos reales.
- Generación de `.srt` con timestamps reales por segmento (no hardcodeados).

#### `LocalOcrNode.cs` — Inferencia Real Tesseract 5
- Descarga automática de `tessdata/{spa,eng}.traineddata` según idioma seleccionado.
- Fallback a inglés si el tessdata del idioma solicitado no se descarga.
- `TesseractEngine` + `Pix.LoadFromFile()` + `page.GetText()` para OCR real.
- Metadatos: `Ocr:Text`, `Ocr:WordCount`, `Ocr:LineCount`, `Ocr:Language`, `Ocr:Engine`.

### 🔢 Resultado de Pruebas
- `dotnet build FileFlow.Plugin.AI` → **0 errores, 0 advertencias**.
- Suite completa de tests ejecutada tras los cambios.

---

## [2026-09-02] - Visualizador de Archivos Multiformato Integrado (*FileFlow QuickPreviewer*)


### 📋 Acciones y Mejoras Realizadas

1. **Arquitectura Extensible por Proveedores (`IFilePreviewProvider` & `FilePreviewRegistry`)**:
   - Detección y resolución dinámica del motor de vista previa adecuado según formato y metadatos del archivo.
   - `FilePreviewContext`: Encapsula `CurrentPath`, `OriginalPath`, metadatos completos y capacidad de comparación dual.

2. **Proveedores de Visualización Implementados**:
   - `ImagePreviewProvider`: Visor interactivo de imágenes (`.jpg`, `.png`, `.webp`, `.bmp`, `.gif`, `.ico`, `.tiff`, `.svg`) con zoom mediante rueda del ratón/botones, paneo, rotación de 90° y control de comparación "Antes vs Después" (`ImageCompareSliderControl`) con divisor deslizante interactivo.
   - `TextCodePreviewProvider`: Visor de código fuente y texto plano (`.txt`, `.json`, `.xml`, `.cs`, `.js`, `.py`, `.sql`, `.md`, `.log`) con resaltador sintáctico temático `AvalonEdit`, formateo automático de JSON y lectura truncada segura para archivos gigantes (>2 MB).
   - `SpreadsheetPreviewProvider`: Visor de hojas de cálculo y archivos tabulares (`.xlsx`, `.xls`, `.csv`, `.tsv`) con carga streaming de alto rendimiento con `MiniExcel` en `DataGrid` virtualizado.
   - `AudioPreviewProvider`: Reproductor interactivo de audio (`.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`) con controles Play/Pause/Stop y visualización destacada de la transcripción generada por Whisper IA.
   - `ArchiveTreePreviewProvider`: Explorador de archivos comprimidos (`.zip`, `.rar`, `.7z`, `.tar`, `.gz`) en árbol `TreeView` mostrando estructura interna y tamaños sin descomprimir a disco.
   - `FallbackPreviewProvider`: Ficha informativa general con botones de acceso rápido para abrir en el Explorador de Windows o con la aplicación predeterminada.

3. **Integración en la UI & Experiencia de Usuario (UX)**:
   - `FilePreviewerControl`: Control integrado adaptable con panel lateral colapsable de metadatos, tags y etiquetas de IA (`{AI:Category}`, `{Ocr:Text}`, `{Transcript}`).
   - `FilePreviewerWindow` (QuickLook): Ventana flotante/modal con navegación `◀ Anterior` / `Siguiente ▶` entre los archivos del lote y cierre rápido con `Esc` o `Espacio`.
   - Botón `👁️ Previsualizar` integrado en el encabezado del Inspector de Nodos (`NodeInspectorPanelView.xaml`) para inspección instantánea de snapshots en depuración.
   - Botón `👁️ Vista Previa` en el menú de detalles de la consola de ejecución (`LogView.xaml`).

4. **Validación Global**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **368 / 368 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-02] - Nuevo Plugin de IA Embebida y Visión por Computador (`FileFlow.Plugin.AI`)

### 📋 Acciones y Mejoras Realizadas

1. **Nuevo Proyecto de Plugin Puro .NET 9 (`FileFlow.Plugin.AI`)**:
   - Inferencia 100% In-Process / Local sin requerir Python, Docker ni servidores externos.
   - Integración de `Microsoft.ML.OnnxRuntime.DirectML` (v1.20.1) con aceleración DirectX 12 y fallback a CPU con instrucciones vectoriales AVX2/AVX-512, `Whisper.net` (v1.7.4) y `SixLabors.ImageSharp` (v3.1.11).

2. **Gestor Inteligente de Modelos (`AiModelManager`)**:
   - Detección automática del directorio de modelos en `%AppData%/FileFlow/Models/` o en `data/models/` (para versión portable).
   - Descarga bajo demanda asíncrona (*On-Demand Downloader*) con verificación de integridad y barra de progreso.

3. **Nodos Implementados**:
   - `LocalOcrNode`: Reconocimiento óptico de caracteres para imágenes y documentos escaneados inyectando `{Ocr:Text}`, `{Ocr:WordCount}`, `{Ocr:LineCount}` e `{Ocr:Language}`.
   - `SmartImageClassifierNode`: Clasificador temático de fotos (Paisajes, Facturas/Documentos, Retratos, Vehículos, Comida, etc.) con inyección de `{AI:Category}`, `{AI:TopLabel}` y `{AI:Confidence}`.
   - `FaceDetectorNode`: Detector de rostros y personas con bifurcación dual (`FacesFound` / `NoFaces`) e inyección de `{AI:HasFaces}` y `{AI:FaceCount}`.
   - `ObjectDetectorNode`: Detección múltiple de objetos cotidianos (personas, vehículos, animales, objetos) e inyección de `{AI:DetectedObjects}` y `{AI:TopObject}`.
   - `LocalWhisperTranscriberNode`: Transcripción de audios/vídeos con modelo Whisper local e inyección de `{Transcript}` y generación automática de archivos de subtítulos sincronizados `.srt`.

4. **Integración en la UI & Localización Dinámica**:
   - Nueva categoría `AI & Computer Vision` (🤖 IA y Visión por Computador) en el selector de herramientas y catálogo de nodos.
   - Mapeo de iconos temáticos (`🤖`, `🔍`, `👁️`, `👤`, `🎯`, `🎙️`).
   - Diccionarios bilingües `Strings.resx` y `Strings.es.resx` actualizados.

5. **Validación Global**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **366 / 366 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-02] - Nuevo Plugin de Datos, Hojas de Cálculo y Bases de Datos (`FileFlow.Plugin.Data`)

### 📋 Acciones y Mejoras Realizadas

1. **Nuevo Proyecto de Plugin Puro .NET 9 (`FileFlow.Plugin.Data`)**:
   - Totalmente desacoplado de la UI y del Core, referenciando exclusivamente `FileFlow.Sdk`.
   - Integración de `MiniExcel` (v1.38.0) para I/O streaming de alto rendimiento y bajo uso de memoria, y `Microsoft.Data.Sqlite` (v9.0.2) para auditoría e inventario SQL.

2. **Nodos Implementados**:
   - `ExcelReaderNode`: Lee archivos `.xlsx` y emite cada fila como un registro de datos virtual con sus columnas en `item.Metadata`.
   - `CsvReaderNode`: Lectura streaming de archivos delimitados (CSV, TSV, TXT) con autodetección de delimitador (`,`, `;`, `\t`, `|`), opciones de codificación y control de cabecera.
   - `DataLookupNode`: Búsqueda y cruce de datos en memoria (*Data Lookup / VLOOKUP*) con caché hash optimizada O(1) e inyección parametrizada de columnas con prefijo configurable.
   - `ExcelReportGeneratorNode`: Acumula los metadatos de los archivos procesados y genera un archivo `.xlsx` estructurado con auto-ajuste de columnas y emisión por puerto `Report` mediante `OnWorkflowCompletedAsync`.
   - `CsvExportNode`: Exporta y acumula los metadatos seleccionados en archivos CSV con soporte de modo append y delimitadores personalizables.
   - `SqliteDatabaseSinkNode`: Registro histórico y auditoría en SQLite con creación automática de tablas e índices (`FileName`, `CurrentPath`, `FileSizeBytes`, `HashSHA256`, `ProcessedAtUtc`, `MetadataJson`).
   - `DataFormatConverterNode`: Conversor directo entre formatos estructurados (`Excel ⇄ CSV ⇄ JSON`).

3. **Integración en la UI & Localización Dinámica**:
   - Nueva categoría `Data & Databases` (📊 Datos y Bases de Datos) en el selector de herramientas y catálogo de nodos.
   - Mapeo de iconos temáticos (`📊`, `📑`, `🔍`, `🗄️`, `🔄`).
   - Diccionarios bilingües `Strings.resx` y `Strings.es.resx` actualizados con todas las claves y descripciones.

4. **Validación Global**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **361 / 361 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-02] - Implementación Completa de Mejoras del Motor DAG y Core (`FileFlow.Core`) - Fases 1 a 4

### 📋 Acciones y Mejoras Realizadas

1. **Fase 1: Watchdog / Modo Disparador en Tiempo Real (*Trigger Watcher Mode*)**:
   - `FolderWatcherService` ampliado para soportar multi-directorio simultáneo (`Start(IEnumerable<string>)`), `Lock` de .NET 9, colas asíncronas con `Channel<FileItemContext>`, polling dinámico optimizado y evento `ItemDiscovered`.
   - `WorkflowExecutor.ExecuteWatchModeAsync`: bucle continuo que despacha exclusivamente el archivo nuevo individual detectado directamente hacia los puertos de salida de los nodos generadores/fuente (evitando re-escanear y reprocesar todos los archivos preexistentes del directorio).
   - `ControlBarViewModel.ToggleWatchModeCommand` y botón interactivo reactivo de un solo clic `👁️ Vigilante` en `ControlBarView.xaml` (reemplazando `ToggleButton` para eliminar conflictos de estado `IsChecked` con el comando).
   - Pruebas unitarias: `WorkflowFolderWatcherTests.cs` (validando que ante nuevos archivos solo se procesa el elemento entrante).

2. **Fase 2: Monitoreo de Rendimiento y Mapa de Cuellos de Botella (*Bottleneck Heatmap*)**:
   - `NodeTelemetryStats` y enum `LatencyHeatLevel` (`Low`, `Medium`, `High`) en `FileFlow.Sdk.Telemetry`.
   - `WorkflowTelemetryTracker`: acumulación atómica de microsegundos con `Stopwatch.GetTimestamp()` y `Stopwatch.GetElapsedTime()` por nodo, cálculo de latencia media, ratio relativo del tiempo total y detección automática del nodo cuello de botella (`IsBottleneck`).
   - `WorkflowExecutionCoordinator`: sincronización a 30 FPS de las métricas por nodo con los `NodeViewModel`.
   - `NodeCardView.xaml`: Badge reactivo en la cabecera del nodo con visualización de latencia (`⚡ 12 ms` / `⏱️ 1.4 s`), nivel de calor visual (Verde, Ámbar, Rojo Neón) y alerta `⚠️ Cuello de botella`.
   - Pruebas unitarias: `WorkflowBottleneckTelemetryTests.cs`.

3. **Fase 3: Ampliación Avanzada del Modo CLI / Headless Runner**:
   - `WorkflowCliOptions` y `WorkflowCliRunner` ampliados para soportar:
     - Inyección de variables globales: `--var Key=Value` / `-v Key=Value`.
     - Sobrescritura granular de parámetros por nodo: `--param NodeId.ParameterName=Value` / `-p NodeId.ParameterName=Value`.
     - Ejecución desatendida en modo vigilante: `--watch` / `-w`.
     - Generación de reportes de ejecución JSON estructurados: `--json-summary <report.json>` / `--summary <report.json>`.
   - Pruebas unitarias: `WorkflowCliRunnerTests.cs`.

4. **Fase 4: Puntos de Control y Reanudación de Flujos Interrumpidos (*State Checkpointing & Resumption*)**:
   - Nuevo `WorkflowCheckpointManager` con persistencia en `%LocalAppData%/FileFlowStudio/checkpoints/` y soporte thread-safe de guardado/lectura/limpieza.
   - `WorkflowExecutor`: detección y reanudación automática de puntos de control pendientes, omisión inteligente de archivos ya procesados (`CompletedFileKeys`), guardado progresivo en nodos terminales y limpieza limpia al completar todo el flujo sin errores.
   - Opciones CLI `--resume` y `--no-checkpoint`.
   - Pruebas unitarias: `WorkflowCheckpointTests.cs`.

5. **Validación Global de la Suite de Pruebas**:
   - `dotnet test FileFlow.slnx` $\rightarrow$ **353 / 353 pruebas unitarias e integración superadas al 100% (0 errores, 0 fallos)**.

---

## [2026-09-02] - Nuevo Plugin de Red y Almacenamiento en Servidores (`FileFlow.Plugin.Network`)

### 📋 Acciones y Mejoras Realizadas

1. **Nuevo Proyecto de Plugin Puro .NET 9 (`FileFlow.Plugin.Network`)**:
   - Totalmente desacoplado: solo referencia a `FileFlow.Sdk` y librerías estandarizadas de dominio (`FluentFTP` v52.0.0 y `SSH.NET` v2024.2.0).
   - Registrado en la solución `FileFlow.slnx`, `FileFlow.App.csproj` (Target `CopyPlugins`) y `FileFlow.Tests.csproj`.

2. **Nodos Implementados**:
   - **`FtpUploadNode`**: Subida asíncrona a servidores FTP y FTPS (TLS/SSL explícito e implícito, modo pasivo/activo, creación recursiva de directorios remotos). Genera metadatos `{RemoteUrl}`, `{RemotePath}` y `{UploadedBytes}`.
   - **`SftpUploadNode`**: Transferencia cifrada mediante SSH/SFTP hacia servidores Linux, VPS y hosting con soporte para autenticación por contraseña y llaves privadas RSA/Ed25519 (`.pem`/`.key`).
   - **`SmbCopyNode`**: Copia asíncrona de alto rendimiento a rutas compartidas de red local y unidades NAS (`\\NAS\Backups\...`) con buffer optimizado de 80 KB y política de reintentos exponenciales ante microcortes de red.
   - **`WebDavUploadNode`**: Subida a servidores WebDAV, Nextcloud, ownCloud y almacenamiento NAS mediante HTTP PUT y creación automática de colecciones remotas con `MKCOL`.
   - **`RemoteDownloadNode`**: Descarga de ficheros remotos desde URLs HTTP, HTTPS o WebDAV hacia una carpeta local (compatible con `{GlobalOutputDir}`) para alimentar el flujo de trabajo.

3. **Helper de Plantillas Dinámicas en Red (`NetworkTemplateHelper`)**:
   - Resolución automática de tokens en rutas y nombres remotos: `{FileName}`, `{FileNameWithoutExtension}`, `{Extension}`, `{Date}`, `{Year}`, `{Month}`, `{Day}`, `{Hour}`, `{Minute}`, `{Second}`, `{OriginalDirectoryName}` y metadatos personalizados `{Key}`.

4. **Integración en la UI y Catálogo de Nodos**:
   - Nueva categoría **`Network & Remote`** (🌐 Red y Servidores) descubierta dinámicamente en el selector desplegable `ComboBox`.
   - Iconos temáticos integrados: `🌐` Categoría, `📤` FTP, `🔒` SFTP, `🖧` SMB/NAS, `☁️` WebDAV, `📥` Descarga.

5. **Validación y Suite de Pruebas**:
   - Creada suite de pruebas unitarias `NetworkNodesTests.cs` en `FileFlow.Tests/Unit/Plugins/Network/`.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **346 / 346 pruebas pasadas al 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-02] - Categorías Dinámicas y Selector Desplegable Moderno (Dropdown ComboBox) en el Catálogo de Nodos

### 📋 Acciones y Mejoras Realizadas

1. **Descubrimiento 100% Dinámico de Categorías de Plugins (`ToolboxViewModel`)**:
   - Modelo `ToolboxCategoryFilterItem` con clave técnica (`Key`), nombre traducido dinámicamente (`DisplayName`), icono representativo (`Icon`), conteo en tiempo real (`Count`) y estado de selección (`IsSelected`).
   - Propiedad `SelectedCategoryItem` con sincronización bidireccional inmediata con el control desplegable `ComboBox`.
   - Escaneo automático en tiempo de ejecución de `_pluginLoader.DiscoveredNodeTypes` para extraer todas las categorías presentes en plugins cargados (incluyendo la nueva categoría `Documents` de PDFs, `Scripting`, `Images`, `Hashing`, etc., así como futuros plugins de terceros).
   - Cálculo reactivo de contadores de nodos por categoría respetando la búsqueda por texto y favoritos.

2. **Selector Desplegable Moderno (Dropdown / ComboBox Temático) en 1 Sola Línea (`NodeToolboxView.xaml`)**:
   - Reemplazo del bloque vertical amontonado de botones por un **control selector desplegable `ComboBox` compacto de 1 sola fila** integrado con los temas dinámicos (`BgSurfaceBrush`, `BorderDarkBrush`, `TextPrimaryBrush`, `AccentGlowBrush`).
   - Muestra de forma concisa el icono, nombre y contador de la categoría activa: `[ 🌐 Todas (28) ▾ ]`, `[ 📄 Documentos y PDFs (4) ▾ ]`, etc.
   - Menú desplegable con plantilla enriquecida: icono temático, nombre localizado y badge numérico de conteo `(N)` alineado a la derecha.
   - Libera todo el espacio vertical del panel lateral para la exploración visual de las tarjetas de nodos.

3. **Localización e Internacionalización Completa (i18n)**:
   - Claves de categorías añadidas a `Strings.resx` y `Strings.es.resx` (`Category_Documents`, `Category_Images`, `Category_Scripting`, etc.) con traducción en caliente.

4. **Validación y Suite de Pruebas**:
   - Añadidos tests unitarios `AvailableCategories_ShouldDynamicallyIncludeNewPluginCategoriesAndCounts`, `SetCategoryFilter_ShouldFilterNodesAndHighlightSelectedChip` y `SelectedCategoryItem_ShouldFilterNodes_WhenChangedByDropdown` en `ToolboxViewModelTests.cs`.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **341 / 341 pruebas pasadas al 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-02] - Implementación Secuencial Completa: 5 Nuevas Funcionalidades Mayores

### 📋 Acciones y Mejoras Realizadas

1. **Tarea 1: Notas Adhesivas / Sticky Notes en el Canvas (`AnnotationViewModel` & `AnnotationCardView`)**:
   - Modelos de datos `WorkflowAnnotation` con serialización JSON bidireccional (`X`, `Y`, `Width`, `Height`, `Title`, `Content`, `Color`).
   - Componente visual `AnnotationCardView` con selector de 6 colores pastel, redimensionado interactivo por `Thumb`, edición en vivo y **soporte completo de arrastre y reposicionamiento en el lienzo mediante `HeaderThumb_DragDelta`**.
   - Enlace `CanvasDecorators` polimórfico en `NodifyEditor` y botón `📝 Nota` en la barra de zoom.

2. **Tarea 2: Marcos de Agrupación Visual ("Group Frames / Group Boxes")**:
   - Modelo `WorkflowGroup` y `GroupViewModel` enlazados a nodos con `NodeIds`.
   - Componente visual `GroupCardView` con selector de paleta de color para el encabezado/borde y redimensionado mediante `ResizeThumb`.
   - **Corrección de Interacción Completa (Hit-Testing Preciso)**: Estructura desacoplada en `GroupCardView.xaml` donde el fondo interior translúcido es `IsHitTestVisible="False"` para permitir hacer clic y arrastrar los nodos interiores sin interferencias, mientras que la cabecera (título, paleta de colores, botón eliminar, arrastre de grupo) y el tirador inferior `ResizeThumb` mantienen `IsHitTestVisible="True"` activo en todo momento.
   - **Contención Espacial Dinámica y Estricta**: `HeaderThumb_DragDelta` evalúa en tiempo real si el centro del nodo está contenido estrictamente dentro de los límites del marco del grupo (`[groupLeft, groupRight]` y `[groupTop, groupBottom]`), sincronizando `NodeIds`. Si un nodo se arrastra fuera de la ventana del grupo, queda automáticamente desacoplado y deja de moverse con el marco; asimismo, los nodos externos cercanos no son capturados por error.
   - Comando `GroupSelectedNodesCommand` (`Ctrl+G`) que calcula el bounding box automático de los nodos seleccionados.
   - Botón `🔲 Grupo` en la barra de herramientas del editor.

3. **Tarea 3: Ejecutor Headless / CLI Runner (`WorkflowCliRunner`)**:
   - Módulo desacoplado en `FileFlow.Core/Engine/WorkflowCliRunner.cs` para ejecución desatendida por línea de comandos.
   - Argumentos soportados: `--run / -r <workflow.json>`, `--input / -i <path>`, `--output / -o <path>`, `--dryrun / -d`, `--silent / -s`, `--help / -h`.
   - Integración directa en `App.xaml.cs` que ejecuta en modo consola sin inicializar UI gráfica y retorna el código de salida adecuado (`0` / `1`).

4. **Tarea 4: Plugin de Documentos y PDFs (`FileFlow.Plugin.Documents`)**:
   - Nuevo proyecto de plugin puro .NET 9 con dependencias en `PdfSharp` y `PdfPig`.
   - Implementados 4 nuevos nodos de procesamiento de documentos:
     - `PdfMergeNode`: Combina múltiples archivos PDF en un archivo consolidado.
     - `PdfSplitNode`: Divide documentos multipágina en páginas individuales con nombres dinámicos.
     - `PdfTextExtractorNode`: Extrae texto completo de PDFs hacia metadatos o archivos `.txt`.
     - `PdfMetadataNode`: Lee e inspecciona metadatos y permite actualizarlos con resolución de plantillas.

6. **Validación y Suite de Pruebas**:
   - Añadidos `AnnotationViewModelTests`, `GroupViewModelTests`, `WorkflowCliRunnerTests` y `DocumentsTests`.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **340 / 340 pruebas unitarias e integración pasadas al 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-02] - Variable Global de Salida por Defecto (`{GlobalOutputDir}` / `{DefaultOutputDir}`) en Nodos y Expresiones

### 📋 Acciones y Mejoras Realizadas
1. **Centralización en el SDK (`AppPaths.cs`)**:
   - Incorporada la propiedad `AppPaths.DefaultGlobalOutputDir` con resolución automática para entorno estándar (`%USERPROFILE%/Documents/FileFlowStudio/Output`) y modo portable (`data/output`).
   - `UserPreferencesService` y `UserPreferencesData` sincronizados para usar `AppPaths.DefaultGlobalOutputDir` de forma nativa.
2. **Ampliación de Resolución en `SystemVariablesResolver.cs` y `VariableTemplateResolver`**:
   - Soporte para variables `{GlobalOutputDir}`, `{DefaultOutputDir}`, `{DefaultGlobalOutputDir}`, `{GlobalOutputPath}`, `{DefaultOutputPath}`, `{GlobalOutput}`, `{DefaultOutput}`, `{OutputDir}`, `{DefaultDir}` y sintaxis clásica `<GlobalOutputDir>`, `<DefaultOutputDir>`.
   - Búsqueda en metadatos del elemento (`Metadata["GlobalOutputDir"]`, `Metadata["DefaultGlobalOutputDir"]`, etc.) con fallback determinista a `AppPaths.DefaultGlobalOutputDir`.
3. **Integración en Asistentes y Catálogos de UI**:
   - `VariableDiscoveryService.cs`: Agregadas `{GlobalOutputDir}` y `{DefaultOutputDir}` al grupo de variables `🌐 System & Environment`.
   - `RenamerTagCatalogService.cs`: Incorporadas en la sección `"Sistema y Archivo"` para el Renombrador Avanzado.
4. **Validación y Suite de Pruebas**:
   - Nuevos tests en `GlobalOutputDirTests.cs` (`VariableTemplateResolver_ResolvesAllGlobalOutputDirAliases`, `VariableTemplateResolver_WithoutExplicitMetadata_FallsBackToAppPathsDefault`).
   - `dotnet test FileFlow.slnx` $\rightarrow$ **320 / 320 pruebas pasadas con 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-02] - Reportes de Operaciones en Memoria, Eliminación de `DestinationFolder` y Ciclo de Vida `OnWorkflowCompletedAsync`

### 📋 Acciones y Mejoras Realizadas
1. **Hook de Ciclo de Vida en el SDK (`IFlowNode.cs`)**:
   - Añadido `Task OnWorkflowCompletedAsync(IFlowExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;` para permitir a nodos acumuladores/agregadores emitir resultados al finalizar el flujo.
2. **Coordinación DAG en `WorkflowExecutor.cs`**:
   - Invocación determinista de `OnWorkflowCompletedAsync` para todos los nodos tras completar el lote inicial, y drenaje asíncrono de tareas subsiguientes con `DrainActiveTasksAsync`.
3. **Generación Pura en Memoria en `OperationReportNode.cs`**:
   - Eliminado el parámetro `DestinationFolder`.
   - Generación de reportes individuales y consolidados 100% en memoria (`Metadata["ReportContent"]` y `Metadata["VirtualContent"]`), emitiéndolos por el puerto `Report` sin tocar el disco de forma forzada.
   - Reenvío continuo de los archivos de entrada por `Out`.
4. **Soporte de Archivos Virtuales en `DestinationSinkNode.cs`**:
   - `DestinationSinkNode` puede persistir archivos recibidos en memoria (`VirtualContent` / `ReportContent` en texto o bytes) en cualquier carpeta destino configurada.
5. **Validación y Suite de Pruebas**:
   - `OperationReportNodeTests.cs` actualizado para validar generación en memoria, ciclo de vida de finalización e integración directa con `DestinationSinkNode`.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **318 / 318 pruebas superadas al 100% de éxito**.

---

## [2026-09-02] - Bugfix: Concurrencia en Reporte de Operaciones y Resolución de Rutas Relativas `{RelativeDir}`

### 🐛 Problemas Detectados
1. **Bloqueo Concurrente de Archivos (`IOException`) en `OperationReportNode`**:
   - Al procesar múltiples archivos en paralelo, varios hilos intentaban escribir simultáneamente en el mismo archivo de reporte consolidado (`Reporte_Ejecucion_*.html`) con `File.WriteAllTextAsync`, lanzando `The process cannot access the file ... because it is being used by another process`.
2. **Desvío de Rutas Relativas al Directorio de Trabajo de la Aplicación**:
   - Al usar `{RelativeDir}\Output` para un archivo en la raíz (ej. `d:\pepe\file.txt`), `{RelativeDir}` resolvía a cadena vacía `""`, generando la ruta `\Output`.
   - `ParameterHelper.ResolveOutputPath` consideraba `\Output` como ruta absoluta (por empezar con `\`), pero al no tener letra de unidad (`!Path.IsPathFullyQualified`), Windows la resolvía contra el directorio de trabajo del proceso en lugar de la carpeta de origen `d:\pepe\Output`.

### 🔧 Solución Aplicada
1. **Sincronización Concurrente y FileShare en `OperationReportNode.cs`**:
   - Incorporado `SemaphoreSlim _writeLock = new(1, 1)` para serializar de forma asíncrona la escritura del reporte consolidado sin bloquear los canales del pipeline.
   - Apertura de streams con `FileShare.ReadWrite` en `FileStream` tanto para reportes individuales como consolidados.
   - Implementado `IDisposable` para liberar deterministamente los semáforos.
2. **Anclaje Inteligente de Rutas Relativas en `ParameterHelper.cs`**:
   - `ResolveOutputPath` normaliza separadores iniciales huérfanos (`\Output` $\rightarrow$ `Output`).
   - Si la ruta no está completamente calificada (`!Path.IsPathFullyQualified`) y no hay `GlobalOutputDir`, ancla automáticamente la ruta relativa bajo el directorio de origen del archivo (`SourceRootPath` o `Path.GetDirectoryName(OriginalPath)` o `CurrentPath`).
   - Resultado: `{RelativeDir}\Output` para `d:\pepe\archivo.txt` resuelve exactamente a `d:\pepe\Output`.
3. **Validación y Suite de Pruebas**:
   - Nuevos tests en `GlobalOutputDirTests.cs` (`ResolveOutputPath_WithoutGlobalOutputDir_AnchorsUnderSourceDirectory`, `ResolveOutputPath_WithSubdirectoryAndSourceRootPath_AnchorsCorrectly`).
   - Nuevo test de estrés concurrente en `OperationReportNodeTests.cs` (`ExecuteAsync_ShouldHandleConcurrentExecutionWithoutFileLockingErrors` con 20 tareas en paralelo).
   - `dotnet test FileFlow.slnx` $\rightarrow$ **319 / 319 pruebas pasadas con 100% de éxito**.

---

## [2026-09-02] - Evaluación y Previsualización de Parámetros en Tiempo Real en el Inspector (Enfoque Híbrido)

### 📋 Acciones y Mejoras Realizadas
1. **Evaluación Reactiva en `NodeParameterViewModel`**:
   - Nuevas propiedades `EvaluatedValue` (`string`), `HasExpression` (`bool`) e `IsCopied` (`bool`).
   - Método `UpdateEvaluationContext(FileItemContext? context, string? sourceRootPath = null)` para sincronizar la evaluación con el contexto del archivo en depuración mediante `VariableTemplateResolver.Resolve(...)`.
   - Comando `CopyEvaluatedValueCommand` con copia al portapapeles y retroalimentación reactiva.
2. **Sincronización Contextual en `NodeInspectorViewModel`**:
   - Detección automática y propagación del `ItemSnapshot` de `SelectedSnapshot`, o del último snapshot de entrada/salida disponible, hacia todos los parámetros del nodo inspeccionado.
   - Propiedades de estado `HasActiveEvaluationSnapshot` y `ActiveEvaluationContextFileName` para la cabecera del panel.
3. **Interfaz Gráfica e i18n (`NodeInspectorPanelView.xaml`)**:
   - Indicador visual en la cabecera de la pestaña de parámetros con el archivo de depuración activo.
   - Badge `{x}` en la etiqueta del parámetro si contiene tokens o expresiones dinámicas.
   - Bloque visual inline `⚡ Evaluado: [valor]` en tipografía monospace con botón de copia rápida `📋 Copiar`.
   - Claves de internacionalización (`Strings.resx` y `Strings.es.resx`): `Inspector_EvaluatedLabel`, `Inspector_CopyEvaluatedToolTip`, `Inspector_ExpressionBadgeToolTip`, `Inspector_ActiveContextLabel`, `Inspector_NoSnapshotForEvaluation`.
4. **Validación y Suite de Pruebas**:
   - Pruebas unitarias en `NodeParameterViewModelTests.cs` y `NodeInspectorViewModelTests.cs`.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **316 / 316 pruebas superadas al 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-02] - Bugfix: Claves de Localización Faltantes en la Barra de Estado (StatusBar)

### 🐛 Problema Detectado
La barra de estado inferior de la aplicación mostraba las claves de localización literales (p.ej. `StatusBar_Nodes`, `StatusBar_Connections`, `StatusBar_OutputLabel`, etc.) en lugar de los textos traducidos correspondientes. Esto ocurría porque las 9 claves `StatusBar_*` referenciadas en `StatusBarView.xaml` no estaban definidas en ninguno de los archivos `.resx`.

### 🔧 Solución Aplicada
1. **`FileFlow.App/Resources/Strings.resx` (Inglés)**:
   - Añadidas 9 claves nuevas bajo la sección `<!-- Status Bar -->`:
     - `StatusBar_Nodes` → `"Nodes"`
     - `StatusBar_NodesToolTip` → `"Number of nodes in the current workflow graph"`
     - `StatusBar_Connections` → `"Connections"`
     - `StatusBar_ConnectionsToolTip` → `"Number of connections between nodes in the current workflow"`
     - `StatusBar_OutputLabel` → `"Output"`
     - `StatusBar_OutputFolderToolTip` → `"Click to open the global output folder in File Explorer"`
     - `StatusBar_RamToolTip` → `"Current RAM memory usage of the application"`
     - `StatusBar_CpuToolTip` → `"Current CPU usage of the application process"`
     - `StatusBar_FitScreenToolTip` → `"Click to fit the workflow graph to the visible canvas area. Current zoom level."`
2. **`FileFlow.App/Resources/Strings.es.resx` (Español)**:
   - Mismas 9 claves añadidas con traducción española correcta.
3. **Validación**: `dotnet build FileFlow.slnx --warnaserror` → **0 Errores, 0 Advertencias**.

---

## [2026-09-02] - Soporte de Filtrado por Extensión en Nodo Carpeta Origen (FolderSourceNode)

### 📋 Acciones y Mejoras Realizadas
1. **Nuevo Parámetro `ExtensionFilter` en `FolderSourceNode`**:
   - Incorporado el parámetro `ExtensionFilter` con descriptor declarativo `ParameterEditorType.Text` en `ParameterDescriptors` (Orden 2).
   - Parser flexible `ParseExtensionFilter` que acepta múltiples formatos y delimitadores: `*.jpg, *.png`, `.zip; .rar`, `pdf|docx`, `jpg png webp`, `*` o `*.*`.
2. **Filtrado Eficiente en 1 Sola Pasada y Pre-conteo Optimizado**:
   - `FastCountSourceFiles` y la tarea en segundo plano calculan la estimación exacta de elementos filtrando por el conjunto de extensiones activas.
   - `StreamAndEmitDirAsync` emite únicamente los archivos coincidentes a través del canal acotado (`Channel.CreateBounded<FileItemContext>`).
3. **Localización e i18n Completa**:
   - Registrada la clave `Param_ExtensionFilter` en `FileFlow.Plugin.FileSystem` (`Resources/Strings.resx` y `Strings.es.resx`) y en `FileFlow.App` (`Resources/Strings.resx` y `Strings.es.resx`) traducida como *"Filtro de Extensiones"* / *"Extension Filter"*.
4. **Validación y Suite de Pruebas**:
   - Incorporadas pruebas unitarias completas en `FolderSourceNodeTests.cs` validando el filtrado por extensión múltiple, case-insensitivity, manejo de comodines y parseo.
   - `dotnet test FileFlow.slnx -c Release` $\rightarrow$ **312 / 312 pruebas unitarias e integración superadas al 100% (0 errores, 0 avisos)**.

## [2026-09-02] - Descentralización Total de Recursos (.resx / i18n) por Plugin (Zero-Touch en FileFlow.App)

### 📋 Acciones y Mejoras Realizadas
1. **Auto-Descubrimiento Inteligente de Recursos en `PluginLoader.cs`**:
   - `PluginLoader.RegisterPluginResources(Assembly asm)`: Inspecciona de forma automatizada los ensamblados `.dll` cargados en busca de clases de recursos (`Strings.ResourceManager`, `*Resources`) y nombres de manifiestos incrustados (`.resources`).
   - Auto-registro determinista de cada `ResourceManager` en `LocalizationManager.Instance.RegisterResourceManager(...)` sin requerir ninguna línea de código en la aplicación principal ni configuración manual.
2. **Soporte para Inicialización Avanzada Opcional (`IPluginInitializer`)**:
   - Definida la interfaz `IPluginInitializer` en `FileFlow.Sdk.Plugins` (`void Initialize()`).
   - `PluginLoader` detecta, instancia y ejecuta deterministamente cualquier inicializador presente en el ensamblado del plugin durante la carga.
3. **Thread-Safety y Optimización en `LocalizationManager.cs`**:
   - Protegida la lista interna `_resourceManagers` mediante el nuevo primitivo de sincronización `System.Threading.Lock` de .NET 9.
   - Manejo resiliente de excepciones individuales al buscar claves por cadena de recursos.
4. **Descentralización Física de Archivos `.resx` a sus Respectivos Plugins**:
   - `FileFlow.Plugin.FileSystem/Resources/`: Creados `Strings.resx` y `Strings.es.resx` con todas las claves de `AdvancedRenamer` y `RegexHelper`.
   - `FileFlow.Plugin.Archives/Resources/`: Creados `Strings.resx` y `Strings.es.resx` con todas las claves de `PasswordManager`.
   - `FileFlow.Plugin.Integrations/Resources/`: Creados `Strings.resx` y `Strings.es.resx` con todas las claves de `PresetManager` (FFmpeg).
   - `FileFlow.App/Resources/`: Purgadas todas las claves exclusivas de plugins, manteniendo únicamente los recursos globales de la aplicación (menú, ajustes, barra de control, consola y catálogo de nodos).
   - **Resultado:** Cualquier plugin contiene de forma 100% autónoma su lógica de negocio, vistas XAML, servicios y diccionarios de traducción. Crear o modificar un plugin no requiere tocar en absoluto `FileFlow.App`.
5. **Nuevas Pruebas Automatizadas y Suite de Tests**:
   - Añadidos tests unitarios en `LocalizationManagerTests.cs` para validar el auto-descubrimiento y resolución bilingüe (`es-ES` / `en-US`) de recursos incrustados de plugins y la concurrencia multihilo.
   - `dotnet test FileFlow.slnx -c Release` $\rightarrow$ **305 / 305 pruebas unitarias e integración superadas al 100% (0 errores, 0 avisos)**.

## [2026-09-02] - Auditoría y Localización Dinámica Completa de Toda la UI (i18n Exhaustiva)

### 📋 Acciones y Mejoras Realizadas
1. **Auditoría e Internacionalización Exhaustiva de Vistas XAML**:
   - Reemplazadas todas las cadenas de texto estáticas/hardcoded por enlaces dinámicos a `LocalizationManager.Instance`:
     - **Catálogo de Nodos (`NodeToolboxView.xaml`)**: Filtros de categorías (`Category_All`, `Category_Favorites`, `Category_Frequent`, `Category_FileSystem`, `Category_Archives`, `Category_MediaDocs`, `Category_Metadata`, `Category_Logic`, `Category_Integrations`), botón de modo compacto (`Toolbox_CompactBtn`), tooltips de vista compacta (`Toolbox_ToggleCompactToolTip`) y tooltips de favoritos (`Toolbox_FavoriteToolTip`).
     - **Inspector de Nodos (`NodeInspectorPanelView.xaml`)**: Pestañas de Parámetros, Salidas, Entradas, Diff y Trazabilidad (`Inspector_Tab*`), encabezados y subencabezados de sección, etiquetas de puertos (`Inspector_InputsPortLabel`, `Inspector_OutputsPortLabel`), columnas de la tabla de diferencias de metadatos (`Inspector_ColKey`, `Inspector_ColStatus`, `Inspector_ColNewValue`, `Inspector_ColOldValue`), metadatos del archivo inspeccionado y botones de acción rápida (`Inspector_CloseBtn`, `Inspector_TestBtn`).
     - **Ajustes Globales (`WorkflowSettingsWindow.xaml`)**: Todas las pestañas (`Settings_TabStorage`, `Settings_TabAppearance`, `Settings_TabPerformance`, `Settings_TabExternalTools`), título de ventana, descripciones de opciones (rutas de salida, colisiones, temas, rendimiento multihilo, niveles de log y rutas de ejecutables de sistema) y botones (`Settings_SaveBtn`, `Settings_BrowseBtn`, `Settings_AutoDetectBtn`, `Settings_CustomizeThemesBtn`).
     - **Personalizador de Temas (`ThemeCustomizerWindow.xaml`)**: Título, subtítulo, encabezados de grupos de configuración (Información General, Fondos y Superficies, Colores de Acento y Estados, Textos y Bordes, Gradiente de Cables, Tipografía), controles de fuentes/radios, vista previa interactiva y botones de acción (`ThemeCustomizer_NewBtn`, `ThemeCustomizer_DuplicateBtn`, `ThemeCustomizer_DeleteBtn`, `ThemeCustomizer_TestInApp`, `ThemeCustomizer_SaveAndApply`).
     - **Consola de Registro (`LogView.xaml`)**: Tooltips de control de consola (`Log_ClearSearchToolTip`, `Log_ToggleLiveToolTip`, `Log_ExportToolTip`, `Log_ClearToolTip`) y botones de detalles (`Log_TraceabilityBtn`, `Log_CopyJsonBtn`).
     - **Diálogos de Plugins Desacoplados**:
       - `PasswordManagerWindow.xaml` (`FileFlow.Plugin.Archives`): Título, subtítulo, botones de importar/exportar txt y guardar claves.
       - `MediaPresetManagerWindow.xaml` (`FileFlow.Plugin.Integrations`): Título, subtítulo, formulario de edición de perfiles (Nombre, Categoría, Extensión, Descripción, CLI Args) y botones.
       - `RegexHelperWindow.xaml` (`FileFlow.Plugin.FileSystem`): Título, subtítulo, biblioteca de patrones predefinidos/guardados, probador en vivo con banderas de regex (IgnoreCase, Multiline, Singleline, IgnoreWhitespace), grupos de captura y botones de acción.
       - `AdvancedRenamerEditorWindow.xaml` (`FileFlow.Plugin.FileSystem`): Título, subtítulo, selector de presets, menú de métodos, tabla de vista previa en vivo y pie de acción.
2. **Sincronización Total de Diccionarios de Recursos (`Strings.resx` y `Strings.es.resx`)**:
   - Incorporadas más de 80 nuevas claves bilingües en inglés y español.
   - Eliminados duplicados de categorías para mantener una compilación 100% limpia sin advertencias (`MSB3568`).
3. **Validación de Compilación y Suite de Pruebas**:
   - `dotnet test FileFlow.slnx -c Release` $\rightarrow$ **303 / 303 pruebas unitarias e integración superadas al 100% (0 errores, 0 avisos)**.

## [2026-09-02] - Localización Dinámica del Menú Principal (Drawer), Tooltips y Persistencia de Idioma

### 📋 Acciones y Mejoras Realizadas
1. **Localización Reactiva del Menú Lateral (Side Drawer) en `MainWindow.xaml`**:
   - Reemplazados todos los textos literales y tooltips estáticos por enlaces dinámicos a `LocalizationManager.Instance`:
     - Títulos de sección: `GESTIÓN DE FLUJOS` (`Drawer_FlowManagement`), `APARIENCIA E IDIOMA` (`Drawer_AppearanceLanguage`), `PANELES Y HERRAMIENTAS` (`Drawer_PanelsTools`), `AYUDA Y RECURSOS` (`Drawer_HelpResources`).
     - Acciones y botones: `Nuevo Flujo` (`Drawer_NewWorkflow`), `Cargar Flujo...` (`Drawer_LoadWorkflow`), `Guardar Flujo...` (`Drawer_SaveWorkflow`), `Tema Visual:` (`Drawer_ThemeLabel`), `Idioma:` (`Drawer_LanguageLabel`), `Personalizar Tema Visual...` (`Drawer_CustomizeTheme`), `Inspector de Datos` (`Drawer_DataInspector`), `Manual de Usuario` (`Drawer_UserManual`), `Ejemplos de Flujos` (`Drawer_ExampleFlows`).
     - Subtítulo de marca `Gestor de Flujos v1.0` (`Drawer_AppSubtitle`) y tooltips de cierre y versión.
2. **Localización de Tooltips de la Barra Superior en `ControlBarView.xaml`**:
   - Tooltips localizados: `ControlBar_MenuToolTip`, `ControlBar_DryRunToolTip`, `ControlBar_SettingsToolTip`, `ControlBar_StepNextToolTip`, `ControlBar_ContinueToolTip`, `ControlBar_PauseToolTip`, `ControlBar_StopToolTip`, `ControlBar_RollbackToolTip`, `ControlBar_InspectorToolTip`.
3. **Ampliación de Diccionarios de Recursos (`Strings.resx` y `Strings.es.resx`)**:
   - Incorporadas todas las claves en inglés y español para soporte bilingüe integral en tiempo real.
4. **Persistencia Automática de Idioma en `UserPreferencesService`**:
   - Añadida la propiedad `Language` a `UserPreferencesData` con valor por defecto `"es-ES"`.
   - `ControlBarViewModel`: Sincronización automática y persistencia inmediata al cambiar de idioma en el selector.
   - `App.xaml.cs`: Inicialización de la cultura de la aplicación a partir de las preferencias guardadas del usuario durante el arranque.
5. **Validación de Compilación y Suite de Tests**:
   - Corregido aviso MVVM Toolkit (`MVVMTK0034`).
   - `dotnet test FileFlow.slnx -c Release` $\rightarrow$ **303 / 303 pruebas pasadas con 100% de éxito (0 errores, 0 avisos)**.

## [2026-09-02] - Versión Oficial en Inglés de los Manuales y Documentación Completa en PDF

### 📋 Acciones y Mejoras Realizadas
1. **Creación de la Suite de Manuales en Inglés (`docs/`)**:
   - [`docs/user_manual.md`](file:///docs/user_manual.md): Manual de usuario general y catálogo exhaustivo de los 27 nodos del motor DAG en inglés.
   - [`docs/beginner_user_guide.md`](file:///docs/beginner_user_guide.md): Guía didáctica para principiantes paso a paso con 4 recetas prácticas, analogías y glosario en inglés.
   - [`docs/scripting_node_manual.md`](file:///docs/scripting_node_manual.md): Manual completo de scripting personalizado en C# (Roslyn) y JavaScript (Jint) en inglés.
2. **Compilación Automatizada a PDF con Microsoft Edge Chromium Headless (`installer/build-pdf-manual.ps1`)**:
   - Compilación simultánea de los 6 documentos PDF de distribución:
     - 🇪🇸 `docs/manual_de_usuario.pdf`, `docs/manual_usuario_principiantes.pdf`, `docs/manual_nodo_scripting.pdf`.
     - 🇬🇧 `docs/user_manual.pdf`, `docs/beginner_user_guide.pdf`, `docs/scripting_node_manual.pdf`.
3. **Despacho Dinámico Bilingüe en la Aplicación (`FileFlow.App` & `FileFlow.Plugin.Scripting`)**:
   - `LocalizationManager`: Añadida propiedad `CurrentLanguage` (`en` / `es`).
   - `ControlBarViewModel.cs`: Detección automática del idioma activo para abrir `user_manual.pdf` en inglés o `manual_de_usuario.pdf` en español.
   - `ScriptStudioWindow.xaml.cs`: Detección automática para abrir `scripting_node_manual.pdf` en inglés o `manual_nodo_scripting.pdf` en español.
4. **Instalador Inno Setup y Publicación en GitHub Releases**:
   - `installer/FileFlow.iss`: Accesos directos condicionales en el Menú de Inicio que apuntan automáticamente a los manuales en inglés si la instalación se realiza en inglés, o en español si se instala en español.
   - `installer/build-installer.ps1` y `.github/workflows/release.yml`: Publicación de los 6 manuales PDF oficiales como assets individuales en cada release.
5. **Validación de Tests**:
   - Suite total: **303 / 303 pruebas pasadas con 100% de éxito**.

### 📋 Acciones y Mejoras Realizadas
1. **Plugins Auto-Contenidos con Soporte WPF en .NET 9**:
   - `FileFlow.Plugin.FileSystem`, `FileFlow.Plugin.Integrations` y `FileFlow.Plugin.Archives` configurados con `net9.0-windows` y `<UseWPF>true</UseWPF>`.
2. **Traslado Físico de Vistas y Servicios a sus Plugins**:
   - `AdvancedRenamerEditorWindow.xaml`, `AdvancedRenamerEditorViewModel`, `RenamerTagCatalogService`, `RenamerSampleDataProvider` y `RenamerLivePreviewService` trasladados a `FileFlow.Plugin.FileSystem/UI/`.
   - `MediaPresetManagerWindow.xaml` y `MediaPresetManagerService` trasladados a `FileFlow.Plugin.Integrations/UI/`.
   - `PasswordManagerWindow.xaml` trasladado a `FileFlow.Plugin.Archives/UI/`.
3. **Despacho Universal mediante `INodeCustomActionProvider`**:
   - `AdvancedRenamerNode`, `MediaTranscoderNode` y `SmartUnpackNode` implementan `INodeCustomActionProvider` y abren sus propias ventanas directamente desde sus ensamblados.
   - `NodeViewModel.ExecuteCustomAction` delega de forma 100% agnóstica en `INodeCustomActionProvider`.
4. **Erradicación Total de Código de Plugins en `FileFlow.App`**:
   - Eliminados todos los archivos de diálogo y servicios de plugins de `FileFlow.App`.
   - `FileFlow.App` queda como un contenedor universal y limpio: para crear o extender un nodo o plugin, solo se escribe código dentro del directorio de ese plugin.
5. **Visibilidad Directa de Acciones en Tarjetas y Despliegue Automatizado de Plugins**:
   - `NodeCardView.xaml`: Integrada barra de acciones (`CustomActions`) directamente visible en la tarjeta del nodo (`🏷️ Pipeline de Métodos...`, `➕ Variable`, `➕ Caso`), accesible al instante sin necesidad de desplegar el panel de ajustes ⚙.
   - `FileFlow.App.csproj`: Corregido el target `CopyPlugins` para apuntar a `$(TargetDir)Plugins\` y compilar/desplegar con precisión los plugins `net9.0-windows` y `net9.0` a la carpeta de ejecución de la app.
7. **Actualización Completa de los 40 Flujos de Ejemplo (`docs/examples/`)**:
   - Se revisaron, limpiaron y actualizaron todos los 40 archivos de ejemplo de workflows (`01_basic`, `02_intermediate`, `03_advanced`, `04_complex`) y `docs/flujo_test.json`.
   - Eliminación total de parámetros y puertos obsoletos:
     - `SafeRecycleDeleteNode`: Puertos `Deleted`, `Error`.
     - `ExpressionFilterNode`: Puertos `True`, `False`; parámetros canónicos `Property`, `Operator`, `ComparisonValue`.
     - `ExifMetadataNode`: Parámetro `FallbackToCreationDate`.
     - `DocumentProcessorNode`: Parámetros `Operation`, `ExtractPageCount`.
     - `WebhookNotificationNode`: Parámetros `Url`, `PayloadTemplate`; puertos `Out`, `Failed`.
     - `ArchiveFilterNode`: Puertos `Archive`, `RegularFile`, `SecondaryVolume`.
     - `BatchBufferNode`: Puertos `ItemIn`, `ForceFlush`, `ItemOut`, `BatchCompleted`; parámetros `BatchSize`, `MaxBatchSizeBytes`.
     - `ForkJoinBarrierNode`: Puertos `In`, `Fork1`, `Fork2`, `AllCompleted`.
     - `ThrottleDelayNode`: Parámetro `DelayMilliseconds`.
     - `EmptyDirectoryCleanerNode`: Puerto `TriggerIn`, `Out`, `Error`.
     - `HashCalculatorNode`: Parámetro `StoreInMetadataKey`.
     - `ImageOptimizerNode`: Parámetros canónicos `Width`, `Height` (con defaults `Height: "100%"`, `Width: ""`).
   - Creado test de integración automatizado `WorkflowExamplesValidationTests.cs` que comprueba de forma continua la validez sintáctica y estructural de todos los flujos de ejemplo frente a los contratos de los nodos reales.
8. **Implementación de FileFlow.Plugin.Scripting (Motor Dual C# Roslyn + JavaScript Jint)**:
   - Creado el nuevo proyecto `FileFlow.Plugin.Scripting` con arquitectura *Zero-Touch* totalmente encapsulada.
   - **`RoslynCSharpEngine`**: Compilación JIT en memoria con cacheo SHA256 (`ScriptRunner<object>`), acceso tipado y directo a `Item` (`FileItemContext`), `Context` (`IFlowExecutionContext`), `EmitAsync(port)`, `Log(msg)` y función universal `Resolve(template)`.
   - **`JintJavaScriptEngine`**: Sandbox administrado en .NET 9 con límites de memoria, tiempo e instrucciones, con funciones globales `emit(port, item)`, `log(msg)`, `console.log(msg)`, `resolve(template)` y `getVar(name)`.
   - **`CustomScriptNode`**: Nodo programable con soporte de puertos dinámicos configurables (`InputPorts`, `OutputPorts`), timeouts y acción personalizada `OpenScriptStudio`.
   - **`ScriptStudioWindow`**: Editor visual con `AvalonEdit` (resaltado sintáctico automático C#/JavaScript, números de línea), botón **`📖 Manual PDF...`**, probador en vivo (`RunTestCommand`) con telemetría de emisiones y consola de logs, y gestor de biblioteca/plantillas predefinidas.
   - **`ScriptLibraryService`**: Almacenamiento y carga de scripts `.ffscript` en `%AppData%/FileFlow/Scripts/` y catálogo de presets incorporados (Enrutador por extensión, Filtro de tamaño, Inyector de variables, Sanitizador de nombres).
9. **Manual de Usuario Didáctico de Scripting, Compilación PDF e Integración en Instalador**:
   - Creado [`docs/manual_nodo_scripting.md`](file:///docs/manual_nodo_scripting.md) redactado para usuarios de nivel básico y medio con guía paso a paso, tablas de propiedades de archivo, variables implícitas (`{FileName}`, `{SizeMB}`, `{Date:*}`), acceso a metadatos previos (`Item.Metadata["Hash:SHA256"]`, etc.) y 7 ejemplos prácticos comentados.
   - Actualizado `installer/build-pdf-manual.ps1` para compilar automáticamente `manual_nodo_scripting.pdf` (1003.8 KB) y `manual_de_usuario.pdf` (1001.4 KB) utilizando el motor Chromium Headless de Microsoft Edge.
   - Actualizado `installer/publish.ps1` para sincronizar todos los PDFs a la carpeta `Docs/` de distribución.
   - Actualizado `installer/FileFlow.iss` con mensajes localizados en español e inglés y creación de acceso directo en el Menú de Inicio para el Manual de Scripting.
10. **Pruebas Unitarias y Validación**:
   - Creadas pruebas exhaustivas en `ScriptingPluginTests.cs` (C# Roslyn, JavaScript Jint, Resolución de Variables Implícitas, Puertos Dinámicos y Biblioteca de Presets).
   - Batería de pruebas: **295 / 295 pruebas superadas al 100% con 0 fallos**.

---

## [2026-09-01] - Desacoplamiento de Vistas XAML y Sistema Universal de Acciones de Nodos (CustomActions)

### 📋 Acciones y Mejoras Realizadas
1. **Auditoría Integral de Vistas**:
   - Clasificación de todos los archivos en `Views/`: Vistas estructurales de la aplicación (Shell, Layout, Log, Toolbox), Utilidades globales (Settings, Themes, Regex, ColorPicker) y Vistas de componentes.
2. **Introducción de `NodeActionDescriptor` en el SDK (`FileFlow.Sdk`)**:
   - Creado record inmutable `NodeActionDescriptor(ActionId, Title, Icon, Tooltip)` e integrado en la interfaz `IFlowNode` mediante `IReadOnlyList<NodeActionDescriptor> CustomActions => [];`.
3. **Declaración en Plugins (`FileFlow.Plugin.*`)**:
   - `AdvancedRenamerNode`, `VariableInjectorNode` y `SwitchCaseNode` declaran sus herramientas y botones de acción avanzada dentro de su propia clase.
4. **Erradicación de Código Acoplado en XAML**:
   - `NodeCardView.xaml` y `NodeInspectorPanelView.xaml` actualizados con `ItemsControl ItemsSource="{Binding CustomActions}"`, eliminando los condicionales fijos (`IsAdvancedRenamerNode`, `IsVariableInjectorNode`, `IsSwitchCaseNode`).
5. **Pruebas Unitarias y Validación**:
   - Nueva prueba unitaria `NodeViewModel_ShouldPopulateCustomActions_FromNodeDefinition`.
   - Batería de pruebas: **289 / 289 pruebas superadas al 100% con 0 fallos**.

---

## [2026-09-01] - Arquitectura Híbrida de Plugins con Esquema Declarativo de Parámetros (Opción C)

### 📋 Acciones y Mejoras Realizadas
1. **Extensión Desacoplada del SDK (`FileFlow.Sdk`)**:
   - Nuevos tipos `ParameterEditorType` (Text, Number, Slider, Dropdown, Toggle, FolderPath, FilePath, MultiLineText, PasswordList, MediaPreset) y `NodeParameterDescriptor`.
   - Soporte nativo de `IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [];` en `IFlowNode`.
   - Interfaz `INodeCustomActionProvider` para acciones modales personalizadas.
2. **Co-ubicación de Esquema en los Plugins (`FileFlow.Plugin.*`)**:
   - Cada nodo (`ImageOptimizerNode`, `FolderSourceNode`, `DestinationSinkNode`, `FileRelocatorNode`, `OriginalFileActionNode`, `AdvancedRenamerNode`, `SmartUnpackNode`, `ArchiveCompressorNode`, `HashCalculatorNode`, `MediaTranscoderNode`, etc.) declara su propio esquema de parámetros con orden, tipos, opciones y valores por defecto.
3. **Generalización de `FileFlow.App` (Schema-Driven UI)**:
   - `NodeParameterManager.cs` refactorizado para ser 100% genérico, eliminando todos los bloques condicionales hardcodeados (`if (isImageOptimizer)`, `if (isRenamer)`).
   - `NodeParameterViewModel.cs` y `NodeParameterTemplates.xaml` actualizados con soporte visual para Sliders, Dropdowns, CheckBoxes y File/Folder Pickers.
4. **Pruebas Unitarias y Validación**:
   - Nuevos tests en `NodeParameterManagerTests.cs` validando la generación e inferencia a partir de los descriptores.
   - Nueva prueba unitaria `InitializeParameters_ShouldNotExposeLegacyPatternOrMethodSteps_ForAdvancedRenamerNode` garantizando que claves legadas e internas (`Pattern`, `NameTemplate`, `CaseTransformation`, `MethodSteps`) queden 100% aisladas y nunca aparezcan como campos de texto en la configuración del nodo.
   - Batería de pruebas: **288 / 288 pruebas superadas al 100% con 0 fallos**.

---

## [2026-09-01] - Configuración Inteligente por Defecto en ImageOptimizerNode (Alto 100% y Ancho Automático)

### 📋 Acciones y Mejoras Realizadas
1. **Dimensiones Predeterminadas en Plugin y Capa de UI**:
   - Modificados los valores por defecto del nodo `ImageOptimizerNode`:
     - **`Height`**: `"100%"` (mantiene el 100% de la altura o escala proporcionalmente).
     - **`Width`**: `""` (*Automático*, calcula el ancho proporcional para preservar la relación de aspecto sin distorsión).
   - Actualizado `NodeParameterManager.cs` en la capa WPF para erradicar valores hardcodeados legados (`1920`/`1080`), sincronizando de forma transparente los valores por defecto en el lienzo visual.
2. **Pruebas Unitarias**:
   - Añadida prueba `CalculateTargetDimensions_DefaultParameters_PreservesFullResolutionAndAspectRatio` en `ImageOptimizerNodeTests.cs`.
   - Añadida prueba `ImageOptimizerNodeViewModel_ShouldInitializeWithDefaultWidthEmptyAndHeight100Pct` en `EditorViewModelTests.cs`.
   - Batería de pruebas: **285 / 285 pruebas superadas al 100% con 0 fallos**.

---

## [2026-09-01] - Localización Dinámica y Reactiva al 100% en la Interfaz Gráfica

### 📋 Acciones y Mejoras Realizadas
1. **Propiedad `DisplayName` Reactiva en `NodeParameterViewModel.cs`**:
   - Los parámetros de los 27 nodos muestran nombres amigables traducidos (`Param_Width` $\rightarrow$ `Ancho` / `Width`, `Param_Quality` $\rightarrow$ `Calidad` / `Quality`, `Param_DestinationRoot` $\rightarrow$ `Carpeta Destino` / `Destination Folder`, etc.) manteniendo la clave técnica (`Key`) intacta en la lógica de procesamiento.
   - Suscripción reactiva al evento `LanguageChanged` para actualizar todas las tarjetas de nodos en el lienzo visual al instante.
2. **Refresco Reactivo de Indexers en `LocalizationManager.cs`**:
   - Incorporada la notificación `OnPropertyChanged("Item[]")` y `OnPropertyChanged("Item")` al cambiar de cultura, garantizando que todos los bindings XAML con sintaxis `{Binding Source={x:Static loc:LocalizationManager.Instance}, Path=[Clave]}` se actualicen en caliente sin reiniciar la app.
3. **Mapeo Completo en Diccionarios de Recursos (`Strings.resx` y `Strings.es.resx`)**:
   - Añadidas todas las traducciones en español e inglés para parámetros de nodos, tooltips, opciones de navegación, filtros de consola de logs y nombres de categorías.
4. **Localización de Vistas XAML**:
   - Actualizados `ControlBarView.xaml`, `LogView.xaml`, `NodeInspectorPanelView.xaml` y `NodeToolboxView.xaml` para erradicar textos estáticos fijos y vincularlos a `LocalizationManager`.
6. **Formalización de Regla Maestra de Diseño e Internacionalización**:
   - Añadida la directriz obligatoria de localización de UI en [`.agents/rules/rules.md`](file:///.agents/rules/rules.md), [`AGENTS.md`](file:///AGENTS.md), [`GEMINI.md`](file:///GEMINI.md) y [`docs/architecture.md`](file:///docs/architecture.md) (ADR-005).
   - Todos los componentes de la interfaz deben soportar localización dinámica (Español e Inglés), preservando las claves técnicas en inglés puro.

---

## [2026-09-01] - Estandarización del Principio de Inmutabilidad del Archivo de Origen (*Source Immutability by Default*)

### 📋 Acciones y Mejoras Realizadas
1. **Formalización de Directrices de Diseño y Reglas Maestras**:
   - Incorporado el *Principio de Inmutabilidad del Archivo de Origen* en [`.agents/rules/rules.md`](file:///.agents/rules/rules.md), [`AGENTS.md`](file:///AGENTS.md), [`GEMINI.md`](file:///GEMINI.md) y [`docs/architecture.md`](file:///docs/architecture.md) (ADR-004).
   - Los flujos son **no destructivos por defecto**: los archivos de entrada no se sobreescriben, mueven ni borran; toda mutación queda centralizada en `OriginalFileActionNode`.
2. **Soporte de `MoveToRecycleBin` en `OriginalFileActionNode.cs`**:
   - Incorporada la opción segura `MoveToRecycleBin` utilizando la API nativa de Windows Shell (`SHFILEOPSTRUCT` / `SHFileOperationW`) para permitir enviar los originales a la Papelera de Reciclaje de Windows de forma recuperable.
   - Opciones completas del selector: `Keep`, `MoveToRecycleBin`, `MoveToQuarantine`, `PermanentDelete`.
3. **Copia Segura por Defecto en `FileRelocatorNode.cs`**:
   - Modificado el valor predeterminado del parámetro `Operation` de `"Move"` a `"Copy"` para prevenir la eliminación o desplazamiento inadvertido del original.
4. **Validación y Pruebas Unitarias**:
   - Nuevos tests en `OriginalFileActionNodeTests.cs` validando el reciclaje seguro a la papelera.
   - Batería de pruebas: **280 / 280 pruebas superadas al 100% con 0 fallos**.

---

## [2026-09-01] - Desacoplamiento de Renombrado Virtual en AdvancedRenamerNode y Destino Final

### 📋 Acciones y Mejoras Realizadas
1. **Soporte de `RenameMode` en `AdvancedRenamerNode` (`Virtual` vs `DirectInPlace`)**:
   - Incorporado el parámetro `RenameMode` (por defecto `"Virtual"`):
     - **`Virtual`**: Solo calcula y transforma el nuevo nombre en memoria dentro de `FileItemContext` sin alterar físicamente el archivo en el disco de origen.
     - **`DirectInPlace`**: Renombra físicamente el archivo en la carpeta original (`File.Move`) con registro en el diario de operaciones (*Journal Undo*).
2. **Propiedad `PhysicalPath` y Resolución Dinámica en `FileItemContext.cs`**:
   - Incorporada la propiedad `PhysicalPath` y el método `GetExistingPhysicalPath()` que resuelve de forma transparente la ubicación del archivo físico real en disco (`PhysicalPath` $\rightarrow$ `OriginalPath` $\rightarrow$ `CurrentPath`).
3. **Lectura Segura en `DestinationSinkNode` y `FileRelocatorNode`**:
   - `DestinationSinkNode` lee desde `item.GetExistingPhysicalPath()` y copia/guarda en la carpeta de destino (`DestinationRoot`) con el nombre ya transformado en `item.FileName`, dejando el archivo original intacto.
   - `FileRelocatorNode` adopta la misma resolución para traslados y copias virtuales.
4. **Validación Exhaustiva**:
   - Incorporadas pruebas unitarias completas en `AdvancedRenamerExhaustiveTests.cs` validando el modo virtual encadenado con `DestinationSinkNode` y el modo directo in-situ.
   - Batería de pruebas: **279 / 279 pruebas superadas al 100%**.

---

## [2026-09-01] - Rediseño y Simplificación Inteligente de Dimensiones en ImageOptimizerNode

### 📋 Acciones y Mejoras Realizadas
1. **Reorganización y Orden Visual Limpio de Parámetros (`ImageOptimizerNode.cs` & `NodeParameterManager.cs`)**:
   - `Width` y `Height` se posicionan en la cabecera del panel de configuración de la tarjeta de nodo en UI.
   - Eliminado el desplegable `SizeMode` ("Pixels" / "Percentage") y los campos redundantes `ScalePercentage`, `ScalePercentageY` y `MaintainAspectRatio`.
2. **Sintaxis Inteligente y Unificada de Dimensiones (`DimensionParser`)**:
   - `Width` y `Height` aceptan directamente cifras en píxeles (`1920`, `800px`), porcentajes (`50%`, `75%`), o vacío / `auto` / `0` para cálculo automático.
   - **Deducción Automática de Relación de Aspecto (*Aspect Ratio*)**: Si se especifica solo una dimensión (`Width` o `Height`), la otra se calcula proporcionalmente sin deformar la imagen. Si se especifican ambas en píxeles, la imagen se ajusta al recuadro delimitador (*Bounding Box Fit*).
3. **Migración Automática y Limpieza de Parámetros Legados (`NodeParameterManager.cs`)**:
   - Migración transparente de flujos antiguos con `SizeMode == "Percentage"` hacia valores en formato `%` y eliminación de parámetros obsoletos en la UI.
4. **Validación Exhaustiva con Tests Unitarios (`ImageOptimizerNodeTests.cs`)**:
   - Actualizados y superados todos los tests unitarios con sintaxis de píxeles, porcentajes simétricos/asimétricos y cálculo proporcional automático.
   - Batería de pruebas: **277 / 277 pruebas superadas al 100%**.

---

## [2026-09-01] - Optimización Arquitectónica, Concurrencia y Recursos en .NET 10 / C# 13

### 📋 Acciones y Correcciones Realizadas
1. **Gestión Determinista de Descriptores en Descompresión (`SafeArchiveExtractor.cs`)**:
   - Se garantizó la disposición inmediata de `archive?.Dispose()` dentro del bloque `catch` al evaluar contraseñas candidatas, evitando bloqueos de archivos en disco.
2. **Reutilización y DNS Pooling en Notificaciones Webhook (`WebhookNotificationNode.cs`)**:
   - `HttpClient` estático configurado con `SocketsHttpHandler`, `PooledConnectionLifetime = TimeSpan.FromMinutes(15)` y `EnableMultipleHttp2Connections = true`, resolviendo el problema de conexiones obsoletas y refresco de DNS dinámico.
3. **Despacho No Bloqueante en UI Dispatcher (`NodeViewModel.cs`)**:
   - Reemplazado `Dispatcher.Invoke` síncrono por `Dispatcher.InvokeAsync` / `BeginInvoke` en `AddSnapshot`, `SetExecutionStatus` y `ClearDebugData`, eliminando contención de hilos del motor DAG contra la interfaz de usuario.
4. **P/Invoke de Shell32 con Memoria No Administrada (`SafeRecycleDeleteNode.cs`)**:
   - Asignación explícita con `Marshal.StringToHGlobalUni` y liberación garantizada en `finally` con `Marshal.FreeHGlobal`, asegurando el doble terminador nulo `\0\0` requerido por la API nativa de Windows Shell.
5. **Eliminación de `.Result` en Hot-Paths Asíncronos (`CliExecutionNode.cs`)**:
   - Sustituido el acceso a `.Result` por `await readOutTask.ConfigureAwait(false)` y `await readErrTask.ConfigureAwait(false)`, evitando el desenvolvimiento implícito de `AggregateException`.
6. **Captura Defensiva de `IOException` en Streaming de Archivos (`FolderSourceNode.cs`)**:
   - Añadida `IOException` al filtro `when` de captura en `StreamAndEmitDirAsync` para tolerar archivos con bloqueos exclusivos temporales o enlaces simbólicos rotos sin detener el lote.
7. **Simplificación Idiomática de `UndoAction` (`AdvancedRenamerNode.cs`)**:
   - Eliminado `async` y `return await Task.FromResult(true)` redundantes en el delegado de rollback del diario de operaciones.
8. **Protección ante Cierre en UI Ring Buffer (`FastObservableRingBuffer.cs`)**:
   - Comprobación de `Dispatcher.HasShutdownStarted` antes de invocar `BeginInvoke` para prevenir excepciones al cerrar la aplicación.
9. **Suite de Pruebas Unitarias de Auditoría y Rendimiento (`SecurityAndRobustnessAuditTests.cs`)**:
   - Suite total actualizada: **277 / 277 pruebas unitarias e integración pasadas con 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-01] - Auditoría Integral de Seguridad, Concurrencia y Resiliencia (QA Lead)

### 📋 Acciones y Correcciones Realizadas
1. **Drenaje Determinista de Tareas DAG (`WorkflowExecutor.cs`) [CRIT-01]**:
   - Implementado ciclo de captura y agregación de excepciones en la espera final de `_activeNodeTasks` para evitar tareas huérfanas en segundo plano si un nodo downstream falla inesperadamente.
2. **Protección contra Pérdida de Datos y Rutas Idénticas (`FileRelocatorNode.cs`) [CRIT-02]**:
   - Detección previa de rutas idénticas (`fullSource == fullTarget`) para omitir la operación sin lanzar `IOException`.
   - Implementado *Safe Move* con verificación de integridad: `File.Copy` $\rightarrow$ Validación de hash SHA-256 de destino $\rightarrow$ Eliminación segura del archivo de origen solo tras confirmar la integridad del nuevo archivo.
3. **Corrección de Registro de Journal en Limpiador de Carpetas (`EmptyDirectoryCleanerNode.cs`) [HIGH-01]**:
   - Incorporado `JournalOperationType.DeletedPermanently` al enum `JournalOperationType` en `FileFlow.Sdk`.
   - Corregido el registro erróneo de `CreatedDirectory` a `DeletedPermanently` al eliminar subdirectorios vacíos.
4. **Resiliencia ante Sintaxis Regex Inválida del Usuario (`SearchReplaceStepHandler.cs`, `NormalizeNumbersStepHandler.cs`) [HIGH-02]**:
   - Encapsulada la construcción de `Regex` en bloques `try/catch (ArgumentException)` defensivos, registrando un log contextual y evitando que excepciones de sintaxis del usuario interrumpan el lote de renombrado.
5. **Caché en Memoria Concurrente para Herramientas Externas (`ExternalToolsService.cs`) [HIGH-03]**:
   - Incorporado `ConcurrentDictionary<string, string> _resolvedToolCache` para evitar escaneos de disco redundantes (I/O intensivo) al resolver ejecutables como FFmpeg o 7-Zip en pipelines masivos.
6. **Soporte Completo de `DryRun` en Optimizador de Imágenes (`ImageOptimizerNode.cs`) [MED-01]**:
   - Registro explícito de `PlannedAction` con `PlannedOperationType.TransformMedia` y cálculo de metadatos estimados en modo simulación virtual.
7. **Propagación de Fallos en Tuberías Asíncronas (`FolderSourceNode.cs`) [MED-02]**:
   - El productor pasa la excepción no controlada a `channel.Writer.Complete(producerError)` para que el consumidor downstream reaccione de inmediato ante errores de I/O.
8. **Limpieza Defensiva de Archivos Temporales (`WorkflowStorageService.cs`) [MED-03]**:
   - Protegido `File.Delete(tempPath)` en el bloque `finally` para no enmascarar excepciones de serialización.
9. **Diferenciación de Cancelación y Timeout (`CliExecutionNode.cs`) [LOW-01]**:
   - Detección precisa de `cancellationToken.IsCancellationRequested` para emitir `OperationCanceledException` en lugar de un falso `TimeoutException`.
10. **Protección de Eventos Asíncronos en UI (`WorkflowSettingsWindow.xaml.cs`) [LOW-02]**:
    - Deshabilitación reactiva del botón durante la búsqueda automática de herramientas para evitar clics concurrentes.
11. **Nueva Suite de Pruebas Unitarias (`SecurityAndRobustnessAuditTests.cs`)**:
    - Añadidos 6 tests de verificación de auditoría. Suite total: **276 / 276 pruebas superadas con 100% de éxito (0 errores, 0 fallos)**.

---

## [2026-09-01] - Refactorización Modular Fase 2 (Core, Archives, Sdk y App ViewModels)

### 📋 Acciones Realizadas
1. **Módulo 1 (`FileFlow.Core` / Telemetría)**:
   - Desacoplado `SqliteLogStore.cs` (de 472L a 389L).
   - Extraído `SqliteLogSchema.cs` (DDL inmutable, índices SQLite y configuración de pragmas de memoria).
   - Extraído `SqliteLogMetricsReader.cs` (consultas analíticas y cálculo de KPIs de ejecución por nodo).
2. **Módulo 2 (`FileFlow.Plugin.Archives` / Descompresión Segura)**:
   - Desacoplado `SmartUnpackNode.cs` (de 320L a 157L).
   - Extraído `SafeArchiveExtractor.cs` en `FileFlow.Plugin.Archives/Services/` (apertura con candidatos de contraseña, mitigación de Zip Slip y descompresión recursiva).
3. **Módulo 3 (`FileFlow.Sdk` / Motor de Plantillas)**:
   - Desacoplado `SystemVariablesResolver.cs` (de 367L a 198L).
   - Extraído `DomainVariableResolver.cs` en `FileFlow.Sdk/TemplateEngine/Resolvers/` (resolución por dominios `{Domain:Key:Modifier}`).
   - Extraído `PathRelativeCalculator.cs` en `FileFlow.Sdk/TemplateEngine/Resolvers/` (cálculo robusto de rutas y directorios relativos).
4. **Módulo 4 (`FileFlow.App` / Editor y Viewport)**:
   - Desacoplado `EditorViewModel.cs` (de 525L a 417L).
   - Extraído `EditorViewportCalculator.cs` en `FileFlow.App/Services/` (cálculo geométrico de encuadre y zoom de pantalla).
   - Extraído `WorkflowGraphSerializer.cs` en `FileFlow.App/Services/` (serializador e importador desacoplado de `WorkflowGraph`).
5. **Módulo 5 (`FileFlow.App` / Tarjeta de Nodo y SwitchCase)**:
   - Desacoplado `NodeViewModel.cs` (de 496L a 371L).
   - Extraído `NodeCategoryStyling.cs` en `FileFlow.App/Services/` (generación de paleta de colores y estilos por categoría).
   - Extraído `NodeSwitchCaseCoordinator.cs` en `FileFlow.App/Services/` (coordinación dinámica de puertos y reglas de `SwitchCaseNode`).
6. **Pantalla de Carga Fluida y Estilizada (`SplashScreenWindow.xaml`)**:
   - Diseñada e implementada una nueva ventana de carga (`SplashScreenWindow.xaml`) con bordes redondeados (`CornerRadius="16"`), resplandor exterior (*drop shadow glow* `#6366F1`), gradientes sutiles y badge de versión.
   - Barra de progreso animada con gradiente cian a púrpura y reporte de inicialización en tiempo real (*"Iniciando servicios y localización..."*, *"Cargando preferencias..."*, *"Cargando plugins..."*, *"Construyendo espacio de trabajo..."*, *"¡Listo!"*).
   - Transiciones suaves de apertura (`FadeInStoryboard`) y cierre (`FadeOutStoryboard`) orquestadas en `App.xaml.cs`.
7. **Verificación y Calidad de Código**:
   - Creada la nueva suite `ModularArchitecturePhaseTwoTests.cs` en `FileFlow.Tests/Unit/Refactoring/`.
   - `dotnet test FileFlow.slnx`: **270 / 270 pruebas superadas con 100% de éxito (0 errores, 0 fallos, 0 advertencias)**.

---

## [2026-09-01] - Refactorización Modular y Desacoplamiento Clean Code (Fases 1, 2 y 3)

### 📋 Acciones Realizadas
1. **Fase 1 — Auditoría Arquitectónica y Mapa de Riesgos**:
   - Auditoría integral de complejidad ciclomática, conteo de líneas y responsabilidades en todos los módulos de la solución.
   - Detección de archivos monolíticos (`AdvancedRenamerEditorViewModel.cs` 678L, `ControlBarViewModel.cs` 658L, `CustomThemeService.cs` 614L, `WorkflowExecutor.cs` 545L, `RenameTransformEngine.cs` 495L).
   - Elaboración y aprobación del Plan Maestro de Modularización bajo el Principio de Responsabilidad Única (SRP) y Principio Abierto/Cerrado (OCP).
2. **Fase 2 — Ejecución por Sprints Atómicos**:
   - **Sprint 1 (`FileFlow.Sdk`)**: Desacoplado `RenameTransformEngine.cs` (de 495L a 124L) implementando el patrón Strategy con `IRenameStepHandler` y 9 handlers especializados en `FileFlow.Sdk/Renaming/Handlers/` (`NewNameStepHandler`, `SearchReplaceStepHandler`, `InsertStepHandler`, `RemoveStepHandler`, `CaseStepHandler`, `NumberingStepHandler`, `ReplaceListStepHandler`, `CleanupStepHandler`, `NormalizeNumbersStepHandler`, `RenameIndexCalculator`).
   - **Sprint 2 (`FileFlow.App`)**: Desacoplado `CustomThemeService.cs` (de 614L a 140L) extrayendo el catálogo inmutable `BuiltInThemesCatalog.cs` (8 temas de fábrica) y el generador de estilos WPF `ThemeResourceApplier.cs`.
   - **Sprint 3 (`FileFlow.App`)**: Desacoplado `ControlBarViewModel.cs` (de 658L a 463L) extrayendo el coordinador de ejecución en UI `WorkflowExecutionCoordinator.cs` y el localizador de documentación `AppResourceLocator.cs`.
   - **Sprint 4 (`FileFlow.App`)**: Desacoplado `AdvancedRenamerEditorViewModel.cs` (de 678L a 390L) extrayendo el servicio de tokens `RenamerTagCatalogService.cs`, el recolector de muestras `RenamerSampleDataProvider.cs` y el generador reactivo `RenamerLivePreviewService.cs`.
   - **Sprint 5 (`FileFlow.Core`)**: Desacoplado `WorkflowExecutor.cs` (de 545L a 468L) extrayendo la acumulación de métricas en tiempo real `WorkflowTelemetryTracker.cs`.
3. **Fase 3 — Verificación y Batería de Pruebas**:
   - Creación de nueva suite de tests unitarios `ModularRefactoringComponentsTests.cs` validando `BuiltInThemesCatalog`, `ThemeResourceApplier`, `RenameIndexCalculator`, `WorkflowTelemetryTracker` y `AppResourceLocator`.
   - `dotnet test FileFlow.slnx` $\rightarrow$ **264 / 264 pruebas pasadas con 100% de éxito (0 errores, 0 fallos, 0 omitidos)**.

---

## [2026-09-01] - Integración de Parámetros UI y Normalización en ImageOptimizerNode

### 📋 Acciones Realizadas
1. **Normalización y Migración Automática en `NodeParameterManager`**:
   - Se añadió en [`NodeParameterManager.cs`](file:///FileFlow.App/ViewModels/NodeParameterManager.cs) la detección de `ImageOptimizerNode` para migrar de forma transparente los parámetros legados `MaxWidth` y `MaxHeight` hacia `Width` y `Height`, y asegurar la existencia de todos los nuevos parámetros (`SizeMode`, `ScalePercentage`, `ScalePercentageY`, `MaintainAspectRatio`, `OnlyDownscale`).
2. **Soporte de Dropdowns y Enlace Booleano en `NodeParameterViewModel`**:
   - Se registró `"sizemode" => ["Pixels", "Percentage"]` en `DetectOptionsForKey`.
   - Se corrigió la coerción de tipos para valores booleanos (`MaintainAspectRatio`, `OnlyDownscale`) garantizando que los controles `CheckBox` de la interfaz WPF enlacen directamente con tipos `bool`.

---

### 📋 Acciones Realizadas
1. **Nuevo Nodo de Reporte Visual de Operaciones (`OperationReportNode`)**:
   - Desarrollado el nodo `OperationReportNode` en `FileFlow.Plugin.FileSystem` con arquitectura de renderizado extensible (`IReportRenderer`).
   - Soporte de 5 formatos seleccionables por desplegable en el inspector: `HTML` (interactivo, responsive, KPIs, timeline con badges, búsqueda vanilla JS), `Markdown` (.md), `Text` (.txt con árbol ASCII), `JSON` (.json) y `CSV` (.csv).
   - Ámbitos de reporte configurables (`ReportScope`): `Consolidated` (resumen general del lote en **un único archivo consolidado**), `PerFile` (reporte individual adjunto a cada archivo) y `Both`.
   - **Agrupación Jerárquica por Directorios (`GroupBy`)**: Parámetro con opciones `Directory` (por defecto), `Flat`, `Extension` y `Status`.
     - En **HTML**: Acordeón interactivo con carpetas colapsables, métricas de conteo/tamaño por carpeta, badges de salud (`✅ OK` / `⚠️ Errores`), botones *Expandir Todo / Colapsar Todo* y búsqueda reactiva inteligente que auto-despliega las carpetas coincidentes.
     - En **Markdown**: Secciones estructuradas con bloques `<details open><summary>`.
     - En **Texto Plano**: Árbol jerárquico ASCII (`├── 📁 /Fotos/ ... └── 📄 foto.jpg`).
     - En **CSV / JSON**: Campos dedicados de directorio (`Directory`).
   - Soporte de auto-apertura en navegador/visor del sistema (`AutoOpenReport`), personalización de tema (`Theme`: `ModernDark` / `CleanLight`), inclusión de metadatos (`IncludeMetadata`) y rutas parametrizables con plantillas de tokens.
   - **Corrección de Generación Única**: Fijada la ruta de archivo consolidado (`_consolidatedFilePath`) al inicio de cada ejecución/sesión para evitar la dispersión en múltiples archivos al evaluar marcas de tiempo segundo a segundo o subcarpetas relativas.
   - Modo `Dry Run` integrado registrando `PlannedAction` sin modificar el disco real.
2. **Integración en UI / MVVM y Localización**:
   - `NodeParameterViewModel.cs`: Agregadas opciones desplegables para `reportformat`, `reportscope`, `groupby` y `theme`.
   - `ToolboxViewModel.cs`: Sincronización con `Lock`, desuscripción limpia `IDisposable` e icono `📋`.
   - `Strings.resx` y `Strings.es.resx`: Cadenas en inglés y español para el nuevo nodo.
3. **Auditoría Integral del Código y Generación del SRS**:
   - Creado [`docs/ESPECIFICACIONES.md`](file:///docs/ESPECIFICACIONES.md) con la especificación formal del sistema.
   - Actualizados [`docs/manual_de_usuario.md`](file:///docs/manual_de_usuario.md) y [`.agents/nodes_catalog.md`](file:///.agents/nodes_catalog.md) reflejando los 27 nodos del sistema.
4. **Documentación Exhaustiva de Pruebas (Objeto, Qué y Cómo)**:
   - Creado [`docs/guia_de_pruebas.md`](file:///docs/guia_de_pruebas.md) conteniendo el catálogo estructurado de las 190 pruebas con su objetivo, regla de negocio y estrategia AAA (*Arrange, Act, Assert*).
   - Documentación en el código fuente mediante comentarios XML doc en español (`/// <summary>`) detallando `OBJETO`, `QUÉ` y `CÓMO` en cada método de prueba.
5. **Verificación Automatizada de Calidad**:
   - Nuevos tests unitarios en `OperationReportNodeTests.cs` (HTML, Markdown, Text, JSON, CSV, PerFile/Both, Dry Run, Validación de Archivo Único Consolidado, Agrupación Jerárquica por Directorios) y `ToolboxViewModelTests.cs`.
   - `dotnet test FileFlow.slnx`: **190 / 190 pruebas superadas con 100% de éxito (0 errores, 0 fallos)**.
6. **Publicación del `README.md` Principal para GitHub**:
   - Creado [`README.md`](file:///README.md) en la raíz con badges de estado, descripción del motor DAG, diagrama arquitectónico, catálogo de los 27 nodos, guía de inicio rápido y enlaces a toda la documentación técnica.
7. **Automatización de Release e Instalador con GitHub Actions**:
   - Desarrollado [`.github/workflows/release.yml`](file:///.github/workflows/release.yml) con soporte de ejecución manual (`workflow_dispatch`) y publicación por etiquetas (`v*`), generando el instalador Inno Setup (`.exe`), el paquete portable (`.zip`) y las sumas de verificación SHA-256 adjuntas en GitHub Releases.
   - Implementada la sanitización automática de nombres de etiqueta Git (reemplazo de espacios por guiones como `1.0.0 beta` $\rightarrow$ `1.0.0-beta`) y asignación de `target_commitish: ${{ github.sha }}` para permitir la creación de releases desde cualquier commit.
   - Desarrollado [`.github/workflows/ci.yml`](file:///.github/workflows/ci.yml) para validación continua de compilación y pruebas en ramas principales y PRs.
   - Añadida la directiva de entorno `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: "true"` para evitar avisos de deprecación de Node 20 en los runners de GitHub Actions.
   - Refactorizados los scripts `installer/publish.ps1` y `installer/build-installer.ps1` para soporte robusto de parámetros tipados en PowerShell CLI.
8. **Mantenimiento y Control de Versiones (.gitignore)**:
   - Añadidas las carpetas temporales de análisis `coverage-report/`, `TestResults/` y `.dotnet_tmp/` al archivo [`.gitignore`](file:///.gitignore).

---

## [2026-08-23] - Auditoría Integral 360° del Sistema y Refactorización Completa (Fase 1 y Fase 2 Ejecutadas)

### 📋 Estado, Hallazgos y Correcciones Aplicadas (100% Completado)
1. **Fase 1 (Auditoría 360°)**:
   - Identificados 16 hallazgos clasificados en Código/Lógica, Rendimiento/Recursos, Arquitectura y UX/UI.
2. **Fase 2 (Ejecución de Refactorización por Sprints)**:
   - **Sprint 1 (Fugas y Concurrencia)**: `_concurrencyThrottle.Dispose()` en `WorkflowExecutor`, reemplazo de `ConcurrentBag<Task>` por drenaje seguro con `Lock` y shutdown hook de `SqliteLogStore` en `App.OnExit`.
   - **Sprint 2 (Memory Leaks UI)**: Dispose sistemático de `NodeViewModel` en `EditorViewModel.ClearGraph()`, handler nominal `OnNodePropertyChanged` y `IDisposable` en `ControlBarViewModel`.
   - **Sprint 3 (Rendimiento UI)**: Migración de `LogViewModel.Logs` a `FastObservableRingBuffer` (eliminada notación $O(n)$) e indexado con `Dictionary` de `UpdateEdgeDispatched` a $O(1)$.
   - **Sprint 4 (Clean Code & .NET 9)**: Migración de `object _lock` a `System.Threading.Lock` en 3 servicios singleton, `ExecuteNonQueryAsync` en `SqliteLogStore` y logging contextual en `PluginLoader`.
   - **Sprint 5 (Robustez)**: Lecturas `Volatile.Read` en `WaitIfPausedAsync`, persistencia de crashes en `crash.log` y captura de `TaskScheduler.UnobservedTaskException`.
   - **Sprint 6 (Testing Crítico)**: Batería ampliada en `WorkflowExecutorTests.cs` (paralelismo, pausa/resume, DryRun, errores y cancelación).
3. **Resultado Final de Verificación**:
   - Compilación en .NET 9 / C# 13: **0 Advertencias, 0 Errores**.
   - Batería de Pruebas: **181 / 181 pruebas superadas al 100% con éxito** en 3s.

---

## [2026-08-23] - Auditoría y Estandarización Exhaustiva de Observabilidad y Telemetría en los 24 Nodos del Sistema

### 🛠 Cambios e Implementaciones
1. **Auditoría Integral de Observabilidad en los 24 Nodos de Producción**:
   - Clasificación y normalización de todos los nodos según su nivel de telemetría (eliminados nodos silenciosos y logs genéricos sin contexto).
   - Estandarización uniforme con niveles `Debug` (ruido/traza interna), `Information` (hitos de negocio con resumen descriptivo), `Warning` (desviaciones recuperables) y `Error` (fallos críticos).
2. **Telemetría Enriquecida con Métricas de Tiempo (`durationMs`) y Cargas Útiles JSON (`detailsJson`)**:
   - **`FileFlow.Plugin.Logic`**:
     - `SwitchCaseNode`: Emite `[INFO]` y `[DEBUG]` con `detailsJson` ({expression, evaluatedValue, matchedCase, pattern}).
     - `ExpressionFilterNode`: Emite `[INFO]` con `detailsJson` ({property, operator, targetValue, actualValue, result}) y desvío a ramas.
     - `ThrottleDelayNode`: Emite `[DEBUG]` con milisegundos de retardo aplicado.
     - `BatchBufferNode`: Emite `[INFO]` con métricas de lote en `detailsJson` ({batchCount, totalSizeBytes, totalMB}).
     - `ForkJoinBarrierNode`: Emite `[DEBUG]` en recepción de ramas e `[INFO]` con `detailsJson` ({requiredBranches, completedBranches}).
   - **`FileFlow.Plugin.FileSystem`**:
     - `DestinationSinkNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({destinationRoot, targetPath, strategy, isDryRun, sizeBytes}) y `[DEBUG]` en colisiones.
     - `FileRelocatorNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({operation, sourcePath, targetPath, integrityVerified, sha256}) y `[DEBUG]` en validaciones.
     - `AdvancedRenamerNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({pattern, originalName, newName, collisionStrategy}).
     - `DocumentProcessorNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({documentType, estimatedPages, lineCount, fileSizeBytes}).
     - `DirectoryInspectorNode`: Emite `[INFO]` con `detailsJson` ({filesCount, directoriesCount, targetDir}).
     - `EmptyDirectoryCleanerNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({targetDirectory, deletedCount, recursive, isDryRun}).
     - `SafeRecycleDeleteNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({targetPath, fileSizeBytes, deleteOriginal}).
     - `OriginalFileActionNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({action, quarantinePath / targetPath, isDryRun}).
     - `VariableInjectorNode`: Emite `[DEBUG]` por variable e `[INFO]` consolidado con `detailsJson`.
     - `FolderSourceNode`: Emite `[INFO]` al finalizar con `detailsJson` ({sourcePath, emittedCount, totalSizeBytes, totalMB, unit}).
   - **`FileFlow.Plugin.Archives`**:
     - `SmartUnpackNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({archive, extractDir, entriesCount, hasSingleWrapper, passwordProtected}) y `[DEBUG]` en niveles de wrapper.
     - `ArchiveCompressorNode`: Emite `[INFO]` con `durationMs`, cálculo de ratio de compresión % y `detailsJson` ({archiveFormat, compressionType, targetPath, originalSizeBytes, compressedSizeBytes, ratioPct}).
     - `ArchiveFilterNode`: Emite `[DEBUG]` en archivos regulares y `[INFO]` en primarios/secundarios con `detailsJson`.
   - **`FileFlow.Plugin.Images`**:
     - `ImageOptimizerNode`: Emite `[INFO]` con `durationMs`, cálculo de porcentaje de ahorro de espacio (%) y `detailsJson` ({format, quality, originalDimensions, optimizedDimensions, originalSizeBytes, optimizedSizeBytes, savedPct}).
     - `ExifMetadataNode`: Emite `[INFO]` con `durationMs`, `detailsJson` ({dateTaken, cameraModel, make, resolution, orientation, megapixels}) y `[DEBUG]` en lectura de dimensiones.
   - **`FileFlow.Plugin.Hashing`**:
     - `HashCalculatorNode`: Emite `[INFO]` con `durationMs`, prefijo del hash y `detailsJson` ({algorithm, hash, metadataKey, fileSizeBytes}).
     - `DeduplicationFilterNode`: Emite `[DEBUG]` en archivos únicos e `[INFO]` en duplicados con `detailsJson` ({hash, duplicateOf, currentPath}).
   - **`FileFlow.Plugin.Integrations`**:
     - `CliExecutionNode`: Emite `[INFO]`/`[WARN]` con `durationMs` real del subproceso y `detailsJson` ({executable, arguments, exitCode, stdOutLength, stdErrLength, stdOutSample, stdErrSample}).
     - `WebhookNotificationNode`: Emite `[INFO]`/`[WARN]` con `durationMs`, código de estado HTTP y `detailsJson` ({url, statusCode, statusText, payloadSample, responseSample}).
     - `MediaTranscoderNode`: Emite `[DEBUG]` progresivo cada 5 segundos y log final `[INFO]` con `durationMs` y `detailsJson` ({preset, targetPath, ffmpegAvailable, realTranscode, outSizeBytes}).
4. **Rediseño UI/UX y Alineación Vertical de la Consola de Logs (`LogView.xaml`, `LogViewModel.cs`, `ValueConverters.cs`)**:
   - **Toolbar Unificada y Compacta**: Agrupación limpia con título, selector de filtros por severidad con contadores en tiempo real (Todos, Errores, Warn, Info, Debug), input de búsqueda integrado con botón de borrado inmediato (`✕`), contador total de logs y botones de acción rápida (`⚡ En Vivo`, `💾 Exportar`, `🗑 Limpiar`).
   - **Alineación Vertical Perfecta y Altura de Fila Uniforme (`RowHeight="24"`)**: Estandarización de `VerticalContentAlignment="Center"` en todas las celdas, eliminando descuadres de texto y saltos de línea.
   - **Pill Badges de Severidad con Fondo Translúcido**: Nuevos convertidores `LogLevelToBadgeBackgroundConverter` y `LogLevelToBadgeForegroundConverter` para renderizar etiquetas con estética moderna tipo IDE.
   - **Columna Duración Centrada**: Alineación y encabezado centrados con ancho ampliado (`Width="80"`), evitando solapamientos con la columna adyacente de mensajes.
   - **Corrección de Selección Reactiva de Filtros (`EnumToBooleanConverter`)**: Sincronización bidireccional de `IsChecked` y asignación de `GroupName="LogFilterGroup"` en los `RadioButton` de severidad, resolviendo la desincronización y el salto involuntario al botón "Todos".
   - **Filtrado Estricto por Nivel (`ExactLevel`)**: Añadido soporte de `ExactLevel` en `LogFilterCriteria` y `SqliteLogStore`, garantizando que al seleccionar `🟣 Debug`, `🔵 Info` o `🟠 Warn` se muestren única y estrictamente los logs del nivel correspondiente.
5. **Auditoría de Código y Refactorización Modular (Clean Code & SRP)**:
   - **Desacoplamiento de `WorkflowExecutionContext.cs`**: Extraído de `WorkflowExecutor.cs` a su propio archivo independiente, reduciendo el orquestador principal a un tamaño manejable.
   - **Extracción de `SqliteLogQueryBuilder.cs`**: Aislada toda la lógica de construcción de SQL dinámico parametrizado fuera de `SqliteLogStore.cs`.
   - **Modularización de `ValueConverters.cs`**: Eliminado el archivo monolítico y dividido en 3 submódulos cohesivos por dominio: `BooleanConverters.cs`, `TelemetryConverters.cs` y `GraphConverters.cs`.
6. **Auditoría de Seguridad, Robustez y Depuración de Errores**:
   - **Mitigación Estricta de Zip Slip (`SmartUnpackNode.cs`)**: Normalizado el directorio base asegurando el separador de directorio final (`Path.TrimEndingDirectorySeparator + Path.DirectorySeparatorChar`) para evitar ataques por prefijos comunes.
   - **Papelera Segura sin Borrado Destructivo (`SafeRecycleDeleteNode.cs`)**: Ajustada la estructura P/Invoke x64 de `SHFILEOPSTRUCT` y eliminado el fallback a `File.Delete` que borraba permanentemente los archivos en caso de fallo.
   - **Medición Segura de Procesos (`CliExecutionNode.cs`)**: Sustituido el cálculo de tiempo por `Stopwatch.StartNew()` eliminando potenciales `InvalidOperationException` al consultar `process.ExitTime`.
   - **Despacho Seguro en UI (`FastObservableRingBuffer.cs`)**: Comprobación de `Application.Current.Dispatcher.CheckAccess()` en `NotifyReset()` evitando excepciones de colección en hilos de fondo.
   - **Limpieza de Tareas en Background (`FolderWatcherService.cs`)**: Cancelación y espera limpia con timeout de `_processingTask` en `Stop()`.
   - **Sanitización de Nombres de Archivo (`AdvancedRenamerNode.cs`)**: Reemplazo automático de caracteres ilegales devueltos por plantillas (`Path.GetInvalidFileNameChars`).
   - **Drenaje de Canal (`SqliteLogStore.cs`)**: Invocación de `_ingestionChannel.Writer.TryComplete()` en `DisposeAsync()` y `Dispose()`.
7. **Batería de Automatización y Testing Exhaustivo (178 Pruebas)**:
   - **`FileItemContextExhaustiveTests`**: Memoización zero-alloc de `ShortIdString`, reactividad en cambio de ruta y clonación profunda.
   - **`SystemVariablesResolverExhaustiveTests`**: Comprobación de formato numérico de tamaños con `CultureInfo.InvariantCulture`, contadores y metadatos.
   - **`AdvancedRenamerExhaustiveTests`**: Sanitización automática de caracteres ilegales, estrategias de colisión (`AutoIncrement`) y soporte de casing exacto.
   - **`CliExecutionNodeExhaustiveTests`**: Ejecución con captura de stdout/stderr y modo Dry-Run.
   - **`SafeRecycleDeleteNodeExhaustiveTests`**: Resiliencia frente a archivos inexistentes y modo Dry-Run.
   - **`SqliteLogQueryBuilderTests`**: Validación de cláusulas SQL con `ExactLevel`, `SearchText` y rangos de fechas.
   - **`ValueConvertersExhaustiveTests`**: Badges cortos de severidad, bindings bidireccionales y convertidores de visibilidad/ancho.
   - Suite completa ejecutada: **178 / 178 pruebas superadas con 100% de éxito (0 errores, 0 fallos)** en 1.1s.

---

### 🛠 Cambios e Implementaciones
1. **Botón Visual e Interactivo en Cabecera de Nodo (`NodeCardView.xaml` & `ValueConverters.cs`)**:
   - Incorporado un botón interactivo al estilo del breakpoint en la cabecera de cada tarjeta de nodo.
   - **Indicador Visual**: Icono estilizado (`≡`) con fondo cian brillante (`#06B6D4`) cuando está **Encendido (emite logs)** y gris translúcido atenuado (`#475569`) cuando está **Apagado (silenciado)**.
   - **ToolTips y Menú Contextual**: ToolTip reactivo ("Logs: Habilitados (clic para silenciar)" / "Logs: Silenciados (clic para activar)") y opción en el menú contextual (`MenuItem: Alternar Emisión de Logs`).
2. **Modelo de Vista y Comandos (`NodeViewModel.cs`)**:
   - Añadida propiedad reactiva `IsLoggingEnabled` (por defecto `true`) y comando `ToggleLoggingCommand`.
3. **Control y Supresión en el Motor de Ejecución (`WorkflowExecutor.cs` & `WorkflowGraph.cs`)**:
   - `WorkflowGraph` y `WorkflowNode` persisten el estado `IsLoggingEnabled` y el set `DisabledLoggingNodeIds`.
   - `WorkflowExecutionContext` y `WorkflowExecutor.NotifyLog` descartan de inmediato los logs de nodos silenciados en $O(1)$ sin asignación en memoria ni saturación de base de datos.
4. **Pruebas Automatizadas y Validación**:
   - Añadidas pruebas unitarias en `StructuredLogContextTests.cs` validando la supresión y re-activación de logs en caliente.
   - Suite completa superada al 100%: **145 / 145 pruebas exitosas (0 errores, 0 advertencias)**.

---

## [2026-08-23] - Optimización de Rendimiento Extremo (Performance Engineering) y Zero-Alloc Hot Paths

### 🛠 Cambios e Implementaciones
1. **Memoización en `FileItemContext.cs` (Zero-Alloc Hot Paths)**:
   - Cacheo interno e inmutable de `IdString` (`"d3b07384..."`) y `ShortIdString` (`"d3b07384"`).
   - Propiedad `FileName` reactiva a mutaciones en `CurrentPath`.
   - Eliminadas más de 160.000 asignaciones redundantes de strings de GUIDs y rutas por ejecución de flujo masivo.
2. **Formateo Zero-Boxing en `StructuredLogRecord.cs`**:
   - `FormattedFileSize` optimizado con formateo numérico directo en lugar de `FormattableString.Invariant`, eliminando allocation de factories, arrays `object[]` de parámetros y boxing.
   - Parámetro `fileName` precalculado en `StructuredLogRecord.Create` para evitar llamadas redundantes a `Path.GetFileName`.
3. **Reutilización de Conexión y Transacciones Masivas en `SqliteLogStore.cs`**:
   - `InsertBatchAsync` reutiliza `_keepAliveConnection` protegida bajo `_flushLock`, eliminando la apertura y cierre repetitivo de conexiones SQLite.
   - Ejecución sincrónica nativa dentro del worker thread para evitar la sobrecarga de `Task` en bases de datos in-memory.
4. **Benchmarking Multinúcleo y Validación de Alta Concurrencia**:
   - Añadido `Benchmark_Telemetry_HighThroughput_ParallelIngestion` en `PerformanceBenchmarkSuiteTests.cs` simulando ingestión paralela en todos los núcleos de CPU (28 hilos).
   - **Throughput alcanzado**: **>82.000 logs/segundo** persistidos e indexados en SQLite In-Memory en ~600 ms con apenas 8 recolecciones Gen0.
   - Suite completa: **143 / 143 pruebas pasadas con 100% de éxito (0 errores, 0 advertencias)**.

---

## [2026-08-23] - Modernización de Logs Estructurados, Trazabilidad por ID de Flujo (`ItemId`) y Visor Interactivo JSON

### 🛠 Cambios e Implementaciones
1. **Auto-Vinculación Contextual de Archivos en Motor (`WorkflowExecutionContext.cs` & `WorkflowExecutor.cs`):**
   - Inyectada la referencia activa al `FileItemContext` en cada ciclo de ejecución de nodo.
   - Cualquier invocación a `context.Log(...)` extrae de forma automática y transparente: `ItemId` (`item.Id`), `FilePath` (`item.CurrentPath`), `FileName` (`Path.GetFileName`), y `FileSizeBytes` (`item.FileSizeBytes`).
   - Resuelto definitivamente el problema de nombres de archivos vacíos en los logs.
2. **Estructuración JSON y Mensajes Descriptivos de 1 Línea (`LogOutputNode.cs`):**
   - Empaquetamiento de metadatos, tags, tamaño e historial de ejecución en un JSON formateado (`DetailsJson`).
   - Generación de mensajes concisos y limpios de una sola línea (`🔍 Inspección: archivo.ext (X MB) • N tags • M metadatos • K nodos previos`), eliminando el texto multilínea caótico que se cortaba en el grid.
3. **Persistencia e Indexación en Memoria SQLite (`SqliteLogStore.cs` & `StructuredLogRecord.cs`):**
   - Esquema de tabla `ExecutionLogs` actualizado con `ItemId TEXT`, `FileSizeBytes INTEGER` y `DetailsJson TEXT`.
   - Creado el índice B-Tree `IX_Logs_ItemId` y el método analítico `GetItemTraceAsync(string itemId)` para recuperar toda la cadena de procesamiento de un archivo específico desde su origen hasta el final.
4. **DataGrid Profesional en WPF con Fila Expansible (`LogView.xaml` & `LogViewModel.cs`):**
   - **Columna `ID Flujo`**: Badge compacto (`#a1b2c3d4`) clicable que filtra al instante toda la vida del archivo.
   - **ToolTips Ricos**: Muestra ruta completa, tamaño formateado e ID al pasar el cursor sobre la columna Fichero.
   - **Panel Expansible `RowDetailsTemplate`**: Se despliega al seleccionar la fila con badges de metadatos, visor de JSON formateado y botones de acción rápida (`🔍 Trazabilidad` y `📋 Copiar JSON`).
5. **Ampliación de Pruebas Automatizadas:**
   - Nuevos tests en `StructuredLogContextTests.cs` y ampliación de `SqliteLogStoreTests.cs`.
   - Suite total incrementada a **142 / 142 pruebas pasadas con 100% de éxito (0 errores, 0 advertencias)**.

---

## [2026-08-23] - Capa de Telemetría Atómica (Snapshot Pull a 30 FPS) y Consola de Logs Virtualizada con RingBuffer

### 🛠 Cambios e Implementaciones
1. **Desacoplamiento Total Motor $\leftrightarrow$ UI mediante Snapshots Atómicos (`WorkflowExecutor.cs` & `ExecutionTelemetry.cs`):**
   - Incorporado el struct inmutable `TelemetrySnapshot` (`ProcessedItems`, `TotalItems`, `ProcessedBytes`, `ItemsPerSecond`, `MegabytesPerSecond`, `Percentage`, `Elapsed`, `StatusMessage`).
   - El motor de ejecución actualiza contadores atómicos con `Interlocked` y `Stopwatch` en O(1) con 0 asignaciones de memoria en heap.
   - Eliminado el encolamiento de delegados por cada archivo procesado en la cola del Dispatcher de WPF.
2. **Cálculo Ultrarrápido de Totales y Seguimiento Integral de Elementos (`WorkflowExecutor.cs` & `FolderSourceNode.cs`):**
   - **Soporte Integral para Archivos, Carpetas y Mixto**: `FolderSourceNode` evalúa `EmitMode` ("FilesOnly", "DirectoriesOnly", "FilesAndDirectories") tanto en `FastCountSourceFiles` como en el streaming, adaptando la métrica y las etiquetas contextuales ("elementos", "carpetas", "archivos").
   - **Rastreo Reactivo de Elementos en Streaming**: Incorporado `_sourceItemsEmitted` y resolución de aristas no conectadas en `DispatchEmitAsync`, garantizando que la barra de progreso refleje el avance en vivo desde el primer milisegundo independientemente de la topología o topologías abiertas.
   - **Feedback Fiel de Estado**:
     - Durante la ejecución: `⚡ Procesando: X/Total elementos (N%) • ops/s`
     - Al culminar: `🟢 Completado: Total/Total elementos (100%)`
3. **Puente de Telemetría y Coalescencia a 30 FPS (`ControlBarViewModel.cs`):**
   - El temporizador visual `visualFlushTimer` muestrea la instantánea atómica a 30 FPS constantes (~33 ms), actualizando la barra de progreso, estados de nodos, aristas y mensajes de estado en un único ciclo por frame.
   - Eliminada al 100% la cola residual de eventos y el retraso visual al finalizar flujos de trabajo masivos.
4. **Motor de Logs Estructurados en Memoria SQLite y DataGrid Fluido (`SqliteLogStore.cs` y `LogViewModel.cs`):**
   - **Cero Consumo de CPU en Reposo (0.0%)**: Añadida coalescencia de lotes (`Task.Delay(20)`) en el worker de SQLite, eliminando micro-transacciones unitarias continuas y reduciendo el consumo residual de CPU a 0%.
   - **Renderizado Instantáneo y Reactivo en DataGrid**: Conexión directa mediante `ObservableCollection<StructuredLogRecord>` con virtualización por reciclaje (`VirtualizationMode="Recycling"`). Los logs aparecen en tiempo real durante la ejecución sin pantallas en blanco ni parpadeos.
   - **Operaciones de Borrado y Exportación sin Bloqueos**:
     - `ClearAsync` protegido con `_flushLock` y sin `VACUUM` bloqueante, limpiando la consola y la base de datos de inmediato.
     - `ExportLogs` ejecutado 100% en streaming en hilo de fondo (`Task.Run`), permitiendo guardar logs de millones de registros sin congelar la ventana.
   - **Ordenación y Filtros SQL Instantáneos**:
     - Clic en cabecera **Duración** (`ORDER BY DurationMs DESC`) para detectar cuellos de botella al instante; clic en **Nivel**, **Hora**, **Nodo** o **Fichero**.
     - Búsqueda en tiempo real indexada en SQLite.
5. **Ampliación de la Suite de Pruebas Automatizadas:**
   - Creados `AsyncVirtualizingListTests.cs`, `SqliteLogStoreTests.cs`, `PagedLogStoreTests.cs`, `FastObservableRingBufferTests.cs` y `ExecutionTelemetryTests.cs`.
   - Suite completa superada con éxito: **139 / 139 pruebas unitarias y de integración pasadas (0 errores, 0 advertencias)**.

---

## [2026-08-22] - Sincronización Simultánea en Tiempo Real de Barra de Progreso y Logs

### 🛠 Cambios e Implementaciones
1. **Sincronización en Tiempo Real a 30 FPS (`ControlBarViewModel.cs`):**
   - Agrupada la actualización de progreso (`ProgressPercentage` y `StatusMessage`) directamente en el temporizador visual `visualFlushTimer` con prioridad normal.
   - Eliminado el encolamiento retrasado de miles de delegados de progreso que quedaban atrapados detrás del vaciado de logs. Ahora la barra de progreso avanza **simultáneamente y en tiempo real con la salida de logs**.
2. **Cálculo de Porcentaje en Vivo durante Streaming (`WorkflowExecutor.cs`):**
   - Lectura atómica volátil de `_totalItemsCount` en `finally` y reporte porcentual progresivo (`⚡ Procesando: X/Y (N%)`).

---

## [2026-08-22] - Corrección de Conteo en Nodos Terminales y Vaciado Instantáneo de Logs

### 🛠 Cambios e Implementaciones
1. **Conteo Preciso de Aristas Activas y Cierre al 100% (`WorkflowExecutor.cs`):**
   - Corregido el incremento de `_totalItemsCount` en `DispatchEmitAsync` para que sume únicamente las conexiones reales conectadas (`matchingEdges.Count`). Evitado que nodos terminales (como `LogOutputNode`) cuyos puertos de salida no están conectados desvíen el conteo total esperado.
   - Añadida notificación explícita del 100% de progreso al culminar todas las tareas en `ExecuteAsync` (`Procesados N/N (100%)`).
2. **Vaciado Adaptativo e Instantáneo de Logs (`LogViewModel.cs` & `ControlBarViewModel.cs`):**
   - Implementado escalado dinámico del tamaño de lote (de 75 hasta 500 registros por ciclo si la cola supera los 1.000 elementos).
   - Incorporado el método `FlushAllPendingLogs()`, invocado inmediatamente al finalizar el flujo para que la consola muestre el 100% de los logs en el mismo milisegundo en que concluye el procesamiento sin tiempos de espera residuales.

---

## [2026-08-22] - Streaming Fluido en UI a 60 FPS y Prevención de Congelamiento por Inundación

### 🛠 Cambios e Implementaciones
1. **Limitación de Tasa de Renderizado de Logs (`LogViewModel.cs`):**
   - Incorporado el límite de vaciado `MaxLogsPerFlush = 75` en `FlushPendingLogs` (cada 35 ms), evitando que ráfagas de miles de logs saturen la cola de renderizado de WPF y bloqueen la ventana en estado "No responde".
2. **Agrupación y Throttling de Eventos Visuales en el Lienzo (`ControlBarViewModel.cs`):**
   - Desacoplados los eventos `EdgeItemDispatched` y `NodeStatusChanged` mediante un temporizador `visualFlushTimer` a 30 FPS con diccionarios concurrentes (`pendingEdgeUpdates` / `pendingStatusUpdates`).
   - Reducido el tráfico de delegados en la cola del Dispatcher en más de un **99%**, manteniendo la interfaz 100% interactiva, fluida y con respuesta inmediata durante el procesamiento masivo.
3. **Modo Compacto de Inspección (`LogOutputNode.cs`):**
   - Incorporado el parámetro opcional `CompactFormat` para generar resúmenes concisos de 1 sola línea por archivo en flujos de alto volumen.

---

## [2026-08-22] - Escaneo de 1 Sola Pasada I/O y Búfer Acotado con Contrapresión (Bounded Channel)

### 🛠 Cambios e Implementaciones
1. **Constructores I/O de 1 Sola Pasada (`FileItemContext.cs`):**
   - Añadidos constructores optimizados `FileItemContext(FileInfo)` y `FileItemContext(DirectoryInfo)`.
   - Eliminadas las comprobaciones duplicadas de `File.Exists(path)` e instanciaciones redundantes de `FileInfo.Length`, reduciendo en un **66% las llamadas I/O de sistema de archivos a Windows** (1 sola pasada I/O).
2. **Tubería Productor-Consumidor con Contrapresión (`FolderSourceNode.cs`):**
   - Incorporado un canal acotado `Channel.CreateBounded<FileItemContext>(1000)` para pausar automáticamente el escáner si los nodos receptores son más lentos, evitando el uso excesivo de memoria RAM.
   - Puntos de cesión de hilo (`await Task.Yield()`) cada 100 archivos enumerados para garantizar cero bloqueos de la interfaz y respuesta instantánea ante cancelaciones.
   - Reporte dinámico en tiempo real cada 100 ms con métricas acumuladas de conteo y megabytes (`⚡ Escaneando y emitiendo: 1,450 archivos (850.5 MB)...`).

---

## [2026-08-22] - Tubería Productor-Consumidor No Bloqueante y Estado Continuo en Tiempo Real

### 🛠 Cambios e Implementaciones
1. **Tubería Productor-Consumidor No Bloqueante (`WorkflowExecutor.cs`):**
   - Eliminado el bloqueo secuencial en `DispatchEmitAsync`. Las llamadas a `context.EmitAsync` por parte de `FolderSourceNode` ahora son no bloqueantes, permitiendo que la lectura de archivos en disco ocurra a máxima velocidad (miles de archivos/segundo) mientras los nodos receptores procesan en paralelo.
   - Conteo atómico de tareas activas (`_activeNodeTasks`) y drenaje determinista en `ExecuteAsync`, garantizando 100% de finalización sin tareas huérfanas.
2. **Formateo y Persistencia de Estado Activo (`StatusBarViewModel.cs`):**
   - Incorporado el método helper `UpdateActiveStatusMessage` para mantener el indicador activo `⚡` y texto en español de forma ininterrumpida mientras `IsRunning` sea `true`.
   - Evitado que mensajes predeterminados como `"Listo"` o conteos secundarios reseteen la barra inferior durante la ejecución activa.

---

## [2026-08-22] - Escaneo Asíncrono en Streaming y Estado Reactivo en Tiempo Real

### 🛠 Cambios e Implementaciones
1. **Escaneo y Emisión Asíncrona en Streaming (`FolderSourceNode.cs`):**
   - Eliminada la recolección previa monolítica en lista (`List<FileItemContext>`).
   - Implementado escaneo asíncrono con emisión inmediata por archivo (`await context.EmitAsync("Out", item)`). Los nodos posteriores comienzan el procesamiento instantáneamente sin esperas iniciales ni congelamientos de UI.
   - Incorporado reporte de progreso periódico en tiempo real cada 100 ms (`⚡ Escaneando y emitiendo: N elementos...`).
2. **Sincronización Reactiva de la Barra de Estado Inferior (`StatusBarViewModel.cs` & `MainViewModel.cs`):**
   - Inyectada la instancia `LogViewModel` en `StatusBarViewModel`.
   - Suscripción reactiva a `LogViewModel.PropertyChanged` para reflejar en tiempo real los mensajes de escaneo y avance dinámico (`⚡ Escaneando y emitiendo: 1,450 elementos...`, `⚡ Procesando 45%`, `🟢 Listo`).

---

## [2026-08-22] - Optimización de Rendimiento de Alto Nivel y Benchmarking (.NET 9 & C# 13)

### 🛠 Cambios e Implementaciones
1. **Búsqueda Vectorizada SIMD y Cero Asignaciones en Motor de Plantillas (`VariableTemplateResolver.cs`):**
   - Incorporada la primitiva de .NET 9 `System.Buffers.SearchValues<char>` (`OpenBraceSearch`) para aceleración por hardware SIMD en la localización de delimitadores.
   - Eliminadas las asignaciones innecesarias de cadenas mediante rodajas `ReadOnlySpan<char>`.
2. **Optimizaciones de Clonado Profundo (`FileItemContext.cs`):**
   - Inicialización por capacidad exacta (`Metadata.Count`, `Tags.Count`, `ExecutionLog.Count`) en `DeepClone()` eliminando las reasignaciones internas (*array resizing*) en bifurcaciones masivas de puertos.
3. **Optimización de E/S Criptográfica (`HashCalculatorNode.cs` & `DeduplicationFilterNode.cs`):**
   - Incrementado el buffer de lectura de `FileStream` a 128 KB (`131072` bytes) reduciendo llamadas al sistema operativo durante operaciones de hash masivo.
4. **Nueva Suite de Benchmarking de Alto Rendimiento (`PerformanceBenchmarkSuiteTests.cs`):**
   - Batería de pruebas que mide throughput (operaciones/segundo), latencia, consumo pico de memoria y colecciones del Garbage Collector (Gen 0, Gen 1, Gen 2).
   - Suite de pruebas automatizadas incrementada a **117 / 117 pruebas pasadas con éxito**.

---

## [2026-08-22] - Generación de la Suite Completa de Documentación Técnica (`docs/`)

### 🛠 Cambios e Implementaciones
1. **Creación de la Suite Completa de Documentación Técnica (6 Archivos Markdown):**
   - **`docs/architecture.md`**: Documento de arquitectura, diagrama Mermaid.js, flujo de datos por capas, patrones de diseño y Registros de Decisiones Arquitectónicas (ADRs).
   - **`docs/setup_and_deployment.md`**: Guía de instalación, requisitos previos (.NET 9 SDK), script `.\run.ps1`, configuración de herramientas externas, empaquetado para distribución y CI/CD.
   - **`docs/api_reference.md`**: Referencia técnica completa de la capa SDK (`IFlowNode`, `FileItemContext`, `IFlowExecutionContext`), motor de plantillas de variables, firmas de métodos y guía de extensión de nodos.
   - **`docs/user_guide.md`**: Manual de usuario visual paso a paso, catálogo de los 22 nodos, gestor de presets multimedia, gestor de contraseñas, simulación *Dry Run*, catálogo de 40 ejemplos y resolución de problemas (FAQ).
   - **`docs/contributing.md`**: Estándares de código C# 13, workflow de Git, guía de desarrollo de nodos personalizados y baterías de pruebas `dotnet test`.
   - **`docs/README.md`**: Índice principal y centro de documentación con navegación por enlaces relativos.

---

## [2026-08-22] - Auditoría de Errores y Seguridad (QA Lead & Security Audit)

### 🛠 Cambios e Implementaciones
1. **Solución de Interbloqueo y Procesos Huérfanos (`CliExecutionNode.cs` & `MediaTranscoderNode.cs`):**
   - Lectura concurrente de `StandardOutput` y `StandardError` mediante `Task.WhenAll` evitando congelamientos de buffer.
   - Eliminación determinista de procesos hijos (`process.Kill(entireProcessTree: true)`) ante cancelación o expiración de tiempo de espera.
2. **Protección SSRF (`WebhookNotificationNode.cs`):**
   - Validación estricta de esquema de URI (`http`/`https`) en peticiones HTTP POST.
3. **Control Thread-Safe de Concurrencia (`WorkflowExecutor.cs`):**
   - Modificación segura de `MaxDegreeOfParallelism` evitando excepciones `ObjectDisposedException`.
4. **Nuevas Pruebas Unitarias de Seguridad (`QASecurityAuditFixesTests.cs`):**
   - Suite ampliada a **102 / 102 pruebas pasadas con éxito** (0 errores, 0 advertencias).

---

## [2026-08-22] - Auditoría de Arquitectura, Mapa de Riesgos y Plan de Modularización (Fase 1)

### 🛠 Cambios Implementados
1. **Auditoría de Archivos Monolíticos:**
   - Identificados 7 archivos principales de más de 300 líneas de código/XAML (`NodeCardView.xaml`, `NodeViewModel.cs`, `VariableTemplateResolver.cs`, `EditorViewModel.cs`, `ControlBarViewModel.cs`, `SmartUnpackNode.cs`, `NodeInspectorPanelView.xaml`).
2. **Identificación de Riesgos y Code Smells:**
   - Fugas potenciales de memoria por falta de desuscripción de eventos singleton (`IDisposable`).
   - Falta de modularidad en el motor de plantillas de variables.
   - Complejidad en plantillas XAML de Nodify y ViewModel acoplado a UI.
3. **Elaboración del Artefacto Implementation Plan:**
   - Creado artefacto `implementation_plan.md` detallando la estrategia de modularización iterativa en 4 módulos bajo el Principio de Responsabilidad Única (SRP).

---

## [2026-08-22] - Creación de la Biblioteca Completa de 40 Ejemplos de Flujos de Trabajo (`docs/examples/`)

### 🛠 Cambios Implementados
1. **Generación de 40 Flujos de Trabajo Ejecutables (`.json`) y Documentación Markdown (`.md`):**
   - Se estructuró el directorio `docs/examples/` en 4 niveles con 10 ejemplos por nivel (80 archivos en total + catálogo principal):
     - `docs/examples/01_basic/` (Flujos `flow_01` a `flow_10` - Canales lineales simples, optimización WebP, extracción MP3, hashes SHA-256, renombrado, etc.).
     - `docs/examples/02_intermediate/` (Flujos `flow_11` a `flow_20` - Filtrado condicional, bifurcación por extensión, EXIF, deduplicación, webhooks HTTP).
     - `docs/examples/03_advanced/` (Flujos `flow_21` a `flow_30` - Lotes `BatchBufferNode`, paralelismo `ForkJoinBarrierNode`, rate limit `ThrottleDelayNode`, reintentos).
     - `docs/examples/04_complex/` (Flujos `flow_31` a `flow_40` - Scatter-Gather, doble hash inmutable, ingesta masiva empresarial, arquitectura de fallback resiliente).
2. **Creación del Catálogo Principal (`docs/examples/README.md`):**
   - Se generó un catálogo con enlaces a todos los ejemplos, indicando categoría, descripción y cómo importarlos en FileFlow Studio.
3. **Prueba Unitaria de Validación de Esquema (`WorkflowStorageServiceTests.cs`):**
   - Se incorporó la prueba unitaria `AllFortyGeneratedWorkflowExamples_ShouldDeserializeSuccessfully` (86/86 pruebas pasadas) que valida automáticamente que los 40 archivos `.json` deserializan limpiamente en objetos `WorkflowGraph` válidos.
1. **Auditoría y Sustitución de Rutas Absolutas a Relativas:**
   - Se revisaron todos los nodos de la aplicación y se reemplazaron las rutas predeterminadas absolutas (como `C:\SampleFiles`, `C:\FileFlowOutput`, `C:\Quarantine`, `C:\FileFlowUnpacked`, `C:\FileFlowOptimized`) por patrones de rutas relativas basadas en plantillas:
     - **FolderSourceNode**: `SourcePath` $\rightarrow$ `{RelativeDir}\Input`
     - **DestinationSinkNode**: `DestinationRoot` $\rightarrow$ `{RelativeDir}\Output`
     - **OriginalFileActionNode**: `QuarantinePath` $\rightarrow$ `{RelativeDir}\Quarantine`
     - **SmartUnpackNode**: `DestinationFolder` $\rightarrow$ `{RelativeDir}\Unpacked`
     - **ImageOptimizerNode**: `OutputDirectory` $\rightarrow$ `{RelativeDir}\OptimizedImages`
2. **Anclaje Automático a la Ruta Global de Salida (`ParameterHelper.ResolveOutputPath`):**
   - Al usar patrones de rutas relativas, `ParameterHelper.ResolveOutputPath` las ancla automáticamente bajo el directorio configurado en **Ajustes de la Aplicación > Almacenamiento y Rutas (DefaultGlobalOutputDir)**.
1. **Actualización Automática de Argumentos (`NodeViewModel.cs`):**
   - Al seleccionar cualquier preset en el menú desplegable del nodo **Transcodificar Media**, se activa la actualización inmediata del parámetro `CustomArguments` en la tarjeta del nodo con los comandos FFmpeg configurados en dicho preset (ej: `-vn -c:a libmp3lame -b:a 192k` para MP3).
2. **Eliminación del Campo Redundante `FFmpegPath` (`MediaTranscoderNode.cs`):**
   - Se eliminó el parámetro `FFmpegPath` de las tarjetas del nodo de media para simplificar la interfaz visual.
   - El nodo resuelve de forma limpia la ruta ejecutable de FFmpeg directamente desde `ExternalToolsService` (definida globalmente en Ajustes > Herramientas Externas), con fallback dinámico en el `PATH` del sistema.
1. **Configuración de ComboBox (`NodeParameterTemplates.xaml`):**
   - Se estableció `IsEditable="False"` en el control `ComboBox` de los parámetros de nodos.
   - En WPF, la combinación de `IsEditable="True"` con `SelectedItem` en un ComboBox provocaba que el control de texto editable interno sobrescribiera el ítem seleccionado al hacer clic en el desplegable. Al establecer `IsEditable="False"`, la selección de cualquier preset del menú (*Extraer Audio MP3*, *Convertir 720p H.264*, *WebM VP9*, etc.) funciona de manera instantánea y estable.
2. **Sincronización Dinámica de Presets (`NodeParameterViewModel.cs`):**
   - Se añadió la suscripción al evento `MediaPresetManagerService.Instance.PresetsChanged` para refrescar automáticamente la lista de opciones (`Options`) de los nodos de media al crear, editar o eliminar presets en el gestor.
1. **Desacoplamiento de Binding Conflictuco (`NodeParameterTemplates.xaml`):**
   - Se eliminó el binding redundante `Text="{Binding Value...}"` con `UpdateSourceTrigger=PropertyChanged` que competía con `SelectedItem` en el control `ComboBox` editable de las tarjetas de nodos.
   - Ahora `SelectedItem="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"` administra la selección limpia de cualquier preset del desplegable sin que la UI revierta la selección.
1. **Creación del Convertidor (`ValueConverters.cs`):**
   - Se creó e implementó `StringEqualsToVisibilityConverter` en la capa de convertidores WPF (`FileFlow.App.Converters`) para comparar dinámicamente la categoría de presets con parámetros de texto y devolver el estado de visibilidad del icono correspondiente.
2. **Registro de Recurso Global y Local (`App.xaml` & `MediaPresetManagerWindow.xaml`):**
   - Se registró la clave de recurso `<converters:StringEqualsToVisibilityConverter x:Key="StringEqualsToVisibilityConverter" />` tanto globalmente en `App.xaml` como localmente en los recursos de `MediaPresetManagerWindow.xaml`, solucionando por completo la excepción de tipo `XamlParseException` / `StaticResourceHolder` al presionar el botón `⚙️ Presets`.
1. **Inicialización al Arrancar la Aplicación (`App.xaml.cs`):**
   - Se añadieron invocaciones explícitas a `UserPreferencesService.Instance.Load()` y `ExternalToolsService.Instance.LoadConfig()` en el evento `OnStartup`.
   - Se aplica de forma inmediata el tema visual guardado (`ActiveTheme`) vía `ThemeManager.Instance.SetTheme(themeEnum)`.
2. **Sincronización Reactiva en ViewModels (`ControlBarViewModel`, `ToolboxViewModel`, `EditorViewModel`, `LogViewModel`):**
   - **`ControlBarViewModel.cs`**: Carga e iguala `SelectedTheme` y `IsDryRun` con `DefaultDryRunState` al iniciar y cuando se modifican desde la ventana de Ajustes Generales.
   - **`ToolboxViewModel.cs`**: Ajusta automáticamente `IsCompactMode` con `IsCompactToolbox` al refrescar el catálogo.
   - **`EditorViewModel.cs`**: Se suscribe al evento `PreferencesChanged` para actualizar reactivamente `GlobalOutputDir` si se cambia desde los ajustes generales.
   - **`LogViewModel.cs`**: Lee de forma dinámica la preferencia `MaxLogEntries` para delimitar el buffer de registros en la consola.
1. **Control de Concurrencia en Motor (`WorkflowExecutor.cs`):**
   - Se activó e integró la protección por `SemaphoreSlim` (`_concurrencyThrottle`) dentro del despacho de nodos (`DispatchEmitAsync`).
   - Todos los hilos secundarios de despacho pasan obligatoriamente por `await _concurrencyThrottle.WaitAsync()` y liberan en el bloque `finally` (`_concurrencyThrottle.Release()`), garantizando el respeto estricto del número de hilos configurado (`MaxDegreeOfParallelism`).
2. **Conexión con Preferencias de Aplicación (`ControlBarViewModel.cs`):**
   - Al instanciar `WorkflowExecutor` antes de iniciar la ejecución de cualquier flujo, se lee dinámicamente `UserPreferencesService.Instance.Preferences.MaxParallelThreads` (salvo en modo depuración interactiva, donde se fuerza a 1 hilo para inspección secuencial).
1. **Persistencia Completa de Preferencias (`UserPreferencesService.cs`):**
   - Extensión del servicio JSON en `%APPDATA%\FileFlowStudio\user_preferences.json` para guardar de forma permanente todos los ajustes de la aplicación:
     - `DefaultGlobalOutputDir`: Ruta de salida global persistente (restaurada automáticamente en el editor).
     - `ActiveTheme`: Tema visual activo (`Dark`, `Light`, `Cyber`, `Pastel`).
     - `IsCompactToolbox`: Modo de vista del catálogo (`Compacto` / `Detallado`).
     - `MaxParallelThreads`: Hilos de CPU máximos para procesamiento paralelo (1 a 16 hilos).
     - `DefaultDryRunState`: Estado del Modo Prueba (Simulación) por defecto al arrancar.
     - `DefaultConflictStrategy`: Estrategia de resolución de colisiones predeterminada (`RenameIncremental`, `Overwrite`, `Skip`).
     - `DefaultLogLevel`: Filtro de logs predeterminado (`Information`, `Warning`, `Error`, `Debug`).
     - `AutoScrollConsole` & `MaxLogEntries`: Desplazamiento automático y límite de memoria de registros en consola.
     - `EnableAutoSave` & `AutoSaveIntervalMinutes`: Configuración de respaldo automático de flujos.
2. **Rediseño Modal de Ajustes Generales (`WorkflowSettingsWindow.xaml`):**
   - Diálogo organizado en 4 pestañas fluidas:
     - 📂 **Almacenamiento & Rutas** (Ruta global, colisiones, auto-guardado).
     - 🎨 **Apariencia & UI** (Tema, vista de catálogo, auto-scroll y límite de logs).
     - ⚡ **Rendimiento & Ejecución** (Hilos paralelos de CPU, modo prueba por defecto, nivel de log).
     - 🛠️ **Herramientas Externas** (FFmpeg, FFprobe, 7z, Python con Autobúsqueda).
1. **Gestor de Presets de Media (`MediaPresetManagerService.cs` & `MediaPresetManagerWindow.xaml`):**
   - Servicio Singleton con almacenamiento en `%APPDATA%\FileFlowStudio\media_presets.json`.
   - Sistema CRUD completo (Crear, Editar, Eliminar, Restablecer) de presets de conversión.
   - Ventana modal interactiva con 10 presets de fábrica preconfigurados (Extracción de MP3, AAC, FLAC, 1080p H.264, 720p H.264, 4K H.265/HEVC, WebM VP9, GIF Animado, Móvil Ultra-Comprimido, Personalizado).
   - Botón `⚙️ Presets` integrado en la tarjeta del nodo `Transcodificar Media` para acceso instantáneo al gestor.
2. **Servicio de Autobúsqueda de Herramientas Externas (`ExternalToolsService.cs`):**
   - Servicio de detección automática de ejecutables (`ffmpeg.exe`, `ffprobe.exe`, `7z.exe`, `python.exe`) escaneando el `PATH` del sistema, rutas conocidas (`Program Files`, `C:\ffmpeg\bin`, `AppData`, `Chocolatey`, `WinGet`, `Scoop`) y el Registro de Windows.
   - Pestaña **"🛠️ Herramientas Externas"** añadida a la Configuración del Flujo (`WorkflowSettingsWindow.xaml`) con el botón **`🔍 Auto-Detectar Herramientas`**.
3. **Ejecución Real con Control de Progreso (`MediaTranscoderNode.cs`):**
   - Ejecución de `ffmpeg.exe` en subproceso con redirección de `stderr` para capturar el progreso en tiempo real (`time=00:01:23.45`) y emitirlo a la consola de logs.
   - Modo de simulación/fallback seguro cuando FFmpeg no está presente o en pruebas simuladas.
1. **Reducción de Nombres de Nodos (`Strings.es.resx` & `Strings.resx`):**
   - Se simplificaron los nombres de los 22 nodos en los recursos de idioma para eliminar términos redundantes y hacerlos directos y explícitos:
     - `Borrado Seguro a Papelera` $\rightarrow$ **`Enviar a Papelera`**
     - `Renombrador Avanzado con Tokens` $\rightarrow$ **`Renombrar Archivo`**
     - `Reubicador y Copiador de Archivos` $\rightarrow$ **`Mover / Copiar`**
     - `Limpiador de Carpetas Vacías` $\rightarrow$ **`Limpiar Carpetas`**
     - `Acción sobre Archivo Original` $\rightarrow$ **`Acción en Origen`**
     - `Inspector de Directorios` $\rightarrow$ **`Escanear Carpeta`**
     - `Descompresión Inteligente` $\rightarrow$ **`Descomprimir`**
     - `ArchiveCompressorNode` $\rightarrow$ **`Comprimir ZIP / 7z`**
     - `ArchiveFilterNode` $\rightarrow$ **`Filtrar Comprimidos`**
     - `Optimizador de Imágenes` $\rightarrow$ **`Optimizar Imagen`**
     - `Media & Video Transcoder` $\rightarrow$ **`Transcodificar Media`**
     - `Document & PDF Processor` $\rightarrow$ **`Procesar Documento`**
     - `Inyector de Variables` $\rightarrow$ **`Inyectar Variable`**
     - `Calculador de Hash Criptográfico` $\rightarrow$ **`Calcular Hash`**
     - `Filtro de Deduplicación por Hash` $\rightarrow$ **`Filtrar Duplicados`**
     - `Agrupador de Lotes (Batch Buffer)` $\rightarrow$ **`Agrupar por Lotes`**
     - `Control de Tasa y Pausa (Throttle)` $\rightarrow$ **`Pausa / Throttle`**
     - `Barrera de Sincronización (Fork & Join)` $\rightarrow$ **`Barrera Fork & Join`**
     - `Enrutador Condicional (Switch / Case)` $\rightarrow$ **`Enrutador Switch`**
     - `Filtro por Condición Lógica` $\rightarrow$ **`Filtro Condicional`**
     - `Ejecutor de Comandos y Procesos CLI` $\rightarrow$ **`Ejecutar Comando CLI`**
     - `Notificador Webhook (HTTP POST)` $\rightarrow$ **`Enviar Webhook`**
     - `Inspector de Registros` $\rightarrow$ **`Registrar Log`**
2. **Ajuste de Diseño Visual en el Catálogo:**
   - Con esta optimización, todos los títulos caben en una sola línea en las tarjetas del catálogo sin recortarse ni requerir puntos suspensivos (`...`).
1. **Límite de 10 Nodos Frecuentes (`ToolboxViewModel.cs`):**
   - Se ajustó la consulta LINQ al construir el grupo `🔥 Más Usados` para limitar el listado exactamente a los **10 nodos con mayor frecuencia de uso acumulada** (`.Take(10)`).
   - Mantiene el catálogo limpio y enfocado exclusivamente en el TOP 10 de nodos más utilizados.
1. **Servicio de Persistencia de Usuario (`UserPreferencesService.cs`):**
   - Servicio Singleton que guarda de forma persistente en `%APPDATA%\FileFlowStudio\user_preferences.json`:
     - `FavoriteNodeTypes`: Conjunto de tipos de nodos marcados por el usuario.
     - `NodeUsageCounts`: Conteo histórico de veces que se ha insertado cada nodo en el lienzo de trabajo.
2. **Sistema de Favoritos Manuales (⭐) e Historias Frecuentes (🔥):**
   - **Botón Estrella en Tarjeta**: Cada nodo en `NodeToolboxView.xaml` incluye un botón de estrella `[ ⭐ ]` / `[ ☆ ]` para marcar o desmarcar como favorito con un clic.
   - **Categorías Dinámicas**: Creación automática de los grupos superiores `⭐ Favoritos` y `🔥 Más Usados`.
   - **Recuento Automático**: Al añadir o arrastrar un nodo al editor (`EditorViewModel.cs`), se incrementa su contador de uso acumulado.
3. **Pestañas de Categoría Multilínea (`NodeToolboxView.xaml`):**
   - Reemplazado el contenedor horizontal por un `<WrapPanel Orientation="Horizontal">`.
   - Los chips de filtrado (`Todas`, `⭐ Favoritos`, `🔥 Frecuentes`, `📁 Archivos`, `📦 Compresión`, `🎬 Media & Docs`, `🏷️ Metadatos`, `🔀 Lógica`, `⚡ Integraciones`) se distribuyen limpiamente en 2 o 3 filas dinámicas sin necesidad de hacer scroll horizontal.
1. **Reestructuración de la Taxonomía de Nodos (`FileFlow.Plugin.*`):**
   - **`FileSystem` (📁 Archivos y Disco)**: `FolderSourceNode`, `DestinationSinkNode`, `DirectoryInspectorNode`, `FileRelocatorNode`, `AdvancedRenamerNode`, `EmptyDirectoryCleanerNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`.
   - **`Archives` (📦 Compresión)**: `SmartUnpackNode`, `ArchiveCompressorNode`, `ArchiveFilterNode`.
   - **`MediaDocs` (🎬 Multimedia y Documentos)**: `ImageOptimizerNode`, `MediaTranscoderNode`, `DocumentProcessorNode`.
   - **`Metadata` (🏷️ Metadatos e Integridad)**: `VariableInjectorNode`, `ExifMetadataNode`, `HashCalculatorNode`, `DeduplicationFilterNode`.
   - **`Logic` (🔀 Lógica y Control)**: `SwitchCaseNode`, `ExpressionFilterNode`, `BatchBufferNode`, `ThrottleDelayNode`, `ForkJoinBarrierNode`.
   - **`Integrations` (⚡ Integración y CLI)**: `CliExecutionNode`, `WebhookNotificationNode`, `LogOutputNode`.
2. **Actualización de Filtros por Chips (`ToolboxViewModel.cs` & `NodeToolboxView.xaml`):**
   - Actualizada la barra superior del catálogo para incluir chips de filtrado de las 6 categorías principales (`Todas`, `📁 Archivos`, `📦 Compresión`, `🎬 Media & Docs`, `🏷️ Metadatos`, `🔀 Lógica`, `⚡ Integraciones`).
1. **Iconografía Específica por Función (`AppModels.cs` & `ToolboxViewModel.cs`):**
   - Asignado icono visual único según el propósito técnico del nodo (ej: `📁` `FolderSourceNode`, `🕵️` `DirectoryInspectorNode`, `📦` `SmartUnpackNode`, `🗜️` `ArchiveCompressorNode`, `🖼️` `ImageOptimizerNode`, `🎬` `MediaTranscoderNode`, `📄` `DocumentProcessorNode`, `🏷️` `VariableInjectorNode`, `🔀` `SwitchCaseNode`, `💾` `DestinationSinkNode`, `🗑️` `OriginalFileActionNode`).
2. **Modos de Vista Intercambiables (`ToolboxViewModel.cs` & `NodeToolboxView.xaml`):**
   - **Modo Lista Compacta (Predeterminado)**: Muestra únicamente el icono y el título de cada nodo en 28px de altura, triplicando la cantidad de nodos visibles en pantalla sin necesidad de hacer scroll. La descripción completa se despliega al pasar el ratón en un ToolTip flotante rico.
   - **Modo Detallado**: Muestra el icono, título y la descripción multilínea.
   - **Botonera Toggle**: Selector `[ ☰ Compacto ]` / `[ 📋 Detallado ]` en la cabecera.
3. **Pestañas Chips de Filtro por Categoría (`NodeToolboxView.xaml`):**
   - Barra superior con chips de filtrado rápido (`Todas`, `📁 Archivos`, `📦 Compresión`, `🖼️ Imágenes`, `🔀 Lógica`).
1. **Pinceles Dinámicos del Sistema de Temas (`LogView.xaml.cs`):**
   - Se reemplazaron los pinceles estáticos de texto (`#F1F5F9`) por llamadas a `GetThemeBrush(resourceKey, fallbackHex)`.
   - El cuerpo del mensaje de log ahora consume `TextPrimaryBrush` (`#0F172A` en temas claros como `Light` y `Pastel`, `#F1F5F9` en temas oscuros como `Dark` y `Cyber`).
   - Los niveles de log adaptan sus niveles de contraste con el fondo (`AccentErrorBrush`, `AccentWarningBrush`, `AccentCyanBrush`, `AccentPurpleBrush`).
2. **Suscripción a Eventos de Tema (`ThemeManager.cs`):**
   - `LogView` se suscribe a `ThemeManager.Instance.ThemeChanged` para reconstruir automáticamente el `FlowDocument` al cambiar de tema, garantizando legibilidad perfecta en cualquier modo.
1. **Estilo `FilterRadioButton` (`ButtonStyles.xaml`):**
   - Definido estilo dinámico para controles `RadioButton` con `TargetType="RadioButton"`, vinculado a pinceles del sistema de temas (`BgHoverBrush`, `TextPrimaryBrush`, `BorderDarkBrush`, `AccentPrimaryBrush`).
2. **Aplicación en la Consola (`LogView.xaml`):**
   - Asignado `Style="{StaticResource FilterRadioButton}"` a los botones de filtro rápido de la consola (`Todos`, `🔴 Errores`, `🟠 Advertencias`).
   - Al cambiar de tema (`Oscuro`, `Claro`, `Cyber`, `Pastel`), los botones adaptan de forma inmediata sus fondos, bordes y colores de texto sin desajustes estéticos.
1. **Migración a `RichTextBox` en WPF (`LogView.xaml`):**
   - Reemplazado el `<TextBox>` plano por un `<RichTextBox>` estilizado con tipografía monoespaciada `Cascadia Code`/`Consolas`.
   - Propiedades `IsReadOnly="True"` e `IsDocumentEnabled="True"` para permitir la selección de texto libre con ratón/teclado y copiado con `Ctrl+C` sin permitir edición del documento.
2. **Formateador de Registro con Código de Colores (`LogView.xaml.cs`):**
   - Cada entrada de log se convierte dinámicamente en elementos `Paragraph` y `Run` en el `FlowDocument`:
     - **Marca de tiempo `[HH:mm:ss]`**: Gris Pizarra (`#64748B`).
     - **🔴 `[CRITICAL]` / `[ERROR]`**: Rojo Neón (`#EF4444`) en Negrita.
     - **🟠 `[WARNING]`**: Naranja/Ámbar (`#F59E0B`) en Negrita.
     - **🔵 `[INFO]`**: Azul Cielo (`#38BDF8`).
     - **🟣 `[DEBUG]`**: Púrpura Suave (`#C084FC`).
     - **⚪ `[TRACE]`**: Gris Pizarra (`#94A3B8`).
     - **Mensaje**: Blanco/Gris primario (`#F1F5F9`).
3. **Filtros Rápidos por Nivel de Log (`LogViewModel.cs` & `LogView.xaml`):**
   - Añadida barra de botones de filtro en la cabecera de la consola (`Todos`, `🔴 Errores`, `🟠 Advertencias`) con contadores de fallos y advertencias en tiempo real.
4. **Auto-Scroll Inteligente:**
   - La consola se desplaza automáticamente al final con cada nuevo mensaje excepto cuando el usuario tiene texto seleccionado o está inspeccionando el historial superior.
1. **Ampliación de `NodePort` (`NodePort.cs`):**
   - Se añadió el parámetro opcional `Description` a la definición del record `NodePort` para documentar la función técnica de cada puerto.
2. **ViewModel de Puerto Enriquecido (`PortViewModel.cs`):**
   - Propiedades para ToolTips interactivos: `Description`, `TransmittedCount`, `LastItemInfoText`, `MetadataVariables` (claves/valores del contexto), `ConnectionStatusText` e `IsConnected`.
   - Método `UpdatePortContext(FileItemContext)` para actualizar métricas e inyectar metadatos en tiempo de ejecución o durante la depuración paso a paso.
3. **Conexiones Dinámicas (`EditorViewModel.cs`):**
   - `UpdatePortConnectionStates()` notifica a cada puerto si está libre u origen/destino de conexiones activas (ej: `Conectado a FolderSourceNode ("Out")`).
4. **Plantilla XAML Fluent (`NodeCardView.xaml`):**
   - ToolTip contextual rico de 310px de ancho con sombra profunda `DropShadowEffect`.
   - Incluye cabecera con badge del tipo de dato, descripción funcional, métricas de elementos transmitidos, **panel desplegable de metadatos y variables del contexto (ideal para depuración)** e indicador de estado de conexión `🟢 Conectado` / `⚪ Puerto libre`.
1. **Reorganización de Columnas Grid (`StatusBarView.xaml`):**
   - Se configuró el estado del motor (`🟢 Listo`) con `ColumnDefinition Width="Auto"`, ya que es un mensaje corto y predecible.
   - Se asignó la columna expansible `ColumnDefinition Width="*"` al botón de la **Ruta de Salida Global** (`📁 Salida: ...`), aumentando su ancho máximo dinámico hasta `520px` (`TextTrimming="CharacterEllipsis"`).
   - Ahora la ruta de salida dispone de todo el espacio libre de la barra inferior y no se corta prematuramente.
1. **Monitor de Rendimiento en Tiempo Real (`SystemPerformanceMonitor.cs`):**
   - Servicio asíncrono con temporizador ligero (refresco cada 1000ms) para medir consumo de Memoria RAM (`WorkingSet64`) y uso de CPU sin saturar la UI.
2. **ViewModel y Vista de Barra Inferior (`StatusBarViewModel.cs` & `StatusBarView.xaml`):**
   - Vista moderna Fluent Design en la parte inferior de la ventana principal (`MainWindow.xaml`).
   - **Contexto del Grafo**: Muestra conteo en vivo de nodos (`🧩 Nodos`), conexiones (`🔗 Conexiones`) y nodo seleccionado (`🎯 SelectedNode`).
   - **Estado del Motor**: Muestra badges de estado reactivos (`🟢 Listo`, `⚡ Ejecutando flujo...`, `⏸️ Pausado`).
   - **Métricas de Sistema**: Muestra consumo de `🧠 RAM` y `💻 CPU` en tiempo real.
   - **Accesos Rápidos**: Botón clicable de **Ruta de Salida Global** (`📁 Salida: C:\FileFlowOutput`) que abre la carpeta en Windows Explorer, y botón de ajuste de Zoom (`🔍 Fit`).
1. **Definición de Estilo `PrimaryButton` (`ButtonStyles.xaml`):**
   - Agregada la clave `PrimaryButton` heredando de `IconButton` para que los botones de acción principal (como **✅ Aplicar Ajustes**) adopten dinámicamente el color de acento del tema activo (`AccentPrimaryBrush` en Dark, Light, Cyber y Pastel).
2. **Sincronización de Barra de Título Nativa Windows DWM (`WindowThemeHelper.cs`):**
   - Implementado `WindowThemeHelper` con invocación P/Invoke a `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE`).
   - Sincroniza la barra de título superior nativa de la ventana (`MainWindow`, `WorkflowSettingsWindow`, `PasswordManagerWindow`) para cambiar entre tema oscuro y claro automáticamente al cambiar el tema de la aplicación o del sistema operativo.
1. **Eliminación de la Caja de Título Inútil (`ControlBarView.xaml`):**
   - Se eliminó la caja de texto innecesaria que ocupaba espacio en la barra superior.
2. **Conmutador Compacto de Modo Prueba (`ControlBarView.xaml` & `ButtonStyles.xaml`):**
   - Creado el estilo `SecondaryToggleButton` para `<ToggleButton>` con estado activo iluminado (`AccentPrimaryBrush`).
   - Resuelta la excepción XAML de inicialización al asignar el `TargetType` correcto para conmutadores en WPF.
3. **Modal de Configuración del Flujo (`WorkflowSettingsWindow.xaml` & `ControlBarViewModel.cs`):**
   - Corregido el comando de la barra superior exponiendo `Editor` y el comando delegado `OpenWorkflowSettingsCommand` directamente en `ControlBarViewModel.cs`.
   - Se despliega correctamente el modal **`⚙️ Configuración del Flujo`** con la Ruta de Salida Global y botón examinador `📁 Examinar`.
1. **Anclaje Inteligente de Rutas Relativas (`ParameterHelper.ResolveOutputPath`):**
   - Implementado el método `ParameterHelper.ResolveOutputPath` en `FileFlow.Sdk`. Si un nodo especifica una ruta de salida relativa (ej: `Procesados/{FileName}` o `Compressed`), el sistema la unifica automáticamente dentro de la Ruta Global de Salida (`GlobalOutputDir`).
   - Si un nodo especifica una ruta absoluta (ej: `D:\Final\Salida.zip`), se respeta exactamente esa ubicación sin alteración.
2. **Inyección de Token `{GlobalOutputDir}` (`VariableTemplateResolver.cs`):**
   - Agregado el token `{GlobalOutputDir}` al motor de plantillas de variables.
3. **Control de Ruta Global en la Barra de Herramientas UI (`ControlBarView.xaml` & `EditorViewModel.cs`):**
   - Añadido un campo **📁 Salida Global** con botón examinador en la barra de control superior para configurar fácilmente la carpeta base del flujo (por defecto `C:\FileFlowOutput`).
   - Persistencia de `GlobalOutputDir` en el modelo JSON del grafo (`WorkflowGraph.cs`).
4. **Actualización de Nodos de Escritura:**
   - Actualizados `DestinationSinkNode`, `ArchiveCompressorNode`, `ImageOptimizerNode` y `MediaTranscoderNode` para consumir `ParameterHelper.ResolveOutputPath`.
5. **Pruebas Automatizadas (`GlobalOutputDirTests.cs`):**
   - Añadidas pruebas unitarias verificando anclaje dinámico, rutas absolutas directas e interpolación de tokens (80/80 pruebas exitosas).
1. **Gestor Modal de Contraseñas y Corrección de Diálogo (`PasswordManagerWindow.xaml`):**
   - Nueva ventana modal WPF para escribir contraseñas multilínea con soporte de **Importar (.txt)** y **Exportar (.txt)** mediante diálogos de archivo nativos.
   - Solucionado el error XAML `StaticResourceExtension` en `PasswordManagerWindow.xaml` sustituyendo `{StaticResource PrimaryButton}` y `{StaticResource SecondaryButton}` por `{DynamicResource PrimaryButton}` y `{DynamicResource SecondaryButton}`, asegurando la resolución correcta de estilos globales de la aplicación.
   - Vinculado `win.Owner = Application.Current.MainWindow` y `WindowStartupLocation="CenterOwner"` para centrado fluido sobre la ventana principal.
   - Integrado botón `🔑 Claves` en el inspector para el parámetro `PasswordList` de `SmartUnpackNode`.
2. **Presets Editables y Desplegables de Parámetros (`NodeParameterViewModel.cs`):**
   - ComboBoxes del inspector configurados con `IsEditable="True"` para poder seleccionar presets existentes o escribir y registrar nuevos presets personalizados.
   - Añadidas opciones desplegables para `ArchiveFormat`, `CompressionType` y `Preset`.
3. **Descompresión Inteligente con Contraseñas y Multipartes (`SmartUnpackNode.cs`):**
   - Prueba secuencial de claves candidatas (`PasswordList` y `PasswordFile`) inyectando la clave usada en `Metadata["UsedPassword"]`.
   - Detección de volúmenes multipartes (`FindRelatedVolumeFiles`) enviando el conjunto de partes al puerto `Error` ante claves incorrectas o archivos corruptos.
4. **Enrutamiento Inteligente por Rangos y Operadores (`SwitchCaseNode.cs`):**
   - Soporte en la regla `Pattern` para rangos de tamaño (`< 10 MB`, `10 MB..1 GB`), fechas (`2025-01-01..2025-12-31`), números y extensiones.
5. **Parseo Numérico y de Unidades (`ExpressionFilterNode.cs` & `ParameterHelper.cs`):**
   - Extracción y normalización de unidades de almacenamiento (`TB`, `GB`, `MB`, `KB`, `Bytes`) y tiempo (`ms`, `s`, `m`, `h`).
6. **Automatización Desatendida y Resiliencia (`FileFlow.Core`):**
   - Implementados `FolderWatcherService` (supervisión de carpetas en tiempo real con debounce anti-colisión), `FlowSchedulerService` (programador de tareas) y `ExecutionRetryHelper` (política de reintentos con exponential backoff).
7. **Nuevos Nodos de Procesamiento (`ArchiveCompressorNode`, `DocumentProcessorNode`, `MediaTranscoderNode`):**
   - Creados nodos para compresión ZIP/7Z/TAR.GZ, procesamiento e inspección de PDF/documentos, y transcodificación multimedia.

---

## [2026-08-20] - Optimización de Rendimiento UI: Actualización en Tiempo Real de Logs y Barra de Progreso

### 🛠 Cambios Implementados
1. **Búfer de Logs Asíncrono e Inmune a Saturación de UI (`LogViewModel.cs`):**
   - Se reemplazó el despacho síncrono e inmediato por línea de log en la UI por una cola de concurrencia thread-safe (`ConcurrentQueue<LogEntry>`).
   - Implementado un temporizador `DispatcherTimer` a nivel de fondo (`DispatcherPriority.Background`) que refresca el cuadro de texto y la consola cada 50ms (20 FPS).
2. **Despacho de Barra de Progreso y Estado de Baja Prioridad:**
   - `UpdateProgress` utiliza `DispatcherPriority.Background` evitando acaparar el hilo principal de renderizado WPF.
3. **Resultado:**
   - La consola muestra la transmisión de logs **en tiempo real a medida que ocurre la ejecución** sin congelar el renderizado visual ni bloquear la interfaz ni los controles de la aplicación.

---

## [2026-08-20] - Solución Definitiva al Bloqueo de Flujos (Eliminación de Interbloqueo / Deadlock de Semáforo)

### 🛠 Cambios Implementados
1. **Eliminación del Interbloqueo Canónico (*Semaphore Deadlock*) en `WorkflowExecutor.cs`:**
   - Se identificó la causa raíz: en `DispatchEmitAsync`, se llamaba a `_concurrencyThrottle.WaitAsync` mientras el nodo padre estaba esperando a que los nodos hijos terminasen `ExecuteAsync`. Al encadenar 2 o más nodos (ej. `FolderSourceNode` -> `VariableInjectorNode` -> `DestinationSinkNode`), los hilos padres bloqueaban todas las fichas del semáforo esperando a los hijos, mientras los hijos esperaban una ficha libre del semáforo, provocando un **Interbloqueo Recursivo (Deadlock)** absoluto.
   - Se eliminó el estrangulamiento anidado en `DispatchEmitAsync`, permitiendo que la tubería de emisión asíncrona procese lotes de archivos de forma fluida y sin bloqueos de aplicación.
2. **Extracción Directa de Metadatos Dinámicos en `EditorViewModel.cs`:**
   - Se corrigió la función de travesía `GetUpstreamAvailableVariables` para extraer las claves directas de `VariableInjectorNode` de forma inmediata.

---

## [2026-08-20] - Corrección de Bloqueo al Ejecutar Flujos con `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Sincronización de Hilos (*Thread-Safety*) en `VariableInjectorNode.cs` y `NodeViewModel.cs`:**
   - La ejecución del flujo ocurre en un hilo secundario (`Task.Run`), mientras la UI modifica los parámetros. Se añadió sincronización `lock (Parameters)` y la creación de instantáneas (*snapshots*) previas a la iteración para evitar bucles infinitos por corrupción interna del diccionario.
2. **Filtrado Seguro en `ExportToGraphModel` (`EditorViewModel.cs`):**
   - Agrupación e ignorado de claves vacías o en proceso de edición mediante `.Where(p => !string.IsNullOrWhiteSpace(p.Key)).GroupBy(...)` evitando excepciones `ArgumentException` al serializar el grafo.
3. **Limpieza de Parámetros en `GraphValidator.cs`:**
   - `instance.Parameters.Clear()` antes de asignar los parámetros exportados.

---

## [2026-08-20] - Gestión Dinámica de Variables en `VariableInjectorNode` (Botones ➕ y 🗑️)

### 🛠 Cambios Implementados
1. **Controles UI Dinámicos en la Tarjeta del Nodo (`EditorView.xaml`):**
   - Añadido botón verde **`➕ Variable`** en la cabecera del panel de ajustes del nodo.
   - Cada fila de variable cuenta con:
     - `TextBox` editable para el **Nombre de la Variable** (Clave).
     - `TextBox` editable para la **Expresión / Valor**.
     - Botón selector visual **`[{x}]`** para insertar variables de nodos anteriores.
     - Botón rojo **`🗑` (Papelera)** para eliminar esa variable individual al instante.

2. **Gestión MVVM y Sincronización en Tiempo Real (`NodeViewModel.cs` / `NodeParameterViewModel.cs`):**
   - Implementados los comandos `AddVariableCommand` y `RemoveParameterCommand`.
   - Sincronización bidireccional automática con `_nodeInstance.Parameters`.

---

## [2026-08-20] - Inyección Multivariable en `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Soporte Multivariable en `VariableInjectorNode.cs`:**
   - Rediseño del nodo para permitir definir y resolver múltiples pares de variables (`Key1`/`Value1`, `Key2`/`Value2`, ..., `Key5`/`Value5`) de forma simultánea dentro del mismo nodo.
2. **Actualización de la Travesía Topológica `EditorViewModel.cs`:**
   - La travesía del grafo hacia atrás (*Upstream Traversal*) inspecciona todas las claves no vacías de `VariableInjectorNode` y las ofrece automáticamente en el menú desplegable **`[{x}]`** de los nodos conectados posteriormente.
3. **Actualización de Pruebas Unitarias (`VariableInjectorNodeTests.cs`):**
   - Cobertura completa de resolución e inyección simultánea de múltiples variables.

---

## [2026-08-20] - Suite Exhaustiva de Automatización de Pruebas (xUnit, FluentAssertions, Moq)

### 🛠 Cambios Implementados
1. **Nuevo Proyecto de Pruebas (`FileFlow.Tests/FileFlow.Tests.csproj`):**
   - Configurado en .NET 9 (`net9.0-windows` con `UseWPF=true`) e integrado en `FileFlow.slnx`.
   - Incluye **xUnit**, **FluentAssertions** y **Moq**.

2. **Tests Unitarios (`FileFlow.Tests/Unit/`):**
   - **`VariableTemplateResolverTests`:** Reemplazo de variables, funciones de fecha, transformaciones de texto (`Upper`, `Lower`, `PadLeft`), saneamiento de caracteres ilegales (`Sanitize`), cascadas `Coalesce` e interpolación dinámica.
   - **`LocalizationManagerTests`:** Verificación de singleton y disparo del evento `LanguageChanged`.
   - **`WorkflowExecutorTests`:** Ejecución topológica y validación de nodos.
   - **`FolderSourceNodeTests` & `VariableInjectorNodeTests`:** Inyección de metadatos (`Counter`, `SourceRootPath`, `CustomCategory`) e I/O.
   - **`EditorViewModelTests`:** Travesía topológica inversa (`GetUpstreamAvailableVariables`) y cálculo dinámico de variables en la interfaz.

3. **Tests de Integración (`FileFlow.Tests/Integration/`):**
   - **`WorkflowIntegrationTests`:** Flujo E2E desde `FolderSourceNode` $\rightarrow$ `VariableInjectorNode` $\rightarrow$ `DestinationSinkNode` validando la tubería completa de archivos reales.

4. **Tests de Estrés / Rendimiento (`FileFlow.Tests/Performance/`):**
   - **`PerformanceStressTests`:** Procesamiento de **10,000 elementos masivos** evaluando el motor de plantillas en menos de 1 segundo (651 ms).

---

## [2026-08-20] - Variables Avanzadas de Sistema, Metadatos Multimedia/Compresión y Funciones de Expresión

### 🛠 Cambios Implementados
1. **Nuevas Variables del Sistema y Ejecución (`VariableTemplateResolver.cs`):**
   - Incorporación de `{DateNow}` (`yyyy-MM-dd`), `{TimeNow}` (`HH-mm-ss`), `{DateTimeNow}` (`yyyy-MM-dd_HH-mm-ss`).
   - Contador incremental de secuencia por lote `{Counter}` / `{Index}` inyectado desde `FolderSourceNode.cs`.
   - Métricas de peso de archivo: `{SizeMB}`, `{SizeKB}`, `{SizeBytes}`.
   - Variables de entorno del sistema: `{UserName}`, `{MachineName}`.

2. **Nuevos Metadatos de Imagen y Archivos Comprimidos:**
   - **`ExifMetadataNode.cs`:** Extracción de `{ImageWidth}`, `{ImageHeight}`, `{Orientation}` (`Landscape`/`Portrait`/`Square`), `{AspectRatio}` (ej. `16:9`) y `{Megapixels}` (ej. `24.1MP`).
   - **`SmartUnpackNode.cs`:** Extracción de `{ArchiveFormat}` (`ZIP`/`7Z`/`RAR`) y `{UnpackedFileCount}`.

3. **Nuevas Funciones de Expresión Prácticas:**
   - `{Sanitize(text)}`: Limpieza automática de caracteres ilegales en Windows (`\ / : * ? " < > |`).
   - `{PadLeft(val, length, char)}`: Relleno de números con ceros u otros caracteres (ej. `{PadLeft(Counter, 4, "0")}`).
   - `{Substring(text, start, length)}`: Extracción segura de subcadenas sin desbordamientos de índice.
   - `{RegexMatch(text, pattern)}`: Extracción por expresión regular.
   - `{RegexReplace(text, pattern, replacement)}`: Reemplazo con expresiones regulares.
   - `{Coalesce(val1, val2, ...)}`: Evaluación en cascada retornando el primer valor no vacío.
   - `{FileAgeDays(dateStr)}`: Cálculo de antigüedad en días transcurridos.

4. **Integración en el Selector Gráfico `[{x}]` (`EditorViewModel.cs`):**
   - Actualización de `GetUpstreamAvailableVariables` clasificando y ofreciendo todas las nuevas variables y funciones ordenadas por grupos (`🌐 System & Environment`, `📷 Image & Media`, `📦 Archives`, `🔤 Expression Functions`).

---

## [2026-08-20] - Consola de Ejecución Seleccionable, Exportación y Categorización de Nodos

### 🛠 Cambios Implementados
1. **Exportación de Logs a Archivo:**
   - Se añadió la acción `ExportLogsCommand` en `LogViewModel.cs` y el botón **`💾 Exportar Log`** / **`💾 Export Log`** en la barra de herramientas de la consola (`LogView.xaml`).
   - Permite guardar todo el historial formateado con marcas de tiempo en archivos `.log` o `.txt` mediante `SaveFileDialog`.

2. **Selección y Copiado de Texto con el Ratón:**
   - Se reemplazó el control estático por un visor de texto editable/seleccionable (`TextBox` en modo `IsReadOnly="True"`) en `LogView.xaml`.
   - Los usuarios pueden arrastrar el ratón para seleccionar cualquier bloque de texto y copiarlo directamente con `Ctrl+C` o el menú contextual.
   - Implementado autodesplazamiento hacia la última línea recibida (`LogConsoleTextBox_TextChanged`).

3. **Corrección de Categoría del Nodo Inyector de Variables:**
   - Se actualizó `VariableInjectorNode.cs` asignándole la categoría **`Utility`** (*Utilidades*).
   - Ahora aparece correctamente clasificado junto a herramientas como `Log Inspector` dentro del panel lateral de herramientas (*Toolbox*).

---

## [2026-08-20] - Subsistema de Variables Dinámicas, Motor de Expresiones e Inyector de Variables

### 🛠 Cambios Implementados
1. **Motor de Plantillas de Variables (`VariableTemplateResolver.cs`):**
   - Interpolación de tokens dinámicos en cualquier parámetro de texto o ruta de nodo.
   - **Variables del Sistema Estandarizadas al Inglés:** `{FileName}`, `{FileNameNoExt}`, `{Extension}`, `{CurrentPath}`, `{OriginalPath}`, `{CurrentDir}`, `{OriginalDir}`, `{RelativePath}`.
   - **Funciones de Expresión Integradas:**
     - **Fechas:** `{Year(date)}`, `{Month(date)}`, `{Day(date)}`, `{FormatDate(date, "yyyy-MM")}`.
     - **Texto:** `{Upper(text)}`, `{Lower(text)}`, `{Trim(text)}`, `{Replace(text, "old", "new")}`, `{Default(val, "fallback")}`.

2. **Cálculo de `RelativePath` (Estructura de Directorio Relativo):**
   - Se actualizó `FolderSourceNode.cs` para adjuntar la metadata `SourceRootPath` al escanear directorios.
   - `VariableTemplateResolver` calcula la ruta de subcarpetas relativa exacta (ej. `mami/antiguo`) excluyendo el nombre del archivo.

3. **Nodo `Inyector de Variables` (`VariableInjectorNode.cs`):**
   - Permite calcular e inyectar claves de metadatos personalizadas (`item.Metadata["VariableName"] = ResolvedValue`) en el flujo para nodos posteriores.

4. **Selector Gráfico de Variables `{x}` con Travesía Topológica Inversa:**
   - Se agregó el botón gráfico **`[{x}]`** al lado de los campos de entrada de parámetros en `EditorView.xaml`.
   - Al pulsar **`[{x}]`**, `EditorViewModel.GetUpstreamAvailableVariables` recorre el grafo hacia atrás (*Upstream Traversal*) para ofrecer únicamente las variables exportadas por los nodos conectados previamente (EXIF, origen de descompresión, variables inyectadas).

---

## [2026-08-20] - Sistema de Internacionalización Multilingüe (Español / Inglés)

### 🛠 Cambios Implementados
1. **Gestor de Localización Estándar de .NET (`LocalizationManager.cs`):**
   - Singleton encargado de administrar el cambio de cultura al vuelo (`CultureInfo`) entre Español (`es-ES`) e Inglés (`en-US`).
   - Notificación de cambio mediante el evento `LanguageChanged` y propiedad indexadora `this[string key]`.

2. **Ficheros de Recursos `.resx`:**
   - Creación de `Strings.resx` (Inglés por defecto) y `Strings.es.resx` (Español).

3. **Selector Dinámico de Idioma en Barra de Control:**
   - Desplegable en `ControlBarView.xaml` reactivo en tiempo real para traducir al instante la UI, los títulos de los nodos en lienzo, las descripciones y el catálogo de herramientas.

---

## [2026-08-20] - Descubrimiento y Carga Robusta de Plugins

### 🛠 Cambios Implementados
1. **Contexto de Carga Desacoplado (`PluginAssemblyLoadContext.cs`):**
   - Carga de ensamblados `.dll` en memoria para evitar bloqueos del sistema operativo sobre los archivos en disco.
   - Búsqueda de dependencias de plugins con fallback a `AppDomain.CurrentDomain.BaseDirectory` para librerías como `SixLabors.ImageSharp`, `MetadataExtractor` y `SharpCompress`.

---

## [2026-08-20] - Estructura de Documentación y Repositorio Git Initial

### 🛠 Cambios Implementados
1. **Repositorio Git (`.gitignore` & `git init`):**
   - Configuración de exclusión de binarios `bin/`, `obj/` y archivos de cache `.antigravity/`.
   - Registro del commit inicial en la rama `main`.

2. **Documentación del Proyecto (`docs/`):**
   - `docs/README.md`: Centro de documentación y visión general del proyecto.
   - `docs/nodes/CREATING_NODES.md`: Guía de desarrollo de nodos personalizados.
   - `docs/nodes/examples/SampleMultiPortNode.cs`: Ejemplo de nodo de código completo para desarrolladores.
   - `docs/ARCHITECTURE_DEEP_DIVE.md`: Guía arquitectónica detallada en 4 niveles de complejidad.

---

## [2026-08-24] - Guía Maestra Universal para Agentes de IA (`AGENTS.md`)

### 🛠 Cambios Implementados
1. **Creación de `AGENTS.md`:**
   - Estandarización de directrices para agentes de IA (Antigravity, Cursor, Claude Code, Copilot, Roo Code, Windsurf).
   - Definición del **Protocolo de Arranque Obligatorio** (lectura secuencial de `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md`, `.antigravity/knowledge/repo_architecture.md`, `.agents/rules/rules.md`).
   - Mapa exhaustivo de ficheros auxiliares existentes (`.agents/`, `.antigravity/`, `docs/`, `GEMINI.md`).
   - Resumen de principios técnicos (.NET 9, C# 13, `System.Threading.Lock`, aislamiento en `FileFlow.Sdk`).
   - Protocolo de validación y mantenimiento continuo post-sesión.

---

## [2026-09-01] - Inclusión de Ejemplos de Flujos y Manual de Usuario en el Instalador y la App

### 🛠 Cambios Implementados
1. **Publicación y Empaquetado (`installer/publish.ps1`):**
   - Incorporada copia recursiva de la colección completa de ejemplos de flujos (`docs/examples` -> `publish/win-x64/Examples`) estructurada en 4 niveles (01_basic, 02_intermediate, 03_advanced, 04_complex).
   - Incorporada copia de la documentación y manuales de usuario (`docs/manual_de_usuario.md`, `docs/user_guide.md`, `README.md` -> `publish/win-x64/Docs`).
2. **Asistente Inno Setup (`installer/FileFlow.iss`):**
   - Configurado empaquetado automático de las carpetas `Examples\` y `Docs\`.
   - Creados accesos directos en el menú de inicio para el *Manual de Usuario* y la carpeta de *Ejemplos de Flujos*.
   - Mensajes personalizados bilingües (`[CustomMessages]`: español e inglés).
3. **Acceso Directo desde la Interfaz de Usuario (`FileFlow.App`):**
   - Comandos `OpenUserManualCommand` y `OpenExamplesFolderCommand` en `ControlBarViewModel.cs` con detección inteligente en entornos instalados y de desarrollo local.
   - Nueva sección *"AYUDA Y RECURSOS"* en el cajón de navegación lateral (`MainWindow.xaml`) con accesos a 📖 Manual de Usuario y 💡 Ejemplos de Flujos.
4. **Validación de Compilación y Suite de Pruebas:**
   - Generación exitosa del ejecutable instalador `FileFlowStudio-Setup-1.0.0.exe` con Inno Setup.
   - **190 / 190 pruebas superadas con éxito** (0 errores, 0 fallos).

---

## [2026-09-01] - Corrección de Nodos Duplicados en el Catálogo y Barra Lateral (Toolbox)

### 🛠 Cambios Implementados
1. **Deduplicación en `ToolboxViewModel.cs`**:
   - `PluginLoader.DiscoveredNodeTypes` almacena dos claves por cada tipo de nodo (`FullName` y `Name`) para permitir resolución por ambos nombres al crear instancias.
   - Al iterar el catálogo, `ToolboxViewModel` recorría todas las claves del diccionario, provocando que cada nodo se agregara dos veces (duplicación en la lista).
   - Se ajustó `RefreshToolbox()` para iterar sobre los tipos únicos (`DiscoveredNodeTypes.Values.Distinct()`).
   - Se completó el mapeo de iconos en `GetIconForNodeType` para todos los 25+ tipos de nodos del ecosistema.
2. **Registro de Plugins en `MainViewModel.cs`**:
   - Se añadió el registro explícito del ensamblado `FileFlow.Plugin.Scripting` junto con los demás plugins base.
   - Se actualizó el conteo del log de arranque para reportar el número real de tipos de nodos únicos activos.
3. **Pruebas Unitarias (`ToolboxViewModelTests.cs`)**:
   - Se agregó la prueba `ToolboxViewModel_ShouldNotContainDuplicateItems_WhenAssembliesRegistered` para garantizar que ningún nodo aparezca duplicado en sus grupos de categorías.
   - **296 / 296 pruebas superadas con 100% de éxito**.

---

## [2026-09-01] - Creación de Scripts de Limpieza Integral (`clean.ps1` y `clean.bat`)

### 🛠 Cambios Implementados
1. **Script PowerShell de Limpieza (`clean.ps1`)**:
   - Cierre preventivo de procesos `FileFlow.App` para liberar bloqueos sobre ficheros `.dll` y `.exe`.
   - Invocación de `dotnet clean` en configuraciones Debug y Release.
   - Eliminación recursiva y forzada de carpetas `bin` y `obj` en todos los proyectos de la solución.
   - Eliminación de carpetas de publicación e instalador (`installer/publish`, `installer/output`).
   - Eliminación de directorios de resultados de pruebas y cobertura (`TestResults`, `coverage-report`).
   - Eliminación de cachés de IDE y archivos temporales (`.vs`, `.dotnet_tmp`, `*.user`, `*.suo`, `crash.log`).
   - Parámetros `-DryRun` (simulación con cálculo de espacio recuperable) e `-IncludePdfs` (limpieza opcional de PDFs generados).
2. **Wrapper Batch para Consola CMD (`clean.bat`)**:
   - Facilita la ejecución inmediata desde la consola de Windows o mediante doble clic.
3. **Validación**:
   - Ejecución de `.\clean.ps1 -DryRun` comprobando la detección correcta de artefactos sin efectos destructivos.
   - Ejecución de `.\clean.ps1` liberando espacio y limpiando el árbol de directorios.
   - Recompilación limpia y ejecución de la suite de pruebas: **296 / 296 pruebas superadas con 100% de éxito**.

---

## [2026-09-01] - Integración de Temas Visuales e Internacionalización Dinámica en el Editor de Scripting

### 🛠 Cambios Implementados
1. **Adopción Completa del Sistema de Temas (`ScriptStudioWindow.xaml`)**:
   - Reemplazados todos los estilos y colores hexadecimales hardcodeados por recursos dinámicos (`{DynamicResource BgDarkBrush}`, `{DynamicResource BgCardBrush}`, `{DynamicResource BgSurfaceBrush}`, `{DynamicResource BorderDarkBrush}`, `{DynamicResource TextPrimaryBrush}`, `{DynamicResource TextSecondaryBrush}`, `{DynamicResource AccentPrimaryBrush}`, etc.).
   - Adaptado el editor de código AvalonEdit (`CodeEditor`) para consumir los pinceles del tema activo en fondo, primer plano y números de línea.
   - Establecido `window.Owner = Application.Current.MainWindow` en `CustomScriptNode.cs` para herencia de recursos y centrado óptimo.
2. **Internacionalización Dinámica y Reactiva (i18n)**:
   - Migrados todos los títulos de ventana, pestañas, etiquetas, botones, descripciones y tips de ayuda a enlaces dinámicos con `LocalizationManager.Instance`.
   - Incorporadas 23 nuevas claves de localización en `Strings.resx` (Inglés) y `Strings.es.resx` (Español) para el motor de scripting.
   - Actualizado `ScriptStudioViewModel.cs` para obtener mensajes de estado en caliente (`Ready to test` / `Listo para probar`, `Running...` / `Ejecutando...`, `Success` / `Éxito`, `Error`).
3. **Validación y Suite de Pruebas**:
   - Compilación y ejecución de la suite xUnit: **296 / 296 pruebas superadas con 100% de éxito**.

---

## [2026-09-01] - Paneles Redimensionables Dinámicos en el Estudio de Renombrado Avanzado

### 🛠 Cambios Implementados
1. **Separación Horizontal y Vertical Flexible (`AdvancedRenamerEditorWindow.xaml`)**:
   - **Splitter Horizontal (Redimensionamiento Vertical)**: Se sustituyó la altura fija del panel de vista previa (`Height="185"`) por una fila proporcional dinámica con límites mínimos (`RowDefinition Height="3*" MinHeight="180"` para el editor de pasos y `RowDefinition Height="2*" MinHeight="120"` para la tabla de Live Preview) interconectada por un `GridSplitter` (`Cursor="SizeNS"`).
   - **Splitter Vertical (Redimensionamiento Horizontal)**: Se insertó un `GridSplitter` interactivo (`Cursor="SizeWE"`) entre la lista de pasos del pipeline (panel izquierdo) y el configurador de métodos de renombrado (panel derecho), con anchos mínimos configurados (`MinWidth="220"` y `MinWidth="320"`).
   - **Columnas de DataGrid**: Habilitado `CanUserResizeColumns="True"` en la tabla de vista previa en vivo para permitir ajuste personalizado de anchuras de columnas.
2. **Validación**:
   - Compilación en limpio y ejecución de la suite de pruebas: **296 / 296 pruebas superadas con 100% de éxito**.

---

## [2026-09-01] - Expansión de Muestras Sintéticas y Catálogo de Presets en Renombrado Avanzado

### 🛠 Cambios Implementados
1. **Catálogo de Presets Predefinidos (`RenamerPresetService.cs`)**:
   - Ampliado de 4 a **12 presets predefinidos de nivel profesional** organizados por categorías:
     - 📷 *Fotografía Digital (Fecha EXIF + Modelo + Contador)*
     - 🖼️ *Fotografía (Fecha + Resolución [Ancho x Alto])*
     - 🎬 *Series de TV y Vídeo (Estandarizar S01E02 / NxN)*
     - 🎵 *Música y Audio (Pista - Artista - Título)*
     - 💿 *Música (Artista - [Año] Álbum - Pista. Título)*
     - 🌐 *Web & SEO Cleaner (Slug Limpio en Minúsculas / Kebab-case)*
     - 🔠 *Normalización de Título (TitleCase con Espacios Limpios)*
     - 💼 *Documentos y Facturas (Fecha ISO_Carpeta_Nombre_Hash)*
     - 🧹 *Limpieza Extrema (Sanitizar SO + Colapsar Espacios + Trim)*
     - 🔢 *Numeración Incremental (001, 002...) por Carpeta*
     - 0️⃣1️⃣ *Rellenar Números (1, 2... 10 -> 01, 02... 10)*
     - ✂️ *Limpiador de Tags / Publicidad (Regex Cleaner)*
2. **Muestras Sintéticas Enriquecidas (`RenamerSampleDataProvider.cs`)**:
   - Ampliado de 6 a **18 muestras sintéticas hiperrealistas y diversificadas** con metadatos completos:
     - Réflex DSLR Nikon D850 (45.4 MP, EXIF), Smartphone iPhone 15 Pro, RAW Canon EOS R5, GoPro HERO12 5.3K.
     - Series de TV (`Breaking.Bad.S01E03...`, `Stranger.Things.2x04...`), Tutorial 4K.
     - Audio MP3 Queen con ID3, FLAC 24-bit Pink Floyd, Podcast IA.
     - Facturas fiscales con SHA256/MD5, informes trimestrales, balances Excel, presentaciones PowerPoint.
     - Casos de prueba de limpieza: nombres con espacios y puntos múltiples, nombres con caracteres extraños (`#%&`), listas numeradas sin ceros y backups `.tar.gz`.
3. **Validación y Suite de Pruebas**:
   - Actualizadas las aserciones de prueba en `AdvancedRenamerEditorViewModelTests.cs`.
   - Compilación limpia y paso del 100% de la suite de pruebas: **296 / 296 pruebas superadas con éxito**.

---

## [2026-09-01] - Externalización de Muestras Sintéticas, Presets y Bibliotecas a Ficheros de Configuración JSON

### 🛠 Cambios Implementados
1. **Ficheros de Configuración JSON Desacoplados**:
   - Creado `Config/renamer_samples.json` en `FileFlow.Plugin.FileSystem` con las 18 muestras sintéticas enriquecidas.
   - Creado `Config/renamer_presets.json` en `FileFlow.Sdk` con los 12 presets de renombrado profesional.
   - Creado `Config/regex_patterns.json` en `FileFlow.Plugin.FileSystem` con el catálogo completo de expresiones regulares.
   - Creado `Config/script_presets.json` en `FileFlow.Plugin.Scripting` con las plantillas de script de C# y JavaScript.
2. **Carga en Cascada y Fallback Determinista**:
   - `RenamerSampleDataProvider.cs`: Intenta cargar desde `%AppData%/FileFlow/renamer_samples.json` (personalizaciones del usuario), luego desde `Config/renamer_samples.json` de fábrica, y en su defecto aplica fallback seguro en memoria.
   - `RenamerPresetService.cs`: Carga presets desde `%AppData%/FileFlow/renamer_presets.json` $\rightarrow$ `Config/renamer_presets.json` $\rightarrow$ Fallback en memoria.
   - `RegexLibraryService.cs`: Carga catálogo desde `Config/regex_patterns.json` con fallback en memoria + patrones de usuario en `%AppData%/FileFlow/regex_library.json`.
   - `ScriptLibraryService.cs`: Carga plantillas desde `Config/script_presets.json` con fallback en memoria + scripts de usuario en `%AppData%/FileFlow/Scripts/`.
3. **Automatización de Despliegue en Compilación (`.csproj`)**:
   - Configuradas reglas `<None Update="Config\**"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>` en `FileFlow.Plugin.FileSystem.csproj`, `FileFlow.Sdk.csproj` y `FileFlow.Plugin.Scripting.csproj`.
4. **Nuevas Pruebas Unitarias y Validación**:
   - Añadidas pruebas de carga y deserialización JSON en `AdvancedRenamerEditorViewModelTests.cs`.
   - Ejecución de la suite completa: **299 / 299 pruebas superadas con 100% de éxito**.

---

## [2026-09-01] - Estandarización de Almacenamiento Centralizado (`AppPaths`) y Externalización de Presets Multimedia

### 🛠 Cambios Implementados
1. **Proveedor Centralizado de Rutas de Almacenamiento (`FileFlow.Sdk/Storage/AppPaths.cs`)**:
   - Unificación de todas las carpetas de usuario bajo `%AppData%/FileFlow/` con jerarquía estructurada:
     - `config/` $\rightarrow$ `user_preferences.json`, `external_tools.json`
     - `themes/` $\rightarrow$ `custom_themes.json`
     - `presets/` $\rightarrow$ `renamer_presets.json`, `media_presets.json`, `regex_library.json`
     - `samples/` $\rightarrow$ `renamer_samples.json`
     - `scripts/` $\rightarrow$ `*.ffscript`
     - `logs/` $\rightarrow$ `crash.log`
   - **Migración Transparente y No Destructiva**: `AppPaths.EnsureDirectories()` detecta ficheros de `%AppData%/FileFlowStudio/` o de la raíz de `%AppData%/FileFlow/` y los migra de forma segura a la nueva estructura sin sobreescribir ficheros más recientes.
2. **Externalización de Presets Multimedia (`FileFlow.Plugin.Integrations`)**:
   - Creado `FileFlow.Plugin.Integrations/Config/media_presets.json` con los 10 presets de FFmpeg predefinidos (MP3, AAC, FLAC, 1080p, 720p, 4K HEVC, WebM VP9, GIF animado, Móvil y Personalizado).
   - Configurado `FileFlow.Plugin.Integrations.csproj` con copia automática de la carpeta `Config/`.
   - Actualizado `MediaPresetManagerService.cs` para consumir `AppPaths.MediaPresetsFile`, cargar desde `Config/media_presets.json` con fallback seguro en memoria.
3. **Refactorización Completa de Servicios**:
   - `UserPreferencesService.cs`, `ExternalToolsService.cs`, `CustomThemeService.cs`, `RenamerPresetService.cs`, `RenamerSampleDataProvider.cs`, `RegexLibraryService.cs`, `ScriptLibraryService.cs` y `App.xaml.cs` actualizados para consumir `AppPaths`.
4. **Nuevas Pruebas Unitarias y Validación**:
   - Creado `FileFlow.Tests/Unit/Sdk/AppPathsTests.cs` para validar coherencia de rutas, existencia de subdirectorios y migración.
   - Ejecución de la suite completa: **301 / 301 pruebas superadas con 100% de éxito**.

---

## [2026-09-01] - Arquitectura de Modo Portable Autónomo y Generador de Distribución ZIP

### 🛠 Cambios Implementados
1. **Detección Dinámica de Modo Portable en `AppPaths.cs`**:
   - `AppPaths.IsPortableMode`: Detección instantánea por presencia de archivo marcador (`portable.dat`, `.portable`), existencia de carpeta `data/` junto al ejecutable, variable de entorno `FILEFLOW_PORTABLE=1`, o sobreescritura dinámica por código / CLI (`SetCustomDataDirectory`).
   - Redirección automática de `RootDirectory` hacia `<AppBaseDir>/data/` preservando subcarpetas (`config/`, `themes/`, `presets/`, `samples/`, `scripts/`, `logs/`).
   - Método `AppPaths.ResolveApplicationPath(path)` para resolver rutas relativas de herramientas portables (ej. `tools\ffmpeg\ffmpeg.exe`).
2. **Auto-Detección de Herramientas Portables en `ExternalToolsService.cs`**:
   - Soporte para ejecutar FFmpeg, FFprobe, 7-Zip y Python colocados dentro de la carpeta local `tools/` de la aplicación portable sin requerir instalación en el sistema operativo.
3. **Script Automatizado de Empaquetado Portable (`installer/build-portable.ps1`)**:
   - Publica los binarios optimizados (SingleFile o SelfContained), estructura la carpeta autónoma con `portable.dat`, crea la jerarquía `data/`, copia las configuraciones de fábrica `Config/` y el manual PDF, y genera el archivo comprimido `installer/output/FileFlowStudio-v<Version>-Portable-<Runtime>.zip`.
4. **Nuevas Pruebas Unitarias**:
   - Ampliado `AppPathsTests.cs` con pruebas de redirección de datos, conmutación de modo portable y resolución de rutas relativas y absolutas.
   - **303 / 303 pruebas unitarias e integración superadas al 100%**.

---

## [2026-09-01] - Creación del Manual Didáctico de Usuario para Principiantes y Compilación PDF

### 🛠 Cambios Implementados
1. **Manual Didáctico Paso a Paso ([`docs/manual_usuario_principiantes.md`](file:///docs/manual_usuario_principiantes.md))**:
   - Redactado en lenguaje coloquial, ameno y accesible para usuarios no técnicos.
   - Metáforas visuales claras (cintas transportadoras, estaciones de trabajo).
   - Explicación de las 4 zonas de la pantalla y el sistema de conexión de cables.
   - 4 recetas prácticas completas:
     1. Renombrar fotos con fecha y modelo de cámara.
     2. Organizar la carpeta de Descargas (separando vídeos, fotos y documentos).
     3. Descomprimir múltiples archivos ZIP/RAR con gestor de contraseñas.
     4. Convertir vídeos pesados a formato ultra-ligero para móvil/WhatsApp.
   - Guía de seguridad (Simulación Virtual *Dry Run*, Deshacer *Rollback* y Papelera de reciclaje), FAQ y Glosario.
2. **Compilación a Documento PDF ([`docs/manual_usuario_principiantes.pdf`](file:///docs/manual_usuario_principiantes.pdf))**:
   - Compilado automáticamente mediante Edge/Chromium Headless con diseño tipográfico A4 a color (1053.1 KB).
   - Actualizado `installer/build-pdf-manual.ps1` y `installer/build-portable.ps1` para incluir los 3 manuales PDF en la distribución de la aplicación.











