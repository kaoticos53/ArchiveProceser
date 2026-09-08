# 📖 Manual de Usuario y Guía de Referencia de Nodos
## **FileFlow Studio v2.0**
*Plataforma Modular de Automatización, Procesamiento Masivo y Transformación de Archivos basada en Grafos DAG*
*Runtime .NET 9 | C# 13 | Licencia GNU GPLv3 | Copyright © 2026 RGLara*

---

## 📑 Tabla de Contenidos

1. [Introducción y Filosofía de Diseño](#1-introducción-y-filosofía-de-diseño)
2. [Conceptos Fundamentales del Editor Visual](#2-conceptos-fundamentales-del-editor-visual)
   - [Lienzo de Nodos Interactivo (Nodify)](#lienzo-de-nodos-interactivo-nodify)
   - [El Contexto del Archivo (`FileItemContext`)](#el-contexto-del-archivo-fileitemcontext)
   - [Sub-flujos y Macros Multinivel (Breadcrumbs)](#sub-flujos-y-macros-multinivel-breadcrumbs)
   - [Telemetría Reactiva en Conexiones](#telemetría-reactiva-en-conexiones)
   - [Visor Rápido QuickLook e Inspector](#visor-rápido-quicklook-e-inspector)
3. [Modos de Ejecución y Seguridad de Datos](#3-modos-de-ejecución-y-seguridad-de-datos)
   - [Ejecución Normal en Paralelo](#ejecución-normal-en-paralelo)
   - [Modo Simulación Virtual ("Dry Run")](#modo-simulación-virtual-dry-run)
   - [Modo Monitorización Continua (Watchdog)](#modo-monitorización-continua-watchdog)
   - [Sistema de Rollback Transaccional (LIFO)](#sistema-de-rollback-transaccional-lifo)
   - [Depuración Interactiva con Puntos de Interrupción (Breakpoints)](#depuración-interactiva-con-puntos-de-interrupción-breakpoints)
   - [Sistema de Archivos Virtual (VFS) y Banco de Pruebas No Destructivo](#sistema-de-archivos-virtual-vfs-y-banco-de-pruebas-no-destructivo)
   - [Diseñador Visual de Conjuntos de Datos Sintéticos](#diseñador-visual-de-conjuntos-de-datos-sintéticos-syntheticdatasetdesignerwindow)
   - [Simulación Híbrida de Archivos Comprimidos](#simulación-híbrida-de-archivos-comprimidos-zip-rar-7z)
4. [Motor de Tokens y Variables Dinámicas](#4-motor-de-tokens-y-variables-dinámicas)
   - [Sintaxis y Dominios](#sintaxis-y-dominios)
   - [Tabla Completa de Tokens](#tabla-completa-de-tokens)
5. [Catálogo Exhaustivo de Nodos (58 Nodos DAG)](#5-catálogo-exhaustivo-de-nodos-58-nodos-dag)
   - [📁 Categoría 1: FileSystem (E/S de Disco y Ciclo de Vida)](#-categoría-1-filesystem-15-nodos)
   - [🗜️ Categoría 2: Archives (Compresión y Desempaquetado)](#️-categoría-2-archives-3-nodos)
   - [🖼️ Categoría 3: Images (Procesamiento Gráfico y EXIF)](#️-categoría-3-images-4-nodos)
   - [🌐 Categoría 4: Network & Remote Storage (Hubs Multi-Protocolo)](#-categoría-4-network--remote-storage-2-nodos-unificados)
   - [🤖 Categoría 5: AI & Machine Learning (Inferencia Local ONNX)](#-categoría-5-ai--machine-learning-8-nodos)
   - [📄 Categoría 6: Documents & PDFs (Gestión y Extracción)](#-categoría-6-documents--pdfs-4-nodos)
   - [📊 Categoría 7: Data & Tabular Files (Excel, CSV, SQLite)](#-categoría-7-data--tabular-files-3-nodos)
   - [⚙️ Categoría 8: Logic & Control Flow (Ruteo y Sincronización)](#️-categoría-8-logic--control-flow-6-nodos)
   - [🔐 Categoría 9: Hashing & Security (Criptografía y Duplicados)](#-categoría-9-hashing--security-3-nodos)
   - [📜 Categoría 10: Scripting & Extensibility (C# Roslyn & JS)](#-categoría-10-scripting--extensibility-3-nodos)
   - [🔌 Categoría 11: Integrations & CLI (Herramientas Externas)](#-categoría-11-integrations--cli-5-nodos)
6. [Tutoriales Prácticos Paso a Paso](#6-tutoriales-prácticos-paso-a-paso)
   - [Tutorial A: Organización y Optimización Automatizada de Fotografías](#tutorial-a-organización-y-optimización-automatizada-de-fotografías)
   - [Tutorial B: Ingesta Remota SFTP, Extracción y Reporte Consolidado](#tutorial-b-ingesta-remota-sftp-extracción-y-reporte-consolidado)
   - [Tutorial C: Pipeline de Inteligencia Artificial con OCR y Anonimización](#tutorial-c-pipeline-de-inteligencia-artificial-con-ocr-y-anonimización)
7. [Atajos de Teclado y Productividad](#7-atajos-de-teclado-y-productividad)

---

## 1. Introducción y Filosofía de Diseño

**FileFlow Studio** es un entorno de ingeniería visual y orquestación de procesamiento de archivos por lotes inspirado en herramientas de vanguardia como *n8n*, *ComfyUI* y *Node-RED*, diseñado específicamente para aprovechar la potencia de **.NET 9** y **C# 13**.

### Pilares Fundamentales:
- **🛡️ Inmutabilidad y Seguridad por Defecto**: Los flujos son no destructivos. Los archivos originales (`OriginalPath`) jamás se modifican ni se eliminan a menos que se configure explícitamente el nodo `OriginalFileActionNode`.
- **🧩 Arquitectura de Microkernel Desacoplado (ADR-006)**: Cada funcionalidad reside en un plugin autónomo (`FileFlow.Plugin.*`) con cero dependencias hacia la interfaz gráfica y con recursos de localización co-ubicados.
- **⚡ Rendimiento Asíncrono Concurrente**: Despacho paralelo multihilo impulsado por `System.Threading.Channels` y `TPL Dataflow`, superando los **82.000 eventos de telemetría/segundo**.
- **🌐 Simetría e Inteligencia en Red**: Nodos maestros universales para transferencias remotas (`HTTP`, `FTP`, `SFTP`, `WebDAV`, `SMB`) con parámetros dinámicos contextuales.

---

## 2. Conceptos Fundamentales del Editor Visual

### Lienzo de Nodos Interactivo (Nodify)
El lienzo visual permite modelar tuberías de trabajo arrastrando nodos desde la **Caja de Herramientas (Toolbox)**:
- **Puertos de Entrada (Izquierda)**: Reciben archivos entrantes (`In`, `BranchA`, `Files`).
- **Puertos de Salida (Derecha)**: Emiten elementos procesados o bifurcaciones condicionales (`Out`, `Done`, `Error`, `Matched`, `Unmatched`).
- **Indicador LED de Estado**:
  - ⚪ *Gris (Inactivo)*: En espera.
  - 🔵 *Azul Pulsante (En Ejecución)*: Procesando elementos en tiempo real.
  - 🟢 *Verde (Completado)*: Finalizado exitosamente.
  - 🔴 *Rojo (Error)*: Excepción capturada (desviada al puerto de error sin interrumpir el flujo general).
- **Punto de Interrupción (Breakpoint)**: Clic en el círculo superior para pausar la ejecución al llegar un archivo.

### El Contexto del Archivo (`FileItemContext`)
La unidad fundamental de datos que fluye por las conexiones del grafo:
```csharp
public sealed record FileItemContext
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CurrentPath { get; set; }       // Ruta actual en el paso del pipeline
    public string OriginalPath { get; init; }      // Ruta inmutable de origen
    public long FileSizeBytes { get; set; }        // Tamaño exacto en bytes
    public bool IsDirectory { get; set; }
    public Dictionary<string, object?> Metadata { get; } // Metadatos enriquecidos (EXIF, Hash, OCR, AI)
    public HashSet<string> Tags { get; }          // Etiquetas de clasificación rápida
    public List<string> ExecutionLog { get; }     // Historial de auditoría
}
```

### Sub-flujos y Macros Multinivel (Breadcrumbs)
Encapsula sub-grafos complejos dentro de nodos compuestos:
1. Haz doble clic en un nodo de sub-flujo para abrir su editor interno.
2. La barra superior de migas de pan (*Breadcrumbs*) te permite navegar entre niveles jerárquicos: `Flujo Principal ❯ Macro Extracción ❯ Normalización`.
3. Al pulsar un nivel superior, el sub-flujo se valida y vuelve al lienzo padre.

### Telemetría Reactiva en Conexiones
Cada conexión física entre nodos cuenta con un indicador numérico en tiempo real (ej. `⚡ 2,450`) que informa cuántos archivos han atravesado ese enlace, facilitando el diagnóstico visual instantáneo.

### Visor Rápido QuickLook e Inspector
- Pulsa la tecla `Espacio` o el botón `👁️ QuickLook` en cualquier nodo o elemento del registro para previsualizar instantáneamente imágenes, texto, PDFs o tablas de metadatos.
- El **Inspector Lateral** expone todos los parámetros del nodo seleccionado con controles enriquecidos (selectores de archivo, sliders, checkboxes y desplegables reactivos).

---

## 3. Modos de Ejecución y Seguridad de Datos

### Ejecución Normal en Paralelo
Pulsa **"▶ Ejecutar Flujo"** (`F5`). El motor orquesta el despacho a través de canales de alto rendimiento particionados por hardware y tipo de disco.

### Modo Simulación Virtual ("Dry Run")
1. Activa la casilla **"Dry Run"** antes de ejecutar.
2. El motor evalúa todos los cálculos de rutas, condiciones, renombrados y consultas a bases de datos **sin realizar modificaciones reales en disco**.
3. Consulta el diario de acciones planificadas en la consola para auditar el resultado antes de la ejecución real.

### Modo Monitorización Continua (Watchdog)
- Activa el modo centinela pulsando **"👁️ Modo Vigilante"**.
- El sistema monitorizará automáticamente las carpetas de origen configuradas y disparará el procesamiento en cuanto se detecten nuevos archivos en disco.

### Sistema de Rollback Transaccional (LIFO)
Si necesitas deshacer una ejecución:
1. Pulsa **"↩ Deshacer"** en la barra superior.
2. El sistema revierte todas las operaciones de renombrado y movimiento en orden inverso (último en ejecutarse, primero en restaurarse).

### Depuración Interactiva con Puntos de Interrupción (Breakpoints)
- Pulsa **"🐛 Depurar"**.
- Al alcanzar un nodo con breakpoint activo, la ejecución se pausará.
- Usa **"Paso a Paso (F10)"** para inspeccionar las transformaciones de metadatos nodo a nodo.

### Sistema de Archivos Virtual (VFS) y Banco de Pruebas No Destructivo
Para diseñar, validar y depurar tuberías complejas sin manipular discos físicos ni arriesgar archivos reales:
1. **Activación Automática**:
   - Al colocar el nodo **`SyntheticDataSourceNode`** o marcar elementos virtuales (`IsVirtual = true`), el motor DAG (`WorkflowExecutor`) activa en memoria un almacén aislado de sistema de archivos virtual (`IVirtualFileSystemStore`).
   - Los nodos de destino y reorganización (**`DestinationSinkNode`**, **`FileRelocatorNode`**, **`SafeRecycleDeleteNode`**, **`OriginalFileActionNode`**) detectan el entorno virtual y redirigen automáticamente sus escrituras, copias, movimientos y borrados lógicos hacia el VFS sin arrojar errores de I/O ni ensuciar carpetas reales.
2. **Explorador Visual VFS (`VirtualFileSystemExplorerWindow`)**:
   - Tras completar una ejecución con datos virtuales, la barra superior muestra el botón reactivo **`🗂️ VFS (N)`** informando del total de archivos generados. También se accede permanentemente desde el Drawer lateral.
   - **Vista Dividida en 3 Columnas**:
     - *Árbol de Directorios*: Estructura jerárquica reactiva de carpetas creadas en memoria.
     - *Tabla de Archivos*: Listado detallado con badges visuales de operación (`Guardado`, `Copiado`, `Movido`, `Renombrado con Conflicto`, `Reciclado`, `Eliminado`).
     - *Inspector de Metadatos*: Panel derecho categorizado por dominios (Fotografía/EXIF, Música/Audio ID3, Cine/Vídeo, Documentos/Fiscal y Sumas Criptográficas SHA/MD5).
   - **Herramientas de Exportación**:
     - `📋 Copiar Árbol`: Genera un diagrama jerárquico ASCII formateado al portapapeles.
     - `📂 Abrir en Explorador`: Materializa el estado del VFS en una carpeta temporal segura (`%TEMP%/FileFlow_VFS_Sandbox/...`) y la abre en el Explorador de archivos de Windows.

### Diseñador Visual de Conjuntos de Datos Sintéticos (`SyntheticDataSetDesignerWindow`)
Permite al usuario crear, editar, guardar y reutilizar bancos de pruebas personalizados con estructuras de carpetas a medida:
- **Acceso Directo**:
  1. Botón `🎨 Diseñar Conjuntos de Datos...` en el inspector del nodo `SyntheticDataSourceNode`.
  2. Botón `📊 Diseñador...` en la barra de muestras del Estudio de Renombrado (`AdvancedRenamerEditorWindow`).
  3. Opción `📊 Diseñador de Datos Sintéticos` en el Drawer lateral de la ventana principal.
- **Catálogo de Datasets**:
  - Buscador reactivo por nombre y categoría.
  - Métricas instantáneas de elementos, carpetas y archivos comprimidos.
  - Creación (`➕ Nuevo`), Duplicación (`📄 Duplicar`) y Eliminación (`🗑️ Eliminar`) con protección de los datasets base incorporados (`IsBuiltIn`).
  - Persistencia thread-safe en `%AppData%/FileFlow/SyntheticDataSets/*.json`.
- **Tres Modos de Edición Sincronizados**:
  - **📋 Tabla Visual**: Edición de rutas relativas (`RelativePath`), tamaños en bytes, flags `Es Directorio` y `Es Comprimido`, y diccionario de metadatos.
  - **🌲 Árbol Rápido (DSL)**: Editor textual con parser jerárquico que interpreta niveles de carpetas por sangría/tabulaciones, tamaños legibles y metadatos. Pulsa `🔄 Aplicar Cambios` para sincronizar.
  - **📄 JSON Puro**: Edición masiva o importación/exportación de la estructura serializada.

#### Sintaxis del Lenguaje de Árbol Rápido (DSL):
```dsl
# Declaración de estructura jerárquica con metadatos y comprimidos
Fotos/
    2026/
        playa.jpg (3.5MB, Exif:CameraModel=Sony A7 IV, Exif:ISO=100)
        vacaciones.zip (15MB) [archive: ruta.gpx (12KB); diario.txt (5KB)]
Documentos/
    Facturas/
        Factura_001.pdf (250KB, Doc:Author=Contabilidad, FiscalYear=2026)
Musica/
    Daft Punk/
        Discovery/
            01 - One More Time.flac (35MB, Audio:Artist=Daft Punk, Audio:Track=01)
```
- **Carpetas**: Terminan en `/` o no tienen extensión.
- **Tamaños**: Declarados entre paréntesis `(500B)`, `(15KB)`, `(3.5MB)`, `(1.2GB)`.
- **Metadatos**: Declarados como pares `Clave=Valor` dentro del paréntesis: `(2MB, Audio:Artist=Queen)`.
- **Contenidos de Archivos Comprimidos**: Declarados al final de la línea mediante `[archive: inner1.txt (500B); inner2.png (2MB)]`.

### Simulación Híbrida de Archivos Comprimidos (ZIP, RAR, 7Z)
- **Extracción Virtual Directa (VFS)**: Cuando un archivo simulado fluye hacia **`SmartUnpackNode`**, el nodo detecta que es virtual o contiene `Archive:Entries` y extrae directamente cada una de sus entradas internas en el `IVirtualFileSystemStore` recreando su jerarquía y asignando sus metadatos individuales, sin acceder al disco.
- **Generación Real en Modo `PhysicalMock`**: Si `SyntheticDataSourceNode` se configura en modo físico, empaqueta automáticamente un archivo `.zip` real y ligero mediante `System.IO.Compression` con los ficheros simulados en su interior, permitiendo probar utilidades de descompresión físicas y herramientas externas.

---

## 4. Motor de Tokens y Variables Dinámicas

El motor de plantillas `VariableTemplateResolver` permite parametrizar rutas, nombres de archivo y comandos externos.

### Sintaxis y Dominios
`{Dominio:Clave:Modificador}` o `{Variable}`

### Tabla Completa de Tokens

| Token | Ejemplo de Salida | Descripción |
| :--- | :--- | :--- |
| `{FileName}` | `informe.pdf` | Nombre completo del archivo con extensión |
| `{FileNameNoExt}` | `informe` | Nombre del archivo sin extensión |
| `{Ext}` | `pdf` | Extensión en minúsculas (sin punto) |
| `{ParentDir}` | `Facturas_2026` | Nombre del directorio contenedor |
| `{CreationDate:yyyyMMdd}` | `20260903` | Fecha de creación formateada |
| `{ModifiedDate:yyyy-MM-dd}`| `2026-09-03` | Fecha de última modificación |
| `{Now:yyyyMMdd_HHmmss}` | `20260903_205000` | Marca de tiempo actual del sistema |
| `{FileSize:MB}` | `14.50` | Tamaño en Megabytes formateado |
| `{FileSize:KB}` | `14848.0` | Tamaño en Kilobytes |
| `{Hash:SHA256}` | `e3b0c44298...` | Suma de comprobación SHA-256 completa |
| `{Hash:SHA256:8}` | `e3b0c442` | Hash SHA-256 truncado a 8 caracteres |
| `{Hash:MD5:6}` | `d41d8c` | Hash MD5 truncado a 6 caracteres |
| `{Exif:CameraModel}` | `Nikon Z8` | Modelo de cámara desde metadatos EXIF |
| `{Exif:DateTimeOriginal}` | `2026:08:15 14:20:00` | Fecha y hora original de la captura |
| `{Ocr:Text}` | `Factura N° 1024` | Texto extraído mediante OCR |
| `{Env:USERPROFILE}` | `C:\Users\Usuario` | Variable de entorno del sistema operativo |
| `{Meta:MiClave}` | `ValorPersonalizado`| Metadato inyectado por nodos previos |

---

## 5. Catálogo Exhaustivo de Nodos (57 Nodos DAG)

---

### 📁 Categoría 1: FileSystem (15 Nodos)

1. **`FolderSourceNode`**: Inicia el pipeline escaneando directorios con filtros por extensión, recursividad y soporte de monitorización reactiva en tiempo real.
2. **`DestinationSinkNode`**: Receptor final de archivos con estrategias de resolución de colisiones (`Overwrite`, `Skip`, `RenameIncremental`) y soporte no destructivo transparente para el VFS.
3. **`AdvancedRenamerNode`**: Renombrado avanzado con plantillas dinámicas de tokens, sanitización de caracteres y previsualización.
4. **`FileRelocatorNode`**: Mueve, copia o crea enlaces duros hacia rutas calculadas con validación opcional SHA-256 y redirección VFS en pruebas.
5. **`SafeRecycleDeleteNode`**: Eliminación segura enviando los archivos a la Papelera de reciclaje de Windows mediante `SHFileOperationW` (o borrado lógico en VFS).
6. **`OriginalFileActionNode`**: Controla el ciclo de vida del archivo original (`Keep`, `MoveToRecycleBin`, `MoveToQuarantine`).
7. **`OperationReportNode`**: Genera informes interactivos multi-formato (`HTML`, `Markdown`, `Text`, `JSON`, `CSV`) con trazabilidad completa.
8. **`DirectoryInspectorNode`**: Clasifica carpetas según su contenido estructural (comprimido único vs. archivos mixtos).
9. **`EmptyDirectoryCleanerNode`**: Limpieza determinista de árboles de directorios vacíos tras operaciones de movimiento.
10. **`DocumentProcessorNode`**: Extracción unificada de metadatos en documentos (`.pdf`, `.docx`, `.txt`, `.csv`, `.json`).
11. **`VariableInjectorNode`**: Inyecta variables personalizadas y metadatos calculados en el contexto del archivo.
12. **`LogOutputNode`**: Emite trazas enriquecidas y personalizadas a la consola de ejecución.
13. **`FileAttributeNode`**: Modifica atributos de archivo del sistema (Lectura, Oculto, Temporal, Timestamps).
14. **`PathSplitterNode`**: Descompone la ruta en partes individuales inyectándolas como variables independientes.
15. **`SyntheticDataSourceNode`**: Ingesta de prueba y banco de datos sintéticos hiperrealistas. Permite simular colecciones completas (Películas, Series, Música con ID3, Fotos con EXIF, Documentos/Facturas o Datasets personalizados del usuario) en memoria (`Virtual` sobre VFS) o en disco temporal (`PhysicalMock`), soportando jerarquías de carpetas intermedias (`EmitDirectories`), pausas regulables de emisión (`EmissionDelayMs`) y simulación híbrida de archivos comprimidos con desempaquetado virtual inmediato en `SmartUnpackNode`. Dispone de un botón de acción personalizada para invocar el **Diseñador Visual de Conjuntos de Datos Sintéticos**.

---

### 🗜️ Categoría 2: Archives (3 Nodos)

1. **`SmartUnpackNode`**: Descompresión universal (ZIP, RAR, 7Z, TAR, GZ) con aplanado de carpetas redundantes y protección anti *Zip Slip*.
2. **`ArchiveCompressorNode`**: Empaqueta y comprime archivos individuales o lotes en formatos ZIP, 7Z, TAR o GZ con nivel de compresión configurable.
3. **`ArchiveFilterNode`**: Detecta y procesa exclusivamente la primera parte de archivos divididos multivolumen (`.part1.rar`, `.z01`).

---

### 🖼️ Categoría 3: Images (4 Nodos)

1. **`ImageOptimizerNode`**: Optimiza, redimensiona y convierte imágenes a WebP, JPEG o PNG calculando el porcentaje exacto de ahorro de bytes.
2. **`ExifMetadataNode`**: Extrae metadatos EXIF de cámaras (fabricante, modelo, coordenadas GPS, fecha de captura).
3. **`ImageWatermarkNode`**: Aplica marcas de agua visuales de texto o logotipo con opacidad y posición configurable.
4. **`ImageMetadataStripperNode`**: Elimina metadatos privados e información GPS para garantizar la privacidad antes de compartir imágenes.

---

### 🌐 Categoría 4: Network & Remote Storage (2 Nodos Unificados)

1. **`NetworkDownloadNode`** *(Hub Universal de Descarga)*:
   - Soporta 5 protocolos simétricos: **HTTP/HTTPS**, **FTP/FTPS**, **SFTP (SSH)**, **WebDAV (Nextcloud/ownCloud)** y **SMB (Red Local/NAS)**.
   - Parámetros dinámicos contextuales que se adaptan en tiempo real según el protocolo seleccionado.
2. **`NetworkUploadNode`** *(Hub Universal de Subida y Transferencia)*:
   - Soporta 5 protocolos simétricos: **HTTP POST/PUT**, **FTP/FTPS**, **SFTP (SSH)**, **WebDAV** y **SMB**.
   - Transferencias seguras con reintentos automáticos, autenticación por contraseña o claves privadas SSH y creación remota de directorios.

---

### 🤖 Categoría 5: AI & Machine Learning (8 Nodos)

1. **`SmartImageClassifierNode`**: Clasifica imágenes sin conexión mediante modelos ONNX locales (ej. ResNet, MobileNet).
2. **`PromptObjectDetectorNode`**: Detección de objetos guiada por texto mediante YOLO-World o Grounding DINO en ONNX.
3. **`LocalOcrNode`**: Reconocimiento óptico de caracteres local para extraer texto de imágenes y facturas escaneadas.
4. **`WhisperAudioTranscriberNode`**: Transcripción automática de voz a texto para archivos de audio y video mediante Whisper local.
5. **`FaceDetectorNode`**: Detecta rostros en fotografías inyectando coordenadas de bounding boxes y conteo total.
6. **`ZeroShotSemanticSearchNode`**: Clasificación semántica sin entrenamiento previo basada en similitud de texto.
7. **`PiiAnonymizerNode`**: Detección y anonimización de datos personales (DNI, tarjetas, nombres, emails) mediante modelos NER.
8. **`SuperResolutionUpscalerNode`**: Escalado y mejora de resolución de imágenes mediante redes neuronales convolucionales.

---

### 📄 Categoría 6: Documents & PDFs (4 Nodos)

1. **`PdfMergeNode`**: Fusiona múltiples archivos PDF en un único documento maestro consolidado.
2. **`PdfSplitNode`**: Divide documentos PDF en páginas individuales o por rangos especificados.
3. **`PdfTextExtractorNode`**: Extrae el contenido de texto completo de documentos PDF mediante `PdfPig`.
4. **`PdfMetadataNode`**: Inspecciona y actualiza metadatos estándar de documentos PDF (Título, Autor, Palabras Clave).

---

### 📊 Categoría 7: Data & Tabular Files (3 Nodos)

1. **`ExcelReaderNode`**: Lector de hojas de cálculo Excel (`.xlsx`, `.xls`) de ultra-alto rendimiento en streaming con `MiniExcel`.
2. **`CsvProcessorNode`**: Ingesta, procesado y conversión avanzada de archivos delimitados CSV/TSV.
3. **`DataLookupNode`**: Cruce relacional de datos $O(1)$ en memoria contra tablas maestras de referencia.

---

### ⚙️ Categoría 8: Logic & Control Flow (6 Nodos)

1. **`SwitchCaseNode`**: Enrutador condicional multidireccional basado en reglas de coincidencia de extensiones o metadatos.
2. **`ExpressionFilterNode`**: Filtro booleano con operadores lógicos (`Equal`, `Contains`, `GreaterThan`, `RegexMatch`).
3. **`BatchBufferNode`**: Acumula elementos en memoria hasta alcanzar un tamaño de lote o límite de tiempo.
4. **`ThrottleDelayNode`**: Controla la tasa de emisión introduciendo pausas para evitar la saturación de I/O o APIs remotas.
5. **`ForkJoinBarrierNode`**: Sincroniza ramas paralelas de procesamiento esperando a que todas culminen antes de continuar.
6. **`VariableInjectorNode`**: Inyecta y calcula variables personalizadas en el flujo.

---

### 🔐 Categoría 9: Hashing & Security (3 Nodos)

1. **`HashCalculatorNode`**: Calcula sumas criptográficas (SHA-256, SHA-512, MD5, SHA-1, xxHash).
2. **`DeduplicationFilterNode`**: Filtra y desvía archivos duplicados en tiempo real comparando sus firmas hash en memoria.
3. **`ChecksumVerifierNode`**: Valida archivos contra sumas de verificación provistas en archivos `.sha256` o metadatos.

---

### 📜 Categoría 10: Scripting & Extensibility (3 Nodos)

1. **`CustomScriptNode`**: Ejecución de código a medida con soporte dual para **C# (Roslyn JIT)** y **JavaScript (Jint sandbox)**.
2. **`ScriptStudio`**: Entorno integrado de desarrollo con resaltado sintáctico, plantillas `.ffscript` y pruebas en vivo.
3. **`PythonScriptNode`**: Integración con entornos de ejecución Python externos para procesamiento avanzado.

---

### 🔌 Categoría 11: Integrations & CLI (5 Nodos)

1. **`CliExecutionNode`**: Ejecuta scripts y binarios del sistema (PowerShell, CMD, ejecutables nativos) capturando stdout/stderr.
2. **`WebhookNotificationNode`**: Envío de alertas y eventos HTTP POST/PUT a Discord, Slack o webhooks personalizados.
3. **`MediaTranscoderNode`**: Conversión y transcodificación de audio y video mediante FFmpeg integrado.
4. **`SqliteDatabaseSinkNode`**: Inserción de registros estructurados de auditoría en bases de datos SQLite locales.
5. **`MessageQueuePublisherNode`**: Publica mensajes y metadatos de archivos en colas de mensajería (RabbitMQ, MQTT).

---

## 6. Tutoriales Prácticos Paso a Paso

### Tutorial A: Organización y Optimización Automatizada de Fotografías
**Objetivo**: Escanear una tarjeta SD, extraer metadatos EXIF, optimizar imágenes a WebP y organizarlas en carpetas por año y modelo de cámara.

1. **`FolderSourceNode`**:
   - `SourcePath`: `E:\DCIM\100NIKON`
   - `ExtensionFilter`: `*.jpg, *.jpeg, *.png`
2. Conecta `Out` a **`ExifMetadataNode`** (extrae cámara y fecha).
3. Conecta `Out` a **`ImageOptimizerNode`**:
   - `TargetFormat`: `WebP`
   - `Quality`: `85`
4. Conecta `Out` a **`DestinationSinkNode`**:
   - `DestinationRoot`: `D:\Fotos_Organizadas\{Exif:Make}_{Exif:CameraModel}\{CreationDate:yyyy}\{CreationDate:MM}`
   - `ConflictStrategy`: `AutoIncrement`
5. Conecta `Done` a **`OriginalFileActionNode`**:
   - `ActionType`: `MoveToQuarantine` (respalda los originales de forma segura).

---

### Tutorial B: Ingesta Remota SFTP, Extracción y Reporte Consolidado
**Objetivo**: Descargar copias de seguridad desde un servidor SSH remoto, descomprimir su contenido, descartar duplicados y generar un informe HTML interactivo.

1. **`NetworkDownloadNode`**:
   - `Protocol`: `SFTP`
   - `Host`: `backup.miempresa.com` | `Username`: `operador`
   - `RemoteFilePath`: `/var/backups/daily.zip`
   - `DestinationFolder`: `C:\Temp\Ingesta`
2. Conecta `Out` a **`SmartUnpackNode`** (descomprime y extrae los archivos contenidos).
3. Conecta `Out` a **`HashCalculatorNode`** (`Algorithm: SHA256`).
4. Conecta `Out` a **`DeduplicationFilterNode`**:
   - Salida `Unique` $\rightarrow$ Conecta a **`DestinationSinkNode`** (`DestinationRoot: D:\Almacen_Limpio`).
   - Salida `Duplicate` $\rightarrow$ Conecta a **`SafeRecycleDeleteNode`** (envía a papelera).
5. Conecta `Unique` a **`OperationReportNode`**:
   - `ReportFormat`: `HTML`
   - `AutoOpenReport`: `true`

---

### Tutorial C: Pipeline de Inteligencia Artificial con OCR y Anonimización
**Objetivo**: Procesar facturas y documentos confidenciales, extraer el texto mediante OCR y anonimizar datos personales antes de archivarlos.

1. **`FolderSourceNode`** (`SourcePath: C:\Facturas_Nuevas`).
2. Conecta a **`LocalOcrNode`** (extrae texto de la imagen).
3. Conecta a **`PiiAnonymizerNode`** (detecta y enmascara DNI, tarjetas de crédito y nombres).
4. Conecta a **`DestinationSinkNode`** (`DestinationRoot: C:\Facturas_Anonimizadas`).

---

## 7. Atajos de Teclado y Productividad

| Atajo | Acción |
| :--- | :--- |
| `F5` | Ejecutar flujo de trabajo actual |
| `Ctrl + F5` | Ejecutar en Modo Simulación Virtual (Dry Run) |
| `F10` | Avanzar un paso en modo Depuración |
| `Ctrl + Z` | Deshacer última acción en el lienzo |
| `Ctrl + Y` | Rehacer última acción en el lienzo |
| `Ctrl + S` | Guardar flujo de trabajo actual (`.json`) |
| `Ctrl + O` | Abrir archivo de flujo de trabajo |
| `Ctrl + N` | Crear nuevo flujo en blanco |
| `Espacio` | Abrir visor rápido QuickLook para el elemento seleccionado |
| `Supr / Delete` | Eliminar nodo o conexión seleccionada |
| `Ctrl + F` | Buscar nodos en la Caja de Herramientas |
| `Ctrl + Wheel` | Zoom in / Zoom out en el lienzo visual |

---

*Manual oficial de FileFlow Studio. Distribuido bajo licencia GNU General Public License v3.0.*
