# QA — Las Entradas y los Atajos que Faltaban del Menú del Host Uno (hito 259)

El hito 257 portó la barra de control y su cajón, pero dejó **once entradas y seis atajos declarados
pendientes**, en parte porque esperaban al servicio de ventanas y diálogos que el hito 258 construyó. Este
tramo cierra esa mitad: **tres órdenes de flujo** (Nuevo / Cargar / Guardar) cumplidas por el canal propio
del host, **tres entradas de ayuda** (Manual / Ejemplos / Acerca de) servidas por sus órdenes canónicas —con
«Acerca de» ya como superficie real del host— y los **seis atajos** enrutados por una tabla.

- **Evidencia de la sesión con la aplicación abierta**: `docs/qa/qa-manual-269/` (capturas `50_…`-`99_…`,
  `timeline.jsonl`, `menu2-session.json`, 15 fotogramas de cambio de escena del vigilante).
- **Instrumento**: `docs/qa/qa_menu2_uia.py` (driver externo por UIA + teclado físico, sobre el fontanero de
  `qa_ajustes_uia.py` y el instrumento de píxeles de `qa_manual_session.py`).
- **Sonda**: `--selfcheck-controlbar` → `selfcheck-controlbar-report.txt` (**25 `[OK]`**, antes 14).

---

## 1. La frontera que era el bloqueo real (y cómo se cruza)

Las tres órdenes de flujo no estaban pendientes «por falta de tiempo»: su comando del núcleo pide un
diálogo **SÍNCRONO** y este host no puede cumplirlo desde el hilo de UI. Medido y ya escrito en el host
desde el hito 240:

| Contrato | Lo que hace el host desde el hilo de UI | Nota |
| :--- | :--- | :--- |
| `IFileDialogService.ShowOpenFileDialog` / `ShowSaveFileDialog` | **devuelve `null`** (el picker de WinRT exige el hilo de UI y bloquear ahí interbloquearía) | declarado en `UnoFileDialogService` |
| `IDialogService.ShowConfirmation` | **devuelve falso** (`ContentDialogResult.None`) | declarado en `UnoDialogService` |
| `IFileDialogService.Show*Async` | **funciona** (pickers encolados al `DispatcherQueue`) | hito 240 |

Es decir: dibujar la entrada y ejecutar el comando canónico habría sido **un botón que no hace nada**, sin
crash y sin mensaje. El cruce no es reimplementar el flujo en el host, sino **separar el diálogo de la
operación** en el view model portable —la misma regla que el hito 254 ya usó con «cargar un flujo»:

| Pieza nueva en el núcleo | Qué es |
| :--- | :--- |
| `ControlBarViewModel.CreateNewWorkflow()` | El flujo nuevo sin la confirmación: vaciar el lienzo, reponer el nombre y dejar la constancia en la consola. `NewWorkflow()` pasa a ser *confirmar + llamarlo*. |
| `ControlBarViewModel.SaveWorkflowToFileAsync(path)` | El guardado sin el picker: la ruta la pone quien la tenga. Simétrico de `LoadWorkflowFromFileAsync`, que ya existía por la misma razón. |

Y en el host: `UnoDialogService.ShowConfirmationAsync` (la confirmación que **se puede esperar** sin
bloquear el hilo de UI, con su ancla de automatización y su regla de «un solo modal a la vez»), más las tres
manos de la ventana que eligen con los pickers asíncronos y llaman a los métodos portables.

## 2. Qué se construyó

