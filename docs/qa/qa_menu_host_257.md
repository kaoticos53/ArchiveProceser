# QA — El Menú Principal del Host Uno (hito 257)

El host Uno tenía lienzo, paneles, atajos y ajustes, pero **no la barra de control del escritorio ni sus
menús**. Este tramo porta esa superficie —la barra y su cajón— sobre el **mismo `ControlBarViewModel`
portable** del núcleo, con paridad de **entradas, órdenes, estado por contexto y atajos**, y sin tocar
ninguna otra superficie.

- **Evidencia de la sesión con la aplicación abierta**: `docs/qa/qa-manual-268/` (18 capturas rotuladas,
  `timeline.jsonl`, `watch.log`, `menu-session.json`).
- **Instrumento**: `docs/qa/qa_menu_uia.py` (driver externo por UIA, que reutiliza el fontanero de
  `qa_ajustes_uia.py` y el instrumento de píxeles de `qa_manual_session.py`).

---

## 1. Qué se construyó (y qué no se tocó)

| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/ControlBar.xaml(.cs)` | La barra del escritorio: marca, botón «Menú» y **tres islas** (modos · ciclo · herramientas) con **14 entradas** ancladas por `AutomationId`. |
| `FileFlow.App.Uno/Controls/MainMenuDrawer.xaml(.cs)` | El cajón: velo + panel de 320 px, con el mismo estado (`IsMenuOpen`) que el botón «Menú». |
| `FileFlow.App.Uno/MainWindow.xaml(.cs)` | El montaje: `Bar.Vm = Drawer.Vm = mainVm.ControlBar`, las dos entradas de ajustes al mismo `Settings.Open()`, y el Inspector conmutando la columna derecha del marco. |
| `FileFlow.App.Uno/RuntimeSelfCheck.cs` | `RunControlBarProbe` (14 comprobaciones), en modo propio. |
| `FileFlow.App.Uno/App.xaml.cs` | El modo `--selfcheck-controlbar`. |
| `run-uno.ps1` / `run-uno-fast.ps1` / `AGENTS.md` | El switch `-SelfCheckControlBar`, que espera y **hereda el código de salida**. |

**Cero líneas en `FileFlow.App`** (la app de escritorio sólo se usó como referencia de comportamiento) y
**ninguna clave `Uno_*` nueva inventada**: los **30** textos de la barra y del cajón se copian del
diccionario del escritorio, clave por clave, en los dos idiomas.

## 2. La paridad, declarada (lo que no llega, nunca fingido)

Censo de **19 filas**: AutomationId, vista, **dónde vive la orden** (code-behind si es un comando, XAML si
es un enlace bidireccional) y **estado por contexto**. Frente a él, el escritorio se lee en sus **dos modos
de enlace** y cada orden suya tiene destino:

| Destino | Órdenes |
| :--- | :--- |
| **Dibujadas aquí (13)** | `ToggleMenuCommand`, `ToggleWatchModeCommand` y `IsDryRun` (Modo Prueba y Vigilante), `ExecuteWorkflowCommand`, `DebugWorkflowCommand`, `StepNextCommand`, `ContinueWorkflowCommand`, `TogglePauseCommand`, `StopWorkflowCommand`, `UndoCommand`, `RedoCommand`, `RollbackLastExecutionCommand`, `ToggleInspectorCommand` |
| **Cumplida por el host (1)** | `OpenWorkflowSettingsCommand` → el **evento** `SettingsRequested` de la barra: el comando del núcleo abre una *ventana* por `IWindowService`, que aquí es el Nulo declarado (`HostOwnedOrders`) |
| **Pendientes (11)** | Nuevo / Cargar / Guardar Flujo (diálogo **síncrono**, fase 5.3), Estudio de temas, Métricas, Explorador VFS, Diseñador de dataset, Manual, Ejemplos, Acerca de, aviso de actualización (`DeclaredPendingEntries`) |
| **Atajos no enrutados (6)** | F5, F10, Shift+F5 (ciclo) y Ctrl+N, Ctrl+O, Ctrl+S (flujo): el host enruta **sólo** los del lienzo (`EditorKeyboardShortcuts`) (`DeclaredUnroutedShortcuts`) |

> 🔎 **Un defecto del propio instrumento, encontrado al escribir la guardia**: la primera lectura de las
> órdenes del escritorio miraba sólo `{Binding XCommand}` (la barra) y **no** `{Binding ControlBar.XCommand}`
> (su ventana, donde vive el cajón): la mitad del menú quedaba invisible para la guardia y con ella
> `OpenWorkflowSettingsCommand`. Se corrigió la lectura y el caso se **ata contra el vacío** (tiene que
> encontrar, como poco, el botón de menú y la orden de ajustes del escritorio): una lectura que no
> encuentra nada no puede pasar por paridad.

## 3. Guardia y mutaciones

**`FileFlow.Tests/Unit/App/UnoControlBarParityGuardTests.cs` — 8 de 8 casos**: censo con ancla y estado; cada
entrada con su orden **en el artefacto que la posee** y sin reimplementar el ciclo; el montaje compartido con
el VM portable; la paridad de órdenes con las tres tablas **disjuntas**; los 30 textos idénticos al
escritorio en los dos idiomas; el diccionario del host sin claves huérfanas; la sonda en modo propio; y los
atajos declarados contra la tabla canónica del lienzo.

| Mutación | Muta | Testigo (rojo) | Control (verde) |
| :--- | :--- | :--- | :--- |
| `menu-que-ejecuta-la-orden-de-otro` | `ControlBar.xaml.cs` | `EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt` | `EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState` |
| `menu-que-no-declara-lo-que-falta` | `ControlBar.xaml.cs` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` | `EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt` |
| `menu-sin-el-estado-de-su-contexto` | `ControlBar.xaml` | `EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` |
| `menu-que-no-declara-un-atajo` | `ControlBar.xaml.cs` | `EveryDesktopShortcut_ShouldBeRoutedHere_OrDeclaredUnrouted` | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` |

**Las cuatro MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes y recompilado). COVERAGE →
**66 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda **14 de 43**.

## 4. Medición con la aplicación abierta (sesión 268, 16 de 16 pasos)

La versión vigente del host tenía el tema del usuario (`pastel_spring`, claro), que es lo que hace visible el
**velo** del cajón. El driver actuó sobre los controles reales por `Invoke` / `TogglePattern` mientras el
vigilante medía el pixel cada ~0,8 s.

| Paso | Medición |
| :--- | :--- |
| Línea base | La barra expone **10 de sus 14** entradas. Las 4 que faltan son **exactamente** las de contexto: `Step`, `Continue` (depuración), `Pause`, `Stop` (ciclo en marcha). El cajón: **0 de 5**. Deshacer/Rehacer **deshabilitados** (`CanUndo`/`CanRedo` del editor). Pixel central `#FCF8F8` (77,8 %); derecha `#FCFCFC` (94,8 %). |
| Pulsar **«Menú»** | El cajón aparece: **5 de 5** anclas en el árbol. El **velo se ve en el pixel**: banda central `#FCF8F8` → **`#585454` (99,9 %)**. |
| Pulsar **«Ajustes»** del cajón | La superficie de ajustes del host se abre: **6 de 6** anclas (`SettingsTab*`, `SettingsSaveButton`, `SettingsCloseButton`). La entrada del cajón y la de la barra abren **la misma**. |
| Cerrar el cajón | Sus **5** entradas salen del árbol y el pixel central **vuelve al de la línea base** (`#585454` → `#FCF8F8`, 77,8 %). |
| Pulsar el **Inspector** | La columna derecha **pasa a ser lienzo**: 0,0 % → **93,1 %** de la banda con el color del fondo. Insistiendo: **0,0 %**. |
| **Modo Prueba** | La casilla conmuta por `TogglePattern`: **1 → 0**, y se devuelve a su estado original. |
| Deshacer deshabilitado | El canal externo lo lee **deshabilitado** y al invocarlo el control **rechaza** la orden. |
| Cierre | Centro y derecha vuelven a los valores de la línea base. |

