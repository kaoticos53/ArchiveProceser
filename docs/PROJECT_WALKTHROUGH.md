# FileFlow Studio - Historial de Cambios y Registro de Implementación (Walkthrough)

Este documento registra cronológicamente todos los cambios, mejoras, correcciones y nuevas funcionalidades implementadas en el proyecto **FileFlow Studio**.

---

## [2026-08-22] - Migración Total a Rutas Relativas por Defecto en Nodos de Plugins

### 🛠 Cambios Implementados
1. **Auditoría y Sustitución de Rutas Absolutas a Relativas:**
   - Se revisaron todos los nodos de la aplicación y se reemplazaron las rutas predeterminadas absolutas (como `C:\SampleFiles`, `C:\FileFlowOutput`, `C:\Quarantine`, `C:\FileFlowUnpacked`, `C:\FileFlowOptimized`) por patrones de rutas relativas basadas en plantillas:
     - **FolderSourceNode**: `SourcePath` $\rightarrow$ `{RelativeDir}\Input`
     - **DestinationSinkNode**: `DestinationRoot` $\rightarrow$ `{RelativeDir}\Output`
     - **OriginalFileActionNode**: `QuarantinePath` $\rightarrow$ `{RelativeDir}\Quarantine`
     - **SmartUnpackNode**: `DestinationFolder` $\rightarrow$ `{RelativeDir}\Unpacked`
     - **ImageOptimizerNode**: `OutputDirectory` $\rightarrow$ `{RelativeDir}\OptimizedImages`
2. **Anclaje Automático a la Ruta Global de Salida (`ParameterHelper.ResolveOutputPath`):**
   - Al usar patrones de rutas relativas, `ParameterHelper.ResolveOutputPath` las ancla automáticamente bajo el directorio configurado en **Ajustes de la Aplicación > Almacenamiento y Rutas (DefaultGlobalOutputDir)**.
1. **Actualización Automática de Argumentos (`NodeViewModel.cs`):**
   - Al seleccionar cualquier preset en el menú desplegable del nodo **Transcodificar Media**, se activa la actualización inmediata del parámetro `CustomArguments` en la tarjeta del nodo con los comandos FFmpeg configurados en dicho preset (ej: `-vn -c:a libmp3lame -b:a 192k` para MP3).
2. **Eliminación del Campo Redundante `FFmpegPath` (`MediaTranscoderNode.cs`):**
   - Se eliminó el parámetro `FFmpegPath` de las tarjetas del nodo de media para simplificar la interfaz visual.
   - El nodo resuelve de forma limpia la ruta ejecutable de FFmpeg directamente desde `ExternalToolsService` (definida globalmente en Ajustes > Herramientas Externas), con fallback dinámico en el `PATH` del sistema.
1. **Configuración de ComboBox (`NodeParameterTemplates.xaml`):**
   - Se estableció `IsEditable="False"` en el control `ComboBox` de los parámetros de nodos.
   - En WPF, la combinación de `IsEditable="True"` con `SelectedItem` en un ComboBox provocaba que el control de texto editable interno sobrescribiera el ítem seleccionado al hacer clic en el desplegable. Al establecer `IsEditable="False"`, la selección de cualquier preset del menú (*Extraer Audio MP3*, *Convertir 720p H.264*, *WebM VP9*, etc.) funciona de manera instantánea y estable.
2. **Sincronización Dinámica de Presets (`NodeParameterViewModel.cs`):**
   - Se añadió la suscripción al evento `MediaPresetManagerService.Instance.PresetsChanged` para refrescar automáticamente la lista de opciones (`Options`) de los nodos de media al crear, editar o eliminar presets en el gestor.
1. **Desacoplamiento de Binding Conflictuco (`NodeParameterTemplates.xaml`):**
   - Se eliminó el binding redundante `Text="{Binding Value...}"` con `UpdateSourceTrigger=PropertyChanged` que competía con `SelectedItem` en el control `ComboBox` editable de las tarjetas de nodos.
   - Ahora `SelectedItem="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"` administra la selección limpia de cualquier preset del desplegable sin que la UI revierta la selección.
1. **Creación del Convertidor (`ValueConverters.cs`):**
   - Se creó e implementó `StringEqualsToVisibilityConverter` en la capa de convertidores WPF (`FileFlow.App.Converters`) para comparar dinámicamente la categoría de presets con parámetros de texto y devolver el estado de visibilidad del icono correspondiente.
2. **Registro de Recurso Global y Local (`App.xaml` & `MediaPresetManagerWindow.xaml`):**
   - Se registró la clave de recurso `<converters:StringEqualsToVisibilityConverter x:Key="StringEqualsToVisibilityConverter" />` tanto globalmente en `App.xaml` como localmente en los recursos de `MediaPresetManagerWindow.xaml`, solucionando por completo la excepción de tipo `XamlParseException` / `StaticResourceHolder` al presionar el botón `⚙️ Presets`.
1. **Inicialización al Arrancar la Aplicación (`App.xaml.cs`):**
   - Se añadieron invocaciones explícitas a `UserPreferencesService.Instance.Load()` y `ExternalToolsService.Instance.LoadConfig()` en el evento `OnStartup`.
   - Se aplica de forma inmediata el tema visual guardado (`ActiveTheme`) vía `ThemeManager.Instance.SetTheme(themeEnum)`.
2. **Sincronización Reactiva en ViewModels (`ControlBarViewModel`, `ToolboxViewModel`, `EditorViewModel`, `LogViewModel`):**
   - **`ControlBarViewModel.cs`**: Carga e iguala `SelectedTheme` y `IsDryRun` con `DefaultDryRunState` al iniciar y cuando se modifican desde la ventana de Ajustes Generales.
   - **`ToolboxViewModel.cs`**: Ajusta automáticamente `IsCompactMode` con `IsCompactToolbox` al refrescar el catálogo.
   - **`EditorViewModel.cs`**: Se suscribe al evento `PreferencesChanged` para actualizar reactivamente `GlobalOutputDir` si se cambia desde los ajustes generales.
   - **`LogViewModel.cs`**: Lee de forma dinámica la preferencia `MaxLogEntries` para delimitar el buffer de registros en la consola.
