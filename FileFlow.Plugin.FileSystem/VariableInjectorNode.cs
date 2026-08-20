using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("VariableInjectorNode_Name", "Utility", "VariableInjectorNode_Desc")]
public class VariableInjectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("VariableInjectorNode_Name", "Inyector de Variables");
    public string Category => "Utility";
    public string Description => LocalizationManager.Instance.GetString("VariableInjectorNode_Desc", "Calcula e inyecta una variable personalizada en los metadatos del elemento para nodos posteriores.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VariableName"] = "CustomKey",
        ["ExpressionValue"] = "{FileNameNoExt}_processed"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string varName = Parameters.TryGetValue("VariableName", out var nameVal) ? nameVal?.ToString() ?? "CustomKey" : "CustomKey";
        string exprValue = Parameters.TryGetValue("ExpressionValue", out var exprVal) ? exprVal?.ToString() ?? string.Empty : string.Empty;

        if (!string.IsNullOrWhiteSpace(varName))
        {
            string resolvedValue = VariableTemplateResolver.Resolve(exprValue, item);
            item.Metadata[varName] = resolvedValue;

            context.Log($"[Inyector de Variables] Inyectado '{varName}' = '{resolvedValue}'", LogLevel.Information);
            item.AddLog($"VariableInjectorNode inyectó {varName}={resolvedValue}");
        }

        await context.EmitAsync("Out", item);
    }
}
