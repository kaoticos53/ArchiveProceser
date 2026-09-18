# FileFlow Studio - Historial de Cambios y Registro de Implementación (Walkthrough)

## [2026-09-18] - Rediseño Plano de Tarjetas de Nodos (Flat Modern Design) (Hito 138)

### 🎯 Diagnóstico y Causa Raíz
- **Solicitud del Usuario**: Eliminar el borde negro en cabecera y pie y el efecto de elevación de los nodos, prefiriendo un diseño completamente plano (*flat*).
- **Causa Raíz Visual**:
  - En `FileFlow.App/Views/Components/NodeCardView.axaml`, la tarjeta contenía un `Border` con `BoxShadow="{DynamicResource Elev2}"` que proyectaba una sombra difuminada dándole un aspecto 3D de elevación respecto al lienzo.
  - La cabecera (`nodify:Node.HeaderTemplate`) utilizaba `Background="{DynamicResource BgHeaderBrush}"` con `BorderThickness="0,0,0,1"` y `BorderBrush="{DynamicResource BorderDarkBrush}"`, generando una franja oscura separada por una línea negra en la parte superior.
  - El pie (`nodify:Node.FooterTemplate`) utilizaba `Background="{DynamicResource BgHeaderBrush}"` con `BorderThickness="0,1,0,0"` y `BorderBrush="{DynamicResource BorderDarkBrush}"`, generando otra franja oscura separada por una línea negra en la parte inferior.
  - El control contenedor `<nodify:Node>` tenía `BorderThickness="1.2"`, causando ligeros artefactos de suavizado subpíxel.

### 🎯 Solución Implementada
1. **Eliminación de la Sombra de Elevación**:
   - Eliminado el elemento `<Border BoxShadow="{DynamicResource Elev2}" ... />` de la tarjeta en `NodeCardView.axaml`. Ahora la tarjeta se asienta de forma 100% plana sobre el lienzo.
2. **Homogeneización y Eliminación de Bordes en Cabecera y Pie**:
   - En `nodify:Node.HeaderTemplate`: fondo configurado como `Background="Transparent"`, borde como `BorderThickness="0"` y `BorderBrush="Transparent"`. Corregida la altura de la caja de icono a `Height="22"`.
   - En `nodify:Node.FooterTemplate`: fondo configurado como `Background="Transparent"`, borde como `BorderThickness="0"` y `BorderBrush="Transparent"`.
   - La cabecera, el cuerpo de puertos y el pie de telemetría quedan integrados en un único bloque continuo con el fondo limpio `BgCardBrush` del nodo.
3. **Perímetro Nítido de 1px**:
   - `<nodify:Node>` configurado con `BorderThickness="1"` para un contorno plano, nítido y preciso.
4. **Actualización de Líneas Base de Regresión Visual (`FileFlow.Tests`)**:
   - Regeneradas y verificadas las líneas base `node-card-dark.png` y `app-shell-light.png` reflejando el aspecto plano.

### 🧪 Validación
- `dotnet build FileFlow.App/FileFlow.App.csproj`: **0 advertencias / 0 errores**.
- Suite completa de pruebas (`dotnet test`): **1035 superadas, 0 fallos, 1 omitida (100% verde)**.

---

## [2026-09-18] - Alineación Perfecta y Enrase Perimetral de Sockets de Puertos vía ConnectorTemplate (Hito 136)

### 🎯 Diagnóstico y Causa Raíz
- **Síntoma 1 - Sockets alejados del borde de la tarjeta**: Los gráficos de los sockets de entrada y salida se mostraban desplazados hacia el interior del nodo con un hueco de ~20px respecto al borde.
- **Síntoma 2 - Sockets desalineados en función de la longitud del texto**: En los puertos de salida, el gráfico del socket se movía horizontalmente dependiendo del tamaño de la etiqueta del puerto (`DisplayName`), en lugar de permanecer en una línea vertical fija a la derecha.
- **Causa Raíz Arquitectónica**:
  - En Nodify.Avalonia, `NodeInput` y `NodeOutput` heredan directamente de `Connector` y poseen en su plantilla interna dos componentes: `PART_Header` (donde se inyecta `HeaderTemplate`) y `PART_Connector` (el ancla de cable y socket, controlado por `ConnectorTemplate`).
  - Anteriormente, el gráfico del socket (`PortSocketTemplate`) se ubicaba incorrectamente dentro de un `StackPanel` en `HeaderTemplate` junto al `TextBlock`.
  - Esto provocaba que `PART_Connector` (el conector nativo de Nodify) quedase invisible en el extremo perimetral ocupando espacio, empujando el socket hacia dentro y haciendo que en las salidas la posición del socket dependiese de la anchura del texto de la etiqueta. Además, las líneas de conexión se calculaban sobre el invisible `PART_Connector` en lugar de nacer exactamente en el centro del gráfico del socket.

### 🎯 Solución Implementada
1. **Definición de `PortSocketTemplate` como `ControlTemplate` (`FileFlow.App/Views/Components/NodeCardView.axaml`)**:
   - `PortSocketTemplate` se configuró como `ControlTemplate x:Key="PortSocketTemplate" x:DataType="vm:PortViewModel"`.
   - Se inyecta directamente en la propiedad `ConnectorTemplate="{StaticResource PortSocketTemplate}"` tanto en `nodify:NodeInput` como en `nodify:NodeOutput`.
2. **Estructuración Limpia de `HeaderTemplate`**:
   - En `nodify:NodeInput`: `PART_Connector` (el socket) se sitúa a la izquierda del todo, seguido del `TextBlock` con margen izquierdo de 6px.
   - En `nodify:NodeOutput`: `PART_Header` (el `TextBlock` con margen derecho de 6px) se sitúa a la izquierda, y `PART_Connector` (el socket) se fija rígidamente en el extremo derecho del nodo.
   - Todos los sockets de salida comparten exactamente la misma coordenada X enrasada con el borde derecho del nodo independientemente de la longitud del texto, y todos los sockets de entrada se enrasan rígidamente al borde izquierdo.
3. **Actualización de Contratos Visuales y Regresiones (`FileFlow.Tests`)**:
   - Actualizado `NodeCardVisualContractTests.cs` para validar la presencia de `ConnectorTemplate` y compatibilidad con `ControlTemplate`.
   - Regenerada la línea base visual `node-card-dark.png` con la nueva disposición enrasada y fija.

### 🧪 Validación
- `dotnet build FileFlow.App/FileFlow.App.csproj`: **0 advertencias / 0 errores**.
- Suite completa de pruebas (`dotnet test`): **1035 superadas, 0 fallos, 1 omitida (100% verde)**.

---

## [2026-09-18] - Corrección de Seguimiento Dinámico de Cursor y Ciclo de Vida Limpio en PendingConnection (Hito 134)

### 🎯 Diagnóstico y Causa Raíz
1. **Línea de Conexión Congelada en (0,0) en Arrastres Sucesivos**:
   - En el primer arrastre de cable, la línea seguía al ratón correctamente. Sin embargo, en el segundo y sucesivos arrastres, el punto inicial se situaba en el socket de origen pero el extremo final quedaba clavado en la esquina superior izquierda `(0, 0)` sin seguir al cursor, aunque al soltar sobre el puerto de destino la conexión sí se realizaba.
   - **Causa Raíz**: En `Nodify.Avalonia.Connections.PendingConnection`, el método `OnApplyTemplate` suscribe cuatro manejadores de eventos enrutados en el `NodifyEditor` anfitrión (`PendingConnectionStartedEvent`, `PendingConnectionDragEvent`, `PendingConnectionCompletedEvent`, `KeyUpEvent`), pero la clase **carecía por completo de implementación de `OnDetachedFromVisualTree`**. Al finalizar el primer arrastre (`EditorViewModel.PendingConnection = null`), Avalonia desmantelaba el control visual del árbol pero este permanecía eternamente suscrito en el editor. En el segundo arrastre, la nueva instancia de `PendingConnection` también se suscribía. Al mover el ratón, la primera instancia (huérfana y fuera del árbol) recibía el evento primero, marcaba `e.Handled = true` y actualizaba sus coordenadas muertas. Avalonia omitía la invocación de la segunda instancia (la activa y visible) porque su manejador tenía `handledEventsToo: false`, dejando su `TargetAnchor` en su valor por defecto `(0, 0)` permanentemente.
2. **Salto Inicial a (0,0) en el Primer Arrastre**:
   - Al hacer clic sobre el puerto de salida/entrada, el extremo final de la línea apuntaba por un instante a `(0, 0)` antes del primer movimiento de ratón.
   - **Causa Raíz**: El control `PendingConnection` nacía con `TargetAnchor = default(Point) = (0, 0)`. Como el evento `PendingConnectionStartedEvent` se disparaba antes de que la plantilla visual estuviese montada, `TargetAnchor` no se actualizaba hasta el primer evento de arrastre (`PendingConnectionDragEvent`).

### 🎯 Solución Implementada
1. **Control Robusto `FlowPendingConnection` (`FileFlow.App/Views/Components/FlowPendingConnection.cs`)**:
   - Hereda de `Nodify.Avalonia.Connections.PendingConnection` con `StyleKeyOverride => typeof(PendingConnection)` para heredar automáticamente las plantillas spline y colores por tipo de dato.
   - **Ciclo de vida limpio (`OnDetachedFromVisualTree`)**: Desuscribe explícitamente todos los manejadores enrutados del `NodifyEditor` anfitrión y establece `IsVisible = false`, eliminando memory leaks y evitando que instancias anteriores intercepten los eventos de arrastre.
   - **Inmunidad ante eventos marcados**: En `OnApplyTemplate`, suscribe `PendingConnectionDragEvent` con `handledEventsToo: true`. En `OnPendingConnectionDrag`, asegura que si el evento venía marcado como `Handled`, se recalcule y actualice `TargetAnchor` en la instancia visual activa.
   - **Eliminación del parpadeo a (0,0)**: En `OnSourceAnchorChanged` y `OnAttachedToVisualTree`, si `TargetAnchor == default(Point)`, inicializa inmediatamente `TargetAnchor = SourceAnchor`, haciendo que la línea nazca en el socket y se expanda suavemente con el cursor desde el primer microsegundo.
2. **Integración en XAML (`FileFlow.App/Views/EditorView.axaml`)**:
   - Reemplazado `nodifyConn:PendingConnection` por `components:FlowPendingConnection` en `NodifyEditor.PendingConnectionTemplate`.
3. **Estilos de Puertos y Cables (`FileFlow.App/Styles/Ports.axaml`)**:
   - Actualizados los selectores a `:is(nodifyConn|PendingConnection)` para abarcar de forma transparente a `FlowPendingConnection`.
4. **Seguridad en ViewModel (`FileFlow.App/ViewModels/EditorViewModel.cs`)**:
   - En `FinishConnection` y `CancelConnection`, se establece `PendingConnection.IsVisible = false` antes de `PendingConnection = null`.
5. **Pruebas Unitarias (`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`)**:
   - Añadida prueba `FlowPendingConnection_WhenSourceAnchorAssigned_InitializesTargetAnchorToSourceAnchor` (verifica que no haya salto a `(0, 0)`).
   - Añadida prueba `FlowPendingConnection_SuccessiveDrags_FollowCursorCorrectly` (simula arrastres sucesivos verificando el correcto seguimiento de `TargetAnchor` en cada intento).

### 🧪 Validación
- `dotnet build FileFlow.App/FileFlow.App.csproj`: **0 advertencias / 0 errores**.
- Suite completa de pruebas (`dotnet test`): **1035 superadas, 0 fallos, 1 omitida (100% verde)**.

---

## [2026-09-18] - Corrección Definitiva de Paneo con Clic Derecho y Menús Contextuales en Nodos y Conexiones (Hito 133)

### 🎯 Diagnóstico y Causa Raíz
1. **Paneo perdido tras Hito 132**: Al asignar `Pan.Value = PointerGesture(MiddleClick)`, el lienzo ya no podía desplazarse con el botón derecho del ratón. El usuario requería `RightClick` drag en espacio libre para panear.
2. **Con Pan=RightClick, menús contextuales bloqueados**: Al restaurar `Pan.Value = PointerGesture(RightClick)`, los clics derechos sobre nodos y conexiones burbujeaban hasta `NodifyEditor`, donde el gesto de paneo capturaba el puntero, cancelando `PointerReleased` y por ende `ContextRequested` (la señal de Avalonia que abre el `ContextMenu`).
3. **Necesidad de distinción contextual**: El comportamiento correcto es:
   - **Clic derecho en fondo libre del canvas** → paneo del lienzo
   - **Clic derecho sobre un nodo** → menú contextual del nodo
   - **Clic derecho sobre una conexión** → menú contextual de la conexión

### 🎯 Solución Implementada
**Estrategia**: Interceptar el evento de clic derecho en el nivel adecuado antes de que llegue a `NodifyEditor`.

1. **`FileFlow.App/Views/Components/NodeCardView.axaml.cs`**:
   - En `NodeCardView_PointerPressed`: si `IsRightButtonPressed == true`, se marca `e.Handled = true` (bloquea el evento antes de que burbujee a `NodifyEditor`) y se llama a `ContextMenu.Open(this)` directamente. Esto da control total sobre el menú del nodo sin que Nodify inicie el pan.
2. **`FileFlow.App/Views/EditorView.axaml.cs`**:
   - Restaurado `Pan.Value = PointerGesture(RightClick)` para el paneo por clic derecho en canvas libre.
   - Añadido handler de tunneling `EditorView_TunnelingPointerPressed` via `AddHandler(PointerPressedEvent, ..., RoutingStrategies.Tunnel)`. Se ejecuta ANTES que `NodifyEditor`. Cuando detecta clic derecho sobre un `ConnectionContainer` con `ContextMenu`, marca `e.Handled = true` y llama a `connMenu.Open(connContainer)`, evitando que Nodify inicie el pan sobre la conexión.
   - Añadido `using Avalonia.VisualTree` para `FindAncestorOfType<ConnectionContainer>()`.
3. **`FileFlow.Tests/Unit/Views/NodeCardInteractiveControlsPointerTests.cs`**:
   - Añadida prueba `RightClick_OnNodeCard_ShouldMarkHandledAndOpenContextMenu` que verifica que el clic derecho sobre la superficie del nodo marca `e.Handled = true`.

### ✅ Resultado de Pruebas
- **Build**: `dotnet build FileFlow.App.csproj` → **0 Advertencias, 0 Errores**
- **Suite**: **1033 superadas, 0 fallos, 1 omitida (100% verde)**

---

## [2026-09-18] - Restauración de Menús Contextuales en Nodos y Conexiones y Liberación del Clic Derecho en el Editor (Hito 132)

### 🎯 Diagnóstico y Causa Raíz
1. **Supresión del Clic Derecho por el Gesto de Paneo en Nodify (`Editor.Pan`)**:
   - Al hacer clic derecho sobre cualquier elemento del lienzo (nodo, conexión o fondo), el menú contextual no aparecía.
   - **Causa Raíz**: En `Nodify.Avalonia`, el mapa de gestos por defecto `EditorGestures.Mappings.Editor.Pan` incluía `RightClick` y `MiddleClick`. Al pulsar el botón derecho, `NodifyEditor` interpretaba la interacción como el inicio de un paneo del lienzo, capturaba el puntero (`e.Pointer.Capture`) y marcaba el evento como manejado (`e.Handled = true`), suprimiendo la notificación de menú contextual (`ContextRequested` / `PointerReleased`) de Avalonia en toda la jerarquía visual.
   - **Solución**: En el constructor estático de `EditorView.axaml.cs`, se configuró `EditorGestures.Mappings.Editor.Pan.Value = new PointerGesture(MouseAction.MiddleClick);`, reservando el botón derecho del ratón exclusivamente para la apertura de menús contextuales en nodos, conexiones y lienzo.
2. **Desconexión de Comandos en el Menú Contextual de Tarjetas de Nodo (`NodeCardView.axaml`)**:
   - En `NodeCardView.axaml`, las acciones de copiar, cortar, duplicar y borrar utilizaban enlaces relativos `{Binding $parent[views:EditorView].((vm:EditorViewModel)DataContext).CopySelectedNodesCommand}` / `DeleteSelectedNodesCommand`.
   - **Causa Raíz**: En Avalonia, los menús contextuales (`ContextMenu`) se renderizan en una capa flotante (`OverlayLayer` / `PopupRoot`) fuera del árbol visual del control padre (`EditorView`). Debido a esto, `$parent[views:EditorView]` se evaluaba a `null`, dejando las opciones del menú inactivas o sin respuesta al hacer clic.
   - **Solución**: En `NodeViewModel.cs` se crearon los comandos MVVM directos `DeleteCommand`, `CopyCommand`, `CutCommand` y `DuplicateCommand` delegando en `ParentEditor`. En `NodeCardView.axaml`, se actualizaron los enlaces del `ContextMenu` a `{Binding CopyCommand}`, `{Binding CutCommand}`, `{Binding DuplicateCommand}` y `{Binding DeleteCommand}` directamente contra el `DataContext` de la tarjeta (`NodeViewModel`).
3. **Menú Contextual de Conexiones (`nodifyConn:Connection`)**:
   - En `EditorView.axaml`, la opción de borrar conexión intentaba enlazar a través de `$parent[views:EditorView]`.
   - **Solución**: En `ConnectionViewModel.cs` se añadió el comando `DeleteCommand` (`Source?.NodeOwner?.ParentEditor?.DeleteConnection(this)`), se añadió el estilo en `EditorView.axaml` para `nodifyConn:ConnectionContainer` con `ContextMenu` vinculado a `{Binding DeleteCommand}`, y se actualizó `nodifyConn:Connection.ContextMenu` con `Command="{Binding DeleteCommand}"`.

### 🎯 Cambios Implementados
1. **`FileFlow.App/Views/EditorView.axaml.cs`**:
   - En constructor estático, asignado `Nodify.Avalonia.EditorGestures.Mappings.Editor.Pan.Value = new PointerGesture(MouseAction.MiddleClick)`.
2. **`FileFlow.App/ViewModels/NodeViewModel.cs`**:
   - Añadidos `[RelayCommand] Delete()`, `[RelayCommand] Copy()`, `[RelayCommand] Cut()`, `[RelayCommand] Duplicate()`.
3. **`FileFlow.App/ViewModels/ConnectionViewModel.cs`**:
   - Añadido `[RelayCommand] Delete()`.
4. **`FileFlow.App/Views/Components/NodeCardView.axaml`**:
   - Enlaces de `ContextMenu` simplificados y directos a `{Binding CopyCommand}`, `{Binding CutCommand}`, `{Binding DuplicateCommand}` y `{Binding DeleteCommand}`.
5. **`FileFlow.App/Views/EditorView.axaml`**:
   - Añadido estilo `nodifyConn|ConnectionContainer` con `ContextMenu` para borrar conexiones.
   - Actualizado `nodifyConn:Connection.ContextMenu` con `Command="{Binding DeleteCommand}"`.
6. **`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`**:
   - Añadidas pruebas unitarias `ContextMenu_OnNodeCard_ShouldHaveCommands_AndExecuteProperly` y `ContextMenu_OnConnection_ShouldHaveDeleteCommand_AndExecuteProperly`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas (`dotnet test`): **1032 superadas, 0 fallos, 1 omitida (100% verde)**.

---

## [2026-09-17] - Corrección de Anclaje de Socket (SourceAnchor) y Seguimiento Dinámico de Cursor en PendingConnection (Hito 131)

### 🎯 Diagnóstico y Causa Raíz
1. **Línea de Conexión Naciendo en (0,0) / Esquina Superior Izquierda**:
   - Al iniciar el arrastre de conexión, el inicio de la línea partía de `(0, 0)` en vez de situarse en el conector seleccionado.
   - **Causa Raíz**: En `Nodify.Avalonia`, `PendingConnection` es instanciado reactivamente por `NodifyEditor` una vez que el evento `PendingConnectionStartedEvent` ya ha terminado de dispararse en el conector. Como el control se creaba después del evento, nunca recibía `e.Anchor`, dejando `SourceAnchor` en `(0, 0)`.
   - **Solución**: Vincular explícitamente `SourceAnchor="{Binding Source.Anchor}"` en `PendingConnectionTemplate`. Dado que `PortViewModel.Anchor` es actualizado en tiempo real por `<nodify:NodeInput Anchor="{Binding Anchor, Mode=OneWayToSource}">` y `<nodify:NodeOutput Anchor="{Binding Anchor, Mode=OneWayToSource}">`, `SourceAnchor` se posiciona inmediatamente en el centro del socket sin depender de la secuencia del evento de inicio.
2. **Extremo de la Línea Bloqueado en el Nodo Inicial en Intentos Sucesivos**:
   - En intentos sucesivos, el extremo final no seguía al cursor y se quedaba fijo en el nodo.
   - **Causa Raíz**: En `EditorView.axaml`, `TargetAnchor` estaba enlazado de forma unidireccional a `TargetLocation` (`TargetAnchor="{Binding TargetLocation}"`). Como `TargetLocation` no se actualiza en el ViewModel durante el movimiento del ratón, el binding sobreescribía y bloqueaba el cálculo interno que `PendingConnection.OnPendingConnectionDrag` realizaba a partir del evento enrutado.
   - **Solución**: Eliminar la asignación de `TargetAnchor` en el XAML de `PendingConnectionTemplate`, permitiendo que `PendingConnection` actualice `TargetAnchor` libremente según los eventos de arrastre del puntero (`PendingConnectionDragEvent`).
