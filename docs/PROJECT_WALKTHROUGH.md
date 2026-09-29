# Bitácora de Ingeniería — FileFlow Studio

> **Qué es esto.** El registro cronológico de lo que se hizo, **lo que se midió** y **qué quedó declarado** como
> frontera, hito a hito: el encargo, los hallazgos de la medida, las piezas, las guardias y la validación con cifras.
>
> **Cómo se lee.** Este fichero es la **ventana viva**: las entradas del tramo en curso. Todo lo anterior vive en
> [`docs/history/`](history/), en archivos fríos por periodo, con su rango de hitos en el índice de abajo. Al cerrar
> un tramo se hace un corte: las entradas que dejan de ser el día a día se **mueven enteras** al archivo —no se
> resumen ni se reescriben— y aquí quedan el índice y la ventana nueva.
>
> **Cómo se añade una entrada.** Va **al principio**, con su fecha y su número de hito. Una entrada vieja **no se
> reescribe**: si una medida se corrige, se dice en la entrada nueva y se deja constancia del error.

## Índice del archivo frío

| Periodo | Hitos | Qué encontrarás | Documento |
| :--- | :--- | :--- | :--- |
| 2026-09-26 → 2026-09-27 | 225 – 254 | El lienzo Uno vivo (selección, arrastre, decoradores, spotlight, migas), la re-tematización en caliente, las mutaciones del tema y del redibujado de cables, el cable pegado a sus sockets, el hit-test del área de clic, el teclado con puntero real y el plan de la rebanada 5 | [`docs/history/2026-09-28_walkthrough_2026-09-26_a_2026-09-27.md`](history/2026-09-28_walkthrough_2026-09-26_a_2026-09-27.md) |
| 2026-09-23 → 2026-09-25 | 169 – 224 | Los paneles del editor del host Uno (caja de herramientas e inspector), las pestañas de Entradas/Salidas/Diff, el «Probar» del inspector, el modo `--selfcheck-uia` y las fases 3.1 a 3.6 del lienzo | [`docs/history/2026-09-28_walkthrough_2026-09-23_a_2026-09-25.md`](history/2026-09-28_walkthrough_2026-09-23_a_2026-09-25.md) |
| 2026-09-10 → 2026-09-22 | 118 – 168 | Los cimientos del host Uno (rebanadas 1 y 2), el rediseño visual, el núcleo portable, el archivo de flujo (versión, reparación y convergencia) y el motor DAG | [`docs/history/2026-09-28_walkthrough_2026-09-10_a_2026-09-22.md`](history/2026-09-28_walkthrough_2026-09-10_a_2026-09-22.md) |
| Antes del 2026-09-10 | — | Fases 1 a 8, los sprints de agosto de 2026 y los desarrollos fundacionales | [`docs/history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md`](history/2026-09-13_PROJECT_WALKTHROUGH_ARCHIVE.md) |

## Ventana viva

## [2026-09-28] - Hito 271: Configuración Integral del IDE y Entorno de Desarrollo para Uno Platform

### 🎯 El encargo
«este proyecto usa una interfaz de usuario basada en uno platform pero parece que el entorno de desarrollo y el ide no estan bien configurados. configura todo para que funcione bien.»

### 🔬 Lo que encontró la medida
1. **El IDE carecía de configuración para Uno Platform y C#**:
   - `.vscode/settings.json` contenía únicamente `"dotrush.roslyn.projectOrSolutionFiles": []` (un array vacío que dejaba a DotRush sin solución). No existía definición de solución por defecto (`dotnet.defaultSolution`), provocando que C# Dev Kit cargase `FileFlow.slnx` (el host Avalonia) en lugar de `FileFlow.Uno.slnx` (el host Uno Platform sin dependencias de escritorio).
   - No existía `.vscode/launch.json` para depuración con F5 en VS Code ni `.vscode/tasks.json` para tareas de compilación, ejecución y self-check.
   - No existía `.vscode/extensions.json` recomendando la extensión oficial de Uno Platform (`unoplatform.vscode`) ni las herramientas de C# Dev Kit.
   - La extensión de Uno Platform para VS Code no estaba instalada en el sistema; se instaló `unoplatform.vscode` v0.26.1.
2. **Defecto en los scripts lanzadores de PowerShell (`run-uno.ps1` y `run-uno-fast.ps1`)**:
   - Al invocar los scripts con parámetros de sondeo (como `-SelfCheck`), la concatenación `@("--selfcheck") + $AppArgs` cuando `$AppArgs` es `$null` creaba un array con un elemento nulo (`@("--selfcheck", $null)`).
   - PowerShell fallaba en `Start-Process` con la excepción: `Start-Process : No se puede validar el argumento del parámetro 'ArgumentList'. El argumento es null o está vacío.`
   - Se refactorizó la recolección de argumentos usando `List[string]` y comprobación explícita de `IsNullOrWhiteSpace`, eliminando el fallo y garantizando que el paso de parámetros a `Start-Process` sea limpio tanto con argumentos como sin ellos.
3. **Optimización de `.gitignore` y estandarización con `.editorconfig`**:
   - Se ajustó `.gitignore` para versionar la configuración esencial del IDE (`.vscode/settings.json`, `tasks.json`, `launch.json`, `extensions.json`) ignorando temporales.
   - Se introdujo `.editorconfig` con directivas precisas de sangrado para C# (4 espacios), XAML/XML/JSON (2 espacios), codificación UTF-8 y saltos de línea CRLF.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `.vscode/settings.json` | Configura `FileFlow.Uno.slnx` como solución principal para C# Dev Kit, DotRush y OmniSharp, asocia archivos XAML/AXAML/SLNX a XML, anidamiento de ficheros (`*.xaml` -> `*.xaml.cs`, `*.axaml` -> `*.axaml.cs`) y exclusión de directorios `bin/` y `obj/` en búsquedas. |
| `.vscode/launch.json` | Perfiles de depuración `coreclr` listos para F5: ejecución normal, ejecución sin depuración y modos de autorrevisión (`--selfcheck`, `--selfcheck-dialogs`, `--selfcheck-controlbar`, `--selfcheck-settings`). |
| `.vscode/tasks.json` | Tareas de compilación (`build-uno`, `build-uno-release`), ejecución (`run-uno`, `run-uno-fast`), pruebas unitarias (`test-all`) y sondeos automatizados (`selfcheck-uno*`). |
| `.vscode/extensions.json` | Recomendaciones de extensiones clave: `unoplatform.vscode`, `ms-dotnettools.csdevkit`, `ms-dotnettools.csharp` y `ms-dotnettools.vscode-dotnet-runtime`. |
| `.editorconfig` | Estándar de codificación unificado para el IDE y herramientas de análisis. |
| `run-uno.ps1` / `run-uno-fast.ps1` | Corrección del paso de argumentos en `Start-Process`, asegurando ejecución confiable de la app y sus sondeos. |
| Extensión `unoplatform.vscode` | Instalada la extensión oficial v0.26.1 de Uno Platform en el entorno VS Code. |

### 🛡️ Cómo se verificó
1. **Compilación hermética Uno**: `dotnet build FileFlow.Uno.slnx -p:FileFlowUnoHost=true` → **0 errores**.
2. **Sondeo en runtime del lienzo**: `.\run-uno.ps1 -SelfCheck -NoBuild` → **EXIT 0 · 85 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
3. **Sondeo de paneles de nodo y diálogos**: `.\run-uno-fast.ps1 -SelfCheckDialogs` → **EXIT 0 · 51 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
4. **Sondeo de barra de control y cajón**: `.\run-uno-fast.ps1 -SelfCheckControlBar` → **EXIT 0 · 42 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
5. **Sondeo de ajustes (tema e idioma)**: `.\run-uno-fast.ps1 -SelfCheckSettings` → **EXIT 0 · 18 `[OK]` · 0 `[FALLO]` · VERIFICADO**.
6. **Guardias de arquitectura Uno**: `UnoHermeticBuildGuardTests` y `UnoNodeDialogsGuardTests` → **21 superadas de 21**.
7. **Suite completa de pruebas**: `.\test.ps1` → **1963 superadas, 1 omitida, 0 fallos** en 178 s.

## [2026-09-28] - La Bitácora se Divide: el Archivo Frío y la Ventana Viva

### 🎯 El encargo
«hay muchos archivos auxiliares .md que o bien tienen un tamaño muy grande o ya están obsoletos o no tienen ya uso.
Mueve estos archivos ya viejos o muy grandes al archivo `docs/history`. Por ejemplo `PROJECT_WALKTHROUGH.md` ha
crecido demasiado usando demasiado contexto y tokens: comprime y resume las partes más antiguas o sin utilidad
actual, dejando solo las partes relevantes. Todo lo antiguo pásalo al archivo.»

### 🔬 Lo que encontró la medida
1. **La bitácora pesaba 970 KB en 7.689 líneas y 207 entradas** (2026-09-10 → 2026-09-28). `AGENTS.md` obliga a
   consultarla **al empezar cada sesión**, así que el coste de arrancar crecía con la **historia** y no con el
   trabajo por hacer: el mismo protocolo que la hace útil la volvía un impuesto.
2. **El resumen de sesión pesaba otros 579 KB** con **162 bloques de hito** (109 → 270) bajo el rótulo «Hito más
   reciente»: el rótulo describía una ventana que llevaba decenas de hitos sin recortarse.
3. **Los planes de las rebanadas 3 y 4 estaban cerrados** —el de la 4 lo dice en su propia cabecera, «Cerrada en el
   hito 236»— y el plan de rediseño visual es una **propuesta del tramo 1**, entregada hace veintiséis tramos.
4. **`docs/user_guide.md` era un manual superado**: el que el instalador copia y el que `build-pdf-manual.ps1`
   regenera es `docs/manual_de_usuario.md`; aquél era una versión corta que además era el destino del índice de
   `docs/README.md` y de las instrucciones de Copilot.
5. **Lo pesado de verdad ya estaba fuera de git**: las capturas de las sesiones manuales (cientos de PNG de 200 a
   400 KB) y los `__pycache__` los excluye el `.gitignore` desde el hito 265. Lo que quedaba dentro del control de
   versiones era **texto**: 108 ficheros `.md`.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `docs/PROJECT_WALKTHROUGH.md` | La **ventana viva**: cabecera con las reglas de lectura, el **índice del archivo frío** y las entradas del tramo en curso (hitos **255 a 270**). De **970 KB a 138 KB**. |
| `docs/history/2026-09-28_walkthrough_2026-09-26_a_2026-09-27.md` | Archivo frío: hitos **225 – 254** (1.333 líneas). |
| `docs/history/2026-09-28_walkthrough_2026-09-23_a_2026-09-25.md` | Archivo frío: hitos **169 – 224** (2.450 líneas). |
| `docs/history/2026-09-28_walkthrough_2026-09-10_a_2026-09-22.md` | Archivo frío: hitos **118 – 168** (3.129 líneas). |
| `.antigravity/knowledge/session_summary.md` | El resumen de sesión se queda con los **hitos 256 – 270**; el resto (109 – 255) pasa a `knowledge/history/`, con el aviso de la cabecera apuntando a los dos volúmenes. |
| [`docs/history/2026-09-28_uno_canvas_plan_rebanada3.md`](history/2026-09-28_uno_canvas_plan_rebanada3.md), [`…_uno_panels_plan_rebanada4.md`](history/2026-09-28_uno_panels_plan_rebanada4.md) y [`…_ui_redesign_plan.md`](history/2026-09-28_ui_redesign_plan.md) | Los planes **cerrados**: las rebanadas 3 y 4 (el de la 4 lo dice en su cabecera, «Cerrada en el hito 236») y el rediseño visual del tramo 1. El de la rebanada 5 se queda vivo: es el único con fases pendientes (empaquetado y entrega). |
| [`docs/history/2026-09-28_user_guide_manual_es_obsoleto.md`](history/2026-09-28_user_guide_manual_es_obsoleto.md) | El manual de usuario superado por `docs/manual_de_usuario.md` —el que el instalador copia y regenera—, con sus dos enlaces vivos reapuntados. |
| [`docs/history/2026-09-28_flujo_test_legado.json`](history/2026-09-28_flujo_test_legado.json) | Un flujo de prueba de un formato viejo que vivía suelto en `docs/`: sus parámetros no los declara ningún nodo y su ruta es de otra máquina (lo dice el propio comentario que lo excluye de la validación del catálogo). |

### 🛡️ Cómo se verificó
- **Ninguna guardia lee estos documentos**: el barrido de `*.cs`, `*.ps1`, `*.yml` y `*.props` no encuentra ni una
  referencia a la bitácora, al resumen de sesión ni a los planes, así que moverlos no toca la suite. Lo que sí está
  atado por guardias (`mutations/COVERAGE.md`, `.agents/nodes_catalog.md`, `docs/api_reference.md`) **no se movió**.
- **Los tres cortes caen entre entradas**, no dentro: los tres empiezan con su `## [fecha] - …`, y la suma de líneas
  de los tres archivos más la ventana viva cuadra con el original.
- **Cada archivo frío lleva su cabecera** con su periodo, su rango de hitos y el porqué del corte.

### 🟠 Fronteras declaradas
- **El archivo no se resume: se mueve entero.** Reescribir lo viejo para «comprimirlo» lo convertiría en otra cosa y
  perdería las medidas que lo sostienen; lo que se comprime es **lo que se lee cada sesión**, no lo que se conserva.
- **El corte es por periodo**, y las fechas dentro del último tramo no son monotónicas —los hitos 118 a 168 se
  apilaron al final, desordenados—, así que el índice habla de **rangos de hito** y el archivo lo advierte.
- **Los enlaces de los archivos fríos no se reapuntan**: son registros fríos, y reescribir su historia para arreglar
  un enlace sería falsearla. Los enlaces de los documentos **vivos** sí se actualizaron.
- **Los PDF de los manuales (9,5 MB) siguen versionados**: son **entrada del instalador**
  (`build-installer.ps1` los copia y `build-pdf-manual.ps1` los regenera). Sacarlos del control de versiones es una
  decisión de empaquetado, no de limpieza, y queda declarada.
- **Las capturas de las sesiones manuales no se tocaron**: ya están fuera de git y son la evidencia de lo que se
  midió; borrarlas es irreversible y no lo pidió nadie.

---

## [2026-09-28] - Hito 270: El Botón del Nodo que Abre una Ventana del Escritorio Avisa, y los Paneles Laterales se Redimensionan

### 🎯 Objetivos y Alcance
El encargo, en una frase del usuario: «los botones en los nodos no parecen funcionar y los paneles laterales de inspector y catálogo de nodos no se pueden redimensionar. arréglalo». Dos mitades: (1) averiguar **qué** botón del nodo no hacía nada y por qué, y (2) hacer **redimensionables** el cajón de nodos y la ficha del inspector del host Uno, que tenían ancho fijo (la columna de 280 y el `Width="300"` de la ficha), como el escritorio los tiene (sus dos `GridSplitter` y el reparto 180–480 / 220–750).

