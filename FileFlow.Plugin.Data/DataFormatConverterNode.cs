using System.IO;
using System.Text;
using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

[NodeDefinition("DataFormatConverterNode_Name", "Data & Databases", "DataFormatConverterNode_Desc")]
public class DataFormatConverterNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DataFormatConverterNode_Name", "Conversor de Formatos de Datos");
    public string Category => "Data & Databases";
    public string Description => LocalizationManager.Instance.GetString("DataFormatConverterNode_Desc", "Convierte archivos tabulares y estructurados directamente entre formatos Excel (.xlsx), CSV y JSON.");

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
        ["TargetFormat"] = "JSON",
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("TargetFormat", ParameterEditorType.Dropdown, DefaultValue: "JSON", Options: ["JSON", "CSV", "ExcelXlsx"], DisplayOrder: 1),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 2)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[DataConverter] Archivo de entrada no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            return;
        }

        string outDir = Parameters.TryGetValue("OutputDirectory", out var od) ? od?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
        outDir = Environment.ExpandEnvironmentVariables(outDir);

        if (item.Metadata.TryGetValue("GlobalOutputDir", out var gOutObj) && gOutObj is string gOut)
        {
            outDir = outDir.Replace("{GlobalOutputDir}", gOut, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(outDir))
        {
            outDir = Path.GetDirectoryName(item.CurrentPath) ?? Path.GetTempPath();
        }

        Directory.CreateDirectory(outDir);

        string targetFormat = Parameters.TryGetValue("TargetFormat", out var tf) ? tf?.ToString() ?? "JSON" : "JSON";
        string inputExt = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        string baseName = Path.GetFileNameWithoutExtension(item.FileName);

        context.Log($"[DataConverter] Convirtiendo '{item.FileName}' ({inputExt}) a formato '{targetFormat}'...", LogLevel.Information, item);

        // 1. Cargar datos tabulares a memoria
        List<Dictionary<string, object?>> records = [];

        if (inputExt is ".xlsx" or ".xls")
        {
            await using var stream = new FileStream(item.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var rows = await stream.QueryAsync(useHeaderRow: true).ConfigureAwait(false);
            foreach (IDictionary<string, object> row in rows)
            {
                records.Add(row.ToDictionary(k => k.Key, v => (object?)v.Value));
            }
        }
        else if (inputExt is ".csv" or ".tsv" or ".txt")
        {
            using var reader = new StreamReader(item.CurrentPath, Encoding.UTF8);
            string? headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(headerLine))
            {
                char delimiter = headerLine.Contains(';') ? ';' : (headerLine.Contains('\t') ? '\t' : ',');
                var headers = headerLine.Split(delimiter).Select(h => h.Trim(' ', '"')).ToList();

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = line.Split(delimiter).Select(c => c.Trim(' ', '"')).ToList();
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < headers.Count; i++)
                    {
                        row[headers[i]] = i < cols.Count ? cols[i] : string.Empty;
                    }
                    records.Add(row);
                }
            }
        }
        else if (inputExt is ".json")
        {
            string json = await File.ReadAllTextAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    var row = new Dictionary<string, object?>();
                    foreach (var prop in el.EnumerateObject())
                    {
                        row[prop.Name] = prop.Value.ToString();
                    }
                    records.Add(row);
                }
            }
        }

        // 2. Guardar en el formato destino
        string destPath;
        if (targetFormat.Equals("JSON", StringComparison.OrdinalIgnoreCase))
        {
            destPath = Path.Combine(outDir, $"{baseName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonOutput = JsonSerializer.Serialize(records, options);
            await File.WriteAllTextAsync(destPath, jsonOutput, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        else if (targetFormat.Equals("ExcelXlsx", StringComparison.OrdinalIgnoreCase) || targetFormat.Equals("Excel", StringComparison.OrdinalIgnoreCase))
        {
            destPath = Path.Combine(outDir, $"{baseName}.xlsx");
            await MiniExcel.SaveAsAsync(destPath, records, overwriteFile: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else // CSV
        {
            destPath = Path.Combine(outDir, $"{baseName}.csv");
            using var stream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            var headers = records.Count > 0 ? records[0].Keys.ToList() : [];
            writer.WriteLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

            foreach (var row in records)
            {
                var values = headers.Select(h => row.TryGetValue(h, out var v) ? $"\"{v?.ToString()?.Replace("\"", "\"\"")}\"" : "\"\"");
                writer.WriteLine(string.Join(",", values));
            }
        }

        var convertedItem = item.DeepClone();
        convertedItem.CurrentPath = destPath;
        convertedItem.FileSizeBytes = new FileInfo(destPath).Length;
        convertedItem.Metadata["ConvertedFrom"] = inputExt;
        convertedItem.Metadata["ConvertedTo"] = targetFormat;
        convertedItem.Metadata["TotalRowsConverted"] = records.Count;

        context.Log($"[DataConverter] Conversión completada: '{Path.GetFileName(destPath)}' ({records.Count} filas)", LogLevel.Information, convertedItem);

        await context.EmitAsync("Out", convertedItem).ConfigureAwait(false);
    }
}
