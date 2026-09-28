# QA del hito 260 — Las ventanas que faltaban del menú (host Uno)

**Fecha:** 2026-09-28 · **Rama:** `worktree-2026-09-23T10-05-23` · **Host:** `FileFlow.App.Uno`
(`net10.0-windows10.0.19041.0`, Debug, MSBuild de Visual Studio 18) · **Sesión con la app abierta:**
`docs/qa/qa-manual-270/` · **Driver:** `docs/qa/qa_menu3_uia.py`

## 0. Qué se cerró

El hito 259 dejó **cinco entradas declaradas** en `DeclaredPendingEntries`. Cuatro de ellas abrían una
VENTANA del escritorio y no tenían dónde ir en este host; este tramo las sirve **por el catálogo de
diálogos** (`DialogKeys`), sobre los view models PORTABLES del núcleo:

| Entrada del menú | Clave del catálogo | Vista del host | View model (núcleo) |
| :--- | :--- | :--- | :--- |
| Estudio de Temas (cajón) | `ThemeCustomizer` | `ThemeCustomizerBody` | `ThemeCustomizerViewModel` |
| Métricas y Rendimiento (cajón) | `WorkflowMetricsDashboard` | `MetricsDashboardBody` | `WorkflowMetricsDashboardViewModel` |
| Explorador Virtual / VFS (cajón y barra) | `VirtualFileSystemExplorer` | `VirtualFileSystemExplorerBody` | `VirtualFileSystemExplorerViewModel` (lo construye el host con la carga útil) |
| Aviso de actualización (barra) | `UpdateDialog` | `UpdateDialogBody` | `UpdateDialogViewModel` |

El censo del servicio pasa de **3 servidas + 6 declaradas** a **7 servidas + 2 declaradas**
(`WorkflowSettings` —la superficie de ajustes del host ya tiene su punto de entrada en la barra y el
cajón; abrirla por este camino sería una segunda copia— y `AiModelUrlsConfig` —pertenece a la pestaña de
modelos de IA de los ajustes, que este host no tiene—).

**La quinta entra** (Diseñador de Datasets) sigue declarada, ahora con la razón exacta: su ventana la
monta **el propio plugin** con el toolkit del escritorio (`SyntheticDataSetDesignerWindow`), y este host
es WinUI. Servirla pide una vista del host sobre el view model del plugin, que hoy vive dentro de su
ensamblado. Es una frontera de toolkit, no un olvido.

## 1. Compilación

| Comprobación | Resultado |
| :--- | :--- |
| `MSBuild.exe FileFlow.App.Uno.csproj -p:Configuration=Debug` | **0 errores** |

Tres compilaciones en el tramo: la primera destapó **8 errores** de tipos (los `using` que faltaban en la
sonda para los tipos del sistema de archivos virtual), la segunda compiló, y las dos siguientes son las
del emparejado con el producto (los nombres de automatización de las filas de las tres listas). Las cifras
de abajo son de la ÚLTIMA compilación, que es la que corrieron las sondas y la sesión.

## 2. Las cuatro sondas (una corrida por comprobación)

| Modo | Resultado medido |
| :--- | :--- |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 9 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 37 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 25) |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** |

Las **12 comprobaciones nuevas** del sondeo del menú, con lo que miden:

```
[OK] la entrada «Estudio de Temas» del cajón abre el estudio del host con el catálogo del núcleo
     (9 temas, 9 secciones, 34 ajustes editables)
[OK] y su editor escribe en el tema ('AccentPrimary' '#EC4899' -> '#FF00AA' -> '#EC4899'), que es el
     mismo camino del usuario
[OK] y cerrarlo deja el host sin superficie abierta
[OK] la entrada «Métricas y Rendimiento» abre el panel del host con una fila por nodo del lienzo
     (3 de 3) y su duración formateada ('12,1 ms', pie: '3 nodos analizados. 0 cuello(s) de botella.')
[OK] y cerrarlo también la retira
[OK] el explorador VFS que pide el núcleo se sirve con el almacén de la carga útil
     (3 ficheros, 3 filas, 9 metadatos del seleccionado)
[OK] y cerrarlo la retira sin dejar rastro en el host
[OK] el chip del VFS aparece con el estado del view model y lleva su recuento ('VFS (7)')
[OK] el distintivo de actualización aparece al dejar una novedad pendiente, con su versión ('v9.9.9')
[OK] y su comando abre el AVISO DE ACTUALIZACIÓN del host, con la versión que se va a instalar
     ('9.9.9'; pulsado=True, abierto=True, clave=UpdateDialog)
[OK] y la orden «recordármelo luego» del propio aviso lo retira
[OK] y sin novedad pendiente el distintivo vuelve a esconderse
[OK] (y las de flujo / atajos de los hitos anteriores siguen verdes con el producto nuevo)
```