### 🔬 Lo que encontró la medida (con puntero REAL, no programático)
1. **Los botones de la tarjeta SÍ respondían**. Con el puntero inyectado del instrumento de `docs/qa/qa_manual.py` (SetCursorPos + mouse_event, el que la sesión del 260 midió como «el contenido no reacciona»), hoy el host responde en todas las puertas del nodo, medido píxel a píxel: el LED del breakpoint se enciende en rojo, el del log se apaga, el conmutador despliega el panel, el «➕ Caso» de la tarjeta añade su puerto y el botón de la ficha abre el gestor de presets. La hipótesis de trabajo (el arrastre del lienzo robaba el puntero al pulsar un botón) **se descartó midiendo**: la traza del lienzo no recibe ni un `press` sobre un botón de la tarjeta —`ButtonBase` marca el gesto como manejado y el handler del editor no llega—.
2. **El botón que no hacía nada era el de la ventana del ESCRITORIO**. Sobre el nodo de script, pulsar «💻 Editor de Scripts...» con puntero real no abría nada, no avisaba y no dejaba más rastro que una línea en la consola del proceso («`[DesktopOnlySurface] «Estudio de Scripts» no se puede montar en este host…`»). La causa: `NodeViewModel.ExecuteCustomAction` construía el `NodeCustomActionContext` **sin el servicio de diálogos**, así que la costura del hito 268 —que declara la frontera por los diálogos de quien la abrió— caía al `NullDialogService` del Sdk. Los siete nodos con ventana del toolkit hacían su mitad (`(context as NodeCustomActionContext)?.Dialogs`) y **el teléfono no estaba puesto en la otra**: la frontera era cierta en el código y falsa de cara al usuario. El mismo hueco estaba en la puerta del **gestor de contraseñas** (`NodeParameterViewModel.OpenPasswordManager`).
3. **La frontera del 268 decía la verdad y no se había ejercido**, tal y como su propio apartado de fronteras declaraba: lo medido era la costura con un doble puesto a mano, y la guardia del 268 no mira quién construye el contexto. Un defecto así no lo caza ningún lint de texto (el nodo sigue declarando, la traza sigue escribiéndose y el sabor sigue siendo hermético).
4. **El aviso tampoco era visible para nadie más**. `UnoDialogService.ShowCoreAsync` mostraba su `ContentDialog` **fuera** del estado `UnoWindowService.ActiveDialog`, que es el que el propio host consulta antes de abrir otro modal (WinUI admite uno) y el que leen las sondas: el aviso se veía, pero el host no sabía que estaba ahí y una sonda no podía distinguir «se avisó» de «no pasó nada». Ahora se publica por `RunOwnedAsync`.
5. **El lanzador no esperaba a la aplicación**. `run-uno.ps1` / `run-uno-fast.ps1` lanzaban el host con `& $exePath` y PowerShell **no espera a las aplicaciones de GUI**: el script devolvía «exit 0» con el sondeo todavía corriendo y el informe a medio escribir —o el de la corrida anterior—. Se midió al leer un veredicto que no era de la corrida que se acababa de lanzar; ahora los dos lanzadores usan `Start-Process -Wait -PassThru` y heredan el código de salida de verdad.
6. **La sonda nueva tenía su propia trampa**: `WaitUntil` ya envuelve su condición en `Probe`, así que anidar un `Probe` dentro de él deja al hilo de UI esperándose a sí mismo y devuelve un «no» falso (medido en esta misma sonda: el texto del aviso aparecía en el informe como si se hubiera leído). La espera y la lectura quedan separadas.
7. **WinUI 3 no trae `GridSplitter` y `Border` está sellado**: el asa se escribe sobre `Grid` (lo que necesita del árbol es un `Background` opaco al puntero), con el reparto del escritorio y una cota más —el arrastre no puede dejar al lienzo por debajo de su mínimo—, porque el área de clic de las tarjetas se mide del árbol visual.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Core/ViewModels/NodeViewModel.cs` | El contexto del botón del nodo lleva el **servicio de diálogos del host** (`CoreDialogHost.ResolveDialogService()`, el mismo camino que la superficie declarada veinte líneas más abajo): la frontera del 268 llega al usuario. |
| `FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs` | La puerta del **gestor de contraseñas** del mismo hueco: su `_dialogService`, que ya tenía en la mano. |
| `FileFlow.App.Uno/Platform/UnoDialogService.cs` + `UnoWindowService.cs` | El aviso se muestra **publicándose** como el modal abierto (`RunOwnedAsync`): el segundo `ContentDialog` se detecta de verdad y la sonda ve el aviso. |
| `FileFlow.App.Uno/Controls/PanelSplitter.cs` (nuevo) | El **asa** del marco: 5 px, puntero capturado durante el arrastre, cursor de redimensionado y la cuenta en UN sitio (`Resolve(startWidth, delta, min, max, room)`), con el tope del lienzo como segunda cota. |
| `FileFlow.App.Uno/MainWindow.xaml` / `.xaml.cs` | Las **cinco columnas** del editor (cajón · asa · lienzo · asa · ficha) con las cotas del escritorio, las dos asas atadas a sus columnas y la ficha que **conserva su ancho** al plegarse y volver (`ApplyInspectorVisibility`, con el asa siguiendo la visibilidad del panel). |
| `FileFlow.App.Uno/RuntimeSelfCheck.cs` | Tres medidas nuevas en `--selfcheck-dialogs`: el nodo con ventana del escritorio declara y la ficha la pinta, su botón **AVISA nombrando la ventana** (el nombre se lee del diccionario del plugin, no de un literal), y el aviso se retira limpio. |
| `run-uno.ps1` / `run-uno-fast.ps1` | Los sondeos **esperan** al proceso y heredan su código de salida. |
| `FileFlow.Tests/Unit/App/NodeActionFrontierWiringTests.cs` (nuevo) | Tres casos: el aviso **llega** al servicio de diálogos del host por el camino entero del botón; el contexto lleva SIEMPRE un servicio (el nulo declarado sin host, nunca `null`); y el **censo** de las construcciones del contexto en el núcleo portable (una puerta nueva sin diálogos vuelve a ser un botón mudo). |
| `mutations/boton-del-nodo-que-no-avisa.json` (nuevo) | El defecto declarado: quitar el tercer argumento del contexto deja el botón mudo sin que ningún lint de texto lo vea. |

### 🛡️ Guardias, pruebas y mutaciones
- **+3 casos** en `NodeActionFrontierWiringTests` (la suite pasa de 1961 a **1964**).
- **1 mutación nueva** `boton-del-nodo-que-no-avisa` → **MUERDE** (34,5 s; testigo `TheNodeActionButton_ShouldShowTheDesktopOnlyWarning_InTheHostDialogs` rojo, control `DeclaringTheFrontier_ShouldShowItInTheHostDialogs` verde — que es justo lo que separa «el botón no lleva sus diálogos» de «la costura dejó de avisar», con su propia mutación desde el 268).
- La sonda del host es la que **cierra la frontera del 268** («empujar esos botones con la aplicación abierta sigue siendo materia de una sesión manual»): ahora se empuja desde el propio sondeo y el aviso se lee en el informe.
- **Dos defectos los cazó la suite al cerrar el tramo** (arreglados antes de darlo por bueno): (1) las dos claves de las asas (`Uno_SplitterToolbox` / `Uno_SplitterInspector`) se citaban **sin estar en ninguno de los dos diccionarios**, así que el nombre del asa se habría resuelto por el fallback incrustado en el código y **no habría cambiado de idioma** —el defecto que la superficie de ajustes mide, cazado por `EveryCitedUnoKey_ShouldExistInBothDictionaries`—; (2) el sobre del modal que este tramo añadió escribía **una tercera** llamada a `TearDownInlineQuestion(false)`, y la guardia del 263 cuenta **dos** vías de abandono: en vez de aflojar la guardia, la ventana del catálogo y el aviso comparten ahora `ShowOwnedModalAsync` y la cuenta sigue siendo **dos**.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **0 errores** |
| Sonda del host (`-SelfCheck`) | **EXIT 0 · 85 `[OK]` · 0 `[FALLO]` · VERIFICADO** con el marco nuevo (el área de clic sigue coincidiendo con las tarjetas dibujadas: el lienzo pasa a (285,0) y se **mide**, no se supone) |
| Sonda de los paneles de nodo (`-SelfCheckDialogs`) | **EXIT 0 · 51 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 48: las tres medidas de la frontera) |
| La suite completa | **1963 superadas + 1 omitida de 1964, 0 errores** (2 m 33 s; el total pasa de 1961 a 1964) |
| Mutaciones | **1 nueva, MUERDE** (34,5 s) |

### 🟠 Fronteras declaradas
- **Las siete ventanas del toolkit siguen sin poder montarse en este host**: lo que cambia es que su botón **avisa** nombrando la ventana (y su traza queda en consola). Abrirlas es del escritorio.
- **El asa se mide por su cuerpo, no por el ratón del sistema**: lo ejercitado en el selfcheck es el mismo camino (la cuenta y la aplicación del ancho) y el arrastre con puntero real del asa queda para la sesión manual, junto al resto de gestos.
- **Los anchos no se persisten entre sesiones**: el reparto del escritorio se recupera al arrancar (cajón 280, ficha 300) y lo que el usuario ajuste vive lo que viva la ventana.
- **El asa de la ficha se retira con el panel**: sin columna que gobernar, un mando visible sería un mando que no manda.
- **El censo del contexto cubre el núcleo portable**, no las construcciones que un plugin haga por su cuenta (hoy no hay ninguna).

---

## [2026-09-28] - Hito 269: La Tarjeta Enseña lo que Hace, y las Acciones del Nodo Llegan a la Ficha

### 🎯 Objetivos y Alcance
El encargo, en dos frases del usuario: «en los nodos, al desplegarlos se ve el listado de parámetros pero no se puede hacer nada con ellos: mejor quítalos y déjalos que solo se puedan editar en el inspector» y «en estos a veces aparecen botones que abrirían diálogos de configuración que no están en el inspector: añádelos». Es decir: el panel plegable de la tarjeta del host Uno tenía una **lista muerta** (nombres sin editor) y, al mismo tiempo, la ficha del inspector **no tenía** las acciones del nodo, que son la puerta a sus superficies.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/NodeCardView.xaml` | El panel plegable se queda **con lo que hace algo**: fuera el `ItemsControl` de `Node.Parameters` (nombres, sin editor y sin gesto), dentro las **acciones del nodo**. El conmutador de la cabecera pasa a dibujarse **sólo si el nodo declara acciones** (un chevron que despliega un panel vacío es el botón-que-no-hace-nada que este tramo quita). Los `x:Name` (`ParametersToggle`, `ParametersPanel`, `ParametersIcon`) y el `AutomationId` `NodeCardExpandToggle` se conservan: son anclas estables de las sondas y de la guardia, y el comentario del XAML dice por qué el nombre ya no describe lo que despliegan. |
| `FileFlow.App.Uno/Controls/NodeCardViewModel.cs` | La condición en UNA propiedad: `ActionsPanelVisible => HasCustomActions && _node.IsExpanded`. El estado desplegado sigue siendo del **núcleo** (`Node.IsExpanded`, con su refresco agregado) y el rótulo del conmutador pasa a decir lo que hace **este** host («Mostrar/Ocultar las acciones del nodo») conservando la clave del escritorio, que es la que audita la guardia de textos compartidos. |
| `FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs` | El **bloque de ACCIONES** de la ficha (pestaña de Parámetros, en el orden del escritorio: descripción → acciones → parámetros): un botón por acción declarada, con su `ToolTip`, su ancla `InspectorAction_<ActionId>` y el comando del view model **portable** (`NodeActionViewModel.ExecuteCommand` → `NodeViewModel.ExecuteCustomAction`, la MISMA orden que el botón de la tarjeta). El encabezado se localiza en caliente (`Uno_InspectorActions`) y el bloque entero se colapsa sin acciones. Superficie para la sonda: `ActionButtonCount` y `ActionControl(actionId)`. |
| `FileFlow.App.Uno/Resources/Strings{,.es}.resx` | La clave nueva del encabezado y el texto del conmutador, en los dos idiomas (el diccionario del host es el que este host carga; el del plugin no). |
| `FileFlow.Tests/Unit/App/UnoNodeDialogsGuardTests.cs` | El caso de la puerta de la tarjeta se reescribe al contrato nuevo: el panel cuelga de `ActionsPanelVisible`, el conmutador de `HasCustomActions`, y el listado de parámetros de la tarjeta **no puede volver** (aserción negativa sobre el XAML). |
| `FileFlow.Tests/Unit/App/UnoInspectorPanelGuardTests.cs` | Caso nuevo `InspectorPanel_ShouldPaintTheNodeActions_SoTheirSurfacesAreReachableWithoutTheCard` (colección del núcleo, comando portable, ancla por `ActionId`, encabezado localizado, bloque que se colapsa y la medición en runtime) + fila en la tabla de paridad del inspector. |
| `mutations/acciones-del-nodo-que-solo-se-pulsan-desde-la-tarjeta.json` (nuevo) | La mutación del bloque nuevo: quitar el bucle de acciones de la ficha deja la superficie del nodo inalcanzable para quien no despliegue una tarjeta. |

### 🔬 Lo que encontró la medida
1. **La lista muerta se veía verde por todas partes.** El panel de la tarjeta existía, se desplegaba, tenía su conmutador probado y su acción «🎬 Presets...» cableada; lo que no había era **nada que hacer** con la mitad de su contenido. Ninguna pieza mentía por separado: el defecto estaba en la composición (una lista que invita a interactuar y no responde).
2. **La puerta existía en un solo sitio.** Las acciones del nodo se pintaban **sólo** en el panel de la tarjeta del lienzo; el escritorio las pinta también en su ficha, y este host no. La medición lo cazó en cuanto la sonda preguntó por ellas: `acciones del nodo en la ficha: 0 de 0` en el flujo de ejemplo y, en el modo de los paneles, `la ficha del inspector pinta las acciones del nodo (1 botón, ancla 'InspectorAction_ManageMediaPresets')` tras el arreglo.
3. **La sonda dijo la verdad incómoda.** El primer intento de la sonda buscaba en el lienzo una tarjeta **con acciones** —y el flujo de ejemplo no tiene ninguna, porque el único nodo con acciones lo añade el modo de los paneles. En vez de dar la medida por buena, el sondeo mide ahora el **contrato** en todas las tarjetas (lo que enseña la vista == lo que dice el adaptador) y deja el ejercicio completo —desplegar, ver el panel con su chevron y pulsar la acción— donde el nodo existe de verdad.
4. **El andamiaje cazó dos mutaciones propias obsoletas.** `MutationDeclarationGuardTests.EveryDeclaredMutation_ShouldStillFitTheProductAndTheSuite` se puso rojo nombrando los fragmentos que el código ya no contiene (`conmutador-de-parametros-que-no-refresca`, que cita la línea de refresco que este tramo reescribió) y `mutate.ps1` rechazó la mutación nueva por una palabra de menos en el fragmento declarado («el del view model» vs «del view model»). Las dos se corrigieron **antes** de dar el tramo por bueno: la declaración de una mutación es código, no prosa.
5. **Un rótulo que mentía.** El conmutador de la tarjeta se llamaba «Mostrar/Ocultar parámetros» y ya no despliega parámetros: el texto de este host dice ahora lo que hace, con la clave intacta para no romper el censo de textos compartidos.

### 🛡️ Guardias, pruebas y mutaciones
- **+1 caso** en `UnoInspectorPanelGuardTests` y el caso de la tarjeta reescrito: la suite pasa de **1960** a **1961** (1959 superadas + 1 omitida en la corrida completa, con el flake de CPU conocido en `EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven`, que pasa en aislamiento).
- **1 mutación nueva** `acciones-del-nodo-que-solo-se-pulsan-desde-la-tarjeta` → **MUERDE** (28,4 s; testigo `InspectorPanel_ShouldPaintTheNodeActions_SoTheirSurfacesAreReachableWithoutTheCard` rojo, control `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive` verde). Las **dos** mutaciones que citan lo que este tramo reescribió se actualizaron y vuelven a morder: `tarjeta-sin-la-puerta-de-sus-parametros` (**MUERDE**, 28,9 s) y `conmutador-de-parametros-que-no-refresca` (**MUERDE**, 28,6 s).
- `COVERAGE.md` regenerado por su guardia: **100 declaradas** (antes 99).

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **Compilación correcta · 0 errores** (los avisos son los preexistentes del host y el `PRI257` de WinAppSDK) |
| Sonda del host (`.\run-uno-fast.ps1 -SelfCheck`) | **EXIT 0 · 85 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 88: el ejercicio del panel se mudó a donde hay una tarjeta con acciones y la puerta se mide ahora como contrato sobre las 3 tarjetas del lienzo) |
| Sonda de los paneles de nodo (`-SelfCheckDialogs`) | **EXIT 0 · 48 `[OK]` · 0 `[FALLO]` · VERIFICADO** (eran 46; incluye las dos medidas nuevas: la acción del nodo en la ficha y el despliegue del panel de la tarjeta con su chevron) |
| Sondas de barra de control y de ajustes | `-SelfCheckControlBar` y `-SelfCheckSettings` → **exit 0** (sin cambios en esas superficies) |
| Suite completa | **1959 superadas + 1 omitida de 1961** (2 m 39 s) |
| Mutaciones | **1 nueva + 2 actualizadas, las tres MUERDEN** · **100 declaradas** |

### 🟠 Fronteras declaradas
- **El cambio es del host Uno.** El host de escritorio (Avalonia) conserva sus parámetros **en línea en la tarjeta**, que allí **sí** se editan (toggle, deslizador, desplegable, ruta con explorar, editor y catálogo de variables): la queja —«no se puede hacer nada con ellos»— es de la tarjeta del host Uno, donde el listado era un `TextBlock` sin editor. Igualar los dos hosts aquí sería quitarle al escritorio una capacidad que funciona.
- **Las acciones del nodo viven ahora en DOS sitios del host Uno** (el panel de la tarjeta y el bloque de la ficha): es la paridad con el escritorio y una decisión deliberada — el atajo del lienzo no se toca, y la ficha garantiza que la superficie no dependa de saber desplegar una tarjeta.
- **Un nodo sin acciones no despliega nada**: sin conmutador no hay panel, y el estado `IsExpanded` del núcleo se conserva (los grafos guardados no cambian de forma).
- **El botón de la ficha ejecuta la MISMA orden** que el de la tarjeta, así que en este host hereda su frontera: las superficies que el catálogo sirve (gestor de presets, diseñador de datasets) se abren; las que son ventanas del toolkit (configuración del VLM, estudio de scripts, gestión de contraseñas) **avisan** nombrando la ventana. La ficha no finge una capacidad que el host no tiene.

---

## [2026-09-28] - Hito 268: La Solución del Host Uno que Compila con `dotnet` y sin Avalonia (el Sabor de UI)

### 🎯 Objetivos y Alcance
El encargo: el host Uno se compilaba con **MSBuild de Visual Studio** (la nota de `AGENTS.md` decía que los targets de WinAppSDK **no corren** con `dotnet build`) y su binario arrastraba **las 16 DLL de Avalonia** que entran por los cinco plugins que traen ventanas del toolkit del escritorio. Objetivo: **una solución para Visual Studio** del host Uno, que **compile también con `dotnet`** y que compile ese host **sin nada de Avalonia**.

### 🔍 La premisa caducada (lo primero que se midió)
`dotnet build FileFlow.App.Uno/FileFlow.App.Uno.csproj` **ya compilaba** —**0 errores, 37 s**, con su `.exe`—: el proyecto declara `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`, que es justo lo que hace correr los targets de WinAppSDK sin MSBuild de VS. La frontera de `AGENTS.md` **había dejado de ser cierta sin que nadie volviera a medirla**, y el camino caro (seguir invocando MSBuild de fuera) tapaba además el problema de verdad: **Avalonia viajaba al host**. De los cinco plugins con ventanas (`AI`, `Archives`, `FileSystem`, `Integrations`, `Scripting`) salen **7 ventanas `.axaml`**, 12 ficheros `.cs` con `using Avalonia` y, en el binario del host, **16 DLL** (`Avalonia.*`, `AvaloniaEdit`, `Material.Icons.Avalonia`).

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.Uno.slnx` (nuevo) | La solución del host Uno: su proyecto y **todo su grafo**, sin `FileFlow.App` (el host Avalonia) ni `FileFlow.Tests`. Se abre en Visual Studio **y** compila con `dotnet build`. |
| `Directory.Build.props` | El **SABOR DE UI**: `FileFlowUnoHost` sale del **NOMBRE de la solución** (`$(SolutionFileName) == 'FileFlow.Uno.slnx'`), `FileFlowDesktopToolkit` es su inverso y de ahí sale la constante `FILEFLOW_NO_DESKTOP_TOOLKIT` que leen los nodos. El defecto es **escritorio**: compilar un proyecto suelto (o la solución del escritorio) no cambia de producto por sorpresa, y el script pasa `-p:FileFlowUnoHost=true` explícito para no depender del nombre. |
| 5 `FileFlow.Plugin.*/…csproj` | Los paquetes de Avalonia (`Avalonia`, `Avalonia.Themes.Fluent`, `Avalonia.Controls.DataGrid`, `Avalonia.AvaloniaEdit`, `Material.Icons.Avalonia`) pasan a estar **condicionados** al toolkit, y el sabor Uno declara qué ficheros **no compila** (las 7 ventanas, sus convertidores, los dos view models que sólo existen para ellas) y añade `Material.Icons` —el paquete puro— porque el árbol del diseñador sí elige su icono. |
| `FileFlow.Sdk/Services/DesktopOnlySurface.cs` (nuevo) | La **costura de la frontera**: el nodo que se compila sin el toolkit **no** construye su ventana y **declara** que pertenece al escritorio por los diálogos de quien lo abrió (`ShowWarning`) más una traza en el canal de errores. El **texto** no vive aquí: lo pone cada plugin (mecanismo en el SDK, palabras en quien las dice). |
| Los **7 nodos** | `SmartUnpackNode`, `ArchiveFanOutNode`, `MultimodalVisionLlmNode`, `AdvancedRenamerNode`, `SyntheticDataSourceNode`, `MediaTranscoderNode` y `CustomScriptNode`: su bloque del toolkit (construir la ventana, resolver el propietario, `ShowDialog` y leer el resultado) queda dentro de su región condicional, con `DesktopOnlySurface.Declare` en la mitad sin toolkit. Los dos que **además declaran su superficie** al SDK (diseñador de datasets y gestor de presets) llevan ahí su **defensa declarada**: en el host Uno se sirven por el catálogo del host y por este camino no se llega (el núcleo abre la superficie declarada antes de tocar la acción). |
| `UI/Services/DesktopFilePicker.cs` (nuevo, plugin FileSystem) | La costura del **selector de archivos** del Diseñador de Datasets: su view model es PORTABLE —lo pintan los dos hosts— y hasta ahora llamaba a la API de almacenamiento de Avalonia, así que arrastraba el toolkit entero *y* hacía del importar/exportar del host Uno un **no-op silencioso**. Una implementación por sabor (la de escritorio monta el selector real; la del host declara la frontera y devuelve «no hay fichero»). |
| `run-uno.ps1` | Compila con **`dotnet build FileFlow.Uno.slnx`** (adiós al parámetro `-MsBuildPath` y a MSBuild de VS) y sigue esperando el proceso y heredando el código de salida en los sondeos. |
| `FileFlow.Tests/Unit/App/UnoHermeticBuildGuardTests.cs` (nuevo) | La guardia del sabor: 7 casos que atan la solución (y que todo lo que el host referencia esté en ella), el sabor y su constante, la condición del toolkit en los cinco plugins, la **medición del hermetismo** (ningún fichero que el sabor Uno compila menciona Avalonia **fuera de una región condicional**, con la profundidad de preprocesador como criterio y el censo de >100 ficheros como anti-vacuidad), la frontera de los 7 nodos, sus textos en los DOS idiomas (y las claves de los nombres que citan) y el lanzador. |
| `FileFlow.Tests/Unit/App/DesktopOnlySurfaceTests.cs` (nuevo) | La medición **por comportamiento** de la costura: con un doble de diálogos que se acuerda, la frontera se dice **una vez** y con el nombre de la superficie; sin diálogos no revienta. |

### 🔬 Lo que encontró la medida (y lo que cazó la guardia mientras se escribía)
1. **La premisa escrita y nunca re-medida.** `AGENTS.md` afirmaba que el host Uno **no** compilaba con `dotnet build`. Era **falso desde que el proyecto declaró `WindowsPackageType=None`**: la nota sobrevivió al cambio que la invalidaba. Lección del tramo: una frontera declarada sin fecha de caducidad se vuelve una excusa para no probar el camino corto.
2. **El compilador fue el mapa del acoplamiento.** Enumerar «qué menciona Avalonia» a mano dejaba fuera cosas: el primer `dotnet build FileFlow.Uno.slnx` falló por **7 errores concretos** —dos usings de namespaces que dejan de existir sin el toolkit, el view model del VLM que sólo existía para su ventana y `MaterialIconKind` en el árbol del diseñador— y cada uno fue una pieza de arquitectura que estaba escondida detrás de un `using`.
3. **La guardia cazó su propio criterio, dos veces.** La primera, al confundir **prosa con código**: el `DesktopFilePicker` explicaba en su documentación que el toolkit es de Avalonia y el barrido lo contaba como mención (ahora lee el código **sin comentarios**). La segunda, al censar por prefijo: `FileFlow.App.Core` —la capa PORTABLE que los dos hosts comparten— empieza igual que el host de escritorio, así que el censo se mira **por directorio** y no por nombre.
4. **Las DLL de Avalonia sobrevivían al cambio de fuente** en el `bin` del host (un build incremental no borra lo que ya estaba): el «cero Avalonia» se midió **limpiando la salida** y volviendo a compilar, no confiando en el build incremental.

### 🛡️ Guardias, pruebas y mutaciones
7 casos en `UnoHermeticBuildGuardTests` + 2 en `DesktopOnlySurfaceTests` (**+9**, la suite pasa de 1951 a **1960**). Una mutación nueva **`frontera-de-escritorio-que-no-avisa` → MUERDE** (30,5 s; testigo `DeclaringTheFrontier_ShouldShowItInTheHostDialogs` rojo, control `NoPluginThatDrawsAWindow_ShouldReferenceTheToolkit_OutsideItsCondition` verde). `COVERAGE.md` regenerado por su guardia: **99 declaradas · 15 de 17 subsistemas · 17 de 46 guardias** con una mutación que las muerde (antes **98 · 17 de 45**).

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| `dotnet build FileFlow.Uno.slnx` | **Compilación correcta · 0 errores** (los 51 avisos son los preexistentes del host) |
| Salida del host Uno | **0 DLL de Avalonia** (antes **16**: `Avalonia.*`, `AvaloniaEdit`, `Material.Icons.Avalonia`), medida tras limpiar `bin/` y `obj/`; queda `Material.Icons.dll`, que es el paquete puro del icono |
| Sonda del host Uno (`.\run-uno.ps1 -SelfCheck`) | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]` · VERIFICADO**, el mismo recuento que antes del cambio: la hermesis del build no tocó el comportamiento del host |
| Suite completa | **1959 superadas + 1 omitida de 1960, 0 errores** (2 m 58 s; antes **1950 + 1**: los siete casos del sabor y los dos de la costura) |
| Mutaciones | **1 nueva, MUERDE** · **99 declaradas** · **17 de 46 guardias** con mutación que las muerde |
| Solución del escritorio | `FileFlow.slnx` **intacta** (la guardia exige que siga compilando la app con Avalonia) |

