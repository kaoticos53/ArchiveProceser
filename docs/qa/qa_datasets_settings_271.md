# QA del hito 261 — El Diseñador de Datasets y las dos pestañas de ajustes que faltaban (host Uno)

Fecha: 2026-09-28 · Build: `6787` · Rama: `worktree-2026-09-23T10-05-23`

## 0. Qué se cerró

El hito 260 dejó **8 claves servidas + 2 declaradas** en el censo de `DialogKeys`, y de esas dos declaradas una —`DataSetDesigner`— era la única **entrada de menú** que seguía sin superficie. Este tramo cierra las dos superficies que el usuario nombró:

| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs` | **Contrato nuevo del SDK**: el NODO dice qué diálogo quiere (`DialogKey`) y qué contiene (`Payload`). La identidad del diálogo deja de decidirla el host. |
| `FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs` | Implementa el contrato: declara `DialogKeys.DataSetDesigner` y entrega su `SyntheticDataSetDesignerViewModel` **portable**. |
| `Controls/DataSetDesignerBody.xaml(.cs)` | La vista del host sobre ese view model. Cero lógica de producto: sus órdenes **son los comandos del VM del plugin**. |
| `Controls/SettingsPanel.xaml(.cs)` | Las dos pestañas que faltaban —**Modelos de IA** y **Actualizaciones**— sobre sus secciones portables. |
| `Controls/MainMenuDrawer.xaml(.cs)` | La entrada **Diseñador de Datasets** (el cajón pasa de 14 a **15 entradas**). |
| `Controls/ControlBar.xaml.cs` | `DeclaredPendingEntries` queda **VACÍA**: ya no hay ninguna orden de menú del escritorio sin dibujar, declarar o cumplir por el host. |
| `Platform/UnoWindowService.cs` | `DataSetDesigner` **servida** con su vista. `AiModelUrlsConfig` sigue declarada, con su razón **corregida** (ver §7). |

## 1. Compilación

```
MSBuild de VS 18 (los targets de WinAppSDK no corren con dotnet build)
FileFlow.App.Uno -> bin\Debug\net10.0-windows10.0.19041.0\FileFlow.App.Uno.dll
0 errores
```

## 2. Las cuatro sondas (una corrida por comprobación, con el binario final)

| Sonda | Comando | Resultado |
| :--- | :--- | :--- |
| Lienzo | `.\run-uno.ps1 -SelfCheck -NoBuild` | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| Menú | `.\run-uno.ps1 -SelfCheckControlBar -NoBuild` | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 37) |
| Paneles de nodo | `.\run-uno.ps1 -SelfCheckDialogs -NoBuild` | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** |
| Ajustes | `.\run-uno.ps1 -SelfCheckSettings -NoBuild` | **EXIT 0 · 12 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 9) |

Los pasos nuevos, tal como quedaron escritos en su informe:

- **Menú (42)**: `la entrada «Diseñador de Datasets» del cajón abre la superficie del host sobre el view model del plugin (7 datasets en el catálogo, 'Cómics y Manga (Oficial)' con 40 elementos; abierto=True)`.
- **Ajustes (12)**: `la superficie de ajustes se abre con sus seis secciones` · `la sección «Modelos de IA» enseña el catálogo del gestor del núcleo, su carpeta y su estado` · `la sección «Actualizaciones» enseña la versión, el formato, los canales y la comprobación automática del view model portable`.

## 3. Guardias

| Guardia | Antes | Ahora |
| :--- | :--- | :--- |
| `UnoControlBarParityGuardTests` | 12 | **13 de 13** |
| `UnoSettingsSurfaceGuardTests` | 11 | **13 de 13** |
| `UnoNodeDialogsGuardTests` | 8 | **9 de 9** |
| `UnoShortcutParityGuardTests` | 5 | **5 de 5** |

Dos guardias del repositorio salieron **rojas al cambiar el producto** y se arreglaron, que es la señal de que las tablas de declaración son producto vigilado y no prosa:

- `UnoHostFreeOfAvaloniaGuardTests`: una razón declarada nombraba el ensamblado del escritorio.
- `DeferredWorkInventoryGuardTests`: la comprobación de actualizaciones del arranque entra como espera `RealTime`, con su motivo registrado.

## 4. Mutaciones (todas MUERDEN, con testigo rojo y control verde)

| Mutación | Hito | Verdicto | Tiempo |
| :--- | :--- | :--- | :--- |
| `nodo-que-declara-su-superficie-sin-clave` | 261 | **MUERDE** | 33,3 s |
| `disenador-de-datasets-fuera-del-catalogo` | 261 | **MUERDE** | 28,9 s |
| `vista-del-disenador-con-su-propio-modelo` | 261 | **MUERDE** | 28,4 s |
| `seccion-que-pierde-el-panel-que-conmutaba` | 261 | **MUERDE** | 28,5 s |
| `accion-de-urls-por-modelo-sin-declarar` | 261 | **MUERDE** | 31,9 s |
| `menu-que-no-declara-lo-que-falta` | 257→**reapuntada** | **MUERDE** | 28,0 s |

La última nació de lo que este tramo encontró **a ojo**: la razón declarada de `AiModelUrlsConfig` había quedado falsa y nadie la vigilaba. Su mutante dibuja una tercera acción en la fila de modelos (la edición de URLs) **sin tocar el censo**: el host sigue funcionando y la declaración deja de ser cierta —exactamente lo que pasó cuando la pestaña llegó y la razón no se revisó—. Testigo rojo (1 de 1 cae) y control verde (1 de 1 pasa), con el árbol restaurado por hash.

Declaradas en el repositorio: **80**. El ejecutor restaura, recompila y verifica por hash antes de salir.

## 5. Suite completa

```
dotnet test FileFlow.Tests/FileFlow.Tests.csproj
Correctas! - Con error: 0, Superado: 1917, Omitido: 1, Total: 1918 — 2 m 26 s
```

(1916 → **1917** superadas en el tramo: la guardia gana el caso nuevo de §7.1.)

La omitida es la de siempre (`SemanticEmbeddingEngine_ClassifyZeroShot_WithClipModel_ShouldScoreEnglishAndSpanishCategories`: exige el modelo CLIP en disco). No apareció el fallo intermitente en esta corrida.

## 6. Ejercido en la aplicación abierta (sesión 271): 39 de 39 pasos VERIFICADO

Driver externo por **UIA** (`docs/qa/qa_windows2_uia.py`) + vigilante de píxeles. Las dos ventanas que el pase anterior no había ejercido, más las dos nuevas:

| Superficie | Cómo se abrió | Qué apareció (medido) | Qué queda al cerrar |
| :--- | :--- | :--- | :--- |
| **Explorador VFS** | botón *VFS* de la barra (chip `📁 \| VFS (4)`) | 5 anclas UIA (`VfsSearchBox`, `VfsFileList`, `VfsTotalFiles`, `VfsRefreshButton`, `VfsCloseButton`) y **4 filas** con nombre real (`Sembrado 01..04.mkv`); centro de píxel `#B0ACAC` | base `#FCF8F8`, **0 filas** |
| **Aviso de actualización** | distintivo de la barra (`🚀 \| v9.9.9`) | 6 anclas (`UpdateCurrentVersion`, `UpdateNewVersion`, `UpdateReleaseNotes`, `UpdateRemindButton`, `UpdateInstallButton`, `UpdateSkipButton`); versión actual `1.0.0-beta+build.6759`, nueva `9.9.9`; centro `#B0ACAC` | superficie cerrada (`#FCF8F8`) y el **distintivo sigue puesto** |
| **Diseñador de Datasets** | entrada del cajón | 8 anclas (`DataSetSearchBox`, `DataSetList`, `DataSetTabTree`, `DataSetTabDsl`, `DataSetTabJson`, `DataSetAddFileButton`, `DataSetRemoveNodeButton`, `DataSetCloseButton`) y **7 filas** de dataset; centro `#B0ACAC` | `#FCF8F8` |
| **Ajustes, dos pestañas nuevas** | botón *Ajustes* | **6 secciones** con su rótulo; pestaña **Modelos de IA**: **24 filas**, carpeta `…\FileFlow\models`; pestaña **Actualizaciones**: versión `1.0.0-beta+build.6759`; centro `#585454` | `#FCF8F8` |

