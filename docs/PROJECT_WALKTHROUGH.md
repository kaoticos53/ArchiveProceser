# FileFlow Studio - Historial de Cambios y Registro de Implementación (Walkthrough)

Este documento registra cronológicamente los hitos, cambios, mejoras y correcciones activas del proyecto **FileFlow Studio**.

> [!NOTE]
> **Historial Consolidado y Fases Previas**:
> El registro histórico completo correspondiente a fases anteriores (Fases 1 a 8, Sprints de Agosto 2026 y desarrollos fundacionales) ha sido consolidado y archivado para optimización de contexto en:
> 📄 [**`docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md`**](file:///docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md)

## [2026-09-15] - Modernización Integral de Vistas AXAML, Agrupación Ergonómica e i18n

### 🎯 Cambios Implementados
1. **Estandarización de Temas Dinámicos (`DynamicResource`)**:
   - **`AboutDialogWindow.axaml`**: Migrados todos los colores estáticos `#HEX` a recursos dinámicos (`BgCardBrush`, `BgSurfaceBrush`, `AccentPrimaryBrush`, `BorderDarkBrush`, etc.), garantizando compatibilidad con todos los temas de la aplicación (Dark, Light, Cyber, Pastel).
   - **`FilePreviewerControl.axaml` y `ImageCompareSliderControl.axaml`**: Eliminados fondos fijos (`#0B0D14`, `#111318`), integrando `BgEditorBrush`, `BgSurfaceBrush`, `AccentCyanBrush` y soporte i18n para etiquetas de metadatos, insignias y botones de explorador.
2. **Localización e Internacionalización Completa**:
   - **`WorkflowMetricsDashboardWindow.axaml`**: Cabeceras de `DataGrid` ("Nodo", "Categoría", "Duración", "Items", "Datos", "% Tiempo", "Estado") y botón de cierre vinculados dinámicamente a `LocalizationManager.Instance`.
3. **Rediseño Ergonómico de la Barra Superior ([ControlBarView.axaml](file:///FileFlow.App/Views/ControlBarView.axaml))**:
   - Reorganizados los controles en **3 islas/píldoras semánticas**:
     - *Isla 1 (Modos)*: Dry Run con indicador LED, Modo Observador y VFS Explorer con badge de archivos.
     - *Isla 2 (Ciclo de Vida)*: Botones primarios `▶ Ejecutar` / `🐞 Depurar`, controles de paso a paso `⏭` / `▶▶`, y botones de emergencia `⏸` / `⏹`.
     - *Isla 3 (Herramientas)*: `↶ Rollback` e `🔍 Inspector`.
4. **Microinteracciones y Polish en [MainWindow.axaml](file:///FileFlow.App/MainWindow.axaml)**:
   - `GridSplitter` actualizados con cursores dedicados (`SizeWestEast`, `SizeNorthSouth`) y sombras de elevación `BoxShadow="8 0 32 0 #70000000"` para el Drawer de navegación lateral.
5. **Validación Exhaustiva**:
   - Suite completa de pruebas ejecutada con éxito: **839 / 839 pruebas superadas al 100% (0 fallos, 0 errores)**.

---

## [2026-09-15] - Rediseño Visual Integral del Lienzo de Nodos (ComfyUI & Blender Studio Edition)

### 🎯 Cambios Implementados
1. **Rediseño Completo de Tarjetas de Nodo ([NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml))**:
   - **Formato Compacto por Defecto con Expansión Dinámica**: Las tarjetas de nodo se inician en un formato compacto y elegante (ancho base de 220px) mostrando la cabecera, conectores de entrada y salida, y la telemetría mínima.
   - **Botón de Expansión/Colapso en Cabecera (`▼` / `▶`)**: Botón interactivo tipo pastilla con icono de engranaje y chevron para desplegar u ocultar el panel de parámetros de configuración y acciones personalizadas en caliente.
   - **Puertos de Entrada y Salida Justo al Borde Exterior**: Conectores alineados en el contorno exacto de la tarjeta (`Margin="-7,3,0,3"` para entradas a la izquierda y `Margin="0,3,-7,3"` para salidas a la derecha) de modo que el centro geométrico de cada socket (Círculo, Cuadrado, Triángulo, Diamante) se sitúa directamente sobre la línea perimetral exterior del nodo, fiel al estándar visual de ComfyUI y Blender.
   - **Barra de Acento Superior por Categoría**: Tira de color neón superior integrada (`AccentColor`) en la cabecera con esquinas redondeadas de 6px.
   - **Soporte de Modelos de IA en Tarjeta**: Botón directo de carga/descarga en memoria (RAM/VRAM) con indicador de estado circular integrado en la cabecera para nodos que gestionan pesos de redes neuronales.
2. **Widgets y Parámetros Estilo Blender 4.x ([NodeParameterTemplates.axaml](file:///FileFlow.App/Themes/Templates/NodeParameterTemplates.axaml))**:
   - Campos de texto, autocompletados, sliders numéricos y selectores de ruta encapsulados dentro de un panel inset oscuro (`#16171B`) con borde sutil (`#2D313C`) y botones acoplados `{x}` para inserción de variables en tiempo real.
   - Botones de acciones personalizadas (`CustomActions`) formateados como pastillas interactivas con esquinas redondeadas.
3. **Paleta Studio Dark Refinada ([DarkTheme.axaml](file:///FileFlow.App/Themes/DarkTheme.axaml))**:
   - Nuevos tokens armónicos para el editor de nodos (`BgEditorBrush`: `#13151A`, `BgCardBrush`: `#1C1E24`, `BgHeaderBrush`: `#232630`, `BgSurfaceBrush`: `#16171B`).
4. **Validación Exhaustiva**:
   - Suite completa de pruebas ejecutada con éxito: **839 / 839 pruebas superadas al 100% (0 fallos, 0 errores)**.

---

## [2026-09-15] - Animación de Cable en Conexión Pendiente y Modernización UI/UX (Nodify.Playground Style)

### 🎯 Cambios Implementados
1. **Animación y Renderizado de Conexión Pendiente al Arrastrar Cable ([EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml) y [PendingConnectionViewModel.cs](file:///FileFlow.App/ViewModels/PendingConnectionViewModel.cs))**:
   - **Causa Raíz de no visualización**: `PendingConnection` en Nodify.Avalonia enlaza la posición dinámica del cursor mediante `TargetAnchor` (un `Point`) y el conector de origen mediante `SourceAnchor`/`Source`. En la plantilla XAML se estaba enlazando la propiedad `Target` (tipo `Object`) en lugar de `TargetAnchor`, y el valor local de `StrokeThickness` sobreescribía la animación de Avalonia.
   - **Solución Completa**:
     - Configurado `TargetAnchor="{Binding TargetLocation, Mode=TwoWay}"` y `SourceAnchor="{Binding Source.Anchor}"` en [EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml) junto con `EnablePreview="True"`, `EnableSnapping="True"` y `IsVisible="{Binding IsVisible}"`.
     - Enriquecido [PendingConnectionViewModel.cs](file:///FileFlow.App/ViewModels/PendingConnectionViewModel.cs) con notificación de cambios en `WireColor`, `Target` e `IsVisible`.
     - Definida la animación en `UserControl.Styles` y `NodifyEditor.Styles` oscilando continuamente `Opacity` (0.55 $\leftrightarrow$ 1.0) y `StrokeThickness` (3.0 $\leftrightarrow$ 4.5) con `Duration="0:0:0.8"`, creando un efecto de respiración y flujo de energía en tiempo real al arrastrar el cable.
2. **Modernización UI/UX Estilo Blender 4.x / Nodify.Playground**:
   - **HUD de Telemetría Flotante**: Añadido panel HUD semitransparente superior derecho en el lienzo visual mostrando estadísticas reactivas (`Selected: X / N`, `Connections: C`, `Location: X, Y`, `Zoom: Zx`) con colores neón.
   - **Sockets Geométricos Tipados**: Nuevas formas de pin en [PortViewModel.cs](file:///FileFlow.App/ViewModels/PortViewModel.cs) y [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml) (cuadrados para archivos, círculos para imágenes, triángulos para hashes y diamantes para colecciones/otros).
   - **Paleta Studio Dark**: Ajuste refinado de tonalidades oscuras en [DarkTheme.axaml](file:///FileFlow.App/Themes/DarkTheme.axaml) y [ThemeDefinition.cs](file:///FileFlow.App/Themes/ThemeDefinition.cs) inspiradas en Blender 4.x y ComfyUI.
   - **Barra de Búsqueda con Icono**: Añadido icono de lupa `🔍` y placeholder enriquecido en la caja de búsqueda de nodos de [NodeToolboxView.axaml](file:///FileFlow.App/Views/NodeToolboxView.axaml).
3. **Validación Exhaustiva**:
   - Resueltas colisiones de paralelismo en xUnit marcando los tests de `AiModelManager` con `[Collection("AiModelDownloadSequential")]`.
   - Suite completa de pruebas ejecutada con éxito: **839 / 839 pruebas superadas al 100% (0 fallos, 0 errores)**.

---

## [2026-09-15] - Corrección de Conexiones por Arrastre (ValueTuple Handling) y Menú Contextual de Nodos

### 🎯 Problemas Identificados y Solucionados
1. **Creación de Conexiones por Arrastre y Soltado (`FinishConnection` y `StartConnection`)**:
   - **Causa Raíz**: En `Nodify.Avalonia`, cuando el usuario suelta el cable sobre un conector destino, el evento `PendingConnectionCompletedEvent` pasa un parámetro empaquetado de tipo `ValueTuple<object, object>` conteniendo `(SourcePort, TargetPort)`. Al intentar castear directamente `target is PortViewModel`, la condición fallaba silenciosamente en runtime porque las estructuras genéricas `ValueTuple<T1, T2>` no son covariantes y no coincidían con `PortViewModel`.
   - **Solución**: En [EditorViewModel.cs](file:///FileFlow.App/ViewModels/EditorViewModel.cs), se implementó el método extractor `ExtractPortsFromParameter(object? param)` utilizando la interfaz `System.Runtime.CompilerServices.ITuple` (compatible con todas las variantes de tuplas por valor y por referencia de .NET 9). Ahora `StartConnection`, `FinishConnection` y `DisconnectConnector` desempaquetan correctamente tanto puertos individuales como pares `(Source, Target)` o tuplas compuestas, creando la conexión de forma inmediata al soltar el conector.
2. **Menú Contextual de Tarjetas de Nodo ([NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml))**:
   - **Causa Raíz**: El menú contextual estaba declarado únicamente a nivel de `<nodify:Node.ContextMenu>`. Al hacer clic derecho sobre los bordes, cabecera o áreas intermedias del nodo, el evento de menú contextual no se activaba en el `UserControl` raíz.
   - **Solución**: Se elevó la definición del menú contextual al nivel superior `<UserControl.ContextMenu>` en [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml), permitiendo abrir el menú contextual con clic derecho sobre cualquier parte de la tarjeta del nodo (inspeccionar, renombrar, pausar, alternar logs, copiar, cortar, duplicar, cambiar color y eliminar).
3. **Validación Exhaustiva**:
   - Creada la prueba unitaria `NodifyConnectionCommands_ShouldHandleValueTuplesAndDirectPorts` en [EditorViewLayoutTests.cs](file:///FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs) cubriendo el ciclo completo de creación, finalización mediante tuplas y desconexión.
   - Suite completa de 839 pruebas pasando al 100%: **839 / 839 superadas (0 fallos, 0 errores)**.

---

## [2026-09-15] - Estilizado Visual de Nodos y Conexiones Estilo ComfyUI (Canvas, Sockets y Splines)

### 🎯 Cambios Visuales y Arquitectura de Estilos
1. **Conexiones y Cables Estilo ComfyUI / LiteGraph**:
   - En [EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml): Se implementaron cables de conexión Bézier suaves con `StrokeThickness="3.5"`, `Spacing="45"` y color dinámico enlazado a `Stroke="{Binding WireColor}"`.
   - En [ConnectionViewModel.cs](file:///FileFlow.App/ViewModels/ConnectionViewModel.cs) y [PendingConnectionViewModel.cs](file:///FileFlow.App/ViewModels/PendingConnectionViewModel.cs): Se implementó la propiedad reactiva `WireColor` que refleja fielmente el color del tipo de dato del puerto de origen (`Source.PortColor`), permitiendo distinguir visualmente flujos de imágenes, metadatos, archivos, hashes, colecciones y números idéntico a ComfyUI.
2. **Pines y Sockets Circulares con Estado Conectado / Desconectado**:
   - En [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml): Se rediseñaron los pines de entrada y salida (`NodeInput` y `NodeOutput`) como sockets circulares de 14x14px que sobresalen del borde de la tarjeta con `Margin="-7,3,0,3"` y `Margin="0,3,-7,3"`.
   - En [PortViewModel.cs](file:///FileFlow.App/ViewModels/PortViewModel.cs): Se configuraron `SocketFillColor` y `SocketBorderColor` de modo que los sockets libres se muestran como anillos huecos con borde coloreado (`#181A22` relleno, `PortColor` borde) y los sockets activos se llenan sólidamente con el color del tipo de datos (`PortColor` relleno y borde).
3. **Validación Exhaustiva**:
   - Suite completa de 838 pruebas pasando al 100%: **838 / 838 superadas (0 fallos, 0 errores)**.

---

## [2026-09-15] - Corrección de Redimensionado de Nodos y Creación de Conexiones en Lienzo Nodify

### 🎯 Problemas Identificados y Solucionados
1. **Conexiones Interactivas entre Nodos en el Lienzo Nodify**:
   - **Causa Raíz**: En [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml), tanto `<nodify:NodeInput>` como `<nodify:NodeOutput>` tenían la propiedad `IsConnected="True"` hardcodeada en XAML. En Nodify, cuando un conector tiene `IsConnected = true`, el gesto de arrastre se interpreta como una desconexión en lugar de la creación de una nueva conexión pendiente (`ConnectionStartedCommand`). Además, `<nodify:NodifyEditor>` en [EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml) no tenía enlazados los comandos de editor `ConnectionStartedCommand`, `ConnectionCompletedCommand` y `DisconnectConnectorCommand`.
   - **Solución**: Se enlazó reactivamente `IsConnected="{Binding IsConnected, Mode=TwoWay}"` en ambos conectores (consumiendo el estado real de `PortViewModel.IsConnected`), y se configuraron los comandos `ConnectionStartedCommand="{Binding StartConnectionCommand}"`, `ConnectionCompletedCommand="{Binding FinishConnectionCommand}"` y `DisconnectConnectorCommand="{Binding DisconnectConnectorCommand}"` en el lienzo `NodifyEditor`.
2. **Redimensionado de Nodos (`Width`) con Tirador de Redimensión (`Thumb`)**:
   - **Causa Raíz**: Aunque el tirador `Thumb` de la tarjeta ejecutaba `node.UpdateWidth(...)` correctamente en `ResizeThumb_DragDelta`, ni el contenedor de elemento (`nodify|ItemContainer`), ni el control raíz `NodeCardView`, ni `<nodify:Node>` tenían enlazada la propiedad `Width`. Por ende, el ancho visual del nodo permanecía estático en el lienzo.
   - **Solución**: Se configuraron enlaces bidireccionales `Width="{Binding Width, Mode=TwoWay}"` tanto en el estilo `nodify|ItemContainer` de [EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml) como en [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml) (`UserControl` y `<nodify:Node>`), permitiendo redimensionar los nodos de forma fluida arrastrando el tirador inferior derecho (`⇲`).
3. **Validación Exhaustiva**:
   - Creados tests unitarios dedicados en [EditorViewLayoutTests.cs](file:///FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs) (`Node_UpdateWidth_ShouldClampAndNotify` y `Ports_ConnectionState_ShouldUpdateCorrectlyOnConnectAndDisconnect`).
   - Suite completa de 838 pruebas pasando al 100%: **838 / 838 superadas (0 fallos, 0 errores)**.

---

## [2026-09-15] - Auditoría Exhaustiva de Seguridad, Rendimiento, Concurrencia y Robustez (QA & Security Fixes)

### 🎯 Problemas Identificados y Solucionados
1. **Seguridad e Inyección de Comandos en CLI y Redes**:
   - En [FfmpegMediaTranscoderService.cs](file:///FileFlow.Core/Services/FfmpegMediaTranscoderService.cs): Reemplazada la concatenación e interpolación de comandos `ProcessStartInfo.Arguments` por `ProcessStartInfo.ArgumentList` con tokenización segura (`TokenizeArguments`), mitigando inyección de comandos arbitrarios en argumentos FFmpeg.
   - En [HttpTransportStrategy.cs](file:///FileFlow.Plugin.Network/Transports/HttpTransportStrategy.cs): Validación estricta del esquema de URI (`http` / `https`) tanto en descargas (`DownloadAsync`) como en subidas (`UploadAsync`), previniendo SSRF y protocolos no autorizados (`file://`, `ftp://`, etc.).
   - En [JintJavaScriptEngine.cs](file:///FileFlow.Plugin.Scripting/Engines/JintJavaScriptEngine.cs): Añadido límite de recursión (`LimitRecursion(1000)`) y aislamiento estricto de memoria para proteger contra desbordamientos de pila o bucles recursivos hostiles.
2. **Concurrencia, Procesos Huérfanos y Sincronización**:
   - En [WorkflowExecutor.cs](file:///FileFlow.Core/Engine/WorkflowExecutor.cs): Blindado el ajuste dinámico de `MaxDegreeOfParallelism` contra `ObjectDisposedException` mediante comprobación de `_disposed` antes de recrear el `SemaphoreSlim` del worker pool.
   - En [SevenZipCliRunner.cs](file:///FileFlow.Plugin.Archives/Services/SevenZipCliRunner.cs): Añadido `process.Kill(entireProcessTree: true)` al capturar `OperationCanceledException` durante `WaitForExitAsync`, previniendo procesos zombis de 7-Zip en disco tras cancelaciones de usuario.
   - En [RoslynCSharpEngine.cs](file:///FileFlow.Plugin.Scripting/Engines/RoslynCSharpEngine.cs): Corregida la política de evicción de caché LRU multihilo usando `FirstOrDefault()` y comprobación de clave no nula en lugar de `.First()`, eliminando `InvalidOperationException` en condiciones de carrera de compilación.
3. **Manejo Seguro de Excepciones y Ciclo de Vida**:
   - En [CustomScriptNode.cs](file:///FileFlow.Plugin.Scripting/CustomScriptNode.cs), [SmartUnpackNode.cs](file:///FileFlow.Plugin.Archives/SmartUnpackNode.cs) y [ArchiveFanOutNode.cs](file:///FileFlow.Plugin.Archives/ArchiveFanOutNode.cs): Encapsulados todos los métodos `async void ExecuteCustomAction(...)` en bloques `try { ... } catch (Exception ex) { ... }`, impidiendo que errores de UI o apertura de modales desestabilicen el SynchronizationContext o cuelguen la aplicación.
   - En [OnnxSessionManager.cs](file:///FileFlow.Plugin.AI/Inference/OnnxSessionManager.cs): Manejo resiliente de inicialización diferida `Lazy<InferenceSession>`, desalojando del `ConcurrentDictionary` cualquier entrada que lance una excepción durante `lazy.Value` para permitir reintentos inmediatos sin reiniciar la app.
   - En [LogViewModel.cs](file:///FileFlow.App/ViewModels/LogViewModel.cs): Implementada la interfaz `IDisposable` para detener de forma limpia el `DispatcherTimer` de volcado de logs periódicos (`_flushTimer`).
   - En [SystemPerformanceMonitor.cs](file:///FileFlow.App/Services/SystemPerformanceMonitor.cs): Desvinculado el manejador de eventos `_timer.Tick -= OnTimerTick` en `Dispose()` para prevenir referencias circulares en el GC.
   - En [OperationReportNode.cs](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/OperationReportNode.cs): Reemplazada la apertura de reportes temporales por comandos multiplataforma (`Process.Start` con `UseShellExecute` en Windows, `open` en macOS, `xdg-open` en Linux).
   - En [ToolboxViewModel.cs](file:///FileFlow.App/ViewModels/ToolboxViewModel.cs): Notificación reactiva de `PerspectiveButtonText` e `IsPipelineRolePerspective` mediante `[NotifyPropertyChangedFor]` y en `RefreshToolbox()`.
4. **Validación Exhaustiva**:
   - Compilación completa de 14 proyectos en .NET 9: **0 Errores, 0 Advertencias**.
   - Suite completa de pruebas unitarias e integración: **836 / 836 superadas al 100% (0 errores, 0 fallos, 0 omitidas)** en 19.5 segundos.

---

## [2026-09-15] - Corrección de Congelamiento (Freeze) y Renderizado de Nodos en Lienzo Nodify / Drag & Drop

### 🎯 Problemas Identificados y Solucionados
1. **Error de parseo XAML en KeyGesture `InputGesture="Del"` en [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml)**:
   - En Avalonia 12, `Key.Delete` es el identificador válido en lugar de `Del` (WPF). Se corrigió a `InputGesture="Delete"`, evitando la excepción `ArgumentException: Requested value 'Del' was not found` al instanciar tarjetas de nodos.
2. **Auto-encapsulación de Convertidores de Valores en [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml)**:
   - Se declararon explícitamente todos los converters de valores (`StringEqualsToBooleanConverter`, `BreakpointToBrushConverter`, `LoggingToBrushConverter`, `LoggingToTooltipConverter`, `NodeExecutionStatusToBrushConverter`, `DurationMsToTextConverter`, `BytesToTextConverter`) en `<UserControl.Resources>`, garantizando resolución inmediata sin dependencia de jerarquía visual al compilarse dentro del `DataTemplate` de Nodify.
   - Conexión de `Anchor="{Binding Anchor, Mode=OneWayToSource}"` en `NodeInput` y `NodeOutput` para sincronizar los pines con los cables de conexión del lienzo.
3. **Manejo Seguro de Drag & Drop en [NodeToolboxView.axaml.cs](file:///FileFlow.App/Views/NodeToolboxView.axaml.cs) y [NodeToolboxView.axaml](file:///FileFlow.App/Views/NodeToolboxView.axaml)**:
   - Se eliminó el inicio síncrono/inmediato de `DragDrop.DoDragDropAsync` en el evento `PointerPressed` básico (que causaba bloqueos del hilo de UI e interceptaba clics simples y botones como la estrella de favoritos).
   - Se implementó detección de umbral de desplazamiento (> 6 píxeles) en `PointerMoved` con retención de `PointerPressedEventArgs`, así como la propiedad computada `FavoriteIcon` (`"★"` / `"☆"`) en `NodeToolboxItem` para evitar conversiones booleanas inválidas en `TextBlock.Text`.
   - Marcado explícito de `e.Handled = true` en `Editor_DragOver` y `Editor_Drop` en [EditorView.axaml.cs](file:///FileFlow.App/Views/EditorView.axaml.cs).
4. **Aislamiento de Descargas en [AiModelDownloader.cs](file:///FileFlow.Plugin.AI/Management/AiModelDownloader.cs)**:
   - Uso de nombres temporales únicos con GUID para evitar colisiones de bloqueo de archivo (`.downloading`) en ejecuciones concurrentes.
5. **Validación Exhaustiva**:
   - Suite completa de 836 pruebas unitarias e integración pasando al 100% (**836 / 836 superadas, 0 errores, 0 fallos**).

---

## [2026-09-15] - Migración Integral Multiplataforma a Avalonia 12 UI + FluentAvaloniaUI (WinUI 3) & Corrección de Type Resolution en Bindings

### 🎯 Objetivos y Alcance
1. **Migración Completa de la Solución a Avalonia 12 (`net9.0`)**:
   - Conversión de `FileFlow.App` y todos los plugins a Avalonia 12.1.2 puro (`TargetFramework: net9.0`), eliminando por completo `<UseWPF>true</UseWPF>` y `net9.0-windows`.
   - Adopción de **`Avalonia.Themes.Fluent` (FluentTheme nativo Avalonia 12)** para estilos Fluent v2 / WinUI 3 puros y diálogos modales nativos multiplataforma en `AvaloniaDialogService`.
   - Integración de **`Nodify.Avalonia 2.0.0`** para el renderizado del lienzo DAG de nodos visuales con zoom, pan y conexiones reactivas.
   - Reemplazo del editor de código por **`Avalonia.AvaloniaEdit 12.0.0`**.
2. **Corrección de Resolución de Tipos y Expresiones en ReflectionBinding**:
   - Corrección de sintaxis no válida de enlace relativo en [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml) reemplazando `$parent[UserControl;1]` por `$parent[views:EditorView].((vm:EditorViewModel)DataContext)` para comandos de copiar, cortar, duplicar y eliminar.
   - Sustitución de sintaxis `using:...` por `clr-namespace:...;assembly=...` en todas las vistas y templates con enlace dinámico (`NodeToolboxView.axaml`, `EditorView.axaml`, `NodeCardView.axaml`, `GroupCardView.axaml`, `AnnotationCardView.axaml`, `InspectorTemplates.axaml`, `ScriptStudioWindow.axaml`).
   - Eliminación del atributo obsoleto de WPF `SharedSizeGroup` en [NodeParameterTemplates.axaml](file:///FileFlow.App/Themes/Templates/NodeParameterTemplates.axaml).
3. **Restauración de Renderizado de Nodos y Drag & Drop en el Lienzo Nodify (`NodifyEditor`)**:
   - Inclusión obligatoria del tema de controles de Nodify en [App.axaml](file:///FileFlow.App/App.axaml): `<StyleInclude Source="avares://Nodify.Avalonia/Themes/Controls.xaml" />`.
   - Definición de selectores de estilo en [EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml) para `nodify|ItemContainer` (enlazando `Location` y `IsSelected` bidireccionalmente en `NodeViewModel`) y `nodify|DecoratorContainer` (enlazando notas adhesivas y grupos).
   - Corrección del controlador de recepción de arrastre (*drop*) en [EditorView.axaml.cs](file:///FileFlow.App/Views/EditorView.axaml.cs) utilizando la API de Avalonia 12 `e.DataTransfer.TryGetText()` y proyectando las coordenadas de pantalla al espacio de viewport con zoom y desplazamiento del lienzo.
4. **Conversión y Modernización de Vistas (`.axaml`)**:
   - Migración de todas las vistas principales y componentes: `MainWindow`, `EditorView`, `ControlBarView`, `StatusBarView`, `LogConsoleView`, `NodeInspectorView`, `ToolboxView`, `NodeCardView`, `ThemeCustomizerWindow`, `VariablePickerWindow`, `VirtualFileSystemExplorerWindow`, etc.
   - Preservación íntegra de los 4 temas visuales (`DarkTheme`, `LightTheme`, `CyberTheme`, `PastelTheme`) con soporte de personalización en caliente vía `ThemeResourceApplier` y `CustomThemeService`.
   - Conversión y registro global en `App.axaml` de todos los converters de valores (`DurationMsToTextConverter`, `BytesToTextConverter`, `LoggingToBrushConverter`, `LoggingToTooltipConverter`, `LogLevelToBadgeBackgroundConverter`, etc.).
   - Desacoplamiento del Clipboard mediante `Avalonia.Input.Platform.IClipboard` y diálogos mediante `TopLevel.StorageProvider`.
4. **Actualización de Scripts de Lanzamiento y Automatización (`run.ps1`, `run-fast.ps1`, `run.bat`, `run-fast.bat`)**:
   - Corrección de la ruta del ejecutable de salida de `net9.0-windows` a `net9.0` puro multiplataforma.
   - Purga completa de binarios residuales de compilaciones previas mediante `clean.ps1`.
5. **Validación Exhaustiva y Suite de Tests**:
   - Compilación limpia de los 14 proyectos en `FileFlow.slnx`: **0 Advertencias, 0 Errores**.
   - Suite completa de pruebas unitarias (`.\test.ps1` / `dotnet test`): **833 / 833 superadas al 100% (0 errores, 0 fallos, 0 omitidas)**.
   - Verificación de arranque en caliente de la ventana principal `MainWindow`.

---

## [2026-09-14] - Reversión de Migración a Avalonia UI y Adaptación Dinámica de Temas en Barra de Estado (WPF)

### 🎯 Objetivos y Alcance
1. **Reversión Integral de la Migración a Avalonia UI**:
   - Reversión limpia del commit `15e32ec` restaurando el 100% de la arquitectura nativa WPF (`net9.0-windows`, `<UseWPF>true</UseWPF>`, Nodify nativo WPF, Vistas XAML y recursos WPF en `FileFlow.App` y plugins satélite).
   - Eliminación de dependencias transitorias y archivos huérfanos `.axaml`.
   - Verificación de arranque nativo sin errores a través de `run.ps1` y `run-fast.ps1`.
2. **Corrección de Temas y Legibilidad en la Barra de Estado (`StatusBarView.xaml`)**:
   - Sustitución de colores oscuros fijos (`#0C101B`, `#111827`, `#1E293B`, `#1F2937`) por recursos dinámicos de tema (`BgHeaderBrush`, `BgSurfaceBrush`, `BorderDarkBrush`).
   - Sincronización visual del fondo de la barra de estado con la barra superior de control (`ControlBarView.xaml`).
   - Sustitución de etiquetas fijas en gris bajo contraste (`#94A3B8`) y valores (`#F8FAFC`, `#E2E8F0`) por `TextPrimaryBrush`, `TextSecondaryBrush` y acentos semánticos (`AccentCyanBrush`, `AccentSuccessBrush`, `AccentPurpleBrush`), garantizando contraste y legibilidad óptima tanto en temas claros (`LightTheme`, `PastelTheme`) como oscuros (`DarkTheme`, `CyberTheme`).
3. **Validación y Métricas**:
   - Compilación completa con `dotnet build FileFlow.slnx --warnaserror`: **0 Advertencias, 0 Errores**.
   - Ejecución de pruebas unitarias de UI (`FileFlow.Tests.Unit.App`): **175 / 175 superadas con éxito**.

---

## [2026-09-13] - Plan de Migración Multiplataforma a Avalonia UI, Publicación Dual y Generador de Instaladores Linux y Windows

### 🎯 Objetivos y Alcance
1. **Plan de Migración Multiplataforma a Avalonia UI (Estrategia A)**:
   - Elaboración del plan integral por fases para migrar la capa de presentación de `FileFlow.App` (WPF `net9.0-windows`) hacia **Avalonia UI (`net9.0`)**, habilitando la ejecución nativa en **Windows (10/11), Linux (X11 / Wayland) y macOS**.
   - Aprobación formal del artefacto de arquitectura y hoja de ruta en 7 fases (`implementation_plan.md`).
2. **Abstracciones de UI en `FileFlow.Sdk` y `FileFlow.App` (Fase 1)**:
   - [`IUiDispatcher.cs`](file:///FileFlow.Sdk/Services/IUiDispatcher.cs) y [`NullUiDispatcher.cs`](file:///FileFlow.Sdk/Services/NullUiDispatcher.cs): Contratos e implementaciones nulas para desacoplar el despacho a hilos de UI (`Post`, `InvokeAsync`, `CheckAccess`) de `System.Windows.Application.Current.Dispatcher`.
   - [`IClipboardService.cs`](file:///FileFlow.Sdk/Services/IClipboardService.cs) y [`NullClipboardService.cs`](file:///FileFlow.Sdk/Services/NullClipboardService.cs): Abstracciones de acceso al portapapeles del sistema operativo de forma asíncrona y segura.
   - [`WpfUiDispatcher.cs`](file:///FileFlow.App/Services/WpfUiDispatcher.cs) y [`WpfClipboardService.cs`](file:///FileFlow.App/Services/WpfClipboardService.cs): Adaptadores de infraestructura enlazados a WPF registrados en el contenedor IoC ([`ServiceCollectionExtensions.cs`](file:///FileFlow.App/Services/ServiceCollectionExtensions.cs)).
3. **Estructura y Compatibilidad de Temas y Conversores (Fase 2)**:
   - Verificación de la compatibilidad del catálogo de paquetes NuGet (`NodifyAvalonia` v6.6.0, `Avalonia` 11.x/12.x).
   - Comprobación de neutralidad de los modelos de datos de temas ([`ThemeDefinition.cs`](file:///FileFlow.App/Themes/ThemeDefinition.cs)) y contratos de servicio ([`IThemeService.cs`](file:///FileFlow.App/Services/IThemeService.cs)).
   - Análisis de adaptación de conversores de datos ([`BooleanConverters.cs`](file:///FileFlow.App/Converters/BooleanConverters.cs), [`TelemetryConverters.cs`](file:///FileFlow.App/Converters/TelemetryConverters.cs)) hacia `Avalonia.Data.Converters.IValueConverter` y la propiedad nativa `IsVisible`.
4. **Desacoplamiento y Portabilidad del Lienzo DAG (Fase 3)**:
   - Eliminación de directivas de UI no utilizadas en [`NodeViewModel.cs`](file:///FileFlow.App/ViewModels/NodeViewModel.cs) y verificación de paridad geométrica `Point`/`Size` entre WPF y `NodifyAvalonia`.
   - Validación de los 25+ tests de interacción de nodos en [`FileFlow.Tests`](file:///FileFlow.Tests/).
5. **Alineación de Paneles de Diagnóstico y Desacoplamiento de Plugins (Fases 4 y 5)**:
   - Desacoplamiento de llamadas de Dispatcher en [`MultimodalVlmConfigViewModel.cs`](file:///FileFlow.Plugin.AI/ViewModels/MultimodalVlmConfigViewModel.cs) mediante inyección de `IUiDispatcher`.
   - Limpieza y verificación de ViewModels auxiliares ([`NodeInspectorViewModel.cs`](file:///FileFlow.App/ViewModels/NodeInspectorViewModel.cs), [`LogViewModel.cs`](file:///FileFlow.App/ViewModels/LogViewModel.cs), [`StatusBarViewModel.cs`](file:///FileFlow.App/ViewModels/StatusBarViewModel.cs), [`ControlBarViewModel.cs`](file:///FileFlow.App/ViewModels/ControlBarViewModel.cs), [`VirtualFileSystemExplorerViewModel.cs`](file:///FileFlow.App/ViewModels/VirtualFileSystemExplorerViewModel.cs), [`WorkflowMetricsDashboardViewModel.cs`](file:///FileFlow.App/ViewModels/WorkflowMetricsDashboardViewModel.cs), [`ThemeCustomizerViewModel.cs`](file:///FileFlow.App/ViewModels/ThemeCustomizerViewModel.cs) y [`TextEditorDialogViewModel.cs`](file:///FileFlow.App/ViewModels/TextEditorDialogViewModel.cs)).
6. **Automatización de Compilación Cruzada y Publicación Dual (Fase 6)**:
   - Creación de [`publish-all.ps1`](file:///publish-all.ps1) y [`publish-all.bat`](file:///publish-all.bat) para compilar y empaquetar de forma unificada y automatizada en un solo comando:
     - `dist/windows-x64/`: Aplicación de escritorio autoincluida (Self-Contained / Single-File) para Windows con carpeta `Config/`.
     - `dist/linux-x64/`: Motor (`engine/` con `Config/`), lanzador `fileflow.sh`, `fileflow.png` y el 100% de los 11 plugins en sus carpetas dedicadas (`Plugins/FileFlow.Plugin.*/`) conteniendo sus binarios, dependencias de dominio, diccionarios de localización (`es/`) y presets de configuración (`Config/*.json`).
7. **Generador de Instaladores, AppImage y Paquetes Nativos para Linux**:
   - Creación de herramientas de empaquetado e instalación en [`installer/linux/`](file:///installer/linux/):
     - [`fileflow.desktop`](file:///installer/linux/fileflow.desktop): Entrada estándar FreeDesktop XDG con categorías, mimetype y soporte de iconos.
     - [`fileflow.sh`](file:///installer/linux/fileflow.sh): Lanzador de bash con resolución automática de rutas y flags de compatibilidad Wayland/X11.
     - [`AppRun`](file:///installer/linux/AppRun): Punto de entrada estándar para el bundle ejecutable AppImage.
     - [`build-appimage.sh`](file:///installer/linux/build-appimage.sh): Script de construcción automatizado de `.AppImage` mediante `appimagetool` con soporte para ejecución en WSL y entornos Linux nativos.
     - [`install.sh`](file:///installer/linux/install.sh): Script universal de instalación con soporte para instalación a nivel de sistema (`/opt/fileflow` y `/usr/local/bin/fileflow`) y modo usuario (`$HOME/.local/share/fileflow` y `$HOME/.local/bin/fileflow`) con auto-creación de accesos directos de escritorio.
     - [`uninstall.sh`](file:///installer/linux/uninstall.sh): Desinstalador limpio para purgar binarios, enlaces y archivos de escritorio.
   - Creación de [`installer/build-linux-installer.ps1`](file:///installer/build-linux-installer.ps1), [`installer/build-linux-installer.bat`](file:///installer/build-linux-installer.bat), [`installer/build-all.ps1`](file:///installer/build-all.ps1) y [`installer/build-all.bat`](file:///installer/build-all.bat):
     - Generación del ejecutable único universal `FileFlow-v{Version}-x86_64.AppImage` con el 100% de plugins y configuraciones integradas.
     - Generación automatizada del bundle `fileflow-linux-x64-v{Version}.tar.gz` con scripts de instalación.
     - Generación del paquete árbol Debian/Ubuntu `fileflow_{Version}_amd64_deb_tree.tar.gz` listo para compilar con `dpkg-deb -b`.
     - Soporte para el flag `-FrameworkDependent` en todas las herramientas para generar distribuibles ultraligeros para sistemas con .NET 9 instalado.
8. **Automatización de GitHub Actions CI/CD Multiplataforma**:
   - Actualización de [`.github/workflows/release.yml`](file:///.github/workflows/release.yml):
     - Pipeline orquestado en 4 fases (`resolve-version`, `build-windows`, `build-linux`, `publish-release`).
     - Generación paralela de instaladores de Windows (`.exe` Inno Setup, `.zip` portable) en `windows-latest` y paquetes de Linux (`.AppImage`, `.deb`, `.tar.gz`) en `ubuntu-latest`.
     - Generación automática de sumas criptográficas SHA-256 (`checksums.txt`) agregadas para todos los distribuibles y publicación en GitHub Releases.
   - Actualización de [`.github/workflows/ci.yml`](file:///.github/workflows/ci.yml):
     - Validación continua de compilación y pruebas en Windows junto con validación de empaquetado del motor y plugins en Linux (`ubuntu-latest`).
9. **Mantenimiento y Limpieza del Repositorio (`.gitignore` & `clean.ps1`)**:
   - Actualización integral de [`.gitignore`](file:///.gitignore) incorporando todos los 11 plugins (`FileFlow.Plugin.*/`), artefactos de distribución multiplataforma (`dist/`, `installer/temp_linux_build/`, `squashfs-root/`, `*.AppImage`), cachés de IDE y carpetas de prueba.
   - Actualización de [`clean.ps1`](file:///clean.ps1) y [`clean.bat`](file:///clean.bat) con detección exhaustiva de carpetas `bin`/`obj` de los 14 proyectos, artefactos de distribución, temporales y modo simulación `-DryRun`.
10. **Métricas de Calidad y Pruebas**:
   - **833 / 833 pruebas unitarias e integración superadas al 100% con éxito**.
   - Compilación limpia bajo `--warnaserror` (0 advertencias, 0 errores).

---

## [2026-09-11] - Soporte Universal de Decodificación y Visualización WebP en WPF y OCR (`WpfImageLoader` & `LocalOcrNode`)

### 🎯 Objetivos y Alcance
1. **Diagnóstico y Causa Raíz**:
   - En Windows/.NET, el componente nativo de WPF `BitmapImage` delega en WIC (Windows Imaging Component), el cual carece de códec nativo para el formato `.webp` en la mayoría de instalaciones estándar de Windows. Al abrir o previsualizar imágenes WebP en el panel de inspección de FileFlow o en el deslizador de comparación antes/después (`ImageCompareSliderControl`), `BitmapImage` fallaba silenciosamente y la UI mostraba un recuadro vacío.
   - En el nodo de OCR local ([`LocalOcrNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/LocalOcrNode.cs)), Leptonica (librería C de bajo nivel de Tesseract) no admite directamente el formato WebP en crudo (`Pix.LoadFromMemory`), generando fallos al procesar documentos en este formato.
2. **Implementación de `WpfImageLoader` en `FileFlow.App`**:
   - [`WpfImageLoader.cs`](file:///FileFlow.App/Preview/Helpers/WpfImageLoader.cs): Cargador universal de imágenes para WPF que combina carga nativa rápida con fallback/decodificación completa vía ImageSharp (`Image.Load` $\rightarrow$ `MemoryStream` PNG a `BitmapSource`). Permite visualizar y comparar de forma 100% determinista y sin dependencias externas cualquier formato moderno (`WebP`, `TGA`, `TIFF`, etc.).
   - [`ImagePreviewProvider.cs`](file:///FileFlow.App/Preview/Providers/ImagePreviewProvider.cs) e [`ImageCompareSliderControl.xaml.cs`](file:///FileFlow.App/Preview/Controls/ImageCompareSliderControl.xaml.cs): Migrados para consumir `WpfImageLoader.LoadBitmapSource`.
3. **Soporte WebP en `LocalOcrNode` (`FileFlow.Plugin.AI`)**:
   - [`LocalOcrNode.cs`](file:///FileFlow.Plugin.AI/Nodes/Language/LocalOcrNode.cs): Transcodificación transparente en memoria a PNG vía ImageSharp antes de invocar `Pix.LoadFromMemory`, permitiendo OCR local de alta precisión sobre archivos `.webp`.
4. **Métricas de Calidad y Pruebas Unitarias**:
   - [`ImageOptimizerNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ImageOptimizerNodeTests.cs): Añadido test `ExecuteAsync_WhenInputIsWebP_ShouldDecodeAndOptimizeSuccessfully` validando la re-optimización y conversión bidireccional de WebP a PNG/WebP.
   - [`FilePreviewerTests.cs`](file:///FileFlow.Tests/Unit/App/FilePreviewerTests.cs): Añadido test `WpfImageLoader_ShouldDecodeWebP_IntoValidBitmapSource` verificando la carga correcta de WebP en WPF `BitmapSource`.
   - **831 / 831 pruebas unitarias e integración superadas al 100% con éxito (0 errores, 0 omitidas)**.
   - Compilación estricta con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

---

## [2026-09-11] - Garantía de Cero Pérdida de Archivos en Empaquetado (Fan-In) y Passthrough Seguro en Optimizador de Imágenes

### 🎯 Objetivos y Alcance
1. **Erradicación de Ficheros Omitidos o Perdidos en Empaquetado Final**:
   - En flujos de cómics (`.cbz`) y archivos comprimidos con contenidos heterogéneos (imágenes mixtas + archivos auxiliares como `ComicInfo.xml`, `metadata.json`, `.nfo`, etc.):
     - Si un archivo no era imagen o fallaba su decodificación en `ImageOptimizerNode`, ImageSharp emitía a la salida `Error`, provocando que el archivo no llegara al `ArchiveFanInNode`. Esto dejaba incompleta la sesión (`ReceivedItems.Count < TotalEntries`) y hacía que el archivo comprimido final no se generara o perdiera todos los ficheros no-imagen.
     - Si se seleccionaba `KeepOriginalIfLarger` en imágenes donde WebP no lograba mejor ratio de compresión, se conservaba el original pero se requería garantizar su inclusión íntegra en el empaquetado final sin colisiones de nombres ni omisiones.
2. **Mejoras en `ImageOptimizerNode` (`FileFlow.Plugin.Images`)**:
   - [`ImageOptimizerNode.cs`](file:///FileFlow.Plugin.Images/ImageOptimizerNode.cs):
     - Incorporado parámetro `PassThroughNonImages` (por defecto `true`): los archivos no-imagen (`.xml`, `.json`, `.txt`, `.nfo`, etc.) o formatos no decodificables pasan directamente y de forma segura al puerto `Out` con el flag `IsImageOptimized = false`.
     - Manejo de excepciones en decodificación: si un archivo dañado no puede ser decodificado, se registra un aviso detallado y se transfiere intacto a `Out` en lugar de romper el pipeline y abortar la sesión de compresión.
3. **Robustez y Resiliencia en `ArchiveFanInNode` (`FileFlow.Plugin.Archives`)**:
   - [`ArchiveFanInNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanInNode.cs):
     - Implementado hook `OnWorkflowCompletedAsync` para finalizar y empaquetar cualquier sesión pendiente que haya recibido elementos aguas arriba.
     - En `CompleteArchiveSessionAsync`: consolidación de entradas recibidas mapeando `Archive:RelativePath` con fallback automático hacia cualquier fichero extraído en `session.WorkingFolder` que no haya sido procesado o sustituido por downstream, asegurando que el 100% de los archivos del cómic/archivo original se preserven sin duplicados.
4. **Métricas de Calidad y Pruebas Unitarias**:
   - [`ArchiveFanOutFanInPipelineTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ArchiveFanOutFanInPipelineTests.cs): Añadida prueba `DirectLinearPipeline_WhenPipingAllExtractedItemsDirectlyThroughOptimizer_ShouldPreserveAllEntriesAndNonImages` verificando la preservación del 100% de entradas (imágenes optimizadas, imágenes originales conservadas y ficheros `ComicInfo.xml` / `notes.txt`).
   - [`ImageOptimizerNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ImageOptimizerNodeTests.cs): Pruebas añadidas para verificar el comportamiento de `PassThroughNonImages` (true/false).
   - **829 / 829 pruebas unitarias e integración superadas al 100% con éxito (0 errores, 0 omitidas)**.
   - Compilación estricta sin advertencias (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).

---

## [2026-09-11] - Gestión de Ciclo de Vida y Limpieza Determinista del Espacio Temporal de Ejecución (Temp Workspace Lifecycle & Housekeeping)

### 🎯 Objetivos y Alcance
1. **Erradicación de Basura Temporal y Contención de Disco**:
   - En flujos complejos (Fan-Out/Fan-In de cómics, conversiones masivas de imágenes, IA de visión), se generaban miles de subcarpetas aleatorias no rastreadas (`Guid.NewGuid()[..8]`) a través de `ParameterHelper.ResolveIntermediateOutputDir`.
   - Se diseñó e implementó una arquitectura integral de **espacio de trabajo temporal acotado y determinista por ejecución de flujo** (`Runs/{ExecutionId}/`), garantizando limpieza total al terminar, cancelar o fallar el flujo.
2. **Contratos en `FileFlow.Sdk`**:
   - [`ITempWorkspaceManager.cs`](file:///FileFlow.Sdk/Storage/ITempWorkspaceManager.cs): Interfaz para la gestión del espacio temporal de ejecución (`ExecutionTempDirectory`, `CreateSubdirectory`, `RegisterTemporaryFile`, `RegisterTemporaryDirectory`, `CleanupExecutionWorkspaceAsync`).
   - [`NullTempWorkspaceManager.cs`](file:///FileFlow.Sdk/Storage/NullTempWorkspaceManager.cs): Implementación segura no-op para tests y modo virtual sin fugas.
   - [`IFlowExecutionContext.cs`](file:///FileFlow.Sdk/IFlowExecutionContext.cs): Expone `TempWorkspace`, `RegisterTemporaryFile` y `RegisterTemporaryDirectory`.
   - [`AppPaths.cs`](file:///FileFlow.Sdk/Storage/AppPaths.cs): Incorpora `RunsDirectory` y la utilidad estática `CleanupStaleTempDirectories(TimeSpan? maxAge)` para purgar ejecuciones residuales o abandonadas.
   - [`ParameterHelper.cs`](file:///FileFlow.Sdk/ParameterHelper.cs): `ResolveIntermediateOutputDir` ahora canaliza los temporales hacia `context.TempWorkspace.CreateSubdirectory("intermediate")` o la carpeta de sesión de Fan-Out, eliminando la creación descontrolada de GUIDs aleatorios.
3. **Motor de Orquestación en `FileFlow.Core`**:
   - [`WorkflowWorkspaceManager.cs`](file:///FileFlow.Core/Engine/WorkflowWorkspaceManager.cs): Gestor concurrente y seguro (`ConcurrentBag<string>`, `System.Threading.Lock`, `IDisposable`, `IAsyncDisposable`) que calcula tamaños liberados y purga todos los archivos y carpetas registrados.
   - [`WorkflowExecutionContext.cs`](file:///FileFlow.Core/Engine/WorkflowExecutionContext.cs): Vincula `TempWorkspace` directamente con el `WorkspaceManager` del ejecutor activo.
   - [`WorkflowExecutor.cs`](file:///FileFlow.Core/Engine/WorkflowExecutor.cs): Inicializa `WorkspaceManager` por cada ejecución con un `ExecutionId` dedicado y ejecuta la purga en bloques `finally` (garantizado ante éxito, cancelación o error fatal) si `AutoCleanIntermediateTempFiles == true`.
4. **Optimización en Nodos de Plugins (`FileFlow.Plugin.Archives` e `Images`)**:
   - [`ArchiveFanOutNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanOutNode.cs): Registra el directorio de sesión en `context.RegisterTemporaryDirectory` para limpieza preventiva si el pipeline aborta antes del Fan-In.
   - [`ArchiveFanInNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanInNode.cs): Empaqueta **única y exclusivamente las entradas recibidas en `session.ReceivedItems`**, mapeando sus rutas relativas actualizadas (`Archive:RelativePath`). Se elimina el escaneo ciego de archivos en disco en `session.WorkingFolder`, erradicando que convivan archivos originales e imágenes optimizadas duplicadas en el archivo final.
   - [`ImageOptimizerNode.cs`](file:///FileFlow.Plugin.Images/ImageOptimizerNode.cs): Parámetro `FileNameSuffix` (por defecto `""`). Genera nombres limpios sin sufijo `_optimized` innecesario y gestiona escrituras en el mismo archivo con archivo temporal intermedio seguro.
5. **Preferencias de Usuario e Interfaz Gráfica (`FileFlow.App`)**:
   - [`UserPreferencesData.cs`](file:///FileFlow.App/Services/UserPreferencesService.cs): Nuevas propiedades persistentes `AutoCleanIntermediateTempFiles` (por defecto `true`) y `CleanStaleTempOnStartup` (por defecto `true`).
   - [`App.xaml.cs`](file:///FileFlow.App/App.xaml.cs): Tarea en segundo plano no bloqueante al inicio que purga automáticamente ejecuciones huérfanas (> 2 horas).
   - [`WorkflowSettingsViewModel.cs`](file:///FileFlow.App/ViewModels/WorkflowSettingsViewModel.cs) y [`WorkflowSettingsWindow.xaml`](file:///FileFlow.App/Views/Components/WorkflowSettingsWindow.xaml): Nuevos controles en la pestaña de Almacenamiento y comando interactivo `CleanTemporaryFilesNowCommand` con reporte de MB liberados.
   - Recursos multilingües actualizados en [`Strings.resx`](file:///FileFlow.App/Resources/Strings.resx) y [`Strings.es.resx`](file:///FileFlow.App/Resources/Strings.es.resx).
6. **Métricas de Calidad y Pruebas**:
   - Creados [`TempWorkspaceManagerTests.cs`](file:///FileFlow.Tests/Unit/Sdk/TempWorkspaceManagerTests.cs), [`StaleTempHousekeeperTests.cs`](file:///FileFlow.Tests/Unit/Sdk/StaleTempHousekeeperTests.cs), [`WorkflowWorkspaceCleanupIntegrationTests.cs`](file:///FileFlow.Tests/Integration/WorkflowWorkspaceCleanupIntegrationTests.cs) y ampliados [`ArchiveFanOutFanInPipelineTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ArchiveFanOutFanInPipelineTests.cs) con prueba de cero duplicados.
   - **827 / 827 pruebas unitarias e integración superadas al 100% con éxito (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Motor de Descompresión Universal Multi-Estrategia (.NET 9 Zip, 7-Zip CLI Universal y SharpCompress Resiliente)

### 🎯 Objetivos y Alcance
1. **Erradicación de Extracciones Truncadas y Fallos Silenciosos**:
   - En archivos sólidos (*solid archives* en 7z, RAR5, CBR) y códecs avanzados (Zstandard/ZIPX, Deflate64, diccionarios RAR5 >128 MB), la extracción directa por acceso aleatorio de SharpCompress causaba interrupciones silenciosas tras descomprimir 1 o 2 ficheros.
   - Se diseñó un motor híbrido multi-estrategia con fallback automático transparente:
     - **Motor 1 (.NET 9 `DotNetZipArchiveExtractor`)**: Descompresión nativa de ultra-alta velocidad para `.zip`, `.cbz`, `.epub`, `.jar`, 100% compatible con Zip64, UTF-8 y protección anti Zip-Slip.
     - **Motor 2 (7-Zip CLI Universal `SevenZipCliRunner`)**: Auto-detección en Windows (`C:\Program Files\7-Zip\7z.exe`, `PATH`, ruta personalizada) para soportar el 100% de formatos y compresiones del mundo (RAR5, 7z LZMA2, CBR, CB7, ZIPX, split volumes).
     - **Motor 3 (SharpCompress Resiliente `SharpCompressResilientExtractor`)**: Modo administrado en C# con stream por stream continuo y captura `try/catch` individualizada por entrada para que ningún archivo dañado aborte el resto del lote.
2. **Orquestación Centralizada en `SafeArchiveExtractor.UniversalExtractAsync`**:
   - Selector configurable de motor (`ArchiveExtractionEngine.Auto`, `SevenZip`, `DotNetZip`, `SharpCompress`).
   - Retorno estructurado `ArchiveExtractionResult` con telemetría de motor usado, ficheros extraídos y avisos no fatales.
3. **Actualización de Nodos y Filtros**:
   - [`SmartUnpackNode.cs`](file:///FileFlow.Plugin.Archives/SmartUnpackNode.cs) y [`ArchiveFanOutNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanOutNode.cs): Incorporados parámetros `ExtractionEngine` y `CustomSevenZipPath` integrados con `UniversalExtractAsync`.
   - [`ArchiveFilterNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFilterNode.cs) y [`ArchiveVolumeResolver.cs`](file:///FileFlow.Plugin.Archives/Services/ArchiveVolumeResolver.cs): Ampliado el reconocimiento regex para cómics y formatos modernos (`.cbz`, `.cbr`, `.cb7`, `.zipx`, `.zst`, `.epub`).
4. **Localización e Internacionalización Autónoma (i18n)**:
   - Diccionarios [`Strings.resx`](file:///FileFlow.Plugin.Archives/Resources/Strings.resx) y [`Strings.es.resx`](file:///FileFlow.Plugin.Archives/Resources/Strings.es.resx) actualizados exclusivamente dentro de `FileFlow.Plugin.Archives`.
5. **Métricas de Calidad y Pruebas Unitarias**:
   - Creados [`DotNetZipArchiveExtractorTests.cs`](file:///FileFlow.Tests/Unit/Plugins/DotNetZipArchiveExtractorTests.cs), [`SevenZipCliRunnerTests.cs`](file:///FileFlow.Tests/Unit/Plugins/SevenZipCliRunnerTests.cs), [`SharpCompressResilientExtractorTests.cs`](file:///FileFlow.Tests/Unit/Plugins/SharpCompressResilientExtractorTests.cs), [`UniversalArchiveExtractorTests.cs`](file:///FileFlow.Tests/Unit/Plugins/UniversalArchiveExtractorTests.cs), y ampliados [`ArchiveFilterNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ArchiveFilterNodeTests.cs) y [`SmartUnpackNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/SmartUnpackNodeTests.cs).
   - **819 / 819 pruebas unitarias e integración superadas al 100% con éxito (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Patrón Genérico Fan-Out / Fan-In de Archivos Comprimidos (CBZ/ZIP/7Z), Preservación de Subcarpetas y Optimización Condicional de Imágenes

### 🎯 Objetivos y Alcance
1. **Preservación Estricta de Subcarpetas y Empaquetado Jerárquico Determinista**:
   - En [`ArchiveFanInNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanInNode.cs) y [`ArchiveCompressorNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveCompressorNode.cs), se sustituyó el empaquetado por un recorrido recursivo determinista que inyecta cada archivo con su ruta relativa exacta normalizada (`Path.GetRelativePath(..., ...).Replace('\\', '/')`), garantizando que todas las subcarpetas internas (ej. `Capitulo 1/01.webp`, `CD1/track01.mp3`, etc.) se empaqueten íntegras sin omitir archivos.
   - En [`ArchiveFanOutNode.cs`](file:///FileFlow.Plugin.Archives/ArchiveFanOutNode.cs), se cambió `CleanWrapper` por defecto a `false` para preservar cualquier estructura de subcarpetas interna por defecto y evitar aplanados accidentales de directorios legítimos.
2. **Desempaquetador Streamer `ArchiveFanOutNode` (`FileFlow.Plugin.Archives`)**:
   - Descomprime archivos comprimidos multiformato (`.cbz`, `.zip`, `.7z`, `.tar.gz`, `.rar`) en una sesión de trabajo temporal aislada (`Path.GetTempPath()/FileFlow_Sessions/{SessionId}`).
   - Emite cada archivo interno individualmente como un `FileItemContext` hacia el pipeline DAG, inyectando metadatos canónicos de correlación de sesión (`Archive:SessionId`, `Archive:OriginalArchivePath`, `Archive:OriginalArchiveFileName`, `Archive:OriginalArchiveFormat`, `Archive:RelativePath`, `Archive:EntryIndex`, `Archive:TotalEntries`, `Archive:WorkingFolder`).
   - Soporte para descompresión segura anti Zip-Slip, eliminación de carpeta envoltorio redundante (`CleanWrapper`), eliminación del comprimido original y contraseñas.
3. **Barrera de Agregación y Re-Empaquetador `ArchiveFanInNode` (`FileFlow.Plugin.Archives`)**:
   - Recibe los elementos procesados downstream vinculados a `Archive:SessionId`.
   - Mantiene el estado de la sesión protegido por `System.Threading.Lock`.
   - Cuando todos los elementos (`ReceivedCount >= TotalEntries`) han llegado, sincroniza y re-empaqueta automáticamente la carpeta de la sesión en el archivo final en `DestinationFolder`.
   - Soporte para alias de formato `CBZ`, `ZIP`, `7Z`, `TAR`, `GZ` y resolución dinámica de nombres (`{Archive:OriginalArchiveFileName}`, `{FileNameWithoutExtension}.cbz`).
   - Limpieza automática del directorio temporal de sesión (`CleanWorkingFolder`) y emisión del archivo comprimido resultante con telemetría de compresión y ahorro.
4. **Optimización Condicional con Comparación de Peso en `ImageOptimizerNode` (`FileFlow.Plugin.Images`)**:
   - Nuevos parámetros `KeepOriginalIfLarger` y `ReplaceOriginalInPlace`.
   - Si `KeepOriginalIfLarger == true` y la imagen convertida (ej. WebP) resulta más pesada o igual que la original, se descarta el archivo generado y se conserva el archivo original emitiendo `IsOriginalKept = true`.
   - Si `ReplaceOriginalInPlace == true` y la conversión es exitosa y menor en peso, elimina el archivo original.
5. **Autodescubrimiento de Variables en `VariableDiscoveryService`**:
   - Soporte para `{Archive:SessionId}`, `{Archive:OriginalArchivePath}`, `{Archive:OriginalArchiveFileName}`, `{Archive:OriginalArchiveFormat}`, `{Archive:RelativePath}`, `{Archive:TotalEntries}`, `{Archive:SavedPercent}`, `{Archive:CompressedSize}` en el selector de variables (`{x}`).
6. **Métricas de Calidad y Pruebas**:
   - Creados [`ArchiveFanOutNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ArchiveFanOutNodeTests.cs), [`ArchiveFanInNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ArchiveFanInNodeTests.cs) (incluyendo prueba de preservación de directorios anidados), [`ArchiveFanOutFanInPipelineTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ArchiveFanOutFanInPipelineTests.cs) y ampliados [`ImageOptimizerNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/ImageOptimizerNodeTests.cs).
   - **806 / 806 pruebas unitarias e integración superadas al 100% con éxito (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Sincronización en Hilo Dispatcher para Colecciones de Variables en Diálogo VLM y Streaming Fluido Downstream

### 🎯 Objetivos y Alcance
1. **Corrección de Excepción de `CollectionView` en Banco de Pruebas de 1 Ciclo (`MultimodalVlmConfigViewModel.cs`)**:
   - Al ejecutar la inferencia de prueba de 1 ciclo, la tarea asíncrona reanudaba en un hilo del ThreadPool y modificaba la colección observable `SampleDiscoveredVariables` vinculada al `DataGrid` de WPF.
   - Esto provocaba que `CollectionView` lanzara `NotSupportedException: Este tipo de CollectionView no admite cambios en el SourceCollection de un subproceso distinto del subproceso Dispatcher`, interrumpiendo el bucle de inserción tras la primera variable (`{tipo_documento}`).
   - Se recolectan todas las variables aplanadas en una lista local y se despacha la actualización a `Application.Current.Dispatcher`, asegurando que todas las propiedades y colecciones (`SampleDiscoveredVariables`) se pueblen de forma 100% segura en el hilo de interfaz de usuario.
2. **Diagnóstico y Corrección de Inversión de Semáforos en `WorkflowItemDispatcher.cs`**:
   - Se invirtió el orden de adquisición: las tareas esperan su turno primero en `nodeThrottle` **sin consumir permisos globales**.
   - Una vez obtenido el permiso del nodo, adquieren 1 slot en `concurrencyThrottle`.
   - El pipeline ahora opera en modo *streaming* fluido 1 a 1: conforme el nodo de IA procesa cada imagen, el nodo downstream (`LogOutputNode`) la recibe y procesa de inmediato en tiempo real sin esperas.
3. **Métricas de Calidad y Pruebas**:
   - **800 / 800 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Variables Dinámicas VLM, Structured Outputs Determinista (`json_schema`), Aplanador Recursivo y Banco de Pruebas de 1 Ciclo

### 🎯 Objetivos y Alcance
1. **Autodescubrimiento Topológico de Variables de IA Multimodal en Nodos Aguas Abajo**:
   - En [`VariableDiscoveryService.cs`](file:///FileFlow.App/Services/VariableDiscoveryService.cs), se integró el descubrimiento ascendente para [`MultimodalVisionLlmNode`](file:///FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs) e [`ImageTypeClassifierNode`](file:///FileFlow.Plugin.AI/Nodes/Vision/ImageTypeClassifierNode.cs).
   - Se exponen automáticamente en el catálogo visual de variables (`{x}`) tanto las variables fijas de telemetría (`{AI:VlmResponse}`, `{AI:VlmJson}`, `{AI:VlmCategory}`, `{AI:VlmTags}`, `{AI:VlmReason}`, `{AI:VlmModel}`, `{AI:VlmTokens}`, `{AI:VlmDurationMs}`, `{AI:VlmProvider}`) como las variables de la plantilla seleccionada (`{numero_factura}`, `{importe_total}`, `{emisor_nombre}`, etc.) y variables dinámicas descubiertas interactivamente (`{campo_custom}`).
   - Enriquecido `CreatePreviewItem` con metadatos reales de VLM para visualización fidedigna en tiempo de diseño.
2. **Aplanador Recursivo y Des-anidado Automático de JSON (`JsonMetadataFlattener`)**:
   - Creada la utilidad centralizada [`JsonMetadataFlattener.cs`](file:///FileFlow.Plugin.AI/Utilities/JsonMetadataFlattener.cs) en `FileFlow.Plugin.AI`.
   - Aplana automáticamente objetos JSON anidados (`emisor: { nombre: "Acme" }` $\rightarrow$ `{emisor_nombre}` y `{emisor.nombre}`), formatea arrays y desempaqueta strings que contengan JSONs serializados (resolviendo el problema de "JSON dentro de otro JSON").
   - Integrado en `MultimodalVisionLlmNode.ExecuteAsync` para inyectar automáticamente todas las propiedades en `item.Metadata`.
3. **Structured Outputs Determinista con `json_schema` y Fallback Gradual**:
   - En [`MultimodalVlmClientEngine.cs`](file:///FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs), se añadió `GetPresetJsonSchema` con esquemas canónicos y soporte de `json_schema` en `ExecuteChatCompletionAsync`.
   - Propiedad `JsonSchema` añadida a [`VlmModels.cs`](file:///FileFlow.Plugin.AI/Management/VlmModels.cs) y [`VlmConfigurationStorageService.cs`](file:///FileFlow.Plugin.AI/Management/VlmConfigurationStorageService.cs).
   - Mecanismo de degradación resiliente: si el servidor rechaza `json_schema` con error 400, degrada a `json_object` con caché negativa, y si tampoco lo soporta, reintenta sin `response_format`.
4. **Banco de Pruebas Interactivo de 1 Ciclo con Archivo de Muestra**:
   - En [`MultimodalVlmConfigWindow.xaml`](file:///FileFlow.Plugin.AI/UI/MultimodalVlmConfigWindow.xaml) y [`MultimodalVlmConfigViewModel.cs`](file:///FileFlow.Plugin.AI/ViewModels/MultimodalVlmConfigViewModel.cs), se incorporó la pestaña **"🔬 Probar Muestra (1 Ciclo)"**.
   - Permite seleccionar un archivo local de prueba, ejecutar la inferencia VLM en segundo plano, inspeccionar la salida en vivo (JSON/Texto) y ver el desglose en tabla de todas las variables dinámicas detectadas.
   - Botón **"📥 Importar al Flujo"** para persistir `DiscoveredVariables` en el nodo y propagarlas inmediatamente a los nodos downstream.
5. **Métricas de Calidad y Pruebas**:
   - Nuevas suites de pruebas unitarias: [`JsonMetadataFlattenerTests.cs`](file:///FileFlow.Tests/Unit/AI/JsonMetadataFlattenerTests.cs), ampliaciones en [`VariableDiscoveryServiceTests.cs`](file:///FileFlow.Tests/Unit/App/VariableDiscoveryServiceTests.cs), [`MultimodalVisionLlmNodeTests.cs`](file:///FileFlow.Tests/Unit/AI/MultimodalVisionLlmNodeTests.cs) y [`MultimodalVlmConfigViewModelTests.cs`](file:///FileFlow.Tests/Unit/AI/MultimodalVlmConfigViewModelTests.cs).
   - **800 / 800 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Mensajes Personalizados con Variables Dinámicas y Editor Multilínea Ampliado en el Nodo Registrar Log (`LogOutputNode`)

### 🎯 Objetivos y Alcance
1. **Parámetro `CustomMessage` con Resolución de Expresiones de Plantilla**:
   - Se añadió el parámetro `CustomMessage` en [`LogOutputNode`](file:///FileFlow.Plugin.FileSystem/Nodes/Processing/LogOutputNode.cs) (Plugin `FileFlow.Plugin.FileSystem`).
   - Admite expresiones de plantilla y variables del sistema e ítems (p. ej. `{FileName}`, `{Extension}`, `{SizeKb}`, `{AI:Category}`, `{AI:VlmTags}`, funciones de fecha, etc.) resueltas en tiempo de ejecución mediante [`VariableTemplateResolver.Resolve`](file:///FileFlow.Sdk/TemplateEngine/VariableTemplateResolver.cs).
   - Si `CustomMessage` contiene texto, se resuelve y registra directamente en el contexto (`IFlowExecutionContext.Log`) y en el historial del elemento (`item.AddLog`). Si se deja vacío, el nodo mantiene su comportamiento retrocompatible de resumen de inspección estándar o compacto.
2. **Descriptor de Parámetro con Tipo de Editor Multilínea (`ParameterEditorType.MultiLineText`)**:
   - Se definieron formalmente los `ParameterDescriptors` de `LogOutputNode` con `CustomMessage` como `ParameterEditorType.MultiLineText` en primera posición (`DisplayOrder: 1`).
   - En la interfaz gráfica (`FileFlow.App`), esto activa automáticamente el botón de edición ampliada (`⤢`, `OpenTextEditorCommand`) que abre la ventana modal con resaltado sintáctico y el selector interactivo de variables (`{x}`, `OpenVariablePickerCommand`).
3. **Localización e Internacionalización (i18n)**:
   - Cadenas y descripciones de ayuda añadidas en los diccionarios de recursos del propio plugin: [`Strings.resx`](file:///FileFlow.Plugin.FileSystem/Resources/Strings.resx) y [`Strings.es.resx`](file:///FileFlow.Plugin.FileSystem/Resources/Strings.es.resx) (`Param_CustomMessage`, `Param_CustomMessage_Help`, `LogOutputNode_Name`, `LogOutputNode_Desc`, etc.).
4. **Métricas de Calidad y Pruebas**:
   - Nuevos tests unitarios en [`LogOutputNodeTests.cs`](file:///FileFlow.Tests/Unit/Plugins/LogOutputNodeTests.cs) validando la interpolación de variables, el formato por defecto y la configuración del descriptor.
   - **791 / 791 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Concurrencia Configurable en Nodos de IA Multimodal (VLM) y Perfiles de Proveedor

### 🎯 Objetivos y Alcance
1. **Ajuste Dinámico de Concurrencia Máxima por Nodo (`MaxConcurrency`)**:
   - En [`MultimodalVisionLlmNode`](file:///FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs), la propiedad `MaxConcurrency` ahora es dinámica y configurable directamente desde el panel de parámetros del Inspector (`ParameterEditorType.Number`, `Min: 1`, `Max: 32`, Default: 1).
   - Si el usuario dispone de un servidor local (p. ej. LM Studio configurado con 4 slots de inferencia paralela o GPUs de alta capacidad) o un endpoint remoto (OpenAI, Ollama multi-slot), puede configurar la concurrencia a 2, 4 u 8 directamente en la tarjeta del nodo.
2. **Propagación al Motor Cliente y Adaptadores de IA (`MultimodalVlmClientEngine`)**:
   - [`VlmExecutionRequest`](file:///FileFlow.Plugin.AI/Inference/Adapters/IVlmAdapter.cs) y [`OpenAiCompatibleVlmAdapter`](file:///FileFlow.Plugin.AI/Inference/Adapters/OpenAiCompatibleVlmAdapter.cs) ahora transfieren el valor `ConcurrencyLimit` hacia `MultimodalVlmClientEngine.ExecuteChatCompletionAsync`.
   - `MultimodalVlmClientEngine.GetThrottleForEndpoint` gestiona semáforos dimensionados exactamente según la capacidad configurada (`s_endpointThrottles[$"{hostKey}::{count}"]`), permitiendo que las peticiones se ejecuten en paralelo sin serializarse artificialmente a 1.
3. **Soporte en Perfiles de Proveedor y Ventana Modal de Configuración VLM**:
   - En [`VlmModels.cs`](file:///FileFlow.Plugin.AI/Management/VlmModels.cs), se añadió la propiedad observable `ConcurrencyLimit` a `VlmProviderProfile` con persistencia JSON automática en `vlm_providers.json`.
   - En [`MultimodalVlmConfigWindow.xaml`](file:///FileFlow.Plugin.AI/UI/MultimodalVlmConfigWindow.xaml) y [`MultimodalVlmConfigViewModel.cs`](file:///FileFlow.Plugin.AI/ViewModels/MultimodalVlmConfigViewModel.cs), se incluyó el campo de edición para `ConcurrencyLimit`, permitiendo definir el número de slots por defecto para cada proveedor y sincronizarlo al aplicar al nodo.
4. **Métricas de Calidad y Pruebas**:
   - Nuevos tests en [`MultimodalVisionLlmNodeTests.cs`](file:///FileFlow.Tests/Unit/AI/MultimodalVisionLlmNodeTests.cs) y [`VlmConfigurationStorageServiceTests.cs`](file:///FileFlow.Tests/Unit/AI/VlmConfigurationStorageServiceTests.cs) validando la configurabilidad, límites mínimos y persistencia de `ConcurrencyLimit`.
   - **788 / 788 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Corrección de Medición de Rendimiento en Nodos (Eliminación de Distorsión por Cola) y Deduplicación del Contador de Elementos Completados

### 🎯 Objetivos y Alcance
1. **Eliminación de la Distorsión por Tiempo en Cola en la Ficha de Rendimiento e Inspector**:
   - **Causa Raíz Identificada**: El temporizador de ejecución `Stopwatch.GetTimestamp()` en [`WorkflowItemDispatcher`](file:///FileFlow.Core/Engine/WorkflowItemDispatcher.cs) se iniciaba antes de ingresar a `targetNode.ExecuteAsync`. En nodos con concurrencia restringida (como `MultimodalVisionLlmNode` que serializa llamadas a LM Studio mediante un semáforo interno para proteger la GPU local), los 16 hilos del motor DAG entraban en paralelo a `ExecuteAsync` y se quedaban esperando en el semáforo. El despachador medía `tiempo de espera en cola + tiempo real de procesamiento`: el archivo 1 marcaba 1.2s, el archivo 2 marcaba 2.4s (1.2s de cola + 1.2s de proceso) y el archivo 10 marcaba 12s, elevando la media en la tarjeta del nodo a más de 6 segundos de forma totalmente distorsionada.
   - **Solución Arquitectónica**:
     - Se añadió `int MaxConcurrency => 0;` a los contratos [`IFlowNode`](file:///FileFlow.Sdk/IFlowNode.cs) y [`FlowNodeBase`](file:///FileFlow.Sdk/FlowNodeBase.cs).
     - En `WorkflowItemDispatcher`, si el nodo declara `MaxConcurrency > 0`, la admisión se gestiona mediante un semáforo por nodo (`_nodeConcurrencyThrottles`) **antes** de tomar la marca de tiempo `startTicks`.
     - Se incorporó el método `void ReportExecutionDuration(double durationMs)` en [`IFlowExecutionContext`](file:///FileFlow.Sdk/IFlowExecutionContext.cs) y [`WorkflowExecutionContext`](file:///FileFlow.Core/Engine/WorkflowExecutionContext.cs), permitiendo que los nodos con cómputo o inferencia externa (como `MultimodalVisionLlmNode`) reporten directamente la duración neta (`result.DurationMs`) sin que ningún retardo de red o espera local contamine la métrica.
2. **Corrección de Inflación en el Contador de Elementos Procesados**:
   - **Causa Raíz Identificada**: En [`WorkflowItemDispatcher`](file:///FileFlow.Core/Engine/WorkflowItemDispatcher.cs), cada vez que un nodo emitía por un puerto de salida sin conexiones activas (como el puerto `Structured` en `MultimodalVisionLlmNode` cuando solo se conecta `Out`, o el nodo terminal `ExcelReportGeneratorNode`), se llamaba incondicionalmente a `_telemetryTracker.IncrementCompletedFiles()`. Además, al finalizar el bloque de ejecución del nodo sumidero terminal (`!targetContext.HasEmittedAnyDownstream`), se volvía a llamar a `IncrementCompletedFiles()`. Para un lote de 10 archivos, el contador se incrementaba 20 o 30 veces, provocando que la interfaz mostrase cifras anómalas como `20/20 elementos` para 10 archivos de entrada.
   - **Solución Arquitectónica**:
     - En [`WorkflowTelemetryTracker`](file:///FileFlow.Core/Engine/WorkflowTelemetryTracker.cs), se implementó un registro de unicidad concurrente (`ConcurrentDictionary<string, byte> _uniqueCompletedFiles`). El método `IncrementCompletedFiles(string? fileKey = null)` verifica si el identificador único del archivo (`OriginalPath`, `CurrentPath` o `IdString`) ya fue completado previamente en el flujo, garantizando que cada archivo físico se contabilice **exactamente una vez**.
     - En `WorkflowItemDispatcher`, todas las llamadas a `IncrementCompletedFiles` y `RecordCompletedFile` pasan la clave unívoca del elemento, sincronizando el progreso al 100% de concordancia con los archivos reales.
3. **Métricas de Calidad y Pruebas**:
   - Nuevos tests unitarios añadidos a [`WorkflowBottleneckTelemetryTests.cs`](file:///FileFlow.Tests/Unit/Core/WorkflowBottleneckTelemetryTests.cs) verificando la deduplicación de archivos completados y el reporte de duración personalizada.
   - **787 / 787 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Mitigación Definitiva de LM Studio `Channel Error` y HTTP 400: Optimización a 1024px, Cooldown Local y Reintentos Transitorios

### 🎯 Objetivos y Alcance
1. **Análisis Forense de los Logs de LM Studio (`Channel Error` a las 13:21:35)**:
   - Los registros de LM Studio revelaron dos factores críticos:
     - **Sobrecarga de KV Cache y Parches de Visión (1536px)**: En la primera petición exitosa, una sola imagen procesada generó **2441 tokens de prompt**, provocando una advertencia crítica en LM Studio: `W srv alloc: - making room for prompt cache entry, removing oldest entry (size = 139.731 MiB)`. Cada imagen a 1536px consumía ~140 MB de caché de slots en VRAM.
     - **Inundación Concurrente**: Al procesar tandas con la versión previa de la DLL en ejecución, más de 20 peticiones HTTP concurrentes llegaron a LM Studio en el mismo segundo (`13:21:35`), mientras el slot 0 estaba evaluando el prompt al 7.4%. Esto provocó que el proxy Node.js / llama.cpp de LM Studio abortara el canal IPC (`Error: Channel Error: Fetch.onAborted`) y devolviera códigos 400 / 500 para el resto de peticiones en cola.
2. **Reducción de Dimensión Máxima a 1024px (`MaxImageDimension = 1024`)**:
   - Se ajustó el valor por defecto de 1536px a 1024px en [`MultimodalVlmClientEngine.PrepareImageAsBase64Jpeg`](file:///FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs), en [`MultimodalVisionLlmNode`](file:///FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs) y en todos los perfiles de proveedor de [`VlmConfigurationStorageService`](file:///FileFlow.Plugin.AI/Management/VlmConfigurationStorageService.cs).
   - Para modelos como `Qwen2.5-VL-7B-Instruct`, 1024px mantiene el 100% de la precisión visual (OCR, facturas, detalles de fotos) pero **reduce los tokens de visión en más de un 60%** (~850-1000 tokens en lugar de ~2500 tokens). La memoria de KV Cache por slot cae de ~140 MB a ~40 MB, eliminando las expulsiones forzadas de caché de prompt en llama-server.
3. **Periodo de Enfriamiento (Cooldown) para Endpoints Locales**:
   - En [`MultimodalVlmClientEngine.cs`](file:///FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs), en el bloque `finally` tras la inferencia, se agregó una pausa de 250 ms (`IsLocalEndpoint`) antes de liberar el semáforo de concurrencia. Esto otorga al motor llama-server de LM Studio el tiempo necesario para liberar tensores y reciclar el slot de inferencia antes de que el siguiente elemento del pipeline inicie su transmisión.
4. **Reintento Transitorio ante Códigos 400 con `Channel Error` o `Socket Aborted`**:
   - Si LM Studio retorna HTTP 400 debido a un fallo interno de canal (`channel`, `overload`, `busy`, `terminated`, `aborted`, `slot`), FileFlow no da por perdido el archivo: lo identifica como transitorio y efectúa reintentos con espera progresiva (2s, 4s), permitiendo que el servidor local recupere su slot y complete la clasificación.
5. **Desempaquetado Plano de Metadatos JSON en `MultimodalVisionLlmNode`**:
   - Para alimentar directamente nodos tabulares downstream (como `ExcelReportGeneratorNode` o `CsvExportNode`), `MultimodalVisionLlmNode` ahora analiza el JSON estructurado devuelto y aplana sus propiedades raíz directamente al diccionario `item.Metadata` (`categoria`, `etiquetas_descriptivas`, `motivo`, `AI:VlmTags`, `AI:VlmReason`). Los arrays se formatean automáticamente como cadenas delimitadas por coma.
6. **Métricas de Calidad y Pruebas**:
   - Sincronización completa de binarios en `FileFlow.App\bin\Debug\net9.0-windows\Plugins\FileFlow.Plugin.AI.dll`.
   - **785 / 785 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación estricta con `--warnaserror`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Resiliencia VLM: Serialización de Concurrencia por Host, Caché Negativa de `response_format` y Reintentos 5xx con Backoff

### 🎯 Objetivos y Alcance
1. **Diagnóstico de los Errores HTTP 400 y HTTP 500 (`Channel Error: fetch failed`) en LM Studio**:
   - **Error 500 (Channel Error / Undici `Fetch.onAborted` / `Engine protocol predict request failed`)**: Al procesar carpetas de imágenes en FileFlow, el motor DAG ejecuta elementos en paralelo aprovechando todos los núcleos (`MaxDegreeOfParallelism = Environment.ProcessorCount`, 8-16 hilos). Múltiples hilos enviaban imágenes Base64 gigantescas a `http://localhost:1234` exactamente al mismo segundo. Los servidores locales (LM Studio / llama.cpp) solo disponen de un slot (`launch_slot_: id 1`) y VRAM finita para parches de visión, colapsando el canal IPC interno de LM Studio y abortando sockets.
   - **Error 400 (`'response_format.type' must be 'json_schema' or 'text'`)**: Para presets estructurados, FileFlow enviaba `response_format: {"type": "json_object"}`. Aunque existía un reintento reactivo, no se recordaba la incompatibilidad en memoria, lo que provocaba que cada una de las imágenes de la tanda generase primero un 400 y reintentase inmediatamente, duplicando la avalancha de peticiones y colapsando el servidor.
2. **Implementación de Semáforos de Concurrencia por Host (`s_endpointThrottles`)**:
   - En [`MultimodalVlmClientEngine.cs`](file:///FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs), se introdujo un semáforo dinámico por host (`GetThrottleForEndpoint`).
   - Para endpoints locales (`localhost`, `127.0.0.1`, `::1` en puertos como 1234 o 11434), la concurrencia se limita estrictamente a **1 petición simultánea**, serializando las llamadas a la GPU/LM Studio sin bloquear las demás etapas no-AI del pipeline.
   - Para endpoints remotos se establece un límite balanceado de 4 peticiones simultáneas.
3. **Caché Negativa en Memoria de Incompatibilidad (`s_unsupportedResponseFormatCache`)**:
   - Si un endpoint/modelo devuelve 400 por `response_format`, se registra en memoria `s_unsupportedResponseFormatCache[$"{cleanEndpoint}::{model}"] = true`.
   - Las siguientes imágenes del lote verifican esta caché y omiten directamente `response_format`, evitando generar errores 400 previos y eliminando el tráfico redundante.
4. **Política de Reintentos con Backoff Exponencial para Errores Transitorios 5xx (500, 502, 503, 504)**:
   - Ante errores transitorios 5xx (típicos mientras LM Studio reinicia o cicla sus slots de inferencia), FileFlow efectúa hasta 3 intentos espaciados por backoff exponencial (1.5s, 3s), recuperando la inferencia de manera transparente.
5. **Métricas de Calidad y Pruebas**:
   - Nuevos tests unitarios en [`MultimodalVisionLlmNodeTests.cs`](file:///FileFlow.Tests/Unit/AI/MultimodalVisionLlmNodeTests.cs):
     - Verificación de que la segunda imagen procesada consulta la caché negativa y no genera error 400 ni doble petición.
     - Verificación del reintento exitoso ante un error 500 Channel Error de LM Studio.
   - **19 / 19 pruebas de MultimodalVisionLlmNodeTests superadas al 100%**.
   - Compilación con `--warnaserror`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Corrección de Interpretación de Caracteres Unicode y Serialización JSON Relajada en Visor de Logs e Inspector

### 🎯 Objetivos y Alcance
1. **Causa Raíz Identificada**:
   - Por defecto en .NET (`System.Text.Json`), `JavaScriptEncoder.Default` codifica agresivamente todos los caracteres sensibles a HTML, backticks (`` ` ``) como `\u0060`, comillas dentro de strings como `\u0022`, caracteres matemáticos (`<`, `>`, `+`) y caracteres no-ASCII (acentos como `á` en `\u00E1` o `ñ` en `\u00F1`) a secuencias de escape unicode literales `\uXXXX`.
   - Cuando `WorkflowExecutionContext.Log` serializaba `CurrentItem.Metadata` para generar `detailsJson`, utilizaba `JsonSerializer.Serialize` sin opciones ni codificador relajado, provocando que campos como `AI:VlmResponse` contuvieran `"\u0060\u0060\u0060json\n{\n  \u0022categoria\u0022: \u0022Fotografia_Paisaje\u0022..."`.
   - En el visor de detalles del registro de logs (`LogView.xaml`) y en el panel del inspector de nodos (`NodeInspectorPanelView.xaml`), este texto en crudo se visualizaba con las secuencias unicode sin interpretar.
2. **Creación de `JsonDefaults` en `FileFlow.Sdk/Serialization`**:
   - Se introdujo [`JsonDefaults.cs`](file:///FileFlow.Sdk/Serialization/JsonDefaults.cs) con:
     - `RelaxedOptions` y `RelaxedIndentedOptions`: `JsonSerializerOptions` con `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, preservando caracteres UTF-8 puros, comillas y backticks sin escapar.
     - `UnescapeUnicode`: Regex de alto rendimiento (`\uXXXX`) que decodifica con seguridad secuencias unicode a sus caracteres reales sin dañar rutas de archivos de Windows (ej. `C:\Users`).
     - `SerializeRelaxed`: Serializador centralizado seguro y legible.
     - `FormatDetailsForDisplay`: Prettifier que desescapa secuencias unicode y formatea con indentación cualquier payload JSON para presentación clara al usuario.
3. **Integración en Core, SDK, Plugins y UI**:
   - **`StructuredLogRecord`**: Añadida la propiedad calculada `DisplayDetails` que aplica `JsonDefaults.FormatDetailsForDisplay(DetailsJson)`, y asegurado el desescapado de mensajes en `StructuredLogRecord.Create`.
   - **`WorkflowExecutionContext`**: Todos los métodos `Log` serializan `Metadata` con `JsonDefaults.SerializeRelaxed(..., indented: true)`.
   - **`InProcessVlmAdapter` y `LanguageInferenceEngine`**: Serializan objetos y métricas con `JsonDefaults.SerializeRelaxed` y desescapa respuestas en `VlmInferenceResult`.
4. **Métricas de Calidad y Pruebas**:
   - Creados tests en [`JsonDefaultsTests.cs`](file:///FileFlow.Tests/Unit/Sdk/JsonDefaultsTests.cs).
   - **784 / 784 pruebas unitarias superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación estricta con `--warnaserror`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Rediseño de Layout para Parámetros Multilínea en Tarjetas de Nodo (Prompts a Ancho Completo)

### 🎯 Objetivos y Alcance
1. **Eliminación del Estrangulamiento Horizontal de Parámetros Multilínea (`IsMultiLine`)**:
   - Anteriormente, todos los parámetros del nodo compartían el mismo `SharedSizeGroup="ParamKey"` en una cuadrícula rígida de 2 columnas. Dado que el parámetro `AdditionalPrompt` tenía una etiqueta extensa (*"Instrucciones Adicionales / Prompt Particular"*), la columna 0 se expandía hasta ocupar ~65% de la tarjeta, empujando la caja de texto multilínea y sus botones (`[⤢]` y `[{x}]`) a un espacio restante de apenas ~90px de ancho (un cuadrado diminuto e inutilizable).
   - Se reestructuró la plantilla de parámetros en [`NodeParameterTemplates.xaml`](file:///FileFlow.App/Themes/Templates/NodeParameterTemplates.xaml):
     - **Parámetros de Línea Simple**: Se agrupan ahora bajo la propiedad `IsStandardRow` (`!IsVariableInjectorNode && !IsMultiLine`). La columna de etiquetas (`SharedSizeGroup="ParamKey"`) solo calcula el ancho entre parámetros compactos (ej. *"Proveedor"*, *"Plantilla de Tarea"*, *"Idioma Destino"*), recuperando espacio horizontal generoso para los desplegables.
     - **Parámetros Multilínea**: Cuentan con su propio bloque visual dedicado que aprovecha el **100% del ancho de la tarjeta**:
       - *Fila 0 (Cabecera)*: Etiqueta descriptiva a la izquierda y botones de acción rápida alineados a la derecha (`[⤢]` para abrir el editor modal ampliado y `[{x}]` para insertar variables dinámicas).
       - *Fila 1 (Caja de Texto)*: Cuadro `TextBox` multilínea expandido a ancho completo (`HorizontalAlignment="Stretch"`), con tipografía monospace (`Cascadia Code`), scroll vertical y altura ergonómica.
2. **Refinamiento de Etiquetas de Localización (i18n)**:
   - Se acortó la etiqueta de `Param_AdditionalPrompt`:
     - En español (`Strings.es.resx`): de *"Instrucciones Adicionales / Prompt Particular"* a *"Instrucciones Adicionales"*.
     - En inglés (`Strings.resx`): de *"Additional Instructions / Prompt"* a *"Additional Instructions"*.
3. **Métricas de Calidad y Pruebas**:
   - Agregadas pruebas unitarias en `NodeParameterViewModelTests.cs` evaluando la discriminación de `IsStandardRow` para parámetros de línea simple vs multilínea y la actualización reactiva de `UpdateOptions`.
   - **775 / 775 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación estricta con `--warnaserror`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Categorización en Lenguaje y LLM y Sincronización Reactiva de Nombres de Plantillas en IA Multimodal (VLM)

### 🎯 Objetivos y Alcance
1. **Reubicación a la Categoría Canónica "🧠 Lenguaje y LLM" (`LanguageAI`)**:
   - Se actualizó `MultimodalVisionLlmNode` modificando tanto el metadato del atributo `[NodeDefinition(..., "LanguageAI", ...)]` como la propiedad `Category => "LanguageAI"`.
   - El nodo se integra ahora armónicamente en el cajón de herramientas junto al resto de procesadores de lenguaje (`LocalLlmProcessorNode`, `LocalAiTranslatorNode`, `PromptTransformerNode`, `ZeroShotSemanticSearchNode`).
2. **Nombres Legibles en el Desplegable de Plantillas (`TaskPreset`)**:
   - Se modificó la generación del descriptor de parámetros para extraer los nombres humanos configurados en el editor (`templates.Select(t => t.Name)`), en lugar de identificadores técnicos internos (`t.Id`).
   - El desplegable en la tarjeta del lienzo y en el panel de inspección muestra ahora opciones descriptivas como *"Extracción de Facturas y Recibos (JSON)"*, *"OCR y Resumen Ejecutivo"*, o los nombres personalizados que asigne el usuario a sus plantillas.
   - Preservada 100% la compatibilidad hacia atrás en `ExecuteAsync`, reconociendo tanto por `t.Name` como por `t.Id` y nombres de enum del sistema.
3. **Sincronización Reactiva de Opciones Dinámicas tras Acciones Personalizadas**:
   - Se implementó el método `UpdateOptions(IEnumerable<string>? newOptions)` en `NodeParameterViewModel` con despacho seguro al `Dispatcher` de WPF y preservación del valor seleccionado.
   - En `NodeViewModel.ExecuteCustomAction`, al cerrar el diálogo modal de configuración (`OpenVlmConfig`), se re-evalúan los descriptores de parámetros (`_nodeInstance.ParameterDescriptors`) y se sincronizan tanto las nuevas opciones de las listas desplegables (`param.UpdateOptions(...)`) como los valores seleccionados (`param.Value`).
   - Las plantillas recién creadas, renombradas o eliminadas, así como los nuevos proveedores, aparecen de inmediato en los desplegables de la tarjeta y del inspector sin necesidad de recargar o reinsertar el nodo.
4. **Métricas de Calidad y Pruebas**:
   - Actualizadas las aserciones de categoría y descriptores en `MultimodalVisionLlmNodeTests.cs`.
   - Ajustadas las pruebas en `MultimodalVlmConfigViewModelTests.cs` para validar `DisplayName` y `Name`.
   - **773 / 773 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `--warnaserror`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Mejoras de Configuración VLM: CRUD de Proveedores, Auto-Detección de Modelos, Prompts Expandibles y Esquemas JSON Canónicos

### 🎯 Objetivos y Alcance
1. **Desplegable Editable y Detección Automática de Modelos Locales**:
   - Se transformó el campo de nombre del modelo en un desplegable editable (`ComboBox IsEditable="True"`), permitiendo tanto seleccionar modelos descubiertos automáticamente en servidores locales como escribir nombres arbitrarios a mano.
   - **Corrección de Template WPF para `IsEditable="True"`**: El `ControlTemplate` global de `ComboBox` carecía de `PART_EditableTextBox`, lo que provocaba que no se mostrara texto alguno ni se pudiera hacer clic para escribir con teclado. Se integró `PART_EditableTextBox` con trigger dedicado en `InputStyles.xaml` y se definió un estilo autónomo `ModernComboBox` en `MultimodalVlmConfigWindow.xaml`.
   - **Notificación Reactiva con `ObservableObject`**: Se adaptó `VlmProviderProfile` para heredar de `ObservableObject` con `SetProperty`, garantizando sincronización bidireccional inmediata al cambiar de proveedor o teclear un modelo.
   - Implementado el comando `RefreshModelsCommand` que consulta dinámicamente `/v1/models` (o `/models`) para servidores compatibles con OpenAI / LM Studio y `/api/tags` para servidores Ollama, poblando `AvailableModels`.
   - Soporte para modelos internos en el proveedor *FileFlow In-Process* (`FileFlow-Structural-VLM`, `FileFlow-InProcess-Fast`).
2. **Gestión CRUD Completa de Proveedores de IA**:
   - Posibilidad de crear nuevos proveedores (`➕ Nuevo`), duplicar perfiles existentes (`📋 Duplicar`) y eliminarlos (`🗑️ Eliminar`), con bloqueo de borrado para proveedores de sistema integrados (`IsBuiltIn = true`).
   - Edición completa de `DisplayName` legible e identificador interno `ProviderId`.
   - Sincronización en tiempo de ejecución con las opciones del descriptor de parámetros del nodo `MultimodalVisionLlmNode`.
3. **Editor de Plantillas con Prompts Libres de Restricciones de Altura**:
   - Eliminadas las limitaciones fijas (`MaxHeight`) en los cuadros de texto de `SystemPrompt` y `UserPrompt`.
   - Añadido scroll vertical automático (`VerticalScrollBarVisibility="Auto"`) con alturas mínimas ergonómicas (`MinHeight="140"` y `MinHeight="100"`), permitiendo acomodar instrucciones largas y prompts de razonamiento extenso sin truncamiento.
4. **Estandarización Canónica e Inmutable de Salidas JSON**:
   - Revisadas y actualizadas todas las plantillas del sistema (`ExtractInvoiceReceiptJson`, `QualityInspection`, `ClassifyAndTag`, `DocumentOcrAndSummary`) para requerir nombres de campos fijos y estables en minúsculas con guiones bajos (`numero_factura`, `fecha_emision`, `nif_emisor`, `lineas_articulos`, `importe_total`, `es_valido_para_tramite`, etc.).
   - Añadidas directrices negativas explícitas para impedir alucinaciones o variaciones lingüísticas del LLM que rompan nodos downstream del flujo de trabajo.
   - Sincronizados los generadores sintéticos en `InProcessVlmAdapter` para emitir exactamente las mismas claves JSON canónicas.
5. **Localización (i18n) y Robustez**:
   - Cadenas multilingües añadidas a `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx`.
   - Ajuste de orden en `LocalizationManager.RegisterResourceManager` (`Insert(0, rm)`) para asegurar precedencia de diccionarios de plugins sobre los del host.
   - Aislamiento seguro de colecciones xUnit (`[Collection("RenamerSampleDataTests")]`) para evitar interferencia de estados en memoria entre tests concurrentes.
6. **Métricas de Calidad y Pruebas**:
   - 8 nuevas pruebas unitarias añadidas en `MultimodalVlmConfigViewModelTests.cs` y `VlmConfigurationStorageServiceTests.cs`.
   - **773 / 773 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `--warnaserror`: **0 advertencias, 0 errores**.

---

## [2026-09-11] - Rediseño de Parámetros del Nodo IA Multimodal (VLM) y Ventana Modal Avanzada con Pestañas

### 🎯 Objetivos y Alcance
1. **Reducción Ergonómica de Parámetros Visibles en Diseñador e Inspector**:
   - Se simplificó la tarjeta del nodo y el panel de propiedades en `MultimodalVisionLlmNode`, reduciendo los 14 parámetros apiñados anteriores a únicamente **4 controles esenciales**:
     - `Provider` (Dropdown: LM Studio, Ollama, OpenAI Compatible, In-Process).
     - `TaskPreset` (Dropdown con las plantillas dinámicas activas).
     - `TargetLanguage` (Text: Español, Inglés, etc.).
     - `AdditionalPrompt` (MultiLineText: Directrices particulares para ese nodo concreto).
   - Todos los parámetros técnicos subyacentes se conservan en el diccionario `Parameters` para compatibilidad transparente de deserialización y workflows.
2. **Sistema de Prompts en 2 Niveles (Plantilla Global + Instrucción Local)**:
   - La plantilla seleccionada aporta la estructura fija y el formato (JSON estricto, Markdown, etc.).
   - `AdditionalPrompt` permite especificar directrices puntuales para ese nodo específico sin necesidad de duplicar ni crear plantillas nuevas para cada pequeño matiz.
   - En `ExecuteAsync`, `VariableTemplateResolver` evalúa las variables dinámicas (`{FileName}`, `{Date}`, `{Tag}`, etc.) y las anexa automáticamente al prompt del modelo.
3. **Ventana Modal de Configuración Técnica Avanzada (`MultimodalVlmConfigWindow`)**:
   - Implementado el contrato canónico `INodeCustomActionProvider` en `MultimodalVisionLlmNode` con botón visible en la tarjeta y en el inspector (`⚙️ Configurar Proveedores y Plantillas...`).
   - Creada la interfaz modal XAML con 3 pestañas:
     - **Pestaña 1 (Proveedores de IA)**: Gestión de endpoints, nombres de modelo, API Keys, temperatura, tokens máximos, resolución de VRAM y timeout, con botón interactivo de **`⚡ Probar Conexión (Ping)`** a `/models` con diagnóstico visual en vivo.
     - **Pestaña 2 (Gestor de Plantillas)**: Interfaz Maestro-Detalle con catálogo lateral y badges `[SISTEMA]` / `[USUARIO]`, acciones CRUD (`➕ Nueva`, `📋 Duplicar`, `🗑️ Eliminar`, `🔄 Restaurar Fábrica`), editor de prompts multilínea y opciones de salida.
     - **Pestaña 3 (Vista Previa del Prompt)**: Visor reactivo en tiempo real del prompt completo ensamblado con variables simuladas.
4. **Co-ubicación Estricta y Autonomía Total (Regla 6 Zero-Touch)**:
   - Todos los archivos XAML, ViewModels (`MultimodalVlmConfigViewModel`), convertidores (`VlmUiConverters`), modelos (`VlmModels`), servicios de persistencia (`VlmConfigurationStorageService` en `%AppData%/FileFlow/`) y recursos multilingües (`Strings.resx` y `Strings.es.resx`) residen **exclusivamente dentro de `FileFlow.Plugin.AI`**.
   - `FileFlow.App` permanece 100% desacoplado.
5. **Pruebas y Métricas de Calidad**:
   - Creadas 12 nuevas pruebas unitarias dedicadas: `VlmConfigurationStorageServiceTests.cs` (5 tests) y `MultimodalVlmConfigViewModelTests.cs` (7 tests).
   - Actualizado `MultimodalVisionLlmNodeTests.cs` para validar la nueva superficie de 4 descriptores y custom actions.
   - **765 / 765 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)** (+12 nuevos tests).
   - Compilación estricta `--warnaserror` con **0 advertencias y 0 errores**.

---

## [2026-09-10] - Arquitectura de Adaptadores de IA Multimodal (IVlmAdapter), Motor In-Process y Ciclo de Vida IModelLifecycleNode

### 🎯 Objetivos y Alcance
1. **Desacoplamiento mediante Patrón Adaptador (`IVlmAdapter`)**:
   - Siguiendo los principios de diseño de adaptadores para modelos de IA, se implementó el contrato `IVlmAdapter` y su factoría `VlmAdapterFactory`, permitiendo al usuario alternar dinámicamente entre servidores externos y ejecución interna:
     - `OpenAiCompatibleVlmAdapter`: Gestiona las peticiones HTTP contra servidores locales o remotos (LM Studio, Ollama, OpenAI) delegando en `MultimodalVlmClientEngine`.
     - `InProcessVlmAdapter`: Ejecuta inferencia visual y razonamiento documental 100% in-process dentro de FileFlow Studio sin necesidad de dependencias externas ni procesos en segundo plano.
2. **Motor Multimodal In-Process (`InProcessVlmAdapter`)**:
   - Integrado en `FileFlow.Plugin.AI/Inference/Adapters/InProcessVlmAdapter.cs`:
     - **Análisis de Geometría Visual**: Utiliza `ImageTypeAnalyzerEngine` para inspeccionar la imagen (contraste bimodal, densidad de texto, relación de aspecto, luminosidad).
     - **Extracción de Texto y Metadatos**: Lee el texto OCR existente (`Ocr:Text` o léxico documental) y combina las señales visuales y textuales.
     - **Resolución de Presets Multimodales**:
       - `ExtractInvoiceReceiptJson`: Extrae códigos de factura, importes, fechas y emisor a JSON estructurado y validado.
       - `DocumentOcrAndSummary`: Genera transcripción y resumen ejecutivo formateado en Markdown.
       - `TranslateDocument`: Traduce el contenido al idioma seleccionado (`TargetLanguage`).
       - `ClassifyAndTag`: Genera clasificación documental, tags visuales y justificación en JSON.
       - `QualityInspection`: Realiza auditoría de resolución, contraste, desenfoque y legibilidad.
       - `CustomPrompt`: Inyecta el contexto visual en `LanguageInferenceEngine` para consultas libres.
3. **Ciclo de Vida y Zero-Touch (`IModelLifecycleNode`)**:
   - `MultimodalVisionLlmNode` implementa `IModelLifecycleNode`, permitiendo consultar el estado del modelo y precargar/descargar recursos desde el lienzo visual.
   - Parámetro `Provider` ampliado con la opción `"Internal Engine (In-Process)"`.
   - Inyección de metadatos `AI:VlmProvider` en `FileItemContext`.
   - Cero dependencias añadidas a `FileFlow.App` (cumplimiento estricto de la regla Zero-Touch).
4. **Pruebas y Métricas de Calidad**:
   - Añadidas 9 pruebas unitarias adicionales en `MultimodalVisionLlmNodeTests.cs` evaluando la factoría de adaptadores, la extracción de facturas a JSON in-process, la clasificación visual in-process, la síntesis de resúmenes y el ciclo de vida.
   - **753 / 753 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)** (+9 nuevos tests).
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia en toda la solución: **0 advertencias y 0 errores**.

---

## [2026-09-10] - Nuevo Nodo de IA Multimodal `MultimodalVisionLlmNode` y Motor Cliente `MultimodalVlmClientEngine` (Qwen2.5-VL / LM Studio / Ollama)

### 🎯 Objetivos y Alcance
1. **Integración de Modelos de Visión-Lenguaje de Última Generación (VLM)**:
   - Implementado soporte para inferencia multimodal (texto + imagen de alta resolución) con modelos de la familia **Qwen2.5-VL (7B / 3B)**, Llama-3.2-Vision o Phi-3.5-Vision corriendo localmente vía **LM Studio** (`http://localhost:1234/v1`), **Ollama** (`http://localhost:11434/v1`) o cualquier endpoint OpenAI-compatible.
2. **Motor Cliente HTTP Resiliente y Optimizado (`MultimodalVlmClientEngine`)**:
   - Desarrollado en `FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs`:
     - Preprocesado en memoria con `SixLabors.ImageSharp`: reescalado bicúbico proporcional a dimensión máxima configurable (`MaxImageDimension`, default 1536 px) y compresión JPEG 85% a Base64 Data URL (`data:image/jpeg;base64,...`) para transmisiones ultrarrápidas sin saturar ancho de banda ni VRAM.
     - Payload compatible con OpenAI Chat Completions Multimodal (`image_url`).
     - Extracción y sanitización automática de bloques de código JSON (` ```json ... ``` `).
     - 6 plantillas de prompts predefinidas (`VlmTaskPreset`):
       - `ExtractInvoiceReceiptJson`: Extracción completa de facturas y tickets a JSON estructurado.
       - `DocumentOcrAndSummary`: Transcripción de texto y resumen ejecutivo.
       - `TranslateDocument`: Traducción visual directa preservando formato.
       - `ClassifyAndTag`: Clasificación temática y etiquetado descriptivo.
       - `QualityInspection`: Auditoría de calidad, firmas, sellos y legibilidad.
       - `CustomPrompt`: Prompt libre con soporte de resolución de variables (`{FileName}`, `{Date}`, `{Tag}`).
3. **Nodo de Flujo Multimodal (`MultimodalVisionLlmNode`)**:
   - Desarrollado en `FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs`:
     - Puertos: `In` (entrada), `Out` (salida continua), `Structured` (bifurcación si se extrajo JSON estructurado válido) y `Error`.
     - Inyección de metadatos en `FileItemContext`: `AI:VlmResponse`, `AI:VlmJson`, `AI:VlmCategory`, `AI:VlmModel`, `AI:VlmTokens`, `AI:VlmDurationMs`.
     - Soporte opcional para guardar el resultado como nuevo archivo (`SaveAsNewFile`, `.json` o `.md`).
4. **Localización e Internacionalización (i18n)**:
   - Cadenas en español e inglés añadidas exclusivamente a `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx` manteniendo aislamiento estricto (Zero-Touch en `FileFlow.App`).
5. **Pruebas y Métricas de Validación**:
   - Creadas 8 pruebas unitarias en `FileFlow.Tests/Unit/AI/MultimodalVisionLlmNodeTests.cs` evaluando preprocesamiento de imágenes, sanitización JSON, simulación de respuestas VLM con mock HTTP, guardado de archivos y control de errores.
   - Refactorizada la resolución en `LocalizationManager.cs` a orden inverso (LIFO) para garantizar que los recursos de plugins tengan precedencia sobre cadenas base.
   - **744 / 744 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)** (+8 nuevos tests).
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia: **0 advertencias y 0 errores**.

---

## [2026-09-10] - Nuevo Nodo Especializado `ImageTypeClassifierNode` y Motor de Visión Estructural e IA (`ImageTypeAnalyzerEngine`)

### 🎯 Objetivos y Alcance
1. **Solución a la Limitación de Modelos Multimodales Zero-Shot (CLIP) para Documentos**:
   - Se implementó una solución dedicada y de alto rendimiento que no depende exclusivamente del downsampling destructivo a 224x224 ni de márgenes estrechos de similitud de coseno para clasificar imágenes de documentos escaneados, recibos o capturas.
2. **Motor Determinista de Visión y Heurística Estructural (`ImageTypeAnalyzerEngine`)**:
   - Desarrollado en `FileFlow.Plugin.AI/Engines/ImageTypeAnalyzerEngine.cs` con análisis en una única pasada de píxeles:
     - **Contraste Bimodal y Luminancia**: Detección de fondo blanco/claro (>60% en L > 215) característico de páginas escaneadas y tickets.
     - **Densidad de Líneas Horizontales de Texto**: Detección de transiciones y gradientes horizontales con longitud y espaciado representativo de líneas tipográficas.
     - **Relaciones de Aspecto Estándar**: Discriminación de tickets/recibos alargados (ratio > 1.8), documentos A4 / Carta (~1.41), tarjetas y documentos de identidad ID-1 / DNI (~1.58), y pantallas de visualización (16:9, 19.5:9).
     - **Metadatos EXIF Fotográficos**: Inspección del perfil EXIF de cámara (Make, Model, FNumber, ExposureTime, ISOSpeedRatings) para certificar fotografías reales del mundo físico.
     - **Paleta Discreta de Colores Planos**: Detección de ilustraciones, viñetas, cómics o diagramas mediante histograma cuantizado de color plano (>40%).
     - **Detección Facial Integrada (UltraFace RFB-320)**: Discriminación neuronal inequívoca entre retratos individuales (1 rostro dominante ocupando >8% de área) y fotos grupales (2 o más rostros).
3. **Nodo de Pipeline con Enrutamiento Directo Multi-Puerto (`ImageTypeClassifierNode`)**:
   - Integrado en `FileFlow.Plugin.AI/Nodes/Vision/ImageTypeClassifierNode.cs` con **11 puertos** de salida:
     - 9 puertos de categorías: `Document`, `Receipt`, `Portrait`, `GroupPhoto`, `Photo`, `Screenshot`, `Illustration`, `IDCard`, `Other`.
     - 2 puertos de control estándar: `Out` (emisión universal continua con metadatos) y `Error`.
   - Inyección de metadatos detallados en `FileItemContext`: `AI:ImageType`, `AI:ImageTypeConfidence`, `AI:ImageTypeScoresJson`, `AI:HasFaces`, `AI:FaceCount`, `AI:HasCameraExif`, `AI:AspectRatio`.
   - Parámetros configurables: `ConfidenceThreshold` (Slider 0.10 - 0.95), `EnableFaceDetection` (Toggle), `CheckExifMetadata` (Toggle).
4. **Localización Multilingüe (i18n)**:
   - Añadidas todas las claves de nodo, descripción y parámetros en español e inglés en `FileFlow.Plugin.AI/Resources/Strings.resx` y `Strings.es.resx` manteniendo autonomía total sin alterar `FileFlow.App` (Zero-Touch).
5. **Pruebas y Validación**:
   - Creados 8 tests unitarios exhaustivos en `FileFlow.Tests/Unit/AI/ImageTypeClassifierNodeTests.cs` evaluando documentos sintéticos, recibos térmicos, capturas de pantalla de interfaz, umbrales de confianza y motor directo de análisis.
   - **736 / 736 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)** (+8 nuevos tests).
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia: **0 advertencias y 0 errores**.

---

## [2026-09-15] - Integración de SplashScreenWindow y Corrección de Crash en Inicialización de Vistas

### 🎯 Diagnóstico y Resolución
1. **Diagnóstico del Bloqueo en Inicio**:
   - Inspección del fichero de volcado de excepciones [`%APPDATA%/FileFlow/logs/crash.log`](file:///%APPDATA%/FileFlow/logs/crash.log): se identificó una excepción silenciosa `KeyNotFoundException: Static resource 'BooleanToBrushConverter' not found` lanzada durante la inicialización XAML de [`ControlBarView.axaml`](file:///e:/Users/kaoti/Documentos/GitHub/FileFlow.WT/avalonia/FileFlow.App/Views/ControlBarView.axaml#L55).
2. **Implementación de `BooleanToBrushConverter`**:
   - Creado [`BooleanToBrushConverter`](file:///e:/Users/kaoti/Documentos/GitHub/FileFlow.WT/avalonia/FileFlow.App/Converters/BooleanConverters.cs) con soporte para brochas activa (`#10B981`) y reposo (`#64748B`) y registrado globalmente en [`App.axaml`](file:///e:/Users/kaoti/Documentos/GitHub/FileFlow.WT/avalonia/FileFlow.App/App.axaml).
3. **Activación de `SplashScreenWindow` durante el Arranque**:
   - Conectado [`SplashScreenWindow.axaml`](file:///e:/Users/kaoti/Documentos/GitHub/FileFlow.WT/avalonia/FileFlow.App/Views/SplashScreenWindow.axaml) en el ciclo de vida de [`App.axaml.cs`](file:///e:/Users/kaoti/Documentos/GitHub/FileFlow.WT/avalonia/FileFlow.App/App.axaml.cs) mostrando el progreso en tiempo real de carga de localización, servicios de inyección de dependencias, preferencias, temas, autodescubrimiento de plugins y nodos DAG, antes de realizar la transición suave a `MainWindow`.
   - Ajustada la ventana de SplashScreen a `WindowDecorations="None"`, `CanResize="False"` y bordes translúcidos con sombra.
4. **Validación**:
   - **839 / 839 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias y 0 errores**.

---

## [2026-09-15] - Modernización Integral de Vistas AXAML y Sistema Spotlight Quick-Add (Shift+A / Espacio / Doble Clic)

### 🎯 Objetivos y Alcance
1. **Auditoría e Implementación de Modernización de Vistas AXAML**:
   - **`AboutDialogWindow.axaml`**: Migración de colores hardcodeados a tokens de recursos dinámicos (`BgCardBrush`, `BorderDarkBrush`, `TextPrimaryBrush`, `TextSecondaryBrush`), espaciado simétrico y botones táctiles.
   - **`FilePreviewerControl.axaml` & `ImageCompareSliderControl.axaml`**: Estilización de controles de previsualización e inspección de imágenes con tokens semánticos Fluent y tipografía Cascadia Code.
   - **`WorkflowMetricsDashboardWindow.axaml`**: Localización dinámica de todas las cabeceras de columnas del DataGrid mediante `LocalizationManager.Instance` (`RunTimeHeader`, `TotalTimeHeader`, `SuccessCountHeader`, `FailureCountHeader`, `MemoryUsageHeader`).
   - **`ControlBarView.axaml`**: Reorganización ergonómica en tres islas semánticas bien delimitadas (Modos de Ejecución con indicadores LED, Acciones de Ciclo de Vida en bloques destacados, y Herramientas/Ajustes de Workflow).
   - **`MainWindow.axaml`**: GridSplitters refinados con cursor táctil y sombras de elevación Fluent en el Drawer lateral.

2. **Sistema Spotlight Quick-Add de Nodos (ComfyUI / Blender Style)**:
   - **Atajos de Invocación Rápida**: Invocable en el lienzo DAG mediante `Shift+A`, tecla `Espacio`, doble clic en el fondo del lienzo o mediante el menú contextual "Añadir Nodo...".
   - **Posicionamiento Inteligente**: El nodo creado se sitúa exactamente bajo el cursor del ratón en coordenadas del lienzo teniendo en cuenta el nivel de Zoom y el desplazamiento de la cámara.
   - **Búsqueda Multicriterio Reactiva**: Filtra en tiempo real por título localizado, categoría, descripción y etiquetas de nodo (`Tags`).
   - **Navegación con Teclado**: Teclas `↑`/`↓` para ciclar resultados, `Enter` para instanciar en el lienzo y `Esc` para descartar.
   - **Tarjeta Flotante Estilizada**: Radio de 10px, sombra de elevación `BoxShadow="0 16 48 0 #A0000000"`, badges de categoría e iconos por nodo.

3. **Pruebas y Validación**:
   - **839 / 839 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación estricta con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias y 0 errores**.

---

## [2026-09-15] - Rediseño Visual Completo (Blender Dark Studio + ComfyUI + Nodify.Playground) y Correcciones de Interacción en el Editor DAG

### 🎯 Objetivos y Alcance
1. **Adopción Estética y Funcional de Nodify.Playground**:
   - **HUD de Telemetría en Tiempo Real**: Superposición translúcida en la base del lienzo DAG (`#9914161C`) con métricas reactivas en colores neón:
     - Izquierda: `Selected: X / N` (verde neón `#10B981`), `Connections: C` (ámbar neón `#F59E0B`).
     - Derecha: `Location: X, Y` (naranja neón `#FB923C`), `Zoom: Zx` (azul cian `#06B6D4`) en tipografía monoespaciada.
   - **Pines y Conectores Geométricos Tipados**: En lugar de círculos homogéneos, cada puerto dibuja una forma geométrica distintiva según la naturaleza de sus datos (`SocketShape` en `PortViewModel.cs`):
     - 🟣 **Cuadrado**: Archivos, Streams binarios y Colecciones (`FileItemContext`, `byte[]`, `Stream`, `IEnumerable`).
     - 🔵 **Círculo**: Texto y Cadenas (`string`, tipos genéricos).
     - 🔺 **Triángulo**: Lógica condicional, booleanos y flujo de control (`bool`).
     - 🔶 **Rombo**: Valores numéricos y transformaciones de datos (`int`, `long`, `double`, `float`).
     - Estados interactivos: **anillo / contorno hueco** cuando el puerto está desconectado y **figura sólida rellena** cuando está conectado.

2. **Rediseño Visual de Tarjetas de Nodos (ComfyUI + Blender Studio Aesthetic)**:
   - Tarjetas compactas con fondo neutro carbón `#282828`, bordes nítidos `#3C3C3C` y radio de curvatura de 6px.
   - Cabeceras estilizadas (`#323232`) con barra superior de acento según categoría (`Height="3"`), indicador LED de ejecución, y botones de punto de interrupción y registro de traza.
   - Controles de parámetros embebidos como pastillas oscuras inset (`#1E1E1E`) con bordes sutiles y radio de 4px (`NodeParameterTemplates.axaml`).

3. **Paleta y Tema Global (Blender 4.x Dark Studio)**:
   - Fondo general de la app `#1E1E1E` y lienzo de edición `#1A1D24` con rejilla técnica `#2A2E3B`.
   - Barra de control superior estilizada con botones compactos y badges de estado claros.
   - Panel de herramientas (Toolbox) e Inspector de propiedades con esquinas de 4px y tipografía optimizada.
   - Sincronización completa de tokens en `DarkTheme.axaml` y `ThemeDefinition.cs`.

4. **Correcciones Funcionales en Interacción DAG**:
   - Corrección en el desempaquetado de tuplas de conexión (`(Source, Target)`) en `EditorViewModel.cs` mediante `System.Runtime.CompilerServices.ITuple` para permitir arrastrar y soltar conexiones fluidamente en Nodify.Avalonia.
   - Elevación del menú contextual en las tarjetas de nodos (`NodeCardView.axaml`) para apertura confiable con clic derecho en cualquier punto del nodo.
   - Cables de conexión con curvas Bézier suaves (`Spacing="45"`, grosor `3.5`) y colores dinámicos por tipo de datos.

5. **Modernización UI/UX y Principios Fluent Design de Windows 11**:
   - Escala tipográfica unificada con `Segoe UI Variable Text` / `Segoe UI Variable Display` para la interfaz y `Cascadia Code` / `JetBrains Mono` para telemetría y consola.
   - Sistema de espaciado estándar en rejilla de múltiplos de 4px / 8px (`padding: 4, 8, 12, 16px`).
   - Barra de búsqueda del Toolbox mejorada con icono de lupa (`🔍`) y controles interactivos consistentes.
   - Micro-sombras y elevaciones en tooltips y ventanas flotantes de zoom.

6. **Pruebas y Validación**:
   - **839 / 839 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia: **0 advertencias y 0 errores**.

---

## [2026-09-10] - Corrección Crítica en Inferencia ONNX de CLIP ViT-B/32 y Soporte Multilingüe en Búsqueda Semántica

### 🎯 Objetivos y Alcance
1. **Diagnóstico Profundo de Inferencia ONNX en CLIP (`SemanticEmbeddingEngine`)**:
   - Se diagnosticó mediante pruebas de inspección de tensores por qué las imágenes escaneadas arrojaban puntuación `0.0` en todas las categorías tanto en español como en inglés.
   - **Error 1 (Tipo de tensor en imagen)**: El grafo ONNX de CLIP (`clip-vit-base-patch32.onnx`) requiere `pixel_values` (Float [1,3,224,224]), `input_ids` (Int64) y `attention_mask` (Int64). `GetImageEmbedding` enviaba el tensor Float a `session.InputNames[0]` (que es `input_ids`), provocando una excepción de incompatibilidad de tipos `Float metadata expected: Int64` que caía en el fallback léxico de 384 dimensiones.
   - **Error 2 (Salida errónea en texto)**: `GetTextEmbedding` tomaba `outputs.First()`, que en CLIP corresponde a `logits_per_image` (un escalar 1x1) en vez de `text_embeds` (512 dimensiones).
   - **Error 3 (Discrepancia de dimensiones)**: Al calcular similitud de coseno entre el vector de 384 dimensiones y el de 1 dimensión, `CosineSimilarity` retornaba `0.0` de forma invariable (`vecA.Length != vecB.Length`).
2. **Implementación de Inferencia Multimodal Nativa**:
   - `GetImageEmbedding` ahora inyecta adecuadamente `pixel_values` normalizados, tokens auxiliares y máscara de atención, extrayendo el tensor canónico `image_embeds` de 512 dimensiones.
   - `GetTextEmbedding` inyecta `input_ids`, máscara de atención y tensor de píxeles cero, extrayendo el tensor canónico `text_embeds` de 512 dimensiones.
   - Incorporado tokenizador BPE con vocabulario CLIP (`ClipVocab`) y traducción automática transparente de conceptos en español (`documento` $\rightarrow$ `document`, `factura` $\rightarrow$ `invoice`, `retrato` $\rightarrow$ `portrait photo`, `texto escaneado` $\rightarrow$ `scanned text document`, etc.).
3. **Pruebas y Validación**:
   - **728 / 728 pruebas unitarias e integración superadas al 100% (0 errores, 0 omitidas)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia: **0 advertencias y 0 errores**.

---

## 📜 Historial de Versiones Anteriores (Archivado)

Las fases históricas previas (Fases 1 a 8, Sprints de Agosto 2026 y desarrollos fundacionales anteriores) han sido consolidadas y archivadas para optimización de contexto en:
- 📄 [**`docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md`**](file:///docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md)
