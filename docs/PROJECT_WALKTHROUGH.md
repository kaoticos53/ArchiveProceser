# FileFlow Studio - Historial de Cambios y Registro de Implementación (Walkthrough)

Este documento registra cronológicamente todos los cambios, mejoras, correcciones y nuevas funcionalidades implementadas en el proyecto **FileFlow Studio**.

---

## [2026-08-20] - Inyección Multivariable en `VariableInjectorNode`

### 🛠 Cambios Implementados
1. **Soporte Multivariable en `VariableInjectorNode.cs`:**
   - Rediseño del nodo para permitir definir y resolver múltiples pares de variables (`Key1`/`Value1`, `Key2`/`Value2`, ..., `Key5`/`Value5`) de forma simultánea dentro del mismo nodo.
2. **Actualización de la Travesía Topológica `EditorViewModel.cs`:**
   - La travesía del grafo hacia atrás (*Upstream Traversal*) inspecciona todas las claves no vacías de `VariableInjectorNode` y las ofrece automáticamente en el menú desplegable **`[{x}]`** de los nodos conectados posteriormente.
3. **Actualización de Pruebas Unitarias (`VariableInjectorNodeTests.cs`):**
   - Cobertura completa de resolución e inyección simultánea de múltiples variables.

---

## [2026-08-20] - Suite Exhaustiva de Automatización de Pruebas (xUnit, FluentAssertions, Moq)

### 🛠 Cambios Implementados
1. **Nuevo Proyecto de Pruebas (`FileFlow.Tests/FileFlow.Tests.csproj`):**
   - Configurado en .NET 9 (`net9.0-windows` con `UseWPF=true`) e integrado en `FileFlow.slnx`.
   - Incluye **xUnit**, **FluentAssertions** y **Moq**.

2. **Tests Unitarios (`FileFlow.Tests/Unit/`):**
   - **`VariableTemplateResolverTests`:** Reemplazo de variables, funciones de fecha, transformaciones de texto (`Upper`, `Lower`, `PadLeft`), saneamiento de caracteres ilegales (`Sanitize`), cascadas `Coalesce` e interpolación dinámica.
   - **`LocalizationManagerTests`:** Verificación de singleton y disparo del evento `LanguageChanged`.
   - **`WorkflowExecutorTests`:** Ejecución topológica y validación de nodos.
   - **`FolderSourceNodeTests` & `VariableInjectorNodeTests`:** Inyección de metadatos (`Counter`, `SourceRootPath`, `CustomCategory`) e I/O.
   - **`EditorViewModelTests`:** Travesía topológica inversa (`GetUpstreamAvailableVariables`) y cálculo dinámico de variables en la interfaz.

3. **Tests de Integración (`FileFlow.Tests/Integration/`):**
   - **`WorkflowIntegrationTests`:** Flujo E2E desde `FolderSourceNode` $\rightarrow$ `VariableInjectorNode` $\rightarrow$ `DestinationSinkNode` validando la tubería completa de archivos reales.

4. **Tests de Estrés / Rendimiento (`FileFlow.Tests/Performance/`):**
   - **`PerformanceStressTests`:** Procesamiento de **10,000 elementos masivos** evaluando el motor de plantillas en menos de 1 segundo (651 ms).

---

## [2026-08-20] - Variables Avanzadas de Sistema, Metadatos Multimedia/Compresión y Funciones de Expresión

### 🛠 Cambios Implementados
1. **Nuevas Variables del Sistema y Ejecución (`VariableTemplateResolver.cs`):**
   - Incorporación de `{DateNow}` (`yyyy-MM-dd`), `{TimeNow}` (`HH-mm-ss`), `{DateTimeNow}` (`yyyy-MM-dd_HH-mm-ss`).
   - Contador incremental de secuencia por lote `{Counter}` / `{Index}` inyectado desde `FolderSourceNode.cs`.
   - Métricas de peso de archivo: `{SizeMB}`, `{SizeKB}`, `{SizeBytes}`.
   - Variables de entorno del sistema: `{UserName}`, `{MachineName}`.

