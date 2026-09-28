# Plan de la rebanada 5 — El host Uno como producto: gestos con puntero real, pickers de variables y empaquetado

> **Estado**: decisión tomada, **sin código tocado**. A diferencia de las rebanadas 3 y 4 —escritas
> antes de empezar— esta se escribe **después de la primera medición de su primera fase**: la sesión
> manual del hito 247 ya ejecutó el guion con puntero real, certificó el canal del gesto físico y midió
> el defecto de *hit-test* que hay que arreglar antes de certificar nada más
> ([`docs/qa/qa_manual_gestos_247.md`](file:///docs/qa/qa_manual_gestos_247.md)). Eso entra aquí como
> punto de partida MEDIDO, no como promesa.
> **Origen**: cierre de la rebanada 4 (hito 246): el host Uno ya tiene lienzo (rebanada 3) y paneles
> (rebanada 4). Lo que falta para que sea un PRODUCTO, y no una demo que se ejecuta desde `bin/Debug`:
> (1) que sus gestos estén **certificados** con puntero real; (2) que los servicios que hoy caen a Null
> tengan implementación —los **pickers de variables** y los diálogos de nodo, que es donde el parámetro
> se edita de verdad—; y (3) que se pueda **ENTREGAR** (paquete, firma, CI y publicación).

---

## 1. El encargo

«Escribe el plan de la rebanada 5 del host Uno: gestos de puntero real, pickers de variables y empaquetado.»

Las tres líneas no son independientes: el empaquetado sin gestos certificados entrega una app que
nadie ha probado como se usa (con ratón), y los pickers sin el hit-test arreglado heredan una superficie
de clic que miente. El orden de las fases (§5) es por dependencia, no por gusto.

## 2. Lo que se midió antes de decidir

### 2.1 Gestos: lo que el hito 247 dejó medido

| Pieza | Estado medido (247) | Consecuencia para el plan |
| :--- | :--- | :--- |
| Canal del puntero real | **CERTIFICADO**: con el ratón del operador el borde de selección aparece y desaparece (`sel 5 ↔ 1461 px`, medido por el vigilante) | El guion ya es ejecutable; lo que falta es arreglar lo que bloquea su mitad |
| Puntero inyectado | **BLOQUEADO**: `--inject-test` da delta 0 px en acento, borde y diferencia general de píxeles | La certificación es una sesión HUMANA: el plan la trata como tal (protocolo, no automatización) |
| **Hit-test de las tarjetas** | **ROTO Y MEDIDO**: la sonda cae `(280, 41)` fuera (el lienzo vive en columna 1 y fila 1 de `MainWindow.xaml`); clicar la cara de una tarjeta selecciona OTRA o ninguna. `CardAt`/`HitsInteractiveControl` pasan un punto de `GetCurrentPoint(RootGrid)` a `FindElementsInHostCoordinates` | **Primera fase**: sin esto no hay selección, ni arrastre, ni doble clic, ni guardia de la barra de zoom que valgan |
| Rubber band | **NO CERTIFICADO** y con duda declarada: `UpdateRubberSelection` compara el rectángulo del puntero (espacio de pantalla) contra `Canvas.GetLeft/Top` del contenedor (espacio LOCAL del plano) | Entra en la fase de gestos como sospecha a medir, no como hallazgo |
| Sockets | Camino LIMPIO (`NodeCardView.OnSocketPressed`/`OnSocketRightTapped`, hit-testing propio); el clic derecho del guion falló por PUNTERÍA sobre una diana de ~12 px | La diana del socket es un hallazgo de usabilidad que la fase debe resolver (o declarar con su medición) |
| Observación UIA | La conmutación a las pestañas con contenido de snapshots materializado **tumba el proceso** (exit 127 sin WER, frontera LATENTE del 245) | Las sondas de la fase 5.4 no pueden conectar un cliente UIA con ese contenido en pie: se declara y se rodea |
| Instrumento | [`docs/qa/qa_manual_session.py`](file:///docs/qa/qa_manual_session.py) mide calibración, escena por fotograma (acento, borde, diferencia general), primer plano, Z-ORDER y cursor; el reparto es explícito: el veredicto del GESTO lo firma el operador, la evidencia de que llegó la firma la medición | Es el instrumento oficial de la rebanada: se amplía, no se reescribe |

### 2.2 Pickers: qué servicios quedan en Null y quién los consume

Los contratos son **portables y ya están escritos** en el SDK; los ViewModels también están en el
núcleo. El host Uno sólo tiene que implementar el adaptador y la vista — y hoy no tiene ninguno.

| Contrato (SDK) | Quién lo consume | Escritorio | Host Uno hoy | Consecuencia |
| :--- | :--- | :--- | :--- | :--- |
| `IWindowService` (`DialogKeys.TextEditor`, `VariablePicker`, …) | `NodeParameterViewModel`, `NodeInspectorViewModel`, `EditorViewModel`, `ControlBarViewModel`, `WorkflowSettingsViewModel`, `AiModelManagerViewModel` (todos en el núcleo) | `AvaloniaWindowService` + `TextEditorDialogWindow`/`VariablePickerWindow` | **No registrado** → `NullWindowService`: los diálogos «se cierran sin confirmar» | Los parámetros multilínea y la inserción de variables no tienen UI: el botón existe y no hace nada |
| `IPopupMenuService` (`PopupMenuDescriptor`) | `NodeParameterViewModel` (el menú rápido de variables) | `AvaloniaPopupMenuService` | **No registrado** → `NullPopupMenuService` | El menú de variables no se abre |
| `IColorPickerService` | El editor de color (anotaciones / parámetros con color) | `ColorPickerService` | **No registrado** → el nulo que registra el núcleo (`NullColorPickerService`) | Los controles de color quedan inertes |
| `IFileDialogService` | Explorar rutas del inspector | `FileDialogService` | `UnoFileDialogService` ✅ (rebanada 4) | Ya resuelto: es el patrón a repetir |
| `VariablePickerViewModel` + `VariablePickerRequest` | El picker | La ventana lo envuelve | **Sin vista** | El VM portátil ya trae grupos, nodo objetivo y contexto de vista previa: la fase escribe VISTA |
| `IVariableDiscoveryService` | `TextEditorDialogViewModel`, el picker | (núcleo: `VariableDiscoveryService`) | (núcleo) ✅ | El dato ya viaja: no hay que inventar catálogo |
| `ServiceHolders.WindowService` / `.PopupMenu` | Todo el núcleo | Los fija `App.axaml.cs` | Sólo se fija `.FileDialog` ([`App.xaml.cs`](file:///FileFlow.App.Uno/App.xaml.cs)); el comentario del propio host declara que los otros dos quedan en Null | Fase 5.3: fijarlos aquí es literalmente la pieza que falta |

Lo que el ancla instalada cambia: `MainWindowOwner` es lo que usan las **acciones personalizadas de los
nodos**; mientras sea `null`, esas acciones caen a su no-op seguro (se declara, no se finge).

### 2.3 Empaquetado: cómo se ejecuta y cómo se publica hoy

| Pieza | Estado medido | Consecuencia |
| :--- | :--- | :--- |
| `FileFlow.App.Uno.csproj` | `net10.0-windows10.0.19041.0`, `RuntimeIdentifiers win-x64`, `Uno.Sdk/6.7.30`, `UnoSingleProject`, `UseWinUI` | Un solo host de escritorio Windows x64; el resto de targets de Uno no se usan |
| Tipo de paquete | **`WindowsPackageType=None`** (desempaquetado) y `WindowsAppSDKSelfContained=true` | La app arranca desde su carpeta sin instalar el runtime de WinAppSDK (y sin identidad de paquete) |
| `Package.appxmanifest` | `Identity FileFlow.Studio.Uno` / `Publisher CN=FileFlow` / `Version 0.1.0.0`; los logos de tile apuntan a `assets\FileFlow.ico` y **no hay `Assets/` en el proyecto** (el `.ico` vive en `assets/` de la raíz, con `generate-icon.ps1`) | El manifest **no se usa hoy** (desempaquetado) y **no validaría** para MSIX: un `.ico` no es un tile, faltan los PNG por tamaño (44/150/…) y un Publisher real |
| Compilación | `run-uno.ps1` → **MSBuild de Visual Studio** (los targets de WinAppSDK no corren con `dotnet build`); `run-uno-fast.ps1` sin compilar | La entrega no puede depender de Visual Studio instalado en la máquina del usuario |
| Publicación | **Sin perfiles de publicación** en el host Uno; el escritorio tiene `publish-all.ps1` / `publish-optimized.ps1` → `dist/` | La fase 5.5 escribe el contrato del host: `pack-uno.ps1` |
| CI | [`ci.yml`](file:///.github/workflows/ci.yml) declara **.NET 9.0.x** y hace `dotnet build/test FileFlow.slnx`; el slnx **incluye el host Uno**, que targeta `net10.0-windows…` y cuyos targets de WinAppSDK no corren con `dotnet build` | Medido el desajuste: la CI que hay no puede estar cubriendo al host Uno (a verificar con una corrida en 5.5) |
| Release | [`release.yml`](file:///.github/workflows/release.yml) usa .NET 10.0.x, Inno Setup y un zip portable del **escritorio** | No hay artefacto del host Uno |
| Versión | `.build_number` (6310) y el manifest (`0.1.0.0`) **no se alimentan entre sí**; las notas de versión citan «compilación NNNN» | La entrega necesita una sola fuente de versión |
| Avisos silenciados | `<NoWarn>NU1903;NU1902</NoWarn>` (vulnerabilidades de NuGet) | Deuda declarada: revisar en la fase de entrega, no tapar |

## 3. El contrato (lo único que ata las dos mitades)

**No se extrae ninguna interfaz nueva.** El host Uno escribe vistas y adaptadores sobre contratos que ya
existen y ya son portables: `IWindowService` + `IPopupMenuService` + `IColorPickerService` (SDK),
`VariablePickerViewModel` + `VariablePickerRequest` + `IVariableDiscoveryService` (núcleo) y
`ServiceHolders` (el ancla de arranque). La regla escrita de las rebanadas 3 y 4 sigue vigente: si falta
lógica, se añade al VM portable — nunca al host.

La única pieza NUEVA de contrato que este plan admite es de diagnóstico, no de producto: la sonda que
compara **el área de clic con la geometría dibujada** (§5.1), porque el defecto del 247 es de una clase
que ninguna prueba de unidad veía.

## 4. Las opciones, con su coste escrito

### Opción A — Diálogos con `ContentDialog` de WinUI + `MenuFlyout` para el menú ✅
- El modal nativo del host: foco, teclado (Escape/Enter) y accesibilidad salen gratis, y el árbol UIA lo
  expone sin trabajo extra (la lección del 238: lo que no tiene peer no materializa).
- Coste: `ContentDialog` exige `XamlRoot` — hay que inyectar el árbol correcto en la app desempaquetada
  (medir en la fase; es la trampa conocida de WinUI 3 sin identidad de paquete).
- El menú de variables es un `MenuFlyout` anclado al control del parámetro, ejecutando los `ICommand`
  portables del descriptor tal cual.

### Opción B — Paneles propios dentro de la escena (al estilo del spotlight)
- Reutiliza el patrón que ya funciona en el lienzo (overlay + caja de texto + lista).
- Coste: reimplementar foco, navegación por teclado, Escape y accesibilidad que `ContentDialog` da
  hechos; y dos lenguajes visuales de diálogo en el mismo host.

### Opción C — Portar las ventanas de Avalonia a WinUI una a una
- Coste real: son ventanas con su propio XAML y su propio ciclo de vida (pickers, configurador de temas,
  dashboard, explorador…); portar todas multiplica el trabajo y arrastra piezas que esta rebanada no
  necesita (dashboard, VFS, actualizador).

**Decisión: A**, con el menú como `MenuFlyout`, y **C** acotada a lo que el propio diagrama de diálogo
pida (`TextEditor`, `VariablePicker`) — el resto de claves se declaran pendientes con su no-op.

## 5. Las fases (rebanadas dentro de la rebanada)

### Fase 5.1 — El hit-test del lienzo: arreglado, atrapado y medido

> **Estado al cierre de los hitos 249 y 250: HECHA, y el criterio de salida cumplido en su mitad de puntero.**
> El cruce de espacio vive en `PointInHostSpace` (un solo sitio, usado por `CardAt` y
> `HitsInteractiveControl`), la sonda `ProbeHitAreas` informa `3/3` con el lienzo en `(280,42)` de la
> raíz (cayó en rojo con la medida exacta del defecto antes de arreglarlo: `0/3`, «resolvió OTRA tarjeta
> (#2)»), la guardia `UnoHitTestSpaceGuardTests` (4 casos) está en verde y la mutación
> `hit-test-en-el-espacio-equivocado` **MUERDE** (el plan la llamaba `hit-test-en-el-espacio-equivocado` y
> citaba la sonda como testigo: no puede serlo —el andamiaje mide con `dotnet test` y la sonda corre
> dentro de la app—, así que el testigo es la guardia del censo).
>
> **La re-sesión con puntero real del hito 250 cerró el criterio en lo que el puntero puede probar**: el
> clic reparte a la tarjeta clicada en las tres (`sel0 3→1154`, `sel1 4→1453`, `sel2 6→1454`), el arrastre
> por la cara mueve esa tarjeta y el doble clic en el fondo abre el buscador. Y destapó lo que el guion
> no podía ver sin dedos de verdad: **los atajos del lienzo (`Ctrl+Z`, `Ctrl+Y`, `Supr`) no llegan en un
> flujo que empieza con el puntero** — entraron en la fase 5.2 como defecto a arreglar y **quedaron
> RESUELTOS Y CERTIFICADOS en el hito 252** (el teclado se resuelve por burbujeo, sin depender del foco:
> `Supr` borra, `Ctrl+Z` restaura, `Ctrl+Y` rehace y `F2` abre el renombrado, medido con puntero real).
> ([`docs/qa/qa_manual_gestos_250.md`](file:///docs/qa/qa_manual_gestos_250.md),
> [`docs/qa/qa_manual_gestos_252.md`](file:///docs/qa/qa_manual_gestos_252.md))
- **Medir el espacio que `FindElementsInHostCoordinates` espera** (una línea, pero se decide con el
  experimento del 247, no con la memoria): el punto pasa a tomarse en el espacio de la ventana
  (`XamlRoot`/`TransformToVisual(null)`) y `CardAt` y `HitsInteractiveControl` comparten UN helper.
- **Sonda `ProbeHitAreas` en el selfcheck**: para cada tarjeta, comparar `CardAt(centro de su caja
  DIBUJADA)` con la propia tarjeta, tomando la caja dibujada del árbol visual. Es la guardia que este
  entorno SÍ puede correr (el puntero no se puede inyectar): **hoy falla, tras el arreglo mide
  desplazamiento 0** en las tres tarjetas.
- **Guardia `UnoHitTestSpaceGuardTests`**: censo de cada llamada a hit-testing del control con el espacio
  declarado en una tabla (la lección del 217: ningún cruce de puntos hecho a mano), y que el helper sea
  único.
- **Mutación `hit-test-en-el-espacio-equivocado`**: devolver el punto al espacio relativo al lienzo → el
  testigo rojo es la guardia del censo del espacio (`EveryHitTestCall_ShouldCrossToTheHostSpace…`), no la
  sonda: el andamiaje sólo puede morder con `dotnet test`. Control: la prueba hermana de la guardia, que
  audita la sonda y no depende del argumento mutado.
- **Criterio de salida**: sonda con desplazamiento 0; clic real sobre la cara de cada tarjeta selecciona
  ESA tarjeta (medido con el instrumento del 247); arrastre, doble clic y guardia de la barra de zoom
  dejan de estar contaminados (comprobado por el mismo protocolo de predicción que funcionó en el 247).

### Fase 5.2 — El canal del puntero real: las 24 interacciones, certificadas o declaradas
- **El instrumento se amplía, no se reescribe**: modo de guion por pasos (el operador sigue la lista
  numerada; el instrumento marca la ventana de cada paso y mide la señal esperada) + las métricas que
  faltan: caja dibujada vs clic, rectángulo del rubber band, cable pendiente y resaltado de socket.
- **La lista de trabajo ya existe**: las **24** interacciones de la tabla de paridad del hito 234 (contadas en el fichero, no estimadas)
  ([`UnoInteractionParityGuardTests`](file:///FileFlow.Tests/Unit/App/UnoInteractionParityGuardTests.cs))
  pasan a ser la lista de la sesión, con columna **certificado / declarado** en el informe.
- **Las dos dudas a resolver** (no a tapar): el espacio del rubber band (`UpdateRubberSelection`) y la
  diana del socket (~12 px). **Estado tras el 250**: el rubber band está MEDIDO — con la transform
  identidad acierta (medio + derecha al envolverlas) y con `translate=(154,322)` seleccionó **2 de las 3**
  envueltas, que es el síntoma del desfase de espacios; la diana del socket está medida **dos veces**
  (sesgo del operador de ~100–160 px bajo la barra) y con un efecto colateral nuevo: fallar el clic
  derecho **arranca un pan** porque la condición del pan no excluye tarjetas, contra su propio comentario.
- **RESUELTO en el hito 252 — los atajos del lienzo no llegaban con un flujo que empieza con el puntero**:
  focalizar el `UserControl` (el primer intento, dentro del mismo hito) **no bastó**: el rastro con puntero real mostró que
  el clic sí entregaba el foco y que ~0,5 s después un `ScrollViewer` desprendido se lo llevaba. El arreglo
  definitivo quita el foco del contrato —el teclado se resuelve por **burbujeo**, como en el escritorio: la
  raíz de `MainWindow` enruta al lienzo lo que nadie consumió, con el respeto por `e.Handled` y la cortesía
  del cuadro de texto dentro del resolver `TryHandleShortcutKey`— y está **certificado con puntero real**
  (sesión 252: `Supr` borra `nglobal 3→2`, `Ctrl+Z` restaura `2→3`, `Ctrl+Y` rehace y `F2` abre la caja),
  con sonda (`ProbeShortcutResolution`, selfcheck 78 OK), guardia (`UnoCanvasKeyboardGuardTests`) y
  mutación (`atajo-que-no-llega-sin-foco`). Los pasos 3.2.4/3.2.5/3.2.9 pasan de «declarado» a
  **certificado**.
- **El ladrón, identificado en el hito 253**: NO es del producto — es un envoltorio de scroll de la
  **plantilla de ventana del framework** (sin nombre, sin `DataContext`, del tamaño del área de
  contenido, fuera del árbol visual; medido con el fondo del lienzo y con una tarjeta, 141 ms y 78 ms
  después del clic). Lo que sí era del producto —el lienzo perdiendo el teclado con cada clic— se
  arregla con una **reclamación acotada**: el clic declara el teclado suyo 700 ms y el lienzo lo
  recupera en el tick siguiente si un dueño ajeno se lo lleva, con cuatro guardias (ventana del clic,
  cuadro de texto, subárbol propio y paneles del editor). Sonda `ProbeKeyboardReclaim` (80 OK en el
  selfcheck), guardia de 6 casos y mutación `reclamacion-que-roba-al-cuadro-de-texto` que muerde;
  **certificado con puntero real** en la sesión 257 (`foco RECUPERADO`, `Supr` borra, `Ctrl+Z`
  restaura y el buscador del cajón conserva sus letras).
- **RESUELTO en el hito 254 — el cable no tocaba sus sockets y su forma no era de cable**: reportado desde
  la app («al mover o ajustar el zoom las líneas de conexión se desplazan quedando fuera de su sitio» y
  después «al mover un nodo la parte recta es demasiado grande… una forma como de Z»). La sonda
  `ProbeWireTracking` midió el extremo dibujado contra el socket real en la raíz: **45,0 px** con el plano
  quieto, **0,0** tras un pan y **6,1** tras el zoom —con la tarjeta inmóvil y el ancla medida corriéndose
  `542→538`, que delata la escala olvidada en `TransformToVisualCenter`—. La geometría compartida dibuja
  ahora **una Bézier que nace y muere en las anclas** (sin tramos rectos: los bajíos del control eran los
  palos de la Z) con el cuello acotado por el hueco (`min(100 + √(25 · ancho), ancho/2)`); medida del ancla
  corregida (y con ella el hit-testing del lienzo), guardia `UnoCanvasWireGuardTests`, 9 casos nuevos de
  comportamiento en `ConnectionGeometryTests` y tres mutaciones que muerden. **Certificado con puntero real**
  (sesiones 258-260): «*ya parece un cable: sin Z y sin bajío*», con un único tramo de cable por columna
  medido en el hueco. Frontera declarada —y **cerrada en el hito 266**: el **escritorio** ya dibuja con
  `ConnectionGeometry` (`FlowConnection`, el control del host sobre el trazador del núcleo), con su guardia y
  su mutación, en vez del `Connection` de Nodify.
- **Gestos que quedaron fuera y entran aquí**: el arrastre desde el cajón (3.4), el arrastre de notas y
  grupos, el pan con botón derecho y el zoom con la rueda.
- **Protocolo de la sesión** (la lección del 247, escrita para no repetir sus tres tomas perdidas):
  1) ventana en primer plano comprobada antes de medir; 2) predicción escrita ANTES del gesto; 3) un
   paso por ronda cuando el estado importa; 4) el veredicto del operador y la medición, los dos, en el
  informe; 5) no conectar un cliente UIA mientras el contenido de snapshots está materializado (frontera
  LATENTE del 245).
- **Criterio de salida**: informe en `docs/qa/` con las 24 interacciones y su evidencia — certificadas
  por medición o declaradas con su motivo, ninguna fingida.

### Fase 5.3 — Los pickers de variables y los diálogos de nodo
- **Los adaptadores**: `UnoWindowService : IWindowService` (resuelve la ventana por sí mismo,
  `MainWindowOwner` real, `ShowDialogAsync` por `DialogKeys` con `ContentDialog` y `DialogResultPayload`,
  y las claves no implementadas declaradas) y `UnoPopupMenuService : IPopupMenuService` (`MenuFlyout`
  anclado al control del parámetro, ejecutando los comandos del descriptor).
- **Las vistas**: el editor multilínea (`DialogKeys.TextEditor`) con su botón de insertar variable, y el
  picker (`DialogKeys.VariablePicker`) sobre el `VariablePickerViewModel` del núcleo, que ya trae grupos,
  nodo objetivo y contexto de vista previa; el token elegido vuelve en `DialogResultPayload.Value` y el
  parámetro lo escribe por su vía de producción.
- **El ancla**: `App.xaml.cs` fija `ServiceHolders.WindowService` y `.PopupMenu` junto al `.FileDialog`
  que ya fija (la línea que hoy falta, con su comentario de declarado actualizado).
- **El color**: `IColorPickerService` en el host (o su declaración con motivo) — es el mismo patrón.
- **Guardias**: `UnoDialogHostGuardTests` (el host registra e instala los dos servicios; las claves son
  las constantes de `DialogKeys`, no literales; el picker cita el VM del núcleo y el servicio de
  descubrimiento; el resultado viaja en `DialogResultPayload`).
- **Mutación `dialogo-que-no-devuelve-el-token`**: descartar `Value` en el resultado → el parámetro no
  recibe la variable (testigo rojo en el VM/DI; control: el resto del diálogo).
- **Criterio de salida**: en el host Uno, editar un parámetro multilínea e insertarle una variable del
  flujo, medido por sonda (valor del parámetro antes/después) y con el árbol expuesto para las anclas.

> **Cumplido en el hito 258, con tres enmiendas de nombre y alcance**: la guardia se llama
> `UnoNodeDialogsGuardTests` (no `UnoDialogHostGuardTests`) y tiene **9 casos**; el mutante
> `dialogo-que-no-devuelve-el-token` se convirtió en **cuatro** (`panel-de-nodo-sin-su-servicio-de-ventanas`,
> `fila-de-variables-que-abre-el-menu-que-no-esta-portado`, `editor-que-no-devuelve-el-texto-confirmado` y
> `campo-que-no-muestra-lo-que-el-dialogo-escribio`), los cuatro mordiendo; y `UnoPopupMenuService` y
> `IColorPickerService` **no** entraron: el host no tiene menú emergente, así que su «{x}» abre **directo el
> catálogo completo** (la primera entrada del menú del escritorio) y la ausencia queda **declarada** en vez de
> fingida. El criterio de salida se midió por sonda (`--selfcheck-dialogs`, 24 OK: el token insertado y el
> texto guardado **leídos del parámetro del nodo**) y también con la aplicación abierta, 25 de 25 pasos.
> Evidencia: [`docs/qa/qa_dialogs_host_258.md`](file:///docs/qa/qa_dialogs_host_258.md).

### Fase 5.4 — La observación UIA de los pickers
- AIDs desde el primer commit (diálogo, lista, fila, botón de confirmar) y anclas nombradas como
  recursos — el patrón del 238/245.
- Sondas nuevas en [`selfcheck_uia_probe.py`](file:///docs/qa/selfcheck_uia_probe.py): abrir el picker
  por teclado desde el editor del parámetro, leer sus filas, confirmar y ver el token en el editor.
- **Límite declarado**: el contenido de snapshots sigue reservado (frontera LATENTE del 245); el sondeo
  no lo conmuta.
- **Criterio de salida**: `--selfcheck-uia` EXIT 0 con las sondas nuevas y la frontera re-declarada.

### Fase 5.5 — Empaquetado y entrega
- **5.5.1 `pack-uno.ps1`** (el contrato, hermano de `publish-all.ps1`): publicación x64 a `dist/uno-win-x64`
  (desempaquetado, `WindowsAppSDKSelfContained` para no exigir runtime), zip portable y versión tomada de
  `.build_number` — **una sola fuente de versión**, que es lo que hoy no existe.
- **5.5.2 MSIX (decisión con su coste)**: assets reales por tamaño (extender `assets/generate-icon.ps1`),
  Publisher de verdad, firma con certificado **autofirmado para sideload** (documentado como camino de
  prueba, con el aviso de SmartScreen que eso implica, no disimulado), `WindowsPackageType=MSIX` y la
  versión del manifest derivada del build number. **Antes de conmutar**: medir el selfcheck bajo la
  identidad de paquete (rutas, cwd, permisos cambian) y mantener el camino desempaquetado como el de
  desarrollo.
- **5.5.3 CI**: corregir el desajuste medido (SDK 10.0.x) y añadir el trabajo del host Uno con **MSBuild
  de Visual Studio** (disponible en `windows-latest`); medir si el selfcheck puede correr en el runner
  (ventana + sesión) y, si no, declararlo: guardias y suite en CI, sondas en local/pre-release.
- **5.5.4 Release**: `release.yml` publica el artefacto del host Uno (zip y, si 5.5.2 sale, el MSIX) con
  la misma versión que las notas.
- **5.5.5 Deuda declarada**: revisar `NU1903;NU1902` silenciados y documentar requisitos reales
  (Windows 10 19041+ x64) en `AGENTS.md` (comandos nuevos) y en `setup_and_deployment.md` (sección del
  host Uno, que hoy no existe).
- **Criterio de salida**: un artefacto que arranca en una máquina Windows x64 limpia **sin instalar .NET
  ni el runtime de WinAppSDK**, con el selfcheck en verde; CI que compila y prueba todo, host Uno incluido.

### Fase 5.6 — Cierre de la rebanada
- Selfcheck EXIT 0 con las sondas nuevas; suite al 100%; mutaciones mordiendo y `COVERAGE.md` regenerado
  por su guardia; walkthrough, `session_summary` y (por ser tramo visible) `notas_de_version`.
- Los pendientes de las rebanadas 3 y 4 que esta rebanada cierra se marcan en sus planes: el guion manual
  del lienzo (3.2) y los «pickers de variables» de los paneles (4.x).

## 6. Los umbrales

- **Ninguna lógica duplicada**: catálogo de variables, filtrado y write-back viven en el núcleo; el host
  escribe vistas y adaptadores.
- **Localización en caliente**: todo texto nuevo en `App.xaml` (es/en) por claves `Uno_*`; los diálogos
  no llevan cadenas incrustadas.
- **Observabilidad desde el primer commit**: lo que deba observarse nace con AID y con peer; lo que no
  materialice se declara (la lección del 238).
- **Certificación con dos firmas**: el veredicto del operador y la medición del instrumento; una sin la
  otra no es evidencia.
- **Lo que no llegue queda DECLARADO**, con su medición, nunca fingido.

## 7. Riesgos, con su mitigación

| Riesgo | Mitigación |
| :--- | :--- |
| El cambio a MSIX altera la identidad del proceso (rutas, cwd, permisos) y rompe el selfcheck en silencio | Medir el selfcheck bajo la identidad de paquete ANTES de conmutar; camino desempaquetado como el de desarrollo |
| La firma autofirmada no vale para distribución y SmartScreen avisa | Documentarlo como camino de pruebación/sideload y no venderlo como entrega firmada; la firma real es una decisión aparte |
| La sesión de gestos depende de una persona (el puntero no se puede inyectar, medido) | Protocolo del 247 (predicción escrita, una cosa por ronda, ventana en primer plano comprobada); el guion es re-ejecutable sin código nuevo |
| Un cliente UIA conectado con el contenido de snapshots en pie tumba el proceso (frontera LATENTE del 245) | El sondeo no conmuta esa pestaña; la frontera se re-declara en cada informe |
| La CI no puede garantizar una ventana para el selfcheck | Guardias estáticas y suite en CI; sondas en local/pre-release, declarado; si el runner lo permite, se mide y se añade |
| Tres temas en una rebanada es mucho | El orden es por dependencia: el hit-test primero (sin él nada se certifica), pickers después, paquete al final — un corte a mitad sigue dejando valor |

## 8. Criterio de éxito de la rebanada

1. **Los gestos**: las 24 interacciones de la tabla de paridad certificadas con puntero real (o
   declaradas con su medición), con el hit-test arreglado y atrapado por una sonda que corre en cada
   selfcheck.
2. **Los pickers**: en el host Uno se edita un parámetro multilínea y se le inserta una variable del
   flujo, por los servicios del SDK y el VM del núcleo, sin lógica duplicada y con el árbol observable.
3. **El paquete**: un artefacto que arranca en una máquina limpia sin instalar nada más, con la versión
   atada al build number y la CI compilando y probando también el host Uno.

## 9. Lo que este plan NO hace

- No rediseña el lienzo ni los paneles: arregla el hit-test y certifica, no re-arquitectura.
- No toca el host Avalonia (salvo lo que el núcleo comparta, con sus pruebas).
- No porta las ventanas que no pide el diagrama de diálogo (dashboard, VFS, actualizador): se declaran
  pendientes. **Enmienda del hito 255**: la de **ajustes / apariencia e idioma** SÍ entró —es la mitad visible
  del encargo de la migración y su sección de apariencia hace de configurador de tema e idioma—; su **Theme
  Studio** (crear y editar temas) sigue fuera y se declara pendiente.
- No publica en Microsoft Store ni gestiona certificados de producción.
- No añade features de nodos ni cambia el formato de flujo.
- No convierte el selfcheck en una suite de UI automatizada: el puntero real es humano por medición.

## 10. Estado de ejecución

- **5.1 — Medida, NO arreglada.** El hito 247 ejecutó la sesión, cerró el canal del puntero real y midió
  el defecto de hit-test `(280, 41)` con predicción verificada fuera de muestra
  ([`docs/qa/qa_manual_gestos_247.md`](file:///docs/qa/qa_manual_gestos_247.md)). El arreglo, la sonda
  `ProbeHitAreas`, la guardia y la mutación están por hacer.
- **5.2 — Con el instrumento listo** ([`docs/qa/qa_manual_session.py`](file:///docs/qa/qa_manual_session.py))
  y el protocolo ya escrito en el informe del 247; el guion completo sin ejecutar.
- **5.3 — PARCIAL, y su mitad con paridad ya está PROBADA (hito 255).** Lo que cierra este tramo es la
  **superficie de AJUSTES / apariencia e idioma** del host: cuatro secciones sobre el
  `WorkflowSettingsViewModel` portable, persistencia por sus comandos canónicos, **diccionario propio del host
  en dos idiomas** (las claves `Uno_*`) y un arranque que aplica **lo guardado** (tema e idioma) antes de
  activar la ventana. Medido en su superficie real, con la aplicación abierta: cambiar tema e idioma por los
  controles reales y guardar escribe la preferencia, el marco cambia en caliente y **las dos preferencias
  sobreviven al cierre** (fotograma base de la reapertura: 0 % claro con el tema oscuro guardado, 97 % con el
  claro; el texto del marco leído por UIA sigue al idioma guardado). Sonda propia (`--selfcheck-settings`, 9 OK),
  guardia de 12 casos (censo de 21 controles con su camino hasta la preferencia) y tres mutaciones que muerden.
  **Lo que sigue faltando de la fase** (a fecha de este tramo): los **pickers de variables** y los **diálogos
  de nodo** —**portados después, en el hito 258**: ver el boletín de más abajo—, y las pestañas
  **Actualizaciones** y **Modelos de IA** de la propia ventana de ajustes (el VM portable las trae; la vista
  del host no). Evidencia:
  [`docs/qa/qa_ajustes_host_255.md`](file:///docs/qa/qa_ajustes_host_255.md).
  - **Re-verificado en la sesión 267, esta vez con el tema medido en píxeles**: el instrumento ganó el renglón
    `METRIC top_colors` (el color que más superficie ocupa de la ventana = el fondo del lienzo) y con él el
    recorrido queda cerrado sin lecturas internas: `#FFF8FA` 73,1 % al arrancar con `pastel_spring` → `#10131B`
    72,9 % tras cambiarlo en el desplegable real y guardar → **`#10131B` 73,1 % al reabrir** (y el botón del
    marco leyendo «Settings»), con las preferencias del usuario restauradas después (`#FFF8FA` 73,1 %).
    También quedó precisado que **el tema y el idioma se aplican al guardar** (no en vivo) y que **Cancelar deja
    la aplicación como estaba**, la misma semántica que la ventana del escritorio. Los seis defectos que el
    playtest destapó eran **del instrumento** (driver e instrumento), no del producto, y están arreglados: tema
    legible en píxeles, driver que ya no muere con los emoji de los temas, presencia del panel por sus anclas
    (un `Border` no tiene peer de automatización), `SIN_LECTURA` en vez de un mensaje que culpaba a la búsqueda,
    índice del árbol fresco tras actuar y patrones por `iface_*` (el respaldo de comtypes llamaba a una función
    que no existe y un `except` ancho lo tragaba). Evidencia: §8 del mismo informe y `docs/qa/qa-manual-267/`.
- **El MENÚ PRINCIPAL del host — PORTADO Y PROBADO (hito 257).** Este tramo no estaba en las fases de
  arriba (el hueco lo señaló la memoria de la sesión, no este plan), así que se anota aquí para que no se
  pierda. La barra de control del escritorio y su cajón viven ya en el host sobre el **mismo
  `ControlBarViewModel` portable**: 14 entradas ancladas, órdenes despachadas por el núcleo con su
  `CanExecute`, estado por contexto por enlace, los 30 textos **copiados** del diccionario del escritorio y
  sonda propia (`--selfcheck-controlbar`, 14 OK), guardia de 8 casos y cuatro mutaciones que muerden.
  Evidencia: [`docs/qa/qa_menu_host_257.md`](file:///docs/qa/qa_menu_host_257.md).
  - **Lo que sigue faltando de él, declarado en el control y atado por la guardia**: las **once entradas** que
    exigen ventanas que el host no tiene (Estudio de temas, Métricas, VFS, Diseñador de dataset, Manual,
    Ejemplos, Acerca de y el aviso de actualización) o un **diálogo síncrono** de fichero/confirmación
    (Nuevo, Cargar, Guardar — la misma frontera de 5.3), y los **seis atajos** de ciclo y de flujo del
    escritorio (F5, F10, Shift+F5, Ctrl+N, Ctrl+O, Ctrl+S): el host sólo enruta los atajos del lienzo.
  - **CERRADO en su mayor parte en el hito 259** (`docs/qa/qa_menu_host_259.md`): las **tres de flujo**
    (Nuevo / Cargar / Guardar) se cumplen por el **canal asíncrono del host** —confirmación y pickers
    asíncronos + los métodos portables `CreateNewWorkflow` / `LoadWorkflowFromFileAsync` /
    `SaveWorkflowToFileAsync`, que separan el diálogo de la operación—, las **tres de ayuda** (Manual,
    Ejemplos, Acerca de) ejecutan su orden canónica (con «Acerca de» como superficie real del host) y los
    **seis atajos** están enrutados por la tabla `RoutedShortcuts`, que es la que despacha. **Siguen
    pendientes cinco entradas** con su razón escrita: **Estudio de temas, Métricas, VFS, Diseñador de
    dataset y el aviso de actualización** (este host no comprueba actualizaciones).
- **Los PANELES DE NODO del host — PORTADOS Y PROBADOS (hito 258).** La fase 5.3 cierra su mitad de
  parámetros: `UnoWindowService` sirve **2** de las **9** claves de `DialogKeys` (`TextEditor`,
  `VariablePicker`) sobre los **view models portables** del núcleo, las **7** restantes quedan declaradas con
  su razón y con traza (`DeclinedDialogs`), y el inspector ancla su servicio en
  `ServiceHolders.WindowService` (sin ese anclaje el contenedor lo tiene y las filas siguen en el Nulo: es lo
  que declara la 68.ª mutación). Sonda propia (`--selfcheck-dialogs`, **24 OK**: catálogo poblado con 47
  variables, filtro en caliente, token insertado **leído del parámetro**, editor sembrado con el valor y texto
  guardado), guardia de 9 casos y cuatro mutaciones que muerden. **Un defecto real encontrado y arreglado**
  usándolo: las cajas de las filas sólo escribían en un sentido, así que el valor que escribía el diálogo
  **no volvía al campo**. Evidencia: [`docs/qa/qa_dialogs_host_258.md`](file:///docs/qa/qa_dialogs_host_258.md).
  - **Lo que sigue faltando de la fase**: `UnoPopupMenuService` (se decidió **no** portarlo: el «{x}» abre el
    catálogo completo y la diferencia queda declarada), `IColorPickerService`, el **gestor de presets de
    medios** y el de **contraseñas** (ventanas que el host no tiene), y las pestañas **Actualizaciones** y
    **Modelos de IA** de la propia ventana de ajustes.
- **Las VENTANAS del menú del host — PORTADAS Y PROBADAS (hito 260).** De las **cinco entradas de ventana**
  que el hito 259 dejó pendientes, **cuatro ya se sirven** por el catálogo de diálogos sobre los **view models
  portables del núcleo** (cero lógica de producto en la vista): **Estudio de Temas** (`ThemeCustomizerBody`, con
  **9 secciones / 34 ajustes editables** y selector por **tipo** de fila), **Métricas**
  (`MetricsDashboardBody`, 4 tarjetas y 7 columnas), **Explorador VFS**
  (`VirtualFileSystemExplorerBody`, construido por el host con el almacén de la carga útil, como el
  `AvaloniaWindowService` del escritorio) y **aviso de actualización** (`UpdateDialogBody`), y para que el
  distintivo no fuese decorado entra la **comprobación de actualizaciones del arranque** (la del escritorio,
  saltada en los modos de sonda). El censo del servicio pasa de **3 servidas + 6 declaradas** a **7 + 2**; la
  barra gana el **chip VFS** y el **distintivo de actualización** con su estado de contexto; el cajón pasa de 11
  a **14 entradas** y las **192 claves** de texto se copian del diccionario del escritorio en EN+ES. Sonda
  `--selfcheck-controlbar` de **25 → 37 OK**, guardia de **9 → 12 casos** y **cuatro mutaciones nuevas que
  muerden** (más una reapuntada; **75 declaraciones**). **Una debilidad de la guardia la encontró una mutación**
  (buscaba las órdenes dentro de la tabla que las declara) y se endureció con `WithoutDeclarationTables`.
  Evidencia: [`docs/qa/qa_windows_host_260.md`](file:///docs/qa/qa_windows_host_260.md).
  - **Lo que sigue faltando**: el **Diseñador de dataset** (su ventana la monta el propio plugin con su juego
    de herramientas: servirla pide una vista del host sobre lógica de su ensamblado) y, en el **Estudio de
    temas**, **Eliminar / Importar / Exportar** (contrato **síncrono** de diálogos) y la **vista previa en
    vivo** (necesitaría su propia copia de los tokens del lienzo) — todo declarado en `DeclaredPendingParts` y
    atado por la guardia. Siguen también los gestores de **presets de medios** y **contraseñas** y las pestañas
    **Actualizaciones** y **Modelos de IA** de la ventana de ajustes.
- **El DISEÑADOR DE DATASETS y las DOS PESTAÑAS de ajustes — PORTADOS Y PROBADOS (hito 261).** Las dos
  superficies que seguían sin servir, cerradas. (1) El **Diseñador de Datasets** era la última **entrada de
  menú** declarada del censo, y su frontera escrita («esa ventana la monta el propio plugin con el toolkit del
  escritorio; un host WinUI no puede montar una ventana ajena») se cruza con un **contrato NUEVO del SDK**:
  `INodeDialogSurfaceProvider` deja que el **nodo** declare **qué** diálogo quiere (`DialogKey`, la clave del
  catálogo compartido) y **qué** contiene (`Payload`, su `SyntheticDataSetDesignerViewModel` portable). El host
  pregunta al nodo y sirve esa clave con **SU vista sobre el MISMO VM del plugin** (cero lógica de producto, y
  sin posibilidad de dos diseñadores vivos = dos verdades sobre los mismos datasets). (2) Las **pestañas de
  ajustes** que faltaban —**Modelos de IA** y **Actualizaciones**— se sirven sobre sus secciones portables; la
  superficie de ajustes pasa a **SEIS secciones**. El censo del servicio pasa a **9 servidas + 1 declarada**,
  el cajón a **15 entradas** y `DeclaredPendingEntries` queda **VACÍA** (ya no hay ninguna orden de menú del
  escritorio sin dibujar, declarar o cumplir por el host). Sondas: `--selfcheck-controlbar` **37 → 42 OK** y
  `--selfcheck-settings` **9 → 12 OK**, las dos VERIFICADO; guardias **13 + 13 + 9 + 5 = 40** casos
  (`UnoSettingsSurfaceGuardTests` gana un caso con el que nace de abajo); **cinco mutaciones nuevas y una
  reapuntada, las seis muerden** (**80 declaraciones**). Y **una razón declarada había quedado FALSA** —y la
  había encontrado el OJO—: `AiModelUrlsConfig` decía «pertenece a la pestaña de modelos de IA, que este host
  todavía no tiene» cuando este tramo **sí** le dio esa pestaña; se reescribió para decir lo que de verdad pasa
  **y las dos mitades quedaron atadas por una prueba** (`TheAiModelRowActions_ShouldMatchWhatTheDialogCensusDeclares`:
  la fila dibuja exactamente descargar y borrar **y** la clave sigue declarada con una razón que no puede decir
  que la pestaña no existe) **y por una mutación que muerde** (`accion-de-urls-por-modelo-sin-declarar`: dibuja
  la tercera acción sin tocar el censo, el modo de fallo exacto). Evidencia:
  [`docs/qa/qa_datasets_settings_271.md`](file:///docs/qa/qa_datasets_settings_271.md).
  - **Lo que sigue faltando**: el **punto de entrada de la configuración de URLs de modelos** —la pestaña de
    modelos de IA del host lista el catálogo y gestiona descargas, pero **no ofrece la edición de URLs por
    modelo desde la fila**, que es la acción con la que el escritorio abre `AiModelUrlsConfig`; sin ella,
    servir la ventana sería una ventana que nadie puede abrir, así que la clave sigue declarada con esa razón—;
    y, del **Estudio de temas**, **Eliminar / Importar / Exportar** (contrato **síncrono**) y la **vista previa
    en vivo**, declarados en `DeclaredPendingParts`. Siguen también los gestores de **presets de medios** y
    **contraseñas**.
- **El EDITOR DE URLs POR MODELO — PORTADO Y PROBADO (hito 262).** El único frente que quedaba abierto de
  las superficies se cerró: la pestaña de **Modelos de IA** ganó la **acción de URLs por fila**
  (`SettingsAiModelUrlsButton`, en el mismo puesto que en el escritorio) con su **rama propia** que ejecuta
  la orden **canónica** del gestor (`ConfigureUrlsCommand`), y el host **sirve** la clave con una vista del
  `AiModelUrlsConfigViewModel` portable. El censo pasa a **10 servidas + 1 declarada** (`WorkflowSettings`,
  que sigue siendo una decisión: su superficie tiene puerta en la barra y el cajón). **Dónde queda el
  cambio**: en el almacén del gestor del núcleo (`SetCustomUrls`), porque lo escribe el view model portable
  —el cuerpo no contiene `SetCustomUrls` y una prueba lo exige—. Sonda `--selfcheck-settings` de **12 → 18
  OK** (los seis pasos nuevos pulsan el botón real de la fila, escriben en la caja real y **restauran** la
  instantánea del usuario); **una mutación nueva muerde** (`accion-de-urls-que-descarga-el-modelo`: quitar la
  rama hace que pulsar «URLs» **descargue** el modelo) y **una anterior se retiró** por haber quedado
  obsoleta (**80 declaraciones**). Y **un defecto del PRODUCTO lo encontró la medición**: la caja enlazaba
  `Text` sin `UpdateSourceTrigger` y en WinUI escribía al perder el foco. Evidencia:
  [`docs/qa/qa_urls_host_262.md`](file:///docs/qa/qa_urls_host_262.md).
- **5.4, 5.5, 5.6 — Sin empezar.** (El desglose por trozos, con su tamaño, está en §11.)

## 11. Lo que queda de la migración, con su tamaño

Para poder atacar cada trozo sin volver a estudiar el terreno. El tamaño es de **trabajo medible** (qué se
construye y qué se mide), no de horas.

| Trozo | Qué es | Tamaño |
| :--- | :--- | :--- |
| **5.4 — Observación UIA de los pickers** | AIDs ya existen; sondas nuevas en `selfcheck_uia_probe.py` (abrir el picker por teclado, leer filas, confirmar y ver el token en el editor) + re-declarar la frontera del contenido de snapshots | **Pequeño** — 1 fichero de sondas y su informe; sin código de producto |
| **5.5.1 — `pack-uno.ps1`** | Publicación x64 self-contained a `dist/uno-win-x64`, zip portable y versión de **una sola fuente** (`.build_number`) | **Medio** — 1 script + 1 ejecución real + humo del artefacto |
| **5.5.2 — MSIX** | Assets por tamaño, Publisher real, firma **autofirmada para sideload** (con su aviso), `WindowsPackageType=MSIX` y versión del manifest del build number; **antes de conmutar**, medir el selfcheck bajo la identidad de paquete | **Grande** — el riesgo declarado del plan (cambian rutas, cwd y permisos) |
| **5.5.3 — CI** | Corregir el desajuste medido del SDK y añadir el trabajo del host Uno con **MSBuild de VS**; decidir si el selfcheck puede correr en el runner y declararlo si no | **Medio** — 1 workflow + 1 decisión medida |
| **5.5.4 — Release** | `release.yml` publica el artefacto del host Uno (zip y, si 5.5.2 sale, el MSIX) con la versión de las notas | **Pequeño** — depende de 5.5.1/5.5.2 |
| **5.5.5 — Deuda declarada** | Revisar `NU1903;NU1902` silenciados y documentar requisitos reales (Windows 10 19041+ x64) en `AGENTS.md` y en `setup_and_deployment.md` (sección del host Uno, que hoy no existe) | **Pequeño** — prosa + 1 decisión sobre los avisos |
| **5.6 — Cierre** | Selfcheck verde con las sondas nuevas, suite al 100%, mutaciones mordiendo, `COVERAGE.md` regenerado por su guardia, walkthrough/`session_summary`/`notas_de_version`, y marcar los pendientes que esta rebanada cierra (el guion manual del lienzo 3.2 y los «pickers de variables» 4.x) | **Pequeño** |
| ~~**Gestor de PRESETS DE MEDIOS**~~ | **CERRADO (hito 263)**: cuerpo sobre el view model portable del plugin, clave servida en el catálogo (**10 servidas + 1 declarada**), las **dos puertas** (tarjeta y fila), su conmutador de parámetros en la tarjeta, sonda (**33 OK** en diálogos y **88 OK** en lienzo), guardia (**11 de 11**) y mutaciones (**8 del tramo, las 8 muerden**). Ejercido en la app abierta: **34 de 34 pasos** | **Hecho** |
| ~~**Órdenes DESTRUCTIVAS del gestor de presets**~~ | **CERRADO (hito 264)**: la confirmación **asíncrona** del contrato del SDK (`IDialogService.ConfirmAsync`) y la **regla** en el view model portable; el host Uno pregunta **dentro del modal abierto** (WinUI sólo admite un `ContentDialog`), sin bloquear el hilo de UI, y las **dos puertas** comportan igual. Sonda de diálogos **46 OK** (antes 33), guardia **12 de 12**, **2 mutaciones nuevas que muerden** y sesión con la app abierta **42 de 42 pasos** | **Hecho** |
| ~~**Las otras seis órdenes destructivas del host**~~ | **CERRADO (hito 265)**: las seis esperan la respuesta real por `IDialogService.ConfirmAsync` —«Nuevo Flujo» (menú y `Ctrl+N`, con la ventana ejecutando la orden **canónica**), «Revertir Archivos», «Eliminar tema» del Estudio (**con su botón dibujado**, que antes no existía), «Limpiar VFS», «Eliminar modelo» y «Eliminar dataset»—. Dos defectos más, encontrados al revisar: el **diseñador de datasets nacía con el doble nulo** (borraba **en silencio en los dos hosts**: los diálogos del host viajan ahora por el `NodeCustomActionContext`) y un **evento muerto** que dejaba `Ctrl+N` sin el refresco del renglón del ciclo; y la pregunta del borrado de modelo, la única de las ocho **escrita en el código** (no se traducía), pasa al diccionario con una guardia que lo exige y un mutante que la muerde. Guardias **14 + 13 + 5**, **5 mutaciones nuevas que muerden**, `COVERAGE.md` **95 declaradas**, suite **1935 + 1**, y sesión con la app abierta **33 de 33 pasos** | **Hecho** |
| **`ClearVirtualFileSystemAsync` sin puerta** | La orden del **Explorador Virtual** pregunta ya por la vía buena, pero **ninguna vista dibuja su botón** (ni la del escritorio ni la de este host): hoy no hay quién la invoque. Dibujarla es UI nueva | **DECLARADO, no pendiente** (fuera del encargo de las seis) |
| **`DeleteModelAsync` no medido con el ratón** | Su borrado retira ficheros **reales** del disco (`%AppData%\FileFlow\models`, varios GB). Su determinación se mide en la suite, no en la app abierta | **DECLARADO** en el informe del 265 |
| **Gestor de CONTRASEÑAS** (ventana que el host no tiene) | Ídem, con la frontera de seguridad que traiga (¿clave del sistema? hay que decidirlo antes) | **Medio**, más la decisión |
| **`IPopupMenuService` y `IColorPickerService`** | Servicios que el host no tiene. Hoy el «{x}» abre el catálogo completo y el color se elige por tokens | **DECLARADO, no pendiente**: portarlos sin necesidad sería duplicar el escritorio |
| **Contrato SÍNCRONO de diálogos del núcleo** | La frontera que obligó al canal asíncrono del host (confirmaciones y pickers de fichero) | **DECLARADO**: no se cambió el núcleo; otro host tendrá la misma frontera |

**Censo de superficies de usuario del escritorio a fecha del hito 263** (cierre del **gestor de presets**):
**portadas y probadas** la barra y el cajón (**31 entradas**, pendientes **VACÍA**), **10 de 11** claves del
catálogo de diálogos, las **6 secciones** de ajustes, los paneles de nodo (inspector, editor de texto, catálogo
de variables y **el gestor de presets**), las **4 acciones de fila** y las siete ventanas del host.
**Sin portar, con razón**: el **gestor de CONTRASEÑAS** (la **única** superficie de usuario que queda; su fila está
declarada con ese motivo), el **menú emergente de variables** (el «{x}» abre el catálogo completo),
**`WorkflowSettings`** como diálogo (sería una **segunda copia** de los ajustes del host) y
`IPopupMenuService`/`IColorPickerService` (**declarado, no pendiente**).

**Fuera de las superficies de usuario queda sólo el empaquetado y la entrega**: las fases **5.4-5.6** de la tabla
de arriba —observación UIA de los pickers, `pack-uno.ps1`, **MSIX**, CI, release y la deuda declarada—. El
producto **no reparte** el host Uno: lo ejecuta quien lo compila.

**La frontera del producto que midió el hito 263 quedó CERRADA en el hito 264**: las órdenes **destructivas** del
gestor de presets ya **preguntan** por la vía asíncrona del contrato (`IDialogService.ConfirmAsync`), el host Uno
pregunta **dentro del modal abierto** (con su velo y sus dos botones, sin bloquear el hilo de UI) y las **dos
puertas** —la fila y la tarjeta— se comportan igual, con la respuesta real del usuario mandando. Evidencia:
[`docs/qa/qa_presets_confirm_263.md`](file:///docs/qa/qa_presets_confirm_263.md).

**Y las otras seis quedaron CERRADAS en el hito 265**: ninguna orden destructiva del producto pregunta ya por la
vía **síncrona** —una guardia **barre el árbol de fuentes entero** para que no vuelva a ocurrir—, «Eliminar tema»
tiene por fin **botón** en el Estudio, y el **contenido de las superficies declaradas viaja con los diálogos del
host** (sin ellos nacía con el doble nulo y el diseñador de datasets borraba **en silencio en los dos hosts**).
Evidencia: [`docs/qa/qa_destructive_orders_265.md`](file:///docs/qa/qa_destructive_orders_265.md). **La frontera
SÍNCRONA del contrato de diálogos del núcleo sigue declarada** (no se cambió el núcleo: otro host tendrá la misma
frontera y cuenta con el canal asíncrono del host y con `ConfirmAsync` por defecto).
