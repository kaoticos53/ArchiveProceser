using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("VariableInjectorNode_Name", "Integrations", "VariableInjectorNode_Desc", PipelineRole.Control,
    "variables", "inyectar", "tokens", "metadata", "clave", "valor", "inject")]
public sealed class VariableInjectorNode : FlowNodeBase
{
    private readonly Lock _lock = new();

    public override string Name => LocalizationManager.Instance.GetString("VariableInjectorNode_Name", "Variable Injector");
    public override string Category => "Integrations";
    public override string Description => LocalizationManager.Instance.GetString("VariableInjectorNode_Desc", "Calculates and injects dynamic custom variables into item metadata for downstream nodes.");

    public VariableInjectorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
        ];

        Parameters["CustomCategory"] = "{FileNameNoExt}_processed";
    }

    public override IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("AddVariable", "➕ Variable", "➕", "Añadir nueva variable personalizada")
    ];

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        KeyValuePair<string, object?>[] snapshot;
        lock (_lock)
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