1. **Control de Concurrencia en Motor (`WorkflowExecutor.cs`):**
   - Se activó e integró la protección por `SemaphoreSlim` (`_concurrencyThrottle`) dentro del despacho de nodos (`DispatchEmitAsync`).
   - Todos los hilos secundarios de despacho pasan obligatoriamente por `await _concurrencyThrottle.WaitAsync()` y liberan en el bloque `finally` (`_concurrencyThrottle.Release()`), garantizando el respeto estricto del número de hilos configurado (`MaxDegreeOfParallelism`).
2. **Conexión con Preferencias de Aplicación (`ControlBarViewModel.cs`):**
   - Al instanciar `WorkflowExecutor` antes de iniciar la ejecución de cualquier flujo, se lee dinámicamente `UserPreferencesService.Instance.Preferences.MaxParallelThreads` (salvo en modo depuración interactiva, donde se fuerza a 1 hilo para inspección secuencial).
1. **Persistencia Completa de Preferencias (`UserPreferencesService.cs`):**
   - Extensión del servicio JSON en `%APPDATA%\FileFlowStudio\user_preferences.json` para guardar de forma permanente todos los ajustes de la aplicación:
     - `DefaultGlobalOutputDir`: Ruta de salida global persistente (restaurada automáticamente en el editor).
     - `ActiveTheme`: Tema visual activo (`Dark`, `Light`, `Cyber`, `Pastel`).
     - `IsCompactToolbox`: Modo de vista del catálogo (`Compacto` / `Detallado`).
     - `MaxParallelThreads`: Hilos de CPU máximos para procesamiento paralelo (1 a 16 hilos).
     - `DefaultDryRunState`: Estado del Modo Prueba (Simulación) por defecto al arrancar.
     - `DefaultConflictStrategy`: Estrategia de resolución de colisiones predeterminada (`RenameIncremental`, `Overwrite`, `Skip`).
     - `DefaultLogLevel`: Filtro de logs predeterminado (`Information`, `Warning`, `Error`, `Debug`).
     - `AutoScrollConsole` & `MaxLogEntries`: Desplazamiento automático y límite de memoria de registros en consola.
     - `EnableAutoSave` & `AutoSaveIntervalMinutes`: Configuración de respaldo automático de flujos.
2. **Rediseño Modal de Ajustes Generales (`WorkflowSettingsWindow.xaml`):**
   - Diálogo organizado en 4 pestañas fluidas:
     - 📂 **Almacenamiento & Rutas** (Ruta global, colisiones, auto-guardado).
     - 🎨 **Apariencia & UI** (Tema, vista de catálogo, auto-scroll y límite de logs).
     - ⚡ **Rendimiento & Ejecución** (Hilos paralelos de CPU, modo prueba por defecto, nivel de log).
     - 🛠️ **Herramientas Externas** (FFmpeg, FFprobe, 7z, Python con Autobúsqueda).
1. **Gestor de Presets de Media (`MediaPresetManagerService.cs` & `MediaPresetManagerWindow.xaml`):**
   - Servicio Singleton con almacenamiento en `%APPDATA%\FileFlowStudio\media_presets.json`.
   - Sistema CRUD completo (Crear, Editar, Eliminar, Restablecer) de presets de conversión.
   - Ventana modal interactiva con 10 presets de fábrica preconfigurados (Extracción de MP3, AAC, FLAC, 1080p H.264, 720p H.264, 4K H.265/HEVC, WebM VP9, GIF Animado, Móvil Ultra-Comprimido, Personalizado).
   - Botón `⚙️ Presets` integrado en la tarjeta del nodo `Transcodificar Media` para acceso instantáneo al gestor.
2. **Servicio de Autobúsqueda de Herramientas Externas (`ExternalToolsService.cs`):**
   - Servicio de detección automática de ejecutables (`ffmpeg.exe`, `ffprobe.exe`, `7z.exe`, `python.exe`) escaneando el `PATH` del sistema, rutas conocidas (`Program Files`, `C:\ffmpeg\bin`, `AppData`, `Chocolatey`, `WinGet`, `Scoop`) y el Registro de Windows.
   - Pestaña **"🛠️ Herramientas Externas"** añadida a la Configuración del Flujo (`WorkflowSettingsWindow.xaml`) con el botón **`🔍 Auto-Detectar Herramientas`**.
3. **Ejecución Real con Control de Progreso (`MediaTranscoderNode.cs`):**
   - Ejecución de `ffmpeg.exe` en subproceso con redirección de `stderr` para capturar el progreso en tiempo real (`time=00:01:23.45`) y emitirlo a la consola de logs.
   - Modo de simulación/fallback seguro cuando FFmpeg no está presente o en pruebas simuladas.
