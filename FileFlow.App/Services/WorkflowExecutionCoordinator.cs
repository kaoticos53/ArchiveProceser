using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// Opciones de ejecución para la orquestación del flujo de trabajo en la interfaz de usuario.
/// </summary>
public record WorkflowExecutionOptions(
    bool IsDebug,
    bool IsDryRun,
    int MaxParallelThreads,
    string WorkflowName,
    bool IsWatchMode = false,
    FolderWatcherService? WatcherService = null,
    bool EnableCheckpointing = true
);

/// <summary>
/// Resultado del ciclo de vida de la ejecución de un flujo.
/// </summary>
public record WorkflowExecutionResult(
    bool Succeeded,
    bool Cancelled,
    string? ErrorMessage,
    ExecutionJournalService? JournalService,
    int PlannedActionsCount
);

/// <summary>
/// Coordinador de ejecución de flujos para la interfaz gráfica.
/// Maneja el ciclo de vida del executor, sesiones de depuración, timers de telemetría a 30 FPS y desacoplamiento de eventos.
/// </summary>
public sealed class WorkflowExecutionCoordinator
{
    private readonly EditorViewModel _editorViewModel;
    private readonly PluginLoader _pluginLoader;
    private readonly LogViewModel _logViewModel;
    private readonly NodeInspectorViewModel _nodeInspectorViewModel;

    private WorkflowExecutor? _activeExecutor;
    private WorkflowDebugSession? _activeDebugSession;

    public WorkflowExecutor? ActiveExecutor => _activeExecutor;
    public WorkflowDebugSession? ActiveDebugSession => _activeDebugSession;

