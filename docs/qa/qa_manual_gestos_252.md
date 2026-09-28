# Guion manual de gestos con PUNTERO REAL — el teclado del lienzo tras el arreglo del enrutado (hito 252)

**Veredicto: EJECUTADA Y CERTIFICADA. Un clic con el puntero deja los atajos del lienzo funcionando**:
`Supr` borra, `Ctrl+Z` restaura, `Ctrl+Y` rehace y `F2` abre el renombrado — medido por píxeles y con el
rastro del foco, **mientras el lienzo NO era el dueño del foco**.

La sesión se condujo sobre el host Uno con el arreglo del **enrutado del teclado** dentro (la ventana
resuelve al lienzo las teclas que nadie consumió). El instrumento **no inyecta** puntero: lo movió el
operador, y la medición firma la llegada del gesto al producto.

---

## 1. El encargo y qué es lo nuevo que se venía a medir

«Arregla el foco del lienzo Uno para que un clic con el puntero deje los atajos funcionando
(`Ctrl+Z`, `Ctrl+Y`, `Supr`, `F2`), con su sonda, su guardia y su mutación.»

El hito 250 midió con puntero real que **los atajos no llegaban** y dejó la causa candidata; el primer arreglo
entregó el foco al `UserControl` del lienzo (en vez del `Grid` del handler) y **volvió a fallar**: el rastro
de esa sesión dejó escrito que el clic **sí** entregaba el foco y que ~0,5 s después un `ScrollViewer`
anónimo se lo llevaba. Con eso quedó claro que el contrato «el atajo vive en el foco del lienzo» no se puede
sostener, y el arreglo pasó a ser el del escritorio: **el teclado se resuelve por burbujeo**, con la ventana
enrutando al lienzo lo que nadie consumió.

Lo que esta sesión viene a certificar es exactamente esa mitad: **los atajos funcionan sin ser dueños del
foco**. Y de paso a nombrar al ladrón, que es lo único que quedó sin identificar.

## 2. El instrumento, y lo único que se añadió

