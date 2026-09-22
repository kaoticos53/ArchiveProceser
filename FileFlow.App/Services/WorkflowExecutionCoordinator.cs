using System.Collections.Concurrent;
using Avalonia.Threading;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

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
    int PlannedActionsCount,
    FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore? VirtualFileSystem = null
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
    private readonly ILocalizationService _loc;
    private readonly IUiDispatcher _ui;
    private readonly IUserPreferencesService _prefs;

    private WorkflowExecutor? _activeExecutor;
    private WorkflowDebugSession? _activeDebugSession;

    public WorkflowExecutor? ActiveExecutor => _activeExecutor;
    public WorkflowDebugSession? ActiveDebugSession => _activeDebugSession;
    public FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore? LastVirtualFileSystem { get; private set; }

    /// <summary>
    /// Los dos colaboradores del entorno son inyectables porque son lo único que ataba esta orquestación al
    /// proceso: el despachador de la interfaz y las preferencias del usuario. Sin ellos, ejecutar un flujo
    /// completo sólo era posible dentro de la aplicación en marcha.
    ///
    /// Del despachador, lo único que exigía de verdad el hilo de la interfaz es el despacho <b>esperado</b> del
    /// cierre —publicar el estado final de los modelos—: ése no vuelve nunca si nadie bombea el bucle, y se
    /// llevaba consigo la ejecución entera. Publicar sin esperar (<c>Post</c>) y el cronómetro del lienzo no
    /// necesitan nada, porque no hacen nada hasta que el bucle los atienda. Con <see cref="NullUiDispatcher"/>
    /// —que ejecuta en línea— el cierre termina, y es lo mismo que el <see cref="AvaloniaUiDispatcher"/> de
    /// producción hace cuando ya está sobre el hilo de la interfaz.
    ///
    /// De las preferencias se leen el directorio temporal, la limpieza de intermedios y la descarga de modelos
    /// al terminar: leerlas del proceso hacía que una prueba tocara —y pudiera escribir— la configuración real
    /// del usuario.
    /// </summary>
    public WorkflowExecutionCoordinator(
        EditorViewModel editorViewModel,
        PluginLoader pluginLoader,
        LogViewModel logViewModel,
        NodeInspectorViewModel nodeInspectorViewModel,
        ILocalizationService? localizationService = null,
        IUiDispatcher? uiDispatcher = null,
        IUserPreferencesService? userPreferencesService = null)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
        _nodeInspectorViewModel = nodeInspectorViewModel;
        _loc = localizationService ?? LocalizationManager.Instance;
        _ui = uiDispatcher ?? AvaloniaUiDispatcher.Instance;
        _prefs = userPreferencesService ?? UserPreferencesService.Instance;
    }

    public async Task<WorkflowExecutionResult> RunAsync(
        WorkflowExecutionOptions options,
        Action<bool> onBreakpointStateChanged,
        CancellationToken cancellationToken)
    {
        _editorViewModel.ClearDebugStates();
        _editorViewModel.ResetAllNodeMetrics();
        var graph = _editorViewModel.ExportToGraphModel(options.WorkflowName);

        // Qué va a hacer este flujo, antes de crear nada: la misma regla que usa el CLI, para que los dos
        // puntos de entrada no puedan decir cosas distintas del mismo grafo. Un flujo que no puede ejecutarse
        // se devuelve por el camino del fallo que ya existía —el mismo que un error de validación, que deja el
        // aviso en la consola y explica en un diálogo, y que sin esto terminaba en verde por no hacer nada—.
        var diagnosis = WorkflowDiagnosis.Analyze(graph, _pluginLoader);

        if (!diagnosis.CanRun)
        {
            return new WorkflowExecutionResult(
                Succeeded: false,
                Cancelled: false,
                ErrorMessage: diagnosis.ErrorSummary,
                JournalService: null,
                PlannedActionsCount: 0);
        }

        // Los avisos se dejan en la consola antes de arrancar, y no bloquean: el diagnóstico cuenta lo que
        // conviene saber, no lo que se puede prohibir. Cuando el flujo no puede ejecutarse no se llega aquí: el
        // error ya lo cuenta quien maneja el fallo, y repetirlo aquí serían dos veces la misma frase.
        _logViewModel.AddLog(LogLevel.Information, diagnosis.Summary);
        foreach (var warning in diagnosis.Warnings)
        {
            _logViewModel.AddLog(LogLevel.Warning, $"⚠️ {warning.Message}");
        }
        string effectiveGlobalDir = !string.IsNullOrWhiteSpace(graph.GlobalOutputDir)
            ? graph.GlobalOutputDir
            : _editorViewModel.GlobalOutputDir;

        string effectiveTempDir = !string.IsNullOrWhiteSpace(graph.TemporaryDirectory)
            ? graph.TemporaryDirectory
            : _prefs.Preferences.TemporaryDirectory;

        _activeExecutor = new WorkflowExecutor
        {
            GlobalOutputDir = effectiveGlobalDir,
            TemporaryDirectory = effectiveTempDir,
            IsDryRun = options.IsDryRun,
            MaxDegreeOfParallelism = options.IsDebug ? 1 : options.MaxParallelThreads,
            EnableCheckpointing = options.EnableCheckpointing,
            AutoCleanIntermediateTempFiles = _prefs.Preferences.AutoCleanIntermediateTempFiles
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
                _ui.Post(() =>
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
                _ui.Post(() =>
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

            LastVirtualFileSystem = _activeExecutor.VirtualFileSystem;
            return new WorkflowExecutionResult(
                    Succeeded: true,
                    Cancelled: false,
                    ErrorMessage: null,
                    JournalService: _activeExecutor.JournalService,
                    PlannedActionsCount: _activeExecutor.PlannedActions.Count,
                    VirtualFileSystem: _activeExecutor.VirtualFileSystem
                );
            }
            catch (OperationCanceledException)
            {
                LastVirtualFileSystem = _activeExecutor?.VirtualFileSystem;
                return new WorkflowExecutionResult(
                    Succeeded: false,
                    Cancelled: true,
                    ErrorMessage: null,
                    JournalService: _activeExecutor?.JournalService,
                    PlannedActionsCount: _activeExecutor?.PlannedActions.Count ?? 0,
                    VirtualFileSystem: _activeExecutor?.VirtualFileSystem
                );
            }
            catch (Exception ex)
            {
                LastVirtualFileSystem = _activeExecutor?.VirtualFileSystem;
                return new WorkflowExecutionResult(
                    Succeeded: false,
                    Cancelled: false,
                    ErrorMessage: ex.Message,
                    JournalService: _activeExecutor?.JournalService,
                    PlannedActionsCount: _activeExecutor?.PlannedActions.Count ?? 0,
                    VirtualFileSystem: _activeExecutor?.VirtualFileSystem
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

                var finalNodeStats = _activeExecutor.GetNodeTelemetryStats();
                if (finalNodeStats.Count > 0)
                {
                    foreach (var node in _editorViewModel.Nodes)
                    {
                        if (finalNodeStats.TryGetValue(node.Id, out var stats))
                        {
                            node.UpdateTelemetryStats(stats);
                        }
                    }
                }
            }

            FlushPendingUiUpdates(pendingEdgeUpdates, pendingStatusUpdates, pendingNodeProgressUpdates);
            _logViewModel.FlushAllPendingLogs();

            if (_prefs.Preferences.AutoUnloadAiModelsOnCompletion)
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
                    FileFlow.Sdk.ModelSessionRegistry.ClearAllSessions();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkflowExecutionCoordinator] Error auto-unloading AI models: {ex.Message}");
                }
            }

            await _ui.InvokeAsync(() =>
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
