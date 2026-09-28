# Guion manual de gestos 3.2/3.3 con PUNTERO REAL — host Uno (hito 247, 2026-09-27)

**Veredicto: EJECUTADA. La frontera del hito 231 queda CERRADA por un lado y AMPLIADA por otro.**

1. **El canal del puntero real funciona**: con el ratón del operador los gestos llegan al producto — la
   tarjeta muestra y pierde el borde de selección, medido en píxeles (`sel 5 → 1461 px`). Lo que el 231
   no pudo demostrar (sin UIAccess el puntero inyectado no llega) queda demostrado aquí por la vía
   humana, que es la que el guion reserva.
2. **El puntero INYECTADO sigue sin llegar**, re-medido hoy sobre escena calibrada y con una métrica que
   ya no es ciega (delta 0 px en acento, en borde de selección y en diferencia general de píxeles).
3. **La sesión encontró un defecto de producto que bloquea la mitad del guion**: el *hit-test* de las
   tarjetas del lienzo resuelve **desplazado respecto a lo que se ve** — clicar la cara de una tarjeta
   selecciona OTRA tarjeta (o ninguna, y entonces deselecciona todo). Está medido, con predicción
   verificada fuera de muestra y con el sitio del código localizado (§4).

## 1. El instrumento de la sesión

