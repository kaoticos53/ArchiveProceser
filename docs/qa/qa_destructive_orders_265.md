# Las seis órdenes destructivas que quedaban mudas en el host Uno (hito 265)

**Medido el 28 de septiembre de 2026** sobre el host Uno compilado con MSBuild de VS 18 (compilación 6961,
el número con el que el tramo cerró; el contador `.build_number` sigue avanzando con cada compilación de
verificación, así que la app puede declarar uno posterior), con la aplicación **abierta** y el driver externo
`qa_destructive_uia.py`.

## El defecto que se cierra

El hito 264 cerró la confirmación del gestor de presets y **declaró** que el mismo patrón seguía vivo en otras
**seis** órdenes del host. No eran del gestor: eran del **contrato mal usado**. Todas preguntaban con la
variante **síncrona** (`IDialogService.ShowConfirmation`), que en este host no puede contestar de verdad:

| Quién la usaba | Orden | Qué pasaba |
| :--- | :--- | :--- |
| `ControlBarViewModel` | `NewWorkflowAsync` («Nuevo Flujo», menú y `Ctrl+N`) | **no** vaciaba el lienzo y **no** avisaba |
| `ControlBarViewModel` | `RollbackLastExecutionAsync` («Revertir Archivos») | **no** revertía nada y **no** avisaba |
| `ThemeCustomizerViewModel` | `DeleteThemeAsync` («Eliminar tema») | **no** borraba y **no** avisaba |
| `VirtualFileSystemExplorerViewModel` | `ClearVirtualFileSystemAsync` («Limpiar VFS») | **no** vaciaba y **no** avisaba |
| `AiModelManagerViewModel` | `DeleteModelAsync` («Eliminar modelo») | **no** borraba y **no** avisaba |
| `SyntheticDataSetDesignerViewModel` | `DeleteDataSetAsync` («Eliminar dataset») | **no** borraba y **no** avisaba |

La variante síncrona contesta «no» desde el hilo de UI (WinUI no puede bloquearlo) y «**sí**» desde un
servicio sin diálogos: ninguna de las dos es la respuesta del usuario.

## Lo que se construyó

La regla es **la que ya existía** —`IDialogService.ConfirmAsync` del SDK y la decisión en el **view model
portable**—, aplicada a las seis sin duplicar una línea en las vistas:

| Pieza | Qué es |
| :--- | :--- |
| `ControlBarViewModel` | `NewWorkflowAsync` y `RollbackLastExecutionAsync` esperan `ConfirmAsync`. `CreateNewWorkflow()` se separa: **confirmar no es parte de crear un flujo** (quien ya tiene la respuesta no necesita el diálogo). |
| `ThemeCustomizerViewModel` | `DeleteThemeAsync` espera `ConfirmAsync`. |
| `VirtualFileSystemExplorerViewModel` | `ClearVirtualFileSystemAsync` espera `ConfirmAsync`. |
| `AiModelManagerViewModel` | `DeleteModelAsync` espera `ConfirmAsync`, y su pregunta deja de estar escrita en el código: sale del diccionario (`AiModelManager_ConfirmDeleteMsg` / `AiModelManager_ConfirmDeleteTitle`, en los **cuatro** diccionarios de los dos hosts). |
| `SyntheticDataSetDesignerViewModel` | `DeleteDataSetAsync` espera `ConfirmAsync`. |
| `App.Uno/Controls/ThemeCustomizerBody.xaml(.cs)` | **Botón «Eliminar» dibujado** (`ThemeStudioDeleteButton`, habilitado sólo con un tema propio): su fila sale de `DeclaredPendingParts`. Antes la orden del estudio no tenía puerta. |
| `App.Uno/MainWindow.xaml.cs` | «Nuevo Flujo» deja de confirmar en la vista: ejecuta la **orden canónica** y refresca el renglón del ciclo que lee el canal externo. |
| `App.Uno/Controls/ControlBar.xaml.cs` | El atajo `Ctrl+N` enruta por el **mismo camino** que el botón del cajón (una orden, un camino: sigue siendo la ventana quien refresca el renglón). La fila de `HostOwnedOrders` explicando el desvío. |

### Tres defectos que encontró la revisión adversarial (y quedaron arreglados)

1. **`SyntheticDataSetDesignerViewModel` nacía con el doble nulo.** El nodo declaraba su superficie pero
   construía el contenido **sin diálogos** (`new SyntheticDataSetDesignerViewModel()`), y el nodo —que vive
   en un plugin— no puede resolverlos: se los pasa quien abre, por el `NodeCustomActionContext`. Cambiar la
   pregunta a la vía asíncrona **no bastaba**: el contrato asíncrono del doble nulo delega en su síncrona, que
   contesta «sí». El diseñador borraba **en silencio en los dos hosts**. Ahora los diálogos viajan por el
   contexto, como ya hacía el gestor de presets, y también la ventana del escritorio y el comando del núcleo
   los entregan.
