# Catálogo Completo de Nodos y Especificaciones - FileFlow Studio

## 1. Módulo: FileFlow.Plugin.FileSystem (12 Nodos)
1. **FolderSourceNode**
   - Tipo: Trigger / Input
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `SourcePath` (string), `ExtensionFilter` (string), `Recursive` (bool), `EmitMode` (FilesOnly, DirectoriesOnly, FilesAndDirectories), `MaxRecursionDepth` (int), `WatchRealtime` (bool)
   - Función: Escanea el árbol de directorios y emite elementos asíncronamente con soporte de filtrado múltiple por extensión (ej. `*.jpg, *.png, *.zip`). Soporta monitorización de cambios en tiempo real.

2. **DestinationSinkNode**
   - Tipo: Sink / Output
   - Entradas: `In` (FileItemContext)
   - Salidas: `Done` (FileItemContext)
   - Parámetros: `DestinationRoot` (string), `ConflictStrategy` (Overwrite, Skip, RenameIncremental)
   - Función: Escribe o consolida el archivo procesado en la ruta destino final.

3. **AdvancedRenamerNode**
   - Tipo: Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `Pattern` (string), `CollisionStrategy` (AutoIncrement, Overwrite, Skip), `PreserveExtension` (bool)
   - Función: Renombrado masivo con motor de plantillas de tokens (`{Date:*}`, `{Exif:*}`, `{Hash:*}`) y sanitización de caracteres inválidos.

4. **FileRelocatorNode**
   - Tipo: Action
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `TargetDirectory` (string), `OperationType` (Move, Copy, HardLink), `VerifyChecksum` (bool)
   - Función: Reubica archivos en disco con opción de verificación de integridad SHA-256 post-transferencia.

5. **SafeRecycleDeleteNode**
   - Tipo: Action
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `DeleteOriginal` (bool), `UseShellRecycleBin` (bool)
   - Función: Borrado seguro no destructivo mediante envío directo a la Papelera de reciclaje de Windows (`SHFileOperationW`).

6. **OriginalFileActionNode**
   - Tipo: Lifecycle / Action
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `ActionType` (Keep, MoveToRecycleBin, MoveToQuarantine), `QuarantinePath` (string)
   - Función: Aplica la política de ciclo de vida al archivo de origen tras completar con éxito el procesamiento.

