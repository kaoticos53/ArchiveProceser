# El ladrón del foco del lienzo Uno: identificado, y el teclado reclamado (hito 253)

**Veredicto: IDENTIFICADO Y ARREGLADO.** El elemento que se lleva el foco ~0,1 s después de un clic **no es
del producto**: es un envoltorio de scroll de la **plantilla de ventana del framework** (sin nombre, sin
`DataContext`, del tamaño del área de contenido, fuera del árbol visual). Lo que **sí** era del producto —el
lienzo perdiendo el teclado justo después de cada clic— queda arreglado con una **reclamación acotada**, y
está **certificado con puntero real** en los cuatro pasos: recuperar el teclado del ajeno, responder a `Supr`
y `Ctrl+Z`, y **no** quitárselo al buscador del cajón.

Cuatro sesiones, todas con el ratón del operador (el instrumento no inyecta puntero): **254, 255, 256**
(identificación) y **257** (certificación del arreglo).

---

## 1. El encargo

«Identifica qué elemento desprendido se lleva el foco ~0,5 s después del clic en el lienzo Uno y, si es del
producto, haz que deje de robarlo, con su sonda y su guardia.»

El punto de partida era la frase del hito anterior: «el ladrón queda identificado en clase y sin nombre — un
`ScrollViewer` que el gestor de foco entrega a un elemento que no está en el árbol visual». Identificar eso
exige el puntero, porque **el robo no se reproduce sin él** (un intento previo de reproducirlo desde el
sondeo, seleccionando un nodo por el mismo camino, dejó el foco quieto).

## 2. Lo que cambió en el instrumento (y sigue siendo sólo medición)

- **Los envoltorios de las pestañas del inspector tienen NOMBRE** (`InspectorParamsScroll`,
  `InspectorSnapshotsScroll`, `InspectorInputsScroll`, `InspectorOutputsScroll`, `InspectorDiffScroll`) y el
  del cajón también (`ToolboxScroll`). Sin nombres, el rastro sólo podía imprimir `ScrollViewer#`.
- **La ficha del ladrón** (`DescribeThief`): tipo y nombre, `IsLoaded`, tamaño medido, padre **lógico**,
  `XamlRoot`, `DataContext`, y —si es un `ScrollViewer`— su contenido y el `DataContext` de éste.
- **Dos lecturas del foco**: el origen del `GotFocus` en la raíz **más** una re-lectura un tick después
  (un elemento recién creado puede no estar todavía en el árbol cuando el evento ocurre). El `GotFocus`
  también se registra en la raíz, que es lo que revela si el ladrón **burbujea** o no.
- **El refresco del inspector** se anota con su pestaña activa, para casar el robo con la reconstrucción.
- **La ronda discriminante**: dos clics separados (fondo del lienzo y cara de una tarjeta) para separar
  «activación de ventana» de «selección de tarjeta».

## 3. La identificación (sesiones 254, 255 y 256)

Lo que el rastro dice, literal:

```
press src=Border punto=(437,225) foco=True enfocado=EditorCanvasControl#Canvas
GotFocus  enfocado=EditorCanvasControl#Canvas
foco global -> src=EditorCanvasControl#Canvas | gestor=EditorCanvasControl#Canvas
LostFocus enfocado=ScrollViewer# | cargado=True | mide=3072x1657 | padreLogico=DependencyObject
          | con XamlRoot | datacontext=sin DataContext
          | contenido=Border#<-ScrollContentPresenter#<-ScrollViewer# | contenidoCargado=True | popups=ninguno
```

Tres hechos que cierran la identificación:

1. **No burbujea**: no aparece ninguna línea `foco global ->` para el `ScrollViewer`. Su `GotFocus` no llega a
   la raíz de la ventana — y no puede, porque **no está en el árbol visual** (`VisualTreeHelper.GetParent` es
   nulo). El gestor de foco entrega el teclado a un elemento desprendido.
