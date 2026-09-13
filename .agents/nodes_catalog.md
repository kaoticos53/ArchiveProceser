# Catálogo Rápido de Nodos de FileFlow Studio

Este documento ofrece un mapa compacto de los nodos disponibles en los 11 plugins de **FileFlow Studio** con sus puertos, parámetros clave y enlaces a código fuente.

> [!NOTE]
> Para consultar la especificación extendida detallada con descripciones de cada parámetro:
> 📄 [**`docs/history/2026-09-13_nodes_catalog_full.md`**](file:///docs/history/2026-09-13_nodes_catalog_full.md)

---

## 1. FileFlow.Plugin.FileSystem (13 Nodos)

| Nodo | Entradas | Salidas | Parámetros Clave | Código Fuente |
| :--- | :--- | :--- | :--- | :--- |
| **FolderSourceNode** | — | `Out` | `SourcePath`, `ExtensionFilter`, `Recursive`, `WatchRealtime` | [`FolderSourceNode.cs`](file:///FileFlow.Plugin.FileSystem/FolderSourceNode.cs) |
| **DestinationSinkNode** | `In` | `Done` | `DestinationRoot`, `ConflictStrategy` | [`DestinationSinkNode.cs`](file:///FileFlow.Plugin.FileSystem/DestinationSinkNode.cs) |
| **AdvancedRenamerNode** | `In` | `Out`, `Error` | `Pattern`, `CollisionStrategy`, `PreserveExtension` | [`AdvancedRenamerNode.cs`](file:///FileFlow.Plugin.FileSystem/AdvancedRenamerNode.cs) |
| **FileRelocatorNode** | `In` | `Out`, `Error` | `TargetDirectory`, `OperationType` (Move/Copy/HardLink), `VerifyChecksum` | [`FileRelocatorNode.cs`](file:///FileFlow.Plugin.FileSystem/FileRelocatorNode.cs) |
| **SafeRecycleDeleteNode** | `In` | `Out`, `Error` | `DeleteOriginal`, `UseShellRecycleBin` | [`SafeRecycleDeleteNode.cs`](file:///FileFlow.Plugin.FileSystem/SafeRecycleDeleteNode.cs) |
| **OriginalFileActionNode**| `In` | `Out`, `Error` | `ActionType` (Keep/MoveToRecycleBin/MoveToQuarantine), `QuarantinePath` | [`OriginalFileActionNode.cs`](file:///FileFlow.Plugin.FileSystem/OriginalFileActionNode.cs) |
| **OperationReportNode** | `In` | `Out`, `Report`, `Error` | `ReportFormat` (HTML/MD/JSON/CSV), `ReportScope`, `DestinationFolder` | [`OperationReportNode.cs`](file:///FileFlow.Plugin.FileSystem/OperationReportNode.cs) |
| **DirectoryInspectorNode** | `In` | `HasFiles`, `IsEmpty`, `Out` | `InspectRecursively`, `MinimumFileCount` | [`DirectoryInspectorNode.cs`](file:///FileFlow.Plugin.FileSystem/DirectoryInspectorNode.cs) |
| **EmptyFolderCleanerNode** | `In` | `Out`, `Cleaned`, `Error` | `TargetDirectory`, `DeleteRootIfEmpty`, `CleanRecursively` | [`EmptyFolderCleanerNode.cs`](file:///FileFlow.Plugin.FileSystem/EmptyFolderCleanerNode.cs) |
| **VariableInjectorNode** | `In` | `Out`, `Error` | `Injections` (pares clave-valor con resolución de expresiones) | [`VariableInjectorNode.cs`](file:///FileFlow.Plugin.FileSystem/VariableInjectorNode.cs) |
| **LogOutputNode** | `In` | `Out` | `CustomMessage` (multilínea con tokens), `LogLevel`, `Compact` | [`LogOutputNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/LogOutputNode.cs) |
| **SyntheticDataSourceNode**| — | `Out` | `DataSetDefinitionJson`, `UseMemoryVirtualStore` | [`SyntheticDataSourceNode.cs`](file:///FileFlow.Plugin.FileSystem/SyntheticDataSourceNode.cs) |
| **VirtualToDiskNode** | `In` | `Out`, `Error` | `TargetDirectory`, `OverwriteExisting` | [`VirtualToDiskNode.cs`](file:///FileFlow.Plugin.FileSystem/VirtualToDiskNode.cs) |

---

## 2. FileFlow.Plugin.Logic (5 Nodos)

| Nodo | Entradas | Salidas | Parámetros Clave | Código Fuente |
| :--- | :--- | :--- | :--- | :--- |
| **ConditionalFilterNode** | `In` | `True`, `False` | `Rules` (expresiones lógicas compuestas, operadores AND/OR) | [`ConditionalFilterNode.cs`](file:///FileFlow.Plugin.Logic/ConditionalFilterNode.cs) |
| **SwitchCaseNode** | `In` | `Case1`..`CaseN`, `Default` | `EvaluateExpression`, `Cases` | [`SwitchCaseNode.cs`](file:///FileFlow.Plugin.Logic/SwitchCaseNode.cs) |
| **BatchBufferNode** | `In` | `Out`, `Flush` | `BatchSize`, `TimeoutSeconds`, `GroupByKey` | [`BatchBufferNode.cs`](file:///FileFlow.Plugin.Logic/BatchBufferNode.cs) |
| **ThrottleRateLimitNode** | `In` | `Out` | `ItemsPerSecond`, `MaxBurst` | [`ThrottleRateLimitNode.cs`](file:///FileFlow.Plugin.Logic/ThrottleRateLimitNode.cs) |
| **ForkJoinGateNode** | `In` | `Out`, `Completed` | `ExpectedBranchesCount`, `PassThrough` | [`ForkJoinGateNode.cs`](file:///FileFlow.Plugin.Logic/ForkJoinGateNode.cs) |

---

## 3. FileFlow.Plugin.Archives (5 Nodos)

| Nodo | Entradas | Salidas | Parámetros Clave | Código Fuente |
| :--- | :--- | :--- | :--- | :--- |
| **SmartUnpackNode** | `In` | `Out`, `Error` | `OutputDirectory`, `ExtractionEngine` (Auto/7z/DotNetZip/SharpCompress) | [`SmartUnpackNode.cs`](file:///FileFlow.Plugin.Archives/SmartUnpackNode.cs) |
| **ArchiveCompressorNode** | `In` | `Out`, `Error` | `DestinationFolder`, `ArchiveFormat` (Zip/7z/Tar/Gz), `CompressionLevel` | [`ArchiveCompressorNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveCompressorNode.cs) |
| **ArchiveFilterNode** | `In` | `Archive`, `NonArchive` | `SupportedFormatsRegex` (soporte CBZ, CBR, CB7, ZIPX, TAR, 7Z, RAR) | [`ArchiveFilterNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFilterNode.cs) |
| **ArchiveFanOutNode** | `In` | `Out`, `Error` | `CleanWrapper`, `ExtractionEngine`, `CustomSevenZipPath` | [`ArchiveFanOutNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanOutNode.cs) |
| **ArchiveFanInNode** | `In` | `Out`, `Error` | `DestinationFolder` (con `{Archive:RelativeDir}`), `OutputFormat` (CBZ/ZIP/7Z) | [`ArchiveFanInNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanInNode.cs) |

---

## 4. FileFlow.Plugin.Images (3 Nodos)

| Nodo | Entradas | Salidas | Parámetros Clave | Código Fuente |
| :--- | :--- | :--- | :--- | :--- |
| **ImageOptimizerNode** | `In` | `Out`, `Error` | `TargetFormat` (WebP/JPEG/PNG), `Quality`, `KeepOriginalIfLarger`, `PassThroughNonImages` | [`ImageOptimizerNode.cs`](file:///FileFlow.Plugin.Images/ImageOptimizerNode.cs) |
| **ExifMetadataNode** | `In` | `Out`, `Error` | `ExtractGps`, `InjectMetadataPrefix`, `ExifTagsFilter` | [`ExifMetadataNode.cs`](file:///FileFlow.Plugin.Images/ExifMetadataNode.cs) |
| **ImageWatermarkNode** | `In` | `Out`, `Error` | `WatermarkPath`, `Position`, `Opacity`, `Scale` | [`ImageWatermarkNode.cs`](file:///FileFlow.Plugin.Images/ImageWatermarkNode.cs) |

---

## 5. FileFlow.Plugin.AI (10 Nodos)

| Nodo | Entradas | Salidas | Parámetros Clave | Código Fuente |
| :--- | :--- | :--- | :--- | :--- |
| **MultimodalVisionLlmNode**| `In` | `Out`, `Structured`, `Error` | `Provider` (LM Studio/Ollama/OpenAI/In-Process), `TaskPreset`, `MaxConcurrency` | [`MultimodalVisionLlmNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs) |
| **ImageTypeClassifierNode**| `In` | 9 categorías, `Out`, `Error` | `ConfidenceThreshold`, `EnableFaceDetection`, `CheckExifMetadata` | [`ImageTypeClassifierNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/ImageTypeClassifierNode.cs) |
| **SmartImageClassifierNode**| `In` | `Out`, `Error` | `ModelFamily` (CLIP ViT-B/32 / MobileNet), `ConfidenceThreshold` | [`SmartImageClassifierNode.cs`](file:///FileFlow.Plugin.AI/SmartImageClassifierNode.cs) |
| **ObjectDetectorNode** | `In` | `Out`, `Detected`, `Error` | `ModelName` (YOLOv8 / TinyYOLO), `ConfidenceThreshold`, `NmsThreshold` | [`ObjectDetectorNode.cs`](file:///FileFlow.Plugin.AI/ObjectDetectorNode.cs) |
| **PromptObjectDetectorNode**| `In` | `Out`, `Detected`, `Error` | `Prompts` (consultas libres de texto), `ModelName` (YOLO-World) | [`PromptObjectDetectorNode.cs`](file:///FileFlow.Plugin.AI/PromptObjectDetectorNode.cs) |
| **BackgroundRemoverNode** | `In` | `Out`, `Error` | `ModelName` (RMBG-1.4 / U2Net), `PostProcessMask`, `OutputFormat` | [`BackgroundRemoverNode.cs`](file:///FileFlow.Plugin.AI/BackgroundRemoverNode.cs) |
| **FaceDetectorNode** | `In` | `Out`, `FacesDetected`, `Error` | `ModelName` (UltraFace RFB-320), `BlurFaces`, `MinFaceSize` | [`FaceDetectorNode.cs`](file:///FileFlow.Plugin.AI/FaceDetectorNode.cs) |
| **SuperResolutionUpscalerNode**| `In` | `Out`, `Error` | `ModelName` (Real-ESRGAN x4), `ScaleFactor`, `DenoiseStrength` | [`SuperResolutionUpscalerNode.cs`](file:///FileFlow.Plugin.AI/SuperResolutionUpscalerNode.cs) |
| **LocalOcrNode** | `In` | `Out`, `Error` | `Language` (spa/eng), `PageSegmentationMode`, `AutoTranscodeWebP` | [`LocalOcrNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/LocalOcrNode.cs) |
| **LocalAiTranslatorNode** | `In` | `Out`, `Error` | `SourceLanguage`, `TargetLanguage`, `ModelFamily` (MarianMT/NLLB-200) | [`LocalAiTranslatorNode.cs`](file:///FileFlow.Plugin.AI/LocalAiTranslatorNode.cs) |

---

## 6. FileFlow.Plugin.Data, Documents, Audio, Video, Network, Scripting & Integrations

| Plugin / Nodo | Entradas | Salidas | Propósito Principal | Código Fuente |
| :--- | :--- | :--- | :--- | :--- |
| **ExcelReportGeneratorNode** (Data) | `In` | `Out`, `Report` | Genera reportes tabulares `.xlsx` / `.csv` formateados | [`ExcelReportGeneratorNode.cs`](file:///FileFlow.Plugin.Data/ExcelReportGeneratorNode.cs) |
| **SqliteDatabaseSinkNode** (Data) | `In` | `Out`, `Error` | Ingesta de metadatos en tablas SQLite con transacciones | [`SqliteDatabaseSinkNode.cs`](file:///FileFlow.Plugin.Data/SqliteDatabaseSinkNode.cs) |
| **PdfMergeNode** (Documents) | `In` | `Out`, `Merged`, `Error` | Unión secuencial de documentos PDF | [`PdfMergeNode.cs`](file:///FileFlow.Plugin.Documents/PdfMergeNode.cs) |
| **AudioTranscoderNode** (Audio) | `In` | `Out`, `Error` | Transcodificación de audio (MP3, FLAC, AAC, WAV) | [`AudioTranscoderNode.cs`](file:///FileFlow.Plugin.Audio/AudioTranscoderNode.cs) |
| **VideoTranscoderNode** (Video) | `In` | `Out`, `Error` | Transcodificación FFmpeg (MP4, MKV, WebM, H.264/H.265) | [`VideoTranscoderNode.cs`](file:///FileFlow.Plugin.Video/VideoTranscoderNode.cs) |
| **RemoteUploadNode** (Network) | `In` | `Out`, `Error` | Subida vía SFTP, FTP, WebDAV, SMB o REST HTTP | [`RemoteUploadNode.cs`](file:///FileFlow.Plugin.Network/RemoteUploadNode.cs) |
| **RoslynScriptNode** (Scripting) | `In` | `Out`, `Error` | Ejecución segura de scripts dinámicos en C# 13 con compilador Roslyn | [`RoslynScriptNode.cs`](file:///FileFlow.Plugin.Scripting/RoslynScriptNode.cs) |
| **CliExecutionNode** (Integrations) | `In` | `Out`, `Error` | Invocación de herramientas de línea de comandos de terceros | [`CliExecutionNode.cs`](file:///FileFlow.Plugin.Integrations/CliExecutionNode.cs) |
| **WebhookNotificationNode** (Integrations)| `In` | `Out`, `Error` | Envío de notificaciones HTTP POST / JSON (Discord, Slack, REST) | [`WebhookNotificationNode.cs`](file:///FileFlow.Plugin.Integrations/WebhookNotificationNode.cs) |
| **HashCalculatorNode** (Hashing) | `In` | `Out`, `Error` | Cálculo de sumas criptográficas (MD5, SHA-1, SHA-256, SHA-512) | [`HashCalculatorNode.cs`](file:///FileFlow.Plugin.Hashing/HashCalculatorNode.cs) |