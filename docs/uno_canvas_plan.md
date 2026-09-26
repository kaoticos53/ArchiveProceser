# Plan de la rebanada 3 — El lienzo del editor en el host Uno

> **Estado**: decisión tomada, **sin código tocado**. Este documento se escribe antes de empezar
> para que la decisión se tome una vez y a conciencia, no por omisión a mitad de la rebanada.
> **Origen**: notas del hito 211 («rebanada 3 natural: …decidir qué hace falta para que
> `MainWindow` pinte el editor de verdad»).

---

## 1. El encargo

«Decide cómo pintar el lienzo del editor en el host Uno —el control Nodify de Avalonia no existe
ahí— y deja un plan escrito antes de tocar código.»

El host Uno ya arranca con el núcleo portable vivo (rebanada 2): descubre los nodos de los plugins,
resuelve el `MainViewModel` completo y localiza su texto. Lo que no tiene es **el lienzo**: la vista
donde el usuario ve y edita el grafo. En Avalonia esa vista es `EditorView` + `NodeCardView` + los
estilos de puertos, montada sobre **Nodify.Avalonia**, un control de tercero que trae gratis el
área infinita, el pan/zoom, el arrastre de nodos, el cálculo de anclas de puertos y el enrutado de
cables. Ninguna de esas piezas existe en WinUI/Uno, y el censo (abajo) no encontró **ningún**
control equivalente para esa plataforma —sólo puertos de Nodify a WPF y Avalonia—.

---

## 2. Lo que se midió antes de decidir

### 2.1 La superficie del ViewModel que la vista consume

El contrato que el lienzo debe satisfacer ya existe y está probado: es la superficie pública de
`EditorViewModel` y sus hijos, en `FileFlow.App.Core`. Inventariada:

| Pieza | Superficie que consume la vista | Notas |
| :--- | :--- | :--- |
| Nodos | `Nodes` (`ObservableCollection<NodeViewModel>`): `Location` (`Sdk.Point`), `Width`, `ZIndex`, `IsSelected`, `IsEditingTitle`, `InputPorts`, `OutputPorts`, `Title`… | El `Location` habla en `Sdk.Point` (hito 211): **cada host proyecta**, no el núcleo. |
| Cables | `Connections`: `Source`/`Target` (`PortViewModel`), `Anchor` (`Sdk.Point`, **escrito por la vista** — `OneWayToSource`), tipos para las clases de estilo (`IsFilesType`…) | **El cálculo del ancla lo hacía Nodify.** En un lienzo propio lo calculamos nosotros (fase 3.0). |
| Cable pendiente | `PendingConnection`: `Source`, `TargetLocation`, `IsVisible`; comandos `StartConnectionCommand`, `FinishConnectionCommand`, `CancelConnectionCommand`, `DisconnectConnectorCommand` | La lógica de validación ya está en el VM. |
| Viewport | `ViewportLocation`, `ViewportSize`, `ViewportZoom` (TwoWay), `FitToScreenCommand`, `ZoomIn/Out` | `EditorViewportCalculator` ya es portable y puro… **y no tiene una sola prueba** (medido: ningún test lo referencia). |
| Decoradores | `CanvasDecorators` (grupos + notas), `Annotations`, `Groups` | Los grupos se insertan al fondo (`Insert(0, …)`), las notas encima: el orden del z ya viene del VM. |
| Selección | `SelectedNode`, `UpdateSelectedCount()`, `BringToFront()` | Reaccionan a `IsSelected` de cada nodo. |
| Servicios del editor | `EditorView_KeyDown` en el host: Escape, Shift+A/Space (spotlight), Ctrl+Z/Y/C/V/X/D, Delete/Back, F2 | Los atajos son **código de vista** hoy; el plan los mantiene en cada host (son de teclado, que es del host). |

### 2.2 Lo que Nodify hacía gratis (y hay que reemplazar o escribir)

1. **Área infinita con pan y zoom** y `MinViewportZoom/MaxViewportZoom`.
2. **Posición de contenedores** por binding `Location` (TwoWay, arrastre incluido).
3. **Cálculo de anclas** de cada socket en espacio de grafo — el write-back que hoy llega a
   `PortViewModel.Anchor` por `Anchor="{Binding Anchor, Mode=OneWayToSource}"`.
4. **Enrutado de cables** (`Connection` con `Spacing=45`, `Direction=Forward`: el estilo en escalón).
5. **Cable pendiente** que sigue al cursor con snapping y `AllowOnlyConnectors`.
6. **Hit-testing de conexiones** para el menú contextual de borrar cable.
7. **Rubber-band** de selección sobre el fondo.

### 2.3 Los tokens y recursos que el lienzo consume

