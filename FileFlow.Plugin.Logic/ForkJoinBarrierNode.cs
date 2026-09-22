using System.Collections.Concurrent;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("ForkJoinBarrierNode_Name", "Logic", "ForkJoinBarrierNode_Desc", PipelineRole.Control,
    "fork", "join", "sincronizar", "barrera", "paralelo", "barrier", "merge")]
public sealed class ForkJoinBarrierNode : FlowNodeBase
{
    private sealed class BarrierState
    {
        public FileItemContext OriginalItem { get; init; } = null!;
        public HashSet<string> CompletedBranches { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConcurrentDictionary<Guid, BarrierState> _activeBarriers = new();
    private readonly Lock _lock = new();

    public override string Name => LocalizationManager.Instance.GetString("ForkJoinBarrierNode_Name", "Barrera de Sincronización (Fork & Join)");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("ForkJoinBarrierNode_Desc", "Bifurca un archivo hacia múltiples ramas paralelas independientes y actúa como barrera de sincronización, esperando a que todas las ramas finalicen su tarea antes de liberar el flujo.");

    public ForkJoinBarrierNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In"),
            new NodePort("Branch1_Done", typeof(FileItemContext), PortDirection.Input, "Branch1_Done"),
            new NodePort("Branch2_Done", typeof(FileItemContext), PortDirection.Input, "Branch2_Done")
        ];

        Outputs =
        [
            new NodePort("Fork1", typeof(FileItemContext), PortDirection.Output, "Fork1"),
            new NodePort("Fork2", typeof(FileItemContext), PortDirection.Output, "Fork2"),
            new NodePort("AllCompleted", typeof(FileItemContext), PortDirection.Output, "AllCompleted")
        ];

        Parameters["RequiredBranchesCount"] = 2;
    }

    private string? _lastExecutionId;

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (item.Metadata.TryGetValue("WorkflowExecutionId", out var execIdObj) && execIdObj?.ToString() is string execId && _lastExecutionId != execId)
        {
            _lastExecutionId = execId;
            _activeBarriers.Clear();
        }

        int requiredBranches = GetParameter("RequiredBranchesCount", 2);

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
                // Merge metadata from completing branch
                foreach (var (k, v) in item.Metadata)
                {
                    state.OriginalItem.Metadata[k] = v;
                }

                // Update current path if modified in branch
                if (!string.IsNullOrWhiteSpace(item.CurrentPath) &&
                    !string.Equals(item.CurrentPath, state.OriginalItem.CurrentPath, StringComparison.OrdinalIgnoreCase))
                {
                    state.OriginalItem.CurrentPath = item.CurrentPath;
                }

                // Merge logs
                foreach (var log in item.ExecutionLog)
                {
                    if (!state.OriginalItem.ExecutionLog.Contains(log))
                    {
                        state.OriginalItem.ExecutionLog.Add(log);
                    }
                }

                state.CompletedBranches.Add(inputPortName);
                context.Log($"[Barrera ForkJoin] Rama '{inputPortName}' completada ({state.CompletedBranches.Count}/{requiredBranches})", LogLevel.Debug, item);

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
                string detailsJson = $"{{\"requiredBranches\": {requiredBranches}, \"completedBranches\": [\"{string.Join("\", \"", state.CompletedBranches)}\"]}}";
                context.Log($"[Barrera ForkJoin] Convergencia total de {requiredBranches} ramas paralelas completada con éxito", LogLevel.Information, finalItem, durationMs: 0.0, detailsJson: detailsJson);
                await context.EmitAsync("AllCompleted", finalItem);
            }
        }
    }
}