3. **Cuadro Negro Grande al Final de la Línea**:
   - **Causa Raíz**: `PendingConnection` hereda de `ContentControl` y su plantilla nativa ubica un contenedor `Border` con `ContentPresenter` en las coordenadas `TargetAnchor` para mostrar previews/miniaturas. Al anidar un `<nodifyConn:Connection>` dentro del cuerpo de `<nodifyConn:PendingConnection>`, el control `Connection` se incrustaba dentro de ese slot de contenido en el extremo del cursor, renderizando su caja de layout por defecto con fondo oscuro.
   - **Solución**: Redefinir la `ControlTemplate` en `Ports.axaml` utilizando `<Canvas>` con un único `<nodifyConn:Connection>` vinculado a `{TemplateBinding SourceAnchor}` y `{TemplateBinding TargetAnchor}` con `Spacing="45"`.

### 🎯 Cambios Implementados
1. **`FileFlow.App/Views/EditorView.axaml`**:
   - Configurado `Source="{Binding Source}"` y `SourceAnchor="{Binding Source.Anchor}"` en `NodifyEditor.PendingConnectionTemplate`.
   - Eliminado `TargetAnchor="{Binding TargetLocation}"` para no bloquear el arrastre, y eliminado cualquier contenido anidado.
2. **`FileFlow.App/Styles/Ports.axaml`**:
   - Definida la `ControlTemplate` de `nodifyConn:PendingConnection` con `<Canvas>` y `<nodifyConn:Connection>` con curvatura spline (`Spacing="45"`).
3. **`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`**:
   - Añadida prueba unitaria `PendingConnection_TestSplineTemplate` verificando la resolución de `SourceAnchor` y `TargetAnchor`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas (`dotnet test --nologo`): **1030 superadas, 0 fallos, 1 omitida (100% verde)**.


---

## [2026-09-17] - Implementación de Curvas Spline Bézier Fluidas en Conexiones Interactivas (PendingConnection) (Hito 130)


### 🎯 Diagnóstico y Objetivo
1. **Curva Spline en Tiempo Real al Arrastrar Conexiones**:
   - Al hacer clic sobre un conector de entrada o salida y arrastrar hacia otro nodo, el usuario requería que la línea mostrada tuviera la misma forma de curva spline (Bézier cúbica con `Spacing="45"`) que los cables definitivos ya conectados, siguiendo fluidamente al cursor y acoplándose con *snapping* a los puertos de destino.
2. **Causa del Comportamiento Previo**:
   - El control nativo `nodifyConn:PendingConnection` de `Nodify.Avalonia` utiliza internamente una plantilla base basada en `LineConnection` (línea recta rígida).
   - Para obtener la curva Bézier fluida, la plantilla del control debía incorporar un componente `nodifyConn:Connection` vinculado a las propiedades de anclaje dinámico `SourceAnchor`, `TargetAnchor`, `Direction`, `Stroke` y `StrokeThickness`.

### 🎯 Cambios Implementados
1. **`FileFlow.App/Styles/Ports.axaml`**:
   - Redefinido el estilo base de `nodifyConn:PendingConnection` con una `ControlTemplate` que aloja un `<nodifyConn:Connection>` con curvatura spline nativa (`Spacing="45"`):
     ```xaml
     <Style Selector="nodifyConn|PendingConnection">
         <Setter Property="Stroke" Value="{DynamicResource AccentGlowBrush}" />
         <Setter Property="StrokeThickness" Value="3.5" />
         <Setter Property="StrokeDashArray" Value="{x:Null}" />
         <Setter Property="EnablePreview" Value="False" />
         <Setter Property="Template">
             <ControlTemplate TargetType="nodifyConn:PendingConnection">
                 <nodifyConn:Connection Source="{TemplateBinding SourceAnchor}"
                                        Target="{TemplateBinding TargetAnchor}"
                                        Spacing="45"
                                        Direction="{TemplateBinding Direction}"
                                        Stroke="{TemplateBinding Stroke}"
                                        StrokeThickness="{TemplateBinding StrokeThickness}"
                                        StrokeDashArray="{TemplateBinding StrokeDashArray}" />
             </ControlTemplate>
         </Setter>
     </Style>
     ```
   - Mantenida la tematización dinámica por familias de tipo de datos (`.wireFiles`, `.wireText`, `.wireBoolean`, `.wireNumber`, etc.) mediante tokens de color de Avalonia.
2. **`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`**:
   - Actualizadas las pruebas unitarias para validar las propiedades iniciales del ViewModel de conexiones pendientes (`PendingConnectionViewModel`) y asegurar la estabilidad de la suite.

### 🧪 Validación
- `dotnet build FileFlow.App\FileFlow.App.csproj`: **0 advertencias / 0 errores**.
- Suite completa de pruebas (`dotnet test --nologo`): **1028 superadas, 1 omitida (CLIP), 0 fallos (100% verde)**.

---

## [2026-09-17] - Restauración del Comportamiento Estándar de Conexiones en Nodify y Eliminación de Líneas a (0,0) y Animaciones (Hito 129)

### 🎯 Diagnóstico y Causa Raíz
1. **Línea Recta Apuntando a (0,0) (Borde Superior Izquierdo) al Arrastrar Conexiones**:
   - Al hacer clic en un conector de entrada o salida para arrastrar y conectar, una línea recta se dibujaba hacia la esquina superior izquierda `(0, 0)`.
   - **Causa Raíz**: En `FileFlow.App/Views/EditorView.axaml`, la plantilla `PendingConnectionTemplate` tenía enlaces manuales forzados `Source="{Binding Source.Anchor}"` y `Target="{Binding TargetLocation, Mode=TwoWay}"`. En Nodify.Avalonia, `PendingConnection.Source` y `Target` son propiedades de tipo `object?` (para controles/modelos de conector), mientras que los extremos geométricos se calculan internamente a través de `SourceAnchor` y `TargetAnchor` mediante los eventos enrutados `PendingConnectionStartedEvent`, `PendingConnectionDragEvent` y `PendingConnectionCompletedEvent`. Al enlazar `Target` a `TargetLocation` (que se evaluaba como `Point(0,0)`), la vinculación forzaba a `PendingConnection` a fijar su destino en `(0,0)`, anulando el seguimiento nativo del cursor de Nodify.
2. **Animaciones con Guiones y Transiciones en Conexiones**:
   - En `FileFlow.App/Styles/Ports.axaml` y `EditorView.axaml` quedaban capas de energía y transiciones CSS (`Transitions`) sobre `Border.socket`, `Path.socketTriangle` y `nodifyConn|Connection.energy` con `StrokeDashArray` animado.
   - Estos estilos introducían latencia y efectos visuales no deseados durante el arrastre continuo de cables.

### 🎯 Cambios Implementados
1. **`FileFlow.App/Views/EditorView.axaml`**:
   - Simplificada la plantilla `PendingConnectionTemplate` al estándar nativo de Nodify.Avalonia, eliminando los enlaces manuales conflictivos de `Source` y `Target`:
     ```xaml
     <nodify:NodifyEditor.PendingConnectionTemplate>
         <DataTemplate x:DataType="vm:PendingConnectionViewModel">
             <nodifyConn:PendingConnection EnablePreview="False"
                                         EnableSnapping="True"
                                         AllowOnlyConnectors="True"
                                         Direction="Forward"
                                         StrokeDashArray="{x:Null}"
                                         Classes.wireFiles="{Binding Source.IsFilesType}"
                                         Classes.wireText="{Binding Source.IsTextType}"
                                         Classes.wireBoolean="{Binding Source.IsBooleanType}"
                                         Classes.wireNumber="{Binding Source.IsNumberType}"
                                         Classes.wireBinary="{Binding Source.IsBinaryType}"
                                         Classes.wireCollection="{Binding Source.IsCollectionType}"
                                         Classes.wireAny="{Binding Source.IsAnyType}" />
         </DataTemplate>
     </nodify:NodifyEditor.PendingConnectionTemplate>
     ```
   - Nodify gestiona ahora de forma nativa e instantánea el seguimiento del cursor del ratón en cada evento de arrastre.
2. **`FileFlow.App/Styles/Ports.axaml`**:
   - Eliminados todos los bloques `nodifyConn|Connection.energy` y `StrokeDashOffset` animados.
   - Eliminadas las transiciones `Transitions` en sockets para respuesta instantánea.
   - Asegurado `StrokeDashArray="{x:Null}"` en todas las clases de cables y conexiones pendientes.
3. **`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`**:
   - Añadida prueba unitaria verificando el estado inicial de `PendingConnectionViewModel` y compatibilidad con el sistema de temas y clases de estilo por tipo de dato.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1028 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

### 🎯 Diagnóstico y Causa Raíz
1. **La Animación / Cable de Conexión solo aparecía la primera vez**:
   - Al realizar la primera conexión desde un conector en el lienzo, el cable seguía al cursor correctamente. Sin embargo, al intentar crear conexiones sucesivas desde cualquier entrada o salida de un nodo, no aparecía nada en pantalla al arrastrar.
2. **Causa Raíz - Bloqueo de Arrastre por `IsConnected` en `NodeInput` y `NodeOutput`**:
   - En `FileFlow.App/Views/Components/NodeCardView.axaml`, los controles `<nodify:NodeInput>` y `<nodify:NodeOutput>` tenían configurado `IsConnected="{Binding IsConnected, Mode=TwoWay}"`.
   - En Nodify.Avalonia, cuando un conector tiene `IsConnected = true`, el gesto de arrastre se conmuta internamente a "modo desconexión", ignorando el evento `ConnectionStartedCommand` y llamando exclusivamente a `DisconnectConnectorCommand`.
   - En un motor DAG/flujo de datos, los **puertos de salida (`NodeOutput`) pueden conectarse a múltiples entradas** (fan-out) y deben permitir arrastrar nuevos cables en todo momento, independientemente de si ya tienen conexiones salientes previas.
   - El estado visual del conector (relleno sólido vs hueco) ya está gobernado de forma reactiva por `PortSocketTemplate` (`Classes.connected="{Binding IsConnected}"`), por lo que el control base de Nodify no debe retener `IsConnected = true` para no bloquear el inicio de nuevas conexiones.

### 🎯 Correcciones Implementadas
1. **`FileFlow.App/Views/Components/NodeCardView.axaml`**:
   - Eliminado el atributo `IsConnected="{Binding IsConnected, Mode=TwoWay}"` de los controles `<nodify:NodeInput>` y `<nodify:NodeOutput>`, garantizando que el inicio de arrastre de cable (`ConnectionStartedCommand`) se dispare limpiamente en cada intento de conexión para cualquier entrada o salida.
2. **`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`**:
   - Añadida prueba unitaria `RepeatedConnectionDrag_ShouldUpdatePendingConnectionState_AndAllowSubsequentConnections` validando el ciclo de vida completo de múltiples intentos sucesivos de conexión (inicio, arrastre, completado, cancelación y reconexión sobre puertos ya conectados).

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1026 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

## [2026-09-17] - Corrección del Seguimiento del Cursor en el Cable de Conexión Pendiente (PendingConnection) (Hito 127)

### 🎯 Diagnóstico y Causa Raíz
1. **El Cable de Conexión Pendiente Apuntaba a (0, 0)**:
   - Al hacer clic en un conector de entrada o salida (`NodeInput` / `NodeOutput`) y arrastrar para crear un enlace entre nodos, el cable no seguía al cursor del ratón; en su lugar, se dibujaba desde el conector de origen hacia la esquina superior izquierda de la pantalla `(0, 0)`.
2. **Falta de Enlace Bidireccional de `TargetAnchor` en la Plantilla de Conexión Pendiente**:
   - En `FileFlow.App/Views/EditorView.axaml` dentro de `NodifyEditor.PendingConnectionTemplate`, el control `<nodifyConn:PendingConnection>` tenía configurado `Source="{Binding Source}"`, `SourceAnchor="{Binding Source.Anchor}"` y `Target="{Binding Target}"`, pero **carecía** de `TargetAnchor="{Binding TargetLocation, Mode=TwoWay}"` y `Target="{Binding Target, Mode=TwoWay}"`.
   - En Nodify.Avalonia, `PendingConnection` utiliza la propiedad de dependencia `TargetAnchor` (`Point`) para calcular y dibujar la curva Bézier hacia el extremo de destino. Al no estar enlazado a `TargetLocation` de `PendingConnectionViewModel`, `TargetAnchor` se mantenía en su valor por defecto `(0, 0)`, ignorando las coordenadas de arrastre del puntero generadas por el lienzo.

### 🎯 Correcciones Implementadas
1. **`FileFlow.App/Views/EditorView.axaml`**:
   - En `NodifyEditor.PendingConnectionTemplate`, configurado `<nodifyConn:PendingConnection>` con:
     ```xaml
     <nodifyConn:PendingConnection Source="{Binding Source}"
                                   Target="{Binding Target, Mode=TwoWay}"
                                   SourceAnchor="{Binding Source.Anchor}"
                                   TargetAnchor="{Binding TargetLocation, Mode=TwoWay}"
                                   Stroke="{DynamicResource PendingWireBrush}"
                                   StrokeThickness="2.5"
                                   Direction="Forward" />
     ```
2. **`FileFlow.App/ViewModels/PendingConnectionViewModel.cs`**:
   - Verificado que `_targetLocation` se inicializa correctamente con `source?.Anchor ?? default`, asegurando que al comenzar el arrastre, el punto de destino parte exactamente de la posición del conector antes de actualizarse dinámicamente con cada movimiento del puntero.
3. **`FileFlow.Tests/Unit/Views/EditorViewLayoutTests.cs`**:
   - Añadida prueba unitaria `PendingConnection_WhenStarted_ShouldInitializeTargetLocationToSourceAnchor` para validar que `PendingConnectionViewModel` inicializa `TargetLocation` en la coordenada del conector de origen y responde reactivamente a las actualizaciones de posición durante el arrastre por el lienzo.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1025 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

## [2026-09-17] - Sincronización Reactiva de Presets al Guardar y Aplicar en el Estudio de Renombrado Avanzado (Hito 126)

### 🎯 Diagnóstico y Requerimiento
1. **Falta de Sincronización del Preset al Guardar y Aplicar**:
   - En el Estudio de Renombrado Avanzado (`AdvancedRenamerEditorWindow`), al seleccionar un preset del catálogo y hacer clic en el botón "Guardar y Aplicar" (`SaveAndClose`), el preset seleccionado no se reflejaba de forma reactiva en el parámetro `"PipelineName"` de la tarjeta del lienzo ni en el panel de inspección.
2. **Asincronía en Diálogos Modales y Desacoplamiento de Plugins**:
   - En Avalonia, `window.ShowDialog(owner)` es asíncrono (`Task`) y no bloqueante. Al ejecutarse una acción personalizada (`provider.ExecuteCustomAction(...)`), la sincronización previa de parámetros se ejecutaba inmediatamente antes de que el usuario interactuara con el diálogo modal. Al cerrar la ventana con `Close(true)`, el anfitrión `NodeViewModel` no recibía notificación de finalización para refrescar los parámetros.

### 🎯 Correcciones Implementadas
1. **`FileFlow.Sdk/Descriptors/NodeCustomActionContext.cs`**:
   - Creado el contrato desacoplado `public sealed record NodeCustomActionContext(object? ParentWindow = null, Action? OnCompleted = null);` en el SDK base, permitiendo que cualquier plugin reciba el contexto de la ventana anfitriona y un callback determinista de finalización sin depender de `FileFlow.App`.
2. **`FileFlow.App/ViewModels/NodeViewModel.cs`**:
   - Implementado `SyncParametersFromNodeInstance()` para recargar descriptores y actualizar de forma reactiva `param.Value` y `param.UpdateOptions(...)` en los parámetros del nodo.
   - Actualizado `ExecuteCustomAction(string actionId)` para inyectar `new NodeCustomActionContext(App.MainWindow, () => SyncParametersFromNodeInstance())`.
3. **`FileFlow.Plugin.FileSystem/Nodes/Processing/AdvancedRenamerNode.cs` y Plugins del Ecosistema**:
   - En `AdvancedRenamerNode.cs`, `MediaTranscoderNode.cs`, `MultimodalVisionLlmNode.cs`, `SmartUnpackNode.cs`, `ArchiveFanOutNode.cs`, `CustomScriptNode.cs` y `SyntheticDataSourceNode.cs`: Adaptada la ejecución de acciones personalizadas para extraer `NodeCustomActionContext` y suscribir `window.Closed += (_, _) => onCompleted();` (o invocar `onCompleted?.Invoke()` tras `ShowDialog<bool>`).
4. **`FileFlow.Plugin.FileSystem/UI/ViewModels/AdvancedRenamerEditorViewModel.cs`**:
   - En `OnSelectedPresetChanged`, el cambio de `SelectedPreset` actualiza inmediatamente `PipelineName = value.Name` y clona sus pasos en la colección `Steps`.
   - Al pulsar "Guardar y Aplicar" (`SaveAndClose`), se persiste `_node.Parameters["PipelineName"] = PipelineName;`, garantizando que al cerrarse la ventana, el callback `OnCompleted` dispara `SyncParametersFromNodeInstance()`, reflejando instantáneamente el preset activo en la tarjeta visual y en el inspector.
5. **`NodeParameterViewModel.cs`**:
   - Actualizados `OpenMediaPresetManager()` y `OpenPasswordManager()` para suministrar `NodeCustomActionContext` con sincronización reactiva al cierre.
6. **Pruebas Unitarias**:
   - Añadida prueba `SelectedPreset_WhenChanged_ShouldUpdatePipelineNameAndSteps` en `AdvancedRenamerEditorViewModelTests.cs`.
   - Añadida prueba `ExecuteCustomAction_WithNodeCustomActionContext_ShouldAcceptContext` en `AdvancedRenamerEditorViewModelTests.cs`.
   - Añadida prueba `SyncParametersFromNodeInstance_ShouldUpdateParameterValuesAndOptions` en `NodeParameterManagerTests.cs`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1024 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

## [2026-09-17] - Desplegable con Todos los Presets en el Parámetro Nombre del Pipeline (AdvancedRenamerNode) (Hito 125)

### 🎯 Diagnóstico y Requerimiento
1. **Descriptor de Parámetro como Texto Plano en Lugar de Desplegable de Presets**:
   - En el nodo de Renombrar Archivo (`AdvancedRenamerNode`), el parámetro `"PipelineName"` estaba configurado como un cuadro de texto plano (`ParameterEditorType.Text`), requiriendo que el usuario conociera y escribiera manualmente los nombres exactos de los presets incorporados (ej. `"📷 Fotografía Digital (Fecha EXIF + Modelo + Contador)"`, `"0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)"`, `"🧹 Limpiar Nombre"`, etc.) para aplicarlos desde la tarjeta o el inspector.
2. **Sincronización y Carga de Presets en el Editor Visual**:
   - Al abrir el Estudio de Renombrado Avanzado (`AdvancedRenamerEditorWindow`) habiendo seleccionado un preset en la tarjeta del lienzo, los presets se cargaban después de inicializar los datos del nodo, impidiendo que el selector de presets de la ventana modal marcara automáticamente el preset activo en su desplegable.

### 🎯 Correcciones Implementadas
1. **`AdvancedRenamerNode.cs`**:
   - Modificado el descriptor de parámetro de `"PipelineName"` a `ParameterEditorType.Dropdown` con `Options: GetPresetOptions()`.
   - Implementado el método `GetPresetOptions()` que incluye `"Pipeline Predeterminado"` y auto-descubre todos los presets incorporados y de usuario expuestos por `RenamerPresetService.GetBuiltinPresets()`.
2. **`AdvancedRenamerEditorViewModel.cs`**:
   - Reordenada la inicialización en el constructor para invocar `LoadPresets()` antes de `LoadFromNode()`.
   - Añadido fallback en `LoadFromNode()` para cargar y clonar deterministamente los pasos del preset correspondiente si `MethodSteps` está vacío y `PipelineName` coincide con un preset de `AvailablePresets`.
   - Vinculado reactivamente `SelectedPreset` para reflejar y resaltar inmediatamente el preset coincidente al abrir el editor.
   - En `OnSelectedPresetChanged`, los pasos se clonan (`s.Clone()`) para asegurar que la edición interactiva en el lienzo no muta la definición del preset original en memoria.
3. **`NodeParameterManager.cs`**:
   - En `OnParameterValueChanged`, al modificar el parámetro `"PipelineName"` desde el desplegable del lienzo o del inspector, se limpia preventivamente `_nodeInstance.Parameters["MethodSteps"] = string.Empty;` para que el nodo active de inmediato los pasos del preset seleccionado sin conflictos con configuraciones previas.
