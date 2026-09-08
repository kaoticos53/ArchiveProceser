Viewed session_summary.md:1-200
Viewed repo_architecture.md:1-138
Viewed PROJECT_WALKTHROUGH.md:1-60
Ran command: `Get-ChildItem -Recurse -Filter "*.csproj" | Select-Object FullName`
Ran command: `dotnet build FileFlow.slnx`

I have started building the solution to check for any compilation diagnostics, warnings, and to establish the current build baseline for our Phase 1 static audit. I will analyze the codebase as soon as the build completes.

Used tool: manage_task
Used tool: schedule

The solution build is currently in progress. Once the baseline compilation finishes, I will proceed with the static code analysis and audit for Phase 1.

Used tool: manage_task
Ran command: `Get-ChildItem -Recurse -Filter "*.csproj" | ForEach-Object {
    $content = [xml](Get-Content $_.FullName)
    [PSCustomObject]@{
        Project = $_.Name
        Packages = ($content.Project.ItemGroup.PackageReference | ForEach-Object { "$($_.Include) ($($_.Version))" }) -join "; "
        Projects = ($content.Project.ItemGroup.ProjectReference | ForEach-Object { $_.Include }) -join "; "
    }
} | Format-List`
Searched for "FileFlow.Plugin"
Viewed MainViewModel.cs:60-90
Viewed ServiceCollectionExtensions.cs:20-60
Searched for "Tesseract"
Searched for "CommunityToolkit.Mvvm"
Searched for "CommunityToolkit"
Searched for "ObservableObject"
Searched for "CommunityToolkit"
Searched for "ObservableObject"
Searched for "RelayCommand"
Searched for "ObservableObject"
Searched for "Sqlite"
Ran command: `dotnet build FileFlow.slnx -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest -warnaserror:false -v:minimal`
Used tool: schedule
Ran command: `dotnet format analyzers --verify-no-changes -v diag`
Used tool: schedule

I am currently inspecting the codebase using Roslyn analyzers to identify any unused references, formatting/style discrepancies, and dead code. I will report the findings shortly.

Used tool: manage_task
Used tool: schedule

Analyzing diagnostics across all solution projects with dotnet format...

