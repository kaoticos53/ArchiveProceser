using System.Collections.Concurrent;
using System.Diagnostics;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Despachador y enrutador concurrente de elementos a través de las aristas del grafo DAG.
/// </summary>
public sealed class WorkflowItemDispatcher
{
    private readonly WorkflowExecutor _executor;
    private readonly WorkflowTelemetryTracker _telemetryTracker;
    private readonly WorkflowTaskTracker _taskTracker;
    private readonly WorkflowCheckpointHandler _checkpointHandler;
    private readonly ConcurrentDictionary<string, int> _edgeCounts = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, string, int>? EdgeItemDispatched;

    public WorkflowItemDispatcher(
        WorkflowExecutor executor,
        WorkflowTelemetryTracker telemetryTracker,
        WorkflowTaskTracker taskTracker,
        WorkflowCheckpointHandler checkpointHandler)
    {
        _executor = executor;
        _telemetryTracker = telemetryTracker;
        _taskTracker = taskTracker;
        _checkpointHandler = checkpointHandler;
    }

    /// <summary>
    /// Despacha un elemento emitido desde un puerto de salida hacia todos los puertos destino conectados.
    /// </summary>
    public Task DispatchEmitAsync(
        string sourceNodeId,
        string outputPortName,
        FileItemContext item,
        ConcurrentDictionary<string, IFlowNode> nodeInstances,
        ConcurrentDictionary<string, List<WorkflowEdge>> outgoingEdges,
        HashSet<string> startNodeIds,
        string executionId,
        string globalOutputDir,
        bool isDryRun,
        WorkflowDebugSession? debugSession,
        SemaphoreSlim concurrencyThrottle,
        Func<CancellationToken, Task> waitIfPausedAsync,
        CancellationToken cancellationToken)
    {
        item.Metadata["WorkflowExecutionId"] = executionId;
        if (!string.IsNullOrWhiteSpace(globalOutputDir))
        {
            item.Metadata["GlobalOutputDir"] = globalOutputDir;
        }

        if (isDryRun)
        {
            item.Metadata["DryRun"] = true;
        }

        if (debugSession != null)
        {
            debugSession.RecordSnapshot(NodeDataSnapshot.CreateOutput(sourceNodeId, outputPortName, item));
        }

        if (startNodeIds.Contains(sourceNodeId))
        {
            _telemetryTracker.IncrementSourceItemsEmitted();

            if (_checkpointHandler.IsFileAlreadyCompleted(item.OriginalPath))
            {
                _executor.NotifyLog($"[Checkpoint] Omitiendo archivo completado previamente: {item.FileName}", LogLevel.Debug);
                _telemetryTracker.IncrementCompletedFiles();
                return Task.CompletedTask;
            }
        }

        if (!outgoingEdges.TryGetValue(sourceNodeId, out var edges))
        {
            long doneFiles = _telemetryTracker.IncrementCompletedFiles();
            _checkpointHandler.RecordCompletedFile(item.OriginalPath, doneFiles);
            return Task.CompletedTask;
        }

        var matchingEdges = edges.Where(e => e.SourcePortName.Equals(outputPortName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingEdges.Count == 0)
        {
            long doneFiles = _telemetryTracker.IncrementCompletedFiles();
            _checkpointHandler.RecordCompletedFile(item.OriginalPath, doneFiles);
            return Task.CompletedTask;
        }

        _telemetryTracker.AddTotalItems(matchingEdges.Count);
        if (item.FileSizeBytes > 0)
        {
            _telemetryTracker.AddProcessedBytes(item.FileSizeBytes);
        }

        bool isMultipleTargets = matchingEdges.Count > 1;

        string edgeKey = $"{sourceNodeId}:{outputPortName}";
        int newCount = _edgeCounts.AddOrUpdate(edgeKey, 1, (_, c) => c + 1);
        EdgeItemDispatched?.Invoke(sourceNodeId, outputPortName, newCount);

        foreach (var edge in matchingEdges)
        {
            if (nodeInstances.TryGetValue(edge.TargetNodeId, out var targetNode))
            {
                var targetItem = isMultipleTargets ? item.DeepClone() : item;

                var task = Task.Run(async () =>
                {
                    await concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var targetContext = new WorkflowExecutionContext(targetNode.Id, _executor, cancellationToken, targetItem);
                    try
                    {
                        await waitIfPausedAsync(cancellationToken).ConfigureAwait(false);

                        if (debugSession != null)
                        {
                            debugSession.RecordSnapshot(NodeDataSnapshot.CreateInput(targetNode.Id, edge.TargetPortName, targetItem));
                            await debugSession.CheckBreakpointOrStepAsync(targetNode.Id, edge.TargetPortName, targetItem, cancellationToken).ConfigureAwait(false);
                        }

                        _executor.NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Running);
                        if (!string.IsNullOrWhiteSpace(targetItem.FileName))
                        {
                            long doneFiles = _telemetryTracker.CompletedFilesCount;
                            long totalFiles = _telemetryTracker.ExpectedTotalItems;
                            long effective = Math.Max(totalFiles, doneFiles);
                            double pct = effective > 0 ? (double)doneFiles / effective * 100.0 : 0.0;
                            if (_executor.IsRunning && pct >= 100.0) pct = 99.0;
                            _executor.NotifyProgress(pct, $"⚡ {targetNode.Name}: {targetItem.FileName}");
                        }

                        long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                        long startTicks = Stopwatch.GetTimestamp();
                        try
                        {
                            await targetNode.ExecuteAsync(edge.TargetPortName, targetItem, targetContext, cancellationToken).ConfigureAwait(false);
                            double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                            long endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                            long allocatedBytes = Math.Max(0, endAllocatedBytes - startAllocatedBytes);
                            bool isGpu = targetItem.Metadata.ContainsKey("AI:DirectMlAccelerated") || 
                                         (targetItem.Metadata.TryGetValue("AI:Device", out var dev) && dev?.ToString()?.Contains("GPU", StringComparison.OrdinalIgnoreCase) == true) ||
                                         (targetNode is IModelLifecycleNode lifecycleNode && lifecycleNode.IsGpuAccelerated);

                            _telemetryTracker.RecordNodeExecution(targetNode.Id, elapsedMs, allocatedBytes, 0.0, isGpu);
                            _executor.NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Completed);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                            long endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                            long allocatedBytes = Math.Max(0, endAllocatedBytes - startAllocatedBytes);
                            _telemetryTracker.RecordNodeExecution(targetNode.Id, elapsedMs, allocatedBytes, 0.0, false);
                            _executor.NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Faulted);
                            if (debugSession != null)
                            {
                                await debugSession.HandleNodeErrorAsync(targetNode.Id, edge.TargetPortName, targetItem, ex, cancellationToken).ConfigureAwait(false);
                            }
                            throw;
                        }
                    }
                    finally
                    {
                        concurrencyThrottle.Release();
                        _telemetryTracker.IncrementProcessedItems();

                        if (!targetContext.HasEmittedAnyDownstream)
                        {
                            long doneFiles = _telemetryTracker.IncrementCompletedFiles();
                            long totalFiles = _telemetryTracker.ExpectedTotalItems;
                            long effective = Math.Max(totalFiles, doneFiles);
                            double pct = effective > 0 ? (double)doneFiles / effective * 100.0 : 0.0;
                            if (_executor.IsRunning && pct >= 100.0) pct = 99.0;
                            else if (pct > 100.0) pct = 100.0;
                            _executor.NotifyProgress(pct, $"⚡ Procesando: {doneFiles:N0}/{effective:N0} elementos ({pct:F0}%)");

                            _checkpointHandler.RecordCompletedFile(targetItem.OriginalPath, doneFiles);
                        }
                    }
                }, cancellationToken);

                _taskTracker.TrackTask(task);
            }
        }

        return Task.CompletedTask;
    }
}