### 🟠 Fronteras declaradas
- **Lo que el host Uno pierde, y se dice al decirlo**: las siete ventanas del toolkit (Gestor de Contraseñas, configuración del VLM, Estudio de Renombrado, Diseñador de Datasets por la acción personalizada, Gestor de Presets por la acción personalizada, Estudio de Scripts) y el **selector de archivos** del diseñador. Dos de ellas se sirven por su **superficie declarada** (el catálogo del host las cumple); las otras cinco **avisan con el nombre de la ventana** y su motivo por los diálogos del host, en vez de no hacer nada. Antes de este tramo, empujar esos botones construía una ventana de **otro framework** dentro del proceso WinUI.
- **El sabor es una propiedad del GRAFO, no del código**: los plugins siguen siendo **los mismos** y el escritorio no cambia una línea de su comportamiento; lo que cambia es qué mitad de cada plugin se compila. Las dos soluciones **se pisan los `bin`** de los plugins (el último build manda), así que compilar un host recompila los plugins para ese host.
- **`FileFlow.Tests` no entra en la solución del host Uno**: sus pruebas montan ventanas de Avalonia (siguen siendo del sabor de escritorio) y entran por `FileFlow.slnx`.
- **El host Uno sigue sin empaquetado**: se compila y se ejecuta, no se reparte instalado (la frontera de entrega sigue abierta).
- **La frontera NO se ha ejercido con el host Uno abierto**: lo que está medido es (a) que el sabor Uno compila y su salida no lleva Avalonia —build tras limpiar, guardia del censo y sonda del host en verde—, (b) que la costura **avisa de verdad** (prueba de comportamiento con un doble de diálogos) y (c) que **cada nodo la declara** con el nombre de su ventana y sus textos en los dos idiomas. **Empujar esos botones con la aplicación abierta y leer el aviso** —y su canal externo— sigue siendo materia de una sesión manual, como el resto de las fronteras declaradas.

---

## [2026-09-28] - Hito 267: La Sonda del Escritorio (el Trazo, el Pan y el Zoom con Puntero Inyectado)

### 🎯 Objetivos y Alcance
Cerrar la segunda mitad de la frontera que el **266 declaró**: «el escritorio **no tiene sonda propia** (las `--selfcheck*` son del host Uno) y el trazo **con un dedo** queda para una sesión del escritorio». Objetivo: dar al host de escritorio su **propia sonda de autorrevisión** —arranca la **aplicación real** y la mide desde dentro, con **veredicto por código de salida** e informe junto al ejecutable, como las del host Uno— y **usarla para medir el lienzo con puntero inyectado**: el trazo del cable, el pan y el zoom, cada uno con su gesto y con la medida repetida después.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `App/SelfCheck/DesktopSelfCheck.cs` (nuevo) | La sonda: `--selfcheck` arranca la aplicación real (servicios, plugins, vistas y estilos del producto) y mide **el trazo del cable** (cada cable dibujado con el control del host, su curva contra `ConnectionGeometry.BuildWire` y su extremo **sobre el punto dibujado de su socket, en píxeles de ventana**), el **pan** con el botón derecho, el **zoom** con la rueda (acercar, alejar y el techo `MaxViewportZoom`) y el **reparto de botones** (el izquierdo sobre el fondo **no** panea). Informe en `selfcheck-report.txt` con líneas `[OK]`/`[FALLO]` y recuento; veredicto por `desktop.Shutdown(0/1)`. |
| `App/Program.cs` | La **puerta** del modo: con el argumento, arranca el anfitrión de la sonda **antes** del arranque normal (y sin el argumento, nada cambia). |
| `App/App.axaml.cs` | La medida corre **con la ventana ya en pantalla** y el arranque **no lanza la comprobación de actualizaciones** en este modo: su red y su aviso romperían la hermesis del veredicto (el mismo corte que hace el host Uno). |
| `App/FileFlow.App.csproj` | `Avalonia.Headless`: la plataforma **headless con Skia real**, que es la única forma de que un puntero entre por el **pipeline de entrada** de Avalonia desde dentro del proceso (el escritorio no expone inyección de entrada cruda). Fuera de este modo no se usa. |
| `run.ps1` · `run-fast.ps1` | `-SelfCheck`: pasa el argumento, **espera el proceso y hereda su código de salida** (los gemelos del escritorio de `run-uno.ps1 -SelfCheck`). |
| `FileFlow.Tests/Unit/Views/DesktopCanvasGestureTests.cs` (nuevo) | La misma entrada —puntero inyectado por el pipeline, la que ya usaba `InputSimulator`— desde el suite y **con la escena bajo control**: el aterrizaje del trazo sobre el punto dibujado de cada socket, el pan (encuadre y desplazamiento del trazo), el zoom (acercar, alejar y el techo), y el arrastre de una tarjeta que mueve el nodo y **no** el plano. |
| `FileFlow.Tests/Unit/App/DesktopSelfCheckGuardTests.cs` (nuevo) | La guardia de la **puerta** del modo: que exista en la línea de comandos de la aplicación, que arranque el anfitrión **que sabe inyectar un puntero**, que **sólo mida** (los gestos mueven el encuadre; la sonda no lo escribe) y que prepare la escena sin escribir estado del usuario. |

### 🔬 Lo que encontró la medida (cuatro cosas que sólo aparecen midiendo)
1. **El gesto iba a la pieza flotante.** La primera corrida eligió el punto del gesto fijándolo en la esquina libre del lienzo… y ahí está la **barra de zoom**: el puntero pulsó **encima** de ella y la sonda midió el silencio del lienzo (el encuadre no se movía y el trazo tampoco). Ahora el punto se **busca por hit-testing** —el primero cuyo recorrido de respuesta llega al lienzo y no a una tarjeta, un decorador, un cable ni la barra de zoom—, con la misma idea que se ganó en el host Uno al medir el hit-testing.
2. **La escena que se medía a sí misma.** El bucle de reintentos —copiado del host Uno, donde la medida es del árbol y puede repetirse— aquí **mutaba** la escena: cada intento paneaba y hacía zoom sobre el anterior, así que la corrida siguiente medía un encuadre `-120, -64` con el zoom ya al tope que **ningún usuario ve**, y la fase del pan salía en rojo por un estado que la propia sonda había dejado. Ahora la espera **no mide** (espera a que el lienzo tenga tamaño) y la medida es **una sola vez**, sobre la escena que el producto presenta.
3. **El ancla medida contra la etiqueta del puerto.** Medir el aterrizaje contra el **control del conector** daba un hueco de **14,5 px** —el ancho de la etiqueta del puerto, que vive dentro del mismo control—. El punto que el usuario ve es la **figura del socket** (la plantilla `PortSocketTemplate`, clases `socket`/`socketTriangle`), y es donde Nodify sitúa el ancla del puerto: con esa referencia el hueco es **0,00 px** en las dos anclas, con el plano quieto y después de cada gesto.
4. **El arrastre de una tarjeta no es un gesto del lienzo.** La fase se midió y se **retiró** de la sonda, con su razón escrita: la tarjeta **se lleva al frente** al pulsarla —lo que reordena la colección del documento— y, si el puntero se va hacia el borde, el editor **auto-paneea** mientras arrastra: dos efectos del propio producto que no tienen que ver con el reparto de botones que la sonda defiende. En su lugar la sonda mide **el botón que no panea** (el izquierdo sobre el fondo) y el arrastre de la tarjeta se queda en el suite, donde la escena está bajo control (y donde una mutación lo muerde).

### 🛡️ Guardias, pruebas y mutaciones
Cuatro casos de gesto (`DesktopCanvasGestureTests`): el trazo toca sus sockets con el plano quieto, el **pan** mueve el encuadre el gesto dividido por el zoom —y el trazo sigue a la mano—, la **rueda** acerca, aleja y se detiene en el techo (2,5) sin despegar el trazo de sus sockets, y el arrastre de una tarjeta mueve el nodo **y no el plano**. Cuatro casos de puerta (`DesktopSelfCheckGuardTests`): el modo en la línea de comandos y el anfitrión que inyecta, el veredicto por código de salida y su informe, la medida **en espacio de ventana** y **sin escribir el encuadre** (una regla que impide que la sonda se mida a sí misma), y el arranque sin trabajo de fondo. **Dos mutaciones nuevas, las dos MUERDEN**: `pan-que-responde-al-boton-izquierdo` (32,1 s; testigo `TheRightDragOnTheCanvas_…` rojo, control —el arrastre de la tarjeta, que el mutante **no** rompe, medido— verde) y `sonda-de-escritorio-sin-el-anfitrion-del-puntero` (29,3 s; testigo `TheProbe_ShouldBeItsOwnCommandLineMode_…` rojo, control `TheCable_ShouldTouchItsSockets_InWindowSpace` verde). `COVERAGE.md` regenerado por su guardia: **98 declaradas · 15 de 17 subsistemas · 17 de 45 guardias** con una mutación que las muerda.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| Compilación (`FileFlow.App`, XAML de Avalonia incluido) | **0 errores** |
| Sonda del escritorio (`.\run.ps1 -SelfCheck`) | **EXIT 0 · 41 `[OK]` · 0 `[FALLO]` · VERIFICADO**, y **el informe es byte a byte idéntico entre dos corridas** (medido con `diff`): la medida no depende del reloj ni del azar de la carga |
| Suite completa | **1950 superadas + 1 omitida de 1951, 0 errores** (cuatro corridas: 2 m 40 s, 2 m 45 s, 2 m 40 s y 2 m 44 s; antes **1942 + 1**: los cuatro gestos y las cuatro guardias nuevas) |
| Rojo intermitente | **una corrida trajo 1 fallo que no quedó nombrado** (la salida se cortó con un `tail`); las **tres** corridas siguientes —una de ellas con compilación, como la que falló— quedaron **verdes** con los mismos 1950. Es el ruido de carga ya declarado en los hitos 255, 258 y 262 |
| Mutaciones | **2 nuevas, las 2 MUERDEN** · **98 declaradas** · **17 de 45 guardias** con mutación que las muerde |
| Sonda del host Uno | No se re-ejecutó en este tramo: lo tocado es del escritorio (`FileFlow.App`) y el host Uno **no** referencia ese ensamblado (referencia `FileFlow.App.Core`, `Sdk`, `Core` y sus plugins); la suite cubre los dos hosts |

### 🟢 Las cifras que midió la sonda (la evidencia)
Escena: lienzo **1075×565**, ejemplo `flow_01_organizador_imagenes.json` (**3 nodos · 2 cables**), dos cables dibujados de dos, y la curva de cada uno **la del núcleo**. **Trazo**: hueco `0,00 px` en las dos anclas. **Pan** (arrastre de `(120, 80)` px con el derecho, zoom 1): el encuadre se mueve `(-120,0, -80,0)` —el gesto dividido por el zoom, exacto—, el trazo se desplaza `(120,0, 80,0)` **siguiendo a la mano** y sus huecos siguen en `0,00 px`. **Zoom**: la rueda hacia arriba `1,000 → 2,000`; hacia abajo `2,000 → 0,200` (el suelo `MinViewportZoom`); **20 muescas** dejan el zoom en `2,500` (el techo) y el trazo sigue sobre sus sockets (`0,00 px` con la escala al tope). **Reparto de botones**: el arrastre izquierdo sobre el fondo **no** mueve el encuadre (`-120,0, -64,0` antes y después).

### 🟠 Fronteras declaradas
- **El puntero es del pipeline de entrada, no del sistema operativo**: la sonda inyecta movimiento, pulsación, arrastre y rueda por el camino real de Avalonia (hit-testing, gestos del editor, captura de puntero) sobre la aplicación montada, pero **no** es un dedo del sistema operativo sobre la ventana nativa —eso sigue siendo materia de una sesión manual—. La frontera va escrita **en el propio informe**, para que nadie lea sus 41 `[OK]` como una sesión con ratón.
- **El precio del instrumento**: la sonda necesita `Avalonia.Headless` en el proyecto del producto (el modo es el único que lo usa). La alternativa —medir sobre la plataforma nativa— no puede inyectar entrada desde dentro del proceso, y una sonda sin dedos daría verde midiendo un lienzo quieto (lo vigila la mutación del anfitrión).
- **El arrastre de una tarjeta no lo mide la sonda** (se mide en el suite, con su razón escrita arriba).
- **La escena es un ejemplo del catálogo**, cargado con el mismo camino que usa el host Uno (`WorkflowGraph.FromJson` + `LoadFromGraphModel`), porque el escritorio **no carga documento al arrancar**: la sonda no escribe nada del usuario (ni preferencias, ni contadores de uso) y el grafo vive en memoria hasta que el proceso termina.
- **Sin comparación de píxeles entre hosts**: lo medido es la geometría dibujada proyectada al espacio de la ventana; que el **píxel** del cable coincida con el del host Uno sigue sin medirse.

---

## [2026-09-28] - Hito 266: El Cable del Escritorio lo Dibuja el Trazador Compartido (y la Frontera que el 254 Dejó Declarada)

### 🎯 Objetivos y Alcance
Cerrar la frontera que el hito 254 **midió y declaró sin arreglar**: la geometría del cable vive desde entonces en el núcleo (`ConnectionGeometry`, la Bézier que **nace y muere en las anclas**), el host Uno la dibuja… y el **escritorio seguía dibujando con el control de conexión de Nodify**, que traza su propia curva —una Bézier retirada de las anclas y unida a ellas por dos **tramos rectos**, con el cuello `Spacing` fijo—. Objetivo: el escritorio dibuja con la geometría compartida (no sólo la usa para el hit-testing), con su guardia y su mutación.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `App/Views/Components/FlowConnection.cs` (nuevo) | Un `Shape` de Avalonia cuyo `DefiningGeometry` sale de **`ConnectionGeometry.BuildWire`**: la curva la decide el núcleo y el host pone el envoltorio del framework. Conserva lo que el lienzo usa —`Stroke`, `StrokeThickness`, `StrokeDashArray`, `Cursor`, las clases por familia de tipo y el menú contextual— y, del control, lo que era bueno: cuello horizontal, techo `100 + √(25·ancho)` y tope de la mitad del hueco. |
| `App/Converters/GraphConverters.cs` | `ConnectionDirectionConverter`: el cable **en curso** lo arrastra el control de Nodify (su flag: arrastrar desde una entrada va hacia atrás) y el dibujo se pide en el vocabulario del núcleo. Los dos flags tienen los mismos dos valores. |
| `App/Views/EditorView.axaml` | La plantilla de cables usa `components:FlowConnection` (mismos enlaces de ancla con el conversor de punto, mismo menú de borrado). Se va el `Spacing="45"` del control. |
| `App/Styles/Ports.axaml` | Los estilos del cable apuntan al control nuevo (`components|FlowConnection` y sus siete familias de tipo) y el **cable en curso** —el de la plantilla del `PendingConnection`— se dibuja con el **mismo** control: el trazo ya no cambia de forma al soltar el botón. |
| `FileFlow.Tests/Unit/Views/FlowConnectionGeometryTests.cs` (nuevo) | Cinco casos que leen la figura que el control **va a pintar** (`DefiningGeometry`) y la comparan punto por punto con el núcleo: nace y muere en las anclas, el cuello es el del núcleo (200 contra los 45 del control), se da la vuelta con `Backward`, sigue a sus anclas al moverse y el conversor habla los dos vocabularios. |

