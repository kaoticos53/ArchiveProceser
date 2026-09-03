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
using FileFlow.Sdk.Telemetry;

namespace FileFlow.App.ViewModels;

public record MetadataDiffItem(string Key, string? OldValue, string? NewValue, string ChangeType);

public partial class NodeInspectorViewModel : ObservableObject, IRecipient<NodeSelectedMessage>
{
    private readonly EditorViewModel _editorViewModel;
    private readonly IFileDialogService _fileDialogService;
    private readonly LogViewModel? _logViewModel;

    [ObservableProperty]
    private NodeViewModel? _inspectedNode;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveEvaluationContextFileName))]
    [NotifyPropertyChangedFor(nameof(HasActiveEvaluationSnapshot))]
    private NodeDataSnapshot? _selectedSnapshot;

    public bool HasActiveEvaluationSnapshot => SelectedSnapshot != null && !string.IsNullOrWhiteSpace(SelectedSnapshot.ItemSnapshot.FileName);

    public string ActiveEvaluationContextFileName => SelectedSnapshot != null && !string.IsNullOrWhiteSpace(SelectedSnapshot.ItemSnapshot.FileName)
        ? SelectedSnapshot.ItemSnapshot.FileName
        : (SelectedSnapshot != null && !string.IsNullOrWhiteSpace(SelectedSnapshot.ItemSnapshot.CurrentPath)
            ? Path.GetFileName(SelectedSnapshot.ItemSnapshot.CurrentPath)
            : string.Empty);

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<MetadataDiffItem> MetadataDiffs { get; } = [];

    public NodeInspectorViewModel(EditorViewModel editorViewModel, IFileDialogService fileDialogService, LogViewModel? logViewModel = null)
    {
        _editorViewModel = editorViewModel;
        _fileDialogService = fileDialogService;
        _logViewModel = logViewModel;

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
        UpdateParametersEvaluationContext();
    }

    public void InspectNodeById(string? nodeId, string? detailsJson = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        var targetNode = _editorViewModel.Nodes.FirstOrDefault(n => 
            string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Title, nodeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.NodeTypeName, nodeId, StringComparison.OrdinalIgnoreCase));

        if (targetNode != null)
        {
            InspectNode(targetNode, autoOpen: true);
        }
    }

    public void InspectLogRecord(StructuredLogRecord? log)
    {
        if (log == null) return;

        NodeViewModel? targetNode = null;

        if (!string.IsNullOrWhiteSpace(log.NodeId))
        {
            targetNode = _editorViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, log.NodeId, StringComparison.OrdinalIgnoreCase));
        }

        if (targetNode == null && !string.IsNullOrWhiteSpace(log.NodeName))
        {
            targetNode = _editorViewModel.Nodes.FirstOrDefault(n => 
                string.Equals(n.Title, log.NodeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n.NodeTypeName, log.NodeName, StringComparison.OrdinalIgnoreCase));
        }

        if (targetNode == null) return;

        InspectedNode = targetNode;
        IsOpen = true;

        // Try to locate existing snapshot matching log's ItemId or FilePath
        NodeDataSnapshot? matchingSnapshot = null;
        if (!string.IsNullOrWhiteSpace(log.ItemId))
        {
            matchingSnapshot = targetNode.OutputSnapshots.LastOrDefault(s => string.Equals(s.ItemSnapshot.IdString, log.ItemId, StringComparison.OrdinalIgnoreCase) || string.Equals(s.ItemSnapshot.ShortIdString, log.ItemId, StringComparison.OrdinalIgnoreCase))
                               ?? targetNode.InputSnapshots.LastOrDefault(s => string.Equals(s.ItemSnapshot.IdString, log.ItemId, StringComparison.OrdinalIgnoreCase) || string.Equals(s.ItemSnapshot.ShortIdString, log.ItemId, StringComparison.OrdinalIgnoreCase));
        }

        if (matchingSnapshot == null && !string.IsNullOrWhiteSpace(log.FilePath))
        {
            matchingSnapshot = targetNode.OutputSnapshots.LastOrDefault(s => string.Equals(s.ItemSnapshot.CurrentPath, log.FilePath, StringComparison.OrdinalIgnoreCase) || string.Equals(s.ItemSnapshot.OriginalPath, log.FilePath, StringComparison.OrdinalIgnoreCase))
                               ?? targetNode.InputSnapshots.LastOrDefault(s => string.Equals(s.ItemSnapshot.CurrentPath, log.FilePath, StringComparison.OrdinalIgnoreCase) || string.Equals(s.ItemSnapshot.OriginalPath, log.FilePath, StringComparison.OrdinalIgnoreCase));
        }

        if (matchingSnapshot != null)
        {
            SelectedSnapshot = matchingSnapshot;
        }
        else if (!string.IsNullOrWhiteSpace(log.FilePath) || !string.IsNullOrWhiteSpace(log.FileName) || !string.IsNullOrWhiteSpace(log.DetailsJson))
        {
            // Build synthetic snapshot from log details
            string path = !string.IsNullOrWhiteSpace(log.FilePath) ? log.FilePath : (!string.IsNullOrWhiteSpace(log.FileName) ? log.FileName : "item");
            var item = (!string.IsNullOrWhiteSpace(log.ItemId) && Guid.TryParse(log.ItemId, out var parsedGuid))
                ? new FileItemContext(path) { Id = parsedGuid }
                : new FileItemContext(path);

            if (!string.IsNullOrWhiteSpace(log.DetailsJson))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(log.DetailsJson);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            if (kvp.Value is System.Text.Json.JsonElement je)
                            {
                                if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                                    item.Metadata[kvp.Key] = je.GetString()!;
                                else if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int intVal))
                                    item.Metadata[kvp.Key] = intVal;
                                else if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetDouble(out double dblVal))
                                    item.Metadata[kvp.Key] = dblVal;
                                else if (je.ValueKind == System.Text.Json.JsonValueKind.True || je.ValueKind == System.Text.Json.JsonValueKind.False)
                                    item.Metadata[kvp.Key] = je.GetBoolean();
                                else
                                    item.Metadata[kvp.Key] = je.GetRawText();
                            }
                            else
                            {
                                item.Metadata[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
                catch { }
            }

            var syntheticSnap = NodeDataSnapshot.CreateOutput(targetNode.Id, "Log", item);
            targetNode.OutputSnapshots.Add(syntheticSnap);
            SelectedSnapshot = syntheticSnap;
        }
        else
        {
            SelectedSnapshot = targetNode.OutputSnapshots.LastOrDefault() ?? targetNode.InputSnapshots.LastOrDefault();
        }

        UpdateMetadataDiff();
        UpdateParametersEvaluationContext();
    }

    partial void OnInspectedNodeChanged(NodeViewModel? value)
    {
        UpdateParametersEvaluationContext();
    }

    partial void OnSelectedSnapshotChanged(NodeDataSnapshot? value)
    {
        UpdateMetadataDiff();
        UpdateParametersEvaluationContext();
    }

    private void UpdateParametersEvaluationContext()
    {
        if (InspectedNode == null) return;
        var context = SelectedSnapshot?.ItemSnapshot ?? InspectedNode.OutputSnapshots.LastOrDefault()?.ItemSnapshot ?? InspectedNode.InputSnapshots.LastOrDefault()?.ItemSnapshot;
        foreach (var p in InspectedNode.Parameters)
        {
            p.UpdateEvaluationContext(context);
        }
    }

    private void UpdateMetadataDiff()
    {
        MetadataDiffs.Clear();
        if (InspectedNode == null) return;

        var currentItem = SelectedSnapshot?.ItemSnapshot;
        var lastInput = (currentItem != null ? InspectedNode.InputSnapshots.LastOrDefault(s => s.ItemSnapshot.Id == currentItem.Id)?.ItemSnapshot : null)
                        ?? InspectedNode.InputSnapshots.LastOrDefault()?.ItemSnapshot;
        var lastOutput = SelectedSnapshot != null && !SelectedSnapshot.IsInput
                        ? SelectedSnapshot.ItemSnapshot
                        : (currentItem != null ? InspectedNode.OutputSnapshots.LastOrDefault(s => s.ItemSnapshot.Id == currentItem.Id)?.ItemSnapshot : null)
                           ?? InspectedNode.OutputSnapshots.LastOrDefault()?.ItemSnapshot;

        if (currentItem != null && lastInput == null && lastOutput == null)
        {
            lastOutput = currentItem;
        }

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

                var mockContext = new MockFlowExecutionContext(InspectedNode, item, _logViewModel);

                await InspectedNode.NodeInstance.ExecuteAsync(inputPort, item, mockContext, CancellationToken.None);

                InspectedNode.SetExecutionStatus(NodeExecutionStatus.Completed);
                SelectedSnapshot = InspectedNode.OutputSnapshots.LastOrDefault() ?? snapshotIn;
                UpdateMetadataDiff();

                MessageBox.Show($"Prueba completada con éxito para el nodo '{InspectedNode.Title}'.", "Prueba Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                InspectedNode.SetExecutionStatus(NodeExecutionStatus.PausedOnError, ex.Message);
                _logViewModel?.AddLog(LogLevel.Error, $"[{InspectedNode.Title}] Error durante la prueba aislada: {ex.Message}");
                MessageBox.Show($"Error durante la prueba aislada: {ex.Message}", "Fallo en la Prueba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void PreviewSpecificSnapshot(NodeDataSnapshot? snapshot)
    {
        if (snapshot != null)
        {
            SelectedSnapshot = snapshot;
        }

        OpenQuickPreview();
    }

    [RelayCommand]
    public void OpenQuickPreview()
    {
        var targetSnapshot = SelectedSnapshot ??
                             InspectedNode?.OutputSnapshots.LastOrDefault() ??
                             InspectedNode?.InputSnapshots.LastOrDefault();

        string? path = targetSnapshot?.ItemSnapshot?.CurrentPath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show("No hay un archivo válido generado o seleccionado para previsualizar.", "Vista Previa", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var previewCtx = targetSnapshot != null
            ? FileFlow.App.Preview.Core.FilePreviewContext.FromFileItemContext(targetSnapshot.ItemSnapshot)
            : new FileFlow.App.Preview.Core.FilePreviewContext(path);

        // Recopilar todos los snapshots de salida (o entrada) como hermanos (siblings) para permitir navegación continua
        var siblingsList = new List<FileFlow.App.Preview.Core.FilePreviewContext>();
        if (InspectedNode != null)
        {
            var sourceSnapshots = InspectedNode.OutputSnapshots.Count > 0 
                ? InspectedNode.OutputSnapshots 
                : InspectedNode.InputSnapshots;

            foreach (var s in sourceSnapshots)
            {
                if (!string.IsNullOrWhiteSpace(s.ItemSnapshot?.CurrentPath) && File.Exists(s.ItemSnapshot.CurrentPath))
                {
                    siblingsList.Add(FileFlow.App.Preview.Core.FilePreviewContext.FromFileItemContext(s.ItemSnapshot));
                }
            }
        }

        var win = new FileFlow.App.Preview.Views.FilePreviewerWindow();
        _ = win.ShowPreviewAsync(previewCtx, siblings: siblingsList.Count > 0 ? siblingsList : null, owner: Application.Current.MainWindow);
    }

    private class MockFlowExecutionContext : IFlowExecutionContext
    {
        private readonly NodeViewModel _nodeVm;
        private readonly FileItemContext _initialItem;
        private readonly LogViewModel? _logViewModel;

        public MockFlowExecutionContext(NodeViewModel nodeVm, FileItemContext initialItem, LogViewModel? logViewModel = null)
        {
            _nodeVm = nodeVm;
            _initialItem = initialItem;
            _logViewModel = logViewModel;
        }

        public bool IsDryRun => false;

        public Task EmitAsync(string outputPortName, FileItemContext item)
        {
            var snapOut = NodeDataSnapshot.CreateOutput(_nodeVm.Id, outputPortName, item);
            _nodeVm.AddSnapshot(snapOut);
            return Task.CompletedTask;
        }

        public void ReportProgress(double percentage, string statusMessage)
        {
            if (_logViewModel != null)
            {
                _logViewModel.StatusMessage = statusMessage;
                _logViewModel.ProgressPercentage = percentage;
            }
        }

        public void Log(string message, LogLevel level)
        {
            Log(message, level, _initialItem);
        }

        public void Log(string message, LogLevel level, string? filePath, double durationMs = 0)
        {
            var record = StructuredLogRecord.Create(
                executionId: "TEST",
                level: level,
                message: message,
                nodeId: _nodeVm.Id,
                nodeName: _nodeVm.Title,
                filePath: filePath ?? _initialItem.CurrentPath,
                durationMs: durationMs,
                fileSizeBytes: _initialItem.FileSizeBytes,
                fileName: _initialItem.FileName
            );
            FileFlow.Core.Telemetry.SqliteLogStore.Instance.EnqueueLog(record);
            _logViewModel?.AddStructuredLog(record);
        }

        public void Log(string message, LogLevel level, FileItemContext? item, double durationMs = 0, string? detailsJson = null)
        {
            var effectiveItem = item ?? _initialItem;
            if (detailsJson == null && effectiveItem?.Metadata != null && effectiveItem.Metadata.Count > 0)
            {
                try { detailsJson = System.Text.Json.JsonSerializer.Serialize(effectiveItem.Metadata); } catch { }
            }
            var record = StructuredLogRecord.Create(
                executionId: "TEST",
                level: level,
                message: message,
                nodeId: _nodeVm.Id,
                nodeName: _nodeVm.Title,
                filePath: effectiveItem?.CurrentPath,
                durationMs: durationMs,
                fileSizeBytes: effectiveItem?.FileSizeBytes ?? 0,
                detailsJson: detailsJson,
                fileName: effectiveItem?.FileName
            );
            FileFlow.Core.Telemetry.SqliteLogStore.Instance.EnqueueLog(record);
            _logViewModel?.AddStructuredLog(record);
        }

        public void Log(string message, LogLevel level, string? filePath, double durationMs, string? detailsJson, string? itemId = null)
        {
            var effectiveItem = _initialItem;
            if (detailsJson == null && effectiveItem?.Metadata != null && effectiveItem.Metadata.Count > 0)
            {
                try { detailsJson = System.Text.Json.JsonSerializer.Serialize(effectiveItem.Metadata); } catch { }
            }
            var record = StructuredLogRecord.Create(
                executionId: "TEST",
                level: level,
                message: message,
                nodeId: _nodeVm.Id,
                nodeName: _nodeVm.Title,
                filePath: filePath ?? effectiveItem?.CurrentPath,
                durationMs: durationMs,
                fileSizeBytes: effectiveItem?.FileSizeBytes ?? 0,
                detailsJson: detailsJson,
                itemId: itemId ?? effectiveItem?.IdString,
                fileName: effectiveItem?.FileName
            );
            FileFlow.Core.Telemetry.SqliteLogStore.Instance.EnqueueLog(record);
            _logViewModel?.AddStructuredLog(record);
        }

        public void RegisterPlannedAction(PlannedAction action) { }
        public void RecordJournalEntry(JournalEntry entry) { }
    }
}