| Pieza | Qué es |
| :--- | :--- |
| `MainMenuDrawer.xaml(.cs)` | **Dos secciones nuevas**, en el orden del escritorio: **GESTIÓN DE FLUJOS** (Nuevo / Cargar / Guardar) y **AYUDA Y RECURSOS** (Manual / Ejemplos / Acerca de). El cajón pasa de 5 a **11 entradas** ancladas; las tres de flujo declaran *qué se ha pedido* por eventos y las tres de ayuda ejecutan la **orden canónica** del núcleo. |
| `MainWindow.xaml.cs` | Las tres manos de flujo: confirmación asíncrona / picker de apertura / picker de guardado + los métodos portables (`CreateNewWorkflow`, `LoadWorkflowFromFileAsync`, `SaveWorkflowToFileAsync`). Y el **enrutado del teclado**: lo que el lienzo no reclama llega a la tabla de atajos del menú. |
| `Controls/ControlBar.xaml.cs` | La tabla **`RoutedShortcuts`** (6 filas: gesto, tecla, modificadores, orden y vía) que **es la que enruta** —el manejador de la ventana la recorre, no hay un `switch` paralelo— más las tablas del censo actualizadas. |
| `Controls/AboutDialogBody.xaml(.cs)` | La ventana **«Acerca de»** del host: la misma información que la del escritorio (rótulos `Uno_About_*` **copiados**, versión de la misma fuente que el pie del cajón) y las insignias de lo que este host es (`.NET 10.0`, `Uno Platform · WinUI 3`, `DAG Flow Engine`). |
| `Platform/UnoWindowService.cs` | `ShowWindow(DialogKeys.About)` servido: **3.ª clave del censo servida** (2 diálogos + 1 ventana), con el modal anclado (`AboutDialog`) y la regla de un solo modal. El censo pasa de 2+7 a **3 servidas + 6 declaradas**. |
| `App.Core/ViewModels/ControlBarViewModel.cs` | Los dos métodos portables del §1. |
| `Resources/Strings*.resx` | Las claves `Uno_Drawer_FlowManagement` / `…NewWorkflow` / `…LoadWorkflow` / `…SaveWorkflow` / `…HelpResources` / `…UserManual(+ToolTip)` / `…ExampleFlows(+ToolTip)` / `…About(+ToolTip)` y `Uno_About_*` **copiadas del diccionario del escritorio**, clave por clave, en los dos idiomas. |

## 3. La paridad, escrita (lo que no llega, nunca fingido)

| Destino | Órdenes |
| :--- | :--- |
| **Dibujadas aquí (25 entradas censadas)** | Las 14 de la barra + las 11 del cajón (con las 6 nuevas). |
| **Cumplidas por el host (4)** | `OpenWorkflowSettingsCommand` (su superficie del 255), y **`NewWorkflowCommand` / `LoadWorkflowCommand` / `SaveWorkflowCommand`** por el canal asíncrono del host. |
| **Atajos enrutados (6)** | F5 → `ContinueWorkflowCommand`, F10 → `StepNextCommand`, Shift+F5 → `StopWorkflowCommand` (comandos del ciclo del núcleo) y Ctrl+N / Ctrl+O / Ctrl+S por el canal del host. `DeclaredUnroutedShortcuts` queda **vacía**, y la guardia exige que lo que el escritorio liga esté en una de las dos tablas. |
| **Pendientes (5)** | `OpenThemeCustomizerCommand` (Estudio de temas), `OpenMetricsDashboardCommand` (métricas), `OpenVirtualFileSystemExplorerCommand` (VFS), `OpenSyntheticDataSetDesignerCommand` (diseñador de dataset) y `OpenUpdateDialogCommand` (aviso de actualización: el host no comprueba actualizaciones). Cada una con su razón en `DeclaredPendingEntries`. |

## 4. Guardia y mutaciones

**`UnoControlBarParityGuardTests` — 9 casos** (antes 8). El nuevo,
`TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne`, cierra las dos mitades: que
la ventana use `ShowConfirmationAsync` / `ShowOpenFileDialogAsync` / `ShowSaveFileDialogAsync` y los métodos
portables, y que **no** ejecute `NewWorkflowCommand.Execute` / `LoadWorkflowCommand.Execute` /
`SaveWorkflowCommand.Execute` —ni reimplemente el producto (`ClearGraph(`, `ExportToGraphModel(` en la
vista)—. El caso de atajos pasa a leer **las dos tablas** (enrutados y declarados), exige que ninguna
contradiga a la otra y que la tabla del lienzo siga siendo la del lienzo.

