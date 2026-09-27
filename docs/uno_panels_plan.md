# Plan de la rebanada 4 — Paneles del editor en el host Uno (caja de herramientas + inspector)

> **Estado**: decisión tomada, **sin código tocado**. Escrito antes de empezar, como la rebanada 3.
> **Cerrada en el hito 236** (ver §5): las cuatro fases en verde, selfcheck EXIT 0, suite 1849 + 1
> omitida de 1850, dos mutaciones mordiendo.
> **Origen**: cierre de la rebanada 3 (hito 234/235): el lienzo está vivo, medido y defendido; lo que
> falta para que el host Uno sea un editor completo son los dos paneles que flanquean el lienzo en el
> escritorio: la **caja de herramientas** (catálogo de nodos) y el **inspector** (ficha del nodo).

---

## 1. El encargo

«Abre el siguiente tramo del host Uno: la caja de herramientas y el inspector de nodos con paridad
al escritorio.»

En el host Avalonia esos paneles son `NodeToolboxView` + `NodeInspectorPanelView` consumiendo
`ToolboxViewModel` y `NodeInspectorViewModel`. En WinUI/Uno ninguna de las dos vistas existe. Lo
medido antes de decidir:

| Pieza | Estado medido | Consecuencia |
| :--- | :--- | :--- |
| `ToolboxViewModel` / `NodeInspectorViewModel` | **Portables y ya compartidos**: viven SOLO en `FileFlow.App.Core` y el host Avalonia los consume de ahí (el movimiento ya se hizo). `MainViewModel` los expone (`Toolbox`, `NodeInspector`). | El host Uno no portaNADA del núcleo: sólo escribe **vistas**. |
| `AddFileFlowCoreServices` | Ya registra ambos VMs como singletons. | `App.Services` los resuelve sin registro nuevo. |
| Iconos (`MaterialIconKind`) | El paquete **base** `Material.Icons` (netstandard, ya referenciado por el núcleo) trae el enum y un proveedor de geometría (`MaterialIconDataProvider`); el control visual `Material.Icons.Avalonia` es sólo de Avalonia. | Un conversor `MaterialIconKind → Geometry` de WinUI dibuja los glifos con el paquete que YA está (ya existe precedent en el host: `MaterialIconKindToGeometryConverter`, fase 3.4). |
| Localización | Las claves `Toolbox_*`, `Inspector_*`, `Category_*`, `Role_*` viven en los resx de `FileFlow.App` (host), **no** registrados en el host Uno. Las claves `Uno_*` están en `App.xaml`. | Los textos nuevos del panel entran como claves `Uno_*` en `App.xaml` (es/es), el patrón de la fase 3.5. El catálogo en sí ya llega localizado POR el VM (usa `LocalizationManager` con los recursos de plugins). |
| `IFileDialogService` | `AddFileFlowCoreServices` registra el **nulo**; el host Uno no registró uno real. | Botón «explorar» del inspector sin función. Se escribe `UnoFileDialogService` con `Windows.Storage.Pickers` y se registra ANTES de resolver VMs. |
| `ServiceHolders.WindowService` / `PopupMenu` | Caen a Null en el host Uno. | El botón de texto multilínea del inspector (diálogo) queda **sin función declarada**: la paridad registra lo pendiente, no lo finge. |
| Filtro y write-back ya probados | `ToolboxViewModel_SearchText_ShouldExpandMatchingCategories` y `NodeClipboardServiceTests` ya prueban el filtro y `NodeInstance.Parameters` — pero SON del núcleo, no de que el HOST Uno los use. | Las guardias de esta rebanada apuntan al host: que el panel cite el VM y que el inspector muestre los flags del VM. |

## 2. El contrato (lo único que ata las dos mitades)

Igual que la rebanada 3: **no se extrae ninguna interfaz nueva**. El contrato ya existe y es la
superficie pública de `ToolboxViewModel` / `NodeInspectorViewModel` (+ `NodeParameterViewModel`).
La regla escrita: el host Uno escribe vistas que consumen esa superficie y nada más; cualquier
lógica nueva (filtrado, validación, creación de nodos) NO se escribe en el host — si falta en el
VM, se añade al VM (portable, probada por ambos hosts).

## 3. Las fases

### Fase 4.1 — Los dos paneles y el marco de tres columnas
- `NodeToolboxPanel` (WinUI): buscador (`SearchText` TwoWay → el VM filtra), filtro de categoría
  (chips desde `AvailableCategories`), grupos `CategoryGroups` con expansión (acordeón exclusivo
  del VM), ítem compacto con icono (conversor de geometría), insignia de rol, favorito conmutables
  (`ToggleFavoriteCommand`), y **doble clic para añadir** en el centro del viewport (el gesto de
  arrastrar espera puntero real, igual que el cajón del escritorio en su día).
- `NodeInspectorPanel` (WinUI): cabecera (título + «Probar» + «Cerrar» → `ClosePanelCommand`),
  descripción del nodo, parámetros por los flags del `NodeParameterViewModel` (texto, número,
  slider, desplegable, booleano, ruta con explorar, multilínea), valor evaluado con copia,
  bloque de telemetría (`UpdateTelemetryStats` → duración / elementos / estado).