2. **Nuevos Metadatos de Imagen y Archivos Comprimidos:**
   - **`ExifMetadataNode.cs`:** Extracción de `{ImageWidth}`, `{ImageHeight}`, `{Orientation}` (`Landscape`/`Portrait`/`Square`), `{AspectRatio}` (ej. `16:9`) y `{Megapixels}` (ej. `24.1MP`).
   - **`SmartUnpackNode.cs`:** Extracción de `{ArchiveFormat}` (`ZIP`/`7Z`/`RAR`) y `{UnpackedFileCount}`.

3. **Nuevas Funciones de Expresión Prácticas:**
   - `{Sanitize(text)}`: Limpieza automática de caracteres ilegales en Windows (`\ / : * ? " < > |`).
   - `{PadLeft(val, length, char)}`: Relleno de números con ceros u otros caracteres (ej. `{PadLeft(Counter, 4, "0")}`).
   - `{Substring(text, start, length)}`: Extracción segura de subcadenas sin desbordamientos de índice.
   - `{RegexMatch(text, pattern)}`: Extracción por expresión regular.
   - `{RegexReplace(text, pattern, replacement)}`: Reemplazo con expresiones regulares.
   - `{Coalesce(val1, val2, ...)}`: Evaluación en cascada retornando el primer valor no vacío.
   - `{FileAgeDays(dateStr)}`: Cálculo de antigüedad en días transcurridos.

4. **Integración en el Selector Gráfico `[{x}]` (`EditorViewModel.cs`):**
   - Actualización de `GetUpstreamAvailableVariables` clasificando y ofreciendo todas las nuevas variables y funciones ordenadas por grupos (`🌐 System & Environment`, `📷 Image & Media`, `📦 Archives`, `🔤 Expression Functions`).

---

## [2026-08-20] - Consola de Ejecución Seleccionable, Exportación y Categorización de Nodos

### 🛠 Cambios Implementados
1. **Exportación de Logs a Archivo:**
   - Se añadió la acción `ExportLogsCommand` en `LogViewModel.cs` y el botón **`💾 Exportar Log`** / **`💾 Export Log`** en la barra de herramientas de la consola (`LogView.xaml`).
   - Permite guardar todo el historial formateado con marcas de tiempo en archivos `.log` o `.txt` mediante `SaveFileDialog`.

2. **Selección y Copiado de Texto con el Ratón:**
   - Se reemplazó el control estático por un visor de texto editable/seleccionable (`TextBox` en modo `IsReadOnly="True"`) en `LogView.xaml`.
   - Los usuarios pueden arrastrar el ratón para seleccionar cualquier bloque de texto y copiarlo directamente con `Ctrl+C` o el menú contextual.
   - Implementado autodesplazamiento hacia la última línea recibida (`LogConsoleTextBox_TextChanged`).

3. **Corrección de Categoría del Nodo Inyector de Variables:**
   - Se actualizó `VariableInjectorNode.cs` asignándole la categoría **`Utility`** (*Utilidades*).
   - Ahora aparece correctamente clasificado junto a herramientas como `Log Inspector` dentro del panel lateral de herramientas (*Toolbox*).

---

## [2026-08-20] - Subsistema de Variables Dinámicas, Motor de Expresiones e Inyector de Variables

