# QA — Los Paneles de Nodo del Host Uno (hito 258)

El host Uno ya tenía lienzo, paneles, atajos, ajustes y barra de control, pero **los paneles que cada nodo
tiene dentro** —el editor de texto del parámetro largo y el selector de variables— seguían cayendo al
**Nulo declarado**: el botón «✎» y el botón «{x}» existían y **no hacían nada**. Este tramo escribe el
`IWindowService` del host, las dos vistas sobre los **view models portables del núcleo** y el anclaje que
hace que las filas del inspector los alcancen.

- **Evidencia de la sesión con la aplicación abierta**: `docs/qa/qa-manual-268/` (capturas `40_…`–`45_…`,
  `timeline.jsonl`, `watch.log`, `dialogos-session.json`).
- **Instrumento**: `docs/qa/qa_dialogs_uia.py` (driver externo por UIA, que reutiliza el fontanero de
  `qa_manual_session.py` y el de `qa_ajustes_uia.py`).
- **Sonda**: `--selfcheck-dialogs` → `selfcheck-dialogs-report.txt` (**24 `[OK]`**).

---

## 1. Qué se construyó (y qué no se tocó)

| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Platform/UnoWindowService.cs` | El `IWindowService` real del host: `ShowDialogAsync` por `DialogKeys` con `ContentDialog` y `DialogResultPayload`, `MainWindowOwner` real, y las dos tablas del censo (`ImplementedDialogs` = **2 servidas**; `DeclaredPendingDialogs` = **7 declaradas con su razón**). Lo que este host no sirve **no se cancela mudo**: se escribe en `DeclinedDialogs` y en la consola de la aplicación (`Decline`), porque un «cancelado» sin traza se lee como un error del usuario. |
| `FileFlow.App.Uno/Controls/TextEditorDialogBody.xaml(.cs)` | La vista del `TextEditorDialogViewModel` **portable**: caja `TextEditorBox` (`TwoWay`), los botones de insertar variable y limpiar, y el panel lateral del **propio VM** (WinUI no admite dos `ContentDialog` a la vez), con inserción por `vm.InsertTokenAt(caret, token)`. El valor confirmado sale de `vm.SaveResult()`. |
| `FileFlow.App.Uno/Controls/VariablePickerDialogBody.xaml(.cs)` | La vista del `VariablePickerViewModel` **portable** (grupos, nodo objetivo y contexto de vista previa que ya trae el núcleo): lista con selección `TwoWay`, buscador que filtra en caliente, recuento, detalle del token y botón de copiar; el modal cierra con `RequestClose` y devuelve `vm.SelectedToken`. |
| `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | Las **acciones de fila**: `HostRowActions` (**3 dibujadas**: explorar ruta «…», editor «✎», catálogo «{x}») y `DeclaredPendingRowActions` (**3 declaradas**), con sus anclas `ParamBrowse_` / `ParamEditor_` / `ParamVariable_`. |
| `FileFlow.App.Uno/App.xaml.cs` | Registra `services.AddSingleton<IWindowService, UnoWindowService>()` **y ancla** `ServiceHolders.WindowService` — de ahí lo leen los `NodeParameterViewModel` que el inspector construye sin recibir servicios por constructor. Arranca el modo `--selfcheck-dialogs`. |
| `FileFlow.App.Uno/RuntimeSelfCheck.cs` | `RunDialogsProbe` (**24 `[OK]`**), en **modo propio**. |
| `FileFlow.App.Uno/Resources/Strings.resx` (+`.es`) | Las claves `Uno_Dialog_*` de los dos diálogos, **copiadas del escritorio** clave por clave. |
| `run-uno.ps1` / `run-uno-fast.ps1` / `AGENTS.md` | El switch `-SelfCheckDialogs`, que espera y **hereda el código de salida**. |

**Cero líneas en `FileFlow.App`** (el escritorio sólo se usó como referencia de comportamiento).

## 2. La paridad, declarada (lo que no llega, nunca fingido)

- **Censo de diálogos**: las **9** claves de `DialogKeys` se reparten entre **2 servidas** (`TextEditor`,
  `VariablePicker`) y **7 declaradas** con su razón (`UpdateDialog`, `WorkflowSettings`,
  `VirtualFileSystemExplorer`, `About`, `WorkflowMetricsDashboard`, `ThemeCustomizer`, `AiModelUrlsConfig`).
  La guardia compara el censo **contra las constantes del SDK**: una clave nueva sin destino cae en la
  tabla y el caso se pone rojo.
  > `WorkflowSettings` se declara **a propósito**: la superficie de ajustes del host (hito 255) tiene su
  > punto de entrada en la barra y el cajón, y abrirla por este camino sería una **segunda copia** de lo
  > mismo.
