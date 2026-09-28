# Hito 264 — La confirmación de las órdenes destructivas del gestor de presets (y las dos puertas que se comportan igual)

**Fecha:** 2026-09-28 · **Sesión con la app abierta:** `qa-manual-263` · **Driver:** `docs/qa/qa_presets_uia.py`

El tramo anterior (hito 263) portó el gestor de presets y **midió un defecto de producto**, declarándolo sin
arreglar: las órdenes destructivas **no pedían confirmación** y, encima, **se comportaban distinto según la
puerta**. Este tramo lo cierra.

## El defecto, medido

| Puerta | Antes | Qué veía el usuario |
| :--- | :--- | :--- |
| Fila del preset (`ParamPreset_Preset`) | El view model recibía el servicio **Nulo** (`ShowConfirmation => true`) | «Eliminar» **borraba sin preguntar** (11 → 10 medido) |
| Tarjeta del nodo (acción `ManageMediaPresets`) | El view model recibía el servicio **del host**, cuya confirmación es **síncrona** y devuelve «no» desde el hilo de UI | «Eliminar» **no borraba y tampoco avisaba** (11 → 11 medido) |

Dos comportamientos para una sola regla, y ninguno de los dos preguntaba.

## Qué quedó arreglado

1. **Contrato del SDK** (`IDialogService.ConfirmAsync`): la confirmación **asíncrona**, con implementación por
   defecto que **delega en la síncrona** en un hilo de fondo. El escritorio (bomba anidada de mensajes) y los
   dobles de prueba siguen funcionando sin cambios; un host moderno la reescribe.
2. **La regla vive en el view model portable** (`MediaPresetManagerViewModel`): `DeletePresetAsync` y
   `ResetDefaultsAsync` **esperan la respuesta REAL** del usuario y sólo destruyen si dijo que sí. La variante
   síncrona ya no se usa para destruir.
3. **El host Uno pregunta de verdad** (`UnoWindowService.AskInsideActiveDialogAsync`): la pregunta se monta como
   una **capa DENTRO del cuerpo del modal abierto** —WinUI sólo admite un `ContentDialog` por raíz, así que un
   segundo modal no cabe— con sus dos botones reales y **sin bloquear el hilo de UI**.
4. **Las dos puertas resuelven el mismo servicio del host**: la del núcleo que pasa el servicio **Nulo** quedó
   eliminada de la fila (`CoreDialogHost.ResolveDialogService()`), y la de la tarjeta pide el suyo igual.
5. **La pregunta no puede quedarse tomada**: hay **dos** caminos por los que una pregunta se pierde sin que
   nadie conteste —el modal que se va con ella en pantalla (Escape, su botón de cerrar) y **otra pregunta que la
   sustituye**— y los dos la **contestan «no» y la retiran**. Sin eso, el host rechazaba toda pregunta posterior
   (la orden no hacía nada y no avisaba) y la que la esperaba no terminaba nunca: su botón se quedaba muerto
   para el resto de la sesión.
6. **El aviso informativo también es una capa** y se contesta con su «Aceptar»; cuando llega una pregunta de
   verdad, **la sustituye** en vez de declinarse en silencio.

## Lo que se midió (números)

| Pieza | Resultado |
| :--- | :--- |
| Host Uno (MSBuild de VS 18, salida por la que corren las sondas) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú/cajón) | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 46 `[OK]` · 0 `[FALLO]`** (antes 33: **+13** de este tramo) |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardia tocada | `UnoNodeDialogsGuardTests` **12 de 12**, 0 rojos |
| Pruebas del gestor | `MediaPresetManagerViewModelTests` **10 casos**, 0 rojos |
| Mutaciones nuevas | `gestor-que-borra-sin-preguntar` **MUERDE** (33,5 s) · `pregunta-de-borrado-por-la-via-sincrona` **MUERDE** (30,9 s) — testigo rojo, control verde en las dos |
| `mutations/COVERAGE.md` | regenerado por su guardia: **90 declaradas · 15 de 17 subsistemas** |
| Suite completa | **1930 superadas + 1 omitida de 1931, 0 errores** (2 m 38 s) |
| Rojo intermitente | **no apareció** en esta corrida |

### Las 13 comprobaciones nuevas de la sonda de diálogos