`Ports.axaml` + `EditorView.axaml` consumen ~20 tokens de tema (`BgEditorBrush`, `BgCardBrush`,
`AccentPrimaryBrush`, `ConnectionWireBrush`, `TextPrimaryBrush`, `Radius*`, `FontSize*`…) vía
`DynamicResource`. **WinUI/Uno no tiene `DynamicResource`**: tiene `ThemeResource`/`CustomResource`
(resueltos una vez) — la república en caliente de tokens (lo que `AvaloniaThemeHost` hace en el host
Avalonia) necesita su equivalente Uno (fase 3.5).

También consume **Material.Icons.Avalonia** (librería de control de Avalonia: no reutilizable) y
**Nodify.Avalonia** (idem).

### 2.4 Prior art: lo que existe y lo que no

- Nodify (WPF, MIT, Miroiu) y Nodify.Avalonia (MIT, wieslawsoltes): **no hay puerto a WinUI/Uno**.
- La búsqueda de controles «node editor» para WinUI 3/Uno no devuelve ninguna librería mantenida:
  los nodos de chat/grafos de la plataforma se hacen a mano (Canvas + transforms) o con Blazor/web.
- Conclusión medida: **no hay atajo de terceros**. La decisión es entre escribirlo o portar un
  tercero, y portarlo es escribirlo con extra pasos.

---

## 3. Las opciones, con su coste escrito

### Opción A — Portar Nodify.Avalonia a Uno/WinUI

Fork del fork, reescribiendo `AvaloniaProperty` → `DependencyProperty`, el sistema de estilos de
Avalonia → estilos WinUI (diametralmente distinto en selectores y herencia), la capa visual y el
input de Avalonia → manipulación WinUI. Meses de trabajo acoplados a las **internas** de un tercero
que no controlamos, para mantener un fork que nadie más usa. **Descartada**: el coste es el de la
opción B multiplicado por el acoplamiento.

### Opción B — Lienzo propio en el host Uno sobre el contrato portable ✅

Un `EditorCanvasControl` (nombre de trabajo) en `FileFlow.App.Uno`, hecho con controles nativos de
WinUI/Uno: `Canvas` + transformaciones de pan/zoom + `ItemsControl` para nodos y cables +
`ManipulationDelta`/rueda para navegar. La lógica **no se toca**: vive ya en `FileFlow.App.Core`
(validación de conexiones, undo/redo, portapapeles, selección, avisos de cables perdidos); el host
sólo hace **render + input**, que es exactamente lo que un host debe hacer.

- A favor: sin dependencia nueva, acceso nativo a texto (renombrado), accesibilidad, menús
  (`MenuFlyout`), y el host ya compila con el toolkit de Uno.
- En contra: las siete piezas del §2.2 son nuestras; el rendimiento con grafos grandes es nuestro
  problema (mitigación: §6 fase 3.6).

### Opción C — Todo el lienzo dibujado con Skia (SkiaSharp/Win2D dentro del host Uno)

El host ya activa `SkiaRenderer`. Dibujar el lienzo entero en un `CanvasControl` de Skia da control
total y rendimiento predecible… y nos obliga a reimplementar **hit-testing, edición de texto,
accesibilidad, focus, IME del renombrado y los iconos Material** en una tercera pila de render
distinta de las dos que ya existen (Avalonia y WinUI). **Descartada como primera rebanada**; queda
anotada como **salida de emergencia de rendimiento** si la B no llega a 60 fps con el grafo de
referencia (§7).

### Decisión: **Opción B**, con la geometría compartida en Core

El lienzo es del host Uno; la matemática que el lienzo necesita (enrutado de cables, anclas,
encuadre) es **pura y por tanto portable y probable**: va a `FileFlow.App.Core` con sus pruebas, no
al host. El host Avalonia **no cambia**: sigue en Nodify, sin tocar una línea de su canvas — la
rebanada no arriesga el escritorio que funciona.

---

## 4. El contrato (lo único que ata las dos mitades)

Por ahora **no se extrae ninguna interfaz nueva**: el contrato ya existe y es la superficie pública
de `EditorViewModel` + hijos (§2.1). Extraer `IEditorCanvasView` hoy sería abstraer contra un solo
consumidor — especulación. La regla escrita:

- El lienzo Uno consume **sólo** la superficie que `EditorView` (Avalonia) consume hoy.
- Si en 3.2/3.3 aparece fricción (algo que la vista necesita y hoy se resuelve en el code-behind de
  Avalonia), **entonces** se extrae a Core el contrato con su guardia de literales, al estilo de
  `ThemeVariantPropagationTests`.
