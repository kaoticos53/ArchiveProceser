# Cobertura de mutaciones

> Generado por `MutationDeclarationCoverageTests` a partir de `mutations/*.json` y del árbol de fuentes.
> **No se edita a mano.**
> Regenerar: `FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests`.

Mutaciones declaradas: 9
Subsistemas del producto con alguna mutación: 4 de 15
Guardias que auditan el repositorio con mutación que las muerda: 4 de 32

## Qué declara cada mutación

| Mutación | Fichero que muta | Testigo | Control |
| :--- | :--- | :--- | :--- |
| `borrado-virtual-solo-ve-archivos` | `FileFlow.Sdk/Storage/VirtualStorageService.cs` | `VirtualStorageService_EnumerationAndDirectoryDeletion_ShouldOperateInTheStore` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `censo-de-puertos-ignora-un-puerto-nuevo` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `NodePortCoverageGuardTests` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `censo-de-puertos-sin-su-asiento` | `FileFlow.Tests/TestHelpers/NodePortInventory.cs` | `NodePortCoverageGuardTests` | `PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly` |
| `contrato-de-colecciones-sin-su-regla` | `FileFlow.Tests/TestHelpers/TestCollectionContractAnalyzer.cs` | `TestCollectionContractGuardTests` | `Analyzer_ShouldRequireLocalization_WhenClassUsesRealUserPreferences` |
| `indice-de-pruebas-ciego-al-cr` | `FileFlow.Tests/TestHelpers/TestSuiteIndex.cs` | `TestSuiteIndexTests` | `SourceTextTests` |
| `limpiador-borra-por-su-cuenta` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `AnEmptyDirectoryCleanerWhoseStorageFailsToDelete` | `AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort` |
| `limpiador-vuelve-a-mirar-el-disco` | `FileFlow.Plugin.FileSystem/Nodes/Actions/EmptyDirectoryCleanerNode.cs` | `VirtualEmptyFolderCleanupIntegrationTests` | `EmptyDirectoryCleaner_RegistersDeletedPermanentlyJournalEntry` |
| `token-de-tema-que-desaparece` | `FileFlow.App/Services/ThemeResourceApplier.cs` | `ThemeTokenCompletenessTests` | `DisabledStateLintTests` |
| `validador-sin-materializar-puertos` | `FileFlow.Core/Engine/GraphValidator.cs` | `GraphValidatorDynamicPortTests` | `GraphValidatorTests` |

## Subsistemas del producto sin ninguna mutación declarada (11 de 15)

Una mutación por comportamiento que importa; estos proyectos no tienen ninguna, así que ningún
defecto declarado demuestra que sus pruebas muerdan. Es la lista de trabajo, no un reproche.

- `FileFlow.Plugin.AI`
- `FileFlow.Plugin.Archives`
- `FileFlow.Plugin.Data`
- `FileFlow.Plugin.Documents`
- `FileFlow.Plugin.Hashing`
- `FileFlow.Plugin.Images`
- `FileFlow.Plugin.Integrations`
- `FileFlow.Plugin.Logic`
- `FileFlow.Plugin.Network`
- `FileFlow.Plugin.Scripting`
- `FileFlow.Plugin.Subflows`

## Guardias del repositorio sin ninguna mutación que las muerda (28 de 32)

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
- `FileFlow.Tests/Unit/Core/WorkflowExamplesValidationTests.cs`
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
- **FileFlow.Core**: `validador-sin-materializar-puertos`
- **FileFlow.Plugin.FileSystem**: `censo-de-puertos-ignora-un-puerto-nuevo`, `limpiador-borra-por-su-cuenta`, `limpiador-vuelve-a-mirar-el-disco`
- **FileFlow.Sdk**: `borrado-virtual-solo-ve-archivos`
- **FileFlow.Tests (infraestructura de pruebas)**: `censo-de-puertos-sin-su-asiento`, `contrato-de-colecciones-sin-su-regla`, `indice-de-pruebas-ciego-al-cr`

Las que mutan la **declaración** de una guardia (el censo, el analizador) cuentan como infraestructura de pruebas, no como subsistema del producto: son 3.
