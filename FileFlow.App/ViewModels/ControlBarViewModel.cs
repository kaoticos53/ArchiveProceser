using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using Microsoft.Win32;

namespace FileFlow.App.ViewModels;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;
    private readonly PluginLoader _pluginLoader;
    private readonly LogViewModel _logViewModel;
    private WorkflowExecutor? _activeExecutor;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

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

    public ControlBarViewModel(EditorViewModel editorViewModel, PluginLoader pluginLoader, LogViewModel logViewModel)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
    }

    [RelayCommand]
    public async Task ExecuteWorkflowAsync()
    {
        if (IsRunning) return;

        try
        {
            IsRunning = true;
            IsPaused = false;
            _cts = new CancellationTokenSource();

            var graph = _editorViewModel.ExportToGraphModel(WorkflowName);

            _activeExecutor = new WorkflowExecutor
            {
                IsDryRun = IsDryRun,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            _activeExecutor.ProgressChanged += (pct, status) =>
            {
                _logViewModel.UpdateProgress(pct, status);
            };

            _activeExecutor.LogEmitted += (msg, level) =>
            {
                _logViewModel.AddLog(level, msg);
            };

            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogStartingExecution"]);

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
            MessageBox.Show($"Error al ejecutar el flujo: {ex.Message}", "Error de Ejecución", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
            _activeExecutor = null;
        }
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (!IsRunning || _activeExecutor == null) return;

        if (IsPaused)
        {
            _activeExecutor.Resume();
            IsPaused = false;
        }
        else
        {
            _activeExecutor.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    public void StopWorkflow()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
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
