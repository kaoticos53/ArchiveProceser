using System.Collections.Concurrent;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("ForkJoinBarrierNode_Name", "Logic", "ForkJoinBarrierNode_Desc")]
public class ForkJoinBarrierNode : IFlowNode
{
    private sealed class BarrierState
    {
        public FileItemContext OriginalItem { get; init; } = null!;
        public HashSet<string> CompletedBranches { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConcurrentDictionary<Guid, BarrierState> _activeBarriers = new();
    private readonly Lock _lock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ForkJoinBarrierNode_Name", "Barrera de Sincronización (Fork & Join)");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("ForkJoinBarrierNode_Desc", "Bifurca un archivo hacia múltiples ramas paralelas independientes y actúa como barrera de sincronización, esperando a que todas las ramas finalicen su tarea antes de liberar el flujo.");


    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In"),
        new NodePort("Branch1_Done", typeof(FileItemContext), PortDirection.Input, "Branch1_Done"),
        new NodePort("Branch2_Done", typeof(FileItemContext), PortDirection.Input, "Branch2_Done")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Fork1", typeof(FileItemContext), PortDirection.Output, "Fork1"),
        new NodePort("Fork2", typeof(FileItemContext), PortDirection.Output, "Fork2"),
        new NodePort("AllCompleted", typeof(FileItemContext), PortDirection.Output, "AllCompleted")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RequiredBranchesCount"] = 2
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int requiredBranches = Parameters.TryGetValue("RequiredBranchesCount", out var rVal) ? ParameterHelper.GetInt32(rVal, 2) : 2;

        if (inputPortName.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            _activeBarriers[item.Id] = new BarrierState { OriginalItem = item };
            await context.EmitAsync("Fork1", item.DeepClone());
            await context.EmitAsync("Fork2", item.DeepClone());
            return;
        }

        // Branch completion reported
        if (_activeBarriers.TryGetValue(item.Id, out var state))
        {
            bool isAllDone = false;
            FileItemContext finalItem;

            lock (_lock)
            {
                state.CompletedBranches.Add(inputPortName);
                if (state.CompletedBranches.Count >= requiredBranches)
                {
                    _activeBarriers.TryRemove(item.Id, out _);
                    isAllDone = true;
                }
            }

            if (isAllDone)
            {
                finalItem = state.OriginalItem;
                finalItem.AddLog($"ForkJoinBarrier: All {requiredBranches} branches completed.");
                context.Log($"[ForkJoinBarrierNode] Item {item.Id} completed all parallel branches.", LogLevel.Information);
                await context.EmitAsync("AllCompleted", finalItem);
            }
        }
    }
}
