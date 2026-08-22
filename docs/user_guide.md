# Manual de Usuario y Guía de Operación - FileFlow Studio

Bienvenido a la **Guía de Usuario de FileFlow Studio**. Este documento está diseñado para guiar a usuarios finales, operadores de sistemas y administradores en la automatización de procesos de archivos sin requerir conocimientos de programación.

---

## 1. Visión General de la Interfaz

La interfaz principal de FileFlow Studio está dividida en **6 áreas funcionales**:

```
+-----------------------------------------------------------------------------------+
| 1. BARRA SUPERIOR DE CONTROL (Ejecutar, Pausa, Dry Run, Rollback, Presets, Ajustes)|
+-------------------+-------------------------------------------+-------------------+
|                   |                                           |                   |
| 2. CATÁLOGO DE    | 3. LIENZO INTERACTIVO DE NODOS (Nodify)   | 4. INSPECTOR DE   |
|    HERRAMIENTAS   |                                           |    PARÁMETROS Y   |
|    (TOOLBOX)      |    [Nodo Origen] ---> [Nodo Destino]      |    PROPIEDADES    |
|                   |                                           |                   |
+-------------------+-------------------------------------------+-------------------+
| 5. CONSOLA DE REGISTROS Y LOGS EN TIEMPO REAL (Filtros, Búsqueda, Exportar)      |
+-----------------------------------------------------------------------------------+
| 6. BARRA DE ESTADO (🧩 Nodos, 🔗 Conexiones, 🧠 RAM, 💻 CPU, 📁 Ruta Salida Global)|
+-----------------------------------------------------------------------------------+
```

1. **Barra Superior de Control:** Botones para Iniciar (`▶ Ejecutar`), Pausar (`⏸ Pausa`), Modo Prueba (`🧪 Modo Simulación / Dry Run`), Revertir Cambios (`↩ Rollback`), Abrir Ajustes Generales (`⚙ Ajustes`) y Gestor de Presets (`⚙ Presets`).
2. **Catálogo de Herramientas (Toolbox):** Panel lateral con el listado de 22 nodos clasificados por categorías, barra de búsqueda en tiempo real, filtro por favoritos (`⭐`) y modos de vista (Compacto/Detallado).
3. **Lienzo de Nodos (Canvas):** Área de trabajo visual para arrastrar, conectar y organizar nodos mediante cables interactivos.
4. **Inspector de Nodos:** Panel lateral derecho para ajustar los parámetros específicos del nodo seleccionado (rutas, plantillas, formatos, contraseñas).
5. **Consola de Registros (Log Console):** Consola rica formateada en tiempo real con códigos de color (`🔴 ERROR`, `🟠 WARN`, `🔵 INFO`), selección libre de texto y exportación a archivo.
6. **Barra de Estado:** Métricas en vivo del grafo (`🧩 Nodos`, `🔗 Conexiones`), consumo de recursos del sistema (`🧠 RAM`, `💻 CPU`) y acceso directo a la **Ruta de Salida Global**.

---

## 2. Catálogo de Nodos por Categorías

FileFlow Studio incluye **22 nodos de procesamiento** organizados en 6 categorías técnicas:

### 📁 2.1 Archivos y Disco (`FileSystem`)
- **`Escanear Carpeta` (`FolderSourceNode`):** Punto de entrada del flujo. Escanea un directorio filtrando por patrón (`*.jpg`, `*.pdf`, `*.*`) o en vivo (*Watch Folder*).
- **`Mover / Copiar` (`FileRelocatorNode`):** Copia o mueve archivos hacia un directorio destino con estrategias anti-colisión (Renombrar incremental, Sobrescribir, Omitir).
- **`Renombrar Archivo` (`AdvancedRenamerNode`):** Renombra archivos dinámicamente interpolando tokens (`{FileNameNoExt}_v1.{Extension}`).
- **`Guardar en Destino` (`DestinationSinkNode`):** Salida final de archivos hacia el directorio global o personalizado.
- **`Acción en Origen` (`OriginalFileActionNode`):** Define qué hacer con el archivo original tras procesarlo (Conservar, Eliminar, Mover a Cuarentena).
- **`Enviar a Papelera` (`SafeRecycleDeleteNode`):** Envía archivos o carpetas a la Papelera de Reciclaje de Windows mediante la API del Shell nativo (recuperables).
- **`Limpiar Carpetas` (`EmptyDirectoryCleanerNode`):** Elimina carpetas vacías o desiertas tras operaciones de limpieza.

