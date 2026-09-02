using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

[NodeDefinition("ExcelReaderNode_Name", "Data & Databases", "ExcelReaderNode_Desc")]
public class ExcelReaderNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ExcelReaderNode_Name", "Lector de Hojas Excel");
    public string Category => "Data & Databases";
    public string Description => LocalizationManager.Instance.GetString("ExcelReaderNode_Desc", "Lee archivos Excel (.xlsx/.csv) y emite cada fila como un registro de datos con sus columnas en los metadatos.");

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
        ["FilePath"] = @"{RelativeDir}\data.xlsx",
        ["SheetName"] = "",
        ["HeaderRowIndex"] = 1,
        ["SkipEmptyRows"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("FilePath", ParameterEditorType.FilePath, DefaultValue: @"{RelativeDir}\data.xlsx", DisplayOrder: 1),
        new("SheetName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 2),
        new("HeaderRowIndex", ParameterEditorType.Number, DefaultValue: 1, Min: 1, Max: 100, DisplayOrder: 3),
        new("SkipEmptyRows", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
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
            context.Log($"[ExcelReader] Archivo no encontrado: '{targetPath}'", LogLevel.Error);
            return;
        }

        string sheetName = Parameters.TryGetValue("SheetName", out var sn) ? sn?.ToString() ?? string.Empty : string.Empty;
        bool skipEmpty = Parameters.TryGetValue("SkipEmptyRows", out var se) && ParameterHelper.GetBoolean(se, true);

        context.Log($"[ExcelReader] Abriendo hoja de cálculo: {Path.GetFileName(targetPath)}", LogLevel.Information);

        await using var stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        var rows = await stream.QueryAsync(useHeaderRow: true, sheetName: string.IsNullOrWhiteSpace(sheetName) ? null : sheetName).ConfigureAwait(false);

        long rowIndex = 0;
        foreach (IDictionary<string, object> row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowIndex++;

            if (skipEmpty && (row.Values.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()))))
            {
                continue;
            }

            var rowItem = new FileItemContext(targetPath)
            {
                OriginalPath = targetPath,
                FileSizeBytes = item.FileSizeBytes
            };

            // Copiar metadatos previos
            foreach (var (k, v) in item.Metadata)
            {
                rowItem.Metadata[k] = v;
            }

            rowItem.Metadata["RowIndex"] = rowIndex;
            rowItem.Metadata["SourceExcelFile"] = Path.GetFileName(targetPath);

            foreach (var kvp in row)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                string cleanKey = kvp.Key.Trim();
                string valStr = kvp.Value?.ToString() ?? string.Empty;
                rowItem.Metadata[cleanKey] = valStr;
            }

            await context.EmitAsync("RowOut", rowItem).ConfigureAwait(false);
        }

        context.Log($"[ExcelReader] Lectura finalizada. {rowIndex} filas emitidas desde '{Path.GetFileName(targetPath)}'.", LogLevel.Information);
    }
}