1. **Reducción de Nombres de Nodos (`Strings.es.resx` & `Strings.resx`):**
   - Se simplificaron los nombres de los 22 nodos en los recursos de idioma para eliminar términos redundantes y hacerlos directos y explícitos:
     - `Borrado Seguro a Papelera` $\rightarrow$ **`Enviar a Papelera`**
     - `Renombrador Avanzado con Tokens` $\rightarrow$ **`Renombrar Archivo`**
     - `Reubicador y Copiador de Archivos` $\rightarrow$ **`Mover / Copiar`**
     - `Limpiador de Carpetas Vacías` $\rightarrow$ **`Limpiar Carpetas`**
     - `Acción sobre Archivo Original` $\rightarrow$ **`Acción en Origen`**
     - `Inspector de Directorios` $\rightarrow$ **`Escanear Carpeta`**
     - `Descompresión Inteligente` $\rightarrow$ **`Descomprimir`**
     - `ArchiveCompressorNode` $\rightarrow$ **`Comprimir ZIP / 7z`**
     - `ArchiveFilterNode` $\rightarrow$ **`Filtrar Comprimidos`**
     - `Optimizador de Imágenes` $\rightarrow$ **`Optimizar Imagen`**
     - `Media & Video Transcoder` $\rightarrow$ **`Transcodificar Media`**
     - `Document & PDF Processor` $\rightarrow$ **`Procesar Documento`**
     - `Inyector de Variables` $\rightarrow$ **`Inyectar Variable`**
     - `Calculador de Hash Criptográfico` $\rightarrow$ **`Calcular Hash`**
     - `Filtro de Deduplicación por Hash` $\rightarrow$ **`Filtrar Duplicados`**
     - `Agrupador de Lotes (Batch Buffer)` $\rightarrow$ **`Agrupar por Lotes`**
     - `Control de Tasa y Pausa (Throttle)` $\rightarrow$ **`Pausa / Throttle`**
     - `Barrera de Sincronización (Fork & Join)` $\rightarrow$ **`Barrera Fork & Join`**
     - `Enrutador Condicional (Switch / Case)` $\rightarrow$ **`Enrutador Switch`**
     - `Filtro por Condición Lógica` $\rightarrow$ **`Filtro Condicional`**
     - `Ejecutor de Comandos y Procesos CLI` $\rightarrow$ **`Ejecutar Comando CLI`**
     - `Notificador Webhook (HTTP POST)` $\rightarrow$ **`Enviar Webhook`**
     - `Inspector de Registros` $\rightarrow$ **`Registrar Log`**
2. **Ajuste de Diseño Visual en el Catálogo:**
   - Con esta optimización, todos los títulos caben en una sola línea en las tarjetas del catálogo sin recortarse ni requerir puntos suspensivos (`...`).
1. **Límite de 10 Nodos Frecuentes (`ToolboxViewModel.cs`):**
   - Se ajustó la consulta LINQ al construir el grupo `🔥 Más Usados` para limitar el listado exactamente a los **10 nodos con mayor frecuencia de uso acumulada** (`.Take(10)`).
   - Mantiene el catálogo limpio y enfocado exclusivamente en el TOP 10 de nodos más utilizados.
1. **Servicio de Persistencia de Usuario (`UserPreferencesService.cs`):**
   - Servicio Singleton que guarda de forma persistente en `%APPDATA%\FileFlowStudio\user_preferences.json`:
     - `FavoriteNodeTypes`: Conjunto de tipos de nodos marcados por el usuario.
     - `NodeUsageCounts`: Conteo histórico de veces que se ha insertado cada nodo en el lienzo de trabajo.
2. **Sistema de Favoritos Manuales (⭐) e Historias Frecuentes (🔥):**
   - **Botón Estrella en Tarjeta**: Cada nodo en `NodeToolboxView.xaml` incluye un botón de estrella `[ ⭐ ]` / `[ ☆ ]` para marcar o desmarcar como favorito con un clic.
   - **Categorías Dinámicas**: Creación automática de los grupos superiores `⭐ Favoritos` y `🔥 Más Usados`.
   - **Recuento Automático**: Al añadir o arrastrar un nodo al editor (`EditorViewModel.cs`), se incrementa su contador de uso acumulado.
3. **Pestañas de Categoría Multilínea (`NodeToolboxView.xaml`):**
   - Reemplazado el contenedor horizontal por un `<WrapPanel Orientation="Horizontal">`.
   - Los chips de filtrado (`Todas`, `⭐ Favoritos`, `🔥 Frecuentes`, `📁 Archivos`, `📦 Compresión`, `🎬 Media & Docs`, `🏷️ Metadatos`, `🔀 Lógica`, `⚡ Integraciones`) se distribuyen limpiamente en 2 o 3 filas dinámicas sin necesidad de hacer scroll horizontal.
1. **Reestructuración de la Taxonomía de Nodos (`FileFlow.Plugin.*`):**
   - **`FileSystem` (📁 Archivos y Disco)**: `FolderSourceNode`, `DestinationSinkNode`, `DirectoryInspectorNode`, `FileRelocatorNode`, `AdvancedRenamerNode`, `EmptyDirectoryCleanerNode`, `SafeRecycleDeleteNode`, `OriginalFileActionNode`.
   - **`Archives` (📦 Compresión)**: `SmartUnpackNode`, `ArchiveCompressorNode`, `ArchiveFilterNode`.
   - **`MediaDocs` (🎬 Multimedia y Documentos)**: `ImageOptimizerNode`, `MediaTranscoderNode`, `DocumentProcessorNode`.
   - **`Metadata` (🏷️ Metadatos e Integridad)**: `VariableInjectorNode`, `ExifMetadataNode`, `HashCalculatorNode`, `DeduplicationFilterNode`.
   - **`Logic` (🔀 Lógica y Control)**: `SwitchCaseNode`, `ExpressionFilterNode`, `BatchBufferNode`, `ThrottleDelayNode`, `ForkJoinBarrierNode`.
   - **`Integrations` (⚡ Integración y CLI)**: `CliExecutionNode`, `WebhookNotificationNode`, `LogOutputNode`.
2. **Actualización de Filtros por Chips (`ToolboxViewModel.cs` & `NodeToolboxView.xaml`):**
   - Actualizada la barra superior del catálogo para incluir chips de filtrado de las 6 categorías principales (`Todas`, `📁 Archivos`, `📦 Compresión`, `🎬 Media & Docs`, `🏷️ Metadatos`, `🔀 Lógica`, `⚡ Integraciones`).
1. **Iconografía Específica por Función (`AppModels.cs` & `ToolboxViewModel.cs`):**
   - Asignado icono visual único según el propósito técnico del nodo (ej: `📁` `FolderSourceNode`, `🕵️` `DirectoryInspectorNode`, `📦` `SmartUnpackNode`, `🗜️` `ArchiveCompressorNode`, `🖼️` `ImageOptimizerNode`, `🎬` `MediaTranscoderNode`, `📄` `DocumentProcessorNode`, `🏷️` `VariableInjectorNode`, `🔀` `SwitchCaseNode`, `💾` `DestinationSinkNode`, `🗑️` `OriginalFileActionNode`).
