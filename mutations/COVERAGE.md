# Cobertura de mutaciones

> Generado por `MutationDeclarationCoverageTests` a partir de `mutations/*.json` y del árbol de fuentes.
> **No se edita a mano.**
> Regenerar: `FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests`.

Mutaciones declaradas: 96
Subsistemas del producto con alguna mutación: 15 de 17
Guardias que auditan el repositorio con mutación que las muerda: 16 de 44

## Qué declara cada mutación

| Mutación | Fichero que muta | Testigo | Control |
| :--- | :--- | :--- | :--- |
| `accion-de-urls-que-descarga-el-modelo` | `FileFlow.App.Uno/Controls/SettingsPanel.xaml.cs` | `TheModelUrlAction_ShouldOpenTheServedEditor_AndWriteWhereTheDesktopWrites` | `TheSixSections_ShouldBeDeclared_WithTheirPaneAndTheirButton` |
| `ajuste-que-no-devuelve-el-idioma` | `FileFlow.App.Uno/Controls/SettingsPanel.xaml.cs` | `TheRestore_ShouldReturnWhatTheUserHad_NotAConstant` | `ThePersistence_ShouldBeTheViewModelsOwnCommands` |
| `ajuste-sin-su-texto` | `FileFlow.App.Uno/Resources/Strings.resx` | `EveryCitedUnoKey_ShouldExistInBothDictionaries` | `TheProbe_ShouldHaveItsOwnMode_OutsideTheCanvasProbes` |
| `ancla-que-ignora-la-escala` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt` | `TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor` |
| `archivo-vacio-dejado-atras` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorAskedForAContainerItsWriterRejects_ShouldLeaveNoFileBehind` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive` |
| `arranque-que-no-aplica-el-tema-guardado` | `FileFlow.App.Uno/App.xaml.cs` | `TheStartup_ShouldApplyTheSavedThemeAndLanguage` | `TheHostSurface_ShouldBeWiredToThePortableSettingsViewModel` |
| `atajo-que-no-llega-sin-foco` | `FileFlow.App.Uno/MainWindow.xaml.cs` | `TheWindow_ShouldRouteTheKeysNobodyConsumed_ToTheCanvas` | `TheShortcutCourtesies_ShouldKeepEveryOtherKeyboardOwner` |
| `aviso-de-actualizacion-que-nadie-enciende` | `FileFlow.App.Uno/App.xaml.cs` | `TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` |
| `borrado-virtual-solo-ve-archivos` | `FileFlow.Sdk/Storage/VirtualStorageService.cs` | `VirtualStorageService_EnumerationAndDirectoryDeletion_ShouldOperateInTheStore` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `bufer-de-lotes-heredado` | `FileFlow.Plugin.Logic/BatchBufferNode.cs` | `ExecuteAsync_WhenTheSameInstanceSeesAnotherExecution_ShouldNotMixThePendingItemsOfThePreviousOne` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `cable-con-anclas-estimadas` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `DrawWires_ShouldConsumeTheWrittenBackAnchors` | `Canvas_ShouldSubscribeToConnectionsCollectionChanged` |
| `cable-con-la-curva-al-reves` | `FileFlow.App.Core/Services/ConnectionGeometry.cs` | `ConnectionGeometryTests` | `EditorViewportCalculatorTests` |
| `cable-de-escritorio-por-el-control-de-nodify` | `FileFlow.App/Views/EditorView.axaml` | `EveryCableOfTheCanvas_ShouldBeDrawnWithTheCoreGeometry_NotWithTheNodifyControl` | `TheCable_ShouldBeTheCoreCurve_BetweenItsTwoAnchors` |
| `cable-que-no-toca-su-socket` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor` | `TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt` |
| `cables-que-no-llegan-tarde` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `UnoCanvasConnectionsGuardTests` | `Canvas_ShouldKeepTheNodesSubscriptionAlongsideConnections` |
| `cache-de-embeddings-sin-entorno` | `FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs` | `AVectorFromAWorldWithoutModel_ShouldNotAnswerOnceTheModelArrives` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `campo-que-no-muestra-lo-que-el-dialogo-escribio` | `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | `EveryTextRow_ShouldFollowTheParameter_BothWays` | `EveryRowAction_ShouldBeDrawnOnTheSameRowsAsTheDesktop` |
| `carpeta-de-nodo-de-ia-donde-corre` | `FileFlow.Plugin.AI/Common/NodeOutputDirectory.cs` | `WithNoFolderAtAll_ShouldUseTheProductsTempFolder_NotTheProcessWorkingDirectory|WithNothingDeclared_ShouldUseTheItemFolder` | `WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin|WithADeclaredFolder_ShouldUseIt_AnchoringItInTheFlowFolderWhenRelative` |
| `catalogo-sin-carpeta-de-destino` | `docs/examples/01_basic/flow_08_compresion_zip_automatica.json` | `EveryCompressorInTheCatalog_ShouldSayWhereTheArchiveGoes` | `AllExampleFlows_ShouldBeWrittenByTheProductWriter` |
| `censo-de-puertos-ignora-un-puerto-nuevo` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `NodePortCoverageGuardTests` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `censo-de-puertos-sin-su-asiento` | `FileFlow.Tests/TestHelpers/NodePortInventory.cs` | `NodePortCoverageGuardTests` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `compresor-contra-su-propia-entrada` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorWhoseDestinationIsItsOwnInput_ShouldRefuseInsteadOfTruncatingTheInput|ExampleFlowsEndToEndTests` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive` |
| `compresor-que-escribe-donde-corre` | `FileFlow.Plugin.Archives/ArchiveCompressorNode.cs` | `ACompressorWithoutADestination_ShouldWriteInTheFlowsOutputFolder_AndSaySo|AFlowSavedWithoutADestination_ShouldWriteInTheOutputFolderTheLauncherHands` | `ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive|TheFactoryDefaultOfTheCompressor_ShouldBeTheOutputFolderOfTheFlow` |
| `conmutador-de-parametros-que-no-refresca` | `FileFlow.App.Uno/Controls/NodeCardViewModel.cs` | `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive` | `TheMediaPresetManager_ShouldBeDeclaredByTheNode_AndPaintedByBothHosts` |
| `contrato-de-colecciones-sin-su-regla` | `FileFlow.Tests/TestHelpers/TestCollectionContractAnalyzer.cs` | `TestCollectionContractGuardTests` | `Analyzer_ShouldRequireLocalization_WhenClassUsesRealUserPreferences` |
| `cuello-que-no-cabe-en-el-hueco` | `FileFlow.App.Core/Services/ConnectionGeometry.cs` | `TheNeck_ShouldNeverOvershootTheHueco_EvenWhenTheNodesAreCupped` | `TheTrace_ShouldStartAndEndAtTheAnchors_WithoutStraightBars` |
| `cuerpo-del-gestor-que-habla-con-el-almacen` | `FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml.cs` | `TheMediaPresetManager_ShouldBeDeclaredByTheNode_AndPaintedByBothHosts` | `EveryTextUsedByTheDialogs_ShouldExistInBothHostDictionaries` |
| `datos-solo-el-token-canonico` | `FileFlow.Plugin.Data/Nodes/Exporters/CsvExportNode.cs` | `CsvExportNode_WithAnAliasOfTheFlowFolder_ShouldWriteWhereTheCanonicalTokenWrites` | `CsvExportNode_WithTheFlowFolderToken_ShouldWriteInsideTheFolderTheFlowDeclares` |
| `decoradores-que-no-llegan-al-arbol` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `UnoCanvasDecoratorsGuardTests` | `Canvas_ShouldKeepTheNodeAndWireSubscriptionsAlongsideDecorators` |
| `diario-acumula-ejecuciones` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun` | `ExecutionJournal_Rollback_RestoresMovedFile` |
| `disenador-de-datasets-fuera-del-catalogo` | `FileFlow.App.Uno/Platform/UnoWindowService.cs` | `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` |
| `disenador-que-borra-sin-preguntar` | `FileFlow.Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs` | `DeleteDataSetCommand_WhenRefused_ShouldLeaveTheDataSetAlone` | `DeleteDataSetCommand_WhenConfirmed_ShouldRemoveTheCustomDataSet` |
| `dry-run-que-entrega-el-disparador` | `FileFlow.Plugin.Network/Transports/HttpTransportStrategy.cs` | `NetworkDownloadNode_Http_DryRun_ShouldSimulateAndEmitOut` | `CliExecutionNode_WhenExitCodeNonZero_ShouldEmitFailedAndCaptureStdErr` |
| `editor-que-no-devuelve-el-texto-confirmado` | `FileFlow.App.Uno/Platform/UnoWindowService.cs` | `TheDialogs_ShouldBeViewsOfThePortableViewModels_NotCopiesOfThem` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` |
| `ejecucion-pausada-heredada` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `ARunThatEndedWhilePaused_ShouldNotLeaveTheNextOneWaiting` | `Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun` |
| `enlace-de-geometria-sin-proyeccion` | `FileFlow.App/Views/EditorView.axaml` | `GeometryBindingProjectionTests` | `TheConnectionTemplate_ShouldDefineStandardConnectionWire` |
| `entrada-de-ventana-que-no-ejecuta-su-orden` | `FileFlow.App.Uno/Controls/MainMenuDrawer.xaml.cs` | `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` |
| `estudio-de-temas-que-esconde-lo-que-no-sirve` | `FileFlow.App.Uno/Controls/ThemeCustomizerBody.xaml.cs` | `TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` |
| `extension-de-preset-sin-punto-al-guardar` | `FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | `Saving_ShouldNormalizeTheExtension` | `Saving_ShouldWriteThroughTheStore_NotOnlyInTheList` |
| `fila-de-presets-sin-su-boton` | `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | `EveryDesktopRowDialog_ShouldBeServedHere_OrDeclaredPending` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` |
| `fila-de-variables-que-abre-el-menu-que-no-esta-portado` | `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | `EveryDesktopRowDialog_ShouldBeServedHere_OrDeclaredPending` | `EveryRowAction_ShouldBeDrawnOnTheSameRowsAsTheDesktop` |
| `flujo-que-se-cumple-por-el-picker-sincrono` | `FileFlow.App.Uno/MainWindow.xaml.cs` | `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` | `EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt` |
| `gestor-de-presets-que-guarda-solo-en-su-copia` | `FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | `Saving_ShouldWriteThroughTheStore_NotOnlyInTheList` | `TheManager_ShouldOpenWithTheStoreCatalog_AndTheFirstOneChosen` |
| `gestor-que-borra-los-presets-del-sistema` | `FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | `DeletingASystemPreset_ShouldWarn_AndNotDelete` | `DeletingAUserPreset_ShouldAskFirst_AndDeleteWhenConfirmed` |
| `gestor-que-borra-sin-preguntar` | `FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | `DeletingAUserPreset_ShouldNotDelete_WhenNotConfirmed` | `DeletingASystemPreset_ShouldWarn_AndNotDelete` |
| `hit-test-en-el-espacio-equivocado` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `EveryHitTestCall_ShouldCrossToTheHostSpace_ThroughTheSingleConversionHelper` | `TheClickAreaProbe_ShouldMeasureTheDrawnGeometry_ThroughTheHandlersOwnHitTest` |
| `identidad-perdida-al-optimizar` | `FileFlow.Plugin.Images/ImageOptimizerNode.cs` | `ExecuteAsync_WithARealImage_ShouldKeepTheIdentityOfTheItemThatEntered` | `ExecuteAsync_WhenInputIsWebP_ShouldDecodeAndOptimizeSuccessfully` |
| `indice-de-hashes-heredado` | `FileFlow.Plugin.Hashing/DeduplicationFilterNode.cs` | `TheHashIndexOfOneRun_ShouldNotClassifyTheFilesOfTheNext` | `TheBatchBuffer_ShouldNotKeepPendingItemsForTheNextRun` |
| `indice-de-pruebas-ciego-al-cr` | `FileFlow.Tests/TestHelpers/TestSuiteIndex.cs` | `TestSuiteIndexTests` | `SourceTextTests` |
| `inspector-sin-write-back` | `FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs` | `EditingParameterThroughTheViewModel_ShouldWriteThroughToTheNodeInstance` | `ToolboxViewModel_SearchText_ShouldExpandMatchingCategories` |
| `latido-rearrancado-que-no-late` | `FileFlow.App.Core/Services/HeartbeatService.cs` | `AStoppedBeat_ShouldStopDelivering_AndResumeWhenStartedAgain` | `TheRegistry_ShouldNotAdmitTwoBeatsWithTheSameName` |
| `lienzo-sin-peer-uia` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `UnoAutomationSurfaceGuardTests` | `TheZoomBar_ShouldExposeItsAnchor_ItsLevel_AndItsThreeButtons` |
| `limpiador-borra-por-su-cuenta` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `AnEmptyDirectoryCleanerWhoseStorageFailsToDelete` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `limpiador-vuelve-a-mirar-el-disco` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `VirtualEmptyFolderCleanupIntegrationTests` | `EmptyDirectoryCleaner_RegistersDeletedPermanentlyJournalEntry` |
| `lote-incompleto-perdido-al-terminar` | `FileFlow.Plugin.Logic/BatchBufferNode.cs` | `OnWorkflowCompleted_WhenTheBatchDidNotFill_ShouldDeliverItAndCloseItWithItsMarker` | `ExecuteAsync_WhenBatchSizeReached_ShouldEmitBufferedItemsAndCompletionMarker` |
| `menu-que-ejecuta-la-orden-de-otro` | `FileFlow.App.Uno/Controls/ControlBar.xaml.cs` | `EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt` | `EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState` |
| `menu-que-no-declara-lo-que-falta` | `FileFlow.App.Uno/Controls/ControlBar.xaml.cs` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` | `EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt` |
| `menu-que-no-declara-un-atajo` | `FileFlow.App.Uno/Controls/ControlBar.xaml.cs` | `EveryDesktopShortcut_ShouldBeRoutedHere_OrDeclaredUnrouted` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` |
| `menu-sin-el-estado-de-su-contexto` | `FileFlow.App.Uno/Controls/ControlBar.xaml` | `EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` |
| `modelo-clip-buscado-por-su-id` | `FileFlow.Plugin.AI/Inference/ClipEmbeddingDatabase.cs` | `AFileNamedWithTheModelId_ShouldNotCountAsTheDownloadedModel` | `TheSamePrompt_ShouldOnlyBeComputedOnce` |
| `modo-virtual-heredado` | `FileFlow.Core/Engine/WorkflowExecutor.cs` | `ASyntheticRun_ShouldNotLeaveTheNextOneInVirtualMode` | `VirtualEmptyFolderCleanupIntegrationTests` |
| `nodo-que-declara-su-superficie-sin-clave` | `FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs` | `TheDataSetDesigner_ShouldBeDeclaredByTheNode_AndServedByTheHost` | `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` |
| `orden-destructiva-que-vuelve-a-la-via-sincrona` | `FileFlow.App.Core/ViewModels/ControlBarViewModel.cs` | `EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne` | `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` |
| `panel-de-nodo-sin-su-servicio-de-ventanas` | `FileFlow.App.Uno/App.xaml.cs` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` | `TheDialogs_ShouldBeViewsOfThePortableViewModels_NotCopiesOfThem` |
| `pasos-de-renombrado-ilegibles` | `FileFlow.Sdk/Renaming/RenamerPresetService.cs` | `AdvancedRenamer_WhenTheStepsArriveWithEnumNames_ShouldApplyThemInsteadOfTheDefaultTemplate` | `AdvancedRenamer_MethodStepsPipeline_ShouldExecuteCorrectly` |
| `portapapeles-sin-vigilante` | `FileFlow.Tests/TestHelpers/TestCollectionContractAnalyzer.cs` | `TestCollectionContractGuardTests` | `Analyzer_ShouldRequireTheExampleFlowBank_WhenClassMovesTheProcessWorkingDirectory` |
| `pregunta-de-borrado-por-la-via-sincrona` | `FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | `DeletingAUserPreset_ShouldNotDelete_WhenNotConfirmed` | `Resetting_ShouldAsk_AndResetTheStore` |
| `pregunta-destructiva-escrita-en-el-codigo` | `FileFlow.App.Core/ViewModels/AiModelManagerViewModel.cs` | `EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne` | `TheSixSections_ShouldBeDeclared_WithTheirPaneAndTheirButton` |
| `proyeccion-uno-sin-guardia` | `FileFlow.Tests/TestHelpers/UnoGeometryBindingScanner.cs` | `UnoGeometryBindingGuardTests` | `Analyzer_ShouldRequireNodeClipboard_WhenClassExercisesTheProcessClipboard` |
| `prueba-sincrona-en-hilo-de-ui` | `FileFlow.App.Core/ViewModels/NodeInspectorViewModel.cs` | `TestNodeWithCustomFileAsync_ShouldPickThroughTheAsyncDialogVariant` | `InspectNode_ShouldComputeMetadataDiff_WhenInputAndOutputSnapshotsExist` |
| `punto-de-control-sobrevive-a-la-ejecucion` | `FileFlow.Core/Engine/WorkflowCheckpointHandler.cs` | `WorkflowExecutor_ReusedForASecondRun_ShouldProcessEveryFileAgain` | `CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile` |
| `punto-de-control-vuelve-a-escribir-por-archivo` | `FileFlow.Core/Engine/WorkflowCheckpointHandler.cs` | `CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile` | `CheckpointManager_SaveRetrieveAndClear_OperatesCorrectly` |
| `reclamacion-que-roba-al-cuadro-de-texto` | `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | `TheCanvas_ShouldReclaimTheKeyboard_FromAForeignOwnerAfterAClick_WithItsCourtesies` | `TheCanvas_ShouldResolveEveryShortcutInOnePlace` |
| `retroalimentacion-de-barrera-contada-como-ciclo` | `FileFlow.Core/Engine/GraphValidator.cs` | `Validate_ShouldAcceptABranchReturningToABarrierNode` | `Validate_ShouldFail_WhenGraphContainsCycle` |
| `retroalimentacion-sin-avisos` | `FileFlow.Core/Engine/GraphValidator.cs` | `Validate_ShouldWarnWhenANodeIsOnlyFedByFeedbackPorts` | `Validate_ShouldAcceptABranchReturningToABarrierNode` |
| `salida-global-sin-expandir` | `FileFlow.Sdk/ParameterHelper.cs` | `VariableTemplateResolver_WithATemplateOutputFolder_ResolvesAFolderInAnyParameter|ResolveOutputPath_WithGlobalOutputDirDeclaredAsATemplate_ExpandsAndAnchorsItUnderTheSourceRoot|AFlowWhoseOutputFolderIsATemplate_ShouldAnchorTheArchiveInARealFolder|WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin|CsvExportNode_WithTheFlowFolderToken_ShouldWriteInsideTheFolderTheFlowDeclares|CsvExportNode_WithAnAliasOfTheFlowFolder_ShouldWriteWhereTheCanonicalTokenWrites|ExcelReportGeneratorNode_WithATemplateFlowFolder_ShouldWriteTheReportInsideTheOrigin` | `ResolveOutputPath_WithRelativePath_AnchorsUnderGlobalOutputDir|ResolveOutputPath_WithoutGlobalOutputDir_AnchorsUnderSourceDirectory|VariableTemplateResolver_WithoutExplicitMetadata_FallsBackToAppPathsDefault` |
| `seccion-que-pierde-el-panel-que-conmutaba` | `FileFlow.App.Uno/Controls/SettingsPanel.xaml.cs` | `TheSixSections_ShouldBeDeclared_WithTheirPaneAndTheirButton` | `ThePersistence_ShouldBeTheViewModelsOwnCommands` |
| `snapshots-congelados-en-el-panel` | `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | `InspectorPanel_ShouldBuildSnapshotTabsFromTheNodeCollectionsAndTheCoreDiff` | `InspectorPanel_ShouldWireTheTestButtonThroughTheCanonicalCoreCommand` |
| `sondeo-uia-sin-hijo-externo` | `FileFlow.App.Uno/SelfCheckUia.cs` | `TheExternalUiaProbeMode_ShouldBeWired_WithTheHouseInstrumentAndHonestVerdicts` | `TheUiAnchorTable_ShouldCoverTheObservableSurface` |
| `stderr-del-cli-que-se-desvanece` | `FileFlow.Plugin.Integrations/CliExecutionNode.cs` | `CliExecutionNode_WhenExitCodeNonZero_ShouldEmitFailedAndCaptureStdErr` | `PdfTextExtractorNode_ExtractsTextSuccessfully` |
| `struct-de-shell-desalineado` | `FileFlow.Core/Platform/WindowsPlatformService.cs` | `WindowsShellFileOperationLayoutTests` | `ForkJoinBarrierNodeTests` |
| `superficie-de-presets-sin-la-accion-que-sustituye` | `FileFlow.Plugin.Integrations/MediaTranscoderNode.cs` | `TheMediaPresetManager_ShouldBeDeclaredByTheNode_AndPaintedByBothHosts` | `TheManager_ShouldOpenWithTheStoreCatalog_AndTheFirstOneChosen` |
| `superficie-declarada-sin-los-dialogos-del-host` | `FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs` | `EveryDeclaredSurface_ShouldCarryTheHostDialogs_SoItsDestructiveOrdersCanAskForReal` | `TheMediaPresetManager_ShouldBeDeclaredByTheNode_AndPaintedByBothHosts` |
| `tabla-en-cache-sin-mirar-el-tamano` | `FileFlow.Plugin.Data/Nodes/Processing/DataLookupTableLoader.cs` | `TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp` | `TheLookupTableInMemory_ShouldBeTheOneOfThisRun` |
| `tabla-en-cache-sin-tope` | `FileFlow.Plugin.Data/Nodes/Processing/DataLookupTableLoader.cs` | `TheTableCache_ShouldNotGrowWithoutBoundAcrossRuns` | `TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp` |
| `tarjeta-que-no-habla-por-su-color` | `FileFlow.App.Core/Services/PortPalette.cs` | `SocketMatrixTests` | `UnoGeometryBindingGuardTests` |
| `tarjeta-sin-la-puerta-de-sus-parametros` | `FileFlow.App.Uno/Controls/NodeCardView.xaml` | `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive` | `EveryTextUsedByTheDialogs_ShouldExistInBothHostDictionaries` |
| `tema-sin-repintado` | `FileFlow.App.Uno/Platform/UnoThemeHost.cs` | `UnoThemeRepaintGuardTests` | `ThemeHost_ShouldKeepCreatingMissingBrushes_AndThePortableGenerator` |
| `texto-del-pdf-que-se-olvida` | `FileFlow.Plugin.Documents/PdfTextExtractorNode.cs` | `PdfTextExtractorNode_ExtractsTextSuccessfully` | `PdfSplitNode_SplitsMultiplePagePdf` |
| `toggle-que-no-persiste` | `FileFlow.App.Core/ViewModels/ToolboxViewModel.cs` | `ToolboxViewModel_ToggleViewMode_ShouldPersistCompactMode` | `ToolboxViewModel_SearchText_ShouldExpandMatchingCategories` |
| `token-de-tema-que-desaparece` | `FileFlow.App/Services/ThemeResourceApplier.cs` | `ThemeTokenCompletenessTests` | `DisabledStateLintTests` |
| `toolbox-sin-filtro` | `FileFlow.App.Core/ViewModels/ToolboxViewModel.cs` | `ToolboxViewModel_SearchText_ShouldReduceTheCatalogueToMatchingNodes` | `ToolboxViewModel_SearchText_ShouldExpandMatchingCategories` |
| `validador-sin-materializar-puertos` | `FileFlow.Core/Engine/GraphValidator.cs` | `GraphValidatorDynamicPortTests` | `GraphValidatorTests` |
| `ventana-servida-que-no-esta-en-el-catalogo` | `FileFlow.App.Uno/Platform/UnoWindowService.cs` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` | `EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState` |
| `vfs-que-se-vacia-sin-preguntar` | `FileFlow.App.Core/ViewModels/VirtualFileSystemExplorerViewModel.cs` | `ClearVirtualFileSystemCommand_WhenRefused_ShouldLeaveTheStoreAlone` | `ClearVirtualFileSystemCommand_WhenConfirmed_ClearsStore` |
| `vista-del-disenador-con-su-propio-modelo` | `FileFlow.App.Uno/Controls/DataSetDesignerBody.xaml.cs` | `TheDataSetDesigner_ShouldBeDeclaredByTheNode_AndServedByTheHost` | `EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState` |

## Subsistemas del producto sin ninguna mutación declarada (2 de 17)

Una mutación por comportamiento que importa; estos proyectos no tienen ninguna, así que ningún
defecto declarado demuestra que sus pruebas muerdan. Es la lista de trabajo, no un reproche.

- `FileFlow.Plugin.Scripting`
- `FileFlow.Plugin.Subflows`

## Guardias del repositorio sin ninguna mutación que las muerda (28 de 44)

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
- `FileFlow.Tests/Unit/Views/SelectorBindingGuardTests.cs`
- `FileFlow.Tests/Unit/Views/SettingsTabsCoverageTests.cs`
- `FileFlow.Tests/Unit/Views/ThemeStudioVisualContractTests.cs`
- `FileFlow.Tests/Unit/Views/ThemeTokenOverwriteGuardTests.cs`
- `FileFlow.Tests/Unit/Views/WindowActivationContractTests.cs`

## Dónde muta cada declaración

- **FileFlow.App**: `cable-de-escritorio-por-el-control-de-nodify`, `enlace-de-geometria-sin-proyeccion`, `token-de-tema-que-desaparece`
- **FileFlow.App.Core**: `cable-con-la-curva-al-reves`, `cuello-que-no-cabe-en-el-hueco`, `inspector-sin-write-back`, `latido-rearrancado-que-no-late`, `orden-destructiva-que-vuelve-a-la-via-sincrona`, `pregunta-destructiva-escrita-en-el-codigo`, `prueba-sincrona-en-hilo-de-ui`, `tarjeta-que-no-habla-por-su-color`, `toggle-que-no-persiste`, `toolbox-sin-filtro`, `vfs-que-se-vacia-sin-preguntar`
- **FileFlow.App.Uno**: `accion-de-urls-que-descarga-el-modelo`, `ajuste-que-no-devuelve-el-idioma`, `ajuste-sin-su-texto`, `ancla-que-ignora-la-escala`, `arranque-que-no-aplica-el-tema-guardado`, `atajo-que-no-llega-sin-foco`, `aviso-de-actualizacion-que-nadie-enciende`, `cable-con-anclas-estimadas`, `cable-que-no-toca-su-socket`, `cables-que-no-llegan-tarde`, `campo-que-no-muestra-lo-que-el-dialogo-escribio`, `conmutador-de-parametros-que-no-refresca`, `cuerpo-del-gestor-que-habla-con-el-almacen`, `decoradores-que-no-llegan-al-arbol`, `disenador-de-datasets-fuera-del-catalogo`, `editor-que-no-devuelve-el-texto-confirmado`, `entrada-de-ventana-que-no-ejecuta-su-orden`, `estudio-de-temas-que-esconde-lo-que-no-sirve`, `fila-de-presets-sin-su-boton`, `fila-de-variables-que-abre-el-menu-que-no-esta-portado`, `flujo-que-se-cumple-por-el-picker-sincrono`, `hit-test-en-el-espacio-equivocado`, `lienzo-sin-peer-uia`, `menu-que-ejecuta-la-orden-de-otro`, `menu-que-no-declara-lo-que-falta`, `menu-que-no-declara-un-atajo`, `menu-sin-el-estado-de-su-contexto`, `panel-de-nodo-sin-su-servicio-de-ventanas`, `reclamacion-que-roba-al-cuadro-de-texto`, `seccion-que-pierde-el-panel-que-conmutaba`, `snapshots-congelados-en-el-panel`, `sondeo-uia-sin-hijo-externo`, `tarjeta-sin-la-puerta-de-sus-parametros`, `tema-sin-repintado`, `ventana-servida-que-no-esta-en-el-catalogo`, `vista-del-disenador-con-su-propio-modelo`
- **FileFlow.Core**: `diario-acumula-ejecuciones`, `ejecucion-pausada-heredada`, `modo-virtual-heredado`, `punto-de-control-sobrevive-a-la-ejecucion`, `punto-de-control-vuelve-a-escribir-por-archivo`, `retroalimentacion-de-barrera-contada-como-ciclo`, `retroalimentacion-sin-avisos`, `struct-de-shell-desalineado`, `validador-sin-materializar-puertos`
- **FileFlow.Plugin.AI**: `cache-de-embeddings-sin-entorno`, `carpeta-de-nodo-de-ia-donde-corre`, `modelo-clip-buscado-por-su-id`
- **FileFlow.Plugin.Archives**: `archivo-vacio-dejado-atras`, `compresor-contra-su-propia-entrada`, `compresor-que-escribe-donde-corre`
- **FileFlow.Plugin.Data**: `datos-solo-el-token-canonico`, `tabla-en-cache-sin-mirar-el-tamano`, `tabla-en-cache-sin-tope`
- **FileFlow.Plugin.Documents**: `texto-del-pdf-que-se-olvida`
- **FileFlow.Plugin.FileSystem**: `censo-de-puertos-ignora-un-puerto-nuevo`, `disenador-que-borra-sin-preguntar`, `limpiador-borra-por-su-cuenta`, `limpiador-vuelve-a-mirar-el-disco`, `nodo-que-declara-su-superficie-sin-clave`, `superficie-declarada-sin-los-dialogos-del-host`
- **FileFlow.Plugin.Hashing**: `indice-de-hashes-heredado`
- **FileFlow.Plugin.Images**: `identidad-perdida-al-optimizar`
- **FileFlow.Plugin.Integrations**: `extension-de-preset-sin-punto-al-guardar`, `gestor-de-presets-que-guarda-solo-en-su-copia`, `gestor-que-borra-los-presets-del-sistema`, `gestor-que-borra-sin-preguntar`, `pregunta-de-borrado-por-la-via-sincrona`, `stderr-del-cli-que-se-desvanece`, `superficie-de-presets-sin-la-accion-que-sustituye`
- **FileFlow.Plugin.Logic**: `bufer-de-lotes-heredado`, `lote-incompleto-perdido-al-terminar`
- **FileFlow.Plugin.Network**: `dry-run-que-entrega-el-disparador`
- **FileFlow.Sdk**: `borrado-virtual-solo-ve-archivos`, `pasos-de-renombrado-ilegibles`, `salida-global-sin-expandir`
- **FileFlow.Tests (infraestructura de pruebas)**: `catalogo-sin-carpeta-de-destino`, `censo-de-puertos-sin-su-asiento`, `contrato-de-colecciones-sin-su-regla`, `indice-de-pruebas-ciego-al-cr`, `portapapeles-sin-vigilante`, `proyeccion-uno-sin-guardia`

Las que mutan la **declaración** de una guardia (el censo, el analizador) cuentan como infraestructura de pruebas, no como subsistema del producto: son 6.
