# Catálogo Completo de Nodos y Especificaciones - FileFlow Studio

Este catálogo contiene la especificación técnica completa de los **62 nodos** disponibles en los plugins oficiales de **FileFlow Studio**, detallando sus puertos de entrada y salida, parámetros configurables, tipo de operación y librerías de dominio subyacentes.

---

## 1. Módulo: FileFlow.Plugin.FileSystem (12 Nodos)

### 1. FolderSourceNode
- **Tipo:** Trigger / Input (Ingesta)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `SourcePath` (string), `ExtensionFilter` (string, ej. `*.jpg, *.png`), `Recursive` (bool), `EmitMode` (`FilesOnly`, `DirectoriesOnly`, `FilesAndDirectories`), `MaxRecursionDepth` (int), `WatchRealtime` (bool)
- **Función:** Escanea directorios y emite archivos de forma asíncrona con soporte de monitorización reactiva de cambios en tiempo real (`FileSystemWatcher`).

### 2. DestinationSinkNode
- **Tipo:** Sink / Output (Destino)
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Done` (`FileItemContext`)
- **Parámetros:** `DestinationRoot` (string), `ConflictStrategy` (`Overwrite`, `Skip`, `RenameIncremental`)
- **Función:** Escribe o consolida el archivo procesado en la ruta destino final gestionando colisiones de nombres.

### 3. AdvancedRenamerNode
- **Tipo:** Transformer (Modificación)
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Pattern` (string), `CollisionStrategy` (`AutoIncrement`, `Overwrite`, `Skip`), `PreserveExtension` (bool)
- **Acción Custom:** Editor visual de plantillas de tokens (`{Date:*}`, `{Exif:*}`, `{Hash:*}`).
- **Función:** Renombrado masivo avanzado con sanitización de caracteres inválidos en disco.

### 4. FileRelocatorNode
- **Tipo:** Action / Mover
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `TargetDirectory` (string), `OperationType` (`Move`, `Copy`, `HardLink`), `VerifyChecksum` (bool)
- **Función:** Reubica o duplica archivos en disco con opción de verificación de integridad SHA-256 post-transferencia.

### 5. SafeRecycleDeleteNode
- **Tipo:** Action / Ciclo de Vida
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `DeleteOriginal` (bool), `UseShellRecycleBin` (bool)
- **Función:** Borrado no destructivo mediante envío directo a la Papelera de reciclaje de Windows (`SHFileOperationW`).

### 6. OriginalFileActionNode
- **Tipo:** Lifecycle / Acción Centralizada
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `ActionType` (`Keep`, `MoveToRecycleBin`, `MoveToQuarantine`), `QuarantinePath` (string)
- **Función:** Aplica la política de retención o cuarentena al archivo de origen (`OriginalPath`) tras completar con éxito el procesamiento del pipeline.

### 7. OperationReportNode
- **Tipo:** Reporting / Diagnóstico
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Report` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `ReportFormat` (`HTML`, `Markdown`, `Text`, `JSON`, `CSV`), `ReportScope` (`Consolidated`, `PerFile`, `Both`), `DestinationFolder` (string), `ReportFileName` (string), `Theme` (`ModernDark`, `CleanLight`), `AutoOpenReport` (bool), `IncludeMetadata` (bool)
- **Función:** Genera reportes interactivos y trazabilidad de operaciones aplicadas a cada elemento procesado.

### 8. DirectoryInspectorNode
- **Tipo:** Router / Lógica de Carpeta
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `SingleArchive` (`FileItemContext`), `MixedContent` (`FileItemContext`), `DirectoriesOnly` (`FileItemContext`)
- **Función:** Evalúa la estructura de un directorio para bifurcar según contenga un archivo comprimido único o contenidos mixtos.

### 9. EmptyDirectoryCleanerNode
- **Tipo:** Cleanup / Limpieza
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `TargetDirectory` (string), `Recursive` (bool), `DeleteRootIfEmpty` (bool)
- **Función:** Limpieza recursiva de directorios vacíos residuales tras completar pipelines.

### 10. DocumentProcessorNode
- **Tipo:** Enricher / Extractor
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `ExtractLineCount` (bool), `DetectDocType` (bool)
- **Función:** Extrae estadísticas básicas (conteo de líneas, tipo de documento) hacia el diccionario `Metadata`.

### 11. VariableInjectorNode
- **Tipo:** Enricher / Metadatos
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `Variables` (Diccionario clave-valor con resolución de tokens dinámicos)
- **Función:** Inyecta variables dinámicas en el contexto del archivo para consumo por nodos downstream.

### 12. LogOutputNode
- **Tipo:** Diagnostic / Registro
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `LogLevel` (`Debug`, `Information`, `Warning`, `Error`), `MessageTemplate` (string)
- **Función:** Emite mensajes de log estructurados con resolución de variables en la consola de telemetría.

---

## 2. Módulo: FileFlow.Plugin.Logic (5 Nodos)

### 1. SwitchCaseNode
- **Tipo:** Router / Bifurcación Múltiple
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Default` (`FileItemContext`), `Cases...` (Puertos dinámicos)
- **Parámetros:** `EvaluationProperty` (string), `Cases` (Lista de patrones)
- **Función:** Enrutador multicamino que evalúa una propiedad o metadato y bifurca hacia ramas específicas.