2. **Es de la ventana entera, no de un panel**: mide `3072x1657`, el área de contenido completa. Los paneles
   del producto son de 280 y 300 px y **tienen nombre** (el rastro no imprime ninguno). Tampoco hay popups
   abiertos (`popups=ninguno`), así que no es un `ToolTip` ni un desplegable.
3. **No lo dispara la selección**: la ronda discriminante midió el robo **dos veces**, una por clic
   (`press src=Grid` en el fondo, `press src=Border` en la tarjeta), a **141 ms** y **78 ms** del clic
   respectivamente. Es decir: lo dispara **la pulsación en el lienzo**, no el nodo seleccionado ni el
   refresco del inspector.

**Conclusión**: es un envoltorio de la plantilla de ventana de Uno/WinAppSDK (tamaño del área de contenido,
sin `DataContext`, con un `Border` dentro y su padre lógico fuera de lo que el XAML expone), **no código del
producto**. No hay nada que «arreglar» en él; lo que el producto tiene que arreglar es su consecuencia: el
lienzo pierde el teclado con cada clic.

## 4. El arreglo: la reclamación acotada

Como el ladrón no es del producto, la cura es del lado que sí lo es. El lienzo declara el teclado **suyo
durante una ventana corta** tras el clic y, si el envoltorio ajeno se lo lleva dentro de esa ventana, lo
**recupera** en el tick siguiente. Cuatro guardias, en orden de importancia:

| # | Guardia | Por qué |
| :--- | :--- | :--- |
| 1 | `TickCount64 > _keyboardOwnedUntil` → no reclamar | Sin clic reciente esto no es un robo: es un cambio de dueño, y el lienzo no discute |
| 2 | `IsTextInput(owner)` → no reclamar | Un cuadro de texto manda en su teclado (buscador, renombrado, editores) |
| 3 | `IsInsideSelf(owner)` → no reclamar | Si el dueño está DENTRO del lienzo, las teclas ya le llegan por burbujeo |
| 4 | `IsInsideEditorPanel(owner)` → no reclamar | Si el usuario acaba de clicar en el cajón o en el inspector, el teclado es suyo |

La reclamación se hace **en el tick siguiente** a perder el foco, no en el `LostFocus`: el envoltorio
necesita su pase de layout para quedar como dueño, y reclamar antes sería una carrera que a veces se perdería.
La ventana (700 ms) acota la pelea: el mecanismo es idempotente y termina solo.

## 5. La certificación con puntero real (sesión 257)

Predicciones escritas antes de cada gesto. El rastro, literal y abreviado:

```
press src=Border punto=(440,237) foco=True enfocado=EditorCanvasControl#Canvas
LostFocus enfocado=ScrollViewer# | mide=3072x1657 | datacontext=sin DataContext | popups=ninguno
foco RECUPERADO del envoltorio ajeno (ScrollViewer#)          ← el arreglo
GotFocus  enfocado=EditorCanvasControl#Canvas
tecla=Delete src=EditorCanvasControl enfocado=EditorCanvasControl#Canvas
tecla=Z      src=EditorCanvasControl enfocado=EditorCanvasControl#Canvas
LostFocus enfocado=TextBox#SearchBox | mide=262x32 | padreLogico=StackPanel#<-Grid#<-NodeToolboxPanel#Toolbox
enrutado tecla=I    consumido=False enfocado=TextBox#SearchBox<-...<-NodeToolboxPanel#Toolbox
enrutado tecla=M    consumido=False enfocado=TextBox#SearchBox<-...<-NodeToolboxPanel#Toolbox
enrutado tecla=Back consumido=False enfocado=TextBox#SearchBox<-...<-NodeToolboxPanel#Toolbox
```

Y la medición de píxeles del vigilante (51 fotogramas):