Searched for "File.Exists"
Searched for "File.Exists"
Searched for "File."
Searched for "File."
Searched for "File."
Searched for "File."
Searched for "parameters.TryGetValue"
Searched for "class ImageOptimizerNode"
Viewed ImageOptimizerNode.cs:20-90
Viewed ImageOptimizerNode.cs:200-260
Searched for "class ParameterHelper"
Ran command: `Get-ChildItem -Recurse -Filter "*Helper*.cs" | Select-Object FullName`
Viewed NetworkTemplateHelper.cs:1-51
Searched for "class VariableTemplateResolver"
Searched for "class SystemVariablesResolver"
Viewed VariableTemplateResolver.cs:1-40
Viewed SystemVariablesResolver.cs:1-45
Viewed CliExecutionNode.cs:70-120
Viewed CliExecutionNode.cs:30-70
Searched for "Process.Start"
Viewed MediaTranscoderNode.cs:225-260
Viewed MediaTranscoderNode.cs:140-210
Viewed FfmpegMediaTranscoderService.cs:1-60
Viewed IFlowExecutionContext.cs:1-25
Searched for ".Instance"
Viewed App.xaml.cs:1-70
Viewed App.xaml.cs:70-148
Viewed MainViewModel.cs:1-60
Searched for "new MainViewModel("
Searched for "MainViewModel"
Viewed MainWindow.xaml:10-25
Viewed MainWindow.xaml.cs:1-32
Searched for ".DataContext>"
Ran command: `Get-ChildItem -Recurse -Filter "*.cs" FileFlow.Sdk | Select-Object -ExpandProperty FullName`
Searched for "ThemeDefinition"
Viewed IOsPlatformService.cs:1-45
Viewed IFileRecycler.cs:1-13
Searched for "IFileRecycler"
Ran command: `Get-ChildItem -Recurse -Filter "*.cs" FileFlow.Core | Select-Object -ExpandProperty FullName`
Ran command: `Get-ChildItem -Recurse -Filter "VirtualStorageService.cs" | Select-Object FullName`
Viewed VirtualStorageService.cs:1-60
Viewed VirtualStorageService.cs:1-15
Viewed IFileRecycler.cs:1-9
Viewed WindowsShellFileRecycler.cs:1-22
Ran command: `dotnet build FileFlow.slnx -warnaserror:false -v:quiet -p:RunAnalyzers=true -p:RunAnalyzersDuringBuild=true -p:NoWarn=""`
Used tool: schedule
Viewed Directory.Build.props:1-60
Viewed Directory.Build.props:61-86
Ran command: `$classes = Get-ChildItem -Recurse -Include "*.cs" -Exclude "*AssemblyInfo*", "*GlobalUsings*", "*.g.cs" | ForEach-Object {
    $content = Get-Content $_.FullName
    foreach ($line in $content) {
        if ($line -match '^\s*(public|internal|private)?\s*(sealed|abstract|static)?\s*(class|interface|record|enum|struct)\s+([A-Za-z0-9_]+)') {
            $matches[4]
        }
    }
} | Sort-Object -Unique

$results = foreach ($c in $classes) {
    # Skip common words
    if ($c -in @("Program", "App", "MainWindow", "Task", "Builder", "Test", "Extensions", "Rule", "State", "Option", "Item", "Entry")) { continue }
    $count = (Get-ChildItem -Recurse -Include "*.cs", "*.xaml" | Select-String -Pattern "\b$c\b" -SimpleMatch).Count
    [PSCustomObject]@{
        Name = $c
        Count = $count
    }
}

$results | Where-Object { $_.Count -le 2 } | Sort-Object Count | Format-Table -AutoSize
`
Used tool: schedule

Scanning the solution for unreferenced and low-reference types to identify dead or deprecated code...

Used tool: manage_task
Used tool: schedule

Continuing reference analysis...

Used tool: manage_task
Used tool: manage_task
Searched for "IModelLifecycleNode"
Searched for ": FlowNodeBase"
Viewed FlowNodeBase.cs:1-70
Viewed IFlowNode.cs:1-38
Viewed TextCodePreviewProvider.cs:50-85
Ran command: `dotnet test --nologo -v:minimal`
Used tool: schedule

Running the entire test suite to verify baseline integrity before drafting the Phase 1 diagnostic report...

Used tool: manage_task
Used tool: schedule

# Auditoría de Arquitectura de Software - FASE 1: Análisis y Detección (Auditoría Estática)