### 2. ExpressionFilterNode
- **Tipo:** Filter / Decisión Booleana
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Matched` (`FileItemContext`), `Unmatched` (`FileItemContext`)
- **Parámetros:** `Property` (string), `Operator` (`Equal`, `Contains`, `GreaterThan`, `LessThan`, `RegexMatch`), `TargetValue` (string)
- **Función:** Filtra elementos evaluando condiciones numéricas, textuales o expresiones regulares.

### 3. BatchBufferNode
- **Tipo:** Control / Agrupación
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Batch` (`FileItemContext`), `Timeout` (`FileItemContext`)
- **Parámetros:** `BatchSize` (int), `TimeoutSeconds` (int)
- **Función:** Acumula elementos hasta alcanzar un tamaño de lote o una ventana temporal antes de liberarlos.

### 4. ThrottleDelayNode
- **Tipo:** Control / Flujo
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `DelayMilliseconds` (int), `RandomJitterMs` (int)
- **Función:** Introduce retardos controlados para evitar saturación de APIs remotas o sobrecarga de I/O.

### 5. ForkJoinBarrierNode
- **Tipo:** Synchronization / Barrera
- **Entradas:** `BranchA` (`FileItemContext`), `BranchB` (`FileItemContext`)
- **Salidas:** `Joined` (`FileItemContext`)
- **Parámetros:** `JoinKey` (string), `TimeoutSeconds` (int)
- **Función:** Sincroniza y reúne ramas paralelas que procesan el mismo archivo antes de continuar.

---

## 3. Módulo: FileFlow.Plugin.Images (2 Nodos)

### 1. ImageOptimizerNode
- **Tipo:** Transformer / Imagen
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `MaxWidth` (int), `MaxHeight` (int), `TargetFormat` (`WebP`, `Jpeg`, `Png`), `Quality` (int 1-100)
- **Dependencia:** `SixLabors.ImageSharp`
- **Función:** Redimensionamiento y optimización de peso de imágenes con cálculo del ahorro de bytes.

### 2. ExifMetadataNode
- **Tipo:** Enricher / Metadatos de Imagen
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `ExtractGps` (bool), `ExtractCameraInfo` (bool), `FormatDate` (string)
- **Dependencia:** `MetadataExtractor`
- **Función:** Extrae información EXIF (cámara, fecha de captura, geolocalización GPS) hacia `Metadata`.

---

## 4. Módulo: FileFlow.Plugin.Hashing (2 Nodos)

### 1. HashCalculatorNode
- **Tipo:** Enricher / Criptografía
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `Algorithm` (`SHA256`, `SHA512`, `MD5`), `MetadataKey` (string)
- **Función:** Calcula sumas de comprobación criptográficas y las guarda en `Metadata` para auditoría.

