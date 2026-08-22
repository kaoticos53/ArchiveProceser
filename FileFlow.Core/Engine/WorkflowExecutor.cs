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
            _maxDegreeOfParallelism = Math.Max(1, value);
            var oldThrottle = _concurrencyThrottle;
            _concurrencyThrottle = new SemaphoreSlim(_maxDegreeOfParallelism);
            oldThrottle?.Dispose();
        }
    }

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

    public async Task ExecuteAsync(WorkflowGraph graph, PluginLoader loader, CancellationToken cancellationToken)
    {
        _nodeInstances.Clear();
        _outgoingEdges.Clear();
        _processedItemsCount = 0;
        _totalItemsCount = 0;

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

        await Task.WhenAll(startTasks);
        NotifyLog("Workflow execution completed successfully.", LogLevel.Information);
    }

    internal async Task DispatchEmitAsync(string sourceNodeId, string outputPortName, FileItemContext item, CancellationToken cancellationToken)
    {
        await WaitIfPausedAsync(cancellationToken);

        if (IsDryRun)
        {
            item.Metadata["DryRun"] = true;
        }

        if (DebugSession != null)
        {
            DebugSession.RecordSnapshot(NodeDataSnapshot.CreateOutput(sourceNodeId, outputPortName, item));
        }

        Interlocked.Increment(ref _totalItemsCount);

        if (!_outgoingEdges.TryGetValue(sourceNodeId, out var edges))
        {
            return;
        }

        var matchingEdges = edges.Where(e => e.SourcePortName.Equals(outputPortName, StringComparison.OrdinalIgnoreCase)).ToList();
        bool isMultipleTargets = matchingEdges.Count > 1;

        string edgeKey = $"{sourceNodeId}:{outputPortName}";
        int newCount = _edgeCounts.AddOrUpdate(edgeKey, 1, (_, c) => c + 1);
        EdgeItemDispatched?.Invoke(sourceNodeId, outputPortName, newCount);

        List<Task> dispatchTasks = [];
        foreach (var edge in matchingEdges)
        {
            if (_nodeInstances.TryGetValue(edge.TargetNodeId, out var targetNode))
            {

                var targetItem = isMultipleTargets ? item.DeepClone() : item;

                dispatchTasks.Add(Task.Run(async () =>
                {
                    await WaitIfPausedAsync(cancellationToken);
                    var targetContext = new WorkflowExecutionContext(targetNode.Id, this, cancellationToken);

                    if (DebugSession != null)
                    {
                        DebugSession.RecordSnapshot(NodeDataSnapshot.CreateInput(targetNode.Id, edge.TargetPortName, targetItem));
                        await DebugSession.CheckBreakpointOrStepAsync(targetNode.Id, edge.TargetPortName, targetItem, cancellationToken);
                    }

                    NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Running);

                    try
                    {
                        await targetNode.ExecuteAsync(edge.TargetPortName, targetItem, targetContext, cancellationToken);
                        NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Completed);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        NotifyNodeStatus(targetNode.Id, NodeExecutionStatus.Faulted);
                        if (DebugSession != null)
                        {
                            await DebugSession.HandleNodeErrorAsync(targetNode.Id, edge.TargetPortName, targetItem, ex, cancellationToken);
                        }
                        throw;
                    }
                    finally
                    {
                        long currentProcessed = Interlocked.Increment(ref _processedItemsCount);
                        double pct = _totalItemsCount > 0 ? (double)currentProcessed / _totalItemsCount * 100.0 : 100.0;
                        NotifyProgress(pct, $"Processed {currentProcessed}/{_totalItemsCount} node outputs");
                    }
                }, cancellationToken));
            }
        }

        await Task.WhenAll(dispatchTasks);
    }

    private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (_isPaused)
        {
            await _pauseSemaphore.WaitAsync(cancellationToken);
            _pauseSemaphore.Release();
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
