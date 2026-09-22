using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("ThrottleDelayNode_Name", "Logic", "ThrottleDelayNode_Desc", PipelineRole.Control,
    "retardo", "pausa", "esperar", "delay", "throttle", "jitter", "sleep")]
public sealed class ThrottleDelayNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ThrottleDelayNode_Name", "Control de Tasa y Pausa (Throttle)");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("ThrottleDelayNode_Desc", "Regula y desacelera la velocidad del flujo de archivos introduciendo una pausa controlada en milisegundos entre elementos para evitar saturar discos HDD, CPUs o conexiones de red.");

    public ThrottleDelayNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
        ];

        Parameters["DelayMilliseconds"] = 100;
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int delayMs = GetParameter("DelayMilliseconds", 100);
        if (delayMs > 0)
        {
            context.Log($"[Throttle] Aplicando retardo de regulación: {delayMs} ms", LogLevel.Debug, item, durationMs: delayMs);
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        await context.EmitAsync("Out", item);
    }
}