7. **OperationReportNode**
   - Tipo: Reporting / Diagnostic
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Report` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `ReportFormat` (HTML, Markdown, Text, JSON, CSV), `ReportScope` (Consolidated, PerFile, Both), `DestinationFolder` (string), `ReportFileName` (string), `Theme` (ModernDark, CleanLight), `AutoOpenReport` (bool), `IncludeMetadata` (bool)
   - Función: Genera reportes visuales interactivos y trazabilidad completa del ciclo de vida y operaciones aplicadas a cada archivo.

8. **DirectoryInspectorNode**
   - Tipo: Router / Logic
   - Entradas: `In` (FileItemContext)
   - Salidas: `SingleArchive` (FileItemContext), `MixedContent` (FileItemContext), `DirectoriesOnly` (FileItemContext)
   - Función: Evalúa la estructura de una carpeta para discernir si contiene exclusivamente un comprimido o contenidos mixtos.

9. **EmptyDirectoryCleanerNode**
   - Tipo: Cleanup
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `TargetDirectory` (string), `Recursive` (bool), `DeleteRootIfEmpty` (bool)
   - Función: Limpia de forma recursiva carpetas vacías residuales tras la ejecución de flujos.

10. **DocumentProcessorNode**
    - Tipo: Enricher
    - Entradas: `In` (FileItemContext)
    - Salidas: `Out` (FileItemContext)
    - Parámetros: `ExtractLineCount` (bool), `DetectDocType` (bool)
    - Función: Extrae conteo de líneas y tipo de documento (.pdf, .docx, .txt, .csv, .json) hacia `Metadata`.

11. **VariableInjectorNode**
    - Tipo: Enricher
    - Entradas: `In` (FileItemContext)
    - Salidas: `Out` (FileItemContext)
    - Parámetros: `Variables` (Diccionario clave-valor con soporte de tokens)
    - Función: Inyecta pares clave-valor dinámicos en el diccionario `Variables` del contexto.

12. **LogOutputNode**
    - Tipo: Diagnostic
    - Entradas: `In` (FileItemContext)
    - Salidas: `Out` (FileItemContext)
    - Parámetros: `LogLevel` (Debug, Information, Warning, Error), `MessageTemplate` (string)
    - Función: Emite mensajes de log estructurados personalizados durante el recorrido del pipeline.

---

## 2. Módulo: FileFlow.Plugin.Logic (5 Nodos)
1. **SwitchCaseNode**
   - Tipo: Router
   - Entradas: `In` (FileItemContext)
   - Salidas: `Default` (FileItemContext), `Cases...` (Puertos dinámicos)
   - Parámetros: `EvaluationProperty` (string), `Cases` (Lista de patrones)
   - Función: Enrutador condicional múltiple que evalúa propiedades o metadatos y bifurca hacia ramas dedicadas.

2. **ExpressionFilterNode**
   - Tipo: Filter
   - Entradas: `In` (FileItemContext)
   - Salidas: `Matched` (FileItemContext), `Unmatched` (FileItemContext)
   - Parámetros: `Property` (string), `Operator` (Equal, Contains, GreaterThan, LessThan, RegexMatch), `TargetValue` (string)
   - Función: Filtro de condición booleana para clasificar archivos.

3. **BatchBufferNode**
   - Tipo: Flow Control
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `BatchSize` (int), `TimeoutMs` (int)
   - Función: Acumula elementos hasta alcanzar el tamaño de lote o tiempo de espera antes de liberarlos.

4. **ForkJoinBarrierNode**
   - Tipo: Synchronization
   - Entradas: `Branch1` (FileItemContext), `Branch2` (FileItemContext)
   - Salidas: `Joined` (FileItemContext)
   - Parámetros: `RequiredBranchesCount` (int), `TimeoutSeconds` (int)
   - Función: Barrera de sincronización que aguarda a que un archivo complete múltiples ramas concurrentes antes de continuar.

5. **ThrottleDelayNode**
   - Tipo: Flow Control
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `DelayMilliseconds` (int)
   - Función: Introduce retardos controlados para evitar saturación de APIs o cuellos de botella en disco.

---

## 3. Módulo: FileFlow.Plugin.Archives (3 Nodos)
1. **SmartUnpackNode**
   - Tipo: Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `CleanWrapper` (bool), `AutoDeleteAfterExtraction` (bool), `DestinationFolder` (string)
   - Dependencia: `SharpCompress`
   - Función: Descompresión inteligente. Detecta carpetas raíz internas únicas (*folder wrappers*) para evitar duplicación de subdirectorios. Incluye protección canónica contra ataques *Zip Slip*.

2. **ArchiveCompressorNode**
   - Tipo: Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `ArchiveFormat` (Zip, TarGz, SevenZip), `CompressionLevel` (Fastest, Optimal), `TargetPath` (string)
   - Función: Comprime archivos o lotes calculando el ratio de compresión obtenido.

3. **ArchiveFilterNode**
   - Tipo: Filter
   - Entradas: `In` (FileItemContext)
   - Salidas: `PrimaryArchive` (FileItemContext), `SecondaryPart` (FileItemContext), `RegularFile` (FileItemContext)
   - Función: Clasifica archivos comprimidos detectando partes secundarias multipartes (.part02.rar, .z01) para evitar descompresiones redundantes.

---

## 4. Módulo: FileFlow.Plugin.Images (2 Nodos)
1. **ExifMetadataNode**
   - Tipo: Enricher
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `FallbackToCreationDate` (bool), `ExtractGps` (bool)
   - Dependencia: `MetadataExtractor`
   - Función: Extrae etiquetas EXIF (`DateTaken`, `CameraModel`, `GPS`, `Orientation`) y las almacena en `FileItemContext.Metadata`.

2. **ImageOptimizerNode**
   - Tipo: Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `MaxWidth` (int), `MaxHeight` (int), `TargetFormat` (WebP, Jpeg, Png), `Quality` (int 1-100)
   - Dependencia: `SixLabors.ImageSharp`
   - Función: Redimensiona manteniendo la relación de aspecto y comprime imágenes calculando el porcentaje de ahorro de espacio.

---

## 5. Módulo: FileFlow.Plugin.Hashing (2 Nodos)
1. **HashCalculatorNode**
   - Tipo: Enricher
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `Algorithm` (SHA256, SHA512, MD5), `MetadataKey` (string)
   - Función: Computa el hash criptográfico del contenido del archivo y lo guarda en `Metadata`.

2. **DeduplicationFilterNode**
   - Tipo: Filter
   - Entradas: `In` (FileItemContext)
   - Salidas: `Unique` (FileItemContext), `Duplicate` (FileItemContext)
   - Parámetros: `HashAlgorithm` (SHA256, MD5), `Scope` (Session, PersistentDb)
   - Función: Identifica archivos duplicados basándose en sumas de comprobación de contenido.

---

## 6. Módulo: FileFlow.Plugin.Integrations (3 Nodos)
1. **CliExecutionNode**
   - Tipo: Integration / Action
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `ExecutablePath` (string), `ArgumentsTemplate` (string), `TimeoutSeconds` (int), `CaptureStdOut` (bool)
   - Función: Ejecuta procesos externos del sistema operativo sustituyendo dinámicamente tokens de archivo.

2. **WebhookNotificationNode**
   - Tipo: Integration / Action
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `Url` (string), `HttpMethod` (POST, PUT, GET), `PayloadTemplate` (string), `CustomHeaders` (string)
   - Función: Envía notificaciones HTTP asíncronas con payloads JSON personalizados.

3. **MediaTranscoderNode**
   - Tipo: Media / Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `Preset` (H264, H265, WebM, MP3), `QualityPreset` (string), `FfmpegPath` (string)
   - Función: Transcodifica pistas de audio y vídeo mediante perfiles y aceleración por hardware.

---

## 7. Módulo: FileFlow.Plugin.Scripting (1 Nodo)
1. **CustomScriptNode**
   - Tipo: Programmable / Logic / Transformer
   - Entradas: Dinámicas (`In`, configurables por el usuario)
   - Salidas: Dinámicas (`Out`, `True`, `False`, configurables por el usuario con `EmitAsync` / `emit`)
   - Parámetros: `Language` (CSharp, JavaScript), `ScriptCode` (string), `InputPorts` (string), `OutputPorts` (string), `TimeoutSeconds` (int)
   - Acción Personalizada: `OpenScriptStudio` (💻 Editor de Scripts con AvalonEdit, probador en vivo y biblioteca de plantillas).
   - Función: Ejecuta lógica de programación personalizada del usuario en C# (Roslyn JIT en memoria) o JavaScript (sandbox Jint), permitiendo bifurcación multicanal, modificación de metadatos, tags y operaciones avanzadas sin compilar código externo.

---

## 8. Módulo: FileFlow.Plugin.Documents (4 Nodos)
1. **PdfMergeNode** (Fusión de documentos PDF)
2. **PdfSplitNode** (División de páginas de documentos PDF)
3. **PdfTextExtractorNode** (Extracción de texto estructurado de PDFs)
4. **PdfMetadataNode** (Lectura y edición de metadatos de documentos PDF)

---

## 9. Módulo: FileFlow.Plugin.Network (5 Nodos)
1. **FtpUploadNode** (Subida segura a servidores FTP/FTPS)
2. **SftpUploadNode** (Transferencia cifrada SSH/SFTP hacia servidores Linux/VPS)
3. **SmbCopyNode** (Copia a carpetas de red local y almacenamiento NAS UNC)
4. **WebDavUploadNode** (Sincronización con nubes privadas Nextcloud y ownCloud)
5. **RemoteDownloadNode** (Descarga remota de archivos vía HTTP, HTTPS y FTP)

---

## 10. Módulo: FileFlow.Plugin.Data (7 Nodos)
1. **ExcelReaderNode** (Lectura y streaming de filas de hojas Excel .xlsx)
2. **CsvReaderNode** (Lectura de archivos delimitados CSV, TSV y TXT con autodetección)
3. **DataLookupNode** (Cruce y enriquecimiento de metadatos VLOOKUP en memoria)
4. **ExcelReportGeneratorNode** (Generación de reportes tabulares consolidados en .xlsx)
5. **CsvExportNode** (Exportación y acumulación de metadatos a archivos delimitados CSV)
6. **SqliteDatabaseSinkNode** (Registro de trazabilidad y auditoría en bases de datos SQLite)
7. **DataFormatConverterNode** (Conversión directa entre formatos estructurados Excel ⇄ CSV ⇄ JSON)

---

## 11. Módulo: FileFlow.Plugin.AI (5 Nodos)
1. **LocalOcrNode** (Reconocimiento óptico de caracteres para imágenes y documentos)
2. **SmartImageClassifierNode** (Clasificador temático de fotos por visión artificial)
3. **FaceDetectorNode** (Detector y contador de rostros humanos con bifurcación dual)
4. **ObjectDetectorNode** (Detección múltiple de objetos y personas con modelo YOLO)
5. **LocalWhisperTranscriberNode** (Transcriptor de voz a texto y generador de subtítulos .srt)