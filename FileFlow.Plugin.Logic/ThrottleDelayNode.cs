using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("ThrottleDelayNode_Name", "Logic", "ThrottleDelayNode_Desc")]
public class ThrottleDelayNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ThrottleDelayNode_Name", "Control de Tasa y Pausa (Throttle)");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("ThrottleDelayNode_Desc", "Regula y desacelera la velocidad del flujo de archivos introduciendo una pausa controlada en milisegundos entre elementos para evitar saturar discos HDD, CPUs o conexiones de red.");


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
        ["DelayMilliseconds"] = 100
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int delayMs = Parameters.TryGetValue("DelayMilliseconds", out var dVal) ? ParameterHelper.GetInt32(dVal, 100) : 100;
        if (delayMs > 0)
        {
            context.Log($"[Throttle] Aplicando retardo de regulación: {delayMs} ms", LogLevel.Debug, item, durationMs: delayMs);
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        await context.EmitAsync("Out", item);
    }
}
