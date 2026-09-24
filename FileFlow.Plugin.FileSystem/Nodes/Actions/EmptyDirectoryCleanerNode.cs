using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("EmptyDirectoryCleanerNode_Name", "Files", "EmptyDirectoryCleanerNode_Desc", PipelineRole.Transform,
    "limpiar", "carpetas vacias", "directorios vacios", "purgar", "cleaner", "empty")]
public sealed class EmptyDirectoryCleanerNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("EmptyDirectoryCleanerNode_Name", "Empty Directory Cleaner");
    public override string Category => "Files";
    public override string Description => LocalizationManager.Instance.GetString("EmptyDirectoryCleanerNode_Desc", "Recursively scans a target directory after batch processing and removes all empty subdirectories.");

    public EmptyDirectoryCleanerNode()
    {
        Inputs =
        [
            new NodePort("TriggerIn", typeof(FileItemContext), PortDirection.Input, "TriggerIn")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["TargetDirectory"] = @"{SourceDir}";
        Parameters["Recursive"] = true;
        Parameters["IgnoreHiddenSystemFiles"] = true;
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            string dirTemplate = GetParameter("TargetDirectory", @"{CurrentDir}");
            bool recursive = GetParameter("Recursive", false);
            bool ignoreHidden = GetParameter("IgnoreHiddenSystemFiles", false);

            string targetDir = VariableTemplateResolver.Resolve(dirTemplate, item);

            // El almacenamiento del contexto, resuelto una vez: es el que contesta las tres preguntas del nodo
            // —¿existe la carpeta?, ¿qué cuelga de ella?, ¿está vacía?— y el que borra. Resolverlo una vez y no en
            // cada nivel del árbol importa: cada resolución en una ejecución virtual construye un servicio nuevo.
            IStorageService storage = context.GetStorage();

            if (string.IsNullOrWhiteSpace(targetDir) || !await storage.DirectoryExistsAsync(targetDir, cancellationToken).ConfigureAwait(false))
            {
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_EmptyCleaner_NotFound", "[Empty Directory Cleaner] Directory not found: '{0}'", targetDir), LogLevel.Warning, item);
                await context.EmitAsync("Out", item);
                return;
            }

            int deletedCount = await CleanEmptyDirectoriesAsync(
                targetDir, recursive, ignoreHidden, context.IsDryRun, storage, context, Id, Name, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            string detailsJson = $"{{\"targetDirectory\": \"{targetDir.Replace("\\", "\\\\")}\", \"deletedCount\": {deletedCount}, \"recursive\": {recursive.ToString().ToLowerInvariant()}, \"isDryRun\": {context.IsDryRun.ToString().ToLowerInvariant()}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_EmptyCleaner_Deleted", "[Empty Directory Cleaner] Deleted {0:N0} empty folders in '{1}' (DryRun={2})", deletedCount, targetDir, context.IsDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            item.AddLog($"Cleaned {deletedCount} empty directories in {targetDir}");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_EmptyCleaner_Error", "[Empty Directory Cleaner] Error cleaning empty folders: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Empty directory cleaner failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    /// <summary>
    /// Recorre el árbol y borra las carpetas que quedan vacías, de dentro hacia fuera. Devuelve cuántas borró.
    ///
    /// <para><b>Todo el nodo pasa por el almacenamiento del contexto</b> (<see cref="IStorageService"/>): preguntar
    /// si la carpeta existe, recorrerla, saber si está vacía <i>y</i> borrarla. El borrado va por el contrato como
    /// en el resto de los nodos que borran (<c>SafeRecycleDeleteNode</c>, <c>IntermediateCleanupNode</c>): el
    /// almacenamiento físico real <i>devuelve</i> el fallo en vez de lanzarlo, así que un borrado que no se pudo
    /// hacer tiene que leerse del resultado y salir por la rama de error. Ese fallo es, además, <b>lo único</b> que
    /// puede recorrer esa rama —ninguna entrada de la configuración lo provoca, ni los permisos del sistema de
    /// archivos lo provocan igual en los tres sistemas operativos—, y por eso conviene que sea inyectable: es la
    /// costura que usan las pruebas para ejecutar la rama.</para>
    ///
    /// <para><b>El recorrido también va por el contrato, y no es un detalle</b>: mientras fue
    /// <c>Directory.Enumerate*</c>, una ejecución virtual tenía las carpetas en el almacén virtual y el recorrido
    /// miraba el disco del anfitrión, así que este nodo respondía «no hay nada que limpiar» sobre un árbol que
    /// existía en su propio almacén —y el borrado, que sí pasaba por el contrato, no llegaba a pedirse—. Enumerar
    /// y borrar tienen que mirar el mismo sitio, o el nodo miente en una de las dos ejecuciones.</para>
    ///
    /// <para>Las rutas se manipulan con <see cref="CrossPlatformPath"/> y no con <see cref="Path"/>: el almacén
    /// guarda rutas de Windows y de Unix —las del sistema en que se creó la ejecución virtual— y el separador del
    /// anfitrión parte un nombre de archivo por la mitad.</para>
    /// </summary>
    private static async Task<int> CleanEmptyDirectoriesAsync(
        string rootDir,
        bool recursive,
        bool ignoreHidden,
        bool isDryRun,
        IStorageService storage,
        IFlowExecutionContext context,
        string nodeId,
        string nodeName,
        CancellationToken cancellationToken)
    {
        int deleted = 0;

        if (recursive)
        {
            foreach (string subDir in await storage.EnumerateDirectoriesAsync(rootDir, cancellationToken).ConfigureAwait(false))
            {
                deleted += await CleanEmptyDirectoriesAsync(
                    subDir, recursive, ignoreHidden, isDryRun, storage, context, nodeId, nodeName, cancellationToken).ConfigureAwait(false);
            }
        }

        IEnumerable<string> entries = await storage.EnumerateFileSystemEntriesAsync(rootDir, cancellationToken).ConfigureAwait(false);
        if (ignoreHidden)
        {
            entries = entries.Where(e =>
            {
                string name = CrossPlatformPath.GetFileName(e);
                return !name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
            });
        }

        if (!entries.Any())
        {
            if (isDryRun)
            {
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    nodeId,
                    nodeName,
                    PlannedOperationType.Delete,
                    rootDir,
                    null,
                    "Delete empty directory"
                ));
            }
            else
            {
                var result = await storage.DeleteAsync(rootDir, permanent: true, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    throw new IOException(result.ErrorMessage ?? $"No se pudo borrar la carpeta vacía '{rootDir}'.");
                }

                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    nodeId,
                    JournalOperationType.DeletedPermanently,
                    rootDir,
                    null
                ));
            }
            deleted++;
        }

        return deleted;
    }
}
