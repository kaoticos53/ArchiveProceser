# La superficie de AJUSTES del host Uno, ejercida con la app abierta (hito 255)

**Veredicto: COMPLETA Y PROBADA, con dos defectos que el playtest destapó y quedaron arreglados** — uno en el
arranque del host (el tema guardado no llegaba al lienzo) y otro en la sonda del lienzo (daba por hecho que la
aplicación arranca oscura, que era cierto sólo mientras el primero existía). Todo medido en píxeles y con
rastro; sin commit ni push.

---

## 1. Qué superficie se completó

La de **ajustes / apariencia e idioma del host multiplataforma**: el botón «Ajustes» del marco abre una
superficie de cuatro secciones (Almacenamiento, Apariencia, Rendimiento, Herramientas) que edita el
`WorkflowSettingsViewModel` **portable** —el mismo que alimenta la ventana de ajustes del escritorio— y
persiste por sus comandos canónicos. Cambiar el **tema** y el **idioma** se ve en caliente, las preferencias
se escriben en el fichero del producto, y **al cerrar y reabrir la aplicación lo elegido sigue puesto**.

Lo que la hace real y no una maqueta, y que ya estaba en el árbol antes de esta sesión:

- Vista y adaptadores propios del host (`Controls/SettingsPanel.xaml(.cs)`), sin tocar el núcleo.
- **Diccionario del host en dos idiomas** (`Resources/Strings.resx` y `Strings.es.resx`, claves `Uno_*`): sin
  él, elegir English re-culturaba el proceso y todo seguía en español por el fallback incrustado.
- Sonda en **modo propio** (`--selfcheck-settings`) que mide la superficie por dentro y **restaura** lo del
  usuario; guardia `UnoSettingsSurfaceGuardTests` (12 casos) y **tres mutaciones que muerden**.

## 2. El playtest: cómo se ejerció la superficie de verdad

El reparto es el de las sesiones 252-260, con otro dedo para actuar:

| Papel | Instrumento |
| :--- | :--- |
| **Actúa** | `docs/qa/qa_ajustes_uia.py` — **driver externo por UI Automation**: otro proceso le da a los controles REALES por su `AutomationId` (el botón del marco, las pestañas, los dos desplegables, el botón de guardar). No es la sonda en proceso: la sonda mide y restaura; el driver **configura**, como el usuario. |
| **Mide** | `docs/qa/qa_manual_session.py` (`--launch`, `--shot`, `--watch`, `--stop`) — el vigilante del 247: captura la pantalla y mide la escena por píxeles. |
| **Prueba la persistencia** | Cerrar la app, volver a lanzarla y medir **el fotograma base** de la nueva sesión + el texto que UIA lee del marco (idioma vigente de verdad). |

Sesiones: `docs/qa/qa-manual-261` (el cambio con la app abierta), `qa-manual-262..266` (reabrir y medir).

### 2.1 El cambio, con la aplicación abierta (sesión 261 y 264/265)

1. `--launch` (base + calibración) y el vigilante midiendo.
2. Driver: `--open` (botón real, y conmuta a Apariencia por el patrón de selección) → `--theme Oscuro` /
   `--language English` (los desplegables reales; el de temas expone sus 10 entradas al abrirse) → `--save`
   (el botón del pie) → `--stop`.
3. La preferencia queda escrita: `ActiveTheme: dark_fluent`, `Language: en-US`.
4. **En caliente**, sin reiniciar: el texto del marco pasó de «Ajustes»/«Guardar ajustes» a
   **«Settings»/«Save settings»** (leído por UIA y visible en `tras-guardar.png`).

### 2.2 La persistencia, medida en píxeles (el hallazgo)

El fotograma base de cada lanzamiento, medio RGB de la franja del lienzo:

| sesión | tema **guardado** al lanzar | medio RGB | claro | lectura |
| :--- | :--- | ---: | ---: | :--- |
| 263 | `light_studio` | (17,7 · 21,1 · 29,6) | 0 % | **antes de arreglar el orden**: el tema guardado NO llegaba al lienzo |
| 264 | `light_studio` | (242,0 · 244,2 · 247,0) | 97 % | **con el arreglo**: llega |
| 265 | `light_studio` | (242,0 · 244,2 · 247,0) | 97 % | repetible |
| 266 | `dark_fluent` **guardado por el driver** | (17,7 · 21,1 · 29,6) | 0 % | la elección hecha en la app real sobrevive al cierre |

Y el idioma, leído del propio marco al arrancar (UIA, el texto que ve el usuario): en 264 el botón decía
**«Settings»** (guardado `en-US`) y en 265/266 **«Ajustes»** (guardado `es-ES`). Las dos direcciones.

