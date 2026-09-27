# Guion manual de interacciones 3.2/3.3 — host Uno (resultado de la sesión con puntero, 2026-09-26)

**Veredicto: NO EJECUTABLE EN ESTE ENTORNO — bloqueo irreductible de inyección de puntero, documentado con la evidencia completa.** El guion queda escrito y el instrumento preparado (`qa_manual.py`, en este directorio) para la primera sesión con puntero real.

> **ACTUALIZACIÓN (2026-09-27, vía UIA)**: la sesión de UI Automation (`qa_uia_probe.py`,
> `qa_uia_probe_b.py`, `qa_uia_gestures.py` + `qa_uia_gestures_report.md` en este directorio)
> **acotó este bloqueo**: el PUNTERO sigue sin llegar al contenido WinUI (0 px, ruido 0 — confirmado),
> pero el **TECLADO SÍ LLEGA** cuando el foco lo entrega UIA (`set_focus` del proveedor, sin
> UIAccess): el buscador del cajón recibió 'fold'/'folder' inyectado y el filtro reaccionó en vivo,
> y el modificador Shift también llegó ('A' mayúscula). El atajo del lienzo no se dispara POR
> DISEÑO (su `OnKeyDown` ignora TextBox y la tecla no está en el lienzo): la puerta que falta es el
> **foco del lienzo**, que en producción entrega el clic. Resultado del guion por esta vía: **5/5
> pasos observados en verde**, con la mitad física (selección, arrastre, cable) pendiente del puntero.
>
> **ACTUALIZACIÓN (2026-09-27, superficie UIA del 238)**: el lienzo tiene ahora ancla explícita
> (`AutomationId="CanvasRoot"`), `IsTabStop` y **peer de automatización enfocable** (hito 238) — la
> Sonda C (`qa_uia_anchors.py` + `qa_uia_anchors_report.md` en este directorio) entrega el foco del
> lienzo por `set_focus` UIA **SIN puntero** y el atajo Shift+A **SE DISPARA** (spotlight abierto):
> el canal de teclado del lienzo queda ABIERTO. La mitad física que sigue pendiente del puntero es
> el GESTO DEL RATÓN (selección por clic, arrastre, cable por click-drag del socket, rubber band).
> El estado del zoom ya es observable sin puntero: Invoke de los botones y nivel leído por su
> AutomationId (`ZoomLevelText`).

## Qué intentó esta sesión (y qué demostró cada intento)

| # | Técnica | Resultado medido | Conclusión |
| :--- | :--- | :--- | :--- |
| 1 | `mouse_event` (down/up) sobre tarjetas/sockets | 0 px cambiados en capturas (regiones de tarjeta y pantalla completa) | El contenido no reacciona |
| 2 | `SendInput` absoluto | Cursor llega al punto objetivo (verificado con `GetCursorPos`) pero 0 px cambiados | El cursor se mueve; el contenido no reacciona |
| 3 | `SendInput` relativo + frame de movimiento previo | 0 px cambiados | Sin reacción ni con fotograma de puntero previo |
| 4 | `SendInput` con `MOUSEEVENTF_VIRTUALDESK` (escritorio virtual completo, monitor con origen negativo) | Cursor correcto, 0 px cambiados | Descartado el error de multi-monitor |
| 5 | `InjectTouchInput` (WM_POINTER nativo, la vía que WinUI consume) | **Denegado: error 5 (acceso)** | El sistema exige UIAccess para inyectar puntero |
| 6 | `PostMessage` sintético (WM_LBUTTONDOWN/UP) al `Microsoft.UI.Content.DesktopChildSiteBridge` | 0 px cambiados | WinUI no consume mensajes de ratón sintetizados |
| 7 | Sonda de teclado: NumLock inyectado | El estado del sistema cambia (1→0) | El teclado inyectado SÍ llega al sistema |
| 8 | Sonda de teclado en la app: Alt+F4 | La app se cierra (exit 0) | Los atajos del sistema llegan a la ventana |
| 9 | Atajos del lienzo por teclado (Shift+A → spotlight) | 0 % de panel no-fondo en el centro | El foco del lienzo exige puntero; atajo no ejecutable sin él |

**El punto exacto del bloqueo**: el input de puntero inyectado desde un proceso sin UIAccess no llega al contenido de WinAppSDK/WinUI 3 (la pila de input de la ventana no lo entrega al `ContentIsland`), aunque el cursor se mueva y el estado de botón llegue al sistema. `InjectTouchInput` — la única vía que genera WM_POINTER real — está denegada por política de seguridad de este entorno (error 5, sin opción de elevación de UIAccess disponible).

## Qué sí quedó verificado de la app viva (aporte de la sesión)

- La app arranca con el ejemplo auto-cargado y la ventana maximizable a 3860×2120; la captura de pantalla muestra las 3 tarjetas con sus barras de acento `#818CF8` segmentables por análisis de píxel (base 3 tarjetas, huecos de ~69 px, sockets In/Out localizados a ambos lados de la barra).
- El calibrador visual (`qa_manual.py --calibrate`) funciona: tarjetas, sockets y fondo localizados y registrados en `calib.json`.
- El selfcheck en runtime sigue EXIT 0 (44 OK), que es lo máximo demostrable sin puntero (selección por comando, borrado/undo, conexión/desconexión por los mismos métodos que los handlers, decoradores/spotlight/migas por métodos).

## Estado de los pasos del guion

- **Sin ejecutar en este entorno**: los 15 pasos (3.2.1–3.2.9 y 3.3.0–3.3.4). Ningún paso se marca PASS ni FAIL de producto: el bloqueo es del instrumento (inyección), no del producto. La corrida anterior que marcó PASS/FAIL mixtos medía ruido (cable estático, huecos de segmentación) y queda invalidada por esta auditoría.
- **Instrumento listo**: `qa_manual.py` (calibración + guion completo con métricas de píxel: anillo de selección, posición de tarjetas, área de cable en el hueco, panel del spotlight, conteo de tarjetas). En una sesión con puntero real (o UIAccess), `--calibrate` y `--run` ejecutan el guion completo y escriben este mismo informe con los resultados.

## Cobertura que sí tiene la fase (para no confundir)

Los comportamientos del guion están demostrados **por los mismos métodos que ejecutan los handlers** en el selfcheck del host (hito 230): selección con reacción del núcleo y contenedor del glow, Delete/undo, conexión/desconexión con anclas reales y restauración exacta, decoradores, spotlight que añade nodo real, migas. Lo que este entorno no puede dar es la entrega del GESTO físico (arrastre, timing, foco) — que es lo que la sesión humana con puntero debe certificar con este guion.