**La línea de tiempo del vigilante lo corrobora** por su cuenta: las 3 tarjetas detectadas pasan a **0**
mientras el velo cubre la escena y vuelven a **3** al recogerse, con **20 cambios materiales** registrados.

**El fichero de preferencias del usuario queda byte-idéntico** (md5 `c0ff8a204b46ed6519f6af7aad3d2a74` antes y
después): el menú no escribe nada.

## 5. Resultados medidos

| Comprobación | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 14 `[OK]` · 0 `[FALLO]`** · VERIFICADO |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 9 `[OK]` · 0 `[FALLO]`** |
| `.\run-uno.ps1 -NoBuild -SelfCheckControlBar` | **EXIT 0** · VERIFICADO (el switch nuevo hereda el código de salida) |
| Guardia del menú | **8 de 8** |
| Mutaciones 64.ª-67.ª | **4 de 4 MUERDEN** |
| Suite completa | **1902 superadas + 1 omitida de 1903, 0 errores** (RC 0) |
| Sesión con la app abierta | **16 de 16 pasos** |

## 6. Fronteras declaradas

1. **Las 11 entradas pendientes y los 6 atajos**: declarados en el control con su razón y atados por la
   guardia. No se dibuja un botón que no puede hacer nada, ni se deja una tecla que no hace nada sin decirlo.
2. **El Inspector**: el host arranca con el panel **abierto** (es una columna del marco, como hasta ahora) y
   su entrada lo conmuta. El estado inicial es decisión del marco; **la conmutación sí es la del núcleo**.
3. **Los desplegables de tema e idioma del cajón**: se ejercen por su **presencia** en el árbol, su **enlace
   bidireccional** y su **catálogo** (guardia y sonda). Su **selección en vivo** es la del
   `ControlBarViewModel` portable —write-through— y **no se tocó en esta sesión** para no escribir el fichero
   de preferencias del usuario: esa misma ruta ya está medida con la app abierta en la sesión de los ajustes.
4. **`MainMenuDrawer` y `DrawerScrim` llevan su `AutomationId` en un `Border`**, que **no tiene peer** de
   automatización (la lección de la sesión 267): la presencia del cajón se prueba por sus **entradas**, que
   sí son controles.
5. **Sigue pendiente lo que nombró el usuario**: los **paneles que algunos nodos tienen** —diálogos de
   parámetros y selectores de variables, la fase 5.3—, y del menú del escritorio, las once entradas y los
   seis atajos de arriba.
