# Cobertura de mutaciones

> Generado por `MutationDeclarationCoverageTests` a partir de `mutations/*.json` y del árbol de fuentes.
> **No se edita a mano.**
> Regenerar: `FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests`.

Mutaciones declaradas: 33
Subsistemas del producto con alguna mutación: 10 de 15
Guardias que auditan el repositorio con mutación que las muerda: 6 de 33

## Qué declara cada mutación

| Mutación | Fichero que muta | Testigo | Control |
| :--- | :--- | :--- | :--- |
| `archivo-vacio-dejado-atras` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorAskedForAContainerItsWriterRejects_ShouldLeaveNoFileBehind` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive` |
| `borrado-virtual-solo-ve-archivos` | `FileFlow.Sdk/Storage/VirtualStorageService.cs` | `VirtualStorageService_EnumerationAndDirectoryDeletion_ShouldOperateInTheStore` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `bufer-de-lotes-heredado` | `FileFlow.Plugin.Logic/BatchBufferNode.cs` | `ExecuteAsync_WhenTheSameInstanceSeesAnotherExecution_ShouldNotMixThePendingItemsOfThePreviousOne` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `cache-de-embeddings-sin-entorno` | `FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs` | `AVectorFromAWorldWithoutModel_ShouldNotAnswerOnceTheModelArrives` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `carpeta-de-nodo-de-ia-donde-corre` | `FileFlow.Plugin.AI/Common/NodeOutputDirectory.cs` | `WithNoFolderAtAll_ShouldUseTheProductsTempFolder_NotTheProcessWorkingDirectory|WithNothingDeclared_ShouldUseTheItemFolder` | `WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin|WithADeclaredFolder_ShouldUseIt_AnchoringItInTheFlowFolderWhenRelative` |
| `catalogo-sin-carpeta-de-destino` | `docs/examples/01_basic/flow_08_compresion_zip_automatica.json` | `EveryCompressorInTheCatalog_ShouldSayWhereTheArchiveGoes` | `AllExampleFlows_ShouldBeWrittenByTheProductWriter` |
| `censo-de-puertos-ignora-un-puerto-nuevo` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `NodePortCoverageGuardTests` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `censo-de-puertos-sin-su-asiento` | `FileFlow.Tests/TestHelpers/NodePortInventory.cs` | `NodePortCoverageGuardTests` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `compresor-contra-su-propia-entrada` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorWhoseDestinationIsItsOwnInput_ShouldRefuseInsteadOfTruncatingTheInput|ExampleFlowsEndToEndTests` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive` |
| `compresor-que-escribe-donde-corre` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorWithoutADestination_ShouldWriteInTheFlowsOutputFolder_AndSaySo|AFlowSavedWithoutADestination_ShouldWriteInTheOutputFolderTheLauncherHands` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive|TheFactoryDefaultOfTheCompressor_ShouldBeTheOutputFolderOfTheFlow` |
| `contrato-de-colecciones-sin-su-regla` | `FileFlow.Tests/TestHelpers/TestCollectionContractAnalyzer.cs` | `TestCollectionContractGuardTests` | `Analyzer_ShouldRequireLocalization_WhenClassUsesRealUserPreferences` |
| `datos-solo-el-token-canonico` | `FileFlow.Plugin.Data/Nodes/Exporters/CsvExportNode.cs` | `CsvExportNode_WithAnAliasOfTheFlowFolder_ShouldWriteWhereTheCanonicalTokenWrites` | `CsvExportNode_WithTheFlowFolderToken_ShouldWriteInsideTheFolderTheFlowDeclares` |
| `diario-acumula-ejecuciones` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun` | `ExecutionJournal_Rollback_RestoresMovedFile` |
| `ejecucion-pausada-heredada` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `ARunThatEndedWhilePaused_ShouldNotLeaveTheNextOneWaiting` | `Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun` |
| `identidad-perdida-al-optimizar` | `FileFlow.Plugin.Images/ImageOptimizerNode.cs` | `ExecuteAsync_WithARealImage_ShouldKeepTheIdentityOfTheItemThatEntered` | `ExecuteAsync_WhenInputIsWebP_ShouldDecodeAndOptimizeSuccessfully` |
| `indice-de-hashes-heredado` | `FileFlow.Plugin.Hashing/DeduplicationFilterNode.cs` | `TheHashIndexOfOneRun_ShouldNotClassifyTheFilesOfTheNext` | `TheBatchBuffer_ShouldNotKeepPendingItemsForTheNextRun` |
| `indice-de-pruebas-ciego-al-cr` | `FileFlow.Tests/TestHelpers/TestSuiteIndex.cs` | `TestSuiteIndexTests` | `SourceTextTests` |
| `limpiador-borra-por-su-cuenta` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `AnEmptyDirectoryCleanerWhoseStorageFailsToDelete` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `limpiador-vuelve-a-mirar-el-disco` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `VirtualEmptyFolderCleanupIntegrationTests` | `EmptyDirectoryCleaner_RegistersDeletedPermanentlyJournalEntry` |
| `lote-incompleto-perdido-al-terminar` | `FileFlow.Plugin.Logic/BatchBufferNode.cs` | `OnWorkflowCompleted_WhenTheBatchDidNotFill_ShouldDeliverItAndCloseItWithItsMarker` | `ExecuteAsync_WhenBatchSizeReached_ShouldEmitBufferedItemsAndCompletionMarker` |
| `modelo-clip-buscado-por-su-id` | `FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs` | `AFileNamedWithTheModelId_ShouldNotCountAsTheDownloadedModel` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `modo-virtual-heredado` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `ASyntheticRun_ShouldNotLeaveTheNextOneInVirtualMode` | `VirtualEmptyFolderCleanupIntegrationTests` |
| `pasos-de-renombrado-ilegibles` | `FileFlow.Sdk/Renaming/RenamerPresetService.cs` | `AdvancedRenamer_WhenTheStepsArriveWithEnumNames_ShouldApplyThemInsteadOfTheDefaultTemplate` | `AdvancedRenamer_MethodStepsPipeline_ShouldExecuteCorrectly` |
| `punto-de-control-sobrevive-a-la-ejecucion` | `FileFlow.Core/Engine/WorkflowCheckpointHandler.cs` | `WorkflowExecutor_ReusedForASecondRun_ShouldProcessEveryFileAgain` | `CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile` |
| `punto-de-control-vuelve-a-escribir-por-archivo` | `FileFlow.Core/Engine/WorkflowCheckpointHandler.cs` | `CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile` | `CheckpointManager_SaveRetrieveAndClear_OperatesCorrectly` |
| `retroalimentacion-de-barrera-contada-como-ciclo` | `FileFlow.Core/Engine/GraphValidator.cs` | `Validate_ShouldAcceptABranchReturningToABarrierNode` | `Validate_ShouldFail_WhenGraphContainsCycle` |
| `retroalimentacion-sin-avisos` | `FileFlow.Core/Engine/GraphValidator.cs` | `Validate_ShouldWarnWhenANodeIsOnlyFedByFeedbackPorts` | `Validate_ShouldAcceptABranchReturningToABarrierNode` |
| `salida-global-sin-expandir` | `FileFlow.Sdk/ParameterHelper.cs` | `VariableTemplateResolver_WithATemplateOutputFolder_ResolvesAFolderInAnyParameter|ResolveOutputPath_WithGlobalOutputDirDeclaredAsATemplate_ExpandsAndAnchorsItUnderTheSourceRoot|AFlowWhoseOutputFolderIsATemplate_ShouldAnchorTheArchiveInARealFolder|WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin|CsvExportNode_WithTheFlowFolderToken_ShouldWriteInsideTheFolderTheFlowDeclares|CsvExportNode_WithAnAliasOfTheFlowFolder_ShouldWriteWhereTheCanonicalTokenWrites|ExcelReportGeneratorNode_WithATemplateFlowFolder_ShouldWriteTheReportInsideTheOrigin` | `ResolveOutputPath_WithRelativePath_AnchorsUnderGlobalOutputDir|ResolveOutputPath_WithoutGlobalOutputDir_AnchorsUnderSourceDirectory|VariableTemplateResolver_WithoutExplicitMetadata_FallsBackToAppPathsDefault` |
| `struct-de-shell-desalineado` | `FileFlow.Core/Platform/WindowsPlatformService.cs` | `WindowsShellFileOperationLayoutTests` | `ForkJoinBarrierNodeTests` |
| `tabla-en-cache-sin-mirar-el-tamano` | `FileFlow.Plugin.Data/Nodes/Processing/DataLookupTableLoader.cs` | `TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp` | `TheLookupTableInMemory_ShouldBeTheOneOfThisRun` |
| `tabla-en-cache-sin-tope` | `FileFlow.Plugin.Data/Nodes/Processing/DataLookupTableLoader.cs` | `TheTableCache_ShouldNotGrowWithoutBoundAcrossRuns` | `TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp` |
| `token-de-tema-que-desaparece` | `FileFlow.App/Services/ThemeResourceApplier.cs` | `ThemeTokenCompletenessTests` | `DisabledStateLintTests` |
| `validador-sin-materializar-puertos` | `FileFlow.Core/Engine/GraphValidator.cs` | `GraphValidatorDynamicPortTests` | `GraphValidatorTests` |

## Subsistemas del producto sin ninguna mutación declarada (5 de 15)

Una mutación por comportamiento que importa; estos proyectos no tienen ninguna, así que ningún
defecto declarado demuestra que sus pruebas muerdan. Es la lista de trabajo, no un reproche.

- `FileFlow.Plugin.Documents`
- `FileFlow.Plugin.Integrations`
- `FileFlow.Plugin.Network`
- `FileFlow.Plugin.Scripting`
- `FileFlow.Plugin.Subflows`

## Guardias del repositorio sin ninguna mutación que las muerda (27 de 33)

Las guardias que auditan el árbol (usan `SourceTree`, `TestRepositoryLocator` o `TestSuiteIndex`) y no
aparecen como testigo de ninguna mutación: están escritas, y nadie ha demostrado que muerdan.

- `FileFlow.Tests/Unit/AI/WeakModelStatusRelayTests.cs`
- `FileFlow.Tests/Unit/App/ApplicationHeartbeatContractTests.cs`
- `FileFlow.Tests/Unit/App/DeferredWorkInventoryGuardTests.cs`
- `FileFlow.Tests/Unit/App/DisabledStateLintTests.cs`
- `FileFlow.Tests/Unit/App/MutationDeclarationCoverageTests.cs`
- `FileFlow.Tests/Unit/App/MutationDeclarationGuardTests.cs`
- `FileFlow.Tests/Unit/App/NodeEmissionPortGuardTests.cs`
- `FileFlow.Tests/Unit/App/SplashScreenStartupTests.cs`
- `FileFlow.Tests/Unit/App/ThemeStudioCatalogTests.cs`
- `FileFlow.Tests/Unit/App/ThemeVariantPropagationTests.cs`
- `FileFlow.Tests/Unit/App/UiIconographyTests.cs`
- `FileFlow.Tests/Unit/App/UiStyleContractTests.cs`
- `FileFlow.Tests/Unit/App/UiStyleLintTests.cs`
- `FileFlow.Tests/Unit/Core/FlowFormatSerializationGuardTests.cs`
- `FileFlow.Tests/Unit/Core/WorkflowFormatShapeTests.cs`
- `FileFlow.Tests/Unit/Plugins/NodeArchitectureGuardTests.cs`
- `FileFlow.Tests/Unit/Plugins/NodeCatalogGuardTests.cs`
- `FileFlow.Tests/Unit/Plugins/NodeRuntimeCatalogGuardTests.cs`
- `FileFlow.Tests/Unit/Views/AnimationClockTests.cs`
- `FileFlow.Tests/Unit/Views/DesignStateBaselinesTests.cs`
- `FileFlow.Tests/Unit/Views/HeadlessInfrastructureTests.cs`
- `FileFlow.Tests/Unit/Views/NodeCardVisualContractTests.cs`
- `FileFlow.Tests/Unit/Views/SelectorBindingGuardTests.cs`
- `FileFlow.Tests/Unit/Views/SettingsTabsCoverageTests.cs`
- `FileFlow.Tests/Unit/Views/ThemeStudioVisualContractTests.cs`
- `FileFlow.Tests/Unit/Views/ThemeTokenOverwriteGuardTests.cs`
- `FileFlow.Tests/Unit/Views/WindowActivationContractTests.cs`

## Dónde muta cada declaración

- **FileFlow.App**: `token-de-tema-que-desaparece`
- **FileFlow.Core**: `diario-acumula-ejecuciones`, `ejecucion-pausada-heredada`, `modo-virtual-heredado`, `punto-de-control-sobrevive-a-la-ejecucion`, `punto-de-control-vuelve-a-escribir-por-archivo`, `retroalimentacion-de-barrera-contada-como-ciclo`, `retroalimentacion-sin-avisos`, `struct-de-shell-desalineado`, `validador-sin-materializar-puertos`
- **FileFlow.Plugin.AI**: `cache-de-embeddings-sin-entorno`, `carpeta-de-nodo-de-ia-donde-corre`, `modelo-clip-buscado-por-su-id`
- **FileFlow.Plugin.Archives**: `archivo-vacio-dejado-atras`, `compresor-contra-su-propia-entrada`, `compresor-que-escribe-donde-corre`
- **FileFlow.Plugin.Data**: `datos-solo-el-token-canonico`, `tabla-en-cache-sin-mirar-el-tamano`, `tabla-en-cache-sin-tope`
- **FileFlow.Plugin.FileSystem**: `censo-de-puertos-ignora-un-puerto-nuevo`, `limpiador-borra-por-su-cuenta`, `limpiador-vuelve-a-mirar-el-disco`
- **FileFlow.Plugin.Hashing**: `indice-de-hashes-heredado`
- **FileFlow.Plugin.Images**: `identidad-perdida-al-optimizar`
- **FileFlow.Plugin.Logic**: `bufer-de-lotes-heredado`, `lote-incompleto-perdido-al-terminar`
- **FileFlow.Sdk**: `borrado-virtual-solo-ve-archivos`, `pasos-de-renombrado-ilegibles`, `salida-global-sin-expandir`
- **FileFlow.Tests (infraestructura de pruebas)**: `catalogo-sin-carpeta-de-destino`, `censo-de-puertos-sin-su-asiento`, `contrato-de-colecciones-sin-su-regla`, `indice-de-pruebas-ciego-al-cr`

Las que mutan la **declaración** de una guardia (el censo, el analizador) cuentan como infraestructura de pruebas, no como subsistema del producto: son 4.
