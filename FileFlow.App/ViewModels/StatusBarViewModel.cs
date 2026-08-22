using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;

namespace FileFlow.App.ViewModels;

public partial class StatusBarViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;
    private readonly ControlBarViewModel _controlBarViewModel;
    private readonly SystemPerformanceMonitor _performanceMonitor;

    [ObservableProperty]
    private int _nodeCount;

    [ObservableProperty]
    private int _connectionCount;

    [ObservableProperty]
    private string _selectedNodeName = "Ninguno";

    [ObservableProperty]
    private string _ramText = "-- MB";

    [ObservableProperty]
    private string _cpuText = "-- %";

    [ObservableProperty]
    private string _globalOutputDir = @"C:\FileFlowOutput";

    [ObservableProperty]
    private string _statusMessage = "🟢 Listo para ejecutar";

    [ObservableProperty]
    private double _zoomPercentage = 100;

    public StatusBarViewModel(
        EditorViewModel editorViewModel, 
        ControlBarViewModel controlBarViewModel, 
        SystemPerformanceMonitor performanceMonitor)
    {
        _editorViewModel = editorViewModel;
        _controlBarViewModel = controlBarViewModel;
        _performanceMonitor = performanceMonitor;

        // Subscripciones a eventos de EditorViewModel
        _editorViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.GlobalOutputDir))
            {
                GlobalOutputDir = _editorViewModel.GlobalOutputDir;
            }
            else if (e.PropertyName == nameof(EditorViewModel.SelectedNode))
            {
                UpdateSelectedNodeInfo();
            }
        };

        _editorViewModel.Nodes.CollectionChanged += (s, e) => NodeCount = _editorViewModel.Nodes.Count;
        _editorViewModel.Connections.CollectionChanged += (s, e) => ConnectionCount = _editorViewModel.Connections.Count;

        NodeCount = _editorViewModel.Nodes.Count;
        ConnectionCount = _editorViewModel.Connections.Count;
        GlobalOutputDir = _editorViewModel.GlobalOutputDir;
        UpdateSelectedNodeInfo();

        // Subscripciones a eventos de ControlBarViewModel
        _controlBarViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ControlBarViewModel.IsRunning))
            {
                StatusMessage = _controlBarViewModel.IsRunning ? "⚡ Ejecutando flujo..." : "🟢 Listo";
            }
            else if (e.PropertyName == nameof(ControlBarViewModel.IsPaused))
            {
                if (_controlBarViewModel.IsPaused)
                {
                    StatusMessage = "⏸️ Flujo pausado";
                }
            }
        };

        // Rendimiento en tiempo real
        _performanceMonitor.PerformanceUpdated += (metrics) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RamText = metrics.RamFormatted;
                CpuText = metrics.CpuFormatted;
            });
        };
    }

    private void UpdateSelectedNodeInfo()
    {
        var sel = _editorViewModel.SelectedNode;
        SelectedNodeName = sel != null ? $"{sel.Title} ({sel.NodeTypeName})" : "Ninguno";
    }

    [RelayCommand]
    public void OpenGlobalOutputFolder()
    {
        try
        {
            string folder = string.IsNullOrWhiteSpace(GlobalOutputDir) ? @"C:\FileFlowOutput" : GlobalOutputDir;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo abrir la carpeta de salida global: {ex.Message}", "Error I/O", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void OpenWorkflowSettings()
    {
        _editorViewModel.OpenWorkflowSettings();
    }

    [RelayCommand]
    public void FitToScreen()
    {
        _editorViewModel.FitToScreen();
    }
}