2. **Un evento muerto en la barra.** Al pasar «Nuevo Flujo» al comando canónico, `Bar.NewWorkflowRequested`
   dejó de dispararse (el compilador lo dijo: `CS0067`) y la ventana seguía suscrita a él: el atajo `Ctrl+N`
   ejecutaba el comando **sin** los dos pasos de host que sí hacía el cajón (el rastro y el refresco del
   renglón del ciclo). Ahora el atajo pide la orden a la ventana, como el cajón.
3. **La pregunta del borrado de un modelo estaba escrita en el código.** De las ocho órdenes destructivas,
   siete ya sacaban su texto del diccionario (`_loc.GetString` / `LocalizationManager.Instance`) y ésta llevaba
   el literal —«¿Estás seguro de que deseas eliminar el modelo 'X' del disco local?» y «Eliminar Modelo»—, así
   que **no se traducía nunca**, ni en el escritorio ni en este host. No destruye nada, así que ninguna medición
   de datos lo ve: lo ve la **guardia**, que ahora exige que el método de cada orden destructiva lea al menos un
   texto del diccionario (y un **mutante** que vuelve a escribirlo, para que esa exigencia no sea una frase).

### Fronteras declaradas (no se tocaron)

- **`ClearVirtualFileSystemAsync` no tiene puerta**: ninguna vista del escritorio ni del host dibuja su botón.
  Queda preguntando por la vía correcta y **sin entrada**; dibujarla es UI nueva, fuera del encargo, y así se
  declara en vez de fingir una medición.
- **`DeleteModelAsync` no se mide en la app abierta**: su borrado retira ficheros **reales** del disco
  (`.onnx`/`.gguf` de `%AppData%\FileFlow\models`, varios GB). Se mide su determinación en la suite
  (dicotomía «pregunta / no destruye») y no con el ratón.
- **El escritorio no cambia**: conserva su confirmación síncrona y `ConfirmAsync` delega en ella, así que sus
  seis órdenes siguen preguntando como preguntaban.

## Verificación

| Comprobación | Resultado |
| :--- | :--- |
| Host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` | **EXIT 0 · 46 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-settings` | **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias | `UnoNodeDialogsGuardTests` **14** · `UnoControlBarParityGuardTests` **13** · `UnoShortcutParityGuardTests` **5** |
| Pruebas del tramo | `VirtualFileSystemExplorerViewModelTests` (2 del vaciado) · `SyntheticDataSetDesignerViewModelTests` **16** (2 del borrado) |
| Mutaciones | **5 nuevas, las 5 MUERDEN** · **95 declaradas** · **15 de 17 subsistemas** |
| Suite completa | **1935 superadas + 1 omitida de 1936, 0 errores** (dos corridas completas verdes) |
| Rojo intermitente | **una corrida intermedia trajo 1 fallo que no quedó nombrado** (su salida se cortó al leerla); `ExampleFlowsEndToEndTests` **4 de 4 en aislamiento** |
| Sesión con la app abierta | **33 de 33 pasos** (`qa-manual-265`) |

Los tres bloques que miden comportamiento —las cuatro sondas, las cinco mutaciones y la sesión con la app
abierta— se **repitieron sobre el binario reconstruido** después del último cambio de fuente del tramo, con
los **mismos recuentos** que aquí se publican (y la sesión, sobre la carpeta `qa-manual-265` que se conserva).

### Las cinco mutaciones nuevas

| Mutación | Testigo que la muerde | Control que sigue verde | Tiempo |
| :--- | :--- | :--- | ---: |
| `vfs-que-se-vacia-sin-preguntar` | `ClearVirtualFileSystemCommand_WhenRefused_ShouldLeaveTheStoreAlone` | `ClearVirtualFileSystemCommand_WhenConfirmed_ClearsStore` | 60,4 s |
| `disenador-que-borra-sin-preguntar` | `DeleteDataSetCommand_WhenRefused_ShouldLeaveTheDataSetAlone` | `DeleteDataSetCommand_WhenConfirmed_ShouldRemoveTheCustomDataSet` | 51,1 s |
| `orden-destructiva-que-vuelve-a-la-via-sincrona` | `EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne` | `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` | 44,5 s |
| `superficie-declarada-sin-los-dialogos-del-host` | `EveryDeclaredSurface_ShouldCarryTheHostDialogs_SoItsDestructiveOrdersCanAskForReal` | `TheMediaPresetManager_ShouldBeDeclaredByTheNode_AndPaintedByBothHosts` | 45,0 s |
| `pregunta-destructiva-escrita-en-el-codigo` | `EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne` | `TheSixSections_ShouldBeDeclared_WithTheirPaneAndTheirButton` | 29,2 s |

