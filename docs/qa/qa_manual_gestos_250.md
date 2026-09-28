# Guion manual de gestos 3.2/3.3 con PUNTERO REAL — la re-sesión sobre el hit-test arreglado (hito 250)

**Veredicto: EJECUTADA. La mitad del guion que el 247 dejó BLOQUEADA por el defecto queda certificada**;
la otra mitad destapa un defecto nuevo (los atajos del lienzo no llegan en un flujo que empieza con el
puntero) y una frontera de medición (los gestos de socket de ~12 px).

La sesión se condujo sobre el host Uno **con el arreglo del hit-test del hito 249 dentro** (mismo binario
que dejó el selfcheck en `EXIT 0` con la sonda del área de clic en `3/3`). Las nueve rondas se hicieron con
el ratón del operador; el instrumento **no inyecta** puntero, mide.

---

## 1. El encargo y lo que cambió respecto al 247

«Repite la sesión manual con puntero real del 247 sobre el hit-test ya arreglado y deja en `docs/qa` la
evidencia de qué pasos del guion quedan certificados ahora.»

El escenario también cambió, y conviene decirlo porque arregla el problema que invalidó la primera toma del
247: esta vez **el chat estaba en el segundo monitor** (`Freebuff Desktop`, rect `(3886,224)-(6124,1762)`),
así que nunca solapó la región de la app. Lo confirma la forense de capturas (`--frames`): en los 41
fotogramas guardados la cobertura del fondo del lienzo es `bg≈0.795` y la de la rejilla `grid=0.002`,
constantes — **la región capturada fue el lienzo de la app de principio a fin**.

## 2. El instrumento, y lo único que cambió

[`docs/qa/qa_manual_session.py`](file:///docs/qa/qa_manual_session.py) — el mismo del 247 (no inyecta;
calibra por píxel, vigila la escena cada ~1,4 s y mide acento por tarjeta, **borde de selección
`#6366F1`**, **diferencia general de píxeles**, huecos de cable, spotlight, primer plano y cursor).

- **Cambio único**: la carpeta de la sesión ahora la elige quien la conduce con `FILEFLOW_QA_WORK` (por
  omisión, la del 247). Esta sesión escribió en `qa-manual-250/`, para no mezclar la toma del defecto con
  la de su arreglo.
- **Límites del instrumento que esta sesión dejó a la vista** (§6): guarda captura sólo en los primeros 40
  cambios materiales (los gestos de las rondas 8 y 9 no tienen fotograma), las cajas por tarjeta están
  ancladas a la calibración inicial (un pan las invalida) y el muestreo de ~1,4 s no reconstruye el
  rectángulo de un rubber band.

## 3. Las nueve rondas