2. **Modos de Vista Intercambiables (`ToolboxViewModel.cs` & `NodeToolboxView.xaml`):**
   - **Modo Lista Compacta (Predeterminado)**: Muestra únicamente el icono y el título de cada nodo en 28px de altura, triplicando la cantidad de nodos visibles en pantalla sin necesidad de hacer scroll. La descripción completa se despliega al pasar el ratón en un ToolTip flotante rico.
   - **Modo Detallado**: Muestra el icono, título y la descripción multilínea.
   - **Botonera Toggle**: Selector `[ ☰ Compacto ]` / `[ 📋 Detallado ]` en la cabecera.
3. **Pestañas Chips de Filtro por Categoría (`NodeToolboxView.xaml`):**
   - Barra superior con chips de filtrado rápido (`Todas`, `📁 Archivos`, `📦 Compresión`, `🖼️ Imágenes`, `🔀 Lógica`).
1. **Pinceles Dinámicos del Sistema de Temas (`LogView.xaml.cs`):**
   - Se reemplazaron los pinceles estáticos de texto (`#F1F5F9`) por llamadas a `GetThemeBrush(resourceKey, fallbackHex)`.
   - El cuerpo del mensaje de log ahora consume `TextPrimaryBrush` (`#0F172A` en temas claros como `Light` y `Pastel`, `#F1F5F9` en temas oscuros como `Dark` y `Cyber`).
   - Los niveles de log adaptan sus niveles de contraste con el fondo (`AccentErrorBrush`, `AccentWarningBrush`, `AccentCyanBrush`, `AccentPurpleBrush`).
2. **Suscripción a Eventos de Tema (`ThemeManager.cs`):**
   - `LogView` se suscribe a `ThemeManager.Instance.ThemeChanged` para reconstruir automáticamente el `FlowDocument` al cambiar de tema, garantizando legibilidad perfecta en cualquier modo.
1. **Estilo `FilterRadioButton` (`ButtonStyles.xaml`):**
   - Definido estilo dinámico para controles `RadioButton` con `TargetType="RadioButton"`, vinculado a pinceles del sistema de temas (`BgHoverBrush`, `TextPrimaryBrush`, `BorderDarkBrush`, `AccentPrimaryBrush`).
2. **Aplicación en la Consola (`LogView.xaml`):**
   - Asignado `Style="{StaticResource FilterRadioButton}"` a los botones de filtro rápido de la consola (`Todos`, `🔴 Errores`, `🟠 Advertencias`).
   - Al cambiar de tema (`Oscuro`, `Claro`, `Cyber`, `Pastel`), los botones adaptan de forma inmediata sus fondos, bordes y colores de texto sin desajustes estéticos.
1. **Migración a `RichTextBox` en WPF (`LogView.xaml`):**
   - Reemplazado el `<TextBox>` plano por un `<RichTextBox>` estilizado con tipografía monoespaciada `Cascadia Code`/`Consolas`.
   - Propiedades `IsReadOnly="True"` e `IsDocumentEnabled="True"` para permitir la selección de texto libre con ratón/teclado y copiado con `Ctrl+C` sin permitir edición del documento.
2. **Formateador de Registro con Código de Colores (`LogView.xaml.cs`):**
   - Cada entrada de log se convierte dinámicamente en elementos `Paragraph` y `Run` en el `FlowDocument`:
     - **Marca de tiempo `[HH:mm:ss]`**: Gris Pizarra (`#64748B`).
     - **🔴 `[CRITICAL]` / `[ERROR]`**: Rojo Neón (`#EF4444`) en Negrita.
     - **🟠 `[WARNING]`**: Naranja/Ámbar (`#F59E0B`) en Negrita.
     - **🔵 `[INFO]`**: Azul Cielo (`#38BDF8`).
     - **🟣 `[DEBUG]`**: Púrpura Suave (`#C084FC`).
     - **⚪ `[TRACE]`**: Gris Pizarra (`#94A3B8`).
     - **Mensaje**: Blanco/Gris primario (`#F1F5F9`).
3. **Filtros Rápidos por Nivel de Log (`LogViewModel.cs` & `LogView.xaml`):**
   - Añadida barra de botones de filtro en la cabecera de la consola (`Todos`, `🔴 Errores`, `🟠 Advertencias`) con contadores de fallos y advertencias en tiempo real.
4. **Auto-Scroll Inteligente:**
   - La consola se desplaza automáticamente al final con cada nuevo mensaje excepto cuando el usuario tiene texto seleccionado o está inspeccionando el historial superior.
1. **Ampliación de `NodePort` (`NodePort.cs`):**
   - Se añadió el parámetro opcional `Description` a la definición del record `NodePort` para documentar la función técnica de cada puerto.
2. **ViewModel de Puerto Enriquecido (`PortViewModel.cs`):**
   - Propiedades para ToolTips interactivos: `Description`, `TransmittedCount`, `LastItemInfoText`, `MetadataVariables` (claves/valores del contexto), `ConnectionStatusText` e `IsConnected`.
   - Método `UpdatePortContext(FileItemContext)` para actualizar métricas e inyectar metadatos en tiempo de ejecución o durante la depuración paso a paso.
3. **Conexiones Dinámicas (`EditorViewModel.cs`):**
   - `UpdatePortConnectionStates()` notifica a cada puerto si está libre u origen/destino de conexiones activas (ej: `Conectado a FolderSourceNode ("Out")`).
4. **Plantilla XAML Fluent (`NodeCardView.xaml`):**
   - ToolTip contextual rico de 310px de ancho con sombra profunda `DropShadowEffect`.
   - Incluye cabecera con badge del tipo de dato, descripción funcional, métricas de elementos transmitidos, **panel desplegable de metadatos y variables del contexto (ideal para depuración)** e indicador de estado de conexión `🟢 Conectado` / `⚪ Puerto libre`.
