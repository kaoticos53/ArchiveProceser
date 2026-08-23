using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FileFlow.Core.Plugins;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Engine;

public class WorkflowExecutionContext : IFlowExecutionContext
{
    private readonly string _sourceNodeId;
    private readonly WorkflowExecutor _executor;
    private readonly CancellationToken _cancellationToken;
    internal bool HasEmittedAnyDownstream { get; private set; }

    public WorkflowExecutionContext(string sourceNodeId, WorkflowExecutor executor, CancellationToken cancellationToken)
    {
        _sourceNodeId = sourceNodeId;
        _executor = executor;
        _cancellationToken = cancellationToken;
    }

    public bool IsDryRun => _executor.IsDryRun;

    public async Task EmitAsync(string outputPortName, FileItemContext item)
    {
        HasEmittedAnyDownstream = true;
        await _executor.DispatchEmitAsync(_sourceNodeId, outputPortName, item, _cancellationToken);
    }

    public void ReportProgress(double percentage, string statusMessage)
    {
        _executor.SetCustomStatusMessage(statusMessage);
        _executor.NotifyNodeProgress(_sourceNodeId, percentage, statusMessage);
        _executor.NotifyProgress(percentage, statusMessage);
    }

    public void SetTotalExpectedItems(long totalExpectedItems)
    {
        _executor.SetTotalExpectedItems(totalExpectedItems);
    }

    public void Log(string message, LogLevel level)
    {
        _executor.NotifyLog(_sourceNodeId, message, level);
    }

    public void Log(string message, LogLevel level, string? filePath, double durationMs = 0.0)
    {
        _executor.NotifyLog(_sourceNodeId, message, level, filePath, durationMs);
    }

    public void RegisterPlannedAction(PlannedAction action)
    {
        _executor.RegisterPlannedAction(action);
    }

    public void RecordJournalEntry(JournalEntry entry)
    {
        _executor.JournalService.Record(entry);
    }
}