### 2. DeduplicationFilterNode
- **Tipo:** Filter / Integridad
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Unique` (`FileItemContext`), `Duplicate` (`FileItemContext`)
- **Parámetros:** `HashAlgorithm` (`SHA256`, `MD5`), `Scope` (`Session`, `PersistentDb`)
- **Función:** Detecta y separa archivos duplicados mediante hashes de contenido en memoria o persistentes.

---

## 5. Módulo: FileFlow.Plugin.Integrations (3 Nodos)

### 1. CliExecutionNode
- **Tipo:** Integration / Proceso Externo
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `ExecutablePath` (string), `ArgumentsTemplate` (string), `TimeoutSeconds` (int), `CaptureStdOut` (bool)
- **Función:** Ejecuta comandos de línea de órdenes o ejecutables de Windows con inyección de tokens de archivo.

### 2. WebhookNotificationNode
- **Tipo:** Integration / Red
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Url` (string), `HttpMethod` (`POST`, `PUT`, `GET`), `PayloadTemplate` (string), `CustomHeaders` (string)
- **Función:** Emite llamadas HTTP REST/Webhooks asíncronas con payloads JSON personalizados.

### 3. MediaTranscoderNode
- **Tipo:** Transformer / Multimedia
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Preset` (`H264`, `H265`, `WebM`, `MP3`), `QualityPreset` (string), `FfmpegPath` (string)
- **Acción Custom:** Probador interactivo de presets FFmpeg y comprobación de códecs instalados.
- **Función:** Transcodifica audio y vídeo mediante FFmpeg con soporte de aceleración por GPU.

---

## 6. Módulo: FileFlow.Plugin.Scripting (1 Nodo)

### 1. CustomScriptNode
- **Tipo:** Programmable / Scripting
- **Entradas:** Dinámicas (`In`, configurables por el usuario)
- **Salidas:** Dinámicas (`Out`, `True`, `False`, etc.)
- **Parámetros:** `Language` (`CSharp`, `JavaScript`), `ScriptCode` (string), `InputPorts` (string), `OutputPorts` (string), `TimeoutSeconds` (int)
- **Acción Custom:** Editor Script Studio integrado con resaltado de sintaxis AvalonEdit y probador en vivo.
- **Función:** Ejecuta lógica C# (Roslyn JIT) o JavaScript (Jint) sin compilar DLLs externas.

---

## 7. Módulo: FileFlow.Plugin.Documents (4 Nodos)

### 1. PdfMergeNode
- **Tipo:** Transformer / PDF
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `OutputFileName` (string), `OutputDirectory` (string)
- **Función:** Fusiona múltiples documentos PDF en un único documento consolidado.

### 2. PdfSplitNode
- **Tipo:** Transformer / PDF
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `SplitMode` (`EveryPage`, `PageRanges`), `PageRanges` (string), `OutputDirectory` (string)
- **Función:** Divide un documento PDF en archivos individuales por páginas o rangos específicos.

### 3. PdfTextExtractorNode
- **Tipo:** Enricher / Extractor
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `TargetMetadataKey` (string), `ExportToTextFile` (bool), `OutputDirectory` (string)
- **Función:** Extrae el contenido textual nativo de documentos PDF hacia variables o archivos de texto `.txt`.

### 4. PdfMetadataNode
- **Tipo:** Enricher / Metadatos PDF
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Author` (string), `Title` (string), `Subject` (string), `Keywords` (string)
- **Función:** Lee y edita los metadatos estándar (autor, título, asunto, palabras clave) de documentos PDF.

---

## 8. Módulo: FileFlow.Plugin.Network (7 Nodos)

### 1. FtpUploadNode
- **Tipo:** Sink / Red
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Host` (string), `Port` (int), `Username` (string), `Password` (string), `RemoteDirectory` (string), `Encryption` (`None`, `Explicit`, `Implicit`), `PassiveMode` (bool)
- **Función:** Sube archivos a servidores FTP/FTPS con cifrado TLS/SSL y creación automática de carpetas remotas.

### 2. FtpDownloadNode
- **Tipo:** Source & Transformer / Descarga FTP
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Host` (string), `Port` (int), `Username` (string), `Password` (string), `RemoteFilePath` (string), `DestinationFolder` (string), `FileName` (string), `Encryption` (`None`, `Explicit`, `Implicit`), `PassiveMode` (bool), `Overwrite` (bool), `DeleteAfterDownload` (bool)
- **Función:** Descarga archivos desde servidores FTP/FTPS hacia almacenamiento local con soporte para plantillas dinámicas y borrado remoto post-descarga.

