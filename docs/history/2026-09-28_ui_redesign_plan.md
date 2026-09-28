# Plan Maestro de Rediseño Visual y UX — FileFlow Studio (Avalonia 12 / .NET 9)

> **Fecha:** 2026-09-15 · **Rama:** `feature/crossplatform-avalonia` · **Ámbito:** capa de presentación (`FileFlow.App`, UI de plugins vía recursos compartidos)
> **Estado:** PROPUESTA — pendiente de aprobación de alcance (ver §12).

---

## 1. Resumen ejecutivo

La UI funciona y es funcionalmente rica (lienzo DAG Nodify, inspector, consola SQLite, Theme Studio, Spotlight), pero **no existe un sistema de diseño**: el estilo se aplica "a mano" en cada vista. El resultado es una interfaz que se ve *cargada*, con jerarquía visual débil, contraste irregular y una sensación de "plantilla oscura genérica" en lugar de un producto de estudio profesional.

**Diagnóstico en una frase:** hay *tokens de color* (y sólo de color), pero no hay *capa de componentes*, ni *escala de forma*, ni *tipografía*, ni *iconografía vectorial*, ni *sistema de movimiento*; y el motor de temas tiene fugas que hacen que la personalización visual no se aplique.

**Datos de la auditoría (medidos, no estimados):**

| Métrica | Valor | Problema |
| :--- | :--- | :--- |
| Literales `#HEX` en AXAML | **240** (÷ 143 dentro de diccionarios de tema, 97 en vistas) | Temas no intercambiables, rotura en tema claro |
| Valores distintos de `CornerRadius` inline | **21** (2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 16, 32, 4,0,0,4…) | Sin escala de radios → bordes incoherentes |
| Valores distintos de `FontSize` inline | **25** (8 → 28, con medias décimas: 9.5, 10.5, 11.5…) | Sin escala tipográfica → jerarquía borrosa |
| Emojis usados como iconos | **93 en AXAML + 153 en C#** | Dependencia de fuentes emoji del SO: en Linux sin *Noto Color Emoji* se muestran como *tofu* (cuadrado). Riesgo multiplataforma real, contradictorio con el objetivo del proyecto |
| Estilos globales de controles en `App.axaml` | **2** (`Window`, `ToolTip`) | Cero política de componentes: cada Button/TextBox repite `Padding`, `CornerRadius`, `FontSize`, `Cursor="Hand"` |
| Tokens definidos pero **nunca consumidos** | **7**: `AppFontFamily`, `AppFontSize`, `AppCornerRadius`, `ScrollbarThumbBrush`, `ConnectionWireBrush`, `GridLineBrush`, `NodeShadowEffect` | El Theme Studio promete personalización (radio, tipografía) que **no tiene efecto visual** |

**Defectos funcionales del sistema de temas detectados (bugs, no opiniones):**