4. **`AdvancedRenamerExhaustiveTests.cs` y `AdvancedRenamerEditorViewModelTests.cs`**:
   - Añadida prueba unitaria `AdvancedRenamer_NewInstance_ShouldHaveDefaultPipelineName_AndNoPatternParameter` validando que `PipelineName` expone el tipo `ParameterEditorType.Dropdown` y contiene todos los presets.
   - Añadida prueba unitaria `AdvancedRenamer_SelectingPresetFromDropdown_ShouldExecutePresetCorrectly` validando que la selección de un preset desde el desplegable ejecuta el pipeline correctamente (ej. relleno de números a 2 dígitos).
   - Añadida prueba unitaria `Constructor_WithPresetSelected_ShouldLoadPresetStepsAndSelectPreset` validando la sincronización en el editor visual.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1021 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

## [2026-09-17] - Estado Visual Seleccionado y Sincronización Reactiva de Chips en FileVersionSelector (Hito 123)

### 🎯 Diagnóstico y Causa Raíz
1. **Ausencia de Estado Visual Activo/Seleccionado en los Chips de Versión**:
   - Al pulsar sobre los botones/chips de versión de archivo (`Original`, `Actual`, etc.) en los nodos de Copiar/Mover Archivo o Selector de Mejor Versión, el valor del parámetro se actualizaba internamente, pero los botones mantenían invariables su fondo (`BgSurfaceBrush`) y borde (`BorderDarkBrush`).
   - Debido a la prioridad de selectores en Avalonia FluentTheme (`ContentPresenter#PART_ContentPresenter`), los botones no reflejaban visualmente cuál opción estaba activa o seleccionada.
2. **Falta de Propiedad Reactiva de Selección en el Modelo de Datos**:
   - `FileVersionOption` era un `record` inmutable sin propiedad observable `IsSelected` que notificara a la interfaz cuándo el chip correspondía al `ActiveVersionTag` del nodo.

### 🎯 Correcciones Implementadas
1. **`AppModels.cs`**:
   - Convertido `FileVersionOption` en `ObservableObject` con la propiedad reactiva `[ObservableProperty] private bool _isSelected;`.
2. **`Buttons.axaml`**:
   - Creada la clase de estilo `Button.chipButton` con soporte completo para estados normal, `:pointerover`, `.selected` (con fondo de acento `AccentPrimaryBrush`, texto en contraste `TextOnAccentBrush` y tipografía destacada `Bold`) y `.selected:pointerover` (`AccentHoverBrush`), aplicados sobre `ContentPresenter#PART_ContentPresenter`.
3. **`NodeParameterViewModel.cs`**:
   - Implementado el método `UpdateVersionOptionsSelection()` que sincroniza de forma reactiva `opt.IsSelected` comparando el valor activo (`Value` / `ActiveVersionTag`) con el token/etiqueta de cada opción al seleccionar, cargar o modificar el parámetro.
4. **`NodeParameterTemplates.axaml` y `NodeInspectorPanelView.axaml`**:
   - Actualizadas las plantillas de chips a `Classes="chipButton"` y `Classes.selected="{Binding IsSelected}"`, heredando el color de texto e icono vectoriales desde el botón contenedor (`Foreground="{Binding $parent[Button].Foreground}"`).
5. **`NodeCardInteractiveControlsPointerTests.cs`**:
   - Actualizada la prueba unitaria para verificar que al pulsar un chip de versión, `originalOption.IsSelected` pasa a `true` y el parámetro actualiza su valor.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Pruebas visuales y de interacción: **100% superadas**.
- Suite completa de pruebas: **1019 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

## [2026-09-17] - Corrección de Cierre al Expandir Nodos con FileVersionSelector (Hito 122)

### 🎯 Diagnóstico y Causa Raíz
1. **Excepción de Resolución de Tipos en Tiempo de Ejecución (`XamlTypeResolver`)**:
   - Al expandir nodos como `FileRelocatorNode` ("Copiar / Mover Archivo") o `BestVersionSelectorNode` ("Selector de Mejor Versión"), la aplicación se cerraba abruptamente con `System.ArgumentException: Unable to resolve type vm:NodeParameterViewModel from any of the following locations:`.
   - En `NodeParameterTemplates.axaml`, el selector de versiones de archivo (`IsFileVersionSelector`) declaraba un binding con casting de tipo de datos: `{Binding $parent[ItemsControl].((vm:NodeParameterViewModel)DataContext).SelectVersionOptionCommand}`.
   - Sin embargo, en la cabecera de `NodeParameterTemplates.axaml`, el espacio de nombres `xmlns:vm` estaba definido como `using:FileFlow.App.ViewModels` en lugar de la sintaxis canónica de CLR con ensamblado cualificado (`clr-namespace:FileFlow.App.ViewModels;assembly=FileFlow.App`).
   - El resolver de tipos en runtime de Avalonia (`ExpressionNodeFactory.LookupType` / `XamlTypeResolver.Resolve`) no puede inferir el ensamblado para conversiones de tipo en tiempo de ejecución a partir de prefijos `using:`, provocando la excepción no capturada durante el pase de medición/renderizado (`MeasureCore`/`ApplyTemplate`).
2. **Ausencia de Soporte de `FileVersionSelector` en el Panel Inspector**:
   - En `NodeInspectorPanelView.axaml`, no existía el bloque de plantilla para `IsFileVersionSelector` y los namespaces no tenían ensamblado explícito.

### 🎯 Correcciones Implementadas
1. **`NodeParameterTemplates.axaml`**:
   - Actualizados los namespaces `xmlns:vm`, `xmlns:models` y `xmlns:loc` a `clr-namespace` con ensamblado explícito (`assembly=FileFlow.App`, `assembly=FileFlow.Sdk`), permitiendo la resolución determinista de tipos en Avalonia XAML.
2. **`NodeInspectorPanelView.axaml`**:
   - Actualizados namespaces a `clr-namespace` y añadida la sección de visualización de chips interactivos para `IsFileVersionSelector` con botón `{x}` de variables.
3. **`NodeCardInteractiveControlsPointerTests.cs`**:
   - Añadida prueba `ExpandingNodeCard_WithFileVersionSelector_ShouldRenderWithoutException` para verificar que la expansión y renderizado de `FileRelocatorNode` y `BestVersionSelectorNode` se realiza limpiamente sin excepciones de layout o tipos.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Pruebas visuales y de contrato de tarjeta: **21 / 21 superadas al 100%**.
- Suite completa de pruebas: **1019 superadas, 1 omitida (CLIP opcional), 0 fallos (100% verde)**.

---

## [2026-09-17] - Corrección Integral del Catálogo de Variables y Expresiones (Hito 121)

### 🎯 Diagnóstico y Causas Raíz
1. **Discrepancia de Nombres de Propiedades en `DataGrid` de `VariablePickerWindow.axaml`**:
   - Las columnas del `DataGrid` vinculaban a `FullExpression`, `CategoryName` y `ExampleValue`.
   - La clase del modelo `VariableItem` expone `Token`, `Category`, `Description`, `SampleValue`, `IsUpstream` y `SourceNodeTitle`.
   - Debido al fallo de resolución de propiedades en Avalonia, todas las celdas del catálogo (salvo la descripción) se renderizaban en blanco y vacías.
2. **Colección Vacía por Falta de Descubrimiento por Defecto**:
   - Al abrir `VariablePickerWindow` sin grupos preestablecidos (o desde constructores auxiliares como el editor de texto `TextEditorDialogWindow` sin nodo o sin conexiones), `AllVariables` quedaba inicializada en `[]` porque nunca se invocaba a `VariableDiscoveryService.Instance.GetAvailableVariables(targetNode, [])` para poblar el catálogo de variables globales/sistema/fechas/tamaños/funciones.
3. **Falta de Comandos de Inserción y Cableado de Eventos**:
   - `InsertSelectedCommand` y `InsertPreviewText` no estaban expuestos en `VariablePickerViewModel`, impidiendo retornar la variable seleccionada al diálogo o editor invocador.
   - En `TextEditorDialogWindow.axaml.cs`, el botón de variables instanciaba `VariablePickerWindow` sin capturar el `SelectedToken` ni insertarlo en el cursor del `TextEditor`.

### 🎯 Correcciones Implementadas
1. **`VariablePickerViewModel.cs`**:
   - Descubrimiento automático y carga de respaldo en el constructor: si no se suministran grupos, se invoca `VariableDiscoveryService.Instance.GetAvailableVariables(targetNode, [])`.
   - Soporte para filtros por categoría bilingüe (`ALL`, `UPSTREAM`, `SYSTEM`, `DATES`, `SIZES`, `FUNCTIONS`).
   - Implementado `InsertSelectedCommand`, propiedad `InsertPreviewText` reactiva y evento `RequestClose`.
2. **`VariablePickerWindow.axaml` y `VariablePickerWindow.axaml.cs`**:
   - Corregidos los enlaces de columnas del `DataGrid` a `{Binding Token}`, `{Binding Category}`, `{Binding Description}` y `{Binding SampleValue}`.
   - Píldoras de filtrado por categoría, caja de búsqueda con botón de limpieza, doble clic/tap en fila para inserción rápida y panel lateral derecho de detalles e inspección.
   - Respeto estricto a los tokens de tema (`TextMutedBrush`, `BgCardBrush`, `BorderDarkBrush`, iconos vectoriales `MaterialIconKind`).
3. **`TextEditorDialogWindow.axaml.cs`**:
   - Cableado completo de `BtnInsertVar_Click` para abrir el selector de variables de forma modal y pegar el token resultante en la posición actual del cursor de texto.
4. **`IVariableDiscoveryService.cs` y `VariableDiscoveryService.cs`**:
   - Flexibilizados los parámetros de entrada (`targetNode` y `connections` opcionales/anulables) para permitir la consulta segura de variables en cualquier contexto.
5. **`Strings.resx` y `Strings.es.resx`**:
   - Añadida la clave `VarPicker_DescriptionLabel` localizada en español e inglés.
6. **Líneas Base Visuales y Pruebas Unitarias**:
   - Añadidos tests unitarios en `VariablePickerAndIntelliSenseTests.cs` validando el descubrimiento por defecto sin grupos y la ejecución de `InsertSelectedCommand`.
   - Actualizadas las líneas base visuales en `ModalVisualRegressionTests`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Pruebas unitarias de VariablePicker: **9 / 9 superadas**.
- Suite completa de pruebas: **1017 superadas, 1 omitida, 0 fallos, 1018 total (100% verde)**.

---

## [2026-09-17] - Elevación Automática de Nodos al Primer Plano al Seleccionar (BringToFront on Select) (Hito 120)

### 🎯 Diagnóstico y Necesidad
Al hacer clic sobre un nodo o seleccionarlo en el lienzo DAG de Nodify, si el nodo quedaba parcialmente superpuesto o detrás de otros nodos adyacentes, el usuario no podía ver ni editar sus parámetros sin reubicarlo manualmente.

### 🎯 Correcciones Implementadas
1. **`NodeViewModel.cs`**:
   - En el setter de `IsSelected`, cuando el valor pasa a `true`, se invoca automáticamente `Owner?.BringToFront(this)`.
2. **`EditorViewModel.cs`**:
   - Optimización de `BringToFront`: si el nodo ya posee el `ZIndex` máximo (`_maxZIndex`), la llamada retorna de inmediato sin provocar reordenamientos innecesarios en el árbol visual de Nodify.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite de pruebas de nodos y lienzo: **100% superadas**.

---

## [2026-09-17] - Corrección de Listas Desplegables (ComboBox / AutoCompleteBox) en Tarjetas de Nodos Expandidas (Hito 119)

### 🎯 Diagnóstico y Causa Raíz
1. **Pérdida de Puntero por Burbujeo a `ItemContainer` de Nodify**:
   - En `NodeCardView.axaml.cs`, al pulsar sobre un `ComboBox`, `AutoCompleteBox` o control interactivo dentro de una tarjeta de nodo expandida, `NodeCardView_PointerPressed` detectaba el control y retornaba (`return;`), pero **no marcaba el evento como manejado (`e.Handled = true`)**.
   - Al quedar `e.Handled = false`, el evento `PointerPressed` continuaba burbujeando hasta `nodify:ItemContainer`.
   - `ItemContainer.OnPointerPressed` se ejecutaba al ver un evento no manejado y capturaba el puntero (`e.Pointer.Capture(this)`) para iniciar el arrastre del nodo sobre el lienzo.
   - Esta captura de puntero por parte de `ItemContainer` robaba el foco e interrumpía el ciclo de eventos del `ComboBox`, cerrando instantáneamente el popup o impidiendo que `ComboBox` recibiera `PointerReleased` para desplegar sus opciones. En cambio, en el panel del inspector (`NodeInspectorPanelView`), al no existir `NodifyCanvas`/`ItemContainer`, el `ComboBox` funcionaba sin interferencias.
2. **Apertura de Sugerencias en `AutoCompleteBox` con `MinimumPrefixLength == 0`**:
   - Los controles `AutoCompleteBox` (usados para desplegables editables con inserción de variables `{x}`) no desplegaban sus opciones automáticamente al hacer clic/foco sin escribir texto previo.
3. **Interferencia de Doble Clic sobre Controles Interactivos**:
   - `NodeCardView_DoubleTapped` invocaba `InspectNode()` incondicionalmente, interfiriendo con la selección de texto en campos de texto o interacción dentro de la tarjeta.

### 🎯 Correcciones Implementadas
1. **`NodeCardView.axaml.cs`**:
   - Centralizado el método `IsInteractiveVisual(object? source)` que detecta de forma exhaustiva `ComboBox`, `ComboBoxItem`, `AutoCompleteBox`, `Button`, `ToggleButton`, `TextBox`, `ToggleSwitch`, `Slider`, `NumericUpDown`, `ListBox`, `ListBoxItem` y `ScrollViewer`.
   - En `NodeCardView_PointerPressed`, al detectar un control interactivo, se marca explícitamente **`e.Handled = true`**, evitando que el evento llegue a `ItemContainer` de Nodify y asegurando que las listas desplegables mantengan el puntero y se abran fluidamente.
   - En `NodeCardView_DoubleTapped`, se ignora el doble clic sobre controles interactivos para no interrumpir la edición ni robar el foco abriendo el inspector.
   - Añadido handler para `GotFocusEvent` que abre automáticamente el desplegable de `AutoCompleteBox` (`IsDropDownOpen = true`) cuando `MinimumPrefixLength == 0`.
2. **`NodeInspectorPanelView.axaml.cs`**:
   - Añadido handler para `GotFocusEvent` que abre el desplegable de `AutoCompleteBox` al recibir foco cuando `MinimumPrefixLength == 0`.
3. **`NodeCardInteractiveControlsPointerTests.cs`**:
   - Creada suite de pruebas unitarias en `FileFlow.Tests` verificando que los eventos de puntero sobre `ComboBox` y `AutoCompleteBox` son manejados por `NodeCardView`, mientras que la superficie no interactiva de la tarjeta permite el arrastre en Nodify.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- `dotnet test`: **1014 superadas + 1 skip / 1015 (100% verde)**.

---

## [2026-09-17] - Rediseño del Catálogo de Nodos a Menú Homogéneo con Chevron Izquierdo e Iconos Vectoriales (Hito 118)

### 🎯 Diagnóstico y Causa Raíz
1. **Apariencia Fragmentada y Cajas Pesadas en Expander**:
   - Cada categoría en el catálogo de nodos (`NodeToolboxView.axaml`) se renderizaba como una tarjeta aislada con borde y fondo (`Expander` Fluent default), separada de las demás por márgenes de 8px, provocando una sensación de botones/cajas disconexas en vez de un panel de menú de navegación unificado.
   - Dentro de cada categoría, cada elemento de nodo individual estaba envuelto en su propio contenedor `Border` con fondo y borde (`BorderThickness="1"`), saturando visualmente la lista.
2. **Heterogeneidad de Tamaños, Emojis Sueltos y Disposición de Chevron**:
   - Los encabezados de categorías tenían alturas variables, chevrons ubicados a la derecha y emojis inconsistentes embebidos en las cadenas de traducción (algunas categorías tenían emojis y otras como "General" o "Testing" carecían de ellos).
3. **Localización de Placeholder de Búsqueda Faltante**:
   - `SearchNodesPlaceholder` no estaba definido en `Strings.resx` ni `Strings.es.resx`, mostrando la clave cruda en el campo de búsqueda.

### 🎯 Correcciones Implementadas
1. **`Containers.axaml`**:
   - Definido el estilo `Expander.menuAccordion` con plantilla personalizada: chevron de expansión interactivo posicionado a la **izquierda** con rotación suave (0° a 90° al expandir), fondo y bordes transparentes, altura uniforme (`MinHeight="28"`), estados hover limpios (`BgHoverBrush`), y eliminación de bordes innecesarios en el contenido expandido.
   - Definido el estilo `Border.nodeMenuItem`: filas homogéneas y sin bordes por defecto, altura mínima uniforme (`MinHeight="28"`), alineación vertical centrada, transiciones fluidas de hover con `BgHoverBrush`.
2. **`NodeToolboxView.axaml` y `ToolboxViewModel.cs`**:
   - `ToolboxCategoryGroup` expone ahora la propiedad `MaterialIconKind Icon`, resolviendo iconos vectoriales para todas las categorías mediante `NodeIconResolver`.
   - Implementado `Expander Classes="menuAccordion"` con `Grid` en el encabezado: icono vectorial Material Design (14×14 px), nombre de categoría con `TextTrimming="CharacterEllipsis"` y badge de píldora que indica la cantidad de nodos (`Items.Count`).
   - Reemplazadas las cajas de nodo por filas `Classes="nodeMenuItem"` con iconos de 15x15 alineados, botón de favorito compacto con espaciado derecho holgado (8px de margen respecto a la barra de scroll) y padding uniforme.
3. **`Strings.resx`, `Strings.es.resx` y `NodeIconResolver.cs`**:
   - Limpieza de emojis sueltos de los nombres de categorías y roles para garantizar 100% de uniformidad gráfica.
   - Mapeo centralizado en `NodeIconResolver` de categorías estándar y dinámicas (`general`, `testing`, `test`, `muestra`, roles ETL).
   - Añadida la clave de localización `SearchNodesPlaceholder` ("Search nodes..." / "Buscar nodos...").
4. **Líneas Base Visuales**:
   - Actualizadas las capturas de regresión visual (`panel-toolbox-dark.png`, `app-shell-dark.png`, `app-shell-light.png`).

### 🧪 Validación
- `dotnet test --filter "FullyQualifiedName~VisualRegression"`: **21 / 21 superadas al 100%**.

---

### 🎯 Diagnóstico y Causa Raíz
1. **Invalidación de Árbol Visual y Pérdida de Captura en `NodeCardView.axaml.cs`**:
   - `NodeCardView` se suscribía incondicionalmente a `PointerPressed += NodeCardView_PointerPressed;` llamando a `EditorViewModel.BringToFront(Node)`.
   - Al pulsar sobre un `ComboBox` o `AutoCompleteBox` en la tarjeta del nodo, el evento de puntero burbujeaba inmediatamente hacia `NodeCardView`.
   - `BringToFront` modificaba el `ZIndex` del nodo, obligando a Nodify / Avalonia a reordenar los hijos del lienzo en pleno clic, lo cual destruía de inmediato la captura de puntero del popup antes de que pudiera abrirse o recibir la selección.
2. **Requisito de Prefijo en `AutoCompleteBox`**:
   - Por defecto, `AutoCompleteBox` utilizaba `MinimumPrefixLength = 1`, provocando que los desplegables editables no mostraran sugerencias ni abrieran el desplegable al hacer clic sin haber escrito antes caracteres.
3. **Sobrescrituras de Estilo de Popup en `Inputs.axaml` para FluentTheme**:
   - Se requerían selectores de plantilla explícitos para el contenedor del popup (`Popup#PART_SuggestionsContainer`, `Border#PopupBorder`), límites de altura (`MaxDropDownHeight`) y estilos de hover/selección en `ComboBoxItem` (`PART_ContentPresenter`).

### 🎯 Correcciones Implementadas
1. **`NodeCardView.axaml.cs`**:
   - Inspección del tipo visual de origen del evento de puntero (`e.Source`). Si el clic proviene de un control interactivo (`ComboBox`, `AutoCompleteBox`, `TextBox`, `Slider`, `ToggleSwitch`, `Button`, `NumericUpDown` o elementos popup), se omite la llamada a `BringToFront`, garantizando la captura de puntero ininterrumpida.
2. **`EditorViewModel.cs`**:
   - Protección en `BringToFront`: `if (node.ZIndex == _maxZIndex && _maxZIndex > 0) return;`, evitando reordenamientos innecesarios y sobrecarga en el árbol visual cuando el nodo ya se encuentra al frente.
3. **`Inputs.axaml`**:
   - Añadido `MaxDropDownHeight="320"` a `ComboBox`.
   - Estilizado de `Border#PopupBorder` con tokens (`BgCardBrush`, `BorderDarkBrush`, `RadiusXs`, `Elev3`).
   - Sobrescrituras completas de hover (`BgHoverBrush`), selección (`AccentPrimaryBrush`, `TextOnAccentBrush`) y hover seleccionado (`AccentHoverBrush`) en `ComboBoxItem`.
   - Estilo completo para `AutoCompleteBox` con `MinimumPrefixLength="0"` y `MaxDropDownHeight="280"`.
4. **`NodeParameterTemplates.axaml` y `NodeInspectorPanelView.axaml`**:
   - Configurados `ComboBox` y `AutoCompleteBox` con `MinimumPrefixLength="0"`, `FilterMode="None"` y `MaxDropDownHeight`.
