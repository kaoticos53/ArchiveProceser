using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Data;

[NodeDefinition("DataLookupNode_Name", "Data", "DataLookupNode_Desc", PipelineRole.Analyze,
    "lookup", "vlookup", "buscar", "cruzar", "enriquecer", "tabla", "clave")]
public sealed class DataLookupNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("DataLookupNode_Name", "Cruce de Datos (Lookup / BUSCARV)");
    public override string Category => "Data";
    public override string Description => LocalizationManager.Instance.GetString("DataLookupNode_Desc", "Busca y cruza información del archivo actual contra una tabla externa (Excel, CSV o JSON) inyectando sus columnas en los metadatos.");

    public DataLookupNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Matched", typeof(FileItemContext), PortDirection.Output, "Matched"),
            new NodePort("Unmatched", typeof(FileItemContext), PortDirection.Output, "Unmatched")
        ];

        Parameters["DataSourcePath"] = @"{RelativeDir}\lookup_table.xlsx";
        Parameters["LookupKeyColumn"] = "Id";
        Parameters["MatchExpression"] = "{FileNameWithoutExtension}";
        Parameters["PrefixColumns"] = "Lookup_";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DataSourcePath", ParameterEditorType.FilePath, DefaultValue: @"{RelativeDir}\lookup_table.xlsx", DisplayOrder: 1),
        new("LookupKeyColumn", ParameterEditorType.Text, DefaultValue: "Id", DisplayOrder: 2),
        new("MatchExpression", ParameterEditorType.Text, DefaultValue: "{FileNameWithoutExtension}", DisplayOrder: 3),
        new("PrefixColumns", ParameterEditorType.Text, DefaultValue: "Lookup_", DisplayOrder: 4)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        string dataSourcePath = GetParameter("DataSourcePath", string.Empty);
        dataSourcePath = Environment.ExpandEnvironmentVariables(dataSourcePath);

        if (item.Metadata.TryGetValue("GlobalOutputDir", out var gOutObj) && gOutObj is string gOut)
        {
            dataSourcePath = dataSourcePath.Replace("{GlobalOutputDir}", gOut, StringComparison.OrdinalIgnoreCase);
        }

        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(dataSourcePath) || !await storage.FileExistsAsync(dataSourcePath, cancellationToken).ConfigureAwait(false))
        {
            context.Log($"[DataLookup] Archivo de datos de referencia no encontrado: '{dataSourcePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Unmatched", item).ConfigureAwait(false);
            return;
        }

        string keyColumn = GetParameter("LookupKeyColumn", "Id");
        string matchExpr = GetParameter("MatchExpression", "{FileNameWithoutExtension}");
        string prefix = GetParameter("PrefixColumns", string.Empty);

        // Evaluar la clave de búsqueda sobre el item actual
        string searchKey = ResolveSearchKey(matchExpr, item);

        if (string.IsNullOrWhiteSpace(searchKey))
        {
            context.Log($"[DataLookup] Clave de búsqueda vacía al evaluar '{matchExpr}' en {item.FileName}", LogLevel.Debug, item);
            await context.EmitAsync("Unmatched", item).ConfigureAwait(false);
            return;
        }

        var lookupIndex = await DataLookupTableLoader.LoadLookupTableAsync(dataSourcePath, keyColumn, cancellationToken, storage).ConfigureAwait(false);

        if (lookupIndex.TryGetValue(searchKey, out var matchedRow))
        {
            context.Log($"[DataLookup] Coincidencia encontrada para clave '{searchKey}' en {Path.GetFileName(dataSourcePath)}", LogLevel.Information, item);

            foreach (var (colName, colVal) in matchedRow)
            {
                string finalKey = string.IsNullOrWhiteSpace(prefix) ? colName : $"{prefix}{colName}";
                item.Metadata[finalKey] = colVal;
            }

            item.Metadata["LookupMatched"] = true;
            item.Metadata["LookupKey"] = searchKey;

            await context.EmitAsync("Matched", item).ConfigureAwait(false);
        }
        else
        {
            context.Log($"[DataLookup] Sin coincidencia para clave '{searchKey}' en {Path.GetFileName(dataSourcePath)}", LogLevel.Debug, item);
            item.Metadata["LookupMatched"] = false;
            await context.EmitAsync("Unmatched", item).ConfigureAwait(false);
        }
    }

    private static string ResolveSearchKey(string expression, FileItemContext item)
    {
        string result = expression;

        string nameNoExt = Path.GetFileNameWithoutExtension(item.FileName);
        string ext = Path.GetExtension(item.FileName);

        result = result.Replace("{FileName}", item.FileName, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{FileNameWithoutExtension}", nameNoExt, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{Name}", nameNoExt, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{Extension}", ext, StringComparison.OrdinalIgnoreCase);

        foreach (var (k, v) in item.Metadata)
        {
            string token = $"{{{k}}}";
            string metaToken = $"{{Metadata:{k}}}";
            string vStr = v?.ToString() ?? string.Empty;
            result = result.Replace(token, vStr, StringComparison.OrdinalIgnoreCase);
            result = result.Replace(metaToken, vStr, StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim();
    }
}
