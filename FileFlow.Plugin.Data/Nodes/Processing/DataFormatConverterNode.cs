using System.IO;
using System.Text;
using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

[NodeDefinition("DataFormatConverterNode_Name", "Data", "DataFormatConverterNode_Desc", PipelineRole.Transform,
    "convertir", "formato", "excel a csv", "csv a json", "json a excel", "transformar", "tabular")]
public sealed class DataFormatConverterNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DataFormatConverterNode_Name", "Conversor de Formatos de Datos");
    public string Category => "Data";
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
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken))
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

        if (!await storage.DirectoryExistsAsync(outDir, cancellationToken))
        {
            await storage.CreateDirectoryAsync(outDir, cancellationToken);
        }

        string targetFormat = Parameters.TryGetValue("TargetFormat", out var tf) ? tf?.ToString() ?? "JSON" : "JSON";
        string inputExt = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        string baseName = Path.GetFileNameWithoutExtension(item.FileName);

        context.Log($"[DataConverter] Convirtiendo '{item.FileName}' ({inputExt}) a formato '{targetFormat}'...", LogLevel.Information, item);

        // 1. Cargar datos tabulares a memoria
        List<Dictionary<string, object?>> records = [];

        if (inputExt is ".xlsx" or ".xls")
        {
            await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken);
            var rows = await stream.QueryAsync(useHeaderRow: true).ConfigureAwait(false);
            foreach (IDictionary<string, object> row in rows)
            {
                records.Add(row.ToDictionary(k => k.Key, v => (object?)v.Value));
            }
        }
        else if (inputExt is ".csv" or ".tsv" or ".txt")
        {
            await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
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
            string json = await storage.ReadAllTextAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
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
            await storage.WriteAllTextAsync(destPath, jsonOutput, ct: cancellationToken).ConfigureAwait(false);
        }
        else if (targetFormat.Equals("ExcelXlsx", StringComparison.OrdinalIgnoreCase) || targetFormat.Equals("Excel", StringComparison.OrdinalIgnoreCase))
        {
            destPath = Path.Combine(outDir, $"{baseName}.xlsx");
            await using var outStream = await storage.OpenWriteAsync(destPath, cancellationToken);
            await MiniExcel.SaveAsAsync(outStream, records, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else // CSV
        {
            destPath = Path.Combine(outDir, $"{baseName}.csv");
            await using var stream = await storage.OpenWriteAsync(destPath, cancellationToken);
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            var headers = records.Count > 0 ? records[0].Keys.ToList() : [];
            writer.WriteLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

            foreach (var row in records)
            {
                var values = headers.Select(h => row.TryGetValue(h, out var v) ? $"\"{v?.ToString()?.Replace("\"", "\"\"")}\"" : "\"\"");
                writer.WriteLine(string.Join(",", values));
            }
        }

        long destSize = await storage.FileExistsAsync(destPath, cancellationToken)
            ? await storage.GetFileSizeAsync(destPath, cancellationToken)
            : 0;

        var convertedItem = item.DeepClone();
        convertedItem.CurrentPath = destPath;
        convertedItem.FileSizeBytes = destSize;
        convertedItem.Metadata["ConvertedFrom"] = inputExt;
        convertedItem.Metadata["ConvertedTo"] = targetFormat;
        convertedItem.Metadata["TotalRowsConverted"] = records.Count;

        context.Log($"[DataConverter] Conversión completada: '{Path.GetFileName(destPath)}' ({records.Count} filas)", LogLevel.Information, convertedItem);

        await context.EmitAsync("Out", convertedItem).ConfigureAwait(false);
    }
}
