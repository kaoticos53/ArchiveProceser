# Hito 263 — El Gestor de Presets de Medios del host Uno (y la puerta que le faltaba a la tarjeta)

**Fecha:** 28 de septiembre de 2026 · **Compilación:** 6818 → **6893** · **Sesión de la app abierta:** `docs/qa/qa-manual-263/`
**Driver:** `docs/qa/qa_presets_uia.py` · **Sin commit ni push.**

---

## 1. El encargo

Portar al host Uno (WinUI 3) la superficie del **Gestor de Presets de Medios** del escritorio, con paridad de
comportamiento: **sobre view models portables del núcleo**, con su **punto de entrada donde el escritorio lo
tiene** y **sin lógica de producto en la vista**. Si ya estuviera portada, declararlo con prueba y no rehacerla.

---

## 2. Lo que se encontró (y por qué se portó, en vez de declararlo)

El gestor **existía sólo como ventana Avalonia del plugin** —`FileFlow.Plugin.Integrations/UI/Views/
MediaPresetManagerWindow.axaml(.cs)`— con toda su lógica en el code-behind, y **no existía en el host Uno**. No
era un duplicado a evitar: era una superficie sin portar.

La decisión de diseño es la del **Diseñador de Datasets (hito 261)**, que ya estaba probada: la superficie la
**declara el NODO** con el contrato del SDK (`INodeDialogSurfaceProvider`) y cada host la **pinta con su propia
vista sobre el MISMO view model portable**. El contrato se amplió con `string? ReplacesCustomActionId` para unir
los dos botones del producto (el de la tarjeta del nodo y el de la fila del parámetro) con la superficie
declarada, en vez de que cada uno tirara por su cuenta.

---

## 3. Lo construido

| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.Sdk/Services/IWindowService.cs` | `DialogKeys.MediaPresetManager`: la clave del catálogo compartido. |
| `FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs` | `ReplacesCustomActionId`: qué acción personalizada sustituye la superficie (el hilo que une las dos puertas). |
| `FileFlow.Sdk/Descriptors/NodeCustomActionContext.cs` | `IDialogService? Dialogs`: los diálogos del host viajan al view model que la superficie construye. |
| `FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | El view model **portable** (sin toolkit): catálogo, formulario, alta, guardado, borrado, restablecimiento, normalización de la extensión y protección de los presets del sistema. **Es quien escribe en el almacén.** |
| `FileFlow.Plugin.Integrations/UI/Services/IMediaPresetStore.cs` | El contrato del almacén; `MediaPresetManagerService` (el real, el que lee el nodo al transcodificar) lo implementa. |
| `FileFlow.Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml(.cs)` | La ventana del **escritorio**, refactorizada a **vista del mismo view model**: sus manejadores de guardado/borrado propios desaparecieron. |
| `FileFlow.Plugin.Integrations/MediaTranscoderNode.cs` | Declara su superficie (`DialogKeys.MediaPresetManager`, `ReplacesCustomActionId => "ManageMediaPresets"`) y entrega el view model portable como carga útil. |
| `FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs` · `NodeViewModel.cs` | Las dos puertas abren la **superficie declarada** por el servicio de ventanas del host (o la acción del toolkit si el host no la sirve). |
| `FileFlow.App.Core/HostUi.cs` · `App.axaml.cs` · `App.xaml.cs` (Uno) | El catálogo de ventanas del host llega al núcleo; el host Uno fija además `CoreDialogHost.Services`. |
| `FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml(.cs)` | La **vista del host Uno** sobre el view model portable (lista, formulario, tres acciones y cierre). **Ni un cuadro suyo escribe en el almacén.** |
| `FileFlow.App.Uno/Platform/UnoWindowService.cs` | Sirve la clave con su vista: el censo de diálogos queda en **10 servidas + 1 declarada**. |
| `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | El botón «🎬» de la **fila del preset** (`ParamPreset_<clave>`) y la orden canónica `OpenMediaPresetManagerCommand`. |
| `FileFlow.App.Uno/Controls/NodeCardView.xaml(.cs)` · `NodeCardViewModel.cs` | **La puerta que faltaba**: el conmutador de parámetros de la tarjeta (`NodeCardExpandToggle`). Ver §5. |
| `FileFlow.App.Uno/Resources/Strings*.resx` | **22 claves** nuevas en EN+ES (las `PresetManager_*` del plugin, el rótulo de la fila y el del conmutador). |

---

## 4. Validación

| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18, `Debug`) | **0 errores** (sólo los avisos preexistentes y el `PRI257` de WinAppSDK) |
| `run-uno.ps1 -SelfCheck` (lienzo) | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** — *antes 83*; los 5 nuevos son del conmutador de parámetros |
| `run-uno.ps1 -SelfCheckControlBar` | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `run-uno.ps1 -SelfCheckDialogs` | **EXIT 0 · 33 `[OK]` · 0 `[FALLO]`** — *antes 24*; los 9 nuevos son del gestor de presets |
| `run-uno.ps1 -SelfCheckSettings` | **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias tocadas | `UnoNodeDialogsGuardTests` **11 de 11** (10 + `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive`), 0 rojos |
| Pruebas nuevas del view model | `MediaPresetManagerViewModelTests` **9 casos**, 0 rojos (con almacén falso y diálogos que anotan) |
| Mutaciones del tramo | **8** (6 del pase anterior + 2 nuevas) → **las 8 MUERDEN** |
| Mutación nueva 1 | `tarjeta-sin-la-puerta-de-sus-parametros` — **MUERDE** (43,3 s; testigo rojo, control verde) |
| Mutación nueva 2 | `conmutador-de-parametros-que-no-refresca` — **MUERDE** (30,9 s; testigo rojo, control verde) |
| `mutations/COVERAGE.md` | Regenerado por su guardia: **88 declaradas · 15 de 17 subsistemas** |
| Suite completa | **1928 superadas + 1 omitida de 1929, 0 errores** (2 m 30 s) |
| Rojo intermitente | **1 corrida** con `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` rojo (`flow_23_regulacion_velocidad_rate_limit` no entregó `paquete.zip`); **verde en aislamiento 2 de 2** → **ruido de carga, no regresión**; la corrida final, verde |
| Sesión con la app abierta (`qa-manual-263`) | **34 de 34 pasos** (`presets-session.json`) |

---

## 5. La puerta que le faltaba a la tarjeta (el defecto que encontró la medición)

El producto tiene **dos** puntos de entrada para esta superficie: el botón «🎬 Presets...» de la **tarjeta del
nodo** y el botón «🎬» de la **fila del parámetro**. La primera sesión con la app abierta midió que **la puerta
de la tarjeta no existía en el host**:

> El panel de acciones rápidas de la tarjeta —donde vive el botón— cuelga de `Node.IsExpanded`, y el host Uno
> **no tenía ningún control que conmutara ese estado**: el escritorio lo hace con un `ToggleButton` de la
> cabecera y este host no lo portó. La acción estaba **dibujada y sin puerta**: visible en el XAML, inalcanzable
> con el ratón.

Arreglado en el host, con el estado **del núcleo** (nada nuevo en el view model): un `ToggleButton` en la
cabecera (`NodeCardExpandToggle`, chevron arriba/abajo, dos vías con `Node.IsExpanded`, rótulo del diccionario
del host con **la misma clave que el escritorio**), su geometría en el adaptador y `IsExpanded` en la lista de
refresco agregado. La sonda de lienzo gana **5 comprobaciones** que lo miden en el árbol real (la caja, el
estado que escribe en el núcleo, el panel que se despliega, la geometría del chevron y la vuelta al estado
inicial) y la guardia exige las tres piezas para que no vuelva a quedarse a medias.

---

## 6. La sesión con la aplicación abierta: 34 de 34 pasos

Driver externo por UIA (`qa_presets_uia.py`), que **actúa** por las anclas y el puntero reales y **mide** por el
fichero del almacén, leído por fuera de la aplicación.

| Paso | Lo medido |
| :--- | :--- |
| Base | 3 tarjetas; el almacén expone **10 presets**, el primero `Extraer Audio MP3` |
| El nodo | Se añade por el cajón (búsqueda `Transcoder`) y su fila expone `ParamPreset_Preset` |
| **Puerta A — la TARJETA** | Se despliega con su conmutador y aparece `🎬 Presets...` en `(1930,1343)`; al pulsarlo, el gestor con **10/10 anclas** y **10 filas**, la primera **la del almacén**; el pixel central pasa de `#FCF8F8` a **`#B0ACAC`** |
| Cierre | Con su ancla `PresetManagerCloseButton`: el pixel vuelve a `#FCF8F8` y las 10 anclas desaparecen |
| **Puerta B — la FILA** | El botón «🎬» abre **la misma superficie** (10/10 anclas, mismo pixel `#B0ACAC`) |
| El formulario | Trae el preset **elegido** (`Extraer Audio MP3`) con **su descripción del almacén**, no una vacía |
| **Guardar** | Se escribe en la **caja real** y se pulsa el botón dibujado: el fichero `%AppData%\FileFlow\presets\media_presets.json` pasa a traer la descripción escrita; guardar **no** cierra la superficie |
| Persistencia | Al reabrir, la caja trae **lo guardado** |
| Restauración | La descripción del usuario vuelve por el mismo camino |
| **Alta y baja** | «Nuevo» lleva el almacén de **10 a 11** (nombre de fábrica `Nuevo Preset Personalizado`) y «Eliminar» lo devuelve a **10** |
| Final | 3 tarjetas, pixel de línea base, **almacén byte-idéntico** (`9b1e8f19477c5ebc3605ac373a38b38b`) y **ajustes del usuario intactos** (tema `pastel_spring`, idioma `es-ES`, carpeta de salida y favoritos) |

