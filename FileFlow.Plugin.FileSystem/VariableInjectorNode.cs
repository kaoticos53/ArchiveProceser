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
    public string Description => LocalizationManager.Instance.GetString("VariableInjectorNode_Desc", "Calcula e inyecta variables personalizadas dinámicas en los metadatos del elemento para nodos posteriores.");

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
        ["CustomCategory"] = "{FileNameNoExt}_processed"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        KeyValuePair<string, object?>[] snapshot;
        lock (Parameters)
        {
            snapshot = Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Key)).ToArray();
        }

        foreach (var (key, value) in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string exprValue = value?.ToString() ?? string.Empty;
            string resolvedValue = VariableTemplateResolver.Resolve(exprValue, item);
            item.Metadata[key] = resolvedValue;

            context.Log($"[Inyector de Variables] Inyectado '{key}' = '{resolvedValue}'", LogLevel.Information);
            item.AddLog($"VariableInjectorNode inyectó {key}={resolvedValue}");
        }

        await context.EmitAsync("Out", item);
    }
}