Como Arquitecto de Software Senior, he completado la **Fase 1** de inspección estática profunda sobre la solución `FileFlow.slnx` (.NET 9 / C# 13). 

A continuación se presenta el informe de hallazgos agrupado por nivel de prioridad (**Crítico**, **Medio** y **Limpieza**), evaluando:
1. Código muerto e inutilizado (tipos, dependencias y paquetes NuGet).
2. Duplicación de código y lógica redundante.
3. Violaciones de Clean Architecture y principios SOLID (acoplamientos rígidos, fugas de abstracción y responsabilidades mezcladas).

---

## 🔴 1. Hallazgos de Prioridad CRÍTICA
*(Afectan directamente a la estabilidad, portabilidad, testabilidad, seguridad o violan los contratos centrales de Clean Architecture)*

### [CRIT-01] Bipaseo Masivo de la Abstracción de I/O (`IStorageService`) en Múltiples Plugins
- **Ubicación:** 
  - `FileFlow.Plugin.Images`: [`ImageOptimizerNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Images/ImageOptimizerNode.cs#L214-L298), [`ExifMetadataNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Images/ExifMetadataNode.cs#L44).
  - `FileFlow.Plugin.Archives`: [`SmartUnpackNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Archives/SmartUnpackNode.cs#L181-L234), [`ArchiveCompressorNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Archives/ArchiveCompressorNode.cs#L62-L105), [`SafeArchiveExtractor.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Archives/Services/SafeArchiveExtractor.cs#L148-L165).
  - `FileFlow.Plugin.Documents`: [`PdfMergeNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Documents/PdfMergeNode.cs#L51-L151), [`PdfSplitNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Documents/PdfSplitNode.cs#L47), [`PdfTextExtractorNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Documents/PdfTextExtractorNode.cs#L47-L90), [`PdfMetadataNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Documents/PdfMetadataNode.cs#L54).
  - `FileFlow.Plugin.Data`: [`ExcelReaderNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Data/Nodes/Readers/ExcelReaderNode.cs#L45-L55), [`CsvReaderNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Data/Nodes/Readers/CsvReaderNode.cs#L45-L55), [`DataFormatConverterNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Data/Nodes/Processing/DataFormatConverterNode.cs#L43-L131), [`CsvExportNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Data/Nodes/Exporters/CsvExportNode.cs#L75).
  - `FileFlow.Plugin.AI`: [`SuperResolutionUpscalerNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.AI/Nodes/Vision/SuperResolutionUpscalerNode.cs#L130-L214), [`BackgroundRemoverNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.AI/Nodes/Vision/BackgroundRemoverNode.cs#L131-L276), [`PiiAnonymizerNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.AI/Nodes/Language/PiiAnonymizerNode.cs#L119-L199), [`LocalLlmProcessorNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.AI/Nodes/Language/LocalLlmProcessorNode.cs#L136-L216), [`LocalWhisperTranscriberNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.AI/Nodes/Audio/LocalWhisperTranscriberNode.cs#L57-L234).
- **Problema:** Tras la creación de [`IStorageService`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/Storage/IStorageService.cs), solo `FileFlow.Plugin.FileSystem` y `FileFlow.Plugin.Hashing` fueron refactorizados para usar `context.GetStorage()`. En los plugins indicados, existen más de 100 llamadas directas y estáticas a `System.IO.File`, `System.IO.Directory` y `System.IO.FileInfo`.
- **Impacto Arquitectónico:**
  1. *Violación de DIP (Dependency Inversion Principle):* La lógica de negocio depende directamente de la implementación de disco del SO.
  2. *Ruptura de VFS (Virtual File System):* Cuando un elemento procesado es virtual (`item.IsVirtual`), estos nodos fallan con `FileNotFoundException` o escriben archivos residuales en el disco físico real.
  3. *Incompatibilidad con Dry-Run:* Operaciones como `File.Delete` en `SmartUnpackNode` o `SafeArchiveExtractor` pueden borrar archivos reales en simulaciones.
  4. *Testabilidad nula sin disco:* No se pueden probar estos nodos con fakes o VFS puro en memoria.

---

### [CRIT-02] Acoplamiento Rígido entre Presentación (`FileFlow.App`) y Plugins Concretos (`FileFlow.Plugin.*`)
- **Ubicación:** 
  - [`FileFlow.App.csproj`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/FileFlow.App.csproj): Referencia directa a nivel de compilación (`<ProjectReference>`) de los 11 proyectos de plugins.
  - [`StatusBarViewModel.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/StatusBarViewModel.cs#L187-L244) y [`WorkflowExecutionCoordinator.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Services/WorkflowExecutionCoordinator.cs#L285): Acceso estático directo a tipos internos de IA: `FileFlow.Plugin.AI.Inference.OnnxSessionManager`, `AudioInferenceEngine` y `AiPluginInitializer.ClearAllSessions()`.
  - [`NodeSwitchCaseCoordinator.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Services/NodeSwitchCaseCoordinator.cs#L19-L139): Comprueba tipos concretos con `if (node is FileFlow.Plugin.Logic.SwitchCaseNode switchNode)` e instancia `FileFlow.Plugin.Logic.SwitchCaseRule`.
  - [`ControlBarViewModel.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/ControlBarViewModel.cs#L508): Instanciación directa de ventanas de vistas de un plugin (`new FileFlow.Plugin.FileSystem.UI.Views.SyntheticDataSetDesignerWindow`).
- **Problema:** Viola el principio *Zero-Touch en FileFlow.App* y el Principio Abierto/Cerrado (OCP). La aplicación anfitriona no debe conocer los tipos concretos de los plugins.
- **Propuesta de Solución:**
  1. Definir contratos en `FileFlow.Sdk` para puertos dinámicos configurables (ej. `IDynamicCaseSwitchNode` o `IDynamicBranchNode`) de modo que `NodeSwitchCaseCoordinator` opere sobre abstracciones.
  2. Gestionar el ciclo de vida de sesiones de inferencia a través de la interfaz existente [`IModelLifecycleNode`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/IModelLifecycleNode.cs) o un servicio mediator desacoplado, sin referenciar clases estáticas de `FileFlow.Plugin.AI`.
  3. Desacoplar la apertura de diseñadores mediante [`INodeCustomActionProvider`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/Descriptors/INodeCustomActionProvider.cs) y servicios de diálogo de UI.

---

### [CRIT-03] Ausencia de Abstracción Universal de Procesos del SO (`IProcessRunner`)
- **Ubicación:** 
  - [`CliExecutionNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Integrations/CliExecutionNode.cs#L81-L120)
  - [`MediaTranscoderNode.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Integrations/MediaTranscoderNode.cs#L140-L245)
  - [`ExternalToolsService.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Services/ExternalToolsService.cs#L74)
  - [`FallbackPreviewProvider.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Preview/Providers/FallbackPreviewProvider.cs#L57) y [`FilePreviewerViewModel.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Preview/ViewModels/FilePreviewerViewModel.cs#L149) (Hardcoded `Process.Start("explorer.exe", ...)`).
- **Problema:** Dispersión y duplicación de lógica para iniciar procesos del SO con `new Process()`, redirección de streams stdout/stderr, cancelación por timeout y matado de árbol de procesos (`kill(entireProcessTree: true)`).
- **Impacto Arquitectónico:**
  1. No es testeable con Mocks; ejecutar tests que toquen estos nodos fuerza la invocación de procesos del sistema operativo.
  2. Ruptura de portabilidad al hardcodear `explorer.exe` en la UI en lugar de usar `IOsPlatformService.OpenFolderAndSelectFile(...)`.
- **Propuesta de Solución:** Crear la abstracción `IProcessRunner` en `FileFlow.Sdk.Platform` con implementación unificada en `FileFlow.Core.Platform` y soporte para Mocks y modo Dry-Run.

---

### [CRIT-04] Vulnerabilidad de Seguridad Alta en Paquete NuGet `SSH.NET`
- **Ubicación:** [`FileFlow.Plugin.Network.csproj`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Network/FileFlow.Plugin.Network.csproj#L16).
- **Problema:** Referencia al paquete `SSH.NET` v`2024.2.0`, el cual emite la advertencia de compilación `NU1903`: Vulnerabilidad de gravedad alta conocida ([GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284)).
- **Impacto:** Riesgo potencial en la capa de transporte SFTP/SSH. Requiere actualización o mitigación en Fase 3/4.

---

## 🟡 2. Hallazgos de Prioridad MEDIA
*(Duplicidad de algoritmos, contaminación de capas o ineficiencias de inicialización)*

### [MED-01] Contaminación de Capas: Fuga de Conceptos de UI hacia el SDK de Dominio
- **Ubicación:** [`FileFlow.Sdk/Themes/ThemeDefinition.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/Themes/ThemeDefinition.cs).
- **Problema:** Define modelos con propiedades de estilo y color hexadecimales XAML (ej. `PrimaryBrush = "#0078D4"`, `BackgroundBrush = "#1E1E1E"`).
- **Impacto:** `FileFlow.Sdk` debe permanecer agnóstico a conceptos visuales y temas de interfaz gráfica. Los temas pertenecen a la capa de presentación (`FileFlow.App`).

---

### [MED-02] Duplicación Idéntica del Registro de Plugins en el Arranque
- **Ubicación:**
  - [`MainViewModel.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/ViewModels/MainViewModel.cs#L67-L89) (constructor por defecto).
  - [`ServiceCollectionExtensions.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Services/ServiceCollectionExtensions.cs#L30-L52).
- **Problema:** El bloque de registro de ensamblados de plugins (`RegisterNodeTypesFromAssembly(...)`) y la carga del directorio `/Plugins` están duplicados línea por línea.
- **Propuesta de Solución:** Centralizar el autodescubrimiento en `PluginLoader` o en una clase de extensión unificada del contenedor de DI.

---

### [MED-03] Instanciación Redundante en el Arranque de `MainWindow.xaml`
- **Ubicación:** [`MainWindow.xaml`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/MainWindow.xaml#L18-L20) y [`MainWindow.xaml.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/MainWindow.xaml.cs#L14-L17).
- **Problema:** `MainWindow.xaml` declara:
  ```xml
  <Window.DataContext>
      <vm:MainViewModel />
  </Window.DataContext>
  ```
  Al llamar a `InitializeComponent()`, WPF crea una instancia completa no inyectada de `MainViewModel` (que crea un `PluginLoader` y vuelve a cargar todos los ensamblados). Milisegundos después, en `MainWindow.xaml.cs`, esa instancia se descarta inmediatamente al reasignar `DataContext = mainViewModel;` proveniente del contenedor IoC.
- **Propuesta de Solución:** Sustituir por `d:DataContext="{d:DesignInstance Type=vm:MainViewModel}"` para reservar el DataContext estático solo a tiempo de diseño, ahorrando tiempo de inicio y consumo de memoria.

---

### [MED-04] Lógica Redundante e Inconsistente de Plantillas en `NetworkTemplateHelper`
- **Ubicación:** [`NetworkTemplateHelper.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Network/NetworkTemplateHelper.cs#L11-L49).
- **Problema:** Reimplementa la sustitución de tokens (`{FileName}`, `{Year}`, `{Metadata}`, etc.) con múltiples `.Replace()` encadenados manuales, en lugar de reutilizar el motor transversal [`VariableTemplateResolver.Resolve(...)`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/TemplateEngine/VariableTemplateResolver.cs) de `FileFlow.Sdk`.
- **Impacto:** Carece de soporte para funciones de cadena, funciones de fecha avanzadas, tokens de hash o EXIF y añade deuda técnica de mantenimiento.

---

### [MED-05] Subutilización de `FlowNodeBase` y Repetición de Boilerplate
- **Ubicación:** Prácticamente todos los nodos de los plugins (`FileFlow.Plugin.*`).
- **Problema:** A pesar de existir la clase abstracta [`FlowNodeBase.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/FlowNodeBase.cs), prácticamente la totalidad de los nodos implementan la interfaz [`IFlowNode`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/IFlowNode.cs) directamente, repitiendo boilerplate de `Id`, `Inputs`, `Outputs`, `Parameters`, `CustomActions` y `ParameterDescriptors`.
- **Propuesta de Solución:** Promover que los nodos hereden de `FlowNodeBase` y aprovechen sus métodos `GetParameter<T>()`, `SetParameter<T>()` y `EmitAsync()`.

---

## 🟢 3. Hallazgos de Prioridad LIMPIEZA
*(Código muerto, dependencias sin uso, patrones obsoletos y advertencias de analizador)*

### [LIM-01] Paquetes NuGet Inutilizados en Plugins
- **Ubicación:** 
  - [`FileFlow.Plugin.Archives.csproj`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Archives/FileFlow.Plugin.Archives.csproj#L16): Referencia a `CommunityToolkit.Mvvm (8.4.0)`.
  - [`FileFlow.Plugin.Integrations.csproj`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Plugin.Integrations/FileFlow.Plugin.Integrations.csproj#L16): Referencia a `CommunityToolkit.Mvvm (8.4.0)`.
- **Diagnóstico:** Ninguno de los dos proyectos contiene código MVVM, ViewModels, `ObservableObject` ni `RelayCommand`. Son dependencias residuales innecesarias.

---

### [LIM-02] Clases e Interfaces Muertas o de Compatibilidad Redundante
- **Ubicación:**
  - [`FileFlow.Core/Engine/IFileRecycler.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/IFileRecycler.cs): Interfaz vacía redundante (`public interface IFileRecycler : FileFlow.Sdk.Platform.IFileRecycler`).
  - [`FileFlow.Core/Storage/VirtualStorageService.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Storage/VirtualStorageService.cs): Clase vacía que solo hereda de `FileFlow.Sdk.Storage.VirtualStorageService`.
  - [`IFileRecycler`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Sdk/Platform/IFileRecycler.cs) y [`WindowsShellFileRecycler.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.Core/Engine/WindowsShellFileRecycler.cs): Obsoletas; todo el motor y plugins usan `IOsPlatformService.MoveToTrash(path)` o `IStorageService.DeleteAsync(...)`.

---

### [LIM-03] Advertencia CA2024 de Rendimiento Asíncrono en `TextCodePreviewProvider`
- **Ubicación:** [`TextCodePreviewProvider.cs`](file:///d:/Users/ricardo/Documents/GitHub/ArchiveProceser/FileFlow.App/Preview/Providers/TextCodePreviewProvider.cs#L66).
- **Problema:** Uso de `reader.EndOfStream` en un método asíncrono. Esta propiedad ejecuta I/O síncrono bloqueante sobre el stream subyacente.
- **Solución:** Reemplazar `while (!reader.EndOfStream)` por `while ((line = await reader.ReadLineAsync(cancellationToken)) != null)`.

---

### [LIM-04] Abuso del Patrón Singleton / Service Locator (`.Instance`) frente a DI
- **Ubicación:**
  - Vistas y ViewModels consumen directamente instancias estáticas: `LocalizationManager.Instance`, `UserPreferencesService.Instance`, `ThemeManager.Instance`, `ExternalToolsService.Instance`, `SqliteLogStore.Instance`.
- **Problema:** Dificulta el aislamiento y pruebas unitarias de los ViewModels al mantener estado compartido global en lugar de inyectar dependencias por constructor.

---

## 📊 Resumen Ejecutivo de la Fase 1

| Categoría | Cantidad | Estado / Impacto |
| :--- | :---: | :--- |
| **Crítico** | 4 | Bipaseo de `IStorageService`, acoplamiento UI-Plugins, falta de `IProcessRunner`, vulnerabilidad NuGet. |
| **Medio** | 5 | Fuga de UI al SDK (`ThemeDefinition`), duplicación de carga de plugins, doble inicio XAML, duplicación de plantillas, boilerplate de nodos. |
| **Limpieza** | 4 | Paquetes NuGet sin uso, interfaces redundantes, advertencia asíncrona CA2024, proliferación de `.Instance`. |

---

### 🚦 Siguiente Paso

Conforme a la metodología establecida, **no se ha modificado ningún archivo de código todavía**. 

Quedo a la espera de tu confirmación o ajustes sobre este diagnóstico para dar paso a la **FASE 2: Desacoplamiento e Inyección de Dependencias (DI)**. ¿Deseas aprobar este diagnóstico o priorizar algún punto en particular antes de comenzar?