- **Censo de acciones de fila**: el «{x}» del escritorio despliega un **menú emergente** de variables; el
  host **no tiene menú emergente** y abre **directo el catálogo completo**, que es su primera entrada. El
  resto de las órdenes de fila del escritorio (`OpenMediaPresetManagerCommand`, `OpenPasswordManagerCommand`)
  abren ventanas que este host no tiene: declaradas.

## 3. 🔴 El defecto REAL que destapó el driver (y que se arregló)

Las cajas de texto de las filas del inspector **sólo escribían en un sentido**: del campo al parámetro
(ida). Cuando el valor lo escribía **el diálogo** —insertar `{FileName}` desde el catálogo—, el parámetro
del nodo cambiaba pero **el campo seguía mostrando el texto viejo**. El usuario habría visto su inserción
desaparecer de la pantalla. Se mide en la sesión: el campo pasa de `''` a `'{FileName}'` **después** del
arreglo.

**Arreglo** (`NodeInspectorPanel.xaml.cs`): `WireBoxToParameter(TextBox, NodeParameterViewModel)` (ida +
escucha de `Value` → `box.Text = value;`) y una lista `_rowValueSubscriptions` de suscripciones que se
sueltan en `RebuildParameters()` (sin ella, reconstruir el inspector dejaría escuchas huérfanas). Aplicado
a las **tres** cajas: estándar, multilínea y ruta con explorar.

## 4. Guardia y mutaciones

**`FileFlow.Tests/Unit/App/UnoNodeDialogsGuardTests.cs` — 9 de 9 casos**: el censo de claves (servidas +
declaradas = todas las de `DialogKeys`); cada pendiente **contestada con su razón** y no con un cancelar
mudo; las órdenes de fila del escritorio con destino (dibujadas o declaradas); las acciones dibujadas **en
las mismas filas** que el escritorio (leído de `FileFlow.App/Themes/Templates/NodeParameterTemplates.axaml`);
el **atado bidireccional** de las cajas de texto; los diálogos como **vistas de los VMs portables** (no
copias: `vm.SaveResult()`, `vm.InsertTokenAt`); los **20 textos** copiados del escritorio en los dos
idiomas; el diccionario del host sin claves huérfanas; y la sonda en **modo propio**.

| Mutación | Muta | Testigo (rojo) | Control (verde) | Tiempo |
| :--- | :--- | :--- | :--- | :--- |
| `panel-de-nodo-sin-su-servicio-de-ventanas` | `App.xaml.cs` (el anclaje) | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` | `TheDialogs_ShouldBeViewsOfThePortableViewModels_NotCopiesOfThem` | 34,8 s |
| `fila-de-variables-que-abre-el-menu-que-no-esta-portado` | `NodeInspectorPanel.xaml.cs` | `EveryDesktopRowDialog_ShouldBeServedHere_OrDeclaredPending` | `EveryRowAction_ShouldBeDrawnOnTheSameRowsAsTheDesktop` | 30 s |
| `editor-que-no-devuelve-el-texto-confirmado` | `UnoWindowService.cs` | `TheDialogs_ShouldBeViewsOfThePortableViewModels_NotCopiesOfThem` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` | 29 s |
| `campo-que-no-muestra-lo-que-el-dialogo-escribio` | `NodeInspectorPanel.xaml.cs` | `EveryTextRow_ShouldFollowTheParameter_BothWays` | `EveryRowAction_ShouldBeDrawnOnTheSameRowsAsTheDesktop` | 28 s |

**Las cuatro MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes y recompilado).
`mutations/COVERAGE.md` regenerado por su guardia → **70 declaraciones**, 15 de 17 subsistemas, guardias
que auditan el repositorio **con mutación que las muerda: 15 de 44**.

> 🔎 **La cuarta mutación nació sobreviviente, y eso fue información**: la primera versión (quitar la
> escucha `p.PropertyChanged += OnParameterChanged;`) **SOBREVIVIÓ**, así que la guardia se endureció
> hasta exigir el atado completo (`p.PropertyChanged += OnParameterChanged;` **y**
> `_rowValueSubscriptions.Add((p, OnParameterChanged));`) — y entonces mordió. Una guardia que no muerde
> es una guardia que no probaba lo que decía probar.

## 5. Medición con la aplicación abierta (sesión 268, 25 de 25 pasos)

Driver externo por UIA sobre los controles reales, con el **vigilante** de píxeles en paralelo.