## Sesión con la aplicación abierta (`qa-manual-265`)

Driver externo `docs/qa/qa_destructive_uia.py` (33 pasos, **VERIFICADO**: 33 de 33). Se ejercen **dos** de las
seis órdenes, las dos restaurables sin tocar datos del usuario:

**«Eliminar tema» del Estudio** (la orden que estrena puerta en este host):

| Paso | Medido |
| :--- | :--- |
| Línea base | catálogo del usuario con **1** tema propio (`☀️ Claro Minimalista (Personalizado)`, md5 `f8b1a4e9…`), lienzo con **3** tarjetas |
| El estudio se abre desde el cajón | anclas `ThemeStudioThemeList` · `ThemeStudioNewButton` · `ThemeStudioDeleteButton` · `ThemeStudioCloseButton` |
| Con un tema de fábrica elegido | **«Eliminar» deshabilitado** (los del sistema son inmutables) |
| «Nuevo tema» | el **fichero** del almacén pasa de **1 a 2** temas (el nuevo: `Mi Tema Personalizado`) |
| «Eliminar» | **PREGUNTA**: `HostConfirmationAccept` + `HostConfirmationCancel` en el árbol, dentro del estudio (el estudio sigue debajo), y el centro pasa a `#B0ACAC` por el velo |
| Con la pregunta en pantalla | almacén **2** (nada destruido) |
| **Cancelar** | almacén **2**, el tema propio sigue, la escena vuelve al estudio |
| **Confirmar** | almacén **1**, el tema creado **desaparece**, catálogo **idéntico** al de partida y fichero **byte a byte** (`f8b1a4e9…`) |

**«Nuevo Flujo»** (menú → «Nuevo Flujo», con nodos en el lienzo):

| Paso | Medido |
| :--- | :--- |
| «Nuevo Flujo» | **PREGUNTA** en su **propio modal** (no hay otro abierto): `HostConfirmationDialog`, botones «Aceptar»/«Cancelar», centro `#B0ACAC` |
| Con la pregunta en pantalla | las **3 tarjetas** siguen en el árbol |
| **Cancelar** | las **3 tarjetas** siguen (árbol y **3** barras de acento por pixel) y la escena vuelve al pixel de base `#FCF8F8` |
| **Confirmar** | lienzo **vacío**: **0** tarjetas en el árbol y **0** barras de acento en la captura |

**Cierre y datos del usuario:**

- El grafo **no se persiste**: la app se reinicia y el lienzo vuelve a sus **3 tarjetas** enteras.
- El **catálogo de temas** queda **byte a byte** (`f8b1a4e9…`) y el **almacén de presets** ni se toca
  (`9b1e8f19…`).
- Las **preferencias** tienen el **mismo md5** antes y después de ejercer las dos órdenes (el driver lo lee
  del fichero y lo compara, así que el valor concreto no se congela aquí: cambia entre arranques); la única
  clave que reescribe la sesión entera es `LastUpdateCheckUtc`, y la reescribe el
  **arranque** de la app (`AutoCheckForUpdates`), no las órdenes —comprobado midiendo el md5 **antes** del
  reinicio, que es idéntico al de partida—.
- Los **ajustes esenciales** quedan intactos: `ActiveTheme=pastel_spring`, `Language=es-ES`,
  `DefaultGlobalOutputDir=E:\---- Test Data\Salida`, `FavoriteNodeTypes=[FileFlow.Plugin.FileSystem.FolderSourceNode]`.

### Dos defectos del INSTRUMENTO, escritos para el guion futuro

1. **`ThemeStudioBody` no existe para el canal externo.** Su raíz es un `Grid` con `AutomationId` y un `Grid`
   no tiene peer de automatización: el driver reconocía el estudio por una ancla que **nunca** llega. El
   estudio abierto se reconoce por sus **controles** (`ThemeStudioThemeList`, `ThemeStudioNewButton`, …).
2. **El pixel del centro no distingue un lienzo con tarjetas de uno vacío**: el fondo del lienzo es el mismo.
   Las tarjetas se cuentan por su **barra de acento** sobre la captura (`accent_groups`), que sí lo distingue
   (3 → 0).