## 3. Los dos defectos que el playtest destapó

### 3.1 El tema guardado se aplicaba al gestor de temas y no al lienzo

El primer arreglo del arranque aplicaba lo guardado **antes de crear la ventana**. Medido (sonda de ajustes):
`arranque: tema guardado='light_studio'->'light_studio' aplicado='light_studio'` **y** el token del lienzo en
`#FF10131B` — el oscuro de por defecto. La publicación del tema pasa por `UnoThemeHost.PublishThemeVariant`,
que muta los pinceles y la variante **a través de la ventana**: sin ventana, la notificación se pierde sin
ruido (ni excepción, ni aviso). El mismo estado, medido en píxeles: sesión 263 (0 % claro) frente a 264 (97 %).

**Arreglo**: crear la ventana y aplicar las preferencias guardadas **antes de activarla** (el usuario no ve el
tema de por defecto ni un fotograma). La guardia lo ata: el orden `new MainWindow()` →
`ApplySavedPreferences` → `Activate()` está en el censo, con su mutación
(`arranque-que-no-aplica-el-tema-guardado`).

### 3.2 La sonda del lienzo medía su propia suposición

Con el arreglo puesto, el **selfcheck del lienzo pasó a ROJO** (4 fallos, después 2 deterministas):

```
[FALLO] las tarjetas adoptan el color del tema nuevo (ThemeResource ya evaluado se actualiza)
[FALLO] la restauración del tema deja el dark_fluent activo (grafo como al entrar)
[color] fondo #FFFFF8FA->#FFF8FAFC tarjeta #FFFFFFFF->#FFFFFFFF restaurado #FF10131B contra #FFFFF8FA
```

Los colores crudos lo dicen todo: el fondo de entrada era **`#FFFFF8FA` = `pastel_spring`**, el tema que el
usuario tenía guardado (y que ahora sí se aplica). La sonda probaba con `light_studio` fijo —el mismo tema que
ya estaba puesto, de ahí que la tarjeta no cambiara— y «restauraba» a un `dark_fluent` fijo que no era el de la
entrada. **Era verde porque el producto ignoraba el tema guardado**: el defecto 3.1 era su condición de verde.

**Arreglo**: la sonda elige el tema **contrario al activo** y devuelve **el de la entrada** (la misma regla que
la sonda de ajustes ya usaba para su desplegable), y el renglón `[color]` deja los valores crudos en el
informe. Verificado en las dos direcciones:

| tema guardado al lanzar | selfcheck del lienzo | `[color]` |
| :--- | :--- | :--- |
| `dark_fluent` | EXIT 0 · **83 OK** | `fondo #FF10131B->#FFF8FAFC` `restaurado #FF10131B contra #FF10131B` |
| `pastel_spring` (claro) | EXIT 0 · **83 OK** | `fondo #FFFFF8FA->#FF10131B` `restaurado #FFFFF8FA contra #FFFFF8FA` |

## 4. Verificación final (todo sobre el binario con los dos arreglos)

| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS) | 0 errores |
| Selfcheck del lienzo (`--selfcheck`) | **EXIT 0 · 83 OK · 0 FALLO**, con tema guardado claro y oscuro |
| Selfcheck de ajustes (`--selfcheck-settings`) | **EXIT 0 · 9 OK · 0 FALLO** (`arranque:` verde, token medido en los dos sentidos) |
| Suite completa | **1894 superadas + 1 omitida de 1895, 0 errores** |
| Mutaciones del hito | 3 de 3 **MUERDEN** (testigo rojo, control verde, árbol restaurado por bytes) |
| COVERAGE | **62 declaraciones**, 15 de 17 subsistemas, guardias con mutación que las muerda **13 de 42** |
| Preferencias del usuario | restauradas a `dark_fluent` + `es-ES` (el valor con el que empezó el trabajo) |

## 5. Ruido del entorno, atribuido (y lo que NO es regresión)

- **Dos pruebas de medida real** fallaron en corridas cargadas y **pasan en aislamiento** (dos rondas cada una,
  y la suite entera quedó verde dos veces): `SystemPerformanceMonitorTests.TheHeartbeat_ShouldPublishAPlausibleSample`
  (34 ms en aislamiento) y `EngineFirstRunTests.FirstRun_ShouldUseEveryThreadItWasGiven` (26 de 27 hilos
  simultáneos en carga; 983 ms en aislamiento). Ninguna toca la superficie de ajustes ni el arranque del host.