- `MainWindow` pasa a tres columnas: cajón (Ancho fijo ~280) | lienzo (estrella) | inspector
  (Ancho ~300, `Visibility` atado a `NodeInspector.IsOpen`).
- Criterio de salida: la sonda del selfcheck (fase 4.4) inventaría el árbol de ambos paneles.

### Fase 4.2 — Guardias (la lección del 165: contratos como código vivo, árbol sin comentarios)
- `UnoToolboxPanelGuardTests` (4 tests): el panel usa el VM del núcleo (no reinventa catálogo),
  el añadir pasa por `EditorViewModel.AddNode`, el favorito por `ToggleFavoriteCommand`, y el
  buscador está atado a `SearchText` (TwoWay) — citados por nombre.
- `UnoInspectorPanelGuardTests` (3 tests): el panel se monta sobre `NodeInspectorViewModel`,
  la edición de parámetros usa los flags `Is*` del VM (los mismos que el escritorio) y el
  write-back pasa por `OnParameterValueChanged` (mutación asociada), con el bloque de telemetría
  atado a `NodeViewModel.TelemetryStats`.

### Fase 4.3 — Mutaciones (morder lo que importa)
- `toolbox-sin-filtro.json`: si `ToolboxViewModel.RefreshToolbox` deja de respetar `SearchText`,
  el testigo rojo exige que el filtro reduzca el catálogo (`ToolboxViewModelTests`, control:
  la prueba del acordeón que no depende del filtro).
- `inspector-sin-write-back.json`: si `NodeParameterViewModel` deja de llamar a
  `OnParameterValueChanged`, el valor editado no llega al nodo — testigo rojo con
  `NodeInstance.Parameters` (control: la prueba que sólo toca el VM).
- COVERAGE.md regenerado por su guardia.

### Fase 4.4 — Cierre de la rebanada
- Sonda `ProbePanelsRoundTrip` en el selfcheck: catálogo poblado (≥ 1 grupo), filtro que reduce,
  favorito conmutado y restaurado, inspector abierto por selección con parámetros del nodo,
  **write-through** de un parámetro al `NodeInstance`, bloque de telemetría y cierre.
- `--selfcheck` EXIT 0 con la rebanada nueva; suite al 100%; walkthrough, session_summary y
  (si el tramo es visible) notas de versión.

## 4. Los umbrales

- Paridad de flujo de trabajo: **encontrar un nodo → añadirlo → editarlo → ver su telemetría**
  sin teclado, con los dos paneles y el lienzo, en el host Uno.
- Ninguna lógica de catálogo o de ficha DUPLICADA en el host: si está en el VM, la vista la llama.
- Todo texto nuevo del marco en `App.xaml` (es/en) por claves `Uno_*`; el catálogo hereda la
  localización del núcleo (recursos de plugins) sin copiar cadenas.
- Lo que no llegue queda DECLARADO pendiente (diálogos de ventana, popup de variables), nunca
  fingido.

---

## 5. Estado de la rebanada (hito 236): CERRADA

- **Fase 4.1** — Los dos paneles y el marco de tres columnas: **CUMPLIDA**. `NodeToolboxPanel`
  (XAML + x:Bind) y `NodeInspectorPanel` (por código: WinUI no selecciona DataTemplates por
  propiedad del ítem), `MainWindow` tres columnas, `UnoFileDialogService` registrado. Medido en la
  app viva: catálogo 81 ítems, filtro 81 → 5, añadir por AddNode con undo, favorito conmutado,
  inspector 10/10 editores, write-through verificado, cierre por comando.
- **Fase 4.2** — Guardias: **CUMPLIDA**. 11 tests (6 del cajón + 5 del inspector), guardias de
  árbol (la lección del 232) + tablas de paridad con citas verificadas contra el índice real (la
  lección del 227).
- **Fase 4.3** — Mutaciones: **CUMPLIDA**. `toolbox-sin-filtro` y `inspector-sin-write-back`, las
  dos MUERDEN (testigo rojo + control verde, árbol restaurado por bytes); COVERAGE.md → 48.
- **Fase 4.4** — Cierre: **CUMPLIDA**. Selfcheck EXIT 0 con la sonda de paneles; suite 1849 + 1
  omitida de 1850, 0 errores; walkthrough y notas al día.

**Declarado pendiente (no fingido):**

- El toggle compacto/detallado del cajón: `x:Bind` dentro de una `DataTemplate` de WinUI no alcanza
  la página (sólo ve el ítem) — la insignia de rol va siempre visible.
- El botón «Probar» del inspector (prueba aislada con fichero): el contrato síncrono del
  `IFileDialogService` exige bloquear FUERA del hilo de UI; una llamada hecha desde el hilo de UI
  del click devolvería null antes que interbloquear — requiere la variante asíncrona del contrato.
- El picker de variables y los diálogos de nodo (`ServiceHolders.WindowService`/`PopupMenu` siguen
  en Null en este host): los botones existen, caen a su no-op seguro del núcleo.
- El gesto de arrastre fino desde el cajón (sesión con puntero real, el pendiente del hito 231).