| t | Gesto | Medición | Veredicto |
| :--- | :--- | :--- | :--- |
| 44,2 s | clic en la cara de la tarjeta del medio | `sel1 4 → 1453` | seleccionada ✅ |
| 47,3 s | **`Supr`** | **`nglobal 3 → 2`** (y `sel1 → 0`) | **borra** ✅ |
| 51,8 s | **`Ctrl+Z`** | **`nglobal 2 → 3`**, `sel1` de vuelta en 1453 | **restaura** ✅ |
| 54,9 → 59,5 s | clic en el buscador del cajón y escribir **`im`** | `dpx 4412 → 98880` con la caja enfocada (el cajón **filtra** al escribir) | **las letras llegan** ✅ |
| 67,2 → 68,8 s | volver al lienzo y clic en el fondo | `dpx → 0` (escena base), con **segunda reclamación** medida | ✅ |

Lo importante de la secuencia del buscador: la tecla crece con `consumido=False` y el dueño es
`TextBox#SearchBox` — es decir, **el lienzo no se la quitó**, y el filtro del cajón reaccionó (los 98 k px de
cambio son la lista regenerándose). La cortesía del cuadro de texto no sólo está declarada: está medida en la
app viva.

## 6. Sonda, guardia y mutación del arreglo

- **Sonda** `ProbeKeyboardReclaim` (selfcheck, **EXIT 0 con 80 OK**): con la propiedad del clic abierta,
  simula el robo con **objetivos reales de la ventana** y mide los cuatro casos. Su propio renglón declara con
  qué midió — `objetivos: ajeno=Button#, cuadro=TextBox#SearchBox, panel=Button#ViewModeToggle, lienzo=Button#`
  — porque una sonda que no dice con qué midió miente por omisión. (La primera corrida de esta sonda cazó un
  **error de la sonda misma**: el filtro de «dentro/fuera de los paneles» estaba invertido; el renglón con los
  objetivos lo hizo evidente.)
- **Guardia** `UnoCanvasKeyboardGuardTests` → **6 casos** (+1: exige la ventana de propiedad, las tres
  cortesías en una sola condición, el censo de los tres usos de la reclamación, el tick siguiente y la sonda
  corrida por el selfcheck, y cita su mutación).
- **Mutación** `reclamacion-que-roba-al-cuadro-de-texto` (la 56.ª declarada): quita **sólo** la cortesía del
  cuadro de texto → **MUERDE** (testigo rojo, control verde, árbol restaurado por bytes). Es el defecto
  inverso al que se arregla —el lienzo quitándole el foco a media palabra a quien escribe— y no produce ningún
  error visible. COVERAGE: **56 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda
  **11 de 40**.

## 7. Límites y lo que sigue sin medir

1. **El envoltorio del framework sigue ahí**: no es del producto y no se puede tocar; el producto se defiende
   de él (reclamación + enrutado). Si una versión futura de Uno cambia ese comportamiento, la reclamación se
   vuelve innecesaria — no dañina (sólo actúa cuando el dueño es ajeno y el clic es reciente).
2. **El robo sólo se puede medir con puntero real** (el entorno no lo inyecta): la sonda lo **simula** con
   elementos reales de la ventana, y la sesión humana es la que certifica.
3. **Sigue sin medirse** lo que el 250 dejó: la puntería del clic sobre los conectores (~12 px de diana), el
   rectángulo de selección con el lienzo desplazado, y las rondas de zoom y de arrastre desde el cajón.

## 8. Reproducir esta sesión

```powershell
cd docs/qa
$env:FILEFLOW_QA_WORK = "qa-manual-257"        # 254/255/256 para la identificación
$env:FILEFLOW_CANVAS_TRACE = "1"              # el rastro del foco y del enrutado
python qa_manual_session.py --launch
python qa_manual_session.py --watch 300
python qa_manual_session.py --timeline
python qa_manual_session.py --stop            # o crear qa-manual-257/stop_watch
# el rastro queda en FileFlow.App.Uno/bin/Debug/<tfm>/canvas-focus-trace.txt
```
