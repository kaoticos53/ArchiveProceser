using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FileFlow.Core.Plugins;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Engine;

/// <summary>
/// Orquestador principal del grafo DAG para ejecución concurrente, depuración y simulación virtual.
/// </summary>
public class WorkflowExecutor
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, IFlowNode> _nodeInstances = new();
    private readonly ConcurrentDictionary<string, List<WorkflowEdge>> _outgoingEdges = new();
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private readonly WorkflowTelemetryTracker _telemetryTracker = new();

    private int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private SemaphoreSlim _concurrencyThrottle = new(Environment.ProcessorCount);
    private bool _isDryRun;
    private bool _isPaused;
    private bool _isRunning;
    private readonly HashSet<string> _startNodeIds = new(StringComparer.OrdinalIgnoreCase);

    public WorkflowDebugSession? DebugSession { get; set; }
    public ExecutionJournalService JournalService { get; } = new();
    public List<PlannedAction> PlannedActions { get; } = [];
    public WorkflowCheckpointData? Checkpoint { get; set; }
    public bool EnableCheckpointing { get; set; } = true;
    private readonly Lock _checkpointLock = new();

    public event Action<double, string>? ProgressChanged;
    public event Action<string, double, string>? NodeProgressChanged;
    public event Action<string, NodeExecutionStatus>? NodeStatusChanged;
    public event Action<string, LogLevel>? LogEmitted;
    public event Action<StructuredLogRecord>? StructuredLogEmitted;
    public event Action<string, string, int>? EdgeItemDispatched;

    private readonly ConcurrentDictionary<string, int> _edgeCounts = new(StringComparer.OrdinalIgnoreCase);

    public TelemetrySnapshot GetTelemetrySnapshot()
    {
        return _telemetryTracker.GetSnapshot(_isRunning);
    }

    public IReadOnlyDictionary<string, NodeTelemetryStats> GetNodeTelemetryStats()
    {
        return _telemetryTracker.GetNodeStats();
    }

    public void SetTotalExpectedItems(long totalExpectedItems)
    {
        _telemetryTracker.SetTotalExpectedItems(totalExpectedItems);
    }

    public void SetCustomStatusMessage(string message)
    {
        _telemetryTracker.SetCustomStatusMessage(message);
    }

    public void RegisterPlannedAction(PlannedAction action)
    {
        lock (_lock)
        {
            PlannedActions.Add(action);
        }
        NotifyLog($"[DryRun] Planned: {action.OperationType} -> {action.SourcePath} ({action.Description})", LogLevel.Information);
    }

    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set
        {
            lock (_lock)
            {
                int newCap = Math.Max(1, value);
                if (_maxDegreeOfParallelism != newCap)
                {
                    _maxDegreeOfParallelism = newCap;
                    _concurrencyThrottle?.Dispose();
                    _concurrencyThrottle = new SemaphoreSlim(_maxDegreeOfParallelism);
                }
            }
        }
    }

    public string GlobalOutputDir { get; set; } = string.Empty;

    public bool IsDryRun
    {
        get => _isDryRun;
        set => _isDryRun = value;
    }

    public bool IsPaused => _isPaused;

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isPaused)
            {
                _isPaused = true;
                _pauseSemaphore.Wait(0);
                NotifyLog("Execution paused.", LogLevel.Information);
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_isPaused)
            {
                _isPaused = false;
                if (_pauseSemaphore.CurrentCount == 0)
                {
                    _pauseSemaphore.Release();
                }
                NotifyLog("Execution resumed.", LogLevel.Information);
            }
        }
    }

    private string _currentExecutionId = Guid.NewGuid().ToString("N");
    private readonly Lock _tasksLock = new();
    private readonly List<Task> _activeNodeTasks = [];
    private readonly HashSet<string> _disabledLoggingNodeIds = new(StringComparer.OrdinalIgnoreCase);

    private void TrackTask(Task task)
    {
        lock (_tasksLock)
        {
            _activeNodeTasks.Add(task);
        }
    }

    public bool IsLoggingDisabledForNode(string? nodeId) => !string.IsNullOrWhiteSpace(nodeId) && _disabledLoggingNodeIds.Contains(nodeId);

    public void SetNodeLoggingEnabled(string nodeId, bool enabled)
    {
        lock (_lock)
        {
            if (enabled) _disabledLoggingNodeIds.Remove(nodeId);
            else _disabledLoggingNodeIds.Add(nodeId);
        }
    }

    public async Task ExecuteAsync(WorkflowGraph graph, PluginLoader loader, CancellationToken cancellationToken)
    {
        _currentExecutionId = Guid.NewGuid().ToString("N");
        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _disabledLoggingNodeIds.Clear();
        foreach (var disabledId in graph.DisabledLoggingNodeIds)
        {
            _disabledLoggingNodeIds.Add(disabledId);
        }
        foreach (var nodeDto in graph.Nodes.Where(n => !n.IsLoggingEnabled))
        {
            _disabledLoggingNodeIds.Add(nodeDto.Id);
        }

        _telemetryTracker.Reset();
        _isRunning = true;
        lock (_tasksLock)
        {
            _activeNodeTasks.Clear();
        }

        try
        {
            var validator = new GraphValidator();
            var validation = validator.Validate(graph, loader);

            if (!validation.IsValid)
            {
                foreach (var err in validation.Errors)
                {
                    NotifyLog($"Validation Error: {err}", LogLevel.Error);
                }
                throw new InvalidOperationException($"Workflow validation failed with {validation.Errors.Count} errors.");
            }

            // Sincronizar breakpoints si hay una sesión de depuración
            if (DebugSession != null)
            {
                DebugSession.SetBreakpoints(graph.BreakpointNodeIds);
            }

            // Build node dictionary & outgoing edges map
            foreach (var node in validation.TopologicalOrder)
            {
                _nodeInstances[node.Id] = node;
                _outgoingEdges[node.Id] = graph.Edges.Where(e => e.SourceNodeId == node.Id).ToList();
            }

            if (EnableCheckpointing && !IsDryRun && !string.IsNullOrWhiteSpace(graph.Name))
            {
                if (Checkpoint == null)
                {
                    if (WorkflowCheckpointManager.Instance.HasPendingCheckpoint(graph.Name, out var savedCp) && savedCp != null)
                    {
                        Checkpoint = savedCp;
                        NotifyLog($"[Checkpoint] Reanudando ejecución previa para '{graph.Name}' ({Checkpoint.CompletedFileKeys.Count} archivos ya completados).", LogLevel.Information);
                    }
                    else
                    {
                        Checkpoint = new WorkflowCheckpointData
                        {
                            WorkflowName = graph.Name,
                            ExecutionId = _currentExecutionId
                        };
                    }
                }
            }

            NotifyLog($"Starting workflow execution '{graph.Name}' with {validation.TopologicalOrder.Count} nodes (DryRun={IsDryRun}, Debug={DebugSession != null}).", LogLevel.Information);

            // Find entry nodes (nodes with no connected input edges)
            HashSet<string> targetNodeIds = graph.Edges.Select(e => e.TargetNodeId).ToHashSet();
            List<IFlowNode> startNodes = validation.TopologicalOrder.Where(n => !targetNodeIds.Contains(n.Id)).ToList();

            if (startNodes.Count == 0 && validation.TopologicalOrder.Count > 0)
            {
                startNodes.Add(validation.TopologicalOrder[0]);
            }

            _startNodeIds.Clear();
            foreach (var sn in startNodes)
            {
                _startNodeIds.Add(sn.Id);
            }

            List<Task> startTasks = [];
            foreach (var startNode in startNodes)
            {
                startTasks.Add(Task.Run(async () =>
                {
                    await WaitIfPausedAsync(cancellationToken);
                    // Trigger entry node with null or empty input port name
                    var dummyItem = new FileItemContext(string.Empty);
                    dummyItem.Metadata["WorkflowExecutionId"] = _currentExecutionId;
                    if (!string.IsNullOrWhiteSpace(GlobalOutputDir))
                    {
                        dummyItem.Metadata["GlobalOutputDir"] = GlobalOutputDir;
                    }

                    if (IsDryRun)
                    {
                        dummyItem.Metadata["DryRun"] = true;
                    }

                    var ctx = new WorkflowExecutionContext(startNode.Id, this, cancellationToken, dummyItem);

                    if (DebugSession != null)
                    {
                        DebugSession.RecordSnapshot(NodeDataSnapshot.CreateInput(startNode.Id, string.Empty, dummyItem));
                        await DebugSession.CheckBreakpointOrStepAsync(startNode.Id, string.Empty, dummyItem, cancellationToken);
                    }

                    NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Running);

                    long startTicks = Stopwatch.GetTimestamp();
                    try
                    {
                        await startNode.ExecuteAsync(string.Empty, dummyItem, ctx, cancellationToken);
                        double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                        _telemetryTracker.RecordNodeExecution(startNode.Id, elapsedMs);
                        NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Completed);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                        _telemetryTracker.RecordNodeExecution(startNode.Id, elapsedMs);
                        NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Faulted);
                        if (DebugSession != null)
                        {
                            await DebugSession.HandleNodeErrorAsync(startNode.Id, string.Empty, dummyItem, ex, cancellationToken);
                        }
                        throw;
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(startTasks).ConfigureAwait(false);

            // Wait for all asynchronously dispatched downstream node tasks to finish deterministically
            List<Exception> executionErrors = [];
            await DrainActiveTasksAsync(executionErrors).ConfigureAwait(false);

            // Notify all nodes about workflow completion (e.g. for aggregators, batch emitters, consolidated reports)
            var completionDummy = new FileItemContext(string.Empty);
            completionDummy.Metadata["WorkflowExecutionId"] = _currentExecutionId;
            if (!string.IsNullOrWhiteSpace(GlobalOutputDir)) completionDummy.Metadata["GlobalOutputDir"] = GlobalOutputDir;
            if (IsDryRun) completionDummy.Metadata["DryRun"] = true;

            foreach (var node in _nodeInstances.Values)
            {
                var completionContext = new WorkflowExecutionContext(node.Id, this, cancellationToken, completionDummy);
                try
                {
                    await node.OnWorkflowCompletedAsync(completionContext, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    executionErrors.Add(ex);
                }
            }

            // Drain any subsequent downstream tasks dispatched by completion hooks
            await DrainActiveTasksAsync(executionErrors).ConfigureAwait(false);

            if (executionErrors.Count > 0)
            {
                throw new AggregateException("Se produjeron errores durante la ejecución de los nodos del flujo.", executionErrors);
            }

            if (EnableCheckpointing && !IsDryRun && !string.IsNullOrWhiteSpace(graph.Name))
            {
                WorkflowCheckpointManager.Instance.ClearCheckpoint(graph.Name);
            }

            _telemetryTracker.Stop();
            long finalFiles = _telemetryTracker.CompletedFilesCount;
            if (finalFiles == 0) finalFiles = _telemetryTracker.SourceItemsEmitted;
            if (finalFiles == 0) finalFiles = _telemetryTracker.ProcessedItemsCount;
            NotifyProgress(100.0, $"🟢 Completado: {finalFiles:N0}/{finalFiles:N0} elementos (100%)");
            NotifyLog("Workflow execution completed successfully.", LogLevel.Information);
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Ejecuta el grafo en modo continuo de supervisión de carpetas (Watch Folder Trigger Mode).
    /// El motor inicializa el grafo y permanece escuchando los eventos de FolderWatcherService,
    /// inyectando cada archivo recién completado directamente en los nodos de entrada del DAG.
    /// </summary>
    public async Task ExecuteWatchModeAsync(
        WorkflowGraph graph,
        PluginLoader loader,
        FolderWatcherService watcherService,
        CancellationToken cancellationToken)
    {
        _currentExecutionId = Guid.NewGuid().ToString("N");
        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _disabledLoggingNodeIds.Clear();
        foreach (var disabledId in graph.DisabledLoggingNodeIds)
        {
            _disabledLoggingNodeIds.Add(disabledId);
        }
        foreach (var nodeDto in graph.Nodes.Where(n => !n.IsLoggingEnabled))
        {
            _disabledLoggingNodeIds.Add(nodeDto.Id);
        }

        _telemetryTracker.Reset();
        _isRunning = true;

        var validator = new GraphValidator();
        var validation = validator.Validate(graph, loader);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
            {
                NotifyLog($"Validation Error: {err}", LogLevel.Error);
            }
            throw new InvalidOperationException($"Workflow validation failed with {validation.Errors.Count} errors.");
        }

        foreach (var node in validation.TopologicalOrder)
        {
            _nodeInstances[node.Id] = node;
            _outgoingEdges[node.Id] = graph.Edges.Where(e => e.SourceNodeId == node.Id).ToList();
        }

        HashSet<string> targetNodeIds = graph.Edges.Select(e => e.TargetNodeId).ToHashSet();
        List<IFlowNode> startNodes = validation.TopologicalOrder.Where(n => !targetNodeIds.Contains(n.Id)).ToList();
        if (startNodes.Count == 0 && validation.TopologicalOrder.Count > 0)
        {
            startNodes.Add(validation.TopologicalOrder[0]);
        }

        _startNodeIds.Clear();
        foreach (var sn in startNodes)
        {
            _startNodeIds.Add(sn.Id);
        }

        NotifyLog($"Modo Vigilante Activo: Escuchando eventos en tiempo real para '{graph.Name}'...", LogLevel.Information);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var item = await watcherService.ItemReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (item == null) continue;

                NotifyLog($"[Watchdog] Disparando procesamiento para: {item.FileName}", LogLevel.Information);

                foreach (var startNode in startNodes)
                {
                    var itemClone = item.DeepClone();
                    itemClone.Metadata["WorkflowExecutionId"] = _currentExecutionId;
                    if (!string.IsNullOrWhiteSpace(GlobalOutputDir))
                    {
                        itemClone.Metadata["GlobalOutputDir"] = GlobalOutputDir;
                    }
                    if (IsDryRun)
                    {
                        itemClone.Metadata["DryRun"] = true;
                    }

                    var ctx = new WorkflowExecutionContext(startNode.Id, this, cancellationToken, itemClone);

                    TrackTask(Task.Run(async () =>
                    {
                        NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Running);
                        long startTicks = Stopwatch.GetTimestamp();
                        try
                        {
                            if (startNode.Inputs.Count == 0 && startNode.Outputs.Count > 0)
                            {
                                foreach (var outPort in startNode.Outputs)
                                {
                                    await DispatchEmitAsync(startNode.Id, outPort.Name, itemClone, cancellationToken).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                string inPortName = startNode.Inputs.FirstOrDefault()?.Name ?? "In";
                                await startNode.ExecuteAsync(inPortName, itemClone, ctx, cancellationToken).ConfigureAwait(false);
                            }

                            double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                            _telemetryTracker.RecordNodeExecution(startNode.Id, elapsedMs);
                            NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Completed);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                            _telemetryTracker.RecordNodeExecution(startNode.Id, elapsedMs);
                            NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Faulted);
                            NotifyLog(startNode.Id, $"Error en nodo inicial {startNode.Name} para {itemClone.FileName}: {ex.Message}", LogLevel.Error, itemClone.CurrentPath);
                        }
                    }, cancellationToken));
                }
            }
        }
        catch (OperationCanceledException)
        {
            NotifyLog("Modo Vigilante detenido.", LogLevel.Information);
        }
        finally
        {
            _isRunning = false;
        }
    }

    internal Task DispatchEmitAsync(string sourceNodeId, string outputPortName, FileItemContext item, CancellationToken cancellationToken)
    {
        item.Metadata["WorkflowExecutionId"] = _currentExecutionId;
        if (!string.IsNullOrWhiteSpace(GlobalOutputDir))
        {
            item.Metadata["GlobalOutputDir"] = GlobalOutputDir;
        }

        if (IsDryRun)
        {
            item.Metadata["DryRun"] = true;
        }

        if (DebugSession != null)
        {
            DebugSession.RecordSnapshot(NodeDataSnapshot.CreateOutput(sourceNodeId, outputPortName, item));
        }

        if (_startNodeIds.Contains(sourceNodeId))
        {
            _telemetryTracker.IncrementSourceItemsEmitted();
        }

        if (Checkpoint != null && !string.IsNullOrWhiteSpace(item.OriginalPath) && Checkpoint.CompletedFileKeys.Contains(item.OriginalPath))
        {
            NotifyLog($"[Checkpoint] Omitiendo archivo completado previamente: {item.FileName}", LogLevel.Debug);
            _telemetryTracker.IncrementCompletedFiles();
            return Task.CompletedTask;
        }

        if (!_outgoingEdges.TryGetValue(sourceNodeId, out var edges))
        {
            long doneFiles = _telemetryTracker.IncrementCompletedFiles();
            if (Checkpoint != null && !string.IsNullOrWhiteSpace(item.OriginalPath))
            {
                lock (_checkpointLock)
                {
                    Checkpoint.CompletedFileKeys.Add(item.OriginalPath);
                    Checkpoint.ProcessedItemsCount = doneFiles;
                    WorkflowCheckpointManager.Instance.SaveCheckpoint(Checkpoint);
                }
            }
            return Task.CompletedTask;
        }

        var matchingEdges = edges.Where(e => e.SourcePortName.Equals(outputPortName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingEdges.Count == 0)
        {
            long doneFiles = _telemetryTracker.IncrementCompletedFiles();
            if (Checkpoint != null && !string.IsNullOrWhiteSpace(item.OriginalPath))
            {
                lock (_checkpointLock)
                {
                    Checkpoint.CompletedFileKeys.Add(item.OriginalPath);
                    Checkpoint.ProcessedItemsCount = doneFiles;
                    WorkflowCheckpointManager.Instance.SaveCheckpoint(Checkpoint);
                }
            }
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
            if (_nodeInstances.TryGetValue(edge.TargetNodeId, out var targetNode))
            {
                var targetItem = isMultipleTargets ? item.DeepClone() : item;

                var task = Task.Run(async () =>
                {
                    await _concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var targetContext = new WorkflowExecutionContext(targetNode.Id, this, cancellationToken, targetItem);
                    try
                    {
                        await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);

                        if (DebugSession != null)
                        {
                            DebugSession.RecordSnapshot(NodeDataSnapshot.CreateInput(targetNode.Id, edge.TargetPortName, targetItem));
                            await DebugSession.CheckBreakpointOrStepAsync(targetNode.Id, edge.TargetPortName, targetItem, cancellationToken).ConfigureAwait(false);
                        }

                        NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Running);

                        long startTicks = Stopwatch.GetTimestamp();
                        try
                        {
                            await targetNode.ExecuteAsync(edge.TargetPortName, targetItem, targetContext, cancellationToken).ConfigureAwait(false);
                            double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                            _telemetryTracker.RecordNodeExecution(targetNode.Id, elapsedMs);
                            NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Completed);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                            _telemetryTracker.RecordNodeExecution(targetNode.Id, elapsedMs);
                            NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Faulted);
                            if (DebugSession != null)
                            {
                                await DebugSession.HandleNodeErrorAsync(targetNode.Id, edge.TargetPortName, targetItem, ex, cancellationToken).ConfigureAwait(false);
                            }
                            throw;
                        }
                    }
                    finally
                    {
                        _concurrencyThrottle.Release();
                        _telemetryTracker.IncrementProcessedItems();

                        if (!targetContext.HasEmittedAnyDownstream)
                        {
                            long doneFiles = _telemetryTracker.IncrementCompletedFiles();
                            long totalFiles = _telemetryTracker.ExpectedTotalItems;
                            long effective = Math.Max(totalFiles, doneFiles);
                            double pct = effective > 0 ? (double)doneFiles / effective * 100.0 : 0.0;
                            if (_isRunning && pct >= 100.0) pct = 99.0;
                            else if (pct > 100.0) pct = 100.0;
                            NotifyProgress(pct, $"⚡ Procesando: {doneFiles:N0}/{effective:N0} elementos ({pct:F0}%)");

                            if (Checkpoint != null && !string.IsNullOrWhiteSpace(targetItem.OriginalPath))
                            {
                                lock (_checkpointLock)
                                {
                                    Checkpoint.CompletedFileKeys.Add(targetItem.OriginalPath);
                                    Checkpoint.ProcessedItemsCount = doneFiles;
                                    WorkflowCheckpointManager.Instance.SaveCheckpoint(Checkpoint);
                                }
                            }
                        }
                    }
                }, cancellationToken);

                TrackTask(task);
            }
        }

        return Task.CompletedTask;
    }

    private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _isPaused))
        {
            await _pauseSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_pauseSemaphore.CurrentCount == 0)
            {
                _pauseSemaphore.Release();
            }
        }
    }

    private async Task DrainActiveTasksAsync(List<Exception> executionErrors)
    {
        while (true)
        {
            Task[] pending;
            lock (_tasksLock)
            {
                _activeNodeTasks.RemoveAll(t => t.IsCompleted);
                if (_activeNodeTasks.Count == 0) break;
                pending = [.. _activeNodeTasks];
            }

            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    executionErrors.Add(ex);
                }
            }
        }
    }

    internal void NotifyNodeStatus(string nodeId, NodeExecutionStatus status)
    {
        NodeStatusChanged?.Invoke(nodeId, status);
        DebugSession?.NotifyNodeStatus(nodeId, status);
    }

    internal void NotifyNodeProgress(string nodeId, double percentage, string statusMessage)
    {
        NodeProgressChanged?.Invoke(nodeId, percentage, statusMessage);
    }

    internal void NotifyProgress(double percentage, string statusMessage)
    {
        ProgressChanged?.Invoke(percentage, statusMessage);
    }

    internal void NotifyLog(
        string? nodeId,
        string message,
        LogLevel level,
        string? filePath = null,
        long fileSizeBytes = 0,
        double durationMs = 0.0,
        string? detailsJson = null,
        string? itemId = null,
        string? fileName = null)
    {
        if (IsLoggingDisabledForNode(nodeId)) return;

        string? nodeName = null;
        if (!string.IsNullOrWhiteSpace(nodeId) && _nodeInstances.TryGetValue(nodeId, out var node))
        {
            nodeName = node.Name;
        }

        var record = StructuredLogRecord.Create(
            executionId: _currentExecutionId,
            level: level,
            message: message,
            nodeId: nodeId,
            nodeName: nodeName,
            filePath: filePath,
            durationMs: durationMs,
            itemId: itemId,
            fileSizeBytes: fileSizeBytes,
            detailsJson: detailsJson,
            fileName: fileName
        );

        SqliteLogStore.Instance.EnqueueLog(record);
        StructuredLogEmitted?.Invoke(record);

        string formattedMsg = !string.IsNullOrWhiteSpace(nodeId) ? $"[{nodeId}] {message}" : message;
        LogEmitted?.Invoke(formattedMsg, level);
    }

    internal void NotifyLog(string message, LogLevel level)
    {
        NotifyLog(null, message, level);
    }
}
