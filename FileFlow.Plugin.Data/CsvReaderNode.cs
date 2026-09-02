using System.IO;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Data;

[NodeDefinition("CsvReaderNode_Name", "Data & Databases", "CsvReaderNode_Desc")]
public class CsvReaderNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("CsvReaderNode_Name", "Lector de Archivos CSV / TSV");
    public string Category => "Data & Databases";
    public string Description => LocalizationManager.Instance.GetString("CsvReaderNode_Desc", "Lee archivos delimitados (CSV, TSV, TXT) con autodetección de formato y emite cada fila con sus columnas en los metadatos.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("RowOut", typeof(FileItemContext), PortDirection.Output, "RowOut")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FilePath"] = @"{RelativeDir}\data.csv",
        ["Delimiter"] = "Auto",
        ["Encoding"] = "UTF-8",
        ["HasHeader"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("FilePath", ParameterEditorType.FilePath, DefaultValue: @"{RelativeDir}\data.csv", DisplayOrder: 1),
        new("Delimiter", ParameterEditorType.Dropdown, DefaultValue: "Auto", Options: ["Auto", ",", ";", "\t", "|"], DisplayOrder: 2),
        new("Encoding", ParameterEditorType.Dropdown, DefaultValue: "UTF-8", Options: ["UTF-8", "ANSI", "ASCII", "Unicode"], DisplayOrder: 3),
        new("HasHeader", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        string targetPath = !string.IsNullOrWhiteSpace(item.CurrentPath) && File.Exists(item.CurrentPath)
            ? item.CurrentPath
            : (Parameters.TryGetValue("FilePath", out var fp) ? fp?.ToString() ?? string.Empty : string.Empty);

        targetPath = Environment.ExpandEnvironmentVariables(targetPath);
        if (item.Metadata.TryGetValue("GlobalOutputDir", out var outDirObj) && outDirObj is string gOut)
        {
            targetPath = targetPath.Replace("{GlobalOutputDir}", gOut, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            context.Log($"[CsvReader] Archivo CSV no encontrado: '{targetPath}'", LogLevel.Error);
            return;
        }

        string delimiterConfig = Parameters.TryGetValue("Delimiter", out var dVal) ? dVal?.ToString() ?? "Auto" : "Auto";
        string encodingConfig = Parameters.TryGetValue("Encoding", out var eVal) ? eVal?.ToString() ?? "UTF-8" : "UTF-8";
        bool hasHeader = Parameters.TryGetValue("HasHeader", out var hVal) && ParameterHelper.GetBoolean(hVal, true);

        Encoding enc = encodingConfig.ToUpperInvariant() switch
        {
            "ANSI" => Encoding.Latin1,
            "ASCII" => Encoding.ASCII,
            "UNICODE" => Encoding.Unicode,
            _ => Encoding.UTF8
        };

        context.Log($"[CsvReader] Leyendo archivo delimitado: {Path.GetFileName(targetPath)}", LogLevel.Information);

        using var reader = new StreamReader(targetPath, enc);
        string? firstLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            context.Log($"[CsvReader] El archivo CSV está vacío.", LogLevel.Warning);
            return;
        }

        char delimiter = ResolveDelimiter(firstLine, delimiterConfig);
        List<string> headers = [];

        if (hasHeader)
        {
            headers = ParseCsvLine(firstLine, delimiter);
        }
        else
        {
            var firstRowCols = ParseCsvLine(firstLine, delimiter);
            for (int i = 0; i < firstRowCols.Count; i++)
            {
                headers.Add($"Column_{i + 1}");
            }
        }

        long rowIndex = 0;

        // Si no tenía cabecera, la primera línea leída ya era una fila de datos
        if (!hasHeader)
        {
            rowIndex++;
            var firstRowItem = CreateRowItem(targetPath, item, rowIndex, headers, ParseCsvLine(firstLine, delimiter));
            await context.EmitAsync("RowOut", firstRowItem).ConfigureAwait(false);
        }

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            rowIndex++;
            var columns = ParseCsvLine(line, delimiter);
            var rowItem = CreateRowItem(targetPath, item, rowIndex, headers, columns);

            await context.EmitAsync("RowOut", rowItem).ConfigureAwait(false);
        }

        context.Log($"[CsvReader] Procesamiento CSV finalizado. {rowIndex} filas emitidas.", LogLevel.Information);
    }

    private static FileItemContext CreateRowItem(string targetPath, FileItemContext originalItem, long rowIndex, List<string> headers, List<string> columns)
    {
        var rowItem = new FileItemContext(targetPath)
        {
            OriginalPath = targetPath,
            FileSizeBytes = originalItem.FileSizeBytes
        };

        foreach (var (k, v) in originalItem.Metadata)
        {
            rowItem.Metadata[k] = v;
        }

        rowItem.Metadata["RowIndex"] = rowIndex;
        rowItem.Metadata["SourceCsvFile"] = Path.GetFileName(targetPath);

        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i].Trim();
            string val = i < columns.Count ? columns[i] : string.Empty;
            rowItem.Metadata[header] = val;
        }

        return rowItem;
    }

    private static char ResolveDelimiter(string headerLine, string delimiterConfig)
    {
        if (delimiterConfig.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            int commas = headerLine.Count(c => c == ',');
            int semicolons = headerLine.Count(c => c == ';');
            int tabs = headerLine.Count(c => c == '\t');
            int pipes = headerLine.Count(c => c == '|');

            int max = Math.Max(commas, Math.Max(semicolons, Math.Max(tabs, pipes)));
            if (max == semicolons && semicolons > 0) return ';';
            if (max == tabs && tabs > 0) return '\t';
            if (max == pipes && pipes > 0) return '|';
            return ',';
        }

        return delimiterConfig switch
        {
            ";" => ';',
            "\\t" or "\t" => '\t',
            "|" => '|',
            _ => ','
        };
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        List<string> result = [];
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString().Trim());
        return result;
    }
}
