# Cobertura de mutaciones

> Generado por `MutationDeclarationCoverageTests` a partir de `mutations/*.json` y del árbol de fuentes.
> **No se edita a mano.**
> Regenerar: `FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests`.

Mutaciones declaradas: 52
Subsistemas del producto con alguna mutación: 15 de 17
Guardias que auditan el repositorio con mutación que las muerda: 9 de 38

## Qué declara cada mutación

| Mutación | Fichero que muta | Testigo | Control |
| :--- | :--- | :--- | :--- |
| `archivo-vacio-dejado-atras` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorAskedForAContainerItsWriterRejects_ShouldLeaveNoFileBehind` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive` |
| `borrado-virtual-solo-ve-archivos` | `FileFlow.Sdk/Storage/VirtualStorageService.cs` | `VirtualStorageService_EnumerationAndDirectoryDeletion_ShouldOperateInTheStore` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `bufer-de-lotes-heredado` | `FileFlow.Plugin.Logic/BatchBufferNode.cs` | `ExecuteAsync_WhenTheSameInstanceSeesAnotherExecution_ShouldNotMixThePendingItemsOfThePreviousOne` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `cable-con-anclas-estimadas` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `DrawWires_ShouldConsumeTheWrittenBackAnchors` | `Canvas_ShouldSubscribeToConnectionsCollectionChanged` |
| `cable-con-la-curva-al-reves` | `FileFlow.App.Core/Services/ConnectionGeometry.cs` | `ConnectionGeometryTests` | `EditorViewportCalculatorTests` |
| `cables-que-no-llegan-tarde` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `UnoCanvasConnectionsGuardTests` | `Canvas_ShouldKeepTheNodesSubscriptionAlongsideConnections` |
| `cache-de-embeddings-sin-entorno` | `FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs` | `AVectorFromAWorldWithoutModel_ShouldNotAnswerOnceTheModelArrives` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `carpeta-de-nodo-de-ia-donde-corre` | `FileFlow.Plugin.AI/Common/NodeOutputDirectory.cs` | `WithNoFolderAtAll_ShouldUseTheProductsTempFolder_NotTheProcessWorkingDirectory|WithNothingDeclared_ShouldUseTheItemFolder` | `WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin|WithADeclaredFolder_ShouldUseIt_AnchoringItInTheFlowFolderWhenRelative` |
| `catalogo-sin-carpeta-de-destino` | `docs/examples/01_basic/flow_08_compresion_zip_automatica.json` | `EveryCompressorInTheCatalog_ShouldSayWhereTheArchiveGoes` | `AllExampleFlows_ShouldBeWrittenByTheProductWriter` |
| `censo-de-puertos-ignora-un-puerto-nuevo` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `NodePortCoverageGuardTests` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `censo-de-puertos-sin-su-asiento` | `FileFlow.Tests/TestHelpers/NodePortInventory.cs` | `NodePortCoverageGuardTests` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `compresor-contra-su-propia-entrada` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorWhoseDestinationIsItsOwnInput_ShouldRefuseInsteadOfTruncatingTheInput|ExampleFlowsEndToEndTests` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive` |
| `compresor-que-escribe-donde-corre` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorWithoutADestination_ShouldWriteInTheFlowsOutputFolder_AndSaySo|AFlowSavedWithoutADestination_ShouldWriteInTheOutputFolderTheLauncherHands` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive|TheFactoryDefaultOfTheCompressor_ShouldBeTheOutputFolderOfTheFlow` |
| `contrato-de-colecciones-sin-su-regla` | `FileFlow.Tests/TestHelpers/TestCollectionContractAnalyzer.cs` | `TestCollectionContractGuardTests` | `Analyzer_ShouldRequireLocalization_WhenClassUsesRealUserPreferences` |
| `datos-solo-el-token-canonico` | `FileFlow.Plugin.Data/Nodes/Exporters/CsvExportNode.cs` | `CsvExportNode_WithAnAliasOfTheFlowFolder_ShouldWriteWhereTheCanonicalTokenWrites` | `CsvExportNode_WithTheFlowFolderToken_ShouldWriteInsideTheFolderTheFlowDeclares` |
| `decoradores-que-no-llegan-al-arbol` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `UnoCanvasDecoratorsGuardTests` | `Canvas_ShouldKeepTheNodeAndWireSubscriptionsAlongsideDecorators` |
| `diario-acumula-ejecuciones` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun` | `ExecutionJournal_Rollback_RestoresMovedFile` |
| `dry-run-que-entrega-el-disparador` | `FileFlow.Plugin.Network/Transports/HttpTransportStrategy.cs` | `NetworkDownloadNode_Http_DryRun_ShouldSimulateAndEmitOut` | `CliExecutionNode_WhenExitCodeNonZero_ShouldEmitFailedAndCaptureStdErr` |
| `ejecucion-pausada-heredada` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `ARunThatEndedWhilePaused_ShouldNotLeaveTheNextOneWaiting` | `Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun` |
| `enlace-de-geometria-sin-proyeccion` | `FileFlow.App/Views/EditorView.axaml` | `GeometryBindingProjectionTests` | `TheConnectionTemplate_ShouldDefineStandardConnectionWire` |
| `identidad-perdida-al-optimizar` | `FileFlow.Plugin.Images/ImageOptimizerNode.cs` | `ExecuteAsync_WithARealImage_ShouldKeepTheIdentityOfTheItemThatEntered` | `ExecuteAsync_WhenInputIsWebP_ShouldDecodeAndOptimizeSuccessfully` |
| `indice-de-hashes-heredado` | `FileFlow.Plugin.Hashing/DeduplicationFilterNode.cs` | `TheHashIndexOfOneRun_ShouldNotClassifyTheFilesOfTheNext` | `TheBatchBuffer_ShouldNotKeepPendingItemsForTheNextRun` |
| `indice-de-pruebas-ciego-al-cr` | `FileFlow.Tests/TestHelpers/TestSuiteIndex.cs` | `TestSuiteIndexTests` | `SourceTextTests` |
| `inspector-sin-write-back` | `FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs` | `EditingParameterThroughTheViewModel_ShouldWriteThroughToTheNodeInstance` | `ToolboxViewModel_SearchText_ShouldExpandMatchingCategories` |
| `latido-rearrancado-que-no-late` | `FileFlow.App.Core/Services/HeartbeatService.cs` | `AStoppedBeat_ShouldStopDelivering_AndResumeWhenStartedAgain` | `TheRegistry_ShouldNotAdmitTwoBeatsWithTheSameName` |
| `lienzo-sin-peer-uia` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `UnoAutomationSurfaceGuardTests` | `TheZoomBar_ShouldExposeItsAnchor_ItsLevel_AndItsThreeButtons` |
| `limpiador-borra-por-su-cuenta` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `AnEmptyDirectoryCleanerWhoseStorageFailsToDelete` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `limpiador-vuelve-a-mirar-el-disco` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `VirtualEmptyFolderCleanupIntegrationTests` | `EmptyDirectoryCleaner_RegistersDeletedPermanentlyJournalEntry` |
| `lote-incompleto-perdido-al-terminar` | `FileFlow.Plugin.Logic/BatchBufferNode.cs` | `OnWorkflowCompleted_WhenTheBatchDidNotFill_ShouldDeliverItAndCloseItWithItsMarker` | `ExecuteAsync_WhenBatchSizeReached_ShouldEmitBufferedItemsAndCompletionMarker` |
| `modelo-clip-buscado-por-su-id` | `FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs` | `AFileNamedWithTheModelId_ShouldNotCountAsTheDownloadedModel` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `modo-virtual-heredado` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `ASyntheticRun_ShouldNotLeaveTheNextOneInVirtualMode` | `VirtualEmptyFolderCleanupIntegrationTests` |
| `pasos-de-renombrado-ilegibles` | `FileFlow.Sdk/Renaming/RenamerPresetService.cs` | `AdvancedRenamer_WhenTheStepsArriveWithEnumNames_ShouldApplyThemInsteadOfTheDefaultTemplate` | `AdvancedRenamer_MethodStepsPipeline_ShouldExecuteCorrectly` |
| `portapapeles-sin-vigilante` | `FileFlow.Tests/TestHelpers/TestCollectionContractAnalyzer.cs` | `TestCollectionContractGuardTests` | `Analyzer_ShouldRequireTheExampleFlowBank_WhenClassMovesTheProcessWorkingDirectory` |
| `proyeccion-uno-sin-guardia` | `FileFlow.Tests/TestHelpers/UnoGeometryBindingScanner.cs` | `UnoGeometryBindingGuardTests` | `Analyzer_ShouldRequireNodeClipboard_WhenClassExercisesTheProcessClipboard` |
| `prueba-sincrona-en-hilo-de-ui` | `FileFlow.App.Core/ViewModels/NodeInspectorViewModel.cs` | `TestNodeWithCustomFileAsync_ShouldPickThroughTheAsyncDialogVariant` | `InspectNode_ShouldComputeMetadataDiff_WhenInputAndOutputSnapshotsExist` |
| `punto-de-control-sobrevive-a-la-ejecucion` | `FileFlow.Core/Engine/WorkflowCheckpointHandler.cs` | `WorkflowExecutor_ReusedForASecondRun_ShouldProcessEveryFileAgain` | `CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile` |
| `punto-de-control-vuelve-a-escribir-por-archivo` | `FileFlow.Core/Engine/WorkflowCheckpointHandler.cs` | `CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile` | `CheckpointManager_SaveRetrieveAndClear_OperatesCorrectly` |
| `retroalimentacion-de-barrera-contada-como-ciclo` | `FileFlow.Core/Engine/GraphValidator.cs` | `Validate_ShouldAcceptABranchReturningToABarrierNode` | `Validate_ShouldFail_WhenGraphContainsCycle` |
| `retroalimentacion-sin-avisos` | `FileFlow.Core/Engine/GraphValidator.cs` | `Validate_ShouldWarnWhenANodeIsOnlyFedByFeedbackPorts` | `Validate_ShouldAcceptABranchReturningToABarrierNode` |
| `salida-global-sin-expandir` | `FileFlow.Sdk/ParameterHelper.cs` | `VariableTemplateResolver_WithATemplateOutputFolder_ResolvesAFolderInAnyParameter|ResolveOutputPath_WithGlobalOutputDirDeclaredAsATemplate_ExpandsAndAnchorsItUnderTheSourceRoot|AFlowWhoseOutputFolderIsATemplate_ShouldAnchorTheArchiveInARealFolder|WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin|CsvExportNode_WithTheFlowFolderToken_ShouldWriteInsideTheFolderTheFlowDeclares|CsvExportNode_WithAnAliasOfTheFlowFolder_ShouldWriteWhereTheCanonicalTokenWrites|ExcelReportGeneratorNode_WithATemplateFlowFolder_ShouldWriteTheReportInsideTheOrigin` | `ResolveOutputPath_WithRelativePath_AnchorsUnderGlobalOutputDir|ResolveOutputPath_WithoutGlobalOutputDir_AnchorsUnderSourceDirectory|VariableTemplateResolver_WithoutExplicitMetadata_FallsBackToAppPathsDefault` |
| `snapshots-congelados-en-el-panel` | `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | `InspectorPanel_ShouldBuildSnapshotTabsFromTheNodeCollectionsAndTheCoreDiff` | `InspectorPanel_ShouldWireTheTestButtonThroughTheCanonicalCoreCommand` |
| `sondeo-uia-sin-hijo-externo` | `FileFlow.App.Uno/SelfCheckUia.cs` | `TheExternalUiaProbeMode_ShouldBeWired_WithTheHouseInstrumentAndHonestVerdicts` | `TheUiAnchorTable_ShouldCoverTheObservableSurface` |
| `stderr-del-cli-que-se-desvanece` | `FileFlow.Plugin.Integrations/CliExecutionNode.cs` | `CliExecutionNode_WhenExitCodeNonZero_ShouldEmitFailedAndCaptureStdErr` | `PdfTextExtractorNode_ExtractsTextSuccessfully` |
| `struct-de-shell-desalineado` | `FileFlow.Core/Platform/WindowsPlatformService.cs` | `WindowsShellFileOperationLayoutTests` | `ForkJoinBarrierNodeTests` |
| `tabla-en-cache-sin-mirar-el-tamano` | `FileFlow.Plugin.Data/Nodes/Processing/DataLookupTableLoader.cs` | `TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp` | `TheLookupTableInMemory_ShouldBeTheOneOfThisRun` |
| `tabla-en-cache-sin-tope` | `FileFlow.Plugin.Data/Nodes/Processing/DataLookupTableLoader.cs` | `TheTableCache_ShouldNotGrowWithoutBoundAcrossRuns` | `TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp` |
| `tarjeta-que-no-habla-por-su-color` | `FileFlow.App.Core/Services/PortPalette.cs` | `SocketMatrixTests` | `UnoGeometryBindingGuardTests` |
| `tema-sin-repintado` | `FileFlow.App.Uno/Platform/UnoThemeHost.cs` | `UnoThemeRepaintGuardTests` | `ThemeHost_ShouldKeepCreatingMissingBrushes_AndThePortableGenerator` |
| `texto-del-pdf-que-se-olvida` | `FileFlow.Plugin.Documents/PdfTextExtractorNode.cs` | `PdfTextExtractorNode_ExtractsTextSuccessfully` | `PdfSplitNode_SplitsMultiplePagePdf` |
| `token-de-tema-que-desaparece` | `FileFlow.App/Services/ThemeResourceApplier.cs` | `ThemeTokenCompletenessTests` | `DisabledStateLintTests` |
| `toolbox-sin-filtro` | `FileFlow.App.Core/ViewModels/ToolboxViewModel.cs` | `ToolboxViewModel_SearchText_ShouldReduceTheCatalogueToMatchingNodes` | `ToolboxViewModel_SearchText_ShouldExpandMatchingCategories` |
| `validador-sin-materializar-puertos` | `FileFlow.Core/Engine/GraphValidator.cs` | `GraphValidatorDynamicPortTests` | `GraphValidatorTests` |

