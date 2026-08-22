using System.Collections.Concurrent;
using System.Threading.Channels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

public class WorkflowExecutionContext : IFlowExecutionContext
{
    private readonly string _sourceNodeId;
    private readonly WorkflowExecutor _executor;
    private readonly CancellationToken _cancellationToken;

    public WorkflowExecutionContext(string sourceNodeId, WorkflowExecutor executor, CancellationToken cancellationToken)
    {
        _sourceNodeId = sourceNodeId;
        _executor = executor;
        _cancellationToken = cancellationToken;
    }

    public bool IsDryRun => _executor.IsDryRun;

    public async Task EmitAsync(string outputPortName, FileItemContext item)
    {
        await _executor.DispatchEmitAsync(_sourceNodeId, outputPortName, item, _cancellationToken);
    }

    public void ReportProgress(double percentage, string statusMessage)
    {
        _executor.NotifyNodeProgress(_sourceNodeId, percentage, statusMessage);
        _executor.NotifyProgress(percentage, statusMessage);
    }

    public void Log(string message, LogLevel level)
    {
        _executor.NotifyLog($"[{_sourceNodeId}] {message}", level);
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

    private int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private SemaphoreSlim _concurrencyThrottle = new(Environment.ProcessorCount);
    private bool _isDryRun;
    private bool _isPaused;
    private long _processedItemsCount;
    private long _totalItemsCount;

    public WorkflowDebugSession? DebugSession { get; set; }
    public ExecutionJournalService JournalService { get; } = new();
    public List<PlannedAction> PlannedActions { get; } = [];

    public event Action<double, string>? ProgressChanged;
    public event Action<string, double, string>? NodeProgressChanged;
    public event Action<string, NodeExecutionStatus>? NodeStatusChanged;
    public event Action<string, LogLevel>? LogEmitted;
    public event Action<string, string, int>? EdgeItemDispatched;

    private readonly ConcurrentDictionary<string, int> _edgeCounts = new(StringComparer.OrdinalIgnoreCase);

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
        while (_activeNodeTasks.TryTake(out _)) { }

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

        long finalTotal = Volatile.Read(ref _processedItemsCount);
        NotifyProgress(100.0, $"Procesados {finalTotal}/{finalTotal} (100%)");
        NotifyLog("Workflow execution completed successfully.", LogLevel.Information);
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

        if (!_outgoingEdges.TryGetValue(sourceNodeId, out var edges))
        {
            return Task.CompletedTask;
        }

        var matchingEdges = edges.Where(e => e.SourcePortName.Equals(outputPortName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingEdges.Count == 0)
        {
            return Task.CompletedTask;
        }

        Interlocked.Add(ref _totalItemsCount, matchingEdges.Count);

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
                    try
                    {
                        await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                        var targetContext = new WorkflowExecutionContext(targetNode.Id, this, cancellationToken);

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
                        long currentProcessed = Interlocked.Increment(ref _processedItemsCount);
                        long total = Volatile.Read(ref _totalItemsCount);
                        double pct = total > 0 ? (double)currentProcessed / total * 100.0 : 0.0;
                        if (pct > 100.0) pct = 100.0;
                        NotifyProgress(pct, $"⚡ Procesando: {currentProcessed}/{total} ({pct:F0}%)");
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

    internal void NotifyLog(string message, LogLevel level)
    {
        LogEmitted?.Invoke(message, level);
    }
}
