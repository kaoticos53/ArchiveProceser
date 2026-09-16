using Xunit;

// El suite corre en paralelo por colecciones, con el estado global de proceso confinado en
// colecciones no paralelizables (DisableParallelization = true, que en xUnit ejecuta la colección
// en exclusiva: ni siquiera las colecciones ajenas corren a la vez).
//
// Colecciones exclusivas y el estado que confinan:
//   · Unit.Views.VisualSnapshotsCollection — la sesión headless de Avalonia y su Dispatcher, el tema
//     activo (ThemeManager) y el diccionario de recursos de la aplicación.
//   · Unit.AI.OnnxInferenceCollection — el clúster de inferencia nativo: clases que alcanzan los
//     registros de sesiones (ModelSessionRegistry, OnnxSessionManager, AiPluginInitializer) o los
//     motores con caché propia (SemanticEmbeddingEngine, AudioInferenceEngine, Whisper). Ojo: no es
//     "todo Unit/AI" — MultimodalVisionLlmNodeTests corre fuera, sin colección, porque es HTTP
//     simulado sin registros de sesión (su backoff de reintentos está escalado a 0 % vía
//     MultimodalVlmClientEngine.RetryBackoffScalePercent, internal visible a los tests).
//   · Unit.AI.AiModelDownloadSequentialCollection — descargas reales de modelos: red + escritura en
//     el perfil del usuario.
//   · "Localization" → UNIFICADA en "VisualSnapshots" (2026-09-16): la cultura/idioma del proceso
//     es estado global que las capturas headless también renderizan, y dos colecciones exclusivas
//     distintas SÍ corren a la vez entre sí — la carrera culture-vs-captura era posible. Ahora cultura,
//     preferencias reales y sesión headless se serializan en una sola colección. Miembros: las 10 clases
//     que antes declaraban "Localization" (LocalizationManagerTests, PortSemanticsTests,
//     ToolboxViewModelTests, ToolboxOrganizationTests, NodeParameterViewModelTests,
//     VariableDiscoveryServiceTests, WorkflowMetricsDashboardViewModelTests,
//     MediaPresetsAndToolsServicesTests, SystemVariablesResolverExhaustiveTests,
//     ThemeCustomizerViewModelTests). Nota: las capturas fijan además el idioma durante la captura y lo
//     restauran al salir (VisualSnapshot), como cinturón de seguridad para futuros mutadores.
//
// El resto de colecciones es seguro paralelizarlas: son pruebas de lógica pura, o de solo lectura
// sobre el repositorio (lints de .axaml, catálogos, contratos de estilo). El aislamiento por prueba
// sigue siendo responsabilidad de cada prueba; esto sólo define qué puede correr a la vez.
// Si el suite vuelve a colgarse o a fallar «en la prueba equivocada», el sospechoso es un estado
// global nuevo que deba vivir en una de las colecciones exclusivas de arriba.
//
// Este contrato tiene GUARDIA: TestCollectionContractGuardTests (con la lógica en
// TestHelpers/TestCollectionContractAnalyzer) barre el árbol y falla si una clase toca un estado
// global exclusivo sin declarar ninguna colección exclusiva. Al añadir un estado global, añade su
// regla al analizador y su colección a ExclusiveCollections.
[assembly: CollectionBehavior(DisableTestParallelization = false)]