### 3. SftpUploadNode
- **Tipo:** Sink / Red
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Host` (string), `Port` (int), `Username` (string), `AuthMethod` (`Password`, `PrivateKey`), `Password` (string), `PrivateKeyPath` (string), `PrivateKeyPassphrase` (string), `RemoteDirectory` (string)
- **Función:** Transfiere archivos cifrados mediante SSH/SFTP hacia servidores remotos con soporte de llaves privadas.

### 4. SftpDownloadNode
- **Tipo:** Source & Transformer / Descarga SFTP
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Host` (string), `Port` (int), `Username` (string), `AuthMethod` (`Password`, `PrivateKey`), `Password` (string), `PrivateKeyPath` (string), `PrivateKeyPassphrase` (string), `RemoteFilePath` (string), `DestinationFolder` (string), `FileName` (string), `Overwrite` (bool), `DeleteAfterDownload` (bool)
- **Función:** Descarga archivos cifrados mediante SFTP (SSH) desde servidores remotos Linux/VPS con soporte para claves privadas y borrado remoto.

### 5. SmbCopyNode
- **Tipo:** Sink / Almacenamiento Local
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `UncPath` (string), `Domain` (string), `Username` (string), `Password` (string)
- **Función:** Copia elementos hacia carpetas compartidas de red local y dispositivos NAS (rutas UNC).

### 6. WebDavUploadNode
- **Tipo:** Sink / Cloud Privado
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `ServerUrl` (string), `Username` (string), `Password` (string), `RemotePath` (string)
- **Función:** Sincroniza archivos con nubes privadas Nextcloud, ownCloud y servidores WebDAV.

### 7. RemoteDownloadNode
- **Tipo:** Trigger / Ingesta Remota
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `SourceUrl` (string), `DestinationFolder` (string), `FileName` (string), `Overwrite` (bool), `TimeoutSeconds` (int)
- **Función:** Descarga archivos remotos vía HTTP/HTTPS hacia el sistema de archivos local.

---

## 9. Módulo: FileFlow.Plugin.Data (7 Nodos)

### 1. ExcelReaderNode
- **Tipo:** Source / Tabular
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `SheetName` (string), `HasHeaderRow` (bool), `EmitPer` (`Row`, `Document`)
- **Función:** Lee y emite filas de hojas de cálculo Excel `.xlsx` como elementos individuales o consolidados.

### 2. CsvReaderNode
- **Tipo:** Source / Tabular
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Delimiter` (string), `HasHeader` (bool), `Encoding` (string)
- **Función:** Ingiere archivos delimitados (CSV, TSV) con autodetección de separador.

### 3. DataLookupNode
- **Tipo:** Enricher / Búsqueda VLOOKUP
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `NotFound` (`FileItemContext`)
- **Parámetros:** `DataSourceFile` (string), `KeyColumn` (string), `TargetColumn` (string), `OutputMetadataKey` (string)
- **Función:** Cruza valores del archivo contra tablas externas en memoria para enriquecer metadatos.

### 4. ExcelReportGeneratorNode
- **Tipo:** Sink / Tabular
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `OutputFile` (string), `SheetName` (string), `IncludeColumns` (string), `Theme` (string)
- **Función:** Compila metadatos de los elementos procesados en un libro de Excel formateado.

### 5. CsvExportNode
- **Tipo:** Sink / Tabular
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`)
- **Parámetros:** `DestinationFile` (string), `Delimiter` (string), `Append` (bool), `Fields` (string)
- **Función:** Exporta y acumula metadatos en archivos CSV planos.

### 6. SqliteDatabaseSinkNode
- **Tipo:** Sink / Base de Datos
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `DatabasePath` (string), `TableName` (string), `AutoCreateSchema` (bool)
- **Función:** Inserta registros estructurados de auditoría y trazabilidad en bases de datos SQLite.

### 7. DataFormatConverterNode
- **Tipo:** Transformer / Conversión de Formato
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `TargetFormat` (`Csv`, `Excel`, `Json`), `OutputDirectory` (string)
- **Función:** Convierte bidireccionalmente archivos entre formatos tabulares (Excel ⇄ CSV ⇄ JSON).

---

## 10. Módulo: FileFlow.Plugin.Archives (3 Nodos)