public class WorkflowExecutor
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, IFlowNode> _nodeInstances = new();
    private readonly ConcurrentDictionary<string, List<WorkflowEdge>> _outgoingEdges = new();
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private readonly Stopwatch _stopwatch = new();

    private int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private SemaphoreSlim _concurrencyThrottle = new(Environment.ProcessorCount);
    private bool _isDryRun;
    private bool _isPaused;
    private bool _isRunning;
    private long _processedItemsCount;
    private long _totalItemsCount;
    private long _expectedTotalItems;
    private long _sourceItemsEmitted;
    private long _completedFilesCount;
    private long _processedBytesCount;
    private string _lastCustomStatusMessage = string.Empty;
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
        long doneElements = Volatile.Read(ref _completedFilesCount);
        long emittedElements = Volatile.Read(ref _sourceItemsEmitted);
        long expectedElements = Volatile.Read(ref _expectedTotalItems);
        long processedOps = Volatile.Read(ref _processedItemsCount);
        long totalOps = Volatile.Read(ref _totalItemsCount);

        long effectiveTotal = Math.Max(expectedElements, Math.Max(doneElements, emittedElements));
        long effectiveProcessed = doneElements > 0 ? doneElements : emittedElements;

        if (effectiveTotal == 0)
        {
            effectiveTotal = totalOps;
            effectiveProcessed = processedOps;
        }

        long bytes = Volatile.Read(ref _processedBytesCount);
        TimeSpan elapsed = _stopwatch.Elapsed;
        double elapsedSec = elapsed.TotalSeconds;

        double itemsPerSec = elapsedSec > 0.05 ? effectiveProcessed / elapsedSec : 0.0;
        double mbPerSec = elapsedSec > 0.05 ? (bytes / (1024.0 * 1024.0)) / elapsedSec : 0.0;

        double pct = 0.0;
        if (effectiveTotal > 0)
        {
            pct = (double)effectiveProcessed / effectiveTotal * 100.0;
            if (_isRunning && pct >= 100.0)
            {
                pct = 99.0;
            }
            else if (pct > 100.0)
            {
                pct = 100.0;
            }
        }

        string status;
        if (!_isRunning && effectiveProcessed > 0)
        {
            status = $"🟢 Completado: {effectiveProcessed:N0}/{effectiveProcessed:N0} elementos (100%)";
        }
        else if (effectiveTotal > 0)
        {
            status = $"⚡ Procesando: {effectiveProcessed:N0}/{effectiveTotal:N0} elementos ({pct:F0}%) • {itemsPerSec:F0} ops/s";
        }
        else if (effectiveProcessed > 0)
        {
            status = $"⚡ Procesando: {effectiveProcessed:N0} elementos • {itemsPerSec:F0} ops/s";
        }
        else
        {
            string customStatus = Volatile.Read(ref _lastCustomStatusMessage);
            status = !string.IsNullOrWhiteSpace(customStatus) ? customStatus : "Ejecutando...";
        }

        return new TelemetrySnapshot(
            ProcessedItems: effectiveProcessed,
            TotalItems: effectiveTotal,
            ProcessedBytes: bytes,
            ItemsPerSecond: itemsPerSec,
            MegabytesPerSecond: mbPerSec,
            Percentage: pct,
            Elapsed: elapsed,
            StatusMessage: status
        );
    }

    public void SetTotalExpectedItems(long totalExpectedItems)
    {
        Interlocked.Exchange(ref _expectedTotalItems, totalExpectedItems);
    }

    public void SetCustomStatusMessage(string message)
    {
        Volatile.Write(ref _lastCustomStatusMessage, message);
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
    private readonly ConcurrentBag<Task> _activeNodeTasks = new();

    public async Task ExecuteAsync(WorkflowGraph graph, PluginLoader loader, CancellationToken cancellationToken)
    {
        _currentExecutionId = Guid.NewGuid().ToString("N");
        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _processedItemsCount = 0;
        _totalItemsCount = 0;
        _expectedTotalItems = 0;
        _processedBytesCount = 0;
        _lastCustomStatusMessage = string.Empty;
        _isRunning = true;
        _stopwatch.Restart();
        while (_activeNodeTasks.TryTake(out _)) { }

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
            _sourceItemsEmitted = 0;
            _completedFilesCount = 0;

            List<Task> startTasks = [];
            foreach (var startNode in startNodes)
            {
                startTasks.Add(Task.Run(async () =>
                {
                    await WaitIfPausedAsync(cancellationToken);
                    var ctx = new WorkflowExecutionContext(startNode.Id, this, cancellationToken);
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
            while (_activeNodeTasks.TryTake(out var activeTask))
            {
                await activeTask.ConfigureAwait(false);
            }

            _stopwatch.Stop();
            long finalFiles = Volatile.Read(ref _completedFilesCount);
            if (finalFiles == 0) finalFiles = Volatile.Read(ref _sourceItemsEmitted);
            if (finalFiles == 0) finalFiles = Volatile.Read(ref _processedItemsCount);
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
            Interlocked.Increment(ref _sourceItemsEmitted);
        }

        if (!_outgoingEdges.TryGetValue(sourceNodeId, out var edges))
        {
            Interlocked.Increment(ref _completedFilesCount);
            return Task.CompletedTask;
        }

        var matchingEdges = edges.Where(e => e.SourcePortName.Equals(outputPortName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingEdges.Count == 0)
        {
            Interlocked.Increment(ref _completedFilesCount);
            return Task.CompletedTask;
        }

        Interlocked.Add(ref _totalItemsCount, matchingEdges.Count);
        if (item.FileSizeBytes > 0)
        {
            Interlocked.Add(ref _processedBytesCount, item.FileSizeBytes);
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
                    var targetContext = new WorkflowExecutionContext(targetNode.Id, this, cancellationToken);
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
                        Interlocked.Increment(ref _processedItemsCount);

                        if (!targetContext.HasEmittedAnyDownstream)
                        {
                            long doneFiles = Interlocked.Increment(ref _completedFilesCount);
                            long totalFiles = Volatile.Read(ref _expectedTotalItems);
                            long effective = Math.Max(totalFiles, doneFiles);
                            double pct = effective > 0 ? (double)doneFiles / effective * 100.0 : 0.0;
                            if (_isRunning && pct >= 100.0) pct = 99.0;
                            else if (pct > 100.0) pct = 100.0;
                            NotifyProgress(pct, $"⚡ Procesando: {doneFiles:N0}/{effective:N0} elementos ({pct:F0}%)");
                        }
                    }
                }, cancellationToken);

                _activeNodeTasks.Add(task);
            }
        }

        return Task.CompletedTask;
    }

    private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (_isPaused)
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

    internal void NotifyLog(string? nodeId, string message, LogLevel level, string? filePath = null, double durationMs = 0.0)
    {
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
            durationMs: durationMs
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
