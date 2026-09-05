using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("VariableInjectorNode_Name", "Integrations", "VariableInjectorNode_Desc", PipelineRole.Control,
    "variables", "inyectar", "tokens", "metadata", "clave", "valor", "inject")]
public class VariableInjectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("VariableInjectorNode_Name", "Variable Injector");
    public string Category => "Integrations";
    public string Description => LocalizationManager.Instance.GetString("VariableInjectorNode_Desc", "Calculates and injects dynamic custom variables into item metadata for downstream nodes.");

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

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("AddVariable", "➕ Variable", "➕", "Añadir nueva variable personalizada")
    ];

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

        var injectedMap = new Dictionary<string, string>();

        foreach (var (key, value) in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string cleanKey = System.Text.RegularExpressions.Regex.Replace(key, @"[^\w]", "_");
            if (string.IsNullOrWhiteSpace(cleanKey) || cleanKey.All(c => c == '_')) continue;

            string exprValue = value?.ToString() ?? string.Empty;
            string resolvedValue = VariableTemplateResolver.Resolve(exprValue, item);
            item.Metadata[cleanKey] = resolvedValue;
            injectedMap[cleanKey] = resolvedValue;

            context.Log(LocalizationManager.Instance.GetFormattedString("Log_VarInjector_Var", "[Variable Injector] Variable '{0}' = '{1}'", cleanKey, resolvedValue), LogLevel.Debug, item);
            item.AddLog($"VariableInjectorNode injected {cleanKey}={resolvedValue}");
        }

        if (injectedMap.Count > 0)
        {
            string detailsJson = System.Text.Json.JsonSerializer.Serialize(injectedMap);
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_VarInjector_Injected", "[Variable Injector] Injected {0} variables into metadata", injectedMap.Count), LogLevel.Information, item, durationMs: 0.0, detailsJson: detailsJson);
        }

        await context.EmitAsync("Out", item);
    }
}