- La proyección de puntos se repite por host: `SdkPointProjection` existe en Avalonia; el host Uno
  tendrá su `UnoPointProjection` (`Sdk.Point` → `Windows.Foundation.Point`). **La lección del hito
  211 aplica por host**: cada binding de geometría entre VM (`Sdk.Point`) y control (punto del
  framework) pasa por su proyección explícita, o muere en silencio.

---

## 5. Fases (rebanadas dentro de la rebanada)

Cada fase termina en verde y con protocolo. Ninguna toca el host Avalonia.

### Fase 3.0 — Geometría pura en Core (sin UI, sólo matemática y pruebas) ✅ **HECHA (hito 216)**

- **`ConnectionGeometry`** (nuevo, en Core, [`Services/ConnectionGeometry.cs`](file:///FileFlow.App.Core/Services/ConnectionGeometry.cs)):
  dado `source`, `target`, `spacing` y dirección, devuelve las anclas de control de la curva.
  **Medición que corrigió el plan**: el estilo del lienzo NO es un escalón — el XAML usa la clase
  `Connection` de Nodify, que es una **Bézier cúbica** (fuente de Nodify leída: cuello =
  `max(min(100, alto), ancho/2)`, con techo `100 + √(ancho·25)`). La clase transcribe ese algoritmo
  y sus 13 pruebas clavan valores calculados a mano (horizontal, vertical, invertido, nodos pegados,
  nodos lejanos, interpolación, tangente e hit-testing por muestreo).
- **Pruebas de `EditorViewportCalculator`** (11 nuevas, antes sin ninguna): `CenterOn` (zoom,
  respaldo de tarjeta sin medir, zoom cero) y `CalculateFitToScreen` (vacío, mínimo, grafo ancho,
  grafo alto, promesa de visibilidad, coherencia con `CenterOn`). **Hallazgo fijado por la prueba**:
  el techo de zoom (1.8) es inalcanzable para el ajuste a pantalla — el alto de respaldo (220) fija
  el suelo del escalado (380/220 ≈ 1.727).
- **Decisión de anclas (escrita)**: en Uno, el lienzo calculará `PortViewModel.Anchor` **en espacio
  de grafo**, con la misma regla que Nodify usa en Avalonia: la posición del socket medida en la
  tarjeta, convertida a espacio de grafo con `ViewportLocation` y `ViewportZoom`, y escrita por
  OneWayToSource tras cada medición/movimiento. La precisión es la del double, sin redondeo (Nodify
  no redondea las anclas; los redondeos del calculador de encuadre son sólo de presentación). El
  write-back ya está demostrado vivo por `GeometryBindingProjectionTests` (hito 215).
- Criterio de salida: **cumplido** — 24 pruebas nuevas en verde, suite completa verde, cero líneas
  en `FileFlow.App.Uno` (la geometría es del núcleo).

### Fase 3.1 — Lienzo estático (el grafo se ve) — **CRITERIO COMPLETO (hito 226)**

> **Decisión de plataforma medida (hito 221)**: el motor XAML de WinUI/Uno **no evalúa `{Binding}` dentro de
> `Setter.Value`** — el enlace no falla: no hace nada (la posición sería 0,0 en silencio). El plan original
> de posicionar con Setter + binding es imposible en WinUI. En su lugar, el code-behind aplica la posición
> leyendo la posición **ya proyectada** del adaptador (`NodeCardViewModel.Position`, que pasa por
> `UnoPointConverter`), y la guardia del 217 fue ampliada para censar también el code-behind: delata tanto
> el posicionamiento con la Location cruda como el punto del framework construido a mano desde `.X/.Y`
> (el cruce debe pasar por `UnoPointProjection`). La lectura en espacio de grafo (`Sdk.Point`) para la
> matemática de cables sigue siendo legítima — el cruce explícito es lo censado.

- [x] `EditorCanvasControl`: pan (arrastrar fondo), zoom (rueda + botones, 0.2–2.5), grid de fondo.
- [x] Nodos posicionados por `Location` proyectada (`NodeCardViewModel.Position` vía `UnoPointConverter`);
  **tarjeta en modo lectura** (sin puertos vivos: sockets dibujados, sin interacción).
- [x] Cables estáticos con `ConnectionGeometry` (Bézier del núcleo, proyección explícita al dibujar).
- [x] Encuadre con el mismo `EditorViewportCalculator` del núcleo que usa el escritorio.
- [x] Guardia del 217 ampliada al code-behind (+3 auto-tests; mutación `proyeccion-uno-sin-guardia`
  actualizada y volviendo a morder).
- [x] `NodeCardView` visual completo (hito 224): la tarjeta de 559 líneas traducida a WinUI — glows de
  estado, barra de acento, iconos por path data del paquete `Material.Icons` (no hay font: el plan lo
  asumía y la medición lo corrigió), LEDs de breakpoint/logging, badge de categoría, LED de estado,
  cuello de botella, progreso, puertos dibujados con la matriz de sockets (forma/tipo/estado — los
  30 selectores del escritorio viven ahora en `PortPalette` del núcleo), panel de parámetros plegable
  y pie de telemetría. Sin interacción de puertos (fase 3.3).
- [x] Criterio de salida demostrado por mitad (hito 225): el host Uno arranca con DI completa, 70 nodos
  descubiertos y el ejemplo cargado, y el sondeo `FileFlow.App.Uno.exe --selfcheck` sale con código 0 en
  dos corridas consecutivas verificando el árbol visual real: 3/3 tarjetas materializadas, título con
  binding comprobado en el control (vía `x:Name`), posiciones proyectadas aplicadas a los contenedores
  (100/350/600, coincidentes con `NodeCardViewModel.Position`), 3 iconos con geometría resuelta
  (`PathGeometry` 20x16 / 18x18) y 2/2 cables Bézier en la capa.
- [x] **Tres defectos de producto cazados y corregidos por el sondeo en este tramo**:
  (1) el importador añade todos los nodos ANTES que las aristas, y el lienzo sólo redibujaba con
  `Nodes.CollectionChanged` — un flujo cargado de disco quedaba sin cables; ahora también escucha
  `Connections.CollectionChanged`; (2) el posicionamiento corría en `Rebuild()` sobre un árbol sin
  contenedores (la materialización del ItemsControl ocurre en el pase de layout) y las tarjetas quedaban
  en (0,0) — el primer pase ahora se consume en `NodesHost.LayoutUpdated` mientras queden contenedores
  sin posicionar, y la suscripción anónima por contenedor (fuga en cada Rebuild) desapareció;
  (3) la geometría que el parser XAML produce por la propiedad `Data` **no es asignable** a `Path.Data` en
  este host (bisección del sondeo: `ArgumentException` hasta sobre un Path recién creado; una
  `PathGeometry` construida por código sí se asigna) — el conversor de iconos clona ahora figura a figura
  (receta cables) y devuelve geometría asignable.
- [x] **Mitad visual del criterio demostrada (hito 226) — veredicto de la comparación con el escritorio**.
  Método: dos capturas del MISMO flujo (`flow_01`) con el MISMO mapeo grafo→pantalla (encuadre «Ajustar»
  compartido: zoom 1.11, translate −(44.6, 34.8)·1.11) — (a) sonda temporal headless en `FileFlow.Tests`
  que monta la `EditorView` real (Skia, PNG 980×640), (b) `FileFlow.App.Uno.exe --dump-canvas` temporal
  (`RenderTargetBitmap` 2401², contenido a 1:1 lógico, recortado a la misma ventana). Comparación por
  **features** con PIL/numpy (no píxel a píxel: motores de render distintos). Ambas sondas se eliminaron
  al cerrar el hito; queda aquí el veredicto:

  | Feature | Avalonia | Uno | Veredicto |
  | :--- | :--- | :--- | :--- |
  | Fondo del lienzo `#10131B` | (16,19,27) exacto | (16,19,27) exacto | ✅ idéntico byte a byte |
  | Grid `#21262D` | presente (lo pinta el shell) | presente (paso 50, doble sutil) | ✅ presente en ambos |
  | Posición de las 3 tarjetas (franja acento, fila y=130) | (63..281, 340..558, 618..836) | (67..278, 345..555, 622..833) | ✅ ±2 px / ±6 px sobre el mapeo compartido (61..283, 339..561, 616..838) |
  | Barra de acento `#818CF8` arriba de cada tarjeta | sí (y129..135) | sí (y130..142 tras el fix del 226) | ✅ presente en ambos |
  | Cables cruzando los huecos entre tarjetas | sí | sí | ✅ misma Bézier del núcleo |
  | Ancla vertical del cable en los huecos | y217..219 (ancla real del socket ≈ y 194 de grafo) | y171..174 (`Location.Y + 40` provisional) | 🔶 **brecha declarada de la fase 3.1**: desaparece con las anclas write-back de la 3.3 |
  | Color del cable por tipo (Files = verde `#10B981`) | sí | `#818CF8` fijo | 🔶 brecha de la fase 3.5 (la matriz de sockets ya vive compartida en `PortPalette`) |
  | Cara de tarjeta `#161B22` | la sonda aislada pinta `#1E1E1E` (su fusión de diccionarios difiere de la app completa; la línea base humana `panel-editor-dark.png` confirma `#161B22`/`#212222` en la app real) | token `#161B22` | 🔶 a re-medir con la sonda montada en el shell completo (fase 3.5); no afecta al veredicto de posiciones ni tokens del lienzo |

  Conclusión: **la pintura del lienzo Uno coincide con la del escritorio a nivel de estructura y tokens**
  (mismo mapeo grafo→pantalla, mismas posiciones, fondo y acento idénticos, cables en los huecos), con
  dos divergencias medidas que el propio plan ya declaraba como trabajo de fases posteriores (ancla del
  cable → 3.3, color por tipo y cara → 3.5). El píxel a píxel sigue sin ser el criterio: los motores de
  render difieren y el análisis por features es el que puede dar fe.

  **Defecto real cazado y corregido por esta comparación**: la barra de acento y el relleno del icono del
  Uno no se pintaban (transparentes) — `{Binding Node.AccentBrushColor}` dentro de un `SolidColorBrush`
  no resuelve el DataContext en WinUI (el color vive en `NodeCardViewModel`, no en `Node`). Corregido a
  `{Binding AccentBrushColor}`; re-verificado con un segundo volcado (5.468 px de `#818CF8`) y el
  selfcheck en verde (exit 0). Es el cuarto defecto que la infraestructura de verificación caza en este
  tramo (225: cables, posiciones, iconos; 226: acento).

### Fase 3.2 — Selección, arrastre y teclado — **IMPLEMENTADA (hito 228); criterio manual pendiente de sesión con puntero**

- [x] Click selecciona (`IsSelected` TwoWay), `BringToFront`, drag de nodos (manipulación), rubber band.
  El clic sobre la tarjeta escribe `IsSelected` y el NÚCLEO reacciona (SelectedNode + BringToFront +
  contador, la misma reacción del escritorio); el arrastre mueve la selección entera en espacio de grafo
  (delta de pantalla dividido por el zoom, el inverso del mapeo compartido), repasa el cable al vuelo y
  registra `MoveNodesAction` en el `UndoRedoService` del núcleo al soltar. El rubber band dibuja el
  rectángulo (capa `RubberLayer`), selecciona por centro de tarjeta y un clic sin arrastre en el fondo
  deselecciona (el estándar de Nodify). El pan queda en el botón DERECHO, como el escritorio.
- [x] Atajos del §2.1 en el host Uno (los mismos de `EditorView_KeyDown`) — con la TABLA COMPARTIDA que
  el plan anticipaba: [`EditorKeyboardShortcuts`](file:///FileFlow.App.Core/Services/EditorKeyboardShortcuts.cs)
  en el núcleo (claves canónicas + clasificación + ejecutor sobre `EditorViewModel`); el host Uno la
  consume en `OnKeyDown` y el escritorio refactorizado a ella (su spotlight conserva la posición del
  cursor). Una sola fuente de claves para los dos hosts.
- [x] Renombrado F2 con el cuadro de edición: F2 llega por la tabla (StartRenaming del núcleo), la caja
  del host Uno refresca con `IsEditingTitle`/`EditingTitleText` (faltaban en el refresco agregado de la
  tarjeta: el defecto cazado por la propia implementación), toma el foco al aparecer (callback de
  `VisibilityProperty`) y confirma con Enter/LostFocus, cancela con Escape — las teclas de la caja del
  escritorio.
- [x] Guardia de origen y atajos: [`UnoShortcutParityGuardTests`](file:///FileFlow.Tests/Unit/App/UnoShortcutParityGuardTests.cs)
  (5 tests) — la tabla no se vacía ni duplica combinaciones y cubre los diez comandos del lienzo; los
  dos hosts resuelven y ejecutan POR el servicio (nada de switches paralelos con claves propias); toda
  tecla mapeada en un host existe como binding canónico; y las cajas de renombrado conservan su teclado
  local en ambos hosts (el lienzo no secuestra un TextBox). La guardia de origen del 3.1 sigue vigente.
- [x] Sonda en el selfcheck (lo verificable sin puntero ni foco): selección con reacción del núcleo
  (SelectedNode asignado), contenedor del glow presente, Delete por comando canónico (3→2) y
  restauración por el undo del propio núcleo (2→3) — el estado queda intacto y el undo queda probado.
- [ ] Criterio de salida SIN demostrar — y el intento de cerrarlo, escrito: la lista de interacciones de `InputInteractionTests`
  ejecutada a mano en el host Uno con resultado escrito. **Sesión con puntero inyectado (2026-09-26, hito 231): BLOQUEO
  IRREDUCTIBLE del entorno, documentado con evidencia** — el puntero inyectado (mouse_event, SendInput absoluto/relativo/virtual,
  PostMessage al bridge) no llega al contenido de WinAppSDK aunque el cursor se mueva y el teclado inyectado SÍ llega (Alt+F4 cierra la app);
`InjectTouchInput` — la única vía WM_POINTER nativa — está denegada (error 5, exige UIAccess). El instrumento queda conservado
(`docs/qa/qa_manual.py`: calibración de tarjetas/sockets por píxel + guion completo con métricas) y el informe en
[`docs/qa/guion_manual_32_33_resultado.md`](file:///docs/qa/guion_manual_32_33_resultado.md) con la matriz de las 9 técnicas probadas. Queda para la primera sesión con puntero real (o UIAccess):
  el guion es click selecciona y sube de Z; drag mueve y Ctrl+Z lo deshace; rubber band selecciona varias; clic en fondo
  deselecciona; Shift+A abre el spotlight; F2 renombra y Enter confirma; Delete borra; Ctrl+D duplica. Los comportamientos
  están demostrados POR LOS MISMOS MÉTODOS que los handlers en el selfcheck (sondas 3.2/3.3/3.4).

### Fase 3.3 — Puertos y cables vivos — **IMPLEMENTADA (hito 229); criterio del plan demostrado por sonda**

- [x] Anclas calculadas por el lienzo y escritas en `PortViewModel.Anchor` (el write-back que Nodify
  hacía), con la proyección de puntos explícita: `AnchorOf` localiza el socket real en el árbol visual
  (el elemento cuyo DataContext es el puerto), toma su centro transformado y lo cruza a espacio de grafo
  por `UnoPointProjection` — la guardia del 217 validó que no quedara ningún cruce hecho a mano. El
  write-back corre tras el primer layout, en cada arrastre y antes de conectar; `DrawWires` traza con
  las anclas REALES (la estimación `Location.Y + 40` queda sólo como respaldo si el árbol no materializó).
- [x] Sockets interactivos: el socket pulsa para iniciar/terminar cable (la tarjeta reporta por eventos
  `SocketRequested`/`DisconnectRequested` — no conoce el lienzo — y el lienzo habla con los comandos del
  núcleo), cable pendiente siguiendo al cursor (`TargetLocation` en Sdk.Point, la misma Bézier compartida,
  con snapping a 20 px del socket compatible), soltar conecta vía `FinishConnectionCommand` (o cancela en
  el vacío, como Escape), resaltado de compatibilidad (`ApplyPortCompatibilityHighlight` del núcleo
  alimenta los estados que la matriz de sockets ya pintaba) y desconectar con click derecho
  (`DisconnectConnectorCommand`).
- [x] Borrado de cable: la desconexión por puerto quita los cables del socket; el hit-testing fino por
  `ConnectionGeometry.DistanceTo` queda para la revisión del menú contextual del cable (3.4 junto al
  resto de decoradores — el click derecho del socket ya cubre el caso de uso del criterio).
- [x] Aviso de cables perdidos del VM visible en el host: banner (`CanvasNoticeBanner`) con el texto y
  las filas de `DroppedConnectionFixViewModel` — «Ir al nodo» y «Reconectar a «X»» ejecutan los comandos
  del VM; refresco por `PropertyChanged`.
- [x] Criterio de salida DEMOSTRADO por sonda en el selfcheck (sin puntero): conectar/desconectar dos
  nodos cualesquiera por los MISMOS métodos que usan los handlers — anclas reales verificadas,
  StartConnection+FinishConnection añade la conexión, estados de puerto refrescados, desconexión por
  comando, restauración exacta por la pila de undo (las tres undos devuelven también la conexión que
  CreateConnection sustituyó; el grafo queda como al entrar). Lo que exige puntero real (el gesto de
  arrastre del cable) queda cubierto por los mismos métodos que la sonda ejecuta.

### Fase 3.4 — Decoradores y servicios del lienzo

- **Estado (hito 230): IMPLEMENTADA.** Notas y grupos renderizados en capas del lienzo (grupos detrás,
  notas delante) con posiciones proyectadas por el conversor del 217; la capa se reconstruye por
  `CanvasDecorators.CollectionChanged` (suscripción simétrica en el setter, la misma vida de Nodes/Connections).
  Notas: crear, mover (arrastre por los deltas del gesto), recolorear, borrar; grupos con `GroupSelectedNodes`.
- Drag & drop del cajón (DragOver/Drop crea el nodo en el punto del grafo) y spotlight (Shift+A, Espacio,
  doble clic en fondo) con lista filtrada y confirmación que añade el nodo real; migas de subflujos con
  navegación por `NavigateToBreadcrumbCommand`.
- Criterio de salida: **demostrado por sonda** en el selfcheck (nota creada/movida/borrada con la capa al
  día, grupo creado y borrado, spotlight que añade un nodo real en el punto pedido, migas navegadas) por
  los mismos métodos que los handlers; el **gesto** de arrastrar desde el cajón espera la sesión con puntero
  (mismo guion manual que la 3.2/3.3).
- Dos defectos cazados por la propia sonda en su primera corrida: la capa de decoradores no se enteraba de
  `AddAnnotation`/`AddGroup` (faltaba la suscripción a `CanvasDecorators`) y el reintento del sondeo heredaba
  la selección del undo (Delete acumulado: 3→0) — cura: desselección explícita tras restaurar, que es lo que
  el clic en el fondo del gesto real implica.

### Fase 3.5 — Temas y localización

- **Estado (hito 233): IMPLEMENTADA.** El host Uno implementa su mitad de `ThemeHostBridge`
  ([`UnoThemeHost`](file:///FileFlow.App.Uno/Platform/UnoThemeHost.cs), espejo de `AvaloniaThemeHost`):
  publica la variante sobre la raíz del contenido (`RequestedTheme`) y aplica los tokens del tema activo.
- **La lección central de la fase**: en WinUI ni `StaticResource` (captura la instancia en la carga) ni
  `ThemeResource` de aplicación re-evalúan al reescribir `Application.Resources` — la republicación por
  claves NO llegaba a los consumidores vivos en ninguna de las corridas. La cura robusta: los tokens
  `Canvas*` viven UNA vez en App.xaml como pinceles singleton y `UnoThemeHost.RepublishTokens` cambia su
  COLOR in-place — la mutación repinta a todos los consumidores vivos (XAML capturado y lecturas de
  código, que ahora resuelven por `Application.Current.Resources` porque la indexación directa del
  control no encadena). El consumo unificado mató además los 3 diccionarios duplicados de los controles.
- **Localización**: `LocalizationManager` ya es portable y `SetCulture` notifica; el host Uno registra
  los resx del núcleo y rescribe sus textos de marco en `LanguageChanged` (los textos del lienzo llegan
  de los VMs del núcleo, ya localizados).
- **Criterio de salida: DEMONSTRADO POR SONDA** en el selfcheck (hito 233): `SetThemeById("light_studio")`
  por la API del núcleo re-tematiza el fondo del lienzo y la cara de las tarjetas EN CALIENTE (colores
  medidos del árbol real: `#10131B` → `#F8FAFC`), la variante clara llega heredada al control y la
  restauración deja el `dark_fluent` activo.
- **La caza del tramo**: el crash `Cannot create instance of EditorCanvasControl` en las primeras
  corridas fue doble — builds incrementales obsoletos (XBF viejo) y las lecturas de código
  `Resources["CanvasWireBrush"]` lanzando `KeyNotFound` al instanciar (la indexación directa no encadena
  a Application.Resources). La sonda temporal de la excepción interna del `InitializeComponent` lo
  delató; quedó retirada tras la cura.

### Fase 3.6 — Cierre de la rebanada

- **Rendimiento medido**: el grafo de referencia (40 nodos + cables, como el banco de ejemplos)
  se arrastra y se encuadra sin tirones perceptibles; si no llega, primera medida: virtualizar
  contenedores (`ItemsControl` con reciclado) y segundo: evaluar la opción C acotada al cable.
- Guardia de paridad: tabla de las ~20 interacciones del lienzo, cada una con dónde está probada
  (suite: la lógica; host: el sondeo manual de cada fase).
- Protocolo completo: hito, session_summary, notas de versión, COVERAGE si hay mutación nueva.

---

## 6. Riesgos, con su mitigación

| Riesgo | Por qué puede doler | Mitigación |
| :--- | :--- | :--- |
| Los bindings de geometría mueren en silencio | La lección del hito 211, ahora con dos pilas de binding (WinUI `Binding` por reflexión y `x:Bind` compilado) | **Escrita y activa desde el hito 217 (antes del primer enlace)**: `UnoPointProjection` (mitad portable, en Core) + `UnoPointConverter` (envoltorio WinUI en el host) + `UnoGeometryBindingGuardTests` — el censo del XAML del host falla si algún enlace de geometría (Location/Anchor/Source/Target/ViewportLocation/TargetLocation/Spotlight*) no lleva `conv:UnoPointConverter.Instance`; su mutación (`proyeccion-uno-sin-guardia`) muerde. La regla nació con cero enlaces en el árbol: no hay excepciones históricas |
| Material.Icons no existe para Uno | La tarjeta y la caja de herramientas usan decenas de iconos | El **font** Material Design Icons (TTF) como recurso del host + `FontIcon`/glyph; cero dependencia de control |
| `DynamicResource` no existe en WinUI | Los ~20 tokens del lienzo no se refrescarían al cambiar de tema | Fase 3.5: republicación de tokens por el puente, como hace el host Avalonia |
| Rendimiento con grafos grandes | `Canvas` de WinUI no virtualiza solo; la tarjeta es pesada | Medir en 3.6 con el grafo de referencia; virtualización y, si no basta, la opción C acotada |
| Divergencia de comportamiento entre hosts | Dos lienzos, dos hosts, un VM | El VM manda (validación, undo, avisos); guardias de literales sobre ambos XAML; lista de paridad en 3.6 |
| El renombrado y el IME | La edición de texto en un lienzo propio es un campo minado clásico | 3.2 usa un `TextBox` real superpuesto, nunca texto dibujado a mano |

---

## 7. Criterio de éxito de la rebanada

1. El host Uno abre el flujo de ejemplo, lo edita (mover, conectar, renombrar, borrar, deshacer)
   y lo guarda — **el mismo archivo** que el escritorio abre sin tocar nada (el formato ya lo
   garantiza el escritor compartido).
2. La suite completa sigue verde al 100 % y **el host Avalonia no ha cambiado** (sus baselines
   visuales intactas).
3. Toda la geometría nueva está probada en la suite (pura, en Core).
4. Las guardias cierran el triángulo: núcleo sin UI, Uno sin Avalonia, lienzo Uno sin atajos
   divergentes.

## 8. Lo que este plan NO hace

- No toca `FileFlow.App` (host Avalonia) ni sus baselines.
- No introduce dependencias nuevas en `FileFlow.App.Core` (la geometría es C# puro).
- No extrae `IEditorCanvasView` salvo que la fricción lo pida (§4).
- No promete fechas por fase: cada fase cierra cuando su criterio de salida se mide verde.

## 9. Estado de ejecución

| Fase | Estado | Hito |
| :--- | :--- | :--- |
| 3.0 — Geometría pura en Core | ✅ HECHA | 216 |
| 3.1 — Lienzo estático | ✅ CRITERIO DEMOSTRADO (hito 225: app corriendo con el grafo real —selfcheck verde—; hito 226: comparación visual con el escritorio por features —mismo mapeo, posiciones ±2/±6 px, fondo y acento idénticos, cables en los huecos— con las brechas declaradas de 3.3/3.5 anotadas) | 221, 224, 225, 226 |
| 3.2 — Selección, arrastre y teclado | 🔶 IMPLEMENTADA (hito 228: selección/drag/rubber band, tabla compartida de atajos en el núcleo consumida por los dos hosts, renombrado F2 completo, guardia de paridad 5/5, sonda de selección/borrado/deshacer en el selfcheck; el criterio «interacciones ejecutadas a mano» queda para sesión con puntero) | 228 |
| 3.3 — Puertos y cables vivos | ✅ CRITERIO DEMOSTRADO (hito 229: anclas write-back reales del árbol, sockets vivos con cable pendiente y snapping, desconexión por socket, aviso de cables perdidos pintado; conectar/desconectar verificado por sonda en el selfcheck con restauración exacta) | 229 |
| 3.4 — Decoradores y servicios del lienzo | 🔶 IMPLEMENTADA (hito 230: notas/grupos en capas proyectadas, spotlight con confirmación real, migas navegables, drag & drop del cajón; criterio demostrado por sonda —nota/grupo/spotlight/migas—; el gesto del cajón espera sesión con puntero) | 230 |
| 3.5 — Temas y localización | ✅ CRITERIO DEMOSTRADO (hito 233: mitad Uno del puente con mutación in-place de pinceles singleton — la única vía que WinUI repinta; sonda: light_studio re-tematiza fondo y tarjetas en caliente con restauración; localización por LanguageChanged + textos del núcleo) | 233 |
| 3.6 — Cierre de la rebanada | ⬜ Pendiente | — |

El plan se escribe antes de la primera fase y no se edita a mano por avance: la columna de estado se
actualiza cuando cada fase mide su criterio de salida verde (la 3.1 quedó demostrada por sus dos mitades
—hito 225 el árbol, hito 226 la comparación por features—; las 3.2/3.4 están implementadas y sus criterios
de «interacciones a mano» quedaron documentadas tras la sesión de QA con puntero inyectado (hito 231: bloqueo
irreductible de la inyección de puntero en este entorno, evidencia en `qa-manual-report.md` junto al instrumento
`qa_manual.py` que queda preparado); la 3.3 quedó demostrada por sonda y la 3.5 por su sonda de re-tematización).