## Subsistemas del producto sin ninguna mutación declarada (2 de 17)

Una mutación por comportamiento que importa; estos proyectos no tienen ninguna, así que ningún
defecto declarado demuestra que sus pruebas muerdan. Es la lista de trabajo, no un reproche.

- `FileFlow.Plugin.Scripting`
- `FileFlow.Plugin.Subflows`

## Guardias del repositorio sin ninguna mutación que las muerda (29 de 38)

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
- `FileFlow.Tests/Unit/App/UnoInteractionParityGuardTests.cs`
- `FileFlow.Tests/Unit/App/UnoToolboxPanelGuardTests.cs`
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

- **FileFlow.App**: `enlace-de-geometria-sin-proyeccion`, `token-de-tema-que-desaparece`
- **FileFlow.App.Core**: `cable-con-la-curva-al-reves`, `inspector-sin-write-back`, `latido-rearrancado-que-no-late`, `prueba-sincrona-en-hilo-de-ui`, `tarjeta-que-no-habla-por-su-color`, `toolbox-sin-filtro`
- **FileFlow.App.Uno**: `cable-con-anclas-estimadas`, `cables-que-no-llegan-tarde`, `decoradores-que-no-llegan-al-arbol`, `lienzo-sin-peer-uia`, `snapshots-congelados-en-el-panel`, `sondeo-uia-sin-hijo-externo`, `tema-sin-repintado`
- **FileFlow.Core**: `diario-acumula-ejecuciones`, `ejecucion-pausada-heredada`, `modo-virtual-heredado`, `punto-de-control-sobrevive-a-la-ejecucion`, `punto-de-control-vuelve-a-escribir-por-archivo`, `retroalimentacion-de-barrera-contada-como-ciclo`, `retroalimentacion-sin-avisos`, `struct-de-shell-desalineado`, `validador-sin-materializar-puertos`
- **FileFlow.Plugin.AI**: `cache-de-embeddings-sin-entorno`, `carpeta-de-nodo-de-ia-donde-corre`, `modelo-clip-buscado-por-su-id`
- **FileFlow.Plugin.Archives**: `archivo-vacio-dejado-atras`, `compresor-contra-su-propia-entrada`, `compresor-que-escribe-donde-corre`
- **FileFlow.Plugin.Data**: `datos-solo-el-token-canonico`, `tabla-en-cache-sin-mirar-el-tamano`, `tabla-en-cache-sin-tope`
- **FileFlow.Plugin.Documents**: `texto-del-pdf-que-se-olvida`
- **FileFlow.Plugin.FileSystem**: `censo-de-puertos-ignora-un-puerto-nuevo`, `limpiador-borra-por-su-cuenta`, `limpiador-vuelve-a-mirar-el-disco`
- **FileFlow.Plugin.Hashing**: `indice-de-hashes-heredado`
- **FileFlow.Plugin.Images**: `identidad-perdida-al-optimizar`
- **FileFlow.Plugin.Integrations**: `stderr-del-cli-que-se-desvanece`
- **FileFlow.Plugin.Logic**: `bufer-de-lotes-heredado`, `lote-incompleto-perdido-al-terminar`
- **FileFlow.Plugin.Network**: `dry-run-que-entrega-el-disparador`
- **FileFlow.Sdk**: `borrado-virtual-solo-ve-archivos`, `pasos-de-renombrado-ilegibles`, `salida-global-sin-expandir`
- **FileFlow.Tests (infraestructura de pruebas)**: `catalogo-sin-carpeta-de-destino`, `censo-de-puertos-sin-su-asiento`, `contrato-de-colecciones-sin-su-regla`, `indice-de-pruebas-ciego-al-cr`, `portapapeles-sin-vigilante`, `proyeccion-uno-sin-guardia`

Las que mutan la **declaración** de una guardia (el censo, el analizador) cuentan como infraestructura de pruebas, no como subsistema del producto: son 6.
