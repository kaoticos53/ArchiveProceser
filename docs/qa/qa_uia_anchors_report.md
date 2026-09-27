# Sonda C — `qa_uia_anchors.py`: la observación UIA externa sobre la superficie nueva del hito 238

**Fecha**: 2026-09-27 · **Instrumento**: `docs/qa/qa_uia_anchors.py` (pywinauto 0.6.9, backend `uia`, sin UIAccess) · **App**: host Uno con la superficie UIA del hito 238.

## El encargo

«Añade AutomationIds explícitos al lienzo del host Uno y a la barra de zoom para que la observación UIA externa alcance su foco y estado».

## Qué cambió en el producto (hito 238)

- **Lienzo** (`EditorCanvasControl.xaml`): `AutomationProperties.AutomationId="CanvasRoot"` en el `UserControl` + `IsTabStop="True"`; `CanvasSurface` en el `Grid` de gestos; `CanvasGraphPlane` en el plano con el transform de la cámara. Anclas como recursos nombrados (`UiAnchorCanvas`, `UiAnchorZoomLevel`): renombrar una es tocar UNA línea.
- **Peer de automatización** (`EditorCanvasControl.xaml.cs`): `OnCreateAutomationPeer` override con `CanvasAutomationPeer : FrameworkElementAutomationPeer` (control + contenido). Sin peer, un contenedor (UserControl + Grid) no expone NADA por UIA — el árbol del 237 llegaba a las tarjetas pero no a la superficie que recibe el foco y el teclado.
- **Barra de zoom**: `ZoomBar` (Border), `ZoomLevelText` (nivel), `ZoomInButton` / `ZoomOutButton` / `FitToScreenButton`.
- **Selfcheck** (66 comprobaciones, EXIT 0): la sonda de superficie UIA verifica desde dentro — ancla + peer expuestos, foco programático aceptado, estado del zoom observable (cambiado y restaurado).
- **Guardia** (`UnoAutomationSurfaceGuardTests`, 7 tests): las anclas y el peer como código vivo + tabla de anclas citada contra `TestSuiteIndex.MethodNames`.

## Resultados de la Sonda C (app viva, pid 5908)

| # | Medición | Veredicto | Evidencia |
| :-- | :--- | :--- | :--- |
| A1 | Anclas por AutomationId | **5/8** — `CanvasRoot`, `ZoomLevelText`, `ZoomInButton`, `ZoomOutButton`, `FitToScreenButton` presentes; `CanvasSurface`, `CanvasGraphPlane`, `ZoomBar` AUSENTES del árbol | WinUI solo materializa elementos con peer: los contenedores (Grid/Canvas/Border) sin peer no aparecen aunque lleven AutomationId. Declaradas y medidas; no fingidas. |
| A2 | Foco del lienzo por UIA | **PASS** | `set_focus` sobre `CanvasRoot` ejecutado sin error — el paso que el acotamiento del 237 dejó en el puntero del usuario. |
| A3 | Estado del zoom observable | **PASS** | Invoke de `ZoomInButton`: `'100 %' → '110 %'`; restore con `ZoomOutButton`: `'100 %'`. InvokePattern sobre Button + estado leído por ancla. |
| B1 | Atajo del lienzo con foco UIA (la pregunta del 237) | **PASS — HALLAZGO** | Shift+A con el foco EN `CanvasRoot` (entregado por `set_focus` UIA) **ABRE el spotlight**. |

## El hallazgo B1: el canal de teclado del lienzo queda ABIERTO

La cadena de acotamientos del entorno queda así:

- **Hito 231**: ni teclado ni puntero llegan; `InjectTouchInput` exige UIAccess. Bloqueo irreductible documentado.
- **Hito 237**: el teclado SÍ llega (Sonda B: 'fold' escribió el buscador); el bloqueo del 231 se acota al **FOCO** — y el foco del lienzo parecía exigir el clic del usuario (puntero).
- **Hito 238 (esta sonda)**: el foco del lienzo NO exige puntero — **el `set_focus` de UIA sobre el peer enfocable lo entrega** — y con el foco en el lienzo, `keybd_event` dispara el atajo (spotlight abierto). La mitad FÍSICA de 3.2.3 que el 237 dio por acotada al puntero tiene ahora una vía observable sin UIAccess.

Lo que sigue pendiente de puntero (no simulado): el gesto del ratón — selección por clic en tarjeta, arrastre, cable por click-drag del socket, rubber band. El teclado del lienzo (atajos, spotlight) ya es ejercitable desde un observador externo.

## Cómo correrla

```powershell
PYTHONIOENCODING=utf-8 python docs/qa/qa_uia_anchors.py
```

Exit 0 si A1+A2+A3 pasan (B1 se reporta como INFO en cualquier sentido: es la frontera medida, no una exigencia). La app se lanza y se cierra sola.

## Lecciones de instrumento

- `descendants()` de pywinauto 0.6.9 no acepta `automation_id` (lección del 237): filtrar en Python — esta sonda lo hace.
- Las anclas de contenedores sin peer (Grid/Canvas/Border) **no materializan** en el árbol UIA aunque la propiedad esté asignada. Para anclar un contenedor: peer propio (como el del lienzo) o leer a través de un hijo con peer (como `ZoomLevelText`).
- InvokePattern vive en los Button de WinUI aunque el 237 no lo encontrara en un Text — la vía de observación del estado de la barra sin puntero.