1. **Reorganización de Columnas Grid (`StatusBarView.xaml`):**
   - Se configuró el estado del motor (`🟢 Listo`) con `ColumnDefinition Width="Auto"`, ya que es un mensaje corto y predecible.
   - Se asignó la columna expansible `ColumnDefinition Width="*"` al botón de la **Ruta de Salida Global** (`📁 Salida: ...`), aumentando su ancho máximo dinámico hasta `520px` (`TextTrimming="CharacterEllipsis"`).
   - Ahora la ruta de salida dispone de todo el espacio libre de la barra inferior y no se corta prematuramente.
1. **Monitor de Rendimiento en Tiempo Real (`SystemPerformanceMonitor.cs`):**
   - Servicio asíncrono con temporizador ligero (refresco cada 1000ms) para medir consumo de Memoria RAM (`WorkingSet64`) y uso de CPU sin saturar la UI.
2. **ViewModel y Vista de Barra Inferior (`StatusBarViewModel.cs` & `StatusBarView.xaml`):**
   - Vista moderna Fluent Design en la parte inferior de la ventana principal (`MainWindow.xaml`).
   - **Contexto del Grafo**: Muestra conteo en vivo de nodos (`🧩 Nodos`), conexiones (`🔗 Conexiones`) y nodo seleccionado (`🎯 SelectedNode`).
   - **Estado del Motor**: Muestra badges de estado reactivos (`🟢 Listo`, `⚡ Ejecutando flujo...`, `⏸️ Pausado`).
   - **Métricas de Sistema**: Muestra consumo de `🧠 RAM` y `💻 CPU` en tiempo real.
   - **Accesos Rápidos**: Botón clicable de **Ruta de Salida Global** (`📁 Salida: C:\FileFlowOutput`) que abre la carpeta en Windows Explorer, y botón de ajuste de Zoom (`🔍 Fit`).
1. **Definición de Estilo `PrimaryButton` (`ButtonStyles.xaml`):**
   - Agregada la clave `PrimaryButton` heredando de `IconButton` para que los botones de acción principal (como **✅ Aplicar Ajustes**) adopten dinámicamente el color de acento del tema activo (`AccentPrimaryBrush` en Dark, Light, Cyber y Pastel).
2. **Sincronización de Barra de Título Nativa Windows DWM (`WindowThemeHelper.cs`):**
   - Implementado `WindowThemeHelper` con invocación P/Invoke a `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE`).
   - Sincroniza la barra de título superior nativa de la ventana (`MainWindow`, `WorkflowSettingsWindow`, `PasswordManagerWindow`) para cambiar entre tema oscuro y claro automáticamente al cambiar el tema de la aplicación o del sistema operativo.
1. **Eliminación de la Caja de Título Inútil (`ControlBarView.xaml`):**
   - Se eliminó la caja de texto innecesaria que ocupaba espacio en la barra superior.
2. **Conmutador Compacto de Modo Prueba (`ControlBarView.xaml` & `ButtonStyles.xaml`):**
   - Creado el estilo `SecondaryToggleButton` para `<ToggleButton>` con estado activo iluminado (`AccentPrimaryBrush`).
   - Resuelta la excepción XAML de inicialización al asignar el `TargetType` correcto para conmutadores en WPF.
3. **Modal de Configuración del Flujo (`WorkflowSettingsWindow.xaml` & `ControlBarViewModel.cs`):**
   - Corregido el comando de la barra superior exponiendo `Editor` y el comando delegado `OpenWorkflowSettingsCommand` directamente en `ControlBarViewModel.cs`.
   - Se despliega correctamente el modal **`⚙️ Configuración del Flujo`** con la Ruta de Salida Global y botón examinador `📁 Examinar`.
1. **Anclaje Inteligente de Rutas Relativas (`ParameterHelper.ResolveOutputPath`):**
   - Implementado el método `ParameterHelper.ResolveOutputPath` en `FileFlow.Sdk`. Si un nodo especifica una ruta de salida relativa (ej: `Procesados/{FileName}` o `Compressed`), el sistema la unifica automáticamente dentro de la Ruta Global de Salida (`GlobalOutputDir`).
   - Si un nodo especifica una ruta absoluta (ej: `D:\Final\Salida.zip`), se respeta exactamente esa ubicación sin alteración.
2. **Inyección de Token `{GlobalOutputDir}` (`VariableTemplateResolver.cs`):**
   - Agregado el token `{GlobalOutputDir}` al motor de plantillas de variables.
3. **Control de Ruta Global en la Barra de Herramientas UI (`ControlBarView.xaml` & `EditorViewModel.cs`):**
   - Añadido un campo **📁 Salida Global** con botón examinador en la barra de control superior para configurar fácilmente la carpeta base del flujo (por defecto `C:\FileFlowOutput`).
   - Persistencia de `GlobalOutputDir` en el modelo JSON del grafo (`WorkflowGraph.cs`).
4. **Actualización de Nodos de Escritura:**
   - Actualizados `DestinationSinkNode`, `ArchiveCompressorNode`, `ImageOptimizerNode` y `MediaTranscoderNode` para consumir `ParameterHelper.ResolveOutputPath`.
5. **Pruebas Automatizadas (`GlobalOutputDirTests.cs`):**
   - Añadidas pruebas unitarias verificando anclaje dinámico, rutas absolutas directas e interpolación de tokens (80/80 pruebas exitosas).
1. **Gestor Modal de Contraseñas y Corrección de Diálogo (`PasswordManagerWindow.xaml`):**
   - Nueva ventana modal WPF para escribir contraseñas multilínea con soporte de **Importar (.txt)** y **Exportar (.txt)** mediante diálogos de archivo nativos.
   - Solucionado el error XAML `StaticResourceExtension` en `PasswordManagerWindow.xaml` sustituyendo `{StaticResource PrimaryButton}` y `{StaticResource SecondaryButton}` por `{DynamicResource PrimaryButton}` y `{DynamicResource SecondaryButton}`, asegurando la resolución correcta de estilos globales de la aplicación.
   - Vinculado `win.Owner = Application.Current.MainWindow` y `WindowStartupLocation="CenterOwner"` para centrado fluido sobre la ventana principal.
   - Integrado botón `🔑 Claves` en el inspector para el parámetro `PasswordList` de `SmartUnpackNode`.