| Paso | Medición |
| :--- | :--- |
| Línea base | `tarjetas=3`, pixel central `#FCF8F8` (77,8 %), inspector abierto, **0 filas `Param*`** (el inspector no dibuja parámetros hasta que hay nodo elegido). |
| Clic en la tarjeta **`Folder Source`** | Aparecen **10 anclas `Param*`**: `ParamBox_ExtensionFilter`, `ParamVariable_ExtensionFilter`, `ParamBox_SourcePath`, `ParamBrowse_SourcePath`, `ParamDropdown_EmitMode`, `ParamToggle_Recursive`, `ParamToggle_WatchRealtime`, `ParamBox_MaxRecursionDepth`, … |
| Pulsar **«{x}»** de `ExtensionFilter` | Abre el **catálogo**: `VariablePickerList`, `VariablePickerSearchBox`, `VariablePickerCountText`, `VariablePickerDetailToken`; y **el modal se ve en el pixel**: el centro pasa de `#FCF8F8` a **`#B0ACAC` (52,6 %)**. |
| Buscador `'Guid'` → borrar | «Mostrando **1** de 47 variables» → «**47** de 47»: el filtro del VM portable trabaja en caliente. |
| Elegir la fila **`{FileName}`** | El detalle lee **`'{FileName}'`** y «Insertar Variable» queda habilitado. |
| **«Insertar Variable»** | El modal cierra (el pixel vuelve a `#FCF8F8`) y **el campo pasa de `''` a `'{FileName}'`** — la vuelta que faltaba (§3). Se devuelve a `''`. |
| Cajón → buscar `'Registrar'` → doble clic en **`Registrar Log`** | El nodo entra al lienzo (tarjeta nueva `L1959,T1085,R2099,B1105`) y el inspector expone `ParamBox_CustomMessage`, `ParamEditor_CustomMessage`, `ParamVariable_CustomMessage`, `ParamDropdown_LogLevel` y 3 toggles. |
| Pulsar **«✎»** | Abre el **editor de texto** (`TextEditorBox`, `TextEditorInsertVariableButton`, `TextEditorClearButton`), **sembrado con el valor de la fila** (`'prompt de la sesion 268'`), no vacío. |
| «Insertar Variable» del editor | Despliega su panel con el catálogo del VM (**13 variables**). |
| Escribir `'prompt de la sesion 268 + {FileName}'` + **«Guardar y Aplicar»** | **El campo del nodo queda con ese texto** y el pixel vuelve al de la línea base. |
| Limpieza | El nodo añadido se retira con **`Supr`** (`presente=False`, tarjetas 2 — el conteo incluye el título duplicado del inspector, coherente con la base) y el pixel final es `#FCF8F8`. |

**El fichero de preferencias del usuario queda byte-idéntico** (md5 `d5f199a068113d8a7e16ad6ee6f726b3`
antes y después): los paneles de nodo no escriben nada del usuario.

> 🔎 **Nota metodológica medida en esta sesión**: el **clic físico inyectado SÍ llega** al contenido de
> WinAppSDK (la casilla «Modo Prueba» conmuta 1→0→1 por `SetCursorPos` + `mouse_event`), a diferencia del
> clic mediado **por UIA**, que no. Queda escrito para quien retome el guion de gestos.

## 6. Resultados medidos

| Comprobación | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** · VERIFICADO |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 9 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 14 `[OK]` · 0 `[FALLO]`** |
| Guardia de los paneles de nodo | **9 de 9** |
| Mutaciones 68.ª-71.ª | **4 de 4 MUERDEN** (testigo rojo, control verde, árbol restaurado) |
| Suite completa | **1910 superadas + 1 omitida de 1912**; el único rojo de cada corrida fue **un test distinto y pesado** (`EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` en una, `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` en otra), **verde en aislamiento** (1/1 y 4/4) → **ruido de carga del entorno, no regresión** |
| Sesión con la app abierta | **25 de 25 pasos** |

## 7. Fronteras declaradas

1. **Las 7 claves de diálogo que este host no sirve** (Actualizador, VFS, Acerca de, Métricas, Estudio de
   temas, configuración de modelos de IA, y ajustes por esta vía): declaradas con su razón, con traza de lo
   pedido (`DeclinedDialogs`) y atadas por la guardia. **Un botón que no puede abrir nada no se dibuja.**
2. **El «{x}» de las filas abre el catálogo completo, no el menú emergente**: el host no tiene
   `IPopupMenuService` y la primera entrada de aquel menú **es** el catálogo. Es la diferencia declarada, no
   un olvido.
3. **`WorkflowSettings` no se sirve por `IWindowService`** aunque exista la superficie: su entrada es la
   barra y el cajón; una segunda puerta sería una segunda copia.
4. **Las ventanas que el host no tiene** (dashboard, VFS, gestor de presets de medios, gestor de
   contraseñas) siguen pendientes, cada una en su tabla.
5. **Sigue pendiente** de la migración: las **11 entradas** y los **6 atajos** del menú del escritorio
   (hito 257), las pestañas **Actualizaciones** y **Modelos de IA** de los ajustes (hito 255) y el
   **empaquetado/entrega** (fases 5.4-5.6 del plan de la rebanada 5).