### 1. SmartUnpackNode
- **Tipo:** Source / Extractor
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `ExtractDirectory` (string), `CleanArchiveAfterExtract` (bool), `SupportedFormats` (ZIP, RAR, 7Z, TAR, GZ)
- **Acción Custom:** Inspector de contenido comprimido sin extraer.
- **Función:** Descomprime archivos multiformato con protección contra rutas maliciosas (Zip Slip).

### 2. ArchiveCompressorNode
- **Tipo:** Transformer / Compresión
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Format` (`Zip`, `TarGz`, `SevenZip`), `CompressionLevel` (`Fast`, `Optimal`, `Ultra`), `OutputDirectory` (string)
- **Función:** Empaqueta y comprime archivos y carpetas en contenedores optimizados.

### 3. ArchiveFilterNode
- **Tipo:** Filter / Inspección
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Matched` (`FileItemContext`), `Unmatched` (`FileItemContext`)
- **Parámetros:** `ContainsPattern` (string), `MinFiles` (int), `MaxFiles` (int)
- **Función:** Inspecciona el índice de un comprimido para decidir el enrutamiento sin extraer a disco.

---

## 11. Módulo: FileFlow.Plugin.AI (16 Nodos)

### 1. LocalOcrNode
- **Tipo:** Enricher / Visión & OCR
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `paddle-ocr`, `Custom`), `Language` (`es`, `en`, `de`, `fr`), `ConfidenceThreshold` (double)
- **Función:** Reconocimiento óptico de caracteres en imágenes y documentos escaneados mediante modelos ONNX locales.

### 2. SmartImageClassifierNode
- **Tipo:** Classifier / Visión
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `mobilenet-v2`, `resnet50`, `Custom`), `TopK` (int), `ConfidenceThreshold` (double)
- **Función:** Clasificación de imágenes fotográficas asignando categorías temáticas a `Metadata`.

### 3. FaceDetectorNode
- **Tipo:** Filter / Visión & Rostros
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `FacesFound` (`FileItemContext`), `NoFaces` (`FileItemContext`), `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `ultraface-320`, `Custom`), `MinFaces` (int), `ConfidenceThreshold` (double)
- **Función:** Detecta rostros humanos y bifurca el flujo según la presencia y número de personas detectadas.

### 4. ObjectDetectorNode
- **Tipo:** Detector / Visión
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Detected` (`FileItemContext`), `NotDetected` (`FileItemContext`), `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `yolov8n`, `yolov8s`, `yolov5s`, `Custom`), `TargetClasses` (string, ej. `person, car`), `ConfidenceThreshold` (double)
- **Función:** Detección de objetos multi-clase en tiempo real mediante modelos de la familia YOLO.

### 5. PromptObjectDetectorNode
- **Tipo:** Detector Abierto / Visión con Lenguaje
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Detected` (`FileItemContext`), `NotDetected` (`FileItemContext`), `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `grounding-dino`, `Custom`), `TextPrompt` (string en lenguaje natural), `BoxThreshold` (double)
- **Función:** Detección de objetos de vocabulario abierto mediante prompts en lenguaje natural libre (Grounding DINO).

### 6. LocalWhisperTranscriberNode
- **Tipo:** Enricher / Audio a Texto
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `whisper-tiny`, `whisper-base`, `whisper-small`, `Custom`), `Language` (string), `GenerateSrt` (bool), `OutputDirectory` (string)
- **Función:** Transcripción neuronal de voz a texto y generación automática de subtítulos `.srt` y transcripciones `.txt`.

### 7. LocalAiTranslatorNode
- **Tipo:** Transformer / Traducción de Texto
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `marian-es-en`, `marian-en-es`, `nllb-200`, `Custom`), `SourceLanguage` (string), `TargetLanguage` (string), `InputSource` (`FileContent`, `MetadataKey`), `OutputDirectory` (string)
- **Función:** Traducción neuronal de documentos de texto entre idiomas sin dependencias de servicios en la nube.

### 8. LocalLlmProcessorNode
- **Tipo:** Transformer / LLM & Razonamiento
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `phi-3-mini`, `Custom`), `SystemPrompt` (string), `TaskType` (`Summarize`, `ExtractJson`, `CustomPrompt`), `MaxTokens` (int)
- **Función:** Resumen de textos, extracción estructurada de entidades en JSON y análisis generativo local mediante modelos LLM compactos.

### 9. PromptTransformerNode
- **Tipo:** Transformer / Enriquecimiento de Prompts
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `TargetLanguage` (string), `EnhancementStyle` (`Descriptive`, `Concise`, `KeywordsOnly`), `InputMetadataKey` (string)
- **Función:** Normalización, enriquecimiento estilístico y traducción asistida de prompts para alimentar detectores y generadores.

### 10. BackgroundRemoverNode
- **Tipo:** Transformer / Visión Creativa
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Mask` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `rmbg-1.4`, `modnet`, `Custom`), `OutputFormat` (`PngTransparent`, `SolidColorReplacement`, `AlphaMaskOnly`), `BackgroundColor` (string hex), `OutputDirectory` (string)
- **Función:** Recorte automático de sujetos y eliminación de fondos en imágenes de forma no destructiva.