### 🛡️ Guardias, pruebas y mutaciones
`NodeCardVisualContractTests` gana **`EveryCableOfTheCanvas_ShouldBeDrawnWithTheCoreGeometry_NotWithTheNodifyControl`**: barre el XAML del lienzo para que **ningún** cable vuelva al control de Nodify, exige que los **dos** (establecido y en curso) usen el control del host y que la curva se pida a `ConnectionGeometry`. Los casos del cable se reapuntan al control nuevo (la plantilla, el menú contextual colgando del trazo, los estilos por familia de tipo) y `GeometryBindingProjectionTests` sigue midiendo **en el árbol real** que los dos extremos del cable llevan el ancla convertida de su puerto: al reapuntarlo, el mismo caso comprueba también que la figura dibujada es la del núcleo para esas anclas. Y **`ThePendingCable_ShouldAlsoBeDrawnWithTheCoreGeometry`** monta el cable **en curso** en una ventana —con los estilos de la aplicación— y exige que sea el control del host, que su dirección salga del conversor (arrastrar desde una entrada, `Backward`) y que la figura sea la que el núcleo traza para esas dos anclas. **Una mutación nueva `cable-de-escritorio-por-el-control-de-nodify` → MUERDE** (29,2 s, testigo rojo y control —`TheCable_ShouldBeTheCoreCurve_BetweenItsTwoAnchors`, que mide el trazador sin pasar por el lienzo— verde). `COVERAGE.md` regenerado por su guardia: **96 declaradas · 15 de 17 subsistemas**.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| Compilación (`FileFlow.App`, XAML de Avalonia incluido) | **0 errores** |
| Suite completa | **1942 superadas + 1 omitida de 1943, 0 errores** (dos corridas verdes: 2 m 37 s y 4 m 21 s; antes **1935 + 1**) |
| Mutaciones | **1 nueva, MUERDE** · **96 declaradas** |
| Sonda del host Uno (el que ya dibujaba con la geometría compartida) | `--selfcheck` **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`**, con sus medidas de cable (cables dibujados, el cable toca su socket con el plano quieto y tras pan/zoom, y la forma cabe en un hueco estrecho sin el rulo del 2) |
| Rojo intermitente | `EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` (medida de CPU) cayó en la corrida con carga y queda **verde en aislamiento**; las dos corridas completas de la suite, verdes |

### 🟠 Fronteras declaradas
- **No se mide en la app del escritorio con puntero**: el escritorio no tiene sonda propia (las `--selfcheck*` son del host Uno). Lo que se mide aquí es la **figura** que el control va a pintar y el **árbol visual real** del lienzo headless (control materializado, ancla enlazada y trazo con el color de su familia de tipo); el trazo **con un dedo** queda para una sesión del escritorio.
- **El contenedor de Nodify se conserva**: `ConnectionContainer` sigue envolviendo cada cable (selección, foco y el menú contextual del contenedor no se tocan; el lienzo no activa su `IsSelectable`). Lo que se sustituye es **quien dibuja el trazo**.
- **Heredado del 254 y sin tocar**: el caso **apilado** (anclas sin hueco horizontal) dibuja recta vertical, y la **caída tipo hilo** no está implementada.

---

## [2026-09-28] - Hito 265: Las Seis Órdenes Destructivas que Quedaban Mudas (y el Contenido que Nacía sin Diálogos)

### 🎯 Objetivos y Alcance
Cerrar las **seis órdenes destructivas** que el hito 264 **localizó y declaró sin arreglar**: todas preguntaban por la variante **síncrona** del contrato de diálogos, que en este host **no muestra nada y no hace nada** (su hilo de UI no puede bloquearse) y que en un servicio sin diálogos contesta «sí» **sin preguntar**. Objetivo: la **misma regla compartida** que ya existía —`IDialogService.ConfirmAsync` y la decisión en el **view model portable**—, sin duplicar lógica en las vistas, sin cambiar las órdenes que no destruyen, y declarando con su razón lo que no se toca.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `App.Core/ViewModels/ControlBarViewModel.cs` | `NewWorkflowAsync` y `RollbackLastExecutionAsync` esperan `ConfirmAsync`. **`CreateNewWorkflow()` se separa**: confirmar no es parte de crear un flujo, y quien ya tiene la respuesta no necesita el diálogo. |
| `App.Core/ViewModels/ThemeCustomizerViewModel.cs` · `VirtualFileSystemExplorerViewModel.cs` · `AiModelManagerViewModel.cs` | `DeleteThemeAsync` · `ClearVirtualFileSystemAsync` · `DeleteModelAsync`: los tres esperan la respuesta real. |
| `Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs` | `DeleteDataSetAsync` espera la respuesta real. |
| `App.Uno/Controls/ThemeCustomizerBody.xaml(.cs)` | El **botón «Eliminar» del Estudio de Temas**, dibujado (`ThemeStudioDeleteButton`, habilitado sólo con un tema propio) y su orden ejecutada por el comando del view model: su fila sale de `DeclaredPendingParts`. La orden existía **sin puerta**. |
| `App.Uno/MainWindow.xaml.cs` | «Nuevo Flujo» deja de confirmar en la vista: ejecuta la **orden CANÓNICA** (que ya pregunta por el contrato asíncrono) y refresca el renglón del ciclo que lee el canal externo. |
| `App.Uno/Controls/ControlBar.xaml.cs` | El atajo `Ctrl+N` enruta por el **mismo camino** que el botón del cajón (una orden, un camino) y la fila de `HostOwnedOrders` explica el desvío. |

### 🐛 Los tres defectos que encontró la revisión adversarial
1. **El diseñador de datasets nacía con el doble nulo.** El nodo declaraba su superficie pero construía el contenido **sin diálogos** (`new SyntheticDataSetDesignerViewModel()`), y el nodo —que vive en un ensamblado de plugin— no puede resolverlos: se los pasa quien abre, por el `NodeCustomActionContext`. Cambiar la pregunta a la vía asíncrona **no bastaba**: la asíncrona del doble nulo **delega en su síncrona**, que contesta «sí». El diseñador **borraba en silencio en los DOS hosts**. Arreglado en los tres caminos (el contenido del nodo, la ventana del escritorio y la puerta del host Uno).
2. **Un evento muerto que el compilador cantó** (`CS0067`): al pasar «Nuevo Flujo» al comando canónico, `Bar.NewWorkflowRequested` dejó de dispararse y la ventana seguía suscrita, así que el atajo `Ctrl+N` ejecutaba el comando **sin** los dos pasos de host que sí hacía el cajón (el rastro y el refresco del renglón del ciclo que lee el canal externo). Ahora el atajo pide la orden a la ventana, como el cajón.
3. **La pregunta del borrado de un modelo estaba escrita en el código.** Al revisar las ocho órdenes, siete ya sacaban su texto del diccionario (`_loc.GetString` / `LocalizationManager.Instance`) y ésta lo llevaba literal —«¿Estás seguro de que deseas eliminar el modelo 'X' del disco local?» y «Eliminar Modelo»—, así que **no cambiaba de idioma nunca**. Ahora va por el diccionario (claves `AiModelManager_ConfirmDeleteMsg` / `AiModelManager_ConfirmDeleteTitle` en los **cuatro** diccionarios de los dos hosts) y la guardia **exige** que el método de cada orden destructiva lea al menos un texto del diccionario: el defecto no destruye nada, así que ninguna medición de datos lo ve, y por eso se mide con un mutante que vuelve a escribirlo.

### 🛡️ Guardias, pruebas y mutaciones
`UnoNodeDialogsGuardTests` **14 de 14**: el caso nuevo **`EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne`** ata la tabla de las **ocho** órdenes destructivas (método asíncrono con su `[RelayCommand]` y `await _dialogService.ConfirmAsync(`), **barre el árbol de fuentes del producto** para que nadie vuelva a preguntar por la vía síncrona y exige que el contrato siga conservándola; y **`EveryDeclaredSurface_ShouldCarryTheHostDialogs_SoItsDestructiveOrdersCanAskForReal`** ata la entrega de los diálogos del host al contenido de las dos superficies declaradas. `UnoControlBarParityGuardTests` **13** (el reparto de las tres órdenes de flujo, con el atajo enrutado por el camino de la ventana). Pruebas nuevas: el **vaciado del VFS** (confirmado vacía / **cancelado no toca nada**, con la síncrona contestando «sí» a propósito como trampa) y el **borrado del dataset** (16 casos en total). El caso de la tabla exige además que **la pregunta de cada orden venga del diccionario** (clave y texto de reserva), no de un literal escrito en el código: sin esa mitad, el texto de una orden destructiva se queda en un idioma para siempre. **Cinco mutaciones nuevas, las cinco MUERDEN** (`vfs-que-se-vacia-sin-preguntar` 60,4 s · `disenador-que-borra-sin-preguntar` 51,1 s · `orden-destructiva-que-vuelve-a-la-via-sincrona` 44,5 s · `superficie-declarada-sin-los-dialogos-del-host` 45,0 s · `pregunta-destructiva-escrita-en-el-codigo` 29,2 s; las cinco **re-ejecutadas** sobre el árbol final), todas con testigo rojo y control verde. `COVERAGE.md` regenerado por su guardia: **95 declaradas · 15 de 17 subsistemas**.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** / **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 46 `[OK]` · 0 `[FALLO]`** / **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias / pruebas | **14 + 13 + 5** / las dos del VFS y las dos del dataset en verde |
| Mutaciones | **5 nuevas, las 5 MUERDEN** · **95 declaradas** |
| Suite completa | **1935 superadas + 1 omitida de 1936, 0 errores** (dos corridas completas verdes: 4 m 5 s y 2 m 37 s) |
| Rojo intermitente | **una corrida intermedia trajo 1 fallo que no quedó nombrado** (la salida se cortó al leerla); las dos corridas completas siguientes, verdes, y `ExampleFlowsEndToEndTests` **4 de 4 en aislamiento** |
| Sesión con la app abierta | **33 de 33 pasos** (`qa-manual-265`) |

### 🟢 Ejercido con la aplicación abierta
**Dos** de las seis órdenes, las dos restaurables sin tocar datos del usuario. **«Eliminar tema» del Estudio**: catálogo con **1** tema propio → el estudio se abre desde el cajón (**4 anclas**) → «Nuevo tema» lleva el **fichero** del almacén de **1 a 2** → «Eliminar» **PREGUNTA** (`HostConfirmationAccept`/`HostConfirmationCancel`, **dentro del estudio**, centro `#B0ACAC`) → con la pregunta en pantalla siguen **2** → **cancelar deja 2** → **confirmar deja 1**, con el catálogo **idéntico** y el fichero **byte a byte** (`f8b1a4e9…`). **«Nuevo Flujo»**: **PREGUNTA** en su **propio modal** (`HostConfirmationDialog`, botones «Aceptar»/«Cancelar») → con la pregunta siguen las **3 tarjetas** → **cancelar las deja** (árbol y **3** barras de acento por pixel, pixel de base `#FCF8F8`) → **confirmar vacía el lienzo** (**0** tarjetas, **0** barras de acento). El grafo no se persiste: al **reiniciar**, el lienzo vuelve a sus **3 tarjetas**. Al final: temas **byte a byte**, presets **intactos** (`9b1e8f19…`), preferencias con el **mismo md5** antes y después de las dos órdenes —el driver lo lee del fichero y lo compara; la única clave que reescribe la sesión entera es `LastUpdateCheckUtc`, y la escribe el arranque— y ajustes esenciales intactos.

### 🟠 Fronteras declaradas
- **`ClearVirtualFileSystemAsync` no tiene puerta**: ninguna vista —ni la del escritorio ni la del host— dibuja su botón. Queda preguntando por la vía correcta y **sin entrada**; dibujarla es UI nueva, fuera del encargo. Se declara, no se finge.
- **`DeleteModelAsync` no se mide con el ratón**: su borrado retira ficheros **reales** del disco (`%AppData%\FileFlow\models`, varios GB). Se mide su determinación en la suite y no en la app abierta.
- **El escritorio no cambia**: conserva su confirmación síncrona y `ConfirmAsync` delega en ella.
- **Dos defectos del INSTRUMENTO, escritos para el guion futuro**: `ThemeStudioBody` no existe para el canal externo (la raíz del estudio es un `Grid` **sin peer de automatización**: el driver reconocía el estudio por un ancla que nunca llega) y el **pixel del centro no distingue un lienzo vacío de uno con tarjetas** (el fondo es el mismo: las tarjetas se cuentan por su **barra de acento**).

### 📄 Evidencia
[`docs/qa/qa_destructive_orders_265.md`](file:///docs/qa/qa_destructive_orders_265.md) + `docs/qa/qa-manual-265/` (capturas `40_…`-`48_…`, `destructivas-session.json`) + el driver `docs/qa/qa_destructive_uia.py`.

---

## [2026-09-28] - Hito 264: La Confirmación de las Órdenes Destructivas del Gestor (y las Dos Puertas que se Comportan Igual)

### 🎯 Objetivos y Alcance
Cerrar el defecto que el tramo anterior **midió y declaró sin arreglar**: las órdenes destructivas del gestor de presets **no pedían confirmación** y **se comportaban distinto según la puerta** —por la **fila** el view model recibía el servicio **Nulo** (`ShowConfirmation => true`) y borraba **en silencio**; por la **tarjeta** recibía el del host, cuya confirmación es **síncrona** y devuelve «no» desde el hilo de UI, así que **no borraba y tampoco avisaba**—. Objetivo: **una sola regla** con la semántica del escritorio (borrar pregunta de verdad y depende de la respuesta REAL del usuario, sin bloquear el hilo de UI, y ninguna puerta borra en silencio ni deja de avisar), sin tocar el resto de la superficie.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Sdk/Services/IDialogService.cs` | **`ConfirmAsync`**: la confirmación asíncrona del contrato, con implementación por defecto que delega en la síncrona en un hilo de fondo (el escritorio y los dobles no cambian). |
| `Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | La **regla**: `DeletePresetAsync` y `ResetDefaultsAsync` **esperan la respuesta real** y sólo destruyen si el usuario dijo que sí. |
| `App.Core/ViewModels/NodeParameterViewModel.cs` · `NodeViewModel.cs` | Las **dos puertas** resuelven el servicio del host: la fila deja de caer en el Nulo que auto-confirma. |
| `App.Uno/Platform/UnoDialogService.cs` | `ConfirmAsync` del host: pregunta dentro del modal abierto si lo hay, y en su propio modal si no. |
| `App.Uno/Platform/UnoWindowService.cs` | `AskInsideActiveDialogAsync`: la pregunta como **CAPA dentro del cuerpo del modal** (con su velo, su tarjeta y sus dos botones reales), **sin bloquear el hilo de UI**; `TearDownInlineQuestion` la retira y la contesta «no» cuando la abandona el cierre del modal o la sustituye otra pregunta. |
| `App.Uno/Controls/MediaPresetManagerBody.xaml.cs` | `ResetAction` · `DeleteAction`: las **dos** órdenes destructivas, expuestas para poder medirlas. |

### 🐛 Lo que encontró la medición (y quedó arreglado)
`AskInsideActiveDialogAsync` montaba la capa **sacando el cuerpo de su diálogo** para volver a colgarlo de un `Grid`: **WinUI no deja colgar un elemento de dos padres** y la llamada lanzaba `COMException` (medido: el aviso de «Guardar» y el borrado fallaban con excepción). Ahora la capa se monta **dentro** del cuerpo (el cuerpo sigue siendo el contenido del diálogo). Y el **aviso informativo** —una capa con una sola salida— dejaba el estado tomado: la pregunta siguiente se declinaba **en silencio**, así que la orden no hacía nada *y no avisaba*; ahora **la sustituye**, contestando «no» la anterior.

### 🛡️ Guardia, pruebas y mutaciones
`UnoNodeDialogsGuardTests` **12 de 12** (el caso del contrato destructivo gana la sustitución, la retirada de la capa, el montaje dentro del cuerpo y los **dos** caminos de abandono, más la medición de la segunda puerta y del aviso). `MediaPresetManagerViewModelTests` **10 casos**. **Dos mutaciones nuevas, las dos MUERDEN**: `gestor-que-borra-sin-preguntar` (33,5 s) y `pregunta-de-borrado-por-la-via-sincrona` (30,9 s), con testigo rojo y control verde. `COVERAGE.md` regenerado por su guardia: **90 declaradas · 15 de 17 subsistemas**.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** / **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 46 `[OK]` · 0 `[FALLO]`** (antes 33: **+13**) / **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias / pruebas | **12 de 12** / **10** del view model, 0 rojos |
| Mutaciones | **2 nuevas, las 2 MUERDEN** · **90 declaradas** |
| Suite completa | **1930 superadas + 1 omitida de 1931, 0 errores** (2 m 38 s) |
| Rojo intermitente | **no apareció** |
| Sesión con la app abierta | **42 de 42 pasos** (`qa-manual-263`) |

### 🟢 Ejercido con la aplicación abierta
Por **cada puerta**, el ciclo destructivo completo: alta **10 → 11** · «Eliminar» **pregunta** (anclas `HostConfirmationAccept`/`HostConfirmationCancel` en el árbol; el centro pasa a `#B0ACAC` por el velo) · **con la pregunta en pantalla siguen 11** · **cancelar deja 11** · **confirmar deja 10**. Y «Restablecer» pregunta con su opción de cancelar: cancelarlo deja el catálogo del usuario intacto. Al final, lienzo con **3 tarjetas**, almacén **byte-idéntico** (`9b1e8f19477c5ebc3605ac373a38b38b`) y **ajustes del usuario intactos**.

### 🟠 Fronteras declaradas
- **El mismo patrón sigue en seis órdenes destructivas del host** (cerrar/nuevo flujo con cambios sin guardar ×2, restablecer un tema, limpiar el VFS, borrar un modelo descargado, quitar un dataset sintético): todas usan la confirmación **síncrona**, que en un host WinUI **no muestra nada y no hace nada**. Quedan **declaradas y localizadas**, con el mismo arreglo de una línea + su guardia para el próximo tramo: cambiarlas aquí habría sido tocar seis superficies fuera del encargo.
- **Dos preguntas a la vez** se resuelven **sustituyendo** la anterior (contestarla «no»), no encolándose.
- **El escritorio no cambia**: conserva su confirmación síncrona y `ConfirmAsync` delega en ella.
- **Defecto del INSTRUMENTO arreglado en este tramo**: la sonda comparaba el **escapado** del fichero del almacén (hex en mayúsculas contra minúsculas) y daba por fallido un guardado correcto; ahora lee el JSON y compara el valor. Y el informe guarda el **marco** de cada excepción, no sólo su mensaje.

### 📄 Evidencia
[`docs/qa/qa_presets_confirm_263.md`](file:///docs/qa/qa_presets_confirm_263.md) + `docs/qa/qa-manual-263/` (`presets-session.json`, capturas `85_*_pregunta.png`) + `selfcheck-dialogs-report.txt`.

---

## [2026-09-28] - Hito 263: El Gestor de Presets de Medios del host Uno (y la Puerta que le Faltaba a la Tarjeta)

### 🎯 Objetivos y Alcance
Portar al host Uno la superficie del **Gestor de Presets de Medios** del escritorio, con **paridad de comportamiento**: sobre **view models portables del núcleo**, con su **punto de entrada donde el escritorio lo tiene** y **sin lógica de producto en la vista**. Si ya estuviera portada, declararlo con prueba y no rehacerla.

### 🔍 Lo que se encontró
El gestor existía **sólo** como **ventana Avalonia del plugin** (`FileFlow.Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml(.cs)`) con toda su lógica en el code-behind: **no había nada que rehacer en el host Uno, porque no había nada**. Se portó con el patrón del **Diseñador de Datasets (261)**: la superficie la **declara el NODO** con el contrato del SDK y cada host la pinta sobre el **mismo** view model portable.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Sdk/Descriptors/INodeDialogSurfaceProvider.cs` | Ampliado con `ReplacesCustomActionId`: el hilo que une el botón de la TARJETA y el de la FILA con la superficie declarada. |
| `Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs` | El view model **portable** (sin toolkit): catálogo, formulario, alta/guardado/borrado/restablecimiento, normalización de la extensión y protección de los presets del sistema. **Es quien escribe en el almacén.** |
| `Plugin.Integrations/UI/Services/IMediaPresetStore.cs` | El contrato del almacén; `MediaPresetManagerService` lo implementa. |
| `Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml(.cs)` | La ventana del ESCRITORIO, **refactorizada a vista** del mismo view model: sus manejadores propios de guardar y borrar desaparecieron. |
| `Plugin.Integrations/MediaTranscoderNode.cs` | Declara su superficie (`DialogKeys.MediaPresetManager`, `ReplacesCustomActionId => "ManageMediaPresets"`) y entrega el view model portable. |
| `App.Core/ViewModels/NodeParameterViewModel.cs` · `NodeViewModel.cs` · `HostUi.cs` | Las **dos puertas** abren la superficie declarada por el catálogo de ventanas del host; el host Uno fija además `CoreDialogHost.Services`. |
| `App.Uno/Controls/MediaPresetManagerBody.xaml(.cs)` | La vista del host sobre el view model portable: **ni un cuadro suyo escribe en el almacén**. |
| `App.Uno/Platform/UnoWindowService.cs` · `NodeInspectorPanel.xaml.cs` | La clave **servida** con su vista (censo **10 servidas + 1 declarada**) y el botón «🎬» de la **fila** (`ParamPreset_`). |
| `App.Uno/Controls/NodeCardView.xaml` · `NodeCardViewModel.cs` | **La puerta que faltaba** (ver abajo). |
| `App.Uno/Resources/Strings*.resx` | **22 claves** nuevas en EN+ES. |

### 🚪 La puerta que le faltaba a la tarjeta (el defecto que encontró la medición)
El botón «🎬 Presets...» de la tarjeta vive en el panel de acciones rápidas, y ese panel cuelga de `Node.IsExpanded`… **y el host Uno no tenía ningún control que conmutara ese estado** (el escritorio lo hace con un `ToggleButton` de la cabecera). La acción estaba **dibujada y sin puerta**: el usuario no podía alcanzarla. Arreglado con el estado **del núcleo** (`NodeCardExpandToggle`: chevron arriba/abajo, dos vías con `Node.IsExpanded`, rótulo del diccionario del host con **la misma clave que el escritorio**, geometría en el adaptador y `IsExpanded` en el refresco agregado). La sonda de lienzo gana **5 comprobaciones** y la guardia exige las tres piezas.

### 🛡️ Guardia, pruebas y mutaciones
`UnoNodeDialogsGuardTests` **11 de 11** (10 + `TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive`), que ata el conmutador, el estado del núcleo que conmuta y el bloque que cuelga de él, y amplía el censo de textos a las claves `PresetManager_*`, `Node_Param_*` y el rótulo del conmutador. **9 casos nuevos** del view model portable (`MediaPresetManagerViewModelTests`, con almacén falso y diálogos que anotan). **Dos mutaciones nuevas**, las dos **MUERDEN**: `tarjeta-sin-la-puerta-de-sus-parametros` (43,3 s) y `conmutador-de-parametros-que-no-refresca` (30,9 s). `COVERAGE.md` regenerado por su guardia: **88 declaradas · 15 de 17 subsistemas**.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 88 `[OK]` · 0 `[FALLO]`** (antes 83) / **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 33 `[OK]` · 0 `[FALLO]`** (antes 24) / **EXIT 0 · 18 `[OK]` · 0 `[FALLO]`** |
| Guardias / pruebas nuevas | **11 de 11** (`UnoNodeDialogsGuardTests`) / **9** del view model portable, 0 rojos |
| Mutaciones | **8 del tramo, las 8 MUERDEN** (2 nuevas) · **88 declaradas** |
| Suite completa | **1928 superadas + 1 omitida de 1929, 0 errores** (2 m 30 s) |
| Rojo intermitente | `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` rojo en **una** corrida y **verde en aislamiento 2 de 2** → ruido de carga; la corrida final, verde |
| Sesión con la app abierta | **34 de 34 pasos** (`qa-manual-263`) |

### 🟢 Ejercido con la aplicación abierta (sesión 263), 34 de 34 pasos
Driver UIA `qa_presets_uia.py`. Medido: 3 tarjetas de base y el almacén con **10 presets** → el nodo de transcodificación se añade por el cajón (buscando la **clave** `Transcoder`, que es lo que este host muestra) y su fila expone `ParamPreset_Preset` → **puerta A (la tarjeta)**: se despliega con su conmutador, aparece `🎬 Presets...`, y al pulsarlo el gestor con **10/10 anclas** y **10 filas** cuya primera es la del almacén, pixel `#FCF8F8` → **`#B0ACAC`** → cierra y el pixel vuelve → **puerta B (la fila)**: **la misma superficie**, el formulario trae el preset **elegido** con **su** descripción → se escribe en la **caja real** y «Guardar» deja la descripción en el **fichero** (`%AppData%\FileFlow\presets\media_presets.json`), sin cerrar el modal → al reabrir, la caja trae **lo guardado** → se restaura → **«Nuevo» lleva el almacén de 10 a 11 y «Eliminar» lo devuelve a 10** → el lienzo queda con 3 tarjetas, el almacén **byte-idéntico** (`9b1e8f19477c5ebc3605ac373a38b38b`) y los **ajustes del usuario intactos**.

### 🔍 Defectos del INSTRUMENTO que encontró la medición (para el guion futuro)
1. **El almacén no estaba donde el driver lo leía**: el modo instalado de `AppPaths.RootDirectory` es `%AppData%\FileFlow` (presets en `presets/`, preferencias en `config/`), no la carpeta vieja `%AppData%\FileFlowStudio`, que guarda copias de hace semanas: medir contra ella decía «Guardar no escribe» — falso. Corregido en el driver y en `qa_dialogs_uia.PREFS`.
2. **El cajón de este host muestra la CLAVE cruda del recurso** (`MediaTranscoderNode_Name`), no el texto resuelto: hay que buscar por la clave. La tarjeta del lienzo, en cambio, **sí** muestra el texto.
3. **Una tarjeta por nodo = un conmutador por tarjeta**, todos con la misma ancla: hay que elegir el de la tarjeta medida.

### 🟠 Frontera medida (defecto del PRODUCTO, declarado y NO arreglado)
Las órdenes **destructivas** del gestor **no piden confirmación** en el host Uno, y se comportan **distinto según la puerta**: por la **fila** el servicio que llega al view model es el **Nulo** (`ShowConfirmation => true`) y «Eliminar» borra **de verdad y sin diálogo** (medido 11 → 10); por la **tarjeta** llega el **del host**, cuyo `ShowConfirmation` es **síncrono** y devuelve «no» desde el hilo de UI, así que «Eliminar» **no borra** (medido 11 → 11) y tampoco muestra nada. Es la **frontera síncrona** del contrato de diálogos del núcleo; arreglarlo pide confirmación asíncrona en el SDK o comandos asíncronos en el gestor: **una rebanada, no un parche**.

### 📌 Censo definitivo de superficies de usuario del escritorio
**Portadas y probadas**: barra + cajón (**31 entradas** censadas, pendientes **VACÍA**); catálogo de diálogos **10 de 11 servidas**; ajustes (**6 secciones**); paneles de nodo (inspector, editor de texto, catálogo de variables y **el gestor de presets**); **4 acciones de fila** servidas; lienzo (tarjetas con su conmutador, sockets, cables, zoom, spotlight y atajos); y las siete ventanas del host. **No portadas, con razón**: el **gestor de CONTRASEÑAS** (declarado: una ventana del plugin con el toolkit que este host no tiene; **es la única superficie de usuario que queda sin portar**), el **menú emergente de variables** (el «{x}» abre el catálogo completo), **`WorkflowSettings`** como diálogo (sería una **segunda copia** de los ajustes del host) y `IPopupMenuService`/`IColorPickerService` (**declarado, no pendiente**). **Fuera de lo pedido queda SÓLO el empaquetado y la entrega** (fases **5.4-5.6** del plan de la rebanada 5, con su tamaño escrito en `docs/uno_slice5_plan.md` §11).

### 📄 Evidencia
[`docs/qa/qa_presets_host_263.md`](file:///docs/qa/qa_presets_host_263.md) + `docs/qa/qa-manual-263/` (capturas `80_…`-`99_…`, `presets-session.json`) + el driver `docs/qa/qa_presets_uia.py` + `selfcheck-dialogs-report.txt` / `selfcheck-report.txt`.

---

## [2026-09-28] - Hito 262: El Editor de URLs por Modelo de IA (El Punto de Entrada que Faltaba y Dónde Queda Escrito)

### 🎯 Objetivos y Alcance
El hito 261 dejó una frontera **declarada**: la pestaña de **Modelos de IA** existía, pero **no ofrecía la edición de URLs por modelo desde la fila** —la acción con la que el escritorio abre `AiModelUrlsConfig`—, así que su clave no podía servirse («servirlo sería una ventana que nadie puede abrir»). Este tramo **le da el punto de entrada y sirve la ventana** sobre el view model portable que ya existía, y deja escrito **dónde queda el cambio**: en el almacén del gestor del núcleo, porque lo escribe el propio view model portable, no la vista.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Controls/SettingsPanel.xaml` | La **acción de URLs** de la fila (`Tag="urls"`, ancla `SettingsAiModelUrlsButton`) en el mismo puesto que en la fila del escritorio: entre descargar y borrar. |
| `Controls/SettingsPanel.xaml.cs` | Su **rama propia**: ejecuta la orden **canónica** del gestor (`ConfigureUrlsCommand`). Sin ella, el `default` la sustituía y pulsar «URLs» **descargaba el modelo**. |
| `Controls/AiModelUrlsConfigBody.xaml(.cs)` | La vista del `AiModelUrlsConfigViewModel` portable: caja en **dos sentidos al teclear**, recuento, distintivo de estado, probar, restablecer y los resultados de la prueba. |
| `Platform/UnoWindowService.cs` | `AiModelUrlsConfig` **servida** con su vista y su arm en el catálogo; fuera de `DeclaredPendingDialogs`: el censo queda en **10 servidas + 1 declarada**. |
| `Resources/Strings*.resx` | Las **10 claves** `AiModelUrls_*` del escritorio copiadas en EN+ES, más una del host (`Uno_AiModelUrls_RequiredWarning`, la frase del propio view model). |

### 🛡️ Guardia y mutaciones
La prueba nueva (`TheModelUrlAction_ShouldOpenTheServedEditor_AndWriteWhereTheDesktopWrites`) ata **las tres mitades**: la fila dibuja exactamente `download`/`delete`/`urls`; la rama de la acción ejecuta `ConfigureUrlsCommand` —**no** la descarga del `default`—; la clave está **servida** con su arm y **sin** seguir declarada; y el cuerpo es una vista del view model portable que **no** escribe la configuración por su cuenta (no contiene `SetCustomUrls`; dos sitios escribiendo lo mismo serían dos verdades). La mutación nueva (`accion-de-urls-que-descarga-el-modelo`) quita esa rama y **MUERDE** (32,5 s): un botón que hace otra cosa es peor que uno que no hace nada. **Una mutación anterior se retiró** (`accion-de-urls-por-modelo-sin-declarar`) porque vigilaba que la fila **no** dibujara la acción, y este tramo la dibuja: dejarla habría sido un mutante que ya no mide nada. Declaradas: **80**. Y **una guardia del repositorio salió roja al cambiar el producto** —`UnoControlBarParityGuardTests` exigía que esta clave siguiera declarada— y se actualizó: es la señal de que el censo es producto vigilado.

### 🟢 Ejercido con la aplicación abierta (sesión 272), 24 de 24 pasos
Driver externo por UIA (`qa_urls_uia.py`). Medido: el catálogo con **24 filas** y **MobileNetV2 ImageNet** la primera → se pulsa la **acción de URLs de esa fila** → el editor aparece con **7 anclas** y hablando del **mismo modelo**, pixel `#FCF8F8` → **`#3C3C3C`** → teclear **2 URLs** lleva el recuento del view model de **`1 URL(s)` a `2 URL(s)`** → Guardar cierra el modal (pixel de vuelta a `#585454`, el de la superficie abierta detrás) → **al reabrir, la caja trae las dos URLs Y el distintivo pasa de `📦 Oficial / Predeterminado` a `🔧 Personalizado`**: el cambio quedó escrito donde lo escribe el escritorio → se restaura (`🔧` → `📦`) → preferencias md5 **byte-idénticas** (`d5f199a068113d8a7e16ad6ee6f726b3`).

### 🔍 Tres defectos que encontró la medición (uno del producto, dos del instrumento)
1. **Del producto**: la caja del editor enlazaba `Text` sin `UpdateSourceTrigger`, así que en WinUI escribía **al perder el foco** y el recuento se quedaba con el valor viejo mientras el usuario teclea (el escritorio lo actualiza al teclear). Corregido en el enlace.
2. **Del instrumento**: `window_text()` de una **fila enlazada** devuelve el nombre del **tipo del view model** (`FileFlow.App.ViewModels.AiModelItemViewModel`), no lo que se ve; el nombre vive en el `AutomationProperties.Name` del panel de la fila.
3. **Del instrumento**: el píxel tras cerrar el modal **no vuelve al del lienzo** sino al de la **superficie de ajustes que sigue abierta detrás** (`#585454`); comparar contra la línea base medía mal el producto.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` / `--selfcheck-controlbar` | **EXIT 0 · 83 `[OK]`** / **EXIT 0 · 42 `[OK]`** · VERIFICADO |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 18 `[OK]`** · VERIFICADO (antes 12) |
| Guardias | **13 + 13 + 9 + 5 = 40** de 40 |
| Mutaciones | **6 de 6 MUERDEN** (1 nueva + 5 corroboradas) · **80 declaradas** |
| Suite completa | **1917 superadas + 1 omitida de 1918, 0 errores** (2 m 24 s) |
| Rojo intermitente | **2 corridas con un rojo distinto cada una** (`EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` y `SystemPerformanceMonitorTests.TheHeartbeat_ShouldPublishAPlausibleSample`), **los dos verdes en aislamiento (1 de 1)** → **ruido de carga**, no regresión; la corrida final, verde |
| Sesión con la app abierta | **24 de 24 pasos** |

### 📌 Fronteras declaradas
1. **El aviso de «URL requerida» no sale como segundo `ContentDialog`** (WinUI sólo admite uno y el editor ya está abierto): la petición del view model queda escrita en la consola y **el host la repite dentro del editor**, sin cerrar el modal sobre algo rechazado.
2. **`WorkflowSettings` sigue siendo la única clave declarada**: su superficie tiene puerta en la barra y el cajón.
3. **Hallazgo del escritorio, anotado y NO tocado**: su `AiModelUrlsConfigDialog` enlaza `{Binding SaveCommand}`, que su view model no expone; ese botón no guarda. El host no hereda el defecto (llama al mismo `Save()`) y el escritorio no se tocó.

---

## [2026-09-28] - Hito 261: El Diseñador de Datasets y las Dos Pestañas de Ajustes que Faltaban (El Contrato de Superficie del SDK)

### 🎯 Objetivos y Alcance
El hito 260 dejó **8 claves servidas + 2 declaradas**, y de esas dos la única **entrada de menú** sin superficie era el **Diseñador de Datasets**. Su ventana la monta el **propio plugin** con el toolkit del escritorio —un host WinUI no puede montar una ventana ajena—, así que el tramo anterior la había dejado declarada «con su razón exacta». Este tramo la **cruza sin reimplementar el diseñador**: un **contrato NUEVO del SDK** deja que el **nodo** declare qué diálogo quiere y qué contiene, y el host pinta esa clave con **su propia vista sobre el view model PORTABLE del plugin**. Además se sirven las **dos pestañas de ajustes** que faltaban —**Modelos de IA** y **Actualizaciones**— sobre sus secciones del núcleo, y `DeclaredPendingEntries` queda **VACÍA**: ya no hay ninguna orden de menú del escritorio sin dibujar, declarar o cumplir por el host.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs` | **Contrato nuevo del SDK**: el nodo dice **qué** diálogo quiere (`DialogKey`, la misma clave para todos los hosts) y **qué** contiene (`Payload`, su view model portable). La identidad del diálogo deja de decidirla el host. |
| `Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs` | Implementa el contrato: declara `DialogKeys.DataSetDesigner` y entrega su `SyntheticDataSetDesignerViewModel`. |
| `Controls/DataSetDesignerBody.xaml(.cs)` | La vista del host sobre ese view model: buscador, catálogo, las tres pestañas (árbol / DSL / JSON) y las órdenes de añadir y quitar. **Cero lógica de producto**: sus órdenes **son los comandos del VM del plugin**. |
| `Controls/SettingsPanel.xaml(.cs)` | Las dos pestañas nuevas —**Modelos de IA** (catálogo, carpeta, estado, descargar / borrar por fila) y **Actualizaciones** (versión, formato, canales, comprobación automática)— sobre sus secciones portables. La superficie pasa a **seis secciones**. |
| `Controls/MainMenuDrawer.xaml(.cs)` | La entrada **Diseñador de Datasets**, cumplida por el evento propio del cajón; el cajón pasa de 14 a **15 entradas** ancladas. |
| `Controls/ControlBar.xaml.cs` | `DeclaredPendingEntries` **vacía** (la tabla se conserva con su guardia para que la próxima orden sin destino tenga dónde declararse) y `OpenSyntheticDataSetDesignerCommand` añadida a `HostOwnedOrders` con su mecanismo. |
| `Platform/UnoWindowService.cs` + `IWindowService.cs` | `DataSetDesigner` **servida** con su vista; el censo pasa a **9 servidas + 1 declarada**. La razón de `AiModelUrlsConfig` se **corrige** (§7 del QA). |

### 📐 La paridad, escrita
**31 entradas censadas** (16 de la barra + 15 del cajón); **9 claves de catálogo servidas** con su vista y **1 declarada con su razón** (`WorkflowSettings`: abrirla por aquí sería una SEGUNDA copia de la superficie de ajustes del host); **cinco órdenes del escritorio** cumplidas por el canal propio (`HostOwnedOrders`); **cero entradas declaradas pendientes** y **cero atajos sin enrutar**.

### 🛡️ Guardia y mutaciones
La guardia sube a **13 + 13 + 9 + 5 casos** entre los cuatro ficheros: el censo de diálogos del servicio, la tabla `HostOwnedOrders` para el diseñador y el contrato del nodo (`TheDataSetDesigner_ShouldBeDeclaredByTheNode_AndServedByTheHost`), más la sección nueva de ajustes. Y **un caso nace de lo que este tramo encontró a ojo**: `TheAiModelRowActions_ShouldMatchWhatTheDialogCensusDeclares` ata las acciones dibujadas en la fila de modelos a lo que el censo de diálogos declara (ver «Una razón que había quedado falsa»). **Seis mutaciones muerden**: cinco nuevas (`nodo-que-declara-su-superficie-sin-clave`, `disenador-de-datasets-fuera-del-catalogo`, `vista-del-disenador-con-su-propio-modelo`, `seccion-que-pierde-el-panel-que-conmutaba`, `accion-de-urls-por-modelo-sin-declarar`) y `menu-que-no-declara-lo-que-falta` **reapuntada** a la tabla que cambió de estado. **Dos guardias del repositorio salieron rojas al cambiar el producto** (la del host libre de Avalonia —una razón declarada nombraba el ensamblado— y la del inventario de trabajo aplazado —la espera del arranque, registrada `RealTime` con su motivo—): es la señal de que las tablas de declaración son producto vigilado, no prosa.

### 🟢 Ejercido con la aplicación abierta (sesión 271), 39 de 39 pasos
Driver externo por UIA (`qa_windows2_uia.py`) + vigilante de píxeles. Las **dos ventanas que el pase anterior no había ejercido** y las dos nuevas: **Explorador VFS** (botón de la barra, chip `📁 | VFS (4)`) → **5 anclas**, **4 filas** con nombre real (`Sembrado 01..04.mkv`), pixel `#B0ACAC`, y al cerrar `#FCF8F8` y **0 filas**; **aviso de actualización** (distintivo `🚀 | v9.9.9`) → **6 anclas**, versión actual `1.0.0-beta+build.6759` contra `9.9.9`, pixel `#B0ACAC`, y al cerrar la superficie se va **pero el distintivo sigue puesto**; **Diseñador de Datasets** (entrada del cajón) → **8 anclas** y **7 filas** de dataset, pixel `#B0ACAC`; **Ajustes** → **6 secciones**, pestaña Modelos de IA con **24 filas** y carpeta `…\FileFlow\models`, pestaña Actualizaciones con su versión. `preferencias.md5` **idénticas** (`d5f199a068113d8a7e16ad6ee6f726b3`).

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 42 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 37) |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 12 `[OK]`** · VERIFICADO (antes 9) |
| Guardias | **13 + 13 + 9 + 5** = **40 casos** |
| Mutaciones | **6 de 6 MUERDEN** (5 nuevas + 1 reapuntada) · **80 declaradas** |
| Suite completa | **1917 superadas + 1 omitida de 1918, 0 errores** (2 m 26 s) |
| Sesión con la app abierta | **39 de 39 pasos** · 3 tarjetas |
| Guardias del repositorio que salieron rojas | **2 y las dos se arreglaron** |

### 📌 Fronteras declaradas
1. **El Diseñador de Datasets se sirve por el contrato del SDK, no por el comando canónico**: el comando del escritorio abre la ventana que monta el plugin con el toolkit del escritorio. El cajón lo cumple con su evento propio, la ventana pide al nodo la superficie declarada y el **catálogo de diálogos del host** la sirve con SU vista sobre ese mismo view model.
2. **`WorkflowSettings` (la clave del catálogo) sigue declarada y no servida**: abrirla por `IWindowService` sería una SEGUNDA copia de la superficie de ajustes que el host ya tiene en la barra y el cajón.
3. **`AiModelUrlsConfig` sigue declarada**, con razón corregida **y ahora vigilada**: la pestaña de modelos de IA del host lista el catálogo y gestiona descargas, pero **no ofrece la edición de URLs por modelo desde la fila**, que es la acción con la que el escritorio abre ese diálogo. Servirlo sin punto de entrada sería una ventana que nadie puede abrir. (La razón anterior —«esa pestaña no existe aquí»— había quedado falsa al añadirla este tramo; una razón obsoleta miente igual que un no-op mudo. **La encontró el ojo, así que las dos mitades quedan atadas por una prueba y por una mutación que muerde**: si alguien dibuja esa acción, la declaración deja de ser cierta y el caso cae nombrando la clave.)
4. **Hallazgo del escritorio, anotado y NO tocado**: el botón Guardar del `AiModelUrlsConfigDialog` enlaza `{Binding SaveCommand}`, que el view model **no expone** (tiene `Save()` sin `[RelayCommand]`). Ese diálogo no puede guardar en el escritorio. Antes de portarlo «con paridad» hay que decidir cuál es el comportamiento correcto.
5. **El `FileInfoText` del diseñador no se dibuja**: el escritorio lo rellena desde su catálogo de modelos y aquí no hay fuente; se dibujan los cuatro campos que el `VirtualFileEntry` del núcleo sí expone.

---

## [2026-09-28] - Hito 260: Las Ventanas que Faltaban del Menú del Host Uno (Las Cuatro Puertas del Catálogo de Diálogos)

### 🎯 Objetivos y Alcance
El hito 259 cerró la mitad del menú y dejó **cinco entradas declaradas**; cuatro de ellas abrían una **ventana** del escritorio que este host no tenía. Este tramo las **sirve por el catálogo de diálogos** (`DialogKeys`) sobre los **view models PORTABLES del núcleo** —cero lógica de producto en la vista—: el **Estudio de Temas**, las **Métricas**, el **Explorador Virtual (VFS)** y el **aviso de actualización**. El censo del servicio pasa de **3 servidas + 6 declaradas** a **7 + 2**, y la única que queda declarada —el **Diseñador de Datasets**— lleva ahora su razón exacta: su ventana la monta el propio plugin con su toolkit, y servirla pide una vista del host sobre un view model que vive dentro de su ensamblado.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `Controls/ThemeCustomizerBody.xaml(.cs)` | El **Estudio de Temas**: catálogo del núcleo, editor por secciones generado desde `ThemeSettingCatalog` (9 secciones, **34 ajustes editables**) y sus tres plantillas (color con su muestra, número con su rango y su paso, elección) repartidas por un **selector por TIPO de fila** —añadir un ajuste al catálogo no toca la vista—. Declara en `DeclaredPendingParts` lo que no sirve (Eliminar / Importar / Exportar, que piden el contrato SÍNCRONO de diálogos, y la vista previa en vivo). |
| `Controls/MetricsDashboardBody.xaml(.cs)` | El **panel de Métricas**: las cuatro tarjetas y las **siete columnas** del escritorio, leídas del `WorkflowMetricsDashboardViewModel` —él formatea, la vista pinta—, con una cabecera y una plantilla de fila (WinUI no trae `DataGrid`). |
| `Controls/VirtualFileSystemExplorerBody.xaml(.cs)` | El **Explorador VFS**: el host construye el `VirtualFileSystemExplorerViewModel` con el almacén que llega como carga útil (como el `AvaloniaWindowService` del escritorio) y la vista enlaza buscador, lista, selección y metadatos. Dibuja los cuatro campos que el `VirtualFileEntry` SÍ tiene. |
| `Controls/UpdateDialogBody.xaml(.cs)` | El **aviso de actualización**: versiones, formato del paquete, novedades y progreso del `UpdateDialogViewModel`; sus tres órdenes son sus comandos y el cierre lo pide el propio view model por `RequestClose` (el servicio retira el modal). |
| `Platform/UnoWindowService.cs` | Las cuatro claves **servidas** con su vista y las dos que quedan **declaradas con su razón**; el cuerpo como superficie del host (misma decisión que «Acerca de») con su **clave de catálogo como ancla** (`ActiveWindowKey`) y un `CloseActiveWindow()` que usan los pies de las ventanas. Una carga útil que no es la esperada se **declina con su motivo**, nunca en silencio. |
| `Controls/ControlBar.xaml.cs` | Las dos entradas con **estado de contexto** de la barra: el chip **VFS (`HasVirtualFiles`)** con su recuento y el **distintivo de actualización (`HasPendingUpdate`)** con la versión nueva. La tabla nueva `ServedWindowEntries` deja escrito dónde vive cada una de las cuatro. La tabla de **declaradas baja a una fila**. |
| `Controls/MainMenuDrawer.xaml(.cs)` | Las tres entradas del cajón (Estudio de Temas, Métricas y VFS) ejecutando las **órdenes CANÓNICAS** del núcleo; el cajón pasa de 11 a **14 entradas** ancladas. |
| `App.xaml.cs` + `MainWindow.xaml.cs` | La **comprobación de actualizaciones del arranque**, la misma del escritorio (en segundo plano, sin forzar, respetando la versión ignorada) y **saltada entera en los modos de sondeo**; entrega la novedad a la ventana, que es quien enciende el distintivo. Sin esa mitad, el aviso que el host ya sirve no lo pediría nadie. |
| `Resources/Strings*.resx` | **192 claves copiadas** del diccionario del escritorio en los dos idiomas (ThemeStudio, Metrics, VfsExplorer, Update, Drawer_*): los view models portables piden sus textos por clave y el host los resuelve con los suyos, así que el editor y las ventanas salen en el idioma elegido. |

### 📐 La paridad, escrita
**28 entradas censadas** (16 de la barra + 14 del cajón, con las 4 de ventana); **4 ventanas servidas** (2 nuevas claves de catálogo además de las 2 del 258 y la del 259) y **2 declaradas con su razón**; **una entrada declarada pendiente** (el diseñador de datasets del plugin, con la frontera del toolkit escrita); **4 claves de catálogo** con su vista en `ImplementedDialogs`.

### 🛡️ Guardia y mutaciones
`UnoControlBarParityGuardTests` pasa a **12 casos**: `TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue` (la tabla `ServedWindowEntries` + cada orden DIBUJADA fuera de las tablas + toda clave del SDK con destino), `TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt` y `TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes` (la **llamada**, no sólo la definición). **Siete mutaciones muerden**: cuatro nuevas (`ventana-servida-que-no-esta-en-el-catalogo`, `entrada-de-ventana-que-no-ejecuta-su-orden`, `aviso-de-actualizacion-que-nadie-enciende`, `estudio-de-temas-que-esconde-lo-que-no-sirve`), `menu-que-no-declara-lo-que-falta` **reapuntada** a la fila que queda, y dos de los hitos 257/258 re-verificadas. **Una debilidad de la guardia nueva la encontró la mutación**: buscar las órdenes en el texto de las vistas se conformaba con la propia tabla que las nombra, así que vaciar un manejador no caía; se añadió `WithoutDeclarationTables` y la mutación pasó de sobrevivir a morder.

### 🟢 Ejercido con la aplicación abierta (sesión 270), 25 de 25 pasos
Driver externo por UIA (`qa_menu3_uia.py`). Medido: cajón con **14/14 entradas** y su velo (`#FCF8F8` → `#585454`); **«Estudio de Temas»** → **6/6 anclas**, **9 temas** leídos por el canal externo («🌙 Oscuro Fluent», «☀️ Claro Minimalista»…), título del modal localizado, pixel **`#B0ACAC` 46,7 %** y el cajón recogido al elegir; **«Métricas»** → **4/4 anclas**, **3 filas** (una por nodo) y el pie «**3 nodos analizados. 0 cuello(s) de botella.**», pixel **`#B0ACAC` 49,4 %**; cerrar cada una devuelve el pixel a **`#FCF8F8` 77,8 %** y deja el árbol sin sus anclas; **preferencias md5 idénticas** (`d5f199a068113d8a7e16ad6ee6f726b3`).

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 37 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 25) |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 9 `[OK]`** |
| Guardia del menú | **12 de 12** (antes 9) |
| Mutaciones | **7 de 7 MUERDEN** (4 nuevas + 1 reapuntada + 2 re-verificadas) · **75 declaradas** |
| Suite completa | **1915 superadas + 1 omitida de 1916, 0 errores** (2 m 29 s en la corrida del tramo; **re-verificada al cierre, 2 m 26 s**) |
| Sesión con la app abierta | **25 de 25 pasos** |
| Guardias del repositorio que salieron rojas | **2 y las dos se arreglaron en el producto o en su registro** (la del host libre de Avalonia —una razón declarada nombraba el ensamblado— y la del inventario de trabajo aplazado —la espera nueva del arranque, registrada `RealTime` con su motivo—) |

### 📌 Fronteras declaradas
1. **Las cuatro ventanas son superficies modales dentro de la ventana del host**, no ventanas nuevas: el mismo criterio de «Acerca de» (hito 258).
2. **El Estudio de temas no dibuja Eliminar / Importar / Exportar** ni la vista previa en vivo: los tres primeros dependen del contrato SÍNCRONO de diálogos (desde el hilo de UI devuelve «no»/nulo) y la vista previa necesitaría una copia propia de tokens. Todo declarado en `DeclaredPendingParts`, con su razón.
3. **El canal del VFS con el almacén real de una ejecución** se mide con su estado de contexto (el chip de la barra) y con un almacén construido por la sonda por el MISMO camino del servicio; no se ejecutó un flujo que produjera archivos virtuales.
4. **El aviso de actualización se ejerció con una novedad sintética** (`v9.9.9`); en la aplicación normal sólo aparece con una release nueva de verdad.
5. **Hallazgo del escritorio, anotado y no tocado**: su rejilla del VFS declara «Tamaño» y «Modificado» enlazando a propiedades que no existen en el `VirtualFileEntry` del núcleo —columnas vacías en silencio—.

---

## [2026-09-28] - Hito 259: Las Entradas y los Atajos que Faltaban del Menú del Host Uno (El Contrato Síncrono, Cruzado por el Canal Asíncrono del Host)

### 🎯 Objetivos y Alcance
El hito 257 portó la barra de control y su cajón, y dejó **once entradas y seis atajos declarados pendientes** —en parte esperando al servicio de ventanas del 258—. Este tramo cierra esa mitad sin rehacer nada de lo portado: **tres órdenes de flujo** (Nuevo / Cargar / Guardar) cumplidas por el canal propio del host, **tres entradas de ayuda** (Manual / Ejemplos / Acerca de) por sus órdenes canónicas —con «Acerca de» ya como superficie real— y los **seis atajos** enrutados.

### 🔴 La frontera que era el bloqueo real (y cómo se cruza)
Las tres órdenes de flujo no estaban pendientes por falta de tiempo: su comando del núcleo pide un diálogo **SÍNCRONO**, y desde el hilo de UI este host devuelve `null` en el picker y `false` en la confirmación (medido y declarado desde el 240 en `UnoFileDialogService` / `UnoDialogService`). Dibujar la entrada y ejecutar el comando canónico habría sido **un botón que no hace nada**, sin crash y sin mensaje. El cruce no reimplementa el flujo en el host: **separa el diálogo de la operación en el view model portable**, que es la regla que el 254 ya usó con «cargar un flujo» —`ControlBarViewModel.CreateNewWorkflow()` y `SaveWorkflowToFileAsync(path)`, simétricos de `LoadWorkflowFromFileAsync`— y el host aporta lo que sí sabe hacer: `UnoDialogService.ShowConfirmationAsync` (la confirmación que se puede esperar sin bloquear el hilo de UI) y los pickers asíncronos de WinRT.

### 🧱 Lo construido
| Pieza | Qué es |
| :--- | :--- |
| `MainMenuDrawer.xaml(.cs)` | **Dos secciones nuevas**, en el orden del escritorio: **GESTIÓN DE FLUJOS** (Nuevo / Cargar / Guardar) y **AYUDA Y RECURSOS** (Manual / Ejemplos / Acerca de). El cajón pasa de 5 a **11 entradas** ancladas; las de flujo declaran *qué se ha pedido* por evento y las de ayuda ejecutan la orden canónica. |
| `MainWindow.xaml.cs` | Las tres manos de flujo (confirmación y pickers asíncronos + los métodos portables) y el **enrutado del teclado**: lo que el lienzo no reclama llega a la tabla de atajos del menú. |
| `Controls/ControlBar.xaml.cs` | La tabla **`RoutedShortcuts`** (6 filas: gesto, tecla, modificadores, orden y vía) que **es la que enruta** —el manejador la recorre; no hay un `switch` paralelo que se pueda desincronizar— y las tablas del censo actualizadas. |
| `Controls/AboutDialogBody.xaml(.cs)` | La ventana **«Acerca de»** del host: los mismos rótulos del escritorio (`Uno_About_*`, copiados), la versión de la misma fuente que el pie del cajón y las insignias de lo que este host es (`.NET 10.0`, `Uno Platform · WinUI 3`, `DAG Flow Engine`). |
| `Platform/UnoWindowService.cs` | `ShowWindow(DialogKeys.About)` servido y anclado (`AboutDialog`): el censo pasa de 2 servidas + 7 declaradas a **3 + 6**. |
| `App.Core/ViewModels/ControlBarViewModel.cs` | Los dos métodos portables sin diálogo del apartado anterior. |

Los textos nuevos (`Uno_Drawer_FlowManagement`, `Uno_Drawer_New/Load/SaveWorkflow`, `Uno_Drawer_HelpResources`, `Uno_Drawer_UserManual(+ToolTip)`, `Uno_Drawer_ExampleFlows(+ToolTip)`, `Uno_Drawer_About(+ToolTip)`, `Uno_About_*`) se **copian** del diccionario del escritorio, clave por clave, en los dos idiomas.

### 📐 La paridad, escrita
**25 entradas censadas** (14 de la barra + 11 del cajón); **4 órdenes cumplidas por el host** (`OpenWorkflowSettingsCommand` y las tres de flujo); **6 atajos enrutados** (F5 / F10 / Shift+F5 al comando del ciclo del núcleo; Ctrl+N / Ctrl+O / Ctrl+S por el canal del host) con `DeclaredUnroutedShortcuts` **vacía**; y **5 entradas pendientes con su razón**: Estudio de temas, métricas, VFS, diseñador de dataset y aviso de actualización (el host no comprueba actualizaciones).

### 🛡️ Guardia y mutaciones
`UnoControlBarParityGuardTests` pasa a **9 casos**: el nuevo `TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne` exige las dos mitades —las APIs asíncronas y los métodos portables, y **no** los `…Command.Execute` del núcleo ni el producto reimplementado en la vista— y el caso de atajos lee ahora **las dos tablas** (enrutados + declarados), con la exigencia de que ninguna contradiga a la otra. **🧬 Mutación nueva (71.ª): `flujo-que-se-cumple-por-el-picker-sincrono` → MUERDE** (35 s), y **dos mutaciones del 257 actualizadas al producto nuevo** (`menu-que-no-declara-lo-que-falta`, `menu-que-no-declara-un-atajo` —esta última borra ahora una fila de la tabla que enruta—) **también muerden**. La guardia de declaraciones **falló al cambiar el producto** y fue el aviso que hacía falta: dos mutantes habrían quedado mintiendo en silencio. COVERAGE → **71 declaraciones**.

### 🟢 Ejercido con la aplicación abierta (sesión 269), 27 de 27 pasos
Driver externo por UIA + **teclado FÍSICO** (`keybd_event`, el mismo canal que midió la sesión 268) con el **vigilante** midiendo (79 fotogramas, 15 cambios de escena). Medido: base `tarjetas=3` y **0 anclas del cajón** → «Menú» expone **11/11** entradas y el velo se ve en el pixel (`#FCF8F8` → **`#585454`**) → **«Acerca de»** abre la superficie del host (anclas `AboutDialog` / `AboutVersionText` / `AboutDescriptionText`; la versión leída por UIA: **`v1.0.0-beta+build.6664 · net10.0 · Uno Platform (WinUI 3)`**; el modal en el pixel: **`#B0ACAC` 67,1 %**) y al cerrarla el pixel vuelve a la base → **«Nuevo Flujo»** pide confirmación (**«¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.»**, pixel `#B0ACAC` 77,5 %) y **cancelar deja las mismas 3 tarjetas** → **Ctrl+N** por tecla física abre **la misma confirmación** y deja **el mismo pixel** → **F5** y **F10** por teclado físico quedan en el **rastro**: `menu atajo=F5 orden=ContinueWorkflowCommand` y `menu atajo=F10 orden=StepNextCommand`. Cierre: escena y tarjetas como al entrar y **preferencias del usuario byte-idénticas** (md5 `d5f199a068113d8a7e16ad6ee6f726b3`).

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 25 `[OK]` · 0 `[FALLO]`** · VERIFICADO (antes 14) |
| `--selfcheck-dialogs` / `--selfcheck-settings` | **EXIT 0 · 24 `[OK]`** / **EXIT 0 · 9 `[OK]`** |
| Guardia del menú | **9 de 9** |
| Mutaciones | **3 de 3 MUERDEN** (la nueva + las dos del 257 actualizadas) |
| Suite completa | **1912 superadas + 1 omitida de 1913, 0 errores** (2 m 26 s) |
| Sesión con la app abierta | **27 de 27 pasos** |

### 📌 Fronteras declaradas
1. **Cinco entradas siguen pendientes** con su razón escrita (Estudio de temas, métricas, VFS, diseñador de dataset y aviso de actualización).
2. **«Acerca de» es modal aquí y ventana en el escritorio**: el host sirve la misma información dentro de su única ventana. Diferencia declarada.
3. **Las órdenes del núcleo siguen pidiendo el contrato síncrono**: el host las cumple por su canal, no cambiando el contrato; otro host tendrá la misma frontera y las mismas dos piezas portables para cruzarla.
4. **Ctrl+O y Ctrl+S no se pulsaron con tecla física** (abren el picker del sistema, que se lleva la sesión de UIA): su camino lo ata la guardia y su mitad sin diálogo se ejerció por la sonda (guardar y cargar sobre un fichero temporal, medido).
5. **La confirmación del host usa botones `OK`/`Cancel`**, como el adaptador de diálogos que ya existía: el escritorio no tiene esa confirmación con otros textos que copiar.

---

## [2026-09-28] - Hito 258: Los Paneles de Nodo del Host Uno: los Diálogos de Parámetro y el Selector de Variables, Sobre los View Models del Núcleo

### 🎯 Objetivos y Alcance
El host Uno tenía lienzo, paneles, atajos, ajustes y barra de control, pero **los paneles que cada nodo tiene dentro** —el editor de texto y prompts del parámetro largo y el selector de variables— seguían cayendo al **Nulo declarado**: el botón «✎» y el botón «{x}» existían y **no hacían nada** (sin crash y sin error en pantalla, el usuario pulsaba y no pasaba nada). Este tramo escribe el `IWindowService` del host, las dos vistas sobre los **view models portables del núcleo** y el **anclaje** que hace que las filas del inspector los alcancen, y **no toca ninguna otra superficie**.

### 🧱 Lo construido (tres piezas en el host, cero líneas en `FileFlow.App`)
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Platform/UnoWindowService.cs` | El `IWindowService` real: `ShowDialogAsync` por `DialogKeys` con `ContentDialog` y `DialogResultPayload`, `MainWindowOwner` real, y las dos tablas del censo — `ImplementedDialogs` (**2 servidas**) y `DeclaredPendingDialogs` (**7 declaradas con su razón**)—. Lo que el host no sirve **no se cancela mudo**: `Decline` escribe la clave y el motivo en `DeclinedDialogs` y en la consola de la aplicación, porque un «cancelado» sin traza se lee como un error del usuario. |
| `Controls/TextEditorDialogBody.xaml(.cs)` y `Controls/VariablePickerDialogBody.xaml(.cs)` | Las vistas de los VMs **portables**: el editor con su caja `TwoWay`, sus botones de insertar variable y limpiar y su panel lateral del **propio VM** (WinUI no admite dos `ContentDialog` a la vez), insertando por `vm.InsertTokenAt(caret, token)` y devolviendo `vm.SaveResult()`; el catálogo con lista de selección `TwoWay`, buscador que filtra en caliente, recuento, detalle del token y devolución de `vm.SelectedToken`. |
| `Controls/NodeInspectorPanel.xaml.cs` | Las **acciones de fila**: `HostRowActions` (**3 dibujadas**: explorar ruta «…», editor «✎», catálogo «{x}», con las anclas `ParamBrowse_` / `ParamEditor_` / `ParamVariable_`) y `DeclaredPendingRowActions` (**3 declaradas**). |

`App.xaml.cs` registra `services.AddSingleton<IWindowService, UnoWindowService>()` **y ancla** `ServiceHolders.WindowService` —de ahí lo leen los `NodeParameterViewModel` que el inspector construye **sin recibir servicios por constructor**: sin ese anclaje el servicio existe en el contenedor y las filas siguen en el Nulo—. Los textos de los dos diálogos son claves `Uno_Dialog_*` **copiadas del diccionario del escritorio**, clave por clave y en los dos idiomas.

### 🔴 El defecto REAL que destapó el driver (y que se arregló)
Las cajas de texto de las filas del inspector **sólo escribían en un sentido**: del campo al parámetro. Cuando el valor lo escribía **el diálogo** —insertar `{FileName}` desde el catálogo—, el parámetro del nodo cambiaba pero **el campo seguía mostrando el texto viejo**: el usuario habría visto su inserción desaparecer de la pantalla. **Arreglo**: `WireBoxToParameter(TextBox, NodeParameterViewModel)` (ida + escucha de `Value` → `box.Text = value;`) y una lista `_rowValueSubscriptions` que se suelta en `RebuildParameters()` (sin ella, reconstruir el inspector dejaría escuchas huérfanas), aplicado a las **tres** cajas (estándar, multilínea y ruta con explorar).

### 📐 La paridad, escrita (y lo que no llega, declarado)
El **censo de diálogos** reparte las **9** claves de `DialogKeys` entre **2 servidas** (`TextEditor`, `VariablePicker`) y **7 declaradas** con su razón (`UpdateDialog`, `WorkflowSettings`, `VirtualFileSystemExplorer`, `About`, `WorkflowMetricsDashboard`, `ThemeCustomizer`, `AiModelUrlsConfig`); la guardia lo compara **contra las constantes del SDK**, así que una clave nueva sin destino cae en la tabla. Dos declaraciones que son decisión, no olvido: **`WorkflowSettings`** no se sirve por esta vía porque su superficie (hito 255) ya tiene punto de entrada en la barra y el cajón y una segunda puerta sería **una segunda copia**; y el **«{x}»** del host abre **directo el catálogo completo** en vez del **menú emergente** del escritorio (el host no tiene `IPopupMenuService` y el catálogo **es** la primera entrada de aquel menú).

### 🛡️ Guardia y mutaciones
`UnoNodeDialogsGuardTests` (**9 casos**): el censo contra `DialogKeys`; cada pendiente **contestada con su razón** y no con un cancelar mudo; las órdenes de fila del escritorio con destino; las acciones dibujadas **en las mismas filas** que el escritorio (leído de `NodeParameterTemplates.axaml`); el **atado bidireccional** de las cajas; los diálogos como **vistas de los VMs portables** (`vm.SaveResult()`, `vm.InsertTokenAt`); los **20 textos** copiados del escritorio en los dos idiomas; el diccionario del host sin claves huérfanas; y la sonda en **modo propio**. **🧬 Cuatro mutaciones (68.ª-71.ª): `panel-de-nodo-sin-su-servicio-de-ventanas`, `fila-de-variables-que-abre-el-menu-que-no-esta-portado`, `editor-que-no-devuelve-el-texto-confirmado` y `campo-que-no-muestra-lo-que-el-dialogo-escribio` → las cuatro MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes; 34,8 s / 30 s / 29 s / 28 s). La cuarta **nació sobreviviente**: su primera versión (quitar la escucha) no moría, así que la guardia se endureció hasta exigir el atado completo (la escucha **y** su registro para soltarla) — y entonces mordió. COVERAGE → **70 declaraciones**, 15 de 17 subsistemas, guardias que auditan el repositorio con mutación que las muerda **15 de 44**.

### 🟢 Ejercido con la aplicación abierta (sesión 268), 25 de 25 pasos
Driver externo por UIA (`docs/qa/qa_dialogs_uia.py`) **actuando** sobre los controles reales con el **vigilante** de píxeles midiendo en paralelo. En la escena del ejemplo: base `tarjetas=3` y **0 filas `Param*`** → clic en la tarjeta **`Folder Source`** y aparecen **10 anclas `Param*`** → pulsar **«{x}»** de `ExtensionFilter` **abre el catálogo** y **el modal se ve en el pixel** (centro `#FCF8F8` → **`#B0ACAC` 52,6 %**) → el buscador escribe «Guid» y el recuento pasa de «47 de 47» a **«1 de 47»** y vuelve → elegir **`{FileName}`** llena el detalle y habilita «Insertar Variable» → pulsar y **el campo del nodo pasa de `''` a `'{FileName}'`** (la vuelta del §defecto), devuelto a `''`. Después, nodo real añadido por el cajón (`Registrar Log`): sus filas exponen `ParamBox_CustomMessage`, `ParamEditor_CustomMessage`, `ParamVariable_CustomMessage`, `ParamDropdown_LogLevel` y 3 toggles; pulsar **«✎»** abre el **editor** (`TextEditorBox`) **sembrado con el valor de la fila**; su «Insertar Variable» despliega el catálogo del VM (**13 variables**); escribir `'prompt de la sesion 268 + {FileName}'` y **«Guardar y Aplicar»** deja **ese texto en el parámetro del nodo**; y el nodo añadido se retira con `Supr`. **Preferencias del usuario byte-idénticas** (md5 `d5f199a068113d8a7e16ad6ee6f726b3`): los paneles de nodo no escriben nada del usuario. Nota metodológica medida: el **clic físico inyectado SÍ llega** al contenido de WinAppSDK (la casilla «Modo Prueba» conmuta 1→0→1), a diferencia del clic mediado por UIA.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** — los paneles no rompieron ninguna sonda anterior |
| `--selfcheck-dialogs` (paneles de nodo) | **EXIT 0 · 24 `[OK]` · 0 `[FALLO]`** · «RESULTADO: VERIFICADO» |
| `--selfcheck-settings` / `--selfcheck-controlbar` | **EXIT 0 · 9 `[OK]`** / **EXIT 0 · 14 `[OK]`** |
| Guardia de los paneles de nodo | **9 de 9** superados |
| Mutaciones 68.ª-71.ª | **4 de 4 MUERDEN** |
| Suite completa | **1910 superadas + 1 omitida de 1912** por corrida; el único rojo de cada una fue **un test distinto y pesado** (`EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` una vez, `ExampleFlowsEndToEndTests.EveryExample_ShouldDeliverWhatItPromises` otra), **verde en aislamiento** (1/1 y 4/4): **ruido de carga, no regresión** |
| Sesión con la app abierta | **25 de 25 pasos** |

### 📌 Fronteras declaradas
1. **Las 7 claves de diálogo que el host no sirve** (Actualizador, VFS, Acerca de, Métricas, Estudio de temas, configuración de modelos de IA y los ajustes por esta vía): declaradas con su razón, con traza de lo pedido y atadas por la guardia. Un botón que no puede abrir nada no se dibuja.
2. **El «{x}» abre el catálogo completo, no el menú emergente**: el host no tiene `IPopupMenuService` y el catálogo es la primera entrada de aquel menú.
3. **`WorkflowSettings` no se sirve por `IWindowService`** aunque la superficie exista: su entrada es la barra y el cajón; una segunda puerta sería una segunda copia.
4. **Las ventanas que el host no tiene** (dashboard, VFS, gestor de presets de medios, gestor de contraseñas) siguen pendientes, cada una en su tabla.
5. **Sigue pendiente** de la migración: las **11 entradas** y los **6 atajos** del menú del escritorio (hito 257), las pestañas **Actualizaciones** y **Modelos de IA** de los ajustes (hito 255) y el **empaquetado y la entrega** (fases 5.4-5.6 del plan de la rebanada 5).

---

## [2026-09-28] - Hito 257: El Menú Principal del Host Uno: La Barra de Control y su Cajón, Sobre el View Model del Núcleo

### 🎯 Objetivos y Alcance
El tramo de los ajustes dejó señalado su propio hueco: el **menú principal**. El host Uno tenía el botón de ajustes, la barra de zoom y los atajos del lienzo, pero **no la barra de control del escritorio ni sus menús**. Este tramo porta esa superficie —la barra (`FileFlow.App/Views/ControlBarView.axaml`) y el cajón (el `Border` de 320 px de `MainWindow.axaml`)— sobre el **MISMO `ControlBarViewModel` portable** que el contenedor del núcleo ya resolvía (el del botón Ejecutar del hito 243), con paridad de **entradas, órdenes, estado habilitado/deshabilitado por contexto y atajos**, y **sin tocar ninguna otra superficie**.

### 🧱 Lo construido (dos controles del host, cero líneas en `FileFlow.App`)
| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App.Uno/Controls/ControlBar.xaml(.cs)` | La barra: marca, botón «Menú» y **tres islas** como el escritorio (modos · ciclo · herramientas), con **14 entradas** ancladas por `AutomationId`. Cada botón despacha el **comando canónico** del view model con su `CanExecute` respetado; el estado —visibilidad y habilitación— sale de **enlaces con el view model**, no de una copia local. |
| `FileFlow.App.Uno/Controls/MainMenuDrawer.xaml(.cs)` | El cajón: velo + panel de 320 px a la izquierda, sobre el **mismo estado** (`IsMenuOpen`, el que conmuta el botón «Menú»). Sus dos desplegables (tema e idioma) son los del núcleo y **aplican y guardan al elegir**, como el cajón del escritorio; su entrada «Ajustes» abre la **misma** superficie del hito 255, y su pie enseña la versión del producto. |
| `MainWindow.xaml(.cs)` | El montaje: `Bar.Vm = Drawer.Vm = mainVm.ControlBar` (una sola instancia), las **dos** entradas de ajustes al mismo `Settings.Open()`, y el Inspector conmutando la columna derecha del marco desde su `ToggleInspectorCommand`. |
| `RuntimeSelfCheck.RunControlBarProbe` | El sondeo del menú (**14 `[OK]`**), en **modo propio** (`--selfcheck-controlbar`) porque su ciclo de ejecución mueve el documento y las sondas del lienzo no toleran esa mudanza a mitad. |

⚠️ **Ninguna clave `Uno_*` nueva se inventó y ninguna traducción se reescribió**: los 30 textos de la barra y del cajón se **copian** del diccionario del escritorio, clave por clave, en los dos idiomas, y una guardia lo exige al carácter.

### 📐 La paridad, escrita (y lo que no llega, declarado)
El censo tiene **19 filas** con el AutomationId de cada entrada, la vista que la dibuja, **dónde vive su orden** —el code-behind si es un comando, el XAML si es un enlace bidireccional— y su **estado por contexto**. Frente a él, el escritorio se lee en sus **dos modos de enlace** (`{Binding XCommand}` en su barra y `{Binding ControlBar.XCommand}` en su ventana —mirar sólo el primero dejaba fuera la mitad del menú, el cajón: lo cazó la propia guardia al escribirse) y cada orden suya tiene destino en tres tablas del control:

- **Dibujadas aquí (13)**: menú, Vigilante, Ejecutar, Depurar, Siguiente Paso, Continuar, Pausar, Detener, Deshacer, Rehacer, Revertir, Inspector y el Modo Prueba (casilla).
- **Cumplida por el host (1)**: `OpenWorkflowSettingsCommand` —el ítem «Ajustes» del cajón del escritorio— se cumple por el **evento del host**, porque el comando del núcleo abre una *ventana* por `IWindowService`, que aquí es el Nulo declarado. `HostOwnedOrders`.
- **Pendientes (11)**: Nuevo / Cargar / Guardar Flujo (piden diálogo **síncrono**, frontera de la fase 5.3), Estudio de temas, Métricas, VFS, Diseñador de dataset, Manual, Ejemplos, Acerca de y el aviso de actualización. `DeclaredPendingEntries`. **Un botón cuyo destino no existe no se dibuja: se declara.**
- **Atajos (6)**: el host enruta **sólo** los del lienzo (`EditorKeyboardShortcuts`), así que F5 / F10 / Shift+F5 / Ctrl+N / Ctrl+O / Ctrl+S **no hacen nada aquí** y se declaran con su tecla y su razón (`DeclaredUnroutedShortcuts`). Enrutar una de ellas obliga a quitar su fila: la guardia exige que **ninguna tecla declarada como no enrutada esté en la tabla canónica del lienzo**.

### 🛡️ Guardia y mutaciones
`UnoControlBarParityGuardTests` (**8 casos**): el censo con su ancla y su estado; cada entrada con su orden en el artefacto que la posee y **sin reimplementar** el ciclo (`new ControlBarViewModel(` / `WorkflowExecutionCoordinator` prohibidos en la vista); el montaje compartido con el VM portable; la paridad de órdenes contra el escritorio con las tres tablas disjuntas; los **30 textos idénticos** al escritorio en los dos idiomas; el diccionario del host sin claves huérfanas; la sonda en modo propio; y los atajos declarados contra la tabla del lienzo. **🧬 Cuatro mutaciones (64.ª-67.ª): `menu-que-ejecuta-la-orden-de-otro`, `menu-que-no-declara-lo-que-falta`, `menu-sin-el-estado-de-su-contexto` y `menu-que-no-declara-un-atajo` → las cuatro MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes). COVERAGE → **66 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda **14 de 43** (`UnoControlBarParityGuardTests` deja de estar en la lista de guardias sin mutación).

### 🟢 Ejercido con la aplicación abierta (sesión 268)
El **driver externo por UIA** (`docs/qa/qa_menu_uia.py`, que reutiliza el fontanero de la sesión de ajustes y el instrumento de píxeles del 247) actuó sobre los controles reales mientras el **vigilante** medía. **16 de 16 pasos verificados**:

| Paso | Medición |
| :--- | :--- |
| Línea base | La barra expone **10 de sus 14** entradas y las 4 que faltan son **exactamente** las de contexto (Paso/Continuar son de la depuración; Pausar/Detener, del ciclo en marcha). El cajón: **0 de 5**. Deshacer y Rehacer llegan **deshabilitados** al canal externo (CanUndo/CanRedo del editor) y el control **rechaza** la orden. |
| Pulsar «Menú» | El cajón aparece (**5 de 5** anclas) y **el velo se ve en el pixel**: la banda central del lienzo pasa de `#FCF8F8` (77,8 %) a **`#585454` (99,9 %)**. |
| Pulsar «Ajustes» del cajón | La superficie de ajustes del host se abre (**6 de 6** anclas): la entrada del cajón y la de la barra abren la misma. |
| Cerrar el cajón | Sus entradas **salen del árbol** y el pixel central **vuelve al de la línea base** (`#585454` → `#FCF8F8`). |
| Pulsar el Inspector | La columna derecha **pasa a ser lienzo**: 0,0 % → **93,1 %** de la banda con el color del fondo. Insistiendo: **0,0 %**. |
| Modo Prueba | La casilla conmuta por `TogglePattern` (**1 → 0**) y se devuelve a su estado original. |

La línea de tiempo del vigilante lo corrobora (las 3 tarjetas pasan a 0 mientras el velo cubre la escena y vuelven a 3 al recogerse; **20 cambios materiales**). Al terminar, el fichero de preferencias del usuario queda **byte-idéntico** (md5 igual): el menú no escribe nada.

### ✅ Validación (una corrida por comprobación)
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS 18) | **0 errores** (sólo avisos de nulabilidad preexistentes) |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 `[OK]` · 0 `[FALLO]`** — el menú no rompió ninguna sonda anterior |
| `--selfcheck-controlbar` (menú) | **EXIT 0 · 14 `[OK]` · 0 `[FALLO]`** · «RESULTADO: VERIFICADO» |
| `--selfcheck-settings` (ajustes) | **EXIT 0 · 9 `[OK]` · 0 `[FALLO]`** |
| Guardia del menú | **8 de 8** superados |
| Mutaciones 64.ª-67.ª | **4 de 4 MUERDEN** (testigo rojo, control verde, árbol restaurado) |
| Suite completa | **1902 superadas + 1 omitida de 1903, 0 errores** (RC 0, 2 m 36 s) |

### 📌 Fronteras declaradas
1. **Las once entradas pendientes y los seis atajos** del menú del escritorio: declarados en el control, con su razón, y atados por la guardia. No se dibuja un botón que no puede hacer nada.
2. **El Inspector**: el host arranca con el panel abierto (es una columna del marco, como hasta ahora) y su entrada lo conmuta. El estado inicial es decisión del marco; **la conmutación sí es la del núcleo**.
3. **Los desplegables de tema e idioma del cajón** se ejercen en esta sesión por su presencia, su enlace bidireccional y su catálogo (guardia + sonda); su **selección en vivo** es la del `ControlBarViewModel` portable —write-through, ya medida con la app abierta en la sesión de ajustes para el mismo par de preferencias— y no se volvió a tocar aquí para no escribir en el fichero del usuario.
4. **`MainMenuDrawer` y `DrawerScrim` llevan su `AutomationId` en un `Border`**, que no tiene peer de automatización (la lección de la sesión 267): la presencia del cajón se prueba por sus **entradas**, que sí son controles.
5. **Sigue pendiente** de lo que nombró el usuario: los **paneles que algunos nodos tienen** (los diálogos de nodo y los pickers de variables de la fase 5.3), y del menú del escritorio, las once entradas y los seis atajos de arriba.

## [2026-09-28] - Hito 256: Los Ajustes del Host Uno Ejercidos con la Aplicación Abierta (Verificación a Fondo)

### 🎯 Objetivos y Alcance
El tramo anterior dejó la superficie de **ajustes / apariencia e idioma** del host Uno en el árbol, pero **pidió permiso sin cerrar la verificación**: desde las últimas ediciones (el arranque que aplica el tema y el idioma guardados, la guardia nueva) no había compilación, ni sondas, ni suite demostradas, y la superficie **no se había ejercido nunca en la aplicación real**. Este tramo cierra eso y **no toca el producto**: reconstruye el host, corre sus dos sondas, deja la suite verde, **repite en aislamiento los dos fallos que se habían atribuido a la carga** y ejerce la superficie de verdad con la aplicación abierta, el vigilante de píxeles y un driver externo por UI Automation (el reparto de las sesiones 252-260: el driver **actúa** sobre los controles reales, el vigilante **mide**).

### 📊 Ejercida de verdad, medida en píxeles (sesión 267)
El tema se lee como **la tonalidad que más superficie ocupa de la ventana** (el fondo del lienzo es el área mayor), que es una huella directa del tema vigente y, al reabrir, de cuál se aplicó:

| Paso (app abierta, driver externo + vigilante) | Preferencia tras guardar | Píxel dominante |
| :--- | :--- | :--- |
| Arranque con lo guardado del usuario | `pastel_spring` · `es-ES` | `#FFF8FA` **73,1 %** (claro) |
| Tema por el desplegable real (3 flechas) + Guardar | `dark_fluent` | `#10131B` **72,9 %** (oscuro) |
| Idioma por el desplegable real + Guardar | `en-US` | marco: «Guardar ajustes» → **«Save settings»**/**«Settings»** |
| **Cerrar y reabrir** la aplicación | `dark_fluent` · `en-US` | `#10131B` **73,1 %** + el botón lee **«Settings»** |
| Preferencias del usuario restauradas y reabierto | `pastel_spring` · `es-ES` | `#FFF8FA` **73,1 %** + «Ajustes» |

Las dos preferencias **sobreviven al cierre**, medido en píxeles y en el texto que UIA lee del marco. Además, con la app abierta: **las cuatro secciones** son alcanzables por su conmutador segmentado y cada una expone exactamente sus controles (Almacenamiento 16 anclas, Apariencia 13, Rendimiento 14, Herramientas 13); una **casilla** (`SettingsAutoSaveCheck`) se conmuta por `TogglePattern` y escribe la preferencia (`EnableAutoSave: True → False`); y **tres guardados seguidos** dejan el primero cerrando la superficie y los siguientes sin botón que pulsar (sin caída).

### 🔍 El comportamiento del tema y el idioma, precisado
Medido con las tres pulsaciones: el desplegable **sí registra** cada flecha (el guardado escribió `midnight_oled` = índice 1+3) pero **no aplica nada en vivo**: el lienzo no se repinta hasta que se pulsa Guardar, y por eso mismo **Cancelar deja la aplicación como estaba**. Es la misma semántica que la **ventana de ajustes del escritorio** (el `SelectedThemeId` del VM portable no aplica; aplican `SaveSettingsCommand` → `SetThemeById`/`SetCulture`), y distinta del **cajón de control**, que sí aplica en vivo. Queda escrito para que nadie lo lea como defecto.

### 🛠️ Seis defectos del instrumento (ninguno del producto), encontrados usándolo
El playtest no cambió el producto: cambió las herramientas que lo miden, porque se rompían delante del usuario.
1. **El instrumento no medía el tema**: `--shot` no tenía renglón con el color dominante → se añadió `METRIC top_colors` (la huella del tema, sin tocar la escena del vigilante).
2. **El driver moría con los emoji de los temas**: la consola cp1252 lanzaba `UnicodeEncodeError` al imprimir la lista de items **antes de elegir** → salida fijada a UTF-8 con reemplazo.
3. **Una ancla que nunca podía aparecer**: `SettingsPanel` está puesto en un `Border` y un `Border` **no tiene peer** de automatización; el driver lo tomaba por prueba de presencia y decía «panel ausente» con la superficie abierta → la presencia se prueba por las anclas propias de la superficie.
4. **Mensaje que culpaba a la búsqueda**: cuando la selección se enviaba pero el canal no la podía leer, el driver decía «no se encontró un tema cuyo nombre contenga …» → resultado propio (`SIN_LECTURA`) que se declara en vez de mentir.
5. **Índice del árbol cacheado tras actuar**: tras pulsar Guardar el panel ya estaba cerrado y el caché seguía dando sus anclas por presentes → lectura fresca en `read_state`.
6. **El respaldo de comtypes no existía**: `comtypes.client.GetPattern` no es una función, y un `except` ancho lo tragaba: los patrones **nunca** llegaban por ese camino y varias lecturas salían como «sin lectura» culpando a WinUI → patrones por `iface_*` de pywinauto. De paso, los items del desplegable venían **duplicados** (20 items para 10 temas) y elegir la copia equivocada era una de las razones de la intermitencia; el driver ahora deduplica y tiene `--open <sección>`, `--toggle` y `--value`.

### 📐 La frontera declarada del canal externo
`SettingsMaxCpuThreadsBox` (un `NumberBox` de WinUI) no expone `ValuePattern` al exterior: se ve como un `Spinner` sin hijos. Los tres campos numéricos ya están verificados **por dentro** (sonda de ajustes: `hilos=28->29` write-through), y los `ComboBox` no exponen su selección (`GetCurrentSelection` vacío, `SelectionItem` dice «no seleccionado» para todos): el driver lo declara y lo que zanja es la **preferencia guardada** y el **píxel**.

### ✅ Validación
| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS) | **0 errores** (solo avisos de nulabilidad preexistentes) |
| Selfcheck del lienzo (`--selfcheck`) | **EXIT 0 · 83 OK · 0 FALLO** |
| Selfcheck de ajustes (`--selfcheck-settings`) | **EXIT 0 · 9 OK · 0 FALLO** (con `pastel_spring` guardado: arranque verde en los dos sentidos) |
| Suite completa | **1894 superadas + 1 omitida de 1895, 0 errores** (RC 0) |
| Los dos fallos «de carga» (`TheHeartbeat_ShouldPublishAPlausibleSample`, `FirstRun_ShouldUseEveryThreadItWasGiven`) | **pasan 3 rondas de 3 en aislamiento** → **ruido del entorno, no regresión** |
| Guardia de la superficie (`UnoSettingsSurfaceGuardTests`) | **12 superados de 12, 0 fallos** (53 ms) |
| Mutación del arranque (`arranque-que-no-aplica-el-tema-guardado`) | **MUERDE**: testigo rojo (1 de 1), control verde (1 de 1), árbol restaurado por bytes y recompilado (33,9 s) |
| Preferencias del usuario | **byte-idénticas** al terminar (la sonda no las toca; el playtest las restauró) |

El `RC=1` de una corrida intermedia **no era del producto**: dos `dotnet test` concurrentes en el mismo directorio de salida (`MSB3027/MSB3021` por `testhost` vivo bloqueando los `*.resources.dll`). Repetida en solitario, la suite cierra en **RC 0**.

### 📄 Evidencia
[`docs/qa/qa_ajustes_host_255.md`](file:///docs/qa/qa_ajustes_host_255.md) (§8, esta sesión) + `docs/qa/qa-manual-267/` (`timeline.jsonl`, `watch.log`, los catorce fotogramas rotulados) + el driver `docs/qa/qa_ajustes_uia.py` (con `--open <sección>`, `--toggle`, `--value`).

### 📌 Fronteras
No hay driver de puntero (el clic físico es humano). El **menú principal / barra de control completa** del escritorio sigue **pendiente**, igual que las pestañas **Actualizaciones** y **Modelos de IA** del propio ajustes, los **pickers de variables y los diálogos de nodo** (5.3) y el **empaquetado/CI/release** del host (5.5). Sin commit ni push.

---

## [2026-09-28] - Los Ajustes del Host Uno: La Superficie Que Faltaba, y el Tema Guardado Que No Llegaba al Lienzo (Hito 255)

### El encargo

«Termina de realizar la migración completa a Uno Platform… entre otras cosas el menú principal, **ajustes, temas, idioma**, los paneles que tienen algunos nodos». Este tramo cierra **la superficie de ajustes / apariencia e idioma** del host multiplataforma, y con ella los dos defectos que sólo se ven al **usarla con la aplicación abierta**.

### Lo construido (antes de esta sesión, en el árbol)

- **`FileFlow.App.Uno/Controls/SettingsPanel.xaml(.cs)`**: superficie de cuatro secciones (Almacenamiento, Apariencia, Rendimiento, Herramientas) montada en la ventana, con el `WorkflowSettingsViewModel` **portable** por DataContext (el mismo de la ventana del escritorio) y persistencia por sus comandos canónicos (`SaveSettingsCommand` → `UpdatePreferences` + `SetCulture` + `SetThemeById`). Los exploradores de rutas y la autodetección de herramientas van por los **pickers asíncronos** del host (el contrato síncrono del núcleo aborta en el hilo de UI, declarado en `UnoFileDialogService`).
- **Diccionario PROPIO del host** (`Resources/Strings.resx` y `Strings.es.resx`, ~60 claves `Uno_*`) registrado en `App.xaml.cs`: sin él, elegir English re-culturaba el proceso y los textos seguían saliendo del fallback incrustado — el defecto que la superficie mide.
- **Conmutación de secciones por VISIBILIDAD con los cuatro paneles siempre materializados**: el `Pivot` de WinUI materializa el cuerpo de la pestaña en el pase de layout SIGUIENTE y conmutarlo dentro de un callback de su propia reconstrucción muere con `COMException` (medido: `Failed to assign to property 'Content'`, proceso muerto con exit 127).
- **Sonda en modo propio** (`--selfcheck-settings`): su medición cambia tema e idioma (estado global) y conviviendo con las del lienzo hacía caer la sonda de selección, la de paneles y la de foco del lienzo. Dos tiempos (desplegar y dejar asentar el layout; medir) y **restauración de lo guardado**.

### Los dos defectos que sólo salieron al usarla

**1. El tema guardado se aplicaba al gestor de temas y no al lienzo.** El arranque aplicaba las preferencias guardadas **antes de crear la ventana**; el renglón de la sonda lo midió en rojo (`arranque: tema guardado='light_studio'->'light_studio' aplicado='light_studio'` **y** el token del lienzo en `#FF10131B`, el oscuro por defecto), y el playtest lo confirmó **en píxeles**: con `light_studio` guardado, el fotograma base era oscuro (medio RGB `(17,7 · 21,1 · 29,6)`, 0 % de píxeles claros). La publicación del tema pasa por `UnoThemeHost.PublishThemeVariant`, que muta pinceles y variante **a través de la ventana**: sin ventana la notificación se pierde **sin ruido** — ni excepción ni aviso. **Arreglo**: crear la ventana, aplicar lo guardado y **después** activarla (el usuario no ve el tema de por defecto ni un fotograma). Medido después: `(242,0 · 244,2 · 247,0)`, 97 % claro con el mismo valor guardado.

**2. La sonda del lienzo medía su propia suposición.** Con el arreglo puesto, el selfcheck del lienzo pasó a ROJO (2 fallos deterministas) y el renglón `[color]` crudo que se añadió a la sonda lo explicó en una línea: `fondo #FFFFF8FA->#FFF8FAFC tarjeta #FFFFFFFF->#FFFFFFFF restaurado #FF10131B contra #FFFFF8FA`. El fondo de entrada era **`#FFFFF8FA` = `pastel_spring`**, el tema guardado (y ahora sí aplicado); la sonda probaba con `light_studio` **fijo** —el mismo tema que ya estaba— y «restauraba» a un `dark_fluent` **fijo** que no era el de la entrada. **Era verde porque el producto ignoraba el tema guardado**: el defecto 1 era su condición de verde. **Arreglo**: elegir el tema **contrario al activo** y devolver **el de la entrada** (la regla que la sonda de ajustes ya usaba), con el `[color]` crudo en el informe. Verde en las dos direcciones (guardado oscuro y guardado claro, 83 OK las dos).

### La sesión con la aplicación abierta (261-266)

Sin puntero humano (el puntero inyectado sigue descartado por WinAppSDK, medido en 231/247), el reparto es: **actúa** un driver externo por UI Automation (`docs/qa/qa_ajustes_uia.py`: el botón del marco, las pestañas, los dos desplegables y el botón de guardar por sus `AutomationId`) y **mide** el vigilante del 247 (`qa_manual_session.py`: `--launch`, `--shot`, `--watch`, `--stop`) más el fotograma base de cada reapertura.

| sesión | qué se hizo | medición |
| :--- | :--- | :--- |
| 261 | abrir ajustes por el botón real, elegir tema e idioma en sus desplegables, **Guardar** | preferencia escrita (`dark_fluent`, `en-US`); el marco pasa a «Settings»/«Save settings» **en caliente**; `vigilante.log` + `timeline.jsonl` |
| 263 / 264 / 265 | lanzar con `light_studio` guardado, antes y después del arreglo del orden | **0 % claro → 97 % claro** (medio RGB `(17,7·21,1·29,6)` → `(242,0·244,2·247,0)`) |
| 264 / 265 / 266 | reabrir y leer el marco por UIA | «Settings» con `en-US` guardado; «Ajustes» con `es-ES` guardado |
| 266 | reabrir con `dark_fluent` **guardado por el driver** | **0 % claro**: la elección hecha en la app real sobrevive al cierre |

### Ruido del entorno, atribuido

El perfil de usuario está **compartido con otras sesiones de la máquina**: un vigilante de 1 s midió **~70 reescrituras seguidas del mismo valor** y el tema pasando a `pastel_spring` sin que nada de esta sesión corriera (las escrituras siguieron **después** de terminar el selfcheck, sin ningún proceso `FileFlow*` vivo), con campos que esta superficie no toca modificados (`NodeUsageCounts`, `LastUpdateCheckUtc`). La sonda de ajustes deja el fichero **byte-idéntico** en las comparaciones pareadas. Las **dos pruebas de medida real** que fallaron en corridas cargadas (`TheHeartbeat_ShouldPublishAPlausibleSample`, `FirstRun_ShouldUseEveryThreadItWasGiven`) **pasan en aislamiento** (dos rondas cada una) y no tocan esta superficie: es carga, no regresión.

### Sonda, guardia y mutaciones

- **Sondas**: `--selfcheck-settings` **EXIT 0 con 9 OK** (con el renglón `arranque:` comparando lo GUARDADO con lo APLICADO antes de tocar nada) y `--selfcheck` del lienzo **EXIT 0 con 83 OK** en las dos direcciones de tema guardado.
- **Guardia** `UnoSettingsSurfaceGuardTests` (12 casos): cableado al view model portable, **censo de los 21 controles** con su camino hasta la preferencia (enlace `TwoWay` o **write-back declarado** — los tres campos numéricos van por `NumberBox`, cuyo `Value` es `double` y el VM guarda `int`), el diccionario del host en los dos idiomas sin claves huérfanas, el arranque que aplica lo guardado con su orden, la sonda en modo propio y la restauración de lo del usuario.
- **Mutaciones (61.ª, 62.ª y 63.ª)**: `ajuste-que-no-devuelve-el-idioma`, `ajuste-sin-su-texto` y `arranque-que-no-aplica-el-tema-guardado` → las tres **MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes). COVERAGE: **62 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda **13 de 42**.

### Verificación

Host Uno 0 errores (MSBuild de VS); suite **1894 superadas + 1 omitida de 1895, 0 errores**; las dos sondas en verde; las tres mutaciones mordiendo. Evidencia en [`docs/qa/qa_ajustes_host_255.md`](file:///docs/qa/qa_ajustes_host_255.md) y `docs/qa/qa-manual-261..266/`. **Sin commitear**.

### Frontera declarada

Faltan las pestañas **Actualizaciones** y **Modelos de IA** de la ventana de ajustes (el VM portable las trae, la vista del host no), los **pickers de variables y los diálogos de nodo** (5.3), y el **menú principal / barra de control** completa del escritorio (el host tiene el botón de ajustes y la barra de zoom). Los `ComboBox` de esta pantalla no exponen su selección al canal externo (medido), y la elección de tema por UIA es intermitente: es del driver, no del producto.

