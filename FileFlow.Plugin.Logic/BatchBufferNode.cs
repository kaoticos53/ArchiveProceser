using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("BatchBufferNode_Name", "Logic", "BatchBufferNode_Desc", PipelineRole.Control,
    "lote", "batch", "buffer", "acumular", "paquete", "buffer", "aggregate")]
public sealed class BatchBufferNode : FlowNodeBase
{
    private readonly List<FileItemContext> _buffer = [];
    private readonly Lock _lock = new();

    public override string Name => LocalizationManager.Instance.GetString("BatchBufferNode_Name", "Agrupador de Lotes (Batch Buffer)");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("BatchBufferNode_Desc", "Acumula archivos entrantes en memoria hasta alcanzar una cantidad de N elementos o un tamaño total en MB antes de liberarlos juntos, optimizando procesos por lotes.");

    public BatchBufferNode()
    {
        Inputs =
        [
            new NodePort("ItemIn", typeof(FileItemContext), PortDirection.Input, "ItemIn"),
            new NodePort("ForceFlush", typeof(FileItemContext), PortDirection.Input, "ForceFlush")
        ];

        Outputs =
        [
            new NodePort("ItemOut", typeof(FileItemContext), PortDirection.Output, "ItemOut"),
            new NodePort("BatchCompleted", typeof(FileItemContext), PortDirection.Output, "BatchCompleted")
        ];

        Parameters["BatchSize"] = 10;
        Parameters["MaxBatchSizeBytes"] = 0L; // 0 = disabled
    }

    private string? _lastExecutionId;

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int batchSize = GetParameter("BatchSize", 10);
        long maxSizeBytes = GetParameter("MaxBatchSizeBytes", 0L);

        List<FileItemContext>? toEmit = null;

        lock (_lock)
        {
            if (item.Metadata.TryGetValue("WorkflowExecutionId", out var execIdObj) && execIdObj?.ToString() is string execId && _lastExecutionId != execId)
            {
                _lastExecutionId = execId;
                _buffer.Clear();
            }

            if (inputPortName.Equals("ForceFlush", StringComparison.OrdinalIgnoreCase))
            {
                if (_buffer.Count > 0)
                {
                    toEmit = [.. _buffer];
                    _buffer.Clear();
                }
            }
            else
            {
                _buffer.Add(item);

                long currentTotalBytes = _buffer.Sum(b => b.FileSizeBytes);
                if (_buffer.Count >= batchSize || (maxSizeBytes > 0 && currentTotalBytes >= maxSizeBytes))
                {
                    toEmit = [.. _buffer];
                    _buffer.Clear();
                }
            }
        }

        if (toEmit != null && toEmit.Count > 0)
        {
            await EmitBatchAsync(toEmit, context, incomplete: false);
        }
    }

    /// <summary>
    /// <b>El último lote sale también cuando no llegó a llenarse.</b>
    ///
    /// <para>Un búfer que sólo suelta al alcanzar el umbral retiene todo lo que no llegue a llenarlo, y ese
    /// pendiente muere con la ejecución: el flujo termina en verde sin haber entregado nada. Pasa en cuanto la
    /// entrada es más pequeña que el lote —seis archivos con un lote de diez—, que es el caso normal, no el
    /// raro: el umbral es un tope para no acumular sin fin, no un requisito para entregar.</para>
    ///
    /// <para>El motor invoca este gancho con todos los nodos aguas arriba ya drenados y su propia fase de
    /// drenado posterior, así que lo que salga de aquí —los elementos pendientes por <c>ItemOut</c> y su
    /// marcador <c>BatchCompleted</c>, igual que en un lote completo— recorre el resto del flujo y termina en su
    /// destino (mismo patrón que <c>ArchiveFanInNode</c> con sus sesiones a medias).</para>
    /// </summary>
    public override async Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        List<FileItemContext>? pending;
        lock (_lock)
        {
            pending = _buffer.Count > 0 ? [.. _buffer] : null;
            _buffer.Clear();
        }

        if (pending is null)
        {
            return;
        }

        await EmitBatchAsync(pending, context, incomplete: true);
    }

    /// <summary>
    /// Suelta un lote: los elementos por <c>ItemOut</c> (numerados dentro del lote) y un marcador por
    /// <c>BatchCompleted</c> que dice cuántos salieron y si el lote se cerró por umbral o por fin de ejecución.
    /// Un único camino de salida para el lote completo y para el incompleto: lo que se entrega no puede
    /// depender de por qué se entregó.
    /// </summary>
    private async Task EmitBatchAsync(
        List<FileItemContext> batch,
        IFlowExecutionContext context,
        bool incomplete)
    {
        long totalBytes = batch.Sum(b => b.FileSizeBytes);
        double totalMB = totalBytes / (1024.0 * 1024.0);
        string detailsJson = $"{{\"batchCount\": {batch.Count}, \"totalSizeBytes\": {totalBytes}, \"totalMB\": {totalMB.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, \"incomplete\": {(incomplete ? "true" : "false")}}}";

        string message = incomplete
            ? $"[Buffer Lotes] Lote incompleto entregado al terminar el flujo: {batch.Count:N0} elementos ({totalMB:F2} MB)"
            : $"[Buffer Lotes] Emitiendo lote consolidado de {batch.Count:N0} elementos ({totalMB:F2} MB)";

        context.Log(message, LogLevel.Information, batch[^1], durationMs: 0.0, detailsJson: detailsJson);

        int idx = 1;
        foreach (var bufferedItem in batch)
        {
            bufferedItem.Metadata["BatchIndex"] = idx++;
            bufferedItem.Metadata["BatchSize"] = batch.Count;
            await context.EmitAsync("ItemOut", bufferedItem);
        }

        var markerItem = new FileItemContext(string.Empty);
        markerItem.Metadata["BatchSize"] = batch.Count;
        markerItem.Metadata["BatchIncomplete"] = incomplete;
        await context.EmitAsync("BatchCompleted", markerItem);
    }
}