Capturas en `docs/qa/qa-manual-263/` (`80_base` … `99_final`) y el acta en `presets-session.json`.

---

## 7. Tres defectos del INSTRUMENTO que encontró la medición (escritos para el guion futuro)

1. **El almacén no estaba donde el driver lo leía.** El modo instalado de `AppPaths.RootDirectory` es
   **`%AppData%\FileFlow`**, y el gestor escribe en `presets/media_presets.json`; las preferencias, en
   `config/user_preferences.json`. El driver leía `%AppData%\FileFlowStudio\…`, **una copia de hace semanas que
   nunca cambia**: la conclusión era «Guardar no escribe» — y era falso. Corregido en el driver y en
   `qa_dialogs_uia.PREFS`.
2. **El cajón de herramientas de este host muestra la CLAVE cruda del recurso** (`MediaTranscoderNode_Name`),
   porque no carga el diccionario del plugin: la búsqueda del driver tiene que ir por la clave, no por el texto
   visible («Transcodificar» no encuentra nada). La tarjeta del lienzo, en cambio, **sí** muestra el texto
   resuelto («Transcodificar Media»).
3. **Un árbol con una tarjeta por nodo tiene un conmutador por tarjeta**, todos con la misma ancla: hay que
   elegir el de la tarjeta que se está midiendo (el más cercano a su título), no el primero.

---

## 8. Frontera medida (defecto del PRODUCTO, declarado y NO arreglado en este tramo)

Las órdenes **destructivas** del gestor (Eliminar, Restablecer) **no piden confirmación en el host Uno, y además
se comportan distinto según por qué puerta se entre**:

