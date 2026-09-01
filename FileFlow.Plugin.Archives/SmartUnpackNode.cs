using System.IO;
using System.Windows;
using FileFlow.Plugin.Archives.Services;
using FileFlow.Plugin.Archives.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("SmartUnpackNode_Name", "Archives", "SmartUnpackNode_Desc")]
public class SmartUnpackNode : IFlowNode, INodeCustomActionProvider
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmartUnpackNode_Name", "Smart Unpack");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("SmartUnpackNode_Desc", "Inspects archive structure and extracts intelligently, supporting password lists and multipart archives.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationFolder"] = @"{RelativeDir}\Unpacked",
        ["CleanWrapper"] = true,
        ["AutoDeleteAfterExtraction"] = false,
        ["RecursiveUnpack"] = true,
        ["PasswordList"] = "",
        ["PasswordFile"] = ""
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Unpacked", DisplayOrder: 1),
        new("CleanWrapper", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 2),
        new("AutoDeleteAfterExtraction", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 3),
        new("RecursiveUnpack", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4),
        new("PasswordList", ParameterEditorType.PasswordList, DefaultValue: "", DisplayOrder: 5),
        new("PasswordFile", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 6)
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("ManagePasswords", "🔑 Claves...", "🔑", "Gestionar lista de contraseñas para descompresión de archivos cifrados")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (actionId.Equals("ManagePasswords", StringComparison.OrdinalIgnoreCase) ||
            actionId.Equals("OpenPasswordManager", StringComparison.OrdinalIgnoreCase))
        {
            string currentPasswords = Parameters.TryGetValue("PasswordList", out var pVal) ? pVal?.ToString() ?? string.Empty : string.Empty;
            var window = new PasswordManagerWindow(currentPasswords);
            if (context is Window ownerWindow)
            {
                window.Owner = ownerWindow;
            }
            else if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }

            if (window.ShowDialog() == true)
            {
                lock (Parameters)
                {
                    Parameters["PasswordList"] = window.PasswordsText;
                }
            }
        }
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string archivePath = item.CurrentPath;
        string destPattern = Parameters.TryGetValue("DestinationFolder", out var val) ? ParameterHelper.GetString(val, @"{RelativeDir}\Unpacked") : @"{RelativeDir}\Unpacked";
        string destFolder = ParameterHelper.ResolveOutputPath(destPattern, item);
        bool cleanWrapper = Parameters.TryGetValue("CleanWrapper", out var cwVal) ? ParameterHelper.GetBoolean(cwVal, true) : true;
        bool autoDelete = Parameters.TryGetValue("AutoDeleteAfterExtraction", out var adVal) && ParameterHelper.GetBoolean(adVal, false);
        bool recursiveUnpack = !Parameters.TryGetValue("RecursiveUnpack", out var ruVal) || ParameterHelper.GetBoolean(ruVal, true);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        string pwdListParam = Parameters.TryGetValue("PasswordList", out var plVal) ? ParameterHelper.GetString(plVal, "") : "";
        string pwdFileParam = Parameters.TryGetValue("PasswordFile", out var pfVal) ? ParameterHelper.GetString(pfVal, "") : "";

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            context.Log($"[Descompresor] Archivo comprimido no encontrado: '{archivePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            var passwordCandidates = SafeArchiveExtractor.GetPasswordCandidates(pwdListParam, pwdFileParam, item);
            var (archive, validPassword) = SafeArchiveExtractor.OpenArchiveWithPassword(archivePath, passwordCandidates, context);

            using (archive)
            {
                var entryKeys = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Select(e => e.Key?.Replace('\\', '/') ?? string.Empty)
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .ToList();

                string? commonRoot = ArchiveVolumeResolver.GetCommonRootFolder(entryKeys);
                bool hasSingleWrapper = !string.IsNullOrEmpty(commonRoot);

                string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
                string finalExtractDir;

                if (hasSingleWrapper && cleanWrapper)
                {
                    finalExtractDir = destFolder;
                    context.Log($"[Descompresor] Carpeta envoltorio única detectada ('{commonRoot}'). Limpiando nivel redundante y extrayendo en: {finalExtractDir}", LogLevel.Debug, item);
                }
                else
                {
                    finalExtractDir = Path.Combine(destFolder, archiveNameNoExt);
                    context.Log($"[Descompresor] Múltiples entradas raíz. Extrayendo en subcarpeta: {finalExtractDir}", LogLevel.Debug, item);
                }

                if (!isDryRun)
                {
                    if (!Directory.Exists(finalExtractDir))
                    {
                        Directory.CreateDirectory(finalExtractDir);
                    }

                    SafeArchiveExtractor.ExtractEntriesSafely(archive, finalExtractDir, cancellationToken);

                    if (recursiveUnpack)
                    {
                        SafeArchiveExtractor.ExtractNestedArchives(finalExtractDir, passwordCandidates, context, cancellationToken);
                    }

                    if (autoDelete)
                    {
                        File.Delete(archivePath);
                        context.Log($"[Descompresor] Archivo comprimido original eliminado tras extracción: '{archivePath}'", LogLevel.Debug, item);
                    }
                }

                sw.Stop();
                var outputItem = new FileItemContext(finalExtractDir, isDirectory: true);
                outputItem.Metadata["UnpackedFrom"] = archivePath;
                outputItem.Metadata["HasSingleWrapper"] = hasSingleWrapper;
                outputItem.Metadata["ArchiveFormat"] = Path.GetExtension(archivePath).TrimStart('.').ToUpperInvariant();
                outputItem.Metadata["UnpackedFileCount"] = entryKeys.Count;
                if (!string.IsNullOrEmpty(validPassword))
                {
                    outputItem.Metadata["UsedPassword"] = validPassword;
                }
                outputItem.AddLog($"SmartUnpackNode extracted to {finalExtractDir}");

                string detailsJson = $"{{\"archive\": \"{archivePath.Replace("\\", "\\\\")}\", \"extractDir\": \"{finalExtractDir.Replace("\\", "\\\\")}\", \"entriesCount\": {entryKeys.Count}, \"hasSingleWrapper\": {hasSingleWrapper.ToString().ToLowerInvariant()}, \"passwordProtected\": {!string.IsNullOrEmpty(validPassword)}}}";
                context.Log($"[Descompresor] Descompresión completada: {entryKeys.Count} ficheros extraídos en '{finalExtractDir}'", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                await context.EmitAsync("Out", outputItem);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"archive\": \"{archivePath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Descompresor] Error en descompresión: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"SmartUnpackNode error: {ex.Message}");

            var relatedVolumes = ArchiveVolumeResolver.FindRelatedVolumeFiles(archivePath);
            item.Metadata["RelatedVolumeFiles"] = string.Join(";", relatedVolumes);
            item.Metadata["IsMultipartArchive"] = relatedVolumes.Count > 1;

            await context.EmitAsync("Error", item);
        }
    }
}
