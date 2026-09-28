# QA del hito 262 — El editor de URLs por modelo de IA (host Uno)

Fecha: 2026-09-28 · Build: `6818` · Rama: `worktree-2026-09-23T10-05-23`

## 0. Qué se cerró

El hito 261 dejó esta frontera **declarada**: la pestaña de **Modelos de IA** existía y listaba el catálogo, pero **no ofrecía la edición de URLs por modelo desde la fila** —la acción con la que el escritorio abre `AiModelUrlsConfig`—, así que la clave del catálogo no podía servirse («servirlo sería una ventana que nadie puede abrir»). Este tramo **le da el punto de entrada y sirve la ventana**, sobre el view model portable que ya existía.

| Pieza | Qué es |
| :--- | :--- |
| `Controls/SettingsPanel.xaml` | La **acción de URLs** de la fila (`Tag="urls"`, ancla `SettingsAiModelUrlsButton`), en el mismo puesto que en la fila del escritorio: entre descargar y borrar. |
| `Controls/SettingsPanel.xaml.cs` | Su **rama propia**: ejecuta la orden **canónica** del gestor (`ConfigureUrlsCommand`), no la descarga del `default`. |
| `Controls/AiModelUrlsConfigBody.xaml(.cs)` | La vista del `AiModelUrlsConfigViewModel` portable: caja de URLs (**dos sentidos, al teclear**), recuento, distintivo, probar, restablecer y los resultados de la prueba. |
| `Platform/UnoWindowService.cs` | `AiModelUrlsConfig` **servida** (`ImplementedDialogs`), con el arm que la pide, y **fuera** de `DeclaredPendingDialogs`: el censo queda en **10 servidas + 1 declarada**. |
| `Resources/Strings*.resx` | Las **10 claves** del escritorio (`AiModelUrls_*`) copiadas en EN+ES, más una del host (`Uno_AiModelUrls_RequiredWarning`). |

**Dónde queda escrito el cambio**: donde lo escribe el escritorio, porque lo escribe el propio view model portable (`Save()` → `AiModelManager.SetCustomUrls(modelId, urls)`), que es el almacén que lee el motor de descargas. La vista no escribe nada: no hay `SetCustomUrls` en su código, y una prueba lo exige.

## 1. Compilación

```
MSBuild de VS 18 (los targets de WinAppSDK no corren con dotnet build)
FileFlow.App.Uno -> bin\Debug\net10.0-windows10.0.19041.0\FileFlow.App.Uno.dll
0 errores
```

## 2. Las cuatro sondas (una corrida por comprobación)

| Sonda | Resultado |
| :--- | :--- |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** · VERIFICADO (**antes 12**) |

Los **seis pasos nuevos** de la sonda de ajustes miden la cadena entera sin puntero, en el mismo proceso:

- `la acción «URLs» de la fila del catálogo abre el editor del modelo (fila pulsada=True, clave abierta=AiModelUrlsConfig)`
- `y el editor es la vista del view model portable del modelo elegido ('MobileNetV2 ImageNet', esperado 'MobileNetV2 ImageNet')`
- `el editor se abre con las URLs que el gestor tiene configuradas para ese modelo (1 URL(s))`
- `escribir en su caja llega al view model y su recuento se pone al día ('2 URL(s)')`
- `y pulsar Guardar escribe el cambio DONDE LO ESCRIBE EL ESCRITORIO (el almacén del gestor del núcleo: contiene la URL escrita)`
- `la sonda deja la configuración del usuario como estaba (instantánea previa restaurada por el gestor)`

La sonda pulsa el **botón real de la fila** por su peer de automatización (no llama al comando por su cuenta), escribe en la **caja real** y **restaura** la instantánea del usuario.

## 3. Guardias

| Guardia | Estado |
| :--- | :--- |
| `UnoControlBarParityGuardTests` | **13 de 13** (el censo pasa a 10 servidas + 1 declarada) |
| `UnoSettingsSurfaceGuardTests` | **13 de 13** |
| `UnoNodeDialogsGuardTests` | **9 de 9** |
| `UnoShortcutParityGuardTests` | **5 de 5** |

La prueba nueva, `TheModelUrlAction_ShouldOpenTheServedEditor_AndWriteWhereTheDesktopWrites`, ata las **tres mitades**: la fila dibuja exactamente `download`/`delete`/`urls`; la rama de la acción ejecuta `ConfigureUrlsCommand` (**no** la descarga del `default`); la clave está **servida** con su vista, con su arm en el catálogo y **sin** seguir declarada; y el cuerpo del editor es una vista del view model portable que **no** escribe la configuración por su cuenta (no contiene `SetCustomUrls`).

## 4. Mutaciones (muerden, con testigo rojo y control verde)

| Mutación | Hito | Verdicto | Tiempo |
| :--- | :--- | :--- | :--- |
| `accion-de-urls-que-descarga-el-modelo` | 262 | **MUERDE** | 32,5 s |
| `nodo-que-declara-su-superficie-sin-clave` | 261 | **MUERDE** | 33,3 s |
| `disenador-de-datasets-fuera-del-catalogo` | 261 | **MUERDE** | 28,9 s |
| `vista-del-disenador-con-su-propio-modelo` | 261 | **MUERDE** | 28,4 s |
| `seccion-que-pierde-el-panel-que-conmutaba` | 261 | **MUERDE** | 28,5 s |
| `menu-que-no-declara-lo-que-falta` | 257→reapuntada | **MUERDE** | 28,0 s |

