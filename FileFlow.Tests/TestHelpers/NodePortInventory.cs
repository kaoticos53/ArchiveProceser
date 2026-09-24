using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// <b>El inventario de puertos del producto</b>: <i>cada</i> puerto de salida que declara cada nodo —los de
/// rama (<c>Error</c>, <c>Skipped</c>, <c>Failed</c>) y los del camino feliz— con la prueba que lo cubre y el
/// grado en que esa prueba lo cubre.
///
/// <para><b>Por qué existe</b>: un puerto es la mitad del contrato de un nodo, y el camino feliz no se vigila
/// solo. Las ramas se esconden de la vista —el hito 188 encontró tres nodos que emitían por puertos que no
/// declaraban, es decir que cortaban el flujo en silencio—, pero un <b>puerto principal sin prueba</b> es el
/// otro extremo del mismo agujero: un nodo que se puede arrastrar al lienzo, cablear y ejecutar sin que nada
/// haya ejecutado nunca su salida. El hito 192 cerró los dos huecos de rama que quedaban; este inventario
/// generaliza la pregunta a todos los puertos y convierte «no lo he probado» en un dato escrito, por puerto.</para>
///
/// <para><b>Los tres grados</b> (<see cref="Coverage"/>) no son una escala de opinión: cada uno se puede
/// comprobar sobre el texto del suite con <see cref="PortWitnessIndex"/>.</para>
///
/// <list type="number">
/// <item><description><see cref="Coverage.ByNamedTest"/>: hay un caso que <b>habla del nodo y cita el nombre
/// del puerto</b> (<c>"Out"</c>, <c>"Error"</c>). Es como una prueba dice por dónde sale el ítem.</description></item>
/// <item><description><see cref="Coverage.ByExecutingTest"/>: hay casos que <b>ejecutan el nodo</b> pero
/// ninguno nombra ese puerto. El puerto está bajo un nodo probado, no ejecutado por su nombre: se declara así
/// en vez de fingir la evidencia fuerte.</description></item>
/// <item><description><see cref="Coverage.WithoutExecution"/>: <b>nadie ejecuta el nodo</b>. Se declara con el
/// motivo en vez de dejar la casilla vacía, y el motivo tiene que explicar qué haría falta.</description></item>
/// </list>
///
/// <para><b>Qué vigila la guardia</b> (<c>NodePortCoverageGuardTests</c>): que todo puerto declarado o emitido
/// esté en el inventario, que ningún asiento apunte a un puerto que el árbol ya no declara, que el testigo
/// citado exista y verifique lo que el grado promete, y que un grado no se quede anticuado —si aparece un caso
/// que nombra el puerto, un asiento <c>ByExecutingTest</c> deja de ser cierto—. Un puerto nuevo en el producto
/// <b>rompe el suite</b> hasta declararse, que es lo que impide que el censo caduque.</para>
/// </summary>
public static class NodePortInventory
{
    /// <summary>
    /// Nombres de puerto que el producto usa para <b>ramas</b> (lo que salió mal o se omitió) y no para el
    /// camino feliz. No decide qué se vigila —se vigilan todos los puertos— sino cómo se lee cada asiento: un
    /// puerto de rama sin prueba es el agujero del hito 188, y uno del camino feliz es un nodo que nadie ha
    /// puesto a trabajar.
    /// </summary>
    public static IReadOnlySet<string> BranchPortNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Error",
        "Skipped",
        "Failed"
    };

    /// <summary>Hasta dónde llega la evidencia de un puerto.</summary>
    public enum Coverage
    {
        /// <summary>Un caso del suite habla del nodo y cita el nombre del puerto como literal.</summary>
        ByNamedTest,

        /// <summary>
        /// Hay casos que ejecutan el nodo, pero ninguno nombra este puerto: el puerto viaja bajo un nodo probado
        /// sin que ninguna prueba diga por dónde sale.
        /// </summary>
        ByExecutingTest,

        /// <summary>
        /// Ninguna prueba ejecuta el nodo. Se declara con el motivo —qué haría falta para cubrirlo— en vez de
        /// dejar la casilla vacía.
        /// </summary>
        WithoutExecution
    }

    /// <summary>
    /// Un puerto declarado. <paramref name="Evidence"/> es el nombre del método de prueba que lo cubre
    /// (<see cref="Coverage.ByNamedTest"/> y <see cref="Coverage.ByExecutingTest"/>) o el motivo por el que
    /// nadie lo ejecuta (<see cref="Coverage.WithoutExecution"/>).
    /// </summary>
    public sealed record Entry(string NodeClass, string Port, Coverage Coverage, string Evidence)
    {
        /// <summary>Clave con la que se ata la entrada al árbol: el par nodo+puerto.</summary>
        public string Key => $"{NodeClass}.{Port}";

        /// <summary>¿Es un puerto de rama?</summary>
        public bool IsBranch => BranchPortNames.Contains(Port);

        /// <summary>Texto para el mensaje de la guardia.</summary>
        public string Describe() => Coverage switch
        {
            Coverage.ByNamedTest => $"{Key} ← {Evidence}",
            Coverage.ByExecutingTest => $"{Key} ≈ {Evidence} (del nodo; ninguna prueba nombra el puerto)",
            _ => $"{Key} (sin nadie que lo ejecute: {Evidence})"
        };
    }

    /// <summary>
    /// <b>El censo</b>: un asiento por cada puerto de salida que el producto declara, con la prueba que lo
    /// cubre y el grado en que esa prueba lo cubre.
    ///
    /// <para>Se lee así: <c>ByNamedTest</c> es un caso que nombra el puerto (<c>"Out"</c>, <c>"Error"</c>), la
    /// evidencia más fuerte que se puede leer del texto; <c>ByExecutingTest</c> es un caso que ejecuta el nodo
    /// pero no nombra ese puerto; <c>WithoutExecution</c> es un puerto sin nadie que lo ejecute, declarado con
    /// el motivo. Un puerto puede declarar <b>más de un testigo</b> cuando tiene más de un camino cubierto, y
    /// cada testigo citado tiene que verificar por su cuenta.</para>
    /// </summary>
    public static IReadOnlyList<Entry> Entries { get; } =
    [
        // ── Inteligencia artificial (visión, audio, lenguaje) (FileFlow.Plugin.AI) ─────────────────────────────────
        new("BackgroundRemoverNode", "Bypass", Coverage.ByNamedTest,
            "BackgroundRemoverNode_ShouldHaveValidPortsAndParameters"),
        new("BackgroundRemoverNode", "Error", Coverage.ByNamedTest,
            "BackgroundRemoverNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError"),
        new("BackgroundRemoverNode", "Mask", Coverage.ByNamedTest,
            "BackgroundRemoverNode_ShouldHaveValidPortsAndParameters"),
        new("BackgroundRemoverNode", "Out", Coverage.ByNamedTest,
            "BackgroundRemoverNode_ExecuteAsync_WhenOutputFileExistsAndSkipIfExistsTrue_ShouldBypassInferenceAndEmitOut"),
        new("ContentModerationFilterNode", "Error", Coverage.ByNamedTest,
            "ContentModerationFilterNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError"),
        new("ContentModerationFilterNode", "Safe", Coverage.ByNamedTest,
            "ContentModerationFilterNode_ShouldHaveValidPortsAndParameters"),
        new("ContentModerationFilterNode", "Sensitive", Coverage.ByNamedTest,
            "ContentModerationFilterNode_ShouldHaveValidPortsAndParameters"),
        new("FaceDetectorNode", "FacesFound", Coverage.ByNamedTest,
            "FaceDetectorNode_ProcessesImageAndEmitsBranch"),
        new("FaceDetectorNode", "NoFaces", Coverage.ByNamedTest,
            "FaceDetectorNode_ProcessesImageAndEmitsBranch"),
        new("ImageTypeClassifierNode", "Document", Coverage.ByNamedTest,
            "ExecuteAsync_WithSyntheticDocument_ShouldClassifyAsDocument"),
        new("ImageTypeClassifierNode", "Error", Coverage.ByNamedTest,
            "AnAiVisionNodeThatCannotFindItsInput_ShouldLeaveByTheDeclaredErrorPort"),
        new("ImageTypeClassifierNode", "GroupPhoto", Coverage.ByNamedTest,
            "ImageTypeClassifierNode_ShouldHaveExpectedPortsAndParameters"),
        new("ImageTypeClassifierNode", "IDCard", Coverage.ByNamedTest,
            "ImageTypeClassifierNode_ShouldHaveExpectedPortsAndParameters"),
        new("ImageTypeClassifierNode", "Illustration", Coverage.ByNamedTest,
            "ImageTypeClassifierNode_ShouldHaveExpectedPortsAndParameters"),
        new("ImageTypeClassifierNode", "Other", Coverage.ByNamedTest,
            "ExecuteAsync_WhenConfidenceBelowThreshold_ShouldEmitOther"),
        new("ImageTypeClassifierNode", "Out", Coverage.ByNamedTest,
            "ExecuteAsync_WhenConfidenceBelowThreshold_ShouldEmitOther"),
        new("ImageTypeClassifierNode", "Photo", Coverage.ByNamedTest,
            "ImageTypeClassifierNode_ShouldHaveExpectedPortsAndParameters"),
        new("ImageTypeClassifierNode", "Portrait", Coverage.ByNamedTest,
            "ImageTypeClassifierNode_ShouldHaveExpectedPortsAndParameters"),
        new("ImageTypeClassifierNode", "Receipt", Coverage.ByNamedTest,
            "ExecuteAsync_WithSyntheticReceipt_ShouldClassifyAsReceipt"),
        new("ImageTypeClassifierNode", "Screenshot", Coverage.ByNamedTest,
            "ExecuteAsync_WithSyntheticScreenshot_ShouldClassifyAsScreenshot"),
        new("LocalAiTranslatorNode", "Error", Coverage.ByNamedTest,
            "ExecuteAsync_WhenFileNotFound_ShouldEmitError"),
        new("LocalAiTranslatorNode", "Translated", Coverage.ByNamedTest,
            "ExecuteAsync_WithCreateNewFileMode_ShouldWriteOutputFile"),
        new("LocalLlmProcessorNode", "Error", Coverage.ByExecutingTest,
            "LocalLlmProcessorNode_ParametersAndDescriptors_ShouldSupportOfficialModels"),
        new("LocalLlmProcessorNode", "Processed", Coverage.ByNamedTest,
            "ExecuteAsync_ExtractStructuredData_ShouldGenerateValidJsonWithEntities"),
        new("LocalOcrNode", "Error", Coverage.WithoutExecution,
            "Ninguna prueba ejecuta el nodo: la única mención del suite es la tabla de iconos, que enumera tipos de nodo sin ponerlos a trabajar. El puerto se declara con su motivo hasta que exista un caso que cargue un modelo de OCR y emita por él."),
        new("LocalOcrNode", "Out", Coverage.WithoutExecution,
            "Ninguna prueba ejecuta el nodo: la única mención del suite es la tabla de iconos, que enumera tipos de nodo sin ponerlos a trabajar. El puerto se declara con su motivo hasta que exista un caso que cargue un modelo de OCR y emita por él."),
        new("LocalWhisperTranscriberNode", "Error", Coverage.ByNamedTest,
            "LocalWhisperTranscriberNode_HandlesInvalidAudioGracefully"),
        new("LocalWhisperTranscriberNode", "Out", Coverage.ByNamedTest,
            "LocalWhisperTranscriberNode_HandlesInvalidAudioGracefully"),
        new("MultimodalVisionLlmNode", "Error", Coverage.ByNamedTest,
            "AnAiVisionNodeThatCannotFindItsInput_ShouldLeaveByTheDeclaredErrorPort"),
        new("MultimodalVisionLlmNode", "Out", Coverage.ByNamedTest,
            "ExecuteAsync_WhenServerReturnsTransient500_ShouldRetryAndSucceed"),
        new("MultimodalVisionLlmNode", "Structured", Coverage.ByNamedTest,
            "ExecuteAsync_WithInProcessProvider_ClassifyAndTag_ShouldProduceCategoryAndTags"),
        new("ObjectDetectorNode", "Error", Coverage.ByNamedTest,
            "PromptObjectDetectorNode_ExecuteAsync_NonExistentFile_ShouldEmitToError"),
        new("ObjectDetectorNode", "Out", Coverage.ByNamedTest,
            "ObjectDetectorNode_EmitsOutAndInjectsMetadataOrPasses"),
        new("PiiAnonymizerNode", "Clean", Coverage.ByNamedTest,
            "PiiAnonymizerNode_ExecuteAsync_WithCleanData_ShouldEmitClean"),
        new("PiiAnonymizerNode", "Error", Coverage.ByNamedTest,
            "PiiAnonymizerNode_ShouldHaveValidPortsAndParameters"),
        new("PiiAnonymizerNode", "Out", Coverage.ByNamedTest,
            "PiiAnonymizerNode_ExecuteAsync_WithCleanData_ShouldEmitClean"),
        new("PiiAnonymizerNode", "SensitiveFound", Coverage.ByNamedTest,
            "PiiAnonymizerNode_ExecuteAsync_WithSensitiveData_ShouldEmitSensitiveFound"),
        new("PromptObjectDetectorNode", "Error", Coverage.ByNamedTest,
            "PromptObjectDetectorNode_ExecuteAsync_NonExistentFile_ShouldEmitToError"),
        new("PromptObjectDetectorNode", "NoObjects", Coverage.ByNamedTest,
            "PromptObjectDetectorNode_ExecuteAsync_NonImageFile_ShouldEmitToNoObjects"),
        new("PromptObjectDetectorNode", "ObjectsFound", Coverage.ByNamedTest,
            "PromptObjectDetectorNode_ShouldHaveValidPortsAndParameters"),
        new("PromptTransformerNode", "Error", Coverage.ByNamedTest,
            "ExecuteAsync_WhenTemplateIsEmpty_ShouldEmitError"),
        new("PromptTransformerNode", "Transformed", Coverage.ByNamedTest,
            "ExecuteAsync_WithExpandSynonyms_ShouldAddVisualSynonyms"),
        new("SmartImageClassifierNode", "Error", Coverage.ByNamedTest,
            "SmartImageClassifierNode_EmitsOutAndSetsCorrectMetadataOrPasses"),
        new("SmartImageClassifierNode", "Out", Coverage.ByNamedTest,
            "SmartImageClassifierNode_EmitsOutAndSetsCorrectMetadataOrPasses"),
        new("SuperResolutionUpscalerNode", "Error", Coverage.ByNamedTest,
            "SuperResolutionUpscalerNode_ShouldHaveValidPortsAndParameters"),
        new("SuperResolutionUpscalerNode", "Out", Coverage.ByNamedTest,
            "SuperResolutionUpscalerNode_ShouldHaveValidPortsAndParameters"),
        new("SuperResolutionUpscalerNode", "Skipped", Coverage.ByNamedTest,
            "SuperResolutionUpscalerNode_ExecuteAsync_WithUnsupportedFormat_ShouldEmitSkipped"),
        new("TextToSpeechNode", "Error", Coverage.ByNamedTest,
            "TextToSpeechNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError"),
        new("TextToSpeechNode", "Out", Coverage.ByNamedTest,
            "TextToSpeechNode_ShouldHaveValidPortsAndParameters"),
        new("VoiceActivityDetectorNode", "Error", Coverage.ByNamedTest,
            "VoiceActivityDetectorNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError"),
        new("VoiceActivityDetectorNode", "Out", Coverage.ByNamedTest,
            "VoiceActivityDetectorNode_ExecuteAsync_WithUnsupportedExtension_ShouldEmitSilentAndOut"),
        new("VoiceActivityDetectorNode", "Silent", Coverage.ByNamedTest,
            "VoiceActivityDetectorNode_ExecuteAsync_WithUnsupportedExtension_ShouldEmitSilentAndOut"),
        new("VoiceActivityDetectorNode", "Speech", Coverage.ByNamedTest,
            "VoiceActivityDetectorNode_ShouldHaveValidPortsAndParameters"),
        new("ZeroShotSemanticSearchNode", "Error", Coverage.ByNamedTest,
            "ZeroShotSemanticSearchNode_ShouldHaveValidPortsAndParameters"),
        new("ZeroShotSemanticSearchNode", "Matched", Coverage.ByNamedTest,
            "ZeroShotSemanticSearchNode_ExecuteAsync_WithMatchingQuery_ShouldEmitMatched"),
        new("ZeroShotSemanticSearchNode", "Out", Coverage.ByNamedTest,
            "ZeroShotSemanticSearchNode_ExecuteAsync_WithMatchingQuery_ShouldEmitMatched"),
        new("ZeroShotSemanticSearchNode", "Unmatched", Coverage.ByNamedTest,
            "ZeroShotSemanticSearchNode_ShouldHaveValidPortsAndParameters"),

        // ── Archivos comprimidos (FileFlow.Plugin.Archives) ────────────────────────────────────────────────────────
        new("ArchiveCompressorNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("ArchiveCompressorNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("ArchiveFanInNode", "Error", Coverage.ByNamedTest,
            "AFanInThatCannotCreateItsDestination_ShouldLeaveByTheDeclaredErrorPort"),
        new("ArchiveFanInNode", "Out", Coverage.ByNamedTest,
            "EndToEnd_FolderSourceToFanOutToImageOptimizerToFanIn_ShouldRunEveryNodeAndRepackTheArchive"),
        new("ArchiveFanOutNode", "Error", Coverage.ByNamedTest,
            "AnArchiveThatCannotBeExtracted_ShouldLeaveByTheDeclaredErrorPort_WithItsDiagnosis"),
        // Este puerto tiene dos caminos cubiertos y distintos —el comprimido que no se puede extraer y el
        // comprimido que no trae ficheros—, y los dos se citan: un puerto puede declarar más de un testigo, y
        // cada uno tiene que verificar por su cuenta.
        new("ArchiveFanOutNode", "Error", Coverage.ByNamedTest,
            "AnArchiveWithoutFiles_ShouldAlsoLeaveByTheDeclaredErrorPort_ButWithoutVolumeDiagnosis"),
        new("ArchiveFanOutNode", "Out", Coverage.ByNamedTest,
            "EndToEnd_FolderSourceToFanOutToImageOptimizerToFanIn_ShouldRunEveryNodeAndRepackTheArchive"),
        new("ArchiveFilterNode", "Archive", Coverage.ByNamedTest,
            "ExecuteAsync_ShouldEmitToArchivePort_WhenComicOrEpubArchive"),
        new("ArchiveFilterNode", "RegularFile", Coverage.ByNamedTest,
            "ExecuteAsync_ShouldEmitToRegularFilePort_WhenDocFile"),
        new("ArchiveFilterNode", "SecondaryVolume", Coverage.ByNamedTest,
            "ExecuteAsync_ShouldEmitToSecondaryVolumePort_WhenSplitRarVolume"),
        new("SmartUnpackNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("SmartUnpackNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),

        // ── Datos (lectores, exportadores, bases de datos) (FileFlow.Plugin.Data) ──────────────────────────────────
        new("CsvExportNode", "Out", Coverage.ByExecutingTest,
            "CsvExportNode_WritesAndAppendsRowsCorrectly"),
        new("CsvReaderNode", "RowOut", Coverage.ByExecutingTest,
            "CsvReaderNode_ValidCsv_EmitsAllRowsWithMetadata"),
        new("DataFormatConverterNode", "Out", Coverage.ByNamedTest,
            "DataFormatConverterNode_CsvToJson_ConvertsCorrectly"),
        new("DataLookupNode", "Matched", Coverage.ByNamedTest,
            "DataLookupNode_MatchedKeyInExcel_EnrichesItemAndEmitsToMatched"),
        new("DataLookupNode", "Unmatched", Coverage.ByNamedTest,
            "DataLookupNode_MatchedKeyInExcel_EnrichesItemAndEmitsToMatched"),
        new("ExcelReaderNode", "RowOut", Coverage.ByExecutingTest,
            "ExcelReaderNode_ValidXlsx_EmitsRowsWithMetadata"),
        new("ExcelReportGeneratorNode", "Out", Coverage.ByNamedTest,
            "ExcelReportGeneratorNode_CollectsItemsAndEmitsXlsxOnCompletion"),
        new("ExcelReportGeneratorNode", "Report", Coverage.ByNamedTest,
            "ExcelReportGeneratorNode_CollectsItemsAndEmitsXlsxOnCompletion"),
        new("SqliteDatabaseSinkNode", "Error", Coverage.ByNamedTest,
            "ASinkWithAnUnsafeTableName_ShouldLeaveByTheDeclaredErrorPort_AndNotTouchTheDatabase"),
        new("SqliteDatabaseSinkNode", "Out", Coverage.ByNamedTest,
            "SqliteDatabaseSinkNode_InsertsRecordsAndCreatesSchemaAutomatically"),

        // ── Documentos (PDF) (FileFlow.Plugin.Documents) ───────────────────────────────────────────────────────────
        new("PdfMergeNode", "Out", Coverage.ByNamedTest,
            "PdfMergeNode_ExecuteAsync_And_OnWorkflowCompletedAsync_MergesAndEmitsConsolidatedPdf"),
        new("PdfMergeNode", "PassThrough", Coverage.ByNamedTest,
            "PdfMergeNode_ExecuteAsync_And_OnWorkflowCompletedAsync_MergesAndEmitsConsolidatedPdf"),
        new("PdfMetadataNode", "Out", Coverage.ByNamedTest,
            "PdfMetadataNode_ReadsAndModifiesMetadata"),
        new("PdfSplitNode", "Original", Coverage.ByExecutingTest,
            "PdfSplitNode_SplitsMultiplePagePdf"),
        new("PdfSplitNode", "Out", Coverage.ByNamedTest,
            "PdfSplitNode_SplitsMultiplePagePdf"),
        new("PdfTextExtractorNode", "Out", Coverage.ByNamedTest,
            "PdfTextExtractorNode_ExtractsTextSuccessfully"),
        new("PdfTextExtractorNode", "TextFile", Coverage.ByExecutingTest,
            "PdfTextExtractorNode_ExtractsTextSuccessfully"),

        // ── Sistema de archivos (FileFlow.Plugin.FileSystem) ───────────────────────────────────────────────────────
        new("AdvancedRenamerNode", "Error", Coverage.ByNamedTest,
            "ARenamerThatCannotFindItsSource_ShouldLeaveByTheDeclaredErrorPort"),
        new("AdvancedRenamerNode", "Out", Coverage.ByNamedTest,
            "AdvancedRenamer_IllegalCharacters_ShouldBeSanitizedAutomatically"),
        new("AdvancedRenamerNode", "Skipped", Coverage.ByNamedTest,
            "TheFilesWhoseTargetNameIsAlreadyTaken_ShouldLeaveByTheDeclaredSkippedPort"),
        new("DestinationSinkNode", "Done", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("DestinationSinkNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("DirectoryInspectorNode", "DirectoriesOnly", Coverage.ByNamedTest,
            "ExecuteAsync_ShouldEmitToDirectoriesOnlyPort_WhenFolderContainsOnlySubdirectories"),
        new("DirectoryInspectorNode", "MixedContent", Coverage.ByNamedTest,
            "ExecuteAsync_ShouldEmitToMixedContentPort_WhenFolderContainsMultipleFiles"),
        new("DirectoryInspectorNode", "SingleArchive", Coverage.ByNamedTest,
            "ExecuteAsync_ShouldEmitToSingleArchivePort_WhenFolderContainsOnlyOneArchiveFile"),
        new("DocumentProcessorNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("DocumentProcessorNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("EmptyDirectoryCleanerNode", "Error", Coverage.ByNamedTest,
            "AnEmptyDirectoryCleanerWhoseStorageFailsToDelete_ShouldLeaveByTheDeclaredErrorPort"),
        new("EmptyDirectoryCleanerNode", "Out", Coverage.ByNamedTest,
            "AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort"),
        new("FileRelocatorNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("FileRelocatorNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("FolderSourceNode", "Out", Coverage.ByNamedTest,
            "EndToEnd_FolderSourceToFanOutToImageOptimizerToFanIn_ShouldRunEveryNodeAndRepackTheArchive"),
        new("LogOutputNode", "Out", Coverage.ByNamedTest,
            "ExecuteAsync_WithCustomMessage_ShouldResolveVariablesAndLogCustomMessage"),
        new("OperationReportNode", "Error", Coverage.ByNamedTest,
            "AReportWhoseContentCannotBeMaterialized_ShouldLeaveByTheDeclaredErrorPort"),
        new("OperationReportNode", "Out", Coverage.ByNamedTest,
            "DestinationSinkNode_ShouldSaveInMemoryReportFileToDisk"),
        new("OperationReportNode", "Report", Coverage.ByNamedTest,
            "ACancelledReport_ShouldPropagateTheCancellationInsteadOfLeavingByTheErrorPort"),
        new("OriginalFileActionNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("OriginalFileActionNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("SafeRecycleDeleteNode", "Deleted", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("SafeRecycleDeleteNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("SyntheticDataSourceNode", "Out", Coverage.ByNamedTest,
            "SyntheticDataSourceNode_CustomItems_EmitsCustomTextList"),
        new("VariableInjectorNode", "Out", Coverage.ByNamedTest,
            "EndToEnd_FolderSourceToInjectorToDestinationSink_ShouldProcessPipeline"),

        // ── Huellas y deduplicación (FileFlow.Plugin.Hashing) ──────────────────────────────────────────────────────
        new("DeduplicationFilterNode", "Duplicate", Coverage.ByNamedTest,
            "DeduplicationFilterNode_IdentifiesDuplicates"),
        new("DeduplicationFilterNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("DeduplicationFilterNode", "Unique", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("HashCalculatorNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("HashCalculatorNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),

        // ── Imágenes (FileFlow.Plugin.Images) ──────────────────────────────────────────────────────────────────────
        new("ExifMetadataNode", "Out", Coverage.ByNamedTest,
            "EndToEnd_FolderSourceToInjectorToDestinationSink_ShouldProcessPipeline"),
        new("ImageOptimizerNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("ImageOptimizerNode", "Out", Coverage.ByNamedTest,
            "EndToEnd_FolderSourceToFanOutToImageOptimizerToFanIn_ShouldRunEveryNodeAndRepackTheArchive"),

        // ── Integraciones (CLI, webhooks, medios) (FileFlow.Plugin.Integrations) ───────────────────────────────────
        new("CliExecutionNode", "Failed", Coverage.ByNamedTest,
            "ACliNodeWhoseExecutableDoesNotExist_ShouldLeaveByTheDeclaredFailedPort"),
        new("CliExecutionNode", "Success", Coverage.ByNamedTest,
            "ACliNodeWhoseExecutableDoesNotExist_ShouldLeaveByTheDeclaredFailedPort"),
        new("MediaTranscoderNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("MediaTranscoderNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("WebhookNotificationNode", "Failed", Coverage.ByNamedTest,
            "AWebhookWithAnUnsupportedUrl_ShouldLeaveByTheDeclaredFailedPort"),
        new("WebhookNotificationNode", "Out", Coverage.ByExecutingTest,
            "AWebhookWithAnUnsupportedUrl_ShouldLeaveByTheDeclaredFailedPort"),

        // ── Lógica y control de flujo (FileFlow.Plugin.Logic) ──────────────────────────────────────────────────────
        new("BatchBufferNode", "BatchCompleted", Coverage.ByNamedTest,
            "ExecuteAsync_WhenBatchSizeReached_ShouldEmitBufferedItemsAndCompletionMarker"),
        new("BatchBufferNode", "ItemOut", Coverage.ByNamedTest,
            "ExecuteAsync_WhenBatchSizeReached_ShouldEmitBufferedItemsAndCompletionMarker"),
        new("BestVersionSelectorNode", "Out", Coverage.ByNamedTest,
            "GetAvailableFileVersions_ShouldDiscoverAllUpstreamVersionsInChain"),
        new("BestVersionSelectorNode", "WonA", Coverage.ByNamedTest,
            "BestVersionSelectorNode_SelectsSmallestSize_WhenOptimizedIsSmaller"),
        new("BestVersionSelectorNode", "WonB", Coverage.ByNamedTest,
            "BestVersionSelectorNode_SelectsOriginal_WhenOptimizedIsLarger_AndAutoPurgesOptimized"),
        new("ExpressionFilterNode", "False", Coverage.ByNamedTest,
            "ExpressionFilterNode_ShouldCompareOutputFileSizeWithOriginalFileSize"),
        new("ExpressionFilterNode", "True", Coverage.ByNamedTest,
            "ExpressionFilterNode_ShouldCompareOutputFileSizeWithOriginalFileSize"),
        new("FileForkNode", "Current", Coverage.ByNamedTest,
            "FileForkNode_EmitsParallelBranchesForOriginalAndCurrent"),
        new("FileForkNode", "Original", Coverage.ByNamedTest,
            "FileForkNode_EmitsParallelBranchesForOriginalAndCurrent"),
        new("FileForkNode", "Version", Coverage.ByExecutingTest,
            "FileForkNode_EmitsParallelBranchesForOriginalAndCurrent"),
        new("ForkJoinBarrierNode", "AllCompleted", Coverage.WithoutExecution,
            "Ninguna prueba del suite menciona siquiera a la barrera: sus tres puertos se declaran con este motivo en vez de darlos por cubiertos. Su caso necesita dos ramas que vuelvan a Branch1_Done y Branch2_Done con el mismo ítem, y nadie lo ha escrito todavía."),
        new("ForkJoinBarrierNode", "Fork1", Coverage.WithoutExecution,
            "Ninguna prueba del suite menciona siquiera a la barrera: sus tres puertos se declaran con este motivo en vez de darlos por cubiertos. Su caso necesita dos ramas que vuelvan a Branch1_Done y Branch2_Done con el mismo ítem, y nadie lo ha escrito todavía."),
        new("ForkJoinBarrierNode", "Fork2", Coverage.WithoutExecution,
            "Ninguna prueba del suite menciona siquiera a la barrera: sus tres puertos se declaran con este motivo en vez de darlos por cubiertos. Su caso necesita dos ramas que vuelvan a Branch1_Done y Branch2_Done con el mismo ítem, y nadie lo ha escrito todavía."),
        new("IntermediateCleanupNode", "Out", Coverage.ByExecutingTest,
            "IntermediateCleanupNode_PurgesIntermediateFiles_PreservingOriginal"),
        new("SwitchActiveFileNode", "Out", Coverage.ByExecutingTest,
            "ParameterDescriptors_ForVersionNodes_SpecifyFileVersionSelector"),
        new("SwitchCaseNode", "Case 1", Coverage.ByNamedTest,
            "AValueThatMatchesNoCase_ShouldLeaveByTheDeclaredDefaultPort"),
        new("SwitchCaseNode", "Case 2", Coverage.ByNamedTest,
            "SwitchCaseNodeViewModel_ShouldInitializeWithCase1AndDefault_AndSupportDynamicAdditionAndRenaming"),
        new("SwitchCaseNode", "Default", Coverage.ByNamedTest,
            "AValueThatMatchesNoCase_ShouldLeaveByTheDeclaredDefaultPort"),
        new("ThrottleDelayNode", "Out", Coverage.ByNamedTest,
            "WhenAPortSurvivesTheChange_ShouldKeepItsInstanceAndItsCable"),
        new("VersionRouterNode", "False", Coverage.ByNamedTest,
            "VersionRouterNode_RoutesToFalse_AndActivatesOriginal_WhenOptimizedIsLarger"),
        new("VersionRouterNode", "True", Coverage.ByExecutingTest,
            "ParameterDescriptors_ForVersionNodes_SpecifyFileVersionSelector"),

        // ── Red (FileFlow.Plugin.Network) ──────────────────────────────────────────────────────────────────────────
        new("NetworkDownloadNode", "Error", Coverage.ByNamedTest,
            "ANodeWithAnUnsupportedProtocol_ShouldLeaveByTheDeclaredErrorPort"),
        new("NetworkDownloadNode", "Out", Coverage.ByNamedTest,
            "NetworkDownloadNode_Ftp_DryRun_ShouldSimulateAndEmitOut"),
        new("NetworkUploadNode", "Error", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),
        new("NetworkUploadNode", "Out", Coverage.ByNamedTest,
            "ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort"),

        // ── Subflujos (FileFlow.Plugin.Subflows) ───────────────────────────────────────────────────────────────────
        new("SubflowInputNode", "Alterna", Coverage.ByNamedTest,
            "AConfiguredPort_ShouldBeTheOneTheSubflowInputEmits"),
        new("SubflowInputNode", "Entrada", Coverage.ByNamedTest,
            "AConfiguredPort_ShouldBeTheOneTheSubflowInputEmits"),
        new("SubflowNode", "Done", Coverage.ByNamedTest,
            "AContainerWithARenamedBoundary_ShouldEmitByTheDeclaredBoundaryPort"),
        new("SubflowOutputNode", "Out", Coverage.ByNamedTest,
            "SubflowOutputNode_ShouldExposeOneInputPerConfiguredName"),
    ];

    /// <summary>
    /// Contrasta el inventario con el árbol y con el suite, y devuelve una infracción por cada desacuerdo.
    ///
    /// <para>Seis desacuerdos posibles, todos comprobables sobre el texto: un puerto que el árbol declara (o
    /// emite) y el inventario no declara; un asiento cuyo puerto el árbol ya no declara ni emite; un testigo que
    /// no existe en el suite; un testigo que no habla del nodo o no dice lo que su grado promete; un grado que
    /// el suite ya desmintió (hay un caso que nombra el puerto cuando el asiento dice <c>ByExecutingTest</c>, o
    /// hay casos que ejecutan el nodo cuando el asiento dice <c>WithoutExecution</c>); y un motivo que no
    /// explica nada. La lógica vive aquí —y no dentro de la guardia— para poder auto-testearla con entradas
    /// sintéticas sin dejar nodos infractores en el árbol.</para>
    /// </summary>
    /// <param name="declaredOrEmittedPorts">
    /// Todos los puertos que los nodos del producto declaran o emiten (nodo y nombre). Los declarados cubren el
    /// camino feliz y las ramas; los emitidos, cualquier nombre que se use de verdad.
    /// </param>
    /// <param name="witnesses">
    /// El índice de testigos por nodo (<see cref="PortWitnessIndex"/>), que es lo que permite juzgar el grado de
    /// cada asiento: qué casos hablan del nodo, cuáles lo ejecutan y cuál nombra cada puerto.
    /// </param>
    /// <param name="testMethodNames">Nombres de los métodos de prueba que existen en el suite.</param>
    /// <param name="entries">El inventario a auditar.</param>
    /// <param name="computedPortNodes">
    /// Nodos cuyos puertos se calculan en ejecución, con los puertos que declaran al materializarlos. Se pasan
    /// porque el texto del nodo no los contiene: sin ellos, un puerto calculado sería invisible al inventario.
    /// </param>
    public static IReadOnlyList<string> Audit(
        IEnumerable<(string NodeClass, string Port)> declaredOrEmittedPorts,
        IReadOnlyDictionary<string, PortWitnessIndex.NodeWitnesses> witnesses,
        IReadOnlySet<string> testMethodNames,
        IReadOnlyList<Entry> entries,
        IEnumerable<(string NodeClass, IReadOnlyList<string> DeclaredPorts)>? computedPortNodes = null)
    {
        ArgumentNullException.ThrowIfNull(declaredOrEmittedPorts);
        ArgumentNullException.ThrowIfNull(witnesses);
        ArgumentNullException.ThrowIfNull(testMethodNames);
        ArgumentNullException.ThrowIfNull(entries);

        var violations = new List<string>();

        var requiredPorts = declaredOrEmittedPorts.Distinct().ToList();

        // Los puertos de un nodo que los calcula no están en su código: se declaran al resolver su topología y
        // son puertos como cualquier otro.
        foreach ((string nodeClass, IReadOnlyList<string> declaredPorts) in computedPortNodes ?? [])
        {
            requiredPorts.AddRange(declaredPorts.Distinct().Select(port => (nodeClass, port)));
        }

        requiredPorts = [.. requiredPorts.Distinct()];

        var declared = entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);

        foreach ((string nodeClass, string port) in requiredPorts)
        {
            if (!declared.Contains($"{nodeClass}.{port}"))
            {
                violations.Add(BranchPortNames.Contains(port)
                    ? $"{nodeClass} emite por '{port}' y el inventario no lo declara: una rama sin prueba (o sin " +
                      $"el motivo de por qué no se puede forzar) es exactamente el agujero que dejó pasar el " +
                      $"Fan-Out del hito 188."
                    : $"{nodeClass} declara el puerto '{port}' y el inventario no lo declara: un puerto del camino " +
                      $"feliz que ninguna prueba ejecuta es un nodo que se puede cablear y ejecutar sin que nadie " +
                      $"haya recorrido su salida.");
            }
        }

        foreach (Entry entry in entries)
        {
            if (!requiredPorts.Contains((entry.NodeClass, entry.Port)))
            {
                violations.Add(
                    $"{entry.Key} está en el inventario y el árbol ya no declara ni emite ese puerto: una entrada " +
                    $"que apunta a un puerto inexistente vigila a nadie.");
            }

            PortWitnessIndex.NodeWitnesses? node = witnesses.GetValueOrDefault(entry.NodeClass);

            switch (entry.Coverage)
            {
                case Coverage.ByNamedTest:
                    if (!testMethodNames.Contains(entry.Evidence))
                    {
                        violations.Add(
                            $"{entry.Key} cita la prueba '{entry.Evidence}', que no existe en el suite: la evidencia " +
                            $"tiene que poder ejecutarse, no solo sonar bien.");
                    }
                    else if (node is null || !MentionsTheNode(node, entry.Evidence))
                    {
                        violations.Add(
                            $"{entry.Key} cita la prueba '{entry.Evidence}', que no habla de '{entry.NodeClass}': un " +
                            $"asiento del inventario no puede taparse con la prueba del nodo vecino.");
                    }
                    else if (!CitesThePort(node, entry.Evidence, entry.Port))
                    {
                        violations.Add(
                            $"{entry.Key} dice {entry.Coverage} y cita '{entry.Evidence}', pero ese caso no nombra " +
                            $"el puerto: o el caso tiene que decir por dónde sale el ítem, o el grado es " +
                            $"'{Coverage.ByExecutingTest}' (el nodo se ejecuta, el puerto no se nombra).");
                    }
                    break;

                case Coverage.ByExecutingTest:
                    if (!testMethodNames.Contains(entry.Evidence))
                    {
                        violations.Add(
                            $"{entry.Key} cita la prueba '{entry.Evidence}', que no existe en el suite.");
                    }
                    else if (node is null || !MentionsTheNode(node, entry.Evidence) || !node.IsExercised)
                    {
                        violations.Add(
                            $"{entry.Key} dice {entry.Coverage} y cita '{entry.Evidence}', pero no hay ningún caso " +
                            $"que ejecute '{entry.NodeClass}': si nadie lo ejecuta, el grado es " +
                            $"'{Coverage.WithoutExecution}' y lo que falta es el motivo.");
                    }
                    else if (node.Citing(entry.Port) is { } citing)
                    {
                        violations.Add(
                            $"{entry.Key} dice {entry.Coverage} («el nodo se ejecuta, el puerto no se nombra») pero el " +
                            $"suite ya tiene un caso que nombra el puerto: '{citing.Name}'. Declara ese testigo como " +
                            $"'{Coverage.ByNamedTest}', que es la evidencia más fuerte.");
                    }
                    break;

                default:
                    if (!Explains(entry.Evidence))
                    {
                        violations.Add(
                            $"{entry.Key} se declara sin nadie que lo ejecute sin explicar por qué: el motivo tiene " +
                            $"que decir qué haría falta, no dejar la casilla vacía.");
                    }
                    else if (node is not null && node.IsExercised)
                    {
                        violations.Add(
                            $"{entry.Key} se declara sin nadie que lo ejecute y el suite tiene casos que lo ejecutan " +
                            $"({node.DescribeMentions()}): un nodo probado no se declara hueco.");
                    }
                    break;
            }
        }

        foreach (IGrouping<string, Entry> group in entries.GroupBy(e => $"{e.Key}|{e.Evidence}", StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                violations.Add($"El inventario repite la entrada {group.Key}.");
            }
        }

        return violations;
    }

    /// <summary>
    /// ¿Ese caso habla del nodo? Se comprueba sobre el <b>texto</b> del caso y no sobre la pertenencia al
    /// índice: así el auditor no depende de que quien le pase los testigos los haya filtrado bien, y una lista
    /// fabricada a mano —como la de sus propias pruebas— tampoco puede colarle un testigo ajeno.
    /// </summary>
    private static bool MentionsTheNode(PortWitnessIndex.NodeWitnesses node, string testName) =>
        node.MentioningTheNode.Any(b =>
            string.Equals(b.Name, testName, StringComparison.Ordinal)
            && b.Source.Contains(node.NodeClass, StringComparison.Ordinal));

    /// <summary>¿Y cita el nombre del puerto (como literal)?</summary>
    private static bool CitesThePort(PortWitnessIndex.NodeWitnesses node, string testName, string port) =>
        node.MentioningTheNode.Any(b =>
            string.Equals(b.Name, testName, StringComparison.Ordinal)
            && b.Source.Contains(node.NodeClass, StringComparison.Ordinal)
            && b.Source.Contains($"\"{port}\"", StringComparison.Ordinal));

    /// <summary>
    /// ¿El texto explica algo? Un motivo es una frase (varias palabras y con longitud de frase), no un nombre de
    /// prueba ni una palabra suelta: la diferencia entre declarar un hueco y taparlo con una etiqueta.
    /// </summary>
    private static bool Explains(string reason) =>
        reason.Length >= 80 && reason.Contains(' ') && !reason.EndsWith('_');
}