`preferencias.md5` **antes == después** (`d5f199a068113d8a7e16ad6ee6f726b3`): la sesión no dejó rastro en las preferencias del usuario.

## 7. Tres cosas que se vieron al medir (y se arreglaron)

1. **La razón declarada de `AiModelUrlsConfig` había quedado falsa, y ahora está VIGILADA.** Decía «pertenece a la pestaña de modelos de IA de los ajustes, **que este host todavía no tiene**» — y este tramo **sí** le dio esa pestaña. La razón se reescribió para decir lo que de verdad pasa: la pestaña existe, lista el catálogo y gestiona descargas, pero **no ofrece la edición de URLs por modelo desde la fila**, que es la acción con la que el escritorio abre ese diálogo; sin punto de entrada que lo pida, servirlo sería una ventana que nadie puede abrir. Una razón obsoleta miente igual que un no-op mudo.

   Lo importante: la había encontrado **el ojo**, y algo que sólo caza el ojo vuelve a pasar. Así que las dos mitades quedan **atadas por una prueba** —`TheAiModelRowActions_ShouldMatchWhatTheDialogCensusDeclares`, en `UnoSettingsSurfaceGuardTests` (11 → **13 casos**)— que exige que la fila de modelos dibuje **exactamente** descargar y borrar **y** que la clave siga declarada con una razón que **no** pueda decir que la pestaña no existe. Si alguien dibuja la acción de URLs (o la sirve), la declaración deja de ser cierta y el caso cae **nombrando la clave**. La mutación **`accion-de-urls-por-modelo-sin-declarar`** dibuja esa tercera acción **sin tocar el censo** —el modo de fallo exacto que produjo la razón obsoleta— y **MUERDE** (31,9 s; testigo rojo 1 de 1, control verde 1 de 1).