| Mutación | Muta | Testigo (rojo) | Control (verde) | Tiempo |
| :--- | :--- | :--- | :--- | :--- |
| `flujo-que-se-cumple-por-el-picker-sincrono` (**nueva, 71.ª**) | `MainWindow.xaml.cs` (Cargar) | `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` | `EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt` | 35 s |

Y **dos mutaciones del 257 actualizadas** al producto nuevo, verificadas tras el cambio:
`menu-que-no-declara-lo-que-falta` (ahora borra la fila del VFS con su texto nuevo) y
`menu-que-no-declara-un-atajo` (ahora borra una fila de **`RoutedShortcuts`**, que es la tabla que enruta:
la tecla del escritorio se queda sin ruta ni declaración). **Las tres MUERDEN** (testigo rojo, control
verde, árbol restaurado por bytes). `mutations/COVERAGE.md` regenerado → **71 declaraciones**.

> 🔎 La guardia de declaraciones (`EveryDeclaredMutation_ShouldStillFitTheProductAndTheSuite`) **falló al
> cambiar el producto** y eso fue el aviso que hacía falta: dos mutaciones del 257 apuntaban a líneas que
> este tramo reescribió. Sin ese caso, dos mutantes habrían quedado mintiendo en silencio sobre lo que
> vigilan.

## 5. La sonda en runtime (`--selfcheck-controlbar`, 25 `[OK]`)

De 14 a 25 comprobaciones, todas por el canal del usuario (el peer de automatización) y midiendo el
resultado, no el gesto:

- el cajón expone sus **11 entradas** (censo completo contra la tabla del host);
- **«Acerca de»** abre la superficie del host (con la versión del producto leída del árbol) y cerrarla la
  retira sin dejar diálogos abiertos;
- **«Nuevo Flujo»** pide confirmación y **cancelar deja el grafo intacto (3 → 3 nodos)**; **confirmar** crea
  el flujo nuevo (**0 nodos**) y el **guardado portable escribe el fichero** que después **carga de vuelta
  (3 nodos)**, los dos métodos del §1 ejercidos de punta a punta;
- los **6 atajos** están enrutados y ninguno declarado sin ruta; **F5** se mide por su **efecto** (reanudar
  escribe en la consola: `'Flujo reanudado por el usuario.'`) y F10 / Shift+F5 por su despacho, con una
  tecla que no es del menú (F7) **sin tragarse**;
- **Ctrl+N** abre la MISMA confirmación que la entrada del cajón y cancelarla deja el grafo igual.

## 6. Medición con la aplicación abierta (sesión 269, 27 de 27 pasos)

Driver externo por UIA (clics por `Invoke`) + **teclado FÍSICO** (`keybd_event`) mientras el **vigilante**
medía (79 fotogramas, 15 cambios de escena guardados).

| Paso | Medición |
| :--- | :--- |
| Línea base | `tarjetas=3`, pixel central `#FCF8F8` (77,8 %), **0 anclas del cajón** en el árbol. |
| «Menú» | El cajón expone **11/11** entradas (las 6 nuevas incluidas) y el velo se ve en el pixel: `#FCF8F8` → **`#585454` (99,9 %)**. |
| **«Acerca de»** | Abre la superficie del host: anclas **`AboutDialog`**, `AboutVersionText`, `AboutDescriptionText`; la versión leída por UIA es **`v1.0.0-beta+build.6664 · net10.0 · Uno Platform (WinUI 3)`**; el modal se ve en el pixel (**`#B0ACAC` 67,1 %**). |
| Cerrarla | Su superficie sale del árbol y el pixel vuelve al de la línea base (`#B0ACAC` → `#FCF8F8`). |
| **«Nuevo Flujo»** (entrada) | Pide confirmación: el texto leído es **«¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.»** y el pixel pasa a **`#B0ACAC` (77,5 %)**. |
| Cancelar | **Las mismas 3 tarjetas** en el lienzo y el pixel de vuelta a la base. |
| **Ctrl+N** (tecla física) | Abre **la misma confirmación** (mismo texto) y su modal deja **el mismo pixel** que el de la entrada; cancelar deja el lienzo como estaba. |
| **F5** / **F10** (tecla física) | Rastro: `menu atajo=F5 orden=ContinueWorkflowCommand` y `menu atajo=F10 orden=StepNextCommand` (con el renglón `enrutado tecla=F5 consumido=True` del propio enrutador). |
| Cierre | Escena y tarjetas como al entrar; **preferencias del usuario byte-idénticas** (md5 `d5f199a068113d8a7e16ad6ee6f726b3`). |