- **El perfil de usuario compartido está siendo escrito por otra sesión de la máquina.** Medido con un vigilante
  de 1 s sobre el fichero real: **~70 reescrituras seguidas del mismo valor**, `ActiveTheme` cambiando de
  `dark_fluent` a `pastel_spring` sin que nada de esta sesión corriera (las escrituras siguieron **después** de
  que el selfcheck terminara, y sin ningún proceso `FileFlow*` vivo), y campos que esta superficie no toca
  modificados (`NodeUsageCounts`, `LastUpdateCheckUtc` — un chequeo de actualizaciones se ejecutó). La sonda de
  ajustes deja el fichero **byte-idéntico** en las comparaciones pareadas, y el selfcheck del lienzo se midió
  sin cambios. **No es regresión de este trabajo**: es la máquina compartida, y además explica la carga que
  hace intermitentes a las dos pruebas de arriba.

## 6. Fronteras declaradas

- **Dos pestañas de la ventana de ajustes del escritorio no están**: **Actualizaciones** y **Modelos de IA**
  (el view model portable las trae; la vista del host no).
- Los **pickers de variables y los diálogos de nodo** siguen cayendo a su no-op seguro (fase 5.3 del plan):
  el dialogo de ficheros sí es real.
- **No hay driver de puntero**: el clic físico es humano (medido en el 231/247: el puntero inyectado sin
  UIAccess no llega a WinAppSDK). Lo que esta sesión ejercita es el **canal de automatización** de los mismos
  controles. Los `ComboBox` de esta pantalla **no exponen su selección al canal externo** (medido: `SelectionItem`
  dice «no seleccionado» para todos los items y `GetCurrentSelection` no devuelve nada, incluso cuando el cambio
  SÍ llegó al producto): el driver lo declara en vez de inventar un veredicto, y lo que zanja es la preferencia
  guardada y el píxel al reabrir.
- La elección de tema por UIA es **intermitente** (dos intentos de varios quedaron en el tema anterior): es
  del driver, no del producto — el mismo camino funcionó y la preferencia quedó escrita.
- **Instrumento**: `docs/qa/qa_ajustes_uia.py` se conserva (es el driver de la sesión, hermano de los
  instrumentos del 247 y del sondeo UIA del 245); no hay andamiaje desechable en el árbol.

## 7. Qué queda pendiente de lo que nombró el encargo

| Superficie | Estado |
| :--- | :--- |
| **Ajustes** | **Completa y probada** (esta sesión), salvo las dos pestañas declaradas |
| **Temas / idioma** | **Completos**: en caliente y al arrancar, medidos en píxeles y con el texto del marco leído al reabrir |
| **Paneles de nodos** (cajón, inspector) | Ya existían de la rebanada 4 y siguen verdes (selfcheck 83 OK); les faltan los pickers de variables (5.3) |
| **Menú principal / cajón de la ventana** (barra de control del escritorio: tema, idioma, ejecutar, ajustes, atajos) | **Pendiente**: el host tiene un botón de ajustes y la barra de zoom, no la barra de control completa |
| Ventanas del escritorio (Theme Studio, dashboard, VFS, diseñador de dataset, manual, Acerca de) | Pendientes y **declaradas fuera** del plan de la rebanada 5 (§9) |
| Empaquetado, CI y release del host (5.5) | Pendiente |

---

## 8. Sesión 267: la misma superficie, ejercida con la aplicación abierta y medida en píxeles

El pase anterior dejó la superficie en el árbol pero **pidió permiso sin cerrar la verificación**. Esta sesión
la cierra. No se tocó el producto: se reconstruyó el host, se corrieron sus dos sondas, se dejó la suite verde y
se **ejerció la superficie de verdad**, con el tema medido en píxeles.

### 8.1 El tema, medido en píxeles (lo que faltaba)

El instrumento no sabía leer el tema de una captura. Se le añadió un renglón —`METRIC top_colors`, el color que
más superficie ocupa de la ventana: el fondo del lienzo es el área mayor— y con eso el tema vigente (y, al
reabrir, el tema **guardado**) quedan medidos de forma directa, sin depender de ninguna lectura interna.

| Paso (app abierta · driver externo UIA + vigilante de píxeles) | Preferencia tras guardar | Píxel dominante |
| :--- | :--- | :--- |
| Arranque con lo guardado del usuario | `pastel_spring` · `es-ES` | `#FFF8FA` **73,1 %** |
| Tema elegido en su desplegable real (3 flechas) + Guardar | `dark_fluent` | `#10131B` **72,9 %** |
| Idioma elegido en su desplegable real + Guardar | `en-US` | marco: «Guardar ajustes» → **«Save settings»** |
| **Cerrar y reabrir la aplicación** | `dark_fluent` · `en-US` | `#10131B` **73,1 %** · el botón lee **«Settings»** |
| Preferencias del usuario restauradas y reabierto | `pastel_spring` · `es-ES` | `#FFF8FA` **73,1 %** · «Ajustes» |

