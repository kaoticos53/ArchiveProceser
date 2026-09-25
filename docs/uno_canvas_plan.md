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

### Fase 3.1 — Lienzo estático (el grafo se ve) — **EN CURSO (código completo, hito 221)**

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
- [ ] `NodeCardView` visual completo (la tarjeta de 559 líneas de Avalonia traducida a estilos WinUI:
  hoy la tarjeta es una versión reducida de lectura).
- [ ] Criterio de salida pendiente de demostrar: el host Uno arranca y muestra el flujo de ejemplo con
  nodos y cables a sus posiciones; sondeo de captura comparado con la pinta del Avalonia.

#### Traducción de `NodeCardView.axaml` a XAML WinUI/Uno (pendiente, resto de la fase)

- La tarjeta tiene 559 líneas y es el trozo más gordo de la rebanada. Los estilos con selectores de
  Avalonia se reescriben como recursos/estilos WinUI; los estados (`connected`, `dragSource`,
  `compatible`…) pasan a clases visuales equivalentes.

- Criterio de salida: el host Uno arranca y muestra el flujo de ejemplo con nodos y cables a sus
  posiciones; sondeo de captura comparado con la pinta del Avalonia.

### Fase 3.2 — Selección, arrastre y teclado

- Click selecciona (`IsSelected` TwoWay), `BringToFront`, drag de nodos (manipulación), rubber band.
- Atajos del §2.1 en el host Uno (los mismos de `EditorView_KeyDown`).
- Renombrado F2 con el cuadro de edición.
- Guardia de origen nueva (estilo `UnoHostFreeOfAvaloniaGuardTests`): el lienzo Uno no referencia
  Avalonia, y las claves de atajos son las mismas que las del host Avalonia (tabla compartida si
  hace falta).
- Criterio de salida: la lista de interacciones de `InputInteractionTests` ejecutada a mano en el
  host Uno con resultado escrito.

### Fase 3.3 — Puertos y cables vivos

- Anclas calculadas por el lienzo y escritas en `PortViewModel.Anchor` (el write-back que Nodify
  hacía), con la proyección de puntos explícita.
- Sockets interactivos: iniciar cable, resaltado de compatibilidad (`ApplyPortCompatibilityHighlight`
  ya existe en el VM), cable pendiente siguiendo al cursor, soltar conecta vía
  `FinishConnectionCommand`, desconectar desde el socket.
- Menú contextual de cable (borrar) — necesita hit-testing de cables: `ConnectionGeometry` expone
  el punto más cercano del trazado.
- Criterio de salida: conectar/desconectar dos nodos cualesquiera; el aviso de cables perdidos del
  VM se ve y sus filas reconectan.

### Fase 3.4 — Decoradores y servicios del lienzo

- Notas (crear, arrastrar, recolorear, borrar) y grupos (incluido `GroupSelectedNodes`).
- Drag & drop desde la caja de herramientas (soltar crea el nodo en el punto del grafo).
- Spotlight (Shift+A) y migas de pan de subflujos.
- Criterio de salida: el flujo de ejemplo se edita por completo desde el host Uno.

### Fase 3.5 — Temas y localización

- El host Uno implementa su mitad de `ThemeHostBridge`: publica la variante y **republica los
  tokens** en los recursos de la app Uno cuando cambia el tema (equivalente a lo que
  `AvaloniaThemeHost` hace con `app.Resources`), porque `CustomResource` no se refresca solo.
- Localización: `LocalizationManager` ya es portable; el XAML Uno consume por binding a los VMs
  (ya localizan) o una extensión de marcado propia — decisión en fase, con el riesgo anotado.
- Criterio de salida: cambiar el tema en el host Uno re-tematiza el lienzo en caliente.

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
| 3.1 — Lienzo estático | 🔶 EN CURSO (código completo; tarjeta visual y criterio de salida pendientes) | 221 |
| 3.2 — Selección, arrastre y teclado | ⬜ Pendiente | — |
| 3.3 — Puertos y cables vivos | ⬜ Pendiente | — |
| 3.4 — Decoradores y servicios del lienzo | ⬜ Pendiente | — |
| 3.5 — Temas y localización | ⬜ Pendiente | — |
| 3.6 — Cierre de la rebanada | ⬜ Pendiente | — |

El plan se escribe antes de la primera fase y no se edita a mano por avance: la columna de estado se
actualiza cuando cada fase mide su criterio de salida verde (el criterio de salida de la 3.1 exige la
app corriendo y la captura comparada, no está medido aún).
