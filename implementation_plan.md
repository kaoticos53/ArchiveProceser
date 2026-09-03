# FASE 1 — Auditoría de Código, Análisis de Patrones y Plan de Refactorización

## Resumen Ejecutivo

| Métrica | Valor |
|---|---|
| **Líneas C# totales** | ~43.400 |
| **Archivos .cs (sin bin/obj/test)** | ~255 |
| **Archivos de test** | 92 (10.316 líneas) |
| **Tests** | 481 / 481 ✅ |
| **Compilación** | 0 errores, 0 warnings |

### Distribución por proyecto (C# sin tests)

| Proyecto | Archivos | Líneas | % total |
|---|---|---|---|
| FileFlow.App | 84 | 10.640 | 32% |
| FileFlow.Plugin.AI | 26 | 6.328 | 19% |
| FileFlow.Plugin.FileSystem | 29 | 3.968 | 12% |
| FileFlow.Sdk | 50 | 3.462 | 10% |
| FileFlow.Core | 20 | 2.962 | 9% |
| Resto de plugins (7) | 39 | 4.601 | 14% |
| FileFlow.Tests | 92 | 10.316 | — |

---

## 1. Archivos Monolíticos Críticos (≥300 líneas)

> [!WARNING]
> Estos archivos concentran demasiadas responsabilidades y son los principales candidatos a modularización.

### 🔴 Prioridad ALTA (≥500 líneas)

| Archivo | Líneas | Problema |
|---|---|---|
| [`PromptTranslator.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/PromptTranslator.cs) | **841** | ~600 líneas son un diccionario estático literal de 400+ entradas español→inglés. Mezcla datos y lógica de traducción en un solo fichero. |
| [`AiModelManager.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/AiModelManager.cs) | **826** | Clase `static` monolítica que combina: catálogo de modelos (datos), descarga HTTP con progreso, gestión de URLs configurables, persistencia JSON, y verificación de integridad. Viola SRP severamente. |
| [`OnnxInferenceEngine.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/OnnxInferenceEngine.cs) | **761** | Motor de inferencia que acumula métodos para clasificación, detección facial, detección de objetos, super-resolución, eliminación de fondo y moderación de contenido — cada método es un mini-motor con preprocesado/postprocesado propio. |
| [`WorkflowExecutor.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowExecutor.cs) | **628** | Orquestador del DAG con lógica de checkpointing, telemetría, dry-run y journaling entremezclados (ya fue refactorizado parcialmente). |
| [`LogView.xaml`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Views/LogView.xaml) | **568** | XAML muy extenso con DataGrid complejo, menú contextual, templates inline y converters. |
| [`NodeCardView.xaml`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Views/Components/NodeCardView.xaml) | **648** | XAML con muchas secciones condicionales (badges de telemetría, puertos, indicadores). |

### 🟡 Prioridad MEDIA (300–500 líneas)