    public WorkflowExecutionCoordinator(
        EditorViewModel editorViewModel,
        PluginLoader pluginLoader,
        LogViewModel logViewModel,
        NodeInspectorViewModel nodeInspectorViewModel)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
        _nodeInspectorViewModel = nodeInspectorViewModel;
    }

    public async Task<WorkflowExecutionResult> RunAsync(
        WorkflowExecutionOptions options,
        Action<bool> onBreakpointStateChanged,
        CancellationToken cancellationToken)
    {
        _editorViewModel.ClearDebugStates();
        var graph = _editorViewModel.ExportToGraphModel(options.WorkflowName);
        string effectiveGlobalDir = !string.IsNullOrWhiteSpace(graph.GlobalOutputDir)
            ? graph.GlobalOutputDir
            : _editorViewModel.GlobalOutputDir;

        _activeExecutor = new WorkflowExecutor
        {
            GlobalOutputDir = effectiveGlobalDir,
            IsDryRun = options.IsDryRun,
            MaxDegreeOfParallelism = options.IsDebug ? 1 : options.MaxParallelThreads,
            EnableCheckpointing = options.EnableCheckpointing
        };

        if (options.IsDebug)
        {
            _activeDebugSession = new WorkflowDebugSession
            {
                IsDebugMode = true,
                BreakOnError = true
            };

            _activeDebugSession.NodeStatusChanged += (nodeId, status, details) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                    if (node != null)
                    {
                        node.SetExecutionStatus(status, details);

                        if (status == NodeExecutionStatus.PausedAtBreakpoint || status == NodeExecutionStatus.PausedOnError)
                        {
                            onBreakpointStateChanged(true);
                            _nodeInspectorViewModel.InspectNode(node, autoOpen: true);
                        }
                        else if (status == NodeExecutionStatus.Running)
                        {
                            onBreakpointStateChanged(false);
                        }
                    }
                });
            };

            _activeDebugSession.SnapshotRecorded += (snapshot) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(snapshot.NodeId, StringComparison.OrdinalIgnoreCase));
                    node?.AddSnapshot(snapshot);
                });
            };

            _activeExecutor.DebugSession = _activeDebugSession;
        }

        var pendingEdgeUpdates = new ConcurrentDictionary<string, (string src, string port, int count)>(StringComparer.OrdinalIgnoreCase);
        var pendingStatusUpdates = new ConcurrentDictionary<string, NodeExecutionStatus>(StringComparer.OrdinalIgnoreCase);
        var pendingNodeProgressUpdates = new ConcurrentDictionary<string, (double pct, string message)>(StringComparer.OrdinalIgnoreCase);

        var visualFlushTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(33) // 30 FPS
        };

        visualFlushTimer.Tick += (_, _) =>
        {
            if (_activeExecutor != null)
            {
                var snapshot = _activeExecutor.GetTelemetrySnapshot();
                _logViewModel.ProgressPercentage = snapshot.Percentage;
                _logViewModel.StatusMessage = snapshot.StatusMessage;

                var nodeStats = _activeExecutor.GetNodeTelemetryStats();
                if (nodeStats.Count > 0)
                {
                    foreach (var node in _editorViewModel.Nodes)
                    {
                        if (nodeStats.TryGetValue(node.Id, out var stats))
                        {
                            node.UpdateTelemetryStats(stats);
                        }
                    }
                }
            }

            FlushPendingUiUpdates(pendingEdgeUpdates, pendingStatusUpdates, pendingNodeProgressUpdates);
        };
        visualFlushTimer.Start();

        _activeExecutor.NodeStatusChanged += (nodeId, status) =>
        {
            if (_activeDebugSession != null && _activeDebugSession.IsPaused && _activeDebugSession.CurrentPausedNodeId == nodeId)
            {
                return;
            }
            pendingStatusUpdates[nodeId] = status;
        };

        _activeExecutor.NodeProgressChanged += (nodeId, pct, message) =>
        {
            pendingNodeProgressUpdates[nodeId] = (pct, message);
        };

        _activeExecutor.StructuredLogEmitted += (rec) =>
        {
            _logViewModel.AddStructuredLog(rec);
        };

        _activeExecutor.EdgeItemDispatched += (src, port, count) =>
        {
            pendingEdgeUpdates[$"{src}:{port}"] = (src, port, count);
        };

        string startMsg = options.IsWatchMode
            ? FileFlow.Sdk.Localization.LocalizationManager.Instance["Log_WatchModeStarting"]
            : (options.IsDebug
                ? FileFlow.Sdk.Localization.LocalizationManager.Instance["Log_DebugStarting"]
                : (options.IsDryRun
                    ? FileFlow.Sdk.Localization.LocalizationManager.Instance["Log_DryRunStarting"]
                    : FileFlow.Sdk.Localization.LocalizationManager.Instance["LogStartingExecution"]));
        _logViewModel.AddLog(LogLevel.Information, startMsg);

        try
        {
            await Task.Run(async () =>
            {
                if (options.IsWatchMode && options.WatcherService != null)
                {
                    await _activeExecutor.ExecuteWatchModeAsync(graph, _pluginLoader, options.WatcherService, cancellationToken);
                }
                else
                {
                    await _activeExecutor.ExecuteAsync(graph, _pluginLoader, cancellationToken);
                }
            }, cancellationToken);

            return new WorkflowExecutionResult(
                Succeeded: true,
                Cancelled: false,
                ErrorMessage: null,
                JournalService: _activeExecutor.JournalService,
                PlannedActionsCount: _activeExecutor.PlannedActions.Count
            );
        }
        catch (OperationCanceledException)
        {
            return new WorkflowExecutionResult(
                Succeeded: false,
                Cancelled: true,
                ErrorMessage: null,
                JournalService: _activeExecutor?.JournalService,
                PlannedActionsCount: _activeExecutor?.PlannedActions.Count ?? 0
            );
        }
        catch (Exception ex)
        {
            return new WorkflowExecutionResult(
                Succeeded: false,
                Cancelled: false,
                ErrorMessage: ex.Message,
                JournalService: _activeExecutor?.JournalService,
                PlannedActionsCount: _activeExecutor?.PlannedActions.Count ?? 0
            );
        }
        finally
        {
            visualFlushTimer.Stop();

            if (_activeExecutor != null)
            {
                var finalSnapshot = _activeExecutor.GetTelemetrySnapshot();
                _logViewModel.ProgressPercentage = finalSnapshot.Percentage;
                _logViewModel.StatusMessage = finalSnapshot.StatusMessage;
            }

            FlushPendingUiUpdates(pendingEdgeUpdates, pendingStatusUpdates, pendingNodeProgressUpdates);
            _logViewModel.FlushAllPendingLogs();

            if (UserPreferencesService.Instance.Preferences.AutoUnloadAiModelsOnCompletion)
            {
                try
                {
                    foreach (var node in _editorViewModel.Nodes)
                    {
                        if (node.IsModelManaged && node.IsModelLoaded)
                        {
                            node.ToggleModelLoadCommand.Execute(null);
                        }
                    }
                    FileFlow.Plugin.AI.AiPluginInitializer.ClearAllSessions();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkflowExecutionCoordinator] Error auto-unloading AI models: {ex.Message}");
                }
            }

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                foreach (var node in _editorViewModel.Nodes)
                {
                    if (node.IsModelManaged)
                    {
                        node.UpdateModelStatus();
                    }
                }
            });

            // Liberación determinista de memoria, purga de pools y recorte de Working Set del proceso
            try
            {
                FileFlow.Core.Utils.MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkflowExecutionCoordinator] Error reclaiming memory: {ex.Message}");
            }

            _activeExecutor = null;
            _activeDebugSession = null;
        }
    }

    private void FlushPendingUiUpdates(
        ConcurrentDictionary<string, (string src, string port, int count)> pendingEdgeUpdates,
        ConcurrentDictionary<string, NodeExecutionStatus> pendingStatusUpdates,
        ConcurrentDictionary<string, (double pct, string message)> pendingNodeProgressUpdates)
    {
        foreach (var key in pendingEdgeUpdates.Keys)
        {
            if (pendingEdgeUpdates.TryRemove(key, out var edgeInfo))
            {
                _editorViewModel.UpdateEdgeDispatched(edgeInfo.src, edgeInfo.port, edgeInfo.count);
            }
        }

        foreach (var nodeId in pendingStatusUpdates.Keys)
        {
            if (pendingStatusUpdates.TryRemove(nodeId, out var status))
            {
                var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                node?.SetExecutionStatus(status);
            }
        }

        foreach (var nodeId in pendingNodeProgressUpdates.Keys)
        {
            if (pendingNodeProgressUpdates.TryRemove(nodeId, out var progressInfo))
            {
                var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                node?.UpdateProgress(progressInfo.pct, progressInfo.message);
            }
        }
    }
}
