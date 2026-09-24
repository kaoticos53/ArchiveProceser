using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("ArchiveCompressorNode_Name", "Archives", "ArchiveCompressorNode_Desc", PipelineRole.Transform,
    "comprimir", "empaquetar", "zip", "7z", "targz", "comprimido", "compress", "archive")]
public sealed class ArchiveCompressorNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ArchiveCompressorNode_Name", "Archive Compressor");
    public override string Category => "Archives";
    public override string Description => LocalizationManager.Instance.GetString("ArchiveCompressorNode_Desc", "Empaqueta y comprime archivos o directorios en formatos ZIP, TAR, GZ o 7Z.");

    public ArchiveCompressorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["ArchiveFormat"] = "ZIP";
        Parameters["CompressionType"] = "Deflate";
        Parameters["DestinationFolder"] = DefaultDestinationFolder;
        Parameters["DestinationDirectory"] = "";
        Parameters["ArchiveName"] = "{FileNameWithoutExtension}.zip";
    }

    /// <summary>
    /// Dónde escribe el comprimido cuando el flujo no dice otra cosa: la <b>carpeta de salida del flujo</b>, que es
    /// la que el flujo declara como su salida y, cuando no declara ninguna, la salida por defecto de los ajustes.
    /// El valor es una plantilla como cualquier otro parámetro, así que el usuario ve en la ficha exactamente dónde
    /// acaba el archivo (hito 209).
    /// </summary>
    public const string DefaultDestinationFolder = "{GlobalOutputDir}";

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: DefaultDestinationFolder, DisplayOrder: 1),
        new("ArchiveName", ParameterEditorType.Text, DefaultValue: @"{FileNameWithoutExtension}.zip", DisplayOrder: 2),
        new("ArchiveFormat", ParameterEditorType.Dropdown, DefaultValue: "ZIP", DisplayOrder: 3, Options: ["ZIP", "TAR", "GZ", "7Z"]),
        new("CompressionType", ParameterEditorType.Dropdown, DefaultValue: "Deflate", DisplayOrder: 4, Options: ["Deflate", "Store", "LZMA", "BZip2"])
    ];

    /// <summary>
    /// Carpeta donde se escribe el comprimido, tal y como la declara el flujo. La regla, escrita en un solo sitio
    /// para poder leerla y probarla: con carpeta declarada manda la plantilla —anclada por
    /// <see cref="ParameterHelper.ResolveOutputPath"/>, así que una ruta relativa cae dentro de lo que el flujo
    /// declaró como salida y <b>nunca</b> donde corra el proceso—; <b>sin declararla</b>, el comprimido sale en la
    /// <b>salida del flujo</b>, que es <see cref="DefaultDestinationFolder"/>: la carpeta que el flujo declara como
    /// suya y, cuando no declara ninguna, la salida por defecto de los ajustes.
    ///
    /// <para>Hasta el hito 209 lo que pasaba sin declarar carpeta era otra cosa: el comprimido salía <b>junto al
    /// archivo</b>, que es lo que hace un compresor de línea de órdenes. El valor de fábrica del parámetro ya no
    /// deja ese hueco —<c>{CurrentDir}</c> es hoy la forma de pedir «junto al archivo»: se declara— y el nodo dice
    /// en el log cuándo escribió en la salida por no haber nada declarado, porque dónde fue el archivo no se deduce
    /// del resultado. «DestinationDirectory» es el nombre heredado del parámetro y sigue funcionando como respaldo.</para>
    ///
    /// <para>Devuelve también <b>si la carpeta la puso el flujo o es la de fábrica</b>: las dos escriben en el mismo
    /// sitio cuando el flujo no dice nada, pero sólo una merece una frase en el log.</para>
    ///
    /// <para>Y el nombre heredado <c>DestinationDirectory</c> sigue mandando <b>sobre el valor de fábrica</b>, no sólo
    /// sobre un vacío: no aparece en la ficha del nodo, así que un valor ahí sólo puede venir de un flujo guardado con
    /// el nombre viejo —uno que sí declaró dónde escribe— y el valor de fábrica que la instancia trae hoy puesto no
    /// puede taparlo. Con el valor moderno declarado de verdad, manda el moderno.</para>
    /// </summary>
    private (string Folder, bool ChosenByTheFlow) DeclaredDestinationFolder()
    {
        string destFolder = GetParameter("DestinationFolder", string.Empty);
        string legacyFolder = GetParameter("DestinationDirectory", string.Empty);

        bool modernIsJustTheFactoryDefault = string.Equals(destFolder, DefaultDestinationFolder, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(legacyFolder) && (string.IsNullOrWhiteSpace(destFolder) || modernIsJustTheFactoryDefault))
        {
            return (legacyFolder, true);
        }

        if (!string.IsNullOrWhiteSpace(destFolder) && !modernIsJustTheFactoryDefault)
        {
            return (destFolder, true);
        }

        return (DefaultDestinationFolder, false);
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string inputPath = item.CurrentPath;
        (string destFolder, bool chosenByTheFlow) = DeclaredDestinationFolder();

        string destDir = ParameterHelper.ResolveOutputPath(destFolder, item);

        // Lo último que este nodo no puede hacer es escribir donde corre el proceso: el comprimido aparecería
        // dentro de la aplicación y el flujo parecería haber entregado cero. La resolución de rutas del SDK ancla
        // toda ruta relativa —bajo la salida del flujo o bajo el origen del archivo—, así que esto no debería
        // dispararse; si dispara (un patrón que se queda relativo, un ítem sin origen), el comprimido se ancla en
        // la salida por defecto de los ajustes y se dice, en vez de dejarlo en la carpeta de la aplicación.
        if (!CrossPlatformPath.IsPathFullyQualified(destDir))
        {
            string anchored = Path.Combine(AppPaths.DefaultGlobalOutputDir, destDir);
            context.Log(
                $"[Compresor] La carpeta de destino resuelta ('{destDir}') no era una ruta completa: el comprimido se " +
                $"escribe en la salida por defecto de los ajustes, '{anchored}'.",
                LogLevel.Warning, item);
            destDir = anchored;
        }

        string archiveName = GetParameter("ArchiveName", "{FileNameWithoutExtension}.zip");
        archiveName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(archiveName, item);

        string formatStr = GetParameter("ArchiveFormat", "ZIP").ToUpperInvariant();
        string compTypeStr = GetParameter("CompressionType", "Deflate").ToUpperInvariant();

        var storage = context.GetStorage();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        bool fileExists = await storage.FileExistsAsync(inputPath, cancellationToken).ConfigureAwait(false);
        bool dirExists = await storage.DirectoryExistsAsync(inputPath, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(inputPath) || (!fileExists && !dirExists))
        {
            context.Log($"[Compresor] Ruta de entrada no encontrada: '{inputPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        if (!chosenByTheFlow)
        {
            // Dónde fue el comprimido no se deduce: se dice. Este es el caso de todo flujo que no puso carpeta
            // —incluidos los que se guardaron cuando el valor de fábrica era vacío—, y desde el hito 209 significa
            // la salida del flujo, no el lado del archivo; el log nombra la carpeta y cómo pedir el otro sitio.
            context.Log(
                $"[Compresor] Sin carpeta de destino propia: el comprimido se escribe en la salida por omisión del " +
                $"flujo, en '{destDir}'. Para escribirlo junto al archivo que comprime, declare {{CurrentDir}} en " +
                $"'Carpeta de Destino'.",
                LogLevel.Information, item);
        }

        // El archivo de destino se abre (y se trunca) ANTES de construir el escritor, así que un escritor que
        // rechaza la combinación de formato y compresión deja en el disco un archivo de cero bytes con la
        // extensión del archivo prometido. El usuario ve un `.7z` que no es un `.7z`, y ninguna fase posterior
        // puede distinguirlo de un archivo bien hecho. Se anota la ruta aquí para poder retirarlo si la
        // compresión no llegó a escribirse.
        string? targetArchivePath = null;

        try
        {
            if (dirExists)
            {
                string fullInput = Path.GetFullPath(inputPath);
                string fullDest = Path.GetFullPath(destDir);
                if (fullDest.StartsWith(fullInput, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Destination directory '{destDir}' cannot be inside input source directory '{inputPath}' to prevent recursive compression loops.");
                }
            }

            if (!await storage.DirectoryExistsAsync(destDir, cancellationToken).ConfigureAwait(false))
            {
                await storage.CreateDirectoryAsync(destDir, cancellationToken).ConfigureAwait(false);
            }

            targetArchivePath = Path.Combine(destDir, archiveName);

            // El destino puede caer sobre el propio archivo de entrada: con la carpeta del archivo como destino
            // —«{CurrentDir}», o la salida del flujo cuando ésta es la carpeta del archivo— y el nombre de fábrica
            // «{FileNameWithoutExtension}.zip», comprimir 'paquete.zip' da 'paquete.zip', el mismo archivo. El
            // destino se abre (y se trunca) antes de leer la entrada, así que el archivo del usuario quedaba vaciado
            // sobre sí mismo y en su lugar un comprimido sin entradas:
            // el archivo, destruido. Un archivo no cabe dentro de sí mismo, así que se para antes de tocar nada
            // (destapado al ejecutar de punta a punta el ejemplo 21, hito 205).
            if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(targetArchivePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Archive destination '{targetArchivePath}' is the input file itself: compressing a file into itself would truncate it. Set a different 'DestinationFolder' or 'ArchiveName'.");
            }

            ArchiveType archiveType = formatStr switch
            {
                "TAR" => ArchiveType.Tar,
                "GZ" => ArchiveType.GZip,
                "7Z" => ArchiveType.SevenZip,
                _ => ArchiveType.Zip
            };

            CompressionType compType = compTypeStr switch
            {
                "STORE" or "NONE" => CompressionType.None,
                "LZMA" => CompressionType.LZMA,
                "BZIP2" => CompressionType.BZip2,
                "PPMD" => CompressionType.PPMd,
                _ => CompressionType.Deflate
            };

            await using (var stream = await storage.OpenWriteAsync(targetArchivePath, cancellationToken).ConfigureAwait(false))
            using (var writer = WriterFactory.OpenWriter(stream, archiveType, new WriterOptions(compType)))
            {
                if (fileExists)
                {
                    await using var inStream = await storage.OpenReadAsync(inputPath, cancellationToken).ConfigureAwait(false);
                    writer.Write(Path.GetFileName(inputPath), inStream);
                }
                else if (dirExists)
                {
                    var filesToPack = Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories);
                    foreach (var file in filesToPack)
                    {
                        string relativeEntryName = Path.GetRelativePath(inputPath, file).Replace('\\', '/');
                        await using var inStream = await storage.OpenReadAsync(file, cancellationToken).ConfigureAwait(false);
                        writer.Write(relativeEntryName, inStream);
                    }
                }
            }

            sw.Stop();
            long outSize = await storage.FileExistsAsync(targetArchivePath, cancellationToken).ConfigureAwait(false)
                ? await storage.GetFileSizeAsync(targetArchivePath, cancellationToken).ConfigureAwait(false)
                : 0;
            double compressionRatio = item.FileSizeBytes > 0 ? (double)outSize / item.FileSizeBytes * 100.0 : 100.0;

            var outputItem = item.DeepClone();
            outputItem.CurrentPath = targetArchivePath;
            outputItem.IsDirectory = false;
            outputItem.FileSizeBytes = outSize;
            outputItem.Metadata["CompressedFrom"] = inputPath;
            outputItem.Metadata["ArchiveFormat"] = formatStr;
            outputItem.AddLog($"ArchiveCompressorNode created archive {targetArchivePath}");

            string detailsJson = $"{{\"archiveFormat\": \"{formatStr}\", \"compressionType\": \"{compTypeStr}\", \"targetPath\": \"{targetArchivePath.Replace("\\", "\\\\")}\", \"originalSizeBytes\": {item.FileSizeBytes}, \"compressedSizeBytes\": {outSize}, \"ratioPct\": {compressionRatio.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
            context.Log($"[Compresor] Archivo {formatStr} generado ({compTypeStr}): '{Path.GetFileName(targetArchivePath)}' (Ratio: {compressionRatio:F1}%)", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await RemoveEmptyArchiveAsync(targetArchivePath, context, cancellationToken).ConfigureAwait(false);
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"input\": \"{inputPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Compresor] Error al comprimir archivo: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"ArchiveCompressorNode error: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    /// <summary>
    /// Retira el archivo de destino cuando la compresión no llegó a escribirlo. Se borra <b>sólo</b> si quedó a
    /// cero bytes: si ya existía algo con ese nombre y no lo tocamos, el archivo se queda como estaba y el
    /// error lo señala el log.
    /// </summary>
    private static async Task RemoveEmptyArchiveAsync(
        string? targetArchivePath,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetArchivePath)) return;

        try
        {
            var storage = context.GetStorage();
            if (!await storage.FileExistsAsync(targetArchivePath, cancellationToken).ConfigureAwait(false)) return;
            if (await storage.GetFileSizeAsync(targetArchivePath, cancellationToken).ConfigureAwait(false) != 0) return;

            await storage.DeleteAsync(targetArchivePath, permanent: true, ct: cancellationToken).ConfigureAwait(false);
            context.Log($"[Compresor] Archivo de destino retirado por quedar vacío: '{Path.GetFileName(targetArchivePath)}'", LogLevel.Warning);
        }
        catch (Exception cleanupEx)
        {
            context.Log($"[Compresor] No se pudo retirar el archivo vacío '{targetArchivePath}': {cleanupEx.Message}", LogLevel.Warning);
        }
    }
}
