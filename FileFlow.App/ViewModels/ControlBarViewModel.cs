using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using Microsoft.Win32;

namespace FileFlow.App.ViewModels;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;
    private readonly PluginLoader _pluginLoader;
    private readonly LogViewModel _logViewModel;
    private readonly NodeInspectorViewModel _nodeInspectorViewModel;
    private WorkflowExecutor? _activeExecutor;
    private WorkflowDebugSession? _activeDebugSession;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDebugging;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isPausedAtBreakpointOrError;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    private string _workflowName = "Flujo de Procesamiento de Archivos";

    [ObservableProperty]
    private string _selectedLanguage = "es-ES";

    partial void OnSelectedLanguageChanged(string value)
    {
        FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(value);
    }

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    partial void OnSelectedThemeChanged(string value)
    {
        if (Enum.TryParse<Services.AppTheme>(value, true, out var theme))
        {
            Services.ThemeManager.Instance.SetTheme(theme);
        }
    }

    public ControlBarViewModel(EditorViewModel editorViewModel, PluginLoader pluginLoader, LogViewModel logViewModel, NodeInspectorViewModel nodeInspectorViewModel)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
        _nodeInspectorViewModel = nodeInspectorViewModel;
    }

    public NodeInspectorViewModel NodeInspector => _nodeInspectorViewModel;

    [RelayCommand]
    public void ToggleInspector()
    {
        _nodeInspectorViewModel.TogglePanel();
    }

    [RelayCommand]
    public async Task ExecuteWorkflowAsync()
    {
        await RunWorkflowCoreAsync(isDebug: false);
    }

    [RelayCommand]
    public async Task DebugWorkflowAsync()
    {
        await RunWorkflowCoreAsync(isDebug: true);
    }

    private async Task RunWorkflowCoreAsync(bool isDebug)
    {
        if (IsRunning) return;

        try
        {
            IsRunning = true;
            IsDebugging = isDebug;
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
            _cts = new CancellationTokenSource();

            _editorViewModel.ClearDebugStates();

            var graph = _editorViewModel.ExportToGraphModel(WorkflowName);

            _activeExecutor = new WorkflowExecutor
            {
                IsDryRun = IsDryRun,
                MaxDegreeOfParallelism = isDebug ? 1 : Environment.ProcessorCount // En depuración ejecutamos de forma secuencial para inspección clara
            };

            if (isDebug)
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
                                IsPausedAtBreakpointOrError = true;
                                _nodeInspectorViewModel.InspectNode(node, autoOpen: true);
                            }
                            else if (status == NodeExecutionStatus.Running)
                            {
                                IsPausedAtBreakpointOrError = false;
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

            _activeExecutor.NodeStatusChanged += (nodeId, status) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                    if (node != null)
                    {
                        if (_activeDebugSession != null && _activeDebugSession.IsPaused && _activeDebugSession.CurrentPausedNodeId == nodeId)
                        {
                            return;
                        }
                        node.SetExecutionStatus(status);
                    }
                });
            };

            _activeExecutor.NodeProgressChanged += (nodeId, pct, message) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                    node?.UpdateProgress(pct, message);
                });
            };

            _activeExecutor.ProgressChanged += (pct, status) =>
            {
                _logViewModel.UpdateProgress(pct, status);
            };

            _activeExecutor.LogEmitted += (msg, level) =>
            {
                _logViewModel.AddLog(level, msg);
            };

            string startMsg = isDebug ? "Iniciando depuración del flujo..." : FileFlow.Sdk.Localization.LocalizationManager.Instance["LogStartingExecution"];
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, startMsg);

            await Task.Run(async () =>
            {
                await _activeExecutor.ExecuteAsync(graph, _pluginLoader, _cts.Token);
            }, _cts.Token);

            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogExecutionFinished"]);
        }
        catch (OperationCanceledException)
        {
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Warning, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogExecutionCancelled"]);
        }
        catch (Exception ex)
        {
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Error, $"Error de Ejecución: {ex.Message}");
            if (!isDebug)
            {
                MessageBox.Show($"Error al ejecutar el flujo: {ex.Message}", "Error de Ejecución", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsRunning = false;
            IsDebugging = false;
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
            _cts?.Dispose();
            _cts = null;
            _activeExecutor = null;
            _activeDebugSession = null;
        }
    }

    [RelayCommand]
    public void StepNext()
    {
        if (_activeDebugSession != null)
        {
            if (_activeDebugSession.IsPaused)
            {
                _activeDebugSession.StepNext();
            }
            else
            {
                _activeDebugSession.IsStepMode = true;
            }
            _activeExecutor?.Resume();
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void ContinueWorkflow()
    {
        if (_activeDebugSession != null)
        {
            _activeDebugSession.Continue();
            _activeExecutor?.Resume();
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
        }
        else if (_activeExecutor != null && IsPaused)
        {
            _activeExecutor.Resume();
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (!IsRunning || _activeExecutor == null) return;

        if (IsPaused || IsPausedAtBreakpointOrError)
        {
            ContinueWorkflow();
        }
        else
        {
            if (_activeDebugSession != null)
            {
                _activeDebugSession.Pause();
            }
            _activeExecutor.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    public void PauseDebug()
    {
        if (!IsRunning || _activeExecutor == null) return;
        _activeDebugSession?.Pause();
        _activeExecutor.Pause();
        IsPaused = true;
    }

    [RelayCommand]
    public void StopWorkflow()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _activeDebugSession?.Continue(); // Desbloquear si estaba esperando en breakpoint
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Warning, "Cancelación solicitada...");
        }
    }

    [RelayCommand]
    public void SaveWorkflow()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "flujo.json"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var graph = _editorViewModel.ExportToGraphModel(WorkflowName);
                string json = graph.ToJson();
                File.WriteAllText(saveFileDialog.FileName, json);
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Flujo guardado en {saveFileDialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el flujo: {ex.Message}", "Error al Guardar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void LoadWorkflow()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var graph = WorkflowGraph.FromJson(json);
                _editorViewModel.LoadFromGraphModel(graph);
                WorkflowName = graph.Name;
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Flujo cargado desde {openFileDialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el flujo: {ex.Message}", "Error al Cargar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
