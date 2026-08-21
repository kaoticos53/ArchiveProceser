using System.Collections.Concurrent;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

public class WorkflowDebugSession
{
    private readonly Lock _lock = new();
    private TaskCompletionSource _stepTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<string, List<NodeDataSnapshot>> _snapshotsByNode = new();
    private readonly HashSet<string> _breakpoints = new(StringComparer.OrdinalIgnoreCase);

    public bool IsDebugMode { get; set; } = true;
    public bool IsStepMode { get; set; }
    public bool BreakOnError { get; set; } = true;
    public bool IsPaused { get; private set; }
    public string? CurrentPausedNodeId { get; private set; }
    public NodeExecutionStatus CurrentPauseReason { get; private set; } = NodeExecutionStatus.Idle;

    public event Action<string, NodeExecutionStatus, string?>? NodeStatusChanged;
    public event Action<NodeDataSnapshot>? SnapshotRecorded;

    public IReadOnlyCollection<string> Breakpoints
    {
        get
        {
            lock (_lock)
            {
                return [.. _breakpoints];
            }
        }
    }

    public void SetBreakpoints(IEnumerable<string> nodeIds)
    {
        lock (_lock)
        {
            _breakpoints.Clear();
            foreach (var id in nodeIds)
            {
                _breakpoints.Add(id);
            }
        }
    }

    public void ToggleBreakpoint(string nodeId)
    {
        lock (_lock)
        {
            if (!_breakpoints.Add(nodeId))
            {
                _breakpoints.Remove(nodeId);
            }
        }
    }

    public bool HasBreakpoint(string nodeId)
    {
        lock (_lock)
        {
            return _breakpoints.Contains(nodeId);
        }
    }

    public void RecordSnapshot(NodeDataSnapshot snapshot)
    {
        var list = _snapshotsByNode.GetOrAdd(snapshot.NodeId, _ => []);
        lock (list)
        {
            list.Add(snapshot);
        }
        SnapshotRecorded?.Invoke(snapshot);
    }

    public IReadOnlyList<NodeDataSnapshot> GetSnapshotsForNode(string nodeId)
    {
        if (_snapshotsByNode.TryGetValue(nodeId, out var list))
        {
            lock (list)
            {
                return [.. list];
            }
        }
        return [];
    }

    public void ClearSnapshots()
    {
        _snapshotsByNode.Clear();
    }

    public void NotifyNodeStatus(string nodeId, NodeExecutionStatus status, string? details = null)
    {
        NodeStatusChanged?.Invoke(nodeId, status, details);
    }

    public async ValueTask CheckBreakpointOrStepAsync(string nodeId, string portName, FileItemContext item, CancellationToken cancellationToken)
    {
        if (!IsDebugMode) return;

        bool shouldPause = false;
        NodeExecutionStatus reason = NodeExecutionStatus.Running;

        lock (_lock)
        {
            if (IsStepMode)
            {
                shouldPause = true;
                reason = NodeExecutionStatus.PausedAtBreakpoint;
            }
            else if (_breakpoints.Contains(nodeId))
            {
                shouldPause = true;
                reason = NodeExecutionStatus.PausedAtBreakpoint;
            }
        }

        if (shouldPause)
        {
            await PauseAndAwaitResumeAsync(nodeId, reason, cancellationToken);
        }
    }

    public async ValueTask HandleNodeErrorAsync(string nodeId, string portName, FileItemContext item, Exception ex, CancellationToken cancellationToken)
    {
        var errorSnapshot = NodeDataSnapshot.CreateError(nodeId, portName, item, ex);
        RecordSnapshot(errorSnapshot);

        if (IsDebugMode && BreakOnError)
        {
            await PauseAndAwaitResumeAsync(nodeId, NodeExecutionStatus.PausedOnError, cancellationToken, ex.Message);
        }
        else
        {
            NotifyNodeStatus(nodeId, NodeExecutionStatus.Faulted, ex.Message);
        }
    }

    private async ValueTask PauseAndAwaitResumeAsync(string nodeId, NodeExecutionStatus reason, CancellationToken cancellationToken, string? details = null)
    {
        Task waitTask;
        lock (_lock)
        {
            IsPaused = true;
            CurrentPausedNodeId = nodeId;
            CurrentPauseReason = reason;
            _stepTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waitTask = _stepTcs.Task;
        }

        NotifyNodeStatus(nodeId, reason, details);

        using var registration = cancellationToken.Register(() =>
        {
            lock (_lock)
            {
                _stepTcs.TrySetCanceled(cancellationToken);
            }
        });

        await waitTask;
    }

    public void StepNext()
    {
        lock (_lock)
        {
            IsStepMode = true;
            IsPaused = false;
            if (CurrentPausedNodeId != null)
            {
                NotifyNodeStatus(CurrentPausedNodeId, NodeExecutionStatus.Running);
            }
            CurrentPausedNodeId = null;
            _stepTcs.TrySetResult();
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            IsStepMode = true;
            IsPaused = true;
        }
    }

    public void Continue()
    {
        lock (_lock)
        {
            IsStepMode = false;
            IsPaused = false;
            if (CurrentPausedNodeId != null)
            {
                NotifyNodeStatus(CurrentPausedNodeId, NodeExecutionStatus.Running);
            }
            CurrentPausedNodeId = null;
            _stepTcs.TrySetResult();
        }
    }
}