1. **`Application.RequestedThemeVariant` nunca cambia.** `ThemeManager` sólo inyecta brushes en `Application.Resources`; `ThemeVariant` se fija a `Dark` en `App.axaml`. Consecuencia: en temas **Light/Pastel** todos los controles del `FluentTheme` (ComboBox, ScrollBar, DataGrid, ContextMenu, TabControl, CheckBox, Popup) **siguen pintándose oscuros** mientras el resto es claro. Es la causa raíz más visible de la sensación de "UI mal rematada".
2. **Sólo la ventana principal se re-tematiza.** `WindowThemeHelper.ApplyThemeToWindow` se invoca únicamente en `MainWindow.axaml.cs:22`. Los 10 diálogos (`AboutDialogWindow`, `ThemeCustomizerWindow`, `WorkflowSettingsWindow`, `VariablePickerWindow`, `TextEditorDialogWindow`, `VirtualFileSystemExplorerWindow`, `WorkflowMetricsDashboardWindow`, `AiModelDownloadDialog`, `FilePreviewerWindow`, `ScriptStudioWindow`) permanecen en variante oscura.
3. **Recursos inexistentes.** `EditorView.axaml:192` usa `{DynamicResource CardBgBrush}` y `CardBorderBrush`, **que no están definidos en ningún sitio** → la barra de *breadcrumbs* del sub-workflow se pinta sin fondo ni borde.
4. **`TextMutedBrush` sólo existe en `DarkTheme.axaml`** y `ThemeResourceApplier` no lo emite (y `ApplyResourceDictionary` nunca elimina claves antiguas). Se usa en telemetría de nodos (`NodeCardView.axaml:463-474`), breadcrumbs (`EditorView.axaml:209`) y `AiModelDownloadDialog.axaml:74` → queda para siempre el valor oscuro `#64748B` sobre fondos claros.
5. **Duplicidad de fuentes de verdad.** `Themes/*.axaml` (4 diccionarios escritos a mano) y `BuiltInThemesCatalog` + `ThemeResourceApplier` (diccionario generado en C#) definen los mismos tokens por separado → *drift* garantizado (ver puntos 3 y 4).
6. **i18n violada en elementos nuevos:** los rótulos del HUD del lienzo están en inglés fijo (`"Selected:"`, `"Connections:"`, `"Location:"`, `"Zoom:"` en `EditorView.axaml`) y los textos del Spotlight (`"↑↓ Navegar • Enter Crear • Esc Cerrar"`), el `SplashScreenWindow` ("Inicializando Motor de Flujo DAG…") y `ThemeCustomizerWindow` ("Botón de Prueba", "Guardar y Aplicar") están *hardcodeados* — incumple la norma 5 (`AGENTS.md`) y la regla 5 (`.agents/rules/rules.md`).
7. **Colores no temáticos en superficies clave:** `EditorView.axaml` (HUD: `#9914161C`, `#10B981`, `#F59E0B`, `#FB923C`, `#06B6D4`), `EditorZoomBarView.axaml` (`#C01E1E1E`), `SplashScreenWindow.axaml` (11 hex), `NodeCardView.axaml` (glow `#90…`/`#E0…`).
8. **Binding por reflexión:** `AvaloniaUseCompiledBindingsByDefault=false` → todos los enlaces se resuelven por reflexión: más lentos y sin detección de errores en compilación.

---

## 2. Objetivos del rediseño

| # | Objetivo | Métrica de éxito |
| :--- | :--- | :--- |
| O1 | **Sistema de diseño real** (tokens → componentes → vistas) | ≥ 90 % de vistas sin `#HEX`, sin `CornerRadius` inline y sin `FontSize` inline |
| O2 | **Modernidad visual y jerarquía** | Elevación, radios, espaciado y color en 3 niveles claros: lienzo / panel / tarjeta |
| O3 | **Dinamismo y atractivo** | Catálogo de microinteracciones (§7) implementado y perceptible pero ≤ 250 ms, sin bloquear entrada |
| O4 | **Tematización sin fugas** | Cambio Dark/Light/Cyber/Pastel/Custom correcto en **todas** las ventanas y en los controles Fluent internos |
| O5 | **Multiplataforma real** | Cero dependencia de emojis del sistema; iconografía vectorial; funciona en Windows/Linux/macOS |
| O6 | **Usabilidad y accesibilidad** | Ratio de contraste ≥ 4.5:1 en texto de cuerpo, ≥ 3:1 en texto grande/iconos; foco visible y navegable por teclado; `AutomationProperties` en acciones |
| O7 | **Rendimiento de UI** | Toolbox virtualizado; sin sombras por nodo; ≥ 60 FPS al arrastrar sobre un grafo de 150 nodos |

---

## 3. Dirección visual (propuesta)

**Concepto: "Studio Pro" — lienzo oscuro tipo ComfyUI/Blender, paneles flotantes tipo nave espacial, acento índigo-cian.**

* **Capas de superficie (3 niveles):** lienzo (el más profundo) → paneles (drawer, toolbox, inspector, consola) → tarjetas (nodos, filas, subpaneles). Hoy lienzo y paneles comparten casi el mismo valor (`#13151A` y `#16171B`), por eso "todo se ve plano".
* **Bordes redondeados coherentes:** radios **6 / 8 / 10 / 12 / 14 / pill** (según tamaño de elemento). Los nodos y paneles suben de 6-8 px a **10-12 px**; los controles internos se quedan en 6-8 px.
* **Color:** acento primario índigo `#6366F1` con **halo** (`Glow`) al 30 % de alpha; estados (success/warning/error/info) con **variantes "soft"** (fondo al 12-15 % + borde al 35 % + texto saturado) para badges y banners; color de categoría de nodo como *acento lateral* + barra cimera.
* **Elevación:** 4 niveles de sombra + **borde luminoso de 1 px** en la cara superior (`inset light border`) — es lo que da sensación de cristal y de "moderno".
* **Cristal selectivo:** HUD, barra de zoom y menús flotantes sobre el lienzo con fondo translúcido del color de superficie + blur (si el backend lo permite) o gradiente equivalente.
* **Tipografía:** escala de **7 tamaños** (`20 / 16 / 14 / 13 / 12 / 11 / 10`), monoespaciada sólo para telemetría/JSON/rutas. Interlineado y pesos normalizados (400/600/700, se eliminan los "Medium" sueltos).
* **Iconografía:** set vectorial propio (~60 glifos `StreamGeometry`) en lugar de emojis; trazo 1.5 px, esquinas redondeadas, 3 tamaños (12/16/20).

---

## 4. Arquitectura del sistema de diseño

### 4.1 Decisión pendiente: dónde vive el sistema de diseño

Los plugins consumen hoy recursos del host vía `{DynamicResource …}` (49 usos sólo en `FileFlow.Plugin.AI`) y **no pueden** referenciar `FileFlow.App` (regla 2/6). Para que los diálogos de plugin dejen de verse "de otro programa" hacen falta dos piezas separadas:

| Pieza | Ubicación propuesta | Motivo |
| :--- | :--- | :--- |
| **Tokens** (colores, radios, espaciado, tipografía, movimiento) | Aplicados en caliente a `Application.Resources` (como hoy: `ThemeResourceApplier`) | Ya funciona; los plugins los heredan gratis |
| **Estilos de control, iconos y plantillas** | **Nuevo proyecto `FileFlow.Ui`** (class library Avalonia, sin dependencias de Core/App), referenciado por `FileFlow.App` y por los plugins | `FileFlow.Sdk` debe seguir puro (sin Avalonia); `FileFlow.App` no es referenciable por plugins |

Si se aprueba `FileFlow.Ui`, requiere **2 ajustes de gobernanza** (documentados, no silenciosos):
1. Añadir `FileFlow.Ui` a la lista de ensamblados compartidos del ALC en `FileFlow.Core/Plugins/PluginAssemblyLoadContext.cs:19-25` (hoy sólo comparte `FileFlow.Sdk` y `FileFlow.Core`) para evitar cargas duplicadas del ensamblado de UI.
2. Actualizar `AGENTS.md` §6 y `.agents/rules/rules.md` §2 con la excepción explícita ("los plugins pueden referenciar `FileFlow.Ui`").

**Alternativa sin nuevo proyecto:** mantener los estilos sólo en `FileFlow.App`; los diálogos de plugin conservan aspecto Fluent por defecto (coste: inconsistencia visual permanente). Es la opción de mínimo riesgo si se quiere acotar el alcance.

### 4.2 Token set v2 (contrato)

Todos los tokens se generan en `ThemeResourceApplier` **y** se validan con un test de completitud (§9). Se mantienen los nombres actuales por compatibilidad (los consumen tests y plugins) y se añaden familias nuevas:

```text
Superficies   Surface.Canvas / Panel / Raised / Inset / Overlay / Hover / Selected / Header
Bordes        Border.Subtle / Default / Strong / Focus    +  Border.Glow(accent)
Texto         Text.Primary / Secondary / Muted / Disabled / OnAccent / Link
Acento        Accent.Primary / Hover / Pressed / Glow / Soft(12%)
Semánticos    Success / Warning / Error / Info  (+ .Soft y .Border por cada uno)
Nodos         Node.Category.<cat>.Accent / .Soft / .Header   (12 categorías)
Radios        Radius.Xs=4 · Sm=6 · Md=8 · Lg=10 · Xl=12 · Xxl=14 · Pill=999
Espaciado     Space.1=2 · 2=4 · 3=6 · 4=8 · 5=12 · 6=16 · 7=20 · 8=24 · 9=32
Tipografía    Font.Display=20/Bold · Title=16/Bold · Subtitle=14/SemiBold
              Body=13 · BodySm=12 · Caption=11 · Micro=10  (+ Mono)
Elevación     Elev.1..4 (BoxShadows) + Elev.Glow.(accent|success|error)
Movimiento    Motion.Duration.Instant=80 · Fast=120 · Base=200 · Slow=320
              Motion.Ease.Standard / Enter / Exit / Spring
```

**Regla dura:** ningún AXAML nuevo puede contener `#HEX`, `CornerRadius`, `FontSize` literales; sólo `{DynamicResource …}` o clases de estilo. Se hace cumplir con un test (`UiStyleLintTests`) que fallará el build si aparecen literales fuera de `Themes/`.

### 4.3 Capa de componentes (`FileFlow.Ui/Styles/*`)

| Fichero | Contenido |
| :--- | :--- |
| `Tokens.axaml` | Tokens por defecto (espejo de los preset de `BuiltInThemesCatalog`) |
| `Buttons.axaml` | `Button` base + clases `.primary .success .danger .ghost .icon .toolbar .link` con estados *hover / pressed / focus-visible / disabled* y transición de color 120 ms |
| `Inputs.axaml` | `TextBox` (`.search`, `.path`, `.mono`), `NumericUpDown`, `ComboBox`, `CheckBox`, `Slider`, `ToggleSwitch`, `RadioButton` (`.segmented`) |
| `Surfaces.axaml` | `Border` con clases `.panel .raised .inset .card .interactive` (elevación + hover lift) |
| `Containers.axaml` | `TabControl` (`.pill` y `.underline`), `Expander` (`.card`), `ScrollViewer`, **ScrollBar fina con auto-ocultado**, `GridSplitter` con grip y resalte al hover |
| `Overlays.axaml` | `ToolTip`, `ContextMenu`, `MenuItem`, `Flyout`, `Toast`, `ProgressRing`, `ProgressBar` slim, `Skeleton` |
| `DataGrid.axaml` | Cabecera, filas zebra, hover, selección, edición — unifica consola e inspector |
| `Nodes.axaml` | Estilos Nodify: `Node`, `NodeInput/Output`, `Connection`, `PendingConnection`, `Decorator` |
| `Windows.axaml` | Shell de ventana/diálogo unificado (título, acciones, cierre, borde redondeado, sombra) |
| `Icons.axaml` | ~60 `StreamGeometry` + control `IconGlyph` (tamaño/color por propiedad) |

Tras crearla, se sustituye el estilo inline en las **31 vistas AXAML** (prioridad: `MainWindow`, `NodeCardView`, `EditorView`, `ControlBarView`, `NodeToolboxView`, `NodeInspectorPanelView`, `LogView`, `StatusBarView`, y los 10 diálogos + `Themes/Templates/*`).

---

## 5. Fases de implementación

### Fase 0 — Saneamiento y red de seguridad (1-2 días) — ✅ **COMPLETADA (2026-09-15)**

*Implementado: propagación de `RequestedThemeVariant` y reaplicación a todas las ventanas, token `TextMuted` + `OverlaySurfaceBrush`, eliminación de tokens fantasma y de los 3 diccionarios de tema muertos, baseline espejo del preset `dark_fluent`, tipografía alimentada por tokens, i18n del HUD y del Spotlight, y 16 guardias nuevas (`ThemeTokenCompletenessTests`, `UiStyleLintTests` con trinquete, `ThemeVariantPropagationTests`). Detalle completo en `docs/PROJECT_WALKTHROUGH.md` (2026-09-15).*

*Pendiente heredado: endurecer `AvaloniaTestHelper` (la inicialización headless falla en silencio por acceso entre hilos al Dispatcher), y los ~150 literales de color restantes en vistas que el lint mantiene congelados hasta la Fase 2.*
* Corregir los **8 defectos** de §1.1-1.8 (empezando por `CardBgBrush`/`TextMutedBrush` y la propagación de `RequestedThemeVariant` a `Application` y a todas las ventanas).
* Añadir `Application.Current.RequestedThemeVariant` en `ThemeManager.SetTheme` + helper `ApplyThemeToWindow` invocado desde una base común de ventana (`ThemedWindow`) o desde `OnOpened` de cada diálogo.
* Sustituir `Themes/*.axaml` por **generación única**: o se eliminan (el runtime ya los sobrescribe) o se autogeneran desde `BuiltInThemesCatalog` en build. Elimina el drift.
* Tests nuevos: `ThemeTokenCompletenessTests` (todo token referenciado con `DynamicResource` en el repo existe en los 4 presets), `UiStyleLintTests` (sin literales de estilo), `ThemeVariantPropagationTests` (headless: cambiar tema → `RequestedThemeVariant` y brushes coherentes en ventana hija).
* **Quick wins de i18n** (§1.1.6) y retirada de literales de color en HUD/zoom bar/splash.

### Fase 1 — Fundación de tokens y tipografía (3-4 días) — ✅ **COMPLETADA (2026-09-15)**

*Implementado: set de tokens v2 completo en `ThemeDefinition` + `ThemeResourceApplier` + baseline + presets (**radios** `RadiusXs`..`RadiusXxl`, **tipografía** `FontSizeMicro`..`FontSizeDisplay`, **espaciado** `Space1..9`/`Pad1..9`, **elevación** `Elev1..4` + `ElevGlow*` + `ElevPanelLeft`, velos `Scrim*` y tintes `Chip*`/`TintFaint`); escala tipográfica única de 7 escalones sustituyendo los 25 tamaños distintos (**400 `FontSize` + 154 `CornerRadius` + 7 sombras literales migrados en 31 vistas** host y plugins, conservando radios asimétricos y el `0` deliberado); y **Theme Studio reconstruido sobre `ThemeSettingCatalog`** con filas tipadas por reflexión, ventana a 100% declarativo (cero literales) y **vista previa en vivo** que resuelve los tokens del tema en edición (radios, densidad y profundidad incluidas). Guarias nuevas: `ThemeStudioCatalogTests` (11), `ThemeStudioVisualContractTests` (6) y +2 en `UiStyleLintTests` (cero tolerancia con `FontSize`/`BoxShadow` literales); trinquete de estilos de 24 a 14 ficheros. `ConnectionWireBrush`/`GridLineBrush` ya estaban conectados desde la Fase 0.*

**Pendiente heredado de esta fase:** migrar los `Padding`/`Margin` masivos a `Space*`/`Pad*` (el espaciado existe y se consume en la capa de estilos y en el Studio, pero las vistas aún usan literales), los acentos de categoría de `NodeCategoryStyling` y decidir si `AppFontSize`/`AppCornerRadius` se consolidan en `FontSize*`/`Radius*` o se documentan como alias estables.

**Plan original (referencia):**
* Implementar el token set v2 completo en `ThemeDefinition` + `ThemeResourceApplier`. ✔
* **Hacer efectivos los tokens hoy muertos**: `AppCornerRadius`, `AppFontSize`, `AppFontFamily`, shadows → conectados a los estilos de componente. ✔
* Escala tipográfica única; migración de los 25 tamaños a 7. ✔
* Migrar `NodeCategoryStyling` (colores por categoría) y los 12 acentos de categoría a tokens temáticos. ⏳ *pendiente*

### Fase 2 — Capa de componentes y migración de vistas (5-7 días) — 🔶 **EN CURSO (2026-09-15)**

*Implementado: capa de componentes en **`FileFlow.App/Styles/`** (`Typography`, `Surfaces`, `Buttons`, `Inputs`, `Containers`) con clases reutilizables para botones, campos, superficies, pestañas, `GridSplitter`, scrollbars y la rejilla de la consola; y migración a 100% declarativo de `ControlBarView`, `StatusBarView` y `LogView` (0 literales de color/radio/tipografía), con guardias de contrato de clases y de contenido. Nota de arquitectura: la capa vive en el host (no en un ensamblado `FileFlow.Ui` compartido) porque los plugins no pueden referenciar `FileFlow.App`; la decisión sobre el ensamblado compartido sigue pendiente para los diálogos de plugin. Pendiente: resto de vistas (drawer/toolbox, inspector, diálogos, `MainWindow`, tarjetas de nodo), `Controls/` propios (`IconGlyph`, `StatusLed`, `Badge`, `ToastHost`, `CommandPalette`, `NumberStepper`) y *compiled bindings* por vista.*


* Crear `FileFlow.Ui/Styles/*` (§4.3) + `FileFlow.Ui/Controls/` (control `IconGlyph`, `StatusLed`, `Badge`, `ToastHost`, `CommandPalette`, `NumberStepper`).
* Migrar las 31 vistas a clases de estilo; retirar los 97 hex y los radios/tamaños inline.
* Activar `AvaloniaUseCompiledBindingsByDefault=true` de forma incremental (por vista, con `x:DataType`), empezando por las vistas ya migradas.

### Fase 3 — Iconografía vectorial (2-3 días) — ✅ **COMPLETADA (2026-09-15)**

*Implementado con **`Material.Icons.Avalonia` 3.0.2** (13.645 glifos vectoriales, MIT, Avalonia 12) en lugar de un `Icons.axaml` autorado a mano: `NodeIconResolver` devuelve ahora `MaterialIconKind` (tabla exacta + heurística + tabla de emoji heredado para no romper el contrato del SDK ni los flujos guardados), los iconos de nodo/toolbox/métricas/acciones están tipados de extremo a extremo, y **los 137 usos de emoji en el AXAML del host y de los plugins** (28 vistas) se han convertido a iconos vectoriales que heredan el color del tema. Los emojis que son **texto** (logs, telemetría, informes Markdown/HTML/CSV, CLI) se conservan por decisión de producto: no se pueden representar como vector. Guardias nuevas: pictogramas prohibidos en AXAML de UI, validez de todo `Kind` literal, cobertura por tipo de nodo, traducción de valores heredados y registro de estilos. Detalle en `docs/PROJECT_WALKTHROUGH.md` (2026-09-15).*

*Pendiente heredado de esta fase: `NodeIconResolver` cubre los tipos de nodo conocidos; conviene ampliar la tabla exacta (o aceptar la heurística) al añadir plugins nuevos, y sustituir los emojis de los mensajes de estado por texto neutro si algún día se quiere una consola 100% libre de pictogramas.*

**Plan original (referencia):**
* `Icons.axaml` con los ~60 glifos; `NodeIconResolver` deja de devolver emojis y devuelve **claves de icono** (`"archive.unpack"`), con tabla de equivalencia para no romper persistencia (`FilesystemIcon` en workflows guardados se mantiene como key).
* Sustituir los 93 emojis de AXAML y los 153 de C# (toolbox, spotlight, menús, barra de estado, nodos, diálogos).
* Fallback tipográfico por SO en `FontFamily` (sin depender de emoji fonts).

### Fase 4 — Movimiento y microinteracciones (3-4 días)
* Implementar el catálogo §7 como estilos reutilizables (`Styles/Motion.axaml`) respetando `Motion.Duration.*` y un **modo "reducir movimiento"** (detectado del SO y/o preferencia de usuario, que también debe persistirse en `UserPreferences`).

### Fase 5 — Shell, layout e IA de la aplicación (5-8 días)
* **Barra de título integrada** (`ExtendClientAreaToDecorationsHint`) con el menú y el branding (verificar comportamiento en Wayland: dejar *fallback* a decoraciones nativas).
* **Rail lateral de iconos** (Nodos · Favoritos · Recientes · Archivos/VFS) reemplazando la duplicidad actual *drawer ↔ botones de la barra de control*: la actual drawer repite 「Inspector」, 「Ejecutar」 y el toggle de panel, que ya están en la barra superior. Se propone: **rail siempre visible + drawer sólo para acciones de aplicación** (tema, idioma, ayuda).
* **Dock inferior con pestañas** (Consola · Métricas · Archivos/VFS · Problemas) con colapso animado, en lugar del panel de log fijo de 200 px.
* **Paleta de comandos global** (`Ctrl+K`) reutilizando la infraestructura del *Spotlight* (hoy sólo añade nodos): ejecutar flujo, abrir ajustes, cambiar tema, buscar nodo, abrir workflow.
* **Empty states**: lienzo sin nodos ("Pulsa Shift+A para añadir tu primer nodo" + plantillas de ejemplo), consola sin logs, toolbox sin resultados de búsqueda, inspector sin selección.
* **Sistema de notificaciones toast** (éxito/error/progreso) en sustitución de mensajes sólo en la barra de estado.
* **Persistencia de layout** (posición/tamaño de paneles por usuario) y atajos documentados en un panel de ayuda («?»).

### Fase 6 — Lenguaje visual del lienzo y de los nodos (4-6 días) — 🔶 **EN CURSO (2026-09-15)**

*Implementado (2026-09-15): **tarjeta de nodo** con cabecera en dos líneas (icono + título tipado por tokens + controles · badge de categoría + estado + cuello de botella) y **pie de telemetría con icono vectorial y valor crudo formateado en la vista**, oculto hasta la primera ejecución; **semántica de tipo en los sockets** (forma por familia de dato, color por tokens del tema, tooltip con dirección/familia/tipo/estado y plantilla única compartida por entradas y salidas); **resaltado de compatibilidad al arrastrar** (origen, compatible en verde, aviso ámbar por tipos distintos y atenuado del resto, también en la etiqueta del puerto y en las formas `Path`/rombo); **flow de energía animado** en los cables durante la ejecución (capa superpuesta con guiones en movimiento, color por familia de tipo, menú contextual movido al cable interactivo); i18n y guardias nuevas (`PortSemanticsTests`, `ConnectionEnergyTests`, `NodeCardVisualContractTests`). Dos bugs de binding silenciosos corregidos (telemetría del pie y etiquetas de las acciones del nodo).*

*Pendiente de esta fase: minimapa, regla de coordenadas, rejilla de puntos en dos niveles dependiente del zoom, marco de selección con relleno tenue y multi-selección diferenciada, indicador de nodo "actual" en depuración, panel de parámetros con animación de altura y HUD del lienzo unificado.*

**Plan original (referencia):**
* Tarjeta de nodo rediseñada: cabecera con icono vectorial + categoría + estado, cuerpo con puertos agrupados, panel de parámetros plegable con animación de altura, footer de telemetría legible (hoy 9.5 px).
* Semántica de puertos: leyenda de tipos de conector, resaltado del puerto compatible al arrastrar (dim del resto), etiquetas al hover cuando el nodo está colapsado.
* Cables: animación de **flujo de energía** (dash offset animado) durante la ejecución; color por tipo ya existe.
* Selección: marco de selección con relleno tenue, multi-selección diferenciada, indicador de nodo "actual" en depuración.
* Añadir **minimapa** y **regla de coordenadas** opcionales, fondo del lienzo con rejilla de puntos sutil en dos niveles (zoom dependiente).
* HUD inferior unificado y temático (con i18n) con los indicadores actuales + CPU/RAM/GPU (hoy duplicados en la barra de estado).

### Fase 7 — Diálogos, accesibilidad y pulido (4-5 días)
* Unificar los 10 diálogos + ventanas de plugin con la plantilla `.dialog` de `Windows.axaml`.
* Accesibilidad: tamaños mínimos legibles (elevar los 15 usos de 9.5 px y 33 de 10 px), `AutomationProperties.Name` en botones de sólo icono, orden de tabulación, foco visible de 2 px con halo, y un preset de tema **Alto Contraste**.
* Pulido final: revisión de cada vista con capturas comparativas (headless render → PNG) para detectar desalineaciones.

---

## 6. Cambios por vista (mapa de trabajo)

| Vista | Trabajo principal |
| :--- | :--- |
| `MainWindow.axaml` | Shell, barra de título, rail, dock con pestañas, escrim y drawer temáticos, toast host |
| `ControlBarView.axaml` | Islas → botonera con jerarquía (primario/ secundario/ icono), estados de ejecución animados |
| `NodeToolboxView.axaml` | **Virtualización** (hoy `ItemsControl` dentro de `ScrollViewer` = render de todos los nodos), tarjetas con icono vectorial, favoritos, drag ghost |
| `EditorView.axaml` | HUD temático + i18n, empty state, spotlight rediseñado, minimapa, marco de selección |
| `NodeCardView.axaml` | Rediseño completo (§5 Fase 6); hoy 484 líneas con estilos inline y 15 hex |
| `NodeInspectorPanelView.axaml` + `Themes/Templates/*` | Secciones agrupadas, widgets Blender, cabecera fija, validación inline |
| `LogView.axaml` | Densidad conmutable, badges de nivel, resaltado de fila nueva, agrupación por nodo |
| `StatusBarView.axaml` | Reducir a 3 islas, evitar duplicar métricas del HUD |
| `SplashScreenWindow.axaml` | Temático + i18n, animación de entrada y de barra |
| 10 diálogos y ventanas | Plantilla `.dialog` unificada, i18n, foco |

---

## 7. Catálogo de microinteracciones

| Momento | Interacción | Notas técnicas (Avalonia 12) |
| :--- | :--- | :--- |
| Hover en botón | Elevación + tinte de acento | `Background`/`BorderBrush` con `Transitions` 120 ms |
| Click en botón | Compresión 0.97 y liberación | `RenderTransform` + `TransformOperationsTransition` |
| Hover en tarjeta de nodo | Elevación suave + borde que se ilumina | Capa de sombra pre-renderizada con `Opacity` animada (transicionar `BoxShadow` no es posible y dibujarlo por nodo es caro) |
| Nodo ejecutándose | Anillo/LED pulsante + barra de progreso con brillo | Ya existe patrón (`Animation`, `IterationCount=Infinite`) |
| Cables durante la ejecución | Flujo de energía (dash offset) + intensidad según throughput | Animar `StrokeDashOffset` |
| Arrastre de conexión | Pulso del cable pendiente + resaltado de puertos compatibles + dim del resto | Ya existe el pulso; añadir clases `.compatible`/`.dimmed` |
| Apertura de Spotlight/Paleta | Fade + escalado 0.96→1.0 (150 ms) | Clase `.open` + `Style.Animations` (o `TransitioningContentControl`) |
| Drawer lateral | Deslizamiento con easing de salida + escrim con fade | `TranslateTransform` + transición; foco atrapado |
| Cambio de pestaña del dock | Subrayado/píldora que se desliza + fade del contenido | Animación sobre el indicador |
| Nueva entrada en consola | Destello de fondo 600 ms + auto-scroll si está al final | Animación por clase en la fila recién insertada |
| Éxito/error de un flujo | Toast con icono vectorial, barra de progreso y desvanecimiento | `ToastHost` en el shell |
| Cambio de tema/idioma | Cross-fade del fondo de superficies (200 ms) | Evitar re-layout brusco |
| Carga de nodos/modelos | Esqueletos (skeleton) en toolbox/inspector | `Skeleton` con `Animation` de brillo |
| Arranque | Splash con progreso animado y transición de salida a la ventana | `CloseWithFadeAsync` ya existe |

Reglas: ninguna animación > 320 ms; nada animado durante la ejecución masiva del grafo salvo lo relacionado con el nodo activo; respetar "reducir movimiento".

---

## 8. Accesibilidad (checklist de aceptación)

* Contraste: texto de cuerpo ≥ 4.5:1; texto grande, iconos y contornos de foco ≥ 3:1; tokens `Text.Muted` corregidos (hoy `#64748B` sobre `#13151A` ≈ 3.4:1 → por debajo de AA).
* Ningún texto < 11 px efectivos para contenido informativo; telemetría secundaria mínimo 10.5 px con contraste AA.
* Foco visible en **todos** los controles interactivos y navegación completa por teclado del shell (rail, dock, toolbox, inspector).
* `AutomationProperties.Name` en botones de sólo icono (hoy decenas: `☰ ✕ ⚙ 🔍 ↶ ▶ ⏹ …`) y en LEDs de estado.
* No depender sólo del color para estados (breakpoint, logging, estado de nodo): añadir forma/icono/patrón.
* Preset de tema "Alto Contraste" + respeto de `RequestedThemeVariant` del SO cuando el usuario elige "Sistema".

---

## 9. Testing y criterios de aceptación

**Tests automatizados nuevos**
1. `ThemeTokenCompletenessTests`: cada clave usada con `DynamicResource` en el repo existe en los 4 presets built-in y en el diccionario generado.
2. `UiStyleLintTests`: falla si aparece `#HEX`, `CornerRadius="…"` o `FontSize="…"` en vistas (fuera de `Themes/` y `Styles/`).
3. `ThemeVariantPropagationTests` (Avalonia.Headless, ya disponible en `FileFlow.Tests`): cambio de tema → `RequestedThemeVariant` de `Application` y de ventanas hijas correcto; `TextMutedBrush` presente en todos los temas.
4. `IconResolverTests`: resolución de icono por tipo/categoría devuelve claves válidas y cubre el 100 % de nodos descubiertos por `PluginLoader`.
5. `MotionTokensTests`: las animaciones usan sólo duraciones de token (protege el modo "reducir movimiento").
6. Ampliar `EditorViewLayoutTests` con el nuevo árbol visual (HUD i18n, empty state, rail).
7. **Infraestructura headless y capturas de render (✅ implementado el 2026-09-15)**:
   - `HeadlessInfrastructureTests` (11 guardias): sesión única con Skia real y fotogramas capturables, arranque idempotente bajo concurrencia, despacho serializado entre hilos y **anidado sin interbloqueo**, estilos sin animaciones, recursos del host e idioma fijados, ventanas reales renderizables y **contrato de hilo** (construir controles o resolver tokens fuera del hilo de la sesión lanza un mensaje que dice qué hacer; la fábrica de una captura corre ya dentro del hilo correcto; la sesión decodifica imágenes).
   - `VisualRegressionTests` (galería de diseño en dos presets, tarjeta de nodo, Theme Studio y sondas de token —radio, elevación, botón primario, fondo por preset—) y **`AppShellVisualRegressionTests`** (ventana principal completa en claro y oscuro + los seis paneles por separado).
   - `FileFlow.Tests/VisualBaselines/` (12 imágenes, versionadas): comparación píxel a píxel con tolerancia acotada y mapa de diferencias en los fallos; **regeneración con `FILEFLOW_UPDATE_VISUALS=1`** y una línea base ausente se crea **fallando a propósito** para forzar la revisión humana.
   - **Aislamiento**: el suite corre **en paralelo por colecciones** (`TestAssemblyParallelism`) con el estado global de proceso confinado en colecciones no paralelizables (`DisableParallelization`): `VisualSnapshotsCollection` (sesión headless de Avalonia, tema y diccionario de recursos), `OnnxInferenceCollection` (clúster IA/ONNX y sesiones nativas), `AiModelDownloadSequentialCollection` (descargas reales de modelos) y `LocalizationCollection` (cultura, idioma y preferencias reales).

**Criterios de aceptación globales**
* 100 % de la suite en verde **con `dotnet test` sin filtros** (hoy 960/960 en ~30 s, clúster IA/ONNX incluido) y compilación con 0 advertencias (`TreatWarningsAsErrors`).
* Ningún cambio de diseño se acepta sin captura: cada vista que se rediseñe añade su superficie a `AppVisualFixture` y su línea base revisada.
* Ninguna vista de plugin o host con colores/tamaños literales.
* Cambio de tema correcto en las 11 ventanas + 4 presets + tema personalizado.
* Sin *tofu* de iconos en Linux (verificación manual en entorno Linux).
* Arrastre fluido con un grafo de ≥ 150 nodos.

---

## 10. Rendimiento

* Virtualizar el toolbox (`ItemsRepeater`/`VirtualizingStackPanel`; hoy sin virtualización) — mejora directa en arranque de panel con 100+ tipos de nodo.
* Sombras: sustituir `BoxShadow` por nodo por capas de sombra reutilizadas con opacidad animada; verificar si Nodify.Avalonia 2.0 permite virtualización del lienzo y activarla si existe.
* Evitar animaciones sobre propiedades que provoquen *layout* (usar `Opacity`/`RenderTransform`).
* Medir antes/después con el `SystemPerformanceMonitor` existente y documentar en `docs/PROJECT_WALKTHROUGH.md`.

---

## 11. Riesgos y mitigaciones

| Riesgo | Mitigación |
| :--- | :--- |
| Romper el aspecto de los diálogos de plugin al migrar a `FileFlow.Ui` | Migración incremental: el host primero, los 11 plugins después, con `PluginAssemblyLoadContext` actualizado y smoke test de arranque |
| Regresión en tests que asumen claves de tema | Mantener los nombres de token actuales; los tests de tema ya verifican `AppBackgroundBrush`/`AccentPrimaryBrush`/`ConnectionWireBrush` |
| Cambios en `ThemeDefinition` rompen temas personalizados guardados en disco | Añadir tokens con valores por defecto; no renombrar ni eliminar claves existentes (o migración en `CustomThemeService.LoadCustomThemes`) |
| Barra de título personalizada problemática en Linux/Wayland | Detección de plataforma con *fallback* a decoraciones nativas |
| Alcance enorme (31 vistas) | Fases con entregables verificables; migración vista a vista con capturas comparativas |
| Directiva "Zero-Touch en FileFlow.App" para plugins | La excepción de `FileFlow.Ui` se documenta en `AGENTS.md` y `rules.md` antes de tocar código de plugins |

---

## 12. Decisiones pendientes de aprobación

1. **¿Nuevo proyecto `FileFlow.Ui` compartido con plugins, o sistema de diseño sólo para el host?** (impacta a `AGENTS.md`, a `PluginAssemblyLoadContext` y al alcance).
2. **¿Dirección estética:** Studio Pro oscuro (propuesta §3) o apuesta más radical (glass/neón) con el lienzo como protagonista absoluto?
3. **¿Alcance del shell:** ¿se aprueba rediseñar layout/IA (rail + dock con pestañas + paleta de comandos) o sólo el aspecto visual en la primera iteración?

---

## 13. Orden recomendado de trabajo (primera iteración)

1. ~~Fase 0 completa (saneamiento + tests de guardia)~~ — ✅ completada el 2026-09-15.
2. ~~Fase 1 (tokens v2 + tipografía y Theme Studio funcional)~~ — ✅ completada el 2026-09-15; siguiente: bajar el trinquete de estilos en las 14 vistas restantes — *el tema ya se personaliza de verdad en toda la interfaz*.
3. ~~Fase 2 sobre `ControlBarView`, `StatusBarView`, `LogView`~~ — ✅ completada el 2026-09-15; siguiente: `MainWindow`, drawer/toolbox e inspector — *la UI se siente nueva sin tocar el lienzo*.
4. Fase 3 (iconos) y Fase 4 (movimiento) — *el salto de "moderno" a "atractivo"*.
5. Fase 6 (lienzo y nodos) — *el corazón visual del producto*.
6. Fases 5 y 7 (shell/IA y accesibilidad) — *consolidación y pulido*.

Cada fase termina con: suite al 100 %, `docs/PROJECT_WALKTHROUGH.md` actualizado, capturas antes/después y entrada en `.antigravity/knowledge/session_summary.md`.