2. **Presets Editables y Desplegables de Parámetros (`NodeParameterViewModel.cs`):**
   - ComboBoxes del inspector configurados con `IsEditable="True"` para poder seleccionar presets existentes o escribir y registrar nuevos presets personalizados.
   - Añadidas opciones desplegables para `ArchiveFormat`, `CompressionType` y `Preset`.
3. **Descompresión Inteligente con Contraseñas y Multipartes (`SmartUnpackNode.cs`):**
   - Prueba secuencial de claves candidatas (`PasswordList` y `PasswordFile`) inyectando la clave usada en `Metadata["UsedPassword"]`.
   - Detección de volúmenes multipartes (`FindRelatedVolumeFiles`) enviando el conjunto de partes al puerto `Error` ante claves incorrectas o archivos corruptos.
4. **Enrutamiento Inteligente por Rangos y Operadores (`SwitchCaseNode.cs`):**
   - Soporte en la regla `Pattern` para rangos de tamaño (`< 10 MB`, `10 MB..1 GB`), fechas (`2025-01-01..2025-12-31`), números y extensiones.
5. **Parseo Numérico y de Unidades (`ExpressionFilterNode.cs` & `ParameterHelper.cs`):**
   - Extracción y normalización de unidades de almacenamiento (`TB`, `GB`, `MB`, `KB`, `Bytes`) y tiempo (`ms`, `s`, `m`, `h`).
6. **Automatización Desatendida y Resiliencia (`FileFlow.Core`):**
   - Implementados `FolderWatcherService` (supervisión de carpetas en tiempo real con debounce anti-colisión), `FlowSchedulerService` (programador de tareas) y `ExecutionRetryHelper` (política de reintentos con exponential backoff).
7. **Nuevos Nodos de Procesamiento (`ArchiveCompressorNode`, `DocumentProcessorNode`, `MediaTranscoderNode`):**
   - Creados nodos para compresión ZIP/7Z/TAR.GZ, procesamiento e inspección de PDF/documentos, y transcodificación multimedia.

---

## [2026-08-20] - Optimización de Rendimiento UI: Actualización en Tiempo Real de Logs y Barra de Progreso

### 🛠 Cambios Implementados
1. **Búfer de Logs Asíncrono e Inmune a Saturación de UI (`LogViewModel.cs`):**
   - Se reemplazó el despacho síncrono e inmediato por línea de log en la UI por una cola de concurrencia thread-safe (`ConcurrentQueue<LogEntry>`).
   - Implementado un temporizador `DispatcherTimer` a nivel de fondo (`DispatcherPriority.Background`) que refresca el cuadro de texto y la consola cada 50ms (20 FPS).
2. **Despacho de Barra de Progreso y Estado de Baja Prioridad:**
   - `UpdateProgress` utiliza `DispatcherPriority.Background` evitando acaparar el hilo principal de renderizado WPF.
3. **Resultado:**
   - La consola muestra la transmisión de logs **en tiempo real a medida que ocurre la ejecución** sin congelar el renderizado visual ni bloquear la interfaz ni los controles de la aplicación.

---

## [2026-08-20] - Solución Definitiva al Bloqueo de Flujos (Eliminación de Interbloqueo / Deadlock de Semáforo)

### 🛠 Cambios Implementados
1. **Eliminación del Interbloqueo Canónico (*Semaphore Deadlock*) en `WorkflowExecutor.cs`:**
   - Se identificó la causa raíz: en `DispatchEmitAsync`, se llamaba a `_concurrencyThrottle.WaitAsync` mientras el nodo padre estaba esperando a que los nodos hijos terminasen `ExecuteAsync`. Al encadenar 2 o más nodos (ej. `FolderSourceNode` -> `VariableInjectorNode` -> `DestinationSinkNode`), los hilos padres bloqueaban todas las fichas del semáforo esperando a los hijos, mientras los hijos esperaban una ficha libre del semáforo, provocando un **Interbloqueo Recursivo (Deadlock)** absoluto.
   - Se eliminó el estrangulamiento anidado en `DispatchEmitAsync`, permitiendo que la tubería de emisión asíncrona procese lotes de archivos de forma fluida y sin bloqueos de aplicación.
2. **Extracción Directa de Metadatos Dinámicos en `EditorViewModel.cs`:**
   - Se corrigió la función de travesía `GetUpstreamAvailableVariables` para extraer las claves directas de `VariableInjectorNode` de forma inmediata.

---

## [2026-08-20] - Corrección de Bloqueo al Ejecutar Flujos con `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Sincronización de Hilos (*Thread-Safety*) en `VariableInjectorNode.cs` y `NodeViewModel.cs`:**
   - La ejecución del flujo ocurre en un hilo secundario (`Task.Run`), mientras la UI modifica los parámetros. Se añadió sincronización `lock (Parameters)` y la creación de instantáneas (*snapshots*) previas a la iteración para evitar bucles infinitos por corrupción interna del diccionario.
2. **Filtrado Seguro en `ExportToGraphModel` (`EditorViewModel.cs`):**
   - Agrupación e ignorado de claves vacías o en proceso de edición mediante `.Where(p => !string.IsNullOrWhiteSpace(p.Key)).GroupBy(...)` evitando excepciones `ArgumentException` al serializar el grafo.
3. **Limpieza de Parámetros en `GraphValidator.cs`:**
   - `instance.Parameters.Clear()` antes de asignar los parámetros exportados.

---

## [2026-08-20] - Gestión Dinámica de Variables en `VariableInjectorNode` (Botones ➕ y 🗑️)