[`qa_manual_session.py`](file:///docs/qa/qa_manual_session.py) — instrumento de la sesión manual. **No
inyecta nada**: el puntero lo mueve el operador; el instrumento hace lo que el operador no puede con
precisión, medir.

| Modo | Qué hace |
| :--- | :--- |
| `--launch` | Arranca el host Uno desacoplado, lo maximiza y CALIBRA la escena por píxel (3 tarjetas, sus sockets y los huecos de cable) → `qa-manual-247/calib_manual.json` |
| `--watch <s>` | **Vigilante**: mide la escena cada ~1,25 s mientras el operador gesticula y escribe la línea de tiempo (`timeline.jsonl`), con una captura por cambio material. El operador no tiene que avisar de nada ni posar para la foto |
| `--timeline` | Resume la línea de tiempo: cada cambio material con lo que cambió |
| `--frames` | Forense de las capturas: ¿la región capturada sigue siendo la app? (cobertura del fondo y de la rejilla, diferencia contra la base) |
| `--windows` | Ventanas visibles en Z-ORDER: **qué cubre la región capturada** (sin esto, una ventana encima se confunde con la app) |
| `--shot <etiqueta>` | Captura + medición puntual |
| `--inject-test` | Prueba de UNA inyección de puntero, para decidir si la sesión puede automatizarse |
| `--status` / `--stop` | Estado de la ventana / cierre limpio por `WM_CLOSE` |

Señales medidas por fotograma: acento `#818CF8` (la barra del nodo y los cables) por tarjeta; **borde de
selección `#6366F1`** por tarjeta; **diferencia general de píxeles** contra el fotograma de referencia;
`center` (panel del spotlight); huecos de cable; **ventana en primer plano**; **posición del cursor**.

## 2. La frontera del 231, re-medida (y el error del instrumento que la medía)

- `--inject-test` sobre la escena calibrada: `SetCursorPos` + `mouse_event` (botón izquierdo) en el
  centro de la tarjeta izquierda → **delta 0** en las tres señales, **0 píxeles cambiados** en los
  3860×2120 de la ventana. Confirma el 231: sin UIAccess el puntero inyectado no llega al contenido de
  WinAppSDK.
- **Honestidad sobre el instrumento**: el instrumento del 231 (y la primera versión del de esta sesión)
  medía la selección con el color del ACENTO (`#818CF8`), pero **el borde de selección es OTRO color**
  (`CanvasAccentPrimaryBrush = #6366F1`, en `App.xaml`). Esa métrica era **ciega a la selección** y
  habría dado "0 px" aunque el gesto hubiera llegado. De ahí las dos señales nuevas (§1): el borde real
  y la diferencia general de píxeles. El "0 px" que sostiene el 231 es hoy el de la diferencia general.

## 3. Las tomas

| Toma | Qué pasó | Estado |
| :--- | :--- | :--- |
| 1 | El operador respondió antes de gesticular y **el chat (`Freebuff Desktop`, 2238×1266) quedó por encima de la región** de la app (`fg False`, primer plano = chat, `center` 0,9 no-fondo) con un instrumento que además era ciego a la selección; la app terminó al final de la toma | **INVALIDADA** (queda archivada: `timeline_toma1_instrumento_ciego.jsonl`) |
| 2 | Primer clic real con el instrumento curado: borde de selección en la tarjeta del medio (`sel1 5 → 1461`, `dpx 0 → 6636`) y deselección con el clic en el fondo (`sel1 1461 → 5`) | **El puntero real llega** ✅ |
| 3 | Tres clics guiados por predicción para medir el desplazamiento (§4) | **Defecto medido** ❌ |
| 4 | Clic derecho en el socket de entrada del nodo del medio para desconectar su cable | **No certificado**: el operador clicó en (798, 373) y el socket está en (787, 273) — 100 px por debajo. La línea de tiempo no registra ningún cambio en los huecos de cable (`gap0` 159 y `gap1` 147 constantes) |

## 4. EL HALLAZGO: el *hit-test* de las tarjetas resuelve desplazado

**Qué se ve**: clicar la cara de una tarjeta NO la selecciona; clicar a su derecha/derecha-abajo sí
selecciona *esa* tarjeta; clicar su cara puede deseleccionar todo (el clic cae en el fondo).

**Medido** (cursor real del operador en el fotograma del cambio, en coordenadas de ventana; cajas de las
tarjetas medidas por su barra de acento):

| Cambio (t) | Cursor (pantalla) | Sonda = ventana − (280, 41) | Tarjeta clicada por el operador | Tarjeta seleccionada MEDIDA |
| :--- | :--- | :--- | :--- | :--- |
| 87,40 s | (830, 358) | (560, 327) | la derecha (x≈1225) | **la IZQUIERDA** (caja 498..741 × 291..751) |
| 201,45 s | (1217, 349) | (947, 318) | la derecha (x≈1190) | **la DEL MEDIO** (caja 808..1051 × 291..751) |
| 66,09 s | (517, 709) | (247, 678) | la izquierda | **ninguna** (la sonda cae sobre el cajón, x 0..280) → deselecciona todo |
| 224,29 s | (1203, 704)* | — | fondo (y≈890) | **ninguna** → deselecciona todo |

\* el fotograma cogió el cursor en tránsito hacia el socket; el clic guiado fue a y≈890 y la sonda cae
por debajo de la caja (849 > 751) → fondo, que es lo medido.

**Predicción verificada fuera de muestra**: antes de la toma 3 se predijo por escrito que clicar la cara
de la tarjeta DERECHA seleccionaría la DEL MEDIO (cumplido: t=201,45 s, `sel1 5 → 1461`), y que clicar
400 px más abajo no seleccionaría nada (cumplido: todo deseleccionado). El modelo no se ajustó a los
datos: los anticipó.

**El sitio del código**: el lienzo vive en `MainWindow.xaml` en la **columna 1 (el cajón mide 280)** y en
la **fila 1 (la barra superior ≈41)**; dentro del control, el puntero se toma en coordenadas del lienzo
(`e.GetCurrentPoint(RootGrid).Position`) y se le pasa a
`VisualTreeHelper.FindElementsInHostCoordinates(point, this)`, que **no** es ese espacio:

- `CardAt` ([`EditorCanvasControl.xaml.cs`](file:///FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs),
  «la tarjeta bajo el puntero») — el hit-testing de la selección, del arrastre y del doble clic.
- `HitsInteractiveControl` (mismo fichero) — la guardia que impide arrancar rubber band o pan sobre la
  barra de zoom.

El desplazamiento medido es exactamente el del lienzo en la ventana, `(280, 41)`: la sonda cae 280 px a
la izquierda y 41 arriba de la intención. Con eso, el área de clic útil de cada tarjeta vive 280 px a la
derecha y 41 px abajo de su dibujo (y la tarjeta de la derecha queda casi fuera del alcance: para
seleccionarla hay que clicar ~560 px a su derecha).

**Qué gestos del guion quedan contaminados por el mismo punto**:

- selección por clic, arrastre de nodo (`OnCanvasPressed` → `CardAt`) y doble clic en tarjeta (que con el
  desplazamiento cae en el fondo y abre el spotlight);
- la guardia de la barra de zoom (`HitsInteractiveControl`);
- **el rubber band no se certificó en esta sesión** y su lectura deja una duda declarada:
  `UpdateRubberSelection` compara el rectángulo del puntero (espacio de pantalla) contra
  `Canvas.GetLeft/GetTop` del contenedor (espacio LOCAL del plano) — dos espacios distintos si la cámara
  no es identidad;
- **los sockets van por su propio camino** (`NodeCardView.OnSocketPressed` / `OnSocketRightTapped`, con
  hit-testing real): ese canal NO está contaminado; lo que falló fue la puntería (un socket de ~12 px),
  no el canal.

## 5. Estado del guion (3.2/3.3) paso a paso

| Paso | Estado | Evidencia |
| :--- | :--- | :--- |
| Entrega del gesto físico (foco del lienzo, clic) | ✅ **CERTIFICADO** | el borde de selección aparece y desaparece medido (`sel 5↔1461`) |
| 3.2.1 clic selecciona con glow | ❌ **FALLA** | clic en la cara → selecciona otra tarjeta o ninguna (§4) |
| 3.2.2 clic en fondo deselecciona | ✅ funciona (y funciona *de más*) | `sel0/sel1 → 5` con el clic en el fondo |
| 3.2.3–3.2.5 arrastre, Ctrl+Z, Ctrl+Y | ⛔ **BLOQUEADO** por el defecto (el arrastre arranca por `CardAt`) | — |
| 3.2.6 rubber band | ⛔ **NO CERTIFICADO** (duda de espacios en `UpdateRubberSelection`) | — |
| 3.2.7/3.2.9 spotlight, F2, Delete | ⛔ **BLOQUEADO** (exigen selección previa) | — |
| 3.3.1 desconexión por clic derecho en socket | ⚠️ **NO CERTIFICADO** por puntería (socket de ~12 px) | `gap0/gap1` sin cambio en toda la toma |
| 3.3.2–3.3.4 cable pendiente, cancelar y conectar | ⚠️ canal limpio (sockets con hit-testing propio), pendiente de sesión | — |

## 6. Qué queda, y qué no se finge

1. **Arreglar el hit-test** (el punto del puntero en el espacio que la API espera) y volver a ejecutar el
   guion: es lo único que desbloquea los pasos 3.2.3–3.2.9 y el cable.
2. **Certificar el rubber band** tras el arreglo, y declarar si su comparación de espacios necesita el
   mismo tratamiento.
3. **El socket pide más área (o ayuda de anclaje)**: 12 px de diana no se aciertan ni con puntero real y
   a la vista; es un hallazgo de usabilidad de la propia sesión.
4. **Los checkpoints de la app**: la toma 1 terminó con la app cerrada al final (sin evento en el
   registro de Windows); no se reprodujo en las tomas 2-4 (405 fotogramas con la app viva). Queda
   anotado, no convertido en hallazgo.

## 7. Artefactos de la sesión

- `qa-manual-247/calib_manual.json` — calibración de la escena (3 tarjetas, sockets y huecos).
- `qa-manual-247/timeline.jsonl` — línea de tiempo de la toma válida (240 fotogramas, ~355 s).
- `qa-manual-247/timeline_toma1_instrumento_ciego.jsonl` — la toma invalidada, archivada con su motivo.
- `qa-manual-247/00_base.png`, `inj_antes.png`, `inj_despues.png` — base y la prueba de la frontera del 231.
- `qa-manual-247/f*.png` — una captura por cambio material de la escena.