> 🔎 **Un defecto del instrumento, medido y arreglado**: el driver usaba el índice **cacheado** del árbol
> (`qa_ajustes_uia.by_aid`) y tras abrir el modal seguía viendo el árbol anterior —«la entrada no está en el
> árbol» con el cajón desplegado—; `qa_dialogs_uia` ya tenía su propia lectura fresca y es la que usa ahora.
> El otro tropiezo fue del propio guion: elegir «Acerca de» **cierra el cajón** (la orden del núcleo pone
> `IsMenuOpen` en falso), así que hay que volver a desplegarlo para la entrada siguiente — lo que haría el
> usuario—.

## 7. Resultados medidos

| Comprobación | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 25 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 14) |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 9 `[OK]` · 0 `[FALLO]`** |
| Guardia del menú | **9 de 9** |
| Mutación nueva + las dos actualizadas | **3 de 3 MUERDEN** |
| Suite completa | **1912 superadas + 1 omitida de 1913, 0 errores** (2 m 26 s) |
| Sesión con la app abierta | **27 de 27 pasos** |

> 🔎 **Un rojo intermitente, atribuido y comprobado**: una de las corridas de cierre dejó
> `SystemPerformanceMonitorTests.TheHeartbeat_ShouldNotOverlap_WhenTwoTicksCoincide` en rojo; **pasa 1 de 1 en
> aislamiento** (31 ms) y es el mismo tipo de ruido de carga que el hito 255 ya documentó para ese caso y para
> `EngineFirstRunTests.FirstRun…`. La corrida final de la suite quedó **verde** con el árbol exacto que se
> entrega. No es regresión de este trabajo: ni ese caso ni el de hilos tocan el menú, el host Uno ni
> `ControlBarViewModel`.

## 8. Fronteras declaradas

1. **Cinco entradas siguen pendientes**, cada una con su razón escrita: el **Estudio de temas** (edita temas
   por secciones), el **panel de métricas**, el **explorador VFS**, el **diseñador de dataset** (ventana del
   plugin de sistema de archivos) y el **aviso de actualización** (este host no comprueba actualizaciones, así
   que no hay novedad que anunciar).
2. **«Acerca de» es modal aquí y ventana en el escritorio**: el host no abre una segunda ventana de Windows;
   sirve la misma información como superficie dentro de la suya. Diferencia declarada, no disimulada.
3. **Las mismas órdenes del núcleo siguen pidiendo el contrato síncrono**: el host las cumple por su canal,
   no cambiando el contrato. Un host nuevo (Linux/macOS) tendrá la misma frontera y las mismas dos piezas
   portables para cruzarla.
4. **Ctrl+O y Ctrl+S no se pulsaron con tecla física en la sesión** (abren el picker del sistema, que se
   lleva la sesión de UIA): su camino está atado por la guardia (la tabla y los métodos portables) y su mitad
   sin diálogo se ejerció por la sonda (`SaveWorkflowToFileAsync` + `LoadWorkflowFromFileAsync`, medidos).
5. **El diálogo de confirmación del host usa botones `OK`/`Cancel`**, como el adaptador de diálogos que ya
   existía; sus rótulos no se copiaron del escritorio porque el escritorio no tiene esa confirmación con esos
   textos (usa su propio `IDialogService`).
