using System.Collections.Concurrent;
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

                    try
                    {
                        await startNode.ExecuteAsync(string.Empty, dummyItem, ctx, cancellationToken);
                        NotifyNodeStatus(startNode.Id, NodeExecutionStatus.Completed);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
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

            // Wait for all asynchronously dispatched downstream node tasks to finish
            while (true)
            {
                Task[] pending;
                lock (_tasksLock)
                {
                    _activeNodeTasks.RemoveAll(t => t.IsCompleted);
                    if (_activeNodeTasks.Count == 0) break;
                    pending = [.. _activeNodeTasks];
                }
                await Task.WhenAll(pending).ConfigureAwait(false);
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

        if (!_outgoingEdges.TryGetValue(sourceNodeId, out var edges))
        {
            _telemetryTracker.IncrementCompletedFiles();
            return Task.CompletedTask;
        }

        var matchingEdges = edges.Where(e => e.SourcePortName.Equals(outputPortName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingEdges.Count == 0)
        {
            _telemetryTracker.IncrementCompletedFiles();
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

                        try
                        {
                            await targetNode.ExecuteAsync(edge.TargetPortName, targetItem, targetContext, cancellationToken).ConfigureAwait(false);
                            NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Completed);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
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