## 3. Guardia

`UnoControlBarParityGuardTests` pasa de **9 a 12 casos** (los tres nuevos):

1. `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` — la tabla `ServedWindowEntries` nombra las
   cuatro órdenes, cada una está **dibujada** (fuera de las tablas de declaración) y su clave está en el
   catálogo; y **toda** clave de `DialogKeys` (leída del SDK) está en la tabla de servidas o en la de
   declaradas.
2. `TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt` — las cuatro partes del estudio del
   escritorio que este host no sirve están declaradas **con su razón** y no dibujadas, y las que sí se
   dibujan ejecutan los comandos del view model portable (no una copia).
3. `TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes` — el arranque **arranca** la comprobación (la
   llamada, no sólo la definición), la entrega a la ventana por el mismo camino del escritorio
   (`ControlBar.SetPendingUpdate`) y se salta entera en los modos de sondeo.

`UnoNodeDialogsGuardTests` sigue en **9 de 9**: su caso de censo es genérico (servidas + declaradas cubren
todas las claves) y el host pasó de 3+6 a 7+2 sin tocarlo.

**Una debilidad de la guardia nueva, encontrada por la mutación** (no a ojo): la primera versión buscaba
las cuatro órdenes en el texto de las vistas, y la propia tabla `ServedWindowEntries` las nombra — así que
vaciar el manejador de una entrada no hacía caer el caso. Se añadió `WithoutDeclarationTables`, que mide
el código **sin** las tablas, y la mutación pasó de sobrevivir a morder.

## 4. Mutaciones (todas MUERDEN, con testigo rojo y control verde)

| Mutación | Qué borra | Testigo que muerde | Tiempo |
| :--- | :--- | :--- | :--- |
| `menu-que-no-declara-lo-que-falta` (retargeted) | la única fila de `DeclaredPendingEntries` (el diseñador de datasets) | `EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost` | 39,5 s |
| `ventana-servida-que-no-esta-en-el-catalogo` (nueva) | la fila del Estudio de temas en `ImplementedDialogs` | `TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared` | 28,5 s |
| `entrada-de-ventana-que-no-ejecuta-su-orden` (nueva) | el `Execute(null)` de Métricas en el cajón | `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` | 27,9 s |
| `aviso-de-actualizacion-que-nadie-enciende` (nueva) | la llamada `StartUpdateCheck(s_services)` | `TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes` | 27,3 s |
| `estudio-de-temas-que-esconde-lo-que-no-sirve` (nueva) | la fila de la vista previa en `DeclaredPendingParts` | `TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt` | 27,4 s |
| `menu-que-no-declara-un-atajo` (del 257, re-verificada) | la fila F5 de `RoutedShortcuts` | `EveryDesktopShortcut_ShouldBeRoutedHere_OrDeclaredUnrouted` | 27,3 s |
| `flujo-que-se-cumple-por-el-picker-sincrono` (del 258, re-verificada) | la línea del picker asíncrono en la ventana | `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` | 27,1 s |

`mutations/COVERAGE.md` regenerado: **75 mutaciones declaradas** (antes 71).

## 5. Suite completa

`dotnet test FileFlow.Tests/FileFlow.Tests.csproj` → **1915 superadas + 1 omitida de 1916, 0 errores**
(2 m 29 s). **Primera corrida verde**: en este tramo no hubo ningún rojo intermitente.

Dos guardias del repositorio **salieron rojas con el producto nuevo y se arregló el producto o su
registro** (no la guardia):

- `UnoHostFreeOfAvaloniaGuardTests.UnoHost_HasNoAvaloniaInCsharp`: una razón declarada decía «una ventana
  de Avalonia.» y el texto se buscaba en crudo. Reescrita la razón (la frontera es de toolkit, no hacía
  falta nombrar el ensamblado).
- `DeferredWorkInventoryGuardTests.EveryTimerOrWaitInProduction_ShouldBeExercisedOrExplained`: la espera
  nueva del arranque (`Task.Delay(3000)` antes de consultar actualizaciones) es un sitio nuevo del
  inventario. Registrado como `RealTime` con su motivo, **la misma decisión que la espera equivalente del
  escritorio** (`FileFlow.App/App.axaml.cs::StartBackgroundWork::Delay`).