[`docs/qa/qa_manual_session.py`](file:///docs/qa/qa_manual_session.py) — el mismo de las sesiones 247/250
(no inyecta; calibra por píxel, vigila la escena cada ~1,4 s y mide acento por tarjeta, **borde de selección
`#6366F1`**, diferencia general de píxeles, huecos de cable, primer plano y cursor).

- **Nuevo: el rastro del foco** (`FILEFLOW_CANVAS_TRACE=1`). El host Uno escribe, con reloj monótono, cada
  cambio de foco de la ventana (`foco global ->`, `LostFocus`) y cada tecla que la ventana **enruta** al
  lienzo: `enrutado tecla=<T> consumido=<bool> enfocado=<quién tenía el foco>`. Esa línea es la prueba de
  esta sesión: un atajo `consumido=True` con `enfocado=ScrollViewer#` significa que el lienzo lo resolvió
  **sin** ser dueño del foco.
- **Nuevo: el ladrón con nombre y apellidos** (`DescribeOpenPopups`): el `ScrollViewer` que recibe el foco
  no tiene ancestros en el árbol visual, así que la ronda 253 pregunta además por los `popup` abiertos.
- **Carpetas**: `qa-manual-252/` (la ronda de certificación) y `qa-manual-253/` (la identificación del
  ladrón), para no mezclar tomas — la convención que estrenó el 247 con `FILEFLOW_QA_WORK`.

## 3. La ronda de certificación (252): predicción escrita antes, medición después

Escena calibrada: tres tarjetas con las barras en `y=281`, caras a `y≈500`; caras a `x` 488–731 / 800–1044
/ 1113–1356; cables `gap0=159` (izquierda↔medio) y `gap1=151` (medio↔derecha). 78 fotogramas, 22 cambios
materiales, la app viva todo el tiempo.

| t | Gesto | Predicción (escrita antes) | Medición | Veredicto |
| :--- | :--- | :--- | :--- | :--- |
| 40,0 s | clic en la cara de la tarjeta **del medio** (922,500) | se selecciona ESA | `sel1 4 → 1453`, `sel0/sel2` en ruido (3/3), `dpx 3036` | **P1 ✅** |
| 69,0 s | **`Supr`** | la tarjeta desaparece (`nglobal 3 → 2`) | **`nglobal 3 → 2`**, `gspans` pasa a dos cajas, `gap0/gap1 → 0` (los cables que la tocaban) | **P2 ✅** |
| 72,2 s | **`Ctrl+Z`** | reaparece (`2 → 3`) | **`nglobal 2 → 3`**, la tarjeta vuelve a `cx1=922`, `sel1=1448` | **P3 ✅** |
| 83,0 s | **`Ctrl+Y`** | la vuelve a borrar | **`nglobal 3 → 2`** | **P4 ✅** |
| 84,6 s | **`Ctrl+Z`** | la trae otra vez | **`nglobal 2 → 3`** | **P4 ✅** |
| 92,3 / 93,9 s | `Ctrl+Y` y `Ctrl+Z` (repetición del operador) | borra y restaura | `3 → 2` y `2 → 3` | ✅ |
| 98,5 → 101,7 s | **`F2`** y **`Escape`** | aparece la caja de renombrado y se cierra | `dpx 3036 → 9124 → 3036` (la caja tapa parte de la barra: `sel1 1453 → 1255 → 1453`) | **P5 ✅** |
| 106,2 s | el operador vuelve al chat | — | `fg True → False`, primer plano `Freebuff Desktop` | fin de ronda |

Los `nglobal 3 → 2 → 3` son la firma del par borrar/deshacer: **el mismo par que el 250 midió sin llegar
(18 s sin un píxel de cambio y `nglobal` intacto)**. Aquí el píxel se mueve en cada paso, y en el orden que
el atajo dicta.

## 4. El rastro: quién tenía el foco cuando llegó cada tecla

```
foco global -> EditorCanvasControl#Canvas<-Grid#<-Grid#root<-ContentPresenter#ClientAreaPresenter<-…
LostFocus   enfocado=ScrollViewer#            ← el clic entrega el foco y este se lo lleva
enrutado tecla=Delete consumido=True  enfocado=ScrollViewer#
enrutado tecla=Z      consumido=True  enfocado=ScrollViewer#
enrutado tecla=Y      consumido=True  enfocado=ScrollViewer#
enrutado tecla=F2     consumido=True  enfocado=ScrollViewer#
enrutado tecla=Control consumido=False enfocado=ScrollViewer#   ← el modificador suelto pasa de largo
```

Las cuatro teclas de los atajos entran por la vía **enrutada** (`enfocado=ScrollViewer#`, no el lienzo) y
las cuatro quedan `consumido=True`; el `Ctrl` suelto no consume nada, que es lo correcto. Es la
certificación de la tesis: **el atajo no necesita el foco**.

## 5. La ronda 253: al ladrón le falta nombre

`LostFocus enfocado=ScrollViewer# | popups=ninguno` — y el `ScrollViewer` **no tiene ancestros en el árbol
visual** (el rastro sólo puede imprimir su tipo). Que no haya popups abiertos descarta el `ToolTip` y
cualquier desplegable: lo que el gestor de foco entrega es un `ScrollViewer` **desprendido del árbol**, sin
nombre y sin padre. La ronda midió además que las teclas ajenas no se tocan (`tecla=F3 consumido=False`,
`tecla=Menu consumido=False`) y que el renombrado se abre y se cierra igual (`dpx 9124` y vuelta).

Queda declarado como frontera del instrumento, no como excusa: **el ladrón está identificado en clase
(`ScrollViewer` desprendido, sin popup) y sin nombre**; perseguirlo exigiría enganchar la creación de
elementos del gestor de foco, y el arreglo ya no depende de él.

## 6. Límites y lo que sigue sin medir

1. **El ladrón no tiene nombre** (§5). El arreglo no lo necesita, pero un lector futuro que vea morir el
   foco en otro host se topará con el mismo anonimato.
2. **La sesión certifica la vía del teclado**, no las que dependen del puntero fino (sockets de ~12 px) ni
   el rubber band con transform no identidad: siguen como el 250 las dejó.
3. **El guion 3.2/3.3 completo no se repitió**: esta sesión mide los pasos del teclado (3.2.4, 3.2.5, 3.2.9
   y el renombrado), que eran los que el 250 dejó en rojo.

## 7. Reproducir esta sesión

```powershell
# con el host Uno compilado (MSBuild de Visual Studio) y el rastro encendido
cd docs/qa
$env:FILEFLOW_QA_WORK = "qa-manual-252"
$env:FILEFLOW_CANVAS_TRACE = "1"
python qa_manual_session.py --launch          # arranca, maximiza, calibra
python qa_manual_session.py --watch 420       # vigilante: mide cada ~1,4 s mientras el operador gesticula
python qa_manual_session.py --timeline        # los cambios materiales, con lo que cambió en cada uno
python qa_manual_session.py --stop            # (o crear qa-manual-252/stop_watch para parar el vigilante)
# el rastro del foco y del enrutado queda en FileFlow.App.Uno/bin/Debug/<tfm>/canvas-focus-trace.txt
```