| Puerta | Servicio de diálogos que llega al view model | Lo medido |
| :--- | :--- | :--- |
| La **fila** del parámetro | El **Nulo** (`NullDialogService`: `ShowConfirmation => true`) | «Nuevo» 10→11 y «Eliminar» **11→10**: borra de verdad y **sin ningún diálogo** en el árbol |
| La **tarjeta** del nodo | El del **host** (`UnoDialogService`) | «Nuevo» 10→11 y «Eliminar» **11→11**: **no borra** y tampoco muestra nada — su `ShowConfirmation` es **síncrono** y desde el hilo de UI devuelve «no» |

Es la **frontera síncrona** del contrato de diálogos del núcleo, ya declarada en el plan de la rebanada 5 y que
el hito 259 esquivó para las órdenes de flujo con un canal asíncrono propio. El gestor **no** puede esquivarla
tal cual: sus órdenes son síncronas porque nacieron en el escritorio. Arreglarlo pide **confirmación asíncrona
en el contrato del SDK** (o comandos asíncronos en el gestor): es una rebanada, no un parche de última hora.
**Medido y escrito aquí para que no se descubra por accidente con los presets de un usuario dentro.**

---

## 9. Censo definitivo de las superficies de usuario del escritorio

### 9.1 Portadas y probadas en el host Uno

| Superficie | Estado |
| :--- | :--- |
| **Barra de control + cajón** | **31 entradas censadas** (16 de la barra + 15 del cajón) y la tabla de pendientes **VACÍA**: ninguna orden de menú del escritorio sin dibujar, declarar o cumplir |
| **Catálogo de diálogos** | **10 de 11 claves servidas** con su vista; **1 declarada** con su razón |
| **Ajustes** | **6 secciones** (General, Apariencia, Rutas, Registros, Modelos de IA, Actualizaciones) |
| **Paneles de nodo** | Inspector de parámetros, editor de texto y prompts, catálogo de variables **y el Gestor de Presets de Medios** |
| **Acciones de fila** | 4 servidas: rutas (`ParamBrowse_`), editor (`ParamEditor_`), catálogo de variables (`ParamVariable_`) y **presets** (`ParamPreset_`) |
| **Lienzo** | Tarjetas (con su conmutador de parámetros), sockets, cables, zoom, buscador (spotlight) y atajos enrutados |
| **Otras ventanas** | «Acerca de», actualización, VFS, métricas, estudio de temas, diseñador de datasets y editor de URLs por modelo |

### 9.2 NO portadas, con su razón

| Superficie | Razón |
| :--- | :--- |
| **Gestor de CONTRASEÑAS** (`PasswordManagerWindow`, ventana del plugin con el toolkit de Avalonia) | Declarada en `DeclaredPendingRowActions`: «abre el gestor de contraseñas, una ventana que este host todavía no tiene». **Es la ÚNICA superficie de usuario del escritorio que queda sin portar**, y su tamaño está escrito en el plan (**medio**, más la decisión de dónde vive la clave) |
| **Menú emergente de variables** (`OpenVariablePickerCommand`) | Declarado: este host no tiene menú emergente; su «{x}» abre el **catálogo completo**, que es la primera entrada de aquél |
| **`WorkflowSettings`** como diálogo | Declarado: la superficie de ajustes del host ya existe **con su propio punto de entrada** (barra y cajón); abrirla por este camino sería una **segunda copia** de lo mismo |
| **`IPopupMenuService` y `IColorPickerService`** | **Declarado, no pendiente**: portarlos sin necesidad sería duplicar el escritorio |

### 9.3 ¿Es el empaquetado lo único que queda fuera de lo pedido?

**Sí.** De las superficies de usuario del escritorio, lo único sin portar es el **gestor de contraseñas** (§9.2),
y fuera de ellas queda **sólo** el **empaquetado y la entrega**: las fases **5.4-5.6** del plan de la rebanada
5 —observación UIA de los pickers, `pack-uno.ps1` (publicación x64 self-contained + zip portable), **MSIX** con
su firma de sideload, CI con MSBuild de VS, release y la deuda declarada—, con su tamaño ya escrito en
`docs/uno_slice5_plan.md` §11. **El producto no reparte el host Uno; lo ejecuta quien lo compila.**
