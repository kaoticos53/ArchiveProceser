using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

[NodeDefinition("ExcelReaderNode_Name", "Data", "ExcelReaderNode_Desc", PipelineRole.Source,
    "excel", "xlsx", "leer", "tabla", "hoja", "filas", "importar", "sheet")]
public sealed class ExcelReaderNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ExcelReaderNode_Name", "Lector de Hojas Excel");
    public override string Category => "Data";
    public override string Description => LocalizationManager.Instance.GetString("ExcelReaderNode_Desc", "Lee archivos Excel (.xlsx/.csv) y emite cada fila como un registro de datos con sus columnas en los metadatos.");

    public ExcelReaderNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("RowOut", typeof(FileItemContext), PortDirection.Output, "RowOut")
        ];

        Parameters["FilePath"] = "";
        Parameters["SheetName"] = "";
        Parameters["SkipEmptyRows"] = true;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("FilePath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 1),
        new("SheetName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 2),
        new("SkipEmptyRows", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        string targetPath = !string.IsNullOrWhiteSpace(item.CurrentPath) && await storage.FileExistsAsync(item.CurrentPath, cancellationToken)
            ? item.CurrentPath
            : GetParameter("FilePath", string.Empty);

        targetPath = Environment.ExpandEnvironmentVariables(targetPath);
        // El patrón lo resuelve la regla única del SDK: expande la carpeta del flujo <b>y todos sus alias</b> y
        // ancla toda ruta relativa, así que aquí no queda el texto de una plantilla ni una ruta que dependa de
        // dónde corre el proceso (hitos 209 y 210).
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            targetPath = ParameterHelper.ResolveOutputPath(targetPath, item);
        }

        if (string.IsNullOrWhiteSpace(targetPath) || !await storage.FileExistsAsync(targetPath, cancellationToken))
        {
            context.Log($"[ExcelReader] Archivo no encontrado: '{targetPath}'", LogLevel.Error);
            return;
        }

        string sheetName = GetParameter("SheetName", string.Empty);
        bool skipEmpty = GetParameter("SkipEmptyRows", false);

        context.Log($"[ExcelReader] Abriendo hoja de cálculo: {Path.GetFileName(targetPath)}", LogLevel.Information);

        await using var stream = await storage.OpenReadAsync(targetPath, cancellationToken);
        
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