### 📦 2.2 Compresión y Empaquetado (`Archives`)
- **`Descomprimir` (`SmartUnpackNode`):** Extrae archivos ZIP, 7Z, RAR, TAR, GZ inteligentemente. Soporta descompresión recursiva, eliminación de carpetas redundantes (*Clean Wrapper*) y listas de contraseñas.
- **`Comprimir ZIP / 7z` (`ArchiveCompressorNode`):** Empaqueta y comprime archivos o directorios en formatos ZIP, TAR, GZ o 7Z con algoritmos configurables (`Deflate`, `Store`, `LZMA`, `BZip2`).
- **`Filtrar Comprimidos` (`ArchiveFilterNode`):** Evalúa la integridad de un archivo comprimido separando archivos válidos de comprimidos dañados o corruptos.

### 🎬 2.3 Multimedia y Documentos (`MediaDocs`)
- **`Optimizar Imagen` (`ImageOptimizerNode`):** Recomprime y redimensiona imágenes (JPEG, PNG, WebP) conservando la calidad visual.
- **`Transcodificar Media` (`MediaTranscoderNode`):** Transcodifica archivos de audio y vídeo mediante presets (MP3, AAC, 1080p H.264, WebM, GIF animado) o comandos FFmpeg personalizados.
- **`Procesar Documento` (`DocumentProcessorNode`):** Inspecciona y procesa documentos (PDF, Word) extrayendo recuento de páginas y texto.

### 🏷️ 2.4 Metadatos e Integridad (`Metadata`)
- **`Inyectar Variable` (`VariableInjectorNode`):** Calcula e inyecta variables personalizadas en el contexto del archivo para nodos posteriores.
- **`Metadatos EXIF` (`ExifMetadataNode`):** Extrae metadatos fotográficos (`{ImageWidth}`, `{ImageHeight}`, `{Megapixels}`, `{Orientation}`).
- **`Calcular Hash` (`HashCalculatorNode`):** Calcula hashes criptográficos SHA-256, MD5 o SHA-1.
- **`Filtrar Duplicados` (`DeduplicationFilterNode`):** Compara hashes de contenido para detectar y separar archivos únicos de copias duplicadas.

### 🔀 2.5 Lógica y Control (`Logic`)
- **`Enrutador Switch` (`SwitchCaseNode`):** Bifurca el flujo según reglas lógicas por extensión, patrón de tamaño (`< 10 MB`, `10 MB..1 GB`) o coincidencia de texto.
- **`Filtro Condicional` (`ExpressionFilterNode`):** Evalúa expresiones lógicas booleanas filtrando elementos.
- **`Agrupar por Lotes` (`BatchBufferNode`):** Acumula archivos en memoria hasta alcanzar N elementos o MB antes de liberarlos juntos.
- **`Pausa / Throttle` (`ThrottleDelayNode`):** Modera la velocidad de procesamiento introduciendo pausas configurables.
- **`Barrera Fork & Join` (`ForkJoinBarrierNode`):** Bifurca un archivo hacia múltiples ramas paralelas y espera a que todas finalicen antes de continuar.

### ⚡ 2.6 Integraciones (`Integrations`)
- **`Ejecutar Comando CLI` (`CliExecutionNode`):** Lanza ejecutables externos (`cmd.exe`, PowerShell, Python, Node.js) inyectando tokens de metadatos.
- **`Enviar Webhook` (`WebhookNotificationNode`):** Envía peticiones HTTP POST con cargas JSON dinámicas a Discord, Slack, n8n o servidores propios.
- **`Registrar Log` (`LogOutputNode`):** Emite mensajes personalizados a la consola de registros.

---

## 3. Guía Paso a Paso: Cómo Crear Tu Primer Flujo

### Ejemplo: Convertir Imágenes a WebP y Organizar en Salida Global