La nueva cierra el modo de fallo que de verdad importa ahora: si la **rama** de la acción desaparece, el `default` la sustituye y pulsar «URLs» **descarga el modelo**. Un botón que hace otra cosa es peor que uno que no hace nada, porque el usuario ya no distingue un fallo de su propia intención. Testigo 1 de 1 cae / control 1 de 1 pasa.

**Una mutación anterior se retiró** (`accion-de-urls-por-modelo-sin-declarar`): vigilaba que la fila NO dibujara la acción de URLs, y este tramo la dibuja. Dejarla habría sido un mutante que ya no mide nada. Declaradas: **80**.

## 5. Suite completa

```
dotnet test FileFlow.Tests/FileFlow.Tests.csproj
Correctas! - Con error: 0, Superado: 1917, Omitido: 1, Total: 1918 — 2 m 24 s
```

**Un rojo intermitente apareció en dos corridas de este tramo, distinto en cada una** —`EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` y `SystemPerformanceMonitorTests.TheHeartbeat_ShouldPublishAPlausibleSample`— y **los dos pasan en aislamiento (1 de 1 cada uno)**. Es el ruido de carga que los hitos 255 y 258 ya documentaron: no es regresión (son pruebas de hilos y de un latido de rendimiento, sin relación con una superficie de UI del host). La corrida final quedó verde.

## 6. Ejercido en la aplicación abierta (sesión 272): 24 de 24 pasos VERIFICADO

Driver externo por UIA (`docs/qa/qa_urls_uia.py`), que **pulsa los controles reales** y lee el árbol y el píxel. Lo medido, en orden:

| Paso | Medición |
| :--- | :--- |
| Catálogo | **24 filas**; la primera es **MobileNetV2 ImageNet** |
| **Qué fila y qué acción** | se pulsa `SettingsAiModelUrlsButton` de esa fila |
| **Qué superficie aparece** | el editor, con **7 anclas** (`AiModelUrlsModelName`, `AiModelUrlsTextBox`, `AiModelUrlsCountText`, `AiModelUrlsStatusBadge`, `AiModelUrlsTestButton`, `AiModelUrlsResetButton`, `AiModelUrlsCategory`), hablando del **mismo modelo** que la fila; pixel central `#FCF8F8` → **`#3C3C3C`** |
| **Qué valor queda escrito** | teclear **2 URLs** lleva el recuento del view model de `1 URL(s)` a **`2 URL(s)`**; Guardar cierra el modal (pixel de vuelta a `#585454`, el de la superficie que sigue abierta detrás); **al reabrir**, la caja trae las dos URLs escritas **y el distintivo pasa de `📦 Oficial / Predeterminado` a `🔧 Personalizado`** — la configuración del modelo quedó guardada en el gestor del núcleo |
| Restauración | `🔧` → `📦 Oficial / Predeterminado` y la caja con las URLs originales: **la sesión mide, no configura** |
| Cierre | tarjetas **3**, pixel central `#FCF8F8`, preferencias md5 **`d5f199a068113d8a7e16ad6ee6f726b3` antes == después** |

**Dos defectos del DRIVER (no del producto) se encontraron y se arreglaron midiendo**, y quedan escritos porque afectan a cualquier guion futuro:

1. `window_text()` de una **fila enlazada** devuelve el nombre del **tipo del view model** (`FileFlow.App.ViewModels.AiModelItemViewModel`), no lo que se ve. El nombre del modelo vive en el `AutomationProperties.Name` del panel de la fila.
2. El píxel tras cerrar el modal **no vuelve al del lienzo** sino al de la **superficie de ajustes que sigue abierta detrás** (`#585454`): comparar contra la línea base del lienzo medía mal el producto, no un defecto.

y **uno del PRODUCTO**, también encontrado midiendo: la caja del editor enlazaba `Text` sin `UpdateSourceTrigger`, así que en WinUI escribía al **perder el foco** y el recuento se quedaba con el valor viejo mientras el usuario teclea (el escritorio lo actualiza al teclear). Corregido en el enlace: es la misma semántica que la ventana del escritorio.

## 7. Fronteras declaradas

- **El aviso de «URL requerida» no puede salir como segundo `ContentDialog`**: WinUI sólo admite uno a la vez y el editor ya está abierto. El view model lo pide por `IDialogService` —la misma vía que el escritorio—, la petición queda escrita en la consola del host, y **el host lo repite dentro del editor** (nota `AiModelUrlsRequiredNote`) y **no cierra el modal** sobre algo que el view model rechazó. El texto es la misma frase del view model, copiada al diccionario del host.
- **`WorkflowSettings` sigue declarada y no servida** (una sola declarada): su superficie ya tiene puerta en la barra y el cajón, y abrirla además por `IWindowService` sería una segunda copia de lo mismo.
- **Hallazgo del escritorio, anotado y NO tocado**: su `AiModelUrlsConfigDialog` enlaza `{Binding SaveCommand}`, que su view model **no expone** (tiene `Save()` sin `[RelayCommand]`), así que ese botón no guarda. El host no hereda el defecto —llama al mismo `Save()` del view model— y **no se tocó el escritorio**.
- **Los dos pickers de servicios que el host no tiene** (`IPopupMenuService`, `IColorPickerService`) siguen declarados: el «{x}» abre el catálogo completo y no hay selector de color nativo.
