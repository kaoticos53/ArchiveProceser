using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("BatchBufferNode_Name", "Logic", "BatchBufferNode_Desc")]
public class BatchBufferNode : IFlowNode
{
    private readonly List<FileItemContext> _buffer = [];
    private readonly Lock _lock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("BatchBufferNode_Name", "Agrupador de Lotes (Batch Buffer)");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("BatchBufferNode_Desc", "Acumula archivos entrantes en memoria hasta alcanzar una cantidad de N elementos o un tamaño total en MB antes de liberarlos juntos, optimizando procesos por lotes.");


    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("ItemIn", typeof(FileItemContext), PortDirection.Input, "ItemIn"),
        new NodePort("ForceFlush", typeof(FileItemContext), PortDirection.Input, "ForceFlush")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("ItemOut", typeof(FileItemContext), PortDirection.Output, "ItemOut"),
        new NodePort("BatchCompleted", typeof(FileItemContext), PortDirection.Output, "BatchCompleted")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BatchSize"] = 10,
        ["MaxBatchSizeBytes"] = 0L // 0 = disabled
    };

    private string? _lastExecutionId;

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int batchSize = Parameters.TryGetValue("BatchSize", out var bVal) ? ParameterHelper.GetInt32(bVal, 10) : 10;
        long maxSizeBytes = Parameters.TryGetValue("MaxBatchSizeBytes", out var mVal) ? Convert.ToInt64(mVal) : 0L;

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
            context.Log($"[BatchBufferNode] Emitting batch of {toEmit.Count} items.", LogLevel.Information);
            int idx = 1;
            foreach (var bufferedItem in toEmit)
            {
                bufferedItem.Metadata["BatchIndex"] = idx++;
                bufferedItem.Metadata["BatchSize"] = toEmit.Count;
                await context.EmitAsync("ItemOut", bufferedItem);
            }

            var markerItem = new FileItemContext(string.Empty);
            markerItem.Metadata["BatchSize"] = toEmit.Count;
            await context.EmitAsync("BatchCompleted", markerItem);
        }
    }
}
