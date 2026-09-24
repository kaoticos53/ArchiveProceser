using System.Collections.Concurrent;
using System.Diagnostics;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _nodeConcurrencyThrottles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pares (nodo, puerto) ya avisados en esta ejecución. Un nodo puede emitir miles de ítems por el mismo
    /// puerto mal escrito, y el aviso tiene que explicar el defecto una vez, no una vez por archivo.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _warnedUndeclaredPorts = new(StringComparer.OrdinalIgnoreCase);

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
    /// Olvida los avisos de puerto de la ejecución anterior: el mismo defecto vuelve a contarse en el siguiente
    /// flujo, en lugar de quedar silenciado por la ejecución que ya terminó.
    /// </summary>
    public void ResetDiagnostics() => _warnedUndeclaredPorts.Clear();

    public Task DispatchEmitAsync(
        string sourceNodeId,
        string outputPortName,
        FileItemContext item,
        ConcurrentDictionary<string, IFlowNode> nodeInstances,
        ConcurrentDictionary<string, WorkflowEdge[]> indexedPortEdges,
        HashSet<string> startNodeIds,
        string executionId,
        string globalOutputDir,
        bool isDryRun,
        WorkflowDebugSession? debugSession,
        SemaphoreSlim concurrencyThrottle,
        Func<CancellationToken, Task> waitIfPausedAsync,
        CancellationToken cancellationToken,
        string temporaryDirectory = "")
    {
        item.Metadata["WorkflowExecutionId"] = executionId;
        if (!string.IsNullOrWhiteSpace(globalOutputDir))
        {
            item.Metadata["GlobalOutputDir"] = globalOutputDir;
        }

        if (!string.IsNullOrWhiteSpace(temporaryDirectory))
        {
            item.Metadata["TemporaryDirectory"] = temporaryDirectory;
        }

        if (isDryRun)
        {
            item.Metadata["DryRun"] = true;
        }

        if (debugSession != null)
        {
            debugSession.RecordSnapshot(NodeDataSnapshot.CreateOutput(sourceNodeId, outputPortName, item));
        }

        string fileKey = !string.IsNullOrWhiteSpace(item.OriginalPath) 
            ? item.OriginalPath 
            : (!string.IsNullOrWhiteSpace(item.CurrentPath) ? item.CurrentPath : item.IdString);

        if (startNodeIds.Contains(sourceNodeId))
        {
            _telemetryTracker.IncrementSourceItemsEmitted();

            if (_checkpointHandler.IsFileAlreadyCompleted(item.OriginalPath))
            {
                _executor.NotifyLog(LocalizationManager.Instance.GetFormattedString("Log_CheckpointSkippingFile", "[Checkpoint] Skipping previously completed file: {0}", item.FileName), LogLevel.Debug);
                _telemetryTracker.IncrementCompletedFiles(fileKey);
                return Task.CompletedTask;
            }
        }

        WarnIfEmitPortIsNotDeclared(sourceNodeId, outputPortName, nodeInstances);

        string edgeKey = $"{sourceNodeId}:{outputPortName}";
        if (!indexedPortEdges.TryGetValue(edgeKey, out var matchingEdges) || matchingEdges.Length == 0)
        {
            long doneFiles = _telemetryTracker.IncrementCompletedFiles(fileKey);
            _checkpointHandler.RecordCompletedFile(fileKey, doneFiles);
            return Task.CompletedTask;
        }

        _telemetryTracker.AddTotalItems(matchingEdges.Length);
        if (item.FileSizeBytes > 0)
        {
            _telemetryTracker.AddProcessedBytes(item.FileSizeBytes);
        }

        bool isMultipleTargets = matchingEdges.Length > 1;

        int newCount = _edgeCounts.AddOrUpdate(edgeKey, 1, static (_, c) => c + 1);
        EdgeItemDispatched?.Invoke(sourceNodeId, outputPortName, newCount);

        foreach (var edge in matchingEdges)
        {
            if (nodeInstances.TryGetValue(edge.TargetNodeId, out var targetNode))
            {
                var targetItem = isMultipleTargets ? item.DeepClone() : item;

                var task = Task.Run(async () =>
                {
                    SemaphoreSlim? nodeThrottle = null;
                    if (targetNode.MaxConcurrency > 0)
                    {
                        nodeThrottle = _nodeConcurrencyThrottles.GetOrAdd(targetNode.Id, _ => new SemaphoreSlim(targetNode.MaxConcurrency, targetNode.MaxConcurrency));
                        await nodeThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    bool concurrencyAcquired = false;
                    try
                    {
                        await concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
                        concurrencyAcquired = true;

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
                            long currentCompleted = _telemetryTracker.CompletedFilesCount;
                            if (!string.IsNullOrWhiteSpace(targetItem.FileName) && (currentCompleted <= 1 || currentCompleted % 10 == 0))
                            {
                                long totalFiles = _telemetryTracker.ExpectedTotalItems;
                                long effective = Math.Max(totalFiles, currentCompleted);
                                double pct = effective > 0 ? (double)currentCompleted / effective * 100.0 : 0.0;
                                if (_executor.IsRunning && pct >= 100.0) pct = 99.0;
                                _executor.NotifyProgress(pct, $"⚡ {targetNode.Name}: {targetItem.FileName}");
                            }

                            long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                            long startTicks = Stopwatch.GetTimestamp();
                            try
                            {
                                await targetNode.ExecuteAsync(edge.TargetPortName, targetItem, targetContext, cancellationToken).ConfigureAwait(false);
                                double elapsedMs = targetContext.CustomExecutionDurationMs ?? Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
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
                                double elapsedMs = targetContext.CustomExecutionDurationMs ?? Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
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
                            _telemetryTracker.IncrementProcessedItems();

                            if (!targetContext.HasEmittedAnyDownstream)
                            {
                                string targetFileKey = !string.IsNullOrWhiteSpace(targetItem.OriginalPath) 
                                    ? targetItem.OriginalPath 
                                    : (!string.IsNullOrWhiteSpace(targetItem.CurrentPath) ? targetItem.CurrentPath : targetItem.IdString);

                                long doneFiles = _telemetryTracker.IncrementCompletedFiles(targetFileKey);
                                long totalFiles = _telemetryTracker.ExpectedTotalItems;
                                long effective = Math.Max(totalFiles, doneFiles);
                                double pct = effective > 0 ? (double)doneFiles / effective * 100.0 : 0.0;
                                if (_executor.IsRunning && pct >= 100.0) pct = 99.0;
                                else if (pct > 100.0) pct = 100.0;

                                if (doneFiles == 1 || doneFiles == effective || doneFiles % 10 == 0)
                                {
                                    _executor.NotifyProgress(pct, LocalizationManager.Instance.GetFormattedString("Log_ProcessingItemsProgress", "⚡ Processing: {0:N0}/{1:N0} items ({2:F0}%)", doneFiles, effective, pct));
                                }

                                _checkpointHandler.RecordCompletedFile(targetFileKey, doneFiles);
                            }
                        }
                    }
                    finally
                    {
                        if (concurrencyAcquired)
                        {
                            concurrencyThrottle.Release();
                        }
                        nodeThrottle?.Release();
                    }
                }, cancellationToken);

                _taskTracker.TrackTask(task);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Avisa cuando un nodo emite por un puerto que <b>no declara</b>. Es el defecto que dejaba una rama entera
    /// muerta sin decir nada: la interfaz sólo puede dibujar cables desde los puertos declarados, así que un
    /// nombre mal escrito en <c>EmitAsync</c> no tiene arista que lo recoja, y el motor lo trataba como si el
    /// ítem hubiera terminado —sin error, sin log y sin nodos descendentes—. Ocurrió en producción con el nodo
    /// Fan-Out, que emitía en <c>ItemOut</c> mientras declaraba <c>Out</c>.
    ///
    /// <para>Sólo se juzga a los nodos que declaran algún puerto: un nodo sin puertos de salida no tiene
    /// contra qué compararse, y los puertos dinámicos (un switch, un subflujo) se consultan ya materializados
    /// en la instancia, que es la que conoce sus nombres reales.</para>
    /// </summary>
    private void WarnIfEmitPortIsNotDeclared(
        string sourceNodeId,
        string outputPortName,
        ConcurrentDictionary<string, IFlowNode> nodeInstances)
    {
        if (!nodeInstances.TryGetValue(sourceNodeId, out var sourceNode)) return;

        var declaredOutputs = sourceNode.Outputs;
        if (declaredOutputs == null || declaredOutputs.Count == 0) return;

        if (declaredOutputs.Any(p => string.Equals(p.Name, outputPortName, StringComparison.OrdinalIgnoreCase))) return;

        if (!_warnedUndeclaredPorts.TryAdd($"{sourceNodeId}:{outputPortName}", 0)) return;

        _executor.NotifyLog(
            sourceNodeId,
            LocalizationManager.Instance.GetFormattedString(
                "Log_UndeclaredOutputPort",
                "[Engine] Node '{0}' emitted an item on port '{1}', which it does not declare: the item did not reach any downstream node.",
                sourceNode.Name,
                outputPortName),
            LogLevel.Warning);
    }
}