Por la **fila** (bloque 3.b): el alta llega al almacén como preset propio · «Eliminar» **pregunta** (la capa con
sus dos botones, dentro del modal) · el cuerpo sigue siendo el mismo · **con la pregunta en pantalla no se ha
borrado nada** · la pregunta se contesta por su **botón de cancelar real** · un «no» **no borra** · un «sí»
**borra** · «Restablecer» **también pregunta** y un «no» deja el catálogo del usuario intacto.
Por la **tarjeta** (bloque 3.c): el botón de la acción —en el panel que despliega su conmutador— abre **el mismo
gestor** · alta · «Eliminar» **pregunta** y un «no» **no borra** · un «sí» **borra** · cierra como el de la fila.
Y el cierre del bloque: el catálogo de presets del usuario queda **byte a byte** como estaba (3504 bytes).

### La sesión con la app abierta: **42 de 42 pasos** (`qa-manual-263`)

Ambas puertas, cada una con su ciclo completo: **alta 10 → 11** · «Eliminar» **pregunta** (anclas
`HostConfirmationAccept`/`HostConfirmationCancel` en el árbol, y el centro de la ventana pasa a `#B0ACAC` por el
velo) · **con la pregunta en pantalla, 11 presets** · **cancelar deja 11** · **confirmar deja 10**. Después,
«Restablecer» pregunta y **cancelarlo deja 10**. Al final: lienzo con sus 3 tarjetas, almacén **byte-idéntico**
(`9b1e8f19477c5ebc3605ac373a38b38b`) y **ajustes del usuario intactos** (tema, idioma, carpeta de salida y
favoritos). Capturas de la pregunta: `85_tarjeta_pregunta.png`, `85_fila_pregunta.png`.

## ¿El mismo patrón afecta a otras órdenes destructivas del host?

**Sí: quedan seis llamadas síncronas** que en este host **no preguntan nada y no hacen nada** (el hilo de UI no
puede bloquearse, así que `UnoDialogService.ShowConfirmation` devuelve «no» sin mostrar diálogo):

| Fichero | Orden |
| :--- | :--- |
| `App.Core/ViewModels/ControlBarViewModel.cs` (×2) | cerrar/nuevo flujo con cambios sin guardar |
| `App.Core/ViewModels/ThemeCustomizerViewModel.cs` | restablecer un tema |
| `App.Core/ViewModels/VirtualFileSystemExplorerViewModel.cs` | limpiar el almacén virtual |
| `App.Core/ViewModels/AiModelManagerViewModel.cs` | borrar un modelo descargado |
| `Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs` | quitar un dataset sintético |

**Fronteras declaradas:**

- Estas seis **no se tocan aquí**: cada una es el mismo arreglo de una línea (pasar a `ConfirmAsync`) **más su
  guardia y su medición**, y este tramo era el gestor de presets. Quedan **declaradas y localizadas**, con su
  síntoma (la orden no hace nada y no avisa) para que el próximo tramo las tome en bloque.
- La semántica elegida para dos preguntas simultáneas es **sustituir la anterior** (contestarla «no»), no
  encolarlas: encolar dejaría la segunda pregunta sin mostrar hasta que alguien conteste la primera, y con el
  velo puesto el usuario puede estar mirando otra cosa.
- El escritorio **no cambia de comportamiento**: su `ShowConfirmation` síncrono se conserva y `ConfirmAsync`
  delega en él, así que sus órdenes destructivas siguen preguntando como antes.

## Defecto del instrumento que este tramo encontró (y arregló)

La sonda comparaba el **escapado** del fichero del almacén contra el de otro serializador: el producto escribe los
acentos como `\u00F3` (**hex en mayúsculas**) y `JsonSerializer.Serialize` los produce en minúsculas, así que
«Guardar» se daba por fallido con el valor escrito. Ahora el fichero se **lee como JSON** y se compara el valor.
También se guardó el **marco** de cada excepción del sondeo, no sólo su mensaje: sin eso, una excepción del hilo
de UI obligaba a adivinar qué lectura la lanzó.

## Evidencia

- `FileFlow.App.Uno/bin/Debug/net10.0-windows10.0.19041.0/selfcheck-dialogs-report.txt` (46 `[OK]`, 0 `[FALLO]`).
- `docs/qa/qa-manual-263/presets-session.json` (42/42, y los dos ciclos destructivos con sus recuentos).
- `docs/qa/qa-manual-263/85_{tarjeta,fila}_pregunta.png` (la pregunta en pantalla, por las dos puertas).