| # | Gestos | Predicción (escrita antes) | Medición | Veredicto |
| :--- | :--- | :--- | :--- | :--- |
| 1 | clic en el fondo (2200,1400); clic en la cara de la **izquierda** (600,500) | el clic selecciona ESA tarjeta (en el 247 no seleccionaba nada) | `sel0 3 → 1154` con el cursor en (622,365); `sel1`/`sel2` en su ruido (4/3) | **P1 ✅** |
| 2 | traer al frente; clic en el fondo; clic en la cara del **medio** (912,500) | el fondo deselecciona; el clic selecciona el medio | `sel0/1/2 = 3/4/3` y **`dpx = 0`** (escena idéntica a la base) → deseleccionado; después `sel1 4 → 1453` con `sel0` en 3 | **P3 ✅ / P4 ✅** |
| 3 | arrastre del medio +150/−70; `Ctrl+Z`; `Ctrl+Y`; `Ctrl+Z` | el arrastre mueve la tarjeta y `Ctrl+Z` la devuelve | `gap0 159 → 302`, `gap1 151 → 118`, la barra del medio sale de su caja (`cx1 = -1`); **las teclas no cambian ni un píxel** (302/118 constantes de t=187 a t=205) | arrastre ✅ / **deshacer-rehacer ❌** |
| 4 | clic en el lienzo; `Shift+A`; doble clic | — | `center` se queda en `0,000`: **no se abrió el spotlight**; los fotogramas muestran el cursor **sobre las tarjetas** (946,588 / 834,248 / 1252,387): el doble clic sobre una tarjeta no lo abre **por diseño** → ronda sin ejecutar donde toca | ronda **nula** |
| 5 | doble clic en el fondo; `Shift+A` | el doble clic abre; `Shift+A` no | **dos aperturas** medidas (`t≈342`: `center 0,152`, `dpx 15408→54768→15408`; `t≈360`: `center 0,070`, `dpx 15408→54668→15408`), panel visible en `f03424`/`f03440` | doble clic ✅ / `Shift+A` **no atribuible** |
| 6 | clic en la cara de una tarjeta; **Supr** | Supr no borra (el teclado no llega al lienzo) | `sel2 1461→6→1454` (el clic reparte a la tarjeta clicada, la derecha) y **`nglobal` sigue en 3** con la tarjeta seleccionada | **P12 ✅** |
| 7 | clic en el fondo; rubber band (760,800)→(1400,160) | selecciona la del medio y la derecha, no la izquierda | el rectángulo se dibuja (`dpx 361184`, span de acento `488..1541@127`) y tras soltar: **`sel1 685`, `sel2 1454`, `sel0 8`** | **P14 ✅** (transform identidad) |
| 8 | clic derecho en el socket del medio (807,179); arrastre socket→socket | desconecta el cable izquierda↔medio; el arrastre lo reconecta | el clic cayó en (≈810,340), **110–160 px por debajo** del socket: **`(+154,+322)` de pan** de las tres tarjetas con ancho de barra idéntico (243 px), no una desconexión | **pan medido**; 3.3.1/3.3.4 **no certificadas** |
| 9 | rubber band envolviendo las tarjetas dibujadas; clic derecho seco en (961,504) | con el plano paneado, no saldrán las tres | **`sel1 613`, `sel2 579`, `sel0 9`**: las dos derechas seleccionadas y la izquierda **no**; el clic derecho cayó en (968,607), otra vez ~100 px bajo el socket | **duda del rubber band medida**; socket **no** |

## 4. Estado del guion 3.2/3.3, paso a paso

| Paso | 247 | **250** | Evidencia |
| :--- | :--- | :--- | :--- |
| Entrega del gesto físico (el puntero real llega) | ✅ | ✅ **CERTIFICADO** | borde de selección medido en cinco gestos distintos (`sel 3 ↔ 1454`) |
| 3.2.1 clic selecciona con glow | ❌ FALLA | ✅ **CERTIFICADO** | ronda 1 (`sel0 3→1154`), ronda 2 (`sel1 4→1453`), ronda 6 (`sel2 6→1454`): siempre la tarjeta clicada |
| 3.2.2 clic en fondo deselecciona | ✅ | ✅ **CERTIFICADO** | `dpx = 0` con la escena idéntica a la base (134,46 s) |
| 3.2.3 arrastre de tarjeta | ⛔ BLOQUEADO | ✅ **CERTIFICADO** | `gap0 159→302`, `gap1 151→118`, la tarjeta sale de su caja |
| 3.2.4/3.2.5 `Ctrl+Z` / `Ctrl+Y` | ⛔ BLOQUEADO | ❌ **FALLA** | 18 s de toma sin un píxel de cambio tras las teclas (ronda 3); **causa candidata** en §5.3 |
| 3.2.6 rubber band | ⚠️ NO CERTIFICADO | ⚠️ **CERTIFICADO sólo con transform identidad** | ✅ ronda 7 (medio+derecha, no la izquierda); ✗ ronda 9: con `translate=(154,322)` seleccionó 2 de las 3 envueltas |
| 3.2.7 spotlight | ⛔ BLOQUEADO | ✅ **por puntero** / ⚠️ **por teclado sin atribuir** | doble clic en el fondo: panel medido dos veces (`center 0,152`, `dpx → 54768`); `Shift+A` con dos aperturas no atribuibles (§6) |
| 3.2.9 `F2` / renombrar / **Supr** | ⛔ BLOQUEADO | ❌ **FALLA** `Supr` | `nglobal = 3` con la tarjeta seleccionada (ronda 6) |
| 3.3.1 desconexión por clic derecho en el socket | ⚠️ puntería | ⚠️ **puntería, y además panea** | el clic cae ~100–160 px bajo el socket en **las dos sesiones**; el botón derecho sobre el cuerpo arranca un **pan** (§5.4) |
| 3.3.4 cable pendiente y reconectar | ⚠️ pendiente | ⛔ **NO EJECUTADO donde toca** | los arrastres de la ronda 8 fueron, en realidad, un pan |
| Pan del lienzo (botón derecho) | ⚠️ pendiente | ✅ **CERTIFICADO (sin querer)** | `(+154,+322)` de traslación con ancho de barra constante |
| Zoom con la rueda | ⚠️ pendiente | ⛔ **NO EJECUTADO** | no se llegó a la ronda de zoom |

