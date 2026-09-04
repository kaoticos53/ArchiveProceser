using System.IO;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Data;

[NodeDefinition("CsvExportNode_Name", "Data", "CsvExportNode_Desc", PipelineRole.Sink,
    "csv", "exportar", "guardar", "tabla", "delimitado", "valores")]
public class CsvExportNode : IFlowNode
{
    private readonly Lock _lock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("CsvExportNode_Name", "Exportador CSV / TSV");
    public string Category => "Data";
    public string Description => LocalizationManager.Instance.GetString("CsvExportNode_Desc", "Exporta y acumula los metadatos de cada archivo procesado en un archivo CSV delimitado con formato configurable.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationPath"] = @"{GlobalOutputDir}\export.csv",
        ["Delimiter"] = ",",
        ["Columns"] = "FileName, FileSizeBytes, Timestamp",
        ["AppendMode"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DestinationPath", ParameterEditorType.FilePath, DefaultValue: @"{GlobalOutputDir}\export.csv", DisplayOrder: 1),
        new("Delimiter", ParameterEditorType.Dropdown, DefaultValue: ",", Options: [",", ";", "\t", "|"], DisplayOrder: 2),
        new("Columns", ParameterEditorType.Text, DefaultValue: "FileName, FileSizeBytes, Timestamp", DisplayOrder: 3),
        new("AppendMode", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        string destPath = Parameters.TryGetValue("DestinationPath", out var dp) ? dp?.ToString() ?? string.Empty : string.Empty;
        destPath = Environment.ExpandEnvironmentVariables(destPath);

        if (item.Metadata.TryGetValue("GlobalOutputDir", out var gOutObj) && gOutObj is string gOut)
        {
            destPath = destPath.Replace("{GlobalOutputDir}", gOut, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(destPath))
        {
            destPath = Path.Combine(Path.GetTempPath(), "FileFlow_Export.csv");
        }

        string dir = Path.GetDirectoryName(destPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        string delimiter = Parameters.TryGetValue("Delimiter", out var dVal) ? dVal?.ToString() ?? "," : ",";
        if (delimiter == "\\t") delimiter = "\t";

        string colsConfig = Parameters.TryGetValue("Columns", out var cols) ? cols?.ToString() ?? string.Empty : string.Empty;
        var selectedCols = string.IsNullOrWhiteSpace(colsConfig)
            ? ["FileName", "CurrentPath", "FileSizeBytes"]
            : colsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool appendMode = Parameters.TryGetValue("AppendMode", out var am) && ParameterHelper.GetBoolean(am, true);

        lock (_lock)
        {
            bool writeHeader = !File.Exists(destPath) || !appendMode;

            using var stream = new FileStream(destPath, appendMode ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            if (writeHeader)
            {
                writer.WriteLine(string.Join(delimiter, selectedCols.Select(EscapeCsvField)));
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

            writer.WriteLine(string.Join(delimiter, values));
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