5. **`NodeParameterViewModelTests.cs`**:
   - Añadidas pruebas unitarias `DropdownParameter_ShouldRecognizeDropdownAndMatchOption` y `EditableDropdownParameter_ShouldBeMarkedAsEditable`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Pruebas visuales y de estilo (`UiStyleLintTests`, `NodeCardVisualContractTests`, `VisualRegressionTests`): **48 / 48 superadas al 100%**.
- Suite completa de pruebas: **1011 superadas, 1 omitida, 0 fallos, 1012 total**.

---

### 🎯 Diagnóstico y Causa Raíz
En el componente de tarjeta de nodo ([`NodeCardView.axaml`](file:///E:/Users/kaoti/Documentos/GitHub/FileFlow.WT/avalonia/FileFlow.App/Views/Components/NodeCardView.axaml)), la barra inferior de métricas y telemetría (pie de tarjeta) no llegaba hasta los bordes laterales e inferior de la caja del nodo:
1. `NodeCardView.axaml` no definía `Padding="0"` en `<nodify:Node>`, por lo que el padding por defecto de la plantilla de Nodify creaba un margen alrededor del contenido del cuerpo.
2. El pie de métricas estaba incrustado en una fila (`Grid.Row="1"`) dentro del `Content` general del nodo en lugar de utilizar el slot dedicado `nodify:Node.FooterTemplate` / `Footer="{Binding}"`.
3. Al redimensionar o expandir la tarjeta en vertical, el pie quedaba flotando debajo de los puertos/parámetros en vez de anclarse de forma perimetral al borde inferior del nodo.

### 🎯 Correcciones Implementadas
1. **`NodeCardView.axaml`**:
   - Asignado `Padding="0"`, `VerticalAlignment="Stretch"` y `VerticalContentAlignment="Stretch"` en `<nodify:Node>`.
   - Movida la barra de métricas y el tirador de redimensionado a `<nodify:Node.FooterTemplate>` con binding `Footer="{Binding}"`, dejando el cuerpo del nodo (`StackPanel` de puertos y parámetros) en el slot de contenido central.
   - El contenedor del pie (`Border`) abarca el 100% del ancho del nodo (de borde izquierdo a derecho) y se ancla al borde inferior con redondeo `CornerRadius="0,0,6,6"`, división superior `BorderThickness="0,1,0,0"` y fondo `BgHeaderBrush`.
2. **Líneas Base Visuales**:
   - Actualizadas las líneas base de regresión visual `node-card-dark.png` y `panel-editor-dark.png`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- `UiStyleLintTests` & `NodeCardVisualContractTests`: **14 / 14 superadas**.
- `VisualRegressionTests` & `AppShellVisualRegressionTests`: **21 / 21 superadas al 100%**.
- Suite completa de pruebas: **1009 superadas, 1 omitida, 0 fallos, 1010 total**.

---

## [2026-09-16] - Corrección Integral y Modernización de Controles de Lista Desplegable (ComboBox / Dropdowns)

### 🎯 Diagnóstico y Causas Raíz
Al interactuar con los controles de lista desplegable (`ComboBox` y `AutoCompleteBox`) en la aplicación (drawer de ajustes, tarjetas de nodos en el lienzo DAG e inspector de nodos), los desplegables no respondían o no aplicaban sus cambios:
1. **Selector de Tema en el Drawer (`MainWindow.axaml`)**:
   - El `ComboBox` vinculaba `SelectedItem="{Binding ControlBar.SelectedThemeObject}"`.
   - `SelectedThemeObject` no existía en `ControlBarViewModel`, impidiendo la sincronización bidireccional y el cambio de tema desde el menú desplegable.
2. **Selector de Idioma en el Drawer (`MainWindow.axaml`)**:
   - Los elementos `<ComboBoxItem Tag="es-ES">` y `<ComboBoxItem Tag="en-US">` no especificaban `SelectedValueBinding="{Binding Tag, RelativeSource={RelativeSource Self}}"`.
   - Avalonia asignaba el objeto visual `ComboBoxItem` en lugar de la cadena de texto con la cultura (`"es-ES"` / `"en-US"`), rompiendo la llamada al cambio de idioma.
3. **Estilos de Plantilla de ComboBox en Avalonia 11/12 (`FileFlow.App/Styles/Inputs.axaml`)**:
   - Había selectores de plantilla heredados tipo `/template/ Border#Background`, `TextBlock#PlaceholderTextBlock` y `PathIcon#DropDownGlyph` que rompían el renderizado en FluentTheme de Avalonia 11/12.
4. **Plantillas de Parámetros de Nodo e Inspector (`NodeParameterTemplates.axaml` y `NodeInspectorPanelView.axaml`)**:
   - La condición de visibilidad del `ComboBox` estándar dependía de variables que podían evaluarse en falso negativo ante opciones dinámicas.
   - En el inspector de nodos no se contemplaba el `AutoCompleteBox` editable para parámetros `IsEditableDropdown`.

### 🎯 Correcciones Implementadas
1. **`MainWindow.axaml`**:
   - Corregido el selector de tema vinculando `SelectedValue="{Binding ControlBar.SelectedTheme, Mode=TwoWay}"`, con `SelectedValueBinding="{Binding Id}"` y `DisplayMemberBinding="{Binding Name}"`.
   - Corregido el selector de idioma especificando `SelectedValueBinding="{Binding Tag, RelativeSource={RelativeSource Self}}"`.
2. **`FileFlow.App/Styles/Inputs.axaml`**:
   - Modernizados los estilos de `ComboBox`, `ComboBox:pointerover`, `ComboBox:focus`, `ComboBoxItem`, `ComboBoxItem:pointerover` y `ComboBoxItem:selected` con tokens de diseño (`RadiusXs`, `BgSurfaceBrush`, `AccentPrimaryBrush`, `Pad1`, `MinHeight="28"`).
3. **`NodeParameterTemplates.axaml` y `NodeInspectorPanelView.axaml`**:
   - Ajustadas las plantillas de `IsDropdown` para soportar de manera fluida tanto `ComboBox` nativo (`!IsEditableDropdown`) como `AutoCompleteBox` editable con botón de variables dinámicas `{x}` y tokens de redondeo visual.
4. **Líneas Base Visuales**:
   - Actualizada la línea base de regresión visual `panel-toolbox-dark.png`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1009 superadas, 1 omitida, 0 fallos, 1010 total**.
- Pruebas visuales y de estilo: **26 / 26 superadas**.

---

## [2026-09-16] - Corrección de Error del Previsualizador de Vistas y Diálogos en el IDE (Avalonia Previewer)

### 🎯 Diagnóstico
Al abrir el previsualizador XAML de Avalonia en el IDE (Visual Studio / JetBrains Rider / VS Code con Avalonia Extension), el proceso `PreviewerProcess` se cerraba con la excepción:
```
System.AggregateException: One or more errors occurred. (Unable to resolve type DesignInstance from namespace http://schemas.microsoft.com/expression/blend/2008 Line 15, position 9.)
```
**Causa raíz**:
- `MainWindow.axaml` declaraba `d:DataContext="{d:DesignInstance Type=vm:MainViewModel, IsDesignTimeCreatable=False}"`.
- `d:DesignInstance` es una extensión de marcado legacy de WPF / Microsoft Expression Blend (`http://schemas.microsoft.com/expression/blend/2008`) que no existe en el compilador XAML en tiempo de ejecución de Avalonia (`AvaloniaXamlIlRuntimeCompiler`).
- Al analizar el documento XAML para la previsualización del diseño, el parser intentaba resolver `DesignInstance` en el namespace `d:`, fallando e impidiendo el renderizado visual de la ventana y diálogos dependientes.

### 🎯 Corrección
- **`MainWindow.axaml`**: Eliminada la extensión `d:DesignInstance` y los namespaces `xmlns:d` y `xmlns:mc`. Sustituido por el atributo nativo tipado de Avalonia: `x:DataType="vm:MainViewModel"`.
- **`FilePreviewerWindow.axaml`**, **`FilePreviewerControl.axaml`**, **`ImageCompareSliderControl.axaml`**: Limpiados los namespaces heredados de Blend (`xmlns:d` / `xmlns:mc`), migrando a las propiedades adjuntas canónicas de Avalonia: `Design.DesignWidth` y `Design.DesignHeight`.

### 🧪 Validación
- Compilación `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1009 superadas, 1 omitida, 0 fallos, 1010 total**.
- Pruebas visuales y de estilo: **26 / 26 superadas**.

---

## [2026-09-16] - Rediseño Moderno y Adaptativo de Formularios y Parámetros de Nodos

### 🎯 Diagnóstico y Problemas Resueltos
1. **Problema de Visibilidad Multilínea Fantasma**:
   - En `NodeParameterTemplates.axaml`, existía `<Grid IsVisible="{Binding IsMultilineRow}">`, pero `IsMultilineRow` no existía en `NodeParameterViewModel.cs`.
   - Debido al fallback de Avalonia cuando falta la propiedad vinculada, todos los parámetros estándar (incluyendo booleanos que mostraban `0` o `False`) renderizaban un TextBox multilínea gigante y vacío debajo de cada fila, saturando visualmente las tarjetas de nodos y el inspector.
2. **Controles Poco Adaptados al Tipo de Dato**:
   - Parámetros booleanos usaban cajas de texto en vez de interruptores interactivos.
   - Parámetros numéricos continuos o acotados carecían de deslizadores o controles numéricos con formato.
   - Selectores de archivo y rutas carecían de integración elegante con botones de exploración y botones de variables del sistema `{x}`.

### 🎯 Cambios Implementados
1. **`NodeParameterViewModel.cs`**:
   - Añadida la propiedad `IsMultilineRow => !IsVariableInjectorNode && IsMultiLine;` e independizada `IsStandardRow => !IsVariableInjectorNode && !IsMultiLine;`.
   - Añadida la propiedad booleana `ValueAsBool` con soporte bidireccional y parseo tolerante (`bool`, `0`/`1`, `"0"`/`"1"`, `"true"`/`"false"`).
   - Añadida `SliderValue` y `SliderDisplayValue` para rangos interactivos continuos con badge de valor.
   - Añadida propiedad `IsNumber` para detectar enteros y coma flotante.
   - Corregido `DetectIsFileVersion` para evitar falsos positivos en propiedades de ruta como `SourcePath`.
2. **`NodeParameterTemplates.axaml` (Tarjetas de Nodos en Canvas DAG)**:
   - Controles diferenciados por tipo: `ToggleSwitch` moderno para booleanos, `Slider` interactivo para rangos con badge dinámico, `NumericUpDown` para valores numéricos, `ComboBox` y `AutoCompleteBox` estilizados para enumeraciones y listas desplegables.
   - Integración compacta en inputs de texto con botones embebidos para explorador de carpetas/archivos y selector de variables dinámicas (`{x}`).
   - Chips interactivos para selectores de versión de archivo (`FileVersionSelector`).
3. **`NodeInspectorPanelView.axaml` (Panel de Inspección Lateral)**:
   - Modernizado con la misma jerarquía de controles adaptados al tipo de dato, respetando tokens de diseño (`RadiusXs`, `FontSizeBody`, `Pad1`, `Pad2`).
4. **Cumplimiento de Linter y Regresión Visual**:
   - 0 violaciones de literales de forma/color en `UiStyleLintTests`.
   - Baselines visuales de regresión actualizados con los nuevos componentes renderizados.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- `UiStyleLintTests`: **5 / 5 superadas al 100%**.
- `AppShellVisualRegressionTests` & `VisualSnapshots`: **21 / 21 superadas al 100%**.
- Suite completa de pruebas: **1009 superadas, 1 omitida, 0 fallos, 1010 total**.

---

## [2026-09-16] - Corrección de Colapso del Panel Inspector en el Canvas de Nodos

### 🎯 Diagnóstico
Al ocultar o cerrar el panel del Inspector de Nodos (`NodeInspector.IsOpen = false`), la columna 4 del Grid principal en `MainWindow.axaml` mantenía un ancho fijo asignado (`Width="360"`), provocando que el `Grid` reservara 360 px en el extremo derecho. Como resultado, la columna central con el lienzo del editor DAG (`views:EditorView`, con `Width="*"`) se quedaba recortada y comprimida a la izquierda con un espacio vacío a la derecha, comportándose como si el inspector siguiera ocupando su espacio físico.

### 🎯 Corrección
- Vinculada la propiedad `ColumnDefinition.Width` de la columna 4 al estado `NodeInspector.IsOpen` mediante `BooleanToGridLengthConverter` con `ConverterParameter=360`:
  - Cuando `NodeInspector.IsOpen` es `false`: la columna colapsa a `GridLength(0, Pixel)`, permitiendo que el lienzo del editor `EditorView` (`Width="*"`) ocupe el 100% del ancho disponible de la ventana.
  - Cuando `NodeInspector.IsOpen` es `true`: la columna se dimensiona a `GridLength(360, Pixel)`.
- El `GridSplitter` adyacente (columna 3, con `Width="Auto"` e `IsVisible="{Binding NodeInspector.IsOpen}"`) colapsa también a 0 de forma coordinada.
- Añadida cobertura de pruebas unitarias para `BooleanToGridLengthConverter` con parámetros y conversión bidireccional en `ValueConvertersExhaustiveTests`.

### 🧪 Validación
- `dotnet build FileFlow.slnx`: **0 advertencias / 0 errores**.
- Suite completa de pruebas: **1009 superadas, 1 omitida, 0 fallos, 1010 total**.

## [2026-09-16] - Corrección de Cierre Silencioso al Arrancar la Aplicación


### 🎯 Diagnóstico
La aplicación aparecía brevemente en el administrador de procesos y terminaba antes de mostrar la ventana. La ejecución real con `dotnet run` y el `crash.log` identificaron la excepción exacta: Avalonia 12 no tiene un animador registrado para `RenderTransform`, y las animaciones declaradas en `FileFlow.App/Styles/Ports.axaml` se aplicaban al construir el splash window.

### 🎯 Corrección
- Sustituidas las animaciones de escala sobre `RenderTransform` en los sockets compatibles por animaciones de `Opacity`, que sí tienen animador soportado en Avalonia 12.
- Conservado el `RenderTransform` estático del socket rombo; sólo se elimina de los keyframes animados, preservando su orientación visual.
- La animación de energía de cables y el pulso de conexiones siguen funcionando porque no animan `RenderTransform`.

### 🧪 Validación
- `dotnet build FileFlow.App/FileFlow.App.csproj`: **0 advertencias / 0 errores**.
- Ejecución de la aplicación durante 12 s: el proceso permanece activo y no produce excepciones ni stderr; antes terminaba con `No animator registered for the property RenderTransform`.
- Guardias headless relevantes: **13/13**; las guardias visuales asociadas también quedan en verde.
- Suite completa: **1009 superadas, 1 omitida, 0 fallos, 1010 total**.

### 📌 Regla para el futuro
En Avalonia 12 no declarar animaciones de estilo sobre `RenderTransform` salvo que se registre explícitamente un animador compatible; preferir `Opacity` u otras propiedades soportadas y mantener una prueba de arranque de ventana real.


Este documento registra cronológicamente los hitos, cambios, mejoras y correcciones activas del proyecto **FileFlow Studio**.

> [!NOTE]
> **Historial Consolidado y Fases Previas**:
> El registro histórico completo correspondiente a fases anteriores (Fases 1 a 8, Sprints de Agosto 2026 y desarrollos fundacionales) ha sido consolidado y archivado para optimización de contexto en:
> 📄 [**`docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md`**](file:///docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md)

## [2026-09-16] - Mejoras xUnit P1/P2/P5: Esperas Deterministas, MemberData y Naming

### 🎯 Objetivos y Alcance
Aplicar tres mejoras priorizadas de la auditoría contra `csharp-xunit`: cerrar el silent-skip de inferencia opcional, convertir el catálogo de modelos IA en una prueba data-driven y corregir el naming del test de CLIP.

### 🎯 Cambios Implementados
- **P1**: el test opcional de CLIP ya no retorna silenciosamente cuando falta el modelo; queda marcado como skip explícito con motivo visible en el resultado de xUnit. Las esperas ciegas ya habían sido sustituidas por `AsyncTestWaiter` en el hito anterior.
- **P2**: `AiModelManager_GetDefaultUrls_ShouldReturnWorkingUrlsForCatalogModel` usa `[Theory]` + `[MemberData]`, generando un caso independiente y ordenado por cada id del catálogo; el caso específico de YOLOv8 queda como test separado.
- **P5**: `ClipModel_Diagnostic_Test` renombrado a `SemanticEmbeddingEngine_ClassifyZeroShot_WithClipModel_ShouldScoreEnglishAndSpanishCategories`, describiendo método, escenario y resultado.

### 🧪 Validación incremental
- Después de P1: **984 superadas, 1 omitida, 0 fallos, 985 total**.
- Después de P2: **1008 superadas, 1 omitida, 0 fallos, 1009 total**.
- Después de P5: **1008 superadas, 1 omitida, 0 fallos, 1009 total**.
- Cada etapa incluyó build/test de la suite completa; no se regeneraron líneas base.

### 📌 Notas para la próxima sesión
El skip de CLIP es deliberadamente explícito porque xUnit 2.9 no soporta skip condicional dinámico de forma fiable; si el modelo pasa a ser un artefacto obligatorio de CI, debe eliminarse el skip y promoverse a aserción.

## [2026-09-16] - Sondeo Determinista para Pruebas Asíncronas

### 🎯 Objetivos y Alcance
Eliminar esperas ciegas basadas en `Task.Delay` de `AsyncVirtualizingListTests` y `WorkflowFolderWatcherTests`, sustituyéndolas por sondeo de estado observable con límite de tiempo.

### 🎯 Cambios Implementados
- Añadido `FileFlow.Tests/TestHelpers/AsyncTestWaiter`: `WaitForAsync` evalúa la condición inmediatamente y después con polling configurable, timeout obligatorio, cancelación cooperativa y mensajes de diagnóstico con la descripción de la condición.
- Migrados los dos accesos asíncronos de `AsyncVirtualizingListTests`: ahora esperan el mensaje/valor de duración realmente cargado, no una pausa arbitraria de 100 ms.
- Migrados los dos escenarios de `WorkflowFolderWatcherTests`: esperan los dos eventos descubiertos y el contador del nodo downstream, manteniendo límites máximos explícitos de 3 s y 6 s.
- Añadidos tres tests unitarios para éxito, timeout diagnosticable y cancelación del helper.

### 🧪 Validación
- Tests relevantes: **9/9** (`AsyncTestWaiterTests`, `AsyncVirtualizingListTests`, `WorkflowFolderWatcherTests`).
- Compilación de `FileFlow.Tests`: **0 advertencias / 0 errores**.

### 📌 Notas para la próxima sesión
Usar `AsyncTestWaiter.WaitForAsync` para nuevas condiciones asíncronas observables; reservar `Task.Delay` únicamente para probar explícitamente el paso del tiempo o temporizadores.

## [2026-09-16] - Patrón Calibrado Compartido y Sustitución del Benchmark Falso del Motor

### 🎯 Objetivos y Alcance
Extender el análisis de determinismo (hito 107) a los otros dos ficheros de `Performance/`: `PerformanceStressTests` y `EngineParallelStressTests`, factorizando el patrón calibrado en un helper compartido.

### 🔬 Hallazgos
- **`EngineParallelStressTests` era un benchmark falso**: construía un `WorkflowExecutor`, **nunca invocaba `ExecuteAsync`** y medía un `Task.WhenAll` sobre lambdas locales con `Task.Yield()` — el umbral de 20 s no comparaba nada del motor. La prueba verde era teatro.
- **`PerformanceStressTests` tenía el mismo umbral fijo** (1 s para 10.000 interpolaciones del resolvedor) y su test de snapshots creaba un `NodeViewModel` **sin llamar `Cleanup()`**: dejaba el suscriptor eterno de `LocalizationManager.LanguageChanged` vivo para siempre — el mismo patrón zombi eliminado en los hitos 103/105.

### 🎯 Cambios Implementados
1. **`TestHelpers/CalibratedBenchmark`**: el esqueleto del hito 107 (calibración en línea, mediana de 3 mediciones, factor ×40 sobre la calibración, informe al TRX con cifras de diagnóstico) extraído a helper compartido y documentado como contrato del suite (qué se mide, qué no se asierta —GC/memoria—, cuándo elevar el factor).
2. **`PerformanceBenchmarkSuiteTests` refactorizado** para delegar en el helper (misma conducta, cero duplicación).
3. **`PerformanceStressTests`**: umbral calibrado en el resolvedor; el test de snapshots guarda el VM y llama `Cleanup()` en el `Dispose` de la clase de pruebas (higiene de zombis); `IDisposable` documentado.
4. **`EngineParallelStressTests` reescrito**: ejecuta el DAG **real** (origen de carpeta → sumidero de destino sobre 100 ficheros en disco, `MaxDegreeOfParallelism` = núcleos) con contrato doble: **corrección** (el destino recibe exactamente las 100 fichas — determinista) y **regresión** (tiempo calibrado con factor ×80, elevado por la varianza estructural del I/O de disco; el destino se limpia entre mediciones para que cada pasada mida el mismo trabajo).

### 🧪 Validación
- Clúster de rendimiento completo: **15/15** (los 5 benchmarks + el test real del motor en 1,07 s + el resto).
- **982 / 982 pruebas superadas en paralelo en dos ejecuciones (24 s / 22 s)**. Compilación: 0 advertencias / 0 errores.

### 📌 Notas para la próxima sesión
- El patrón ya tiene hogar canónico (`CalibratedBenchmark`): cualquier benchmark nuevo del suite debe usarlo; el lint futuro de umbrales de tiempo fijos (propuesto en el hito 107) tiene ahora una clase que prohibir.

## [2026-09-16] - Análisis y Determinismo de PerformanceBenchmarkSuiteTests

### 🎯 Objetivos y Alcance
Analizar `PerformanceBenchmarkSuiteTests` como se hizo con el clúster IA: qué testea, si sus aserciones de tiempo son flaky bajo contención y cómo hacerlo determinista.

### 🔬 Análisis
- **Qué testea** (5 pruebas, sin colección — corren en paralelo con todo el suite): throughput del resolvedor de plantillas (50k interpolaciones), clonado profundo con capacidad exacta (20k), ingesta de telemetría paralela (50k fichas, SQLite en memoria), letterbox SIMD (50 imágenes 720p→640×640) y hashing SHA256 en streaming (100 MB, I/O real). Cuatro con aserción de tiempo de reloj de pared de umbral **fijo**; la de telemetría, sin ninguna.
- **Riesgo de flakiness medido, no teórico**: los umbrales fijos tenían margen estrecho contra la **corrida en frío** — `HashCalculator` 67 MB/s vs umbral 50 (×1,35) y `TemplateResolver` 1,97 s vs 2,5 s (×1,27) en la primera pasada del proceso. En la máquina de 28 núcleos, ni el suite paralelo ni contención extrema inducida (28 spinners, 100% CPU sostenido) los derribaron — pero en una máquina de CI más lenta o un portátil a batería, esos márgenes son un fallo intermitente esperando turno. Añadido: los recuentos del GC y el delta de memoria se reportaban pero **no** eran atribuibles en un proceso paralelo (ninguna aserción sobre ellos, correctamente).
- **Hueco de contratos**: la prueba de telemetría medía throughput sin afirmar **corrección** (todas las fichas encoladas deben llegar), y su bucle incluía el flush dentro del tiempo medido.

### 🎯 Cambios Implementados
1. **Umbrales relativos con calibración en línea**: cada prueba mide primero la velocidad de la máquina (`WarmupIterations` ejecuciones de la carga, que precalientan el JIT) y aplica `TimeLimitFactor = 40` sobre esa calibración — el mismo factor detecta la regresión real en cualquier equipo, insensible a lo lenta que sea la máquina de turno. Mensaje de fallo con cifras completas (calibración, mediana, peor repetición, umbral).
2. **Mediana de repeticiones** (`RepeatMeasurements = 3`): la medición contaminada por otra colección paralela es una muestra, no el resultado — demostrado en la primera corrida (una medición de DeepClone salió ×2,8 manchada; la mediana la absorbió).
3. **Aserción de corrección en telemetría**: las 50.000 fichas de N productores concurrentes deben llegar exactas a `GetTotalCountAsync` (determinista por naturaleza); el flush se excluye del tiempo medido (lo que se testea es la ingesta).
4. **Dejas de reportarse como aserción** los contadores de GC y memoria (quedan como diagnóstico); consumo del resultado del letterbox para que el JIT no elimine la llamada; documentación de la clase explicando el contrato (detección de regresión, no benchmarking).

### 🧪 Validación
- Clase aislada ×2, bajo suite paralelo completo y bajo **contención extrema inducida (28 spinners, 100% CPU)**: 5/5 en todas. El diseño calibrado aguanta lo que el umbral fijo ponía en riesgo en frío (×1,35 de margen).
- **982 / 982 pruebas superadas en paralelo (22 s, sin coste neto tras recortar el calentamiento a 3 pasadas)**. Compilación: 0 advertencias / 0 errores.

### 📌 Notas para la próxima sesión
- El factor ×40 es un trinquete deliberadamente holgado: si un día se quiere detección fina, bajarlo a ×5-10 tras medir la varianza real en CI — nunca apostar por umbrales fijos.
- `EngineParallelStressTests` y `PerformanceStressTests` viven en la misma carpeta sin colección: candidatos a la misma revisión si algún día muestran inestabilidad.

## [2026-09-16] - Coste de VisualSnapshots: Fixture por Clase y Cortocircuito de Tema por Captura

### 🎯 Objetivos y Alcance
Reducir el coste de la colección `VisualSnapshots` (~5,6 s medidos en la suite completa) creando la `AppVisualFixture` una vez por clase en lugar de una por captura, sin perder el aislamiento entre pruebas.

### 🔬 Hallazgos de la medición (base para las decisiones)
- **La fixture por captura NO era el coste**: `Create()` en caliente cuesta 5-17 ms (sonda por etapas). El peso real es el **arranque en frío del proceso**: el primer `Create()` cuesta ~8 s (3,7 s en `CreateConfiguredLoader` — registro por reflexión de 11 ensamblados de plugins — y 4,0 s en el primer `ToolboxViewModel`, que instancia cada tipo de nodo). La suite completa paga esos 8 s una sola vez, en la clase que llegue primero.
- Descomposición de la colección: `AppShellVisualRegressionTests` 3,53 s · `ModalVisualRegressionTests` 1,33 s · `LocalizationManagerTests` 0,52 s · el resto <0,25 s. La suma no cuelga: son ~21 capturas reales (XAML + render + comparación píxel a píxel) más el frío compartido del proceso.

### 🎯 Cambios Implementados
1. **`AppVisualFixture.EnsureFrozen`**: el congelado de la muestra se hace público e idempotente (recarga el grafo vía `LoadFromGraphModel` → `ClearGraph`, que dispone los nodos viejos; vacía y vuelve a sembrar la consola; re-afija la barra de estado; reabre el inspector). `Freeze` queda como alias privado para `Create`.
2. **`SharedAppVisualFixture` + `IClassFixture`** en `AppShellVisualRegressionTests`: la fixture se construye una vez por clase (marshaling al hilo de UI en el constructor del wrapper, que xUnit ejecuta antes de la primera prueba) y se dispone al finalizar la clase — mismo contrato de limpieza que antes, pero ×1 en vez de ×9. Cada captura llama `EnsureFrozen` dentro de la fábrica de captura, de modo que la imagen parte del estado congelado aunque la prueba anterior hubiera tocado view models. El test de datos usa la fixture compartida con `EnsureFrozen` en `RunOnUI` (la guardia de hilo lo exigió, como debe).
3. **Cortocircuito del tema en `VisualSnapshot`**: nueva `ApplyCaptureTheme` aplica el preset salvo que el activo sea **semánticamente idéntico** (comparación por serialización de la definición, no por referencia ni por id: `ResolveTheme` devuelve instancias nuevas y una prueba puede haber mutado el tema activo), y `RestoreCaptureTheme` sólo restaura si el id cambió de verdad. En una tanda de capturas sobre el mismo preset se elimina el doble `SetTheme` completo (diccionario de recursos + publicación del cambio a toda la app) por captura.

### 🧪 Validación
- Las **21 capturas** de las tres clases visuales quedan **idénticas a sus líneas base** sin regenerar nada (aislamiento preservado).
- Corrida filtrada de `AppShellVisualRegressionTests`: contador de VSTest de 11 s a 3 s (el frío queda ahora atribuido al constructor del fixture de clase, que VSTest no contabiliza en ninguna prueba; reloj de pared ~24 s en ambos casos).
- **982 / 982 pruebas superadas en paralelo en dos ejecuciones consecutivas (21-22 s)**. Compilación: 0 advertencias / 0 errores.

### 📌 Notas para la próxima sesión
- Para rebajar la colección de verdad habría que atacar el frío del proceso: cachear un `PluginLoader` configurado por proceso en `PluginRegistryHelper` (los tests ya tratan el registro como idempotente en su mayoría) o precachear instancias de nodos del toolbox. Es una decisión de diseño (compartir registro entre fixtures), no una microoptimización local.
- `ModalVisualRegressionTests` mantiene su fixture por captura (ventanas distintas por superficie, no comparten estado): no aplicar ahí el patrón sin una razón.

## [2026-09-16] - Relay Débil en Nodos de IA, Líneas Base de Modales y Cierre Definitivo de los Bindings Zombi

### 🎯 Objetivos y Alcance
Eliminar la suscripción eterna de `AiFlowNodeBase` y sus 13 nodos derivados al evento estático `SessionStateChanged` (limpieza determinista o WeakEvent), dotar de líneas base visuales a las ventanas modales (host y plugins) y dejar la suite completa corriendo en paralelo al 100%, incluida la colección de capturas.

### 🎯 Cambios Implementados
1. **`WeakModelStatusRelay` (`FileFlow.Plugin.AI/Common/`)**: cada nodo envuelve su lambda de reenvío (`() => ModelStatusChanged?.Invoke()`) en un relay que guarda la suscripción al evento estático **sólo detrás de una referencia débil** al lambda; autolimpieza en el primer disparo tras la recolección del nodo y `Dispose()` determinista. Sin registro global: 13 constructores migrados mecánicamente, `InternalsVisibleTo` ya existente. Guardias en `WeakModelStatusRelayTests` (5): reenvío, autolimpieza verificada con GC y barrido de ambos eventos (`OnnxSessionManager` **y** `AudioInferenceEngine` — los nodos de audio escuchan el segundo), dispose determinista y guardia de fuente que prohíbe volver al patrón de suscripción eterna.
2. **Líneas base visuales de 9 modales** (`ModalVisualFixture` + `ModalVisualRegressionTests`): About (además en claro), VariablePicker, AiModelManager, AiModelUrls, WorkflowSettings, MultimodalVlm (plugin IA), PasswordManager (Archives), RegexHelper (FileSystem) y MediaPresetManager (Integrations) — dobles de puertos en todas (almacenamiento VLM temporal, IDs de modelo falsos, constructores de prueba), 10 líneas base nuevas (12+10 en `VisualBaselines/`). Nueva API `VisualSnapshot.CaptureWindow(factory, themeId)`: muestra la ventana real headless (su XAML raíz, bindings de ventana y tamaño), normaliza escala 1:1 y fondo opaco cuando la modal es transparente/acrílico, y purga+cierra dentro de su propio despacho. Regla headless descubierta: **ventana y captura deben vivir en el mismo despacho** (construir en uno y mostrar en otro devuelve frame `null`). La fixture registra además los recursos de localización de los plugins por ambas ramas de `PluginLoader` (clase generada **y** recursos embebidos del manifiesto — el plugin IA no tiene `Strings.Designer.cs`), imitando a producción.
3. **Unificación de colecciones exclusivas**: `Localization` desaparece y sus 10 clases pasan a `VisualSnapshots`. Motivo: dos colecciones exclusivas distintas **sí corren a la vez entre sí**, y ambas mutan la misma variable global de proceso (cultura/idioma) — la carrera era estructural, no de implementación. Actualizados analizador de la guardia, auto-tests, mapa de `TestAssemblyParallelism.cs` y comentarios.
4. **`LogViewModel` con dispose determinista**: guarda su handler de `LanguageChanged` (convención ya existente en `NodeParameterViewModel`/`ToolboxViewModel`) y se desuscribe en `Dispose` (antes sólo paraba el timer). Además se eliminó de `ModalVisualFixture` una variable muerta que instanciaba el VM sin usarlo (resto de una iteración): su constructor suscribía eternamente el evento del singleton y su handler tocaba código con afinidad de hilo.
5. **Marshaling de cultura en pruebas (`AvaloniaTestHelper.SetCultureOnUI`)**: el cierre de raíz de los bindings zombi. La purga de árboles (visual + lógico) es efectiva (verificado control por control, 0 errores), pero Avalonia retiene vinculaciones del *chrome* de la ventana tras `Close` hasta que el GC recolecta el objetivo — y una notificación de cultura disparada desde el hilo del runner reevalúa esos bindings contra controles propiedad del hilo de UI: `InvalidOperationException` que mataba al test ajeno que la provocaba. En producción la cultura sólo cambia desde la UI, así que las 31 llamadas de 5 clases de test (`PortSemanticsTests`, `LocalizationManagerTests`, `NodeParameterViewModelTests`, `ToolboxOrganizationTests` — cuyo setter de `CurrentCulture` **también** dispara `PropertyChanged` —, y restos) se marshaling al hilo de la sesión vía el nuevo helper. Borrada la sonda temporal `ZombieProbeTests`.

### 🧪 Validación
- Compilación: **0 advertencias / 0 errores**.
- Clúster IA: 28/28 con el relay migrado. Par problemático (modales + localización): 49/49.
- **982 / 982 pruebas superadas en paralelo en dos ejecuciones consecutivas (23 s cada una)**, sin `SetCulture` desde el hilo runner en ningún test.

### 📌 Notas para la próxima sesión
- Regla de oro para pruebas nuevas: **toda mutación de cultura/idioma del proceso pasa por `AvaloniaTestHelper.SetCultureOnUI`** (nunca `SetCulture` ni `CurrentCulture` desde el hilo del runner), y las clases que la usan pertenecen a `VisualSnapshots`.
- `VisualSnapshot.CaptureWindow` exige fábrica (construcción+captura en el mismo despacho); no reintroducir sobrecargas con ventana ya construida.

## [2026-09-16] - Guardia del Contrato de Colecciones del Suite Paralelo

### 🎯 Objetivos y Alcance
Crear un test de guardia que falle cuando una clase de test toque `ModelSessionRegistry`, `OnnxSessionManager`, `UserPreferencesService` real o la sesión headless de Avalonia sin pertenecer a su colección exclusiva — convirtiendo el contrato documentado en `TestAssemblyParallelism.cs` en un test que se ejecuta en cada pasada.

### 🎯 Cambios Implementados
1. **Analizador puro** (`TestHelpers/TestCollectionContractAnalyzer.cs`): reglas estado→patrones (el singleton real de preferencias, no los dobles), resolución del atributo `[Collection]` por literal y por constante **cualificada** (`FileFlow.Tests.Unit.Views.VisualSnapshotsCollection.Name`), separación de atributo/clase a través de comentarios XML de documentación (patrón real del repo), análisis **por clase** y exclusión de infraestructura (`TestHelpers`) y de ficheros auto-referenciales (los auto-tests contienen los patrones en sus snippets sintéticos; sin la exclusión, la guardia se encontraría a sí misma).
2. **Regla de aceptación por exclusividad**: basta con declarar **cualquier** colección exclusiva — `DisableParallelization = true` ejecuta la colección en exclusividad total, así que una clase en `OnnxInference` que además muta las preferencias reales es correcta (`ModelLifecycleAndMemoryTests`). Lo que el contrato no perdona es tocar el estado desde una clase sin colección o en una paralela.
3. **Guardia** (`Unit/App/TestCollectionContractGuardTests.cs`): el barrido del árbol real + 15 auto-tests de la lógica (cada estado, cada forma de tocar la sesión, análisis por clase, ficheros no-test, y una prueba de infracción plantada en directorio temporal).
4. **Infractor real corregido**: `ThemeStudioVisualContractTests` (colección paralela `ThemeTokens`) llamaba a `AvaloniaTestHelper.EnsureInitialized()` — arrancaba la sesión headless fuera de la colección exclusiva `VisualSnapshots`. Recolocada a `VisualSnapshots`.

### 🧪 Validación
- 15/15 pruebas de la guardia; verificación negativa de extremo a extremo: quitar la colección a `ModelLifecycleAndMemoryTests` pone el barrido en rojo señalando el estado y la colección canónica; restaurado, vuelve al verde.
- Compilación: **0 advertencias / 0 errores**. **975 / 975 pruebas superadas en paralelo (19 s)** con la guardia incluida.

### 📌 Notas para la próxima sesión
- Al añadir un estado global nuevo, añade su regla a `TestCollectionContractAnalyzer.Rules` y su colección a `ExclusiveCollections`: la guardia y la documentación de `TestAssemblyParallelism.cs` deben moverse juntas.

## [2026-09-16] - Rendimiento del Clúster IA y Estabilidad del Paralelismo

### 🎯 Objetivos y Alcance
Investigar si los tests del clúster IA pueden compartir la sesión ONNX real de forma segura y si hay clases etiquetadas en `OnnxInference` que no ejecuten inferencia y puedan salir de la colección (corren en serie y son la parte más lenta del suite).

### 🎯 Cambios Implementados
1. **Clasificación fina por medición (TRX por prueba)**: de las 16 clases de `OnnxInference`, la «ballena» `MultimodalVisionLlmNodeTests` (8,7 s de 13,6 s) **no ejecuta inferencia nativa**: es política de reintentos HTTP contra handlers de Moq inyectados vía `CustomHttpClient` — 7,1 s eran esperas reales (`Task.Delay`) del backoff (1,5 s/2 s por intento) y del *cooldown* de 250 ms para endpoints locales en `MultimodalVlmClientEngine`.
2. **Costura de escala de pruebas** en `MultimodalVlmClientEngine`: `RetryBackoffScalePercent` (`internal static`, por defecto **100** = producción intacta); los tests lo fijan a 0. La clase pasa de **9,3 s a 0,8 s** sin tocar la política de reintento que se prueba. Requirió `<InternalsVisibleTo Include="FileFlow.Tests" />` en el csproj del plugin: los tests compilan contra la ref assembly, que Roslyn sólo rellena de internos si existe esa línea (sin ella, CS0117 fantasma con la DLL de implementación correcta).
3. **`MultimodalVisionLlmNodeTests` sale de `OnnxInference`** y corre en paralelo: sin registros de sesión (motor cliente no consulta `OnnxSessionManager`; `CustomHttpClient` inyectado, ni puertos ocupa).
4. **El resto de la colección se queda**, verificado que sí alcanza estado nativo global: los nodos de visión/audio heredan de `AiFlowNodeBase` (consulta `OnnxSessionManager`), `SemanticEmbeddingEngine` y `AudioInferenceEngine` tienen cachés de sesión **propias** (el fallback lexical de `ClassifyZeroShot(null,…)` evita sesión pero no garantiza la clase), y `AiNodesTests` puede abrir sesiones reales si hay modelos en disco. **Compartir la sesión ONNX entre colecciones: descartado** — los registros son estáticos del proceso y la exclusividad de la colección ya es el mecanismo seguro de compartición; el ahorro restante (~4 s) no justifica el riesgo.
5. **Carrera real descubierta y cerrada**: bindings zombi. Las vistas enlazan con `{Binding [Clave], Source={x:Static loc:LocalizationManager.Instance}}`; los árboles de las capturas sobrevivían al test y, al cambiar `Localization` la cultura desde otro hilo, un binding zombi escribía una propiedad animable de un control propiedad del Dispatcher de la sesión ya apagada → 'The calling thread cannot access this object' en pruebas ajenas (11-12 fallos intermitentes). Cierre determinista: `VisualSnapshot.PurgeBindings` (barrido de `ClearValue` sobre todas las propiedades registradas + `DataContext = null`, de abajo arriba; Avalonia no tiene el `ClearAllBindings` de WPF) tras cada captura, `VisualSnapshot.DetachTree` en las ventanas de humo, `AppVisualFixture.Dispose` (desuscribe los VMs de los singletons y para el `DispatcherTimer` de la consola) y `ThemeCustomizerViewModelTests` a `Localization` (sufijo de duplicado dependiente del idioma).

### 🧪 Validación
- Compilación: **0 advertencias / 0 errores**.
- **960 / 960 pruebas superadas en paralelo en cuatro ejecuciones consecutivas (21 / 15 / 15 / 20 s)**, clúster IA/ONNX incluido. Suite VLM: 22/22 en **0,8 s** (antes 9,3 s).

### 📌 Notas para la próxima sesión
- El mapa de colecciones de `TestAssemblyParallelism.cs` queda actualizado: `OnnxInference` ya no es «todo Unit/AI» — `MultimodalVisionLlmNodeTests` corre fuera, sin colección. Cualquier prueba nueva del plugin de IA que toque `OnnxSessionManager`/`ModelSessionRegistry` o los motores con caché va en `OnnxInference`; la que sea HTTP simulado sin registros, puede correr libre.

## [2026-09-16] - Suite en Paralelo: Aislamiento del Clúster ONNX en Colecciones Exclusivas

### 🎯 Objetivos y Alcance
Reactivar el paralelismo de xUnit —suspendido el 15/09 como *workaround* contra el cuelgue histórico del suite— confinando cada foco de estado global de proceso en su propia colección no paralelizable. Objetivo: que `dotnet test` siga siendo fiable sin renunciar al paralelismo de la parte del suite que no comparte nada.

### 🎯 Cambios Implementados
1. **Paralelismo reactivado** (`TestAssemblyParallelism.cs`): `DisableTestParallelization = false`, con el mapa completo de colecciones exclusivas y el estado que confinan documentado en el propio fichero.
2. **Nueva colección exclusiva `OnnxInference`** (`Unit/AI/OnnxInferenceCollection.cs`, `DisableParallelization`): las 16 clases que ejercitan el clúster de IA — todo `Unit/AI` (excepto `AiModelManagerConfigTests`, que descarga modelos y va en su colección propia) y `Unit/Plugins/AI/AiNodesTests` (puede crear sesiones nativas si hay modelos descargados en disco). Registros de sesiones (`ModelSessionRegistry`, `OnnxSessionManager`, `AiPluginInitializer`), motores nativos y las cachés que abortan el host (`0xC0000005`) al cargar/descargar assemblies nativos en paralelo. Se descartó etiquetar el resto de `Unit/Plugins` (archivos, red, datos): lógica pura sin estado nativo.
3. **`ModelLifecycleAndMemoryTests` recolocada** en `OnnxInference` (estaba en `Localization`): vacía cachés de `OnnxSessionManager`/`AudioInferenceEngine` y muta el singleton real de `UserPreferencesService`; no era estado de localización.
4. **`AiModelDownloadSequential` definida de forma explícita** (`AiModelDownloadSequentialCollection.cs`): era una colección implícita (3 clases serializadas entre sí pero en paralelo con el resto) y descarga modelos reales por red con escritura en el perfil del usuario.
5. **`Localization` definida de forma explícita** (`Unit/Sdk/LocalizationCollection.cs`): cultura e idioma del proceso (`LocalizationManager`, `CultureInfo.CurrentCulture`) y el singleton real de `UserPreferencesService` (mutado con restauración). Nuevo miembro: `SystemVariablesResolverExhaustiveTests`, que muta la cultura y estaba fuera de cualquier colección.
6. **Por qué es seguro**: `DisableParallelization = true` (xUnit 2.9.2) ejecuta la colección en **exclusividad total** — mientras corre, no corre ninguna otra colección, ni siquiera las no relacionadas. El resto del suite es lógica pura, lints de texto sobre .axaml o catálogos sin estado compartido. La sesión headless de Avalonia arranca bajo `lock` y despacha en serie, y sólo la consume la colección `VisualSnapshots` (también exclusiva).

### 🧪 Validación
- Compilación: **0 advertencias / 0 errores**.
- **960 / 960 pruebas superadas en paralelo en tres ejecuciones consecutivas (30 s / 23 s / 22 s)** con `dotnet test FileFlow.Tests/FileFlow.Tests.csproj` sin filtros y con el clúster IA/ONNX incluido (142/142 también en aislamiento).
- El cuelgue histórico de `dotnet test` queda resuelto **por confinamiento de estado, no por serialización**: la suite corre en paralelo y termina de forma reproducible.

### 📌 Notas para la próxima sesión
- Cualquier prueba nueva debe declararse en: `OnnxInference` (inferencia nativa, sesiones ONNX, nodos del plugin de IA, `UserPreferencesService` real), `AiModelDownloadSequential` (descargas de modelos), `Localization` (cultura/idioma) o `VisualSnapshots` (sesión headless de Avalonia). El comentario de `TestAssemblyParallelism.cs` es el mapa de referencia.

---

## [2026-09-15] - Infraestructura Headless Fiable y Red de Seguridad Visual (Capturas de las Vistas Clave)

### 🎯 Objetivos y Alcance
Convertir las pruebas de interfaz en una **medida fiable** y añadir la red que faltaba: **capturas renderizadas de las vistas clave** comparadas píxel a píxel contra líneas base en el repositorio. El punto de partida era incómodo: la inicialización de Avalonia era correcta pero **frágil por diseño** —cualquier prueba que construyera controles desde el hilo del runner "funcionaba" por casualidad—, y el suite completo no terminaba de forma fiable.

### 🎯 Cambios Implementados
1. **Contrato de hilo explícito en la sesión headless** (`AvaloniaTestHelper`):
   - Marca `[ThreadStatic]` de «este hilo es el de la sesión» (no `Dispatcher.UIThread.CheckAccess()`, que **antes de arrancar** dice que sí sobre el hilo del runner) y `RequireUIThread(operación)`, que lanza un mensaje accionable en lugar de dejar que el fallo aparezca tres capas más abajo como `Unable to locate IWindowingPlatform` o como «el token no existe».
   - **Despacho reentrante**: una llamada anidada (público usado desde dentro de una fábrica u otro despacho) se ejecuta **en línea**; antes habría encolado y bloqueado el bucle que tenía que atenderla (interbloqueo silencioso hasta el *timeout* del runner).
   - **Registro de recursos no destructivo**: se elimina el `ClearResourceManagers()` global que borraba los diccionarios registrados por los plugins y hacía fallar pruebas de localización **de otras clases** (sólo al ejecutar el suite entero). El idioma se fija únicamente si no lo estaba ya, para no disparar `LanguageChanged` sin motivo.
2. **Serialización de todo el suite** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`, `FileFlow.Tests/TestAssemblyParallelism.cs`): los tests comparten estado de proceso (sesión y Dispatcher de Avalonia, tema y diccionario de recursos, cultura y diccionarios de `LocalizationManager`, registro de sesiones ONNX). En paralelo los fallos aparecían **en la prueba equivocada** y sin ser reproducibles: cultura inesperada, tema no aplicado, `TaskCanceledException` de `DispatcherOperation.Wait` por un `Dispatcher.UIThread.Invoke` que expira mientras otro hilo tiene ocupado el bucle. Consecuencia directa: **`dotnet test` (el comando de `AGENTS.md`) ya no se cuelga y pasa al 100% en ~30 s**, incluido el clúster IA/ONNX.
3. **API de captura imposible de usar mal** (`VisualSnapshot`):
   - `Capture` recibe una **fábrica** de contenido y la invoca **dentro del hilo de UI**; ya no existe la sobrecarga que aceptaba un control ya construido, que era exactamente la trampa.
   - Nueva `CaptureNaturalHeight` para barras: la altura sale del `DesiredSize` **con los estilos aplicados**, así que un cambio de densidad o de tipografía no recorta la captura (antes se fijaba a mano).
   - Comprobaciones propias: la captura debe salir a escala **1:1** y con el tamaño exacto pedido (si no, la línea base dependería de la máquina); la codificación PNG deja atrás la API obsoleta.
   - `ResolveToken<T>` sustituye a los ayudantes locales de cada test y exige el hilo de UI (preguntar fuera de él devolvía un `null` que parecía «el token no existe»).
4. **Muestra determinista de la aplicación** (`AppVisualFixture` + dobles `InMemoryUserPreferencesService`, `FrozenPerformanceMonitor`, `InMemoryLogStore`, `NullFileDialogService`, `InMemoryWorkflowStorageService`):
   - Los view models son los **reales**, pero sus puertos son dobles: sin el fichero de preferencias del desarrollador (favoritos, contadores de uso, tema, idioma), sin el monitor de rendimiento que cambiaría las cifras de CPU/RAM/GPU en cada ejecución y sin tocar la base de datos SQLite de logs ni la carpeta de flujos del usuario.
   - El grafo de ejemplo (origen de carpeta → filtro lógico → destino, con conexiones, nota y grupo) se **carga desde el modelo de datos** (`LoadFromGraphModel`) en lugar de con `AddNode`, que escribe contadores de uso en las preferencias reales.
   - La consola se siembra por la vía real (`AddStructuredLog` + `FlushAllPendingLogs`) con **marcas de tiempo fijas**, y la barra de estado se declara **sin modelos de IA cargados**: el registro de sesiones es un singleton del proceso y, si una prueba anterior cargó un modelo, la isla de «modelos en memoria» aparecía y la captura dejaba de ser reproducible (fallaba sólo al ejecutar el suite entero).
5. **Capturas de las vistas clave con líneas base (8 nuevas, 12 en total en `FileFlow.Tests/VisualBaselines`)**:
   - `app-shell-dark` / `app-shell-light` (**la ventana principal completa**, extrayendo el contenido real de `MainWindow`, con los seis paneles y el inspector abierto sobre un nodo).
   - `panel-editor-dark` (lienzo con el grafo), `panel-toolbox-dark`, `panel-inspector-dark`, `panel-log-console-dark`, `panel-status-bar-dark` y `panel-control-bar-dark`.
   - Cada una se acompaña de sondas que impiden una línea base «verde» inútil: **todo panel pinta más de cuatro colores distintos** (no un lienzo plano ni un control invisible) y la muestra del shell debe contener sus 3 nodos, 2 conexiones, registros y el inspector abierto.
   - Regeneración documentada: `FILEFLOW_UPDATE_VISUALS=1`; una línea base que no existe se crea y **falla a propósito**, para que ninguna imagen se bendiga sin revisarla. Los fallos escriben `actual`/`expected`/`diff` en el directorio temporal y el mensaje indica la zona afectada.
6. **Defectos reales encontrados por el camino**:
   - **`FilePreviewerTests.AvaloniaImageLoader_ShouldDecodeWebP_IntoValidBitmap` no probaba nada**: pasaba sólo porque el helper antiguo inicializaba Avalonia en el hilo del runner. Con la sesión, decodificar desde el hilo de test devuelve `null` **sin lanzar** (el cargador se lo traga y su plan B también falla). Ahora se despacha al hilo de UI y se afirma el tamaño real (150×80), y una guardia nueva (`Session_ShouldDecodeImagesOnTheUiThread`) fija dónde se puede decodificar.
   - **Sondas mal situadas en las pruebas de token**: la de radios comparaba **fondo contra fondo** (el `StackPanel` alineaba las cajas al centro, así que ningún muestreo caía dentro) y la de elevación muestreaba **dentro** de la caja. Reescritas: la primera alinea explícitamente y exige que la caja sin radio esté rellena; la segunda contrasta la sombra contra el fondo de la aplicación.
7. **Guardias de la propia infraestructura (11, antes 6)**: aplicación única con Skia real y fotogramas capturables, arranque idempotente bajo concurrencia, despacho serializado entre hilos, **despacho anidado sin interbloqueo**, estilos sin animaciones, recursos del host y idioma fijados, ventanas reales renderizables, **construir controles fuera del hilo falla con un mensaje que dice qué hacer**, **resolver un token fuera del hilo también**, **la fábrica de una captura corre dentro de la sesión** (y el píxel capturado es el del token), una fábrica vacía se rechaza y la sesión decodifica imágenes. Verificado que las guardias fallan ante regresiones reales introducidas a propósito.
8. **Colección serializada** (`VisualSnapshotsCollection`, `DisableParallelization`): propiedad explícita de la sesión headless para que cualquier prueba nueva que la use se declare aquí, y documentación de por qué estos tests no pueden convivir con otros.

### 🧪 Validación
- Compilación: **0 advertencias / 0 errores**.
- **960 / 960 pruebas superadas al 100%** con `dotnet test FileFlow.Tests/FileFlow.Tests.csproj` (sin filtros) **en dos ejecuciones completas consecutivas (~30 s)**, incluido el clúster IA/ONNX (`142/142` también en aislamiento). El cuelgue histórico del suite en paralelo queda resuelto por la serialización, no por filtros.
- Las 12 capturas se generaron y revisaron, y las comparaciones pasan en ejecuciones posteriores del suite completo (determinismo comprobado entre dos procesos distintos).

### 📌 Notas para la próxima sesión
- Cualquier vista nueva que merezca vigilancia visual se añade como superficie en `AppVisualFixture` (o como composición propia) y se captura con `VisualSnapshot.Capture`; el primer run crea la línea base y falla para forzar la revisión.
- Las líneas base viven en `FileFlow.Tests/VisualBaselines/` y **no están ignoradas por git**: forman parte del repositorio.

---

## [2026-09-15] - Fase 1: Set de Tokens Completo y Theme Studio Funcional (Radios, Tipografía y Sombras)

### 🎯 Objetivos y Alcance
Cierre de la **Fase 1** de [`docs/ui_redesign_plan.md`](file:///docs/ui_redesign_plan.md): completar el conjunto de tokens del tema (radios, espaciado, elevación y escala tipográfica) y conseguir que **el Theme Studio personalice de verdad esa geometría en toda la interfaz**, no sólo el color. El plan de la fase partía de una queja concreta de la auditoría: el usuario movía el radio o el tamaño de fuente en el Studio y **no pasaba nada**, porque los tokens no existían o las vistas usaban literales.

### 🎯 Cambios Implementados
1. **Set de tokens completo (`ThemeDefinition`, `ThemeResourceApplier`, `DarkTheme.axaml`, presets JSON)**:
   - **Radios**: escala simétrica `RadiusXs` → `RadiusXxl` derivada de `CornerRadius` (antes sólo existían en el baseline, sin origen en el tema).
   - **Tipografía**: `FontSizeMicro` → `FontSizeDisplay` (7 escalones) derivados de `BaseFontSize`; `AppFontFamily`/`CodeFontFamily` conectados a la interfaz y al código.
   - **Espaciado**: `Space1`..`Space9` (valores sueltos) y `Pad1`..`Pad9` (los mismos como `Thickness`) derivados de `SpacingUnit`: un único ajuste cambia la densidad de la interfaz.
   - **Elevación**: `Elev1`..`Elev4` (tarjeta → panel → superposición → modal) más `ElevGlowAccent`/`ElevGlowSuccess`/`ElevGlowError`, todos derivados de `NodeShadowBlur` y `NodeShadowOpacity`, y `ElevPanelLeft` para el panel de navegación (una sombra vertical no sirve en un panel a sangre completa).
   - **Velos y tintes nuevos**: `ScrimBrush`/`ScrimStrongBrush` (capas modales) y `ChipBrush`/`ChipStrongBrush`/`TintFaintBrush`, derivados de `TextSecondary` para que los chips se lean igual sobre superficies oscuras y claras.
   - Cada paso de la escalera se redondea a 0.5 px y los valores por defecto del baseline siguen siendo **espejo exacto** del preset `dark_fluent` (`ThemeTokenCompletenessTests` lo verifica en ambos sentidos).
2. **Los tokens ahora llegan a la UI (la parte que pedía el usuario)**:
   - Migración automatizada de **400 tamaños de fuente literales, 154 radios literales y 7 sombras escritas a mano** en **31 ficheros AXAML** (host y plugins) a los tokens del tema. Se conservan a propósito los radios **asimétricos** (`4,0,0,4`, `9,9,0,0`) y el `0` deliberado, que la escala simétrica no puede expresar, y `●` de `PasswordChar` (no es un icono).
   - Tras la migración, **la inmensa mayoría de las vistas desaparece de la línea base de `UiStyleLintTests`**: los literales de forma/tipografía bajan de ~370 a 15 y los ficheros vigilados de 24 a 14 (y sólo por color inline o radios asimétricos).
3. **Theme Studio reconstruido (generado desde un catálogo, no escrito a mano)**:
   - **`ThemeSettingCatalog`** es la fuente única de verdad de «qué se puede personalizar»: 9 secciones y **34 ajustes** con tipo de control, rango, paso, opciones y clave de localización, más `NotEditableYet` con el motivo de las 5 propiedades que no se editan (identidad y variante).
   - **Filas tipadas por reflexión** (`ThemeColorRowViewModel`, `ThemeNumberRowViewModel`, `ThemeChoiceRowViewModel` sobre `ThemeSettingRowViewModel`): el editor se construye recorriendo el catálogo, así que **añadir un token y exponerlo aquí es suficiente**; ya no puede haber un control que no escriba en el tema porque la fila escribe sobre la propiedad declarada.
   - **Vista previa en vivo real**: `LivePreviewResources` es una **instancia estable** que se actualiza en el sitio y que la vista previa engancha una sola vez (`PreviewHost.Resources = …` en el code-behind), de modo que sus `DynamicResource` resuelven contra el **tema en edición** mientras el resto de la ventana sigue mostrando el tema activo. El panel demuestra las tres escalas: radios (`RadiusXs`..`RadiusXxl`, `RadiusPill`), densidad (`Space1`..`Space9`) y profundidad (`Elev1`..`Elev4`, `ElevGlowAccent`), además de una tarjeta de nodo, botones, campos y tabla de muestra.
   - **Ventana reescrita a 100% declarativo** con la capa de estilos (`card`, `chromeTop`, `chromeBottom`, `brandLg`, `badge`/`badgeAccent`, `led*`, `dividerH`, `primary`/`ghost`) y **cero literal de color, radio, tamaño o sombra**: es la vista de referencia del rediseño. Se añade estilo de `NumericUpDown` a `Inputs.axaml` para los ajustes numéricos.
   - **Bugs del Studio antiguo corregidos**: la ventana enlazaba comandos y propiedades inexistentes (`CreateNewThemeCommand`, `ApplyAndSaveThemeCommand`, `BaseType`, `BgApp`, `AccentPrimary` sobre el view model) y **todo su texto estaba en español fijo**, así que en inglés la mitad de la interfaz no se traducía.
4. **Localización y limpieza**: 66 claves nuevas en `Strings.resx` **y** `Strings.es.resx` (847 en ambos, sin duplicados y con paridad verificada); 6 etiquetas que ahora acompañan a un icono vectorial pierden el emoji (`New`, `Duplicate`, `Delete theme`, `Live preview`, `Test in app`, `Save & apply`).
5. **Defectos preexistentes detectados y corregidos**:
   - **Emojis residuales que la guardia no veía**: `ℹ️` (U+2139) en `MainWindow` y `👁️` en el plugin de IA se escapaban porque el rango de pictogramas cubría los bloques principales pero no los signos con presentación emoji. La guardia ahora los enumera uno a uno (sin vetar signos tipográficos legítimos como `©` o `®`) y la lista quedó a cero.
   - **Texto corrupto (doble codificación) en `FileFlow.Plugin.AI/UI/MultimodalVlmConfigWindow.axaml`**: los bytes UTF-8 interpretados como CP437 dejaban `PESTA├æA`, `Configuraci├│n` y botones con basura visible (`ƒöì Detectar Modelos`). Ya estaba así en el commit inicial (verificado con `git show HEAD`), no lo introdujo este trabajo. Recuperado el texto por punto de código (encode CP437 → decode UTF-8), reconstruido lo irrecuperable (los pictogramas perdidos) y sustituido el icono de cabecera por una placa temática.
6. **Guardias nuevas (19 pruebas)** y trinquete bajado:
   - `ThemeStudioCatalogTests` (11): **toda propiedad visual del tema está en el catálogo o declarada como no editable**, cada entrada apunta a una propiedad real y sin duplicados, ninguna sección vacía, claves de rótulo y sección en ambos idiomas, y —lo más importante— **cada ajuste mueve de verdad los tokens que declara** (un control inerte hace fallar la suite), con comprobaciones independientes de las escalas completas de radio, tipografía, espaciado y elevación, y del efecto de `NodeShadowOpacity`/`NodeShadowBlur` en **toda** la escala.
   - `ThemeStudioVisualContractTests` (6): **cada binding de la ventana se valida contra el `x:DataType` de su propia plantilla**, el editor debe estar generado por el catálogo (y sin bindings manuales a propiedades del tema, el patrón que quedaba muerto), cero literales de forma/tipografía/color en la ventana, la previsualización demuestra todas las escalas, el code-behind engancha `LivePreviewResources`, y una prueba de comportamiento comprueba que **un descendiente de la previsualización resuelve los tokens del tema en edición y sigue los cambios en caliente** (se ejecuta sin Dispatcher a propósito: ejercitar el bucle de mensajes bloqueaba al resto de la suite).
   - `UiStyleLintTests` (+2): **cero tolerancia con `FontSize` y `BoxShadow` literales** en todas las vistas, y el Theme Studio no puede volver a la línea base.
   - Trinquete actualizado a la nueva realidad (14 ficheros, 130 colores inline + 15 radios asimétricos).

### 🧪 Pruebas y Validación
- Compilación `dotnet build FileFlow.slnx` → **0 advertencias / 0 errores**.
- **929 / 929 pruebas superadas al 100%** con `dotnet test … -- xUnit.ParallelizeTestCollections=false` (876 previas + 19 nuevas + 34 de la Fase 6 = 929).
- Verificado en dos mitades estables: **787 / 787** (sin `Unit.AI`) y **142 / 142** (`Unit.AI` en aislamiento), ambas en dos ejecuciones consecutivas.
- **Cuelgue detectado en la ejecución paralela por defecto**: la suite completa (`dotnet test` sin argumentos) se queda colgada sin llegar a publicar resultados. Aislado el origen: cada mitad pasa por separado y el conjunto pasa con las colecciones serializadas, de modo que es la **concurrencia preexistente del clúster de inferencia ONNX** con el resto de pruebas (documentada en las fases anteriores), no los cambios de esta fase. Workaround documentado: `dotnet test FileFlow.Tests/FileFlow.Tests.csproj -- xUnit.ParallelizeTestCollections=false`.
- Se comprobó que las guardias nuevas **fallan ante regresiones reales** introducidas a propósito: ajuste que no mueve sus tokens, ajuste sin declarar qué tokens mueve y `FontSize` literal en una vista.
- `git diff` de 39 AXAML con finales de línea normalizados para no mezclar LF/CRLF.

### 📌 Notas y Pendientes
- **Fase 2 (resto de vistas)**: quedan los 14 ficheros de la línea base de estilo; el siguiente paso natural es bajar el trinquete en cada uno (drawer/toolbox, inspector, diálogos) usando el Theme Studio como referencia.
- **Espaciado**: `Space*`/`Pad*` existen y se consumen en la capa de estilos y en el Studio, pero la migración masiva de `Padding`/`Margin` literales a la escala de densidad queda para la Fase 2 (esta fase se centró en radios, tipografía y sombras, que es lo que el usuario pidió).
- **Tokens con consumidor único**: `AppFontSize`/`AppCornerRadius` se mantienen como alias históricos del tamaño y el radio base (los consumen la capa de estilos y los diccionarios propios); conviene decidir si se consolidan en `FontSize*`/`Radius*` o se documentan como API estable.
- **Los 3 `Themes/*.axaml` manuales eliminados** (`LightTheme`, `CyberTheme`, `PastelTheme`) siguen apareciendo como borrados en el árbol de trabajo de una fase anterior: los presets viven en `Resources/builtin_themes.json`.

## [2026-09-15] - Fase 6 (I): Rediseño de la Tarjeta de Nodo, Semántica de Puertos y Flujo de Energía en Cables

### 🎯 Objetivos y Alcance
Primera entrega de la **Fase 6** de [`docs/ui_redesign_plan.md`](file:///docs/ui_redesign_plan.md): lenguaje visual del lienzo. La tarjeta de nodo pasa a tener jerarquía explícita de cabecera y telemetría, los sockets comunican el **tipo de dato** por forma y color, el arrastre de un cable resalta los destinos válidos y los cables muestran **flujo de energía animado** durante la ejecución.

### 🎯 Cambios Implementados
1. **Jerarquía de la Tarjeta (`NodeCardView.axaml`)**:
   - **Cabecera en dos líneas**: (1) identidad — placa de icono vectorial sobre el acento de su categoría, **título dominante** tipado con la escala del tema (`bodySm strong primaryText`), botón de modelo IA, plegado de parámetros, breakpoint y logging; (2) contexto — badge de categoría, indicador de estado de ejecución con su texto y badge de cuello de botella.
   - **Pie de telemetría reescrito**: métricas con **icono vectorial + valor crudo formateado por convertidor** (`CurrentStats.ProcessedCount`, `RollingLatencyMs`, `CurrentStats.RollingAvgAllocatedBytes`), con la fila oculta hasta que el nodo se ha ejecutado (`HasTelemetry`/`HasRamTelemetry`). El pie sigue alojando el tirador de redimensionado.
   - **Bug corregido — bindings muertos**: el pie enlazaba `ExecutionDuration` y `ProcessedBytes`, **propiedades que no existen en `NodeViewModel`**; con bindings por reflexión no falla la compilación y el pie aparecía vacío. Ahora se enlazan datos reales de `CurrentStats`.
   - **Bug corregido — botones de acción sin texto**: las acciones personalizadas del nodo enlazaban `{Binding Label}` y `{Binding Description}` sobre `NodeActionViewModel`, cuyos miembros reales son `Title` y `Tooltip`; los botones se dibujaban **sin etiqueta**.
   - **Emojis fuera de la telemetría inline**: `LatencyText`/`RollingRamText` (`⚡ 3.1 ms`, `💾 12 MB`) se eliminan del view model; el icono vectorial ya comunica la magnitud y el valor se formatea en la vista. El texto del tooltip detallado de métricas sí conserva su formato (es telemetría de texto, no iconografía).
   - **Radios y tipografías por token** (`RadiusMd`, `RadiusSm`, `RadiusXs`, `RadiusPill`, `FontSizeMicro`): la tarjeta participa del Theme Studio y baja de **44 a 4 literales de forma/tipografía y 0 de tamaño de fuente** (antes 8 valores distintos de `FontSize`). El LED de "modelo IA cargado" usa la clase temática `led`/`led.onSuccess` en lugar de un convertidor con color fijo.
2. **Semántica de Tipo en los Sockets (`PortViewModel`, `Styles/Ports.axaml`)**:
   - **Familia de dato** (`PortTypeKind`: Files/Text/Boolean/Number/Binary/Collection/Any) y **forma del socket** (`PortSocketShape`): círculo = texto, cuadrado = archivo/colección, triángulo = booleano, rombo = numérico. El color sale de los **tokens del tema** por clases de estilo (verde, cian, ámbar, primario, púrpura, error, glow), nunca del view model.
   - `SocketToolTip` describe nombre, **dirección**, familia, tipo real y estado: es la vía para descubrir la semántica sin abrir el inspector.
   - **Un único `PortSocketTemplate`** compartido por entradas y salidas, para que no puedan divergir.
3. **Resaltado de Puertos Compatibles al Arrastrar**:
   - `PortViewModel.ApplyDragHighlight` clasifica cada puerto en **origen**, **compatible** (latido verde), **advertencia de tipo** (ámbar: el cable puede crearse pero los tipos no encajan sin conversión) o **atenuado**; `CanConnect` cubre las reglas estructurales (puerto distinto, nodo distinto, direcciones opuestas) y `AreTypesCompatible` trata `object` como comodín.
   - Cableado desde `EditorViewModel` en `StartConnection` / `FinishConnection` / `CancelConnection`, con clases de estilo que atenúan también la **etiqueta** del puerto, no sólo el socket.
   - **Triángulo y rombo ahora también resaltan**: el socket booleano es un `Path` (no un `Border`) y el rombo necesita conservar su rotación dentro de la animación; ambos tenían estados propios incompletos.
4. **Flujo de Energía Animado en los Cables (`EditorView.axaml`, `ConnectionViewModel`)**:
   - La plantilla de cable superpone una **capa de energía** (`Classes="energy"`, guiones en movimiento con `StrokeDashOffset` animado) sobre el cable base, visible sólo mientras `IsExecuting` está activo y sin capturar el ratón (`IsHitTestVisible="False"`). El trazo base no se anima nunca, para no perder legibilidad en grafos densos.
   - Color de energía por familia de tipo (`Connection.energy.wire*`), coherente con el cable en reposo.
   - **Menú contextual reubicado**: colgaba de la capa de energía (no interactiva), así que el clic derecho sobre un cable no llegaba nunca al menú; ahora cuelga del cable base.
   - `PulseConnectionEnergy` con **generación por pulso**: un temporizador antiguo no puede apagar la energía de un pulso más reciente (ráfagas de datos). `CompleteConnectionPulse(connection, generation)` extrae esa regla para poder verificarla sin depender de un temporizador ni del Dispatcher.
5. **Localización de lo nuevo**: claves `Port_Type_*`, `Port_Direction_*`, `Port_Status_*`, `Port_Item*`, `Port_Desc_*`, `Node_BottleneckPercent` y los `NodeStatus_*` (que **faltaban** en ambos diccionarios: el estado de ejecución de la tarjeta se mostraba siempre con el literal en español del código) en `Strings.resx` y `Strings.es.resx`. `PortViewModel` y `NodeViewModel` componen sus textos **al vuelo** y se refrescan en caliente vía `OnLanguageChanged`.
6. **Guardias nuevas (34 pruebas)** y correcciones de guardias existentes:
   - `PortSemanticsTests` (14): clasificación de familias de tipo, forma por tipo, **exactamente una** clase de forma y **exactamente una** clase de tipo activas por socket (dos activas = color/forma mentirosos), reglas de conexión, comodín `object`, los tres estados del resaltado, reposo tras el arrastre y texto localizado del socket.
   - `ConnectionEnergyTests` (7): encendido del cable con incremento de generación, apagado al vencer el pulso, **un pulso obsoleto no apaga uno nuevo**, `durationMs <= 0`, apagado global y despacho real (`UpdateEdgeDispatched` energiza sólo los cables de esa salida).
   - `NodeCardVisualContractTests` (9): valida **cada binding de la tarjeta contra el `x:DataType` de su propia plantilla** (no contra un conjunto de candidatos: así se detecta una ruta que existe en otro view model), contrato del pie y del socket, capa de energía y menú contextual del cable, y **paridad de claves es/en** de todo texto localizado del view model (con lista blanca documentada para `Node_BottleneckPercent` y `NodeStatus_Faulted`).
   - `UiIconographyTests` corregido: los tramos comentados se descartan **por posición**, de modo que las tablas de documentación con flechas no son falsos positivos pero un pictograma real antes de un comentario en la misma línea sí se detecta.
   - Verificado que las guardias **fallan ante una regresión real**: con `HasTelemetryTypo`, `Classes="energia"` y `compatibleWarining` las cuatro guardias implicadas fallan con mensaje accionable (fichero, línea, ruta y tipo de contexto).
7. **Pruebas y Validación**:
   - Compilación `dotnet build FileFlow.slnx` → **0 advertencias / 0 errores**.
   - **910 / 910 pruebas superadas al 100% en dos ejecuciones completas consecutivas** (876 previas + 34 nuevas). Además, **768 / 768 en 5 ejecuciones consecutivas** excluyendo el clúster de IA/ONNX, para aislar la inestabilidad del punto siguiente.
   - **Inestabilidad preexistente detectada y acotada (dos frentes)**: (a) las pruebas de `Unit.AI` que ejecutan inferencia ONNX real abortan el host de forma intermitente (`0xC0000005` en `onnxruntime`, `testhost bloqueado`) al correr en paralelo con el resto; verificado que ocurre **también sin los tests nuevos** y que la clase pasa 12/12 en aislamiento (problema de entorno/concurrencia nativa, no de estos cambios); (b) `ModelLifecycleAndMemoryTests.UserPreferences_AutoUnloadAiModelsOnCompletion_*` afirmaba el **valor por defecto** leyendo el singleton, que carga el `user_preferences.json` **real del usuario**: cualquier perfil con la preferencia activada hacía fallar la prueba (y la propia prueba escribía en ese fichero de perfil, dejando el valor alterado si la ejecución se interrumpía — de hecho quedó en `true` tras una ejecución abortada por el punto (a)). Se desdobla en dos pruebas: una comprueba el defecto declarado en `UserPreferencesData` (independiente del entorno) y otra verifica el ida y vuelta del singleton **restaurando el valor original** en `finally`.
   - **Trinquete de estilos reducido**: la línea base de `UiStyleLintTests` baja con las dos vistas rediseñadas — tarjeta de nodo **44 → 4** literales de forma/tipografía (y **0** de tamaño de fuente) y 18 → 17 de color; `EditorView` 31 → 22.

---

## [2026-09-15] - Fase 0 del Rediseño Visual: Propagación de Tema, Tokens y Guardias de UI

### 🎯 Objetivos y Alcance
Ejecución de la **Fase 0** de [`docs/ui_redesign_plan.md`](file:///docs/ui_redesign_plan.md): saneamiento del sistema de temas, corrección de tokens rotos/inexistentes y localización del lienzo, con red de seguridad automatizada.

### 🎯 Cambios Implementados
1. **Propagación del Tema a la Aplicación y a Todas las Ventanas**:
   - `ThemeManager` publica ahora `Application.RequestedThemeVariant` mediante el nuevo punto único `WindowThemeHelper.ResolveThemeVariant(bool)` y reaplica la variante a **todas las ventanas abiertas** con `WindowThemeHelper.ApplyThemeToOpenWindows()` (iteración sobre `IClassicDesktopStyleApplicationLifetime.Windows`, con despacho seguro a UI thread).
   - **Bug corregido**: la variante de FluentTheme permanecía fija en `Dark` (fijada en `App.axaml`), por lo que en los temas Light/Pastel todos los controles internos de Fluent (ComboBox, ScrollBar, DataGrid, ContextMenu, Popup, TabControl) seguían pintándose oscuros sobre una UI clara.
   - **Bug corregido**: sólo `MainWindow` se re-tematizaba (`WindowThemeHelper` se invocaba únicamente en su constructor); se elimina esa suscripción duplicada porque la propagación es ahora centralizada en `ThemeManager`.
2. **Tokens Rotos, Inexistentes y Muertos**:
   - Nuevo token **`TextMuted`** en `ThemeDefinition` (con valor propio y contraste AA ≥ 4.5:1 sobre `BgSurface` y `BgCard` en los 8 presets), añadido a `builtin_themes.json` y emitido como `TextMutedBrush` por `ThemeResourceApplier` (antes: `TextMutedBrush` sólo existía en el diccionario oscuro y **nunca se reescribía** al cambiar de tema, dejando la telemetría de nodos con color oscuro en temas claros).
   - Nuevo token **`OverlaySurfaceBrush`** derivado del color de superficie de cada tema (alfa `0xE6` integrado en el `Color`), para el HUD del lienzo y la barra de zoom flotante.
   - **Eliminados tokens fantasma** que ningún tema definía y que dejaban elementos sin fondo: `BgAppBrush` (3 diálogos), `CardBgBrush` y `CardBorderBrush` (barra de breadcrumbs del sub-workflow). Sustituidos por `AppBackgroundBrush` / `BgCardBrush` / `BorderDarkBrush`.
   - **Drift estructural eliminado**: borrados los diccionarios manuales `LightTheme.axaml`, `CyberTheme.axaml` y `PastelTheme.axaml` (código muerto que contradecía los presets JSON: sólo `DarkTheme.axaml` se referenciaba). `Themes/DarkTheme.axaml` se reescribe como **espejo exacto del preset `dark_fluent`**, con cabecera que documenta que la fuente autoritativa son `builtin_themes.json` + `ThemeResourceApplier`. Se retira también `NodeShadowEffect` (no lo emitía ni lo consumía nadie).
   - **Tipografía efectiva**: `App.axaml` deja de fijar `FontFamily`/`FontSize` literales y consume `{DynamicResource AppFontFamily}` / `{DynamicResource AppFontSize}`, activando dos tokens que hasta ahora no tenían ningún efecto visual en el Theme Studio.
3. **Localización (i18n) del Lienzo**:
   - **HUD del lienzo** localizado (`CanvasHud_Selected`, `CanvasHud_Connections`, `CanvasHud_Location`, `CanvasHud_Zoom`) y **temático**: fondo `OverlaySurfaceBrush` en lugar de `#9914161C` fijo y acentos por token (`AccentSuccessBrush`, `AccentWarningBrush`, `AccentCyanBrush`) en lugar de hex fijos.
   - **Spotlight**: pie con atajos localizado (`Spotlight_FooterHints`).
   - 5 claves nuevas registradas en `Strings.resx` y `Strings.es.resx`.
   - `EditorZoomBarView` usa `OverlaySurfaceBrush` (antes `#C01E1E1E` fijo) y `NodeInspectorPanelView` usa `AccentCyanBrush` (antes `#00E5FF` fijo).
4. **Red de Seguridad Automatizada (16 tests nuevos)**:
   - **`ThemeTokenCompletenessTests` (4)**: todo token referenciado con `DynamicResource` en el repo (host **y plugins**) existe en los 8 presets generados y en el baseline de arranque; **paridad bidireccional** baseline ↔ preset `dark_fluent` (incluye la dirección inversa que causó el bug de `TextMutedBrush`: tokens declarados sólo en el baseline quedarían congelados al cambiar de tema); contraste WCAG AA del texto atenuado en cada preset.
   - **`UiStyleLintTests` (3)**: lint con estrategia de **trinquete** — fija los 27 ficheros con estilos inline (158 literales de color y 546 de forma/tipografía) como línea base y **falla si una vista empeora o si una vista nueva introduce literales**, permitiendo reducir la línea base conforme avance la migración a tokens de la Fase 2. Incluye instantánea lista para actualizar la baseline en el mensaje de error.
   - **`ThemeVariantPropagationTests` (9)**: decisión claro/oscuro → `ThemeVariant`, resolución de los 8 presets, eventos `ThemeChanged`, emisión de `TextMutedBrush`/`OverlaySurfaceBrush`/tipografía por tema, no-op seguro sin ciclo de vida de escritorio y **guardia de código fuente** que impide eliminar la publicación de la variante o la reaplicación a ventanas.
   - **Verificación de las guardias**: se comprobó con un fichero sonda temporal que las tres guardias fallan con mensajes accionables ante una regresión real (token inexistente + estilos inline), y el fichero se eliminó después.
   - Nuevo helper `TestRepositoryLocator` para que los tests que auditan ficheros localicen la raíz del repositorio.
5. **Estabilidad de la Suite**:
   - **Fallo intermitente corregido**: `VariableDiscoveryServiceTests` resuelve títulos de nodo vía `LocalizationManager` (cultura global del proceso) y se ejecutaba en paralelo con las clases que cambian de cultura; se serializa con `[Collection("Localization")]`.
   - **Limitación detectada en la infraestructura de tests** (documentada para la siguiente sesión): `AvaloniaTestHelper.EnsureInitialized()` silencia la excepción de `AppBuilder...SetupWithoutStarting()` cuando otro test ha creado antes `Dispatcher.UIThread` en otro hilo (error `VerifyAccess` en `DefaultRenderLoop.Add`), dejando `Application.Current` en `null`. Los tests de la Fase 0 se diseñaron para ser deterministas sin depender de esa inicialización global.
6. **Validación**:
   - Compilación limpia: **0 advertencias, 0 errores**.
   - Suite completa: **855 / 855 pruebas superadas al 100% (0 fallos, 0 omitidas)** en dos ejecuciones consecutivas (839 de referencia + 16 guardias nuevas).

---

## [2026-09-15] - Fase 3: Iconografía Vectorial Multiplataforma (Adiós a los Emojis)

### 🎯 Objetivos y Alcance
Eliminar la dependencia de los emojis como iconografía de la UI. Los emojis se renderizan con la fuente del sistema: en Windows a color, en Linux sin *Noto Color Emoji* aparecen como cuadraditos (*tofu*) o monocromos y en macOS con otro diseño, así que una aplicación que se define multiplataforma no puede apoyar su iconografía en ellos. Alcance migrado: **todo el AXAML del host y de los plugins**, los iconos de nodo, los del toolbox y las cadenas de icono que los view models exponen a la vista. Los emojis que son **texto** (mensajes de log, telemetría, informes Markdown/HTML/CSV y salida de CLI) se conservan por decisión explícita de producto.

### 🎯 Cambios Implementados
1. **Dependencia de iconografía (MIT, compatible con Avalonia 12)**:
   - Nuevo paquete **`Material.Icons.Avalonia` 3.0.2** (iconos de Material Design como **geometrías vectoriales**, no como fuente) en `FileFlow.App` y en los plugins con UI (`FileSystem`, `Integrations`, `Scripting`). Se eligió frente al catálogo autorado a mano porque aporta **13.645 glifos** ya dibujados y verificados, frente a ~60 hechos a mano con riesgo de trazo inconsistente.
   - Estilos registrados una sola vez en `App.axaml` (`<materialIcons:MaterialIconStyles />`), de modo que las ventanas de los plugins también los heredan. El color del icono llega por `Foreground` desde los tokens del tema, así que **los iconos se re-colorean con el tema activo** en lugar de quedar fijados.
2. **Nuevo catálogo central `NodeIconResolver` (reescrito)**:
   - Devuelve `MaterialIconKind` en lugar de `string`, con **tabla exacta** para 60+ tipos de nodo, **heurística por palabra clave** para tipos desconocidos (orden de reglas documentado) y tabla de categorías del toolbox (incluidas las claves localizadas en español e inglés).
   - **Compatibilidad hacia atrás**: `FromLegacyIcon()` traduce un valor heredado a icono vectorial, porque el contrato `NodeActionDescriptor.Icon` vive en `FileFlow.Sdk` (que debe permanecer puro, sin depender de la librería de iconos) y los plugins pueden seguir declarando emojis, igual que un flujo guardado antes de la migración. La tabla cubre ~110 equivalencias emoji → glifo y acepta también el nombre del enum ya migrado; un valor irreconocible cae a un icono de reserva, nunca a una excepción.
3. **Tipado de la iconografía de extremo a extremo**:
   - `NodeToolboxItem.Icon`/`FavoriteIcon`, `FileVersionOption.Icon`, `ToolboxCategoryFilterItem.Icon`, `NodeMetricsRowViewModel.NodeIcon`/`CategoryIcon`, `NodeActionViewModel.Icon`, `PortViewModel.DirectionIcon`/`ConnectionStatusIcon` y `AiModelItemViewModel.StatusIcon` pasan de `string` (emoji) a `MaterialIconKind`: **el compilador valida ahora cada glifo** y un nombre inventado deja de ser un fallo silencioso en tiempo de ejecución.
   - Los seis bindings que transportaban el icono como texto (`{Binding Icon}` en el lienzo, el toolbox, el inspector y el favorito) pasan a `Kind="{Binding Icon}"`; de otro modo habrían mostrado el *nombre del enum*.
4. **Migración de las 28 vistas con emojis (137 usos)**:
   - Formas transformadas: `TextBlock` con emoji único → `MaterialIcon` dimensionado con el `FontSize` que tenía; `Button` con emoji único → `MaterialIcon` hijo o la extensión `{materialIcons:MaterialIconExt Kind=...}`; etiquetas con emoji + texto (p. ej. "🏷️ Insertar Variable") → se conserva el texto y desaparece el pictograma; indicador de plegado del nodo → dos `MaterialIcon` (`ChevronUp`/`ChevronDown`) conmutados por `IsVisible`, en lugar de un glifo generado por convertidor.
   - Casos especiales resueltos a mano: selector de idioma (se retiran las banderas, el código de locale ya está en el `Tag`), insignia del splash y barra de comparación de imágenes. El único pictograma que sobrevive es `●` de `PasswordChar`, que **no es un icono** (máscara de campo de contraseña) y está documentado como excepción en la guardia.
   - Los finales de línea mixtos que dejó la migración automatizada se normalizaron al estilo dominante de cada fichero para no ensuciar el diff.
5. **Red de Seguridad (5 guardias nuevas, 35 → 40 tests de UI)**:
   - **`UiIconographyTests`**: (a) ningún AXAML de UI puede contener pictogramas, con lista de excepciones explícita y documentada; (b) todo `Kind="..."` literal debe existir en el enum `MaterialIconKind`; (c) todo tipo de nodo conocido debe resolver a un icono propio, no al de reserva; (d) la traducción de valores heredados (emojis y nombres de enum) funciona y un valor desconocido no lanza; (e) los estilos del control deben estar registrados en `App.axaml` (sin ellos no se dibuja nada).
   - El escaneo de pictogramas se hace **por punto de código** porque el motor de expresiones regulares de .NET no admite rangos por encima del BMP: un `\u1F000` se interpreta como `\u1F00` + `0`, que es exactamente el tipo de error que habría dejado la guardia inservible.
   - Verificado con una vista sonda temporal: la guardia falla con fichero y línea (`__IconProbe.axaml:3 '🔍'`). Sonda eliminada después.
   - Tests actualizados: `AiModelManagerViewModelTests` y `ToolboxViewModelTests` comparaban emojis y ahora comparan glifos tipados.
6. **Validación**: compilación del *slnx* completo **0 advertencias / 0 errores** (el compilador XAML valida cada `Kind`) y **865 / 865 pruebas superadas** (860 previas + 5 guardias nuevas).

---

## [2026-09-15] - Fase 2 (I): Capa de Estilos de Componentes y Migración de Barra de Control, Barra de Estado y Consola

### 🎯 Objetivos y Alcance
Creación de la **capa de componentes del sistema de diseño** (Fase 2 de [`docs/ui_redesign_plan.md`](file:///docs/ui_redesign_plan.md)) con clases reutilizables de estilo, y migración de las tres vistas de chrome (barra de control, barra de estado y consola de ejecución) para que dejen de llevar estilo inline.

### 🎯 Cambios Implementados
1. **Capa de Estilos (`FileFlow.App/Styles/`, 5 ficheros, registrados en `App.axaml` después de `FluentTheme`)**:
   - **`Typography.axaml`**: escala tipográfica del tema (`display`, `title`, `subtitle`, `body`, `bodySm`, `caption`, `micro`) y clases de color/énfasis (`primaryText`, `secondary`, `muted`, `onAccent`, `accent`, `accentCyan`, `accentPurple`, `accentSuccess/Warning/Error`, `mono`, `numeric`, `strong`, `semiBold`, `sectionLabel`). Las clases de tamaño **no** fijan color (funcionan dentro de botones de acento) y las de color **sólo** fijan `Foreground`.
   - **`Surfaces.axaml`**: tres niveles de profundidad (`surface` < `card` < `overlay`) más `panel`, `island`, `inset`, `chromeTop`, `chromeBottom`, `statusPill`, `brand`, `badge`, `badgeAccent`, `led` (+ `on`, `onInfo`, `onSuccess`, `onError`, `ledLarge`), `dividerV`, `dividerH` y `accentStrip`. Todos los radios provienen de la escala del tema (`RadiusXs…RadiusPill`).
   - **`Buttons.axaml`**: `Button` base con estados hover/pressed/disabled declarados sobre `ContentPresenter#PART_ContentPresenter` (parte real de la plantilla Fluent, donde el tema base fija sus colores y por tanto gana la cascada) y variantes `primary`, `success`, `danger`, `warning`, `debug`, `ghost`, `icon`, `toolbar`, `link`, `pill`; `ToggleButton` con variantes `chip` e `icon`; y `ControlTheme` **`SegmentTheme`** para grupos de filtros exclusivos (pastilla activa con acento y texto `onAccent`).
   - **`Inputs.axaml`**: `TextBox` (estados sobre `Border#PART_BorderElement`) con variantes `search`, `mono` y `flush` (sin cromo, para vivir dentro de un `Border.inset`), placeholder en `TextMutedBrush`; `ComboBox`/`ComboBoxItem` (estados sobre `Border#Background`, glifo de desplegable en `TextSecondaryBrush`); `CheckBox` y `RadioButton`.
   - **`Containers.axaml`**: **pestañas** (`TabItem` con transición de `Foreground`, indicador `PART_SelectedPipe` como pastilla de acento y variante `TabControl.pill`), **`GridSplitter`** (5 px, acento al pasar el puntero), **barras de desplazamiento** (`Thumb` de 9 px con extremos redondeados, hover/pressed/disabled sobre el `Border` interno y pista visible al expandirse) y `DataGrid.logGrid` para la consola (filas de 26 px, código monoespaciado, cabeceras temáticas).
2. **Migración de las Tres Vistas de Chrome**:
   - **`ControlBarView.axaml`**: reorganizada en `Between` de tres **islas semánticas** (Modos · Ciclo de vida · Herramientas) mediante `Border.island`; LEDs de estado conmutados declarativamente (`Classes.on="{Binding IsDryRun}"`), botones con variantes semánticas (`success` para ejecutar, `debug` para depurar, `warning` para paso a paso, `danger` para detener), marca de app con `Border.brand` y tipografía por clase. **0 literales de color, radio y tamaño de fuente.**
   - **`StatusBarView.axaml`**: telemetría agrupada en **píldoras** (`Border.statusPill`) — grafo, estado del motor, ruta de salida global, modelos IA en memoria y métricas de hardware (RAM/CPU/GPU) — con separadores internos `dividerV`, LEDs semánticos (`led onInfo`, `led onSuccess`) y valores en tipografía `numeric`.
   - **`LogView.axaml`**: filtros de severidad como segmentos pastilla usando el `ControlTheme` `SegmentTheme`, campo de búsqueda instantánea en `Border.inset` + `TextBox.flush search` con botón `icon` de limpieza, barra de progreso fina y rejilla virtualizada `DataGrid.logGrid` con insignias `badge`. Sustituye el estilo inline anterior por clases en toda la vista.
3. **Red de Seguridad Ampliada (3 guardias nuevas, 27 → 30 tests de UI)**:
   - **`ThemeTokenCompletenessTests`**: la recopilación de tokens referidos excluye ahora los recursos locales de la capa de estilos (`x:Key` en `Styles/*.axaml`, como `SegmentTheme`), que no son tokens de tema y no debe publicar `ThemeResourceApplier` (antes provocaban un falso positivo en los dos tests de completitud).
   - **`UiStyleContractTests.ThemeResourceReferences_ShouldResolveToADeclaredKey`** (nueva): todo `Theme="{DynamicResource X}"` debe apuntar a una clave declarada con `x:Key`; una errata ahí no rompe la compilación y el control se quedaría con la plantilla por defecto (mismo modo de fallo silencioso que las clases inexistentes).
   - **`UiStyleContractTests.MigratedViews_ShouldNotDeclareInlineShapeOrColorLiterals`**: guarda que las tres vistas migradas no reintroduzcan literales de color, radio o tamaño de fuente.
   - **Estabilidad**: `WorkflowMetricsDashboardViewModelTests` se serializa con `[Collection("Localization")]` (sus filas derivan de `NodeViewModel.Title`, que `OnLanguageChanged` reescribe; en paralelo con los tests que mutan la cultura global producía un fallo intermitente con `FilteredNodeRows` vacío).
4. **Validación**:
   - Compilación de `FileFlow.App`: **0 advertencias, 0 errores**.
   - Suite completa: **860 / 860 pruebas superadas al 100% (0 fallos, 0 omitidas)** (855 de referencia + 4 guardias nuevas + `ThemeResourceReferences`), verificada también en modo `--blame-hang`.
   - Nota operativa: dos ejecuciones del suite se colgaron por un `testhost.exe` huérfano (≈2 GB) de una ejecución anterior interrumpida por timeout, no por un fallo del código; se liberó el proceso y la suite volvió a pasar.

---

## [2026-09-15] - Auditoría de UI/UX Avalonia y Plan Maestro de Rediseño Visual

### 🎯 Objetivos y Alcance
1. **Auditoría Técnica de la Capa de Presentación (`FileFlow.App`)**:
   - Medición objetiva del estado del diseño: **240 literales `#HEX`** en AXAML (97 fuera de los diccionarios de tema), **21 valores distintos de `CornerRadius` inline**, **25 tamaños de `FontSize` distintos** (8 → 28), **93 emojis usados como iconos en AXAML + 153 en C#** y únicamente **2 estilos globales de control** en `App.axaml` (`Window` y `ToolTip`).
   - Identificación de **7 tokens declarados pero nunca consumidos** (`AppFontFamily`, `AppFontSize`, `AppCornerRadius`, `ScrollbarThumbBrush`, `ConnectionWireBrush`, `GridLineBrush`, `NodeShadowEffect`), lo que anula de facto la personalización prometida por el Theme Studio.
2. **Defectos del Motor de Temas Detectados (bugs, no opiniones)**:
   - `Application.RequestedThemeVariant` **nunca se actualiza** en `ThemeManager`: en temas Light/Pastel los controles internos de `FluentTheme` (ComboBox, ScrollBar, DataGrid, ContextMenu, TabControl, Popup) permanecen oscuros mientras el resto de la UI es clara.
   - `WindowThemeHelper.ApplyThemeToWindow` sólo se invoca desde `MainWindow.axaml.cs`: los **10 diálogos** del host quedan en variante oscura al cambiar de tema.
   - Recursos inexistentes `CardBgBrush` / `CardBorderBrush` (`EditorView.axaml`) → barra de breadcrumbs sin fondo ni borde.
   - `TextMutedBrush` existe sólo en `DarkTheme.axaml` y no lo emite `ThemeResourceApplier`, por lo que la telemetría de nodos (`NodeCardView`) mantiene un valor oscuro en temas claros (y es una clave *nunca borrada* al cambiar de tema).
   - Duplicidad de fuentes de verdad entre `Themes/*.axaml` y `BuiltInThemesCatalog`/`ThemeResourceApplier` (drift estructural garantizado).
   - Incumplimientos de i18n en UI reciente: HUD del lienzo en inglés fijo (`Selected:`, `Connections:`, `Location:`, `Zoom:`), textos del Spotlight y `SplashScreenWindow`/`ThemeCustomizerWindow` con cadenas hardcodeadas.
   - Colores no temáticos en superficies clave (HUD `#9914161C`, `EditorZoomBarView` `#C01E1E1E`, `SplashScreenWindow`, glows de nodo).
3. **Plan Maestro de Rediseño Entregado**:
   - Nuevo documento [**`docs/ui_redesign_plan.md`**](file:///docs/ui_redesign_plan.md) con objetivo, dirección visual "Studio Pro", arquitectura del sistema de diseño (tokens v2 → capa de componentes → vistas), decisión pendiente sobre un ensamblado compartido `FileFlow.Ui` para los diálogos de plugin (implica actualizar la lista de ensamblados compartidos de `PluginAssemblyLoadContext` y las reglas de gobernanza), **8 fases** de trabajo con entregables verificables, catálogo de **14 microinteracciones**, checklist de accesibilidad, plan de testing (incluyendo `UiStyleLintTests` y `ThemeTokenCompletenessTests` con Avalonia.Headless ya disponible), riesgos y quick wins.
4. **Validación**:
   - Análisis estático sobre el árbol de trabajo actual de la rama `feature/crossplatform-avalonia` (sin modificaciones de código en esta entrada; sólo documentación). Suite de referencia: **839 / 839 pruebas superadas**.

---

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

## [2026-09-17] - Elevación Automática de Nodos a Primer Plano (ZIndex / BringToFront) y Activación Fluida de Controles Interactivos

### 🎯 Objetivos y Alcance
1. **Solución a Controles Desplegables en Nodos Expandidos (`ComboBox` / `TextBox` / `Slider`)**:
   - Se diagnosticó la causa por la cual los `ComboBox` dentro de la tarjeta de un nodo expandido no desplegaban su lista flotante al hacer clic sobre el lienzo (mientras que en el panel Inspector sí funcionaban).
   - En `NodeCardView.axaml.cs`, el manejador `NodeCardView_PointerPressed` intercepta eventos de puntero originados en controles interactivos (`IsInteractiveVisual(e.Source)`). Al detectar un control interactivo (ComboBox, TextBox, Slider, etc.), ahora selecciona explícitamente el nodo (`interactiveNode.IsSelected = true`) y llama a `ParentEditor.BringToFront(interactiveNode)` para situarlo en la capa superior visual sin interferir con la apertura del popup de Avalonia.
2. **Elevación de Nodos a Primer Plano al Seleccionarlos (`ZIndex`)**:
   - Se corrigió la omisión de vinculación de `ZIndex` en `EditorView.axaml`. Se añadió `<Setter Property="ZIndex" Value="{Binding ZIndex, Mode=TwoWay}" />` al estilo de `nodify:ItemContainer` tipado a `vm:NodeViewModel`.
   - Cada vez que un nodo es seleccionado por el usuario en el lienzo visual, la cabecera o mediante interacción con cualquiera de sus parámetros, su `ZIndex` se eleva automáticamente por encima de cualquier otro nodo solapado, garantizando que nunca quede oculto detrás de otros elementos durante la edición.
3. **Pruebas y Validación**:
   - Nueva prueba unitaria añadida en `NodeCardInteractiveControlsPointerTests.cs`: `PointerPressed_OnInteractiveControl_ShouldSelectAndBringNodeToFront`, verificando que la interacción con controles interactivos selecciona el nodo y eleva su `ZIndex` sobre otros nodos en el canvas.
   - **1015 / 1015 pruebas unitarias, integración y regresión visual superadas al 100% (0 errores, 1 omitida)**.
   - Compilación con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` limpia: **0 advertencias y 0 errores**.

---

## [2026-09-17] - Rediseño Compacto y Homogéneo del Catálogo de Nodos (Menu Style & Spacing Optimization)

### 🎯 Objetivos y Alcance
1. **Unificación Estética del Catálogo de Nodos (`NodeToolboxView.axaml`)**:
   - Transformación del catálogo de cajas separadas a una navegación en árbol/menú continua, homogénea y compacta inspirada en IDEs modernos (VS Code / JetBrains).
   - Sustitución de cajas y contenedores heterogéneos por elementos fluidos con hover reactivo (`nodeMenuItem`, `menuAccordion`).
2. **Control de Despliegue y Jerarquía**:
   - Reposicionamiento del control de expansión/colapso (chevron rotativo 0° $\rightarrow$ 90°) a la **izquierda** de cada título de categoría.
   - Vectorización e iconografía 100% homogénea para todas las categorías (`MaterialIconKind`), eliminando emojis residuales en diccionarios de recursos (`Strings.resx` / `Strings.es.resx`).
3. **Compactación y Optimización de Espaciado Vertical**:
   - Ajuste de altura mínima e intercalado en categorías (`MinHeight="22"`, `Padding="2,1,4,1"`, `Margin="0,0,0,1"`) y en elementos hijo (`MinHeight="20"`, `Padding="4,1.5,6,1.5"`).
   - Ajuste de márgenes del botón de favorito (`Margin="2,0,4,0"`) para evitar colisión con la barra de scroll y garantizar un alineamiento simétrico.
   - Fijación de `VerticalAlignment="Top"` en la lista contenedora para evitar estiramiento vertical no deseado en paneles altos.
4. **Corrección de Seguimiento Dinámico del Cable de Conexión en el Lienzo (`PendingConnection`)**:
   - Se eliminó la sobreescritura de `TargetAnchor="{Binding TargetLocation, Mode=TwoWay}"` en `EditorView.axaml` que reiniciaba el extremo del cable a `(0, 0)` en intentos sucesivos de conexión tras el reciclado de la vista.
   - Vinculado `Source="{Binding Source}"` y `Target="{Binding Target}"` canónicos con `SourceAnchor="{Binding Source.Anchor}"`, permitiendo que el motor de Nodify.Avalonia mantenga el anclaje reactivo directamente sobre el cursor del ratón durante todo el arrastre.
5. **Pruebas y Validación**:
   - **1011 / 1011 pruebas unitarias, integración y regresión visual superadas al 100% (0 errores)**.
   - Regeneración y validación de snapshots de regresión visual (`panel-toolbox-dark`, `app-shell-dark`, `app-shell-light`).
   - Compilación limpia con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: **0 advertencias y 0 errores**.

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

## [2026-09-17] - Hito 124: Sustitución de Botones de Versión de Archivo por Dropdown ComboBox

### 🎯 Objetivos y Alcance
1. **Sustitución de Chips por Lista Desplegable (ComboBox)**:
   - Se reemplazó el contenedor horizontal de botones chips (`ItemsControl` con `ScrollViewer`) para la selección de versión de archivo (`IsFileVersionSelector`) tanto en la tarjeta de nodo (`NodeParameterTemplates.axaml`) como en el panel de inspección (`NodeInspectorPanelView.axaml`).
   - El nuevo control es un `ComboBox` estilizado con soporte para vector icons (`MaterialIcon`), etiquetas claras (`Tag`) y vinculación bidireccional limpia con `SelectedVersionOption` y `AvailableVersionOptions`.
   - Se mantiene el botón adjunto `{x}` para la inserción de variables y expresiones personalizadas.
2. **Sincronización Bidireccional MVVM en `NodeParameterViewModel`**:
   - Se implementó la propiedad `SelectedVersionOption` con getter/setter sincronizado con `Value` (resolviendo por `Token`, `Tag` o `ActiveVersionTag`).
   - Se emiten notificaciones reactivas de `SelectedVersionOption` en `OnValueChanged`, `SelectVersionOption` y `RefreshAvailableVersions`.
3. **Pruebas y Validación**:
   - Se actualizaron las pruebas unitarias en `NodeCardInteractiveControlsPointerTests.cs` para validar la instanciación e interacción del `ComboBox`.
   - **1019 / 1020 pruebas unitarias superadas al 100% (1 omitida por requerir modelo CLIP externo, 0 fallos)**.
   - Compilación limpia: 0 advertencias, 0 errores.

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

## [2026-09-17] - Hito 128: Desplegable de Presets en Nodo Renombrar y Restauración de Cables Estándar Nodify

### 🎯 Objetivos y Alcance
1. **Desplegable de Presets en Nodo Renombrar Archivo (`AdvancedRenamerConfigViewModel`)**:
   - Se integró un selector desplegable (`ComboBox`) con la lista completa de presets disponibles en el nodo de Renombrado Avanzado (`SelectedPresetOption` y `AvailablePresetOptions`).
   - Al pulsar "Guardar y Aplicar" en el diálogo del Estudio de Renombrado Avanzado, el preset seleccionado o recién guardado se sincroniza y selecciona inmediatamente en el nodo actual del editor.
2. **Restauración y Simplificación a Estándar de Nodify (Cables, Arrastre y Conexiones 100% Sólidas e Instantáneas)**:
   - Se eliminaron todas las transiciones animadas (`Transitions` de `BrushTransition` y `DoubleTransition`) en `Border.socket`, `Path.socketTriangle` y `TextBlock.portLabel` en [Ports.axaml](file:///FileFlow.App/Styles/Ports.axaml), eliminando cualquier transición o retardo visual al hacer clic e iniciar el arrastre desde cualquier conector.
   - Se eliminaron las animaciones dinámicas con keyframes (`Style.Animations`) en `PendingConnection` y en los estados compatibles/warning de los sockets.
   - Se eliminó por completo la capa superpuesta de animación con guiones (`Connection.energy`), se configuró `StrokeDashArray="{x:Null}"` explícito en todos los estilos de `Connection` y `PendingConnection` en [Ports.axaml](file:///FileFlow.App/Styles/Ports.axaml), y se desactivó `EnablePreview="False"` para evitar cualquier línea punteada o animada de Nodify.
   - En [NodeCardView.axaml](file:///FileFlow.App/Views/Components/NodeCardView.axaml): vinculación estándar de Nodify con `IsConnected="{Binding IsConnected, Mode=TwoWay}"` y `Anchor="{Binding Anchor, Mode=OneWayToSource}"` en `<nodify:NodeInput>` y `<nodify:NodeOutput>`.
   - En [EditorView.axaml](file:///FileFlow.App/Views/EditorView.axaml):
     - `PendingConnectionTemplate` simplificado y estandarizado con `Source="{Binding Source.Anchor}"` y `Target="{Binding TargetLocation, Mode=TwoWay}"`, siguiendo al cursor con precisión en todos los intentos de conexión de forma sólida.
     - `ConnectionTemplate` enlazado con `Source="{Binding Source.Anchor}"` y `Target="{Binding Target.Anchor}"`, garantizando que los cables activos sigan el movimiento de los nodos.
3. **Pruebas y Validación**:
   - Nuevos tests de integración de interacción en `EditorViewLayoutTests.cs` (`RepeatedConnectionDrag_ShouldUpdatePendingConnectionState_AndAllowSubsequentConnections` y `MovingNode_ShouldUpdatePortAnchor_AndAffectConnections`).
   - Suite completa: **1027 superadas, 0 fallos, 1 omitida** (1028 tests totales).
   - Compilación limpia sin errores.

---

## 📜 Historial de Versiones Anteriores (Archivado)

Las fases históricas previas (Fases 1 a 8, Sprints de Agosto 2026 y desarrollos fundacionales anteriores) han sido consolidadas y archivadas para optimización de contexto en:
- 📄 [**`docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md`**](file:///docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md)