## 5. Los hallazgos

### 5.1 El hit-test está arreglado, y se ve en el reparto
El mismo tipo de clic que en el 247 seleccionaba **otra** tarjeta o ninguna, aquí selecciona **siempre la
clicada**: la izquierda (`sel0 3→1154`), el medio (`sel1 4→1453`) y la derecha (`sel2 6→1454`). Además, el
arrastre por la cara de una tarjeta ya mueve **esa** (`gap0 159→302`) y el doble clic en el fondo abre el
buscador. Lo que bloqueaba el 247 queda desbloqueado.

### 5.2 El reparto es el correcto incluso cuando el operador se equivoca de objetivo
No es un detalle menor: en las rondas 6 y 8 el operador apuntó a otro sitio del que creía, y el borde
apareció **en la tarjeta donde el cursor estaba**, no en la que se pretendía. La métrica no se ajusta al
deseo: mide.

### 5.3 🔴 HALLAZGO NUEVO: los atajos del lienzo no llegan en un flujo que empieza con el puntero
Medido con tres atajos distintos, y con la app en primer plano (`fg True`) durante todo el gesto:

| Atajo | Efecto esperado | Medición |
| :--- | :--- | :--- |
| `Ctrl+Z` | devolver la tarjeta a su sitio | `gap0/gap1` sin cambio durante 18 s (302/118) |
| `Ctrl+Y` | volver a moverla | sin cambio |
| `Supr` | borrar la tarjeta seleccionada | `nglobal = 3` (no borró) |

Los tres caen en el mismo patrón: la tecla llega a la **app** pero no al **lienzo**. En la sesión UIA del
238 los atajos sí funcionaban porque el `set_focus` externo enfoca el `UserControl` del lienzo, que es
dueño del handler (`KeyDown += OnKeyDown` y `KeyDown="OnKeyDown"` sobre `RootGrid`).

**Causa candidata del fuente (leída, no medida en runtime)**: `OnCanvasPressed` intenta entregar el foco
con `((FrameworkElement)sender).Focus(FocusState.Programmatic)` — y `sender` es `RootGrid`, un `Grid`, que
**no es focusable**; el valor de retorno se descarta. Con el puntero, el foco no entra al lienzo. Nota: la
atribución de `Shift+A` no se pudo cerrar en esta sesión (dos aperturas del buscador medidas, sin poder
distinguir cuál provocó la segunda), así que el `NO-LLEVA` se declara sólo para los tres atajos de la
tabla.

### 5.4 🟠 El botón derecho SOBRE UNA TARJETA panea el lienzo
El comentario del código lo dice al revés que el código:

```csharp
// 3. Pan (fondo del lienzo, botón derecho): nunca sobre tarjetas ni controles.
if (properties.IsRightButtonPressed && !HitsInteractiveControl(point))   // no comprueba CardAt
```

Medido: un clic derecho (con movimiento) sobre el **cuerpo** de la tarjeta del medio trasladó el plano
`(+154,+322)` — las tres tarjetas, con ancho de barra idéntico (243 px), o sea **pan**, no zoom ni
arrastre— y esa traslación dejó inválidas las cajas del instrumento (los huecos de cable midieron 0 sin
que se hubiera borrado ningún cable).

### 5.5 🟠 La duda del rubber band es real: con `translate ≠ 0` selecciona de más y de menos
Con el plano paneado `(154,322)`, un rectángulo que envolvía **las tres** tarjetas dibujadas seleccionó
**dos**, y dejó fuera la izquierda (`sel1 613`, `sel2 579`, `sel0 9`) — exactamente el síntoma de comparar
el rectángulo del puntero (espacio del lienzo) contra `Canvas.GetLeft/Top` (espacio local del plano), que
el 247 dejó declarado como duda. Con la transform identidad (ronda 7) el mismo gesto selecciona justo las
tarjetas envueltas, lo que confirma que el defecto vive en el desfase, no en el gesto.