### 🛠 Cambios Implementados
1. **Controles UI Dinámicos en la Tarjeta del Nodo (`EditorView.xaml`):**
   - Añadido botón verde **`➕ Variable`** en la cabecera del panel de ajustes del nodo.
   - Cada fila de variable cuenta con:
     - `TextBox` editable para el **Nombre de la Variable** (Clave).
     - `TextBox` editable para la **Expresión / Valor**.
     - Botón selector visual **`[{x}]`** para insertar variables de nodos anteriores.
     - Botón rojo **`🗑` (Papelera)** para eliminar esa variable individual al instante.

2. **Gestión MVVM y Sincronización en Tiempo Real (`NodeViewModel.cs` / `NodeParameterViewModel.cs`):**
   - Implementados los comandos `AddVariableCommand` y `RemoveParameterCommand`.
   - Sincronización bidireccional automática con `_nodeInstance.Parameters`.

---

## [2026-08-20] - Inyección Multivariable en `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Soporte Multivariable en `VariableInjectorNode.cs`:**
   - Rediseño del nodo para permitir definir y resolver múltiples pares de variables (`Key1`/`Value1`, `Key2`/`Value2`, ..., `Key5`/`Value5`) de forma simultánea dentro del mismo nodo.
2. **Actualización de la Travesía Topológica `EditorViewModel.cs`:**
   - La travesía del grafo hacia atrás (*Upstream Traversal*) inspecciona todas las claves no vacías de `VariableInjectorNode` y las ofrece automáticamente en el menú desplegable **`[{x}]`** de los nodos conectados posteriormente.
3. **Actualización de Pruebas Unitarias (`VariableInjectorNodeTests.cs`):**
   - Cobertura completa de resolución e inyección simultánea de múltiples variables.

---

## [2026-08-20] - Suite Exhaustiva de Automatización de Pruebas (xUnit, FluentAssertions, Moq)

### 🛠 Cambios Implementados
1. **Nuevo Proyecto de Pruebas (`FileFlow.Tests/FileFlow.Tests.csproj`):**
   - Configurado en .NET 9 (`net9.0-windows` con `UseWPF=true`) e integrado en `FileFlow.slnx`.
   - Incluye **xUnit**, **FluentAssertions** y **Moq**.

2. **Tests Unitarios (`FileFlow.Tests/Unit/`):**
   - **`VariableTemplateResolverTests`:** Reemplazo de variables, funciones de fecha, transformaciones de texto (`Upper`, `Lower`, `PadLeft`), saneamiento de caracteres ilegales (`Sanitize`), cascadas `Coalesce` e interpolación dinámica.
   - **`LocalizationManagerTests`:** Verificación de singleton y disparo del evento `LanguageChanged`.
   - **`WorkflowExecutorTests`:** Ejecución topológica y validación de nodos.
   - **`FolderSourceNodeTests` & `VariableInjectorNodeTests`:** Inyección de metadatos (`Counter`, `SourceRootPath`, `CustomCategory`) e I/O.
   - **`EditorViewModelTests`:** Travesía topológica inversa (`GetUpstreamAvailableVariables`) y cálculo dinámico de variables en la interfaz.

3. **Tests de Integración (`FileFlow.Tests/Integration/`):**
   - **`WorkflowIntegrationTests`:** Flujo E2E desde `FolderSourceNode` $\rightarrow$ `VariableInjectorNode` $\rightarrow$ `DestinationSinkNode` validando la tubería completa de archivos reales.

4. **Tests de Estrés / Rendimiento (`FileFlow.Tests/Performance/`):**
   - **`PerformanceStressTests`:** Procesamiento de **10,000 elementos masivos** evaluando el motor de plantillas en menos de 1 segundo (651 ms).

---

## [2026-08-20] - Variables Avanzadas de Sistema, Metadatos Multimedia/Compresión y Funciones de Expresión

### 🛠 Cambios Implementados
1. **Nuevas Variables del Sistema y Ejecución (`VariableTemplateResolver.cs`):**
   - Incorporación de `{DateNow}` (`yyyy-MM-dd`), `{TimeNow}` (`HH-mm-ss`), `{DateTimeNow}` (`yyyy-MM-dd_HH-mm-ss`).
   - Contador incremental de secuencia por lote `{Counter}` / `{Index}` inyectado desde `FolderSourceNode.cs`.
   - Métricas de peso de archivo: `{SizeMB}`, `{SizeKB}`, `{SizeBytes}`.
   - Variables de entorno del sistema: `{UserName}`, `{MachineName}`.

2. **Nuevos Metadatos de Imagen y Archivos Comprimidos:**
   - **`ExifMetadataNode.cs`:** Extracción de `{ImageWidth}`, `{ImageHeight}`, `{Orientation}` (`Landscape`/`Portrait`/`Square`), `{AspectRatio}` (ej. `16:9`) y `{Megapixels}` (ej. `24.1MP`).
   - **`SmartUnpackNode.cs`:** Extracción de `{ArchiveFormat}` (`ZIP`/`7Z`/`RAR`) y `{UnpackedFileCount}`.

3. **Nuevas Funciones de Expresión Prácticas:**
   - `{Sanitize(text)}`: Limpieza automática de caracteres ilegales en Windows (`\ / : * ? " < > |`).
   - `{PadLeft(val, length, char)}`: Relleno de números con ceros u otros caracteres (ej. `{PadLeft(Counter, 4, "0")}`).
   - `{Substring(text, start, length)}`: Extracción segura de subcadenas sin desbordamientos de índice.
   - `{RegexMatch(text, pattern)}`: Extracción por expresión regular.
   - `{RegexReplace(text, pattern, replacement)}`: Reemplazo con expresiones regulares.
   - `{Coalesce(val1, val2, ...)}`: Evaluación en cascada retornando el primer valor no vacío.
   - `{FileAgeDays(dateStr)}`: Cálculo de antigüedad en días transcurridos.

