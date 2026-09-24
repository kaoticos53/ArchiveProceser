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

    /// <summary>Reloj del latido visual: inyectable para poder medir su cadencia sin esperar fotogramas reales.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// El latido visual, ya <b>declarado</b> en el registro y aún sin arrancar: es el único de los cuatro que no
    /// late siempre, sino sólo mientras hay una ejecución en marcha. Se declara en el constructor para que el
    /// registro de la aplicación pueda enumerar los cuatro latidos desde el arranque, y cada ejecución lo arranca
    /// y lo para.
    /// </summary>
    private readonly IHeartbeat _visualFrameBeat;

    /// <summary>
    /// El periodo del <b>latido visual</b> que pinta lo que el motor publica durante la ejecución. Público para
    /// que la prueba de cadencia avance el reloj contra <i>este</i> número, no contra una copia.
    /// </summary>
    public static readonly TimeSpan VisualFlushInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>Nombre del latido en el registro: con él se busca, se mide su cadencia y se sabe cuál falló.</summary>
    public const string VisualFrameBeat = "visual-frame";

    private WorkflowExecutor? _activeExecutor;
    private WorkflowDebugSession? _activeDebugSession;

    /// <summary>
    /// Lo que el motor publica y el lienzo todavía no ha pintado. Vive en el coordinador —y no en variables
    /// locales de <see cref="RunAsync"/>— porque el <b>latido visual</b> que lo vacía es un paso con nombre
    /// propio, y un paso con nombre se puede ejercitar desde las pruebas: mientras el fotograma periódico era
    /// una lambda con todo capturado dentro de una ejecución en marcha, su camino no corría nunca en el suite.
    /// Se vacían al arrancar cada ejecución: un fotograma no puede pintar lo que quedó de la anterior.
    /// </summary>
    private readonly ConcurrentDictionary<string, (string src, string port, int count)> _pendingEdgeUpdates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, NodeExecutionStatus> _pendingStatusUpdates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (double pct, string message)> _pendingNodeProgressUpdates = new(StringComparer.OrdinalIgnoreCase);

    public WorkflowExecutor? ActiveExecutor => _activeExecutor;
    public WorkflowDebugSession? ActiveDebugSession => _activeDebugSession;

    /// <summary>
    /// Programa el <b>latido visual</b> de una ejecución —el fotograma periódico (~30 FPS) que pinta lo que el
    /// motor publica— y devuelve su temporizador para que quien lo programó lo deseche al terminar.
    ///
    /// <para>Es un paso con nombre propio, y público como los otros tres latidos, porque su <b>cadencia</b> era lo
    /// único que no se podía afirmar: se declara en el registro con el reloj inyectable, así que un periodo es un
    /// latido y medirlo no exige poner una ejecución en marcha. El latido entrega el mismo paso público que las
    /// pruebas (<see cref="FlushVisualFrame"/>) y el tick —que el reloj entrega en un hilo del grupo de hilos— se
    /// despacha al hilo de la interfaz con el despacho que ya usaba el cierre.</para>
    /// </summary>
    public IHeartbeat StartVisualHeartbeat() => _visualFrameBeat.Start();
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
        IUserPreferencesService? userPreferencesService = null,
        TimeProvider? timeProvider = null,
        IHeartbeatService? heartbeats = null)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
        _nodeInspectorViewModel = nodeInspectorViewModel;
        _loc = localizationService ?? LocalizationManager.Instance;
        _ui = uiDispatcher ?? AvaloniaUiDispatcher.Instance;
        _prefs = userPreferencesService ?? UserPreferencesService.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Declarado (no arrancado): el primer fotograma lo pide la primera ejecución.
        _visualFrameBeat = (heartbeats ?? new HeartbeatService(_timeProvider, _ui))
            .Declare(VisualFrameBeat, VisualFlushInterval, FlushVisualFrame);
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

        _pendingEdgeUpdates.Clear();
        _pendingStatusUpdates.Clear();
        _pendingNodeProgressUpdates.Clear();

        IHeartbeat visualFlushTimer = StartVisualHeartbeat();

        _activeExecutor.NodeStatusChanged += (nodeId, status) =>
        {
            if (_activeDebugSession != null && _activeDebugSession.IsPaused && _activeDebugSession.CurrentPausedNodeId == nodeId)
            {
                return;
            }
            QueueNodeStatus(nodeId, status);
        };

        _activeExecutor.NodeProgressChanged += (nodeId, pct, message) =>
        {
            QueueNodeProgress(nodeId, pct, message);
        };

        _activeExecutor.StructuredLogEmitted += (rec) =>
        {
            _logViewModel.AddStructuredLog(rec);
        };

        _activeExecutor.EdgeItemDispatched += (src, port, count) =>
        {
            QueueEdgeDispatch(src, port, count);
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
            visualFlushTimer.Dispose();

            // El cierre usa el mismo paso que el latido: lo que quedó encolado se pinta junto a las
            // estadísticas finales, sin una segunda copia de la regla que pudiera divergir del fotograma
            // periódico (era la misma veintena de líneas escrita dos veces).
            FlushVisualFrame();
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

    /// <summary>
    /// Encola el estado que publica el motor para el nodo indicado. Son las mismas llamadas que hacen los
    /// manejadores de los eventos de ejecución, expuestas para que el latido se pueda ejercitar sin motor.
    /// </summary>
    public void QueueNodeStatus(string nodeId, NodeExecutionStatus status) => _pendingStatusUpdates[nodeId] = status;

    /// <summary>Encola el progreso de un nodo, tal y como lo publica el motor.</summary>
    public void QueueNodeProgress(string nodeId, double percentage, string message) =>
        _pendingNodeProgressUpdates[nodeId] = (percentage, message);

    /// <summary>Encola el pulso de un cable (ítems despachados por un puerto de salida).</summary>
    public void QueueEdgeDispatch(string sourceNodeId, string portName, int count) =>
        _pendingEdgeUpdates[$"{sourceNodeId}:{portName}"] = (sourceNodeId, portName, count);

    /// <summary>
    /// El <b>latido visual</b> de la ejecución: pinta en el lienzo lo que el motor publicó y vacía lo encolado.
    ///
    /// <para><b>Público y sin argumentos para poder ejercitarlo desde las pruebas</b>, como el paso del barrido
    /// de la splash (hito 169). Antes era una lambda con los diccionarios capturados dentro de
    /// <see cref="RunAsync"/>, de modo que el fotograma periódico —el que hace que el lienzo se mueva
    /// <i>durante</i> la ejecución y no sólo al terminar— no se ejecutaba nunca en el suite.</para>
    ///
    /// <para>Vacía lo encolado con <c>TryRemove</c> para que sea un fotograma y no un bucle: si algo llega
    /// mientras se pinta, lo pinta el siguiente. Funciona también sin ejecución en marcha (el temporizador
    /// puede latir antes de arrancar o después de terminar) y con nodos ya borrados del lienzo, que se
    /// ignoran en lugar de reventar.</para>
    /// </summary>
    public void FlushVisualFrame()
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

        FlushPendingUiUpdates();
    }

    private void FlushPendingUiUpdates()
    {
        foreach (var key in _pendingEdgeUpdates.Keys)
        {
            if (_pendingEdgeUpdates.TryRemove(key, out var edgeInfo))
            {
                _editorViewModel.UpdateEdgeDispatched(edgeInfo.src, edgeInfo.port, edgeInfo.count);
            }
        }

        foreach (var nodeId in _pendingStatusUpdates.Keys)
        {
            if (_pendingStatusUpdates.TryRemove(nodeId, out var status))
            {
                var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                node?.SetExecutionStatus(status);
            }
        }

        foreach (var nodeId in _pendingNodeProgressUpdates.Keys)
        {
            if (_pendingNodeProgressUpdates.TryRemove(nodeId, out var progressInfo))
            {
                var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                node?.UpdateProgress(progressInfo.pct, progressInfo.message);
            }
        }
    }
}