**Lo que no cuadra y no tapo**: la aritmética estática no reconcilia del todo el eje Y (las cajas de la
comparación son `card.Width`×`140`, no la geometría dibujada), así que el desfase medido se queda como
«selecciona mal» con los números por delante, sin una fórmula cerrada. La sonda que lo cerraría es
instrumentar `UpdateRubberSelection` para que cante, por tarjeta, el centro que compara.

### 5.6 ⚪ La puntería del socket, medida por segunda vez
El operador clicó a ~110–160 px por debajo del socket en la ronda 8 y a ~100 px en la ronda 9: en las dos
sesiones el sesgo es el mismo, y el objetivo es un circulito de ~12 px en la barra. Es el hallazgo de
usabilidad que el 247 ya declaró; ahora tiene dos medidas consistentes. Lo que esta sesión añade es la
consecuencia: **fallar ese clic no es neutro** — arranca un pan (§5.4).

## 6. Límites de la medición (declarados, no disimulados)

1. **Capturas**: el vigilante guarda fotograma en los **40** primeros cambios materiales; las rondas 8 y 9
   quedaron fuera (la 3, la 5 y la 7 sí tienen imagen). El resto de rondas se sostienen sobre la medición
   numérica de la línea de tiempo.
2. **Cajas ancladas a la calibración inicial**: un pan desplaza las tarjetas y las cajas por tarjeta /
   huecos de cable dejan de coincidir con lo dibujado. Por eso `gap0/gap1 = 0` al final **no** significa
   «cables borrados», significa «el instrumento mira donde ya no hay nada».
3. **Muestreo ~1,4 s**: el rectángulo de un rubber band no se reconstruye (sólo se ven 2–3 posiciones del
   cursor). Por eso la ronda 9 se lee con la medición de la **selección**, no con la geometría exacta del
   rectángulo que dibujó el operador.
4. **La app no era el primer plano en 433 de los 626 fotogramas** (respondía en el chat, en el otro
   monitor). La verificación de región (§1) demuestra que la captura seguía siendo el lienzo; el dato se
   declara porque en el 247 esa misma situación invalidó una toma entera.
5. **El veredicto del gesto es del operador y la evidencia es de la medición**: donde los dos no
   coincidieron (ronda 4: el operador creyó haber ejecutado el guion y los fotogramas muestran el cursor
   sobre las tarjetas), se anota la ronda como **nula** y no como fallo del producto.

## 7. Artefactos

- Línea de tiempo completa: `qa-manual-250/timeline.jsonl` — **626 fotogramas** de 0,56 s a 918,44 s,
  **50 cambios materiales**, con la app viva de principio a fin y **sin ninguna muerte del proceso**.
- Calibración y capturas: `qa-manual-250/calib_manual.json`, `00_base.png` y 40 fotogramas `f*.png`
  (entre ellos `f03424`/`f03440`, el panel del buscador abierto, y `f03456`/`f03638`, cerrado).
- El instrumento con la carpeta elegible: `FILEFLOW_QA_WORK=qa-manual-250 python qa_manual_session.py --launch|--watch|--timeline|--frames|--windows`.

## 8. Qué queda

1. **Los atajos del lienzo** (§5.3): arreglar el foco del puntero (enfocar el `UserControl`, que es quien
   tiene el handler) y **certificar con la misma sesión manual** `Ctrl+Z`, `Ctrl+Y`, `Supr`, `F2` y
   `Shift+A`.
2. **El pan con el botón derecho sobre una tarjeta** (§5.4): la condición o el comentario; y con ello
   decidir qué gesto abre el menú contextual de la tarjeta, si es que debe existir.
3. **El espacio del rubber band** (§5.5): comparar contra la geometría **dibujada** (el mismo
   `TransformToVisualCenter` de la sonda del 249) y cerrar la duda con la sonda que canta el centro
   comparado.
4. **La diana del socket** (§5.6): o se agranda la zona activa, o el clic derecho sobre una tarjeta deja
   de panear; hoy fallar el socket tiene un efecto colateral que el usuario no espera.
5. **El zoom con la rueda y las rondas que quedaron fuera** (zoom, `F2`, duplicar, arrastre desde el
   cajón): sin ejecutar en esta sesión.