### 11. SuperResolutionUpscalerNode
- **Tipo:** Transformer / Restauración de Imagen
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Skipped` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `realesrgan-compact`, `Custom`), `ScaleFactor` (2x, 4x), `MaxInputDimension` (int), `OutputDirectory` (string)
- **Función:** Aumento de resolución convolucional 2x/4x y restauración de alta frecuencia para fotografías y documentos escaneados.

### 12. ContentModerationFilterNode
- **Tipo:** Filter / Moderación Visual
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Safe` (`FileItemContext`), `Sensitive` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `opennsfw2`, `Custom`), `SensitivityThreshold` (double 0.0 - 1.0)
- **Función:** Evaluación de contenido sensible o inapropiado y bifurcación automática del flujo según umbral de probabilidad.

### 13. VoiceActivityDetectorNode
- **Tipo:** Filter & Transformer / Audio
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Speech` (`FileItemContext`), `Silent` (`FileItemContext`), `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `silero-vad`, `Custom`), `Mode` (`DetectOnly`, `TrimSilence`), `SensitivityThreshold` (double), `MinSpeechDurationMs` (int), `PaddingDurationMs` (int), `OutputDirectory` (string)
- **Función:** Detección de presencia de voz humana y recorte de silencios muertos en pistas de audio con Silero VAD v5.

### 14. TextToSpeechNode
- **Tipo:** Source & Transformer / Voz
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `piper-es-davefx`, `piper-en-lessac`, `Custom`), `InputSource` (`FileContent`, `MetadataKey`, `CustomText`), `MetadataKeyName` (string), `CustomTextTemplate` (string), `SpeechRate` (double 0.5x - 2.0x), `OutputDirectory` (string)
- **Función:** Síntesis neural de voz natural en español e inglés generando archivos de audio `.wav` PCM de 16 bits.

### 15. PiiAnonymizerNode
- **Tipo:** Transformer / Cumplimiento RGPD
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Clean` (`FileItemContext`), `SensitiveFound` (`FileItemContext`), `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `pii-ner-multilingual`, `Custom`), `AnonymizationMode` (`TagReplacement`, `Mask`, `Hash`, `Remove`), toggles individuales (`FilterDniNie`, `FilterIban`, `FilterCreditCards`, `FilterEmails`, `FilterPhones`, `FilterIpAddresses`, `FilterPersonNames`), `OutputDirectory` (string)
- **Función:** Detección algorítmica y sanitización de datos de carácter personal sensible (DNI, IBAN, tarjetas, emails, teléfonos) bajo RGPD.

### 16. ZeroShotSemanticSearchNode
- **Tipo:** Filter & Classifier / Semántica Multimodal
- **Entradas:** `In` (`FileItemContext`)
- **Salidas:** `Matched` (`FileItemContext`), `Unmatched` (`FileItemContext`), `Out` (`FileItemContext`), `Error` (`FileItemContext`)
- **Parámetros:** `Model` (`Auto`, `clip-vit-b32`, `bge-small-multilingual`, `Custom`), `SearchQuery` (string), `CandidateLabels` (string), `SimilarityThreshold` (double), `TopK` (int)
- **Función:** Búsqueda semántica zero-shot y enrutamiento inteligente por similitud de coseno en lenguaje natural libre (CLIP / BGE).