### 🛠 Cambios Implementados
1. **Motor de Plantillas de Variables (`VariableTemplateResolver.cs`):**
   - Interpolación de tokens dinámicos en cualquier parámetro de texto o ruta de nodo.
   - **Variables del Sistema Estandarizadas al Inglés:** `{FileName}`, `{FileNameNoExt}`, `{Extension}`, `{CurrentPath}`, `{OriginalPath}`, `{CurrentDir}`, `{OriginalDir}`, `{RelativePath}`.
   - **Funciones de Expresión Integradas:**
     - **Fechas:** `{Year(date)}`, `{Month(date)}`, `{Day(date)}`, `{FormatDate(date, "yyyy-MM")}`.
     - **Texto:** `{Upper(text)}`, `{Lower(text)}`, `{Trim(text)}`, `{Replace(text, "old", "new")}`, `{Default(val, "fallback")}`.

2. **Cálculo de `RelativePath` (Estructura de Directorio Relativo):**
   - Se actualizó `FolderSourceNode.cs` para adjuntar la metadata `SourceRootPath` al escanear directorios.
   - `VariableTemplateResolver` calcula la ruta de subcarpetas relativa exacta (ej. `mami/antiguo`) excluyendo el nombre del archivo.

3. **Nodo `Inyector de Variables` (`VariableInjectorNode.cs`):**
   - Permite calcular e inyectar claves de metadatos personalizadas (`item.Metadata["VariableName"] = ResolvedValue`) en el flujo para nodos posteriores.

4. **Selector Gráfico de Variables `{x}` con Travesía Topológica Inversa:**
   - Se agregó el botón gráfico **`[{x}]`** al lado de los campos de entrada de parámetros en `EditorView.xaml`.
   - Al pulsar **`[{x}]`**, `EditorViewModel.GetUpstreamAvailableVariables` recorre el grafo hacia atrás (*Upstream Traversal*) para ofrecer únicamente las variables exportadas por los nodos conectados previamente (EXIF, origen de descompresión, variables inyectadas).

---

## [2026-08-20] - Sistema de Internacionalización Multilingüe (Español / Inglés)

### 🛠 Cambios Implementados
1. **Gestor de Localización Estándar de .NET (`LocalizationManager.cs`):**
   - Singleton encargado de administrar el cambio de cultura al vuelo (`CultureInfo`) entre Español (`es-ES`) e Inglés (`en-US`).
   - Notificación de cambio mediante el evento `LanguageChanged` y propiedad indexadora `this[string key]`.

2. **Ficheros de Recursos `.resx`:**
   - Creación de `Strings.resx` (Inglés por defecto) y `Strings.es.resx` (Español).

3. **Selector Dinámico de Idioma en Barra de Control:**
   - Desplegable en `ControlBarView.xaml` reactivo en tiempo real para traducir al instante la UI, los títulos de los nodos en lienzo, las descripciones y el catálogo de herramientas.

---

## [2026-08-20] - Descubrimiento y Carga Robusta de Plugins

### 🛠 Cambios Implementados
1. **Contexto de Carga Desacoplado (`PluginAssemblyLoadContext.cs`):**
   - Carga de ensamblados `.dll` en memoria para evitar bloqueos del sistema operativo sobre los archivos en disco.
   - Búsqueda de dependencias de plugins con fallback a `AppDomain.CurrentDomain.BaseDirectory` para librerías como `SixLabors.ImageSharp`, `MetadataExtractor` y `SharpCompress`.

---

## [2026-08-20] - Estructura de Documentación y Repositorio Git Initial

### 🛠 Cambios Implementados
1. **Repositorio Git (`.gitignore` & `git init`):**
   - Configuración de exclusión de binarios `bin/`, `obj/` y archivos de cache `.antigravity/`.
   - Registro del commit inicial en la rama `main`.

2. **Documentación del Proyecto (`docs/`):**
   - `docs/README.md`: Centro de documentación y visión general del proyecto.
   - `docs/nodes/CREATING_NODES.md`: Guía de desarrollo de nodos personalizados.
   - `docs/nodes/examples/SampleMultiPortNode.cs`: Ejemplo de nodo de código completo para desarrolladores.
   - `docs/ARCHITECTURE_DEEP_DIVE.md`: Guía arquitectónica detallada en 4 niveles de complejidad.
