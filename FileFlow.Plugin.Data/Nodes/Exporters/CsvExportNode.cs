using System.IO;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Data;

[NodeDefinition("CsvExportNode_Name", "Data", "CsvExportNode_Desc", PipelineRole.Sink,
    "csv", "exportar", "guardar", "tabla", "delimitado", "valores")]
public sealed class CsvExportNode : FlowNodeBase
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public override string Name => LocalizationManager.Instance.GetString("CsvExportNode_Name", "Exportador CSV / TSV");
    public override string Category => "Data";
    public override string Description => LocalizationManager.Instance.GetString("CsvExportNode_Desc", "Exporta y acumula los metadatos de cada archivo procesado en un archivo CSV delimitado con formato configurable.");

    public CsvExportNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
        ];

        Parameters["DestinationPath"] = @"{GlobalOutputDir}\export.csv";
        Parameters["Delimiter"] = ",";
        Parameters["Columns"] = "FileName, FileSizeBytes, Timestamp";
        Parameters["AppendMode"] = true;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DestinationPath", ParameterEditorType.FilePath, DefaultValue: @"{GlobalOutputDir}\export.csv", DisplayOrder: 1),
        new("Delimiter", ParameterEditorType.Dropdown, DefaultValue: ",", Options: [",", ";", "\t", "|"], DisplayOrder: 2),
        new("Columns", ParameterEditorType.Text, DefaultValue: "FileName, FileSizeBytes, Timestamp", DisplayOrder: 3),
        new("AppendMode", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        string destPath = GetParameter("DestinationPath", string.Empty);
        destPath = Environment.ExpandEnvironmentVariables(destPath);

        // El patrón lo resuelve la regla única del SDK: expande la carpeta del flujo <b>y todos sus alias</b>
        // (`{GlobalOutputDir}`, `{DefaultOutputDir}`, `{OutputDir}`…) y ancla toda ruta relativa, de modo que aquí
        // no queda el texto de una plantilla declarada ni una carpeta que dependa de dónde corre el proceso. Antes
        // sólo se sustituía el token canónico por lo que hubiera en la metadata tal cual (hitos 209 y 210).
        if (!string.IsNullOrWhiteSpace(destPath))
        {
            destPath = ParameterHelper.ResolveOutputPath(destPath, item);
        }

        if (string.IsNullOrWhiteSpace(destPath))
        {
            destPath = Path.Combine(Path.GetTempPath(), "FileFlow_Export.csv");
        }

        var storage = context.GetStorage();
        string dir = Path.GetDirectoryName(destPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(dir) && !await storage.DirectoryExistsAsync(dir, cancellationToken))
        {
            await storage.CreateDirectoryAsync(dir, cancellationToken);
        }

        string delimiter = GetParameter("Delimiter", ",");
        if (delimiter == "\\t") delimiter = "\t";

        string colsConfig = GetParameter("Columns", string.Empty);
        var selectedCols = string.IsNullOrWhiteSpace(colsConfig)
            ? ["FileName", "CurrentPath", "FileSizeBytes"]
            : colsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool appendMode = GetParameter("AppendMode", false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool fileExists = await storage.FileExistsAsync(destPath, cancellationToken).ConfigureAwait(false);
            bool writeHeader = !fileExists || !appendMode;

            await using var stream = appendMode
                ? await storage.OpenAppendAsync(destPath, cancellationToken).ConfigureAwait(false)
                : await storage.OpenWriteAsync(destPath, cancellationToken).ConfigureAwait(false);

            await using var writer = new StreamWriter(stream, Encoding.UTF8);

            if (writeHeader)
            {
                await writer.WriteLineAsync(string.Join(delimiter, selectedCols.Select(EscapeCsvField))).ConfigureAwait(false);
            }

            var values = new List<string>();
            foreach (var col in selectedCols)
            {
                if (col.Equals("FileName", StringComparison.OrdinalIgnoreCase)) values.Add(EscapeCsvField(item.FileName));
                else if (col.Equals("CurrentPath", StringComparison.OrdinalIgnoreCase)) values.Add(EscapeCsvField(item.CurrentPath));
                else if (col.Equals("OriginalPath", StringComparison.OrdinalIgnoreCase)) values.Add(EscapeCsvField(item.OriginalPath));
                else if (col.Equals("FileSizeBytes", StringComparison.OrdinalIgnoreCase)) values.Add(item.FileSizeBytes.ToString());
                else if (col.Equals("Timestamp", StringComparison.OrdinalIgnoreCase)) values.Add(DateTime.UtcNow.ToString("o"));
                else if (item.Metadata.TryGetValue(col, out var mVal)) values.Add(EscapeCsvField(mVal?.ToString() ?? string.Empty));
                else values.Add(string.Empty);
            }

            await writer.WriteLineAsync(string.Join(delimiter, values)).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await context.EmitAsync("Out", item).ConfigureAwait(false);
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains(';') || field.Contains('\t') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
