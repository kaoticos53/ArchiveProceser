using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileFlow.App.Messages;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

public record MetadataDiffItem(string Key, string? OldValue, string? NewValue, string ChangeType);

public partial class NodeInspectorViewModel : ObservableObject, IRecipient<NodeSelectedMessage>
{
    private readonly EditorViewModel _editorViewModel;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty]
    private NodeViewModel? _inspectedNode;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private NodeDataSnapshot? _selectedSnapshot;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<MetadataDiffItem> MetadataDiffs { get; } = [];

    public NodeInspectorViewModel(EditorViewModel editorViewModel, IFileDialogService fileDialogService)
    {
        _editorViewModel = editorViewModel;
        _fileDialogService = fileDialogService;

        WeakReferenceMessenger.Default.RegisterAll(this);

        _editorViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.SelectedNode))
            {
                if (_editorViewModel.SelectedNode != null && IsOpen)
                {
                    InspectNode(_editorViewModel.SelectedNode, autoOpen: false);
                }
            }
        };
    }

    public void Receive(NodeSelectedMessage message)
    {
        if (message.Value != null)
        {
            InspectNode(message.Value, message.AutoOpenInspector);
        }
    }

    public void InspectNode(NodeViewModel node, bool autoOpen = true)
    {
        InspectedNode = node;
        if (autoOpen)
        {
            IsOpen = true;
        }

        // Seleccionar el último snapshot si existe
        SelectedSnapshot = node.OutputSnapshots.LastOrDefault() ?? node.InputSnapshots.LastOrDefault();
        UpdateMetadataDiff();
    }

    partial void OnSelectedSnapshotChanged(NodeDataSnapshot? value)
    {
        UpdateMetadataDiff();
    }

    private void UpdateMetadataDiff()
    {
        MetadataDiffs.Clear();
        if (InspectedNode == null) return;

        var lastInput = InspectedNode.InputSnapshots.LastOrDefault()?.ItemSnapshot;
        var lastOutput = InspectedNode.OutputSnapshots.LastOrDefault()?.ItemSnapshot;

        if (lastInput == null && lastOutput == null) return;

        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (lastInput != null)
        {
            foreach (var k in lastInput.Metadata.Keys) allKeys.Add(k);
        }
        if (lastOutput != null)
        {
            foreach (var k in lastOutput.Metadata.Keys) allKeys.Add(k);
        }

        foreach (var key in allKeys.OrderBy(k => k))
        {
            object? oldVal = null;
            object? newVal = null;
            bool hasOld = lastInput?.Metadata.TryGetValue(key, out oldVal) ?? false;
            bool hasNew = lastOutput?.Metadata.TryGetValue(key, out newVal) ?? false;

            string oldStr = oldVal?.ToString() ?? "(null)";
            string newStr = newVal?.ToString() ?? "(null)";

            if (!hasOld && hasNew)
            {
                MetadataDiffs.Add(new MetadataDiffItem(key, "-", newStr, "Added"));
            }
            else if (hasOld && !hasNew)
            {
                MetadataDiffs.Add(new MetadataDiffItem(key, oldStr, "-", "Removed"));
            }
            else if (hasOld && hasNew && !string.Equals(oldStr, newStr, StringComparison.Ordinal))
            {
                MetadataDiffs.Add(new MetadataDiffItem(key, oldStr, newStr, "Modified"));
            }
            else
            {
                MetadataDiffs.Add(new MetadataDiffItem(key, oldStr, newStr, "Unchanged"));
            }
        }
    }

    [RelayCommand]
    public void TogglePanel()
    {
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    public void ClosePanel()
    {
        IsOpen = false;
    }

    [RelayCommand]
    public async Task TestNodeWithCustomFileAsync()
    {
        if (InspectedNode == null) return;

        var filePath = _fileDialogService.ShowOpenFileDialog("Seleccionar archivo para prueba aislada del nodo", "Todos los archivos (*.*)|*.*");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var item = new FileItemContext(filePath, isDirectory: false);
                string inputPort = InspectedNode.InputPorts.FirstOrDefault()?.Name ?? string.Empty;

                var snapshotIn = NodeDataSnapshot.CreateInput(InspectedNode.Id, inputPort, item);
                InspectedNode.AddSnapshot(snapshotIn);
                InspectedNode.SetExecutionStatus(NodeExecutionStatus.Running);

                var mockContext = new MockFlowExecutionContext(InspectedNode, item);

                await InspectedNode.NodeInstance.ExecuteAsync(inputPort, item, mockContext, CancellationToken.None);

                InspectedNode.SetExecutionStatus(NodeExecutionStatus.Completed);
                SelectedSnapshot = InspectedNode.OutputSnapshots.LastOrDefault() ?? snapshotIn;
                UpdateMetadataDiff();

                MessageBox.Show($"Prueba completada con éxito para el nodo '{InspectedNode.Title}'.", "Prueba Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                InspectedNode.SetExecutionStatus(NodeExecutionStatus.PausedOnError, ex.Message);
                MessageBox.Show($"Error durante la prueba aislada: {ex.Message}", "Fallo en la Prueba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private class MockFlowExecutionContext : IFlowExecutionContext
    {
        private readonly NodeViewModel _nodeVm;
        private readonly FileItemContext _initialItem;

        public MockFlowExecutionContext(NodeViewModel nodeVm, FileItemContext initialItem)
        {
            _nodeVm = nodeVm;
            _initialItem = initialItem;
        }

        public Task EmitAsync(string outputPortName, FileItemContext item)
        {
            var snapOut = NodeDataSnapshot.CreateOutput(_nodeVm.Id, outputPortName, item);
            _nodeVm.AddSnapshot(snapOut);
            return Task.CompletedTask;
        }

        public void ReportProgress(double percentage, string statusMessage) { }
        public void Log(string message, LogLevel level) { }
    }
}