## 6. Ejercido en la aplicación abierta (sesión 270): 25 de 25 pasos VERIFICADO

Driver externo por UIA (`docs/qa/qa_menu3_uia.py --session`), app lanzada y maximizada por
`qa_manual_session.py --launch`; capturas `60_…`–`99_…` y `menu3-session.json` en `qa-manual-270/`.

| Paso | Evidencia medida |
| :--- | :--- |
| Base | `tarjetas=3`, pixel centro `#FCF8F8` (77,8 %), **0 anclas** de las 3 entradas nuevas |
| «Menú» | **14/14 entradas** del cajón en el árbol; el velo se ve: `#FCF8F8` → **`#585454`** |
| «Estudio de Temas» | **6/6 anclas visibles** (`ThemeStudioThemeList`, `NameBox`, `StatusText`, `Apply`, `Save`, `Close`), **9 temas** leídos por UIA —«🌙 Oscuro Fluent (Predeterminado)», «☀️ Claro Minimalista»…— el título del modal leído por el canal externo es «Estudio de Personalización de Temas» (del diccionario del host), pixel **`#B0ACAC` 46,7 %**, y el cajón se recoge al elegir la entrada |
| Cerrar el estudio | sus 6 anclas desaparecen y el pixel vuelve a **`#FCF8F8` 77,8 %** |
| «Métricas y Rendimiento» | **4/4 anclas**, **3 filas** en la tabla (una por nodo del lienzo), pie leído por UIA «**3 nodos analizados. 0 cuello(s) de botella.**», pixel **`#B0ACAC` 49,4 %** |
| Cerrar el panel | anclas fuera y pixel de vuelta a la base |
| Preferencias | md5 **idéntico** antes y después: `d5f199a068113d8a7e16ad6ee6f726b3` |

## 7. Dos cosas que se vieron al medir (y se arreglaron)

1. **Las filas de las listas no tenían nombre para el canal externo**: UIA leía el NOMBRE DEL TIPO del
   elemento (`FileFlow.App.Themes.ThemeDefinition`, `FileFlow.App.ViewModels.NodeMetric`) en vez del tema
   o del nodo que la fila enseña. Se añadió `AutomationProperties.Name` a la plantilla de fila de las tres
   listas nuevas; ahora el canal externo lee los temas por su nombre (medido en la sesión 270).
2. **El peer de automatización no existe en los contenedores**: el `AutomationId` del cuerpo de cada
   ventana vive en su rejilla raíz, que no tiene peer, así que el canal externo ve sus CONTROLES pero no
   su ancla de contenedor. Queda declarado en el driver (`STUDIO_CONTAINER_ANCHORS`): esa ancla la lee la
   sonda en proceso, y la sesión externa mide las que un lector de pantalla alcanza.

## 8. Hallazgo sobre el ESCRITORIO (no se toca)

La rejilla del explorador VFS del escritorio declara dos columnas —«Tamaño» y «Modificado»— que
enlazan a `SizeText` y `ModifiedTimestamp`, propiedades que **no existen** en el `VirtualFileEntry` del
núcleo: se dibujan vacías en silencio. La vista del host dibuja los cuatro campos que sí existen
(nombre, ruta virtual, rol y nodo emisor). Es un hallazgo del escritorio, fuera del alcance de este
tramo, y queda anotado aquí para que no se pierda.

## 9. Fronteras declaradas

1. **Las cuatro ventanas son superficies modales dentro de la ventana del host**, no ventanas nuevas: es
   el mismo criterio que «Acerca de» (hito 258) y la misma diferencia declarada con el escritorio.
2. **El Estudio de temas no dibuja Eliminar, Importar ni Exportar** ni la vista previa en vivo, cada uno
   declarado en `DeclaredPendingParts` con su razón (los tres dependen del contrato SÍNCRONO de diálogos,
   que desde el hilo de UI devuelve «no»/nulo; la vista previa necesitaría una copia propia de tokens).
3. **El canal de la entrada del VFS desde el cajón con el almacén real de una ejecución** no se ejerció
   extremo a extremo en la sesión: el chip de la barra se mide con su estado de contexto y la ventana con
   un almacén construido por la sonda (mismo camino del servicio: `ShowWindow(VFS, store)`).
4. **El aviso de actualización se midió con una novedad sintética** (`v9.9.9`): en la aplicación normal
   sólo aparece cuando hay una release nueva de verdad. La comprobación de arranque es la del escritorio.
