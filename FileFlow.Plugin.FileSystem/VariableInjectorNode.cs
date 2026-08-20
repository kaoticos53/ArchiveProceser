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
    public string Description => LocalizationManager.Instance.GetString("VariableInjectorNode_Desc", "Calcula e inyecta múltiples variables personalizadas en los metadatos del elemento para nodos posteriores.");

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
        ["Key1"] = "CustomCategory",
        ["Value1"] = "{FileNameNoExt}_processed",
        ["Key2"] = "CustomYear",
        ["Value2"] = "{Year(DateNow)}",
        ["Key3"] = "",
        ["Value3"] = "",
        ["Key4"] = "",
        ["Value4"] = "",
        ["Key5"] = "",
        ["Value5"] = ""
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var (k, v) in Parameters)
        {
            if (k.StartsWith("Key", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = k["Key".Length..];
                string varName = v?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(varName))
                {
                    string valKey = "Value" + suffix;
                    string exprValue = Parameters.TryGetValue(valKey, out var valObj) ? valObj?.ToString() ?? string.Empty : string.Empty;
                    string resolvedValue = VariableTemplateResolver.Resolve(exprValue, item);
                    item.Metadata[varName] = resolvedValue;

                    context.Log($"[Inyector de Variables] Inyectado '{varName}' = '{resolvedValue}'", LogLevel.Information);
                    item.AddLog($"VariableInjectorNode inyectó {varName}={resolvedValue}");
                }
            }
        }

        await context.EmitAsync("Out", item);
    }
}