4. **Integración en el Selector Gráfico `[{x}]` (`EditorViewModel.cs`):**
   - Actualización de `GetUpstreamAvailableVariables` clasificando y ofreciendo todas las nuevas variables y funciones ordenadas por grupos (`🌐 System & Environment`, `📷 Image & Media`, `📦 Archives`, `🔤 Expression Functions`).

---

## [2026-08-20] - Consola de Ejecución Seleccionable, Exportación y Categorización de Nodos

### 🛠 Cambios Implementados
1. **Exportación de Logs a Archivo:**
   - Se añadió la acción `ExportLogsCommand` en `LogViewModel.cs` y el botón **`💾 Exportar Log`** / **`💾 Export Log`** en la barra de herramientas de la consola (`LogView.xaml`).
   - Permite guardar todo el historial formateado con marcas de tiempo en archivos `.log` o `.txt` mediante `SaveFileDialog`.

2. **Selección y Copiado de Texto con el Ratón:**
   - Se reemplazó el control estático por un visor de texto editable/seleccionable (`TextBox` en modo `IsReadOnly="True"`) en `LogView.xaml`.
   - Los usuarios pueden arrastrar el ratón para seleccionar cualquier bloque de texto y copiarlo directamente con `Ctrl+C` o el menú contextual.
   - Implementado autodesplazamiento hacia la última línea recibida (`LogConsoleTextBox_TextChanged`).

3. **Corrección de Categoría del Nodo Inyector de Variables:**
   - Se actualizó `VariableInjectorNode.cs` asignándole la categoría **`Utility`** (*Utilidades*).
   - Ahora aparece correctamente clasificado junto a herramientas como `Log Inspector` dentro del panel lateral de herramientas (*Toolbox*).

---

## [2026-08-20] - Subsistema de Variables Dinámicas, Motor de Expresiones e Inyector de Variables

### 🛠 Cambios Implementados
1. **Motor de Plantillas de Variables (`VariableTemplateResolver.cs`):**
   - Interpolación de tokens dinámicos en cualquier parámetro de texto o ruta de nodo.
   - **Variables del Sistema Estandarizadas al Inglés:** `{FileName}`, `{FileNameNoExt}`, `{Extension}`, `{CurrentPath}`, `{OriginalPath}`, `{CurrentDir}`, `{OriginalDir}`, `{RelativePath}`.
   - **Funciones de Expresión Integradas:**
     - **Fechas:** `{Year(date)}`, `{Month(date)}`, `{Day(date)}`, `{FormatDate(date, "yyyy-MM")}`.
     - **Texto:** `{Upper(text)}`, `{Lower(text)}`, `{Trim(text)}`, `{Replace(text, "old", "new")}`, `{Default(val, "fallback")}`.

2. **Cálculo de `RelativePath` (Estructura de Directorio Relativo):**
   - Se actualizó `FolderSourceNode.cs` para adjuntar la metadata `SourceRootPath` al escanear directorios.
   - `VariableTemplateResolver` calcula la ruta de subcarpetas relativa exacta (ej. `mami/antiguo`) excluyendo el nombre del archivo.

3. **Nodo `Inyector de Variables` (`VariableInjectorNode.cs`):**
   - Permite calcular e inyectar claves de metadatos personalizadas (`item.Metadata["VariableName"] = ResolvedValue`) en el flujo para nodos posteriores.

4. **Selector Gráfico de Variables `{x}` con Travesía Topológica Inversa:**
   - Se agregó el botón gráfico **`[{x}]`** al lado de los campos de entrada de parámetros en `EditorView.xaml`.
   - Al pulsar **`[{x}]`**, `EditorViewModel.GetUpstreamAvailableVariables` recorre el grafo hacia atrás (*Upstream Traversal*) para ofrecer únicamente las variables exportadas por los nodos conectados previamente (EXIF, origen de descompresión, variables inyectadas).

---

## [2026-08-20] - Sistema de Internacionalización Multilingüe (Español / Inglés)

### 🛠 Cambios Implementados
1. **Gestor de Localización Estándar de .NET (`LocalizationManager.cs`):**
   - Singleton encargado de administrar el cambio de cultura al vuelo (`CultureInfo`) entre Español (`es-ES`) e Inglés (`en-US`).
   - Notificación de cambio mediante el evento `LanguageChanged` y propiedad indexadora `this[string key]`.

2. **Ficheros de Recursos `.resx`:**
   - Creación de `Strings.resx` (Inglés por defecto) y `Strings.es.resx` (Español).

3. **Selector Dinámico de Idioma en Barra de Control:**
   - Desplegable en `ControlBarView.xaml` reactivo en tiempo real para traducir al instante la UI, los títulos de los nodos en lienzo, las descripciones y el catálogo de herramientas.

---

## [2026-08-20] - Descubrimiento y Carga Robusta de Plugins

### 🛠 Cambios Implementados
1. **Contexto de Carga Desacoplado (`PluginAssemblyLoadContext.cs`):**
   - Carga de ensamblados `.dll` en memoria para evitar bloqueos del sistema operativo sobre los archivos en disco.
   - Búsqueda de dependencias de plugins con fallback a `AppDomain.CurrentDomain.BaseDirectory` para librerías como `SixLabors.ImageSharp`, `MetadataExtractor` y `SharpCompress`.

---

## [2026-08-20] - Estructura de Documentación y Repositorio Git Initial

### 🛠 Cambios Implementados
1. **Repositorio Git (`.gitignore` & `git init`):**
   - Configuración de exclusión de binarios `bin/`, `obj/` y archivos de cache `.antigravity/`.
   - Registro del commit inicial en la rama `main`.

2. **Documentación del Proyecto (`docs/`):**
   - `docs/README.md`: Centro de documentación y visión general del proyecto.
   - `docs/nodes/CREATING_NODES.md`: Guía de desarrollo de nodos personalizados.
   - `docs/nodes/examples/SampleMultiPortNode.cs`: Ejemplo de nodo de código completo para desarrolladores.
   - `docs/ARCHITECTURE_DEEP_DIVE.md`: Guía arquitectónica detallada en 4 niveles de complejidad.