2. **El comentario de cabecera del servicio de ventanas** seguía diciendo que el Estudio de temas, las Métricas y el VFS «no se dibujan en las vistas». Ya se dibujan desde el 260. Reescrito.
3. **La tabla de entradas pendientes del host quedó VACÍA** y eso también es un dato: ya no hay ninguna orden del menú del escritorio sin dibujar, declarar o cumplir por el host.

## 8. Hallazgo sobre el ESCRITORIO (no se toca)

El botón **Guardar** del `AiModelUrlsConfigDialog` del escritorio enlaza `{Binding SaveCommand}`, y el `AiModelUrlsConfigViewModel` **no expone `SaveCommand`**: expone un método `Save()` sin `[RelayCommand]`. El enlace no resuelve, así que ese botón no hace nada — el diálogo del escritorio no puede guardar. **No se toca en este tramo** (es del escritorio y no lo pidió nadie), pero queda anotado porque afecta a cualquier intento de portar esa ventana «con paridad»: primero habría que decidir cuál es el comportamiento correcto.

## 9. Fronteras declaradas

- **`WorkflowSettings` (clave del catálogo) sigue declarada y no servida**: la superficie de ajustes de este host tiene su punto de entrada en la barra y el cajón, y abrirla además por `IWindowService` sería una SEGUNDA copia de lo mismo. La orden del escritorio se cumple por el canal propio del host (tabla `HostOwnedOrders`).
- **`AiModelUrlsConfig` sigue declarada** por la razón de §7: sin la acción por fila en la pestaña, no hay quién pida la ventana.
- **El Diseñador de Datasets se sirve por el contrato del SDK, no por el comando canónico**: el comando del escritorio abre la ventana que monta el propio plugin con el toolkit del escritorio, y un host WinUI no puede montar una ventana ajena. El cajón lo cumple con su evento propio y el host pinta la superficie del nodo.
- **El `FileInfoText` del diseñador no se dibuja**: el escritorio lo rellena desde su catálogo de modelos y aquí no hay fuente; se dibujan los cuatro campos que el `VirtualFileEntry` del núcleo sí expone.
- Fuera de alcance del tramo (siguen sin portar): gestores de **presets de medios** y **contraseñas**, el **empaquetado y la entrega** (5.4-5.6) y el **diálogo de URLs por modelo**.
