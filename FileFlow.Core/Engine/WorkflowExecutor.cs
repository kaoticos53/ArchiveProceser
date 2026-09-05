using System.Collections.Concurrent;
using System.Diagnostics;
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
    private readonly ConcurrentDictionary<string, WorkflowEdge[]> _indexedPortEdges = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private readonly WorkflowTelemetryTracker _telemetryTracker = new();
    private readonly WorkflowTaskTracker _taskTracker = new();
    private readonly WorkflowCheckpointHandler _checkpointHandler = new();
    private readonly WorkflowItemDispatcher _itemDispatcher;

    private int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private SemaphoreSlim _concurrencyThrottle = new(Environment.ProcessorCount);
    private bool _isDryRun;
    private bool _isPaused;
    private bool _isRunning;
    private readonly HashSet<string> _startNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _disabledLoggingNodeIds = new(StringComparer.OrdinalIgnoreCase);

    public WorkflowDebugSession? DebugSession { get; set; }
    public ExecutionJournalService JournalService { get; } = new();
    public List<PlannedAction> PlannedActions { get; } = [];

    public WorkflowCheckpointData? Checkpoint
    {
        get => _checkpointHandler.Checkpoint;
        set => _checkpointHandler.Checkpoint = value;
    }

    public bool EnableCheckpointing
    {
        get => _checkpointHandler.EnableCheckpointing;
        set => _checkpointHandler.EnableCheckpointing = value;
    }

    public bool IsRunning => _isRunning;

    public event Action<double, string>? ProgressChanged;
    public event Action<string, double, string>? NodeProgressChanged;
    public event Action<string, NodeExecutionStatus>? NodeStatusChanged;
    public event Action<string, LogLevel>? LogEmitted;
    public event Action<StructuredLogRecord>? StructuredLogEmitted;
    public event Action<string, string, int>? EdgeItemDispatched
    {
        add => _itemDispatcher.EdgeItemDispatched += value;
        remove => _itemDispatcher.EdgeItemDispatched -= value;
    }

    public WorkflowExecutor()
    {
        _itemDispatcher = new WorkflowItemDispatcher(this, _telemetryTracker, _taskTracker, _checkpointHandler);
    }

    public TelemetrySnapshot GetTelemetrySnapshot() => _telemetryTracker.GetSnapshot(_isRunning);
    public IReadOnlyDictionary<string, NodeTelemetryStats> GetNodeTelemetryStats() => _telemetryTracker.GetNodeStats();
    public void SetTotalExpectedItems(long totalExpectedItems) => _telemetryTracker.SetTotalExpectedItems(totalExpectedItems);
    public void SetCustomStatusMessage(string message) => _telemetryTracker.SetCustomStatusMessage(message);

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
    public bool IsDryRun { get => _isDryRun; set => _isDryRun = value; }
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
        if (string.IsNullOrWhiteSpace(GlobalOutputDir) && !string.IsNullOrWhiteSpace(graph.GlobalOutputDir))
        {
            GlobalOutputDir = graph.GlobalOutputDir;
        }
        _currentExecutionId = Guid.NewGuid().ToString("N");
        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _indexedPortEdges.Clear();
        _disabledLoggingNodeIds.Clear();
        foreach (var disabledId in graph.DisabledLoggingNodeIds) _disabledLoggingNodeIds.Add(disabledId);
        foreach (var nodeDto in graph.Nodes.Where(n => !n.IsLoggingEnabled)) _disabledLoggingNodeIds.Add(nodeDto.Id);

        _telemetryTracker.Reset();
        _isRunning = true;
        _taskTracker.Clear();

        try
        {
            var validator = new GraphValidator();
            var validation = validator.Validate(graph, loader);

            if (!validation.IsValid)
            {
                foreach (var err in validation.Errors) NotifyLog($"Validation Error: {err}", LogLevel.Error);
                throw new InvalidOperationException($"Workflow validation failed with {validation.Errors.Count} errors.");
            }

            if (DebugSession != null)
            {
                DebugSession.SetBreakpoints(graph.BreakpointNodeIds);
            }

            foreach (var node in validation.TopologicalOrder)
            {
                _nodeInstances[node.Id] = node;
                _outgoingEdges[node.Id] = graph.Edges.Where(e => e.SourceNodeId == node.Id).ToList();
            }

            var portGroups = graph.Edges.GroupBy(e => $"{e.SourceNodeId}:{e.SourcePortName}", StringComparer.OrdinalIgnoreCase);
            foreach (var g in portGroups)
            {
                _indexedPortEdges[g.Key] = g.ToArray();
            }

            _checkpointHandler.InitializeCheckpoint(graph.Name, _currentExecutionId, IsDryRun, (msg, lvl) => NotifyLog(msg, lvl));

            NotifyLog($"Starting workflow execution '{graph.Name}' with {validation.TopologicalOrder.Count} nodes (DryRun={IsDryRun}, Debug={DebugSession != null}).", LogLevel.Information);

            HashSet<string> targetNodeIds = graph.Edges.Select(e => e.TargetNodeId).ToHashSet();
            List<IFlowNode> startNodes = validation.TopologicalOrder.Where(n => !targetNodeIds.Contains(n.Id)).ToList();
            if (startNodes.Count == 0 && validation.TopologicalOrder.Count > 0)
            {
                startNodes.Add(validation.TopologicalOrder[0]);
            }

            _startNodeIds.Clear();
            foreach (var sn in startNodes) _startNodeIds.Add(sn.Id);

            List<Task> startTasks = [];
            foreach (var startNode in startNodes)
            {
                startTasks.Add(Task.Run(async () =>
                {
                    await WaitIfPausedAsync(cancellationToken);
                    var dummyItem = new FileItemContext(string.Empty);
                    dummyItem.Metadata["WorkflowExecutionId"] = _currentExecutionId;
                    if (!string.IsNullOrWhiteSpace(GlobalOutputDir)) dummyItem.Metadata["GlobalOutputDir"] = GlobalOutputDir;
                    if (IsDryRun) dummyItem.Metadata["DryRun"] = true;

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

            List<Exception> executionErrors = [];
            await _taskTracker.DrainActiveTasksAsync(executionErrors, remaining =>
            {
                long doneFiles = _telemetryTracker.CompletedFilesCount;
                long totalFiles = _telemetryTracker.ExpectedTotalItems;
                long effective = Math.Max(totalFiles, doneFiles + remaining);
                double pct = effective > 0 ? (double)doneFiles / effective * 100.0 : 95.0;
                if (pct >= 100.0) pct = 99.0;
                NotifyProgress(pct, FileFlow.Sdk.Localization.LocalizationManager.Instance.GetFormattedString("Log_DrainTaskQueueProgress", "⚡ Finalizando cola de tareas: {0} restante(s) ({1}/{2})...", remaining, doneFiles.ToString("N0"), effective.ToString("N0")));
            }).ConfigureAwait(false);

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

            await _taskTracker.DrainActiveTasksAsync(executionErrors, remaining =>
            {
                NotifyProgress(99.0, FileFlow.Sdk.Localization.LocalizationManager.Instance.GetFormattedString("Log_DrainPostWorkflowProgress", "⚡ Finalizando operaciones de post-flujo ({0} pendiente(s))...", remaining));
            }).ConfigureAwait(false);

            if (executionErrors.Count > 0)
            {
                throw new AggregateException(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Log_ExecutionErrorsOccurred", "Se produjeron errores durante la ejecución de los nodos del flujo."), executionErrors);
            }

            _checkpointHandler.ClearCheckpoint(graph.Name, IsDryRun);

            _telemetryTracker.Stop();
            long finalFiles = _telemetryTracker.CompletedFilesCount;
            if (finalFiles == 0) finalFiles = _telemetryTracker.SourceItemsEmitted;
            if (finalFiles == 0) finalFiles = _telemetryTracker.ProcessedItemsCount;
            NotifyProgress(100.0, FileFlow.Sdk.Localization.LocalizationManager.Instance.GetFormattedString("Log_WorkflowCompletedProgress", "🟢 Completado: {0}/{1} elementos (100%)", finalFiles.ToString("N0"), finalFiles.ToString("N0")));
            NotifyLog(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Log_ExecutionCompletedSuccessfully", "Workflow execution completed successfully."), LogLevel.Information);
        }
        finally
        {
            _isRunning = false;
        }
    }

    public async Task ExecuteWatchModeAsync(
        WorkflowGraph graph,
        PluginLoader loader,
        FolderWatcherService watcherService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(GlobalOutputDir) && !string.IsNullOrWhiteSpace(graph.GlobalOutputDir))
        {
            GlobalOutputDir = graph.GlobalOutputDir;
        }
        _currentExecutionId = Guid.NewGuid().ToString("N");
        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _disabledLoggingNodeIds.Clear();
        foreach (var disabledId in graph.DisabledLoggingNodeIds) _disabledLoggingNodeIds.Add(disabledId);
        foreach (var nodeDto in graph.Nodes.Where(n => !n.IsLoggingEnabled)) _disabledLoggingNodeIds.Add(nodeDto.Id);

        _telemetryTracker.Reset();
        _isRunning = true;

        var validator = new GraphValidator();
        var validation = validator.Validate(graph, loader);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors) NotifyLog($"Validation Error: {err}", LogLevel.Error);
            throw new InvalidOperationException($"Workflow validation failed with {validation.Errors.Count} errors.");
        }

        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _indexedPortEdges.Clear();

        foreach (var node in validation.TopologicalOrder)
        {
            _nodeInstances[node.Id] = node;
            _outgoingEdges[node.Id] = graph.Edges.Where(e => e.SourceNodeId == node.Id).ToList();
        }

        var portGroupsWatch = graph.Edges.GroupBy(e => $"{e.SourceNodeId}:{e.SourcePortName}", StringComparer.OrdinalIgnoreCase);
        foreach (var g in portGroupsWatch)
        {
            _indexedPortEdges[g.Key] = g.ToArray();
        }

        HashSet<string> targetNodeIds = graph.Edges.Select(e => e.TargetNodeId).ToHashSet();
        List<IFlowNode> startNodes = validation.TopologicalOrder.Where(n => !targetNodeIds.Contains(n.Id)).ToList();
        if (startNodes.Count == 0 && validation.TopologicalOrder.Count > 0) startNodes.Add(validation.TopologicalOrder[0]);

        _startNodeIds.Clear();
        foreach (var sn in startNodes) _startNodeIds.Add(sn.Id);

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
                    if (!string.IsNullOrWhiteSpace(GlobalOutputDir)) itemClone.Metadata["GlobalOutputDir"] = GlobalOutputDir;
                    if (IsDryRun) itemClone.Metadata["DryRun"] = true;

                    var ctx = new WorkflowExecutionContext(startNode.Id, this, cancellationToken, itemClone);

                    _taskTracker.TrackTask(Task.Run(async () =>
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
        return _itemDispatcher.DispatchEmitAsync(
            sourceNodeId,
            outputPortName,
            item,
            _nodeInstances,
            _indexedPortEdges,
            _startNodeIds,
            _currentExecutionId,
            GlobalOutputDir,
            IsDryRun,
            DebugSession,
            _concurrencyThrottle,
            WaitIfPausedAsync,
            cancellationToken);
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

    internal void NotifyNodeStatus(string nodeId, NodeExecutionStatus status)
    {
        NodeStatusChanged?.Invoke(nodeId, status);
        DebugSession?.NotifyNodeStatus(nodeId, status);
    }

    internal void NotifyNodeProgress(string nodeId, double percentage, string statusMessage) =>
        NodeProgressChanged?.Invoke(nodeId, percentage, statusMessage);

    private long _lastProgressReportTicks = 0;

    internal void NotifyProgress(double percentage, string statusMessage, bool force = false)
    {
        if (force || percentage >= 100.0 || percentage <= 0.0)
        {
            Volatile.Write(ref _lastProgressReportTicks, Environment.TickCount64);
            ProgressChanged?.Invoke(percentage, statusMessage);
            return;
        }

        long now = Environment.TickCount64;
        long last = Volatile.Read(ref _lastProgressReportTicks);
        if (now - last >= 35) // ~28 fps max for UI updates
        {
            if (Interlocked.CompareExchange(ref _lastProgressReportTicks, now, last) == last)
            {
                ProgressChanged?.Invoke(percentage, statusMessage);
            }
        }
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

    internal void NotifyLog(string message, LogLevel level) => NotifyLog(null, message, level);
}