| Archivo | Líneas | Problema |
|---|---|---|
| [`LogViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/LogViewModel.cs) | 498 | Gestiona filtrado, paginación, exportación, sincronización con SQLite y sincronización con Inspector — demasiados ejes de responsabilidad. |
| [`ControlBarViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ControlBarViewModel.cs) | 488 | Gestiona ejecución, UI de drawer, apertura de manuales, idioma, watchdog — ya fue parcialmente delegado. |
| [`LanguageInferenceEngine.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/LanguageInferenceEngine.cs) | 484 | Motor NLP monolítico que mezcla traducción MarianMT, NLLB-200, tokenización, inferencia LLM y SRT parsing. |
| [`AudioInferenceEngine.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/AudioInferenceEngine.cs) | 461 | Motor de audio: resampling NAudio, Silero VAD, Piper TTS, generador armónico, recorte de silencios — todo en una sola clase. |
| [`RenamerPresetService.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Sdk/Renaming/RenamerPresetService.cs) | 452 | Contiene los presets hardcodeados como datos estáticos extensos + lógica de carga JSON + fallback. |
| [`ToolboxViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ToolboxViewModel.cs) | 448 | Gestiona perspectivas, búsqueda por tags, favoritos, persistencia de preferencias, y ordenación. |
| [`EditorViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/EditorViewModel.cs) | 442 | Contiene operaciones de grafo, serialización, gestión de anotaciones y grupos en un solo ViewModel. |
| [`NodeInspectorViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/NodeInspectorViewModel.cs) | 410 | Gestiona inspección, test de nodos, snapshots, previsualización y sincronización con LogView. |
| [`NodeViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/NodeViewModel.cs) | 402 | Ya fue parcialmente delegado en `NodeCategoryStyling` y `NodeSwitchCaseCoordinator`. |
| [`AiModelManagerViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/AiModelManagerViewModel.cs) | 363 | Lógica de UI + descarga + estado de error + filtrado — aceptable pero denso. |
| [`NodeParameterViewModel.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/NodeParameterViewModel.cs) | 359 | Renderizado de parámetros + evaluación de templates + mapeo de DisplayName. |
| [`SqliteLogStore.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Telemetry/SqliteLogStore.cs) | 333 | Ya fue parcialmente delegado en `SqliteLogSchema` y `SqliteLogMetricsReader`. |
| [`BuiltInThemesCatalog.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Services/BuiltInThemesCatalog.cs) | 326 | 12 temas definidos como objetos literales extensos — datos puros que podrían externalizarse. |
| [`FolderSourceNode.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.FileSystem/FolderSourceNode.cs) | 327 | Nodo con lógica compleja de filtrado, escaneo recursivo y watchdog. |
| [`VariableTemplateResolver.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Sdk/TemplateEngine/VariableTemplateResolver.cs) | 316 | Motor de resolución de tokens — fue parcialmente delegado. |
| [`WorkflowCliRunner.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Engine/WorkflowCliRunner.cs) | 315 | Runner CLI con parsing de argumentos, reporting JSON y watchdog. |
| [`AdvancedRenamerNode.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.FileSystem/AdvancedRenamerNode.cs) | 303 | Nodo complejo con lógica de renombrado virtual/directo + colisiones. |

---

## 2. Código Duplicado y Oportunidades DRY

### 🔴 CRÍTICO: Clases completamente duplicadas entre `FileFlow.App` y `FileFlow.Plugin.FileSystem`

> [!CAUTION]
> Los siguientes archivos son **copias exactas o casi exactas** que coexisten en ambos proyectos. Esto viola DRY de forma grave y genera divergencia inevitable.

| Clase/Vista | Ubicación App | Ubicación Plugin | Acción |
|---|---|---|---|
| `RegexLibraryService.cs` | [`FileFlow.App/Services/`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Services/RegexLibraryService.cs) (283L) | [`FileFlow.Plugin.FileSystem/UI/Services/`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.FileSystem/UI/Services/RegexLibraryService.cs) (267L) | **Eliminar la copia de App** — la versión canónica debe vivir en el plugin. |
| `RegexHelperViewModel.cs` | [`FileFlow.App/ViewModels/`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/ViewModels/RegexHelperViewModel.cs) (242L) | [`FileFlow.Plugin.FileSystem/UI/ViewModels/`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.FileSystem/UI/ViewModels/RegexHelperViewModel.cs) (213L) | **Eliminar la copia de App**. |
| `RegexHelperWindow.xaml` | [`FileFlow.App/Views/Components/`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Views/Components/RegexHelperWindow.xaml) (244L) | [`FileFlow.Plugin.FileSystem/UI/Views/`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.FileSystem/UI/Views/RegexHelperWindow.xaml) (245L) | **Eliminar la copia de App**. |

> **Líneas rescatables**: ~750 líneas de código muerto eliminadas inmediatamente.

---

### 🟡 Patrón repetido: Boilerplate de nodos de pipeline

Todos los nodos de plugin siguen una estructura repetitiva:
1. `Id`, `Name`, `Description`, `Category` (propiedades idénticas en estructura)
2. `Inputs`/`Outputs` (declaración de puertos)
3. `Parameters` / `ParameterDescriptors`
4. `try/catch(Exception ex)` envolviendo toda la lógica con `context.Log(Error)` + `EmitAsync("Error", item)`
5. Patrón de resolución de modelo: `AiModelManager.ResolveModelPathAsync` → verificar → descargar → logging

**60+ nodos** repiten este scaffolding. Aunque la naturaleza del pipeline (cada nodo tiene lógica de dominio distinta) justifica cierta repetición, hay oportunidades para:

- **Clase base abstracta `FlowNodeBase`** en `FileFlow.Sdk` con:
  - Implementación por defecto de `Id`, `Name`, `Description`, `Category`
  - Wrapper `try/catch` en `ProcessAsync` con emisión automática al puerto `Error`
  - Métodos helper: `EmitErrorAsync`, `EmitOutAsync`, `LogAndEmitError`
- **Clase base `AiFlowNodeBase`** en `FileFlow.Plugin.AI` para los ~18 nodos de IA con:
  - Resolución y descarga automática de modelos (`ResolveModelOrFail`)
  - Patrón común de validación de extensiones de archivo

> **Impacto estimado**: Eliminación de ~15-25 líneas de boilerplate por nodo × 60 nodos = ~900-1500 líneas de código redundante.

---

### 🟡 Datos estáticos extensos embebidos en código

| Archivo | Datos | Líneas datos | Propuesta |
|---|---|---|---|
| [`PromptTranslator.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/PromptTranslator.cs) | Diccionario de 400+ conceptos visuales ES→EN | ~600 | Externalizar a JSON: `visual_concepts_es_en.json` |
| [`AiModelManager.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Plugin.AI/AiModelManager.cs) | Catálogo de ~20 modelos IA con URLs, tamaños, tiers | ~200 | Externalizar a JSON: `ai_models_catalog.json` |
| [`BuiltInThemesCatalog.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.App/Services/BuiltInThemesCatalog.cs) | 12 temas con 30+ propiedades cada uno | ~280 | Externalizar a JSON: `builtin_themes.json` |
| [`RenamerPresetService.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Sdk/Renaming/RenamerPresetService.cs) | Presets de renombrado hardcodeados | ~300 | Ya tiene fallback JSON — eliminar datos hardcoded. |

> **Impacto estimado**: ~1.400 líneas de datos separadas de lógica.

---

## 3. Code Smells y Bugs Latentes

### 3.1 `catch(Exception ex)` genérico excesivo

- **71 ocurrencias** de `catch (Exception ex)` en todo el proyecto.
- En nodos de pipeline esto es aceptable (catch-all para resiliencia del flujo), pero en servicios de UI (`ThemeCustomizerViewModel`, `WorkflowSettingsWindow.xaml.cs`) puede ocultar bugs.

> [!NOTE]
> **Acción**: En servicios no-pipeline, reemplazar por excepciones específicas (`IOException`, `JsonException`, `InvalidOperationException`). En nodos, mantener pero estandarizar el patrón mediante la clase base `FlowNodeBase`.

### 3.2 `GC.Collect()` manual en `PluginLoader.cs`

- [`PluginLoader.cs:192`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/FileFlow.Core/Plugins/PluginLoader.cs#L192): Llamada explícita a `GC.Collect()` durante la carga de plugins.
- Impacto potencial: pausa de GC durante el arranque de la app.

> **Acción**: Evaluar si es necesario o si se puede eliminar. Si se necesita liberar `AssemblyLoadContext`, usar `ConditionalWeakTable` o `WeakReference`.

### 3.3 Ausencia de `IDisposable` en motores de inferencia

- `OnnxInferenceEngine`, `LanguageInferenceEngine`, `AudioInferenceEngine` mantienen caches de `InferenceSession` (GPU/DirectML) como campos estáticos `Lazy<T>` pero **no implementan `IDisposable`**.
- Las sesiones ONNX **deberían liberarse** al cerrar la aplicación para devolver memoria GPU.

> **Acción**: Implementar patrón `IDisposable` o método `Shutdown()` invocado en `App.OnExit`.

### 3.4 Solo 3 marcadores `TODO`/`FIXME`/`HACK` en toda la base de código

✅ Muy limpio. Los 3 están en archivos no críticos (converters y enums).

### 3.5 Sin antipatrones `.Result` ni `.Wait()`

✅ Excelente — todo el I/O asíncrono está bien propagado.

### 3.6 Sin instancias de `new HttpClient()`

✅ Correcto — se usa factory method con `SocketsHttpHandler`.

---

## 4. Dependencias y Acoplamiento

### Relaciones de dependencia actuales (correctas)

```mermaid
graph TD
    SDK["FileFlow.Sdk (puro)"]
    CORE["FileFlow.Core"]
    APP["FileFlow.App"]
    AI["Plugin.AI"]
    FS["Plugin.FileSystem"]
    ARCH["Plugin.Archives"]
    NET["Plugin.Network"]
    LOGIC["Plugin.Logic"]
    SCRIPT["Plugin.Scripting"]
    INT["Plugin.Integrations"]
    DATA["Plugin.Data"]
    DOCS["Plugin.Documents"]
    IMG["Plugin.Images"]
    HASH["Plugin.Hashing"]
    
    SDK --> CORE
    SDK --> AI
    SDK --> FS
    SDK --> ARCH
    SDK --> NET
    SDK --> LOGIC
    SDK --> SCRIPT
    SDK --> INT
    SDK --> DATA
    SDK --> DOCS
    SDK --> IMG
    SDK --> HASH
    CORE --> APP
    SDK --> APP
```

✅ **No se detectan dependencias circulares**. Los plugins solo dependen de `Sdk`. `Core` solo depende de `Sdk`. `App` depende de `Core` y `Sdk`.

### Riesgos de acoplamiento

- **`FileFlow.Plugin.AI`** es el módulo más grande (6.328 líneas, 26 archivos) — casi el doble que el siguiente plugin. Su relación interna entre los 4 motores de inferencia y los 18 nodos es estrecha pero justificada por el dominio.

---

## 5. Plan de Refactorización y Modularización

### Fase 2A — Limpieza Inmediata (Riesgo bajo, alto impacto)

| # | Acción | Archivos | Impacto |
|---|---|---|---|
| **A1** | **Eliminar duplicados App ↔ Plugin.FileSystem** (RegexLibraryService, RegexHelperViewModel, RegexHelperWindow) | 6 archivos (~750L eliminadas) | Elimina divergencia y código muerto |
| **A2** | **Limpiar código muerto**: verificar si las copias de App se referencian; si no, eliminar directamente | Compilación + grep | 0 regresiones si no se usan |

---

### Fase 2B — Externalización de Datos Estáticos (Riesgo bajo)

| # | Acción | Archivos | Impacto |
|---|---|---|---|
| **B1** | Externalizar `ConceptDictionary` de `PromptTranslator.cs` a JSON | 1 CS + 1 JSON | PromptTranslator baja de 841 → ~240 líneas |
| **B2** | Externalizar catálogo de modelos de `AiModelManager.cs` a JSON | 1 CS + 1 JSON | AiModelManager baja de 826 → ~620 líneas |
| **B3** | Externalizar temas de `BuiltInThemesCatalog.cs` a JSON | 1 CS + 1 JSON | BuiltInThemesCatalog baja de 326 → ~60 líneas |

---

### Fase 2C — Modularización de Motores Monolíticos (Riesgo medio)

| # | Archivo | Propuesta de extracción | Nuevos módulos |
|---|---|---|---|
| **C1** | `AiModelManager.cs` (826L) | Separar en: catálogo, descargador, configuración de URLs | `AiModelCatalog.cs`, `AiModelDownloader.cs`, `AiModelUrlConfig.cs` |
| **C2** | `OnnxInferenceEngine.cs` (761L) | Separar por dominio de inferencia | `ClassificationInference.cs`, `FaceDetectionInference.cs`, `ObjectDetectionInference.cs`, `ImageProcessingInference.cs` |
| **C3** | `LanguageInferenceEngine.cs` (484L) | Separar por tipo de tarea NLP | `TranslationEngine.cs`, `LlmInferenceEngine.cs`, `SrtParser.cs` |
| **C4** | `AudioInferenceEngine.cs` (461L) | Separar por función de audio | `AudioResampler.cs`, `VadEngine.cs`, `TtsEngine.cs` |
| **C5** | `LogViewModel.cs` (498L) | Extraer coordinación con Inspector | `LogInspectorSyncService.cs` |

---

### Fase 2D — Abstracción de Boilerplate de Nodos (Riesgo bajo-medio)

| # | Acción | Ubicación |
|---|---|---|
| **D1** | Crear `FlowNodeBase` abstracto en `FileFlow.Sdk` con propiedades comunes e implementación de try/catch | `FileFlow.Sdk/FlowNodeBase.cs` |
| **D2** | Crear `AiFlowNodeBase` en `FileFlow.Plugin.AI` con resolución/descarga de modelos | `FileFlow.Plugin.AI/AiFlowNodeBase.cs` |
| **D3** | Migrar nodos gradualmente (empezar por los más simples) | 60 nodos (incremental) |

---

### Fase 2E — Mejoras de Robustez (Riesgo bajo)

| # | Acción |
|---|---|
| **E1** | Implementar `IDisposable`/`Shutdown()` en motores de inferencia ONNX |
| **E2** | Evaluar/eliminar `GC.Collect()` en `PluginLoader.cs` |
| **E3** | Refinar `catch(Exception)` en servicios de UI por excepciones específicas |

---

## Verificación

### Tests Automatizados
```powershell
.\test.ps1        # 481 tests deben pasar al 100%
.\coverage.ps1    # Verificar cobertura no disminuye
dotnet build FileFlow.slnx --warnaserror  # 0 errores, 0 warnings
```

### Verificación Manual
- Ejecutar la app (`.\run.ps1`) y validar que el Toolbox, Inspector, LogView y Previewer funcionan correctamente.
- Verificar que el RegexHelper se abre desde el nodo AdvancedRenamer sin problemas tras la eliminación de los duplicados.

---

## Open Questions

> [!IMPORTANT]
> **¿Cuáles de las fases (A-E) deseas aprobar para ejecutar?** Puedo proceder en orden secuencial (A → B → C → D → E) o priorizar alguna fase específica.

> [!IMPORTANT]
> **¿La clase base `FlowNodeBase` (D1) debe ser abstracta obligatoria o una opción opt-in?** Una clase base abstracta fuerza coherencia pero modifica la firma de 60 nodos. Un enfoque opt-in (helper methods estáticos o extension methods) es menos invasivo pero menos uniforme.

> [!IMPORTANT]
> **Sobre la externalización de datos (B1-B3):** ¿Prefieres que los JSON se carguen desde archivos embebidos en el assembly (EmbeddedResource) o desde archivos en disco (`AppPaths.ConfigDirectory`)? Los embebidos son más robustos pero requieren rebuild; los de disco permiten personalización por el usuario.