Comandos de la sesión (carpeta `docs/qa/qa-manual-267/`, `FILEFLOW_QA_WORK=qa-manual-267`):
`qa_manual_session.py --launch` → `qa_ajustes_uia.py --open` / `--keyselect SettingsThemeCombo …` / `--save` →
`qa_manual_session.py --shot <rótulo>` → `--stop` → `--launch` (reapertura) → `--shot` → `--stop`, con
`--watch 110` midiendo en paralelo (`timeline.jsonl` + `watch.log`).

### 8.2 Lo demás que se ejerció con la app abierta

- **Las cuatro secciones**, alcanzables por su conmutador segmentado, y cada una expone exactamente sus
  controles (Almacenamiento 16 anclas, Apariencia 13, Rendimiento 14, Herramientas 13). La superficie **abre en
  Almacenamiento** (no en Apariencia).
- **Una casilla**: `SettingsAutoSaveCheck` por `TogglePattern` (`1 → 0`) y `--save` → `EnableAutoSave: True → False`.
- **Tres guardados seguidos**: el primero cierra la superficie; los siguientes no encuentran el botón. Sin caída.
- **Cancelar** (botón de cerrar o velo): la aplicación queda como estaba (y la preferencia, intacta).
- **El tema y el idioma no se aplican en vivo**: las tres pulsaciones de flecha **sí registran** (el guardado
  escribió `midnight_oled`, el índice 1+3) pero el lienzo no se repinta hasta Guardar. Es la semántica de la
  ventana de ajustes del escritorio (el `SelectedThemeId` del VM portable no aplica; aplican
  `SaveSettingsCommand` → `SetThemeById`/`SetCulture`), distinta de la del cajón de control. **Declarado, para
  que no se lea como defecto.**

### 8.3 Defectos del instrumento (6), encontrados usándolo

Ninguno del producto: el playtest rompió las **herramientas**, y se arreglaron porque mentían delante del usuario.

| # | Defecto | Arreglo |
| :-- | :--- | :--- |
| 1 | `--shot` no medía el tema | renglón `METRIC top_colors` en `qa_manual_session.py` |
| 2 | El driver **moría** imprimiendo los nombres de tema (emoji en consola cp1252): `--theme` nunca llegaba a elegir | salida a UTF-8 con reemplazo |
| 3 | `SettingsPanel` (AutomationId de un `Border`, que **no tiene peer**) se tomaba por prueba de presencia: decía «panel ausente» con la superficie abierta | presencia por las anclas propias de la superficie |
| 4 | Con la selección enviada pero sin lectura, el mensaje culpaba a la búsqueda («no se encontró un tema…») | resultado propio `SIN_LECTURA`, declarado |
| 5 | El índice del árbol cacheado: tras Guardar, el panel ya estaba cerrado y el caché seguía dando sus anclas | lectura fresca en `read_state` |
| 6 | El respaldo de comtypes llamaba a `comtypes.client.GetPattern`, **que no existe**: un `except` ancho lo tragaba, los patrones nunca llegaban y varias lecturas salían como «sin lectura» culpando a WinUI. De paso: los items del desplegable venían **duplicados** (20 para 10 temas) | patrones por `iface_*` de pywinauto, items deduplicados, y modos nuevos `--open <sección>`, `--toggle`, `--value` |

### 8.4 Verificación final de la sesión

| Pieza | Resultado |
| :--- | :--- |
| Compilación del host Uno (MSBuild de VS) | **0 errores** |
| `--selfcheck` (lienzo) | **EXIT 0 · 83 OK · 0 FALLO** |
| `--selfcheck-settings` | **EXIT 0 · 9 OK · 0 FALLO** (con `pastel_spring` guardado: arranque verde en los dos sentidos) |
| Suite completa | **1894 superadas + 1 omitida de 1895, 0 errores (RC 0)** |
| Los dos fallos «de carga» | **3 de 3 en aislamiento** → ruido del entorno, **no regresión** |
| Las tres mutaciones del hito | `old` exacto y único en su fichero y testigo presente (verificado por conteo) |
| Preferencias del usuario | restauradas **byte-idénticas** a como estaban al empezar |

**El `RC=1` de una corrida intermedia no era del producto**: dos `dotnet test` concurrentes sobre el mismo
directorio de salida (un `testhost` vivo bloqueaba los `*.resources.dll` → `MSB3027`/`MSB3021`). En solitario,
la suite cierra en **RC 0**. Y las dos pruebas que antes fallaban bajo carga (`TheHeartbeat_ShouldPublishAPlausibleSample`,
`FirstRun_ShouldUseEveryThreadItWasGiven`) **pasan en aislamiento**, que es la respuesta que este pase debía dar.