1. **Insertar Nodo Origen:** Arrastra **`Escanear Carpeta`** desde el panel izquierdo (*Toolbox*) al lienzo. En el inspector, configura `SourcePath` como `{RelativeDir}\Input` y activa `WatchFolder = True`.
2. **Insertar Optimizador:** Arrastra **`Optimizar Imagen`** al lienzo. Conecta el puerto `Out` de *Escanear Carpeta* con el puerto `In` de *Optimizar Imagen*. En el inspector, elige `Format = WebP` y `Quality = 85`.
3. **Insertar Nodo Destino:** Arrastra **`Guardar en Destino`** al lienzo. Conecta `Out` de *Optimizar Imagen* con `In` de *Guardar en Destino*. Configura `DestinationRoot` como `{RelativeDir}\OptimizedImages`.
4. **Probar con Modo Simulación (`Dry Run`):** Presiona el botón **`🧪 Modo Prueba`** en la barra superior. Al presionar **`▶ Ejecutar`**, el sistema simulará la conversión y te mostrará exactamente qué acciones realizaría en el disco sin modificar ningún archivo real.
5. **Ejecución Real:** Desactiva el modo prueba y presiona **`▶ Ejecutar`**.

---

## 4. Funcionalidades Avanzadas

### 4.1 Gestor de Presets Multimedia (`⚙ Presets`)
Haz clic en el botón `⚙ Presets` en la barra superior o en la tarjeta del nodo `Transcodificar Media`. Se desplegará un gestor modal donde podrás:
- Seleccionar entre 10 presets de fábrica (MP3 192k, 1080p H.264, WebM VP9, GIF animado, etc.).
- Crear nuevos presets personalizados definiendo la extensión de salida y los argumentos de FFmpeg (`-c:v libx265 -crf 24`).

### 4.2 Gestor Modal de Contraseñas (`🔑 Claves`)
En el nodo `Descomprimir`, pulsa el botón `🔑 Claves` junto al parámetro `PasswordList`. Podrás escribir, importar y exportar listas de contraseñas candidatas (`.txt`). El nodo probará secuencialmente cada clave hasta desbloquear el archivo comprimido.

### 4.3 Sistema de Reversión de Cambios (`↩ Rollback`)
Si ejecutaste un flujo y deseas deshacer las operaciones físicas realizadas en el disco (como archivos movidos o eliminados a la papelera), presiona el botón **`↩ Rollback`** en la barra de herramientas. El motor ejecutará las transacciones inversas registradas en el `ExecutionJournalService`.

---

## 5. Catálogo de 40 Ejemplos Listos para Usar (`docs/examples/`)

FileFlow Studio incluye un catálogo de **40 plantillas de flujos ejecutables** en el directorio `docs/examples/`:

- **01_basic (Ejemplos 01 al 10):** Canales lineales simples, optimización WebP, extracción MP3, generación de hashes SHA-256.
- **02_intermediate (Ejemplos 11 al 20):** Filtrado condicional, bifurcación por extensión, EXIF, deduplicación, webhooks HTTP.
- **03_advanced (Ejemplos 21 al 30):** Lotes por tamaño, paralelismo *Fork & Join*, limitación de tasa (*Throttle*), políticas de reintento.
- **04_complex (Ejemplos 31 al 40):** Patrones *Scatter-Gather*, doble hash inmutable, ingesta masiva empresarial y fallback resiliente.

Para cargar cualquiera de estos ejemplos, dirígete al menú superior **Archivo > Importar Flujo (.json)** y selecciona el archivo en `docs/examples/`.

---

## 6. Resolución de Problemas (Troubleshooting & FAQ)

### ❓ FFmpeg no fue detectado en el sistema
- **Causa:** El ejecutable `ffmpeg.exe` no se encuentra en la variable `PATH` ni en rutas conocidas.
- **Solución:** Ve a **⚙ Ajustes > 🛠 Herramientas Externas > 🔍 Auto-Detectar Herramientas** o presiona `Examinar` y selecciona manualmente la ubicación de `ffmpeg.exe`.

### ❓ Los archivos de solo lectura no se procesan en carpetas supervisadas
- **Causa:** En versiones anteriores se requería acceso de escritura para verificar el estado de bloqueo.
- **Solución:** Asegúrate de estar usando la versión actualizada que realiza las comprobaciones con permiso de lectura (`FileAccess.Read`).

### ❓ Al seleccionar un preset en el ComboBox se restaura el texto anterior
- **Solución:** Se ha corregido el comportamiento ajustando `IsEditable="False"` en los ComboBoxes de presets del inspector. Todos los presets seleccionados se aplican al instante.
