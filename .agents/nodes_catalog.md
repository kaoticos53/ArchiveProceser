# Catálogo de Nodos y Especificaciones

## Módulo: FileFlow.Plugin.FileSystem
1. **FolderSourceNode**
   - Tipo: Trigger / Input
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `SourcePath` (string), `Recursive` (bool), `WatchRealtime` (bool)
   - Función: Escanea el árbol de directorios y emite elementos de forma asíncrona.

2. **DirectoryInspectorNode**
   - Tipo: Router / Logic
   - Entradas: `In` (FileItemContext)
   - Salidas: `SingleArchive` (FileItemContext), `MixedContent` (FileItemContext), `DirectoriesOnly` (FileItemContext)
   - Función: Evalúa si una carpeta contiene exclusivamente un archivo comprimido o múltiples archivos/subcarpetas.

3. **OriginalFileActionNode**
   - Tipo: Action / Lifecycle
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `ActionType` (Keep, MoveToRecycleBin, PermanentDelete, MoveToQuarantine), `QuarantinePath` (string)
   - Función: Aplica la política de ciclo de vida al archivo original tras confirmar el éxito de los nodos previos.

4. **DestinationSinkNode**
   - Tipo: Sink / Output
   - Entradas: `In` (FileItemContext)
   - Salidas: `Done` (FileItemContext)
   - Parámetros: `DestinationRoot` (string), `ConflictStrategy` (Overwrite, Skip, RenameIncremental)
   - Función: Escribe o mueve el archivo final a la ruta proyectada.

## Módulo: FileFlow.Plugin.Archives
1. **SmartUnpackNode**
   - Tipo: Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `CleanWrapper` (bool), `AutoDeleteAfterExtraction` (bool)
   - Dependencia: `SharpCompress`
   - Función: Abre el archivo sin extraerlo. Si todo su contenido depende de una sola carpeta raíz interna (*folder wrapper*), extrae directamente en destino sin crear otra subcarpeta. Si hay mezcla en la raíz del comprimido, crea una carpeta con el nombre del comprimido y extrae su estructura interna.

## Módulo: FileFlow.Plugin.Images
1. **ExifMetadataNode**
   - Tipo: Enricher
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext)
   - Parámetros: `FallbackToCreationDate` (bool)
   - Dependencia: `MetadataExtractor`
   - Función: Extrae `DateTaken`, `CameraModel`, `GPS` y los almacena en `Metadata` del contexto.

2. **ImageOptimizerNode**
   - Tipo: Transformer
   - Entradas: `In` (FileItemContext)
   - Salidas: `Out` (FileItemContext), `Error` (FileItemContext)
   - Parámetros: `MaxWidth` (int), `MaxHeight` (int), `TargetFormat` (WebP, Jpeg, Png), `Quality` (int 1-100)
   - Dependencia: `SixLabors.ImageSharp`
   - Función: Redimensiona manteniendo relación de aspecto y comprime la imagen.