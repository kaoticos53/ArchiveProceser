# Catálogo de Nodos de FileFlow Studio

**70 nodos de producción**, generados desde el código. Este documento no se edita a mano:
lo produce `NodeCatalogDocument` a partir del catálogo que descubre el cargador de la aplicación
—el mismo camino que ejecuta la app al arrancar— y `NodeCatalogGuardTests` falla si deja de coincidir con él.

Para regenerarlo tras añadir, quitar o cambiar un nodo:

```bash
FILEFLOW_UPDATE_NODE_CATALOG=1 dotnet test --filter NodeCatalogGuardTests
```

> Especificación extendida, con descripción de cada parámetro: [`2026-09-13_nodes_catalog_full.md`](file:///docs/history/2026-09-13_nodes_catalog_full.md).

---

## 1. FileFlow.Plugin.AI (18 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BackgroundRemoverNode** | ImageVision | `In` | `Out`, `Bypass`, `Mask`, `Error` | `Model` (Dropdown), `OutputMode` (Dropdown), `BackgroundColor` (Text), `OutputDirectory` (FolderPath), `SkipIfExists` (Toggle) | [`BackgroundRemoverNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/BackgroundRemoverNode.cs) |
| **ContentModerationFilterNode** | Security | `In` | `Safe`, `Sensitive`, `Error` | `Model` (Dropdown), `SensitivityThreshold` (Slider) | [`ContentModerationFilterNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/ContentModerationFilterNode.cs) |
| **FaceDetectorNode** | ImageVision | `In` | `FacesFound`, `NoFaces` | `Model` (Dropdown), `ConfidenceThreshold` (Slider), `MinimumFaces` (Number) | [`FaceDetectorNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/FaceDetectorNode.cs) |
| **ImageTypeClassifierNode** | ImageVision | `In` | `Document`, `Receipt`, `Portrait`, `GroupPhoto`, `Photo`, `Screenshot`, `Illustration`, `IDCard`, `Other`, `Out`, `Error` | `ConfidenceThreshold` (Slider), `EnableFaceDetection` (Toggle), `CheckExifMetadata` (Toggle) | [`ImageTypeClassifierNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/ImageTypeClassifierNode.cs) |
| **LocalAiTranslatorNode** | LanguageAI | `In` | `Translated`, `Error` | `Model` (Dropdown), `SourceLanguage` (Dropdown), `TargetLanguage` (Dropdown), `InputSource` (Dropdown), `MetadataKeyName` (Text), `OutputMode` (Dropdown), `TargetFileNamePattern` (Text), `TranslateSrtTimestamps` (Toggle) | [`LocalAiTranslatorNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/LocalAiTranslatorNode.cs) |
| **LocalLlmProcessorNode** | LanguageAI | `In` | `Processed`, `Error` | `Model` (Dropdown), `TaskType` (Dropdown), `SystemPrompt` (MultiLineText), `UserPrompt` (MultiLineText), `OutputFormat` (Dropdown), `SaveAsNewFile` (Toggle), `Temperature` (Slider), `MaxTokens` (Number) | [`LocalLlmProcessorNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/LocalLlmProcessorNode.cs) |
| **LocalOcrNode** | Documents | `In` | `Out`, `Error` | `Language` (Dropdown), `EngineMode` (Dropdown) | [`LocalOcrNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/LocalOcrNode.cs) |
| **LocalWhisperTranscriberNode** | AudioVoice | `In` | `Out`, `Error` | `ModelSize` (Dropdown), `Language` (Dropdown), `GenerateSrtSubtitles` (Toggle), `OutputDirectory` (FolderPath) | [`LocalWhisperTranscriberNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Audio/LocalWhisperTranscriberNode.cs) |
| **MultimodalVisionLlmNode** | LanguageAI | `In` | `Out`, `Structured`, `Error` | `Provider` (Dropdown), `TaskPreset` (Dropdown), `TargetLanguage` (EditableDropdown), `AdditionalPrompt` (MultiLineText), `MaxConcurrency` (Number) | [`MultimodalVisionLlmNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs) |
| **ObjectDetectorNode** | ImageVision | `In` | `Out`, `Error` | `Model` (Dropdown), `MinimumConfidence` (Slider), `FilterLabel` (Text), `MaxDetections` (Number) | [`ObjectDetectorNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/ObjectDetectorNode.cs) |
| **PiiAnonymizerNode** | Security | `In` | `Clean`, `SensitiveFound`, `Out`, `Error` | `Model` (Dropdown), `AnonymizationMode` (Dropdown), `FilterDniNie` (Toggle), `FilterIban` (Toggle), `FilterCreditCards` (Toggle), `FilterEmails` (Toggle), `FilterPhones` (Toggle), `FilterIpAddresses` (Toggle), `FilterPersonNames` (Toggle), `OutputDirectory` (FolderPath), `SkipIfExists` (Toggle) | [`PiiAnonymizerNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/PiiAnonymizerNode.cs) |
| **PromptObjectDetectorNode** | ImageVision | `In` | `ObjectsFound`, `NoObjects`, `Error` | `Prompt` (MultiLineText), `MinimumConfidence` (Slider), `AutoTranslateToEnglish` (Toggle), `MaxDetections` (Number) | [`PromptObjectDetectorNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/PromptObjectDetectorNode.cs) |
| **PromptTransformerNode** | LanguageAI | `In` | `Transformed`, `Error` | `PromptTemplate` (MultiLineText), `TargetLanguage` (Dropdown), `ExpandSynonyms` (Toggle) | [`PromptTransformerNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/PromptTransformerNode.cs) |
| **SmartImageClassifierNode** | ImageVision | `In` | `Out`, `Error` | `Model` (Dropdown), `MinimumConfidence` (Slider), `FallbackCategory` (Text) | [`SmartImageClassifierNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/SmartImageClassifierNode.cs) |
| **SuperResolutionUpscalerNode** | ImageVision | `In` | `Out`, `Skipped`, `Error` | `Model` (Dropdown), `ScaleFactor` (Dropdown), `MaxInputDimension` (Number), `OutputDirectory` (FolderPath), `SkipIfExists` (Toggle) | [`SuperResolutionUpscalerNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Vision/SuperResolutionUpscalerNode.cs) |
| **TextToSpeechNode** | AudioVoice | `In` | `Out`, `Error` | `Model` (Dropdown), `InputSource` (Dropdown), `MetadataKeyName` (Text), `CustomTextTemplate` (MultiLineText), `SpeechRate` (Slider), `OutputDirectory` (FolderPath), `SkipIfExists` (Toggle) | [`TextToSpeechNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Audio/TextToSpeechNode.cs) |
| **VoiceActivityDetectorNode** | AudioVoice | `In` | `Speech`, `Silent`, `Out`, `Error` | `Model` (Dropdown), `Mode` (Dropdown), `SensitivityThreshold` (Slider), `MinSpeechDurationMs` (Number), `PaddingDurationMs` (Number), `OutputDirectory` (FolderPath), `SkipIfExists` (Toggle) | [`VoiceActivityDetectorNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Audio/VoiceActivityDetectorNode.cs) |
| **ZeroShotSemanticSearchNode** | LanguageAI | `In` | `Matched`, `Unmatched`, `Out`, `Error` | `Model` (Dropdown), `SearchQuery` (Text), `CandidateLabels` (MultiLineText), `SimilarityThreshold` (Slider), `TopK` (Number) | [`ZeroShotSemanticSearchNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/ZeroShotSemanticSearchNode.cs) |

---

## 2. FileFlow.Plugin.Archives (5 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **ArchiveCompressorNode** | Archives | `In` | `Out`, `Error` | `DestinationFolder` (FolderPath), `ArchiveName` (Text), `ArchiveFormat` (Dropdown), `CompressionType` (Dropdown) | [`ArchiveCompressorNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveCompressorNode.cs) |
| **ArchiveFanInNode** | Archives | `In` | `Out`, `Error` | `DestinationFolder` (FolderPath), `ArchiveName` (Text), `ArchiveFormat` (Dropdown), `CompressionType` (Dropdown), `CleanWorkingFolder` (Toggle), `TimeoutSeconds` (Number) | [`ArchiveFanInNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanInNode.cs) |
| **ArchiveFanOutNode** | Archives | `In` | `Out` | `OutputDirectory` (FolderPath), `ArchiveFormat` (Dropdown), `PreserveDirectoryStructure` (Toggle), `FilterPattern` (Text), `PasswordList` (Text), `PasswordFile` (FilePath) | [`ArchiveFanOutNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanOutNode.cs) |
| **ArchiveFilterNode** | Archives | `In` | `Archive`, `RegularFile`, `SecondaryVolume` | — | [`ArchiveFilterNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFilterNode.cs) |
| **SmartUnpackNode** | Archives | `In` | `Out`, `Error` | `OutputDirectory` (FolderPath), `ArchiveFormat` (Dropdown), `PreserveDirectoryStructure` (Toggle), `FilterPattern` (Text), `PasswordList` (Text), `CleanRedundantFolder` (Toggle), `DeleteArchiveAfterExtraction` (Toggle), `PasswordFile` (FilePath) | [`SmartUnpackNode.cs`](file:///FileFlow.Plugin.Archives/SmartUnpackNode.cs) |

---

## 3. FileFlow.Plugin.Data (7 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CsvExportNode** | Data | `In` | `Out` | `DestinationPath` (FilePath), `Delimiter` (Dropdown), `Columns` (Text), `AppendMode` (Toggle) | [`CsvExportNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Exporters/CsvExportNode.cs) |
| **CsvReaderNode** | Data | `In` | `RowOut` | `FilePath` (FilePath), `Delimiter` (Dropdown), `Encoding` (Dropdown), `HasHeader` (Toggle) | [`CsvReaderNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Readers/CsvReaderNode.cs) |
| **DataFormatConverterNode** | Data | `In` | `Out` | `TargetFormat` (Dropdown), `OutputDirectory` (FolderPath) | [`DataFormatConverterNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Processing/DataFormatConverterNode.cs) |
| **DataLookupNode** | Data | `In` | `Matched`, `Unmatched` | `DataSourcePath` (FilePath), `LookupKeyColumn` (Text), `MatchExpression` (Text), `PrefixColumns` (Text) | [`DataLookupNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Processing/DataLookupNode.cs) |
| **ExcelReaderNode** | Data | `In` | `RowOut` | `FilePath` (FilePath), `SheetName` (Text), `SkipEmptyRows` (Toggle) | [`ExcelReaderNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Readers/ExcelReaderNode.cs) |
| **ExcelReportGeneratorNode** | Data | `In` | `Out`, `Report` | `OutputDirectory` (FolderPath), `ReportFileName` (Text), `ColumnsToExport` (Text) | [`ExcelReportGeneratorNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Exporters/ExcelReportGeneratorNode.cs) |
| **SqliteDatabaseSinkNode** | Data | `In` | `Out` | `DatabasePath` (FilePath), `TableName` (Text), `AutoCreateTable` (Toggle), `StoreMetadataAsJson` (Toggle) | [`SqliteDatabaseSinkNode.cs`](file:///FileFlow.Plugin.Data/Nodes/Exporters/SqliteDatabaseSinkNode.cs) |

---

## 4. FileFlow.Plugin.Documents (4 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **PdfMergeNode** | Documents | `In` | `Out`, `PassThrough` | `OutputDirectory` (FolderPath), `OutputFileName` (Text) | [`PdfMergeNode.cs`](file:///FileFlow.Plugin.Documents/PdfMergeNode.cs) |
| **PdfMetadataNode** | Documents | `In` | `Out` | `UpdateMetadata` (Toggle), `Title` (Text), `Author` (Text), `Subject` (Text), `Keywords` (Text), `OutputDirectory` (FolderPath) | [`PdfMetadataNode.cs`](file:///FileFlow.Plugin.Documents/PdfMetadataNode.cs) |
| **PdfSplitNode** | Documents | `In` | `Out`, `Original` | `OutputDirectory` (FolderPath), `FileNamePattern` (Text) | [`PdfSplitNode.cs`](file:///FileFlow.Plugin.Documents/PdfSplitNode.cs) |
| **PdfTextExtractorNode** | Documents | `In` | `Out`, `TextFile` | `ExportTextFile` (Toggle), `OutputDirectory` (FolderPath) | [`PdfTextExtractorNode.cs`](file:///FileFlow.Plugin.Documents/PdfTextExtractorNode.cs) |

---

## 5. FileFlow.Plugin.FileSystem (13 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **AdvancedRenamerNode** | Files | `In` | `Out` | `PipelineName` (Dropdown), `RenameMode` (Dropdown), `CollisionStrategy` (Dropdown) | [`AdvancedRenamerNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/AdvancedRenamerNode.cs) |
| **DestinationSinkNode** | Files | `In` | `Done`, `Error` | `DestinationRoot` (FolderPath), `ConflictStrategy` (Dropdown) | [`DestinationSinkNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Actions/DestinationSinkNode.cs) |
| **DirectoryInspectorNode** | Files | `In` | `SingleArchive`, `MixedContent`, `DirectoriesOnly` | — | [`DirectoryInspectorNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/DirectoryInspectorNode.cs) |
| **DocumentProcessorNode** | Documents | `In` | `Out`, `Error` | — | [`DocumentProcessorNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/DocumentProcessorNode.cs) |
| **EmptyDirectoryCleanerNode** | Files | `TriggerIn` | `Out`, `Error` | — | [`EmptyDirectoryCleanerNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs) |
| **FileRelocatorNode** | Files | `In` | `Out`, `Error` | `SourcePath` (FileVersionSelector), `Operation` (Dropdown), `DestinationDirectory` (FolderPath), `VerifyIntegrity` (Toggle), `CreateDirectories` (Toggle), `CleanupSource` (Toggle) | [`FileRelocatorNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Actions/FileRelocatorNode.cs) |
| **FolderSourceNode** | Files | — | `Out` | `SourcePath` (FolderPath), `ExtensionFilter` (Text), `Recursive` (Toggle), `EmitMode` (Dropdown), `MaxRecursionDepth` (Number), `WatchRealtime` (Toggle) | [`FolderSourceNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Sources/FolderSourceNode.cs) |
| **LogOutputNode** | Integrations | `In` | `Out` | `CustomMessage` (MultiLineText), `LogLevel` (Dropdown), `LogMetadata` (Toggle), `LogExecutionHistory` (Toggle), `CompactFormat` (Toggle) | [`LogOutputNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/LogOutputNode.cs) |
| **OperationReportNode** | Integrations | `In` | `Out`, `Report`, `Error` | `ReportFormat` (Dropdown), `ReportScope` (Dropdown), `GroupBy` (Dropdown), `ReportFileName` (Text), `Theme` (Dropdown), `AutoOpenReport` (Toggle), `IncludeMetadata` (Toggle) | [`OperationReportNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/OperationReportNode.cs) |
| **OriginalFileActionNode** | Files | `In` | `Out`, `Error` | `ActionType` (Dropdown), `QuarantinePath` (FolderPath) | [`OriginalFileActionNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Actions/OriginalFileActionNode.cs) |
| **SafeRecycleDeleteNode** | Files | `In` | `Deleted`, `Error` | — | [`SafeRecycleDeleteNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Actions/SafeRecycleDeleteNode.cs) |
| **SyntheticDataSourceNode** | Files | — | `Out` | `Category` (Dropdown), `EmissionMode` (Dropdown), `MaxItems` (Number), `EmissionDelayMs` (Number), `EmitDirectories` (Toggle), `CustomItems` (MultiLineText) | [`SyntheticDataSourceNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs) |
| **VariableInjectorNode** | Integrations | `In` | `Out` | — | [`VariableInjectorNode.cs`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/VariableInjectorNode.cs) |

---

## 6. FileFlow.Plugin.Hashing (2 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **DeduplicationFilterNode** | Security | `In` | `Unique`, `Duplicate`, `Error` | — | [`DeduplicationFilterNode.cs`](file:///FileFlow.Plugin.Hashing/DeduplicationFilterNode.cs) |
| **HashCalculatorNode** | Security | `In` | `Out`, `Error` | `Algorithm` (Dropdown), `StoreInMetadataKey` (Text) | [`HashCalculatorNode.cs`](file:///FileFlow.Plugin.Hashing/HashCalculatorNode.cs) |

---

## 7. FileFlow.Plugin.Images (2 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **ExifMetadataNode** | ImageVision | `In` | `Out` | — | [`ExifMetadataNode.cs`](file:///FileFlow.Plugin.Images/ExifMetadataNode.cs) |
| **ImageOptimizerNode** | ImageVision | `In` | `Out`, `Error` | `Width` (Text), `Height` (Text), `TargetFormat` (Dropdown), `Quality` (Slider), `OnlyDownscale` (Toggle), `OutputDirectory` (FolderPath), `FileNameSuffix` (Text), `KeepOriginalIfLarger` (Toggle), `ReplaceOriginalInPlace` (Toggle), `PassThroughNonImages` (Toggle) | [`ImageOptimizerNode.cs`](file:///FileFlow.Plugin.Images/ImageOptimizerNode.cs) |

---

## 8. FileFlow.Plugin.Integrations (3 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CliExecutionNode** | Integrations | `In` | `Success`, `Failed` | — | [`CliExecutionNode.cs`](file:///FileFlow.Plugin.Integrations/CliExecutionNode.cs) |
| **MediaTranscoderNode** | AudioVoice | `In` | `Out`, `Error` | `Preset` (Dropdown), `OutputExtension` (Text), `OutputFolder` (FolderPath), `CustomArguments` (Text), `CustomFfmpegPath` (FilePath), `HardwareAcceleration` (Dropdown) | [`MediaTranscoderNode.cs`](file:///FileFlow.Plugin.Integrations/MediaTranscoderNode.cs) |
| **WebhookNotificationNode** | Integrations | `In` | `Out`, `Failed` | — | [`WebhookNotificationNode.cs`](file:///FileFlow.Plugin.Integrations/WebhookNotificationNode.cs) |

---

## 9. FileFlow.Plugin.Logic (10 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BatchBufferNode** | Logic | `ItemIn`, `ForceFlush` | `ItemOut`, `BatchCompleted` | — | [`BatchBufferNode.cs`](file:///FileFlow.Plugin.Logic/BatchBufferNode.cs) |
| **BestVersionSelectorNode** | Logic | `In` | `Out`, `WonA`, `WonB` | `CandidateA` (FileVersionSelector), `CandidateB` (FileVersionSelector), `Criterion` (Dropdown), `Threshold` (Number), `DiscardLoser` (Toggle), `SetWinnerAsCurrent` (Toggle) | [`BestVersionSelectorNode.cs`](file:///FileFlow.Plugin.Logic/BestVersionSelectorNode.cs) |
| **ExpressionFilterNode** | Logic | `In` | `True`, `False` | — | [`ExpressionFilterNode.cs`](file:///FileFlow.Plugin.Logic/ExpressionFilterNode.cs) |
| **FileForkNode** | Logic | `In` | `Original`, `Current`, `Version` | `ForkOriginal` (Toggle), `ForkCurrent` (Toggle), `ForkAllVersions` (Toggle) | [`FileForkNode.cs`](file:///FileFlow.Plugin.Logic/FileForkNode.cs) |
| **ForkJoinBarrierNode** | Logic | `In`, `Branch1_Done`, `Branch2_Done` | `Fork1`, `Fork2`, `AllCompleted` | — | [`ForkJoinBarrierNode.cs`](file:///FileFlow.Plugin.Logic/ForkJoinBarrierNode.cs) |
| **IntermediateCleanupNode** | Logic | `In` | `Out` | — | [`IntermediateCleanupNode.cs`](file:///FileFlow.Plugin.Logic/IntermediateCleanupNode.cs) |
| **SwitchActiveFileNode** | Logic | `In` | `Out` | `TargetFile` (FileVersionSelector), `DeleteCurrentFileFirst` (Toggle) | [`SwitchActiveFileNode.cs`](file:///FileFlow.Plugin.Logic/SwitchActiveFileNode.cs) |
| **SwitchCaseNode** | Logic | `In` | `Case 1`, `Default` | `Expression` (Text) | [`SwitchCaseNode.cs`](file:///FileFlow.Plugin.Logic/SwitchCaseNode.cs) |
| **ThrottleDelayNode** | Logic | `In` | `Out` | — | [`ThrottleDelayNode.cs`](file:///FileFlow.Plugin.Logic/ThrottleDelayNode.cs) |
| **VersionRouterNode** | Logic | `In` | `True`, `False` | `Property` (Text), `Operator` (Dropdown), `ComparisonValue` (Text), `TrueFile` (FileVersionSelector), `FalseFile` (FileVersionSelector), `PurgeUnselectedTemps` (Toggle) | [`VersionRouterNode.cs`](file:///FileFlow.Plugin.Logic/VersionRouterNode.cs) |

---

## 10. FileFlow.Plugin.Network (2 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **NetworkDownloadNode** | Network | `In` | `Out`, `Error` | `Protocol` (Dropdown), `SourceUrl` (Text), `TimeoutSeconds` (Number), `Host` (Text), `Port` (Number), `Username` (Text), `Password` (Text), `RemoteFilePath` (Text), `Encryption` (Dropdown), `PassiveMode` (Toggle), `AuthMethod` (Dropdown), `PrivateKeyPath` (FilePath), `PrivateKeyPassphrase` (Text), `ServerUrl` (Text), `UncPath` (Text), `Domain` (Text), `DestinationFolder` (FolderPath), `FileName` (Text), `Overwrite` (Toggle), `DeleteAfterDownload` (Toggle) | [`NetworkDownloadNode.cs`](file:///FileFlow.Plugin.Network/NetworkDownloadNode.cs) |
| **NetworkUploadNode** | Network | `In` | `Out`, `Error` | `Protocol` (Dropdown), `TargetUrl` (Text), `HttpMethod` (Dropdown), `AuthHeader` (Text), `Host` (Text), `Port` (Number), `Username` (Text), `Password` (Text), `RemoteDirectory` (Text), `Encryption` (Dropdown), `PassiveMode` (Toggle), `AuthMethod` (Dropdown), `PrivateKeyPath` (FilePath), `PrivateKeyPassphrase` (Text), `ServerUrl` (Text), `UncPath` (Text), `Domain` (Text) | [`NetworkUploadNode.cs`](file:///FileFlow.Plugin.Network/NetworkUploadNode.cs) |

---

## 11. FileFlow.Plugin.Scripting (1 nodo)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CustomScriptNode** | Logic | `In` | `Out` | `Language` (Dropdown), `TimeoutSeconds` (Number), `InputPorts` (Text), `OutputPorts` (Text) | [`CustomScriptNode.cs`](file:///FileFlow.Plugin.Scripting/CustomScriptNode.cs) |

---

## 12. FileFlow.Plugin.Subflows (3 nodos)

| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **SubflowInputNode** | Subflows | `In` | `In` | `PortNames` (Text) | [`SubflowInputNode.cs`](file:///FileFlow.Plugin.Subflows/SubflowInputNode.cs) |
| **SubflowNode** | Subflows | `In` | `Out` | `SubflowPath` (FilePath), `EmbedDefinition` (Toggle), `SubflowName` (Text) | [`SubflowNode.cs`](file:///FileFlow.Plugin.Subflows/SubflowNode.cs) |
| **SubflowOutputNode** | Subflows | `Out` | `Out` | `PortNames` (Text) | [`SubflowOutputNode.cs`](file:///FileFlow.Plugin.Subflows/SubflowOutputNode.cs) |

