# Bitácora de Ingeniería — FileFlow Studio

> **Qué es esto.** El registro cronológico de lo que se hizo, **lo que se midió** y **qué quedó declarado** como
> frontera, hito a hito: el encargo, los hallazgos de la medida, las piezas, las guardias y la validación con cifras.
>
> **Cómo se lee.** Este fichero es la **ventana viva**: las entradas del tramo en curso. Todo lo anterior vive en
> [`docs/history/`](history/), en archivos fríos por periodo, con su rango de hitos en el índice de abajo. Al cerrar
> un tramo se hace un corte: las entradas que dejan de ser el día a día se **mueven enteras** al archivo —no se
> resumen ni se reescriben— y aquí quedan el índice y la ventana nueva.
>
> **Cómo se añade una entrada.** Va **al principio**, con su fecha y su número de hito. Una entrada vieja **no se
> reescribe**: si una medida se corrige, se dice en la entrada nueva y se deja constancia del error.

## Índice del archivo frío

| Periodo | Hitos | Qué encontrarás | Documento |
| :--- | :--- | :--- | :--- |
| 2026-09-26 → 2026-09-27 | 225 – 254 | El lienzo Uno vivo (selección, arrastre, decoradores, spotlight, migas), la re-tematización en caliente, las mutaciones del tema y del redibujado de cables, el cable pegado a sus sockets, el hit-test del área de clic, el teclado con puntero real y el plan de la rebanada 5 | [`docs/history/2026-09-28_walkthrough_2026-09-26_a_2026-09-27.md`](history/2026-09-28_walkthrough_2026-09-26_a_2026-09-27.md) |
| 2026-09-23 → 2026-09-25 | 169 – 224 | Los paneles del editor del host Uno (caja de herramientas e inspector), las pestañas de Entradas/Salidas/Diff, el «Probar» del inspector, el modo `--selfcheck-uia` y las fases 3.1 a 3.6 del lienzo | [`docs/history/2026-09-28_walkthrough_2026-09-23_a_2026-09-25.md`](history/2026-09-28_walkthrough_2026-09-23_a_2026-09-25.md) |
| 2026-09-10 → 2026-09-22 | 118 – 168 | Los cimientos del host Uno (rebanadas 1 y 2), el rediseño visual, el núcleo portable, el archivo de flujo (versión, reparación y convergencia) y el motor DAG | [`docs/history/2026-09-28_walkthrough_2026-09-10_a_2026-09-22.md`](history/2026-09-28_walkthrough_2026-09-10_a_2026-09-22.md) |
| Antes del 2026-09-10 | — | Fases 1 a 8, los sprints de agosto de 2026 y los desarrollos fundacionales | [`docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md`](history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md) |

## Ventana viva

## [2026-09-30] - Hito 298: Diálogo de Ajustes — Transformación de RadioButtons a Barra de Pestañas Moderna (Tab Bar)

### 🎯 El encargo
«en el dialogo de ajustes de la imagen transforma todos esos radiobuttons de arriba en pestañas.»

### 🔬 El diagnóstico
- En `SettingsPanel.xaml`, la barra superior de selección de secciones utilizaba controles `RadioButton` con su estilo por defecto de WinUI 3, renderizando los glifos circulares tradicionales con punto azul central.
- Visualmente resultaba arcaico e inapropiado para un cuadro de diálogo de configuración moderno, generando ruido visual y ocupando espacio con selectores de formulario en lugar de una barra de pestañas (Tab Bar / Segmented Control) coherente con el resto de la interfaz.

### 🧱 El arreglo
- **Plantilla de Pestaña sin Glifo Circular (`SettingsTabButtonStyle`)**:
  - Declarado un `Style` específico para `RadioButton` en `SettingsPanel.xaml.Resources`.
  - El `ControlTemplate` reemplaza el glifo circular por un `Border` rectangular con esquinas redondeadas (`CornerRadius="6"`), relleno equilibrado (`Padding="12,6"`), tipografía centrada y `ContentPresenter`.
- **Contenedor Unificado de Barra de Pestañas**:
  - La fila de navegación se envuelve en un contenedor `Border` con fondo de tarjeta `Background="{StaticResource CanvasCardBrush}"`, borde fino `BorderBrush="{StaticResource CanvasBorderBrush}"` y `CornerRadius="8"`.
  - Agrupa las 6 secciones («Almacenamiento y Rutas», «Apariencia e Idioma», «Rendimiento y Ejecución», «Herramientas Externas», «Modelos de IA» y «Actualizaciones») en un diseño segmentado y compacto.
- **Estados Visuales Reactivos y Limpios en Code-Behind**:
  - En `SettingsPanel.xaml.cs`, `ShowSection` actualiza el estilo de cada botón mediante `UpdateTabButtonStyle`:
    - **Pestaña Activa**: Fondo sólido `CanvasSurfaceBrush`, borde perimetral `CanvasBorderBrush`, texto en color de acento brillante `CanvasAccentGlowBrush` y peso `FontWeights.SemiBold`.
    - **Pestaña Inactiva**: Fondo transparente sobre la barra, borde transparente, texto secundario `CanvasSecondaryBrush` y peso `Medium`.
    - **Interacción Hover**: Manejadores `OnTabPointerEntered` y `OnTabPointerExited` para iluminar las pestañas inactivas en `CanvasTextBrush` al pasar el puntero.
- **Preservación Total de Contratos y Accesibilidad**:
  - Se conservan todos los `x:Name` (`StorageTabButton`, etc.), `Tag`, `GroupName`, `AutomationProperties.AutomationId` y la tabla de censos `SectionButtons`.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores.
- **Guardias de arquitectura (`dotnet test FileFlow.slnx --filter UnoSettingsSurfaceGuardTests`)**: 13/13 superadas.
- **Suite de mutaciones (`mutate.ps1 -Name seccion-que-pierde-el-panel-que-conmutaba`)**: MUERDE (testigo en rojo, control en verde, árbol restaurado).
- **Sonda de runtime (`.\run-uno-fast.ps1 -SelfCheckSettings`)**: VERIFICADO (exit code 0; apertura con 6 secciones, lectura y cambio de temas, culturas, y conmutación de pestañas).
- **Suite completa de pruebas (`dotnet test FileFlow.slnx`)**: 1.755 superadas (100% de éxito).



### 🎯 El encargo
«en el catalogo de nodos haz que las categorias se diferencien de los nodos que ahora son casi iguales y cambia todos los botones que hay arriba con las categorias por un desplegable que filtre nos nodos segun lo que se selecciones.»

### 🔬 El diagnóstico
1. **Ambigüedad Visual entre Categorías y Nodos**:
   - Tanto el encabezado de categoría como cada nodo hijo compartían un contenedor cuadrado de icono de 24×24 con fondo `CanvasCardBrush`, borde fino de acento, el mismo `PathIcon` de 14×14 en color de acento y tipografía regular de 12px.
   - El encabezado de categoría tenía `Background="Transparent"` por defecto en su plantilla (`CategoryGroupHeaderStyle`), distinguiéndose de los nodos únicamente por un chevron minúsculo de 10×10. Para el usuario, una categoría lucía idéntica a un nodo más dentro de la lista.
2. **Saturación de la Cabecera por Chips de Categoría**:
   - El bloque `CategoryChips` desplegaba un `WrapPanel` con 15 botones de conmutación (`ToggleButton`). Al tener tantas categorías (Archivos, Imágenes, IA, Flujo, Datos, etc.), la cabecera se fragmentaba en múltiples filas de botones que robaban valioso espacio vertical al catálogo y dificultaban la lectura y filtrado ágil.

### 🧱 El arreglo
- **Selector Desplegable `ComboBox` (`CategoryFilterComboBox`)**:
   - Se reemplazó la tira de botones `CategoryChips` por un desplegable `ComboBox` enlazado a `Vm.AvailableCategories` y sincronizado bidireccionalmente con `Vm.SelectedCategoryItem` y `Vm.SelectedCategoryFilter`.
   - Plantilla de ítem rica en `ComboBox.ItemTemplate`: cada opción muestra su icono respectivo convertido por `IconToGeometry`, el nombre de categoría (`DisplayName`) y una pastilla con el contador dinámico de nodos (`Count`).
   - Sincronización en code-behind (`NodeToolboxPanel.xaml.cs`): manejador `OnCategoryFilterSelectionChanged` y actualización automática en `OnVmPropertyChanged` ante cambios de `SelectedCategoryItem` o `SelectedCategoryFilter`.
   - Adaptación de la sonda interna `ChipBoxesForProbe()`: mide la caja de `CategoryFilterComboBox` dentro del panel y valida que todas las categorías declaradas están perfectamente contenidas y visibles dentro de los límites del cajón, manteniendo el contrato de `SelfCheckPanels.cs` y `UnoSelfCheckLayoutGuardTests`.
- **Diferenciación Visual Jerárquica entre Categorías y Nodos**:
   - **Categorías (Tarjetas de Sección Prominentes)**:
     - `CategoryGroupHeaderStyle`: configurado con fondo sólido de tarjeta `Background="{StaticResource CanvasCardBrush}"`, borde visible `BorderThickness="1"` `BorderBrush="{StaticResource CanvasBorderBrush}"`, esquinas redondeadas `CornerRadius="8"` y margen vertical `Margin="0,2,0,2"`.
     - Icono de categoría: alojado en un contenedor destacado de 26×26 con fondo `CanvasSurfaceBrush` y borde acentuado `BorderBrush="{StaticResource CanvasAccentGlowBrush}"`, con icono brillante de 15×15.
     - Tipografía de categoría: `FontSize="12.5"`, `FontWeight="Bold"` para marcar claramente el nivel de título de sección.
     - Contador de categoría: cápsula pill estilizada (`CornerRadius="10"`, relleno `Padding="7,2"`, tipografía `10.5px Bold` en `CanvasAccentGlowBrush`).
     - Chevron de acordeón: más nítido (12×12) con indicadores diferenciados para estado contraído y expandido.
   - **Nodos Hijos (Elementos de Lista Subordinados en Cascada)**:
     - Indentación pronunciada: margen izquierdo incrementado a `22px` (`Margin="22,3,2,4"`), transmitiendo de inmediato su anidación lógica bajo la categoría.
     - En reposo: `Background="Transparent"` y borde transparente, evitando competir con la presencia visual de las tarjetas de categoría.
     - Icono de nodo: contenedor compacto subordinado de 20×20 con esquinas de 4px, fondo transparente y borde con opacidad reducida (`Opacity="0.8"`), con icono de 12×12 en color secundario (`CanvasSecondaryBrush`).
     - Tipografía de nodo: `FontSize="11.5"`, peso `FontWeight="Normal"`.
     - Al pasar el puntero (`PointerEntered`): se resalta interactivamente con fondo `CanvasCardBrush` y borde `CanvasBorderBrush`.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores, 0 fallos de enlace.
- **Suite de mutaciones (`mutate.ps1`)**:
  - `toolbox-que-no-sigue-el-tema`: MUERDE (testigo rojo, control verde, restauración exacta).
  - `estados-fuera-de-la-raiz-de-la-plantilla`: MUERDE (testigo rojo, control verde, restauración exacta).
- **Suite de pruebas unitarias (`dotnet test FileFlow.slnx`)**:
  - Guardias Uno (`UnoToolboxPanelGuardTests`, `UnoThemeRepaintGuardTests`, `UiIconographyTests`, `UnoVisualStateNameGuardTests`): 28/28 superadas.
  - Suite completa: 1.755 pruebas superadas (100% de éxito, 0 errores, 1 omitida justificada).
- **Sonda de runtime del host Uno (`.\run-uno-fast.ps1 -SelfCheck`)**:
  - 100+ comprobaciones en verde (`RESULTADO: VERIFICADO`, exit code 0).
  - Validación del catálogo con 81 nodos y 15 categorías desplegables contenidas en el panel.



### 🎯 El encargo
«en los dialogos la rueda del raton deberia desplazar las lineas abajo y arriba.»

### 🔬 El diagnóstico
1. **Intercepción del contenedor modal en WinUI 3**: En la plataforma WinUI 3 desktop, los `ContentDialog` envuelven internamente su contenido en un `ScrollViewer` de plantilla (`ContentScrollViewer`). Cuando la vista modal (`Content`) tiene un tamaño definido (o menor que la ventana), dicho `ContentScrollViewer` tiene `ScrollableHeight == 0` pero intercepta el evento enrutado `PointerWheelChanged` y lo marca como manipulado (`e.Handled = true`). En consecuencia, los controles hijos (listas `ListView`, editores multilínea `TextBox` y paneles con `ScrollViewer`) no recibían el evento de rueda o lo ignoraban si el foco del teclado estaba en los botones del diálogo.
2. **Propiedades de Scroll en controles de diálogo**:
   - En `TextEditorDialogBody.xaml`, el `EditorBox` multilínea (`AcceptsReturn="True"`) carecía de las propiedades adjuntas `ScrollViewer.VerticalScrollBarVisibility="Auto"` y `ScrollViewer.VerticalScrollMode="Enabled"`, impidiendo que el motor de texto de WinUI 3 desplegara barras de desplazamiento o respondiera al giro de rueda.
   - En `VariablePickerDialogBody.xaml` y `AdvancedRenamerBody.xaml`, diversas listas y paneles de detalle requerían declarar explícitamente `ScrollViewer.VerticalScrollBarVisibility` y `VerticalScrollMode` para activar la virtualización y desplazamiento fluido en el árbol visual.

### 🧱 El arreglo
- **Manejador Inteligente Global en `UnoWindowService.cs` (`EnableDialogMouseWheelScrolling`)**:
  - En `ShowOwnedModalAsync(ContentDialog dialog)` se conecta un controlador a `UIElement.PointerWheelChangedEvent` con `handledEventsToo: true`.
  - Función de resolución de objetivo `FindScrollTarget`: localiza el control desplazable más cercano partiendo de `e.OriginalSource` (el elemento visual exacto bajo el puntero del ratón). Sube por la jerarquía visual detectando instancias de `ListViewBase` (obteniendo su `ScrollViewer` interno), `TextBox` multilínea o `ScrollViewer` que tengan `ScrollableHeight > 0`. Si no está en ancestros directos, explora descendientes del panel bajo el puntero o el propio `Content` del diálogo.
  - Cálculo de desplazamiento proporcional: convierte las muescas de giro de rueda (múltiplos estándar de 120) a desplazamiento vertical en píxeles (~48px por salto, equivalente a ~3 líneas de texto o elementos), aplicando `Math.Clamp` y llamando a `targetScrollViewer.ChangeView(null, newOffset, null, disableAnimation: true)`. Incluye soporte análogo para rueda horizontal si se activa.
- **Configuración de Scroll en Diálogos Específicos**:
  - `VariablePickerDialogBody.xaml`: añadidos `ScrollViewer.VerticalScrollBarVisibility="Auto"` y `ScrollViewer.VerticalScrollMode="Enabled"` en `VariablesList` y el `ScrollViewer` de la columna de detalle.
  - `TextEditorDialogBody.xaml`: añadidos `ScrollViewer.VerticalScrollBarVisibility="Auto"` y `ScrollViewer.VerticalScrollMode="Enabled"` tanto en `EditorBox` como en `VariableList`.
  - `AdvancedRenamerBody.xaml`: añadidos `ScrollViewer.VerticalScrollBarVisibility="Auto"` y `ScrollViewer.VerticalScrollMode="Enabled"` en `StepsListView`, `ReplaceListView` y `PreviewListView`.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores.
- **Suite de guardias (`dotnet test FileFlow.slnx`)**:
  - `UnoNodeDialogCatalogGuardTests`: 5/5 superadas.
  - `UiIconographyTests`: 3/3 superadas (0 emojis no permitidos).
- **Sondas de runtime del host Uno (`.\run-uno-fast.ps1`)**:
  - `-SelfCheck`: verificado (0 fallos, exit 0).
  - `-SelfCheckDialogs`: verificado (0 fallos, exit 0).


## [2026-09-30] - Hito 295: Desbloqueo y Redimensionamiento Interactivo del Diálogo de Catálogo de Variables

### 🎯 El encargo
«en el dialogo de catalogo de variables el contenido queda recortado y no se puede redimensionar»

### 🔬 El diagnóstico
1. **Recorte lateral del panel de detalle**: El diálogo de Catálogo de Variables (`VariablePickerDialogBody.xaml`) está diseñado con una estructura de dos columnas: a la izquierda el listado de variables agrupadas por categoría y a la derecha una columna lateral con la tarjeta de detalle (token `{nombre}`, descripción completa, categoría, origen y muestra evaluada). Debido a que WinUI 3 limita los `ContentDialog` a un `MaxWidth` de 548px de fábrica, la columna derecha de detalle de 280px quedaba recortada por el margen derecho, impidiendo visualizar la información completa de la variable seleccionada.
2. **Imposibilidad de redimensionar**: El diálogo carecía tanto de botón de maximizar/restaurar como de un tirador de arrastre (`Resize Grip`), impidiendo adaptar el tamaño al monitor del usuario o desplegarlo en pantalla grande para examinar variables de descripciones extensas.

### 🧱 El arreglo
- **Ampliación de los Límites en `UnoWindowService.cs`**:
  - En `ShowVariablePickerAsync`, se establecen `dialog.MaxWidth = 2400`, `dialog.MaxHeight = 1600` y se sobreescriben los recursos del diálogo `ContentDialogMaxWidth` y `ContentDialogMaxHeight` a 2400.0 y 1600.0 respectivamente.
- **Redimensionamiento y Flexibilidad en `VariablePickerDialogBody.xaml` y `.xaml.cs`**:
  - `PickerRoot` configurado con dimensiones amplias por defecto: `Width="820"` y `Height="520"`, con márgenes elásticos `MinWidth="640"`, `MaxWidth="2200"`, `MinHeight="420"`, `MaxHeight="1400"`.
  - El panel lateral de detalle se aloja en un contenedor de ancho fijo `280px` con un `ScrollViewer` vertical interno (`VerticalScrollBarVisibility="Auto"`), asegurando que incluso descripciones o tokens extensos se lean con comodidad sin cortar los botones inferiores.
  - **Botón Maximizar/Restaurar** (`MaximizeRestoreButton`) añadido a la cabecera del diálogo: alterna de forma inmediata entre el tamaño estándar (820×520) y el tamaño maximizado adaptado al espacio visible de la ventana (`XamlRoot.Size.Width - 60` × `XamlRoot.Size.Height - 80`), alternando su indicador visual entre `[+]` y `[-]`.
  - **Grip de arrastre vectorial** (`VariablePickerResizeGrip`): tirador visual en la esquina inferior derecha con captura de puntero (`PointerPressed`, `PointerMoved`, `PointerReleased`, `PointerCaptureLost`) y cálculo continuo con `Math.Clamp` para redimensionar libremente con el ratón.
  - Preservados rigurosamente todos los identificadores de automatización (`VariablePickerSearchBox`, `VariablePickerCountText`, `VariablePickerList`, `VariablePickerDetailToken`, `VariablePickerDetailEvaluated`, `VariablePickerCopyTokenButton`) requeridos por las sondas de runtime y herramientas de accesibilidad.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores.
- **Suite de guardias (`dotnet test FileFlow.slnx`)**:
  - `UnoNodeDialogCatalogGuardTests`: 5/5 superadas.
  - `UiIconographyTests`: 3/3 superadas (0 emojis no permitidos).
- **Sondas de runtime del host Uno (`.\run-uno-fast.ps1`)**:
  - `-SelfCheck`: verificado (0 fallos, exit 0).
  - `-SelfCheckDialogs`: verificado (0 fallos, exit 0).


## [2026-09-30] - Hito 294: Redimensionamiento Interactivo y Desbloqueo del Ancho Máximo en Diálogos Modales WinUI 3

### 🎯 El encargo
«el dialogo de pipeline de metodos queda recortado como se ve en la imagen y tampoco se puede redimensionar al dialogo. arreglalo.»

### 🔬 El diagnóstico
1. **Recorte a 548px**: En WinUI 3 / Windows App SDK, la plantilla de control del `ContentDialog` impone un `MaxWidth` predeterminado (`{ThemeResource ContentDialogMaxWidth}`) de tan solo **548 píxeles**. Cuando `AdvancedRenamerBody` solicitaba un ancho de 960px a 1100px, el contenedor del diálogo ignoraba las dimensiones del hijo y lo recortaba en 548px con el scroll horizontal deshabilitado, cortando a la mitad la barra de presets, el editor del método y la previsualización en vivo.
2. **Falta de redimensionamiento nativo en WinUI `ContentDialog`**: WinUI 3 no dota a los `ContentDialog` de bordes de arrastre (`Resize Grip`) ni botón de maximizar/restaurar de serie, imposibilitando al usuario ajustar las proporciones del panel modal según su monitor.

### 🧱 El arreglo
- **Ampliación de los Límites de Tema y Diálogo**:
  - `App.xaml`: declarados `<x:Double x:Key="ContentDialogMaxWidth">2400</x:Double>` y `<x:Double x:Key="ContentDialogMaxHeight">1600</x:Double>` en los recursos de la aplicación para elevar el techo del contenedor del diálogo en todo el host.
  - `UnoWindowService.cs` (`ShowAdvancedRenamerAsync` y `ShowSurfaceAsync`): se asignan explícitamente `dialog.MaxWidth = 2400`, `dialog.MaxHeight = 1600` y sus recursos locales correspondientes (`dialog.Resources["ContentDialogMaxWidth"] = 2400.0`, etc.) para garantizar que el `ContentDialog` nunca constriña superficies anchas.
- **Redimensionamiento Interactivo y Botón de Maximizar en `AdvancedRenamerBody`**:
  - `AdvancedRenamerBody.xaml`:
    - `RenamerRoot` nace ahora con `Width="1020" Height="640"` predeterminado (y límites elásticos `MinWidth="800" MaxWidth="2400" MinHeight="520" MaxHeight="1600"`), permitiendo que todas las secciones quepan desahogadamente desde el primer milisegundo.
    - **Botón Maximizar/Restaurar** (`MaximizeRestoreButton`) en la cabecera: conmuta con un solo clic entre el tamaño estándar (1020×640) y el tamaño maximizado adaptado al espacio visible de la ventana (`XamlRoot.Size.Width - 60` × `XamlRoot.Size.Height - 80`).
    - **Grip de arrastre vectorial** (`RenamerResizeGrip`) en la esquina inferior derecha: un `Border` táctil con un `Path` vectorial diagonal que captura el puntero (`PointerPressed`, `PointerMoved`, `PointerReleased`, `PointerCaptureLost`) y permite redimensionar suavemente en tiempo real el ancho y alto del diálogo arrastrando con el ratón.
  - `AdvancedRenamerBody.xaml.cs`: lógica de cálculo continuo de deltas con sujeción segura (`Math.Clamp`) dentro de los límites visuales de la ventana.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores.
- **Guardias**: `UiIconographyTests` (3/3), `UnoThemeRepaintGuardTests` (6/6), `UnoNodeDialogCatalogGuardTests` (3/3).
- **Sondas de runtime Uno (`run-uno-fast.ps1`)**:
  - `-SelfCheck`: verificado (exit 0).
  - `-SelfCheckDialogs`: verificado (exit 0).

## [2026-09-30] - Hito 293: Superficie de Estudio de Renombrado Avanzado y Acciones de Diálogo Accesibles en Tarjeta e Inspector

### 🎯 El encargo
«en el nodo renombrar archivo aparece un boton pipeline de metodos que al pulsarlo sale el aviso de la imagen cunado deberia abrir el dialogo para configurar los pipelines de renoimbrado. eso mismo pasa en otros nodos. estos botones que tienen algunos nodos deberia tambien aparecer en el inspector para facilidad del usuario.»

### 🔬 El diagnóstico
1. En el nodo `AdvancedRenamerNode`, la acción personalizada «🏷️ Pipeline de Métodos...» delegaba en `DesktopOnlySurface.Declare(...)` sin implementar `INodeDialogSurfaceProvider`. Como resultado, al pulsarse desde la tarjeta en el host Uno, el orquestador mostraba el diálogo de aviso *"Ventana del host de escritorio: este host no tiene el toolkit que la monta"* en vez de desplegar la superficie del editor de pipelines.
2. En `SyntheticDataSourceNode`, se implementaba `INodeDialogSurfaceProvider` pero faltaba declarar `ReplacesCustomActionId => "OpenDataSetDesigner"`, provocando que el fallback cayera igualmente en `DesktopOnlySurface.Declare`.
3. En el Inspector lateral (`NodeInspectorPanel`), los parámetros con superficies de edición dedicadas (como `PipelineName` para el pipeline de renombrado) no exponían el botón de acción de fila `🏷️` asociado, impidiendo abrir la herramienta directamente desde la propiedad.

### 🧱 El arreglo
- **Soporte Canónico de Diálogo para el Renombrador Avanzado**:
  - `IWindowService.cs` (`DialogKeys.AdvancedRenamer`): registrado el identificador `"AdvancedRenamer"`.
  - `AdvancedRenamerNode.cs`: implementado `INodeDialogSurfaceProvider` con `DialogKey => DialogKeys.AdvancedRenamer`, `ReplacesCustomActionId => "OpenRenamerPipeline"`, y fábrica de payload portable `CreateDialogPayload(IServiceProvider)` instanciando `AdvancedRenamerEditorViewModel` con la configuración del nodo.
  - `SyntheticDataSourceNode.cs`: declarado `ReplacesCustomActionId => "OpenDataSetDesigner"`.
- **Implementación de la Vista WinUI 3 `AdvancedRenamerBody`** (`FileFlow.App.Uno/Controls/AdvancedRenamerBody.xaml` y `.xaml.cs`):
  - Vista completa de dos columnas: lista de pasos con reordenación, adición con menú flyout de métodos (NewName, SearchReplace, Insert, Remove, CaseConversion, Numbering, ReplaceList, TrimClean, NormalizeNumbers), panel de edición dinámico según método seleccionado, gestión de presets (cargar, guardar) y vista previa en vivo (Live Preview) con tabla comparativa de nombres y probador de muestras en tiempo real.
  - Iconografía y tipografía limpia conforme a `UiIconographyTests` sin emojis dependientes de fuentes de plataforma.
- **Servicio de Ventanas del Host Uno (`UnoWindowService.cs`)**:
  - Añadido `(DialogKeys.AdvancedRenamer, nameof(AdvancedRenamerBody))` a `ImplementedDialogs`.
  - Método `ShowAdvancedRenamerAsync` montando `AdvancedRenamerBody` dentro de un `ContentDialog` con botones Aceptar/Cancelar y actualización bidireccional del pipeline del nodo.
- **Acceso Directo desde el Inspector (`NodeInspectorPanel.xaml.cs` y `NodeParameterViewModel.cs`)**:
  - `NodeParameterViewModel`: añadida propiedad reactiva `IsRenamerPipeline` y comando `OpenRenamerPipelineCommand` con delegación al servicio de ventanas.
  - `NodeInspectorPanel.xaml.cs`: censada `OpenRenamerPipelineCommand` en `HostRowActions` con ancla `ParamRenamer_` y botón `🏷️` en la fila del parámetro `PipelineName`, garantizando además la accesibilidad de las acciones personalizadas desde la cabecera `_actionsHost`.
  - Preservada la estructura sintáctica exacta para la mutación declarada `fila-de-presets-sin-su-boton`.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores.
- **Suite de pruebas unitarias (`dotnet test FileFlow.Tests`)**: **1755 superadas, 0 errores, 1 omitida** (1756).
- **Guardias de arquitectura**:
  - `UnoNodeDialogCatalogGuardTests`: 5/5 superadas.
  - `DesktopOnlySurfaceGuardTests`: 2/2 superadas.
  - `UiIconographyTests`: 3/3 superadas.
  - `MutationDeclarationGuardTests`: 9/9 superadas.
- **Andamiaje de mutaciones (`mutate.ps1`)**:
  - `acciones-del-nodo-que-solo-se-pulsan-desde-la-tarjeta`: **MUERDE** (1/1).
  - `fila-de-presets-sin-su-boton`: **MUERDE** (1/1).
- **Sondas de runtime Uno (`run-uno-fast.ps1`)**:
  - `-SelfCheck`: **0 errores (exit 0)**.
  - `-SelfCheckDialogs`: **0 errores (exit 0)**.

## [2026-09-30] - Hito 292: Alineación Inmediata de Iconos y Nodos en el Despliegue de Categorías del Catálogo Uno

### 🎯 El encargo
«en el panel del catalogo de nodos al desplegar una categoria los iconos salen desalineados con respecto al texto. luego si hago mas grande o pequeño el panel se alinean, solo pasa al abrirlos. arreglalo»

### 🔬 El diagnóstico
1. En `NodeToolboxPanel.xaml`, el bloque `ToolboxItemDetails` (que aloja la insignia de rol y la descripción detallada) carecía de `Visibility="Collapsed"` en su declaración XAML, por lo que su valor inicial de fábrica era `Visibility="Visible"`.
2. Al desplegar una categoría, WinUI / Uno Platform instanciaba la `DataTemplate` de cada ítem y ejecutaba el pase inicial de `Measure` y `Arrange` con `ToolboxItemDetails` visible, midiendo la tarjeta con una altura extendida de ~60px (nombre + insignia + descripción).
3. En esa tarjeta de 60px, la columna 0 (el `Border` de 24x24 que aloja el icono, con `VerticalAlignment="Center"`) se posicionaba en `Y = 18px` (centrado en los 60px).
4. Inmediatamente después, el evento `Loading` (`OnToolboxItemDetailsLoading`) fijaba `details.Visibility = Visibility.Collapsed` (al estar la app en modo compacto por defecto). El bloque colapsaba y el texto del nombre retrocedía a la cabecera (`Y = 0`), pero el motor de renderizado de Uno no re-disponía la columna 0 del `Grid`, dejando el icono desfasado 18px por debajo de su texto con un espacio en blanco debajo.
5. Al redimensionar la ventana o el panel lateral (incluso un solo píxel), se forzaba un pase global de layout desde la raíz; en ese segundo pase `ToolboxItemDetails` ya figuraba como `Collapsed`, por lo que el `Grid` medía exactamente 24px y tanto el icono como el texto se alineaban a la perfección en el centro.

### 🧱 El arreglo
- **`Visibility="Collapsed"` nativo en XAML** (`NodeToolboxPanel.xaml`): `ToolboxItemDetails` nace directamente con `Visibility="Collapsed"`. Desde la primera medición, el contenedor se crea con 24px de altura, impidiendo cualquier salto vertical o desalineación de la columna 0.
- **Convertidor tipado en la visibilidad del grupo** (`NodeToolboxPanel.xaml`): `ItemsControl.Visibility` pasa a enlazar con `Converter={StaticResource BoolToVis}`.
- **Asentamiento inmediato al desplegar** (`NodeToolboxPanel.xaml.cs`): se añade `Checked="OnCategoryGroupExpanded"` en el `ToggleButton` del encabezado de categoría para forzar la invalidación y asentamiento inmediato del contenedor al abrirse.
- **Propagación en cascada de cambios de visibilidad** (`NodeToolboxPanel.xaml.cs`): `InvalidateDetailsAncestors` asegura que al conmutar entre modos compacto y detallado se recalculen los ancestros inmediatos (`Grid`, `ToolboxItemRoot`), y `OnVmPropertyChanged` evalúa `DispatcherQueue.HasThreadAccess` para aplicar la visibilidad instantáneamente si ya se encuentra en el hilo de UI.
- **Sonda de detalles en runtime**: `ProbeDetailsBlocks` sincroniza `ApplyViewMode` sobre los contenedores recién forzados.

### 📊 Validación del estado
- **Compilación Uno (`FileFlow.Uno.slnx`)**: 0 errores.
- **Sondas de runtime Uno (`run-uno.ps1` / `run-uno-fast.ps1`)**:
  - `-SelfCheck`: **VERIFICADO** (0 fallos, código de salida 0; inventario con 113 verificaciones `[OK]`, 81 ítems, 15 chips, toggle de modo compacto/detallado 20/20 comprobado).
  - `-SelfCheckSettings`: **VERIFICADO** (0 fallos, exit 0).
  - `-SelfCheckControlBar`: **VERIFICADO** (0 fallos, exit 0).
  - `-SelfCheckDialogs`: **VERIFICADO** (0 fallos, exit 0).
- **Mutaciones**: `toolbox-que-no-sigue-el-tema` y `toggle-que-no-persiste` **MUERDEN** (1/1).
- **Suite de pruebas (`FileFlow.slnx`)**: guardias unitarias (`UnoToolboxPanelGuardTests`, `WorkflowDiagnosisTests`, etc.) 100% en verde.

## [2026-09-30] - Hito 291: Los Estilos Compartidos del Host Uno en su Propio Diccionario (y el VisualStateManager que estaba mudo)

### 🎯 El encargo
«Extrae los estilos y plantillas de control compartidos del host Uno a su propio diccionario de recursos, para que cada plantilla tenga su espacio de nombres de estados.»

### 🧱 El arreglo
- **El diccionario nuevo** (`FileFlow.App.Uno/Themes/ControlStyles.xaml`): los estilos y plantillas compartidos salen de sus antiguos dueños —`CategoryChipStyle` del panel del catálogo y `InspectorTabRadioButtonStyle` de `App.xaml`— a un `ResourceDictionary` propio con los pinceles por `StaticResource` (nunca `ThemeResource`, la lección del 233). Se fusiona en `App.xaml` (`ResourceDictionary Source="ms-appx:///Themes/ControlStyles.xaml"`), con lo que el chip queda disponible para su panel y la pestaña del inspector para su `Application.Current.Resources`.
- **El espacio de nombres vuelve a cada página**: al vivir el chip en otro archivo, su `VisualStateGroup CommonStates` deja de compartir namescope con el panel. El encabezado del catálogo recupera así su propia máquina de estados: el realce del puntero pasa a ser **declarativo** (`PointerOver`/`Pressed`/`Disabled`/`Checked*` que pintan `HeaderRoot.Background`), y los handlers `OnGroupHeaderPointerEntered/Exited` del code-behind **se retiran** — la razón por la que el 290 tuvo que escribirlos (el choque de nombres) ha dejado de existir.

### 🔬 El hallazgo: los estados del chip estaban MUERTOS (y nadie lo sabía)
- Al medir el estado seleccionado del chip sobre la app viva (el chip «Todas» estaba `On` según UIA), **no pintaba el acento**: sólo el color de reposo. Lo mismo con el encabezado: el realce del puntero no aparecía pese a que el code-behind del 290 funcionaba (los ítems sí se realzaban).
- **La causa**: en el 290 el bloque `VisualStateManager.VisualStateGroups` se declaró como **hermano del elemento raíz** dentro del `ControlTemplate`. Esa forma **compila** (por eso nadie la vio) pero en Uno los grupos de estados tienen que colgar **dentro del primer hijo de la raíz** de la plantilla para APLICARSE: los estados hermanos del raíz quedan declarados y mudos. Es decir, el «chip seleccionado ya no nace claro sobre claro» del 290 era una cura **no aplicada**.
- **La cura**: mover el bloque **dentro del raíz** (`Border x:Name="ChipRoot"` y `HeaderRoot`), como primer hijo. Medido tras el arreglo sobre la app viva: el chip seleccionado pinta **`#6366F1`** (1.279 px), su hover **`#818CF8`** (1.280 px) y el encabezado realzado el pincel de tarjeta (9.515 px). Los estados del chip y del encabezado, por fin, se aplican.
- **La guardia nueva de esa regla**: `UnoVisualStateNameScanner.InspectTemplateStateGroupPlacement` parsea el XAML como XML (lo es: comprobado que los 20 del host son bien formados) y cuenta los bloques que cuelgan del `ControlTemplate` en vez de su raíz. El hecho `EveryVisualStateGroupInTheUnoHost_ShouldLiveInsideItsTemplateRoot` barre todo el host con su control («hay estados dentro de la raíz»), y dos auto-tests cubren la forma muda (hermano del raíz) y la forma aplicada (dentro del raíz).

### 🛡️ Guardias actualizadas
- `UnoVisualStateNameGuardTests`: el caso del chip se retargeta al **diccionario nuevo** (declara `CommonStates`+`Checked`) y se afirma que el **panel recupera su espacio de nombres** (declara su propio `CommonStates`+`PointerOver`); el barrido del árbol ya incluye `Themes/ControlStyles.xaml`.
- `UnoThemeRepaintGuardTests.HostXaml_ShouldConsumeTheThemeBrushesByStaticResource`: el diccionario nuevo entra en la lista de archivos que deben consumir los pinceles por `StaticResource`.
- **Mutación nueva `estados-fuera-de-la-raiz-de-la-plantilla`**: devuelve el bloque del encabezado a hermano del raíz (compila) → **MUERDE** en ~24,5 s, control verde, árbol restaurado por bytes y recompilado.

### 📊 Validación del estado
- **Host Uno (`FileFlow.Uno.slnx`)**: `dotnet build` con **0 errores**.
- **Suite completa (`dotnet test`)**: **1755 superadas, 1 omitida (ONNX local), 0 errores** (1756).
- **Sondas en runtime del host Uno**: `-SelfCheck` **113 `[OK]` · 0 `[FALLO]`** (exit 0, 81 ítems, 15 chips con caja, filtro 81→5, 20 bloques detallados con 0 visibles en compacto) y `-SelfCheckSettings` **18 `[OK]` · 0 `[FALLO]`** (exit 0, cambia y restaura tema e idioma).
- **Medición visual sobre la app viva** (UIA + captura): el chip seleccionado pinta **`#6366F1`**, su hover **`#818CF8`**, y el encabezado de categoría se realza a **`#161B22`** al pasar el puntero (antes: ningún cambio, por el bloque mudo).
- `mutations/COVERAGE.md` regenerado: **110 mutaciones declaradas · guardias con mutación 17/35 · subsistemas con mutación 14/16**.

## [2026-09-30] - Hito 290: El Catálogo de Nodos Sigue el Tema (y deja de leerse claro sobre claro)

### 🎯 El encargo
«el panel izquierdo, catálogo de nodos, al cambiar de tema no lo sigue y queda siempre claro con las letras también claras y no se pueden leer. la organización de las categorías y los nodos es visualmente muy deficiente, está todo desalineado y es poco atractivo. mejora este panel y asegura que siga el tema y siempre se puedan leer bien los textos.»

### 🔬 El diagnóstico
1. **El panel no seguía el tema, y la culpa era del hito 288**: aquel rediseño pidió los pinceles del catálogo (y del fondo del cajón y de la franja de estado en `MainWindow.xaml`) por `{ThemeResource …}`, creyendo que era «lo dinámico». Es exactamente al revés, y el propio `UnoThemeHost` lo tenía escrito desde el hito 233: el puente de temas **no republica claves** — cambia el COLOR del pincel singleton declarado en `App.xaml`, y `StaticResource` captura esa MISMA instancia, así que su mutación repinta a todos los consumidores vivos; un `ThemeResource` de aplicación **no re-evalúa** y el control queda congelado en los colores de arranque. Medido con grep: los 25 `{ThemeResource}` del host eran **22 del catálogo y 3 de la ventana** — el único rincón del host que aún los usaba. Ningún otro panel los tenía: por eso el resto de la app sí seguía el tema y este no.
2. **Las plantillas de los `ToggleButton` no eran nuestras**: la de fábrica de WinUI pinta los estados `Checked`/`PointerOver`/`Pressed` con los pinceles del tema del sistema e **ignora los `Setter` del estilo**, así que el chip seleccionado nacía claro sobre claro aunque el resto de los colores fueran los correctos.
3. **Dos `VisualStateGroup` en el mismo diccionario de recursos no compilan** (hallazgo del arreglo, no del encargo): con el chip y el encabezado declarando cada uno su grupo, el `XamlCompiler` sale con **código 1 sin mensaje** (el error no llega a MSBuild ni a `output.json`). Aislado por bisección: los dos bloques → falla; el del chip solo → compila; el del encabezado solo → compila.
4. **La alineación era accidental**: el encabezado de categoría (icono `20×20` con el chevron fuera de la plantilla, hueco 8) y la tarjeta de nodo (icono `28×28`, indentación `8`) arrancaban cada uno en una x distinta, así que el nombre de la categoría y el del nodo no compartían columna.

### 🧱 El arreglo
- **El tema primero (`NodeToolboxPanel.xaml` y `MainWindow.xaml`)**: los 25 `{ThemeResource}` pasan a `{StaticResource}` — el panel se alinea con el resto del host y el repintado en caliente vuelve a llegarle por la instancia del pincel. El cajón, además, pinta su propio fondo (`CanvasSurfaceBrush`) en su `Grid` raíz.
- **Plantilla propia del chip** (`CategoryChipStyle`): `ControlTemplate` con `Border` + `ContentPresenter` y un `VisualStateGroup` «CommonStates» que resuelve `Normal`, `PointerOver`, `Pressed`, `Disabled`, `Checked`, `CheckedPointerOver` y `CheckedPressed` con los pinceles `Canvas*` — el chip elegido va en acento primario con texto `CanvasOnAccentBrush`, legible en cualquier tema. Se le añade `MinWidth="0"`/`MinHeight="0"` y el `Margin` pasa al propio chip (la tira ya no necesita `Spacing`).
- **Encabezado del grupo**: `ControlTemplate` con el chevron dentro (dos `PathIcon` que se muestran por `Visibility` atados a `IsExpanded` con los conversores `BoolToVisibility`/`InverseBoolToVisibility` que el host ya tenía) y el contador en píldora alineado a la derecha; el realce del puntero va por code-behind precisamente porque **no cabe un segundo `VisualStateGroup` en este archivo** (hallazgo 3, documentado en el propio XAML y en el code-behind).
- **La alineación pasa a ser una sola cuenta**: encabezado (`padding 6` + chevron `10` + hueco `6`) y lista indentada (`margen 16` + `padding 6` + icono `24` + hueco `10`) dejan el icono en `x=22` y el nombre en `x=56` en las dos filas, con el mismo `ColumnSpacing=10`. La tarjeta de nodo estrena realce al pasar el puntero con el pincel del tema (no un literal).
- **La guardia del tema**: nuevo hecho en `UnoThemeRepaintGuardTests` (`HostXaml_ShouldConsumeTheThemeBrushesByStaticResource`) que cierra el catálogo: ningún `{ThemeResource` en el panel ni en la ventana, y consumo real de `{StaticResource Canvas…}` (la aserción de presencia es el control de un mutante que borrara los consumidores).
- **La guardia de los nombres de estado** (la mina que descubrió el arreglo): antes de escribirla se midió la regla **a golpe de compilación**, porque el compilador no la dice. Cinco builds, cada uno moviendo una sola pieza:

  | Montaje | Resultado |
  | :--- | :--- |
  | Dos plantillas con su grupo `CommonStates` en el mismo `<UserControl.Resources>` | **falla** (código 1, sin mensaje) |
  | Las mismas dos con el grupo renombrado a `GroupHeaderStates` **y** los estados renombrados | **compila** |
  | Grupos con nombres distintos pero un `<VisualState x:Name="Normal" />` repetido en los dos | **falla** |
  | Un `x:Name` de `Border`/`Grid` repetido entre dos plantillas del mismo diccionario (y entre contenido y recursos) | **compila** |
  | Un bloque de estados en el CONTENIDO con el `CommonStates` de una plantilla de los recursos | **falla** |

  La regla real no es «un grupo por diccionario» (dos caben si se llaman distinto) sino que **los nombres de `VisualStateGroup` y `VisualState` son únicos en TODO el archivo**, recursos y contenido incluidos. Y renombrarlos no es una salida en la práctica: WinUI pide sus estados **por nombre** (`GoToState("Checked")`), así que un XAML sólo admite una máquina de estados convencional — la del chip. Eso convierte el code-behind del encabezado en la única salida, no en una preferencia.
  - **El escáner** (`FileFlow.Tests/TestHelpers/UnoVisualStateNameScanner.cs`): censa los nombres declarados con las líneas de cada uno (el compilador no las imprime: la guardia las pone) y reporta la repetición citando los dos usos. No cuenta los nombres repetidos de elementos que no son estados —eso compila y prohibirlo sería un falso positivo— ni los citados en comentarios XML.
  - **La guardia** (`UnoVisualStateNameGuardTests`, 11 casos): barre todo el XAML del host; con el defecto plantado a mano en el árbol real el hecho **se pone rojo**; y un control comprueba que la guardia **miró** (el host declara estados: sin eso, un patrón roto también daría verde). Seis auto-tests con snippets sintéticos cubren cada forma medida y cada no-defecto.
  - **Sin mutación, y por qué**: el andamiaje declara defectos que **compilan** (un `NO-COMPILA` es un veredicto de fallo), y este no compila. La guardia es aquí toda la red; su testigo es la tabla de arriba.

### 📊 Validación del estado
- **Host Uno (`FileFlow.Uno.slnx`)**: compila con **0 errores**.
- **Suite completa (`dotnet test`)**: **1752 superadas, 1 omitida (ONNX local), 0 errores** (1753), en **tres corridas consecutivas**.
- **Un hallazgo lateral que costó cuatro corridas rojas**: con la guardia nueva el suite empezó a fallar **siempre, pero en una prueba distinta cada vez** (el reloj de carpeta, la ejecución de un flujo sin nodos, la diagnosis del grafo vacío, el latido de rendimiento) — la firma que el propio contrato de colecciones señala como «fallo en la prueba equivocada». El sospechoso no era un estado global: era **mi enumeración**, que recorría `FileFlow.App.Uno` entero **incluyendo `bin`/`obj`** —el runtime de WinUI, miles de ficheros— para luego filtrarlos; ese barrido gratuito competía por el disco, en paralelo, con las pruebas de temporización del suite. Podando los directorios de compilación **al recorrer** (en vez de filtrar la lista ya enumerada) el suite volvió a verde en tres corridas seguidas. La poda vive en `UnoGeometryBindingScanner.XamlFilesUnder`, así que la aprovechan también las guardias de geometría que ya existían.
- **Medición visual sobre la app viva** (captura del panel del cajón, 2880×1544): fondo `#131720` (`CanvasSurfaceBrush` del tema activo) y textos candidatos con contraste **12,0:1** (`#CED4DA`), **6,0:1** (`#818CF8`) y **5,0:1** (`#7F8891`) — los tres por encima del **4,5:1** de AA para texto normal. El fondo oscuro con texto claro confirma que el panel ya no queda claro sobre claro.
- **Sondas en runtime del host Uno**:
  - `-SelfCheck`: **VERIFICADO** — **113 `[OK]` · 0 `[FALLO]`**, exit 0. El catálogo mide **81 ítems**, **15 chips con caja dentro del cajón**, filtro `81 -> 5` con «Folder», y **20 bloques detallados materializados con 0 visibles** en compacto.
  - `-SelfCheckSettings` (la que cambia tema e idioma): **VERIFICADO** — **18 `[OK]` · 0 `[FALLO]`**, exit 0; el tema elegido (`light_studio`) llega al token del host (`#FFF8FAFC`, antes `#FF10131B`) y se restaura.
- **Mutación nueva `toolbox-que-no-sigue-el-tema`**: **MUERDE** (el fondo del panel vuelve a `ThemeResource` → la guardia cae; el control queda verde), con el árbol restaurado por bytes y recompilado. `mutations/COVERAGE.md` regenerado: **109 mutaciones declaradas · guardias sin mutación 19/35** (la de los nombres de estado entra como guardia sin mutación, y no por olvido: el defecto que vigila no compila).

## [2026-09-30] - Hito 289: Reclamación de Espacio del Lienzo al Cerrar Inspector y Pestañas Reales sin Glifo Circular

### 🎯 El encargo
«el panel lateral inspector al cerrarlo el camvas donde se pintan los flujos no recupera ese espacio. Los selectores para elejir las paginas que estan en la parte superior del panel ahora son radio botones y deberian ser pestañas.»

### 🔬 El diagnóstico
1. **Reclamación de espacio en el lienzo**:
   - En `MainWindow.xaml`, `InspectorColumn` declaraba `MinWidth="220"`. Al plegarse el inspector (`ApplyInspectorVisibility(false)`), se fijaba `InspectorColumn.Width = 0`, pero `MinWidth="220"` seguía activo. En el sistema de layout de WinUI, `MinWidth` tiene precedencia y restringe el ancho efectivo de la columna a `Math.Max(220, 0) = 220px`. Por consiguiente, la columna colapsada seguía reservando un hueco vacío de 220px y la columna del lienzo (`CanvasColumn`, con `Width="*"`) no podía expandirse hasta el extremo derecho de la ventana.
   - Además, la anchura antes del repliegue sólo se capturaba condicionalmente en `isOpen == true`, perdiendo el ancho real redimensionado por el usuario al cerrarse.
2. **Selectores de páginas del inspector**:
   - En `NodeInspectorPanel.xaml.cs`, las secciones («Parámetros», «Snapshots», «Entradas», «Salidas», «Diff», «Telemetría») utilizaban controles `RadioButton` estándar. Aunque se envolvieron en un contenedor con borde y estilo de píldora, la plantilla por defecto (`ControlTemplate`) de WinUI 3 incluye obligatoriamente un glifo circular de selección de radio button (`RootEllipse` / `CheckGlyph`) a la izquierda del texto. Visualmente se presentaban como botones de opción circulares en lugar de pestañas auténticas.

### 🧱 El arreglo
- **Reclamación de espacio del lienzo (`MainWindow.xaml.cs`)**:
  - `ApplyInspectorVisibility` actualizado para alternar dinámicamente `MinWidth`:
    - Al cerrar (`isOpen == false`): guarda el ancho del usuario (`InspectorColumn.ActualWidth`), colapsa el inspector y el splitter, fija `InspectorColumn.MinWidth = 0` y `Width = 0px`. La columna pasa a 0 px y el lienzo (`CanvasColumn`, `Width="*"`) recupera instantáneamente los 300+ px.
    - Al abrir (`isOpen == true`): restaura `InspectorColumn.MinWidth = 220`, `MaxWidth = 750` y restituye el ancho previo que el usuario tenía fijado (`Math.Clamp(_inspectorWidthBeforeCollapse, 220, 750)`).
- **Transformación de Selectores en Pestañas Auténticas (`App.xaml` y `NodeInspectorPanel.xaml.cs`)**:
  - Declarado `InspectorTabRadioButtonStyle` en `App.xaml` y factoría fallback `GetTabButtonTemplate()` en `NodeInspectorPanel.xaml.cs` sustituyendo la plantilla de `RadioButton` por un `Border` con `ContentPresenter` limpio, eliminando por completo el glifo circular.
  - Conmutador con aspecto de pestaña / píldora de alta fidelidad: contenedor `tabContainer` con fondo `CanvasBgDarkBrush`, borde `1px CanvasBorderBrush`, esquinas redondeadas `8px` y `Padding="3"`.
  - Pestaña activa resaltada con fondo `CanvasCardBrush`, borde sutil `CanvasBorderBrush`, texto en acento primario (`CanvasAccentPrimaryBrush`), tipografía `SemiBold` y esquinas redondeadas `6px`.
  - Pestañas inactivas con fondo transparente, borde transparente de 1px (evita saltos subpixel al alternar), texto secundario y feedback sutil al pasar el puntero (`PointerEntered` / `PointerExited`).
- **Sonda en Runtime (`SelfCheckFrame.cs`)**:
  - Añadida comprobación de medición que cierra el inspector, verifica que `InspectorColumn.ActualWidth < 0.51` y que `CanvasColumn.ActualWidth` se expande recuperando los 305px (`1040 -> 1345 (+305px)`), y que al reabrirse recupera con exactitud los 300px previos.

### 📊 Validación del estado
- **Host Uno (`FileFlow.Uno.slnx`)**: Compila limpio con 0 errores.
- **Sondeo en runtime del Host Uno (`.\run-uno-fast.ps1`)**:
  - `-SelfCheck`: **VERIFICADO** (0 fallos, salida con código 0; sondeo del marco confirma `lienzo 1040 -> 1345 (+305px)`).
  - `-SelfCheckControlBar`: **VERIFICADO** (código 0).
  - `-SelfCheckSettings`: **VERIFICADO** (código 0).
  - `-SelfCheckDialogs`: **VERIFICADO** (código 0).
- **Pruebas de guardias Uno (`FileFlow.Tests`)**: **145/145 superadas, 0 fallos** (100%).
- **Pruebas de mutaciones (`MutationDeclarationGuardTests`)**: **16/16 superadas, 0 fallos** (100%).

## [2026-09-30] - Hito 288: Rediseño Visual y Ergonómico de Paneles Laterales (Inspector y Catálogo de Nodos)

### 🎯 El encargo
«Mejora el aspecto de los paneles laterales: inspector y catálogo de nodos que deberían tener pestañas y no como tienen ahora como botones sueltos; mejora los márgenes que todo está muy pegado a los bordes y la organización de los formularios que se ven desordenados y sin forma; en el catálogo de nodos el icono y el nombre del nodo o categoría a menudo está desalineado; el catálogo de nodos no sigue los colores del tema quedando muchas veces las letras ilegibles. Analiza todo esto y crea un plan detallado para mejorar la interface de usuario de forma que sea más atractiva y ordenada.»

### 🔬 El diagnóstico
1. **Catálogo de Nodos (`NodeToolboxPanel`)**:
   - Cabeceras de categorías y chips tipo píldora tenían fondos y textos sin adaptación dinámica al tema activo (`ThemeResource`), generando ilegibilidad por falta de contraste en temas claros/oscuros.
   - En la lista de nodos, los iconos vectoriales `PathIcon` flotaban en un `StackPanel` horizontal sin contenedor delimitado, produciendo desalineación vertical y descentrado respecto al nombre, rol y descripción.
   - Cabeceras de acordeón de categorías con icono y contador desalineados.
2. **Inspector de Nodos (`NodeInspectorPanel`)**:
   - Conmutador de secciones implementado con botones `RadioButton` sueltos en fila, sin apariencia de pestaña o control segmentado moderno.
   - Márgenes y paddings muy estrechos (`12,10,10,10`), provocando sensación de ahogo contra el borde de la ventana y el splitter.
   - Los parámetros del nodo se renderizaban planos en un `StackPanel` sin tarjetas contenedoras ni jerarquía visual.
   - Botones de acción («…», «✎», «{x}», «🎬», «🔑») con esquinas rectas y sin estilo armonizado.

### 🧱 El arreglo
- **Catálogo de Nodos (`NodeToolboxPanel.xaml`)**:
  - Chips de categoría rediseñados como píldoras (`CornerRadius="12"`) con `ThemeResource` dinámico (`CanvasCardBrush`, `CanvasSecondaryBrush`, `CanvasBorderBrush`).
  - Cabeceras de categorías de acordeón con icono en contenedor cuadrado de `20x20px` (`CornerRadius="4"`) y badge redondeado para el contador (`CornerRadius="8"`, padding `6,1`).
  - Tarjetas de nodo con contenedor cuadrado redondeado de `28x28px` (`CornerRadius="6"`) para el icono, alineado vertical y horizontalmente al centro, eliminando cualquier descuadre con el texto.
  - Tipografía refinada: nombres de nodo con peso `Medium`, badges de rol en `CanvasAccentGlowBrush` (`SemiBold`) y descripciones con opacidad cuidada.
- **Inspector de Nodos (`NodeInspectorPanel.xaml.cs`)**:
  - Tira de pestañas envuelta en un contenedor segmentado `tabContainer` con fondo `CanvasBgDarkBrush`, `CornerRadius="8"`, padding `3px` y margen inferior espaciado.
  - Implementado `UpdateTabButtonStyle(RadioButton, bool)` que estiliza la pestaña activa con pastilla elevada (`CanvasCardBrush`, borde sutil y texto acentuado) y las inactivas transparentes.
  - Cabecera rediseñada con mayor jerarquía (`FontSize="14"`, `FontWeight="Bold"`), botón `Probar` primario (`CornerRadius="6"`, fondo acento) y botón `Cerrar` con borde sutil.
  - Parámetros organizados en tarjetas individuales (`Border` con `CanvasSurfaceBrush`, `CornerRadius="6"`, `CanvasBorderBrush` y padding `10,8`).
  - Cajas de texto y botones de acción («…», «✎», «{x}», «🎬», «🔑») con `CornerRadius="4"` homogéneo y bordes sutiles.
  - Padding general del panel ampliado a `16,14,16,14` para dar respiración al panel.
- **Ventana Principal (`MainWindow.xaml`)**:
  - Sustituidos estilos estáticos y colores hexadecimales hardcodeados (`#161B22`, `#F0F6FC`) en Toolbox y StatusBar por tokens dinámicos (`{ThemeResource CanvasSurfaceBrush}`, `{ThemeResource CanvasBgDarkBrush}`, `{ThemeResource CanvasTextBrush}`).

### 📊 Validación del estado
- **Host Uno (`FileFlow.Uno.slnx`)**: Compila limpio con 0 errores.
- **Suite de pruebas (`FileFlow.Tests`)**: **1.740 pruebas superadas, 0 fallos, 1 omitida** (100% de éxito).
- **Sondeos en runtime del Host Uno**:
  - `.\run-uno-fast.ps1 -SelfCheck`: **VERIFICADO** (83 comprobaciones `[OK]`, lienzo y paneles).
  - `.\run-uno-fast.ps1 -SelfCheckControlBar`: **VERIFICADO** (ciclo del motor y menú principal).
  - `.\run-uno-fast.ps1 -SelfCheckSettings`: **VERIFICADO** (superficie de ajustes y temas).
  - `.\run-uno-fast.ps1 -SelfCheckDialogs`: **VERIFICADO** (diálogos modales y paneles de nodo).

## [2026-09-29] - Hito 287: Supr se Lleva la Selección Entera — Nodos y Cables — de Una Sola Vez

### 🎯 El encargo
«Unifica el borrado: cuando la selección del lienzo tiene nodos y cables a la vez, Supr debe llevarse las dos cosas en una sola operación de deshacer, en vez de borrar sólo los cables marcados.»

### 🔬 El diagnóstico
El `case Delete` de la tabla compartida era un `if`/`else` **excluyente**: si había un cable marcado se ejecutaba `DeleteSelectedConnections` —una transacción con una acción por cable— y **los nodos elegidos se quedaban en el grafo**; si no había cables, los nodos. Con el rectángulo del hito 286, que marca las dos cosas de una vez, eso obligaba a borrar en **dos tandas** y a deshacer otras dos, y el usuario que rodeaba dos tarjetas con su cable se quedaba mirando cómo desaparecía el cable y no los nodos.

### 🧱 El arreglo
- **`EditorViewModel.DeleteSelection(parameter)`**: los nodos elegidos y los cables marcados caen **dentro de una transacción** («Eliminar Selección»), así que **un solo Ctrl+Z** devuelve la selección entera. Los cables que caen por sus nodos **no se borran dos veces**: primero caen los nodos con todo lo que cuelga de ellos —una sola `DeleteNodesAction`— y sólo se borran a mano los marcados que **sigan** en el grafo.
- **Una sola copia de la baja de un nodo**: la que era el cuerpo de `DeleteSelectedNodes` pasa a `DeleteNodesWithTheirConnections`, compartida por el borrado de nodos y el de la selección entera.
- **Una sola orden para los dos hosts**: la tabla compartida apunta `Delete` a `DeleteSelectionCommand` y la `KeyBinding` del **escritorio** apunta a la misma orden (allí `SelectedConnections` está siempre vacía, así que su conducta no cambia: lo que cambia es que ya no hay una copia del criterio en cada host).
- **Un defecto latente que salió al medir el ciclo completo**: el undo del borrado **reinserta** el nodo, y los dos avisos de un nodo —la marca de selección (que asigna el nodo de referencia y lo trae al frente) y el recuento— se suscribían en las puertas de **alta** (`AddNode` y el importador), no en la **única puerta por la que un nodo entra en el grafo**: el nodo que volvía del deshacer se elegía y **el núcleo no se enteraba**. Los dos avisos pasan a la suscripción de `Nodes.CollectionChanged` —la cubren el nuevo, el importado, el pegado y el restaurado— y las dos copias de las puertas de alta se quitan.

### ✅ La medida (ratón y Supr inyectados + el ciclo de deshacer, 8 fases)
Contada del árbol (tarjetas y cables, que es lo que el usuario ve), sobre el grafo de ejemplo de 3 nodos y 2 cables:

| Fase | Gesto | Medida |
| :--- | :--- | :--- |
| P0 | arranque | **3 tarjetas / 2 cables** |
| P1 | clic en la tarjeta **C** | 3/2 (C elegida) |
| P2 | **Ctrl**+clic en el cable que **NO** cuelga de C | 3/2: la selección tiene las dos cosas de tipos distintos |
| P3 | **Supr** | **2 / 0**: cae el nodo C con su cable **y** el cable marcado. Con el criterio viejo aquí quedaban 3/1 (sólo el cable) |
| P4 | **un solo** Ctrl+Z | **3 / 2** |
| P5 | rectángulo sobre la tarjeta **A** (A y su cable marcado) | 3/2 |
| P6 | **Supr** | **2 / 1**: A con su cable, **sin borrarlo dos veces** (queda el otro cable) |
| P7 | **un solo** Ctrl+Z | **3 / 2** |

### 🧪 El aparato que lo guarda
- **Tres casos nuevos en `EditorSelectionRuleTests`** (nueve en el fichero): la selección mixta cae entera y **un solo deshacer** la devuelve; el cable **que no cuelga** del nodo elegido también cae; y el nodo que vuelve del deshacer **sigue gobernado** por el núcleo (la mitad que le faltaba a la suscripción).
- **Guardia del cable puesta al día**: su cita de la tabla compartida era la rama borrada (`if (editor.SelectedConnections.Count > 0)`) y ahora cita la orden única (`DeleteSelectionCommand`), con el porqué.
- **Mutación nueva `borrado-que-deja-los-nodos` MUERDE** (30,8 s): devuelve la condición vieja (los nodos sólo caen si no hay cables marcados), con el borrado de sólo cables como control. `COVERAGE.md` → **113 · 15/17 · 20/51**.
- **Sonda `ProbeMixedDeletion`** (renglón nuevo del lienzo): construye la selección mixta que deja el rectángulo, corre el Supr por la tabla del núcleo y mide el ida y vuelta —«nodos 3->2->3, cables 2->1->2»—.

### 📊 Validación del estado
Lienzo **111 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 110; +1 renglón), paneles **59**, barra **42**, ajustes **18** (las cuatro EXIT 0) y la del **escritorio** **41 / 0** EXIT 0; suite completa **1987 superadas + 1 omitida de 1988, 0 errores**; las dos soluciones **0 errores**.

### 🟠 Fronteras declaradas
- **Cierra la frontera del hito 285** («Supr borra una cosa: con marca es el cable, sin marca los nodos»): ahora borra LA SELECCIÓN, que puede ser las dos cosas. La entrada vieja no se reescribe.
- **La acción del nodo no cambia**: el borrado que ofrece la propia tarjeta sigue siendo «este nodo» (`DeleteSelectedNodesCommand`), no «la selección».
- **El escritorio no cambia de conducta** (nunca llena la marca de cables), pero pasa a compartir la orden en vez de tener su propia copia del criterio.
- **El rehacer del ciclo mixto no se midió** con dedos (el undo sí); y Supr sin nada elegido sigue sin hacer nada.

## [2026-09-29] - Hito 286: el Rectángulo de Selección Marca También los Cables (y el Plano Ya No lo Desplaza)

### 🎯 El encargo
«Haz que el rectángulo de selección capture también los cables que quedan dentro y que Ctrl+rectángulo AÑADA o reste marca a la selección de nodos, con su sonda, su guardia y su mutación.» Las dos respuestas de alcance del usuario: **Ctrl AÑADE y NO resta** (una sola regla, la del hito 285: la selección no se quita arrastrando) y **un cable entra cuando sus DOS anclas caen dentro** (no cuando su curva roza el borde del rectángulo).

### 🔬 El diagnóstico (y el defecto latente que salió al camino)
El rectángulo sólo miraba tarjetas: recorría `_containers` y encendía `card.Node.IsSelected`, **sin una línea sobre los cables**, así que pasar el lazo por encima de dos nodos dejaba el cable que los une sin marcar y el Supr no se lo llevaba (había que pulsar los cables uno a uno). Y al ir a añadir la mitad de los cables salió **el otro defecto, ya declarado como frontera**: el rectángulo comparaba los puntos del puntero —espacio de la RAÍZ— contra `Canvas.GetLeft/Top` de las tarjetas —espacio del GRAFO— escalando **sólo el ancho** (`card.Width * CanvasTransform.ScaleX`) y no la posición: la comparación sólo acertaba con el plano sin mover, que es palabra por palabra lo que las notas de versión dejaron escrito («el rectángulo de selección cuando el lienzo está desplazado (con el lienzo centrado acierta)»).

### 🧱 El arreglo (la política en el núcleo, el gesto en la vista)
- **`EditorViewModel.ApplyRubberSelection(nodesInside, connectionsInside, add, baseNodes, baseConnections)`**: marca **a la vez** nodos y cables —sin `add` **reemplaza** (manda el área) y con `add` **AÑADE** lo que ya estaba elegido—, con el nodo de referencia en el último elegido del grafo. La marca de los cables se toca **sólo donde cambia**: cada alta o baja de la colección repinta la capa de cables entera y el arrastre recalcula por cada movimiento del puntero.
- **El gesto, en sus tres tiempos** (`BeginRubberBand` / `UpdateRubberBand` / `EndRubberBand`, como el gesto del puerto): el modificador se lee **al pulsar** —como en el clic— y decide **qué quedó dentro**: una tarjeta entra por su **centro** y un cable con sus **DOS anclas** dentro (`MeasureWireAnchors` las mide **una vez** al arrancar; sin las dos medidas, ese cable no entra, porque el rectángulo no adivina).
- **Y el rectángulo se decide en el GRAFO**: los dos puntos del gesto pasan por el mismo inverso que todo lo demás (`GraphPointFromScreen`) y las cajas se comparan **sin escalar la posición**; el rectángulo *dibujado* sigue en la raíz, que es donde está el puntero y donde la capa del lazo vive.

### ✅ La medida (ratón y Ctrl inyectados, 11 fases verdes)
Medida por **anillo** (píxeles del acento alrededor del título de cada tarjeta) y por las **ventanas entre tarjetas** (por donde pasa cada cable, sin los anillos):

| Fase | Gesto | Medida |
| :--- | :--- | :--- |
| P0 | arranque | anillos A=B=C=0 · huecos **cable 39/36, acento 0** |
| P1 | rectángulo SIN Ctrl sobre TODO | **los tres anillos** (1576/1895/1698) y los dos huecos **cable 13/12, acento 65/60**: **nodos y cables a la vez** |
| P2 | **Ctrl**+rectángulo lejos de todo | **idéntico**: no suelta nada de lo elegido |
| P3 | rectángulo lejos SIN Ctrl | vuelve a P0: manda el área (vacía) |
| P4 | clic en la tarjeta A | anillo de A (1380) y **huecos intactos**: el clic NO marca el cable (contraste con P1) |
| P5 | rectángulo SIN Ctrl sobre C | A suelta, C elegida (1464) → reemplaza |
| P6 | **Ctrl**+rectángulo sobre B | B y C (1465/1464) → añade |
| P7 | **tras PAN**, clic en el vacío | todo suelto |
| P8 | rectángulo **tras el PAN** sobre TODO | los tres anillos **en su sitio nuevo** y los dos cables marcados (acento 78/72) |
| P9 | **tras ZOOM**, clic en el vacío | todo suelto (una tarjeta sale por la derecha: la tercera no se puede encerrar) |
| P10 | rectángulo **tras el ZOOM** sobre lo visible | las tarjetas que quedan, elegidas, y el cable entre ellas marcado (**cable 244 → acento 746**) |

P7-P10 son la prueba de la mitad que el hito 285 dejó declarada: **con el plano movido y con zoom el rectángulo sigue acertando**.

### 🧪 El aparato que lo guarda
- **`EditorSelectionRuleTests`**: dos casos nuevos (seis en total) miden la política en el núcleo —«el rectángulo encierra dos nodos y el cable que los une: las dos cosas quedan elegidas» y «con Ctrl se añade sin soltar lo de fuera; sin Ctrl manda el área»—.
- **Guardia del cable** (`UnoCanvasWireGuardTests`), sección nueva: cita la llamada al núcleo, la base de Ctrl de los **cables**, la medida **única** de las anclas, que un cable necesita **sus dos** anclas dentro, el modificador leído al pulsar y el rectángulo decidido **en el grafo** (`GraphPointFromScreen`) con el centro de la tarjeta comparado sin escalar. La misma guardia cantó el cambio de uso de la medida del centro (**7 → 8**, con el motivo escrito: la caja del rectángulo).
- **Dos mutaciones nuevas, las dos MUERDEN**: `rectangulo-que-no-ve-los-cables` (31,5 s) —el conjunto de cables que entran se vacía— y `rectangulo-con-ctrl-que-reemplaza` (30 s) —el Ctrl deja de conservar lo de fuera—. `COVERAGE.md` → **112 · 15/17 · 20/51**.
- **Sonda `ProbeRubberBand`** (dos renglones nuevos en el lienzo): corre los tres tiempos del gesto con puntos de la raíz sobre un rectángulo medido del árbol, y comprueba de una vez que encierra nodos **y** cables, que con Ctrl no suelta lo de fuera y que sin Ctrl lo suelta todo.
- **La guardia de geometría cazó a la sonda**: `TheCanvasCodeBehind_ShouldPositionThroughTheProjection_NotThroughRawLocationReads` marcó el punto construido desde `.X/.Y` del lazo de prueba; el arreglo no fue esconderlo sino **declarar el espacio** (los puntos del gesto son de la **raíz**, el cruce al grafo lo hace el rectángulo por dentro) y no construirlos desde coordenadas de otro espacio.

### 📊 Validación del estado
Lienzo **110 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 108; +2 renglones), paneles **59**, barra **42**, ajustes **18** (las cuatro EXIT 0) y la del **escritorio** **41 / 0** EXIT 0; suite completa **1984 superadas + 1 omitida de 1985, 0 errores**; las dos soluciones **0 errores**.

### 🟠 Fronteras declaradas
- **Ctrl AÑADE y no resta** (decidido con el usuario): un rectángulo nunca quita lo que estaba elegido, ni con modificadores; para soltarlo todo está el clic en el vacío.
- **Un cable entra por sus ANCLAS, no por su curva** (decidido con el usuario): un rectángulo que sólo cubre el tramo entre dos sockets marca el cable aunque ninguna de las dos tarjetas caiga dentro.
- **La frontera del rectángulo desplazado queda CERRADA aquí**, y se saldará en `docs/notas_de_version.md` al cerrar el tramo: una nota de una versión publicada no se reescribe, así que la frase vieja («con el lienzo centrado acierta») sigue ahí con esta entrada como referencia.
- **El escritorio no se toca**: su rectángulo es de Nodify y no comparte este método.
- **El rectángulo no marca cables por las anclas estimadas**: si el árbol no materializó los sockets, ese cable no entra (la estimación es el respaldo del *dibujo*, no de una decisión de selección).

## [2026-09-29] - Hito 285: Pulsar REEMPLAZA la Selección (y Ctrl AÑADE) — en las Tarjetas y en los Cables

### 🎯 El encargo
«Haz que al seleccionar con el botón izquierdo sólo se seleccione un elemento, es decir que se deseleccionen los demás, excepto pulsando la tecla Control que se añadirían las selecciones.» Las dos respuestas de alcance del usuario quedan dentro: **el Ctrl vale también para los CABLES** (Ctrl+clic añade cables a la marca, y **Supr borra todos los marcados de una vez con un solo deshacer**) y **el rectángulo de selección cumple la misma regla** (sin Ctrl reemplaza, con Ctrl añade), igual que el clic.

### 🔬 El diagnóstico (leído antes de tocar)
El clic en una tarjeta hacía `SelectConnection(null)` y luego `node.IsSelected = true`: **marcaba sin soltar**, así que la selección se acumulaba y el Supr se llevaba de más —el síntoma que reportó el usuario—. El rectángulo de selección (`UpdateRubberSelection`) **nunca deseleccionaba**: sólo sumaba, así que un rectángulo sobre una zona vacía dejaba la selección de antes intacta. Y la marca del cable era **un campo suelto** (`EditorViewModel.SelectedConnection`), sin sitio donde meter un conjunto.

### 🧱 El arreglo (la regla, en el núcleo y en un solo sitio)
- **`EditorViewModel`**: `SelectNode(node, add)` y `SelectConnection(connection, add)` —sin `add` **sueltan** lo anterior, nodos **y** cables (la selección del lienzo es UNA: elegir un cable suelta los nodos y al revés), con `add` **añaden**—; `ClearSelection()` (el clic en el vacío) y `DeselectAllNodes()`; la marca del cable pasa de campo a **colección** `SelectedConnections`, con la invariante de que el cable que **sale** del grafo suelta su marca (en `Connections.CollectionChanged`); y `DeleteSelectedConnections` (**nuevo**) borra el conjunto dentro de **una transacción** de `UndoRedoService`, que es lo que hace que **un solo Ctrl+Z** los devuelva.
- **`EditorKeyboardShortcuts` (tabla compartida)**: `case Delete` → si hay cables marcados son **los cables**; si no, los nodos (la conducta del escritorio).
- **Host Uno**: el clic de tarjeta llama `SelectNode(card.Node, add: IsKeyDown(VirtualKey.Control))`; `OnWirePressed` llama `SelectConnection(connection, add: IsKeyDown(...Control))`; el rectángulo decide **al pulsar** (`_rubberAdditive` + `_rubberBaseNodes`, con `ClearSelection()` de arranque si no hay Ctrl); el clic en el vacío llama `ClearSelection()`; y el resalte se repinta por la suscripción a **`SelectedConnections.CollectionChanged`** (el campo suelto no podía avisar de que la marca del vecino había cambiado). Que el cable marcado **se vea** se conserva: el trazo pasa al acento y engorda.

### ✅ La medida (ratón y Ctrl inyectados sobre la app en marcha, 14 fases verdes)
Medida por **caja de tarjeta** (píxeles del acento `(236,72,153)` alrededor de cada título) y por **franja de cables** (color del cable `(244,114,182)` contra el acento):

| Fase | Gesto | Medida |
| :--- | :--- | :--- |
| P0 | arranque | A=0 B=0 C=0 · franja cable **369**, acento **0** |
| P1 | clic en la tarjeta **A** | **A=1385** B=0 C=0 (una sola) |
| P2 | clic en la tarjeta **B** | A=**0** **B=1243** C=0 → **reemplaza** |
| P3 | **Ctrl**+clic en **A** | **A=1385 B=1243** C=0 → **añade** |
| P4 | clic en el vacío | A=B=C=0 → suelta todo |
| P5 | clic en el **cable 1** | franja acento **305** y cable **369→257**: marca **uno** y suelta los nodos |
| P6 | clic en el **cable 2** (sin Ctrl) | marca **sigue siendo una** (cable 245, acento 310) → **reemplaza** |
| P7 | **Ctrl**+clic en el **cable 1** | franja cable **133**, acento **615** → **dos** marcados |
| P8 | **Supr** | franja cable **0**, acento **0** → **los dos de una vez** |
| P9 | **Ctrl+Z** | franja cable **369** → **un solo deshacer los devuelve** |
| P10 | clic en la tarjeta **A** | A=1385 |
| P11 | rectángulo SIN Ctrl sobre **C** | **A=0 C=1241** → **reemplaza** |
| P12 | **Ctrl**+rectángulo sobre **B** | **B=1243 C=1241** → **añade** |
| P13 | rectángulo SIN Ctrl sobre **B** | **B=1243 C=0** → **reemplaza** |
| P14 | clic en el vacío | estado limpio |

(Los píxeles de acento que aparecen en las cajas durante las fases del cable —105/140— son el **trazo grueso del cable marcado** cruzando esa caja, no un anillo de tarjeta: el anillo de una tarjeta mide ~1.240.)

### 🧪 El aparato que lo guarda
- **`EditorSelectionRuleTests` (nuevo, 4 casos, comportamiento del núcleo)**: pulsar reemplaza y Ctrl añade entre nodos; elegir un cable suelta los nodos y Ctrl suma a la marca; Supr borra los cables marcados y **una** transacción de undo los devuelve; y Supr no toca los no marcados. *(El `using Point = FileFlow.Sdk.Point;` es obligatorio dentro de `FileFlow.Tests.Unit.App`: el nombre `Sdk` resuelve al espacio de nombres de pruebas.)*
- **Guardia del cable puesta al día** (`UnoCanvasWireGuardTests`): citaba la rama borrada (`SelectedConnection` singular) y el `SelectConnection(connection)` sin modificador; ahora cita **la colección** (`SelectedConnections.Contains`), el `add: IsKeyDown(...Control)`, la **suscripción** del resalte y `ClearSelection()`, más las **mitades de la regla** en el fuente (el rectángulo que pregunta el modificador y el `SelectNode` de la tarjeta) y los **tres renglones nuevos** del sondeo.
- **Mutación nueva `seleccion-que-no-reemplaza`**: quita el bloque que suelta lo anterior en `SelectNode` (el clic vuelve a **acumular**), testigo `PulsarUnNodo_ShouldReplaceTheSelection_AndControlShouldAddToIt` y control el borrado en lote de los cables. **MUERDE** (33,5 s).
- **Mutaciones del sujeto, puestas al día y verificadas**: el fragmento de `cable-marcado-que-no-se-ve` citaba `ReferenceEquals` con el campo suelto (ahora pregunta a la colección) y el de `cables-que-no-llegan-tarde` incluía el cierre del bloque de suscripciones, que se movió al entrar la línea nueva. Ambas **MUERDEN**, y con ellas `cable-que-no-se-puede-pulsar`, `menu-que-sale-en-una-esquina` y `cable-con-anclas-estimadas`.
- **Sonido del lienzo**: +3 renglones (`ProbeSelectionRule`) —«pulsar reemplaza la selección», «y con Ctrl se AÑADE a lo elegido» y el borrado del conjunto con un solo deshacer—.

### 🐛 Un fallo del instrumento, no del producto
La sonda nueva lanzó `COMException [0x80004005]` al principio: el rastro (`WinRT.ExceptionHelpers` → `NodeCardViewModel.OnNodePropertyChanged`) mostró que el refresco del resalte pinta sobre el **árbol visual**, y el ciclo **borrar+deshacer** que corre antes reconstruye las tarjetas **sin pase de layout** (el sondeo es síncrono): la tarjeta recién creada no tiene contenedor y su refresco falla con `E_FAIL`. La sonda pasó a correr **antes** de ese ciclo, que es donde las tarjetas están materializadas —el estado del gesto real—. La app de verdad no lo sufre: entre dos gestos hay pases de layout.

### 📊 Validación del estado
Sonda del **lienzo** **108 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 105; +3 renglones), paneles **59**, barra **42**, ajustes **18** (las cuatro EXIT 0) y la del **escritorio** **41 / 0** EXIT 0; suite completa **1982 superadas + 1 omitida de 1983, 0 errores**; las dos soluciones **0 errores**; y `COVERAGE.md` regenerado: **110 · 15/17 · 20/51**.

### 🟠 Fronteras declaradas
- **No hay resta con Ctrl**: con Ctrl todo se AÑADE (el rectángulo con Ctrl no suelta lo que ya estaba elegido). Si el usuario quiere quitar uno, suelta todo con un clic en el vacío.
- **Supr borra una cosa**: si hay cables marcados, los cables (todos los marcados); si no, los nodos. No se borran nodos y cables en la misma pulsación.
- **El escritorio no se toca**: su lienzo (Avalonia/Nodify) nunca llena `SelectedConnections`, así que su Supr sigue borrando nodos y su multiselección sigue siendo la de su vista; la regla nueva vive en el núcleo y la consumen los hosts que la piden (`SelectNode`/`SelectConnection` con `add`).
- **No hay «seleccionar todo»** ni multiselección por lazo sobre los **cables** pasando por encima de una tarjeta, y el rectángulo no selecciona cables (sólo tarjetas), como antes.

## [2026-09-29] - Hito 284: el Menú del Cable Sale Bajo el Ratón (no en la esquina del cable)

### 🎯 El encargo
«El menú contextual al pulsar el botón derecho no sale alineado con el cursor del ratón sino en una esquina.»

### 🔬 El diagnóstico (medido antes de tocar nada)
El host Uno tiene **un solo** menú contextual: el del cable (`BuildWireMenu`), que se mostraba con **`ShowAt(hit)`** —anclado a la **DIANA**, la misma Bézier con trazo grueso que hace pulsable el cable—. Y ahí está el defecto: **el rectángulo de una curva que va de un socket al otro abarca el cable entero**, así que WinUI coloca el menú en una esquina de esa caja, no bajo el ratón. Medido con el ratón inyectado sobre la app en marcha: clic derecho en `(1442,760)` y menú en `(643,419)` — **855 px** de distancia. El escritorio no tiene ese problema porque allí el menú es un `ContextMenu` de Avalonia, y ese sale en el puntero: la posición explícita es **paridad**, no un adorno.

### 🧱 El arreglo (una pieza, en la mitad de los cables)
`ShowWireMenuAt(connection, e.GetPosition(RootGrid))` muestra el menú con **posición explícita** sobre la **raíz del lienzo**:
```csharp
flyout.ShowAt(RootGrid, new FlyoutShowOptions
{
    Position = canvasPoint,
    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
});
```
El punto se refiere a `RootGrid` —el **mismo** espacio en el que el lienzo lee el puntero para todo lo demás— y **no** al elemento pulsado: así el sitio del menú no depende de por dónde cayó el clic dentro de la curva.

### ✅ La medida (ratón inyectado, 4 fases verdes)
| Gesto | Medida |
| :--- | :--- |
| Clic derecho en el **cable 1** `(1151,792)` | menú en `(1158,799)` → desplazado **(7,7)** — antes: 855 px |
| Clic derecho en el **cable 2** `(1463,792)` | menú en `(1469,799)` → desplazado **(6,7)**: **sigue al puntero**, no es un desplazamiento fijo desde el cable |
| Su entrada, pulsada | **borra la conexión**: 179 → **117 píxeles** de cable |
| **Ctrl+Z** | **la devuelve**: 117 → **179 píxeles** |

Los ~7 px del borde son el marco del propio `MenuFlyoutPresenter` (la entrada queda 20 px dentro), que es como Windows coloca un menú de contexto.

### 🧪 El aparato que lo guarda
La conducta es de **puntero** y el host Uno no se materializa en el suite, así que se guarda como las demás de su clase: **censo de fuente** en el caso del cable (el punto del puntero, el `ShowAt` sobre la raíz con `FlyoutShowOptions`, su `Position`… y **prohibido `ShowAt(hit)`**, que es la firma del defecto) y **mutación nueva** [`menu-que-sale-en-una-esquina`](mutations/menu-que-sale-en-una-esquina.json), que devuelve el anclaje al elemento y hace caer esas líneas. La mutación se probó: **MUERDE** (28,5 s, testigo rojo y control verde).

### 🟠 Fronteras declaradas
- **La entrada 280 declaraba la frontera contraria** («el menú lo coloca WinUI… no se fija una posición propia»): queda **cerrada** por el informe del usuario. La entrada vieja **no se reescribe** (así se lee en esta bitácora): lo que cambió se dice aquí.
- **La sonda del lienzo no muestra el menú**: su renglón lee la **entrada** del menú construido, no sus píxeles, y mostrar un `Flyout` dentro del sondeo lo metería en el informe de las demás sondas. La posición se mide donde se puede —con el ratón inyectado, arriba—, y su censo de fuente y su mutación la atan.
- **`Escape` y el clic fuera siguen cerrando el menú** sin borrar nada (probado en el propio playtest: se abre y se cierra entre las dos medidas).
- **El escritorio no se toca**: su `ContextMenu` ya sale en el puntero.

### 📊 Validación del estado
Sonda del **lienzo** EXIT 0 con **105 `[OK]` · 0 `[FALLO]` · VERIFICADO** (sin cambios: el sitio del menú no es cosa suya), las otras tres **59 / 42 / 18** y la del **escritorio** **41 / 0**, todas EXIT 0; suite **1978 + 1 omitida de 1979, 0 errores**; las dos soluciones **0 errores**; `COVERAGE.md` regenerado por su guardia: **109 declaradas · 15 de 17 · 20 de 51**.

## [2026-09-29] - Hito 283 (verificación): Supr Sobre el Cable Marcado, Re-medido con Rehacer y Retroceso

### 🎯 El encargo
«Que el cable seleccionado se borre también con **Supr**, reutilizando la misma orden del núcleo, y medir con el ratón y el teclado inyectados que el **undo** lo restaura.»

### 🔬 Lo que se encontró: **ya estaba servido** (hito 281), y no se re-implementó nada
Leído en el camino real, no supuesto: el host mapea **`Delete` y `Retroceso`** a la misma clave física de la tabla compartida (`EditorCanvasControl.xaml.cs`, `MapKey`), y `EditorKeyboardShortcuts.Execute` resuelve `ShortcutKey.Delete` así — **si hay cable marcado, es el cable**:
```csharp
if (editor.SelectedConnection is { } connection)
{
    editor.DeleteConnection(connection);   // la orden del NÚCLEO, la misma que cumple el menú
    return true;
}

editor.DeleteSelectedNodesCommand.Execute(null);   // y si no, los nodos, como el escritorio
```
`EditorViewModel.DeleteConnection` es el **único** cuerpo del borrado de una conexión (lo comparten el menú del clic derecho —vía `ConnectionViewModel.DeleteCommand`, que la sonda compara por referencia— y este atajo), y es quien **registra `DeleteConnectionAction` en la pila del undo**. No hay copia en el host que pudiera divergir: el host sólo pide la orden. **Este pase no añade ni cambia una línea de producto**; añade la medida que faltaba.

### ✅ La medida (ratón y teclado inyectados sobre la app en marcha, 7 fases verdes)
El ejemplo trae **2 cables**; se marca el primero con el **clic izquierdo** (el trazo pasa al acento, `(236,72,153)`) y se recorre el resto **con el teclado**:
| Gesto | Medida |
| :--- | :--- |
| **Supr** | 2 → **1 cable** (el otro sigue ahí) y 179 → **117 píxeles** de cable: borra **el marcado**, no cualquiera |
| **Ctrl+Z** | 1 → **2 cables**, 117 → **179 píxeles**: el undo lo devuelve |
| **Ctrl+Y (rehacer)** | 2 → **1 cable**, 179 → **117 píxeles** |
| **Ctrl+Z** | vuelve a **2 cables / 179 píxeles** |
| Marcar + **Retroceso** | 2 → **1 cable**: la otra tecla de la MISMA clave de la tabla hace lo mismo |
| **Ctrl+Z** | **2 cables / 179 píxeles** |

El **rehacer** es la prueba de que el borrado está **en la pila del núcleo** y no es un quitado del host: un borrado que el host hiciera por su cuenta no se podría rehacer (y el undo del renglón anterior tampoco lo restauraría).

### 🐛 Un fallo del instrumento, no del producto (medido y corregido en el sitio)
La primera corrida dio tres `[FALLO]` y el producto estaba bien: mi sonda contaba **filas** de cable (el trazo mide ~3 px, así que cada cable producía 3 «cables») y comparaba contra `cables - 1`. Los cables se agrupan **por columna**, no por fila consecutiva —dos cables distintos son vecinos en la misma fila—, y con eso las siete fases quedaron verdes. Se deja escrito porque el error es del tipo que se repite: medir el lienzo por barrido de píxeles exige agrupar antes de contar.

### 🟠 Fronteras declaradas
- **Nada cambió en el producto**, así que no hay guardia ni mutación nueva: el aparato que ya existe sigue midiendo lo mismo —el renglón permanente de la sonda del lienzo («y el **Supr** de la tabla del núcleo borra el cable marcado, con el undo restaurándolo», dentro de los **105 `[OK]` · 0 `[FALLO]`**) y la mutación `cable-marcado-que-no-se-ve`—, y el atajo comparte clave con el escritorio por la tabla (`UnoShortcutParityGuardTests` censa que ninguna tecla mapeada en un host falte en la tabla).
- **El rehacer no entra en la sonda permanente**: se midió a mano (arriba) y el undo de la sonda ya implica que la acción está en la pila; añadir el paso de rehacer al renglón sería medir dos veces lo mismo.
- **Supr borra UNA cosa, la marcada**: con la marca puesta es el cable; sin marca, los nodos (conducta del escritorio). Marcar un cable desmarca los nodos a propósito.

## [2026-09-29] - Hito 282: la Mitad de los Cables del Lienzo (el traslado, sin cambiar ni una medida)

### 🎯 El encargo
El lienzo del host Uno tenía **los cables dentro del fichero del control** —un fichero de 3.271 líneas que también lleva el pan/zoom, las tarjetas, el teclado y los decoradores—. El encargo: sacar **la diana del cable, su menú y su sonda** a **su propia mitad parcial** del control, como ya se hizo con el instrumento del inspector (`NodeInspectorPanel.Probes.cs`), **sin cambiar ni una medida**.

### 🧱 Lo que se movió (entero, sin editar una línea de código)
| Pieza | Qué es |
| :--- | :--- |
| `EditorCanvasControl.Wires.cs` (**nuevo**, 278 líneas) | `DrawWires` (el cable que se ve **y su diana**), `CreateWireGeometry` —la figura, con su porqué de las dos Bézier—, `ToWindowsPoint`, las anclas y nombres (`CanvasWire`, `CanvasWireHit`, los grosores, `SelectedWireBrushKey`), `WireLabel`, `OnWirePressed` (el clic que marca y no panea), `BuildWireMenu` (la orden del núcleo con su rótulo) y `ProbeWireSelection`. |
| `EditorCanvasControl.xaml.cs` | **3.271 → 3.023 líneas**: se queda con el pan/zoom, las tarjetas, el teclado, los decoradores y el gesto del puerto. La figura del cable y sus dos sondas siguen aquí; el objeto `Path` de la diana se construye en la mitad nueva. |

El traslado se hizo **por rango de líneas, verbatim** (sin reformatear ni retocar comentarios), y el compilador es el que dice si el corte está bien: `FileFlow.Uno.slnx` con **0 errores**. La mitad nueva lleva su cabecera explicando **por qué** está separada (el control tiene tres sujetos y éste es el de los cables: se rompen juntos) y que la convención es la del instrumento del inspector.

### 🧪 Las medidas que había que NO mover (y no se movieron)
Un traslado sólo es inocuo si lo que medía sigue midiendo lo mismo. Pero cuatro guardias y **cuatro mutaciones** apuntaban al fichero viejo, así que el pase tuvo que **re-apuntar el aparato** —y el propio aparato lo dijo, sin que nadie lo adivinara:
- **La guardia de las mutaciones** (`EveryDeclaredMutation_ShouldStillFitTheProductAndTheSuite`) cantó los cuatro fragmentos que ya no aparecían: `cable-con-anclas-estimadas`, `cable-marcado-que-no-se-ve`, `cable-que-no-se-puede-pulsar` y `cable-que-no-toca-su-socket` (dos fragmentos). Los cuatro cambian de fichero, con el porqué del control de uno de ellos ajustado (ya no es «el mismo fichero»).
- **Los censos**: la guardia del redibujado lee ahora la mitad nueva para `DrawWires` (las suscripciones siguen en el fichero del control); la del cable lee **las dos mitades** donde la figura se declara en una y la usa la otra (el cable del grafo y su diana, allí; el pendiente del arrastre, aquí), con los **mismos números** (tres usos de la figura, tres del trazado del núcleo); la de **portabilidad de textos** añade la mitad nueva a su censo (el rótulo del menú, `Uno_Connection_Delete`, se cita ahora ahí: sin añadirla, la clave se habría quedado sin quien la vigile); y la de **geometría** barre **las dos mitades** del code-behind —una mitad nueva no puede quedar fuera de una regla que no admite excepciones históricas—. **El barrido por comodín no valía**: `EditorCanvasControl*.xaml.cs` no casa con `EditorCanvasControl.Wires.cs` (el nombre de la mitad sigue la convención del inspector, `<Control>.<Sujeto>.cs`), y la lista explícita de las dos mitades es la que lo deja claro.

### ✅ Validación (las mismas medidas, del mismo tamaño)
- **Playtest con ratón y teclado inyectados**, las cinco fases **verdes** igual que antes del traslado: el clic izquierdo **marca** (el píxel al acento `(236,72,153)` y el grosor **3 → 5 px**), **Supr borra** el cable marcado (los píxeles del color del cable caen de **179 a 117** —y el tramo horizontal del cable mide **62 px**, idéntico—), **Ctrl+Z lo devuelve** (→ 179), el clic en el **vacío** quita la marca, y el **clic derecho** sigue abriendo el menú del núcleo (su entrada se lee en el árbol) **borrando** la conexión y volviendo con el undo. *(El recuento absoluto cambia de una corrida a otra porque el muestreo va cada 2 px desde la esquina de la ventana, que no está en el mismo sitio; lo comparable —tramo, grosor y proporción— es idéntico.)*
- **Sonda del lienzo EXIT 0: 105 `[OK]` · 0 `[FALLO]` · VERIFICADO**, con los mismos renglones (eran 105 antes del traslado); las otras tres del host **59 / 42 / 18** EXIT 0 y la del **escritorio** **41 / 0** EXIT 0.
- **Suite completa: 1978 superadas + 1 omitida de 1979, 0 errores** —incluidas las cinco guardias que hubo que re-apuntar— y las dos soluciones **0 errores**.
- **Las cinco mutaciones del sujeto siguen MUERDEN** tras el traslado, ya mutando la mitad nueva: `cable-que-no-se-puede-pulsar` (28,8 s), `cable-marcado-que-no-se-ve` (29,4 s), `cable-que-no-toca-su-socket` (27,9 s), `cable-con-anclas-estimadas` (28 s) y `ancla-que-ignora-la-escala` (28,5 s), con **0 rechazos** (es decir: los fragmentos encajan en el fichero nuevo). `COVERAGE.md` regenerado por su guardia: **108 · 15/17 · 20/51**.

### 🟠 Fronteras declaradas
- **La sonda del seguimiento del cable** (`ProbeWireTracking`, con su forma del hueco estrecho y el pan/zoom que la mueve) **se queda en el fichero del control**: mide el plano y las anclas, no la capa de cables, y moverla habría arrastrado el aparato del pan/zoom. Es una mitad de cables, no un museo del cable.
- **El gesto del puerto** (pulsar/mover/soltar) sigue en el fichero del control: es el gesto de las **tarjetas** que arranca un cable, no la capa.
- **El `.xaml` no se tocó**: la capa `WireLayer` es del lienzo, y su mitad de cables la usa por nombre.
- **Nada de conducta cambió**, y eso es una frontera: el pase no arregla nada que estuviera roto —su valor es que el siguiente cambio en los cables no tenga que leer tres mil líneas de tarjetas—.

## [2026-09-29] - Hito 281: la Selección Visible del Cable (marcarlo, verlo y borrarlo con Supr)

### 🎯 El encargo
El hito 280 dejó el cable **borrable por su menú**; lo que faltaba es lo que el escritorio hace con un clic: **marcar** la conexión, **verla marcada** y borrarla con **Supr**. Sin la marca visible, el usuario elige algo que se ve igual que los demás y el atajo se lleva una conexión que nunca vio elegida.

### 🔬 El diagnóstico (la convención del producto, leída antes de escribir)
- **Cómo marca el producto un nodo**: `NodeViewModel.IsSelected` (lo pinta la tarjeta) + `EditorViewModel.SelectedNode` (el último), y la tecla entra por la **tabla del núcleo** `EditorKeyboardShortcuts.Execute`, cuyo caso `ShortcutKey.Delete` ejecuta `DeleteSelectedNodesCommand`. La vista del host no tiene estado propio: pide la orden y el grafo se entera.
- **El resalte del nodo** lo fija la propia tarjeta: un borde con `CanvasAccentPrimaryBrush`. Esa es la vara con la que se mide «seleccionado» en este producto (y el motivo de que el cable marcado use el mismo pincel, no uno inventado).
- **El escritorio no se toca**: allí el cable no se marca (no tiene gesto) y su borrado por menú sigue igual; el caso `Delete` de la tabla sólo cambia cuando **hay** un cable marcado, que es estado que el escritorio nunca pone.

### 🧱 Lo construido (cuatro piezas, ninguna capa nueva)
| Pieza | Qué hace |
| :--- | :--- |
| `EditorViewModel` (`SelectedConnection` + `SelectConnection`) | **La única casa de la marca** (uno a la vez): marcarla desmarca los nodos —Supr borra una cosa, la marcada—, desmarcarla (`null`) no toca la selección de nodos, y la invariante vive en `Connections.CollectionChanged`: el cable marcado que **sale** del grafo (borrado, undo, otro grafo) no puede dejar la marca puesta. |
| `EditorKeyboardShortcuts.Execute` (`case Delete`) | **Supr borra LO MARCADO**: si hay cable marcado, es el cable (por `DeleteConnection`, con su undo); si no, los nodos, que es la conducta del escritorio. Ocho líneas, en la tabla compartida. |
| `EditorCanvasControl.DrawWires` | El cable marcado **se ve marcado**: trazo `CanvasAccentPrimaryBrush` (el mismo acento de la tarjeta seleccionada) y **6 px** en vez de 3,5. La marca se pregunta al núcleo en cada trazado (`ReferenceEquals(connection, _editor.SelectedConnection)`): el cable **no lleva copia** que pueda divergir. |
| `EditorCanvasControl.OnWirePressed` | El clic **izquierdo** marca el cable y le entrega el **FOCO** al lienzo (hito 252) — sin foco ninguna tecla llega—; el **derecho** no reclama el teclado (su menú manda). El clic en una tarjeta y el clic en el vacío **desmarcan**: `Supr` borra lo que se ve marcado, y tiene que poder quitarse. |
| `SelfCheckCanvas` + `ProbeWireSelection()` | Dos renglones nuevos en la app viva: la marca se VE (el trazo del acento, más grueso) y **el Supr de la tabla del núcleo borra el cable marcado**, con el undo devolviendo la medida. |
| `mutations/cable-marcado-que-no-se-ve.json` (nuevo) | Quita el resalte (el cable se pinta siempre normal): la marca sigue en el núcleo y deja de verse. Testigo el caso del cable, control la figura (otro sujeto del mismo censo). |

### 🐛 El defecto que introdujo el primer intento (y lo cazó el rastro, no la intuición)
Al entregar el foco también en el pulsado del **botón derecho**, el menú del hito 280 dejó de quedarse abierto: el rastro del lienzo (`FILEFLOW_CANVAS_TRACE=1`) lo midió entero — `WIRE press right=True`, `WIRE RightTapped -> menú`, la ventana `MenuFlyoutPresenter#` **cargada y midiendo 141x38**, y en la línea siguiente `foco RECUPERADO del envoltorio ajeno (MenuFlyoutPresenter#<-Canvas#)`: reclamar el teclado cerraba el menú recién abierto. El playtest del clic derecho lo confirmó (la entrada «Eliminar conexión» ya no estaba en el árbol). **El arreglo no toca la reclamación**: el botón derecho ya no la pide —su menú es el dueño del teclado— y la izquierda sigue pidiéndola, que es lo que hace funcionar el Supr. Las dos mitades quedan medidas en el mismo playtest.

### ✅ Validación (medida)
- **Playtest con ratón y teclado inyectados sobre la app en marcha** (tema pastel activo: cable `(244,114,182)`, acento `(236,72,153)`), las cinco fases verdes:
  - **El clic IZQUIERDO marca el cable**: el píxel del trazo pasa de `(244,114,182)` a **`(236,72,153)`** —el acento— y el grosor medido en esa columna pasa de **3 a 5 px**.
  - **Supr borra el cable marcado**: los píxeles del color del cable caen de **185 a 123** (los del cable que estaba marcado) y **Ctrl+Z los devuelve a 185**.
  - **El clic en el vacío quita la marca**: el cable vuelve a su color.
  - **El clic DERECHO sigue abriendo el menú del núcleo** (su entrada se lee en el árbol, en `(L708,T484,R872,B519)`), **su entrada borra la conexión** (185 → 123) **y Ctrl+Z la restaura** (→ 185). El hito 280 no se reabrió.
- **Sonda del host Uno (lienzo) EXIT 0**: **105 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 103), con los dos renglones nuevos: «el cable marcado **SE VE marcado**: su trazo pasa al acento de selección y engorda» y «y el **Supr** de la tabla del núcleo borra el cable marcado, con el undo restaurándolo». Las otras tres sondas del host, EXIT 0: paneles **59**, barra **42**, ajustes **18**. Sonda del **escritorio** (`run-fast.ps1 -SelfCheck`) EXIT 0: **41 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
- **Suite completa: 1978 superadas + 1 omitida de 1979, 0 errores**; las dos soluciones (`FileFlow.slnx` y `FileFlow.Uno.slnx`) **0 errores**.
- **Mutaciones**: `cable-marcado-que-no-se-ve` (nueva) **MUERDE** (28,5 s), y siguen mordiendo las tres del mismo sujeto re-ejecutadas tras tocar la guardia (`cable-que-no-se-puede-pulsar`, `cable-que-no-toca-su-socket`, `ancla-que-ignora-la-escala`). `mutations/COVERAGE.md` regenerado por su guardia: **108 declaradas · 15 de 17 subsistemas · 20 de 51 guardias**.
- **El corte del pase**: la marca empezó con una bandera `IsSelected` en el propio cable (hermana de la del nodo) y se **podó antes de cerrar** —era una segunda copia de un estado que el núcleo ya tiene, y dos copias pueden divergir—; `ConnectionViewModel` queda **sin una línea de diff** y la guardia del trazo mide la marca que queda. También se actualizó el fragmento de `cable-que-no-se-puede-pulsar` (su bloque `old` citaba la línea del cable que el clic cambió).
- **Un `flake` de rendimiento, declarado con sus números**: el renglón `re-posicionado total bajo el umbral (< 60 ms)` del lienzo midió **58,1 / 61,1 / 62,5 / 72,3 ms** en corridas del mismo binario (y **3,7 ms** en la primera, con el proceso frío). Es la misma línea que ya había fallado antes de este pase (59,2 ms el del frame de drag), no una regresión: el pase añade una comparación por cable al trazar. Queda como frontera, medida por los dos lados.

### 🟠 Fronteras declaradas
- **La marca es de UN cable** (no hay multiselección de cables): es lo que el encargo pide y lo que el escritorio tiene —un cable se borra de uno en uno—; un `Ctrl+clic` de cables sería capacidad nueva.
- **Marcar el cable NO lo trae al frente ni cambia el orden de dibujo**: se repinta con el acento, y el orden de la capa es el del grafo.
- **El clic derecho no marca** (abre el menú): marcar y ofrecer el borrado en el mismo gesto se solaparían.
- **No se probó el Supr con el cable marcado sobre un grafo grande** (40 nodos/28 cables): el playtest corre sobre el ejemplo de 2 cables, y la sonda mide el camino del núcleo.
- **El escritorio no se tocó**: su caso `Delete` sigue borrando nodos (nunca marca un cable) y no se le añadió resalte.

## [2026-09-29] - Hito 280: El Cable que No Se Podía Seleccionar (la conexión que se ve y no se puede borrar)

### 🎯 El encargo
«No puedo seleccionar las conexiones para borrarlas.» El usuario ve el cable en el lienzo, quiere quitarlo y no tiene sobre qué pulsar: la capacidad (borrar una conexión) existe en el producto desde el escritorio, y en **este host estaba sin servir**.

### 🔬 El diagnóstico (leído en el árbol y medido en la app)
- **El cable no existía para el ratón**: `DrawWires()` materializa cada conexión como un `Microsoft.UI.Xaml.Shapes.Path` de **3,5 px** dentro de `WireLayer`, y esa capa nacía con **`IsHitTestVisible="False"`**. No era una diana pequeña: era nada.
- **Y el clic derecho hacía otra cosa**: al no haber nada bajo el puntero, el evento caía al fondo del lienzo, donde `OnCanvasPressed` arranca el **PAN** (`properties.IsRightButtonPressed && !HitsInteractiveControl(point)`). Es decir: el usuario pulsaba el cable para borrarlo y **se le movía el lienzo** (medido en este pase: el rectángulo del otro cable no cambia con el clic derecho *después* del arreglo; antes lo hacía el pan).
- **Lo que el producto sí tiene**: el escritorio borra una conexión con **clic derecho sobre el cable → menú → «Eliminar conexión»** (`FileFlow.App/Views/EditorView.axaml`, la entrada `DeleteConnection` de su diccionario, sobre `ConnectionViewModel.DeleteCommand` — la orden del **núcleo**, con su undo). Lo único que este host ofrecía era el clic derecho sobre un **socket** (hito 278), que desconecta ese puerto: no es lo mismo que el cable que el usuario está mirando.

### 🧱 Lo construido (la capacidad que el producto ya tenía, servida aquí)
| Pieza | Qué hace |
| :--- | :--- |
| `EditorCanvasControl.xaml` | `WireLayer` pasa a **`IsHitTestVisible="True"`**: sin eso, el cable se ve y no existe para el puntero. |
| `EditorCanvasControl.xaml.cs` (`DrawWires`) | Cada cable lleva su **DIANA**: la MISMA Bézier con un trazo **grueso (14 px) y transparente**, anclada (`CanvasWireHit`) y con su **nombre accesible** («origen: puerto → destino: puerto»). |
| Idem (`OnWirePressed`) | El botón derecho sobre el cable **no es el pan**: el manejador del cable marca el evento como atendido (el fondo no recibe lo ya atendido) y el menú se abre al soltar. |
| Idem (`BuildWireMenu`) | El menú del cable: **una** entrada, «Eliminar conexión», cuya orden es **la del núcleo** (`connection.DeleteCommand`, con su undo), igual que la del escritorio. El host no borra nada por su cuenta. |
| `FileFlow.App.Uno/Resources/Strings{,.es}.resx` | La clave `Uno_Connection_Delete` con **el texto del escritorio** (`DeleteConnection`): «Eliminar conexión» / «Delete Connection». |
| `UnoDialogPortabilityGuardTests` | La pareja `Uno_Connection_Delete` ↔ `DeleteConnection` entra en su tabla de textos compartidos, y **el lienzo entra en su censo**: la clave se mide como las de las demás superficies. |
| `UnoCanvasWireGuardTests` | Caso nuevo `TheWire_ShouldBeSelectableToBeDeleted_ThroughTheCoreOrder`: la capa alcanzable, la diana (trazo grueso y transparente, figura propia trazada del MISMO `wire`), el botón derecho no-pan, la orden del núcleo con su rótulo del diccionario, y la sonda que lo mide. |
| `SelfCheckCanvas.cs` + `ProbeWireSelection()` | Dos medidas nuevas en la app viva: cada cable tiene su diana y su menú es el del núcleo con el rótulo del diccionario. |
| `mutations/cable-que-no-se-puede-pulsar.json` (nuevo) | Quita la diana: testigo el caso nuevo, control la medida del ancla (otro sujeto del mismo censo). |

### 🐛 La trampa que midió la sonda (y que obligó a trazar dos figuras)
La primera versión de la diana **reutilizaba la misma `Geometry`** que el cable dibujado. La sonda del lienzo lo cazó al instante: `[FALLO] cables dibujados en la capa: 1 (esperados 8)` y siete sondas más cayendo con `ArgumentException: Value does not fall within the expected range` — una `Geometry` de WinUI **no se puede compartir entre dos `Path`**. La diana traza **su propia Bézier desde el mismo `wire` del núcleo** (el dibujo sale idéntico; lo que se comparte es el trazado, no el objeto), y el porqué queda escrito en el fuente. Consecuencia en la guardia de la figura: su censo pasa de **dos** usos de `CreateWireGeometry(wire)` a **tres**, con la razón (eran el cable del grafo y el pendiente; ahora también la diana).

### ✅ Validación (medida)
- **Playtest con el ratón inyectado sobre la app en marcha** (el ejemplo trae 2 cables, los dos con diana observable por UIA: `CanvasWireHit`, tipo `Group`, nombre `Folder Source: Out → Optimizador de Imágenes: In`, rect `(1003,686,1121,704)`):
  - **El clic DERECHO sobre el cable abre su menú**, con su entrada «**Eliminar conexión**» (leída del árbol)
  - **y NO panea el lienzo**: el otro cable sigue exactamente en `(1311,686,1432,704)` — el defecto que el usuario estaba viendo era, además de no poder borrar, que se le movía el lienzo.
  - **Su entrada borra la conexión**: 2 → 1 cable(s) con diana, y el **píxel** en el centro del cable pasa del color del cable `(244,114,182)` al fondo del lienzo `(255,248,250)`.
  - **Ctrl+Z la restaura** (2 cables y el píxel otra vez en `(244,114,182)`): la orden es la del **núcleo**, con su undo, no un borrado del host.
- **Sonda del host Uno (lienzo) EXIT 0**: **103 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 101), con los dos renglones nuevos: «cada cable del grafo tiene su diana en la capa (**2 de 2**): el cable que se ve se puede pulsar» y «y su menú es el del núcleo, con el rótulo del diccionario: '**Eliminar conexión**'». Las otras tres sondas del host, EXIT 0: paneles **59**, barra **42**, ajustes **18** (0 `[FALLO]`). El rendimiento del redibujado no se movió (build 40 nodos + 28 cables **124 ms**, frame de drag **3,8 ms**).
- **Suite completa: 1978 superadas + 1 omitida de 1979, 0 errores**; las dos soluciones **0 errores**.
- **Mutación nueva `cable-que-no-se-puede-pulsar` MUERDE** (30,4 s: testigo rojo, control verde, árbol restaurado por bytes y recompilado). `mutations/COVERAGE.md` regenerado por su guardia: **107 declaradas · 15 de 17 subsistemas · 20 de 51 guardias**.
- **Un rechazo del propio andamiaje, corregido antes de cerrar**: la primera declaración de esa mutación salió **IMPRECISA** porque su control (`TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor`) **también cae** con el mutante —ese caso cuenta los trazados del núcleo y la diana añade uno—. El control pasó a un hermano de otro sujeto (`TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt`) y el porqué quedó escrito en la declaración.

### 🟠 Fronteras declaradas
- **Sin cursor propio**: el cable del escritorio **no** pone `Cursor` (el `Cursor="Hand"` que aparece en `EditorView.axaml` es de un botón de las migas), así que aquí no se añade ninguno: sería capacidad nueva.
- **El clic derecho sobre un SOCKET sigue desconectando su puerto** (hito 278): son dos caminos que el escritorio también tiene por separado, y este pase no toca el del socket.
- **El menú lo coloca WinUI** (`MenuFlyout.ShowAt`, junto al elemento pulsado): no se fija una posición propia —el escritorio tampoco la fija— y la medición lee su entrada, no sus píxeles.
- **El borrado NO se probó sobre una conexión entrante a un nodo de subflujo ni sobre el cable pendiente del arrastre** (esos casos no tienen menú: el pendiente no es una conexión del grafo).
- **El escritorio no se tocó**: su menú del cable es el que ya tenía.

## [2026-09-29] - Hito 279 (cierre): el Playtest de Importar/Exportar y una Sola Fuente para los Textos del Gestor

### 🎯 El encargo
El arreglo del desempaquetador dejó dos deudas, ninguna capacidad nueva: **(1)** el cuerpo del gestor estrena botones de **importar y exportar** que nadie había pulsado —recorrerlos con el **ratón inyectado** sobre la app en marcha (abrir, elegir un archivo real, cancelar y la vuelta a la fila) y dejar medido qué hace cada uno; si alguno no puede completar su trabajo, **o se sirve o no se ofrece**—; y **(2)** cada texto del gestor vive en **tres copias** (el diccionario del plugin, el del host y lo que ya registra el cargador): dejar **una sola fuente de verdad** y que **la guardia siga mordiendo** si vuelven a divergir. Fuera del encargo: reabrir las dos conductas ya probadas (conectar nodos y el botón abriendo su diálogo) y llevarlo al **escritorio** (la fila de claves de la app de escritorio no es parte del defecto). El diff debía **encoger o quedarse igual**.

### 🔬 El playtest de importar/exportar (medido, con clics reales inyectados)
Recorrido completo sobre la app en marcha (cajón → tarjeta → fila `ParamPassword_PasswordList` → gestor), con el **mismo instrumento del hito 278** (`mouse_event` absoluto, `pywinauto` para leer anclas, `send_keys` para teclear en los pickers de WinRT):
- **Abrir**: la fila abre el gestor de ESTE host (el modal se titula `Gestor de Contraseñas - SmartUnpack`, que es la clave del diccionario del PLUGIN resuelta en español). Lista de partida `alfa / beta`, recuento «2 clave(s) cargada(s)».
- **IMPORTAR**: pulsar `PasswordManagerImportButton` abre el selector **`Abrir`** del host; se escribe la ruta de un fichero real de 14 bytes y se pulsa `Abrir` → el selector se cierra y el editor queda `alfa\rbeta\rgamma\rdelta` con el recuento en «4 clave(s) cargada(s)». **Sirve.**
- **IMPORTAR y CANCELAR**: reabrir el selector y pulsar `Cancelar` deja la lista **igual**. **Sirve.**
- **EXPORTAR**: pulsar `PasswordManagerExportButton` abre **`Guardar como`**; el selector **llega prerelleno** con el nombre sugerido (`passwords.txt`), y escribir la ruta sin vaciarlo antes produce el error del sistema («El nombre de archivo no es válido») —trampa medida—; con `Ctrl+A` + `Supr` el selector se cierra, **el archivo se crea en disco** con `alfa\nbeta\ngamma\ndelta`, y el aviso del resultado se muestra **dentro** del gestor (no en un modal aparte). **Sirve.**
- **La vuelta a la fila**: «Guardar Claves» escribe el parámetro y la fila enseña `alfa; beta; gamma; delta`; el aviso de frontera «se abre en el host de escritorio» aparece **0 veces** en todo el recorrido.
- **Veredicto**: los dos botones **completan su trabajo** en este host → **se quedan**; ninguno contesta que no puede.

### 🔬 La fuente única de los textos (medido antes de borrar)
- **Lo que de verdad estaba triplicado**: la familia **`PresetManager_*`** — **20 claves** en cada diccionario del host (`Strings.resx` y `Strings.es.resx`) que ya declaraba `FileFlow.Plugin.Integrations`, el plugin que trae la superficie. La familia **`PasswordManager_*` NUNCA tuvo copia en el host** (los textos del gestor de claves ya se resolvían del diccionario de `FileFlow.Plugin.Archives`): esto **corrige** la fila de la tabla de la entrada anterior, que daba por copiadas también esas claves.
- **Por qué la copia era redundante y peligrosa**: `PluginLoader.RegisterPluginResources` registra el diccionario de cada plugin al cargar el ensamblado (`PluginRegistryHelper.CreateConfiguredLoader` es el camino que usan los dos hosts), y `LocalizationManager.GetString` recorre los `ResourceManager` **en orden de registro** devolviendo el primero con valor: el del host se registra antes (en `App.OnLaunched`), así que **tapaba** al del plugin — dos fuentes de la misma frase, con la del host ganando—.
- **Medido antes de tocar**: las 20 copias eran **byte-idénticas** a las del plugin en los dos idiomas (0 discrepancias) → quitarlas **no cambia ningún texto visible**. 40 líneas menos en el diff.
- **Un artefacto de la deduplicación, corregido**: los dos `.resx` del host habían perdido su **BOM** al reescribirlos (ruido en el diff, sin efecto funcional). Se restauró para que el diff de los diccionarios sea **sólo las familias**: 20 claves fuera + `Uno_InspectorResetMetrics` (del hito 275) + `Node_Param_OpenPasswordManager` (del 279).

### 🧱 La guardia, reparada (y comprobada mordiendo)
`UnoDialogPortabilityGuardTests.EveryTextUsedByTheDialogs_ShouldExistInBothHostDictionaries` contrastaba **todas** las claves citadas contra los dos diccionarios del **host**, así que la deduplicación la dejaba roja exigiendo la copia que se acababa de quitar. Ahora el censo se contrasta **contra el diccionario que DECLARA cada familia** (tabla `PluginFamilies`: `PresetManager_` → `FileFlow.Plugin.Integrations`, `PasswordManager_` → `FileFlow.Plugin.Archives`), y además exige que el host **NO** vuelva a copiar esas familias (la tercera copia tapa a la del plugin) y que **cada par** de diccionarios declare las mismas claves en los dos idiomas. El nombre del caso se conserva porque **dos mutaciones ya declaradas** (`cuerpo-del-gestor-que-habla-con-el-almacen` y `tarjeta-sin-la-puerta-de-sus-parametros`) lo usan como **control** —renombrarlo las dejaría sin medida—.
- **Muerde por los tres caminos, medido uno a uno** (mutante aplicado a mano sobre los `.resx`, guardia roja, árbol restaurado y **verificado por `diff`** contra la copia previa): (a) quitar `PasswordManager_HeaderTitle` del diccionario del **plugin** → `[FAIL]`; (b) reintroducir la copia en el diccionario del **host** → `[FAIL]`; (c) dejar una clave del plugin **sólo en inglés** → `[FAIL]`. Sin mutación nueva declarada (el diff no debía crecer); la que ya existe sigue mordiendo.

### ✅ Validación (medida)
- **Playtest con ratón inyectado**: arriba, todos los desenlaces verdes.
- **Cuatro sondas del host Uno EXIT 0**: lienzo **101 `[OK]` · 0 `[FALLO]`**, paneles de nodo **59 / 0**, barra **42 / 0**, ajustes **18 / 0**. **Sonda del host de ESCRITORIO** (`run-fast.ps1 -SelfCheck`) **EXIT 0 · 41 `[OK]` · 0 `[FALLO]`**.
- **Suite completa**: **1977 superadas + 1 omitida de 1978, 0 errores**. Build de las dos soluciones **0 errores**.
- **Dos *flakes* de CPU vistos en la sesión** (medidos y declarados, ninguno tocado por este pase): en una corrida completa cayó `EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` (ya conocido) y en otra `SystemPerformanceMonitorTests.TheHeartbeat_ShouldPublishAPlausibleSample`; **los dos verdes en aislamiento** (9 casos, 38 ms) y la corrida completa siguiente **verde entera**. Son medidas de carga de la máquina, no del árbol.
- **Mutaciones re-ejecutadas: las cuatro MUERDEN** — `gestor-de-claves-que-el-host-no-sirve` (29 s), `fila-de-presets-sin-su-boton` (28,5 s) y las dos cuyo **control es la guardia tocada** (`cuerpo-del-gestor-que-habla-con-el-almacen` 27,4 s y `tarjeta-sin-la-puerta-de-sus-parametros` 27,2 s: control emparejado y verde, testigo rojo). `mutations/COVERAGE.md` regenerado por su guardia: **106 declaradas · 15 de 17 subsistemas · 20 de 51 guardias**.

### 🐛 Hallazgo de método (una trampa de la sesión, cazada por la propia sonda)
El **sabor de UI lo elige el NOMBRE de la solución** (`Directory.Build.props`): compilar **`FileFlow.slnx`** (escritorio) *después* de `FileFlow.Uno.slnx` deja en el `bin` del host Uno los **plugins del sabor de escritorio** (sin `FILEFLOW_NO_DESKTOP_TOOLKIT`). Con ese `bin` híbrido, la sonda de los paneles de nodo **falla sus dos medidas de la frontera** (el nodo del Estudio de Scripts toma la rama del toolkit y **no llega a declarar**: `[FALLO] su botón AVISA … ('')`), sin que haya nada roto en el producto. Se midió las dos caras: con ese `bin` la sonda da **57 / 2**; reconstruyendo **`FileFlow.Uno.slnx` al final**, **59 / 0**. *La solución del host Uno se compila la ÚLTIMA antes de sondearlo.*

### 🟠 Fronteras declaradas
- **La familia `PasswordManager_*` ya era de una sola fuente** (el host nunca la copió): la deduplicación fue la de `PresetManager_*`. Queda como está — el gestor de claves se sigue leyendo del diccionario de su plugin.
- **No se añadió mutación nueva** para la guardia reparada: morder está **demostrado a mano por sus tres caminos** y el encargo pedía que el diff no creciera. Si otro pase quiere atarlo con el andamiaje, la mutación natural es reintroducir una copia en el diccionario del host.
- **La fila de claves del ESCRITORIO** sigue sin editor ni botón (`IsStandardInput` excluye `IsPasswordList`): defecto latente anterior, **fuera de este encargo por indicación expresa**.
- **El aviso inline de exportación** se lee por el ancla `HostConfirmationAccept` del modal del host: el playtest mide que **aparece dentro del gestor**, no que su texto sea el esperado (el texto sale del diccionario del plugin, ya cubierto por la guardia de textos).

## [2026-09-29] - Hito 279: El Gestor de Claves del Desempaquetador (la capacidad que se ofrecía sin poder servirse)

### 🎯 El encargo
«En el nodo desempaquetador, pulsar la acción de claves contesta con el aviso “se abre en el host de escritorio” en vez de abrir su diálogo. El resultado esperado es que en ESTE host el botón abra su diálogo y funcione lo que ese diálogo ofrece, y que el mensaje deje de poder aparecer por esa puerta. Lo importante no es sólo el botón: hoy la misma capacidad se declara “no servida” en la tabla de puertas de la ficha y a la vez se ofrece como acción de la tarjeta. Deja esas dos puertas de acuerdo (la tabla, la acción y el camino de compilación deben decir lo mismo), sin borrar la capacidad por la vía fácil de esconder el botón. Lo mides pulsando de verdad con el ratón inyectado sobre la app en marcha, y dejas una guardia o sonda que muerda si la oferta y la capacidad vuelven a contradecirse.»

### 🔬 El diagnóstico (leído en el árbol y medido en la app)
- **El aviso era literal del diccionario del plugin**: `Plugin_DesktopOnly_Title` + `Plugin_DesktopOnly_Message` con `PasswordManager_WindowTitle` («Gestor de Contraseñas - SmartUnpack») — carácter a carácter el modal de la captura del usuario. Sale de `DesktopOnlySurface.Declare` en la rama `#if FILEFLOW_NO_DESKTOP_TOOLKIT` de `SmartUnpackNode` y `ArchiveFanOutNode`.
- **La contradicción, medida en el árbol**: la ficha declaraba `OpenPasswordManagerCommand` en su tabla de **puertas pendientes** («abre el gestor de contraseñas, una ventana que este host todavía no tiene») y la **misma capacidad** se ofrecía como acción del nodo (`ManagePasswords`, la que pinta la tarjeta y la ficha). El nodo **no declaraba ninguna superficie** al SDK, así que no había forma de servirla: la única salida del botón era declarar la frontera.
- **El camino de compilación decía lo mismo que las otras dos puertas: nada.** La ventana vive en `FileFlow.Plugin.Archives/UI/Views`, que en el host Uno ni se compila (`Compile Remove="UI\Views\**\*.cs"`), y no había clave de diálogo ni view model portable que los dos hosts pudieran pintar.
- **Y el gestor se ofrecía por una fila que no podía abrirlo** en el escritorio: su botón «Claves» vive dentro del bloque `IsVisible="IsStandardInput"`, y `IsStandardInput` **excluye** `IsPasswordList` — la fila de claves del escritorio no enseña ni editor ni botón (defecto latente, de antes de este pase; ver fronteras).

### 🧱 Lo construido (la capacidad servida, no escondida)
| Fichero | Qué cambia |
| :--- | :--- |
| `FileFlow.Sdk/Services/IWindowService.cs` | Clave canónica nueva: `DialogKeys.PasswordManager` (la identidad del diálogo no depende del host). |
| `FileFlow.Plugin.Archives/UI/ViewModels/PasswordManagerViewModel.cs` (nuevo) | El gestor **portable**, sin toolkit: el texto (una clave por línea), su recuento, la forma canónica que se guarda (`; `), la lectura/escritura del .txt y la vuelta al nodo. Es donde vive la regla del producto; las dos vistas la pintan. |
| `FileFlow.Plugin.Archives/UI/Views/PasswordManagerWindow.axaml(.cs)` | La ventana del **escritorio** pasa a ser una VISTA de ese view model (antes tenía la regla en su code-behind): el editor y el recuento van enlazados y guardar/cerrar es lo único que decide la ventana. |
| `FileFlow.Plugin.Archives/SmartUnpackNode.cs` y `ArchiveFanOutNode.cs` | Los dos nodos que ofrecen «🔑 Claves...» declaran la superficie (`INodeDialogSurfaceProvider`, clave `PasswordManager`, `ReplacesCustomActionId => "ManagePasswords"`) con su carga útil portable y **una sola vuelta** al parámetro (`SavePasswordList`), compartida por la superficie y la rama del toolkit. |
| `FileFlow.App.Uno/Controls/PasswordManagerBody.xaml(.cs)` (nuevo) | El cuerpo del **host Uno**: pinta el mismo view model, con su selector de archivos (el del host) para importar/exportar. Ninguna regla del producto: ni una línea escribe el parámetro del nodo. |
| `FileFlow.App.Uno/Platform/UnoWindowService.cs` | La clave pasa a la tabla de **servidos** (`(DialogKeys.PasswordManager, nameof(PasswordManagerBody))`), con la comprobación de carga útil, la propiedad `ActivePasswordManager` para la sonda y el modal (`Guardar Claves` guarda por el view model; cancelar/Escape descarta). |
| `FileFlow.App/Services/AvaloniaWindowService.cs` | El escritorio sirve la MISMA clave con la ventana del plugin sobre ese view model (sin esto, declararla habría roto el escritorio). |
| `FileFlow.Core` → `NodeParameterViewModel.cs` | La **fila** abre la superficie declarada primero (`OpenPasswordManagerAsync`, con la vuelta que resincroniza) y sólo cae al camino del toolkit si el nodo no declara nada — el mismo contrato que ya usaba el gestor de presets. |
| `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | El botón «🔑» de la fila (`ParamPassword_` + clave, mismo flag `IsPasswordList` que el escritorio) y la orden movida de **pendientes** a **servidas** en la tabla de puertas: las dos puertas dicen lo mismo. |
| `FileFlow.App.Uno/Resources/Strings{,.es}.resx` | Las claves que la superficie cita (las del plugin, copiadas como las del gestor de presets) y `Node_Param_OpenPasswordManager`, en los dos idiomas. |
| `FileFlow.Tests/Unit/App/UnoDeclaredSurfaceGuardTests.cs` | Caso nuevo: censo **por barrido** de todos los nodos que ofrecen `ManagePasswords` —cada uno tiene que declarar su superficie—, el host que la sirve (servida, no pendiente), las dos vistas como vistas del view model portable y las dos puertas cableadas. |
| `FileFlow.Tests/Unit/App/UnoNodeDialogCatalogGuardTests.cs` y `UnoDialogPortabilityGuardTests.cs` | La fila de claves se dibuja donde el escritorio la marca (`IsPasswordList`), y el censo de textos del host incluye ya la familia `PasswordManager_`. |
| `mutations/gestor-de-claves-que-el-host-no-sirve.json` (nuevo) | Quita la clave de la tabla de servidos: testigo el caso nuevo, control el gestor de presets. |

### ✅ Validación (medida)
- **Playtest con el ratón inyectado sobre la app en marcha** (el camino entero, con clics reales): se añade el nodo por el cajón (chip *Archives* + doble clic) → se pulsa su tarjeta → la ficha expone el botón `ParamPassword_PasswordList` → **pulsarlo abre el GESTOR DE CONTRASEÑAS de este host** (clave `PasswordManager`), no el aviso → se teclea `alfa / beta` (el recuento de la superficie dice «2 clave(s) cargada(s)») → «Guardar Claves» cierra y **la fila enseña `alfa; beta`** → la **ACCIÓN del nodo** (`InspectorAction_ManagePasswords`, la puerta de la tarjeta) abre el mismo gestor **con la lista guardada ya cargada** (ida y vuelta) → cancelar deja el host limpio → y el aviso «se abre en el host de escritorio» **aparece 0 veces** por ninguna de las dos puertas.
- **Defecto cazado por el playtest** (no por el censo): el gestor **reabría con la lista en una sola línea** —`alfa; beta`— porque el view model nuevo no partía por el «; » con el que el nodo guarda el parámetro. Corregido en la MISMA entrada (`Separators` incluye el «; ») y re-medido: reabre con una clave por línea.
- **Sonda del host (paneles de nodo) EXIT 0**: **59 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 51: +8 comprobaciones nuevas —ancla, apertura, regla del view model, guardado en el parámetro, la puerta de la acción, el cierre y la traza—). El resto de sondas, con sus cifras: lienzo **101 / 0**, barra **42 / 0**, ajustes **18 / 0**.
- Suite completa: **1977 superadas + 1 omitida de 1978, 0 errores** (2 m 39 s); build de las dos soluciones (`FileFlow.Uno.slnx` y `FileFlow.slnx`) **0 errores**.
- **Sonda del host de ESCRITORIO** (`run-fast.ps1 -SelfCheck`) **EXIT 0**: **41 `[OK]` · 0 `[FALLO]` · VERIFICADO** — la ventana de la otra rama (la que monta el toolkit) sigue arrancando con su ventana convertida en vista del view model portable.
- Mutaciones re-ejecutadas tras el cambio (una nueva y una que el producto invalidaba): `gestor-de-claves-que-el-host-no-sirve` **MUERDE** (29,4 s) y `fila-de-presets-sin-su-boton` **MUERDE** (28,6 s) —su fragmento se actualizó al nuevo texto de la condición, como manda su guardia—. `mutations/COVERAGE.md` regenerado: **106 declaradas · 15 de 17 subsistemas · 20 de 51 guardias**.

### 🟠 Fronteras declaradas
- **La rama del toolkit sigue en los dos nodos** como defensa declarada (`DesktopOnlySurface.Declare`): un host que ignore las superficies declaradas —o que llame al proveedor directamente— sigue avisando en vez de quedarse mudo. Las dos puertas del producto (la fila y la acción) ya no pasan por ahí: `NodeViewModel` y `NodeParameterViewModel` abren la superficie declarada ANTES de caer al camino del toolkit.
- **El gestor se ofrece por la tarjeta Y por la ficha Y por la fila** (tres puertas, una superficie); en el escritorio la fila **no** enseña su editor ni su botón «Claves» (`IsStandardInput` excluye `IsPasswordList`): la fila de claves del escritorio sólo tiene la puerta de la tarjeta. Es un defecto **latente y anterior** a este pase —no se tocó por no cambiar la conducta del otro host— y queda nombrado aquí.
- **El botón de importar/exportar del host Uno usa su propio selector** (el del host), y el VM sólo recibe rutas: el archivo lo elige la vista y la regla —qué es una clave— es del view model.
- **La cifra del lienzo sigue siendo 101**, la misma que dejó el hito 278 (sin comprobaciones nuevas de este pase).

## [2026-09-29] - Hito 278: El Gesto del Cable, de Punta a Punta (dos defectos del usuario, medidos con el ratón inyectado)

### 🎯 El encargo
«El usuario no puede conectar nodos: al pulsar sobre un puerto, o bien se arrastra la tarjeta en vez de arrancar el cable, o bien no ocurre absolutamente nada. Haz que el gesto funcione de punta a punta sobre la app en marcha: pulsar el puerto con el botón izquierdo arranca el cable sin mover ni arrastrar la tarjeta; mover el puntero lo dibuja; soltarlo sobre un puerto compatible crea la conexión; y sobre un destino incompatible, sobre vacío o con Escape, la cancela dejando el estado limpio (sin cable fantasma y sin captura de puntero colgada). Comprueba cada desenlace con el ratón inyectado sobre la ventana real, no leyendo código, y arregla lo que rompa». El otro síntoma que reportó el usuario —el botón del desempaquetador que avisa en vez de abrir su ventana— quedaba **fuera de este pase**, y no se tocó.

### 🔬 El diagnóstico (medido con el ratón inyectado, no leído)
Tres defectos independientes, cada uno capaz de matar el gesto por su cuenta:

1. **El cableado de los sockets no existía.** Pulsar la etiqueta de un puerto **sí** entraba al handler de la tarjeta (`OnSocketPressed`, con su `PortViewModel`) y **moría ahí**: `SocketRequested` no tenía suscriptor. `WireCardEvents` se llamaba desde `Rebuild` —donde el `ItemsSource` acaba de asignarse y **todavía no hay ninguna vista**— y buscaba la vista en `pair.Value.Content`, que es el **`NodeCardViewModel`**, no el `NodeCardView`: la comparación no podía dar `true` nunca. Dos errores en la misma línea de razonamiento, y el defecto entero del «no pasa nada».
2. **El punto del puerto estaba fuera de la tarjeta.** La fila del puerto sangraba 16 px a cada lado (`Grid Margin="-16,0"`) para sacar el socket al borde, y ese sobrante quedaba **fuera del rectángulo de la tarjeta**: no se pintaba **ni se podía pulsar**. Medido con el barrido de pulsaciones sobre la ventana real a la altura de la fila: de `x=702` a `x=732` (físicos) el pulsado era del **puerto**; a partir de `x=735` —donde está el punto— **no ocurría nada en absoluto**, ni puerto ni lienzo; y en la cara de la tarjeta, a `x≤700`, el pulsado **armaba el arrastre del nodo**. Son exactamente los dos síntomas que reportó el usuario, separados por unos píxeles.
3. **Sin captura de puntero no llegaban los movimientos.** Con la pulsación en la fila, la app recibía **un solo** `PointerMoved` —en la posición de la pulsación— y **ninguno más** hasta soltar (medido con el rastro y con una sonda que inyecta y cronometra): el cable no podía seguir al cursor. Con `CapturePointer` en la fila, los movimientos llegan uno a uno.
4. **Cable fantasma (cazado por la sonda, no por el playtest).** `EndSocketGesture` cambiaba el estado del núcleo pero **nunca sacaba de la capa el cable en la mano**: quedaba colgado del último punto del arrastre. Lo cazó la sonda del sondeo en la app viva —`cable en la capa=True` en todos los cierres, `93 [OK] / 3 [FALLO]`— antes de que el playtest lo midiera (el playtest lo daba por bueno porque medía la banda equivocada).

**Instrumento (hallazgo de método, para las sesiones siguientes)**: `SetCursorPos` mueve el cursor pero la app **ve la posición de la pulsación en todos los movimientos siguientes** (con captura activa); lo que un ratón físico genera —y lo que la app procesa bien— es un evento **absoluto** (`mouse_event(MOUSEEVENTF_MOVE|MOUSEEVENTF_ABSOLUTE)`, o `SendInput`). El playtest se apoya en eso y **espera a que la app procese cada movimiento** (lo delata el rastro) en vez de suponer que el mensaje ya llegó.

### 🧱 Lo construido (mínimo para la conducta pedida)
| Fichero | Qué cambia |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs` | **El cableado** se hace en el pase de layout que ya tiene contenedores (`WireCardEvents()` desde `OnNodesHostLayoutUpdated`) y la vista se busca en el **árbol visual** del contenedor con un `FirstDescendant<NodeCardView>` nuevo. `EndSocketGesture` **retira el cable pendiente** de la capa (`ClearPendingWire`), que es lo que cierra el gesto sin fantasma. La sonda `ProbeSocketGesture` exige además que **todas las tarjetas materializadas estén cableadas** y **explica por qué** falla cada desenlace; el rastro del gesto (arranca / cambia de destino bajo el cursor / cierra con el recuento de conexiones) queda escrito con `FILEFLOW_CANVAS_TRACE=1`, el mismo instrumento de sesión del hito 252. |
| `FileFlow.App.Uno/Controls/NodeCardView.xaml` | Los puertos viven **dentro** de la cara de la tarjeta (se va el sangrado de ‑16 px), la fila lleva **relleno** (`Padding="4,3"`) y fondo transparente —la diana deja de ser el texto de la etiqueta, que medía ~19 px— y se engancha `PointerReleased`/`PointerCaptureLost`. |
| `FileFlow.App.Uno/Controls/NodeCardView.xaml.cs` | La fila del puerto **se queda con el puntero** al pulsarla (`CapturePointer`) y lo suelta por los dos finales del gesto (`OnSocketReleased`), para que los movimientos lleguen al lienzo sin dejar una captura colgada. |
| `FileFlow.Tests/Unit/App/UnoCanvasWireGuardTests.cs` | El caso del gesto vigila también el **cableado** (la llamada después del posicionamiento, la búsqueda en el árbol visual), la **captura y su suelta** y el **relleno** de la diana; cita la mutación nueva. |
| `mutations/socket-que-se-queda-sin-cablear.json` (nuevo) | La mutación del cableado: `FirstDescendant<NodeCardView>(pair.Value)` → `pair.Value.Content as NodeCardView`. Testigo: el caso del gesto. Control: la figura del cable. |

### ✅ Validación (medida)
- **Playtest con el ratón inyectado sobre la ventana real** (9 desenlaces, todos verdes): pulsar el socket arranca el gesto y **no** mueve la tarjeta · mover dibuja el cable (banda hacia el puntero: 998 px distintos) · soltar en un puerto compatible **crea la conexión** (`conexiones 1->2`, y el click derecho la quita) · soltar en el vacío, con Escape y sobre un destino incompatible **cancela** (`destino=ninguno`, `conexiones 1->1`, `cable en la capa=False`) · tras soltar no queda captura colgada · y la cara de la tarjeta **sigue arrastrando el nodo** (la regresión del arreglo).
- **El punto del puerto se ve**: el mapa de píxeles de la fila muestra ahora el socket dibujado (15×15) junto a la etiqueta, dentro de la tarjeta — antes esa zona estaba vacía.
- **Cuatro sondas del host EXIT 0**: lienzo **101 `[OK]` · 0 `[FALLO]`** (era 97: la sonda del gesto añade sus cuatro comprobaciones y **pasó de 93/3 a verde** al quitar el cable fantasma) y **42 / 18 / 51** en barra, ajustes y paneles.
- Suite completa: **1976 superadas + 1 omitida de 1977, 0 errores** (2 m 42 s); build `FileFlow.Uno.slnx` **0 errores**.
- Las **dos mutaciones del gesto muerden en aislamiento**: `gesto-de-puerto-que-no-conecta` (29,9 s) y `socket-que-se-queda-sin-cablear` (31,1 s), testigo rojo, control verde y árbol restaurado por bytes. `mutations/COVERAGE.md` regenerado por su guardia: **105 declaradas · 15 de 17 subsistemas · 20 de 51 guardias**.

### 🟠 Fronteras declaradas
- **El ancla del cable es el centro de la fila** (etiqueta + punto + relleno), no el centro del punto: es de antes de este pase y no se cambió (la fila es el elemento que declara el `PortViewModel`), pero ahora la fila está dentro de la tarjeta, así que el cable muere unos píxeles más adentro.
- **La diana al soltar es de 48 px** (`SocketDropTolerance`, radio): es una **decisión de producto nueva** (antes eran 20 px sobre la tarjeta bajo el puntero) y es lo que hace que soltar unos píxeles corto **sí** conecte.
- **`SetCursorPos` no sirve para inyectar arrastres** en esta app: mueve el cursor y la app sigue viendo la posición de la pulsación. Queda escrito arriba para no repetir la trampa.
- **El defecto del botón del desempaquetador sigue abierto** (es el otro síntoma del informe del usuario): quedó fuera del encargo de este pase, por indicación expresa.
- El punto del puerto se dibuja con la matriz del socket (forma por tipo, relleno por estado): en estado libre el relleno es del color de la paleta, así que se lee como una ficha cuadrada — es la matriz del escritorio, no un cambio de este pase.

## [2026-09-29] - Hito 277: La Partición, Terminada: los Cuatro Modos en su Casa y Cada Guardia por su Sujeto

### 🎯 El encargo
«El pase anterior abrió la separación pero la dejó a medias: en el fichero del sondeo siguen dentro sus cuatro modos y el cinturón de medida compartido, y las dos guardias de unas novecientas líneas continúan mezclando sujetos distintos, que es lo que mantiene el diseño en la nota más baja del hilo. Termina esa misma separación con el criterio que el propio pase dejó escrito —una capa, una casa; cada preocupación recibe el comprobador de quien la llama; la vista sin su instrumento y el instrumento sin veredictos—, llevando cada modo junto a sus iguales y agrupando cada guardia por el sujeto que vigila, sin cambiar una línea de comportamiento del producto ni el veredicto de ninguna sonda.»

### 🔬 El diagnóstico (medido antes de tocar nada)
- **`RuntimeSelfCheck.cs`: 3079 líneas** — el pase anterior sacó el marco y los paneles, pero seguían dentro **los cuatro modos** (el del lienzo, con su `Inspect` de 388 líneas; ajustes, 405; barra de control, 791; diálogos, 953) y **el cinturón de medida compartido** (recorrer el árbol visual y describir un fallo), que los cuatro citaban desde su propio fichero.
- **Dos guardias de ~900 líneas mezclando sujetos**: `UnoControlBarParityGuardTests` (**965**) —censo de entradas, paridad de órdenes, atajos, entradas con ventana, textos y su medición— y `UnoNodeDialogsGuardTests` (**899**) —catálogo de diálogos, filas del inspector, portabilidad de las vistas, textos, superficies declaradas por el nodo y su medición—.
- **Ayudantes con dos dueños**: la lectura de una tabla declarada en el control de la barra (`Table`/`ReadTable`, usada por dos guardias) y la lectura de un `.resx` (`Dictionary`, escrita **dos veces**, idéntica en los dos ficheros). Y el fichero del sondeo tenía **dos** `FixtureSignalPath` (el suyo y el que ya declaraba `SelfCheckUia`).

### 🧱 La estructura resultante (una capa, una casa)
| Casa | Qué es | Tamaño |
| :--- | :--- | :--- |
| `RuntimeSelfCheck.cs` | El **DESPACHADOR, y nada más**: el bucle de reintentos hasta ver las plantillas materializadas, el veredicto por código de salida y el informe `selfcheck-report.txt`. Arranca el modo del lienzo y no contiene ningún modo. | **104** (era 3079) |
| `SelfCheckTree.cs` (nuevo) | El **cinturón de medida compartido**: recorrer el árbol visual (la única vía de WinUI), el ascendiente del contenedor generado y describir el marco de una excepción. Los modos lo piden por su nombre. | 141 |
| `SelfCheckCanvas.cs` (nuevo) | El **modo del LIENZO** (el base): tarjetas, cables, área de clic, fases 3.2/3.3/3.4 por los mismos métodos de los handlers, el tema en caliente (3.5) y el rendimiento con el grafo de referencia (3.6). Es el dueño de `PerformanceProbeRan`, el one-shot que lee el despachador. | 361 |
| `SelfCheckPointerless.cs` (nuevo) | Las medidas que **este entorno no puede recorrer con el puntero real** (el inyectado entrega pulsaciones pero no movimientos): superficie UIA (238), foco (252), enrutado de atajos (252), seguimiento de cables (254) y reclamación del teclado (253). Recibe el comprobador del modo del lienzo. | 109 |
| `SelfCheckSettings.cs` (nuevo) | El modo de **AJUSTES** (`--selfcheck-settings`), con sus dos tiempos y su informe. | 453 |
| `SelfCheckControlBar.cs` (nuevo) | El modo de la **BARRA Y SU CAJÓN** (`--selfcheck-controlbar`), con su censo de entradas (16 + 15) y las tres lecturas de su ciclo. | 893 |
| `SelfCheckDialogs.cs` (nuevo) | El modo de los **PANELES DE NODO** (`--selfcheck-dialogs`), con sus dos lecturas de fila y la del almacén de presets. | 1061 |
| `SelfCheckUia.cs` | La observación externa, que ahora alberga **también** la montura del fixture (`MountUiaExternalScene`/`TryMountUiaScene`): la escena del inspector se prepara en el modo que la observa. | 383 |

**Las guardias, por el sujeto que vigilan** (cada una declara sólo los ficheros que lee: el censo bajó **65 constantes sin uso**):

| Guardia | Sujeto | Tamaño |
| :--- | :--- | :--- |
| `UnoControlBarEntryGuardTests` | El **censo de entradas** de la barra y su paridad con el escritorio (órdenes + atajos) y su medición. | 481 |
| `UnoControlBarSurfaceGuardTests` | Las entradas que abren una **VENTANA** por el catálogo de diálogos (ajustes, VFS, métricas, estudio de temas, actualización) y el diseñador de datasets. | 358 |
| `UnoControlBarTextsGuardTests` | Los **textos** del menú, copiados del escritorio clave por clave. | 169 |
| `UnoNodeDialogCatalogGuardTests` | El **catálogo de diálogos** del host y las filas del inspector que los abren. | 327 |
| `UnoDialogPortabilityGuardTests` | La **portabilidad** de las dos vistas de diálogo (view models portables) y sus textos. | 173 |
| `UnoDeclaredSurfaceGuardTests` | Las **superficies que declara el nodo** (gestor de presets, diseñador de datasets) y sus órdenes destructivas. | 398 |
| `UnoNodeDialogProbeGuardTests` | La **medición** de los paneles de nodo (modo propio, canal del usuario, valor escrito). | 81 |
| `TestHelpers/UnoControlBarTables.cs` (nuevo) | La lectura de una tabla del control, que **dos** guardias necesitaban. `EmptyableTable` desaparece: el ayudante admite la tabla vacía. | 46 |
| `TestHelpers/HostDictionaries.cs` (nuevo) | La lectura de un `.resx` como clave → valor, que estaba **escrita dos veces**. | 34 |

Y la guardia de la forma (`UnoSelfCheckLayoutGuardTests`, **6 casos**, antes 3) pasa a fijar el reparto nuevo: el despachador sólo despacha; cada modo declara su clase y su informe, y ningún otro fichero escribe ese informe; cada medida tiene una sola casa; el recorrido del árbol vive en el cinturón; y las preocupaciones reciben el comprobador (marco, paneles y puntero) sin escribir veredictos.

### 🧁 Lo que se tiró (superseded)
- `EmptyableTable` (dos líneas que sólo envolvían `ReadTable`) · **65 constantes privadas sin uso** en las guardias nuevas (cada una declara ya sólo lo que lee) · la **lectura de `.resx` duplicada** (`Dictionary` en los dos ficheros) · el **`FixtureSignalPath` duplicado** del sondeo (ya vivía en `SelfCheckUia`) · el `<summary>`/`<param>` huérfanos de `MountUiaExternalScene` (tenía dos cabeceras, una con parámetros de un método que ya no los tiene) · los ayudantes de tabla duplicados.
- **Nada que quedara sin casa**: las 28 afirmaciones de las dos guardias (14 + 14) se cuentan una a una antes y después —las mismas, verbatim—; el reorden no reescribió ninguna aserción.

### ✅ Validación
| Prueba | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** (50 avisos) |
| Las **cuatro sondas** del host | **EXIT 0**: `-SelfCheck` **97 `[OK]` · 0 `[FALLO]` · VERIFICADO**, control-bar **42**, settings **18**, dialogs **51** — la misma cuenta y el mismo veredicto que antes del corte: el código se movió entero |
| Suite completa | **1975 superadas + 1 omitida de 1976, 0 errores** (2 m 31 s; +3 casos de la guardia de forma) |
| `mutations/COVERAGE.md` | Regenerado por su guardia: **103 declaradas · 15 de 17 subsistemas · 20 de 51 guardias** (antes 17 de 46) |
| Las 103 referencias | Resuelven contra el árbol: **112 reemplazos**, 60 ficheros, **0 sin resolver** (comprobado tras el corte) |
| `./mutate.ps1 -All` | **103 ejecutadas**: la pasada completa marca **101 mordidas** y **2 marcadas** (una superviviente y un rechazo del andamiaje por binarios); **las dos repetidas EN AISLAMIENTO muerden** (`vista-del-disenador-con-su-propio-modelo` 26,1 s · `tabla-en-cache-sin-tope` 26,0 s: testigo rojo, control verde, árbol restaurado por bytes) — el marcado era del andamiaje al encadenar 103 corridas, no de una guardia muerta |

### ⚠️ Fronteras declaradas
1. **Sin cambio de comportamiento**: es una mudanza, no una reescritura; la prueba fuerte son las cuatro sondas con la misma cuenta y el mismo veredicto, y el suite entero verde.
2. **Los dos modos grandes siguen grandes**: `SelfCheckControlBar.cs` (893) y `SelfCheckDialogs.cs` (1061) son **un sujeto cada uno** (un modo, un proceso, un informe), pero dentro tienen secciones —censo/cajón/ejecución, y cada familia de diálogo— que otro pase podría partir con el mismo criterio. No se hizo aquí porque el encargo era sacar los modos del fichero del sondeo, no abrirlos por dentro.
3. **`SelfCheckPointerless` agrupa por el eje «lo que el puntero no puede medir aquí»**, no por familia: sus cinco medidas son de hitos distintos (238, 252, 253, 254) y las une la razón por la que están juntas en un mismo archivo —el puntero inyectado entrega pulsaciones pero no movimientos—, no un tema común.
4. **Dos guardias nuevas no tienen mutación que las muerda**: `UnoControlBarTextsGuardTests` y `UnoNodeDialogProbeGuardTests` aparecen en la lista de trabajo de `COVERAGE.md`. Se declara aquí en vez de dejarlo caer.
5. **Un defecto de este pase, cazado por su propia validación**: el ayudante compartido de tablas se escribió con una lectura **infiel** —expresión regular y exigencia de «tabla no vacía» distintas de las de las dos guardias de origen, y una línea de `Where` que no filtraba nada—, justo el riesgo de extraer un ayudante: cambiar lo que se mide sin cambiar la aserción. Se volvió a escribir **replicando la lectura previa** (misma expresión, misma exigencia) y la mutación que la ronda marcó como superviviente muerde en aislamiento.

## [2026-09-29] - Hito 276: El Aparato de Prueba, Repartido: Una Capa por Afirmación y Cada Cosa en su Fichero

### 🎯 El encargo
«Seis pases de arreglos han ido dejando su aparato de prueba amontonado en los dos ficheros que ya lo llevan todo —el sondeo del host y el panel del inspector— y hay afirmaciones que hoy se prueban hasta tres veces (sonda, guardia de xunit y mutación) sin que quede claro cuál es su casa. Reordena sólo el trabajo de este hilo: da a cada afirmación una sola capa y el fichero que le corresponde, saca de los ficheros grandes lo que sea una preocupación aparte, y tira lo que los seis pases dejaron superseded —ayudantes que ya nadie llama, guardias que sólo repiten una aserción, referencias muertas—, sin cambiar ni una línea de comportamiento del producto.»

### 🔬 El diagnóstico (medido antes de tocar nada)
- **`RuntimeSelfCheck.cs`: 3324 líneas** — los cuatro modos (base, control-bar, ajustes, diálogos) más un modo base cuyo recorrido entero vivía dentro de un `Inspect` de 638 líneas.
- **`NodeInspectorPanel.xaml.cs`: 1485 líneas** — las últimas **227** eran la superficie de observación (accesos por ancla, censos de lo materializado, sondas de estado) pegada al final del fichero que construye la ficha, sin ninguna frontera entre lo que la aplicación usa y lo que sólo la mide.
- **Una afirmación, tres capas sin casa declarada**: el «Probar» (sonda + guardia + mutación), la Telemetría (ídem), las pestañas (sonda + guardia)... y, dentro de la MISMA capa, literales repetidos: `InspectorPanel_ShouldSeparateInputsAndOutputs_WithParityOfData` repetía las dos aserciones de colecciones del caso de las pestañas; `TheUnoHost_ShouldExposeTheCanonicalExecuteCommand_AsAnObservableChannel` (barra de control y franja de estado) vivía en la guardia del panel.
- **Muertos de los seis pases**: `_scrollPanes` (declaración y dos `.Add`, nadie lo leía), `FrameInspectorSplitter`/`FrameInspectorColumn`, `ParameterControlIds`/`ActionControlIds`.

### 🧱 La estructura resultante (una capa, una casa)
| Capa | Casa | Qué es |
| :--- | :--- | :--- |
| **La sonda** (mide el comportamiento y afirma) | `SelfCheckFrame.cs` (nuevo, 81) · `SelfCheckPanels.cs` (nuevo, 257) · `SelfCheckUia.cs`, orquestados por `RuntimeSelfCheck.cs` (**3079**, era 3324) | Cada preocupación recibe el **comprobador** de quien la llama: la sonda escribe `[OK]`/`[FALLO]` y decide el veredicto, y las preocupaciones sólo miden. El orquestador queda como cáscara: los cuatro puntos de entrada, el `Inspect` que ordena y el cinturón de medida compartido (recorrer el árbol visual, describir un fallo). |
| **El instrumento** (mide y devuelve, nunca juzga) | `NodeInspectorPanel.Probes.cs` (nuevo, 280) | La otra mitad (`partial`) del panel: la superficie de observación de la ficha. Su cabecera declara el contrato —recorren el estado por el MISMO camino del usuario y lo **restauran**; devuelven medidas, no veredictos— y su sitio es ése: si un miembro pasa a usarlo la aplicación, se va al fichero de la vista. |
| **La guardia** (fija la regla en el árbol de pruebas y es testigo de su mutación) | `UnoInspectorPanelGuardTests` (383) · `UnoInspectorTelemetryGuardTests` · `UnoSelfCheckLayoutGuardTests` (nuevo, 3 casos) · el resto de `Uno*GuardTests` | Cada caso cita el fichero donde vive lo que vigila (la vista, el instrumento o la sonda de los paneles), no «el sondeo» en general. La guardia nueva fija la FORMA: cada preocupación en su archivo, la vista sin su instrumento, y el instrumento sin veredictos. |
| **La mutación** (demuestra que muerde) | `mutations/*.json` | Sin cambios: las 103 declaradas siguen apuntando a código vivo. |

### 🧁 Lo que se tiró (superseded)
`_scrollPanes` y sus dos escrituras · `FrameInspectorSplitter` y `FrameInspectorColumn` · `ParameterControlIds` y `ActionControlIds` · las dos aserciones duplicadas del caso de Entradas/Salidas (**el caso sigue**, sin repetir lo que ya dice el de las pestañas) · el caso del canal de ejecución se **mudó** a la guardia de la barra de control, que es su sujeto.

### ✅ Validación
| Prueba | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** (50 avisos, eran 54 al empezar el tramo) |
| Las **cuatro sondas** del host | **EXIT 0**: `-SelfCheck` **97 `[OK]` · 0 `[FALLO]` · VERIFICADO**, control-bar **42**, settings **18**, dialogs **51** — la misma cuenta, el mismo veredicto y los mismos textos de comprobación (el código se movió entero, sin reescribir una línea) |
| Suite completa | **1972 superadas + 1 omitida de 1973, 0 errores** (2 m 33 s; antes 1969 + 1: +3 del guardián de la forma) |
| Las mutaciones del hilo | `boton-probar-que-se-ofrece-sin-nodo` **MUERDE (26,6 s)** y `seccion-de-telemetria-que-se-ofrece-en-vacio` **MUERDE (26,1 s)**: testigo rojo, control verde, árbol restaurado por bytes |
| Las 103 mutaciones declaradas | los **112 reemplazos** siguen resolviendo contra el árbol (comprobado uno a uno antes de mutar: mover el instrumento no dejó ninguna apuntando a texto ausente) |
| `mutations/COVERAGE.md` | regenerado por su guardia: **103 declaradas · 15 de 17 subsistemas · 17 de 46 guardias** |

### ⚠️ Fronteras declaradas
1. **Sin cambio de comportamiento**: el reorden movió código (el diff es una mudanza, no una reescritura); las cuatro sondas dan la misma cuenta con el mismo veredicto, y el suite entero sigue verde.
2. **La guardia de forma no tiene mutación**: `UnoSelfCheckLayoutGuardTests` fija la forma y nadie ha demostrado que muerda. Se declara aquí en vez de dejarlo caer: el censo de `COVERAGE.md` no la cuenta (sólo cuenta las guardias que auditan el árbol con `SourceTree`/`TestRepositoryLocator`/`TestSuiteIndex`), así que este apartado es su registro.
3. **Lo que NO se movió, y por qué**: los otros tres modos (control-bar, ajustes, diálogos) y el cinturón de medida compartido siguen en el orquestador — son de hitos anteriores y el encargo era reordenar el trabajo de este hilo; y `NodeToolboxPanel` conserva su superficie de observación dispersa porque no es uno de los dos ficheros que lo llevaban todo (si crece, la convención es la misma: una mitad `*.Probes.cs`).
4. **La convención queda escrita donde se lee**: en la cabecera de `RuntimeSelfCheck` (el reparto y las capas), en la de `NodeInspectorPanel` (dónde vive su instrumento) y en la del propio instrumento (el contrato con el sondeo).

## [2026-09-29] - Hito 275: La Telemetría del Nodo: qué Era y Dónde Vive

### 🎯 El encargo
«Queda un hueco nombrado en la ficha del inspector: la sección de Telemetría del nodo no llega a montarse, y antes de tocarla hay que decir con pruebas qué es —una superficie que se ofrece y no pinta nada, o una capacidad que el host de escritorio tiene y éste perdió— mirando el panel de escritorio, el catálogo de nodos y las guardias que ya existen, porque de la respuesta depende el arreglo. Resuélvelo sin inventar capacidad: si la fuente de esas medidas ya existe y el camino es de sólo lectura, móntala como la monta el escritorio; si no existe, quita de la ficha la superficie que se ofrece en vacío junto con sus anclas, sin dejar secciones fantasma ni rótulos huérfanos. Pruébalo con el ratón sobre la aplicación abierta —abrir el inspector, recorrer la sección y los controles que ofrezca— y deja la sonda o guardia que habría cazado una sección vacía.»

### 🔬 Qué era (con las pruebas a la vista)
**Una capacidad que el ESCRITORIO tiene y este host perdió** — y que este host ya tenía construida y sin montar.
1. **El escritorio sí la monta**: su ficha tiene una pestaña de Telemetría (`Inspector_TabTelemetry`) cuyo cuerpo es una tarjeta de métricas de ejecución leída del MISMO `InspectedNode.CurrentStats` — `FileFlow.App/Views/NodeInspectorPanelView.axaml`, su tercera pestaña.
2. **La fuente existe y el camino es de sólo lectura**: `NodeViewModel.CurrentStats` es el agregado que escribe el MOTOR (`UpdateTelemetryStats`) y `ExecutionStatusText` es la propiedad localizada del view model portable. La ficha del host ya leía esas dos fuentes en cinco filas… y las rellenaba para nadie.
3. **Lo que faltaba no era la fuente ni el cálculo: el MONTAJE.** El montaje que tenía en el cuerpo de la ficha (antes de las pestañas) se perdió al entrar éstas: desde entonces `_telemetryHeader`, `_resetMetricsButton` y `_telemetryRows` se construían, se localizaban y se rellenaban sin entrar al árbol. **Ninguna guardia lo decía**: la que existía medía que las filas se construyeran (cableado), no que se ofrecieran.
4. **El «Vaciar métricas» NO es una capacidad del escritorio**: su pestaña de telemetría no ofrece borrar nada y el reinicio del comando del VM portable (`ResetNodeMetricsCommand`) no lo monta **ninguna** vista del producto. Dibujarlo aquí sería capacidad nueva de ESTE host → **no se monta**, y su clave quedó huérfana: se retiró de los dos diccionarios junto al botón.
5. **El catálogo de nodos no guarda ninguna superficie de telemetría**: el dato que la ficha enseña lo produce el motor durante una ejecución (`ProcessedCount`, latencia media, tiempo total y pico de memoria del nodo, más su estado).

### 🧱 Lo construido (cada pieza donde viven sus iguales)
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/NodeInspectorTelemetrySection.cs` (nuevo, ~175 líneas) | La sección como control propio (hermano de los demás controles del host, no una pila más dentro de la ficha): cinco filas —estado, procesados, latencia media, tiempo total y pico de memoria— leídas del nodo, con la clave del diccionario de cada rótulo y el **ancla del valor** (`InspectorTelemetry_Status`…`_PeakRam`) para que la observación externa lea cada medida; se ata al nodo por `Bind` (suelta el anterior), lo sigue por `PropertyChanged` —las medidas las escribe el motor mientras la ficha está abierta— y **sólo lee**: no cita `UpdateTelemetryStats` ni el comando de reinicio. Las filas se **materializan una vez** (`EnsureRows`) y refrescar es reescribir su texto (`CurrentValues`): el latido del motor reescribe las medidas hasta ~30 veces por segundo mientras hay una ejecución, así que reconstruir la fila en cada fotograma crearía y tiraría quince elementos por latido sin cambiar lo que se ve (el escritorio, con enlaces, tampoco las reconstruye). |
| `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` (**~90 líneas menos**) | La ficha la MONTA como una sección más: sexta entrada de `InspectorTabs` (`InspectorTabTelemetry`, la última, como en el escritorio) con su envoltorio `InspectorTelemetryScroll`; le pasa el nodo (`_telemetrySection.Bind(_inspected)`) y la relocaliza con el idioma. Y le quitó los fantasmas: `RebuildTelemetry`, `TelemetryRow`, `FormatBytes`, `TelemetryRowCount` y los tres campos que nunca se montaron, más la suscripción al nodo que sólo servía para rellenar filas invisibles. |
| `FileFlow.App.Uno/RuntimeSelfCheck.cs` | Una comprobación nueva: la sonda recorre la cadena entera (pestaña declarada con su ancla, cuerpo dentro del host de paneles con la sección por contenido, la conmutación que la deja **visible y sola**, las filas pintadas y el **ida y vuelta al nodo** —sin nodo no queda ni una fila y al reatarlo se vuelven a materializar las suyas—), con el detalle de las cinco medidas y **cuántas salieron en blanco** (una fila en blanco no es una medida). 97 `[OK]`. |
| `FileFlow.Tests/Unit/App/UnoInspectorTelemetryGuardTests.cs` (nuevo, 2 casos) | La sección (de dónde salen sus medidas, sus cinco rótulos, sus cinco anclas, que se queda sin filas sin nodo, que **no vuelve a construir la fila que ya está** y la **aserción negativa** de que no escribe) y el montaje (el panel la declara, la monta, le pasa y le quita el nodo, la relocaliza, la sonda mide el ida y vuelta al nodo, y los fantasmas no vuelven). El caso de telemetría del panel se mudó aquí. |
| `mutations/seccion-de-telemetria-que-se-ofrece-en-vacio.json` (nueva) | **MUERDE (28,7 s)**: testigo rojo (el montaje) y control verde (la sección). `COVERAGE.md` regenerado por su guardia: **103 declaradas · 15 de 17 subsistemas · 18 de 46 guardias**. |
| Los dos diccionarios del host | `Uno_InspectorResetMetrics` fuera (su botón no se monta): sin rótulos huérfanos, y la paridad de claves de los dos idiomas intacta. |

### ✅ Validación (medida)
| Prueba | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** (54 avisos preexistentes) |
| `.\run-uno.ps1 -SelfCheck -NoBuild` | **97 [OK] · 0 [FALLO] · VERIFICADO** (96 + 1), con las **seis** secciones de la ficha con caja propia dentro del panel (300x1081: `InspectorTabTelemetry=120x32@(125,93)`) y la medida `5 filas (0 en blanco), montada=True, mostrada=True, con nodo=5 sin nodo=0` |
| la sonda **muerde** | montando un cuerpo vacío → `[FALLO] la sección de Telemetría está montada en su pestaña, se muestra sola y PINTA sus filas (filas: 5, en blanco: 0) — montada=False, mostrada=True`, veredicto **FALLOS**; repuesta → 97 `[OK]` |
| `.\mutate.ps1 -Name seccion-de-telemetria-que-se-ofrece-en-vacio` | **MUERDE** (testigo 1 con error / control 1 superado; árbol restaurado por bytes, recompilado y verificado) |
| Suite completa | **1969 superadas + 1 omitida de 1970, 0 errores** (2 m 34 s; antes 1968 + 1) |
| Sondas del host (las otras tres) | control-bar **42**, settings **18**, dialogs **51** `[OK]`, EXIT 0, VERIFICADO |
| Ratón — **sin nodo** | en el arranque la ficha está abierta sin nodo («Selecciona un nodo para inspeccionarlo.»): las cinco anclas **no están** en el árbol y la tira de secciones tampoco — la sección no se ofrece sin nodo |
| Ratón — **con nodo y la sección abierta** | seleccionando la tarjeta «Folder Source» y pulsando la pestaña «Telemetría»: aparecen **las cinco anclas** con las medidas del nodo, `En espera | 0 | 0,0 ms | 0,0 ms | —` |
| Ratón — **las medidas son del MOTOR** | pulsando «▶ Ejecutar Flujo» con la sección a la vista, las filas se actualizan **en vivo**: `Completado | 1 | 10,1 ms | 10,1 ms | 164,7 KB` (otra corrida: `11,1 ms | 146,9 KB`) |
| Ratón — **sigue al nodo inspeccionado** | seleccionando «Optimizador de Imágenes» el título de la ficha y las filas pasan a ser las SUYAS (sus ceros, sin medidas de nadie más) |
| Ratón — **qué controles ofrece** | ninguno propio: en el árbol no queda ningún «Vaciar métricas»; los botones del panel son su cabecera (Probar, Cerrar) y su tira de secciones |

### ⚠️ Fronteras declaradas (lo que NO quedó demostrado)
1. **Cierra el hallazgo 4 del hito 274** (la telemetría construida y sin montar) y su frontera: la ficha del host ya enseña la sección, con su fuente intacta.
2. **La sección no lleva rótulo propio**: el de su pestaña («Telemetría») es el suyo. El escritorio sí repite un encabezado dentro de su tarjeta («Métricas de ejecución»); copiarlo aquí habría pedido una clave nueva para decir dos veces lo mismo.
3. **El vaciado de métricas sigue sin puerta en los DOS hosts**: `ResetNodeMetricsCommand` y su clave (`Metrics_ResetNodeMetrics`) existen sin que ninguna vista los monte. Es deuda declarada y no se arregla aquí: montarla sería capacidad nueva de un host que el escritorio no ofrece.
4. **De las cinco filas, sólo el VALOR va anclado** (el contenedor de la fila no materializa en el árbol de accesibilidad): la observación externa lee las medidas por `InspectorTelemetry_*`; los rótulos se leen por posición.
5. **La vuelta al nodo anterior no se pudo recorrer con el puntero**: **un clic en el lienzo vacío NO vacía la ficha** (medido: el título sigue nombrando al último nodo inspeccionado), así que el sentido B→A de la selección no se alcanzó con dedos; lo cubre la **sonda en proceso** (`con nodo=5 sin nodo=0`: sin nodo no queda fila y al reatar el suyo se vuelven a materializar).
6. **El instrumento**: la identidad del nodo inspeccionado se leyó por el **título de la ficha** (es el nombre del nodo, medido); el puntero inyectado entrega pulsaciones pero **algunas no registran** (en la misma corrida seleccionaron «Folder Source» y «Optimizador de Imágenes» y falló el tercer clic, con la tarjeta a la vista), mientras que los clics de la barra, del cajón y de la tira de secciones sí llegaron siempre; y un clic sobre una tarjeta ocluida no selecciona.

## [2026-09-29] - Hito 274: El «Probar» que Mentía: lo que la Ficha Ofrece en Cada Estado

### 🎯 El encargo
«Arregla el defecto vivo que dejó la auditoría: con la ficha abierta y **ningún nodo seleccionado**, «Probar» se muestra habilitado y al pulsarlo **no ocurre absolutamente nada**; y esa misma clase —control alcanzable sin trabajo que hacer— revísala en los demás estados del panel (sin selección, nodo sin acciones, sin snapshots, sin diff, sin parámetros), porque **el fallo no es del botón sino de la condición que decide qué se ofrece** en cada estado. No añadas capacidad nueva: donde el control no tiene nada que hacer **no debe ofrecerse**, y si ya existe una condición de visibilidad, esa manda y se corrige **donde vive**, no con un parche en el manejador del clic. Pruébalo con el ratón sobre la aplicación abierta en todos esos estados y deja la guardia o sonda que habría cazado un botón inerte.»

### 🔬 Lo que encontró la medida
1. **El botón no era el defecto: su condición.** `_testButton` se construía siempre y entraba en la cabecera, y `UpdateVisibility()` —el único sitio donde vive el estado de la ficha— conmutaba `Visibility`, `_emptyText` y `_body` y **nunca el botón**. Sin nodo, el comando canónico del núcleo encontraba `InspectedNode == null` y volvía: la pulsación llegaba y el efecto era **ninguno**. La ficha nace abierta y sin selección (`MainWindow`: `NodeInspector. IsOpen = true` en la puesta en marcha), que es justo el estado donde el botón mentía.
2. **La guardia medía el cableado, no la oferta.** `HasWiredTestButton()` comprueba que el botón exista, cante su `AutomationId` y apunte al comando del núcleo —las tres, ciertas—, así que el botón inerte pasaba la sonda. Es la lección que este hito deja escrita: **una guardia que mide que algo exista no mide que algo sirva**.
3. **El encabezado de PARÁMETROS no seguía a sus editores.** El bloque de ACCIONES se colapsa sin acciones desde el 269; `_paramsHeader` se dibujaba siempre. (Su caso vacío **no es alcanzable** hoy: los nodos de producción declaran al menos un parámetro y `BuildParameterRow` no descarta ninguno — medido añadiendo cinco tipos desde el cajón con el ratón: 1–2 parámetros cada uno.)
4. **La TELEMETRÍA del nodo está construida y nunca montada** (hallazgo de este pase, declarado sin arreglar): `_telemetryHeader`, `_resetMetricsButton` y `_telemetryRows` se crean, se localizan y se rellenan (`RebuildTelemetry`: cinco filas desde `CurrentStats`), pero **ninguno entra al árbol**. El montaje que tenían (`bodyStack.Children.Add(_telemetryRows)`) desapareció al entrar las pestañas, así que la ficha no tiene la sección que el escritorio sí pinta (su pestaña de Telemetría). Quedan un guardia que defiende código muerto (`InspectorPanel_ShouldShowTelemetryFromTheNodeViewModel`) y una fila de la tabla de paridad que declara una cobertura que no existe.

### 🧱 Lo que se construyó
| Pieza | Qué es |
| :--- | :--- |
| `NodeInspectorPanel.xaml.cs` — `UpdateVisibility()` | **La condición de estado, en un sitio**: `isOpen` + `hasNode` deciden cuerpo, texto de «sin selección» y **«Probar»**. Con nodo, el botón se dibuja y está habilitado; sin nodo, **no se ofrece**. No se corrige deshabilitando: un botón deshabilitado sigue ofreciéndose. |
| `NodeInspectorPanel.xaml.cs` — constructor | La ficha arranca en un estado **decidido**, no en el de por defecto: `UpdateVisibility()` antes de que llegue el view model (antes, entre la construcción y el VM se enseñaban a la vez el cuerpo y el texto de vacío, con el botón de la cabecera ya dibujado). |
| `NodeInspectorPanel.xaml.cs` — `RebuildParameters()` | `_paramsHeader` se colapsa cuando no hay editores (la regla del bloque de acciones) y la rama sin nodo lo colapsa también. |
| `NodeInspectorPanel.xaml.cs` — superficie de sonda | `ProbeTestButtonOffer(node)`: recorre los dos estados **por la propiedad del VM** (`InspectedNode = null` + `IsOpen = true`, y luego el camino de la selección del lienzo) y lee si el botón se ofrece; restaura lo que había. `ParametersHeaderOffered`. |
| `RuntimeSelfCheck.cs` | **Tres comprobaciones nuevas**: la oferta sin nodo (falso) y con nodo (verdadero), y el encabezado de Parámetros contra la cuenta de editores materializados. 96 `[OK]` (eran 93). |
| `UnoInspectorPanelGuardTests` | Dos casos nuevos: la oferta atada a la condición de estado (y la aserción negativa de que no se arregla deshabilitando) y el encabezado de Parámetros atado a sus editores. |
| `mutations/boton-probar-que-se-ofrece-sin-nodo.json` (nueva) | **MUERDE (39,3 s)**: testigo rojo (`InspectorPanel_ShouldOfferTheTestButton_OnlyWithAnInspectedNode`) y control verde (el caso del cableado). `COVERAGE.md` regenerado por su guardia: **102 declaradas · 15 de 17 subsistemas · 17 de 46 guardias**. |

### ✅ Validación (medida)
| Prueba | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** (54 avisos preexistentes) |
| `.\run-uno.ps1 -SelfCheck -NoBuild` | **96 [OK] · 0 [FALLO] · VERIFICADO** (93 + 3) |
| la sonda **muerde** | quitando la condición → `[FALLO] el «Probar» NO se ofrece sin nodo inspeccionado: no hay nada que probar, así que no se dibuja`, veredicto **FALLOS**; repuesta → 96 `[OK]` |
| `.\mutate.ps1 -Name boton-probar-que-se-ofrece-sin-nodo` | **MUERDE** (testigo 1 con error / control 1 superado; árbol restaurado por bytes, recompilado y verificado) |
| Suite completa | **1968 superadas + 1 omitida de 1969, 0 errores** (2 m 44 s; antes 1966 + 1) |
| Ratón sobre la app abierta — **sin selección** | `InspectorTestButton` **no está en el árbol UIA**; el clic en su hueco exacto de la cabecera (2756,348) **no abre nada**, la ficha sigue abierta con su texto de «sin selección» y no hay pestañas (el cuerpo está colapsado) |
| Ratón — **con nodo** | el botón aparece en la cabecera y pulsarlo **abre el picker** (`#32770 'Abrir'`, hwnd nuevo tras cerrar el anterior): está ofrecido y tiene trabajo; cancelado, no queda diálogo |
| Ratón — **las cinco secciones** | conmutan dejando visible **sólo** su cuerpo (374 px dentro de la ficha); en los estados vacíos **no hay un solo control** que ofrecer: 0 tarjetas de snapshot, 0 botones «Ver», 0 filas de diff |
| Ratón — **nodo sin acciones** | los tres nodos del flujo de ejemplo declaran 0 acciones: 0 botones y **sin encabezado «Acciones»** |

### ⚠️ Fronteras declaradas (lo que NO quedó demostrado)
1. **«Sin parámetros» no es alcanzable** con los nodos de producción (todos declaran al menos un parámetro) → la regla del encabezado se mide por su **invariante** (la sonda compara encabezado con editores; hoy: «10 editores, encabezado ofrecido») y por la guardia de fuente, **no** con el ratón.
2. **La Telemetría del nodo sigue sin montarse** (hallazgo 4): montarla es UI nueva y este encargo la prohíbe. Queda localizada —el montaje se perdió al entrar las pestañas, y el guardia que la «cubre» defiende código muerto— para el tramo que la reponga.
3. **Las acciones y los LED de la tarjeta del lienzo no tienen ancla de automatización** (sólo `NodeCardExpandToggle`): lo pulsado en la tarjeta se localizó por nombre/geometría, no por ancla estable. Deuda declarada (no tocada aquí).
4. **El puntero inyectado entrega pulsaciones, no movimientos** (heredado del 272): la selección de tarjeta y los clics se ejercen con el ratón; ningún gesto de arrastre.

## [2026-09-29] - Hito 273: Las Cinco Secciones de la Ficha con su Caja, y el Picker que Tiene Dueño

> **Nota de registro**: este tramo se cerró **sin entrada**; se escribe aquí al día siguiente del cierre con lo **medido hoy** sobre el mismo árbol (auditoría con ratón) y con lo que declaran su código y sus guardias. No se reescribe nada del 272.

### 🎯 El encargo
Devolver a la ficha del inspector la **paridad de secciones** con el escritorio sin que ninguna naciera inalcanzable, y dejar que los **pickers** del host (el explorar de una ruta, el diálogo de la prueba aislada) abrieran **con dueño** desde el clic de UI.

### 🔬 Lo que encontró la medida
1. **El `Pivot` repartía y no envolvía**: con la ficha en sus ~300 px lógicos, los cinco rótulos de sección se repartían el ancho, así que «Salidas» y «Diff» medían **rectángulo vacío** —fuera del alcance del ratón— y «Entradas» nacía recortada, sin scroll ni rueda que las alcanzara. El censo de declaración no lo veía: las cinco existían para el view model.
2. **El explorar de una ruta era síncrono**: el botón «…» de una fila de ruta llamaba a una vía pensada para hilos de fondo; desde el clic de UI (hilo de UI) el diálogo síncrono devuelve nulo. La variante asíncrona no existía en el view model portable.
3. **Los pickers se abrían sin dueño**: `COMException: Invalid window handle (0x80070578)` — un diálogo de WinRT abierto sin ventana propietaria.

### 🧱 Lo que se construyó
| Pieza | Qué es |
| :--- | :--- |
| `NodeInspectorPanel.xaml.cs` | El `Pivot` sustituido por `_tabStrip` (**`WrapPanel`**: envuelve) + `_paneHost`, con `_tabButtons`/`_tabPanes`, la tabla `InspectorTabs` (clave, texto de reserva y ancla) y `ShowTab(int)` como único conmutador (lo usan el clic y la sonda). Cada sección con su ancla (`InspectorTabParams`…`InspectorTabDiff`) y su envoltorio de scroll **con nombre** (`InspectorParamsScroll`…`InspectorDiffScroll`). |
| `NodeParameterViewModel` | `BrowsePathAsync()` y `BrowsePathAsyncCommand` — **a mano**: `[RelayCommand]` recorta el sufijo «Async» y colisionaría con `BrowsePathCommand`. |
| `UnoFileDialogService` | `RunOnUi` / `EnqueueOnUiAsync` toman un `Func<Window, …>` y `OwnPicker(picker, window)` da dueño al picker (`InitializeWithWindow.Initialize` + `WindowNative.GetWindowHandle`) |
| Guardias | `UnoPickerOwnershipGuardTests` (3 casos; **muerde** al quitar `OwnPicker`), las tuplas de la tira y `BrowsePathAsyncCommand` en `UnoInspectorPanelGuardTests`, y la sonda de la tira (`TabButtonBoxesForProbe`) exigiendo caja dentro de la ficha. |

### ✅ Validación (medida hoy, sobre este mismo árbol)
| Prueba | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** (54 avisos preexistentes) |
| `.\run-uno.ps1 -SelfCheck -NoBuild` | **93 [OK] · 0 [FALLO] · VERIFICADO** (antes del 274) |
| `-SelfCheckControlBar` / `-SelfCheckSettings` / `-SelfCheckDialogs` | **42** · **18** · **51** `[OK]` / 0 `[FALLO]`, EXIT 0 |
| Suite completa | **1966 superadas + 1 omitida de 1967, 0 errores** (2 m 46 s) — el rojo intermitente de `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` («flow_22_paralelismo_fork_join no entregó 'datos.csv'») **pasa en aislamiento**: carga, no producto |
| Ratón sobre la app abierta | las **cinco secciones** (cajas reales de **150x40** dentro del panel) conmutan dejando visible **sólo** su cuerpo; **«Guardar Flujo…»** abre su `#32770 'Guardar como'` (nombre por defecto `flujo.json`) y el «…» de una fila de ruta abre **`Seleccionar carpeta`**; el ciclo completo —escribir la ruta, guardar (**2063 bytes**), «Nuevo Flujo» + confirmar (0 tarjetas) y cargar de vuelta (**3 nodos**)— se ejercita entero |

### ⚠️ Fronteras declaradas
1. **Se cierra la frontera 2 del 272: NO era un defecto.** Elegir «English» en el desplegable de idioma de Ajustes **no reescribe los rótulos** (y no toca el disco); quien decide la cultura es **`Save()`**, y pulsar **«Guardar ajustes»** pasa la UI entera a inglés, escribe `en-US` y **cierra el panel** (medido). El selector del **cajón** sí aplica en caliente, por su propio camino. Queda escrito para que nadie «arregle» lo que funciona: el desplegable es una selección pendiente de guardar, no un conmutador en caliente.
2. **`_scrollPanes` es una lista muerta** (sólo recibe `.Add`) cuyo comentario dice que la audita la sonda: **no la audita nadie**. Se deja tal cual y se declara.
3. **La ficha del host Uno tiene cinco secciones y el escritorio cuatro bloques** (Parámetros, Snapshots, Telemetría): la paridad es de **datos y comandos**, no de composición — y la Telemetría del host está sin montar (hito 274, hallazgo 4).

## [2026-09-29] - Hito 272: El editor recupera su fila, las tiras que no cabían y el instrumento que faltaba

### 🎯 El encargo
«chequea a fondo la aplicacion y corrige los errorers que tiene actualmente sobretodo en la interfaz de usuario. haz todo sin intervencion humana.» — y el paso concreto de este tramo: **atacar primero el defecto crítico** (el `Workspace` sin su fila, pintado encima de la barra de control), devolver a cada zona su fila, y seguir el orden de la auditoría empezando por lo que impide usar la aplicación normalmente, **verificando con el ratón del sistema** y dejando el instrumento que faltaba para que la próxima vez la regresión salte sola.

### 🔬 Lo que encontró la medida
1. **La regresión crítica del hito 270**: `MainWindow.xaml` declaraba `<Grid x:Name="Workspace">` **sin** `Grid.Row`, así que la barra de control y el editor compartían la fila 0 y el editor —declarado después— se pintaba ENCIMA. La barra quedaba invisible e inalcanzable con el ratón: el menú y los ajustes no se podían abrir, y la franja inferior de la ventana quedaba en negro.
2. **Por qué nadie lo vio**: las 85 comprobaciones del sondeo en runtime pasaban en verde. Todas pulsan la barra **por método** (`ControlBar.Press`), no por puntero, y **ninguna medía dónde cae cada zona del marco**: se comprobaba que los controles existen, no que se puedan pulsar.
3. **Once de las quince categorías del cajón no se podían pulsar**: la tira de chips era un `StackPanel` horizontal dentro de una columna de 280 px; las que no cabían quedaban recortadas contra el borde (rectángulo vacío en el árbol de accesibilidad), sin scroll y con la rueda sin efecto.
4. **La pestaña «Actualizaciones» de Ajustes tampoco**: los seis rótulos pedían ~886 px lógicos contra los 772 del panel de 800, así que la última nacía recortada —y el censo de la sonda, que sólo miraba la declaración, seguía diciendo «seis secciones».
5. **El arrastre de las asas era una afirmación sin medida**: `PanelSplitter.DragBy` y `RememberedWidth` existían y **no los llamaba nadie** —ni la sonda ni el ratón—, aunque el comentario del propio archivo decía que los usaba el sondeo.
6. **El puntero inyectable: los botones sí, los movimientos no** (medido en este entorno): `SetCursorPos` + `mouse_event` entregan pulsación y suelta (los botones de zoom pasan de 100 % a 110 % y a 121 %; el engranaje abre Ajustes) pero **ningún movimiento**: 0 px de cambio por hover en seis controles (Ejecutar, Ajustes, Menú, buscador, asa e Inspector) a la vez que el pulsado sí pinta. El control lo confirma: la Calculadora de Windows se comporta igual (hover 0 px; el pulsado escribe el «7»), mientras que la **barra de tareas sí acusa el hover** (1.322 px). Es el reparto de Windows: la shell lee `WM_MOUSEMOVE` y las aplicaciones XAML leen `WM_POINTER`, que `SetCursorPos` no sintetiza.

### 🧱 Lo que se construyó
- **`FileFlow.App.Uno/MainWindow.xaml`**: `<Grid x:Name="Workspace" Grid.Row="1">` — la barra recupera su fila, el editor la suya y la franja de estado la tercera.
- **`FileFlow.App.Uno/Controls/WrapPanel.cs`** (nuevo, ~85 líneas): el panel que **parte la línea** con el ancho disponible y la separación declarada. Panel propio y no `ItemsWrapGrid` porque aquél es un panel de elementos virtualizados (para contenedores de `ListViewBase`) y aquí los hijos ya están materializados.
- **`NodeToolboxPanel.xaml`**: la tira de chips pasa a `WrapPanel` → las quince categorías se dibujan (medido: 15 de 15 con caja, cinco filas).
- **`SettingsPanel.xaml`**: la tira de secciones pasa a `WrapPanel` → las seis pestañas se dibujan (cinco en la primera fila, «Actualizaciones» en la segunda).
- **`MainWindow.xaml.cs`**: la superficie interna que la sonda mide —las tres zonas del marco y las dos asas con sus columnas— para poder medir la geometría sin abrir la ventana a nadie más.
- **`RuntimeSelfCheck.cs`** (4 guardias nuevas en el modo base, 1 en el censo de Ajustes, 1 en el cajón):
  - **el marco**: las tres zonas con caja propia, la barra ARRIBA y el editor DEBAJO, las cajas disjuntas;
  - **las asas**: el arrastre por su mismo camino (`DragBy`), el tope que no deja al lienzo por debajo de su mínimo y la vuelta al ancho de partida;
  - **las chips**: cada categoría que declara el view model con caja DENTRO del cajón;
  - **el censo de secciones** de Ajustes exige ahora la caja de cada pestaña dentro del panel.

### ✅ Validación (medida)
| Prueba | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx -p:FileFlowUnoHost=true` | **0 errores** (54 avisos preexistentes) |
| `.\run-uno.ps1 -SelfCheck -NoBuild` | **92 [OK] · 0 [FALLO] · VERIFICADO** (85 + 7 nuevas) |
| La guardia del marco **muerde** | quitando `Grid.Row="1"` → `[FALLO] la barra queda ARRIBA y el editor no la tapa (barra hasta y=603, editor desde y=0)` y veredicto **FALLOS**; repuesta → 92 [OK] |
| `-SelfCheckSettings` | **EXIT 0**; censo con cajas: `174x32@(15,77) … 120x32@(15,115) 124x32@(141,115)` |
| `-SelfCheckControlBar` / `-SelfCheckDialogs` | **42 [OK] / 0 [FALLO]** · **51 [OK] / 0 [FALLO]** |
| Suite completa | **1963 superadas + 1 omitida de 1964, 0 errores** (2 m 58 s) |
| Ratón del sistema (sobre la app abierta) | el engranaje **abre Ajustes**; «Menú» **abre el cajón** (28 entradas: Nuevo, Cargar, Guardar, Personalizar Tema, Ajustes, Inspector); el botón del Inspector **conmuta** la ficha; el zoom va **100 % → 110 % → 121 %**; pulsar las chips antes recortadas **«Documents» y «Logic»** filtra el catálogo; pulsar la pestaña antes recortada **«Actualizaciones»** enseña sus controles |

### ⚠️ Fronteras declaradas (lo que NO quedó demostrado)
1. **El arrastre de las asas con el ratón del sistema sigue sin poder ejercerse aquí** y no por el producto: en este entorno el puntero inyectado entrega pulsaciones pero no movimientos (medido arriba, con su control). El camino del arrastre queda cubierto **por la sonda** (`DragBy`), no por dedos reales; si un día el entorno puede inyectar movimientos, la guardia que ya existe medirá el mismo camino con puntero.
2. **Candidato abierto en los desplegables de Ajustes** (no confirmado, ninguna pieza tocada por él): en *Ajustes → Apariencia*, elegir «English» en el desplegable de idioma —con puntero (cuatro intentos) y con el patrón de automatización— dejó **todos los rótulos en español** (el botón «Guardar ajustes» y la barra), mientras el sondeo de Ajustes —que mueve el mismo desplegable por método, en su propio proceso— mide que esa selección **sí** cambia la cultura y reescribe los textos. No se pudo leer la selección del control por UIA (no expone el patrón de selección), así que queda como medir con el view model a la vista.
3. **El coste de partir las tiras**: las quince chips ocupan cinco filas (~155 px de alto del cajón, medido: de y=460 a y=611 en la ventana). Es el precio de que las quince se puedan pulsar en una columna de 280 px; el usuario puede ensanchar el cajón (asa 180–480) para reducir filas.
4. **Los punteros del archivo frío a las capturas manuales borradas** (petición anterior): el texto de cada medida se conserva entero, la captura no.

## [2026-09-28] - Hito 271: Configuración Integral del IDE y Entorno de Desarrollo para Uno Platform

### 🎯 El encargo
«este proyecto usa una interfaz de usuario basada en uno platform pero parece que el entorno de desarrollo y el ide no estan bien configurados. configura todo para que funcione bien.»

### 🔬 Lo que encontró la medida
1. **El IDE carecía de configuración para Uno Platform y C#**:
   - `.vscode/settings.json` contenía únicamente `"dotrush.roslyn.projectOrSolutionFiles": []` (un array vacío que dejaba a DotRush sin solución). No existía definición de solución por defecto (`dotnet.defaultSolution`), provocando que C# Dev Kit cargase `FileFlow.slnx` (el host Avalonia) en lugar de `FileFlow.Uno.slnx` (el host Uno Platform sin dependencias de escritorio).
   - No existía `.vscode/launch.json` para depuración con F5 en VS Code ni `.vscode/tasks.json` para tareas de compilación, ejecución y self-check.
   - No existía `.vscode/extensions.json` recomendando la extensión oficial de Uno Platform (`unoplatform.vscode`) ni las herramientas de C# Dev Kit.
   - La extensión de Uno Platform para VS Code no estaba instalada en el sistema; se instaló `unoplatform.vscode` v0.26.1.
2. **Defecto en los scripts lanzadores de PowerShell (`run-uno.ps1` y `run-uno-fast.ps1`)**:
   - Al invocar los scripts con parámetros de sondeo (como `-SelfCheck`), la concatenación `@("--selfcheck") + $AppArgs` cuando `$AppArgs` es `$null` creaba un array con un elemento nulo (`@("--selfcheck", $null)`).
   - PowerShell fallaba en `Start-Process` con la excepción: `Start-Process : No se puede validar el argumento del parámetro 'ArgumentList'. El argumento es null o está vacío.`
   - Se refactorizó la recolección de argumentos usando `List[string]` y comprobación explícita de `IsNullOrWhiteSpace`, eliminando el fallo y garantizando que el paso de parámetros a `Start-Process` sea limpio tanto con argumentos como sin ellos.
3. **Optimización de `.gitignore` y estandarización con `.editorconfig`**:
   - Se ajustó `.gitignore` para versionar la configuración esencial del IDE (`.vscode/settings.json`, `tasks.json`, `launch.json`, `extensions.json`) ignorando temporales.
   - Se introdujo `.editorconfig` con directivas precisas de sangrado para C# (4 espacios), XAML/XML/JSON (2 espacios), codificación UTF-8 y saltos de línea CRLF.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `.vscode/settings.json` | Configura `FileFlow.Uno.slnx` como solución principal para C# Dev Kit, DotRush y OmniSharp, asocia archivos XAML/AXAML/SLNX a XML, anidamiento de ficheros (`*.xaml` -> `*.xaml.cs`, `*.axaml` -> `*.axaml.cs`) y exclusión de directorios `bin/` y `obj/` en búsquedas. |
| `.vscode/launch.json` | Perfiles de depuración `coreclr` listos para F5: ejecución normal, ejecución sin depuración y modos de autorrevisión (`--selfcheck`, `--selfcheck-dialogs`, `--selfcheck-controlbar`, `--selfcheck-settings`). |
| `.vscode/tasks.json` | Tareas de compilación (`build-uno`, `build-uno-release`), ejecución (`run-uno`, `run-uno-fast`), pruebas unitarias (`test-all`) y sondeos automatizados (`selfcheck-uno*`). |
| `.vscode/extensions.json` | Recomendaciones de extensiones clave: `unoplatform.vscode`, `ms-dotnettools.csdevkit`, `ms-dotnettools.csharp` y `ms-dotnettools.vscode-dotnet-runtime`. |
| `.editorconfig` | Estándar de codificación unificado para el IDE y herramientas de análisis. |
| `run-uno.ps1` / `run-uno-fast.ps1` | Corrección del paso de argumentos en `Start-Process`, asegurando ejecución confiable de la app y sus sondeos. |
| Extensión `unoplatform.vscode` | Instalada la extensión oficial v0.26.1 de Uno Platform en el entorno VS Code. |

### 🛡️ Cómo se verificó
1. **Compilación hermética Uno**: `dotnet build FileFlow.Uno.slnx -p:FileFlowUnoHost=true` → **0 errores**.
2. **Sondeo en runtime del lienzo**: `.\run-uno.ps1 -SelfCheck -NoBuild` → **EXIT 0 · 85 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
3. **Sondeo de paneles de nodo y diálogos**: `.\run-uno-fast.ps1 -SelfCheckDialogs` → **EXIT 0 · 51 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
4. **Sondeo de barra de control y cajón**: `.\run-uno-fast.ps1 -SelfCheckControlBar` → **EXIT 0 · 42 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
5. **Sondeo de ajustes (tema e idioma)**: `.\run-uno-fast.ps1 -SelfCheckSettings` → **EXIT 0 · 18 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
6. **Guardias de arquitectura Uno**: `UnoHermeticBuildGuardTests` y `UnoNodeDialogsGuardTests` → **21 superadas de 21**.
7. **Suite completa de pruebas**: `.\test.ps1` → **1963 superadas, 1 omitida, 0 fallos** en 178 s.

## [2026-09-28] - La Bitácora se Divide: el Archivo Frío y la Ventana Viva

### 🎯 El encargo
«hay muchos archivos auxiliares .md que o bien tienen un tamaño muy grande o ya están obsoletos o no tienen ya uso.
Mueve estos archivos ya viejos o muy grandes al archivo `docs/history`. Por ejemplo `PROJECT_WALKTHROUGH.md` ha
crecido demasiado usando demasiado contexto y tokens: comprime y resume las partes más antiguas o sin utilidad
actual, dejando solo las partes relevantes. Todo lo antiguo pásalo al archivo.»

### 🔬 Lo que encontró la medida
1. **La bitácora pesaba 970 KB en 7.689 líneas y 207 entradas** (2026-09-10 → 2026-09-28). `AGENTS.md` obliga a
   consultarla **al empezar cada sesión**, así que el coste de arrancar crecía con la **historia** y no con el
   trabajo por hacer: el mismo protocolo que la hace útil la volvía un impuesto.
2. **El resumen de sesión pesaba otros 579 KB** con **162 bloques de hito** (109 → 270) bajo el rótulo «Hito más
   reciente»: el rótulo describía una ventana que llevaba decenas de hitos sin recortarse.
3. **Los planes de las rebanadas 3 y 4 estaban cerrados** —el de la 4 lo dice en su propia cabecera, «Cerrada en el
   hito 236»— y el plan de rediseño visual es una **propuesta del tramo 1**, entregada hace veintiséis tramos.
4. **`docs/user_guide.md` era un manual superado**: el que el instalador copia y el que `build-pdf-manual.ps1`
   regenera es `docs/manual_de_usuario.md`; aquél era una versión corta que además era el destino del índice de
   `docs/README.md` y de las instrucciones de Copilot.
5. **Lo pesado de verdad ya estaba fuera de git**: las capturas de las sesiones manuales (cientos de PNG de 200 a
   400 KB) y los `__pycache__` los excluye el `.gitignore` desde el hito 265. Lo que quedaba dentro del control de
   versiones era **texto**: 108 ficheros `.md`.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `docs/PROJECT_WALKTHROUGH.md` | La **ventana viva**: cabecera con las reglas de lectura, el **índice del archivo frío** y las entradas del tramo en curso (hitos **255 a 270**). De **970 KB a 138 KB**. |
| `docs/history/2026-09-28_walkthrough_2026-09-26_a_2026-09-27.md` | Archivo frío: hitos **225 – 254** (1.333 líneas). |
| `docs/history/2026-09-28_walkthrough_2026-09-23_a_2026-09-25.md` | Archivo frío: hitos **169 – 224** (2.450 líneas). |
| `docs/history/2026-09-28_walkthrough_2026-09-10_a_2026-09-22.md` | Archivo frío: hitos **118 – 168** (3.129 líneas). |
| `.antigravity/knowledge/session_summary.md` | El resumen de sesión se queda con los **hitos 256 – 270**; el resto (109 – 255) pasa a `knowledge/history/`, con el aviso de la cabecera apuntando a los dos volúmenes. |
| [`docs/history/2026-09-28_uno_canvas_plan_rebanada3.md`](history/2026-09-28_uno_canvas_plan_rebanada3.md), [`…_uno_panels_plan_rebanada4.md`](history/2026-09-28_uno_panels_plan_rebanada4.md) y [`…_ui_redesign_plan.md`](history/2026-09-28_ui_redesign_plan.md) | Los planes **cerrados**: las rebanadas 3 y 4 (el de la 4 lo dice en su cabecera, «Cerrada en el hito 236») y el rediseño visual del tramo 1. El de la rebanada 5 se queda vivo: es el único con fases pendientes (empaquetado y entrega). |
| [`docs/history/2026-09-28_user_guide_manual_es_obsoleto.md`](history/2026-09-28_user_guide_manual_es_obsoleto.md) | El manual de usuario superado por `docs/manual_de_usuario.md` —el que el instalador copia y regenera—, con sus dos enlaces vivos reapuntados. |
| [`docs/history/2026-09-28_flujo_test_legado.json`](history/2026-09-28_flujo_test_legado.json) | Un flujo de prueba de un formato viejo que vivía suelto en `docs/`: sus parámetros no los declara ningún nodo y su ruta es de otra máquina (lo dice el propio comentario que lo excluye de la validación del catálogo). |

### 🛡️ Cómo se verificó
- **Ninguna guardia lee estos documentos**: el barrido de `*.cs`, `*.ps1`, `*.yml` y `*.props` no encuentra ni una
  referencia a la bitácora, al resumen de sesión ni a los planes, así que moverlos no toca la suite. Lo que sí está
  atado por guardias (`mutations/COVERAGE.md`, `.agents/nodes_catalog.md`, `docs/api_reference.md`) **no se movió**.
- **Los tres cortes caen entre entradas**, no dentro: los tres empiezan con su `## [fecha] - …`, y la suma de líneas
  de los tres archivos más la ventana viva cuadra con el original.
- **Cada archivo frío lleva su cabecera** con su periodo, su rango de hitos y el porqué del corte.

### 🟠 Fronteras declaradas
- **El archivo no se resume: se mueve entero.** Reescribir lo viejo para «comprimirlo» lo convertiría en otra cosa y
  perdería las medidas que lo sostienen; lo que se comprime es **lo que se lee cada sesión**, no lo que se conserva.
- **El corte es por periodo**, y las fechas dentro del último tramo no son monotónicas —los hitos 118 a 168 se
  apilaron al final, desordenados—, así que el índice habla de **rangos de hito** y el archivo lo advierte.
- **Los enlaces de los archivos fríos no se reapuntan**: son registros fríos, y reescribir su historia para arreglar
  un enlace sería falsearla. Los enlaces de los documentos **vivos** sí se actualizaron.
- **Los PDF de los manuales (9,5 MB) siguen versionados**: son **entrada del instalador**
  (`build-installer.ps1` los copia y `build-pdf-manual.ps1` los regenera). Sacarlos del control de versiones es una
  decisión de empaquetado, no de limpieza, y queda declarada.
- **Las capturas de las sesiones manuales no se tocaron**: ya están fuera de git y son la evidencia de lo que se
  midió; borrarlas es irreversible y no lo pidió nadie.

---

## [2026-09-28] - Hito 270: El Botón del Nodo que Abre una Ventana del Escritorio Avisa, y los Paneles Laterales se Redimensionan

### 🎯 Objetivos y Alcance
El encargo, en una frase del usuario: «los botones en los nodos no parecen funcionar y los paneles laterales de inspector y catálogo de nodos no se pueden redimensionar. arréglalo». Dos mitades: (1) averiguar **qué** botón del nodo no hacía nada y por qué, y (2) hacer **redimensionables** el cajón de nodos y la ficha del inspector del host Uno, que tenían ancho fijo (la columna de 280 y el `Width="300"` de la ficha), como el escritorio los tiene (sus dos `GridSplitter` y el reparto 180–480 / 220–750).

### 🔬 Lo que encontró la medida (con puntero REAL, no programático)
1. **Los botones de la tarjeta SÍ respondían**. Con el puntero inyectado del instrumento de `docs/qa/qa_manual.py` (SetCursorPos + mouse_event, el que la sesión del 260 midió como «el contenido no reacciona»), hoy el host responde en todas las puertas del nodo, medido píxel a píxel: el LED del breakpoint se enciende en rojo, el del log se apaga, el conmutador despliega el panel, el «➕ Caso» de la tarjeta añade su puerto y el botón de la ficha abre el gestor de presets. La hipótesis de trabajo (el arrastre del lienzo robaba el puntero al pulsar un botón) **se descartó midiendo**: la traza del lienzo no recibe ni un `press` sobre un botón de la tarjeta —`ButtonBase` marca el gesto como manejado y el handler del editor no llega—.
2. **El botón que no hacía nada era el de la ventana del ESCRITORIO**. Sobre el nodo de script, pulsar «💻 Editor de Scripts...» con puntero real no abría nada, no avisaba y no dejaba más rastro que una línea en la consola del proceso («`[DesktopOnlySurface] «Estudio de Scripts» no se puede montar en este host…`»). La causa: `NodeViewModel.ExecuteCustomAction` construía el `NodeCustomActionContext` **sin el servicio de diálogos**, así que la costura del hito 268 —que declara la frontera por los diálogos de quien la abrió— caía al `NullDialogService` del Sdk. Los siete nodos con ventana del toolkit hacían su mitad (`(context as NodeCustomActionContext)?.Dialogs`) y **el teléfono no estaba puesto en la otra**: la frontera era cierta en el código y falsa de cara al usuario. El mismo hueco estaba en la puerta del **gestor de contraseñas** (`NodeParameterViewModel.OpenPasswordManager`).
3. **La frontera del 268 decía la verdad y no se había ejercido**, tal y como su propio apartado de fronteras declaraba: lo medido era la costura con un doble puesto a mano, y la guardia del 268 no mira quién construye el contexto. Un defecto así no lo caza ningún lint de texto (el nodo sigue declarando, la traza sigue escribiéndose y el sabor sigue siendo hermético).
4. **El aviso tampoco era visible para nadie más**. `UnoDialogService.ShowCoreAsync` mostraba su `ContentDialog` **fuera** del estado `UnoWindowService.ActiveDialog`, que es el que el propio host consulta antes de abrir otro modal (WinUI admite uno) y el que leen las sondas: el aviso se veía, pero el host no sabía que estaba ahí y una sonda no podía distinguir «se avisó» de «no pasó nada». Ahora se publica por `RunOwnedAsync`.
5. **El lanzador no esperaba a la aplicación**. `run-uno.ps1` / `run-uno-fast.ps1` lanzaban el host con `& $exePath` y PowerShell **no espera a las aplicaciones de GUI**: el script devolvía «exit 0» con el sondeo todavía corriendo y el informe a medio escribir —o el de la corrida anterior—. Se midió al leer un veredicto que no era de la corrida que se acababa de lanzar; ahora los dos lanzadores usan `Start-Process -Wait -PassThru` y heredan el código de salida de verdad.
6. **La sonda nueva tenía su propia trampa**: `WaitUntil` ya envuelve su condición en `Probe`, así que anidar un `Probe` dentro de él deja al hilo de UI esperándose a sí mismo y devuelve un «no» falso (medido en esta misma sonda: el texto del aviso aparecía en el informe como si se hubiera leído). La espera y la lectura quedan separadas.
7. **WinUI 3 no trae `GridSplitter` y `Border` está sellado**: el asa se escribe sobre `Grid` (lo que necesita del árbol es un `Background` opaco al puntero), con el reparto del escritorio y una cota más —el arrastre no puede dejar al lienzo por debajo de su mínimo—, porque el área de clic de las tarjetas se mide del árbol visual.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Core/ViewModels/NodeViewModel.cs` | El contexto del botón del nodo lleva el **servicio de diálogos del host** (`CoreDialogHost.ResolveDialogService()`, el mismo camino que la superficie declarada veinte líneas más abajo): la frontera del 268 llega al usuario. |
| `FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs` | La puerta del **gestor de contraseñas** del mismo hueco: su `_dialogService`, que ya tenía en la mano. |
| `FileFlow.App.Uno/Platform/UnoDialogService.cs` + `UnoWindowService.cs` | El aviso se muestra **publicándose** como el modal abierto (`RunOwnedAsync`): el segundo `ContentDialog` se detecta de verdad y la sonda ve el aviso. |
| `FileFlow.App.Uno/Controls/PanelSplitter.cs` (nuevo) | El **asa** del marco: 5 px, puntero capturado durante el arrastre, cursor de redimensionado y la cuenta en UN sitio (`Resolve(startWidth, delta, min, max, room)`), con el tope del lienzo como segunda cota. |
| `FileFlow.App.Uno/MainWindow.xaml` / `.xaml.cs` | Las **cinco columnas** del editor (cajón · asa · lienzo · asa · ficha) con las cotas del escritorio, las dos asas atadas a sus columnas y la ficha que **conserva su ancho** al plegarse y volver (`ApplyInspectorVisibility`, con el asa siguiendo la visibilidad del panel). |
| `FileFlow.App.Uno/RuntimeSelfCheck.cs` | Tres medidas nuevas en `--selfcheck-dialogs`: el nodo con ventana del escritorio declara y la ficha la pinta, su botón **AVISA nombrando la ventana** (el nombre se lee del diccionario del plugin, no de un literal), y el aviso se retira limpio. |
| `run-uno.ps1` / `run-uno-fast.ps1` | Los sondeos **esperan** al proceso y heredan su código de salida. |
| `FileFlow.Tests/Unit/App/NodeActionFrontierWiringTests.cs` (nuevo) | Tres casos: el aviso **llega** al servicio de diálogos del host por el camino entero del botón; el contexto lleva SIEMPRE un servicio (el nulo declarado sin host, nunca `null`); y el **censo** de las construcciones del contexto en el núcleo portable (una puerta nueva sin diálogos vuelve a ser un botón mudo). |
| `mutations/boton-del-nodo-que-no-avisa.json` (nuevo) | El defecto declarado: quitar el tercer argumento del contexto deja el botón mudo sin que ningún lint de texto lo vea. |

### 🛡️ Guardias, pruebas y mutaciones
- **+3 casos** en `NodeActionFrontierWiringTests` (la suite pasa de 1961 a **1964**).
- **1 mutación nueva** `boton-del-nodo-que-no-avisa` → **MUERDE** (34,5 s; testigo `TheNodeActionButton_ShouldShowTheDesktopOnlyWarning_InTheHostDialogs` rojo, control `DeclaringTheFrontier_ShouldShowItInTheHostDialogs` verde — que es justo lo que separa «el botón no lleva sus diálogos» de «la costura dejó de avisar», con su propia mutación desde el 268).
- La sonda del host es la que **cierra la frontera del 268** («empujar esos botones con la aplicación abierta sigue siendo materia de una sesión manual»): ahora se empuja desde el propio sondeo y el aviso se lee en el informe.
- **Dos defectos los cazó la suite al cerrar el tramo** (arreglados antes de darlo por bueno): (1) las dos claves de las asas (`Uno_SplitterToolbox` / `Uno_SplitterInspector`) se citaban **sin estar en ninguno de los dos diccionarios**, así que el nombre del asa se habría resuelto por el fallback incrustado en el código y **no habría cambiado de idioma** —el defecto que la superficie de ajustes mide, cazado por `EveryCitedUnoKey_ShouldExistInBothDictionaries`—; (2) el sobre del modal que este tramo añadió escribía **una tercera** llamada a `TearDownInlineQuestion(false)`, y la guardia del 263 cuenta **dos** vías de abandono: en vez de aflojar la guardia, la ventana del catálogo y el aviso comparten ahora `ShowOwnedModalAsync` y la cuenta sigue siendo **dos**.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** |
| Sonda del host (`-SelfCheck`) | **EXIT 0 · 85 `[OK]` · 0 `[FALLO]` · VERIFICADO** con el marco nuevo (el área de clic sigue coincidiendo con las tarjetas dibujadas: el lienzo pasa a (285,0) y se **mide**, no se supone) |
| Sonda de los paneles de nodo (`-SelfCheckDialogs`) | **EXIT 0 · 51 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 48: las tres medidas de la frontera) |
| La suite completa | **1963 superadas + 1 omitida de 1964, 0 errores** (2 m 33 s; el total pasa de 1961 a 1964) |
| Mutaciones | **1 nueva, MUERDE** (34,5 s) |

### 🟠 Fronteras declaradas
- **Las siete ventanas del toolkit siguen sin poder montarse en este host**: lo que cambia es que su botón **avisa** nombrando la ventana (y su traza queda en consola). Abrirlas es del escritorio.
- **El asa se mide por su cuerpo, no por el ratón del sistema**: lo ejercitado en el selfcheck es el mismo camino (la cuenta y la aplicación del ancho) y el arrastre con puntero real del asa queda para la sesión manual, junto al resto de gestos.
- **Los anchos no se persisten entre sesiones**: el reparto del escritorio se recupera al arrancar (cajón 280, ficha 300) y lo que el usuario ajuste vive lo que viva la ventana.
- **El asa de la ficha se retira con el panel**: sin columna que gobernar, un mando visible sería un mando que no manda.
- **El censo del contexto cubre el núcleo portable**, no las construcciones que un plugin haga por su cuenta (hoy no hay ninguna).

---

## [2026-09-28] - Hito 269: La Tarjeta Enseña lo que Hace, y las Acciones del Nodo Llegan a la Ficha

### 🎯 Objetivos y Alcance
El encargo, en dos frases del usuario: «en los nodos, al desplegarlos se ve el listado de parámetros pero no se puede hacer nada con ellos: mejor quítalos y déjalos que solo se puedan editar en el inspector» y «en estos a veces aparecen botones que abrirían diálogos de configuración que no están en el inspector: añádelos». Es decir: el panel plegable de la tarjeta del host Uno tenía una **lista muerta** (nombres sin editor) y, al mismo tiempo, la ficha del inspector **no tenía** las acciones del nodo, que son la puerta a sus superficies.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/NodeCardView.xaml` | El panel plegable se queda **con lo que hace algo**: fuera el `ItemsControl` de `Node.Parameters` (nombres, sin editor y sin gesto), dentro las **acciones del nodo**. El conmutador de la cabecera pasa a dibujarse **sólo si el nodo declara acciones** (un chevron que despliega un panel vacío es el botón-que-no-hace-nada que este tramo quita). Los `x:Name` (`ParametersToggle`, `ParametersPanel`, `ParametersIcon`) y el `AutomationId` `NodeCardExpandToggle` se conservan: son anclas estables de las sondas y de la guardia, y el comentario del XAML dice por qué el nombre ya no describe lo que despliegan. |
| `FileFlow.App.Uno/Controls/NodeCardViewModel.cs` | La condición en UNA propiedad: `ActionsPanelVisible => HasCustomActions && _node.IsExpanded`. El estado desplegado sigue siendo del **núcleo** (`Node.IsExpanded`, con su refresco agregado) y el rótulo del conmutador pasa a decir lo que hace **este** host («Mostrar/Ocultar las acciones del nodo») conservando la clave del escritorio, que es la que audita la guardia de textos compartidos. |
| `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | El **bloque de ACCIONES** de la ficha (pestaña de Parámetros, en el orden del escritorio: descripción → acciones → parámetros): un botón por acción declarada, con su `ToolTip`, su ancla `InspectorAction_<ActionId>` y el comando del view model **portable** (`NodeActionViewModel.ExecuteCommand` → `NodeViewModel.ExecuteCustomAction`, la MISMA orden que el botón de la tarjeta). El encabezado se localiza en caliente (`Uno_InspectorActions`) y el bloque entero se colapsa sin acciones. Superficie para la sonda: `ActionButtonCount` y `ActionControl(actionId)`. |
| `FileFlow.App.Uno/Resources/Strings{,.es}.resx` | La clave nueva del encabezado y el texto del conmutador, en los dos idiomas (el diccionario del host es el que este host carga; el del plugin no). |
| `FileFlow.Tests/Unit/App/UnoNodeDialogsGuardTests.cs` | El caso de la puerta de la tarjeta se reescribe al contrato nuevo: el panel cuelga de `ActionsPanelVisible`, el conmutador de `HasCustomActions`, y el listado de parámetros de la tarjeta **no puede volver** (aserción negativa sobre el XAML). |
| `FileFlow.Tests/Unit/App/UnoInspectorPanelGuardTests.cs` | Caso nuevo `InspectorPanel_ShouldPaintTheNodeActions_SoTheirSurfacesAreReachableWithoutTheCard` (colección del núcleo, comando portable, ancla por `ActionId`, encabezado localizado, bloque que se colapsa y la medición en runtime) + fila en la tabla de paridad del inspector. |
| `mutations/acciones-del-nodo-que-solo-se-pulsan-desde-la-tarjeta.json` (nuevo) | La mutación del bloque nuevo: quitar el bucle de acciones de la ficha deja la superficie del nodo inalcanzable para quien no despliegue una tarjeta. |

### 🔬 Lo que encontró la medida
1. **La lista muerta se veía verde por todas partes.** El panel de la tarjeta existía, se desplegaba, tenía su conmutador probado y su acción «🎬 Presets...» cableada; lo que no había era **nada que hacer** con la mitad de su contenido. Ninguna pieza mentía por separado: el defecto estaba en la composición (una lista que invita a interactuar y no responde).
2. **La puerta existía en un solo sitio.** Las acciones del nodo se pintaban **sólo** en el panel de la tarjeta del lienzo; el escritorio las pinta también en su ficha, y este host no. La medición lo cazó en cuanto la sonda preguntó por ellas: `acciones del nodo en la ficha: 0 de 0` en el flujo de ejemplo y, en el modo de los paneles, `la ficha del inspector pinta las acciones del nodo (1 botón, ancla 'InspectorAction_ManageMediaPresets')` tras el arreglo.
3. **La sonda dijo la verdad incómoda.** El primer intento de la sonda buscaba en el lienzo una tarjeta **con acciones** —y el flujo de ejemplo no tiene ninguna, porque el único nodo con acciones lo añade el modo de los paneles. En vez de dar la medida por buena, el sondeo mide ahora el **contrato** en todas las tarjetas (lo que enseña la vista == lo que dice el adaptador) y deja el ejercicio completo —desplegar, ver el panel con su chevron y pulsar la acción— donde el nodo existe de verdad.
4. **El andamiaje cazó dos mutaciones propias obsoletas.** `MutationDeclarationGuardTests.EveryDeclaredMutation_ShouldStillFitTheProductAndTheSuite` se puso rojo nombrando los fragmentos que el código ya no contiene (`conmutador-de-parametros-que-no-refresca`, que cita la línea de refresco que este tramo reescribió) y `mutate.ps1` rechazó la mutación nueva por una palabra de menos en el fragmento declarado («el del view model» vs «del view model»). Las dos se corrigieron **antes** de dar el tramo por bueno: la declaración de una mutación es código, no prosa.
5. **Un rótulo que mentía.** El conmutador de la tarjeta se llamaba «Mostrar/Ocultar parámetros» y ya no despliega parámetros: el texto de este host dice ahora lo que hace, con la clave intacta para no romper el censo de textos compartidos.

### 🛡️ Guardias, pruebas y mutaciones
- **+1 caso** en `UnoInspectorPanelGuardTests` y el caso de la tarjeta reescrito: la suite pasa de **1960** a **1961** (1959 superadas + 1 omitida en la corrida completa, con el flake de CPU conocido en `EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven`, que pasa en aislamiento).
- **1 mutación nueva** `acciones-del-nodo-que-solo-se-pulsan-desde-la-tarjeta` → **MUERDE** (28,4 s; testigo `InspectorPanel_ShouldPaintTheNodeActions_SoTheirSurfacesAreReachableWithoutTheCard` rojo, control `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive` verde). Las **dos** mutaciones que citan lo que este tramo reescribió se actualizaron y vuelven a morder: `tarjeta-sin-la-puerta-de-sus-parametros` (**MUERDE**, 28,9 s) y `conmutador-de-parametros-que-no-refresca` (**MUERDE**, 28,6 s).
- `COVERAGE.md` regenerado por su guardia: **100 declaradas** (antes 99).

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **Compilación correcta · 0 errores** (los avisos son los preexistentes del host y el `PRI257` de WinAppSDK) |
| Sonda del host (`.\run-uno-fast.ps1 -SelfCheck`) | **EXIT 0 · 85 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 88: el ejercicio del panel se mudó a donde hay una tarjeta con acciones y la puerta se mide ahora como contrato sobre las 3 tarjetas del lienzo) |
| Sonda de los paneles de nodo (`-SelfCheckDialogs`) | **EXIT 0 · 48 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 46; incluye las dos medidas nuevas: la acción del nodo en la ficha y el despliegue del panel de la tarjeta con su chevron) |
| Sondas de barra de control y de ajustes | `-SelfCheckControlBar` y `-SelfCheckSettings` → **exit 0** (sin cambios en esas superficies) |
| Suite completa | **1959 superadas + 1 omitida de 1961** (2 m 39 s) |
| Mutaciones | **1 nueva + 2 actualizadas, las tres MUERDEN** · **100 declaradas** |

### 🟠 Fronteras declaradas
- **El cambio es del host Uno.** El host de escritorio (Avalonia) conserva sus parámetros **en línea en la tarjeta**, que allí **sí** se editan (toggle, deslizador, desplegable, ruta con explorar, editor y catálogo de variables): la queja —«no se puede hacer nada con ellos»— es de la tarjeta del host Uno, donde el listado era un `TextBlock` sin editor. Igualar los dos hosts aquí sería quitarle al escritorio una capacidad que funciona.
- **Las acciones del nodo viven ahora en DOS sitios del host Uno** (el panel de la tarjeta y el bloque de la ficha): es la paridad con el escritorio y una decisión deliberada — el atajo del lienzo no se toca, y la ficha garantiza que la superficie no dependa de saber desplegar una tarjeta.
- **Un nodo sin acciones no despliega nada**: sin conmutador no hay panel, y el estado `IsExpanded` del núcleo se conserva (los grafos guardados no cambian de forma).
- **El botón de la ficha ejecuta la MISMA orden** que el de la tarjeta, así que en este host hereda su frontera: las superficies que el catálogo sirve (gestor de presets, diseñador de datasets) se abren; las que son ventanas del toolkit (configuración del VLM, estudio de scripts, gestión de contraseñas) **avisan** nombrando la ventana. La ficha no finge una capacidad que el host no tiene.

---

## [2026-09-28] - Hito 268: La Solución del Host Uno que Compila con `dotnet` y sin Avalonia (el Sabor de UI)

### 🎯 Objetivos y Alcance
El encargo: el host Uno se compilaba con **MSBuild de Visual Studio** (la nota de `AGENTS.md` decía que los targets de WinAppSDK **no corren** con `dotnet build`) y su binario arrastraba **las 16 DLL de Avalonia** que entran por los cinco plugins que traen ventanas del toolkit del escritorio. Objetivo: **una solución para Visual Studio** del host Uno, que **compile también con `dotnet`** y que compile ese host **sin nada de Avalonia**.

### 🔍 La premisa caducada (lo primero que se midió)
`dotnet build FileFlow.App.Uno/FileFlow.App.Uno.csproj` **ya compilaba** —**0 errores, 37 s**, con su `.exe`—: el proyecto declara `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`, que es justo lo que hace correr los targets de WinAppSDK sin MSBuild de VS. La frontera de `AGENTS.md` **había dejado de ser cierta sin que nadie volviera a medirla**, y el camino caro (seguir invocando MSBuild de fuera) tapaba además el problema de verdad: **Avalonia viajaba al host**. De los cinco plugins con ventanas (`AI`, `Archives`, `FileSystem`, `Integrations`, `Scripting`) salen **7 ventanas `.axaml`**, 12 ficheros `.cs` con `using Avalonia` y, en el binario del host, **16 DLL** (`Avalonia.*`, `AvaloniaEdit`, `Material.Icons.Avalonia`).

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.Uno.slnx` (nuevo) | La solución del host Uno: su proyecto y **todo su grafo**, sin `FileFlow.App` (el host Avalonia) ni `FileFlow.Tests`. Se abre en Visual Studio **y** compila con `dotnet build`. |
| `Directory.Build.props` | El **SABOR DE UI**: `FileFlowUnoHost` sale del **NOMBRE de la solución** (`$(SolutionFileName) == 'FileFlow.Uno.slnx'`), `FileFlowDesktopToolkit` es su inverso y de ahí sale la constante `FILEFLOW_NO_DESKTOP_TOOLKIT` que leen los nodos. El defecto es **escritorio**: compilar un proyecto suelto (o la solución del escritorio) no cambia de producto por sorpresa, y el script pasa `-p:FileFlowUnoHost=true` explícito para no depender del nombre. |
| 5 `FileFlow.Plugin.*/…csproj` | Los paquetes de Avalonia (`Avalonia`, `Avalonia.Themes.Fluent`, `Avalonia.Controls.DataGrid`, `Avalonia.AvaloniaEdit`, `Material.Icons.Avalonia`) pasan a estar **condicionados** al toolkit, y el sabor Uno declara qué ficheros **no compila** (las 7 ventanas, sus convertidores, los dos view models que sólo existen para ellas) y añade `Material.Icons` —el paquete puro— porque el árbol del diseñador sí elige su icono. |
| `FileFlow.Sdk/Services/DesktopOnlySurface.cs` (nuevo) | La **costura de la frontera**: el nodo que se compila sin el toolkit **no** construye su ventana y **declara** que pertenece al escritorio por los diálogos de quien lo abrió (`ShowWarning`) más una traza en el canal de errores. El **texto** no vive aquí: lo pone cada plugin (mecanismo en el SDK, palabras en quien las dice). |
| Los **7 nodos** | `SmartUnpackNode`, `ArchiveFanOutNode`, `MultimodalVisionLlmNode`, `AdvancedRenamerNode`, `SyntheticDataSourceNode`, `MediaTranscoderNode` y `CustomScriptNode`: su bloque del toolkit (construir la ventana, resolver el propietario, `ShowDialog` y leer el resultado) queda dentro de su región condicional, con `DesktopOnlySurface.Declare` en la mitad sin toolkit. Los dos que **además declaran su superficie** al SDK (diseñador de datasets y gestor de presets) llevan ahí su **defensa declarada**: en el host Uno se sirven por el catálogo del host y por este camino no se llega (el núcleo abre la superficie declarada antes de tocar la acción). |
| `UI/Services/DesktopFilePicker.cs` (nuevo, plugin FileSystem) | La costura del **selector de archivos** del Diseñador de Datasets: su view model es PORTABLE —lo pintan los dos hosts— y hasta ahora llamaba a la API de almacenamiento de Avalonia, así que arrastraba el toolkit entero *y* hacía del importar/exportar del host Uno un **no-op silencioso**. Una implementación por sabor (la de escritorio monta el selector real; la del host declara la frontera y devuelve «no hay fichero»). |
| `run-uno.ps1` | Compila con **`dotnet build FileFlow.Uno.slnx`** (adiós al parámetro `-MsBuildPath` y a MSBuild de VS) y sigue esperando el proceso y heredando el código de salida en los sondeos. |
| `FileFlow.Tests/Unit/App/UnoHermeticBuildGuardTests.cs` (nuevo) | La guardia del sabor: 7 casos que atan la solución (y que todo lo que el host referencia esté en ella), el sabor y su constante, la condición del toolkit en los cinco plugins, la **medición del hermetismo** (ningún fichero que el sabor Uno compila menciona Avalonia **fuera de una región condicional**, con la profundidad de preprocesador como criterio y el censo de >100 ficheros como anti-vacuidad), la frontera de los 7 nodos, sus textos en los DOS idiomas (y las claves de los nombres que citan) y el lanzador. |
| `FileFlow.Tests/Unit/App/DesktopOnlySurfaceTests.cs` (nuevo) | La medición **por comportamiento** de la costura: con un doble de diálogos que se acuerda, la frontera se dice **una vez** y con el nombre de la superficie; sin diálogos no revienta. |

### 🔬 Lo que encontró la medida (y lo que cazó la guardia mientras se escribía)
1. **La premisa escrita y nunca re-medida.** `AGENTS.md` afirmaba que el host Uno **no** compilaba con `dotnet build`. Era **falso desde que el proyecto declaró `WindowsPackageType=None`**: la nota sobrevivió al cambio que la invalidaba. Lección del tramo: una frontera declarada sin fecha de caducidad se vuelve una excusa para no probar el camino corto.
2. **El compilador fue el mapa del acoplamiento.** Enumerar «qué menciona Avalonia» a mano dejaba fuera cosas: el primer `dotnet build FileFlow.Uno.slnx` falló por **7 errores concretos** —dos usings de namespaces que dejan de existir sin el toolkit, el view model del VLM que sólo existía para su ventana y `MaterialIconKind` en el árbol del diseñador— y cada uno fue una pieza de arquitectura que estaba escondida detrás de un `using`.
3. **La guardia cazó su propio criterio, dos veces.** La primera, al confundir **prosa con código**: el `DesktopFilePicker` explicaba en su documentación que el toolkit es de Avalonia y el barrido lo contaba como mención (ahora lee el código **sin comentarios**). La segunda, al censar por prefijo: `FileFlow.App.Core` —la capa PORTABLE que los dos hosts comparten— empieza igual que el host de escritorio, así que el censo se mira **por directorio** y no por nombre.
4. **Las DLL de Avalonia sobrevivían al cambio de fuente** en el `bin` del host (un build incremental no borra lo que ya estaba): el «cero Avalonia» se midió **limpiando la salida** y volviendo a compilar, no confiando en el build incremental.

### 🛡️ Guardias, pruebas y mutaciones
7 casos en `UnoHermeticBuildGuardTests` + 2 en `DesktopOnlySurfaceTests` (**+9**, la suite pasa de 1951 a **1960**). Una mutación nueva **`frontera-de-escritorio-que-no-avisa` → MUERDE** (30,5 s; testigo `DeclaringTheFrontier_ShouldShowItInTheHostDialogs` rojo, control `NoPluginThatDrawsAWindow_ShouldReferenceTheToolkit_OutsideItsCondition` verde). `COVERAGE.md` regenerado por su guardia: **99 declaradas · 15 de 17 subsistemas · 17 de 46 guardias** con una mutación que las muerde (antes **98 · 17 de 45**).

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **Compilación correcta · 0 errores** (los 51 avisos son los preexistentes del host) |
| Salida del host Uno | **0 DLL de Avalonia** (antes **16**: `Avalonia.*`, `AvaloniaEdit`, `Material.Icons.Avalonia`), medida tras limpiar `bin/` y `obj/`; queda `Material.Icons.dll`, que es el paquete puro del icono |
| Sonda del host Uno (`.\run-uno.ps1 -SelfCheck`) | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]` · VERIFICADO**, el mismo recuento que antes del cambio: la hermesis del build no tocó el comportamiento del host |
| Suite completa | **1959 superadas + 1 omitida de 1960, 0 errores** (2 m 58 s; antes **1950 + 1**: los siete casos del sabor y los dos de la costura) |
| Mutaciones | **1 nueva, MUERDE** · **99 declaradas** · **17 de 46 guardias** con mutación que las muerde |
| Solución del escritorio | `FileFlow.slnx` **intacta** (la guardia exige que siga compilando la app con Avalonia) |

### 🟠 Fronteras declaradas
- **Lo que el host Uno pierde, y se dice al decirlo**: las siete ventanas del toolkit (Gestor de Contraseñas, configuración del VLM, Estudio de Renombrado, Diseñador de Datasets por la acción personalizada, Gestor de Presets por la acción personalizada, Estudio de Scripts) y el **selector de archivos** del diseñador. Dos de ellas se sirven por su **superficie declarada** (el catálogo del host las cumple); las otras cinco **avisan con el nombre de la ventana** y su motivo por los diálogos del host, en vez de no hacer nada. Antes de este tramo, empujar esos botones construía una ventana de **otro framework** dentro del proceso WinUI.
- **El sabor es una propiedad del GRAFO, no del código**: los plugins siguen siendo **los mismos** y el escritorio no cambia una línea de su comportamiento; lo que cambia es qué mitad de cada plugin se compila. Las dos soluciones **se pisan los `bin`** de los plugins (el último build manda), así que compilar un host recompila los plugins para ese host.
- **`FileFlow.Tests` no entra en la solución del host Uno**: sus pruebas montan ventanas de Avalonia (siguen siendo del sabor de escritorio) y entran por `FileFlow.slnx`.
- **El host Uno sigue sin empaquetado**: se compila y se ejecuta, no se reparte instalado (la frontera de entrega sigue abierta).
- **La frontera NO se ha ejercido con el host Uno abierto**: lo que está medido es (a) que el sabor Uno compila y su salida no lleva Avalonia —build tras limpiar, guardia del censo y sonda del host en verde—, (b) que la costura **avisa de verdad** (prueba de comportamiento con un doble de diálogos) y (c) que **cada nodo la declara** con el nombre de su ventana y sus textos en los dos idiomas. **Empujar esos botones con la aplicación abierta y leer el aviso** —y su canal externo— sigue siendo materia de una sesión manual, como el resto de las fronteras declaradas.

---

## [2026-09-28] - Hito 267: La Sonda del Escritorio (el Trazo, el Pan y el Zoom con Puntero Inyectado)

### 🎯 Objetivos y Alcance
Cerrar la segunda mitad de la frontera que el **266 declaró**: «el escritorio **no tiene sonda propia** (las `--selfcheck*` son del host Uno) y el trazo **con un dedo** queda para una sesión del escritorio». Objetivo: dar al host de escritorio su **propia sonda de autorrevisión** —arranca la **aplicación real** y la mide desde dentro, con **veredicto por código de salida** e informe junto al ejecutable, como las del host Uno— y **usarla para medir el lienzo con puntero inyectado**: el trazo del cable, el pan y el zoom, cada uno con su gesto y con la medida repetida después.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `App/SelfCheck/DesktopSelfCheck.cs` (nuevo) | La sonda: `--selfcheck` arranca la aplicación real (servicios, plugins, vistas y estilos del producto) y mide **el trazo del cable** (cada cable dibujado con el control del host, su curva contra `ConnectionGeometry.BuildWire` y su extremo **sobre el punto dibujado de su socket, en píxeles de ventana**), el **pan** con el botón derecho, el **zoom** con la rueda (acercar, alejar y el techo `MaxViewportZoom`) y el **reparto de botones** (el izquierdo sobre el fondo **no** panea). Informe en `selfcheck-report.txt` con líneas `[OK]`/`[FALLO]` y recuento; veredicto por `desktop.Shutdown(0/1)`. |
| `App/Program.cs` | La **puerta** del modo: con el argumento, arranca el anfitrión de la sonda **antes** del arranque normal (y sin el argumento, nada cambia). |
| `App/App.axaml.cs` | La medida corre **con la ventana ya en pantalla** y el arranque **no lanza la comprobación de actualizaciones** en este modo: su red y su aviso romperían la hermesis del veredicto (el mismo corte que hace el host Uno). |
| `App/FileFlow.App.csproj` | `Avalonia.Headless`: la plataforma **headless con Skia real**, que es la única forma de que un puntero entre por el **pipeline de entrada** de Avalonia desde dentro del proceso (el escritorio no expone inyección de entrada cruda). Fuera de este modo no se usa. |
| `run.ps1` · `run-fast.ps1` | `-SelfCheck`: pasa el argumento, **espera el proceso y hereda su código de salida** (los gemelos del escritorio de `run-uno.ps1 -SelfCheck`). |
| `FileFlow.Tests/Unit/Views/DesktopCanvasGestureTests.cs` (nuevo) | La misma entrada —puntero inyectado por el pipeline, la que ya usaba `InputSimulator`— desde el suite y **con la escena bajo control**: el aterrizaje del trazo sobre el punto dibujado de cada socket, el pan (encuadre y desplazamiento del trazo), el zoom (acercar, alejar y el techo), y el arrastre de una tarjeta que mueve el nodo y **no** el plano. |
| `FileFlow.Tests/Unit/App/DesktopSelfCheckGuardTests.cs` (nuevo) | La guardia de la **puerta** del modo: que exista en la línea de comandos de la aplicación, que arranque el anfitrión **que sabe inyectar un puntero**, que **sólo mida** (los gestos mueven el encuadre; la sonda no lo escribe) y que prepare la escena sin escribir estado del usuario. |

### 🔬 Lo que encontró la medida (cuatro cosas que sólo aparecen midiendo)
1. **El gesto iba a la pieza flotante.** La primera corrida eligió el punto del gesto fijándolo en la esquina libre del lienzo… y ahí está la **barra de zoom**: el puntero pulsó **encima** de ella y la sonda midió el silencio del lienzo (el encuadre no se movía y el trazo tampoco). Ahora el punto se **busca por hit-testing** —el primero cuyo recorrido de respuesta llega al lienzo y no a una tarjeta, un decorador, un cable ni la barra de zoom—, con la misma idea que se ganó en el host Uno al medir el hit-testing.
2. **La escena que se medía a sí misma.** El bucle de reintentos —copiado del host Uno, donde la medida es del árbol y puede repetirse— aquí **mutaba** la escena: cada intento paneaba y hacía zoom sobre el anterior, así que la corrida siguiente medía un encuadre `-120, -64` con el zoom ya al tope que **ningún usuario ve**, y la fase del pan salía en rojo por un estado que la propia sonda había dejado. Ahora la espera **no mide** (espera a que el lienzo tenga tamaño) y la medida es **una sola vez**, sobre la escena que el producto presenta.
3. **El ancla medida contra la etiqueta del puerto.** Medir el aterrizaje contra el **control del conector** daba un hueco de **14,5 px** —el ancho de la etiqueta del puerto, que vive dentro del mismo control—. El punto que el usuario ve es la **figura del socket** (la plantilla `PortSocketTemplate`, clases `socket`/`socketTriangle`), y es donde Nodify sitúa el ancla del puerto: con esa referencia el hueco es **0,00 px** en las dos anclas, con el plano quieto y después de cada gesto.
4. **El arrastre de una tarjeta no es un gesto del lienzo.** La fase se midió y se **retiró** de la sonda, con su razón escrita: la tarjeta **se lleva al frente** al pulsarla —lo que reordena la colección del documento— y, si el puntero se va hacia el borde, el editor **auto-paneea** mientras arrastra: dos efectos del propio producto que no tienen que ver con el reparto de botones que la sonda defiende. En su lugar la sonda mide **el botón que no panea** (el izquierdo sobre el fondo) y el arrastre de la tarjeta se queda en el suite, donde la escena está bajo control (y donde una mutación lo muerde).

### 🛡️ Guardias, pruebas y mutaciones
Cuatro casos de gesto (`DesktopCanvasGestureTests`): el trazo toca sus sockets con el plano quieto, el **pan** mueve el encuadre el gesto dividido por el zoom —y el trazo sigue a la mano—, la **rueda** acerca, aleja y se detiene en el techo (2,5) sin despegar el trazo de sus sockets, y el arrastre de una tarjeta mueve el nodo **y no el plano**. Cuatro casos de puerta (`DesktopSelfCheckGuardTests`): el modo en la línea de comandos y el anfitrión que inyecta, el veredicto por código de salida y su informe, la medida **en espacio de ventana** y **sin escribir el encuadre** (una regla que impide que la sonda se mida a sí misma), y el arranque sin trabajo de fondo. **Dos mutaciones nuevas, las dos MUERDEN**: `pan-que-responde-al-boton-izquierdo` (32,1 s; testigo `TheRightDragOnTheCanvas_…` rojo, control —el arrastre de la tarjeta, que el mutante **no** rompe, medido— verde) y `sonda-de-escritorio-sin-el-anfitrion-del-puntero` (29,3 s; testigo `TheProbe_ShouldBeItsOwnCommandLineMode_…` rojo, control `TheCable_ShouldTouchItsSockets_InWindowSpace` verde). `COVERAGE.md` regenerado por su guardia: **98 declaradas · 15 de 17 subsistemas · 17 de 45 guardias** con una mutación que las muerda.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| Compilación (`FileFlow.App`, XAML de Avalonia incluido) | **0 errores** |
| Sonda del escritorio (`.\run.ps1 -SelfCheck`) | **EXIT 0 · 41 `[OK]` · 0 `[FALLO]` · VERIFICADO**, y **el informe es byte a byte idéntico entre dos corridas** (medido con `diff`): la medida no depende del reloj ni del azar de la carga |
| Suite completa | **1950 superadas + 1 omitida de 1951, 0 errores** (cuatro corridas: 2 m 40 s, 2 m 45 s, 2 m 40 s y 2 m 44 s; antes **1942 + 1**: los cuatro gestos y las cuatro guardias nuevas) |
| Rojo intermitente | **una corrida trajo 1 fallo que no quedó nombrado** (la salida se cortó con un `tail`); las **tres** corridas siguientes —una de ellas con compilación, como la que falló— quedaron **verdes** con los mismos 1950. Es el ruido de carga ya declarado en los hitos 255, 258 y 262 |
| Mutaciones | **2 nuevas, las 2 MUERDEN** · **98 declaradas** · **17 de 45 guardias** con mutación que las muerde |
| Sonda del host Uno | No se re-ejecutó en este tramo: lo tocado es del escritorio (`FileFlow.App`) y el host Uno **no** referencia ese ensamblado (referencia `FileFlow.App.Core`, `Sdk`, `Core` y sus plugins); la suite cubre los dos hosts |

### 🟢 Las cifras que midió la sonda (la evidencia)
Escena: lienzo **1075×565**, ejemplo `flow_01_organizador_imagenes.json` (**3 nodos · 2 cables**), dos cables dibujados de dos, y la curva de cada uno **la del núcleo**. **Trazo**: hueco `0,00 px` en las dos anclas. **Pan** (arrastre de `(120, 80)` px con el derecho, zoom 1): el encuadre se mueve `(-120,0, -80,0)` —el gesto dividido por el zoom, exacto—, el trazo se desplaza `(120,0, 80,0)` **siguiendo a la mano** y sus huecos siguen en `0,00 px`. **Zoom**: la rueda hacia arriba `1,000 → 2,000`; hacia abajo `2,000 → 0,200` (el suelo `MinViewportZoom`); **20 muescas** dejan el zoom en `2,500` (el techo) y el trazo sigue sobre sus sockets (`0,00 px` con la escala al tope). **Reparto de botones**: el arrastre izquierdo sobre el fondo **no** mueve el encuadre (`-120,0, -64,0` antes y después).

### 🟠 Fronteras declaradas
- **El puntero es del pipeline de entrada, no del sistema operativo**: la sonda inyecta movimiento, pulsación, arrastre y rueda por el camino real de Avalonia (hit-testing, gestos del editor, captura de puntero) sobre la aplicación montada, pero **no** es un dedo del sistema operativo sobre la ventana nativa —eso sigue siendo materia de una sesión manual—. La frontera va escrita **en el propio informe**, para que nadie lea sus 41 `[OK]` como una sesión con ratón.
- **El precio del instrumento**: la sonda necesita `Avalonia.Headless` en el proyecto del producto (el modo es el único que lo usa). La alternativa —medir sobre la plataforma nativa— no puede inyectar entrada desde dentro del proceso, y una sonda sin dedos daría verde midiendo un lienzo quieto (lo vigila la mutación del anfitrión).
- **El arrastre de una tarjeta no lo mide la sonda** (se mide en el suite, con su razón escrita arriba).
- **La escena es un ejemplo del catálogo**, cargado con el mismo camino que usa el host Uno (`WorkflowGraph.FromJson` + `LoadFromGraphModel`), porque el escritorio **no carga documento al arrancar**: la sonda no escribe nada del usuario (ni preferencias, ni contadores de uso) y el grafo vive en memoria hasta que el proceso termina.
- **Sin comparación de píxeles entre hosts**: lo medido es la geometría dibujada proyectada al espacio de la ventana; que el **píxel** del cable coincida con el del host Uno sigue sin medirse.

---

## [2026-09-28] - Hito 266: El Cable del Escritorio lo Dibuja el Trazador Compartido (y la Frontera que el 254 Dejó Declarada)

### 🎯 Objetivos y Alcance
Cerrar la frontera que el hito 254 **midió y declaró sin arreglar**: la geometría del cable vive desde entonces en el núcleo (`ConnectionGeometry`, la Bézier que **nace y muere en las anclas**), el host Uno la dibuja… y el **escritorio seguía dibujando con el control de conexión de Nodify**, que traza su propia curva —una Bézier retirada de las anclas y unida a ellas por dos **tramos rectos**, con el cuello `Spacing` fijo—. Objetivo: el escritorio dibuja con la geometría compartida (no sólo la usa para el hit-testing), con su guardia y su mutación.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `App/Views/Components/FlowConnection.cs` (nuevo) | Un `Shape` de Avalonia cuyo `DefiningGeometry` sale de **`ConnectionGeometry.BuildWire`**: la curva la decide el núcleo y el host pone el envoltorio del framework. Conserva lo que el lienzo usa —`Stroke`, `StrokeThickness`, `StrokeDashArray`, `Cursor`, las clases por familia de tipo y el menú contextual— y, del control, lo que era bueno: cuello horizontal, techo `100 + √(25·ancho)` y tope de la mitad del hueco. |
| `App/Converters/GraphConverters.cs` | `ConnectionDirectionConverter`: el cable **en curso** lo arrastra el control de Nodify (su flag: arrastrar desde una entrada va hacia atrás) y el dibujo se pide en el vocabulario del núcleo. Los dos flags tienen los mismos dos valores. |
| `App/Views/EditorView.axaml` | La plantilla de cables usa `components:FlowConnection` (mismos enlaces de ancla con el conversor de punto, mismo menú de borrado). Se va el `Spacing="45"` del control. |
| `App/Styles/Ports.axaml` | Los estilos del cable apuntan al control nuevo (`components|FlowConnection` y sus siete familias de tipo) y el **cable en curso** —el de la plantilla del `PendingConnection`— se dibuja con el **mismo** control: el trazo ya no cambia de forma al soltar el botón. |
| `FileFlow.Tests/Unit/Views/FlowConnectionGeometryTests.cs` (nuevo) | Cinco casos que leen la figura que el control **va a pintar** (`DefiningGeometry`) y la comparan punto por punto con el núcleo: nace y muere en las anclas, el cuello es el del núcleo (200 contra los 45 del control), se da la vuelta con `Backward`, sigue a sus anclas al moverse y el conversor habla los dos vocabularios. |

### 🛡️ Guardias, pruebas y mutaciones
`NodeCardVisualContractTests` gana **`EveryCableOfTheCanvas_ShouldBeDrawnWithTheCoreGeometry_NotWithTheNodifyControl`**: barre el XAML del lienzo para que **ningún** cable vuelva al control de Nodify, exige que los **dos** (establecido y en curso) usen el control del host y que la curva se pida a `ConnectionGeometry`. Los casos del cable se reapuntan al control nuevo (la plantilla, el menú contextual colgando del trazo, los estilos por familia de tipo) y `GeometryBindingProjectionTests` sigue midiendo **en el árbol real** que los dos extremos del cable llevan el ancla convertida de su puerto: al reapuntarlo, el mismo caso comprueba también que la figura dibujada es la del núcleo para esas anclas. Y **`ThePendingCable_ShouldAlsoBeDrawnWithTheCoreGeometry`** monta el cable **en curso** en una ventana —con los estilos de la aplicación— y exige que sea el control del host, que su dirección salga del conversor (arrastrar desde una entrada, `Backward`) y que la figura sea la que el núcleo traza para esas dos anclas. **Una mutación nueva `cable-de-escritorio-por-el-control-de-nodify` → MUERDE** (29,2 s, testigo rojo y control —`TheCable_ShouldBeTheCoreCurve_BetweenItsTwoAnchors`, que mide el trazador sin pasar por el lienzo— verde). `COVERAGE.md` regenerado por su guardia: **96 declaradas · 15 de 17 subsistemas**.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| Compilación (`FileFlow.App`, XAML de Avalonia incluido) | **0 errores** |
| Suite completa | **1942 superadas + 1 omitida de 1943, 0 errores** (dos corridas verdes: 2 m 37 s y 4 m 21 s; antes **1935 + 1**) |
| Mutaciones | **1 nueva, MUERDE** · **96 declaradas** |
| Sonda del host Uno (el que ya dibujaba con la geometría compartida) | `--selfcheck` **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`**, con sus medidas de cable (cables dibujados, el cable toca su socket con el plano quieto y tras pan/zoom, y la forma cabe en un hueco estrecho sin el rulo del 2) |
| Rojo intermitente | `EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` (medida de CPU) cayó en la corrida con carga y queda **verde en aislamiento**; las dos corridas completas de la suite, verdes |

### 🟠 Fronteras declaradas
- **No se mide en la app del escritorio con puntero**: el escritorio no tiene sonda propia (las `--selfcheck*` son del host Uno). Lo que se mide aquí es la **figura** que el control va a pintar y el **árbol visual real** del lienzo headless (control materializado, ancla enlazada y trazo con el color de su familia de tipo); el trazo **con un dedo** queda para una sesión del escritorio.
- **El contenedor de Nodify se conserva**: `ConnectionContainer` sigue envolviendo cada cable (selección, foco y el menú contextual del contenedor no se tocan; el lienzo no activa su `IsSelectable`). Lo que se sustituye es **quien dibuja el trazo**.
- **Heredado del 254 y sin tocar**: el caso **apilado** (anclas sin hueco horizontal) dibuja recta vertical, y la **caída tipo hilo** no está implementada.

---

## [2026-09-28] - Hito 265: Las Seis Órdenes Destructivas que Quedaban Mudas (y el Contenido que Nacía sin Diálogos)

### 🎯 Objetivos y Alcance
Cerrar las **seis órdenes destructivas** que el hito 264 **localizó y declaró sin arreglar**: todas preguntaban por la variante **síncrona** del contrato de diálogos, que en este host **no muestra nada y no hace nada** (su hilo de UI no puede bloquearse) y que en un servicio sin diálogos contesta «sí» **sin preguntar**. Objetivo: la **misma regla compartida** que ya existía —`IDialogService.ConfirmAsync` y la decisión en el **view model portable**—, sin duplicar lógica en las vistas, sin cambiar las órdenes que no destruyen, y declarando con su razón lo que no se toca.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `App.Core/ViewModels/ControlBarViewModel.cs` | `NewWorkflowAsync` y `RollbackLastExecutionAsync` esperan `ConfirmAsync`. **`CreateNewWorkflow()` se separa**: confirmar no es parte de crear un flujo, y quien ya tiene la respuesta no necesita el diálogo. |
| `App.Core/ViewModels/ThemeCustomizerViewModel.cs` · `VirtualFileSystemExplorerViewModel.cs` · `AiModelManagerViewModel.cs` | `DeleteThemeAsync` · `ClearVirtualFileSystemAsync` · `DeleteModelAsync`: los tres esperan la respuesta real. |
| `Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs` | `DeleteDataSetAsync` espera la respuesta real. |
| `App.Uno/Controls/ThemeCustomizerBody.xaml(.cs)` | El **botón «Eliminar» del Estudio de Temas**, dibujado (`ThemeStudioDeleteButton`, habilitado sólo con un tema propio) y su orden ejecutada por el comando del view model: su fila sale de `DeclaredPendingParts`. La orden existía **sin puerta**. |
| `App.Uno/MainWindow.xaml.cs` | «Nuevo Flujo» deja de confirmar en la vista: ejecuta la **orden CANÓNICA** (que ya pregunta por el contrato asíncrono) y refresca el renglón del ciclo que lee el canal externo. |
| `App.Uno/Controls/ControlBar.xaml.cs` | El atajo `Ctrl+N` enruta por el **mismo camino** que el botón del cajón (una orden, un camino) y la fila de `HostOwnedOrders` explica el desvío. |

### 🐛 Los tres defectos que encontró la revisión adversarial
1. **El diseñador de datasets nacía con el doble nulo.** El nodo declaraba su superficie pero construía el contenido **sin diálogos** (`new SyntheticDataSetDesignerViewModel()`), y el nodo —que vive en un ensamblado de plugin— no puede resolverlos: se los pasa quien abre, por el `NodeCustomActionContext`. Cambiar la pregunta a la vía asíncrona **no bastaba**: la asíncrona del doble nulo **delega en su síncrona**, que contesta «sí». El diseñador **borraba en silencio en los DOS hosts**. Arreglado en los tres caminos (el contenido del nodo, la ventana del escritorio y la puerta del host Uno).
2. **Un evento muerto que el compilador cantó** (`CS0067`): al pasar «Nuevo Flujo» al comando canónico, `Bar.NewWorkflowRequested` dejó de dispararse y la ventana seguía suscrita, así que el atajo `Ctrl+N` ejecutaba el comando **sin** los dos pasos de host que sí hacía el cajón (el rastro y el refresco del renglón del ciclo que lee el canal externo). Ahora el atajo pide la orden a la ventana, como el cajón.
3. **La pregunta del borrado de un modelo estaba escrita en el código.** Al revisar las ocho órdenes, siete ya sacaban su texto del diccionario (`_loc.GetString` / `LocalizationManager.Instance`) y ésta lo llevaba literal —«¿Estás seguro de que deseas eliminar el modelo 'X' del disco local?» y «Eliminar Modelo»—, así que **no cambiaba de idioma nunca**. Ahora va por el diccionario (claves `AiModelManager_ConfirmDeleteMsg` / `AiModelManager_ConfirmDeleteTitle` en los **cuatro** diccionarios de los dos hosts) y la guardia **exige** que el método de cada orden destructiva lea al menos un texto del diccionario: el defecto no destruye nada, así que ninguna medición de datos lo ve, y por eso se mide con un mutante que vuelve a escribirlo.

### 🛡️ Guardias, pruebas y mutaciones
`UnoNodeDialogsGuardTests` **14 de 14**: el caso nuevo **`EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne`** ata la tabla de las **ocho** órdenes destructivas (método asíncrono con su `[RelayCommand]` y `await _dialogService.ConfirmAsync(`), **barre el árbol de fuentes del producto** para que nadie vuelva a preguntar por la vía síncrona y exige que el contrato siga conservándola; y **`EveryDeclaredSurface_ShouldCarryTheHostDialogs_SoItsDestructiveOrdersCanAskForReal`** ata la entrega de los diálogos del host al contenido de las dos superficies declaradas. `UnoControlBarParityGuardTests` **13** (el reparto de las tres órdenes de flujo, con el atajo enrutado por el camino de la ventana). Pruebas nuevas: el **vaciado del VFS** (confirmado vacía / **cancelado no toca nada**, con la síncrona contestando «sí» a propósito como trampa) y el **borrado del dataset** (16 casos en total). El caso de la tabla exige además que **la pregunta de cada orden venga del diccionario** (clave y texto de reserva), no de un literal escrito en el código: sin esa mitad, el texto de una orden destructiva se queda en un idioma para siempre. **Cinco mutaciones nuevas, las cinco MUERDEN** (`vfs-que-se-vacia-sin-preguntar` 60,4 s · `disenador-que-borra-sin-preguntar` 51,1 s · `orden-destructiva-que-vuelve-a-la-via-sincrona` 44,5 s · `superficie-declarada-sin-los-dialogos-del-host` 45,0 s · `pregunta-destructiva-escrita-en-el-codigo` 29,2 s; las cinco **re-ejecutadas** sobre el árbol final), todas con testigo rojo y control verde. `COVERAGE.md` regenerado por su guardia: **95 declaradas · 15 de 17 subsistemas**.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** / **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 46 `[OK]` · 0 `[FALLO]`** / **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias / pruebas | **14 + 13 + 5** / las dos del VFS y las dos del dataset en verde |
| Mutaciones | **5 nuevas, las 5 MUERDEN** · **95 declaradas** |
| Suite completa | **1935 superadas + 1 omitida de 1936, 0 errores** (dos corridas completas verdes: 4 m 5 s y 2 m 37 s) |
| Rojo intermitente | **una corrida intermedia trajo 1 fallo que no quedó nombrado** (la salida se cortó al leerla); las dos corridas completas siguientes, verdes, y `ExampleFlowsEndToEndTests` **4 de 4 en aislamiento** |
| Sesión con la app abierta | **33 de 33 pasos** (`qa-manual-265`) |

### 🟢 Ejercido con la aplicación abierta
**Dos** de las seis órdenes, las dos restaurables sin tocar datos del usuario. **«Eliminar tema» del Estudio**: catálogo con **1** tema propio → el estudio se abre desde el cajón (**4 anclas**) → «Nuevo tema» lleva el **fichero** del almacén de **1 a 2** → «Eliminar» **PREGUNTA** (`HostConfirmationAccept`/`HostConfirmationCancel`, **dentro del estudio**, centro `#B0ACAC`) → con la pregunta en pantalla siguen **2** → **cancelar deja 2** → **confirmar deja 1**, con el catálogo **idéntico** y el fichero **byte a byte** (`f8b1a4e9…`). **«Nuevo Flujo»**: **PREGUNTA** en su **propio modal** (`HostConfirmationDialog`, botones «Aceptar»/«Cancelar») → con la pregunta siguen las **3 tarjetas** → **cancelar las deja** (árbol y **3** barras de acento por pixel, pixel de base `#FCF8F8`) → **confirmar vacía el lienzo** (**0** tarjetas, **0** barras de acento). El grafo no se persiste: al **reiniciar**, el lienzo vuelve a sus **3 tarjetas**. Al final: temas **byte a byte**, presets **intactos** (`9b1e8f19…`), preferencias con el **mismo md5** antes y después de las dos órdenes —el driver lo lee del fichero y lo compara; la única clave que reescribe la sesión entera es `LastUpdateCheckUtc`, y la escribe el arranque— y ajustes esenciales intactos.

### 🟠 Fronteras declaradas
- **`ClearVirtualFileSystemAsync` no tiene puerta**: ninguna vista —ni la del escritorio ni la del host— dibuja su botón. Queda preguntando por la vía correcta y **sin entrada**; dibujarla es UI nueva, fuera del encargo. Se declara, no se finge.
- **`DeleteModelAsync` no se mide con el ratón**: su borrado retira ficheros **reales** del disco (`%AppData%\FileFlow\models`, varios GB). Se mide su determinación en la suite y no en la app abierta.
- **El escritorio no cambia**: conserva su confirmación síncrona y `ConfirmAsync` delega en ella.
- **Dos defectos del INSTRUMENTO, escritos para el guion futuro**: `ThemeStudioBody` no existe para el canal externo (la raíz del estudio es un `Grid` **sin peer de automatización**: el driver reconocía el estudio por un ancla que nunca llega) y el **pixel del centro no distingue un lienzo vacío de uno con tarjetas** (el fondo es el mismo: las tarjetas se cuentan por su **barra de acento**).

### 📄 Evidencia
[`docs/qa/qa_destructive_orders_265.md`](file:///docs/qa/qa_destructive_orders_265.md) + `docs/qa/qa-manual-265/` (capturas `40_…`-`48_…`, `destructivas-session.json`) + el driver `docs/qa/qa_destructive_uia.py`.

---

## [2026-09-28] - Hito 264: La Confirmación de las Órdenes Destructivas del Gestor (y las Dos Puertas que se Comportan Igual)

### 🎯 Objetivos y Alcance
Cerrar el defecto que el tramo anterior **midió y declaró sin arreglar**: las órdenes destructivas del gestor de presets **no pedían confirmación** y **se comportaban distinto según la puerta** —por la **fila** el view model recibía el servicio **Nulo** (`ShowConfirmation => true`) y borraba **en silencio**; por la **tarjeta** recibía el del host, cuya confirmación es **síncrona** y devuelve «no» desde el hilo de UI, así que **no borraba y tampoco avisaba**—. Objetivo: **una sola regla** con la semántica del escritorio (borrar pregunta de verdad y depende de la respuesta REAL del usuario, sin bloquear el hilo de UI, y ninguna puerta borra en silencio ni deja de avisar), sin tocar el resto de la superficie.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Sdk/Services/IDialogService.cs` | **`ConfirmAsync`**: la confirmación asíncrona del contrato, con implementación por defecto que delega en la síncrona en un hilo de fondo (el escritorio y los dobles no cambian). |
| `Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | La **regla**: `DeletePresetAsync` y `ResetDefaultsAsync` **esperan la respuesta real** y sólo destruyen si el usuario dijo que sí. |
| `App.Core/ViewModels/NodeParameterViewModel.cs` · `NodeViewModel.cs` | Las **dos puertas** resuelven el servicio del host: la fila deja de caer en el Nulo que auto-confirma. |
| `App.Uno/Platform/UnoDialogService.cs` | `ConfirmAsync` del host: pregunta dentro del modal abierto si lo hay, y en su propio modal si no. |
| `App.Uno/Platform/UnoWindowService.cs` | `AskInsideActiveDialogAsync`: la pregunta como **CAPA dentro del cuerpo del modal** (con su velo, su tarjeta y sus dos botones reales), **sin bloquear el hilo de UI**; `TearDownInlineQuestion` la retira y la contesta «no» cuando la abandona el cierre del modal o la sustituye otra pregunta. |
| `App.Uno/Controls/MediaPresetManagerBody.xaml.cs` | `ResetAction` · `DeleteAction`: las **dos** órdenes destructivas, expuestas para poder medirlas. |

### 🐛 Lo que encontró la medición (y quedó arreglado)
`AskInsideActiveDialogAsync` montaba la capa **sacando el cuerpo de su diálogo** para volver a colgarlo de un `Grid`: **WinUI no deja colgar un elemento de dos padres** y la llamada lanzaba `COMException` (medido: el aviso de «Guardar» y el borrado fallaban con excepción). Ahora la capa se monta **dentro** del cuerpo (el cuerpo sigue siendo el contenido del diálogo). Y el **aviso informativo** —una capa con una sola salida— dejaba el estado tomado: la pregunta siguiente se declinaba **en silencio**, así que la orden no hacía nada *y no avisaba*; ahora **la sustituye**, contestando «no» la anterior.

### 🛡️ Guardia, pruebas y mutaciones
`UnoNodeDialogsGuardTests` **12 de 12** (el caso del contrato destructivo gana la sustitución, la retirada de la capa, el montaje dentro del cuerpo y los **dos** caminos de abandono, más la medición de la segunda puerta y del aviso). `MediaPresetManagerViewModelTests` **10 casos**. **Dos mutaciones nuevas, las dos MUERDEN**: `gestor-que-borra-sin-preguntar` (33,5 s) y `pregunta-de-borrado-por-la-via-sincrona` (30,9 s), con testigo rojo y control verde. `COVERAGE.md` regenerado por su guardia: **90 declaradas · 15 de 17 subsistemas**.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** / **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 46 `[OK]` · 0 `[FALLO]`** (antes 33: **+13**) / **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias / pruebas | **12 de 12** / **10** del view model, 0 rojos |
| Mutaciones | **2 nuevas, las 2 MUERDEN** · **90 declaradas** |
| Suite completa | **1930 superadas + 1 omitida de 1931, 0 errores** (2 m 38 s) |
| Rojo intermitente | **no apareció** |
| Sesión con la app abierta | **42 de 42 pasos** (`qa-manual-263`) |

### 🟢 Ejercido con la aplicación abierta
Por **cada puerta**, el ciclo destructivo completo: alta **10 → 11** · «Eliminar» **pregunta** (anclas `HostConfirmationAccept`/`HostConfirmationCancel` en el árbol; el centro pasa a `#B0ACAC` por el velo) · **con la pregunta en pantalla siguen 11** · **cancelar deja 11** · **confirmar deja 10**. Y «Restablecer» pregunta con su opción de cancelar: cancelarlo deja el catálogo del usuario intacto. Al final, lienzo con **3 tarjetas**, almacén **byte-idéntico** (`9b1e8f19477c5ebc3605ac373a38b38b`) y **ajustes del usuario intactos**.

### 🟠 Fronteras declaradas
- **El mismo patrón sigue en seis órdenes destructivas del host** (cerrar/nuevo flujo con cambios sin guardar ×2, restablecer un tema, limpiar el VFS, borrar un modelo descargado, quitar un dataset sintético): todas usan la confirmación **síncrona**, que en un host WinUI **no muestra nada y no hace nada**. Quedan **declaradas y localizadas**, con el mismo arreglo de una línea + su guardia para el próximo tramo: cambiarlas aquí habría sido tocar seis superficies fuera del encargo.
- **Dos preguntas a la vez** se resuelven **sustituyendo** la anterior (contestarla «no»), no encolándose.
- **El escritorio no cambia**: conserva su confirmación síncrona y `ConfirmAsync` delega en ella.
- **Defecto del INSTRUMENTO arreglado en este tramo**: la sonda comparaba el **escapado** del fichero del almacén (hex en mayúsculas contra minúsculas) y daba por fallido un guardado correcto; ahora lee el JSON y compara el valor. Y el informe guarda el **marco** de cada excepción, no sólo su mensaje.

### 📄 Evidencia
[`docs/qa/qa_presets_confirm_263.md`](file:///docs/qa/qa_presets_confirm_263.md) + `docs/qa/qa-manual-263/` (`presets-session.json`, capturas `85_*_pregunta.png`) + `selfcheck-dialogs-report.txt`.

---

## [2026-09-28] - Hito 263: El Gestor de Presets de Medios del host Uno (y la Puerta que le Faltaba a la Tarjeta)

### 🎯 Objetivos y Alcance
Portar al host Uno la superficie del **Gestor de Presets de Medios** del escritorio, con **paridad de comportamiento**: sobre **view models portables del núcleo**, con su **punto de entrada donde el escritorio lo tiene** y **sin lógica de producto en la vista**. Si ya estuviera portada, declararlo con prueba y no rehacerla.

### 🔍 Lo que se encontró
El gestor existía **sólo** como **ventana Avalonia del plugin** (`FileFlow.Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml(.cs)`) con toda su lógica en el code-behind: **no había nada que rehacer en el host Uno, porque no había nada**. Se portó con el patrón del **Diseñador de Datasets (261)**: la superficie la **declara el NODO** con el contrato del SDK y cada host la pinta sobre el **mismo** view model portable.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Sdk/Descriptors/INodeDialogSurfaceProvider.cs` | Ampliado con `ReplacesCustomActionId`: el hilo que une el botón de la TARJETA y el de la FILA con la superficie declarada. |
| `Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | El view model **portable** (sin toolkit): catálogo, formulario, alta/guardado/borrado/restablecimiento, normalización de la extensión y protección de los presets del sistema. **Es quien escribe en el almacén.** |
| `Plugin.Integrations/UI/Services/IMediaPresetStore.cs` | El contrato del almacén; `MediaPresetManagerService` lo implementa. |
| `Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml(.cs)` | La ventana del ESCRITORIO, **refactorizada a vista** del mismo view model: sus manejadores propios de guardar y borrar desaparecieron. |
| `Plugin.Integrations/MediaTranscoderNode.cs` | Declara su superficie (`DialogKeys.MediaPresetManager`, `ReplacesCustomActionId => "ManageMediaPresets"`) y entrega el view model portable. |
| `App.Core/ViewModels/NodeParameterViewModel.cs` · `NodeViewModel.cs` · `HostUi.cs` | Las **dos puertas** abren la superficie declarada por el catálogo de ventanas del host; el host Uno fija además `CoreDialogHost.Services`. |
| `App.Uno/Controls/MediaPresetManagerBody.xaml(.cs)` | La vista del host sobre el view model portable: **ni un cuadro suyo escribe en el almacén**. |
| `App.Uno/Platform/UnoWindowService.cs` · `NodeInspectorPanel.xaml.cs` | La clave **servida** con su vista (censo **10 servidas + 1 declarada**) y el botón «🎬» de la **fila** (`ParamPreset_`). |
| `App.Uno/Controls/NodeCardView.xaml` · `NodeCardViewModel.cs` | **La puerta que faltaba** (ver abajo). |
| `App.Uno/Resources/Strings*.resx` | **22 claves** nuevas en EN+ES. |

### 🚪 La puerta que le faltaba a la tarjeta (el defecto que encontró la medición)
El botón «🎬 Presets...» de la tarjeta vive en el panel de acciones rápidas, y ese panel cuelga de `Node.IsExpanded`… **y el host Uno no tenía ningún control que conmutara ese estado** (el escritorio lo hace con un `ToggleButton` de la cabecera). La acción estaba **dibujada y sin puerta**: el usuario no podía alcanzarla. Arreglado con el estado **del núcleo** (`NodeCardExpandToggle`: chevron arriba/abajo, dos vías con `Node.IsExpanded`, rótulo del diccionario del host con **la misma clave que el escritorio**, geometría en el adaptador y `IsExpanded` en el refresco agregado). La sonda de lienzo gana **5 comprobaciones** y la guardia exige las tres piezas.

### 🛡️ Guardia, pruebas y mutaciones
`UnoNodeDialogsGuardTests` **11 de 11** (10 + `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive`), que ata el conmutador, el estado del núcleo que conmuta y el bloque que cuelga de él, y amplía el censo de textos a las claves `PresetManager_*`, `Node_Param_*` y el rótulo del conmutador. **9 casos nuevos** del view model portable (`MediaPresetManagerViewModelTests`, con almacén falso y diálogos que anotan). **Dos mutaciones nuevas**, las dos **MUERDEN**: `tarjeta-sin-la-puerta-de-sus-parametros` (43,3 s) y `conmutador-de-parametros-que-no-refresca` (30,9 s). `COVERAGE.md` regenerado por su guardia: **88 declaradas · 15 de 17 subsistemas**.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** (antes 83) / **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 33 `[OK]` · 0 `[FALLO]`** (antes 24) / **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias / pruebas nuevas | **11 de 11** (`UnoNodeDialogsGuardTests`) / **9** del view model portable, 0 rojos |
| Mutaciones | **8 del tramo, las 8 MUERDEN** (2 nuevas) · **88 declaradas** |
| Suite completa | **1928 superadas + 1 omitida de 1929, 0 errores** (2 m 30 s) |
| Rojo intermitente | `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` rojo en **una** corrida y **verde en aislamiento 2 de 2** → ruido de carga; la corrida final, verde |
| Sesión con la app abierta | **34 de 34 pasos** (`qa-manual-263`) |

### 🟢 Ejercido con la aplicación abierta (sesión 263), 34 de 34 pasos
Driver UIA `qa_presets_uia.py`. Medido: 3 tarjetas de base y el almacén con **10 presets** → el nodo de transcodificación se añade por el cajón (buscando la **clave** `Transcoder`, que es lo que este host muestra) y su fila expone `ParamPreset_Preset` → **puerta A (la tarjeta)**: se despliega con su conmutador, aparece `🎬 Presets...`, y al pulsarlo el gestor con **10/10 anclas** y **10 filas** cuya primera es la del almacén, pixel `#FCF8F8` → **`#B0ACAC`** → cierra y el pixel vuelve → **puerta B (la fila)**: **la misma superficie**, el formulario trae el preset **elegido** con **su** descripción → se escribe en la **caja real** y «Guardar» deja la descripción en el **fichero** (`%AppData%\FileFlow\presets\media_presets.json`), sin cerrar el modal → al reabrir, la caja trae **lo guardado** → se restaura → **«Nuevo» lleva el almacén de 10 a 11 y «Eliminar» lo devuelve a 10** → el lienzo queda con 3 tarjetas, el almacén **byte-idéntico** (`9b1e8f19477c5ebc3605ac373a38b38b`) y los **ajustes del usuario intactos**.

### 🔍 Defectos del INSTRUMENTO que encontró la medición (para el guion futuro)
1. **El almacén no estaba donde el driver lo leía**: el modo instalado de `AppPaths.RootDirectory` es `%AppData%\FileFlow` (presets en `presets/`, preferencias en `config/`), no la carpeta vieja `%AppData%\FileFlowStudio`, que guarda copias de hace semanas: medir contra ella decía «Guardar no escribe» — falso. Corregido en el driver y en `qa_dialogs_uia.PREFS`.
2. **El cajón de este host muestra la CLAVE cruda del recurso** (`MediaTranscoderNode_Name`), no el texto resuelto: hay que buscar por la clave. La tarjeta del lienzo, en cambio, **sí** muestra el texto.
3. **Una tarjeta por nodo = un conmutador por tarjeta**, todos con la misma ancla: hay que elegir el de la tarjeta medida.

### 🟠 Frontera medida (defecto del PRODUCTO, declarado y NO arreglado)
Las órdenes **destructivas** del gestor **no piden confirmación** en el host Uno, y se comportan **distinto según la puerta**: por la **fila** el servicio que llega al view model es el **Nulo** (`ShowConfirmation => true`) y «Eliminar» borra **de verdad y sin diálogo** (medido 11 → 10); por la **tarjeta** llega el **del host**, cuyo `ShowConfirmation` es **síncrono** y devuelve «no» desde el hilo de UI, así que «Eliminar» **no borra** (medido 11 → 11) y tampoco muestra nada. Es la **frontera síncrona** del contrato de diálogos del núcleo; arreglarlo pide confirmación asíncrona en el SDK o comandos asíncronos en el gestor: **una rebanada, no un parche**.

### 📌 Censo definitivo de superficies de usuario del escritorio
**Portadas y probadas**: barra + cajón (**31 entradas** censadas, pendientes **VACÍA**); catálogo de diálogos **10 de 11 servidas**; ajustes (**6 secciones**); paneles de nodo (inspector, editor de texto, catálogo de variables y **el gestor de presets**); **4 acciones de fila** servidas; lienzo (tarjetas con su conmutador, sockets, cables, zoom, spotlight y atajos); y las siete ventanas del host. **No portadas, con razón**: el **gestor de CONTRASEÑAS** (declarado: una ventana del plugin con el toolkit que este host no tiene; **es la única superficie de usuario que queda sin portar**), el **menú emergente de variables** (el «{x}» abre el catálogo completo), **`WorkflowSettings`** como diálogo (sería una **segunda copia** de los ajustes del host) y `IPopupMenuService`/`IColorPickerService` (**declarado, no pendiente**). **Fuera de lo pedido queda SÓLO el empaquetado y la entrega** (fases **5.4-5.6** del plan de la rebanada 5, con su tamaño escrito en `docs/uno_slice5_plan.md` §11).

### 📄 Evidencia
[`docs/qa/qa_presets_host_263.md`](file:///docs/qa/qa_presets_host_263.md) + `docs/qa/qa-manual-263/` (capturas `80_…`-`99_…`, `presets-session.json`) + el driver `docs/qa/qa_presets_uia.py` + `selfcheck-dialogs-report.txt` / `selfcheck-report.txt`.

---

## [2026-09-28] - Hito 262: El Editor de URLs por Modelo de IA (El Punto de Entrada que Faltaba y Dónde Queda Escrito)

### 🎯 Objetivos y Alcance
El hito 261 dejó una frontera **declarada**: la pestaña de **Modelos de IA** existía, pero **no ofrecía la edición de URLs por modelo desde la fila** —la acción con la que el escritorio abre `AiModelUrlsConfig`—, así que su clave no podía servirse («servirlo sería una ventana que nadie puede abrir»). Este tramo **le da el punto de entrada y sirve la ventana** sobre el view model portable que ya existía, y deja escrito **dónde queda el cambio**: en el almacén del gestor del núcleo, porque lo escribe el propio view model portable, no la vista.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Controls/SettingsPanel.xaml` | La **acción de URLs** de la fila (`Tag="urls"`, ancla `SettingsAiModelUrlsButton`) en el mismo puesto que en la fila del escritorio: entre descargar y borrar. |
| `Controls/SettingsPanel.xaml.cs` | Su **rama propia**: ejecuta la orden **canónica** del gestor (`ConfigureUrlsCommand`). Sin ella, el `default` la sustituía y pulsar «URLs» **descargaba el modelo**. |
| `Controls/AiModelUrlsConfigBody.xaml(.cs)` | La vista del `AiModelUrlsConfigViewModel` portable: caja en **dos sentidos al teclear**, recuento, distintivo de estado, probar, restablecer y los resultados de la prueba. |
| `Platform/UnoWindowService.cs` | `AiModelUrlsConfig` **servida** con su vista y su arm en el catálogo; fuera de `DeclaredPendingDialogs`: el censo queda en **10 servidas + 1 declarada**. |
| `Resources/Strings*.resx` | Las **10 claves** `AiModelUrls_*` del escritorio copiadas en EN+ES, más una del host (`Uno_AiModelUrls_RequiredWarning`, la frase del propio view model). |

### 🛡️ Guardia y mutaciones
La prueba nueva (`TheModelUrlAction_ShouldOpenTheServedEditor_AndWriteWhereTheDesktopWrites`) ata **las tres mitades**: la fila dibuja exactamente `download`/`delete`/`urls`; la rama de la acción ejecuta `ConfigureUrlsCommand` —**no** la descarga del `default`—; la clave está **servida** con su arm y **sin** seguir declarada; y el cuerpo es una vista del view model portable que **no** escribe la configuración por su cuenta (no contiene `SetCustomUrls`; dos sitios escribiendo lo mismo serían dos verdades). La mutación nueva (`accion-de-urls-que-descarga-el-modelo`) quita esa rama y **MUERDE** (32,5 s): un botón que hace otra cosa es peor que uno que no hace nada. **Una mutación anterior se retiró** (`accion-de-urls-por-modelo-sin-declarar`) porque vigilaba que la fila **no** dibujara la acción, y este tramo la dibuja: dejarla habría sido un mutante que ya no mide nada. Declaradas: **80**. Y **una guardia del repositorio salió roja al cambiar el producto** —`UnoControlBarParityGuardTests` exigía que esta clave siguiera declarada— y se actualizó: es la señal de que el censo es producto vigilado.

### 🟢 Ejercido con la aplicación abierta (sesión 272), 24 de 24 pasos
Driver externo por UIA (`qa_urls_uia.py`). Medido: el catálogo con **24 filas** y **MobileNetV2 ImageNet** la primera → se pulsa la **acción de URLs de esa fila** → el editor aparece con **7 anclas** y hablando del **mismo modelo**, pixel `#FCF8F8` → **`#3C3C3C`** → teclear **2 URLs** lleva el recuento del view model de **`1 URL(s)` a `2 URL(s)`** → Guardar cierra el modal (pixel de vuelta a `#585454`, el de la superficie abierta detrás) → **al reabrir, la caja trae las dos URLs Y el distintivo pasa de `📦 Oficial / Predeterminado` a `🔧 Personalizado`**: el cambio quedó escrito donde lo escribe el escritorio → se restaura (`🔧` → `📦`) → preferencias md5 **byte-idénticas** (`d5f199a068113d8a7e16ad6ee6f726b3`).

### 🔍 Tres defectos que encontró la medición (uno del producto, dos del instrumento)
1. **Del producto**: la caja del editor enlazaba `Text` sin `UpdateSourceTrigger`, así que en WinUI escribía **al perder el foco** y el recuento se quedaba con el valor viejo mientras el usuario teclea (el escritorio lo actualiza al teclear). Corregido en el enlace.
2. **Del instrumento**: `window_text()` de una **fila enlazada** devuelve el nombre del **tipo del view model** (`FileFlow.App.ViewModels.AiModelItemViewModel`), no lo que se ve; el nombre vive en el `AutomationProperties.Name` del panel de la fila.
3. **Del instrumento**: el píxel tras cerrar el modal **no vuelve al del lienzo** sino al de la **superficie de ajustes que sigue abierta detrás** (`#585454`); comparar contra la línea base medía mal el producto.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 83 `[OK]`** / **EXIT 0 · 42 `[OK]`** · VERIFICADO |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 18 `[OK]`** · VERIFICADO (antes 12) |
| Guardias | **13 + 13 + 9 + 5 = 40** de 40 |
| Mutaciones | **6 de 6 MUERDEN** (1 nueva + 5 corroboradas) · **80 declaradas** |
| Suite completa | **1917 superadas + 1 omitida de 1918, 0 errores** (2 m 24 s) |
| Rojo intermitente | **2 corridas con un rojo distinto cada una** (`EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` y `SystemPerformanceMonitorTests.TheHeartbeat_ShouldPublishAPlausibleSample`), **los dos verdes en aislamiento (1 de 1)** → **ruido de carga**, no regresión; la corrida final, verde |
| Sesión con la app abierta | **24 de 24 pasos** |

### 📌 Fronteras declaradas
1. **El aviso de «URL requerida» no sale como segundo `ContentDialog`** (WinUI sólo admite uno y el editor ya está abierto): la petición del view model queda escrita en la consola y **el host la repite dentro del editor**, sin cerrar el modal sobre algo rechazado.
2. **`WorkflowSettings` sigue siendo la única clave declarada**: su superficie tiene puerta en la barra y el cajón.
3. **Hallazgo del escritorio, anotado y NO tocado**: su `AiModelUrlsConfigDialog` enlaza `{Binding SaveCommand}`, que su view model no expone; ese botón no guarda. El host no hereda el defecto (llama al mismo `Save()`) y el escritorio no se tocó.

---

## [2026-09-28] - Hito 261: El Diseñador de Datasets y las Dos Pestañas de Ajustes que Faltaban (El Contrato de Superficie del SDK)

### 🎯 Objetivos y Alcance
El hito 260 dejó **8 claves servidas + 2 declaradas**, y de esas dos la única **entrada de menú** sin superficie era el **Diseñador de Datasets**. Su ventana la monta el **propio plugin** con el toolkit del escritorio —un host WinUI no puede montar una ventana ajena—, así que el tramo anterior la había dejado declarada «con su razón exacta». Este tramo la **cruza sin reimplementar el diseñador**: un **contrato NUEVO del SDK** deja que el **nodo** declare qué diálogo quiere y qué contiene, y el host pinta esa clave con **su propia vista sobre el view model PORTABLE del plugin**. Además se sirven las **dos pestañas de ajustes** que faltaban —**Modelos de IA** y **Actualizaciones**— sobre sus secciones del núcleo, y `DeclaredPendingEntries` queda **VACÍA**: ya no hay ninguna orden de menú del escritorio sin dibujar, declarar o cumplir por el host.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs` | **Contrato nuevo del SDK**: el nodo dice **qué** diálogo quiere (`DialogKey`, la misma clave para todos los hosts) y **qué** contiene (`Payload`, su view model portable). La identidad del diálogo deja de decidirla el host. |
| `Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs` | Implementa el contrato: declara `DialogKeys.DataSetDesigner` y entrega su `SyntheticDataSetDesignerViewModel`. |
| `Controls/DataSetDesignerBody.xaml(.cs)` | La vista del host sobre ese view model: buscador, catálogo, las tres pestañas (árbol / DSL / JSON) y las órdenes de añadir y quitar. **Cero lógica de producto**: sus órdenes **son los comandos del VM del plugin**. |
| `Controls/SettingsPanel.xaml(.cs)` | Las dos pestañas nuevas —**Modelos de IA** (catálogo, carpeta, estado, descargar / borrar por fila) y **Actualizaciones** (versión, formato, canales, comprobación automática)— sobre sus secciones portables. La superficie pasa a **seis secciones**. |
| `Controls/MainMenuDrawer.xaml(.cs)` | La entrada **Diseñador de Datasets**, cumplida por el evento propio del cajón; el cajón pasa de 14 a **15 entradas** ancladas. |
| `Controls/ControlBar.xaml.cs` | `DeclaredPendingEntries` **vacía** (la tabla se conserva con su guardia para que la próxima orden sin destino tenga dónde declararse) y `OpenSyntheticDataSetDesignerCommand` añadida a `HostOwnedOrders` con su mecanismo. |
| `Platform/UnoWindowService.cs` + `IWindowService.cs` | `DataSetDesigner` **servida** con su vista; el censo pasa a **9 servidas + 1 declarada**. La razón de `AiModelUrlsConfig` se **corrige** (§7 del QA). |

### 📐 La paridad, escrita
**31 entradas censadas** (16 de la barra + 15 del cajón); **9 claves de catálogo servidas** con su vista y **1 declarada con su razón** (`WorkflowSettings`: abrirla por aquí sería una SEGUNDA copia de la superficie de ajustes del host); **cinco órdenes del escritorio** cumplidas por el canal propio (`HostOwnedOrders`); **cero entradas declaradas pendientes** y **cero atajos sin enrutar**.

### 🛡️ Guardia y mutaciones
La guardia sube a **13 + 13 + 9 + 5 casos** entre los cuatro ficheros: el censo de diálogos del servicio, la tabla `HostOwnedOrders` para el diseñador y el contrato del nodo (`TheDataSetDesigner_ShouldBeDeclaredByTheNode_AndServedByTheHost`), más la sección nueva de ajustes. Y **un caso nace de lo que este tramo encontró a ojo**: `TheAiModelRowActions_ShouldMatchWhatTheDialogCensusDeclares` ata las acciones dibujadas en la fila de modelos a lo que el censo de diálogos declara (ver «Una razón que había quedado falsa»). **Seis mutaciones muerden**: cinco nuevas (`nodo-que-declara-su-superficie-sin-clave`, `disenador-de-datasets-fuera-del-catalogo`, `vista-del-disenador-con-su-propio-modelo`, `seccion-que-pierde-el-panel-que-conmutaba`, `accion-de-urls-por-modelo-sin-declarar`) y `menu-que-no-declara-lo-que-falta` **reapuntada** a la tabla que cambió de estado. **Dos guardias del repositorio salieron rojas al cambiar el producto** (la del host libre de Avalonia —una razón declarada nombraba el ensamblado— y la del inventario de trabajo aplazado —la espera del arranque, registrada `RealTime` con su motivo—): es la señal de que las tablas de declaración son producto vigilado, no prosa.

### 🟢 Ejercido con la aplicación abierta (sesión 271), 39 de 39 pasos
Driver externo por UIA (`qa_windows2_uia.py`) + vigilante de píxeles. Las **dos ventanas que el pase anterior no había ejercido** y las dos nuevas: **Explorador VFS** (botón de la barra, chip `📁 | VFS (4)`) → **5 anclas**, **4 filas** con nombre real (`Sembrado 01..04.mkv`), pixel `#B0ACAC`, y al cerrar `#FCF8F8` y **0 filas**; **aviso de actualización** (distintivo `🚀 | v9.9.9`) → **6 anclas**, versión actual `1.0.0-beta+build.6759` contra `9.9.9`, pixel `#B0ACAC`, y al cerrar la superficie se va **pero el distintivo sigue puesto**; **Diseñador de Datasets** (entrada del cajón) → **8 anclas** y **7 filas** de dataset, pixel `#B0ACAC`; **Ajustes** → **6 secciones**, pestaña Modelos de IA con **24 filas** y carpeta `…\FileFlow\models`, pestaña Actualizaciones con su versión. `preferencias.md5` **idénticas** (`d5f199a068113d8a7e16ad6ee6f726b3`).

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 37) |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 12 `[OK]`** · VERIFICADO (antes 9) |
| Guardias | **13 + 13 + 9 + 5** = **40 casos** |
| Mutaciones | **6 de 6 MUERDEN** (5 nuevas + 1 reapuntada) · **80 declaradas** |
| Suite completa | **1917 superadas + 1 omitida de 1918, 0 errores** (2 m 26 s) |
| Sesión con la app abierta | **39 de 39 pasos** · 3 tarjetas |
| Guardias del repositorio que salieron rojas | **2 y las dos se arreglaron** |

### 📌 Fronteras declaradas
1. **El Diseñador de Datasets se sirve por el contrato del SDK, no por el comando canónico**: el comando del escritorio abre la ventana que monta el plugin con el toolkit del escritorio. El cajón lo cumple con su evento propio, la ventana pide al nodo la superficie declarada y el **catálogo de diálogos del host** la sirve con SU vista sobre ese mismo view model.
2. **`WorkflowSettings` (la clave del catálogo) sigue declarada y no servida**: abrirla por `IWindowService` sería una SEGUNDA copia de la superficie de ajustes que el host ya tiene en la barra y el cajón.
3. **`AiModelUrlsConfig` sigue declarada**, con razón corregida **y ahora vigilada**: la pestaña de modelos de IA del host lista el catálogo y gestiona descargas, pero **no ofrece la edición de URLs por modelo desde la fila**, que es la acción con la que el escritorio abre ese diálogo. Servirlo sin punto de entrada sería una ventana que nadie puede abrir. (La razón anterior —«esa pestaña no existe aquí»— había quedado falsa al añadirla este tramo; una razón obsoleta miente igual que un no-op mudo. **La encontró el ojo, así que las dos mitades quedan atadas por una prueba y por una mutación que muerde**: si alguien dibuja esa acción, la declaración deja de ser cierta y el caso cae nombrando la clave.)
4. **Hallazgo del escritorio, anotado y NO tocado**: el botón Guardar del `AiModelUrlsConfigDialog` enlaza `{Binding SaveCommand}`, que el view model **no expone** (tiene `Save()` sin `[RelayCommand]`). Ese diálogo no puede guardar en el escritorio. Antes de portarlo «con paridad» hay que decidir cuál es el comportamiento correcto.
5. **El `FileInfoText` del diseñador no se dibuja**: el escritorio lo rellena desde su catálogo de modelos y aquí no hay fuente; se dibujan los cuatro campos que el `VirtualFileEntry` del núcleo sí expone.

---

## [2026-09-28] - Hito 260: Las Ventanas que Faltaban del Menú del Host Uno (Las Cuatro Puertas del Catálogo de Diálogos)

### 🎯 Objetivos y Alcance
El hito 259 cerró la mitad del menú y dejó **cinco entradas declaradas**; cuatro de ellas abrían una **ventana** del escritorio que este host no tenía. Este tramo las **sirve por el catálogo de diálogos** (`DialogKeys`) sobre los **view models PORTABLES del núcleo** —cero lógica de producto en la vista—: el **Estudio de Temas**, las **Métricas**, el **Explorador Virtual (VFS)** y el **aviso de actualización**. El censo del servicio pasa de **3 servidas + 6 declaradas** a **7 + 2**, y la única que queda declarada —el **Diseñador de Datasets**— lleva ahora su razón exacta: su ventana la monta el propio plugin con su toolkit, y servirla pide una vista del host sobre un view model que vive dentro de su ensamblado.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Controls/ThemeCustomizerBody.xaml(.cs)` | El **Estudio de Temas**: catálogo del núcleo, editor por secciones generado desde `ThemeSettingCatalog` (9 secciones, **34 ajustes editables**) y sus tres plantillas (color con su muestra, número con su rango y su paso, elección) repartidas por un **selector por TIPO de fila** —añadir un ajuste al catálogo no toca la vista—. Declara en `DeclaredPendingParts` lo que no sirve (Eliminar / Importar / Exportar, que piden el contrato SÍNCRONO de diálogos, y la vista previa en vivo). |
| `Controls/MetricsDashboardBody.xaml(.cs)` | El **panel de Métricas**: las cuatro tarjetas y las **siete columnas** del escritorio, leídas del `WorkflowMetricsDashboardViewModel` —él formatea, la vista pinta—, con una cabecera y una plantilla de fila (WinUI no trae `DataGrid`). |
| `Controls/VirtualFileSystemExplorerBody.xaml(.cs)` | El **Explorador VFS**: el host construye el `VirtualFileSystemExplorerViewModel` con el almacén que llega como carga útil (como el `AvaloniaWindowService` del escritorio) y la vista enlaza buscador, lista, selección y metadatos. Dibuja los cuatro campos que el `VirtualFileEntry` SÍ tiene. |
| `Controls/UpdateDialogBody.xaml(.cs)` | El **aviso de actualización**: versiones, formato del paquete, novedades y progreso del `UpdateDialogViewModel`; sus tres órdenes son sus comandos y el cierre lo pide el propio view model por `RequestClose` (el servicio retira el modal). |
| `Platform/UnoWindowService.cs` | Las cuatro claves **servidas** con su vista y las dos que quedan **declaradas con su razón**; el cuerpo como superficie del host (misma decisión que «Acerca de») con su **clave de catálogo como ancla** (`ActiveWindowKey`) y un `CloseActiveWindow()` que usan los pies de las ventanas. Una carga útil que no es la esperada se **declina con su motivo**, nunca en silencio. |
| `Controls/ControlBar.xaml.cs` | Las dos entradas con **estado de contexto** de la barra: el chip **VFS (`HasVirtualFiles`)** con su recuento y el **distintivo de actualización (`HasPendingUpdate`)** con la versión nueva. La tabla nueva `ServedWindowEntries` deja escrito dónde vive cada una de las cuatro. La tabla de **declaradas baja a una fila**. |
| `Controls/MainMenuDrawer.xaml(.cs)` | Las tres entradas del cajón (Estudio de Temas, Métricas y VFS) ejecutando las **órdenes CANÓNICAS** del núcleo; el cajón pasa de 11 a **14 entradas** ancladas. |
| `App.xaml.cs` + `MainWindow.xaml.cs` | La **comprobación de actualizaciones del arranque**, la misma del escritorio (en segundo plano, sin forzar, respetando la versión ignorada) y **saltada entera en los modos de sondeo**; entrega la novedad a la ventana, que es quien enciende el distintivo. Sin esa mitad, el aviso que el host ya sirve no lo pediría nadie. |
| `Resources/Strings*.resx` | **192 claves copiadas** del diccionario del escritorio en los dos idiomas (ThemeStudio, Metrics, VfsExplorer, Update, Drawer_*): los view models portables piden sus textos por clave y el host los resuelve con los suyos, así que el editor y las ventanas salen en el idioma elegido. |

### 📐 La paridad, escrita
**28 entradas censadas** (16 de la barra + 14 del cajón, con las 4 de ventana); **4 ventanas servidas** (2 nuevas claves de catálogo además de las 2 del 258 y la del 259) y **2 declaradas con su razón**; **una entrada declarada pendiente** (el diseñador de datasets del plugin, con la frontera del toolkit escrita); **4 claves de catálogo** con su vista en `ImplementedDialogs`.

### 🛡️ Guardia y mutaciones
`UnoControlBarParityGuardTests` pasa a **12 casos**: `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` (la tabla `ServedWindowEntries` + cada orden DIBUJADA fuera de las tablas + toda clave del SDK con destino), `TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt` y `TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes` (la **llamada**, no sólo la definición). **Siete mutaciones muerden**: cuatro nuevas (`ventana-servida-que-no-esta-en-el-catalogo`, `entrada-de-ventana-que-no-ejecuta-su-orden`, `aviso-de-actualizacion-que-nadie-enciende`, `estudio-de-temas-que-esconde-lo-que-no-sirve`), `menu-que-no-declara-lo-que-falta` **reapuntada** a la fila que queda, y dos de los hitos 257/258 re-verificadas. **Una debilidad de la guardia nueva la encontró la mutación**: buscar las órdenes en el texto de las vistas se conformaba con la propia tabla que las nombra, así que vaciar un manejador no caía; se añadió `WithoutDeclarationTables` y la mutación pasó de sobrevivir a morder.

### 🟢 Ejercido con la aplicación abierta (sesión 270), 25 de 25 pasos
Driver externo por UIA (`qa_menu3_uia.py`). Medido: cajón con **14/14 entradas** y su velo (`#FCF8F8` → `#585454`); **«Estudio de Temas»** → **6/6 anclas**, **9 temas** leídos por el canal externo («🌙 Oscuro Fluent», «☀️ Claro Minimalista»…), título del modal localizado, pixel **`#B0ACAC` 46,7 %** y el cajón recogido al elegir; **«Métricas»** → **4/4 anclas**, **3 filas** (una por nodo) y el pie «**3 nodos analizados. 0 cuello(s) de botella.**», pixel **`#B0ACAC` 49,4 %**; cerrar cada una devuelve el pixel a **`#FCF8F8` 77,8 %** y deja el árbol sin sus anclas; **preferencias md5 idénticas** (`d5f199a068113d8a7e16ad6ee6f726b3`).

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 37 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 25) |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 9 `[OK]`** |
| Guardia del menú | **12 de 12** (antes 9) |
| Mutaciones | **7 de 7 MUERDEN** (4 nuevas + 1 reapuntada + 2 re-verificadas) · **75 declaradas** |
| Suite completa | **1915 superadas + 1 omitida de 1916, 0 errores** (2 m 29 s en la corrida del tramo; **re-verificada al cierre, 2 m 26 s**) |
| Sesión con la app abierta | **25 de 25 pasos** |
| Guardias del repositorio que salieron rojas | **2 y las dos se arreglaron en el producto o en su registro** (la del host libre de Avalonia —una razón declarada nombraba el ensamblado— y la del inventario de trabajo aplazado —la espera nueva del arranque, registrada `RealTime` con su motivo—) |

### 📌 Fronteras declaradas
1. **Las cuatro ventanas son superficies modales dentro de la ventana del host**, no ventanas nuevas: el mismo criterio de «Acerca de» (hito 258).
2. **El Estudio de temas no dibuja Eliminar / Importar / Exportar** ni la vista previa en vivo: los tres primeros dependen del contrato SÍNCRONO de diálogos (desde el hilo de UI devuelve «no»/nulo) y la vista previa necesitaría una copia propia de tokens. Todo declarado en `DeclaredPendingParts`, con su razón.
3. **El canal del VFS con el almacén real de una ejecución** se mide con su estado de contexto (el chip de la barra) y con un almacén construido por la sonda por el MISMO camino del servicio; no se ejecutó un flujo que produjera archivos virtuales.
4. **El aviso de actualización se ejerció con una novedad sintética** (`v9.9.9`); en la aplicación normal sólo aparece con una release nueva de verdad.
5. **Hallazgo del escritorio, anotado y no tocado**: su rejilla del VFS declara «Tamaño» y «Modificado» enlazando a propiedades que no existen en el `VirtualFileEntry` del núcleo —columnas vacías en silencio—.

---

## [2026-09-28] - Hito 259: Las Entradas y los Atajos que Faltaban del Menú del Host Uno (El Contrato Síncrono, Cruzado por el Canal Asíncrono del Host)

### 🎯 Objetivos y Alcance
El hito 257 portó la barra de control y su cajón, y dejó **once entradas y seis atajos declarados pendientes** —en parte esperando al servicio de ventanas del 258—. Este tramo cierra esa mitad sin rehacer nada de lo portado: **tres órdenes de flujo** (Nuevo / Cargar / Guardar) cumplidas por el canal propio del host, **tres entradas de ayuda** (Manual / Ejemplos / Acerca de) por sus órdenes canónicas —con «Acerca de» ya como superficie real— y los **seis atajos** enrutados.

### 🔴 La frontera que era el bloqueo real (y cómo se cruza)
Las tres órdenes de flujo no estaban pendientes por falta de tiempo: su comando del núcleo pide un diálogo **SÍNCRONO**, y desde el hilo de UI este host devuelve `null` en el picker y `false` en la confirmación (medido y declarado desde el 240 en `UnoFileDialogService` / `UnoDialogService`). Dibujar la entrada y ejecutar el comando canónico habría sido **un botón que no hace nada**, sin crash y sin mensaje. El cruce no reimplementa el flujo en el host: **separa el diálogo de la operación en el view model portable**, que es la regla que el 254 ya usó con «cargar un flujo» —`ControlBarViewModel.CreateNewWorkflow()` y `SaveWorkflowToFileAsync(path)`, simétricos de `LoadWorkflowFromFileAsync`— y el host aporta lo que sí sabe hacer: `UnoDialogService.ShowConfirmationAsync` (la confirmación que se puede esperar sin bloquear el hilo de UI) y los pickers asíncronos de WinRT.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `MainMenuDrawer.xaml(.cs)` | **Dos secciones nuevas**, en el orden del escritorio: **GESTIÓN DE FLUJOS** (Nuevo / Cargar / Guardar) y **AYUDA Y RECURSOS** (Manual / Ejemplos / Acerca de). El cajón pasa de 5 a **11 entradas** ancladas; las de flujo declaran *qué se ha pedido* por evento y las de ayuda ejecutan la orden canónica. |
| `MainWindow.xaml.cs` | Las tres manos de flujo (confirmación y pickers asíncronos + los métodos portables) y el **enrutado del teclado**: lo que el lienzo no reclama llega a la tabla de atajos del menú. |
| `Controls/ControlBar.xaml.cs` | La tabla **`RoutedShortcuts`** (6 filas: gesto, tecla, modificadores, orden y vía) que **es la que enruta** —el manejador la recorre; no hay un `switch` paralelo que se pueda desincronizar— y las tablas del censo actualizadas. |
| `Controls/AboutDialogBody.xaml(.cs)` | La ventana **«Acerca de»** del host: los mismos rótulos del escritorio (`Uno_About_*`, copiados), la versión de la misma fuente que el pie del cajón y las insignias de lo que este host es (`.NET 10.0`, `Uno Platform · WinUI 3`, `DAG Flow Engine`). |
| `Platform/UnoWindowService.cs` | `ShowWindow(DialogKeys.About)` servido y anclado (`AboutDialog`): el censo pasa de 2 servidas + 7 declaradas a **3 + 6**. |
| `App.Core/ViewModels/ControlBarViewModel.cs` | Los dos métodos portables sin diálogo del apartado anterior. |

Los textos nuevos (`Uno_Drawer_FlowManagement`, `Uno_Drawer_New/Load/SaveWorkflow`, `Uno_Drawer_HelpResources`, `Uno_Drawer_UserManual(+ToolTip)`, `Uno_Drawer_ExampleFlows(+ToolTip)`, `Uno_Drawer_About(+ToolTip)`, `Uno_About_*`) se **copian** del diccionario del escritorio, clave por clave, en los dos idiomas.

### 📐 La paridad, escrita
**25 entradas censadas** (14 de la barra + 11 del cajón); **4 órdenes cumplidas por el host** (`OpenWorkflowSettingsCommand` y las tres de flujo); **6 atajos enrutados** (F5 / F10 / Shift+F5 al comando del ciclo del núcleo; Ctrl+N / Ctrl+O / Ctrl+S por el canal del host) con `DeclaredUnroutedShortcuts` **vacía**; y **5 entradas pendientes con su razón**: Estudio de temas, métricas, VFS, diseñador de dataset y aviso de actualización (el host no comprueba actualizaciones).

### 🛡️ Guardia y mutaciones
`UnoControlBarParityGuardTests` pasa a **9 casos**: el nuevo `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` exige las dos mitades —las APIs asíncronas y los métodos portables, y **no** los `…Command.Execute` del núcleo ni el producto reimplementado en la vista— y el caso de atajos lee ahora **las dos tablas** (enrutados + declarados), con la exigencia de que ninguna contradiga a la otra. **🧬 Mutación nueva (71.ª): `flujo-que-se-cumple-por-el-picker-sincrono` → MUERDE** (35 s), y **dos mutaciones del 257 actualizadas al producto nuevo** (`menu-que-no-declara-lo-que-falta`, `menu-que-no-declara-un-atajo` —esta última borra ahora una fila de la tabla que enruta—) **también muerden**. La guardia de declaraciones **falló al cambiar el producto** y fue el aviso que hacía falta: dos mutantes habrían quedado mintiendo en silencio. COVERAGE → **71 declaraciones**.

### 🟢 Ejercido con la aplicación abierta (sesión 269), 27 de 27 pasos
Driver externo por UIA + **teclado FÍSICO** (`keybd_event`, el mismo canal que midió la sesión 268) con el **vigilante** midiendo (79 fotogramas, 15 cambios de escena). Medido: base `tarjetas=3` y **0 anclas del cajón** → «Menú» expone **11/11** entradas y el velo se ve en el pixel (`#FCF8F8` → **`#585454`**) → **«Acerca de»** abre la superficie del host (anclas `AboutDialog` / `AboutVersionText` / `AboutDescriptionText`; la versión leída por UIA: **`v1.0.0-beta+build.6664 · net10.0 · Uno Platform (WinUI 3)`**; el modal en el pixel: **`#B0ACAC` 67,1 %**) y al cerrarla el pixel vuelve a la base → **«Nuevo Flujo»** pide confirmación (**«¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.»**, pixel `#B0ACAC` 77,5 %) y **cancelar deja las mismas 3 tarjetas** → **Ctrl+N** por tecla física abre **la misma confirmación** y deja **el mismo pixel** → **F5** y **F10** por teclado físico quedan en el **rastro**: `menu atajo=F5 orden=ContinueWorkflowCommand` y `menu atajo=F10 orden=StepNextCommand`. Cierre: escena y tarjetas como al entrar y **preferencias del usuario byte-idénticas** (md5 `d5f199a068113d8a7e16ad6ee6f726b3`).

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 25 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 14) |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 9 `[OK]`** |
| Guardia del menú | **9 de 9** |
| Mutaciones | **3 de 3 MUERDEN** (la nueva + las dos del 257 actualizadas) |
| Suite completa | **1912 superadas + 1 omitida de 1913, 0 errores** (2 m 26 s) |
| Sesión con la app abierta | **27 de 27 pasos** |

### 📌 Fronteras declaradas
1. **Cinco entradas siguen pendientes** con su razón escrita (Estudio de temas, métricas, VFS, diseñador de dataset y aviso de actualización).
2. **«Acerca de» es modal aquí y ventana en el escritorio**: el host sirve la misma información dentro de su única ventana. Diferencia declarada.
3. **Las órdenes del núcleo siguen pidiendo el contrato síncrono**: el host las cumple por su canal, no cambiando el contrato; otro host tendrá la misma frontera y las mismas dos piezas portables para cruzarla.
4. **Ctrl+O y Ctrl+S no se pulsaron con tecla física** (abren el picker del sistema, que se lleva la sesión de UIA): su camino lo ata la guardia y su mitad sin diálogo se ejerció por la sonda (guardar y cargar sobre un fichero temporal, medido).
5. **La confirmación del host usa botones `OK`/`Cancel`**, como el adaptador de diálogos que ya existía: el escritorio no tiene esa confirmación con otros textos que copiar.

---

## [2026-09-28] - Hito 258: Los Paneles de Nodo del Host Uno: los Diálogos de Parámetro y el Selector de Variables, Sobre los View Models del Núcleo

### 🎯 Objetivos y Alcance
El host Uno tenía lienzo, paneles, atajos, ajustes y barra de control, pero **los paneles que cada nodo tiene dentro** —el editor de texto y prompts del parámetro largo y el selector de variables— seguían cayendo al **Nulo declarado**: el botón «✎» y el botón «{x}» existían y **no hacían nada** (sin crash y sin error en pantalla, el usuario pulsaba y no pasaba nada). Este tramo escribe el `IWindowService` del host, las dos vistas sobre los **view models portables del núcleo** y el **anclaje** que hace que las filas del inspector los alcancen, y **no toca ninguna otra superficie**.

### 🧱 Lo construido (tres piezas en el host, cero líneas en `FileFlow.App`)
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Platform/UnoWindowService.cs` | El `IWindowService` real: `ShowDialogAsync` por `DialogKeys` con `ContentDialog` y `DialogResultPayload`, `MainWindowOwner` real, y las dos tablas del censo — `ImplementedDialogs` (**2 servidas**) y `DeclaredPendingDialogs` (**7 declaradas con su razón**)—. Lo que el host no sirve **no se cancela mudo**: `Decline` escribe la clave y el motivo en `DeclinedDialogs` y en la consola de la aplicación, porque un «cancelado» sin traza se lee como un error del usuario. |
| `Controls/TextEditorDialogBody.xaml(.cs)` y `Controls/VariablePickerDialogBody.xaml(.cs)` | Las vistas de los VMs **portables**: el editor con su caja `TwoWay`, sus botones de insertar variable y limpiar y su panel lateral del **propio VM** (WinUI no admite dos `ContentDialog` a la vez), insertando por `vm.InsertTokenAt(caret, token)` y devolviendo `vm.SaveResult()`; el catálogo con lista de selección `TwoWay`, buscador que filtra en caliente, recuento, detalle del token y devolución de `vm.SelectedToken`. |
| `Controls/NodeInspectorPanel.xaml.cs` | Las **acciones de fila**: `HostRowActions` (**3 dibujadas**: explorar ruta «…», editor «✎», catálogo «{x}», con las anclas `ParamBrowse_` / `ParamEditor_` / `ParamVariable_`) y `DeclaredPendingRowActions` (**3 declaradas**). |

`App.xaml.cs` registra `services.AddSingleton<IWindowService, UnoWindowService>()` **y ancla** `ServiceHolders.WindowService` —de ahí lo leen los `NodeParameterViewModel` que el inspector construye **sin recibir servicios por constructor**: sin ese anclaje el servicio existe en el contenedor y las filas siguen en el Nulo—. Los textos de los dos diálogos son claves `Uno_Dialog_*` **copiadas del diccionario del escritorio**, clave por clave y en los dos idiomas.

### 🔴 El defecto REAL que destapó el driver (y que se arregló)
Las cajas de texto de las filas del inspector **sólo escribían en un sentido**: del campo al parámetro. Cuando el valor lo escribía **el diálogo** —insertar `{FileName}` desde el catálogo—, el parámetro del nodo cambiaba pero **el campo seguía mostrando el texto viejo**: el usuario habría visto su inserción desaparecer de la pantalla. **Arreglo**: `WireBoxToParameter(TextBox, NodeParameterViewModel)` (ida + escucha de `Value` → `box.Text = value;`) y una lista `_rowValueSubscriptions` que se suelta en `RebuildParameters()` (sin ella, reconstruir el inspector dejaría escuchas huérfanas), aplicado a las **tres** cajas (estándar, multilínea y ruta con explorar).

### 📐 La paridad, escrita (y lo que no llega, declarado)
El **censo de diálogos** reparte las **9** claves de `DialogKeys` entre **2 servidas** (`TextEditor`, `VariablePicker`) y **7 declaradas** con su razón (`UpdateDialog`, `WorkflowSettings`, `VirtualFileSystemExplorer`, `About`, `WorkflowMetricsDashboard`, `ThemeCustomizer`, `AiModelUrlsConfig`); la guardia lo compara **contra las constantes del SDK**, así que una clave nueva sin destino cae en la tabla. Dos declaraciones que son decisión, no olvido: **`WorkflowSettings`** no se sirve por esta vía porque su superficie (hito 255) ya tiene punto de entrada en la barra y el cajón y una segunda puerta sería **una segunda copia**; y el **«{x}»** del host abre **directo el catálogo completo** en vez del **menú emergente** del escritorio (el host no tiene `IPopupMenuService` y el catálogo **es** la primera entrada de aquel menú).

### 🛡️ Guardia y mutaciones
`UnoNodeDialogsGuardTests` (**9 casos**): el censo contra `DialogKeys`; cada pendiente **contestada con su razón** y no con un cancelar mudo; las órdenes de fila del escritorio con destino; las acciones dibujadas **en las mismas filas** que el escritorio (leído de `NodeParameterTemplates.axaml`); el **atado bidireccional** de las cajas; los diálogos como **vistas de los VMs portables** (`vm.SaveResult()`, `vm.InsertTokenAt`); los **20 textos** copiados del escritorio en los dos idiomas; el diccionario del host sin claves huérfanas; y la sonda en **modo propio**. **🧬 Cuatro mutaciones (68.ª-71.ª): `panel-de-nodo-sin-su-servicio-de-ventanas`, `fila-de-variables-que-abre-el-menu-que-no-esta-portado`, `editor-que-no-devuelve-el-texto-confirmado` y `campo-que-no-muestra-lo-que-el-dialogo-escribio` → las cuatro MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes; 34,8 s / 30 s / 29 s / 28 s). La cuarta **nació sobreviviente**: su primera versión (quitar la escucha) no moría, así que la guardia se endureció hasta exigir el atado completo (la escucha **y** su registro para soltarla) — y entonces mordió. COVERAGE → **70 declaraciones**, 15 de 17 subsistemas, guardias que auditan el repositorio con mutación que las muerda **15 de 44**.

### 🟢 Ejercido con la aplicación abierta (sesión 268), 25 de 25 pasos
Driver externo por UIA (`docs/qa/qa_dialogs_uia.py`) **actuando** sobre los controles reales con el **vigilante** de píxeles midiendo en paralelo. En la escena del ejemplo: base `tarjetas=3` y **0 filas `Param*`** → clic en la tarjeta **`Folder Source`** y aparecen **10 anclas `Param*`** → pulsar **«{x}»** de `ExtensionFilter` **abre el catálogo** y **el modal se ve en el pixel** (centro `#FCF8F8` → **`#B0ACAC` 52,6 %**) → el buscador escribe «Guid» y el recuento pasa de «47 de 47» a **«1 de 47»** y vuelve → elegir **`{FileName}`** llena el detalle y habilita «Insertar Variable» → pulsar y **el campo del nodo pasa de `''` a `'{FileName}'`** (la vuelta del §defecto), devuelto a `''`. Después, nodo real añadido por el cajón (`Registrar Log`): sus filas exponen `ParamBox_CustomMessage`, `ParamEditor_CustomMessage`, `ParamVariable_CustomMessage`, `ParamDropdown_LogLevel` y 3 toggles; pulsar **«✎»** abre el **editor** (`TextEditorBox`) **sembrado con el valor de la fila**; su «Insertar Variable» despliega el catálogo del VM (**13 variables**); escribir `'prompt de la sesion 268 + {FileName}'` y **«Guardar y Aplicar»** deja **ese texto en el parámetro del nodo**; y el nodo añadido se retira con `Supr`. **Preferencias del usuario byte-idénticas** (md5 `d5f199a068113d8a7e16ad6ee6f726b3`): los paneles de nodo no escriben nada del usuario. Nota metodológica medida: el **clic físico inyectado SÍ llega** al contenido de WinAppSDK (la casilla «Modo Prueba» conmuta 1→0→1), a diferencia del clic mediado por UIA.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** — los paneles no rompieron ninguna sonda anterior |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** · «RESULTADO: VERIFICADO» |
| `--selfcheck-settings` / `--selfcheck-controlbar` | **EXIT 0 · 9 `[OK]`** / **EXIT 0 · 14 `[OK]`** |
| Guardia de los paneles de nodo | **9 de 9** superados |
| Mutaciones 68.ª-71.ª | **4 de 4 MUERDEN** |
| Suite completa | **1910 superadas + 1 omitida de 1912** por corrida; el único rojo de cada una fue **un test distinto y pesado** (`EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` una vez, `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` otra), **verde en aislamiento** (1/1 y 4/4): **ruido de carga, no regresión** |
| Sesión con la app abierta | **25 de 25 pasos** |

### 📌 Fronteras declaradas
1. **Las 7 claves de diálogo que el host no sirve** (Actualizador, VFS, Acerca de, Métricas, Estudio de temas, configuración de modelos de IA y los ajustes por esta vía): declaradas con su razón, con traza de lo pedido y atadas por la guardia. Un botón que no puede abrir nada no se dibuja.
2. **El «{x}» abre el catálogo completo, no el menú emergente**: el host no tiene `IPopupMenuService` y el catálogo es la primera entrada de aquel menú.
3. **`WorkflowSettings` no se sirve por `IWindowService`** aunque la superficie exista: su entrada es la barra y el cajón; una segunda puerta sería una segunda copia.
4. **Las ventanas que el host no tiene** (dashboard, VFS, gestor de presets de medios, gestor de contraseñas) siguen pendientes, cada una en su tabla.
5. **Sigue pendiente** de la migración: las **11 entradas** y los **6 atajos** del menú del escritorio (hito 257), las pestañas **Actualizaciones** y **Modelos de IA** de los ajustes (hito 255) y el **empaquetado y la entrega** (fases 5.4-5.6 del plan de la rebanada 5).

---

## [2026-09-28] - Hito 257: El Menú Principal del Host Uno: La Barra de Control y su Cajón, Sobre el View Model del Núcleo

### 🎯 Objetivos y Alcance
El tramo de los ajustes dejó señalado su propio hueco: el **menú principal**. El host Uno tenía el botón de ajustes, la barra de zoom y los atajos del lienzo, pero **no la barra de control del escritorio ni sus menús**. Este tramo porta esa superficie —la barra (`FileFlow.App/Views/ControlBarView.axaml`) y el cajón (el `Border` de 320 px de `MainWindow.axaml`)— sobre el **MISMO `ControlBarViewModel` portable** que el contenedor del núcleo ya resolvía (el del botón Ejecutar del hito 243), con paridad de **entradas, órdenes, estado habilitado/deshabilitado por contexto y atajos**, y **sin tocar ninguna otra superficie**.

### 🧱 Lo construido (dos controles del host, cero líneas en `FileFlow.App`)
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/ControlBar.xaml(.cs)` | La barra: marca, botón «Menú» y **tres islas** como el escritorio (modos · ciclo · herramientas), con **14 entradas** ancladas por `AutomationId`. Cada botón despacha el **comando canónico** del view model con su `CanExecute` respetado; el estado —visibilidad y habilitación— sale de **enlaces con el view model**, no de una copia local. |
| `FileFlow.App.Uno/Controls/MainMenuDrawer.xaml(.cs)` | El cajón: velo + panel de 320 px a la izquierda, sobre el **mismo estado** (`IsMenuOpen`, el que conmuta el botón «Menú»). Sus dos desplegables (tema e idioma) son los del núcleo y **aplican y guardan al elegir**, como el cajón del escritorio; su entrada «Ajustes» abre la **misma** superficie del hito 255, y su pie enseña la versión del producto. |
| `MainWindow.xaml(.cs)` | El montaje: `Bar.Vm = Drawer.Vm = mainVm.ControlBar` (una sola instancia), las **dos** entradas de ajustes al mismo `Settings.Open()`, y el Inspector conmutando la columna derecha del marco desde su `ToggleInspectorCommand`. |
| `RuntimeSelfCheck.RunControlBarProbe` | El sondeo del menú (**14 `[OK]`**), en **modo propio** (`--selfcheck-controlbar`) porque su ciclo de ejecución mueve el documento y las sondas del lienzo no toleran esa mudanza a mitad. |

⚠️ **Ninguna clave `Uno_*` nueva se inventó y ninguna traducción se reescribió**: los 30 textos de la barra y del cajón se **copian** del diccionario del escritorio, clave por clave, en los dos idiomas, y una guardia lo exige al carácter.

### 📐 La paridad, escrita (y lo que no llega, declarado)
El censo tiene **19 filas** con el AutomationId de cada entrada, la vista que la dibuja, **dónde vive su orden** —el code-behind si es un comando, el XAML si es un enlace bidireccional— y su **estado por contexto**. Frente a él, el escritorio se lee en sus **dos modos de enlace** (`{Binding XCommand}` en su barra y `{Binding ControlBar.XCommand}` en su ventana —mirar sólo el primero dejaba fuera la mitad del menú, el cajón: lo cazó la propia guardia al escribirse) y cada orden suya tiene destino en tres tablas del control:

- **Dibujadas aquí (13)**: menú, Vigilante, Ejecutar, Depurar, Siguiente Paso, Continuar, Pausar, Detener, Deshacer, Rehacer, Revertir, Inspector y el Modo Prueba (casilla).
- **Cumplida por el host (1)**: `OpenWorkflowSettingsCommand` —el ítem «Ajustes» del cajón del escritorio— se cumple por el **evento del host**, porque el comando del núcleo abre una *ventana* por `IWindowService`, que aquí es el Nulo declarado. `HostOwnedOrders`.
- **Pendientes (11)**: Nuevo / Cargar / Guardar Flujo (piden diálogo **síncrono**, frontera de la fase 5.3), Estudio de temas, Métricas, VFS, Diseñador de dataset, Manual, Ejemplos, Acerca de y el aviso de actualización. `DeclaredPendingEntries`. **Un botón cuyo destino no existe no se dibuja: se declara.**
- **Atajos (6)**: el host enruta **sólo** los del lienzo (`EditorKeyboardShortcuts`), así que F5 / F10 / Shift+F5 / Ctrl+N / Ctrl+O / Ctrl+S **no hacen nada aquí** y se declaran con su tecla y su razón (`DeclaredUnroutedShortcuts`). Enrutar una de ellas obliga a quitar su fila: la guardia exige que **ninguna tecla declarada como no enrutada esté en la tabla canónica del lienzo**.

### 🛡️ Guardia y mutaciones
`UnoControlBarParityGuardTests` (**8 casos**): el censo con su ancla y su estado; cada entrada con su orden en el artefacto que la posee y **sin reimplementar** el ciclo (`new ControlBarViewModel(` / `WorkflowExecutionCoordinator` prohibidos en la vista); el montaje compartido con el VM portable; la paridad de órdenes contra el escritorio con las tres tablas disjuntas; los **30 textos idénticos** al escritorio en los dos idiomas; el diccionario del host sin claves huérfanas; la sonda en modo propio; y los atajos declarados contra la tabla del lienzo. **🧬 Cuatro mutaciones (64.ª-67.ª): `menu-que-ejecuta-la-orden-de-otro`, `menu-que-no-declara-lo-que-falta`, `menu-sin-el-estado-de-su-contexto` y `menu-que-no-declara-un-atajo` → las cuatro MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes). COVERAGE → **66 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda **14 de 43** (`UnoControlBarParityGuardTests` deja de estar en la lista de guardias sin mutación).

### 🟢 Ejercido con la aplicación abierta (sesión 268)
El **driver externo por UIA** (`docs/qa/qa_menu_uia.py`, que reutiliza el fontanero de la sesión de ajustes y el instrumento de píxeles del 247) actuó sobre los controles reales mientras el **vigilante** medía. **16 de 16 pasos verificados**:

| Paso | Medición |
| :--- | :--- |
| Línea base | La barra expone **10 de sus 14** entradas y las 4 que faltan son **exactamente** las de contexto (Paso/Continuar son de la depuración; Pausar/Detener, del ciclo en marcha). El cajón: **0 de 5**. Deshacer y Rehacer llegan **deshabilitados** al canal externo (CanUndo/CanRedo del editor) y el control **rechaza** la orden. |
| Pulsar «Menú» | El cajón aparece (**5 de 5** anclas) y **el velo se ve en el pixel**: la banda central del lienzo pasa de `#FCF8F8` (77,8 %) a **`#585454` (99,9 %)**. |
| Pulsar «Ajustes» del cajón | La superficie de ajustes del host se abre (**6 de 6** anclas): la entrada del cajón y la de la barra abren la misma. |
| Cerrar el cajón | Sus entradas **salen del árbol** y el pixel central **vuelve al de la línea base** (`#585454` → `#FCF8F8`). |
| Pulsar el Inspector | La columna derecha **pasa a ser lienzo**: 0,0 % → **93,1 %** de la banda con el color del fondo. Insistiendo: **0,0 %**. |
| Modo Prueba | La casilla conmuta por `TogglePattern` (**1 → 0**) y se devuelve a su estado original. |

La línea de tiempo del vigilante lo corrobora (las 3 tarjetas pasan a 0 mientras el velo cubre la escena y vuelven a 3 al recogerse; **20 cambios materiales**). Al terminar, el fichero de preferencias del usuario queda **byte-idéntico** (md5 igual): el menú no escribe nada.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** (sólo avisos de nulabilidad preexistentes) |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** — el menú no rompió ninguna sonda anterior |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 14 `[OK]` · 0 `[FALLO]`** · «RESULTADO: VERIFICADO» |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 9 `[OK]` · 0 `[FALLO]`** |
| Guardia del menú | **8 de 8** superados |
| Mutaciones 64.ª-67.ª | **4 de 4 MUERDEN** (testigo rojo, control verde, árbol restaurado) |
| Suite completa | **1902 superadas + 1 omitida de 1903, 0 errores** (RC 0, 2 m 36 s) |

### 📌 Fronteras declaradas
1. **Las once entradas pendientes y los seis atajos** del menú del escritorio: declarados en el control, con su razón, y atados por la guardia. No se dibuja un botón que no puede hacer nada.
2. **El Inspector**: el host arranca con el panel abierto (es una columna del marco, como hasta ahora) y su entrada lo conmuta. El estado inicial es decisión del marco; **la conmutación sí es la del núcleo**.
3. **Los desplegables de tema e idioma del cajón** se ejercen en esta sesión por su presencia, su enlace bidireccional y su catálogo (guardia + sonda); su **selección en vivo** es la del `ControlBarViewModel` portable —write-through, ya medida con la app abierta en la sesión de ajustes para el mismo par de preferencias— y no se volvió a tocar aquí para no escribir en el fichero del usuario.
4. **`MainMenuDrawer` y `DrawerScrim` llevan su `AutomationId` en un `Border`**, que no tiene peer de automatización (la lección de la sesión 267): la presencia del cajón se prueba por sus **entradas**, que sí son controles.
5. **Sigue pendiente** de lo que nombró el usuario: los **paneles que algunos nodos tienen** (los diálogos de nodo y los pickers de variables de la fase 5.3), y del menú del escritorio, las once entradas y los seis atajos de arriba.

## [2026-09-28] - Hito 256: Los Ajustes del Host Uno Ejercidos con la Aplicación Abierta (Verificación a Fondo)

### 🎯 Objetivos y Alcance
El tramo anterior dejó la superficie de **ajustes / apariencia e idioma** del host Uno en el árbol, pero **pidió permiso sin cerrar la verificación**: desde las últimas ediciones (el arranque que aplica el tema y el idioma guardados, la guardia nueva) no había compilación, ni sondas, ni suite demostradas, y la superficie **no se había ejercido nunca en la aplicación real**. Este tramo cierra eso y **no toca el producto**: reconstruye el host, corre sus dos sondas, deja la suite verde, **repite en aislamiento los dos fallos que se habían atribuido a la carga** y ejerce la superficie de verdad con la aplicación abierta, el vigilante de píxeles y un driver externo por UI Automation (el reparto de las sesiones 252-260: el driver **actúa** sobre los controles reales, el vigilante **mide**).

### 📊 Ejercida de verdad, medida en píxeles (sesión 267)
El tema se lee como **la tonalidad que más superficie ocupa de la ventana** (el fondo del lienzo es el área mayor), que es una huella directa del tema vigente y, al reabrir, de cuál se aplicó:

| Paso (app abierta, driver externo + vigilante) | Preferencia tras guardar | Píxel dominante |
| :--- | :--- | :--- |
| Arranque con lo guardado del usuario | `pastel_spring` · `es-ES` | `#FFF8FA` **73,1 %** (claro) |
| Tema por el desplegable real (3 flechas) + Guardar | `dark_fluent` | `#10131B` **72,9 %** (oscuro) |
| Idioma por el desplegable real + Guardar | `en-US` | marco: «Guardar ajustes» → **«Save settings»**/**«Settings»** |
| **Cerrar y reabrir** la aplicación | `dark_fluent` · `en-US` | `#10131B` **73,1 %** + el botón lee **«Settings»** |
| Preferencias del usuario restauradas y reabierto | `pastel_spring` · `es-ES` | `#FFF8FA` **73,1 %** + «Ajustes» |

Las dos preferencias **sobreviven al cierre**, medido en píxeles y en el texto que UIA lee del marco. Además, con la app abierta: **las cuatro secciones** son alcanzables por su conmutador segmentado y cada una expone exactamente sus controles (Almacenamiento 16 anclas, Apariencia 13, Rendimiento 14, Herramientas 13); una **casilla** (`SettingsAutoSaveCheck`) se conmuta por `TogglePattern` y escribe la preferencia (`EnableAutoSave: True → False`); y **tres guardados seguidos** dejan el primero cerrando la superficie y los siguientes sin botón que pulsar (sin caída).

### 🔍 El comportamiento del tema y el idioma, precisado
Medido con las tres pulsaciones: el desplegable **sí registra** cada flecha (el guardado escribió `midnight_oled` = índice 1+3) pero **no aplica nada en vivo**: el lienzo no se repinta hasta que se pulsa Guardar, y por eso mismo **Cancelar deja la aplicación como estaba**. Es la misma semántica que la **ventana de ajustes del escritorio** (el `SelectedThemeId` del VM portable no aplica; aplican `SaveSettingsCommand` → `SetThemeById`/`SetCulture`), y distinta del **cajón de control**, que sí aplica en vivo. Queda escrito para que nadie lo lea como defecto.

### 🛠️ Seis defectos del instrumento (ninguno del producto), encontrados usándolo
El playtest no cambió el producto: cambió las herramientas que lo miden, porque se rompían delante del usuario.
1. **El instrumento no medía el tema**: `--shot` no tenía renglón con el color dominante → se añadió `METRIC top_colors` (la huella del tema, sin tocar la escena del vigilante).
2. **El driver moría con los emoji de los temas**: la consola cp1252 lanzaba `UnicodeEncodeError` al imprimir la lista de items **antes de elegir** → salida fijada a UTF-8 con reemplazo.
3. **Una ancla que nunca podía aparecer**: `SettingsPanel` está puesto en un `Border` y un `Border` **no tiene peer** de automatización; el driver lo tomaba por prueba de presencia y decía «panel ausente» con la superficie abierta → la presencia se prueba por las anclas propias de la superficie.
4. **Mensaje que culpaba a la búsqueda**: cuando la selección se enviaba pero el canal no la podía leer, el driver decía «no se encontró un tema cuyo nombre contenga …» → resultado propio (`SIN_LECTURA`) que se declara en vez de mentir.
5. **Índice del árbol cacheado tras actuar**: tras pulsar Guardar el panel ya estaba cerrado y el caché seguía dando sus anclas por presentes → lectura fresca en `read_state`.
6. **El respaldo de comtypes no existía**: `comtypes.client.GetPattern` no es una función, y un `except` ancho lo tragaba: los patrones **nunca** llegaban por ese camino y varias lecturas salían como «sin lectura» culpando a WinUI → patrones por `iface_*` de pywinauto. De paso, los items del desplegable venían **duplicados** (20 items para 10 temas) y elegir la copia equivocada era una de las razones de la intermitencia; el driver ahora deduplica y tiene `--open <sección>`, `--toggle` y `--value`.

### 📐 La frontera declarada del canal externo
`SettingsMaxCpuThreadsBox` (un `NumberBox` de WinUI) no expone `ValuePattern` al exterior: se ve como un `Spinner` sin hijos. Los tres campos numéricos ya están verificados **por dentro** (sonda de ajustes: `hilos=28->29` write-through), y los `ComboBox` no exponen su selección (`GetCurrentSelection` vacío, `SelectionItem` dice «no seleccionado» para todos): el driver lo declara y lo que zanja es la **preferencia guardada** y el **píxel**.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS) | **0 errores** (solo avisos de nulabilidad preexistentes) |
| Selfcheck del lienzo (`--selfcheck`) | **EXIT 0 · 83 OK · 0 FALLO** |
| Selfcheck de ajustes (`--selfcheck-settings`) | **EXIT 0 · 9 OK · 0 FALLO** (con `pastel_spring` guardado: arranque verde en los dos sentidos) |
| Suite completa | **1894 superadas + 1 omitida de 1895, 0 errores** (RC 0) |
| Los dos fallos «de carga» (`TheHeartbeat_ShouldPublishAPlausibleSample`, `FirstRun_ShouldUseEveryThreadItWasGiven`) | **pasan 3 rondas de 3 en aislamiento** → **ruido del entorno, no regresión** |
| Guardia de la superficie (`UnoSettingsSurfaceGuardTests`) | **12 superados de 12, 0 fallos** (53 ms) |
| Mutación del arranque (`arranque-que-no-aplica-el-tema-guardado`) | **MUERDE**: testigo rojo (1 de 1), control verde (1 de 1), árbol restaurado por bytes y recompilado (33,9 s) |
| Preferencias del usuario | **byte-idénticas** al terminar (la sonda no las toca; el playtest las restauró) |

El `RC=1` de una corrida intermedia **no era del producto**: dos `dotnet test` concurrentes en el mismo directorio de salida (`MSB3027/MSB3021` por `testhost` vivo bloqueando los `*.resources.dll`). Repetida en solitario, la suite cierra en **RC 0**.

### 📄 Evidencia
[`docs/qa/qa_ajustes_host_255.md`](file:///docs/qa/qa_ajustes_host_255.md) (§8, esta sesión) + `docs/qa/qa-manual-267/` (`timeline.jsonl`, `watch.log`, los catorce fotogramas rotulados) + el driver `docs/qa/qa_ajustes_uia.py` (con `--open <sección>`, `--toggle`, `--value`).

### 📌 Fronteras
No hay driver de puntero (el clic físico es humano). El **menú principal / barra de control completa** del escritorio sigue **pendiente**, igual que las pestañas **Actualizaciones** y **Modelos de IA** del propio ajustes, los **pickers de variables y los diálogos de nodo** (5.3) y el **empaquetado/CI/release** del host (5.5). Sin commit ni push.

---

## [2026-09-28] - Los Ajustes del Host Uno: La Superficie Que Faltaba, y el Tema Guardado Que No Llegaba al Lienzo (Hito 255)

### El encargo

«Termina de realizar la migración completa a Uno Platform… entre otras cosas el menú principal, **ajustes, temas, idioma**, los paneles que tienen algunos nodos». Este tramo cierra **la superficie de ajustes / apariencia e idioma** del host multiplataforma, y con ella los dos defectos que sólo se ven al **usarla con la aplicación abierta**.

### Lo construido (antes de esta sesión, en el árbol)

- **`FileFlow.App.Uno/Controls/SettingsPanel.xaml(.cs)`**: superficie de cuatro secciones (Almacenamiento, Apariencia, Rendimiento, Herramientas) montada en la ventana, con el `WorkflowSettingsViewModel` **portable** por DataContext (el mismo de la ventana del escritorio) y persistencia por sus comandos canónicos (`SaveSettingsCommand` → `UpdatePreferences` + `SetCulture` + `SetThemeById`). Los exploradores de rutas y la autodetección de herramientas van por los **pickers asíncronos** del host (el contrato síncrono del núcleo aborta en el hilo de UI, declarado en `UnoFileDialogService`).
- **Diccionario PROPIO del host** (`Resources/Strings.resx` y `Strings.es.resx`, ~60 claves `Uno_*`) registrado en `App.xaml.cs`: sin él, elegir English re-culturaba el proceso y los textos seguían saliendo del fallback incrustado — el defecto que la superficie mide.
- **Conmutación de secciones por VISIBILIDAD con los cuatro paneles siempre materializados**: el `Pivot` de WinUI materializa el cuerpo de la pestaña en el pase de layout SIGUIENTE y conmutarlo dentro de un callback de su propia reconstrucción muere con `COMException` (medido: `Failed to assign to property 'Content'`, proceso muerto con exit 127).
- **Sonda en modo propio** (`--selfcheck-settings`): su medición cambia tema e idioma (estado global) y conviviendo con las del lienzo hacía caer la sonda de selección, la de paneles y la de foco del lienzo. Dos tiempos (desplegar y dejar asentar el layout; medir) y **restauración de lo guardado**.

### Los dos defectos que sólo salieron al usarla

**1. El tema guardado se aplicaba al gestor de temas y no al lienzo.** El arranque aplicaba las preferencias guardadas **antes de crear la ventana**; el renglón de la sonda lo midió en rojo (`arranque: tema guardado='light_studio'->'light_studio' aplicado='light_studio'` **y** el token del lienzo en `#FF10131B`, el oscuro por defecto), y el playtest lo confirmó **en píxeles**: con `light_studio` guardado, el fotograma base era oscuro (medio RGB `(17,7 · 21,1 · 29,6)`, 0 % de píxeles claros). La publicación del tema pasa por `UnoThemeHost.PublishThemeVariant`, que muta pinceles y variante **a través de la ventana**: sin ventana la notificación se pierde **sin ruido** — ni excepción ni aviso. **Arreglo**: crear la ventana, aplicar lo guardado y **después** activarla (el usuario no ve el tema de por defecto ni un fotograma). Medido después: `(242,0 · 244,2 · 247,0)`, 97 % claro con el mismo valor guardado.

**2. La sonda del lienzo medía su propia suposición.** Con el arreglo puesto, el selfcheck del lienzo pasó a ROJO (2 fallos deterministas) y el renglón `[color]` crudo que se añadió a la sonda lo explicó en una línea: `fondo #FFFFF8FA->#FFF8FAFC tarjeta #FFFFFFFF->#FFFFFFFF restaurado #FF10131B contra #FFFFF8FA`. El fondo de entrada era **`#FFFFF8FA` = `pastel_spring`**, el tema guardado (y ahora sí aplicado); la sonda probaba con `light_studio` **fijo** —el mismo tema que ya estaba— y «restauraba» a un `dark_fluent` **fijo** que no era el de la entrada. **Era verde porque el producto ignoraba el tema guardado**: el defecto 1 era su condición de verde. **Arreglo**: elegir el tema **contrario al activo** y devolver **el de la entrada** (la regla que la sonda de ajustes ya usaba), con el `[color]` crudo en el informe. Verde en las dos direcciones (guardado oscuro y guardado claro, 83 OK las dos).

### La sesión con la aplicación abierta (261-266)

Sin puntero humano (el puntero inyectado sigue descartado por WinAppSDK, medido en 231/247), el reparto es: **actúa** un driver externo por UI Automation (`docs/qa/qa_ajustes_uia.py`: el botón del marco, las pestañas, los dos desplegables y el botón de guardar por sus `AutomationId`) y **mide** el vigilante del 247 (`qa_manual_session.py`: `--launch`, `--shot`, `--watch`, `--stop`) más el fotograma base de cada reapertura.

| sesión | qué se hizo | medición |
| :--- | :--- | :--- |
| 261 | abrir ajustes por el botón real, elegir tema e idioma en sus desplegables, **Guardar** | preferencia escrita (`dark_fluent`, `en-US`); el marco pasa a «Settings»/«Save settings» **en caliente**; `vigilante.log` + `timeline.jsonl` |
| 263 / 264 / 265 | lanzar con `light_studio` guardado, antes y después del arreglo del orden | **0 % claro → 97 % claro** (medio RGB `(17,7·21,1·29,6)` → `(242,0·244,2·247,0)`) |
| 264 / 265 / 266 | reabrir y leer el marco por UIA | «Settings» con `en-US` guardado; «Ajustes» con `es-ES` guardado |
| 266 | reabrir con `dark_fluent` **guardado por el driver** | **0 % claro**: la elección hecha en la app real sobrevive al cierre |

### Ruido del entorno, atribuido

El perfil de usuario está **compartido con otras sesiones de la máquina**: un vigilante de 1 s midió **~70 reescrituras seguidas del mismo valor** y el tema pasando a `pastel_spring` sin que nada de esta sesión corriera (las escrituras siguieron **después** de terminar el selfcheck, sin ningún proceso `FileFlow*` vivo), con campos que esta superficie no toca modificados (`NodeUsageCounts`, `LastUpdateCheckUtc`). La sonda de ajustes deja el fichero **byte-idéntico** en las comparaciones pareadas. Las **dos pruebas de medida real** que fallaron en corridas cargadas (`TheHeartbeat_ShouldPublishAPlausibleSample`, `FirstRun_ShouldUseEveryThreadItWasGiven`) **pasan en aislamiento** (dos rondas cada una) y no tocan esta superficie: es carga, no regresión.

### Sonda, guardia y mutaciones

- **Sondas**: `--selfcheck-settings` **EXIT 0 con 9 OK** (con el renglón `arranque:` comparando lo GUARDADO con lo APLICADO antes de tocar nada) y `--selfcheck` del lienzo **EXIT 0 con 83 OK** en las dos direcciones de tema guardado.
- **Guardia** `UnoSettingsSurfaceGuardTests` (12 casos): cableado al view model portable, **censo de los 21 controles** con su camino hasta la preferencia (enlace `TwoWay` o **write-back declarado** — los tres campos numéricos van por `NumberBox`, cuyo `Value` es `double` y el VM guarda `int`), el diccionario del host en los dos idiomas sin claves huérfanas, el arranque que aplica lo guardado con su orden, la sonda en modo propio y la restauración de lo del usuario.
- **Mutaciones (61.ª, 62.ª y 63.ª)**: `ajuste-que-no-devuelve-el-idioma`, `ajuste-sin-su-texto` y `arranque-que-no-aplica-el-tema-guardado` → las tres **MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes). COVERAGE: **62 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda **13 de 42**.

### Verificación

Host Uno 0 errores (MSBuild de VS); suite **1894 superadas + 1 omitida de 1895, 0 errores**; las dos sondas en verde; las tres mutaciones mordiendo. Evidencia en [`docs/qa/qa_ajustes_host_255.md`](file:///docs/qa/qa_ajustes_host_255.md) y `docs/qa/qa-manual-261..266/`. **Sin commitear**.

### Frontera declarada

Faltan las pestañas **Actualizaciones** y **Modelos de IA** de la ventana de ajustes (el VM portable las trae, la vista del host no), los **pickers de variables y los diálogos de nodo** (5.3), y el **menú principal / barra de control** completa del escritorio (el host tiene el botón de ajustes y la barra de zoom). Los `ComboBox` de esta pantalla no exponen su selección al canal externo (medido), y la elección de tema por UIA es intermitente: es del driver, no del producto.

---

## [2026-09-29] - Hito 285: Purga Total de Avalonia y Consolidación Canónica de Uno Platform

### 🎯 El encargo
Eliminar completamente toda dependencia, paquete y código de Avalonia en FileFlow Studio, unificando la interfaz de usuario bajo **Uno Platform** como host multiplataforma canónico único (Windows, Linux, macOS y Web thin client) y adaptando la suite de pruebas para alcanzar el 100% de éxito en `.NET 10` puro sin referencias a Avalonia.

### 🧹 Desmontaje y Limpieza Radical
1. **Purga en Plugins (`FileFlow.Plugin.*`):**
   - Eliminados todos los paquetes y referencias a `Avalonia` y `Material.Icons.Avalonia` en los 5 plugins afectados (`AI`, `Data`, `Documents`, `FileSystem`, `Integrations`).
   - Mantenimiento estricto de los contratos del SDK y `FileFlow.App.Core` (ViewModels puros sin dependencias de UI).
2. **Eliminación Física de `FileFlow.App`:**
   - El directorio físico `FileFlow.App/` y su proyecto `FileFlow.App.csproj` han sido completamente eliminados del repositorio.
   - Actualizada la solución `FileFlow.slnx`: se retiró `FileFlow.App` y se consolidó `FileFlow.App.Uno`.
3. **Consolidación en `Directory.Build.props`:**
   - Establecido `FileFlowUnoHost = true` y `FileFlowDesktopToolkit = false` con `FILEFLOW_NO_DESKTOP_TOOLKIT` permanente.
4. **Unificación de Lanzadores:**
   - `run.ps1` y `run-fast.ps1` redirigidos de forma transparente al host canónico `FileFlow.App.Uno` (`run-uno.ps1` y `run-uno-fast.ps1`), heredando todas las sondas de runtime (`-SelfCheck`, `-SelfCheckControlBar`, `-SelfCheckSettings`, `-SelfCheckDialogs`, `-SelfCheckUia`).

### 🧪 Adaptación de la Suite de Pruebas (`FileFlow.Tests`)
- Retiradas las referencias de proyecto y paquetes de Avalonia en `FileFlow.Tests.csproj`.
- Eliminadas guardias y pruebas obsoletas acopladas al host antiguo (`DesktopSelfCheckGuardTests`, `UiStyleLintTests`, `UiStyleContractTests`, `DisabledStateLintTests`).
- Adaptadas guardias canónicas al host Uno:
  - `DeferredWorkInventoryGuardTests`: 9/9 superadas (0 errores) verificando contra `FileFlow.App.Uno`.
  - `UiIconographyTests`: ampliado `AllowedGlyphs` con los glifos tipográficos nativos y ligeros de Uno Platform en XAML (⚡, ☰, 👁, 📁, 🚀, 🗑, ⚠, ✕, ↑, ↓, 📊, ⬇, 🔗, 🎨).
  - `UnoControlBarEntryGuardTests`: sincronizadas las listas de comandos canónicos (`DesktopCommands`) y atajos del menú principal (`DesktopShortcuts`) con las declaraciones reales de `ControlBar.xaml.cs`.
  - `ThemeStudioCatalogTests`, `UnoHermeticBuildGuardTests`, `UnoInteractionParityGuardTests`: 100% superadas.
- **Resultado de la Suite:** **1.740 superadas**, 1 omitida (modelo CLIP local ausente), **0 errores** (100% de éxito).
- **Sondeo en Runtime Uno (`.\run.ps1 -SelfCheck`):** 83/83 verificaciones correctas [OK], exit code 0.


